# Design source assets

Brand artwork in its delivered form. **Nothing here is served or bundled** — the
web app never loads these files. They live outside `src/LaunchPad.Web/public`
deliberately: anything under `public/` is copied into `dist/` and published to
the static web app, and there is no reason to ship several MB of print-scale
exports to every visitor.

| File | What it is |
|---|---|
| `launchpad-logo.svg` | Source of truth for the full mark. Rocket with exhaust trail, 561.92 × 645.16. |
| `launchpad-logo@{0.5,0.75,1.5,2,3,4}x.png` | Delivered raster exports of the full mark, as supplied. |
| `Rocket Vertical.svg`, `Rocket Vertical@0.5x.png` | Source of truth for the **standing** rocket, isolated — no trail. Upright on the pad; used for static chrome (nav icon, favicons). PNG is the delivered export, as supplied. |
| `Rocket.svg`, `Rocket@0.5x.png` | Source of truth for the **angled** rocket, isolated — no trail. Tilted into its own flight path; used wherever the mark is already in motion (the candidate dashboard's JourneyTrail). PNG is the delivered export, as supplied. |
| `launchpad-mark-512.png` | Superseded. Earlier master, cropped from the full logo's bounding box — that crop is what let a sliver of white background through once. `Rocket Vertical.svg` / `Rocket.svg` are dedicated isolated art and are now the source for every rocket-alone export. Kept only as delivered history. |
| `Rocket Launch.mp4` | Source of truth for the animated rocket — 3D-rendered footage of the same angled pose in `Rocket.svg` actually launching, 960×960, 1.7s, no audio. Used for the initial-loading porthole and the nav-logo hover (`RocketLaunch.tsx`). Delivered as plain H.264 with **no alpha channel** — rendered on its own near-white stage rather than transparent, unlike every still asset above. |

Despite the `.svg` extension, none of these are vector: each is an SVG wrapper
around an embedded base64 PNG. They carry no scaling advantage over a sized
raster, which is why the app ships PNGs rather than referencing the SVGs
directly.

`Rocket Launch.mp4` doesn't get the same transparency treatment. `-background
none` has no video equivalent that's reliable cross-browser — the installed
ffmpeg's libvpx-vp9 build doesn't actually emit alpha into WebM despite
accepting `-pix_fmt yuva420p` silently, and Safari has no transparent-video
path at all (no alpha WebM, would need HEVC-in-.mov). Chasing that isn't worth
it: `RocketLaunch.tsx` ships the footage as-is and callers frame it in a plate
pinned to the footage's own stage color (`#FCFCFA`) instead — same fix as the
`launchpad-mark-512.png` white-background bug, applied to video.

## Regenerating the served assets

The app loads five files, all in `src/LaunchPad.Web/public/brand`:
`launchpad-mark-96.png` (nav, static chrome), `launchpad-mark-angled-96.png`
(JourneyTrail), `launchpad-logo-320.png` (sign-in, initial load, home banner),
and `rocket-launch.mp4` / `rocket-launch-poster.png` (initial-loading porthole,
nav hover). The favicons live one level up in `public/`.

```bash
cd src/LaunchPad.Web/public
DESIGN=../../../design

# Full logo, transparent
magick -background none -density 600 "$DESIGN/launchpad-logo.svg" -resize 2249x2582 -depth 8 /tmp/full4x.png
magick /tmp/full4x.png -trim +repage -resize x320 -background none \
  -depth 8 -strip brand/launchpad-logo-320.png

# Rocket marks — tightly trimmed to their own art, natural (non-square) aspect.
# Rendered oversize first so the trim/resize order can't clip fine detail (the
# glass dome, the flame) at the final small size.
magick -background none -density 600 "$DESIGN/Rocket Vertical.svg" -resize x384 -trim +repage \
  -resize x96 -depth 8 -strip brand/launchpad-mark-96.png
magick -background none -density 600 "$DESIGN/Rocket.svg" -resize x384 -trim +repage \
  -resize x96 -depth 8 -strip brand/launchpad-mark-angled-96.png

# Favicons — square slots, so the standing rocket is padded onto a square
# canvas with room around it, then downsized per target.
magick -background none -density 600 "$DESIGN/Rocket Vertical.svg" -resize 460x460 \
  -background none -gravity center -extent 512x512 -depth 8 -strip /tmp/mark-square-512.png
magick /tmp/mark-square-512.png -resize 192x192 -depth 8 -strip favicon-192.png
magick /tmp/mark-square-512.png -resize 32x32  -depth 8 -strip favicon-32.png
magick /tmp/mark-square-512.png -resize 152x152 -background white \
  -gravity center -extent 180x180 -depth 8 -strip apple-touch-icon.png

# Rocket launch — downscaled (960->480; the largest use is a 168px porthole,
# plenty of headroom at retina) and re-encoded for web delivery. No audio track
# to strip (source has none). Poster is just the video's own first frame.
ffmpeg -i "$DESIGN/Rocket Launch.mp4" -vf "scale=480:480" -c:v libx264 -profile:v high \
  -pix_fmt yuv420p -crf 23 -movflags +faststart -an brand/rocket-launch.mp4
ffmpeg -i brand/rocket-launch.mp4 -update 1 -frames:v 1 /tmp/rocket-poster-raw.png
magick /tmp/rocket-poster-raw.png -depth 8 -strip brand/rocket-launch-poster.png
```

Two flags in there are not optional, both learned the hard way:

- **`-background none`** — without it the alpha is flattened to white and the
  logo sits in a white box on any tinted surface.
- **`-depth 8`** — ImageMagick renders these SVGs at 16-bit, which roughly
  triples every output (an earlier @4x export came out at 8.4MB against the
  supplied original's 1.2MB). 8-bit matches the supplied exports and is
  lossless at that depth.

`apple-touch-icon.png` is the deliberate exception to transparency: iOS
composites alpha to black, so it gets a white plate. `rocket-launch-poster.png`
is the other exception — it's a plain frame grab of opaque footage, not a keyed
asset, so it's meant to report `opaque=True`.

After regenerating, check alpha survived:

```bash
magick identify -format "%f %[channels] opaque=%[opaque]\n" brand/*.png favicon-*.png apple-touch-icon.png
```

Everything except `apple-touch-icon.png` and `rocket-launch-poster.png` should
report `opaque=False`.
