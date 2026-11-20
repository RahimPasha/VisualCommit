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
in their order); "Changes to expected results" says what part L changes on purpose; "Open points" first
says what the lead settled on 2026-11-20, where the design disagreed with itself, then lists where the
design is silent or leaves a value to its builder, with the reading this report takes. The review's
findings were applied on 2026-11-20, after the lead's rulings of that day.

The other parts, F (14 checks), K (8), B (2), A (6), U (3) and G (3), are added just before their
packages are built (D244), as design 0.5 lists them.

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
  Control on every platform, CI's macOS job included (phase 6's rule).
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
  `LayoutAudit.FindClippedText` (shortened text that shows whole in its tooltip is allowed).
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
  the lead's `--expected` file for each names them (0.7). The real-window captures are compared with
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

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Each check is named by its part's letter
and its place in its part's design list (2.5 for part L, 4.6 for part I). Its steps and expected
results are in its part's section above, its methods and starts in the part's methods table. "R" in
Notes marks the steps the real-window pass repeats (design 2.5, 4.6, 12.3.4, 12.5); "Real window" says
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

## What proves each Delivers item

The rows of the Delivers items of parts L and I (design 0.1); the other items' rows are added with
their parts.

| Delivers item | Checks | Elsewhere |
|---|---|---|
| The contrast fixes in one round (D410; plan.md's items from phase 6, 768 and 769) | L1 to L6; real window L1, L2, L4 a, L5 dark, L6 a to c | `ThemeTokensTests` (`Every_text_reaches_4_5_to_1_on_every_background_it_lies_on`, `The_contrast_rounds_values_are_the_designs`, `The_placeholder_has_its_own_token_at_full_opacity`, `Both_themes_hold_the_same_keys`); `PanelViewTests`' three tests; `Phase8aExpectationTests.L`; wave 1's masked comparison against phase 7b's references with the scan's boxes, and the scan's eight lists in `docs/test-reports/phase-8a-contrast/` (design 0.7, 2.6) |
| Two instances; the macOS data folder (plan.md 778) | I1 (real window), I2 (and real window), I3; by hand: a second start behind another window | `SingleInstanceGuardTests`, `InstanceNoticeViewModelTests`, `MainWindowBringForwardTests`; `AppPathsTests.The_default_data_folder_is_the_platforms_documented_one`, read in CI's macOS job's log; `Phase8aExpectationTests.I` |

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
and I, with the reading this report takes. The builder of the part, or the lead, settles each open
point before the part's merge.

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
