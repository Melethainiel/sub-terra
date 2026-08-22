using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class TileCatalogTests
{
    /// <summary>
    /// The tile set dictated for this port, in docs/tuiles.md. Locked here because it
    /// is a decision, not a deduction: nothing in the rulebook or the code implies it.
    /// </summary>
    public static readonly TheoryData<TileKind, TileShape, int> DictatedComposition = new()
    {
        { TileKind.Normal, TileShape.Junction, 3 },
        { TileKind.Bridge, TileShape.Corridor, 2 },
        { TileKind.Key, TileShape.DeadEnd, 3 },
        { TileKind.DartTrap, TileShape.Corner, 4 },
        { TileKind.Lava, TileShape.Junction, 2 },
        { TileKind.Lava, TileShape.Crossroads, 3 },
        { TileKind.SpikeTrap, TileShape.Crossroads, 3 },
        { TileKind.Ruins, TileShape.Junction, 3 },
        { TileKind.Ruins, TileShape.Crossroads, 3 },
        { TileKind.Guardian, TileShape.DeadEnd, 2 },
        { TileKind.Guardian, TileShape.Corner, 2 },
    };

    [Theory]
    [MemberData(nameof(DictatedComposition))]
    public void TheBagHoldsTheDictatedCopiesOfEachTile(TileKind kind, TileShape shape, int expected)
    {
        var copies = TileCatalog.CreateBag()
            .Count(tile => tile.Kind == kind && tile.Shape == shape);

        Assert.Equal(expected, copies);
    }

    [Fact]
    public void TheBagHoldsNothingBeyondTheDictatedTiles()
    {
        var dictated = DictatedComposition
            .Select(row => ((TileKind)row[0], (TileShape)row[1]))
            .ToHashSet();

        Assert.All(
            TileCatalog.CreateBag(),
            tile => Assert.Contains((tile.Kind, tile.Shape), dictated));

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
    public void TileIdsAreUnique()
    {
        var ids = TileCatalog.CreateBag()
            .Concat(TileCatalog.CreateJournalTiles())
            .Select(tile => tile.Id)
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void KeysAndGuardiansSitAtTheEndOfADeadEnd()
    {
        // Deliberate: reaching a Key or waking a Guardian means walking into a
        // pocket with no way on. Nothing else in the bag is a dead end.
        var deadEnds = TileCatalog.CreateBag()
            .Where(tile => tile.OpenSides.OpeningCount() == 1)
            .Select(tile => tile.Kind)
            .Distinct()
            .Order();

        Assert.Equal([TileKind.Key, TileKind.Guardian], deadEnds);
    }
}
