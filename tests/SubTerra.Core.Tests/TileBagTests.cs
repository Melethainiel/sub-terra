using SubTerra.Core.Randomness;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class TileBagTests
{
    private static List<string> DrawAll(ulong seed)
    {
        var bag = TileBag.Temple();
        var rng = new Rng(seed);
        return [.. Enumerable.Range(0, bag.Count).Select(_ => bag.Draw(rng).Id)];
    }

    [Fact]
    public void ANewBagHoldsEveryDictatedTile()
    {
        Assert.Equal(30, TileBag.Temple().Count);
        Assert.False(TileBag.Temple().IsEmpty);
    }

    [Fact]
    public void DrawingEmptiesTheBagExactlyOnce()
    {
        var bag = TileBag.Temple();
        var rng = new Rng(1);
        var drawn = new List<string>();

        while (!bag.IsEmpty)
        {
            drawn.Add(bag.Draw(rng).Id);
        }

        Assert.Equal(0, bag.Count);
        Assert.Equal(30, drawn.Count);
        Assert.Equal(
            TileCatalog.CreateBag().Select(tile => tile.Id).Order(),
            drawn.Order());
    }

    [Fact]
    public void DrawingFromAnEmptyBagIsRefused()
    {
        var bag = new TileBag([]);
        Assert.Throws<InvalidOperationException>(() => bag.Draw(new Rng(1)));
    }

    [Fact]
    public void TheSameSeedDealsTheSameTiles()
    {
        Assert.Equal(DrawAll(seed: 20260822), DrawAll(seed: 20260822));
    }

    [Fact]
    public void DifferentSeedsDealDifferentTiles()
    {
        Assert.NotEqual(DrawAll(seed: 1), DrawAll(seed: 2));
    }

    [Fact]
    public void AReturnedTileCanComeBackOut()
    {
        // The Archaeologist keeps one of two tiles and drops the other back in.
        var bag = TileBag.Temple();
        var rng = new Rng(5);

        var kept = bag.Draw(rng);
        var returned = bag.Draw(rng);
        Assert.Equal(28, bag.Count);

        bag.Return(returned);
        Assert.Equal(29, bag.Count);
        Assert.Contains(returned, bag.Remaining);
        Assert.DoesNotContain(kept, bag.Remaining);
    }

    [Fact]
    public void TheDealIsStableAcrossVersions()
    {
        // Guards the generator itself: changing it silently would break every saved
        // game and every replay. Update this only on a deliberate change.
        Assert.Equal(
            ["Lava-2", "Bridge-1", "Lava-3", "Normal-1", "Ruins-1"],
            DrawAll(seed: 42).Take(5));
    }
}
