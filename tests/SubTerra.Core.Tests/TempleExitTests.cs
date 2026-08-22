using SubTerra.Core.Board;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class TempleExitTests
{
    /// <summary>A printed tile laid out so that exactly these sides are open.</summary>
    private static PlacedTile Tile(Sides openSides)
    {
        var layout = TileShapeExtensions.Match(openSides)
            ?? throw new ArgumentException($"No tile shape lays out {openSides}.", nameof(openSides));

        return new PlacedTile(new TileDefinition("test", TileKind.Normal, layout.Shape), layout.Rotation);
    }

    [Fact]
    public void AnExitPointsAtTheCellBeyondIt()
    {
        var exit = new TempleExit(new Cell(3, 1), Direction.South);
        Assert.Equal(new Cell(3, 2), exit.Target);
    }

    [Fact]
    public void OnlyOpenSidesGivingOntoEmptyGroundCount()
    {
        var board = new TempleBoard();
        board.PlaceFixed(new Cell(3, 1), Tile(Sides.South | Sides.East));

        var exits = board.OpenExits().ToList();

        Assert.Equal(2, exits.Count);
        Assert.Contains(new TempleExit(new Cell(3, 1), Direction.South), exits);
        Assert.Contains(new TempleExit(new Cell(3, 1), Direction.East), exits);
    }

    [Fact]
    public void AnExitStopsBeingOneOnceATileFillsIt()
    {
        var board = new TempleBoard();
        var origin = new Cell(3, 1);
        board.PlaceFixed(origin, Tile(Sides.South));

        Assert.Single(board.OpenExits());

        board.Place(new Cell(3, 2), Tile(Sides.All), origin);

        Assert.DoesNotContain(new TempleExit(origin, Direction.South), board.OpenExits());
    }

    [Fact]
    public void ExitsNeverLeaveTheLateralTilesSpan()
    {
        var board = new TempleBoard();
        board.PlaceFixed(new Cell(0, 1), Tile(Sides.All));

        // West would run off the left edge, north back behind the Entrance.
        Assert.Equal(
            [Direction.East, Direction.South],
            board.OpenExits().Select(exit => exit.Direction).Order());
    }

    [Fact]
    public void ADemolishedWallOpensAnExit()
    {
        var board = new TempleBoard();
        var origin = new Cell(3, 1);
        board.PlaceFixed(origin, Tile(Sides.North));

        Assert.Empty(board.OpenExits());

        board.Demolish(origin, new Cell(3, 2));

        Assert.Equal([new TempleExit(origin, Direction.South)], board.OpenExits());
    }

    [Fact]
    public void TheSetupBoardOnlyOpensSouthwardIntoTheTemple()
    {
        var exits = TempleSetup.CreateBoard().OpenExits().ToList();

        Assert.All(exits, exit => Assert.Equal(Direction.South, exit.Direction));

        // The four Normal cells of the arms, plus the Entrance crossroads itself.
        // The Guardian pockets at the far ends open inward only.
        Assert.Equal(5, exits.Count);
    }
}
