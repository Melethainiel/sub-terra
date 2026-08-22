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

## Le moteur de règles, où il en est

`GameState` est le seul point de passage : une commande entre, des événements
sortent. Une partie se résume à sa graine et à sa liste de commandes, ce qui
rend le rejeu, la sauvegarde et l'hôte autoritaire identiques par construction.

Sont joués : le tour de joueur et ses points d'action, les huit actions de base,
les six faces du dé de Péril, les Gardiens et leurs activations, les éboulis, le
Sanctuaire, l'Artefact, la malédiction, la piste d'Éruption, la coulée de lave
et les conditions de fin.

Ne sont pas joués : les dix Explorateurs et leurs capacités, la tuile Journal.

Deux écarts assumés, notés ici pour ne pas les oublier :

- Le livret confie de nombreux arbitrages au **Chef d'Expédition** — quelle
  cible un Gardien frappe, où va le Sanctuaire quand plusieurs places
  conviennent. Le moteur tranche par l'ordre du plateau, ce qui est
  déterministe mais n'est pas la règle. Ces choix doivent devenir des commandes
  adressées à un joueur désigné.
- Le nombre d'exemplaires des tuiles et leurs tracés viennent de
  `docs/tuiles.md`, dicté, et non du livret.

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

Chaque type de tuile est une scène de `scenes/board/` — `TileLava.tscn`,
`TileGuardian.tscn`… — et les matériaux sont des `.tres` partagés dans
`resources/materials/`. Retoucher la roche, c'est éditer un fichier ; retoucher
une tuile, c'est ouvrir sa scène dans l'éditeur. Rien de visuel ne vit dans le
code.

Ces scènes sont des **placeholders assumés** : des boîtes et des cylindres. Le
jour où un `.glb` sort de Blender, il se dépose dans la scène correspondante,
sans toucher au code.

Chaque scène de tuile porte ses **quatre** murs, nommés `Wall_North` à
`Wall_West`. `BoardView` masque ceux que la tuile ouvre. Rien ne pivote : la
rotation choisie à la pose est déjà encodée dans `PlacedTile.OpenSides`, donc
n'importe quel tracé s'exprime sans tourner le nœud — et une tuile modélisée
n'aura pas besoin de quatre variantes.

Une scène manquante déclenche un avertissement et un repli sur `TileNormal`
plutôt qu'un trou dans le temple.
