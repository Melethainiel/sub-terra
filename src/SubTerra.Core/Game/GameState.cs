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

    private readonly List<Explorer> _explorers;
    private readonly Dictionary<Cell, List<ItemKind>> _items = [];
    private readonly List<Cell> _guardians = [];

    private int _current;
    private bool _bagAnnouncedEmpty;

    public GameState(IEnumerable<Explorer> explorers, TempleBoard board, TileBag bag, Rng rng)
    {
        _explorers = [.. explorers];

        if (_explorers.Count == 0)
        {
            throw new ArgumentException("A game needs at least one explorer.", nameof(explorers));
        }

        Board = board;
        Bag = bag;
        Rng = rng;
        ActionPoints = ActionsPerTurn;
    }

    /// <summary>A game laid out as the rulebook's setup describes, ready for turn one.</summary>
    public static GameState NewGame(IEnumerable<(string Name, int MaxHealth)> roster, ulong seed)
    {
        var board = TempleSetup.CreateBoard();
        var explorers = roster.Select((sheet, index) =>
            new Explorer(new ExplorerId(index), sheet.Name, sheet.MaxHealth, TempleSetup.EntranceCrossing));

        return new GameState(explorers, board, TileBag.Temple(), new Rng(seed));
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

    public Explorer CurrentExplorer => _explorers[_current];

    public int ActionPoints { get; private set; }

    public bool HasOverexerted { get; private set; }

    /// <summary>Rounds completed. Every explorer plays once per round.</summary>
    public int Round { get; private set; }

    public IEnumerable<Explorer> ExplorersOn(Cell cell) =>
        _explorers.Where(explorer => explorer.Cell == cell);

    public CommandResult Execute(GameCommand command) => command switch
    {
        Move move => ExecuteMove(move),
        Run run => ExecuteRun(run),
        Reveal reveal => ExecuteReveal(reveal),
        Explore explore => ExecuteExplore(explore),
        Heal heal => ExecuteHeal(heal),
        PickUpItem pickUp => ExecutePickUp(pickUp),
        DropItem => ExecuteDrop(),
        Overexert => ExecuteOverexert(),
        EndTurn => ExecuteEndTurn(),
        _ => CommandResult.Reject($"Commande inconnue : {command.GetType().Name}."),
    };

    private CommandResult ExecuteMove(Move move)
    {
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

        explorer.Cell = target;
        yield return new ExplorerMoved(explorer.Id, from, target);

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

        yield return new TrapSprung(cell, TileKind.SpikeTrap);

        foreach (var wound in Wound(explorer, SpikeTrapDamage))
        {
            yield return wound;
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
                _guardians.Add(cell);
                yield return new GuardianAppeared(cell);
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

        return CommandResult.Accept(new ItemPickedUp(explorer.Id, pickUp.Item, explorer.Cell));
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
        Drop(explorer.Cell, item);
        ActionPoints--;

        return CommandResult.Accept(new ItemDropped(explorer.Id, item, explorer.Cell));
    }

    /// <summary>Why a downed explorer cannot do this, or <c>null</c> if they are up.</summary>
    private string? RequireActive() =>
        CurrentExplorer.IsDown ? "À terre, un Explorateur ne peut que ramper." : null;

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

    private CommandResult ExecuteEndTurn()
    {
        var events = new List<GameEvent> { new TurnEnded(CurrentExplorer.Id) };

        _current++;

        if (_current == _explorers.Count)
        {
            _current = 0;
            Round++;
            events.Add(new RoundEnded(Round));
        }

        // A downed explorer can only crawl: one tile, and nothing else.
        HasOverexerted = false;
        ActionPoints = CurrentExplorer.IsDown ? 1 : ActionsPerTurn;

        events.Add(new TurnBegan(CurrentExplorer.Id, ActionPoints));

        return new CommandResult(true, null, events);
    }
}
