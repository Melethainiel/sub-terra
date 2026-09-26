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
    Sanctuary,
}

public static class TileKindExtensions
{
    /// <summary>The tile's name at the table, for a prompt.</summary>
    public static string Name(this TileKind kind) => kind switch
    {
        TileKind.Normal => "Normale",
        TileKind.Bridge => "Pont",
        TileKind.Key => "Clé",
        TileKind.Lava => "Lave",
        TileKind.SpikeTrap => "Piège à pics",
        TileKind.DartTrap => "Piège à fléchettes",
        TileKind.Ruins => "Ruines",
        TileKind.Guardian => "Gardien",
        TileKind.Journal => "Journal",
        TileKind.Entrance => "Entrée",
        TileKind.Sanctuary => "Sanctuaire",
        _ => kind.ToString(),
    };
}
