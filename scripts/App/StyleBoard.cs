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
    private bool _toon;

    /// <summary>The painted miniature, with a finer ink line and brighter paint so it
    /// still stands out of the rock seen from above.</summary>
    private bool _hybrid;

    public override async void _Ready()
    {
        var style = OS.GetEnvironment("SUBTERRA_STYLE");
        _hybrid = style == "hybrid";
        _toon = style is not ("figurine" or "hybrid");
        var table = OS.GetEnvironment("SUBTERRA_VIEW") == "table";

        if (OS.GetEnvironment("SUBTERRA_VIEW") == "lineup")
        {
            await Lineup();
            return;
        }

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

    /// <summary>
    /// The whole cast in a row, each in a seat's colour, as the game dresses them —
    /// for judging them against one another rather than one at a time.
    /// </summary>
    private async System.Threading.Tasks.Task Lineup()
    {
        _hybrid = true;
        AddChild(new WorldEnvironment { Environment = Environment(table: false) });
        Light();

        var cast = SubTerra.Core.Explorers.ExplorerRoster.All;
        const float Spacing = 0.72f;

        for (var index = 0; index < cast.Count; index++)
        {
            var x = (index - (cast.Count - 1) / 2f) * Spacing;
            var stand = Miniature.Stand($"res://resources/models/figures/{cast[index].Id}.glb", Miniature.ExplorerBase, Palette.SeatColor(index), Palette.SeatColor(index));
            stand.Position = new Vector3(x, 0f, 0f);
            AddChild(stand);

            AddChild(new Label3D
            {
                Text = cast[index].Name.Replace("La ", "").Replace("Le ", "").Replace("L'", ""),
                FontSize = 40,
                PixelSize = 0.0025f,
                Modulate = new Color(0.95f, 0.9f, 0.8f),
                OutlineSize = 8,
                Position = new Vector3(x, -0.12f, 0.35f),
            });
        }

        var guardian = Miniature.Stand("res://resources/models/figures/guardian.glb", Miniature.GuardianBase, ring: Palette.Guardian);
        guardian.Position = new Vector3(cast.Count / 2f * Spacing + 0.35f, 0f, -0.6f);
        AddChild(guardian);

        var camera = new Camera3D { Fov = 34f, Current = true };
        AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0.3f, 1.5f, 9.2f), new Vector3(0.3f, 0.85f, 0f), Vector3.Up);

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
        // The chosen look is the game's own, from the very same code.
        if (_hybrid)
        {
            Miniature.Dress(node);
            return;
        }

        if (node is MeshInstance3D mesh && mesh.Mesh is { } shape)
        {
            for (var surface = 0; surface < shape.GetSurfaceCount(); surface++)
            {
                var name = shape.SurfaceGetMaterial(surface)?.ResourceName ?? "";

                if (Miniature.Paints.TryGetValue(name, out var paint))
                {
                    mesh.SetSurfaceOverrideMaterial(surface, _toon ? Toon(paint) : Mini(paint));
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Dress(child);
        }
    }

    private static Material Toon(Miniature.Paint paint)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/styles/toon.gdshader") };
        material.SetShaderParameter("albedo", paint.Bright);
        material.SetShaderParameter("emission", paint.Bright * paint.Glow);

        // Fire has no ink line; everything solid does.
        if (paint.Glow == 0f)
        {
            material.NextPass = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/styles/ink_outline.gdshader") };
        }

        return material;
    }

    private static Material Mini(Miniature.Paint paint)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/styles/painted_mini.gdshader") };
        material.SetShaderParameter("albedo", paint.Painted);
        material.SetShaderParameter("metallic", paint.Metal);
        material.SetShaderParameter("roughness", paint.Rough);
        material.SetShaderParameter("emission", paint.Painted * paint.Glow);
        return material;
    }
}
