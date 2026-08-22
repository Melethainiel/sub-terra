namespace SubTerra.Core.Randomness;

/// <summary>
/// The only source of chance in the game. Seeded, and reproducible across machines
/// and .NET versions — <see cref="System.Random"/> guarantees neither, and a replayed
/// game that diverges is worse than no replay at all.
/// </summary>
/// <remarks>
/// splitmix64: one word of state, easy to serialise into a save file or a network
/// packet, and far better than the dice of this game will ever need.
/// </remarks>
public sealed class Rng
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    private ulong _state;

    public Rng(ulong seed) => _state = seed;

    /// <summary>The whole generator. Store it to resume a game exactly where it was.</summary>
    public ulong State => _state;

    public static Rng FromState(ulong state) => new(state);

    /// <summary>A uniform value in [0, <paramref name="exclusiveUpperBound"/>).</summary>
    public int Next(int exclusiveUpperBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveUpperBound);

        var bound = (ulong)exclusiveUpperBound;

        // Reject the ragged tail so every value is equally likely, rather than the
        // low ones coming up marginally more often.
        var threshold = (ulong.MaxValue - bound + 1) % bound;

        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value < threshold);

        return (int)(value % bound);
    }

    /// <summary>Rolls the plain d6 used by Attack, Spike Traps and Collapse.</summary>
    public int RollDie(int sides = 6) => Next(sides) + 1;

    /// <summary>Rolls the Peril die.</summary>
    public Game.PerilFace RollPeril() => (Game.PerilFace)Next(6);

    private ulong NextUInt64()
    {
        var z = _state += GoldenGamma;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
