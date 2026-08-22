using SubTerra.Core.Tiles;

namespace SubTerra.Core.Board;

/// <summary>A tile on the table: its printed definition, its rotation and its face.</summary>
public sealed record PlacedTile(
    TileDefinition Definition,
    int Rotation = 0,
    TileFace Face = TileFace.Temple)
{
    /// <summary>The open sides as they actually lie on the table.</summary>
    public Sides OpenSides => Definition.OpenSides.Rotate(Rotation);

    public TileKind Kind => Definition.Kind;

    public bool IsOpen(Direction direction) => OpenSides.IsOpen(direction);

    public PlacedTile Flipped() => this with { Face = TileFace.Volcano };
}
