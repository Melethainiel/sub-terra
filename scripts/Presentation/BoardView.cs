using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Presentation;

/// <summary>
/// Draws a <see cref="TempleBoard"/> by instantiating one scene per tile kind from
/// <c>scenes/board/</c>. This is the only place that knows how a <see cref="Cell"/>
/// maps to world space — the rules engine never deals in metres.
/// </summary>
/// <remarks>
/// Every tile scene carries all four walls, named <c>Wall_North</c> … <c>Wall_West</c>;
/// the view hides the ones the tile opens onto. Nothing rotates: the rotation chosen
/// when the tile was placed is already baked into <see cref="PlacedTile.OpenSides"/>,
/// so any shape is expressible without turning the node.
/// </remarks>
public partial class BoardView : Node3D
{
    /// <summary>Edge length of one tile, in metres. Must match the tile scenes.</summary>
    public const float TileSize = 2f;

    private const string TileScenePath = "res://scenes/board/Tile{0}.tscn";

    private readonly Dictionary<TileKind, PackedScene> _scenes = [];

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
        if (SceneFor(tile.Kind) is not { } scene)
        {
            return;
        }

        var node = scene.Instantiate<Node3D>();
        node.Name = $"Tile_{cell.Column}_{cell.Row}";
        node.Position = ToWorld(cell);
        AddChild(node);

        foreach (var direction in DirectionExtensions.All)
        {
            if (node.GetNodeOrNull<Node3D>($"Wall_{direction}") is { } wall)
            {
                wall.Visible = !tile.IsOpen(direction);
            }
        }
    }

    private PackedScene? SceneFor(TileKind kind)
    {
        if (_scenes.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var path = string.Format(TileScenePath, kind);
        var scene = ResourceLoader.Load<PackedScene>(path);

        if (scene is null)
        {
            // A missing scene is a mistake worth hearing about, not a silent hole in
            // the temple: fall back to the plain tile so the board stays walkable.
            GD.PushWarning($"Aucune scène de tuile pour {kind} ({path}) — repli sur TileNormal.");
            scene = ResourceLoader.Load<PackedScene>(string.Format(TileScenePath, TileKind.Normal));
        }

        _scenes[kind] = scene;
        return scene;
    }
}
