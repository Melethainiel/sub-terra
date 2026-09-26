using Godot;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Dev tool for the art direction: stands the two sketch figures on a tile of the
/// temple and dresses them in one candidate style, then shoots and quits. The same
/// models go through every style, so a board of shots compares looks, not models.
/// <code>
/// SUBTERRA_STYLE=toon|figurine SUBTERRA_VIEW=hero|table SUBTERRA_SHOT=/tmp/x.png \
///     godot --path . res://scenes/_style_board.tscn
/// </code>
/// </summary>
public partial class StyleBoard : Node3D
{
    /// <summary>
    /// One paint per part, by the material name the figure script gives it: how the
    /// cartoon candidate colours it, how the miniature candidate paints it, how
    /// metallic and rough that paint is, and how brightly it glows, if it does.
    /// </summary>
    private static readonly Dictionary<string, (Color Toon, Color Mini, float Metal, float Rough, float Glow)> Paints = new()
    {
        ["Skin"] = (new Color(0.96f, 0.72f, 0.55f), new Color(0.82f, 0.6f, 0.48f), 0f, 0.7f, 0f),
        ["Cloth"] = (new Color(0.25f, 0.42f, 0.66f), new Color(0.2f, 0.26f, 0.34f), 0f, 0.9f, 0f),
        ["Coat"] = (new Color(0.9f, 0.64f, 0.2f), new Color(0.52f, 0.45f, 0.29f), 0f, 0.85f, 0f),
        ["Leather"] = (new Color(0.52f, 0.28f, 0.14f), new Color(0.34f, 0.2f, 0.12f), 0f, 0.6f, 0f),
        ["Accent"] = (new Color(0.88f, 0.18f, 0.14f), new Color(0.55f, 0.1f, 0.08f), 0f, 0.6f, 0f),
        ["Metal"] = (new Color(0.82f, 0.8f, 0.72f), new Color(0.78f, 0.76f, 0.74f), 0.9f, 0.3f, 0f),
        ["Wood"] = (new Color(0.5f, 0.32f, 0.18f), new Color(0.36f, 0.24f, 0.15f), 0f, 0.8f, 0f),
        ["Flame"] = (new Color(1f, 0.62f, 0.15f), new Color(1f, 0.55f, 0.15f), 0f, 1f, 3f),
        ["Ash"] = (new Color(0.3f, 0.26f, 0.32f), new Color(0.16f, 0.15f, 0.16f), 0f, 0.9f, 0f),
        ["Armor"] = (new Color(0.45f, 0.36f, 0.4f), new Color(0.26f, 0.23f, 0.23f), 0.2f, 0.75f, 0f),
        ["Bone"] = (new Color(0.94f, 0.88f, 0.7f), new Color(0.78f, 0.74f, 0.63f), 0f, 0.6f, 0f),
        ["Ember"] = (new Color(1f, 0.38f, 0.08f), new Color(1f, 0.32f, 0.06f), 0f, 1f, 4f),
    };

    private bool _toon;

    public override async void _Ready()
    {
        _toon = OS.GetEnvironment("SUBTERRA_STYLE") != "figurine";
        var table = OS.GetEnvironment("SUBTERRA_VIEW") == "table";

        AddChild(new WorldEnvironment { Environment = Environment(table) });
        Light();

        var tile = GD.Load<PackedScene>("res://scenes/board/TileNormal_Junction.tscn").Instantiate<Node3D>();
        tile.RotationDegrees = new Vector3(0f, 90f, 0f);
        AddChild(tile);

        if (table)
        {
            // Sliced at vault height, as the game does from above.
            BoardView.ShowVaults(false);
        }
        else
        {
            // Up close the rock is a solid block around the camera: keep the paved
            // floor, drop the rest, and shoot the figures as on a display plinth.
            tile.GetNode("Cave").QueueFree();
        }

        Stand("res://resources/models/figures/explorer_sketch.glb", new Vector3(-0.5f, 0f, 0.25f), 20f, baseRadius: 0.34f);
        Stand("res://resources/models/figures/guardian_sketch.glb", new Vector3(0.55f, 0f, -0.2f), -25f, baseRadius: 0.44f);

        var camera = new Camera3D { Fov = table ? 32f : 40f, Current = true };
        AddChild(camera);

        if (table)
        {
            // Roughly the game's own view from above: the whole temple in frame, so a
            // figure is a handful of pixels tall and must still read.
            camera.LookAtFromPosition(new Vector3(0f, 13f, 10f), Vector3.Zero, Vector3.Up);
        }
        else
        {
            camera.LookAtFromPosition(new Vector3(0.1f, 1.6f, 4.3f), new Vector3(0f, 1.2f, 0f), Vector3.Up);
        }

        for (var i = 0; i < 8; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }

        GetViewport().GetTexture().GetImage().SavePng(OS.GetEnvironment("SUBTERRA_SHOT"));
        GetTree().Quit();
    }

