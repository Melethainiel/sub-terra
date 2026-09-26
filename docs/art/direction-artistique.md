# Direction artistique

**Choix : B+, la figurine peinte à trait d'encre.** Les pièces de Sub Terra II sont
des figurines de jeu de plateau, peintes comme on peint une figurine Warhammer, et
posées sur socle. Un contour d'encre fin et des peintures un peu plus vives que
nature les détachent de la roche sombre.

Planches de référence : `planche-styles.png` (les pistes A et B) et
`planche-hybride.png` (B contre B+). Pour les régénérer :

```
SUBTERRA_STYLE=hybrid SUBTERRA_VIEW=hero SUBTERRA_SHOT=/tmp/b+.png \
    godot --path . res://scenes/_style_board.tscn
```

La piste A (cartoon façon Borderlands) et la B pure (figurine sans trait) sont
écartées. Leurs rendus restent dans `StyleBoard` pour comparaison
(`SUBTERRA_STYLE=toon|figurine`).

## Le rendu

Tout le rendu vit dans le moteur : `scripts/Presentation/Miniature.cs`. Un modèle
n'apporte **aucun** aspect de lui-même.

- **Peinture** (`resources/shaders/styles/painted_mini.gdshader`)
  - **Lumière zénithale peinte** : ce qui regarde vers le haut est plus clair.
  - **Lavis** : ce qui se détourne de l'œil s'assombrit, comme le pigment dans les
    creux.
  - **Rehaut** : un filet clair sur la silhouette.
  - **Vernis mat.**
- **Trait d'encre** (`ink_outline.gdshader`)
  - L'enveloppe du modèle, gonflée de `Miniature.InkThickness` (1,6 cm) et vue de
    l'intérieur.
  - Pas de trait sur ce qui brille (flamme, braise).
- **Palette** (`Miniature.Paints`)
  - Chaque pièce a une couleur « peinte » et une couleur « vive ».
  - B+ prend le mélange `Miniature.Blend` (45 % vers la vive).

## Le socle et la couleur du joueur

Vue du dessus, une figurine ne fait que quelques pixels. Ce n'est pas elle qui dit
à qui elle appartient :

- **Socle rond** : bord noir verni, dessus terre, comme une vraie figurine.
  - Explorateur : 0,24 m de rayon.
  - Gardien : 0,32 m de rayon.
- **Dessus du socle à la couleur du siège**, légèrement lumineux.
- **Anneau au sol** autour du socle, à la couleur du siège, que la figurine ne
  peut pas masquer. Violet pour les Gardiens.
- **Manteau** (matériau `Coat`) repeint à la couleur du siège.

Un Explorateur à terre se couche sur le flanc, socle compris, comme une figurine
renversée sur la table.

## Conventions de modélisation

| | |
|---|---|
| Format | `.glb` (glTF binaire), un fichier par figurine |
| Emplacement | `resources/models/figures/` |
| Nom | l'identifiant de la fiche : `archeologue.glb`, `guide.glb`… ; `guardian.glb` |
| Axes | Y en haut, la figurine regarde vers +Z (Blender : Z en haut, regard vers −Y) |
| Origine | au sol, entre les pieds — le socle est ajouté par le moteur, pas modélisé |
| Échelle | 1 unité = 1 m ; Explorateur ≈ 1,70 m, Gardien ≈ 2,10 m hors armes |
| Budget | Explorateur ≤ 8 000 triangles, Gardien ≤ 10 000 |
| Normales | lissées : le trait d'encre suit les normales, une arête vive le déchire |

**Matériaux** : chaque pièce porte un matériau **nommé pour ce qu'elle est**, et
rien d'autre ne compte (couleurs, textures et réglages du fichier sont ignorés).
Les noms reconnus sont :

- `Skin`, `Hair`, `Cloth`, `Coat`, `Leather`, `Accent` ;
- `Metal`, `Wood`, `Flame` ;
- `Ash`, `Armor`, `Bone`, `Ember`.

Un nom inconnu déclenche un avertissement. Une nouvelle matière s'ajoute d'abord à
`Miniature.Paints`.

**La distribution actuelle** (`distribution.png`) : les dix Explorateurs et le
Gardien, sculptés par `tools/blender/figure.py` — corps paramétrable, une
silhouette et des accessoires propres à chacun. Ce sont des figurines honnêtes mais
simples, faites pour être remplacées fichier par fichier par des modèles générés
depuis des illustrations (Meshy, Tripo…), nettoyés et remis aux conventions
ci-dessus.

**En attendant** : tant que `<fiche>.glb` n'existe pas, `TokenView` pose
l'esquisse `explorer_sketch.glb`. Les modèles peuvent donc arriver un par un.
Esquisses et futurs modèles sortent de `tools/blender/figure.py` :

```
blender --background --python tools/blender/figure.py -- --out resources/models/figures
```

## Limite connue

La vue du dessus cadre tout le temple. Plus il s'étend, plus les figurines
rapetissent. L'anneau de couleur garde la lecture de qui est où, mais le détail
des figurines ne se voit vraiment qu'en vue à la première personne. Un zoom dans
la vue du dessus serait le vrai remède.
