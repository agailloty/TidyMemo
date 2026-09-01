# Audit du moteur Photo Motion FFmpeg

## 1. État actuel

SlideTune est une application .NET 10/Avalonia 12. `SlideshowViewModel` construit un
`SlideshowOptions`, `SlideshowService` prépare les médias et pilote FFmpeg, et les projets
`.slidetune` sont sérialisés par `JsonSlideshowProjectStore` avec un contexte JSON source-generated.
Les réglages globaux de transition sont aussi conservés dans `AppSettings`.

Une photo est représentée à trois niveaux : chemin dans `SlideshowOptions.Images`,
`SlideshowSlide` persisté (identité, chemin, durée et transition optionnelles), et
`SlideshowItemViewModel` dans la liste UI. Le modèle par-photo existe donc, mais l'export utilise
encore les réglages globaux et une durée commune.

## 2. Emplacement de `xfade` et timeline

`TransitionGraphBuilder.Build` chaîne directement les entrées `[0:v]`, `[1:v]`, etc. avec
`xfade`. `SlideshowTimeline` calcule les offsets et la durée totale comme
`N * duréePhoto - somme(duréesTransition)`. Chaque entrée est une image finale bouclée avec
`-framerate`, `-loop 1` et `-t duréePhoto`; le mouvement peut donc rester actif pendant le
chevauchement au lieu de se figer avant la transition.

## 3. Pipeline réellement observé

```text
source photo
  -> appel FFmpeg de normalisation
     scale aspect-ratio=decrease -> RGBA -> drawbox (bordure)
     -> split -> colorchannelmixer/pad/gblur (ombre) -> overlay -> pad
  -> PNG temporaire homogène
  -> composition du fond final si nécessaire
     fond image/solid/gradient -> overlay photo
  -> PNG du canvas final
  -> entrée image bouclée à la cadence et à la durée demandées
  -> xfade entre canvases normalisés
  -> libx264/yuv420p/faststart (+ audio optionnel)
```

Sans transition, le pipeline historique utilise un manifeste concat, puis `fps`, `scale` et
`pad`/composition. Avec transition, `RenderFinalSlidesAsync` crée les canvases finaux avant le
graphe `xfade`. `BuildImageEffectFilter` contient bordure et ombre; `BuildFilter` contient fond,
scale, crop, padding et overlay.

Il n'existe pas de preview vidéo distinct : l'export est le seul rendu temporel. Il ne faut donc
pas introduire de simulation UI; une future preview devra appeler les mêmes builders FFmpeg.

## 4. Classes à réutiliser et à modifier

- Réutiliser `SlideshowTimeline`, `TransitionCatalog`, `TransitionGraphBuilder`, le rendu des
  canvases et la construction sûre des arguments de `SlideshowService`.
- Étendre `SlideshowOptions`, `SlideshowProject`, `AppSettings`, `SettingsViewModel` et
  `SlideshowViewModel` pour les réglages Motion.
- Faire évoluer la sonde `FfmpegCapabilitiesService` afin de connaître `zoompan`.
- Ajouter un catalogue Motion indépendant, un modèle start/end normalisé, un sélecteur Random
  déterministe et un générateur d'expressions.
- Faire évoluer le graphe vidéo par segments sans réécrire le chaînage `xfade`.

## 5. Catalogue retenu

Le socle fiable utilise `zoompan` : None, Slow Zoom In/Out, Push/Pull, quatre pans, quatre pans
diagonaux, huit Ken Burns directionnels, quatre Ken Burns diagonaux, quatre drifts, Random,
Random Soft et Random Ken Burns. Tous sont des presets d'un même transform start/end.

La rotation est différée : sur le canvas final, `rotate` révèle les coins sauf si un overscan et
un crop supplémentaires sont imposés, ce qui modifie bordure et ombre. Les effets visuels
(`vignette`, `gblur`, `unsharp`, `eq`, monochrome, warm/cool/sepia) restent une responsabilité
distincte et ne sont pas exposés avant validation visuelle et détection individuelle.

## 6. Expressions et coordonnées

Le nombre de frames est `max(1, round(duration * fps))`. La progression est dérivée de `on`
et bornée à `[0,1]`, puis transformée par Linear, Ease In (`p²`), Ease Out
(`1-(1-p)²`) ou Ease In Out (`3p²-2p³`). Zoom, focus X et focus Y interpolent leurs valeurs
start/end. Le viewport est converti par :

```text
x = (iw - iw / zoom) * normalizedFocusX
y = (ih - ih / zoom) * normalizedFocusY
```

Les pans utilisent un zoom minimal supérieur à 1 afin de disposer d'une marge de déplacement;
aucune expression ne dépend d'une résolution ou d'une cadence codée en dur.

## 7. Stratégie UI et persistance

Une section globale compacte expose le preset, l'intensité et l'easing. Les détails FFmpeg ne
quittent jamais les services/modèles. `Motion=None` est la valeur par défaut pour les anciens
fichiers et réglages. `SlideshowSlide.MotionId` prépare un futur override par photo sans imposer
ce workflow à la première UI. Un `RandomSeed` rend la sélection reproductible.

## 8. Stratégie de tests

Les tests purs couvriront catalogue/preset, direction des pans, combinaison Ken Burns,
progression indépendante du framerate, intensité, expressions, Random déterministe et absence
de répétition. Les tests du graphe vérifieront que les branches Motion précèdent `xfade`.
Les tests d'intégration FFmpeg seront conditionnels à la présence de l'exécutable afin de rester
portables en CI.

## 9. Risques de régression et performance

- Le chemin concat historique doit rester strictement sélectionné pour Motion=None et
  Transition=None.
- `zoompan` augmente nettement le coût CPU et chaque photo devient une branche vidéo.
- 10 à 50 entrées sont raisonnables; 100 doivent être mesurées. À 500, longueur du graphe,
  décodage parallèle, mémoire et limites de handles justifient un futur rendu par lots.
- Le mouvement du canvas final anime aussi bordure et ombre. Un futur `Content Motion` avant
  composition demandera de conserver une source overscannée distincte; un futur `Object Motion`
  pourra animer le canvas. Cette distinction est documentée sans dupliquer maintenant le moteur.
- Portrait, paysage, carré et panoramique restent sûrs parce que chaque entrée Motion part d'un
  canvas déjà normalisé au ratio de sortie et `zoompan` ne descend jamais sous 1.
