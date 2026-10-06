# Phase 0 visual test report — Foundation

The Check, Steps and Expected columns are written at the start of the phase, before the UI they
test is built. The result columns are filled in when the gate runs.

## Run

Filled in when the gate runs: date, app commit, Windows version, display scaling, screenshot folder.

## What the shell must show

Checks 1–4 all expect this layout. Sizes are in logical pixels; a tolerance of 2 pixels is allowed.

| Region | Position | Expected content |
|---|---|---|
| Repo tabs | Top edge, full width, 36 high | One tab labelled "No repository" with an accent-coloured line along its top edge, then a "+" button |
| Toolbar | Under the tabs, full width, 52 high | Buttons with an icon above a label, in this order from the left: Undo, Redo, Fetch, Pull, Push, Branch, Stash, Pop. At the right end: Search, Theme. All are dimmed (disabled) except Theme |
| Left panel | Left, 260 wide, from the toolbar down to the status bar | A filter box with the hint "Filter", then five section headers in this order: "Local branches", "Remotes", "Pull requests", "Tags", "Stashes", each with the count "0" at its right |
| Commit graph | Centre, all the width between the panels | A column-header row: "Branch / Tag", "Graph", "Message", "Author", "Date", "SHA". Below it, centred: "No repository open" and under that, smaller, "Open, clone or init a repository to see its history." |
| Right panel | Right, 400 wide, same height as the left panel | The header "Commit details" and, centred, "Select a commit to see its details." |
| Status bar | Bottom edge, full width, 26 high | At the left "No repository" and "Ready". At the right "Git" followed by the version that `git --version` reports on the machine, then "Activity log" |

No text is clipped, cut off or overlapping, and no region is empty.

Colours, sampled from an empty spot of each region:

| Region | Dark theme | Light theme |
|---|---|---|
| Repo tabs, toolbar, status bar, graph column headers | `#20242C` | `#EBEDF1` |
| Left and right panels | `#1A1D24` | `#F5F6F8` |
| Commit graph area | `#14161B` | `#FFFFFF` |
| Main text | `#E4E7EC` (light on dark) | `#1B1F27` (dark on light) |
| Accent (line on the tab) | `#7C8CFF` | `#4353D8` |

The Theme button shows a sun icon in the dark theme and a moon icon in the light theme: the icon
is the theme the button switches to.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes).

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| 1 | Shell, dark, 1100×700 | 1. Start the app on a new, empty data folder with a 1100×700 window. 2. Wait for git detection to finish. 3. Screenshot. | The app opens in the dark theme without being told to (dark is the default). The window shows the layout and the dark colours in "What the shell must show". The graph is 440 wide (1100 − 260 − 400). | | | | |
| 2 | Shell, dark, 1920×1080 | As check 1 with a 1920×1080 window. | Same content and colours as check 1. The left panel is still 260 wide and the right panel 400; the graph takes the extra width (1260) and the panels the extra height. Tabs, toolbar and status bar keep their heights. Nothing is stretched or clipped. | | | | |
| 3 | Shell, light, 1100×700 | 1. Start the app with a 1100×700 window on a data folder whose `settings.json` holds the light theme. 2. Wait for git detection. 3. Screenshot. | Same layout and text as check 1, in the light colours in "What the shell must show". No region is left in dark colours. The Theme button shows the moon icon. | | | | |
| 4 | Shell, light, 1920×1080 | As check 3 with a 1920×1080 window. | Same layout as check 2, in the light colours. | | | | |
| 5 | Theme switch | 1. Start as in check 1 (dark). 2. Click the Theme button with a simulated mouse click at its centre. 3. Screenshot. 4. Click it again. 5. Screenshot. | After step 2 the whole window is in the light theme, the same as check 3, and the Theme button's icon has changed from sun to moon. `settings.json` in the data folder says the theme is Light. After step 4 the window is dark again, the same as check 1, and `settings.json` says Dark. | | | | |
| 6 | Theme survives a restart | 1. Start on a new, empty data folder (dark). 2. Click the Theme button (light). 3. Close the app. 4. Start a new instance on the same data folder. 5. Screenshot the first frame it shows. | The new instance opens in the light theme straight away, the same as check 3. `settings.json` still says Light. The log file in the data folder's `logs` folder has one start-up line for each of the two starts. | | | | |
| 7 | Panels resize | 1. Start as in check 1. 2. Press the mouse on the right edge of the left panel, drag 60 to the right, release. 3. Screenshot. 4. Press on the left edge of the right panel, drag 60 to the left, release. 5. Screenshot. | After step 2 the left panel is 320 wide and the graph 380; the right panel is unchanged. After step 4 the right panel is 460 wide and the graph 320. Panel content follows the new widths; nothing overlaps. | | | | |
| 8 | Real window matches scripted | 1. Build the app in Release. 2. Run the real-window pass. It starts the built app on a new, empty data folder and sets the window's client area to 1100×700. 3. Screenshot A. 4. It moves the real mouse to the Theme button and clicks. Screenshot B. 5. It closes the window, starts the app again on the same data folder. Screenshot C. 6. If a 1920×1080 window fits the screen at its scaling, it repeats step 3 at that size (screenshot D). | A shows the same layout, text and state as the scripted screenshot of check 1; B the same as check 5 after step 2; C the same as check 6; D the same as check 2. The only differences are the operating system's window frame, with the title "VisualCommit", and small differences in font rendering. After B, `settings.json` says Light. | | | | |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say "n/a".

## Changes to expected results

Any expected result changed after it was first committed, with the reason.

## Could not run

Checks or parts of the gate that could not run, why, and what that leaves unproven.

## macOS checklist for the owner

A short list of things to try by hand on a Mac for this phase.
