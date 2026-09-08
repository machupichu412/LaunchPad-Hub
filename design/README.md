# Design source assets

Brand artwork in its delivered form. **Nothing here is served or bundled** — the
web app never loads these files. They live outside `src/LaunchPad.Web/public`
deliberately: anything under `public/` is copied into `dist/` and published to
the static web app, and there is no reason to ship ~3.5MB of print-scale exports
to every visitor.

| File | What it is |
|---|---|
| `launchpad-logo.svg` | Source of truth. Rocket with exhaust trail, 561.92 × 645.16. |
| `launchpad-logo@{0.5,0.75,1.5,2,3,4}x.png` | Delivered raster exports, as supplied. |
| `launchpad-mark-512.png` | Rocket alone, no trail — the master the favicons are cut from. |

Despite the `.svg` extension, the logo is **not vector**: it is an SVG wrapper
around embedded base64 PNGs. It carries no scaling advantage over a sized raster,
which is why the app ships PNGs rather than referencing the SVG directly.

## Regenerating the served assets

The app loads exactly two files, both in `src/LaunchPad.Web/public/brand`:
`launchpad-mark-96.png` (nav) and `launchpad-logo-320.png` (sign-in, initial
load, home banner). The favicons live one level up in `public/`.

```bash
cd src/LaunchPad.Web/public
SVG=../../../design/launchpad-logo.svg

# Full logo, transparent
magick -background none -density 600 "$SVG" -resize 2249x2582 -depth 8 /tmp/full4x.png
magick /tmp/full4x.png -trim +repage -resize x320 -background none \
  -depth 8 -strip -define png:compression-level=9 brand/launchpad-logo-320.png

# Rocket only — crop is the Rocket layer's bounding box in the SVG viewBox
magick /tmp/full4x.png -crop 1300x1960+246+0 +repage -trim +repage \
  -resize 460x460 -background none -gravity center -extent 512x512 \
  -depth 8 -strip ../../../design/launchpad-mark-512.png
magick ../../../design/launchpad-mark-512.png -resize 96x96 -depth 8 -strip brand/launchpad-mark-96.png

# Favicons
magick ../../../design/launchpad-mark-512.png -resize 192x192 -depth 8 -strip favicon-192.png
magick ../../../design/launchpad-mark-512.png -resize 32x32  -depth 8 -strip favicon-32.png
magick ../../../design/launchpad-mark-512.png -resize 152x152 -background white \
  -gravity center -extent 180x180 -depth 8 -strip apple-touch-icon.png
```

Two flags in there are not optional, both learned the hard way:

- **`-background none`** — without it the alpha is flattened to white and the
  logo sits in a white box on any tinted surface.
- **`-depth 8`** — ImageMagick renders this SVG at 16-bit, which roughly triples
  every output (the @4x export came out at 8.4MB against the original's 1.2MB).
  8-bit matches the supplied exports and is lossless at that depth.

`apple-touch-icon.png` is the deliberate exception to transparency: iOS composites
alpha to black, so it gets a white plate.

After regenerating, check alpha survived:

```bash
magick identify -format "%f %[channels] opaque=%[opaque]\n" brand/*.png favicon-*.png
```

Everything except `apple-touch-icon.png` should report `opaque=False`.
