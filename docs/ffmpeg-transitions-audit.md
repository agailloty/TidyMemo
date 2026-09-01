# Audit des transitions FFmpeg de SlideTune

## 1. Architecture actuelle

TidyMemo est une application desktop .NET 10/Avalonia 12 organisée simplement en modèles,
ViewModels, vues AXAML et services. `MainWindowViewModel` compose directement les services et
crée `SlideshowViewModel`. Il n'existe ni conteneur d'injection, ni projet de tests avant cette
évolution.

Le diaporama est représenté par `SlideshowOptions` et produit par `SlideshowService`. Le
ViewModel rassemble les réglages de session mais ceux du diaporama ne sont pas persistés : seul
`AppSettings` conserve le chemin FFmpeg, le sous-dossier vidéo et l'activation du compresseur.

FFmpeg est soit choisi dans Settings, soit téléchargé dans les données locales par
`FfmpegDownloadService`. Le téléchargement est épinglé au paquet `ffmpeg-static` `b6.1.1`.
Avant cette évolution, la validité se limitait à l'existence du fichier (ou à `-version` lors du
téléchargement), sans version minimale ni détection de filtre.

## 2. Pipeline FFmpeg actuel

```text
fichiers source
  -> un appel FFmpeg par image : orientation du décodeur, scale, RGBA,
     bordure, ombre, padding transparent/noir
  -> PNG homogènes temporaires (ou composition ImageMagick dans un cas précis)
  -> manifeste ffconcat avec une durée par image
  -> flux concat unique
  -> fps + composition finale (scale/pad ou fond + overlay)
  -> libx264 / CRF / preset / yuv420p / faststart
  -> audio bouclé optionnel, volume AAC, arrêté avec la vidéo
```

La résolution vient d'un catalogue UI, le framerate vaut 30 par défaut, la durée 3 s et le
pixel format de sortie `yuv420p`. `fps`, `scale`, `pad` et `setsar=1` homogénéisent le flux. Les
timestamps sont remis à zéro dans le mode avec fond. Les erreurs sont lues sur stderr, tronquées
à 16 000 caractères puis les huit dernières lignes sont retournées; il n'existe pas de journal
persistant.

Le filtre final doit précéder la transition : l'utilisateur attend une transition entre les
compositions complètes, et non entre les photos brutes.

## 3. Composants concernés

- `Models/SlideshowModels.cs` : options et résultat.
- `Services/SlideshowService.cs` : préparation, graphes, processus et progression.
- `ViewModels/SlideshowViewModel.cs` : réglages et construction des options.
- `Views/SlideshowView.axaml` : contrôles de présentation.
- `Services/FfmpegDownloadService.cs` : provenance de l'exécutable.
- nouveaux composants ciblés : catalogue métier, timeline pure, graphe de transitions et sonde
  de capacités.

Il n'existait aucune transition, durée de chevauchement, fondu ou abstraction de filter graph.

## 4. Contraintes et problèmes identifiés

`xfade` exige deux entrées vidéo homogènes (résolution, format, framerate et timebase). Le flux
concat historique ne fournit pas une entrée par photo. Le chemin de transition doit donc rendre
chaque composition complète à dimensions et format homogènes, puis créer un segment bouclé par
rendu. `-framerate` est posé sur chaque entrée image : il fixe cadence et timebase, et chaque
démultiplexeur d'image commence à PTS zéro. Cette stratégie a été validée sur la build auditée;
placer `setpts` avant `xfade` y efface le champ de cadence et provoque une erreur `1/0`.

La durée de transition doit être strictement inférieure à la durée d'une photo. Pour `N` photos
de durée `D` et des chevauchements `T_i`, la durée finale est :

```text
N * D - somme(T_i), pour i = 1..N-1
```

Le début de la transition numéro `i` (base zéro) dans le flux déjà composé est
`(i + 1) * D - somme(T_0..T_i)`; avec une durée globale `T`, `(i + 1) * D - (i + 1) * T`.

## 5. Catalogue natif ciblé

La sonde exécute `ffmpeg -hide_banner -h filter=xfade` et extrait les noms réellement annoncés.
Le catalogue applicatif couvre les 58 transitions natives de la build auditée :

- Basic : fade, dissolve, fadeblack, fadewhite, fadegrays, fadefast, fadeslow.
- Slide : slideleft/right/up/down, smoothleft/right/up/down.
- Wipe : wipeleft/right/up/down, wipetl/tr/bl/br.
- Geometric : circlecrop/open/close, rectcrop, distance, radial, vertopen/close,
  horzopen/close, diagtl/tr/bl/br.
- Dynamic : pixelize, hblur, squeezeh/v, zoomin, hlslice/hrslice/vuslice/vdslice,
  hlwind/hrwind/vuwind/vdwind.
