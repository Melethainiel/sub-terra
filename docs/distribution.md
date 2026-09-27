# Distribution

Sub Terra II se diffuse **en privé** : le dépôt GitHub est privé, et ses Releases
le sont aussi. C'est l'adaptation numérique d'un jeu de plateau commercial (Inside
the Box Board Games, et Nuts! Publishing en France). Une diffusion publique
demanderait leur accord.

## Publier une version

1. Monter `application/config/version` dans `project.godot` (affiché en bas de
   l'accueil) et `application/file_version` dans `export_presets.cfg`.
2. Taguer et pousser : `git tag v0.9.0 && git push origin v0.9.0`.
3. Le workflow **Release** (`.github/workflows/release.yml`) exporte Linux et
   Windows, les zippe et les attache à une Release GitHub du même nom.

Le workflow **Tests** fait passer les tests du moteur à chaque push.

## Exporter à la main

Les modèles d'export Godot 4.7.2 .NET se rangent dans
`~/.local/share/godot/export_templates/4.7.2.stable.mono/` ; seuls les modèles
Linux et Windows x86_64 sont nécessaires.

```
godot --headless --path . --export-release "Linux"   build/linux/SubTerra.x86_64
godot --headless --path . --export-release "Windows" build/windows/SubTerra.exe
```

Les profils (`export_presets.cfg`) laissent de côté :

- les scènes de développement (`scenes/_*`) ;
- `docs/`, `tools/`, `tests/` et `src/` (le moteur y entre compilé, pas en source) ;
- l'addon MCP de l'éditeur.

L'addon MCP d'exécution reste : il est déclaré en autoload et ne s'ouvre jamais dans
un build final.

## Vérifier un export

Un export se vérifie sans le lancer à la main. Le Movie Maker de Godot marche aussi
dans un build final, et enregistre l'image et le son :

```
build/linux/SubTerra.x86_64 --write-movie /tmp/run.avi --fixed-fps 30 --quit-after 150
```

C'est ainsi qu'on a vu que le pied de page de l'accueil, et son bouton Quitter,
tombaient hors de l'écran.

## Limites connues

- L'exécutable Windows garde l'icône de Godot dans l'explorateur : y mettre la nôtre
  demande `rcedit`, qui ne tourne que sous Windows (ou Wine). La fenêtre du jeu porte
  bien la nôtre.
- L'export Windows n'est vérifié que dans sa structure (exécutable PE 64 bits et son
  runtime .NET), pas lancé : il faut une machine Windows pour ça.
- Rien n'est signé : Windows SmartScreen avertira au premier lancement.
