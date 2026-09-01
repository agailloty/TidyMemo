# SlideTune transitions

SlideTune can blend the completed slide canvases between photos. Borders, shadows, selected
backgrounds, scaling and positioning are rendered before the transition.

## Settings

- **None** keeps the original cut between photos and is the default for existing installations.
- **Native** applies the selected FFmpeg transition between every pair of photos.
- **Random** chooses a supported transition for each pair and avoids immediate repetitions.
- **Duration** controls the overlap, from 0.1 to 3 seconds. It must remain shorter than the
  number of seconds per image.

The transition list is grouped by a category prefix. Availability is checked against the FFmpeg
executable configured in Settings when an export starts. If its `xfade` filter or a selected
transition is unavailable, SlideTune stops before encoding and reports the capability problem.

## Timing

A transition overlaps adjacent photos; it does not add time. For example, ten photos displayed
for five seconds with nine one-second transitions produce a 41-second video:

```text
10 × 5 seconds - 9 × 1 second = 41 seconds
```

Background music is looped as needed and trimmed to the resulting video duration.
