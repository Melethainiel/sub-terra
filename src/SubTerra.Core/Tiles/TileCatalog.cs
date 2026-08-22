namespace SubTerra.Core.Tiles;

/// <summary>The printed tiles: what goes in the bag, and what stays out of it.</summary>
public static class TileCatalog
{
    /// <summary>
    /// The tiles that start in the bag: kind, carved shape, and how many copies.
    /// Dictated for this port — see docs/tuiles.md — not derived from the rulebook.
    /// </summary>
    private static readonly (TileKind Kind, TileShape Shape, int Count)[] Composition =
    [
        (TileKind.Normal, TileShape.Junction, 3),
        (TileKind.Bridge, TileShape.Corridor, 2),
        (TileKind.Key, TileShape.DeadEnd, 3),
        (TileKind.DartTrap, TileShape.Corner, 4),
        (TileKind.Lava, TileShape.Junction, 2),
        (TileKind.Lava, TileShape.Crossroads, 3),
        (TileKind.SpikeTrap, TileShape.Crossroads, 3),
        (TileKind.Ruins, TileShape.Junction, 3),
        (TileKind.Ruins, TileShape.Crossroads, 3),
        (TileKind.Guardian, TileShape.DeadEnd, 2),
        (TileKind.Guardian, TileShape.Corner, 2),
    ];

    private static readonly (TileShape Shape, int Count)[] JournalComposition =
    [
        (TileShape.Junction, 2),
        (TileShape.Crossroads, 1),
    ];

    public const int JournalTileCount = 3;

    /// <summary>
    /// The thirty tiles of the bag, in a fixed order. Shuffling is the caller's job,
    /// so that a game is reproducible from its seed.
    /// </summary>
    public static IReadOnlyList<TileDefinition> CreateBag()
    {
        var tiles = new List<TileDefinition>();
        var numbering = new Dictionary<TileKind, int>();

        foreach (var (kind, shape, count) in Composition)
        {
            for (var copy = 0; copy < count; copy++)
            {
                var index = numbering.GetValueOrDefault(kind) + 1;
                numbering[kind] = index;

                // Each Ruins tile carries the die face that brings it down.
                int? ruinsNumber = kind == TileKind.Ruins ? index : null;

                tiles.Add(new TileDefinition($"{kind}-{index}", kind, shape, ruinsNumber));
            }
        }

        return tiles;
    }

    /// <summary>Set aside at setup; only the Aristocrat's Research ability plays them.</summary>
    public static IReadOnlyList<TileDefinition> CreateJournalTiles()
    {
        var tiles = new List<TileDefinition>();

        foreach (var (shape, count) in JournalComposition)
        {
            for (var copy = 0; copy < count; copy++)
            {
                tiles.Add(new TileDefinition(
                    $"{TileKind.Journal}-{tiles.Count + 1}",
                    TileKind.Journal,
                    shape));
            }
        }

        return tiles;
    }

    public static TileDefinition Sanctuary { get; } =
        new("Sanctuary", TileKind.Sanctuary, TileShape.Crossroads);
}
