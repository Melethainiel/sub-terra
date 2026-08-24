using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Presentation;

/// <summary>
/// Draws a <see cref="TempleBoard"/> by instantiating one scene per printed tile from
/// <c>scenes/board/</c>. This is the only place that knows how a <see cref="Cell"/>
/// maps to world space — the rules engine never deals in metres.
/// </summary>
/// <remarks>
/// A tile is a piece of card: its passage is carved in, not assembled. Each scene
/// holds the exact geometry of one kind-and-shape pair, and the view only lays it
/// down and turns it — nothing here builds or hides walls.
/// </remarks>
public partial class BoardView : Node3D
{
    /// <summary>Edge length of one tile, in metres. Must match the tile scenes.</summary>
    public const float TileSize = 3f;

    /// <summary>
    /// One quarter turn of a tile. Negative because the model turns a tile clockwise
    /// on the table — north becomes east — while Godot's Y rotation is anticlockwise.
    /// Not private: the FPS camera turns to face a <see cref="Direction"/> by the same
    /// convention, so a carved passage and the way it looks down it agree.
    /// </summary>
    internal const float QuarterTurnDegrees = -90f;

    private const string TileScenePath = "res://scenes/board/Tile{0}_{1}.tscn";

    private const float TorchHeight = 1.5f;

    private const float TorchRange = 3.9f;

    private const float TorchEnergy = 4.5f;

    private const float TorchAttenuation = 0.9f;

    private const int RubblePieces = 3;

    private const float RubbleMinSize = 0.12f;

    private const float RubbleMaxSize = 0.3f;

    /// <summary>Where the vault runs, over the middle of a tile: what stalactites
    /// hang from. A carved tile arches, so this only holds near the axis of a
    /// passage — hence the short reach they are scattered over.</summary>
    private const float VaultHeight = 2.15f;

    private const int StalactitesMin = 1;

    private const int StalactitesMax = 3;

    /// <summary>Chance a given stalactite is caught mid-drip, glowing at the tip —
    /// not every one, or the ceiling reads as a string of lamps instead of rock.</summary>
    private const double DripChance = 0.55;

    private static readonly Lazy<Material?> RubbleMaterial =
        new(() => ResourceLoader.Load<Material>("res://resources/materials/rubble_rock.tres"));

    private static readonly Lazy<Material?> RockMaterial =
        new(() => ResourceLoader.Load<Material>("res://resources/materials/wall_rock.tres"));

    private static readonly Lazy<Material?> DripMaterial =
        new(() => ResourceLoader.Load<Material>("res://resources/materials/lava_drip.tres"));

    private readonly Dictionary<(TileKind Kind, TileShape Shape), PackedScene?> _scenes = [];

    public static Vector3 ToWorld(Cell cell) => new(cell.Column * TileSize, 0f, cell.Row * TileSize);

