using SubTerra.Core.Board;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Game;

/// <summary>
/// A game in progress. Every change goes through <see cref="Execute"/>, so the same
/// seed and the same commands always rebuild the same game — which is what makes a
/// replay, a save file and an authoritative host all work the same way.
/// </summary>
public sealed class GameState
{
    /// <summary>Two actions a turn; a third can be bought with a heart.</summary>
    public const int ActionsPerTurn = 2;

    /// <summary>Step onto the spikes and roll this or better to walk away unhurt.</summary>
    public const int SpikeTrapSafeRoll = 4;

    public const int SpikeTrapDamage = 3;

    public const int DartTrapDamage = 1;

    public const int LavaDamage = 1;

    public const int CollapseDamage = 5;

    public const int GuardianDamage = 1;

    /// <summary>Roll this or better to cut a guardian down.</summary>
    public const int AttackSuccessRoll = 4;

    /// <summary>Five meeples in the box; a sixth guardian never appears.</summary>
    public const int MaxGuardians = 5;

    /// <summary>The temple's own turn, at the end of every round.</summary>
    public const int GuardianActivationsPerRound = 2;

    /// <summary>Three keys laid in the sanctuary hall free the Artefact.</summary>
    public const int KeysToUnlock = 3;

    private readonly List<Explorer> _explorers;
    private readonly Dictionary<Cell, List<ItemKind>> _items = [];
    private readonly List<Cell> _guardians = [];
    private readonly HashSet<Cell> _rubble = [];
    private readonly HashSet<Cell> _flooded = [];

    private int _current;
    private bool _bagAnnouncedEmpty;

    public GameState(
        IEnumerable<Explorer> explorers,
        TempleBoard board,
        TileBag bag,
        Rng rng,
        Difficulty difficulty = Difficulty.Normal)
    {
        _explorers = [.. explorers];

        if (_explorers.Count == 0)
        {
            throw new ArgumentException("A game needs at least one explorer.", nameof(explorers));
        }

        Board = board;
        Bag = bag;
        Rng = rng;
        Difficulty = difficulty;
        EruptionCountdown = EruptionTrack.StartingCount(difficulty, _explorers.Count);
        ActionPoints = ActionsPerTurn;
    }

    /// <summary>A game laid out as the rulebook's setup describes, ready for turn one.</summary>
    public static GameState NewGame(
        IEnumerable<(string Name, int MaxHealth)> roster,
        ulong seed,
        Difficulty difficulty = Difficulty.Normal)
    {
        var board = TempleSetup.CreateBoard();
        var explorers = roster.Select((sheet, index) =>
            new Explorer(new ExplorerId(index), sheet.Name, sheet.MaxHealth, TempleSetup.EntranceCrossing));

        return new GameState(explorers, board, TileBag.Temple(), new Rng(seed), difficulty);
    }

    public TempleBoard Board { get; }

    public TileBag Bag { get; }

    public Rng Rng { get; }

    public IReadOnlyList<Explorer> Explorers => _explorers;

    /// <summary>What is lying on a tile, waiting to be picked up.</summary>
    public IReadOnlyList<ItemKind> ItemsOn(Cell cell) =>
        _items.TryGetValue(cell, out var items) ? items : [];

    /// <summary>Where the Ashen Legion currently stands. One cell may hold several.</summary>
    public IReadOnlyList<Cell> Guardians => _guardians;

    /// <summary>Tiles blocked by fallen rock. Nobody walks in until it is dug out.</summary>
    public IReadOnlySet<Cell> Rubble => _rubble;

    /// <summary>Where the three keys are laid, once the sanctuary has been found.</summary>
    public Cell? SanctuaryHall { get; private set; }

    /// <summary>The pocket beyond it, where the Artefact waits.</summary>
    public Cell? SanctuaryVault { get; private set; }

    public int KeysDeposited { get; private set; }

    /// <summary>Once someone lifts the Artefact the temple turns on them.</summary>
    public bool IsCursed { get; private set; }

    public Difficulty Difficulty { get; }

    /// <summary>Rounds left before the mountain is ready to blow.</summary>
    public int EruptionCountdown { get; private set; }

    /// <summary>At zero the next flame on a Peril die opens the mountain.</summary>
    public bool IsVolcanoReady => EruptionCountdown == 0;

    public bool HasErupted { get; private set; }

