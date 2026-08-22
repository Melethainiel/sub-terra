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

## Les tuiles

Les effectifs par type viennent du livret ; **les tracés de couloirs sont les
nôtres**. Le livret ne les imprime pas — c'est de l'illustration — et ce portage
est une version 3D, pas un fac-similé. `TileShape` en définit quatre : croisement,
T, couloir, coude. `TileCatalog` les répartit sur les 30 tuiles avec un peu moins
de trois ouvertures par tuile en moyenne, de sorte que le temple se lise comme un
labyrinthe plutôt que comme une esplanade. Des tests verrouillent les effectifs
par type et cette densité d'ouvertures.

## Le rendu des tuiles

`BoardView` assemble aujourd'hui des primitives : dalle de sol, murs épais de
roche, et du mobilier par type — la lave est sa propre source de lumière, les
Gardiens et les Clés portent un sceau émissif, les Ruines un tas d'éboulis
dispersé depuis les coordonnées de la case pour rester stable d'un rendu à
l'autre.

Quand des tuiles modélisées arriveront, la couture à remplacer est `AddTile` :
instancier une scène par `TileKind` au lieu d'assembler des boîtes, et garder le
reste. Le dispersement décoratif utilise son propre générateur, volontairement
séparé du `Rng` du moteur : décorer ne doit jamais consommer des jets dont la
partie dépend.
