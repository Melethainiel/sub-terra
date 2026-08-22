# Architecture

## Stack

| | |
|---|---|
| Moteur | Godot 4.7.2 (.NET / mono) |
| Langage | C# — `net8.0` |
| Rendu | 3D, caméra en vue plongeante sur le temple |
| Réseau | Multiplayer haut niveau de Godot (ENet + RPC), **hôte autoritaire** |
| Tests | xUnit, sans dépendance à Godot |

## Découpage en projets

```
SubTerra.sln
├── SubTerra.csproj              projet Godot — scènes, rendu 3D, entrées, réseau
├── src/SubTerra.Core/           moteur de règles pur, aucune référence à Godot
└── tests/SubTerra.Core.Tests/   xUnit sur le moteur de règles
```

`SubTerra.Core` ne référence **jamais** `GodotSharp`. C'est ce qui garantit que
les règles restent testables en une seconde sans lancer le moteur, et
rejouables à l'identique. Le projet Godot le glob-exclut explicitement
(`<Compile Remove="src/**/*.cs" />`) et y accède par `ProjectReference`.

## Le moteur de règles

Trois principes, dont découle tout le reste :

**Déterminisme.** `GameState` + une commande + une graine de RNG donnent
toujours le même `GameState`. Aucun appel à `Random.Shared`, aucune horloge :
l'aléatoire passe par un `IDiceRoller` explicite porté par l'état. Une partie se
résume à sa graine et à sa liste de commandes.

**Commandes et événements.** Le joueur émet une `GameCommand` (`Move`,
`Reveal`, `UseAbility`…). Le moteur la valide, l'applique, et produit une liste
d'`GameEvent` décrivant ce qui s'est passé. La présentation ne lit pas l'état
pour deviner ce qui a changé : elle rejoue les événements en animations.

**Ignorance de Godot.** Aucun `Node`, `Vector3` ni `Resource` dans `Core`. Les
coordonnées sont des `Cell(int Column, int Row)` ; la conversion vers l'espace
3D appartient à la couche de présentation.

## Le réseau

L'hôte détient le seul `GameState` faisant foi. Les clients envoient des
commandes, l'hôte valide et diffuse les événements résultants. Un jeu au tour
par tour n'a besoin ni de prédiction ni de rollback : la latence est invisible
derrière les animations.

Conséquence sur le moteur : la validation des commandes doit être exhaustive
côté hôte, jamais déléguée à l'UI. L'UI grise les actions impossibles pour le
confort ; l'hôte les refuse pour la correction.

Le Chef d'Expédition tranche les nombreux choix ambigus des règles (cible d'une
attaque de Gardien, direction d'un déplacement, emplacement du Sanctuaire). Ces
choix deviennent des commandes explicites adressées à un joueur désigné, pas des
résolutions automatiques : c'est un point de conception à ne pas court-circuiter.

## Arborescence Godot

```
scenes/app/      point d'entrée, menus, lobby réseau
scenes/board/    plateau 3D, tuiles, meeples, caméra
scenes/ui/       HUD, fiches d'Explorateur, plateau Volcan
scripts/App/     amorçage, cycle de vie de la partie
scripts/Net/     transport, RPC, synchronisation
scripts/Presentation/  lecture des GameEvent → animations
resources/       .tres de données (fiches, catalogue de tuiles)
assets/          modèles, textures, sons
```