    /// <summary>Tiles turned to their volcano face. Nothing enters them again.</summary>
    public IReadOnlySet<Cell> Flooded => _flooded;

    public Outcome Outcome { get; private set; } = Outcome.InProgress;

    public bool IsOver => Outcome != Outcome.InProgress;

    public Explorer CurrentExplorer => _explorers[_current];

    public int ActionPoints { get; private set; }

    public bool HasOverexerted { get; private set; }

    /// <summary>Rounds completed. Every explorer plays once per round.</summary>
    public int Round { get; private set; }

    public IEnumerable<Explorer> ExplorersOn(Cell cell) =>
        _explorers.Where(explorer => explorer.Cell == cell);

    public CommandResult Execute(GameCommand command)
    {
        if (IsOver)
        {
            return CommandResult.Reject("La partie est terminée.");
        }

        var result = Dispatch(command);

        if (!result.Accepted)
        {
            return result;
        }

        var events = new List<GameEvent>(result.Events);
        events.AddRange(CheckForEnding());

        return new CommandResult(true, null, events);
    }

    private CommandResult Dispatch(GameCommand command) => command switch
    {
        Move move => ExecuteMove(move),
        Run run => ExecuteRun(run),
        Reveal reveal => ExecuteReveal(reveal),
        Explore explore => ExecuteExplore(explore),
        Heal heal => ExecuteHeal(heal),
        PickUpItem pickUp => ExecutePickUp(pickUp),
        DropItem => ExecuteDrop(),
        Attack => ExecuteAttack(),
        Dig dig => ExecuteDig(dig),
        Overexert => ExecuteOverexert(),
        EndTurn => ExecuteEndTurn(),
        _ => CommandResult.Reject($"Commande inconnue : {command.GetType().Name}."),
    };

    private CommandResult ExecuteMove(Move move)
    {
        if (!CurrentExplorer.IsPlaying)
        {
            return CommandResult.Reject("Cet Explorateur a quitté le Temple.");
        }

        if (ActionPoints < 1)
        {
            return CommandResult.Reject("Plus de point d'action.");
        }

        if (StepRejection(CurrentExplorer, CurrentExplorer.Cell, move.Direction) is { } rejection)
        {
            return CommandResult.Reject(rejection);
        }

        ActionPoints--;
        return new CommandResult(true, null, [.. Step(CurrentExplorer, move.Direction)]);
    }

    private CommandResult ExecuteRun(Run run)
    {
        if (RequireActive() is { } down)
        {
            return CommandResult.Reject(down);
        }

        if (run.Steps.Count is 0 or > 3)
        {
            return CommandResult.Reject("Courir couvre une à trois tuiles.");
        }

        if (ActionPoints < 2)
        {
            return CommandResult.Reject("Courir coûte deux points d'action.");
        }

        // The whole route is checked before a single step is taken: a run that cannot
        // finish should not leave the explorer stranded halfway for the same price.
        var cell = CurrentExplorer.Cell;

        foreach (var direction in run.Steps)
        {
            if (StepRejection(CurrentExplorer, cell, direction) is { } rejection)
            {
                return CommandResult.Reject(rejection);
            }

            cell = cell.Neighbour(direction);
        }

        ActionPoints -= 2;
        var events = new List<GameEvent>();

        foreach (var direction in run.Steps)
        {
            events.AddRange(Step(CurrentExplorer, direction));

            // Whatever they ran into may have put them on the floor.
            if (CurrentExplorer.IsDown)
            {
                break;
            }
        }

        return new CommandResult(true, null, events);
    }

    /// <summary>Why this step is not allowed, or <c>null</c> if it is.</summary>
    private string? StepRejection(Explorer explorer, Cell from, Direction direction)
    {
        var target = from.Neighbour(direction);

        if (!Board.AreConnected(from, target))
        {
            return $"{target} n'est pas reliée à {from}.";
        }

        if (_flooded.Contains(target))
        {
            return $"{target} a été engloutie par la lave.";
        }

        if (_rubble.Contains(target))
        {
            return $"{target} est bloquée par un Éboulis.";
        }

        // A bridge takes one explorer's weight at a time.
        if (Board.TileAt(target)?.Kind == TileKind.Bridge
            && ExplorersOn(target).Any(other => other != explorer))
        {
            return "Le Pont ne supporte qu'un Explorateur à la fois.";
        }

        return null;
    }

