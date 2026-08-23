# Sub Terra II : Au bord de l'enfer — règles, version implémentable

Transcription structurée du livret officiel (Nuts! Publishing, 2021). Le PDF
lui-même n'est pas versionné — 14 Mo qui pèseraient sur chaque clone ; il se range
en local dans `docs/rules/`, que git ignore. Ce fichier-ci est la source de vérité
pour `SubTerra.Core`.
Les points marqués **[?]** ne sont pas tranchés par le livret et demandent une décision.

Jeu coopératif, 1 à 6 joueurs. Explorer le temple, déverrouiller le sanctuaire,
s'échapper avec l'artefact avant l'éruption.

---

## 1. Matériel

| Élément | Quantité |
|---|---|
| Tuile Entrée | 1 (multi-cases) |
| Tuiles Latérales | 2 (multi-cases) |
| Tuiles Temple | 30 (dans le sac) |
| Tuiles Journal | 3 (hors sac, réservées à l'Aristocrate) |
| Tuile Sanctuaire | 1 (hors sac et hors jeu au départ) |
| Fiches / meeples d'Explorateur | 10 |
| Meeples de Gardien | 5 |
| Marqueurs Éboulis | 6 |
| Marqueurs Clé | 3 |
| Marqueur Artefact | 1 |
| Marqueurs Consolidation | 4 (Contremaître) |
| Marqueurs Démolition | 3 (Sapeur) |
| Marqueur Bouclier | 1 (Combattante) |
| Plateau Volcan + marqueur Éruption | 1 + 1 |
| Dés de Péril | 2 |
| Dé (d6 classique) | 1 |
| Médaillon Chef d'Expédition | 1 (recto / face maudite) |

Répartition des 30 tuiles Temple : Normale ×3, Pont ×2, Clé ×3, Lave ×5,
Piège à pics ×3, Piège à fléchettes ×4, Ruines ×6, Gardien ×4.

---

## 2. Mise en place

1. Tuile Entrée sur un bord de la table, une tuile Latérale de chaque côté.
2. Tuile Sanctuaire mise de côté.
3. Les 30 tuiles Temple dans le sac. Les 3 tuiles Journal restent de côté.
4. Chaque joueur choisit une fiche d'Explorateur et prend ses PV max (3, 5 ou 7).
   Les meeples démarrent sur le **croisement** de la tuile Entrée (la case qui
   la relie aux tuiles Latérales).
   - 2 joueurs → chacun contrôle 2 Explorateurs.
   - Solo → 3 à 6 Explorateurs.
5. Marqueur Éruption placé selon difficulté × nombre d'Explorateurs :

   | Difficulté | 3 Expl. | 4 | 5 | 6 |
   |---|---|---|---|---|
   | Débutant | 27 | 24 | 22 | 20 |
   | Normal | 26 | 21 | 19 | 17 |
   | Avancé | 22 | 18 | 16 | 14 |
   | Expert | 20 | 16 | 14 | 12 |

6. Un joueur devient **Chef d'Expédition** (médaillon, face maudite cachée).
   Il tranche tous les désaccords et tous les choix « au choix de l'équipe ».

**[?]** Le livret ne donne pas de valeur pour 1 ou 2 Explorateurs — en solo/duo on
contrôle au moins 3 Explorateurs, donc la table couvre tous les cas légaux.

---

## 3. Objectif

1. Placer toutes les tuiles Temple du sac, ce qui permet de poser le Sanctuaire.
2. Apporter les 3 Clés au Sanctuaire pour le déverrouiller et libérer l'Artefact.
3. Regagner l'Entrée avec l'Artefact.

---

## 4. Structure d'un tour de jeu

Une partie dure environ 20 tours de jeu.

1. En commençant par le Chef d'Expédition puis dans le sens horaire, chaque
   Explorateur joue son **tour de joueur** :
   - A. réalise ses actions ;
   - B. lance **un** dé de Péril (**deux** une fois la malédiction active).
2. Fin du tour de jeu :
   - A. tous les Gardiens en jeu s'activent **2 fois** ;
   - B. le marqueur Éruption progresse d'**une** case (**deux** sous malédiction).

## 5. Fin de partie

La partie s'arrête quand plus aucun Explorateur survivant n'est dans le Temple,
ou quand tous les Explorateurs sont à terre.

Victoire si au moins un Explorateur s'est échappé avec l'Artefact :

| Condition | Rang |
|---|---|
| Tous les Explorateurs ont survécu | Légendaire |
| 1 mort | Or |
| 2 morts | Argent |
| 3 morts | Bronze |
| Personne ne s'échappe avec l'Artefact | Défaite — « Oubliés à jamais » |

---

## 6. Le plateau et les tuiles

### 6.1 Placement

- Chaque tuile Temple a deux faces : **Temple** et **Volcan**. Elles entrent en
  jeu face Temple.
- Les tuiles ne peuvent pas être placées derrière ni au-delà des tuiles
  Latérales : la zone jouable est bornée latéralement par l'emprise des
  Latérales, et s'étend uniquement en s'éloignant de l'Entrée.
- **Connexion** : deux tuiles sont connectées si elles sont adjacentes **et**
  qu'aucun mur ne les sépare. Un côté ouvert posé contre un mur ne crée pas de
  connexion (le mur compte quand même).
- L'orientation d'une tuile piochée est libre, à condition qu'une connexion soit
  établie avec la tuile d'où l'on révèle.
- **Totalement bloqué** : si la tuile placée fermerait toute possibilité de
  placer de nouvelles tuiles, la remettre dans le sac et en piocher une autre.
  Si aucune tuile du sac ne résout la situation, défausser le sac et placer le
  Sanctuaire.

**Géométrie des tuiles — dessinée par nous.** Le livret ne décrit pas
l'agencement des murs de chacune des 30 tuiles : c'est de l'illustration, pas de
la règle. Ce portage étant une version 3D et non une reproduction, les tracés
sont les nôtres (`TileShape` : croisement, T, couloir, coude), répartis sur les
types dans `TileCatalog`. Les effectifs par type, eux, viennent du livret.

**[?]** La largeur exacte de l'emprise des Latérales reste à confirmer. Le
schéma p. 8 se lit comme une grille de 7 colonnes jouables, l'Entrée occupant la
colonne centrale sur la rangée des Latérales.

### 6.2 Types de tuiles Temple

| Type | ×  | Règle |
|---|---|---|
| Normale | 3 | Aucune particularité. |
| Pont | 2 | Un seul Explorateur à la fois ; impossible d'y entrer si occupée. Les Gardiens peuvent y entrer même occupée. |
| Clé | 3 | À la pose, poser un marqueur Clé dessus. Récupérable via *Manier un objet*. |
| Lave | 5 | Les Explorateurs dessus perdent 1 ♥ à chaque symbole Lave obtenu sur un dé de Péril. |
| Piège à pics | 3 | En **entrant** : lancer le dé ; 4+ évite, 1–3 déclenche. Déclenché (par l'entrée ou par la face Piège) : tous les Explorateurs sur la tuile perdent 3 ♥. |
| Piège à fléchettes | 4 | Ne se déclenche **pas** à l'entrée, seulement sur la face Piège. Déclenché : tous les Explorateurs sur la tuile **et sur les tuiles adjacentes connectées** perdent 1 ♥. |
| Ruines | 6 | À la pose, poser un Éboulis. Impossible d'y entrer tant que l'Éboulis est là (*Creuser* d'abord) ; on peut en sortir. Une fois dégagée, peut s'effondrer à nouveau. Chaque tuile Ruines porte un chiffre de dé. |
| Gardien | 4 | À la pose, y placer un Gardien. Cible des effets *Réveiller un Gardien*. |
| Journal | 3 | Hors sac. Placées par l'Aristocrate. Se comportent comme des Normales. |

### 6.3 Tuiles multi-cases

- **Entrée** — point d'entrée et de sortie. Les Explorateurs démarrent sur le
  croisement. Un Explorateur peut se placer sur la **sortie** : il est alors
  retiré du Temple et **sauvé**. Il ne peut plus revenir, ni agir, ni utiliser de
  capacité. Il continue de lancer le dé de Péril ; pour *Réveiller* / *Activer
  les Gardiens*, on le considère juste à la sortie du Temple.
- **Latérales ×2** — posées de part et d'autre de l'Entrée. Elles contiennent des
  cases Gardien, mais **aucun Gardien n'y est placé en début de partie**.
- **Sanctuaire** — ni en jeu ni dans le sac au départ (cf. §10).

### 6.4 Ligne de vue

On voit en ligne droite à travers les cases dégagées. La vue est bloquée par les
murs et par les Éboulis. Une tuile portant un Éboulis n'est pas visible.

### 6.5 Destruction de murs

La capacité *Démolir* du Sapeur pose un marqueur Démolition sur un mur adjacent.
Le mur cesse d'exister : déplacement, révélation et exploration traversent la
brèche.

---

## 7. Actions

Un Explorateur dispose de **2 points d'action (PA)** par tour de joueur. Une même
action peut être répétée ; il n'est pas obligatoire de tout dépenser.

| Action | Coût | Effet |
|---|---|---|
| **Révéler** | 1 | Choisir une issue non encore connectée de sa tuile, piocher une tuile Temple et la placer face Temple en la connectant à sa tuile. Orientation libre si la connexion tient. |
| **Se déplacer** | 1 | Vers une tuile adjacente **et connectée**. |
| **Explorer** | 1 | Révéler une tuile puis y entrer immédiatement dans la même action. Si l'entrée est impossible, on reste sur place. |
| **Soigner** | 1 | +1 ♥ à soi ou à un Explorateur de sa tuile, sans dépasser le maximum de sa fiche. |
| **Manier un objet** | 1 | Ramasser un objet de sa tuile / prendre celui d'un Explorateur de sa tuile (avec son accord) / lui donner le sien / déposer le sien. Clés et Artefact sont des objets. **Un Explorateur ne peut détenir qu'un seul objet à la fois.** |
| **Attaquer** | 1 | Lancer le dé ; sur 4+, éliminer un ennemi de sa tuile. |
| **Courir** | 2 | Jusqu'à 3 fois *Se déplacer*. |
| **Creuser** | 2 | Retirer un Éboulis de sa tuile ou d'une tuile adjacente connectée. |

### 7.1 Se dépasser

Une fois par tour de joueur, un Explorateur peut perdre 1 ♥ pour gagner 1 PA
supplémentaire ce tour-ci.

### 7.2 Points de vie et état « à terre »

- ≥ 1 ♥ → **actif**, joue normalement.
- 0 ♥ → **à terre**, le meeple est couché. Si cela survient pendant son tour de
  joueur, celui-ci se termine immédiatement.
- À terre : une seule action possible, **Ramper** (se déplacer d'une seule tuile).
  Pas de *Se dépasser*. Les capacités passives restent actives. Il continue de
  lancer le dé de Péril à la fin de son tour.
- Soigné d'au moins 1 ♥ → il se relève et redevient actif.

---

## 8. Dé de Péril

Lancé à la fin de chaque tour de joueur (deux dés sous malédiction, résolus dans
l'ordre choisi). Six faces :

| Face | Effet |
|---|---|
| **Trébucher** | Si l'Explorateur s'est dépassé ce tour, il perd 1 ♥ de plus. |
| **Lave** | **Tous** les Explorateurs situés sur une tuile Lave perdent 1 ♥. |
| **Effondrement** | S'il existe des tuiles Ruines en jeu sans Éboulis, lancer le dé. Si le résultat correspond au chiffre d'une de ces tuiles, elle s'effondre : poser un Éboulis, les Explorateurs dessus perdent 5 ♥, les ennemis dessus sont éliminés. Une tuile Ruines qui porte déjà un Éboulis ne peut pas s'effondrer à nouveau. |
| **Déclencher un piège** | Déclenche le Piège à pics **de la tuile de l'Explorateur actif** (3 ♥ à tous ceux qui s'y trouvent) et les Pièges à fléchettes de sa tuile **et des tuiles adjacentes** (1 ♥). Seuls les pièges à proximité de l'Explorateur actif se déclenchent ; plusieurs pièges peuvent partir sur un même jet. |
| **Réveiller un Gardien** | Placer un Gardien sur la tuile Gardien la plus proche de l'Explorateur actif, occupée ou non. Ignoré si les 5 Gardiens sont déjà en jeu. |
| **Activer les Gardiens** | Activer **une fois** tous les Gardiens en jeu. |

---

## 9. Les Gardiens (la Légion Cendrée)

### 9.1 Placement

- Une tuile Gardien mise en jeu reçoit immédiatement un Gardien — **sauf** les
  cases Gardien des Latérales en début de partie.
- Face *Réveiller un Gardien* : tuile Gardien la plus proche de l'Explorateur
  actif, distance mesurée par le **chemin le plus court passant par des tuiles
  connectées**. Égalité → le joueur actif choisit. L'empilement est autorisé.
- Si les 5 Gardiens sont déjà dans le Temple, on n'en place pas.

### 9.2 Activation

Deux fois à la fin de chaque tour de jeu, et une fois par face *Activer les
Gardiens*. À chaque activation, chaque Gardien exécute la **première action
possible** de la liste :

1. **Attaquer** — s'il partage sa tuile avec un Explorateur **actif** (pas à
   terre), celui-ci perd 1 ♥. Plusieurs cibles → le Chef d'Expédition choisit.
2. **Se déplacer** — d'une tuile vers l'Explorateur actif le plus proche, si le
   chemin n'est pas bloqué par un Éboulis. Ambiguïté → le Chef décide.
3. **Creuser** — retirer un Éboulis adjacent. Ordre d'activation ambigu → le Chef
   décide.

### 9.3 Fuir

Quitter une tuile occupée par des Gardiens coûte 1 ♥ **par Gardien présent**. Si
l'Explorateur y perd son dernier ♥, il s'effondre sur la tuile adjacente vers
laquelle il se dirigeait.

### 9.4 Élimination

Les Gardiens ignorent les pièges et la lave. Ils ne peuvent être éliminés que
par : l'action *Attaquer* (4+), une capacité spéciale d'Explorateur, une Ruine
qui s'effondre sur eux, ou une tuile retournée face Volcan.

---

## 10. Le Sanctuaire, l'Artefact et la malédiction

1. Quand le sac est vide et toutes les tuiles Temple placées, poser la tuile
   Sanctuaire connectée à une tuile de la **colonne la plus éloignée possible de
   l'Entrée**. Plusieurs emplacements possibles → le Chef choisit. Si la dernière
   colonne ne permet aucune connexion, essayer la précédente, et ainsi de suite.
2. Placer les 3 Clés sur la tuile Sanctuaire — **une action *Manier un objet* par
   Clé**.
3. Dès la 3ᵉ Clé posée, l'Artefact apparaît sur le piédestal au centre du
   Sanctuaire. Il se ramasse comme un objet ordinaire.

### La malédiction

Dès qu'un Explorateur prend l'Artefact, retourner le médaillon côté maudit :

- à la fin du tour de **chaque** Explorateur, **2** dés de Péril sont lancés et
  résolus dans l'ordre voulu ;
- à la fin de **chaque tour de jeu**, le marqueur Éruption avance de **2** cases.

**[?]** « Colonne » : dans l'orientation du schéma p. 8 (Entrée en haut,
extension vers le bas) il s'agit des rangées successives en s'éloignant de
l'Entrée. Le schéma p. 22 est dessiné pivoté de 90°. À confirmer contre le
matériel réel.

---

## 11. Le Volcan

- Fin de chaque tour de jeu : le marqueur Éruption avance de 1 case (2 une fois
  l'Artefact récupéré).
- Tant que le marqueur est au-dessus de 0, rien ne se passe.
- Arrivé à **0**, le Volcan est prêt. **La prochaine fois qu'un Explorateur
  obtient un symbole Lave** sur un dé de Péril : retirer le marqueur Éruption et
  retourner le plateau Volcan, **en plus** de l'effet normal du dé.
  - Si l'Artefact n'a pas quitté le Sanctuaire, ou si le Sanctuaire n'a pas
    encore été découvert → **partie perdue**.
  - Si le Sanctuaire est en jeu, le retourner sur sa face Volcan.

### 11.1 Propagation de la lave

Une fois le plateau Volcan retourné, la lave s'écoule quand :

- le marqueur Éruption aurait été déplacé (donc 2 fois à la fin de chaque tour de
  jeu, l'Artefact étant récupéré) ;
- un Explorateur obtient un symbole Lave sur un dé de Péril (en plus des effets
  du dé).

Quand la lave s'écoule : retourner face Volcan toutes les tuiles Temple
adjacentes **et connectées** à des tuiles déjà face Volcan, en conservant les
connexions. Entrée et Latérales se retournent **en groupe**.

### 11.2 Tuile retournée face Volcan

Tous les marqueurs et meeples présents sont retirés : les Explorateurs sont
**tués**, les Gardiens éliminés. Il devient impossible d'entrer sur la tuile.

Les Explorateurs défunts **continuent de lancer les dés de Péril à leur tour**.
Pour *Réveiller* / *Activer les Gardiens*, on les considère situés à l'Entrée.

---

## 12. Les Explorateurs

Domaines de prédilection : **Éclaireur** 👁, **Connecteur** ✕, **Défenseur** 🛡,
**Appui** ✚. Les capacités sans coût en PA sont **passives** et permanentes.

| Explorateur | ♥ | Domaine | Capacité 1 | Capacité 2 |
|---|---|---|---|---|
| **L'Archéologue** | 5 | Éclaireur | *Érudite* (passive) — au lieu de piocher 1 tuile, en piocher 2, en choisir une, remettre l'autre dans le sac. | *Aventurière* (passive) — à son tour, dépenser 1 ♥ pour relancer n'importe quel dé. |
| **Le Guide** | 3 | Éclaireur | *Agile* (passive) — lors de *Se déplacer*, *Courir* ou *Explorer*, ignorer les marqueurs Éboulis. | *Illuminer* (1 PA) — effectuer deux fois l'action *Révéler*. |
| **Le Gredin** | 3 | Éclaireur | *Sprinter* (1 PA) — effectuer deux fois *Se déplacer*. | *Vigilance* (passive) — lui et les Explorateurs de sa tuile ne peuvent ni déclencher de piège ni perdre de ♥ du fait d'un piège. |
| **L'Aristocrate** | 5 | Connecteur | *Ordonner* (1 PA) — un Explorateur pas à terre effectue immédiatement une action *Se déplacer*. | *Rechercher* (2 PA, ×3) — placer une tuile Journal à côté de n'importe quelle tuile du Temple, en connexion. |
| **Le Contremaître** | 5 | Connecteur | *Excaver* (1 PA) — effectuer une action *Creuser*. | *Consolider* (1 PA, ×4) — poser un marqueur Consolidation sur sa tuile : elle devient une tuile Normale jusqu'à la fin de la partie. |
| **La Tireuse d'élite** | 5 | Défenseur | *Lunette de visée* (1 PA) — révéler une tuile visible en ligne droite à 3 tuiles ou moins. | *Tir de précision* (1 PA) — éliminer un ennemi visible en ligne droite à 3 tuiles ou moins, mais pas sur sa propre tuile. |
| **Le Sapeur** | 5 | Défenseur | *Grenade* (1 PA) — éliminer tous les ennemis d'une tuile adjacente et connectée (pas la sienne) ; les Explorateurs de cette tuile perdent 1 ♥. | *Démolir* (1 PA, ×3) — détruire un mur adjacent, y poser un marqueur Démolition. |
| **La Combattante chevronnée** | 7 | Défenseur | *Anéantir* (1 PA) — éliminer un ennemi de sa tuile. | *Se préparer* (1 PA) — ne peut plus perdre de ♥ jusqu'au début de son prochain tour (*Se dépasser* coûte toujours 1 ♥) ; poser le marqueur Bouclier, le retirer au début du prochain tour ; capacité indisponible au tour suivant. |
| **La Guérisseuse** | 3 | Appui | *Guérir* (1 PA) — un autre Explorateur visible à 2 tuiles ou moins regagne 2 ♥. | *Survivante* (passive) — sur un symbole *Trébucher*, regagner 1 ♥ au lieu de l'effet habituel. |
| **Le Prêtre** | 5 | Appui | *Ranimer* (3 PA) — un autre Explorateur récupère 1 ♥ s'il est à terre, 3 ♥ sinon. | *Purifier* (3 PA) — éliminer tous les ennemis d'une tuile autre que la sienne. |

---

## 13. Points à trancher avant implémentation

1. **Emprise exacte de la zone jouable** — largeur en colonnes et profondeur
   maximale, à confirmer sur le matériel.
2. **Chiffres des tuiles Ruines** — le livret parle du « chiffre » de chaque
   tuile Ruines ; 6 tuiles, vraisemblablement 1 à 6, à confirmer.
3. **Orientation « colonne »** pour le placement du Sanctuaire (cf. §10).
4. **Face retournée du plateau Volcan** — le livret la montre sans détailler ses
   pictogrammes.
