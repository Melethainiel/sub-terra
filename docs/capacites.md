# Des capacités au moteur : ce qu'il reste à câbler

Les 10 fiches d'Explorateur sont dans `ExplorerRoster` avec leurs deux capacités
chacune, telles que transcrites depuis `docs/regles.md` §12 — mais en texte
seul. `Explorer.Sheet` porte déjà la fiche jusqu'au moteur ; rien n'en lit
encore le contenu. Ce document relie chaque capacité à ce qu'il faut y ajouter :
une nouvelle entrée de `GameCommand`, un crochet dans une règle existante, et —
depuis que les actions communes sont passées en cartes (voir
`scripts/Presentation/Hud.cs`, `scripts/App/AppRoot.cs`) — la façon de la
viser sur le plateau.

## 1. Où ça s'accroche : les actions communes

Chacune a déjà sa carte et son `GameCommand` :

| Action | PA | `GameCommand` | Carte / ciblage |
|---|---|---|---|
| Se déplacer | 1 | `Move(Direction)` | carte, case voisine connectée |
| Explorer | 1 | `Explore(Direction)` | carte, issue non connectée |
| Révéler | 1 | `Reveal(Direction)` | carte, issue non connectée |
| Courir | 2 | `Run(IReadOnlyList<Direction>)` | carte, jusqu'à 3 clics enchaînés |
| Creuser | 2 | `Dig(Cell)` | carte, sa case ou une voisine connectée |
| Soigner | 1 | `Heal(ExplorerId)` | bouton, cible figée sur soi-même (voir §3) |
| Manier un objet | 1 | `PickUpItem` / `DropItem` | bouton, sans cible |
| Attaquer | 1 | `Attack` | bouton, sans cible |
| Se dépasser | — | `Overexert` | bouton, sans cible |

Une capacité qui *répète* une de ces actions (Sprinter, Excaver, Illuminer...)
n'a besoin d'aucun nouveau ciblage : elle appelle le même crochet une seconde
fois. Une capacité qui vise ailleurs que la case du joueur ou une voisine —
la plupart des capacités de Défenseur et d'Appui — a besoin d'un ciblage que
l'interface ne sait pas encore faire (§2).

## 2. Ciblages qu'il faudra ajouter à l'interface

L'armement d'une carte (`AppRoot._armed`) ne sait aujourd'hui pointer que :
une case voisine et connectée (`Cell.DirectionTo`), ou la case du joueur lui-
même. Trois nouveaux ciblages reviennent assez pour valoir une brique commune
plutôt qu'un bricolage par capacité :

- **Explorateur visible, à N cases ou moins** (Guérir, Ranimer). « Visible »
  au sens §6.4 des règles : ligne droite à travers des cases dégagées, bloquée
  par les murs et les Éboulis. La brique existe :
  `GameState.VisibleFrom(case, portée)` rend la case d'origine puis chaque
  ligne droite, de la plus proche à la plus lointaine ; un mur l'arrête, une
  brèche de Démolition la laisse passer, et une tuile sous Éboulis n'est ni
  vue ni traversée. La géométrie seule est dans `TempleBoard.VisibleFrom`.
- **Case visible en ligne droite, à N cases ou moins** (Lunette de visée, Tir
  de précision). Même brique de ligne de vue que ci-dessus, appliquée à une
  case plutôt qu'à un Explorateur.
- **Tuile adjacente et connectée, autre que la sienne** (Grenade, Excaver déjà
  couvert par Creuser). Presque `Cell.DirectionTo`, sauf qu'il faut exclure sa
  propre case — un garde-fou d'un cran, pas une nouvelle brique.
- **N'importe quelle tuile du Temple** (Rechercher, Purifier). Pas de portée :
  un clic n'importe où sur le plateau déjà posé. Le plus simple des quatre à
  câbler côté clic ; le plus large à mettre en évidence (`Hints()` devrait
  éclairer tout le plateau, pas une poignée de cases).

Tant que ces briques n'existent pas, une capacité qui en a besoin n'est pas
qu'un `GameCommand` à écrire : elle attend son ciblage.

## 3. Un carré déjà connu : Soigner

