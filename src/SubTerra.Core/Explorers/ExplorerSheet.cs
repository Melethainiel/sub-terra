namespace SubTerra.Core.Explorers;

/// <summary>
/// What an Explorer is good at. The rulebook calls these "domaines de prédilection"
/// and prints an icon for each; a party is meant to spread across them.
/// </summary>
public enum Domain
{
    /// <summary>Éclaireur — sees further, moves further.</summary>
    Scout,

    /// <summary>Connecteur — bends the temple itself.</summary>
    Connector,

    /// <summary>Défenseur — deals with the Ashen Legion.</summary>
    Defender,

    /// <summary>Appui — keeps everyone else standing.</summary>
    Support,
}

/// <summary>
/// One of the two things an Explorer can do that nobody else can.
/// </summary>
/// <param name="Id">
/// A stable name for the day the ability is wired into the engine. Nothing reads it
/// yet — the sheets carry their abilities as text so the choice at least means
/// something at the table.
/// </param>
/// <param name="Cost">What it costs, as printed: "passive", "1 PA", "2 PA, ×3".</param>
public sealed record Ability(string Id, string Name, string Cost, string Text)
{
    /// <summary>A passive ability is always on and costs no action.</summary>
    public bool IsPassive => Cost.StartsWith("passive", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// An Explorer's printed sheet: who they are before the temple gets to them. The
/// <see cref="Game.Explorer"/> is the meeple on the board; this is the card.
/// </summary>
public sealed record ExplorerSheet(
    string Id,
    string Name,
    int MaxHealth,
    Domain Domain,
    Ability First,
    Ability Second);
