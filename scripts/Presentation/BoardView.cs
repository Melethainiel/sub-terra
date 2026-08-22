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
    public const float TileSize = 2f;

    /// <summary>
    /// One quarter turn of a tile. Negative because the model turns a tile clockwise
    /// on the table — north becomes east — while Godot's Y rotation is anticlockwise.
    /// </summary>
    private const float QuarterTurnDegrees = -90f;

    private const string TileScenePath = "res://scenes/board/Tile{0}_{1}.tscn";

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
    }

    /// <summary>
    /// Checks that the carved scene agrees with the tile the rules engine placed. The
    /// geometry is authored by hand, so a wall in the wrong place would otherwise show
    /// up as a temple that silently disagrees with itself.
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
