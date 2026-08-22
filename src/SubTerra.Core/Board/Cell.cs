namespace SubTerra.Core.Board;

/// <summary>
/// A single tile-sized square of the table. Multi-tile pieces (Entrance, Laterals,
/// Sanctuary) occupy several cells; the rules treat each of those cells as a tile.
/// </summary>
public readonly record struct Cell(int Column, int Row)
{
    public Cell Neighbour(Direction direction) => direction switch
    {
        Direction.North => this with { Row = Row - 1 },
        Direction.East => this with { Column = Column + 1 },
        Direction.South => this with { Row = Row + 1 },
        Direction.West => this with { Column = Column - 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>The direction one must face to step from this cell to an adjacent one.</summary>
    public Direction? DirectionTo(Cell other)
    {
        foreach (var direction in DirectionExtensions.All)
        {
            if (Neighbour(direction) == other)
            {
                return direction;
            }
        }

        return null;
    }

    public override string ToString() => $"({Column},{Row})";
}
