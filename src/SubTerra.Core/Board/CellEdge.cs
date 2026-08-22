namespace SubTerra.Core.Board;

/// <summary>
/// The undirected boundary between two adjacent cells. Used to record where the
/// Sapper's Demolition markers have knocked a wall down.
/// </summary>
public readonly record struct CellEdge
{
    public Cell Low { get; }

    public Cell High { get; }

    public CellEdge(Cell a, Cell b)
    {
        if (a.DirectionTo(b) is null)
        {
            throw new ArgumentException($"Cells {a} and {b} are not adjacent.", nameof(b));
        }

        // Normalised so that the edge (a,b) and the edge (b,a) compare equal.
        var swap = (b.Row, b.Column).CompareTo((a.Row, a.Column)) < 0;
        Low = swap ? b : a;
        High = swap ? a : b;
    }

    public override string ToString() => $"{Low}|{High}";
}
