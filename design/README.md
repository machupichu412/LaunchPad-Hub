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
| `Rocket Launch.mp4` | Source of truth for the animated rocket — 3D-rendered footage of the same angled pose in `Rocket.svg` actually launching, 960×960, 1.7s, no audio. Used for the initial-loading screen and the nav-logo hover (`RocketLaunch.tsx`). Delivered as plain H.264 with **no alpha channel** — rendered on its own near-white stage rather than transparent, unlike every still asset above. |

Despite the `.svg` extension, none of these are vector: each is an SVG wrapper
around an embedded base64 PNG. They carry no scaling advantage over a sized
raster, which is why the app ships PNGs rather than referencing the SVGs
directly.

`Rocket Launch.mp4` doesn't get transparency for free the way the still assets
do. `-background none` has no video-codec equivalent that's reliable
cross-browser (the installed ffmpeg's libvpx-vp9 build doesn't actually emit
alpha into WebM despite silently accepting `-pix_fmt yuva420p`, and Safari has
no transparent-video path at all). Worse, a plain color-key doesn't work on
this footage even as a still image: the stage is colorimetrically identical to
some of the rocket's own paint (the glass dome highlight, the top hull edge),
so keying by color distance punches holes through the rocket, not just the
background. What actually works — see the recipe below — is flood-filling
transparency in from each frame's four corners: the true background is one
region that touches every edge of the canvas, while the dome highlight is
fully enclosed inside the rocket's silhouette and never gets touched. The
result ships as an animated WebP (real per-pixel alpha, unlike WebM here).

## Regenerating the served assets

The app loads seven files, all in `src/LaunchPad.Web/public/brand`:
`launchpad-mark-96.png` (nav, static chrome), `launchpad-mark-angled-96.png`
(JourneyTrail), `launchpad-logo-320.png` + `launchpad-logo-320.webp`
(sign-in, initial load, home banner — `BrandMark.tsx` serves the WebP via
`<picture>`, with the PNG as the `<img>` fallback for a browser that doesn't
support WebP), and `rocket-launch.webp` / `rocket-launch-poster-end.png`
(initial loading, nav hover). The favicons live one level up in `public/`.

Only the full logo gets a WebP twin — the mark PNGs are already 7-10KB, not
worth a second request for. `launchpad-logo-320.png` is 60KB; `cwebp -q 90`
gets it to 11KB with no visible difference at 4x zoom (checked by decoding
both and comparing side by side, same as the animated rocket's compression
check below).

`rocket-launch.webp` is authored with a **finite loop count (1)**, not JS —
`RocketLaunch.tsx` is a plain `<img>`. That's what makes it play through once
and hold on its last frame rather than cycling: that final frame is where the
trail re-forms the full logo's own shape, so it's the intended resting state,
not a truncation. Each fresh mount of the `<img>` replays from frame one,
which is what gives AppShell's hover-to-relaunch its restart. Building it
needs `libwebp` (`brew install webp`) for `img2webp`, `cwebp`, and `dwebp`.

```bash
cd src/LaunchPad.Web/public
DESIGN=../../../design

# Full logo, transparent
magick -background none -density 600 "$DESIGN/launchpad-logo.svg" -resize 2249x2582 -depth 8 /tmp/full4x.png
magick /tmp/full4x.png -trim +repage -resize x320 -background none \
  -depth 8 -strip brand/launchpad-logo-320.png

# WebP twin, served in preference to the PNG above — see "Regenerating the served
# assets" for why only this asset gets one.
cwebp -q 90 brand/launchpad-logo-320.png -o brand/launchpad-logo-320.webp

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

# Rocket launch — one frame at a time: extract at full source resolution (best
# keying detail), flood-fill transparency in from the four corners (contiguous,
# so it can't eat through the dome highlight — see above), then downscale.
# -fuzz sets how close to the corner's own color counts as "still background".
rm -rf /tmp/rocket-frames /tmp/rocket-frames-alpha
mkdir -p /tmp/rocket-frames /tmp/rocket-frames-alpha
ffmpeg -i "$DESIGN/Rocket Launch.mp4" /tmp/rocket-frames/f%03d.png
for f in /tmp/rocket-frames/f*.png; do
  magick "$f" -alpha set -fuzz 6% \
    -fill none -draw "color 0,0 floodfill" \
    -fill none -draw "color 959,0 floodfill" \
    -fill none -draw "color 0,959 floodfill" \
    -fill none -draw "color 959,959 floodfill" \
    -resize 480x480 -depth 8 -strip \
    "/tmp/rocket-frames-alpha/$(basename "$f")"
done

# Assemble into an animated WebP. -d 33 matches the source's 30fps. -loop 1 is
# the whole trick behind "plays once and stops" — no JS controls this.
img2webp -d 33 -loop 1 -lossy -q 80 -m 6 /tmp/rocket-frames-alpha/f*.png \
  -o brand/rocket-launch.webp

# Last frame, standalone — the prefers-reduced-motion fallback.
cp /tmp/rocket-frames-alpha/f051.png brand/rocket-launch-poster-end.png
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

Everything except `apple-touch-icon.png` should report `opaque=False` —
`rocket-launch-poster-end.png` included, now that it's a flood-filled frame
rather than a plain grab of opaque footage. `rocket-launch.webp` isn't a PNG,
so it's not in that glob; sanity-check it by opening it directly in a browser
tab against a dark page background, and confirm `webpinfo brand/rocket-launch.webp`
reports `Loop count : 1`.
