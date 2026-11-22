# Phase 8a visual test report — Release readiness in the app

The Check, Steps and Expected columns are written before the UI they test is built: since D244
each part's checks are written and committed just before its work package is built. The result
columns are filled in when the gate runs.

Phase 8a's checks come in eight parts (`docs/phase-8a-design.md` 0.3, 0.5): **L** the contrast round
(§2), **I** one instance per data folder (§4), **F** first run (§5), **K** keyboard (§6), **B** the icon
(§7), **A** About and the notices (§8), **U** screen readers (§9) and **G** the guide's links (§11). This
first version holds parts L and I, written on 2026-11-20 while WP0 was being built and before wave 1's
§2 and §4 started, from the design's sections 0, 1 (1.5, 1.11, 1.14, 1.15), 2, 4 and 12 (12.2, 12.3,
12.5), decisions D410, D418, D419, D421, D422, D442, D443 and D448, rulings 24 to 31 and 63, phase 6's
part L (its report and `Phase6Checks.L*.cs`) for how a colour is asserted, and the views, tokens and
scenarios as they are on `bea1bfb`. No UI of either part existed: every colour is 1.5's or a token's
that the round keeps, every text of the notice is 4.3.4's and every log line 4.3.5's; the texts the
checks only pass through (a placeholder, a dialog's title, a menu item) are today's, quoted under each
part's "Texts". "What the checks share" holds what every part relies on, then each part's conventions,
methods, steps and texts; "Checks" is the table of L1 to L6 and I1 to I3 (design 2.5's and 4.6's lists,
in their order), then of each later part's checks; "Checks by hand" lists what a person does at the gate;
"Changes to expected results" says what each part
changes on purpose; "Open points" first says what the lead settled on 2026-11-20, where the design
disagreed with itself, then lists where the design is silent or leaves a value to its builder, with the
reading this report takes, then the same for each later part. The review's findings were applied on
2026-11-20, after the lead's rulings of that day.

Parts F (14 checks), K (8) and B (2) were added on 2026-11-20, just before wave 2 builds §5, §6 and §7
(design 0.4), each written, reviewed and corrected on its own: F from the design's sections 0, 1 (1.2,
1.3, 1.4, 1.10, 1.14, 1.15, 1.17), 5, 12 (12.3.4 launches 7 to 10) and 13 (rulings 4 to 9 and 40 to 48;
13.4 items 6, 7 and 22) and decisions D405, D423, D424, D449 and D453; K from sections 0, 1 (1.2, 1.8,
1.15, 1.17), 6 and 12 (12.3.4 launches 11 and 12), decisions D417, D425, D426, D427 and D452 and the
research's `keyboard.md`; F and K from the code as it stands on `85c76c6` for the texts the checks only
pass through; B from sections 0, 1 (1.5, 1.6, 1.15, 1.17), 2.3, 7, 8.5, 12 (12.3.4 launch 3) and 13
(13.3; 13.4 items 10 and 11), decisions D407, D428, D429, D430 and D447, round 2's masters, README and
`small-zoom.png` (`docs/brand/icon/round-2/`) and phase 7b's three framed captures. Their open points
are numbered on from part I's: F 7 to 29, K 30 to 43, B 44 to 55.

Parts A (6 checks) and U (3) were added on 2026-11-21, just before wave 3 builds §8 and §9 (design
0.4), each written, reviewed and corrected on its own, then settled by the lead's rulings of that day:
A from the design's sections 0 (0.1, 0.3, 0.5, 0.7), 1 (1.6, 1.7, 1.8, 1.10, 1.13, 1.15, 1.17), 8, 12
(12.2, 12.3.1 to 12.3.5, 12.5, 12.9) and 13 (rulings 3, 14, 15, 54 to 59; 13.4 item 12) and decisions
D398, D409, D417 and D431 to D434, with the code as it stands on `eb16fb1` for the texts and places
the checks only pass through, its steps filling part B's launch 3 (A1 and A4); U from sections 1
(1.12, 1.15, 1.17), 9, 12 (12.3.4 launch 13, 12.5, 12.9) and 13 (13.3; 13.4 items 13 to 16), decisions
D49, D411, D435, D445 and D450, with the code as it stands on `d2318ba` for the texts the checks only
pass through. Their open points are numbered on from part B's: A 56 to 74, U 75 to 92.

The last part, G (3), is added just before its package is built (D244), as design 0.5 lists it.

## Run

Not yet.

## Live command

Not yet.

## What the checks share

### Conventions of every part

What every part of phase 8a relies on; each part's conventions add to these.

- **Parts and the project** (design 0.3, 0.5, 1.1, 1.15; D416, D230). One project,
  `tests/VisualCommit.VisualTests.Phase8a` (WP0's), about 45 checks for the phase, one class per part,
  `Phase8aChecks<Part>` (`Phase8aChecksL`, `Phase8aChecksI`, …, in `Phase8aChecks.<Part>[.<topic>].cs`)
  on the base `Phase8aChecks` and its support `Phase8aChecks.Support.cs`. What a part compares against
  is in `Phase8aExpectations.<Part>.cs` (`Phase8aExpectations.L`, `Phase8aExpectations.I`: texts,
  colours, ids, picture names and, for §12, each R step's session), written from this report and not
  from the app's texts, and its tests in `Phase8aExpectationTests.<part>.cs`;
  `Phase8aExpectations.Common.cs` holds `Phase = "8a"`, the sizes 1100×700 and 1920×1080,
  `PictureName` and `PinnedVersion`. The real-window pass links `Phase8aExpectations*.cs`, so those
  files use nothing of the 8a project. 8a's tests that start the app through `ShellDriver` are in this
  project (D416). Check names are the part's letter and its number in its design list; test methods
  are `Check_<ids>_<what>`. Each method stays under about 40 s alone, timed with
  `bash scripts/time-scripted-check.sh "<method>" 8a`; if part L alone passes 8 minutes, the lead moves
  it into a project of its own, as D230 did for phase 6.
- **One test method per app start** (phase 3's rule, `FreshApplication`), each on a fresh copy of its
  scenario and a fresh data folder. A notice shown through `InstanceNoticeHost` starts no session.
- **Defaults** (0.5). Dark and 1100×700 unless a check names light, 1920×1080 or the notice's own
  size. Every session has what phase 6's 17.2 and 7b give it: `GitIsolation`'s variables, the three
  providers off, `VISUALCOMMIT_KEYCHAIN=memory`, and since 8a the stand-in's three variables
  (`VISUALCOMMIT_GIT_STAND_IN`, `…_VERSION`, `…_FAILURE`) removed by `GitIsolation.MachineIndependence`
  (1.14). No test reads the machine's `PATH`, home folder, git or settings (section 1's rules).
- **The version** (1.6, 1.15; ruling 59). Every headless start gets `TestIdentity.Pinned`, so a
  scripted picture that shows the version shows `0.1.0 (1a2b3c4)` (`Phase8aExpectations.PinnedVersion`,
  in `Phase8aExpectations.Common.cs`) on every commit; the real window shows the built commit's, seven
  characters apart from its twin (part A). Parts L and I show no version.
- **Made-up data only** (ruling 63). Authors at `example.com`, paths under `/home/demo` or
  `C:\Users\demo`; the owner's name only where D409 and `LICENSE` put it (About, part A), never with an
  e-mail address. A picture that shows a temporary folder's name varies by nature, and its Notes say so.
- **Texts** (section 1's rules; ruling 58). On screen the American spelling ("License", "color").
  Every text the app shows is a constant of one texts class per area (`InstanceNoticeTexts` for part
  I), which the part's expectation tests compare with this report word for word. `…` is the single
  character U+2026. Keys are written as Windows shows them; the headless platform's command modifier is
  Control on every platform, CI's macOS job included (phase 6's rule). On screen the spelling is American
  for every text phase 8a adds, and the app's earlier texts stay as they are (`Fetch origin cancelled`,
  D113, which part K reads).
- **The guard in the harnesses** (1.11; D421). The single-instance guard runs only in `Program.Main`:
  `ShellDriver` calls `AppSession.Start` itself and never meets it, and each real-window launch has a
  data folder of its own, which gains `instance.lock` and `instance.json` and one Info line in its log
  (4.7). No check lists a data folder's files but I2's real-window launch.
- **Pictures** (0.3). `Shot(app, name)` saves under `artifacts/visual/phase-8a/scripted/` (step times
  under `artifacts/visual/phase-8a/timings/`); the real window's captures, their difference pictures and
  `run.txt` land under `artifacts/visual/phase-8a/real-window/`. The guide's pictures
  (`artifacts/guide/pictures/`) and the performance series (`artifacts/perf/phase-8a/`) lie outside
  every list of pictures. Names match `Phase8aExpectations.PictureName`,
  `^[abfgiklu]\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`: the part's letter, the check's number
  (`l1a`, `i2c`), a step letter running on through the check's methods in the order of its part's
  methods table, then words. Every name is unique in the report, and `Shot` refuses one saved twice in a
  run. Before a capture with no menu open and no hover asked for, `Shot` rests the pointer at the
  neutral point (760, 687 at 1100×700, in the status bar); "pointer kept" (`keepPointer`) leaves it
  where the step put it. "Rest the pointer on X until its tooltip shows" is `RestPointerOnAsync(X)` in
  the scripted run and `RealApp.RestPointerOn(X, its tooltip)` in the real window: one move onto X's
  centre, then a wait until X's tooltip is open; the capture keeps the pointer there and shows the
  tooltip, and the phrase is written only where a tooltip opens. "Move the pointer onto X" is
  `MoveMouse(X)` (the real window's `SetPointer`) alone, with no wait, written only where no tooltip
  opens there or where no picture is taken before the pointer moves on (phase 6's conventions). "Rest
  the pointer at the neutral point" is `MoveToNeutral()`. Every window picture passes
  `LayoutAudit.FindClippedText` (shortened text that shows whole in its tooltip is allowed). Part B's
  pictures carry no step letter: they keep the design's names (`b1-icon-sizes-dark`; 7.5, 12.3.4),
  which the pattern allows (open point 53).
- **Colours** (2.3, 2.5; phase 6's part L). Written by token with their values, dark and light. A
  text's colour is asserted by `TextInk.Problems` (`tests/VisualCommit.Testing/TextInk.cs`, tolerance 8:
  within the text's rectangle each channel's extreme toward the text colour lies within 8 of it, and
  more than 8 from the "replaced" colour where one is given) and by the control's brush read in the
  tree; a background as the exact colour at a sampled point clear of text; a backdrop within ±8 per
  channel of the blend. A box's rectangle holds its text alone: the template's `PART_Placeholder` (or the
  typed text), less the caret's column while the box has the focus. Ratios are WCAG 2's
  (`tests/VisualCommit.Testing/Contrast.cs`); the figures quoted are 1.5's.
- **Waits** are on conditions, never on a fixed time (`WaitUntilReadyAsync`, `WaitForIdleAsync`,
  `WaitForDialogAsync<T>()`, `Menu()`, `WaitForSettingsReadsAsync()`, the theme's `ShowsDarkTheme`).
- **Nothing confirmed** (2.5). A check that opens and cancels takes its repository's
  `ExactStateAsync()` before its first step and asserts it unchanged after each cancelled dialog and at
  its end.
- **R steps** (0.5, 1.15, 12). A step marked R is repeated in the real window. Its picture names,
  texts, colours and the session it starts from are constants in `Phase8aExpectations.<Part>.cs`, and its
  state is built only by code both projects compile.
- **The real window** (design 12; D33, D58, D224, D442, D443 as D448 amends it, D444). `Phase8aRealWindowPass`
  runs 13 methods, one launch each, in `Phase8aOrder` (L 2, A with B 1, I 3, F 4, K 2, U 1), and I1's
  second process. Each method first reads `RealApp.HighContrastIsOn()`; when it is on, the method fails,
  and run.txt says, `Windows' high contrast is on. The pass needs it off and never changes it (D224):
  turn it off and run again.` Each launch starts on a new data folder whose session holds exactly its
  twins' tabs and starting theme (D442), written before the start (`RealApp.Launch(D, session)`; a
  launch that shows the notice writes none, since its twin has none), sized 1100×700 (`Sized`; 200 %
  on the laptop) unless it shows the notice, with the scenario's copy declared before the app; it
  waits on named conditions with a limit; it rests the real pointer at the neutral point before a
  capture unless a menu is open or the step hovers; it captures the client area under the scripted
  twin's name and compares it with the twin, at most 3 % of the pixels different (the share in
  run.txt), the twin having to exist on the machine; and it ends with the app closed by its window and
  exit code 0, unless the method asserts another.
  The notice's captures go through `CompareMasked`: the capture's height divided by the scaling must
  equal the twin's within 1 px, else the method fails with `The notice is {h} high and its scripted
  twin {t}: their layouts differ, so the masked comparison cannot hold.`; then `InstanceNoticeFolder`'s UI
  Automation bounds are painted one flat colour in the capture and, divided by the scaling, in a copy of
  the twin in the test's temporary folder (the scripted file never changes), and the two are compared at
  3 %; the folder's text is asserted through UI Automation. Ink in a capture (`InkOf`, 12.3.3) is
  `TextInk.Of`'s rule at the capture's scale, with `TextInk.Tolerance` and the twin's "replaced"
  colour, in the element's UI Automation bounds inset to hold the text alone; a box is read unfocused,
  except the text typed into the focused `FilterBox` in launch 1 (L1's and L2's `sea`), which is read
  less the caret's column (12.3.3; the lead's ruling of 2026-11-20); where no such rectangle exists the
  comparison carries the colour and run.txt says so; a miss is reported to the lead, never met by a
  wider tolerance. No step presses anything that leaves the app (12.3.2); parts L and I touch none of
  its controls. No key is sent to a window that is not the foreground window: launch 3 (part A) and
  part I's launches 5 and 6, whose first input is a key, assert first that their window, the main
  window or the notice, is the foreground window (12.3.3, 12.3.4).
  Part B's six real-window captures have no scripted twin and are compared with none; they are
  inspected (7.5, 12.5).
- **The lagged list** (0.4, D262). 8a's project is on it: at each wave's last merge and at the merges
  of §5, §6, §9 and §10 the lead runs it whole under `VISUALCOMMIT_WATCHER_LAG_MS=1500`. A failure under
  the lag gets a fix, never a looser test.
- **Earlier pictures** (0.7; D419; ruling 3). Nothing earlier changes but what rulings 3, 13, 40, 54
  and 62 name. §2's comparison runs phase 7b's references
  (`D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/`) against wave 1's whole run with the
  scan's boxes as the expected regions; a difference outside them is a finding. Once it passes, that
  run's scripted pictures are the references (`D:/Projects/Personal/phase8-work/references/wave1/`) of
  the later merges until wave 2's close; from then every later merge and the gate compare against the
  references of the last closed wave (wave 2's from its close,
  `D:/Projects/Personal/phase8-work/references/wave2/`, and after wave 3's close wave 3's; open point
  68) with `scripts/compare-pictures.py --expected <file>`, whose summary has the headings "Changed
  outside the expected regions", "Changed, not expected", "Expected, not changed" and "Changed size".
  Two of part L's own pictures change at later merges, `l3a-palette-placeholder` (§8's and §11's) and
  `l3f-settings-name-placeholder` (§5's and §8's), and the lead's `--expected` file for each names them
  (0.7). A third picture of 8a's own changes at later merges, part K's `k3a-palette-cancel`, at §8's
  and §11's in the palette's scroll thumb, and the lead's `--expected` file for each of those merges
  names it (0.7). Three more change at §8's merge, part F's `f12a-row-next-start`, `f12b-row-too-old`
  and `f14a-netrc-note`, in the About button's region (x 140–317, y 255–285 at 1100×700): §8's merge
  changes six of 8a's own pictures, and the lead's `--expected` file for it names all six (0.7; open
  point 67). The real-window captures are compared with phase 7b's at the gate. Every picture is
  inspected (D247).

### Part L: the contrast round

Part L covers D410's three fixes with D418's tokens (design 1.5, 2.1, 2.3): every text box's
placeholder in its own token at full opacity, in both themes (L1 to L3); the dark danger colour on
hovered, selected and control backgrounds (L4) and the dark label on the danger button (L5); and
`VcAccentOnRowBrush` on HEAD's name and on a renamed file's "R" while the row is hovered or selected, in
the five views that take it (L6). Checks L1 to L6, design 2.5's list. Its R steps are L1, L2, L4 a, L5
dark and L6 a to c (2.5, 12.3.4 launches 1 and 2): nine captures in two launches.

#### Conventions of part L

- **Code** (2.2): `Phase8aChecks.L.cs` and `Phase8aChecks.L.Helpers.cs`; `Phase8aExpectations.L.cs`
  (the colours below, the colours they replace, the texts, the picture names, and for §12 the two
  launches' sessions, names and colours); `Phase8aExpectationTests.L.cs`, which asserts each new
  colour's ratio of 4.5:1 or more on every background a check reads it on, that each new colour is
  more than 8 from the one a check tells it from (placeholder and secondary 12, accent on a row and
  accent 15, danger and lane 3 29), and that every name matches 0.3's pattern; `ShellDriver.Phase8a.Colours.cs`
  only if a helper is needed.
- **Colours by token** (1.5, D418; `Theme/Tokens.axaml`, whose other values the round keeps):

  | Token | Dark | Light | Replaced (dark; light) | Ratio (1.5) |
  |---|---|---|---|---|
  | `VcTextPlaceholderBrush` and the three `TextControlPlaceholderForeground…` keys | `#8F97A4` | `#68707D` | `#616773`; `#ADB2B8` (the secondary at Fluent's opacity 0.5) | 4.75 on `#272C36`; 5.00 on `#FFFFFF` |
  | `TextControlPlaceholderOpacity` | 1 | 1 | 0.5 | |
  | `TextControlBackground` (a box) | `#272C36` | `#FFFFFF` | kept | |
  | `TextControlForeground` (typed text) | `#E4E7EC` | `#1B1F27` | kept | 11.30; 16.51 |
  | `VcTextSecondaryBrush` (the Summary counter) | `#9BA3B0` | `#5C6572` | kept | 5.51; 5.90 |
  | `VcDangerBrush` | `#EA7D86` | `#B7323D` | `#E5606B`; kept | dark 4.55 hover, 4.70 selection, 5.18 control; light 4.57 hover |
  | `VcDangerTextBrush` (the danger button's label) | `#10121A` | `#FFFFFF` | `#FFFFFF`; kept | 6.92; 5.94 |
  | `VcAccentOnRowBrush` (new) | `#8B9AFF` | `#4353D8` | `#7C8CFF` (`VcAccentBrush`); the same as light `VcAccentBrush` | dark 4.78 hover, 4.94 selection; light 4.66 hover |
  | `VcAccentBrush`, `VcLane3Brush` | `#7C8CFF`, `#E5606B` | `#4353D8` (accent) | kept | |
  | `VcPanelBackgroundBrush` | `#1A1D24` | `#F5F6F8` | kept | |
  | `VcHoverBackgroundBrush` (a hovered row, a highlighted menu item) | `#2F3542` | `#DEE2E9` | kept | |
  | `VcSelectionBrush` (a selected row, the palette's highlighted row) | `#2A3150` | `#DDE1FA` | kept | |
  | `VcControlBackgroundBrush` (`VcActionButton` at rest) | `#272C36` | `#FFFFFF` | kept | |
  | `VcBackdropBrush` | `#99000000` | `#66000000` | kept | |

- **How each colour is read.** A step names the text, its colour, the background it lies on and, where
  the round changed it, the colour it replaces. The background is the exact colour at a point of the
  row, box or button clear of text; the text's ink is `TextInk.Problems` in its rectangle; its brush is
  read in the tree: a box's `PART_Placeholder` is visible, with `Opacity` 1 and `Foreground` the
  token's colour; a status letter's, a ref name's, a menu item's, a palette row's and a button's
  `Foreground` resolve to the colour the step names. A colour unchanged by the round (the typed text,
  the counter) is asserted without a "replaced" colour. `CommitSummary`'s rectangle ends at the box's
  right padding, 40 px from its right edge, so that `SummaryCounter`, drawn over the box's right end,
  lies outside it, in both harnesses: the scripted `PART_Placeholder` ends there already, and the real
  window's inset takes the 40 px (the counter's ink lies further toward the foreground than the
  placeholder's in both themes, so a rectangle that held it would read the counter).
- **Shown by no check** (13.4 item 2, settled). Of the dark danger's uses (2.3), part L shows the "D"
  letter (L4 c), a destructive menu item (L4 a), a destructive palette entry (L4 b), the Remotes dialog's
  "Remove" (L4 d) and the danger button (L5); the "U" letters of the stage panel and the resolver, the
  failures in the status bar, the activity log and the dashboard, the error paths and the settings
  section's error dot are proven by `ThemeTokensTests`' table (danger text on window, panel, chrome,
  control, hover, selection and row hover) and by the masked comparison over the scan's 360 dark-danger
  pictures, and shown by no check. Of the 19 boxes with a placeholder (2.3), part L shows eleven
  (`FilterBox`, `CommitSummary`, `CommitDescription`, `CommandPaletteBox`, `SearchBox`, `PickFilter`,
  `NameBox`, `RemoteNameBox`, `RemoteUrlBox`, `UserNameBox`, `UserEmailBox`); the other eight
  (`MessageSummary`, `MessageDescription`, the Name dialog's message box, the GitHub browser's filter,
  `PullRequestTitle`, `PullRequestDescription`, `PushBranchBox`, `RunnableExtraBox`) take the same keys,
  which `ThemeTokensTests.The_placeholder_has_its_own_token_at_full_opacity` and `PanelViewTests`'
  placeholder test prove reach the template, and the comparison covers their earlier pictures. A
  disabled box keeps Fluent's background (ruling 29) and no check shows one. All five styles of
  `VcAccentOnRowBrush` are shown (L6).
- **Routes** are those earlier checks use: the working-changes row by a click on graph row 0 (phase
  2); `Ctrl+Shift+E` to the left panel's filter (phase 6's keys); a branch's menu from its row in the
  left panel (phase 3); the palette by `Ctrl+Shift+P` with the pointer at the neutral point (phase 6's
  L31); the search bar by `Ctrl+F` (phase 3); "Set upstream…" (phase 3's check 28), "Create branch
  here…" (phase 3's check 1) and "Manage remotes…" (phase 3's check 22); the settings screen by
  `Ctrl+,` and its `Git` section (phase 6's part S); the comparison by a drop of one label on another
  (phase 5's FD11); "File history" from a file row's menu (phase 5). Every dialog is closed unconfirmed.

#### Test methods and app starts (part L)

Seven methods, seven starts, each on a fresh data folder. Methods 1 and 2 are the twins of the real
window's two launches and take their steps in the same order.

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_L1_L5_L2_changes_placeholders_and_danger_button` | L1, L5 dark, L2, L5 light | one tab on a fresh "changes" (`Scenarios.ChangesAsync()`) | 1100×700, dark; light from step 8 (`ThemeSwitch`) | `l1a`, `l1b`, `l5a`, `l2a`, `l2b`, `l5b` |
| 2 | `Check_L4a_L6_graph_danger_item_and_head_row` | L4 a, L6 a to c | one tab on a fresh "graph" (`Scenarios.GraphAsync()`) | 1100×700, dark | `l4a`, `l6a`, `l6b`, `l6c` |
| 3 | `Check_L3_placeholders_in_every_other_box` | L3 | one tab on a fresh "graph" | 1100×700, dark | `l3a` to `l3f` |
| 4 | `Check_L4_danger_text_on_rows_and_controls` | L4 b to d | one tab on a fresh "graph" | 1100×700, dark | `l4b` to `l4f` |
| 5 | `Check_L6_renamed_file_on_rows` | L6 d to f | one tab on a fresh "graph" | 1100×700, dark | `l6d` to `l6h` |
| 6 | `Check_L6_staged_rename_on_rows` | L6 g | one tab on a fresh "changes" | 1100×700, dark | `l6i`, `l6j` |
| 7 | `Check_L6_head_row_light` | L6 h | one tab on a fresh "graph"; `settings.json` `{"theme": "Light"}` | 1100×700, light | `l6k`, `l6l`, `l6m` |

#### Steps and expected results (part L)

A step numbered in a method runs on from that method's last step in an earlier check.

**L1. Placeholders in their own token, dark** (R; D410, D418; design 1.5, 2.3, 2.5). Method 1.

Steps:
1. Open the tab on the fresh "changes"; wait until it is ready and the graph drawn. Take `ExactStateAsync()`.
2. Click graph row 0, the working-changes row; wait for the stage panel and its lists. Screenshot
   (`l1a-placeholders-dark`).
3. Press `Ctrl+Shift+E`; wait until `FilterBox` has the keyboard focus. Capture a frame (`app.Capture()`,
   not saved).
4. Type `sea`. Screenshot (`l1b-filter-typed-dark`).
5. Empty the filter (`Ctrl+A`, `Backspace`); wait until the left panel lists `main` again.

Expected:
- After step 2: `FilterBox`, `CommitSummary` and `CommitDescription` are empty, none has the focus, and
  each shows its placeholder, `Filter`, `Summary` and `Description`. In each box `PART_Placeholder` is
  visible, `Opacity` 1, `Foreground` `#8F97A4`; its ink is `#8F97A4` on the box's `#272C36`, more than 8
  from `#616773`. `TextControlPlaceholderOpacity` resolves to 1 in the dark variant. `SummaryCounter`
  reads `72`, its ink `#9BA3B0`, more than 8 from `#8F97A4`, so the counter does not read as the
  placeholder (D418).
- After step 3: `FilterBox` has the focus and is still empty; its placeholder's ink is as after step 2,
  the caret's column left out.
- After step 4: `FilterBox` holds `sea`, its ink `#E4E7EC` on `#272C36`; its `PART_Placeholder` is not
  visible.
- After step 5: `FilterBox` is empty.

**L2. Placeholders in their own token, light** (R; as L1). Method 1, after L5's step 7.

Steps:

8. Click `ThemeSwitch`; wait until the window shows the light theme (`ShowsDarkTheme` false). Screenshot
   (`l2a-placeholders-light`).
9. Press `Ctrl+Shift+E`; wait until `FilterBox` has the focus; capture a frame (not saved). Type `sea`.
   Screenshot (`l2b-filter-typed-light`).
10. Empty the filter as in step 5.

Expected:
- After step 8: the three boxes empty and unfocused, each placeholder's `PART_Placeholder` visible,
  `Opacity` 1, `Foreground` `#68707D`, its ink `#68707D` on `#FFFFFF`, more than 8 from `#ADB2B8`;
  `TextControlPlaceholderOpacity` 1 in the light variant; `SummaryCounter` `72`, ink `#5C6572`, more
  than 8 from `#68707D`.
- After step 9: on the frame before typing, the focused, empty `FilterBox`'s placeholder as after step 8,
  the caret's column left out; after typing, `sea` in ink `#1B1F27` on `#FFFFFF`, no placeholder.
- After step 10: `FilterBox` empty.

**L3. The placeholder in every other kind of box, dark** (not R; design 2.3, 2.5). Method 3.

Steps:
1. Open the tab on the fresh "graph"; wait until ready. Take `ExactStateAsync()`. Rest the pointer at
   the neutral point.
2. Press `Ctrl+Shift+P`; wait until the palette is open and `CommandPaletteBox` has the focus.
   Screenshot (`l3a-palette-placeholder`). Press Esc; wait until the palette has closed.
3. Press `Ctrl+F`; wait until the search bar shows with `SearchBox` focused, its mode `Commits`.
   Screenshot (`l3b-search-placeholder`). Press Esc; wait until the search bar has closed.
4. Click `Local branches › feature` (the folder opens); right-click `Local branches › feature › login`;
   click `Set upstream…`; wait for the Pick dialog `Set upstream for feature/login` with `PickFilter`
   focused. Screenshot (`l3c-pick-filter-placeholder`). Press Esc; wait until no dialog shows.
5. Right-click the graph row of "Add settings page" (HEAD's commit); click `Create branch here…`; wait
   for the dialog `Create branch` with `NameBox` focused. Screenshot (`l3d-branch-name-placeholder`).
   Click `CancelButton`; wait until no dialog shows.
6. Right-click the left panel's `Remotes` header; click `Manage remotes…`; wait for the Remotes dialog
   listing `origin`; click `AddRemoteButton`; wait until `RemoteNameBox` has the focus. Screenshot
   (`l3e-remote-form-placeholders`). Press Esc until no dialog shows.
7. Rest the pointer at the neutral point; press `Ctrl+,`; click `SettingsNavGit`; wait for the
   section's reads. Screenshot (`l3f-settings-name-placeholder`). Press Esc; wait until the screen has
   closed.

Expected:
- In each picture the named boxes are empty; each placeholder's `PART_Placeholder` is visible, `Opacity`
  1, `Foreground` `#8F97A4`, and its ink `#8F97A4` on `#272C36`, more than 8 from `#616773`, the caret's
  column left out of a focused box: `l3a` `CommandPaletteBox` (focused), `Search commands, branches,
  tags, stashes, remotes, repositories and workspaces`; `l3b` `SearchBox` (focused), `Message, author or
  SHA`; `l3c` `PickFilter` (focused), `Filter`; `l3d` `NameBox` (focused), `Branch name`; `l3e`
  `RemoteNameBox` (focused), `Name`, and `RemoteUrlBox`, `URL`; `l3f` the Git section on `All
  repositories (global)`, `UserNameBox` and `UserEmailBox`, unfocused, both `Not set` (the session's
  global file holds neither, as phase 6's `s14c-git-global` shows).
- After each closing step: no palette, search bar or dialog shows; `ExactStateAsync()` unchanged. After
  step 7 `settings.json` is unchanged and the session's global git file was not written (phase 6's L31
  step 5).

**L4. Danger text on hovered, selected and control backgrounds, dark** (R: a; D410; design 2.3, 2.5).
Method 2 (a), then method 4 (b to d).

Steps, method 2:
1. Open the tab on the fresh "graph"; wait until ready. Take `ExactStateAsync()`.
2. Click `Local branches › feature` (the folder opens); click `Local branches › feature › login` (the row
   is selected and has the keyboard focus). Press `Shift+F10`; wait for the menu headed `feature/login`.
   Press Down until `Delete feature/login…` is highlighted. Screenshot, pointer kept
   (`l4a-delete-item-menu`). Press Esc; wait until no menu shows.

Steps, method 4:
1. Open the tab on the fresh "graph"; wait until ready. Take `ExactStateAsync()`. Rest the pointer at
   the neutral point.
2. Press `Ctrl+Shift+P`; type `delete main`; wait until the first row reads `Delete main on origin…`
   and is highlighted. Screenshot (`l4b-delete-entry-palette`). Press Esc; wait until the palette has
   closed.
3. Click the graph row of "Add settings page"; wait for its details. Rest the pointer on the details'
   row of `docs/notes.txt` until its tooltip, `docs/notes.txt`, shows. Screenshot, pointer kept
   (`l4c-deleted-file-hovered`).
4. Click that row; wait for its diff. Screenshot (`l4d-deleted-file-selected`).
5. Right-click the left panel's `Remotes` header; click `Manage remotes…`; wait for `origin`'s row.
   Screenshot (`l4e-remove-button-at-rest`).
6. Move the pointer onto `origin`'s `RemoteRemoveButton` (it has no tooltip). Screenshot, pointer kept
   (`l4f-remove-button-hovered`).
7. Press Esc until no dialog shows.

Expected (every ink more than 8 from `#E5606B`, every `Foreground` `#EA7D86`):
- Method 2, step 2: the menu's highlighted item is `Delete feature/login…`, enabled and destructive; its
  label's ink `#EA7D86` on the highlight `#2F3542` (4.55:1).
- Method 4, step 2: the palette's first row `Delete main on origin…`, its detail `origin/main`, enabled,
  destructive (`ShowsDanger`) and highlighted: its label's ink `#EA7D86` on `#2A3150` (4.70:1).
- Step 3: the "D" (`FileStatus`) of `docs/notes.txt`, its row hovered: ink `#EA7D86` on `#2F3542`; the
  row's tooltip open, reading `docs/notes.txt`.
- Step 4: the same row selected (its diff open), the pointer at the neutral point: ink `#EA7D86` on
  `#2A3150`.
- Step 5: `RemoteRemoveButton` `Remove` at rest: ink `#EA7D86` on `#272C36` (5.18:1).
- Step 6: the same button hovered: ink `#EA7D86` on `#2F3542`.
- `VcLane3Brush` still resolves to `#E5606B` in the dark variant (the graph's lane 3 is unchanged).
- At each method's end: `ExactStateAsync()` unchanged; no backup ref, no remote removed.

**L5. The danger button: the dark label, and light kept** (R: dark; D410, D418; design 2.3, 2.5).
Method 1, after L1's step 5 (dark) and after L2's step 10 (light).

Steps, dark, after L1's step 5:

6. Move the pointer onto the unstaged list's row of `src/Calculator.cs`; wait until its
   `RowDiscardButton` shows; click it; wait for the confirmation. Screenshot (`l5a-discard-dialog-dark`).
7. Click `CancelButton`; wait until no dialog shows.

Steps, light, after L2's step 10:

11. As step 6. Screenshot (`l5b-discard-dialog-light`).
12. As step 7.

Expected:
- After step 6: the confirmation `Discard changes?` with the question `Discard the changes to
  Calculator.cs?` and the note `A snapshot is saved first, so the changes can be restored.`, over the
  backdrop; `CancelButton` `Cancel` has the keyboard focus; `ConfirmButton` `Discard` has the class
  `danger`. Its background, at a point clear of its label, is `#EA7D86` (replaced `#E5606B`); its
  label's ink `#10121A` on it (6.92:1), more than 8 from `#FFFFFF`; its `Foreground` is
  `VcDangerTextBrush`, resolving to `#10121A`. The backdrop `#99000000` over the window, asserted as
  phase 6's L31 asserts it.
- After step 11: the same dialog in light: the background `#B7323D`, the label's ink `#FFFFFF` (5.94:1),
  `VcDangerTextBrush` resolving to `#FFFFFF`, the backdrop `#66000000`, `Cancel` focused.
- After steps 7 and 12: no dialog; `src/Calculator.cs` still modified and unstaged; `ExactStateAsync()`
  unchanged.

**L6. The accent on hovered and selected rows** (R: a to c; D410, D418; design 2.2, 2.3, 2.5). Methods
2, 5, 6 and 7.

Steps, method 2, after L4 a:

3. Rest the pointer at the neutral point. Screenshot (`l6a-head-idle`).
4. Move the pointer onto the centre of `Local branches › main`'s row (`MoveMouse`, design 2.5 b). The
   centre lies right of `main`'s text, and `RefName`'s tooltip opens only over its text
   (`ShellDriver.RefRowNamePoint`), so no tooltip opens. Screenshot, pointer kept (`l6b-head-hovered`).
5. Click the row; rest the pointer at the neutral point. Screenshot (`l6c-head-selected`).

Steps, method 5:
1. Open the tab on the fresh "graph"; wait until ready. Take `ExactStateAsync()`.
2. Click the graph row of "Add settings page"; wait for its details; rest the pointer at the neutral
   point. Screenshot (`l6d-renamed-idle`).
3. Rest the pointer on the details' row of `src/main-app.txt` (from `src/app.txt`) until its tooltip,
   `src/main-app.txt` and `renamed from src/app.txt` on two lines, shows. Screenshot, pointer kept
   (`l6e-renamed-hovered`).
4. Click the row; wait for its diff; rest the pointer at the neutral point. Screenshot
   (`l6f-renamed-selected`).
5. Close the diff (`CloseDiffButton`). Drag `main`'s label in the graph onto `origin/main`'s and choose
   `Compare main with origin/main` (From `origin/main`, To `main`); wait for the comparison's file list.
   Click its row of `src/main-app.txt`; wait for its diff; rest the pointer at the neutral point.
   Screenshot (`l6g-compare-renamed-selected`).
6. Close the diff (`CloseDiffButton`), then the comparison (`CompareClose`); wait until the graph shows
   (`ShowsGraph`): closing the comparison alone leaves its diff in the graph area. Click the graph row of
   "Add settings page"; right-click its details' row of `src/main-app.txt`; click `File history`; wait
   for the page's rows. Click the row of "Add settings page", the page's first; wait until the page's
   diff of that row is read (its side label the row's short SHA, its file `src/main-app.txt`, its body
   no longer loading); rest the pointer at the neutral point. Screenshot
   (`l6h-history-renamed-selected`).

Steps, method 6:
1. Open the tab on the fresh "changes"; wait until ready. Take `ExactStateAsync()`.
2. Click graph row 0; wait for the stage panel and its lists. Rest the pointer on the staged list's row
   of `src/helpers.py` (from `src/util.py`) until its tooltip, `src/helpers.py` and `renamed from
   src/util.py` on two lines, shows. Screenshot, pointer kept (`l6i-staged-renamed-hovered`).
3. Click the row; wait for its diff; rest the pointer at the neutral point. Screenshot
   (`l6j-staged-renamed-selected`).

Steps, method 7 (light from the start):
1. Open the tab on the fresh "graph"; wait until ready, the window light. Take `ExactStateAsync()`.
2. to 4. As method 2's steps 3 to 5. Screenshots (`l6k-head-idle-light`, `l6l-head-hovered-light`,
   `l6m-head-selected-light`).

Expected:
- `main`'s name (`TextBlock.refName.head`), method 2: step 3, the row neither hovered nor selected: ink
  `#7C8CFF` (`VcAccentBrush`) on `#1A1D24`, more than 8 from `#8B9AFF`; step 4, hovered: `#8B9AFF`
  (`VcAccentOnRowBrush`) on `#2F3542` (4.78:1), more than 8 from `#7C8CFF`, and no tooltip is open
  (`ToolTip.GetIsOpen` of `RefName` false); step 5, the row `selected` (and `feature/login`'s no
  longer): `#8B9AFF` on `#2A3150` (4.94:1), more than 8 from `#7C8CFF`. Its `Foreground` is the colour
  of each step.
- The "R" (`FileStatus`) of `src/main-app.txt`, method 5: step 2 idle, `#7C8CFF` on `#1A1D24`, more than
  8 from `#8B9AFF`; step 3 hovered (its tooltip open), `#8B9AFF` on `#2F3542`; step 4 selected,
  `#8B9AFF` on `#2A3150`; in steps 2 to 4 the "D" of `docs/notes.txt` stays `#EA7D86`. Step 5: the
  comparison lists `src/main-app.txt` as renamed from `src/app.txt` (2.5 asks the builder to confirm
  it), its "R", selected, `#8B9AFF` on `#2A3150`. Step 6: the file history's row of "Add settings page"
  shows "R", selected (`ListBoxItem:selected`), `#8B9AFF` on `#2A3150`, and the page's diff shows
  `No changes to the file's content.`, as `l6f` does for the same file at the same commit (the commit
  only renames the file). Each hovered or selected "R" more than 8 from `#7C8CFF`.
- The "R" of `src/helpers.py`, method 6: step 2 hovered (its tooltip open), `#8B9AFF` on `#2F3542`;
  step 3 selected, `#8B9AFF` on `#2A3150`.
- Method 7: `main`'s name `#4353D8` in all three states (light `VcAccentOnRowBrush` equals light
  `VcAccentBrush`): on `#F5F6F8` idle, `#DEE2E9` hovered (4.66:1; no tooltip open, as in method 2),
  `#DDE1FA` selected.
- At each method's end `ExactStateAsync()` unchanged.

#### Texts these checks rely on (part L)

The round shows no new text (2.3). The texts the steps pass through, as the code has them on
`bea1bfb`: the placeholders `Filter` (`FilterBox`, `PickFilter`), `Summary`, `Description`, `Search
commands, branches, tags, stashes, remotes, repositories and workspaces`
(`CommandPaletteViewModel.Placeholder`), `Message, author or SHA` (`SearchViewModel.Placeholder`,
`Commits`), `Branch name`, `Name`, `URL`, `Not set` (`SettingsTexts.NotSet`); the counter `72`
(`WorkingChangesViewModel.SummaryLimit`); the items `Set upstream…`, `Create branch here…`, `Manage
remotes…`, `Delete feature/login…`, `File history`, `Compare main with origin/main`; the Pick dialog's
title `Set upstream for feature/login`; the Name dialog's `Create branch`; the palette's row `Delete main
on origin…` with `origin/main`; the Remotes dialog's `Remove` and `Add remote`; the settings scope `All
repositories (global)`; the discard confirmation `Discard changes?`, `Discard the changes to
Calculator.cs?`, `A snapshot is saved first, so the changes can be restored.`, `Cancel`, `Discard`; the
file rows' tooltips (`ChangedFileRow.ToolTip`, `CommitDetailsRows.cs:144`, the path and, for a rename,
its origin on a second line) `docs/notes.txt`, `src/main-app.txt` / `renamed from src/app.txt` and
`src/helpers.py` / `renamed from src/util.py`.

#### The real window (part L)

Two launches (12.3.4 launches 1 and 2), each with exactly its twin method's tab and starting theme
(D442), 1100×700, dark; nine captures, each compared with its scripted twin by name (at most 3 %), and
the inks read by `InkOf`.

1. `Check_L_changes_placeholders_and_danger_button` (twin: method 1; "changes", one tab). L1: a real
   click on graph row 0; the ink of `FilterBox`, `CommitSummary` and `CommitDescription`, each
   unfocused, is `#8F97A4` on `#272C36` (`CommitSummary`'s rectangle ending at its right padding,
   40 px, so that `SummaryCounter` lies outside it); capture `l1a-placeholders-dark`. `Ctrl+Shift+E` and
   `sea` on the real keyboard: its ink `#E4E7EC`, read in the focused `FilterBox` less the caret's
   column; `l1b-filter-typed-dark`; the filter emptied. L5 dark: the discard of `src/Calculator.cs`
   asked as method 1 asks it (the real pointer moved onto the row, a real click on `RowDiscardButton`
   once it shows); a spot of the danger button `#EA7D86` (`AssertColour`), its label's ink `#10121A`;
   `l5a-discard-dialog-dark`; Cancel; `git status --porcelain` still lists `src/Calculator.cs`
   modified. A real click on
   `ThemeSwitch`; L2: the same three boxes, unfocused, `#68707D` on `#FFFFFF`; `l2a-placeholders-light`;
   `Ctrl+Shift+E` and `sea` on the real keyboard: its ink `#1B1F27` on `#FFFFFF`, less the caret's
   column; `l2b-filter-typed-light`.
2. `Check_L_graph_danger_item_and_head_row` (twin: method 2; "graph", one tab). L4 a: `feature/login`'s
   menu by a real `Shift+F10`, real Down until `Delete feature/login…` is highlighted: its ink
   `#EA7D86` on `#2F3542`; `l4a-delete-item-menu` (pointer kept); Esc. L6: `main`'s row a) the pointer
   at the neutral point: `#7C8CFF`; `l6a-head-idle`; b) the pointer at the row's centre: `#8B9AFF` on
   `#2F3542`; `l6b-head-hovered` (pointer kept); c) a real click on the row, the pointer at the neutral
   point: `#8B9AFF` on `#2A3150`; `l6c-head-selected`.

Unverified until D246's runs (2.9 item 6, 12.9): full-coverage ink at 200 % under ClearType, and that
no tooltip opens over `main`'s row in the real `l6b`, as none does in its twin.

### Part I: one instance per data folder

Part I covers the single-instance guard and its notice (design 1.11, 4.3; D421, D422, D448): a second
start on the same data folder hands over to the first and exits 0 (I1, real window only, since no
harness runs `Program.Main`); and the notice shown when the first does not answer, with its "Try
again", in dark on Windows (I2) and in light on Linux (I3). Its R steps are I1 and I2 (4.6, 12.3.4
launches 4 to 6): three launches, I1's second process and three captures, each compared with its
folder line masked.

#### Conventions of part I

- **Code** (4.2): `InstanceNoticeHost.cs` (1.11), `Phase8aChecks.I.cs`, `Phase8aExpectations.I.cs`
  (`Phase8aExpectations.I`: the texts below word for word, the ids, the picture names, the made-up
  folders, the log lines' fragments §12 reads, and `InstanceNoticeFolder`, the element D448 masks),
  `Phase8aExpectationTests.I.cs` (`InstanceNoticeTexts`' texts equal `Phase8aExpectations.I`'s; every
  name matches 0.3's pattern); §12's methods in `Phase8aRealWindowPass.I.cs`.
- **The notice in the headless app** (4.6; 1.11). `InstanceNoticeHost.Show(GuidancePlatform platform,
  string folder, ThemeVariant theme)` shows `InstanceNoticeWindow` over `InstanceNoticeViewModel` with
  the platform's sentence, the folder given and the theme set on the window's own
  `RequestedThemeVariant`, and a fake try in place of the guard, which a step holds and answers; §4's
  builder may give the host optional parameters after these three for that try (1.11; the lead's
  ruling of 2026-11-20). No guard, no session, no data folder. A picture is the notice's own frame
  (`CaptureRenderedFrame`), 480 wide, as high as its content, at scale 1. The folders are made up
  (ruling 63): `C:\Users\demo\AppData\Roaming\VisualCommit` (I2) and `/home/demo/.config/VisualCommit`
  (I3). §9's sweep shows the notice the same way.
- **The notice** (4.3.4). Window `InstanceNotice`, `Title` `VisualCommit`, width 480, `SizeToContent`
  height, not resizable, centred on the screen, background `VcPanelBackgroundBrush`, text
  `VcTextPrimaryBrush`, font size 12.5, padding 20. Top to bottom: the heading `InstanceNoticeHeading`
  (`dialogTitle`); the folder `InstanceNoticeFolder` (`TrimmedTextBlock`, class `dialogRepository`,
  `TextTrimming` `LeadingCharacterEllipsis`: one line, never wrapped; its text, `ToolTip.Tip` and
  automation name the data folder's full path, D448); the body `InstanceNoticeBody` (`dialogText`); the
  platform's sentence `InstanceNoticeIfNoWindow` (`dialogText`); the status `InstanceNoticeStatus`
  (`dialogWarning`), only after an unanswered try; then `dialogButtons`, right-aligned, left to right,
  `InstanceNoticeClose` `Close` (`VcActionButton`) and `InstanceNoticeTryAgain` `Try again`
  (`VcPrimaryButton`, `IsDefault`), which has the focus at open. Tab goes round the two buttons; Enter
  is "Try again"; Esc, the title bar's close and Alt+F4 are "Close". Each button's automation name is
  its text; the window's `AutomationProperties.Name` is the heading and its `AutomationProperties.HelpText`
  the body, one space and the platform's sentence (4.3.4; the lead's ruling of 2026-11-20). While a try
  runs, "Try again" reads `Trying…` and is disabled; an unanswered try shows the status and enables
  "Try again", which keeps the focus.
- **Colours** (tokens the round keeps; 1.5's rule that new views set no colour of their own):
  background `#1A1D24` dark, `#F5F6F8` light; the heading, body and sentence `#E4E7EC` dark, `#1B1F27`
  light; the folder line (`VcTextSecondaryBrush`) `#9BA3B0` dark, `#5C6572` light; the status
  (`VcWarningBrush`) `#E0A23B` dark, `#895A17` light.
- **Outcomes and exit codes** (4.3.4; ruling 67; D422). The headless checks read the notice's outcome
  from its view model (`NoticeOutcome` `HandedOver`, `StartHere` or `Closed`); the exit codes and the
  start after a freed lock are proven in the real window alone: 0 after a hand-over before any window
  (launch 4, I1's second process), 2 after "Close" (launch 5), and 0 after "Try again" starts the app in
  the same process once the lock is freed (launch 6, D422). The notice's own hand-over (`HandedOver`,
  exit 0) has no real-window run: `InstanceNoticeViewModelTests` prove the outcome, and the lead reads
  the code that gives it exit code 0 in review (4.3.4, 4.9).
- **By hand at the gate** (0.5, 4.6): a second start while the app is behind another window. The lead
  gives the owner a `.cmd` file outside the repository that sets CLAUDE.md's isolating variables
  (`VISUALCOMMIT_DATA_DIR` to a folder of its own, `GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL` to an
  empty file of its own, `XDG_CONFIG_HOME` to an empty folder, `VISUALCOMMIT_KEYCHAIN=memory`, and
  `VISUALCOMMIT_GITHUB_URL`, `VISUALCOMMIT_AZURE_DEVOPS_URL` and `VISUALCOMMIT_GITLAB_URL` set to `none`)
  and starts the Release build; the owner starts it twice, the second time while another window is in
  front of the first, and the report notes what came to the front. macOS's second start (`open -n`,
  Terminal) goes to 8b's Mac checklist.

#### Test methods and app starts (part I)

| # | Test method | Checks | Start | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_I2_notice_dark_try_unanswered` | I2 | `InstanceNoticeHost.Show(GuidancePlatform.Windows, @"C:\Users\demo\AppData\Roaming\VisualCommit", ThemeVariant.Dark)`, its try held | the notice, 480 wide, dark | `i2a`, `i2b`, `i2c` |
| 2 | `Check_I3_notice_light_by_keys` | I3 | `InstanceNoticeHost.Show(GuidancePlatform.Linux, "/home/demo/.config/VisualCommit", ThemeVariant.Light)` | the notice, 480 wide, light | `i3a` |

I1 is the real window's `Check_I1_second_start_hands_over` alone (`Phase8aRealWindowPass.I.cs`).

#### Steps and expected results (part I)

**I1. A second start on the same data folder hands over** (real window only; D421, D448; design 4.3.1
to 4.3.5, 4.6, 12.3.4 launch 4). `Check_I1_second_start_hands_over`: a new data folder D, a session of
one empty tab, 1100×700, dark. Not scripted: no harness runs `Program.Main`.

Steps:
1. `RealApp.Launch(D, session)` with a session of one empty tab, written into D before the start (D442),
   so that step 2 has a `session.json` to read; `Sized` 1100×700; wait until the welcome page shows.
2. `Minimise()` the app's window; wait until `IsIconic`. Read the bytes of D's `session.json` and
   `instance.json`.
3. `StartBeside(D, …)` with the same executable and the same environment.
4. Close the app by its window.

Expected:
- After step 1: D holds `instance.lock` and `instance.json`, whose `v` is 1 and `pid` the app's; D's log
  has `This VisualCommit holds the data folder {D}: a second start on it is sent here.` once.
- After step 3: the second process exits with code 0 within 10 s, having shown no window; within 2 s
  the app's window is not iconic and is visible on the desktop (`IsIconic` false,
  `IsVisibleOnDesktop` true). `ForegroundIsThis` is written to run.txt, not asserted: the test host is
  not the foreground process, so Windows may flash the taskbar button instead (4.3.3). D's log holds the
  second's `VisualCommit already runs on the data folder {D} (process {the app's pid}); it was asked to
  come to the front, and this start ends.` and the app's `A second VisualCommit (process {the second's
  pid}) started on this data folder: answered activated.` `session.json`'s and `instance.json`'s bytes
  are those read in step 2: a `Held` guard's `Dispose` touches no file (1.11, D448).
- After step 4: the app exits with code 0.

**I2. The notice, dark, and a "Try again" left unanswered** (R; D421, D422, D448; design 4.3.4, 4.6,
12.3.4 launches 5 and 6). Method 1.

Steps:
1. Show the notice (the method's start); wait until it is shown and `InstanceNoticeTryAgain` has the
   keyboard focus. Screenshot (`i2a-notice-dark`).
2. Press Enter; the fake try is held. Wait until `InstanceNoticeTryAgain` reads `Trying…`. Screenshot
   (`i2b-notice-trying`).
3. Answer the held try with no answer (`NoticeRetry.NoAnswer`); wait until `InstanceNoticeStatus`
   shows. Screenshot (`i2c-notice-no-answer`).
4. Press Esc.

Expected:
- After step 1: the frame is 480 wide; the background `#1A1D24` at a point clear of text. Top to
  bottom, as the conventions give them: `VisualCommit is already running`; the folder line
  `C:\Users\demo\AppData\Roaming\VisualCommit`, one line high, whole (it fits: no ellipsis), its ink
  `#9BA3B0`, its `ToolTip.Tip` and automation name the same path; the body `Another VisualCommit uses the
  same data folder but did not answer. Switch to its window, or close it and start VisualCommit
  again.`; the sentence `If no VisualCommit window is open, end VisualCommit in Task Manager, then
  choose Try again.`; the heading, body and sentence in ink `#E4E7EC`; no `InstanceNoticeStatus`
  (not visible); then `Close` and `Try again`, right-aligned in that order, both enabled, `Try again`
  with the keyboard focus. The window's automation name `VisualCommit is already running`, its help
  text the body, one space and the Windows sentence; the buttons' automation names `Close` and `Try
  again`. Read in the tree, since neither the frame nor the real capture shows the title bar: the window
  `InstanceNotice` has `Title` `VisualCommit`, `CanResize` false and `SizeToContent` `Height`;
  `InstanceNoticeClose` has the theme `VcActionButton`, `InstanceNoticeTryAgain` the theme
  `VcPrimaryButton` and `IsDefault` true; `InstanceNoticeFolder` is a `TrimmedTextBlock` with
  `TextTrimming` `LeadingCharacterEllipsis` and `TextWrapping` `NoWrap`. No text cut. The view model has
  no outcome yet.
- After step 2: `InstanceNoticeTryAgain` reads `Trying…` and is disabled, and it and `InstanceNoticeClose`
  have the bounds they had after step 1 (the button keeps its width through a try); `Close` enabled; no status;
  the frame as high as after step 1.
- After step 3: `InstanceNoticeStatus` reads `It still did not answer.`, below the sentence and above
  the buttons, its ink `#E0A23B`; `InstanceNoticeTryAgain` reads `Try again`, enabled, with the keyboard
  focus; the frame taller than after step 1 by the status's line and its margin.
- After step 4: the outcome is `Closed`; the window has closed.

**I3. The notice, light, on Linux, by the keys** (not R; design 4.3.4, 4.6). Method 2.

Steps:
1. Show the notice; wait until `InstanceNoticeTryAgain` has the focus. Screenshot (`i3a-notice-light`).
2. Press Tab. 3. Press Tab. 4. Press Shift+Tab. 5. Press Space.

Expected:
- After step 1: as I2 after step 1, in light: the background `#F5F6F8`, the heading, body and sentence in
  ink `#1B1F27`, the folder line `/home/demo/.config/VisualCommit` in ink `#5C6572`, and the sentence `If
  no VisualCommit window is open, end VisualCommit in your system monitor, then choose Try again.`; the
  help text the body, one space and that sentence.
- After steps 2, 3 and 4 the keyboard focus is on `InstanceNoticeClose`, `InstanceNoticeTryAgain` and
  `InstanceNoticeClose`; the fake try was never asked; "Try again" still reads `Try again`.
- After step 5: the outcome is `Closed`; the window has closed; the fake try was never asked.

#### Texts these checks rely on (part I)

`InstanceNoticeTexts` (4.3.4), word for word: the title `VisualCommit`; the heading `VisualCommit is
already running`; the body `Another VisualCommit uses the same data folder but did not answer. Switch to
its window, or close it and start VisualCommit again.`; the sentences, Windows `If no VisualCommit window
is open, end VisualCommit in Task Manager, then choose Try again.`, macOS `If no VisualCommit window is
open, quit VisualCommit in Activity Monitor, then choose Try again.` (no check shows it) and Linux `If no
VisualCommit window is open, end VisualCommit in your system monitor, then choose Try again.`; the
buttons `Try again`, `Trying…` and `Close`; the status `It still did not answer.`

The log lines the real window reads (4.3.5), `{folder}` the data folder and `{pid}` a process id: `This
VisualCommit holds the data folder {folder}: a second start on it is sent here.`; `VisualCommit already
runs on the data folder {folder} (process {pid}); it was asked to come to the front, and this start
ends.`; `A second VisualCommit (process {pid}) started on this data folder: answered activated.`;
`VisualCommit holds the data folder {folder} but did not answer (no instance.json).`

#### The real window (part I)

Three launches (12.3.4 launches 4 to 6), each on a data folder D of its own, and I1's second process.
The notice is never sized; its captures are compared with `CompareMasked`.

4. `Check_I1_second_start_hands_over`: I1's steps and results above.
5. `Check_I2_notice_and_exit_code_2`. The test opens D's `instance.lock` (`AppPaths.InstanceLockFile`)
   with `FileShare.None` and holds it, with no `instance.json`. `RealApp.Launch(D)` without a session:
   neither `session.json` nor `settings.json` is written into D before the start, since the notice's
   twin has none (the notice is then dark, 4.3.4). Within 15 s the process's main window is the notice.
   `InstanceNoticeFolder`'s automation name is D's path; `CompareMasked` with `i2a-notice-dark`. Before
   any key: the notice is the foreground window (`ForegroundIsThis`) and `InstanceNoticeTryAgain` has
   the keyboard focus (UI Automation's focused element). If either fails, a real click on the notice's
   title bar (its non-client area, which gives no control the focus) and both asserted again within
   5 s; else the method fails before it sends any key, with `The notice is not the foreground window
   with Try again focused: a real Enter would reach another window.` Then a real Enter, as the scripted
   twin presses it (4.6; the lead's ruling of 2026-11-20); while `InstanceNoticeTryAgain` reads
   `Trying…` and is disabled, `CompareMasked` with `i2b-notice-trying`. Within 10 s
   `InstanceNoticeStatus` reads `It still did not answer.`; `CompareMasked` with `i2c-notice-no-answer`.
   A real click on `InstanceNoticeClose`: the process exits with code 2 within 5 s; D holds
   `instance.lock`, `logs/` and the launch's own `harness/`, nothing else (no session started, nothing
   written); D's log holds the Warning `VisualCommit holds the data folder {D} but did not answer (no
   instance.json).`
6. `Check_I2_notice_try_again_starts_here`. As launch 5 up to the notice, without a session as there.
   The test releases the lock; the guard of launch 5 (the notice the foreground window and
   `InstanceNoticeTryAgain` focused, the title-bar click only if not, no key until both hold); a real
   Enter, as the twin presses it: within 15 s `OtherWindowOfProcess` finds the main window, with
   `GitStatus` in it, and the notice has closed; `instance.json`'s `pid` is the app's. That window
   closed by its window: exit code 0 within 15 s (D422).

### Part F: first run: git's detection, guidance and setting

Part F covers the first run (R4; D405, D423, D424, D449, D453; design 5): one screen, `GitSetup`, in the
welcome page's place on every empty tab while git is missing, chosen and gone, too old or not working,
or while the askpass helper is missing (git working); its texts by state and platform (Windows, macOS,
Linux, the guidance's), git's own words for a git that does not work, a broken `~/.gitconfig` named by
its line; "Check again" without a restart, reopening a tab whose restore failed; "Choose git.exe…" and
the `gitExecutable` setting; winget on Windows, after a confirm, for a missing and for a too-old git
(D453); the Command Line Tools on macOS and the distribution's package on Linux; the status bar's git
box with its tooltip and `GitSetupButton`; the settings screen's "Git program" row; and the Accounts
section's netrc note (D424). Checks F1 to F14, design 5.5's list, F6b inside F6. Its R steps (5.5, 12.3.4
launches 7 to 10): F1 dark, F2's confirm and its Cancel, F13, F4 through the native Open dialog, F5 to
its first picture through the stand-in, F6 through the stand-in and F6b through real git: four
launches, five captures.

#### Conventions of part F

- **Code** (5.2, 1.15): `Phase8aChecks.F.cs` (`Phase8aChecksF : Phase8aChecks`, with a `.F.Helpers.cs`
  if needed), `Phase8aExpectations.F.cs` (`Phase8aExpectations.F`: every text below word for word, the
  ids, the fake's answers and made-up values, the picture names, and for §12, 5.5's list: F1's and F4's
  scenario and session, F5's and F6's, F13's focus order with winget found, the picker's title `Choose
  git.exe`, F5's version line `git version 2.29.0`, F6's failure text, the confirm's title),
  `Phase8aExpectationTests.F.cs` (`GitSetupTexts`' texts equal `Phase8aExpectations.F`'s; every picture
  name matches 0.3's pattern), `ShellDriver.Phase8a.FirstRun.cs` (the part's driver helpers),
  `FakeGitSetupProbe.cs` (Testing, 1.15) and, beside the checks in 8a's project, `GitSetupShellTests`
  (5.4; no picture). §12's methods are in `Phase8aRealWindowPass.F.cs`.
- **The seam and the fakes** (1.2, 1.15, 5.3). `FakeGitSetupProbe` stands for the machine's search for
  git and for the screen's guidance facts. It sees every lookup as one `DetectAsync(GitLookup, …)`. A
  `ProbeAsync` (a file the picker gave, on the screen or in the settings row) reaches it as
  `new GitLookup(path, null)`, the same call as the session's search once the setting holds that path
  (1.2, 1.10; a headless test gives a probe and never sets the stand-in, so its `StandIn` is null,
  1.17), so the fake cannot tell the two apart. A lookup whose `ChosenPath` has an
  `AnswerFor(path, answer)` entry gets that answer, every time. Any other lookup takes the next answer
  of a queue: the first answer for the start's search and the next for each "Check again", the last
  repeated. It records every lookup it receives, probes included, in `Lookups`, in order. Its
  `GitGuidance` (platform, `WingetPath`, distribution) is fixed for the start. `RealAt(path, source,
  version?)` is the machine's git, found the harness's usual way and run for every git call, reported
  at the made-up path and source given and, if given, at that version (else the
  machine git's own, as the status bar of every earlier picture shows it). An answer with a path has the
  source `Path` unless a step names another. The made-up values are 5.3's: `C:\Program Files\Git\cmd\git.exe`,
  `C:/Users/demo/.gitconfig`, Ubuntu 22.04 (`ubuntu`, [`debian`], `Ubuntu 22.04.5 LTS`) and Alpine; and
  this report's, under `C:\Users\demo` and `/home/demo` (ruling 63; open points 8 and 9):

  | Value | Windows | macOS and Linux |
  |---|---|---|
  | winget's path (the Windows guidance's `WingetPath`) | `C:\Users\demo\AppData\Local\Microsoft\WindowsApps\winget.exe` | the same (the guidance's platform is Windows) |
  | the chosen git (F3 gone; F8 and F12 found), design 5.5's | `D:\Tools\PortableGit\cmd\git.exe` | `/home/demo/tools/PortableGit/cmd/git.exe` |
  | the too-old file chosen (F8, F12) | `C:\Users\demo\Tools\OldGit\cmd\git.exe` | `/home/demo/tools/OldGit/cmd/git.exe` |
  | the git of the macOS and Linux states (F9 b, F10 c) | `/usr/bin/git` | `/usr/bin/git` |
  | every too-old git's version (F5's, 1.14's stand-in default) | `2.29.0` | `2.29.0` |
  | Alpine's os-release | `ID` `alpine`, no `ID_LIKE` | the same |
  | the helper looked for in F11 (`askPassHelper`, no file there) | `C:\Users\demo\AppData\Local\Programs\VisualCommit\visualcommit-askpass.exe` | `/home/demo/.local/share/VisualCommit/visualcommit-askpass` |

  Below, `{chosen}` and `{old}` stand for the running platform's column. `FakeFileLauncher` records each
  start (`Starts`, each the `LaunchCommand` the app built), address (`OpenUrl`) and reveal (`Revealed`) in
  `Requests` and starts nothing; `FakeFolderPicker` answers the file dialog (`AnswerFile`) and records its
  titles (`FileTitles`); `FakeClipboard` holds what Copy gives. No test starts `cmd.exe`, winget,
  `xcode-select`, a terminal or a browser (1.3, 5.4, 5.8).
- **The platform** of the screen's texts and buttons is the guidance's (`GitGuidance.Platform`; 5.3's W,
  M, L), not the machine's: a Windows state shows the same on CI's macOS and Linux jobs as on the laptop.
  The lookup's rule for a chosen path (a full path, on Windows ending in `.exe`, D449) is the running
  platform's, hence the two columns above (open point 9).
- **Starts.** One test method per app start, each on a fresh data folder (and a fresh scenario copy
  where it opens one). A method that gives the session a tab writes it with `ShellDriver.StartWithTabs`;
  "one empty tab" is a start with no session written, where the app opens its one empty tab (`New tab`),
  as the real window's twin does. Every start but F6b's gives `gitProbe` a `FakeGitSetupProbe`; F6b's
  (method 6) gives the check's own `MachineSearchProbe`, whose search for git is the machine's, as in
  every earlier check, and whose guidance is fixed (`new GitGuidance(GitAccess.RunningPlatform, null,
  null)`), so that the check reads neither the `PATH` for `winget.exe` nor `/etc/os-release` (the lead
  at wave 2's merge, `fc123ec`; no expected result changed). `askPassHelper` is left
  null (the helper next to `VisualCommit.Git`, which the build puts there) but in F11.
- **Where things are read** (1.2's ids). The screen by its ids: `GitSetup` (the view), `GitSetupTitle`,
  `GitSetupBody`, `GitSetupPath`, `GitSetupGitSaid`, `GitSetupGuidance`, `GitSetupCommand`, the buttons
  `GitSetupWingetButton`, `GitSetupDownloadButton`, `GitSetupCltButton`, `GitSetupShowFileButton`,
  `GitSetupChooseButton`, `GitSetupUsePathButton`, `GitSetupCheckAgainButton`, `GitSetupContinueButton`,
  `GitSetupCopyButton`, `GitSetupError`, the links `GitSetupMoreLink` and `GitSetupGuideLink`; the status
  bar's `GitStatus` (its text, `ToolTip.Tip` and help text) and `GitSetupButton`; the settings row's
  `GitProgramPath`, `GitProgramNote`, `GitProgramStatus`, `GitProgramBrowseButton`,
  `GitProgramUsePathButton`; the Accounts section's `AccountsNetrcNote` and `AccountsNetrcDismiss`.
  "Shown" is `IsEffectivelyVisible` with a laid-out size; an element a state has no text for is not
  shown. Each button's and link's automation name is its text (5.3, 1.12: §5's controls take their
  names from their own texts). Beside the tree: the launcher's `Requests`, the probe's `Lookups`, the
  picker's `FileTitles`, the clipboard, method 1's merge-tool `Requests` (its `HeldMergeTools`),
  `settings.json` in the data folder (parsed; `gitExecutable` is written only when set, 1.4),
  `session.json`, the log (`LogMark`, `LogLinesSince`) and the git calls (`GitCalls()`; presence and
  order only).
- **Waiting for the screen** (5.3: the guidance is read once per session, off the UI thread, the first
  time a state shows the screen, and the screen shows only once it is read; the waits stay, open point
  29). "Wait until `GitSetup` shows" always means until `GitSetup` shows with its guidance applied; so do
  F1's "the start-up work has ended and `GitSetup` shows" and F11's step 3. Concretely, all of these
  hold:
  - `GitSetupGuidance` reads the state's guidance as the check's expected result gives it (F6b: it
    starts with `Git cannot read line 1 of`);
  - where the state shows `GitSetupChooseButton`, it reads the guidance platform's text: `Choose
    git.exe…` in every Windows state, `Choose the git program…` in F9 and F10, in F6b the running
    platform's;
  - where the expected result has them, `GitSetupWingetButton` shows (F1, F5) and so does
    `GitSetupCommand` (F9 a and b, F10 a).

  Never wait on `GitSetup`'s visibility alone, nor on `WaitUntilReadyAsync` alone. A wait that times
  out names which of these did not come. A button's absence (F8's winget button, F6's) is asserted
  only after this wait (open point 29).
- **The screen** (5.3). `GitSetupView` sits in `CommitGraphView`'s row 0 in place of the welcome page
  (`ShowsWelcome` false: `No repository open` and `WelcomeError` not shown), laid out as the welcome page,
  `MaxWidth` 520. Top to bottom: the title (`GitSetupTitle`, font size 16, `SemiBold`); the body
  (`GitSetupBody`, class `secondary`); the path (`GitSetupPath`, a `PathTextBlock`); "Git said:" over
  `GitSetupGitSaid` (a `SelectableTextBlock` in `VcCodeFontFamily`, bordered in `VcBorderBrush`, six lines
  high at most, then a scroll bar); the guidance (`GitSetupGuidance`); the command (`GitSetupCommand`)
  with `GitSetupCopyButton`; the buttons (`VcActionButton`); `GitSetupError`; the links (`VcFlatButton`,
  their text in `VcAccentBrush`). The view's `AutomationProperties.Name` is the state's title and its
  `AutomationProperties.HelpText` the state's body. The first button is the graph area's focus target
  (`KeyRegion.IsMain`); the view itself takes no focus. No Windows state shows a command (13.5): on
  Windows git-scm.com's winget line shows in the confirm alone. Buttons are listed below in the order of
  5.3's table, which this report takes as their order on the screen, left to right (open point 7).
- **Colours** (tokens only, 5.3; 1.5's rule that new views set no colour of their own), each asserted as
  the brush read in the tree resolving to the token's colour in the active theme, as the support's
  `AssertStatus` does: the body `VcTextSecondaryBrush`, `#9BA3B0` dark, `#5C6572` light; the links
  `VcAccentBrush`, `#7C8CFF` dark, `#4353D8` light; "Git said:"'s border `VcBorderBrush`, `#323845` dark;
  the netrc note `VcWarningBrush`, `#E0A23B` dark.
- **The versions.** `{min}` is `GitVersion.Minimum` read when the check runs, 2.34.1 since `bba59c7`
  (D420), never a literal in a check (1.2). `{v}` is the version the state names: `2.29.0` for every
  too-old git; for a `RealAt` without a version, the machine git's (the laptop's 2.56.0; each CI job's
  own, the floor job's 2.34.1).
- **The status bar** (5.3; today's texts `MainWindowViewModel.Describe` keeps). `GitStatus` reads `Git
  {v}`, `Git {v} is too old (needs {min} or newer)`, `Git not found`, `Git is not working` (today's), or
  the new `Chosen Git not found` and `Git {v} (helper missing)`; `Looking for Git...` (three full stops,
  today's) while a search runs. Its `ToolTip.Tip`, and its help text, equal (open point 14): `{path}, found
  on the PATH`, `{path}, found in Git for Windows' install folder` (else `{path}, found in a usual install
  folder`), `{path}, chosen in Settings` or `{path}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)`; none
  without a path. `GitSetupButton` (`VcFlatButton`) shows only in the screen's states: `Set up Git…`, or
  `Details…` in HelperMissing.
- **Pictures.** Dark and 1100×700 unless a check names light (F1's `f1b`). Each picture passes
  `AssertNoClippedText`, and in each no tooltip is open and `GitSetupButton` is not hovered (open point
  19: the neutral point lies in the status bar). The pointer rests at the neutral point before every
  picture (no part F picture keeps it).
- **Routes** are those earlier checks use: the settings screen by `Ctrl+,` (`OpenSettingsAsync(SettingsOpening.Key)`)
  and a click on `SettingsNavGit` or `SettingsNavAccounts`, waiting for the screen's reads; its Save, a
  click on `ConfirmButton` (`Save`); Esc closes it unsaved (phase 6's part S); the scope switch's
  `GitScopeGlobal` and `GitScopeRepository`. The confirmation is phase 2's dialog (`ConfirmationDialog`:
  `ConfirmTitle`, `ConfirmQuestion`, `ConfirmDetail`, `CancelButton` with the focus as it opens,
  `ConfirmButton`). The sign-in of F14 is phase 7a's (`SignInAsync`, A11's step 1).
- **Nothing confirmed but against the fakes.** A method with a repository takes its `ExactStateAsync()`
  before its first step and asserts it unchanged at its end. The scripted checks press the winget confirm's
  `Install`, `Download Git for Windows`, `Copy`, `Show the file` and `Install the Command Line Tools`
  only against the fakes, and never a link (part G's); the real window presses none of them (12.3.2).
- **The lagged list** (5.7, 0.4): part F and `GitSetupShellTests` run in 8a's project under
  `VISUALCOMMIT_WATCHER_LAG_MS=1500` at §5's merge and at each later merge that runs the list.
- **CI** (5.8): part F runs in CI's six jobs, F6b on the floor job's git 2.34.1 among them; the macOS and
  Linux jobs' pictures are read under the report's "Other platforms".

#### Test methods and app starts (part F)

Fifteen methods, fifteen starts. Method 1 is the twin of the real window's launch 7 and takes its steps
in the same order (F1, F2, F13, F4; its F4 by "Check again", the real window's by Choose, design 5.5 and
12.3.4); methods 4, 5 and 6 are the twins of launches 8, 9 and 10.

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_F1_F2_F13_F4_missing_offers_keys_and_found_again` | F1 dark, F2 (confirm, Cancel), F13, F4 | one tab restored on a fresh "graph" (`Scenarios.GraphAsync()`); `tools:` a `HeldMergeTools`' `Catalogues`, released (`Release()`) before the start, so that it answers `ToolCatalogues.FixedMergeTools` at once and counts each request in `Requests` (`Fixed`'s editors and `PATHEXT`, so no picture changes); the probe: Windows, `WingetPath` winget's; answers `Missing`, `Missing`, `RealAt(C:\Program Files\Git\cmd\git.exe, Path)` | 1100×700, dark | `f1a`, `f2a`, `f4a` |
| 2 | `Check_F1_F2_light_install_and_download` | F1 light, F2 (Install, Download) | as method 1, but with the fixed tool catalogues and the theme Light written before the start (the support's `UseTheme`, `JsonSettingsStore.Update`); answers `Missing` | 1100×700, light | `f1b` |
| 3 | `Check_F3_chosen_gone_use_the_git_it_found` | F3 | one empty tab; `gitExecutable` `{chosen}` written before the start through `JsonSettingsStore.Update(settings => settings with { GitExecutable = {chosen} })`, as the support's `UseTheme` writes the theme; the probe: Windows, `WingetPath` winget's; answers `ChosenMissing` with the candidate `C:\Program Files\Git\cmd\git.exe`, then `RealAt(C:\Program Files\Git\cmd\git.exe, Path)` | 1100×700, dark | `f3a` |
| 4 | `Check_F5_too_old_update_offer` | F5 | one empty tab; the probe: Windows, `WingetPath` winget's; answers `TooOld` at `C:\Program Files\Git\cmd\git.exe`, `Path`, `2.29.0` | 1100×700, dark | `f5a`, `f5b` |
| 5 | `Check_F6_config_fault_show_the_file` | F6 | one empty tab; the probe: Windows, `WingetPath` winget's; answers `ConfigFault`: not working at `C:\Program Files\Git\cmd\git.exe`, `Path`, exit 128, git's words `fatal: bad config line 1 in file C:/Users/demo/.gitconfig`, `ConfigFault("C:/Users/demo/.gitconfig", 1)` | 1100×700, dark | `f6a` |
| 6 | `Check_F6b_real_git_broken_config` | F6b | one empty tab; the check's own probe (the machine's search for git, fixed guidance); the session's global file `GitIsolation.SessionGlobalConfigFile(data)` holds `[user` on its line 1 | 1100×700, dark | none |
| 7 | `Check_F7_not_started` | F7 | one empty tab; the probe: Windows, `WingetPath` winget's; answers `NotStarted` at `C:\Program Files\Git\cmd\git.exe`, `Path` | 1100×700, dark | `f7a` |
| 8 | `Check_F8_choose_too_old_then_found` | F8 | one empty tab; the probe: Windows, no `WingetPath`; answers `Missing`; `AnswerFor({old}, TooOld 2.29.0 at {old})`, `AnswerFor({chosen}, RealAt({chosen}, Setting))`; the picker's files `{old}`, then `{chosen}` | 1100×700, dark | `f8a` |
| 9 | `Check_F9_macos_tools_missing_then_broken` | F9 | one empty tab; the probe: macOS; answers `CommandLineToolsMissing`, then `NotWorking` at `/usr/bin/git` with git's words starting `xcrun: error` | 1100×700, dark | `f9a`, `f9b` |
| 10 | `Check_F10_ubuntu_missing` | F10 a | one empty tab; the probe: Linux, Ubuntu (`ubuntu`, [`debian`], `Ubuntu 22.04.5 LTS`); answers `Missing` | 1100×700, dark | `f10a` |
| 11 | `Check_F10_alpine_missing` | F10 b | one empty tab; the probe: Linux, Alpine; answers `Missing` | 1100×700, dark | `f10b` |
| 12 | `Check_F10_ubuntu_too_old` | F10 c | one empty tab; the probe: Linux, Ubuntu; answers `TooOld` at `/usr/bin/git`, `Path`, `2.29.0` | 1100×700, dark | `f10c` |
| 13 | `Check_F11_helper_missing_continue_and_details` | F11 | one empty tab; the probe: Windows; answers `RealAt(C:\Program Files\Git\cmd\git.exe, Path)`; `askPassHelper` the helper path of the conventions' table (no file there) | 1100×700, dark | `f11a` |
| 14 | `Check_F12_settings_row` | F12 | one tab on a fresh "graph"; the probe: Windows; answers `RealAt(C:\Program Files\Git\cmd\git.exe, Path)`; `AnswerFor({chosen}, RealAt({chosen}, Setting))`, `AnswerFor({old}, TooOld 2.29.0 at {old})`; the picker's files `{chosen}`, then `{old}` | 1100×700, dark | `f12a`, `f12b` |
| 15 | `Check_F14_netrc_note` | F14 | one empty tab; GitHub's fake served in process on a copy of `Scenarios.HostingAsync()` (`Scenarios.ServeHostingAsync(copy, inProcess: true, ownsCopy: true)`), the session started with `hosting: new HostingOptions(fake.Endpoints, fake.CreateHandler)` as Hosting.Tests' `SignInEntryTests.Start` does, never `gitHub:` (no port, ruling 20); the probe: Windows; answers `RealAt(C:\Program Files\Git\cmd\git.exe, Path, 2.43.0)`; the session's home (`GitIsolation.SessionHomeFolder(data)`) empty | 1100×700, dark | `f14a` |

F10's three pictures take three starts because the probe's guidance, and so the distribution, is fixed
for a start (1.15), and the step letters run on in the table's order (`f10a` Ubuntu missing, `f10b`
Alpine, `f10c` Ubuntu too old, 5.5's names).

#### Steps and expected results (part F)

A step numbered in a method runs on from that method's last step in an earlier check.

**F1. Git missing on Windows, winget found** (R: dark; D405, D423, D453; design 5.3, 5.5, 12.3.4 launch
7). Methods 1 (dark) and 2 (light).

Steps, method 1:
1. Take the scenario's `ExactStateAsync()`. Start (the tab restored on it); wait until the start-up work
   has ended and `GitSetup` shows in the tab. Screenshot (`f1a-missing-windows`).

Steps, method 2:
1. As method 1's step 1, light. Screenshot (`f1b-missing-windows-light`).

Expected, after method 1's step 1:
- The tab strip holds one tab, `New tab`: the restore failed and the tab keeps its folder
  (`session.json` still lists the scenario's path, today's `FailedPath`). The welcome page is not shown.
- `GitSetup` shows: `GitSetupTitle` `VisualCommit needs Git`; `GitSetupBody` `VisualCommit works through
  the Git installed on this computer, and it found none: not on the PATH, nor in Git for Windows' usual
  install folders.`; `GitSetupGuidance` `Install Git for Windows, then press Check again. VisualCommit
  does not need to restart.`; `GitSetupPath`, "Git said:", `GitSetupGitSaid`, `GitSetupCommand`,
  `GitSetupCopyButton` and `GitSetupError` not shown.
- The buttons, in order, each enabled, each named by its text: `GitSetupWingetButton` `Install with
  winget…`, `GitSetupDownloadButton` `Download Git for Windows`, `GitSetupChooseButton` `Choose git.exe…`,
  `GitSetupCheckAgainButton` `Check again`; `GitSetupCltButton`, `GitSetupShowFileButton`,
  `GitSetupUsePathButton` and `GitSetupContinueButton` not shown. The links: `GitSetupGuideLink` `How
  VisualCommit finds Git`; `GitSetupMoreLink` not shown (none on Windows: the download button opens
  that page, 5.9).
- `GitSetup`'s automation name `VisualCommit needs Git`, its help text the body. The title at font size
  16, `SemiBold`; the body `#9BA3B0`; the link `#7C8CFF`; `MaxWidth` 520.
- The status bar: `GitStatus` `Git not found`, no tooltip and no help text (no path); `GitSetupButton`
  `Set up Git…`.
- No git call since the start (`GitCalls()` empty); the probe's `Lookups` holds one search, with
  `ChosenPath` and `StandIn` null; the launcher has no request; no `settings.json` was written.

Expected, after method 2's step 1: as after method 1's step 1, in light, but `settings.json` holds what
the start was given, unchanged (byte for byte); the body `#5C6572`, the link `#4353D8`; the window's
`ActualThemeVariant` Light.

**F2. The winget offer and the download** (R: the confirm and its Cancel; D449, D453; design 1.3, 5.3,
5.5, 12.3.2, 12.3.4 launch 7). Methods 1 (confirm, Cancel) and 2 (Install, Download).

Steps, method 1, after F1's step 1:

2. Click `GitSetupWingetButton`; wait for the confirmation. Screenshot (`f2a-winget-confirm`).
3. Click its `CancelButton`; wait until no dialog shows.

Steps, method 2, after F1's step 1:

2. Click `GitSetupWingetButton`; wait for the confirmation; click its `ConfirmButton` (`Install`); wait
   until no dialog shows and the launcher holds one start.
3. Click `GitSetupDownloadButton`; wait until the launcher holds one address.
4. Take `ExactStateAsync()` again.

Expected:
- Method 1, after step 2: the confirmation over the backdrop: `ConfirmTitle` `Install Git with winget?`;
  `ConfirmQuestion` `VisualCommit opens a terminal window that runs winget install --id Git.Git -e
  --source winget`; `ConfirmDetail` `winget downloads Git for Windows from its community repository,
  which the Git for Windows project does not maintain, and runs the installer; Windows may ask for
  permission. When it has finished, close the window and press Check again.`; no `DialogRepository`;
  `CancelButton` `Cancel` with the keyboard focus; `ConfirmButton` `Install`, without the class
  `danger` (`IsDestructive` false: the primary button). The launcher has no request.
- Method 1, after step 3: no dialog; the launcher's `Requests` empty; the log has no `Started in a
  terminal:` line; the screen, the status bar and `Lookups` (one) as after F1.
- Method 2, after step 2: the launcher's `Starts` holds exactly one command (1.3): `FileName` `cmd.exe`;
  `Arguments` empty; `CommandLine` `/S /K ""C:\Users\demo\AppData\Local\Microsoft\WindowsApps\winget.exe"
  install --id Git.Git -e --source winget"`; `WorkingDirectory` `Path.GetDirectoryName` of winget's path:
  on Windows the literal `C:\Users\demo\AppData\Local\Microsoft\WindowsApps`, elsewhere that expression's
  value (open point 10); `InNewTerminal` true.
  The screen is still Missing, as after F1, with no `GitSetupError`; `Lookups` still one (Install starts
  no search). The log has `Started in a terminal: cmd.exe /S /K ""C:\Users\demo\AppData\Local\Microsoft\WindowsApps\winget.exe"
  install --id Git.Git -e --source winget"` once, where the screen's view model writes it (open point 11).
- Method 2, after step 3: the launcher's `Requests` gain one `OpenUrl`, `https://git-scm.com/install/windows`,
  and nothing else; the screen unchanged.
- Method 2, after step 4: `ExactStateAsync()` unchanged; no git call since the start.

**F3. A chosen git that is gone** (not R; ruling 41, D423; design 1.2, 1.4, 5.3, 5.5). Method 3.

Steps:
1. Start; wait until `GitSetup` shows. Screenshot (`f3a-chosen-gone`).
2. Click `GitSetupUsePathButton`; wait until `GitSetup` is gone.

Expected:
- After step 1: `Lookups` holds one search, its `ChosenPath` `{chosen}`. `GitSetupTitle` `The chosen Git is
  gone`; `GitSetupBody` `VisualCommit was set to use the Git at this path, and there is no file there
  now:`; `GitSetupPath` `{chosen}`; `GitSetupGuidance` `VisualCommit found another Git at C:\Program
  Files\Git\cmd\git.exe.`. The buttons, in order: `Choose git.exe…`, `GitSetupUsePathButton` `Use the
  Git it found`, `Check again`; no `GitSetupWingetButton` and no `GitSetupDownloadButton` (shown only for
  ChosenMissing without a candidate). The links: `How VisualCommit finds Git`; no `GitSetupMoreLink`.
  `GitStatus` `Chosen Git not found`; `GitSetupButton` `Set up Git…`. No git call: the app falls back to
  no other git (ruling 41).
- After step 2: `settings.json` holds no `gitExecutable` (its other content unchanged); `Lookups` holds
  two, the second's `ChosenPath` null; `GitSetup` gone and the welcome page shown, with no `WelcomeError`;
  `GitStatus` `Git {v}`, its tooltip and help text `C:\Program Files\Git\cmd\git.exe, found on the PATH`;
  `GitSetupButton` not shown.

**F4. Found again by "Check again"** (R: through the native Open dialog; D423, ruling 5; design 1.2,
5.3, 5.5, 12.3.4 launch 7). Method 1, after F13.

Steps:

9. Read `Requests` of the start's `HeldMergeTools`. Click `GitSetupCheckAgainButton`; wait until
   `CheckGitAgainCommand.IsRunning` is false, `GitSetup` is gone and the tab's repository has loaded
   (`WaitForRepositoryAsync`, then `WaitForWatchersQuietAsync`). Screenshot (`f4a-found-again`).
10. Take `ExactStateAsync()` again.

Expected:
- While the search ran: `GitSetupCheckAgainButton` read `Checking…` and was disabled, and `GitStatus`
  read `Looking for Git...` (open point 12).
- After step 9: `Lookups` holds three. `GitSetup` is not shown; the tab shows the scenario's graph, its
  title the repository folder's name: the folder the failed restore kept was opened again (`ReopenAsync`),
  without a new entry in the recent list. `GitStatus` `Git {v}` (the machine git's version), its tooltip
  and help text `C:\Program Files\Git\cmd\git.exe, found on the PATH`; `GitSetupButton` not shown. The
  merge tools were asked again (5.3: `ForgetToolLookup`, then the merge tools' lookup after the
  reopenings): the `HeldMergeTools`' `Requests` is exactly one more than at step 9's reading. No
  `git mergetool --tool-help` runs in either harness (the scripted one gives the session tool
  catalogues, here the `HeldMergeTools`', `ToolCatalogues.Fixed` otherwise; the real window sets
  `VISUALCOMMIT_TOOL_CATALOGUES=fixed`): that the lookup asks the new git is for `CheckGitAgainTests` and
  `GitConfigurationForgetTests` to prove (5.4), not this check. `settings.json` has no `gitExecutable`.
- After step 10: `ExactStateAsync()` unchanged.

**F5. Too old on Windows: the update offer** (R: to `f5a`, through the stand-in; D453, D449; design 5.3,
5.5, 12.3.4 launch 8). Method 4.

Steps:
1. Start; wait until `GitSetup` shows. Screenshot (`f5a-too-old`).
2. Click `GitSetupWingetButton`; wait for the confirmation. Screenshot (`f5b-winget-update-confirm`).
3. Click its `CancelButton`; wait until no dialog shows.

Expected:
- After step 1: `GitSetupTitle` `Git 2.29.0 is too old`; `GitSetupBody` `VisualCommit needs Git {min} or
  newer. It found Git 2.29.0 at:`; `GitSetupPath` `C:\Program Files\Git\cmd\git.exe`; `GitSetupGuidance`
  `Update Git for Windows, then press Check again. Its installer updates it in place.`. The buttons, in
  order: `GitSetupWingetButton` `Update with winget…` (the git was found on the machine, not chosen,
  D453), `Download Git for Windows`, `Choose git.exe…`, `Check again`; no `GitSetupUsePathButton` (not
  chosen). The links: `How VisualCommit finds Git`; no `GitSetupMoreLink`. `GitStatus` `Git 2.29.0 is too
  old (needs {min} or newer)`, its tooltip and help text `C:\Program Files\Git\cmd\git.exe, found on the
  PATH`; `GitSetupButton` `Set up Git…`. No git call.
- After step 2: `ConfirmTitle` `Update Git with winget?`; `ConfirmQuestion` `VisualCommit opens a
  terminal window that runs winget install --id Git.Git -e --source winget`; `ConfirmDetail` `winget
  downloads the newest Git for Windows from its community repository, which the Git for Windows project
  does not maintain, and runs the installer, which updates the one installed; Windows may ask for
  permission. When it has finished, close the window and press Check again.`; `CancelButton` `Cancel`
  focused; `ConfirmButton` `Update`, without the class `danger`.
- After step 3: no dialog; the launcher's `Requests` empty; no `Started in a terminal:` line; the
  screen as after step 1.

**F6. A broken configuration names its line** (R: F6 through the stand-in, F6b through real git; plan
767; design 1.2, 5.3, 5.5, 12.3.4 launches 9 and 10). Methods 5 (F6) and 6 (F6b).

Steps, method 5:
1. Start; wait until `GitSetup` shows. Screenshot (`f6a-config-fault`).
2. Click `GitSetupShowFileButton`; wait until the launcher holds one request.

Steps, method 6 (F6b):
1. Start; wait until `GitSetup` shows. No picture: the git's path and the session's file name differ
   from machine to machine.

Expected:
- Method 5, after step 1: `GitSetupTitle` `Git is not working`; `GitSetupBody` `VisualCommit found Git at
  the path below, but it did not report its version.`; `GitSetupPath` `C:\Program Files\Git\cmd\git.exe`;
  `Git said:` over `GitSetupGitSaid` `fatal: bad config line 1 in file C:/Users/demo/.gitconfig` (one line of git's words, wrapped to the box's width, in `VcCodeFontFamily`, selectable); `GitSetupGuidance` `Git cannot read line 1 of
  C:/Users/demo/.gitconfig. Correct that line, or rename the file, then press Check again.`. The
  buttons, in order (5.3's table order, open point 7): `GitSetupDownloadButton` `Download Git for
  Windows` (a git that does not work gets the download, D405, D453), `GitSetupShowFileButton` `Show the
  file`, `GitSetupChooseButton` `Choose git.exe…`, `GitSetupCheckAgainButton` `Check again`; no
  `GitSetupWingetButton` (never for a git that does not work, D453), no `GitSetupUsePathButton` (not
  chosen). The links: `How VisualCommit finds Git`. `GitStatus` `Git is not working`, its tooltip and
  help text `C:\Program Files\Git\cmd\git.exe, found on the PATH`; `GitSetupButton` `Set up Git…`.
- Method 5, after step 2: the launcher's `Revealed` is exactly `C:/Users/demo/.gitconfig` (one request,
  nothing else); the screen unchanged; `Lookups` still one.
- Method 6, after step 1: `GitSetupTitle` `Git is not working`; `GitSetupGitSaid` starts with `fatal: bad
  config line 1 in file` and names the session's global file (`GitFileNames.SamePath`: separators and
  case aside); `GitSetupGuidance` `Git cannot read line 1 of {that file, as git names it}. Correct that
  line, or rename the file, then press Check again.`; `GitSetupShowFileButton` shown; `GitSetupPath` the
  git the machine's search found; `GitStatus` `Git is not working`. The log holds a Warning that names
  git's first line (5.3). On every CI job, the floor's git 2.34.1 among them (5.8).

**F7. Git that cannot be started** (not R; design 5.3, 5.5). Method 7.

Steps:
1. Start; wait until `GitSetup` shows. Screenshot (`f7a-not-started`).

Expected: `GitSetupTitle` `Git is not working`; `GitSetupBody` `VisualCommit found Git at the path below,
but it could not be started: {message}`, `{message}` the fake's start failure (open point 8);
`GitSetupPath` `C:\Program Files\Git\cmd\git.exe`; `GitSetupGuidance` `Repair or reinstall Git for
Windows, then press Check again.`; "Git said:" and `GitSetupGitSaid` not shown (open point 13). The buttons,
in order: `Download Git for Windows`, `Choose git.exe…`, `Check again`; no
`GitSetupWingetButton`, `GitSetupShowFileButton` or `GitSetupUsePathButton`. The links: `How VisualCommit
finds Git`. `GitStatus` `Git is not working`; `GitSetupButton` `Set up Git…`.

**F8. "Choose git.exe…": a refusal, then a git that works** (not R; ruling 4; design 1.4, 5.3's Choose,
5.5). Method 8.

Steps:
1. Start; wait until `GitSetup` shows.
2. Click `GitSetupChooseButton`; the picker gives `{old}`; wait until `GitSetupError` shows the refusal.
   Screenshot (`f8a-choose-too-old`).
3. Click `GitSetupChooseButton`; the picker gives `{chosen}`; wait until `GitSetup` is gone.

Expected:
- After step 1: Missing on Windows without winget: the screen, the status bar and the links as F1's
  after its step 1, the tab strip one `New tab` (an empty start, no restore), without
  `GitSetupWingetButton` (5.3: only with `WingetPath` set); the buttons `Download Git for Windows`,
  `Choose git.exe…`, `Check again`; `Lookups` one.
- After step 2: the picker was asked once, its title `Choose git.exe` (`FileTitles`); `GitSetupError`
  reads `Git 2.29.0 at this path is too old: VisualCommit needs {min} or newer.` (while the probe ran it
  read `Checking…`, open point 12); no `settings.json` written; the state still Missing; `Lookups` two,
  the second's `ChosenPath` `{old}` (the probe); no git call.
- After step 3: the picker asked again, `Choose git.exe`; `settings.json`'s `gitExecutable` is
  `{chosen}`; `Lookups` four: the probe of `{chosen}`, then the search that Choose's Check again starts,
  both with `ChosenPath` `{chosen}`; `GitSetup` gone, the welcome page shown with no `WelcomeError`;
  `GitStatus` `Git {v}`, its tooltip and help text `{chosen}, chosen in Settings`; `GitSetupButton` not
  shown.

**F9. macOS: the Command Line Tools missing, then damaged** (not R; ruling 45; design 1.3, 5.3, 5.5).
Method 9.

Steps:
1. Start; wait until `GitSetup` shows. Screenshot (`f9a-macos-missing`).
2. Click `GitSetupCltButton`; wait until the launcher holds one start.
3. Click `GitSetupCopyButton`.
4. Click `GitSetupCheckAgainButton`; wait until `Lookups` holds two, `CheckGitAgainCommand.IsRunning` is
   false and the title reads `Git is not working`, the screen with its guidance (the conventions'
   "Waiting for the screen"). Screenshot (`f9b-macos-broken`).

Expected:
- After step 1: `GitSetupTitle` `VisualCommit needs Git`; `GitSetupBody` `VisualCommit works through the
  Git installed on this Mac, and it found none. Apple's Command Line Tools, which include Git, are not
  installed.`; `GitSetupPath` not shown; `GitSetupGuidance` `Install the Command Line Tools, which macOS
  asks you to confirm, or install Git with Homebrew, then press Check again. With Homebrew, in
  Terminal:`; `GitSetupCommand` `brew install git` with `GitSetupCopyButton` `Copy`. The buttons, in
  order: `GitSetupCltButton` `Install the Command Line Tools`, `GitSetupChooseButton` `Choose the git
  program…`, `Check again`; no winget or download button. The links, in order: `GitSetupMoreLink` `Other
  ways to install Git`, `GitSetupGuideLink` `How VisualCommit finds Git`. `GitStatus` `Git not found`;
  `GitSetupButton` `Set up Git…`.
- After step 2: no confirmation opened; the launcher's `Starts` holds exactly one command,
  `/usr/bin/xcode-select` with the one argument `--install`, no `CommandLine`, `InNewTerminal` false
  (1.3); the log has `Started: /usr/bin/xcode-select --install` once, where the view model writes it
  (open point 11); the screen unchanged.
- After step 3: the clipboard holds `brew install git`, and only that.
- After step 4: `GitSetupTitle` `Git is not working`; `GitSetupBody` `VisualCommit found Git at the path
  below, but it did not report its version.`; `GitSetupPath` `/usr/bin/git`; `GitSetupGitSaid` the fake's
  text starting `xcrun: error` (open point 8); `GitSetupGuidance` `Apple's Command Line Tools look
  damaged. Install them again, or select them in Terminal, then press Check again:`; `GitSetupCommand`
  `sudo xcode-select --switch /Library/Developer/CommandLineTools` with `Copy`. The buttons, in order:
  `Install the Command Line Tools`, `Choose the git program…`, `Check again`. The links: `How VisualCommit
  finds Git`; no `GitSetupMoreLink` (not for a git that does not work). `GitStatus` `Git is not working`,
  its tooltip and help text `/usr/bin/git, found on the PATH`. The launcher still holds one start.

**F10. Linux: Ubuntu missing, Alpine missing, Ubuntu too old** (not R; D449; design 5.3, 5.5). Methods
10, 11 and 12.

Steps, method 10: 1. Start; wait until `GitSetup` shows. Screenshot (`f10a-ubuntu-missing`).
Steps, method 11: 1. As method 10. Screenshot (`f10b-alpine-missing`).
Steps, method 12: 1. As method 10. Screenshot (`f10c-ubuntu-too-old`).

Expected:
- Method 10: `GitSetupTitle` `VisualCommit needs Git`; `GitSetupBody` `VisualCommit works through the Git
  installed on this computer, and it found none on the PATH or in the usual folders.`; `GitSetupGuidance`
  `Install it with your distribution's package manager, in a terminal, then press Check again. On Ubuntu
  22.04.5 LTS:`; `GitSetupCommand` `sudo apt install git` with `Copy`. The buttons, in order: `Choose the
  git program…`, `Check again`; no winget, download or tools button. The links, in order: `Other ways to
  install Git`, `How VisualCommit finds Git`. `GitStatus` `Git not found`; `GitSetupButton` `Set up Git…`.
- Method 11: as method 10, but `GitSetupGuidance` `Install the package named git with your
  distribution's package manager, then press Check again.` and no `GitSetupCommand` and no
  `GitSetupCopyButton` (`alpine` names no command, 5.3).
- Method 12: `GitSetupTitle` `Git 2.29.0 is too old`; `GitSetupBody` `VisualCommit needs Git {min} or
  newer. It found Git 2.29.0 at:`; `GitSetupPath` `/usr/bin/git`; `GitSetupGuidance` `Install a newer Git
  from your distribution, or choose another Git, then press Check again.`; no `GitSetupCommand` and no
  `GitSetupCopyButton` (the generic Linux text, whatever `ID` or `ID_LIKE`; no third-party package source,
  D449). The buttons, in order: `Choose the git program…`, `Check again`. The links, in order: `Other ways
  to install Git`, `How VisualCommit finds Git`. `GitStatus` `Git 2.29.0 is too old (needs {min} or
  newer)`, its tooltip and help text `/usr/bin/git, found on the PATH`.

**F11. The askpass helper missing** (scripted only, ruling 43; ruling 9; design 1.10, 5.3, 5.5).
Method 13.

Steps:
1. Start; wait until `GitSetup` shows. Screenshot (`f11a-helper-missing`).
2. Click `GitSetupContinueButton`; wait until the welcome page shows.
3. Click `GitSetupButton`; wait until `GitSetup` shows.

Expected:
- After step 1: `GitSetupTitle` `Part of VisualCommit is missing`; `GitSetupBody` `Git works, but
  VisualCommit's helper for git's questions is missing. It was looked for at:`; `GitSetupPath` the
  helper path given (the conventions' table); `GitSetupGuidance` `Without it, git and ssh cannot ask for
  a password, a passphrase or a host key here, and a signed-in account cannot be used for git. Install
  VisualCommit again to restore it.`. The buttons, in order: `Check again`, `GitSetupContinueButton`
  `Continue without it`; no `GitSetupChooseButton` (all states but this one), no winget or download
  button. The links: `GitSetupGuideLink` `What the helper does`. `GitStatus` `Git {v} (helper missing)`,
  its tooltip and help text `C:\Program Files\Git\cmd\git.exe, found on the PATH`; `GitSetupButton`
  `Details…`.
- After step 2: `GitSetup` gone; the welcome page (`No repository open`) with no `WelcomeError`;
  `GitStatus` still `Git {v} (helper missing)`; `GitSetupButton` still `Details…`; no `settings.json`
  written (5.9: Continue lasts for the session).
- After step 3: `GitSetup` back in the same tab, as after step 1; the tab strip still holds one tab
  (5.3: `Details…` undoes Continue and activates the first tab showing the screen).

**F12. The settings screen's "Git program" row** (not R; rulings 40, 42; design 1.4, 5.3, 5.5, 5.6).
Method 14.

Steps:
1. Open the tab on the fresh "graph"; wait until ready. Take `ExactStateAsync()`. Press `Ctrl+,`; click
   `SettingsNavGit`; wait for the section's reads.
2. Click `GitScopeRepository`; then click `GitScopeGlobal`.
3. Click `GitProgramBrowseButton`; the picker gives `{chosen}`; wait until `GitProgramPath` reads `{chosen}`.
   Screenshot (`f12a-row-next-start`).
4. Click `ConfirmButton` (`Save`); wait until the screen has closed.
5. Press `Ctrl+,`; click `SettingsNavGit`; wait for the reads.
6. Click `GitProgramUsePathButton`.
7. Click `ConfirmButton`; wait until the screen has closed.
8. Press `Ctrl+,`; click `SettingsNavGit`; wait for the reads. Click `GitProgramBrowseButton`; the picker
   gives `{old}`; wait until `GitProgramStatus` shows the refusal. Screenshot (`f12b-row-too-old`).
9. Press Esc; wait until the screen has closed. Take `ExactStateAsync()` again.

Expected:
- After step 1: on the global scope (`All repositories (global)` selected), last in it, below the merge
  tool's note, the row `Git program` (a `Grid` `132,12,*`, top margin 18, its label in the first column
  as `Name`, `Email`, `Pull` and `Merge tool` are): `GitProgramPath` `C:\Program Files\Git\cmd\git.exe` (a
  `TrimmedTextBlock` that shortens at its start); `GitProgramNote` `Found on the PATH.`;
  `GitProgramBrowseButton` `Browse…`; `GitProgramUsePathButton` not shown; no `GitProgramStatus` text.
  `Lookups` one. (No fit is asserted here: open point 20.)
- After step 2: on `This repository` no part of the row is shown; back on the global scope it is as
  after step 1.
- After step 3: the picker's title `Choose git.exe` on Windows, `Choose the git program` on macOS and
  Linux (open point 17); `GitProgramPath` `{chosen}`; `GitProgramNote` `Chosen here. VisualCommit uses it
  from its next start.`; `GitProgramUsePathButton` `Use the Git on the PATH`; no refusal in
  `GitProgramStatus`; `settings.json` not yet written; `Lookups` two, the second's `ChosenPath`
  `{chosen}` (Browse's probe).
- After step 4: `settings.json`'s `gitExecutable` is `{chosen}`, its other keys unchanged. The git in use
  is still the first (ruling 42): `Lookups` still two, no search ran; `GitStatus` `Git {v}`, its tooltip
  `C:\Program Files\Git\cmd\git.exe, found on the PATH`.
- After step 5: `GitProgramPath` `{chosen}`, `GitProgramNote` `Chosen here. VisualCommit uses it from its
  next start.` and `GitProgramUsePathButton` `Use the Git on the PATH` (the saved choice still differs from
  the git in use; open point 16).
- After step 6: `GitProgramPath` `C:\Program Files\Git\cmd\git.exe`, `GitProgramNote` `Found on the
  PATH.`, `GitProgramUsePathButton` not shown.
- After step 7: `settings.json` holds no `gitExecutable`; `Lookups` still two.
- After step 8: the picker's title as after step 3; `GitProgramStatus` `Git 2.29.0 at this path is too
  old: VisualCommit needs {min} or newer.`; `GitProgramPath` `C:\Program Files\Git\cmd\git.exe` and
  `GitProgramNote` `Found on the PATH.`, unchanged; `Lookups` three, the third's `ChosenPath` `{old}`.
- After step 9: `settings.json` unchanged since step 7; `ExactStateAsync()` unchanged; the session's
  global git file not written (no Git field was edited).

**F13. The screen by keys** (R; keyboard.md D-a; design 5.3, 5.5, 12.3.2, 12.3.4 launch 7). Method 1,
after F2's step 3.

Steps:

4. Press F6 once without looking, then again while `GitSetupWingetButton`, the screen's first button,
   does not have the keyboard focus, at most four presses in all (one per region of
   `KeyRegionNavigator`'s order); the number of presses is not asserted, and if the button does not
   have the focus after the fourth, the check fails. F6 goes round the shown regions in 6.6's order
   (the left panel, the graph area, the right panel, the activity log), the region that holds the focus
   coming last (`KeyRegionNavigator.Move`). After F2's step 3 the focus is either on
   `GitSetupWingetButton` without the outline (D104: `MainWindow.OnDialogRemoving` gives it back with
   `NavigationMethod.Unspecified`) or, if the button was disabled while its confirm was open, on no
   control (open point 28), so a loop that looked before pressing could send none. The first F6 leaves
   the graph area for the next shown region that takes the focus or, from no control, goes to the
   first shown region that takes it; a later one comes back to the graph area, whose target is
   `GitSetupWingetButton` (the element the region remembers, or its focus target, the screen's first
   button, 5.3), given with `Tab`. Which element of another region a press reaches is not this check's
   concern.
5. Press Tab. 6. Press Tab. 7. Press Tab.
8. Press Enter; wait until the command has ended: `Lookups` holds two, `CheckGitAgainCommand.IsRunning`
   is false, and `GitSetupCheckAgainButton` reads `Check again` and is enabled. Neither `Lookups` nor
   `GitStatus` marks that end: the second lookup is recorded when the search starts, and `GitStatus` reads
   `Git not found` again before the screen and the command end (5.3's order; 1.2; `CheckHelper()` runs only
   while git works and the helper was missing, so not here).

Expected:
- After step 4: at least one F6 was sent; `GitSetupWingetButton` has the keyboard focus, with the
  keyboard outline (`:focus-visible`); no press opened a dialog or made a launcher request, and
  `Lookups` is still one.
- After steps 5, 6 and 7: the focus on `GitSetupDownloadButton`, `GitSetupChooseButton` and
  `GitSetupCheckAgainButton`, in turn, each with `:focus-visible` (F13's order with winget found, open
  point 7); no Tab acted: no dialog, no launcher request, `Lookups` one.
- After step 8: Enter searched once more (`Lookups` two, the second's `ChosenPath` null); while it ran
  `GitSetupCheckAgainButton` read `Checking…`, disabled, and `GitStatus` `Looking for Git...` (open point
  12); the state is still Missing, the screen as after F1; `GitStatus` `Git not found`;
  `GitSetupCheckAgainButton` reads `Check again`, enabled, and has the keyboard focus again with
  `:focus-visible` (5.3: `Tab`, since it had the outline when pressed), asserted once the dispatcher has
  settled after the wait, not waited for, since 5.3 gives it back when the search ends; no launcher
  request; no git call.

**F14. The netrc note** (not R; ruling 8, D424; design 1.10, 5.3, 5.5). Method 15.

Steps:
1. Start; wait until ready. Sign in to the fake as octo with `github_pat_FAKE-octo-1` (`SignInAsync`,
   phase 7a's A11 step 1); wait until `accounts.json`'s record shows `useForGit` true.
2. Press `Ctrl+,`; click `SettingsNavAccounts`; wait for the screen's reads, the netrc question among
   them (open point 18). Press Esc; wait until the screen has closed.
3. Write an empty `_netrc` into the session's home. Press `Ctrl+,`; click `SettingsNavAccounts`; wait
   until `AccountsNetrcNote` shows. Screenshot (`f14a-netrc-note`).
4. Click `AccountsNetrcDismiss`.
5. Press Esc; wait until the screen has closed. Press `Ctrl+,`; click `SettingsNavAccounts`; wait for the
   reads. Press Esc; wait until the screen has closed.

Expected:
- After step 1: no settings screen is open; `accounts.json`'s GitHub record is `octo`'s, with
  `useForGit` true (a new sign-in's default); `GitStatus` reads `Git 2.43.0`.
- After step 2, once the reads have ended and before its Esc: GitHub's block (`AccountsGitHub`) shows
  `AccountLogin` `octo`, with `AccountUseForGit` `Use for git over HTTPS` checked; no `AccountsNetrcNote`
  and no `AccountsNetrcDismiss` (no file in the session's home). After its Esc: the screen has closed.
- After step 3: under the blocks, `AccountsNetrcNote` (its classes include `settingsNote` and `notice`,
  and the brush read on it resolves to `VcWarningBrush`, `#E0A23B` dark; today `notice` alone is
  secondary and the warning colour comes with `.notice.warning`, `SettingsDialogView.axaml:646`; open
  point 27) reads `Your home folder has a .netrc or _netrc file. With Git 2.43.0, an entry in that file
  for a host can answer git's requests to it before your signed-in account. Git 2.46 or later stops this
  for git's own requests; git-lfs reads the file with every version of Git.`, with
  `AccountsNetrcDismiss` `Dismiss`. The file is empty: the note asks only that it exists (5.3).
- After step 4: the note and its button gone; `settings.json` not written by the dismissal (5.9); the
  file still there.
- After step 5, once the reopened section's reads have ended and before its last Esc: `AccountsGitHub`
  still shows `octo` with `AccountUseForGit` checked; no `AccountsNetrcNote` and no
  `AccountsNetrcDismiss` (dismissed for the session; the `_netrc` still there). After that Esc: the
  screen has closed.

#### Texts these checks rely on (part F)

The first-run screen (5.3), word for word. The titles: `VisualCommit needs Git`, `The chosen Git is
gone`, `Git {v} is too old`, `Git is not working`, `Part of VisualCommit is missing`. The bodies:
Missing W `VisualCommit works through the Git installed on this computer, and it found none: not on the
PATH, nor in Git for Windows' usual install folders.`; Missing M `VisualCommit works through the Git
installed on this Mac, and it found none.`, with the tools missing ` Apple's Command Line Tools, which
include Git, are not installed.` after it; Missing L `VisualCommit works through the Git installed on this
computer, and it found none on the PATH or in the usual folders.`; ChosenMissing `VisualCommit was set to
use the Git at this path, and there is no file there now:` (for a setting the lookup does not run, `VisualCommit
was set to use the Git at this path, but it runs Git only from a full path to the program itself:`,
which no check shows); TooOld `VisualCommit needs Git {min} or newer. It found Git {v} at:`; NotWorking
`VisualCommit found Git at the path below, but it did not report its version.`, with no output
`VisualCommit found Git at the path below, but it said nothing and exited with code {n}.` (no check shows
it), not started `VisualCommit found Git at the path below, but it could not be started: {message}`;
HelperMissing `Git works, but VisualCommit's helper for git's questions is missing. It was looked for
at:`. "Git said:". The guidance: Missing W `Install Git for Windows, then press Check again. VisualCommit
does not need to restart.`; Missing M with the tools missing `Install the Command Line Tools, which macOS
asks you to confirm, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:`
(else `Install Git with Homebrew, then press Check again. In Terminal:`), the command `brew install git`;
Missing L known `Install it with your distribution's package manager, in a terminal, then press Check
again. On {PRETTY_NAME, else ID}:` with the command (`debian`, `ubuntu`: `sudo apt install git`; `fedora`,
`rhel`, `centos`: `sudo dnf install git`; `arch`: `sudo pacman -S git`; `suse`, `opensuse`, `opensuse-*`:
`sudo zypper install git`), unknown `Install the package named git with your distribution's package
manager, then press Check again.`; ChosenMissing `VisualCommit found another Git at {candidate}.`, none
`No other Git was found on the PATH or in the usual install folders. Choose another, or press Check again
once the chosen one is back.`; TooOld W `Update Git for Windows, then press Check again. Its installer
updates it in place.`, M `Update the Command Line Tools in System Settings, General, Software Update, or
install Git with Homebrew, then press Check again. With Homebrew, in Terminal:` with `brew install git`,
L `Install a newer Git from your distribution, or choose another Git, then press Check again.`;
NotWorking with a fault `Git cannot read line {n} of {file}. Correct that line, or rename the file, then
press Check again.`, W `Repair or reinstall Git for Windows, then press Check again.`, M with the tools
broken `Apple's Command Line Tools look damaged. Install them again, or select them in Terminal, then
press Check again:` with `sudo xcode-select --switch /Library/Developer/CommandLineTools`, M `Reinstall
Git, then press Check again.`, L `Reinstall git with your distribution's package manager, then press Check
again.`; HelperMissing `Without it, git and ssh cannot ask for a password, a passphrase or a host key
here, and a signed-in account cannot be used for git. Install VisualCommit again to restore it.`. The
buttons: `Install with winget…`, `Update with winget…`, `Download Git for Windows`, `Install the Command
Line Tools`, `Show the file`, `Choose git.exe…` (W), `Choose the git program…` (M, L), `Use the Git it
found`, `Use the Git on the PATH`, `Check again`, `Checking…`, `Continue without it`, `Copy`; the links
`Other ways to install Git`, `How VisualCommit finds Git`, `What the helper does`. The addresses
`https://git-scm.com/install/windows`, `https://git-scm.com/install/mac`, `https://git-scm.com/install/linux`
(the links' addresses are part G's).

The confirms (5.3): `Install Git with winget?`, `VisualCommit opens a terminal window that runs winget
install --id Git.Git -e --source winget`, `winget downloads Git for Windows from its community repository,
which the Git for Windows project does not maintain, and runs the installer; Windows may ask for
permission. When it has finished, close the window and press Check again.`, `Install`; for a too-old git
`Update Git with winget?`, the same question, `winget downloads the newest Git for Windows from its
community repository, which the Git for Windows project does not maintain, and runs the installer, which
updates the one installed; Windows may ask for permission. When it has finished, close the window and
press Check again.`, `Update`; `Cancel` (today's `ConfirmationRequest.CancelText`).

Choose (5.3): the picker's titles `Choose git.exe` and `Choose the git program`; `Checking…`; the refusals
`Git {v} at this path is too old: VisualCommit needs {min} or newer.`, `This Git did not report its
version. Git said: {first line}`, `This file could not be started: {message}`, and today's
`SettingsTexts.ProgramMissing` `There is no file at this path.` and `SettingsTexts.ProgramNotExe`
`Choose the program itself (an .exe file): a .cmd or .bat file would run through cmd.exe.` (no check
shows the last four).

The status bar (5.3, and today's `MainWindowViewModel`): `Looking for Git...`, `Git {v}`, `Git {v} is too old
(needs {min} or newer)`, `Git not found`, `Git is not working`, and the new `Chosen Git not found`, `Git
{v} (helper missing)`; the tooltips `{path}, found on the PATH`, `{path}, found in Git for Windows' install
folder`, `{path}, found in a usual install folder`, `{path}, chosen in Settings`, `{path}, from
VISUALCOMMIT_GIT_STAND_IN (a test switch)`; `Set up Git…`, `Details…`.

The settings row (5.3): `Git program`; `None found`; `Found on the PATH.`, `Found in Git for Windows'
install folder.`, `Found in a usual install folder.`, `Chosen here.`, `Chosen here. There is no file at
this path.`, `Chosen here. VisualCommit runs Git only from a full path to the program itself.`, `From
VISUALCOMMIT_GIT_STAND_IN, a test switch.`, `No Git was found on the PATH or in the usual install
folders.`, each followed, while the choice differs from the git in use, by ` VisualCommit uses it from its
next start.` (git works) or ` VisualCommit looks for Git again when you save.`; after `Use the Git it found`
while the chosen git is gone and another was found, `VisualCommit looks for Git again when you save.` alone
(the lead's ruling on the review's FD5 and FT8; no check shows it); `Browse…`, `Use the Git on
the PATH`, `Use the Git it found`. The netrc note (5.3): `Your home folder has a .netrc or _netrc file.
With Git {v}, an entry in that file for a host can answer git's requests to it before your signed-in
account. Git 2.46 or later stops this for git's own requests; git-lfs reads the file with every version of
Git.`, `Dismiss`.

Today's texts the steps pass through: `New tab` (`RepoTabViewModel.EmptyTitle`), `No repository open`,
the settings screen's `Git`, `Accounts`, `All repositories (global)`, `This repository`, `Save`, `Cancel`,
the Accounts section's `Use for git over HTTPS`.

The log lines (1.2, 1.14, 5.3): `Using Git {version} at {path}.` (word for word; the real window waits on
it); `Started in a terminal: {the command's DisplayText}`; `Started: /usr/bin/xcode-select --install`;
`{winget|xcode-select} could not be started: {message}` (in `GitSetupError`; no check shows it); the
stand-in's Warnings `VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names a Git, Git is
looked for only at {value}.` and `VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names
a Git, this computer is treated as having no Git.`; the setting's Warnings `The Git chosen in Settings is
not used: {path} is not a full path.` and `The Git chosen in Settings is not used: {path} is not an .exe
file.` (no check reads them).

#### The real window (part F)

Four launches (12.3.4 launches 7 to 10), each on a data folder D of its own whose session holds exactly
its twin's tabs and starting theme (D442; launches 8 to 10 write none, as their twins have none), sized
1100×700, dark; five captures, each compared with its scripted twin by name (at most 3 %); the path
shown may differ from the twin's, the stand-in's or the machine's for the fake's, and the difference
stays under 3 % (ruling 47). The stand-in is `RealApp.StandInPath`, in `RealWindowTests`' output (a
missing one fails with `The stand-in for git is missing at {path}: build tests/VisualCommit.RealWindowTests,
which references it.`), its variables given by `StandInEnvironment` and set last. run.txt names winget's
path, the stand-in's path and whether it holds a space. Each launch ends closed by its window, exit code
0. No step presses, clicks or sends Enter to the winget confirm's `Install`, `GitSetupDownloadButton`,
`GitSetupMoreLink`, `GitSetupGuideLink`, `GitSetupCopyButton`, `GitSetupShowFileButton` or
`GitSetupCltButton`, nor sends F1 (12.3.2).

7. `Check_F_missing_offers_keys_and_choose` (twin: method 1; one tab on a copy of "graph", restored;
   `StandInEnvironment("none")`). Before the app starts, the test looks for winget as the app does,
   `ProgramSearch.FindOnPath("winget.exe", PATH, ';', File.Exists)` over its own `PATH`, which
   `RealApp.Launch` passes on unchanged; none: the method fails with `winget.exe is not on this machine's
   PATH: F1, F2 and F5 need it (install App Installer from the Microsoft Store).` (D449, D453; never "Not
   run"). F1: the restore fails as the twin's; a wait (12.3.1: `WaitFor` within the pass's
   `StepTimeout`; `Find` alone throws at once) until `GitSetupGuidance`'s UI Automation name is Missing
   W's guidance from `Phase8aExpectations.F`, `GitSetupChooseButton`'s name is `Choose git.exe…` and
   `GitSetupWingetButton` is in the window (the conventions' "Waiting for the screen"); then the
   screen's title, body and buttons as F1's, `GitSetupWingetButton` among them; `GitStatus` `Git not
   found`; `GitSetupButton` `Set up Git…`; capture `f1a-missing-windows`. The log holds the stand-in's Warning for `none` once (this report's
   addition, from 1.14; open point 24). F2: a real click on `GitSetupWingetButton`; the confirm `Install
   Git with winget?`; `f2a-winget-confirm`; a real click on the confirm's `CancelButton`; no
   `ConfirmationDialog` within 5 s, else the method fails before any further key; the log has no
   `Started in a terminal:` line and `NativeMethods.Processes()` lists no `cmd.exe` or `winget.exe` whose
   parent is the app (ruling 6); the screen unchanged. F13: one real F6 without looking, then a real F6
   again while `GitSetupWingetButton` is not UI Automation's focused element, at most four presses, each
   followed by a wait (within `StepTimeout`) until the focused element is `GitSetupWingetButton` or
   another element than before the press; if the button does not have the focus after the fourth, the
   method fails before any further key. After F2's Cancel the button may already hold the focus (D104;
   open point 28), so the first F6 is always sent. Then real Tab through F13's order
   (`GitSetupDownloadButton`, `GitSetupChooseButton`, `GitSetupCheckAgainButton`), each Tab followed by `WaitForFocusedElement` of the next id, whose failure
   ends the method before any further key; Enter only once the focus is on `GitSetupCheckAgainButton`:
   one more search logged, still Missing; once it ends `GitSetupCheckAgainButton` has the focus again.
   F4: a real click on `GitSetupChooseButton`; `ChooseFileInDialog("Choose git.exe", git, 30 s)`, `git`
   being D446's, `GitLocator.FindExecutable(GitSearchContext.FromSystem())` in the test process; within
   60 s the screen is gone and the restored repository's `loaded` and working-tree lines are logged;
   `GitStatus` `Git {v}`, its help text `{git}, chosen in Settings`; `settings.json`'s `gitExecutable` is
   `git`; `f4a-found-again`.
8. `Check_F5_too_old_stand_in` (twin: method 4, to `f5a`; one empty tab; `StandInEnvironment(StandInPath,
   version: "git version 2.29.0")`). Winget looked for first, as in launch 7, with the same failure. The
   same wait as launch 7's, with TooOld W's guidance, `Choose git.exe…` and `GitSetupWingetButton` in
   the window; then the title `Git 2.29.0 is too old`, `{min}` from `GitVersion.Minimum`, `GitSetupPath`
   the stand-in's path, `GitSetupDownloadButton` shown, `GitSetupWingetButton` reading `Update with winget…` (D453: the
   stand-in's git counts as found on the machine), not clicked; `GitStatus`'s help text `{path}, from
   VISUALCOMMIT_GIT_STAND_IN (a test switch)`; the log's stand-in Warning `… Git is looked for only at
   {path}.` once (1.14); `f5a-too-old`. The update confirm is not opened (`f5b` is scripted only).
9. `Check_F6_config_fault_stand_in` (twin: method 5; one empty tab; the stand-in with `failure:` F6's
   text, `fatal: bad config line 1 in file C:/Users/demo/.gitconfig`). The same wait as launch 7's, with
   the fault's guidance and `Choose git.exe…`; then `Git is not working`; `GitSetupGitSaid` that line;
   the fault's guidance, `Git cannot read line 1 of C:/Users/demo/.gitconfig. Correct that line, or
   rename the file, then press Check again.`; `GitSetupDownloadButton` and `GitSetupShowFileButton`
   shown, neither pressed (12.3.2); `f6a-config-fault`.
10. `Check_F6b_real_git_broken_config` (twin: method 6; one empty tab; no stand-in; `[user` written into
    `GitIsolation.SessionGlobalConfigFile(D)` before the launch, as phase 6's `globalGitConfig`). A wait
    as launch 7's until `GitSetupGuidance`'s name starts with `Git cannot read line 1 of`; then the
    title `Git is not working`; `GitSetupGitSaid` starts with `fatal: bad config line 1 in file` and
    names that file (separators and case aside); no capture; the line noted in run.txt.

Unverified until §12's first runs and D246's (12.9): F4's dialog title and box in the native dialog
(S14's pattern), the stand-in's runtime files in this project's output, and a path with a space (a run
in the worktree proves none; the main checkout's does).

### Part K: the keyboard pass and the focus fixes

In this part "F1" to "F7", "F5-A", "G7" and "G8" are `keyboard.md`'s findings (design 6.3), not part
F's checks.

Part K covers §6's fixes (design 6.1, 6.3; keyboard.md's F1 to F7) and two of its walks: Tab from the
diff's text to the hunk bars' buttons and Shift+Tab to its toolbar, with chosen lines staged by the
keys (K1; D426); the arrows picking a blame run, and Shift+F10 its menu (K2); the palette's keyless
"Cancel {title}" (K3; D417 as D452 amends it); a stage row's buttons reached by Tab and drawn only
while they have the keyboard's frame, and the focus after a row's action (K4; D425); the focus after
"Choose" in the repository browser (K5; F5-A); a loading diff that no longer takes the focus the user
moved (K6); the commit boxes and Amend while "Committing…" runs (K7; D392); and the long Tab walk to
the status bar's Cancel and a splitter moved by the arrows (K8; G7, G8, D427). Checks K1 to K8,
design 6.5's list, with its 14 pictures. Its R steps are K1 and K2 (6.5, 12.3.4 launches 11 and 12):
six captures in two launches.

#### Conventions of part K

- **Code** (6.2): `Phase8aChecks.K.cs`; `Phase8aExpectations.K.cs` (`Phase8aExpectations.K`: the texts
  below word for word, the ids, the picture names, the key counts of K1's and K2's routes, K1's
  `--numstat` lines after the hunk and after the two lines, K2's file and its count of Down to run 2,
  and for §12 the two launches' sessions and themes, 6.5 and 1.15's last paragraph);
  `Phase8aExpectationTests.K.cs` (every name matches 0.3's pattern and is used once; the texts equal
  the app's constants, `DashboardTexts.CancelTitle("Fetch origin")` and `OperationTexts.Cancelled("Fetch
  origin")` among them; K1's `--numstat` lines and the index's file follow from
  `Scenarios.ChangesFiles.CalculatorCommitted` and `CalculatorWorking`); `ShellDriver.Phase8a.Keyboard.cs`
  for the part's helpers; `HeldGitRunner` (`tests/VisualCommit.Testing/Headless/`, 1.15). The tests that
  prove the fixes first (`KeyboardPassTests` in 8a's project and the App.Tests classes of 6.4) are not
  checks; the report names them under "What proves each Delivers item". §12's methods are in
  `Phase8aRealWindowPass.K.cs`.
- **Scenarios and sessions.** "changes" (`Scenarios.ChangesAsync()`, phase 2's): staged `README.md`,
  `config/settings.json`, `src/helpers.py` (renamed from `src/util.py`); unstaged, in the flat list's
  order, `README.md`, `assets/logo.png`, `data/blob.bin`, `data/large.txt`, `docs/guide.md` (untracked),
  `docs/old-notes.txt` (deleted), `src/Calculator.cs` (two hunks: `return a + b;` becomes `return a + b +
  _offset;` in `Add`, and four lines added before the last `}`: an empty line, `    public int
  Negate(int a) => -a;`, an empty line, `    public int Square(int a) => a * a;`). "history"
  (`Scenarios.HistoryAsync()`, phase 5's): one unstaged file, `src/Reader.cs`, whose working tree's blame
  has 24 lines in 15 runs (phase 5's `ReaderWorkingTreeRuns`): run 0 lines 1 to 2 `1d5a7cf` "Add the
  parser" (Mira Kovac), run 1 line 3 not committed yet, run 2 line 4 `0575c7f` "Rename the parser to
  reader" (Mira Kovac, `mira@example.com`), and so on. "graph" (`Scenarios.GraphAsync()`, phase 1's):
  HEAD's commit "Add settings page", the remote `origin` a bare repository `origin.git` beside the
  working tree, its refs those the tracking refs already hold. One empty tab for K5, beside a copy of
  phase 7a's "hosting" (`Scenarios.HostingAsync()`), which the fake of GitHub serves (below). Every
  start but K5's is `StartEmptyAsync` or `OpenAsync` of 8a's project (1.17) with the theme the methods
  table names and, for K6 and K7, the probe that hands over `HeldGitRunner`. 8a's `StartEmptyAsync`
  takes no fake of GitHub (every provider stays off, `Phase8aChecks.Support.cs`), so K5 starts as 7a's
  `StartEmptyAsync(data, served)` does: `ShellDriver.Start(data.Path, 1100, 700, gitHub:
  served.GitHub)` (dark, `TestIdentity.Pinned`), then `WaitUntilReadyAsync()` and `MoveToNeutral()`,
  through a private helper in part K's own file, `Phase8aChecks.K.cs`; `Phase8aChecks.Support.cs` is
  not changed for it (open point 43).
- **What a fake stands for.** `FetchHold` (`tests/VisualCommit.Testing/FetchHold.cs`, phase 6's design
  17.2) stands for a slow remote: installed in the session's data folder (`FetchHold.Install`) and set
  on the scenario's copy (`HoldIn`) before the start, it makes git's upload-pack at `origin.git` wait for
  its release, so a fetch shows running, without a percentage, with Cancel. A held fetch's shell gives
  up when its rounds are over, `FetchHold.HeldFor` after it started (`rounds` × 0.5 s of `sleep`: 60 s
  for the default 120 rounds, longer on Windows, where each `sleep` is a process, `FetchHold.cs`), at a
  look that finds no release, and leaves a `gave-up-` marker, on which `WaitForHeldAsync`, `Release()`
  and `Dispose()` fail; a shell that saw the release goes on without looking again, so that `Arm()`,
  which deletes `release`, never makes it give up (`f019688`, open point 42). On Linux and macOS a
  cancel kills git alone (`GitRunner.Kill`, D36), so a cancelled held fetch's shell keeps waiting until
  a release: a cancel does not stop that clock. Method 3 therefore installs the hold with `rounds: 300`
  (`FetchHold.Install(data.Path, rounds: 300)`, `HeldFor` 150 s, under the hang watchdog's 3 minutes),
  so that it outlasts K8's walks of up to 200 Tabs on a slow runner; it releases (`Release()`) once
  each cancelled fetch has ended and been checked (K3's step 6, K8's step 9), so that a cancelled
  fetch's shell goes on; `Arm()` holds again before the next fetch (K8's step 7); and the method
  disposes the hold (a release) in a `finally`. On Linux and macOS K3's cancelled shell may not look
  at `release` between step 6's release and step 7's `Arm()`, when both fall within one of its sleeps:
  it then waits on through K8's hold until step 9, which does no harm, since its marker is in `Arm()`'s
  baseline and not counted, and its rounds started only seconds before K8's fetch's.
  `HeldGitRunner` (1.15) stands for a slow git: an
  `IGitRunner` over the found git, given to the session as the detection's `Runner` by a probe over
  `MachineGitSetupProbe`, that holds the next call whose first argument is the word the step names
  (`diff-files`, the unstaged diff's read,
  `GitRepository.Changes.cs:201`; `commit`, `GitRepository.Writes.cs:219`) until `Release()`, with
  `Started` to await; a step sets it to hold just before its key, once the app is idle
  (`WaitForIdleAsync`). If a tab's git calls bypass the detection's runner (6.9 item 5), K6 and K7 keep
  their tests and drop their pictures (6.4), and the report says so. `FakeGitHub`
  (`tests/VisualCommit.Testing/Hosting/`, phase 7a's) stands for github.com: the account `octo` with a
  made-up token and its four repositories, served from the copy of "hosting" by
  `Scenarios.ServeHostingAsync(copy, port: 0, ownsCopy: true)` in the test process on a free port (port
  0: no new port, ruling 20; 7a's own checks take 47395), the session's GitHub pointed at it
  (`gitHub: served.GitHub`) as phase 7a's part B points it; disposing it stops the fake and deletes
  the copy.
- **What is read where.** The keyboard focus is the focused element in the tree (`FocusedElement`,
  `FocusedAutomationId()`, `FocusIsWithin(…)`), its region `FocusedRegion()` (D213's regions, 6.6 of phase
  6). "Framed" and "the keyboard's frame" mean the focused element has the pseudo-class `:focus-visible`
  (Avalonia sets it for a focus moved by Tab or the arrows, never for a click or for
  `NavigationMethod.Unspecified`), as phase 6's `FocusVisible` reads it; "no frame" means it has not. A
  stage row's buttons are read in the tree: `IsEffectivelyVisible` and `Opacity` of `RowStageButton`,
  `RowDiscardButton` and `RowUnstageButton`. Git's state is read with git in the scenario's copy, never
  from the app: a `--numstat` line is quoted with its two tabs written as single spaces (`1 1
  src/Calculator.cs`). A colour is read as part L reads a background: the exact colour at a sampled
  point clear of text, and the brush resolved in the tree. A focused box's accent border is Fluent's:
  its `:focus` style sets `TextControlBorderBrushFocused` on the template part `Border#PART_BorderElement`,
  while the box's own `BorderBrush` keeps `TextControlBorderBrush` (`#323845` dark). So a check reads
  that template part's `BorderBrush`, never the TextBox's own, and in a picture the pixel at (the box's
  left + 0.5, its vertical centre), as phase 6's `Phase6ChecksL` reads the clone form's focused
  `CloneUrlBox` (`Phase6Checks.L.Sweep.cs:603-604`).
- **Colours** (tokens no package of 8a changes; `Theme/Tokens.axaml`):

  | Token | Dark | Light | Where part K reads it |
  |---|---|---|---|
  | `TextControlBorderBrushFocused` (a focused box's border) | `#7C8CFF` | `#4353D8` | `CommitSummary`'s template `Border#PART_BorderElement` (`BorderBrush`) and the box's left-border pixel, K6 and K7 (dark); `CommitSummary.BorderBrush` itself stays `TextControlBorderBrush`, since Fluent's `:focus` style sets only the template's border |
  | `VcSelectionBrush` (the picked blame run's gutter) | `#2A3150` | `#DDE1FA` | K2 (light) |
  | `VcPanelBackgroundBrush` (an even run's gutter) | `#1A1D24` | `#F5F6F8` | K2 (light) |
  | `VcWindowBackgroundBrush` (an odd run's gutter) | `#14161B` | `#FFFFFF` | K2 (light) |

  Fluent's focus frame keeps Fluent's own colours: the design names none (6.3.1), so a check asserts
  `:focus-visible` and the gate inspects the frame in the picture.
- **Keys** are written as Windows shows them, Ctrl being the headless command modifier on every
  platform (the report's conventions). F6 and Shift+F6 go round the regions shown (D213:
  the left panel, the graph area, the right panel, the activity log), each giving the focus back to its
  last focused element while that is still shown (`KeyRegionNavigator`); F6 into a region that holds
  nothing focusable skips it. A menu opened by Shift+F10 highlights its first enabled item (D105). A
  key is pressed only after a wait for the condition its previous key leads to, never after a fixed
  time; a walk of Tabs presses one at a time and asserts the focused element after each.
- **Routes** are those earlier checks use, unless a check is about the route: the working-changes row
  by a click on graph row 0 (phase 2); the stage panel's first unstaged row by F6 (phase 6's k12b); the
  commit message by Ctrl+Shift+M and the commit by Ctrl+Enter (phase 6's K12); the blame of the open
  diff's file by Ctrl+Shift+B (phase 6's K12); the palette by Ctrl+Shift+P with the pointer at the
  neutral point (phase 6's L31, part L); `Fetch origin` from the menu of `origin` under Remotes in the
  left panel (phase 3's checks 3 and 17); the clone form by `CloneRepoButton` (`Clone`) and the session
  signed in to the fake (phase 7a's B1). The pointer rests at the neutral point before every Shift+F10, so
  that no item of the menu opens under the pointer hovered (1.17's lesson from L4 a), and the capture
  keeps it there.
- **What changes in git.** K1 stages a hunk and two lines of `src/Calculator.cs`, K4 stages `README.md`,
  K7 commits; each asserts exactly that change. Every other check on a repository takes its
  `ExactStateAsync()` before its first step and asserts it unchanged at its end (the report's "Nothing
  confirmed"); K3 and K8 take it after the hold is set, and also take `git for-each-ref`, which no
  cancelled fetch may move. K5, on an empty tab, has no repository: its "nothing changed" is the call
  log's, no `clone` call.
- **R steps** (0.5, 12.3.4, 12.5). K1 and K2, all of their steps but K1's step 11, which is scripted
  only (open point 40), in methods 1 and 2, which are the twins of launches 11 and 12 and take their
  steps in the same order; their sessions (one tab, the scenario, the theme), picture names, key counts
  and git values are constants in `Phase8aExpectations.K`. K3 to K8 are scripted only (6.5).

#### Test methods and app starts (part K)

Seven methods, seven starts, each on a fresh copy of its scenario and a fresh data folder. Methods 1
and 2 are the twins of the real window's launches 11 and 12.

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_K1_hunk_by_keys` | K1 | one tab on a fresh "changes" | 1100×700, dark | `k1a`, `k1b`, `k1c`, `k1d` |
| 2 | `Check_K2_blame_by_arrows` | K2 | one tab on a fresh "history"; `settings.json` `{"theme": "Light"}` | 1100×700, light | `k2a`, `k2b` |
| 3 | `Check_K3_K8_cancel_in_the_palette_and_the_walks` | K3, K8 | one tab on a fresh "graph"; `FetchHold.Install(data.Path, rounds: 300)` in the data folder and `HoldIn` on the copy (the conventions' "What a fake stands for") | 1100×700, dark | `k3a`, `k8a` |
| 4 | `Check_K4_row_buttons_by_tab` | K4 | one tab on a fresh "changes" | 1100×700, dark | `k4a`, `k4b` |
| 5 | `Check_K5_focus_after_choose` | K5 | one empty tab; a copy of 7a's "hosting" (`Scenarios.HostingAsync()`) served by `Scenarios.ServeHostingAsync(copy, port: 0, ownsCopy: true)` (a free port, ruling 20); `ShellDriver.Start(data.Path, 1100, 700, gitHub: served.GitHub)`, then `WaitUntilReadyAsync()` and `MoveToNeutral()`, as 7a's `StartEmptyAsync(data, served)` | 1100×700, dark | `k5a` |
| 6 | `Check_K6_loading_diff_keeps_the_summary` | K6 | one tab on a fresh "changes"; the probe that hands over `HeldGitRunner` | 1100×700, dark | `k6a` |
| 7 | `Check_K7_committing` | K7 | one tab on a fresh "changes"; the probe that hands over `HeldGitRunner` | 1100×700, dark | `k7a`, `k7b` |

#### Steps and expected results (part K)

A step numbered in a method runs on from that method's last step in an earlier check.

**K1. A hunk and chosen lines by the keys** (R; D426; design 6.1 F1, 6.3.1, 6.3.10, 6.5, 12.3.4 launch
11). Method 1.

Steps:
1. Open the tab on the fresh "changes"; wait until it is ready and the graph drawn. Take
   `ExactStateAsync()`.
2. Click graph row 0, the working-changes row; wait for the stage panel and its lists.
3. Press F6; wait until the focus is in the right panel.
4. Press Tab 18 times, one at a time, each followed by a wait for the element step 4's expected result
   names next; after the 18th, `StageFileRow` `src/Calculator.cs` has the focus.
5. Press Enter; wait until the diff of `src/Calculator.cs` shows its text and the focus is in `DiffText`.
6. Press Tab. Screenshot (`k1a-hunk-button`).
7. Press Enter; wait until the stage operation has ended and the diff has been read again.
   Screenshot (`k1b-hunk-staged`).
8. Press Shift+Tab. Screenshot (`k1c-toolbar`).
9. Press Tab; wait until the focus is in `DiffText`. Press Ctrl+Home, then Down four times, then
   Shift+Down twice. Rest the pointer at the neutral point. Press Shift+F10; wait for the menu headed
   `Calculator.cs`. Screenshot, pointer kept (`k1d-lines-menu`).
10. Press Enter; wait until the menu has closed, the operation has ended and the diff has been read
    again.
11. Scripted only (open point 40): press Tab until `DiffText` has the focus, then Shift+Tab until
    `SideBySideDiffButton` has the focus, at most four presses each, each followed by a wait for the
    focused id. Press Enter; wait until `SideBySideDiffButton` is selected.

Expected:
- After step 2: `Unstaged files (7)` and `Staged files (3)`, the rows in the order the conventions give
  (phase 2's `UnstagedFlat` and `StagedFlat`); nothing focused in the right panel.
- After step 3: `StageFileRow` `README.md` (the unstaged list's first row) has the focus with
  `:focus-visible`, `FocusedRegion()` Right: phase 6's `k12b` state. Its row is neither hovered nor
  selected; its `rowActions` panel is shown (`:focus-within`, D425) with `RowStageButton` and
  `RowDiscardButton` effectively visible at `Opacity` 0. Every realized row keeps its buttons in the
  visual tree; no other row's `RowStageButton`, `RowDiscardButton` or `RowUnstageButton` is effectively
  visible (`IsEffectivelyVisible` false, their `rowActions` panels hidden), so none is in UI Automation
  (D425).
- During step 4: after each Tab the focused element is the next stop of 6.3.4's order, a row's
  `StageFileRow` (its automation name the row's path), that row's `RowStageButton`, its
  `RowDiscardButton`, then the next row's `StageFileRow`: `README.md`, `assets/logo.png`, `data/blob.bin`,
  `data/large.txt`, `docs/guide.md`, `docs/old-notes.txt`, `src/Calculator.cs`. Each `RowStageButton` and
  `RowDiscardButton` has `Opacity` 1 and `:focus-visible` while it has the focus, and `Opacity` 0 when the
  focus has moved on within its row; a row the focus has left shows no buttons. Nothing is staged or
  discarded (`ExactStateAsync()` unchanged). After the 18th Tab `StageFileRow` `src/Calculator.cs` has the
  focus with `:focus-visible`.
- After step 5: `DiffFileName` `Calculator.cs` (folder `src`), inline (`InlineDiffButton` selected, the
  default), two hunk bars, each with `Stage hunk` (`StageHunkButton`) and `Discard hunk`
  (`DiscardHunkButton`). In the toolbar, at the left `InlineDiffButton` `Inline` (selected) and
  `SideBySideDiffButton` `Side by side`; at the right `StageFileButton` `Stage file` and
  `DiscardFileButton` `Discard file`, shown and enabled. `StageLinesButton`, `DiscardLinesButton`,
  `UnstageLinesButton` and `UnstageFileButton` are not shown (`IsVisible` false, so not effectively
  visible): the line buttons take the file buttons' place only while lines are selected
  (`ShowsLineStageDiscard`, `DiffView.axaml:125`, `:132`; phase 2's toolbar), and the two unstage
  buttons belong to a staged diff (`ShowsLineUnstage`, `ShowsFileUnstage`, `:139`, `:160`). They are
  hidden, not disabled (their commands have no `CanExecute`): no toolbar button is disabled. The
  keyboard focus is in `DiffText` (its text area), without `:focus-visible`: a diff opened by Enter on
  a row still takes the focus (6.3.6, 6.4). The row `src/Calculator.cs` is selected.
- After step 6 (`k1a`): the first hunk's `StageHunkButton`, on the bar of the change in `Add`, has the
  focus with `:focus-visible`, Fluent's frame drawn around `Stage hunk` (6.3.1); nothing staged.
- After step 7 (`k1b`): in git, `git diff --cached --numstat -- src/Calculator.cs` prints `1 1
  src/Calculator.cs` and `git diff --numstat -- src/Calculator.cs` prints `4 0 src/Calculator.cs`; the
  index's file (`git show :src/Calculator.cs`) is the committed one with `        return a + b +
  _offset;` in `Add` and no other line changed; no other path's state changed. In the app: the diff,
  read again, shows one hunk bar (one `StageHunkButton`, one `DiscardHunkButton`), the four added lines;
  `Staged files (4)` lists `src/Calculator.cs`, which `Unstaged files (7)` still lists. The focus is in
  `DiffText`'s text area without `:focus-visible` (the text took it before the bar left, 6.3.1): no frame
  is drawn and no hunk button has the focus.
- After step 8 (`k1c`): `DiscardFileButton` `Discard file`, the toolbar's last shown button (the hidden
  `UnstageFileButton` after it takes no focus), has the focus with `:focus-visible` (6.3.1).
- After step 9 (`k1d`): before Ctrl+Home the focus is in `DiffText`'s text area. After the keys the
  diff's selected rows are rows 4 and 5 of the remaining hunk, counting its header as row 0: the added
  empty line and `    public int Negate(int a) => -a;` (Shift with the arrows selects whole rows from the
  caret, a last row reached only at its start left out; 6.3.10). The toolbar shows `Stage lines` and
  `Discard lines` (`StageLinesButton`, `DiscardLinesButton` effectively visible and enabled) in place of
  `Stage file` and `Discard file`, which are not shown (`ShowsFileStageDiscard` false once lines are
  selected). The menu, opened by the keyboard: header `Calculator.cs`; `Stage selected lines`
  (highlighted, D105); `Discard selected lines…`; then the file's own items. No item is hovered.
- After step 10: `git diff --cached --numstat -- src/Calculator.cs` prints `3 1 src/Calculator.cs` and
  `git diff --numstat -- src/Calculator.cs` prints `2 0 src/Calculator.cs`; the index's file ends with
  `    }`, an empty line, `    public int Negate(int a) => -a;` and `}`; the diff shows the two lines still
  unstaged, the second empty line and `    public int Square(int a) => a * a;`.
- After step 11 (scripted only): `SideBySideDiffButton` `Side by side` selected; no git change: the two
  `--numstat` lines as after step 10.
- At the method's end: step 1's `ExactStateAsync()` and the end's differ only in `src/Calculator.cs`'s
  lines: its `ls-files --stage` entry (the index's blob) and its `status --porcelain=v2` line. HEAD, the
  refs, every other path's entries and the working files are unchanged.

**K2. A blame run picked by the arrows, light** (R; design 6.1 F2, 6.3.2, 6.5, 12.3.4 launch 12).
Method 2.

Steps:
1. Open the tab on the fresh "history", light from the start; wait until it is ready, the window light.
   Take `ExactStateAsync()`.
2. Click graph row 0, the working-changes row; wait for the stage panel, its one unstaged row
   `src/Reader.cs`.
3. Press F6; wait until `StageFileRow` `src/Reader.cs` has the focus. Press Enter; wait until its diff
   shows its text and the focus is in `DiffText`.
4. Press Ctrl+Shift+B; wait until the blame page shows its text and the focus is in `BlameText`.
5. Press Down three times, one at a time, each followed by a wait until the picked run is the caret's
   and the right panel shows that run's commit (the stage panel for run 1). Screenshot
   (`k2a-blame-run`).
6. Rest the pointer at the neutral point. Press Shift+F10; wait for the menu. Screenshot, pointer kept
   (`k2b-blame-menu`).
7. Press Esc; wait until the menu has closed. Press Esc; wait until the blame page has closed.

Expected:
- After step 4: the page `Blame` in the graph's place: `BlameTitle` `Blame of Reader.cs`, `BlameCaption`
  `in the working tree` (phase 5's `b6a`), 24 lines in the 15 runs of `ReaderWorkingTreeRuns`. No run is
  picked (`BlameGutter.SelectedRun` null: opening picks nothing, 6.3.2); the caret is on line 1; the
  keyboard focus is in `BlameText`'s text area.
- During step 5 (6.3.2: after Up, Down, Left, Right, PageUp, PageDown, Home or End the caret's run is
  picked when it is not the picked one): after the first Down the caret is on line 2 and run 0 (lines
  1 to 2, `1d5a7cf`) is picked, and the right panel shows `1d5a7cf`'s details (`DetailsSubject` `Add the
  parser`); after the second, line 3 and run 1 (not committed yet), the working-changes row selected
  behind the page (`BlameViewModel.Pick`) and the right panel the stage panel (`StagePanel` shown,
  `UnstagedTitle` `Unstaged files (1)`); after the third, line 4 and run 2 (`0575c7f`), the right panel
  `0575c7f`'s details (`DetailsSubject` `Rename the parser to reader`). The focus stays in `BlameText`
  throughout.
- After step 5 (`k2a`): run 2 picked. The gutter beside line 4, at a point 2 px inside the gutter's right
  edge at the line's centre (as phase 5's `b6c` reads it), is `#DDE1FA` (`VcSelectionBrush`, light);
  beside line 1 (run 0, an even run) `#F5F6F8` (`VcPanelBackgroundBrush`) and beside line 3 (run 1, odd)
  `#FFFFFF` (`VcWindowBackgroundBrush`). The graph behind the page has `0575c7f` selected and the right
  panel shows its details, `Rename the parser to reader` by Mira Kovac, `mira@example.com`, as phase 5's
  `b6d` shows them. No menu is open.
- After step 6 (`k2b`): the menu of run 2, opened by the keyboard at the text's top left: header
  `0575c7f Rename the parser to reader`; `Show in graph` (highlighted, D105); —; `Blame before this
  commit`; `File history`; —; `Copy SHA` (phase 5's `RunMenu`, 154 high). Run 2 is still the picked run.
- After step 7: no menu, no page; the graph shows. Nothing in the menu was pressed; `ExactStateAsync()`
  unchanged.

**K3. "Cancel {title}" in the palette** (not R; D417, D452; design 1.8, 6.1 F3, 6.3.3, 6.5). Method 3.

Steps:
1. Open the tab on the fresh "graph", its fetches held (`FetchHold.Install(data.Path, rounds: 300)` and
   `HoldIn` on the copy before the start, so that the hold outlasts the method on every platform; the
   conventions' "What a fake stands for"); wait until it is ready. Take `ExactStateAsync()` and `git
   for-each-ref`. Click the graph row of "Add settings page" (HEAD's commit); wait for its details.
   Rest the pointer at the neutral point.
2. Press Ctrl+Shift+P; wait until the palette is open. Press Esc; wait until it has closed.
3. Right-click `origin` under Remotes in the left panel; wait for the menu headed `origin`; click `Fetch
   origin`. Wait until one fetch is held (`WaitForHeldAsync(1)`) and `OperationCancelButton` shows.
4. Rest the pointer at the neutral point. Press Ctrl+Shift+P; wait until the palette is open.
   Screenshot (`k3a-palette-cancel`).
5. Press Enter; wait until the palette has closed and the fetch has ended.
6. Release the hold (`Release()`), so that the cancelled fetch's shell, which on Linux and macOS the
   cancel left waiting, ends. Press Ctrl+Shift+P; wait until the palette is open. Press Esc; wait until
   it has closed.

Expected:
- After step 2: nothing runs; no palette row's label starts with `Cancel ` (the entry is listed only
  while the tab's operation can be cancelled, 1.8).
- After step 3: `OperationStatus` `Fetching from origin…`, the progress bar without a percentage,
  `OperationCancelButton` `Cancel` shown; one held marker of `origin.git`.
- After step 4 (`k3a`): the palette's first row: label `Cancel Fetch origin`, enabled, highlighted, no
  detail (one tab: dialogs do not name the repository, `NamesRepositoryInDialogs`), no key
  (`CommandPaletteItemKey` not shown), not destructive (no `danger` class); `CommandPaletteBox` empty with
  the keyboard focus. The fetch is still running.
- After step 5: the palette has closed; the fetch ended `Cancelled` (its operation's record);
  `OperationStatus` `Fetch origin cancelled` (`OperationTexts.Cancelled`, 6.3.3), no progress bar, no
  `Cancel`; `git for-each-ref` prints what step 1 read: no remote ref moved. The log has no line `Cancel
  Fetch origin: that operation is no longer running.` (the entry ran while its fetch was the running
  operation, D452).
- After step 6: no palette row's label starts with `Cancel `.

Note (§6's build, `55b8d3a`): before `HoldIn`, method 3 writes the "graph" copy's `origin` URL again
with forward slashes, the same folder (`git remote set-url origin`). Reason: `FetchHold`'s script
(phase 6's harness) names its marker after the remote's path behind its last forward slash
(`${remote##*/}`); on Windows git hands it the copy's `origin.git` path with backslashes, so the
marker's name held the whole path, the script failed (`No such file or directory`) and no fetch was
held. §6's build left `FetchHold.cs` as it was, and no expected result of K3 or K8 changed. Wave 3
changed its script (`f019688`: a fetch that saw its release goes on without looking again; the
conventions' "What a fake stands for", open point 42); no expected result of K3 or K8 changed with it.

**K4. A row's buttons by Tab, and the focus after its action** (not R; D425; design 6.1 F4, 6.3.4,
6.3.5, 6.5). Method 4.

Steps:
1. Open the tab on the fresh "changes"; wait until it is ready. Take `ExactStateAsync()`.
2. Click graph row 0; wait for the stage panel and its lists.
3. Press F6; wait until `StageFileRow` `README.md` has the focus.
4. Press Tab. Screenshot (`k4a-row-stage`).
5. Press Enter; wait until the lists show the result and the focus has moved. Screenshot
   (`k4b-row-staged`).

Expected:
- After step 3: as K1 after its step 3: `README.md`'s row has the keyboard's frame on its own button,
  its `RowStageButton` and `RowDiscardButton` effectively visible at `Opacity` 0, no other row's buttons
  effectively visible, nothing drawn on the row but the frame (`k12b`'s state, 6.3.4).
- After step 4 (`k4a`): `README.md`'s `RowStageButton` has the focus with `:focus-visible` and `Opacity` 1:
  `Stage` drawn and framed; its `RowDiscardButton` `Opacity` 0; nothing staged.
- After step 5 (`k4b`): in git, `git status --porcelain -- README.md` prints `M  README.md` (it printed
  `MM README.md`), `git diff --cached --numstat -- README.md` prints `5 1 README.md` and `git diff
  --numstat -- README.md` prints nothing; no other path changed. In the app: `Unstaged files (6)`, its
  first row `assets/logo.png`; `Staged files (3)`; the focus on `assets/logo.png`'s `StageFileRow`, the
  row now at the staged row's place (6.3.4's first case), with `:focus-visible` (`Tab`, 6.3.5), its row's
  buttons at `Opacity` 0; no diff opened.

**K5. The focus after "Choose"** (not R; F5-A; design 6.1 F5, 6.3.5, 6.5). Method 5.

Steps:
1. Serve a copy of 7a's "hosting" (`Scenarios.HostingAsync()`) by `Scenarios.ServeHostingAsync(copy,
   port: 0, ownsCopy: true)` (a free port, ruling 20). Start on one empty tab with
   `ShellDriver.Start(data.Path, 1100, 700, gitHub: served.GitHub)`, then `WaitUntilReadyAsync()` and
   `MoveToNeutral()`, as 7a's `StartEmptyAsync(data, served)` (the conventions' "Scenarios and
   sessions"). Take the call log's mark. Sign in to the fake as `octo` through the session
   (`SignInAsync`, the fake's made-up token, as phase 7a's B1), which waits until the account is signed
   in and fails otherwise.
2. Click `CloneRepoButton` (`Clone`); wait until the clone form shows, `CloneFromGitHubButton` reads
   `From GitHub…` and `CloneUrlBox` has the focus.
3. Press Shift+Tab; wait until `CloneFromGitHubButton` has the focus.
4. Press Enter; wait until the browser `CloneFromGitHubDialog` is open and lists its rows.
5. Press Tab; wait until the focus is in `CloneFromGitHubList`. Press Home; wait until `octo/hello` is
   selected.
6. Press Enter; wait until the browser has closed and `CloneFromGitHubButton` has the focus. Rest the
   pointer at the neutral point. Screenshot (`k5a-chosen`).

Expected:
- After step 3: `CloneFromGitHubButton` `From GitHub…` has the focus with `:focus-visible` (the button
  lies before `CloneUrlBox` in the Tab order).
- After step 4: the browser over the form, titled `Clone from GitHub`; `CloneFromGitHubFilter` empty
  with the keyboard focus; the list's four rows in phase 7a's order, `octo/hello` first (`Private`);
  `Choose` (`ConfirmButton`) disabled, nothing selected.
- After step 5: `octo/hello` selected; `Choose` enabled.
- After step 6 (`k5a`): the browser has closed; `CloneUrlBox` `http://127.0.0.1:{port}/octo/hello.git`
  (the fake's port), `CloneParentBox` empty, `CloneNameBox` `hello`; `CloneFromGitHubButton` enabled, with
  the keyboard focus and without `:focus-visible`: no frame (F5-A gives the focus back with
  `NavigationMethod.Unspecified` once the command has stopped running, 6.3.5). No `git clone` ran: the
  call log since step 1's mark holds no `clone` call (K5 has no repository to read).
  Inspected against phase 7a's `b06a-chosen` (wave 1's references): the same form and values, the URL's
  port aside.

**K6. A loading diff leaves the moved focus where it is** (not R; design 6.1 F6, 6.3.6, 6.5). Method 6.

Steps:
1. Open the tab on the fresh "changes", `HeldGitRunner` handed over; wait until it is ready. Take
   `ExactStateAsync()`.
2. Click graph row 0; wait for the stage panel and its lists, then until the app is idle.
3. Press F6; wait until `StageFileRow` `README.md` has the focus. Set the runner to hold the next
   `diff-files` call. Press Enter; wait until the held call has started.
4. Press Ctrl+Shift+M; wait until `CommitSummary` has the focus.
5. Release the runner; wait until the diff shows its text. Screenshot (`k6a-summary-kept`).

Expected:
- After step 3: the held call is `diff-files -p`, its arguments ending `-- README.md`; `DiffView` shows
  `README.md`, its body loading (`DiffTextHost` not shown); the focus still on the row.
- After step 4: `CommitSummary` has the keyboard focus, its template's `Border#PART_BorderElement` with
  `BorderBrush` `#7C8CFF` (`TextControlBorderBrushFocused`, dark; `CommitSummary.BorderBrush` itself
  stays `TextControlBorderBrush`, `#323845`, and is not what is read). This step has no picture, so the
  template part's brush is the check.
- After step 5 (`k6a`): the diff shows `README.md`'s unstaged change, its text holding `A small calculator
  for the visual checks.`; `CommitSummary` still has the keyboard focus, its `PART_BorderElement`'s
  `BorderBrush` still `#7C8CFF`, and in `k6a` the pixel at the box's left border at its vertical centre
  (x = left + 0.5, y = centre, as phase 6's `Phase6Checks.L.Sweep` reads `CloneUrlBox`'s focused border)
  exactly `#7C8CFF`; nothing inside `DiffView` has the focus (the read's focus request was dropped once
  the focus had left the element it had at the open, 6.3.6). `ExactStateAsync()` unchanged.

**K7. The commit boxes and Amend while committing** (not R; D392; design 6.1 F7, 6.3.7, 6.5). Method 7.

Steps:
1. Open the tab on the fresh "changes", `HeldGitRunner` handed over; wait until it is ready.
2. Click graph row 0; wait for the stage panel and its lists.
3. Press Ctrl+Shift+M; wait until `CommitSummary` has the focus. Type `First`.
4. Set the runner to hold the next `commit` call. Press Ctrl+Enter; wait until the held call has
   started. Screenshot (`k7a-committing`).
5. Press Ctrl+A; type `Next`.
6. Release the runner; wait until the commit has ended and the lists have been read again.
   Screenshot (`k7b-committed`).

Expected:
- After step 3: `CommitSummary` `First`, `SummaryCounter` `67`; `CommitButton` `Commit 3 files`, enabled;
  `AmendCheckBox` `Amend previous commit` enabled, unticked.
- After step 4 (`k7a`): the held call is `commit --file=- --cleanup=whitespace`. `CommitButton`
  `Committing…`, disabled; `AmendCheckBox` disabled (`CanToggleAmend` false while committing, 6.3.7),
  unticked; `CommitSummary` and `CommitDescription` enabled, the summary `First` with the keyboard focus,
  its `PART_BorderElement`'s `BorderBrush` `#7C8CFF`, and in `k7a` the pixel at the box's left border at
  its vertical centre (x = left + 0.5) exactly `#7C8CFF`. `OperationStatus` `Ready`
  (`OperationStatusViewModel.ReadyText`): a stage-panel write leaves the status bar as it is when idle
  (`ErrorDisplay.StagePanel`, `OperationStatusViewModel.cs:311-315`); `OperationProgress` is not shown
  and `OperationCancelButton` is not shown. HEAD still the scenario's "Add calculator".
- After step 5: `CommitSummary` `Next`, `SummaryCounter` `68`; still `CommitButton` `Committing…`, Amend
  disabled, `OperationStatus` `Ready`.
- After step 6 (`k7b`): in git, `git log -1 --format=%B` prints `First` (the message read when the
  commit started), HEAD's parent is "Add calculator", `git diff --cached --name-only` prints nothing, and
  the unstaged changes are as before. In the app: `CommitSummary` `Next`, not emptied, since it no
  longer holds what was committed (6.3.7), `SummaryCounter` `68`, with the keyboard focus, its
  `PART_BorderElement`'s `BorderBrush` `#7C8CFF`, and in `k7b` the pixel at the box's left border at its
  vertical centre (x = left + 0.5) exactly `#7C8CFF`; `CommitDescription` empty; `AmendCheckBox` enabled
  and unticked; `CommitButton` `Stage files to commit`, disabled; `Staged files (0)` with `No staged
  changes`; `Unstaged files (7)`; the graph's first commit row `First` on `main`, the working-changes row
  above it still selected; no `CommitError`; `OperationStatus` `Ready` (a stage-panel write's result,
  `OperationStatusViewModel.cs:267-273`).

**K8. The walk to Cancel, and a splitter by the arrows** (not R; D427; design 6.1 G7 and G8, 6.3.9,
6.5). Method 3, after K3's step 6.

Steps:

7. Set the hold again (`Arm()`: K3's step 6 released it). Right-click `origin` under Remotes; click
   `Fetch origin`. Wait until one fetch is held (`WaitForHeldAsync(1)`) and `OperationCancelButton`
   shows. The hold installed with `rounds: 300` in K3's step 1 outlasts this step's walk (the
   conventions' "What a fake stands for").
8. Press F6, at most four times, until the focus is in the right panel. Press Tab, one at a time and at
   most 200 times, until `OperationCancelButton` has the focus; write the number of Tabs to the test's
   output. Press Enter; wait until the fetch has ended.
9. Release the hold (`Release()`), as in K3's step 6. Press F6, at most four times, until the focus is
   in the right panel. Press Tab, one at a time and at most 200 times, until `LeftSplitter` has the
   focus; write the number of Tabs to the test's output. Rest the pointer at the neutral point. Press
   Right. Screenshot (`k8a-splitter`).

Expected:
- After step 7: as K3 after its step 3.
- After step 8: the walk reached `OperationCancelButton` within 200 Tabs (6.4's G7 bound), its number
  recorded for the report. After Enter: the fetch ended `Cancelled`; `OperationStatus` `Fetch origin
  cancelled`, no `Cancel`; `git for-each-ref` as step 1 read it.
- After step 9 (`k8a`): `LeftSplitter` has the focus with `:focus-visible`, the walk's number recorded.
  After Right the left panel (`LeftPanel`) is 270 wide (it was 260, `PanelLayout.LeftDefault`): 10 wider
  (6.3.9, keyboard.md's `KeyboardIncrement`); the graph area starts 10 further right and is 10 narrower;
  the right panel keeps its bounds (400 wide). The session's `LeftPanelWidth` is 270: a keyboard move is
  fitted and remembered as a drag's end is (D427). `ExactStateAsync()` unchanged.
- At the method's end: the hold disposed in its `finally` (a release) without failing: no held fetch
  gave up (`FetchHold.GaveUp()` empty).

#### Texts these checks rely on (part K)

New in 8a: the palette entry `Cancel {title}` (`DashboardTexts.CancelTitle`), here `Cancel Fetch origin`,
with no key and no detail on one tab (design 1.8, 6.3.3; D417); the Info line `Cancel {title}: that
operation is no longer running.`, which K3 asserts absent (1.8, 6.3.3; D452). No other text is new: 6.3
changes what the keys do, not what the app says.

Today's texts the steps pass through, as the code has them on `85c76c6`:
- The diff: the hunk bars' `Stage hunk`, `Discard hunk` (`DiffTextView.cs:292-293`; 6.3.1); the toolbar's
  `Inline`, `Side by side`, `Stage lines`, `Discard lines`, `Stage file`, `Discard file`
  (`DiffView.axaml`; 6.3.1 names `Discard file`); the menu's `Stage selected lines`, `Discard selected
  lines…` (`OperationTexts.Menus`; 6.3.10, 6.5), headed by the file's name `Calculator.cs`.
- The blame: `Blame of Reader.cs`, `in the working tree`; the run's menu `0575c7f Rename the parser to
  reader`, `Show in graph` (6.3.2), `Blame before this commit`, `File history`, `Copy SHA` (phase 5's B6).
- The fetch: the remote's menu `Fetch origin` (`OperationTexts.Menus.FetchRemote`), the running text
  `Fetching from origin…` and the result `Fetch origin cancelled` (`OperationTexts.Fetch`,
  `OperationTexts.Cancelled`; 6.3.3), the status bar's `Cancel`. `cancelled` is the app's own spelling
  since phase 3 (D113), which 6.3.3 quotes and no package of 8a changes, so the checks read it word for
  word.
- The stage panel: `Unstaged files (7)`, `Unstaged files (6)`, `Unstaged files (1)` (K2's "history"),
  `Staged files (3)`, `Staged files (4)`, `Staged files (0)`, `No staged changes`, `Commit 3 files`,
  `Committing…` (`CommitButton`'s text alone, `WorkingChangesViewModel.CommitButtonText`; 6.5's K7),
  `Stage files to commit`, `Amend previous commit`, the counter `67` and `68`
  (`WorkingChangesViewModel.SummaryLimit` 72 less the summary's length), the typed `First` and `Next`
  (6.5).
- The status bar: `Ready` (`OperationStatusViewModel.ReadyText`), which K7 reads while the commit is held
  and after it.
- The commit details: the subjects `Add the parser` and `Rename the parser to reader` in
  `DetailsSubject` (K2; phase 5's B6).
- The clone form: `Clone`, `From GitHub…` (`OperationTexts.CloneTexts.FromGitHubButton`; 6.3.5), the
  browser's title `Clone from GitHub`, `Choose`, the row `octo/hello` · `Private`, the URL
  `http://127.0.0.1:{port}/octo/hello.git` and the name `hello` (phase 7a's part B).

#### The real window (part K)

Two launches (12.3.4 launches 11 and 12), each with exactly its twin method's tab and starting theme
(D442), 1100×700, a new data folder, the scenario's copy declared before the app (12.3.1); six
captures, each compared with its scripted twin by name, at most 3 % of its pixels different (D33,
D58). The first input of each launch is a real click, which brings the window to the front, so no key
goes to another window (12.3.3). Every key is a real key, pressed only after the wait its scripted twin
makes; every Tab of a walk is followed by `WaitForFocusedElement` of the id the twin's expected result
names next, whose failure ends the method before any further key (as F13's walk). "The focus in
`DiffText`" (or `BlameText`) is the text's wait: the keyboard focus is then on AvaloniaEdit's
`TextArea` inside the editor, which has no automation id of its own, so in place of
`WaitForFocusedElement("DiffText")` the method waits until UI Automation's focused element is `DiffText`
(`BlameText`) or lies inside that element's bounding rectangle and is no button, and writes to run.txt
which of the two held; in launch 11 the next real Tab's `StageHunkButton` then proves that the text had
the focus. Before each Shift+F10 the real pointer rests at the neutral point, and the capture of the
open menu keeps it there (1.17's lesson from L4 a). No step presses anything of 12.3.2's list: K2's
menu is closed by Esc, `Copy SHA` is never pressed. run.txt gets each launch's start and end and every value below. The UI Automation names
the walk meets are §9's from its merge (`Stage {0}` on a row's button, 1.12); the walk asserts ids and
the rows' names (their paths), not the buttons' names.

11. `Check_K1_hunk_by_keys` (twin: method 1; "changes", one tab, dark). A real click on graph row 0; a
    real F6, then `StageFileRow` `README.md` focused; 18 real Tabs through the order of K1's step 4,
    ending on `StageFileRow` `src/Calculator.cs`; a real Enter: the focus in `DiffText` (the text's
    wait). A real Tab: `StageHunkButton` focused; `k1a-hunk-button`. A real Enter: `git diff --cached
    --numstat -- src/Calculator.cs` prints `1 1 src/Calculator.cs` (`Phase8aExpectations.K`), the focus in
    `DiffText` (the text's wait); `k1b-hunk-staged`. A real Shift+Tab: `DiscardFileButton` focused;
    `k1c-toolbar`. A real Tab (the focus in `DiffText`, the text's wait), Ctrl+Home, Down four times,
    Shift+Down twice; the pointer at the neutral point; a real Shift+F10: the menu holds `Stage selected
    lines`; `k1d-lines-menu` (pointer kept). A real Enter: `git diff --cached --numstat --
    src/Calculator.cs` prints `3 1 src/Calculator.cs`. run.txt: the focused ids of the walk, which of the
    text's two conditions held at each of its waits, both `--numstat` lines. The app closed by its window,
    exit code 0.
12. `Check_K2_blame_by_arrows` (twin: method 2; "history", one tab, `{"theme": "Light"}`). A real click on
    graph row 0; a real F6 (`StageFileRow` `src/Reader.cs` focused) and Enter (the focus in `DiffText`, the
    text's wait); a real Ctrl+Shift+B: the blame page, the focus in `BlameText` (the text's wait); three
    real Downs (`Phase8aExpectations.K`'s count). After each, the method waits for what its pick shows in
    UI Automation; the caret is not read, because `BlameText`'s peer gives its value alone
    (`BlameTextEditor.cs:204-217`) and `BlameGutter`'s gives no picked run:
    - after the first, `DetailsSubject` reads `Add the parser`;
    - after the second, the stage panel shows (`UnstagedTitle` reads `Unstaged files (1)`, as
      `Phase2RealWindowPass` finds that title), run 1 being not committed yet (`BlameViewModel.Pick`
      selects the working-changes row);
    - after the third, `DetailsSubject` reads `Rename the parser to reader` (as `Phase3RealWindowPass.A`'s
      `WaitForDetails`).

    Then `k2a-blame-run`. The pointer at the neutral point; a real Shift+F10: the run's menu, headed
    `0575c7f Rename the parser to reader`; `k2b-blame-menu` (pointer kept). Esc twice: no menu, no page.
    run.txt: each Down's wait, which of the text's two conditions held at each of its waits, the menu's
    header and items. The app closed by its window, exit code 0.

Unverified until D246's runs: that a capture of Fluent's focus frame at 200 % stays within 3 % of its
twin; what UI Automation reports as focused while AvaloniaEdit's `TextArea` in `DiffText` or `BlameText`
has the focus (the text's wait above; no earlier real-window check reads the focus inside an
AvaloniaEdit text); that the stage panel's `UnstagedTitle` is found in launch 12 only while the stage
panel shows; and every point of 6.9 that the scripted run settles first (open point 37).

### Part B: the icon

Part B covers the icon (R5; D407, D428 to D430, D447; design 7): the owner's mark, Lens, chosen on
2026-11-20 with round 2's three masters (design 1.17, 7.8), in the files the icon's tool makes from
them, each shown at its own pixel size on the dark and on the light window background (B1, B2: a sheet
of the `.ico`'s 14 frames and of nine generated PNGs); and, in the real window, the executable's icon,
the window's two icons, the title bar dark and light, and the taskbar button (design 7.5, 12.3.4 launch
3). Checks B1 and B2, design 7.5's list. Neither scripted check is repeated as it is: the real window has
steps of its own for each, in the launch it shares with part A, and its six captures have no scripted
twin; they are inspected and compared with none (7.5, 12.5). Where section 7 says "the placeholder",
this part reads the owner's mark (7.8): §7 writes no placeholder, so no picture of one ever exists.

#### Conventions of part B

- **Code** (7.2): `Phase8aChecks.B.cs` (`Phase8aChecksB : Phase8aChecks`), `IconSheet.cs` (a harness
  control of 8a's project, not the app's), `Phase8aExpectations.B.cs` (the lists, equal to `IconFile`'s;
  the sheet's layout and its heading `VisualCommit.ico`; the colours below; the tolerance 2; the picture
  names; and for §12 launch 3's facts for part B: the six capture names, the crop and its enlargement,
  the taskbar button's name and limit, and the run.txt line), `Phase8aExpectationTests.B.cs`
  (`Phase8aExpectations.B`'s lists equal `IconFile`'s; every picture name matches 0.3's pattern; the
  sheet's rows fit 1100×700). `IconFile` and `PngHeader` (Testing, plain .NET, 1.15) read the files in
  both harnesses. §12's code is
  `Phase8aRealWindowPass.AB.cs` with `RealApp.Phase8a.cs`'s `ExecutablePath`, `WindowIcons`,
  `CaptureScreen` and `TaskbarButton` (12.2).
- **The masters, and what each file is made from** (7.3, 7.8; D429, D447). Round 2's three masters,
  copied unchanged into `assets/brand/`, each with the first comment `<!-- VisualCommit's mark, chosen by
  the owner on 2026-11-20 (D407). -->`. The mark (round 2's README): a blue tile `#4C5BE0` with rounded
  corners; a white line across its middle, from the tile's left edge to the eye and from the eye to the
  tile's right edge; the white outline of an eye, pointed at both ends; a round white pupil at the
  centre.

  | Master | viewBox | What it draws | Files made from it (7.3) |
  |---|---|---|---|
  | `icon-small.svg` | `0 0 16 16` | the tile filling its square, transparent at its four rounded corners alone (radius 3.75); the line two units tall (rows 7 to 9), three units in from each edge; the eye an outline one unit thick, with a blue row between it and the pupil above and below; the pupil 4.2 units across | the `.ico`'s 16, 20, 24, 30 and 32; `png/icon-16`, `-24`, `-32` |
  | `icon.svg` | `0 0 1024 1024` | the tile 896 wide with a transparent margin of 64 (one sixteenth) on every side, corner radius 208; the line and the eye's outline each 64 thick; the pupil 192 across | the `.ico`'s 36 to 256; `png/icon-48` to `-512`; `VisualCommit-48.png`, `-96.png` |
  | `icon-macos.svg` | `0 0 1024 1024` | the mark on Apple's opaque rounded square: the plate 832 wide, 96 from each edge, corner radius 187; the line from the plate's edges to the eye; the eye's outline of the same height and thickness as `icon.svg`'s but narrower (its points at x 200 and 824 of 1024, against 184 and 840); the same pupil, 192 across | the `.iconset`'s ten PNGs, and the `.icns` from them |

  What the sizes show by nature, which the inspection reads as round 2's README ("What is still soft")
  says it, with one correction of arithmetic: 16 and 32 are crisp, every edge of the 16 grid on a whole
  pixel (×1, ×2); the README calls 24 crisp too, but at 24 (×1.5) every odd unit of the grid falls on a
  half pixel, so the line (rows 7 to 9 of the grid, 10.5 to 13.5 at 24) shows two solid rows with a
  half-covered row above and below, as round 2's own `small-zoom.png` shows; 20 is the softest small
  size (every edge of the 16 grid falls on a quarter pixel there); the small sizes fill their square
  while the large ones keep the margin, so the tile is 32 pixels wide at 32 and 31.5 at 36; `icon.svg`
  at 48 draws the line as two solid rows with a half-covered row on each side, and at 36 and 40 the
  tile's edge falls on a quarter or half pixel; macOS's 16 (the `.iconset`'s `icon_16x16`) is soft in every set drawn; on a light desk the line's white
  ends blend into it at 16 to 24 pixels.
- **The sheet** (7.5). `IconSheet` in a headless `Window` of 1100×700 with the app's resources, its
  `RequestedThemeVariant` Dark (B1) or Light (B2), `VcWindowBackgroundBrush` behind; no scenario
  repository, no git, no session, no data folder and no `MainWindow`. It reads its files through
  `IconFile.RepositoryPath` (1.15): the `.ico` at `src/VisualCommit.App/Assets/Icon/VisualCommit.ico`, its
  frames by `IconFile.Read`, and row 2's PNGs under `assets/brand/generated/` (none of them is an Avalonia
  resource). Row 1, headed `VisualCommit.ico`: the `.ico`'s 14 frames in `IconFile.IcoSizes`' order, left
  24, gap 12, bottoms aligned, each an `Image` at its own pixel size (`Stretch="None"`, at whole-pixel
  positions), its size below it in `VcTextSecondaryBrush` at font size 11. Row 2: `png/icon-16.png`,
  `-24`, `-32`, `-48`, `-64`, `-128`, then `VisualCommit.iconset/icon_16x16.png`, `icon_32x32.png` and
  `icon_128x128.png`. No row holds `png/icon-256` and `-512`, the `.iconset`'s other seven files or the
  app's two PNGs (About shows the 96 at 48×48, part A). A picture is the sheet's window's own frame at
  scale 1, 1100×700, saved as part I's notice is (the name claimed once in the run, the step's time noted
  under `timings/`); no pointer moves and no key is pressed.
- **Colours** (tokens the round keeps; 1.5): the background `VcWindowBackgroundBrush`, `#14161B` dark,
  `#FFFFFF` light; the sizes under row 1 `VcTextSecondaryBrush`, `#9BA3B0` dark, `#5C6572` light, their
  ink read by `TextInk.Problems` on that background and their `Foreground` in the tree. The mark's own
  colours are the masters' and no check asserts them as fixed values: each image is compared with its
  own file's pixel (D430: Skia's pixels may differ between machines, so the files are never compared by
  pixel).
- **How an image is read.** An image's place is its `Bounds` in the window, at scale 1. "Its centre" is
  the picture's pixel at (its left + N/2, its top + N/2), N its size; "its file's centre pixel" is the
  file's pixel (N/2, N/2), zero-based, top-down: from `IconFrame.Bgra()` for a bitmap entry, decoded for
  the `.ico`'s 256 (a PNG) and for row 2's PNGs. "Composited over" a background is that pixel,
  unpremultiplied, blended over the background by its alpha. With the owner's mark the pixel lies inside
  the white pupil in every file of both rows (at the `.iconset`'s 16 only just), so it reads white;
  the gaps prove the background (open point 47). "Its top-left pixel" is the picture's pixel at (its
  left, its top), which every master leaves transparent, so it equals the row's background: it proves
  the composition over the background, not only the pupil at the centre (open point 47).
- **By hand** (7.8, 1.17; 7.5): the look-alike check, made by eye against Git's logo and the well-known
  Git clients' icons, with no image search (the owner, 2026-11-20; round 2's README), recorded with the
  report's checks by hand; and B2's taskbar button when the real window finds none (below).
- **Shown by no check.** The notice window's icon (§4's window, 7.2) is proven by `WindowIconTests` item
  2; I2's real captures are its client area and show no title bar. The executable's icon in Explorer,
  Start and Alt+Tab is no check's. X11's window icon, an unbundled app's Dock icon and `icon-macos.svg`
  against macOS 26's grey plate are 8b's checklists (7.9 item 6). The `.icns` is `IconFilesTests`' items
  7 and 8 (D447).
- **Time** (7.7): each method about 5 to 10 s alone; part B's steps about 30 to 45 s of launch 3.

#### Test methods and app starts (part B)

Two methods; neither starts the app (`[AvaloniaFact]`, as part I's).

| # | Test method | Checks | Start | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_B1_icon_sizes_dark` | B1 | `IconSheet` in its headless `Window`, `RequestedThemeVariant` Dark; no session, scenario, git or data folder | the sheet, 1100×700, dark | `b1-icon-sizes-dark` |
| 2 | `Check_B2_icon_sizes_light` | B2 | the same, `RequestedThemeVariant` Light | the sheet, 1100×700, light | `b2-icon-sizes-light` |

B1's and B2's real-window steps are part of the real window's `Check_A_and_B_about_and_icon` (12.3.4
launch 3), shared with part A; they have no scripted twin.

#### Steps and expected results (part B)

**B1. The icon at its sizes, dark** (R: steps of its own in the real window, B1 (a) to (c), launch 3;
D407, D428 to D430, D447; design 7.3, 7.5, 7.8). Method 1.

Steps:
1. Show the sheet (the method's start), dark; wait until the window is shown and each of its 23 images
   has its bitmap and a laid-out `Bounds` that is not empty. Screenshot (`b1-icon-sizes-dark`).
2. Close the sheet's window.

Expected:
- After step 1, the frame is 1100×700 and the window's `ActualThemeVariant` is Dark.
- Row 1's heading is a text that reads `VisualCommit.ico` (7.5), `FontSize` 13, `Foreground` `#E4E7EC`
  (`VcTextPrimaryBrush`), wholly above row 1's images; under it,
  row 1: 14 `Image`s, left to right in `IconFile.IcoSizes`' order, 16, 20, 24, 30, 32, 36, 40, 48, 60,
  64, 72, 80, 96 and 256, each showing the `.ico`'s frame of that size as `IconFile.Read` gives it; each
  `Stretch` `None`, its `Bounds` exactly N×N, its left and top on whole pixels, the 14 bottoms on one line, y
  312; their lefts at x 24, 52, 84, 120, 162, 206, 254, 306, 366, 438, 514, 598, 690 and 798 (left 24,
  gap 12), the 256 ending at x 1054. (These lefts, and open point 45's, hold: open point 44 kept the bare
  number, no wider than its
  image.) Under each image its size, the bare number (open point 44): `Foreground` `#9BA3B0`
  (`VcTextSecondaryBrush`), `FontSize` 11, `FontWeight` SemiBold, under its image and within its width
  (the sheet centres it on a whole pixel), its ink `#9BA3B0` on `#14161B`.
- Row 2, wholly below row 1's sizes: nine `Image`s in this order, `png/icon-16`, `-24`, `-32`, `-48`,
  `-64`, `-128`, `VisualCommit.iconset/icon_16x16`, `icon_32x32` and `icon_128x128`, each showing the
  file of that name; each `Stretch` `None`, its `Bounds` exactly its pixel size (16, 24, 32, 48, 64, 128,
  16, 32, 128), on whole pixels, the row laid out as row 1, its bottoms on one line, y 488, its last image ending at
  x 608 (open point 45).
- Every image of both rows lies wholly inside the 1100×700 frame.
- Every image of both rows: its centre equals its file's centre pixel composited over `#14161B`, within
  2 per channel; with the owner's mark, white, the pupil's.
- Every image of both rows: its top-left pixel, which every master leaves transparent, is `#14161B`,
  the row's background (open point 47).
- The gaps: every pixel between two neighbouring images of a row, across the 12 pixels and from the
  taller image's top to the row's bottom line, is `#14161B`.
- No text cut (`LayoutAudit.FindClippedText`).
- Inspected (at §7's merge and at the gate, D247): the owner's mark in all 23 images, the same mark at
  every size: the blue tile, the white line from the tile's edges to the eye, the eye's white outline
  and the round white pupil. Nothing cut off: every tile whole, its four rounded corners inside its
  image. In the images from `icon-small.svg` (row 1's 16, 20, 24, 30 and 32; row 2's first three) the
  tile fills the image but for its rounded corners; at 16 and 20 the tile, the line, the eye's outline,
  the blue row inside it and the pupil are each told apart; 16 and 32 crisp; at 24 the line two solid
  rows with a half-covered row above and below (the conventions); 20 the softest (round 2's README). In
  those from `icon.svg` (row 1's 36 to 256; row 2's 48, 64 and 128) the tile keeps a transparent margin
  of one sixteenth of the image, so the tile at 36 (31.5 px) is narrower than at 32
  (32 px), by design. In the `.iconset`'s three the mark lies on the macOS plate, 832/1024 of the image
  with a margin of 96/1024; `icon_16x16` is soft (round 2's README) and is judged only as the same mark
  (open point 54). The tile's edge holds against `#14161B` all round, and the white line's ends show
  against it at the tile's left and right edges.
- After step 2: the window has closed.

**B2. The icon at its sizes, light** (R: steps of its own in the real window, B2, launch 3; as B1).
Method 2.

Steps:
1. Show the sheet (the method's start), light; wait as B1's step 1. Screenshot (`b2-icon-sizes-light`).
2. Close the sheet's window.

Expected:
- After step 1: as B1 after step 1, over `#FFFFFF`: the window's `ActualThemeVariant` Light; every
  image's centre equals its file's centre pixel composited over `#FFFFFF`, within 2 per channel; every
  image's top-left pixel `#FFFFFF`, the row's background (open point 47); the gaps `#FFFFFF`; the sizes
  under row 1 `Foreground` `#5C6572`, their ink `#5C6572` on `#FFFFFF`; the same images, sizes, order,
  lefts and bottoms; the heading `VisualCommit.ico`, its `Foreground` `#1B1F27`; no text cut.
- Inspected: as B1, on white. The tile's edge holds against `#FFFFFF`; where the white line reaches the
  tile's left and right edges it meets the white background, so the tile's outline is open there, at
  every size (round 2's README: on a light desk the line's white ends blend into it at 16 to 24 pixels).
  That is the mark, not a cut.
- After step 2: the window has closed.

#### Texts these checks rely on (part B)

The app shows no new text (7.3): the window's title stays `VisualCommit`, which the real window's framed
captures show right of the mark; no control gains a name (§9's sweep sees nothing new). The sheet's texts
are the harness's, not the app's: row 1's heading `VisualCommit.ico` and, under each of its frames, its
size (7.5; open point 44): the bare number, `16` to `256`, at 11 in SemiBold in `VcTextSecondaryBrush`,
centred under its image on a whole pixel; the heading at 13 in `VcTextPrimaryBrush`. Row 2 has no heading
and no labels. The files' names and sizes are 7.3's: the `.ico`'s 16, 20, 24, 30,
32, 36, 40, 48, 60, 64, 72, 80, 96 and 256; `assets/brand/generated/png/icon-<N>.png` for 16, 24, 32,
48, 64, 128, 256 and 512; the `.iconset`'s `icon_16x16`, `icon_16x16@2x`, `icon_32x32`, `icon_32x32@2x`,
`icon_128x128`, `icon_128x128@2x`, `icon_256x256`, `icon_256x256@2x`, `icon_512x512` and
`icon_512x512@2x`. The real window's: the taskbar button's UI Automation name, which starts with
`VisualCommit` (7.5); run.txt's line `B2's taskbar button: Not run (no taskbar button named VisualCommit
was found within 10 s).` (12.3.4).

#### The real window (part B)

One launch, shared with part A (12.3.4 launch 3; 7.5; 12.5: of parts A and B together, 4 of 8 checks
repeated, 2 captures compared, 3 where 1920×1080 fits, and 6 captures without a twin, all six part B's).
`Check_A_and_B_about_and_icon` in `Phase8aRealWindowPass.AB.cs`, the third method of `Phase8aOrder`, uses
`RealApp.Phase8a.cs`'s `ExecutablePath`, `WindowIcons`, `CaptureScreen` and `TaskbarButton`, `IconFile`
and, by the link, `Phase8aExpectations.B` (7.5, 12.2). Each capture is saved by `Run.Save` under its
name, compared with none and inspected. Part B presses nothing but `ThemeSwitch` (12.3.2). Its steps, in
the launch's order:

3. `Check_A_and_B_about_and_icon` (its twins part A's A1 and A4 methods: one empty tab, dark).
   `RealApp.Launch(D, session)` with a session of one empty tab written into a new data folder D before
   the start (D442), and no `settings.json` (so dark); `Sized` 1100×700 (200 % on the laptop); wait until
   the welcome page shows.
   - **B1 (a)**, the executable's icon (`ApplicationIcon`): `Icon.ExtractAssociatedIcon` of
     `ExecutablePath`, the built `VisualCommit.exe`; its size goes into run.txt. The `.ico` (`IconFile.Read`
     of `src/VisualCommit.App/Assets/Icon/VisualCommit.ico` under the `RepoRoot` metadata) has a frame of
     that size, and every pixel of the extracted icon at alpha 255 equals that frame's pixel within 2 per
     channel (open point 48).
   - **B1 (b)**, the window's icons: `WindowIcons()` sends `WM_GETICON` with `ICON_SMALL` and with
     `ICON_BIG` to the app's window, and each gives an icon. Their sizes, the window's DPI, and
     `GetSystemMetricsForDpi` of `SM_CXSMICON` and of `SM_CXICON` at that DPI go into run.txt. Each of
     the two whose size is one of the `.ico`'s equals that frame within 2 per channel, read as (a) reads
     (open point 48).
   - **B1 (c)**, the title bar, dark: the pointer at the neutral point; `CaptureWindow()`, the window
     with its frame, saved as `b1-title-bar-dark-with-frame`; its title bar's left 64 pixels by the
     caption's height (open point 49), enlarged four times nearest-neighbour, saved as
     `b1-title-bar-icon-dark`. Inspected: the owner's mark, sharp, left of the title `VisualCommit`,
     where phase 7b's framed captures show a default icon. Windows draws the window's `ICON_SMALL` icon
     in the title bar at `GetSystemMetricsForDpi(SM_CXSMICON)` for the window's DPI (run.txt, (b)), and
     stretches the handle when its size differs. Drawn at its own size means that `ICON_SMALL`'s size in
     run.txt equals that metric (32 at the laptop's 200 %). The mark at that size is the small drawing
     at 32 pixels or below (the tile filling its square but for the rounded corners) and the full one
     above. Sharp then means, at 16 or 32, that the line's and the outline's straight edges fall on
     whole pixels, which the four-times picture shows as whole blocks with no half-tone row along the
     line (at 20, 125 %, they fall on quarter pixels and at 24, 150 %, the line's on half pixels, 10.5
     and 13.5: soft by nature, not a scaled frame). The picture alone cannot tell a frame drawn at its
     own size from a lower frame doubled pixel for pixel: the straight edges of a 16 doubled to 32 also
     fall on whole pixels. So when the metric is one of the `.ico`'s sizes and `ICON_SMALL`'s size
     differs from it, Windows stretched another frame. That is design 7.9 item 2's miss (Avalonia not
     picking a frame per DPI), a finding for the lead whatever the picture looks like. At a scale whose
     metric is none of the `.ico`'s sizes (175 %: 28), a difference is no miss, and run.txt says which
     frame was handed. Whether Windows draws the title bar active or inactive is not judged (phase 6's
     report found phase 0's framed capture drawn inactive).
   - **A1 and A4**: part A's steps (design 8.5, 12.3.4 launch 3; in "The real window (part A)").
   - **B2**, the title bar, light: once A4 has ended (12.3.4: "Esc, the focus back on the button.
     Esc.", the second Esc closing the settings screen, About with it, which the method waits for as a
     named condition with a limit, 12.3.1; in "The real window (part A)"), with no dialog over the
     toolbar: a real click on `ThemeSwitch`; wait until the window shows the light theme (the condition
     is open point 50); the pointer at the neutral point. `CaptureWindow()` saved as
     `b2-title-bar-light-with-frame`; the title bar's left 64 pixels by the caption's height, four times,
     nearest-neighbour, saved as `b2-title-bar-icon-light`. Inspected: the title bar light (as phase 0's
     check 5 found it turning light with the theme), and the mark, sharp, left of `VisualCommit`, as in
     B1 (c); where the white line reaches the tile's edges it may meet a light title bar (round 2's
     README), which is the mark, not a cut.
   - **B2**, the taskbar button: `TaskbarButton("VisualCommit", 10 s)`, the button whose UI Automation
     name starts with `VisualCommit`, looked for from UI Automation's desktop root for 10 s. Found within 10 s: its existence and
     bounds asserted (it exists, its bounds not empty), and it is captured from the screen by its bounds (`CaptureScreen`) as
     `b2-taskbar-button`, and four times enlarged, nearest-neighbour, as `b2-taskbar-button-enlarged`;
     its name and bounds go into run.txt. The pointer stays at the neutral point, inside the app's window,
     so no preview or tooltip of the taskbar opens. Inspected: the owner's mark, not .NET's default icon,
     whichever frame Windows draws (the small drawing at 32 pixels and below, the full one above). None found within 10 s (a hidden taskbar, or no button): the method does not fail on it, run.txt says `B2's taskbar button: Not run (no
     taskbar button named VisualCommit was found within 10 s).`, the report marks B2's taskbar button
     "Not run" with that reason, and the gate checks the button by hand (open point 51).
   - The app closed by its window: exit code 0.

Unverified until the builder's first runs and D246's (7.9 items 2 and 5; 12.9): that Avalonia 12.1.3's
Win32 icon takes the `.ico`'s bitmaps and PNG and picks a frame per DPI (B1 (b)'s sizes against
`SM_CXSMICON`, read in (c), show it); `WM_GETICON`'s and `ExtractAssociatedIcon`'s sizes under
per-monitor DPI; the taskbar button's name and its reach from the desktop root; a capture outside the
window (the taskbar's). 12.9 no longer counts the
owner's icon as a risk: the owner chose the mark before §7 was built (1.17, 7.8), so D246's runs
already capture it.

### Part A: About and the third-party notices

Part A covers About (R6; D398, D409, D431 to D434; design 8): the settings screen's sixth and last
section, `About`, with the app's icon and name, "Created by Rahim Khajei", the version with its short
commit (pinned in the harness, ruling 59), the copyright line, "MIT License" with "Show license…", the
notices with "Show third-party notices…", the repository's link and the guide's link, and no e-mail
address; the read-only text dialog that shows the license and the notices (D432), with its switch
between `THIRD-PARTY-NOTICES.md` and the four vendor files and its "Copy all"; and the palette's keyless
"About VisualCommit…" (D417, ruling 54), which opens the section. Checks A1 to A6, design 8.5's list.
Its R steps (0.5, 8.5, 12.3.4 launch 3, 12.5): A1 to its first picture by real keys, with `AboutVersion`'s
automation name read against the built app's version and, where 1920×1080 fits the screen, a second
capture of the section at that size, whose twin is A3's picture; and A4's license dialog by a real Tab
and Enter. Two of the six checks are repeated, A1 and A4 (12.5: "A1, A4; B1, B2 (4 of 8)"). One launch,
shared with part B: two captures compared (three where 1920×1080 fits), no link and no "Copy all"
pressed.

#### Conventions of part A

- **Code** (8.2, 8.4, 8.5, 1.15): `Phase8aChecks.A.cs` (`Phase8aChecksA : Phase8aChecks`, with a
  `.A.Helpers.cs` if needed); `Phase8aExpectations.A.cs` (`Phase8aExpectations.A`: every text below as a
  literal, word for word, the six nav slots and About's place at both sizes, the repository's address,
  the ids, the picture names, and for §12 launch 3's facts for part A: the session (one empty tab, dark,
  1100×700), the palette's query `about` and the row's label, the three capture names in the launch's
  order, `a1a-about-dark`, `a3a-about-1920`, `a4a-license`); `Phase8aExpectationTests.A.cs` (every
  picture name matches 0.3's pattern and is used once; no text holds `@`; the literals equal
  `OperationTexts.AboutTexts`' constants and `AppLinks.Repository`). `Phase8aExpectations.A` uses
  nothing of the 8a project (the real-window project links it). The tests that prove the parts no
  picture shows are not checks: `AboutSectionTests` and `AboutEntryTests` in 8a's project, and App.Tests'
  `ThirdPartyNoticesTests`, `AboutSettingsViewModelTests`, `TextDocumentDialogViewModelTests` and
  `DocumentTextViewTests` (8.4); the report names them under "What proves each Delivers item". §12's
  code is `Phase8aRealWindowPass.AB.cs`, shared with part B.
- **Starts and sessions.** One test method per app start, each on a fresh data folder: "one empty tab"
  is a start with no session written, where the app opens its one empty tab (`New tab`, the welcome
  page), through 8a's `StartEmptyAsync`; A6's tab is a fresh copy of "graph" (`Scenarios.GraphAsync()`,
  phase 1's), opened through 8a's `OpenAsync`. Dark and 1100×700 unless a check names light (A2, through
  the support's `UseTheme`, which writes the settings with `theme` `Light` (`JsonSettingsStore.Update`,
  the other settings at their defaults) before the start: `StartEmptyAsync(theme: AppTheme.Light)`) or
  1920×1080 (A3). `settings.json` is not written by any start but A2's, and nothing is launched unless a
  step names it (8.5). Every start gets `TestIdentity.Pinned` (1.15): `VisualCommit`, `Rahim Khajei`, version `0.1.0`
  with the commit `1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d`, `Copyright (c) 2026 Rahim Khajei`, so the
  section shows `0.1.0 (1a2b3c4)` (`Phase8aExpectations.PinnedVersion`, in `Phase8aExpectations.Common.cs`)
  on every commit (ruling 59, D431).
- **What a fake stands for.** `FakeFileLauncher` (`app.Launcher`) stands for the browser: an address the
  app opens is recorded in `Requests` as an `OpenUrl` (`FileLaunch.FullPath` the address's
  `AbsoluteUri`) and nothing opens. `FakeClipboard` (`app.Clipboard`) stands for the system clipboard:
  `Text` is what was copied last, `Texts` everything, in order. No test opens a browser or touches the
  machine's clipboard. The texts About shows are the app's embedded resources (1.13); a check compares
  them with the repository's files, `LICENSE`, `THIRD-PARTY-NOTICES.md` and `notices/Avalonia-NOTICE.md`,
  read through `IconFile.RepositoryPath("LICENSE")`, `IconFile.RepositoryPath("THIRD-PARTY-NOTICES.md")`
  and `IconFile.RepositoryPath("notices/Avalonia-NOTICE.md")` with `\r\n` made `\n`, as
  `ThirdPartyNoticesTests` compares the resources with the files. `IconFile.RepositoryPath` is the public
  path under Testing's `RepoRoot` metadata, through which part B reads `assets/brand/` (design 1.15);
  `VisualRun.RepoRoot` is internal to Testing and visible only to App.Tests and Git.Tests.
- **Where things are read** (1.13's ids). The section list `SettingsNav` and its buttons
  `SettingsNavGeneral`, `SettingsNavGit`, `SettingsNavTools`, `SettingsNavShortcuts`, `SettingsNavAccounts`
  and `SettingsNavAbout`; the section `SettingsAbout` and `AboutIcon`, `AboutName`, `AboutCreator`,
  `AboutVersion`, `AboutCopyright`, `AboutLicense`, `AboutShowLicenseButton`, `AboutNoticesNote`,
  `AboutShowNoticesButton`, `AboutRepositoryLink`, `AboutGuideLink`; the rows' labels, which have no id,
  by their text in the form's first column; the text dialog's `DialogTitle`, `TextDocumentSwitch`,
  `TextDocumentPartMain` and `TextDocumentPartAvalonia`, `TextDocumentPartCommunityToolkitMvvm`,
  `TextDocumentPartOnigwrap`, `TextDocumentPartSkiaSharp` (8.3.2: `TextDocumentPart` and the vendor
  file's name before its first `-`, without dots), `TextDocumentText`, `TextDocumentCopyButton` and
  `ConfirmButton`, each found inside the text dialog's own view (the settings screen beneath it has a
  `ConfirmButton` of its own, `Save`); the palette's `CommandPaletteBox`, `CommandPaletteList` and its rows
  (`CommandPaletteItem`, with `CommandPaletteItemLabel`, `CommandPaletteItemDetail`,
  `CommandPaletteItemKey`). The keyboard focus is the focused element in the tree (`FocusedElement`,
  `FocusedAutomationId()`, `FocusIsWithin(…)`, as part K reads it). For `TextDocumentText`, an
  AvaloniaEdit editor (8.3.2: `DocumentTextView : CodeTextEditor`), the focus is on the `TextArea` inside
  it, which has no automation id of its own, so "the focus is in `TextDocumentText`" means that
  `FocusedAutomationId()` (the nearest ancestor with an id) is `TextDocumentText` and the editor's
  `TextArea.IsFocused` is true, as part K reads `DiffText` and `BlameText`. "Framed" means the focused
  element has `:focus-visible`, as part K reads it. A link is "underlined" when the `TextBlock` it shows has
  `TextDecorations` Underline (8.3.1). The repository link that A1's step 9 frames is also read in its
  picture, against `a1a`, where it is not focused (`AssertFramedLink`, by `LinkFrame` in
  `VisualCommit.Testing`): the text's pixels unchanged but for the underline's rows, the underline shown
  across the text, a whole frame in the theme's frame colour (Fluent's
  `SystemControlFocusVisualPrimaryBrush`, part K's conventions) on four sides outside the text, and the
  background between the frame and the text's ink. `TextDocumentText` is read as a `CodeTextEditor`: its
  document's text, its options (`IsReadOnly`, `WordWrap`, `Options.AcceptsTab`), its scroll offset
  (`CodeTextEditor.ScrollOffset`) against its extent and viewport, and which document lines its text
  view shows whole. "Shown" is `IsEffectivelyVisible` with a laid-out size. Places are window
  coordinates at scale 1, within 2, as phase 7a's and 7b's part A assert the section list's places
  (`Phase7aExpectations.A.Tolerance`).
- **The screen's form** (8.3.1). About is laid out as the Accounts section is: rows 18 apart (the stack's
  `Spacing`), each row a `Grid` `132,12,*` with its label (`settingsLabel`) in the first column and its
  value in the third, a note in `settingsNote`, a button (`VcActionButton`) 10 below its row's text; the
  head row a `Grid` `48,16,*`. At 1100×700 the card is at (120, 40), 860×620, the section's text starts at
  x 336 and a row's value at x 480 (phase 7a's `Card`, `SectionX`, `ControlsX`); at 1920×1080 the card is
  at (530, 200), 860×680, the section from x 746, a value from x 890 (`CardFull`, `SectionXFull`,
  `ControlsXFull`). Every text whole (`AssertNoClippedText`), and no text, tooltip or automation name of
  `SettingsAbout` holds `@` (D409, 8.3.5). The notices are shown as their files have them (D431, D432):
  they name third parties, and the vendors' files may hold addresses; the `@` rule is About's own texts',
  not the notices'.
- **Colours** (tokens only, 1.5; each a value the contrast round keeps, `Theme/Tokens.axaml`), each asserted as the brush read in the tree resolving to the token's colour in the active theme,
  and, for a link's text, as its ink (`TextInk.Problems`) on the background named:

  | Token | Dark | Light | Where part A reads it |
  |---|---|---|---|
  | `VcAccentBrush` | `#7C8CFF` | `#4353D8` | `AboutRepositoryLink`'s and `AboutGuideLink`'s text, at rest, on the card (A1, A2) |
  | `VcPanelBackgroundBrush` | `#1A1D24` | `#F5F6F8` | the settings card and the text dialog's card, at a point clear of text |
  | `VcTextSecondaryBrush` | `#9BA3B0` | `#5C6572` | `AboutCreator` (`secondary`), the rows' labels (`settingsLabel`), `AboutNoticesNote` (`settingsNote`) |
  | `VcTextPrimaryBrush` | `#E4E7EC` | `#1B1F27` | `TextDocumentText`'s text; the palette row's label (A6) |
  | `VcControlBackgroundBrush` | `#272C36` | `#FFFFFF` | `TextDocumentText`'s background; a `selected` `VcFlatButton` (the shown section's button, the shown part's switch button) |
  | `VcBorderBrush` | `#323845` | `#D2D6DE` | `TextDocumentText`'s 1 px border |
  | `VcSelectionBrush` | `#2A3150` | `#DDE1FA` | the palette's highlighted row (A6) |
  | `VcBackdropBrush` | `#99000000` | `#66000000` | the text dialog's own backdrop over the settings screen (A4), asserted as phase 6's L31 asserts a backdrop, at a point of the settings card clear of the text dialog's card |

- **The icon** (1.6, 1.17, 8.3.1). `AboutIcon` is an `Image` 48 × 48 whose source is
  `AppIcon.Image96Uri` (`VisualCommit-96.png`, a 96 × 96 bitmap, `icon.svg` at 96 with the owner's mark,
  Lens, D407: the tile `#4C5BE0` inside a transparent margin of one sixteenth, 3 px at 48 × 48, the line,
  the eye's outline and the pupil white), drawn with `BitmapInterpolationMode.HighQuality`, decorative:
  no automation name. As part B reads an image (open point 62): its centre pixel, inside the pupil, is
  white within 2 per channel, and its top-left pixel, in the transparent margin, is the card's
  background within 2. The owner's mark shows from part A's first run (§7 wrote no placeholder, 1.17),
  as design 8.9 item 8 now says (open point 69).
- **Routes** are those earlier checks use: the palette by `Ctrl+Shift+P` with the pointer at the
  neutral point (phase 6's L31, part L's L3); the settings screen by `Ctrl+,` (`OpenSettingsAsync
  (SettingsOpening.Key)`) or by a click on the toolbar's `SettingsButton` (`SettingsOpening.Button`),
  each opening on General with the focus on `SettingsNavGeneral`, framed (the screen gives its first
  focus to the shown section's button, phase 6's 7.1), and waiting for the screen's reads
  (`WaitForSettingsReadsAsync`); a section by a click on its button, or by Up and Down, which move the
  focus only, and Enter, which shows the focused button's section (phase 6's 7.1; 8.3.1: Down from
  Accounts reaches About). The palette's "About VisualCommit…" opens the screen on About with the focus
  on `SettingsNavAbout`, framed (8.3.3, 8.4's `AboutEntryTests`). Esc closes the screen unsaved; a dialog
  over it closes alone on Esc (D104).
- **Pictures.** Dark and 1100×700 unless a check names light (A2) or 1920×1080 (A3). The pointer rests
  at the neutral point before every picture (no part A picture keeps it): at 1100×700 it lies below the
  settings card and below the text dialog's card, on the backdrop, and outside the palette's card; at
  1920×1080 it lies inside the settings card, below About's last row, on no control (open point 71).
  Each picture passes `AssertNoClippedText`, and in each no tooltip is open. `a6a` shows the palette box's
  caret, which varies from run to run by nature (as `k10a`, `k10f` and `l3d` do); no other part A picture
  shows a caret (`TextDocumentText` draws none, 8.3.2).
- **Nothing written, nothing launched** (8.3.1: About saves nothing). Each method asserts at its end that
  `settings.json` is as the start left it (absent, or A2's bytes unchanged), and that the launcher holds
  exactly the requests its steps name (A1's one address; none elsewhere). A6, on a repository, takes its
  `ExactStateAsync()` before its first step and asserts it unchanged at its end.
- **Shown by no check** (8.3, 8.4). The macOS application menu's one item `About VisualCommit`, its
  `Command` and the absence of `About Avalonia` (`AboutEntryTests`, reading
  `NativeMenu.GetMenu(Application.Current)`; on a Mac, 8b's Mac checklist, 0.5, 8.9 item 2). The palette's
  entry on a dashboard tab and its place right after `Follow the system's theme` (`AboutEntryTests`,
  `PaletteEntriesTests`, phase 6's K10). The links disabled without a launcher, a launcher that throws
  (`Opening the repository's page failed.`, `Opening the user guide failed.`), a text that cannot be read
  (`The text {name} could not be read.`) and a failed copy (`Copying the text to the clipboard failed.`)
  (`AboutSettingsViewModelTests`, `TextDocumentDialogViewModelTests`). The texts read off the caller's
  thread, a second press while they are read opening nothing more, and a screen closed during the read
  showing no dialog (`AboutSettingsViewModelTests`'
  `The_texts_are_read_off_the_callers_thread_and_a_second_press_meanwhile_opens_nothing_more` and
  `A_screen_closed_while_the_texts_are_read_shows_no_dialog`). `DocumentTextView`'s keys but End, in
  the headless `DocumentTextViewTests` alone (no picture and no real-window step shows them): Down,
  PageDown and Home, and Home and End with the platform's command key
  (`Down_scrolls_a_line_and_PageDown_the_view_less_a_line`,
  `End_shows_the_last_line_and_Home_the_first`); Tab leaving the view (`Tab_leaves_the_text`); a move
  up after End letting the end go, though the lines it brings in wrap
  (`A_wheel_up_after_End_leaves_the_end_though_the_lines_it_brings_in_wrap`); Enter,
  with or without modifiers, leaving the view where it is, a plain Enter going on unhandled
  (`Enter_with_or_without_modifiers_leaves_the_view_where_it_is_and_plain_Enter_goes_on_unhandled`);
  typed text and every key that would move, extend or use the hidden caret taken and doing nothing
  (`Keys_that_would_move_extend_or_use_the_hidden_caret_or_type_do_nothing`); cut, paste, undo and redo
  changing nothing (`Cut_paste_undo_and_redo_change_nothing_and_leave_the_view_where_it_is`); the
  platform's select-all and copy keys
  (`The_select_all_key_selects_the_whole_text_and_leaves_the_view_where_it_is`,
  `The_copy_key_copies_a_selection_and_nothing_without_one`); another editor still taking its own keys
  (`Another_editor_still_takes_its_keys_line_breaks_typing_deleting_and_moving_its_caret`); and its
  Document peer's value (`The_peer_is_a_document_whose_value_is_the_text_and_whose_name_is_the_title`).
  A held key is shown only as three headless key downs, and no test shows a Mac's Cmd keys: the
  headless platform's command key is Ctrl on every system, and the reading for a real Mac rests on
  Avalonia 12.1.3's and AvaloniaEdit 12.0.0's code. Left as they are by the lead (2026-11-21): PageUp
  and PageDown with Ctrl, Alt or Meta and no Shift, which Avalonia's scroll viewer takes and pages by a
  whole viewport, and Home and End clearing a selection made with the mouse. A plain Enter in the
  license's text closing the dialog as Close does, the focus back on `Show license…` with its frame,
  and Enter with a modifier doing nothing (`AboutSectionTests`'
  `Enter_in_the_licenses_text_closes_the_dialog_as_Close_does_and_gives_the_focus_back`);
  a held Enter on `Show license…` then opens and closes the dialog in turn, as in any dialog with a
  default button that a button opens by Enter (the lead, 2026-11-21), which no test shows. The
  notices' e-mail addresses, only those their copyright lines carry, each inside a fenced block
  (`ThirdPartyNoticesTests.The_notices_files_only_addresses_are_its_copyright_lines`; open point 70).
  The guide link pressed (part G's G3, 11.5). The sweep's names over About (part U, §9). A link's
  underline on hover (open point 63).
- **The lagged list** (8.7, 0.4): part A runs in 8a's project, which is on the list, at wave 3's close
  and at the merges of §9 and §10 under `VISUALCOMMIT_WATCHER_LAG_MS=1500`; the list gains nothing.
- **CI** (8.8): part A runs in CI's six jobs; the macOS and Linux jobs' pictures are read under the
  report's "Other platforms". A5's bound of 1 s stays 8.5's and is asserted wherever the check runs,
  CI's six jobs among them, and the check writes the time it measured to its output; if the whole
  default run or one of CI's jobs fails it, the lead rules then, with the measured times, and no bound
  is loosened ahead of that (open point 60).
- **Time** (8.7): each method under about 40 s alone (`bash scripts/time-scripted-check.sh "<method>"
  8a`); part A about 3 minutes alone; in the real window about 30 s of launch 3.

#### Test methods and app starts (part A)

Five methods, five starts. Method 1 is the twin of the real window's A1 and A4 in launch 3 and takes
their steps in the same order: A4 runs after A1's first Tab, as the launch's A4 follows A1's picture
(12.3.4), and A1's walk goes on after it (open point 57). Method 3 (A3) is the twin of A1's second
capture in launch 3, at 1920×1080, where it fits.

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_A1_A4_about_dark_and_license` | A1, A4 | one empty tab | 1100×700, dark | `a1a`, `a4a`, `a1b` |
| 2 | `Check_A2_about_light` | A2 | one empty tab; the theme Light written before the start (`StartEmptyAsync(theme: AppTheme.Light)`) | 1100×700, light | `a2a` |
| 3 | `Check_A3_about_1920` | A3 | one empty tab | 1920×1080, dark | `a3a` |
| 4 | `Check_A5_notices` | A5 | one empty tab | 1100×700, dark | `a5a`, `a5b`, `a5c` |
| 5 | `Check_A6_palette_entry_on_a_repository` | A6 | one tab on a fresh "graph" (`Scenarios.GraphAsync()`) | 1100×700, dark | `a6a` |

#### Steps and expected results (part A)

A step numbered in a method runs on from that method's last step in an earlier check.

**A1. About, dark, from the palette** (R: steps 1 to 3 by real keys, launch 3, with a second capture at
1920×1080 where it fits, whose twin is A3's `a3a-about-1920`; D398, D409, D417, D431; design 1.6, 1.8,
1.13, 8.3.1, 8.3.3, 8.5, 12.3.2, 12.3.4). Method 1.

Steps, method 1:
1. Start; wait until ready, the welcome page shown. Rest the pointer at the neutral point.
2. Press `Ctrl+Shift+P`; wait until the palette is open and `CommandPaletteBox` has the focus. Type
   `about`; wait until the list holds one row.
3. Press Enter; wait until the palette has closed, the settings screen shows (`SettingsDialog`) and its
   reads have ended. Screenshot (`a1a-about-dark`).

Steps, method 1, after A4's step 7:

8. Press Tab; wait until `AboutShowNoticesButton` has the focus.
9. Press Tab; wait until `AboutRepositoryLink` has the focus. Screenshot (`a1b-about-link-focus`).
10. Press Enter; wait until the launcher holds one request.
11. Press Tab; wait until `AboutGuideLink` has the focus.
12. Press Esc; wait until the settings screen has closed.

Expected:
- After step 2: `CommandPaletteBox` holds `about`; the list holds exactly one row, `CommandPaletteItem`
  named `About VisualCommit…`, highlighted (selected), enabled; its `CommandPaletteItemDetail` empty (no
  detail), its `CommandPaletteItemKey` not shown and its `AcceleratorKey` empty (no key); its label not
  `danger`; `CommandPaletteEmpty` not shown (8.3.3: listed on every kind of tab, enabled, no key, no
  detail). The launcher has no request.
- After step 3: the palette closed. The settings screen on the dialog layer over the welcome page, its
  card at (120, 40), 860×620, `SettingsTitle` `Settings`. `SettingsNav` holds six buttons, in order
  `General`, `Git`, `Tools`, `Keyboard shortcuts`, `Accounts`, `About` (`SettingsNavGeneral`,
  `SettingsNavGit`, `SettingsNavTools`, `SettingsNavShortcuts`, `SettingsNavAccounts`, `SettingsNavAbout`),
  each named by its text, each 176 × 30 and 2 below the one above it; `SettingsNavAbout` at (141, 255)
  (8.5; within 2, open point 56), exactly 32 below `SettingsNavAccounts` with the same left, with the
  class `selected` (its background `#272C36` at a point clear of its text) and no other button selected,
  and with the keyboard focus, framed. The section `SettingsAbout` shows, no other section; top to
  bottom, rows 18 apart:
  - the head: `AboutIcon` (the conventions' "The icon") at the section's left; 16 to its right
    `AboutName` `VisualCommit` at font size 20, `SemiBold`, over `AboutCreator` `Created by Rahim Khajei`
    (class `secondary`, `#9BA3B0`);
  - `Version`: `AboutVersion` `0.1.0 (1a2b3c4)`, its automation name the same text;
  - `Copyright`: `AboutCopyright` `Copyright (c) 2026 Rahim Khajei`;
  - `License`: `AboutLicense` `MIT License`; 10 below it `AboutShowLicenseButton` `Show license…`
    (`VcActionButton`), enabled;
  - `Notices`: `AboutNoticesNote` `VisualCommit is built on open-source components, each under its own
    license.` (`settingsNote`, `#9BA3B0`); 10 below it `AboutShowNoticesButton` `Show third-party notices…`
    (`VcActionButton`), enabled;
  - `Source code`: `AboutRepositoryLink` `github.com/RahimPasha/VisualCommit`, its tooltip `Opens the
    repository's page in your browser.`;
  - `User guide`: `AboutGuideLink` `Open the user guide`, its tooltip `Opens the user guide in your
    browser.`.

  Each label (`settingsLabel`, `#9BA3B0`) at x 336 and each value at x 480. The two links enabled, not
  underlined, their text `#7C8CFF` (`VcAccentBrush`), ink `#7C8CFF` on the card's `#1A1D24`. Each button
  and link named by its text (8.3.1). `SettingsSection` (the section's scroll viewer) shows no scroll bar:
  its extent is no taller than its viewport (8.3.1: no scrolling at 1100×700). `CancelButton` `Cancel`
  and `ConfirmButton` `Save` at the card's bottom. Nothing clipped; no `@` in the section. The launcher has
  no request.
- After step 8: `AboutShowNoticesButton` focused, framed; no dialog; the launcher has no request.
- After step 9 (`a1b`): `AboutRepositoryLink` focused, framed, its text underlined (8.3.1: underlined
  while focused; the pointer at the neutral point, so not by hover); `AboutGuideLink` not underlined; no
  tooltip open; the launcher has no request. The check reads the frame and the underline in `a1b` too,
  against `a1a` (the conventions' "Where things are read").
- After step 10: the launcher's `Requests` holds exactly one request, an `OpenUrl` of
  `https://github.com/RahimPasha/VisualCommit` (`AppLinks.Repository`, 1.7), and nothing else; the
  screen still open on About; `AboutRepositoryLink` still focused; the log has no `Opening the
  repository's page failed.`.
- After step 11: `AboutGuideLink` focused, framed, underlined; `AboutRepositoryLink` no longer
  underlined; the launcher still holds the one request (Tab opened nothing).
- After step 12: no dialog; the launcher still holds the one request; no `settings.json` in the data
  folder (8.3.1: About saves nothing).

**A2. About, light** (not R; D409, D431; design 1.5, 8.3.1, 8.5). Method 2.

Steps:
1. Start, light; wait until ready. Read `settings.json`'s bytes.
2. Click the toolbar's `SettingsButton`; wait until the screen shows and its reads have ended.
3. Click `SettingsNavAbout`; wait until `SettingsAbout` shows. Rest the pointer at the neutral point.
   Screenshot (`a2a-about-light`).
4. Press Esc; wait until the screen has closed.

Expected:
- After step 2: the screen on General, `SettingsNavGeneral` selected; the window's
  `ActualThemeVariant` Light.
- After step 3: `SettingsNavAbout` selected (its background `#FFFFFF`, `VcControlBackgroundBrush` light),
  no other button selected; About as in A1 after step 3, A1's texts word for word, places and order, in
  light: `AboutCreator`, the labels and `AboutNoticesNote` `#5C6572`; the links' text `#4353D8`
  (`VcAccentBrush` light, 8.5), not underlined, ink `#4353D8` on the card's `#F5F6F8`; the icon's centre
  white and its top-left pixel `#F5F6F8`, each within 2. No scroll bar; nothing clipped; no `@`.
- After step 4: no dialog; `settings.json` holds the bytes step 1 read; the launcher has no request.

**A3. About at 1920×1080** (not R; its picture is the scripted twin of A1's real-window capture at
1920×1080 where it fits, launch 3; D409; design 8.3.1, 8.5, 12.3.4). Method 3.

Steps:
1. Start at 1920×1080; wait until ready.
2. Press `Ctrl+,`; wait until the screen shows, its reads have ended and `SettingsNavGeneral` has the
   focus.
3. Press Down five times, each waited on by the focus moving to the next button: `SettingsNavGit`,
   `SettingsNavTools`, `SettingsNavShortcuts`, `SettingsNavAccounts`, `SettingsNavAbout`.
4. Press Enter; wait until `SettingsAbout` shows. Screenshot (`a3a-about-1920`).
5. Press Esc; wait until the screen has closed.

Expected:
- After step 2: the card at (530, 200), 860×680; the screen on General.
- After step 3: the focus on `SettingsNavAbout`, framed; the shown section still General (Down moves the
  focus only).
- After step 4: About shows; `SettingsNavAbout` at (551, 415) (8.5; within 2, open point 56), exactly 32
  below `SettingsNavAccounts` with the same left, `selected`, still focused and framed; the six buttons
  as in A1; the section's rows, texts, colours and order as A1's after step 3, the labels at x 746 and
  the values at x 890; no scroll bar; nothing clipped; no `@`.
- After step 5: no dialog; no `settings.json`; the launcher has no request.

**A4. The license** (R: Tab, Enter and Esc, launch 3; "Copy all" scripted only; D432, ruling 58; design
8.3.2, 8.5, 12.3.2, 12.3.4). Method 1, after A1's step 3.

Steps:

4. Press Tab; wait until `AboutShowLicenseButton` has the focus.
5. Press Enter; wait until the text dialog shows and the focus is in `TextDocumentText`. Screenshot
   (`a4a-license`).
6. Click `TextDocumentCopyButton`; wait until the clipboard holds a text. Rest the pointer at the neutral
   point.
7. Press Esc; wait until the text dialog has closed and `AboutShowLicenseButton` has the focus.

Expected:
- After step 4: `AboutShowLicenseButton` focused, framed (the first Tab from `SettingsNavAbout`, 8.3.1);
  no dialog; the launcher has no request.
- After step 5: the text dialog stacked over the settings screen, which stays open on About under the
  dialog's own backdrop (`#99000000`; D104, D432). Its card (`dialogCard document`) 760 × 600 at (170, 50)
  (open point 58), on `#1A1D24`. Top to bottom: `DialogTitle` `MIT License` (`dialogTitle`: font size 16,
  `SemiBold`); no `TextDocumentSwitch` (one part); `TextDocumentText`; then, on one line 20 below the
  text, `TextDocumentCopyButton` `Copy all` at the card's content's left and `ConfirmButton` `Close`
  (`VcPrimaryButton`) at its right. `TextDocumentText`: read-only, word-wrapped, `Options.AcceptsTab`
  false (ruling 52), `VcCodeFontFamily` at 11.5; its text `#E4E7EC` on `#272C36` inside a 1 px border
  `#323845`; its document's text equals `LICENSE` (`\n` line ends): the file's 21 lines, which the
  document holds as lines 1 to 21, with the empty line 22 that AvaloniaEdit counts after the final line
  end (`Document.LineCount` 22, as open point 61 says of the notices); all in view: the view shows line 1,
  `MIT License`, line 21 and the empty line 22 whole, and its extent is no taller than its viewport (8.5);
  no caret drawn (8.3.2; open point 66); the keyboard focus is in it (its text area). Its automation peer
  is a Document named `MIT License`. Nothing clipped. The launcher has no request; the clipboard is empty.
- After step 6: the clipboard's `Text` equals `LICENSE` with `\n` line ends, whole (8.3.2: the shown text
  whole), and `Texts` holds that one text; the dialog still open; the log has no `Copying the text to the
  clipboard failed.`.
- After step 7: the text dialog closed (Esc closes it alone, D104); the settings screen still open on
  About; `AboutShowLicenseButton` has the keyboard focus (8.3.2: Esc gives it back to the opening button),
  framed (open point 66); the clipboard unchanged; the launcher has no request.

**A5. The third-party notices** (not R; D431, D432; design 8.3.2, 8.3.4, 8.5, 8.9 item 3, 13.4 item 12).
Method 4.

Steps:
1. Start; wait until ready.
2. Press `Ctrl+,`; wait until the screen shows and its reads have ended. Click `SettingsNavAbout`; wait
   until `SettingsAbout` shows.
3. Click `AboutShowNoticesButton`, starting a stopwatch at the click; wait until the text dialog shows
   with its title, its five switch buttons and `TextDocumentText` laid out with its first line in view,
   and stop the stopwatch. Screenshot (`a5a-notices-top`).
4. Press End; wait until `TextDocumentText`'s vertical scroll offset equals its extent less its
   viewport, within 1 (a timeout names the offset, the extent and the viewport). Screenshot
   (`a5b-notices-end`).
5. Click `TextDocumentPartAvalonia`; wait until `TextDocumentText` holds Avalonia's file and is laid
   out. Screenshot (`a5c-notices-avalonia`).
6. Click `ConfirmButton` (`Close`) in the text dialog; wait until it has closed.
7. Press Esc; wait until the screen has closed.

Expected:
- After step 3: the stopwatch reads at most 1 s (8.5's bound, asserted wherever the check runs; open
  point 60); the check writes the time it measured to its output, and the time goes into the report's
  Notes. `DialogTitle` `Third-party notices`. 12 below the title, `TextDocumentSwitch` holds five
  `VcFlatButton`s 28 high, 4 apart, in one row (their tops equal), in order: `TextDocumentPartMain`
  `Components and licenses`, `TextDocumentPartAvalonia` `Avalonia`,
  `TextDocumentPartCommunityToolkitMvvm` `.NET Community Toolkit`, `TextDocumentPartOnigwrap` `Onigwrap`,
  `TextDocumentPartSkiaSharp` `SkiaSharp and HarfBuzzSharp` (8.3.2, 8.3.5: `VendorNotices()` in name order);
  the first `selected` (its background `#272C36`), the other four not. 8 below the switch,
  `TextDocumentText`, as in A4 but its document's text equals `THIRD-PARTY-NOTICES.md` (`\n` line ends),
  scrolled to its top (scroll offset 0), the view's first line the file's first line; the keyboard focus
  is in it (its text area; 8.3.2: it takes the focus at opening). `Copy all` and `Close` as in A4.
  Nothing clipped. The launcher has no request.
- After step 4: `TextDocumentText` scrolled to its end: its vertical scroll offset is its extent less its
  viewport, within 1; the document's last line (the empty line after the file's final line end, open
  point 61) is the view's last visual line, shown whole, and the file's last line that is not empty shows
  whole in the view, with below it only the empty lines that follow it to the document's end (8.3.2: End
  shows the true last line; open points 60 and 61); the file's first line is out of view; the switch as
  after step 3.
- After step 5: `TextDocumentPartAvalonia` `selected`, `TextDocumentPartMain` not; the title unchanged;
  `TextDocumentText`'s document's text equals `notices/Avalonia-NOTICE.md` word for word (`\n` line ends),
  shown from its top: scroll offset 0, the view's first line the file's first line (8.3.2: a switch shows
  the new text from its top).
- After step 6: the text dialog closed; the settings screen still open on About; `AboutShowNoticesButton`
  has the keyboard focus (8.3.2, 8.5; whether framed is not asserted); the clipboard is empty; the
  launcher has no request.
- After step 7: no dialog; no `settings.json`.

Inspected (`a5a` to `a5c`): the switch on one line; the main file's first lines, then its last lines,
then Avalonia's file from its top, each in the code font, wrapped within the view, with no caret.

**A6. "About VisualCommit…" on a repository tab** (not R; D417, ruling 54; design 1.8, 8.3.3, 8.5).
Method 5.

Steps:
1. Open the tab on the fresh "graph"; wait until ready. Take `ExactStateAsync()`. Rest the pointer at the
   neutral point.
2. Press `Ctrl+Shift+P`; wait until the palette is open and `CommandPaletteBox` has the focus. Type
   `about`; wait until the list holds one row. Screenshot (`a6a-palette-about`).
3. Press Enter; wait until the palette has closed, the settings screen shows and its reads have ended.
4. Press Esc; wait until the screen has closed. Take `ExactStateAsync()` again.

Expected:
- After step 2: as A1 after step 2, on a repository tab: one row, `About VisualCommit…`, highlighted, its
  background `#2A3150` (`VcSelectionBrush`) at a point of the row clear of text, its label `#E4E7EC`
  (`VcTextPrimaryBrush`, not `danger`), enabled, no detail, no key; the card 84 high (phase 6's 5.3: 2 +
  16 + 32 + 6 + 28 for one row). The launcher has no request.
- After step 3: the screen on About, `SettingsNavAbout` selected, focused and framed, the section's texts
  as A1's (8.4's `AboutEntryTests`: the command opens About with the focus on `SettingsNavAbout`).
- After step 4: no dialog; `ExactStateAsync()` unchanged; no `settings.json`; the launcher has no
  request.

#### Texts these checks rely on (part A)

About's texts (8.3.5, `OperationTexts.AboutTexts`), word for word: `Section` `About`; `CreatedBy` `Created
by {creator}` (`Created by Rahim Khajei`); the labels `Version`, `Copyright`, `License`, `Notices`, `Source
code`, `User guide`; `MitLicense` `MIT License`; `ShowLicense` `Show license…`; `NoticesNote` `VisualCommit
is built on open-source components, each under its own license.`; `ShowNotices` `Show third-party
notices…`; `RepositoryTip` `Opens the repository's page in your browser.`; `OpenUserGuide` `Open the user
guide`; `GuideTip` `Opens the user guide in your browser.`; `LicenseTitle` `MIT License`; `NoticesTitle`
`Third-party notices`; `MainNotices` `Components and licenses`; `NoticeFileLabel` `Avalonia`, `.NET
Community Toolkit`, `Onigwrap`, `SkiaSharp and HarfBuzzSharp` (else the file's name, which no check
shows); `CopyAll` `Copy all`; `Close` `Close`; `PaletteCommand` `About VisualCommit…`; `MenuItem` `About
VisualCommit` (no check shows it). `…` is U+2026. No text holds an e-mail address (8.3.5).

The identity (1.6, 1.15, `TestIdentity.Pinned`): `VisualCommit`, `0.1.0 (1a2b3c4)`, `Copyright (c) 2026
Rahim Khajei`. The repository's link shows the address's host and path, `github.com/RahimPasha/VisualCommit`
(8.3.1). The addresses (1.7): `https://github.com/RahimPasha/VisualCommit` (`AppLinks.Repository`), which
A1 opens; `https://rahimpasha.github.io/VisualCommit/` (`AppLinks.Guide(GuidePage.Start)`), which no part A
step opens (part G's G3). The files (8.2, 1.13): `LICENSE`, `THIRD-PARTY-NOTICES.md`,
`notices/Avalonia-NOTICE.md`, `notices/CommunityToolkit.Mvvm-ThirdPartyNotices.txt`,
`notices/Onigwrap-THIRD-PARTY-NOTICES.txt`, `notices/SkiaSharp-THIRD-PARTY-NOTICES.txt`.

The log lines (8.3.1, 8.3.2), which no check shows; A1 and A4 assert the absence of the one their step
could cause: the Error `The text {name} could not be read.`; the Warnings `Opening the repository's page
failed.`, `Opening the user guide failed.`, `Copying the text to the clipboard failed.`.

Today's texts the steps pass through: `New tab`; the settings screen's `Settings`, `General`, `Git`,
`Tools`, `Keyboard shortcuts`, `Accounts`, `Cancel`, `Save` (`OperationTexts.SettingsTexts`,
`HostingTexts.AccountsSection`).

#### The real window (part A)

Part A's steps are in launch 3, `Check_A_and_B_about_and_icon` (12.3.4; `Phase8aRealWindowPass.AB.cs`),
after part B's B1 (a) to (c) and before its B2: a data folder D of its own whose session holds one empty
tab, written before the start, and no `settings.json`, as its twins' (methods 1 and 3; D442); sized
1100×700 (200 % on the laptop); dark. Two captures, `a1a-about-dark` and `a4a-license`, each compared
with its scripted twin by name, at most 3 % (D33, D58), and, where 1920×1080 fits, A1's second capture
`a3a-about-1920`, compared with A3's picture; the scripted twins must exist on the machine. No step
presses, clicks or sends Enter to `AboutRepositoryLink`, `AboutGuideLink` or `TextDocumentCopyButton`,
nor sends F1 (12.3.2). Every wait is on a named condition
within the pass's `StepTimeout` (12.3.1). In the launch's order:

3. `Check_A_and_B_about_and_icon` (part A's twins: method 1 for A1 and A4; method 3, A3's, for A1's
   capture at 1920×1080).
   - **Before the first key.** B1 presses nothing, so A1's `Ctrl+Shift+P` is the launch's first input,
     a key: the method first asserts `ForegroundIsThis`, as launches 5 and 6 do (12.3.3 reads "launches
     3, 5 and 6"). If it does not hold, one real click on the window's title bar (after B1's captures),
     away from its left end (where the icon opens the system menu) and from the caption buttons, then
     `ForegroundIsThis` asserted again within 5 s, else the method fails before any key (12.3.5's second
     ruling, for the main window too). No click at the neutral point. run.txt says whether the click was
     needed (open point 72).
   - **A1.** The pointer at the neutral point; a real `Ctrl+Shift+P`; a wait until `CommandPaletteBox` is
     UI Automation's focused element; `about` typed on the real keyboard; a wait until
     `CommandPaletteList` holds one `CommandPaletteItem`; the selected row's name asserted to be
     `About VisualCommit…` before a real Enter (12.3.2; else the method fails before the key); a wait
     until `AboutVersion` is shown (`ShownElements("AboutVersion")` holds one: a text only About shows,
     as `RealApp.ShownSettingsSection` tells a section by such a text; the section's panel
     `SettingsAbout` is a plain `StackPanel`, as `SettingsAccounts` is, and so not a UI Automation
     control element, phase 3's design, section 0) and `SettingsNavAbout` is UI Automation's focused
     element. `AboutVersion`'s automation name equals `RealApp.BuiltVersionDisplay()`
     (`AppVersionInfo.Parse` of the built `VisualCommit.dll`'s `ProductVersion`; ruling 59, 12.2) and
     ends with ` (`, seven hexadecimal digits and `)`; if the pass's Release build carries no commit,
     the launch fails saying so, and the lead rules then. run.txt names both the `ProductVersion` and
     the name (open point 65). The pointer at the neutral point; capture `a1a-about-dark`, compared with
     the scripted twin, which shows `0.1.0 (1a2b3c4)`, seven characters apart (1.6).
     - **A1 at 1920×1080** (twin: A3's picture, method 3; 12.3.4: "at 1920×1080 too when
       `app.Fits(1920, 1080)`"). When `app.Fits(1920, 1080)`: the window sized 1920×1080; a wait until
       `SettingsNavAbout`'s place in the client area at scale 1
       (`app.BoundsOf(app.Find("SettingsNavAbout"))`, UI Automation's bounds relative to the client area
       divided by `Scaling`) is `Phase8aExpectations.A`'s place at 1920×1080, (551, 415) within 2 (8.5's
       A3; open point 56); the settings card itself is no UI Automation element (a plain `Border` with
       only an `x:Name`, `SettingsCard`). The pointer at the neutral point; capture `a3a-about-1920`,
       compared with A3's picture; then the window sized 1100×700 again and a wait until
       `SettingsNavAbout`'s place is its 1100×700 place, (141, 255) within 2 (8.5's A1; open point 64). Otherwise run.txt says, in phase 0's form, `A1 (1920x1080): not run. A
       1920x1080 window does not fit the {w}x{h} work area at {scaling} scaling.`, and the report's A1
       says "Not run" for its capture at 1920×1080 with that reason (plan.md's Visual test gate, step 4).
   - **A4.** A real Tab; `WaitForFocusedElement("AboutShowLicenseButton")`, whose failure ends the method
     before any further key; a real Enter; a wait until `DialogTitle` reads `MIT License` and the focus
     is in `TextDocumentText` by part K's text's wait: UI Automation's focused element is
     `TextDocumentText`, or lies inside that element's bounding rectangle and is no button
     (AvaloniaEdit's `TextArea`, which takes the focus, has no automation id of its own, so
     `WaitForFocusedElement("TextDocumentText")` cannot be used), with the helper §12 writes for part K's
     launches 11 and 12; run.txt says which of the two held. The pointer at the neutral point; capture
     `a4a-license`, compared with the scripted twin. A real Esc; a wait until no `TextDocumentText` is in
     the window and `AboutShowLicenseButton` is UI Automation's focused element again, `AboutVersion`
     still shown (Esc closed the dialog alone). "Copy all" is never pressed.
   - **The screen closed.** A real Esc; a wait until no `SettingsDialog` is in the window, the named
     condition B2 waits on before its real click on `ThemeSwitch` (part B's B2).
   - B2 follows (part B); the launch ends closed by its window, exit code 0.

Unverified until §12's first runs and D246's (8.9 item 7, 12.9): that `ProductVersion` carries
`+<commit>`, so that `BuiltVersionDisplay()` holds the short commit (else it reads the assembly's
attribute with `System.Reflection.Metadata`), and that the pass's Release build carries a commit at
all (else launch 3 fails saying so and the lead rules then, open point 65); what UI Automation reports
as focused while the `TextArea` in `TextDocumentText` has the focus (the text's wait above, as part
K's; open point 66).

### Part U: screen readers: the graph's row, the names and the sweep

Part U covers §9 (D411, D435 as D450 amends D445, D49 as D435 amends it; design 9, 1.12): the commit
graph as a UI Automation List whose one child, the ListItem `GraphSelectedRow`, is the selected row (the
focus row while several are selected), named by its subject, author, date and refs, its short SHA its
help text, a polite live region whose name change is raised once per move, wherever the keyboard focus
is (U1); a name on every interactive control, proven by the sweep `AutomationSweep` over 14 states of
the app (U2); and the fixed names of 1.12's table, read with every tooltip, help text and key as they
were (U3). Checks U1 to U3, design 9.5's list, with its six pictures. Its R steps (9.5, 12.3.4 launch
13, 12.5): U1's rows and U3's names, on a stopped merge, one launch, no capture. The Narrator
walk-through, 21 steps by the owner, is this part's check by hand (9.3.9; D411, D450).

#### Conventions of part U

- **Code** (9.2, 1.12, 1.15): `Phase8aChecks.U.cs` (`Phase8aChecksU : Phase8aChecks`);
  `Phase8aExpectations.U.cs` (`Phase8aExpectations.U`: the names and hints below word for word, U1's
  rows, the names of 1.12's table and today's tooltips, help texts and keys they keep, the picture
  names, the scenarios' extra state (open point 79), and for §12 launch 13's facts: its scenario and
  session, the untracked file's name and content, the three rows' names and hints, the names it reads,
  the 5 s it waits for each event; 9.5's last paragraph); `Phase8aExpectationTests.U.cs` (the names
  equal `AutomationNames`' constants and formats; the words equal `GraphRowSpeech`'s constants; every
  picture name matches 0.3's pattern and is used once); `AccessibleNameSweepTests.cs` (8a's project,
  ruling 37; 14 facts, 9.3.8: 13 start the app once each, and state 14 shows §4's notice window
  alone); `ShellDriver.Phase8a.Uia.cs` (`GraphRowPeer()`, the window's peer made once,
  `RecordNameChanges(AutomationPeer)`; 1.15); `AutomationSweep` (Testing:
  `FindProblems`, `AssertNamed`, `AutomationProblem`; 1.12). The App.Tests classes of 9.4
  (`GraphRowSpeechTests`, `CommitGraphControlAutomationTests`, `AutomationSweepTests`,
  `StatusBarTests.The_operation_text_is_a_polite_live_region`) are not checks; "What proves each
  Delivers item" names them. §12's method is in `Phase8aRealWindowPass.U.cs`, with
  `RealApp.Phase8a.cs`'s `RecordSelectedRowEvents` (12.2).
- **Scenarios and sessions.** "ops" (`Scenarios.OperationsAsync()`, phase 3's G0; its SHAs pinned by
  `ScenarioTests`, phase 3's report): commits by Test Author on 2026-01-01, HEAD on a clean `main`;
  with a working change the graph's display rows are 0 the working-changes row, 1 the stash
  `stash@{0}` "On main: Experiment" (12:07, `0e95aca`), 2 "Conflicting change" `feature/conflict`
  (12:06, `078ef05`), 3 "Fast-forward me" `feature/ff` (12:05, `d93814d`), 4 "Main work" `main` ✓,
  `origin/main`, `v1.1` (12:04, `88d6b91`), 5 "Alpha two" `feature/alpha`, `origin/feature/alpha`
  (12:03, `ce5ea6a`), 6 "Alpha one" (12:02, `dff8fbf`), 7 "Add core" `v1.0` (12:01, `8beca34`), 8
  "Initial commit" (12:00, `4ef1f7c`). Its left panel: Local branches with the closed folder `feature`
  (`alpha`, `conflict`, `ff`) and `main`; Remotes with `origin` open, its closed folder `feature`
  (`alpha`) and `main`; Tags `v1.0`, `v1.1`; Stashes `On main: Experiment`. Method 1 changes README.md
  in the copy's work tree before the start; method 3 also changes `src/core.txt` and stages it
  (`git add`), and writes the untracked `u-untracked.txt` (the contents `Phase8aExpectations.U`'s; open
  point 79). The stopped merge (`Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`, phase 4's GC-M):
  `git merge feature/theirs` stopped on `main` with `notes.txt` both modified; the banner above the
  column headers; display row 0 the working-changes row "Working changes · 1 conflict", display row 1
  "Ours edits" `main` ✓ (12:09, `da31c97`), display row 2 "Kinds edits" `feature/kinds` (12:08,
  `3a9de3e`), then GC0's other rows. Method 2 writes `u-untracked.txt` into its work tree before the
  start, as launch 13 does (12.3.4), so that the two hold the same session. Every start is 8a's
  `OpenAsync` (one tab, dark, 1100×700, the machine's search for git; 1.17), on a fresh copy and a fresh
  data folder. Dates are UTC in both harnesses (`DateDisplay.TimeZoneVariable`, which `GitIsolation`
  and `RealApp` set to `UTC`), so the names hold 9.3.5's dates.
- **What stands for a screen reader.** The headless run has no UI Automation client and no Windows
  bridge. `GraphRowPeer()` makes the window's peer and the graph's, once, as a client's first read
  does, and returns the graph's one child; `RecordNameChanges(row)` stands for a client's handler of the
  row's Name property changes, recording each change's old and new value in order. A Name change of a
  peer whose live setting is Polite is what the Windows bridge turns into LiveRegionChanged (9.3.7); so
  the scripted checks assert the Name change and the live setting, and only the real window asserts the
  LiveRegionChanged event (D450). Every other name is read through the control's own automation peer
  (`ControlAutomationPeer.CreatePeerForElement`): its name, help text, accelerator key, automation id
  and control type, as a client reads them, never `AutomationProperties` alone (a control that sets no
  help text gives its tooltip as one through its peer: U3 says where). No fake stands for git: every
  check runs on real git.
- **What is read where.** "The exposed row" is the graph peer's one child: its automation id
  `GraphSelectedRow`, class `CommitGraphRow`, control type ListItem, its name, its help text, its live
  setting Polite, its `IsSelected` true and `SelectionContainer` the graph's peer; the graph's peer
  `GraphRows`, control type List, name `Commit graph`, `CanSelectMultiple` true, `IsSelectionRequired`
  false, `GetSelection()` exactly that one peer. "One Name change" is one more change in the recorder,
  waited for, then asserted alone once the dispatcher has settled (`WaitForIdleAsync`): the row's
  `Update` is posted at Background, once per dispatcher turn (9.3.4), so a check never asserts at the
  key. The keyboard focus is `FocusedAutomationId()`, the focused control's own automation id or its
  nearest ancestor's: it names a control, never a peer, so headless it cannot be `GraphSelectedRow`
  whatever §9 does. Ruling 35 is asserted here as the focus on `GraphRows` (on the left panel's
  `RefItem` after a click there) and no control in the window carrying the automation id
  `GraphSelectedRow` (the id is the row peer's alone), and in the real window as UI Automation's
  focused element never `GraphSelectedRow` (launch 13). The row peer's `HasKeyboardFocus` is the
  graph's `IsFocused` (9.3.3). The row peer's bounding rectangle is the display row's: 26 high
  (`WindowLayout.RowHeight`), inside the graph control's bounds, its vertical centre the
  graph's own `RowBounds` (or `WorkingRowBounds`) centre in the window's coordinates, `IsOffscreen`
  false. A sweep's result is `AutomationSweep.AssertNamed(window, state)`: no problem of 9.3.8's four
  rules (`empty`, `type name`, `key text`, `duplicate`), as D456 reads them (U2), the `duplicate` rule
  not applying to `RefItem` under `RefList`, the one exemption `AutomationSweep` holds by the automation
  id, with its reason in a comment (open point 81); `AssertNamed`, which methods 2 and 3 run, throws
  `AutomationSweepException` when it finds a problem, naming every problem. `AccessibleNameSweepTests`
  reads each state's problems with `AutomationSweep.FindProblems` instead and fails once at the fact's
  end (U2). Git's state is the scenario's `ExactStateAsync()`, read before the first step and at the
  end.
- **The sweep's states** (9.3.8; 1.17's bullets on wave 2). `AccessibleNameSweepTests`, one
  `[AvaloniaFact]` each, 14 in all, 13 with one start: (1) the welcome page and the palette; (2) the
  settings screen, each section (About's among them, `2: settings, About`), the Git program row,
  Accounts with the three providers' fakes on and each provider's sign-in dialog; (3) About
  (`3: about`), the license's text dialog over it (`3: the license`: one text, no switch) and the
  notices' dialog on Avalonia's file, reached by its switch (`3: the notices, Avalonia's file`), each
  dialog closed before the next opens and the screen before the fact ends (§8's, swept since §9's
  merge, which came after §8's, 0.4; D456 (5), as the lead took the build's correction at that merge:
  only the notices' dialog has a switch, so a vendor file is reached there); (4) every dialog kind
  through its presenter, as `PromptDialogTests` does: through
  the dialog host the remotes, branch name, tag, stash, reset, pick, choice, push, finishing, error and
  message dialogs, the workspace's name, the repository browser over the three providers, the
  add-repositories checklist with two repositories found (`app`, `site`) and the create-pull-request
  form with its base given (a fixed fake of its actions), then the confirmation, and git's six prompt
  kinds through `PromptDialogPresenter` (the palette is state 1's, the settings screen and the sign-in
  dialogs state 2's); (5) the ops tab: two branches hidden, then one soloed, a commit with files and
  parent links, two unstaged and one staged file with one row selected
  and the pointer over another, the diff's hunk bars, the restore bar after a discard, the activity log,
  the graph's row, a context menu on the graph and on a file; (6) search, compare, file history,
  blame, reflog; (7) the rebase list; (8) the resolver, its card, the banner; (9) the dashboard; the
  first run through `FakeGitSetupProbe`: (10) Windows' Missing, then by "Check again" ChosenMissing,
  TooOld and NotWorking with git's words (`FakeGitAnswer.ChosenMissing` throws for a lookup without a
  `ChosenPath`, so the fact writes `gitExecutable` before the start, as F3 does; 1.17), (11) macOS's
  Missing, (12) Linux's Missing, (13) HelperMissing (`askPassHelper` a path without a file), and in each
  state of (10) to (13) `GitSetup`'s automation name the state's title (`GitSetupTitle`'s text) and its
  help text the state's body (`GitSetupBody`'s text); (14) §4's notice window alone, no app, through
  `InstanceNoticeHost.Show(platform, folder, theme)`, three arguments (1.11, 1.17). §5's buttons and links
  are named by their own texts, bound where the text changes (`GitSetupCheckAgainButton` reads
  `Checking…` during a search; 1.17), and a stage row that holds the keyboard focus has its two buttons
  in UI Automation at `Opacity` 0 (D425; 1.17), so the sweep meets a focused row's buttons too.
- **Pictures.** Dark, 1100×700. Every picture passes `AssertNoClippedText`. U1's first picture is taken
  twice: saved as `u1a-graph-row-head` before any peer of the graph is made, then, once the peer is
  made, a frame captured again with the pointer at the same neutral point and not saved (`Shot` refuses a
  name twice in a run): the two frames are the same bytes (9.5, 9.6: names, live settings and peers take
  no part in layout or drawing). A hover that U2 and U3 picture opens the row's tooltip, so those steps
  rest the pointer until it shows and keep it for the capture.
- **Routes** are those earlier checks use: a ref's click in the left panel selects and reveals its
  commit (`RefActivated`, phase 3), and its folder's click opens it; F6 goes round the regions shown
  (D213: the left panel, the graph area, the right panel, the activity log), the graph area's target
  `GraphRows` (phase 6's `k12a`); the graph's keys Down, Up, Home and Shift with an arrow (phase 4's 4h:
  Shift extends from a commit anchor, and from the working-changes row or a stash moves as without,
  `ExtendWithKey`); the working-changes row by a click on display row 0 and the resolver by a double-click
  on the conflicted file in the stage panel (phase 4's C10); `Hide {b}` and `Solo {b}` from a branch's
  menu (phase 5's FL1, FL2); `Compare {b} with {cur}` from a local branch's menu (phase 5's 6.2); Ctrl+F
  for the search bar (phase 3), Ctrl+J for the activity log (phase 6); a row's discard by its
  `RowDiscardButton` and the confirmation's `ConfirmButton` (phase 2's checks 17 and 18).
- **What changes in git.** Methods 1 and 2 change nothing: `ExactStateAsync()` at the end equals the
  first. Method 3 discards README.md to show the restore bar (open point 83): at its end the state differs
  only in README.md, as committed again, and one new ref under `refs/visualcommit/backup/` (D67); the
  hidden and soloed branches are the graph's filter, not git's.
- **Keys** are written as Windows shows them, Ctrl the headless command modifier on every platform (the
  report's conventions). A tooltip or key text that holds a key reads the platform's words on CI's
  macOS job (`ShellToolTips.*For(KeyTexts.Current)`); the checks compare it with the same constants,
  whose Windows words are quoted below. The names hold no key text on any platform (9.3.8's `key text`
  rule).
- **The lagged list** (9.7, 0.4): nothing new; 8a's project is its last line, and part U and
  `AccessibleNameSweepTests` run in it at §9's merge and at each later merge that runs the list.
- **Time** (0.6, 9.7): each method under about 40 s alone; part U with the sweep's starts about 3 to 5
  minutes of 8a's project.

#### Test methods and app starts (part U)

Three methods, three starts (9.7), each on a fresh copy of its scenario and a fresh data folder; beside
them `AccessibleNameSweepTests`, 14 facts: 13 starts of the app, and state 14's notice window shown
alone (9.3.8). Method 2 makes on the stopped merge what the real window's launch 13 reads of the
graph's rows (it has no capture, so it has no twin; 12.3.4). The step letters run on in the table's
order: `u3a` in method 2, `u3b` in method 3 (9.5's names).

| # | Test method | Checks | Start and what is set before it | Window, theme | Pictures |
|---|---|---|---|---|---|
| 1 | `Check_U1_graph_row_by_keys` | U1 | one tab on a fresh "ops" (`Scenarios.OperationsAsync()`), README.md changed in its work tree | 1100×700, dark | `u1a`, `u1b`, `u1c` |
| 2 | `Check_U1_U3_stopped_merge_rows_and_resolver` | U1 (the stopped merge's three rows, launch 13's), U3 (the resolver) | one tab on a fresh `Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`, `u-untracked.txt` written into its work tree (launch 13's session) | 1100×700, dark | `u3a` |
| 3 | `Check_U2_U3_sweep_and_names_on_the_ops_tab` | U2, U3 (the ops tab) | one tab on a fresh "ops": README.md changed, `src/core.txt` changed and staged, `u-untracked.txt` written (open point 79) | 1100×700, dark | `u2a`, `u3b` |

#### Steps and expected results (part U)

A step numbered in a method runs on from that method's last step in an earlier check.

**U1. The graph's selected row, as a screen reader meets it** (R: the stopped merge's three rows in
launch 13, no capture; D411, D435, D450, D49; design 1.12, 9.3.2 to 9.3.5, 9.5, 12.3.4 launch 13).
Methods 1 and 2.

Steps, method 1:
1. Open the tab on the fresh "ops"; wait until it is ready and the graph drawn, display row 0 the
   working-changes row. Take `ExactStateAsync()`.
2. Click `Local branches › main` in the left panel; wait until "Main work" (display row 4) is the
   graph's one selected row and the right panel shows its details (`DetailsSubject` `Main work`). Press
   F6; wait until `GraphRows` has the keyboard focus. Screenshot (`u1a-graph-row-head`).
3. Make the row's peer (`GraphRowPeer()`) and record its Name changes (`RecordNameChanges`). Capture a
   frame, the pointer at the neutral point (`app.Capture()`, not saved).
4. Press Down; wait for one Name change.
5. Press Up; wait for one Name change.
6. Press Shift+Down; wait for one Name change. Press Shift+Down; wait for one Name change. Screenshot
   (`u1b-graph-rows-three-selected`).
7. Press Up six times, one at a time, each followed by a wait for one Name change. Screenshot
   (`u1c-graph-working-row`).
8. Click `Local branches › feature` (the folder opens); wait until its rows show and the app is idle.
   Click `Local branches › feature › alpha`; wait for one Name change.
9. Take `ExactStateAsync()` again.

Steps, method 2:
1. Open the tab on the fresh stopped merge (with `u-untracked.txt`); wait until it is ready, the banner
   shown. Take `ExactStateAsync()`.
2. Click display row 1, "Ours edits" (below the banner; `ShellDriver.GraphRowPoint(0)`, which counts
   commit rows without the working-changes row); wait until it is the graph's one selected row and
   `GraphRows` has the keyboard focus. Make the row's peer (`GraphRowPeer()`) and record its Name
   changes.
3. Press Up; wait for one Name change. Press Down; wait for one Name change. Press Down; wait for one
   Name change.

Expected, method 1:
- After step 2 (`u1a`): the graph's rows framed by the keyboard's focus frame (`:focus-visible`, as
  phase 6's `k12a-f6-graph`), "Main work" selected; `Local branches › main` with the selection
  background (D98); the right panel shows "Main work"'s details. `FocusedAutomationId()` `GraphRows`.
- After step 3: the frame equals `u1a`'s, the same bytes (9.6). The graph's peer: `GraphRows`, List,
  `Commit graph`, one child, the exposed row: `GraphSelectedRow`, ListItem, class `CommitGraphRow`, live
  setting Polite, `IsSelected` true, its name `Main work, Test Author, 2026-01-01 12:04, current branch
  main, remote branch origin/main, tag v1.1` and its help text `Commit 88d6b91` (9.3.5's example word for
  word), its bounds display row 4's; `GetSelection()` that one peer; `HasKeyboardFocus` true. No Name
  change is recorded between step 3 and step 4's key.
- After each of steps 4 to 8: exactly one Name change since the key or the click, its new value the
  row's name in the table below; from the second change on, its old value the name the change before
  raised (the first change's old value is open point 77). Then the graph's peer has one child, the same
  peer instance as after step 3 (9.3.2: one row peer for the graph's life); the exposed row's name and
  help text are the table's, its bounds the display row's, `GetSelection()` that one peer, whatever
  the number of rows selected (9.3.2); its `HasKeyboardFocus` and `FocusedAutomationId()` as the table
  says (`GraphRows`, `RefItem` after step 8); no control in the window carries the automation id
  `GraphSelectedRow` (the conventions: only the real window reads UI Automation's focused element).

  | After | Key | The exposed row (display row); selected | Name | Help text | Focus |
  |---|---|---|---|---|---|
  | step 4 | Down | Alpha two (5); one | `Alpha two, Test Author, 2026-01-01 12:03, branch feature/alpha, remote branch origin/feature/alpha` | `Commit ce5ea6a` | `GraphRows` |
  | step 5 | Up | Main work (4); one | `Main work, Test Author, 2026-01-01 12:04, current branch main, remote branch origin/main, tag v1.1` | `Commit 88d6b91` | `GraphRows` |
  | step 6, first | Shift+Down | Alpha two (5), the focus row; Main work and Alpha two | `Alpha two, …` as after step 4 | `Commit ce5ea6a, 2 commits selected` | `GraphRows` |
  | step 6, second | Shift+Down | Alpha one (6), the focus row; Main work, Alpha two and Alpha one | `Alpha one, Test Author, 2026-01-01 12:02` | `Commit dff8fbf, 3 commits selected` | `GraphRows` |
  | step 7, 1st | Up | Alpha two (5); one | as after step 4 | `Commit ce5ea6a` | `GraphRows` |
  | step 7, 2nd | Up | Main work (4); one | as after step 5 | `Commit 88d6b91` | `GraphRows` |
  | step 7, 3rd | Up | Fast-forward me (3); one | `Fast-forward me, Test Author, 2026-01-01 12:05, branch feature/ff` | `Commit d93814d` | `GraphRows` |
  | step 7, 4th | Up | Conflicting change (2); one | `Conflicting change, Test Author, 2026-01-01 12:06, branch feature/conflict` | `Commit 078ef05` | `GraphRows` |
  | step 7, 5th | Up | the stash (1); one | `Stash: On main: Experiment, 2026-01-01 12:07, stash@{0}` (9.3.5's example) | `Commit 0e95aca` (open point 78) | `GraphRows` |
  | step 7, 6th | Up | the working-changes row (0) | `Working changes` | empty | `GraphRows` |
  | step 8 | a click on `alpha` | Alpha two (5); one | as after step 4 | `Commit ce5ea6a` | `RefItem` named `alpha` (the row's text; open point 81); the row's `HasKeyboardFocus` false |

  In the table's rows with `GraphRows` the row's `HasKeyboardFocus` is true. Step 8's click on the
  folder `feature` raises no Name change: the graph's selection did not change (9.3.4).
- After step 6 (`u1b`): three rows with the selection background, "Main work", "Alpha two" and "Alpha
  one"; the right panel the multi-selection panel, `3 commits selected` (9.9 item 3); `Local branches ›
  main` without the selection background (D98: several selected). The graph's rows still framed.
- After step 7 (`u1c`): the working-changes row selected, the right panel the stage panel,
  `Unstaged files (1)` with README.md, `Staged files (0)`; the graph's rows framed.
- After step 9: `ExactStateAsync()` unchanged.

Expected, method 2:
- After step 2: the exposed row's name `Ours edits, Test Author, 2026-01-01 12:09, current branch main`,
  its help text `Commit da31c97`; `FocusedAutomationId()` `GraphRows`.
- After step 3, one Name change after each key, the table's names (`Phase8aExpectations.U`'s for launch
  13):

  | Key | The exposed row (display row) | Name | Help text |
  |---|---|---|---|
  | Up | the working-changes row (0) | `Working changes, 1 conflict` (its text `Working changes · 1 conflict`, ` · ` read as `, `, 9.3.5) | empty |
  | Down | Ours edits (1) | `Ours edits, Test Author, 2026-01-01 12:09, current branch main` | `Commit da31c97` |
  | Down | Kinds edits (2) | `Kinds edits, Test Author, 2026-01-01 12:08, branch feature/kinds` | `Commit 3a9de3e` |

  Each time `GraphSelectedRow`, ListItem, live setting Polite, one element in `GetSelection()`, its
  bounds the display row's (below the banner), the focus on `GraphRows`. `u-untracked.txt` does not
  change the working-changes row's text (it counts conflicts only).

**U2. The sweep: every interactive control named** (not R; D411, D435; design 1.12, 9.3.8, 9.4, 9.5).
Method 3 and `AccessibleNameSweepTests`.

Steps, method 3:
1. Open the tab on the fresh "ops" with its three changes; wait until it is ready. Take
   `ExactStateAsync()`. Rest the pointer at the neutral point.
2. Press Ctrl+F; wait until the search bar shows with `SearchBox` focused, its mode `Commits`. Read the
   search bar's three names (U3). Press Esc; wait until the search bar has closed.
3. Click `Local branches › feature` (the folder opens). Right-click `Local branches › feature ›
   conflict`; click `Compare feature/conflict with main`; wait until `CompareSwap` and `CompareClose`
   show. Read their names (U3). Click `CompareClose`; wait until the comparison has closed (open point
   82).
4. Right-click `Local branches › feature › alpha`; click `Hide feature/alpha`; wait until the graph has
   no row "Alpha one" or "Alpha two" and `alpha`'s row under Local branches shows `RefHiddenToggle`.
   Click `Remotes › origin › feature` (the folder opens); wait until its `alpha` row shows
   `RefHiddenToggle` (open point 80).
5. Press Ctrl+J; wait until the activity log shows (`ActivityLogClose` shown).
6. Click display row 0, the working-changes row (`ShellDriver.WorkingRowPoint()`); wait for the stage
   panel and its lists. Click the unstaged list's row of README.md; wait until its diff shows
   (`CloseDiffButton` shown).
7. Rest the pointer on the unstaged list's row of `u-untracked.txt` until its tooltip, `u-untracked.txt`,
   shows. Screenshot, pointer kept (`u2a-sweep-ops-tab`). Run `AutomationSweep.AssertNamed` on the
   window (state `U2: the ops tab`).

Expected:
- `AccessibleNameSweepTests`: each of its 14 facts passes, `AutomationSweep.FindProblems` reporting
  nothing in any state of the 14 (the conventions' list; a fact that finds a problem fails once at
  its end, listing every state's problems), and in states (10) to (13) `GitSetup`'s automation name
  and help text are the state's title and body (9.3.8; part F's texts). The rules are 9.3.8's as D456
  reads them at §9's merge: a control whose control type is ScrollBar, and what lies inside it (its
  thumb, its line and page buttons), is not checked (D456 (1): a scroll bar is a part of a scrolling
  control's template, which a screen reader announces by its type and value); a control's container for
  `duplicate` is the nearest list above it, the search ending at the root of the dialog it lies in
  (`DialogView`), so that two dialogs that lie over each other, a sign-in dialog over the settings
  screen, each have a Cancel of their own (D456 (2)); and a part of a name's last parenthesis is a key
  text also when it is two key texts joined by ` to ` (`Ctrl+1 to Ctrl+8`, the form in which the app's
  own key texts give a run of keys, `ShortcutText.FormatAll`; D456 (4)). Its first run, on the code
  before §9's change (9.4: each new class fails first), reports at least `ActivityLogToggle`
  (`type name`), the three splitters (`empty`), the resolver's five and the search bar's three
  (`key text`), and duplicates of `RowStageButton`, `RowDiscardButton` and `RefHiddenToggle`
  (`duplicate`). This report lists that run's findings, state by state, under U2 at §9's merge (open
  point 90); a control the sweep reports outside 1.12's table is named only at the lead's word, from
  its area's texts class (9.3.6), and any other duplicate the first run finds is the lead's at §9's
  merge (open point 81).
- The first run's findings (§9's build, on the code before 1.12's names; 13 facts then, state 3 not
  yet written; 12 failed, state 14's alone passed): in every state of 1 to 13, `LeftSplitter` and
  `RightSplitter` (`empty`, Thumb) and `ActivityLogToggle` (`type name`, its name
  `Avalonia.Controls.StackPanel`), the dashboard (9) showing the toggle alone; (1) the palette's
  `PART_VerticalScrollBar` (`empty`); (2) Keyboard shortcuts' `PART_VerticalScrollBar` (`empty`), and
  in each provider's sign-in dialog over the settings screen `CancelButton` (`duplicate`); (5) from
  `feature/alpha` hidden on, `RefHiddenToggle` `Hidden in the graph. Click to show it` (`duplicate`),
  with the activity log shown `ActivityLogSplitter` (`empty`), and the stage rows' `RowStageButton`
  `Stage` and `RowDiscardButton` `Discard` (`duplicate`); (6) the search's `SearchPrevious`,
  `SearchNext` and `SearchClose` (`key text`) and the blame's `PART_HorizontalScrollBar` (`empty`);
  (8) the resolver's five (`key text`); (14) nothing. After 1.12's names only the scroll bars and the
  stacked dialogs' Cancel were left, outside the table: D456 (1) and (2) settle them (open point 90).
- After step 2: the search bar's names as U3's table; after its Esc no search bar.
- After step 3: the comparison of `feature/conflict` with `main`, a branch comparison (`IsBranches`), so
  `CompareSwap` and `CompareClose` show (U3's table); after the click on `CompareClose` it has closed.
- After step 4: the graph without "Alpha one" and "Alpha two"; under Local branches `alpha` dimmed with
  its `RefHiddenToggle`, and under `Remotes › origin › feature` `alpha` dimmed with its
  `RefHiddenToggle` (phase 5's FL1: the upstream hidden with it); `conflict`, `ff` and `main` as before.
- After step 6: the stage panel `Unstaged files (2)` (README.md, `u-untracked.txt`) and `Staged files
  (1)` (`src/core.txt`); README.md's row selected, its diff in the graph area with `CloseDiffButton`; the
  activity log under it.
- After step 7 (`u2a`): the state 9.5 names, the busiest of the ops tab: the activity log shown, README.md's
  row selected (its `RowStageButton` and `RowDiscardButton` drawn) and `u-untracked.txt`'s row hovered
  (its two buttons drawn, its tooltip `u-untracked.txt` open), the two hidden branches' rows dimmed with
  their eyes. Two rows' `RowStageButton`s and two `RowDiscardButton`s are in UI Automation under
  `UnstagedFileList`, each pair with different names (U3's table), so no `duplicate`. `AssertNamed`
  reports nothing: the left panel's rows that share a name under `RefList` (two `main`, two `feature`,
  two `alpha`) are `RefItem`s, which the `duplicate` rule exempts (open point 81), and the two eyes read
  their full names (U3's table; open point 75). No text cut.

**U3. The names, and every tooltip as before** (R: the names of the resolver, the activity log, the
splitters, the search bar and a stage row in launch 13; D411, D435; design 1.12, 9.3.6, 9.5, 12.3.4
launch 13). Methods 2 and 3.

Steps, method 2, after U1's step 3:

4. Click display row 0, the working-changes row (`ShellDriver.WorkingRowPoint()`); wait for the stage
   panel, its unstaged list holding `notes.txt` (conflicted, its tooltip `Conflict: click to resolve
   it`) and `u-untracked.txt`. Rest the pointer at the neutral point. Double-click the unstaged
   `notes.txt`; wait until the resolver shows it. Rest the pointer at the neutral point. Screenshot
   (`u3a-resolver-names`).
5. Read the resolver's five names. Run `AutomationSweep.AssertNamed` on the window (state `U3: the
   resolver`).
6. Press Esc; the resolver stays: the double-click left the keyboard focus in its result, where Esc is
   the resolver's own and closes nothing (phase 4's design 11.10). Click `ResolverCloseButton`; wait
   until the resolver has closed. Take `ExactStateAsync()` again.

Steps, method 3, after U2's step 7:

8. Read the names of README.md's and `u-untracked.txt`'s `RowStageButton` and `RowDiscardButton`,
   `CloseDiffButton`, `ActivityLogToggle`, `ActivityLogClose`, `LeftSplitter`, `RightSplitter`,
   `ActivityLogSplitter`, and the two `RefHiddenToggle`s. Read the help texts of the two `alpha` rows,
   `Local branches › feature › alpha` and `Remotes › origin › feature › alpha` (`RefItem`), each
   through its own peer.
9. Right-click `Local branches › feature › ff`; click `Solo feature/ff`; wait until `ff`'s row shows
   `RefSoloToggle`.
10. Rest the pointer on the staged list's row of `src/core.txt` until its tooltip, `src/core.txt`, shows.
    Screenshot, pointer kept (`u3b-stage-row-names`). Read the names of its `RowUnstageButton`,
    `RefSoloToggle` and the two `RefHiddenToggle`s. Run `AutomationSweep.AssertNamed` on the window (state
    `U3: the ops tab, one soloed`).
11. Move the pointer onto README.md's row (selected, its buttons drawn); click its `RowDiscardButton`;
    wait for the confirmation `Discard changes?`; click `ConfirmButton` (`Discard`); wait until the
    restore bar shows `Discarded changes to README.md.` (`RestoreBarText`). Read
    `DismissRestoreButton`'s name. Run `AutomationSweep.AssertNamed` on the window (state `U3: the restore
    bar`).
12. Take `ExactStateAsync()` again.

Expected: each control in the window at its step has the name of this table (1.12's, word for word),
and its tooltip (`ToolTip.Tip`), help text and accelerator key as they were (9.3.6: no tooltip, help
text or key changes; ruling 34, whose one exception, the branch rows' help text, D456 (3), method 3
reads at step 8), today's texts as the code has them on `d2318ba`; the Windows words are quoted, CI's
macOS job reading the same constants' own (the conventions). A help text is the peer's
`GetHelpText()`, as a client reads it. Avalonia 12.1.3's `ControlAutomationPeer.GetHelpTextCore` gives
`AutomationProperties.HelpText`, else a data-validation error, else a string `ToolTip.Tip` (the base's
fallback to the tooltip, which design 9.3.3 overrides for the graph's row alone). That fallback rested
on a reading of Avalonia 12.1.3's source and on 9.3.3 until §9's build ran U3: each control the table
gives "by the peer's fallback" gave its tooltip through its peer (design 9.9 item 7). It falls back
only to the control's own tooltip, never to one inside it (open point 81). Where the table says "the
tooltip", the control sets `AutomationProperties.HelpText` to the tooltip's text, as
`ShellToolTipTests` reads it. Where it says "the tooltip, by the peer's fallback", the control sets no
`AutomationProperties.HelpText`, as before §9 (ruling 34), and its peer still gives the tooltip: the
check asserts the peer's value equal to the tooltip and `AutomationProperties.GetHelpText` null there,
as on `d2318ba`, never the second in place of the first. Where the Help text column says "none" (the
splitters), the control has neither, and the peer gives none:

| Control (its row) | Name | Tooltip | Help text | Key |
|---|---|---|---|---|
| `ResolverPreviousConflict` | `Previous conflict` | `Previous conflict (Alt+Up)` (`ShellToolTips.PreviousConflict`) | the tooltip | `Alt+Up` |
| `ResolverNextConflict` | `Next conflict` | `Next conflict (Alt+Down)` | the tooltip | `Alt+Down` |
| `ResolverPreviousFile` | `Previous file` | `Previous file (Alt+Left)` | the tooltip | `Alt+Left` |
| `ResolverNextFile` | `Next file` | `Next file (Alt+Right)` | the tooltip | `Alt+Right` |
| `ResolverCloseButton` | `Close the resolver` | `Close (Esc)` (`ShellToolTips.CloseResolver`) | the tooltip | `Esc` |
| `SearchPrevious` | `Previous match` | `Previous match (Shift+Enter, Shift+F3)` (`SearchViewModel.PreviousToolTip`) | the tooltip | `Shift+F3` |
| `SearchNext` | `Next match` | `Next match (Enter, F3)` | the tooltip | `F3` |
| `SearchClose` | `Close the search` | `Close the search (Esc)` | the tooltip | none |
| `CompareSwap` | `Swap From and To` | `Swap From and To` | the tooltip | none |
| `CompareClose` | `Close the comparison` | `Close the comparison` | the tooltip | none |
| `CloseDiffButton` | `Close the diff` | `Close` | the tooltip, by the peer's fallback | none |
| `ActivityLogToggle` | `Activity log` | `Hide the activity log (Ctrl+J)` (the log shown) | the tooltip | `Ctrl+J` |
| `ActivityLogClose` | `Close the activity log` | `Close the activity log` | the tooltip, by the peer's fallback | none |
| `LeftSplitter`, `RightSplitter`, `ActivityLogSplitter` | `Resize the left panel`, `Resize the right panel`, `Resize the activity log` | none | none | none |
| `RowStageButton`, `RowDiscardButton` (README.md) | `Stage README.md`, `Discard README.md` | `Stage`, `Discard` | the tooltip, by the peer's fallback | none |
| `RowStageButton`, `RowDiscardButton` (`u-untracked.txt`) | `Stage u-untracked.txt`, `Discard u-untracked.txt` | `Stage`, `Discard` | the tooltip, by the peer's fallback | none |
| `RowUnstageButton` (`src/core.txt`) | `Unstage src/core.txt` | `Unstage` | the tooltip, by the peer's fallback | none |
| `RefHiddenToggle` (`feature/alpha`; `origin/feature/alpha`) | `Show feature/alpha in the graph`; `Show origin/feature/alpha in the graph` (open point 75) | `Hidden in the graph. Click to show it` | the tooltip, by the peer's fallback | none |
| `RefSoloToggle` (`feature/ff`) | `Stop soloing feature/ff` (open point 75) | `Soloed. Click to stop soloing it` | the tooltip, by the peer's fallback | none |
| `DismissRestoreButton` | `Dismiss the restore offer` | `Close` | the tooltip, by the peer's fallback | none |

- Method 2, after step 4 (`u3a`): the resolver of `notes.txt` in the graph area, spanning the right
  panel's column (phase 4's design 11.2), as phase 4's C10 shows it after its step 5 (two blocks, at
  lines 2 and 10); the banner above it; no stage panel beside it.
- Method 2, after step 5: the five names, tooltips, help texts and keys of the table; each a Button in
  UI Automation; `AssertNamed` reports nothing.
- Method 2, after step 6: after Esc the resolver still shows; after the click on `ResolverCloseButton`
  no resolver; `ExactStateAsync()` unchanged (`notes.txt` still both modified, `.git/MERGE_HEAD` as
  before, `u-untracked.txt` untracked).
- Method 3, after step 8: the table's rows for the two unstaged rows, `CloseDiffButton`, the activity
  log's two, the three splitters and the two eyes; the two `alpha` rows' help texts, each the ref's
  full name (`RefItemRow.ToolTip`; D456 (3)): `feature/alpha` and `origin/feature/alpha`, and neither
  row's button with a tooltip (`ToolTip.Tip` null), so nothing drawn changes; nothing else of the
  window changed since `u2a`.
- Method 3, after step 9: `ff` soloed: its row shows `RefSoloToggle`; the hidden `alpha` rows keep their
  eyes (in solo mode a hidden branch stays marked hidden).
- Method 3, after step 10 (`u3b`): `src/core.txt`'s row hovered (its `RowUnstageButton` drawn, its tooltip
  open), README.md's row still selected; `ff` with its solo button, every other branch's name dimmed but
  HEAD's `main` (phase 5's 8.3: HEAD's branch is always shown), the two `alpha` rows with their eyes.
  The names of the table for `RowUnstageButton`, `RefSoloToggle` and the two `RefHiddenToggle`s.
  `AssertNamed` reports nothing (the left panel's `RefItem`s exempt from `duplicate`, open point 81; the
  toggles unique by their full names, open point 75). No text cut.
- Method 3, after step 11: the confirmation's title `Discard changes?` and `ConfirmButton` `Discard`
  (class `danger`), as part L's L5 shows it for its file; after Discard, the restore bar
  (`RestoreBar`) with `RestoreBarText` `Discarded changes to README.md.`, `RestoreDiscardButton`
  `Restore` and `DismissRestoreButton` with the table's name and tooltip; README.md no longer in the
  unstaged list. `AssertNamed` reports nothing, as after step 10.
- Method 3, after step 12: `ExactStateAsync()` differs from step 1's only in README.md's entry (the
  file as committed, no longer modified) and one new ref under `refs/visualcommit/backup/`, whose
  commit holds README.md as it was (D67); `src/core.txt` still staged, `u-untracked.txt` still untracked.

#### The Narrator walk-through by hand (part U)

The owner, at the gate, about 20 minutes (D411, D450; design 9.3.9, 13.3); recorded under "Checks by
hand". The lead starts the app by the PowerShell line with CLAUDE.md's isolating variables and
`VISUALCOMMIT_TIME_ZONE=UTC`, on a data folder whose session holds two tabs: a copy of "ops" with
README.md changed, active, and second a copy of the stopped merge
(`Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`, as launch 13's; open point 85). Narrator:
Ctrl+Win+Enter. For each step the owner writes "as expected", or the words heard: a step whose words
differ is written down as heard. `{date}` is the Date column's own (`yyyy-MM-dd HH:mm`), in UTC, which
the start line sets, so that the dates heard are 9.3.5's (open point 85). Expected, step by step:

1. The app in front: its title, `VisualCommit` (the window's title, 7.3).
2. F6 until the graph has the focus: "Commit graph, list" alone (the focus on `GraphRows`, ruling 35).
   A tab opens with no row selected (the right panel's placeholder), and while no row is selected the
   graph exposes and speaks no row (9.3.2, 9.3.4; open point 86).
3. Down: each next row's name, once, its subject, author, date and refs (9.3.5). From no selection the
   first Down selects the working-changes row, "Working changes"; then "Stash: On main: Experiment,
   {date}, stash@{0}"; "Conflicting change, Test Author, {date}, branch feature/conflict"; "Fast-forward
   me, Test Author, {date}, branch feature/ff".
4. HEAD's row (one more Down): "Main work, Test Author, {date}, current branch main, remote branch
   origin/main, tag v1.1".
5. Home, End, PageDown: each landing row, once: Home "Working changes"; End "Initial commit, Test Author,
   {date}"; PageDown on the last row moves nothing, so nothing new is heard (9.3.4: the same row raises
   nothing; open point 86).
6. Up until "Main work" is heard, then Shift+Down twice: "Alpha two, Test Author, {date}, branch
   feature/alpha, remote branch origin/feature/alpha", then "Alpha one, Test Author, {date}". Is "3
   commits selected" heard (the hint `Commit dff8fbf, 3 commits selected`; 9.9 item 3)? Note it.
7. Down: one row, no count: "Add core, Test Author, {date}, tag v1.0".
8. Up to "Working changes": each row once, ending "Working changes".
9. Shift+F10: the menu's first item, `Stage all` (the working-changes row's menu, headed `Working
   changes`, its first item highlighted, D105); Esc: back on the graph.
10. A branch clicked in the left panel (`Local branches › feature` opened, then `alpha`): its commit's
    row, "Alpha two, Test Author, {date}, branch feature/alpha, remote branch origin/feature/alpha",
    though the focus stays on the left panel's row (9.3.4). Narrator may also read the focused branch
    row, "alpha, button", now with its help text, the ref's full name, "feature/alpha" (D456 (3)); and
    as the folder opens it may name a row "Avalonia.Controls.DockPanel" for a moment (the spike's S4;
    open point 93). Whatever is heard is written down as heard.
11. F6 to the right panel, twice from the left panel: the first F6 gives the graph, "Commit graph,
    list" and Alpha two's row once more (9.3.4: the graph takes the focus with a row exposed; Narrator
    may read the List's selection, or the row twice, 9.9 item 2: note which); the second puts the focus
    on `FileList`'s first row, the first changed file's row, Alpha two's one changed file (phase 6's
    design 6.6, the mark `FileList`; phase 6's K12 after its step 13). That row is a `ChangedFile`
    button named by the file's own name, not its path: "alpha.txt, button" (`ChangedFileRow.Name`; 1.12
    and 9.3.6 rename no file row). Its path, `src/alpha.txt`, is the row's tooltip
    (`ChangedFileRow.ToolTip`), which its peer gives as its help text (U3's fallback): if Narrator reads
    the path, note where. No Tab: Alpha two changed one file, and Tab would leave the list (open point
    86).
12. Ctrl+Shift+M (the commit message), Shift+Tab to README.md's row ("README.md, button"), then Tab:
    "Stage README.md, button" (D425: Tab from a row's own button reaches its Stage).
13. The status bar's toggle: "Activity log, button, Ctrl+J".
14. Ctrl+Shift+F: the result, `Fetched from all remotes` (`OperationStatus`, a polite live region, 9.3.7).
    Are the progress steps read (`Fetching from all remotes…`, git's percentage; 9.9 item 4)? Note how
    many.
15. Ctrl+F, a word (9.3.9 names none; this report takes `alpha`, which matches Alpha one and Alpha two,
    the message search ignoring case; open point 86), Enter: the match's row, its name and ", matches
    the search" if its hint is read; the focus stays in `SearchBox`.
16. Ctrl+Shift+P, "fet": the entries whose words hold "fet", each label with its key where it has one.
17. Ctrl+,, Down to About, Enter, then Narrator+W: About's texts, the owner's name as `AboutCreator`
    reads it and the version as `AboutVersion` reads it (part A; D398, D409).
18. The resolver (the stopped merge's tab, the conflicted file opened from the stage panel): "Next
    conflict, button, Alt+Down".
19. Narrator+Tab on the graph: "Commit graph, list" (ruling 35: the focus is the graph, never the row).
20. After step 19 the owner closes VisualCommit by its window (Alt+F4), and the lead waits until the
    window has gone. The lead then makes the second start: the same start line with
    `VISUALCOMMIT_GIT_STAND_IN=none` added beside the isolating variables and
    `VISUALCOMMIT_TIME_ZONE=UTC` (1.14, 13.4 item 16), on the same data folder, which nothing holds
    now. Its two tabs come back, each `New tab` on the first-run screen, as part F's F1 shows a
    restored tab whose git is missing (5.3, "Place"). A start on that folder while the first window is
    still open would only bring that window forward and exit with code 0 (D421; design 4.3.1), and
    show no first-run screen (open point 85). The first-run screen shows: the state's title,
    "VisualCommit needs Git", and its body,
    "VisualCommit works through the Git installed on this computer, and it found none: not on the PATH,
    nor in Git for Windows' usual install folders." (5.3; part F's F1).
21. Tab to "Check again", Enter: its result is spoken (no Git found again, the focus back on "Check
    again"): while the search runs the button reads "Checking…", and once it ends "Check again, button",
    the focus given back (5.3; open point 86).

Silence where a row's name is expected (step 3's rows, step 5's Home and End) means that the live
region does not reach Narrator (9.9 item 1): a finding that reopens the route, by a decision and a
gate round. The silences this text expects (no row after step 2's "Commit graph, list", none being
selected; step 5's PageDown on the last row) are no finding. Before the gate the owner makes a short
listen, steps 2 to 6 and 14 (about 5 minutes; 13.3, 13.4 item 13), so that a change of route lands
before the review: D455 brought it forward from §9's merge to the first build the owner can hear,
and the owner was asked after the spike and again at §9's merge (status.md, "Waiting on the owner",
names its start file). Its words go into status.md, and the report quotes them here once the listen
is held (open point 89).

#### Texts these checks rely on (part U)

New in 8a (§9): the names of 1.12's table, word for word, with the formats `Stage {0}`, `Discard {0}`,
`Unstage {0}` (`StageFileFormat`, `DiscardFileFormat`, `UnstageFileFormat`, over `ChangedFileRow.Key`),
`Show {0} in the graph` and `Stop soloing {0}` (`ShowRefFormat`, `StopSoloingRefFormat`, over the ref's
full name, the string the row's tooltip shows for a branch or a tag, `RefItemRow.ToolTip`, or a
property of its own that holds the same; open point 75), constants of `AutomationNames` (9.3.6). The
row's words, public constants of `GraphRowSpeech` (9.3.5): a commit `{subject}, {author}, {date}`, then
`, {label}` per label in drawing order: `current branch {name}`, `branch {name}`, `remote branch
{name}`, `tag {name}`, a stash's own text (`stash@{0}`), `HEAD, detached`; a label with an open
request ` (pull request #12: Add the login form)`, ` (draft pull request
#2: …)`, ` (merge request !21: …)`, Azure DevOps' ` (pull request !21: …)` (no check shows them: the
providers are off); `No message`; `Stash: {subject}, {date}`; the working-changes row's text with ` · `
read as `, `, else `Working changes`; the hint `Commit {short SHA}`, then `, {n} commits selected` while n
≥ 2, then `, matches the search`. The graph's peer keeps today's name `Commit graph` and id `GraphRows`;
the row's id `GraphSelectedRow`, class `CommitGraphRow` (9.3.3). The sweep's rules `empty`, `type name`,
`key text`, `duplicate`, and `AutomationProblem`'s fields (1.12). The branch rows' help text, the
ref's full name (`RefItemRow.ToolTip`; D456 (3)): `feature/alpha` and `origin/feature/alpha` for the
two `alpha` rows (`Phase8aExpectations.U.RefRowHelpTexts`).

Today's texts the steps pass through, as the code has them on `d2318ba`: the tooltips, help texts and
keys of U3's table (where a control sets no `AutomationProperties.HelpText`, its help text as a client
reads it is its tooltip, by the peer's fallback); the multi-selection panel's `3 commits selected`
(`MultiSelectionViewModel`); the stage panel's `Unstaged files (1)`, `Unstaged files (2)`, `Staged
files (0)`, `Staged files (1)`; the stage rows' tooltips (`ChangedFileRow.RowToolTip`: the path; on a conflicted row `Conflict: click to
resolve it`); the menus' `Hide feature/alpha`, `Solo feature/ff`, `Compare feature/conflict with main`
(`OperationTexts.MenuTexts`, over the branch's name), the working-changes row's menu `Working changes`
with `Stage all`; the confirmation `Discard changes?` with `Discard`; the restore bar's `Discarded
changes to README.md.` and `Restore`; the status bar's `Fetching from all remotes…` and `Fetched from
all remotes` (the walk-through's step 14); the search bar's mode `Commits`; the comparison's ends
`From main` and `To feature/conflict` (`CompareViewModel.FromText` and `ToText`), which method 3's
step 3 reads.

The walk-through's words are the texts above, part F's first-run texts (5.3) and part A's About.

#### The real window (part U)

One launch (12.3.4 launch 13; 12.5: of part U's three checks, U1 and U3 repeated, no capture, no
twin), `Check_U_graph_row_and_names` in `Phase8aRealWindowPass.U.cs`, the thirteenth and last method of
`Phase8aOrder`. It reads `RealApp.HighContrastIsOn()` first (12.3.1), starts on a new data folder D
whose session holds one tab on the scenario's copy, dark (`RealApp.Launch(D, session)`), sized 1100×700
(`Sized`; 200 % on the laptop), the copy declared before the app; it waits on named conditions with a
limit; it takes no capture; run.txt gets its start, its end, every event and every name it reads
(D445); and it ends with the app closed by its window, exit code 0. It presses nothing of 12.3.2's list.
Before the launch, `u-untracked.txt` is written into the work tree of a copy of
`Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)` (its name and content `Phase8aExpectations.U`'s,
as method 2 writes it).

13. `Check_U_graph_row_and_names` (method 2 is the scripted counterpart of its rows and resolver; no
    twin). Wait until the graph shows its rows below the merge's banner.
    - **The row.** A real click on display row 1, "Ours edits" (`GraphRowCentreY(1)`, which takes the
      banner into account); wait until UI Automation's focused element is `GraphRows`. Read
      `GraphSelectedRow`, the graph's first child, once (its node made; D455), its name `Ours edits,
      Test Author, 2026-01-01 12:09, current branch main`; then `RecordSelectedRowEvents()`, which
      queues each Name change and LiveRegionChanged on `GraphRows`' subtree (kind, id, time).
      Real Up, then real Down twice, one at a time. After each, within 5 s, a LiveRegionChanged from
      `GraphSelectedRow` (D445 as D450 amends it): a Name change alone fails the method and is written
      to run.txt (D450; open point 87). Then
      `GraphSelectedRow`'s name and help text are method 2's table's (`Phase8aExpectations.U`): after Up
      `Working changes, 1 conflict` and an empty help text; after the first Down `Ours edits, Test
      Author, 2026-01-01 12:09, current branch main`, `Commit da31c97`; after the second `Kinds edits,
      Test Author, 2026-01-01 12:08, branch feature/kinds`, `Commit 3a9de3e`. Each time: control type
      ListItem; `GraphRows`' Selection pattern holds one element, `GraphSelectedRow`; the row's bounding
      rectangle `WindowLayout.RowHeight` ± 1 high (26 ± 1, in `GraphRowsBounds()`'s units; open point
      87) and inside `GraphRowsBounds()`; UI Automation's focused element still `GraphRows`, never
      `GraphSelectedRow` (ruling 35). run.txt: each move's events by kind, id and time, and their counts.
    - **The names** (1.12), each read through UI Automation, asserted equal to U3's table's name and
      written to run.txt with its `AcceleratorKey`:
      - the resolver's five: a real click on display row 0, the working-changes row (open point 87), and,
        once the stage panel lists it, a real double-click on `notes.txt`'s row (phase 4's way); once
        the resolver shows, `ResolverPreviousConflict`, `ResolverNextConflict`, `ResolverPreviousFile`,
        `ResolverNextFile`, `ResolverCloseButton`; a real click on `ResolverCloseButton`, and a wait
        until the resolver has closed (a real Esc would leave it open: the double-click leaves the
        focus in its result, phase 4's design 11.10; the lead at §9's merge);
      - a real Ctrl+J; once the activity log shows, `ActivityLogToggle` `Activity log`,
        `ActivityLogClose` `Close the activity log`, and the three splitters, `Resize the left panel`,
        `Resize the right panel`, `Resize the activity log`;
      - a real Ctrl+F; once the search bar shows, `SearchPrevious` `Previous match`, `SearchNext` `Next
        match`, `SearchClose` `Close the search`; a real Esc, and a wait until it has closed;
      - the real pointer onto `u-untracked.txt`'s row in the unstaged list; once its buttons are in the
        window, `Stage u-untracked.txt` and `Discard u-untracked.txt`; a real click on that
        `RowStageButton`; once the staged list holds `u-untracked.txt`, the real pointer onto its row:
        `Unstage u-untracked.txt`. A row that holds the keyboard focus has its buttons in UI Automation
        too (D425; 12.3.5), and a row's buttons are siblings of its `StageFileRow` there, not its
        descendants (`WorkingChangesView.axaml`: the row's `Panel` holds the `StageFileRow` and the
        `rowActions` panel side by side). So each button is found by its id among the shown elements of
        that id (`ShownElements`), as the one whose bounds lie within the vertical extent of the hovered
        row's `StageFileRow` (`RealApp.StageFileRow(list, path)`), never by its name; its Name is then
        asserted equal to the table's and written to run.txt with its `AcceleratorKey`, and a failure
        quotes the name read.
    - The app closed by its window, exit code 0.

The spike (9.3.1; D455), a FlaUI UIA3 client on the Release build of §9's first stage, on "ops" at
200 %, saw no event from `GraphSelectedRow` while it had read nothing below the graph; once it had
read the graph's first child, its handler received one Name change and one LiveRegionChanged from the
row per move, which is why the launch reads that child first. Unverified until D246's runs (12.9):
this launch's own run, on the stopped merge and the merged code (its events and their counts in
run.txt; a LiveRegionChanged asserted per move, ruling 32 and D450; the scripted run asserts one Name
change), and the row's bounds under per-monitor DPI at 200 %, which the spike did not record.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Each check is named by its part's letter
and its place in its part's design list (2.5 for part L, 4.6 for part I, 5.5 for part F, 6.5 for part K,
7.5 for part B, 8.5 for part A, 9.5 for part U). Its steps and expected results are in its part's
section above, its methods and starts in the part's methods table. "R" in Notes marks the steps the
real-window pass repeats (design 2.5, 4.6, 5.5, 6.5, 7.5, 8.5, 9.5, 12.3.4, 12.5; part B's are steps of
its own, with no scripted twin); "Real window" says "n/a" for a check the pass does not repeat.
"Screenshots" lists the planned pictures, the scripted run's and then the real window's.

| # | Check | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|
| L1 | Placeholders in their own token, dark: `Filter`, `Summary`, `Description` `#8F97A4` on `#272C36`, the counter `#9BA3B0`, typed `#E4E7EC` (D410, D418); "changes", dark, 1100×700 | Not run yet | Not run yet | `l1a-placeholders-dark`, `l1b-filter-typed-dark`; real window the same two | R: all of it but the focused empty box and the counter (launch 1). Method 1 |
| L2 | Placeholders in their own token, light: `#68707D` on `#FFFFFF`, the counter `#5C6572`, typed `#1B1F27`; "changes", dark switched to light, 1100×700 | Not run yet | Not run yet | `l2a-placeholders-light`, `l2b-filter-typed-light`; real window the same two | R: all of it but the focused empty box and the counter (launch 1, after a real click on `ThemeSwitch`). Method 1 |
| L3 | The placeholder in every other kind of box: the palette, the search bar, the Pick dialog, the Name dialog, the Remotes form, the settings screen's name and email, each `#8F97A4` on `#272C36`; "graph", dark, 1100×700 | Not run yet | n/a | `l3a-palette-placeholder`, `l3b-search-placeholder`, `l3c-pick-filter-placeholder`, `l3d-branch-name-placeholder`, `l3e-remote-form-placeholders`, `l3f-settings-name-placeholder` | Not R. Method 3. `l3a` changes at §8's and §11's merges (the palette's scroll thumb), `l3f` at §5's (the Git program row) and §8's (the About nav button): design 0.7 |
| L4 | Danger text `#EA7D86`: a menu item and a palette row highlighted, a "D" hovered and selected, the Remotes dialog's "Remove" at rest and hovered; "graph", dark, 1100×700 | Not run yet | Not run yet | `l4a-delete-item-menu`, `l4b-delete-entry-palette`, `l4c-deleted-file-hovered`, `l4d-deleted-file-selected`, `l4e-remove-button-at-rest`, `l4f-remove-button-hovered`; real window `l4a-delete-item-menu` | R: a, the menu by a real `Shift+F10` and Down (launch 2). Methods 2 (a) and 4 (b to d); open point 2 |
| L5 | The danger button: dark `#EA7D86` with the label `#10121A`, light `#B7323D` with white kept; Cancel focused, nothing discarded; "changes", 1100×700 | Not run yet | Not run yet | `l5a-discard-dialog-dark`, `l5b-discard-dialog-light`; real window `l5a-discard-dialog-dark` | R: dark (launch 1). Method 1 |
| L6 | The accent on hovered and selected rows: HEAD's `main` in the left panel, the "R" in the commit details, the comparison, the file history and the stage panel, `#8B9AFF` hovered or selected and `#7C8CFF` idle, light `#4353D8` always; "graph" and "changes", 1100×700 | Not run yet | Not run yet | `l6a-head-idle`, `l6b-head-hovered`, `l6c-head-selected`, `l6d-renamed-idle`, `l6e-renamed-hovered`, `l6f-renamed-selected`, `l6g-compare-renamed-selected`, `l6h-history-renamed-selected`, `l6i-staged-renamed-hovered`, `l6j-staged-renamed-selected`, `l6k-head-idle-light`, `l6l-head-hovered-light`, `l6m-head-selected-light`; real window `l6a`, `l6b`, `l6c` | R: a to c, `main` by the real mouse (launch 2). Methods 2, 5, 6, 7; one picture per state (design 2.5) |
| I1 | A second start on the same data folder hands over: it exits 0 within 10 s, the minimised first comes back, both log lines, `session.json` and `instance.json` unchanged (D421, D448); one empty tab, dark, 1100×700 | n/a: real window only (no harness runs `Program.Main`) | Not run yet | none | Real window only (launch 4, with a second process). By hand at the gate: a second start behind another window (part I's conventions) |
| I2 | The notice, dark, on Windows: heading, folder line, body, sentence, Close and Try again focused; `Trying…`; `It still did not answer.`; Esc closes (D421, D422, D448); the notice, 480 wide | Not run yet | Not run yet | `i2a-notice-dark`, `i2b-notice-trying`, `i2c-notice-no-answer`; real window the same three, masked | R: the three pictures with the folder line masked, exit code 2 after Close (launch 5), and Try again starting the app once the lock is freed (launch 6); Try again pressed by a real Enter in both, after asserting that the notice is the foreground window. Method 1 |
| I3 | The notice, light, on Linux, by Tab, Shift+Tab and Space; the notice, 480 wide | Not run yet | n/a | `i3a-notice-light` | Not R. Method 2 |
| F1 | Git missing on Windows, winget found: `VisualCommit needs Git`, its body and guidance, `Install with winget…`, `Download Git for Windows`, `Choose git.exe…`, `Check again`, `How VisualCommit finds Git`; `Git not found`, `Set up Git…`; the restored tab `New tab`; no git call (R4, D405, D423, D453); one restored tab on "graph", 1100×700, dark and light | Not run yet | Not run yet | `f1a-missing-windows`, `f1b-missing-windows-light`; real window `f1a-missing-windows` | R: dark (launch 7, the stand-in `none`, winget looked for on the machine's PATH first). Methods 1 (dark) and 2 (light) |
| F2 | The winget offer: the confirm `Install Git with winget?` with git-scm.com's line, `Cancel` focused; Cancel starts nothing; Install starts `cmd.exe /S /K` with winget's full path, in its folder, in a terminal of its own; Download opens `https://git-scm.com/install/windows` (D449, D453); fakes only | Not run yet | Not run yet | `f2a-winget-confirm`; real window `f2a-winget-confirm` | R: the confirm and its Cancel, then no `Started in a terminal:` line and no `cmd.exe` or `winget.exe` child of the app (launch 7, 12.3.2); Install and Download never in the real window. Methods 1 (confirm, Cancel) and 2 (Install, Download) |
| F3 | A chosen git that is gone: `The chosen Git is gone`, its path, `VisualCommit found another Git at …`, `Use the Git it found`; `Chosen Git not found`; the button removes `gitExecutable`, searches again and the screen goes (ruling 41, D423); one empty tab, dark | Not run yet | n/a | `f3a-chosen-gone` | Not R. Method 3 |
| F4 | Found again by "Check again": `Checking…`, then the restored tab reopened with its graph, `Git {v}` with `… found on the PATH`, `Set up Git…` gone, the merge tools asked again (D423, ruling 5); one restored tab on "graph", dark | Not run yet | Not run yet | `f4a-found-again`; real window `f4a-found-again` | R: through Choose and the native Open dialog, git by D446 (launch 7): `gitExecutable` written, the tooltip `{git}, chosen in Settings`. Method 1 |
| F5 | Too old on Windows: `Git 2.29.0 is too old`, `{min}` from `GitVersion.Minimum`, the path, `Download Git for Windows`, `Update with winget…`; the confirm `Update Git with winget?` with `Update`; Cancel starts nothing (D453); one empty tab, dark | Not run yet | Not run yet | `f5a-too-old`, `f5b-winget-update-confirm`; real window `f5a-too-old` | R: to `f5a`, through the stand-in at `git version 2.29.0` (launch 8); the confirm is scripted only; needs winget on the gate's machine (D449, D453). Method 4 |
| F6 | A broken configuration: `Git is not working`, git's own `fatal: bad config line 1 in file C:/Users/demo/.gitconfig`, `Git cannot read line 1 of …`, `Show the file` revealing it, `Download Git for Windows` (D405, D453); F6b: real git with `[user` in the session's global file names that file (plan 767); one empty tab, dark | Not run yet | Not run yet | `f6a-config-fault`; real window `f6a-config-fault`; F6b none | R: F6 through the stand-in's failure text (launch 9), F6b through real git (launch 10); `Download Git for Windows` and `Show the file` never pressed. Methods 5 (F6) and 6 (F6b, on every CI job, the floor's git 2.34.1 among them) |
| F7 | Git that cannot be started: `… but it could not be started: {message}`, `Repair or reinstall Git for Windows, then press Check again.`, `Download Git for Windows`; one empty tab, dark | Not run yet | n/a | `f7a-not-started` | Not R. Method 7 |
| F8 | "Choose git.exe…": a too-old file refused, `Git 2.29.0 at this path is too old: VisualCommit needs {min} or newer.`, `settings.json` untouched; then a working file written as `gitExecutable`, searched again and the screen gone (ruling 4, 1.4); Missing without winget, dark | Not run yet | n/a | `f8a-choose-too-old` | Not R. Method 8 |
| F9 | macOS: the Command Line Tools missing, `Install the Command Line Tools` starts `/usr/bin/xcode-select --install` with no confirm, Copy gives `brew install git`; then `xcrun: error`, `Apple's Command Line Tools look damaged…` with `sudo xcode-select --switch /Library/Developer/CommandLineTools` (ruling 45); one empty tab, dark | Not run yet | n/a | `f9a-macos-missing`, `f9b-macos-broken` | Not R; a Mac by hand is 8b's checklist (5.1, 5.9). Method 9 |
| F10 | Linux: Ubuntu missing, `On Ubuntu 22.04.5 LTS:` `sudo apt install git` with Copy; Alpine missing, the generic text, no command; Ubuntu too old, the generic text, no command or Copy, `Other ways to install Git` (D449); one empty tab, dark | Not run yet | n/a | `f10a-ubuntu-missing`, `f10b-alpine-missing`, `f10c-ubuntu-too-old` | Not R. Methods 10, 11 and 12 |
| F11 | The askpass helper missing: `Part of VisualCommit is missing`, the path looked at, `Continue without it`, `What the helper does`; `Git {v} (helper missing)`, `Details…`; Continue shows the welcome page, `Details…` brings the screen back (rulings 9, 43); one empty tab, dark | Not run yet | n/a: scripted only (ruling 43) | `f11a-helper-missing` | Not R. Method 13 |
| F12 | The settings screen's `Git program` row, last in the global scope, never in `This repository`: `Found on the PATH.`; Browse, `Chosen here. VisualCommit uses it from its next start.`, Save writes `gitExecutable` and changes no git in use; `Use the Git on the PATH` removes it; a too-old file refused (rulings 40, 42); "graph", dark | Not run yet | n/a | `f12a-row-next-start`, `f12b-row-too-old` | Not R. Method 14. `f12a` and `f12b` change at §8's merge in the About button's region (x 140–317, y 255–285 at 1100×700; open point 67): design 0.7 |
| F13 | The screen by keys: F6 to `Install with winget…`, Tab to `Download Git for Windows`, `Choose git.exe…` and `Check again`, Enter searches again and `Check again` keeps the focus with its outline (5.3, keyboard.md D-a); one restored tab on "graph", dark | Not run yet | Not run yet | none | R: launch 7, each Tab's focus asserted before the next key, Enter only on `GitSetupCheckAgainButton`. Method 1 |
| F14 | The netrc note: signed in with "Use for git over HTTPS", git 2.43.0: no file, no note; `_netrc` in the session's home, `Your home folder has a .netrc or _netrc file. With Git 2.43.0, …` in `#E0A23B`, `Dismiss`; dismissed for the session (ruling 8, D424); one empty tab, dark | Not run yet | n/a | `f14a-netrc-note` | Not R. Method 15. `f14a` changes at §8's merge in the About button's region (x 140–317, y 255–285 at 1100×700; open point 67): design 0.7 |
| K1 | A hunk and chosen lines by the keys: F6 and a walk of Tabs to `src/Calculator.cs`'s row, Enter, Tab to the first `Stage hunk` (framed), Enter stages that hunk alone and leaves the focus in the text without a frame, Shift+Tab to `Discard file`; Shift with the arrows selects two added lines and Shift+F10's `Stage selected lines` stages them (D426); Tab and Shift+Tab to `Side by side`, Enter selects it (scripted only); "changes", dark, 1100×700 | Not run yet | Not run yet | `k1a-hunk-button`, `k1b-hunk-staged`, `k1c-toolbar`, `k1d-lines-menu`; real window the same four | R: all of it but step 11, which is scripted only (launch 11). Method 1. Open points 30, 31 and 40 |
| K2 | A blame run picked by the arrows: three Downs pick run 2 (`0575c7f`), its gutter `#DDE1FA`, its commit in the right panel; Shift+F10 its menu; light, "history", 1100×700 | Not run yet | Not run yet | `k2a-blame-run`, `k2b-blame-menu`; real window the same two | R: all of it (launch 12; each Down waited on by the right panel, not the caret). Method 2. Open points 32 and 41 |
| K3 | "Cancel Fetch origin" first in the palette while a held fetch runs, no key, no detail; Enter cancels it: `Fetch origin cancelled`, no remote ref moved; listed only while it applies (D417, D452); "graph", dark, 1100×700 | Not run yet | n/a | `k3a-palette-cancel` | Not R. Method 3. Open points 36 and 42. `k3a` changes at §8's and §11's merges in the palette's scroll thumb (x 837–838 at 1100×700; ruling 54, D417), as `l3a-palette-placeholder` does: design 0.7 |
| K4 | A stage row's buttons by Tab: F6 leaves them undrawn (`k12b`'s state), Tab draws and frames `Stage`, Enter stages `README.md` and moves the focus to the row now in its place (D425); "changes", dark, 1100×700 | Not run yet | n/a | `k4a-row-stage`, `k4b-row-staged` | Not R. Method 4 |
| K5 | The focus after "Choose": `From GitHub…` by Shift+Tab and Enter, `octo/hello` by Tab, Home and Enter; the form filled, `From GitHub…` focused without a frame (F5-A), no `clone` call; one empty tab, the fake of GitHub serving a copy of 7a's "hosting" on a free port, dark, 1100×700 | Not run yet | n/a | `k5a-chosen` | Not R. Method 5. Inspected against phase 7a's `b06a-chosen`, the URL's port aside. Open points 33 and 43 |
| K6 | A held `diff-files` read: Ctrl+Shift+M before the release, and the summary keeps the focus and its focused border `#7C8CFF` (its template's `PART_BorderElement`, and the left-border pixel) once the diff shows; "changes", dark, 1100×700 | Not run yet | n/a | `k6a-summary-kept` | Not R. Method 6. Its picture depends on `HeldGitRunner` holding the tab's calls (6.4) |
| K7 | A held commit: `CommitButton` `Committing…`, Amend disabled, the status bar `Ready`, the summary still editable and focused; `Next` typed meanwhile stays, HEAD's message `First` (D392); "changes", dark, 1100×700 | Not run yet | n/a | `k7a-committing`, `k7b-committed` | Not R. Method 7. Its pictures depend on `HeldGitRunner` holding the tab's calls (6.4). Open points 34 and 40 |
| K8 | The walks: from the right panel Tab reaches the status bar's Cancel within 200 Tabs, Enter cancels a second held fetch; Tab reaches `LeftSplitter`, Right widens the left panel by 10, to 270, remembered in the session (D427); "graph", dark, 1100×700 | Not run yet | n/a | `k8a-splitter` | Not R. Method 3, after K3; the hold installed with `rounds: 300` and released after each cancel. The two walks' numbers of Tabs go here at the gate. Open points 35 and 42 |
| B1 | The icon at its sizes, dark: the `.ico`'s 14 frames (16 to 256) and nine generated PNGs, each at its own pixel size on `#14161B`, its centre its file's own, its top-left pixel `#14161B`, the gaps `#14161B`; the owner's mark, Lens, the same at every size (D407, D428 to D430, D447); the sheet, 1100×700 | Not run yet | Not run yet | `b1-icon-sizes-dark`; real window `b1-title-bar-dark-with-frame`, `b1-title-bar-icon-dark` (no twin, inspected) | R: steps of its own, not a repeat of the sheet: (a) the executable's icon and (b) the window's two icons against the `.ico`'s frames, within 2 per channel; (c) the title bar, dark (launch 3, with part A). Method 1 |
| B2 | The icon at its sizes, light: the same on `#FFFFFF`; the sheet, 1100×700 | Not run yet | Not run yet | `b2-icon-sizes-light`; real window `b2-title-bar-light-with-frame`, `b2-title-bar-icon-light`, `b2-taskbar-button`, `b2-taskbar-button-enlarged` (no twin, inspected) | R: after a real click on `ThemeSwitch`, the light title bar and the taskbar button (launch 3). No button found within 10 s: that step "Not run", the button checked by hand at the gate. Method 2 |
| A1 | About from the palette, dark: one row `About VisualCommit…`, no key, no detail; six section buttons, `About` last at (141, 255), selected and focused; the icon, `VisualCommit`, `Created by Rahim Khajei`, `0.1.0 (1a2b3c4)`, `Copyright (c) 2026 Rahim Khajei`, `MIT License` with `Show license…`, the notices' note with `Show third-party notices…`, `github.com/RahimPasha/VisualCommit`, `Open the user guide`; no scroll, nothing clipped, no `@`; Tab to the repository link, underlined, Enter opens exactly `https://github.com/RahimPasha/VisualCommit` (D398, D409, D417, D431); one empty tab, dark, 1100×700 | Not run yet | Not run yet | `a1a-about-dark`, `a1b-about-link-focus`; real window `a1a-about-dark`, and `a3a-about-1920` where 1920×1080 fits | R: steps 1 to 3 by real keys, the highlighted row read before Enter, `AboutVersion`'s name the built version, ending with ` (`, seven hexadecimal digits and `)`, and a second capture at 1920×1080 where `app.Fits(1920, 1080)`, compared with A3's picture, otherwise "Not run" for that capture with run.txt's line (launch 3, with part B); no link pressed. Method 1. Open points 56, 57, 64, 65, 72 |
| A2 | About in light: A1's texts, the links `#4353D8` on `#F5F6F8`, the secondary texts `#5C6572` (D409); one empty tab, light, 1100×700 | Not run yet | n/a | `a2a-about-light` | Not R. Method 2 |
| A3 | About at 1920×1080 by `Ctrl+,`, five Downs and Enter: `About` at (551, 415), selected and focused, the section whole (D409); one empty tab, dark | Not run yet | n/a | `a3a-about-1920` | Not R: its picture is the scripted twin of A1's real-window capture at 1920×1080 where it fits (launch 3). Method 3. Open points 56, 64, 71 |
| A4 | The license: `Show license…` by Tab and Enter; over the dimmed screen `MIT License`, no switch, `LICENSE`'s 21 lines in view, `Copy all` left, `Close` right, the focus in the text; Copy all puts `LICENSE` (`\n`) on the clipboard; Esc gives the focus back to `Show license…` (D432); one empty tab, dark, 1100×700 | Not run yet | Not run yet | `a4a-license`; real window `a4a-license` | R: Tab, a real Enter, Esc (launch 3); Copy all never pressed in the real window. Method 1, after A1's step 3. Open points 58, 59, 66 |
| A5 | The third-party notices: within 1 s the title, five switch buttons in one row, the first selected, `THIRD-PARTY-NOTICES.md` from its top; End shows its last line; `Avalonia` shows `notices/Avalonia-NOTICE.md` from its top; Close gives the focus back (D431, D432); one empty tab, dark, 1100×700 | Not run yet | n/a | `a5a-notices-top`, `a5b-notices-end`, `a5c-notices-avalonia` | Not R. Method 4. The measured time goes here. Open points 60, 61, 70 |
| A6 | "About VisualCommit…" on a repository tab: one row, highlighted, no key, no detail; Enter opens About; nothing in the repository changes (D417, ruling 54); "graph", dark, 1100×700 | Not run yet | n/a | `a6a-palette-about` | Not R. Method 5. `a6a` shows the box's caret, which varies by nature |
| U1 | The graph's selected row in UI Automation: `GraphRows` a List with one child, the ListItem `GraphSelectedRow`, named by its subject, author, date and refs (`Main work, Test Author, 2026-01-01 12:04, current branch main, remote branch origin/main, tag v1.1`; `Stash: On main: Experiment, …, stash@{0}`; `Working changes`), its help text `Commit {short SHA}` with `, 3 commits selected` on the focus row, live setting Polite; one Name change per move (Down, Up, Shift+Down twice, Up through the stash to the working-changes row, a branch clicked in the left panel); the focus on `GraphRows`, on `RefItem` after the click; the peer changes no pixel; the stopped merge's working row and next two rows (D411, D435, D450, D49); "ops" with README.md changed and the stopped merge, dark, 1100×700 | Not run yet | Not run yet | `u1a-graph-row-head`, `u1b-graph-rows-three-selected`, `u1c-graph-working-row`; real window none | R: on the stopped merge with `u-untracked.txt` (launch 13): real Up and Down twice, each raising a LiveRegionChanged from `GraphSelectedRow` within 5 s, a Name change alone failing (D450); no capture. Methods 1 and 2. Open points 76, 77 and 78 |
| U2 | The sweep: `AccessibleNameSweepTests` reports nothing in its 14 states (the first-run screen's name and help text its title and body in states 10 to 13); the busiest state of the ops tab, the activity log, a selected and a hovered stage row, two hidden branches, swept with nothing reported (D411, D435); "ops" with three changes, dark, 1100×700 | Not run yet | n/a | `u2a-sweep-ops-tab` | Not R. Method 3 and `AccessibleNameSweepTests`; the first run's findings, on the code before, listed under U2 at §9's merge. Open points 79, 80, 81 and 90 |
| U3 | The names of 1.12's table with every tooltip, help text and key as before: the resolver's five, the search bar's three, the comparison's two, `Close the diff`, `Activity log` and `Close the activity log`, the three splitters, `Stage README.md`, `Discard README.md`, `Unstage src/core.txt`, `Show feature/alpha in the graph`, `Stop soloing feature/ff`, `Dismiss the restore offer` (D411, D435; ruling 34); the two `alpha` rows' help texts `feature/alpha` and `origin/feature/alpha` (D456); the stopped merge and "ops", dark, 1100×700 | Not run yet | Not run yet | `u3a-resolver-names`, `u3b-stage-row-names`; real window none | R: in launch 13, the resolver's five, the activity log's two, the three splitters, the search bar's three and `u-untracked.txt`'s three, each with its `AcceleratorKey` in run.txt; no capture. Methods 2 and 3; method 3 confirms a discard (D67's backup ref). Open points 75, 82, 83 and 84 |

## Checks by hand

What the gate checks by hand, outside the scripted run and the real-window pass, by the lead or the
owner; each is recorded here when the gate runs. Result values as under "Checks".

| Check by hand | From | Result | Notes |
|---|---|---|---|
| The icon's look-alike comparison: the owner's mark, by eye, against Git's logo and the well-known Git clients' icons, with no image search (the owner's wish, 2026-11-20) | Part B (design 1.17, 7.8; 13.4 item 11) | Not yet | |
| A second start of the app while another window is in front of the first, from the lead's isolating start file; what came to the front is noted | Part I (design 4.6; part I's conventions, "By hand at the gate") | Not yet | macOS's second start is 8b's Mac checklist |
| The taskbar button's icon, if B2 could not find the button: the owner's mark, not .NET's default icon | Part B (B2; design 7.5, 12.3.4; open point 51) | Not yet | Only if B2's taskbar step is "Not run" |
| The Narrator walk-through, 21 steps, by the owner (about 20 minutes): the graph's row, the names, About, and, started with `VISUALCOMMIT_GIT_STAND_IN=none` after the first app has closed (D421), the first-run screen | Part U (D411, D450; design 9.3.9, 13.3, 13.4 item 16) | Not yet | The steps and the words expected at each: part U's "The Narrator walk-through by hand". Each step "as expected", or the words heard. Started on two tabs (the ops copy active, the stopped merge second) with `VISUALCOMMIT_TIME_ZONE=UTC`. Silence where a row's name is expected (step 3's rows, step 5's Home and End) is a finding that reopens the route (9.9 item 1); step 2's "Commit graph, list" alone, no row being selected, is none. Open points 85 and 86 |
| Phase 6's System step: with System chosen in the settings screen, Windows' app mode switched while the app runs; the window, its title bar and the open views follow within a second | Phase 6 (D224; phase 6's report, "By hand on Windows at acceptance") | Not yet | |
| The guide's site, from the gate commit's artifact | Part G | Not yet | Its steps are added with part G |

## What proves each Delivers item

The rows of the Delivers items of parts L, I, F, K, B, A and U, and of the performance pass, which has
no part (design 0.1, 10.5); the other items' rows are added with their parts.

| Delivers item | Checks | Elsewhere |
|---|---|---|
| The contrast fixes in one round (D410; plan.md's items from phase 6, 768 and 769) | L1 to L6; real window L1, L2, L4 a, L5 dark, L6 a to c | `ThemeTokensTests` (`Every_text_reaches_4_5_to_1_on_every_background_it_lies_on`, `The_contrast_rounds_values_are_the_designs`, `The_placeholder_has_its_own_token_at_full_opacity`, `Both_themes_hold_the_same_keys`); `PanelViewTests`' three tests; `Phase8aExpectationTests.L`; wave 1's masked comparison against phase 7b's references with the scan's boxes, and the scan's eight lists in `docs/test-reports/phase-8a-contrast/` (design 0.7, 2.6) |
| Two instances; the macOS data folder (plan.md 778) | I1 (real window), I2 (and real window), I3; by hand: a second start behind another window | `SingleInstanceGuardTests`, `InstanceNoticeViewModelTests`, `MainWindowBringForwardTests`; `AppPathsTests.The_default_data_folder_is_the_platforms_documented_one`, read in CI's macOS job's log; `Phase8aExpectationTests.I` |
| First-run experience (R4, D405; plan.md 757; D423, D449, D453): git missing, too old or not working, each state with its own text; "Check again" without a restart; "Choose git.exe"; winget on Windows after a confirm, in a visible terminal, where winget exists; the Command Line Tools or Homebrew on macOS, the distribution's package on Linux; the missing askpass helper; with plan.md 781's visual check (missing, too old, not working, found again by "Check again") | F1 to F13; real window F1, F2, F4, F5, F6, F6b, F13 | `GitSetupShellTests` (each state: no clipped text at 1100×700 and 1000×560; the view's name and help text); Git.Tests' `GitLocatorTests`, `OsReleaseTests`, `GitStandInTests`, `GitConfigurationForgetTests`; App.Tests' `GitAccessTests`, `CheckGitAgainTests`, `GitSetupTextsTests`, `GitSetupViewModelTests`, `GitProgramViewModelTests`, `FileLauncherTests` (`ResolveStart`, `ToStartInfo` of the winget command, the one real `StartAsync`), `JsonSettingsStoreTests`, `MainWindowViewModelTests`, `SettingsDialogViewTests`; `Phase8aExpectationTests.F`; §9's sweep in the states (10) to (13) and the Narrator walk-through's steps 20 and 21 (part U, by hand); the guide's troubleshooting page and the first-run links (§11, part G); the lead's by-hand `cmd.exe /S /K ""<the path found>" --version"` before §5's merge (13.4 item 7; done on 2026-11-20: the console's window stays open where Windows Terminal is the default console) |
| A broken `~/.gitconfig` shows its own message (plan.md 767, from phase 6's design 8.5) | F6 (the fake), F6b (real git, also in CI's floor job on 2.34.1); real window F6 (the stand-in), F6b (real git) | `GitLocatorTests`: real git with `[user` in `GIT_CONFIG_GLOBAL` gives `ConfigFault(file, 1)`, on every CI job |
| OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE` (plan.md 766, risk 15; ruling 7, D424) | none: a log line, nothing on screen | `AskPassBrokerHelperTests` (the Info line `Git and ssh get SSH_ASKPASS_REQUIRE=force, so that ssh asks through VisualCommit; OpenSSH older than 8.4 ignores it and asks in the terminal VisualCommit was started from, if there is one.` once per session, at the first Ask or Deny scope); the guide's page (§11), whose proof is added with part G |
| The netrc note (plan.md 773, risk 35; ruling 8, D424) | F14 | `AccountsNetrcNoteTests` |
| Keyboard gaps (R7; plan.md 759, 784 and "Done when" 786: every main action reached by keyboard): a hunk and chosen lines from the diff, a blame run and its menu, "Cancel {title}" in the palette, the stage rows' buttons on keyboard focus; Tab out of every read-only text view; a panel resized by the keys (design 0.1, 6.1, 6.3.9) | K1, K2, K3, K4, K8; real window K1, K2 | `KeyboardPassTests` (among them `Tab_and_Enter_on_Discard_hunk_confirmed_leave_the_focus_in_the_text`, `A_keyboard_Stage_held_while_the_user_moves_to_the_summary_leaves_the_focus_there`, `A_keyboard_Stage_held_while_the_user_goes_to_another_tab_leaves_that_tabs_lists_alone` `A_rows_Discard_by_the_keys_cancelled_in_its_question_gives_the_focus_to_the_rows_own_button_with_the_frame` and, for the activity log's splitter, `Up_on_the_activity_logs_splitter_reached_by_Tab_heightens_the_log_by_10_and_keeps_it`; four tests state where Ctrl+Tab leaves the focus, which from a stage row is on nothing until step 19's fix); `DiffTextViewTests`, `ConflictPanesTests`, `BlameViewTests` (`An_arrow_with_Cmd_or_Option_picks_the_carets_run` among them), `WorkingChangesTests` (`Tab_walks_every_row_of_a_list_longer_than_its_view_and_Shift_Tab_walks_back` among them), `StatusBarTests` (6.4's fail-first tests); `RegionKeysTests`, `CommandPaletteViewTests`, `PaletteEntriesTests`, `AppShortcutsTests` passing alone (6.8 item 1); phase 2's `Check_17` and phase 5's `Check_A4_real_keys` alone in the real window at §6's merge (6.8 item 5); the inventory of main actions (design 6.3.9) in the report, from §6's merge report, with its rows "Inline or side by side", proven by K1 (its scripted-only step 11: Tab and Shift+Tab to `SideBySideDiffButton`, Enter), and "Amend", proven by K7 (disabled while committing) and `KeyboardPassTests` (focused by Tab, the focus given back), as the lead settled open point 40 |
| Focus after "Choose" (plan.md 771; phase 7a's design 17 item 5) | K5 | `KeyboardPassTests` (after Choose, reached by Home and Enter in the list, and after Esc in the browser; `Enter_in_the_repository_browsers_list_with_no_row_selected_chooses_nothing`; D454's Enter in the other dialogs' lists: `Enter_on_a_selected_row_of_a_pick_list_confirms_it_as_its_button_does`, `Enter_on_a_choice_dialogs_checked_option_confirms_it_as_its_button_does`, `Enter_on_the_push_dialogs_checked_remote_pushes_as_its_button_does`; the create form's Pick, ruling 51) |
| The loading diff (plan.md 772; phase 7a's design 17 item 11) | K6 | `DiffTextViewTests` (`FocusBody(() => false)`), `KeyboardPassTests` (a held read, an `Empty` body turning to `Text`, a diff opened by Enter still taking the focus) |
| The commit boxes (plan.md 774; D392) | K7 | `WorkingChangesTests` (a held `CommitAsync`, `CanToggleAmend`), `KeyboardPassTests` (Amend focused by Tab gets the focus back; `Amend_focused_by_Tab_leaves_the_focus_the_user_moved_while_the_commit_ran`) |
| Icons and branding (R5, D407; plan.md 764): the icon's SVG, the tool that makes the `.ico`, the `.icns` and the PNG files, the window's and the executable's icon; with plan.md 782's visual check, the icon in the window and the taskbar | B1, B2; real window B1 (a) to (c) and B2 (the executable's icon, the window's two icons, the title bar dark and light, the taskbar button) | `IconFilesTests` (items 1 to 8: the masters' rules, the manifest's hashes, the `.ico`, the `.iconset`, `png/`, `Assets/Icon`, the `.icns` (item 7, D447) and `iconutil` both ways (item 8, in CI's macOS job, whose log shows it passed, not skipped)); `WindowIconTests` (the main window's and the notice window's `Icon`, `AppIcon`'s three URIs); `Phase8aExpectationTests.B`; the files: the masters in `assets/brand/`, the generated files, `tools/VisualCommit.IconBuilder` (built in CI's six jobs; a second run of it leaves `git status` clean, 7.8 item 2); the owner's choice recorded in `docs/brand/icon/round-1/` and `round-2/` (D447); the look-alike check by eye, with the checks by hand |
| The README's "Name" note names the icon (D412; plan.md 764) | none | The README's "Name" note, design 7.3's text, written by the lead at §7's merge: proven by the file (design 0.1). No check of part B proves it; it stands here because it lands at §7's merge |
| Third-party notices, complete for what the app ships and reachable from the app (plan.md 762; R6; D431, D433, D434; design 0.1, 8.1, 8.3.4) | A5 (the notices file and the vendors' files in the app's text dialog); A4 (the license) | `ThirdPartyNoticesTests` (`Every_shipped_package_is_named_with_its_version`, `Every_shipped_file_is_named`, `Every_named_package_ships`, `Every_grammar_and_theme_is_listed`, `Every_license_named_has_its_text`, `The_app_embeds_the_texts_word_for_word`, `The_identity_matches_LICENSE`, `The_notices_files_only_addresses_are_its_copyright_lines`), red on the base first and green in CI's six jobs (8.8; the macOS and Linux natives proven only in CI's macOS and Linux jobs, 8.9 item 5); `TextDocumentDialogViewModelTests`, `DocumentTextViewTests` (the text's keys, part A's "Shown by no check": `The_copy_key_copies_a_selection_and_nothing_without_one`, `The_select_all_key_selects_the_whole_text_and_leaves_the_view_where_it_is`, `Keys_that_would_move_extend_or_use_the_hidden_caret_or_type_do_nothing`, `Cut_paste_undo_and_redo_change_nothing_and_leave_the_view_where_it_is`, `Enter_with_or_without_modifiers_leaves_the_view_where_it_is_and_plain_Enter_goes_on_unhandled`, `Another_editor_still_takes_its_keys_line_breaks_typing_deleting_and_moving_its_caret`, `A_wheel_up_after_End_leaves_the_end_though_the_lines_it_brings_in_wrap` among them); `AboutSectionTests` (the notices' switch shows Avalonia's file; `Enter_in_the_licenses_text_closes_the_dialog_as_Close_does_and_gives_the_focus_back`); the files `THIRD-PARTY-NOTICES.md` and `notices/` (the vendors' files byte for byte as the vendors ship them, `notices/** -text` in `.gitattributes`: two with CRLF, two with LF; 8.2, 8.8); `ci.yml`'s `paths`, which take the notices and `LICENSE` as build inputs (D433; unverified until a push changes only the notices, 8.9 item 6) |
| The About screen (plan.md 763 and 776; R6, D398, D409; with plan.md 782's visual check "The About screen") | A1 to A6; real window A1 (with a 1920×1080 capture where it fits) and A4 | `AboutSettingsViewModelTests` (the texts, the dialogs, the links' addresses, no launcher, a throwing launcher; `The_texts_are_read_off_the_callers_thread_and_a_second_press_meanwhile_opens_nothing_more`, `A_screen_closed_while_the_texts_are_read_shows_no_dialog`); `AboutSectionTests` (`Enter_in_the_licenses_text_closes_the_dialog_as_Close_does_and_gives_the_focus_back` among them); `LinkFocusFrameTests` (App.Tests, 12 cases: a link focused by the keyboard shows its whole text and its underline inside the frame, not under it, in About, dark and light, and in GitLab's sign-in dialog, `An_About_link_focused_by_Tab_shows_its_text_and_underline_inside_the_frame` and `A_sign_in_link_focused_by_Shift_Tab_shows_its_text_and_underline_inside_the_frame`; the frame moves nothing and a link draws outside its bounds only while focused, `The_frame_moves_nothing_and_a_link_draws_outside_its_bounds_only_while_focused`; the frame takes no pointer, `A_pointer_on_the_frame_reaches_what_lies_under_it_not_the_link`; a link focused by a click is underlined without the frame, `An_About_link_focused_by_a_click_is_underlined_without_the_frame` and `A_sign_in_link_focused_by_a_click_is_underlined_without_the_frame`); `AboutEntryTests` (the palette on a welcome, a repository and a dashboard tab; the macOS menu's one item `About VisualCommit`, no `Avalonia`, its `Command` with and without a dialog open); `SettingsDialogViewTests` (Down from Accounts reaches About, Down stays); `PaletteEntriesTests`, `CommandPaletteViewTests` and phase 6's K10 (the entry after `Follow the system's theme`); `AppVersionInfoTests` (WP0) and `The_identity_matches_LICENSE`; `Phase8aExpectationTests.A`; §9's sweep over About (part U); part G's G3 (About's guide link); About in macOS's application menu, 8b's Mac checklist (8.9 item 2) |
| An accessible name on every control and the graph's selected row exposed to UI Automation, with one Narrator walk-through by hand (R7, D411, D49 as D435 amends it, D450; plan.md 759's "an accessible name on every control; the graph's selected row exposed to UI Automation (D49); and one walk-through with Windows Narrator by hand", 770's icon buttons without a name, 784's visual check "the accessible names read through UI Automation in the real window", and "Done when" 786's "every control has an accessible name"; design 0.1) | U1, U2, U3; real window U1 and U3 (launch 13); by hand: the Narrator walk-through | `AccessibleNameSweepTests` (14 states; on the code before §9's names 12 of the first 13 failed, the notice's had nothing to find, and About's came at the merge: U2); `AutomationSweepTests`; `GraphRowSpeechTests`; `CommitGraphControlAutomationTests` (no peer without a client, one row peer for 100,000 rows, one Name change per move, bounds, the request's words, the same bytes after ten moves back); `CommitGraphControlTests.The_graph_is_a_ui_automation_list_with_its_name`; `StatusBarTests.The_operation_text_is_a_polite_live_region` (the status bar's live region, ruling 36); `Phase8aExpectationTests.U`; the spike's events in §9's merge notes (9.3.1); 9.6's grep (only `AutomationProperties.*` and `xmlns` lines in the `.axaml` hunks; no `Render`, `AffectsRender`, `InvalidateVisual`, measure or arrange in the graph's diff; no `IEmbeddedRootProvider`, `[PrivateApi]` or window peer in `src/` without a decision); the guide's sentence on Narrator (§11, ruling 39), whose proof is added with part G; to the Backlog at the close, an item status on the left panel's rows (ruling 36) and the hunk bars' repeated names (13.4 item 15) |
| Performance pass (plan.md's Delivers list; D436 to D438, D457; design 0.1, 10.5) | none: §10 has no part (design 0.5, 10.5) | This report's section "The performance series" and its table (design 10.3.6's step 5); `PerformanceSeriesTests` (RealWindowTests, no window: the median of odd and even counts, the range, the bound's rounding, `ParseRuns`, the summary line); App.Tests' `StartupTimelineTests` (the start-up lines), `RepositoryViewModelTests` and `RepositoryViewModelFilterTests` (the reload's and the heap's log lines) and `RepositoryOperationsPullTests` (a Pull's git calls held to their budgets), design 10.4's classes; Q1 in phase 1's real-window pass (`Phase1RealWindowPass.Checks_8_and_9_the_100k_commit_repo_and_Q1`; D50, D436); still to come in wave 4, ruling 76's package `restore`, proven by its alternated series and the lead's probe (D457) |

Plan.md 786's "Done when" ("the About screen … the complete notices … exist") rests on the rows of the
third-party notices and the About screen.

## Changes to expected results

### Part L: the contrast round (ruling 28, 31; D410, D419; design 0.7, 2.4, 2.6)

The round changes earlier expectations on purpose (ruling 28). Each is named here once, with its
reason. The values are design 1.5's; every lane list keeps `#E5606B`. The package's commits are
`bfd6b61` to `a1a4064` on `wip/p8a-colours`, merged in `3b9d159`.

1. **The dark danger colour, `#E5606B` to `#EA7D86`** (D410, D418: the old colour was 4.15:1 on a
   control's background, 3.64 on a hovered row and 3.76 on a selected one; the new one is 5.18, 4.55
   and 4.70). Changed: `ConflictResolverViewTests`, `ContextMenuAdapterTests`, `PathTextBlockTests`,
   `ThemeTokensTests` (the value moved to `The_contrast_rounds_values_are_the_designs`), the `Danger`
   of `Phase1Expectations` and of the `Expectations.Common` of phases 3, 4, 5, 6, 7a and 7b,
   `Phase7bExpectations.A`, and `Phase6SupportTests`.
2. **The dark danger button's label, white to `#10121A`** (D418: white on `#EA7D86` would be 2.70:1,
   `#10121A` is 6.92). Changed: `DangerText` in the `Expectations.Common` of phases 6, 7a and 7b
   (dark), `Phase6SupportTests`, `Phase6ExpectationTests.L`, and phase 6's L31
   (`Phase6Checks.L.Overlays`), which now compares `VcDangerTextBrush` with the palette's label;
   `l31b-delete-question-dark` shows it.
3. **HEAD's name on a hovered or selected row of the left panel, the row's accent** (D418:
   `VcAccentOnRowBrush`, `#8B9AFF` in dark, where the plain accent `#7C8CFF` was under 4.5:1; light
   keeps `#4353D8`). Changed, with `AccentOnRow` added to the palettes of phases 3, 5 and 6:
   - phase 3's check 32 (`32e`, `32g`), through `Phase3Checks.B.Support`'s `AssertLocalBranchAsync`;
   - phase 3's check 14 (`14d`), through `Phase3Checks.C.Helpers`' `AssertBranchRowAsync`;
   - phase 3's check 6, step 1 (`06a-double-click-local-branch`): `ff` is checked out and its row is
     selected (`Phase3Checks.A.Pointer.cs`; found by the package's review);
   - phase 5's P25, through `Phase5Checks.P.Helpers`' `AssertLeftPanelAsync`;
   - phase 6's G25, step 5: `main` is checked out by a double-click on its row, which leaves the row
     hovered and selected (`Phase6Checks.G.Helpers`' `LeftPanelLines`; found by the review).
   Unchanged, because the row they read is idle and they passed: `Phase3Checks.D.Helpers`,
   `Phase4Checks.A.Helpers`, `Phase4Checks.C`, `Phase5Checks.F.Helpers` and `F.Filters`,
   `PanelViewTests:91`.
4. **A renamed or copied file's letter on the file history's selected row, the row's accent** (D418,
   through `FileHistoryView`'s style for a selected row). Changed: phase 5's B5, steps 4 and 5
   (`b5c-rename-row`, `b5d-from-commit`), through `Phase5Checks.B.Support`'s `AssertHistoryRows`, and
   `Phase5Checks.B.cs:264` (`b5k-side-by-side-light`, where the colour is the same in light). Found
   by the package's review; design 2.4 names three groups, and this is a fourth.
5. **The search box's placeholder, its own colour** (D418: the placeholder has its own token,
   `#8F97A4` in dark, and no longer the secondary text's `#9BA3B0` at half opacity). Changed: phase
   5's F10 and F11, through `Phase5Checks.F`'s `AssertPlaceholderShown`, which read the placeholder as
   the secondary text (design 2.9 item 2's risk, found by running the class).

The files of items 3 to 5 that design 0.4 does not list for the package (`Phase5Checks.F.cs`,
`Phase3Checks.A.Pointer.cs`, `Phase5Checks.B.Support.cs`, `Phase5Checks.B.cs`,
`Phase6Checks.G.Helpers.cs`) belong to no other package; ruling 28 has the round change every earlier
expectation it makes false.

The earlier pictures the round changes are not listed one by one: this report replaces plan.md's 35
danger-label pictures (plan line 768) by the scan's lists (ruling 31, D419), committed in
`docs/test-reports/phase-8a-contrast/` (`colours-any.txt`, `-none`, `-placeholder`,
`-placeholder-double-backdrop`, `-danger`, `-danger-on-row`, `-danger-button`, `-accent-on-row`): 1,333
of phase 7b's 1,350 pictures change (1,137 scripted, 196 captures; the placeholder 1,293, dark danger
text 360, the danger button 41, the accent on a row 13), and 17 do not (`colours-none.txt`). The lists
are the research's, line for line, which `scripts/scan-contrast.py` reproduces. One thing they
understate: the accent on a row changes in 15 pictures, not 13. In `06a-double-click-local-branch`
(phase 3) and `u18j-checkout-redone` (phase 5) the two-letter name `ff` on HEAD's selected row is
taken for the graph's ring by the scan's rule for round shapes; both are in `colours-any.txt` for
their placeholder, so the total holds, and the scan's `expected.json` gives each a box around `ff`.

The three framed real-window captures, phase 0's `A-first-start-dark-with-frame` and
`B-after-theme-click-light-with-frame` and phase 6's `s15-system-start-with-frame`, also change in the
`FilterBox` placeholder, outside the scan's lists, which leave framed captures out (the scan's
`expected.json` has no `with-frame` key). The gate's `--expected` file gives each the title bar (§7) and
its twin's placeholder box moved by the frame (design 0.7; part B's "Changes to expected results"; open
point 55).

Part L's own pictures: `l4e` and `l4f` show origin's address, a temporary folder under the machine's
user folder, as phase 6's `l27h` does, so they differ between machines by nature. The report's open
points 2 and 3 are confirmed on the "graph" scenario: "delete main" gives "Delete main on origin…"
with the detail `origin/main` as its first row; the comparison from `origin/main` to `main` lists
`src/main-app.txt` renamed from `src/app.txt`; the file history's row of "Add settings page" shows
"R".

### Part I: no earlier picture; one earlier test replaced (design 4.5, 4.7; ruling 68)

No earlier picture changes (4.7). `AppPathsTests.The_default_data_folder_is_a_VisualCommit_folder_in_the_users_app_data`,
which compared the method with its own call, is replaced by
`The_default_data_folder_is_the_platforms_documented_one` (`%APPDATA%\VisualCommit` on Windows,
`<home>/Library/Application Support/VisualCommit` on macOS, on Linux `$XDG_CONFIG_HOME/VisualCommit` when
it starts with `/`, else `<home>/.config/VisualCommit`); `Everything_lives_under_the_data_folder` gains
`instance.lock` and `instance.json`.

No expected result of I2 or I3 was changed by the package (`e6f74e5` to `894829a` on
`wip/p8a-single`, merged in `b70abdb`). Its review found that I3 asserted less than its text says
("as I2 after step 1, in light" includes what is read in the tree: the window's title, that it
cannot be resized, the default button); I3 asserts it now (`79d6ac8`).

### The floor: git's words by version, no picture (design 3.4, 3.6)

The floor's package (`d0d4279`, `3e6f89e` on `wip/p8a-floor`, merged in `a63390c`) changes earlier
tests so that they hold on every git from 2.34.1: where git 2.34 words a message or an exit code
otherwise, the test takes the words through `GitWords`; the scenarios' stashes are rebuilt below 2.36,
so that the ids the earlier reports pin hold on every git; phase 5's U18 finds the undo's stash by
`git fsck` where git names it otherwise. On the laptop's git 2.56 every one of them asserts what it
asserted before. Which of them hold on 2.34.1 is CI's floor job's to say (design 3.3.5); its rounds
are recorded in status.md's CI section and, at the close, here under "Other platforms". No earlier
picture changes on the laptop; on macOS phase 5's `a2d-show-reads` shows the second `rev-parse` call
with `--path-format=absolute` (design 3.6).

### Part F: the Git program row in nine of phase 6's pictures; no earlier expectation (design 0.7, 5.4, 5.6; rulings 3, 40)

- **Earlier pictures** (ruling 40; 0.7's row "The Git program row"; 5.6): nine scripted pictures of phase
  6, at the global scope's bottom alone: `s14c-git-global`, `s14i-git-global-1920`, `s18a-name-refused`,
  `s18d-global-repository-sets-its-own`, `s19a-global-ff-only`, `s19d-global-repository-sets-its-own`,
  `s20b-merge-tool-pick`, `l31e-settings-git-dark` and `l31e-settings-git-light` (0.7 wrote `l31e-dark` and
  `l31e-light` before 2026-11-20). From §5's merge each shows, under the merge tool's note, the row `Git
  program` with the git that the harness's machine search found: its path in `GitProgramPath` and its
  note (`Found on the PATH.` on the laptop) in `GitProgramNote`, with `Browse…`. The row shows the first
  git on the test process's `PATH`, which differs by machine and, on one machine, by the shell that
  started the run: on the laptop, through `scripts/heavy-run.sh` (Git Bash),
  `C:\Program Files\Git\ucrt64\bin\git.exe`; from PowerShell, `C:\Program Files\Git\cmd\git.exe`
  (`GitLocator` takes the first on the `PATH`; the status bar's `Git {v}` is the same either way). §5's
  builder and the lead compare only runs started through Git Bash, as wave 1's whole run was (through
  `scripts/heavy-run.sh`, D245), whose pictures are the references. No real-window capture shows the Git
  section, so none changes. Proof (5.6): §5's builder runs `--filter-class "*Phase6ChecksS"` in `VisualCommit.VisualTests.Phase6`, `--filter-method "*Check_L31*"`
  in `VisualCommit.VisualTests.Phase6Look` and `--filter-class "*Phase0Checks" --filter-class
  "*Phase2Checks"` in `VisualCommit.VisualTests`, and compares their pictures with
  `D:/Projects/Personal/phase8-work/references/wave1/` through `scripts/compare-pictures.py`: only the
  nine differ, there; any other difference stops the package (ruling 3). Wave 2's whole run at its close
  covers the rest.
- **8a's own picture** (0.7): part L's `l3f-settings-name-placeholder` gains the same row at §5's merge;
  the lead's `--expected` file for that merge names it (5.6's runs do not take it).
- **No earlier expectation changes** (5.4, 1.4): `ShellTests.The_status_bar_shows_what_the_view_model_reports_about_git`
  (`Looking for Git...`, then `Git not found`) now also shows the screen and passes unchanged; the status
  bar's texts are today's (`GitStatus` keeps its id and its four texts); `GitAccess.GetRunnerAsync`'s
  three texts stay, the too-old one reading `GitVersion.Minimum`; `Using Git {version} at {path}.` stays
  word for word; phase 6's part S still finds its nine properties in `settings.json`, with no
  `gitExecutable` (1.4, 5.8); `FileLauncher.FindOnPath` stays as a forward, so its tests and callers are
  unchanged (5.2); `MergeToolHelpTests.HeldToolHelpRunner` becomes `internal` and asserts nothing new. Two
  earlier tests change their input, not what they assert (D449):
  `GitLocatorTests.FindExecutable_returns_the_first_git_on_the_path` and
  `FindExecutable_prefers_the_path_over_the_usual_install_locations` now give rooted `PATH` folders (`/empty`,
  `/first`, `/second`; `/custom`) instead of relative ones, since the search skips a relative entry
  (`A_relative_path_entry_that_holds_git_is_skipped`).
  Phase 6's S14 asserts that the global scope fits without a scroll bar at `s14c` (1100×700) and `s14i`
  (1920×1080) (`Phase6Checks.S.Helpers`' `AssertFits`); the row must keep that true (in the references'
  `s14c`, wave 1's, about 150 px lie free under the merge tool's note at 1100×700): if it does not,
  §5's builder stops and asks (ruling 3; open point 20).

### Part K: no earlier picture; one earlier helper changed (design 6.4, 6.6; D425; ruling 3)

No earlier picture changes (6.6, ruling 3), by construction: F1 to F3, F6 and F7 draw only after a
Tab, an arrow in the blame, a held operation or a moment no check catches; F4's buttons stay undrawn in
`k12b`'s state; F5-A gives the focus without a frame (`b06a`, `b07a`, `b07b`). The builder proves it by
running phase 2's walk-through, `Phase5ChecksB`, `Phase6ChecksK`, `Phase7aChecksB`, `Phase7aChecksN`,
`Phase7bChecksB` and `Phase7bChecksN` and comparing their pictures with
`D:/Projects/Personal/phase8-work/references/wave1/` (`scripts/compare-pictures.py`): no difference. A
difference stops the package, but in part N of 7a or 7b, where a change from the create form's fix comes
out as a known issue instead (6.3.5, ruling 51).

The comparison for §6's merge, run on `12aa3a4` (the merged app code but for a comment) against wave
1's references, showed no difference the package made, none in part N either: of the 208 pictures 198
were the same files, and the other 10 differ from run to run by nature, like those wave 1's comparison
lists: a caret (`k10a`, `k10f`, 7a's `n06b`), the progress bar without a percentage (7a's `n02b`,
`n07a`, 7b's `n04b`), a temporary folder's name (`k10e`), anti-aliasing one level apart (7b's `n05b`,
`n05d`), and phase 2's `17c-restored`. Phase 2's `17c-restored` flips between two files from run to
run on the branch's base (`fe0d2d4`) as well, so a later comparison that shows it reads it as
run-to-run variance, not as this package's change.

One of part K's own pictures changes at later merges: `k3a-palette-cancel` in the palette's scroll thumb
(x 837–838 at 1100×700), at §8's and §11's merges (ruling 54, D417), as `l3a-palette-placeholder` does;
the lead's `--expected` file for each of those merges names it (design 0.7).

One earlier expectation changes (6.4): `Phase2Checks.Helpers.VisibleRowButtons` counts a button only when
it is drawn (`IsEffectivelyVisible && Opacity > 0.5`). Reason: a row that holds the keyboard focus now
shows its panel with its buttons undrawn, at opacity 0 (D425); check 01's assertion is unchanged.

Not changed, and checked at §6's merge: `AppShortcutsTests`' lists (6.3.3: "Cancel {title}" has no
`AppCommand` value and no key); the real-window helpers that wait for exactly one row button,
`Phase2RealWindowPass.ClickRowButton` and `Phase5RealWindowPass.ClickFileRowButton`, which D425 keeps by
showing the buttons on a focused row only (6.8 item 5 runs `Check_17` and `Check_A4_real_keys` alone);
phase 7a's B6 and 7b's B7, which stay as they are (6.9; K5 answers 7a's report on where the focus
returns). `ActivityLogView.axaml` changes only if 6.3.8's test fails. K10's fourth PageDown (D452) is
§11's, not part K's (0.7).

### Part B: the window's icon in three framed captures; no earlier test (design 0.7, 7.4, 7.6; ruling 3)

- **Earlier pictures** (ruling 3; 0.7's row "The window's icon"): the three framed real-window captures,
  which §7 changes in their title bars alone: phase 0's `A-first-start-dark-with-frame` and
  `B-after-theme-click-light-with-frame`, and phase 6's `s15-system-start-with-frame`. In phase 7b's
  references (`D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/`, at 200 %) a default icon
  stands at the title bar's left, before the title `VisualCommit`; from §7's merge the owner's mark
  stands there. `Phase0RealWindowPass.cs` and `Phase6RealWindowPass.S.cs` save the three and compare
  none, and no expected result names a title-bar icon (phase-0.md checks 1 and 5, phase-6.md S15), so no
  expected result changes: this report names the three under ruling 3. At the gate they differ from
  7b's in the title bar (§7; 0.7's region "the title bar"; the mark at its left) and, through §2's
  contrast round (D410, D418, ruling 13; design 1.5, 2.3), in the left panel's `FilterBox` placeholder
  `Filter`: `#8F97A4` at full opacity in dark (was `#9BA3B0` at half opacity) in
  `A-first-start-dark-with-frame`, `#68707D` at full opacity in light (was `#5C6572` at half opacity) in
  `B-after-theme-click-light-with-frame`, and in `s15-system-start-with-frame` the colour of the app
  mode the machine has (light in phase 7b's reference). This is the change their client-area twins
  `A-first-start-dark`, `B-after-theme-click-light` and `s15-system-start` show in
  `docs/test-reports/phase-8a-contrast/colours-placeholder.txt` (lines 1, 2 and 935; besides
  `colours-any.txt`, the union, in no other list). The scan's boxes do not cover the framed captures:
  0.7 leaves them out of the 1,333, and the scan's `expected.json` has no `with-frame` key. So the
  lead's `--expected` file gives each of the three two regions: the title bar, and its twin's
  `expected.json` box (`[29, 203, 99, 235]` in the twin's 2200×1400, at 7b's 200 %) moved to where the
  client area lies in the framed capture. In phase 7b's references (2226×1471) that is 13 pixels in from
  the left and 58 down from the top, so the box is `[42, 261, 112, 293]` there; the lead reads the
  place again on the gate's captures (open point 55). 7.6 adds that the title moves right; the
  references already hold an icon there, so whether the title moves is read at the gate (open point 52).
- **No scripted picture and no client-area capture changes through §7** (7.6): a headless frame has no
  title bar, the app shows no new text and no control gains a name (7.3). Proof (7.6): §7's builder runs
  `dotnet test --project tests/VisualCommit.VisualTests --filter-class "*Phase0Checks"`, then
  `python scripts/compare-pictures.py D:/Projects/Personal/phase8-work/references/wave1 artifacts/visual <out>`:
  no picture changed by §7. At §7's build (2026-11-20) the comparison found 13 pictures the same file,
  `b1-icon-sizes-dark` and `b2-icon-sizes-light` new, and one changed, not by §7: phase 0's
  `06c-restarted-first-frame`, 603 pixels in the status bar, which reads `Git 2.56.0` where the
  reference reads `Looking for Git...`, the first-frame race that phase-0.md's check 6 records. The
  lead's wave-2 run shows nothing from this package.
- **No earlier test changes** (7.4): no test reads a window's `Icon` or the app's resources (the `.Icon`
  hits in `tests/` are the glyph control's); `ShellTests`, `PanelViewTests` and `MainWindowCloseTests`
  make a `MainWindow` and pass unchanged (7.8 item 1).
- **8a's own pictures that show the icon** (7.6, 7.8, 1.17): B1, B2, their real-window captures and
  §8's About (`AboutIcon`, part A) show the owner's mark from their first run, since §7 builds with round
  2's masters and writes no placeholder; no picture of a placeholder ever exists, so none changes with
  the choice. Should the masters change again, those pictures change and are inspected again; part B's
  assertions name no shape (7.6), so none of them changes.

### Part A: the About button in the section list, the palette's scroll thumb, seven earlier tests (design 0.7, 8.4, 8.6; rulings 3, 14, 54; D409, D417)

- **Earlier pictures: the section list** (ruling 14, D409; 0.7's row "The About nav button"; 8.6). The
  83 scripted pictures of `settings-list-pictures.tsv` (`D:/Projects/Personal/phase8-work/research2/notices-data/`;
  phase 6 31, 7a 23, 7b 29) and 8 real-window captures (phase 6's `s14a-general` and
  `l31d-settings-light`; 7a's `a01a`, `a02b`, `a05a`; 7b's `a02d`, `a05b`, `a06b`) with their difference
  pictures show, from §8's merge, a sixth button `About` under `Accounts`, in x 140–317, y 255–285 at
  1100×700 (x 550–727, y 415–445 at 1920×1080; doubled at 200 %); the lead's `--expected` file for §8's
  merge takes the region from x 140 (open point 56). D409 counted "about 80" and "about 5"; 8.6's 83 and
  8 are the scan's.
- **Earlier pictures: the palette's scroll thumb** (ruling 54, D417; 0.7's row "Two palette commands";
  8.6). With one more command the empty palette's list is one row longer: `k10a-palette-commands`,
  `k10f-palette-names-repository`, `l31c-palette-dark`, `l31c-palette-light` and the captures `k10a` and
  `l31c-palette-light`, in the scroll thumb, x 837–838. §11's "Open the user guide" moves it again.
- **8a's own pictures** (0.7; six, open point 67). `l3a-palette-placeholder` and `k3a-palette-cancel` in
  the palette's scroll thumb (x 837–838), and, in the About button's region (x 140–317, y 255–285; open
  point 56), `l3f-settings-name-placeholder` and part F's `f12a-row-next-start`, `f12b-row-too-old` (the
  Git section) and `f14a-netrc-note` (the Accounts section), which show the settings screen's section
  list too (design 5.5, as part F built them): the lead's `--expected` file for §8's merge names all six,
  as the design's 0.7, corrected, does.
- **Nothing else** (8.6): `VcLinkButton` is the sign-in dialogs' `SignInLinkButton` moved unchanged, the
  native menu draws nothing on Windows, and no typed query of an earlier check matches the command. Proof
  (8.6): §8's builder runs phase 6's part S, K10 and L31 and 7a's and 7b's part A (the sign-in dialogs
  among them), then `scripts/compare-pictures.py --expected` against the references of the last closed
  wave, wave 2's (`D:/Projects/Personal/phase8-work/references/wave2/`; open point 68), with these
  regions: every other picture the same file. The captures are compared at the gate (0.7).
- **Earlier tests** (8.4; each reason once here):
  1. `SettingsDialogViewTests` (`:176-187` at `27c5109`): Down from Accounts reaches `SettingsNavAbout`,
     Down on it stays, three Ups reach Tools; the comment names About last. Reason: About is the sixth
     and last section (D409).
  2. `PaletteEntriesTests` (`:159`, `:340`): "About VisualCommit…" follows "Follow the system's theme".
     Reason: ruling 54, D417.
  3. Phase 6's K10 (ruling 54): `Phase6Expectations.K`'s `OpsCommands` and `DashboardCommands` end with
     `E("About VisualCommit…")`; `OpsCommandCount` 33 becomes 34, `DashboardCommandCount` 22 becomes 23,
     `WebAppCommandCount` 34 becomes 35, `PageDownRows` [11, 22, 32] becomes [11, 22, 33],
     `FirstRowInViewAtEnd` 21 becomes 22, `WheelRow` 26 becomes 27; `Phase6ExpectationTests.K`'s
     `Assert.Equal([11, 22, 32], PartK.PageDownRows)` follows, and the identity
     `Assert.Equal(PartK.OpsCommandCount - 1, PartK.PageDownRows[^1])` is added (33, the last of 34 rows).
     They follow from that file's identities; K10's run confirms them. Reason: one more command at the
     palette's end. §11 moves them again, with a fourth PageDown (D452).
  4. `Phase7aExpectations.A` and `Phase7bExpectations.A`: `NavButtons` and `NavIds` end with `About` and
     `SettingsNavAbout`; 7a's real-window A1 reads the linked file. Reason: D409.
  5. `ShellDriver.Phase6.Settings.cs`' `ChooseSection` gains `SettingsSection.About` →
     `SettingsNavAbout`, which would otherwise fall to General's default. Reason: D409.
  6. Phase 6's report, K10: its "`Shown` 33 commands" on "ops" reads 34 from §8's merge (and moves again at
     §11's). Reason: ruling 54. Stated here, not edited in phase 6's report.
  7. `CommandPaletteViewTests.The_keys_move_the_highlight_into_view_and_typing_filters` (`:99-116`, "ops"):
     34 commands; PageDown to rows 11, 22 and 33 (its comment drops "clamped": 33 is reached exactly);
     Up from row 0 wraps to 33, then 32. Reason: ruling 54.

  §8's builder Greps for others (`SettingsNavAccounts`, `Follow the system's theme`, `Shown.Count`) and
  names any here. As built (merged at `f014b2b`), no other earlier test's assertion changed; the
  comments these changes made false were corrected with them (K10's PageDown rows in
  `Phase6Checks.K.cs`, the 34 commands in `PaletteEntriesTests`, six buttons in 7a's and 7b's part A
  helpers), and the real-window pass's on K10's rows (`Phase6RealWindowPass.K.cs`) by the lead at the
  merge. `RealApp.Phase6.Settings.cs`' `SettingsNavIds` gains nothing: no real-window step clicks
  About's button (launch 3 opens About from the palette); nor does its `ShownSettingsSection`: launch 3
  waits on `AboutVersion` itself.
- **8a's own expectations.** None changes: parts L, I, F, K and B assert no section list or palette count.

### Part U: no earlier picture; one earlier test renamed, one fact of 8a's own changed (design 9.4, 9.6; 1.12; ruling 3)

- **Earlier pictures: none** (9.6, ruling 3). Names, live settings and peers take no part in layout or
  drawing. Proof (9.6): the grep at the review (each `.axaml` hunk adds only `AutomationProperties.*` or
  an `xmlns` line; the graph's diff has no `Render`, `AffectsRender`, `InvalidateVisual`, measure or
  arrange), U1's same bytes before and after the peer, and `scripts/compare-pictures.py` against the
  current references with the lead's `--expected` file for §8's merge: nothing outside what §8 changes
  (0.7's rows for §8: the About nav button, the palette's scroll thumb in `k10a`, `k10f`, `l31c` and in
  8a's own `l3a-palette-placeholder` and `k3a-palette-cancel`, and `l3f-settings-name-placeholder` and
  part F's `f12a-row-next-start`, `f12b-row-too-old` and `f14a-netrc-note` in the About button's
  region, six of 8a's own pictures in all). The references are those of the last closed wave (wave
  2's from its close, `D:/Projects/Personal/phase8-work/references/wave2/`, and after wave 3's close
  wave 3's; open point 91). No real-window capture changes: launch 13 takes none, and names change no
  pixel of the other launches' captures.
- **8a's own pictures: none.** No picture of parts L, I, F, K or B changes through §9. Part U's six
  pictures are new; no later package of 8a changes them (none of them shows the palette, the settings
  screen or the shortcuts list, which §8 and §11 change).
- **One earlier test renamed, one fact of 8a's own changed** (9.4, 1.12):
  `CommitGraphControlTests.The_graph_is_one_ui_automation_element_with_its_name` becomes
  `The_graph_is_a_ui_automation_list_with_its_name` and asserts the control type `List` (D435
  amends D49: the graph is no longer one element); its builders `Linear`, `Branch` and `Host` become
  `internal`, no assertion changed. One earlier test of 8a's own changes beside the rename:
  `KeyboardPassTests.ReadmesStageFact` becomes `Button RowStageButton "Stage README.md" of README.md in
  UnstagedFileList [Right], framed`. The reason: `FocusFact` reads `AutomationProperties.GetName`, and
  1.12's `StageFileFormat` sets that name as a local value over `ChangedFileRow.Key`, which beats
  `VcIconButton`'s binding to the tooltip (`Theme/Controls.axaml:216`) that gives "Stage" today. The
  test that asserts it is
  `Ctrl_Tab_from_a_stage_rows_Stage_reached_by_Tab_leaves_the_focus_on_nothing_and_F6_then_starts_at_the_left_panel`.
  No other assertion of `KeyboardPassTests` changes: `ReadmesRowFact`, `LeftSectionFact`, `GraphFact`
  and `SummaryFact` read names §9 keeps. The fact and its test came at wave 2's merge (`1355a20`, after
  §6's merge `e463392`; 1.17), after 9.4 was written; 9.4 gains it (open point 92). The pre-gate round
  (status.md's step 19) rewrites that test with the lead's ruling on Ctrl+Tab from a stage row (1.17);
  whatever test then reads `ReadmesStageFact` reads §9's name. Otherwise as 9.4:
  `ShellToolTipTests` reads tooltips, help texts and keys, which stay; `HideAndSoloTests` reads
  tooltips; `CommitKeyTests`, `RegionKeysTests`, `CommandPaletteViewTests` and
  `Phase6RealWindowPass.K.cs:224` read the focus on `GraphRows`, which stays (ruling 35).
  `StatusBarTests` gains one test and changes none. No other earlier test changes.
- **The left panel's rows: no earlier test, helper or picture changes** (open point 81). A `RefItem` row
  keeps its automation name, the row's text, as every earlier test and every real-window helper finds
  it (`RealApp.RefRow`, `Phase1Checks.Graph`). The lead chose the `duplicate` rule's exemption for
  `RefItem` under `RefList` over naming the rows by their full names, which would have changed those
  helpers' lookups, because a row of the branch list is told apart by its place under its section and
  its folders, and by its help text, the ref's full name. As built that help text was missing: the
  full name was the tooltip of the row's name text alone, and the row's peer gave none. D456 (3) binds
  the row's button's `AutomationProperties.HelpText` to `RefItemRow.ToolTip`, the one exception to
  ruling 34, with no tooltip on the button: it draws nothing, so no picture changes, and no earlier
  test reads a row's help text.
- **No expected result of 8a's earlier parts changes.** Part K's checks assert ids, the rows' names
  (their paths) and the names of buttons outside 1.12's table, which §9 does not touch (`Stage hunk`
  and `From GitHub…` among them); part K's real window: "the UI Automation names the walk meets are
  §9's from its merge". The one row button's name an 8a test holds is `KeyboardPassTests`' above.
  Part F's controls keep the names of their own texts (1.17), which the sweep's states 10 to 13 read;
  the real-window helpers that find a row's button by id (`Phase2RealWindowPass.ClickRowButton`,
  `Phase5RealWindowPass.ClickFileRowButton`) find it by id, which §9 keeps.

### Changed in this report after its first commit

- **L6, method 5, step 6 and its expected result** (2026-11-20, `24d335f`): the step now waits for the
  file history's diff of the clicked row, and the expected result names what that diff shows. Reason:
  the page selects its first row, this same row, by itself as it opens, so the click starts no read,
  and the wait for "the row selected" passed at once. In wave 1's whole run `l6h` was taken while the
  diff was still loading and showed an empty body; the inspection of the new pictures found it. The
  check asserts the loaded diff now, and that the row is the page's first.
- **I2, after step 2** (2026-11-20, `9a82a8a`): "Try again" and "Close" keep their bounds through a try.
  Reason: "Try again" was 81 px wide and `Trying…` 75, so "Close" moved 6 px to the right during a try
  and back after it, which the inspection of `i2a` to `i2c` showed. The button now never gets narrower
  than it has been drawn; the new assertion failed on the code before. `i2b-notice-trying` changed with
  it; `i2a`, `i2c` and `i3a` are the same files as before.
- **F7, expected result** (2026-11-20, §5's build): "Git said:" and `GitSetupGitSaid` are not shown. The
  report as first committed left them to §5's builder (open point 13); the build puts a start's message
  in the body alone, and F7 asserts it. This completes a result the report left open and reverses none.
- **F13, step 8** (2026-11-20, §5's build): the step's explanation no longer says that `CheckHelper()`
  runs after `GitStatus` reads `Git not found`: as built it runs only while git works and the helper was
  missing. The step's actions, waits and expected results are unchanged.
- **Part F's texts, the settings row's note** (2026-11-20, the lead's ruling on the review's FD5 and
  FT8): after `Use the Git it found` while the chosen git is gone and another was found, the note reads
  `VisualCommit looks for Git again when you save.` alone, since none of 5.3's other texts is true
  there. No check shows it; `GitProgramViewModelTests` pins it.
- **F6b's start** (2026-11-20, the lead at wave 2's merge, `fc123ec`): the check's own probe in place of
  none, so that the check reads the machine only for git. Reason: without a probe the app's own probe
  also looked for `winget.exe` in every folder of the machine's `PATH` and read `/etc/os-release`,
  which no check may depend on. No expected result changed.
- **K3 and K8, step 1** (2026-11-20, §6's build, `55b8d3a`): before `HoldIn` the copy's `origin` URL is
  written again with forward slashes (the note under K3). No expected result changed.
- **K5, step 6** (2026-11-20, the lead at wave 2's merge, `25dd5c8`): the step stands as first written,
  "Press Enter" with the row selected, and passes by a fix of the app: Enter on the selected row of the
  browser's list did nothing before it (Avalonia's list takes Enter on an item for itself), and §6's
  check had gone round it by Tab to `Choose`. The test
  `Enter_in_the_repository_browsers_list_with_no_row_selected_chooses_nothing` holds the other half:
  with no row selected, Enter chooses nothing.
- **B1 and B2, after step 1** (2026-11-20, §7's build, `27c3327`): the sizes under row 1 are the bare
  numbers at 11 in SemiBold, each under its image and within its width; the heading is at 13 in
  `VcTextPrimaryBrush`; row 1's bottoms lie on y 312, row 2's on y 488, and row 2's last image ends at
  x 608. Reason: open points 44 and 45 left the form and the lines to §7's builder; at the regular
  weight `TextInk` could not read the token from `20` and `32` on white (10 off, past its tolerance of
  8), and in SemiBold every size reads its token exactly in both themes.
- **F6, after step 1, a clarification** (2026-11-21, the lead's check of the new pictures): "one line"
  of `GitSetupGitSaid` now reads "one line of git's words, wrapped to the box's width". The box wraps
  a long line by design (5.3), `f6a-config-fault` shows git's one line over two rows, and the check
  asserts one line of text. No expected result changed.
- **Part K's "What a fake stands for", the note under K3 and open point 42** (2026-11-21, the fetch
  hold's fix, `f019688`, merged at `eba3717`): the convention says when a held fetch's shell gives up
  (at a look that finds no release once its rounds are over; one that saw the release never looks
  again) and that on Linux and macOS K3's cancelled shell may wait through K8's hold until step 9,
  which does no harm; the note no longer says that `FetchHold.cs` stays as it is. Reason: CI's run
  44969394972 failed K3 and K8's method on Linux by the script's second look at `release` after
  `Arm()`, and the fix changed the script. No step and no expected result of K3 or K8 changed.
- **Part A's "Shown by no check"** (2026-11-21, §8's build, its review and the lead's ruling on the
  copyright lines, merged at `f014b2b`, and the lead's rulings after that merge on the text dialog's
  keys, merged at `85e2884`): the convention names the tests these brought, and what the lead left
  as it is. §8's review brought `AboutSettingsViewModelTests`' two (the texts read off the caller's
  thread, with a second press meanwhile, AT7; a screen closed during the read, AS3), the move up
  after End that lets the end go (AS2) and the copy test (AS4); the copyright lines quoted with
  their addresses brought `ThirdPartyNoticesTests`' guard of the addresses (open point 70); the
  ruling that Enter in the text is the dialog's brought the Enter tests of `DocumentTextViewTests`
  and `AboutSectionTests`; and the ruling that the text takes no key that would use its hidden caret
  brought the tests of the keys, of cut, paste, undo and redo, and of select-all, and widened the
  other editor's guard. The copy test, first `Ctrl_C_copies_a_selection_and_nothing_without_one`,
  failed CI's macOS job in run 44969394972, where AvaloniaEdit's copy key is Cmd and the headless
  platform's Ctrl; renamed `The_copy_key_copies_a_selection_and_nothing_without_one`, it now presses
  the platform's copy keys. Left as they are by the lead: PageUp and PageDown with Ctrl, Alt or Meta
  and no Shift; Home and End clearing a selection made with the mouse; a held Enter on `Show
  license…` opening and closing the dialog in turn, which no test shows. Reason: these tests and
  rulings came after the convention was written. No step and no expected result changed.
- **U2 and part U's conventions: the sweep's rules, its states and how a sweep fails** (2026-11-21,
  D456 at §9's merge): the rules as D456 reads them (a scroll bar and what lies inside it are not
  checked; a dialog is its own container for `duplicate`; two key texts joined by ` to ` are a key
  text), and the states as built: 14 facts, 13 with one start of the app and state 14 with §4's
  notice window alone (`InstanceNoticeHost`, with no session; the conventions had said one start
  each); state 3 with three sweeps (About, the license's text dialog, the notices' dialog on
  Avalonia's file), state 4 with the add-repositories and create-pull-request dialogs; and the first
  run's findings under U2. Reason: §9's first run left Fluent's unnamed scroll bars and the stacked
  dialogs' Cancel, outside 1.12's table, to the lead (9.3.8); the report's state 3 named About and
  the license's text dialog alone, while a vendor file's text is reached only through the notices'
  dialog, the one with a switch (D456 (5) makes About and its text dialog state 3; the build's
  correction at §9's merge, which the lead took, adds the notices' dialog); and §9's review found
  state 4 without two dialog kinds. Also how a sweep fails, as built: `AutomationSweep.AssertNamed`,
  which methods 2 and 3 run, throws its own `AutomationSweepException`, naming every problem, since
  the Testing project has no xUnit `Assert`; `AccessibleNameSweepTests` does not run `AssertNamed` at
  each state, as U2's expected result first said, but reads each state's problems with
  `AutomationSweep.FindProblems` and fails once at the fact's end, listing them state by state, so
  that one run gives every finding. U2's expected result, nothing reported in each state, is
  unchanged.
- **U3, method 2, step 6 and its expected result, and launch 13's close of the resolver and its read
  of the graph's first child** (2026-11-21, the lead's ruling at §9's merge; the check since
  `96d94bf`): Esc leaves the resolver open, and `ResolverCloseButton` closes it, by a real click in
  launch 13; the check asserts both. Reason: the report misread phase 4: after the double-click on
  `notes.txt` the keyboard focus is in the resolver's result, where Esc is the resolver's own and
  closes nothing (phase 4's design 11.10). The end is unchanged: no resolver, the repository as it
  was. Launch 13 already read `GraphSelectedRow` once before `RecordSelectedRowEvents()`; the step
  now names it the graph's first child, and the paragraph below the launch and open point 88 no
  longer wait on the spike. Reason: D455 records the spike's run: no event from the row while no
  client had read below the graph, and one Name change and one LiveRegionChanged per move, received
  by its FlaUI client at 200 %, once it had read the graph's first child, so launch 13 reads that
  child first (the lead at §9's merge). What waits on D246's runs is this launch's own run, with its
  events and their counts, and the row's bounds at 200 %, which the spike did not record.
  The walk-through's paragraph on the owner's short listen and open point 89 now give D455's timing,
  the first build the owner can hear in place of §9's merge (the lead, after the check of this
  report's edit); no step and no expected result changes by it.
- **U3, `u3a`'s expected result** (2026-11-21, the same ruling): the resolver spans the right panel's
  column, so no stage panel shows beside it; the sentence that put the stage panel with `notes.txt`
  and `u-untracked.txt` in the right panel is gone. Reason: phase 4's design 11.2, as phase 4's `c10e`
  shows; the report misread it. The check does not assert the right panel there.
- **U3, method 3, step 8 and its expected result** (2026-11-21, D456 (3)): the step also reads the two
  `alpha` rows' help texts through their own peers, `feature/alpha` and `origin/feature/alpha`, with no
  tooltip on either row's button. Reason: the `duplicate` rule's exemption of `RefItem` rested on the
  row's peer giving the ref's full name as its help text, and as built it gave none (the full name was
  the tooltip of the row's name text); D456 (3) binds the row's help text to `RefItemRow.ToolTip`, the
  one exception to ruling 34, and the assertion failed on the code before the binding. A new expected
  result; nothing drawn changes. U3's expected result also names that one exception, which step 8
  reads, and no longer calls the peer's fallback to the tooltip unverified. Reason: §9's build ran U3,
  and each control the table gives "by the peer's fallback" gave its tooltip through its peer (design
  9.9 item 7); the fallback reaches only the control's own tooltip, which is why the branch row gave
  none.
- **The Narrator walk-through, step 10** (2026-11-21, D456 (3) and the lead's ruling at §9's merge on
  the spike's S4): Narrator may also read the clicked branch row with its help text, and the owner
  listens, as the folder opens, for a row named `Avalonia.Controls.DockPanel` for a moment (open point
  93); what is heard is written down. Reason: the row gained its help text, and the spike's S4 met that
  name once.
- **A1, after step 9, and part A's conventions, "Where things are read"** (2026-11-22, the link frame's
  fix, merged at `fcf8c1e`): the check also reads the focused repository link in `a1b`, against `a1a`
  (`AssertFramedLink`, by `LinkFrame`): the text's pixels unchanged but for the underline's rows, the
  underline shown, a whole frame in the theme's frame colour on four sides outside the text, the
  background between frame and ink. Reason: in wave 3's whole run `a1b` showed the theme's frame over
  the link's text and its underline hidden, which the check, reading the underline in the tree alone,
  did not see; the inspection of the new pictures found it ("Wave 3's comparison with wave 2's
  references"). The check asserts more of an unchanged expected result: no step and no expected result
  changed.

### Wave 1's comparison with phase 7b's references (2026-11-20)

The whole default run on wave 1's final code (`bba59c7`; 7,993 tests, none failed) against phase 7b's
gate (`D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/`), with the scan's `expected.json`
as the expected regions (D419). `scripts/scan-contrast.py` on the references wrote the eight lists,
each equal to the committed one. Of the 1,150 earlier scripted pictures, 1,141 changed and 9 are the
same file; none changed size. 1,083 changed only inside their expected regions. The other 58 (54
changed also outside their regions, 4 changed that the scan lists under none) were looked at one by
one by six inspectors, each reading both pictures' pixels:

- **42 differ from run to run by nature**, and wave 1 changed nothing they show: a temporary folder's
  random name in a path, in git's output or in a tooltip (15: phase 1's `11a`, `13b`, `14a`, `14b`;
  phase 3's `16j`, `30g`; phase 5's `u21v`; phase 6's `k10e`, `l30c` dark and light, `l32a`, `l32b`
  dark and light, `w1e`, `w3a`); a caret that blinked (12: `21a`; `f10b`, `f10c`, `f10e`, `f10f`,
  `f11c`; `l27g`, `l31g`, `w6a`; 7a's `b01a`, `n06b`; 7b's `n01a`); a progress text or bar caught at
  another moment (5: `17d`, `18b`, `21b2`, `21b3`, 7b's `n04b`); two reads of the activity log in the
  other order, which start together (2: `a2d`, `a2g`, as phase 5's and 7b's reports already say);
  anti-aliasing pixels one or two levels apart with no shape (8: `03e`, `21b1`, `27d`, `32d`; `a3c`,
  `c11f`; `u23e`, `u23f`).
- **16 are the round's own colours where the scan's boxes do not reach**: the placeholder under a
  menu's shadow, cut by a dialog or in a disabled box (7: phase 3's `04c`, `09b2`, `26g`; phase 4's
  `c16a`; phase 6's `l31e` dark and light; 7a's `n07a`), and danger text or its icon just outside a
  box, mostly a line's first letter or a text under a backdrop (9: phase 5's `a2c`, `a2e`; phase 6's
  `w2b`, `w7a`, `w7b`, `w7c`; 7a's `n04a`, `n08b`; 7b's `n05d`). In each the pixels go from the old
  colour to the new one and nothing else moved.
- **None shows a change the design does not name** (ruling 3), and none is unexplained.

The 35 new pictures were each checked against this report's text by three inspectors, colours by
their pixels: 34 show what is expected; `l6h` did not (above). Two things they noted for the
real-window twins: in L4 a's launch the menu opens under a pointer that has not moved, where Windows
may hover an item the scripted picture does not show hovered; and `l2a`, `l2b`, `l5a` and `l5b` show
the unstaged list scrolled to bring `src/Calculator.cs` into view, which the real window must match.

After the two fixes above the run's scripted pictures are the references of the later merges until
wave 2's close (`D:/Projects/Personal/phase8-work/references/wave1/`, 1,185 pictures with their hashes
beside them). The 200 real-window captures were not taken again; the gate takes them.

### Wave 2's comparison with wave 1's references (2026-11-20)

The lead's runs at wave 2's close (status.md's step 12), each alone on the machine, on `c9835e9` (the
merged wave with the merge's fixes; the commit itself only adds words to one test's timeout).

**The whole default run**, through `scripts/heavy-run.sh`: 8,283 tests, 8,278 passed, 5 skipped, none
failed, 1 h 46 min 41 s (20:53 to 22:40).

**The pictures**, with `scripts/compare-pictures.py` against
`D:/Projects/Personal/phase8-work/references/wave1` and an `--expected` file of the regions design 0.7
names for wave 2 (the Git program row in nine of phase 6's settings pictures and 8a's own `l3f`, for §5;
nothing for §6; nothing scripted for §7): of the 1,185 earlier scripted pictures 1,126 are the same
files and 59 changed, none in size. The ten expected ones changed only inside their boxes (`s14c`,
`s14i`, `s18a`, `s18d`, `s19a`, `s19d`, `s20b`, `l31e-settings-git-dark`, `l31e-settings-git-light` and
`l3f-settings-name-placeholder`), and none of them stayed unchanged. The other 49 are named in no
expectation; each is a picture that varies from run to run by nature, and none shows a change of wave
2. 33 of them are pictures wave 1's inspection had already read, with the same regions now; the lead
read the other 16 by their changed pixels (three of them enlarged, reference beside new):

- A temporary folder's random name in a text (16): phase 1's `11a`, `13b`, `14a`, `14b`; phase 3's
  `16j`, `30g`; phase 5's `u21v`; phase 6's `k10e`, `l30c` dark and light, `l32a` dark and light, `l32b`
  dark and light, `w1e`, `w3a`.
- A text box's caret, one column of 16 pixels, drawn in one picture and not in the other (14): phase
  5's `f10b`, `f10c`, `f10d`, `f10e`, `f11b`, `f11c`, `f11d`; phase 6's `k10a`, `k10f`, `l27g`; phase
  7a's `n06b`; phase 7b's `n01a`, `b06b`; 8a's own `l3d`.
- A progress bar or a fetch's percentage caught at another moment (5, three of them with a caret too):
  phase 3's `17d`, `21a`, `21b2`, `21b3`; phase 6's `l31g`.
- Anti-aliased edges a few levels apart, 1 to 7 pixels (14): phase 2's `18c` and phase 5's `u16h` (3
  pixels of the working-changes ring and 1 of the status bar's icon, at most 11 levels: what phase 2's
  `17c-restored` showed at §5's and §6's builds, which did not change in this run); phase 3's `21b1`,
  `27d`, `32d`; phase 4's `c11f`; phase 5's `b6b`, `u16q`, `u23e`; phase 6's `g23i` (7 pixels of the
  toolbar, 5 levels); phase 7a's `a09b`, `n04a`; phase 7b's `n05b`, `n05d`.

**The 35 new pictures** of parts F (19), K (14) and B (2), which their builders and reviewers had
inspected at the build, were checked by the lead against this report on 2026-11-21 (the workflow
`phase8a-wave2-new-pictures`: eight inspectors, four to six pictures each, every text read word for
word and every colour from its pixels; then a second inspector for each picture not passed; 1.69M
tokens; the crops under `D:/Projects/Personal/phase8-work/wave2/inspect/`). **No fault.** 33 passed at
once. Two were held back as unsure, and the second look judged neither a fault:

- `f3a-chosen-gone`: the guidance `VisualCommit found another Git at C:\Program Files\Git\cmd\git.exe.`
  breaks after `C:\Program`. The text is the report's word for word; the line cannot hold it at
  1100×700 (392 px of content, about 400 needed), and the break at the path's space is the one the
  app's rule for paths in running text gives (D227). The chosen path above it stands whole on its own
  line.
- `f6a-config-fault`: git's line shows over two rows in the "Git said:" box, where F6 said "one line".
  The check reads one line of text (`Assert.Single` of its lines), the view wraps it on purpose (5.3:
  six lines, then a scroll bar), and launch 9's twin calls it "that line": the words meant one line of
  git's. F6's text now says so ("Changed in this report after its first commit").

What the inspectors noted beside the checks, none of it a fault of wave 2: `k5a-chosen` differs from
phase 7a's `b06a-chosen` only in the URL box, (381,349)-(502,362), the port that differs by run (the
lead's own pixel comparison); in `k8a-splitter` the graph's Date column is hidden at the left panel's
new width (430 px of graph area: the column rule of D49 lets Date give way before the message is
narrower than 120), which K8 does not name; in `k4b-row-staged` the row's focus frame is drawn over the
list's overlay scroll thumb at its right end, Fluent's layering, the frame whole; in `f9a` the three
buttons stand on two lines, in their order; in `f12a` the Git program row's path column is 195 px and
the path 193, so a longer chosen path is shortened at its start, as designed; the part B sheets show
each of the 23 files pixel for pixel (at most 1 level off on dark, none on light), so the mark's look is
the files' own. Launch 11's real-window capture must show the unstaged list scrolled as `k1a` to `k1d`
do (design 12.3.5).

**The references** of the later merges are this run's 1,220 scripted pictures,
`D:/Projects/Personal/phase8-work/references/wave2/` (their hashes in `wave2-pictures.sha256` beside
it); a picture the lead's check corrects is replaced there.

**The lagged list** (design 0.4, `VISUALCOMMIT_WATCHER_LAG_MS=1500`, on `1821089`'s code, 19:46 to
20:39): `Phase2Checks` 25 of 25; phase 6's five filters 21 of 21; phase 7a's walk-through 205 of 205;
phase 7b's 178 of 178; the twelve Hosting.Tests classes 126 of 126; the 8a project 103 of 103. The
fifteen App.Tests classes: 200 of 201 in the first round, 201 of 201 in a second. The one failure was
phase 3's `RepositoryOperationsFetchTests.Through_the_window_a_held_fetch_shows_its_progress_and_Cancel_and_the_toolbars_Fetch_brings_the_remote_work`,
`Timed out after 60 s waiting for the fetch's percentage.` Run alone it passed five times under the
lag and twice without. Its cause is not known: the wait did not say whether the test's server had held
the response or what the status bar read. Both of the test's waits now add that to a timeout
(`c9835e9`; no condition and no assertion changed), so that the next failure tells a hold that came at
another point of git's output from a fault of the app. It is not counted as R8's fault until then, and
it is not waved through: status.md keeps it open for the review before the gate.

**Two earlier real-window checks alone** (design 6.8 item 5; the owner told, 22:41): phase 2's
`Check_17` passed (46.7 s) and phase 5's `Check_A4_real_keys` passed (1 min 14 s), each started through
`scripts/heavy-run.sh` with its own `--filter-method`. So a stage row's buttons that stay in UI
Automation at `Opacity` 0 while the row has the keyboard focus (D425) do not disturb the two earlier
checks that find a row's button by its id.

### Wave 3's comparison with wave 2's references (2026-11-22)

The lead's runs at wave 3's close (status.md's step 16), each with no agent building or testing beside
it, on `85e2884`'s code (wave 3's last merges: §9, the fetch hold's fix and §8's fixes after its
merge); the docs' agents read and wrote beside the default run's first 25 minutes.

**The whole default run**, through `scripts/heavy-run.sh`: 8,436 tests, 8,431 passed, 5 skipped, none
failed, 2 h 9 min 24 s (22:18 to 00:31). A5's bound of 1 s held in it (open point 60).

**The pictures**, with `scripts/compare-pictures.py` against
`D:/Projects/Personal/phase8-work/references/wave2` and the `--expected` file
`D:/Projects/Personal/phase8-work/wave3/about/expected-about.json`, which names §8's regions (design
0.7, 8.6; part A's "Changes to expected results"): the About button in the settings screen's section
list, x 140–317, y 255–285 at 1100×700 (x 550–727, y 415–445 at 1920×1080), in 87 pictures, and the
palette's scroll thumb, x 837–838, in six. Of the 1,220 earlier scripted pictures 1,077 are the same
files and 143 changed, none in size. 90 changed only inside their expected regions, and none of the 93
expected stayed unchanged. The other 53 are 3 that changed outside their regions too
(`k10a-palette-commands`, `l31c-palette-light`, `l31e-settings-git-light`) and 50 named in no
expectation. Five inspectors read each of the 53 by its pixels, reference beside new (the workflow
`phase8a-wave3-pictures`, run `wf_2349d4b8-a4a`, with the new pictures' inspection below: 10 agents,
1.65M tokens; the crops under `D:/Projects/Personal/phase8-work/wave3/inspect/`): each is a picture
that varies from run to run by nature, and none shows a change of wave 3. In the three that changed
outside their regions, the change inside them is the design's: the palette's thumb ends at y 266, not
269, in `k10a` and `l31c`, for one more command, and `About` stands under `Accounts`, dimmed behind the
merge tool dialog's backdrop, in `l31e`.

- A text box's caret, one column of 16 pixels at the start of an empty box or after its last letter,
  drawn in one picture and not in the other (22): phase 3's `21a`, `21b3`; phase 5's `f10a`, `f10c`,
  `f10d`, `f10e`, `f11b`; phase 6's `k10a`, `l26b`, `l27f`, `l27g`, `l31c` light, `l31e` light, `l31g`
  dark and light; phase 7a's `b09a`; phase 7b's `b03a`, `b03b`, `b06a`, `b06b`, `n04c`; 8a's own `k7a`.
  In `l27g` and `l31g` dark also 3 and 2 pixels of a rounded corner, 1 or 2 levels apart.
- A temporary folder's random name in a text (16): the twelve characters after `plain-`, `clones-`,
  `ops-`, `edges-`, `sync-` or `workspace-` in a path, in git's output or in a tooltip, and the text
  after them drawn a little aside: phase 1's `11a`, `13b`, `14a`, `14b`; phase 3's `16j`, `30g`; phase
  5's `u21v`; phase 6's `k10e`, `l30c` dark and light, `l32a` dark and light, `l32b` dark and light,
  `w1e`, `w3a`, the same 16 as in wave 2's comparison. `16j` also has 4 pixels of a tag icon's edge, 5
  to 15 levels apart.
- A fake's port in a clone form's address (2): phase 7b's `b07b` and 8a's own `k5a`.
- A progress bar or a tooltip caught at another moment (2): phase 1's `10b-second-tab` and phase 7a's
  `n07a`.
- Anti-aliased edge pixels, 1 to 21 in a picture, 1 to 25 levels apart, no shape changed (11): phase
  2's `18c`; phase 3's `21b1`, `27d`; phase 5's `b6b`, `u16q`, `u20c`, `u20e`, `u23f`; phase 6's `g23i`,
  `l27e`; phase 7b's `n05d`. `u20c` and `u20e` reach 25 levels on the outermost pixels of the toolbar's
  Settings and Theme icons, a form of that region that 97 of phase 5's pictures of wave 2's own run
  show; wave 3 changed no toolbar file.

The two caught at another moment: in phase 7a's `n07a` the status bar's progress bar, which runs
without a percentage while the push gives none, shows its track empty in wave 2's picture and its
moving segment at the track's right end in wave 3's. In phase 1's `10b-second-tab` the new tab's
tooltip `New tab` is open over the toolbar's Fetch and Pull icons, (139,37)-(205,66): check 10 clicks
`+` (`AddRepoButton`) and takes the picture at once, the pointer left on the new tab, with no move to
the neutral point and no wait for a tooltip (`Phase1Checks.Shell.cs` lines 70 and 71), so whether the
tooltip shows depends on how long the click and the capture take against Avalonia's delay for a
tooltip. The lists of the gates of phases 1 to 5 hold one hash for `10b`. The later gates' pictures
of it and wave 1's and wave 2's references are other files, but none of those still on the machine
shows the tooltip: phase 7b's gate picture, the same file as 7a's, and wave 2's reference, the same
file as wave 1's (phase 6's gate picture is no longer on the machine). So this is the first run
known to catch it, most likely on a machine loaded by the whole run; it is on the list of the review
before the gate (status.md's step 19). The two ports: in phase 7b's `b07b` the fake of Azure DevOps
did not get its port 47397 in this run and fell back to a free one, 56841 (D363); in `k5a` the fake of
GitHub's port, chosen per run, changes the address in the URL box, (381,349)-(502,362), which K5's
notes set aside.

**The 15 new pictures** of parts A (9) and U (6) were checked by the lead against this report on
2026-11-22, in the same workflow: four inspectors, three to five pictures each, every text read word
for word and every colour from its pixels; then a second inspector for the one picture not passed, who
tried to refute its fault and found its cause. 14 passed. **One fault, `a1b-about-link-focus`.** A1's
step 9 expects `AboutRepositoryLink` "focused, framed, its text underlined". The picture showed
Fluent's focus frame on the link's own bounds, (481,375)-(710,391), which are its text's line box: its
2 px white outer line, on columns 481–482 and 708–709 and rows 375–376 and 389–390, turned the first
`g`'s left stroke and the last `t`'s right side white; its 1 px inner line, on columns 483 and 707 and
rows 377 and 388, darkened the tops of the capitals and ascenders and the descenders; and the
underline, which the text had in the tree, lay under the inner line and the white row 389, unseen.
The cause was the theme `VcLinkButton` (`Theme/Controls.axaml`): `Padding` 0, a template that was only
a `ContentPresenter`, and Fluent's default focus adorner, which is drawn on the control's bounds. The
sign-in dialog's two token links use the same theme; no picture of 7a or 7b shows one focused. The
check had passed because it asserted the underline in the tree (`TextDecorations` Underline) and read
a link's ink only while it was not underlined.

The fix (`8ef7e86`, `77c1936`, `5177cec`; merged at `fcf8c1e`, 2026-11-22 04:40): `VcLinkButton` takes
no focus adorner (`FocusAdorner` null) and does not clip (`ClipToBounds` False). Its template is a
`Panel` that holds the `ContentPresenter` and, laid over it, `Border#FocusFrame` (`Margin` -4,-3,
`BorderThickness` 2, `IsHitTestVisible` False) with `Border#FocusFrameInner` (`BorderThickness` 1)
inside it; their brushes, Fluent's `SystemControlFocusVisualPrimaryBrush` and
`SystemControlFocusVisualSecondaryBrush`, are set only under `:focus-visible`. So the frame's outer
line lies 4 px outside the link's bounds left and right and 3 above and below, the inner line 1 px
inside it, with 1 px of the background between it and the text's ink left and right; above and below,
the line box keeps the letters 2 px and the underline 1 px clear of it. The panel takes the content's
size and the frame's negative margin gives back its borders, so nothing moves. The frame is only
drawn, never hit: a pointer on it reaches what lies under it, so a hover or a click just outside the
text is none on the link, focused or not. It is the keyboard focus's alone: a link focused by a click
is underlined without it, and a link not focused draws nothing outside its bounds. It was built by a
fixer with its tests first and checked by an independent verifier, in three rounds (run
`wf_939ded6a-40e`: 6 agents, 1.35M tokens; the account in
`D:/Projects/Personal/phase8-work/wave3/linkframe/result-rounds.txt`, the last check's crops under
`wave3/linkframe/verify3/`). Round 1's verifier found that the frame, then built round the content,
took clicks: a click 3.5 px left of the link's bounds opened the repository's address. Round 2 laid it
over the content, never hit, and its verifier found that no test held the frame to the keyboard's
focus: with `:focus` for `:focus-visible` a click drew 870 pixels of the guide link's frame, and every
test passed. Round 3 added the cases of a link focused by a click, and its verifier found no fault.
`LinkFocusFrameTests` (App.Tests, 12 cases: the About links focused by Tab, in dark and light, three;
GitLab's two sign-in links focused by Shift+Tab, two; the frame moving nothing, one; the pointer on the
frame, one; the About links focused by a click, three; the sign-in links focused by a click, two), with
the helper `LinkFrame` in `VisualCommit.Testing`, and A1's step 9 (`AssertFramedLink`) hold it: each
of four mutations fails some of them, the old theme (seven cases and A1's step 9), no air beside the
text (seven cases), a frame that is hit (the pointer's case) and `:focus` for `:focus-visible` (the
five cases of a click).

The lead's checks on the merged tree, whose code is the verified `5177cec`'s (the diff outside `docs`
prints nothing): `LinkFocusFrameTests` 12 of 12, part A's three classes (`Phase8aChecksA`,
`AboutSectionTests`, `AboutEntryTests`) 13 of 13, `Phase7aChecksA` 8 of 8 and `Phase7bChecksA` 9 of 9
(`D:/Projects/Personal/phase8-work/wave3/linkframe/merge/summary.txt`). Of their pictures `a1b`
differs from wave 3's references in (477,372)-(714,394), the link's bounds grown by 4 and 3 (2,952
pixels): the whole text as in `a1a`, the underline across it in rows 388 and 389, the frame whole
round them. Beside it only 7a's `a09b-git-refused` differs, 2 pixels one level apart at (350,206), the
anti-aliasing wave 2's comparison already lists for it. The other eight pictures of part A, the other
23 of 7a's part A and the 29 of 7b's, the sign-in dialogs among them, are the same files.

What the inspectors noted beside the checks, none of it a fault: the settings form's left lies 1 px
right of this report's 336 and 480 (746 and 890 at 1920×1080), within its 2 px, as phase 7a's Accounts
picture `a05a-signed-in` shows too; `u3a` equals phase 4's `c10e-resolver-open` pixel for pixel; in
`u2a` the activity log is open and empty, as the check leaves it (no git write before the picture,
"Show reads" off); the notices' text shows the app's usual thin scroll thumb, `#858585` and 2 px wide,
Fluent's colour, as `k3a`'s palette and `k1a`'s diff do, which A5 does not name; in `u1c` the graph's
focus frame covers the top 3 px of the selected first row, the same `:focus-visible` adorner as in
phase 6's `k12a`.

**The references** of the later merges are this run's 1,235 scripted pictures,
`D:/Projects/Personal/phase8-work/references/wave3/` (their hashes in `wave3-pictures.sha256` beside
it), with `a1b-about-link-focus` there the fix's picture (SHA-256 `ad85f645…`; the list beside them
corrected).

**The lagged list** (design 0.4, `VISUALCOMMIT_WATCHER_LAG_MS=1500`, on `85e2884`'s code, 20:54 to
22:18, each command's time with the lock and its build): `Phase2Checks` 25 of 25 (227 s); the fifteen
App.Tests classes 207 of 207 (868 s); phase 6's five filters 21 of 21 (563 s); phase 7a's walk-through
205 of 205 (688 s); phase 7b's 178 of 178 (583 s); the twelve Hosting.Tests classes 126 of 126 (1,473
s); the 8a project 144 of 144 (665 s).

## The performance series (design 10.3.6; D457)

Phase 8a's performance pass (§10; D436 to D438) measures M2 to M7 (design 10.3.4) with the opt-in
class `Phase8aPerformance`, on the laptop, in the Release build, outside the gate; M1, Q1, stays in
phase 1's real-window pass (below). Times here are the laptop's, indications for any other machine.

**How a series is taken.** The lead, the owner told, runs
`VISUALCOMMIT_PERF_RUNS=5 bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase8aPerformance"`
(CLAUDE.md's row): five counted runs of each measure, M2 and M3 each after one uncounted warm-up
launch, M4 in M5's launches (D437). It writes `artifacts/perf/phase-8a/series-<date>-<commit>.txt`.
From now on (D457 (2)) the real-window project is built in Release on the very commit at least ten
minutes before, nothing is committed in between, and Windows' Task Scheduler is read for what is due
in the next ten minutes and, afterwards, for what ran (CLAUDE.md's note). The reason: a start straight
after a build is slow; a commit made after the build makes the command build again, since the build
is stamped with the commit; and a scheduled task with an idle trigger, which a rested and untouched
laptop invites, ran beside one series (D457 (1); plan.md's risk 10).

**The three series** (D457 (1)), on 2026-11-22, five runs each, git 2.56.0.windows.4; each file with
its `summary.txt` copied to `D:/Projects/Personal/phase8-work/wave3/perf-before/`, `perf-before-2/`
and `perf-before-3/`. Between them only docs, the link button's theme and its tests changed. None was
quiet throughout:
1. 00:47 on `86ef5b0`, 7 min 8 s (549 s with the lock and the build), the laptop rested from 00:33.
   The command built first, since the merges after §10's first run had changed the projects, so M2 ran
   within two minutes of a Release build, which its windows show (opened after 9.2, 5.3, 4.9, 5.1 and
   4.1 s).
2. 04:59 on `fcf8c1e`, 7 min 4 s (433 s with the lock and the build's check; the Release build made
   at 04:41). Office's updater (`\Microsoft\Office\Office Automatic Updates 2.0`, a task with an idle
   trigger) started at 05:00:45, as M2 ended and M3 began; two small `SoftLanding` tasks ran at
   05:06:01, during M7.
3. 05:53 on `1afb980`, 7 min 20 s (520 s with the lock, of which 82 s a Release build: the status
   commit `1afb980`, made after the build of 04:41, changed the commit the build is stamped with), so
   M2 again ran straight after a build. The scheduler ran Windows Error Reporting's `QueueReporting`
   at 05:52:08 and the Customer Experience Program's `Consolidator` at 06:00:01.

One row per measure (design 10.3.6's step 5): before, the median and the range of the five runs of
each series, a disturbed take marked; after; the bound; the trigger with its numbers; the decision
(D457 (3), (4)).

| Measure | Before: series 1, 2 and 3 | After | Bound | Trigger | Decision (D457) |
|---|---|---|---|---|---|
| M2: the start with one restored 100,000-commit tab, from the process's start to its first rows | 1: 7,825 ms (6,753 to 13,898), after a build; 2: 4,621 ms (4,570 to 4,872); 3: 8,184 ms (6,630 to 10,368), after a build | no change | 6,100 ms, from the second series alone (4,872) | none but the bound | None. A bound drawn from a series that ran straight after a build guards nothing, so M2's comes from the quiet take; if the unchanged build's M2 passes it in a series taken as D457 (2) says, it is raised by a ruling with the numbers |
| M3: the start with ten such tabs, the active one's first rows | 1: 6,759 ms (6,622 to 9,556); 2: 9,367 ms (7,822 to 10,988), with Office's updater; 3: 8,284 ms (6,523 to 12,776) | after ruling 76's change, in wave 4 | unset until the change's series | the median above 1.25 times M2's median. The series' own lines compare within one series: not fired in the first (against 9,781.3 ms) and the third (10,230 ms), whose M2 was disturbed, fired in the second (5,776.3 ms). Against 1.25 times the quiet M2 (4,621 ms), 5,776 ms, the fastest of M3's fifteen runs, 6,523 ms, lies above: **fired** (D457 (4)) | A change in wave 4: ruling 76, the restore order (below) |
| M4 after the load: the working set 2 s after the `loaded` line, in M5's launches | 1: 286.3 MB (285.8 to 286.8); 2: 286.5 MB (286.2 to 289.7); 3: 287.4 MB (286.2 to 288.9) | no change | 370 MB (289.7) | read in the peak's row | None |
| M4's peak after the reload: the peak working set 2 s after the reload line | 1: 346 MB (344.9 to 348.2); 2: 346.9 MB (346.3 to 348.3); 3: 351.1 MB (345.2 to 352) | no change | 440 MB (352) | the peak's median above twice the median after the load: 346 against 572.6, 346.9 against 573, 351.1 against 574.9 MB, not fired | None: not fired |
| M5: the reload after an outside commit, from git's return to the reload line; and the reload line's own time (refs read to swap) | 1: 3,232.7 ms (3,066.6 to 3,668.9), the line's median 2,927 ms; 2: 3,227.5 ms (3,057.6 to 3,573.9), the line's 2,928 ms; 3: 2,839.1 ms (2,623.6 to 3,157.7), the line's 2,547 ms | no change | 4,600 ms (3,668.9) | the reload line's median above 2,000 ms: **fired** in every series | To plan.md's Backlog: no stall was seen, about nine tenths of the reload is the one `git log --date-order` of D47, and every change that would cut it amends D47 or D52 |
| M6: the reload after a fetch, from the click on Fetch to the reload line; and the reload line's own time | 1: 6,018.2 ms (5,447.9 to 6,321.2), the line's median 3,418 ms; 2: 5,932.8 ms (4,855.6 to 6,578.1), the line's 3,004 ms; 3: 6,643.2 ms (5,996.6 to 7,024.9), the line's 4,013 ms | no change | 8,800 ms (7,024.9) | as M5's: **fired** in every series | To the Backlog, with M5 |
| M7: Pull's settings reads, the median of 20 calls of `ReadPullConfigurationAsync("main")`, no window | 1: 326.7 ms (295.7 to 338); 2: 464.6 ms (439.6 to 499.7), two small `SoftLanding` tasks beside it; 3: 316.2 ms (279.3 to 355.3) | no change | 630 ms (499.7) | the median at 250 ms or more: **fired** in every series | To the Backlog: S8's batch would save 2 of a Pull's 23 git calls, about 0.2 s of a plain Pull on Windows and nothing of Pull (rebase), and would rewrite the reader that decides how Pull integrates and about 19 earlier tests in 11 files |

Each bound is the slowest counted run, in brackets, times 1.25, rounded up (times to 100 ms, M7's to
10 ms, memory to 10 MB; design 10.3.6's step 2): M2's from the second series alone, the others from
all fifteen runs, which no build touched (D457 (3)). The bounds D457 gives go into `Phase8aPerformance`
in a commit of their own; each counted run of a later series is held to them.

**The reload's stall probe** (the lead's, by hand, outside any test, 00:59;
`D:/Projects/Personal/phase8-work/wave3/reload-probe/reload_stall_probe.ps1`, its result
`wave3/reload-probe-run1.txt`): the Release build on a clone of the tests' 100,000-commit repository,
with a data folder, a git configuration and a keychain of its own; three times an empty commit made
outside the app, and meanwhile the window's thread asked an empty message every 15 ms until 1.5 s
after the reload line, the time of each answer kept. During the three reloads (3,433, 2,997 and 3,444
ms by their lines) the longest answer took 36.1 ms, the 95th percentile at most 2.4 ms, and none 50 ms
or more; idle, the longest took 24.7 ms. No stall was seen, which is what design 10.3.6's step 3 asks
before the reload goes to the backlog.

**The probes of the start with several tabs** (the lead's, by hand, outside any test, 06:01 to 06:08;
`D:/Projects/Personal/phase8-work/wave3/startup10-probe/`: the script `startup_tabs_probe.ps1`,
`run1.txt`, `run2-small.txt` and the data folders with the app's logs): the Release build started on a
data folder of its own whose session restores clones of the tests' 100,000-commit repository, the
active tab first, with a git configuration and a keychain of its own and the providers off; no input;
from the moment the window existed until every tab's `loaded` line, the window's thread asked an empty
message every 15 ms. `run1.txt` set one large tab against ten large tabs, `run2-small.txt` one large tab
against one large tab beside nine tabs of 2,000 commits, the cases alternately, three times each. Their
numbers are indications: alternated, but few. By the active tab's own clock (its line `first graph rows
drawn after`, D50's and Q1's quantity), a 100,000-commit tab restored alone showed its first rows after
2,105 ms (the warm-up), 2,557, 2,686 and 2,310 ms in the first probe and 1,771, 2,189 and 2,813 ms in
the second; beside nine more such tabs after 6,644, 5,044 and 5,219 ms; beside nine tabs of 2,000
commits after 4,287, 6,145 and 7,763 ms. The delay lies before the active tab's refs are read (its
`refs read after` 3.2 to 6.2 s, against 0.7 to 1.4 s alone): in the about eighty git processes that
ten openings start at once, not in the nine large walks (D457). The window's thread answered after up
to 435, 1,148 and 483 ms with ten large tabs (13, 19 and 16 answers of 50 ms or more), up to 304, 420
and 581 ms with nine small ones (7, 10 and 11), and up to 164 to 349 ms with one tab. All loads had
ended after 22 to 28 s with ten large tabs and after 10 to 15 s with nine small ones. So the cost falls
on every Windows session with many restored tabs, on the quantity Q1 names, for the tab the user looks
at (D457). The probes' app found `C:\Program Files\Git\cmd\git.exe`, Git for Windows' launcher, as a
user's desktop does; the series and the real-window pass start the app from Git Bash, whose PATH finds
git itself (`ucrt64\bin\git.exe`).

**The two panels**, read only, each of four analysts and then three judges with different lenses (the
user's, the phase's and the rules'): run `wf_eb13d24d-e57` on M5, M6 and M7 (7 agents, 1.88M tokens)
and run `wf_23f00a94-3d2` on M3 (7 agents, 1.99M tokens), their accounts in
`D:/Projects/Personal/phase8-work/wave3/perf-triggers-result.json` and `perf-m3-result.json`. The lead
took their advice before each ruling (D457). The reload (M5, M6): all three judges for a backlog row.
Pull's reads (M7): the user's and the phase's judges for a backlog row, the rules' judge for the batch
in wave 4, because the 250 ms had been set before the series as the line between the batch and the
backlog, and every reason given for a row (the gain's size, the tests it rewrites, the alternation's
time) had been weighed when the line was set, so a row would undo by taste after the measure what the
trigger was set to decide. The restore (M3), on the series alone: the user's and the phase's judges
for a backlog row, the rules' judge for a change, the active tab first. Each of the two for the backlog
named what would change the answer: a stall of the window during a ten-tab start (an answer of 100 ms
or more, by the user's judge; one of 250 ms or more, or several of 50 ms or more, by the phase's), or
the same delay with ordinary background tabs (the shown tab about 0.5 s or more slower beside nine of
1,000 to 10,000 commits, by the user's; of about 10,000, by the phase's). The probes showed both.

**Ruling 76, the restore order** (D457 (5)), which wave 4 builds and proves. When the session's active
restored tab is a repository's, it opens first and alone. The other restored tabs open at the earliest
of: its graph has its first page; its opening or its first load fails or ends without a page; its tab
is closed; 5 s have passed on the session's clock. They then open at most four at once, as "Add
repositories" opens and a dashboard reads (D207); the package measures the window's answers and may
lower the four with its numbers. A waiting tab that the user shows opens at once; a waiting tab that is
closed never opens. When the active tab is a dashboard or holds no repository, every restored tab
opens at once, as today. `InitializeAsync` keeps its contract (it completes when every restored tab
has loaded its history, `MainWindowViewModel.cs:171-176`), so no harness wait changes; the start-up
line still counts every restored tab; the merge tools' lookup still starts when the first restored
load ends (D202). The package `restore` (`wip/p8a-restore`): a design section first; its tests written
first and failing on today's code; `SettingsEntryTests`' test of the merge-tool lookup rewritten (it
asks for both openings at once); a review; and the proof D436 asks, M2 and M3 with the two builds run
alternately, five times each, taken as D457 (2) says, with the lead's probe (the tab's own clock and
the window's answers, beside nine large and beside nine small tabs). It is proven when the fix's M3
median is at or under 1.25 times its M2 median while the base's is above, and the fix's M2 lies within
the base's range; if the alternation does not show it, the change is reverted and the row goes to the
Backlog with the numbers. Its cost belongs to the week's share the owner is asked for before wave 4
(D241). Not chosen: design 10.3.6's own candidate, background tabs in summary mode promoted when
shown, which breaks that contract and the checks that wait on it, puts a whole load on the first
switch to such a tab and moves D289's reads; it goes to plan.md's Backlog with the memory it would
save (about 800 MB of working set with ten 100,000-commit tabs, against 286 MB with one).

**What reopens the backlog rows** (plan.md's Backlog). Pull's batch: a rested median of about 600 ms,
or the owner (D457). The reload: D457 and the row name no measure; a change to it amends D47 or D52,
and design 10.3.6's step 3 sent it to the backlog because no stall was seen. Background tabs in
summary mode: the row names none; ruling 76 orders the openings instead.

**The one cost D457 records for the reload**, in the backlog's row: an operation that moves a ref
keeps its running text until the reload ends, because the queue awaits the refresh
(`OperationQueue.cs:603-617`), so in a 100,000-commit history a fetch shows as running for about 6 s.

**M1 (Q1)** is not in the series: phase 1's real-window pass asserts it at every gate
(`Phase1RealWindowPass.Checks_8_and_9_the_100k_commit_repo_and_Q1`: the first rows within 2,000 ms,
the frames' 95th percentile within 8 ms and the longest within 33 ms, D50), and the gate's pass keeps
it there alone (D436). That pass, like the series, starts the app from Git Bash, whose PATH finds git
itself (`ucrt64\bin\git.exe`), where a user who starts the app from the desktop finds `cmd\git.exe`,
Git for Windows' launcher, which starts git as a child. By the tab's clock Q1 was 1,771 to 2,813 ms in
the probes (1,970 ms in the stall probe of 00:59), against 1,400 to 1,827 ms in the last gates' passes,
so the pass does not measure what an installed user gets (D457): the review before the gate has it
(status.md's step 19; open point 98).

## Found by the gate

Not yet.

## Could not run

Not yet.

## Other platforms

Not yet.

## macOS checklist for the owner

Not yet. Design 0.5 and 4.6 send macOS's second start to 8b's Mac checklist, and part A sends About in
macOS's application menu there (`About VisualCommit` opening About, no `About Avalonia`; design 0.5,
8.9 item 2). Phase 8a's lines in that checklist:

- macOS's second start (design 0.5, 4.6)
- About in the application menu (design 8.3.3, 8.9 item 2)

## Open points

What the lead settled, then where the design is silent or leaves a value to its builder about parts L
and I, with the reading this report takes; then parts F, K, B, A and U, each under its own heading,
their points numbered on. The builder of the part, or the lead, settles each open point before the
part's merge.

Settled by the lead on 2026-11-20. Where this report's first draft found the design at odds with
itself, the lead ruled and corrected the design; the report follows each ruling:
- **L6's pictures.** One picture per state, `l6a` to `l6m`, `l6a` to `l6c` being 2.5's a) to c), as
  the real window names them; `l6h` is the file history's row, not the light sub-step (design 2.5).
- **A box in the real window.** The real window reads the text typed into the focused `FilterBox`,
  less the caret's column, and every other box unfocused (design 12.3.3). Text is typed into
  `FilterBox` in launch 1 alone, so the reads are L1's `sea` (`#E4E7EC`) and L2's (`#1B1F27`), both
  in launch 1 (12.3.4 launch 1).
- **I2's real captures.** Three, `i2a`, `i2b` and `i2c`, each compared with its folder line masked
  (design 4.7, 12.3.4, 12.5).
- **I2's input.** In launches 5 and 6 the real window presses Try again with a real Enter, as the twin
  does (4.6), so the focus frame in `i2b` and `i2c` matches. Because a real key goes to the foreground
  window and the test host is not the foreground process (4.3.3), each launch first asserts that the
  notice is the foreground window and that Try again has the focus, clicks the notice's title bar if
  not, and fails before any key if it still does not hold (design 12.3.4 launches 5 and 6).
- **The notice's help text.** The window's `AutomationProperties.HelpText` is the body followed by the
  platform's sentence, one space between (design 4.3.4); its automation name is the heading. D448's
  "its heading and its body" is read so.
- **The fake try.** `InstanceNoticeHost.Show` takes the platform, the folder and the theme, and §4's
  builder may add optional parameters after them, such as the try that I2 holds and answers (design
  1.11). Part I needs only that a try can be held, then answered `NoAnswer`, and that a check can see
  it was never asked.
- **8a's own pictures that later packages change** (design 0.7, corrected the same day):
  `l3a-palette-placeholder` in the palette's scroll thumb, which §8's and §11's commands move (ruling
  54), and `l3f-settings-name-placeholder` in the settings screen, where §5's Git program row and §8's
  About nav button land (rulings 40, 3). The lead's `--expected` file for each of those merges names
  them.

Still open:
1. **L1's and L2's focused empty box** has no picture in 2.5; its ink is read on an unsaved frame
   (`app.Capture()`).
2. **L4 b's entry.** 2.5 says "a destructive entry highlighted". This report types `delete main` and
   takes its first row, `Delete main on origin…` · `origin/main`, as phase 6's K10 lists it on "ops"; the
   builder confirms the rows on "graph".
3. **L6 e's and f's routes.** "`main` compared with `origin/main`" is taken as From `origin/main`, To
   `main` (the drop's `Compare main with origin/main`), the direction in which "Add settings page"'s
   rename lies; `origin/main`'s own menu item compares the other way. That the comparison lists the
   rename is the builder's to confirm (2.5), and so is the file history's "R" on the rename's row.
4. **L3's Pick dialog** is reached by `Set upstream…` on `feature/login` (phase 3's check 28), not by the
   settings screen's `Choose…` (phase 6's L31), so that it lies in no region a later package changes.
5. **I2's real-window Warning.** The Warning `… did not answer (no instance.json).` in launch 5 is this
   report's addition from 4.3.5 and 4.5's test 3; 12.3.4 does not name it.
6. **Values the design leaves open.** No position or height of the notice's parts is fixed beyond
   width 480 and padding 20, so the checks assert order, one line for the folder and the heights'
   relations, not pixels. The light backgrounds of L6 h (`#F5F6F8`, `#DEE2E9`, `#DDE1FA`) are
   `Tokens.axaml`'s, which the round keeps; 1.5 gives only the hover ratio, 4.66. The comparison's
   growth and join (2.9 item 4, 13.4 item 3) and full-coverage ink at 200 % (2.9 item 6) are unverified
   until wave 1's comparison and D246's runs.

### Part F: first run: git's detection, guidance and setting

Settled before this part was written: winget is offered for a missing and for a too-old git, not for
one that does not work (D453, the owner's answer of 2026-11-20; 13.4 item 22); the ssh line comes once
per session (D424; 13.4 item 6); the laptop has `winget.exe` on its `PATH`, and the real window's F1, F2
and F5 fail without it, never "Not run" (D449; 13.4 item 7 in part).

Where the design's sections differ about this part, with the reading this report takes (the lead
confirms):
- **F4's route.** 5.5 reaches F4 by "Check again" with the probe's next answer; 12.3.4 launch 7 (and 0.5)
  by Choose through the native Open dialog. The twins end in the same `f4a`, but the status bar's
  tooltip (`… found on the PATH` against `… chosen in Settings`) and `settings.json` (no `gitExecutable`
  against `git`) differ. This report follows each section in its own harness; F8 covers Choose in the
  scripted run.
- **"Every new screen has a real-window picture: the first-run screen (F1, F4, F5, F6)"** (0.5): F4's
  capture, `f4a-found-again`, shows the screen gone (5.5, 12.3.4); the first-run screen's real
  captures are F1's, F5's and F6's, and its confirm F2's. **Settled by the lead on 2026-11-20:** 0.5 now
  reads "the first-run screen (F1, F5, F6)".
- **`GitStatus`'s tooltip** (5.3) against its help text (12.3.4 launches 7 and 8): open point 14.
  **Settled by the lead on 2026-11-20:** 5.3 gives `GitStatus` a help text equal to its tooltip's text
  (open point 14).
- **The earlier pictures §5 changes.** 5.6 says "none else" and its builder's runs show only phase 6's
  nine; 0.7 also names part L's own `l3f-settings-name-placeholder` at §5's merge, which 5.6's runs do
  not take. Both hold: `l3f` is 8a's own picture, named in the lead's `--expected` file.
- **The download for a git that does not work.** D405 and D453 (the owner's) give a git that does not
  work the download; 5.3's table shows it only "without a fault", a narrowing that no decision or
  ruling records (it has been there since the draft, `85169db`). F6 follows D405 and D453: `Download
  Git for Windows` shows with a configuration fault. The lead corrects 5.3's row before §5's builder
  starts, or, to keep the narrowing, asks the owner, since only the owner amends D405 and D453; the
  report's F6 then follows that decision. **Settled by the lead on 2026-11-20:** the design now says
  what the report says (FD3): 5.3's row of `GitSetupDownloadButton` reads "W Missing, TooOld,
  NotWorking, ChosenMissing without a candidate", and 12.3.4 launch 9 names both buttons as shown and
  never pressed; with a fault the guidance names the file's line as the cure, and the download stays as
  D405 offers it. No open point remains on it.

Still open, each with the reading this report takes and who settles it before §5's merge; a point the
lead settled on 2026-11-20 says so:

7. **The buttons' order on the screen, and F13's.** 5.3 lists the buttons in a table and lays out
   "buttons" as one block, but says nowhere that the table's order is their order on the screen; 5.5
   leaves "F13's focus order with winget found" to `Phase8aExpectations.F`. This report reads 5.3's
   table's order: winget, download, tools, `Show the file`, Choose, `Use the Git…`, `Check again`,
   `Continue without it`, and for F13 `GitSetupWingetButton`, `GitSetupDownloadButton`,
   `GitSetupChooseButton`, `GitSetupCheckAgainButton`; and the links the same way, 5.3's table order:
   `GitSetupMoreLink` before `GitSetupGuideLink` (F9 a, F10). §5's builder confirms; the lead rules if
   the screen lays them out otherwise, and the report's F13 and every "in order" follow. **Settled by §5's
   build (merged at `53fc353`):** as this report reads it. The buttons sit in 5.3's table order in the panel
   `GitSetupButtons`, the links in `GitSetupLinks`, `GitSetupMoreLink` first; F13's order with winget found is
   `GitSetupWingetButton`, `GitSetupDownloadButton`, `GitSetupChooseButton`, `GitSetupCheckAgainButton`
   (`Phase8aExpectations.F.FocusOrderWithWinget`).
8. **The fake's values.** 5.3 gives `C:\Program Files\Git\cmd\git.exe`, `C:/Users/demo/.gitconfig`,
   Ubuntu's three values and "Alpine". This report adds the conventions' table: winget's path, `/usr/bin/git`
   for the macOS and Linux states, `2.29.0` for every too-old git (F5's, 1.14's default), Alpine's `ID`
   `alpine` with no `ID_LIKE`, the source `Path` unless named, and the helper's path in F11. It leaves to
   §5's builder the fake's start failure (F7's `{message}`) and its text starting `xcrun: error` (F9 b),
   which `Phase8aExpectations.F` records and the checks quote. §5's builder fixes them in
   `FakeGitSetupProbe` and `Phase8aExpectations.F`; the lead quotes them here before the merge. §5's
   builder also gives `FakeGitSetupProbe` the conventions' rule for its lookups, which 1.15 does not
   state and F8's and F12's counts rest on: an `AnswerFor` entry answers every lookup whose
   `ChosenPath` is its path, probe or search alike (the two reach it as the same call), and every
   lookup it receives is recorded in `Lookups`, in order. **Settled by the lead on 2026-11-20** for the
   lookups' rule: 1.15's `FakeGitSetupProbe` row now states it (FD2): `AnswerFor` answers every lookup
   whose `ChosenPath` is the path, and `Lookups` holds every lookup received, probes among them, in order.
   **Settled by §5's build (merged at `53fc353`)** for the rest: F7's `{message}` is `The system cannot find
   the file specified.`; F9 b's words are `xcrun: error: invalid active developer path
   (/Library/Developer/CommandLineTools), missing xcrun at:
   /Library/Developer/CommandLineTools/usr/bin/xcrun`, with exit code 1; Alpine's `PRETTY_NAME` is `Alpine
   Linux v3.20`, which no check shows (`alpine` names no command, so the guidance is the unknown one).
   `FakeGitSetupProbe` holds them, and `Phase8aExpectations.F` as `NotStartedMessage` and `ToolsBrokenWords`.
9. **A chosen path on macOS and Linux.** `GitSetupTexts` applies the lookup's rule to a chosen path
   (5.3: a full path, on Windows ending in `.exe`, D449), and `Path.IsPathFullyQualified` is the running
   platform's: on CI's macOS and Linux jobs 5.5's `D:\Tools\PortableGit\cmd\git.exe` is no full path, so
   F3 would show the "runs Git only from a full path" body there, and the settings row could too. This
   report gives F3, F8 and F12 a chosen path per platform (the conventions' table), keeping `.exe` off
   Windows too so that Choose's check passes whether "on Windows" (5.3) means the guidance's platform or
   the machine's. The lead rules (a path per platform, or a rule read from the guidance's platform).
   **Settled by the lead on 2026-11-20:** a chosen path per platform, by the machine's rule. In 5.3's
   lookup rule (D449) "on Windows" means the machine the app runs on (`OperatingSystem.IsWindows()`),
   and "a full path" is `Path.IsPathFullyQualified` on that machine; the guidance's platform chooses
   texts only. So a test gives a chosen path that is a full path on the machine it runs on: the
   conventions' table of paths per platform.
10. **The winget start's folder off Windows.** `Path.GetDirectoryName` of a Windows path is empty on
    macOS and Linux, where F2's method 2 also runs; the check expects `Path.GetDirectoryName(WingetPath)`
    as the running platform computes it, `C:\Users\demo\AppData\Local\Microsoft\WindowsApps` on Windows.
    The lead, if the folder should be asserted as a literal on Windows alone.
    **Settled by the lead on 2026-11-20:** the working folder is `Path.GetDirectoryName(wingetPath)`
    (1.3, 5.3); a test asserts the literal folder on Windows and that expression's value elsewhere, as
    F2's method 2 now does.
11. **Who writes `Started in a terminal:` and `Started: /usr/bin/xcode-select --install`.** 5.3 gives
    the lines but not whether the screen's view model or `FileLauncher.StartAsync` writes them; written
    by the launcher, the fake (which records and starts nothing) never shows them. The scripted checks
    assert them only if the view model writes them, and always the fake's `Starts`; the real window
    asserts the first's absence after Cancel either way. §5's builder. **Settled by §5's build (merged at
    `53fc353`):** the screen's view model writes both lines, once the launcher's `StartAsync` has returned;
    `FileLauncher.StartAsync` logs nothing. So the scripted checks see each line once, beside the fake's
    `Starts` (F2 method 2, F9 step 2).
12. **Transient states.** `Checking…` on `GitSetupCheckAgainButton`, `Looking for Git...` in
    `GitStatus` (F4, F13) and `Checking…` in `GitSetupError` (F8) last while the probe answers; 1.15's
    fake has no hold, so a check cannot wait inside the search. §5's builder adds an optional held answer
    to `FakeGitSetupProbe` (as part I's `HeldNoticeTry`), or the check reads them in the dispatcher turn
    of the click; without either, those three expectations are read by `CheckGitAgainTests` and
    `GitSetupViewModelTests` alone, and the report says so. **Settled by §5's build (merged at `53fc353`):** a
    held answer. `FakeGitSetupProbe.HoldNext()` holds the next lookup, once recorded, until its
    `ProbeHold.Release()`. F4 and F13 assert `Checking…` as the automation name of the disabled
    `GitSetupCheckAgainButton` and `Looking for Git...` in `GitStatus` while the search is held; F8 asserts
    `Checking…` in `GitSetupError` while Choose's probe is held.
13. **"Git said:" for a git that could not be started** (F7). 5.3 puts the start's message in the body
    (`… could not be started: {message}`) and `ErrorText` holds the same message (1.2); it does not say
    whether "Git said:" and `GitSetupGitSaid` show it too. §5's builder; the lead, before F7's expected
    result is completed. **Settled by §5's build (merged at `53fc353`):** the start's message is in the body
    alone; "Git said:" and `GitSetupGitSaid` are not shown (F7 asserts it). A search that throws, which has no
    path, shows its message under "Git said:".
14. **`GitStatus`'s help text.** 5.3 gives `GitStatus` a `ToolTip.Tip`; 12.3.4 (launches 7 and 8)
    reads its help text through UI Automation. This report expects both, equal, as the app's other
    tooltipped controls set both. §5's builder.
    **Settled by the lead on 2026-11-20:** 5.3 gives `GitStatus` `AutomationProperties.HelpText` equal to
    its tooltip's text (`GitStatusToolTip`), since 12.3.4's launches 7 and 8 read it through UI
    Automation; no picture changes.
15. **The status bar's tooltip in ChosenMissing.** "Null without a path" (5.3): whether the detection of
    a gone choice carries the chosen path as its `ExecutablePath` is not said; F3 does not assert the
    tooltip. §5's builder. **Settled by §5's build (merged at `53fc353`):** it does not: a gone choice's
    detection has no `ExecutablePath`, so in ChosenMissing `GitStatus` (`Chosen Git not found`) has no tooltip
    and no help text.
16. **The row's note after Save** (F12 step 5). This report reads 5.3's "while the choice differs from
    the git in use" as covering a choice saved but not yet used (ruling 42): `Chosen here. VisualCommit
    uses it from its next start.` on reopening. §5's builder; the lead if otherwise. **Settled by §5's build
    (merged at `53fc353`):** as this report reads it; F12 asserts that note after steps 3 and 5.
17. **The picker's title and the `.exe` check in the settings row.** The row has no guidance, so its
    W and M L forms (`Choose git.exe`, `Choose the git program`) follow the running platform in this
    report; the screen's follow the guidance's. §5's builder. **Settled by §5's build (merged at `53fc353`):**
    as this report reads it; every `.exe` refusal, the screen's Choose's too, follows the running machine
    (`OperatingSystem.IsWindows()`).
18. **What F14 waits on before asserting no note.** `HomeHasNetrc` runs off the UI thread (1.10, 5.3);
    an absence can be asserted only after a known end: this report asks that the question be one of the
    screen's reads that `WaitForSettingsReadsAsync` waits for, or a condition of its own. §5's builder.
    **Settled by §5's build (merged at `53fc353`):** one of the screen's reads:
    `SettingsDialogViewModel.Loaded` awaits `AccountsSettingsViewModel.ReadNetrcAsync`, which asks the
    question off the UI thread, so F14 waits for the reads as for any other.
19. **The neutral point and the status bar.** The neutral point (760, 687) lies in the status bar; with
    `GitSetupButton` beside `GitStatus` and a long text (`Git 2.29.0 is too old (needs 2.34.1 or newer)`),
    it may fall on either, so that a picture shows a hover or `GitStatus`'s new tooltip. Every part F
    picture asserts neither; if one fails, the lead moves part F's resting point (5.3 does not place
    `GitSetupButton`). **Settled by §5's build (merged at `53fc353`)** for the scripted run: `GitSetupButton`
    sits left of `GitStatus`, and every part F picture passes both assertions. In `f5a` and `f10c` the neutral
    point lies on `GitStatus`'s text `Git 2.29.0 is too old (needs 2.34.1 or newer)`, which now has a tooltip;
    the scripted run opened none. A real pointer resting there in launch 8 can open it after the hover delay,
    so §12 gives part F a resting point of its own, on no control with a tooltip, for the scripted `f5a` and
    `f10c` and for launch 8, the scripted pictures staying the same files (the lead's ruling, design 12.3.5).
20. **Phase 6's `AssertFits`, and F12.** The global scope with the row must still fit at 1100×700 and
    1920×1080 (phase 6's S14 at `s14c` and `s14i`); if it does not, ruling 3: §5's builder stops and
    asks the lead. F12 asserts no fit of its own (the design fixes none): on "graph" the global scope
    also holds the two notes `graph sets its own: …` under Name and Email (as part L's
    `l3f-settings-name-placeholder` shows), so less room lies free there than in `s14c`, and the row's
    height is §5's builder's to set. If the row does not fit there, `f12a` and `f12b` show a scroll bar,
    and §5's builder tells the lead. **Settled by §5's build (merged at `53fc353`):** it fits. Phase 6's S14
    passed at `s14c` and `s14i` in the builder's run of `*Phase6ChecksS`, and `f12a` and `f12b` show no scroll
    bar; F12 still asserts no fit of its own.
21. **The scenario of F1, F4 and F12.** 5.5 names none; this report takes "graph" (`Scenarios.GraphAsync()`),
    which `Phase8aExpectations.F` names for §12's launch 7. §5's builder may take another before the
    merge, with the report's text changed to match. **Settled by §5's build (merged at `53fc353`):** "graph",
    for methods 1, 2 and 14.
22. **The helper's path in F11.** `askPassHelper` is "a path without a file" (1.10); this report gives
    a made-up path per platform so that `f11a` is the same on every run. If the session turns it into a
    full path of its own (a path relative to the current folder off Windows), §5's builder uses a path
    under the data folder instead and the picture's Notes say it varies by nature. **Settled by §5's build
    (merged at `53fc353`):** the made-up path is kept as given (`Phase8aExpectations.F.MissingHelper`); it is
    a full path on its platform, so the session keeps it.
23. **F14's sign-in.** This report signs in through `SignInAsync` (phase 7a's A11), not the dialog, with
    GitHub's fake in process (no port, ruling 20); §5's builder may take another route that leaves
    `useForGit` saved on. **Settled by §5's build (merged at `53fc353`):** `SignInAsync`, as this report has
    it; step 1 waits until `accounts.json` holds exactly one record, with `useForGit` true, and asserts it is
    GitHub's `octo`.
24. **Launch 7's stand-in Warning.** 12.3.4 asserts the stand-in's Warning in launch 8 alone; this
    report adds launch 7's (`… this computer is treated as having no Git.`, 1.14). §12's builder; the
    lead.
25. **Still open from the design** (13.4 item 7; 5.9; D453): whether `cmd.exe /S /K ""<the path
    found>" --version"`, started with `UseShellExecute`, shows a window that stays open where Windows
    Terminal is the default console, tried by hand by the lead, the owner told, before §5's merge; and
    that winget upgrades a Git for Windows its own installer put there, unverified, which no check can
    show (no check runs winget). 13.4 item 6 is settled (the ssh line once per session, D424).
26. **States no check shows.** 5.5's list has no check of ChosenMissing without a candidate (its
    winget and download buttons, its "none" guidance), of a chosen git that is too old or not working
    (`Use the Git on the PATH` on the screen), of a git that "said nothing and exited with code {n}", or
    of a failed winget or `xcode-select` start in `GitSetupError`. `GitSetupViewModelTests` and
    `GitSetupTextsTests` prove them (5.4), and §9's sweep passes through ChosenMissing (9.3.8, state 10).
    The lead, if a check is wanted.
27. **The netrc note's class** (F14). 5.3 gives the note the classes `settingsNote notice` and
    `VcWarningBrush`; in today's settings view `notice` alone draws in the secondary colour and only
    `TextBlock.notice.warning` sets `VcWarningBrush` (`SettingsDialogView.axaml:646`). F14 therefore
    asserts that the note's classes include `settingsNote` and `notice` and that its brush resolves to
    `VcWarningBrush`, not the exact list of classes. §5's builder chooses between the class `warning`
    and the token set on the note. **Settled by §5's build (merged at `53fc353`):** the class `warning`. The
    note's classes are `settingsNote notice warning`, and the existing `TextBlock.notice.warning` style gives
    `VcWarningBrush`; F14 asserts as written.
28. **The focus after F2's Cancel** (F13, launch 7). Whether F2's Cancel gives `GitSetupWingetButton`
    the focus back depends on how §5 builds the winget command, which the design leaves open (5.3:
    "confirm, then 1.3's start"): a command that is disabled while it awaits its confirm is not given
    the focus back by D104 (the case 6.3.5 handles for other openers). F13 does not depend on which
    way it is built (its step 4 always sends at least one F6), and F2 asserts nothing about the focus
    after Cancel. §5's builder. **Settled by §5's build (merged at `53fc353`):** the winget command allows
    concurrent executions (`GitSetupViewModel.WingetCommand`), so its button stays enabled while its confirm
    is open, and D104 can give it the focus back after Cancel, without the outline. F2 still asserts nothing
    about that focus, and F13 always sends the first F6.
29. **The screen before its guidance.** 5.3 reads the guidance off the UI thread while the screen
    shows, and does not say what the screen shows before it arrives: another platform's texts, no
    winget button, no command. Every check waits for it (the conventions' "Waiting for the screen";
    launches 7 to 10 likewise), and the waits hold either way. The lead's correction of 5.3 (the
    screen shows only once its guidance is read, so `Initialization` and Check again end with the
    screen and its guidance) would settle it; until then §5's builder; the lead.
    **Settled by the lead on 2026-11-20:** the design now says the screen shows only once the guidance is
    read (5.3: it is read once per session, off the UI thread, the first time a state shows the screen;
    12.3.4's launches 7 to 10 wait on `GitSetupGuidance`, launches 7 and 8 also on
    `GitSetupWingetButton`, before they read or capture). The checks keep their waits.

### Part K: the keyboard pass and the focus fixes

Where the design is silent, leaves a value to its builder, or does not match the app it builds on,
with the reading this part takes. The lead settles each before §6's build starts unless it names
another; §12's builder follows the report's steps.

30. **K1's route to `src/Calculator.cs`'s row.** 6.5 ("F6 to the stage row, Enter opens
    `src/Calculator.cs`'s unstaged diff") and 12.3.4 launch 11 ("real F6 until `StageFileRow` of
    `src/Calculator.cs` has the focus") have F6 reach that row; F6 gives the unstaged list's first row,
    `README.md` (6.3.4; `KeyRegionNavigator`; phase 6's `k12b`), and `src/Calculator.cs` is the seventh. This
    part takes F6 and then 18 Tabs along 6.3.4's order (each row's own button, its `Stage`, its
    `Discard`), so that only the keyboard reaches the diff, as the outline means. The count is derived
    from the scenario's seven unstaged rows; §6's builder confirms it, and that the walk reaches the rows
    below the list's visible part (the list virtualizes; `l2a`'s list had to be scrolled for
    `src/Calculator.cs`). The other way, a click on the row, would leave K1's start to the mouse. The lead.
    **Settled by the lead on 2026-11-20:** K1's route as this part has it: F6 gives the unstaged list's
    first row, and `src/Calculator.cs` is the seventh; the walk is F6 and then 18 Tabs along 6.3.4's order
    (each row's own button, its Stage, its Discard). The design's 6.5 and 12.3.4 launch 11 follow; §6's
    builder confirms the counts, and `Phase8aExpectations.K` holds them. **Confirmed by §6's build**
    (merged in `e463392`): 18 Tabs (`Phase8aExpectations.K.TabsToCalculator`; the row and id each Tab
    reaches in `CalculatorWalk`). The walk reaches the rows below the list's view because
    `WorkingChangesView` walks the lists in their rows' order (design 6.3.4; `55b8d3a`); Avalonia's own
    order stopped at the rows the list had made.
31. **K1's chosen lines.** 6.5 and 12.3.4 write "F6, Down, Shift+Down twice, Shift+F10" after the
    Shift+Tab to `Discard file`. F6 from the toolbar goes to the next region, the right panel, and the
    graph area gives back its last focused element, the toolbar's button (D213; `KeyRegionNavigator`), so
    F6 cannot put the focus back in the text; and from where the caret is after the re-read, one Down and
    two Shift+Downs would select context rows of the remaining hunk, which stage nothing. This part takes
    Tab back into the text (6.3.1's order), Ctrl+Home, four Downs and two Shift+Downs, which select rows 4
    and 5 of the remaining hunk (its header row 0, then git's three context lines): the added empty line
    and `Negate`, so that "two lines" are staged and the `--numstat` lines are fixed (`3 1` staged, `2 0`
    unstaged). The rows are derived from the scenario and git's default context; §6's builder confirms
    them, and `Phase8aExpectations.K` holds the counts and lines (6.5). The lead.
    **Settled by the lead on 2026-11-20:** K1's lines as this part has them: chosen by Tab back into the
    text, Ctrl+Home, four Downs and two Shift+Downs (rows 4 and 5 of the remaining hunk), not by "F6,
    Down, Shift+Down twice". The design's 6.5 and 12.3.4 launch 11 follow; §6's builder confirms the
    counts, and `Phase8aExpectations.K` holds them. **Confirmed by §6's build:** `DownsToLines` 4 and
    `ShiftDownsForLines` 2 select the added empty line and `    public int Negate(int a) => -a;`; git
    then prints `3 1` staged and `2 0` unstaged (`StagedAfterLines`, `UnstagedAfterLines`), and the
    index's file is `IndexAfterLines`.
32. **K2's run 2 and its count of Down.** 6.3.2 numbers the runs from 0 ("run 0's menu"), so run 2 is the
    third run, line 4, `0575c7f`; keyboard.md's test (a), which the design draws on, says "run 2:
    `SelectedRun.Index == 1`". This part takes index 2: 6.5's "its commit in the right panel" holds only for
    it, run 1 (line 3) being not committed yet. The count, three Downs, takes the caret on line 1 when the
    page opens: 6.3.2 says opening picks nothing, not where the caret is. §6's builder confirms both, and
    `Phase8aExpectations.K` holds them (6.5). The real window waits for each Down by the right panel, not
    by the caret (open point 41). **Settled by §6's build** as this part reads it: run 2 is index 2
    (`0575c7f`, line 4); the page opens with the caret on line 1 and no run picked; three Downs
    (`DownsToRun2`), the run, line and subject of each in `BlameDowns` (no subject for run 1, where
    the stage panel shows).
33. **K5's keys.** 6.5 writes "From GitHub…" by Tab and Enter, and "a row, Enter". The button lies before
    `CloneUrlBox` in the Tab order (`WelcomeView.axaml`'s `DockPanel`), and the form opens with the focus in
    the box (phase 7a's B1), so this part takes one Shift+Tab. For the row it takes Tab into the list and
    Home, which selects the first row, `octo/hello`, whatever row the Tab left focused, so that the form's
    values are `b06a`'s; 6.3.9's inventory writes "arrows". How `k5a` is held against `b06a` ("`b06a`'s form
    outside the URL's text", 6.5) the design does not say: this part leaves it to the gate's inspection,
    the URL's port differing by run; a masked comparison would need a box the lead names.
    **Settled by §6's build:** steps 3 and 5 as this part has them: one Shift+Tab from `CloneUrlBox`
    gives `From GitHub…` the focus with `:focus-visible`; Tab from the filter enters the list, and Home
    selects `octo/hello` and enables `Choose`. `k5a` against `b06a` stays the gate's inspection.
34. **K7's "type Next".** The summary holds `First` while the commit is held; typed onto it, `Next` would
    give `FirstNext`. This part selects the summary's text first (Ctrl+A), so that the summary reads `Next`
    as 6.5 expects. **Settled by §6's build** as this part has it: Ctrl+A, then `Next`; the summary
    reads `Next` (`SummaryCounter` `68`) and HEAD's message is `First`.
35. **K8.** "With K3's held fetch" is read as a second fetch held the same way: K3's own fetch ends at K3's
    Enter. 6.5 puts the numbers of Tabs "in the report" but gives no bound for the walk to `LeftSplitter`:
    this part bounds both walks by 6.4's 200 (G7's bound) and records both numbers. After Enter on Cancel
    the button hides as the fetch ends, and the design does not say where the focus goes, so the walk to
    `LeftSplitter` starts from F6 to the right panel again. The 10 per press is keyboard.md's, unverified
    until G8's test (6.9 item 3); whether D427's code is needed is that test's to say (6.1), and the
    expected result, the session's `LeftPanelWidth` 270, holds either way. **Settled by §6's build** as
    this part reads it: a second fetch held by `Arm()`, both walks bounded by 200 (`WalkLimit`), both
    numbers written to the test's output. On the build's runs 8 Tabs reached `OperationCancelButton`
    from where F6 put the focus in the right panel, and, after F6 to the right panel again, 1 Tab
    reached `LeftSplitter`: the splitters come after the panels in `MainWindow.axaml`, before the status
    bar. `GridSplitter`'s Right moves 10 and raises no drag's end, so D427's code was needed (the test
    failed first on the base, the session's width unset): `MainWindow` keeps a keyboard move of
    `LeftSplitter` and `RightSplitter` as a drag's end, and the session's `LeftPanelWidth` is 270.
36. **K3's held fetch.** 6.4's `StatusBarTests` hold a "Fetch origin" queued straight on the tab; a scripted
    check starts the real one from the remote's menu and holds it at the remote (`FetchHold`, phase 6's).
    On "graph" the remote holds nothing new, so a fetch that was not cancelled would move no ref either:
    K3's proof of the cancel is the operation's outcome and the status text, and "no remote ref moved"
    (6.5) is asserted as the design asks. A commit added to `origin.git` before the start would make it a
    proof of its own; the lead may ask the builder for it. **Settled by §6's build** as this part has
    it, no commit added to `origin.git`: the proof is the fetch's `Cancelled` outcome, `Fetch origin
    cancelled` and `git for-each-ref` unchanged. On Windows the hold needed the copy's `origin` URL
    written with forward slashes (the note under K3).
37. **Unverified points of 6.9 that the checks meet.** Tab from a focusable `TextArea` set to `Continue`
    entering the bars, then leaving, and Enter reaching a button inside it (item 1; K1); `:focus-within`
    with `:not()` in Avalonia 12.1.3 (item 2; K1, K4); `GridSplitter`'s arrows and `DragCompleted` (item 3;
    K8); a tab's git calls using the detection's runner (item 5; K6, K7, whose pictures drop if not, 6.4).
    §6's fail-first tests settle them before the checks run; a check that meets one unsettled is reported
    to the lead. **Settled by §6's build:** Tab from the text, set to `Continue`, enters the bars'
    buttons in hunk order and then leaves the diff, and Enter runs a framed button inside the text area
    (`DiffTextViewTests`, `ConflictPanesTests`, K1); `:focus-within` with `:not()` works in Avalonia
    12.1.3 (`WorkingChangesTests`, K1, K4); `GridSplitter`'s arrows move 10 per press and raise no
    `DragCompleted`, so D427's code was built (`KeyboardPassTests`, K8); a tab's git calls take the
    detection's runner, so `HeldGitRunner` holds them and K6 and K7 keep their pictures.
38. **The focus frame's look.** The design names no colour for Fluent's frame (6.3.1); the checks assert
    `:focus-visible` and the gate inspects the frame in `k1a`, `k1c`, `k4a`, `k4b` and `k8a`.
39. **The inventory** (6.3.9) enters the report from §6's merge report, with 6.3.10's lines for the guide
    (to §11): the lead, at §6's merge. **Settled at §6's merge (`e463392`):** 6.3.9's inventory holds as
    the design lists it, the rows it calls unchanged included. The rows changed or proven by 8a, as
    built, with their proofs: a hunk's Stage, Unstage and Discard, Tab to its buttons, in hunk order
    (K1; `DiffTextViewTests`, `ConflictPanesTests` for the resolver's bars, `KeyboardPassTests`); Inline
    or side by side, Shift+Tab from the text (K1's step 11); chosen lines, F6 back to the text or Tab
    from the toolbar, Ctrl+Home, the arrows and Shift with the arrows, then Shift+F10 or the palette
    (K1; `KeyboardPassTests`' G1); a file row's buttons, Tab from the row, in the rows' order, rows
    below the list's view included (K4, K1's walk; `WorkingChangesTests`,
    `Tab_walks_every_row_of_a_list_longer_than_its_view_and_Shift_Tab_walks_back` among them); Amend,
    Tab, disabled while committing, the focus given back (K7; `KeyboardPassTests`); a blame run, the
    arrows, PageUp, PageDown, Home and End with any modifier, and its menu by Shift+F10, run 0's while
    none is picked (K2; `BlameViewTests`, `KeyboardPassTests`); the running operation's Cancel, "Cancel
    {title}" in the palette, or a walk of Tabs from the right panel, 8 in K8 (K3, K8; `StatusBarTests`);
    the clone browser's Choose as 6.3.9's row has it, the focus back on `From GitHub…` without a frame
    (K5; `KeyboardPassTests`); a panel's size, Tab to its splitter, 10 per arrow, a side panel's width
    kept as after a drag (K8; `KeyboardPassTests`); commits not next to each other, the mouse only
    (ruling 50; the guide). 6.3.10's lines for the guide hold, with the blame's PageUp, PageDown, Home
    and End, Tab from a stage row on to the next row, and the palette's entry listed only while the
    running operation can be cancelled.
40. **The inventory's proofs of two rows.** 6.3.9 cites K1 for "Inline or side by side" by Shift+Tab and
    K7 for "Amend" by Tab. As written, K1's Shift+Tab ends on `Discard file`, and K7 reaches Amend by no
    key. This part takes `KeyboardPassTests`' "Amend focused by Tab gets the focus back" (6.4) as the proof
    for Amend. For the mode buttons the lead either adds a scripted-only step to method 1 after step 10
    (Tab until `DiffText` has the focus, Shift+Tab until `SideBySideDiffButton` has the focus, at most
    four presses each followed by a wait for the focused id, then Enter: `SideBySideDiffButton` selected,
    no git change), or names another proof in the inventory. The first Delivers row carries the proofs
    of those two rows as the lead settles them.
    **Settled by the lead on 2026-11-20:** "Inline or side by side" is proven by K1, with a scripted-only
    step added to its method 1 after step 10 (K1's step 11: Tab until `DiffText` has the focus,
    Shift+Tab until `SideBySideDiffButton` has the focus, at most four presses each followed by a wait
    for the focused id, Enter: `SideBySideDiffButton` selected, no git change); "Amend" by K7 (disabled
    while committing) and `KeyboardPassTests` (focused by Tab, the focus given back). The design's 6.3.9
    and part K's first Delivers row name them so.
41. **K2's Downs in the real window.** 12.3.4 launch 12's "real Down until the caret is in run 2" cannot
    be read in the real window: neither `BlameText`'s peer (its value alone, `BlameTextEditor.cs:204-217`)
    nor `BlameGutter`'s (a `Custom` element, there for its bounds) exposes the caret or the picked run.
    This part counts three Downs (`Phase8aExpectations.K`, open point 32) and waits after each on the
    right panel (`DetailsSubject`, or the stage panel for run 1, the run not committed yet), and its
    scripted twin waits the same way (K2's step 5). The design's 12.3.4 item 12 is to read so: the lead.
    **Settled by the lead on 2026-11-20:** 12.3.4 item 12 reads so: the blame's arrows in the real window
    wait on the right panel, not on the caret.
42. **Method 3's hold.** 6.4 and 6.5 do not say how long the hold must last. On Linux and macOS a cancel
    kills git alone (`GitRunner.Kill`, D36), so K3's cancelled fetch's shell would go on waiting and,
    `HeldFor` after it started, leave a `gave-up-` marker that fails `WaitForHeldAsync` in K8's step 7 or
    the final `Dispose`; and K8's walk of up to 200 Tabs must end before its own fetch's hold gives up.
    This part installs the hold with `rounds: 300` (`HeldFor` 150 s, under the hang watchdog's 3 minutes)
    and releases it after each cancel (K3's step 6, K8's step 9). The lead may set other rounds.
    **Settled by §6's build:** `rounds: 300` (`Phase8aExpectations.K.FetchHoldRounds`), released after
    each cancel; no held fetch gave up (`FetchHold.GaveUp()` empty at the method's end).
    **At wave 3 (`f019688`, merged at `eba3717`):** this point named only the route where the rounds
    run out. CI's run 44969394972 found a second, in its container with the SDK from Ubuntu's packages:
    K3's cancelled fetch's shell left its loop at step 6's release, looked at `release` a second time
    after step 7's `Arm()` had deleted it, and wrote its marker, and the method failed after 4.9 s with
    "The hold gave up". Once it has made its marker, the script now looks at `release` only in its
    loop's test, and gives up only when that look finds none once its rounds are over (the
    conventions' "What a fake stands for"); `FetchHoldTests`'
    `A_fetch_that_saw_the_release_does_not_give_up_when_the_hold_is_armed_again_before_it_goes_on`
    holds a shell that saw the release inside its `rm` until after `Arm()`, and failed on the old
    script. A shell that misses a short release still counts its rounds from its own start (`Arm()`'s
    comment says so); no test comes near that. No expected result changed. The exception that hid the
    first failure is open point 94.
43. **K5's start.** 8a's `StartEmptyAsync` takes a theme and a probe and no fake of GitHub (1.17; every
    provider stays off, `Phase8aChecks.Support.cs`). This part starts K5 as 7a's `StartEmptyAsync(data,
    served)` does, on a copy of 7a's "hosting" served on a free port; the lead may instead give 8a's
    helper a `FakeGitHub? gitHub` parameter (WP0's file).
    **Settled by the lead on 2026-11-20:** K5 starts as phase 7a's `StartEmptyAsync(data, served)` does,
    through a private helper in part K's own file; `Phase8aChecks.Support.cs` is not changed for it, and
    1.17 gains nothing.

### Part B: the icon

Settled before this part was written (the owner, 2026-11-20; design 1.17, 7.8; round 2's README):
- **The mark.** Lens: round 2's `icon.svg`, `icon-small.svg` and `icon-macos.svg` as they stand, the
  outlined eye at the small sizes, the blue `#4C5BE0`. Wherever section 7 says "the placeholder" this
  part reads the owner's mark. 13.4 item 10 is settled so: no placeholder stands at the counted round, and
  13.3's row "Before the counted round", 0.8 and 12.9's risks say that nothing waits there (the lead,
  2026-11-21). 7.3's placeholder drawing and its
  contrast figures (`#6170F0`, 3.9:1 and 3.7:1) do not describe the mark; round 2's README gives the
  owner's blue 3.0:1 on a dark taskbar `#202020`. No check asserts either.
- **The look-alike check** (13.4 item 11): made by eye, against Git's logo and the well-known Git
  clients' icons, with no image search; recorded with the report's checks by hand.

Still open, each with who settles it; a point the lead settled on 2026-11-20 says so:

44. **The sheet's texts.** 7.5 gives row 1's heading `VisualCommit.ico` and "the size below" each frame,
    in `VcTextSecondaryBrush` at 11, but not the size's form: this report reads the bare number (`16` to
    `256`). 7.5 gives row 1's heading no token or size, and row 2 no heading and no labels. The checks
    assert the heading's text `VisualCommit.ico` (7.5's, word for word), the sizes' colour and font
    size, and that no text is cut. The sizes' form and the heading's token and font size stay open until
    §7's builder fixes them in `Phase8aExpectations.B`; `IconSheet` writes its heading itself, not from
    `Phase8aExpectations.B`, so that the assertion compares two copies. Row 1's lefts (B1), and open
    point 45's, hold while each size's text is no wider than its image, as the bare number at 11 is;
    another form changes them, and §7's builder then writes them in `Phase8aExpectations.B`. §7's builder,
    with the lead quoting them under part B's texts before §7's merge.
    **Settled at §7's merge on 2026-11-20 (`6fe69bd`):** a size is the bare number, `16` to `256`, at 11
    in `FontWeight` SemiBold, in `VcTextSecondaryBrush`, centred under its image on a whole pixel
    (`IconSheet.ArrangeOverride`). At the regular weight `TextInk` could not read the token on white:
    `48` centred in a box as wide as its image read `#6B6572`, 15 off `#5C6572`, and on whole pixels `20`
    and `32` read `#666572`, 10 off, past its tolerance of 8; in SemiBold every size reads its token
    exactly in both themes. The heading is `VisualCommit.ico` at 13 in `VcTextPrimaryBrush` (`#E4E7EC`
    dark, `#1B1F27` light); row 2 has no heading and no labels. Each size is no wider than its image, so
    row 1's lefts and open point 45's hold. `Phase8aExpectations.B` holds them (`HeadingFontSize`,
    `SizeFontSize`, `SizeFontWeight`); the checks also assert the heading's font size and colour and
    each size's weight, and part B's texts quote them.
45. **Row 2's layout.** 7.5 gives row 1's layout (left 24, gap 12, bottoms aligned) and row 2's contents
    and order only. This report reads row 2 as laid out as row 1, below row 1's sizes: lefts 24, 52, 88,
    132, 192, 268, 408, 436 and 480, ending at x 608. Neither row's top nor the heading's height is fixed:
    the checks assert bottoms aligned, whole pixels and both rows inside 1100×700. §7's builder confirms.
    **Settled at §7's merge on 2026-11-20 (`6fe69bd`):** as this report reads it, row 2's lefts 24, 52,
    88, 132, 192, 268, 408, 436 and 480, ending at x 608 (`Phase8aExpectations.B.Row2Lefts`, `Row2End`).
    §7's builder fixed the lines too: the heading's top at y 24, row 1's bottoms on y 312, the sizes'
    tops at y 316 and row 2's bottoms on y 488 (`HeadingTop`, `Row1Bottom`, `SizesTop`, `Row2Bottom`);
    the checks assert both bottom lines and both rows' ends, x 1054 and 608, exactly.
46. **The sheet's ids.** 7.5 names none. No step clicks or focuses anything in the sheet, so the checks
    read `IconSheet`'s images and texts from the control itself, row by row in order. §7's builder.
    **Settled at §7's merge on 2026-11-20 (`6fe69bd`):** no ids. The checks read `IconSheet`'s `Heading`,
    `Row1`, `Row1Sizes` and `Row2` in order, and compare its `Row2Files` with
    `Phase8aExpectations.B.Row2`.
47. **The centre pixel and the gaps.** 7.5 gives no index for "its centre pixel": this report reads the
    pixel (N/2, N/2), zero-based. With the owner's mark it lies inside the opaque white pupil in every
    file, so its composite is white over either background: it proves only that the image is drawn with
    its pupil at the centre, not its place to the pixel and not the composition: a shift of a pixel, or
    at the larger sizes of several, leaves the centre inside the pupil. The size and place are proven by
    `Bounds` exactly N×N on whole pixels, and the background by the gaps' colour. "The gaps" are read as
    every pixel between neighbouring images, across the gap, from the taller image's top to the row's
    bottom line. A pixel the masters leave transparent in every file (the top-left, outside the small
    drawing's corner at radius 3.75 and inside the large drawings' margins) would test the composition
    too; it is no expectation of the design's. The lead, before §7's build: add it or leave it.
    **Settled by the lead on 2026-11-20:** added. The sheets also assert one pixel that every master
    leaves transparent, the top-left pixel of each drawn image, which equals the row's background, so
    that the composition over the background is proven and not only the pupil at the centre (7.5; B1,
    B2).
48. **The real window's comparisons.** 7.5's (a) compares "its opaque pixels" within 2 per channel; (b)
    says "equals that frame within 2", which this report reads as (a): the pixels at alpha 255, per
    channel. Neither says what follows when the extracted icon's size is none of the `.ico`'s ((a) then
    fails, since it asserts a frame of that size) or when neither `WM_GETICON` size is ((b) then compares
    nothing and only run.txt shows it); both sizes are unverified under per-monitor DPI (12.9). A failure
    is a finding for the lead, never met by a wider tolerance. §12's builder, in its first runs; the lead
    rules on a miss. For 7.9 item 2, run.txt's `ICON_SMALL` size is read against `SM_CXSMICON` at the
    window's DPI (B1 (c)). `ICON_BIG` is recorded beside `SM_CXICON` but not paired with it: Avalonia
    12.1.3 loads the big icon at its own taskbar size (`LoadBigIcon`, `s_taskbarIconSize` in
    Avalonia.Win32's metadata; the value not read), so a difference there is no miss by itself. Whether
    to assert the small pair is the lead's (the design only writes the numbers into run.txt).
    **Settled by the lead on 2026-11-20** for the small pair: `ICON_SMALL` and `SM_CXSMICON` are written
    to run.txt and inspected, not asserted. The rest of this point stands.
49. **The crop.** "The title bar's left 64 × caption height" (7.5, 12.3.4) gives neither the scale of the
    64 nor the caption height's source. This report reads 64 pixels of the framed capture from its left
    edge (the invisible resize border that `GetWindowRect` includes among them) and the caption's height
    in the capture's own pixels, from its top; in phase 7b's references at 200 % the default icon lies
    inside such a crop. §12's builder settles the height's source in its first run (12.8 item 3) and
    names it in run.txt.
50. **B2's wait.** 0.5 and 7.5 ask for "the light title bar" after the real click on `ThemeSwitch` and
    name no condition for the title bar's redraw. §12's builder names one (a named condition with a limit,
    12.3.1) and run.txt says which.
51. **The taskbar button's outcome.** 7.5 and 12.3.4 say "asserted to exist" and also that no button
    found makes the step "Not run", checked by hand at the gate. This report reads: found within 10 s,
    its existence and bounds are asserted and it is captured; none found, the method does not fail on
    it, run.txt has the line, and the report's B2 says "Not run" for that step with its reason. 7.5's
    "starts with VisualCommit" is read for 12.3.4's "named VisualCommit". The lead, before §12's build.
    **Settled by the lead on 2026-11-20:** as this report reads it: found within 10 s, its existence and
    bounds are asserted and it is captured; none found, the method does not fail on it, run.txt has the
    line, and the report's B2 says "Not run" for that step, which is then looked at by hand at the gate
    ("Checks by hand").
52. **The title in the three framed captures.** 7.6's "the title moved right", against phase 7b's
    references, where an icon already stands before the title. The expected regions stay 0.7's "the
    title bar" and §2's `FilterBox` box (open point 55); the lead reads it at the gate.
53. **The pictures' names.** 7.5 and 12.3.4 name part B's pictures without a step letter
    (`b1-icon-sizes-dark`, `b2-taskbar-button-enlarged`), which 0.3's pattern allows; parts L's and I's
    carry one. This report keeps the design's names, which §12 reads from `Phase8aExpectations.B`. The
    lead, if the convention's step letter is to hold for every part.
    **Settled by the lead on 2026-11-20:** part B's pictures keep the design's names, without a step
    letter ("Conventions of every part").
54. **What is unverified until the builds run** (7.8, 7.9 items 2, 3 and 5; 12.9). Round 2's pictures
    were rendered by Edge: §7's builder compares the tool's renders of `icon-small.svg` at 16, 20, 24, 30
    and 32 with round 2's `small-zoom.png` before the files are frozen and reports each pixel that
    differs by more than a tenth (7.8); the inspection's "crisp" and "soft" are round 2's, read again on
    the tool's files. Round 2's README calls 24 crisp; its own `small-zoom.png` shows the line at 24
    with a half-covered row above and below (the grid's ×1.5), and this part expects that at 24 (the
    conventions, B1's inspection, B1 (c)). 7.5's "its parts distinct at 16 and 20" is read for the frames from
    `icon-small.svg`: the `.iconset`'s `icon_16x16` is the macOS drawing at 16, which round 2's README
    calls soft in every set drawn, and is judged only as the same mark. `HeadlessIconLoaderStub` taking
    the `.ico` is shown by `WindowIconTests` and `ShellTests`; Avalonia's Win32 icon picking a frame per
    DPI, `WM_GETICON`'s and `ExtractAssociatedIcon`'s sizes under per-monitor DPI, the taskbar button's
    name and its reach from the desktop root, and a capture outside the window by §12's first runs and
    D246's. §7's builder and §12's builder; the lead at each merge.
    **Settled at §7's merge on 2026-11-20 (`6fe69bd`)** for §7's part: the tool's `.ico` frames from
    `icon-small.svg` at 16, 20, 24, 30 and 32, over `#202020` and `#F3F3F3`, against Edge's pixels read
    from `small-zoom.png`: at 16 and 24 no pixel differs by more than a tenth (25.5 of 255 in a
    channel); at 20 six on each desk do, at 30 two and at 32 five (up to 42), all on antialiased curves
    (the tile's rounded corners, the eye's outline, the pupil), none on a straight edge of the grid, and
    nothing shifted (a mean difference of 1.4 to 2.5 per channel), so no master is at fault. The
    inspection of `b1-icon-sizes-dark` and `b2-icon-sizes-light` read 16 and 32 crisp, 20 the softest,
    24's line two solid rows with a half-covered row above and below, and the `.iconset`'s
    `icon_16x16` soft. `WindowIconTests` and `ShellTests` pass, so `HeadlessIconLoaderStub` takes the
    `.ico`. The rest of this point, §12's, stands.
55. **The framed captures' second region.** 0.7's row "The window's icon", its sentence "Every one but
    the framed captures is in the 1,333", 7.6 ("at the title bar's left only") and 12.6 name only the
    title bar for the three framed captures. §2's placeholder (1.5) also changes them, in the
    `FilterBox` ("Changes to expected results", part B). The report's §2 section (its "Changes to
    expected results" for part L, the scan's lists) leaves them out too: its 1,350 are phase 7b's
    scripted pictures and captures, without the three framed ones. The lead, before the gate: corrects
    the design and that section, and writes both regions into the gate's `--expected` file, the
    `FilterBox` box at the client area's place read on the gate's captures.
    **Settled by the lead on 2026-11-20:** the design's 0.7 (the row of the window's icon and the
    sentence after the table), 7.6 and 12.6 now say that the three framed captures also change in the
    `FilterBox` placeholder, by §2, and part L's "Changes to expected results" says so. The gate's
    `--expected` file gives each the title bar (§7) and its twin's placeholder box moved by the frame,
    its place read on the gate's captures.

### Part A: About and the third-party notices

Settled before this part was written: the owner's mark, Lens, stands in `AboutIcon` from the start
(1.17, 7.8; 13.4 item 10); `VcLinkButton` is §8's and §5's first-run links stay `VcFlatButton`s (13.2);
About's guide link reads `Open the user guide` beside the label `User guide` (13.2, 1.13); A4 is repeated
in the real window (13.2).

Where the design's sections differ about this part, with the reading this report takes (the lead
confirms):
- **8a's own pictures that §8 changes.** 0.7 names three (`k3a`, `l3a`, `l3f`) and 8.6 none; 5.5 gives
  `f12a-row-next-start`, `f12b-row-too-old` and `f14a-netrc-note`, pictures of the settings screen, which
  gain the About button too: open point 67. **Settled (the lead, 2026-11-21):** six, not three: `k3a`,
  `l3a`, `l3f`, and part F's `f12a-row-next-start`, `f12b-row-too-old` and `f14a-netrc-note`, in the
  About button's region; the design's 0.7 is corrected to name the six (open point 67).
- **The placeholder.** 8.9 item 8 says part A shows §7's placeholder and changes when the chosen mark is
  regenerated; 1.17 and 7.8 say the owner chose before §7 was built and §7 wrote no placeholder: open
  point 69. **Settled (the lead, 2026-11-21):** 8.9 item 8 is corrected: part A shows the mark from its
  first run (open point 69).
- **Launch 3's first key.** 12.3.3's rule (a launch whose first input is a key asserts `ForegroundIsThis`
  first) names launches 5 and 6; launch 3's first input is also a key: open point 72. **Settled (the
  lead, 2026-11-21):** launch 3 asserts `ForegroundIsThis` before its first key, as launches 5 and 6 do
  (12.3.3 reads "launches 3, 5 and 6"), with one real click on the title bar as its fallback (open point
  72).

Still open, each with the reading this report takes and who settles it; a point the lead settled on
2026-11-21 says so:

56. **About's place.** 8.5 puts `SettingsNavAbout` at (141, 255) at 1100×700 and (551, 415) at 1920×1080,
    and 0.7's region starts at x 141; phase 7a's and 7b's expectations put `SettingsNavAccounts` at (140,
    223) and (550, 383), asserted within 2. The checks assert the design's places within 2, as 7a does,
    and, exactly, that About lies 32 below Accounts with the same left. `Phase8aExpectations.A` holds the
    place §8's builder reads; the lead confirms before §8's merge. **Settled (the lead, 2026-11-21):** as
    this report has it: the design's places asserted within 2, and About exactly 32 below Accounts with
    the same left; `Phase8aExpectations.A` holds the place §8's builder reads. The lead's `--expected`
    file for §8's merge takes the region from x 140, and the design's 0.7 and 8.6 give it as x 140–317,
    y 255–285 at 1100×700 (x 550–727, y 415–445 at 1920×1080): phase 7a's and 7b's expectations put the
    section list's buttons at x 140 and 550, asserted within 2. 8.5's places for About stay as written.
    **Settled by §8's build (merged at `f014b2b`):** `Phase8aExpectations.A.AboutAt` (141, 255) and
    `AboutAtFull` (551, 415), asserted within 2.
57. **Method 1's order.** 8.5 lists A1's three Tabs before A4; 12.3.4 has the real window go from A1's
    picture to A4 by "Tab to `AboutShowLicenseButton`". Method 1 takes A4 after A1's first Tab, which
    lands on `Show license…`, and A1's walk goes on from there after Esc gives the focus back: the three
    Tabs still stop at `Show license…`, `Show third-party notices…` and the repository link, and the twin
    takes its steps in the launch's order. 8.5's "For §12: A1 up to the Tab walk" is read as "up to it,
    not including it" (12.3.4 names no Tab walk). The lead. **Settled (the lead, 2026-11-21):** as this
    report reads it.
58. **The text dialog's card.** 8.2 gives `Border.dialogCard.document` MaxWidth 760, MaxHeight 600, Margin
    40, stretched, and no place. This report derives 760 × 600 at (170, 50) at 1100×700 by the rule the
    settings card follows (`Theme/Dialogs.axaml`: stretched both ways and centred by its maximum). §8's
    builder confirms and `Phase8aExpectations.A` holds it; the checks assert its size and place within 2.
    **Settled (the lead, 2026-11-21):** as this report reads it; §8's builder fixes the values in
    `Phase8aExpectations.A`. **Settled by §8's build (merged at `f014b2b`):** 760 × 600 at (170, 50)
    (`Phase8aExpectations.A.DialogCard`), asserted within 2.
59. **The gaps with one part.** 8.3.2 gives 12 below the title for the switch and 8 below the switch for
    the text; with one part (the license) it does not say whether the text lies 8, 12 or 20 below the
    title. A4 asserts the order (title, text, buttons) and the buttons 20 below the text, not that gap; A5
    asserts 12 and 8. §8's builder fixes it in `Phase8aExpectations.A`. **Settled (the lead,
    2026-11-21):** as this report reads it; §8's builder fixes the values in `Phase8aExpectations.A`.
    **Settled by §8's build (merged at `f014b2b`):** with one text and no switch the text lies 12 below
    the title (`TextBelowTitleAlone`); with the switch, the switch 12 below the title and the text 8
    below the switch (`SwitchBelowTitle`, `TextBelowSwitch`). Method 1 asserts the 12 in A4 as well,
    within 0.5.
60. **A5's 1 s and End with word wrap** (8.9 item 3, 13.4 item 12): unverified until part A runs; if either
    fails, the lead rules during §8 (no wrap, or a later bound). The bound is also read in CI's six jobs,
    whose runners are slower than the laptop (macOS's above all): the lead's ruling says whether it holds
    there or is asserted on the laptop alone. **Settled (the lead, 2026-11-21):** the bound stays 8.5's
    and is asserted by the scripted check wherever it runs; the check also writes the time it measured to
    its output. If the whole default run or one of CI's jobs fails it, the lead rules then, with the
    measured times, and no bound is loosened ahead of that. **At §8's merge (the lead, 2026-11-21):**
    the bound stays. §8's build asserts it at the method's end, not right after step 3, so that a slow
    opening still lets steps 4 to 7 run and save their pictures; the time is still the one from the
    click to step 3's wait, written to the output. It held in the lead's runs and in all six jobs of
    CI's run 44969394972, the merge's; it failed in the builder's and a fixer's runs on the laptop
    while other agents built and tested beside them (1,001 to 2,119 ms; 1,164 to 1,759 ms). No ruling
    until the whole default run or a CI job fails it. End with word wrap shows the true last line in
    §8's runs, and the view lets go of the end when the reader moves up
    (`DocumentTextViewTests.A_wheel_up_after_End_leaves_the_end_though_the_lines_it_brings_in_wrap`).
61. **The texts' last line.** `THIRD-PARTY-NOTICES.md` is expected to end with a line end, after which
    AvaloniaEdit counts an empty last line. A5 reads 8.5's "its last line at the bottom" as: the scroll
    offset at its end; the document's last line, that empty line, as the view's last visual line; and the
    file's last line that is not empty shown whole in the view with only empty lines below it. The same
    holds for `LICENSE` in A4, which ends with a line end: the file has 21 lines, the document 22
    (`Document.LineCount` 22, its last line empty); if `Phase8aExpectations.A` holds a count, it keeps the
    file's 21 and asserts `LineCount` as that count + 1. §8's builder confirms. **Settled (the lead,
    2026-11-21):** as this report reads it; §8's builder fixes the values in `Phase8aExpectations.A`.
    **Settled by §8's build (merged at `f014b2b`):** `LICENSE` has 21 lines
    (`Phase8aExpectations.A.LicenseLines`), the document 22 (`LineCount` asserted as 21 + 1), and A4
    asserts lines 1 to 22 in view.
62. **The icon's pixels.** 8.3.1 gives `AboutIcon` 48 × 48, decorative, drawn in high quality from the 96
    PNG. This report adds part B's two readings (the centre pixel white, the top-left pixel the card's
    background, each within 2), from 1.17's description of `VisualCommit-96.png`. The lead keeps or
    drops them before §8's build. **Settled (the lead, 2026-11-21):** kept. **Settled by §8's build
    (merged at `f014b2b`):** A1, A2 and A3 also assert the conventions' source, the 96 × 96 bitmap, and
    `BitmapInterpolationMode.HighQuality`.
63. **A link's hover.** 8.3.1: the links underline while hovered or focused. A1 shows the focus's underline;
    no check hovers a link (G3 is part G's, by Tab). The lead, if a hover step is wanted (a picture with
    the pointer kept on `AboutRepositoryLink`, its tooltip then open). **Settled (the lead,
    2026-11-21):** no hover step and no hover picture.
64. **The real window at 1920×1080.** 12.3.4 says, within A1, "at 1920×1080 too when `app.Fits(1920,
    1080)`" and not how the window gets there or back. This report reads it as A1's second capture: About
    still open, the window sized 1920×1080, captured as `a3a-about-1920` and compared with A3's picture,
    then sized 1100×700 again before A4, whose twin is at 1100×700; each resize is followed by a wait on
    `SettingsNavAbout`'s place, because the card, a plain `Border`, is no UI Automation element; and
    run.txt's line in phase 0's form, naming A1. A3 itself stays scripted only: 0.5, 8.5's table and 12.5
    count A1 and A4 as the repeated checks, and 8.5's last paragraph and 12.3.4 put the capture at
    1920×1080 inside A1's steps. On the laptop at 200 % 1920×1080 does not fit, so D246's runs and the gate
    show only the line. §12's builder; the lead. **Settled (the lead, 2026-11-21):** as this report reads
    it; §12's builder.
65. **The version in the real window.** 12.3.4 asserts `AboutVersion`'s name equal to
    `BuiltVersionDisplay()`. A build whose `ProductVersion` holds no `+<commit>` shows `0.1.0`, and the
    equality still holds, though ruling 59 asks for the short commit. Whether the launch also asserts that
    the name ends with ` (` and seven hexadecimal digits and `)` is the lead's, before §12's build;
    `ProductVersion`'s form is unverified until then (8.9 item 7, 12.9). **Settled (the lead,
    2026-11-21):** launch 3 asserts the equality with `BuiltVersionDisplay()` and also that the name ends
    with ` (`, seven hexadecimal digits and `)`. If the pass's Release build carries no commit, the
    launch fails saying so, and the lead rules then (unverified until §12's first run).
66. **What the text dialog leaves to its builder.** How `TextDocumentText` draws no caret (8.3.2: "no caret
    drawn"): A4 and A5 assert it by inspection and through whatever the builder names in
    `Phase8aExpectations.A` (the caret hidden or its brush transparent). And the frame on the opening
    button after Esc (A4): 8.3.2 gives the focus back; the frame follows from today's `MainWindow`, which
    gives the focus back to an element under a stacked dialog by Tab. A4 asserts the frame after a
    keyboard opening; A5, opened by a click, asserts the focus only. §8's builder confirms. Whether UI
    Automation reports the editor or its `TextArea` as the focused element is confirmed by §12's first
    run, through run.txt's note of which condition of the text's wait held (as part K's launches 11 and
    12). **Settled (the lead, 2026-11-21):** as this report reads it; §8's builder and §12's first run, as
    it says. **Settled by §8's build (merged at `f014b2b`):** the text area's caret brush is
    `Brushes.Transparent` (`DocumentTextView`), and A4 and A5 assert a solid brush whose alpha is 0, so
    that a missing brush fails; the text takes the focus at opening without the keyboard's frame
    (`NavigationMethod.Unspecified`, as the diff's text does). What UI Automation reports stays §12's.
67. **8a's own pictures with the settings screen.** 0.7 and 8.6 omit `f12a-row-next-start`,
    `f12b-row-too-old` and `f14a-netrc-note` (part F, 5.5), which show the section list at 1100×700 and so
    change in x 140–317, y 255–285 at §8's merge. The lead names them in the `--expected` file for §8's
    merge and corrects 0.7. **Settled (the lead, 2026-11-21):** six, not three: `k3a`, `l3a`, `l3f`, and
    part F's `f12a-row-next-start`, `f12b-row-too-old` and `f14a-netrc-note`, in the About button's
    region. The design's 0.7 is corrected: it reads "Six" for "Three" and adds part F's three, which show
    the settings screen's section list, in x 140–317, y 255–285 at §8's merge.
68. **The references for §8's comparison.** 8.6 compares against `references/wave1/` "with these regions
    and wave 2's"; since wave 2's close the later merges' references are
    `D:/Projects/Personal/phase8-work/references/wave2/` (the report's "Wave 2's comparison"), which hold
    wave 2's changes already. This report reads 8.6 as a comparison against `references/wave2/` with
    8.6's regions and open point 67's. The lead confirms. **Settled (the lead, 2026-11-21):** from wave
    2's close the references of every later merge are `D:/Projects/Personal/phase8-work/references/wave2/`,
    and after wave 3's close wave 3's. The design's 0.7, 8.6, 9.6 and 10.6 say "the references of the
    last closed wave (wave 2's from its close)".
69. **The placeholder in part A's pictures** (8.9 item 8). Settled by 1.17 and 7.8: the owner's mark was
    chosen before §7 was built, §7 wrote no placeholder, and `AboutIcon` shows the mark from part A's
    first run. Should the masters change again, part A's pictures change with part B's and are inspected
    again; no assertion of part A names a shape. The lead corrects 8.9 item 8. **Settled (the lead,
    2026-11-21):** 8.9 item 8 now reads "Settled (1.17, 7.8): the owner chose the mark before §7 was
    built, so part A shows it from its first run; should the masters change again, part A's pictures
    change with part B's and are inspected again."
70. **The notices in pictures.** `a5a` to `a5c` show the notices word for word (D431, D432): third parties'
    names and copyright lines, and whatever addresses the vendors' files hold. No text of this report
    quotes them, and the `@` rule is About's own texts'. The lead, if any line should be kept out of a
    picture (a scroll position, or another vendor file than Avalonia's for `a5c`). **Settled (the lead,
    2026-11-21):** as this report reads it; the lead at §8's merge, if a line should be kept out of a
    picture. **At §8's merge (the lead, 2026-11-21):** `THIRD-PARTY-NOTICES.md` quotes the copyright
    lines word for word, their holders' e-mail addresses included (8.3.4 as written): "no e-mail
    address" meant the owner's and About's own texts, not third parties' notices. Six addresses, each
    inside a fenced block, which
    `ThirdPartyNoticesTests.The_notices_files_only_addresses_are_its_copyright_lines` admits and no
    other; About's own texts hold no `@`, as before. They lie on the file's lines 72 to 376 of 945,
    out of `a5a`'s and `a5b`'s view, and Avalonia's file holds none. The vendors' files are kept byte
    for byte (`notices/** -text`). No line is kept out of a picture.
71. **The neutral point at 1920×1080.** (760, 687) lies inside the settings card, which ends at y 880, in
    the section's column; About's form, "about 356 high" (8.3.1), ends above it, so nothing is hovered
    there. If the form reaches it (a link would then show its hover underline in `a3a`, and a real pointer
    could open a tooltip), §8's builder tells the lead, who moves part A's resting point at that size in
    both harnesses. **Settled (the lead, 2026-11-21):** as this report reads it; §8's builder, as it says.
    **Settled by §8's build (merged at `f014b2b`):** About's form ends at about y 591 at 1920×1080, by
    the builder's reading, so nothing lies under the neutral point and the resting point stays.
72. **Launch 3's first key.** By 12.3.3's rule the launch asserts `ForegroundIsThis` before A1's
    `Ctrl+Shift+P`; 12.3.4 names the rule for launches 5 and 6 only and gives launch 3 no fallback. This
    report gives it one: if `ForegroundIsThis` fails, a real click at the neutral point (on nothing on the
    welcome page, after B1's captures), as phase 7a's real-window B7 makes before its `Ctrl+Shift+P` on one
    empty tab, then `ForegroundIsThis` asserted again within 5 s, else the method fails before any key.
    The lead makes the final choice before §12's build: this click, a click on the title bar away from its
    left end (12.3.5's ruling 2), or no fallback. **Settled (the lead, 2026-11-21):** launch 3 asserts
    `ForegroundIsThis` before its first key, as launches 5 and 6 do (12.3.3 reads "launches 3, 5 and
    6"). Its fallback, if the assertion fails, is one real click on the window's title bar, away from its
    left end (where the icon opens the system menu) and from the caption buttons, then
    `ForegroundIsThis` asserted again within 5 s, else the method fails before any key (12.3.5's second
    ruling, for the main window too). No click at the neutral point. The launch's steps above and the
    design's 12.3.4 launch 3 say so.
73. **What the design leaves unnamed.** The colours of `AboutName`, `AboutVersion`, `AboutCopyright` and
    `AboutLicense` (8.3.1 says "tokens only", and names none): the checks assert none of them by value. The
    switch buttons' widths: 8.5 expects the five in one row of the card's content (718 wide at 1100×700);
    if `SkiaSharp and HarfBuzzSharp` wraps to a second row, §8's builder tells the lead. §8's builder.
    **Settled (the lead, 2026-11-21):** as this report reads it; §8's builder, as it says. **Settled by
    §8's build (merged at `f014b2b`):** `SkiaSharp and HarfBuzzSharp` stays on the switch's one row (A5
    asserts the five buttons' tops equal); the four colours are asserted by no value.
74. **Unverified in the build** (8.9 items 1, 2, 4 to 6): `NativeMenuItem.Command` in Avalonia 12.1.3 and a
    `NativeMenu` under the headless platform (`AboutEntryTests`; without `Command`, `App.ShowAbout`); the
    macOS menu itself (8b's Mac checklist); Wuffs, SPIRV-Cross and any other component Microsoft's file
    omits, whose licenses are read at the source; the macOS and Linux natives, proven only in CI's macOS
    and Linux jobs; `ci.yml`'s `paths` order rule, until a push changes only the notices. No check of
    part A depends on them. §8's builder; the lead at §8's merge. **Settled (the lead, 2026-11-21):** as
    this report reads it; §8's builder and the lead at §8's merge, as it says. **Settled by §8's build
    (merged at `f014b2b`) and CI's run on the merge:** `NativeMenuItem.Command` exists in Avalonia
    12.1.3, so the item's `Command` is `App.AboutCommand`, and `NativeMenu.GetMenu(Application.Current)`
    reads the menu under the headless platform (`AboutEntryTests`); Wuffs (MIT and Apache-2.0) and
    SPIRV-Cross (Apache-2.0 or MIT) have their licenses as read at their sources;
    `ThirdPartyNoticesTests` passed in all six of CI's jobs in run 44969394972, the macOS and Linux
    natives among what it checks (8.9 item 5). Still open: the macOS menu itself (8b's Mac checklist)
    and `ci.yml`'s `paths` order rule, until a push changes only the notices (8.9 item 6).

### Part U: screen readers: the graph's row, the names and the sweep

Numbered on from part A's: U 75 to 92. §9's builder, or the lead, settles each before §9's merge
unless it names another; §12's builder follows the report's steps for launch 13.

Where the design's sections differ about this part, with the reading this report takes (the lead
confirms):
- **The branch toggles' names** (open point 75). 1.12 and 9.3.6 bind `ShowRefFormat` and
  `StopSoloingRefFormat` over `RefItemRow.Text`; 9.5 expects `Show feature/alpha in the graph` and
  `Stop soloing feature/ff`. The left panel's row text of a branch in a folder is the folder's leaf
  (`LeftPanelViewModel.AddFolder`: `alpha`, `ff`), its full name being `RefItemRow.ToolTip`; so over
  `Text` the names would read `Show alpha in the graph` and `Stop soloing ff`, and the hidden
  `feature/alpha` and its hidden upstream `origin/feature/alpha` would both read `Show alpha in the
  graph` under `RefList`, which 9.3.8's `duplicate` rule reports, while 9.4 counts `RefHiddenToggle`'s
  duplicates among the findings §9 must clear. This report reads 9.5: the branch's full name, as the
  menus' own items word it (`Hide feature/alpha`, `Stop soloing feature/ff`, over `GitRef.Name`), so
  `Show origin/feature/alpha in the graph` for the upstream. The lead corrects 1.12 and 9.3.6 (over
  `RefItemRow.ToolTip`, or a property of its own), or 9.5 and this part's U3. **Settled (the lead,
  2026-11-21):** the ref's full name, as 9.5 and this part have it and as the menus word it: `Show
  feature/alpha in the graph`, `Show origin/feature/alpha in the graph`, `Stop soloing feature/ff`. The
  design's 1.12 and 9.3.6 are corrected: `ShowRefFormat` and `StopSoloingRefFormat` are bound over the
  ref's full name (the string the row's tooltip shows for a branch or a tag, `RefItemRow.ToolTip`, or a
  property of its own that holds the same), not over `RefItemRow.Text`. This part's U3 stands as
  written.
- **The walk-through's step 2** (open point 86). 9.3.9's step 2 expects "the selected row" after
  "Commit graph, list" on F6; 9.3.2 and 9.3.4 expose and speak no row while none is selected, and a tab
  opens with none selected (its right panel's placeholder, phase 6's K12). This part's text says so at
  step 2 and lets step 3's first Down select the working-changes row. The lead, before the gate.
  **Settled (the lead, 2026-11-21):** the design's 9.3.9 follows this part, which follows the code: F6
  to the graph is heard as "Commit graph, list" alone while no row is selected (9.3.2, 9.3.4); step 3's
  first Down selects the working-changes row, and each later step that assumed a selection is worded
  from the row the keys really reach, as this part has them.
- **The walk-through's step 11** (open point 86). 9.3.9's step 11 ("F6, Tab to a file: its path,
  'button'") against the details panel as built: F6 into the right panel already puts the focus on
  `FileList`'s first row (`KeyRegionNavigator`'s mark `FileList`, phase 6's design 6.6; phase 6's K12
  after its step 13, where the row is named `core.txt`), and a `ChangedFile` row is named by
  `ChangedFileRow.Name`, the file's own name (`RightPanelView.axaml`), its path being its tooltip; 1.12
  and 9.3.6 rename no file row. This report reads the code: F6, no Tab, "alpha.txt, button". The lead
  corrects 9.3.9's step 11 before the gate (to "F6 to the right panel: the first changed file's row,
  its name, 'button'"). **Settled (the lead, 2026-11-21):** step 11 reads "F6 to the right panel: the
  first changed file's row, its name, 'button'"; the design's 9.3.9 follows this part.
- **9.4's "No other"** (open point 92). 9.4 was written before wave 2's `KeyboardPassTests` fact
  `ReadmesStageFact` (`1355a20`). This report lists its change under "Changes to expected results"; the
  lead adds it to 9.4's "Earlier tests that change" before §9's build. **Settled (the lead,
  2026-11-21):** the design's 9.4 gains `KeyboardPassTests.ReadmesStageFact`, as the part's
  `forTheDesign` words it. (The pre-gate round's Ctrl+Tab ruling rewrites that test later; that is
  status.md's step 19.)

Still open, each with the reading this report takes and who settles it; a point the lead settled on
2026-11-21 says so:

75. **The branch toggles' names**, above. The lead, before §9's build. **Settled (the lead,
    2026-11-21):** the ref's full name, as above; U3 stands as written.
76. **U1's order.** 9.5 lists "Down; Up to the stash row and the working row; Shift+Down twice (`u1b`);
    the working row (`u1c`)". From the working-changes row or a stash, Shift with an arrow has no anchor
    and moves as without (phase 4's 4h; `CommitGraphControl.ExtendWithKey`), so three rows cannot be
    selected right after the Ups as listed. This report takes: Down, Up, Shift+Down twice (`u1b`), then
    six Ups through the stash to the working-changes row (`u1c`), then a branch clicked. It also reaches
    "F6, 'Main work' selected" (9.5) by a click on `Local branches › main` and F6, as the spike's S4 shows
    a ref's click selecting the row; a click on the graph row would leave the focus on the graph, from
    which F6 moves away. §9's builder confirms; the lead if otherwise. **Settled (the lead,
    2026-11-21):** as this part has it (a click on `Local branches › main`, F6, Down, Up, Shift+Down
    twice, six Ups through the stash to the working-changes row, a branch clicked). The design's 9.5
    outline is corrected to that order.
77. **The first Name change's old value.** The peer is made after F6 (step 3), so no name has been
    raised when step 4's Down moves the row; 9.3.4's table gives "old: the last name raised" for another
    row and "old null" for none to a row, and does not say which holds here. U1 asserts the old value
    from the second change on. §9's builder. **Settled (the lead, 2026-11-21):** as this part reads it;
    §9's builder fixes the value in `Phase8aExpectations.U`, and the lead quotes it in the report at §9's
    merge. **Settled by §9's build (merged at `769f459`):** null (`Phase8aExpectations.U.FirstOldName`),
    which methods 1 and 2 assert for their first change.
78. **The stash row's hint.** 9.3.5 gives `Commit {short SHA}` as the hint and names no other for a
    stash's row; this report expects `Commit 0e95aca`. §9's builder; the lead if another is wanted.
    **Settled (the lead, 2026-11-21):** `Commit {short SHA}`, as this part reads 9.3.5.
79. **The scenarios' extra state.** 9.5 says "README.md changed" and names `u-untracked.txt` for launch
    13, without contents. Method 3's staged `src/core.txt` and its untracked `u-untracked.txt` are this
    report's, so that "Unstage …" has a row and two unstaged rows can be selected and hovered (9.3.8's
    state 5: two unstaged and one staged file). `Phase8aExpectations.U` holds the contents. **Settled (the
    lead, 2026-11-21):** as this part reads it; §9's builder fixes the contents in
    `Phase8aExpectations.U`, and the lead quotes them in the report at §9's merge. **Settled by §9's
    build (merged at `769f459`):** README.md is written as
    `# Operations\n\nA change for the screen readers' checks.\n` (methods 1 and 3; `U.ReadmeChanged`);
    `src/core.txt` as `core\nmain work\nstaged for the names\n`, then staged (method 3;
    `U.CoreChanged`); `u-untracked.txt` holds `An untracked file whose row's buttons are named.\n`
    (methods 2 and 3 and launch 13; `U.UntrackedContent`).
80. **"Two branches hidden"** (9.5's U2, 9.3.8's state 5) is read as `Hide feature/alpha`, which hides
    `origin/feature/alpha` with it (phase 5's FL1), both rows shown once both `feature` folders are open.
    §9's builder. **Settled (the lead, 2026-11-21):** as this part reads it; §9's builder fixes the
    contents and the values in `Phase8aExpectations.U`, and the lead quotes them in the report at §9's
    merge. **Settled by §9's build (merged at `769f459`):** `Hide feature/alpha` (`U.HideFeatureAlpha`)
    hides `origin/feature/alpha` with it; method 3's step 4 waits for both rows' eyes, named
    `Show feature/alpha in the graph` and `Show origin/feature/alpha in the graph` (U3's table), and
    the two rows' help texts are `feature/alpha` and `origin/feature/alpha` (D456 (3); step 8).
81. **The left panel's rows themselves.** `RefItem` and `Section` are Buttons named by their row's text
    (`LeftPanelView.axaml`), so on the ops tab `RefList` holds two `RefItem`s named `main` (Local branches'
    and `origin`'s) and two named `feature` (the two folders), and two named `alpha` once both folders are
    open: each pair one automation id and one name under one items container, which 9.3.8's `duplicate`
    rule reports. 1.12's table names none of them, 9.4's first-run list ("at least") does not either, and
    9.3.6 names a control outside the table only at the lead's word. U2 and U3 expect `AssertNamed` to
    report nothing on the ops tab (and so does the sweep's state 5), which holds only once the lead rules:
    the rows named by their full names (`RefItemRow.ToolTip`), the rule's container narrowed, or an
    exemption for `RefItem`. The lead, before §9's build; this part's expectations follow the ruling.
    **Settled (the lead, 2026-11-21):** an exemption, not new names. A `RefItem` row keeps its
    automation name, the row's text (the leaf: `main`, `feature`, `alpha`), as every earlier test and
    every real-window helper finds it (`RealApp.RefRow`, `Phase1Checks.Graph`): no earlier test, helper
    or picture changes. 9.3.8's `duplicate` rule does not apply to `RefItem` under `RefList`:
    `AutomationSweep` holds that one exemption by the automation id, with its reason in a comment (a row
    of the branch list is told apart by its place under its section and its folders, and by its help
    text; the left panel as a tree with levels is the Backlog's, ruling 36). So `AssertNamed` reports
    nothing on the ops tab, U2's sweep state 5 passes, and U1's step 8 reads the focus on the `RefItem`
    named `alpha` (the row's text), not `feature/alpha`. The toggles inside the rows are unique by open
    point 75's ruling. **Settled at §9's merge (D456 (2), (3); 2026-11-21):** the exemption's reason, as
    first written, was that the row's full name is its tooltip, which its peer gives as its help text;
    as built the peer gave none, since the full name was the tooltip of the row's name text (`RefName`)
    and Avalonia's peer falls back only to its own control's tooltip. The `RefItem` button's
    `AutomationProperties.HelpText` is now bound to `RefItemRow.ToolTip`, the one exception to ruling
    34, with no tooltip on the button and nothing drawn: a branch's or a tag's row gives the ref's full
    name (`feature/alpha`, `origin/feature/alpha`; never `NameToolTip`, which reads "… is hidden in the
    graph" for a hidden branch), a folder's row its path (`feature`, `origin/feature`), a remote's row
    its name (`origin`), a stash's row its message; only a section's row (`Section`, a template of its
    own) gives none. Method 3's step 8 asserts the two `alpha` rows'. So the exemption's reason holds,
    and the sweep's comment says so. The first run's other duplicates (open point 90) are gone: the
    stage rows' buttons and the eyes by 1.12's names (open point 75), and the stacked dialogs' Cancel by
    D456 (2), a dialog being its own container.
82. **The comparison's route.** 9.5 names "the comparison's two" and no route; this report opens
    `Compare feature/conflict with main` from the left panel's menu (phase 5's 6.2), a branch comparison,
    so that `CompareClose` shows (`IsBranches`). §9's builder. **Settled (the lead, 2026-11-21):** as
    this part reads it; §9's builder fixes the contents and the values in `Phase8aExpectations.U`, and
    the lead quotes them in the report at §9's merge. **Settled by §9's build (merged at `769f459`):**
    `Compare feature/conflict with main` from the menu of the folder's `conflict` row; the comparison's
    ends read `From main` and `To feature/conflict` (`U.CompareFrom`, `U.CompareTo`), and it is one of
    branches (`IsBranches`); method 3's step 3 asserts both.
83. **The restore offer.** "Dismiss the restore offer" shows only after a discard (D62), so method 3
    confirms a discard of README.md and its end state carries README.md as committed and one backup ref
    (D67), as the sweep's state 5 does. The lead, if another route is wanted. **Settled (the lead,
    2026-11-21):** as this part reads it; §9's builder fixes the contents and the values in
    `Phase8aExpectations.U`, and the lead quotes them in the report at §9's merge. **Settled by §9's
    build (merged at `769f459`):** method 3's end state is the start's with README.md as committed
    (`# Operations\n`, `U.ReadmeCommitted`), README.md's status line gone, and one ref under
    `refs/visualcommit/backup/` whose commit holds README.md as it was (`U.ReadmeChanged`, read by
    `git show`); `src/core.txt` still staged, `u-untracked.txt` still untracked.
84. **"Close the diff"** (1.12's `CloseDiffButton`) is read in method 3, where README.md's diff is open,
    though 9.5's U3 list does not name it; launch 13 does not read it. **Settled (the lead,
    2026-11-21):** as this part reads it; §9's builder fixes the contents and the values in
    `Phase8aExpectations.U`, and the lead quotes them in the report at §9's merge. **Settled by §9's
    build (merged at `769f459`):** read in method 3's step 8, README.md's diff open: `Close the diff`,
    its tooltip `Close`, its help text the tooltip by the peer's fallback, no key (U3's table).
85. **The walk-through's start.** 9.3.9 starts "copies of the ops scenario with README.md changed and of
    a stopped merge" by the lead's line with CLAUDE.md's isolating variables, which do not set
    `VISUALCOMMIT_TIME_ZONE`: the heard dates are then the machine's zone, not 9.3.5's UTC. This report
    reads one session of two tabs (ops active), the stopped merge being
    `Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`, as launch 13's, and a second start for steps
    20 and 21, made once the first app has closed (step 20). On the walk-through's data folder, a start
    while its window is open hands over (D421). If the lead prefers to leave the first app open, the
    second start needs an empty data folder of its own; it then opens one empty tab on the first-run
    screen. The lead may add `VISUALCOMMIT_TIME_ZONE=UTC` to the start line, so that the dates heard
    are the ones quoted. The lead, before the gate. **Settled (the lead, 2026-11-21):** the design's
    9.3.9 follows this part, which follows the code. The start: one session of two tabs, the ops copy
    active and the stopped merge second, by the lead's start line with CLAUDE.md's isolating variables
    and `VISUALCOMMIT_TIME_ZONE=UTC`, so that the dates heard are 9.3.5's; steps 20 and 21 by a second
    start with `VISUALCOMMIT_GIT_STAND_IN=none` after the first app has been closed, since one instance
    runs per data folder (D421). The owner hears the steps at the gate; a step whose words differ is
    written down as heard.
86. **The walk-through's steps that do not hold as 9.3.9 writes them, or that it leaves open.** Step 2
    (above); step 5, where End and PageDown both land on the last row, so one of them speaks nothing;
    step 6, which from the last row would select nothing more (this report starts it from HEAD's row,
    "Up until 'Main work' is heard"); step 11, where F6 already lands on the only file and the row
    speaks its name, not its path (above); step 15's word, which 9.3.9 leaves open (this report takes
    `alpha`); step 21, whose spoken result the design does not word (`GitStatus` is no live region; the
    screen and its focus stay): this report expects "Checking…" while the search runs and "Check again,
    button" with the focus given back. The lead, with the owner, before the gate. **Settled (the lead,
    2026-11-21):** the design's 9.3.9 follows this part, which follows the code. Step 2: F6 to the graph
    is heard as "Commit graph, list" alone while no row is selected (9.3.2, 9.3.4); step 3's first Down
    selects the working-changes row, and each later step that assumed a selection is worded from the
    row the keys really reach, as this part has them. Step 11: "F6 to the right panel: the first
    changed file's row, its name, 'button'". The owner hears the steps at the gate; a step whose words
    differ is written down as heard.
87. **Launch 13's details 12.3.4 leaves.** The click on the working-changes row before the double-click
    on `notes.txt` (the stage panel shows only then); `WindowLayout.RowHeight` ± 1 read in
    `GraphRowsBounds()`'s units (the window's, not the capture's pixels at 200 %); the run.txt line and
    the failure's message for a Name change without a LiveRegionChanged (D450 fixes the rule, not the
    words); each row button found by its id among the shown elements within the hovered row's vertical
    extent, its name then asserted (12.3.5; the buttons are siblings of the row's `StageFileRow` in UI
    Automation). §12's builder; the lead quotes the message here before §12's merge. **Settled (the
    lead, 2026-11-21):** §12's builder settles them, as this part says.
88. **The spike** (9.3.1; 13.4 item 14). If S1 (cold) finds no LiveRegionChanged from
    `GraphSelectedRow` and S2 (warm) does, the lead decides whether the public route stands; if neither
    does, the build stops until a decision names the `[PrivateApi]` route, and then U1 and launch 13 also
    assert its focus event (D445). The lead, at the spike, before §9's build goes on. **Settled (the
    lead, 2026-11-21; D455):** with a cold client (S1) no event came from `GraphSelectedRow`; once
    the client had read the graph's first child (S2), each move raised one Name change and one
    LiveRegionChanged. The public route stands with the risk recorded: nothing uses `[PrivateApi]`,
    so neither U1 nor launch 13 asserts a focus event (D445), and launch 13 reads the graph's first
    child before its moves. The cold case stays as D455 has it (the lead at §9's merge): the owner's
    Narrator listen decides whether the route stands, and if Narrator is silent a new decision names
    the route before the review.
89. **The short listen** (13.3, 13.4 item 13; D455): steps 2 to 6 and 14, by the owner, which D455
    brought forward from §9's merge to the first build the owner can hear. Asked for after the spike
    and again at §9's merge, and not held yet (status.md, "Waiting on the owner", names its start
    file). The lead records its words in status.md and quotes them under part U.
90. **The first run's findings** (9.4, 9.5: "the report lists the states and the first run's
    findings"): `AccessibleNameSweepTests` on the code before §9's change, state by state, recorded
    under U2 at §9's merge; the list given in U2 is 9.4's "at least". **Settled at §9's merge
    (2026-11-21):** listed under U2. On the code before 1.12's names, 12 of the 13 facts then written
    failed and state 14's alone passed; every finding of 9.4's list came, and beside them the unnamed
    scroll bars of the palette, Keyboard shortcuts and the blame, and each sign-in dialog's Cancel over
    the settings screen's, which D456 (1) and (2) settle.
91. **The references of 9.6's comparison.** 9.6, 0.7 and 1.17 name wave 1's
    (`D:/Projects/Personal/phase8-work/references/wave1/`); the report's "Wave 2's comparison" makes wave
    2's the references of the later merges. This part reads wave 2's, with the lead's `--expected` file
    for §8's regions. The lead, at §9's merge. **Settled (the lead, 2026-11-21):** by part A's ruling
    3, which names this point: from wave 2's close the references of every later merge are
    `D:/Projects/Personal/phase8-work/references/wave2/`, and after wave 3's close wave 3's. The design's
    0.7, 8.6, 9.6 and 10.6 say "the references of the last closed wave (wave 2's from its close)".
92. **9.4's "No other"**, above: `KeyboardPassTests.ReadmesStageFact` changes with 1.12's
    `StageFileFormat` ("Changes to expected results"). The lead adds it to 9.4 before §9's build.
    **Settled (the lead, 2026-11-21):** the design's 9.4 gains `KeyboardPassTests.ReadmesStageFact`, as
    the part's `forTheDesign` words it. (The pre-gate round's Ctrl+Tab ruling rewrites that test later;
    that is status.md's step 19.)

Found at wave 3's merges, numbered on; neither is this wave's (the lead, 2026-11-21): each goes to the
review before the gate (status.md's step 19).

93. **A branch row's name while its folder opens** (the spike's S4). When `Local branches › feature` was
    clicked, UI Automation's FocusChanged named the focused `RefItem` `Avalonia.Controls.DockPanel` for
    a moment: by §9's builder's reading, the row's name binding is null while the folder's rows are
    rebuilt, and the peer falls back to its content's `ToString()`. The sweep, which looks once the
    panel has settled, does not see it; it is older than 8a and outside 1.12's table. The review before
    the gate reproduces it in a test, and the Narrator walk-through listens for it when a folder opens
    (step 10).
94. **A check whose `using var hold` throws in its `Dispose` hides the first exception.** In CI's run
    44969394972 part K's method 3 failed with the fetch hold's `Dispose` throwing "The hold gave up",
    which replaced the exception of the step that failed first (most likely `WaitForHeldAsync` at K8's
    step 7; open point 42). It is older than this wave, and the hold's fix (`f019688`) does not change
    it.

Found at wave 3's close (the lead, 2026-11-22), numbered on: 95 to 97 by the link frame's fixer and
verifiers, 98 by the performance pass.

95. **A link's frame at a display scaling that is no whole number** (125 %, 150 %). No check renders
    one: the headless pictures are at 100 %, and the real window runs at the owner's 200 %, where the
    frame's 4, 3, 2 and 1 px are whole pixels. At 150 % the 1 px inner line could round into the line
    box, as Fluent's own frame would.
96. **The frame under GitLab's legacy token link.** With the legacy link focused, the frame's top lies
    1 px from the token link's line box, the two links lying 4 apart; it is clear of the ink. No check
    takes a picture of it.
97. **`VcRadioButton`'s frame** (`Theme/Dialogs.axaml`) is built round its content, so it probably
    takes clicks just outside the radio button while it has the keyboard's focus, as `VcLinkButton`'s
    did in the link frame's first round. Not checked; for the review before the gate (status.md's step
    19).
98. **Q1 and the launcher.** The real-window pass measures Q1 with git itself on the PATH (Git Bash's
    `ucrt64\bin\git.exe`), not through `cmd\git.exe`, the launcher an installed user's PATH finds
    (D457; "The performance series"): for the review before the gate (status.md's step 19).
