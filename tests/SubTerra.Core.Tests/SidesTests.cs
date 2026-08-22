using SubTerra.Core.Board;

namespace SubTerra.Core.Tests;

public class SidesTests
{
    [Fact]
    public void RotatingOnceTurnsNorthIntoEast()
    {
        Assert.Equal(Sides.East, Sides.North.Rotate(1));
    }

    [Fact]
    public void RotatingWrapsWestBackToNorth()
    {
        Assert.Equal(Sides.North, Sides.West.Rotate(1));
    }

    [Fact]
    public void FourRotationsReturnToTheOriginalLayout()
    {
        var corridor = Sides.North | Sides.South;
        Assert.Equal(corridor, corridor.Rotate(4));
    }

    [Fact]
    public void NegativeRotationTurnsAnticlockwise()
    {
        Assert.Equal(Sides.West, Sides.North.Rotate(-1));
    }

    [Theory]
    [InlineData(Direction.North, true)]
    [InlineData(Direction.East, false)]
    [InlineData(Direction.South, true)]
    [InlineData(Direction.West, false)]
    public void IsOpenReadsTheCorrectSide(Direction direction, bool expected)
    {
        var corridor = Sides.North | Sides.South;
        Assert.Equal(expected, corridor.IsOpen(direction));
    }
}
