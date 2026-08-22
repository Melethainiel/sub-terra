namespace SubTerra.Core.Tiles;

/// <summary>The printed tiles: what goes in the bag, and what stays out of it.</summary>
public static class TileCatalog
{
    /// <summary>
    /// The thirty Temple tiles that start in the bag, and the passage carved through
    /// each. The counts per kind come from the rulebook; the shapes are our own
    /// design — see <see cref="TileShape"/>.
    /// </summary>
    /// <remarks>
    /// The mix averages a little under three openings per tile, which gives corridors
    /// that branch and occasionally open out, rather than a field of crossroads.
    /// </remarks>
    private static readonly (TileKind Kind, TileShape Shape, int Count)[] Composition =
    [
        (TileKind.Normal, TileShape.Crossroads, 1),
        (TileKind.Normal, TileShape.Junction, 1),
        (TileKind.Normal, TileShape.Corridor, 1),

        // A bridge spans a chasm: you cross it, you do not turn on it.
        (TileKind.Bridge, TileShape.Corridor, 2),

        // Keys must not end up behind a single choke point.
        (TileKind.Key, TileShape.Crossroads, 1),
        (TileKind.Key, TileShape.Junction, 2),

        (TileKind.Lava, TileShape.Corridor, 2),
        (TileKind.Lava, TileShape.Corner, 2),
        (TileKind.Lava, TileShape.Junction, 1),

        (TileKind.SpikeTrap, TileShape.Corridor, 1),
        (TileKind.SpikeTrap, TileShape.Corner, 1),
        (TileKind.SpikeTrap, TileShape.Junction, 1),

        (TileKind.DartTrap, TileShape.Corridor, 2),
        (TileKind.DartTrap, TileShape.Corner, 1),
        (TileKind.DartTrap, TileShape.Junction, 1),

        (TileKind.Ruins, TileShape.Corner, 2),
        (TileKind.Ruins, TileShape.Junction, 2),
        (TileKind.Ruins, TileShape.Corridor, 1),
        (TileKind.Ruins, TileShape.Crossroads, 1),

        // Guardians need room to hunt.
        (TileKind.Guardian, TileShape.Crossroads, 2),
        (TileKind.Guardian, TileShape.Junction, 2),
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

                tiles.Add(new TileDefinition($"{kind}-{index}", kind, shape.OpenSides(), ruinsNumber));
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
                    shape.OpenSides()));
            }
        }

        return tiles;
    }

    public static TileDefinition Sanctuary { get; } =
        new("Sanctuary", TileKind.Sanctuary, TileShape.Crossroads.OpenSides());
}
