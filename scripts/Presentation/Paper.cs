using Godot;

namespace SubTerra.Presentation;

/// <summary>
/// The parchment the menus are drawn on — the same paper the in-game HUD prints its
/// Actions card on (see <see cref="Hud"/>'s own parchment palette), but this is the
/// screens a player never sees the cave behind: the accueil, the lobby, Réglages.
/// Kept apart from <see cref="Hud"/>'s private colours rather than shared outright,
/// since a change here should never risk the board itself.
/// </summary>
public static class Paper
{
    /// <summary>The field a whole screen sits on — lighter than a card, so a card
    /// still lifts off it.</summary>
    public static readonly Color Field = Color.Color8(233, 223, 200);

    public static readonly Color Card = Color.Color8(222, 205, 172);

    public static readonly Color CardShade = Color.Color8(203, 184, 149);

    public static readonly Color CardSpine = Color.Color8(236, 224, 198);

    public static readonly Color BorderLine = Color.Color8(34, 25, 18);

    public static readonly Color Ink = Color.Color8(40, 29, 20);

    /// <summary>A warm, low-key accent for a subtitle or a hint — sienna ink rather
    /// than the torch orange the dark screens use, which reads as glare on paper
    /// this light.</summary>
    public static readonly Color Sienna = Color.Color8(138, 58, 28);

    /// <summary>The one grain shader every parchment surface in the game shares —
    /// same resource <see cref="Hud"/> loads, so a card here and a card in play are
    /// unmistakably the same material.</summary>
    public static readonly ShaderMaterial Grain = new()
    {
        Shader = GD.Load<Shader>("res://resources/shaders/paper_grain.gdshader"),
    };

    /// <summary>A bordered sheet of parchment — the one shape every card, panel and
    /// button on these screens is cut from.</summary>
    public static StyleBoxFlat CardStyle(
        Color? bg = null, Color? border = null, float borderWidth = 3f, float radius = 14f)
    {
        var width = (int)borderWidth;
        var corner = (int)radius;

        return new StyleBoxFlat
        {
            BgColor = bg ?? Card,
            BorderColor = border ?? BorderLine,
            BorderWidthLeft = width,
            BorderWidthTop = width,
            BorderWidthRight = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = corner,
            CornerRadiusTopRight = corner,
            CornerRadiusBottomLeft = corner,
            CornerRadiusBottomRight = corner,
        };
    }

    /// <summary>A flat tint with no border — a segmented control's active pill, a
    /// hover under a menu link, that sort of thing.</summary>
    public static StyleBoxFlat Fill(Color tint, float radius = 0f)
    {
        var corner = (int)radius;

        return new StyleBoxFlat
        {
            BgColor = tint,
            CornerRadiusTopLeft = corner,
            CornerRadiusTopRight = corner,
            CornerRadiusBottomLeft = corner,
            CornerRadiusBottomRight = corner,
        };
    }

    /// <summary>The thin rule between two panes of the same card.</summary>
    public static ColorRect Divider(bool vertical = true) => new()
    {
        Color = BorderLine with { A = 0.35f },
        CustomMinimumSize = vertical ? new Vector2(1.5f, 0f) : new Vector2(0f, 1.5f),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    /// <summary>
    /// A row of mutually exclusive pills sharing one border, the way the Actions
    /// card's own columns share theirs — used for a difficulty, a view mode, a
    /// host/join choice. <paramref name="selected"/> names which option is active;
    /// <paramref name="onPick"/> is told which one was clicked.
    /// </summary>
    public static Control Segmented(IReadOnlyList<string> options, int selected, Action<int> onPick)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 0);

        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", CardStyle(bg: CardShade, borderWidth: 2f, radius: 8f));

        for (var i = 0; i < options.Count; i++)
        {
            if (i > 0)
            {
                row.AddChild(Divider());
            }

            var index = i;
            var active = i == selected;

            // Not Flat: a flat button draws no background at all, and the chosen pill's
            // dark fill vanished, leaving light text on light paper.
            var pill = new Button
            {
                Text = options[i],
                FocusMode = Control.FocusModeEnum.None,
                ToggleMode = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            };

            pill.AddThemeStyleboxOverride("normal", active ? Fill(Ink) : new StyleBoxEmpty());
            pill.AddThemeStyleboxOverride("hover", active ? Fill(Ink) : Fill(BorderLine with { A = 0.12f }));
            pill.AddThemeStyleboxOverride("pressed", Fill(Ink));
            pill.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            {
                pill.AddThemeColorOverride(state, active ? CardSpine : Ink);
            }

            pill.AddThemeFontSizeOverride("font_size", 13);
            pill.Pressed += () => onPick(index);

            row.AddChild(pill);
        }

