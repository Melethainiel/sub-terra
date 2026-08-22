using SubTerra.Core.Board;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class TempleSetupTests
{
    [Fact]
    public void SetupPlacesTheEntranceAndBothLaterals()
    {
        var board = TempleSetup.CreateBoard();

        Assert.Equal(8, board.Tiles.Count);
        Assert.Equal(TileKind.Entrance, board.TileAt(TempleSetup.EntranceCrossing)!.Kind);
        Assert.Equal(TileKind.Entrance, board.TileAt(TempleSetup.EntranceExit)!.Kind);
    }

    [Fact]
    public void TheCrossingReachesBothLateralsAndTheExit()
    {
        var board = TempleSetup.CreateBoard();
        var crossing = TempleSetup.EntranceCrossing;

        Assert.True(board.AreConnected(crossing, TempleSetup.EntranceExit));
        Assert.True(board.AreConnected(crossing, TempleSetup.WestLateral[^1]));
        Assert.True(board.AreConnected(crossing, TempleSetup.EastLateral[0]));
    }

    [Fact]
    public void EveryLateralCellIsReachableFromTheStartingCell()
    {
        var board = TempleSetup.CreateBoard();

        foreach (var cell in TempleSetup.WestLateral.Concat(TempleSetup.EastLateral))
        {
            Assert.NotNull(board.Distance(TempleSetup.EntranceCrossing, cell));
        }
    }

    [Fact]
    public void TheOuterEndOfEachLateralIsAGuardianCell()
    {
        var board = TempleSetup.CreateBoard();

        Assert.Equal(TileKind.Guardian, board.TileAt(TempleSetup.WestLateral[0])!.Kind);
        Assert.Equal(TileKind.Guardian, board.TileAt(TempleSetup.EastLateral[^1])!.Kind);
    }

    [Fact]
    public void TilesCanBeRevealedSouthFromALateral()
    {
        var board = TempleSetup.CreateBoard();
        var from = TempleSetup.WestLateral[1];

        Assert.True(board.CanPlace(new Cell(from.Column, 1), new PlacedTile(TileCatalog.CreateBag()[0]), from));
    }
}
