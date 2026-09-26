using Godot;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Réglages: only what already does something in this build. Plein écran and the
/// vue par défaut are real and saved; the commands card underneath is a straight
/// reprint of <see cref="ActionCards"/>, the same data the HUD's own Actions card
/// draws from, plus the two keys no card carries — <c>V</c> and Échap.
/// </summary>
public partial class SettingsScreen : Control
{
    /// <summary>The two bindings <see cref="ActionCards"/> doesn't carry, because
    /// they're not action-card presses — a view toggle and a cancel.</summary>
    private static readonly (string Key, string Label)[] ExtraKeys =
    [
        ("1 / 2", "Capacités de l'Explorateur dont c'est le tour"),
        ("V", "Basculer vue FPS ↔ vue du dessus"),
        ("F1", "Aide : le but, un tour, les actions, le dé de Péril"),
        ("Échap", "Annuler / revenir à la vue du dessus"),
    ];

    private Control _viewRow = null!;

    public override void _Ready()
    {
        var background = new ColorRect { Color = Paper.Field, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 44);
        margin.AddThemeConstantOverride("margin_right", 44);
        margin.AddThemeConstantOverride("margin_top", 36);
        margin.AddThemeConstantOverride("margin_bottom", 36);
        AddChild(margin);

        var stage = new VBoxContainer();
        stage.AddThemeConstantOverride("separation", 18);
        margin.AddChild(stage);

        stage.AddChild(BuildHeader());

        var sections = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        sections.AddThemeConstantOverride("separation", 24);
        stage.AddChild(sections);

        sections.AddChild(BuildDisplaySection());
        sections.AddChild(BuildCommandsSection());
    }

    private Control BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var back = new Button
        {
            Text = "‹ Accueil",
            Flat = true,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        back.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        back.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        back.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        back.AddThemeFontSizeOverride("font_size", 14);
        back.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.62f });
        back.AddThemeColorOverride("font_hover_color", Paper.Sienna);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/app/Home.tscn");

        var title = new Label { Text = "Réglages" };
        title.AddThemeColorOverride("font_color", Paper.Ink);
        title.AddThemeFontSizeOverride("font_size", 30);

        row.AddChild(back);
        row.AddChild(title);
        return row;
    }

    private static (PanelContainer Panel, VBoxContainer Column) BuildCard(string heading, Color accent)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Paper.CardStyle());
        panel.Material = Paper.Grain;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        margin.AddChild(col);

        var title = new Label { Text = heading };
        title.AddThemeColorOverride("font_color", Paper.Ink);
        title.AddThemeFontSizeOverride("font_size", 20);
        col.AddChild(title);

        var rule = new ColorRect { Color = accent, CustomMinimumSize = new Vector2(0, 3) };
        col.AddChild(rule);

        return (panel, col);
    }

    private Control BuildDisplaySection()
    {
        var (panel, col) = BuildCard("Affichage", Palette.Unknown);

        var fullscreenRow = new HBoxContainer();
        fullscreenRow.AddThemeConstantOverride("separation", 10);

        fullscreenRow.AddChild(Paper.Checkbox(Settings.Fullscreen, on => Settings.Fullscreen = on));

        var fullscreenLabel = new Label { Text = "Plein écran" };
        fullscreenLabel.AddThemeColorOverride("font_color", Paper.Ink);
        fullscreenLabel.AddThemeFontSizeOverride("font_size", 13);
        fullscreenRow.AddChild(fullscreenLabel);

        col.AddChild(fullscreenRow);

        var viewLabel = new Label { Text = "Vue par défaut" };
        viewLabel.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.7f });
        viewLabel.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(viewLabel);

        _viewRow = new MarginContainer();
        col.AddChild(_viewRow);
        RebuildViewRow();

        var note = new Label
        {
            Text = "V bascule entre les deux en cours de partie.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        note.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.45f });
        note.AddThemeFontSizeOverride("font_size", 11);
        col.AddChild(note);

        return panel;
    }

    private void RebuildViewRow()
    {
        foreach (var child in _viewRow.GetChildren())
        {
            child.QueueFree();
        }

        var selected = Settings.DefaultViewIsFps ? 1 : 0;

        _viewRow.AddChild(Paper.Segmented(["Vue du dessus", "Vue FPS"], selected, i =>
        {
            Settings.DefaultViewIsFps = i == 1;
            RebuildViewRow();
        }));
    }

    private static Control BuildCommandsSection()
    {
        var (panel, col) = BuildCard("Commandes", Palette.Meta);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 22);
        grid.AddThemeConstantOverride("v_separation", 6);

        foreach (var card in ActionCards.All)
        {
            grid.AddChild(BuildKeyRow(card.Key, card.Name));
        }

        foreach (var (key, label) in ExtraKeys)
        {
            grid.AddChild(BuildKeyRow(key, label));
        }

        col.AddChild(grid);
        return panel;
    }

    private static Control BuildKeyRow(string key, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var badge = new PanelContainer { CustomMinimumSize = new Vector2(0, 22) };
        badge.AddThemeStyleboxOverride("panel", Paper.CardStyle(bg: Paper.CardSpine, borderWidth: 2f, radius: 6f));
        var badgeLabel = new Label
        {
            Text = key,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(30, 0),
        };
        badgeLabel.AddThemeColorOverride("font_color", Paper.Ink);
        badgeLabel.AddThemeFontSizeOverride("font_size", 12);
        badge.AddChild(badgeLabel);

        var text = new Label { Text = label };
        text.AddThemeColorOverride("font_color", Paper.Ink);
        text.AddThemeFontSizeOverride("font_size", 13);

        row.AddChild(badge);
        row.AddChild(text);
        return row;
    }
}
