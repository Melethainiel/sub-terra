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

    /// <summary>Le Guide, 1 PA — Révéler twice.</summary>
    public const string Illuminer = "illuminer";

    /// <summary>Le Gredin, 1 PA — Se déplacer twice.</summary>
    public const string Sprinter = "sprinter";

    /// <summary>Le Contremaître, 1 PA — Creuser.</summary>
    public const string Excaver = "excaver";

    /// <summary>L'Aristocrate, 1 PA — another standing Explorer takes a step there and then.</summary>
    public const string Ordonner = "ordonner";

    /// <summary>Le Contremaître, 1 PA, ×4 — his tile becomes a Normal one for good.</summary>
    public const string Consolider = "consolider";

    /// <summary>La Combattante, 1 PA — an enemy on her tile is eliminated, no roll.</summary>
    public const string Aneantir = "aneantir";

    /// <summary>La Combattante, 1 PA — no heart lost until her next turn, which cannot use it again.</summary>
    public const string SePreparer = "se-preparer";

    /// <summary>L'Archéologue, passive — draws two tiles, keeps one, puts the other back.</summary>
    public const string Erudite = "erudite";

    /// <summary>L'Aristocrate, 2 PA, ×3 — lays a Journal tile against any tile of the Temple.</summary>
    public const string Rechercher = "rechercher";

    /// <summary>La Tireuse d'élite, 1 PA — reveals a tile in sight, 3 tiles or less away.</summary>
    public const string Lunette = "lunette";

    /// <summary>La Tireuse d'élite, 1 PA — eliminates an enemy in sight, 3 tiles or less, not on her tile.</summary>
    public const string TirDePrecision = "tir-de-precision";

    /// <summary>Le Sapeur, 1 PA — clears the enemies off a neighbouring connected tile; those there lose 1 ♥.</summary>
    public const string Grenade = "grenade";

    /// <summary>Le Sapeur, 1 PA, ×3 — knocks down a wall beside him.</summary>
    public const string Demolir = "demolir";

    /// <summary>Le Prêtre, 3 PA — clears the enemies off any tile but his own.</summary>
    public const string Purifier = "purifier";

    /// <summary>La Guérisseuse — another Explorer in sight, 2 tiles or less, regains 2 ♥.</summary>
    public const string Guerir = "guerir";

    /// <summary>Le Prêtre — another Explorer in sight regains 1 ♥ if down, 3 otherwise.</summary>
    public const string Ranimer = "ranimer";
}
