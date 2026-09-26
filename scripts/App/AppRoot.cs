using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Core.Tiles;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Drives a game from the keyboard and mouse. It owns this machine's
/// <see cref="GameState"/> and redraws the board after each command, handing the
/// board whatever events the command produced so a tile can show what just
/// happened to it.
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

    /// <summary>This machine's game, to read — for the dev tools that drive a table.</summary>
    internal GameState Game => _game;
    private BoardView _board = null!;
    private TokenView _tokens = null!;
    private HighlightView _highlights = null!;
    private Hud _hud = null!;
    private Camera3D _camera = null!;
    private Cell? _hovered;

    /// <summary>Which answer to the pending arbitration is currently laid out on the
    /// board — the tile just drawn, turned the way that option would turn it.</summary>
    private int _option;

    /// <summary>
    /// The targeted action card currently in hand, waiting on a cell to point at —
    /// "move", "explore", "reveal", "dig" or "run" — or <c>null</c> when none is
    /// armed. Every other card fires the moment it is pressed; these need a second
    /// click on the board, the same two-step the printed cards ask for.
    /// </summary>
    private string? _armed;

    /// <summary>The directions Courir has built up so far, one click at a time — up
    /// to three, sent together as a single <see cref="Run"/> once the route is
    /// confirmed.</summary>
    private readonly List<Direction> _runSteps = [];

    private ViewMode _viewMode = Settings.DefaultViewIsFps ? ViewMode.Fps : ViewMode.Overview;
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

        _hud.ActionRequested += HandleAction;
        _hud.OptionChosen += option => Apply(new Decide(option));
        _hud.OptionPreviewed += ShowOption;

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

        // Escape is the way anyone tries first to back out of something. A card
        // waiting on a cell comes off the table before anything else does — only a
        // second Escape, with nothing armed, surfaces out of the FPS view.
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _armed is not null)
        {
            Disarm();
            GetViewport().SetInputAsHandled();
            return;
        }

        // With the pointer captured and invisible this is the only way out that
        // doesn't need the pointer back to work. It surfaces rather than toggles:
        // from underground you always come up, never the other way round.
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
            // What is being weighed up stands on the board, and the wheel goes through
            // it: a tile out of the bag turns, a stirring Guardian moves pocket. R for
            // the hand that isn't on the mouse.
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } when Weighing:
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.R } when Weighing:
                Turn(1);
                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } when Weighing:
                Turn(-1);
                GetViewport().SetInputAsHandled();
                break;

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
                HandleAction(action);
                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
                when CellUnder(_viewMode == ViewMode.Fps ? ScreenCentre() : click.Position) is { } cell:
                ClickOn(cell);
                GetViewport().SetInputAsHandled();
                break;

            // The other way to put a card back down without reaching for the keyboard.
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } when _armed is not null:
                Disarm();
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

    /// <summary>Every card, whether it came from a key or from a button.</summary>
    private static string? Bind(Key key) => key switch
    {
        Key.Space => "endturn",
        Key.A => "attack",
        Key.H => "heal",
        Key.O => "overexert",
        Key.D => "drop",
        Key.P => "pickup",
        Key.M => "move",
        Key.E => "explore",
        Key.R => "reveal",
        Key.C => "dig",
        Key.U => "run",
        Key.Key1 => "ability1",
        Key.Key2 => "ability2",
        _ => null,
    };

    /// <summary>Ordonner in hand: who has been picked to move, before where to.</summary>
    private ExplorerId? _orderee;

    /// <summary>The last grant <see cref="Refresh"/> saw, so it arms the granted card
    /// once per change rather than on every redraw.</summary>
    private GrantedActions? _lastGranted;

    private static string CardFor(GrantedAction action) => action switch
    {
        GrantedAction.Move => "move",
        GrantedAction.Reveal => "reveal",
        _ => "dig",
    };

    /// <summary>Whether the turn can pay for a common action — or an ability already has.</summary>
    private bool Affords(GrantedAction action, int cost) =>
        _game.Granted?.Action == action || _game.ActionPoints >= cost;

    /// <summary>The abilities the engine already plays that aim at another Explorer.
    /// Any other ability's column stays greyed until its turn comes.</summary>
    private static readonly HashSet<string> HealingAbilities = [AbilityIds.Guerir, AbilityIds.Ranimer];

    /// <summary>The ability behind the column <c>ability1</c> or <c>ability2</c>, for
    /// whoever's turn it is.</summary>
    private Ability? AbilityIn(string action) => (action, _game.CurrentExplorer.Sheet) switch
    {
        ("ability1", { } sheet) => sheet.First,
        ("ability2", { } sheet) => sheet.Second,
        _ => null,
    };

    /// <summary>
    /// Who a heal card reaches: plain Soigner for <c>heal</c>, or the healing ability
    /// in that column. <c>null</c> when the card is not a heal at all.
    /// </summary>
    private IEnumerable<ExplorerId>? HealTargetsFor(string action) => action switch
    {
        "heal" => _game.HealTargets(),
        _ when AbilityIn(action) is { } ability && HealingAbilities.Contains(ability.Id) => _game.HealTargets(ability.Id),
        _ => null,
    };

    /// <summary>The cards that need no target beyond the explorer's own tile — the
    /// rest are armed rather than played outright, see <see cref="ToggleArm"/>.</summary>
    private GameCommand? Command(string action) => action switch
    {
        "endturn" => new EndTurn(),
        "attack" => new Attack(),
        "overexert" => new Overexert(),
        "drop" => new DropItem(),
        "pickup" when _game.ItemsOn(_game.CurrentExplorer.Cell) is [var item, ..] => new PickUpItem(item),
        "pickup" => null,
        _ => null,
    };

    /// <summary>The cards that point somewhere on the board rather than firing on
    /// the spot: pressing one arms it, and it takes a click on a cell to play it.</summary>
    private static readonly HashSet<string> TargetedActions =
        ["move", "explore", "reveal", "dig", "run", "heal", "ability1", "ability2"];

    /// <summary>Where a card, from either the HUD or the keyboard, ends up: armed if
    /// it points somewhere, played on the spot otherwise.</summary>
    private void HandleAction(string action)
    {
        // Mending oneself is the usual case and has nowhere else to point: no need to
        // arm the card and click one's own tile for it.
        if (action == "heal" && _game.HealTargets().ToList() is [var only] && only == _game.CurrentExplorer.Id)
        {
            Apply(new Heal(only));
            return;
        }

        // An ability that aims at nobody is played on the spot; whatever it buys then
        // comes into hand on its own (see Refresh).
        if (AbilityIn(action) is { } ability && !HealingAbilities.Contains(ability.Id)
            && ability.Id != AbilityIds.Ordonner && !GameState.AimsAtACell(ability.Id))
        {
            Apply(new UseAbility(ability.Id));
            return;
        }

        if (TargetedActions.Contains(action))
        {
            ToggleArm(action);
            return;
        }

        Apply(Command(action));
    }

    /// <summary>
    /// Puts a targeted card in hand, or takes it back out. Pressing the card already
    /// armed either cancels it, or — mid-route on Courir — confirms what has been
    /// clicked so far rather than waiting for a third step.
    /// </summary>
    private void ToggleArm(string action)
    {
        if (_game.IsOver || _game.Pending is not null)
        {
            return;
        }

        if (NotYourTurn() is { } refusal)
        {
            Say(refusal);
            return;
        }

        if (_armed == action)
        {
            if (action == "run" && _runSteps.Count > 0)
            {
                SubmitRun();
            }
            else
            {
                Disarm();
            }

            return;
        }

        if (!CanArm(action))
        {
            Say("Rien à faire avec cette action pour l'instant.");
            return;
        }

        _armed = action;
        _runSteps.Clear();
        Refresh();
    }

    /// <summary>Puts whatever card is in hand back down, unplayed.</summary>
    private void Disarm()
    {
        _armed = null;
        _orderee = null;
        _runSteps.Clear();
        Refresh();
    }

    /// <summary>
    /// Whether a targeted card has anywhere to point right now — for comfort only:
    /// the engine is what actually decides once a command is sent.
    /// </summary>
    private bool CanArm(string action) => action switch
    {
        "move" => Affords(GrantedAction.Move, 1) && _game.Steps().Any(),
        "explore" => _game.ActionPoints >= 1 && _game.Exits().Any(),
        "reveal" => Affords(GrantedAction.Reveal, 1) && _game.Exits().Any(),
        "dig" => Affords(GrantedAction.Dig, 2) && _game.DigTargets().Any(),
        "run" => _game.ActionPoints >= 2 && _game.Steps().Any(),
        "heal" => HealTargetsFor(action)?.Any() ?? false,
        "ability1" or "ability2" => AbilityIn(action) is { } ability
            && (ability.Id switch
            {
                AbilityIds.Ordonner => _game.OrderTargets().Any(),
                _ when GameState.AimsAtACell(ability.Id) => _game.AbilityCells(ability.Id).Any(),
                _ when HealingAbilities.Contains(ability.Id) => _game.HealTargets(ability.Id).Any(),
                _ => _game.AbilityUnavailable(ability.Id) is null,
            }),
        _ => true,
    };

    /// <summary>
    /// One click: settle the tie the game is waiting on if there is one, otherwise
    /// play whatever card is currently armed against this cell. A card is spent the
    /// moment it is clicked, valid target or not — same as pointing at the wrong
    /// square on the table and being told so.
    /// </summary>
    private void ClickOn(Cell cell)
    {
        if (_game.Pending is { } decision)
        {
            // Every way of turning a drawn tile answers on the same cell, so a click
            // on the board takes the one it is showing rather than the first in the
            // list: what you see laid out is what you lay down.
            var option = decision.Options[_option].Cell == cell
                ? _option
                : decision.Options.ToList().FindIndex(candidate => candidate.Cell == cell);

            if (option >= 0)
            {
                Apply(new Decide(option));
                return;
            }

            Say($"{_game.Chooser(decision).Name} doit d'abord trancher.");
            return;
        }

        if (_armed is null)
        {
            Say("Choisis d'abord une action.");
            return;
        }

        var from = _game.CurrentExplorer.Cell;

        switch (_armed)
        {
            case "move":
                // Put down first: what the click plays may put a card straight back
                // in hand (an ability's second free action), and that one stays.
                Disarm();

                if (from.DirectionTo(cell) is { } moveDirection)
                {
                    Apply(new Move(moveDirection));
                }
                else
                {
                    Say($"{cell} n'est pas voisine de {from}.");
                }

                break;

            case "explore":
                // Put down first: what the click plays may put a card straight back
                // in hand (an ability's second free action), and that one stays.
                Disarm();

                if (from.DirectionTo(cell) is { } exploreDirection)
                {
                    Apply(new Explore(exploreDirection));
                }
                else
                {
                    Say($"{cell} n'est pas voisine de {from}.");
                }

                break;

            case "reveal":
                // Put down first: what the click plays may put a card straight back
                // in hand (an ability's second free action), and that one stays.
                Disarm();

                if (from.DirectionTo(cell) is { } revealDirection)
                {
                    Apply(new Reveal(revealDirection));
                }
                else
                {
                    Say($"{cell} n'est pas voisine de {from}.");
                }

                break;

            case "dig":
                // Unlike the others, a dig target may be the explorer's own tile —
                // no neighbour to check.
                Disarm();
                Apply(new Dig(cell));
                break;

            case "run":
                ContinueRun(cell);
                break;

            case "ability1" or "ability2" when AbilityIn(_armed)?.Id == AbilityIds.Ordonner:
                Order(cell);
                break;

            case "ability1" or "ability2" when AbilityIn(_armed) is { } aimed && GameState.AimsAtACell(aimed.Id):
                Disarm();

                if (_game.AbilityAt(aimed.Id, cell) is { } command)
                {
                    Apply(command);
                }
                else
                {
                    Say($"{aimed.Name} ne vise pas {cell}.");
                }

                break;

            case "heal" or "ability1" or "ability2":
                var armed = _armed;
                Disarm();
                HealOn(armed, cell);
                break;
        }
    }

    /// <summary>
    /// Ordonner takes two clicks: first the Explorer who is told to move, then the
    /// tile they are sent to.
    /// </summary>
    private void Order(Cell cell)
    {
        if (_orderee is not { } orderee)
        {
            var targets = _game.OrderTargets().ToHashSet();

            if (_game.Explorers.FirstOrDefault(e => e.Cell == cell && targets.Contains(e.Id)) is not { } picked)
            {
                Say("Personne ici à qui donner un ordre.");
                return;
            }

            _orderee = picked.Id;
            Say($"Où envoyer {picked.Name} ?");
            Refresh();
            return;
        }

        var from = _game.Explorers[orderee.Value].Cell;
        Disarm();

        if (from.DirectionTo(cell) is { } direction && _game.StepsOf(orderee).Contains(direction))
        {
            Apply(new UseAbility(AbilityIds.Ordonner, orderee, direction));
        }
        else
        {
            Say($"{cell} n'est pas un pas possible depuis {from}.");
        }
    }

    /// <summary>
    /// Plays the armed heal on whoever stands on <paramref name="cell"/> and can be
    /// reached — the most hurt of them if the tile is crowded, since that is who a
    /// player pointing at it almost always means.
    /// </summary>
    private void HealOn(string action, Cell cell)
    {
        var reachable = HealTargetsFor(action)?.ToHashSet() ?? [];
        var patient = _game.Explorers
            .Where(explorer => explorer.Cell == cell && reachable.Contains(explorer.Id))
            .OrderBy(explorer => explorer.Health)
            .FirstOrDefault();

        if (patient is null)
        {
            Say("Personne à soigner sur cette case.");
            return;
        }

        Apply(action == "heal"
            ? new Heal(patient.Id)
            : new UseAbility(AbilityIn(action)!.Id, patient.Id));
    }

    /// <summary>
    /// One more step of Courir's route. Where the route would stand after the steps
    /// already clicked is worked out from geometry alone — the engine is the one
    /// that checks each step is actually open once the route is sent — so only the
    /// very first step is shown as a hint on the board; the rest trust the click.
    /// </summary>
    private void ContinueRun(Cell cell)
    {
        var projected = _game.CurrentExplorer.Cell;

        foreach (var step in _runSteps)
        {
            projected = projected.Neighbour(step);
        }

        if (projected.DirectionTo(cell) is not { } direction)
        {
            Say($"{cell} n'est pas voisine de {projected}.");
            Disarm();
            return;
        }

        _runSteps.Add(direction);

        if (_runSteps.Count >= 3)
        {
            SubmitRun();
            return;
        }

        Refresh();
    }

    /// <summary>Sends whatever of Courir's route has been clicked so far as one command.</summary>
    private void SubmitRun()
    {
        Apply(new Run([.. _runSteps]));
        Disarm();
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
        Refresh(result.Events);
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
        Refresh(result.Events);
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
        Refresh(result.Events);
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

    /// <summary>
    /// Whether the question on the table is one the board itself shows — a tile
    /// waiting to be turned, a Guardian about to stir in one pocket or another — and
    /// this machine is the one being asked.
    /// </summary>
    private bool Weighing =>
        _game.Pending is { Kind: DecisionKind.TileOrientation or DecisionKind.GuardianAwakening }
        && Session.Owns(Due)
        && _begun;

    /// <summary>Goes a notch through the answers, either way round.</summary>
    private void Turn(int notches)
    {
        if (_game.Pending is not { } decision)
        {
            return;
        }

        var count = decision.Options.Count;
        ShowOption((((_option + notches) % count) + count) % count);
    }

    /// <summary>
    /// Lays another of the pending arbitration's answers out on the board. Only the
    /// picture changes: nothing is played until the option is taken — and only for
    /// the player being asked, so a spectator never sees a tile that is not there.
    /// </summary>
    private void ShowOption(int option)
    {
        if (_game.Pending is not { } decision || option < 0 || option >= decision.Options.Count
            || !Session.Owns(Due))
        {
            return;
        }

        _option = option;
        _board.Render(_game.Board, Previewed(), rubble: _game.Rubble);
        _tokens.Render(_game, _viewMode == ViewMode.Fps ? Due : null, Stirring());
        _hud.Highlight(_option);
    }

    /// <summary>
    /// The tile the temple is being asked about, turned the way the option under
    /// consideration would turn it — there is nothing to show for the other questions,
    /// whose answers are already on the table.
    /// </summary>
    private (Cell Cell, PlacedTile Tile)? Previewed() =>
        _game.Pending is { } decision
        && Session.Owns(Due)
        && decision.Options[_option] is { Cell: { } cell, Tile: { } tile }
            ? (cell, tile)
            : null;

    /// <summary>
    /// The Guardian pocket being weighed up, where the meeple stands while the player
    /// makes up their mind. The other questions about Guardians are about ones already
    /// on the table, and have nothing to put there.
    /// </summary>
    private Cell? Stirring() =>
        _game.Pending is { Kind: DecisionKind.GuardianAwakening } decision && Session.Owns(Due)
            ? decision.Options[_option].Cell
            : null;

    /// <param name="justHappened">
    /// The events the command just settled produced, if this call follows one — a
    /// trap that sprang, a ruin that came down — passed straight to the board so the
    /// tile in question can show it happening instead of just landing in its new
    /// state. Left out for a redraw that answers no command of its own: a fresh seat
    /// at the table, a camera toggle, a decision preview.
    /// </param>
    private void Refresh(IReadOnlyList<GameEvent>? justHappened = null)
    {
        // A fresh question is shown with its first answer laid out; an answered one
        // leaves nothing hanging over the board.
        _option = 0;

        // Whatever was armed answers a state of the game that has just moved on —
        // a decision came up, the turn changed hands, the last action point went —
        // so a card left in hand from before is put back down rather than trusted.
        if (_armed is not null && (_game.Pending is not null || !Session.Owns(Due) || !CanArm(_armed)))
        {
            _armed = null;
            _orderee = null;
            _runSteps.Clear();
        }

        // Illuminer, Sprinter and Excaver buy a common action to take there and then:
        // its card is put straight in hand, and again after each one taken while any
        // are left. Only when what was granted changes, so Échap still puts it down.
        if (_game.Granted != _lastGranted)
        {
            _lastGranted = _game.Granted;

            if (_game.Granted is { } granted && _game.Pending is null && Session.Owns(Due))
            {
                _armed = CardFor(granted.Action);
                _runSteps.Clear();
            }
        }

        _board.Render(_game.Board, Previewed(), justHappened, _game.Rubble);
        _tokens.Render(_game, _viewMode == ViewMode.Fps ? Due : null, Stirring());
        // Before the highlights: in the FPS view the camera decides what the
        // crosshair rests on, and that is the cell they have to light up.
        PositionCamera();
        _highlights.Render(Hints(), _hovered);
        _hud.Show(_game, Session.Owns(Due) && _begun, Notice(), _armed, CanArm);

        // Nothing is badged for a table watching someone else turn a tile: the board
        // in front of them is bare, and a badge would point at a tile that isn't there.
        _hud.Highlight(Session.Owns(Due) ? _option : -1);
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
    /// What the card currently in hand would do, cell by cell — nothing at all while
    /// no card is armed, so a lit-up temple never promises more than one click will
    /// actually spend. The engine is the one asked for the legal cells either way;
    /// this only paints the answer, and every command is checked again on the way in.
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

        switch (_armed)
        {
            case "explore" or "reveal":
                foreach (var direction in _game.Exits())
                {
                    hints[from.Neighbour(direction)] = HighlightView.Hint.Unknown;
                }

                break;

            case "move":
                foreach (var direction in _game.Steps())
                {
                    hints[from.Neighbour(direction)] = HighlightView.Hint.Step;
                }

                break;

            case "dig":
                foreach (var cell in _game.DigTargets())
                {
                    hints[cell] = HighlightView.Hint.Rubble;
                }

                break;

            case "ability1" or "ability2" when AbilityIn(_armed) is { } aimed && GameState.AimsAtACell(aimed.Id):
                var hint = aimed.Id switch
                {
                    AbilityIds.Lunette or AbilityIds.Rechercher => HighlightView.Hint.Unknown,
                    AbilityIds.Demolir => HighlightView.Hint.Rubble,
                    _ => HighlightView.Hint.Enemy,
                };

                foreach (var cell in _game.AbilityCells(aimed.Id))
                {
                    hints[cell] = hint;
                }

                break;

            case "ability1" or "ability2" when AbilityIn(_armed)?.Id == AbilityIds.Ordonner:
                if (_orderee is { } orderee)
                {
                    var at = _game.Explorers[orderee.Value].Cell;

                    foreach (var direction in _game.StepsOf(orderee))
                    {
                        hints[at.Neighbour(direction)] = HighlightView.Hint.Step;
                    }
                }
                else
                {
                    foreach (var id in _game.OrderTargets())
                    {
                        hints[_game.Explorers[id.Value].Cell] = HighlightView.Hint.Ally;
                    }
                }

                break;

            case "heal" or "ability1" or "ability2":
                foreach (var id in HealTargetsFor(_armed) ?? [])
                {
                    hints[_game.Explorers[id.Value].Cell] = HighlightView.Hint.Ally;
                }

                break;

            case "run" when _runSteps.Count == 0:
                // Only the first step of a route is legality the engine can vouch for
                // in advance; the second and third are trusted to the click, then
                // checked for real once the whole route is sent.
                foreach (var direction in _game.Steps())
                {
                    hints[from.Neighbour(direction)] = HighlightView.Hint.Step;
                }

                break;
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
        TileRevealed revealed => $"tuile révélée : {Say(revealed.Tile.Kind)}",
        TrapSprung trap => $"{Say(trap.Trap)} déclenché !",
        AbilityUsed used => ExplorerRoster.All
            .SelectMany(sheet => new[] { sheet.First, sheet.Second })
            .FirstOrDefault(ability => ability.Id == used.Ability)?.Name ?? used.Ability,
        HealthRegained regained => $"+{regained.Amount} ♥",
        GuardianEliminated => "un Gardien tombe",
        ShieldRaised => "Bouclier levé",
        ShieldLowered => "Bouclier baissé",
        TileConsolidated => "tuile consolidée",
        WallDemolished => "un mur tombe",
        TileReturned returned => $"{Say(returned.Tile.Kind)} retourne au sac",
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
