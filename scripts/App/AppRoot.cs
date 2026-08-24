using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Core.Tiles;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Drives a game from the keyboard and mouse. It owns this machine's
/// <see cref="GameState"/> and redraws the board after each command — no animation
/// yet, just the truth.
/// </summary>
/// <remarks>
/// On a networked table every peer runs the same game from the same seed and applies
/// the same commands in the same order. Nothing of the state itself travels: the host
/// validates a command and relays the one line of text that describes it, and each
/// peer replays it. A fingerprint rides along so a divergence is heard about rather
/// than lived with.
/// </remarks>
public partial class AppRoot : Node3D
{
    /// <summary>Same seed, same temple. Change it in the inspector to deal another one.</summary>
    [Export]
    public int Seed { get; set; } = 42;

    [Export(PropertyHint.Range, "1,6,1")]
    public int PartySize { get; set; } = 3;

    [Export]
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    /// <summary>How many lines of the story stay on screen.</summary>
    private const int LogDepth = 3;

    /// <summary>How often a client says "I am at the table" until the host answers.</summary>
    private const double SeatingCall = 1.0;

    /// <summary>How this machine currently looks at the temple. Purely local — every
    /// peer at the table is free to pick its own, so it never travels as a command.</summary>
    private enum ViewMode
    {
        Overview,
        Fps,
    }

    /// <summary>An adult's eyes above the floor, under a 2.25 m vault.</summary>
    private const float EyeHeight = 1.6f;

    /// <summary>Where a look resets to: tipped down a little, not level. That keeps
    /// <see cref="CellUnder"/>'s floor raycast working and the floor's highlight
    /// patches in view until the mouse looks somewhere else.</summary>
    private const float FpsRestPitchDegrees = -12f;

    private const float FpsFov = 78f;

    /// <summary>Degrees the look turns per pixel of mouse motion.</summary>
    private const float LookSensitivity = 0.12f;

    private const float LookPitchMin = -75f;

    private const float LookPitchMax = 60f;

    private readonly Queue<string> _log = [];

    /// <summary>Host only: the peers that have the temple on screen.</summary>
    private readonly HashSet<long> _seated = [];

    /// <summary>The way each explorer was last seen walking, dug, or turned to
    /// reveal — purely presentational, and rebuilt the same way by every peer as it
    /// replays the same commands, so nothing needs to travel for it.</summary>
    private readonly Dictionary<ExplorerId, Direction> _facing = [];

    private GameState _game = null!;
    private BoardView _board = null!;
    private TokenView _tokens = null!;
    private HighlightView _highlights = null!;
    private Hud _hud = null!;
    private Camera3D _camera = null!;
    private Cell? _hovered;
    private ViewMode _viewMode = ViewMode.Overview;
    private float _overviewFov;

    /// <summary>The look the mouse has built in FPS mode, and whose look it is — so a
    /// turn changing hands, or a step taken, resets it instead of leaving the camera
    /// pointed wherever the mouse last left it for nobody in particular.</summary>
    private float _lookYaw;
    private float _lookPitch;
    private ExplorerId? _lookSubject;

    /// <summary>Whether play has opened. A networked table waits for everybody.</summary>
    private bool _begun = true;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _overviewFov = _camera.Fov;
        _hud = GetNode<Hud>("Hud");

        _hud.ActionRequested += action => Apply(Command(action));
        _hud.OptionChosen += option => Apply(new Decide(option));

        _board = new BoardView { Name = "BoardView" };
        _tokens = new TokenView { Name = "TokenView" };
        _highlights = new HighlightView { Name = "HighlightView" };
        AddChild(_board);
        AddChild(_highlights);
        AddChild(_tokens);

        _game = NewGame();

        Refresh();
        Say("L'expédition entre dans le temple.");

