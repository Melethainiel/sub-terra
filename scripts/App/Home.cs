using Godot;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// The accueil: the temple's mouth painted behind three doors — Solo, Coopératif,
/// Réglages. Nothing here decides anything about a game; it only sends the player
/// on to whichever screen answers that door.
/// </summary>
public partial class Home : Control
{
    private static readonly Color LinkColor = Color.Color8(216, 198, 171);
    private static readonly Color LinkGold = Color.Color8(247, 192, 69);

    private readonly List<(Button Button, Label Mark)> _links = [];

    public override void _Ready()
    {
        Settings.Apply();
        Audio.Instance?.Menu();

        var art = new TitleArt();
        art.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(art);

        var stage = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        stage.SetAnchorsPreset(LayoutPreset.FullRect);
        stage.AddThemeConstantOverride("separation", 56);
        AddChild(stage);

        stage.AddChild(BuildBrand());
        stage.AddChild(BuildMenu());

        AddChild(BuildFooter());
    }

    private static Control BuildBrand()
    {
        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        box.AddThemeConstantOverride("separation", 4);

        var title = new Label
        {
            Text = "SUB TERRA II",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeColorOverride("font_color", Color.Color8(246, 234, 210));
        title.AddThemeColorOverride("font_outline_color", Palette.LavaGlow with { A = 0.55f });
        title.AddThemeConstantOverride("outline_size", 10);
        title.AddThemeFontSizeOverride("font_size", 52);

        var subtitle = new Label
        {
            Text = "Au bord de l'enfer",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeColorOverride("font_color", Color.Color8(227, 180, 131));
        subtitle.AddThemeFontSizeOverride("font_size", 20);

        box.AddChild(title);
        box.AddChild(subtitle);
        return box;
    }

    private Control BuildMenu()
    {
        var menu = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        menu.AddThemeConstantOverride("separation", 20);

        menu.AddChild(BuildLink("Solo", () =>
        {
            Session.RequestedMode = Session.LobbyMode.Solo;
            GetTree().ChangeSceneToFile("res://scenes/app/Lobby.tscn");
        }));

        menu.AddChild(BuildLink("Coopératif", () =>
        {
            Session.RequestedMode = Session.LobbyMode.Coop;
            GetTree().ChangeSceneToFile("res://scenes/app/Lobby.tscn");
        }));

        menu.AddChild(BuildLink("Réglages", () =>
            GetTree().ChangeSceneToFile("res://scenes/app/SettingsScreen.tscn")));

        return menu;
    }

    /// <summary>One door: a bare line of text that lights up gold and grows a
    /// diamond mark to its left on hover, so the whole menu stays free of any card
    /// or border the painted scene behind it would only have to fight for room.</summary>
    private Control BuildLink(string label, Action onChosen)
    {
        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 8);

        var mark = new Label { Text = "◆", Modulate = Colors.White with { A = 0f } };
        mark.AddThemeColorOverride("font_color", LinkGold);
        mark.AddThemeFontSizeOverride("font_size", 16);

        var button = new Button
        {
            Text = label,
            Flat = true,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", Underline());
        button.AddThemeStyleboxOverride("pressed", Underline());
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeFontSizeOverride("font_size", 30);

        foreach (var state in new[] { "font_color", "font_pressed_color" })
        {
            button.AddThemeColorOverride(state, LinkColor);
        }

        button.AddThemeColorOverride("font_hover_color", LinkGold);

        button.MouseEntered += () => mark.Modulate = Colors.White;
        button.MouseExited += () => mark.Modulate = Colors.White with { A = 0f };
        button.Pressed += onChosen;

        row.AddChild(mark);
        row.AddChild(button);
        _links.Add((button, mark));
        return row;
    }

    private static StyleBoxFlat Underline() => new()
    {
        BgColor = Colors.Transparent,
        BorderColor = LinkGold,
        BorderWidthBottom = 2,
        ContentMarginLeft = 4,
        ContentMarginRight = 4,
        ContentMarginTop = 2,
        ContentMarginBottom = 6,
    };

    private Control BuildFooter()
    {
        var bar = new MarginContainer();
        bar.SetAnchorsPreset(LayoutPreset.BottomWide);
        bar.AddThemeConstantOverride("margin_left", 40);
        bar.AddThemeConstantOverride("margin_right", 40);
        bar.AddThemeConstantOverride("margin_bottom", 26);

        var row = new HBoxContainer();

        var quit = new Button
        {
            Text = "Quitter",
            Flat = true,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        quit.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        quit.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        quit.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        quit.AddThemeFontSizeOverride("font_size", 13);
        quit.AddThemeColorOverride("font_color", LinkColor with { A = 0.75f });
        quit.AddThemeColorOverride("font_hover_color", LinkGold);
        quit.Pressed += () => GetTree().Quit();

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };

        var colophon = new Label
        {
            Text = "Sub Terra II — Au bord de l'enfer · édition numérique",
            VerticalAlignment = VerticalAlignment.Center,
        };
        colophon.AddThemeFontSizeOverride("font_size", 12);
        colophon.AddThemeColorOverride("font_color", LinkColor with { A = 0.55f });

        row.AddChild(quit);
        row.AddChild(spacer);
        row.AddChild(colophon);
        bar.AddChild(row);
        return bar;
    }
}
