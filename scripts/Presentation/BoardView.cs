using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Presentation;

/// <summary>
/// Draws a <see cref="TempleBoard"/> as flat 3D tiles with walls on their closed
/// sides. This is the only place that knows how a <see cref="Cell"/> maps to world
/// space — the rules engine never deals in metres.
/// </summary>
public partial class BoardView : Node3D
{
    /// <summary>Edge length of one tile, in metres.</summary>
    public const float TileSize = 2f;

    private const float TileThickness = 0.1f;
    private const float TileInset = 0.05f;
    private const float WallHeight = 0.7f;
    private const float WallThickness = 0.12f;

    private static readonly Color WallColour = Color.Color8(58, 40, 34);

    private readonly Dictionary<TileKind, StandardMaterial3D> _materials = [];

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
        var origin = ToWorld(cell);

        var floor = new MeshInstance3D
        {
            Name = $"Tile_{cell.Column}_{cell.Row}",
            Mesh = new BoxMesh
            {
                Size = new Vector3(TileSize - TileInset, TileThickness, TileSize - TileInset),
            },
            MaterialOverride = MaterialFor(tile.Kind),
            Position = origin,
        };
        AddChild(floor);

        foreach (var direction in DirectionExtensions.All)
        {
            if (!tile.IsOpen(direction))
            {
                AddWall(floor, direction);
            }
        }
    }

    private void AddWall(Node3D tile, Direction direction)
    {
        var alongZ = direction is Direction.North or Direction.South;
        var offset = TileSize / 2f - WallThickness / 2f;
        var sign = direction is Direction.North or Direction.West ? -1f : 1f;

        tile.AddChild(new MeshInstance3D
        {
            Name = $"Wall_{direction}",
            Mesh = new BoxMesh
            {
                Size = alongZ
                    ? new Vector3(TileSize, WallHeight, WallThickness)
                    : new Vector3(WallThickness, WallHeight, TileSize),
            },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = WallColour },
            Position = alongZ
                ? new Vector3(0f, WallHeight / 2f, sign * offset)
                : new Vector3(sign * offset, WallHeight / 2f, 0f),
        });
    }

    private StandardMaterial3D MaterialFor(TileKind kind)
    {
        if (!_materials.TryGetValue(kind, out var material))
        {
            material = new StandardMaterial3D { AlbedoColor = ColourFor(kind), Roughness = 0.9f };
            _materials[kind] = material;
        }

        return material;
    }

    private static Color ColourFor(TileKind kind) => kind switch
    {
        TileKind.Entrance => Color.Color8(196, 170, 122),
        TileKind.Lateral => Color.Color8(122, 80, 62),
        TileKind.Guardian => Color.Color8(108, 72, 132),
        TileKind.Lava => Color.Color8(214, 88, 38),
        TileKind.Key => Color.Color8(190, 150, 70),
        TileKind.Ruins => Color.Color8(86, 74, 68),
        TileKind.Bridge => Color.Color8(150, 96, 64),
        TileKind.SpikeTrap or TileKind.DartTrap => Color.Color8(150, 62, 58),
        TileKind.Sanctuary => Color.Color8(232, 148, 54),
        _ => Color.Color8(134, 92, 72),
    };
}
