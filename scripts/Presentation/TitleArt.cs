using Godot;

namespace SubTerra.Presentation;

/// <summary>
/// The accueil's own painted backdrop — a temple mouth lit by its lava, drawn from
/// flat vector shapes rather than a picture the project doesn't have: a sky, two
/// ridges, a crack of lava running down the nearer one, a glow off the floor, and a
/// few embers drifting above it. The one screen allowed to look like the cave itself
/// instead of paper on a table, since nothing here is meant to be read, only felt.
/// </summary>
public partial class TitleArt : Control
{
    private const float Width = 1600f;
    private const float Height = 900f;

    private static readonly Color SkyTop = Color.Color8(18, 11, 9);
    private static readonly Color SkyBottom = Color.Color8(96, 42, 19);
    private static readonly Color RidgeBack = Color.Color8(35, 20, 14, 235);
    private static readonly Color RidgeMid = Color.Color8(20, 12, 8);
    private static readonly Color VeinTop = Color.Color8(247, 192, 69);
    private static readonly Color VeinBottom = Color.Color8(122, 36, 16);
    private static readonly Color Glow = Palette.LavaGlow;

    /// <summary>Each ember: where it drifts from, how far it bobs, and how far
    /// through that bob it already is — a phase apiece so six dots never move in
    /// lockstep.</summary>
    private readonly (Vector2 At, float Radius, float Phase)[] _embers =
    [
        (new Vector2(340, 760), 2.4f, 0.0f),
        (new Vector2(560, 800), 2.0f, 1.1f),
        (new Vector2(980, 780), 2.2f, 2.4f),
        (new Vector2(1180, 810), 2.6f, 0.6f),
        (new Vector2(800, 840), 2.0f, 3.1f),
        (new Vector2(1320, 750), 1.8f, 1.9f),
    ];

    private float _time;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Material = Paper.Grain;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawPolygon(
            [new Vector2(0, 0), new Vector2(Width, 0), new Vector2(Width, Height), new Vector2(0, Height)],
            [SkyTop, SkyTop, SkyBottom, SkyBottom]);

        DrawColoredPolygon(
        [
            new Vector2(0, 900), new Vector2(0, 560), new Vector2(140, 470), new Vector2(280, 540),
            new Vector2(430, 400), new Vector2(580, 510), new Vector2(720, 380), new Vector2(860, 500),
            new Vector2(1000, 410), new Vector2(1140, 520), new Vector2(1280, 440), new Vector2(1420, 540),
            new Vector2(1600, 470), new Vector2(1600, 900),
        ], RidgeBack);

        DrawColoredPolygon(
        [
            new Vector2(0, 900), new Vector2(0, 630), new Vector2(220, 460), new Vector2(370, 560),
            new Vector2(560, 300), new Vector2(800, 120), new Vector2(1040, 300), new Vector2(1280, 540),
            new Vector2(1460, 420), new Vector2(1600, 600), new Vector2(1600, 900),
        ], RidgeMid);

        DrawPolygon(
            [new Vector2(800, 550), new Vector2(760, 900), new Vector2(840, 900)],
            [VeinTop, VeinBottom, VeinBottom]);

        // The glow off the floor: a stack of soft, low-alpha discs standing in for a
        // radial gradient Godot's flat 2D drawing has no single call for.
        foreach (var (radiusX, alpha) in new (float, float)[] { (420, 0.05f), (300, 0.07f), (190, 0.09f), (110, 0.11f) })
        {
            DrawColoredPolygon(Ellipse(new Vector2(800, 900), radiusX, radiusX * 0.32f), Glow with { A = alpha });
        }

        foreach (var (at, radius, phase) in _embers)
        {
            var bob = Mathf.Sin(_time * 0.6f + phase) * 12f;
            var flicker = 0.55f + 0.35f * Mathf.Sin(_time * 1.7f + phase * 2f);
            DrawCircle(at + new Vector2(0, bob), radius, VeinTop with { A = flicker });
        }
    }

    private static Vector2[] Ellipse(Vector2 centre, float radiusX, float radiusY, int segments = 28)
    {
        var points = new Vector2[segments];

        for (var i = 0; i < segments; i++)
        {
            var angle = Mathf.Tau * i / segments;
            points[i] = centre + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }
}
