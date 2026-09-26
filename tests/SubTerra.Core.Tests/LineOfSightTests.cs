using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class LineOfSightTests
{
    private static readonly Cell Origin = new(0, 0);

    /// <summary>A printed tile laid out so that exactly these sides are open.</summary>
    private static PlacedTile Tile(Sides openSides)
    {
        var layout = TileShapeExtensions.Match(openSides)
            ?? throw new ArgumentException($"No tile shape lays out {openSides}.", nameof(openSides));

        return new PlacedTile(new TileDefinition("test", TileKind.Normal, layout.Shape), layout.Rotation);
    }

    /// <summary>
    /// A crossroads at the origin with a straight gallery running four tiles east of
    /// it, and a bend just south of it that turns east again.
    /// </summary>
    private static TempleBoard Gallery()
    {
        var board = new TempleBoard();
        board.PlaceFixed(Origin, Tile(Sides.All));

        for (var column = 1; column <= 3; column++)
        {
            board.PlaceFixed(new Cell(column, 0), Tile(Sides.East | Sides.West));
        }

        board.PlaceFixed(new Cell(4, 0), Tile(Sides.West));
        board.PlaceFixed(new Cell(0, 1), Tile(Sides.North | Sides.East));
        board.PlaceFixed(new Cell(1, 1), Tile(Sides.West));

        return board;
    }

    [Fact]
    public void TheOriginIsSeenFirstThenEachLineNearestFirst()
    {
        var seen = Gallery().VisibleFrom(Origin, range: 3).ToList();

        Assert.Equal([Origin, new(1, 0), new(2, 0), new(3, 0), new(0, 1)], seen);
    }

    [Fact]
    public void NothingBeyondTheRangeIsSeen()
    {
        var seen = Gallery().VisibleFrom(Origin, range: 2).ToList();

        Assert.Contains(new Cell(2, 0), seen);
        Assert.DoesNotContain(new Cell(3, 0), seen);
        Assert.Equal([Origin], Gallery().VisibleFrom(Origin, range: 0));
    }

    [Fact]
    public void ALineDoesNotTurnACorner()
    {
        // Reachable in two steps through the bend, but not in a straight line.
        Assert.DoesNotContain(new Cell(1, 1), Gallery().VisibleFrom(Origin, range: 3));
    }

    [Fact]
    public void AWallStopsTheLine()
    {
        var board = Gallery();

        // The origin opens north onto it, but its own south side is rock.
        board.PlaceFixed(new Cell(0, -1), Tile(Sides.East));

        Assert.DoesNotContain(new Cell(0, -1), board.VisibleFrom(Origin, range: 3));
    }

    [Fact]
    public void ADemolishedWallLetsTheLineThrough()
    {
        var board = Gallery();
        board.PlaceFixed(new Cell(0, -1), Tile(Sides.East));
        board.Demolish(Origin, new Cell(0, -1));

        Assert.Contains(new Cell(0, -1), board.VisibleFrom(Origin, range: 3));
    }

    [Fact]
    public void AnOpaqueTileIsNotSeenAndHidesWhatLiesBeyond()
    {
        var buried = new Cell(2, 0);
        var seen = Gallery().VisibleFrom(Origin, range: 4, opaque: cell => cell == buried).ToList();

        Assert.Contains(new Cell(1, 0), seen);
        Assert.DoesNotContain(buried, seen);
        Assert.DoesNotContain(new Cell(3, 0), seen);
    }

    [Fact]
    public void WhoeverStandsOnAnOpaqueTileStillSeesItAndOutOfIt()
    {
        var seen = Gallery().VisibleFrom(Origin, range: 1, opaque: cell => cell == Origin).ToList();

        Assert.Equal([Origin, new(1, 0), new(0, 1)], seen);
    }

    [Fact]
    public void FromTheEntranceTheLateralsAreSeenButNotBuriedRuins()
    {
        var start = TempleSetup.EntranceCrossing;
        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Guide", 7, start)],
            TempleSetup.CreateBoard(),
            new TileBag([new TileDefinition("Ruins-test", TileKind.Ruins, TileShape.Crossroads)]),
            new Rng(1));

        game.Play(new Reveal(Direction.South));
        var ruins = new Cell(start.Column, 1);
        Assert.Contains(ruins, game.Rubble);

        Assert.Equal(
            [start, TempleSetup.EntranceExit, new(4, 0), new(5, 0), new(2, 0), new(1, 0)],
            game.VisibleFrom(start, range: 2));
    }
}
