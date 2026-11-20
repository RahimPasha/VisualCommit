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

The other parts, A (6), U (3) and G (3), are added just before their packages are built (D244), as
design 0.5 lists them.

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
  scripted picture that shows the version shows `0.1.0 (1a2b3c4)` (`Phase8aExpectations.Common.PinnedVersion`)
  on every commit; the real window shows the built commit's, seven characters apart from its twin
  (part A). Parts L and I show no version.
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
  its controls. No key is sent to a window that is not the foreground window: part I's launches 5 and
  6, whose first input is a key, assert first that the notice is the foreground window (12.3.4).
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
  every later merge and of the gate, compared with `scripts/compare-pictures.py --expected <file>`,
  whose summary has the headings "Changed outside the expected regions", "Changed, not expected",
  "Expected, not changed" and "Changed size". Two of part L's own pictures change at later merges,
  `l3a-palette-placeholder` (§8's and §11's) and `l3f-settings-name-placeholder` (§5's and §8's), and
  the lead's `--expected` file for each names them (0.7). A third picture of 8a's own changes at later
  merges, part K's `k3a-palette-cancel`, at §8's and §11's in the palette's scroll thumb, and the lead's
  `--expected` file for each of those merges names it (0.7). The real-window captures are compared with
  phase 7b's at the gate. Every picture is inspected (D247).

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
  (method 6) gives none, so the machine's search runs as in every earlier check. `askPassHelper` is left
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
| 6 | `Check_F6b_real_git_broken_config` | F6b | one empty tab; no probe (the machine's search); the session's global file `GitIsolation.SessionGlobalConfigFile(data)` holds `[user` on its line 1 | 1100×700, dark | none |
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
  `Git said:` over `GitSetupGitSaid` `fatal: bad config line 1 in file C:/Users/demo/.gitconfig` (one
  line, in `VcCodeFontFamily`, selectable); `GitSetupGuidance` `Git cannot read line 1 of
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
Windows, then press Check again.`; "Git said:" and `GitSetupGitSaid` as §5's builder settles (open point
13). The buttons, in order: `Download Git for Windows`, `Choose git.exe…`, `Check again`; no
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
   `GitStatus` marks that end: the second lookup is recorded when the search starts, and `GitStatus`
   reads `Git not found` again before `CheckHelper()` runs off the UI thread and before the screen and
   the command end (5.3's order; 1.2).

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
next start.` (git works) or ` VisualCommit looks for Git again when you save.`; `Browse…`, `Use the Git on
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
  up `FetchHold.HeldFor` after it started (`rounds` × 0.5 s of `sleep`: 60 s for the default 120
  rounds, longer on Windows, where each `sleep` is a process, `FetchHold.cs`) and leaves a `gave-up-`
  marker, on which `WaitForHeldAsync`, `Release()` and `Dispose()` fail. On Linux and macOS a cancel
  kills git alone (`GitRunner.Kill`, D36), so a cancelled held fetch's shell keeps waiting until a
  release: a cancel does not stop that clock. Method 3 therefore installs the hold with `rounds: 300`
  (`FetchHold.Install(data.Path, rounds: 300)`, `HeldFor` 150 s, under the hang watchdog's 3 minutes),
  so that it outlasts K8's walks of up to 200 Tabs on a slow runner; it releases (`Release()`) once
  each cancelled fetch has ended and been checked (K3's step 6, K8's step 9), so that no cancelled
  fetch's shell waits on; `Arm()` holds again before the next fetch (K8's step 7); and the method
  disposes the hold (a release) in a `finally`. `HeldGitRunner` (1.15) stands for a slow git: an
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
- Row 1's heading is a text that reads `VisualCommit.ico` (7.5), wholly above row 1's images; under it,
  row 1: 14 `Image`s, left to right in `IconFile.IcoSizes`' order, 16, 20, 24, 30, 32, 36, 40, 48, 60,
  64, 72, 80, 96 and 256, each showing the `.ico`'s frame of that size as `IconFile.Read` gives it; each
  `Stretch` `None`, its `Bounds` exactly N×N, its left and top on whole pixels, the 14 bottoms on one
  line; their lefts at x 24, 52, 84, 120, 162, 206, 254, 306, 366, 438, 514, 598, 690 and 798 (left 24,
  gap 12), the 256 ending at x 1054. (These lefts, and open point 45's, hold while each size's text is no
  wider than its image, as the bare number at 11 is; another form, open point 44, changes them, and §7's
  builder then writes them in `Phase8aExpectations.B`.) Under each image its size (open point 44):
  `Foreground` `#9BA3B0` (`VcTextSecondaryBrush`), `FontSize` 11, its ink `#9BA3B0` on `#14161B`.
- Row 2, wholly below row 1's sizes: nine `Image`s in this order, `png/icon-16`, `-24`, `-32`, `-48`,
  `-64`, `-128`, `VisualCommit.iconset/icon_16x16`, `icon_32x32` and `icon_128x128`, each showing the
  file of that name; each `Stretch` `None`, its `Bounds` exactly its pixel size (16, 24, 32, 48, 64, 128,
  16, 32, 128), on whole pixels, the row laid out as row 1 (open point 45).
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
  lefts and bottoms; the heading `VisualCommit.ico`; no text cut.
- Inspected: as B1, on white. The tile's edge holds against `#FFFFFF`; where the white line reaches the
  tile's left and right edges it meets the white background, so the tile's outline is open there, at
  every size (round 2's README: on a light desk the line's white ends blend into it at 16 to 24 pixels).
  That is the mark, not a cut.
- After step 2: the window has closed.

#### Texts these checks rely on (part B)

The app shows no new text (7.3): the window's title stays `VisualCommit`, which the real window's framed
captures show right of the mark; no control gains a name (§9's sweep sees nothing new). The sheet's texts
are the harness's, not the app's: row 1's heading `VisualCommit.ico` and, under each of its frames, its
size (7.5; its form is open point 44). The files' names and sizes are 7.3's: the `.ico`'s 16, 20, 24, 30,
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
   - **A1 and A4**: part A's steps (design 8.5, 12.3.4 launch 3; written with part A).
   - **B2**, the title bar, light: once A4 has ended (12.3.4: "Esc, the focus back on the button.
     Esc.", the second Esc closing the settings screen, About with it, which the method waits for as a
     named condition with a limit, 12.3.1; written with part A), with no dialog over the toolbar: a real
     click on `ThemeSwitch`; wait until the window shows the light theme (the condition is open point
     50); the pointer at the neutral point. `CaptureWindow()` saved as
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
window (the taskbar's). 12.9's risk that "the
owner's icon, chosen after D246's runs, changes B's captures" no longer holds: the mark was chosen
before §7 is built (1.17), so D246's runs already capture it.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Each check is named by its part's letter
and its place in its part's design list (2.5 for part L, 4.6 for part I, 5.5 for part F, 6.5 for part K,
7.5 for part B). Its steps and expected results are in its part's section above, its methods and starts
in the part's methods table. "R" in Notes marks the steps the real-window pass repeats (design 2.5, 4.6,
5.5, 6.5, 7.5, 12.3.4, 12.5; part B's are steps of its own, with no scripted twin); "Real window" says
"n/a" for a check the pass does not repeat. "Screenshots" lists the planned pictures, the scripted run's
and then the real window's.

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
| F12 | The settings screen's `Git program` row, last in the global scope, never in `This repository`: `Found on the PATH.`; Browse, `Chosen here. VisualCommit uses it from its next start.`, Save writes `gitExecutable` and changes no git in use; `Use the Git on the PATH` removes it; a too-old file refused (rulings 40, 42); "graph", dark | Not run yet | n/a | `f12a-row-next-start`, `f12b-row-too-old` | Not R. Method 14 |
| F13 | The screen by keys: F6 to `Install with winget…`, Tab to `Download Git for Windows`, `Choose git.exe…` and `Check again`, Enter searches again and `Check again` keeps the focus with its outline (5.3, keyboard.md D-a); one restored tab on "graph", dark | Not run yet | Not run yet | none | R: launch 7, each Tab's focus asserted before the next key, Enter only on `GitSetupCheckAgainButton`. Method 1 |
| F14 | The netrc note: signed in with "Use for git over HTTPS", git 2.43.0: no file, no note; `_netrc` in the session's home, `Your home folder has a .netrc or _netrc file. With Git 2.43.0, …` in `#E0A23B`, `Dismiss`; dismissed for the session (ruling 8, D424); one empty tab, dark | Not run yet | n/a | `f14a-netrc-note` | Not R. Method 15 |
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

## Checks by hand

What the gate checks by hand, outside the scripted run and the real-window pass, by the lead or the
owner; each is recorded here when the gate runs. Result values as under "Checks".

| Check by hand | From | Result | Notes |
|---|---|---|---|
| The icon's look-alike comparison: the owner's mark, by eye, against Git's logo and the well-known Git clients' icons, with no image search (the owner's wish, 2026-11-20) | Part B (design 1.17, 7.8; 13.4 item 11) | Not yet | |
| A second start of the app while another window is in front of the first, from the lead's isolating start file; what came to the front is noted | Part I (design 4.6; part I's conventions, "By hand at the gate") | Not yet | macOS's second start is 8b's Mac checklist |
| The taskbar button's icon, if B2 could not find the button: the owner's mark, not .NET's default icon | Part B (B2; design 7.5, 12.3.4; open point 51) | Not yet | Only if B2's taskbar step is "Not run" |
| The Narrator walk-through, 21 steps | Part U (D411, D450) | Not yet | Its steps are added with part U |
| Phase 6's System step: with System chosen in the settings screen, Windows' app mode switched while the app runs; the window, its title bar and the open views follow within a second | Phase 6 (D224; phase 6's report, "By hand on Windows at acceptance") | Not yet | |
| The guide's site, from the gate commit's artifact | Part G | Not yet | Its steps are added with part G |

## What proves each Delivers item

The rows of the Delivers items of parts L, I, F, K and B (design 0.1); the other items' rows are added
with their parts.

| Delivers item | Checks | Elsewhere |
|---|---|---|
| The contrast fixes in one round (D410; plan.md's items from phase 6, 768 and 769) | L1 to L6; real window L1, L2, L4 a, L5 dark, L6 a to c | `ThemeTokensTests` (`Every_text_reaches_4_5_to_1_on_every_background_it_lies_on`, `The_contrast_rounds_values_are_the_designs`, `The_placeholder_has_its_own_token_at_full_opacity`, `Both_themes_hold_the_same_keys`); `PanelViewTests`' three tests; `Phase8aExpectationTests.L`; wave 1's masked comparison against phase 7b's references with the scan's boxes, and the scan's eight lists in `docs/test-reports/phase-8a-contrast/` (design 0.7, 2.6) |
| Two instances; the macOS data folder (plan.md 778) | I1 (real window), I2 (and real window), I3; by hand: a second start behind another window | `SingleInstanceGuardTests`, `InstanceNoticeViewModelTests`, `MainWindowBringForwardTests`; `AppPathsTests.The_default_data_folder_is_the_platforms_documented_one`, read in CI's macOS job's log; `Phase8aExpectationTests.I` |
| First-run experience (R4, D405; plan.md 757; D423, D449, D453): git missing, too old or not working, each state with its own text; "Check again" without a restart; "Choose git.exe"; winget on Windows after a confirm, in a visible terminal, where winget exists; the Command Line Tools or Homebrew on macOS, the distribution's package on Linux; the missing askpass helper; with plan.md 781's visual check (missing, too old, not working, found again by "Check again") | F1 to F13; real window F1, F2, F4, F5, F6, F6b, F13 | `GitSetupShellTests` (each state: no clipped text at 1100×700 and 1000×560; the view's name and help text); Git.Tests' `GitLocatorTests`, `OsReleaseTests`, `GitStandInTests`, `GitConfigurationForgetTests`; App.Tests' `GitAccessTests`, `CheckGitAgainTests`, `GitSetupTextsTests`, `GitSetupViewModelTests`, `GitProgramViewModelTests`, `FileLauncherTests` (`ResolveStart`, `ToStartInfo` of the winget command, the one real `StartAsync`), `JsonSettingsStoreTests`, `MainWindowViewModelTests`, `SettingsDialogViewTests`; `Phase8aExpectationTests.F`; §9's sweep in the states (10) to (13) and the Narrator walk-through's steps 20 and 21 (part U, by hand); the guide's troubleshooting page and the first-run links (§11, part G); the lead's by-hand `cmd.exe /S /K ""<the path found>" --version"` before §5's merge (13.4 item 7) |
| A broken `~/.gitconfig` shows its own message (plan.md 767, from phase 6's design 8.5) | F6 (the fake), F6b (real git, also in CI's floor job on 2.34.1); real window F6 (the stand-in), F6b (real git) | `GitLocatorTests`: real git with `[user` in `GIT_CONFIG_GLOBAL` gives `ConfigFault(file, 1)`, on every CI job |
| OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE` (plan.md 766, risk 15; ruling 7, D424) | none: a log line, nothing on screen | `AskPassBrokerHelperTests` (the Info line `Git and ssh get SSH_ASKPASS_REQUIRE=force, so that ssh asks through VisualCommit; OpenSSH older than 8.4 ignores it and asks in the terminal VisualCommit was started from, if there is one.` once per session, at the first Ask or Deny scope); the guide's page (§11), whose proof is added with part G |
| The netrc note (plan.md 773, risk 35; ruling 8, D424) | F14 | `AccountsNetrcNoteTests` |
| Keyboard gaps (R7; plan.md 759, 784 and "Done when" 786: every main action reached by keyboard): a hunk and chosen lines from the diff, a blame run and its menu, "Cancel {title}" in the palette, the stage rows' buttons on keyboard focus; Tab out of every read-only text view; a panel resized by the keys (design 0.1, 6.1, 6.3.9) | K1, K2, K3, K4, K8; real window K1, K2 | `KeyboardPassTests`; `DiffTextViewTests`, `ConflictPanesTests`, `BlameViewTests`, `WorkingChangesTests`, `StatusBarTests` (6.4's fail-first tests); `RegionKeysTests`, `CommandPaletteViewTests`, `PaletteEntriesTests`, `AppShortcutsTests` passing alone (6.8 item 1); phase 2's `Check_17` and phase 5's `Check_A4_real_keys` alone in the real window at §6's merge (6.8 item 5); the inventory of main actions (design 6.3.9) in the report, from §6's merge report, with its rows "Inline or side by side", proven by K1 (its scripted-only step 11: Tab and Shift+Tab to `SideBySideDiffButton`, Enter), and "Amend", proven by K7 (disabled while committing) and `KeyboardPassTests` (focused by Tab, the focus given back), as the lead settled open point 40 |
| Focus after "Choose" (plan.md 771; phase 7a's design 17 item 5) | K5 | `KeyboardPassTests` (after Choose and after Esc in the browser; the create form's Pick, ruling 51) |
| The loading diff (plan.md 772; phase 7a's design 17 item 11) | K6 | `DiffTextViewTests` (`FocusBody(() => false)`), `KeyboardPassTests` (a held read, an `Empty` body turning to `Text`, a diff opened by Enter still taking the focus) |
| The commit boxes (plan.md 774; D392) | K7 | `WorkingChangesTests` (a held `CommitAsync`, `CanToggleAmend`), `KeyboardPassTests` (Amend focused by Tab gets the focus back) |
| Icons and branding (R5, D407; plan.md 764): the icon's SVG, the tool that makes the `.ico`, the `.icns` and the PNG files, the window's and the executable's icon; with plan.md 782's visual check, the icon in the window and the taskbar | B1, B2; real window B1 (a) to (c) and B2 (the executable's icon, the window's two icons, the title bar dark and light, the taskbar button) | `IconFilesTests` (items 1 to 8: the masters' rules, the manifest's hashes, the `.ico`, the `.iconset`, `png/`, `Assets/Icon`, the `.icns` (item 7, D447) and `iconutil` both ways (item 8, in CI's macOS job, whose log shows it passed, not skipped)); `WindowIconTests` (the main window's and the notice window's `Icon`, `AppIcon`'s three URIs); `Phase8aExpectationTests.B`; the files: the masters in `assets/brand/`, the generated files, `tools/VisualCommit.IconBuilder` (built in CI's six jobs; a second run of it leaves `git status` clean, 7.8 item 2); the owner's choice recorded in `docs/brand/icon/round-1/` and `round-2/` (D447); the look-alike check by eye, with the checks by hand |
| The README's "Name" note names the icon (D412; plan.md 764) | none | The README's "Name" note, design 7.3's text, written by the lead at §7's merge: proven by the file (design 0.1). No check of part B proves it; it stands here because it lands at §7's merge |

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
  unchanged (5.2); `MergeToolHelpTests.HeldToolHelpRunner` becomes `internal` and asserts nothing new.
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
  nothing changed; the lead's wave-2 run shows nothing from this package.
- **No earlier test changes** (7.4): no test reads a window's `Icon` or the app's resources (the `.Icon`
  hits in `tests/` are the glyph control's); `ShellTests`, `PanelViewTests` and `MainWindowCloseTests`
  make a `MainWindow` and pass unchanged (7.8 item 1).
- **8a's own pictures that show the icon** (7.6, 7.8, 1.17): B1, B2, their real-window captures and
  §8's About (`AboutIcon`, part A) show the owner's mark from their first run, since §7 builds with round
  2's masters and writes no placeholder; no picture of a placeholder ever exists, so none changes with
  the choice. Should the masters change again, those pictures change and are inspected again; part B's
  assertions name no shape (7.6), so none of them changes.

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

After the two fixes above the run's scripted pictures are the references of every later merge
(`D:/Projects/Personal/phase8-work/references/wave1/`, 1,185 pictures with their hashes beside
them). The 200 real-window captures were not taken again; the gate takes them.

## Found by the gate

Not yet.

## Could not run

Not yet.

## Other platforms

Not yet.

## macOS checklist for the owner

Not yet. Design 0.5 and 4.6 send macOS's second start to 8b's Mac checklist.

## Open points

What the lead settled, then where the design is silent or leaves a value to its builder about parts L
and I, with the reading this report takes; then parts F, K and B, each under its own heading, their
points numbered on. The builder of the part, or the lead, settles each open point before the part's
merge.

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
   the screen lays them out otherwise, and the report's F13 and every "in order" follow.
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
   whose `ChosenPath` is the path, and `Lookups` holds every lookup received, probes among them, in
   order. The rest of this point stands.
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
    asserts the first's absence after Cancel either way. §5's builder.
12. **Transient states.** `Checking…` on `GitSetupCheckAgainButton`, `Looking for Git...` in
    `GitStatus` (F4, F13) and `Checking…` in `GitSetupError` (F8) last while the probe answers; 1.15's
    fake has no hold, so a check cannot wait inside the search. §5's builder adds an optional held answer
    to `FakeGitSetupProbe` (as part I's `HeldNoticeTry`), or the check reads them in the dispatcher turn
    of the click; without either, those three expectations are read by `CheckGitAgainTests` and
    `GitSetupViewModelTests` alone, and the report says so.
13. **"Git said:" for a git that could not be started** (F7). 5.3 puts the start's message in the body
    (`… could not be started: {message}`) and `ErrorText` holds the same message (1.2); it does not say
    whether "Git said:" and `GitSetupGitSaid` show it too. §5's builder; the lead, before F7's expected
    result is completed.
14. **`GitStatus`'s help text.** 5.3 gives `GitStatus` a `ToolTip.Tip`; 12.3.4 (launches 7 and 8)
    reads its help text through UI Automation. This report expects both, equal, as the app's other
    tooltipped controls set both. §5's builder.
    **Settled by the lead on 2026-11-20:** 5.3 gives `GitStatus` `AutomationProperties.HelpText` equal to
    its tooltip's text (`GitStatusToolTip`), since 12.3.4's launches 7 and 8 read it through UI
    Automation; no picture changes.
15. **The status bar's tooltip in ChosenMissing.** "Null without a path" (5.3): whether the detection of
    a gone choice carries the chosen path as its `ExecutablePath` is not said; F3 does not assert the
    tooltip. §5's builder.
16. **The row's note after Save** (F12 step 5). This report reads 5.3's "while the choice differs from
    the git in use" as covering a choice saved but not yet used (ruling 42): `Chosen here. VisualCommit
    uses it from its next start.` on reopening. §5's builder; the lead if otherwise.
17. **The picker's title and the `.exe` check in the settings row.** The row has no guidance, so its
    W and M L forms (`Choose git.exe`, `Choose the git program`) follow the running platform in this
    report; the screen's follow the guidance's. §5's builder.
18. **What F14 waits on before asserting no note.** `HomeHasNetrc` runs off the UI thread (1.10, 5.3);
    an absence can be asserted only after a known end: this report asks that the question be one of the
    screen's reads that `WaitForSettingsReadsAsync` waits for, or a condition of its own. §5's builder.
19. **The neutral point and the status bar.** The neutral point (760, 687) lies in the status bar; with
    `GitSetupButton` beside `GitStatus` and a long text (`Git 2.29.0 is too old (needs 2.34.1 or newer)`),
    it may fall on either, so that a picture shows a hover or `GitStatus`'s new tooltip. Every part F
    picture asserts neither; if one fails, the lead moves part F's resting point (5.3 does not place
    `GitSetupButton`).
20. **Phase 6's `AssertFits`, and F12.** The global scope with the row must still fit at 1100×700 and
    1920×1080 (phase 6's S14 at `s14c` and `s14i`); if it does not, ruling 3: §5's builder stops and
    asks the lead. F12 asserts no fit of its own (the design fixes none): on "graph" the global scope
    also holds the two notes `graph sets its own: …` under Name and Email (as part L's
    `l3f-settings-name-placeholder` shows), so less room lies free there than in `s14c`, and the row's
    height is §5's builder's to set. If the row does not fit there, `f12a` and `f12b` show a scroll bar,
    and §5's builder tells the lead.
21. **The scenario of F1, F4 and F12.** 5.5 names none; this report takes "graph" (`Scenarios.GraphAsync()`),
    which `Phase8aExpectations.F` names for §12's launch 7. §5's builder may take another before the
    merge, with the report's text changed to match.
22. **The helper's path in F11.** `askPassHelper` is "a path without a file" (1.10); this report gives
    a made-up path per platform so that `f11a` is the same on every run. If the session turns it into a
    full path of its own (a path relative to the current folder off Windows), §5's builder uses a path
    under the data folder instead and the picture's Notes say it varies by nature.
23. **F14's sign-in.** This report signs in through `SignInAsync` (phase 7a's A11), not the dialog, with
    GitHub's fake in process (no port, ruling 20); §5's builder may take another route that leaves
    `useForGit` saved on.
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
    and the token set on the note.
28. **The focus after F2's Cancel** (F13, launch 7). Whether F2's Cancel gives `GitSetupWingetButton`
    the focus back depends on how §5 builds the winget command, which the design leaves open (5.3:
    "confirm, then 1.3's start"): a command that is disabled while it awaits its confirm is not given
    the focus back by D104 (the case 6.3.5 handles for other openers). F13 does not depend on which
    way it is built (its step 4 always sends at least one F6), and F2 asserts nothing about the focus
    after Cancel. §5's builder.
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
    builder confirms the counts, and `Phase8aExpectations.K` holds them.
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
    counts, and `Phase8aExpectations.K` holds them.
32. **K2's run 2 and its count of Down.** 6.3.2 numbers the runs from 0 ("run 0's menu"), so run 2 is the
    third run, line 4, `0575c7f`; keyboard.md's test (a), which the design draws on, says "run 2:
    `SelectedRun.Index == 1`". This part takes index 2: 6.5's "its commit in the right panel" holds only for
    it, run 1 (line 3) being not committed yet. The count, three Downs, takes the caret on line 1 when the
    page opens: 6.3.2 says opening picks nothing, not where the caret is. §6's builder confirms both, and
    `Phase8aExpectations.K` holds them (6.5). The real window waits for each Down by the right panel, not
    by the caret (open point 41).
33. **K5's keys.** 6.5 writes "From GitHub…" by Tab and Enter, and "a row, Enter". The button lies before
    `CloneUrlBox` in the Tab order (`WelcomeView.axaml`'s `DockPanel`), and the form opens with the focus in
    the box (phase 7a's B1), so this part takes one Shift+Tab. For the row it takes Tab into the list and
    Home, which selects the first row, `octo/hello`, whatever row the Tab left focused, so that the form's
    values are `b06a`'s; 6.3.9's inventory writes "arrows". How `k5a` is held against `b06a` ("`b06a`'s form
    outside the URL's text", 6.5) the design does not say: this part leaves it to the gate's inspection,
    the URL's port differing by run; a masked comparison would need a box the lead names.
34. **K7's "type Next".** The summary holds `First` while the commit is held; typed onto it, `Next` would
    give `FirstNext`. This part selects the summary's text first (Ctrl+A), so that the summary reads `Next`
    as 6.5 expects.
35. **K8.** "With K3's held fetch" is read as a second fetch held the same way: K3's own fetch ends at K3's
    Enter. 6.5 puts the numbers of Tabs "in the report" but gives no bound for the walk to `LeftSplitter`:
    this part bounds both walks by 6.4's 200 (G7's bound) and records both numbers. After Enter on Cancel
    the button hides as the fetch ends, and the design does not say where the focus goes, so the walk to
    `LeftSplitter` starts from F6 to the right panel again. The 10 per press is keyboard.md's, unverified
    until G8's test (6.9 item 3); whether D427's code is needed is that test's to say (6.1), and the
    expected result, the session's `LeftPanelWidth` 270, holds either way.
36. **K3's held fetch.** 6.4's `StatusBarTests` hold a "Fetch origin" queued straight on the tab; a scripted
    check starts the real one from the remote's menu and holds it at the remote (`FetchHold`, phase 6's).
    On "graph" the remote holds nothing new, so a fetch that was not cancelled would move no ref either:
    K3's proof of the cancel is the operation's outcome and the status text, and "no remote ref moved"
    (6.5) is asserted as the design asks. A commit added to `origin.git` before the start would make it a
    proof of its own; the lead may ask the builder for it.
37. **Unverified points of 6.9 that the checks meet.** Tab from a focusable `TextArea` set to `Continue`
    entering the bars, then leaving, and Enter reaching a button inside it (item 1; K1); `:focus-within`
    with `:not()` in Avalonia 12.1.3 (item 2; K1, K4); `GridSplitter`'s arrows and `DragCompleted` (item 3;
    K8); a tab's git calls using the detection's runner (item 5; K6, K7, whose pictures drop if not, 6.4).
    §6's fail-first tests settle them before the checks run; a check that meets one unsettled is reported
    to the lead.
38. **The focus frame's look.** The design names no colour for Fluent's frame (6.3.1); the checks assert
    `:focus-visible` and the gate inspects the frame in `k1a`, `k1c`, `k4a`, `k4b` and `k8a`.
39. **The inventory** (6.3.9) enters the report from §6's merge report, with 6.3.10's lines for the guide
    (to §11): the lead, at §6's merge.
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
  13.3's row "Before the counted round" (the chosen mark regenerated, or 8a closing with the placeholder)
  and 12.9's risk of a choice after D246's runs no longer apply. 7.3's placeholder drawing and its
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
    another form changes them, and §7's builder then writes them in `Phase8aExpectations.B`. §7's
    builder, with the lead quoting them under part B's texts before §7's merge.
45. **Row 2's layout.** 7.5 gives row 1's layout (left 24, gap 12, bottoms aligned) and row 2's contents
    and order only. This report reads row 2 as laid out as row 1, below row 1's sizes: lefts 24, 52, 88,
    132, 192, 268, 408, 436 and 480, ending at x 608. Neither row's top nor the heading's height is fixed:
    the checks assert bottoms aligned, whole pixels and both rows inside 1100×700. §7's builder confirms.
46. **The sheet's ids.** 7.5 names none. No step clicks or focuses anything in the sheet, so the checks
    read `IconSheet`'s images and texts from the control itself, row by row in order. §7's builder.
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
