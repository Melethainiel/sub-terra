using Godot;

namespace SubTerra.Presentation;

/// <summary>
/// The pieces on the table, as the art direction has them (docs/art/direction-artistique.md):
/// painted miniatures on round bases, with a thin ink line. A figure's parts carry
/// materials named for what they are — Skin, Coat, Metal… — and are painted here by
/// that name, so a model never brings its own look into the game.
/// </summary>
public static class Miniature
{
    /// <summary>
    /// One paint per part. <c>Bright</c> is the cartoon candidate's colour, <c>Painted</c>
    /// the miniature's; the chosen look sits between the two (<see cref="Blend"/>).
    /// </summary>
    public readonly record struct Paint(Color Bright, Color Painted, float Metal, float Rough, float Glow)
    {
        public Color Chosen => Painted.Lerp(Bright, Blend);
    }

    /// <summary>How far the painted colours lean towards the bright ones: enough for a
    /// figure to stand out of the rock seen from above, not so far it turns cartoon.</summary>
    public const float Blend = 0.45f;

    /// <summary>Thickness of the ink line, in metres — half the cartoon candidate's.</summary>
    public const float InkThickness = 0.016f;

    public const float ExplorerBase = 0.24f;

    public const float GuardianBase = 0.32f;

    private const float BaseHeight = 0.06f;

    public static readonly IReadOnlyDictionary<string, Paint> Paints = new Dictionary<string, Paint>
    {
        ["Gold"] = new(new Color(1f, 0.8f, 0.32f), new Color(0.85f, 0.62f, 0.22f), 0.9f, 0.3f, 0f),
        ["Hair"] = new(new Color(0.42f, 0.24f, 0.12f), new Color(0.22f, 0.14f, 0.09f), 0f, 0.8f, 0f),
        ["Skin"] = new(new Color(0.96f, 0.72f, 0.55f), new Color(0.82f, 0.6f, 0.48f), 0f, 0.7f, 0f),
        ["Cloth"] = new(new Color(0.25f, 0.42f, 0.66f), new Color(0.2f, 0.26f, 0.34f), 0f, 0.9f, 0f),
        ["Coat"] = new(new Color(0.9f, 0.64f, 0.2f), new Color(0.52f, 0.45f, 0.29f), 0f, 0.85f, 0f),
        ["Leather"] = new(new Color(0.52f, 0.28f, 0.14f), new Color(0.34f, 0.2f, 0.12f), 0f, 0.6f, 0f),
        ["Accent"] = new(new Color(0.88f, 0.18f, 0.14f), new Color(0.55f, 0.1f, 0.08f), 0f, 0.6f, 0f),
        ["Metal"] = new(new Color(0.82f, 0.8f, 0.72f), new Color(0.78f, 0.76f, 0.74f), 0.9f, 0.3f, 0f),
        ["Wood"] = new(new Color(0.5f, 0.32f, 0.18f), new Color(0.36f, 0.24f, 0.15f), 0f, 0.8f, 0f),
        ["Flame"] = new(new Color(1f, 0.62f, 0.15f), new Color(1f, 0.55f, 0.15f), 0f, 1f, 3f),
        ["Ash"] = new(new Color(0.3f, 0.26f, 0.32f), new Color(0.16f, 0.15f, 0.16f), 0f, 0.9f, 0f),
        ["Armor"] = new(new Color(0.45f, 0.36f, 0.4f), new Color(0.26f, 0.23f, 0.23f), 0.2f, 0.75f, 0f),
        ["Bone"] = new(new Color(0.94f, 0.88f, 0.7f), new Color(0.78f, 0.74f, 0.63f), 0f, 0.6f, 0f),
        ["Ember"] = new(new Color(1f, 0.38f, 0.08f), new Color(1f, 0.32f, 0.06f), 0f, 1f, 4f),
    };

    public const string Key = "res://resources/models/props/key.glb";

    public const string Artefact = "res://resources/models/props/artefact.glb";

    private static readonly Dictionary<string, PackedScene> Figures = [];

    /// <summary>A piece that stands on no base — the Key, the Artefact — painted like the rest.</summary>
    public static Node3D Piece(string path)
    {
        if (!Figures.TryGetValue(path, out var scene))
        {
            Figures[path] = scene = GD.Load<PackedScene>(path);
        }

        var piece = scene.Instantiate<Node3D>();
        Dress(piece);
        return piece;
    }

    private static readonly Dictionary<(string Part, Color? Livery), Material> Painted = [];

