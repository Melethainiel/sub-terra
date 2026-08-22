namespace SubTerra.Core.Tiles;

/// <summary>The nine Temple tile types, plus the multi-cell special pieces.</summary>
public enum TileKind
{
    Normal,
    Bridge,
    Key,
    Lava,
    SpikeTrap,
    DartTrap,
    Ruins,
    Guardian,

    /// <summary>Set aside at setup; only the Aristocrat brings these into play.</summary>
    Journal,

    Entrance,
    Lateral,
    Sanctuary,
}