    private IEnumerable<GameEvent> Step(Explorer explorer, Direction direction)
    {
        var from = explorer.Cell;
        var target = from.Neighbour(direction);

        // Turning your back on a guardian costs a heart each. Even a last heart:
        // the explorer collapses on the tile they were heading for.
        var pursuers = _guardians.Count(guardian => guardian == from);

        if (pursuers > 0)
        {
            foreach (var wound in Wound(explorer, pursuers))
            {
                yield return wound;
            }
        }

        explorer.Cell = target;
        yield return new ExplorerMoved(explorer.Id, from, target);

        if (target == TempleSetup.EntranceExit)
        {
            explorer.HasEscaped = true;
            ActionPoints = 0;
            yield return new ExplorerEscaped(explorer.Id, explorer.Carried == ItemKind.Artefact);
            yield break;
        }

        foreach (var consequence in OnTileEntered(explorer, target))
        {
            yield return consequence;
        }
    }

    /// <summary>What the tile does to whoever just walked onto it.</summary>
    private IEnumerable<GameEvent> OnTileEntered(Explorer explorer, Cell cell)
    {
        if (Board.TileAt(cell)?.Kind != TileKind.SpikeTrap)
        {
            yield break;
        }

        var roll = Rng.RollDie();
        yield return new DieRolled(roll);

        if (roll >= SpikeTrapSafeRoll)
        {
            yield break;
        }

        foreach (var consequence in SpringSpikes(cell))
        {
            yield return consequence;
        }
    }

    /// <summary>The spikes take every explorer on the tile, not only the one who trod on them.</summary>
    private IEnumerable<GameEvent> SpringSpikes(Cell cell)
    {
        yield return new TrapSprung(cell, TileKind.SpikeTrap);

        foreach (var victim in ExplorersOn(cell).ToList())
        {
            foreach (var wound in Wound(victim, SpikeTrapDamage))
            {
                yield return wound;
            }
        }
    }

    /// <summary>Darts rake the tile and everything connected to it.</summary>
    private IEnumerable<GameEvent> SpringDarts(Cell cell)
    {
        yield return new TrapSprung(cell, TileKind.DartTrap);

        var swept = Board.ConnectedNeighbours(cell).Append(cell);

        foreach (var victim in swept.SelectMany(ExplorersOn).ToList())
        {
            foreach (var wound in Wound(victim, DartTrapDamage))
            {
                yield return wound;
            }
        }
    }

    private CommandResult ExecuteReveal(Reveal reveal)
    {
        if (RequireActive() is { } down)
        {
            return CommandResult.Reject(down);
        }

        if (ActionPoints < 1)
        {
            return CommandResult.Reject("Plus de point d'action.");
        }

        var explorer = CurrentExplorer;

        if (Bag.IsEmpty)
        {
            return CommandResult.Reject("Le sac de tuiles est vide.");
        }

        var exit = new TempleExit(explorer.Cell, reveal.Direction);
        var drawn = Bag.Draw(Rng);
        var tile = new PlacedTile(drawn, reveal.Rotation);

        if (!Board.CanPlace(exit.Target, tile, exit.From))
        {
            // Nothing has happened yet as far as the players are concerned, so the
            // tile goes straight back rather than being lost.
            Bag.Return(drawn);
            return CommandResult.Reject($"{drawn.Id} ne se raccorde pas à {exit.From} vers {reveal.Direction}.");
        }

        Board.Place(exit.Target, tile, exit.From);
        ActionPoints--;

        var events = new List<GameEvent> { new TileRevealed(exit.Target, drawn, reveal.Rotation) };
        events.AddRange(OnTilePlaced(exit.Target, drawn));

        if (Bag.IsEmpty && !_bagAnnouncedEmpty)
        {
            _bagAnnouncedEmpty = true;
            events.Add(new BagEmptied());
            events.AddRange(PlaceSanctuary());
        }

        return new CommandResult(true, null, events);
    }

