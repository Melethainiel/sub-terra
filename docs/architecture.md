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

Ne sont pas jouées : les vingt **capacités** des Explorateurs, la tuile Journal.
Les dix fiches existent (`ExplorerRoster`) avec leurs ♥, leur domaine et le texte
de leurs deux capacités ; on choisit son Explorateur, mais sa capacité ne fait
encore rien. Chaque capacité porte un identifiant stable pour le jour où on la
branche.

Le **Chef d'Expédition** est désigné au début et garde le médaillon toute la
partie ; chaque manche s'ouvre sur lui. C'est lui qui tranche, et il tranche pour
de bon : les ambiguïtés du livret sont devenues des questions posées.

Quand une conséquence rencontre une égalité — deux Explorateurs sur la tuile d'un
Gardien, deux chemins aussi courts, deux places pour le Sanctuaire — le moteur
**suspend le script en cours** et publie un `DecisionRequired`. Plus rien n'est
accepté qu'un `Decide(option)` ; la réponse relance les conséquences exactement où
elles s'étaient arrêtées. Une option unique n'est pas une question et se règle
seule. Le réveil d'un Gardien fait exception au Chef : le livret le confie au
joueur actif, et le moteur suit.

Techniquement, les conséquences d'une commande sont un `IEnumerable<GameEvent>`
paresseux tenu par un `IEnumerator` que `Pump()` déroule. `Ask()` publie la
question et rend la main ; `Decide` remplit la réponse et relance la pompe. Comme
l'arbitrage est une commande, une partie se rejoue toujours à partir de sa graine
et de sa liste de commandes.

Une **Révélation** ne fixe plus l'orientation avant la pioche. On choisit une
issue, la tuile sort du sac, et l'orientation vient après : une préférence
absente vaut « n'importe quel sens qui se raccorde », une préférence exprimée
est honorée ou refusée, jamais silencieusement remplacée. C'est l'ordre réel des
gestes, et c'est l'interface qui a révélé l'erreur.

Deux écarts assumés, notés ici pour ne pas les oublier :

- L'**ordre d'activation** des Gardiens reste celui de leur apparition. Le livret
  laisse au Chef le soin de trancher quand il compte ; ce sera une question de
  plus le jour où ça se verra.
- Le nombre d'exemplaires des tuiles et leurs tracés viennent de
  `docs/tuiles.md`, dicté, et non du livret.

## Le réseau

Chaque machine fait tourner **la même partie**. Rien de l'état ne circule : le
lobby se met d'accord sur une graine, une difficulté et une expédition, et chaque
pair reconstruit le Temple de son côté. Ne voyage ensuite qu'une ligne de texte
par commande (`CommandCodec`), rejouée à l'identique partout. C'est le
déterminisme du moteur qui rend ça possible — c'est même sa raison d'être.

L'hôte reste l'autorité : un client **demande** (`Request`), l'hôte valide avec
son propre `GameState`, refuse en privé (`Refused`) ou diffuse la commande
(`Play`). Une **empreinte** de l'état accompagne chaque diffusion ; un pair qui
n'arrive pas sur le même nombre le dit tout de suite au lieu de dériver en
silence. Elle est calculée à la main en FNV-1a : `HashCode` tire une graine par
processus et ne serait jamais d'accord avec la machine d'à côté — la première
version du réseau s'est fait prendre exactement là.

Personne ne joue avant que tout le monde ait le Temple à l'écran : un client
frappe à la porte (`AtTheTable`) jusqu'à ce que l'hôte ouvre (`Begin`), sinon une
commande envoyée à un pair encore en chargement serait perdue — et une commande
perdue est une divergence.

Qui a le droit de jouer : le siège dont c'est le tour, ou celui du joueur à qui
un arbitrage est adressé (`PendingDecision.Chooser`). L'UI grise ce qui n'est pas
à vous pour le confort ; l'hôte le refuse pour la correction. Un joueur qui
décroche laisse ses Explorateurs à l'hôte plutôt que de bloquer l'expédition.

Pas encore : la reprise en cours de partie, la sauvegarde, le choix du port.

Les arbitrages sont déjà des commandes (`Decide`) adressées à un joueur nommé
(`PendingDecision.Chooser`), et non des résolutions automatiques : côté réseau,
l'hôte n'aura qu'à n'accepter le `Decide` que de ce joueur-là.

## Le lobby

`Lobby.tscn` est la scène de départ. On y joue seul, on héberge ou on rejoint une
adresse, puis chacun prend ses Explorateurs parmi les dix — une carte prise
appartient à son joueur, une carte reprise est rendue. En solo on en prend trois,
qui est le minimum du livret. L'ordre de la liste est l'ordre du tour, et le
premier porte le médaillon.

`Session` est ce que le lobby transmet à la table : l'expédition (fiche + joueur
par siège), la graine, la difficulté, et de quel côté du fil on se trouve.