`Heal(ExplorerId)` cible « soi ou un Explorateur de sa tuile », mais
`AppRoot.Command("heal")` fige la cible sur `_game.CurrentExplorer.Id` — il
n'y a pas encore moyen de soigner quelqu'un d'autre sur sa propre case. Ce
n'est pas une capacité de personnage, mais **Guérir** et **Ranimer** en sont
la version à distance : les trois méritent d'être résolues ensemble plutôt que
Soigner d'un côté et les deux capacités de l'autre.

## 4. Les dix fiches

Capacité passive = aucun coût en PA, toujours active, pas de carte : un
crochet dans le code qui gère déjà la règle qu'elle modifie. Capacité à PA =
une carte de plus dans le HUD, sur le modèle de celles déjà en place.

### Éclaireur

| Explorateur | Capacité | Type | Accroche |
|---|---|---|---|
| L'Archéologue | *Érudite* — piocher 2 tuiles, en garder 1 | passive | `TileBag` : la pioche de `Reveal`/`Explore` doit pouvoir en tirer 2 et en remettre 1, puis laisser choisir laquelle poser. Change la forme de la décision qui suit une révélation. |
| L'Archéologue | *Aventurière* — 1 ♥ pour relancer un dé | passive, illimitée | N'importe quel jet (dé de Péril, Attaquer, piège à pics...) doit pouvoir être proposé à la relance avant d'être résolu — un point d'accroche unique pour tous les jets vaut mieux qu'un par appelant. |
| Le Guide | *Agile* — ignore les Éboulis pour Se déplacer/Courir/Explorer | passive | Ces trois actions traitent aujourd'hui une case à Éboulis comme infranchissable ; le filtre doit savoir laisser passer cet Explorateur-là. |
| Le Guide | *Illuminer* — Révéler deux fois | 1 PA | Nouvelle carte. Rejoue `Reveal` deux fois de suite pour 1 PA au lieu de 2 — pas un nouveau `GameCommand`, une variante de coût sur celui qui existe. |
| Le Gredin | *Sprinter* — Se déplacer deux fois | 1 PA | Nouvelle carte, même remarque qu'Illuminer mais sur `Move`. |
| Le Gredin | *Vigilance* — lui et sa tuile ignorent les pièges | passive | Crochet dans la résolution des pièges à pics/fléchettes (entrée sur la tuile, face Péril *Déclencher un piège*) : exclure sa tuile de la casse tant qu'il y est. |

### Connecteur

| Explorateur | Capacité | Type | Accroche |
|---|---|---|---|
| L'Aristocrate | *Ordonner* — un autre Explorateur debout se déplace immédiatement | 1 PA | Nouveau `GameCommand` (cible : un autre Explorateur debout, sans doute par sa case plutôt que son id, puisqu'il faut ensuite lui faire jouer un `Move`). Premier cas de commande qui joue une action *pour* quelqu'un d'autre — vérifier que `Execute` s'y prête. |
| L'Aristocrate | *Rechercher* — poser un Journal contre une tuile du Temple, ×3 | 2 PA, 3 usages/partie | Nouveau `GameCommand`, ciblage « n'importe quelle tuile » (§2). Compteur d'usages à porter quelque part — sur `Explorer` ou sur l'état de partie, à trancher à l'implémentation. |
| Le Contremaître | *Excaver* — Creuser | 1 PA | Pas de nouveau ciblage : rejoue `Dig` pour 1 PA au lieu de 2. |
| Le Contremaître | *Consolider* — sa tuile devient Normale jusqu'à la fin de la partie, ×4 | 1 PA, 4 usages/partie | Nouveau `GameCommand`, sans cible (toujours sa propre tuile). Il faut qu'une tuile puisse changer de `TileKind` en cours de partie — aujourd'hui `TileDefinition` est posée une fois pour toutes. |

### Défenseur