    private Godot.Environment Environment(bool table) => new()
    {
        BackgroundMode = Godot.Environment.BGMode.Color,
        // Close up, a mid-tone studio backdrop: black ink on black would hide the
        // very line the cartoon look is about, and the painted one needs its shadows read.
        BackgroundColor = table ? new Color(0.04f, 0.025f, 0.02f) : new Color(0.3f, 0.26f, 0.23f),
        AmbientLightSource = Godot.Environment.AmbientSource.Color,
        AmbientLightColor = _toon ? new Color(0.55f, 0.45f, 0.5f) : new Color(0.5f, 0.36f, 0.28f),
        AmbientLightEnergy = _toon ? 0.35f : 0.45f,
        TonemapMode = Godot.Environment.ToneMapper.Filmic,
        GlowEnabled = true,
        GlowIntensity = 0.6f,
        // The miniature look leans on the photograph: occlusion in every recess, as a
        // wash would leave it, and a shallow focus falling off behind the figures.
        SsaoEnabled = !_toon,
        SsaoRadius = 0.25f,
        SsaoIntensity = 2.5f,
    };

    private void Light()
    {
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-40f, 35f, 0f),
            LightColor = new Color(1f, 0.88f, 0.75f),
            LightEnergy = _toon ? 1.6f : 1.3f,
            ShadowEnabled = true,
        });

        // A cold light from behind, to pick the silhouettes out of the dark.
        AddChild(new OmniLight3D
        {
            Position = new Vector3(0.3f, 2.2f, -1.6f),
            LightColor = new Color(0.55f, 0.65f, 1f),
            LightEnergy = 2.2f,
            OmniRange = 5f,
        });

        AddChild(new OmniLight3D
        {
            Position = new Vector3(-0.8f, 2.1f, 0.3f),
            LightColor = new Color(1f, 0.6f, 0.25f),
            LightEnergy = 1.6f,
            OmniRange = 4f,
        });
    }

    private void Stand(string path, Vector3 at, float turn, float baseRadius)
    {
        var figure = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        figure.Position = at;
        figure.RotationDegrees = new Vector3(0f, turn, 0f);

        if (!_toon)
        {
            // A painted miniature stands on its round base, rim painted black.
            var stand = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = baseRadius, BottomRadius = baseRadius + 0.02f, Height = 0.06f, RadialSegments = 40 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.06f, 0.05f, 0.05f), Roughness = 0.35f },
                Position = at + new Vector3(0f, 0.03f, 0f),
            };
            AddChild(stand);

            stand.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = baseRadius - 0.015f, BottomRadius = baseRadius - 0.015f, Height = 0.01f, RadialSegments = 40 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.33f, 0.24f), Roughness = 1f },
                Position = new Vector3(0f, 0.032f, 0f),
            });

            figure.Position += new Vector3(0f, 0.065f, 0f);
        }

        AddChild(figure);
        Dress(figure);
    }

    private void Dress(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is { } shape)
        {
            for (var surface = 0; surface < shape.GetSurfaceCount(); surface++)
            {
                var name = shape.SurfaceGetMaterial(surface)?.ResourceName ?? "";

                if (Paints.TryGetValue(name, out var paint))
                {
                    mesh.SetSurfaceOverrideMaterial(surface, _toon ? Toon(name, paint) : Mini(paint));
                }
                else
                {
                    GD.PushWarning($"Pas de peinture pour « {name} » ({mesh.Name}).");
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Dress(child);
        }
    }

    private static Material Toon(string name, (Color Toon, Color Mini, float Metal, float Rough, float Glow) paint)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/styles/toon.gdshader") };
        material.SetShaderParameter("albedo", paint.Toon);
        material.SetShaderParameter("emission", paint.Toon * paint.Glow);

        // Fire has no ink line; everything solid does.
        if (paint.Glow == 0f)
        {
            material.NextPass = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/styles/ink_outline.gdshader") };
        }

        return material;
    }

    private static Material Mini((Color Toon, Color Mini, float Metal, float Rough, float Glow) paint)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/styles/painted_mini.gdshader") };
        material.SetShaderParameter("albedo", paint.Mini);
        material.SetShaderParameter("metallic", paint.Metal);
        material.SetShaderParameter("roughness", paint.Rough);
        material.SetShaderParameter("emission", paint.Mini * paint.Glow);
        return material;
    }
}
