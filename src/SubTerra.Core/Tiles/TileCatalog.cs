using SubTerra.Core.Board;

namespace SubTerra.Core.Tiles;

/// <summary>The printed tiles: what goes in the bag, and what stays out of it.</summary>
public static class TileCatalog
{
    /// <summary>
    /// PROVISIONAL. The rulebook never prints the wall layout of each tile — it is
    /// artwork, not rules — so every tile is currently a four-way crossroads.
    /// Replace with the real layouts read off the components: docs/regles.md §13.1.
    /// </summary>
    private const Sides ProvisionalOpenSides = Sides.All;

    /// <summary>The thirty Temple tiles that start in the bag.</summary>
    public static readonly IReadOnlyList<(TileKind Kind, int Count)> BagComposition =
    [
        (TileKind.Normal, 3),
        (TileKind.Bridge, 2),
        (TileKind.Key, 3),
        (TileKind.Lava, 5),
        (TileKind.SpikeTrap, 3),
        (TileKind.DartTrap, 4),
        (TileKind.Ruins, 6),
        (TileKind.Guardian, 4),
    ];

    public const int JournalTileCount = 3;

    /// <summary>
    /// The thirty tiles of the bag, in a fixed order. Shuffling is the caller's job,
    /// so that a game is reproducible from its seed.
    /// </summary>
    public static IReadOnlyList<TileDefinition> CreateBag()
    {
        var tiles = new List<TileDefinition>();

        foreach (var (kind, count) in BagComposition)
        {
            for (var index = 1; index <= count; index++)
            {
                // Each Ruins tile carries the die face that brings it down.
                int? ruinsNumber = kind == TileKind.Ruins ? index : null;
                tiles.Add(new TileDefinition($"{kind}-{index}", kind, ProvisionalOpenSides, ruinsNumber));
            }
        }

        return tiles;
    }

    /// <summary>Set aside at setup; only the Aristocrat's Research ability plays them.</summary>
    public static IReadOnlyList<TileDefinition> CreateJournalTiles() =>
        [.. Enumerable.Range(1, JournalTileCount)
            .Select(index => new TileDefinition($"Journal-{index}", TileKind.Journal, ProvisionalOpenSides))];

    public static TileDefinition Sanctuary { get; } =
        new("Sanctuary", TileKind.Sanctuary, ProvisionalOpenSides);
}