    /// <summary>What a tile brings with it the moment it hits the table.</summary>
    private IEnumerable<GameEvent> OnTilePlaced(Cell cell, TileDefinition tile)
    {
        switch (tile.Kind)
        {
            case TileKind.Key:
                Drop(cell, ItemKind.Key);
                yield return new ItemAppeared(cell, ItemKind.Key);
                break;

            case TileKind.Guardian:
                if (_guardians.Count < MaxGuardians)
                {
                    _guardians.Add(cell);
                    yield return new GuardianAppeared(cell);
                }

                break;

            case TileKind.Ruins:
                _rubble.Add(cell);
                yield return new RubbleAppeared(cell);
                break;
        }
    }

    private void Drop(Cell cell, ItemKind item)
    {
        if (!_items.TryGetValue(cell, out var items))
        {
            _items[cell] = items = [];
        }

        items.Add(item);
    }

    /// <summary>
    /// Reveal and step on in one action. If the tile turns out to be unenterable the
    /// explorer stays put — but the tile is on the table either way.
    /// </summary>
    private CommandResult ExecuteExplore(Explore explore)
    {
        var revealed = ExecuteReveal(new Reveal(explore.Direction, explore.Rotation));

        if (!revealed.Accepted)
        {
            return revealed;
        }

        var events = new List<GameEvent>(revealed.Events);

        if (StepRejection(CurrentExplorer, CurrentExplorer.Cell, explore.Direction) is null)
        {
            events.AddRange(Step(CurrentExplorer, explore.Direction));
        }

        return new CommandResult(true, null, events);
    }

    private CommandResult ExecuteHeal(Heal heal)
    {
        if (RequireActive() is { } down)
        {
            return CommandResult.Reject(down);
        }

        if (ActionPoints < 1)
        {
            return CommandResult.Reject("Plus de point d'action.");
        }

        if (_explorers.FirstOrDefault(e => e.Id == heal.Target) is not { } target)
        {
            return CommandResult.Reject($"Aucun Explorateur {heal.Target}.");
        }

        if (target.Cell != CurrentExplorer.Cell)
        {
            return CommandResult.Reject("On ne soigne que sur sa propre tuile.");
        }

        if (target.Health == target.MaxHealth)
        {
            return CommandResult.Reject($"{target.Name} est déjà au maximum.");
        }

        var wasDown = target.IsDown;
        var gained = target.Heal(1);
        ActionPoints--;

        var events = new List<GameEvent> { new HealthRegained(target.Id, gained, target.Health) };

        if (wasDown)
        {
            events.Add(new ExplorerStoodUp(target.Id));
        }

        return new CommandResult(true, null, events);
    }

    private CommandResult ExecutePickUp(PickUpItem pickUp)
    {
        if (RequireActive() is { } down)
        {
            return CommandResult.Reject(down);
        }

        if (ActionPoints < 1)
        {
            return CommandResult.Reject("Plus de point d'action.");
        }

        var explorer = CurrentExplorer;

        if (explorer.Carried is { } held)
        {
            return CommandResult.Reject($"{explorer.Name} porte déjà : {held}.");
        }

        if (!_items.TryGetValue(explorer.Cell, out var items) || !items.Remove(pickUp.Item))
        {
            return CommandResult.Reject($"Rien de tel sur {explorer.Cell}.");
        }

        explorer.Carried = pickUp.Item;
        ActionPoints--;

        var events = new List<GameEvent> { new ItemPickedUp(explorer.Id, pickUp.Item, explorer.Cell) };

        // Lifting the Artefact wakes the mountain: two peril dice a turn from here on.
        if (pickUp.Item == ItemKind.Artefact && !IsCursed)
        {
            IsCursed = true;
            events.Add(new CurseFell());
        }

        return new CommandResult(true, null, events);
    }

    private CommandResult ExecuteDrop()
    {
        if (RequireActive() is { } down)
        {
            return CommandResult.Reject(down);
        }

        if (ActionPoints < 1)
        {
            return CommandResult.Reject("Plus de point d'action.");
        }

        var explorer = CurrentExplorer;

        if (explorer.Carried is not { } item)
        {
            return CommandResult.Reject($"{explorer.Name} ne porte rien.");
        }

        explorer.Carried = null;
        ActionPoints--;

        // Laying a key in the sanctuary hall is not dropping it — it is turning a lock.
        if (item == ItemKind.Key && explorer.Cell == SanctuaryHall)
        {
            KeysDeposited++;
            var turned = new List<GameEvent> { new KeyDeposited(explorer.Cell, KeysDeposited) };

            if (KeysDeposited == KeysToUnlock && SanctuaryVault is { } vault)
            {
                Drop(vault, ItemKind.Artefact);
                turned.Add(new ArtefactRevealed(vault));
            }

            return new CommandResult(true, null, turned);
        }

        Drop(explorer.Cell, item);

        return CommandResult.Accept(new ItemDropped(explorer.Id, item, explorer.Cell));
    }