    /// <summary>Clears and redraws the whole board. Cheap enough at this scale.</summary>
    public void Render(TempleBoard board)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        foreach (var (cell, tile) in board.Tiles)
        {
            AddTile(cell, tile);
        }
    }

    private void AddTile(Cell cell, PlacedTile tile)
    {
        if (SceneFor(tile.Definition) is not { } scene)
        {
            return;
        }

        var node = scene.Instantiate<Node3D>();
        node.Name = $"Tile_{cell.Column}_{cell.Row}";
        node.Position = ToWorld(cell);
        node.RotationDegrees = new Vector3(0f, QuarterTurnDegrees * tile.Rotation, 0f);
        AddChild(node);

        VerifyGeometry(node, tile, cell);
        AddGlow(node, tile.Definition.Kind);
        ScatterRubble(node, cell);
        ScatterStalactites(node, cell);
    }

    /// <summary>
    /// A torch's worth of light so a tile still reads once the camera is down at
    /// eye height, where the overhead sun and fog barely reach. Tinted by what the
    /// tile is, so a Guardian's alcove or a run of Lava is unmistakable even close up.
    /// </summary>
    private static void AddGlow(Node3D tileNode, TileKind kind)
    {
        var colour = Palette.TileGlow(kind);

        tileNode.AddChild(new OmniLight3D
        {
            Name = "Glow",
            Position = new Vector3(0f, TorchHeight, 0f),
            LightColor = colour,
            LightEnergy = TorchEnergy * Palette.TileGlowEnergyScale(kind),
            OmniRange = TorchRange,
            OmniAttenuation = TorchAttenuation,
        });
    }

    /// <summary>
    /// A few loose chunks against the walls — the detail a carved box on its own
    /// can't give: broken silhouette at floor level, not just a painted surface.
    /// </summary>
    private static void ScatterRubble(Node3D tileNode, Cell cell)
    {
        if (RubbleMaterial.Value is not { } material)
        {
            return;
        }

        var rng = new Random(HashCode.Combine(cell.Column, cell.Row, "rubble"));

        for (var i = 0; i < RubblePieces; i++)
        {
            var size = RubbleMinSize + (float)rng.NextDouble() * (RubbleMaxSize - RubbleMinSize);
            var angle = (float)rng.NextDouble() * Mathf.Tau;
            var radius = 0.85f + (float)rng.NextDouble() * 0.5f;

            tileNode.AddChild(new MeshInstance3D
            {
                Name = $"Rubble_{i}",
                Mesh = new BoxMesh { Size = new Vector3(size, size * 0.7f, size) },
                MaterialOverride = material,
                Position = new Vector3(Mathf.Cos(angle) * radius, size * 0.35f, Mathf.Sin(angle) * radius),
                RotationDegrees = new Vector3(0f, (float)rng.NextDouble() * 360f, 0f),
            });
        }
    }

    /// <summary>
    /// A few spikes of rock hanging low enough to notice from an Explorer's eye
    /// height — a ceiling that never breaks its own plane still reads as a lid.
    /// Some are caught mid-drip, a bead of the mountain's own heat at the tip.
    /// </summary>
    private static void ScatterStalactites(Node3D tileNode, Cell cell)
    {
        if (RockMaterial.Value is not { } rock)
        {
            return;
        }

        var rng = new Random(HashCode.Combine(cell.Column, cell.Row, "stalactite"));
        var count = rng.Next(StalactitesMin, StalactitesMax + 1);

        for (var i = 0; i < count; i++)
        {
            var height = 0.33f + (float)rng.NextDouble() * 0.3f;
            var radius = 0.07f + (float)rng.NextDouble() * 0.07f;
            var angle = (float)rng.NextDouble() * Mathf.Tau;
            var offset = 0.25f + (float)rng.NextDouble() * 0.35f;
            var tip = new Vector3(Mathf.Cos(angle) * offset, VaultHeight - height, Mathf.Sin(angle) * offset);

            tileNode.AddChild(new MeshInstance3D
            {
                Name = $"Stalactite_{i}",
                Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = 0f, Height = height, RadialSegments = 6 },
                MaterialOverride = rock,
                Position = tip + new Vector3(0f, height / 2f, 0f),
            });

            if (DripMaterial.Value is not { } drip || rng.NextDouble() > DripChance)
            {
                continue;
            }

            tileNode.AddChild(new MeshInstance3D
            {
                Name = $"Drip_{i}",
                Mesh = new SphereMesh { Radius = radius * 0.9f, Height = radius * 1.8f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = drip,
                Position = tip,
            });
        }
    }

    /// <summary>
    /// Checks that the carved scene agrees with the tile the rules engine placed. The
    /// geometry is authored by hand, so a wall in the wrong place would otherwise show
    /// up as a temple that silently disagrees with itself.
    /// A scene says which sides it walls off with a <c>Rock/Wall_{side}</c> node —
    /// the box that does the walling on the older tiles, a bare marker on the ones
    /// whose rock is a single carved mesh.
    /// </summary>
    private static void VerifyGeometry(Node3D node, PlacedTile tile, Cell cell)
    {
        foreach (var side in DirectionExtensions.All)
        {
            var carvedShut = node.GetNodeOrNull($"Rock/Wall_{side}") is not null;
            var shutOnTable = !tile.IsOpen(side.Rotate(tile.Rotation));

            if (carvedShut != shutOnTable)
            {
                GD.PushWarning(
                    $"{tile.Definition.Id} en {cell} : la face {side} de la scène est " +
                    $"{(carvedShut ? "murée" : "ouverte")}, le plateau la veut " +
                    $"{(shutOnTable ? "murée" : "ouverte")}.");
            }
        }
    }

    private PackedScene? SceneFor(TileDefinition definition)
    {
        var key = (definition.Kind, definition.Shape);

        if (_scenes.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var path = string.Format(TileScenePath, definition.Kind, definition.Shape);
        var scene = ResourceLoader.Load<PackedScene>(path);

        if (scene is null)
        {
            // A missing tile is a mistake worth hearing about, not a silent hole in
            // the temple: fall back to a plain tile of the same shape.
            GD.PushWarning($"Aucune scène pour {definition.Kind}/{definition.Shape} ({path}).");
            scene = ResourceLoader.Load<PackedScene>(
                string.Format(TileScenePath, TileKind.Normal, definition.Shape));
        }

        _scenes[key] = scene;
        return scene;
    }
}
