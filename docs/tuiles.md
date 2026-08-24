# Les tuiles — spec dictée

Liste de référence pour ce portage, **dictée par Thibaud**, tuile par tuile. Elle
prime sur toute déduction faite depuis le livret : c'est elle qui définit les
tracés et les effets à implémenter.

Conventions de géométrie : tuile **3 × 3 m**, à l'échelle d'un Explorateur adulte
(œil à 1,60 m). Les couloirs ne sont pas des boîtes mais des galeries creusées
dans un bloc plein : 1,5 m de large au sol, l'arche s'évase à 2 m d'ouverture à
hauteur d'épaule puis se referme en ogive à **2,25 m**, et la roche monte à 2,7 m.
Sol de 22 cm. Orientation de référence : la forme est décrite non tournée, la
rotation est choisie à la pose.

| № | Tuile | Tracé | × | Effet |
|---|---|---|---|---|
| 1 | Normale | T (3 entrées) | 3 | Aucun. |
| 2 | Pont | I (2 entrées opposées) | 2 | Un seul joueur à la fois sur la tuile. |
| 3 | Clé | Cul-de-sac (1 entrée) | 3 | Contient une Clé, ramassable par un joueur. |
| 4 | Lave | Croix (4 entrées) | 3 | Les Explorateurs dessus perdent 1 ♥ quand le dé tombe sur Flamme. |
| 5 | Lave | T (3 entrées) | 2 | Identique à la 4. |
| 6 | Piège à pics | Croix (4 entrées) | 3 | Le joueur lance 1d6 : 4+ rien, sinon −3 ♥. |
| 7 | Piège à fléchettes | Coude (2 entrées adjacentes) | 4 | Les joueurs sur la tuile **et sur les tuiles adjacentes connectées** perdent 1 ♥. |
| 8 | Effondrement | Croix (4 entrées) | 3 | Lors d'un effondrement, les joueurs sur la tuile perdent 5 ♥. |
| 9 | Effondrement | T (3 entrées) | 3 | Identique à la 8. |
| 10 | Gardien | Cul-de-sac (1 entrée) | 2 | Fait apparaître un Gardien à la pose, et au tirage « nouveau Gardien ». |
| 11 | Gardien | Coude (2 entrées adjacentes) | 2 | Identique à la 10. |

Soit **30 tuiles** dans le sac. Cette composition tombe sur les mêmes effectifs
par type que le livret ; ce sont les tracés qui nous appartiennent.

## Les pièces multiples

| Pièce | Composition |
|---|---|
| **Entrée** | Un cul-de-sac (la sortie du temple) + une **croix** placée au centre, où démarrent les Explorateurs. Le temple s'ouvre au sud de cette croix. |
| **Latérale** (×2) | Deux tuiles **Normale en T** + un **Gardien en cul-de-sac** à l'extrémité extérieure. Aucun Gardien n'y est placé en début de partie. |
| **Sanctuaire** | Une tuile **en couloir** où déposer les trois Clés + un **cul-de-sac** contenant l'Artefact. |

Les Latérales ne sont donc pas un type de tuile à part : elles sont faites de
tuiles Normale et Gardien.

## Encore à définir

- La tuile Journal (réservée à l'Aristocrate).

## État de l'implémentation

Les scènes de `scenes/board/` portent la géométrie et l'apparence de toutes ces
tuiles. Les quinze sont **taillées en grotte** : cinq maillages, un par tracé
(`tools/blender/cave_tile.py`), que chaque scène pose avec le sol et les
décorations de son type.

Côté moteur, **les onze effets sont joués** : le Pont ne supporte qu'un
Explorateur, la Clé se ramasse, le Gardien surgit à la pose, les Ruines arrivent
sous leurs éboulis et s'effondrent sur leur chiffre, la Lave brûle sur la face
Flamme, les pics se déclenchent à l'entrée et sur la face Piège, les fléchettes
balaient la tuile et ses voisines reliées.

### Écarts assumés par rapport à ta dictée

Ta spec ne disait rien de ces points ; j'ai suivi le livret et je les signale
plutôt que de les enterrer.

- **Ruines** — elles arrivent avec un marqueur Éboulis qui interdit d'y entrer
  tant qu'on n'a pas Creusé. Ta dictée ne parlait que des 5 ♥ perdus lors de
  l'effondrement.
- **Piège à pics** — désormais tous les Explorateurs présents sur la tuile
  perdent 3 ♥, comme tu l'as confirmé.
- **Départager les égalités** — le livret confie au Chef d'Expédition le choix
  de la cible d'un Gardien et de sa direction. Le moteur tranche pour l'instant
  par l'ordre du plateau (nord, est, sud, ouest), ce qui est déterministe. À
  reprendre le jour où on saura poser la question au Chef.

Le Sanctuaire se pose tout seul quand le sac se vide, aussi loin de l'Entrée que
le temple s'étend : son couloir reçoit les trois Clés, son cul-de-sac libère
l'Artefact. Le prendre déclenche la malédiction — deux dés de Péril par tour, et
l'Éruption qui avance de deux cases par manche. Sortir par l'Entrée avec
l'Artefact gagne la partie.

## Encore à définir

- La tuile Journal (réservée à l'Aristocrate).
- Les dix Explorateurs et leurs vingt capacités : aucune n'est implémentée.
