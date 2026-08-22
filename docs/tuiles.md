# Les tuiles — spec dictée

Liste de référence pour ce portage, **dictée par Thibaud**, tuile par tuile. Elle
prime sur toute déduction faite depuis le livret : c'est elle qui définit les
tracés et les effets à implémenter.

Conventions de géométrie (posées sur la tuile 1) : tuile 2 × 2 m, couloir 1 m de
large, roche 1,5 m de haut et 0,5 m d'épaisseur, sol de 15 cm. Orientation de
référence : la forme est décrite non tournée, la rotation est choisie à la pose.

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

## Encore à définir

- Les tuiles multiples : Entrée, Latérales, Sanctuaire.
- La tuile Journal (réservée à l'Aristocrate).

## État de l'implémentation

Les scènes de `scenes/board/` portent la **géométrie et l'apparence** de ces onze
tuiles. Aucun de leurs **effets** n'est encore implémenté : le moteur ne connaît
pour l'instant que le tracé et le type.
