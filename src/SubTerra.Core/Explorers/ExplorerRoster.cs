namespace SubTerra.Core.Explorers;

/// <summary>
/// The ten Explorers, straight off their cards. Their abilities are carried as text
/// and are not yet played by the engine — but a party is chosen from these, and the
/// choice is already worth making for the hearts and the domains.
/// </summary>
public static class ExplorerRoster
{
    /// <summary>The rulebook's minimum: solo and duo players run three of them.</summary>
    public const int SmallestParty = 3;

    /// <summary>Six meeples in the box.</summary>
    public const int LargestParty = 6;

    public static readonly IReadOnlyList<ExplorerSheet> All =
    [
        new("archeologue", "L'Archéologue", 5, Domain.Scout,
            new Ability("erudite", "Érudite", "passive",
                "Au lieu de piocher 1 tuile, en piocher 2, en choisir une, remettre l'autre dans le sac."),
            new Ability("aventuriere", "Aventurière", "passive",
                "À son tour, dépenser 1 ♥ pour relancer n'importe quel dé.")),

        new("guide", "Le Guide", 3, Domain.Scout,
            new Ability("agile", "Agile", "passive",
                "Lors de Se déplacer, Courir ou Explorer, ignorer les marqueurs Éboulis."),
            new Ability("illuminer", "Illuminer", "1 PA",
                "Effectuer deux fois l'action Révéler.")),

        new("gredin", "Le Gredin", 3, Domain.Scout,
            new Ability("sprinter", "Sprinter", "1 PA",
                "Effectuer deux fois Se déplacer."),
            new Ability("vigilance", "Vigilance", "passive",
                "Lui et les Explorateurs de sa tuile ne peuvent ni déclencher de piège ni perdre de ♥ du fait d'un piège.")),

        new("aristocrate", "L'Aristocrate", 5, Domain.Connector,
            new Ability("ordonner", "Ordonner", "1 PA",
                "Un Explorateur pas à terre effectue immédiatement une action Se déplacer."),
            new Ability("rechercher", "Rechercher", "2 PA, ×3",
                "Placer une tuile Journal à côté de n'importe quelle tuile du Temple, en connexion.")),

        new("contremaitre", "Le Contremaître", 5, Domain.Connector,
            new Ability("excaver", "Excaver", "1 PA",
                "Effectuer une action Creuser."),
            new Ability("consolider", "Consolider", "1 PA, ×4",
                "Poser un marqueur Consolidation sur sa tuile : elle devient une tuile Normale jusqu'à la fin de la partie.")),

        new("tireuse", "La Tireuse d'élite", 5, Domain.Defender,
            new Ability("lunette", "Lunette de visée", "1 PA",
                "Révéler une tuile visible en ligne droite à 3 tuiles ou moins."),
            new Ability("tir-de-precision", "Tir de précision", "1 PA",
                "Éliminer un ennemi visible en ligne droite à 3 tuiles ou moins, mais pas sur sa propre tuile.")),

        new("sapeur", "Le Sapeur", 5, Domain.Defender,
            new Ability("grenade", "Grenade", "1 PA",
                "Éliminer tous les ennemis d'une tuile adjacente et connectée (pas la sienne) ; les Explorateurs de cette tuile perdent 1 ♥."),
            new Ability("demolir", "Démolir", "1 PA, ×3",
                "Détruire un mur adjacent, y poser un marqueur Démolition.")),

        new("combattante", "La Combattante chevronnée", 7, Domain.Defender,
            new Ability("aneantir", "Anéantir", "1 PA",
                "Éliminer un ennemi de sa tuile."),
            new Ability("se-preparer", "Se préparer", "1 PA",
                "Ne peut plus perdre de ♥ jusqu'au début de son prochain tour ; capacité indisponible au tour suivant.")),

        new("guerisseuse", "La Guérisseuse", 3, Domain.Support,
            new Ability("guerir", "Guérir", "1 PA",
                "Un autre Explorateur visible à 2 tuiles ou moins regagne 2 ♥."),
            new Ability("survivante", "Survivante", "passive",
                "Sur un symbole Trébucher, regagner 1 ♥ au lieu de l'effet habituel.")),

        new("pretre", "Le Prêtre", 5, Domain.Support,
            new Ability("ranimer", "Ranimer", "3 PA",
                "Un autre Explorateur récupère 1 ♥ s'il est à terre, 3 ♥ sinon."),
            new Ability("purifier", "Purifier", "3 PA",
                "Éliminer tous les ennemis d'une tuile autre que la sienne.")),
    ];

    public static ExplorerSheet? Find(string id) =>
        All.FirstOrDefault(sheet => sheet.Id == id);
}
