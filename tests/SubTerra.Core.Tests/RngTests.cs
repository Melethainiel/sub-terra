using SubTerra.Core.Randomness;

namespace SubTerra.Core.Tests;

public class RngTests
{
    [Fact]
    public void TheSameSeedReplaysTheSameSequence()
    {
        var a = new Rng(1234);
        var b = new Rng(1234);
        Assert.Equal(
            Enumerable.Range(0, 50).Select(_ => a.RollDie()),
            Enumerable.Range(0, 50).Select(_ => b.RollDie()));
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var a = new Rng(1);
        var b = new Rng(2);
        Assert.NotEqual(
            Enumerable.Range(0, 20).Select(_ => a.RollDie()),
            Enumerable.Range(0, 20).Select(_ => b.RollDie()));
    }

    [Fact]
    public void AGameCanResumeFromAStoredState()
    {
        var rng = new Rng(99);
        Enumerable.Range(0, 7).ToList().ForEach(_ => rng.RollDie());

        var resumed = Rng.FromState(rng.State);

        Assert.Equal(
            Enumerable.Range(0, 20).Select(_ => rng.RollDie()),
            Enumerable.Range(0, 20).Select(_ => resumed.RollDie()));
    }

    [Fact]
    public void NextStaysWithinItsBound()
    {
        var rng = new Rng(7);
        Assert.All(
            Enumerable.Range(0, 2000).Select(_ => rng.Next(5)),
            value => Assert.InRange(value, 0, 4));
    }

    [Fact]
    public void NextRejectsAnEmptyRange()
    {
        var rng = new Rng(7);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(-3));
    }

    [Fact]
    public void TheDieIsRoughlyFair()
    {
        var rng = new Rng(2026);
        var counts = new int[7];

        for (var roll = 0; roll < 60_000; roll++)
        {
            counts[rng.RollDie()]++;
        }

        Assert.Equal(0, counts[0]);

        // 10 000 expected per face; a fair generator lands well inside ±5%.
        Assert.All(counts[1..], count => Assert.InRange(count, 9_500, 10_500));
    }
}
