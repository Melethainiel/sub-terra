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

    private readonly List<Explorer> _explorers;
    private readonly HashSet<Cell> _keyTokens = [];
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

    /// <summary>Cells holding a Key token, waiting to be picked up.</summary>
    public IReadOnlySet<Cell> KeyTokens => _keyTokens;

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
        Reveal reveal => ExecuteReveal(reveal),
        Overexert => ExecuteOverexert(),
        EndTurn => ExecuteEndTurn(),
        _ => CommandResult.Reject($"Commande inconnue : {command.GetType().Name}."),
    };

    private CommandResult ExecuteMove(Move move)
    {
        var explorer = CurrentExplorer;

        if (ActionPoints < 1)
        {
            return CommandResult.Reject("Plus de point d'action.");
        }

        var target = explorer.Cell.Neighbour(move.Direction);

        if (!Board.AreConnected(explorer.Cell, target))
        {
            return CommandResult.Reject($"{target} n'est pas reliée à {explorer.Cell}.");
        }

        // A bridge takes one explorer's weight at a time.
        if (Board.TileAt(target)?.Kind == TileKind.Bridge && ExplorersOn(target).Any())
        {
            return CommandResult.Reject("Le Pont ne supporte qu'un Explorateur à la fois.");
        }

        var from = explorer.Cell;
        explorer.Cell = target;
        ActionPoints--;

        return CommandResult.Accept(new ExplorerMoved(explorer.Id, from, target));
    }

    private CommandResult ExecuteReveal(Reveal reveal)
    {
        var explorer = CurrentExplorer;

        if (explorer.IsDown)
        {
            return CommandResult.Reject("À terre, un Explorateur ne peut que ramper.");
        }

        if (ActionPoints < 1)
        {
            return CommandResult.Reject("Plus de point d'action.");
        }

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
                _keyTokens.Add(cell);
                yield return new KeyAppeared(cell);
                break;

            case TileKind.Guardian:
                _guardians.Add(cell);
                yield return new GuardianAppeared(cell);
                break;
        }
    }

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
