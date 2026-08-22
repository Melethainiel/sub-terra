using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class TileCatalogTests
{
    [Fact]
    public void TheBagHoldsThirtyTempleTiles()
    {
        Assert.Equal(30, TileCatalog.CreateBag().Count);
    }

    [Fact]
    public void JournalTilesStayOutOfTheBag()
    {
        Assert.DoesNotContain(TileCatalog.CreateBag(), tile => tile.Kind == TileKind.Journal);
        Assert.Equal(3, TileCatalog.CreateJournalTiles().Count);
    }

    [Fact]
    public void EachRuinsTileCarriesADistinctDieFace()
    {
        var ruinsNumbers = TileCatalog.CreateBag()
            .Where(tile => tile.Kind == TileKind.Ruins)
            .Select(tile => tile.RuinsNumber)
            .ToList();

        Assert.Equal(6, ruinsNumbers.Count);
        Assert.Equal(ruinsNumbers.Count, ruinsNumbers.Distinct().Count());
        Assert.All(ruinsNumbers, number => Assert.InRange(number!.Value, 1, 6));
    }

    [Fact]
    public void OnlyRuinsTilesCarryADieFace()
    {
        Assert.All(
            TileCatalog.CreateBag().Where(tile => tile.Kind != TileKind.Ruins),
            tile => Assert.Null(tile.RuinsNumber));
    }

    [Fact]
    public void EveryTileHasAtLeastTwoWaysOut()
    {
        // A dead end drawn late could seal the Temple; the rulebook has a rule for it,
        // but our own tile designs need not court it.
        Assert.All(
            TileCatalog.CreateBag().Concat(TileCatalog.CreateJournalTiles()),
            tile => Assert.True(
                tile.OpenSides.OpeningCount() >= 2,
                $"{tile.Id} has fewer than two open sides."));
    }

    [Fact]
    public void CrossroadsStayScarceSoTheTempleReadsAsAMaze()
    {
        var bag = TileCatalog.CreateBag();
        var crossroads = bag.Count(tile => tile.OpenSides == Sides.All);
        var openings = bag.Sum(tile => tile.OpenSides.OpeningCount());

        Assert.InRange(crossroads, 1, 8);
        Assert.InRange(openings / (double)bag.Count, 2.0, 3.0);
    }

    [Fact]
    public void EveryKindKeepsItsRulebookCount()
    {
        var counts = TileCatalog.CreateBag()
            .GroupBy(tile => tile.Kind)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(
            new Dictionary<TileKind, int>
            {
                [TileKind.Normal] = 3,
                [TileKind.Bridge] = 2,
                [TileKind.Key] = 3,
                [TileKind.Lava] = 5,
                [TileKind.SpikeTrap] = 3,
                [TileKind.DartTrap] = 4,
                [TileKind.Ruins] = 6,
                [TileKind.Guardian] = 4,
            },
            counts);
    }

    [Fact]
    public void TileIdsAreUnique()
    {
        var ids = TileCatalog.CreateBag().Concat(TileCatalog.CreateJournalTiles())
            .Select(tile => tile.Id)
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
