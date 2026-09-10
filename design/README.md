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

Despite the `.svg` extension, none of these are vector: each is an SVG wrapper
around an embedded base64 PNG. They carry no scaling advantage over a sized
raster, which is why the app ships PNGs rather than referencing the SVGs
directly.

## Regenerating the served assets

The app loads exactly three files, all in `src/LaunchPad.Web/public/brand`:
`launchpad-mark-96.png` (nav, static chrome), `launchpad-mark-angled-96.png`
(JourneyTrail), and `launchpad-logo-320.png` (sign-in, initial load, home
banner). The favicons live one level up in `public/`.

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
```

Two flags in there are not optional, both learned the hard way:

- **`-background none`** — without it the alpha is flattened to white and the
  logo sits in a white box on any tinted surface.
- **`-depth 8`** — ImageMagick renders these SVGs at 16-bit, which roughly
  triples every output (an earlier @4x export came out at 8.4MB against the
  supplied original's 1.2MB). 8-bit matches the supplied exports and is
  lossless at that depth.

`apple-touch-icon.png` is the deliberate exception to transparency: iOS
composites alpha to black, so it gets a white plate.

After regenerating, check alpha survived:

```bash
magick identify -format "%f %[channels] opaque=%[opaque]\n" brand/*.png favicon-*.png apple-touch-icon.png
```

Everything except `apple-touch-icon.png` should report `opaque=False`.
