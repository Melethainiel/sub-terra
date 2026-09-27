using Godot;

namespace SubTerra.Presentation;

/// <summary>
/// The table's own menu — Échap, or the Menu button top right: go back to the game,
/// save it, read the rules, leave for the accueil or quit altogether. The game saves
/// itself after every action; the menu says so, and when it last did.
/// </summary>
public partial class Hud
{
    /// <summary>What was chosen: open, resume, save, help, home, quit.</summary>
    [Signal]
    public delegate void MenuChosenEventHandler(string choice);

    private Control? _menu;

    private PanelContainer? _toast;

    public bool MenuOpen => _menu is not null;

    public bool HelpOpen => _help is not null;

    /// <summary>The Menu button, top right, for whoever looks for one before trying Échap.</summary>
    private void BuildMenuButton()
    {
        var button = new Button
        {
            Text = "☰  Menu",
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -118f,
            OffsetRight = -16f,
            OffsetTop = 14f,
            OffsetBottom = 48f,
        };
        button.AddThemeStyleboxOverride("normal", WithMargins(Paper.CardStyle(bg: Parchment, borderWidth: 2f, radius: 8f), 10, 4));
        button.AddThemeStyleboxOverride("hover", WithMargins(Paper.CardStyle(bg: Paper.CardSpine, border: Paper.Sienna, borderWidth: 2f, radius: 8f), 10, 4));
        button.AddThemeStyleboxOverride("pressed", WithMargins(Paper.CardStyle(bg: ParchmentShade, borderWidth: 2f, radius: 8f), 10, 4));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeFontSizeOverride("font_size", 14);
        button.Material = Grain;
        button.Pressed += () => EmitSignal(SignalName.MenuChosen, "open");
        AddChild(button);
    }

    /// <summary>Opens the menu, or closes it. <paramref name="saveNote"/> says how the game
    /// is being kept — automatically, and when last; or by the host.</summary>
    public void ToggleMenu(string saveNote, bool canSave)
    {
        if (_menu is not null)
        {
            _menu.QueueFree();
            _menu = null;
            return;
        }

        var veil = new ColorRect { Color = new Color(0f, 0f, 0f, 0.5f), MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        veil.AddChild(centre);

        var card = new PanelContainer { Material = Grain, CustomMinimumSize = new Vector2(360f, 0f) };
        card.AddThemeStyleboxOverride("panel", WithMargins(Paper.CardStyle(borderWidth: 3f, radius: 14f), 30, 22));
        centre.AddChild(card);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        card.AddChild(column);

        var title = new Label { Text = "Menu", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", Paper.Sienna);
        column.AddChild(title);

        column.AddChild(MenuButton("Reprendre la partie", "resume", primary: true));

        if (canSave)
        {
            column.AddChild(MenuButton("Sauvegarder", "save"));
        }

        column.AddChild(MenuButton("Aide (F1)", "help"));
        column.AddChild(MenuButton("Quitter vers l'accueil", "home"));
        column.AddChild(MenuButton("Quitter le jeu", "quit"));

        var note = new Label
        {
            Text = saveNote,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(300f, 0f),
        };
        note.AddThemeFontSizeOverride("font_size", 12);
        note.AddThemeColorOverride("font_color", Ink with { A = 0.7f });
        column.AddChild(note);

        AddChild(veil);
        _menu = veil;
    }

    private Button MenuButton(string text, string choice, bool primary = false)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        var rest = Paper.CardStyle(bg: primary ? Palette.Step with { A = 0.55f } : Paper.CardSpine, borderWidth: 2f, radius: 8f);
        button.AddThemeStyleboxOverride("normal", WithMargins(rest, 16, 7));
        button.AddThemeStyleboxOverride("hover", WithMargins(Paper.CardStyle(bg: Paper.CardShade, border: Paper.Sienna, borderWidth: 2f, radius: 8f), 16, 7));
        button.AddThemeStyleboxOverride("pressed", WithMargins(Paper.CardStyle(bg: Paper.CardShade, borderWidth: 2f, radius: 8f), 16, 7));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeFontSizeOverride("font_size", 16);
        button.Pressed += () => EmitSignal(SignalName.MenuChosen, choice);
        return button;
    }

    /// <summary>A short line under the party bar that says something was done, then goes.</summary>
    public void Toast(string text)
    {
        if (IsInstanceValid(_toast))
        {
            _toast!.QueueFree();
        }

        var toast = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = Grain,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -150f,
            OffsetRight = 150f,
            OffsetTop = 190f,
            GrowHorizontal = Control.GrowDirection.Both,
        };
        toast.AddThemeStyleboxOverride("panel", WithMargins(Paper.CardStyle(border: Palette.Support, borderWidth: 2f, radius: 8f), 14, 6));
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", Ink);
        toast.AddChild(label);
        AddChild(toast);
        _toast = toast;

        var tween = toast.CreateTween();
        tween.TweenInterval(1.8f);
        tween.TweenProperty(toast, "modulate:a", 0f, 0.5f);
        tween.TweenCallback(Callable.From(toast.QueueFree));
    }
}
