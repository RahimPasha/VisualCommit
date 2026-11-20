# The icon, round 2 (2026-11-20)

The owner chose A, Lens, in round 1 (`../round-1/`). This round works the chosen mark out into the
drawings that the icon's files are made from (D407, D447; design 7.3). The colours are the owner's,
unchanged: the tile `#4C5BE0`, the mark white.

Open `candidates.html` in a browser: it shows every size on a dark and a light desk, the real
pixels of the small sizes enlarged, the one choice that is the owner's, and what is still soft. The
page loads nothing from the web.

## Why a second round

At 64 pixels and up the chosen drawing reads well. At 16 to 32 pixels, where an icon is seen most
(the taskbar, the title bar, a file list), a plain shrinking turns it to mush: the eye's outline is
about one pixel wide and falls between pixels. So the mark needs a second drawing on a 16-pixel
grid, and macOS needs the mark on Apple's rounded square.

## The files

| File | What it is |
|---|---|
| `icon.svg` | The full drawing, for every size above 32 pixels and as Linux's scalable icon. It is the chosen drawing at four times its size; only the eye's curve moved by 8 units of 1024, so that the lids' edges fall on whole pixels at 48, 64, 96, 128 and 256. |
| `icon-small.svg` | The drawing for 16, 20, 24, 30 and 32 pixels, on a 16 grid: the tile fills its square (only the rounded corners are transparent), the line is two units tall, the eye is an outline one unit thick with a clean blue row above and below a four-unit pupil. |
| `icon-small-alt.svg` | The runner-up for the small sizes, kept for the owner's choice: the same tile and line, the eye a solid white almond with a blue pupil. |
| `icon-macos.svg` | The mark on Apple's opaque rounded square: 832 wide, 96 from each edge of the 1024 canvas, corner radius 187 (Apple's grid is about 824, 100 and 185; these numbers put the plate's edges on whole pixels at 32, 64, 128 and 256). |
| `small-zoom.png` | `icon-small.svg` at 16, 20, 24, 30 and 32 pixels, each enlarged six times without smoothing, on a dark desk (`#202020`) above and a light one (`#F3F3F3`) below. |
| `small-compared.png` | The outlined and the solid small drawing at 16 to 32 pixels, enlarged four times: outlined on dark, solid on dark, outlined on light, solid on light; each row ends with `icon.svg` at 36. |
| `large.png` | `icon.svg` and `icon-macos.svg` at 256 pixels on both desks. |

The pictures were rendered with Microsoft Edge, headless, at one device pixel per pixel. The tool
that makes the real icon files renders with Skia, whose edge pixels can differ a little; the lead
compares the two before the masters are frozen.

## How the round was made

Three draughtsmen (AI agents, as the lead is) each drew a full set with a different aim: faithful
to the chosen drawing, pixel first, and bold. Each rendered its work, looked at the pictures and
improved it, 12 to 13 rounds each. Three judges then looked at the renders, one for the small
sizes, one for whether the set is still the owner's mark, one for the masters' rules and the
platforms. All three chose the faithful set (scores 8, 9 and 8 of 10; the pixel-first set 6, 6 and
7; the bold set 5, 3 and 6). A last draughtsman built the final set from it, took from the judges
what the pictures showed to help (the flat inner top and bottom of the small eye, both radii
written on every tile) and left what did not.

## What is still soft

- 20 pixels (the title bar at 125 % scaling) is the softest small size: every edge of the 16 grid
  falls on a quarter pixel there. 16, 24 and 32 are crisp.
- At the small sizes the line is twice the weight of the eye's outline. The grid forces it: the
  tile's centre lies between two rows, and a line one unit tall would be grey.
- The small icons fill their square; the large ones keep the chosen drawing's margin of one
  sixteenth. The tile is 32 pixels wide at 32 and 31.5 at 36; the two never show side by side.
- `icon.svg` at 48 pixels has a line of two solid rows with a half-covered row on each side; at 36
  and 40 the tile's edge falls on a quarter or half pixel.
- On macOS the 16-pixel picture for screens that are not Retina is mush in every set that was
  drawn. Retina screens use the 32-pixel one, which is crisp.
- On a dark taskbar the tile's blue holds at 3.0 to 1, the floor of Microsoft's rule; on a light
  one the line's white ends blend into the desk at 16 to 24 pixels.

## The owner's word

**Yes to the set, with the outlined eye at the small sizes** (the owner, 2026-11-20). The blue stays
the owner's `#4C5BE0`: the lighter one was shown and not asked for. The look-alike check is made by
eye, against Git's logo and the well-known Git clients' icons, with no image search (the owner, the
same day), and is recorded in the phase's test report. So the masters are `icon.svg`,
`icon-small.svg` and `icon-macos.svg` as they stand here; `icon-small-alt.svg` stays in this folder
as the record of the choice.

What was asked:

1. Yes to the set, or what to change.
2. The small sizes: the outlined eye (`icon-small.svg`, the lead's recommendation, because it is
   the same mark at every size) or the solid one (`icon-small-alt.svg`, crisper at 16 but with a
   blue pupil and a change of character between 32 and 36 pixels).
3. Only if the owner wants it: a blue one step lighter, `#5866E6`, for dark taskbars (3.5 to 1 on
   dark, 4.3 to 1 on light, white on it 4.7 to 1). The set is drawn in the owner's `#4C5BE0`.

After the owner's word the three masters go to `assets/brand/` with the icon's tool (design
section 7), which then needs no placeholder mark.