    private CommandResult ExecuteAttack()
    {
        if (RequireActive() is { } down)
        {
            return CommandResult.Reject(down);
        }

        if (ActionPoints < 1)
        {
            return CommandResult.Reject("Plus de point d'action.");
        }

        var cell = CurrentExplorer.Cell;

        if (!_guardians.Contains(cell))
        {
            return CommandResult.Reject($"Aucun ennemi sur {cell}.");
        }

        ActionPoints--;

        var roll = Rng.RollDie();
        var events = new List<GameEvent> { new DieRolled(roll) };

        if (roll >= AttackSuccessRoll)
        {
            _guardians.Remove(cell);
            events.Add(new GuardianEliminated(cell));
        }

        return new CommandResult(true, null, events);
    }

    private CommandResult ExecuteDig(Dig dig)
    {
        if (RequireActive() is { } down)
        {
            return CommandResult.Reject(down);
        }

        if (ActionPoints < 2)
        {
            return CommandResult.Reject("Creuser coûte deux points d'action.");
        }

        var cell = CurrentExplorer.Cell;

        if (dig.Cell != cell && !Board.AreConnected(cell, dig.Cell))
        {
            return CommandResult.Reject($"{dig.Cell} n'est ni votre tuile ni une voisine reliée.");
        }

        if (!_rubble.Remove(dig.Cell))
        {
            return CommandResult.Reject($"Pas d'Éboulis sur {dig.Cell}.");
        }

        ActionPoints -= 2;
        return CommandResult.Accept(new RubbleCleared(dig.Cell));
    }

    /// <summary>
    /// With the bag empty there is enough of the temple on the table to know where the
    /// sanctuary lies: as far from the Entrance as it can be reached. Its hall takes
    /// the three keys, and the vault beyond it holds the Artefact.
    /// </summary>
    /// <remarks>
    /// The rules let the expedition leader choose between equal spots; the deepest
    /// row and then the lowest column stand in for that, so a game replays the same.
    /// </remarks>
    private IEnumerable<GameEvent> PlaceSanctuary()
    {
        if (SanctuaryHall is not null)
        {
            yield break;
        }

        // Deepest first: the sanctuary belongs as far from the Entrance as the temple
        // reaches. The rules let the expedition leader pick between equal spots; the
        // deepest vault, then the deepest hall, then the lowest column stand in for
        // that, so a game replays the same.
        var candidates = Board.OpenExits()
            .Select(exit => (Exit: exit, Hall: exit.Target, Vault: exit.Target.Neighbour(exit.Direction)))
            .OrderByDescending(spot => spot.Vault.Row)
            .ThenByDescending(spot => spot.Hall.Row)
            .ThenBy(spot => spot.Hall.Column);

        foreach (var (exit, hall, vault) in candidates)
        {
            if (Board.IsOccupied(vault) || !Board.Bounds.Contains(vault))
            {
                continue;
            }

            var back = exit.Direction.Opposite();

            var hallLayout = TileShapeExtensions.Match(SidesFor(exit.Direction) | SidesFor(back))!.Value;
            var vaultLayout = TileShapeExtensions.Match(SidesFor(back))!.Value;

            var hallTile = new PlacedTile(
                new TileDefinition("Sanctuary-Hall", TileKind.Sanctuary, hallLayout.Shape),
                hallLayout.Rotation);

            if (!Board.CanPlace(hall, hallTile, exit.From))
            {
                continue;
            }

            Board.Place(hall, hallTile, exit.From);
            Board.PlaceFixed(vault, new PlacedTile(
                new TileDefinition("Sanctuary-Vault", TileKind.Sanctuary, vaultLayout.Shape),
                vaultLayout.Rotation));

            SanctuaryHall = hall;
            SanctuaryVault = vault;

            yield return new SanctuaryFound(hall, vault);
            yield break;
        }
    }

    private static Sides SidesFor(Direction direction) => (Sides)(1 << (int)direction);