        if (Session.IsOnline)
        {
            OpenTable();
        }
    }

    /// <summary>
    /// The party the lobby agreed on — or, when this scene is run on its own from the
    /// editor, the throwaway one the exported properties describe.
    /// </summary>
    private GameState NewGame()
    {
        var party = Session.Sheets.ToList();

        if (party.Count >= ExplorerRoster.SmallestParty)
        {
            return GameState.NewGame(party, Session.Seed, Session.Difficulty);
        }

        var roster = Enumerable.Range(1, PartySize).Select(number => ($"Explorateur {number}", 5));
        return GameState.NewGame(roster, (ulong)Seed, Difficulty);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Looking around is never a move: it stays live even mid-arbitration, once
        // the game is over, or while waiting for the rest of the table.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.V })
        {
            ToggleView();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Escape is the way out anyone tries first, and with the pointer captured
        // and invisible it is the only one that doesn't need the pointer back to
        // work. It surfaces rather than toggles: from underground you always come
        // up, never the other way round.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            && _viewMode == ViewMode.Fps)
        {
            ToggleView();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_game.IsOver || !_begun)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion motion when _viewMode == ViewMode.Fps && ShouldCapture:
                Look(motion.Relative);
                break;

            // Looking through an Explorer's eyes with the pointer free — a decision
            // is open — is looking at a still image: no turning, and no hover from a
            // cursor that is nowhere near what the crosshair is on.
            case InputEventMouseMotion when _viewMode == ViewMode.Fps:
                break;

            case InputEventMouseMotion motion:
                Hover(CellUnder(motion.Position));
                break;

            case InputEventKey { Pressed: true, Echo: false } key when Bind(key.Keycode) is { } action:
                Apply(Command(action));
                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
                when CellUnder(_viewMode == ViewMode.Fps ? ScreenCentre() : click.Position) is { } cell:
                ClickOn(cell, click.ShiftPressed, click.CtrlPressed);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    /// <summary>
    /// Turns the FPS look by a mouse delta — the crosshair stays screen-centre, the
    /// world turns under it instead. Repaints the hover highlight the same way a
    /// mouse move over the board does in the overview, so whatever is dead ahead is
    /// lit up before it is clicked.
    /// </summary>
    private void Look(Vector2 relative)
    {
        _lookYaw -= relative.X * LookSensitivity;
        _lookPitch = Mathf.Clamp(_lookPitch - relative.Y * LookSensitivity, LookPitchMin, LookPitchMax);

        _camera.RotationDegrees = new Vector3(_lookPitch, _lookYaw, 0f);
        Hover(CellUnder(ScreenCentre()));
    }

    private Vector2 ScreenCentre() => GetViewport().GetVisibleRect().Size / 2f;

    /// <summary>
    /// The actions that need no target beyond the explorer's own tile. Everything that
    /// points somewhere goes through the mouse instead.
    /// </summary>
    private static string? Bind(Key key) => key switch
    {
        Key.Space => "endturn",
        Key.A => "attack",
        Key.H => "heal",
        Key.O => "overexert",
        Key.D => "drop",
        Key.P => "pickup",
        _ => null,
    };

    /// <summary>The same six actions, whether they came from a key or from a button.</summary>
    private GameCommand? Command(string action) => action switch
    {
        "endturn" => new EndTurn(),
        "attack" => new Attack(),
        "heal" => new Heal(_game.CurrentExplorer.Id),
        "overexert" => new Overexert(),
        "drop" => new DropItem(),
        "pickup" when _game.ItemsOn(_game.CurrentExplorer.Cell) is [var item, ..] => new PickUpItem(item),
        "pickup" => null,
        _ => null,
    };

    /// <summary>
    /// One click, several meanings: settle the tie the game is waiting on, step onto a
    /// tile that is already there, uncover one where there is nothing yet, or — held
    /// down — reveal without walking in, or dig out a neighbour.
    /// </summary>
    private void ClickOn(Cell cell, bool revealOnly, bool dig)
    {
        if (_game.Pending is { } decision)
        {
            var option = decision.Options.ToList().FindIndex(candidate => candidate.Cell == cell);

            if (option >= 0)
            {
                Apply(new Decide(option));
                return;
            }

            Say($"{_game.Chooser(decision).Name} doit d'abord trancher.");
            return;
        }

        var from = _game.CurrentExplorer.Cell;

        if (dig)
        {
            Apply(new Dig(cell));
            return;
        }

        if (from.DirectionTo(cell) is not { } direction)
        {
            Say($"{cell} n'est pas voisine de {from}.");
            return;
        }

        if (_game.Board.IsOccupied(cell))
        {
            Apply(new Move(direction));
            return;
        }

        Apply(revealOnly ? new Reveal(direction) : new Explore(direction));
    }

    /// <summary>
    /// A command from this machine's player. Alone, it is played on the spot; on a
    /// networked table it goes to the host, who is the only one allowed to decide that
    /// it happened.
    /// </summary>
    private void Apply(GameCommand? command)
    {
        if (command is null)
        {
            Say("Rien à faire ici.");
            return;
        }

        if (NotYourTurn() is { } refusal)
        {
            Say(refusal);
            return;
        }

        if (!Session.IsOnline || Session.IsHost)
        {
            Settle(command);
            return;
        }

        RpcId(Session.HostPeer, nameof(Request), CommandCodec.Encode(command));
    }

    /// <summary>
    /// Whoever is due to act: the chooser while an arbitration stands, the explorer
    /// whose turn it is otherwise.
    /// </summary>
    private ExplorerId Due => _game.Pending is { } decision ? decision.Chooser : _game.CurrentExplorer.Id;

    private string? NotYourTurn() =>
        Session.Owns(Due) ? null : $"C'est à {_game.Explorers[Due.Value].Name} de jouer.";

    /// <summary>
    /// Remembers which way an explorer was facing when they last moved, dug in a
    /// direction, or turned to reveal a tile — so the FPS view has somewhere to look
    /// besides straight into the wall they arrived from.
    /// </summary>
    private void TrackFacing(ExplorerId actor, GameCommand command)
    {
        var facing = command switch
        {
            Move move => move.Direction,
            Explore explore => explore.Direction,
            Reveal reveal => reveal.Direction,
            Run { Steps: [.., var last] } => last,
            _ => (Direction?)null,
        };

        if (facing is { } direction)
        {
            _facing[actor] = direction;

            // The mouse may have turned this explorer's look elsewhere since the
            // last step; walking a new way re-centres it, same as the direction.
            if (_lookSubject == actor)
            {
                ResetLook(actor);
            }
        }
    }

    /// <summary>The host's word: it plays the command and tells everyone else to.</summary>
    private void Settle(GameCommand command)
    {
        var actor = Due;
        var result = _game.Execute(command);

        if (!result.Accepted)
        {
            Say(result.Rejection ?? "Impossible.");
            return;
        }

        TrackFacing(actor, command);
        Refresh();
        Say(Describe(result.Events));

        if (Session.IsOnline)
        {
            Rpc(nameof(Play), CommandCodec.Encode(command), _game.Fingerprint);
        }
    }

    /// <summary>A client asking the host for something. Anything here is suspect.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Request(string line)
    {
        var sender = Multiplayer.GetRemoteSenderId();

        if (CommandCodec.Decode(line) is not { } command)
        {
            GD.PushWarning($"Commande illisible de {sender} : {line}");
            return;
        }

        var actor = Due;

        if (Session.SeatOf(actor)?.Peer != sender)
        {
            RpcId(sender, nameof(Refused), "Ce n'est pas à vous de jouer.");
            return;
        }

        var result = _game.Execute(command);

        if (!result.Accepted)
        {
            RpcId(sender, nameof(Refused), result.Rejection ?? "Impossible.");
            return;
        }

        TrackFacing(actor, command);
        Refresh();
        Say(Describe(result.Events));
        Rpc(nameof(Play), line, _game.Fingerprint);
    }

    /// <summary>
    /// The host says this happened. Every peer replays it on its own game — same seed,
    /// same commands, same temple — and checks it landed on the same state.
    /// </summary>
    [Rpc(CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Play(string line, long fingerprint)
    {
        if (CommandCodec.Decode(line) is not { } command)
        {
            return;
        }

        var actor = Due;
        var result = _game.Execute(command);

        TrackFacing(actor, command);
        Refresh();
        Say(Describe(result.Events));

        if (_game.Fingerprint != fingerprint)
        {
            GD.PushError($"Désynchronisation après « {line} ».");
            Say("Désynchronisation avec l'hôte : la partie n'est plus fiable.");
        }
    }

    [Rpc(CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Refused(string reason) => Say(reason);

    // -------------------------------------------------------------- the table

    /// <summary>
    /// Nobody plays until everyone has the temple on screen: a command sent to a peer
    /// still loading would be lost, and a lost command is a divergence.
    /// </summary>
    private void OpenTable()
    {
        _begun = false;
        Multiplayer.PeerDisconnected += Abandoned;

        if (Session.IsHost)
        {
            Seat(Session.LocalPeer);
            return;
        }

        // The host may still be leaving the lobby, so keep saying it until it answers.
        var knock = new Godot.Timer { Name = "Knock", WaitTime = SeatingCall, Autostart = true };
        knock.Timeout += () =>
        {
            if (_begun)
            {
                knock.QueueFree();
                return;
            }

            RpcId(Session.HostPeer, nameof(AtTheTable));
        };

        AddChild(knock);
        Refresh();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void AtTheTable() => Seat(Multiplayer.GetRemoteSenderId());

    private void Seat(long peer)
    {
        _seated.Add(peer);

        // A knock that crossed the answer: say it again to that peer alone.
        if (_begun)
        {
            if (peer != Session.LocalPeer)
            {
                RpcId(peer, nameof(Begin));
            }

            return;
        }

        if (_seated.Count <= Multiplayer.GetPeers().Length)
        {
            Refresh();
            return;
        }

        Rpc(nameof(Begin));
        Begin();
    }

    [Rpc(CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Begin()
    {
        _begun = true;
        Refresh();
        Say("Tout le monde est là. L'expédition commence.");
    }

    /// <summary>
    /// A player who drops out would otherwise take their Explorers' turns with them.
    /// The host picks up their seats so the expedition can go on.
    /// </summary>
    private void Abandoned(long peer)
    {
        if (!Session.IsHost)
        {
            return;
        }

        Session.HandOver(peer);
        Rpc(nameof(Seats), Session.Encode(Session.Party));
        Refresh();
        Say($"Le joueur {peer} a quitté la table ; l'hôte reprend ses Explorateurs.");
    }

    [Rpc(CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Seats(string party)
    {
        Session.Party = Session.Decode(party);
        Refresh();
    }

    private void Hover(Cell? cell)
    {
        if (cell == _hovered)
        {
            return;
        }

        _hovered = cell;
        _highlights.Render(Hints(), _hovered);
    }

    private void Refresh()
    {
        _board.Render(_game.Board);
        _tokens.Render(_game, _viewMode == ViewMode.Fps ? Due : null);
        // Before the highlights: in the FPS view the camera decides what the
        // crosshair rests on, and that is the cell they have to light up.
        PositionCamera();
        _highlights.Render(Hints(), _hovered);
        _hud.Show(_game, Session.Owns(Due) && _begun, Notice());
        SyncMouseMode();
    }

    /// <summary>Local to this machine: which of the two ways it currently looks at
    /// the temple.</summary>
    private void ToggleView()
    {
        _viewMode = _viewMode == ViewMode.Overview ? ViewMode.Fps : ViewMode.Overview;
        _hud.SetAiming(_viewMode == ViewMode.Fps);

        // A full redraw, not just a camera move: which meeples are drawn, which cell
        // the crosshair rests on and which legend the hint line shows all depend on
        // the view, and all three were left as the other view had them.
        Refresh();
    }

    /// <summary>
    /// Whether the mouse should be locked to the window for a look, rather than free
    /// to click things: not while a decision needs a HUD button a captured, invisible
    /// cursor stuck at screen-centre could never reach, and not once nothing is being
    /// played any more.
    /// </summary>
    private bool ShouldCapture => _viewMode == ViewMode.Fps && _begun && !_game.IsOver && _game.Pending is null;

    /// <summary>
    /// A free cursor stops dead at the edge of the screen, so turning any further
    /// takes picking the mouse up and moving it again — unplayable as a look. Locking
    /// it hides the pointer and re-centres it every frame instead, so <see cref="Look"/>
    /// gets a clean, unbroken stream of relative motion.
    /// </summary>
    private void SyncMouseMode() =>
        Input.MouseMode = ShouldCapture ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;

    private void PositionCamera()
    {
        BoardView.ShowVaults(_viewMode == ViewMode.Fps);

        if (_viewMode == ViewMode.Fps)
        {
            FrameExplorer();
        }
        else
        {
            FrameBoard();
        }
    }

    /// <summary>
    /// Drops the camera to the eyes of whoever is due to act. The look itself is
    /// whatever the mouse last set it to for that explorer — reset to face the way
    /// they were last seen walking whenever the camera picks up a different one.
    /// </summary>
    private void FrameExplorer()
    {
        var explorer = _game.Explorers[Due.Value];

        if (_lookSubject != explorer.Id)
        {
            _lookSubject = explorer.Id;
            ResetLook(explorer.Id);
        }

        _camera.Fov = FpsFov;
        _camera.Position = BoardView.ToWorld(explorer.Cell) + new Vector3(0f, EyeHeight, 0f);
        _camera.RotationDegrees = new Vector3(_lookPitch, _lookYaw, 0f);

        // The crosshair has moved with the camera even though the mouse hasn't: what
        // it now rests on is what a click would act on, so light that up.
        Hover(CellUnder(ScreenCentre()));
    }

    /// <summary>Faces the look back the way an explorer was last seen walking, at the
    /// resting pitch that keeps <see cref="CellUnder"/> able to find a floor to click
    /// on — the same convention <see cref="BoardView"/> turns a tile by.</summary>
    private void ResetLook(ExplorerId explorer)
    {
        var facing = _facing.GetValueOrDefault(explorer, Direction.South);
        _lookYaw = BoardView.QuarterTurnDegrees * (int)facing;
        _lookPitch = FpsRestPitchDegrees;
    }

    /// <summary>Who the table is waiting on, when there is more than one player.</summary>
    private string Notice()
    {
        if (!Session.IsOnline)
        {
            return string.Empty;
        }

        if (!_begun)
        {
            return "En attente des autres joueurs…";
        }

        var name = _game.Explorers[Due.Value].Name;
        var asking = _game.Pending is not null;

        return (Session.Owns(Due), asking) switch
        {
            (true, true) => "À toi de trancher.",
            (true, false) => "À toi de jouer.",
            (false, true) => $"{name} doit trancher.",
            (false, false) => $"Au tour de {name}.",
        };
    }

    /// <summary>
    /// What a click would do, cell by cell. The engine is the one asked — this only
    /// paints the answer, and every command is checked again on the way in.
    /// </summary>
    private Dictionary<Cell, HighlightView.Hint> Hints()
    {
        var hints = new Dictionary<Cell, HighlightView.Hint>();

        if (_game.IsOver || !_begun || !Session.Owns(Due))
        {
            return hints;
        }

        // An arbitration takes the board over: only its own answers are worth pointing at.
        if (_game.Pending is { } decision)
        {
            foreach (var option in decision.Options)
            {
                if (option.Cell is { } cell)
                {
                    hints[cell] = HighlightView.Hint.Choice;
                }
            }

            return hints;
        }

        var from = _game.CurrentExplorer.Cell;

        foreach (var direction in _game.Exits())
        {
            hints[from.Neighbour(direction)] = HighlightView.Hint.Unknown;
        }

        foreach (var direction in _game.Steps())
        {
            hints[from.Neighbour(direction)] = HighlightView.Hint.Step;
        }

        foreach (var cell in _game.DigTargets())
        {
            hints[cell] = HighlightView.Hint.Rubble;
        }

        return hints;
    }

    /// <summary>Keeps the last few lines of the story on screen.</summary>
    private void Say(string line)
    {
        _log.Enqueue(line);

        while (_log.Count > LogDepth)
        {
            _log.Dequeue();
        }

        _hud.Say(string.Join('\n', _log));
    }

    /// <summary>The events of a command, in a line, most interesting first.</summary>
    private static string Describe(IReadOnlyList<GameEvent> events)
    {
        var told = events.Select(Tell).Where(line => line is not null);
        return string.Join("   ·   ", told.DefaultIfEmpty("…"));
    }

    private static string? Tell(GameEvent @event) => @event switch
    {
        TileRevealed revealed => $"{Say(revealed.Tile.Kind)} révélée",
        TrapSprung trap => $"{Say(trap.Trap)} déclenché !",
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
        PerilRolled peril => $"Péril : {Say(peril.Face)}",
        DecisionRequired required => $"à trancher : {required.Decision.Prompt}",
        DecisionMade made => $"choix : {made.Chosen.Label}",
        _ => null,
    };

    private static string Say(PerilFace face) => face switch
    {
        PerilFace.Stumble => "faux pas",
        PerilFace.Lava => "Lave",
        PerilFace.Collapse => "Effondrement",
        PerilFace.Trap => "Pièges",
        PerilFace.WakeGuardian => "un Gardien s'éveille",
        PerilFace.ActivateGuardians => "les Gardiens s'animent",
        _ => face.ToString(),
    };

    private static string Say(TileKind kind) => kind switch
    {
        TileKind.Normal => "une galerie",
        TileKind.Bridge => "un Pont",
        TileKind.Key => "une Clé",
        TileKind.Lava => "une coulée de Lave",
        TileKind.SpikeTrap => "un piège à Pics",
        TileKind.DartTrap => "un piège à Fléchettes",
        TileKind.Ruins => "une Ruine",
        TileKind.Guardian => "une case Gardien",
        TileKind.Journal => "un Journal",
        TileKind.Entrance => "l'Entrée",
        TileKind.Sanctuary => "le Sanctuaire",
        _ => kind.ToString(),
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
        _camera.Fov = _overviewFov;

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
