using SubTerra.Core.Board;

namespace SubTerra.Core.Tiles;

/// <summary>
/// One physical tile, as printed. <paramref name="OpenSides"/> is given in the
/// tile's unrotated orientation; <see cref="Board.PlacedTile"/> applies the rotation
/// chosen when it was placed.
/// </summary>
/// <param name="RuinsNumber">
/// The die face that collapses this tile. Only Ruins tiles carry one.
/// </param>
public sealed record TileDefinition(
    string Id,
    TileKind Kind,
    Sides OpenSides,
    int? RuinsNumber = null);