    /// <summary>
    /// The expedition is over once nobody left in the temple can still act: everyone
    /// has escaped, died, or is lying on the floor.
    /// </summary>
    private IEnumerable<GameEvent> CheckForEnding()
    {
        if (IsOver || _explorers.Any(explorer => explorer.IsPlaying && !explorer.IsDown))
        {
            yield break;
        }

        var escapedWithArtefact = _explorers.Any(
            explorer => explorer.HasEscaped && explorer.Carried == ItemKind.Artefact);

        if (!escapedWithArtefact)
        {
            Outcome = Outcome.ForgottenForever;
            yield return new GameEnded(Outcome);
            yield break;
        }

        // Only those who walked back out count as survivors.
        var lost = _explorers.Count(explorer => !explorer.HasEscaped);

        Outcome = lost switch
        {
            0 => Outcome.Legendary,
            1 => Outcome.Gold,
            2 => Outcome.Silver,
            _ => Outcome.Bronze,
        };

        yield return new GameEnded(Outcome);
    }

    /// <summary>
    /// The eruption marker moves one step. Once it is spent the mountain only waits
    /// for a flame; afterwards each step is another surge of lava through the temple.
    /// </summary>
    private IEnumerable<GameEvent> AdvanceEruption()
    {
        if (HasErupted)
        {
            foreach (var surge in SpreadLava())
            {
                yield return surge;
            }

            yield break;
        }

        if (EruptionCountdown == 0)
        {
            yield break;
        }

        EruptionCountdown--;
        yield return new EruptionAdvanced(EruptionCountdown);

        if (EruptionCountdown == 0)
        {
            yield return new VolcanoReady();
        }
    }

    /// <summary>
    /// The flame that comes after the countdown ends. If the Artefact is still in the
    /// sanctuary — or was never found — there is nothing left to save.
    /// </summary>
    private IEnumerable<GameEvent> Erupt()
    {
        HasErupted = true;
        yield return new VolcanoErupted();

        if (!ArtefactHasLeftTheSanctuary)
        {
            Outcome = Outcome.ForgottenForever;
            yield return new GameEnded(Outcome);
            yield break;
        }

        // The sanctuary goes first; the lava works outwards from there.
        var source = new[] { SanctuaryVault, SanctuaryHall }
            .OfType<Cell>()
            .Where(cell => !_flooded.Contains(cell))
            .ToList();

        foreach (var flooded in Flood(source))
        {
            yield return flooded;
        }
    }

    /// <summary>
    /// Whether someone is carrying the Artefact and has got it clear of the sanctuary.
    /// </summary>
    private bool ArtefactHasLeftTheSanctuary =>
        SanctuaryHall is not null
        && _explorers.Any(explorer => explorer.Carried == ItemKind.Artefact
            && explorer.Cell != SanctuaryHall
            && explorer.Cell != SanctuaryVault);

    /// <summary>Every tile touching the lava goes under, keeping the connections.</summary>
    private IEnumerable<GameEvent> SpreadLava() =>
        Flood(_flooded
            .SelectMany(Board.ConnectedNeighbours)
            .Where(cell => !_flooded.Contains(cell))
            .Distinct()
            .ToList());

    private IEnumerable<GameEvent> Flood(IReadOnlyList<Cell> cells)
    {
        if (cells.Count == 0)
        {
            yield break;
        }

        foreach (var cell in cells)
        {
            _flooded.Add(cell);
            _rubble.Remove(cell);
        }

        yield return new TilesFlooded(cells);

        // Everything standing there is gone: explorers dead, guardians burned away.
        foreach (var cell in cells)
        {
            foreach (var caught in ExplorersOn(cell).Where(explorer => explorer.IsPlaying).ToList())
            {
                caught.IsDead = true;
                caught.Carried = null;
                yield return new ExplorerKilled(caught.Id, cell);
            }

            for (var index = _guardians.Count - 1; index >= 0; index--)
            {
                if (_guardians[index] == cell)
                {
                    _guardians.RemoveAt(index);
                    yield return new GuardianEliminated(cell);
                }
            }
        }
    }

    /// <summary>Why the current explorer cannot do this, or <c>null</c> if they can.</summary>
    private string? RequireActive() => CurrentExplorer switch
    {
        { IsPlaying: false } => "Cet Explorateur a quitté le Temple.",
        { IsDown: true } => "À terre, un Explorateur ne peut que ramper.",
        _ => null,
    };