- Reveal / Cover : coverleft/right/up/down, revealleft/right/up/down.

`custom` est volontairement exclu : il nécessite une expression et appartient à l'extension
future. Une transition absente de la sortie de la sonde est refusée avec un diagnostic utile.
La build embarquée sert de baseline; une installation système différente reste supportée par
détection de capacité plutôt que par hypothèse de version.

## 6. Architecture proposée et décision

**Situation actuelle.** Un service monolithique mais lisible produit un flux concat.

**Problème.** `xfade` nécessite des segments séparés et une timeline testable.

**Options.** Remplacer tout le pipeline; construire un graphe par photo depuis les sources;
ou conserver le chemin historique et ajouter un chemin spécialisé à partir de rendus finaux.

**Décision.** Conserver intégralement le chemin `None`. Pour une transition, préparer les effets,
rendre chaque canvas final en PNG, puis fournir ces PNG comme entrées bouclées à cadence
explicite (`-framerate`, qui fixe également la timebase et démarre chaque image à PTS zéro) à un
`TransitionGraphBuilder`. `SlideshowTimeline` porte les calculs temporels et
`FfmpegCapabilitiesService` valide `xfade` et la transition choisie. Le catalogue contient les
métadonnées et isole tous les noms FFmpeg de l'UI.

**Raison.** Cette variante minimise la régression, transitionne le rendu final et reste testable
sans processus FFmpeg. Le coût est un passage de rendu temporaire supplémentaire uniquement
quand les transitions sont actives.

**Impact.** Les futurs réglages par relation pourront fournir une liste de définitions au builder.
Motion restera une étape de segment distincte, placée avant la transition.

## 7. Stratégie UI

Une section compacte expose le mode `None`, `Native` ou `Random`, une liste de définitions avec
nom et catégorie, et une durée de 0,1 à 3 s. Le catalogue reste plat dans cette première version
mais les libellés préfixés par catégorie rendent les choix parcourables. Les descriptions sont
disponibles en info-bulle. `None` est la valeur par défaut et conserve le rendu historique.

Random choisit dans un profil raisonnable couvrant tout le catalogue disponible et interdit une
répétition immédiate. La séparation mode/définition permettra plus tard Soft/Dynamic/All.

## 8. Stratégie de timeline

Une classe pure valide les durées, calcule chaque offset et la durée finale. Le graphe chaîne
`[0:v][1:v]xfade -> [xf1]`, puis `[xf1][2:v]xfade -> [xf2]`. Les nombres utilisent la
culture invariante. La progression et `-t` utilisent la durée résultante, y compris pour l'audio.

## 9. Stratégie de tests

Les tests sans FFmpeg couvrent le mapping `Fade -> fade`, les offsets, `10 * 5 - 9 * 1 = 41 s`,
les fragments du graphe, Random sans répétition immédiate et l'absence de `xfade` pour `None`.
Une validation manuelle avec l'exécutable configuré complète ces tests purs.

## 10. Risques et performance

- Un graphe à 500 photos comporte 500 entrées et 499 `xfade`; longueur de commande, handles,
  threads, mémoire et limites OS deviennent significatifs. 10 et 50 restent raisonnables; 100
  mérite un test d'intégration; 500 nécessitera probablement un traitement par lots puis concat.
- Les PNG plein format augmentent fortement l'espace temporaire, surtout en 4K.
- `xfade` est CPU-intensif et empêche certaines optimisations du chemin concat.
- Des builds anciennes peuvent proposer `xfade` avec un catalogue réduit : la sonde filtre le
  choix, mais Random doit également se limiter aux capacités détectées.
- L'échec conserve stderr dans le résultat utilisateur; une journalisation durable reste une
  amélioration séparée.

## 11. Plan d'implémentation

1. Ajouter les modèles, métadonnées et le catalogue.
2. Ajouter timeline, sélection Random et génération pure du graphe.
3. Ajouter la sonde `xfade` et les diagnostics ciblés.
4. Ajouter les propriétés du ViewModel et la section Avalonia.
5. Garder `None` sur le pipeline actuel; ajouter rendu final + entrées séparées pour les autres.
6. Ajouter tests unitaires, documentation utilisateur, compilation et validation.

## Future: Photo Motion & Advanced Transitions

Une future `VideoSegmentBuilder` pourra appliquer `Slow Zoom In/Out`, `Pan Left/Right/Up/Down`
ou Ken Burns à un segment avant sa normalisation. Le contrat de transition restera centré sur la
relation `segment N -> segment N+1`. Une seconde implémentation pourra générer des expressions
`xfade=custom` ou des graphes composés pour Zoom + Blur, Push, Photo Drop, Polaroid et Photo Stack.
Ainsi Motion, Composition et Transition restent trois responsabilités ordonnées sans moteur 3D,
GPU ou éditeur de timeline.
