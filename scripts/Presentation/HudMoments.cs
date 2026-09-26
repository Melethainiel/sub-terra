using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// The moments the HUD stops to show rather than lists: a die landing on the table,
/// and the end of the expedition. Both are cued by the <see cref="Choreographer"/>
/// at the point of the playback where they happen.
/// </summary>
public partial class Hud
{
    [Signal]
    public delegate void EndChosenEventHandler(bool again);

    private const float DieShown = 1.6f;

    private PanelContainer? _die;
    private Tween? _dieTween;
    private Control? _ending;

    /// <summary>A moment reached in the playback of a command.</summary>
    public void Cue(GameEvent @event)
    {
        switch (@event)
        {
            case PerilRolled peril:
                ShowDie(Glyph(peril.Face), peril.Face.Name(), Faces(), Palette.Combat);
                break;

            case DieRolled die:
                ShowDie(Pips(die.Face), $"Le dé : {die.Face}", [.. Enumerable.Range(1, 6).Select(Pips)], Palette.Meta);
                break;

            case GameEnded ended:
                ShowEnding(ended.Outcome);
                break;
        }
    }

    private static string[] Faces() => [.. Enum.GetValues<PerilFace>().Select(Glyph)];

    private static string Glyph(PerilFace face) => face switch
    {
        PerilFace.Stumble => "↯",
        PerilFace.Lava => "♨",
        PerilFace.Collapse => "▼",
        PerilFace.Trap => "✱",
        PerilFace.WakeGuardian => "☗",
        PerilFace.ActivateGuardians => "»",
        _ => "?",
    };

    private static string Pips(int face) => face switch
    {
        >= 1 and <= 6 => ((char)('⚀' + face - 1)).ToString(),
        _ => face.ToString(),
    };

    /// <summary>
    /// A die card drops in under the party bar, tumbles through a few faces, settles
    /// on the one rolled with its name underneath, and goes once it has been read.
    /// </summary>
    private void ShowDie(string face, string name, string[] tumble, Color accent)
    {
        _dieTween?.Kill();
        _die?.QueueFree();

        _die = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = Grain,
        };
        _die.AddThemeStyleboxOverride("panel", Paper.CardStyle(border: accent, borderWidth: 3f, radius: 12f) is var style
            ? WithMargins(style, 18, 10)
            : null);

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        var glyph = new Label { Text = tumble[0], HorizontalAlignment = HorizontalAlignment.Center };
        glyph.AddThemeFontSizeOverride("font_size", 54);
        glyph.AddThemeColorOverride("font_color", Ink);
        var caption = new Label { Text = " ", HorizontalAlignment = HorizontalAlignment.Center };
        caption.AddThemeFontSizeOverride("font_size", 15);
        caption.AddThemeColorOverride("font_color", Ink);
        column.AddChild(glyph);
        column.AddChild(caption);
        _die.AddChild(column);

        // Centred under the party bar: anchored on the middle of the top edge, with
        // offsets rather than a position, which Godot would read from the corner.
        var holder = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -110f,
            OffsetRight = 110f,
            OffsetTop = 186f,
            OffsetBottom = 306f,
        };
        holder.AddChild(_die);
        AddChild(holder);
        _die.TreeExiting += holder.QueueFree;

        _die.Modulate = Colors.Transparent;
        _dieTween = _die.CreateTween();
        _dieTween.TweenProperty(_die, "modulate", Colors.White, 0.12f);

        // A few faces flicker past before it settles — a roll, not an announcement.
        for (var i = 1; i <= 5; i++)
        {
            var shown = tumble[(i * 7 + tumble.Length) % tumble.Length];
            _dieTween.TweenCallback(Callable.From(() => glyph.Text = shown));
            _dieTween.TweenInterval(0.06f);
        }

        _dieTween.TweenCallback(Callable.From(() =>
        {
            glyph.Text = face;
            caption.Text = name;
        }));
        _dieTween.TweenInterval(DieShown);
        _dieTween.TweenProperty(_die, "modulate:a", 0f, 0.35f);
        _dieTween.TweenCallback(Callable.From(() => _die?.QueueFree()));
    }

    private static StyleBoxFlat WithMargins(StyleBoxFlat style, int horizontal, int vertical)
    {
        style.ContentMarginLeft = horizontal;
        style.ContentMarginRight = horizontal;
        style.ContentMarginTop = vertical;
        style.ContentMarginBottom = vertical;
        return style;
    }

    /// <summary>How the rulebook ranks an ending, and what it means at the table.</summary>
    private static (string Title, string Line, bool Won) Verdict(Outcome outcome) => outcome switch
    {
        Outcome.Legendary => ("Victoire légendaire", "L'Artefact est sorti, et tout le monde avec lui.", true),
        Outcome.Gold => ("Victoire d'or", "L'Artefact est sorti ; un Explorateur est resté dans la montagne.", true),
        Outcome.Silver => ("Victoire d'argent", "L'Artefact est sorti ; deux Explorateurs sont restés dans la montagne.", true),
        Outcome.Bronze => ("Victoire de bronze", "L'Artefact est sorti ; trois Explorateurs sont restés dans la montagne.", true),
        _ => ("Oublié à jamais", "L'Artefact n'a jamais quitté la montagne.", false),
    };

    /// <summary>The expedition is over: the verdict on a card over a dimmed table, and
    /// the two ways on from here.</summary>
    private void ShowEnding(Outcome outcome)
    {
        _ending?.QueueFree();

        var (title, line, won) = Verdict(outcome);

        var veil = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        veil.AddChild(centre);

        var card = new PanelContainer { Material = Grain };
        card.AddThemeStyleboxOverride("panel", WithMargins(
            Paper.CardStyle(border: won ? Palette.Key : Palette.Combat, borderWidth: 4f, radius: 16f), 40, 28));
        centre.AddChild(card);

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 14);
        card.AddChild(column);

        var heading = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 34);
        heading.AddThemeColorOverride("font_color", won ? Paper.Sienna : Ink);
        column.AddChild(heading);

        var sub = new Label { Text = line, HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 16);
        sub.AddThemeColorOverride("font_color", Ink);
        column.AddChild(sub);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        buttons.AddChild(EndButton("Nouvelle expédition", again: true));
        buttons.AddChild(EndButton("Accueil", again: false));
        column.AddChild(buttons);

        AddChild(veil);
        _ending = veil;

        card.Modulate = Colors.Transparent;
        var tween = veil.CreateTween().SetParallel();
        tween.TweenProperty(veil, "color:a", 0.6f, 0.6f);
        tween.TweenProperty(card, "modulate", Colors.White, 0.6f).SetDelay(0.2f);
    }

    private Button EndButton(string text, bool again)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Ink);
        button.AddThemeStyleboxOverride("normal", WithMargins(Paper.CardStyle(bg: Paper.CardSpine, borderWidth: 2f, radius: 8f), 16, 8));
        button.AddThemeStyleboxOverride("hover", WithMargins(Paper.CardStyle(bg: again ? Palette.Step : Paper.CardShade, borderWidth: 2f, radius: 8f), 16, 8));
        button.AddThemeStyleboxOverride("pressed", WithMargins(Paper.CardStyle(bg: Paper.CardShade, borderWidth: 2f, radius: 8f), 16, 8));
        button.Pressed += () => EmitSignal(SignalName.EndChosen, again);
        return button;
    }
}
