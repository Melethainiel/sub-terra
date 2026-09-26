namespace SubTerra.Core.Explorers;

/// <summary>
/// The stable names of the abilities the engine plays, as printed in
/// <see cref="ExplorerRoster"/>. A <see cref="Game.UseAbility"/> names one of these.
/// </summary>
public static class AbilityIds
{
    /// <summary>L'Archéologue, passive — on her turn, 1 ♥ rerolls any die.</summary>
    public const string Aventuriere = "aventuriere";

    /// <summary>Le Guide, passive — rubble does not stop him moving, running or exploring.</summary>
    public const string Agile = "agile";

    /// <summary>Le Gredin, passive — neither he nor anyone on his tile sets off or suffers a trap.</summary>
    public const string Vigilance = "vigilance";

    /// <summary>La Guérisseuse, passive — a Stumble gives her a heart back instead.</summary>
    public const string Survivante = "survivante";

    /// <summary>La Guérisseuse — another Explorer in sight, 2 tiles or less, regains 2 ♥.</summary>
    public const string Guerir = "guerir";

    /// <summary>Le Prêtre — another Explorer in sight regains 1 ♥ if down, 3 otherwise.</summary>
    public const string Ranimer = "ranimer";
}
