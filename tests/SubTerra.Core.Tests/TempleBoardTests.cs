using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class TempleBoardTests
{
    private static PlacedTile Tile(Sides openSides, TileKind kind = TileKind.Normal) =>
        new(new TileDefinition("test", kind, openSides));

    private static TempleBoard BoardWith(Cell cell, Sides openSides)
    {
        var board = new TempleBoard();
        board.PlaceFixed(cell, Tile(openSides));
        return board;
    }

    [Fact]
    public void TilesConnectWhenBothFacingSidesAreOpen()
    {
        var origin = new Cell(3, 1);
        var board = BoardWith(origin, Sides.All);

        board.Place(new Cell(3, 2), Tile(Sides.North), origin);

        Assert.True(board.AreConnected(origin, new Cell(3, 2)));
    }

    [Fact]
    public void AnOpenSideLaidAgainstAWallDoesNotConnect()
    {
        var origin = new Cell(3, 1);
        var board = BoardWith(origin, Sides.All);
        var neighbour = new Cell(3, 2);

        // The neighbour's north side is a wall, so the pair stays severed even though
        // the origin opens onto it. Placing it there is illegal for the same reason.
        board.PlaceFixed(neighbour, Tile(Sides.South));

        Assert.False(board.AreConnected(origin, neighbour));
    }

    [Fact]
    public void PlacementIsRejectedWhenTheTileWouldNotConnect()
    {
        var origin = new Cell(3, 1);
        var board = BoardWith(origin, Sides.All);

        Assert.False(board.CanPlace(new Cell(3, 2), Tile(Sides.South), origin));
        Assert.Throws<InvalidOperationException>(
            () => board.Place(new Cell(3, 2), Tile(Sides.South), origin));
    }

    [Fact]
    public void RotationCanMakeAnUnplaceableTilePlaceable()
    {
        var origin = new Cell(3, 1);
        var board = BoardWith(origin, Sides.All);
        var deadEndFacingSouth = new TileDefinition("dead-end", TileKind.Normal, Sides.South);

        Assert.False(board.CanPlace(new Cell(3, 2), new PlacedTile(deadEndFacingSouth), origin));
        Assert.True(board.CanPlace(new Cell(3, 2), new PlacedTile(deadEndFacingSouth, Rotation: 2), origin));
    }

    [Fact]
    public void PlacementIsRejectedOutsideTheLateralTilesSpan()
    {
        var origin = new Cell(0, 1);
        var board = BoardWith(origin, Sides.All);

        Assert.False(board.CanPlace(new Cell(-1, 1), Tile(Sides.All), origin));
    }

    [Fact]
    public void PlacementIsRejectedBehindTheEntrance()
    {
        var origin = new Cell(3, 1);
        var board = BoardWith(origin, Sides.All);

        Assert.False(board.CanPlace(new Cell(3, 0), Tile(Sides.All), origin));
    }

    [Fact]
    public void DemolitionOpensAWallForGood()
    {
        var origin = new Cell(3, 1);
        var neighbour = new Cell(3, 2);
        var board = BoardWith(origin, Sides.All);
        board.PlaceFixed(neighbour, Tile(Sides.South));

        Assert.False(board.AreConnected(origin, neighbour));

        board.Demolish(origin, neighbour);

        Assert.True(board.AreConnected(origin, neighbour));
    }

    [Fact]
    public void DistanceCountsTilesAlongTheShortestConnectedPath()
    {
        var board = new TempleBoard();
        for (var row = 1; row <= 4; row++)
        {
            board.PlaceFixed(new Cell(3, row), Tile(Sides.All));
        }

        Assert.Equal(0, board.Distance(new Cell(3, 1), new Cell(3, 1)));
        Assert.Equal(3, board.Distance(new Cell(3, 1), new Cell(3, 4)));
    }

    [Fact]
    public void DistanceIsNullWhenNoConnectedPathExists()
    {
        var board = new TempleBoard();
        board.PlaceFixed(new Cell(3, 1), Tile(Sides.All));
        board.PlaceFixed(new Cell(3, 3), Tile(Sides.All));

        Assert.Null(board.Distance(new Cell(3, 1), new Cell(3, 3)));
    }

    [Fact]
    public void ImpassableTilesCanStillBeTheTargetButNotARouteThrough()
    {
        var board = new TempleBoard();
        for (var row = 1; row <= 3; row++)
        {
            board.PlaceFixed(new Cell(3, row), Tile(Sides.All));
        }

        // A Guardian walking towards an Explorer may not path through a rubble-blocked
        // tile, but it must still be able to measure the distance to one.
        var blocked = new Cell(3, 2);
        Assert.Equal(1, board.Distance(new Cell(3, 1), blocked, passable: cell => cell != blocked));
        Assert.Null(board.Distance(new Cell(3, 1), new Cell(3, 3), passable: cell => cell != blocked));
    }
}
