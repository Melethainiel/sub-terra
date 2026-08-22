using SubTerra.Core.Board;

namespace SubTerra.Core.Tiles;

/// <summary>
/// The passage carved through a tile. The rulebook never prints these — they are
/// artwork — so this is our own design, in the spirit of the originals: mostly
/// junctions and corridors, crossroads kept scarce so the temple reads as a maze
/// rather than an open floor.
/// </summary>
public enum TileShape
{
    /// <summary>Open on all four sides. The rare wide chamber.</summary>
    Crossroads,

    /// <summary>Open on three sides.</summary>
    Junction,

    /// <summary>Open on two opposite sides.</summary>
    Corridor,

    /// <summary>Open on two adjacent sides.</summary>
    Corner,
}

public static class TileShapeExtensions
{
    /// <summary>
    /// The open sides in the shape's base orientation. Arbitrary: a tile is rotated
    /// freely when it is placed.
    /// </summary>
    public static Sides OpenSides(this TileShape shape) => shape switch
    {
        TileShape.Crossroads => Sides.All,
        TileShape.Junction => Sides.North | Sides.East | Sides.South,
        TileShape.Corridor => Sides.North | Sides.South,
        TileShape.Corner => Sides.North | Sides.East,
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };
}
