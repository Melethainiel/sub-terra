namespace SubTerra.Core.Game;

public enum Difficulty
{
    Beginner,
    Normal,
    Advanced,
    Expert,
}

/// <summary>
/// How many rounds the mountain gives you. Fewer explorers means fewer turns per
/// round, so a small party is granted a longer fuse.
/// </summary>
public static class EruptionTrack
{
    private static readonly Dictionary<Difficulty, int[]> Starts = new()
    {
        // Indexed by party size, from three explorers up to six.
        [Difficulty.Beginner] = [27, 24, 22, 20],
        [Difficulty.Normal] = [26, 21, 19, 17],
        [Difficulty.Advanced] = [22, 18, 16, 14],
        [Difficulty.Expert] = [20, 16, 14, 12],
    };

    public const int SmallestParty = 3;

    public const int LargestParty = 6;

    /// <summary>
    /// Where the eruption marker starts. Parties smaller than three are read off the
    /// three-explorer column: the rules never let you control fewer than that.
    /// </summary>
    public static int StartingCount(Difficulty difficulty, int explorers)
    {
        var party = Math.Clamp(explorers, SmallestParty, LargestParty);
        return Starts[difficulty][party - SmallestParty];
    }
}
