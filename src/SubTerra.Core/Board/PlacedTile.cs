using SubTerra.Core.Tiles;

namespace SubTerra.Core.Board;

/// <summary>A tile on the table: its printed definition, its rotation and its face.</summary>
/// <param name="Consolidated">
/// Under the Contremaître's Consolidation marker: whatever it was printed as, it is a
/// Normal tile until the end of the game. Its drawing does not change, its rules do.
/// </param>
public sealed record PlacedTile(
    TileDefinition Definition,
    int Rotation = 0,
    TileFace Face = TileFace.Temple,
    bool Consolidated = false)
{
    /// <summary>The open sides as they actually lie on the table.</summary>
    public Sides OpenSides => Definition.OpenSides.Rotate(Rotation);

    public TileKind Kind => Consolidated ? TileKind.Normal : Definition.Kind;

    public bool IsOpen(Direction direction) => OpenSides.IsOpen(direction);

    public PlacedTile Flipped() => this with { Face = TileFace.Volcano };
}
