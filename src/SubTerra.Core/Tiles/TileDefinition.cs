using SubTerra.Core.Board;

namespace SubTerra.Core.Tiles;

/// <summary>
/// One physical tile, as printed. Its <paramref name="Shape"/> is fixed — a tile is
/// a piece of card, not a kit — and given in the shape's base orientation;
/// <see cref="Board.PlacedTile"/> applies the rotation chosen when it was placed.
/// </summary>
/// <param name="RuinsNumber">
/// The die face that collapses this tile. Only Ruins tiles carry one.
/// </param>
public sealed record TileDefinition(
    string Id,
    TileKind Kind,
    TileShape Shape,
    int? RuinsNumber = null)
{
    /// <summary>The passages out of the tile, before it is rotated onto the table.</summary>
    public Sides OpenSides => Shape.OpenSides();
}
