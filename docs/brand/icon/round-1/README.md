# The icon, round 1 (2026-11-19)

The first candidates for VisualCommit's icon (D407, D447). The lead, an AI model, drew each one by
hand as vector shapes, in the app's own colours; the owner chooses one and directs its changes over
one to three rounds. This folder is the record of that: each round keeps its drawings, what the
owner chose and what the owner asked to change, because the owner's choices and edits are the human
part of the drawing (D407).

Open `candidates.html` in a browser: it shows each candidate at 16, 24, 32, 48, 64, 128 and 256
pixels on a dark desk and on a light one, with what to watch in each. The page loads nothing from
the web.

| Letter | File | The idea |
|---|---|---|
| A | `a-lens.svg` | Lens: the commit on its line, drawn as an eye. One flat colour. |
| B | `b-merge-v.svg` | Merge V: two branches in the graph's first two lane colours meet in one commit and spell a V. |
| C | `c-check.svg` | Check: a commit is work marked done; the tick's corner is a commit's ring. |
| D | `d-ring.svg` | Ring: the letter C around one commit. The simplest, and the strongest at 16 pixels. |
| E | `e-viewfinder.svg` | Viewfinder: one commit on its history line, held in a viewfinder's corners. |

Left out on purpose: Git's diamond and its red, any letter G, and a full picture of a commit graph
(R5: no Git logo and nothing of another client's).

## The owner's choice

Not made yet. The candidates were put before the owner on 2026-11-19, first at 16 to 128 pixels on
a private page, then here with the 256 pixel column.

## What follows the choice

The chosen candidate becomes the three masters in `assets/brand/` (the full drawing, a simpler one
for 32 pixels and below, and the one on Apple's grid), from which the tool makes the `.ico`, the
`.icns` and the PNG files (design 7.3). Before it ships it is checked side by side against Git's
logo and the well-known Git clients' icons, so that it resembles none of them. Until the owner has
chosen, the app carries a placeholder mark that is none of these five (D428).
