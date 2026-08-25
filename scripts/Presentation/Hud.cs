using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// The whole screen the player reads by, built entirely from printed-card objects the
/// way the physical game's own table would be: the status plaque, the alert and the
/// last event as paper up in the corner, the party as portrait cards up top, the
/// Actions card along the bottom with its own hint printed underneath it. Nothing here
/// is bare text floating over the temple — every word sits on some piece of "paper".
/// The HUD asks for commands and never carries them out itself.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>A card was pressed. The name is the one the keyboard uses too.</summary>
    [Signal]
    public delegate void ActionRequestedEventHandler(string action);

    /// <summary>An arbitration was settled: which option was taken.</summary>
    [Signal]
    public delegate void OptionChosenEventHandler(int option);

    /// <summary>An option is being weighed up: the board shows what it would look like.</summary>
    [Signal]
    public delegate void OptionPreviewedEventHandler(int option);

    /// <summary>
    /// Every action, in the order they sit as columns on the one Actions card — an
    /// icon and a name apiece, its PA cost and its key printed underneath, the way a
    /// reference card lists them side by side rather than as separate cards of their
    /// own. <c>Targeted</c> ones point somewhere on the board: pressing the column
    /// only arms it — <see cref="AppRoot"/> then waits for a cell to be clicked
    /// before a command is actually sent, the same two-step "point at the action,
    /// then point at the target" the card asks for. The rest need no target beyond
    /// the explorer's own tile, so pressing the column is the whole of it.
    /// </summary>
    private static readonly (string Action, string Name, string Icon, string Cost, string Key, bool Targeted, Color Accent)[] Cards =
    [
        ("move", "Se déplacer", "→", "1 PA", "M", true, Palette.Step),
        ("explore", "Explorer", "◇", "1 PA", "E", true, Palette.Unknown),
        ("reveal", "Révéler", "☆", "1 PA", "R", true, Palette.Unknown),
        ("dig", "Creuser", "▼", "2 PA", "C", true, Palette.Rubble),
        ("run", "Courir", "»", "2 PA", "U", true, Palette.Step),
        ("attack", "Attaquer", "×", "1 PA", "A", false, Palette.Combat),
        ("heal", "Soigner", "♥", "1 PA", "H", false, Palette.Support),
        ("pickup", "Ramasser", "▲", "1 PA", "P", false, Palette.Support),
        ("drop", "Poser", "▽", "1 PA", "D", false, Palette.Support),
        ("overexert", "Se dépasser", "↑", "1 ♥", "O", false, Palette.Meta),
        ("endturn", "Finir le tour", "■", "—", "Espace", false, Palette.Meta),
    ];

    /// <summary>One column's width and height on the Actions card — narrow enough
    /// that all eleven, plus their dividers, still read as one object rather than a
    /// row of separate ones.</summary>
    private const float ColumnWidth = 66f;

    private const float ColumnHeight = 112f;

    private const float SpineWidth = 26f;

    /// <summary>One party card's footprint, top centre — narrow enough that a full
    /// six-strong party still reads as one row rather than spilling past the
    /// screen's edges.</summary>
    private const float PartyCardWidth = 100f;

    private const float PartyCardHeight = 132f;

    private const float PartyAvatarSize = 52f;

    /// <summary>How far a card that isn't due to play rests below the one that
    /// is — the "raised portrait" a turn-based fight puts forward, done here by
    /// pushing everyone else down rather than lifting the current explorer up.</summary>
    private const float PartyRaise = 16f;

    /// <summary>One compartment's width on the status plaque — narrower than an
    /// Actions column, since a number or a short word fits where an icon-and-name
    /// pair needed more room.</summary>
    private const float TrackerCompartmentWidth = 64f;

    private const float TrackerHeight = 60f;

    private const float TrackerSpineWidth = 22f;

    /// <summary>Every roster name reads "L'…", "Le …" or "La …" — stripped before
    /// an avatar's monogram is drawn from what follows.</summary>
    private static readonly string[] FrenchArticles = ["L'", "Le ", "La ", "Les "];

    /// <summary>
    /// The Actions card is paper, not cave wall — the one part of the screen the
    /// temple itself never has to be, so it reads at a glance instead of fighting
    /// the dark 3D view behind it for contrast. Every other card in the HUD is cut
    /// from the same paper.
    /// </summary>
    private static readonly Color Parchment = Color.Color8(222, 205, 172);

    private static readonly Color ParchmentShade = Color.Color8(203, 184, 149);

    private static readonly Color ParchmentSpine = Color.Color8(236, 224, 198);

    private static readonly Color ParchmentBorder = Color.Color8(34, 25, 18);

    private static readonly Color Ink = Color.Color8(40, 29, 20);

    /// <summary>The Notice ribbon's own tint — parchment pushed warmer, the way a
    /// printed card marks the one entry that needs a second look without leaving
    /// paper for a wholly different material.</summary>
    private static readonly Color NoticeTint = Color.Color8(233, 201, 184);

    /// <summary>
    /// A fine paper grain, shared by every parchment surface in the HUD — the card
    /// itself, its spine, a shaded column, a portrait's face — so none of them reads
    /// as one dead-flat colour. Applied as a <c>Material</c> on the specific Control
    /// that owns the fill; because a canvas_item shader only ever runs over pixels
    /// that Control itself rasterises, it never bleeds past a rounded corner onto
    /// the cave behind it, and it never touches the ink text or icons drawn by a
    /// separate child Control layered on top.
    /// </summary>
    private static readonly ShaderMaterial Grain = new()
    {
        Shader = GD.Load<Shader>("res://resources/shaders/paper_grain.gdshader"),
    };

    private const string MouseLegend =
        "Choisis une carte ci-dessous, puis clique la case visée   ·   V : vue FPS ↔ vue du dessus";

    private const string FpsLegend =
        "Souris : regarder   ·   Choisis une carte, puis clique ce qui est devant vous   ·   Échap : vue du dessus";

    /// <summary>Any targeted card once it is armed and waiting on a cell.</summary>
    private const string TargetLegend =
        "Clique la case visée   ·   Échap ou clic droit : annuler";

    /// <summary>Creuser alone can target the explorer's own tile, not just a neighbour.</summary>
    private const string DigLegend =
        "Clique sa propre case ou une case voisine connectée pour la creuser   ·   Échap ou clic droit : annuler";

    /// <summary>Courir builds its route one click at a time instead of firing at once.</summary>
    private const string RunLegend =
        "Clique jusqu'à 3 cases pour construire l'itinéraire · reclique Courir pour valider plus tôt   ·   Échap ou clic droit : annuler";

    /// <summary>What to do with a tile that is out of the bag but not yet laid.</summary>
    private const string TurningLegend =
        "Molette ou R : tourner la tuile   ·   Clic sur la case : la poser   ·   V : vue FPS ↔ vue du dessus";

    /// <summary>What to do with a Guardian that is stirring but not yet awake.</summary>
    private const string StirringLegend =
        "Molette ou R : changer de case Gardien   ·   Clic sur la case : l'y réveiller";

    private VBoxContainer _col = null!;
    private Control _noticeRibbon = null!;
    private Label _noticeLabel = null!;
    private Label _roundValue = null!;
    private Label _leaderValue = null!;
    private Label _bagValue = null!;
    private Label _eruptionValue = null!;
    private readonly List<Label> _keyPips = [];
    private Label _logLabel = null!;
    private Label _hintBar = null!;
    private HBoxContainer _party = null!;
    private PanelContainer _actions = null!;
    private PanelContainer _decision = null!;
    private Label _prompt = null!;
    private VBoxContainer _options = null!;
    private Control _crosshair = null!;

    /// <summary>Every action's column, by its name, so <see cref="Show"/> can redraw
    /// its state without rebuilding the card each time.</summary>
    private readonly Dictionary<string, Button> _actionCards = [];

    /// <summary>The options as written, so the one laid out on the board can be
    /// badged without the badge ending up in the next label.</summary>
    private readonly List<string> _labels = [];

    /// <summary>Whether this machine is currently looking through an Explorer's
    /// eyes — changes which legend the hint line shows.</summary>
    private bool _aiming;

    public override void _Ready()
    {
        _col = GetNode<VBoxContainer>("%Col");
        _party = GetNode<HBoxContainer>("%Party");
        _actions = GetNode<PanelContainer>("%Actions");
        _decision = GetNode<PanelContainer>("%Decision");
        _prompt = GetNode<Label>("%Prompt");
        _options = GetNode<VBoxContainer>("%Options");
        _crosshair = GetNode<Control>("%Crosshair");

        _decision.Material = Grain;

        BuildStatusColumn();
        BuildActionsCard();
    }

    /// <summary>
    /// The status column, top-left: the same three objects a table would actually
    /// carry there — an alert ribbon (only when there is one), the expedition's own
    /// tracker plaque, and the last event as a note scribbled and left to one side.
    /// </summary>
    private void BuildStatusColumn()
    {
        _col.AddThemeConstantOverride("separation", 12);

        _col.AddChild(BuildNoticeRibbon());
        _col.AddChild(BuildTracker());
        _col.AddChild(BuildLogScrap());
    }

    /// <summary>
    /// The alert ribbon: parchment pushed warmer, capped by a plain-coloured stripe
    /// rather than rounded off like the rest of the paper, the way a card marks the
    /// one entry that needs a second look. Hidden entirely when there is nothing to
    /// say — never an empty strip of paper sitting there for no reason.
    /// </summary>
    private Control BuildNoticeRibbon()
    {
        var ribbon = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        ribbon.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = NoticeTint,
            BorderColor = ParchmentBorder,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        });
        ribbon.Material = Grain;

        var layout = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 0);

        layout.AddChild(new ColorRect
        {
            Color = Palette.Choice,
            CustomMinimumSize = new Vector2(0, 3),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var pad = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        pad.AddThemeConstantOverride("margin_left", 12);
        pad.AddThemeConstantOverride("margin_right", 12);
        pad.AddThemeConstantOverride("margin_top", 8);
        pad.AddThemeConstantOverride("margin_bottom", 8);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);

        var bang = new Label { Text = "!", MouseFilter = Control.MouseFilterEnum.Ignore };
        bang.AddThemeColorOverride("font_color", Palette.Choice);
        bang.AddThemeFontSizeOverride("font_size", 15);
        row.AddChild(bang);

        _noticeLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _noticeLabel.AddThemeColorOverride("font_color", Ink);
        _noticeLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(_noticeLabel);

        pad.AddChild(row);
        layout.AddChild(pad);
        ribbon.AddChild(layout);

        _noticeRibbon = ribbon;
        return ribbon;
    }

    /// <summary>
    /// The expedition's own tracker plaque: a spine down the left edge echoing the
    /// Actions card's, then one compartment apiece for the round, the Chef, the bag,
    /// the eruption and the keys — divided by the same thin rule, so it reads as the
    /// Actions card's sibling rather than a different kind of object.
    /// </summary>
    private Control BuildTracker()
    {
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Parchment,
            BorderColor = ParchmentBorder,
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        });
        card.Material = Grain;

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 0);

        var spine = new PanelContainer
        {
            CustomMinimumSize = new Vector2(TrackerSpineWidth, TrackerHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        spine.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = ParchmentSpine,
            CornerRadiusTopLeft = 7,
            CornerRadiusBottomLeft = 7,
        });
        spine.Material = Grain;
        row.AddChild(spine);
        row.AddChild(Divider());

        row.AddChild(BuildStat(out _roundValue, "Manche"));
        row.AddChild(Divider());
        row.AddChild(BuildStat(out _leaderValue, "Chef", valueFontSize: 11, width: TrackerCompartmentWidth * 1.6f));
        row.AddChild(Divider());
        row.AddChild(BuildStat(out _bagValue, "Sac"));
        row.AddChild(Divider());
        row.AddChild(BuildStat(out _eruptionValue, "Éruption"));
        row.AddChild(Divider());
        row.AddChild(BuildKeysCompartment());

        card.AddChild(row);
        return card;
    }

    /// <summary>One tracker compartment: a value and, printed underneath the way
    /// the Actions card notes a cost, a short caption naming what it is.</summary>
    private static Control BuildStat(out Label value, string caption, int valueFontSize = 16, float width = TrackerCompartmentWidth)
    {
        var col = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        col.AddThemeConstantOverride("separation", 1);

        value = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        value.AddThemeColorOverride("font_color", Ink);
        value.AddThemeFontSizeOverride("font_size", valueFontSize);

        var cap = new Label
        {
            Text = caption,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        cap.AddThemeColorOverride("font_color", Ink with { A = 0.65f });
        cap.AddThemeFontSizeOverride("font_size", 9);

        col.AddChild(value);
        col.AddChild(cap);
        return col;
    }

    /// <summary>The Clés compartment: a pip apiece, the way <see cref="BuildHearts"/>
    /// already marks a heart spent instead of erasing it — filled and dimmed pips
    /// share the same glyph rather than swapping to a hollow one.</summary>
    private Control BuildKeysCompartment()
    {
        var col = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(TrackerCompartmentWidth, 0),
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        col.AddThemeConstantOverride("separation", 1);

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 1);

        for (var i = 0; i < GameState.KeysToUnlock; i++)
        {
            var pip = new Label { Text = "◆", MouseFilter = Control.MouseFilterEnum.Ignore };
            pip.AddThemeFontSizeOverride("font_size", 14);
            row.AddChild(pip);
            _keyPips.Add(pip);
        }

        var cap = new Label
        {
            Text = "Clés",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        cap.AddThemeColorOverride("font_color", Ink with { A = 0.65f });
        cap.AddThemeFontSizeOverride("font_size", 9);

        col.AddChild(row);
        col.AddChild(cap);
        return col;
    }

    /// <summary>
    /// The last event, left as a small note rather than a line of text — tilted a
    /// touch and inset from the tracker plaque above it, the way a scrap actually
    /// dropped on a table wouldn't square up with everything else.
    /// </summary>
    private Control BuildLogScrap()
    {
        var scrap = new PanelContainer
        {
            CustomMinimumSize = new Vector2(250, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            RotationDegrees = -1.6f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        scrap.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = ParchmentShade,
            BorderColor = ParchmentBorder,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ShadowColor = new Color(0f, 0f, 0f, 0.35f),
            ShadowSize = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });
        scrap.Material = Grain;

        _logLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _logLabel.AddThemeColorOverride("font_color", Ink);
        _logLabel.AddThemeFontSizeOverride("font_size", 12);
        scrap.AddChild(_logLabel);

        // A left inset around the tilted note, the same "dropped by hand, not
        // squared up with the plaque above" touch the rotation itself gives it.
        var offset = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        offset.AddThemeConstantOverride("margin_left", 16);
        offset.AddChild(scrap);
        return offset;
    }

    /// <summary>
    /// The one Actions card: a single bordered sheet of parchment — light against
    /// the dark of the temple on purpose, the same way a printed card would sit on
    /// the table rather than try to look like the cave itself — with a titled spine
    /// down the left edge and every action as a column of its own beside it, divided
    /// by a thin rule instead of by a border each. What a click on the board would
    /// now do is printed on a strip along the card's own foot, rather than floating
    /// as a separate line above it.
    /// </summary>
    private void BuildActionsCard()
    {
        _actions.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Parchment,
            BorderColor = ParchmentBorder,
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        });
        _actions.Material = Grain;

        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 0);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 0);

        row.AddChild(BuildSpine());
        row.AddChild(Divider());

        foreach (var (index, entry) in Cards.Select((card, index) => (index, card)))
        {
            if (index > 0)
            {
                row.AddChild(Divider());
            }

            var (action, name, icon, cost, key, targeted, accent) = entry;
            var id = action;
            var column = BuildColumn(name, icon, cost, key, accent, targeted, index % 2 == 0);
            column.Pressed += () => EmitSignal(SignalName.ActionRequested, id);
            row.AddChild(column);
            _actionCards[action] = column;
        }

        stack.AddChild(row);
        stack.AddChild(BuildHintBar());
        _actions.AddChild(stack);
    }

    /// <summary>The strip along the Actions card's own foot — what a click on the
    /// board would now do, printed the way the card's cost and key already are,
    /// instead of a line of text left to float above the card.</summary>
    private Control BuildHintBar()
    {
        var bar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = ParchmentShade,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 5,
            ContentMarginBottom = 5,
        });
        bar.Material = Grain;

        _hintBar = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _hintBar.AddThemeColorOverride("font_color", Ink with { A = 0.75f });
        _hintBar.AddThemeFontSizeOverride("font_size", 12);
        bar.AddChild(_hintBar);

        return bar;
    }

    /// <summary>The title strip down the card's left edge — its own, lighter shade
    /// of the same paper, "ACTIONS" run down it letter by letter rather than turned
    /// sideways, which no Control lays out for free.</summary>
    private static Control BuildSpine()
    {
        var spine = new PanelContainer
        {
            CustomMinimumSize = new Vector2(SpineWidth, ColumnHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        spine.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = ParchmentSpine,
            CornerRadiusTopLeft = 7,
            CornerRadiusBottomLeft = 7,
        });
        spine.Material = Grain;

        var letters = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        letters.AddThemeConstantOverride("separation", 0);

        foreach (var letter in "ACTIONS")
        {
            var label = new Label
            {
                Text = letter.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeColorOverride("font_color", Ink);
            label.AddThemeFontSizeOverride("font_size", 11);
            letters.AddChild(label);
        }

        spine.AddChild(letters);
        return spine;
    }

    /// <param name="mine">
    /// Whether the seat that is due to act belongs to this machine. On a networked
    /// table the others' turns are watched, not played.
    /// </param>
    /// <param name="armed">
    /// The targeted card currently waiting on a cell, or <c>null</c> when none is —
    /// stays pressed down on the table and swaps the hint line to say what a click
    /// on the board would now do.
    /// </param>
    /// <param name="canArm">
    /// Whether a targeted card has anything to point at right now. Purely for
    /// comfort — greying out "Creuser" with no Éboulis in reach — the engine is the
    /// one that actually decides what a command does once it is sent.
    /// </param>
    public void Show(GameState game, bool mine, string notice, string? armed, Func<string, bool> canArm)
    {
        _noticeLabel.Text = notice;
        _noticeRibbon.Visible = notice.Length > 0;

        _roundValue.Text = (game.Round + 1).ToString();
        _leaderValue.Text = game.Leader.Name;
        _bagValue.Text = game.Bag.Count.ToString();
        _eruptionValue.Text = game.EruptionCountdown.ToString();
        UpdateKeyPips(game.KeysDeposited);

        ShowParty(game);
        ShowDecision(game, mine);

        // Nothing may be done while the game is waiting on an answer, once it is over,
        // or while it is somebody else's turn.
        var frozen = game.IsOver || game.Pending is not null || !mine;

        foreach (var (action, _, _, _, _, targeted, _) in Cards)
        {
            var card = _actionCards[action];
            var disabled = frozen || (targeted && !canArm(action));

            card.Disabled = disabled;
            // Greys the whole printed card at once — face, icon and text together —
            // rather than fading each label by hand.
            card.Modulate = disabled ? new Color(1f, 1f, 1f, 0.4f) : Colors.White;

            if (targeted)
            {
                // A toggle card: it stays pressed down on the table while it is the
                // one waiting on a cell, the same way ButtonPressed already draws a
                // pressed button without any text of ours to swap.
                card.ButtonPressed = action == armed;
            }
        }

        _hintBar.Text = (game.IsOver, game.Pending, mine, armed) switch
        {
            (true, _, _, _) => "La partie est terminée.   ·   V : vue FPS ↔ vue du dessus",
            (_, { Kind: DecisionKind.TileOrientation }, true, _) => TurningLegend,
            (_, { Kind: DecisionKind.GuardianAwakening }, true, _) => StirringLegend,
            (_, null, true, "dig") => DigLegend,
            (_, null, true, "run") => RunLegend,
            (_, null, true, not null) => TargetLegend,
            _ => _aiming ? FpsLegend : MouseLegend,
        };
    }

    /// <summary>The last thing that happened, or the reason nothing did.</summary>
    public void Say(string message) => _logLabel.Text = message;

    /// <summary>Recolours the Clés pips to match how many are actually deposited —
    /// filled and amber up to that many, dimmed after, the same trick <see
    /// cref="BuildHearts"/> uses for a spent heart.</summary>
    private void UpdateKeyPips(int deposited)
    {
        for (var i = 0; i < _keyPips.Count; i++)
        {
            _keyPips[i].AddThemeColorOverride("font_color", i < deposited ? Palette.Key : Ink with { A = 0.3f });
        }
    }

    /// <summary>Shows or hides the centre crosshair a click acts on in the FPS view,
    /// and swaps the hint line to match.</summary>
    public void SetAiming(bool aiming)
    {
        _aiming = aiming;
        _crosshair.Visible = aiming;
    }

    private void ShowParty(GameState game)
    {
        foreach (var child in _party.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var explorer in game.Explorers)
        {
            _party.AddChild(BuildPartyCard(game, explorer));
        }
    }

    /// <summary>
    /// One Explorer's card: an avatar placeholder, their name, their hearts, and
    /// whatever badges apply — a leader's star, a carried item, or why they can no
    /// longer act. The one due to play sits raised above the rest of the row and
    /// lit by its own accent, the way a turn-based fight puts the active portrait
    /// forward rather than leaving the roster to read as a plain list.
    /// </summary>
    private static Control BuildPartyCard(GameState game, Explorer explorer)
    {
        var current = explorer.Id == game.CurrentExplorer.Id && !game.IsOver;
        var accent = Palette.For(explorer.Id);
        var resting = explorer.IsDown || explorer.IsDead || explorer.HasEscaped;

        // Nobody is actually lifted — the rest of the row is pushed down instead,
        // which reads the same way and keeps every card's top edge easy to align.
        var raise = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        raise.AddThemeConstantOverride("margin_top", current ? 0 : (int)PartyRaise);

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(PartyCardWidth, PartyCardHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            // Greys the whole card at once, the same trick the Actions card uses
            // for one it can't be played from right now.
            Modulate = resting ? new Color(1f, 1f, 1f, 0.55f) : Colors.White,
        };

        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Parchment,
            BorderColor = current ? accent : ParchmentBorder,
            BorderWidthLeft = current ? 3 : 1,
            BorderWidthTop = current ? 3 : 1,
            BorderWidthRight = current ? 3 : 1,
            BorderWidthBottom = current ? 3 : 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        });
        card.Material = Grain;

        var layout = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 3);

        layout.AddChild(BuildAvatar(explorer, accent, current, explorer.Id == game.Leader.Id));

        var nameLabel = new Label
        {
            Text = explorer.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        nameLabel.AddThemeColorOverride("font_color", Ink);
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        layout.AddChild(nameLabel);

        layout.AddChild(BuildHearts(explorer));

        var statusLabel = new Label
        {
            Text = Standing(game, explorer, current),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        statusLabel.AddThemeColorOverride("font_color", Ink with { A = 0.65f });
        statusLabel.AddThemeFontSizeOverride("font_size", 10);
        layout.AddChild(statusLabel);

        card.AddChild(layout);
        raise.AddChild(card);
        return raise;
    }

    /// <summary>
    /// The avatar placeholder: a monogram on a tile tinted the Explorer's own
    /// colour, standing in for the portrait art the project doesn't have yet. A
    /// leader's star and a carried item both sit as small badges on its corners
    /// rather than as more text squeezed under the name.
    /// </summary>
    private static Control BuildAvatar(Explorer explorer, Color accent, bool current, bool isLeader)
    {
        var slot = new Control
        {
            CustomMinimumSize = new Vector2(PartyAvatarSize, PartyAvatarSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var face = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        face.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        face.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = accent with { A = current ? 1f : 0.55f },
            BorderColor = ParchmentBorder with { A = 0.6f },
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });

        var monogram = new Label
        {
            Text = Monogram(explorer.Name),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        monogram.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        monogram.AddThemeColorOverride("font_color", Colors.White);
        monogram.AddThemeColorOverride("font_outline_color", Colors.Black);
        monogram.AddThemeConstantOverride("outline_size", 4);
        monogram.AddThemeFontSizeOverride("font_size", 22);
        face.AddChild(monogram);

        slot.AddChild(face);

        if (isLeader)
        {
            slot.AddChild(BuildBadge("★", Palette.Key, Control.LayoutPreset.TopLeft));
        }

        if (explorer.Carried is { } item)
        {
            slot.AddChild(BuildBadge(
                item == ItemKind.Key ? "◆" : "✦",
                item == ItemKind.Key ? Palette.Key : Palette.Artefact,
                Control.LayoutPreset.BottomRight));
        }

        return slot;
    }

    /// <summary>A small round badge overlapping one corner of the avatar — a
    /// leader's star or a carried item, in the same colour the rest of the board
    /// already uses for that thing.</summary>
    private static Control BuildBadge(string glyph, Color tint, Control.LayoutPreset corner)
    {
        var badge = new PanelContainer
        {
            CustomMinimumSize = new Vector2(18, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        badge.SetAnchorsAndOffsetsPreset(corner, Control.LayoutPresetMode.KeepSize, -6);
        badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = tint,
            BorderColor = ParchmentBorder,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
        });

        var label = new Label
        {
            Text = glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", Ink);
        label.AddThemeFontSizeOverride("font_size", 10);
        badge.AddChild(label);

        return badge;
    }

    /// <summary>Hearts as two tones on one row — spent ones don't vanish, they read
    /// as empty pips, the way a paper sheet's own hearts get crossed off rather
    /// than erased.</summary>
    private static Control BuildHearts(Explorer explorer)
    {
        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 0);

        var filled = new Label
        {
            Text = new string('♥', explorer.Health),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        filled.AddThemeColorOverride("font_color", Palette.Combat);
        filled.AddThemeFontSizeOverride("font_size", 13);

        var empty = new Label
        {
            Text = new string('♥', explorer.MaxHealth - explorer.Health),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        empty.AddThemeColorOverride("font_color", Ink with { A = 0.25f });
        empty.AddThemeFontSizeOverride("font_size", 13);

        row.AddChild(filled);
        row.AddChild(empty);
        return row;
    }

    /// <summary>
    /// The roster's names are all "L'…", "Le …" or "La …" — a bare first letter
    /// would read "L" on almost every card, so the article is stripped first, the
    /// way a French index already would.
    /// </summary>
    private static string Monogram(string name)
    {
        var stripped = name;
        foreach (var article in FrenchArticles)
        {
            if (stripped.StartsWith(article, StringComparison.Ordinal))
            {
                stripped = stripped[article.Length..];
                break;
            }
        }

        return stripped.Length > 0 ? stripped[..1].ToUpperInvariant() : "?";
    }

    private void ShowDecision(GameState game, bool mine)
    {
        foreach (var child in _options.GetChildren())
        {
            child.QueueFree();
        }

        if (game.Pending is not { } decision)
        {
            _decision.Visible = false;
            return;
        }

        _decision.Visible = true;

        // What the board can show is weighed up by looking at the temple, not at a
        // list: say so, or the buttons read as several names for the same thing.
        var aside = decision.Kind switch
        {
            DecisionKind.TileOrientation =>
                "\nElle est posée à l'essai sur le plateau : molette ou R pour la tourner.",
            DecisionKind.GuardianAwakening =>
                "\nIl se dresse à l'essai sur le plateau : molette ou R pour changer de case.",
            _ => string.Empty,
        };

        _prompt.Text = $"{game.Chooser(decision).Name} tranche\n{decision.Prompt}{aside}";

        _labels.Clear();

        foreach (var (option, index) in decision.Options.Select((option, index) => (option, index)))
        {
            var taken = index;
            var button = Press(option.Label);
            button.Disabled = !mine;
            button.Pressed += () => EmitSignal(SignalName.OptionChosen, taken);

            // Pointing at an option lays it out on the board; it stays laid out when
            // the pointer leaves, so the answer can be given on the temple itself.
            button.MouseEntered += () => EmitSignal(SignalName.OptionPreviewed, taken);
            _options.AddChild(button);
            _labels.Add(option.Label);
        }
    }

    /// <summary>
    /// Badges the answer currently laid out on the board, so the list and the temple
    /// never disagree about which one a click would take.
    /// </summary>
    public void Highlight(int option)
    {
        foreach (var (button, index) in _options.GetChildren().OfType<Button>().Select((b, i) => (b, i)))
        {
            if (index < _labels.Count)
            {
                button.Text = $"{(index == option ? "▶" : " ")}   {_labels[index]}";
            }
        }
    }

    /// <summary>
    /// A plain button that never takes the keyboard focus — otherwise Space would
    /// press the last thing clicked instead of ending the turn. Used for the
    /// arbitration options, which read fine as a list rather than as columns of
    /// their own — but still on the same paper as the Actions card, not the
    /// project's default dark theme.
    /// </summary>
    private static Button Press(string text)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
        };

        button.AddThemeStyleboxOverride("normal", OptionFace(0.12f));
        button.AddThemeStyleboxOverride("hover", OptionFace(0.28f));
        button.AddThemeStyleboxOverride("pressed", OptionFace(0.4f));
        button.AddThemeStyleboxOverride("hover_pressed", OptionFace(0.4f));
        button.AddThemeStyleboxOverride("disabled", OptionFace(0.05f));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            button.AddThemeColorOverride(state, Ink);
        }

        button.AddThemeColorOverride("font_disabled_color", Ink with { A = 0.5f });

        return button;
    }

    /// <summary>One arbitration option's face — the same warm fill at a few
    /// strengths, rather than a border and a shadow it would have to share with the
    /// panel it already sits inside.</summary>
    private static StyleBoxFlat OptionFace(float alpha) => new()
    {
        BgColor = ParchmentBorder with { A = alpha },
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        ContentMarginLeft = 10,
        ContentMarginRight = 10,
        ContentMarginTop = 6,
        ContentMarginBottom = 6,
    };

    /// <summary>
    /// One column of the Actions card: an icon, its name, and — printed underneath
    /// the way a reference card notes it — the action points it spends and the key
    /// that also plays it. Ink on paper, the way the card it copies is — colour is
    /// saved for the moment a column lights up, not spent on telling them apart at
    /// rest. <paramref name="shaded"/> alternates a faint band across the sheet, the
    /// way a printed card's columns are rarely quite the same paper twice.
    /// <paramref name="targeted"/> columns stay lit while armed (<see cref="Show"/>
    /// drives that through <c>ButtonPressed</c>); the rest only ever flash on the
    /// way past.
    /// </summary>
    private static Button BuildColumn(
        string name, string icon, string cost, string key, Color accent, bool targeted, bool shaded)
    {
        var column = new Button
        {
            FocusMode = Control.FocusModeEnum.None,
            ToggleMode = targeted,
            Flat = true,
            CustomMinimumSize = new Vector2(ColumnWidth, ColumnHeight),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        column.Material = Grain;

        StyleBox rest = shaded ? ColumnFill(ParchmentShade, 1f, rounded: false) : new StyleBoxEmpty();

        column.AddThemeStyleboxOverride("normal", rest);
        column.AddThemeStyleboxOverride("hover", ColumnFill(accent, 0.3f, rounded: true));
        column.AddThemeStyleboxOverride("pressed", ColumnFill(accent, 0.45f, rounded: true));
        column.AddThemeStyleboxOverride("hover_pressed", ColumnFill(accent, 0.45f, rounded: true));
        column.AddThemeStyleboxOverride("disabled", rest);
        column.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_right", 4);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 4);

        var layout = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 2);

        var iconLabel = new Label
        {
            Text = icon,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        iconLabel.AddThemeColorOverride("font_color", Ink);
        iconLabel.AddThemeFontSizeOverride("font_size", 26);

        var nameLabel = new Label
        {
            Text = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        nameLabel.AddThemeColorOverride("font_color", Ink);
        nameLabel.AddThemeFontSizeOverride("font_size", 12);

        var spacer = new Control
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        // Cost above key, both centred: printed the way a card notes what a symbol
        // costs, without the two crowding each other in so narrow a column.
        var footer = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        footer.AddThemeConstantOverride("separation", 0);

        var costLabel = new Label
        {
            Text = cost,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        costLabel.AddThemeColorOverride("font_color", Ink with { A = 0.7f });
        costLabel.AddThemeFontSizeOverride("font_size", 10);

        var keyLabel = new Label
        {
            Text = key,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        keyLabel.AddThemeColorOverride("font_color", Ink with { A = 0.7f });
        keyLabel.AddThemeFontSizeOverride("font_size", 9);

        footer.AddChild(costLabel);
        footer.AddChild(keyLabel);

        layout.AddChild(iconLabel);
        layout.AddChild(nameLabel);
        layout.AddChild(spacer);
        layout.AddChild(footer);

        margin.AddChild(layout);
        column.AddChild(margin);

        return column;
    }

    /// <summary>
    /// A flat tint over the paper — the resting shade a column alternates with its
    /// neighbour when <paramref name="rounded"/> is <c>false</c>, or the colour a
    /// column lights up under the mouse or while armed when it is <c>true</c>: the
    /// one border around the whole sheet is what says "card" here, not each column
    /// separately, so a highlight never gets a border or a shadow of its own.
    /// </summary>
    private static StyleBoxFlat ColumnFill(Color tint, float alpha, bool rounded)
    {
        var style = new StyleBoxFlat { BgColor = tint with { A = alpha } };

        if (rounded)
        {
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
        }

        return style;
    }

    /// <summary>The thin rule between two columns, the way a printed reference card
    /// divides its own.</summary>
    private static ColorRect Divider() => new()
    {
        Color = ParchmentBorder with { A = 0.4f },
        CustomMinimumSize = new Vector2(1.5f, 0f),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    /// <summary>The status line under a card's hearts — why they can't act, or how
    /// much they still can this turn. Carrying something is shown as a badge on the
    /// avatar instead, so this stays one short word.</summary>
    private static string Standing(GameState game, Explorer explorer, bool current) => explorer switch
    {
        { HasEscaped: true } => "sorti",
        { IsDead: true } => "englouti",
        { IsDown: true } => "à terre",
        _ when current => $"{game.ActionPoints} PA",
        _ => string.Empty,
    };
}
