namespace SubTerra.Core.Board;

/// <summary>
/// The four sides of a tile. <see cref="North"/> points back towards the Entrance,
/// so rows grow as the temple sinks away from it.
/// </summary>
public enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

public static class DirectionExtensions
{
    public static readonly Direction[] All =
        [Direction.North, Direction.East, Direction.South, Direction.West];

    public static Direction Opposite(this Direction direction) =>
        (Direction)(((int)direction + 2) % 4);

    /// <summary>What the players call it, for a prompt or a label.</summary>
    public static string Name(this Direction direction) => direction switch
    {
        Direction.North => "Nord",
        Direction.East => "Est",
        Direction.South => "Sud",
        Direction.West => "Ouest",
        _ => direction.ToString(),
    };

    /// <summary>Rotates clockwise by <paramref name="quarterTurns"/>.</summary>
    public static Direction Rotate(this Direction direction, int quarterTurns) =>
        (Direction)(((int)direction + (quarterTurns % 4 + 4)) % 4);
}
