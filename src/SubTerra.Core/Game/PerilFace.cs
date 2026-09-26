namespace SubTerra.Core.Game;

/// <summary>
/// The six faces of the Peril die, rolled at the end of every player's turn. The
/// temple answers each turn the explorers take.
/// </summary>
public enum PerilFace
{
    /// <summary>Costs another heart to whoever pushed themselves this turn.</summary>
    Stumble,

    /// <summary>Every explorer standing in lava is burned.</summary>
    Lava,

    /// <summary>A numbered ruin may come down.</summary>
    Collapse,

    /// <summary>Springs the traps around the active explorer.</summary>
    Trap,

    /// <summary>A guardian appears on the nearest guardian tile.</summary>
    WakeGuardian,

    /// <summary>Every guardian in the temple takes a step.</summary>
    ActivateGuardians,
}

public static class PerilFaceExtensions
{
    /// <summary>The symbol's name as the rulebook prints it, for a prompt.</summary>
    public static string Name(this PerilFace face) => face switch
    {
        PerilFace.Stumble => "Trébucher",
        PerilFace.Lava => "Lave",
        PerilFace.Collapse => "Effondrement",
        PerilFace.Trap => "Piège",
        PerilFace.WakeGuardian => "Réveiller un Gardien",
        PerilFace.ActivateGuardians => "Activer les Gardiens",
        _ => face.ToString(),
    };
}
