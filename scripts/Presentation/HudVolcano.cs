using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// The Volcano board, as a plaque under the tracker: how far the eruption marker has
/// come, what state the mountain is in, and whether the curse is on the party.
/// </summary>
public partial class Hud
{
    private const float VolcanoGaugeWidth = 250f;

    private ColorRect _volcanoFill = null!;
    private Label _volcanoState = null!;
    private Label _curse = null!;

    /// <summary>Where the marker started this game — the fullest the gauge ever gets.</summary>
    private int _eruptionStart;

    private Control BuildVolcano()
    {
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Material = Grain };
        card.AddThemeStyleboxOverride("panel", WithMargins(Paper.CardStyle(bg: Parchment, borderWidth: 3f, radius: 10f), 12, 8));

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 5);
        card.AddChild(column);

        var title = new Label { Text = "VOLCAN", MouseFilter = Control.MouseFilterEnum.Ignore };
        title.AddThemeFontSizeOverride("font_size", 11);
        title.AddThemeColorOverride("font_color", Ink);
        column.AddChild(title);

        var gauge = new ColorRect
        {
            CustomMinimumSize = new Vector2(VolcanoGaugeWidth, 12f),
            Color = ParchmentShade,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _volcanoFill = new ColorRect { Color = Palette.Combat, MouseFilter = Control.MouseFilterEnum.Ignore };
        _volcanoFill.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        gauge.AddChild(_volcanoFill);
        column.AddChild(gauge);

        _volcanoState = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _volcanoState.AddThemeFontSizeOverride("font_size", 12);
        _volcanoState.AddThemeColorOverride("font_color", Ink);
        column.AddChild(_volcanoState);

        _curse = new Label
        {
            Text = "Maudits : deux dés de Péril par tour, l'Éruption avance double.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(VolcanoGaugeWidth, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _curse.AddThemeFontSizeOverride("font_size", 11);
        _curse.AddThemeColorOverride("font_color", Paper.Sienna);
        column.AddChild(_curse);

        return card;
    }

    private void ShowVolcano(GameState game)
    {
        _eruptionStart = Math.Max(_eruptionStart, game.EruptionCountdown);
        var burnt = _eruptionStart == 0 ? 1f : 1f - (float)game.EruptionCountdown / _eruptionStart;

        _volcanoFill.AnchorRight = game.HasErupted ? 1f : burnt;
        _volcanoFill.OffsetRight = 0f;
        _volcanoFill.Color = game.HasErupted || game.IsVolcanoReady ? Palette.Combat : Palette.Rubble.Lerp(Palette.Combat, burnt);

        _volcanoState.Text = game switch
        {
            { HasErupted: true } => "EN ÉRUPTION — la lave gagne le Temple.",
            { IsVolcanoReady: true } => "Prêt à exploser : la prochaine Lave le réveille.",
            _ => $"Éruption dans {game.EruptionCountdown} cases.",
        };

        _curse.Visible = game.IsCursed;
    }
}