        frame.AddChild(row);
        return frame;
    }

    /// <summary>A slider on paper: a thin rule, filled in sienna up to the grabber.</summary>
    public static void Slider(HSlider slider)
    {
        StyleBoxFlat Rail(Color colour) => new()
        {
            BgColor = colour,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
        };

        slider.AddThemeStyleboxOverride("slider", Rail(CardShade));
        slider.AddThemeStyleboxOverride("grabber_area", Rail(Sienna));
        slider.AddThemeStyleboxOverride("grabber_area_highlight", Rail(Sienna.Lightened(0.15f)));
    }

    /// <summary>A text field on paper: ink on a lighter card, a darker rule when focused —
    /// not Godot's default dark field, which reads as a hole in the page.</summary>
    public static void TextField(LineEdit edit)
    {
        StyleBoxFlat Box(Color border, float width)
        {
            var box = CardStyle(bg: CardSpine, border: border, borderWidth: width, radius: 6f);
            box.ContentMarginLeft = 8;
            box.ContentMarginRight = 8;
            box.ContentMarginTop = 4;
            box.ContentMarginBottom = 4;
            return box;
        }

        edit.AddThemeStyleboxOverride("normal", Box(BorderLine with { A = 0.5f }, 1f));
        edit.AddThemeStyleboxOverride("focus", Box(Sienna, 2f));
        edit.AddThemeStyleboxOverride("read_only", Box(BorderLine with { A = 0.25f }, 1f));
        edit.AddThemeColorOverride("font_color", Ink);
        edit.AddThemeColorOverride("font_readonly_color", Ink with { A = 0.55f });
        edit.AddThemeColorOverride("font_placeholder_color", Ink with { A = 0.4f });
        edit.AddThemeColorOverride("caret_color", Ink);
        edit.AddThemeColorOverride("selection_color", Sienna with { A = 0.3f });
        edit.AddThemeFontSizeOverride("font_size", 13);
    }

    /// <summary>
    /// A flat meeple silhouette — a capsule body and a round head, the same shape
    /// <c>TokenView</c> already stands each Explorer's token in on the board itself.
    /// Stands in for the portrait or 3D model the project doesn't have yet, so a
    /// character carousel has something other than a blank card to show.
    /// </summary>
    public static Control Meeple(Color tint, float width = 70f, float height = 150f)
    {
        var box = new Control
        {
            CustomMinimumSize = new Vector2(width, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var headSize = width * 0.62f;
        var head = new Panel
        {
            Position = new Vector2((width - headSize) / 2f, 0f),
            Size = new Vector2(headSize, headSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        head.AddThemeStyleboxOverride("panel", Fill(tint, headSize / 2f));

        var bodyTop = headSize * 0.62f;
        var bodyWidth = width * 0.86f;
        var body = new Panel
        {
            Position = new Vector2((width - bodyWidth) / 2f, bodyTop),
            Size = new Vector2(bodyWidth, height - bodyTop),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        body.AddThemeStyleboxOverride("panel", Fill(tint, bodyWidth / 2f));

        box.AddChild(body);
        box.AddChild(head);
        return box;
    }

    /// <summary>A printed checkbox — a square of paper with a border, and a tick
    /// stamped on it once it's on. The tabletop's own idea of a toggle switch.</summary>
    public static Control Checkbox(bool value, Action<bool> onToggle)
    {
        // Not Flat, like the segmented pills: a flat button draws no box to tick.
        var box = new Button
        {
            Text = value ? "✓" : "",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(24, 24),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };

        box.AddThemeStyleboxOverride("normal", CardStyle(bg: Field, borderWidth: 2f, radius: 4f));
        box.AddThemeStyleboxOverride("hover", CardStyle(bg: Field, border: Sienna, borderWidth: 2f, radius: 4f));
        box.AddThemeStyleboxOverride("pressed", CardStyle(bg: Field, borderWidth: 2f, radius: 4f));
        box.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            box.AddThemeColorOverride(state, Ink);
        }

        box.AddThemeFontSizeOverride("font_size", 15);

        // The tick is the box's own state, not the caller's — flips itself on
        // click and only then tells the caller what it settled on, so nothing
        // upstream has to tear the checkbox down just to redraw its glyph.
        box.Pressed += () =>
        {
            var next = box.Text != "✓";
            box.Text = next ? "✓" : "";
            onToggle(next);
        };

        return box;
    }
}