| Explorateur | Capacité | Type | Accroche |
|---|---|---|---|
| La Tireuse d'élite | *Lunette de visée* — révéler une case visible à 3 tuiles ou moins | 1 PA | Nouveau `GameCommand`, ciblage « case visible, N ≤ 3 » (§2). Une révélation qui n'est *pas* depuis sa propre tuile — vérifier que `Reveal` le permette ou en écrire une variante. |
| La Tireuse d'élite | *Tir de précision* — éliminer un ennemi visible à 3 tuiles ou moins, pas sur sa case | 1 PA | Nouveau `GameCommand`, même ciblage que Lunette de visée mais sur un Gardien plutôt qu'une case. |
| Le Sapeur | *Grenade* — élimine tous les ennemis d'une tuile adjacente connectée (pas la sienne) ; les Explorateurs dessus perdent 1 ♥ | 1 PA | Nouveau `GameCommand`, ciblage « tuile adjacente connectée, pas la sienne » (§2). |
| Le Sapeur | *Démolir* — détruit un mur adjacent, ×3 | 1 PA, 3 usages/partie | Nouveau `GameCommand`, ciblage : un mur adjacent plutôt qu'une case — pas encore de représentation cliquable pour « ce mur-ci » (les murs ne sont aujourd'hui que des bits dans `Sides`). Le marqueur Démolition retire le mur pour le reste de la partie (cf. §6.5 des règles). |
| La Combattante chevronnée | *Anéantir* — élimine un ennemi de sa tuile | 1 PA | Nouveau `GameCommand`, sans cible autre que « un ennemi ici » — proche d'`Attack` mais sans jet de dé. |
| La Combattante chevronnée | *Se préparer* — ne perd plus de ♥ jusqu'à son prochain tour ; indisponible le tour suivant | 1 PA | Nouveau `GameCommand`, sans cible. Un état à durée (« bouclier actif », « capacité en recharge ») à porter sur `Explorer` — premier cas du genre dans le moteur. |

### Appui

| Explorateur | Capacité | Type | Accroche |
|---|---|---|---|
| La Guérisseuse | *Guérir* — un autre Explorateur visible à 2 tuiles ou moins regagne 2 ♥ | 1 PA | Variante à distance de `Heal` (§3) : visible, portée 2. **Faite.** |
| La Guérisseuse | *Survivante* — sur *Trébucher*, regagne 1 ♥ au lieu de l'effet habituel | passive | Crochet dans la résolution de la face *Trébucher* du dé de Péril. |
| Le Prêtre | *Ranimer* — un autre Explorateur récupère 1 ♥ à terre, 3 ♥ sinon | 3 PA | Variante de `Heal` à distance : un Explorateur visible, sans limite de portée. **Faite.** |
| Le Prêtre | *Purifier* — élimine tous les ennemis d'une tuile autre que la sienne | 3 PA | Nouveau `GameCommand`, ciblage « n'importe quelle tuile » (§2), sans les hommes. |

## 5. Un ordre qui évite de refaire le travail

1. ~~**Ligne de vue** (§2)~~ — faite (`GameState.VisibleFrom`). Elle
   débloque Lunette de visée, Tir de précision, Guérir, et Ranimer si sa
   portée s'avère limitée.
2. ~~**Soigner à distance** (§3) avec Guérir et Ranimer~~ — fait. Les
   capacités passent par une commande unique, `UseAbility(capacité, cible)`,
   dont le coût et la cible appartiennent à la capacité ; `AbilityIds` en
   porte les noms stables. Ranimer vise un Explorateur visible, sans limite de
   portée (tranché : la fiche n'en donne pas). Soigner vise enfin un autre
   Explorateur de sa tuile. À l'écran, les deux capacités de l'Explorateur
   dont c'est le tour sont les deux dernières colonnes de la carte Actions
   (touches `1` et `2`) ; une capacité pas encore jouée y reste grisée.
3. **Les capacités passives** (Agile, Vigilance, Survivante, Aventurière) —
   chacune un seul crochet, aucun nouveau ciblage, le gain le plus rapide.
4. **Les répétitions d'action commune** (Illuminer, Sprinter, Excaver) — même
   remarque, coût différent seulement.
5. **Les nouveaux `GameCommand` avec ciblage déjà résolu par ailleurs**
   (Anéantir, Se préparer, Consolider, Ordonner).
6. **Les ciblages à bâtir en dernier** — « tuile adjacente pas la sienne »
   (Grenade), « n'importe quelle tuile » (Rechercher, Purifier), « ce mur-ci »
   (Démolir) : chacun n'est utile qu'à une ou deux capacités, ce qui les rend
   moins urgents que la ligne de vue.