    /// <summary>
    /// A figure standing on its base, painted. <paramref name="livery"/> repaints the
    /// coat in a seat's colour; <paramref name="ring"/> paints the top of the base, as
    /// board games colour their pawns' bases — seen from above, a figure is a few
    /// pixels tall and the base is what says whose it is.
    /// </summary>
    public static Node3D Stand(string figure, float baseRadius, Color? livery = null, Color? ring = null)
    {
        var stand = new Node3D();

        var rim = new MeshInstance3D
        {
            Name = "Base",
            Mesh = new CylinderMesh { TopRadius = baseRadius, BottomRadius = baseRadius + 0.02f, Height = BaseHeight, RadialSegments = 32 },
            MaterialOverride = BaseRim,
            Position = new Vector3(0f, BaseHeight / 2f, 0f),
        };
        rim.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = baseRadius - 0.015f, BottomRadius = baseRadius - 0.015f, Height = 0.01f, RadialSegments = 32 },
            MaterialOverride = ring is { } colour ? RingFor(colour) : BaseTop,
            Position = new Vector3(0f, BaseHeight / 2f + 0.002f, 0f),
        });
        stand.AddChild(rim);

        if (ring is { } seat)
        {
            // Around the base, on the floor: the one mark the figure cannot stand over.
            stand.AddChild(new MeshInstance3D
            {
                Name = "Ring",
                Mesh = new TorusMesh { InnerRadius = baseRadius + 0.05f, OuterRadius = baseRadius + 0.2f, Rings = 32, RingSegments = 6 },
                MaterialOverride = GlowFor(seat),
                Scale = new Vector3(1f, 0.25f, 1f),
                Position = new Vector3(0f, 0.02f, 0f),
            });
        }

        if (!Figures.TryGetValue(figure, out var scene))
        {
            Figures[figure] = scene = GD.Load<PackedScene>(figure);
        }

        var model = scene.Instantiate<Node3D>();
        model.Name = "Figure";
        model.Position = new Vector3(0f, BaseHeight, 0f);
        stand.AddChild(model);
        Dress(model, livery);

        return stand;
    }

    /// <summary>Paints every part of a figure by the name of its material.</summary>
    public static void Dress(Node node, Color? livery = null)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is { } shape)
        {
            for (var surface = 0; surface < shape.GetSurfaceCount(); surface++)
            {
                var part = shape.SurfaceGetMaterial(surface)?.ResourceName ?? "";

                if (Paints.ContainsKey(part))
                {
                    mesh.SetSurfaceOverrideMaterial(surface, PaintFor(part, part == "Coat" ? livery : null));
                }
                else
                {
                    GD.PushWarning($"Pas de peinture pour « {part} » ({mesh.Name}).");
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Dress(child, livery);
        }
    }

    /// <summary>The chosen look for one part: painted, inked unless it glows.</summary>
    public static Material PaintFor(string part, Color? livery = null)
    {
        if (Painted.TryGetValue((part, livery), out var cached))
        {
            return cached;
        }

        var paint = Paints[part];
        var colour = livery is { } seat ? paint.Painted.Lerp(seat, 0.8f) : paint.Chosen;

        var material = new ShaderMaterial { Shader = PaintedMini };
        material.SetShaderParameter("albedo", colour);
        material.SetShaderParameter("metallic", paint.Metal);
        material.SetShaderParameter("roughness", paint.Rough);
        material.SetShaderParameter("emission", colour * paint.Glow);

        if (paint.Glow == 0f)
        {
            var ink = new ShaderMaterial { Shader = InkOutline };
            ink.SetShaderParameter("thickness", InkThickness);
            material.NextPass = ink;
        }

        return Painted[(part, livery)] = material;
    }

    private static readonly Dictionary<Color, Material> Rings = [];

    private static readonly Dictionary<Color, Material> Glows = [];

    /// <summary>The ring on the floor round a base: bright enough to find from above.</summary>
    private static Material GlowFor(Color colour) =>
        Glows.TryGetValue(colour, out var glow)
            ? glow
            : Glows[colour] = new StandardMaterial3D
            {
                AlbedoColor = colour,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = colour,
                EmissionEnergyMultiplier = 2f,
            };

    /// <summary>The top of a base in a player's colour, lit just enough to read in the dark.</summary>
    private static Material RingFor(Color colour) =>
        Rings.TryGetValue(colour, out var ring)
            ? ring
            : Rings[colour] = new StandardMaterial3D
            {
                AlbedoColor = colour,
                Roughness = 0.5f,
                EmissionEnabled = true,
                Emission = colour,
                EmissionEnergyMultiplier = 0.9f,
            };

    private static Shader PaintedMini => GD.Load<Shader>("res://resources/shaders/styles/painted_mini.gdshader");

    private static Shader InkOutline => GD.Load<Shader>("res://resources/shaders/styles/ink_outline.gdshader");

    private static readonly StandardMaterial3D BaseRim = new() { AlbedoColor = new Color(0.06f, 0.05f, 0.05f), Roughness = 0.35f };

    private static readonly StandardMaterial3D BaseTop = new() { AlbedoColor = new Color(0.42f, 0.33f, 0.24f), Roughness = 1f };
}