    private CommandResult ExecuteOverexert()
    {
        var explorer = CurrentExplorer;

        if (explorer.IsDown)
        {
            return CommandResult.Reject("À terre, un Explorateur ne peut pas se dépasser.");
        }

        if (HasOverexerted)
        {
            return CommandResult.Reject("Déjà dépassé ce tour-ci.");
        }

        HasOverexerted = true;
        ActionPoints++;

        var events = new List<GameEvent>();
        events.AddRange(Wound(explorer, 1));

        return new CommandResult(true, null, events);
    }

    /// <summary>
    /// Takes hearts off an explorer. Going down during one's own turn ends it on the
    /// spot, which is why this returns events rather than mutating quietly.
    /// </summary>
    private IEnumerable<GameEvent> Wound(Explorer explorer, int amount)
    {
        var lost = explorer.Wound(amount);

        if (lost > 0)
        {
            yield return new HealthLost(explorer.Id, lost, explorer.Health);
        }

        if (explorer.IsDown)
        {
            yield return new ExplorerWentDown(explorer.Id);

            if (explorer == CurrentExplorer)
            {
                ActionPoints = 0;
            }
        }
    }

    /// <summary>
    /// The temple's answer to a player's turn. Rolled once at the end of it — twice
    /// once the Artefact is out of the sanctuary, which is not implemented yet.
    /// </summary>
    private IEnumerable<GameEvent> ResolvePeril(Explorer explorer)
    {
        var face = Rng.RollPeril();
        yield return new PerilRolled(face);

        var consequences = face switch
        {
            PerilFace.Stumble => Stumble(explorer),
            PerilFace.Lava => Burn(),
            PerilFace.Collapse => Collapse(),
            PerilFace.Trap => SpringTrapsAround(explorer),
            PerilFace.WakeGuardian => WakeGuardian(explorer),
            PerilFace.ActivateGuardians => ActivateGuardians(),
            _ => [],
        };

        foreach (var consequence in consequences)
        {
            yield return consequence;
        }
    }

    /// <summary>Pushing yourself has a price, and it is collected later.</summary>
    private IEnumerable<GameEvent> Stumble(Explorer explorer) =>
        HasOverexerted ? Wound(explorer, 1) : [];

    private IEnumerable<GameEvent> Burn()
    {
        if (IsVolcanoReady && !HasErupted)
        {
            foreach (var eruption in Erupt())
            {
                yield return eruption;
            }
        }
        else if (HasErupted)
        {
            foreach (var surge in SpreadLava())
            {
                yield return surge;
            }
        }

        var burning = _explorers
            .Where(explorer => Board.TileAt(explorer.Cell)?.Kind == TileKind.Lava)
            .ToList();

        foreach (var explorer in burning)
        {
            foreach (var wound in Wound(explorer, LavaDamage))
            {
                yield return wound;
            }
        }
    }

    /// <summary>
    /// Each Ruins tile is printed with a die face. Roll it and that ruin comes down —
    /// but a ruin already buried cannot fall twice.
    /// </summary>
    private IEnumerable<GameEvent> Collapse()
    {
        var standing = Board.Tiles
            .Where(entry => entry.Value.Kind == TileKind.Ruins && !_rubble.Contains(entry.Key))
            .ToList();

        if (standing.Count == 0)
        {
            yield break;
        }

        var roll = Rng.RollDie();
        yield return new DieRolled(roll);

        var doomed = standing
            .Where(entry => entry.Value.Definition.RuinsNumber == roll)
            .Select(entry => entry.Key)
            .ToList();

        foreach (var cell in doomed)
        {
            _rubble.Add(cell);
            yield return new RuinsCollapsed(cell);
            yield return new RubbleAppeared(cell);

            // Rock does not care who it lands on.
            foreach (var crushed in ExplorersOn(cell).ToList())
            {
                foreach (var wound in Wound(crushed, CollapseDamage))
                {
                    yield return wound;
                }
            }

            for (var index = _guardians.Count - 1; index >= 0; index--)
            {
                if (_guardians[index] == cell)
                {
                    _guardians.RemoveAt(index);
                    yield return new GuardianEliminated(cell);
                }
            }
        }
    }

