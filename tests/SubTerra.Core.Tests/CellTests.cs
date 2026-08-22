using SubTerra.Core.Board;

namespace SubTerra.Core.Tests;

public class CellTests
{
    [Fact]
    public void NorthWalksBackTowardsTheEntrance()
    {
        Assert.Equal(new Cell(2, 1), new Cell(2, 2).Neighbour(Direction.North));
    }

    [Fact]
    public void DirectionToIsNullForNonAdjacentCells()
    {
        Assert.Null(new Cell(0, 0).DirectionTo(new Cell(2, 0)));
    }

    [Fact]
    public void EdgesAreUndirected()
    {
        var a = new Cell(1, 1);
        var b = new Cell(1, 2);
        Assert.Equal(new CellEdge(a, b), new CellEdge(b, a));
    }

    [Fact]
    public void EdgesRejectNonAdjacentCells()
    {
        Assert.Throws<ArgumentException>(() => new CellEdge(new Cell(0, 0), new Cell(3, 3)));
    }
}