## L'écran

`AppRoot` détient l'unique `GameState` et redessine tout après chaque commande :
`BoardView` pour les tuiles, `TokenView` pour les meeples, les Gardiens et les
objets, `HighlightView` pour ce qu'un clic ferait, `Hud` pour l'expédition, les
boutons d'action et les arbitrages. Aucune animation — les événements sont
affichés en texte, pas encore rejoués.

Le survol allume les cases : vert on avance, ambre on pose une tuile, gris on
creuse, rouge c'est une réponse attendue. `HighlightView` ne connaît aucune règle
— il demande au moteur (`Steps()`, `Exits()`, `DigTargets()`) et peint la réponse.
Ces requêtes sont un confort, jamais une autorité : toute commande est revalidée
à l'entrée.

Le HUD affiche l'expédition entière — cœurs, actions, objet porté, médaillon —
chaque ligne de la couleur de son meeple. Quand une question tombe, le panneau
d'arbitrage s'ouvre, les actions se grisent, et les cases concernées s'allument :
on répond au bouton ou directement sur le plateau.

Un clic se traduit en commande selon ce qu'il désigne : une tuile posée et
voisine, on avance ; du vide au-delà d'une issue, on explore. Maj+clic révèle
sans entrer, Ctrl+clic creuse. Les actions qui ne visent rien d'autre que sa
propre tuile sont au clavier.

Le clic ne passe par aucun collisionneur : on projette le rayon de la caméra sur
le plan de la table et on arrondit. Le plateau est une grille, pas une scène à
sonder.

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

La roche n'est plus empilée en boîtes : elle est **taillée**. `tools/blender/
cave_tile.py` part d'un bloc plein de 2 × 2 m, y creuse une galerie voûtée par
côté ouvert et exporte le résultat en `.obj` dans `resources/models/`, que Godot
importe comme un simple maillage. La scène de la tuile ne fait plus que poser ce
maillage et glisser une dalle de sol dessous. Régénérer une forme :

```
blender --background --python tools/blender/cave_tile.py -- --shape Junction
```

Deux règles tiennent le raccord entre tuiles : toutes les galeries sont creusées
avec le **même profil d'arche**, et le bruit qui accidente la roche s'éteint en
approchant des quatre plans de bord. Deux tuiles voisines, quelle que soit leur
rotation, présentent donc exactement la même ouverture.

Cinq maillages suffisent à tout le catalogue : un par tracé, que les quinze
scènes se partagent. Chacun ne porte que la **forme** de la galerie — 3 à 5 000
triangles, lissés. La
roche elle-même, ses plaques, ses joints et son grain, est l'affaire de
`resources/shaders/rock.gdshader` : un champ cellulaire échantillonné en position
monde y casse la paroi en plaques, chacune inclinée à sa façon, avec des joints
sombres où perce la chaleur de la montagne. Deux avantages sur de la géométrie :
c'est dix fois plus léger, et comme le champ est en coordonnées monde il traverse
les jointures de tuiles sans se répéter. Une tentative précédente portait tout ça
en maillage — 25 700 triangles par tuile, et ça se lisait comme des gravats.

Les sols suivent le même partage : `floor_stone.gdshader` dalle le chemin, et
chaque type de tuile n'est qu'un jeu de couleurs sur ce shader — la Lave laisse
sa chaleur monter entre les dalles (`seam_strength`), le Gardien teinte les
siennes de violet. C'est un `.tres` par type, pas un shader par type.

Une tuile taillée déclare les côtés qu'elle **mure** par un marqueur
`Rock/Wall_{côté}` : c'est ce que `BoardView.VerifyGeometry` compare au tracé que
le moteur a posé. Les tuiles encore en boîtes nomment leurs murs pareil, la boîte
faisant office de marqueur. La rotation, elle, est appliquée au nœud à la pose.

Une scène manquante déclenche un avertissement et un repli sur `TileNormal`
plutôt qu'un trou dans le temple.

Deux scènes servent à regarder tout ça sans lancer de partie. `scenes/_gallery.tscn`
pose les quinze tuiles en grille, chacune avec sa torche et son étiquette : c'est
la vue d'ensemble du catalogue. `scenes/_preview.tscn` en tire une seule, à
hauteur d'œil, et enregistre une image avant de rendre la main :

```
SUBTERRA_TILE=res://scenes/board/TileLava_Junction.tscn \
SUBTERRA_SHOT=/tmp/tuile.png godot --path . res://scenes/_preview.tscn
```

Attention en ouvrant une tuile seule dans l'éditeur : la roche remplace
l'éclairage de Godot par sa propre fonction `light()`, donc sans lumière dans la
scène tout est noir. Les bascules « Preview Sun » et « Preview Environment » de
la vue 3D suffisent.