    /// <summary>
    /// Only the traps near the explorer whose turn it is go off, and several may go
    /// off at once.
    /// </summary>
    private IEnumerable<GameEvent> SpringTrapsAround(Explorer explorer)
    {
        var here = explorer.Cell;

        if (Board.TileAt(here)?.Kind == TileKind.SpikeTrap)
        {
            foreach (var consequence in SpringSpikes(here))
            {
                yield return consequence;
            }
        }

        var darts = Board.ConnectedNeighbours(here).Append(here)
            .Where(cell => Board.TileAt(cell)?.Kind == TileKind.DartTrap)
            .ToList();

        foreach (var cell in darts)
        {
            foreach (var consequence in SpringDarts(cell))
            {
                yield return consequence;
            }
        }
    }

    private IEnumerable<GameEvent> WakeGuardian(Explorer explorer)
    {
        if (_guardians.Count >= MaxGuardians)
        {
            yield break;
        }

        var path = Board.ShortestPath(
            explorer.Cell,
            cell => Board.TileAt(cell)?.Kind == TileKind.Guardian);

        // The explorer may already be standing in a guardian pocket.
        var cell = Board.TileAt(explorer.Cell)?.Kind == TileKind.Guardian
            ? explorer.Cell
            : path?.LastOrDefault();

        if (cell is not { } woken)
        {
            yield break;
        }

        _guardians.Add(woken);
        yield return new GuardianAppeared(woken);
    }

    /// <summary>Every guardian in play takes one action, in the order they appeared.</summary>
    private IEnumerable<GameEvent> ActivateGuardians()
    {
        for (var index = 0; index < _guardians.Count; index++)
        {
            foreach (var action in ActivateGuardian(index))
            {
                yield return action;
            }
        }
    }

    /// <summary>
    /// The first thing on the list it can do: strike, close in, or dig. Ties that the
    /// rules hand to the expedition leader are settled here by board order instead —
    /// deterministic, and a decision to revisit when the leader can be asked.
    /// </summary>
    private IEnumerable<GameEvent> ActivateGuardian(int index)
    {
        var cell = _guardians[index];

        if (ExplorersOn(cell).FirstOrDefault(explorer => !explorer.IsDown) is { } prey)
        {
            yield return new GuardianAttacked(cell, prey.Id);

            foreach (var wound in Wound(prey, GuardianDamage))
            {
                yield return wound;
            }

            yield break;
        }

        var path = Board.ShortestPath(
            cell,
            target => ExplorersOn(target).Any(explorer => !explorer.IsDown),
            target => !_rubble.Contains(target));

        if (path is [var next, ..] && !_rubble.Contains(next))
        {
            _guardians[index] = next;
            yield return new GuardianStepped(cell, next);
            yield break;
        }

        foreach (var neighbour in Board.ConnectedNeighbours(cell))
        {
            if (_rubble.Remove(neighbour))
            {
                yield return new GuardianClearedRubble(cell, neighbour);
                yield break;
            }
        }
    }

    private CommandResult ExecuteEndTurn()
    {
        var events = new List<GameEvent>();

        // Under the curse the temple answers twice for every turn taken.
        for (var roll = 0; roll < (IsCursed ? 2 : 1); roll++)
        {
            events.AddRange(ResolvePeril(CurrentExplorer));
        }

        events.Add(new TurnEnded(CurrentExplorer.Id));

        _current++;

        if (_current == _explorers.Count)
        {
            _current = 0;
            Round++;
            events.Add(new RoundEnded(Round));

            // The temple takes its own turn once everyone has taken theirs.
            for (var activation = 0; activation < GuardianActivationsPerRound; activation++)
            {
                events.AddRange(ActivateGuardians());
            }

            // Two steps under the curse: carrying the Artefact hurries the mountain.
            for (var step = 0; step < (IsCursed ? 2 : 1); step++)
            {
                events.AddRange(AdvanceEruption());
            }
        }

        // A downed explorer can only crawl: one tile, and nothing else.
        HasOverexerted = false;

        // Those who got out, and those the mountain kept, have no actions to take.
        ActionPoints = CurrentExplorer switch
        {
            { IsPlaying: false } => 0,
            { IsDown: true } => 1,
            _ => ActionsPerTurn,
        };

        events.Add(new TurnBegan(CurrentExplorer.Id, ActionPoints));

        return new CommandResult(true, null, events);
    }
}
