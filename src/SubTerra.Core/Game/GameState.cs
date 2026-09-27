using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
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

    /// <summary>The Journal tiles still set aside for the Aristocrate's Rechercher.</summary>
    private readonly Queue<TileDefinition> _journals = new(TileCatalog.CreateJournalTiles());

    /// <summary>How many times each limited ability has been played, by whom.</summary>
    private readonly Dictionary<(ExplorerId Explorer, string Ability), int> _uses = [];
    private readonly HashSet<Cell> _flooded = [];

    private int _current;
    private int _turnsThisRound;
    private bool _bagAnnouncedEmpty;

    /// <summary>
    /// The consequences still unrolling. They are kept suspended while someone is
    /// being asked to settle a tie, and resumed the moment they answer.
    /// </summary>
    private IEnumerator<GameEvent>? _script;

    /// <summary>The option the last <see cref="Decide"/> took, read straight after <see cref="Ask"/>.</summary>
    private int _answer;

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

    /// <summary>
    /// A game from a chosen party. The order of the party is the order of play, and
    /// the first seat holds the medallion.
    /// </summary>
    public static GameState NewGame(
        IEnumerable<ExplorerSheet> party,
        ulong seed,
        Difficulty difficulty = Difficulty.Normal)
    {
        var board = TempleSetup.CreateBoard();
        var explorers = party.Select((sheet, index) =>
            new Explorer(new ExplorerId(index), sheet.Name, sheet.MaxHealth, TempleSetup.EntranceCrossing, sheet));

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

    /// <summary>Free common actions an ability has just bought, if any are left.</summary>
    public GrantedActions? Granted { get; private set; }

    /// <summary>Rounds completed. Every explorer plays once per round.</summary>
    public int Round { get; private set; }

    /// <summary>
    /// Who holds the medallion. Chosen at setup and theirs for the whole expedition:
    /// play opens on them each round, and theirs is the casting vote whenever the team
    /// cannot agree.
    /// </summary>
    public Explorer Leader => _explorers[LeaderIndex];

    /// <summary>The medallion's seat. It does not move once the game has begun.</summary>
    public int LeaderIndex { get; }

    /// <summary>
    /// The tie the game is waiting on, or <c>null</c> if play may go on. While one
    /// stands, <see cref="Decide"/> is the only command that will be accepted.
    /// </summary>
    public PendingDecision? Pending { get; private set; }

    /// <summary>Whoever the rules put in charge of settling <paramref name="decision"/>.</summary>
    public Explorer Chooser(PendingDecision decision) =>
        _explorers.First(explorer => explorer.Id == decision.Chooser);

    public IEnumerable<Explorer> ExplorersOn(Cell cell) =>
        _explorers.Where(explorer => explorer.Cell == cell);

    /// <summary>
    /// A cheap hash of everything that matters. Two peers replaying the same seed and
    /// the same commands must show the same number; the day they do not, the network
    /// says so instead of quietly drifting apart.
    /// </summary>
    /// <remarks>
    /// Hashed by hand rather than with <see cref="HashCode"/>, whose seed is drawn
    /// afresh in every process — it would never agree with the machine next door.
    /// </remarks>
    public long Fingerprint
    {
        get
        {
            const ulong Basis = 14695981039346656037;
            const ulong Prime = 1099511628211;

            var hash = Basis;

            void Mix(long value) => hash = (hash ^ (ulong)value) * Prime;

            Mix((long)Rng.State);
            Mix(Round);
            Mix(_current);
            Mix(ActionPoints);
            Mix(Board.Tiles.Count);
            Mix(_rubble.Count);
            Mix(_flooded.Count);
            Mix(KeysDeposited);
            Mix(EruptionCountdown);
            Mix((long)Outcome);
            Mix(Pending is { } decision ? (long)decision.Kind + 1 : 0);
            Mix(Granted is { } granted ? ((long)granted.Action + 1) * 16 + granted.Remaining : 0);

            foreach (var ((user, ability), count) in _uses.OrderBy(use => use.Key.Explorer.Value).ThenBy(use => use.Key.Ability, StringComparer.Ordinal))
            {
                Mix(user.Value);

                foreach (var letter in ability)
                {
                    Mix(letter);
                }

                Mix(count);
            }

            Mix(Board.Tiles.Count(tile => tile.Value.Consolidated));

            foreach (var edge in Board.Demolished.OrderBy(edge => edge.Low.Row).ThenBy(edge => edge.Low.Column).ThenBy(edge => edge.High.Row).ThenBy(edge => edge.High.Column))
            {
                Mix(edge.Low.Column);
                Mix(edge.Low.Row);
                Mix(edge.High.Column);
                Mix(edge.High.Row);
            }

            foreach (var explorer in _explorers)
            {
                Mix(explorer.Health);
                Mix(explorer.IsShielded ? 1 : 0);
                Mix(explorer.IsRecovering ? 1 : 0);
                Mix(explorer.Cell.Column);
                Mix(explorer.Cell.Row);
                Mix(explorer.Carried is { } item ? (long)item + 1 : 0);
                Mix(explorer.HasEscaped ? 1 : 0);
                Mix(explorer.IsDead ? 1 : 0);
            }

            foreach (var guardian in _guardians)
            {
                Mix(guardian.Column);
                Mix(guardian.Row);
            }

            return unchecked((long)hash);
        }
    }

    /// <summary>
    /// Where the current explorer could step. An interface uses this to show what a
    /// click would do — a courtesy, not an authority: every command is checked again
    /// on the way in, and this is only ever a hint.
    /// </summary>
    public IEnumerable<Direction> Steps() =>
        DirectionExtensions.All.Where(direction =>
            StepRejection(CurrentExplorer, CurrentExplorer.Cell, direction) is null);

    /// <summary>The unconnected sides of the current explorer's tile: where one could be laid.</summary>
    public IEnumerable<Direction> Exits()
    {
        if (Bag.IsEmpty || Board.TileAt(CurrentExplorer.Cell) is not { } tile)
        {
            yield break;
        }

        foreach (var direction in DirectionExtensions.All)
        {
            var target = CurrentExplorer.Cell.Neighbour(direction);

            if ((tile.IsOpen(direction) || Board.IsDemolished(CurrentExplorer.Cell, target))
                && !Board.IsOccupied(target) && Board.Bounds.Contains(target))
            {
                yield return direction;
            }
        }
    }

    /// <summary>
    /// The tiles visible from <paramref name="origin"/> within <paramref name="range"/>
    /// tiles, the origin included (§6.4): straight lines through clear tiles, stopped
    /// by walls and by rubble — a buried tile is not seen at all.
    /// </summary>
    public IEnumerable<Cell> VisibleFrom(Cell origin, int range) =>
        Board.VisibleFrom(origin, range, _rubble.Contains);

    /// <summary>
    /// Who the current explorer could heal: with plain Soigner when
    /// <paramref name="ability"/> is <c>null</c>, or with Guérir or Ranimer. A hint for
    /// the interface, like <see cref="Steps"/>; the command is checked again anyway.
    /// </summary>
    public IEnumerable<ExplorerId> HealTargets(string? ability = null) =>
        _explorers
            .Where(target => (ability is null
                ? HealRejection(target)
                : AbilityRejection(ability) ?? RemoteHealRejection(ability, target)) is null)
            .Select(target => target.Id);

    /// <summary>The rubble within reach of the current explorer: their tile, or a neighbour.</summary>
    public IEnumerable<Cell> DigTargets() =>
        Board.ConnectedNeighbours(CurrentExplorer.Cell)
            .Append(CurrentExplorer.Cell)
            .Where(_rubble.Contains);

    public CommandResult Execute(GameCommand command)
    {
        if (IsOver)
        {
            return CommandResult.Reject("La partie est terminée.");
        }

        if (command is Decide decide)
        {
            return Resume(decide);
        }

        if (Pending is { } waiting)
        {
            return CommandResult.Reject($"{Chooser(waiting).Name} doit d'abord trancher : {waiting.Prompt}");
        }

        // Actions an ability granted are taken there and then: anything else played
        // lets them go — unless it is refused, in which case nothing happened at all.
        var granted = Granted;

        if (Granted is { } bought && !Spends(command, bought.Action))
        {
            Granted = null;
        }

        var (rejection, script) = Dispatch(command);

        if (rejection is not null)
        {
            Granted = granted;
            return CommandResult.Reject(rejection);
        }

        _script = script.Concat(CheckForEnding()).GetEnumerator();

        return new CommandResult(true, null, Pump());
    }

    /// <summary>
    /// Settles the tie on the table and lets the interrupted consequences finish. An
    /// arbitration is a command like any other, so a game still replays from its seed
    /// and its list of commands.
    /// </summary>
    private CommandResult Resume(Decide decide)
    {
        if (Pending is not { } decision)
        {
            return CommandResult.Reject("Aucun arbitrage en attente.");
        }

        if (decide.Option < 0 || decide.Option >= decision.Options.Count)
        {
            return CommandResult.Reject($"Choix hors de la liste : {decide.Option}.");
        }

        Pending = null;
        _answer = decide.Option;

        var events = new List<GameEvent> { new DecisionMade(decision, decide.Option) };
        events.AddRange(Pump());

        return new CommandResult(true, null, events);
    }

    /// <summary>
    /// Unrolls the consequences of a command until they are spent — or until one of
    /// them needs an answer, which leaves the script standing exactly where it is.
    /// </summary>
    private List<GameEvent> Pump()
    {
        var events = new List<GameEvent>();

        while (_script is { } script)
        {
            if (!script.MoveNext())
            {
                _script = null;
                break;
            }

            events.Add(script.Current);

            if (Pending is not null)
            {
                break;
            }
        }

        return events;
    }

    /// <summary>
    /// Puts a tie to whoever the rules say owns it and suspends until they answer; the
    /// caller reads <see cref="_answer"/> as soon as this is done. One option is no
    /// choice at all and settles itself, so the temple never asks a rhetorical question.
    /// </summary>
    private IEnumerable<GameEvent> Ask(
        DecisionKind kind,
        string prompt,
        ExplorerId chooser,
        IReadOnlyList<DecisionOption> options)
    {
        _answer = 0;

        if (options.Count < 2)
        {
            yield break;
        }

        Pending = new PendingDecision(kind, prompt, chooser, options);
        yield return new DecisionRequired(Pending);

        // Execution picks up here once Decide has filled in _answer.
    }

    /// <summary>
    /// What a command amounts to: a refusal, or the consequences waiting to unroll.
    /// Those stay lazy, which is what lets an arbitration stop them halfway.
    /// </summary>
    private readonly record struct Script(string? Rejection, IEnumerable<GameEvent> Events)
    {
        public static Script Refuse(string reason) => new(reason, []);

        public static Script Of(IEnumerable<GameEvent> events) => new(null, events);

        public static Script Of(params GameEvent[] events) => new(null, events);
    }

    private Script Dispatch(GameCommand command) => command switch
    {
        Move move => ExecuteMove(move),
        Run run => ExecuteRun(run),
        Reveal reveal => ExecuteReveal(reveal),
        Explore explore => ExecuteExplore(explore),
        Heal heal => ExecuteHeal(heal),
        UseAbility use => ExecuteAbility(use),
        PickUpItem pickUp => ExecutePickUp(pickUp),
        DropItem => ExecuteDrop(),
        Attack => ExecuteAttack(),
        Dig dig => ExecuteDig(dig),
        Overexert => ExecuteOverexert(),
        EndTurn => ExecuteEndTurn(),
        _ => Script.Refuse($"Commande inconnue : {command.GetType().Name}."),
    };

    private Script ExecuteMove(Move move)
    {
        if (!CurrentExplorer.IsPlaying)
        {
            return Script.Refuse("Cet Explorateur a quitté le Temple.");
        }

        if (CannotPay(GrantedAction.Move, 1))
        {
            return Script.Refuse("Plus de point d'action.");
        }

        if (StepRejection(CurrentExplorer, CurrentExplorer.Cell, move.Direction) is { } rejection)
        {
            return Script.Refuse(rejection);
        }

        Pay(GrantedAction.Move, 1);
        return Script.Of(Step(CurrentExplorer, move.Direction));
    }

    private Script ExecuteRun(Run run)
    {
        if (RequireActive() is { } down)
        {
            return Script.Refuse(down);
        }

        if (run.Steps.Count is 0 or > 3)
        {
            return Script.Refuse("Courir couvre une à trois tuiles.");
        }

        if (ActionPoints < 2)
        {
            return Script.Refuse("Courir coûte deux points d'action.");
        }

        // The whole route is checked before a single step is taken: a run that cannot
        // finish should not leave the explorer stranded halfway for the same price.
        var cell = CurrentExplorer.Cell;

        foreach (var direction in run.Steps)
        {
            if (StepRejection(CurrentExplorer, cell, direction) is { } rejection)
            {
                return Script.Refuse(rejection);
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

        return Script.Of(events);
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

        if (_rubble.Contains(target) && !Has(explorer, AbilityIds.Agile))
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

            if (explorer == CurrentExplorer)
            {
                ActionPoints = 0;
            }

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
        if (Board.TileAt(cell)?.Kind != TileKind.SpikeTrap || IsWatched(cell))
        {
            yield break;
        }

        var die = new Rolled<int>();

        foreach (var rolling in RollDie(die))
        {
            yield return rolling;
        }

        if (die.Value >= SpikeTrapSafeRoll)
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

        foreach (var victim in ExplorersOn(cell).Where(victim => !IsWatched(victim.Cell)).ToList())
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

        foreach (var victim in swept.SelectMany(ExplorersOn).Where(victim => !IsWatched(victim.Cell)).ToList())
        {
            foreach (var wound in Wound(victim, DartTrapDamage))
            {
                yield return wound;
            }
        }
    }

    private Script ExecuteReveal(Reveal reveal)
    {
        if (RequireActive() is { } down)
        {
            return Script.Refuse(down);
        }

        if (CannotPay(GrantedAction.Reveal, 1))
        {
            return Script.Refuse("Plus de point d'action.");
        }

        var explorer = CurrentExplorer;

        if (Bag.IsEmpty)
        {
            return Script.Refuse("Le sac de tuiles est vide.");
        }

        var exit = new TempleExit(explorer.Cell, reveal.Direction);

        // The exit is judged before the bag is touched: drawing a tile only to put it
        // back would still have turned the Rng, and a peer that refused the command
        // without drawing would no longer be playing the same game.
        if (!Board.OpenExits().Contains(exit))
        {
            return Script.Refuse($"Aucune issue à dégager en {exit.From} vers {reveal.Direction.Name()}.");
        }

        Pay(GrantedAction.Reveal, 1);

        return Script.Of(DrawAndLay(exit));
    }

    /// <summary>
    /// Draws the tile for an exit — two for the Archéologue, who keeps one and puts
    /// the other back (Érudite) — then lays it.
    /// </summary>
    private IEnumerable<GameEvent> DrawAndLay(TempleExit exit)
    {
        var drawn = Bag.Draw(Rng);

        if (Has(CurrentExplorer, AbilityIds.Erudite) && !Bag.IsEmpty)
        {
            TileDefinition[] pair = [drawn, Bag.Draw(Rng)];

            foreach (var asking in Ask(
                DecisionKind.TileChoice,
                $"Érudite : quelle tuile poser en {exit.Target} ? L'autre retourne au sac.",
                CurrentExplorer.Id,
                [.. pair.Select(tile => new DecisionOption(
                    $"{tile.Kind.Name()} ({tile.OpenSides.OpeningCount()} galeries)",
                    exit.Target,
                    Tile: Orientations(exit, tile)[0]))]))
            {
                yield return asking;
            }

            drawn = pair[_answer];
            var putBack = pair[1 - _answer];
            Bag.Return(putBack);

            yield return new AbilityUsed(CurrentExplorer.Id, AbilityIds.Erudite);
            yield return new TileReturned(putBack);
        }

        foreach (var consequence in RevealScript(exit, drawn))
        {
            yield return consequence;
        }
    }

    /// <summary>
    /// How the drawn tile is laid, what it brings with it — and, if that was the last
    /// one in the bag, the Sanctuary at the far end of the temple.
    /// </summary>
    private IEnumerable<GameEvent> RevealScript(TempleExit exit, TileDefinition drawn)
    {
        var cell = exit.Target;
        var orientations = Orientations(exit, drawn);

        foreach (var asking in Ask(
            DecisionKind.TileOrientation,
            $"La tuile sort du sac : dans quel sens la poser en {cell} ?",
            CurrentExplorer.Id,
            [.. orientations.Select(tile => new DecisionOption(Openings(tile), cell, Tile: tile))]))
        {
            yield return asking;
        }

        var laid = orientations[_answer];

        Board.Place(cell, laid, exit.From);
        yield return new TileRevealed(cell, drawn, laid.Rotation);

        foreach (var consequence in OnTilePlaced(cell, drawn))
        {
            yield return consequence;
        }

        if (!Bag.IsEmpty || _bagAnnouncedEmpty)
        {
            yield break;
        }

        _bagAnnouncedEmpty = true;
        yield return new BagEmptied();

        foreach (var found in PlaceSanctuary())
        {
            yield return found;
        }
    }

    /// <summary>
    /// The ways the drawn tile could lie on that exit. One entry per distinct layout:
    /// a corridor laid across a passage joins up the same way whichever end faces the
    /// tile it came from, and the temple never asks a question with one answer.
    /// </summary>
    private List<PlacedTile> Orientations(TempleExit exit, TileDefinition drawn)
    {
        var orientations = new List<PlacedTile>();

        for (var rotation = 0; rotation < 4; rotation++)
        {
            var tile = new PlacedTile(drawn, rotation);

            if (Board.CanPlace(exit.Target, tile, exit.From)
                && !orientations.Any(other => other.OpenSides == tile.OpenSides))
            {
                orientations.Add(tile);
            }
        }

        return orientations;
    }

    /// <summary>Where a tile leads once turned, which is all there is to choose between.</summary>
    private static string Openings(PlacedTile tile) =>
        "Galeries : " + string.Join(" · ", DirectionExtensions.All.Where(tile.IsOpen).Select(side => side.Name()));

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
    private Script ExecuteExplore(Explore explore)
    {
        var revealed = ExecuteReveal(new Reveal(explore.Direction));

        return revealed.Rejection is not null
            ? revealed
            : Script.Of(ExploreScript(revealed.Events, explore.Direction));
    }

    private IEnumerable<GameEvent> ExploreScript(IEnumerable<GameEvent> revealed, Direction direction)
    {
        foreach (var consequence in revealed)
        {
            yield return consequence;
        }

        if (StepRejection(CurrentExplorer, CurrentExplorer.Cell, direction) is null)
        {
            foreach (var consequence in Step(CurrentExplorer, direction))
            {
                yield return consequence;
            }
        }
    }

    private Script ExecuteHeal(Heal heal)
    {
        if (_explorers.FirstOrDefault(e => e.Id == heal.Target) is not { } target)
        {
            return Script.Refuse($"Aucun Explorateur {heal.Target}.");
        }

        if (HealRejection(target) is { } rejection)
        {
            return Script.Refuse(rejection);
        }

        ActionPoints--;
        return Script.Of(Mend(target, 1));
    }

    /// <summary>Why plain Soigner could not reach this explorer, or <c>null</c> if it can.</summary>
    private string? HealRejection(Explorer target)
    {
        if (RequireActive() is { } down)
        {
            return down;
        }

        if (ActionPoints < 1)
        {
            return "Plus de point d'action.";
        }

        if (!target.IsPlaying || target.Cell != CurrentExplorer.Cell)
        {
            return "On ne soigne que sur sa propre tuile.";
        }

        return target.Health == target.MaxHealth ? $"{target.Name} est déjà au maximum." : null;
    }

    /// <summary>Gives hearts back, and gets them up off the floor if they were down.</summary>
    private static List<GameEvent> Mend(Explorer target, int amount)
    {
        var wasDown = target.IsDown;
        var gained = target.Heal(amount);
        var events = new List<GameEvent> { new HealthRegained(target.Id, gained, target.Health) };

        if (wasDown)
        {
            events.Add(new ExplorerStoodUp(target.Id));
        }

        return events;
    }

    /// <summary>Whether this explorer's sheet carries that ability.</summary>
    private static bool Has(Explorer explorer, string ability) =>
        explorer.Sheet is { } sheet && (sheet.First.Id == ability || sheet.Second.Id == ability);

    /// <summary>Whether the Gredin stands on this tile: no trap goes off on it or hurts anyone there.</summary>
    private bool IsWatched(Cell cell) =>
        ExplorersOn(cell).Any(explorer => explorer.IsPlaying && Has(explorer, AbilityIds.Vigilance));

    /// <summary>Where a roll lands, for a script to read once the rerolls are done with.</summary>
    private sealed class Rolled<T>
    {
        public T Value { get; set; } = default!;
    }

    /// <summary>A d6, with the Archéologue's reroll on offer.</summary>
    private IEnumerable<GameEvent> RollDie(Rolled<int> die) =>
        Roll(die, () => Rng.RollDie(), face => new DieRolled(face), face => $"Le dé montre {face}.");

    /// <summary>The Peril die, with the Archéologue's reroll on offer.</summary>
    private IEnumerable<GameEvent> RollPeril(Rolled<PerilFace> die) =>
        Roll(die, Rng.RollPeril, face => new PerilRolled(face), face => $"Le dé de Péril montre {face.Name()}.");

    /// <summary>
    /// Rolls, then — on the Archéologue's own turn, while she has a heart to pay
    /// with — asks her whether to keep it or pay 1 ♥ and roll again, as often as she
    /// likes. Every die goes through here, so Aventurière needs no hook of its own
    /// anywhere else.
    /// </summary>
    private IEnumerable<GameEvent> Roll<T>(Rolled<T> die, Func<T> roll, Func<T, GameEvent> announce, Func<T, string> shown)
    {
        die.Value = roll();
        yield return announce(die.Value);

        while (CurrentExplorer is { IsPlaying: true, IsDown: false } adventurer && Has(adventurer, AbilityIds.Aventuriere))
        {
            foreach (var asking in Ask(
                DecisionKind.Reroll,
                $"{shown(die.Value)} Relancer pour 1 ♥ ?",
                adventurer.Id,
                [new DecisionOption("Garder"), new DecisionOption("Relancer (1 ♥)", Explorer: adventurer.Id)]))
            {
                yield return asking;
            }

            if (_answer == 0)
            {
                yield break;
            }

            yield return new AbilityUsed(adventurer.Id, AbilityIds.Aventuriere);

            foreach (var wound in Wound(adventurer, 1, paid: true))
            {
                yield return wound;
            }

            die.Value = roll();
            yield return announce(die.Value);
        }
    }

    /// <summary>Whether a command is one of the actions <paramref name="action"/> stands for.</summary>
    private static bool Spends(GameCommand command, GrantedAction action) => (command, action) switch
    {
        (Move, GrantedAction.Move) or (Reveal, GrantedAction.Reveal) or (Dig, GrantedAction.Dig) => true,
        _ => false,
    };

    /// <summary>Whether a common action can be paid for — out of what an ability
    /// granted if it granted this one, out of the turn's actions otherwise.</summary>
    private bool CannotPay(GrantedAction action, int cost) =>
        Granted?.Action != action && ActionPoints < cost;

    private void Pay(GrantedAction action, int cost)
    {
        if (Granted is { } granted && granted.Action == action)
        {
            Granted = granted.Remaining > 1 ? granted with { Remaining = granted.Remaining - 1 } : null;
        }
        else
        {
            ActionPoints -= cost;
        }
    }

    /// <summary>What Illuminer, Sprinter and Excaver buy, and how many of it.</summary>
    private static (GrantedAction Action, int Count)? GrantOf(string ability) => ability switch
    {
        AbilityIds.Illuminer => (GrantedAction.Reveal, 2),
        AbilityIds.Sprinter => (GrantedAction.Move, 2),
        AbilityIds.Excaver => (GrantedAction.Dig, 1),
        _ => null,
    };

    /// <summary>
    /// Why an ability that aims at nothing could not be played right now, or
    /// <c>null</c> if it could. A hint for the interface, like <see cref="Steps"/>.
    /// </summary>
    public string? AbilityUnavailable(string ability)
    {
        if (AbilityRejection(ability) is { } rejection)
        {
            return rejection;
        }

        if (OnTheSpotRejection(ability) is { } nothing)
        {
            return nothing;
        }

        if (ability is AbilityIds.Consolider or AbilityIds.Aneantir or AbilityIds.SePreparer)
        {
            return null;
        }

        if (AimsAtACell(ability))
        {
            return Aims(ability).Any() ? null : "Aucune cible à portée.";
        }

        // Paying for actions there is nothing to spend on would only waste a point.
        return GrantOf(ability)?.Action switch
        {
            GrantedAction.Reveal when !Exits().Any() => "Rien à révéler d'ici.",
            GrantedAction.Move when !Steps().Any() => "Nulle part où aller d'ici.",
            GrantedAction.Dig when !DigTargets().Any() => "Aucun Éboulis à portée.",
            null => "Cette capacité vise quelqu'un.",
            _ => null,
        };
    }

    private Script ExecuteGrant(string ability)
    {
        if (AbilityUnavailable(ability) is { } rejection)
        {
            return Script.Refuse(rejection);
        }

        var (action, count) = GrantOf(ability)!.Value;
        ActionPoints -= AbilityCost(ability)!.Value;
        Granted = new GrantedActions(action, count);

        return Script.Of(new AbilityUsed(CurrentExplorer.Id, ability));
    }

    /// <summary>How many uses each limited ability allows in a game.</summary>
    private static int? UseLimit(string ability) => ability switch
    {
        AbilityIds.Consolider => 4,
        AbilityIds.Demolir => 3,
        AbilityIds.Rechercher => TileCatalog.JournalTileCount,
        _ => null,
    };

    /// <summary>
    /// How many more times <paramref name="explorer"/> may play a limited ability this
    /// game, or <c>null</c> when it has no limit.
    /// </summary>
    public int? UsesLeft(ExplorerId explorer, string ability) =>
        UseLimit(ability) is { } limit ? limit - _uses.GetValueOrDefault((explorer, ability)) : null;

    private void CountUse(string ability)
    {
        if (UseLimit(ability) is not null)
        {
            var key = (CurrentExplorer.Id, ability);
            _uses[key] = _uses.GetValueOrDefault(key) + 1;
        }
    }

    /// <summary>Why Consolider, Anéantir or Se préparer would do nothing where she stands.</summary>
    private string? OnTheSpotRejection(string ability)
    {
        var explorer = CurrentExplorer;

        return ability switch
        {
            AbilityIds.Aneantir when !_guardians.Contains(explorer.Cell) => "Aucun ennemi sur sa tuile.",
            AbilityIds.SePreparer when explorer.IsShielded => "Le Bouclier est déjà levé.",
            AbilityIds.SePreparer when explorer.IsRecovering => "Se préparer est indisponible ce tour-ci.",
            AbilityIds.Consolider when Board.TileAt(explorer.Cell) is not { } tile
                || tile.Kind is TileKind.Normal or TileKind.Journal or TileKind.Entrance or TileKind.Sanctuary =>
                "Rien à consolider sur cette tuile.",
            _ => null,
        };
    }

    private Script ExecuteOnTheSpot(string ability)
    {
        if (OnTheSpotRejection(ability) is { } rejection)
        {
            return Script.Refuse(rejection);
        }

        var explorer = CurrentExplorer;
        ActionPoints -= AbilityCost(ability)!.Value;
        CountUse(ability);

        var events = new List<GameEvent> { new AbilityUsed(explorer.Id, ability) };

        switch (ability)
        {
            case AbilityIds.Aneantir:
                _guardians.Remove(explorer.Cell);
                events.Add(new GuardianEliminated(explorer.Cell));
                break;

            case AbilityIds.SePreparer:
                explorer.IsShielded = true;
                events.Add(new ShieldRaised(explorer.Id));
                break;

            case AbilityIds.Consolider:
                Board.Consolidate(explorer.Cell);
                events.Add(new TileConsolidated(explorer.Cell));
                break;
        }

        return Script.Of(events);
    }

    /// <summary>The abilities aimed by pointing at a tile of the board.</summary>
    public static bool AimsAtACell(string ability) => ability is
        AbilityIds.Lunette or AbilityIds.TirDePrecision or AbilityIds.Grenade
        or AbilityIds.Purifier or AbilityIds.Demolir or AbilityIds.Rechercher;

    /// <summary>
    /// Every tile a click could aim <paramref name="ability"/> at right now — empty
    /// ground included, for those that lay a tile or open a wall onto it. A hint for
    /// the interface; each command is checked again on the way in.
    /// </summary>
    public IEnumerable<Cell> AbilityCells(string ability) =>
        AbilityRejection(ability) is null ? Aims(ability).Select(aim => aim.Cell).Distinct() : [];

    /// <summary>The command a click on <paramref name="cell"/> would send, if it aims anywhere.</summary>
    public UseAbility? AbilityAt(string ability, Cell cell) =>
        AbilityRejection(ability) is null ? Aims(ability).FirstOrDefault(aim => aim.Cell == cell).Command : null;

    /// <summary>
    /// What each cell-aimed ability can reach from where the current explorer stands:
    /// the cell to click, the command it sends, and the exit it opens for those that
    /// lay a tile. Deterministic, north-east-south-west and row by row.
    /// </summary>
    private IEnumerable<(Cell Cell, UseAbility Command, TempleExit? Exit)> Aims(string ability)
    {
        var here = CurrentExplorer.Cell;

        switch (ability)
        {
            case AbilityIds.Lunette when !Bag.IsEmpty:
                // Each line in sight, followed to its far end: if that end opens onto
                // empty ground no further than three tiles out, that ground can be revealed.
                foreach (var direction in DirectionExtensions.All)
                {
                    var cell = here;

                    for (var distance = 0; distance < LunetteRange; distance++)
                    {
                        var exit = new TempleExit(cell, direction);

                        if (Board.OpenExits().Contains(exit))
                        {
                            yield return (exit.Target, new UseAbility(ability, Direction: direction), exit);
                            break;
                        }

                        var next = cell.Neighbour(direction);

                        if (!Board.AreConnected(cell, next) || _rubble.Contains(next))
                        {
                            break;
                        }

                        cell = next;
                    }
                }

                break;

            case AbilityIds.TirDePrecision:
                foreach (var cell in VisibleFrom(here, LunetteRange).Where(cell => cell != here && _guardians.Contains(cell)))
                {
                    yield return (cell, new UseAbility(ability, Cell: cell), null);
                }

                break;

            case AbilityIds.Grenade:
                foreach (var cell in Board.ConnectedNeighbours(here).Where(_guardians.Contains))
                {
                    yield return (cell, new UseAbility(ability, Cell: cell), null);
                }

                break;

            case AbilityIds.Purifier:
                foreach (var cell in _guardians.Where(cell => cell != here).Distinct().OrderBy(cell => cell.Row).ThenBy(cell => cell.Column))
                {
                    yield return (cell, new UseAbility(ability, Cell: cell), null);
                }

                break;

            case AbilityIds.Demolir:
                foreach (var direction in DirectionExtensions.All)
                {
                    var beyond = here.Neighbour(direction);
                    var walled = Board.IsOccupied(beyond)
                        ? !Board.AreConnected(here, beyond)
                        : Board.Bounds.Contains(beyond) && Board.TileAt(here) is { } tile
                            && !tile.IsOpen(direction) && !Board.IsDemolished(here, beyond);

                    if (walled)
                    {
                        yield return (beyond, new UseAbility(ability, Direction: direction), null);
                    }
                }

                break;

            case AbilityIds.Rechercher when _journals.Count > 0:
                foreach (var exit in Board.OpenExits().DistinctBy(exit => exit.Target))
                {
                    yield return (exit.Target, new UseAbility(ability, Direction: exit.Direction, Cell: exit.From), exit);
                }

                break;
        }
    }

    /// <summary>Lunette de visée and Tir de précision reach this far along a line of sight.</summary>
    public const int LunetteRange = 3;

    private Script ExecuteAimed(UseAbility use)
    {
        if (Aims(use.Ability).Where(aim => aim.Command == use).Select(aim => ((Cell Cell, TempleExit? Exit)?)(aim.Cell, aim.Exit)).FirstOrDefault()
            is not { } aim)
        {
            return Script.Refuse("Cible hors de portée de cette capacité.");
        }

        var user = CurrentExplorer;
        ActionPoints -= AbilityCost(use.Ability)!.Value;
        CountUse(use.Ability);

        return Script.Of(Aimed(user, use, aim.Cell, aim.Exit).Prepend(new AbilityUsed(user.Id, use.Ability)));
    }

    private IEnumerable<GameEvent> Aimed(Explorer user, UseAbility use, Cell cell, TempleExit? exit)
    {
        switch (use.Ability)
        {
            case AbilityIds.Lunette:
                foreach (var consequence in DrawAndLay(exit!.Value))
                {
                    yield return consequence;
                }

                break;

            case AbilityIds.Rechercher:
                foreach (var consequence in RevealScript(exit!.Value, _journals.Dequeue()))
                {
                    yield return consequence;
                }

                break;

            case AbilityIds.TirDePrecision:
                _guardians.Remove(cell);
                yield return new GuardianEliminated(cell);
                break;

            case AbilityIds.Grenade or AbilityIds.Purifier:
                while (_guardians.Remove(cell))
                {
                    yield return new GuardianEliminated(cell);
                }

                if (use.Ability == AbilityIds.Grenade)
                {
                    foreach (var caught in ExplorersOn(cell).Where(explorer => explorer.IsPlaying).ToList())
                    {
                        foreach (var wound in Wound(caught, 1))
                        {
                            yield return wound;
                        }
                    }
                }

                break;

            case AbilityIds.Demolir:
                Board.Demolish(user.Cell, cell);
                yield return new WallDemolished(user.Cell, use.Direction!.Value);
                break;
        }
    }

    /// <summary>Who the Aristocrate could order to move: anyone else standing, still in the Temple.</summary>
    public IEnumerable<ExplorerId> OrderTargets() =>
        AbilityRejection(AbilityIds.Ordonner) is null
            ? _explorers.Where(other => other != CurrentExplorer && other.IsPlaying && !other.IsDown && StepsOf(other).Any())
                .Select(other => other.Id)
            : [];

    /// <summary>Where an explorer other than the current one could step, if told to.</summary>
    public IEnumerable<Direction> StepsOf(ExplorerId explorer) => StepsOf(_explorers[explorer.Value]);

    private IEnumerable<Direction> StepsOf(Explorer explorer) =>
        DirectionExtensions.All.Where(direction => StepRejection(explorer, explorer.Cell, direction) is null);

    /// <summary>Ordonner: another standing Explorer takes a Se déplacer there and then.</summary>
    private Script ExecuteOrder(UseAbility use)
    {
        if (use.Target is not { } id || _explorers.FirstOrDefault(e => e.Id == id) is not { } ordered)
        {
            return Script.Refuse("Ordonner vise un Explorateur.");
        }

        if (use.Direction is not { } direction)
        {
            return Script.Refuse("Ordonner dit où aller.");
        }

        if (ordered == CurrentExplorer)
        {
            return Script.Refuse("On n'ordonne qu'aux autres.");
        }

        if (!ordered.IsPlaying || ordered.IsDown)
        {
            return Script.Refuse($"{ordered.Name} ne peut pas se déplacer.");
        }

        if (StepRejection(ordered, ordered.Cell, direction) is { } rejection)
        {
            return Script.Refuse(rejection);
        }

        ActionPoints -= AbilityCost(AbilityIds.Ordonner)!.Value;

        return Script.Of(Step(ordered, direction).Prepend(new AbilityUsed(CurrentExplorer.Id, AbilityIds.Ordonner)));
    }

    /// <summary>What each ability the engine plays costs in actions.</summary>
    private static int? AbilityCost(string ability) => ability switch
    {
        AbilityIds.Guerir => 1,
        AbilityIds.Ranimer => 3,
        AbilityIds.Illuminer or AbilityIds.Sprinter or AbilityIds.Excaver => 1,
        AbilityIds.Ordonner or AbilityIds.Consolider or AbilityIds.Aneantir or AbilityIds.SePreparer => 1,
        AbilityIds.Lunette or AbilityIds.TirDePrecision or AbilityIds.Grenade or AbilityIds.Demolir => 1,
        AbilityIds.Rechercher => 2,
        AbilityIds.Purifier => 3,
        _ => null,
    };

    /// <summary>
    /// Whether the current explorer may play <paramref name="ability"/> at all, before
    /// looking at what it aims at: it must be on their own sheet, and paid for.
    /// </summary>
    private string? AbilityRejection(string ability)
    {
        if (RequireActive() is { } down)
        {
            return down;
        }

        if (AbilityCost(ability) is not { } cost)
        {
            return $"Capacité inconnue ou pas encore jouée : {ability}.";
        }

        if (!Has(CurrentExplorer, ability))
        {
            return $"{CurrentExplorer.Name} n'a pas cette capacité.";
        }

        if (UsesLeft(CurrentExplorer.Id, ability) == 0)
        {
            return "Cette capacité est épuisée pour la partie.";
        }

        return ActionPoints < cost ? $"Cette capacité coûte {cost} points d'action." : null;
    }

    private Script ExecuteAbility(UseAbility use)
    {
        if (AbilityRejection(use.Ability) is { } rejection)
        {
            return Script.Refuse(rejection);
        }

        return use.Ability switch
        {
            AbilityIds.Guerir or AbilityIds.Ranimer => ExecuteRemoteHeal(use),
            AbilityIds.Illuminer or AbilityIds.Sprinter or AbilityIds.Excaver => ExecuteGrant(use.Ability),
            AbilityIds.Ordonner => ExecuteOrder(use),
            _ when AimsAtACell(use.Ability) => ExecuteAimed(use),
            AbilityIds.Consolider or AbilityIds.Aneantir or AbilityIds.SePreparer => ExecuteOnTheSpot(use.Ability),
            _ => Script.Refuse($"Capacité pas encore jouée : {use.Ability}."),
        };
    }

    /// <summary>
    /// Guérir and Ranimer: another Explorer, in sight — within two tiles for Guérir,
    /// at any distance for Ranimer, whose card names none.
    /// </summary>
    private string? RemoteHealRejection(string ability, Explorer target)
    {
        if (target.Id == CurrentExplorer.Id)
        {
            return "Cette capacité soigne un autre Explorateur.";
        }

        if (!target.IsPlaying)
        {
            return $"{target.Name} n'est plus dans le Temple.";
        }

        var range = ability == AbilityIds.Guerir ? 2 : int.MaxValue;

        if (!VisibleFrom(CurrentExplorer.Cell, range).Contains(target.Cell))
        {
            return ability == AbilityIds.Guerir
                ? $"{target.Name} doit être visible, à deux tuiles ou moins."
                : $"{target.Name} doit être visible.";
        }

        return target.Health == target.MaxHealth ? $"{target.Name} est déjà au maximum." : null;
    }

    private Script ExecuteRemoteHeal(UseAbility use)
    {
        if (use.Target is not { } id || _explorers.FirstOrDefault(e => e.Id == id) is not { } target)
        {
            return Script.Refuse("Cette capacité vise un Explorateur.");
        }

        if (RemoteHealRejection(use.Ability, target) is { } rejection)
        {
            return Script.Refuse(rejection);
        }

        var amount = use.Ability switch
        {
            AbilityIds.Guerir => 2,
            _ => target.IsDown ? 1 : 3,
        };

        ActionPoints -= AbilityCost(use.Ability)!.Value;

        var events = new List<GameEvent> { new AbilityUsed(CurrentExplorer.Id, use.Ability) };
        events.AddRange(Mend(target, amount));
        return Script.Of(events);
    }

    private Script ExecutePickUp(PickUpItem pickUp)
    {
        if (RequireActive() is { } down)
        {
            return Script.Refuse(down);
        }

        if (ActionPoints < 1)
        {
            return Script.Refuse("Plus de point d'action.");
        }

        var explorer = CurrentExplorer;

        if (explorer.Carried is { } held)
        {
            return Script.Refuse($"{explorer.Name} porte déjà : {held}.");
        }

        if (!_items.TryGetValue(explorer.Cell, out var items) || !items.Remove(pickUp.Item))
        {
            return Script.Refuse($"Rien de tel sur {explorer.Cell}.");
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

        return Script.Of(events);
    }

    private Script ExecuteDrop()
    {
        if (RequireActive() is { } down)
        {
            return Script.Refuse(down);
        }

        if (ActionPoints < 1)
        {
            return Script.Refuse("Plus de point d'action.");
        }

        var explorer = CurrentExplorer;

        if (explorer.Carried is not { } item)
        {
            return Script.Refuse($"{explorer.Name} ne porte rien.");
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

            return Script.Of(turned);
        }

        Drop(explorer.Cell, item);

        return Script.Of(new ItemDropped(explorer.Id, item, explorer.Cell));
    }

    private Script ExecuteAttack()
    {
        if (RequireActive() is { } down)
        {
            return Script.Refuse(down);
        }

        if (ActionPoints < 1)
        {
            return Script.Refuse("Plus de point d'action.");
        }

        var cell = CurrentExplorer.Cell;

        if (!_guardians.Contains(cell))
        {
            return Script.Refuse($"Aucun ennemi sur {cell}.");
        }

        ActionPoints--;
        return Script.Of(Strike(cell));
    }

    private IEnumerable<GameEvent> Strike(Cell cell)
    {
        var die = new Rolled<int>();

        foreach (var rolling in RollDie(die))
        {
            yield return rolling;
        }

        if (die.Value >= AttackSuccessRoll)
        {
            _guardians.Remove(cell);
            yield return new GuardianEliminated(cell);
        }
    }

    private Script ExecuteDig(Dig dig)
    {
        if (RequireActive() is { } down)
        {
            return Script.Refuse(down);
        }

        if (CannotPay(GrantedAction.Dig, 2))
        {
            return Script.Refuse("Creuser coûte deux points d'action.");
        }

        var cell = CurrentExplorer.Cell;

        if (dig.Cell != cell && !Board.AreConnected(cell, dig.Cell))
        {
            return Script.Refuse($"{dig.Cell} n'est ni votre tuile ni une voisine reliée.");
        }

        if (!_rubble.Remove(dig.Cell))
        {
            return Script.Refuse($"Pas d'Éboulis sur {dig.Cell}.");
        }

        Pay(GrantedAction.Dig, 2);
        return Script.Of(new RubbleCleared(dig.Cell));
    }

    /// <summary>
    /// With the bag empty there is enough of the temple on the table to know where the
    /// sanctuary lies: as far from the Entrance as it can be reached. Its hall takes
    /// the three keys, and the vault beyond it holds the Artefact. Where several tiles
    /// at that depth would take it, the rules hand the choice to the leader.
    /// </summary>
    private IEnumerable<GameEvent> PlaceSanctuary()
    {
        if (SanctuaryHall is not null)
        {
            yield break;
        }

        var sites = SanctuarySites();

        if (sites.Count == 0)
        {
            yield break;
        }

        foreach (var asking in Ask(
            DecisionKind.SanctuarySite,
            "Le sac est vide : par où s'ouvre le Sanctuaire ?",
            Leader.Id,
            [.. sites.Select(site => new DecisionOption($"{site.Hall} vers {site.Exit.Direction}", site.Hall))]))
        {
            yield return asking;
        }

        var chosen = sites[_answer];

        Board.Place(chosen.Hall, chosen.HallTile, chosen.Exit.From);
        Board.PlaceFixed(chosen.Vault, chosen.VaultTile);

        SanctuaryHall = chosen.Hall;
        SanctuaryVault = chosen.Vault;

        yield return new SanctuaryFound(chosen.Hall, chosen.Vault);
    }

    /// <summary>A hall and the vault behind it, and the two tiles cut to fit them.</summary>
    private readonly record struct SanctuarySite(
        TempleExit Exit,
        Cell Hall,
        Cell Vault,
        PlacedTile HallTile,
        PlacedTile VaultTile);

    /// <summary>
    /// Every place the sanctuary could open, kept to the deepest row it reaches — the
    /// rulebook's "colonne la plus éloignée possible de l'Entrée". All the equals come
    /// back, so the leader is the one who picks between them.
    /// </summary>
    private IReadOnlyList<SanctuarySite> SanctuarySites()
    {
        var sites = new List<SanctuarySite>();

        foreach (var exit in Board.OpenExits())
        {
            var hall = exit.Target;
            var vault = hall.Neighbour(exit.Direction);

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

            sites.Add(new SanctuarySite(
                exit,
                hall,
                vault,
                hallTile,
                new PlacedTile(
                    new TileDefinition("Sanctuary-Vault", TileKind.Sanctuary, vaultLayout.Shape),
                    vaultLayout.Rotation)));
        }

        if (sites.Count == 0)
        {
            return [];
        }

        var deepest = sites.Max(site => site.Vault.Row);

        return [.. sites
            .Where(site => site.Vault.Row == deepest)
            .OrderBy(site => site.Hall.Column)
            .ThenBy(site => site.Hall.Row)];
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

    private Script ExecuteOverexert()
    {
        var explorer = CurrentExplorer;

        if (explorer.IsDown)
        {
            return Script.Refuse("À terre, un Explorateur ne peut pas se dépasser.");
        }

        if (HasOverexerted)
        {
            return Script.Refuse("Déjà dépassé ce tour-ci.");
        }

        HasOverexerted = true;
        ActionPoints++;

        var events = new List<GameEvent>();
        events.AddRange(Wound(explorer, 1, paid: true));

        return Script.Of(events);
    }

    /// <summary>
    /// Takes hearts off an explorer. Going down during one's own turn ends it on the
    /// spot, which is why this returns events rather than mutating quietly.
    /// </summary>
    /// <param name="paid">
    /// A heart spent rather than taken — to overexert, to reroll — which the Bouclier
    /// does not stop.
    /// </param>
    private IEnumerable<GameEvent> Wound(Explorer explorer, int amount, bool paid = false)
    {
        if (explorer.IsShielded && !paid)
        {
            yield break;
        }

        var lost = explorer.Wound(amount);

        // Already on the floor, there is nothing left to lose and no falling again.
        if (lost == 0)
        {
            yield break;
        }

        yield return new HealthLost(explorer.Id, lost, explorer.Health);

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
        var die = new Rolled<PerilFace>();

        foreach (var rolling in RollPeril(die))
        {
            yield return rolling;
        }

        var consequences = die.Value switch
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

    /// <summary>
    /// Pushing yourself has a price, and it is collected later — except by the
    /// Guérisseuse, whom a stumble gives a heart back instead.
    /// </summary>
    private IEnumerable<GameEvent> Stumble(Explorer explorer)
    {
        if (!Has(explorer, AbilityIds.Survivante))
        {
            return HasOverexerted ? Wound(explorer, 1) : [];
        }

        if (!explorer.IsPlaying || explorer.Health == explorer.MaxHealth)
        {
            return [];
        }

        return Mend(explorer, 1).Prepend(new AbilityUsed(explorer.Id, AbilityIds.Survivante));
    }

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

        var die = new Rolled<int>();

        foreach (var rolling in RollDie(die))
        {
            yield return rolling;
        }

        var doomed = standing
            .Where(entry => entry.Value.Definition.RuinsNumber == die.Value)
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

        // Whoever the Gredin keeps an eye on cannot set anything off.
        if (IsWatched(here))
        {
            yield break;
        }

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

    /// <summary>
    /// A Guardian tile stirs, the nearest to the active explorer along connected
    /// tiles. Ties are theirs to settle — the rules give this one to the active player
    /// rather than to the leader.
    /// </summary>
    private IEnumerable<GameEvent> WakeGuardian(Explorer explorer)
    {
        if (_guardians.Count >= MaxGuardians)
        {
            yield break;
        }

        var pockets = NearestGuardianPockets(explorer.Cell);

        if (pockets.Count == 0)
        {
            yield break;
        }

        foreach (var asking in Ask(
            DecisionKind.GuardianAwakening,
            "Un Gardien s'éveille : sur quelle case Gardien ?",
            explorer.Id,
            [.. pockets.Select(pocket => new DecisionOption($"Case Gardien {pocket}", pocket))]))
        {
            yield return asking;
        }

        var woken = pockets[_answer];

        _guardians.Add(woken);
        yield return new GuardianAppeared(woken);
    }

    /// <summary>
    /// The Guardian tiles closest to a cell, measured along connected tiles. Standing
    /// in a pocket already is a distance of nothing at all.
    /// </summary>
    private IReadOnlyList<Cell> NearestGuardianPockets(Cell from)
    {
        if (Board.TileAt(from)?.Kind == TileKind.Guardian)
        {
            return [from];
        }

        var pockets = Board.Tiles
            .Where(entry => entry.Value.Kind == TileKind.Guardian)
            .Select(entry => (Cell: entry.Key, Distance: Board.Distance(from, entry.Key)))
            .Where(pocket => pocket.Distance is not null)
            .ToList();

        if (pockets.Count == 0)
        {
            return [];
        }

        var nearest = pockets.Min(pocket => pocket.Distance!.Value);

        return [.. pockets
            .Where(pocket => pocket.Distance == nearest)
            .Select(pocket => pocket.Cell)
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)];
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
    /// The first thing on the list it can do: strike, close in, or dig. Each of the
    /// three can come out a tie, and every tie goes to the Chef d'Expédition — the
    /// temple never picks its own victim.
    /// </summary>
    private IEnumerable<GameEvent> ActivateGuardian(int index)
    {
        var cell = _guardians[index];
        var prey = ExplorersOn(cell).Where(IsPrey).ToList();

        if (prey.Count > 0)
        {
            foreach (var asking in Ask(
                DecisionKind.GuardianTarget,
                $"Le Gardien de {cell} frappe : qui encaisse ?",
                Leader.Id,
                [.. prey.Select(target => new DecisionOption(target.Name, cell, target.Id))]))
            {
                yield return asking;
            }

            var struck = prey[_answer];
            yield return new GuardianAttacked(cell, struck.Id);

            foreach (var wound in Wound(struck, GuardianDamage))
            {
                yield return wound;
            }

            yield break;
        }

        var steps = StepsTowardPrey(cell);

        if (steps.Count > 0)
        {
            foreach (var asking in Ask(
                DecisionKind.GuardianStep,
                $"Le Gardien de {cell} avance : par où ?",
                Leader.Id,
                [.. steps.Select(step => new DecisionOption($"Vers {step}", step))]))
            {
                yield return asking;
            }

            var next = steps[_answer];

            _guardians[index] = next;
            yield return new GuardianStepped(cell, next);
            yield break;
        }

        var buried = Board.ConnectedNeighbours(cell).Where(_rubble.Contains).ToList();

        if (buried.Count == 0)
        {
            yield break;
        }

        foreach (var asking in Ask(
            DecisionKind.GuardianDig,
            $"Le Gardien de {cell} déblaie : quel Éboulis ?",
            Leader.Id,
            [.. buried.Select(rubble => new DecisionOption($"Éboulis {rubble}", rubble))]))
        {
            yield return asking;
        }

        var cleared = buried[_answer];

        _rubble.Remove(cleared);
        yield return new GuardianClearedRubble(cell, cleared);
    }

    /// <summary>Someone a guardian would bother with: in the temple, and on their feet.</summary>
    private static bool IsPrey(Explorer explorer) => explorer.IsPlaying && !explorer.IsDown;

    /// <summary>
    /// Every first step that puts the guardian on a shortest route to someone worth
    /// chasing. More than one, and the leader says which way it lumbers.
    /// </summary>
    private IReadOnlyList<Cell> StepsTowardPrey(Cell cell)
    {
        var candidates = Board.ConnectedNeighbours(cell)
            .Where(next => !_rubble.Contains(next))
            .Select(next => (Cell: next, Distance: DistanceToPrey(next)))
            .Where(step => step.Distance is not null)
            .ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        var shortest = candidates.Min(step => step.Distance!.Value);

        return [.. candidates.Where(step => step.Distance == shortest).Select(step => step.Cell)];
    }

    /// <summary>Tiles from here to the nearest explorer still standing, rubble barring the way.</summary>
    private int? DistanceToPrey(Cell cell) =>
        ExplorersOn(cell).Any(IsPrey)
            ? 0
            : Board.ShortestPath(
                cell,
                target => ExplorersOn(target).Any(IsPrey),
                target => !_rubble.Contains(target))?.Count;

    private Script ExecuteEndTurn() => Script.Of(EndTurnScript());

    /// <summary>
    /// The temple answers, then the turn passes. A round ends once everyone has played
    /// — the medallion stays where it was dealt, and the next round opens on its holder
    /// again, which is simply the seat play started from.
    /// </summary>
    private IEnumerable<GameEvent> EndTurnScript()
    {
        // Under the curse the temple answers twice for every turn taken.
        for (var roll = 0; roll < (IsCursed ? 2 : 1); roll++)
        {
            foreach (var peril in ResolvePeril(CurrentExplorer))
            {
                yield return peril;
            }
        }

        yield return new TurnEnded(CurrentExplorer.Id);

        _current = (_current + 1) % _explorers.Count;
        _turnsThisRound++;

        if (_turnsThisRound == _explorers.Count)
        {
            _turnsThisRound = 0;
            Round++;
            yield return new RoundEnded(Round);

            // The temple takes its own turn once everyone has taken theirs.
            for (var activation = 0; activation < GuardianActivationsPerRound; activation++)
            {
                foreach (var action in ActivateGuardians())
                {
                    yield return action;
                }
            }

            // Two steps under the curse: carrying the Artefact hurries the mountain.
            for (var step = 0; step < (IsCursed ? 2 : 1); step++)
            {
                foreach (var tremor in AdvanceEruption())
                {
                    yield return tremor;
                }
            }
        }

        HasOverexerted = false;
        Granted = null;

        // The Bouclier holds until her turn comes round, and that turn is spent
        // without it.
        var next = CurrentExplorer;
        next.IsRecovering = next.IsShielded;

        if (next.IsShielded)
        {
            next.IsShielded = false;
            yield return new ShieldLowered(next.Id);
        }

        // Those who got out, and those the mountain kept, have no actions to take.
        ActionPoints = CurrentExplorer switch
        {
            { IsPlaying: false } => 0,
            { IsDown: true } => 1,
            _ => ActionsPerTurn,
        };

        yield return new TurnBegan(CurrentExplorer.Id, ActionPoints);
    }
}
