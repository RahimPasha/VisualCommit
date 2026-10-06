# Phase 0 visual test report — Foundation

The Check, Steps and Expected columns are written at the start of the phase, before the UI they
test is built. The result columns are filled in when the gate runs.

## Run

| | |
|---|---|
| Date | 2026-10-06 |
| App commit | `cfa3193` on `phase/0-foundation`, clean working tree |
| Machine | Windows 11 Pro 10.0.26300, Git 2.36.0.windows.1, .NET SDK 10.0.303 |
| Display | 3000×2000 pixels at 200% scaling: a work area of 1500×952 logical pixels |
| Scripted walk-through | `dotnet test`: 111 tests passed, 7 of them the checks below. Screenshots in `artifacts/visual/phase-0/scripted/` at a scaling of 1 |
| Real-window pass | `dotnet test --project tests/VisualCommit.RealWindowTests -c Release`: passed in 22 seconds. Screenshots, difference pictures and `run.txt` in `artifacts/visual/phase-0/real-window/`, at 200% (a 1100×700 window is 2200×1400 pixels) |
| Inspection | All 22 pictures were opened and compared with the expected results: 14 scripted, 5 real-window, 3 difference pictures. Files with the same content are listed together under "Screenshots" |
| Other platforms | CI ran the scripted walk-through on macOS and Linux as well; see "Other platforms" below |
| Re-run after later fixes | The app's code changed after `cfa3193`: stopping git was hardened, the filter box's automation id was renamed and unused font-size tokens were removed. Both passes were run again on `45f3e85`: 117 tests passed (6 were added), and the real-window pass passed with the same differences. Every picture is byte-for-byte the same as the one inspected before, except `06c-restarted-first-frame`, which was opened again; see check 6 |

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
| 1 | Shell, dark, 1100×700 | 1. Start the app on a new, empty data folder with a 1100×700 window. 2. Wait for git detection to finish. 3. Screenshot. | The app opens in the dark theme without being told to (dark is the default). The window shows the layout and the dark colours in "What the shell must show". The graph is 440 wide (1100 − 260 − 400). | Pass | Pass | `01-shell-dark-1100x700`; real window `A-first-start-dark`, `A-first-start-dark-with-frame`, `A-first-start-dark-difference` | The real-window screenshot differs from the scripted one in 0.65% of pixels, all on the edges of letters. |
| 2 | Shell, dark, 1920×1080 | As check 1 with a 1920×1080 window. | Same content and colours as check 1. The left panel is still 260 wide and the right panel 400; the graph takes the extra width (1260) and the panels the extra height. Tabs, toolbar and status bar keep their heights. Nothing is stretched or clipped. | Pass | Not run | `02-shell-dark-1920x1080` | A 1920×1080 window does not fit this screen; see "Could not run". |
| 3 | Shell, light, 1100×700 | 1. Start the app with a 1100×700 window on a data folder whose `settings.json` holds the light theme. 2. Wait for git detection. 3. Screenshot. | Same layout and text as check 1, in the light colours in "What the shell must show". No region is left in dark colours. The Theme button shows the moon icon. | Pass | n/a | `03-shell-light-1100x700` |  |
| 4 | Shell, light, 1920×1080 | As check 3 with a 1920×1080 window. | Same layout as check 2, in the light colours. | Pass | n/a | `04-shell-light-1920x1080` |  |
| 5 | Theme switch | 1. Start as in check 1 (dark). 2. Click the Theme button with a simulated mouse click at its centre. 3. Screenshot. 4. Click it again. 5. Screenshot. | After step 2 the whole window is in the light theme, the same as check 3, and the Theme button's icon has changed from sun to moon. `settings.json` in the data folder says the theme is Light. After step 4 the window is dark again, the same as check 1, and `settings.json` says Dark. | Pass | Pass (first click) | `05a-theme-switch-before` (same picture as 01), `05b-theme-switch-after-first-click`, `05c-theme-switch-after-second-click`; real window `B-after-theme-click-light`, `B-after-theme-click-light-with-frame`, `B-after-theme-click-light-difference` | In 05b, 05c and B the Theme button has its hover highlight, because the pointer rests on it after the click. Apart from that 05b equals 03 and 05c equals 01. B differs from 05b in 0.63% of pixels. In the real window the title bar turns light with the theme. |
| 6 | Theme survives a restart | 1. Start on a new, empty data folder (dark). 2. Click the Theme button (light). 3. Close the app. 4. Start a new instance on the same data folder. 5. Screenshot the first frame it shows. | The new instance opens in the light theme straight away, the same as check 3. `settings.json` still says Light. The log file in the data folder's `logs` folder has one start-up line for each of the two starts. | Pass | Pass | `06a-first-start-dark` (same picture as 01), `06b-light-before-closing` (same as 05b), `06c-restarted-first-frame`, `06d-restarted-ready` (the same picture as 03); real window `C-restarted-light`, `C-restarted-light-difference` | The first frame after the restart is light in both runs. In the first run it was identical to the settled one, because finding git had already finished; in the re-run the frame came earlier and its status bar still says "Looking for Git...", with everything else the same. The log has two start-up lines. In the real window the app process was really closed and started again; C differs from 06d in 0.63% of pixels. |
| 7 | Panels resize | 1. Start as in check 1. 2. Press the mouse on the right edge of the left panel, drag 60 to the right, release. 3. Screenshot. 4. Press on the left edge of the right panel, drag 60 to the left, release. 5. Screenshot. | After step 2 the left panel is 320 wide and the graph 380; the right panel is unchanged. After step 4 the right panel is 460 wide and the graph 320. Panel content follows the new widths; nothing overlaps. | Pass | n/a | `07a-before-resizing` (same picture as 01), `07b-left-panel-dragged`, `07c-right-panel-dragged` | In 07c the graph is at its narrowest (320): all six column headers are still readable and the explanation under "No repository open" wraps onto two lines. |
| 8 | Real window matches scripted | 1. Build the app in Release. 2. Run the real-window pass. It starts the built app on a new, empty data folder and sets the window's client area to 1100×700. 3. Screenshot A. 4. It moves the real mouse to the Theme button and clicks. Screenshot B. 5. It closes the window, starts the app again on the same data folder. Screenshot C. 6. If a 1920×1080 window fits the screen at its scaling, it repeats step 3 at that size (screenshot D). | A shows the same layout, text and state as the scripted screenshot of check 1; B the same as check 5 after step 2; C the same as check 6; D the same as check 2. The only differences are the operating system's window frame, with the title "VisualCommit", and small differences in font rendering. After B, `settings.json` says Light. | n/a | Pass for A, B and C. D not run | See checks 1, 5 and 6 | UI Automation found every text and every toolbar button under its name, with only Theme enabled. The Theme button was clicked with the real mouse. `settings.json` said Light after B and after C. D needs a 1920×1080 window, which does not fit this screen. |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say "n/a".

