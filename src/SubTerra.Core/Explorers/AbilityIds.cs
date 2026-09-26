namespace SubTerra.Core.Explorers;

/// <summary>
/// The stable names of the abilities the engine plays, as printed in
/// <see cref="ExplorerRoster"/>. A <see cref="Game.UseAbility"/> names one of these.
/// </summary>
public static class AbilityIds
{
    /// <summary>La Guérisseuse — another Explorer in sight, 2 tiles or less, regains 2 ♥.</summary>
    public const string Guerir = "guerir";

    /// <summary>Le Prêtre — another Explorer in sight regains 1 ♥ if down, 3 otherwise.</summary>
    public const string Ranimer = "ranimer";
}
