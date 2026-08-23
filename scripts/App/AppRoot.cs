using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Tiles;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Drives a game from the keyboard and mouse. It owns the only <see cref="GameState"/>
/// and redraws the board after each command — no animation yet, just the truth.
/// </summary>
public partial class AppRoot : Node3D
{
    /// <summary>Same seed, same temple. Change it in the inspector to deal another one.</summary>
    [Export]
    public int Seed { get; set; } = 42;

    [Export(PropertyHint.Range, "1,6,1")]
    public int PartySize { get; set; } = 3;

    [Export]
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    private GameState _game = null!;
    private BoardView _board = null!;
    private TokenView _tokens = null!;
    private Hud _hud = null!;
    private Camera3D _camera = null!;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _hud = GetNode<Hud>("Hud");

        _board = new BoardView { Name = "BoardView" };
        _tokens = new TokenView { Name = "TokenView" };
        AddChild(_board);
        AddChild(_tokens);

        var roster = Enumerable.Range(1, PartySize).Select(number => ($"Explorateur {number}", 5));
        _game = GameState.NewGame(roster, (ulong)Seed, Difficulty);

        Refresh();
        _hud.Say("L'expédition entre dans le temple.");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_game.IsOver)
        {
            return;
        }

        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false } key when Bind(key.Keycode) is { } command:
                Apply(command);
                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
                when CellUnder(click.Position) is { } cell:
                ClickOn(cell, click.ShiftPressed, click.CtrlPressed);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    /// <summary>
    /// The actions that need no target beyond the explorer's own tile. Everything that
    /// points somewhere goes through the mouse instead.
    /// </summary>
    private GameCommand? Bind(Key key) => key switch
    {
        Key.Space => new EndTurn(),
        Key.A => new Attack(),
        Key.H => new Heal(_game.CurrentExplorer.Id),
        Key.O => new Overexert(),
        Key.D => new DropItem(),
        Key.P when _game.ItemsOn(_game.CurrentExplorer.Cell) is [var item, ..] => new PickUpItem(item),
        Key.P => null,
        _ => null,
    };

    /// <summary>
    /// One click, several meanings: step onto a tile that is already there, uncover
    /// one where there is nothing yet, or — held down — reveal without walking in, or
    /// dig out a neighbour.
    /// </summary>
    private void ClickOn(Cell cell, bool revealOnly, bool dig)
    {
        var from = _game.CurrentExplorer.Cell;

        if (dig)
        {
            Apply(new Dig(cell));
            return;
        }

        if (from.DirectionTo(cell) is not { } direction)
        {
            _hud.Say($"{cell} n'est pas voisine de {from}.");
            return;
        }

        if (_game.Board.IsOccupied(cell))
        {
            Apply(new Move(direction));
            return;
        }

        Apply(revealOnly ? new Reveal(direction) : new Explore(direction));
    }

    private void Apply(GameCommand command)
    {
        var result = _game.Execute(command);

        if (!result.Accepted)
        {
            _hud.Say(result.Rejection ?? "Impossible.");
            return;
        }

        Refresh();
        _hud.Say(Describe(result.Events));
    }

    private void Refresh()
    {
        _board.Render(_game.Board);
        _tokens.Render(_game);
        _hud.Show(_game);
        FrameBoard();
    }

    /// <summary>The events of a command, in a line, most interesting first.</summary>
    private static string Describe(IReadOnlyList<GameEvent> events)
    {
        var told = events.Select(Tell).Where(line => line is not null);
        return string.Join("   ·   ", told.DefaultIfEmpty("…"));
    }

    private static string? Tell(GameEvent @event) => @event switch
    {
        TileRevealed revealed => $"{revealed.Tile.Kind} révélée",
        TrapSprung trap => $"{trap.Trap} déclenché !",
        HealthLost lost => $"−{lost.Amount} ♥",
        ExplorerWentDown => "à terre !",
        GuardianAppeared => "un Gardien s'éveille",
        GuardianAttacked => "un Gardien frappe",
        RuinsCollapsed => "les ruines s'effondrent",
        SanctuaryFound => "le Sanctuaire est découvert",
        KeyDeposited deposited => $"{deposited.Total}ᵉ Clé déposée",
        ArtefactRevealed => "l'Artefact apparaît",
        CurseFell => "LA MALÉDICTION S'ABAT",
        VolcanoReady => "le volcan est prêt à exploser",
        VolcanoErupted => "LE VOLCAN ENTRE EN ÉRUPTION",
        ExplorerKilled => "englouti par la lave",
        ExplorerEscaped escaped => escaped.WithArtefact ? "sorti avec l'Artefact !" : "sorti du temple",
        GameEnded ended => $"FIN DE PARTIE — {ended.Outcome}",
        PerilRolled peril => $"Péril : {peril.Face}",
        _ => null,
    };

    /// <summary>Turns a click into the tile it landed on, using the table's own plane.</summary>
    private Cell? CellUnder(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);

        if (Mathf.IsZeroApprox(direction.Y))
        {
            return null;
        }

        var hit = origin + (direction * (-origin.Y / direction.Y));

        return new Cell(
            Mathf.RoundToInt(hit.X / BoardView.TileSize),
            Mathf.RoundToInt(hit.Z / BoardView.TileSize));
    }

    /// <summary>Keeps the whole temple in frame as it grows.</summary>
    private void FrameBoard()
    {
        var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
        var max = new Vector3(float.MinValue, 0f, float.MinValue);

        foreach (var cell in _game.Board.Tiles.Keys)
        {
            var position = BoardView.ToWorld(cell);
            min = new Vector3(Mathf.Min(min.X, position.X), 0f, Mathf.Min(min.Z, position.Z));
            max = new Vector3(Mathf.Max(max.X, position.X), 0f, Mathf.Max(max.Z, position.Z));
        }

        var centre = (min + max) / 2f;
        var extent = Mathf.Max(max.X - min.X, max.Z - min.Z) + BoardView.TileSize;
        var distance = Mathf.Max(extent * 1.25f, 12f);

        _camera.LookAtFromPosition(centre + new Vector3(0f, distance, distance * 0.6f), centre, Vector3.Up);
    }
}
