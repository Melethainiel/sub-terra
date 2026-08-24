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
    /// <summary>A button was pressed. The name is the one the keyboard uses too.</summary>
    [Signal]
    public delegate void ActionRequestedEventHandler(string action);

    /// <summary>An arbitration was settled: which option was taken.</summary>
    [Signal]
    public delegate void OptionChosenEventHandler(int option);

    /// <summary>The actions that need no target beyond the explorer's own tile.</summary>
    private static readonly (string Action, string Label, string Key)[] Buttons =
    [
        ("attack", "Attaquer", "A"),
        ("heal", "Soigner", "H"),
        ("pickup", "Ramasser", "P"),
        ("drop", "Poser", "D"),
        ("overexert", "Se dépasser", "O"),
        ("endturn", "Finir le tour", "Espace"),
    ];

    private const string MouseLegend =
        "Clic : avancer ou explorer   ·   Maj+clic : révéler sans entrer   ·   Ctrl+clic : creuser   ·   V : vue FPS ↔ vue du dessus";

    private const string FpsLegend =
        "Souris : regarder   ·   Clic : agir sur ce qui est devant vous   ·   Maj/Ctrl+clic : révéler/creuser   ·   Échap : vue du dessus";

    private Label _header = null!;
    private Label _notice = null!;
    private VBoxContainer _party = null!;
    private Label _log = null!;
    private Label _hint = null!;
    private HBoxContainer _actions = null!;
    private PanelContainer _decision = null!;
    private Label _prompt = null!;
    private VBoxContainer _options = null!;
    private Control _crosshair = null!;

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
        _actions = GetNode<HBoxContainer>("%Actions");
        _decision = GetNode<PanelContainer>("%Decision");
        _prompt = GetNode<Label>("%Prompt");
        _options = GetNode<VBoxContainer>("%Options");
        _crosshair = GetNode<Control>("%Crosshair");

        _hint.Text = MouseLegend;

        foreach (var (action, label, key) in Buttons)
        {
            var name = action;
            var button = Press($"{label}   {key}");
            button.Pressed += () => EmitSignal(SignalName.ActionRequested, name);
            _actions.AddChild(button);
        }
    }

    /// <param name="mine">
    /// Whether the seat that is due to act belongs to this machine. On a networked
    /// table the others' turns are watched, not played.
    /// </param>
    public void Show(GameState game, bool mine = true, string notice = "")
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

        foreach (var button in _actions.GetChildren().OfType<Button>())
        {
            button.Disabled = frozen;
        }

        _hint.Text = game.IsOver
            ? "La partie est terminée.   ·   V : vue FPS ↔ vue du dessus"
            : _aiming ? FpsLegend : MouseLegend;
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
        _prompt.Text = $"{game.Chooser(decision).Name} tranche\n{decision.Prompt}";

        foreach (var (option, index) in decision.Options.Select((option, index) => (option, index)))
        {
            var taken = index;
            var button = Press(option.Label);
            button.Disabled = !mine;
            button.Pressed += () => EmitSignal(SignalName.OptionChosen, taken);
            _options.AddChild(button);
        }
    }

    /// <summary>
    /// A button that never takes the keyboard focus — otherwise Space would press the
    /// last thing clicked instead of ending the turn.
    /// </summary>
    private static Button Press(string text) => new()
    {
        Text = text,
        FocusMode = Control.FocusModeEnum.None,
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
