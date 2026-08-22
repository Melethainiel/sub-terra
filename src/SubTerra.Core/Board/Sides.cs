namespace SubTerra.Core.Board;

/// <summary>Which sides of a tile are open passages rather than walls.</summary>
[Flags]
public enum Sides
{
    None = 0,
    North = 1 << Direction.North,
    East = 1 << Direction.East,
    South = 1 << Direction.South,
    West = 1 << Direction.West,
    All = North | East | South | West,
}

public static class SidesExtensions
{
    private const int Mask = 0b1111;

    public static bool IsOpen(this Sides sides, Direction direction) =>
        (sides & (Sides)(1 << (int)direction)) != 0;

    /// <summary>Rotates the open sides clockwise by <paramref name="quarterTurns"/>.</summary>
    public static Sides Rotate(this Sides sides, int quarterTurns)
    {
        var turns = (quarterTurns % 4 + 4) % 4;
        var bits = (int)sides & Mask;
        return (Sides)(((bits << turns) | (bits >> (4 - turns))) & Mask);
    }
}