## Changes to expected results

None. The expected results are as first committed in `c2f1f63`, before the UI existed.

Two things about how the checks are run, decided after that commit and recorded in
`decisions.md`: a restart in the scripted walk-through is a new Avalonia application object in the
same test process (D32), and a real-window screenshot matches when at most 3% of its pixels differ
from the scripted one and inspection agrees (D33).

## Found by the gate

- The first real-window run failed check 8: the toolbar buttons had no name for UI Automation, so
  a screen reader would not have read them either. They now expose their label. Fixed in `9fd6555`.
- Two bugs were found earlier in the phase by the automated tests, not by the gate: cancelling a
  git call left child processes running on Windows, and two log writers could overwrite each
  other's entries. Both are fixed (`f828377`).

## Could not run

- **Real window at 1920×1080 (screenshot D of check 8, and the real-window column of check 2).**
  The development screen has a work area of 1500×952 logical pixels at 200% scaling, so a
  1920×1080 window does not fit. The plan asks for this size only "when that fits the screen", so
  this does not fail the gate. Unproven: the real window at that size. The scripted walk-through
  covers the size in both themes (checks 2 and 4), and the real window passes at 1100×700. The
  pass takes screenshot D by itself on a screen that is large enough.
- **Real windows on macOS and Linux.** The real-window pass is Windows-only by design (D7, D19).
  See "Other platforms" for what CI shows and the macOS checklist for what the owner can try.

## Other platforms

CI run [37437294860](https://github.com/RahimPasha/VisualCommit/actions/runs/37437294860) built
and tested the gate's commit, `cfa3193`, on `windows-latest`, `macos-latest` and `ubuntu-latest`.
All 111 tests passed on each. That includes the seven scripted checks with their assertions on
layout, text, colours and cut-off text, and the pinned commit SHAs of the scenario repos, which
are therefore the same on all three platforms.

The run uploaded each platform's 14 scripted screenshots. Two of them were opened and inspected:
check 1 (dark, 1100×700) from macOS and check 3 (light, 1100×700) from Linux. Both show the same
layout, text and colours as on Windows. Letters are drawn slightly differently by each platform's
font renderer, and the status bar shows that runner's own Git (2.55.0). The other CI screenshots
were checked by the automated assertions only.

One earlier CI run, for commit `9fd6555`, hung in the test step on macOS and left no log. The runs
before and after it passed there in about 12 seconds. It is an open issue; see the phase's handoff.

## macOS checklist for the owner

Needs the .NET 10 SDK and Git. In the repo folder run `dotnet run --project src/VisualCommit.App`.

1. A window titled "VisualCommit" opens in the dark theme, and the menu bar shows the app as
   "VisualCommit".
2. The window shows the regions in "What the shell must show". Text is sharp on a Retina display
   and nothing is cut off.
3. The status bar says "Git" and the version that `git --version` prints in Terminal.
4. Click Theme: the whole window turns light. Click again: dark.
5. Choose the light theme, quit with Cmd+Q, start again: it opens light.
6. Drag the right edge of the left panel and the left edge of the right panel: the panels resize
   and stop at a minimum and a maximum width.
7. Resize the window down to its smallest size: nothing overlaps or is cut off.
8. `~/Library/Application Support/VisualCommit` holds `settings.json` and a `logs` folder with
   today's log.
