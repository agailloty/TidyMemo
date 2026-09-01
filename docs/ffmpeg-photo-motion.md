# FFmpeg Photo Motion

## Current pipeline

Sources are normalized by FFmpeg to homogeneous PNG canvases. Aspect-ratio preserving scale,
border, shadow and padding are applied first; background composition then produces a final canvas.
When neither Motion nor Transition is enabled, the historical concat path remains unchanged.

With Motion enabled, each final canvas is rendered sequentially to a short temporary video.
`xfade` then consumes those completed moving segments. This two-pass design keeps dozens of
`zoompan` filters from being active simultaneously and the motion continues naturally throughout
the transition overlap.

If FFmpeg fails or crashes, SlideTune retains the last diagnostic lines, reports the native exit
code when applicable and removes the incomplete MP4 (which otherwise lacks its final `moov`
metadata). Cancellation follows the same cleanup rule.

```text
photo -> appearance/background -> final canvas -> zoompan motion -> normalized segment
                                                              segments -> xfade -> encode
```

This first version treats movement as object/canvas motion, so an existing border and shadow move
with the photo. A future content-motion stage can be inserted before appearance without changing
the start/end transform model.

## Motion architecture

`PhotoMotionDefinition` contains stable metadata and a generic `Start`/`End` transform. A transform
has a zoom and a normalized focus point. Presets never contain FFmpeg syntax. The catalog exposes
zoom, pan, diagonal, Ken Burns and drift families. `PhotoMotionSelector` implements seeded Random,
Random Soft and Random Ken Burns while avoiding immediate preset and category repetition.

`MotionExpressionBuilder` is the only component translating these models to FFmpeg expressions.
Intensity scales zoom and displacement around the neutral transform `(zoom=1, focus=.5/.5)`.

## FFmpeg filters

| Family | Filters | Strategy | Limitations |
|---|---|---|---|
| Zoom / Push / Pull | `zoompan` | interpolate start/end zoom | zoom never below 1 |
| Pan / Drift | `zoompan` | fixed zoom plus interpolated focus | needs overscan supplied by zoom |
| Ken Burns | `zoompan` | interpolate zoom and focus together | operates on final canvas currently |
| Rotation | evaluated `rotate` | not exposed yet | corners require overscan/crop and affect appearance |
| Visual effects | evaluated separately | not exposed yet | require filter-by-filter capability and visual tests |

Capability detection checks `zoompan`; `xfade` continues to be checked only when a transition is
requested. Unsupported required filters fail before encoding with a focused diagnostic.

## Coordinate system

Focus coordinates are normalized and clamped to `[0,1]`. `(0,0)` is top-left, `(1,1)` is
bottom-right and `(.5,.5)` is center. At each frame they become:

```text
x = (iw - iw / zoom) * focusX
y = (ih - ih / zoom) * focusY
```

Thus x/y always remain inside the source. Pan presets use a zoom above 1, while zoom-out presets
start above 1 and end exactly at 1, preventing transparent or black areas.

## Zoom and intensity

Normal slow zoom uses roughly `1.00 -> 1.08`; push/pull uses `1.00 <-> 1.13`; pan uses 1.12 and
drift 1.06. Subtle multiplies deltas by 0.55, Normal by 1.0 and Strong by 1.45. Zoom is clamped to
at least 1 and normalized focus remains within its valid range.

## Timeline and easing

`frames = round(duration * fps)` and FFmpeg's `on` variable supplies the current output frame.
Progress is `clamp(on / (frames - 1), 0, 1)`. Linear, quadratic Ease In/Out and smoothstep Ease In
Out are emitted as FFmpeg math expressions. C# never renders individual frames.

## Interaction with `xfade`

Every motion branch lasts the full photo duration. Transition offsets still come exclusively from
`SlideshowTimeline`; no transition code or duration rule is duplicated. Graph order is:

```text
[input i] -> zoompan -> fps/scale/SAR/format -> [mi]
[m0][m1] -> xfade -> ... -> [video]
```

Without transitions, moving branches use FFmpeg `concat`. With transitions, the existing native
transition definitions and offsets are retained. For very large slideshows, 100 branches should be
measured and 500 will likely require chunked renders to limit graph size, memory and open handles.
