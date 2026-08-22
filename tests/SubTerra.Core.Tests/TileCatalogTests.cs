using SubTerra.Core.Setup;
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
}
