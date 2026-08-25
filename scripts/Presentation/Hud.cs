using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// The expedition down the side of the screen, its actions along the bottom, and —
/// when the rules hand a tie to a player — the question in the middle of it. The HUD
/// asks for commands and never carries them out itself.
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

    /// <summary>
    /// The Actions card is paper, not cave wall — the one part of the screen the
    /// temple itself never has to be, so it reads at a glance instead of fighting
    /// the dark 3D view behind it for contrast.
    /// </summary>
    private static readonly Color Parchment = Color.Color8(222, 205, 172);

    private static readonly Color ParchmentShade = Color.Color8(203, 184, 149);

    private static readonly Color ParchmentSpine = Color.Color8(236, 224, 198);

    private static readonly Color ParchmentBorder = Color.Color8(34, 25, 18);

    private static readonly Color Ink = Color.Color8(40, 29, 20);

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

    private Label _header = null!;
    private Label _notice = null!;
    private VBoxContainer _party = null!;
    private Label _log = null!;
    private Label _hint = null!;
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
        _header = GetNode<Label>("%Header");
        _notice = GetNode<Label>("%Notice");
        _party = GetNode<VBoxContainer>("%Party");
        _log = GetNode<Label>("%Log");
        _hint = GetNode<Label>("%Hint");
        _actions = GetNode<PanelContainer>("%Actions");
        _decision = GetNode<PanelContainer>("%Decision");
        _prompt = GetNode<Label>("%Prompt");
        _options = GetNode<VBoxContainer>("%Options");
        _crosshair = GetNode<Control>("%Crosshair");

        _hint.Text = MouseLegend;

        BuildActionsCard();
    }

    /// <summary>
    /// The one Actions card: a single bordered sheet of parchment — light against
    /// the dark of the temple on purpose, the same way a printed card would sit on
    /// the table rather than try to look like the cave itself — with a titled spine
    /// down the left edge and every action as a column of its own beside it, divided
    /// by a thin rule instead of by a border each.
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
        });

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

        _actions.AddChild(row);
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
        _notice.Text = notice;
        _notice.Visible = notice.Length > 0;

        _header.Text = string.Join("   ·   ",
            $"Manche {game.Round + 1}",
            $"Chef ★ {game.Leader.Name}",
            $"Sac {game.Bag.Count}",
            $"Éruption {game.EruptionCountdown}",
            $"Clés {game.KeysDeposited}/{GameState.KeysToUnlock}");

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

        _hint.Text = (game.IsOver, game.Pending, mine, armed) switch
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
    public void Say(string message) => _log.Text = message;

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
            var current = explorer.Id == game.CurrentExplorer.Id && !game.IsOver;

            var label = new Label
            {
                Text = string.Join("   ", Badge(game, explorer), Hearts(explorer), Standing(game, explorer)),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };

            // The line and the meeple are the same colour, so the roster reads as the board.
            label.AddThemeColorOverride("font_color", current ? Palette.For(explorer.Id) : Palette.Faded);
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 5);
            label.AddThemeFontSizeOverride("font_size", 16);

            _party.AddChild(label);
        }
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

    private static string Badge(GameState game, Explorer explorer) => string.Concat(
        explorer.Id == game.CurrentExplorer.Id && !game.IsOver ? "▶" : " ",
        explorer.Id == game.Leader.Id ? "★" : " ",
        " ",
        explorer.Name);

    private static string Hearts(Explorer explorer) =>
        new string('♥', explorer.Health) + new string('·', explorer.MaxHealth - explorer.Health);

    private static string Standing(GameState game, Explorer explorer)
    {
        var carried = explorer.Carried is { } item ? $"   porte {Say(item)}" : string.Empty;

        return explorer switch
        {
            { HasEscaped: true } => "sorti du temple" + carried,
            { IsDead: true } => "englouti",
            { IsDown: true } => "à terre" + carried,
            _ when explorer.Id == game.CurrentExplorer.Id && !game.IsOver =>
                $"{game.ActionPoints} action(s)" + carried,
            _ => carried.TrimStart(),
        };
    }

    private static string Say(ItemKind item) => item switch
    {
        ItemKind.Key => "une Clé",
        ItemKind.Artefact => "l'Artefact",
        _ => item.ToString(),
    };
}
