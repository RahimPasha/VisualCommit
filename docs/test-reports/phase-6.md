# Phase 6 visual test report — Multi-repo and productivity

The Check, Steps and Expected columns of the table below were written at the start of the phase,
before the UI they test was built (D15), from `requirements.md` (C1, O3, O5, O9, Q4, Q5, R7),
`plan.md`'s phase 6, `docs/phase-6-design.md` and decisions D187 to D231 (D187 to D202 the owner's
answers, D203 to D231 the design's). The scenario facts were derived first, from the scenario
repositories as the merged scenario code builds them (`tests/VisualCommit.Testing` at `b9c29d1`),
read through the app's own git layer with Git 2.36 and their texts replayed with Git 2.55; the checks
were then drafted part by part (W, K, S, G and L), each part reviewed independently and revised, and
the five parts assembled into this report, which made their names, pictures and texts agree. Every
git state in the checks was derived with Git 2.36.0.windows.1 on copies of the scenario repositories
built by the scenario code itself, whose SHAs equal those `Phase6ScenarioTests` (and, for the earlier
scenarios, the earlier phases' reports) pin, and every git text was replayed with Git 2.55.0.windows.5.
No phase 6 UI existed when the checks were written: every position is derived from the design and
taken by the checks from the controls' bounds (D122's rule), and every new text is design section 14's.
Values the design leaves open are chosen in "Decisions this report makes", for the lead to fold into
the design; what the assembly left open the lead has ruled on, under "The lead's rulings on this
report's open points". The result columns are filled in when the gate runs.

How to read it: "What the checks share" holds what every check relies on (the conventions of every
part, the scenario facts, then each part's conventions, test methods and app starts, scenarios and
graphs, and the texts the design fixes); "Checks" is the one table of all 34 checks, each named by its
part's letter and its number in design 17.4; "What proves each Delivers item" maps `plan.md`'s list
and the owner's decisions D187 to D202 to checks and tests; "Changes to expected results" lists what
phase 6 changes in earlier phases' checks on purpose, by phase and check; "How the expected git states
were derived" says how every git state in the checks was obtained; "Decisions this report makes"
lists the values the report chose, each with the design section it fills. "RW" marks a check, or
steps of it, that the real-window pass repeats; "n/a" says why it does not; both are written in each
row's Notes.

The real-window pass runs on the development machine: Windows 11 at 200 percent scaling, whose work
area of 1500×952 logical pixels holds a 1100×700 window and no 1920×1080 one, so every step at
1920×1080 is scripted only (the plan's conditional part). It acts inside the app's window and, where
a check says so, in the app's own native folder and file dialogs (W1, S14); it never changes the
owner's desktop: no change of Windows' theme or high contrast (D224), no editor or other program
started (`Open file`, `Open in editor`, `Show in folder` and `Copy path` are never clicked there).

## Run

Filled in when the gate runs, for each of its two passes: date, app commit, the machine (its
operating system and version), the display scaling of the real-window pass, the screenshot
folders, and how many pictures were inspected. The gate's two passes are run on one commit by a
session on the Windows machine.

If a cloud session ran the scripted pass earlier, its run is listed here too, marked "cloud
session, before the gate", and its results stand in the Scripted column until the Windows
session replaces them with its own. It does not count as the gate's scripted pass.

What the gate runs, as this report plans it: phase 6's scripted checks in two projects,
`tests/VisualCommit.VisualTests.Phase6` (parts W, K, S and G, 38 methods) and
`tests/VisualCommit.VisualTests.Phase6Look` (part L, 9 methods; the lead's ruling 1), run by `dotnet
test` at the repository root beside the earlier projects: 47 test methods saving 198 pictures under
`artifacts/visual/phase-6/scripted/`, the one folder of both projects (their step times under
`artifacts/visual/phase-6/timings/`). The real-window pass (`dotnet test
--project tests/VisualCommit.RealWindowTests -c Release`) repeats the RW steps of W1, W2, W4, W9, K10,
S14, S15 and L34, eight methods in nine launches, and saves 26 captures, the difference pictures of
those compared with a scripted twin and `run.txt` under `artifacts/visual/phase-6/real-window/`; S15's
three captures have names of their own, their twins those of the owner's mode (design 11.8). The
earlier phases' pictures are compared with reference pictures made on `master` before the gate, region
by region (design 17.7; "Changes to expected results").

## What the checks share

### Conventions of every part

What every part relies on. Each part's own conventions follow under its heading and add to these;
where a part's convention is narrower, it says so.

- **Parts and projects** (design 17.3, 17.4, D230). **W** workspaces and the dashboard (W1 to W9),
  **K** the command palette and the keys (K10 to K13), **S** the settings screen and its effects
  (S14 to S21), **G** git behaviour: ignored files kept, and fetch refspecs (G22 to G25), **L** the
  look: every main screen in both themes, the light colours, the long folder and the tab strip (L26 to
  L34). The letters avoid the requirement prefixes C, O, Q, R and T, so that a check's name never reads
  as a requirement; the numbers run on across the parts. The scripted checks of parts W, K, S and G
  are in `tests/VisualCommit.VisualTests.Phase6`, part L's in a project of its own,
  `tests/VisualCommit.VisualTests.Phase6Look` (the lead's ruling 1), which links the base, the support
  and the common expectations of `VisualCommit.VisualTests.Phase6` as `Phase5Undo` links `Phase5`'s
  and saves its pictures in phase 6's one folder; one class per part (`Phase6ChecksW`,
  `Phase6ChecksK`, `Phase6ChecksS`, `Phase6ChecksG`, `Phase6ChecksL`, in
  `Phase6Checks.<part>[.<topic>].cs`), with what they compare against in
  `Phase6Expectations.<part>.cs` (which the real-window pass links: its pattern
  `..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs` covers both projects) and their tests
  in `Phase6ExpectationTests.<part>.cs`; `Phase6Checks.Support.cs` holds 17.2's helpers and the parts'
  additions. The real-window pass's code is `Phase6RealWindowPass.cs` with `.W.cs`, `.K.cs`, `.S.cs`
  and `.L.cs`, and `RealApp.Phase6*.cs`; part G has no real-window step.
- **What the parts hold**, as written (design 17.3 first counted 38 methods, 17.4 about 156 pictures;
  by the lead's ruling 1 the design takes the counts below):

  | Part | Checks | Test methods (app starts) | Scripted pictures | Real-window methods (launches), captures |
  |---|---|---|---|---|
  | W | W1 to W9 | 11 (12) | 45 | W1, W2, W4, W9 (5 launches: W9's restart two), 14 |
  | K | K10 to K13 | 8 (8) | 17 | K10 (1), 4 |
  | S | S14 to S21 | 13 (14) | 42 | S14, S15 (2), 4 |
  | G | G22 to G25 | 6 (6) | 26 | none |
  | L | L26 to L34 | 9 (9) | 68 | L34 (1), 4 |
  | `VisualCommit.VisualTests.Phase6`: W, K, S, G | 25 | 38 (40) | 130 | 7 methods (8 launches), 22 |
  | `VisualCommit.VisualTests.Phase6Look`: L | 9 | 9 (9) | 68 | 1 method (1 launch), 4 |
  | All | 34 | 47 (49) | 198 | 8 methods (9 launches), 26 |

  47 methods pass 17.3's 40, past which 17.3 moves part L whole into
  `tests/VisualCommit.VisualTests.Phase6Look`, with the class and file names part L's conventions give;
  the lead's ruling 1 does so. `VisualCommit.VisualTests.Phase6` keeps parts W, K, S and G (38
  methods), `Phase6Look` part L (9). WP-V's part L creates the project by WP-S's recipe; the lead adds
  it to `CLAUDE.md`'s Commands and `plan.md`'s gate step 3 when it exists. The real-window pass is one
  project for both.
- **One test method per app start** (phase 3's rule, `FreshApplication`), each on a fresh copy of its
  scenario and a fresh data folder, so a check with several starts is several methods; each part's
  "Test methods and app starts" table names them. A check that restarts the app on one data folder
  (W9, S15 part B) is a plain `[Fact]` with two `FreshApplication.RunAsync` halves (D32, phase 5's A3).
  A check of several methods says why in its part's decisions (WD6, WD36, KD5, SD1, GD1). No method may
  come near the hang watchdog's 3 minutes: each part estimates its methods' git processes in its
  conventions, and the lead times the longest with `bash scripts/time-scripted-check.sh "<method
  filter>" 6` when they first run (part W's W6 to W9, part L's L27 and L31 part A first, in
  `Phase6Look`: part L's conventions), splitting a
  method that takes more than about 90 s alone at a step that needs nothing from earlier steps, on a
  fresh copy; a split made then goes under "Changed in this report after its first commit".
- **Defaults.** Unless a check says otherwise, a method writes its session on a fresh copy, starts the
  app at 1100×700 in the dark theme and waits until it is ready: `Phase6Checks.OpenAsync` for a
  repository's tab (the graph drawn, the status shown, the queue idle),
  `Phase6Checks.OpenWorkspaceAsync(data, copy, width, height, theme)` for "Web" with its dashboard as the
  only tab (the dashboard's first read pass and every row's queue idle, part W's conventions),
  `ShellDriver.StartWithTabs` for a session of several tabs. The checks at 1920×1080 are W3 and S14 part
  B; L33 starts at 1000×700. The sweep (L26 to L31) and L32 start with `"theme": "System"` and the fake
  system theme dark, L33 with no theme (Dark), L34 with Light, part S as each check names. Each check's
  row names its scenario, theme and window size.
- **What 17.2 gives every session** (D220): the frozen record clock (every activity-log time
  `09:00:00`, every duration `0 ms`) and the fixed backup clock (the first backup of a session
  `…-20260102T090001000Z`, each later one a second after the last), as phase 5's; auto-fetch off
  (`VISUALCOMMIT_AUTO_FETCH_MINUTES=0`, D112); the session's own git configuration,
  `GIT_CONFIG_GLOBAL` = `<data>/harness/global.gitconfig` (absent until a check seeds it or the app
  writes it) and `XDG_CONFIG_HOME` = `<data>/harness/xdg`, with `GIT_CONFIG_NOSYSTEM=1`, which only
  part S seeds or writes; the fixed tool catalogues of design 12.9 (the editors `vscode`, `sublime` and
  `idea` under `C:\Tools\…`, `/opt/tools/…` off Windows; the merge tools `kdiff3`, `meld` and `vimdiff`
  found, `araxis`, `bc`, `p4merge` and `winmerge` not found; the fixed `PATHEXT` on Windows); and the fake
  system theme, dark unless the check says `systemDark: false`. In the real window `RealApp.Launch` sets
  `VISUALCOMMIT_RECORD_CLOCK=frozen`, `VISUALCOMMIT_BACKUP_CLOCK=fixed`,
  `VISUALCOMMIT_TOOL_CATALOGUES=fixed` and the session's git environment.
- **Dates and identities.** Dates are shown in UTC (`VISUALCOMMIT_TIME_ZONE=UTC`). A scenario's commits
  are by "Test Author" <author@example.com>, dated 2026-01-01 12:00 and a minute apart (each repository
  of "Web" from 12:00 again). What the app writes has D72's dates, 2026-01-02 09:00 UTC, and the
  identity git's configuration gives (the scenario's Test Author everywhere but "settings", whose
  identity part S seeds or saves), so a commit, stash or merge the app makes has the same SHA in every
  run, in both harnesses and on both gits; snapshot commits are by VisualCommit
  <snapshot@visualcommit.invalid> (phase 4's rule).
- **Placeholders.** `{root}` is a copy's folder, different in every run and on every machine: the
  workspace copy's (`TempWorkspace.Root`, `%TEMP%\VisualCommit.Tests\workspace-<12 hex>`) in the
  scenario facts and parts W, K and L, `{root}\<name>` a repository of "Web"; in part S the scenario
  copy's (`TempRepo`'s root), whose `settings` folder is the tab's working tree. `{data}` is the
  method's data folder; each part defines its others (`{ops}`, `{plain}`, `{clones}`, `{path}`). A text
  that holds a copy's folder is asserted with the copy's own path in place of the placeholder, and a
  folder the checks compare is matched as a folder, not as text, where git's spelling can differ (part
  W's `SameFolder`).
- **Rows, lanes and colours** are phase 5's: commit (and stash) rows from 0 at the top, as
  `SelectedIndex` counts them; while the working-changes row shows it is display row 0 and commit row r
  is display row r + 1. With no banner and no search bar, display row d's centre is y 129 + 26 d; a row
  is clicked "in its Message column", 30 px into it: x 468 with one or two lanes, 484 with three, 500
  with four, 516 with five. Lane L's centre is x 406 + 16 L. Lane colours are numbers (dark: 0 `#7C8CFF`,
  1 `#3FB97F`, 2 `#E0A23B`, 3 `#E5606B`, 4 `#4FB3D9`; light after D198: `#4353D8`, `#1C8655`, `#A16A1B`,
  `#C93A46`, `#1D7EA6`). A dashboard's rows count from 0 at the top in the workspace's order, row k's
  centre y 165 + 26 k (part W's conventions). The checks take every position from the controls' bounds
  (D122's rule): the numbers in the rows are what those bounds give with the default panel widths,
  asserted ±2 where stated (the settings card exactly, 7.1).
- **Names of graphs and states** are short names each part's scenarios section defines (W0, TD,
  OPS0, K-OPSk, ST0, IG1, RS2, H0t, TS11, …). A name means one graph or state across the report: OPS0
  ("ops" as built: phase 3's and phase 5's G0), IG0, RS0, ST0 and GR0 (phase 1's "graph") are defined in
  the scenario facts or in more than one part, alike; GC-M, FM and R1 keep phase 4's meaning, H0 and RL0
  phase 5's.
- **Labels and the left panel** are phase 5's: labels in phase 1's order (a detached `HEAD`, the branch
  HEAD is on with ✓, other local branches, remote branches, tags, stashes), "drawn" what the 130-wide
  Branch / Tag column draws (a name cut with "…", phase 1's "+N"), taken from `LabelsInRow`; a label is
  pressed, right-clicked or double-clicked at its centre, and only a drawn one. Left-panel paths are
  written Local branches › feature › alpha, Remotes › origin › pr › 1; a right-click there opens the
  menu only and selects nothing (D114); a folder that does not hold HEAD's branch starts closed.
- **Menus** are written header first, items in order, "—" for a separator, "(danger)" an enabled item in
  `VcDangerBrush`, "(disabled: "…")" a disabled item with its reason, "(highlighted)" the item with the
  focus, the first enabled one when a menu opens (D105), and a keyed item's key (6.7) after it, as
  " [key]" or "label [key]"; its key is right-aligned in the secondary colour, at least 24 right of its
  label, and is also its `AutomationProperties.AcceleratorKey`. A menu's height is 8 + 24 + 26 × items +
  9 × separators (±2), and it lies at least 4 px inside the window: opened by a right-click its top-left
  corner is 2 right of and 2 below the pointer, moved (or flipped) to stay inside; opened by a button it
  opens below the button. The menu layer's log line is `<repo>: Context menu opened: <header> by pointer`
  (or `by keyboard`), on a dashboard `Web: …`.
- **Dialogs** are phase 3's cards on the dialog layer over the backdrop, 400 wide (460 for the wide
  card); the command palette has no backdrop (D212) and the settings screen's card is design 7.1's. The
  error dialog is asserted with `AssertErrorDialogAsync` (`ErrorTitle`, `ErrorSummary`, `ErrorRepository`,
  `ErrorCommand`, the visible buttons left to right, `ErrorOutput`); a refusal the app makes before git
  runs has no output, no command and no "Copy output": "Close" only. "No question" and "no dialog" are
  `AssertNoDialogAsync` after the operation ended. While a dashboard tab is open, every operation's
  dialog names its repository (`In {repository}`, design 3.8).
- **The pointer.** Before a capture with nothing open, or with a dialog open, both harnesses move the
  pointer to the neutral point (760, 687): at 1100×700 it lies in the status bar (on a repository's tab
  left of the Activity log toggle, on a dashboard's right of every text part W shows there, WD23), at
  1920×1080 in the graph area or in the dashboard's list below its tenth row, where nothing has a
  tooltip. Part S rests the pointer at (760, 1000) at 1920×1080, since the settings card covers the
  neutral point there (SD2). A capture with a menu open, a drag running or a tooltip asked for keeps
  the pointer where the step put it (`keepPointer`). "Rest the pointer on X" moves it there and waits
  until X's tooltip is open; "move the pointer onto X" waits for nothing.
- **Waits** are on conditions, never on a fixed time: `WaitForIdleAsync` (its refresh included) and the
  queue's log line `<repo>: <Title>: <outcome> after <N> ms: <message>` for an operation, then
  `WaitForDialogAsync<…>` for a failure; on a dashboard `WaitForDashboardReadAsync()` (the pass's line,
  no row `Reading…`), `WaitForRowAsync`, `WaitForBulkRunAsync`, `WaitForRowPercentAsync` and "until no row
  of the dashboard reads" (part W's WD37); a stage-panel write by the status read with the counts the step
  names (`WaitForStatusReadAsync`); a change made outside the app while it runs by the condition the step
  names, within `OutsideChangeTimeout` (30 s); a network step within `NetworkTimeout` (60 s); the theme's
  flip by `SetSystemThemeAsync`'s own wait (17.2).
- **The call log** (`IGitCallLog`) is read with `CallsSince(sequence[, worktree])` and
  `AssertCalledInOrder`; a command is written as its `CommandText` renders it (an argument with spaces in
  double quotes). A call is recorded when it ends, never while it runs. "In this order" means in this
  order among the step's calls, others between them allowed; reads the app runs side by side are
  asserted as a set (GD18). What "no write" and "only reads" mean is each part's convention (W, S, G),
  each a list of command forms.
- **The toolbar and the status bar** are phase 6's in every picture: `Settings` left of Search (D197);
  an enabled button's tooltip and help text with its key (`Fetch all remotes (Ctrl+Shift+F)`), a disabled
  one's its reason alone (D215); Undo and Redo as the undo history stands (phase 5). On a dashboard tab
  they are part W's TD and TR. A check asserts them where they are its subject.
- **Keys** are pressed as the gestures the steps name (part K presses them literally,
  `ShellDriver.PressKey(Key, RawInputModifiers)`, never through `PressShortcut`, KD9) and written as
  Windows and Linux write them (`Ctrl+Shift+P`). The headless platform's command modifier is Control on
  every platform, CI's macOS job included (design 0.7, 6.4: `KeyTexts.Current` is `Ctrl` there), so
  every scripted run presses Control for Mod and expects the `Ctrl` texts in the palette, the menus, the
  tooltips, the settings list and `AcceleratorKey`; the macOS words (`Cmd+Shift+P`, `Option+Up`,
  `Cmd+Delete`) are `ShortcutTextTests`' and `PaletteEntriesTests`' (6.12). Where a row names a macOS
  text (`Cmd+Shift+Z`, `Cmd+Shift+F`), it is what a real Mac shows, for the macOS checklist, not what CI
  asserts.
- **Colours** are written by token with their dark and light values (part L's "Colours by token": phase
  6's light values after 11.10 to 11.13, the dark ones unchanged). A text's colour is asserted as
  "contains `#RRGGBB`" within its bounds, a background or a line as the exact colour at a sampled point
  clear of text, a translucent thing within ±8 per channel of the blend the step gives.
- **Git's own texts.** The checks quote Git 2.36.0.windows.1's texts (the call log's output, the error
  dialog's `ErrorOutput`) and assert them word for word only when `git --version` is 2.36; on any other
  git a check asserts the line marked "key", that `ErrorOutput` is `GitOutputCleaner.Clean` of the failed
  call's own output (`AssertGitOutputShown`: the cleaned standard error, or the cleaned standard output
  only when nothing is left of the standard error; the two streams are never joined), and the dialog's
  title, summary, repository line, command and buttons (phase 3's rule). Every quoted text was replayed
  with Git 2.55.0.windows.5; where 2.55 prints otherwise the check says so (`advice.diverging`'s nine
  `hint:` lines before a refused fast-forward, a fetch's or push's `pack-reused 0 (from 0)`). Git's
  states (refs, objects, files, the index, `git status`, the stash list, the configuration files, the
  backups, the in-progress files) are asserted on every version; "the exact state" is
  `TempRepo.ExactStateAsync`'s (phase 5's part P). CI's jobs run other gits (Windows and macOS 2.56.0,
  `ubuntu-latest` 2.55.0, the two containers 2.43.0), which this rule covers.
- **Test servers and holds** (17.2, D229). `AuthRemoteServer` serves part W's folder server over the
  workspace copy's `remotes/` (`Scenarios.ServeWorkspaceAsync`) on port 47393
  (`AuthRemoteServer.Phase6VisualChecksPort`) and part L's server of "auth" on port 47394
  (`AuthRemoteServer.Phase6LookVisualChecksPort`, the lead's ruling of 2026-10-22: the two projects run
  in processes side by side, so each has a port of its own, as D150 gave phase 1's clone checks
  47392); every expected text that shows
  the port goes through `Phase6Expectations.WithPort(text, server.Port)`, and a fallback to a free port
  (`PortWasTaken`) is noted in the gate's Run (D97). W4 and W6 take turns on
  `VisualCommit.VisualTests.Phase6`'s one UI thread, so they never hold 47393 at once; L31 runs in
  `Phase6Look`'s own process beside them (the lead's ruling 1) on its own 47394. The fetch hold (`FetchHold`) holds a repository's
  file-remote fetches until released; holds and servers are released and disposed in a `finally`.
- **Pictures** are named by `Phase6Checks.PictureName`'s rule,
  `^[gklsw]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`: the part's letter, the check's number, a step
  letter (and digits where needed), then words (`w4a-held-at-25-75-45`); a check's letters run on
  through its methods in order; part L's sweep adds `-dark` and `-light` to each pair. Every name is
  unique in the report, and `Shot` refuses a name it has saved in the run. A picture whose content
  varies by nature from run to run (a temporary folder's name drawn, a focused text box's blinking
  caret, an indeterminate progress bar, how far start-up has come, git's version) says so in its check's
  Notes; its expected result is structural, and the gate compares it by inspection, not by hash.
- **The real window** repeats the steps marked RW in a real process at 1100×700 with the real mouse and
  keyboard (FlaUI), finds controls by automation id and name, reads texts through UI Automation (a
  tooltip as the control's `HelpText`, a box through its Value pattern), and saves each capture under its
  scripted twin's name in `artifacts/visual/phase-6/real-window/`, with a difference picture and the
  share of differing pixels in `run.txt` (at most 3 percent, D33); S15's captures have names of their own
  (11.8). It needs an unlocked desktop and Windows' high contrast off (S15 fails while it is on, D224).
  It never changes the operating system's theme or any other setting of the desktop and never starts a
  program; 1920×1080 does not fit the development machine's screen, so steps at that size are scripted
  only.

### Scenario facts (every part)

The facts of phase 6's four scenarios ("workspace", "refspecs", "ignored", "settings") and of the
earlier scenarios phase 6's checks reuse ("ops", "auth", "conflicts", "graph", "history", "reflog"),
read through the app's own git layer (`GitRepository.OpenAsync`, `ReadRefsAsync`, `ReadStatusAsync`,
`LoadCommitsAsync`) and laid out by the app's own `GraphLayout`, on copies built by the merged
scenario code itself (`tests/VisualCommit.Testing` at `b9c29d1`), and checked against every pin of
`Phase6ScenarioTests` (no difference) and, for the reused scenarios, against the SHAs phases 1, 3, 4
and 5's reports give (no difference). Every git state below was derived with Git 2.36.0.windows.1 and
every git text replayed with Git 2.55.0.windows.5; where 2.55 differs, the fact says so. How they were
derived is in section 9. The parts refer to them as "scenario facts N" (their section numbers).
Nothing of phase 6's UI is built yet (WP-S, WP1 and WP2 are merged): every position in section 1 is
derived from the design, every text from the design's sections 2 to 14 and the existing
`OperationTexts`; the checks take positions from the controls' bounds (D122's rule), and a text the
app does not have yet is marked "(design)".

#### 0. Conventions used below

- **Rows** of a graph count from 0 at the top, `SelectedIndex`'s numbering; when the working-changes
  row shows (design-system, billing, history) it is display row 0 above commit row 0. **Dashboard
  rows** count from 0 at the top in the workspace's order (2.6); "row api" names a row by its
  repository.
- **Lanes** count from 0 at the left; **colours** are lane colour numbers (dark: 0 `#7C8CFF`,
  1 `#3FB97F`, 2 `#E0A23B`, 3 `#E5606B`, 4 `#4FB3D9`, as phases 4 and 5).
- **Dates** are author dates in UTC as the Date column shows them; the commit date is named only where
  it differs. All of phase 6's own scenario commits are by `Test Author <author@example.com>`, author
  date = commit date, from 2026-01-01 12:00 a minute apart per repository (each repository of the
  workspace starts again at 12:00).
- **Labels** in drawing order (detached `HEAD`, the current branch with ✓, other local branches, remote
  branches, tags, stashes), each group in `for-each-ref` order. Phase 6's scenarios were not measured in
  the running app: "drawn" says what phase 1's rule gives by phases 3 to 5's measured precedents, and
  the checks take it from `LabelsInRow`.
- **Lines** as phase 4's report writes them: "lane L (colour C) from row a's node into row b's node";
  **dashed** marks `GraphLine.IsDashed` (a stash's line).
- **Cells** of the dashboard: `text` (tone; tooltip). Tones: secondary, primary, warning
  (`VcWarningBrush`), danger (`VcDangerBrush`). "empty" is a cell with no text.
- `{root}` is a workspace copy's folder, `TempWorkspace.Root`: `%TEMP%\VisualCommit.Tests\workspace-<12
  hex>` (`TempDirectory`'s name: the label, `-`, the first 12 hex digits of a new GUID), different in
  every copy and on every machine; `{root}\<name>` a repository's saved folder
  (`TempWorkspace.PathOf`, Windows separators on Windows).

#### 1. Window geometry

Derived from the design and `tests/VisualCommit.Testing/WindowLayout.cs` (default panel widths, no
banner, no search bar, the activity log closed unless said). Logical pixels, window coordinates.

**The shell** (`WindowLayout`, unchanged): tab strip y 0 to 36, toolbar 36 to 88, main area from y 88,
status bar the last 26 (y 674 to 700 at 1100×700, 1054 to 1080 at 1920×1080). The neutral point (760,
687) lies in the status bar at 1100×700 and, at 1920×1080, in the graph area or, on a dashboard tab, in
the dashboard's list below its tenth row (rows end at y 412), where nothing has a tooltip. On a
dashboard tab at 1100×700 the status bar holds the workspace icon and name, the run's text (at most 520
wide), its bar and Cancel all and the waiting text: whether (760, 687) stays clear of them while a run
runs is for `ShellDriverCheckSupportTests` to confirm on a dashboard tab, which today it does only on a
repository tab.

**A repository's tab** (phase 5's scenario facts 1, unchanged): graph control at (260, 116), 440 × 558
at 1100×700 and 1260 × 938 at 1920×1080; display row d's centre **y 129 + 26 d**; lane L's centre **x
406 + 16 L**; Branch / Tag column x 260 to 390; Graph column 16 per lane + 16, at least 48. So at
1100×700:

| Lanes | Graph column | Message | Date | A row "in its Message column" |
|---|---|---|---|---|
| 1 or 2 (api, web-app, docs, design-system, infra, scratch, notes, mobile, billing, refspecs, settings, auth, history, reflog) | 48 | x 438, 142 wide | x 580 to 700 | x 468 |
| 3 (ignored, ops, graph) | 64 | x 454, 126 wide | x 580 to 700 | x 484 |
| 5 (conflicts) | 96 | x 486, 214 wide | none | x 516 |

At 1920×1080 the same x for Message, with Author x 1188 (140), Date 1328 (120), SHA 1448 (72). With
the working-changes row (design-system, billing) commit row r's centre is y 155 + 26 r and the working
row's y 129. billing also has the in-progress banner (`Merging: "Merge branch 'feature/tax'". 1 file
has conflicts.`, scenario facts 2.2): its rows start below the banner, which is at least 36 high and
may take two lines at 440 wide (D122: from the banner's bounds).

**The dashboard tab** (2.4 to 2.6; it spans the main area's three columns, x 0 to the window's width):

| | 1100×700 | 1920×1080 |
|---|---|---|
| Header `DashboardHeader` | y 88 to 124, `DockPanel` margin 8,0 (inside x 8 to 1092) | y 88 to 124 (inside x 8 to 1912) |
| Column headings `DashboardColumnHeaders` | y 124 to 152 | y 124 to 152 |
| List `DashboardList` | from y 152, 522 high: 20 rows of 26 | from y 152, 902 high: 34 rows |
| Dashboard row k | y 152 + 26 k to 178 + 26 k, centre **y 165 + 26 k**; the ten rows of "Web" centres 165, 191, 217, 243, 269, 295, 321, 347, 373, 399, the last ending at y 412 | the same |
| Activity log open (200 + 5) | list 152 to 469 (317: 12 rows), splitter 469 to 474, log 474 to 674 | list 152 to 849 (697: 26 rows), log 854 to 1054 |
| Repository | x 0 to 132 | x 0 to 132 |
| Folder | hidden (`rest` = 288 < 468) | x 132 to 780 (648) |
| Branch | x 132 to 282 | x 780 to 930 |
| Ahead / behind | x 282 to 412 | x 930 to 1060 |
| Changes | x 412 to 632 | x 1060 to 1280 |
| State | x 632 to 812 | x 1280 to 1460 |
| Operation | x 812 to 1100 (288) | x 1460 to 1920 (460) |

A cell's text starts 8 into its column and ends 8 before the next (room = column − 16): Operation's
room is 272 at 1100. These columns assume `DashboardColumns.Compute` gets the window's full width; the
checks take them from the cells' bounds. Header, right to left from x 1092 (1100) or 1912 (1920), 6
apart: `DashboardMore` 28 × 28, `DashboardRefresh`, `DashboardPullAll`, `DashboardFetchAll`,
`DashboardAddRepository` (the four about 460 together, 2.5's estimate); left: `IconWorkspace` 14 × 14
from x 8, the name from x 28, the count 10 after it; the run's text, bar (80 × 4) and `Cancel all`
right-aligned 16 before `DashboardAddRepository`.

**The settings dialog** (7.1): card x 120 to 980, y 40 to 660 (860 × 620) at 1100×700; x 530 to 1390,
y 200 to 880 (860 × 680) at 1920×1080. Inside (padding 20): the title `Settings` from the card's top
+ 20, about 21 high; 14 below it the body (about 493 high at 1100×700, 553 at 1920×1080): `SettingsNav`
176 wide (x 140 to 316 at 1100; 550 to 726 at 1920), 20, the section column 622 wide (x 336 to 958; 746
to 1368); the buttons row 30 high, 20 below the body, ending 20 above the card's bottom (y 610 to 640 at
1100×700, 830 to 860 at 1920×1080). Nav buttons 176 × 30, 2 apart. Form columns 132, 12, the rest (464
at 1100×700).

**The command palette** (5.3): card 600 wide, x 250 to 850 at 1100×700 and 660 to 1260 at 1920×1080,
top y 96, height 56 + 28 × max(1, rows), rows at most 12: 84 (`No matches` or one row) to 392 (y 96 to
488). Box `CommandPaletteBox` 582 × 32, x 259 to 841 (669 to 1251), y 105 to 137; list from y 143, row
k (28 high) centred at **y 157 + 28 k**.

**The welcome page's Workspaces section** (13.1): in the graph area (x 260 to 700 at 1100×700, 260 to
1520 at 1920×1080), the stack 392 wide (x 284 to 676) and 440 wide (x 670 to 1110); the section 68 high
with no workspace (8 + 16 + 20 + 8 + 16) and 52 + 26 N with N, between `Init` and `Recent
repositories`; the stack, centred vertically, 34 px higher than in phases 0 to 5's pictures of an empty
tab with no workspace.

**The tab strip** (13.3, 13.4): a repository tab's natural width clamp(51 + title, 120, 220), a
dashboard tab's clamp(69 + title, 120, 220); "+" 38 after the tabs; room A = width − 38 (962 at 1000,
1062 at 1100, 1882 at 1920). At 1100 with "Web" and its nine repositories that can open and one new
tab (11 tabs): six of 97 and five of 96; 12 tabs: six 89 and six 88; 13 tabs: nine 82 and four 81; from
14 tabs 80 each and scrolling (`Offset` 58 with the last active). Every natural width of the scenario is
at least 120 (`Web` 26.9 px → 120; `web-app` 53.2 → 120; `design-system` 87.9 → 139), more than every
cap, so which tabs fill the strip changes no width.

**The "Add repositories" dialog** (1.9): 460 wide at both sizes (418 inside), centred, about 400 high
with eight rows; `FoundList` rows 28 high, 8 shown (`MaxHeight` 232), so the nine of "Web" scroll by one.

#### 2. "workspace" (`Scenarios.WorkspaceAsync`, workspace "Web")

##### 2.1 The copy

`{root}` holds `api`, `billing`, `design-system`, `docs`, `home`, `infra`, `mobile`, `notes`,
`remotes`, `scratch`, `web-app`; `remotes/` holds `api.git`, `billing.git`, `design-system.git`,
`docs.git`, `infra.git`, `mobile.git`, `scratch.git`, `web-app.git` (bare); `home/` holds only an
empty `.gitconfig` (TempRepo's isolation home: a folder, no `.git` entry); `old-site` does not exist.
`TempWorkspace.Names` (and `ToWorkspace()`, "Web", id `web`) in the dashboard's order: api, web-app,
docs, mobile, design-system, billing, infra, scratch, notes, old-site. Every repository with a remote
has `remote.origin.url = ../remotes/<name>.git`, `remote.origin.fetch =
+refs/heads/*:refs/remotes/origin/*`, `branch.main.remote = origin`, `branch.main.merge =
refs/heads/main` and nothing else under `branch.` (billing's `feature/tax` and scratch's `experiment`
track nothing); notes has no `remote.` key (`git remote` prints nothing). Each `.git/config` has the
blank folder's `core.autocrlf=false`, `commit.gpgsign=false`, `gc.auto=0`, `maintenance.auto=false`,
`user.name=Test Author`, `user.email=author@example.com`. A copy holds no `hooks/*.sample`.

##### 2.2 The ten repositories

| Repository | Commits (minute, message, full SHA; files) | Working repository's refs | Remote's `main` | `status --porcelain=v2 --branch` (less `branch.oid`) |
|---|---|---|---|---|
| api | 12:00 Initial commit `e1d2947c77c56bc945a29150b7a8bc3b0845393c` (README.md `# api`, routes.txt `GET /`); 12:01 Add the health route `2248c9dd04d8a32c1aff7c14dfbbf832a1104d70` (routes.txt `GET /` `GET /health`) | main = origin/main = `2248c9d` | `2248c9d` | `# branch.head main`, `# branch.upstream origin/main`, `# branch.ab +0 -0` |
| web-app | 12:00 Initial commit `147c6e39738519f76b29747ef00e42a57903d718` (README.md, index.html `<h1>Home</h1>`); 12:01 Add the about page `0d6e6c54821ad03b4c1856172f30d66c82cb45e6` (about.html); 12:02 Style the pages `cbd9037ee561a55b59ae926291397798d07649b4` (style.css `h1 { color: teal; }`) | main `cbd9037`, origin/main `147c6e3` | `147c6e3` | `+2 -0` |
| docs | 12:00 Initial commit `ec855bae3aa27bbf3f546b0ec7934cca41d65c01` (README.md, guide.md `Guide`); 12:01 Write the install guide `245235b4bc9cde2f2136b0046248ec77dd9dfffe` (install.md); 12:02 Write the FAQ `43723b691dbb278618338f433fcb205b965b961f` (faq.md); 12:03 Fix the guide's typos `23e2ad1a7d93c6b71c2a4ea032fab8c136037672` (guide.md `Guide.`); 12:04 Add the changelog `245b72964a7ee4952ba789ed47201369ab4e2f50` (changelog.md; on the remote only) | main `ec855ba`, origin/main `23e2ad1` | `245b729` | `+0 -3` |
| mobile | 12:00 Initial commit `617d476ca326993202795ad542db9b97cd607b27` (README.md, app.txt `app`); 12:01 Add the login screen `a17bccbba4b8747b9f63c7b2a4526807331f804c`; 12:02 Add the settings screen `f419c6e88b913030d5f8bb4e9a279b067e47170e`; 12:03 Name the app `21b8aeb0563a099a1a0bae837707ac45e2ba3bc0` (parent 12:00; app.txt `Mobile app`); 12:04 Add the about screen `67a26608eb41dcde8cb5f32e12173c99dc741bd1` (parent 12:02; remote only) | main `21b8aeb`, origin/main `f419c6e` | `67a2660` | `+1 -2` |
| design-system | 12:00 Initial commit `98ba31338d5e916371f9f77a97fc11686deec5e9` (README.md, colours.txt `red`, fonts.txt `Inter`, spacing.txt `4`); remote only: 12:01 Tighten the spacing `ea656908d7f6f17248b176a85c321189ac477823` (`2`), 12:02 Loosen the spacing `ab4543784436e2e51f60e7bda197780bb496d08f` (`8`), 12:03 Even out the spacing `948f7d187454e2096a92ef2c5d7281ce72aa34f5` (`6`) | main = origin/main = `98ba313` | `948f7d1` | `+0 -0`, `1 M. … colours.txt`, `1 .D … fonts.txt`, `1 .M … spacing.txt`, `? icons.txt` |
| billing | 12:00 Initial commit `fb577bdb1bf5b4965bf0088f26748eda7f466a7a` (README.md, invoice.txt `total: 10`); 12:01 Add the tax `8236f530eac42c26fdf90b73daa64df549f264fd` (`total: 12`, on feature/tax); 12:02 Round the totals `0e943ed7edee1c1345a7a02b1b11ef89bac5c093` (`total: 10.00`, on main) | main = origin/main = `0e943ed`, feature/tax `8236f53` | `0e943ed` | `+0 -0`, `u UU … invoice.txt`; `MERGE_HEAD` = `8236f53…`; `MERGE_MSG` `Merge branch 'feature/tax'` / (empty) / `# Conflicts:` / `#\tinvoice.txt` |
| infra | 12:00 Initial commit `09d320a17f4690c9317493bf1d77ff7e3f7209c7` (README.md, deploy.sh `echo deploy`); 12:01 Deploy to staging first `229cea5552f11d6279c15a5497fd9ebb4e54fa6f` | main = origin/main = `229cea5`, tag v1.0 (lightweight) `09d320a`, HEAD detached at `09d320a` | `229cea5` | `# branch.head (detached)` only |
| scratch | 12:00 Initial commit `42a778beba5311150c8b7c392b54079db8ee24c1` (README.md); 12:01 Try an idea `ba189765ad74fcaff7fc9262f34e332cdfa8ceed` (idea.txt, on experiment) | main = origin/main = `42a778b`, experiment `ba18976`; HEAD `ref: refs/heads/experiment` | `42a778b` | `# branch.head experiment` only |
| notes | 12:00 Initial commit `bd355fa96270d3eebf738e2bde71fc45e4487ad8` (README.md, todo.txt `Buy milk`); 12:01 Add the reading list `937a43bda4c1286a93ea7ff081f40d54385d595a` (reading.txt) | main `937a43b`; no remote | — | `# branch.head main` only |
| old-site | none | — | — | the folder does not exist; the app never runs git there |

Every text file ends with a line feed. design-system's working tree: colours.txt `red` `blue` (staged:
index blob `836673f`), spacing.txt `4` `16` (unstaged), fonts.txt deleted (unstaged), icons.txt `home`
(untracked). billing's invoice.txt: `<<<<<<< HEAD` / `total: 10.00` / `=======` / `total: 12` /
`>>>>>>> feature/tax`. The app's `ReadStatusAsync` gives: design-system `Staged` = [colours.txt
Modified], `Unstaged` = [fonts.txt Deleted, **icons.txt Added**, spacing.txt Modified], `Untracked` =
{icons.txt}; billing `Unstaged` = [**invoice.txt Conflicted**], `Conflicts` = [invoice.txt
BothModified], `InProgress` Merge, `Commit` `8236f53…`, `Subject` `Merge branch 'feature/tax'` (see
section 8, item 2).

The app's refs read: api to scratch `Remotes` = [origin]; notes []. Upstreams: every `main` with a
remote `Upstream = origin/main`, `UpstreamRemote = origin`, `UpstreamRemoteRef = refs/heads/main`, so
`{up}` (`ToolbarViewModel.UpstreamText`) is `origin/main`. Ahead and behind: web-app 2/0, docs 0/3,
mobile 1/2, the others 0/0. infra `Head` detached at `09d320a`; scratch `Head` `experiment` with no
upstream; notes `main` with no upstream.

##### 2.3 The rows as the dashboard shows them (W2, design 2.5, 2.6)

Header: `Web` (13, SemiBold), `10 repositories`; no row selected; before the first run no run text, the
status bar `Ready`, its branch part `Web` after `IconWorkspace`. Toolbar (4.10): Fetch enabled,
`Fetch every repository of Web (Ctrl+Shift+F)`; Pull enabled, `Pull every repository of Web
(Ctrl+Shift+L)`; Push, Branch, Stash, Pop, Undo, Redo, Search disabled without a tooltip (design).

| k | Repository (tooltip: the folder) | Branch | Ahead / behind (automation name; tooltip) | Changes | State (tooltip) | Operation |
|---|---|---|---|---|---|---|
| 0 | `api` | `main` | `Up to date` (secondary; `main is level with origin/main`) | `Clean` (secondary) | empty | empty |
| 1 | `web-app` | `main` | ↑ `2` (`2 ahead`; `2 commits ahead of origin/main`) | `Clean` | empty | empty |
| 2 | `docs` | `main` | ↓ `3` (`3 behind`; `3 commits behind origin/main`) | `Clean` | empty | empty |
| 3 | `mobile` | `main` | ↑ `1` ↓ `2` (`1 ahead, 2 behind`; `1 commit ahead of and 2 behind origin/main`) | `Clean` | empty | empty |
| 4 | `design-system` | `main` | `Up to date` (`main is level with origin/main`) | `1 staged, 2 unstaged, 1 untracked` (primary) | empty | empty |
| 5 | `billing` | `main` | `Up to date` (`main is level with origin/main`) | `1 conflict` (danger) | `Merge in progress` (warning; `Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.`) | empty |
| 6 | `infra` | `Detached at 09d320a` | empty | `Clean` | empty | empty |
| 7 | `scratch` | `experiment` | `No upstream` (secondary; `experiment has no upstream`) | `Clean` | empty | empty |
| 8 | `notes` | `main` | `No remote` (secondary; `This repository has no remotes`) | `Clean` | empty | empty |
| 9 | `old-site` | empty | empty | empty | `Folder not found` (warning; `{root}\old-site does not exist`) | empty |

The arrows are `IconArrowUp` / `IconArrowDown` 10 × 10 in the secondary colour, each with its count, 6
apart, only above 0. At 1920×1080 the Folder cell holds `{root}\<name>` (secondary) for every row,
old-site's included. Every text fits its cell at 1100 and 1920 (2.6's measure; the tightest
`1 staged, 2 unstaged, 1 untracked` 199.5 of 204 and `Detached at 09d320a` 129.6 of 134). notes'
`No remote` assumes the dashboard tests "no remote" before "no upstream" (section 8, item 1).

**Row menus** (2.7; height by `WindowLayout.MenuHeight`):

- web-app: header `web-app` (tooltip `{root}\web-app`); `Open`; —; `Fetch`; `Pull`; —; `Show in
  folder`; `Copy path`; —; `Move up`; `Move down`; `Remove from workspace`. 8 items, 3 separators:
  267 high. All enabled (nothing runs, so no `Cancel {title}`; nothing kept, so no `Error details…`).
- old-site: header `old-site`; `Open` (disabled: `{root}\old-site does not exist`); —; `Fetch`, `Pull`
  (disabled, the same reason); —; `Show in folder` (disabled, the same); `Copy path`; —; `Move up`;
  `Move down` (disabled: `It is the last repository`); `Locate…`; `Remove from workspace`. 9 items, 3
  separators: 293 high.
- Reasons elsewhere: notes' `Fetch` `This repository has no remotes`, `Pull` `main has no upstream: set
  one first`; scratch's `Pull` `experiment has no upstream: set one first`; infra's `Pull` `HEAD is
  detached: check out a branch to pull`; billing's `Pull` `Finish or abort the merge first`; api's
  `Move up` `It is the first repository`.

##### 2.4 Each repository's graph in its own tab (W1's `Add open tabs`, W8, K10, L33)

As the app lays them out (`GraphLayout`), 1100×700 Message clicks at (468, 129 + 26 r) unless said.

| Repository | Rows: labels, node (lane, colour), message, date, SHA | Lines |
|---|---|---|
| api | 0 `main` ✓, `origin/main` (drawn `main` ✓, `+1`, by H0's precedent) (0, 0) Add the health route 12:01 `2248c9d`; 1 (0, 0) Initial commit 12:00 `e1d2947` | lane 0 rows 0 to 1 |
| web-app | 0 `main` ✓ (0, 0) Style the pages 12:02 `cbd9037`; 1 (0, 0) Add the about page 12:01 `0d6e6c5`; 2 `origin/main` (0, 0) Initial commit 12:00 `147c6e3` | lane 0 rows 0 to 2 |
| docs | 0 `origin/main` (0, 0) Fix the guide's typos 12:03 `23e2ad1`; 1 Write the FAQ `43723b6`; 2 Write the install guide `245235b`; 3 `main` ✓ Initial commit 12:00 `ec855ba` | lane 0 rows 0 to 3 |
| mobile | 0 `main` ✓ (0, 0) Name the app 12:03 `21b8aeb`; 1 `origin/main` (1, 1) Add the settings screen 12:02 `f419c6e`; 2 (1, 1) Add the login screen 12:01 `a17bccb`; 3 (0, 0; lane 1 ends here) Initial commit 12:00 `617d476` | lane 0 (colour 0) rows 0 to 3; lane 1 (colour 1) from row 1's node into row 2's node, then into row 3's node |
| design-system | working-changes row (display 0, ring lane 0, dashed line to row 0); 0 `main` ✓, `origin/main` (0, 0) Initial commit 12:00 `98ba313`, centre y 155 | one row |
| billing | working-changes row (display 0); the banner above; 0 `main` ✓, `origin/main` (0, 0) Round the totals 12:02 `0e943ed`; 1 `feature/tax` (1, 1) Add the tax 12:01 `8236f53`; 2 (0, 0; lane 1 ends here) Initial commit 12:00 `fb577bd` | lane 0 rows 0 to 2; lane 1 (colour 1) from row 1's node into row 2's node. Banner `Merging: "Merge branch 'feature/tax'". 1 file has conflicts.`, tooltip as 2.3's State tooltip |
| infra | 0 `main`, `origin/main` (both drawn, by RL0's `topic`, `origin/topic`) (0, 0) Deploy to staging first 12:01 `229cea5`; 1 `HEAD` (detached), `v1.0` (both drawn, by P1's precedent) (0, 0) Initial commit 12:00 `09d320a` | lane 0 rows 0 to 1; status bar `Detached at 09d320a` |
| scratch | 0 `experiment` ✓ (0, 0) Try an idea 12:01 `ba18976`; 1 `main`, `origin/main` (0, 0) Initial commit 12:00 `42a778b` | lane 0 rows 0 to 1 |
| notes | 0 `main` ✓ (0, 0) Add the reading list 12:01 `937a43b`; 1 (0, 0) Initial commit 12:00 `bd355fa` | lane 0 rows 0 to 1 |

No stash anywhere, no tag but infra's `v1.0`. Left panel: api, web-app, docs, mobile, design-system:
Local branches `1`, Remotes `1` (`origin` › `main`); billing Local branches `2` (folder `feature`
closed; `main`); infra Local `1`, Tags `1`; scratch Local `2` (`experiment`, `main`); notes Local `1`,
Remotes `0`. The reflogs hold only the build's entries (`fast-import`, and `reset: moving to HEAD` in
HEAD's log; infra's HEAD log has the `fast-import` entry alone, HEAD having been written as text).

##### 2.5 Fetch all (W4, W5, W6; design 4.2, 4.3, 4.8)

Decided at the start in row order (4.2): api, web-app, docs, mobile, design-system, billing, infra,
scratch in line (`Waiting to fetch…`); notes left out with `This repository has no remotes`
(secondary, the reason as tooltip); old-site left out, its State `Folder not found`, its Operation cell
nothing new. So `{n}` = 8, 2 skipped (log `Web: Fetch all: started for 8 repositories, 2 skipped`).
Each row runs the toolbar's Fetch: `FetchAsync(null)` reads `git remote` (`origin`, one remote, the
last) and runs **`git fetch --progress origin`** (no `--no-auto-maintenance`, D109).

| Row | git's standard error (2.36), lines holding `%` left out | Exit | Refs after | Ahead / behind after (automation; tooltip) | Operation after |
|---|---|---|---|---|---|
| api, web-app, billing, infra, scratch | nothing | 0 | unchanged | unchanged | `Fetched from all remotes` (secondary) |
| docs | `remote: Enumerating objects: 4, done.` / `remote: Total 3 (delta 1), reused 0 (delta 0), pack-reused 0` / `From ../remotes/docs` / `   23e2ad1..245b729  main       -> origin/main` | 0 | origin/main `245b729` | ↓ `4` (`4 behind`; `4 commits behind origin/main`) | `Fetched from all remotes` |
| mobile | the same with `From ../remotes/mobile` / `   f419c6e..67a2660  main       -> origin/main` | 0 | origin/main `67a2660` | ↑ `1` ↓ `3` (`1 ahead, 3 behind`; `1 commit ahead of and 3 behind origin/main`) | `Fetched from all remotes` |
| design-system | `remote: Enumerating objects: 11, done.` / `remote: Total 9 (delta 3), reused 0 (delta 0), pack-reused 0` / `From ../remotes/design-system` / `   98ba313..948f7d1  main       -> origin/main` | 0 | origin/main `948f7d1` | ↓ `3` (`3 behind`; `3 commits behind origin/main`) | `Fetched from all remotes` |

Progress lines (`remote: ` prefix, each ending in spaces and a CR on the file remote and over HTTP
alike): docs and mobile `Counting objects:  25% (1/4)`, `50% (2/4)`, `75% (3/4)`, `100% (4/4)`,
`100% (4/4), done.`, then `Compressing objects:  50% (1/2)`, `100% (2/2)`, `100% (2/2), done.`;
design-system `Counting objects:   9% (1/11)`, `18%`, `27%`, `36%`, `45% (5/11)`, `54%`, `63%`,
`72%`, `81%`, `90% (10/11)`, `100% (11/11)`, then `Compressing objects:  16% (1/6)`, `33%`, `50%`,
`66%`, `83%`, `100% (6/6)`. The three holds of W4 (25, 75, 45) are Counting percentages
(`Fetching from all remotes… 25%`, `… 75%`, `… 45%` in the rows, `CompactText` without a stage).
2.55: the same lines, `pack-reused 0 (from 0)` in the Total line; exits and refs identical.

Result: `Fetched 8 repositories; 2 skipped` (secondary); with docs, mobile and design-system held and
the other five done, `Fetching 8 repositories… 5 done`, `Percent` ⌊100 × 5 / 8⌋ = **62**, no waiting
count (four slots, three in use). The first four slots go to api, web-app, docs and mobile (row order);
design-system, billing, infra and scratch follow as slots free.

**W5 (Cancel all under `FetchHold.HoldIn` on the eight)**: in line api, web-app, docs, mobile running
(held, no percentage), design-system, billing, infra, scratch `Waiting to fetch…`, the status bar
`4 waiting`; Cancel all: the four in line `Fetch cancelled` at once, `Cancelling: waiting for 4
repositories…`, then the four running ones `Fetch cancelled` (killed in their cancel window while
`upload-pack` waits, before any pack: no lock file), result `Fetched no repositories; 8 cancelled; 2
skipped` (secondary), no dialog.

**Over the folder server (W4, W6; `Scenarios.ServeWorkspaceAsync`)**: port 47393 was free
(`PortWasTaken` false); `UrlOf("docs")` = `http://127.0.0.1:47393/docs.git`; each named `origin` is set
to its URL. Without sign-in the fetches print what the file remote prints, with `From
http://127.0.0.1:47393/docs` (`/mobile`, `/design-system`) in place of `From ../remotes/docs`. With
`RequireAuth`, git without a helper: `fatal: could not read Username for 'http://127.0.0.1:47393':
terminal prompts disabled`, 128, after one `GET /docs.git/info/refs?service=git-upload-pack` answered
401. With an askpass answering alice / s3cret, **docs and mobile ask the same two prompts**:
`Username for 'http://127.0.0.1:47393': ` and `Password for 'http://alice@127.0.0.1:47393': ` (so one
group key answers both, 4.4); requests per repository: `GET …/info/refs` (no user, 401), `GET
…/info/refs` (alice, 200), `POST …/git-upload-pack` `ls-refs` (alice, 200), `POST …/git-upload-pack`
`fetch` (alice, 200). The dialog's texts, phase 3's form with the port (`WithPort`): title `Sign in to
127.0.0.1:47393`, `Git needs a user name and a password or token for http://127.0.0.1:47393.`, the
repository line `In docs` (3.8).

**scratch's missing remote (W6)**: after `git remote set-url origin ../remotes/missing.git`, `git fetch
--progress origin` → `fatal: '../remotes/missing.git' does not appear to be a git repository` /
`fatal: Could not read from remote repository.` / (empty line) / `Please make sure you have the correct
access rights` / `and the repository exists.`, exit 128 (2.55 identical). `FetchAsync(null)` appends
`error: Could not fetch origin`, so `FetchAllFailureDisplay` gives `Fetch failed` / `Git could not fetch
from origin.`, `In scratch`, command `git fetch --progress origin`. W6's result: `Fetched 7
repositories; 1 failed; 2 skipped` (danger); design-system, fetched from its file remote, ↓ `3`.

##### 2.6 Pull all (W7; design 4.2, 4.8)

W7 sets `pull.ff = only` in mobile's `.git/config`. Decided at the start (4.2, `ToolbarViewModel.Reasons`
in its order: detached, no upstream, upstream gone, in progress, no remotes): api, web-app, docs,
mobile, design-system in line (`Waiting to pull…`); left out: billing `Finish or abort the merge
first`, infra `HEAD is detached: check out a branch to pull`, scratch `experiment has no upstream: set
one first`, notes **`main has no upstream: set one first`** (no upstream is tested before no remotes),
old-site (no repository, nothing new in Operation). `{n}` = 5, 5 skipped. Each row, in order: D124's
refs read; the pull configuration read (8.9, design: before the fetch); `git fetch --progress origin`;
the cancel window ends; HEAD read again; then inside `IntegratePullAsync` 9.10's read `git diff-tree -r
-z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --` (none of its paths is on disk in any
row, so no `check-ignore`) and git's merge with `--no-overwrite-ignore` just before `FETCH_HEAD`
(design; today's code passes no option yet).

| Row | 9.10's read | Integration (design) | git (2.36) | Exit | Outcome, Operation cell (tone) | After |
|---|---|---|---|---|---|---|
| api | nothing | `git merge --no-edit --no-overwrite-ignore FETCH_HEAD` | stdout `Already up to date.` | 0 | UpToDate: `main is already up to date` (secondary) | unchanged, `Up to date` |
| web-app | nothing | the same | `Already up to date.` | 0 | `main is already up to date` | unchanged, ↑ `2` |
| docs | `changelog.md`, `faq.md`, `install.md` | the same | stdout `Updating ec855ba..245b729` / `Fast-forward` / ` changelog.md \| 1 +` / ` faq.md       \| 1 +` / ` guide.md     \| 2 +-` / ` install.md   \| 1 +` / ` 4 files changed, 4 insertions(+), 1 deletion(-)` / ` create mode 100644 changelog.md` / ` create mode 100644 faq.md` / ` create mode 100644 install.md` | 0 | FastForward, 4: `Pulled 4 commits into main (fast-forward)` | main = origin/main = `245b729`, `Up to date` (`main is level with origin/main`) |
| mobile | `about.txt`, `login.txt`, `settings.txt` | `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` | stderr `fatal: Not possible to fast-forward, aborting.` | 128 | failed, NotFastForward: `Pull failed` (danger), `Details…` | origin/main `67a2660`, main unchanged: ↑ `1` ↓ `3` |
| design-system | nothing (only spacing.txt changes) | `git merge --no-edit --no-overwrite-ignore FETCH_HEAD` | stdout `Updating 98ba313..948f7d1`; stderr `error: Your local changes to the following files would be overwritten by merge:` / `\tspacing.txt` / `Please commit your changes or stash them before you merge.` / `Aborting` | 1 | failed, MergeBlockedByLocalChanges: `Pull failed`, `Details…` | origin/main `948f7d1`, main and the four changes unchanged: ↓ `3`, `1 staged, 2 unstaged, 1 untracked` |

2.55: identical but mobile's stderr, which has nine `hint:` lines before the `fatal:` line (`hint:
Diverging branches can't be fast-forwarded, you need to either:` / `hint:` / `hint: \tgit merge --no-ff` / `hint:` / `hint: or:` /
`hint:` / `hint: \tgit rebase` / `hint:` / `hint: Disable this message with "git config set
advice.diverging false"`), and the fetches' `pack-reused 0 (from 0)`. Result: **`Pulled 3 repositories;
2 failed; 5 skipped`** (danger). `Details…`: mobile `Pull failed` / `main and origin/main have
diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main.`
(design, `PullNotFastForward`), `In mobile`, git's output as above, command `git merge --ff-only
--no-overwrite-ignore FETCH_HEAD`; design-system `Pull failed` / `1 conflict prevents the merge. Commit,
stash, or discard your changes first.`, `In design-system`, git's output, command `git merge --no-edit
--no-overwrite-ignore FETCH_HEAD`. docs' pull is recorded for undo: opened later, its tab offers `Undo
pull into main`, after which main is `ec855ba` again and the row ↓ `4` (origin/main stays `245b729`).

##### 2.7 The scan of `{root}` (W1, L30; design 1.8, 1.9)

Breadth-first from `{root}` (depth 0): candidates at depth 1 with a `.git` folder: api, billing,
design-system, docs, infra, mobile, notes, scratch, web-app. `home` (no `.git`) and `remotes/` are
searched and give none: a bare `*.git` folder has no `.git` entry. old-site does not exist. Each
candidate opens at its top (`OpenedAtTopAsync` true). The checklist, ordered by relative path
(`OrdinalIgnoreCase`): **api, billing, design-system, docs, infra, mobile, notes, scratch, web-app**,
each row its name and the same relative path; with api already in "Web" (W1), api `Already in this
workspace` (checked, disabled), the other eight checked: `Found 9 repositories in {root}.`, `Add 8
repositories`. Added in the list's order, W1's dashboard rows become api (first, from `Add
repository…`), then billing, design-system, docs, infra, mobile, notes, scratch, web-app: nine rows,
alphabetical, not "Web"'s order of 2.3, and no old-site (section 8, item 3). Each reaches 2.3's state
for its repository; the header `9 repositories`.

#### 3. "refspecs" (`Scenarios.RenamingRefspecAsync`, folder and tab `refspecs`)

##### 3.1 The scenario and its graph

`refspecs` with bare `origin.git`, `fork.git`, `upstream.git` beside it (`remote.<r>.url` `../<r>.git`).
Commits: Initial commit 12:00 `cc31e4ae22a8e076d63050ef2e87c59113ae1f62` (README.md `# refspecs`); Add
the parser 12:01 `8394a5e2e769c4601161cbce8c239ff2aa246eb9` (parser.txt `parse`); Work on the topic
12:02 `de4a87cd5a84d5ed135863187487f81fe89d030c` (topic.txt `topic`, on `topic`). HEAD on a clean
`main` (`8394a5e`), tracking `origin/main` (`+0 -0`); `topic` tracks nothing.

`git config --null --get-regexp ^remote\.` (the refs read of 10.2), in this order: `remote.origin.url`
`../origin.git`; `remote.origin.fetch` `+refs/heads/*:refs/remotes/origin/*`; `remote.origin.fetch`
`+refs/pull/*/head:refs/remotes/origin/pr/*`; `remote.fork.url` `../fork.git`; `remote.fork.fetch`
`+refs/heads/*:refs/remotes/fork/up/*`; `remote.upstream.url` `../upstream.git`; `remote.upstream.fetch`
`+refs/pull/*/head:refs/remotes/upstream/pr/*`. `git remote`: `fork`, `origin`, `upstream`.

Remotes: `origin.git` feature/x `8394a5e`, main `8394a5e`, `refs/pull/1/head` `de4a87c`; `fork.git`
main `8394a5e`; `upstream.git` main `8394a5e`, `refs/pull/7/head` `de4a87c`. Tracking refs:
`fork/up/main`, `origin/feature/x`, `origin/main` at `8394a5e`; `origin/pr/1`, `upstream/pr/7` at
`de4a87c`.

**RS0, "refspecs" as built.** 1 lane, Message x 438, Date 580 to 700, clicks (468, 129 + 26 r).

| Row | Labels | Node | Message | Date | SHA | y |
|---|---|---|---|---|---|---|
| 0 | `topic`, `origin/pr/1`, `upstream/pr/7` | 0, 0 | Work on the topic | 12:02 | `de4a87c` | 129 |
| 1 | `main` ✓, `fork/up/main`, `origin/feature/x`, `origin/main` (drawn `main` ✓, `+3`, by H0's precedent) | 0, 0 | Add the parser | 12:01 | `8394a5e` | 155 |
| 2 | | 0, 0 | Initial commit | 12:00 | `cc31e4a` | 181 |

Lane 0 rows 0 to 2. Left panel: Local branches `2` (`main`, `topic`), Remotes `5` (the section counts
remote branches, part G's GD16): `fork` › `up` ›
`main`, `origin` › `feature` › `x`, `main`, `pr` › `1`, `upstream` › `pr` › `7` (`RemoteName` by the
mapping, 10.3; the same names here as phase 5's longest-prefix rule).

##### 3.2 The mappings (10.1's `SourceOf`, by hand) and git's own `--track`

`FetchRefspecs` is a stub on the phase branch (`NotImplementedException`) and `ReadRefsAsync` sets no
`Refspecs`, so today's `RepoRefs.SourceOf` falls back to phase 5's assumption (fork/up/main "Mapped
fork refs/heads/up/main", origin/pr/1 "refs/heads/pr/1", upstream/pr/7 "refs/heads/pr/7"): G25 needs
WP-RA's reader. By 10.1's rules over the lines above:

| Tracking ref `T` | Candidates (remote: source) | `SourceOf` | `LocalNameOf` (10.6) | `git branch --track probe refs/remotes/<T>` (2.36 and 2.55 alike) |
|---|---|---|---|---|
| `fork/up/main` | fork: `refs/heads/main` | Mapped, fork, `refs/heads/main`, a branch `main`, renamed | `main` | `branch 'probe' set up to track 'fork/main'.`, exit 0; `remote = fork`, `merge = refs/heads/main` |
| `origin/pr/1` | origin: `refs/heads/pr/1` (default line), `refs/pull/1/head` (PR line) | **AmbiguousRefs** (R2), origin | `pr/1` (D201) | `branch 'probe' set up to track 'origin/pr/1'.`, 0; `origin`, `refs/heads/pr/1` (git's first candidate) |
| `upstream/pr/7` | upstream: `refs/pull/7/head` | Mapped, upstream, `refs/pull/7/head`, **not a branch** (R4) | `pr/7` | `branch 'probe' set up to track 'upstream/refs/pull/7/head'.`, 0; `upstream`, `refs/pull/7/head` |
| `origin/feature/x` | origin: `refs/heads/feature/x` (the PR line's destination `refs/remotes/origin/pr/` does not start it) | Mapped, identity | `feature/x` | `branch 'probe' set up to track 'origin/feature/x'.`, 0 |
| `origin/main` | origin: `refs/heads/main` | Mapped, identity | `main` | `… track 'origin/main'.`, 0 |

No line of another remote matches any `T`, so none is AmbiguousRemotes; none is NotFetched. Texts
(10.5): R2 `origin/pr/1 is fetched from more than one ref of origin`; R4 `upstream/pr/7 is
refs/pull/7/head on upstream, not a branch`; `DestinationsOf(origin, refs/heads/pr/1)` =
[`refs/remotes/origin/pr/1`], whose `SourceOf` is AmbiguousRefs and no Mapped destination is held, so
the push guard refuses a push of `pr/1` to origin's `refs/heads/pr/1`.

##### 3.3 G25's operations (each on a fresh copy; 2.36, replayed on 2.55)

| Step | Command (design 10.4) | git | Exit | After |
|---|---|---|---|---|
| `topic` dropped onto `fork/up/main` ("Push topic to fork/up/main") | `git push --progress fork refs/heads/topic:refs/heads/main` | stderr: `Enumerating objects: 4, done.`, Counting/Compressing/Writing progress, `Total 3 (delta 0), reused 0 (delta 0), pack-reused 0`, `To ../fork.git`, `   8394a5e..de4a87c  topic -> main` (2.55: `pack-reused 0 (from 0)`) | 0 | `fork.git` main `de4a87c`, no `up/main` there; **git moves `refs/remotes/fork/up/main` to `de4a87c` itself** (the push updates the tracking ref through the fetch line): graph row 0 `topic`, `fork/up/main`, `origin/pr/1`, `upstream/pr/7`; row 1 `main` ✓, `origin/feature/x`, `origin/main`. Result (design) `Pushed topic to fork/up/main` |
| phase 5's form, for contrast | `git push --progress fork refs/heads/topic:refs/heads/up/main` | `To ../fork.git` / ` * [new branch]      topic -> up/main` | 0 | a wrong `up/main` on fork and a local `refs/remotes/fork/up/up/main` |
| `Check out origin/pr/1` (R2, D201) | `git switch --create pr/1 --no-track --no-overwrite-ignore refs/remotes/origin/pr/1` | stderr `Switched to a new branch 'pr/1'` | 0 | HEAD `pr/1` at `de4a87c`; no `branch.pr/1.*` key (`git config --get-regexp ^branch\.pr/1\.` exit 1); status `# branch.head pr/1`, no upstream lines. Result (design) `Created pr/1 at origin/pr/1 without an upstream: origin/pr/1 is fetched from more than one ref of origin`. Graph row 0 `pr/1` ✓, `topic`, `origin/pr/1`, `upstream/pr/7`; row 1 `main`, `fork/up/main`, `origin/feature/x`, `origin/main` |
| `main` checked out again, then `origin/pr/1` double-clicked | `git switch --no-guess --no-overwrite-ignore main`, then `git switch --no-guess --no-overwrite-ignore pr/1` (the local branch of today's name exists) | stdout `Your branch is up to date with 'origin/main'.`, stderr `Switched to branch 'main'`; then `Switched to branch 'pr/1'` | 0, 0 | HEAD `pr/1` |
| Push of `pr/1` to origin (refused by the app before git, 10.4's guard) | none | — | — | `origin.git` without `refs/heads/pr/1`. What git would do: `git push --progress origin refs/heads/pr/1:refs/heads/pr/1` → `Total 0 (delta 0), reused 0 (delta 0), pack-reused 0` / `To ../origin.git` / ` * [new branch]      pr/1 -> pr/1`, 0; then every `git fetch --progress origin` → `fatal: Cannot fetch both refs/heads/pr/1 and refs/pull/1/head to refs/remotes/origin/pr/1`, 128 (2.55 identical) |
| `Check out upstream/pr/7` (R4: checkout stays enabled) | `git switch --create pr/7 --track --no-overwrite-ignore refs/remotes/upstream/pr/7` | stdout `branch 'pr/7' set up to track 'upstream/refs/pull/7/head'.`, stderr `Switched to a new branch 'pr/7'` | 0 | `branch.pr/7.remote upstream`, `branch.pr/7.merge refs/pull/7/head`; git's status `# branch.upstream upstream/pr/7`, `# branch.ab +0 -0`; the app's refs read `Upstream = upstream/pr/7`, `UpstreamRemoteRef = refs/pull/7/head`, so `UpstreamText` = **`upstream/refs/pull/7/head`** (section 8, item 5) |
| `Delete main on fork…` from `fork/up/main` (10.4 #9) | `git push --progress --force-with-lease=refs/heads/main:8394a5e2e769c4601161cbce8c239ff2aa246eb9 fork --delete refs/heads/main` | stderr `To ../fork.git` / ` - [deleted]         main` | 0 | `fork.git` has no ref; `refs/remotes/fork/up/main` deleted with it; fork's remote row has no branch. Backup (design) `remote-branch-<t>/fork/main` |
| phase 5's deletion, for contrast | `… --force-with-lease=refs/heads/up/main:8394a5e… fork --delete refs/heads/up/main` | ` ! [rejected]        (delete) -> up/main (stale info)` / `error: failed to push some refs to '../fork.git'` | 1 | nothing changed |

Push progress lines carry machine-dependent words (`Delta compression using up to 8 threads`, a
`KiB/s` rate in `Writing objects: 100% (3/3), 314 bytes | … KiB/s, done.`): not to be asserted.

#### 4. "ignored" (`Scenarios.IgnoredAsync`, folder and tab `ignored`)

##### 4.1 The scenario and its graph

`ignored` with bare `origin.git` beside it (`../origin.git`). `.gitignore` `secret.env` / `build/`.
Commits: Initial commit 12:00 `2e4599b4149a2686ea72c962920d8ba1764446d2` (README.md `# Ignored`,
app.txt `app`, .gitignore); Track the secret 12:01 `1ac759d23fd78a78f7660d289a1373ea85378b69`
(secret.env `old`, `-f`); Stop tracking the secret 12:02 `39bd8031f98ec9a1a6d02519ffabd35ca693bc63`
(`git rm --cached`); Add the vendor secret 12:03 `133a796ef1cfa3b4f2dc3237b5342751f85fcc3f` (on vendor;
secret.env `vendor`); Add the build file 12:04 `c8446bb6a7efb5eb9cc91d4a95e2bd37c0be7ced` (on tools; a
file `build`, content `build`); Main work 12:05 `c49cac40cc05ce7ebe709555621751082c868662` (app.txt
`app` `main`). Stash `stash@{0}` "On main: Stash the secret" 12:06
`c8795683e3b86ad70873326b9897659e3c5140dd`, parents `c49cac4` and the index commit
`ea37eb6d131f2c233f9b69403441926176582a5b` ("index on main: c49cac4 Main work"), no third parent; it
adds secret.env `stashed` (staged).

Trees: main and main~1 `.gitignore`, `README.md`, `app.txt`; main~2 (Track the secret) the same and
`secret.env`; vendor `.gitignore`, `README.md`, `app.txt`, `secret.env`; tools `.gitignore`,
`README.md`, `app.txt`, `build`. Refs: main `c49cac4` tracking origin/main (0, 0), vendor `133a796`
tracking origin/vendor (0, 0), tools `c8446bb` (no upstream); origin/main `c49cac4`, origin/vendor and
origin/vendor-remote `133a796`; no `refs/heads/vendor-remote`. `origin.git`: main, vendor,
vendor-remote. Working tree: clean, level main; `secret.env` `LOCAL`, `build/out.bin` `binary`;
`status --porcelain=v2 --branch --ignored`: `# branch.head main`, `# branch.upstream origin/main`,
`# branch.ab +0 -0`, `! build/`, `! secret.env`. The app's status: nothing staged, unstaged or
untracked (no working-changes row).

**IG0, "ignored" as built.** 3 lanes, Graph 64, Message x 454 (126), Date 580 to 700, clicks (484,
129 + 26 r).

| Row | Labels | Node | Message | Date | SHA | y |
|---|---|---|---|---|---|---|
| 0 | `stash@{0}` | 0, 0 (ring) | On main: Stash the secret | 12:06 | `c879568` | 129 |
| 1 | `main` ✓, `origin/main` (drawn `main` ✓, `+1`) | 0, 0 | Main work | 12:05 | `c49cac4` | 155 |
| 2 | `tools` | 1, 1 | Add the build file | 12:04 | `c8446bb` | 181 |
| 3 | `vendor`, `origin/vendor`, `origin/vendor-remote` (drawn `vendor`, `+2`) | 2, 2 | Add the vendor secret | 12:03 | `133a796` | 207 |
| 4 | | 0, 0 (lanes 1 and 2 end here) | Stop tracking the secret | 12:02 | `39bd803` | 233 |
| 5 | | 0, 0 | Track the secret | 12:01 | `1ac759d` | 259 |
| 6 | | 0, 0 | Initial commit | 12:00 | `2e4599b` | 285 |

Lines: lane 0 (colour 0) **dashed** from row 0's ring into row 1's node, then solid from row 1 to row 6;
lane 1 (colour 1) from row 2's node through row 3 into row 4's node; lane 2 (colour 2) from row 3's node
into row 4's node. Left panel: Local branches `3` (`main`, `tools`, `vendor`), Remotes `3` (`origin` ›
`main`, `vendor`, `vendor-remote`), Tags `0`, Stashes `1` (`On main: Stash the secret`). `{y}` of every
remote branch is its own name (identity mappings).

##### 4.2 Checkouts (G22, S21; 9.11): each refused by git with `--no-overwrite-ignore`, nothing changed

After each refusal: HEAD `main` `c49cac4`, heads `main`, `tools`, `vendor` only, `secret.env` `LOCAL`,
`build/out.bin` `binary`, status `!! build/`, `!! secret.env`. Then the app's `git check-ignore --stdin
-z` (without `GIT_LITERAL_PATHSPECS`) on the blocked paths. Title `Checkout blocked`, no "Stash changes
and check out".

| Check's step | Command | git's stderr (2.36; 2.55 identical) | Exit | `check-ignore` input → output | Summary (D179, 9.15) |
|---|---|---|---|---|---|
| `Check out vendor` | `git switch --no-guess --no-overwrite-ignore vendor` | `error: The following untracked working tree files would be overwritten by checkout:` / `\tsecret.env` / `Please move or remove them before you switch branches.` / `Aborting` | 1 | `secret.env\0` → `secret.env\0`, 0 | `1 ignored file is in the way: vendor tracks a file of the same name. Move it away first.` |
| `Check out origin/vendor` (D93: the local vendor) | the same command | the same | 1 | the same | `1 ignored file is in the way: origin/vendor tracks a file of the same name. Move it away first.` |
| `Check out origin/vendor-remote` (tracking) | `git switch --create vendor-remote --track --no-overwrite-ignore refs/remotes/origin/vendor-remote` | the same block | 1 | the same | `… origin/vendor-remote tracks a file of the same name. …`; no `refs/heads/vendor-remote`, no undo entry |
| `Create branch…` at vendor, box ticked | `git switch --create <name> --no-overwrite-ignore 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f` | the same block | 1 | the same | `… vendor tracks a file of the same name. …` (the start as the dialog names it); no branch made |
| `Check out tools` | `git switch --no-guess --no-overwrite-ignore tools` | `error: Updating the following directories would lose untracked files in them:` / `\tbuild` / (empty line) / `Aborting` | 1 | `build/out.bin\0` → `build/out.bin\0`, 0 (the folder by its files) | `1 ignored file is in the way: tools tracks a file of the same name. Move it away first.` (git's count: one folder) |
| S21, Keep ignored files **off**: "Check out this commit" on Track the secret (row 5; `git merge-base --is-ancestor 1ac759d… HEAD` exit 0, so D179's automatic stash path, which keeps its option whatever the setting) | no stash (the status has no changes: ignored files are none), then `git switch --detach --no-overwrite-ignore 1ac759d23fd78a78f7660d289a1373ea85378b69` | the untracked block naming `secret.env` | 1 | `secret.env\0` → `secret.env\0` | `1 ignored file is in the way: 1ac759d tracks a file of the same name. Move it away first.` |

##### 4.3 The app's checks before git (G23; 9.4 to 9.8), Keep ignored files on

Each with `GIT_OPTIONAL_LOCKS=0`; the outputs NUL-separated (`\0` here). No git write follows: no
`git merge`, `git rebase`, `git cherry-pick`, `git revert`, `git stash apply` or `pop`, no backup ref, no undo
entry; `ExactStateAsync` unchanged.

| Operation (title) | Reads and their output | Summary (9.15) |
|---|---|---|
| Merge vendor into main (`Merge failed`) | `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD refs/heads/vendor --` → `secret.env\0`; `git check-ignore --stdin -z` (`secret.env\0`) → `secret.env\0`, 0; `git merge-base --all HEAD refs/heads/vendor` → `39bd8031f98ec9a1a6d02519ffabd35ca693bc63`; `git diff-tree -r -z --no-renames --name-only --diff-filter=AMT 39bd803… refs/heads/vendor --` → `secret.env\0` (kept: the merge writes it) | `1 ignored file is in the way of the merge: secret.env. Move it away first.` |
| Rebase main onto vendor (`Rebase failed`; onto = vendor's SHA) | side by side: `git merge-base --is-ancestor 133a796… HEAD` → exit 1; `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD 133a796… --` → `secret.env\0`; `git log --no-renames --name-only -z --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT 133a796…..HEAD --` → `\x1ec49cac40cc05ce7ebe709555621751082c868662\0\napp.txt\0`; `git rev-list --min-parents=2 --max-count=1 133a796…..HEAD --` → nothing; then `check-ignore` (`app.txt\0secret.env\0`) → `secret.env\0` (app.txt is tracked) | `1 ignored file is in the way of the rebase: secret.env. Move it away first.` |
| Cherry-pick "Add the vendor secret" (`Cherry-pick failed`) | `git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT` fed `133a796…\n` → `secret.env\0`; `check-ignore` → `secret.env\0` | `1 ignored file is in the way of the cherry-pick: secret.env. Move it away first.` |
| Revert "Stop tracking the secret" (`Revert failed`) | the same with `-R`, fed `39bd803…\n` → `secret.env\0` (its deletion reversed); `check-ignore` → `secret.env\0` | `1 ignored file is in the way of the revert: secret.env. Move it away first.` |
| Apply, and pop, `stash@{0}` (`Applying the stash failed`) | D179's count first: `ReadStashPathsAsync`: `git diff-tree -r -z --no-renames --name-status c879568…^1 c879568…` → `A\0secret.env\0`; `git diff-tree -r -z --no-renames --name-only c879568…^1 c879568…^2` → `secret.env\0`; `git rev-parse --verify --quiet c879568…^3` → exit 1 (no untracked part); a status read (nothing local): count 0; then 9.8 (no process before it): `check-ignore` (`secret.env\0`) → `secret.env\0` | `1 ignored file is in the way of applying the stash: secret.env. Move it away first.` |

Call counts here: the merge 4 (the `--diff-filter=A` read, `check-ignore`, and, since something is in
the way, 9.4's `merge-base --all` and one base's read); the rebase 5 (four reads, `check-ignore`; no
merge in the range); the pick and the revert 2; the stash 1 after D179's count (its three stash reads
and a status read).

##### 4.4 Without the app's check (G24, S21; Keep ignored files off), with D72's dates

| Operation | Command | git (2.36; 2.55 identical) | Exit | After |
|---|---|---|---|---|
| Merge vendor (G24) | `git merge --no-edit --no-log -m "Merge branch 'vendor'" refs/heads/vendor` | stdout `Merge made by the 'ort' strategy.` / ` secret.env \| 1 +` / ` 1 file changed, 1 insertion(+)` / ` create mode 100644 secret.env` | 0 | main = merge `63b8fe1713a49735d84bb334cf1b86c66cef8cdb` (parents `c49cac4`, `133a796`; Test Author, 2026-01-02 09:00 author and committer; the same SHA on 2.55); **`secret.env` = `vendor`** (LOCAL lost); status `!! build/` only |
| Check out vendor (G24, S21 off) | `git switch --no-guess vendor` | stdout `Your branch is up to date with 'origin/vendor'.`, stderr `Switched to branch 'vendor'` | 0 | HEAD vendor `133a796`, `secret.env` = `vendor`, status `!! build/` |
| Rebase main onto vendor (informative) | `git rebase 133a796…` | `Rebasing (1/1)` / `ESC[KSuccessfully rebased and updated refs/heads/main.` | 0 | main `7c650738560a30b01be67d32a7215417211ecbb0`, `secret.env` = `vendor` |
| Cherry-pick Add the vendor secret (informative) | `git cherry-pick 133a796…` | `[main e795a48] Add the vendor secret` / ` Date: Thu Jan 1 12:03:00 2026 +0000` / … | 0 | main `e795a483147011279018665fea2d869e70366e96`, `secret.env` = `vendor` |
| Revert Stop tracking the secret (informative) | `git revert --no-edit 39bd803…` | `[main b12d479] Revert "Stop tracking the secret"` / … | 0 | main `b12d47943f9ea42b5fc8424c52d70ff832dcc4a8`, `secret.env` = `old` |
| Stash apply / pop (informative) | `git stash apply --index stash@{0}` / `pop --index` | `Changes to be committed:` … `new file:   secret.env` (pop adds `Dropped stash@{0} (c8795683e3b86ad70873326b9897659e3c5140dd)`) | 0 | `secret.env` = `stashed`, status `A  secret.env`, `!! build/` |

#### 5. "settings" (`Scenarios.GitSettingsAsync`, folder and tab `settings`)

`settings` and its bare `remotes/settings.git` (`../remotes/settings.git`), the lean way; no `user.*`
in `.git/config` (the global file is the session's). Commits: Initial commit 12:00
`9f9a01440ea7e170d173fb8878837ec545ec8b7c` (README.md `# Settings`, notes.txt `one` `two`, run.py
`print("run")`); Remote work 12:01 `e0cbfafef6677bc3e79b2a79650eea4c1675c3e0` (remote.txt; the remote's
main and origin/main); Local work 12:02 `73f8cadf9ea2ae5e574a3e047d1a8fc4af933b98` (local.txt; main,
parent 12:00). main tracks origin/main, `+1 -1`, clean; working tree local.txt, notes.txt, README.md,
run.py.

**ST0.** 2 lanes, Message x 438, clicks (468, 129 + 26 r): 0 `main` ✓ (0, 0) Local work 12:02 `73f8cad`;
1 `origin/main` (1, 1) Remote work 12:01 `e0cbfaf`; 2 (0, 0; lane 1 ends here) Initial commit 12:00
`9f9a014`. Lane 0 rows 0 to 2; lane 1 (colour 1) from row 1's node into row 2's node.

Identity (2.36 and 2.55 alike): with nothing set, `git var GIT_AUTHOR_IDENT` ends `fatal: unable to
auto-detect email address (got 'rpkha@Rahim-Surface.(none)')` (the machine's names), 128; with
`user.useConfigOnly = true` `fatal: no email was given and auto-detection is disabled`; with an email
only `fatal: no name was given and auto-detection is disabled`; a commit refuses with the same block,
128. Pull modes: global `pull.ff = only` → `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` →
`fatal: Not possible to fast-forward, aborting.`, 128 (nothing to fetch: origin/main is the remote's
main); `pull.rebase = true` and `pull.ff = true` in `.git/config` → `git merge-base --is-ancestor HEAD
FETCH_HEAD` exit 1, so `git rebase` → `Rebasing (1/1)` / `Successfully rebased and updated
refs/heads/main.`, main `b4253bb077f7b07e212ac8f16861a728072f58b5` (Local work, author 12:02, committer
2026-01-02 09:00) **when the committer is Test Author <author@example.com>**; with S19's own identity
the SHA follows that identity.

#### 6. The reused scenarios (SHAs equal the earlier reports')

As the app lays them out today; their facts are the earlier reports' (phase 3 for "ops" and "auth",
phase 4 for "conflicts", phase 1 for "graph", phase 5 for "history" and "reflog", whose H0 and RL0 this
run reproduced row for row, SHAs, lanes, colours and labels).

- **"ops"** (OPS0; phase 3): HEAD on clean `main` tracking origin/main (0, 0); stash `stash@{0}` "On
  main: Experiment". 8 rows, 3 lanes (Message x 454): 0 `stash@{0}` (0, 0 ring) On main: Experiment
  12:07 `0e95aca`; 1 `feature/conflict` (1, 1) Conflicting change 12:06 `078ef05`; 2 `feature/ff` (2, 2)
  Fast-forward me 12:05 `d93814d`; 3 `main` ✓, `origin/main`, `v1.1` (0, 0) Main work 12:04 `88d6b91`
  (lane 2 ends here); 4 `feature/alpha`, `origin/feature/alpha` (2, 3) Alpha two 12:03 `ce5ea6a`; 5 (2, 3)
  Alpha one 12:02 `dff8fbf`; 6 `v1.0` (0, 0; lanes 1 and 2 end here) Add core 12:01 `8beca34`; 7 (0, 0)
  Initial commit 12:00 `4ef1f7c`. Lane 0 dashed from row 0 through rows 1 and 2 into row 3's node.
  K10's `Check out feature/alpha`: `git switch --no-guess --no-overwrite-ignore feature/alpha` → stdout
  `Your branch is up to date with 'origin/feature/alpha'.`, stderr `Switched to branch
  'feature/alpha'`, 0. `Delete main…` on `main`: disabled `You cannot delete the branch you are on`.
- **"auth"** (phase 3): one row, `main` ✓, `origin/main` Initial commit 12:00 `e4a56bd`; the served
  `remote.git`'s main one commit further (Remote work `e6761c7`); `origin` = the server's `Url`
  (L31's on port 47394).
- **"conflicts"** (phase 4): HEAD on clean `main` `da31c97` (no remote). 10 rows, 5 lanes (Message x
  486, no Date at 1100): 0 `main` ✓ Ours edits `da31c97` (0, 0); 1 `feature/kinds` Kinds edits `3a9de3e`
  (1, 1); 2 `feature/picks` Pick last `5c4c257` (2, 2); 3 Pick conflicting `01fd976` (2, 2); 4 Pick clean
  `01d2046` (2, 2); 5 `feature/rebase` Second change `305f7f7` (3, 3); 6 First change `70e8c78` (3, 3); 7
  `feature/theirs` Theirs edits `5c77ffb` (4, 4); 8 Base `195768b` (0, 0; lanes 1 to 4 end here); 9
  Initial commit `6bd579f`.
- **"graph"** (phase 1; S15): 13 rows, 3 lanes: 0 `stash@{0}` On main: Work in progress on README
  `95daa96` (ring, dashed into row 2); 1 `feature/search` Highlight matches `f463b85` (1, 1); 2 `main` ✓
  Add settings page `47e6ec7`; 3 `origin/main` Fix typo in docs `974bd83` (2, 2); 4 `v0.2` Bump version
  `2775228`; 5 `bugfix/crash-on-start` Fix crash on start `72267a3` (2, 3); 6 `origin/feature/search`
  Add search box `6aab2e4` (1, 1); 7 Merge branch 'feature/login' `f7919b8` (second parent lane 1,
  colour 4); 8 Update README `10bcd42`; 9 `feature/login` Validate passwords `4a62ef9` (1, 4); 10 Add
  login form `7b230d3` (1, 4); 11 `v0.1` Add app skeleton `933250d`; 12 Initial commit `53347b3`. main
  ↑1 ↓1, feature/search ↑1. Clean.
- **"history"** (L28): H0 exactly (phase 5's scenario facts 2), ` M src/Reader.cs`.
- **"reflog"** (L29): RL0 exactly (phase 5's scenario facts 4), clean.

None of the reused scenarios has an ignored file but "protect" (not reused by phase 6's checks; its
P24 to P27 change on purpose, 16.1).

#### 7. Texts the facts above rely on, as the code has them today

`OperationTexts.FetchedAll` `Fetched from all remotes`; `PullUpToDate` `{b} is already up to date`;
`PulledFastForward` `Pulled {N} commits into {b} (fast-forward)` (`1 commit`); `FetchFailed` `Git could
not fetch from {remotes}.`; `ToolbarViewModel.PullDetachedText` `HEAD is detached: check out a branch
to pull`; `NoRemotesText` `This repository has no remotes`; Reasons' `{b} has no upstream: set one
first`; `FinishOrAbortFirst(Merge)` `Finish or abort the merge first`; `CannotDeleteCurrentBranch` `You
cannot delete the branch you are on`; `ConflictBannerViewModel.TextFor` / `ToolTipFor` as in 2.3 and
2.4. Everything marked "(design)" and the dashboard's, run's, palette's and settings' texts are design
14's, not in the code yet.

#### 8. Surprises: git behaviour or facts the design does not foresee

1. **notes is both "no upstream" and "no remote".** 2.6's Ahead / behind table lists `No upstream`
   before `No remote`; read in order it gives notes `No upstream`, while W2 expects `No remote`. The
   dashboard must test "no remote" first (a report decision). Pull all and the row menu's Pull use the
   toolbar's `Reasons`, which test the upstream first: notes is left out of Pull all with `main has no
   upstream: set one first`, not `This repository has no remotes` (4.2's order agrees; W7's "4.2's
   reasons" must spell it), so the row's Ahead / behind cell and its Pull reason name different things.
2. **`WorkingTreeStatus.Unstaged` holds untracked and conflicted files.** design-system's `Unstaged` is
   [fonts.txt Deleted, icons.txt **Added**, spacing.txt Modified] beside `Untracked` {icons.txt};
   billing's is [invoice.txt **Conflicted**] beside one `Conflicts` entry. Counting `Unstaged` as it is
   would show `1 staged, 3 unstaged, 1 untracked` and `1 conflict, 1 unstaged`; 2.6's `1 staged, 2
   unstaged, 1 untracked` and `1 conflict` need `DashboardTexts` to leave untracked and conflicted
   entries out of the unstaged count. `DashboardTextsTests` should pin both.
3. **W1's scan appends in the checklist's order**: the nine rows become api (from `Add repository…`),
   billing, design-system, docs, infra, mobile, notes, scratch, web-app, alphabetical, not "Web"'s
   order (api, web-app, docs, …) and without old-site; W1's pictures and `workspaces.json` follow that
   order.
4. **Fetch all changes design-system's row too**: level before, ↓ `3` after (`3 commits behind
   origin/main`), beside docs' ↓ `4` and mobile's ↑ `1` ↓ `3` that W4 names; Pull all leaves mobile ↑ `1`
   ↓ `3` and design-system ↓ `3` (each pull fetched before it failed) and docs `Up to date`.
5. **An upstream that is not a branch is named with its full ref.** After `Check out upstream/pr/7`,
   git's status names the upstream `upstream/pr/7`, but the app's `UpstreamText` (from
   `UpstreamRemoteRef = refs/pull/7/head`) gives `upstream/refs/pull/7/head`: the toolbar's Pull tooltip,
   the push dialog and a dashboard row's tooltips would say `upstream/refs/pull/7/head` (as git's own
   `--track` message does). Settled by the lead's ruling 3 (GD15): the app names an upstream by its
   remote-tracking ref when the fetch lines map it to one, `upstream/pr/7` here, as git's status
   does; G25 step 12 asserts it in Pull's tooltip.
6. **The drop's push moves the renamed tracking ref at once.** After `git push fork
   refs/heads/topic:refs/heads/main`, git updates `refs/remotes/fork/up/main` to `de4a87c` itself (no
   fetch needed): G25's graph after the drop has `fork/up/main` on Work on the topic (row 0), beside
   `topic`, `origin/pr/1` and `upstream/pr/7`, and main's row loses that label.
7. **`RepoRefs.Refspecs` is null on the phase branch** (`FetchRefspecs` is WP0's stub): until WP-RA's
   reader is merged, `SourceOf` is phase 5's assumption, under which fork/up/main maps to fork's
   `refs/heads/up/main` (the wrong push of section 3.3), origin/pr/1 to `refs/heads/pr/1` (Mapped, no R2)
   and upstream/pr/7 to `refs/heads/pr/7` (no R4). G25's expectations hold only with the reader.
8. **Visual Studio's Git 2.55 needs its own `mingw64/bin` first on `PATH`.** Run as
   `<VS>/…/mingw64/bin/git.exe` with Git 2.36 first on `PATH`, every fetch failed: its connectivity
   check started 2.36's `git rev-list` with options 2.36 lacks (`usage: git rev-list …`, then `error:
   ../remotes/docs.git did not send all necessary objects`, exit 1), after writing the objects. With its
   folder first on `PATH` (0.2's set-up) every result above came out. A harness that points the app at
   2.55's `git.exe` alone would hit the same (risk 28's family).
9. **design-system's fast-forward refusal prints `Updating …` on standard output and the block on
   standard error** (2.36 and 2.55): the combined text's order depends on how the two streams are
   joined (0.2's D1 note), so a check asserting the error dialog's output should assert the block's lines
   and `Updating 98ba313..948f7d1` without their relative order. Part W settles it: the dialog's
   `ErrorOutput` is `GitOutputCleaner.Clean` of the standard error alone, the two streams never joined,
   so `Updating 98ba313..948f7d1` is not shown (W7 step 4).
10. The workspace's copies hold 324 files (0.10 counted 323); not asserted anywhere.

#### 9. How these facts were derived

A throw-away console program referencing the built `tests/VisualCommit.Testing` (`dotnet build
tests/VisualCommit.Testing` at `b9c29d1`, nothing in the repository changed) called
`Scenarios.WorkspaceAsync` (8 copies), `RenamingRefspecAsync` (10), `IgnoredAsync` (20),
`GitSettingsAsync` (3), `OperationsAsync` (2), `GraphAsync`, `HistoryAsync`, `ReflogAsync`,
`ConflictsAsync`, `AuthRemoteAsync` (with `AuthRemoteServer.StartAsync()` on a system port) and
`ServeWorkspaceAsync` (twice, port 47393), without disposing the copies, with `TMP`/`TEMP` pointing into
the session's scratch folder. That folder's path is long (131 characters before `t\`), so a push into a
bare remote's quarantine (`origin.git/./objects/tmp_objdir-incoming-…/xx/<38>`) passed Windows' 260;
the program therefore added `[core] longpaths = true` to `TempRepo`'s two blank git folders (by
reflection, before any scenario), which every copy inherited: it changes no object, ref, status or
output examined here. A second program read each copy with `GitRepository` and `GraphLayout` (the
rows, lanes, colours, labels and statuses above) and called `ConflictBannerViewModel.TextFor` /
`ToolTipFor`; a third ran the folder server with an askpass program logging git's prompts. Every other
fact was run with Git 2.36.0.windows.1 (`C:\Program Files\Git`) in the app's environment
(`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME` and `XDG_CONFIG_HOME` in the scratch
folder, `LC_ALL=en_US.UTF-8`, `LANGUAGE=en`, `GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`,
`GIT_LITERAL_PATHSPECS=1` but for `check-ignore`, `GIT_OPTIONAL_LOCKS=0` for reads, D72's dates
2026-01-02T09:00:00+00:00, `MSYSTEM` unset) and replayed with Visual Studio's Git 2.55.0.windows.5,
its `mingw64/bin` first on `PATH`, each operation on a fresh copy. Pins checked against
`Phase6ScenarioTests`: every workspace status line, every ref and remote ref (`2248c9d`, `cbd9037`,
`147c6e3`, `ec855ba`, `23e2ad1`, `245b729`, `21b8aeb`, `f419c6e`, `67a2660`, `98ba313`, `948f7d1`,
`0e943ed`, `8236f53`, `229cea5`, `09d320a`, `ba18976`, `42a778b`, `937a43b`), the relative URLs and
branch keys, `MERGE_HEAD`, old-site absent, the copy's fetches (`100% (4/4)`, `100% (11/11)`, then
`+0 -4`, `+1 -3`, `+0 -3`), the refspec lines, tracking refs and the four `branch --track probe` results,
the ignored scenario's commits, stash, ignored entries, `c49cac4`, `vendor-remote` and the three
refusals, and "settings"' missing `user.*`: no difference. Not run: the app itself (no picture, no
measured position), Git 2.30, CI's 2.56 and 2.43.

### Part W: workspaces and the dashboard

Part W covers design section 1 (workspaces: the model, `workspaces.json`, the edits, the commands,
the folder scan and its checklist, folders that are gone, dashboards in the session), section 2 (the
dashboard tab: its place among the tabs, opening a workspace, opening a repository from a row, the
three columns that become one, the header, the rows, the row menu, the toolbar and the status bar on
a dashboard tab, the activity log there, when rows are read), section 3 (the repositories behind the
rows: summary mode, promotion, the registry), section 4 (Fetch all and Pull all: which rows each acts
on, the slots, the prompts of one run, Cancel all and a row's own Cancel, failures kept in their rows
with `Details…`, a single row's Fetch and Pull, Pull's outcomes and texts, the results) and 13.1 (the
welcome page's Workspaces section), with the owner's answers D187, D188, D189 and D195 and the design's
decisions D203 to D211, D227 and D229: checks W1 to W9 of design 17.4. The dashboard's keys are K13's
(part K), the dashboard and the scan's checklist in both themes L30's and the tab strip with ten tabs
L33's (part L); a row's `Fetch` and `Pull` texts that the settings change (identity, pull mode) are part
S's. This part was written before the UI it tests (D15), from `docs/phase-6-design.md` sections 0 to 4,
13.1, 14, 16.1 and 17, decisions D1 to D231, `requirements.md` (C1, Q4), `plan.md` "Phase 6" and the
scenario facts. Every git state below was derived with Git 2.36.0.windows.1 on copies of "workspace"
built by the merged scenario code (scenario facts 9), and every git text replayed with Git
2.55.0.windows.5 (see "How the expected git states were derived", part W). No phase 6 UI exists yet:
every position is derived from the design and taken by the checks from the controls' bounds (D122's
rule), and every new text is design 14's.

#### Conventions of part W

- **Project, class and test methods.** The checks are in `tests/VisualCommit.VisualTests.Phase6`,
  class `Phase6ChecksW` (`Phase6Checks.W.cs` and `Phase6Checks.W.<topic>.cs`), with what they compare
  against in `Phase6Expectations.W.cs` (which the real-window pass links, 17.3) and its tests in
  `Phase6ExpectationTests.W.cs`. One test method per app start (`FreshApplication`), each on a fresh
  copy of "workspace" (`Scenarios.WorkspaceAsync()`) and a fresh data folder, but W9's, a plain
  `[Fact]` with two app starts on one data folder (phase 5's A3 pattern, D32). The methods are named
  in "Test methods and app starts (part W)": eleven methods, twelve app starts (W1 and W5 are two
  methods each, decisions WD6 and WD36). The real-window pass repeats the steps marked RW in
  `Phase6RealWindowPass.W.cs`, with `RealApp.Phase6.cs` and `RealApp.Dashboard.cs`.
- **Run time and the hang watchdog.** `HangWatchdogAttribute` gives each test method 3 minutes in all,
  and design 17.6 puts the default run at about 1.2 s per git process under load (phase 5's reflog
  scenario: 145 calls in over 3 minutes), so a method of 150 git processes run one after another
  could spend 3 minutes in git alone there. Phase 5 split A2, which took up to 2 min 20 s beside other
  runs, and kept every U method to six operations or fewer (phase 5's report, AD7, UD17). The git
  processes of each method, estimated from its steps (none of phase 6's UI has run): a dashboard start
  73 (`git --version` and 17.6's 72 for nine rows; 81 on CI's Windows and macOS, 1.2); a refresh after
  an operation, the refs read and the status read, 6 or 7; a tab that adopts a row's view model, its
  history load and the clean-up's backups read; over HTTP an `http-backend` with its `upload-pack` per
  answered request (the folder server's) and an askpass helper per prompt. W1 part A about 85 (no
  dashboard start: `git --version`, api's two openings and read, the scan's nine openings, eight
  reads), about 150 when step 8's activation reads the nine rows, which the 10 s rule decides; W1 part
  B about 100 (two restored tabs' openings and histories, eight rows read); W2 about 85; W3 about 73;
  W4 about 150 (eight fetches and their refreshes, the HTTP requests' processes); W5 as one method
  about 220 (run 1's four fetches and their refreshes, the F5 pass over nine rows, run 2's eight
  fetches and seven refreshes), about 4 minutes of git at 17.6's rate, so it is two methods on fresh
  copies (decision WD36): `Check_W5_cancel_all` about 160 and `Check_W5_row_cancel` about 140; W6
  about 160; W7 about 170 (five pulls of about 15 calls each, docs' tab, the undo), about 225 when step
  7's activation reads the eight rows without a tab; W8 about 160 to 200 (three adoptions, the held
  fetch, the commit, the 10-s pass of seven rows); W9 about 155 over its two starts. On Windows a held
  fetch adds the hold's `sleep` processes, one per 0.5-s round while it waits (17.2), and a fetch that
  a cancel kills goes with its whole job, its shell and `sleep` included (`GitRunner.Kill` through
  `WindowsJob`); on macOS and Linux only git is killed, so the shells of W5's killed fetches poll on (a
  cheap `sleep 0.5` there) until `Dispose` releases them in the `finally`, at most their 120 rounds.
  Many of these calls run side by side (four slots, four reads at once), so the rate overstates a
  method's length; but these are estimates. No method may take more than about 90 s alone or 2 minutes
  beside a loaded run (the default run going): the lead times each method with `bash
  scripts/time-scripted-check.sh "*Check_W<n>_…"` alone and beside a loaded run when the checks first
  run (WP-V), W6 to W9 first and W7 above all (about 2.5 to 4.5 minutes of git at 17.6's rate if their
  calls ran one after another), writes the measured figures here in place of these estimates, and
  splits a method above those limits at a step that needs nothing from earlier steps, on a fresh copy,
  as W5 is split; a split made then is recorded under "Changed in this report after its first commit"
  (the figures cannot be measured before this report is committed: it is written before the UI, D15).
- **Defaults.** Every check runs at 1100×700 in the dark theme but W3 (1920×1080, light). Unless a
  check says otherwise, a method opens the workspace with `Phase6Checks.OpenWorkspaceAsync(data, copy,
  1100, 700, AppTheme.Dark)` (17.2): it writes `workspaces.json` with `copy.ToWorkspace()` (the
  workspace "Web", id `web`, the ten paths `copy.PathOf(name)` in `TempWorkspace.Names`' order: api,
  web-app, docs, mobile, design-system, billing, infra, scratch, notes, old-site) and `session.json`
  with one tab, `TabState(null, "web")`, active; starts the app; and waits for the dashboard's first
  read (`ShellDriver.WaitForDashboardReadAsync()`: the app log's line `Web: read … repositories in
  {ms} ms` and no row showing `Reading…`) and every row's queue idle (`WaitForAllRowsIdleAsync`). The
  pointer then rests at the neutral point. The harness's clocks are on (phase 5's: the record clock
  frozen at 2026-01-02 09:00:00 UTC, so every activity-log time reads `09:00:00` and every duration
  `0 ms`; the backup clock fixed); auto-fetch is off in both harnesses
  (`VISUALCOMMIT_AUTO_FETCH_MINUTES=0`); the app's git environment is the session's own (17.2:
  `GIT_CONFIG_GLOBAL` in `<data>/harness/global.gitconfig`, which no step of part W writes).
- **"Before the start"** is a file write, a `TempRepo` git call (`copy.Repo(name)`, D72's dates, the
  copy's identity), a `FetchHold` call or a folder server's set-up, made on the copy before
  `ShellDriver.Start`, as a terminal would. **"Outside the app"** while it runs is the same, followed
  by a wait on the condition the step names (`OutsideChangeTimeout`, 30 s).
- **Dashboard rows** count from 0 at the top (scenario facts 0). At 1100×700 and 1920×1080, with the
  activity log closed and no `DashboardNotice`, row k lies at y 152 + 26 k to 178 + 26 k, its centre
  at **y 165 + 26 k** (scenario facts 1): the ten rows' centres 165, 191, 217, 243, 269, 295, 321,
  347, 373 and 399, the tenth ending at y 412. A row is clicked, double-clicked or right-clicked **in
  its Repository cell, at x 66** (decision WD22), so a menu opened on row k has its top-left corner at
  (68, 167 + 26 k), moved up only where it would pass y 696. The columns are 2.6's (scenario facts
  1): at 1100 Repository x 0 to 132, Branch 132 to 282, Ahead / behind 282 to 412, Changes 412 to 632,
  State 632 to 812, Operation 812 to 1100 (Folder hidden); at 1920 Repository 0 to 132, Folder 132 to
  780, Branch 780 to 930, Ahead / behind 930 to 1060, Changes 1060 to 1280, State 1280 to 1460,
  Operation 1460 to 1920; a cell's text starts 8 into its column. The header (`DashboardHeader`) is y
  88 to 124, the headings (`DashboardColumnHeaders`) y 124 to 152. The checks take every position from
  the controls' bounds and assert these values ±2 where they state them.
- **A row is written** `name | branch | ahead / behind | changes | state | operation`, each cell as
  scenario facts 0 writes it: `text` (tone; tooltip), "empty" for a cell with no text; an Ahead /
  behind cell with arrows as ↑ `a` ↓ `b` (`IconArrowUp` and `IconArrowDown`, 10×10, and the counts,
  all in `VcTextSecondaryBrush`, each only above 0, 6 apart) with its automation name. The scripted
  run reads rows through `ShellDriver.DashboardSnapshot()` (the header, and per row its cells' texts,
  tones, tooltips and automation names, whether `Cancel` or `Details…` shows, whether it is selected
  and whether it has a read running or waiting, `IsReading`; for the dashboard whether a pass is under
  way, `IsPassRunning`: decision WD37) and asserts them with `AssertRow` and `AssertDashboardHeader`
  (17.2). Every text a row of part W shows is drawn whole (`TrimmedTextBlock.IsTrimmed` false) at 1100
  and 1920 (2.6's measure), and the checks assert it, but W3's Folder cells off the development
  machine (decision WD29).
- **Colours** are written by token, with the dark values (phase 5's conventions): `VcTextPrimaryBrush`
  `#E4E7EC`, `VcTextSecondaryBrush` `#9BA3B0`, `VcTextDisabledBrush` `#5F6775`, `VcWarningBrush`
  `#E0A23B`, `VcDangerBrush` `#E5606B`, `VcChromeBackgroundBrush` `#20242C`, `VcBorderBrush`
  `#323845`, `VcSelectionBrush` `#2A3150`, `VcRowHoverBrush` `#1C1F27`, `VcBackdropBrush` `#99000000`;
  in W3's light theme `VcTextPrimaryBrush` `#1B1F27`, `VcTextSecondaryBrush` `#5C6572`,
  `VcChromeBackgroundBrush` `#EBEDF1`, `VcSelectionBrush` `#DDE1FA`, and D198's darker light tones
  `VcWarningBrush` `#895A17` and `VcDangerBrush` `#B7323D` (11.10). "Secondary", "primary",
  "warning" and "danger" are those four text tokens. The Branch cell's text is primary (decision WD34).
- **The dashboard tab's toolbar and status bar** (2.8, 4.9, 4.10) are written **TD** for the state
  with rows and no run: `FetchButton` and `PullButton` enabled with the tooltips (and help texts)
  `Fetch every repository of Web (Ctrl+Shift+F)` and `Pull every repository of Web (Ctrl+Shift+L)`;
  `PushButton`, `BranchButton`, `StashButton`, `PopButton`, `UndoButton`, `RedoButton` and
  `SearchButton` disabled with no tooltip; `SettingsButton` (`Open settings (Ctrl+,)`) and
  `ThemeSwitch` (`Switch to the light theme`) enabled; the header's `Fetch all` and `Pull all` as the
  toolbar's Fetch and Pull, `Add repository…` enabled (`Add a repository to Web`), `Refresh` enabled
  (`Read the repositories of Web again (F5)`), `DashboardMore` (`More`). The status bar: the branch
  part `StatusWorkspaceIcon` (`IconWorkspace`, 13×13, secondary) and `Web`; `OperationStatus` `Ready`
  (secondary); no bar, no Cancel, no waiting count; `ActivityLogToggle` as 2.9 says for the selection
  (with no row selected disabled, tooltip `Select a repository to see its activity`). **TR** is the
  same during a run, but Fetch and Pull (toolbar and header) disabled with `Wait for Fetch all to
  finish` (or `Wait for Pull all to finish`), and the status bar showing the run (4.9). The tab:
  `TabButton` id `DashboardTab`, name and tooltip `Web`, `ItemStatus` `Workspace`, `TabIcon`
  (`IconWorkspace` 12×12) 14 from the tab's left and the title 6 after it, primary on the active tab
  and secondary on another, its close button shown, 120 wide at its natural width (13.4).
- **The neutral point** (760, 687): at 1100×700 it lies in a dashboard tab's status bar right of every
  text part W shows there (the branch part ends near x 50, the run's text starts near x 66 and, with
  its bar, `Cancel` and the waiting count, ends left of x 450 for every text of this part) and left of
  `GitStatus` (decision WD23); at 1920×1080 in the dashboard's list below its tenth row. Nothing there
  has a tooltip. `Shot` moves the pointer there before every capture unless the step rests it on
  something; a capture with a menu open leaves it where the click that opened it was; under a dialog
  it moves to the neutral point, as `Shot` does (`Phase6Checks.Shot`: only an open menu, a drag or
  `keepPointer` keeps it).
- **Menus** are written as phase 5's: header first, items in order, "—" for a separator, "(danger)"
  an enabled item in `VcDangerBrush`, "(disabled: "…")" a disabled item with its tooltip,
  "(highlighted)" the item with the focus, the first enabled one when a menu opens (D105). A menu's
  height is 8 + 24 + 26 × items + 9 × separators (±2), `WindowLayout.MenuHeight`; a right-click menu's
  top-left corner lies 2 right of and 2 below the pointer, moved to stay 4 inside the window; a button's
  menu opens below the button, moved to stay 4 inside the window (decision WD10). The menu layer's log
  line names the dashboard (2.7): `Web: Context menu opened: <name> by pointer`.
- **Dialogs** are phase 3's: the name dialog (`NameDialog`: `NameDialogTitle`, `NameDialogContext`,
  `NameBox`, `NameError`, `DialogRepository`, `ConfirmButton`, `CancelButton`), the confirmation
  (`ConfirmationDialog`: `ConfirmTitle`, `ConfirmQuestion`, `ConfirmDetail`), the credentials dialog
  (`CredentialsDialog`: `DialogTitle`, `CredentialsText`, `DialogRepository`, `CredentialUsername`,
  `CredentialPassword`) and the error dialog (`ErrorDialog`, asserted with `AssertErrorDialogAsync`:
  `ErrorTitle`, `ErrorSummary`, `ErrorRepository`, `ErrorOutput`, `ErrorCommand`, the buttons left to
  right); the scan's checklist is 1.9's (`AddRepositoriesDialog`). "No dialog" is
  `AssertNoDialogAsync`. Every dialog of part W over the dashboard dims it with the backdrop (the
  command palette, part K's, does not: D212).
- **Waits** are on conditions, never on a fixed time: `WaitForDashboardReadAsync()` for a read pass
  (with `since`, a pass line logged after a mark of the app log taken before the input: W8 step 9);
  `WaitForRowAsync(name, predicate)` for one row; `WaitForBulkRunAsync()` for a run's end (its result
  text and every row's queue idle); `WaitForRowPercentAsync(name, percent)`; on a repository tab
  `WaitForIdleAsync` and the queue's log line `<repo>: <Title>: <outcome> after <N> ms: <message>`; a
  held fetch by `FetchHold.WaitForHeldAsync(n)` or by the folder server's `HoldNextFetchPack(…)` and
  `HoldNextRequest(…)` tasks; a network step within `NetworkTimeout` (60 s). The fetch hold is
  released in a `finally` (`FetchHold.Dispose`), and the folder server disposed there.
- **The call log** (`IGitCallLog`) is read with `ShellDriver.CallsSince(sequence, worktree)` by
  `GitCallRecord.Sequence` and `WorkingDirectory` (17.2); "no call in X" means no record whose
  `WorkingDirectory` is X's folder by `SameFolder` (its saved spelling or git's, so that CI's macOS,
  where the copy's `/var/…` is git's `/private/var/…`, is checked too). A call is recorded when it
  ends, never while it runs (`GitRunner` adds a record for a call that failed to start, was cancelled
  or completed), so a running call is shown by its row, a marker or the server, not by the call log. A
  command is written as its `CommandText` renders it. "Only reads" means every record has no
  `OperationId` and is one of the reads a row or a tab makes: `git --version` (the start),
  `OpenAsync`'s `git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir
  --git-common-dir`, `git rev-parse --show-prefix` (1.2, only where the opened spelling differs from
  git's, as on CI), `git rev-parse --git-common-dir` (`OpenAsync`'s second call in that same case,
  `ResolveCommonDirectoryAsync`), the refs read's five calls (`git for-each-ref …`, `git remote` or,
  once WP-RA's reader is merged, `git config --null --get-regexp ^remote\.` (10.2), `git stash list
  …`, `git symbolic-ref -q HEAD`, `git rev-parse -q --verify HEAD`), the status read (`git status
  --porcelain=v2 -z --untracked-files=all --renames`) and, on a repository tab, its history load;
  billing's merge is read from files (D89), with no git call.
- **`workspaces.json` and `session.json`** are read with `JsonWorkspaceStore` and `JsonSessionStore`
  and asserted structurally: `version` 1; each workspace's `id`, `name` and `repositories` in order; a
  path the app added is the repository's `IGitRepository.WorkingDirectory` (git's spelling: the
  working tree's top, absolute, no trailing separator, 1.1), which `SameFolder` matches to the
  repository's folder (on the development machine the text of `copy.PathOf(name)`; on CI's Windows the
  long form of its `RUNNER~1`, on CI's macOS `/private/var/…`, so it is never compared as text with
  `copy.PathOf(name)`); a path the check wrote stays the text it wrote (1.2: a read never rewrites
  it). A new workspace's id is 32 lowercase hex digits (decision WD26).
- **Git's own texts** below (`ErrorOutput`, the call log's output) are Git 2.36.0.windows.1's and are
  asserted word for word only when `git --version` is 2.36; on any other git a check asserts the line
  marked "key", that `ErrorOutput` is `GitOutputCleaner.Clean` of the failed call's own output
  (`AssertGitOutputShown`), and the dialog's title, summary, repository line, command and buttons
  (phase 3's rule). `ErrorOutput` is `Clean(standardError, standardOutput)`: the cleaned standard
  error, or the cleaned standard output only when nothing is left of the standard error
  (`GitOutputCleaner.Clean`, `GitFailure.GitOutput`); the two streams are never joined. A failed
  fetch's `ErrorOutput` is `Clean` of the call's standard error followed by the app's own line `error:
  Could not fetch origin`, which `GitRepository.FetchAsync(null)` appends (D109,
  `OperationTexts.CouldNotFetchPrefix`) and the call log's record does not hold: on every git the
  check asserts that the last shown line is exactly that and applies `AssertGitOutputShown` to the
  lines before it (its `appLines` parameter, below). Where 2.55 prints otherwise the text says so
  (mobile's refusal: nine `hint:` lines before its key line; the fetches' `pack-reused 0 (from 0)`).
  Git's states (refs, `git status --porcelain=v2 --branch`, the files, HEAD's and the branch's reflog,
  `FETCH_HEAD` aside) are asserted on every version, each check's own reads with
  `GIT_OPTIONAL_LOCKS=0` (`TempRepo.GitWithEnvironmentAsync`), as the app's are, so that they never
  refresh an index the app reads. "The exact state" of a repository is `TempRepo.ExactStateAsync`'s
  (phase 5's part P). CI's gits (Windows and macOS 2.56.0, `ubuntu-latest` 2.55.0, the containers
  2.43.0) fall under this rule.
- **The folder server** (17.2) serves the copy's `remotes/` on port 47393
  (`AuthRemoteServer.Phase6VisualChecksPort`) through `Scenarios.ServeWorkspaceAsync`, which points
  each named repository's `origin` at `server.UrlOf(name)` (`http://127.0.0.1:47393/<name>.git`); every
  expected text that shows the port goes through `Phase6Expectations.WithPort(text, server.Port)`, and a
  fallback to a free port (`PortWasTaken`) is noted in the run's report (D97). W4 and W6 never hold the
  port at once (the project's headless tests take turns on one UI thread); part L's L31, in
  `Phase6Look`'s own process, can (conventions of every part, "Test servers and holds").
- **The fetch hold** (17.2) is `FetchHold.Install(data.Path)` and `HoldIn(copy.PathOf(name)…)` before
  the start, never the session's global file: every fetch of a held repository's file remote waits from
  its start, new commits or not, shown running without a percentage, until `Release()`.
- **Harness additions part W needs beyond 17.2**, for the lead to add with 17.2's helpers:
  `OpenWorkspaceAsync(data, copy, width, height, theme, TimeProvider? readClock = null)`, passing the
  clock through to `ShellDriver.Start` (17.2's helper takes none; W8 alone passes one);
  `DashboardSnapshot()`'s `IsReading` and `IsPassRunning`, and `DashboardSnapshot(workspaceId)` for a
  dashboard that is not active (decision WD37); `WaitForDashboardReadAsync(since)`;
  `WaitForTabOpenedAsync(path)`, `WaitForRepositoryAsync`'s conditions for a tab that is not active
  (its history loaded, `<repo>: working tree:` logged, its queue idle with the clean-up at opening);
  `Phase6Checks.SameFolder(copy, folder, path)`, true when `WorkspacePaths.Same` finds `path` equal to
  `folder` or to git's own spelling of it (`git rev-parse --show-toplevel` run there once per copy by
  the check's own `TempRepo` with `GIT_OPTIONAL_LOCKS=0`, normalised as `GitRepository` normalises it:
  the platform's separators, no trailing one), used for every folder and call-log match of part W; and
  `AssertGitOutputShown(call, key, shown, appLines)`, where the lines named in `appLines` must be the
  dialog's last lines, in order, and are left out of the "a line git printed" rule.
- **Dates** are UTC (`VISUALCOMMIT_TIME_ZONE=UTC`). The scenario's commits are dated 2026-01-01 12:00
  plus one minute per commit, per repository; the one commit the app makes in part W (W8) has the
  author and committer "Test Author" <author@example.com> (the copy's `user.*`) and both dates
  2026-01-02 09:00 UTC (D72), so it has the same SHA in every run.
- **Pictures** are named by `Phase6Checks.PictureName`'s rule,
  `^[gklsw]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`: `w`, the check's number, a step letter,
  then words (`w4a-held-at-25-75-45`); a check's letters run on through its methods in order. Every
  name is unique in the report (`Shot` refuses a name it has saved in the run). A picture whose
  content varies by nature from run to run (a temporary folder's name drawn, a focused text box's
  blinking caret, an indeterminate progress bar) says so in its check's Notes; its expected result is
  structural, and the gate compares it by inspection, not by hash.
- **The real window** repeats the steps marked RW in a real process at 1100×700 (the development
  machine's work area, 1500×952 logical pixels at 200 percent, holds no 1920×1080 window: those steps
  are scripted only, the plan's conditional part), with the real mouse and keyboard (FlaUI), finds
  controls by automation id and name, reads texts through UI Automation, writes `workspaces.json` with
  `RealApp.WriteWorkspaces`, drives Windows' folder dialog with `RealApp.ChooseFolderInDialog(title,
  folder, timeout)`, and saves each capture under the scripted picture's name in
  `artifacts/visual/phase-6/real-window/`, with a difference picture and the share of differing pixels
  in `run.txt` (at most 3 percent, D33). It never acts outside the window and changes nothing on the
  owner's desktop (no theme change, no program started: `Show in folder` and `Copy path` are never
  clicked in part W).

#### Test methods and app starts (part W)

| Check | Test method (one app start each, unless said) | Scenario as the check starts it | Window, theme |
|---|---|---|---|
| W1 | `Check_W1_workspace_from_the_welcome_page` (steps 1 to 8) | `Scenarios.WorkspaceAsync()`; a fresh data folder: no `workspaces.json`, no `session.json`; `ShellDriver.Start(data.Path, 1100, 700)` (one empty tab) | 1100×700, dark |
| W1 | `Check_W1_add_open_tabs_rename_and_delete` (steps 9 to 13) | `WorkspaceAsync()`; `ShellDriver.WriteWorkspaces` writes "Web" (id `web`) with eight paths, `copy.ToWorkspace()` less web-app and docs: api, mobile, design-system, billing, infra, scratch, notes, old-site (decision WD19); `ShellDriver.StartWithTabs(data.Path, [new TabState(null, "web"), new TabState(copy.PathOf("web-app")), new TabState(copy.PathOf("docs"))], active 0, 1100, 700)` | 1100×700, dark |
| W2 | `Check_W2_ten_repositories` | `WorkspaceAsync()`; before the start `{root}\notes` copied file by file to `{root}\archive\old-site` (decision WD7); `OpenWorkspaceAsync` | 1100×700, dark |
| W3 | `Check_W3_ten_repositories_wide_and_light` | `WorkspaceAsync()`; `OpenWorkspaceAsync(data, copy, 1920, 1080, AppTheme.Light)` | 1920×1080, light |
| W4 | `Check_W4_fetch_all_progress` | `WorkspaceAsync()`; `Scenarios.ServeWorkspaceAsync(copy, requireAuth: false, "docs", "mobile", "design-system")`, then `server.HoldNextFetchPack("docs", 25)`, `("mobile", 75)`, `("design-system", 45)`; `OpenWorkspaceAsync` | 1100×700, dark |
| W5 | `Check_W5_cancel_all` (steps 1 to 4) | `WorkspaceAsync()`; `FetchHold.Install(data.Path)` and `HoldIn` the eight with a remote (api, web-app, docs, mobile, design-system, billing, infra, scratch); `OpenWorkspaceAsync` | 1100×700, dark |
| W5 | `Check_W5_row_cancel` (steps 5 and 6; decision WD36) | the same on a fresh copy and a fresh data folder | 1100×700, dark |
| W6 | `Check_W6_failures_and_one_sign_in` | `WorkspaceAsync()`; `ServeWorkspaceAsync(copy, requireAuth: true, "docs", "mobile")`; in scratch `git remote set-url origin ../remotes/missing.git` (`copy.Repo("scratch")`); `server.HoldNextRequest("mobile")`; `OpenWorkspaceAsync` | 1100×700, dark |
| W7 | `Check_W7_pull_all` | `WorkspaceAsync()`; in mobile `git config pull.ff only` (its `.git/config`); `OpenWorkspaceAsync` | 1100×700, dark |
| W8 | `Check_W8_rows_open_into_tabs_and_reads` | `WorkspaceAsync()`; `FetchHold.Install(data.Path)`, `HoldIn(copy.PathOf("mobile"))`; `OpenWorkspaceAsync(…, readClock: new ManualTimeProvider(…))` (17.2: W8 alone passes a read clock; the helper's `readClock` parameter is a harness addition, conventions: 17.2's `OpenWorkspaceAsync` takes none) | 1100×700, dark |
| W9 | `Check_W9_restart` (a plain `[Fact]`: two app starts on one data folder, each in `FreshApplication.RunAsync`: the first by `OpenWorkspaceAsync`, which writes the files; the second by `ShellDriver.Start(data.Path, 1100, 700)` writing nothing, then `WaitForRepositoryAsync`; in the real window `RealApp.WriteWorkspaces`, `RealApp.Launch(data, session)`, then `RealApp.Launch(data)`) | `WorkspaceAsync()` | 1100×700, dark |

#### Scenarios and graphs of part W

**"workspace"** (`Scenarios.WorkspaceAsync`, workspace "Web"), as scenario facts 2 give it and
`Phase6ScenarioTests` pins it: `{root}` (`TempWorkspace.Root`, `%TEMP%\VisualCommit.Tests\workspace-<12
hex>`, different in every copy) holds api, billing, design-system, docs, home, infra, mobile, notes,
remotes (eight bare `<name>.git`), scratch and web-app; old-site does not exist; every repository with
a remote reaches it by `../remotes/<name>.git`; notes has no remote; the copies' commits and states
are scenario facts 2.2's. Part W names these states:

**W0, the ten rows as read** (scenario facts 2.3; design 2.5, 2.6): header `Web` (13, SemiBold,
primary) and `10 repositories` (secondary); no row selected; no run text.

| k | y | Repository (tooltip) | Branch | Ahead / behind (automation name; tooltip) | Changes (tone) | State (tone; tooltip) | Operation |
|---|---|---|---|---|---|---|---|
| 0 | 165 | `api` (`{root}\api`) | `main` | `Up to date` (secondary; `main is level with origin/main`) | `Clean` (secondary) | empty | empty |
| 1 | 191 | `web-app` | `main` | ↑ `2` (`2 ahead`; `2 commits ahead of origin/main`) | `Clean` | empty | empty |
| 2 | 217 | `docs` | `main` | ↓ `3` (`3 behind`; `3 commits behind origin/main`) | `Clean` | empty | empty |
| 3 | 243 | `mobile` | `main` | ↑ `1` ↓ `2` (`1 ahead, 2 behind`; `1 commit ahead of and 2 behind origin/main`) | `Clean` | empty | empty |
| 4 | 269 | `design-system` | `main` | `Up to date` (`main is level with origin/main`) | `1 staged, 2 unstaged, 1 untracked` (primary; tooltip the same) | empty | empty |
| 5 | 295 | `billing` | `main` | `Up to date` (`main is level with origin/main`) | `1 conflict` (danger; tooltip the same) | `Merge in progress` (warning; `Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.`) | empty |
| 6 | 321 | `infra` | `Detached at 09d320a` | empty | `Clean` | empty | empty |
| 7 | 347 | `scratch` | `experiment` | `No upstream` (secondary; `experiment has no upstream`) | `Clean` | empty | empty |
| 8 | 373 | `notes` | `main` | `No remote` (secondary; `This repository has no remotes`; decision WD4) | `Clean` | empty | empty |
| 9 | 399 | `old-site` (`{root}\old-site`) | empty | empty | empty | `Folder not found` (warning; `{root}\old-site does not exist`) | empty |

Each Repository cell's tooltip is the row's saved folder (`{root}\<name>`). The Changes counts leave
untracked and conflicted entries out of the unstaged count (decision WD5, scenario facts 8 item 2).
Each row but old-site holds a summary-mode view model (3.1) after the read; old-site holds none (the
folder check found it gone with no process, 2.10).

**WA, W1 part A after the checklist** (scenario facts 2.7, 8 item 3): nine rows in the order they were
added, api (by `Add repository…`), then the checklist's eight by relative path: k 0 api, 1 billing,
2 design-system, 3 docs, 4 infra, 5 mobile, 6 notes, 7 scratch, 8 web-app; each row's cells W0's for
its repository; header `9 repositories`; no old-site.

**WB8 and WB10, W1 part B**: WB8 the eight rows of the written "Web" less web-app and docs, k 0 api,
1 mobile, 2 design-system, 3 billing, 4 infra, 5 scratch, 6 notes, 7 old-site, each W0's cells,
`8 repositories`; WB10 WB8 with k 8 web-app and k 9 docs appended (`Add open tabs`, in tab order),
`10 repositories`, the two following their tabs (3.5).

**W2's states**: **WL** is W0 with old-site's path replaced by `{root}\archive\old-site` and its row
read again: `old-site` (tooltip `{root}\archive\old-site`) | `main` | `No remote` (`This repository
has no remotes`) | `Clean` | empty | empty. **WR** is WL without old-site: nine rows k 0 to 8 as W0's,
`9 repositories`. **WM** is WR with docs moved up: api, docs, web-app, mobile, design-system,
billing, infra, scratch, notes.

**The Locate repository** (W2, decision WD7): `{root}\archive\old-site`, a file-by-file copy of
`{root}\notes` made before the start (no git process): `git rev-parse --is-inside-work-tree
--show-toplevel --absolute-git-dir --git-common-dir` there prints `true`, the folder itself, its
`.git` and `.git` (exit 0), `git rev-parse --show-prefix` an empty line; `refs/heads/main` `937a43b`
(Add the reading list), no `[remote]` section (`git remote` prints nothing), `git status
--porcelain=v2 --branch` with `GIT_OPTIONAL_LOCKS=0` only `# branch.oid 937a43b…` and `# branch.head
main` (clean: the stale stat data of the copied index cost a re-hash, never a change).

**WF, after W4's Fetch all** (scenario facts 2.5): W0 but docs ↓ `4` (`4 behind`; `4 commits behind
origin/main`), mobile ↑ `1` ↓ `3` (`1 ahead, 3 behind`; `1 commit ahead of and 3 behind
origin/main`) and design-system ↓ `3` (`3 behind`; `3 commits behind origin/main`), the eight rows with
a remote showing `Fetched from all remotes` (secondary) in Operation, notes `This repository has no
remotes` (secondary; tooltip the same), old-site nothing. Git: docs `refs/remotes/origin/main`
`245b72964a7ee4952ba789ed47201369ab4e2f50`, mobile's `67a26608eb41dcde8cb5f32e12173c99dc741bd1`,
design-system's `948f7d187454e2096a92ef2c5d7281ce72aa34f5`; every other ref, every `main` and every
working tree unchanged.

**WP, after W7's Pull all** (scenario facts 2.6): api `main is already up to date`, web-app the same,
docs `Pulled 4 commits into main (fast-forward)` and `Up to date` (`main is level with origin/main`),
mobile `Pull failed` (danger) and `Details…`, ↑ `1` ↓ `3`, design-system `Pull failed` and `Details…`,
↓ `3`, its changes unchanged; billing `Finish or abort the merge first`, infra `HEAD is detached:
check out a branch to pull`, scratch `experiment has no upstream: set one first`, notes `main has no
upstream: set one first` (each secondary, its tooltip the reason); old-site nothing new. Git: docs
`main` = `origin/main` = `245b729…`, its files `README.md`, `changelog.md` (`Changes.`), `faq.md`
(`Questions.`), `guide.md` (`Guide.`), `install.md` (`Install it.`); mobile `main` `21b8aeb…`,
`origin/main` `67a2660…`, no `MERGE_HEAD`; design-system `main` `98ba313…`, `origin/main` `948f7d1…`,
`1 M. … colours.txt`, `1 .D … fonts.txt`, `1 .M … spacing.txt`, `? icons.txt`; api, web-app as built.

**Graphs in the repository tabs part W opens** (1100×700, Message x 438 with one or two lanes; display
row d's centre y 129 + 26 d; lane L's x 406 + 16 L; colours dark 0 `#7C8CFF`, 1 `#3FB97F`). A0, WA0,
D0 and M0 are scenario facts 2.4's; MF, WC, DP and DU were laid out here by the app's own
`GraphLayout` over `LoadCommitsAsync` on the states real git made ("How the expected git states
were derived", part W):

| Graph | Rows: labels, node (lane, colour), message, date, short SHA | Lines |
|---|---|---|
| A0 (api as built) | 0 `main` ✓, `origin/main` (drawn `main` ✓, `+1`) (0, 0) Add the health route 2026-01-01 12:01 `2248c9d`; 1 (0, 0) Initial commit 12:00 `e1d2947` | lane 0 rows 0 to 1 |
| WA0 (web-app as built) | 0 `main` ✓ (0, 0) Style the pages 12:02 `cbd9037`; 1 (0, 0) Add the about page 12:01 `0d6e6c5`; 2 `origin/main` (0, 0) Initial commit 12:00 `147c6e3` | lane 0 rows 0 to 2 |
| WC (web-app after W8's commit) | 0 `main` ✓ (0, 0) Add the contact page 2026-01-02 09:00 `092116c`; 1 (0, 0) Style the pages 12:02 `cbd9037`; 2 (0, 0) Add the about page 12:01 `0d6e6c5`; 3 `origin/main` (0, 0) Initial commit 12:00 `147c6e3` | lane 0 rows 0 to 3 |
| D0 (docs as built) | 0 `origin/main` (0, 0) Fix the guide's typos 12:03 `23e2ad1`; 1 (0, 0) Write the FAQ 12:02 `43723b6`; 2 (0, 0) Write the install guide 12:01 `245235b`; 3 `main` ✓ (0, 0) Initial commit 12:00 `ec855ba` | lane 0 rows 0 to 3 |
| DP (docs after Pull all) | 0 `main` ✓, `origin/main` (drawn `main` ✓, `+1`) (0, 0) Add the changelog 12:04 `245b729`; 1 (0, 0) Fix the guide's typos 12:03 `23e2ad1`; 2 Write the FAQ 12:02 `43723b6`; 3 Write the install guide 12:01 `245235b`; 4 Initial commit 12:00 `ec855ba` | lane 0 rows 0 to 4 |
| DU (docs after the pull's undo) | 0 `origin/main` (0, 0) Add the changelog 12:04 `245b729`; 1 Fix the guide's typos `23e2ad1`; 2 Write the FAQ `43723b6`; 3 Write the install guide `245235b`; 4 `main` ✓ (0, 0) Initial commit 12:00 `ec855ba` | lane 0 rows 0 to 4 |
| M0 (mobile as built) | 0 `main` ✓ (0, 0) Name the app 12:03 `21b8aeb`; 1 `origin/main` (1, 1) Add the settings screen 12:02 `f419c6e`; 2 (1, 1) Add the login screen 12:01 `a17bccb`; 3 (0, 0; lane 1 ends here) Initial commit 12:00 `617d476` | lane 0 (colour 0) rows 0 to 3; lane 1 (colour 1) from row 1's node into row 2's node, then into row 3's node |
| MF (mobile after W8's fetch) | 0 `origin/main` (0, 0) Add the about screen 12:04 `67a2660`; 1 `main` ✓ (1, 1) Name the app 12:03 `21b8aeb`; 2 (0, 0) Add the settings screen 12:02 `f419c6e`; 3 (0, 0) Add the login screen 12:01 `a17bccb`; 4 (0, 0; lane 1 ends here) Initial commit 12:00 `617d476` | lane 0 (colour 0) rows 0 to 4; lane 1 (colour 1) from row 1's node through rows 2 and 3 into row 4's node |

No working-changes row in any of them when pictured; none has a banner.

**The commit W8 makes** (decision WD17): in web-app, `contact.html` (`<h1>Contact</h1>\n`, untracked,
written outside the app) staged by `Stage all` and committed with the summary `Add the contact page`
and no description: `092116c01a4d62eefc50c08ac9f1e9109fbe0059`, tree `fcfe571`, parent `cbd9037…`,
author and committer Test Author <author@example.com> 2026-01-02 09:00:00 +0000, message
`Add the contact page`; then `git status --porcelain=v2 --branch` `# branch.ab +3 -0` and nothing
else. The same SHA on 2.55.

**W8's file in scratch** (decision WD18): `todo.txt` (`Try it\n`) written outside the app: the status
read gives `Untracked` {todo.txt} and `Unstaged` [todo.txt Added], so the row shows `1 untracked`
(decision WD5).

**The pull's undo** (W7; phase 5's Keep move, its design 0.4 and 7.3): in docs' tab after WP, `Undo
pull into main` runs `git update-index -q --refresh`, `git diff-tree -r -z --no-renames --name-only
--diff-filter=A 245b729… ec855ba…` (the Keep move's ignored-files guard; nothing added, so no
`check-ignore`), `git read-tree -m -u 245b72964a7ee4952ba789ed47201369ab4e2f50
ec855bae3aa27bbf3f546b0ec7934cca41d65c01` (exit 0) and `git update-ref -m "VisualCommit: undo pull
into main" refs/heads/main ec855bae3aa27bbf3f546b0ec7934cca41d65c01
245b72964a7ee4952ba789ed47201369ab4e2f50` (exit 0; `CommandText` quotes the message, one argument with
spaces, `GitCommand.DisplayText`), with the history's reads before and after. After it: `main`
`ec855ba…`, `origin/main` `245b729…`, `# branch.ab +0 -4`, the working tree `README.md` and `guide.md`
(`Guide`) only, the newest entry of HEAD's and of `main`'s reflog `VisualCommit: undo pull into main`.
2.55 identical.

#### Texts these checks rely on (part W)

The design's (section 14; the section named there wins if the two differ), in house style: disabled
reasons, tooltips and status-bar results have no full stop; summaries, notices and questions are
sentences. `{workspace}` is `Web` (`Web team` after W1's rename), `{root}` the copy's folder.

- **The welcome page** (13.1, 14.9): heading `Workspaces`; `New workspace…` (automation name `New
  workspace`; tooltip `Create a workspace: repositories you check and update together`); `No
  workspaces`; an item: the name, `{N} repositories` / `1 repository` / `No repositories`, tooltip the
  folder names joined by `, `, at most 8, then `, …`; the page in reading order `No repository open`,
  `Open, clone or init a repository to see its history.`, `Open`, `Clone`, `Init`, `Workspaces`, `New
  workspace…`, `No workspaces`, `Recent repositories`, `No recent repositories`.
- **The name dialog** (1.6, 14.6): titles `New workspace`, `Rename workspace`; the rename's context
  `Rename {name}`; placeholder `Workspace name`; confirm `Create`, `Rename`; errors `Enter a name.`,
  `This is not a valid workspace name: it has more than 100 characters.`, `This is not a valid
  workspace name: it has a line break or another control character.`, `A workspace named {existing}
  exists.` (none shown by part W). **The deletion** (1.6): title `Delete workspace?`; `Delete the
  workspace {name}? Its {N} repositories stay where they are: nothing on disk changes.` (`Its repository
  stays where it is: …`; `Nothing on disk changes.`); detail `Its dashboard tab closes.`; confirm
  `Delete` (destructive).
- **Folder dialogs' titles** (1.7, 1.10): `Add a repository to {workspace}`; `Locate {folder name}`.
- **The scan's checklist** (1.9, 14.6): title `Add repositories to {workspace}`; `Found {N}
  repositories in {folder}.` (`Found 1 repository in {folder}.`, `No repositories were found in
  {folder}.`, `No other repositories were found in {folder}.`); rows' `Already in this workspace` and
  `Contains {folder name}`; buttons `Cancel`, `Add {N} repositories` / `Add 1 repository`, disabled
  `Add repositories`; the searching texts (`Looking for repositories in {folder}…`, `Searched {n}
  folders, found {k} repositories`) are App tests' (1.12), never pictured.
- **The dashboard's header** (2.5, 14.3): the title; `1 repository` / `{n} repositories` / `No
  repositories`; `Add repository…` (`Add a repository to {workspace}`); `Fetch all`, `Pull all`
  (`Fetch every repository of {workspace} (Ctrl+Shift+F)`, `Pull every repository of {workspace}
  (Ctrl+Shift+L)`; disabled `{workspace} has no repositories`, `Wait for Fetch all to finish`, `Wait
  for Pull all to finish`, `No repository of {workspace} has a remote`, `No repository of {workspace}
  can be pulled`); `Refresh` (`Read the repositories of {workspace} again (F5)`); `DashboardMore`
  (`More`) and its menu: header `{workspace}`, `Add open tabs`, —, `Rename workspace…`, `Delete
  workspace…`. The empty state: `{workspace} has no repositories yet.`, `Add repository…`, `Add open
  tabs`. `Add open tabs`' reasons: `No tab has a repository open`, `Every open repository is in this
  workspace` (1.5, 14.5).
- **The columns and cells** (2.6, 14.3): headings `Repository`, `Folder`, `Branch`, `Ahead / behind`,
  `Changes`, `State`, `Operation`; Branch `{b}`, `Detached at {short}`; Ahead / behind automation names
  `{a} ahead, {n} behind`, `{a} ahead`, `{n} behind`, texts `Up to date`, `{up} is gone`, `No
  upstream`, `No remote`, `No commits yet`, tooltips `1 commit ahead of {up}` / `{a} commits ahead of
  {up}`, `1 commit behind {up}` / `{n} commits behind {up}`, `{a} commits ahead of and {n} behind {up}`
  (`1 commit ahead of and …`), `{b} is level with {up}`, `{b} has no upstream`, `This repository has no
  remotes`; Changes `Clean`, `1 conflict` / `{c} conflicts`, `{s} staged`, `{u} unstaged`, `{t}
  untracked`, joined by `, `; State `Reading…`, `Merge in progress` (and the other in-progress texts),
  `Folder not found`, `Not a repository`, `Git could not open it`, their tooltips `{path} does not
  exist`, `{path} is not a git repository`, `Git could not open {path}: {line}`; Operation
  `CompactText` (`Fetching from all remotes… {p}%`), `Cancel` (name `Cancel`, tooltip `Stop git`),
  `Waiting to fetch…`, `Waiting to pull…`, `Details…` (name `Details`), and the toolbar's results and
  reasons below.
- **The row menu** (2.7, 14.10, 14.5): header `{name}` (tooltip its folder); `Open`; `Fetch`; `Pull`;
  `Cancel {title}`; `Error details…`; `Show in folder`; `Copy path`; `Move up`; `Move down`;
  `Locate…`; `Remove from workspace`; reasons `Wait for {name} to be read`, the State tooltips, the
  toolbar's (`This repository has no remotes`, `HEAD is detached: check out a branch to pull`, `{b} has
  no upstream: set one first`, `Finish or abort the merge first`), `Wait for Fetch all to finish` /
  `Wait for Pull all to finish`, `{title} can no longer be cancelled`, `{path} does not exist`, `It is
  the first repository` / `It is the last repository`.
- **The run** (4.9, 14.3, 14.2): `Fetching {n} repositories…` / `Pulling {n} repositories…`; `Fetching
  8 repositories… 5 done`; `Cancelling: waiting for {r} repositories…` (`… 1 repository…`); results
  `Fetched {k} repositories` / `Pulled {k} repositories` (`Fetched 1 repository`, `Fetched no
  repositories`), then `; {f} failed`, `; {s} stopped`, `; {c} cancelled`, `; {x} skipped`, danger with
  a failure, else warning with a stop, else secondary; `Cancel all` (tooltip `Cancel the rest of Fetch
  all` / `Cancel the rest of Pull all`); the status bar's `{w} waiting`, `Ready` before the first run.
- **Existing operation texts reused** (`OperationTexts`, scenario facts 7): `Fetch` (title, `Fetching
  from all remotes…`, `Fetch failed`, `Git could not fetch from {remotes}.`), `Fetched from all
  remotes`, `Fetch cancelled`; `Pull` (`Pull failed`), `{b} is already up to date`, `Pulled {N} commits
  into {b} (fast-forward)`, `1 conflict prevents the merge. Commit, stash, or discard your changes
  first.`; the undo's `Undo pull into main (Ctrl+Z)`, `Undid pull into main`, `Redo pull into main
  (Ctrl+Y)`, `Nothing to undo`, `Nothing to redo` (phase 5's 7.6 with 6.8's keys). **New**:
  `OperationTexts.PullNotFastForward` `{b} and {up} have diverged, and pull.ff is set to only: merge
  {up} into {b} or rebase {b} onto {up}.` (4.8).
- **The sign-in dialog** (phase 3's, with the port and 3.8's line): `Sign in to 127.0.0.1:{port}`,
  `Git needs a user name and a password or token for http://127.0.0.1:{port}.`, `In {repository}`,
  `User name`, `Password`, `Cancel`, `Sign in`.
- **The status bar and the activity log on a dashboard** (2.8, 2.9, 14.2, 14.5): the branch part
  `{workspace}` after `IconWorkspace`; the toggle's reasons `{workspace} has no repositories`, `Select a
  repository to see its activity`, `Wait for {name} to be read`, the selected row's State tooltip; its
  enabled texts `Show every git command the app has run (Ctrl+J)`, `Hide the activity log (Ctrl+J)`.
- **The tab** (13.4, 14.9): the workspace's name as title, tooltip and automation name, `ItemStatus`
  `Workspace`; "+" named `Open a new tab`.
- **Log lines** (14.14; not on screen): `{workspace}: read 1 repository in {ms} ms` / `{workspace}:
  read {n} repositories in {ms} ms`; `{name}: summary read in {ms} ms` (Debug); `{workspace}: Fetch
  all: started for {n} repositories, {s} skipped`, `{workspace}: Fetch all: {result}` (`Pull all`
  likewise); `Web: Context menu opened: {name} by pointer` (Debug); `Opened the repository at …` per
  confirmed candidate.
- **Git's texts** (2.36; 2.55 where said): scenario facts 2.5 (the fetches, the progress lines, the
  sign-in prompts and requests, the missing remote's four lines, and D109's `error: Could not fetch
  origin`, the app's line and not git's) and 2.6 (the merges' outputs, mobile's `fatal: Not possible to
  fast-forward, aborting.` and 2.55's nine hints, design-system's block on standard error; its
  `Updating 98ba313..948f7d1` is standard output and never shown, correcting scenario facts 8 item 9);
  the undo's calls under "Scenarios and graphs of part W".

### Part K: the command palette and the keys

Part K covers design sections 5 (the command palette) and 6 (the fixed keys), the owner's answers
D191, D192 and D199 and the design's decisions D212 to D215: checks K10 to K13 of design 17.4. The
palette opens with Ctrl+Shift+P over the window without dimming, shows the commands with their keys at
once, filters the commands and the objects' menu items by every word of the query, shows a disabled
entry dimmed with its menu's reason and runs the highlighted entry after it has closed (K10); every
keyed menu item and every keyed button shows its key, the commit button's `Commit 1 file (Ctrl+Enter)`
included, while a disabled button keeps its reason alone (K11); every main action is reachable from
the keyboard alone, through the router's keys, Ctrl+Tab and Mod+digits among them, F6 and Shift+F6
round the regions, Shift+F10 in a branch's menu, Mod+Enter in the message box, D163's undo keys and
Mod+, for the settings screen (K12); and the keys keep their meaning in the conflict resolver and on a
dashboard (K13). The settings screen's Keyboard shortcuts list is part S's (S14); the palette in both
themes is part L's (`l31c-palette`, L34). This part was written before the UI it tests, from
`docs/phase-6-design.md` sections 2.1 to 2.10, 3.3 to 3.8, 4.2, 4.9, 4.10, 5, 6, 7.1, 7.2, 13.3,
13.4, 14, 15, 16.1 and 17, decisions D187 to D231, `requirements.md` R7 and `plan.md`'s phase 6, the
scenario facts and the earlier reports for what stays as it was. Every git state below was derived
with Git 2.36.0.windows.1 and every git text replayed with Git 2.55.0.windows.5, on copies of the
scenario repositories the merged scenario code built (see "How the expected git states were derived",
part K).

#### Conventions of part K

- **Project, class and test methods.** The checks are in `tests/VisualCommit.VisualTests.Phase6`,
  class `Phase6ChecksK` (`Phase6Checks.K.cs`, `Phase6Checks.K.Keys.cs`), what they compare against in
  `Phase6Expectations.K.cs` (nested class `PartK`, linked by the real-window pass) and its tests in
  `Phase6ExpectationTests.K.cs`. One test method per app start (`FreshApplication`), each on fresh
  copies; eight methods (table below). The real-window pass repeats K10's first method in one launch,
  `Phase6RealWindowPass.K.cs`, with `RealApp.Phase6.Palette.cs` (design 5.11, 17.2).
- **Defaults.** A method writes its session (and, on "Web", `workspaces.json`) on a fresh copy and
  starts the app at 1100×700 in the dark theme: `Phase6Checks.OpenAsync` for a repository tab (it waits
  until the graph has drawn its rows, the status is shown and the queue is idle), `OpenWorkspaceAsync`
  for "Web" with its dashboard as the only tab (it waits for the first read,
  `WaitForDashboardReadAsync`), `ShellDriver.StartWithTabs` for the methods with two tabs, then the same
  waits. No editor is chosen, the tool catalogues are the fixed ones (17.2), the system's theme the
  fake's dark, `"theme"` Dark. The record clock is frozen and the backup clock fixed (phase 5's
  conventions); a commit, stash or undo the app makes has D72's dates, 2026-01-02 09:00 UTC, and Test
  Author as author and committer, so its SHA is the same in every run and on both gits.
- **The neutral point** is (760, 687), in the status bar left of the Activity log toggle; both
  harnesses move the pointer there before every capture that is not about a hover or a menu (`Shot`).
  K12 makes no pointer event at all after `OpenAsync`'s `MoveToNeutral`: no press, release, wheel or
  move; its captures keep the pointer where it rests (`keepPointer: true`, at the neutral point).
- **Keys** are pressed as literal gestures, `ShellDriver.PressKey(Key, RawInputModifiers)` with
  Control, the headless platform's command modifier (0.7), never through `PressShortcut`, so that a
  wrong entry in `AppShortcuts` fails the check instead of being pressed for it (decision KD9). The
  checks write each key as the Windows and Linux words (`Ctrl+Shift+P`); the same literal texts are
  what they expect in the palette, the menus, the tooltips and `AcceleratorKey` (6.12). The macOS words
  (`Cmd+Shift+P`, `Option+Up`) are `ShortcutTextTests`' and `PaletteEntriesTests`' (5.12, 6.12).
  "Type `x`" is `ShellDriver.Type` (one text input in the scripted run; key by key in the real window).
- **The palette** is read with `PaletteSnapshot()` (5.11): rows numbered from 0 at the top of the
  list as `Shown` orders them, "row k" centred at y 157 + 28 k ±2 at 1100×700, the card 56 + 28 × rows
  high (84 to 392) from y 96, x 250 to 850, the box x 259 to 841, y 105 to 137. A row is written
  `label` · detail · `key`: " · " separates the parts a row draws; "(highlighted)" the entry on
  `VcSelectionBrush`, which is `Highlighted`, `HighlightedIndex` and UIA's selected item;
  "(danger)" an enabled destructive entry, its label in `VcDangerBrush`; "(disabled: "reason")" a
  disabled entry, its label, key and the reason in the detail's place in `VcTextDisabledBrush`, the
  reason whole in the row's tooltip and its UIA `HelpText`, its `ItemStatus` still the detail (5.7).
  The palette's own pixels are 5.3's: card `VcPanelBackgroundBrush` with a 1-px `VcBorderBrush` border,
  corner 8, the menus' shadow; labels at 12.5 in `VcTextPrimaryBrush` from x 269 (the row's padding);
  details and keys at 11.5 in `VcTextSecondaryBrush`, the detail 8 after the label, the key
  right-aligned at x 831 ±2; no dimming: around the card the window shows as it did before.
- **Menus** are written as phase 5's (header first, items in order, "—" a separator, "(danger)",
  "(disabled: "…")", "(highlighted)"), with " [key]" after a keyed item: its `MenuItemKey` text
  (automation id `ContextMenuItemKey`, a plain `TextBlock` at 12.5, right-aligned 10 before the item's
  right edge ±2 and at least 24 right of its label, `VcTextSecondaryBrush`, `VcTextDisabledBrush` on a
  disabled item), which is also the item's `AutomationProperties.AcceleratorKey`; an item without
  " [key]" has neither. A menu is 8 + 24 + 26 × items + 9 × separators high (±2) and as wide as its
  widest item, a keyed one counting 10 + label + 24 + key + 10 (6.7); the widths below are 0.8's measure
  (Inter's advance widths at 12.5 without kerning, within a pixel or two of what Avalonia draws) and are
  asserted ±3 from the bounds. `PART_InputGestureText` stays hidden.
- **Tooltips** are asserted three ways, enabled or not: `ToolTip.GetTip` (the tooltip, null for none),
  `AutomationProperties.HelpText` (the same text, 6.8) and `AutomationProperties.AcceleratorKey` (the
  key text, set enabled or not, none on another tab's close button). The resolver's five buttons keep
  their fixed texts while disabled (6.8: read with `x:Static`, whatever the state), which do not show
  there (`ToolTip.GetShowOnDisabled` false). "Rest the pointer on X" is `MoveMouse(X)` and a wait
  until its tooltip is open (`ToolTip.GetIsOpen`), so it is written only where a tooltip must open;
  its capture keeps the pointer there. "Move the pointer onto X" is `MoveMouse(X)` alone, with no
  wait, where no tooltip may open. Tooltips show in the app's colours (11.12): `#1A1D24` background,
  `#E4E7EC` text, `#323845` border in the dark theme.
- **The focus** is asserted as the focused element (`TopLevel.FocusManager.GetFocusedElement()`, by its
  automation id, or the row it lies in) and its region (`ShellDriver.FocusedRegion()`, 6.6's
  `KeyRegion.Role`: Left, GraphArea, Right, ActivityLog). F6 and Shift+F6 move it with
  `NavigationMethod.Tab`, so the keyboard focus frame shows: Fluent's own frame round a control without
  a focus look of its own, a focused text box's border in `TextControlBorderBrushFocused` (`#7C8CFF`),
  an activity log row's own `:focus-visible` look (decision KD17); the pictures are inspected for which
  element shows it.
- **Waits** are on conditions, never on a fixed time: the queue's log line `<repo>: <Title>:
  <outcome> after <N> ms: <message>` for an operation (`WaitForOperationAsync`), then `WaitForIdleAsync`;
  for a stage-panel write or a commit (the stage panel's `Stage` and `Commit`, whose results the status bar does not show), the status read with the counts the step
  names (`WaitForStatusReadAsync`) and the branch's new SHA in the refs; `WaitForDashboardReadAsync`
  (2.10's pass line, no row `Reading…`) and `WaitForBulkRunAsync` on a dashboard, and, after an
  activation whose reads 2.10's 10 s rule decides on the session's real clock (only W8 gives a manual
  one, 17.2), "until no row of the dashboard reads" (`DashboardSnapshot()`'s `IsPassRunning` false and
  no row `IsReading`, part W's WD37: true at once when the activation started no read); a palette or a menu
  is there once the press has been handled (`Settle`). A change outside the app (K11 step 3) is
  followed by a wait for the status read with `OutsideChangeTimeout`.
- **The log**: the palette's lines are Debug lines of the app log (14.14, 5.9): `{repo}: Command palette
  opened` on each opening (none for Ctrl+Shift+P while the palette is open) and `{repo}: Command palette
  ran: {label}` with ` ({detail})` when the entry has a detail, the `{repo}: ` prefix only while the
  active tab has a repository; the menu layer's `{repo}: Context menu opened: {header} by pointer` or
  `by keyboard` (D106). The checks read them through `ShellDriver.Log`, the real window from the log
  file.
- **The call log** (`CallsSince(app, mark)`, `AssertCalledInOrder`) names git's commands as
  `CommandText` renders them. "No write" means no call that is not a read (`GIT_OPTIONAL_LOCKS=0`
  calls are reads); the palette's opening starts one read, `git worktree list --porcelain` (5.5).
- **Git's own texts** below are Git 2.36.0.windows.1's and asserted word for word only when `git
  --version` is 2.36; on another git a check asserts the line marked "key" and git's state. Every one
  was replayed with Git 2.55.0.windows.5 and came out the same (the commit's and the stash's SHAs
  too). Git's states (HEAD, refs, `git status`, the stash list, HEAD's reflog, the remote's refs) are
  asserted on every version.
- **Pictures** are named `k<check><step letter>-<words>` (`PictureName()`'s rule), 17 in all: the
  scripted run's under `artifacts/visual/phase-6/scripted/`, the real window's four K10 twins under
  `…/real-window/`. Pictures with a focused text box vary by nature at its caret (on or off when
  captured, as phase 5's `a4a`): `k10a`, `k10b`, `k10d`, `k10e`, `k10f`, `k11d`; the resolver's caret is
  held still (D151). `k10e` also varies at the first one or two characters of the temporary folder's
  random part: a row without a key has no key column (the lead's ruling 4; K10's Notes).
- **The real window** repeats the steps its Notes mark RW, with the real mouse and keyboard (FlaUI), at
  1100×700 on the development machine (200 percent scaling, work area 1500×952); it reads texts through
  UI Automation and compares each capture with its scripted twin (at most 3 percent of pixels). It
  never acts outside the window and changes nothing on the desktop.

#### Test methods and app starts (part K)

| Check | Test method | Scenario and its setup | Size, theme | Git writes; pictures |
|---|---|---|---|---|
| K10 | `Check_K10_palette_search_and_run` | "ops" (`Scenarios.OperationsAsync()`), one tab; nothing before the start | 1100×700, dark | 1 (a checkout); `k10a` to `k10d`; RW |
| K10 | `Check_K10_palette_opens_repositories_and_workspaces` | "Web" (`Scenarios.WorkspaceAsync()`) through `OpenWorkspaceAsync`: `workspaces.json` with "Web" (`copy.ToWorkspace()`), the session's only tab its dashboard; an empty recent list | 1100×700, dark | 0 (a tab opens); `k10e`, `k10f` |
| K11 | `Check_K11_keys_in_menus_and_tooltips` | "ops", one tab; `src/core.txt` written outside the app at step 3 | 1100×700, dark | 1 (Stage all); `k11a` to `k11d` |
| K12 | `Check_K12_tabs_regions_and_the_commit_by_keys` | "ops", one tab, with `src/core.txt` "core\nmain work\nkeys\n" written before the start; a copy of phase 1's "graph" (`Scenarios.GraphAsync()`) made before the start, not open, its folder queued in `ShellDriver.Folders` before step 2 | 1100×700, dark | 4 (Stage all, commit, undo, redo) and an opening; `k12a` to `k12d` |
| K12 | `Check_K12_menus_remotes_stash_and_dialogs_by_keys` | "ops", one tab; nothing before the start | 1100×700, dark | 6 (push, fetch, pull, push, pop, stash); `k12e` |
| K13 | `Check_K13_keys_in_the_conflict_resolver` | "conflicts" stopped in a merge (`ConflictsStoppedAsync(ConflictsStop.Merge)`) and an "ops" copy, as two tabs, the first active (`StartWithTabs(data, [conflicts, ops], 0)`) | 1100×700, dark | 0; `k13a` |
| K13 | `Check_K13_fetch_all_and_next_dashboard_by_keys` | "Web" and a second workspace "Tools" (id `tools`, its repositories `{root}\infra` and `{root}\scratch`), both in `workspaces.json` (`WriteWorkspaces`); the session's tabs the two dashboards (`TabState(null, "web")`, `TabState(null, "tools")`), Web active | 1100×700, dark | 8 fetches in one Fetch all; `k13b` |
| K13 | `Check_K13_rows_by_keys_on_a_dashboard` | the same on fresh copies and a fresh data folder | 1100×700, dark | 0 (`workspaces.json` written twice, a tab opens); no picture |

K12 is two methods so that neither runs more than six operations, phase 5's bound for staying far
from the hang watchdog's 3 minutes. K13's dashboard is two methods for the same reason counted in git
processes: as one method its Fetch all (eight fetch operations, four at once), two passes over nine
rows (the start's, F5's) and the activations' reads made about 220 processes, the count at which part
W splits W5 (its WD36); each of the two is about as heavy as part W's W4 (decision KD5).

#### Scenarios and graphs of part K

Beyond scenario facts 2 ("workspace") and 6 ("ops", "conflicts", "graph"), which these graphs start
from. Rows from 0 at the top as `SelectedIndex` counts commit and stash rows; with the working-changes
row it is display row 0 and commit row r is display row r + 1. Every author Test Author.

**OPS0** ("ops" as built, scenario facts 6): 8 rows, 3 lanes, Graph column 64, Message x 454 (126
wide), Date x 580 to 700, a row clicked in its Message column at x 484, row r's centre y 129 + 26 r:

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `stash@{0}` | 0, 0 (ring) | On main: Experiment | 2026-01-01 12:07 | `0e95aca` |
| 1 | `feature/conflict` | 1, 1 | Conflicting change | 12:06 | `078ef05` |
| 2 | `feature/ff` | 2, 2 | Fast-forward me | 12:05 | `d93814d` |
| 3 | `main` ✓, `origin/main`, `v1.1` | 0, 0 (lane 2 ends here) | Main work | 12:04 | `88d6b91` |
| 4 | `feature/alpha`, `origin/feature/alpha` | 2, 3 | Alpha two | 12:03 | `ce5ea6a` |
| 5 | | 2, 3 | Alpha one | 12:02 | `dff8fbf` |
| 6 | `v1.0` | 0, 0 (lanes 1 and 2 end here) | Add core | 12:01 | `8beca34` |
| 7 | | 0, 0 | Initial commit | 12:00 | `4ef1f7c` |

Lane 0 dashed from row 0's ring through rows 1 and 2 into row 3's node, then solid to row 7. Full SHAs:
Main work `88d6b91e821a43cd39c94aa7892c8095b628caf8`, Alpha two
`ce5ea6ab16759bede02ce706dd5c6865443c2051`, the stash `0e95aca13f7975ef4a1fe029a8b5e9497f31028d`.
HEAD on a clean `main` tracking `origin/main` (+0 −0); `feature/alpha` tracks `origin/feature/alpha`
(+0 −0); `feature/conflict` and `feature/ff` track nothing; tags `v1.0` (annotated) and `v1.1`;
remote `origin` (a bare `origin.git` beside the working tree). Left panel: Local branches 4 (the
`feature` folder closed, holding `alpha`, `conflict`, `ff`; `main` in the accent colour), Remotes 2
(`origin` open, its `feature` folder closed, `main`), Pull requests 0, Tags 2, Stashes 1. Its rows end
at y 324, so (484, 360) lies in the empty graph area.

**K-OPSw**: OPS0 under the working-changes row (` M src/core.txt`, K11 after step 3, K12 part A at its
start). Display row 0 "Working changes" (centre y 129): its ring in lane 0, colour 0 (Main work's lane
and colour), its dashed line down lane 0 under the stash's ring into Main work's node; OPS0's rows one
lower (commit row r's centre y 155 + 26 r).

**K-OPSa** (K10 after step 4): OPS0's rows, lanes and lines with two labels changed: row 3 `main`,
`origin/main`, `v1.1` (no ✓), row 4 `feature/alpha` ✓, `origin/feature/alpha` (laid out by the app's
`GraphLayout`: the same lanes and colours as OPS0). Left panel: the Local branches' `feature` folder
open (it now holds HEAD's branch, `LeftPanelViewModel`'s default for a folder nobody toggled), `alpha`
in the accent colour, `main` not. Status bar `feature/alpha`. Git: `## feature/alpha...origin/feature/alpha`,
clean, the stash kept.

**K-OPSk** (K12 part A after its commit, and again after its redo): `git commit --file=- --cleanup=whitespace`
of the staged `src/core.txt` "core\nmain work\nkeys\n" with the input `Keys only` gives
`9522dae2b1c72b6c45e5dadcf572af1e26798d7f` (tree `a48324397cdc5097f091a20ef00043726eca3e86`, parent
`88d6b91…`, Test Author 2026-01-02 09:00 +0000 author and committer; stdout `[main 9522dae] Keys only` /
` 1 file changed, 1 insertion(+)`; the same SHA on 2.55); `## main...origin/main [ahead 1]`, clean. 9
rows, 4 lanes, Graph column 80, Message x 470 (110 wide), clicks at x 500:

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Keys only | 2026-01-02 09:00 | `9522dae` |
| 1 | `stash@{0}` | 1, 1 (ring) | On main: Experiment | 2026-01-01 12:07 | `0e95aca` |
| 2 | `feature/conflict` | 2, 2 | Conflicting change | 12:06 | `078ef05` |
| 3 | `feature/ff` | 3, 3 | Fast-forward me | 12:05 | `d93814d` |
| 4 | `origin/main`, `v1.1` | 0, 0 (lanes 1 and 3 end here) | Main work | 12:04 | `88d6b91` |
| 5 | `feature/alpha`, `origin/feature/alpha` | 1, 4 | Alpha two | 12:03 | `ce5ea6a` |
| 6 | | 1, 4 | Alpha one | 12:02 | `dff8fbf` |
| 7 | `v1.0` | 0, 0 (lanes 1 and 2 end here) | Add core | 12:01 | `8beca34` |
| 8 | | 0, 0 | Initial commit | 12:00 | `4ef1f7c` |

Lines: lane 0 (colour 0) from row 0's node to row 8; lane 1 (colour 1) **dashed** from row 1's ring
through rows 2 and 3 into row 4's node; lane 2 (colour 2) from row 2's node through rows 3 to 6 into
row 7's node; lane 3 (colour 3) from row 3's node into row 4's node; lane 1 again (colour 4) from row 5's
node through row 6 into row 7's node. The commit's details: `Keys only`, `src/core.txt` (M), shown in
the flat list as `core.txt` with its folder `src`. Its diff of `src/core.txt`: one hunk `@@ -1,2 +1,3
@@`, ` core`, ` main work`, `+keys`. The file's history from `9522dae` (`git log --follow`, phase 5's
reader): Keys only `9522dae` 2026-01-02 09:00 (M), Main work `88d6b91` 12:04 (M), Add core `8beca34`
12:01 (A); its blame at `9522dae`: line 1 `core` `8beca34` Add core, line 2 `main work` `88d6b91` Main
work, line 3 `keys` `9522dae` Keys only (one line per commit; 2.55 the same). After its undo (`git
update-ref` of `main` back to `88d6b91`, the index kept): K-OPSw with ` M` turned to `M  src/core.txt`
(staged), `9522dae` still in the object store.

**K-OPSp** (K12 part B after its pop): `git stash pop` of `stash@{0}` onto the clean `main`: stdout git's
status, ending `Dropped stash@{0} (0e95aca13f7975ef4a1fe029a8b5e9497f31028d)`, exit 0; ` M README.md`
(`# Operations\n\nExperiment.\n`), `git stash list` empty. 7 rows, 3 lanes, under the working-changes
row: 0 `feature/conflict` (0, 0) Conflicting change; 1 `feature/ff` (1, 1) Fast-forward me; 2 `main` ✓,
`origin/main`, `v1.1` (1, 1) Main work; 3 `feature/alpha`, `origin/feature/alpha` (2, 2) Alpha two; 4
(2, 2) Alpha one; 5 `v1.0` (0, 0; lanes 1 and 2 end here) Add core; 6 Initial commit. Not pictured.

**K-OPSs** (K12 part B after its stash, and at its end): `git stash push --include-untracked --message
"Experiment again"` → stdout `Saved working directory and index state On main: Experiment again`,
exit 0; `stash@{0}: On main: Experiment again`, `9b4f935718ae99ea51d393ae2290bb5f93f7c0ce` (parents
`88d6b91…`, `a5da8933403f8d7f11267650c6c418280b2a9857`, `86c954de3f30b0e476c7829f31a2ebc41a8a4eb7`: git
2.36 and 2.55 make the untracked parent with no untracked file too), dated 2026-01-02 09:00; clean.
OPS0's rows, lanes and lines with row 0 the new stash `9b4f935` "On main: Experiment again" (ring,
lane 0, colour 0, dashed into Main work's node) dated 2026-01-02 09:00. The Commits search `alpha`
(phase 5 design 3.2: `git log --no-walk=unsorted --stdin … --format=%H -F --regexp-ignore-case
--grep=alpha` over the eight rows' SHAs, and the authors and SHAs in memory) matches row 4 Alpha two
`ce5ea6a` and row 5 Alpha one `dff8fbf` and nothing else (every author `Test Author
<author@example.com>`; `alpha` is no hex prefix); 2.55 the same.

**The pushes, the fetch and the pull of K12 part B** (each exit 0, nothing changes): `git push
--progress origin refs/heads/main:refs/heads/main` → stderr `Everything up-to-date` (key; 2.55 the
same), `origin.git`'s `main` still `88d6b91`; `git fetch --progress origin` prints nothing (nothing
new); the pull's `git fetch --progress origin`, then `git merge --no-edit --no-overwrite-ignore
FETCH_HEAD` → stdout `Already up to date.` (key).

**GR0** (phase 1's "graph", scenario facts 6): 13 rows, 3 lanes; HEAD on `main` (`47e6ec7` Add
settings page, ↑1 ↓1), clean. Opened by K12's Ctrl+O; not otherwise asserted.

**GC-M, FM and R1** (K13's resolver; phase 4's report, "The conflicted files, the panes and the
results", as phase 5's A4 method 2 uses them): `git merge feature/theirs` stopped on `main` `da31c97`,
`UU notes.txt` (on disk blob `cdd7b39`), `MERGE_HEAD` `5c77ffb…`; the banner; display row 0 "Working
changes · 1 conflict". FM: the resolver on `notes.txt`, 20 lines, two blocks, `2 conflicts left`, one
conflicted file (so `ResolverPreviousFile` and `ResolverNextFile` are disabled, `CanGoToOtherFile`
false, while `ResolverPreviousConflict` and `ResolverNextConflict` are enabled on a text body). R1: 16
lines, `1 conflict left`, after `Use ours` on block 1, which moves the keyboard focus into the result.
No `merge.tool`; Search disabled with `Close the conflict resolver first`; Undo and Redo disabled with
`Finish or abort the merge first`.

**K-WEBf** ("Web" after K13's Fetch all on the file remotes, scenario facts 2.3 and 2.5): the run
decides api, web-app, docs, mobile, design-system, billing, infra and scratch in line, leaves out notes
(`This repository has no remotes`) and old-site (no repository); each row runs `git fetch --progress
origin` (one remote, the last, so no `--no-auto-maintenance`, D109), exit 0; docs `origin/main`
`245b729` (↓ `4`, `4 behind`, `4 commits behind origin/main`), mobile `67a2660` (↑ `1` ↓ `3`, `1 ahead, 3
behind`, `1 commit ahead of and 3 behind origin/main`), design-system `948f7d1` (↓ `3`, `3 behind`,
`3 commits behind origin/main`), the other five unchanged. Log `Web: Fetch all: started for 8
repositories, 2 skipped`, then `Web: Fetch all: Fetched 8 repositories; 2 skipped`. Rows at 1100×700
(Repository · Branch · Ahead / behind · Changes · State · Operation):

| k | Repository | Branch | Ahead / behind | Changes | State | Operation |
|---|---|---|---|---|---|---|
| 0 | `api` | `main` | `Up to date` | `Clean` | | `Fetched from all remotes` |
| 1 | `web-app` | `main` | ↑ `2` | `Clean` | | `Fetched from all remotes` |
| 2 | `docs` | `main` | ↓ `4` | `Clean` | | `Fetched from all remotes` |
| 3 | `mobile` | `main` | ↑ `1` ↓ `3` | `Clean` | | `Fetched from all remotes` |
| 4 | `design-system` | `main` | ↓ `3` | `1 staged, 2 unstaged, 1 untracked` (primary) | | `Fetched from all remotes` |
| 5 | `billing` | `main` | `Up to date` | `1 conflict` (danger) | `Merge in progress` (warning) | `Fetched from all remotes` |
| 6 | `infra` | `Detached at 09d320a` | | `Clean` | | `Fetched from all remotes` |
| 7 | `scratch` | `experiment` | `No upstream` | `Clean` | | `Fetched from all remotes` |
| 8 | `notes` | `main` | `No remote` | `Clean` | | `This repository has no remotes` (secondary; tooltip the same) |
| 9 | `old-site` | | | | `Folder not found` (warning) | |

`Fetched from all remotes` is secondary; a left-out row keeps its reason until the dashboard's next run
or the row's next operation (4.2), so notes' stays through the reads of K13 method 2's activations.
**K-WEBm** (after K13 method 3's Alt+Down and Delete; that method runs no fetch): the rows api,
web-app, mobile, docs, design-system, billing, infra, scratch, notes, in that order, each as scenario
facts 2.3 gives it; `workspaces.json`'s "Web" lists `{root}\api`, `{root}\web-app`, `{root}\mobile`,
`{root}\docs`, `{root}\design-system`, `{root}\billing`, `{root}\infra`, `{root}\scratch`,
`{root}\notes`; the header `9 repositories`.

**K-TOOLS** (the second workspace, "Tools", made by this part): `{ "id": "tools", "name": "Tools",
"repositories": [ {root}\infra, {root}\scratch ] }`. Its rows, once read: 0 `infra` · `Detached at
09d320a` · (empty) · `Clean`; 1 `scratch` · `experiment` · `No upstream` (`experiment has no upstream`)
· `Clean`. A repository in two workspaces has one summary-mode view model, held by both dashboards
(3.4's `AcquireSummaryAsync`), so after Web's Fetch all both rows show `Fetched from all remotes` in
their Operation cells too (decision KD12). Its toolbar: Fetch enabled (both rows have a remote), Pull
disabled `No repository of Tools can be pulled` (infra detached, scratch without an upstream).

**K-DOCS** (docs' tab opened from K13 method 3's dashboard, no fetch before it): scenario facts
2.4's docs, 4 rows, 1 lane: 0 `origin/main` Fix the guide's typos 12:03 `23e2ad1`; 1 Write the FAQ
12:02 `43723b6`; 2 Write the install guide 12:01 `245235b`; 3 `main` ✓ Initial commit 12:00 `ec855ba`.
Lane 0 rows 0 to 3. Left panel `main` ↓3; status bar `main`.

#### Texts these checks rely on (part K)

**The palette** (14.7, 5.3, 5.11): root `Command palette` (automation name only); box placeholder
`Search commands, branches, tags, stashes, remotes, repositories and workspaces`, its name `Search
commands`; list name `Commands`; `No matches` (`CommandPaletteEmpty`) while nothing matches (K10 step 3).
Commands' labels as 5.4 and 14.7 give them, among them `Pop the newest stash`, `Undo {text}` / `Undo`,
`Redo {text}` / `Redo`, `Search commits`, `Go to the commit message`, `Refresh`, `Filter branches and
tags`, `Fetch every repository of {workspace}`, `Pull every repository of {workspace}`, `New tab`,
`Close tab` (detail the tab's title), `Next tab`, `Previous tab`, `Go to the next dashboard` (detail the
workspace it goes to), `Open repository…`, `Clone repository…`, `Init repository…`, `Show the activity
log`, `Open settings…`, `Show keyboard shortcuts…`, `Choose an editor…`, `Use the light theme`, `Follow
the system's theme`, and the menus' `Fetch all remotes`, `Pull`, `Push`, `Create branch…`, `Stash
changes…`, `Show reflog`, `Show backups`, `Create tag at HEAD…`, `Add remote…`, `Manage remotes…`,
`Show all hidden branches`, `Stop soloing`, `New workspace…`, `Add repository…`, `Add open tabs`,
`Rename workspace…`, `Delete workspace…`. Items: the menus' labels with the menu's header as detail
(`Delete main on origin…` · `origin/main`, `Check out feature/alpha` · `feature/alpha`, `Create branch
here…` · `88d6b91 Main work`), `Open repository {name}` · its folder, `Open workspace {name}` · `{N}
repositories`. Disabled reasons (14.5 and the menus'): `There are no changes to stash`, `There are no
stashes`, `Nothing to undo`, `Nothing to redo`, `There are no changes to commit`, `No branch is hidden`,
`No branch is soloed`, `There is only one tab`, `No tab has a repository open`, `Select a repository
to see its activity`, `You cannot delete the branch you are on`.

**Key texts** (14.12, 6.4), as literals: `Ctrl+Shift+P`, `Ctrl+,`, `Ctrl+J`, `F5`, `Ctrl+T`, `Ctrl+W`,
`Ctrl+Tab`, `Ctrl+Shift+Tab`, `Ctrl+Shift+D`, `Ctrl+O`, `Ctrl+Shift+C`, `Ctrl+Shift+I`, `Ctrl+Shift+F`,
`Ctrl+Shift+L`, `Ctrl+Shift+K`, `Ctrl+B`, `Ctrl+Shift+S`, `Ctrl+Shift+O`, `Ctrl+Z`, `Ctrl+Y`, `Ctrl+F`,
`Ctrl+Shift+R`, `Ctrl+Shift+A`, `Ctrl+Shift+X`, `Ctrl+Shift+M`, `Ctrl+Shift+E`, `Ctrl+Enter`, `Alt+Up`,
`Alt+Down`, `Alt+Left`, `Alt+Right`, `Esc`.

**Tooltips and help texts** (14.11, 6.8): `Fetch all remotes ({key})`, `Pull from {up} ({key})`, `Push
{b} to {up} ({key})`, `Create a branch at HEAD ({key})`, `Stash your changes ({key})`, `Pop the newest
stash ({key})`, `Undo {text} ({key})`, `Redo {text} ({key})`, `Search commits ({key})`, `Open settings
({key})`, `Switch to the light theme` (no key), `Close this tab ({key})` / `Close this tab`, `Open,
clone or init a repository ({key})`, `Show every git command the app has run ({key})`, `Hide the
activity log ({key})`, `Open a repository on this computer ({key})`, `Clone a repository from a URL
({key})`, `Create a new repository in a folder ({key})`, the commit button's `{its text} ({key})`
(`Commit 1 file (Ctrl+Enter)`), `Previous conflict ({key})`, `Next conflict ({key})`, `Previous file ({key})`,
`Next file ({key})` (kept while disabled, not shown there), `Close (Esc)`;
on a dashboard `Fetch every repository of {workspace} ({key})`, `Pull every repository of {workspace}
({key})`, `Read the repositories of {workspace} again ({key})` and the reason `No repository of
{workspace} can be pulled`. A disabled button keeps its reason alone (`There are no changes to stash`,
`Nothing to undo`, `Nothing to redo`).

**Log lines** (14.14, Debug): `{repo}: Command palette opened`; `{repo}: Command palette ran: {label}`
with ` ({detail})`; the `{repo}: ` prefix only while the active tab has a repository; the menu layer's
`{repo}: Context menu opened: {header} by pointer` / `by keyboard`; the dashboard's `{workspace}: read
{n} repositories in {ms} ms` (Info) and `{workspace}: Fetch all: started for {n} repositories, {s}
skipped` / `{workspace}: Fetch all: {result}` (Info).

**Earlier phases' texts that stay** (phases 2, 3 and 5): operations `Check out {name}` (`Checking out
{name}…`, `Checked out {b}`), `Fetch` (`Fetching from all remotes…`, `Fetched from all remotes`), `Pull`
(`Pulling {up} into {b}…`, `{b} is already up to date`), `Push {b}` (`Pushing {b} to {target}…`,
`Pushed {b} to {target}`), `Pop stash@{n}` (`Popping stash@{n}…`, `Popped stash@{n}`), `Stash`
(`Stashing the changes…`, `Stashed the changes`), `Undo {text}` / `Redo {text}` (`Undid {text}`, `Redid
{text}`) with the entry texts `checkout of {name}`, `commit "{subject}"`, `pop of stash "{message}"` and
`stash "{message}"`, where `{message}` is in both the stash list's message, never the text typed
(`On main: Experiment`, `On main: Experiment again`; `RepositoryOperations.Undo.cs`'s `TextOf`, phase
5's U20); dialogs `Create branch` (`From {short} "{subject}"`,
`Branch name`, `Check out the new branch`, `Cancel`, `Create branch`) and `Stash changes` (`Message
(optional)`, `Include untracked files`, `Cancel`, `Stash`); the stage panel's `Unstaged files ({n})`,
`Staged files ({n})`, `No staged changes`, `Stage files to commit`, `Commit 1 file`; the stage panel's
operations `Stage` and `Commit` (no message), the activity log's `Activity log`, `Show reads`; the
search bar's `Commits`, `{n} matches`, `{k} of {n}`; the pages' `History of {name}` (its folder after
it) and `Blame of {name}` with `at {short} {subject}`; the dashboard's texts of 14.3 as scenario facts
2.3 and 2.5 give them.

**Git's texts** (Git 2.36.0.windows.1; 2.55.0.windows.5 identical): `git switch --no-guess
--no-overwrite-ignore feature/alpha` → stdout `Your branch is up to date with 'origin/feature/alpha'.`,
stderr `Switched to branch 'feature/alpha'` (key); `git commit` → `[main 9522dae] Keys only` (key) / ` 1
file changed, 1 insertion(+)`; `git push --progress origin refs/heads/main:refs/heads/main` → stderr
`Everything up-to-date` (key); `git merge --no-edit --no-overwrite-ignore FETCH_HEAD` → `Already up to
date.` (key); the pop's output ending `Dropped stash@{0} (0e95aca13f7975ef4a1fe029a8b5e9497f31028d)`
(key); `git stash push --include-untracked --message "Experiment again"` → `Saved working directory and
index state On main: Experiment again` (key); `git fetch --progress origin` on "ops" prints nothing;
K-OPSk's history and blame and K-OPSs' search as those graphs give them.

### Part S: the settings screen and its effects

Part S covers the settings screen (design 7.1 to 7.10, D197, D200, D216), git's own settings behind it
(section 8: the identity, the pull modes and the merge tool at both scopes, D190, D217 to D219, D202),
the theme that follows the system (11.1 to 11.8, D194, D223, D224), Open in editor and Open file's
question (section 12, D226) and the switch "Keep ignored files" turned on and off (9.1, D193, D221):
checks S14 to S21 of design 17.4. The screen itself, its four sections, its entries and Cancel (S14);
the System theme followed live, left by a click, chosen in the palette and the screen, and kept across
a restart (S15); auto-fetch's minutes and "Include untracked files when stashing" (S16); Open file's
extensions and the editor (S17); the identity, global and per repository, with the identity git will
really use (S18); the pull modes with the per-key precedence, `branch.<b>.rebase` and an unusual value
(S19); the merge tool chosen from the list git knows (S20); and Keep ignored files on, then off, with
D179's checkout kept (S21). The operations Keep ignored files covers are part G's (G22 to G24); the
sweep's pictures of the screen in both themes (`l31d`, `l31e`, L34) are part L's. This part was written
before the UI it tests, from `docs/phase-6-design.md` sections 7, 8, 11.1 to 11.8, 12, 14, 15.5, 16.1
and 17, decisions D187 to D231, `plan.md`'s phase 6 and the scenario facts. Every git state below was
derived with Git 2.36.0.windows.1 and every git text replayed with Git 2.55.0.windows.5 (Visual
Studio's, its `mingw64/bin` first on `PATH`), on copies of "settings", "ignored" and "conflicts" made
by the merged scenario code (see "How the expected git states were derived", part S).

#### Conventions of part S

- **Project, class and test methods.** The checks are in `tests/VisualCommit.VisualTests.Phase6`,
  class `Phase6ChecksS` (`Phase6Checks.S.cs` and `Phase6Checks.S.<topic>.cs`), with what they compare
  against in `Phase6Expectations.S.cs` (nested class `PartS`, which the real-window pass links) and
  its tests in `Phase6ExpectationTests.S.cs`. One test method per app start (phase 3's rule,
  `FreshApplication`), each on a fresh copy of its scenario and a fresh data folder, but S15's restart
  (a plain `[Fact]` with two `FreshApplication.RunAsync` halves on one data folder, as phase 0's check
  6 and phase 5's A3): thirteen methods, fourteen app starts (the next table, SD1). The real-window pass
  repeats S14 and S15 in two launches (`Phase6RealWindowPass.S.cs`, `RealApp.Settings.cs`).
- **Defaults.** Unless a check says otherwise, a method writes a session with one tab on a fresh copy
  of the named scenario, starts the app at 1100×700 in the dark theme (`Phase6Checks.OpenAsync(…,
  AppTheme.Dark, systemDark: true)`), and waits until the graph has drawn its rows, the status is
  shown and the queue is idle; the pointer then rests at the part's rest point. Every start has what
  17.2 gives every session: the frozen record clock and the fixed backup clock (the first backup of a
  session is named `…-20260102T090001000Z`, design 2.3 of phase 5); the fixed tool catalogues (12.9:
  editors `vscode` Visual Studio Code, `sublime` Sublime Text and `idea` IntelliJ IDEA under
  `C:\Tools\…`, or `/opt/tools/…` off Windows; merge tools found `kdiff3`, `meld`, `vimdiff`, not
  found `araxis`, `bc`, `p4merge`, `winmerge`, without descriptions, there at once; `PATHEXT`
  `.COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC` on Windows, none elsewhere); the fake
  system theme, dark unless the check says `systemDark: false` (11.3); and the session's own git
  configuration: `GIT_CONFIG_GLOBAL` = `{data}\harness\global.gitconfig` (absent until a check seeds
  it or the app writes it) and `XDG_CONFIG_HOME` = `{data}\harness\xdg` (empty), `GIT_CONFIG_NOSYSTEM=1`
  from the test process, so no scope but `global`, `local` and `command` can hold a value, and
  `VISUALCOMMIT_AUTO_FETCH_MINUTES=0` (D112, D220). `{data}` is the method's data folder
  (`TempDirectory("data")` under `%TEMP%\VisualCommit.Tests`), `{root}` the scenario copy's folder
  (`TempRepo`'s root: `%TEMP%\VisualCommit.Tests\<scenario>-<12 hex>`), so `{root}\settings` is the
  "settings" tab's working tree.
- **"Before the app starts"** is a file write or a git call on the copy as a terminal makes it
  (`TempRepo.GitAsync`, `TempRepo.ConfigAsync`), files written with `\n` line ends; the session's
  global file is seeded with `ShellDriver.SeedGlobalGitConfig(data, text)` (written as given, UTF-8
  without a byte order mark, `\n` line ends, `⇥` below standing for a tab); `settings.json` is written
  as given into `{data}`. **"Outside the app"** while it runs is the same; a change to a tracked file
  is then followed by phase 5's `WaitForStatusReadAsync(app, staged, unstaged, mark)`. So is a change
  to `.git/config` (`WaitForStatusReadAsync(app, 0, 0, mark)` on S19's clean copy), followed by the
  queue idle, before the next action: the watcher leaves out only `objects/`, lock files and the
  file-system monitor's files (`RepositoryWatcher.IsRelevant`), so such a write starts a refresh
  after the quiet period, whose refs and status reads must end before the screen opens, neither
  mixing with the screen's reads in `CallsSince` nor changing what shows behind the backdrop.
- **The settings screen** is driven through `ShellDriver.Phase6.Settings.cs` (17.2), every action a
  click or key on the screen's own controls: `OpenSettingsAsync(how)` (a click on `SettingsButton`,
  Ctrl+, , or the palette's entry) waits until `SettingsDialog` shows and its reads (SD29) have ended
  (`GitReading` and `EditorStatus` hidden, the fields filled); a section is shown by a click on its
  `SettingsNav…` button; radios and check boxes are clicked; a box is focused, its text selected and
  replaced by typing; `SaveSettingsAsync()` clicks `Save` (`ConfirmButton`) and waits for its
  validation, then, for a save that closes the screen, until it has closed and the read back (8.4) has
  ended and, when the repository's keys were written, for the queue's log line `settings: Save
  repository settings: Succeeded after <N> ms: Saved the repository's settings`; a refused Save waits
  only for its validation (the screen open, `SettingsSaving` empty). `SettingsSnapshot()` reads each
  field's text, placeholder, check state, enabled state and tooltip, and every notice line shown.
- **The screen's reads** are 8.1's, 8.6's and 8.7's (SD29): the outside read `git config
  --show-scope --show-origin -z --list` with the call log's `WorkingDirectory` the data folder; with a
  repository's tab active, in its working tree, the repository read (the same command), `git var
  GIT_AUTHOR_IDENT` and `git var GIT_COMMITTER_IDENT`, and, HEAD being on a branch, Pull's reader
  (`ReadPullConfigurationAsync`, 8.9), in its own order: `git config --get pull.ff`; `git config
  --type=bool --get pull.ff` when that value is set and none of `true`, `yes`, `on`, `false`, `no`,
  `off` in any case (`only`: exit 128); `git config --get branch.<b>.rebase`, and when it exits 1 `git
  config --get pull.rebase`; `git config --type=bool --get` of the key that answered when its value is
  none of those six words (`preserve`: exit 128). With no pull setting that is three reads, each exit
  1, so an opening on a repository's tab makes seven processes (eight with `pull.ff = only`, nine with
  an invalid `pull.rebase` beside it) and one on an empty tab. All of them start when the screen opens,
  whatever section it opens on, and again in the read back after every Save (7.8 step 6); a scope
  switch, a section change or a click on a radio reads nothing. They run side by side, so a check
  asserts them as a set, Pull's reader's own in its order. "No write" means no `git config` call with
  `--replace-all` or `--unset-all` since the step began. A check reads the calls with
  `CallsSince(sequence, worktree)` (17.2), taking `sequence` right before the step's first action,
  written as the call log's `CommandText` renders them (an argument with spaces in double quotes). No
  method of this part makes a `git mergetool` call (the fixed catalogue answers the merge-tool list,
  12.9) or starts a program (the fake launcher records, 12.4).
- **An operation's calls "in this order"** (S19's pulls and saves) are the ordered subsequence of the
  operation's calls whose `CommandText` is one the list names, or one a "no …" beside it names, matched
  as written (SHAs and refs as given); the operation's other calls come between them and are not
  asserted there: before a pull's configuration reads the refs read of `RefuseIfPullTargetMovedAsync`
  (which runs its own `git config --null --get-regexp ^remote\.`), after the fetch D124's second HEAD
  check and the HEAD reads, the rebase's own reads (9.6's four, side by side, among them a `git
  merge-base --is-ancestor` and a `git diff-tree` of other arguments; the fork point and its counts;
  `rebase.autoStash`), and the undo recording's reads (`git config --local --null --list` among them)
  and the refresh after it.
- **Git's configuration files** (the session's global file and `.git/config`) are asserted byte for
  byte where a step names their contents; `settings.json` is asserted by its parsed properties
  (`JsonSettingsStore` writes every property, indented, after any change), and by its bytes only where
  nothing may have written it. "settings.json does not exist" means the app wrote no settings.
- **The rest point.** Before every capture with no menu open and no tooltip asked for, the pointer is
  moved to (760, 687) at 1100×700 and to (760, 1000) at 1920×1080: in the status bar, or on the
  dialog layer's backdrop below every card, where nothing has a tooltip (SD2; phase 5's neutral point
  (760, 687) lies inside the settings card at 1920×1080). A capture with a menu open leaves the
  pointer where the right-click that opened it was (phase 5's rule).
- **Positions** are taken from the controls' bounds (D122's rule). The settings card is asserted
  exactly (7.1: "the scripted checks assert the card's bounds at 1100×700 and 1920×1080 exactly");
  every other number given here is what 7.1's layout gives with the default panel widths, asserted ±2
  where it is stated, "about" where 7.1 estimates. Graph rows: as the scenario facts give them (the
  working-changes row at display row 0 when there are changes; a row clicked "in its Message column",
  x 468 with one or two lanes, x 484 with three, x 516 with five).
- **Dialogs over the screen** stack on the dialog layer (D104): the error dialog is asserted with
  `AssertErrorDialogAsync` (title, `ErrorSummary`, `ErrorRepository` "In {repository}",
  `ErrorCommand`, buttons left to right, `ErrorOutput`); the Pick dialog by `PickDialog`, `PickTitle`,
  `PickFilter`, its `PickItem`s (each named by its option's text, its help text the option's detail,
  7.7), the selected item and its buttons; the question by its title, text and buttons.
- **The toolbar and the status bar** are as part K and phase 5's part A give them for an open
  repository; this part asserts `SettingsButton`, `ThemeSwitch`, `PullButton` and `StashButton` where
  they are its subject, and the status bar's operation text (`OperationStatus`, its tone) after each
  operation. Undo is asserted by its tooltip (`Undo {text} (Ctrl+Z)` or `Nothing to undo`) where a
  step names it.
- **Git's own texts** quoted below (the call log's output, `ErrorOutput`) are Git 2.36.0.windows.1's
  and are asserted word for word only when `git --version` is 2.36
  (`Phase6Expectations.IsReportGitAsync`); on any other git a check asserts the line marked "key", that
  `ErrorOutput` is `GitOutputCleaner.Clean` of the failed call's own output, and the dialog's title,
  summary and buttons (phase 3's rule). Where 2.55 prints otherwise the text says so. Git's states
  (refs, the files' bytes, `git status`, the configuration files, the backups) are asserted on every
  version; the SHAs below are the same on 2.36 and 2.55.
- **Other platforms.** CI's macOS and Linux jobs run the same methods: there the fixed editors are
  `/opt/tools/vscode/code`, `/opt/tools/sublime/subl` and `/opt/tools/idea/bin/idea`, no
  `RunnablePathExt` line shows (no `PATHEXT`), paths use `/`, and the editor's working directory is
  the user's home folder (12.4). Each step that differs says so.
- **Pictures** are named by `Phase6Checks.PictureName`'s rule,
  `^[gklsw]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`: `s`, the check's number, a step letter
  running on through the check's methods in order, then words (`s14a-general`, `s19g-branch-rebase-notice`).
  Every name is unique in the report; `Shot` refuses a name saved twice in a run (11.8). A picture whose
  content varies by nature says so in its check's Notes.
- **The real window** repeats S14's and S15's RW steps in a real process with the real mouse and
  keyboard (FlaUI), seeds the session's global file with the same helper (`RealApp` uses
  `ShellDriver.SeedGlobalGitConfig`, 17.2), sets `VISUALCOMMIT_TOOL_CATALOGUES=fixed`, reads texts
  through UI Automation (a tooltip as the control's `HelpText`, a box through its Value pattern), and
  saves each capture under its scripted twin's name in `artifacts/visual/phase-6/real-window/` with a
  difference picture and its share in `run.txt` (at most 3 %, D33), but S15's three captures, which
  have names of their own because their twin depends on the owner's app mode (11.8). It never changes
  the operating system's theme (D224) and never clicks Open file or Open in editor (12.10). 1920×1080
  is scripted only (it does not fit the screen at 200 %).

#### Test methods and app starts (part S)

| Check | Test method | Scenario and what is set before the start | Window and theme |
|---|---|---|---|
| S14 part A | `Check_S14_settings_screen_sections_and_entries` | "settings" (`Scenarios.GitSettingsAsync()`), a session of two tabs: tab 0 the copy (active), tab 1 empty; global file seeded `[user]` / `⇥useConfigOnly = true` | 1100×700, dark |
| S14 part B | `Check_S14_settings_screen_at_1920x1080` | "settings", one tab; the same seed | 1920×1080, dark |
| S15 part A | `Check_S15_system_theme_followed` | "graph" (`Scenarios.GraphAsync()`), one tab; `settings.json` `{ "theme": "System" }`; the fake system dark | 1100×700, System (dark, then light) |
| S15 part B | `Check_S15_system_theme_after_a_restart` (a `[Fact]`, two starts on one data folder) | "graph", one tab; no `settings.json`; first start with the fake dark, second with the fake light | 1100×700, dark, then System (light) |
| S16 | `Check_S16_auto_fetch_and_stash_settings` | "settings", one tab; `notes.txt` `one` `two` `three` (not staged) and `draft.txt` `draft` (untracked); `settings.json` `{ "autoFetchMinutes": 120, "stashIncludeUntracked": false }` | 1100×700, dark |
| S17 | `Check_S17_open_file_extensions_and_editor` | "settings", one tab; `notes.txt` `one` `two` `three` staged, `draft.txt` `draft` untracked | 1100×700, dark |
| S18 part A | `Check_S18_identity_global` | "settings", one tab; `notes.txt` `one` `two` `three` (not staged); global file seeded `[user]` / `⇥useConfigOnly = true` | 1100×700, dark |
| S18 part B | `Check_S18_identity_per_repository` | "settings", one tab; `notes.txt` `one` `two` `three` (not staged); global file seeded `[user]` / `⇥useConfigOnly = true` / `⇥name = Ann Example` / `⇥email = ann@example.com` (the file part A's step 4 writes) | 1100×700, dark |
| S19 part A | `Check_S19_pull_mode_ff_only_and_repository_rebase` | "settings", one tab; global file seeded `[user]` / `⇥name = Test Author` / `⇥email = author@example.com` / `[pull]` / `⇥ff = only` | 1100×700, dark |
| S19 part B | `Check_S19_pull_rebases_by_the_repository_setting` | "settings", one tab; the same seed; `.git/config` given `pull.rebase = true` and `pull.ff = true` (`git config pull.rebase true`, `git config pull.ff true`: the file part A's step 4 writes) | 1100×700, dark |
| S19 part C | `Check_S19_pull_mode_unset_branch_rebase_and_other` | "settings", one tab; the same seed and `.git/config` as part B | 1100×700, dark |
| S20 | `Check_S20_merge_tool_chosen_in_settings` | "conflicts" stopped in its merge (`Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`, phase 4's GC-M), one tab; global file seeded `[mergetool "slow"]` / `⇥cmd = true` | 1100×700, dark |
| S21 | `Check_S21_keep_ignored_files_on_and_off` | "ignored" (`Scenarios.IgnoredAsync()`, IG0), one tab | 1100×700, dark |

**Run time.** The git processes of each method, counted from the call lists of its steps with these
estimates (none of part S's UI has run): an app start with one repository's tab about 11 (git's
detection, `OpenAsync`'s, the refs read's five, 10.2, the status read and the graph's log; 17.6 counts
eight per dashboard row, without the graph); an opening of the screen on a repository's tab 7 to 9,
on an empty tab 1 (SD29); a Save its writes and the read back, which is an opening's reads; `Stage all`
and a commit with its undo recording and refresh about 22 (plan.md risk 25: a stash took 17 with its
refresh); the watcher's refresh after an outside write about 8; the pull refused by `pull.ff = only`
about 27 (the refs read, Pull's four reads, the fetch, the HEAD reads, the `diff-tree`, the merge, the
undo recording, the refresh); the rebasing pull about 42 (the same, with three reads, and 9.6's four
reads, the fork point and its two counts, the backup, `rebase.autoStash`, the rebase and the reads after
it); a pull refused before its fetch about 13; a refused checkout about 12, one that runs about 25.

| Method | Git processes (about) | Of which | Pictures |
|---|---|---|---|
| S14 part A | 47 | a start; five openings on the repository's tab, one on the empty tab; no Save that reaches git | 7 |
| S14 part B | 18 | a start, one opening | 2 |
| S15 part A | 11 | a start (the palette and the fake's flips call no git) | 3 |
| S15 part B | 36 | two starts, an opening and its read back | 2 |
| S16 | 60 | a start, four openings, three read backs | 4 |
| S17 | 43 | a start, two openings, two read backs, two file reads | 4 |
| S18 part A | 56 | a start, two openings, a global save, a commit | 3 |
| S18 part B | 56 | a start, two openings, a repository save, a commit | 3 |
| S19 part A | 70 | a start, three openings, the refused pull, a repository save | 5 |
| S19 part B | 53 | a start, the rebasing pull | 1 |
| S19 part C | 82 | a start, three openings, two repository saves, two outside writes and their refreshes, the pull refused before its fetch | 3 |
| S20 | 50 | a start with the merge stopped, the resolver's reads, two openings, two global saves each with its `merge.tool` read | 3 |
| S21 | 74 | a start, two refused checkouts, an opening and its read back, a checkout | 2 |

About 660 git processes in all. The longest, S19 part C and S21, take about 100 s at phase 5's 1.2 s per
git process under the default run's load (the laptop's `artifacts/visual/phase-5/timings/` of
2026-10-15 put phase 5's longest method, `Check_A2_activity_log_stop_cancel_abort`, at 95 s to its last
picture), so every method stays well under 2 minutes and under the hang watchdog's 3, which
counts each test's own time (`HangClock`). S18 and S19 are split for this (SD1): S18 as one method
made about 119 processes and S19's steps 1 to 6 as one about 123, some 145 s each, near the limit
under the gate's load. With phase 5's
4.8 s per picture the part's 42 pictures take about 3.4 minutes of the UI thread, spread over the
thirteen methods.

#### Scenarios and graphs of part S

**"settings"** (scenario facts 5): `{root}\settings` with its bare `{root}\remotes\settings.git`
(`remote.origin.url = ../remotes/settings.git`), no `user.*` in `.git/config`. The copy's
`.git/config`, as `LeanRepositories` writes it:

```
[core]
	repositoryformatversion = 0
	filemode = false
	bare = false
	logallrefupdates = true
	symlinks = false
	ignorecase = true
	autocrlf = false
[commit]
	gpgsign = false
[gc]
	auto = 0
[maintenance]
	auto = false
[remote "origin"]
	url = ../remotes/settings.git
	fetch = +refs/heads/*:refs/remotes/origin/*
[branch "main"]
	remote = origin
	merge = refs/heads/main
```

(On Linux and macOS the blank folder's `core` lines are those `git init` writes there; the checks
assert the lines below `[branch "main"]`'s and the absence of `[pull]`, `[user]` and `branch.main.rebase`
where a step names them, not the `core` lines.) No `FETCH_HEAD` in a fresh copy.

- **ST0** (as built): 2 lanes, Message x 438, clicks (468, 129 + 26 r): row 0 `main` ✓ (0, 0) Local
  work 2026-01-01 12:02 `73f8cad`; row 1 `origin/main` (1, 1) Remote work 12:01 `e0cbfaf`; row 2 (0, 0;
  lane 1 ends here) Initial commit 12:00 `9f9a014`. Lane 0 (colour 0) rows 0 to 2; lane 1 (colour 1)
  from row 1's node into row 2's node. `main` tracks `origin/main`, `+1 -1`, clean; the status bar's
  branch `main`; Pull enabled with `Pull from origin/main (Ctrl+Shift+L)`. Initial commit's tree:
  `README.md` `# Settings`, `notes.txt` `one` `two`, `run.py` `print("run")`.
- **ST0w** (S16, S17, both parts of S18: a change before the start): the working-changes row at display row 0
  (centre y 129; its ring in lane 0, colour 0, at (406, 129), its dashed line down lane 0 into Local
  work's node), then ST0's rows one lower: Local work at y 155, Remote work 181, Initial commit 207,
  clicked at x 468. S16 and S18 have ` M notes.txt` (S16 also `?? draft.txt`); S17 has `M  notes.txt`
  and `?? draft.txt`.
- **ST1** (S18 step 6, after `Add a third note`): 4 rows, 2 lanes: row 0 `main` ✓ (0, 0) Add a third
  note 2026-01-02 09:00 `3e519fb`; row 1 (0, 0) Local work 12:02 `73f8cad`; row 2 `origin/main` (1, 1)
  Remote work 12:01 `e0cbfaf`; row 3 (0, 0; lane 1 ends here) Initial commit 12:00 `9f9a014`. Lane 0
  rows 0 to 3; lane 1 from row 2's node into row 3's node. `+2 -1`, clean.
- **ST1r** (S18 part B step 9, after `Add a third note` made with the repository's identity): ST1's
  rows, lanes and lines, row 0 `main` ✓ (0, 0) Add a third note 2026-01-02 09:00 with the SHA
  `ead110c`. `+2 -1`, clean.
- **STR** (S19 step 6, after the rebasing pull): 3 rows, 1 lane (Graph column 48, Message x 438): row 0
  `main` ✓ (0, 0) Local work 2026-01-01 12:02 (its author date) `b4253bb`; row 1 `origin/main` (0, 0)
  Remote work 12:01 `e0cbfaf`; row 2 (0, 0) Initial commit 12:00 `9f9a014`. Lane 0 rows 0 to 2. `+1 -0`,
  clean.

**The commits part S makes** (D72's dates, 2026-01-02 09:00 UTC as author and committer date; the
identity from git's configuration; the same SHAs on 2.36 and 2.55):

| Step | Command (the app's, design 6.10 of phase 2) | Identity in effect | New `main` | Parent |
|---|---|---|---|---|
| S18 step 6 | `git commit --file=- --cleanup=whitespace`, standard input `Add a third note` | `Ann Example <ann@example.com>` (global) | `3e519fba64d22688690e5920cc3300e3c757e5d9` | `73f8cad` |
| S18 step 9 (part B, a fresh copy) | the same, `Add a third note` (the same tree, `d8e00c9`) | `Bo Local <bo@example.org>` (`.git/config`) | `ead110cb6ae86125d9789cf40d64fb1608f55612` | `73f8cad` |
| S19 step 6 | `git rebase` (the pull's, upstream `origin/main`) | `Test Author <author@example.com>` (global) as committer; Local work's author kept | `b4253bb077f7b07e212ac8f16861a728072f58b5` | `e0cbfaf` |

S19's rebase leaves HEAD's reflog `rebase (finish): returning to refs/heads/main`, `rebase (pick):
Local work`, `rebase (start): checkout refs/remotes/origin/main` (newest first).

**The identity reads** (8.6; in `{root}\settings`): with the seed `[user]` / `⇥useConfigOnly = true`
and nothing else, `git var GIT_AUTHOR_IDENT` and `git var GIT_COMMITTER_IDENT` each print `Author
identity unknown` (`Committer identity unknown`) / (empty) / `*** Please tell me who you are.` / … /
`Omit --global to set the identity only in this repository.` / (empty) / `fatal: no email was given
and auto-detection is disabled`, exit 128 (2.36 and 2.55; the last line names no machine). With
`user.name = Ann Example` and `user.email = ann@example.com` added globally: `Ann Example
<ann@example.com> 1767344400 +0000` for both (exit 0); with the repository's own `Bo Local` and
`bo@example.org`: `Bo Local <bo@example.org> 1767344400 +0000`; with S19's seed: `Test Author
<author@example.com> 1767344400 +0000` (`1767344400` is 2026-01-02 09:00 UTC, D72's date).

**The session's global file through S18, S19 and S20** (each write `git config --global …` in the data
folder, exit 0):

| After | File (`{data}\harness\global.gitconfig`) |
|---|---|
| S18 part A's seed (also S14's) | `[user]` / `⇥useConfigOnly = true` |
| S18 step 4: `--replace-all user.name "Ann Example"`, then `--replace-all user.email ann@example.com` | `[user]` / `⇥useConfigOnly = true` / `⇥name = Ann Example` / `⇥email = ann@example.com` |
| S18 part B's seed (the same bytes; never written) | `[user]` / `⇥useConfigOnly = true` / `⇥name = Ann Example` / `⇥email = ann@example.com` |
| S19's seed (all three parts; never written) | `[user]` / `⇥name = Test Author` / `⇥email = author@example.com` / `[pull]` / `⇥ff = only` |
| S20's seed | `[mergetool "slow"]` / `⇥cmd = true` |
| S20 step 3: `--replace-all merge.tool slow` | `[mergetool "slow"]` / `⇥cmd = true` / `[merge]` / `⇥tool = slow` |
| S20 step 4: `--unset-all merge.tool` | `[mergetool "slow"]` / `⇥cmd = true` (git removes the emptied `[merge]` section) |

The outside read lists only the global file's entries, as `global` with the origin
`file:<the global file's path, forward slashes>`, the key in git's lower case
(`user.useconfigonly`); the repository read lists them first, then `.git/config`'s as `local`
(`file:.git/config`).

**`.git/config` through S18 and S19** (each write `git config --local …` in `{root}\settings`, exit 0;
the lines after `[branch "main"]` / `⇥remote = origin` / `⇥merge = refs/heads/main`):

| After | What follows `[branch "main"]`'s two lines |
|---|---|
| S18 step 7: `--replace-all user.name "Bo Local"`, `--replace-all user.email bo@example.org` | `[user]` / `⇥name = Bo Local` / `⇥email = bo@example.org` |
| S19 step 4: `--replace-all pull.rebase true`, `--replace-all pull.ff true` | `[pull]` / `⇥rebase = true` / `⇥ff = true` |
| S19 parts B and C before the start (`git config pull.rebase true`, `git config pull.ff true`) | the same |
| S19 step 7: `--unset-all pull.rebase`, `--unset-all pull.ff` | nothing (git removes the emptied `[pull]` section) |
| S19 step 8, outside the app: `git config branch.main.rebase false` | `[branch "main"]` gains `⇥rebase = false` after `merge` |
| S19 step 9: `--unset-all branch.main.rebase` | nothing: `[branch "main"]` back to `remote` and `merge` |
| S19 step 10, outside the app: `git config pull.rebase preserve` | `[pull]` / `⇥rebase = preserve` |

**Pull's reads and git's answers** (8.9, SD29; each `git config --get <key>`, a read, and after it
`git config --type=bool --get <key>` when the value is none of `true`, `yes`, `on`, `false`, `no`,
`off`; the screen's openings and read backs make the same reads, without the "Then"; derived on 2.36
and replayed on 2.55, identical):

| State | `pull.ff` | `branch.main.rebase` | `pull.rebase` | Then |
|---|---|---|---|---|
| S19's seed only (steps 1 to 3; after step 7's and step 9's saves) | `only` (0), then `git config --type=bool --get pull.ff` → `fatal: bad boolean config value 'only' for 'pull.ff'` (128): no boolean, so Only | exit 1 | exit 1 | step 2's pull: `git fetch --progress origin` (0, nothing printed: `origin/main` is the remote's `main`); `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --` → `remote.txt\0` (0; `remote.txt` is not on disk, so no `check-ignore`, 9.3); `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` → `fatal: Not possible to fast-forward, aborting.` (key), 128; 2.55 prints nine lines before it: `hint: Diverging branches can't be fast-forwarded, you need to either:` / `hint:` / `hint: ⇥git merge --no-ff` / `hint:` / `hint: or:` / `hint:` / `hint: ⇥git rebase` / `hint:` / `hint: Disable this message with "git config set advice.diverging false"` |
| with the repository's `pull.rebase = true`, `pull.ff = true` (step 4's read back; steps 5 to 7) | `true` (0: the last value, `.git/config`'s; a common word, so no `--type=bool` read) | exit 1 | `true` (0) | step 6's pull: the fetch (0); `git merge-base --is-ancestor <HEAD's SHA> FETCH_HEAD` exit 1 (diverged); the rebase's reads (its fork point, 9.6's ignored-files check: nothing in the way); the backup; `git rebase` (0): standard error `Rebasing (1/1)` then `Successfully rebased and updated refs/heads/main.` (2.36 joins them with a carriage return and git's erase-line `ESC[K`, which the app removes, `RebaseText.WithoutEraseLine`; 2.55 with a carriage return alone) |
| `branch.main.rebase = false`, no `pull.*` locally (step 8, read by the screen only) | `only` (0), then the `--type=bool` read (128) | `false` (0) | not read (the branch's key answered) | — |
| the repository's `pull.rebase = preserve` (step 10's opening; step 11) | `only` (0), then the `--type=bool` read (128) | exit 1 | `preserve` (0) | `git config --type=bool --get pull.rebase` → `fatal: bad boolean config value 'preserve' for 'pull.rebase'`, 128 (2.36 and 2.55): not a boolean, nor `merges`, `m`, `interactive` or `i`, so `Problem` = `pull.rebase is set to preserve, which this app does not understand.`, and nothing is fetched (8.9). `git pull` itself: `error: preserve: 'preserve' superseded by 'merges'` / `fatal: invalid value for 'pull.rebase': 'preserve'`, 128, before fetching (0.3) |

**"conflicts" stopped in its merge** (S20; phase 4's GC-M and its C10 step 5's resolver): on `main`
(`da31c97`), `git merge feature/theirs` stopped; `git status --porcelain=v2` prints `u UU N... …
notes.txt`; the banner `Merging: "Merge branch 'feature/theirs'". 1 file has conflicts.`; 5 lanes,
the working-changes row at display row 0 below the banner (positions from the banner's bounds, D122).
The resolver over the graph and right-panel columns (x 260 to 1100): header `U`, `notes.txt`, `Both
modified`, `File 1 of 1`; toolbar `Conflict 1 of 2` with its arrows, `Take ours`, `Take theirs`,
`Merge tool…` (`ResolverMergeTool`), `Mark resolved`. `git config --get merge.tool` in
`{root}\conflicts`: exit 1 with S20's seed; `slow` after step 3; exit 1 after step 4.

**"ignored"** (S21; scenario facts 4): IG0, 3 lanes, Message x 454, clicks (484, 129 + 26 r): row 0
`stash@{0}` (ring) On main: Stash the secret; row 1 `main` ✓, `origin/main` Main work `c49cac4`; row 2
`tools` Add the build file; row 3 `vendor`, `origin/vendor`, `origin/vendor-remote` Add the vendor
secret `133a796`; row 4 Stop tracking the secret `39bd803`; row 5 Track the secret `1ac759d` (y 259);
row 6 Initial commit. `secret.env` `LOCAL` and `build/out.bin` `binary`, ignored (`!! build/`,
`!! secret.env`); no working-changes row. Left panel: Local branches `main`, `tools`, `vendor`.
**IG0v**, after S21's checkout of `vendor` with Keep ignored files off: IG0's rows, lanes and lines,
with row 1's labels `main` (no check mark), `origin/main` and row 3's `vendor` ✓, `origin/vendor`,
`origin/vendor-remote` (drawn as `LabelsInRow` gives them); `CurrentBranch` `vendor`; Local branches
› vendor accented; HEAD `vendor` at `133a796`; `secret.env` `vendor` (LOCAL lost), `build/out.bin`
`binary`; `git status --porcelain=v1 --ignored` prints `!! build/` only.

**"graph"** (S15; scenario facts 6, phase 1's): GR0, 13 rows, 3 lanes, `stash@{0}` on row 0 (a ring,
its dashed line into row 2), `main` ✓ on row 2, clean; drawn as phase 1's `01` (dark) and `02` (light)
draw it, its lane colours numbers 0 to 4: dark `#7C8CFF`, `#3FB97F`, `#E0A23B`, `#E5606B`,
`#4FB3D9`; light (phase 6's, D198) `#4353D8`, `#1C8655`, `#A16A1B`, `#C93A46`, `#1D7EA6`. Its 13 rows
end at y 454; the spots S15 samples are clear of text, lanes and rows: the graph area (480, 600), the
left panel (130, 600), the tab strip's chrome (1070, 18): dark `#14161B`, `#1A1D24`, `#20242C`; light
`#FFFFFF`, `#F5F6F8`, `#EBEDF1` (`Phase6Expectations.Dark` / `.Light`'s Window, Panel and Chrome).

**`settings.json`** (parsed; `JsonSettingsStore`'s camelCase names): a save after any change writes
`theme`, `fileList`, `diffMode`, `autoFetchMinutes`, `stashIncludeUntracked`, `showAllFiles`,
`keepIgnoredFiles`, `editor`, `extraRunnableExtensions`; a step names the values it changes, the
others are the defaults (`Dark`, `Flat`, `Inline`, 5, true, false, true, null, empty).

#### Texts these checks rely on (part S)

From design section 14 (14.1, 14.2, 14.6 to 14.8, 14.11, 14.14) and the sections it names, and the
existing `OperationTexts` where a text is phase 3's to 5's. Counts take the singular for one (D113).

**The toolbar** (7.2, 6.8, 11.6, 14.11): `SettingsButton`, label `Settings`, `IconSettings` (Lucide's
`settings`), tooltip and help text `Open settings (Ctrl+,)`, `AcceleratorKey` `Ctrl+,`, never
disabled. `ThemeSwitch` (no key), tooltip and help text: Dark chosen `Switch to the light theme`;
Light chosen `Switch to the dark theme`; System chosen and dark showing `Switch to the light theme
(stops following the system)`; System and light `Switch to the dark theme (stops following the
system)`; the icon the theme it switches to (`IconSun` while dark, `IconMoon` while light, the class
`light` while light). `PullButton` `Pull from origin/main (Ctrl+Shift+L)`; `StashButton` `Stash your
changes (Ctrl+Shift+S)`.

**The screen's frame** (7.1, 7.8, 14.8): title `Settings`; `General`, `Git`, `Tools`, `Keyboard
shortcuts`; the error dot's help text `Has an error`; `Cancel`, `Save`; `SettingsSaving` `Saving…` and
`Waiting for {title} to finish…` (not shown by any check: a save takes well under a second and no
check holds one).

**General** (7.3, 14.8): `Theme`: `Dark`, `Light`, `System`, `System follows your operating system's
light or dark mode.`; `Fetching`: `Fetch in the background every`, the box, `minutes`, `Only the
active tab's repository; it never asks for a password.`, error `Enter a whole number of minutes from 1
to 60.`; `Working tree`: `Keep ignored files`, `A checkout, merge, rebase, cherry-pick, revert, stash
apply or stash pop that would overwrite or delete an ignored file does not start. Off: they run as git
runs them, which can overwrite or delete such a file without a word; checking out an older commit of
the current branch, Undo and Redo still keep it.`; `Include untracked files when stashing`, `The
Stash changes dialog starts with this; its own check box changes it too.`; `Opening files`: `Open file
asks first before it opens a file with one of these extensions:`, `.exe .bat .cmd .com .ps1 .vbs .vbe
.js .jse .wsf .hta .msi .scr .pif .cpl .lnk .url .reg .jar .app .command .sh .desktop`, `From PATHEXT:
.wsh .msc` (Windows, the fixed catalogue), `Also ask for`, placeholder `.py .pl`, error `"{entry}" is
not an extension such as .py: use letters and digits.` (no check shows it: 7.11's tests), `On Linux
and macOS it also asks before it opens a file marked as a program.`.

**Git** (7.4, 8.2 to 8.8, 14.8): `All repositories (global)`, `This repository`; disabled `Open a
repository's tab to change its own settings`; enabled tooltip `{name} — {folder}`; captions `Saved in
your global git configuration, which git in a terminal uses too.` / `Saved in this repository's
.git/config, which all its worktrees share. These values win over those for all repositories.`;
`Reading git's configuration…`; labels `Name`, `Email`, `Pull`, `Merge tool`; placeholders `Not set`,
`Same as global: {value}`; `A name cannot contain <, > or a line break.`; `This does not look like an
email address.`; `Commits here are made as {author}.`; `Git cannot make commits here: no email is
set.`; `user.useConfigOnly is set, so git does not guess a missing name or email.`; the radios `Not
set` / `Same as global`, `Merge (fast-forward when possible)`, `Merge, always with a merge commit`,
`Rebase`, `Rebase, keeping merge commits`, `Fast-forward only`, `Other: {key} = {value}`; `PullNotSet`'s
tooltips `Nothing set: the app merges, and git in a terminal stops when the branches have diverged` /
`Uses the pull mode for all repositories`; `Pulls fail while this value is set.`; `Pull into {b}:
{mode} ({key} = {value} in {file}).`; `Pull into {b}: Merge (fast-forward when possible): nothing is
set, so the app merges; git in a terminal stops when the branches have diverged.`; `Pull into {b}
fails: {problem}`; `{b} has its own pull setting, branch.{b}.rebase = {value}, which git uses instead of
the mode above.`, `Use the repository's setting`, `Save removes branch.{b}.rebase, so {b} pulls as set
above.`, `Keep {b}'s setting`; `Not set`, `Same as global (not set)`, `Choose…`, `Used by Merge tool…
in the conflict resolver, and by git mergetool in a terminal.`; `MergeToolStatus` `git did not find
{tool} on this computer: Merge tool… fails until it is installed.` (8.10); `{repository} sets its own: {value}.`
(SD8); the file names `your global configuration` and `.git/config`.

**Tools** (7.5, 12.5, 14.8): `Editor`; `None`, `Visual Studio Code`, `Other`; `Choose…`; `Open in
editor appears in the file menus, beside Open file, once an editor is chosen.`; `Looking for
editors…`; `Program`, `Arguments`, `Browse…`; `{path} is the file's full path, {line} its line (1 for
now). Without {path}, the path comes last.`; `There is no file at this path.`; the file dialog's title
`Choose the editor's program`.

**Pick dialogs** (7.7, 14.6): titles `Choose an editor`, `Choose a merge tool`; confirm `Choose`;
`None` — `no Open in editor in the file menus`; `{Name}` — `{program path}`; `Other…` — `a program
of your choice`; `Not set` — `Merge tool… stays disabled`; `{tool}` — `found on this computer` (the
fixed catalogue has no descriptions); `{name}` — `your own tool (mergetool.{name}.cmd)`; `{name}` — `not
found on this computer`.

**Keyboard shortcuts** (6.1, 6.11, 7.6; SD21): `These keys are fixed.`; the groups, rows and keys on
Windows and Linux (and headless, whose command modifier is Control, 0.7):

| Group | Action | Keys |
|---|---|---|
| App | Command palette | `Ctrl+Shift+P` |
| | Open settings… | `Ctrl+,` |
| | Show or hide the activity log | `Ctrl+J` |
| | Refresh | `F5, Ctrl+R` |
| Tabs | New tab | `Ctrl+T` |
| | Close tab | `Ctrl+W` |
| | Next tab | `Ctrl+Tab, Ctrl+PageDown` |
| | Previous tab | `Ctrl+Shift+Tab, Ctrl+PageUp` |
| | Go to tab 1 to 8 | `Ctrl+1 to Ctrl+8` |
| | Go to the last tab | `Ctrl+9` |
| | Go to the next dashboard | `Ctrl+Shift+D` |
| Repositories | Open repository… | `Ctrl+O` |
| | Clone repository… | `Ctrl+Shift+C` |
| | Init repository… | `Ctrl+Shift+I` |
| Repository | Fetch all remotes | `Ctrl+Shift+F` |
| | Pull | `Ctrl+Shift+L` |
| | Push | `Ctrl+Shift+K` |
| | Create branch… | `Ctrl+B` |
| | Stash changes… | `Ctrl+Shift+S` |
| | Pop the newest stash | `Ctrl+Shift+O` |
| | Undo | `Ctrl+Z` |
| | Redo | `Ctrl+Y, Ctrl+Shift+Z` |
| | Search commits | `Ctrl+F` |
| | Next match | `F3, Ctrl+G` |
| | Previous match | `Shift+F3, Ctrl+Shift+G` |
| | Show reflog | `Ctrl+Shift+R` |
| | (note) `On a dashboard, Fetch and Pull fetch and pull every repository of its workspace.` | |
| Changes | Stage all | `Ctrl+Shift+A` |
| | Unstage all | `Ctrl+Shift+X` |
| | Go to the commit message | `Ctrl+Shift+M` |
| | Commit, in the message boxes | `Ctrl+Enter` |
| Files | File history of the open diff's file | `Ctrl+Shift+H` |
| | Blame of the open diff's file | `Ctrl+Shift+B` |
| | Close the diff, a page or the search bar | `Esc` |
| Moving around | Filter branches and tags | `Ctrl+Shift+E` |
| | Next region | `F6` |
| | Previous region | `Shift+F6` |
| | Context menu | `Shift+F10, Menu` |
| | Move in the graph | `Up, Down, PageUp, PageDown, Home, End` |
| | Extend the graph's selection | `Shift+Up, Shift+Down, Shift+PageUp, Shift+PageDown, Shift+Home, Shift+End` (two lines) |
| Interactive rebase | Move the selected row | `Alt+Up, Alt+Down` |
| | Pick, reword, squash, fixup, drop | `P, R, S, F, D` |
| Conflict resolver | Previous or next conflict | `Alt+Up, Alt+Down` |
| | Previous or next file | `Alt+Left, Alt+Right` |
| | Leave the result | `Ctrl+Tab` |
| Dashboard | Open the repository | `Enter` |
| | Remove from workspace | `Delete` |
| | Move the repository | `Alt+Up, Alt+Down` |
| Dialogs and menus | Confirm | `Enter` |
| | Confirm from a box of several lines | `Ctrl+Enter` |
| | Cancel or close | `Esc` |

The expectations write every key text as a literal (6.12), never the app's formatter's output.

**Operations, results and dialogs** (8.3, 4.8, 8.9, 9.15, 14.1 to 14.4; phases 3 to 5): `Save
repository settings` (running `Saving the repository's settings…`, error title `Saving the settings
failed`, summary `Git could not save the repository's settings.`), result `Saved the repository's
settings`; a global write's failure `Saving the settings failed` / `Git could not save the settings for
all repositories.` (no check shows it: 8.13's tests); `Pull` (`Pulling origin/main into main…`, `Pull
failed`); `main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main
or rebase main onto origin/main.` (`PullNotFastForward`); `pull.rebase is set to preserve, which this
app does not understand.`; `Pulled and rebased main onto origin/main`; `Check out vendor` (error title
`Checkout blocked`), `Check out 1ac759d`; `1 ignored file is in the way: {name} tracks a file of the
same name. Move it away first.`; `Checked out vendor`; `{Title} failed` in the danger tone; `Commit 1
file`, `Commit 1 file (Ctrl+Enter)`; `Undo commit "{subject}" (Ctrl+Z)`, `Undo pull into main
(Ctrl+Z)`, `Undo checkout of vendor (Ctrl+Z)`, `Nothing to undo`; the stash dialog `Stash changes`,
`Message (optional)`, `Include untracked files`, `Cancel`, `Stash`; Open file's question `Open
{file}?`, `This file can run programs. Open it anyway?`, `Cancel`, `Open`; the file menus' `Open file`,
`Open in editor`, `Show in folder`, `Copy path`, `File history`, `Blame`; the resolver's `Merge tool…`
and its reason `Set merge.tool in git's configuration to use a merge tool`.

**The palette** (5.4, 11.7, 14.7): `Open settings…` (key `Ctrl+,`), `Show keyboard shortcuts…`,
`Choose an editor…` (while no editor is chosen), `Use the dark theme`, `Use the light theme`, `Follow
the system's theme` (the one chosen now left out; no detail, no key).

**Log lines** (14.14): `VISUALCOMMIT_AUTO_FETCH_MINUTES={value} overrides the auto-fetch setting
({minutes} minutes).`; `Theme: Dark.`, `Theme: System (the system's theme is {dark|light}).`; `Theme
changed to {theme}.`; `The system's theme changed to {light|dark}; the app follows it.` / `…; the app
keeps the {dark|light} theme.`; `{repo}: Command palette ran: {label}`; `Open in editor: {command}`;
the queue's `{repo}: {Title}: {outcome} after <N> ms: {message}`. Real-window `run.txt`: `S15 (System
at start): failed. High contrast is on, and Avalonia follows its scheme: turn it off and run the pass
again.`

### Part G: git behaviour: ignored files kept, and fetch refspecs

Part G covers design sections 9 (Keep ignored files, D193, D196, D221) and 10 (fetch refspecs, D201,
D222): checks G22 to G25 of design 17.4. Every checkout keeps an ignored file the target would
overwrite or delete: git's `--no-overwrite-ignore` refuses it and the app says so in D179's words,
on every checkout path, a remote branch's both ways, "Create branch" with its box ticked and a commit
off the branch detached from its menu included (G22); every other operation that can lose one,
which git has no option for, is refused by the app's own check before git, with the operation's own
title and the file's name and nothing written: a merge, a rebase onto a branch, a cherry-pick, a revert, a stash's apply and its pop, and the pull,
whose check runs inside its integration after the fetch, there for a branch only ahead of its
upstream with a local merge whose side branch starts at a commit that tracks the file (found only
by the branch points git's `--rebase-merges` resets to, 9.6's reads (5) and (6)), for a diverged
upstream's merge, while a merge whose source adds nothing the branch lacks goes on and, under
`pull.ff=only`, git's own refusal wins (G23); with the setting off in `settings.json` the app is
phase 5 exactly, with no check, no option and git overwriting and deleting as it does (G24); and the drop's push, the remote branch's
checkout and its deletion follow the remotes' fetch lines: a renamed remote branch pushed and
deleted by its name on the remote, a ref one remote fetches from two refs (R2) checked out into a
branch without an upstream (D201) while its pushes, its deletion and a push that would give it a
second source are refused, and a ref that is not a branch (R4) checked out as git tracks it, its
deletion disabled (G25). The automatic stash's checkout under Off (D179's option whatever the
setting) and the setting on the settings screen are part S's (S21). This part was written before
the UI it tests, from `docs/phase-6-design.md` 0.4, 0.5, 9, 10, 14, 16.1 and 17, decisions D179,
D193, D196, D201, D211, D219, D221 and D222, the scenario facts and phases 3 to 5's reports for what
stays as it was. Every git state below was derived with Git 2.36.0.windows.1, and every git text
and state replayed with Git 2.55.0.windows.5, on copies of "ignored" and "refspecs" that the merged
scenario code built (scenario facts 9; "How the expected git states were derived", part G).

#### Conventions of part G

- **Project, class and test methods.** The checks are in `tests/VisualCommit.VisualTests.Phase6`,
  class `Phase6ChecksG` (`Phase6Checks.G.cs` for G22 to G24, `Phase6Checks.G.Refspecs.cs` for G25),
  with what they compare against in `Phase6Expectations.G.cs` (which the real-window link takes,
  17.3, although no G step runs there) and its tests in `Phase6ExpectationTests.G.cs`. One test
  method per app start (`FreshApplication`), each on a fresh copy of its scenario: six app starts,
  named in the table below (GD1). No method runs more than six operations, and none comes near the
  hang watchdog's 3 minutes (the longest, G23 part B, five pulls on a file remote that has nothing
  new).
- **Defaults.** A method writes a session with one tab on a fresh copy (`Scenarios.IgnoredAsync()`,
  folder and tab `ignored`; `Scenarios.RenamingRefspecAsync()`, folder and tab `refspecs`), starts
  the app at 1100×700 in the dark theme (`Phase6Checks.OpenAsync(…, AppTheme.Dark)`, the fake system
  theme dark, 17.2), and waits until the graph has drawn its rows, the status is shown and the queue
  is idle; the pointer then rests at the neutral point (760, 687), in the status bar. The harness's
  clocks are phase 5's: the frozen record clock and the fixed backup clock, whose first backup name
  in a session is `…-20260102T090001000Z`. Each session has its own git configuration
  (`GIT_CONFIG_GLOBAL` and `XDG_CONFIG_HOME` in its data folder, 17.2), which no G step writes.
- **The setting.** `settings.json` holds no `keepIgnoredFiles` in G22, G23 and G25, so Keep ignored
  files is on, its default (9.1, 7.10). G24 writes `"keepIgnoredFiles": false` before the start
  (beside what `OpenAsync` writes); the settings screen is not opened in this part (S21 does that).
- **"Before the app starts"** is a git call through `TempRepo` on the copy, as a terminal makes it,
  dated by the copy's clock: "ignored" carries on from its template at 2026-01-01 12:07 (after the
  stash, 12:06), and `TempRepo.MergeAsync` moves it to 12:08 after its commit. Only G23 part B has
  such steps (G24 writes `settings.json` only). **"Outside the app"** while it runs is the same; it writes only `.git/config`
  (`git config …`), which reaches the app through the git folder's watcher (D52, design 10.2): the
  check marks the call log before the write, waits (`OutsideChangeTimeout`, 30 s) for the app's next
  refs read, the call `git config --null --get-regexp ^remote\.` after the mark (10.2's one read of
  the remotes), then `WaitForIdleAsync`; where the step names a tooltip, also until the toolbar's
  `PullButton` has it (GD22).
- **Rows.** Commit and stash rows are numbered as `SelectedIndex` counts them, from 0 at the top; no
  graph of this part has the working-changes row or a banner, so row r's centre is y 129 + 26 r. Lane
  L's node is at x 406 + 16 L. A row is clicked in its Message column at x 484 in IG0, IG2 and IG3
  (3 lanes, Graph column 64, Message x 454, Date x 580 to 700), x 500 in IG1 (4 lanes, Graph column
  80, Message x 470 to 700: phase 1's rule leaves no Date column at 1100, the message keeping its 120)
  and x 468 in the RS graphs (1 lane, Graph 48, Message x 438, Date x 580 to 700). The checks take
  every position from the controls' bounds (`GraphRowPoint`, `GraphLabelBounds`, the left panel's
  rows), as D122's rule says; the numbers here are what those bounds give with the default panel
  widths.
- **Labels** are listed in phase 1's order (a detached `HEAD`, the branch HEAD is on with ✓, other
  local branches, remote branches, tags, stashes); which of them the 130-wide Branch / Tag column
  draws, and the "+N" it draws for the rest, is taken from `LabelsInRow` (scenario facts 0: phase 6's
  scenarios were not measured in the running app). A label is double-clicked at its centre, and only
  a drawn one: G22 double-clicks `vendor` on IG0's row 3, which scenario facts 4.1 give as drawn
  (`vendor`, `+2`). Every other ref of this part is reached through the left panel (GD11).
- **Left panel.** Paths are written Local branches › vendor, Remotes › origin › pr › 1, Stashes ›
  On main: Stash the secret (`ShellDriver.RefRowAsync`). A remote's row starts open; a folder that
  does not hold HEAD's branch starts closed (`up` under fork, `pr` under origin and under upstream,
  `feature` under origin), and a check opens it with a click on its row first. The Remotes section's
  count is the number of remote branches (`LeftPanelViewModel`: `remoteBranches.Count`), Local
  branches' the number of local branches (GD16). A right-click on a left-panel row opens its menu
  only and leaves the graph's selection as it was (D114): it selects nothing and clears nothing, so
  a row an earlier step selected stays selected, and the refresh after a refused operation keeps it
  (phase 5's p26a). A double-click on a left-panel branch selects that row and its commit in the
  graph with its first click, then checks out (phase 3's 4c).
- **Drags** are phase 3's check 7, step 5: press at the centre of the left-panel row, move to the
  centre of the target row in 5 equal steps with the button held (`MoveMouseHeld(…, steps: 5)`), the
  first step more than 4 px from the press, release; the drop menu opens with its top-left corner 2
  right of and 2 below the release point. A left-panel row's press selects nothing in the graph.
- **Menus** are written header first, items in order, "—" for a separator; "(danger)" an enabled
  item in `VcDangerBrush`, "(disabled: "…")" a disabled item with its tooltip and help text,
  "(highlighted)" the first enabled item, which has the focus when the menu opens (D105), and a key
  at an item's right where 6.7 gives one. A menu's height is 8 + 24 + 26 × items + 9 × separators
  (±2), and it lies at least 4 px inside the window, moved there when it would pass an edge (phase
  3's 4a). G22 and G23 assert only the item they click, but for G23's stash menu (step 6); that menu,
  the drop menus and the remote branch menus of G25 are compared whole with `MenuSnapshot`.
- **Dialogs.** The error dialog (`ErrorDialog`) is asserted with `AssertErrorDialogAsync`:
  `ErrorTitle`, `ErrorSummary`, `ErrorRepository` ("In ignored", "In refspecs"), `ErrorCommand`, the
  visible buttons left to right ("Copy output" `ErrorCopyButton`, the extra action `ErrorExtraButton`
  if any, "Close" `ConfirmButton`) and `ErrorOutput`. A refusal the app makes before git runs has no
  `ErrorOutput`, no `ErrorCommand` and no "Copy output": "Close" only (phase 5's PD5, design 9.9). A
  question is asserted as phase 3's confirmation (`ConfirmTitle`, `ConfirmQuestion`,
  `ConfirmDetail`, "Cancel" with the focus, the confirm button). "No question" and "no dialog" are
  `AssertNoDialogAsync` after the operation ended. Each dialog is closed with "Close" before the next
  step.
- **Waiting for an operation**: `WaitForOperationAsync(app, repo, "<Title>", mark)`: the queue's log
  line `<repo>: <Title>: <Succeeded|Failed> after <N> ms…` and the refresh after it; then, for a
  failure, `WaitForDialogAsync<ErrorDialogViewModel>`. Each step names the title and the outcome.
- **The status bar** is `OperationStatus` (its tone: secondary, or the danger colour for `<Title>
  failed`; the text, its tooltip and the drawn text) and `CurrentBranch`. `OperationStatus` draws at
  most 520 px at 11.5 px and cuts a longer text with "…"; the check reads the bound text whole.
- **The toolbar** is phase 6's (Settings left of Search, 7.2; keys in the enabled buttons' tooltips,
  6.8), which is not this part's subject; a step asserts a button's tooltip (`ToolTip.GetTip`, no
  hover) only where it names one. The toolbar's Undo starts `Nothing to undo` (disabled); an
  operation that is refused or fails records nothing (phase 5 design 7.2), so every refusal of this
  part leaves Undo as it was.
- **The call log** (`IGitCallLog`, `CallsSince(mark)`, `AssertCalledInOrder`) is where a check reads
  which git commands ran, with their exit codes and output; a command is written as the call log's
  `CommandText` renders it (an argument with spaces in double quotes). "In this order" means in this
  order among the step's calls, others between them allowed (D124's refs read, the in-progress and
  HEAD checks, the refresh). 9.6's four reads run side by side (`Task.WhenAll`), so their order in
  the log is not fixed: they are asserted as a set before what follows them (GD18). The call log
  keeps no standard input for `check-ignore --stdin`, `diff-tree --stdin` and `fmt-merge-msg`: their
  input is given here for the reader and their output asserted. **"No write"** means no call since
  the step's mark whose command starts with `git merge ` (with the space: `git merge-base` is a
  read), `git rebase`, `git cherry-pick`, `git revert`, `git stash apply`, `git stash pop`,
  `git stash push`, `git switch`, `git reset`, `git commit`, `git branch `, `git update-ref` or
  `git push`, and no new backup ref. **"No `git check-ignore`"** (and no other read the app makes
  inside an operation) is asserted by operation, not by time: among the calls whose `OperationId`
  is the operation's (`GitCallRecord.OperationId`, taken from the record of its git command, such as
  its `git merge …` or `git switch …`), and none between the step's mark and that command. The
  repository watcher asks git about every working-tree burst (`GitRepository.CreateWatcher` passes
  `AllIgnoredAsync`, `git check-ignore --stdin -z` through the app's runner, so in the same call log)
  from its timer thread, outside every operation (`OperationId` null); a burst that ends after the
  operation's watcher pause can add such a call after the click.
- **Git's state**, asserted on every git version: HEAD (`git symbolic-ref -q HEAD`, `git rev-parse
  HEAD`), the refs (`git for-each-ref`), a remote repository's refs (`git -C ../fork.git
  for-each-ref`, likewise `origin.git`), the branch keys (`git config --get-regexp ^branch\.`), `git
  stash list`, the files' bytes, HEAD's reflog (`git log -g --format=%gs HEAD`), the in-progress
  files (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `sequencer/`, `rebase-merge/`,
  `rebase-apply/`) and the backup refs (`git for-each-ref refs/visualcommit/backup`). "The exact
  state" is `TempRepo.ExactStateAsync` (phase 5's: HEAD, every ref with its object, `git ls-files
  --stage`, `git status --porcelain=v2 --untracked-files=all --ignored` and every working-tree file
  outside `.git` with its contents, in ordinal order), compared whole with the one read at the point
  the step names; `.git/config` and `FETCH_HEAD` are not part of it.
- **Git's own texts** below (TG1 to TG12: `ErrorOutput`, the call log's streams) are Git
  2.36.0.windows.1's and are asserted word for word only when `git --version` is 2.36; on another git
  a check asserts the line marked "key", that `ErrorOutput` is `GitOutputCleaner.Clean` of the failed
  call's own output (`AssertGitOutputShown`), and the dialog's title, summary and buttons (phase 3's
  rule). Where Git 2.55.0 prints otherwise the text says so. The outputs of the app's own reads (the
  paths and SHAs of TR1 to TR9: `diff-tree`, `merge-base`, `log`, `rev-list`, `check-ignore`) and
  every exit code are git's data, the same on 2.36 and 2.55 (replayed), and are asserted on every
  version; so are the commands. The one exception is the order of TR6's read (5): git prints the
  boundary commits in the order its revision walk meets them, a detail of git rather than of the
  repository, which only 2.36 and 2.55 were seen to print alike; (5) is asserted as the app reads it
  (TR6), its exact order only when `git --version` is 2.36. CI's gits (Windows and macOS 2.56.0,
  `ubuntu-latest` 2.55.0, the containers 2.43.0) all have `switch` and `--no-overwrite-ignore`.
- **Pictures** are named by `Phase6Checks.PictureName`'s rule
  (`^[gklsw]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`): `g`, the check's number, a step letter
  (running on through a check's methods), then words (`g22a-vendor-blocked`). The pointer moves to
  the neutral point before a capture with a dialog open or with nothing open; a capture with a menu
  open leaves the pointer where the click or the release that opened it was. No picture of this part
  shows a temporary folder, a caret, a tooltip or a progress bar. One varies with git's version:
  `g23i-pull-not-fast-forward`, whose `ErrorOutput` is git's own (TG3): one line on 2.36, and on a git
  that prints `advice.diverging`'s hints those lines before it (nine on 2.55), so the output box and
  the dialog's height change with the version. The gate's picture is the development machine's run
  on Git 2.36 (one line); an inspector of another run's `g23i` compares its title, summary,
  repository line, command and buttons as written and its output region with what that run's git
  printed (`AssertGitOutputShown`; `git --version` of that run), not by bytes. Every other picture of
  this part shows only git texts that 2.36 and 2.55 print alike (TG1, TG2) or none.
- **The real window** repeats no step of this part (design 9.17 and 10.9): its error dialogs,
  questions, menus and the push dialog are phases 3 to 5's, pictured in the real window by phase 3's
  checks 7 and 14 and phase 5's P24; what differs here is texts and git calls, which both harnesses
  read alike.

#### Test methods and app starts (part G)

| Check | Test method (one app start each) | Scenario as the check starts it | Window, theme |
|---|---|---|---|
| G22 | `Check_G22_checkouts_refused_for_ignored_files` | `Scenarios.IgnoredAsync()` (IG0), nothing changed | 1100×700, dark |
| G23 | `Check_G23_merge_rebase_pick_revert_and_stash_refused` (part A, steps 1 to 8) | `Scenarios.IgnoredAsync()` (IG0), nothing changed | 1100×700, dark |
| G23 | `Check_G23_pull_refused_inside_the_integration` (part B, steps 9 to 15) | `Scenarios.IgnoredAsync()`, then before the app starts: `GitAsync("commit-tree", "1ac759d23fd78a78f7660d289a1373ea85378b69^{tree}", "-p", "1ac759d23fd78a78f7660d289a1373ea85378b69", "-m", "Start the side branch")` (prints `bf49f82d2de88c816e12a4ef8560c983a2c4c8db`), `CreateBranchAsync("side", <that SHA>)`, `MergeAsync("side")` (`git merge --quiet --no-ff --message "Merge branch 'side'" side`, giving `6e68a258092ca05b31cd7d3a8c359276bafc2bf7`), `ConfigAsync("pull.rebase", "merges")`: IG1 (GD7) | 1100×700, dark |
| G24 | `Check_G24_keep_ignored_files_off_restores_phase_5` | `Scenarios.IgnoredAsync()` (IG0); `settings.json` with `"keepIgnoredFiles": false` written before the start | 1100×700, dark |
| G25 | `Check_G25_renamed_and_ambiguous_remote_branches` (part A, steps 1 to 7) | `Scenarios.RenamingRefspecAsync()` (RS0), nothing changed | 1100×700, dark |
| G25 | `Check_G25_a_renamed_deletion_and_a_ref_that_is_not_a_branch` (part B, steps 8 to 12) | `Scenarios.RenamingRefspecAsync()` (RS0), a fresh copy | 1100×700, dark |

Six methods where design 17.3 counted four for part G (G23 and G25 have two parts each, GD1). With
every part as written (W 11, K 8, S 13, G 6, L 9 scripted) the walk-through has 47 methods, past
17.3's 40, so by 17.3's rule part L moves to `VisualCommit.VisualTests.Phase6Look` (the lead's ruling
1); part G stays in `VisualCommit.VisualTests.Phase6`.

#### Scenarios and graphs of part G

**"ignored"** (`Scenarios.IgnoredAsync`, folder and tab `ignored`), as scenario facts 4.1 give it
and `Phase6ScenarioTests` pins it: bare `origin.git` beside it (`../origin.git`); `.gitignore`
`secret.env` / `build/`; HEAD on a clean `main` (`c49cac4`) tracking `origin/main` (0, 0); `vendor`
(`133a796`) tracking `origin/vendor`; `tools` (`c8446bb`) without an upstream; `origin/vendor-remote`
(`133a796`) without a local branch; the stash `stash@{0}` "On main: Stash the secret" (`c879568`,
its index commit `ea37eb6`, no untracked part), which adds `secret.env` `stashed` (staged). In the
working tree the ignored `secret.env` (`LOCAL\n`) and `build/out.bin` (`binary\n`). Full SHAs:
Initial commit `2e4599b4149a2686ea72c962920d8ba1764446d2`, Track the secret
`1ac759d23fd78a78f7660d289a1373ea85378b69`, Stop tracking the secret
`39bd8031f98ec9a1a6d02519ffabd35ca693bc63`, Add the vendor secret
`133a796ef1cfa3b4f2dc3237b5342751f85fcc3f`, Add the build file
`c8446bb6a7efb5eb9cc91d4a95e2bd37c0be7ced`, Main work `c49cac40cc05ce7ebe709555621751082c868662`, the
stash `c8795683e3b86ad70873326b9897659e3c5140dd`.

**IG0, "ignored" as built** (scenario facts 4.1). 3 lanes, Graph 64, Message x 454 (126 wide), Date
x 580 to 700; no working-changes row (an ignored file is no change).

| Row | Labels (drawn: `LabelsInRow`) | Node: lane, colour | Message | Date | SHA | y centre |
|---|---|---|---|---|---|---|
| 0 | `stash@{0}` | 0, 0 (ring) | On main: Stash the secret | 2026-01-01 12:06 | `c879568` | 129 |
| 1 | `main` ✓, `origin/main` (`main` ✓, `+1`) | 0, 0 | Main work | 12:05 | `c49cac4` | 155 |
| 2 | `tools` | 1, 1 | Add the build file | 12:04 | `c8446bb` | 181 |
| 3 | `vendor`, `origin/vendor`, `origin/vendor-remote` (`vendor`, `+2`) | 2, 2 | Add the vendor secret | 12:03 | `133a796` | 207 |
| 4 | | 0, 0 (lanes 1 and 2 end here) | Stop tracking the secret | 12:02 | `39bd803` | 233 |
| 5 | | 0, 0 | Track the secret | 12:01 | `1ac759d` | 259 |
| 6 | | 0, 0 | Initial commit | 12:00 | `2e4599b` | 285 |

Lines: lane 0 (colour 0) dashed from row 0's ring into row 1's node, then solid from row 1 to row 6;
lane 1 (colour 1) from row 2's node through row 3 into row 4's node; lane 2 (colour 2) from row 3's
node into row 4's node. Left panel: Local branches 3 (`main` accented, `tools`, `vendor`), Remotes 3
(`origin` › `main`, `vendor`, `vendor-remote`), Pull requests 0, Tags 0, Stashes 1 (`On main: Stash
the secret`). `CurrentBranch` `main`. The exact state: HEAD `c49cac4…`; refs `refs/heads/main`
`c49cac4…`, `refs/heads/tools` `c8446bb…`, `refs/heads/vendor` `133a796…`,
`refs/remotes/origin/main` `c49cac4…`, `refs/remotes/origin/vendor` and
`refs/remotes/origin/vendor-remote` `133a796…`, `refs/stash` `c879568…`; the index `.gitignore`
(`53985eb`), `README.md` (`6f4e424`), `app.txt` (`20982c0`); the status `! build/out.bin`,
`! secret.env` (GD17); the files `.gitignore`, `README.md` (`# Ignored\n`), `app.txt`
(`app\nmain\n`), `build/out.bin` (`binary\n`), `secret.env` (`LOCAL\n`). HEAD's reflog has 10
entries, the newest `reset: moving to HEAD` (the stash's). Branch keys: `branch.main.remote origin`,
`branch.main.merge refs/heads/main`, `branch.vendor.remote origin`, `branch.vendor.merge
refs/heads/vendor`.

**IG1: "ignored" with a side branch merged into `main`** (G23 part B; built before the app starts,
GD7). "Start the side branch" is a commit on Track the secret with that commit's tree (no change:
no file is written), `bf49f82d2de88c816e12a4ef8560c983a2c4c8db`, dated 2026-01-01 12:07, on the new
branch `side`; `git merge --no-ff side` makes "Merge branch 'side'"
`6e68a258092ca05b31cd7d3a8c359276bafc2bf7` (parents `c49cac4`, `bf49f82`; tree Main work's; 12:07),
writing nothing to the working tree (`secret.env` stays `LOCAL\n`; HEAD's reflog gains `merge side:
Merge made by the 'ort' strategy.`). `main` is then 2 ahead of `origin/main` and behind by nothing,
with a merge in `origin/main..main`, and neither `origin/main` nor `main`'s tip tracks `secret.env`,
while the side branch's branch point, Track the secret, does. The same SHAs on 2.36 and 2.55.
`pull.rebase = merges` in `.git/config`. 4 lanes, Graph 80, Message x 470 to 700, no Date column.

| Row | Labels | Node: lane, colour | Message | Author date | SHA | y centre |
|---|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 (a merge) | Merge branch 'side' | 2026-01-01 12:07 | `6e68a25` | 129 |
| 1 | `side` | 1, 1 | Start the side branch | 12:07 | `bf49f82` | 155 |
| 2 | `stash@{0}` | 2, 2 (ring) | On main: Stash the secret | 12:06 | `c879568` | 181 |
| 3 | `origin/main` | 0, 0 | Main work | 12:05 | `c49cac4` | 207 |
| 4 | `tools` | 2, 3 | Add the build file | 12:04 | `c8446bb` | 233 |
| 5 | `vendor`, `origin/vendor`, `origin/vendor-remote` | 3, 4 | Add the vendor secret | 12:03 | `133a796` | 259 |
| 6 | | 0, 0 (lanes 2 and 3 end here) | Stop tracking the secret | 12:02 | `39bd803` | 285 |
| 7 | | 0, 0 (lane 1 ends here) | Track the secret | 12:01 | `1ac759d` | 311 |
| 8 | | 0, 0 | Initial commit | 12:00 | `2e4599b` | 337 |

Lines (as the app's `GraphLayout` gives them): lane 0 (colour 0) from row 0 to row 8; from row 0's
node a line into lane 1 (colour 1) and row 1's node, then on down lane 1 through rows 2 to 6 into row
7's node; lane 2
(colour 2) dashed from row 2's ring into row 3's node; lane 2 (colour 3) from row 4's node into row 6's
node; lane 3 (colour 4) from row 5's node into row 6's node. Left panel: Local branches 4 (`main`
accented with ↑ `2`, `side`, `tools`, `vendor`), Remotes 3, Pull requests 0, Tags 0, Stashes 1.
`CurrentBranch` `main`; the toolbar's Pull enabled, `Pull from origin/main (Ctrl+Shift+L)`. The exact
state is IG0's with `refs/heads/main` `6e68a25…` and `refs/heads/side` `bf49f82…`; index, status and
files as IG0's.

**IG1v: IG1 with `branch.main.merge = refs/heads/vendor`** (G23 step 13): `main` tracks
`origin/vendor`, 3 ahead (Merge branch 'side', Start the side branch, Main work) and 1 behind (Add the
vendor secret): left panel `main` ↑ `3` ↓ `1`; Pull's tooltip `Pull from origin/vendor
(Ctrl+Shift+L)`; rows and lines as IG1's.

**IG2: IG0 after G24's merge** (setting off). "Merge branch 'vendor'"
`63b8fe1713a49735d84bb334cf1b86c66cef8cdb` (scenario facts 4.4; parents `c49cac4`, `133a796`; Test
Author, 2026-01-02 09:00 as author and committer date, D72). 8 rows, 3 lanes (Message x 454, Date x
580 to 700).

| Row | Labels | Node: lane, colour | Message | Date | SHA | y centre |
|---|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 (a merge) | Merge branch 'vendor' | 2026-01-02 09:00 | `63b8fe1` | 129 |
| 1 | `stash@{0}` | 2, 2 (ring) | On main: Stash the secret | 2026-01-01 12:06 | `c879568` | 155 |
| 2 | `origin/main` | 0, 0 | Main work | 12:05 | `c49cac4` | 181 |
| 3 | `tools` | 2, 3 | Add the build file | 12:04 | `c8446bb` | 207 |
| 4 | `vendor`, `origin/vendor`, `origin/vendor-remote` | 1, 1 | Add the vendor secret | 12:03 | `133a796` | 233 |
| 5 | | 0, 0 (lanes 1 and 2 end here) | Stop tracking the secret | 12:02 | `39bd803` | 259 |
| 6 | | 0, 0 | Track the secret | 12:01 | `1ac759d` | 285 |
| 7 | | 0, 0 | Initial commit | 12:00 | `2e4599b` | 311 |

Lines: lane 0 (colour 0) from row 0 to row 7; from row 0's node a line into lane 1 (colour 1), through
rows 1 to 3 into row 4's node, then on into row 5's node; lane 2 (colour 2) dashed from row 1's ring
into row 2's node; lane 2 (colour 3) from row 3's node into row 5's node. Left panel: Local branches 3
(`main` accented with ↑ `2`, `tools`, `vendor`), Remotes 3, Stashes 1. `secret.env` `vendor\n`; the
status `!! build/out.bin` only.

**IG3: IG2 after G24's checkout of `tools`**: IG2's rows, lanes and lines, with row 0's label `main`
(no check mark) and row 3's `tools` ✓; left panel `tools` accented; `CurrentBranch` `tools`. HEAD
`refs/heads/tools` at `c8446bb…`; the working tree `.gitignore`, `README.md`, `app.txt` (`app\n`),
`build` (a file, `build\n`); `build/out.bin` and `secret.env` gone; the status prints nothing.

**"refspecs"** (`Scenarios.RenamingRefspecAsync`, folder and tab `refspecs`), as scenario facts 3.1
and 3.2 give it: bare `origin.git`, `fork.git`, `upstream.git` beside it by relative URLs; fetch
lines `origin` `+refs/heads/*:refs/remotes/origin/*` and `+refs/pull/*/head:refs/remotes/origin/pr/*`,
`fork` `+refs/heads/*:refs/remotes/fork/up/*`, `upstream` `+refs/pull/*/head:refs/remotes/upstream/pr/*`;
HEAD on a clean `main` (`8394a5e`) tracking `origin/main` (0, 0); `topic` (`de4a87c`) tracking
nothing. Remotes: `origin.git` `feature/x` and `main` at `8394a5e`, `refs/pull/1/head` at `de4a87c`;
`fork.git` `main` `8394a5e`; `upstream.git` `main` `8394a5e`, `refs/pull/7/head` `de4a87c`. Full
SHAs: Initial commit `cc31e4ae22a8e076d63050ef2e87c59113ae1f62`, Add the parser
`8394a5e2e769c4601161cbce8c239ff2aa246eb9`, Work on the topic
`de4a87cd5a84d5ed135863187487f81fe89d030c`. The mappings (scenario facts 3.2, 10.1's `SourceOf` with
WP-RA's reader, `RepoRefs.Refspecs` read): `fork/up/main` Mapped to fork's `refs/heads/main`, a
renamed branch, local name `main`; `origin/pr/1` AmbiguousRefs (R2), local name `pr/1`;
`upstream/pr/7` Mapped to `refs/pull/7/head`, not a branch (R4), local name `pr/7`;
`origin/feature/x` and `origin/main` identities. G25's expectations need WP-RA's reader: without it
`SourceOf` is phase 5's assumption (scenario facts 8, item 7).

**RS0, "refspecs" as built** (scenario facts 3.1). 1 lane; rows 0 `topic`, `origin/pr/1`,
`upstream/pr/7`, Work on the topic, 12:02, `de4a87c`, y 129; 1 `main` ✓, `fork/up/main`,
`origin/feature/x`, `origin/main` (drawn `main` ✓, `+3`), Add the parser, 12:01, `8394a5e`, y 155; 2
Initial commit, 12:00, `cc31e4a`, y 181; lane 0 rows 0 to 2. Left panel: Local branches 2 (`main`
accented, `topic`), Remotes **5** (the section counts remote branches,
GD16), folders before leaves in each: `fork` › `up` (closed) › `main`; `origin` › `feature` (closed) ›
`x`, `pr` (closed) › `1`, then `main`; `upstream` › `pr` (closed) › `7`; Pull requests 0, Tags 0,
Stashes 0. `CurrentBranch` `main`.

The graphs G25's steps make (each laid out by the app's `GraphLayout` on the state plain git made; one
lane, the rows' messages, dates and SHAs as RS0's):

| Graph | After | Row 0's labels | Row 1's labels | Left panel | `CurrentBranch` |
|---|---|---|---|---|---|
| RS1 | the drop's push (step 2) | `topic`, `fork/up/main`, `origin/pr/1`, `upstream/pr/7` | `main` ✓, `origin/feature/x`, `origin/main` | as RS0 (5 remote branches) | `main` |
| RS2 | `pr/1` made and checked out (steps 4, 6) | `pr/1` ✓, `topic`, `fork/up/main`, `origin/pr/1`, `upstream/pr/7` | `main`, `origin/feature/x`, `origin/main` | Local branches 3: `pr` (open: it holds HEAD's branch) › `1` (accented), then `main`, `topic` | `pr/1` |
| RS3 | `main` checked out again (step 5) | `pr/1`, `topic`, `fork/up/main`, `origin/pr/1`, `upstream/pr/7` | `main` ✓, `origin/feature/x`, `origin/main` | Local branches 3: `pr` (closed: it no longer holds HEAD's branch), `main` (accented), `topic` | `main` |
| RS4 | fork's `main` deleted (part B, step 10) | `topic`, `origin/pr/1`, `upstream/pr/7` | `main` ✓, `origin/feature/x`, `origin/main` | Remotes 4: `fork` (no branch under it, its `up` folder gone); `origin` › `feature` (closed), `pr` (closed), `main`; `upstream` › `pr` (closed) | `main` |
| RS5 | `pr/7` made and checked out (step 11) | `pr/7` ✓, `topic`, `origin/pr/1`, `upstream/pr/7` | `main`, `origin/feature/x`, `origin/main` | Local branches 3: `pr` (open) › `7` (accented), then `main`, `topic`; Remotes 4 | `pr/7` |

**The app's reads before git** (Keep ignored files on; outputs NUL-separated, written `\0` here; every
output the same on 2.36 and 2.55; the design runs the reads with `GIT_OPTIONAL_LOCKS=0`, 9.3 and D47,
which is the design's description here, not an assertion: the call log keeps no environment, so
that is `IgnoredInTheWayTests`' concern, 9.17):

| Read | Operation (step) | Calls in order (exit 0 unless said) and their output | Calls |
|---|---|---|---|
| TR1 | Merge vendor into main (G23 step 2), 9.4 | `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD refs/heads/vendor --` → `secret.env\0`; `git check-ignore --stdin -z` (input `secret.env\0`) → `secret.env\0`; `git merge-base --all HEAD refs/heads/vendor` → `39bd8031f98ec9a1a6d02519ffabd35ca693bc63`; `git diff-tree -r -z --no-renames --name-only --diff-filter=AMT 39bd8031f98ec9a1a6d02519ffabd35ca693bc63 refs/heads/vendor --` → `secret.env\0` (the merge writes it: kept) | 4 |
| TR2 | Rebase main onto vendor (G23 step 3), 9.6 | side by side: `git merge-base --is-ancestor 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f HEAD` → exit 1; `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f --` → `secret.env\0`; `git log --no-renames --name-only -z --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f..HEAD --` → `\x1ec49cac40cc05ce7ebe709555621751082c868662\0\napp.txt\0`; `git rev-list --min-parents=2 --max-count=1 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f..HEAD --` → nothing; then `git check-ignore --stdin -z` (input `app.txt\0secret.env\0`) → `secret.env\0` (`app.txt` is tracked) | 5 |
| TR3 | Cherry-pick Add the vendor secret (G23 step 4), 9.5 | `git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT` (input `133a796ef1cfa3b4f2dc3237b5342751f85fcc3f\n`) → `secret.env\0`; `git check-ignore --stdin -z` → `secret.env\0` | 2 |
| TR4 | Revert Stop tracking the secret (G23 step 5), 9.5 | the same with `-R` last (input `39bd8031f98ec9a1a6d02519ffabd35ca693bc63\n`) → `secret.env\0` (its deletion reversed); `git check-ignore --stdin -z` → `secret.env\0` | 2 |
| TR5 | Apply, then pop, `stash@{0}` (G23 steps 6, 7), 9.8 | D179's count first: the stash's three reads, once in the session (kept by the stash's SHA): `git diff-tree -r -z --no-renames --name-status c8795683e3b86ad70873326b9897659e3c5140dd^1 c8795683e3b86ad70873326b9897659e3c5140dd` → `A\0secret.env\0`; `git diff-tree -r -z --no-renames --name-only c8795683e3b86ad70873326b9897659e3c5140dd^1 c8795683e3b86ad70873326b9897659e3c5140dd^2` → `secret.env\0`; `git rev-parse --verify --quiet c8795683e3b86ad70873326b9897659e3c5140dd^3` → exit 1 (no untracked part); a status read (nothing local): count 0; then `git check-ignore --stdin -z` (input `secret.env\0`) → `secret.env\0` | 1 after the count |
| TR6 | The pull's rebase on IG1, under `pull.rebase` `merges` and `true` (G23 steps 10, 11), 9.10 and 9.6 | `git rev-parse --verify --quiet HEAD` → `6e68a258092ca05b31cd7d3a8c359276bafc2bf7`; `git merge-base --is-ancestor 6e68a258092ca05b31cd7d3a8c359276bafc2bf7 FETCH_HEAD` → exit 1 (not a fast-forward: the rebase is chosen); then side by side: `git merge-base --is-ancestor refs/remotes/origin/main HEAD` → exit 0; `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD refs/remotes/origin/main --` → nothing; `git log --no-renames --name-only -z --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT refs/remotes/origin/main..HEAD --` → nothing (Start the side branch changes no path, the merge is left out); `git rev-list --min-parents=2 --max-count=1 refs/remotes/origin/main..HEAD --` → `6e68a258092ca05b31cd7d3a8c359276bafc2bf7` (a merge in the range); then (5) `git rev-list --topo-order --boundary refs/remotes/origin/main..HEAD --`, asserted as the app reads it (`BranchPointsWriteAsync`): the first line is the tip `6e68a258092ca05b31cd7d3a8c359276bafc2bf7`, the lines without `-` are the set {`6e68a258092ca05b31cd7d3a8c359276bafc2bf7`, `bf49f82d2de88c816e12a4ef8560c983a2c4c8db`} and the boundary lines the set {`-1ac759d23fd78a78f7660d289a1373ea85378b69`, `-c49cac40cc05ce7ebe709555621751082c868662`}; on 2.36 (asserted only there) exactly `6e68a258…` / `bf49f82d…` / `-1ac759d2…` / `-c49cac40…` in this order, as 2.55 printed it too; (6) `git diff-tree --stdin --no-commit-id -r -z --no-renames --name-only --diff-filter=A` (input one line `<boundary> <tip>` per boundary commit in (5)'s order, on 2.36 `1ac759d23fd78a78f7660d289a1373ea85378b69 6e68a258092ca05b31cd7d3a8c359276bafc2bf7\n` and `c49cac40cc05ce7ebe709555621751082c868662 6e68a258092ca05b31cd7d3a8c359276bafc2bf7\n`) → `secret.env\0` in either order (the branch point Track the secret has it, the tip lacks it; Main work adds nothing the tip lacks); `git check-ignore --stdin -z` (input `secret.env\0`) → `secret.env\0` | 7 after the two |
| TR7 | The pull's merge on IG1 with `origin/main` (G23 step 12), 9.10 and 9.4 | `git rev-parse --verify --quiet HEAD` → `6e68a25…`; `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --` → nothing (the source, Main work, adds nothing `main` lacks): no candidate, so no `check-ignore` and no base read (9.4 step 2: one read); 9.4's base read for an ancestor that tracks the path (0.4's U4) is not reached here | 1 after the first |
| TR8 | The pull's merge on IG1v (G23 step 13), 9.10 and 9.4 | `git rev-parse --verify --quiet HEAD` → `6e68a25…`; `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --` → `secret.env\0`; `git check-ignore --stdin -z` → `secret.env\0`; `git merge-base --all HEAD FETCH_HEAD` → `39bd8031f98ec9a1a6d02519ffabd35ca693bc63`; `git diff-tree -r -z --no-renames --name-only --diff-filter=AMT 39bd8031f98ec9a1a6d02519ffabd35ca693bc63 FETCH_HEAD --` → `secret.env\0` | 4 after the first |
| TR9 | The pull's fast-forward under `pull.ff=only` on IG1v (G23 step 14), 9.10 | TR8's five calls, then `git merge-base --is-ancestor 6e68a258092ca05b31cd7d3a8c359276bafc2bf7 FETCH_HEAD` → exit 1 (diverged: nothing counts as in the way) | 5 after the first |

The fetch of each pull, `git fetch --progress origin`, prints nothing and changes no ref (`origin.git`
has nothing new); its `FETCH_HEAD` marks the branch `branch.main.merge` names for merge: `c49cac4…
branch 'main' of ../origin` with `origin/main` (TR6, TR7), `133a796… branch 'vendor' of ../origin`
on IG1v (TR8, TR9), the other two lines `not-for-merge`.

**What git does without the app's check** (derived on fresh copies, 2.36, replayed on 2.55 with the
same result; G24 shows the first two, the rest go into the checks' Notes):

| Operation | Command | Result |
|---|---|---|
| Merge vendor (G24) | `git merge --no-edit --no-log -m "Merge branch 'vendor'" refs/heads/vendor` | exit 0, TG9; `63b8fe1…`; `secret.env` = `vendor\n` (LOCAL lost) |
| Check out tools after it (G24) | `git switch --no-guess tools` | exit 0, TG10; `build/out.bin` deleted, `build` a file; `secret.env` removed (tracked in the merge, absent in `tools`) |
| Check out vendor (S21's) | `git switch --no-guess vendor` | exit 0; `secret.env` = `vendor\n` (scenario facts 4.4) |
| Rebase onto vendor, pick, revert, stash apply and pop | scenario facts 4.4 | each overwrites `secret.env` |
| The pull's rebase keeping merges on IG1 | `git rebase --rebase-merges` | exit 0 (`Rebasing (1/8)` … on 2.36, `(1/6)` … on 2.55, then `Successfully rebased and updated refs/heads/main.`); HEAD stays `6e68a25…`; **`secret.env` gone** (reset to the branch point writes it, the next `reset onto` deletes it) |
| The pull's plain rebase on IG1 | `git rebase` | exit 0, `Rebasing (1/1)`, `Successfully rebased and updated refs/heads/main.`; `main` linear at `82c7ae4e6fe58dea9fdb879001bd0d60bf8c6b5b` (Start the side branch replayed onto Main work); `secret.env` kept: the app refuses all the same (9.6's last sentence, GD7) |
| The pull's merge on IG1v | `git merge --no-edit FETCH_HEAD` | exit 0, `Merge made by the 'ort' strategy.`; `secret.env` = `vendor\n` |

#### Texts these checks rely on (part G)

**The ignored-files texts** (design 9.15, 14.4; D179, D196): checkouts git refused for ignored files
only, the error title `Checkout blocked`, git's output and command below, no "Stash changes and check
out": `1 ignored file is in the way: {name} tracks a file of the same name. Move it away first.` /
`{N} ignored files are in the way: {name} tracks files of the same names. Move them away first.`
(`OperationTexts.IgnoredFilesInTheWay`), `{name}` the branch, the remote branch, the tag, the short
SHA (9.11's `plan.Name`) or, for "Create branch", the start as the dialog names it. The app's own
refusals, no git output, the operation's own error title: `1 ignored file is in the way of the
{operation}: {file}. Move it away first.` / `{N} ignored files are in the way of the {operation}:
{first names}. Move them away first.` (`IgnoredFilesInTheWayOf`; `{operation}` `merge`, `rebase`,
`squash`, `cherry-pick`, `revert`; at most three names, then `, …`); and `1 ignored file is in the way
of applying the stash: {file}. Move it away first.` (`IgnoredFilesInTheWayOfStash`). Error titles:
`Merge failed`, `Rebase failed`, `Cherry-pick failed`, `Revert failed`, `Applying the stash failed`,
`Pull failed` (the pull with `merge` or `rebase`, 9.10); the status bar `{Title} failed`.

**Pull** (4.8, 9.10, 14.1, D211): the title `Pull`, running `Pulling {up} into {b}…`, error title
`Pull failed`; `PullNotFastForward` `{b} and {up} have diverged, and pull.ff is set to only: merge {up}
into {b} or rebase {b} onto {up}.`, its command `git merge --ff-only --no-overwrite-ignore FETCH_HEAD`
(`git merge --ff-only FETCH_HEAD` with the setting off); `{b} is already up to date` (phase 3's
`PullUpToDate`). The toolbar's `Pull from {up} (Ctrl+Shift+L)` and `Pop the newest stash
(Ctrl+Shift+O)` (14.11).

**Refspecs** (10.5, 14.1, 14.2, 14.5; D201, D222): R2 `{T} is fetched from more than one ref of {r}`;
R4 `{T} is {ref} on {r}, not a branch`; `CreatedWithoutUpstream` `Created {n} at {T} without an
upstream: {T} is fetched from more than one ref of {r}` (no full stop); `PushWouldStopFetch`
`Pushing {A} to {target} would stop every fetch of {r}: {D} is fetched from more than one ref of
{r}.` under `Push failed`; under a renaming refspec, for the drop's push, the operation's title `Push
{A} to {T}` (GD24; the same words as the drop menu's item, whose label is unchanged), running `Pushing
{A} to {T}…` and result `Pushed {A} to {T}`; `Delete {y} on {r}…`, `Delete {y} on {r}?`, `Delete the branch {y} on {r}? Everyone who
uses {r} loses it.`, `Deleting {y} on {r}…`, `Deleted {y} on {r}`, the confirm button `Delete on {r}`;
`{y} is already checked out` (10.4 #6). An upstream is named by its remote-tracking ref when the
fetch lines map it to one, as `git status` names it, else `{remote}/{remote ref}` (GD15): Pull's
`Pull from upstream/pr/7 (Ctrl+Shift+L)` for `pr/7`, which tracks `refs/pull/7/head` on `upstream`.

**Phase 3 to 5's texts that stay** (2.33, D113): titles `Check out {name}`, `Create branch {b}`,
`Merge {source}`, `Rebase {b} onto {target}`, `Cherry-pick {short}`, `Revert {short}`, `Apply
stash@{n}`, `Pop stash@{n}`, `Push {b}` (every push but the drop's onto a renamed remote branch, GD24:
the toolbar's, the menus', the push dialog's `Push pr/1` of G25 step 7, an identity drop's), `Delete
{y} on {r}`; results `Merged {source} into {current}`,
`Checked out {b}`, `Checked out {b}, which has no upstream`, `Created {b} tracking {T} and checked it
out`; the name dialog's `Create branch`, `From {name}`, "Check out the new branch" and "Create
branch"; the push dialog's `Push {b}`, "To", "Set as upstream", "Cancel", "Push"; the drop menu's
`{A} → {B}`, `Push {A} to {B}`, `Push {A} to {B} and track it`, `Check out {A} and rebase it onto {B}`,
`Check out {A} and rebase it onto {B} interactively…`, `Compare {A} with {B}`, `{A} is up to date with
{B}`; the remote branch menu's items (`RepositoryMenus.ForRemoteBranch`); the stash menu's;
`A backup is saved first, so this can be undone.`

**Git's texts** (Git 2.36.0.windows.1, standard error unless said; ⇥ is a tab, " / " separates the
lines `ErrorOutput` shows after `GitOutputCleaner.Clean`):

- **TG1** (`git switch --no-guess --no-overwrite-ignore vendor`, also `git switch --create
  vendor-remote --track --no-overwrite-ignore refs/remotes/origin/vendor-remote`, `git switch
  --create vendor-copy --no-overwrite-ignore refs/heads/vendor` and `git switch --detach
  --no-overwrite-ignore 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f`, exit 1, standard output empty):
  `error: The following untracked working tree files would be overwritten by checkout:` (key) /
  `⇥secret.env` / `Please move or remove them before you switch branches.` / `Aborting`. 2.55:
  identical.
- **TG2** (`git switch --no-guess --no-overwrite-ignore tools`, exit 1): `error: Updating the
  following directories would lose untracked files in them:` (key) / `⇥build` / `Aborting` (git prints
  an empty line before `Aborting`, which the cleaning drops). 2.55: identical.
- **TG3** (`git merge --ff-only --no-overwrite-ignore FETCH_HEAD` on IG1v, exit 128, standard output
  empty): `fatal: Not possible to fast-forward, aborting.` (key). 2.55: nine `hint:` lines before it
  (`hint: Diverging branches can't be fast-forwarded, you need to either:` … `hint: Disable this
  message with "git config set advice.diverging false"`, scenario facts 2.6).
- **TG4** (`git push --progress fork refs/heads/topic:refs/heads/main`, exit 0): `Enumerating objects:
  4, done.`, the counting, compressing and writing progress, `Total 3 (delta 0), reused 0 (delta 0),
  pack-reused 0`, `To ../fork.git`, `   8394a5e..de4a87c  topic -> main` (key); the lines `Delta
  compression using up to {n} threads` and `Writing objects: 100% (3/3), 314 bytes | {rate} KiB/s,
  done.` depend on the machine and are not asserted. 2.55: `pack-reused 0 (from 0)`.
- **TG5** (`git switch --create pr/1 --no-track --no-overwrite-ignore refs/remotes/origin/pr/1`, exit
  0): standard output empty; `Switched to a new branch 'pr/1'` (key). 2.55: identical.
- **TG6** (`git switch --no-guess --no-overwrite-ignore main` from `pr/1`, exit 0): standard output
  `Your branch is up to date with 'origin/main'.`; standard error `Switched to branch 'main'` (key);
  then `git switch --no-guess --no-overwrite-ignore pr/1`: standard output empty, `Switched to branch
  'pr/1'` (key). 2.55: identical.
- **TG7** (`git push --progress --force-with-lease=refs/heads/main:8394a5e2e769c4601161cbce8c239ff2aa246eb9
  fork --delete refs/heads/main`, exit 0): `To ../fork.git` / ` - [deleted]         main` (key). 2.55:
  identical.
- **TG8** (`git switch --create pr/7 --track --no-overwrite-ignore refs/remotes/upstream/pr/7`, exit
  0): standard output `branch 'pr/7' set up to track 'upstream/refs/pull/7/head'.`; standard error
  `Switched to a new branch 'pr/7'` (key). 2.55: identical.
- **TG9** (G24's `git merge --no-edit --no-log -m "Merge branch 'vendor'" refs/heads/vendor`, exit 0,
  standard output): `Merge made by the 'ort' strategy.` (key) / ` secret.env | 1 +` / ` 1 file
  changed, 1 insertion(+)` / ` create mode 100644 secret.env`. 2.55: identical, the same SHA.
- **TG10** (G24's `git switch --no-guess tools`, exit 0): standard output empty; `Switched to branch
  'tools'` (key). 2.55: identical.
- **TG11** (`git merge --no-edit --no-overwrite-ignore FETCH_HEAD` on IG1 with `origin/main`, exit 0,
  standard output): `Already up to date.` (key). 2.55: identical.
- **TG12** (`git fetch --progress origin` of every G23 pull, exit 0): nothing on either stream. 2.55:
  identical.

### Part L: the look: every main screen in both themes, the light colours, the long folder and the tab strip

Part L covers design sections 11.10 to 11.14 (the light status and danger colours, `VcDangerTextBrush`,
tooltips through Fluent's keys, the light lane colours of D198, and the sweep of every main screen
in both themes), 13.2 (a long folder in the welcome page's errors and the scan dialog, drawn by
`PathTextBlock`) and 13.3 with 13.4 (the tab strip's overflow, with a dashboard's tab): checks L26 to
L34 of design 17.4. The sweep (L26 to L31) pictures each screen of 11.14's table in pairs, `-dark` and
`-light`, through `ShotInBothThemesAsync`, so that each pair also proves that D194 follows the
system's change on that screen, both ways, with its menu, dialog, page, drag or tooltip open; L32
pictures the long folder of phase 1's check 11 and the clone error of its check 14 in both themes,
asserted on the drawn lines; L33 shrinks and then scrolls the strip of the Web dashboard and its
repositories' tabs; L34 is the real window's own launch with `"theme": "Light"`, whose four captures
are compared with twins L31 takes (11.8, D224). Following the system's change in the real window is
not automated: it is the by-hand line of the acceptance checklist (11.8). What 11.14 leaves
unpictured stays so (the clone in progress, the graph's reload bar, the choice and finishing
dialogs, the passphrase, host-key and prompt dialogs, the overflowing strip in the light theme).

This part was written before the UI it tests (D15), from `docs/phase-6-design.md` sections 2.5 to
2.7, 5.3 to 5.6, 6.1, 6.7, 6.8, 7.1 to 7.4, 7.7, 8.6, 8.7, 8.10, 11, 12.8, 12.9, 13, 14, 15.3, 16.1 and
17, decisions D187 to D231, the earlier phases' reports for every screen that stays as it was (phase
1's checks 10, 11 and 14; phase 2's 17; phase 3's 2, 7, 11, 13, 17, 21, 22, 23, 28, 29 and 31; phase
5's B6 to B9, F12, F14 and U16), and the scenario facts. Every git state below was derived with Git
2.36.0.windows.1 on copies built by the merged scenario code (the scenario facts' copies, copied
again into a folder of this part's own before anything was run in them) and every git text replayed
with Git 2.55.0.windows.5 ("How the expected git states were derived", part L). Values the design
leaves open are this part's decisions LD1 to LD24.

#### Conventions of part L

- **Project, class, files.** The scripted checks are in their own walk-through project,
  `tests/VisualCommit.VisualTests.Phase6Look` (the lead's ruling 1: by 17.3's rule, the walk-through
  having more than 40 methods, part L moves there whole), class `Phase6ChecksL` (`Phase6Checks.L.cs`,
  with `Phase6Checks.L.Sweep.cs`, `.L.Strip.cs` as the topics need), what they compare against in
  `Phase6Expectations.L.cs` (which the real-window pass links: its pattern
  `..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs` takes this project's file too) and its
  tests in `Phase6ExpectationTests.L.cs`. The project links the base (`Phase6Checks.cs`), the support
  (`Phase6Checks.Support.cs`) and the common expectations (`Phase6Expectations.Common.cs`) of
  `VisualCommit.VisualTests.Phase6`, as `Phase5Undo` links `Phase5`'s, and saves its pictures in phase
  6's one folder; WP-V's part L creates it by WP-S's recipe. The real-window method of L34 is in
  `Phase6RealWindowPass.L.cs`, its helpers in `RealApp.Phase6*.cs`.
- **One test method per app start** (phase 3's rule, `FreshApplication`), each on a fresh copy of
  its scenario; part L has nine scripted methods (L31 two) and one real-window method (L34), listed
  under "Test methods and app starts (part L)". The longest come nearer the hang watchdog's 3 minutes
  than earlier phases' methods: L27 takes 16 captures and makes 16 flips (8 pairs; a pair flips twice),
  L31's "ops" method 13 captures and 12 flips, L28 12 and 12. At 17.6's 4.8 s of UI-thread time per
  picture (an upper bound here: a pair's second picture costs a flip and a capture, not a step, 17.6's
  "the sweep's flips lighter") and its wall-to-UI ratios (1.67 idle, 2.6 at the gate), L27 takes up to
  about 77 s of UI time, 128 s on the idle laptop and 200 s at the gate's load, L31's "ops" method up
  to about 62, 104 and 162 s; phase 3's methods of 12 and 13 pictures ran 35 to 75 s
  (`artifacts/visual/phase-3/timings`, the last step's time). So L27 is timed alone with `bash
  scripts/time-scripted-check.sh "*Check_L27*" 6` as soon as it is written (L31's "ops" method with
  it), in `tests/VisualCommit.VisualTests.Phase6Look`: the script sends a filter that names part L
  there, as it sends phase 5's part U to `Phase5Undo` (design 17.3), and its step times land in phase
  6's one `timings` folder. If L27 takes more than about 90 s alone, it is split into two methods,
  each one start on a fresh "ops": steps 1 to 5 (the graph, its menus and tooltips) and steps 6 to 8
  (the dialogs), with the same pictures and expectations, one method more in `Phase6Look` (the
  projects' counts are under "Conventions of every part"). `Phase6Look` runs in a test process of its
  own beside `VisualCommit.VisualTests.Phase6`, on its own UI thread, so part L's UI-thread time does
  not add to parts W, K, S and G's.
- **Window, theme, scenario.** Every method runs at 1100×700 but for L33's first step (1000×700).
  The sweep's methods (L26 to L31) and L32 write `"theme": "System"` into `settings.json` and start
  with the fake system theme dark (`ShellDriver.Start(…, systemDark: true)`, 17.2), through
  `Phase6Checks.OpenAsync(…, AppTheme.System, systemDark: true)` or, on "Web",
  `OpenWorkspaceAsync(data, copy, 1100, 700, AppTheme.System)`; L33 starts with no `theme` key (Dark,
  the default, D194); L34 launches the real app with `"theme": "Light"`. The app's start-up log then
  has `Theme: System (the system's theme is dark).` (11.5), and every sweep picture is taken in a
  window whose `settings.json` says `"System"`, but L31's last step, which saves Light.
- **The pairs** (11.14; `Phase6Checks.Support.cs`'s `ShotInBothThemesAsync(app, name, keepPointer)`,
  17.2). Each pair is one call: (1) the window shows dark (`ShellDriver.ShowsDarkTheme`, the
  application's `ActualThemeVariant` Dark) and the token pixel of LD1 has its dark value; `Shot(name +
  "-dark")`; (2) `SetSystemThemeAsync(false)`: it waits until the theme service's `IsDark` is false
  and the layout has settled; the variant is Light, the token pixel has its light value, the log has
  one more line `The system's theme changed to light; the app follows it.`, `settings.json` still
  says `"System"`, the toolbar's `ThemeSwitch` shows `IconMoon` with the tooltip and help text `Switch
  to the dark theme (stops following the system)`; `Shot(name + "-light")`; (3)
  `SetSystemThemeAsync(true)`: Dark again, the token pixel dark, the log line `The system's theme
  changed to dark; the app follows it.`, `ThemeSwitch` with `IconSun` and `Switch to the light theme
  (stops following the system)`, and **the screen still up**: the same dialog is the top dialog
  (`DialogHostViewModel.Current`), the same menu is open (`MainWindowViewModel.Menu.IsOpen`, its
  header unchanged), the same tooltip is open (`ToolTip.GetIsOpen`), the same page is shown, the drag
  still runs (`DragLayer` holds its pill), as the step's state was. A pair's two pictures differ only
  in colour: the checks assert the same texts, bounds (±0.5) and states in both (the step's
  expectations are asserted twice, once per variant, the colours by variant). `Shot` refuses a name
  it has already saved in the run (17.2). Where the step says `keepPointer`, the pointer stays where
  the step put it (on a tooltip's button, a menu item, in a drag); otherwise `Shot` first moves it to
  the neutral point.
- **The neutral point** (760, 687) lies at 1100×700 in the status bar, where nothing has a tooltip
  or a hover look (phase 3's point); on a dashboard tab before any run the status bar holds only the
  workspace's icon and name at the left and `Ready`, so the point is clear there too (no run runs in
  part L; the scenario facts' note on a running run does not apply). At 1000×700 (L33's first
  picture) it lies in the status bar as well. Every capture is preceded by the move to the neutral
  point (but for `keepPointer`), and so is every key that opens the palette or the settings screen
  (L31 steps 3 and 7, L34 steps 4 and 5): the palette's highlight follows the pointer (5.8), and a
  pointer left over its list by a click on a dialog's button could take the highlight from the first
  row, which a later move does not give back.
- **Colours** are written by token with their values (dark / light, phase 6's light values after
  11.10, 11.12 and 11.13; "Colours by token" under "Texts these checks rely on"). A text's colour is
  asserted as "contains `#RRGGBB`" within the text's bounds (phase 1's `ContainsColour`: at least one
  pixel exactly that colour; anti-aliasing makes the others lighter); a tooltip's text, which its popup
  draws with subpixel smoothing so that no pixel need be exactly its colour, by `TextInk.Problems`
  (`tests/VisualCommit.Testing/TextInk.cs`: each channel's extreme toward the text colour within the
  text's rectangle lies within 8 of it, and more than 8 from the colour it replaced where one is
  given); a background or a line as the
  exact colour at a sampled point clear of text, as phases 1 to 5 sample. Translucent things (the drag
  pill at 85 %, a backdrop) are asserted within ±8 per channel of the blend the step gives.
- **Waits** are on conditions, never on a fixed time: `WaitUntilReadyAsync` and the graph drawn
  (`Phase6Checks.OpenAsync`), `WaitForIdleAsync` (its refresh included), `WaitForDashboardReadAsync`
  (2.10's pass line, no `Reading…`), the clean-up's record (`WaitForOperationRecordAsync("Clean up
  backups")`, phase 5's), `WaitForPageAsync<TView>()` with the row count the step names,
  `WaitForGraphReloadAsync()` (phase 5), `WaitForToolTipAsync(control)` (phase 3), `Menu()` (polls
  until placed), `WaitForDialogAsync<T>()`, the queue's log line `<repo>: <Title>: <outcome> after <N>
  ms: <message>`, and `SetSystemThemeAsync`'s own wait. "No dialog", "no menu" and "no tooltip" are
  phase 3's absence rules.
- **Menus** are written as phase 3's report writes them: header first, "—" for a separator,
  "(danger)" an enabled item in `VcDangerBrush`, "(disabled: "…")" a disabled item with its reason,
  "(highlighted)" the item with the focus, the first enabled one when a menu opens (D105); a keyed
  item (6.7) is written "label [key]", its key right-aligned 24 or more after the label in
  `TextBlock.vcMenuKey` (`VcTextSecondaryBrush`). A menu that fits is 8 + 24 + 26 × items + 9 ×
  separators high (±2) and lies at least 4 px inside the window; opened by a right-click its top-left
  corner is 2 right of and 2 below the pointer, flipped and moved to stay inside (phase 3's 4a).
- **Dialogs** are phase 3's: a card on `VcPanelBackgroundBrush` with a 1-px `VcBorderBrush` border
  over the backdrop `VcBackdropBrush` (dark `#99000000`, light `#66000000`), centred, 400 wide (460
  for the wide card: the Remotes, Pick and Add repositories dialogs); the palette has no backdrop
  (5.2); the settings screen's card is 7.1's.
- **Git's own texts.** Only L32's clone error shows git's words. It is asserted whole against the
  same `git clone --progress` run by the tests' own git on the same URL and cleaned as the app cleans
  it (L32 step 2's `CloneErrorTextAsync`; phase 1's `GitCloneErrorAsync` gives only the first `fatal:`
  line, for check 14's `Assert.Contains`), so it holds on every git; on Git 2.36 and
  2.55, which print it identically, it is also asserted word for word as "Scenarios and graphs of part
  L" gives it, and on every git its key line ends `does not appear to be a git repository`. Git's
  states (refs, files, the index, the backups, `.git/config`, the session's global file) are asserted
  on every version, by `ExactStateAsync` (phase 5) where a step says "nothing changed". CI's gits
  (2.56 on Windows and macOS, 2.55 on `ubuntu-latest`, 2.43 in the containers) follow this rule.
- **Keys** are written as Windows and Linux show them (`Ctrl+Shift+F`), and so are the expected key
  texts in every scripted run: the headless platform's command modifier is Control on every platform,
  CI's macOS job included (design 0.7; `KeyTexts.Current` is `Ctrl` there, 6.4), so the expectations
  write the `Ctrl` literals (6.12), and the macOS column's words a step names (`Cmd+Shift+F`, `Cmd+,`;
  Redo `Cmd+Shift+Z`) are what a real Mac shows (the macOS checklist). The keys the steps press follow
  6.1's rows the same way: a key 6.1 writes with Mod (Ctrl+Shift+P and Ctrl+, in L31 steps 3 and 7 and
  L33 step 2) is pressed with the platform's command modifier (`HotkeyConfiguration.CommandModifiers`,
  as `ShellDriver.CommandModifier` reads it: Control in every headless run), as is the Ctrl+click that
  adds a row to the selection (phase 3's 4h; L27 step 2, L28 step 4); Ctrl+Tab and Ctrl+PageDown stay
  Control on every platform (6.1 row 7; L33 step 5). L34 runs on Windows only.
- **Temporary folders.** A copy's folder (`%TEMP%\VisualCommit.Tests\<label>-<12 hex>`) differs in
  every run and on every machine: texts that show one are asserted with the copy's own path in place
  of `{…}`, and a picture that shows the random part varies by nature, which its check's Notes say.
- **Pictures** are named `l<check><step letter>-<what>` (17.3's `PictureName()` rule,
  `^[gklsw]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`), the sweep's with `-dark` and `-light` at the
  end; letters run in step order through a check's methods, but for `l31d-settings-light`, step 7's,
  which keeps the name design 11.8 and 17.4 give it (L34's twin, named after the pair
  `l31d-settings-system`) and so comes after `l31e` and `l31f`; every name is unique in the report. They
  are saved under `artifacts/visual/phase-6/scripted/`; L34's captures, their difference pictures and
  `run.txt` under `artifacts/visual/phase-6/real-window/`.
- **The real window** (L34 only) runs in a real process with the real mouse and keyboard (FlaUI),
  finds controls by automation id and name, reads texts and states through UI Automation, never
  changes the operating system's theme or any other setting of the desktop and never starts an
  editor (11.8, D224): it starts the app with an explicit Light, which does not depend on the owner's
  mode. Each capture, and each key that opens the palette or the settings screen, is preceded by the
  real mouse's move to the neutral point (LD15). Each capture is compared with its scripted twin with
  D33's limit (at most 3 % of the pixels differing), the share in `run.txt`. 1100×700 fits the development machine's work area (1500×952
  logical pixels at 200 %); nothing of part L runs at 1920×1080.

#### Test methods and app starts (part L)

Every scripted method below is in `tests/VisualCommit.VisualTests.Phase6Look`, class `Phase6ChecksL`
(the lead's ruling 1); L34's is in the real-window project.

| Check | Test method (one app start) | Scenario and set-up, before the app starts | Window, theme | About |
|---|---|---|---|---|
| L26 | `Check_L26_welcome_page_in_both_themes` | No tab; the recent list `graph` (newest), then `linear` (fresh copies of `Scenarios.GraphAsync()` and `Scenarios.LinearAsync()`); `workspaces.json` with "Web" (`Scenarios.WorkspaceAsync()`'s copy, `copy.ToWorkspace()`, written by `ShellDriver.WriteWorkspaces`) | 1100×700, System, fake dark | 2 pairs |
| L27 | `Check_L27_graph_menus_tooltips_and_dialogs_in_both_themes` | One tab on a fresh "ops" (`Scenarios.OperationsAsync()`) | 1100×700, System, fake dark | 8 pairs |
| L28 | `Check_L28_changes_pages_and_views_in_both_themes` | One tab on a fresh "history" (`Scenarios.HistoryAsync()`); `docs/todo.md` written with `# To do\n`, untracked (LD7) | 1100×700, System, fake dark | 6 pairs |
| L29 | `Check_L29_reflog_and_backups_in_both_themes` | One tab on a fresh "reflog" (`Scenarios.ReflogAsync()`) | 1100×700, System, fake dark | 2 pairs |
| L30 | `Check_L30_dashboard_in_both_themes` | A fresh "Web" copy; `OpenWorkspaceAsync` writes "Web" and a session of its dashboard | 1100×700, System, fake dark | 3 pairs |
| L31 | `Check_L31_overlays_and_settings_in_both_themes` | One tab on a fresh "ops" | 1100×700, System, fake dark; Light saved in the last step | 6 pairs and 1 single |
| L31 | `Check_L31_sign_in_and_held_fetch_in_both_themes` | `AuthRemoteServer.StartForVisualChecksAsync(port: AuthRemoteServer.Phase6LookVisualChecksPort)` (47394, the fallback to a free port allowed) with `RequireAuth = true` and the credentials `alice` / `s3cret`; one tab on `Scenarios.AuthRemoteAsync(server)` | 1100×700, System, fake dark | 2 pairs |
| L32 | `Check_L32_long_folder_in_both_themes` | An empty data folder; a plain folder `TempDirectory("plain")`; a parent `TempDirectory("clones")` | 1100×700, System, fake dark | 2 pairs |
| L33 | `Check_L33_tab_strip_shrinks_then_scrolls` | A fresh "Web" copy; "Web" written; a session of eight tabs: the dashboard (`TabState(null, "web")`), then `api`, `web-app`, `docs`, `mobile`, `billing`, `infra`, `scratch` (`TabState(copy.PathOf(name))`), `ActiveTab` 7 (LD21) | 1000×700, then 1100×700; Dark | 5 single pictures |
| L34 | `Check_L34_light_overlays_against_their_twins` (real window, `Phase6RealWindowPass.L.cs`) | A new data folder with `"theme": "Light"` and a session of one tab on a fresh "ops"; `RealApp.Launch(data, session)` | 1100×700, Light | 4 captures |

#### Scenarios and graphs of part L

The scenario facts give the graphs and states this part starts from: "ops" (OPS0, scenario facts 6),
"history" (H0, phase 5's scenario facts 2), "reflog" (RL0 and its 15 backups after the clean-up,
phase 5's scenario facts 4), "auth" (scenario facts 6), "Web" (scenario facts 1 and 2.1 to 2.7) and
the window geometry (scenario facts 1). Beyond them:

**OPS0 in part L.** 8 rows, 3 lanes (Graph column 64), Message x 454 (126 wide), Date x 580 to 700;
a row clicked "in its Message column" at (484, 129 + 26 r): row 1 y 155, 2 181, 3 207, 4 233, 5 259,
6 285, 7 311. Nodes: lane 0 x 406, lane 1 x 422, lane 2 x 438; row 1 (`feature/conflict`, Conflicting
change) lane 1, colour 1; row 2 (`feature/ff`, Fast-forward me) lane 2, colour 2; rows 4 and 5
(`feature/alpha`, `origin/feature/alpha` drawn `feature/alpha` and `+1`) lane 2, colour 3. Toolbar:
Fetch, Pull, Push, Branch, Pop, Search, Settings and Theme enabled; Stash disabled (`There are no
changes to stash`); Undo and Redo disabled (`Nothing to undo`, `Nothing to redo`). `git merge-base
--is-ancestor refs/heads/feature/alpha HEAD` exits 1 (so its deletion's question has a second
sentence). `origin`'s URL is the copy's own `origin.git` folder, an absolute path (`{ops}\origin.git`,
`{ops}` the copy's folder `…\ops-<12 hex>`). **No step of L27 or L31 part A changes git**: L27 ends
every drag with Esc and closes every dialog unconfirmed; L31's toolbar Fetch, `git fetch --progress
origin`, prints nothing and exits 0 on 2.36 and 2.55 (no new objects; refs unchanged), its deletion
question is cancelled, and its settings writes touch only `settings.json`. Each method reads
`ExactStateAsync` at its start and asserts it equal at its end, and that `git for-each-ref
refs/visualcommit/backup` prints nothing.

**H0t: "history" with `docs/todo.md`** (L28's start, LD7). H0's rows below the working-changes row
(display row 0, centre y 129; its ring in lane 0, colour 0, its dashed line into row 0's node); commit
row r's centre y 155 + 26 r, clicked at (468, 155 + 26 r): row 0 `c0bc2dd` y 155, row 4 `c1b6375`
(lane 1, colour 1, node (422, 259)), row 6 `245c395` (lane 1, colour 2, node (422, 311)), row 7
`0575c7f` y 337. `git status --porcelain=v1 --untracked-files=all`: ` M src/Reader.cs`, `?? docs/todo.md`.
The stage panel: Unstaged files (2): A `todo.md` `docs`, M `Reader.cs` `src`; Staged files (0).
Blobs: `src/Reader.cs` in HEAD `7061debb42034635a05bb901042e55362d23c470`, in the working tree
`3056624e938122ca4a98893380cdbeb2dfec5a0b`; its diff is phase 5's scenario facts 2.2 (two hunks,
`@@ -1,6 +1,6 @@` and `@@ -19,4 +19,6 @@ public sealed class Reader`).

**H0d: after the discard of `src/Reader.cs`** (L28 step 2). The app saves its snapshot and writes the
file back (phase 2's D62, D67): `refs/visualcommit/backup/discard-20260102T090001000Z` (the first
backup of the session by the fixed backup clock) at `0bd36b6c3aded07487326d5d4f98d9eb72d11334`, the
commit plain git makes with parent `c0bc2dd`, author and committer `VisualCommit
<snapshot@visualcommit.invalid>` at D72's dates, subject `VisualCommit: snapshot before discarding 1
file`, body `src/Reader.cs`, whose tree is HEAD's with `src/Reader.cs` = `3056624` (derived here with
`read-tree`, `update-index`, `write-tree` and `commit-tree`: the id phase 5's U16 part C gives for the
same discard); `src/Reader.cs` is `7061deb` again; status `?? docs/todo.md` only; Unstaged files (1):
A `todo.md` `docs`; the working-changes row stays (D62's bar lives in the stage panel). **H0r: after
Restore** (step 2), which runs as the discard's Undo since the file has not changed since the discard
(phase 5 design 7.9, UD18): `src/Reader.cs` is `3056624` again (` M src/Reader.cs`, `?? docs/todo.md`, as
H0t); the backup ref stays, and no other is made; no other ref changes.

**HS1: H0t with `feature/compare` hidden and `main` soloed** (L28 step 6). The graph's log is, after
Hide, `git log --date-order --format=%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%s --no-show-signature
--no-color --encoding=UTF-8 --exclude=feature/compare --branches --exclude=origin/feature/compare
--exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --` and, after Solo, the same with the
revisions `--ignore-missing refs/heads/main refs/remotes/origin/main HEAD --` (phase 5 design 8.2;
`Hidden` stays while solo mode is on, 8.1). Both give, with Git 2.36 (`git log --date-order --format='%h
%p %s' --ignore-missing refs/heads/main refs/remotes/origin/main HEAD --` on a copy), 12 commits:
`c0bc2dd` Read whole lines, `20d7fbe` Stop tracing, `e765529` Write new notes, `f513566` Merge
branch 'feature/readme' (parents `884b462`, `c1b6375`), `c1b6375` Explain the reader, `884b462` Remove
the notes, `0575c7f` Rename the parser to reader, `14a5c6c` Add tracing, `365482b` Handle empty input,
`775113f` Add the logo, `1d5a7cf` Add the parser, `7829335` Initial commit. Laid out: the
working-changes row (display row 0); commit rows 0 to 11 at y 155 + 26 r, 2 lanes (Graph 48); row 0
`main` ✓, `origin/main` (drawn `main` ✓ and `+1`, the soloed refs' labels, D159's solo rule), node
(0, 0); row 4 Explain the reader (1, 1); row 5 Remove the notes (0, 0; lane 1 ends here); every other
row (0, 0). Lines: lane 0 (colour 0) rows 0 to 11; lane 1 (colour 1) from row 3's node into row 4's
node, then into row 5's node. Compare whole lines and Add the comparer are not rows; no label
`feature/compare` or `origin/feature/compare`. Left panel (both `feature` folders open): Local
branches 2: `feature` › `compare` (name in `VcTextDisabledBrush`, `RefHiddenToggle` with `IconEyeOff`
at its right), `main` (accent colour, `RefSoloToggle` with `IconSolo` in `VcAccentBrush`); Remotes 2:
`origin` › `feature` › `compare` (disabled colour, `RefHiddenToggle`), `main` (`RefSoloToggle`); Pull
requests 0, Tags 0, Stashes 0. The session's `GraphFilters` entry for the repository: Hidden
`refs/heads/feature/compare`, `refs/remotes/origin/feature/compare`; Soloed `refs/heads/main`,
`refs/remotes/origin/main`; Upstreams (`refs/heads/feature/compare`,
`refs/remotes/origin/feature/compare`) and (`refs/heads/main`, `refs/remotes/origin/main`); HiddenRemotes
none. Git: `git for-each-ref` and `.git/config` byte-identical to the start (a filter changes no ref).

**RL0 in part L.** At 1100×700 the reflog page lists HEAD's 36 entries (no `HEAD@{5}`), rows 26 high
from y 124 (row k's centre y 137 + 26 k), rows 0 to 19 whole in view, the lost marks on `HEAD@{1}` (y
163), `HEAD@{9}` (y 345), `HEAD@{11}` (y 397) and `HEAD@{17}` (y 553) in view and on `HEAD@{23}`,
`HEAD@{33}` below it, the footer y 648 to 674; the backups list 15 rows, row 14 at y 501 (phase 5's
B8 and B9, decisions BD1 to BD5). Nothing of L29 changes git after the clean-up at opening, which
deletes two backup refs before L29's baseline is read (L29 step 1).

**"Web" in part L.** L30 and L33 use the copy as built: L30's rows are scenario facts 2.3's, its row
menu 2.3's old-site menu, and its scan scenario facts 2.7's with every candidate already in "Web"
(LD12): the checklist lists `api`, `billing`, `design-system`, `docs`, `infra`, `mobile`, `notes`,
`scratch`, `web-app`, each `Already in this workspace` (checked, disabled), 8 rows in view (`FoundList`
`MaxHeight` 232) and `web-app` below them; the button `Add repositories`, disabled. Nothing is added
and no git call writes anything: the scan reads the file system and opens each candidate
(`OpenAsync` and `OpenedAtTopAsync`, reads only).

**AUTH0 and its held fetch** (L31 part B). One row, `main` ✓ and `+1` (`origin/main`) on Initial commit
`e4a56bd`; `origin` = `http://127.0.0.1:47394/remote.git` (`server.Url`; the fallback's port in its
place, LD18); the served `main` one commit further (Remote work `e6761c7`). A fetch of it asks git's
two prompts `Username for 'http://127.0.0.1:47394': ` and `Password for 'http://alice@127.0.0.1:47394':
` (scenario facts 2.5's form) and counts 5 objects: `remote: Counting objects:  20% (1/5)`, `40%
(2/5)`, `60% (3/5)`, `80% (4/5)`, `100% (5/5)` (phase 3's "Percentages"); `HoldNextFetchPack(60)`
splits the pack response after the line `60% (3/5)` (D150's split), so the status bar shows exactly
`Fetching from origin… Counting objects 60%` while it is held. A cancel in that window (the pack
not yet received) leaves every ref as it was and no lock file (phase 3's check 17); a prompt cancelled
by the user ends the fetch as Cancelled (phase 3 design 2.16). After L31 part B: `refs/remotes/origin/main`
still `e4a56bd`, `git status -sb` `## main...origin/main`, no `*.lock` under `.git`.

**L32's folders and git's text.** `{plain}` is `TempDirectory("plain").Path`
(`%TEMP%\VisualCommit.Tests\plain-<12 hex>`; on the development machine
`C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests\plain-<12 hex>`). The clone's URL is `new
Uri(clones.Combine("does-not-exist")).AbsoluteUri` (`file:///C:/Users/…/clones-<12 hex>/does-not-exist`,
phase 1's check 14). The app runs `git clone --progress -- <url> cloned` in `{clones}`; Git 2.36.0 and
2.55.0 alike (run here on a folder of this part's own) print on standard error `Cloning into
'cloned'...`, `fatal: '<path>' does not appear to be a git repository`, `fatal: Could not read from
remote repository.`, an empty line, `Please make sure you have the correct access rights`, `and the
repository exists.`, exit 128, `<path>` the URL's path with a drive letter and `/` separators
(`C:/Users/…/clones-<12 hex>/does-not-exist`), and leave no folder `cloned`. `GitOutputCleaner.Clean`
drops the `Cloning into` line and the empty line, so `CloneError` holds four lines. On macOS and Linux
`<path>` is the rooted Unix path (`/tmp/…`, `/var/folders/…`). `PathTextBlock`'s drive pattern matches
`C:/…` up to the closing `'` (excluded by `[^\s"'<>|?*]`); the Unix pattern matches after the `'`.

**L33's strip** (13.3's rules; every natural width at least 120, LD21). Room `A` = width − 38. Natural
widths: the dashboard `Web` clamp(69 + 26.9, 120, 220) = 120; `api`, `web-app` (51 + 53.2), `docs`,
`mobile`, `billing`, `infra`, `scratch`, `notes`, `New tab` 120; `design-system` 51 + 87.9 = 138.9 → 139
(scenario facts 1).

| State | Tabs (left to right) | Room | Widths | `Offset`, "+" |
|---|---|---|---|---|
| TS8 (1000×700) | Web, api, web-app, docs, mobile, billing, infra, scratch | 962 | 120 each (Σ 960), tab k at x 120 k | 0; "+" x 962 to 996 |
| TS8w (1100×700) | the same | 1062 | 120 each | 0; "+" x 962 to 996 |
| TS9 | + design-system | 1062 | 118 each (c = 118, Σ 1062) | 0; "+" x 1064 to 1098 |
| TS10 | + notes | 1062 | 107, 107, then 106 × 8 (c = 106.2) | 0; "+" 1064 to 1098 |
| TS11 | + New tab | 1062 | 97 × 6 (Web to billing), 96 × 5 (infra to New tab) (c = 96.5) | 0; "+" 1064 to 1098 |
| TS12 | + New tab | 1062 | 89 × 6, 88 × 6 (c = 88.5) | 0; "+" 1064 to 1098 |
| TS13 | + New tab | 1062 | 82 × 9, 81 × 4 (c = 81.7) | 0; "+" 1064 to 1098 |
| TS14 | + New tab (the last active) | 1062 | 80 each (Σ 1120 > 1062: scrolls) | 58: tab k at x 80 k − 58, the first tab x −58 to 22 (its last 22 px), the last x 982 to 1062; "+" 1064 to 1098 |
| TS14f | the same, the first active | 1062 | 80 each | 0: tab k at 80 k, the last x 1040 to 1062 shown (22 px), clipped; "+" 1064 to 1098 |

A repository tab's title has its width less 51 (69 at 120, 67 at 118, 56 or 55 at 107 or 106, 46 or 45
at 97 or 96, 38 or 37 at 89 or 88, 31 or 30 at 82 or 81, 29 at 80), the dashboard's less 69 (51, 49,
38 or 37, 28 or 27, 20 or 19, 13 or 12, 11): `Web` (26.9) is whole at 120 to 97 and cut with "…" from
89 down, "…" alone after the icon at 80 (0.8); `web-app` (53.2) whole down to 106 and cut at 97 and
below; `design-system` (87.9) cut at every width of TS9 to TS14. Which other titles are cut is not
asserted (their widths were not measured); each tab's tooltip and automation name is its whole title.

#### Texts these checks rely on (part L)

**The theme** (11.4 to 11.7, 14.11, 14.14): start-up `Theme: Dark.` / `Theme: Light.` / `Theme: System (the
system's theme is {dark|light}).`; following `The system's theme changed to {light|dark}; the app follows
it.`, ignoring `The system's theme changed to {light|dark}; the app keeps the {dark|light} theme.`;
`Theme changed to {theme}.` (`UseTheme`); `ThemeSwitch` `Switch to the light theme` / `Switch to the dark
theme`, while System `Switch to the light theme (stops following the system)` / `Switch to the dark theme
(stops following the system)`; the settings screen's `Dark`, `Light`, `System`, `System follows your
operating system's light or dark mode.`; the palette's `Use the dark theme`, `Use the light theme`,
`Follow the system's theme` (the chosen one left out).

**Tooltips with keys** (6.8, 14.11, 14.12): `Fetch all remotes ({key})`, on a dashboard `Fetch every
repository of {workspace} ({key})` and `Pull every repository of {workspace} ({key})`; `Hide the activity
log ({key})`; `Read the repositories of {workspace} again ({key})`; `Open, clone or init a repository
({key})`; a disabled button its reason alone (`There are no changes to stash`); keys `Ctrl+Shift+F`,
`Ctrl+Shift+L`, `Ctrl+Shift+K`, `Ctrl+B`, `Ctrl+Shift+S`, `Ctrl+Shift+O`, `Ctrl+Z`, `Ctrl+Y`, `Ctrl+F`,
`Ctrl+Shift+M`, `Ctrl+Shift+R`, `Ctrl+J`, `Ctrl+T`, `F5`, `Ctrl+,` (macOS: 6.1's column).

**The welcome page and the tab strip** (13.1, 14.9): `Workspaces`; `New workspace…` (tooltip `Create a
workspace: repositories you check and update together`); `No workspaces`; an item's name and `{N}
repositories` / `1 repository` / `No repositories`, its tooltip the folder names joined by `, `, at most 8,
then `, …`; the page in reading order `No repository open`, `Open, clone or init a repository to see its
history.`, `Open`, `Clone`, `Init`, `Workspaces`, `New workspace…`, `No workspaces` or the items, `Recent
repositories`, `No recent repositories` or the items; phase 1's clone form `Clone a repository`,
`Repository URL`, `Parent folder`, `Browse`, `Folder name`, `Clone`, `Cancel`; `WelcomeError` `{path} is not
a git repository.`; a dashboard tab's title, tooltip and automation name the workspace's name, its
`ItemStatus` `Workspace`; "+" named `Open a new tab`; `New tab`.

**The dashboard** (2.5 to 2.7, 14.3, 14.10): `{workspace}`, `{n} repositories`; `Add repository…` (tooltip
`Add a repository to {workspace}`), `Fetch all`, `Pull all`, `Refresh`, `More`; headings `Repository`,
`Folder`, `Branch`, `Ahead / behind`, `Changes`, `State`, `Operation`; the cells of scenario facts 2.3;
`Ready`; the row menu `Open`, `Fetch`, `Pull`, `Show in folder`, `Copy path`, `Move up`, `Move down`,
`Locate…`, `Remove from workspace`, reasons `{path} does not exist`, `It is the last repository`;
`Select a repository to see its activity`; the log's `{workspace}: Context menu opened: {name} by pointer`.

**The scan dialog** (1.9, 14.6): `Add repositories to {workspace}`; `Found {N} repositories in {folder}.`;
`Already in this workspace`; `Cancel`; `Add {N} repositories` / `Add 1 repository`, disabled `Add
repositories`; the folder dialog's title `Add a repository to {workspace}`.

**The palette** (5.3, 5.4, 5.7, 14.7): placeholder `Search commands, branches, tags, stashes, remotes,
repositories and workspaces`; `No matches`; the commands `Fetch all remotes`, `Pull`, `Push`, `Create
branch…`, `Stash changes…`, `Pop the newest stash`, `Undo` / `Undo {text}`, `Redo` / `Redo {text}`, `Search
commits`, `Go to the commit message`, `Show reflog`, `Show backups`, `Refresh`, …; the items `Open
repository {name}`; reasons `There are no changes to stash`, `Nothing to undo`, `Nothing to redo`, `There
are no changes to commit`; the log `{repo}: Command palette opened`.

**The settings screen** (7.1 to 7.4, 7.7, 8.6, 8.7, 8.10, 14.8): `Settings`; `General`, `Git`, `Tools`,
`Keyboard shortcuts`; General's texts as L31 step 4 quotes them (7.3); `Cancel`, `Save`; Git's `All
repositories (global)`, `This repository`, `Saved in your global git configuration, which git in a terminal
uses too.`, `Name`, `Email`, placeholder `Not set`, `Pull`, `Not set`, `Merge (fast-forward when possible)`,
`Merge, always with a merge commit`, `Rebase`, `Rebase, keeping merge commits`, `Fast-forward only`, `Merge
tool`, `Not set`, `Choose…`, `Used by Merge tool… in the conflict resolver, and by git mergetool in a
terminal.`; the Pick dialog `Choose a merge tool`, `Filter`, `Not set` — `Merge tool… stays disabled`,
`{tool}` — `found on this computer` / `not found on this computer`, `Choose`.

**Earlier phases' texts on these screens, unchanged:** phase 2's diff view (`Unstaged`, `Inline`, `Side by
side`, `Stage file`, `Discard file`, `Stage hunk`, `Discard hunk`), its question `Discard changes?` /
`Discard the changes to {file}?`, the restore bar `Discarded changes to {file}.` with `Restore`; phase 3's
menus (as the steps quote them), `3 commits selected`, `Right-click a selected commit for what can be done
with all of them.`, the name dialog (`Create branch`, `From {short} "{subject}"`, `Branch name`, `Check out
the new branch`, `Create branch`), the push dialog (`Push {b}`, `To`, `Set as upstream`, `Push`), the
remotes dialog (`Remotes`, `Edit`, `Remove`, `Add remote`, `Close`), the deletion question (`Delete
branch?`, `Delete the branch {b}?` and ` It has commits that {cur} does not have.`, `A backup is saved first,
so this can be undone.`, `Cancel`, `Delete`), the credentials dialog (`Sign in to {host}`, `Git needs a user
name and a password or token for {url}.`, `User name`, `Password`, `Cancel`, `Sign in`), the status bar's
`Fetching from origin… Counting objects {N}%`, `{n} waiting`, `Fetch origin cancelled`, `Fetch cancelled`,
`Fetched from all remotes`, Cancel's `Stop git`; phase 5's pages (`Blame of {name}`, `in the working tree`,
`Not committed yet`, `Reflog of HEAD`, `Backups`, `1 entry is not shown: it names no commit that exists`),
`Compare`, `From {short} {subject}`, `To {short} {subject}`, `All files ({N})`, `Hide {b}`, `Solo {b}`,
the entry text `discard of {file}` (in `Undo {text}` and `Redo {text}`, the operation `Undo {text}`
and the status `Undid {text}`; `restore of the discarded files`, recorded only when the file changed
since the discard, phase 5's U16 step 15, does not show in part L), `Nothing to undo`, the activity log's `Activity log`,
`Show reads`.

**Colours by token** (`Theme/Tokens.axaml`; phase 6's light values after 11.10, 11.11, 11.12 and 11.13 in
bold; dark unchanged, D198):

| Token | Dark | Light |
|---|---|---|
| `VcWindowBackgroundBrush` | `#14161B` | `#FFFFFF` |
| `VcPanelBackgroundBrush` | `#1A1D24` | `#F5F6F8` |
| `VcChromeBackgroundBrush` | `#20242C` | `#EBEDF1` |
| `VcControlBackgroundBrush` | `#272C36` | `#FFFFFF` |
| `VcHoverBackgroundBrush` | `#2F3542` | `#DEE2E9` |
| `VcBorderBrush` | `#323845` | `#D2D6DE` |
| `VcTextPrimaryBrush` | `#E4E7EC` | `#1B1F27` |
| `VcTextSecondaryBrush` | `#9BA3B0` | `#5C6572` |
| `VcTextDisabledBrush` | `#5F6775` | `#A1A8B3` |
| `VcAccentBrush`, `VcDragTargetBrush`, lane 0 | `#7C8CFF` | `#4353D8` |
| `VcAccentTextBrush`, `VcLabelTextBrush` | `#10121A` | `#FFFFFF` |
| `VcSuccessBrush` | `#3FB97F` | **`#187248`** (was `#1E8E5A`) |
| `VcWarningBrush` | `#E0A23B` | **`#895A17`** (was `#B7791F`) |
| `VcDangerBrush` | `#E5606B` | **`#B7323D`** (was `#C93A46`) |
| `VcDangerTextBrush` (new) | `#FFFFFF` | `#FFFFFF` |
| `VcSelectionBrush` | `#2A3150` | `#DDE1FA` |
| `VcRowHoverBrush` | `#1C1F27` | `#F3F4F7` |
| `VcLane1Brush` | `#3FB97F` | **`#1C8655`** (was `#1E8E5A`) |
| `VcLane2Brush` | `#E0A23B` | **`#A16A1B`** (was `#B7791F`) |
| `VcLane3Brush` | `#E5606B` | `#C93A46` |
| `VcLane4Brush` | `#4FB3D9` | **`#1D7EA6`** (was `#1F86B0`) |
| `VcLane7Brush` | `#8FB84A` | **`#59811C`** (was `#5F8A1E`) |
| `VcBackdropBrush` | `#99000000` | `#66000000` |
| `VcDiffAddedBrush`, `VcDiffAddedWordBrush` | `#1A2E24`, `#2B5A3F` | `#E6F6EC`, `#B4E5C6` |
| `VcDiffRemovedBrush`, `VcDiffRemovedWordBrush` | `#331D23`, `#6A2D37` | `#FBE9EB`, `#F3BAC1` |
| `VcDiffHunkBrush` | `#1C2130` | `#EEF0FB` |
| `TextControlBackground`, `TextControlBorderBrush`, `…Focused` | `#272C36`, `#323845`, `#7C8CFF` | `#FFFFFF`, `#D2D6DE`, `#4353D8` |
| Fluent's `ToolTipBackground`, `ToolTipForeground`, `ToolTipBorderBrush` (set in `Tokens.axaml`) | **`#1A1D24`, `#E4E7EC`, `#323845`** (were `#2B2B2B`, `#FFFFFF`, about `#1C1C1C`) | **`#F5F6F8`, `#1B1F27`, `#D2D6DE`** (were `#F2F2F2`, `#000000`, about `#D0D0D0`) |
| C# in the diff and blame (Dark+ / Light+, D64): keywords, operators | `#569CD6`, `#D4D4D4` | `#0000FF`, `#000000` |

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). Each check is named by its part's
letter and its number in design 17.4: W1 to W9, K10 to K13, S14 to S21, G22 to G25 and L26 to L34, the
numbers running on across the parts. A check's scenario, theme and window size are named at the end of
its "Check" column, its test methods and app starts in its Steps and in its part's "Test methods and app
starts"; the graphs and states a row names are defined in its part's conventions and scenarios
sections (part W's W0 to WP and TD and TR, part K's OPS0 to K-DOCS, part S's ST0 to STR, part G's IG0
to IG3 and RS0 to RS5, part L's H0t to TS14f), and the texts it relies on under its part's "Texts these
checks rely on". Part W writes a dashboard row in a cell as `name` · branch · ahead / behind · changes ·
state · operation (its conventions' order, `·` for the columns). Which steps the real-window pass
repeats (RW), or "n/a" with the reason, is said in each row's Notes; "Scripted" and "Real window" are
filled in when the gate runs. "Screenshots" lists the planned pictures, the scripted run's and then the
real window's.

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| W1 | Workspaces from the welcome page (design 1.3 to 1.9, 1.11, 2.1, 2.2, 2.5, 2.6, 2.10 trigger 6, 3.5, 13.1, 13.2): the welcome page's Workspaces section with none; a new "Web", whose dashboard takes the empty tab's place, empty; `Add repository…` on `api`, added at once, selected and read with no F5; on the copy's root the scan's checklist of nine with `api` already in it, `Add 8 repositories`, the eight read with no F5; the section again, listing "Web", whose item activates the open dashboard; then, on "Web" less web-app and docs with their tabs open, `Add open tabs` (the two rows following their tabs, no git call), `Rename workspace…` and `Delete workspace…` (the dashboard closes, nothing on disk changes); `workspaces.json` and `session.json` after each change; "workspace", dark, 1100×700 | **Part A** (`Check_W1_workspace_from_the_welcome_page`): 1. "Workspace" scenario, a fresh data folder with no `workspaces.json` and no session. Start the app (`ShellDriver.Start(data.Path, 1100, 700)`), writing nothing. Wait until ready. Screenshot. 2. Click `NewWorkspaceButton`; the dialog read (`NameBox` empty with its placeholder, `Create` disabled, no `NameError`). Type `Web` (decision WD9). Screenshot. 3. Click `Create` (`ConfirmButton`). Wait until the dashboard shows. Screenshot. 4. Queue `copy.PathOf("api")` in the fake folder dialog (`FakeFolderPicker.Answer`); click the empty state's `Add repository…` (`DashboardEmptyAddRepository`). Wait for the read pass (`WaitForDashboardReadAsync`); no F5. Screenshot. 5. Queue `copy.Root`; click the header's `Add repository…` (`DashboardAddRepository`). Wait until the checklist's search is done (`ScanFolderText` starts `Found`). Screenshot. 6. Click `Add 8 repositories` (`ConfirmButton`). Wait for the read pass; no F5. Screenshot. 7. Click "+" (`AddRepoButton`). Wait until the new tab's welcome page shows. Screenshot. 8. Click the new tab's `WorkspaceItem` `Web`; settle.<br>**Part B** (`Check_W1_add_open_tabs_rename_and_delete`, a fresh copy, a new start): 9. Before the start read `ExactStateAsync` of the nine repositories and list `{root}`'s folders; then write "Web" with WB8's eight paths and the session [the dashboard `TabState(null, "web")`, web-app's tab, docs' tab], the dashboard active (`ShellDriver.WriteWorkspaces`, `StartWithTabs`). Start; wait for the dashboard's read pass and until web-app's and docs' tabs have opened in the background (`WaitForTabOpenedAsync`: each history loaded, `<repo>: working tree:` logged, its queue idle with the clean-up at opening). Click `DashboardMore`. Screenshot. 10. Note the call log's and the app log's marks (`mark`). Click `Add open tabs`; settle; read the snapshot. Wait until rows 8 and 9 show their branches. Screenshot. 11. Click `DashboardMore`; the menu read; press Esc. Click `DashboardMore`, then `Rename workspace…`; the dialog read (`NameBox` `Web`, its text selected, `Rename` disabled). Type `Web team` (decision WD16; the typing replaces the selected text). Screenshot. 12. Click `Rename`. Click `DashboardMore`, then `Delete workspace…`. Screenshot. 13. Click `Delete` (`ConfirmButton`). Wait until the strip holds two tabs. Screenshot. | **After step 1:** one tab `New tab` (`RepoTab`) without a close button; the welcome page, its texts in reading order (`VisibleTexts.In`): `No repository open`, `Open, clone or init a repository to see its history.`, `Open`, `Clone`, `Init`, `Workspaces`, `New workspace…`, `No workspaces`, `Recent repositories`, `No recent repositories` (13.1). Between `Init`'s row and `Recent repositories`, in the stack's 8-px spacing: the heading row, its top 24 below the buttons' row (the stack's 8 and its own 16), 20 high: `WorkspacesHeading` `Workspaces` (11, SemiBold, secondary) at the stack's left, `NewWorkspaceButton` at its right (a 12×12 `IconPlus`, 4, `New workspace…`, secondary, no background; enabled; automation name `New workspace`; tooltip `Create a workspace: repositories you check and update together`); then, 8 below it, `NoWorkspacesText` `No workspaces` (secondary). No `WorkspacesNotice`. The stack 392 wide (x 284 to 676) and centred vertically in the graph area (y 88 to 674: its top and bottom gaps equal ±1); the section 68 high (13.1: 8 + 16 + 20 + 8 + 16). The stack's 34-px move against phase 0's `01-shell-dark-1100x700` is 17.7's comparison by region (its step 3), not this check's: no bound of this run gives an earlier picture's layout. The toolbar is an empty tab's: Settings (`Open settings (Ctrl+,)`) and Theme enabled, every other button disabled. The data folder holds no `workspaces.json` (nothing changed yet, 1.3).<br>**After step 2:** the name dialog over the welcome page: `NameDialogTitle` `New workspace`; no `NameDialogContext` (decision WD28) and no `DialogRepository` (decision WD11); read before the typing: `NameBox` empty with the keyboard focus and its placeholder `Workspace name`, `Create` disabled (an empty name), no `NameError`; after it `NameBox` `Web`, no `NameError` (`DialogText.WorkspaceNameError` finds nothing, 1.6), `Cancel` and `Create`, `Create` enabled.<br>**After step 3:** the strip holds one tab, the dashboard: the welcome page's empty tab became it (2.2 step 2), so no `New tab` is left; the tab as the conventions give it (`DashboardTab`, `Web`, `Workspace`, `TabIcon`, primary, 120 wide), "+" after it. The dashboard spans x 0 to 1100: no left panel, no right panel, no splitters (2.4). `DashboardHeader` y 88 to 124 (`VcChromeBackgroundBrush`, a 1-px `VcBorderBrush` line at its bottom): `IconWorkspace` 14×14 (secondary) from x 8, `DashboardTitle` `Web` (13, SemiBold, primary) from x 28, `DashboardCount` `No repositories` (secondary) 10 after it; at the right, left to right and 6 apart, `DashboardAddRepository` `Add repository…` (enabled, tooltip `Add a repository to Web`), `DashboardFetchAll` `Fetch all` and `DashboardPullAll` `Pull all` (both disabled, tooltip `Web has no repositories`), `DashboardRefresh` `Refresh` (enabled, tooltip `Read the repositories of Web again (F5)`) and `DashboardMore` (28×28, `IconMore`, name and tooltip `More`) ending at x 1092; no run text, bar or `Cancel all`. Below the header no column headings and no list (decision WD2): the empty state `DashboardEmpty`, centred in the area below the header, at most 440 wide: `Web has no repositories yet.` (secondary, centred), then side by side `Add repository…` (`DashboardEmptyAddRepository`, enabled) and `Add open tabs` (`DashboardEmptyAddOpenTabs`, disabled, its tooltip `No tab has a repository open`, decision WD21). The toolbar: Fetch and Pull disabled with the tooltip `Web has no repositories` (4.10); Push, Branch, Stash, Pop, Undo, Redo and Search disabled without a tooltip; Settings and Theme enabled. The status bar: `StatusWorkspaceIcon` and `Web`; `Ready` (secondary); `ActivityLogToggle` disabled, tooltip `Web has no repositories` (2.9's first reason). `workspaces.json`: `version` 1 and one workspace, `id` 32 lowercase hex digits (decision WD26), `name` `Web`, `repositories` empty. `session.json`: one tab, `TabState(null, <that id>)`, `ActiveTab` 0.<br>**After step 4:** the fake folder dialog was asked once, titled `Add a repository to Web`; no dialog opened (a working tree's top is appended at once, 1.7). The header `1 repository`; the empty state gone; `DashboardColumnHeaders` (y 124 to 152, `VcChromeBackgroundBrush`, a 1-px bottom line) with `Repository`, `Branch`, `Ahead / behind`, `Changes`, `State` and `Operation` (11, SemiBold, secondary), each 8 into its column (no `Folder` below 1280 wide, 2.6); row 0 (y 152 to 178): `api` · `main` · `Up to date` · `Clean` · empty · empty, W0's cells; selected (`VcSelectionBrush`) and in view. It was read with no F5 (trigger 6): the app log gained `Web: read 1 repository in {ms} ms`; the call log gained only reads, all in api's folder (by `SameFolder`) and none elsewhere: two `OpenAsync` calls (`git rev-parse --is-inside-work-tree …`: the add's own, 1.7, then the row's probe, 2.10 step 3), where the dialog's spelling of the folder is not git's (as on CI) a `git rev-parse --show-prefix` and a `git rev-parse --git-common-dir` after the first, the refs read's five calls and the status read; the check asserts these kinds of call, not their number. TD; the activity-log toggle enabled, `Show every git command the app has run (Ctrl+J)` (a row with a view model selected, 2.9). `workspaces.json`: Web's `repositories` `[{path: api's WorkingDirectory}]`, which `SameFolder` matches to `copy.PathOf("api")`.<br>**After step 5:** the folder dialog was asked a second time, `Add a repository to Web`. `{root}` is no repository (`OpenAsync`: `NotARepositoryException`), so it was scanned (1.7, 1.8). `AddRepositoriesDialog` (the wide card, 460 wide, x 320 to 780, centred) in its done state: `AddRepositoriesTitle` `Add repositories to Web`; `ScanFolderText` `Found 9 repositories in {copy.Root}.`, the folder as the dialog answered it, every drawn line of it but the last ending with a directory separator (`\` on Windows, `/` elsewhere: `Path.DirectorySeparatorChar` or `/`) or a space, and on Windows none `C:` alone (13.2's and 13.5's rule, `DrawnLines.Of`); no `ScanProgressText`, `ScanStoppedText` or `ScanFailedText`; `FoundList` (rows 28 high, `MaxHeight` 232: eight in view, the ninth below them, not scrolled, decision WD30) with the rows api, billing, design-system, docs, infra, mobile, notes, scratch, web-app (by relative path, `OrdinalIgnoreCase`): api's name (primary), `Already in this workspace` (secondary) and its box checked and disabled; each other row its name (primary), its relative path, the same name (secondary), and its box (`FoundRepositoryCheck`) checked and enabled; no `home`, no `remotes` or `*.git`, no `old-site`; buttons `Cancel` and `Add 8 repositories` (enabled). Nothing appended yet: `workspaces.json` as after step 4. The app log gained nine `Opened the repository at …` lines since step 4 (one per candidate, api's included, 1.8).<br>**After step 6:** the dialog closed; WA: the eight appended in the list's order after api, api still selected (decision WD1); header `9 repositories`; every cell whole. Read with no F5: `Web: read 8 repositories in {ms} ms`. `workspaces.json`: nine paths in WA's order, each that repository's WorkingDirectory (matched by `SameFolder`); no `old-site`.<br>**After step 7:** a new tab `New tab` at index 1, active (the strip [Web, New tab]; the dashboard's tab inactive: its icon and title secondary); its welcome page's texts in reading order `No repository open`, `Open, clone or init a repository to see its history.`, `Open`, `Clone`, `Init`, `Workspaces`, `New workspace…`, `Web`, `9 repositories`, `Recent repositories`, `No recent repositories` (adding opens no tab, so nothing was remembered). `WorkspaceList` holds one `WorkspaceItem` (automation name `Web`), 26 high, the stack's width: `Web` (primary) at its left, `9 repositories` (secondary) at its right; its tooltip `api, billing, design-system, docs, infra, mobile, notes, scratch, …` (at most 8 names, then `, …`). The section 78 high (52 + 26), the stack centred. `session.json`: [`TabState(null, <id>)`, `TabState(null)`], `ActiveTab` 1.<br>**After step 8:** the workspace's dashboard was open, so it was activated (2.2 step 1; D227's click on an item): the strip still [Web, New tab], Web active (its icon and title primary), the `New tab` kept, inactive, at index 1; no repository tab opened (D188) and no second dashboard tab; rows WA, api still selected (the selection is kept in the background, 2.7), the list with the keyboard focus; TD. `session.json`: [`TabState(null, <id>)`, `TabState(null)`], `ActiveTab` 0. Whether trigger 1 reads rows depends on the real time since step 6 (the 10 s rule; W1 has no read clock), so no read is asserted either way; a read would leave the rows' values as they are.<br>**After step 9:** before the click, tabs [Web, web-app, docs], the dashboard first (1.11) and active, its rows WB8, `8 repositories`, no row selected. The menu (`DashboardMenus.ForHeader`) below `DashboardMore`, 4 inside the window's right edge: header `Web`; `Add open tabs` (highlighted); —; `Rename workspace…`; `Delete workspace…` (danger); 119 high.<br>**After step 10:** WB10: web-app (k 8, y 373) `web-app` · `main` · ↑ `2` · `Clean` · empty · empty and docs (k 9, y 399) `docs` · `main` · ↓ `3` · `Clean` · empty · empty, appended in tab order; `10 repositories`; no row selected (decision WD20). The two follow their tabs (3.5): right after the click and a settle no row `IsReading` and no pass under way (decision WD37); `OpenRepositories.Summaries` holds no view model for web-app's or docs' worktree; since `mark` the app log has no `web-app: summary read in` or `docs: summary read in` line and no pass line (decision WD35: both added rows follow tabs, so trigger 6 reads nothing), and the call log has no call in any folder of the workspace (the two tabs, waited for in step 9, had nothing left to read). `workspaces.json`: the eight paths, then web-app's and docs' WorkingDirectory.<br>**After step 11:** the More menu read before Esc: `Add open tabs` (disabled: "Every open repository is in this workspace"); `Rename workspace…` (highlighted); `Delete workspace…` (danger). The rename dialog: `NameDialogTitle` `Rename workspace`; `NameDialogContext` `Rename Web`; no `DialogRepository`; read before the typing: `NameBox` `Web` with the keyboard focus, its whole text selected, `Rename` (`ConfirmButton`) disabled (the trimmed name unchanged, 1.6); after it `NameBox` `Web team`, `Rename` enabled, no `NameError`.<br>**After step 12:** renamed (the dialog closed): the tab's title, tooltip and name `Web team`, the tab clamp(69 + the width of `Web team`, 120, 220) wide (13.4: about 128, taken from its bounds), its title whole; web-app's and docs' tabs follow it, "+" after them; `DashboardTitle` `Web team`; the status bar's branch part `Web team`; Fetch's and Pull's tooltips `Fetch every repository of Web team (Ctrl+Shift+F)`, `Pull every repository of Web team (Ctrl+Shift+L)`; `workspaces.json`: name `Web team`, id `web`, the ten paths unchanged; `session.json`'s dashboard still `TabState(null, "web")` (the id, 1.11). The question (`ConfirmationDialog`): `ConfirmTitle` `Delete workspace?`; `ConfirmQuestion` `Delete the workspace Web team? Its 10 repositories stay where they are: nothing on disk changes.`; `ConfirmDetail` `Its dashboard tab closes.`; no `DialogRepository`; `Cancel` and `Delete` (`ConfirmButton`, the destructive `VcDangerButton`).<br>**After step 13:** the dashboard closed as by its close button (2.1): tabs [web-app, docs], web-app active (index 0), its graph WA0; docs' tab unchanged. `workspaces.json`: `version` 1, `workspaces` empty. `session.json`: [web-app's tab, docs' tab], `ActiveTab` 0. Nothing on disk changed: every repository's exact state equals the one read in step 9 before the start, and `{root}` holds the folders listed then. No undo entry (D154: workspace edits are not in the undo history): web-app's Undo `Nothing to undo`. | | | `w1a-welcome-workspaces`, `w1b-new-workspace-dialog`, `w1c-empty-dashboard`, `w1d-api-added`, `w1e-scan-checklist`, `w1f-nine-rows`, `w1g-welcome-lists-web`, `w1h-more-menu`, `w1i-open-tabs-added`, `w1j-rename-dialog`, `w1k-delete-question`, `w1l-dashboard-deleted`; real window: `w1a-welcome-workspaces` to `w1g-welcome-lists-web` | Design 1.3, 1.5 to 1.9, 1.11, 2.1, 2.2, 2.4 to 2.6, 2.10 (trigger 6), 3.5, 4.10, 13.1, 13.2; scenario facts 2.3, 2.7 and 8 items 1 to 3; D188, D189, D203, D204, D205, D227. `w1e-scan-checklist` varies by nature: its folder line shows the temporary folder (`{copy.Root}`), whose lines the check asserts by 13.2's rule, not by their text; no other picture of W1 draws a folder. `w1b-new-workspace-dialog` and `w1j-rename-dialog` vary by nature too: each is taken with the keyboard focus in `NameBox`, whose caret blinks and may be caught on or off (as phase 4's `13c`, `15b`, `16c` and `b7f` and phase 5's F10, F11 and A4: `ShellDriver` holds only the resolver's caret still, D151); their expected results are structural, and the gate compares them by inspection, not by hash. Two methods (decision WD6): after part A every repository of `{root}` is in "Web", so `Add open tabs` there could add nothing. Decisions WD1, WD2, WD6, WD9, WD11, WD16, WD19 to WD22, WD26, WD28, WD30, WD35, WD37. **RW: part A, steps 1 to 7** (step 8 is scripted only), in a real process on a fresh data folder: the name typed with the real keyboard, every button clicked with the real mouse, and `Add repository…` answered twice through Windows' own folder dialog (`RealApp.ChooseFolderInDialog("Add a repository to Web", <folder>, timeout)`: api's folder, then the copy's root); captures after steps 1 to 7 compared with their scripted twins, `w1e-scan-checklist` within D33's 3 percent though the temporary folder differs; the checklist's rows, the header's count and `workspaces.json` read as in the scripted run. What only the real window proves: Windows' folder dialog answering the app's two requests (found by its title, the folder typed and confirmed) and the scan running on a real disk from it. Part B is scripted only: its menu and dialogs are the menu layer's and phase 3's cards, pictured in the real window by W2's row menu and earlier phases. |
| W2 | The ten repositories, read only (design 1.10, 2.4 to 2.7, 2.10; D204, D206, D207): `Web`, `10 repositories`, no row selected; every row's cells, tones and tooltips (scenario facts 2.3), every text whole; each repository's exact state unchanged and only reads called; an Ahead / behind tooltip; web-app's and old-site's row menus with their reasons; old-site's `Locate…` to the folder's new place (`workspaces.json` holds it, the row read again is Ready); its `Remove from workspace` (no question); Alt+Up; "workspace", dark, 1100×700 | `Check_W2_ten_repositories`: 1. "Workspace" scenario; before the start the check reads the exact state of each of the nine repositories and copies `{root}\notes` file by file to `{root}\archive\old-site` (decision WD7). `OpenWorkspaceAsync`. Screenshot. 2. Rest the pointer on mobile's Ahead / behind cell (row 3: the centre of `DashboardRowAheadBehind`'s arrows and counts, about (330, 243)). Screenshot. 3. Move the pointer to the neutral point. Right-click web-app's row (66, 191). Screenshot. Press Esc. 4. Right-click old-site's row (66, 399). Screenshot. 5. Queue `{root}\archive\old-site` in the fake folder dialog; click `Locate…`. Wait for the row's read (`WaitForRowAsync("old-site", Branch "main")`). Screenshot. 6. Right-click old-site's row (66, 399); the menu read; click `Remove from workspace`. Screenshot. 7. Click docs' row (66, 217); press Alt+Up. Screenshot. 8. Right-click api's row (66, 165); the menu read; press Esc. | **After step 1:** W0: `DashboardTitle` `Web`, `DashboardCount` `10 repositories`; the headings and columns as W1 after step 4; the ten rows at y 152 + 26 k with W0's texts, tones, tooltips and automation names; no row selected; every cell's text whole. Colours: names and branches primary; the arrows, their counts, `Up to date`, `No upstream`, `No remote` and `Clean` secondary; design-system's `1 staged, 2 unstaged, 1 untracked` primary; billing's `1 conflict` danger; `Merge in progress` and `Folder not found` warning; the list's rows on no background (none hovered, none selected). TD; the status bar `Web`, `Ready`, the toggle disabled with `Select a repository to see its activity`. One tab, the dashboard. The read: one pass, `Web: read 10 repositories in {ms} ms` (decision WD27); nine `<name>: summary read in {ms} ms` lines (Debug), none for old-site; no call in old-site's folder (2.10: its folder check found it gone, no process); every call since the start only reads.<br>**After step 2:** the tooltip `1 commit ahead of and 2 behind origin/main` open by the cell, inside the window, in the app's tooltip colours (11.12: `#1A1D24` behind `#E4E7EC`); the cell's automation name `1 ahead, 2 behind`. No row selected (resting selects nothing).<br>**After step 3:** web-app selected (a right-click selects the row, 2.7); its menu (`DashboardMenus.ForRow`) with its top-left corner at (68, 193), 267 high (8 items, 3 separators): header `web-app` (tooltip `{root}\web-app`); `Open` (highlighted); —; `Fetch`; `Pull`; —; `Show in folder`; `Copy path`; —; `Move up`; `Move down`; `Remove from workspace`; every item enabled; no `Cancel …` (nothing runs), no `Error details…` (nothing kept), no `Locate…` (Ready). The app log (Debug) `Web: Context menu opened: web-app by pointer`.<br>**After step 4:** old-site selected; its menu at (68, 401), 293 high (9 items, 3 separators), its bottom at y 694, inside the window: header `old-site` (tooltip `{root}\old-site`); `Open` (disabled: "{root}\old-site does not exist"); —; `Fetch` (disabled: "{root}\old-site does not exist"); `Pull` (disabled: "{root}\old-site does not exist"); —; `Show in folder` (disabled: "{root}\old-site does not exist"); `Copy path` (highlighted); —; `Move up`; `Move down` (disabled: "It is the last repository"); `Locate…`; `Remove from workspace` (1.10, 2.7).<br>**After step 5:** the fake folder dialog was asked once, titled `Locate old-site`; no error dialog. `workspaces.json`: old-site's entry, still the tenth, is now the WorkingDirectory of the repository at `{root}\archive\old-site` (git's spelling of that folder, the chosen folder being its top: `SameFolder` matches it to `{root}\archive\old-site`, and on the development machine it is that text); the nine other paths and their order unchanged; `10 repositories`. WL: the row, read again at once with a new token (trigger 6), `old-site` · `main` · `No remote` (`This repository has no remotes`) · `Clean` · empty · empty, its Repository cell's tooltip that saved path, as `workspaces.json` holds it (compared with the file's text, not with the literal `{root}\archive\old-site`: on CI's Windows it is the long form of `RUNNER~1`, on CI's macOS `/private/var/…`); still selected. Its calls, all reads in that folder (by `SameFolder`): two `OpenAsync` calls (Locate's own of the chosen folder, 1.10, then the row's probe of the saved path, 2.10 step 3), where the chosen spelling is not git's a `git rev-parse --show-prefix` or `--git-common-dir` after the first, the refs read's five calls and the status read (the check asserts these kinds of call, not their number); a pass line `Web: read 1 repository in {ms} ms`.<br>**After step 6:** the menu before the click was a Ready row's: header `old-site` (tooltip the saved path, as the cell's); `Open` (highlighted); —; `Fetch` (disabled: "This repository has no remotes"); `Pull` (disabled: "main has no upstream: set one first"); —; `Show in folder`; `Copy path`; —; `Move up`; `Move down` (disabled: "It is the last repository"); `Remove from workspace`; 267 high, no `Locate…`. After the click: no question and no dialog; WR, `9 repositories`; no row selected and the keyboard focus on `DashboardList` (2.7); old-site's summary-mode view model released (`OpenRepositories.Summaries` holds none for its worktree, 3.4). `workspaces.json`: the nine paths of WR, no old-site. `{root}\archive\old-site` is still there, unchanged.<br>**After step 7:** WM: docs moved up one place, now row 1 (y 178 to 204), still selected, the focus in the list (decision WD8); `workspaces.json` in WM's order.<br>**After step 8:** api's menu: `Move up` (disabled: "It is the first repository"), `Move down` enabled, the rest as web-app's. Nothing ran in git: the exact state of each of the nine repositories equals the one read before the start; every call since the start only reads (old-site's new folder's included). | | | `w2a-ten-rows`, `w2b-ahead-behind-tooltip`, `w2c-row-menu`, `w2d-gone-folder-menu`, `w2e-located`, `w2f-removed`, `w2g-moved-up`; real window: `w2a-ten-rows`, `w2c-row-menu` | Design 1.4, 1.5, 1.10, 2.4 to 2.7, 2.10, 3.4; scenario facts 2.2, 2.3, 8 items 1 and 2; D204, D206, D207. No picture varies by nature: at 1100 no folder is drawn (the Folder column is hidden), only tooltips hold it, and the one pictured tooltip holds none. Decisions WD4, WD5, WD7, WD8, WD22, WD27, WD34. **RW: steps 1 and 3**: the dashboard of ten rows started in a real process (`RealApp.WriteWorkspaces`, `RealApp.Launch(data, session)`), the rows' texts read through UI Automation (`DashboardRow` by name, its cells by id), web-app's menu opened by a real right-click and its items read; captures after steps 1 and 3 compared with `w2a-ten-rows` and `w2c-row-menu`. What only the real window proves: the dashboard tab drawn at the machine's 200 percent scaling in a real window, its columns' widths from the real window's width, and a row menu from a real right-click. |
| W3 | W2's rows at 1920×1080 in the light theme, with the Folder column (design 2.4, 2.6, 11.10; D206): every cell, the saved folders in Folder, the light tones; "workspace", light, 1920×1080 | `Check_W3_ten_repositories_wide_and_light`: 1. "Workspace" scenario. `OpenWorkspaceAsync(data, copy, 1920, 1080, AppTheme.Light)`. Screenshot. | **After step 1:** the dashboard spans x 0 to 1920; `DashboardHeader` y 88 to 124 (light `VcChromeBackgroundBrush` `#EBEDF1`), `DashboardMore` ending at x 1912, the run part empty. The headings `Repository`, `Folder`, `Branch`, `Ahead / behind`, `Changes`, `State`, `Operation` at Repository x 0 to 132, Folder 132 to 780 (648), Branch 780 to 930, Ahead / behind 930 to 1060, Changes 1060 to 1280, State 1280 to 1460, Operation 1460 to 1920 (460), each 8 into its column (2.6: `rest` = 1108, Operation `clamp(300 + 320, 300, 460)` = 460, Folder 648). The ten rows at y 152 + 26 k with W0's cells, and each row's Folder cell (`DashboardRowFolder`) the saved text `copy.PathOf(name)` in the secondary colour, old-site's included (decision WD29). Light tones: names and branches `#1B1F27`; secondary texts and the arrows `#5C6572`; `1 conflict` `#B7323D` and `Merge in progress`, `Folder not found` `#895A17` (D198's light danger and warning, 11.10); no row selected. The status bar y 1054 to 1080: `StatusWorkspaceIcon`, `Web`, `Ready`. TD, but `ThemeSwitch` `Switch to the dark theme` (the light theme's, 11.6, 14.11). The neutral point (760, 687) lies in the list below the tenth row (rows end at y 412): no row hovered, no tooltip. Only reads were called. | | | `w3a-ten-rows-wide-light` | Design 2.6's 1920 column, 11.10, D198. `w3a-ten-rows-wide-light` varies by nature: its Folder column draws the temporary folder; the check asserts each Folder cell's text equal to the saved path (structurally) and, on the development machine, whole (decision WD29). **RW: n/a**: a 1920×1080 window does not fit the development machine's work area (1500×952 logical pixels at 200 percent; risk 3, the plan's conditional part); W2's steps 1 and 3 picture the dashboard in the real window at 1100×700. |
| W4 | Fetch all with progress (design 4.2 to 4.4, 4.9, 4.10; D210, D229): the folder server holding docs, mobile and design-system at 25, 75 and 45 percent: their compact texts with `Cancel`, the five others fetched, notes and old-site left out, `Fetching 8 repositories… 5 done` with the bar at 62 and `Cancel all`, the status bar the same and no waiting count; released: `Fetched 8 repositories; 2 skipped`, docs 4 behind, mobile 1 ahead and 3 behind, design-system 3 behind; "workspace" with docs', mobile's and design-system's `origin` on the folder server (47393), dark, 1100×700 | `Check_W4_fetch_all_progress`: 1. "Workspace" scenario; before the start `Scenarios.ServeWorkspaceAsync(copy, requireAuth: false, "docs", "mobile", "design-system")` and the three holds `server.HoldNextFetchPack("docs", 25)`, `("mobile", 75)`, `("design-system", 45)`. `OpenWorkspaceAsync`. 2. Click `DashboardFetchAll`. Wait until the three holds are taken (their tasks complete), docs, mobile and design-system show 25, 75 and 45 (`WaitForRowPercentAsync`), and api, web-app, billing, infra and scratch show `Fetched from all remotes`. Screenshot. 3. `server.Release()`. Wait for the run's end (`WaitForBulkRunAsync`). Screenshot. | **After step 1:** W0 (scenario facts 2.2's states: the URL changes show in no cell); `git remote get-url origin` in docs, mobile and design-system `http://127.0.0.1:47393/<name>.git` (`WithPort`), every other origin `../remotes/<name>.git`. TD.<br>**After step 2:** the run decided every row at its start (4.2): api, web-app, docs, mobile, design-system, billing, infra and scratch in line; notes left out (`This repository has no remotes`), old-site left out by its state; the app log `Web: Fetch all: started for 8 repositories, 2 skipped`. The rows: docs `Fetching from all remotes… 25%` (secondary) then `Cancel` (`DashboardRowCancel`, name `Cancel`, tooltip `Stop git`), the text's tooltip `Fetching from all remotes… Counting objects 25%` (the status bar's whole text, `CompactToolTip`); mobile `Fetching from all remotes… 75%` and `Cancel` (`… Counting objects 75%`); design-system `Fetching from all remotes… 45%` and `Cancel` (`… Counting objects 45%`); api, web-app, billing, infra and scratch `Fetched from all remotes` (secondary, no button); notes `This repository has no remotes` (secondary, its tooltip the same); old-site's Operation empty, its State `Folder not found`. No row `Waiting to fetch…` (three slots held, the fourth free); the Ahead / behind cells still W0's (no held fetch has ended). The header's run part, right-aligned 16 before `Add repository…`: `DashboardBulkText` `Fetching 8 repositories… 5 done` (secondary, whole), 8, `DashboardBulkProgress` (80×4, determinate) at 62 (⌊100 × 5 / 8⌋), 8, `DashboardCancelAll` `Cancel all` (name `Cancel all`, tooltip `Cancel the rest of Fetch all`). TR: Fetch and Pull, the toolbar's and the header's, disabled with `Wait for Fetch all to finish`. The status bar: `Web`; `Fetching 8 repositories… 5 done` (secondary); `OperationProgress` determinate at 62; `OperationCancelButton` (tooltip `Cancel the rest of Fetch all`, decision WD31); no `OperationWaiting` (no row in line). No dialog. The call log, which records a call when it ends: five `git fetch --progress origin` records, exit 0, one each in api, web-app, billing, infra and scratch (no `--no-auto-maintenance`: one remote each, D109); none yet in docs, mobile or design-system, whose held calls are recorded when they end; none in notes or old-site. Git: every ref as built (the five file-remote fetches found nothing new and printed nothing; the three held ones have written nothing).<br>**After step 3:** WF. The header `Fetched 8 repositories; 2 skipped` (secondary), no bar, no `Cancel all`; the status bar the same text, no bar, no Cancel; docs, mobile and design-system `Fetched from all remotes`, their Ahead / behind WF's; notes still `This repository has no remotes`. TD again (Fetch and Pull enabled with their tooltips). The app log `Web: Fetch all: Fetched 8 repositories; 2 skipped`. No dialog. The call log: one `git fetch --progress origin` record in each of the eight folders, exit 0, none in notes or old-site. Git: WF's refs; every `main` and working tree unchanged (design-system's four changes kept). | | | `w4a-held-at-25-75-45`, `w4b-fetched`; real window: `w4a-held-at-25-75-45`, `w4b-fetched` | Design 2.6 (`CompactText`, `CompactToolTip`), 4.2, 4.3 (the four slots in row order: api, web-app, docs and mobile first, design-system, billing, infra and scratch as slots free), 4.9, 4.10; scenario facts 2.5 (the holds are `Counting objects` percentages, the lines over HTTP as over the file remote) and 8 item 4 (design-system changes too); D109, D210, D229. No picture varies by nature: the holds fix the three percentages and the done count, and the bar is determinate. The port shows in no picture of W4. Decisions WD13, WD23, WD31. **RW: steps 1 to 3** in a real process, the folder server in the test process on 47393 (or its fallback), `DashboardFetchAll` clicked with the real mouse; the rows', the header's and the status bar's texts read through UI Automation; captures after steps 2 and 3 compared with `w4a-held-at-25-75-45` and `w4b-fetched`. What only the real window proves: a bulk run with its determinate bar, three rows' percentages and `Cancel` buttons drawn at once in a real window, over HTTP fetches the real app process makes. |
| W5 | Cancel all, and a row's own Cancel, under the fetch hold (design 4.3, 4.5, 4.9; D210): four running, four `Waiting to fetch…`, `4 waiting`; Cancel all: the four in line cancelled at once, `Cancelling: waiting for 4 repositories…`, then `Fetched no repositories; 8 cancelled; 2 skipped`, no dialog; after an F5 under the hold notes still `No remote` and left out; on a fresh start, docs' own Cancel stops docs only, its slot goes on, `Fetched 7 repositories; 1 cancelled; 2 skipped`; "workspace" with the hold in the eight repositories with a remote, dark, 1100×700 | **Part A** (`Check_W5_cancel_all`): 1. "Workspace" scenario; before the start `FetchHold.Install(data.Path)` and `hold.HoldIn(…)` with `copy.PathOf(name)` for api, web-app, docs, mobile, design-system, billing, infra and scratch. `OpenWorkspaceAsync`. 2. Start recording `DashboardBulkText`'s texts. Click `DashboardFetchAll`. `hold.WaitForHeldAsync(4)`; wait until design-system, billing, infra and scratch show `Waiting to fetch…`; settle. Screenshot. 3. Click `DashboardCancelAll`. Wait for the run's end. Note the call log's sequence (`mark`). Screenshot. 4. Click api's row (66, 165), so that the list has the focus (decision WD33); press F5. Wait for the read pass. (In a `finally`: `hold.Dispose()`.)<br>**Part B** (`Check_W5_row_cancel`, a fresh copy and data folder, a new start; decision WD36): 5. "Workspace" scenario; before the start `FetchHold.Install(data.Path)` and `hold.HoldIn(…)` for the same eight. `OpenWorkspaceAsync`. Click `DashboardFetchAll`. `hold.WaitForHeldAsync(4)`. Click docs' `DashboardRowCancel`. `hold.WaitForHeldAsync(5)`; wait until docs shows `Fetch cancelled` and design-system `Fetching from all remotes…`. Screenshot. 6. `hold.Release()`. Wait for the run's end. Screenshot. (In a `finally`: `hold.Dispose()`.) | **After step 2:** api, web-app, docs and mobile `Fetching from all remotes…` (secondary; no percentage: a held file remote prints nothing) with `Cancel` (tooltip `Stop git`); design-system, billing, infra and scratch `Waiting to fetch…` (secondary; no `Cancel`: a row in line has none, 4.5); notes `This repository has no remotes`; old-site nothing new. The header `Fetching 8 repositories…` (nothing ended), the bar at 0, `Cancel all`. TR (no row selected: the toggle disabled with `Select a repository to see its activity`). The status bar `Fetching 8 repositories…`, its bar at 0, the Cancel (`Cancel the rest of Fetch all`), `OperationWaiting` `4 waiting`. `hold.Held` is 4: four markers `held-<name>.git.<pid>`, for api, web-app, docs and mobile, and none for design-system, billing, infra and scratch, which are in line (`Waiting to fetch…` without `Cancel`: no slot and no git process yet, 4.3). The four rows in line beside the four markers are what shows that no fifth fetch started (never more than 4).<br>**After step 3:** the header texts recorded after the click hold `Cancelling: waiting for 4 repositories…`; before it at most `Fetching 8 repositories… 4 done` (the four in line ended by `StopStarting()`, which counts them as done, decision WD13); after it only `Cancelling: waiting for {r} repositories…` (`… 1 repository…`), `{r}` falling below 4 as the killed fetches end (4.5: the rows still running); the result last (decision WD12). `Cancel all` and the status bar's Cancel hid at the click and stayed hidden. The rows: all eight `Fetch cancelled` (secondary), no `Details…` (the four killed fetches were waiting in `upload-pack`, before any pack: no lock file, 4.5); the Ahead / behind cells W0's. The header and the status bar `Fetched no repositories; 8 cancelled; 2 skipped` (secondary); no bar. No dialog. The app log `Web: Fetch all: Fetched no repositories; 8 cancelled; 2 skipped`. The call log: four `git fetch --progress origin` records, one each in api, web-app, docs and mobile, each `Cancelled` (`GitCallOutcome.Cancelled`: recorded when the killed call ended), none in the four that were in line. Git: every ref as built; no `*.lock` file under any repository's `.git`.<br>**After step 4:** api selected (the click), the activity-log toggle enabled with `Show every git command the app has run (Ctrl+J)`. The read pass ended (the line `Web: read … repositories in {ms} ms`); notes still `No remote` (`This repository has no remotes`): the hold's section in eight `.git/config` files gave notes no remote (17.2); its Operation still `This repository has no remotes` and the eight others still `Fetch cancelled` (a run's texts stay until the next run or the row's next operation, 4.2, 4.5); the call log since `mark` holds no `git fetch`, only the pass's reads.<br>**After step 5:** the run decided every row at its start: notes left out with `This repository has no remotes`, old-site by its state, the eight in line; the app log `Web: Fetch all: started for 8 repositories, 2 skipped`. docs `Fetch cancelled` (secondary; no `Details…`), its own Cancel having cancelled its fetch only (4.5: the queue's `CancelCurrent()`); design-system, which took docs' slot, `Fetching from all remotes…` with `Cancel`; api, web-app and mobile still `Fetching from all remotes…` with `Cancel`; billing, infra and scratch `Waiting to fetch…`. The header `Fetching 8 repositories… 1 done` (decision WD13), its bar at 12, `Cancel all` still shown (a row's Cancel is not Cancel all); TR, no row selected (a fresh start: the toggle disabled with `Select a repository to see its activity`); the status bar the same text and bar, the Cancel, `3 waiting`. `hold.Held` 5 since `Install` (which arms the hold, 17.2).<br>**After step 6:** docs `Fetch cancelled`; api, web-app, mobile, design-system, billing, infra and scratch `Fetched from all remotes`; Ahead / behind: mobile ↑ `1` ↓ `3` (`1 ahead, 3 behind`), design-system ↓ `3` (`3 behind`), docs still ↓ `3` (`3 behind`: not fetched), the rest W0's. The header and the status bar `Fetched 7 repositories; 1 cancelled; 2 skipped` (secondary); TD, no row selected. No dialog. Git: mobile's `origin/main` `67a2660…`, design-system's `948f7d1…`, docs' still `23e2ad1…`; no `*.lock` file. | | | `w5a-four-running`, `w5b-all-cancelled` (part A); `w5c-row-cancelled`, `w5d-row-cancel-run-ended` (part B) | Design 4.2, 4.3 (at most 4 slots; a slot from the call to the operation's end), 4.5 (`StopStarting()`, the run's cancellation, a row's own Cancel), 4.9; 17.2's hold (`HoldIn`, `Held`, `WaitForHeldAsync`, `Release`, `Dispose`; two runs under one hold, `Arm()` between them, are `FetchHoldTests`'); D210. Two methods (decision WD36): as one, about 220 git processes, beyond what the hang watchdog's 3 minutes allow under load (conventions, "Run time and the hang watchdog"); the row's Cancel needs nothing from Cancel all, so 17.4's second run is part B's first run on a fresh copy. On macOS and Linux the shells of part A's four killed fetches poll on until the `finally` releases them (only git is killed there; on Windows the job ends the shell with git). No picture varies by nature: the held fetches show no percentage and the bar is determinate. `Cancelling: waiting for 4 repositories…` lasts only until the four killed processes end, so it is asserted from the recorded texts and not pictured (decision WD12). Decisions WD12, WD13, WD33, WD36. **RW: n/a**: W4 pictures a run, its bar, its row buttons and its result in the real window; what W5 adds (which rows Cancel all and a row's Cancel stop, the counts) is the same in both harnesses and read from the view models, and the hold is a shell wrapper whose processes the real window could not tell apart from W4's. |
| W6 | Failures kept in their rows, and one sign-in for two repositories (design 4.4, 4.6, 4.7, 4.9; D195, D210, D211): docs and mobile on the folder server with sign-in: one dialog naming docs, its answer reused for mobile; scratch's missing remote: `Fetch failed` with `Details…`, no dialog until clicked, then the error dialog (as often as asked); `Fetched 7 repositories; 1 failed; 2 skipped` in the danger colour; scratch's own Fetch from its row menu opens the dialog at once; "workspace" with docs' and mobile's `origin` on the folder server with sign-in, scratch's `origin` `../remotes/missing.git`, dark, 1100×700 | `Check_W6_failures_and_one_sign_in`: 1. "Workspace" scenario; before the start `ServeWorkspaceAsync(copy, requireAuth: true, "docs", "mobile")`; in scratch `git remote set-url origin ../remotes/missing.git`; `server.HoldNextRequest("mobile")`. `OpenWorkspaceAsync`. 2. Click `DashboardFetchAll`. Wait until the credentials dialog shows with `DialogRepository` `In docs` and api, web-app, design-system, billing, infra and scratch have each ended (a result in their Operation cells). `server.ReleaseRequest("mobile")`; wait until `server.Requests` holds mobile's `GET /mobile.git/info/refs?service=git-upload-pack` answered 401 (decision WD14). Screenshot. 3. Type `alice` in `CredentialUsername` and `s3cret` in `CredentialPassword` (decision WD24); click `Sign in`. Wait for the run's end. Screenshot. 4. Click scratch's `DashboardRowDetails`. Wait for the error dialog. Screenshot. Click `Close`. Click scratch's `DashboardRowDetails` again; the dialog read; click `Close`. 5. Right-click scratch's row (66, 347); the menu read; click `Fetch`. Wait for the error dialog; it read; click `Close`. | **After step 1:** W0 (scratch's new URL shows in no cell). TD.<br>**After step 2:** one dialog over the dashboard (the backdrop): `CredentialsDialog`, `DialogTitle` `Sign in to 127.0.0.1:47393`; `CredentialsText` `Git needs a user name and a password or token for http://127.0.0.1:47393.` (from git's prompt `Username for 'http://127.0.0.1:47393': `; both through `WithPort`); `DialogRepository` `In docs` (3.8: a dashboard tab is open); the label `User name` over `CredentialUsername` (empty, the keyboard focus) and `Password` over `CredentialPassword` (empty); `Cancel` and `Sign in`, `Sign in` disabled while the user name is empty. Behind it: the header `Fetching 8 repositories… 6 done` (decision WD13), the bar at 75; docs and mobile `Fetching from all remotes…` with `Cancel`; api, web-app, design-system, billing and infra `Fetched from all remotes`; scratch `Fetch failed` (danger) then `Details…` (`DashboardRowDetails`, name `Details`); notes `This repository has no remotes`; old-site nothing new. No error dialog opened (D195). The server's requests so far: docs' `GET /docs.git/info/refs?service=git-upload-pack` (no user, 401) and, after the release, mobile's (no user, 401); mobile's git waits for its group's turn (4.4): no second dialog is stacked.<br>**After step 3:** the dialog closed, and no other dialog showed during the run: mobile's two prompts, the same texts as docs' (`Username for 'http://127.0.0.1:47393': `, `Password for 'http://alice@127.0.0.1:47393': `), were answered from the group's kept answers (4.4). `server.Requests` for docs and for mobile, each in this order: `GET …/info/refs?service=git-upload-pack` (no user, 401), `GET …/info/refs?service=git-upload-pack` (`alice`, 200), `POST …/git-upload-pack` with the command `ls-refs` (`alice`, 200), `POST …/git-upload-pack` with `fetch` (`alice`, 200). The run ended: the header and the status bar `Fetched 7 repositories; 1 failed; 2 skipped` in the danger colour; docs ↓ `4`, mobile ↑ `1` ↓ `3`, design-system ↓ `3` (fetched from its file remote, scenario facts 8 item 4), each `Fetched from all remotes`; scratch `Fetch failed` with `Details…`; no dialog open; the app log `Web: Fetch all: Fetched 7 repositories; 1 failed; 2 skipped`. Git: WF's refs; scratch's refs as built, its `origin` still `../remotes/missing.git`; the call log's `git fetch --progress origin` in scratch exit 128.<br>**After step 4:** the error dialog D95 would have opened (4.6's `ShowDeferredAsync`): `ErrorTitle` `Fetch failed`; `ErrorSummary` `Git could not fetch from origin.` (D109's `FetchAllFailureDisplay`); `ErrorRepository` `In scratch`; `ErrorOutput` `fatal: '../remotes/missing.git' does not appear to be a git repository` (key) / `fatal: Could not read from remote repository.` / `Please make sure you have the correct access rights` / `and the repository exists.` (git's standard error, cleaned; 2.55 identical) / `error: Could not fetch origin` (the app's line, which `FetchAsync(null)` appends, D109: on every git the last line is exactly this, and `AssertGitOutputShown` takes the lines before it, the conventions); `ErrorCommand` `git fetch --progress origin`; `Copy output` and `Close`. After `Close` scratch still shows `Fetch failed` and `Details…`; the second click opened the identical dialog (it opens as often as asked, 4.6).<br>**After step 5:** scratch's menu: header `scratch`; `Open` (highlighted); —; `Fetch`; `Pull` (disabled: "experiment has no upstream: set one first"); `Error details…` (a result kept); —; `Show in folder`; `Copy path`; —; `Move up`; `Move down`; `Remove from workspace`; no `Cancel …`; 293 high (9 items, 3 separators), its top-left corner at (68, 349). The single row's Fetch (the toolbar's, 4.7) opened its dialog at once when git failed (D195), with the same title, summary, `In scratch`, output and command; the call log has one more `git fetch --progress origin` in scratch (exit 128). After `Close`: scratch's Operation `Fetch failed` (danger) without `Details…` (the kept dialog went when its queue started the new fetch, 4.6); the header still `Fetched 7 repositories; 1 failed; 2 skipped` (a run's result stays until the next run). | | | `w6a-one-sign-in`, `w6b-failure-in-row`, `w6c-details` | Design 3.8, 4.4 (the group: one dialog at a time, the same text answered from the first answer), 4.6, 4.7, 4.8's Fetch all outcomes, 4.9; scenario facts 2.5 (the prompts, the requests, the missing remote's lines); D109, D195, D210, D211. `w6a-one-sign-in` shows the port in its title and text (47393, or the fallback's through `WithPort`), and it varies by nature: it is taken with the keyboard focus in the empty `CredentialUsername`, whose caret blinks and may be caught on or off (as phase 4's `13c`, `15b`, `16c` and `b7f` and phase 5's F10, F11 and A4), so its expected result is structural and the gate compares it by inspection, not by hash; no picture varies otherwise: the six rows behind the dialog have ended when it is pictured (decision WD14). Decisions WD13, WD14, WD24. **RW: n/a**: the credentials and error dialogs are phase 3's, pictured in the real window by its checks, and W4 pictures a run there; the prompt group's one dialog and the rows' kept failures read alike from the view models in both harnesses. |
| W7 | Pull all (design 4.2, 4.6, 4.8, 4.9, 4.11; D195, D211): `pull.ff = only` in mobile: api and web-app up to date, docs fast-forwarded by 4 commits, mobile and design-system `Pull failed` with `Details…` (4.8's new `PullNotFastForward` summary; `1 conflict prevents the merge…`), the other five left out with 4.2's reasons, `Pulled 3 repositories; 2 failed; 5 skipped`; docs opened afterwards from its row offers `Undo pull into main` and undoes it; "workspace" with `pull.ff = only` in mobile's `.git/config`, dark, 1100×700 | `Check_W7_pull_all`: 1. "Workspace" scenario; before the start `git config pull.ff only` in mobile (`copy.Repo("mobile")`). `OpenWorkspaceAsync`. 2. Click `DashboardPullAll`. Wait for the run's end. Screenshot. 3. Click mobile's `DashboardRowDetails`. Wait for the error dialog. Screenshot. Click `Close`. 4. Click design-system's `DashboardRowDetails`. Wait for the error dialog. Screenshot. Click `Close`. 5. Double-click docs' row (66, 217). Wait for docs' tab (its graph drawn, the queue idle). Rest the pointer on `UndoButton`. Screenshot. 6. Click `UndoButton`. Wait for the line `docs: Undo pull into main: Succeeded after <N> ms: Undid pull into main` and idle. Screenshot. 7. Note the app log's mark (`logMark`). Click the Web tab; settle (no F5 and no wait for a pass: W7 has no read clock, so whether trigger 1 reads the rows without a tab depends on the real time since Pull all's refreshes, 2.10). | **After step 1:** W0; TD (`Pull every repository of Web (Ctrl+Shift+L)`).<br>**After step 2:** the run decided every row at its start by `ToolbarViewModel.StatesFor` (4.2): api, web-app, docs, mobile and design-system in line; left out, each in its Operation cell in the secondary colour with the reason as its tooltip: billing `Finish or abort the merge first`, infra `HEAD is detached: check out a branch to pull`, scratch `experiment has no upstream: set one first`, notes `main has no upstream: set one first` (`Reasons`' order tests the upstream before the remotes: scenario facts 8 item 1); old-site nothing new (its State says why); the app log `Web: Pull all: started for 5 repositories, 5 skipped`. WP after the run: api `main is already up to date` and web-app `main is already up to date` (secondary); docs `Pulled 4 commits into main (fast-forward)` (secondary) and `Up to date` (`main is level with origin/main`); mobile `Pull failed` (danger) then `Details…`, ↑ `1` ↓ `3`; design-system `Pull failed` then `Details…`, ↓ `3`, `1 staged, 2 unstaged, 1 untracked`. The header and the status bar `Pulled 3 repositories; 2 failed; 5 skipped` in the danger colour; TD. No dialog opened during the run (D195). The call log, in each of the five folders, among its calls in this order: the pull configuration's reads (8.9, before the fetch), `git fetch --progress origin`, `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --` (9.10's read: docs `changelog.md`, `faq.md`, `install.md`; mobile `about.txt`, `login.txt`, `settings.txt`; api, web-app and design-system nothing), then the merge: api, web-app and docs `git merge --no-edit --no-overwrite-ignore FETCH_HEAD` (exit 0; api's and web-app's standard output `Already up to date.`, docs' `Updating ec855ba..245b729` / `Fast-forward` / … as scenario facts 2.6), mobile `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` (exit 128), design-system `git merge --no-edit --no-overwrite-ignore FETCH_HEAD` (exit 1); no `git check-ignore` (none of the added paths is on disk); no call in billing, infra, scratch, notes or old-site's folder. Git: WP's.<br>**After step 3:** the error dialog: `ErrorTitle` `Pull failed`; `ErrorSummary` `main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main.` (4.8's `PullNotFastForward`); `ErrorRepository` `In mobile`; `ErrorOutput` `fatal: Not possible to fast-forward, aborting.` (key `Not possible to fast-forward`; 2.55 prints scenario facts 2.6's nine `hint:` lines before it); `ErrorCommand` `git merge --ff-only --no-overwrite-ignore FETCH_HEAD`; `Copy output` and `Close`.<br>**After step 4:** the error dialog: `Pull failed`; `1 conflict prevents the merge. Commit, stash, or discard your changes first.` (`MergeBlockedByLocalChanges`, phase 5's words); `In design-system`; `ErrorOutput` exactly the four lines of git's standard error, in order: `error: Your local changes to the following files would be overwritten by merge:` (key), `⇥spacing.txt` (⇥ a tab), `Please commit your changes or stash them before you merge.`, `Aborting` (2.55 identical); no `Updating 98ba313..948f7d1`, which git prints on standard output, shown only when standard error is empty (`GitOutputCleaner.Clean`, `GitFailure.GitOutput`: the two streams are never joined; scenario facts 8 item 9 assumed they were, and this replaces its advice; replayed for this revision on 2.36 and 2.55, "How these states were derived"); `ErrorCommand` `git merge --no-edit --no-overwrite-ignore FETCH_HEAD`; `Copy output` and `Close`.<br>**After step 5:** tabs [Web, docs], docs at index 1 (2.3) and active; it adopted the row's summary-mode view model (3.3: the tab's `Repository` is the object the row held, `IsSummary` false): its graph DP, its status bar `main` and `Pulled 4 commits into main (fast-forward)` (the adopted status's last result, decision WD15). `UndoButton` enabled, its tooltip open `Undo pull into main (Ctrl+Z)` (the help text the same; the pull was recorded in the summary-mode view model's history, 4.11, D154); Redo disabled, `Nothing to redo`.<br>**After step 6:** Undo ran: one operation `Undo pull into main`, no question (a local change, phase 5's 7.5), no dialog; the status bar `Undid pull into main` (secondary). The call log among the operation's calls: `git update-index -q --refresh`, `git read-tree -m -u 245b72964a7ee4952ba789ed47201369ab4e2f50 ec855bae3aa27bbf3f546b0ec7934cca41d65c01`, `git update-ref -m "VisualCommit: undo pull into main" refs/heads/main ec855bae3aa27bbf3f546b0ec7934cca41d65c01 245b72964a7ee4952ba789ed47201369ab4e2f50` (the message quoted: one argument with spaces, `GitCommand.DisplayText`). Git: the pull's undo as "Scenarios and graphs" gives it (`main` `ec855ba…`, `+0 -4`, `README.md` and `guide.md` `Guide` only, the reflogs' newest `VisualCommit: undo pull into main`). The graph DU; Undo disabled, `Nothing to undo`; Redo enabled, `Redo pull into main (Ctrl+Y)`.<br>**After step 7:** docs' row follows its tab (3.5): `docs` · `main` · ↓ `4` (`4 behind`; `4 commits behind origin/main`) · `Clean` · empty · `Undid pull into main` (secondary, its view model's last result); it needs no read and has none by the dashboard: since `logMark` no `docs: summary read in` line, and `OpenRepositories.Summaries` holds no view model for docs' worktree; the header still `Pulled 3 repositories; 2 failed; 5 skipped`; the other rows' values WP's (a pass, if trigger 1 started one, reads the same values and leaves the Operation cells as they are, 4.2). | | | `w7a-pull-all-ended`, `w7b-not-fast-forward-details`, `w7c-local-changes-details`, `w7d-undo-pull-tooltip`, `w7e-pull-undone` | Design 3.3, 4.2, 4.6, 4.8 (the outcomes' texts and the new `PullNotFastForward`), 4.9, 4.11; 8.9 (the configuration read before the fetch), 9.10 (the pull's read and `--no-overwrite-ignore`); scenario facts 2.6 and 8 items 1, 4 and 9; phase 5's design 7.3 and 7.6 (the Keep move, `pull into {b}`); D84, D154, D195, D211. No picture varies by nature. Decision WD15. **RW: n/a**: W4 pictures a bulk run in the real window, the error dialogs are C11's (phase 3's real-window checks), and the undo of a pull is phase 5's; the rows' outcomes, the counts and the adopted history read alike from the view models in both harnesses. |
| W8 | Rows opening into tabs, and the dashboard's reads (design 2.3, 2.9, 2.10, 3.3, 3.5; D207, D208, D209; Q4): web-app opened by a double-click after the dashboard, docs by Enter after web-app (one queue each: the tabs adopt the rows' view models); mobile's held Fetch from its row menu, then mobile opened: the fetch goes on in the tab's status bar and ends there, one Fetch record; a commit in web-app's tab updates its row with no read; under the session's manual read clock, a file written in scratch shows only once 10 s have passed and the window is activated; the activity log following the selected row, and its reasons; "workspace" with the hold in mobile, dark, 1100×700 | `Check_W8_rows_open_into_tabs_and_reads`: 1. "Workspace" scenario; before the start `FetchHold.Install(data.Path)`, `hold.HoldIn(copy.PathOf("mobile"))`; `OpenWorkspaceAsync(…, readClock: <a ManualTimeProvider>)` (the helper's `readClock` parameter, conventions). Rest the pointer on `ActivityLogToggle`. Screenshot. 2. Move the pointer to the neutral point. Double-click web-app's row (66, 191). Wait for web-app's graph and its idle queue. Screenshot. 3. Click the Web tab. Click docs' row (66, 217); press Enter. Wait for docs' graph. Screenshot. 4. Click the Web tab. Right-click mobile's row (66, 243); click `Fetch`. `hold.WaitForHeldAsync(1)`. Screenshot. 5. Double-click mobile's row (66, 243). Wait for mobile's graph. Screenshot. 6. `hold.Release()`. Wait for the line `mobile: Fetch: Succeeded after <N> ms: Fetched from all remotes` and idle. 7. Click web-app's tab. Outside the app write `contact.html` (`<h1>Contact</h1>\n`) in web-app; wait until its graph shows the working-changes row. Click the working-changes row (468, 129); click `StageAllButton`; click `CommitSummary` and type `Add the contact page`; click `CommitButton`. Wait until idle. Note the call log's and the app log's marks (`mark`). Click the Web tab; settle. Screenshot. 8. Write `todo.txt` (`Try it\n`) in scratch. Note `mark2` (both logs). `ActivateWindow()`; settle. 9. Note `mark3` (both logs). `ReadClock.Advance(TimeSpan.FromSeconds(10))`. `ActivateWindow()`. Wait for a pass line logged after `mark3` (`WaitForDashboardReadAsync(since: mark3)`). Screenshot. 10. Click mobile's row (66, 243). Click `ActivityLogToggle`. Screenshot. 11. Click old-site's row (66, 399). 12. Click scratch's row (66, 347). | **After step 1:** W0, no row selected; TD; `ActivityLogToggle` disabled, its tooltip open `Select a repository to see its activity` (2.9's second reason; the help text the same) in the app's tooltip colours.<br>**After step 2:** tabs [Web, web-app]: web-app inserted at index 1 (2.3: `1 + max(d − 1, none)`, d = 1) and active; it adopted web-app's summary-mode view model (3.3: the tab's `Repository` is the row's object, `IsSummary` false; `OpenRepositories.Summaries` holds none for web-app); its graph WA0, the left panel and the stage or details panel as any repository tab's; the status bar `main` (`IconBranch`), `Ready`; the recent list's top is web-app (`remember: true`).<br>**After step 3:** the list took the focus when the dashboard became active; docs selected by the click; Enter opened it: tabs [Web, web-app, docs], docs at index 2 (`1 + max(0, 1)`), active, adopted likewise, its graph D0.<br>**After step 4:** mobile's row selected; its Operation `Fetching from all remotes…` (secondary) with `Cancel` (tooltip `Stop git`); one marker `held-mobile.git.<pid>`; the header's run part empty (a row's own Fetch is no run, 4.7); the status bar's operation part `Ready` (the dashboard tab's status is the run's: none; decision WD3); no call-log record of mobile's `git fetch --progress origin` yet (a call is recorded when it ends, step 6): its running shows in that marker, the row's `Fetching from all remotes…` and `Cancel`.<br>**After step 5:** tabs [Web, web-app, docs, mobile], mobile at index 3 and active; its graph M0 (the fetch has not ended); the tab's status bar `main`, `Fetching from all remotes…` with the indeterminate bar and `Cancel` (`Stop git`): the adopted view model's running fetch goes on in the tab (3.3); no `TabWaitingText` (the queue is the adopted view model's own, not a closed one's).<br>**After step 6:** the fetch ended in the tab: its status bar `Fetched from all remotes`; its graph MF; the left panel's `main` ↑ `1` ↓ `3`. The operation log holds exactly one `Fetch` record for mobile's worktree (one queue: the row's Fetch and the tab are one view model), and the call log one `git fetch --progress origin` record in mobile, exit 0.<br>**After step 7:** web-app's graph was WC before the click on the Web tab (the commit `092116c…`, "Scenarios and graphs"; no working-changes row; `Commit 1 file` ran as phase 2's commit). On the dashboard, mobile still selected (since step 4) and the activity-log toggle enabled with `Show every git command the app has run (Ctrl+J)`, web-app's row follows its tab's live refs (3.5): `web-app` · `main` · ↑ `3` (`3 ahead`; `3 commits ahead of origin/main`) · `Clean` · empty · empty; docs `docs` · `main` · ↓ `3` · `Clean` · empty · empty; mobile `mobile` · `main` · ↑ `1` ↓ `3` · `Clean` · empty · `Fetched from all remotes` (its tab's last result); the others W0's. Activating the dashboard read no row (every row without a tab was read 0 s ago by the read clock, which has not moved; rows that follow tabs are never read by the dashboard, 2.10): right after the click and a settle no row `IsReading` and no pass under way (decision WD37); the app log has no `summary read in` line and no pass line since `mark` (a trigger that finds no row to read starts no pass, decision WD35); and the call log since `mark` has no call in any folder but web-app's (whose tab may still refresh on its watcher after the commit).<br>**After step 8:** right after the activation and a settle no row `IsReading` and no pass under way (decision WD37: what the trigger decided, read at once on the UI thread); since `mark2` the app log has no pass line and no `summary read in` line (decision WD35) and the call log no call in scratch (the window's activation reads only rows last read 10 s ago or more, 2.10's triggers 1 and 2); scratch's row unchanged, `Clean`.<br>**After step 9:** one read pass: its line after `mark3`, `Web: read 7 repositories in {ms} ms` (decisions WD27, WD35: api, design-system, billing, infra, scratch, notes and old-site's folder check; web-app, docs and mobile follow their tabs and are not counted); scratch `scratch` · `experiment` · `No upstream` · `1 untracked` (primary; tooltip `1 untracked`) · empty · empty (decision WD5); the call log since `mark2` has scratch's refs read and status read, each with a `Sequence` above `mark3` (made after the clock moved), and none in docs' or mobile's folder (their rows follow their tabs); every other row unchanged; mobile still selected, the toggle enabled with `Show every git command the app has run (Ctrl+J)`. No real time was waited (17.2's read clock).<br>**After step 10:** mobile's row selected; before the click the toggle was enabled with `Show every git command the app has run (Ctrl+J)`; after it the panel shows under the list (2.4: the list y 152 to 469, its ten rows in view; the splitter 469 to 474; the log 474 to 674), its header `Activity log` and `mobile` (2.9: it follows the selected row's worktree), its list one operation row: ✓ `09:00:00` · `Fetch` · `Fetched from all remotes` · `0 ms` (step 4's fetch, which ran in the adopted queue); nothing else; the toggle `selected`, its tooltip `Hide the activity log (Ctrl+J)`.<br>**After step 11:** old-site selected: the panel hidden (nothing to follow), the toggle disabled with the tooltip `{root}\old-site does not exist` (2.9's fourth reason); the list back to y 152 to 674 (decision WD32).<br>**After step 12:** scratch selected: the panel shows again (its open state is the window's, 2.9), its header `scratch`, its list empty (no operation ran in scratch). | | | `w8a-select-a-repository`, `w8b-web-app-opened`, `w8c-docs-opened`, `w8d-row-fetching`, `w8e-fetch-in-the-tab`, `w8f-row-follows-its-tab`, `w8g-read-after-ten-seconds`, `w8h-log-follows-the-row` | Design 2.3 (the leftmost tab, the insertion index), 2.4 (the log under the list), 2.6, 2.7, 2.9, 2.10 (triggers 1, 2 and the 10 s rule by `TabServices.ReadClock`), 3.3 (adoption: the queue, the running operation, the undo history), 3.5, 4.7; 17.2's read clock (W8 alone); D207, D208, D209; Q4 (a change made outside shows when the user comes back to the window, after 10 s). `w8e-fetch-in-the-tab` varies by nature: mobile's tab shows the running fetch's indeterminate progress bar, which animates (as phase 3's `21a`, `21b1` and `21b3` and phase 5's `a2h`); its expected result is structural, and the gate compares it by inspection, not by hash. No other picture varies by nature: `w8a`'s tooltip holds no folder, and old-site's tooltip with the folder (step 11) is asserted, not pictured. Decisions WD3, WD5, WD15, WD17, WD18, WD27, WD32, WD35, WD37. **RW: n/a**: opening a repository tab and its status bar during a fetch are phase 1's and 3's real-window checks; the read clock exists only in the scripted harness (17.2), and a real window could show the 10 s rule only by waiting 10 s; W9 shows the dashboard and a tab opened from it in the real window. |
| W9 | A restart keeps the workspace and the open dashboard (design 1.3, 1.11, 2.1, 2.10; D189, D205, D207): `workspaces.json` and `session.json` after the first start; the second start restores the dashboard first and api's tab after it, api active; the dashboard in the background calls no git until it is activated, then reads its rows; "workspace", dark, 1100×700 | `Check_W9_restart` (a plain `[Fact]`, two app starts on one data folder). First start: 1. "Workspace" scenario. `OpenWorkspaceAsync`. 2. Double-click api's row (66, 165). Wait for api's graph. Screenshot. 3. Close the app (`ShellDriver.Dispose`). Second start, on the same data folder: 4. `ShellDriver.Start(data.Path, 1100, 700)`, writing nothing (`RealApp.Launch(data)` in the real window); wait for api's repository (`WaitForRepositoryAsync`). Screenshot. 5. Click the Web tab. Wait for the read pass. Screenshot. | **After step 2:** tabs [Web, api], api active (index 1), its graph A0; it adopted api's summary-mode view model.<br>**After step 3:** `session.json`: [`TabState(null, "web")`, `TabState(<api's WorkingDirectory>)`], `ActiveTab` 1 (1.11: the dashboard by its workspace's id, the repository by its folder); `workspaces.json` unchanged from what the check wrote: "Web", id `web`, the ten paths in W0's order, old-site's included (a folder that is gone stays, 1.10).<br>**After step 4:** tabs [Web (`DashboardTab`, its icon and title secondary: inactive), api], api active (the saved `ActiveTab`), its graph A0; `AppSession.Initialization` completed. The dashboard in the background read nothing (2.10: a restored dashboard reads nothing until it is active): its view model has started no read and has no pass under way (`DashboardSnapshot("web")`: no row `IsReading`, `IsPassRunning` false; decision WD37); the app log has no `Web: read` line, and the call log since the start has no call in web-app, docs, mobile, design-system, billing, infra, scratch or notes (decision WD25).<br>**After step 5:** the dashboard active; one read pass, its line `Web: read 9 repositories in {ms} ms` (decisions WD27, WD35: the eight repositories with a folder but api, and old-site's folder check; api's row follows its tab); W0's rows and texts, api's from its tab (the same values); no row selected; the list has the focus; the call log since step 4 has a read in each of the eight repositories with a folder but api (old-site: none, its folder check found it gone). TD. | | | `w9a-api-opened`, `w9b-restarted`, `w9c-dashboard-read`; real window: `w9a-api-opened`, `w9b-restarted`, `w9c-dashboard-read` | Design 1.3 (the store), 1.11 (the restore: dashboards first, the active tab mapped), 2.1, 2.3, 2.10 (at start); D46, D189, D205, D207. No picture varies by nature (no folder is drawn at 1100). Decisions WD25, WD27, WD35, WD37. **RW: steps 1 to 5** as a real restart: `RealApp.WriteWorkspaces` and `RealApp.Launch(data, session)`, a real double-click on api's row, `RealApp.Close`, then `RealApp.Launch(data)` on the same data folder, a real click on the Web tab; the app log file (`AppLogFile`) read for the absence of `Web: read` before that click and its presence after; `session.json` and `workspaces.json` read between the launches; captures after steps 2, 4 and 5 compared with `w9a-api-opened`, `w9b-restarted` and `w9c-dashboard-read`. What only the real window proves: a dashboard tab restored by a real process from `session.json` and `workspaces.json`, first in the strip, without a read until a real click activates it. |
| K10 | The command palette (D191, D212; design 5): Ctrl+Shift+P over the graph without dimming, the commands with their keys at once and the first highlighted, PageDown and the wrapping arrows; a query filtering the commands and the objects' menu items by every word, a destructive item in the danger colour and a disabled one dimmed with its menu's reason, Enter on a disabled entry doing nothing, the window's keys declining; a second Ctrl+Shift+P selecting the query; an entry run by Enter after the palette has closed (a checkout: the graph, the status bar, Undo, the log); a dialog opened from the palette by a click on its entry and Esc back to the graph; the pointer's move highlighting a row, a click on a disabled row doing nothing, the wheel scrolling the list; `No matches` and Tab staying in the box; the session's runs first in an empty palette; Esc and a press outside the card cancelling; on a dashboard, `Open repository web-app` opening a tab at the end, the repository's commands naming it while a dashboard is open, and `Open workspace Web` going back to the dashboard; "ops" and "Web", dark, 1100×700 | Method 1 (`Check_K10_palette_search_and_run`). 1. "Ops" scenario. Start the app. Click the row "Main work" (row 3) in its Message column, (484, 207). 2. Press Ctrl+Shift+P. Screenshot. Press PageDown three times, then Down, then Up. Turn the wheel one notch up over the list at (550, 297) (`ShellDriver.Wheel`, which moves the pointer there first). Move the pointer to the neutral point. 3. Type `zzz`. Press Tab. Press Ctrl+A; type `delete main`. Screenshot. Press Up; press Enter; press Down. Move the pointer onto row 1 at (550, 185); click it. Move the pointer to the neutral point. Press Ctrl+T. 4. Press Ctrl+Shift+P. Type `check out feature/alpha`. Press Enter; wait for `Check out feature/alpha`. Screenshot. 5. Press Ctrl+Shift+P; type `create branch`. Click row 0 (`Create branch…`) at (550, 157). Screenshot. Press Esc. 6. Press Ctrl+Shift+P. Press Esc. Press Ctrl+Shift+P. Click (480, 600), outside the card. Method 2 (`Check_K10_palette_opens_repositories_and_workspaces`). 7. "Web" with its dashboard the only tab (`OpenWorkspaceAsync`). Press Ctrl+Shift+P. Type `open repository web-app`. Screenshot. Press Enter; wait until web-app's tab has drawn its rows and its queue is idle. 8. Press Ctrl+Shift+P. Screenshot. Type `open workspace web`. Press Enter. | **At the start of method 1:** OPS0 (scenario facts 6); nothing selected; Undo disabled `Nothing to undo`, Redo `Nothing to redo`; one tab.<br>**After step 1:** row 3 "Main work" selected (`VcSelectionBrush`), the details panel shows `88d6b91` Main work, the keyboard focus in `GraphRows`.<br>**After step 2 (`k10a-palette-commands`):** `IsPaletteOpen`, `Dialogs.IsOpen`; the root `CommandPalette`, a Pane named `Command palette`, no title drawn, no dimming (the backdrop `Panel.paletteBackdrop` is transparent: toolbar, left panel, graph with Main work selected, details panel and status bar show as after step 1 around the card). The card `CommandPaletteCard` x 250 to 850, y 96 to 488 (600 × 392: 12 rows), 5.3's look. The box `CommandPaletteBox` (Edit named `Search commands`) x 259 to 841, y 105 to 137, empty, its placeholder `Search commands, branches, tags, stashes, remotes, repositories and workspaces` whole (490.1 px at 12.5; decision KD13), the keyboard focus in it. The list `CommandPaletteList` (List named `Commands`) from y 143, `Shown` 33 commands and no item (an empty box lists commands only, 5.6), 12 in view, the theme's thin vertical scroll bar at its right; `ShowsNothing` false. Rows in 5.4's order (decision KD1), no detail on the repository's commands (one tab, no dashboard: `NamesRepositoryInDialogs` false, 3.8): 0 `Fetch all remotes` · `Ctrl+Shift+F` (highlighted); 1 `Pull` · `Ctrl+Shift+L`; 2 `Push` · `Ctrl+Shift+K`; 3 `Create branch…` · `Ctrl+B`; 4 `Stash changes…` · `Ctrl+Shift+S` (disabled: "There are no changes to stash"); 5 `Pop the newest stash` · `Ctrl+Shift+O`; 6 `Undo` · `Ctrl+Z` (disabled: "Nothing to undo"); 7 `Redo` · `Ctrl+Y` (disabled: "Nothing to redo"); 8 `Search commits` · `Ctrl+F`; 9 `Go to the commit message` · `Ctrl+Shift+M` (disabled: "There are no changes to commit"); 10 `Show reflog` · `Ctrl+Shift+R`; 11 `Show backups` (no key); in `Shown` after them, scrolled out: 12 `Refresh` · `F5`; 13 `Create tag at HEAD…`; 14 `Add remote…`; 15 `Manage remotes…`; 16 `Show all hidden branches` (disabled: "No branch is hidden"); 17 `Stop soloing` (disabled: "No branch is soloed"); 18 `Filter branches and tags` · `Ctrl+Shift+E`; 19 `New tab` · `Ctrl+T`; 20 `Close tab` · `ops` · `Ctrl+W`; 21 `Next tab` · `Ctrl+Tab` (disabled: "There is only one tab"); 22 `Previous tab` · `Ctrl+Shift+Tab` (disabled: "There is only one tab"); 23 `Open repository…` · `Ctrl+O`; 24 `Clone repository…` · `Ctrl+Shift+C`; 25 `Init repository…` · `Ctrl+Shift+I`; 26 `New workspace…`; 27 `Show the activity log` · `Ctrl+J`; 28 `Open settings…` · `Ctrl+,`; 29 `Show keyboard shortcuts…`; 30 `Choose an editor…` (no editor chosen); 31 `Use the light theme`; 32 `Follow the system's theme` (Dark chosen: `Use the dark theme` left out, 11.7). Not listed: `Next match` and `Previous match` (no search bar), `Stage all`, `Unstage all` and the commit entry (no working changes), 5.4's dashboard rows 11, 12 and 16. UIA of row 0: Name `Fetch all remotes`, ItemStatus empty, no HelpText, AcceleratorKey `Ctrl+Shift+F`, selected; of row 4: Name `Stash changes…`, HelpText `There are no changes to stash`, AcceleratorKey `Ctrl+Shift+S`. One Debug line `ops: Command palette opened`. The opening starts the worktree read on the thread pool (5.5), so the check waits until the call log holds `git worktree list --porcelain` since the press (a condition), then asserts that since the press there is no other call and no write. After the PageDowns the highlight is on row 11 `Show backups`, then row 22 `Previous tab`, then row 32 `Follow the system's theme` (clamped at the end), each scrolled whole into view; Down wraps to row 0, Up back to row 32, which leaves rows 21 to 32 in view; the box's text stays empty. The wheel (5.8): the pointer's move to (550, 297) highlights the row under it, the sixth in view, row 26 `New workspace…` (`HighlightedIndex` 26, on `VcSelectionBrush`); the notch scrolls the list up: its vertical offset smaller than before it and larger than 0, so a row before 21 is now in view; nothing runs (no `Command palette ran:` line), the palette stays open, the box keeps the focus and its empty text. Which row is highlighted after the notch is not asserted: 5.8 does not say whether a row scrolled under a resting pointer takes the highlight, and step 3's first key highlights the first match again (5.6).<br>**After step 3:** with `zzz` typed nothing matches (no command and no item holds it in its label or detail): `ShowsNothing` true, `Highlighted` null, no `CommandPaletteList`; in its place `CommandPaletteEmpty` `No matches` (5.3: `VcTextSecondaryBrush` `#9BA3B0`, 28 high, y 143 to 171, its text from x 269, 10 from the list's left), the card 84 high (y 96 to 180). Tab: the focus stays in `CommandPaletteBox` (5.8: the list and its rows take no focus, the card cycles), its text still `zzz`, nothing ran. Ctrl+A is the box's own (no key of 6.1; the window's keys decline anyway): it selects `zzz`, which the typing replaces.<br>**`k10b-palette-delete-main`:** `Query` `delete main`, the highlight on row 0; `Shown` 2, both tier 0 (the label starts with the query), the enabled first (5.6): 0 `Delete main on origin…` · `origin/main` (danger: the remote branch `origin/main`'s menu item, label in `VcDangerBrush` `#E5606B`; highlighted); 1 `Delete main…` (disabled: "You cannot delete the branch you are on": `main`'s menu item, its label and the reason in `VcTextDisabledBrush` `#5F6775`, the reason whole, 221.9 px). No key on either. Card y 96 to 208 (56 + 2 × 28), rows centred at y 157 and 185. UIA: row 0 Name `Delete main on origin…`, ItemStatus `origin/main`; row 1 Name `Delete main…`, ItemStatus `main`, HelpText `You cannot delete the branch you are on`. No other entry holds both words: no command, no other ref's item, the selection's `ForCommit` items (detail `88d6b91 Main work`) none with `delete`. Up wraps the highlight to row 1; Enter on it does nothing: the palette stays open, no `Command palette ran:` line, no git write (5.7); Down puts it back on row 0. The pointer's move onto row 1 highlights it (`HighlightedIndex` 1, on `VcSelectionBrush`, 5.8); the click on it (press and release on the row) runs nothing, as Enter: the palette stays open with `delete main`, the focus still in the box (the rows take no focus), no `Command palette ran:` line, no git write. The move to the neutral point leaves the card. Ctrl+T declines (a dialog is open, 6.3): one tab still, the query unchanged, nothing typed.<br>**After step 4:** Ctrl+Shift+P with the palette the top dialog selects the box's whole text (`SelectionStart` 0, `SelectionEnd` 11; decision KD20), the palette open, no second `opened` line. Typing replaces it: `Query` `check out feature/alpha`; `Shown` 2: 0 `Check out feature/alpha` · `feature/alpha` (tier 0, highlighted), 1 `Check out origin/feature/alpha` · `origin/feature/alpha` (tier 1: `feature/alpha` starts a word after `/`). Enter: the palette closed before the checkout ran (`IsPaletteOpen` false), the focus back in `GraphRows`; Debug line `ops: Command palette ran: Check out feature/alpha (feature/alpha)`; one operation `Check out feature/alpha`, its one write `git switch --no-guess --no-overwrite-ignore feature/alpha`, exit 0, stdout `Your branch is up to date with 'origin/feature/alpha'.`, stderr `Switched to branch 'feature/alpha'` (key); the queue's line `ops: Check out feature/alpha: Succeeded after <N> ms: Checked out feature/alpha`. Git: HEAD `refs/heads/feature/alpha` at `ce5ea6a…`, `## feature/alpha...origin/feature/alpha`, clean, the stash kept. The picture (`k10c-checked-out-by-palette`): K-OPSa, "Main work" still selected (kept by SHA) with its details; the left panel's `feature` folder open with `alpha` in the accent colour; status bar `Checked out feature/alpha` (secondary), branch part `feature/alpha`; Undo enabled, its tooltip `Undo checkout of feature/alpha (Ctrl+Z)` (6.8), Redo `Nothing to redo`.<br>**After step 5 (`k10d-create-branch-from-palette`):** with `create branch` typed, `Shown` 10, all tier 0 and enabled: 0 `Create branch…` · `Ctrl+B` (the command; highlighted), then the items `Create branch here…` in 5.5's source order (decision KD1): 1 · `feature/alpha`, 2 · `feature/conflict`, 3 · `feature/ff`, 4 · `main` (local branches, the checked-out first, then the left panel's order), 5 · `origin/feature/alpha`, 6 · `origin/main`, 7 · `v1.0`, 8 · `v1.1`, 9 · `88d6b91 Main work` (the selection's); card 336 high (y 96 to 432). The pointer's move onto row 0 leaves the highlight there; the click on it (press and release on the row, 5.8) runs it as Enter would: the palette closed, then Debug `ops: Command palette ran: Create branch…` and the toolbar's Branch: the name dialog `NameDialog`, title `Create branch`, `NameDialogContext` `From ce5ea6a "Alpha two"` (HEAD's commit), `NameBox` empty with its placeholder `Branch name` and the keyboard focus, `NameError` empty, `NameDialogOption` `Check out the new branch` ticked, `Cancel` and `Create branch` (primary, disabled), over the dialog layer's dimming backdrop (phase 3's dialog, unchanged). Esc: no dialog, no branch made (`git for-each-ref refs/heads`: the four), the focus back in `GraphRows`.<br>**After step 6:** the empty palette lists the session's run first (5.6): 0 `Create branch…` · `Ctrl+B` (highlighted), then the catalogue's order without it: 1 `Fetch all remotes`, 2 `Pull`, 3 `Push`, 4 `Stash changes…` (disabled), 5 `Pop the newest stash`, 6 `Undo checkout of feature/alpha` · `Ctrl+Z` (enabled), 7 `Redo` (disabled: "Nothing to redo"), 8 `Search commits`, 9 `Go to the commit message` (disabled), 10 `Show reflog`, 11 `Show backups`; `Shown` 33 (`Check out feature/alpha`, an item, is not listed in an empty box). Esc: closed, nothing ran, the focus back in `GraphRows`. The press at (480, 600) closes the reopened palette and is handled by the backdrop: nothing ran, no menu, "Main work" still the selected row, the focus back in `GraphRows`. Over the method the log has 4 `ops: Command palette opened` lines and exactly 2 `ops: Command palette ran:` lines (steps 4 and 5).<br>**At the start of method 2:** the dashboard of "Web" the only tab, rows as scenario facts 2.3, no row selected.<br>**After step 7, before typing:** `Command palette opened` (no prefix: a dashboard has no repository); `Shown` 22 commands: 0 `Refresh` · `F5` (highlighted; no detail on a dashboard, decision KD2), 1 `Fetch every repository of Web` · `Ctrl+Shift+F`, 2 `Pull every repository of Web` · `Ctrl+Shift+L`, 3 `Add repository…`, 4 `Add open tabs` (disabled: "No tab has a repository open"), 5 `Rename workspace…`, 6 `Delete workspace…` (danger), 7 `New tab` · `Ctrl+T`, 8 `Close tab` · `Web` · `Ctrl+W`, 9 `Next tab` · `Ctrl+Tab` (disabled: "There is only one tab"), 10 `Previous tab` · `Ctrl+Shift+Tab` (disabled), 11 `Go to the next dashboard` · `Web` · `Ctrl+Shift+D` (decision KD3), then `Open repository…`, `Clone repository…`, `Init repository…`, `New workspace…`, `Show the activity log` · `Ctrl+J` (disabled: "Select a repository to see its activity", 2.9), `Open settings…` · `Ctrl+,`, `Show keyboard shortcuts…`, `Choose an editor…`, `Use the light theme`, `Follow the system's theme`.<br>**After typing (`k10e-palette-open-repository`):** `Shown` 1: `Open repository web-app` · `{root}\web-app` (tier 0, highlighted; the label 152.1 px), the detail cut at its end with "…" (its UIA ItemStatus the whole folder, decision KD4), no key and no key column (the detail runs to the row's padding, the lead's ruling 4); card y 96 to 180, the row centred at y 157; the dashboard behind it unchanged.<br>**After Enter:** Debug `Command palette ran: Open repository web-app ({root}\web-app)`; 5.10: no tab shows the worktree and the active tab is a dashboard, so a new tab at the end: tabs [`Web` (dashboard tab, `IconWorkspace`), `web-app`], web-app active and first in the recent list, its tab adopting the row's summary-mode view model (3.3: one queue); web-app's graph (scenario facts 2.4): 0 `main` ✓ Style the pages 12:02 `cbd9037`, 1 Add the about page 12:01 `0d6e6c5`, 2 `origin/main` Initial commit 12:00 `147c6e3`; left panel `main` ↑2; status bar `main`. The dashboard's web-app row follows the tab (3.5); no read of the other rows (the dashboard is in the background, 2.10).<br>**After step 8 (`k10f-palette-names-repository`):** `web-app: Command palette opened`; `Shown` 34; the repository's commands carry its name as detail while a dashboard tab is open (5.4, 3.8): 0 `Fetch all remotes` · `web-app` · `Ctrl+Shift+F` (highlighted), 1 `Pull` · `web-app` · `Ctrl+Shift+L`, 2 `Push` · `web-app` · `Ctrl+Shift+K`, 3 `Create branch…` · `web-app` · `Ctrl+B`, 4 `Stash changes…` · `Ctrl+Shift+S` (disabled: "There are no changes to stash"; ItemStatus `web-app`), 5 `Pop the newest stash` · `Ctrl+Shift+O` (disabled: "There are no stashes"), 6 `Undo` · `Ctrl+Z` (disabled: "Nothing to undo"), 7 `Redo` · `Ctrl+Y` (disabled: "Nothing to redo"), 8 `Search commits` · `web-app` · `Ctrl+F`, 9 `Go to the commit message` · `Ctrl+Shift+M` (disabled: "There are no changes to commit"), 10 `Show reflog` · `web-app` · `Ctrl+Shift+R`, 11 `Show backups` · `web-app`; further down `Next tab` and `Previous tab` enabled (two tabs) and `Go to the next dashboard` · `Web`. Typing `open workspace web`: `Shown` 1, `Open workspace Web` · `10 repositories` (tier 0; the other `Open repository {name}` items hold `workspace` and `open` but not `web`, the active tab's is left out). Enter: `web-app: Command palette ran: Open workspace Web (10 repositories)`; `ShowWorkspace("web", from: web-app's tab)` finds its dashboard open and activates it (2.2's first case): Web active, tabs unchanged ([Web, web-app]), no tab made, `session.json`'s active tab 0. | | | `k10a-palette-commands`, `k10b-palette-delete-main`, `k10c-checked-out-by-palette`, `k10d-create-branch-from-palette`, `k10e-palette-open-repository`, `k10f-palette-names-repository`; real window: `k10a-palette-commands`, `k10b-palette-delete-main`, `k10c-checked-out-by-palette`, `k10d-create-branch-from-palette` | Design 5.1 to 5.11, 6.3, 6.5, 6.8, 3.3, 3.8, 2.2, 13.4, 14.7, 14.14; D191, D212, D205, D163. **RW: method 1, steps 1 to 6**, in one launch (`Phase6RealWindowPass.K.cs`) on a fresh "ops" copy at 1100×700, dark: the real click on "Main work" (`RealApp.GraphRowPoint(3, 3)`: display row 3 of a graph of 3 lanes), real Ctrl+Shift+P (`PressKeys(CONTROL, SHIFT, KEY_P)`), the queries typed key by key (the rows read after the last key; `No matches` as the Text `CommandPaletteEmpty`), real PageDown, Down, Up, Tab, Ctrl+A, Enter, Esc and Ctrl+T, the real mouse's wheel notch over the list, its move onto and click on the disabled `Delete main…` and its click on `Create branch…`, all inside the card, and its press outside the card at (480, 600), inside the window; the rows read through UI Automation (the Pane's name, each `CommandPaletteItem`'s Name, ItemStatus, HelpText, AcceleratorKey and selection); the log's lines counted (4 opened, 2 ran); captures compared with `k10a`, `k10b`, `k10c`, `k10d`. Then, in the same launch: real Ctrl+T, then Ctrl+Tab (the `ops` tab active again: its `CurrentBranch` `feature/alpha`), Ctrl+Shift+E (UIA's focused element `FilterBox`), F6 (focused element `GraphRows`, decision KD22); `FetchButton`'s AcceleratorKey `Ctrl+Shift+F` and HelpText `Fetch all remotes (Ctrl+Shift+F)`; a real right-click at (484, 360) and the menu item `Fetch all remotes`'s AcceleratorKey `Ctrl+Shift+F` (UIA property 30006) and its `ContextMenuItemKey` text, then Esc. What only the real window proves: Windows' key events reaching the window's tunnelling router (Ctrl+Shift+P, Ctrl+Tab before `KeyboardNavigationHandler` moves the focus, F6), a real text box typing the query key by key, and UI Automation's `AcceleratorKey`, `ItemStatus` and `HelpText` as the Win32 provider gives them, which 0.7 read only in the IL. Method 2 is scripted only. `k10a`, `k10b`, `k10d`, `k10e` and `k10f` vary by nature at the focused box's caret. `k10e`: the row's content is 562 wide (x 269 to 831), and a row without a key has no key column (the lead's ruling 4, design 5.3: `CommandPaletteItemKey` hidden, its 16-px gap with it, the detail running to the row's padding), so the detail's room is 562 − 152.1 (the label) − 8 = 401.9 px. With "…" (9.4 px at 11.5) the text before it is at most 392.5 px. On the development machine `C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests\workspace-` alone is 382.5 px at 11.5, so the first hex digit of the temporary folder's random part (4.1 to 7.4 px) shows, or the first two when both are narrow (`f` 4.1, `1` 5.3). **`k10e` therefore varies by nature at the random part's first one or two characters** (besides its caret); the pixels left of them vary only with the machine's user folder. On CI's Windows the user folder `C:\Users\RUNNER~1\…` makes that prefix 412.0 px, so the cut falls before `workspace-`; on CI's Linux the whole folder (`/tmp/VisualCommit.Tests/workspace-<12 hex>/web-app`, about 335 px) shows, random part included. |
| K11 | Keys in menus and tooltips (D192, D199, D215; design 6.7, 6.8): the empty graph area's menu and the working-changes row's with their keys at the right, the newest stash's `Pop stash` keyed and another branch's push not; every toolbar button's tooltip and help text with its key while enabled, a disabled one's reason alone, every keyed control's `AcceleratorKey` enabled or not; the commit button's `Commit 1 file (Ctrl+Enter)` with a file staged and a summary typed, and no tooltip while it cannot commit; the activity log toggle's, "+"'s and the welcome page's; the close button's key only on the active tab, following a switch; "ops", dark, 1100×700 | One app start (`Check_K11_keys_in_menus_and_tooltips`). 1. "Ops" scenario. Start the app. Right-click the empty graph area at (484, 360), below row 7. Screenshot. Press Esc. 2. Read the toolbar's, the tab strip's and the status bar's tooltips, help texts and AcceleratorKeys. Rest the pointer on `PushButton`. Screenshot. Rest it on `StashButton`. Right-click the stash's row (row 0) in its Message column, (484, 129); press Esc. Right-click the label `feature/alpha` on row 4; press Esc. 3. Outside the app: write `src/core.txt` "core\nmain work\nmore\n"; wait for the status read with 0 staged and 1 unstaged. 4. Right-click the working-changes row (display row 0, (484, 129)). Screenshot. Click `Stage all`; wait for the status read with 1 staged and 0 unstaged. 5. Move the pointer onto `CommitButton`. Click `CommitSummary`; type `Note more core work`. Rest the pointer on `CommitButton`. Screenshot. 6. Rest the pointer on `ActivityLogToggle`. Click "+" (`AddRepoButton`). Rest the pointer on the `ops` tab's close button, then on the new tab's. Click the `ops` tab's title; rest the pointer on each close button again. | **After step 1 (`k11a-empty-area-menu-keys`):** one menu, its top-left at (486, 362) ±2 (phase 3's 4a: 2 right of and 2 below the pointer): header `ops`; `Fetch all remotes` [Ctrl+Shift+F] (highlighted); `Pull` [Ctrl+Shift+L]; `Push` [Ctrl+Shift+K]; —; `Create branch…` [Ctrl+B]; `Stash changes…` [Ctrl+Shift+S] (disabled: "There are no changes to stash"); —; `Show reflog` [Ctrl+Shift+R]; `Show backups`; `Manage remotes…`. 8 items and 2 separators: 258 high ±2 (phase 3's `03f`'s height). Each key right-aligned 10 before the item's right edge, the disabled item's label and key in `VcTextDisabledBrush` `#5F6775`, the others' keys in `VcTextSecondaryBrush` `#9BA3B0`; each item's `Header` its label alone. The menu as wide as `Stash changes…` with its key, 10 + 105.8 + 24 + 73.1 + 10 ≈ 223 ±3 (phase 3's `03f` was 200, the minimum). Log `ops: Context menu opened: ops by pointer`.<br>**After step 2:** tooltip = help text, then `AcceleratorKey`: `FetchButton` `Fetch all remotes (Ctrl+Shift+F)`, `Ctrl+Shift+F`; `PullButton` `Pull from origin/main (Ctrl+Shift+L)`, `Ctrl+Shift+L`; `PushButton` `Push main to origin/main (Ctrl+Shift+K)`, `Ctrl+Shift+K`; `BranchButton` `Create a branch at HEAD (Ctrl+B)`, `Ctrl+B`; `StashButton` (disabled) `There are no changes to stash`, `Ctrl+Shift+S`; `PopButton` `Pop the newest stash (Ctrl+Shift+O)`, `Ctrl+Shift+O`; `UndoButton` (disabled) `Nothing to undo`, `Ctrl+Z`; `RedoButton` (disabled) `Nothing to redo`, `Ctrl+Y`; `SettingsButton` `Open settings (Ctrl+,)`, `Ctrl+,`; `SearchButton` `Search commits (Ctrl+F)`, `Ctrl+F`; `ThemeSwitch` `Switch to the light theme`, no AcceleratorKey (no key, 6.1); `AddRepoButton` (name `Open a new tab`) `Open, clone or init a repository (Ctrl+T)`, `Ctrl+T`; the only tab's `CloseTabButton` `Close this tab (Ctrl+W)`, `Ctrl+W`; `ActivityLogToggle` `Show every git command the app has run (Ctrl+J)`, `Ctrl+J`. The toolbar's `FetchDisabledReason` to `PopDisabledReason` null but `StashDisabledReason` `There are no changes to stash`. The picture (`k11b-push-tooltip-key`): the pointer on Push, its tooltip `Push main to origin/main (Ctrl+Shift+K)` (the branch, its upstream and the key put into 6.8's text) below it in the tooltip colours; the window as at the start. Fetch's tooltip with its key is pictured by part L's `l31f-enabled-tooltip` in both themes, so K11 asserts it structurally (decision KD23). On `StashButton` its tooltip `There are no changes to stash` (shown on the disabled button), no key in it. The stash's menu: header `stash@{0} On main: Experiment`; `Apply stash`; `Pop stash` [Ctrl+Shift+O]; `Drop stash…` (danger); —; `Copy SHA`. `feature/alpha`'s menu (another branch): no item has a key, its `Push feature/alpha to origin/feature/alpha` included (6.7: another branch's push is not keyed).<br>**After step 3:** K-OPSw; the stage panel not shown: "Alpha two" (now display row 5) is selected, by step 2's right-click on its label; `StashButton` enabled: `Stash your changes (Ctrl+Shift+S)`, `StashDisabledReason` null.<br>**After step 4 (`k11c-working-row-menu-keys`):** the working-changes row selected (a right-click selects it), the stage panel behind the menu; the menu at (486, 131) ±2: header `Working changes`; `Stage all` [Ctrl+Shift+A] (highlighted); `Unstage all` [Ctrl+Shift+X] (disabled: "Nothing staged"); `Discard all changes…` (danger, no key: 6.7); —; `Stash changes…` [Ctrl+Shift+S]; `Create branch here…` [Ctrl+B]; —; `Show reflog` [Ctrl+Shift+R]; `Show backups`; `Show in folder`. 258 high; as wide as `Stash changes…` with its key (222.9; `Create branch here…` with its key 214.9), ≈ 223 ±3. After `Stage all`: one write `git add --all -- src/core.txt`, exit 0; git `M  src/core.txt`; the stage panel `Unstaged files (0)`, `Staged files (1)` with M `core.txt` `src`; the commit button `Commit 1 file`, disabled (the summary is empty).<br>**After step 5:** before typing, with the pointer moved onto it and no wait (no tooltip may open there), the disabled commit button has no tooltip (`ToolTip.GetTip` null; `CommitDisabledReason` null: an empty summary has no reason, 6.8) and no help text (`AutomationProperties.HelpText` null), its AcceleratorKey `Ctrl+Enter`. After typing: the summary `Note more core work`, its counter `53`; the commit button enabled `Commit 1 file`, its tooltip and help text `Commit 1 file (Ctrl+Enter)` (D199), AcceleratorKey `Ctrl+Enter`. The picture (`k11d-commit-tooltip-key`): the stage panel with the staged `core.txt`, the summary, the description empty, and the tooltip under the pointer on the commit button. Nothing committed (`main` still `88d6b91`).<br>**After step 6:** the toggle's tooltip `Show every git command the app has run (Ctrl+J)`. After "+": tabs [`ops`, `New tab`], the new tab active with its welcome page; on it `OpenRepoButton` `Open a repository on this computer (Ctrl+O)`, `Ctrl+O`; `CloneRepoButton` `Clone a repository from a URL (Ctrl+Shift+C)`, `Ctrl+Shift+C`; `InitRepoButton` `Create a new repository in a folder (Ctrl+Shift+I)`, `Ctrl+Shift+I`; the toolbar's Fetch to Search disabled without a tooltip (their AcceleratorKeys still set); the activity log toggle disabled with `Show every git command the app has run (Ctrl+J)` (6.8). The inactive `ops` tab's close button: tooltip and help text `Close this tab`, no AcceleratorKey (`CloseKeyText` null); the active new tab's `Close this tab (Ctrl+W)`, `Ctrl+W`. After the click on `ops`: swapped, `ops`' `Close this tab (Ctrl+W)` with `Ctrl+W`, the new tab's `Close this tab` without. | | | `k11a-empty-area-menu-keys`, `k11b-push-tooltip-key`, `k11c-working-row-menu-keys`, `k11d-commit-tooltip-key` | Design 6.7, 6.8, 14.10, 14.11, 15.5; D199, D215. n/a: the real window reads `FetchButton`'s and a menu item's `AcceleratorKey` and help text in K10's launch; tooltips are scripted only, as phase 5's A1's. `k11d` varies by nature at the summary box's caret (the focus stays in it while the pointer rests on the button). The commit button keeps its enabled look; its tooltip's text is the only new pixel there. |
| K12 | Every main action from the keyboard alone (R7, D192, D213, D214; design 6.1 to 6.6, 6.9, 6.10; the Done when's "every main action is reachable from the keyboard"), each asserted by what it ran: New tab, Open with the folder dialog, Ctrl+Tab and Ctrl+Shift+Tab round the strip, Mod+1, Mod+9, Ctrl+PageUp; a disabled command's key consumed and doing nothing; F6 and Shift+F6 round the regions, each first visit on 6.6's target and a region's last element remembered (focus outlines pictured); Stage all, Go to the commit message, a summary and Mod+Enter committing; Ctrl+Z in a text box undoing typing only, then Mod+Z and Mod+Y undoing and redoing the commit (D163); Enter on a file row opening its diff, Mod+Shift+H and Mod+Shift+B opening the file's history and blame from it; Shift+F10 on a branch, Down and Enter in its menu; Fetch, Pull, Push, Create branch, Pop, Stash, Search with F3 and Shift+F3 between its matches, Show reflog and the Esc chain; Mod+Shift+D without a dashboard; Mod+, opening the settings screen while the window's keys decline; Init cancelled, Clone and Mod+W; F5; "ops" (with "graph"), dark, 1100×700 | Part A (`Check_K12_tabs_regions_and_the_commit_by_keys`). 1. "Ops" scenario with `src/core.txt` changed before the start; a "graph" copy made. Start the app. Press Ctrl+T. 2. Queue the "graph" copy's folder in `Folders`; press Ctrl+O; wait until its tab has drawn its rows. 3. Press Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+1, Ctrl+9, Ctrl+PageUp, reading the active tab and `session.json` after each. 4. Press Ctrl+Shift+X. 5. Press Ctrl+Shift+E. 6. Press F6. Screenshot. Press Home. 7. Press F6. Screenshot. 8. Press Ctrl+Shift+A; wait for the status read with 1 staged and 0 unstaged. 9. Press Ctrl+Shift+M. Type `Keys only`. 10. Press Ctrl+Enter; wait until `main` is at the new commit and the status read shows nothing changed. 11. Press Ctrl+Shift+E. Type `fe`. Press Ctrl+Z. 12. Press Ctrl+J. Press Shift+F6. Screenshot. 13. Press Esc. Press F6. Screenshot. Press Enter; wait until the diff shows. Press Ctrl+Shift+H; wait until the page lists its rows. Press Esc. Press F6. Press Enter; wait until the diff shows. Press Ctrl+Shift+B; wait until the page shows its lines. Press Esc. 14. Press Ctrl+Z; wait for `Undo commit "Keys only"`. 15. Press Ctrl+Y; wait for `Redo commit "Keys only"`. Part B (`Check_K12_menus_remotes_stash_and_dialogs_by_keys`). 16. "Ops" scenario. Start the app. Press Ctrl+Shift+M. 17. Press Ctrl+Shift+E; press Tab until the focused element is the `RefItem` named `main`, at most six times. 18. Press Shift+F10; press Esc. Press Shift+F10; press Down twice. Screenshot. Press Enter; wait for `Push main`. 19. Press Ctrl+Shift+F; wait for `Fetch`. Press Ctrl+Shift+L; wait for `Pull`. Press Ctrl+Shift+K; wait for `Push main`. 20. Press Ctrl+B; press Esc. 21. Press Ctrl+Shift+O; wait for `Pop stash@{0}`. 22. Press Ctrl+Shift+S; type `Experiment again`; press Enter; wait for `Stash`. 23. Press Ctrl+F; type `alpha`; wait until `SearchCount` reads `2 matches`. Press F3, F3, then Shift+F3. Press Esc. 24. Press Ctrl+Shift+R; wait until the reflog page shows its rows; press Esc. 25. Press Ctrl+Shift+D; press Esc. 26. Press Ctrl+,; press Ctrl+T; press Esc. 27. Press Ctrl+Shift+I. 28. Press Ctrl+Shift+C; press Ctrl+W. 29. Press F5; wait until idle. | **At the start of part A:** K-OPSw, nothing selected, the right panel's `DetailsPlaceholder` (no commit selected); one tab; git ` M src/core.txt`.<br>**After step 1:** tabs [`ops`, `New tab`], the new tab active with its welcome page (`No repository open`), its close button shown (two tabs).<br>**After step 2:** `Folders.Titles` ends with `Open a repository`; 5.10's second case: the active empty tab opens "graph": tabs [`ops`, `graph`], `graph` active, GR0 drawn, status bar `main`, the recent list's first `graph`; no third tab.<br>**After step 3:** the active tab after each key, in order: `ops` (Ctrl+Tab wraps from the last), `graph`, `ops` (Ctrl+1), `graph` (Ctrl+9, the last), `ops` (Ctrl+PageUp); `session.json`'s active tab read back after each: 0, 1, 0, 1, 0. No tab opened or closed.<br>**After step 4:** `Unstage all` is listed and disabled (`Nothing staged`): the key is consumed and runs nothing (6.3): no git write since the step began, no operation record, git still ` M src/core.txt`, nothing selected.<br>**After step 5:** the focus in `FilterBox` (region Left), its text empty.<br>**After step 6 (`k12a-f6-graph`):** the focus on `GraphRows` (region GraphArea), shown by the keyboard focus frame round the graph's rows; K-OPSw, nothing selected; `FilterBox`'s border back to its unfocused colour; the right panel's placeholder. Home selects the working-changes row (display row 0, the graph's own key): the stage panel `WorkingChangesView`: `Unstaged files (1)` with M `core.txt` `src`, `Staged files (0)` with `No staged changes`, the summary empty (counter `72`), the commit button `Stage files to commit` disabled.<br>**After step 7 (`k12b-f6-stage-row`):** region Right; the focus on `UnstagedFileList`'s first row, `core.txt` (6.6's mark), not on `StageFlatButton`; that row shows the focus frame; the working-changes row selected in the graph.<br>**After step 8:** one write `git add --all -- src/core.txt`, exit 0; git `M  src/core.txt`; `Unstaged files (0)`, `Staged files (1)` with M `core.txt`; the commit button `Commit 1 file`, disabled (no summary).<br>**After step 9:** the working-changes row still selected, the focus in `CommitSummary` (region Right); after typing the summary `Keys only` (counter `63`), the commit button enabled.<br>**After step 10:** no line break typed (the summary still one line); one `git commit --file=- --cleanup=whitespace` with the input `Keys only`, exit 0 (stdout `[main 9522dae] Keys only`); git: `main` `9522dae2b1c72b6c45e5dadcf572af1e26798d7f`, `## main...origin/main [ahead 1]`, clean; HEAD's reflog's newest `commit: Keys only`; K-OPSk, row 0 "Keys only" selected with its details; Undo `Undo commit "Keys only" (Ctrl+Z)`.<br>**After step 11:** the focus in `FilterBox`; after typing its text `fe` (the left panel filtered); after Ctrl+Z its text empty again (the box's own undo: one text input, one press) and the left panel whole; no record `Undo commit "Keys only"`, no `git update-ref` since step 10, `main` still `9522dae`, Undo still `Undo commit "Keys only" (Ctrl+Z)` (D163: the window's undo declines in a text box).<br>**After step 12 (`k12c-shift-f6-log`):** the activity log shown under the graph at its default height (the panel y 474 to 674; its header y 474 to 504: `Activity log`, `ops` (secondary), `Show reads` unchecked, the close button; the graph area's content y 88 to 469), the toggle's tooltip `Hide the activity log (Ctrl+J)`. Its list holds exactly two rows, oldest first, both closed (`IconChevronRight`), 24 high: `[IconCheck, VcSuccessBrush #3FB97F] 09:00:00 · Stage · 0 ms` (y 504 to 528) and `[IconCheck] 09:00:00 · Commit · 0 ms` (y 528 to 552), neither with a message (the stage panel's writes report none; phase 5's A1 ran the same two from the stage panel), at the frozen record clock's time and duration; the start's and the refreshes' reads are hidden while `Show reads` is unchecked, step 4's consumed key and step 11's typing made no operation, and the "graph" tab's opening is in graph's log, not ops'. Shift+F6 from the left region goes back round to the last region shown, the activity log: the focus on its first row, `Stage` (6.6's `ActivityLogList` is a `ScrollViewer` with no selection, so its first focusable row), drawn on `VcSelectionBrush` `#2A3150` (the rows' `:focus-visible` look, with no focus frame), `Commit` on the list's background; K-OPSk above it, "Keys only" selected, its details on the right.<br>**After step 13 (`k12d-f6-details-row`):** Esc closes the log (the focus was inside it) and puts the focus on the graph area's content, `GraphRows`; the toggle `Show every git command the app has run (Ctrl+J)`. F6: region Right, the focus on `FileList`'s first row, `core.txt` of "Keys only"'s changes (the region's earlier elements, the unstaged list's row and `CommitSummary`, are no longer shown, so 6.6's target), with the focus frame. Enter on that row (a `Button`, which Enter clicks): its diff opens in place of the graph (D63: `IsDiffOpen`, `src/core.txt` of `9522dae`, K-OPSk's one hunk adding `keys`), the row `core.txt` marked selected. Ctrl+Shift+H (6.1 row 31, the open diff's menu's `File history`): the diff closes and the file history page opens in the graph area (phase 5 design 11.1, 4.1): `FileHistoryTitle` `History of core.txt`, `FileHistoryFolder` `src`, no `FileHistoryFrom` (it starts at HEAD's commit), `FileHistoryList` three rows newest first, Keys only `9522dae`, Main work `88d6b91`, Add core `8beca34` (K-OPSk), the first selected with its change in `FileHistoryDiff`; the right panel still "Keys only"'s details. Esc (no text box has the focus): the page closes and the graph takes the focus (`GraphRows`), K-OPSk with "Keys only" selected. F6: region Right again, the focus back on `FileList`'s `core.txt` row (the region's remembered element, still shown). Enter: the same diff again. Ctrl+Shift+B (row 32, the diff menu's `Blame`): the diff closes and the blame page opens: `BlameTitle` `Blame of core.txt`, `BlameCaption` `at 9522dae Keys only`, `BlameText` `core`, `main work`, `keys`, each line a run of its own commit (`8beca34` Add core, `88d6b91` Main work, `9522dae` Keys only). Esc: the page closes and the graph takes the focus. From the first Enter on, only reads: no write, no operation record, `main` still `9522dae`.<br>**After step 14:** the focus on `GraphRows` since step 13's last Esc. Ctrl+Z: one operation `Undo commit "Keys only"`, among its calls `git update-ref -m "VisualCommit: undo commit \"Keys only\"" --stdin` with the input `update refs/heads/main 88d6b91e821a43cd39c94aa7892c8095b628caf8 9522dae2b1c72b6c45e5dadcf572af1e26798d7f`, exit 0; status `Undid commit "Keys only"`; git `main` `88d6b91`, `M  src/core.txt`, `9522dae` still there (`git cat-file -e`); K-OPSw with the staged change; the summary `Keys only` back in its box (phase 5 design 7.5); Undo `Nothing to undo`, Redo `Redo commit "Keys only" (Ctrl+Y)`.<br>**After step 15:** one operation `Redo commit "Keys only"` with `update refs/heads/main 9522dae2b1c72b6c45e5dadcf572af1e26798d7f 88d6b91e821a43cd39c94aa7892c8095b628caf8`; status `Redid commit "Keys only"`; git as after step 10; HEAD's reflog newest first `VisualCommit: redo commit "Keys only"`, `VisualCommit: undo commit "Keys only"`, `commit: Keys only`; K-OPSk; the summary empty; Undo `Undo commit "Keys only" (Ctrl+Z)`, Redo `Nothing to redo`.<br>**After step 16:** OPS0, clean; `Go to the commit message` is listed and disabled (`There are no changes to commit`): consumed, nothing selected, no stage panel, no write.<br>**After step 17:** the focus first on `FilterBox`, then, Tab by Tab, on the left panel's rows until the `RefItem` named `main` has it, within six presses; among the stops passed, in this order, the section `Local branches` (automation id `Section`) and the folder `feature` (`RefItem` named `feature`) before `main`, and no stop outside the left region (decision KD8). How many other stops lie between them (a scroll viewer of `RefList`'s template, say) is not asserted.<br>**After step 18:** Shift+F10 opens `main`'s menu by keyboard, its top-left at x 4 and y the bottom of the `main` row's bounds ±2 (phase 3's check 5: the row starts at the window's edge): header `main`; `Pull from origin/main` [Ctrl+Shift+L] (highlighted); `Pull from origin/main (rebase)`; —; `Push main to origin/main` [Ctrl+Shift+K]; `Force push main…` (danger); —; `Create branch here…`; `Create tag here…`; `Rename main…`; `Delete main…` (disabled: "You cannot delete the branch you are on"); —; `Change upstream…`; `Stop tracking origin/main`; —; `Show reflog of main`; `Hide main` (disabled: "The checked-out branch is always shown"); `Solo main`; —; `Copy branch name`. 14 items and 5 separators: 441 high ±2; as wide as `Push main to origin/main` with its key, 10 + 145.5 + 24 + 73.3 + 10 ≈ 263 ±3 (`Pull from origin/main (rebase)` alone 194.4). Log `ops: Context menu opened: main by keyboard`, once per Shift+F10. Esc closes it and the focus is on the `main` row again. After the two Downs (`k12e-branch-menu-by-keys`): `Push main to origin/main` highlighted (the second Down passed over the separator), nothing else highlighted; the `main` row of the left panel behind it. Enter: the menu closed; one operation `Push main`, its push `git push --progress origin refs/heads/main:refs/heads/main`, exit 0, stderr `Everything up-to-date` (key); status `Pushed main to origin/main`; `origin.git`'s `main` still `88d6b91`.<br>**After step 19:** `Fetch`: `git fetch --progress origin`, exit 0, nothing printed; status `Fetched from all remotes`. `Pull` (running `Pulling origin/main into main…`): among its calls `git fetch --progress origin`, then `git merge --no-edit --no-overwrite-ignore FETCH_HEAD`, stdout `Already up to date.` (key), exit 0; status `main is already up to date`. `Push main` again as in step 18; status `Pushed main to origin/main`. Git unchanged (OPS0).<br>**After step 20:** the name dialog `Create branch` with `NameDialogContext` `From 88d6b91 "Main work"`, `NameBox` focused; Esc: no dialog, no branch.<br>**After step 21:** one operation `Pop stash@{0}` (running `Popping stash@{0}…`), its pop of `stash@{0}` exit 0, its output ending `Dropped stash@{0} (0e95aca13f7975ef4a1fe029a8b5e9497f31028d)` (key); status `Popped stash@{0}`; K-OPSp (` M README.md`, no stash); Undo `Undo pop of stash "On main: Experiment" (Ctrl+Z)`.<br>**After step 22:** the stash dialog: title `Stash changes`, its message box (placeholder `Message (optional)`) with the keyboard focus, `Include untracked files` ticked (the setting's default), `Cancel` and `Stash`; after typing and Enter: one operation `Stash` (running `Stashing the changes…`), `git stash push --include-untracked --message "Experiment again"`, exit 0, stdout `Saved working directory and index state On main: Experiment again` (key); status `Stashed the changes`; K-OPSs (`stash@{0}` `9b4f935…`, clean); Undo `Undo stash "On main: Experiment again" (Ctrl+Z)` (the entry names the stash by the stash list's message, as phase 5's U20 `stash "On main: Save my work"`).<br>**After step 23:** the search bar (`SearchBar`, phase 5's) under the toolbar, its mode `Commits`, `SearchBox` focused; after typing, K-OPSs' search: `SearchCount` `2 matches`, rows 4 Alpha two and 5 Alpha one marked (phase 5 design 3.3), the others dimmed; nothing selected. F3 (6.1 row 24, the router's on the way down, with the focus in the box): Alpha two selected (the first match from the top, nothing being selected, phase 5 design 3.1), `SearchCount` `1 of 2`, its details (`ce5ea6a` Alpha two) on the right; F3: Alpha one, `2 of 2`; Shift+F3 (row 25): Alpha two again, `1 of 2`; the focus stays in `SearchBox` throughout (phase 5's FD3). Since the press only reads (the search's `git log --no-walk=unsorted --stdin … --grep=alpha`, the details'). Esc (the focus in the box) closes the bar: no `SearchBar`, the marks cleared, Alpha two still selected, the focus on `GraphRows`.<br>**After step 24:** HEAD's reflog page (phase 5's B8) in the graph area, the focus in `ReflogList`, reads only; Esc closes it (no diff over it, no text box focused): the graph back, no page.<br>**After step 25:** no dashboard is open, so the palette opens with `workspace` in its box, the caret at its end (decision KD16): `Shown` 1, `New workspace…` (tier 1, highlighted, no key); no `Open workspace` item (no workspace). `ops: Command palette opened`. Esc: closed, nothing ran.<br>**After step 26:** the key opened the settings screen (`SettingsDialog`) with General shown, `SettingsNavGeneral` selected and focused (D216); Ctrl+T declines while it is open: one tab; Esc: closed, the focus back where it was (what Cancel keeps and writes is S14's).<br>**After step 27:** `Folders.Titles` ends with `Choose a folder for the new repository`, answered as cancelled: no tab made (one tab), no repository made, nothing logged at Warning or Error.<br>**After step 28:** Clone takes a new tab at the end (5.10: `ops` is not empty): tabs [`ops`, `New tab`], the new one active with phase 1's clone form, the focus in `CloneUrlBox`; Ctrl+W, with the focus in that box, closes the active tab: tabs [`ops`], `ops` active.<br>**After step 29:** `Repository.RefreshAsync()`: since the press only reads, among them the refs read's `for-each-ref --format=…` and a `status` read; K-OPSs unchanged, status bar as before. | | | `k12a-f6-graph`, `k12b-f6-stage-row`, `k12c-shift-f6-log`, `k12d-f6-details-row`, `k12e-branch-menu-by-keys` | Design 6.1 to 6.6, 6.9, 6.10, 5.10, 7.2, 14.12; D163, D192, D199, D213, D214, D216, D106. n/a: K10's launch proves the real window's key path (Ctrl+Shift+P, Ctrl+Tab, F6) and its UI Automation; every other key goes through the same router, and phase 5's A4 proved D163's undo keys in the real window. The two methods together press 34 of 6.1's 35 rows, each by its first key, and Ctrl+PageUp: all but Ctrl+Shift+P (K10's); each in the state it needs reached by keys alone (the diff by Enter on a file row for Ctrl+Shift+H and Ctrl+Shift+B, the search's matches by typing for F3 and Shift+F3); `KeyRouterTests` press every tunnelling key, both keys of each row included (6.12). Decisions KD5 (two methods), KD6 (the text box's Ctrl+Z here), KD7, KD8, KD17, KD21. No picture varies by nature: no text box has the focus in them. |
| K13 | The keys in the conflict resolver and on a dashboard (D199, D208, D213; design 6.5, 6.8, 6.9, 2.3, 2.7, 2.9): Ctrl+Tab leaving the resolver's result and keeping the tab, Ctrl+PageDown switching tabs from there and back to the resolver as it was; the resolver's `Next conflict (Alt+Down)` tooltip and its other buttons' texts, the disabled file buttons' kept but not shown (D199); Ctrl+F consumed while Search is disabled; on a dashboard with two workspaces open, Mod+Shift+F running Fetch all and Mod+Shift+D to the next dashboard, which shows the shared results, and round (method 2); F5 reading every row, F6, Home and Down, Mod+B and Mod+Z consumed, Mod+J showing the selected row's activity, Alt+Down, Enter opening a repository beside the dashboards, Mod+1, End and Delete removing a row (method 3); "conflicts" with "ops", and "Web" with "Tools", dark, 1100×700 | Method 1 (`Check_K13_keys_in_the_conflict_resolver`). 1. "Conflicts" stopped in a merge and "ops", two tabs, the first active. Start the app. Click the working-changes row; click the unstaged `notes.txt`; wait until the resolver shows it. 2. Rest the pointer on `ResolverNextConflict`. Screenshot. Read the other four buttons' tooltips, help texts and AcceleratorKeys (no pointer on them). 3. Press Ctrl+F. 4. Click `Use ours` in the ours pane's bar of block 1. Press Ctrl+Tab. 5. Click in the result text on its first line. Press Ctrl+PageDown. 6. Press Ctrl+PageUp. Method 2 (`Check_K13_fetch_all_and_next_dashboard_by_keys`). 7. "Web" and "Tools", the two dashboards as tabs, Web active. Start the app; wait for Web's read. 8. Press Ctrl+Shift+F; wait for the run. 9. Press Ctrl+Shift+D; wait for Tools' read. Screenshot. 10. Press Ctrl+Shift+D; wait until no row of Web reads. Method 3 (`Check_K13_rows_by_keys_on_a_dashboard`). 11. As step 7, on fresh copies and a fresh data folder. Start the app; wait for Web's read. 12. Press F5; wait for the pass. 13. Press F6. Press Home. Press Down twice. 14. Press Ctrl+B. Press Ctrl+Z. Press Ctrl+J. Press Ctrl+J. 15. Press Alt+Down. 16. Press Enter; wait until docs' tab has drawn its rows. 17. Press Ctrl+1; wait until no row of Web reads. 18. Press End. Press Delete. | **After step 1:** GC-M with its banner; the resolver on `notes.txt` in FM (`2 conflicts left`); Search disabled (`Close the conflict resolver first`); tabs [`conflicts`, `ops`].<br>**After step 2 (`k13a-next-conflict-tooltip`):** `ResolverNextConflict`'s tooltip and help text `Next conflict (Alt+Down)`, AcceleratorKey `Alt+Down`, the tooltip under the pointer in the tooltip colours, the resolver as phase 4's C10 shows FM; `ResolverPreviousConflict` `Previous conflict (Alt+Up)`, `Alt+Up`; `ResolverPreviousFile` and `ResolverNextFile` disabled (one conflicted file), their fixed texts kept (6.8: read with `x:Static`, whatever the state): `ToolTip.GetTip` and help text `Previous file (Alt+Left)` and `Next file (Alt+Right)`, `ToolTip.GetShowOnDisabled` false, so no tooltip opens on them (none is waited for), AcceleratorKeys `Alt+Left` and `Alt+Right`; `ResolverCloseButton` `Close (Esc)`, `Esc`.<br>**After step 3:** the key consumed (6.3: a disabled command's key, Mod+F included): no `SearchBar`, the resolver unchanged (FM, the result's text unchanged), no git call.<br>**After step 4:** R1 (16 lines, `1 conflict left`) and the keyboard focus in the result (`ConflictResultEditor`), asserted before the press. Ctrl+Tab: the router declines (a `ConflictResultEditor` among the focused element's ancestors, 6.3), the result leaves Tab with Control to the navigation handler: the focus no longer within `ConflictResultEditor` (`IsKeyboardFocusWithin` false), the result's text still R1 (no tab typed), the active tab still `conflicts`, `session.json`'s active tab 0.<br>**After step 5:** the click puts the focus in the result; Ctrl+PageDown, which the router takes there too (6.5), makes `ops` active (OPS0, status bar `main`); `session.json`'s active tab 1; the `conflicts` tab keeps its resolver open with R1 unsaved.<br>**After step 6:** `conflicts` active again, the resolver on `notes.txt` showing R1, `1 conflict left`; git as at the start: `UU notes.txt`, notes.txt on disk blob `cdd7b39`, `MERGE_HEAD` `5c77ffb…` (the resolver writes nothing before Mark resolved).<br>**After step 7:** tabs [`Web`, `Tools`], both `DashboardTab` with `IconWorkspace`, `Web` active; Web's rows as scenario facts 2.3 after its first read (`Web: read 10 repositories in <N> ms`); the toolbar's `FetchButton` `Fetch every repository of Web (Ctrl+Shift+F)`, `PullButton` `Pull every repository of Web (Ctrl+Shift+L)`, the header's `DashboardRefresh` tooltip `Read the repositories of Web again (F5)`; Tools not read (in the background, 2.10): no `Tools: read` line (infra and scratch are read as Web's rows).<br>**After step 8:** the router ran the dashboard toolbar's Fetch, Fetch all (6.9): the log's `Web: Fetch all: started for 8 repositories, 2 skipped` and `Web: Fetch all: Fetched 8 repositories; 2 skipped`; eight `Fetch` operations, each `git fetch --progress origin` exit 0, at most four at once. K-WEBf's rows, asserted cell by cell, not pictured (part W's `w4b-fetched` pictures the same rows and header after its Fetch all); no row selected; the header `Web`, `10 repositories`, the run's text `Fetched 8 repositories; 2 skipped` (secondary), no bar and no `Cancel all`; the status bar the same text, its branch part `Web` after `IconWorkspace`.<br>**After step 9 (`k13b-next-dashboard`):** Mod+Shift+D goes to the next dashboard after the active tab among the dashboards: `Tools` active (tabs [`Web`, `Tools`]); Tools read (`Tools: read 2 repositories in <N> ms`): the header `Tools`, `2 repositories`; K-TOOLS' rows (infra, scratch), each with `Fetched from all remotes` in its Operation cell (decision KD12); no row selected; the toolbar's Fetch `Fetch every repository of Tools (Ctrl+Shift+F)`, Pull disabled with `No repository of Tools can be pulled`; the status bar `Ready`, its branch part `Tools`.<br>**After step 10:** wrapping round the dashboards: `Web` active again (tabs unchanged), the focus in `DashboardList` (2.7), no row selected, its rows as K-WEBf (notes still `This repository has no remotes`: a read is no run, 4.2). Whether the activation reads Web's rows again is 2.10's 10 s rule on the session's real clock, so the check counts no reads here: it waits until none reads and asserts that no write ran since step 9.<br>**After step 11:** tabs [`Web`, `Tools`] as after step 7, Web's rows as scenario facts 2.3 after its first read, no row selected, Tools not read.<br>**After step 12:** F5 reads every row (2.10's trigger 3): since the press, for each of the nine rows with a folder its refs and status reads (`CallsSince(mark, worktree)`), none for old-site's folder (its check needs no git), and no write; the pass line `Web: read 10 repositories in <N> ms` (decision KD10); the rows as scenario facts 2.3.<br>**After step 13:** F6 puts the focus in the graph area's region (`FocusedRegion()` GraphArea, the only region a dashboard shows with the log closed), within `DashboardList`; whether on the list itself or on its first row (6.6: an items control gives its selected or first focusable item) is not asserted, nor what a Down would select from there. Home selects api (row 0); the two Downs select web-app, then docs (row 2, on `VcSelectionBrush`), the keyboard focus within `DashboardList`; the activity log toggle enabled for the selected row, `Show every git command the app has run (Ctrl+J)`.<br>**After step 14:** Ctrl+B and Ctrl+Z are consumed (6.9: on a dashboard every other key of the set does nothing, as on an empty tab; Branch is disabled without a reason, and a disabled Undo takes its key, D163): no dialog (`Dialogs.IsOpen` false, no `NameDialog`), no operation, no git call since the step began; docs still selected with the focus within `DashboardList`, the rows unchanged. Ctrl+J shows the selected row's activity (6.9, 2.9): the activity log under the list (the list y 152 to 469, the splitter y 469 to 474, the panel y 474 to 674), its header `Activity log`, `docs` (secondary), `Show reads` unchecked, no operation row (docs ran none, and its reads are hidden while `Show reads` is unchecked); the toggle `Hide the activity log (Ctrl+J)`. Ctrl+J again: the panel hidden, the list back to y 152 to 674, the toggle `Show every git command the app has run (Ctrl+J)`, docs still selected with the focus within the list.<br>**After step 15:** Alt+Down (2.7, 1.10): docs is row 3 and mobile row 2; docs still selected with the focus (decision KD11); `workspaces.json`'s "Web" lists api, web-app, mobile, docs, design-system, billing, infra, scratch, notes, old-site (each `{root}\<name>`); Tools unchanged; no git call.<br>**After step 16:** 2.3: docs opens in a new tab at 1 + max(d − 1, none) = 2: tabs [`Web`, `Tools`, `docs`], docs active, K-DOCS, its tab adopting the row's summary-mode view model (3.3); Web's docs row follows the tab (3.5); `session.json`'s tabs the two dashboards and docs' folder, active 2.<br>**After step 17:** Mod+1, the first tab: `Web` active (the dashboards first, D187), the focus in `DashboardList` (it takes the focus when the dashboard becomes active, 2.7), docs still selected at row 3 (the selection is kept in the background). Its other rows are read again only if 10 s or more passed since their last read (2.10's rule on the real clock; docs' row follows its tab, 3.5): the check waits until none reads and counts no reads.<br>**After step 18:** End selects old-site (row 9); Delete removes it at once, with no question (1.10): K-WEBm, the header `9 repositories`, no row selected, the focus still on `DashboardList`; `workspaces.json` as K-WEBm, Tools unchanged; nothing on disk changed; no git call since End. | | | `k13a-next-conflict-tooltip`, `k13b-next-dashboard` | Design 6.3, 6.5, 6.8, 6.9, 2.3, 2.5, 2.7, 2.9, 2.10, 3.3, 3.4, 4.9, 4.10, 1.10; phase 4 design 11.10; D199, D204, D205, D207, D208, D213. n/a: the real window's key path is K10's and its dashboard is part W's (W1, W2, W4, W9); the resolver's own keys are phase 4's real-window C10. Methods 2 and 3 are 17.4's dashboard method split for the hang watchdog (decision KD5): about 150 git processes each (73 at the start; method 2 eight fetches and their refreshes, about 64, and Tools' read, 14; method 3 F5's 63 and docs' history), each plus up to about 60 when an activation finds the rows 10 s old. Fetch all's rows are not pictured here (W4's `w4b-fetched` shows them), and the row moves by Alt+Down, since W2 pictures Alt+Up (`w2g-moved-up`). No picture varies by nature (the resolver's caret is held still, D151). |
| S14 | The settings screen (design 7.1 to 7.7, 6.11, D197, D200, D216): the toolbar's `Settings` button at the start of the right group; the screen opened by the button, by Ctrl+, and by the palette's `Open settings…`, `Show keyboard shortcuts…` and `Choose an editor…`; its card, section list, first focus, keys and buttons; General, Tools (the editor's Pick dialog, Other… and a validation state), Git (opening on `All repositories (global)` on a repository's tab, `This repository` one click away, its edits kept while switching, disabled with its reason on an empty tab) and Keyboard shortcuts (6.11's list); Cancel and Esc change nothing; General and Git at 1920×1080; "settings" (with an empty second tab), dark, 1100×700 and 1920×1080 | **Part A** (`Check_S14_settings_screen_sections_and_entries`): 1. "Settings" scenario; before the app starts seed the session's global file with `[user]` / `⇥useConfigOnly = true` (SD3) and write a session of two tabs, tab 0 the copy (active) and tab 1 empty. Start at 1100×700, dark; wait for ST0. 2. Click `SettingsButton`; wait until the screen shows and its reads have ended. Screenshot (`s14a-general`). 3. Press Down twice, then Enter. Screenshot (`s14b-tools`). 4. Click `Git` (`SettingsNavGit`). Screenshot (`s14c-git-global`). 5. Click `This repository` (`GitScopeRepository`). Screenshot (`s14d-git-repository`). 6. Type `Local Name` into `UserNameBox`; click `All repositories (global)` (`GitScopeGlobal`); type `Global Name` into `UserNameBox`; click `This repository`; click `All repositories (global)`. 7. Click `Keyboard shortcuts` (`SettingsNavShortcuts`). Screenshot (`s14e-shortcuts-top`). Scroll the section (`SettingsSection`) to its end. Screenshot (`s14f-shortcuts-end`). 8. Click `Tools` (`SettingsNavTools`); click `Choose…` (`EditorChooseButton`); click the item `Other…`, then `Choose`. 9. Queue the fake folder picker's file answer `C:\Tools\Missing\Ed.exe` (`/opt/tools/missing/ed` off Windows; SD16) and click `Browse…` (`EditorBrowseButton`). Click `Save`; wait for its validation. Screenshot (`s14g-tools-other-error`). 10. Click `Cancel`. 11. Press Ctrl+,. Press Esc. 12. Press Ctrl+Shift+P, type `settings`, press Enter; press Esc. Press Ctrl+Shift+P, type `keyboard shortcuts`, press Enter; press Esc. Press Ctrl+Shift+P, type `editor`, press Enter; press Esc. 13. Click tab 1 (the empty tab). Click `SettingsButton`; click `Git`. Press Esc.<br>**Part B** (`Check_S14_settings_screen_at_1920x1080`, a new copy and data folder, a new start): 14. "Settings" scenario, the same seed, one tab. Start at 1920×1080, dark; wait for ST0. Press Ctrl+,; wait for the reads. Screenshot (`s14h-general-1920`). 15. Click `Git`. Screenshot (`s14i-git-global-1920`). 16. Click `Keyboard shortcuts`. Press Esc. | **After step 1:** ST0; the tab strip `settings` (active) and `New tab`. The toolbar's right group, left to right in one row: `Settings` (`SettingsButton`, a `ToolbarButton` with `IconSettings` and the label `Settings`), `Search`, `Theme`; `Settings` directly left of `Search`; `Theme`'s right edge within 20 of the window's (phase 0's assertion, `Phase0RealWindowPass.cs:141`, holds); the left group (Undo to Pop) where phases 3 to 5 have it. `SettingsButton` enabled (`IsEffectivelyEnabled`), its tooltip and help text `Open settings (Ctrl+,)`, its `AcceleratorKey` `Ctrl+,` (6.8, D197).<br>**After step 2:** no question; the screen `SettingsDialog` (named `Settings`) on the dialog layer, the window behind under `VcBackdropBrush`. The card (`Border.dialogCard.settings`: `VcPanelBackgroundBrush`, a 1-px `VcBorderBrush` border) exactly at x 120, y 40, 860 × 620. Inside, 20 in: the title `Settings` (`SettingsTitle`, 16 SemiBold) from about (140, 60); 14 below it the body, about 493 high: `SettingsNav` at x 140 to 316, four buttons 176 × 30, 2 apart, from y about 95, their text left-aligned 10 in: `General` (the `selected` look), `Git`, `Tools`, `Keyboard shortcuts`; the section (`SettingsSection`) from x 336, 622 wide; the buttons `Cancel` (`CancelButton`) and `Save` (`ConfirmButton`, `VcPrimaryButton`) right-aligned, 8 apart, on y 610 to 640, `Save` ending at about x 958; no `SettingsSaving` text; no `SettingsNavError`. The keyboard focus on `SettingsNavGeneral`, no text box focused (7.1's `FocusesTextBoxOnOpening` false). **General** (7.3; labels in the 132-wide column x 336 to 468, `VcTextSecondaryBrush`, top-aligned with their control's first line; controls from x 480; each explanation a second line, secondary, 4 below; rows 18 apart): `Theme`: the radios `Dark` (`SettingsThemeDark`, checked), `Light`, `System` in a row, 20 apart, and under them `System follows your operating system's light or dark mode.`; `Fetching`: `AutoFetchCheckBox` `Fetch in the background every` (checked), `AutoFetchMinutesBox` `5` (48 wide, centred, enabled) and `minutes` on one line, then `Only the active tab's repository; it never asks for a password.`; `Working tree`: `KeepIgnoredCheckBox` `Keep ignored files` (checked) with 7.3's line, and 10 below `StashUntrackedCheckBox` `Include untracked files when stashing` (checked) with `The Stash changes dialog starts with this; its own check box changes it too.`; `Opening files`: `RunnableIntro` `Open file asks first before it opens a file with one of these extensions:`, `RunnableList` (code font, wrapping, read-only) `.exe .bat .cmd .com .ps1 .vbs .vbe .js .jse .wsf .hta .msi .scr .pif .cpl .lnk .url .reg .jar .app .command .sh .desktop`, `RunnablePathExt` `From PATHEXT: .wsh .msc` (Windows; not shown elsewhere), `Also ask for` with `RunnableExtraBox` (320 wide, empty, placeholder `.py .pl`), no `RunnableError`, `RunnableUnix` `On Linux and macOS it also asks before it opens a file marked as a program.`. No error, no warning; the section fits without a scroll bar; no text cut (`AssertNoClippedText`). Calls since the click, the screen's reads (SD29), seven: the outside read (in `{data}`) and the repository read (in `{root}\settings`), each exit 0; `git var GIT_AUTHOR_IDENT` and `git var GIT_COMMITTER_IDENT` in `{root}\settings`, each exit 128 (the identity reads above); Pull's reader in `{root}\settings`, in this order `git config --get pull.ff`, `git config --get branch.main.rebase`, `git config --get pull.rebase`, each exit 1 (nothing set: `PullInEffect`'s "nothing is set" line, after step 5); no write; no `git mergetool`.<br>**After step 3:** the first Down moved the focus to `SettingsNavGit` and the second to `SettingsNavTools` with General still shown; Enter showed **Tools** (`SettingsNavTools` selected), the focus staying on its button (7.1), and saved nothing: the screen still open, `SettingsSaving` empty, `settings.json` does not exist, no write and no read back (no call since step 2's reads; 7.8's "Enter saves" is not a nav button's Enter, which 7.1 gives to the section). Tools (7.5): label `Editor`; `EditorValue` `None`; `EditorProgram` not shown; `EditorChooseButton` `Choose…` enabled; `EditorNote` `Open in editor appears in the file menus, beside Open file, once an editor is chosen.` (secondary); no `Program` or `Arguments` row; no `EditorError`; `EditorStatus` (`Looking for editors…`) hidden, the fixed catalogue having answered at once.<br>**After step 4:** **Git**, on `All repositories (global)` though a repository's tab is active (D200): `GitScopeGlobal` (28 high) in the `selected` look, `GitScopeRepository` enabled, its tooltip `settings — ` followed by the copy's working tree folder (`{root}\settings`); `GitScopeCaption` (secondary, 6 below) `Saved in your global git configuration, which git in a terminal uses too.`, its tooltip and help text the global file's full path, `{data}\harness\global.gitconfig`; 14 below, `Name` with `UserNameBox` and `Email` with `UserEmailBox`, both empty with the placeholder `Not set`, no origin line, no error or warning; no `IdentityInEffect` (repository scope only, 8.6); 18 below, `Pull` with the six radios one per line, 8 apart, `Not set` (`PullNotSet`, checked; tooltip `Nothing set: the app merges, and git in a terminal stops when the branches have diverged`), `Merge (fast-forward when possible)`, `Merge, always with a merge commit`, `Rebase`, `Rebase, keeping merge commits`, `Fast-forward only`; no `PullOther`, no `PullNotices`; 18 below, `Merge tool` with `MergeToolValue` `Not set` and `MergeToolChooseButton` `Choose…` enabled (the fixed list is there at once, D202, 12.9), `MergeToolNote` `Used by Merge tool… in the conflict resolver, and by git mergetool in a terminal.`; no `MergeToolStatus`; no `GitReading`; the section fits (about 367).<br>**After step 5:** `This repository` selected; the caption `Saved in this repository's .git/config, which all its worktrees share. These values win over those for all repositories.`; `UserNameBox` and `UserEmailBox` empty with the placeholder `Not set` (the global scope has none, 8.6); `IdentityInEffect` `Git cannot make commits here: no email is set.` (secondary; git's last line `no email was given and auto-detection is disabled`, 8.6); `IdentityNotices` one line, `user.useConfigOnly is set, so git does not guess a missing name or email.`; `Same as global` (`PullNotSet`, checked; tooltip `Uses the pull mode for all repositories`) and the five other radios; `PullInEffect` `Pull into main: Merge (fast-forward when possible): nothing is set, so the app merges; git in a terminal stops when the branches have diverged.`; no `BranchRebaseNotice`; `MergeToolValue` `Same as global (not set)`; the section fits (about 424). No call since step 2's reads: a scope switch reads nothing, `PullInEffect` coming from Pull's reader, which ran at the opening (SD29).<br>**After step 6:** each scope kept its own edit (D200): back in `This repository` `UserNameBox` holds `Local Name`, back in `All repositories (global)` `Global Name`; no error; nothing written.<br>**After step 7:** **Keyboard shortcuts** (7.6): `ShortcutsIntro` `These keys are fixed.` (secondary), then `ShortcutList`'s eleven groups in 6.11's order, each heading (`ShortcutGroup`, 12.5 SemiBold, 12 above) followed by its rows, with exactly the 50 rows and keys of "Texts these checks rely on (part S)" (Windows' and the headless platform's Ctrl texts); each `ShortcutRow` at least 22 high, its action at the left and its keys at the right in the code font and `VcTextSecondaryBrush`, inside the row, apart and not cut; automation name `{label}: {key text}` (`Command palette: Ctrl+Shift+P`); `Extend the graph's selection`'s keys exactly `Shift+Up, Shift+Down, Shift+PageUp, Shift+PageDown, Shift+Home, Shift+End` on two lines, its row taller than 22, every other row one line; the Repository group's note `On a dashboard, Fetch and Pull fetch and pull every repository of its workspace.` under its rows; no Moving around note (it is macOS's); the section scrolls (its extent above its viewport). First picture: the top of the list; second: its end, the last group `Dialogs and menus` with its three rows wholly in view.<br>**After step 8:** the Pick dialog `Choose an editor` (`PickDialog`) stacked over the screen (D104; the settings card still drawn behind a second backdrop) on the wide card (460): `PickTitle` `Choose an editor`; `PickFilter` focused and empty; the options (text — detail) `None` — `no Open in editor in the file menus` (selected, SD6), `Visual Studio Code` — `C:\Tools\Microsoft VS Code\Code.exe`, `Sublime Text` — `C:\Tools\Sublime Text\subl.exe`, `IntelliJ IDEA` — `C:\Tools\JetBrains\IntelliJ IDEA 2026.1\bin\idea64.exe`, `Other…` — `a program of your choice` (off Windows the details `/opt/tools/vscode/code`, `/opt/tools/sublime/subl`, `/opt/tools/idea/bin/idea`), each row 28 high, its text whole, its detail 12 to the right in `VcTextSecondaryBrush` at 11.5, cut at its start when it does not fit (7.7); buttons `Cancel`, `Choose`. After `Other…` and `Choose`: the Pick closed; Tools: `EditorValue` `Other`; the rows `Program` (`EditorProgramBox`, 320 wide, empty, with the keyboard focus) with `Browse…`, and `Arguments` (`EditorArgumentsBox` `{path}`), then `EditorArgumentsHint` `{path} is the file's full path, {line} its line (1 for now). Without {path}, the path comes last.`; no `EditorError` yet (nothing edited, D114).<br>**After step 9:** the fake folder picker was asked once for a file, with the title `Choose the editor's program` (12.5); `EditorProgramBox` holds `C:\Tools\Missing\Ed.exe`. Save's validation (the disk check off the UI thread, 7.8 step 1): `EditorError` `There is no file at this path.` (`dialogError`, `VcDangerBrush`) under the Arguments rows; `SettingsNavError` (6 × 6, `VcDangerBrush`) at the right of `Tools`, its help text `Has an error`; no dot on the other three; Tools shown; the focus in `EditorProgramBox`; the screen open; nothing saved: `settings.json` does not exist, no write, the global file and `.git/config` unchanged.<br>**After step 10:** the screen closed at once, without a question; the focus back on `SettingsButton`; `settings.json` does not exist; the global file byte-identical to the seed and `.git/config` to the copy's; no write since step 1 (D200's typed names and Tools' Other dropped).<br>**After step 11:** Ctrl+, opened a new screen on General, `SettingsNavGeneral` selected and focused, as after step 2 (no dot; Tools back to `None`); Esc closed it; nothing written.<br>**After step 12:** for `settings` the palette's first row (highlighted) is `Open settings…` with its key `Ctrl+,` (`CommandPaletteItemKey`); Enter closed the palette, then opened the screen on General. For `keyboard shortcuts`, `Show keyboard shortcuts…` (no key, D199): the screen on Keyboard shortcuts (`SettingsNavShortcuts` selected and focused). For `editor`, `Choose an editor…` is listed (no editor is chosen, 12.1): the screen on Tools. Each Esc closed the screen; nothing written; each run logged `settings: Command palette ran: {label}`.<br>**After step 13:** tab 1 active (the welcome page); `SettingsButton` enabled there, with the same tooltip; the screen on General; Git on `All repositories (global)` as after step 4 but `This repository` disabled, its tooltip `Open a repository's tab to change its own settings` (D90); the screen's reads were the outside read alone (no read in a working tree, no `git var`, no Pull's reader: one process, SD29). Esc closed it; nothing written.<br>**After step 14:** the card exactly at x 530, y 200, 860 × 680; the title from about (550, 220); `SettingsNav` at x 550 to 726; the section from x 746, 622 wide, its lines wrapping as at 1100×700 (the card does not grow, 7.1); the buttons on y 830 to 860; the body about 553 high; General as after step 2.<br>**After step 15:** Git on `All repositories (global)` (D200) as after step 4, at these coordinates.<br>**After step 16:** Keyboard shortcuts as after step 7 (the same rows; `Extend the graph's selection`'s keys on two lines); Esc closed the screen; nothing written: `settings.json` does not exist, the global file is the seed. | | | `s14a-general`, `s14b-tools`, `s14c-git-global`, `s14d-git-repository`, `s14e-shortcuts-top`, `s14f-shortcuts-end`, `s14g-tools-other-error`, `s14h-general-1920`, `s14i-git-global-1920`; real window: `s14a-general` | RW: part A's steps 1 and 2 in the real window on the same session and seed: `SettingsButton`'s help text `Open settings (Ctrl+,)` and `AcceleratorKey` `Ctrl+,` read through UI Automation, a real click on it, the capture `s14a-general` against its scripted twin; then in the same launch `Tools`, `Choose…` (the Pick dialog's `PickItem` names and help texts as step 8 gives them), `Other…`, `Choose`, `Browse…`: Windows' Open dialog found by its title `Choose the editor's program`, the system folder's `notepad.exe` (`Environment.SpecialFolder.System` + `notepad.exe`) typed into its file name box (automation id `1148`) and confirmed by its button `1` (`ChooseFileInDialog`, 17.2); `EditorProgramBox`'s Value that path, both sides through `Path.GetFullPath` and compared ignoring case (`StringComparison.OrdinalIgnoreCase`, Windows only: `GetFolderPath(System)` can give `C:\WINDOWS\system32` while the dialog hands back the file system's `C:\Windows\System32\notepad.exe`); `Cancel`: `settings.json` does not exist and the global file is the seed; then a real Ctrl+, opening the screen on General, and Esc. What only the real window proves: the native file dialog through `StorageFolderPicker.PickFileAsync` and `TryGetLocalPath` (12.10), the card at 200 % scaling, the real key. The native dialog is not pictured (it shows the machine's folders); the light twin of the screen is L34's `l31d-settings-light`. Part B is scripted only (1920×1080 does not fit the screen). The global caption's tooltip and `This repository`'s name the temporary folders: asserted, not pictured. `s14g-tools-other-error` varies by nature: the keyboard focus is in `EditorProgramBox`, whose caret may be caught on or off (`ShellDriver` holds only the resolver's caret still, D151); only the asserted state counts. Design 7.1 to 7.7, 6.11, 12.5, 12.9, 14.8; D197, D200, D216; decisions SD1, SD2, SD3, SD6, SD9, SD16, SD17, SD19, SD21, SD26, SD28, SD29. |
| S15 | Following the system's theme (D194, design 11.1 to 11.7): a start with `System` in `settings.json` and the system dark; the system turning light without a restart; a click on Theme leaving System; two flips of the system that then change nothing; the palette's `Follow the system's theme` (and the chosen theme left out of the palette); System chosen in the settings screen without a live preview; a restart whose first frame already has the system's theme; "graph", 1100×700 | **Part A** (`Check_S15_system_theme_followed`): 1. "Graph" scenario; before the app starts write `settings.json` `{ "theme": "System" }`. Start with the fake system dark; wait for GR0. Screenshot (`s15a-system-start-dark`). 2. Turn the fake system light (`SetSystemThemeAsync(false)`). Screenshot (`s15b-system-light`). 3. Click `ThemeSwitch`. Screenshot (`s15c-theme-click-dark`). 4. Flip the fake itself (SD11), not through `SetSystemThemeAsync`, which waits for the window to show the variant asked for and a Dark setting never shows light: `app.SystemTheme.Set(true)` on the UI thread; wait for the log line `The system's theme changed to dark; the app keeps the dark theme.` and the layout's settle; capture the window (not saved). `app.SystemTheme.Set(false)`; wait for `The system's theme changed to light; the app keeps the dark theme.` and the settle; capture the window (not saved). 5. Press Ctrl+Shift+P and type `theme`. Select the box's text and type `follow the system`; press Enter.<br>**Part B** (`Check_S15_system_theme_after_a_restart`, a plain `[Fact]`, two app starts on one data folder): First start: 6. "Graph" scenario, a new copy, no `settings.json`; start with the fake system dark; wait for GR0. Press Ctrl+,. Click `Light` (`SettingsThemeLight`); click `System` (`SettingsThemeSystem`). Click `Save`. Close the app. Second start: 7. Start again on the same data folder with the fake system light (`systemDark: false`). At once, before any wait, capture the window. Screenshot of that capture (`s15d-restarted-first-frame-light`). 8. Wait for GR0. Screenshot (`s15e-restarted-light`). | **After step 1:** the window dark: `Application.RequestedThemeVariant` Dark, the window's `ActualThemeVariant` Dark; the samples (480, 600) `#14161B`, (130, 600) `#1A1D24`, (1070, 18) `#20242C`; GR0 in the dark lane colours; `ThemeSwitch` without the `light` class (`IconSun`, the theme it switches to), its tooltip and help text `Switch to the light theme (stops following the system)`; `IsDarkTheme` true, `Theme` System; `settings.json` exactly as written (the app saved nothing); the app log has `Theme: System (the system's theme is dark).` once.<br>**After step 2:** no restart, no dialog: the window light: `RequestedThemeVariant` Light; the samples `#FFFFFF`, `#F5F6F8`, `#EBEDF1`; every view light: GR0's lanes in the light colours (lane colour 0 `#4353D8`, 1 `#1C8655`, 2 `#A16A1B`, 3 `#C93A46`, 4 `#1D7EA6`), row 0's stash ring and its dashed line light, the left panel, the details panel, the toolbar, the tab strip and the status bar in the light tokens; `ThemeSwitch` with the `light` class (`IconMoon`), tooltip and help text `Switch to the dark theme (stops following the system)`; `settings.json` still exactly as written (`"System"`); the log `The system's theme changed to light; the app follows it.`<br>**After step 3:** the window dark (the samples as after step 1); `ThemeSwitch` `IconSun`, tooltip and help text `Switch to the light theme` (System left, 11.6); `settings.json` `"theme": "Dark"`; the log `Theme changed to Dark.`<br>**After step 4:** each flip logged its line (the wait's condition); both captures byte-identical to step 3's (`s15c`); `RequestedThemeVariant` Dark throughout, `IsDarkTheme` true; `settings.json` still `"Dark"`; the log `The system's theme changed to dark; the app keeps the dark theme.`, then `The system's theme changed to light; the app keeps the dark theme.`, and no `Theme changed` line.<br>**After step 5:** for `theme` the palette lists `Use the light theme` and `Follow the system's theme` (no detail, no key) and not `Use the dark theme` (the theme chosen now, 11.7); for `follow the system` one row, `Follow the system's theme`, highlighted. Enter: the palette closed; the window light (the fake system is light): the samples light, `ThemeSwitch` `IconMoon` with `Switch to the dark theme (stops following the system)`; `settings.json` `"theme": "System"`; the log `Theme changed to System.` and `graph: Command palette ran: Follow the system's theme`.<br>**After step 6:** the screen on General with `Dark` checked; after a click on `Light` the window stays dark (`RequestedThemeVariant` Dark, no live preview, 7.8; `ThemeSwitch`'s tooltip still `Switch to the light theme`); after `System` and Save the screen closed, the window dark (the fake system is dark), `ThemeSwitch` `IconSun` with `Switch to the light theme (stops following the system)`; `settings.json` `"theme": "System"`, the rest at the defaults; the log `Theme: Dark.` at the start, then `Theme changed to System.`; no git write.<br>**After step 7:** right after the start, before any wait: `RequestedThemeVariant` Light and the window's `ActualThemeVariant` Light; `IsDarkTheme` false; the samples `#FFFFFF`, `#F5F6F8`, `#EBEDF1` (11.5: the first frame has the resolved variant). What else the frame shows (the tab's loading state or the graph, the status bar's `Looking for Git…` or `Ready`) depends on how far start-up has come.<br>**After step 8:** GR0 drawn light, as after step 2; `ThemeSwitch` `IconMoon`, tooltip and help text `Switch to the dark theme (stops following the system)`; `settings.json` `"theme": "System"`; the log's second start has `Theme: System (the system's theme is light).` | | | `s15a-system-start-dark`, `s15b-system-light`, `s15c-theme-click-dark`, `s15d-restarted-first-frame-light`, `s15e-restarted-light`; real window: `s15-system-start-with-frame`, `s15-system-start`, `s15-theme-click` | RW (11.8, D224): a launch of its own at 1100×700. Before launching, read-only: `RealApp.SystemUsesLightTheme()` (`AppsUseLightTheme` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`: `1` or missing light, `0` dark) and high contrast (`SystemParametersInfo(SPI_GETHIGHCONTRAST)`, `HCF_HIGHCONTRASTON`). With high contrast on, `run.txt` gets `S15 (System at start): failed. High contrast is on, and Avalonia follows its scheme: turn it off and run the pass again.` and the method fails (the gate is then Blocked until the owner turns it off). Otherwise a new data folder with `"theme": "System"` and one tab on "graph"; `RealApp.Launch`; wait for the graph; the chrome, panel and window colours of the variant read at the step 1 spots; `ThemeSwitch`'s help text `Switch to the dark theme (stops following the system)` (light) or `Switch to the light theme (stops following the system)` (dark); the frame capture `s15-system-start-with-frame` (its title bar follows the variant on build 22000 and later, inspected) and the client capture `s15-system-start` against `s15e-restarted-light` (light: the owner's machine today) or `s15a-system-start-dark` (dark); then a real click on `ThemeSwitch`: the other variant shows, `settings.json` says `"Dark"` (from light) or `"Light"` (from dark), the pointer to the rest point, `s15-theme-click` against `s15c-theme-click-dark` (from light) or `s15b-system-light` (from dark). What only the real window proves: the platform's own reading of the desktop's mode at a real start and the title bar. The pass never changes the desktop's mode; following a change live is proven here with the fake, and by hand at acceptance (the report's checklist line of 11.8). `s15d` varies by nature: how far start-up has come at the first frame (phase 0's `06c`); its assertions are the variant and the three samples only. Step 4's captures are compared in memory (SD11). Design 11.1 to 11.7, 14.11, 14.14; D194, D223, D224; decisions SD11, SD12, SD24. |
| S16 | Auto-fetch's minutes and "Include untracked files when stashing" (design 7.3, 7.8, 7.9): a file's 120 shown and kept without an error until the box is edited; 61 refused at Save with the section marked; 15 saved by Enter in the box; off saved as 0 and shown again unchecked at 5; the stash dialog's box following the setting both ways; the environment's override logged; "settings" with a change, dark, 1100×700 | One app start (`Check_S16_auto_fetch_and_stash_settings`): 1. "Settings" scenario; before the app starts write `notes.txt` `one` `two` `three` (not staged), `draft.txt` `draft` (untracked) and `settings.json` `{ "autoFetchMinutes": 120, "stashIncludeUntracked": false }` (SD14). Start; wait for ST0w. Click `SettingsButton`. Screenshot (`s16a-general-from-file`). 2. Click `Save`. 3. Press Ctrl+,. Select the text of `AutoFetchMinutesBox` and type `61`. Click `Save`. Screenshot (`s16b-minutes-error`). 4. Select the box's text and type `15`. Click `StashUntrackedCheckBox`. Click into `AutoFetchMinutesBox` and press Enter. 5. Click the toolbar's `StashButton`. Screenshot (`s16c-stash-dialog-untracked`). Press Esc. 6. Press Ctrl+,. Click `AutoFetchCheckBox`; click `StashUntrackedCheckBox`. Click `Save`. 7. Press Ctrl+,. Screenshot (`s16d-auto-fetch-off`). Press Esc. 8. Click `StashButton`. Press Esc. | **After step 1:** the app log has `VISUALCOMMIT_AUTO_FETCH_MINUTES=0 overrides the auto-fetch setting (120 minutes).` once, after the settings were loaded (the harness's own `0`, D112, 7.3). The screen on General as S14 step 2's but: `AutoFetchCheckBox` checked with `AutoFetchMinutesBox` `120`, enabled, and no `AutoFetchError` (a value above 60 from the file shows no error until the box is edited, 7.3); `StashUntrackedCheckBox` unchecked. `Theme` `Dark`, `KeepIgnoredCheckBox` checked.<br>**After step 2:** the screen closed; nothing changed, so no `ISettingsStore.Update`: `settings.json`'s bytes exactly as written before the start (7.8 step 2).<br>**After step 3:** `AutoFetchError` `Enter a whole number of minutes from 1 to 60.` (`VcDangerBrush`) under the Fetching row as soon as `61` is typed (the box edited, D114). Save refused: the screen open on General; `SettingsNavError` at the right of `General`, help text `Has an error`; the keyboard focus in `AutoFetchMinutesBox`; `settings.json`'s bytes unchanged.<br>**After step 4:** with `15` the error and the dot gone (SD9); Enter in the box saved (7.8): the screen closed; `settings.json` `autoFetchMinutes` 15 and `stashIncludeUntracked` true, the others at their defaults (one `Update` with both changes).<br>**After step 5:** `StashButton` enabled (tooltip `Stash your changes (Ctrl+Shift+S)`); the stash dialog `Stash changes` (phase 3's): `MessageBox` empty with the placeholder `Message (optional)`, `NameDialogOption` `Include untracked files` **checked** (the setting, read when the dialog opens, 7.9), `Cancel`, `Stash`. Esc: no `git stash` call, `git stash list` empty, `settings.json` unchanged.<br>**After step 6:** on opening `AutoFetchCheckBox` checked with `15`, `StashUntrackedCheckBox` checked; once unchecked, `AutoFetchMinutesBox` is disabled and keeps `15`; Save: `settings.json` `autoFetchMinutes` 0 and `stashIncludeUntracked` false.<br>**After step 7:** `AutoFetchCheckBox` unchecked and `AutoFetchMinutesBox` disabled showing `5` (0 or less shows the box unchecked at 5, 7.3); `StashUntrackedCheckBox` unchecked. Esc: nothing written.<br>**After step 8:** the stash dialog's `Include untracked files` **unchecked**; Esc: nothing stashed. No git write in the whole check. | | | `s16a-general-from-file`, `s16b-minutes-error`, `s16c-stash-dialog-untracked`, `s16d-auto-fetch-off` | n/a: the screen is pictured in the real window by S14 and L34, the stash dialog is phase 3's; auto-fetch's tick (7.9) shows in neither harness, both forcing `VISUALCOMMIT_AUTO_FETCH_MINUTES=0` (D112), so the interval's effect is `AutoFetchSchedulerTests`' (17.5). `s16b-minutes-error` (the keyboard focus in `AutoFetchMinutesBox`) and `s16c-stash-dialog-untracked` (in the stash dialog's `MessageBox`, the first text box, focused on opening) vary by nature: the caret may be caught on or off (`ShellDriver` holds only the resolver's caret still, D151); only the asserted state counts. Design 7.3, 7.8, 7.9, 14.8, 14.14; decisions SD9, SD14, SD29. |
| S17 | Open file's extensions and Open in editor (design 7.3, 7.5, 7.7, 12.1 to 12.9, D226): `run.py` opens at once with 12.9's `PATHEXT`; with `.py` added in General, Open file asks first; Visual Studio Code chosen from the fixed catalogue in the Pick dialog stacked over Tools; `Open in editor` right after `Open file` in a commit file's, an unstaged file's, a staged file's and the diff's menus; its click records 12.9's program and the file's absolute path and asks nothing; the palette's `Choose an editor…` gone once an editor is chosen; "settings" with a staged and an untracked change, dark, 1100×700 | One app start (`Check_S17_open_file_extensions_and_editor`): 1. "Settings" scenario; before the app starts write `notes.txt` `one` `two` `three` and stage it (`git add -- notes.txt`), and write `draft.txt` `draft` (untracked). Start; wait for ST0w. 2. Click the row "Initial commit" (commit row 2, display row 3, (468, 207)). Right-click `run.py` in the details panel's file list (`FileList`). Click `Open file`. 3. Click `SettingsButton`. Type `.py` into `RunnableExtraBox`. Click `Save`. 4. Right-click `run.py`; click `Open file`. Screenshot (`s17a-open-file-asks`). Click `Cancel`. 5. Press Ctrl+Shift+P, type `editor`; press Esc. Click `SettingsButton`; click `Tools`; click `Choose…`. Screenshot (`s17b-editor-pick`). Click `Visual Studio Code`, then `Choose`. Screenshot (`s17c-tools-editor-chosen`). Click `Save`. 6. Press Ctrl+Shift+P, type `editor`; press Esc. 7. Right-click `run.py`. Screenshot (`s17d-file-menu-with-editor`). Click `Open in editor`. 8. Click the working-changes row (display row 0, (468, 129)). Right-click `draft.txt` in the unstaged list; press Esc. Right-click `notes.txt` in the staged list; press Esc. Click `notes.txt` in the staged list; right-click the diff's text (`DiffText`); press Esc. | **After step 1:** ST0w; `git status --porcelain=v1` prints `M  notes.txt` and `?? draft.txt`.<br>**After step 2:** Initial commit selected; its details' `Changed files (3)`: A `README.md`, A `notes.txt`, A `run.py`. The menu: header `run.py`; `File history`; `Blame`; —; `Open file`; `Show in folder`; `Copy path`; no `Open in editor` (no editor chosen, 12.1); 171 high (8 + 24 + 26 × 5 + 9, ±2). `Open file`: no question (`.py` is neither in the fixed list nor in the fixed `PATHEXT`, 12.8); the fake launcher recorded one open of `{root}\settings\run.py` (absolute; `/` off Windows).<br>**After step 3:** General as S14 step 2's with `RunnableExtraBox` `.py` and no `RunnableError` (a valid entry); Save: the screen closed; `settings.json` `extraRunnableExtensions` `.py`, the rest at the defaults; no git write (no `git config` with `--replace-all` or `--unset-all`): the only git calls are the screen's reads at its opening and again in the read back (SD29), seven each.<br>**After step 4:** the question (`OperationTexts.ConfirmOpenRunnable`, phase 3): title `Open run.py?`, text `This file can run programs. Open it anyway?`, buttons `Cancel` and `Open` (not destructive). After Cancel: closed; the launcher still holds only step 2's record (12.8's union: the user's `.py`).<br>**After step 5:** with no editor chosen the palette lists `Choose an editor…` (no key). The Pick dialog `Choose an editor` stacked over the screen, as S14 step 8 gives it (`None` selected). After `Visual Studio Code` and `Choose`: Tools shows `EditorValue` `Visual Studio Code` (no ` (git's editor)`: no `core.editor` is set, 12.3), `EditorProgram` `C:\Tools\Microsoft VS Code\Code.exe` (secondary, cut at its start only if it does not fit, its tooltip the whole path; `/opt/tools/vscode/code` off Windows), `Choose…`, `EditorNote`; no `Program` or `Arguments` row; no error. Save: closed; `settings.json` `editor` with `id` `vscode` and `program` `C:\Tools\Microsoft VS Code\Code.exe` (`/opt/tools/vscode/code` off Windows) (`arguments` null: a known editor's come from the catalogue, 12.6), `extraRunnableExtensions` still `.py`.<br>**After step 6:** the palette lists no `Choose an editor…` (only while none is chosen, 12.1) and no `Open in editor` (5.5).<br>**After step 7:** the menu: header `run.py`; `File history`; `Blame`; —; `Open file`; `Open in editor`; `Show in folder`; `Copy path`; 197 high (one item more than step 2's, ±2); `Open in editor` enabled without a reason. Its click: no question (12.1: never asking, though Open file asks for `.py` now); the fake launcher recorded an `Edit` with program `C:\Tools\Microsoft VS Code\Code.exe`, arguments exactly [`{root}\settings\run.py`] (vscode's `{p}`, 12.2), working directory `C:\Tools\Microsoft VS Code` (the program's folder, 12.4) and `ELECTRON_RUN_AS_NODE` among its removed variables; off Windows program `/opt/tools/vscode/code`, arguments [`{root}/settings/run.py`], working directory the user's home folder. No operation (no queue record, Undo unchanged); the app log has one `Open in editor: ` line naming that program.<br>**After step 8:** the stage panel: `draft.txt` unstaged (untracked), `notes.txt` staged. The unstaged file's menu, the staged file's menu and the staged diff's menu each end with exactly the group `Open file`, `Open in editor`, `Show in folder`, `Copy path`, `Open in editor` enabled; the launcher's records unchanged (two). No git write in the whole check. | | | `s17a-open-file-asks`, `s17b-editor-pick`, `s17c-tools-editor-chosen`, `s17d-file-menu-with-editor` | n/a: the real window never clicks Open file or Open in editor (12.10: they would open the owner's programs); the Pick dialog's options are read in S14's launch and the native file dialog is S14's; the menus are drawn alike in both harnesses (phase 3's). `s17b`'s details show the fixed catalogue's paths under `C:\Tools`, the same on every Windows machine. `s17b-editor-pick` varies by nature: the keyboard focus is in `PickFilter` (SD6), whose caret may be caught on or off (`ShellDriver` holds only the resolver's caret still, D151); only the asserted state counts. Design 7.3, 7.5, 7.7, 12.1 to 12.9; D226; decisions SD6, SD20, SD23, SD29. |
| S18 | The identity, global and per repository (D190, D200, design 7.4, 8.1 to 8.4, 8.6): with nothing set but `user.useConfigOnly`, the global scope empty and the repository scope's in-effect line in the app's words; a name with `<` refused at Save (and an email without `@` warned about); a global identity saved into the global file, shown as the repository's placeholder and in effect there, and on the next commit; on a copy whose global file holds that identity, the repository's own identity saved as one operation of its queue, noticed in the global scope, in effect and on the next commit; "settings" with a change, dark, 1100×700 | **Part A** (`Check_S18_identity_global`): 1. "Settings" scenario; before the app starts seed the global file with `[user]` / `⇥useConfigOnly = true` and write `notes.txt` `one` `two` `three` (not staged). Start; wait for ST0w. Click `SettingsButton`; click `Git`. 2. Click `This repository`. 3. Click `All repositories (global)`. Type `Ann <Example>` into `UserNameBox` and `ann.example.com` into `UserEmailBox` (SD4). Click `Save`. Screenshot (`s18a-name-refused`). 4. Replace the name with `Ann Example` and the email with `ann@example.com`. Click `Save`; wait until the screen has closed. 5. Press Ctrl+,; click `Git`; click `This repository`. Screenshot (`s18b-repository-global-identity`). Press Esc. 6. Click the working-changes row (display row 0, (468, 129)); click `Stage all` (`StageAllButton`); type `Add a third note` into `CommitSummary`; click `CommitButton`; wait for "Commit" (Succeeded). Screenshot (`s18c-committed-as-global`).<br>**Part B** (`Check_S18_identity_per_repository`, a new copy and data folder, a new start): 7. "Settings" scenario; before the app starts seed the global file with `[user]` / `⇥useConfigOnly = true` / `⇥name = Ann Example` / `⇥email = ann@example.com` (the file step 4 writes, SD3) and write `notes.txt` `one` `two` `three` (not staged). Start; wait for ST0w. Press Ctrl+,; click `Git`; click `This repository`. Type `Bo Local` into `UserNameBox` and `bo@example.org` into `UserEmailBox`; click `Save`; wait for "Save repository settings" (Succeeded) and the screen closed. 8. Press Ctrl+,; click `Git`. Screenshot (`s18d-global-repository-sets-its-own`). Click `This repository`. Screenshot (`s18e-repository-own-identity`). Press Esc. 9. Click the working-changes row; click `Stage all`; type `Add a third note`; click `CommitButton`; wait for "Commit" (Succeeded). Screenshot (`s18f-committed-as-repository`). | **After step 1:** the global scope as S14 step 4 gives it (`s14c`'s state): `UserNameBox` and `UserEmailBox` empty with the placeholder `Not set`; no `UserNameOrigin` or `UserEmailOrigin` line, no error, no warning; no `IdentityInEffect` (8.6: repository scope only); `Not set` checked; `MergeToolValue` `Not set`. Calls since the click on `SettingsButton`, the screen's reads (SD29): the outside read and the repository read, each exit 0; both `git var` exit 128 with the last line `fatal: no email was given and auto-detection is disabled`; Pull's reader's three reads in `{root}\settings`, each exit 1.<br>**After step 2:** the repository scope as S14 step 5 gives it (`s14d`'s state): both boxes empty with `Not set`; `IdentityInEffect` `Git cannot make commits here: no email is set.`; `IdentityNotices` `user.useConfigOnly is set, so git does not guess a missing name or email.`; `Same as global` checked with `PullInEffect` `Pull into main: Merge (fast-forward when possible): nothing is set, so the app merges; git in a terminal stops when the branches have diverged.`; `MergeToolValue` `Same as global (not set)`. No call since step 1's reads.<br>**After step 3:** `UserNameError` `A name cannot contain <, > or a line break.` (`VcDangerBrush`) under the name and `UserEmailWarning` `This does not look like an email address.` (`TextBlock.dialogWarning`, `VcWarningBrush`) under the email, each shown as soon as typed (D114). Save refused (the name's error; a warning alone would not refuse, 8.6): the screen open on Git; `SettingsNavError` at the right of `Git`, help text `Has an error`; the keyboard focus in `UserNameBox`; no write and no read back; the global file is the seed.<br>**After step 4:** the error, the warning and the dot gone as typed (SD9). Save: the calls `git config --global --replace-all user.name "Ann Example"` then `git config --global --replace-all user.email ann@example.com`, each exit 0, in `{data}` (8.3's app-level writer; no `--local` call, no pull or `merge.tool` key); then the read back, the screen's reads (SD29), both `git var` now exit 0 with `Ann Example <ann@example.com> 1767344400 +0000` and Pull's three reads exit 1; each key's global-scope value in the outside read is the value written (SD30), so the screen closed. The global file exactly `[user]` / `⇥useConfigOnly = true` / `⇥name = Ann Example` / `⇥email = ann@example.com`; `.git/config` unchanged; no queue operation, so the status bar's operation text stays as it was (SD10), and no undo entry (Undo `Nothing to undo`).<br>**After step 5:** the global scope (as it opens) shows `Ann Example` and `ann@example.com` as values, no notice. The repository scope: both boxes empty with the placeholders `Same as global: Ann Example` and `Same as global: ann@example.com`; `IdentityInEffect` `Commits here are made as Ann Example <ann@example.com>.`; `IdentityNotices` the `user.useConfigOnly` line. Esc: nothing written.<br>**After step 6:** after `Stage all` the staged list holds M `notes.txt`; `CommitButton` `Commit 1 file`, tooltip `Commit 1 file (Ctrl+Enter)`. The call `git commit --file=- --cleanup=whitespace` (standard input `Add a third note`), exit 0. Git: `main` at `3e519fba64d22688690e5920cc3300e3c757e5d9` (parent `73f8cad`; author and committer `Ann Example <ann@example.com>`, both dates 2026-01-02 09:00 UTC; message `Add a third note`), `+2 -1`, clean. The graph ST1, row 0 selected; the details panel: `Add a third note`, Author `Ann Example <ann@example.com>`, Date `2026-01-02 09:00`, no Committer and no Commit date row, Parents `73f8cad`, `Changed files (1)` M `notes.txt`. Undo `Undo commit "Add a third note" (Ctrl+Z)`.<br>**After step 7:** before any action ST0w and Undo `Nothing to undo`; on opening, the repository scope as after step 5 (the placeholders `Same as global: Ann Example` and `Same as global: ann@example.com`, `IdentityInEffect` `Commits here are made as Ann Example <ann@example.com>.`, the `user.useConfigOnly` line). Save: one queue operation `Save repository settings` (8.3): the calls `git config --local --replace-all user.name "Bo Local"` then `git config --local --replace-all user.email bo@example.org` in `{root}\settings`, each exit 0, in this order (the conventions' rule), no `--global` call; the queue's log line `settings: Save repository settings: Succeeded after <N> ms: Saved the repository's settings`; the status bar `Saved the repository's settings` (secondary); the read back (the screen's reads, both `git var` now `Bo Local <bo@example.org> 1767344400 +0000`); each key's local-scope value in the repository read is the value written (SD30), so the screen closed. `.git/config` gains `[user]` / `⇥name = Bo Local` / `⇥email = bo@example.org` after `[branch "main"]`'s lines; the global file still the seed; no undo entry: Undo still `Nothing to undo` (D168).<br>**After step 8:** the global scope keeps the global file's values, `Ann Example` and `ann@example.com`, with `UserNameOrigin` `settings sets its own: Bo Local.` and `UserEmailOrigin` `settings sets its own: bo@example.org.` (secondary, 8.4; SD8). The repository scope: `UserNameBox` `Bo Local` and `UserEmailBox` `bo@example.org` (values, not placeholders); `IdentityInEffect` `Commits here are made as Bo Local <bo@example.org>.`; `IdentityNotices` the `user.useConfigOnly` line. Esc: nothing written.<br>**After step 9:** the call `git commit --file=- --cleanup=whitespace` (standard input `Add a third note`), exit 0. Git: `main` at `ead110cb6ae86125d9789cf40d64fb1608f55612` (parent `73f8cad`; author and committer `Bo Local <bo@example.org>`, 2026-01-02 09:00; message `Add a third note`; step 6's tree, only the identity differs), `+2 -1`, clean. The graph ST1r, row 0 selected; the details' Author `Bo Local <bo@example.org>`; Undo `Undo commit "Add a third note" (Ctrl+Z)`. The global file still the seed. | | | `s18a-name-refused`, `s18b-repository-global-identity`, `s18c-committed-as-global`, `s18d-global-repository-sets-its-own`, `s18e-repository-own-identity`, `s18f-committed-as-repository` | n/a: the Git section is pictured in the real window by S14's twin and L31/L34's; the identity is git's, which both harnesses read and write alike through each session's own global file (8.12, 17.2), and nothing native shows. The seed `user.useConfigOnly = true` keeps git from guessing an identity from the machine, so that the in-effect line and every `git var` are the same on every machine and CI runner (8.6). Steps 1 and 2 are asserted and not pictured: their screen states are S14's `s14c-git-global` and `s14d-git-repository` (the same seed; only the working-changes row behind the backdrop and S14's second tab differ). `s18a-name-refused` varies by nature: the keyboard focus is in `UserNameBox`, whose caret may be caught on or off (`ShellDriver` holds only the resolver's caret still, D151); only the asserted state counts. Two methods (SD1): part B starts from the global file part A's step 4 writes, so its commit has `3e519fb`'s parent and tree with Bo Local's identity. Design 7.4, 7.8, 8.1 to 8.4, 8.6, 14.8; D68, D72, D168, D190, D200, D218; decisions SD1, SD3, SD4, SD5, SD8, SD9, SD10, SD25, SD27, SD29, SD30. |
| S19 | Pull modes and their precedence (D190, D78, D84, design 7.4, 8.7 to 8.9, D218, D219): a global `pull.ff = only` shown as `Fast-forward only` and refusing the pull of diverged branches with 4.8's text; `Rebase` saved in "This repository" as both keys at `--local`, which beats the global `only` key by key, noticed in the global scope and in effect in the repository's; with those keys the pull rebasing `main`; `Same as global` unsetting both; a `branch.main.rebase` written outside the app, noticed and removed; `pull.rebase = preserve` shown as `Other` and refusing the pull before any fetch; "settings" with an identity, dark, 1100×700 | **Part A** (`Check_S19_pull_mode_ff_only_and_repository_rebase`): 1. "Settings" scenario; before the app starts seed the global file with `[user]` / `⇥name = Test Author` / `⇥email = author@example.com` / `[pull]` / `⇥ff = only` (SD3). Start; wait for ST0. Click `SettingsButton`; click `Git`. Screenshot (`s19a-global-ff-only`). 2. Press Esc. Click `PullButton`; wait for "Pull" (Failed) and its dialog. Screenshot (`s19b-pull-refused`). Click `Close`. 3. Press Ctrl+,; click `Git`; click `This repository`. Screenshot (`s19c-repository-same-as-global`). 4. Click `Rebase` (`PullRebase`). Click `Save`; wait for "Save repository settings" (Succeeded) and the screen closed. 5. Press Ctrl+,; click `Git`. Screenshot (`s19d-global-repository-sets-its-own`). Click `This repository`. Screenshot (`s19e-repository-rebase`). Press Esc.<br>**Part B** (`Check_S19_pull_rebases_by_the_repository_setting`, a new copy and data folder, a new start): 6. "Settings" scenario; the same seed; before the app starts `git config pull.rebase true` and `git config pull.ff true` in the copy (SD15: the `.git/config` step 4 writes). Start; wait for ST0. Click `PullButton`; wait for "Pull" (Succeeded). Screenshot (`s19f-pulled-rebased`).<br>**Part C** (`Check_S19_pull_mode_unset_branch_rebase_and_other`, a new copy and data folder, a new start): 7. "Settings" scenario; the same seed and the same two keys before the start as part B (SD15). Start; wait for ST0. Press Ctrl+,; click `Git`; click `This repository`; click `Same as global` (`PullNotSet`). Click `Save`; wait for "Save repository settings" (Succeeded) and the screen closed. 8. Outside the app `git config branch.main.rebase false`; wait for the watcher's refresh (`WaitForStatusReadAsync(app, 0, 0, mark)`) and the queue idle (the conventions). Press Ctrl+,; click `Git`; click `This repository`. Screenshot (`s19g-branch-rebase-notice`). 9. Click `Use the repository's setting` (`BranchRebaseButton`). Screenshot (`s19h-branch-rebase-marked`). Click `Save`; wait for "Save repository settings" (Succeeded) and the screen closed. 10. Outside the app `git config pull.rebase preserve`; wait for the watcher's refresh and the queue idle. Press Ctrl+,; click `Git`; click `This repository`. Screenshot (`s19i-other-value`). Press Esc. 11. Click `PullButton`; wait for "Pull" (Failed) and its dialog. Click `Close`. | **After step 1:** the global scope: `Test Author` and `author@example.com` as values, no notice; `Fast-forward only` (`PullFastForwardOnly`) checked, the other five not, no `PullOther`, no `PullNotices` (the value lies in the file the app writes, 8.2); `MergeToolValue` `Not set`. Among the screen's reads (SD29, eight), Pull's reader's four in `{root}\settings`, in this order: `git config --get pull.ff` (0, `only`), `git config --type=bool --get pull.ff` (128, `fatal: bad boolean config value 'only' for 'pull.ff'`: no boolean, so Fast-forward only), `git config --get branch.main.rebase` (1), `git config --get pull.rebase` (1).<br>**After step 2:** the calls of the operation in this order (the conventions' rule): `git config --get pull.ff` (0, `only`), `git config --type=bool --get pull.ff` (128), `git config --get branch.main.rebase` (1), `git config --get pull.rebase` (1), all before the fetch (8.9); `git fetch --progress origin` (0, nothing printed); `git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --` (0, `remote.txt\0`; no `check-ignore`, nothing on disk); `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` (128); no `git merge-base --is-ancestor 73f8cadf9ea2ae5e574a3e047d1a8fc4af933b98 FETCH_HEAD` (`pull.ff = only` chooses the fast-forward without asking, and nothing is in the way, 9.10) and no `git rebase`. The error dialog: `Pull failed`; `ErrorSummary` `main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main.` (`OperationTexts.PullNotFastForward`, 4.8, D211); `In settings`; `ErrorOutput` `fatal: Not possible to fast-forward, aborting.` (key; 2.55 shows its nine `hint:` lines first); `ErrorCommand` `git merge --ff-only --no-overwrite-ignore FETCH_HEAD`; `Copy output`, `Close`. The status bar `Pull failed` (danger). Git: `main` `73f8cad`, `origin/main` `e0cbfaf`, `+1 -1`, clean; `FETCH_HEAD` `e0cbfaf…⇥⇥branch 'main' of ../remotes/settings`; no backup ref; Undo `Nothing to undo`.<br>**After step 3:** the repository scope: the placeholders `Same as global: Test Author` and `Same as global: author@example.com`; `IdentityInEffect` `Commits here are made as Test Author <author@example.com>.`; no `IdentityNotices`; `Same as global` checked; `PullInEffect` `Pull into main: Fast-forward only (pull.ff = only in your global configuration).` (the file named as 8.2 says, its full path the line's tooltip); `MergeToolValue` `Same as global (not set)`. Pull's reader at this opening as at step 1.<br>**After step 4:** one queue operation `Save repository settings`, its writes in this order (the conventions' rule): `git config --local --replace-all pull.rebase true` then `git config --local --replace-all pull.ff true` (8.3's order; 8.7: both keys at the chosen scope), each exit 0, in `{root}\settings`; no `--global` call; the status bar `Saved the repository's settings`; the read back (the screen's reads, Pull's reader now three: `git config --get pull.ff` (0, `true`), `git config --get branch.main.rebase` (1), `git config --get pull.rebase` (0, `true`)): each key's local-scope value in the repository read is the value written (SD30; the global `pull.ff = only` comes before it), so the screen closed (8.4). `.git/config` gains `[pull]` / `⇥rebase = true` / `⇥ff = true`; the global file is the seed.<br>**After step 5:** the global scope: `Fast-forward only` still checked (the global file's own value, 8.2); `PullNotices` `settings sets its own: Rebase.` (8.4; SD8). The repository scope: `Rebase` checked; `PullInEffect` `Pull into main: Rebase (pull.rebase = true in .git/config).` (SD18); Pull's reader as in step 4's read back.<br>**After step 6:** before the pull ST0, `.git/config` with `[pull]` / `⇥rebase = true` / `⇥ff = true` after `[branch "main"]`'s lines, Undo `Nothing to undo`. The calls in this order (the conventions' rule): `git config --get pull.ff` (0, `true`: the repository's value beats the global `only`, 8.7's per-key precedence), `git config --get branch.main.rebase` (1), `git config --get pull.rebase` (0, `true`); `git fetch --progress origin` (0); `git merge-base --is-ancestor 73f8cadf9ea2ae5e574a3e047d1a8fc4af933b98 FETCH_HEAD` (1: HEAD as read before the integration, diverged); the backup, `git update-ref --stdin` with standard input `create refs/visualcommit/backup/rebase-20260102T090001000Z/main 73f8cadf9ea2ae5e574a3e047d1a8fc4af933b98` (0); `git rebase` (0; standard error `Rebasing (1/1)` and `Successfully rebased and updated refs/heads/main.`); no `git config --type=bool --get pull.ff` (`true` is a common word) and no call starting `git merge ` (no merge or fast-forward). Between them and not in the list, the rebase's reads, among them 9.6's ignored-files check (nothing in the way). The status bar `Pulled and rebased main onto origin/main` (secondary); no dialog. Git: `main` at `b4253bb077f7b07e212ac8f16861a728072f58b5` (parent `e0cbfaf`; author Test Author 2026-01-01 12:02, committer `Test Author <author@example.com>` 2026-01-02 09:00), `+1 -0`, clean; the backup ref at `73f8cad`; HEAD's reflog as "Scenarios and graphs" gives. The graph STR; Undo `Undo pull into main (Ctrl+Z)`.<br>**After step 7:** on opening, the global scope `Fast-forward only` with `PullNotices` `settings sets its own: Rebase.`; the repository scope `Rebase` checked, `PullInEffect` `Pull into main: Rebase (pull.rebase = true in .git/config).` (Pull's reader: `true`, exit 1, `true`). After `Same as global` and Save: one operation, its writes in this order `git config --local --unset-all pull.rebase` then `git config --local --unset-all pull.ff`, each exit 0; the status bar `Saved the repository's settings`; the read back (Pull's reader back to step 1's four): the repository read's local scope holds neither key, which is what was written (SD30: the global `pull.ff = only` beneath it, the read's last entry over every scope, is what `Same as global` asks for and holds nothing open), so the screen closed; `.git/config` without a `[pull]` section, its other lines unchanged.<br>**After step 8:** the watcher's refresh ended before the opening (no refs or status read among the screen's calls). The repository scope: `Same as global` checked; `PullInEffect` `Pull into main: Fast-forward only (pull.ff = only in your global configuration).`; `BranchRebaseNotice` `main has its own pull setting, branch.main.rebase = false, which git uses instead of the mode above.` and `BranchRebaseButton` `Use the repository's setting` (the entry is in `.git/config`, 8.8). Pull's reader: `git config --get pull.ff` (0, `only`), `git config --type=bool --get pull.ff` (128), `git config --get branch.main.rebase` (0, `false`), and no `git config --get pull.rebase` (the branch's key answered, 8.9).<br>**After step 9:** the notice `Save removes branch.main.rebase, so main pulls as set above.` and the button `Keep main's setting`; nothing written yet (`.git/config` still has `rebase = false`). Save: one operation with a single write, `git config --local --unset-all branch.main.rebase` (0), and none for `pull.rebase` or `pull.ff` (untouched, 8.3); the status bar `Saved the repository's settings`; the read back (Pull's reader as at step 1): the local scope holds no `branch.main.rebase` (SD30), so the screen closed; `[branch "main"]` back to `remote = origin` and `merge = refs/heads/main`.<br>**After step 10:** after the refresh, as at step 8. The repository scope: `PullOther` shown and checked, `Other: pull.rebase = preserve`, with its second line `Pulls fail while this value is set.`; the six other radios unchecked; `PullInEffect` `Pull into main fails: pull.rebase is set to preserve, which this app does not understand.`; no `BranchRebaseNotice`. (The global scope on opening: `Fast-forward only` with `PullNotices` `settings sets its own: Other: pull.rebase = preserve.`, SD8.) Pull's reader five, nine processes in all (SD29): `only` (0), the `--type=bool` read of `pull.ff` (128), `branch.main.rebase` (1), `preserve` (0), `git config --type=bool --get pull.rebase` (128). Esc: nothing written.<br>**After step 11:** the calls in this order (the conventions' rule): `git config --get pull.ff` (0, `only`), `git config --type=bool --get pull.ff` (128, `fatal: bad boolean config value 'only' for 'pull.ff'`), `git config --get branch.main.rebase` (1), `git config --get pull.rebase` (0, `preserve`), `git config --type=bool --get pull.rebase` (128, `fatal: bad boolean config value 'preserve' for 'pull.rebase'`), and no `git fetch`, no call starting `git merge ` and no `git rebase` (8.9, D219). The error dialog: `Pull failed`; `ErrorSummary` `pull.rebase is set to preserve, which this app does not understand.`; `In settings`; `ErrorOutput` the same sentence; `ErrorCommand` `git config --get pull.rebase`; `Copy output`, `Close`. The status bar `Pull failed` (danger). Git: no `FETCH_HEAD` (nothing was fetched in this copy), `origin/main` `e0cbfaf`, `main` `73f8cad`. | | | `s19a-global-ff-only`, `s19b-pull-refused`, `s19c-repository-same-as-global`, `s19d-global-repository-sets-its-own`, `s19e-repository-rebase`, `s19f-pulled-rebased`, `s19g-branch-rebase-notice`, `s19h-branch-rebase-marked`, `s19i-other-value` | n/a: the screen's pull rows are drawn as S14's twin and L34 draw the screen, the dialogs are phase 3's error dialog (pictured in the real window by phase 3's checks), and the configuration is git's, which both harnesses read alike. The seeded identity is `Test Author <author@example.com>`, so that the rebased commit is the scenario facts' `b4253bb` (SD3). Three methods (SD1): parts B and C start from the `.git/config` part A's step 4 writes (SD15). Design 4.8, 7.4, 8.3, 8.4, 8.7 to 8.9, 9.10, 14.1, 14.8; D78, D84 (amended by D219), D190, D200, D211, D218, D219; decisions SD1, SD3, SD8, SD15, SD18, SD29, SD30. |
| S20 | The merge tool chosen on the screen (D149, D202, design 7.7, 8.10, 8.11): with no `merge.tool` the resolver's `Merge tool…` disabled with its reason; the Pick dialog `Choose a merge tool` listing the fixed catalogue's tools as 8.10 filters them (no `vimdiff`) and the user's own `slow`; `slow` saved globally enabling `Merge tool…` in the open resolver without reopening it; `araxis`, which git did not find, chosen and its notice shown before Save; `Not set` disabling it again; "conflicts" stopped in its merge, dark, 1100×700 | One app start (`Check_S20_merge_tool_chosen_in_settings`): 1. `Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`; before the app starts seed the global file with `[mergetool "slow"]` / `⇥cmd = true` (SD3). Start; wait for GC-M with its banner. Click the working-changes row; move the pointer to the rest point; double-click the unstaged `notes.txt`; wait until the resolver shows it. Screenshot (`s20a-merge-tool-disabled`). 2. Click `SettingsButton`; click `Git`; click `Choose…` (`MergeToolChooseButton`). Screenshot (`s20b-merge-tool-pick`). 3. Click `slow`, then `Choose`. Click `Save`; wait until the screen has closed and `Merge tool…` is enabled. Screenshot (`s20c-merge-tool-enabled`). 4. Click `SettingsButton`; click `Git`; click `Choose…`; click `araxis`, then `Choose`. Click `Choose…` again; click `Not set`, then `Choose`. Click `Save`; wait until the screen has closed and `Merge tool…` is disabled. | **After step 1:** phase 4's C10 step 5 resolver: header `U`, `notes.txt`, `Both modified`, `File 1 of 1`; toolbar `Conflict 1 of 2` with its arrows, `Take ours`, `Take theirs`, `Merge tool…` (`ResolverMergeTool`) **disabled**, `ToolTip.GetTip` `Set merge.tool in git's configuration to use a merge tool` (8.10: the text stays), `Mark resolved`; the banner across the resolver. Git: `UU notes.txt`, `MERGE_HEAD` `5c77ffb0ec78abf986d121f42013eee14422e5a5`; `git config --get merge.tool` exit 1.<br>**After step 2:** Git on `All repositories (global)`: `MergeToolValue` `Not set`; `Choose…` enabled at once (no `MergeToolStatus`, no `Looking for the merge tools git knows…`: the fixed list is there at once, 12.9, D202). The Pick dialog `Choose a merge tool` stacked over the screen: `PickFilter` focused; the options (text — detail), in this order (SD7): `Not set` — `Merge tool… stays disabled` (selected, SD6), `kdiff3` — `found on this computer`, `meld` — `found on this computer`, `slow` — `your own tool (mergetool.slow.cmd)`, `araxis` — `not found on this computer`, `bc` — `not found on this computer`, `p4merge` — `not found on this computer`, `winmerge` — `not found on this computer`: eight rows of 28, all in view without scrolling; no `vimdiff` (a terminal tool, left out by 8.10); buttons `Cancel`, `Choose`. No `git mergetool` call in the session.<br>**After step 3:** `MergeToolValue` `slow`, no `MergeToolStatus` (a tool of one's own is listed as such, not as one git did not find, 8.10), no `MergeToolNotices`. Save: one write, `git config --global --replace-all merge.tool slow` (0) in `{data}`, then the read back (the screen's reads, SD29, in `{data}` and `{root}\conflicts`); the global file exactly `[mergetool "slow"]` / `⇥cmd = true` / `[merge]` / `⇥tool = slow`; the screen closed; no queue operation (SD10). Then, without the resolver closed or reopened (8.11, `GitSettingsChanged`): one `git config --get merge.tool` in `{root}\conflicts` (0, `slow`) and `Merge tool…` **enabled**, with no tooltip; the result unchanged and not modified, `Conflict 1 of 2`; `UU notes.txt`.<br>**After step 4:** in the first Pick dialog `slow` selected (the field's value, SD6); after `araxis` and `Choose`, `MergeToolValue` `araxis` and `MergeToolStatus` `git did not find araxis on this computer: Merge tool… fails until it is installed.` (8.10: a tool the fixed list gives as not found, 12.9); nothing written. In the second Pick dialog `araxis` selected (SD6); after `Not set` and `Choose`, `MergeToolValue` `Not set` and no `MergeToolStatus`. Save: one write, `git config --global --unset-all merge.tool` (0; `araxis` was never saved, so no `--replace-all merge.tool araxis`), then the read back (the screen's reads, SD29); the global file exactly the seed again (`[mergetool "slow"]` / `⇥cmd = true`); closed; one `git config --get merge.tool` (1) and `Merge tool…` disabled again with `Set merge.tool in git's configuration to use a merge tool`; the merge still stopped (`UU notes.txt`, `MERGE_HEAD` as before). | | | `s20a-merge-tool-disabled`, `s20b-merge-tool-pick`, `s20c-merge-tool-enabled` | n/a: the resolver is phase 4's (pictured in the real window by C10), the Pick dialog is S14's (read in its launch), and the merge tool's list in both harnesses is the fixed catalogue; nothing native shows. No tool is run (`cmd = true` is never started). The kept list and the background lookup (D202) are 8.13's `MergeToolHelpTests`' and the lead's `AppSessionTests`' (17.5). `s20b-merge-tool-pick` varies by nature: the keyboard focus is in `PickFilter` (SD6), whose caret may be caught on or off (`ShellDriver` holds only the resolver's caret still, D151); only the asserted state counts. Design 7.7, 8.10, 8.11, 14.6, 14.8; D149, D202, D217; decisions SD3, SD6, SD7, SD10, SD22, SD29. |
| S21 | Keep ignored files on, then off (D193, D196, D221, design 9.1, 9.11, 7.3): on, `Check out vendor` refused with D179's text; turned off in the screen, a checkout of the older commit "Track the secret" of the current branch still refused (D179's automatic stash path keeps its option, 9.1); off, `Check out vendor` runs as git runs it and overwrites the ignored `secret.env`; "ignored", dark, 1100×700 | One app start (`Check_S21_keep_ignored_files_on_and_off`, SD13): 1. "Ignored" scenario (IG0). Start; wait for IG0. Right-click Local branches › vendor in the left panel; click `Check out vendor`; wait for "Check out vendor" (Failed) and its dialog. Click `Close`. 2. Click `SettingsButton`; click `KeepIgnoredCheckBox`; click `Save`. 3. Right-click the row "Track the secret" (commit row 5, (484, 259)) in its Message column; click `Check out this commit`; wait for "Check out 1ac759d" (Failed) and its dialog. Screenshot (`s21a-off-old-commit-blocked`). Click `Close`. 4. Right-click Local branches › vendor; click `Check out vendor`; wait for "Check out vendor" (Succeeded). Screenshot (`s21b-off-checkout-ran`). | **After step 1:** the setting is on (the default). The calls: `git switch --no-guess --no-overwrite-ignore vendor` (1; standard error `error: The following untracked working tree files would be overwritten by checkout:` (key) / `⇥secret.env` / `Please move or remove them before you switch branches.` / `Aborting`; 2.55 identical), then `git check-ignore --stdin -z` without `GIT_LITERAL_PATHSPECS` (input `secret.env\0`, output `secret.env\0`, exit 0). The error dialog: `Checkout blocked`; `ErrorSummary` `1 ignored file is in the way: vendor tracks a file of the same name. Move it away first.` (D179's text, 9.11, 14.4); `In ignored`; `ErrorOutput` git's four lines; `ErrorCommand` `git switch --no-guess --no-overwrite-ignore vendor`; `Copy output`, `Close`; no `Stash changes and check out`. The status bar `Check out vendor failed` (danger). Nothing changed: `TempRepo.ExactStateAsync` equal to the state read at the start (HEAD `main` at `c49cac4`, `secret.env` `LOCAL`, `build/out.bin` `binary`, `!! build/`, `!! secret.env`); Undo `Nothing to undo`.<br>**After step 2:** on opening `KeepIgnoredCheckBox` checked; after the click unchecked. Save: the screen closed; `settings.json` `keepIgnoredFiles` false, the rest at the defaults; no git write (no `git config` with `--replace-all` or `--unset-all`): the only git calls are the screen's reads at its opening and again in the read back (SD29), seven each.<br>**After step 3:** "Track the secret" selected; the commit menu's `Check out this commit` enabled. D179's path (an older commit of the current branch, scenario facts 4.2): no `git stash` call (nothing to stash: an ignored file is no change), then `git switch --detach --no-overwrite-ignore 1ac759d23fd78a78f7660d289a1373ea85378b69` (1; the same block naming `secret.env`), the option kept though the setting is off (9.1, D221), and `git check-ignore --stdin -z` (`secret.env\0` → `secret.env\0`, 0). The error dialog: `Checkout blocked`; `1 ignored file is in the way: 1ac759d tracks a file of the same name. Move it away first.`; `In ignored`; `ErrorOutput` the block; `ErrorCommand` `git switch --detach --no-overwrite-ignore 1ac759d23fd78a78f7660d289a1373ea85378b69`; `Copy output`, `Close`; no extra button. The status bar `Check out 1ac759d failed` (danger). Nothing changed (the exact state as at the start).<br>**After step 4:** the call `git switch --no-guess vendor` (no `--no-overwrite-ignore`: off restores phase 5, D221), exit 0, standard output `Your branch is up to date with 'origin/vendor'.`, standard error `Switched to branch 'vendor'`; no `git check-ignore` and no ignored-files read; no question, no dialog. The status bar `Checked out vendor` (secondary). Git: HEAD `vendor` at `133a796`; `secret.env` `vendor` (the ignored `LOCAL` overwritten without a word, as git does, Q5); `build/out.bin` `binary`; `git status --porcelain=v1 --ignored` prints `!! build/` only; the heads `main`, `tools`, `vendor` and the stash unchanged. The graph IG0v; Undo `Undo checkout of vendor (Ctrl+Z)`. | | | `s21a-off-old-commit-blocked`, `s21b-off-checkout-ran` | n/a: the error dialog is phase 5's (pictured in the real window by P24 and phase 3's check 14) and the switch is the screen's General, pictured in the real window by S14; the commands differ only in their text, which both harnesses read alike (9.17: "not in the real window"). The other operations Keep ignored files covers, on and off, are part G's (G22 to G24); this check proves the switch on the screen and its effect on the next operation without a restart (7.9). Step 1's refusal is asserted and not pictured: it is G22's first picture, `g22a-vendor-blocked` (the same scenario, refusal, dialog and texts; G22 reaches it by a double-click on the graph label, this step from the left panel's menu). Design 7.3, 7.9, 9.1, 9.2, 9.11, 9.15; D179, D193, D196, D221; decisions SD13, SD29. |
| G22 | Checkouts refused for an ignored file in the way, on every checkout path (D193, design 9.2's first row, 9.11, 9.15): a local branch by a double-click on its graph label; a remote branch whose local branch exists, checked out as that branch (D93); a remote branch checked out as a new tracking branch; "Create branch here…" with "Check out the new branch" ticked; an ignored folder where the target has a file; a commit `main` does not reach, detached from the graph's commit menu (D193's path, not D179's automatic stash). Each refused by git with `--no-overwrite-ignore`, told in D179's words with git's count, nothing changed; "ignored", dark, 1100×700 | (`Check_G22_checkouts_refused_for_ignored_files`) 1. "Ignored" scenario, nothing changed (IG0). Start. The check reads the exact state. 2. Double-click the graph label `vendor` (row 3, "Add the vendor secret"). Wait for "Check out vendor" (Failed) and its dialog. Screenshot. Click "Close". 3. Right-click Remotes › origin › vendor and click "Check out origin/vendor". Wait for "Check out origin/vendor" (Failed) and its dialog. Screenshot. Click "Close". 4. Right-click Remotes › origin › vendor-remote and click "Check out origin/vendor-remote". Wait for "Check out origin/vendor-remote" (Failed) and its dialog. Screenshot. Click "Close". 5. Right-click Local branches › vendor and click "Create branch here…". In "Create branch" type `vendor-copy`, leave "Check out the new branch" ticked and click "Create branch". Wait for "Create branch vendor-copy" (Failed) and its dialog. Screenshot. Click "Close". 6. Right-click Local branches › tools and click "Check out tools". Wait for "Check out tools" (Failed) and its dialog. Screenshot. Click "Close". 7. Right-click the row "Add the vendor secret" (row 3, (484, 207)) in its Message column and click "Check out this commit". Wait for "Check out 133a796" (Failed) and its dialog. Screenshot. Click "Close". 8. The check reads the exact state, `git config --get-regexp ^branch\.` and HEAD's reflog. | **After step 1:** IG0; `CurrentBranch` `main`; Undo `Nothing to undo`; `git status --porcelain=v1 --untracked-files=all --ignored` prints `!! build/out.bin`, `!! secret.env`.<br>**After step 2:** the double-click's first click selected "Add the vendor secret" (row 3; the details panel shows it). The call log has, in this order, `git switch --no-guess --no-overwrite-ignore vendor` (exit 1; standard output empty; standard error TG1) and `git check-ignore --stdin -z` (input `secret.env\0`; exit 0, `secret.env\0`), and no `git stash` call (a branch is no automatic stash's target, D179). That exit 0 is what shows D74 here: the call log keeps no environment, and with `GIT_LITERAL_PATHSPECS=1` git refuses the call (exit 128, `fatal: secret.env: pathspec magic not supported by this command: 'literal'`, 2.36 and 2.55), so the app ran it without. The error dialog: `ErrorTitle` "Checkout blocked"; `ErrorSummary` "1 ignored file is in the way: vendor tracks a file of the same name. Move it away first."; "In ignored"; `ErrorOutput` TG1; `ErrorCommand` `git switch --no-guess --no-overwrite-ignore vendor`; "Copy output" and "Close", no "Stash changes and check out" (a stash keeps no ignored file, D91, PD8). The status bar "Check out vendor failed" in the danger colour; `CurrentBranch` `main`. Nothing changed: the exact state equal to step 1's (`secret.env` `LOCAL\n`); HEAD's reflog 10 entries. Undo `Nothing to undo`.<br>**After step 3:** the right-click left the graph's selection as it was (D114): row 3 stays selected; "Check out origin/vendor" was enabled (HEAD is not on `vendor`). The call log has `git switch --no-guess --no-overwrite-ignore vendor` (the local `vendor`, D93; an identity mapping's local name, 10.6), exit 1, TG1, then `git check-ignore --stdin -z` as in step 2; no `--create`, no `--track`. The error dialog as after step 2 but `ErrorSummary` "1 ignored file is in the way: origin/vendor tracks a file of the same name. Move it away first." (9.11's `plan.Name`); the same `ErrorCommand`. The status bar "Check out origin/vendor failed". Nothing changed.<br>**After step 4:** "Check out origin/vendor-remote" was enabled (no local `vendor-remote`). The call log has `git switch --create vendor-remote --track --no-overwrite-ignore refs/remotes/origin/vendor-remote`, exit 1, TG1, then `git check-ignore --stdin -z` as before. The error dialog: "Checkout blocked"; "1 ignored file is in the way: origin/vendor-remote tracks a file of the same name. Move it away first."; "In ignored"; `ErrorOutput` TG1; `ErrorCommand` `git switch --create vendor-remote --track --no-overwrite-ignore refs/remotes/origin/vendor-remote`; "Copy output" and "Close". The status bar "Check out origin/vendor-remote failed". Git made no branch: `git rev-parse --verify --quiet refs/heads/vendor-remote` exit 1, `git config --get-regexp ^branch\.vendor-remote\.` exit 1; the exact state equal; Undo `Nothing to undo` (no entry, phase 5 design 7.2).<br>**After step 5:** the name dialog had the title "Create branch", the context "From vendor" and "Check out the new branch" ticked; "Create branch" enabled once `vendor-copy` was typed. The call log has `git switch --create vendor-copy --no-overwrite-ignore refs/heads/vendor` (the branch menu's start, GD4), exit 1, TG1, then `git check-ignore --stdin -z` as before; no `git branch` call. The error dialog: "Checkout blocked"; "1 ignored file is in the way: vendor tracks a file of the same name. Move it away first." (the start as the dialog names it, 9.11); "In ignored"; `ErrorOutput` TG1; `ErrorCommand` `git switch --create vendor-copy --no-overwrite-ignore refs/heads/vendor`; "Copy output" and "Close", no extra button (phase 5's PD4). The status bar "Create branch vendor-copy failed". No `refs/heads/vendor-copy`; the exact state equal; Undo `Nothing to undo`.<br>**After step 6:** the call log has `git switch --no-guess --no-overwrite-ignore tools`, exit 1, TG2, then `git check-ignore --stdin -z` with the folder by its files (input `build/out.bin\0`, 9.3; exit 0, `build/out.bin\0`). The error dialog: "Checkout blocked"; "1 ignored file is in the way: tools tracks a file of the same name. Move it away first." (git's count: one folder, phase 5's PD9); "In ignored"; `ErrorOutput` TG2; `ErrorCommand` `git switch --no-guess --no-overwrite-ignore tools`; "Copy output" and "Close". The status bar "Check out tools failed". Nothing changed: `build/out.bin` `binary\n`.<br>**After step 7:** row 3 still selected (the right-click on it); the commit menu's "Check out this commit" was enabled (HEAD is not detached at it). No question (HEAD is on a branch: nothing is left behind, phase 3's 2.1), and no `git stash` call: `main` does not reach Add the vendor secret, so it is no automatic stash's target (D179) and takes D193's detached checkout. The call log has `git switch --detach --no-overwrite-ignore 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f` (the full SHA, 9.11), exit 1, TG1, then `git check-ignore --stdin -z` (input `secret.env\0`; exit 0, `secret.env\0`). The error dialog: "Checkout blocked"; "1 ignored file is in the way: 133a796 tracks a file of the same name. Move it away first." (9.11's `plan.Name`, the short SHA); "In ignored"; `ErrorOutput` TG1; `ErrorCommand` `git switch --detach --no-overwrite-ignore 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f`; "Copy output" and "Close", no "Stash changes and check out" (D91, PD8). The status bar "Check out 133a796 failed" in the danger colour; `CurrentBranch` `main`. Nothing changed: HEAD `refs/heads/main` (`git symbolic-ref -q HEAD`), not detached; `secret.env` `LOCAL\n`; Undo `Nothing to undo`.<br>**After step 8:** the exact state equal to step 1's (HEAD `main` at `c49cac4…`; refs `main`, `tools`, `vendor`, the three remote branches and `refs/stash` only; `secret.env` `LOCAL\n`, `build/out.bin` `binary\n`); branch keys `main`'s and `vendor`'s only; HEAD's reflog unchanged (10 entries, the newest `reset: moving to HEAD`); no backup ref; Undo `Nothing to undo`. | | | `g22a-vendor-blocked`, `g22b-origin-vendor-blocked`, `g22c-tracking-checkout-blocked`, `g22d-create-branch-blocked`, `g22e-folder-blocked`, `g22f-commit-blocked`: each the error dialog centred over the dimmed window, IG0 behind it with row 3 ("Add the vendor secret") selected since step 2's double-click (step 7's right-click selects it again); a summary longer than the dialog's line wraps within it, its words as given | RW: n/a: the error dialog is phase 5's, pictured in the real window by P24 and phase 3's check 14; these differ in their texts and git calls, which both harnesses read alike (design 9.17). Design 0.4 (checkouts with the option), 9.2 (its first row: a commit that detaches), 9.11 (the option before the positional argument; `CheckoutRefusalDisplayAsync`; where each path catches and the name its text takes), 9.15; D93, D179's text, D193, D196, D74 (`check-ignore` exits 0 with the path, which git refuses under `GIT_LITERAL_PATHSPECS=1`); scenario facts 4.2. Step 7's git texts derived on a fresh copy of "ignored" with 2.36 and replayed on 2.55, identical (TG1). Decisions GD2 to GD4, GD17. |
| G23 | The app's own check before git (D196, design 9.3 to 9.10, 9.15): a merge, a rebase onto a branch, a cherry-pick, a revert, a stash's apply and its pop refused with their own error titles and 14.4's texts naming `secret.env`, no git output, nothing written; and the pull's check inside its integration (9.10): a branch only ahead of its upstream with a local merge whose side branch starts at a commit that tracks the file, under `pull.rebase` `merges` and `true`, refused through the branch points (9.6's (5) and (6)); a merge whose source adds nothing HEAD lacks let through (one read, no `check-ignore`, git runs); a diverged upstream's merge refused; under `pull.ff=only` git's own refusal shown instead; "ignored", dark, 1100×700 | **Part A** (`Check_G23_merge_rebase_pick_revert_and_stash_refused`): 1. "Ignored" scenario, nothing changed (IG0). Start. The check reads the exact state. 2. Right-click Local branches › vendor and click "Merge vendor into main". Wait for "Merge vendor" (Failed) and its dialog. Screenshot. Click "Close". 3. Right-click Local branches › vendor and click "Rebase main onto vendor". Wait for "Rebase main onto vendor" (Failed) and its dialog. Screenshot. Click "Close". 4. Right-click the row "Add the vendor secret" (row 3, (484, 207)) in its Message column and click "Cherry-pick onto main". Wait for "Cherry-pick 133a796" (Failed) and its dialog. Screenshot. Click "Close". 5. Right-click the row "Stop tracking the secret" (row 4, (484, 233)) and click "Revert this commit". Wait for "Revert 39bd803" (Failed) and its dialog. Screenshot. Click "Close". 6. Right-click Stashes › On main: Stash the secret. Click "Apply stash". Wait for "Apply stash@{0}" (Failed) and its dialog. Screenshot. Click "Close". 7. Click the toolbar's Pop (`PopButton`). Wait for "Pop stash@{0}" (Failed) and its dialog. Screenshot. Click "Close". 8. The check reads the exact state, the backup refs, the stash list and the in-progress files.<br>**Part B** (`Check_G23_pull_refused_inside_the_integration`, a new copy, a new start): 9. "Ignored" scenario; before the app starts build IG1 (the table above: "Start the side branch" on Track the secret, the branch `side`, `git merge --no-ff side`) and set `pull.rebase = merges` in `.git/config`. Start. The check reads the exact state. 10. Click the toolbar's Pull (`PullButton`). Wait for "Pull" (Failed) and its dialog. Screenshot. Click "Close". 11. Outside the app `git config pull.rebase true`; wait for the refs read and idle. Click Pull. Wait for "Pull" (Failed) and its dialog. Click "Close". 12. Outside the app `git config --unset pull.rebase`; wait for the refs read and idle. Click Pull. Wait for "Pull" (Succeeded). 13. Outside the app `git config branch.main.merge refs/heads/vendor`; wait for the refs read and until Pull's tooltip is `Pull from origin/vendor (Ctrl+Shift+L)` (IG1v). Click Pull. Wait for "Pull" (Failed) and its dialog. Screenshot. Click "Close". 14. Outside the app `git config pull.ff only`; wait for the refs read and idle. Click Pull. Wait for "Pull" (Failed) and its dialog. Screenshot. Click "Close". 15. The check reads the exact state, the backup refs and the in-progress files. | **After step 1:** IG0, nothing selected; Undo `Nothing to undo`.<br>**After step 2:** the right-click left the graph's selection as it was: nothing selected (D114); "Merge vendor into main" was enabled. The call log has TR1's four calls in that order and no write (no `git merge `, no `git fmt-merge-msg`: the check comes before `Git.MergeAsync`, 9.9). The error dialog: `ErrorTitle` "Merge failed"; `ErrorSummary` "1 ignored file is in the way of the merge: secret.env. Move it away first."; "In ignored"; no `ErrorOutput`, no `ErrorCommand`, no "Copy output": "Close" only. The status bar "Merge vendor failed" in the danger colour. No `MERGE_HEAD`; the exact state equal to step 1's (`secret.env` `LOCAL\n`); Undo `Nothing to undo`.<br>**After step 3:** still nothing selected; "Rebase main onto vendor" was enabled (`main` does not contain vendor's tip). The call log has TR2's four reads (in any order), then `git check-ignore --stdin -z` (`secret.env\0`): five calls, and no write: no `git rebase`, no backup ref `rebase-…` (`SaveRebaseBackupAsync` comes after the check, 9.9). The error dialog: "Rebase failed"; "1 ignored file is in the way of the rebase: secret.env. Move it away first."; "In ignored"; "Close" only. The status bar "Rebase main onto vendor failed". No `rebase-merge/` or `rebase-apply/`; the exact state equal.<br>**After step 4:** "Add the vendor secret" selected (the right-click); "Cherry-pick onto main" was enabled. The call log has TR3's two calls and no `git cherry-pick`. The error dialog: "Cherry-pick failed"; "1 ignored file is in the way of the cherry-pick: secret.env. Move it away first."; "In ignored"; "Close" only. The status bar "Cherry-pick 133a796 failed". No `CHERRY_PICK_HEAD`, no `sequencer/`; the exact state equal.<br>**After step 5:** "Stop tracking the secret" selected; "Revert this commit" was enabled. The call log has TR4's two calls and no `git revert`. The error dialog: "Revert failed"; "1 ignored file is in the way of the revert: secret.env. Move it away first."; "In ignored"; "Close" only. The status bar "Revert 39bd803 failed". No `REVERT_HEAD`; the exact state equal.<br>**After step 6:** row 4 ("Stop tracking the secret") still selected and the details panel on it: the right-click on a left-panel row leaves the graph's selection as it was (D114), and the refused revert's refresh kept it. The stash's menu: header `stash@{0} On main: Stash the secret`; "Apply stash" (highlighted); "Pop stash" with its key `Ctrl+Shift+O` at the right (6.7: `stash@{0}`'s); "Drop stash…" (danger); —; "Copy SHA"; 145 high. After the click, TR5: D179's count (the stash's three reads, which ran once in the session up to here, and a status read), then `git check-ignore --stdin -z` (`secret.env\0`), and no `git stash apply`. The error dialog: "Applying the stash failed"; "1 ignored file is in the way of applying the stash: secret.env. Move it away first."; "In ignored"; "Close" only. The status bar "Apply stash@{0} failed". `git stash list` prints `stash@{0}: On main: Stash the secret`; the exact state equal.<br>**After step 7:** row 4 still selected (the toolbar's Pop changes no selection; the refusal's refresh keeps it). Pop was enabled, its tooltip `Pop the newest stash (Ctrl+Shift+O)`. The call log has D179's count's status read, none of the stash's three reads (kept by its SHA since step 6), then `git check-ignore --stdin -z` (`secret.env\0`), and no `git stash pop` and no backup ref `stash-…` (the pop's backup comes after the check, 9.9, D171). The error dialog: "Applying the stash failed"; the same summary; "In ignored"; "Close" only. The status bar "Pop stash@{0} failed".<br>**After step 8:** the exact state equal to step 1's; `git for-each-ref refs/visualcommit/backup` prints nothing; `git stash list` as at the start; no `MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `sequencer/`, `rebase-merge/`, `rebase-apply/`; HEAD's reflog unchanged; Undo `Nothing to undo`.<br>**After step 9:** IG1; Pull enabled, `Pull from origin/main (Ctrl+Shift+L)`; `main` ↑ `2` in the left panel; `secret.env` `LOCAL\n`.<br>**After step 10:** "Pull" ran (`Pulling origin/main into main…`). The call log has, in this order: 8.9's reads before the fetch, `git config --get pull.ff` (exit 1), `git config --get branch.main.rebase` (exit 1), `git config --get pull.rebase` (`merges`), `git config --type=bool --get pull.rebase` (exit 128: no boolean, so the rebase keeping merges); `git fetch --progress origin` (exit 0, TG12); then TR6: `git rev-parse --verify --quiet HEAD`, `git merge-base --is-ancestor 6e68a25… FETCH_HEAD` (exit 1), 9.6's four reads, (5), (6) and `git check-ignore --stdin -z` (`secret.env\0`), with TR6's outputs; then no write: no `git rebase`, no backup ref `rebase-…` (`beforeRebase` not called, 9.10), no `git merge `. The error dialog: "Pull failed"; "1 ignored file is in the way of the rebase: secret.env. Move it away first."; "In ignored"; "Close" only (`PullRefusalDisplay` returns null for it, 9.10). The status bar "Pull failed" in the danger colour. The exact state equal to step 9's (`secret.env` `LOCAL\n`; HEAD `6e68a25…`); Undo `Nothing to undo`.<br>**After step 11:** the same calls but `git config --get pull.rebase` prints `true` and no `--type=bool` read follows it (a common word, `ReadPullBoolAsync`); TR6 with the same outputs (the plain rebase is checked by the same reads, 9.6: with a merge in the range (5) and (6) run whatever the options); no write. The same dialog and status bar. The exact state equal.<br>**After step 12:** the config reads (`pull.ff`, `branch.main.rebase`, `pull.rebase` exit 1 each), the fetch, then TR7: `git rev-parse --verify --quiet HEAD` and one `git diff-tree … --diff-filter=A HEAD FETCH_HEAD --` (nothing: the source adds nothing `main` lacks), and among the pull's calls no `git check-ignore` and no `git merge-base --all` (9.4 step 2: one read when nothing is a candidate); then `git merge --no-edit --no-overwrite-ignore FETCH_HEAD` (exit 0, TG11). No dialog. The status bar `main is already up to date` (secondary). HEAD `6e68a25…`; the exact state equal; Undo `Nothing to undo` (nothing changed, no entry).<br>**After step 13:** IG1v (`main` ↑ `3` ↓ `1`). The config reads (exit 1 each), the fetch (`FETCH_HEAD` marks vendor's `133a796…`), then TR8's five calls with their outputs, and no `git merge `. The error dialog: "Pull failed"; "1 ignored file is in the way of the merge: secret.env. Move it away first."; "In ignored"; "Close" only. The status bar "Pull failed". The exact state equal.<br>**After step 14:** the config reads `git config --get pull.ff` (`only`), `git config --type=bool --get pull.ff` (exit 128), `git config --get branch.main.rebase`, `git config --get pull.rebase` (exit 1 each); the fetch; TR9 (the check finds `secret.env`, then `git merge-base --is-ancestor 6e68a25… FETCH_HEAD` exit 1: on diverged branches nothing counts as in the way, 9.10); then `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` (exit 128, TG3). The error dialog: "Pull failed"; `ErrorSummary` "main and origin/vendor have diverged, and pull.ff is set to only: merge origin/vendor into main or rebase main onto origin/vendor." (`PullNotFastForward`, 4.8, D211); "In ignored"; `ErrorOutput` TG3; `ErrorCommand` `git merge --ff-only --no-overwrite-ignore FETCH_HEAD`; "Copy output" and "Close". The status bar "Pull failed". Nothing changed.<br>**After step 15:** the exact state equal to step 9's; no backup ref; no `MERGE_HEAD`, `rebase-merge/`, `rebase-apply/`; `secret.env` `LOCAL\n`; Undo `Nothing to undo`. | | | Part A: `g23a-merge-blocked`, `g23b-rebase-blocked`, `g23c-pick-blocked`, `g23d-revert-blocked`, `g23e-apply-blocked`, `g23f-pop-blocked`: each the compact error dialog ("Close" only) over the dimmed IG0: nothing selected in `g23a` and `g23b`; row 3 ("Add the vendor secret") selected in `g23c`; row 4 ("Stop tracking the secret") selected in `g23d`, `g23e` and `g23f`, since step 5's right-click (the details panel shows it). Part B: `g23g-pull-rebase-merges-blocked`, `g23h-pull-merge-blocked` (over IG1 and IG1v), `g23i-pull-not-fast-forward` (git's output and command shown; its output region depends on git's version: Conventions, "Pictures") | RW: n/a, as G22. Steps 11 and 12 are not pictured: step 11's dialog is step 10's, step 12 shows no dialog. Step 12 is a merge whose source adds nothing HEAD lacks: TR7's first read lists nothing, so 9.4's base read never runs; the merge of an ancestor that tracks the path, whose pair the base read drops (0.4's U4), is left to 9.17's `IntegratePullTests` and `IgnoredInTheWayTests` (a pull of such a source needs an upstream "ignored" does not have without a push). Design 0.4 (U5, the side branch from a commit outside the range), 9.3 to 9.10, 9.15, 4.8; D196, D211 (`PullNotFastForward`), D219 (8.9: the configuration read before the fetch), D221. Git without the check (the derivation table): step 10's `git rebase --rebase-merges` would delete `secret.env` (HEAD unchanged); step 11's plain `git rebase` would keep it, and the app refuses on purpose (9.6's last sentence); step 13's merge would overwrite it. The pull's merge, rebase and fast-forward are also 9.17's `IntegratePullTests`; the interactive rebase, the squash and "Check out and rebase" only 9.17's tests. Decisions GD5 to GD8, GD18 to GD20, GD22, GD23. |
| G24 | Keep ignored files off restores phase 5 exactly (D221, 9.1): with `"keepIgnoredFiles": false` a merge runs as git runs it (Q5), with none of 9.4's reads and no `--no-overwrite-ignore`, and overwrites the ignored `secret.env`; a checkout takes git's default and deletes the ignored folder `build/` where the target has a file; "ignored", dark, 1100×700 | (`Check_G24_keep_ignored_files_off_restores_phase_5`) 1. "Ignored" scenario (IG0); before the app starts write `settings.json` with `"keepIgnoredFiles": false`. Start. The check reads the exact state. 2. Right-click Local branches › vendor and click "Merge vendor into main". Wait for "Merge vendor" (Succeeded). Screenshot. 3. Right-click Local branches › tools and click "Check out tools". Wait for "Check out tools" (Succeeded). Screenshot. 4. The check reads HEAD, the refs, the status with `--ignored`, the files and HEAD's reflog. | **After step 1:** IG0; the settings the app read have `KeepIgnoredFiles` false.<br>**After step 2:** no question, no dialog. The call log has, in this order, `git fmt-merge-msg` (input `133a796ef1cfa3b4f2dc3237b5342751f85fcc3f\t\tbranch 'vendor' of .\n`; output `Merge branch 'vendor'`) and `git merge --no-edit --no-log -m "Merge branch 'vendor'" refs/heads/vendor` (exit 0; standard output TG9): no `--no-overwrite-ignore`, and none of 9.4's reads: no `git diff-tree … --diff-filter=A HEAD refs/heads/vendor --` and no `git merge-base --all` since the click, and no `git check-ignore` among the merge's calls (its `OperationId`, the `git merge …` record's) nor between the click and its `git merge …` (the watcher's own `check-ignore` for the files the merge wrote, outside the operation, may follow; Conventions, "The call log"): phase 5's merge (P27's command before phase 6, phase 5's `Phase5Expectations.P.MergeOtherCommand`'s form). The status bar `Merged vendor into main`. The graph IG2. Git: `main` at `63b8fe1713a49735d84bb334cf1b86c66cef8cdb` (parents `c49cac4…`, `133a796…`; Test Author, 2026-01-02 09:00 as author and committer date); `secret.env` `vendor\n` (LOCAL lost: git's default, Q5); `git status --porcelain=v1 --untracked-files=all --ignored` prints `!! build/out.bin` only; HEAD's reflog's newest entry `merge refs/heads/vendor: Merge made by the 'ort' strategy.`. Undo enabled (a merge is recorded, phase 5 design 7.2).<br>**After step 3:** no question (HEAD is on a branch), no dialog. The call log has `git switch --no-guess tools` (exit 0; standard error TG10): no `--no-overwrite-ignore`, and no `git check-ignore` among the checkout's calls (its `OperationId`, the `git switch …` record's) nor between the click and its `git switch …` (the watcher may ask about the deleted and written files after it). The status bar `Checked out tools`; `CurrentBranch` `tools`. The graph IG3. Git: HEAD `refs/heads/tools` at `c8446bb…`; the working tree `.gitignore`, `README.md`, `app.txt` (`app\n`), `build` (a file, `build\n`); `build/out.bin` gone (the ignored folder deleted, git's default) and `secret.env` gone (tracked in the merge, not in `tools`); the status prints nothing; HEAD's reflog's newest entry `checkout: moving from main to tools`.<br>**After step 4:** as after step 3; `refs/heads/main` `63b8fe1…`, no backup ref. | | | `g24a-merged-without-the-check` (IG2, the status bar `Merged vendor into main`), `g24b-tools-checked-out` (IG3, `tools` ✓ on row 3, the status bar `Checked out tools`) | RW: n/a: no new screen, the graph and status bar are earlier phases'. D179's checkout of an older commit of the current branch keeps its option and its refusal with the setting off: S21 (part S), and 9.17's `RepositoryOperationsKeepIgnoredTests` ("off, phase 5's commands and no read" for every operation of 9.2, undo's guards kept). Design 0.4, 9.1, 9.17; D193, D221; scenario facts 4.4. Decisions GD9, GD10. |
| G25 | Fetch refspecs (D222, D201, design 10): the drop's push onto a renamed remote branch pushes the branch's name on its remote and git moves the tracking ref; onto a ref one remote fetches from two refs (R2) both pushes disabled and its deletion too, while its checkout makes the branch without an upstream, saying so, and, with `main` checked out again, its double-click checks out that branch; a push of that branch to that remote refused before git (it would stop every fetch); a renamed ref's checkout disabled while HEAD is on its name, and its deletion by that name with the mapped lease and backup; a ref that is not a branch (R4): its deletion disabled, its checkout tracking it as git does, and Pull's tooltip naming that upstream by its tracking ref, as git's status does; "refspecs", dark, 1100×700 | **Part A** (`Check_G25_renamed_and_ambiguous_remote_branches`): 1. "Refspecs" scenario, nothing changed (RS0). Start. Click the folder rows Remotes › fork › up and Remotes › origin › pr to open them. 2. Drag the row Local branches › topic onto the row Remotes › fork › up › main and release. Screenshot. Click "Push topic to fork/up/main". Wait for "Push topic to fork/up/main" (Succeeded; the title, GD24). Screenshot. 3. Drag the row Local branches › topic onto the row Remotes › origin › pr › 1 and release. Screenshot. Press Esc. 4. Right-click Remotes › origin › pr › 1. Screenshot. Click "Check out origin/pr/1". Wait for "Check out origin/pr/1" (Succeeded). Screenshot. 5. Double-click Local branches › main. Wait for "Check out main" (Succeeded). 6. Double-click Remotes › origin › pr › 1. Wait for "Check out origin/pr/1" (Succeeded). 7. Right-click Local branches › pr › 1 and click "Push pr/1…". In "Push pr/1" click the remote `origin`, leave the name and "Set as upstream" as they are and click "Push". Wait for "Push pr/1" (Failed) and its dialog. Screenshot. Click "Close".<br>**Part B** (`Check_G25_a_renamed_deletion_and_a_ref_that_is_not_a_branch`, a new copy, a new start): 8. "Refspecs" scenario (RS0). Start. Click the folder row Remotes › fork › up to open it. Right-click its row `main`. Screenshot. 9. Click "Delete main on fork…". Screenshot. 10. Click "Delete on fork". Wait for "Delete main on fork" (Succeeded). 11. Click the folder row Remotes › upstream › pr to open it. Right-click its row `7`. Screenshot. Click "Check out upstream/pr/7". Wait for "Check out upstream/pr/7" (Succeeded). 12. Wait until idle (the refresh after the checkout included). Read Pull's tooltip. | **After step 1:** RS0, with Remotes › fork › up › main and Remotes › origin › pr › 1 now shown; `CurrentBranch` `main`; Undo `Nothing to undo`.<br>**After step 2, the menu:** no pill and no outline after the release; the graph's selection unchanged (the left-panel press selects nothing). Header `topic → fork/up/main`; "Push topic to fork/up/main" (highlighted); "Push topic to fork/up/main and track it"; —; "Check out topic and rebase it onto fork/up/main" (disabled: "topic is up to date with fork/up/main"); "Check out topic and rebase it onto fork/up/main interactively…"; —; "Compare topic with fork/up/main"; 180 high. Both pushes enabled: `fork/up/main` is Mapped to fork's `refs/heads/main`, a renamed branch (10.4 #5).<br>**After step 2, the push:** the operation's title `Push topic to fork/up/main` (the queue's log line `refspecs: Push topic to fork/up/main: Succeeded after …`, GD24); running "Pushing topic to fork/up/main…". The call log has `git push --progress fork refs/heads/topic:refs/heads/main` (exit 0; TG4), not phase 5's `refs/heads/topic:refs/heads/up/main` (10.4 #8). No dialog. The status bar `Pushed topic to fork/up/main`. Git: `fork.git` has `refs/heads/main` at `de4a87c…` and no `refs/heads/up/main`; `refs/remotes/fork/up/main` at `de4a87c…` (git moved it through the fetch line, scenario facts 8, item 6), and no `refs/remotes/fork/up/up/main`. The graph RS1.<br>**After step 3:** header `topic → origin/pr/1`; "Push topic to origin/pr/1" (disabled: "origin/pr/1 is fetched from more than one ref of origin"); "Push topic to origin/pr/1 and track it" (disabled: the same reason); —; "Check out topic and rebase it onto origin/pr/1" (disabled: "topic is up to date with origin/pr/1"); "Check out topic and rebase it onto origin/pr/1 interactively…" (highlighted, the first enabled item); —; "Compare topic with origin/pr/1"; 180 high. Esc closed it; no `git push` call since the step began; nothing changed.<br>**After step 4, the menu:** the graph's selection as it was: none (D114). Header `origin/pr/1`; "Check out origin/pr/1" (highlighted; enabled for R2, D201); "Merge origin/pr/1 into main"; "Merge origin/pr/1 into main (create a merge commit)"; "Rebase main onto origin/pr/1"; "Interactive rebase of main onto origin/pr/1…"; —; "Create branch here…"; "Create tag here…"; —; "Delete pr/1 on origin…" (disabled: "origin/pr/1 is fetched from more than one ref of origin"; today's label, 10.4 #6); —; "Compare origin/pr/1 with main"; "Hide origin/pr/1"; "Solo origin/pr/1"; —; "Copy branch name"; 380 high (12 items, 4 separators), inside the window.<br>**After step 4, the checkout:** no question, no dialog. The call log has `git switch --create pr/1 --no-track --no-overwrite-ignore refs/remotes/origin/pr/1` (exit 0; TG5) and no `--track`. The status bar's text, and its tooltip, `Created pr/1 at origin/pr/1 without an upstream: origin/pr/1 is fetched from more than one ref of origin` (secondary; `CreatedWithoutUpstream`), drawn cut with "…" at its 520 px (GD14); `CurrentBranch` `pr/1`. The graph RS2. Git: HEAD `refs/heads/pr/1` at `de4a87c…`; `git config --get-regexp ^branch\.pr/1\.` exit 1 (no key); `git status --porcelain=v2 --branch` prints `# branch.oid de4a87c…` and `# branch.head pr/1`, no upstream lines.<br>**After step 5:** the call log has `git switch --no-guess --no-overwrite-ignore main` (exit 0; TG6). The status bar `Checked out main`. The graph RS3.<br>**After step 6:** the first click selected the row Remotes › origin › pr › 1 and "Work on the topic" (row 0). The call log has `git switch --no-guess --no-overwrite-ignore pr/1` (exit 0; TG6's second call) and no `git switch --create` (the local branch of today's name exists, 10.4 #10, D201). The status bar `Checked out pr/1, which has no upstream` (GD21); `CurrentBranch` `pr/1`. The graph RS2. Git: HEAD `pr/1`; still no `branch.pr/1.*` key.<br>**After step 7:** the menu's "Push pr/1…" was enabled with its key `Ctrl+Shift+K` at the right (6.7: the checked-out branch's push). The push dialog: title "Push pr/1"; "To" with the radio buttons `fork` (checked), `origin`, `upstream`; `PushBranchBox` `pr/1`; "Set as upstream" ticked; "Cancel", "Push". After "Push": no `git push` call since the step began (10.4's guard, inside the operation before git, GD13). The error dialog: `ErrorTitle` "Push failed"; `ErrorSummary` "Pushing pr/1 to origin/pr/1 would stop every fetch of origin: origin/pr/1 is fetched from more than one ref of origin." (`PushWouldStopFetch`); "In refspecs"; no `ErrorOutput`, no `ErrorCommand`, "Close" only. The status bar "Push pr/1 failed" in the danger colour. Git: `origin.git` holds `refs/heads/feature/x`, `refs/heads/main` (`8394a5e…`) and `refs/pull/1/head` (`de4a87c…`) only, no `refs/heads/pr/1`; no `branch.pr/1.*` key; the refs as after step 6.<br>**After step 8:** RS0 with Remotes › fork › up › main shown; the menu: header `fork/up/main`; "Check out fork/up/main" (disabled: "main is already checked out": its name on fork is HEAD's branch, 10.4 #6, 10.6); "Merge fork/up/main into main" (highlighted); "Merge fork/up/main into main (create a merge commit)"; "Rebase main onto fork/up/main" (disabled: "main is up to date with fork/up/main"); "Interactive rebase of main onto fork/up/main…"; —; "Create branch here…"; "Create tag here…"; —; "Delete main on fork…" (danger; the branch's name on fork, not phase 5's `up/main`); —; "Compare fork/up/main with main"; "Hide fork/up/main"; "Solo fork/up/main"; —; "Copy branch name"; 380 high.<br>**After step 9:** the confirmation: `ConfirmTitle` "Delete main on fork?"; `ConfirmQuestion` "Delete the branch main on fork? Everyone who uses fork loses it."; `ConfirmDetail` "A backup is saved first, so this can be undone."; "Cancel" with the focus and "Delete on fork" (danger).<br>**After step 10:** running "Deleting main on fork…". The call log has `git push --progress --force-with-lease=refs/heads/main:8394a5e2e769c4601161cbce8c239ff2aa246eb9 fork --delete refs/heads/main` (exit 0; TG7): the lease names the branch on fork and the tracking ref's commit, read inside the operation (10.4 #9), not phase 5's `refs/heads/up/main`, which git rejects as stale (scenario facts 3.3). The status bar `Deleted main on fork`. Git: `fork.git` has no ref; `refs/remotes/fork/up/main` deleted with it; `refs/visualcommit/backup/remote-branch-20260102T090001000Z/fork/main` at `8394a5e…` (`remote-branch-<t>/<r>/<y>`, 10.4 #9); the graph RS4; Undo enabled (the deletion is recorded, 10.4 #9).<br>**After step 11, the menu:** header `upstream/pr/7`; "Check out upstream/pr/7" (highlighted; enabled: R4 keeps the checkout, 10.4 #6); "Merge upstream/pr/7 into main"; "Merge upstream/pr/7 into main (create a merge commit)"; "Rebase main onto upstream/pr/7"; "Interactive rebase of main onto upstream/pr/7…"; —; "Create branch here…"; "Create tag here…"; —; "Delete pr/7 on upstream…" (disabled: "upstream/pr/7 is refs/pull/7/head on upstream, not a branch"; today's label); —; "Compare upstream/pr/7 with main"; "Hide upstream/pr/7"; "Solo upstream/pr/7"; —; "Copy branch name"; 380 high.<br>**After step 11, the checkout:** the call log has `git switch --create pr/7 --track --no-overwrite-ignore refs/remotes/upstream/pr/7` (exit 0; TG8). The status bar `Created pr/7 tracking upstream/pr/7 and checked it out`; `CurrentBranch` `pr/7`. The graph RS5. Git: `branch.pr/7.remote` `upstream`, `branch.pr/7.merge` `refs/pull/7/head` (git tracks the ref on the remote, 0.5's P2); `git status --porcelain=v2 --branch` prints `# branch.head pr/7`, `# branch.upstream upstream/pr/7`, `# branch.ab +0 -0`.<br>**After step 12:** Pull enabled; its tooltip (`ToolTip.GetTip`, no hover) and help text `Pull from upstream/pr/7 (Ctrl+Shift+L)` (GD15: the upstream named by the remote-tracking ref the fetch lines map it to, the first of `DestinationsOf(upstream, refs/pull/7/head)` under `refs/remotes/` without `refs/remotes/`, as git's status names it), not `upstream/refs/pull/7/head`. No picture. | | | Part A: `g25a-drop-onto-renamed` (the drop menu at the release point, the left panel's `up` and `pr` folders open), `g25b-pushed-to-renamed` (RS1, the status bar `Pushed topic to fork/up/main`), `g25c-drop-onto-ambiguous`, `g25d-ambiguous-ref-menu`, `g25e-created-without-upstream` (RS2; the status bar's text cut with "…"), `g25f-push-would-stop-fetch` (the compact error dialog over RS2). Part B: `g25g-renamed-ref-menu`, `g25h-delete-renamed-question`, `g25i-not-a-branch-menu` | RW: n/a: the drop menu, the remote branch menu, the question and the error dialog are phases 3 to 5's, pictured in the real window by phase 3's check 7 and phase 5's P24; what changes is their texts and git calls (design 10.9). Design 0.5 (B1, P, P2, P3), 10.1 to 10.6; D93 (amended), D100 (the lease, by the mapping), D201, D222; scenario facts 3.2, 3.3 and 8 (items 5 to 7). Pull's tooltip for `pr/7` names its upstream `upstream/pr/7`, by its tracking ref, where today's `UpstreamText` gives `upstream/refs/pull/7/head` (scenario facts 8, item 5): asserted in step 12 without a picture (GD15, the lead's ruling 3); the push dialog's and a dashboard row's texts, which take the same name, are not asserted here. R1 and R3, B2's left panel, Force push's guard, "Set upstream…"'s list, the force-push restore's fallback and "Create branch here…" with `--no-track` are 10.9's tests only. Decisions GD11 to GD16, GD21, GD24. |
| L26 | The welcome page in both themes, following the system's change each way (11.14, 13.1; D194, D223, D227): an empty tab with a recent list and one workspace, its Workspaces section between `Init` and `Recent repositories`, the recent folders cut with "…"; the clone form, which hides the section; empty tab, System (fake dark), 1100×700 (`Check_L26_welcome_page_in_both_themes`, one start) | 1. Set-up as the methods table says (no tab; recent `graph`, `linear`; "Web" in `workspaces.json`; `"theme": "System"`). Start with the fake dark; wait until ready. `ShotInBothThemesAsync(app, "l26a-welcome")`. 2. Click `Clone` (`CloneRepoButton`); wait until `CloneUrlBox` has the focus. `ShotInBothThemesAsync(app, "l26b-clone-form")`. Click `Cancel` (`CloneCloseButton`). | **At the start:** the log has `Theme: System (the system's theme is dark).`; one tab `New tab` (no close button); the toolbar with only `Settings` and `Theme` enabled, none of the others with a tooltip; `ThemeSwitch` `IconSun`, tooltip and help text `Switch to the light theme (stops following the system)`; the status bar `No repository`.<br>**After step 1, `l26a-welcome-dark` and `-light`:** the welcome page in the graph area (x 260 to 700, y 88 to 674), its stack 392 wide (x 284 to 676), centred vertically (its centre at y 381 ± 1; nothing scrolls). Its texts in reading order (`VisibleTexts.In`): `No repository open`, `Open, clone or init a repository to see its history.`, `Open`, `Clone`, `Init`, `Workspaces`, `New workspace…`, `Web`, `10 repositories`, `Recent repositories`, `graph`, `{graph}`, `linear`, `{linear}` (`{graph}`, `{linear}` the copies' folders as the session holds them). No `WelcomeError`. The Workspaces section (13.1) lies between the buttons' row and `Recent repositories`, 78 high (52 + 26 × 1): the heading row 20 high with 16 above it, `Workspaces` (`WorkspacesHeading`, 11, SemiBold, `VcTextSecondaryBrush`) at its left and `NewWorkspaceButton` at its right (`IconPlus` 12×12, 4, `New workspace…` in `VcTextSecondaryBrush`; enabled; tooltip `Create a workspace: repositories you check and update together`); no `WorkspacesNotice`, no `NoWorkspacesText`; `WorkspaceList` with one `WorkspaceItem` (automation name `Web`), 26 high, 392 wide: `Web` (`VcTextPrimaryBrush`) at its left, `10 repositories` (`VcTextSecondaryBrush`) at its right; its tooltip `api, web-app, docs, mobile, design-system, billing, infra, scratch, …` (the first 8 folder names, then `, …`). The recent list: `RecentRepository` `graph`, then `linear`, each its name (primary) and, 12 after it, its folder (secondary), the folder cut with "…" at the item's right edge, its tooltip the whole folder (phase 1's `10a`). The left panel's sections and counts and the details panel's `Commit details` / `Select a commit to see its details.` as phase 0 gives them. Colours: the graph area `VcWindowBackgroundBrush` `#14161B` / `#FFFFFF`; `Web` contains `#E4E7EC` / `#1B1F27`; `10 repositories`, `Workspaces` and the folders contain `#9BA3B0` / `#5C6572`. No tooltip open.<br>**After step 2, `l26b-clone-form-dark` and `-light`:** `WelcomeButtons` hidden, and with it the buttons, the Workspaces section and the recent list (13.1); above the form `No repository open` and its line; the form: `Clone a repository` (heading), `Repository URL` over `CloneUrlBox` (empty, the keyboard focus), `Parent folder` over `CloneParentBox` with `Browse` (`CloneBrowseButton`) at its right, `Folder name` over `CloneNameBox`, then `Clone` (`CloneStartButton`) and `Cancel` (`CloneCloseButton`); no `CloneError`, no `CloneProgress`. The boxes `TextControlBackground` `#272C36` / `#FFFFFF` with the border `#323845` / `#D2D6DE`, the focused box's border `#7C8CFF` / `#4353D8`. After `Cancel`: the page as after step 1. | | | `l26a-welcome-dark`, `l26a-welcome-light`, `l26b-clone-form-dark`, `l26b-clone-form-light` | n/a: the real window never changes the system's theme (11.8, D224) and the fake that flips it exists only headless; the welcome page is pictured in the real window by phase 0's A to C and phase 1's `11a`, and its Workspaces section by W1's real-window steps (13.5). `l26a` varies by nature only if the Windows run shows a temporary folder's random part in a recent item (phase 1's `10a`, the same items in the same 392, does not: the cut falls before it); the pair's caret in `CloneUrlBox` may be drawn in one picture and not the other (a blinking caret). Design 11.14, 13.1; D194, D223, D227. Decisions LD1, LD2, LD3. |
| L27 | The graph, its menus, tooltips and dialogs in both themes (11.12, 11.13, 11.14; D194, D198, D225): a branch drag's pill and outline flipped mid-drag (`DragLayer`'s redraw), Esc ending it; three rows selected; a danger item under the pointer; a disabled toolbar button's and a disabled menu item's tooltips; Create branch…; the push dialog without an upstream; Manage remotes…; "ops", System (fake dark), 1100×700 (`Check_L27_graph_menus_tooltips_and_dialogs_in_both_themes`, one start) | 1. "Ops" scenario; start with the fake dark; wait for the graph. Press the left mouse button at the centre of the label `feature/alpha` of row 4 (Alpha two) and, holding it, move to the centre of the label `main` of row 3 (Main work) in 5 equal steps (`MoveMouseHeld(…, steps: 5)`, phase 3's check 7). `ShotInBothThemesAsync(app, "l27a-drag", keepPointer: true)`. Press Esc with the button still held; release it. 2. Click row 5 (Alpha one) at (484, 259); Ctrl+click row 6 (Add core) at (484, 285); Ctrl+click row 7 (Initial commit) at (484, 311). `ShotInBothThemesAsync(app, "l27b-three-selected")`. 3. Right-click the label `feature/alpha` of row 4; rest the pointer on `Delete feature/alpha…` (`RestPointerOnMenuItemAsync`). `ShotInBothThemesAsync(app, "l27c-danger-item", keepPointer: true)`. Press Esc. 4. Rest the pointer on the toolbar's `Stash` (`StashButton`); wait for its tooltip. `ShotInBothThemesAsync(app, "l27d-disabled-tooltip", keepPointer: true)`. 5. Click row 3 (Main work) at (484, 207). Right-click the label `main` of row 3; rest the pointer on `Delete main…`; wait for its tooltip. `ShotInBothThemesAsync(app, "l27e-menu-item-tooltip", keepPointer: true)`. Press Esc. 6. Click the toolbar's `Branch` (`BranchButton`); wait for the name dialog. `ShotInBothThemesAsync(app, "l27f-name-dialog")`. Press Esc. 7. Right-click the label `feature/ff` of row 2 (Fast-forward me); click `Push feature/ff…`; wait for the push dialog. `ShotInBothThemesAsync(app, "l27g-push-dialog")`. Press Esc. 8. Right-click the section header `Remotes` in the left panel; click `Manage remotes…`; wait for the remotes dialog. `ShotInBothThemesAsync(app, "l27h-remotes-dialog")`. Click `Close` (`CancelButton`). | **At the start:** OPS0, nothing selected, the details placeholder; the toolbar as "OPS0 in part L"; Undo `Nothing to undo`.<br>**In `l27a`, `l27b` and `l27d`, and in `l27e` for rows 1 and 2 only** (where no menu covers them: in `l27c` the branch menu, its left edge 2 right of the pointer on `feature/alpha` (x ≈ 310) and from y 4 to about 575, covers the nodes of rows 1 to 5 and the right part of the labels `feature/conflict` and `feature/ff`; in `l27e` `main`'s menu, from about (293, 209) to about y 650, covers the nodes of rows 4 and 5; every point below is also asserted outside the open menu's `MenuSnapshot.Bounds`): row 1's node (422, 155) `VcLane1Brush` `#3FB97F` / **`#1C8655`**, row 2's node (438, 181) `VcLane2Brush` `#E0A23B` / **`#A16A1B`** (D198's light lanes, 11.13), rows 4 and 5 (not in `l27e`) `VcLane3Brush` `#E5606B` / `#C93A46` (unchanged); the label `feature/conflict` filled `#3FB97F` / `#1C8655` and `feature/ff` `#E0A23B` / `#A16A1B`, their text `VcLabelTextBrush` `#10121A` / `#FFFFFF` (the label's bounds contain it).<br>**After step 1, `l27a-drag-dark` and `-light`:** the press selected Alpha two (row 4, `VcSelectionBrush` `#2A3150` / `#DDE1FA`; the details show `ce5ea6a` Alpha two) before the drag began; while held over `main`: a pill `feature/alpha` in the local label style at 85 % opacity, its top-left 8 right of and 8 below the pointer (`DragLayer.PillBounds`: from (299.4, 215), 18 high, the pointer on `main`'s centre (291.4, 207), phase 3's `MainLabelCentre`); sampled in its left padding, 4 px inside its left end and 6 below its top (x `PillBounds.X` + 4, y `PillBounds.Top` + 6: the pixel (303, 221)), where row 4's selected background lies under the pill (row 4 spans y 220 to 246 and its labels, the dimmed source among them, start at y 224, the row's `labelTop` of 4; the outline's outer edge ends at y 219): within ±8 per channel of `#C95967` in the dark picture (0.85 × `#E5606B` over `VcSelectionBrush` `#2A3150`) and of `#CC5361` in the light one (0.85 × `#C93A46` over `#DDE1FA`); the source label `feature/alpha` at 50 % opacity; the label `main` outlined 2 px in `VcDragTargetBrush` (`DragLayer.OutlineBounds`, the label inflated by 3, as phase 3's check 7 reads it; its corners are rounded and anti-aliased, and the pill, drawn over it, covers its bottom stroke from x 299.4 and its right stroke from y 215): the middle of its top stroke (x `OutlineBounds.Center.X`, y `OutlineBounds.Top` + 1) and of its left stroke (x `OutlineBounds.X` + 1, y `OutlineBounds.Center.Y`, phase 3's point) exactly `#7C8CFF` / `#4353D8`, and no pixel `#7C8CFF` anywhere in the light picture (the drag layer redrew on `ActualThemeVariantChanged`, 11.14's fix, `DragLayerTests` too); no menu; the selection still Alpha two alone. After the flip back the two outline points `#7C8CFF` again and the pill's point within ±8 of `#C95967`. After Esc: no pill, no outline, the source label at full opacity; after the release no menu opens (phase 3's rule); nothing changed in git.<br>**After step 2, `l27b-three-selected-dark` and `-light`:** rows 5, 6 and 7 on `VcSelectionBrush` `#2A3150` / `#DDE1FA`, no other row; the right panel `MultiSelectionView`: `MultiSelectionTitle` `3 commits selected`; `MultiSelectionList` in row order `dff8fbf` Alpha one, `8beca34` Add core, `4ef1f7c` Initial commit (the short ids in the code font, `VcTextSecondaryBrush`); `MultiSelectionHint` `Right-click a selected commit for what can be done with all of them.`; no file list (phase 3's check 11; three commits are no comparison, phase 5's D162).<br>**After step 3, `l27c-danger-item-dark` and `-light`:** the right-click selected Alpha two alone (row 4 is not in the selection, phase 3's 4b; the right panel shows `ce5ea6a`). The branch menu, 19 items and 5 separators, 571 high, flipped above the pointer and moved to stay 4 inside the window (its top at y 4 ± 2): header `feature/alpha`; `Check out feature/alpha`; `Merge feature/alpha into main`; `Merge feature/alpha into main (create a merge commit)`; `Rebase main onto feature/alpha`; `Interactive rebase of main onto feature/alpha…`; `Fast-forward feature/alpha to origin/feature/alpha` (disabled: "feature/alpha is up to date with origin/feature/alpha"); —; `Push feature/alpha to origin/feature/alpha`; `Force push feature/alpha…` (danger); —; `Create branch here…`; `Create tag here…`; `Rename feature/alpha…`; `Delete feature/alpha…` (danger, highlighted: the pointer rests on it); —; `Change upstream…`; `Stop tracking origin/feature/alpha`; —; `Compare feature/alpha with main`; `Show reflog of feature/alpha`; `Hide feature/alpha`; `Solo feature/alpha`; —; `Copy branch name`; no key column (no item runs a keyed command on this branch, 6.7). The menu's background `VcPanelBackgroundBrush` `#1A1D24` / `#F5F6F8`; `Delete feature/alpha…` on `VcHoverBackgroundBrush` `#2F3542` / `#DEE2E9`, its label still in `VcDangerBrush` (`Theme/Menus.axaml:39`): contains `#E5606B` / **`#B7323D`** (11.10: 4.57:1 on the light hover); `Force push feature/alpha…` contains `#E5606B` / `#B7323D` on the menu's background; the disabled item contains `#5F6775` / `#A1A8B3`; the header `#9BA3B0` / `#5C6572`. No tooltip (the item has none).<br>**After step 4, `l27d-disabled-tooltip-dark` and `-light`:** `Stash` disabled, its icon and label in `VcTextDisabledBrush` (contains `#5F6775` / `#A1A8B3`); its tooltip open, inside the window, the reason alone, `There are no changes to stash` (a disabled button keeps its reason, 6.8, D215), also its help text; the tooltip (11.12, Fluent's template, padding 8,5,8,7, border 1, font 12, unchanged size): background `ToolTipBackground` `#1A1D24` / `#F5F6F8` sampled 2 px inside its border, the 1-px border `ToolTipBorderBrush` `#323845` / `#D2D6DE`, the text `ToolTipForeground` `#E4E7EC` / `#1B1F27` by `TextInk` (more than 8 from Fluent's `#FFFFFF` / `#000000`); the tooltip's bounds equal in both pictures (±0.5: only colours change, 11.12); in the dark picture no pixel of Fluent's `#2B2B2B` or `#FFFFFF` inside the tooltip's rectangle, in the light one none of `#F2F2F2` or `#000000` (LD19).<br>**After step 5, `l27e-menu-item-tooltip-dark` and `-light`:** the click selected Main work alone; the right-click on its label opened `main`'s menu, 14 items and 5 separators, 441 high, its top-left 2 right of and 2 below the pointer: header `main`; `Pull from origin/main [Ctrl+Shift+L]` (highlighted); `Pull from origin/main (rebase)`; —; `Push main to origin/main [Ctrl+Shift+K]`; `Force push main…` (danger); —; `Create branch here…`; `Create tag here…`; `Rename main…`; `Delete main…` (disabled: "You cannot delete the branch you are on"); —; `Change upstream…`; `Stop tracking origin/main`; —; `Show reflog of main`; `Hide main` (disabled: "The checked-out branch is always shown"); `Solo main`; —; `Copy branch name` (phase 5's F14 menu with phase 6's keys, 6.7; the keys contain `#9BA3B0` / `#5C6572`). The pointer rests on `Delete main…`, which takes no highlight (`Pull from origin/main` stays highlighted, `#2F3542` / `#DEE2E9`); its label in `VcTextDisabledBrush`, not the danger colour (D114); its tooltip `You cannot delete the branch you are on`, open inside the window, in 11.12's colours as step 4's.<br>**After step 6, `l27f-name-dialog-dark` and `-light`:** `NameDialog` (card 400, x 350 to 750) over the backdrop: title `Create branch`; `NameDialogContext` `From 88d6b91 "Main work"`; `NameBox` empty, placeholder `Branch name`, the keyboard focus; `NameError` empty; `NameDialogOption` `Check out the new branch` ticked; `Cancel` and `Create branch` (primary, disabled) (phase 3's check 23). The card `#1A1D24` / `#F5F6F8`; the backdrop over the window, sampled at (300, 600) (in the graph area below its last row, which ends at y 324, left of the card and clear of every lane, label and row background): within ±8 of `#08090B` (0.4 × `#14161B`) / `#999999` (0.6 × `#FFFFFF`). After Esc: no dialog, no branch made.<br>**After step 7, `l27g-push-dialog-dark` and `-light`:** the right-click selected Fast-forward me; after the click `PushDialog` (card 400) over the backdrop: `PushTitle` `Push feature/ff`; `To` with `origin` as text (`PushRemote`: one remote); `PushBranchBox` `feature/ff`, the keyboard focus; `PushSetUpstream` `Set as upstream` ticked; `Cancel` and `Push` (primary, enabled) (phase 3's check 29). After Esc: no dialog; `git ls-remote origin` still lists only `refs/heads/feature/alpha` and `refs/heads/main`.<br>**After step 8, `l27h-remotes-dialog-dark` and `-light`:** the section's menu (not pictured) had `Manage remotes…`; `RemotesDialog` (card 460, x 320 to 780), titled `Remotes`: one `RemoteRow` `origin` with its URL `{ops}\origin.git` (`RemoteUrl`, cut with "…" where it does not fit, whole in its tooltip), `Edit` and `Remove` (danger: contains `#E5606B` / `#B7323D`); below it `Add remote`; `Close` (phase 3's check 22). After `Close`: no dialog. At the end the exact state equals the one read at the start; Undo `Nothing to undo`. | | | `l27a-drag-dark`, `l27a-drag-light`, `l27b-three-selected-dark`, `l27b-three-selected-light`, `l27c-danger-item-dark`, `l27c-danger-item-light`, `l27d-disabled-tooltip-dark`, `l27d-disabled-tooltip-light`, `l27e-menu-item-tooltip-dark`, `l27e-menu-item-tooltip-light`, `l27f-name-dialog-dark`, `l27f-name-dialog-light`, `l27g-push-dialog-dark`, `l27g-push-dialog-light`, `l27h-remotes-dialog-dark`, `l27h-remotes-dialog-light` | n/a: the flips need the fake (11.8, D224); the drag, the menus, the toolbar's tooltips, the name, push and remotes dialogs are pictured in the real window by phase 3's checks 7, 1, 13, 23 and 28 and by L34's light delete question. `l27h` varies by nature where the drawn URL reaches the copy's folder `ops-<12 hex>`; `l27f`'s and `l27g`'s boxes have a caret (as L26's). 16.1: these screens' earlier dark pictures change only by 11.12's tooltip colours and 7.2's toolbar box. Design 11.10 to 11.14, 6.7, 6.8; D105, D114, D194, D198, D215, D225. Decisions LD1, LD4, LD5, LD6. |
| L28 | Changes, pages and the graph's views in both themes (11.10, 11.13, 11.14; D194, D198, D225): `src/Reader.cs`'s working change (the stage panel's `M` row, the inline diff in C#'s Dark+ / Light+ colours); the restore bar after a discard, then Restore; its blame with `Not committed yet`; two commits compared; `All files`; `feature/compare` hidden and `main` soloed; "history" with `docs/todo.md`, System (fake dark), 1100×700 (`Check_L28_changes_pages_and_views_in_both_themes`, one start) | 1. "History" scenario with `docs/todo.md` (`# To do\n`) written before the start (H0t). Start with the fake dark; wait for the graph. Click the working-changes row (468, 129); click `Reader.cs` in `UnstagedFileList` (`FileRowAsync("src/Reader.cs")`); wait for the diff. `ShotInBothThemesAsync(app, "l28a-diff")`. 2. Click `Discard file` (`DiscardFileButton`); in the question click `Discard`; wait for the queue's line `history: … Succeeded …` and until idle. `ShotInBothThemesAsync(app, "l28b-restore-bar")`. Click `Restore` (`RestoreDiscardButton`); wait for the queue's line `history: Undo discard of Reader.cs: Succeeded …` and until idle. 3. Right-click `Reader.cs` in `UnstagedFileList`; click `Blame`; wait for 24 lines. `ShotInBothThemesAsync(app, "l28c-blame")`. Press Esc. 4. Click row 7 (Rename the parser to reader) at (468, 337), then Ctrl+click row 0 (Read whole lines) at (468, 155); wait until the comparison is read. `ShotInBothThemesAsync(app, "l28d-compare")`. 5. Click row 0 at (468, 155); wait for the details; click `All files` (`AllFilesButton`). `ShotInBothThemesAsync(app, "l28e-all-files")`. 6. In the left panel click the folder `feature` under Local branches, then the folder `feature` under Remotes › origin. Right-click Local branches › feature › compare; click `Hide feature/compare`; wait for the reload (`WaitForGraphReloadAsync`). Right-click `main` under Local branches; click `Solo main`; wait for the reload. `ShotInBothThemesAsync(app, "l28f-hidden")`. | **At the start:** H0t; Undo `Nothing to undo`.<br>**After step 1, `l28a-diff-dark` and `-light`:** the stage panel (`Working changes`): Unstaged files (2): `A` `todo.md` `docs`, `M` `Reader.cs` `src` (selected, `VcSelectionBrush` `#2A3150` / `#DDE1FA`); Staged files (0). `Reader.cs`'s `M` contains `VcWarningBrush` `#E0A23B` / **`#895A17`** (on the selected row: 4.59:1, 11.10); `todo.md`'s `A` contains `VcSuccessBrush` `#3FB97F` / **`#187248`**. Phase 2's diff view in the graph area: header `M` (contains `#E0A23B` / `#895A17` on the chrome `#20242C` / `#EBEDF1`) `Reader.cs` `src`, at its right `Unstaged`, the close button; toolbar `Inline` (selected) and `Side by side`, `Stage file` and `Discard file`; inline, two hunks: `@@ -1,6 +1,6 @@` with `Stage hunk` and `Discard hunk`, then context 1 `namespace History;`, 2 (empty), removed 3 `/// <summary>Reads the words of a text.</summary>`, added 3 `/// <summary>Reads the lines of a text.</summary>`, context 4 to 6; `@@ -19,4 +19,6 @@ public sealed class Reader` with its buttons, context 19 to 21, added 22 (empty) and 23 `    public int Length => _text.Length;`, context 22/24 `}` (phase 5's scenario facts 2.2). Rows: added `VcDiffAddedBrush` `#1A2E24` / `#E6F6EC`, removed `VcDiffRemovedBrush` `#331D23` / `#FBE9EB`, hunk headers `VcDiffHunkBrush` `#1C2130` / `#EEF0FB`; the changed words `words` and `lines` on `VcDiffRemovedWordBrush` `#6A2D37` / `#F3BAC1` and `VcDiffAddedWordBrush` `#2B5A3F` / `#B4E5C6`; C#'s colours (phase 2's table, D64): `public` and `int` of line 23 contain `#569CD6` / `#0000FF`, `=>` `#D4D4D4` / `#000000`.<br>**After step 2, `l28b-restore-bar-dark` and `-light`:** the question was phase 2's (`Discard changes?`, `Discard the changes to Reader.cs?`); then H0d: the diff closed (its diff became empty); the graph area shows H0t's graph (the working-changes row still at display row 0); the stage panel's restore bar (`RestoreBar`, 40 high, `VcControlBackgroundBrush` `#272C36` / `#FFFFFF`): `Discarded changes to Reader.cs.` (`RestoreBarText`), `Restore` and the close button (`DismissRestoreButton`); Unstaged files (1): `A` `todo.md` `docs`. Undo `Undo discard of Reader.cs (Ctrl+Z)`. Git as H0d (`src/Reader.cs` `7061deb`, the backup ref `discard-20260102T090001000Z` at `0bd36b6`). The graph's row 4 node (422, 259) `#3FB97F` / `#1C8655`, row 6's (422, 311) `#E0A23B` / `#A16A1B`. After `Restore`: no question (the file has not changed since the discard); the bar's Restore ran as the discard's own Undo (phase 5 design 7.9 and UD18, as phase 5's U16 step 13 measured it): the operation `Undo discard of Reader.cs`, its log line `history: Undo discard of Reader.cs: Succeeded after <N> ms: Undid discard of Reader.cs`, the status bar `Undid discard of Reader.cs`; H0r (still one backup ref); no restore bar; Unstaged files (2) as step 1; no new entry: Undo disabled, its tooltip and help text `Nothing to undo`; Redo enabled, `Redo discard of Reader.cs (Ctrl+Y)` (`Cmd+Shift+Z` on macOS; 6.8).<br>**After step 3, `l28c-blame-dark` and `-light`:** the right-click opened `Reader.cs`'s file menu (not pictured) with `File history` and `Blame`; the page `Blame` (pane `Blame of Reader.cs`): `Blame of Reader.cs` (`BlameTitle`), `in the working tree` (secondary), `File history` (`BlameHistory`), close; the gutter 130 wide (x 260 to 390), the text x 390 to 700, from y 124; 24 lines, 15 runs, each run's first line its short SHA and `2026-01-01`, runs 1 (line 3) and 13 (lines 22 to 23) `Not committed yet` in `VcWarningBrush`: contains `#E0A23B` / **`#895A17`**, no SHA or date (phase 5's B6, BD11, BD12); the runs' backgrounds alternating `VcPanelBackgroundBrush` `#1A1D24` / `#F5F6F8` (runs 0, 2, …, 14) and `VcWindowBackgroundBrush` `#14161B` / `#FFFFFF` (runs 1, 3, …, 13); the text in C#'s colours (`public` of line 4 contains `#569CD6` / `#0000FF`). The right panel still the stage panel. After Esc: no page; the graph H0t.<br>**After step 4, `l28d-compare-dark` and `-light`:** rows 7 and 0 on `VcSelectionBrush`; the right panel the comparison (phase 5's F12 step 1): `Compare` and the swap button, `From 0575c7f Rename the parser to reader`, `To c0bc2dd Read whole lines` (links enabled), `Changed files (4)` with `Flat` (selected) and `Tree`: `M` `README.md`; `M` `notes.md` `docs`; `M` `Config.cs` `src`; `M` `Reader.cs` `src`, each `M` containing `#E0A23B` / `#895A17`. The graph's nodes of colours 1 and 2 as step 2's.<br>**After step 5, `l28e-all-files-dark` and `-light`:** row 0 alone selected; the details of `c0bc2dd` Read whole lines; `All files` with its `selected` class; `All files (6)`, flat, in path order: `LICENSE`, `README.md`, `logo.png` `assets`, `notes.md` `docs`, `Config.cs` `src`, `M` `Reader.cs` `src` (only Reader.cs has a letter, phase 5's B7 step 2); `settings.json` gains `"showAllFiles": true`.<br>**After step 6, `l28f-hidden-dark` and `-light`:** HS1 (12 commit rows below the working-changes row, 2 lanes; Compare whole lines and Add the comparer gone; row 0 `main` ✓ and `+1`); the left panel as HS1 gives it: Local branches › feature › compare and Remotes › origin › feature › compare in `VcTextDisabledBrush` (contain `#5F6775` / `#A1A8B3`), each with `RefHiddenToggle` (`IconEyeOff`, 18×18) at its right; `main` (Local branches) in `VcAccentBrush` (contains `#7C8CFF` / `#4353D8`) and Remotes › origin › main, each with `RefSoloToggle` (`IconSolo`, `#7C8CFF` / `#4353D8`); the call log has 8.2's hide form, then its solo form, both exit 0; `GraphReloading` hidden; the session's `GraphFilters` as HS1; the right panel still `c0bc2dd` with `All files (6)` (the selection kept by SHA). Row 4's node (422, 259) `#3FB97F` / `#1C8655`. Git: refs and `.git/config` unchanged; at the end ` M src/Reader.cs`, `?? docs/todo.md` and the one backup ref. | | | `l28a-diff-dark`, `l28a-diff-light`, `l28b-restore-bar-dark`, `l28b-restore-bar-light`, `l28c-blame-dark`, `l28c-blame-light`, `l28d-compare-dark`, `l28d-compare-light`, `l28e-all-files-dark`, `l28e-all-files-light`, `l28f-hidden-dark`, `l28f-hidden-light` | n/a: the flips need the fake (11.8); the stage panel, the diff and the restore bar are pictured in the real window by phase 2's checks, the blame by phase 5's B6, a comparison and the filters by phase 5's part F and B real-window steps, all dark, and the light colours by `ThemeTokensTests`. Nothing here varies by nature: no temporary folder is drawn and the blame shows no date for the uncommitted lines. 11.14's table: these pictures answer the phase 5 handoff's open point on the sweep for the blame and the comparison. Design 11.10, 11.13, 11.14; phase 5 design 4.2, 4.5, 6, 7.9 (the bar's Restore as the discard's Undo, with UD18 and U16 step 13), 8; D62, D73, D159, D162, D172, D194, D198, D225. Decisions LD1, LD7, LD8, LD9, LD10. |
| L29 | The reflog and backups pages in both themes (11.10, 11.14; D194, D225): HEAD's reflog with its lost marks and footer; the backups list; "reflog", System (fake dark), 1100×700 (`Check_L29_reflog_and_backups_in_both_themes`, one start) | 1. "Reflog" scenario; start with the fake dark; wait for the graph and for the background `Clean up backups` record (`Removed 2 backups`); read `ExactStateAsync`, the check's baseline, before any input. Right-click the section header `Local branches`; click `Show reflog`; wait for 36 rows and their lost marks. `ShotInBothThemesAsync(app, "l29a-reflog")`. 2. Press Esc. Right-click `Local branches`; click `Show backups`; wait for 15 rows. `ShotInBothThemesAsync(app, "l29b-backups")`. Press Esc. | **At the start:** RL0, nothing selected; 15 backups left (the log's two `reflog: Removed backup … (older than 30 days)` lines).<br>**After step 1, `l29a-reflog-dark` and `-light`:** the section's menu (not pictured; phase 6 keys `Create branch… [Ctrl+B]` and `Show reflog [Ctrl+Shift+R]`, 15.1) closed; the page `Reflog` (pane `Reflog of HEAD`): header (`VcChromeBackgroundBrush` `#20242C` / `#EBEDF1`) with `ReflogSource` `HEAD` and its chevron and `ReflogClose`; the list y 124 to 648, 36 rows newest first (`HEAD@{0}` … `HEAD@{36}`, no `HEAD@{5}`), rows 0 to 19 whole in view, the columns selector (x 268 to 340, code font, secondary), action (x 348 to 496, cut with "…" where it does not fit), commit (x 504 to 664), lost mark (x 672 to 692), the time hidden (phase 5's B8 step 2, BD3); `IconAlert` on `HEAD@{1}` (y 163), `HEAD@{9}` (y 345), `HEAD@{11}` (y 397) and `HEAD@{17}` (y 553), each containing `VcWarningBrush` `#E0A23B` / **`#895A17`** (11.10), on no other row in view; the footer `1 entry is not shown: it names no commit that exists` (y 648 to 674). No row selected; the right panel the placeholder.<br>**After step 2, `l29b-backups-dark` and `-light`:** the page `Reflog` (pane `Backups`), `ReflogSource` `Backups`; 15 rows as phase 5's scenario facts 4.3 give them, newest first, each its time (x 268 to 404), its words (x 412 to 524) and its commit (x 532 to 692), cut with "…" where they do not fit; row 14 (`abort-20260101T120601000Z`) at y 501, all 15 in view; no footer, no lost marks (phase 5's B9 step 1). The list's background `#14161B` / `#FFFFFF`, the header's `#20242C` / `#EBEDF1`; each row's words contain `VcTextPrimaryBrush` `#E4E7EC` / `#1B1F27`. After Esc: no page. Git unchanged throughout: `ExactStateAsync` at the end equals the baseline read after the `Clean up backups` record and before the first input (one read before the start would still hold the two backup refs the clean-up at opening deletes, and `ExactStateAsync` includes `for-each-ref`). | | | `l29a-reflog-dark`, `l29a-reflog-light`, `l29b-backups-dark`, `l29b-backups-light` | n/a: the flips need the fake (11.8); the pages are pictured dark in the real window by phase 5's B8 and B9 real-window steps. 11.14: the pages answer the phase 5 handoff's open point (pictured only dark). Design 11.10, 11.14; phase 5 design 5, 12.3; D194, D225. Decisions LD1, LD11. |
| L30 | The dashboard in both themes (2.5 to 2.7, 1.9, 11.14, 13.2; D194, D204, D206, D225): every row's state; a row's menu; the scan's checklist, its folder line drawn by `PathTextBlock`; "Web", System (fake dark), 1100×700 (`Check_L30_dashboard_in_both_themes`, one start) | 1. A fresh "Web" copy; `OpenWorkspaceAsync(data, copy, 1100, 700, AppTheme.System)` (the fake dark), which waits for the first read (`WaitForDashboardReadAsync`). `ShotInBothThemesAsync(app, "l30a-dashboard")`. 2. Right-click old-site's row (row 9) at (66, 399). `ShotInBothThemesAsync(app, "l30b-row-menu", keepPointer: true)`. Press Esc. 3. Let the fake folder dialog answer `copy.Root` (`app.Folders.Answer(copy.Root)`); click `Add repository…` (`DashboardAddRepository`); wait until `ScanFolderText` reads `Found 9 repositories in {root}.`. `ShotInBothThemesAsync(app, "l30c-scan-checklist")`. Press Esc. | **After step 1, `l30a-dashboard-dark` and `-light`:** one tab, the dashboard `Web` (`DashboardTab`, active, `TabIcon` in `VcTextPrimaryBrush`); the dashboard across the main area (x 0 to 1100), the side panels hidden. Header (y 88 to 124, `VcChromeBackgroundBrush` `#20242C` / `#EBEDF1`): `IconWorkspace` 14×14 at x 8, `Web` (13, SemiBold, primary), `10 repositories` (secondary); at the right, from x 1092 leftwards 6 apart, `DashboardMore` (tooltip `More`), `Refresh` (tooltip `Read the repositories of Web again (F5)`), `Pull all`, `Fetch all` (enabled, the toolbar's tooltips with their keys), `Add repository…` (tooltip `Add a repository to Web`); no run text, no bar, no `Cancel all`; no `DashboardNotice`. Column headings (y 124 to 152): `Repository`, `Branch`, `Ahead / behind`, `Changes`, `State`, `Operation` (Folder hidden at 1100, 2.6). Rows (26 high from y 152, row k's centre y 165 + 26 k): scenario facts 2.3's ten, texts, tones and tooltips exactly, no row selected, every text whole (2.6), no `Reading…`. Tones: `1 conflict` (billing) contains `VcDangerBrush` `#E5606B` / **`#B7323D`**; `Merge in progress` (billing) and `Folder not found` (old-site) contain `VcWarningBrush` `#E0A23B` / **`#895A17`**; `Up to date`, `No upstream`, `No remote` and the arrows (`IconArrowUp`, `IconArrowDown` 10×10) contain `#9BA3B0` / `#5C6572`; the repository names contain `#E4E7EC` / `#1B1F27`; the list's background `#14161B` / `#FFFFFF`. The toolbar: `Fetch` (tooltip `Fetch every repository of Web (Ctrl+Shift+F)`) and `Pull` (`Pull every repository of Web (Ctrl+Shift+L)`) enabled; Push, Branch, Stash, Pop, Undo, Redo and Search disabled without a tooltip; Settings and Theme enabled. The status bar: `StatusWorkspaceIcon` and `Web`, `Ready` (secondary); `ActivityLogToggle` disabled with `Select a repository to see its activity`.<br>**After step 2, `l30b-row-menu-dark` and `-light`:** old-site's row selected (`VcSelectionBrush` `#2A3150` / `#DDE1FA`); its menu, 9 items and 3 separators, 293 high, its top-left at (68, 401): header `old-site` (tooltip `{root}\old-site`); `Open` (disabled: "{root}\old-site does not exist"); —; `Fetch`, `Pull` (disabled, the same reason); —; `Show in folder` (disabled, the same); `Copy path` (highlighted: the first enabled item); —; `Move up`; `Move down` (disabled: "It is the last repository"); `Locate…`; `Remove from workspace` (scenario facts 2.3). Disabled items contain `#5F6775` / `#A1A8B3`; the highlight `#2F3542` / `#DEE2E9`. The log has `Web: Context menu opened: old-site by pointer` (2.7). `ActivityLogToggle`'s reason `{root}\old-site does not exist`.<br>**After step 3, `l30c-scan-checklist-dark` and `-light`:** the fake folder dialog was asked with the title `Add a repository to Web`; `AddRepositoriesDialog` (wide card, 460, x 320 to 780, about 400 high): `Add repositories to Web` (`AddRepositoriesTitle`); `ScanFolderText` `Found 9 repositories in {root}.` (`{root}` = `copy.Root` as the dialog answered it), a `PathTextBlock`: every drawn line but the last ends with `\` or `/` or a space, no line is `C:` or ends right after `C:`, no folder name of `{root}` is split across lines, the drawn lines joined, U+FEFF and U+200B removed, equal its `Text` (13.2's rule, `DrawnLines.Of`); `ScanProgressText` hidden; `FoundList`: `api`, `billing`, `design-system`, `docs`, `infra`, `mobile`, `notes`, `scratch` in view and `web-app` below (it scrolls by one), each `FoundRepositoryCheck` checked and disabled, its name (primary) and `Already in this workspace` (secondary); no `ScanStoppedText`, no `ScanFailedText`; `Cancel` and `Add repositories` (disabled: no row checked and enabled, 1.9). After Esc: no dialog; `workspaces.json` byte-identical; ten rows. | | | `l30a-dashboard-dark`, `l30a-dashboard-light`, `l30b-row-menu-dark`, `l30b-row-menu-light`, `l30c-scan-checklist-dark`, `l30c-scan-checklist-light` | n/a: W2's real-window steps picture the rows and a row menu, W1's the checklist (dark); the flips need the fake (11.8). `l30c` varies by nature: its folder line shows the copy's temporary folder (17.4); `l30b`'s header tooltip is not drawn. Design 1.9, 2.5 to 2.7, 11.10, 11.14, 13.2; D194, D204, D206, D225, D227. Decisions LD1, LD12, LD13. |
| L31 | Overlays and phase 6's screens in both themes (5.3, 7.1 to 7.4, 7.7, 8.10, 11.7, 11.11, 11.12, 11.14; D194, D200, D202, D215, D225): the activity log open; the deletion question with the danger button's `VcDangerTextBrush`; the palette; the settings screen's General with System checked; its Git section on `All repositories (global)` with the merge tool's Pick dialog stacked; an enabled tooltip with its key; Light saved and the screen opened again (L34's twin); the sign-in prompt and the status bar during a held fetch with a waiting operation; "ops" and "auth", System (fake dark), 1100×700 (`Check_L31_overlays_and_settings_in_both_themes`, `Check_L31_sign_in_and_held_fetch_in_both_themes`: one start each) | **Part A** (`Check_L31_overlays_and_settings_in_both_themes`): 1. "Ops" scenario; start with the fake dark; wait for the graph. Click the toolbar's `Fetch` (`FetchButton`); wait for the line `ops: Fetch: Succeeded after <N> ms: Fetched from all remotes` and until idle. Click `ActivityLogToggle`. `ShotInBothThemesAsync(app, "l31a-activity-log")`. Click `ActivityLogToggle`. 2. In the left panel click the folder `feature` under Local branches; right-click its `alpha`; click `Delete feature/alpha…`; wait for the question. `ShotInBothThemesAsync(app, "l31b-delete-question")`. Click `Cancel`. 3. Move the pointer to the neutral point (the click on `Cancel` left it inside the card, over where the palette's list opens, whose rows the pointer highlights, 5.8). Press Ctrl+Shift+P; wait for the palette. `ShotInBothThemesAsync(app, "l31c-palette")`. Press Esc. 4. Click `SettingsButton`; wait for the settings screen. `ShotInBothThemesAsync(app, "l31d-settings-system")`. 5. Click `SettingsNavGit`; wait until `GitReading` is gone. Click `MergeToolChooseButton`; wait for the Pick dialog. `ShotInBothThemesAsync(app, "l31e-settings-git")`. Press Esc; press Esc. 6. Rest the pointer on `FetchButton`; wait for its tooltip. `ShotInBothThemesAsync(app, "l31f-enabled-tooltip", keepPointer: true)`. 7. Move the pointer to the neutral point. Press Ctrl+,; on General click `SettingsThemeLight`; click `Save`; wait until the dialog is closed and the window shows light. Press Ctrl+,; wait for the settings screen. `Shot(app, "l31d-settings-light")`. Press Esc.<br>**Part B** (`Check_L31_sign_in_and_held_fetch_in_both_themes`, a new start): 8. The server and "auth" as the methods table says; start with the fake dark; wait for the graph. Call `server.HoldNextFetchPack(60)`. Right-click `origin` under Remotes; click `Fetch origin`; wait for the credentials dialog. `ShotInBothThemesAsync(app, "l31g-credentials")`. 9. Type `alice` into `CredentialUsername` and `s3cret` into `CredentialPassword`; click `Sign in`. Wait until the hold's task has completed and the status bar shows `Fetching from origin… Counting objects 60%`. Click the toolbar's `Fetch`; wait until `OperationWaiting` reads `1 waiting`. `ShotInBothThemesAsync(app, "l31h-operation-held")`. 10. Start the status bar's recorder (`app.RecordStatusTexts()`, `ShellDriver.Checks.cs`'s `StatusTextRecorder`, 16.1). Click `OperationCancelButton`; wait for the line `auth: Fetch origin: Cancelled after`. Wait for the credentials dialog of the Fetch that waited; click its `Cancel`; wait until idle. Call `server.Release()`. | **After step 1, `l31a-activity-log-dark` and `-light`:** the call log has `git fetch --progress origin` (exit 0, no output; D109's one remote, the last: no `--no-auto-maintenance`); the status bar `Fetched from all remotes`; refs unchanged. The activity log open at its default height (part A of phase 5): the panel y 474 to 674, the splitter y 469 to 474 with its 1-px `VcBorderBrush` line, the graph area's content y 88 to 469 (OPS0's 8 rows whole); header `Activity log`, `ops` (secondary), `Show reads` (unchecked), the close button; one row: `IconCheck` in `VcSuccessBrush` (contains `#3FB97F` / **`#187248`**), `09:00:00`, `Fetch`, `Fetched from all remotes` (secondary), `0 ms`, closed (its chevron `IconChevronRight`). `ActivityLogToggle` with its `selected` class (background `VcControlBackgroundBrush` `#272C36` / `#FFFFFF` at (1008, 678)), tooltip and help text `Hide the activity log (Ctrl+J)` (6.8). After the toggle again: the panel closed, the graph area as OPS0.<br>**After step 2, `l31b-delete-question-dark` and `-light`:** the folder `feature` open (`alpha`, `conflict`, `ff`); the right-click on `alpha` selected nothing (D114) and opened the menu of L27's step 3 (571 high, `Delete feature/alpha…` (danger)); the graph without a selection; `ConfirmationDialog` (card 400, x 350 to 750) over the backdrop: `ConfirmTitle` `Delete branch?`; `ConfirmQuestion` `Delete the branch feature/alpha? It has commits that main does not have.`; `ConfirmDetail` `A backup is saved first, so this can be undone.`; `Cancel` (the keyboard focus) and `Delete` (`VcDangerButton`): its background `VcDangerBrush` `#E5606B` / **`#B7323D`** sampled 4 px inside its left edge at its vertical centre, its label `Delete` in `VcDangerTextBrush` (the label's bounds contain `#FFFFFF` in both pictures, 11.11; 5.94:1 on the light background, the dark label's 3.38:1 left to phase 8, D198). The button's `Foreground` is the resource `VcDangerTextBrush`, `#FFFFFF` in each variant (`Application.Current.TryGetResource`). After Cancel: no dialog; `feature/alpha` at `ce5ea6a`; no backup ref; Undo `Nothing to undo`.<br>**After step 3, `l31c-palette-dark` and `-light`:** the log has `ops: Command palette opened` (Debug); `CommandPalette` without dimming (the graph area behind at `#14161B` / `#FFFFFF`); `CommandPaletteCard` 600 wide, x 250 to 850, y 96 to 488 (12 rows), `VcPanelBackgroundBrush` `#1A1D24` / `#F5F6F8`, 1-px `VcBorderBrush`, corner 8; `CommandPaletteBox` (582 × 32, x 259 to 841, y 105 to 137) empty with the keyboard focus, placeholder `Search commands, branches, tags, stashes, remotes, repositories and workspaces` (secondary); `CommandPaletteList` rows from y 143 (row k's centre y 157 + 28 k), the commands of 5.4 for "ops" in the catalogue's order (nothing run this session), the first 12 in view: `Fetch all remotes` `Ctrl+Shift+F` (highlighted, on `VcSelectionBrush` `#2A3150` / `#DDE1FA`); `Pull` `Ctrl+Shift+L`; `Push` `Ctrl+Shift+K`; `Create branch…` `Ctrl+B`; `Stash changes…` disabled, its reason `There are no changes to stash` in the detail's place, `Ctrl+Shift+S`; `Pop the newest stash` `Ctrl+Shift+O`; `Undo` disabled, `Nothing to undo`, `Ctrl+Z`; `Redo` disabled, `Nothing to redo`, `Ctrl+Y`; `Search commits` `Ctrl+F`; `Go to the commit message` disabled, `There are no changes to commit`, `Ctrl+Shift+M`; `Show reflog` `Ctrl+Shift+R`; `Show backups` (no key); the list scrolls to the rest (`Refresh` … `Use the dark theme`, `Use the light theme`; no `Follow the system's theme`, which is chosen, 11.7). No detail on the commands (`NamesRepositoryInDialogs` false). Labels contain `#E4E7EC` / `#1B1F27`, keys `#9BA3B0` / `#5C6572`, disabled labels, reasons and keys `#5F6775` / `#A1A8B3` (5.3, 5.7). After Esc: no palette; nothing ran.<br>**After step 4, `l31d-settings-system-dark` and `-light`:** `SettingsDialog` over the backdrop; the card x 120 to 980, y 40 to 660 (7.1); `Settings` (`SettingsTitle`); `SettingsNav` (x 140 to 316): `General` (selected, the keyboard focus), `Git`, `Tools`, `Keyboard shortcuts`, no error dot; General (7.3), every row in view without scrolling: `Theme`: `Dark`, `Light` unchecked, `System` checked (`SettingsThemeSystem`), under them `System follows your operating system's light or dark mode.`; `Fetching`: `AutoFetchCheckBox` checked `Fetch in the background every`, `AutoFetchMinutesBox` `5`, `minutes`, `Only the active tab's repository; it never asks for a password.`, no `AutoFetchError`; `Working tree`: `KeepIgnoredCheckBox` checked `Keep ignored files` with its second line `A checkout, merge, rebase, cherry-pick, revert, stash apply or stash pop that would overwrite or delete an ignored file does not start. Off: they run as git runs them, which can overwrite or delete such a file without a word; checking out an older commit of the current branch, Undo and Redo still keep it.`; `StashUntrackedCheckBox` checked `Include untracked files when stashing` with `The Stash changes dialog starts with this; its own check box changes it too.`; `Opening files`: `Open file asks first before it opens a file with one of these extensions:`, `RunnableList` `.exe .bat .cmd .com .ps1 .vbs .vbe .js .jse .wsf .hta .msi .scr .pif .cpl .lnk .url .reg .jar .app .command .sh .desktop` (code font), on Windows `RunnablePathExt` `From PATHEXT: .wsh .msc` (12.9's fixed `PATHEXT`; hidden elsewhere), `Also ask for` with `RunnableExtraBox` empty (placeholder `.py .pl`), no `RunnableError`, `On Linux and macOS it also asks before it opens a file marked as a program.`; buttons `Cancel` and `Save` (`VcPrimaryButton`, `VcAccentBrush` `#7C8CFF` / `#4353D8`). The card `#1A1D24` / `#F5F6F8`; labels contain `#9BA3B0` / `#5C6572`.<br>**After step 5, `l31e-settings-git-dark` and `-light`:** before the click the Git section (7.4) on `All repositories (global)` (D200): `GitScopeGlobal` selected, `GitScopeRepository` `This repository` enabled (tooltip `ops — {folder}`, `{folder}` the copy's working tree, 7.4's `{name} — {folder}`); `GitScopeCaption` `Saved in your global git configuration, which git in a terminal uses too.` (tooltip and help text the session's `<data>/harness/global.gitconfig`, 8.12); `Name` and `Email` empty with the placeholder `Not set`, no origin, error or warning lines; no `IdentityInEffect` (global scope); `Pull`: `Not set` checked, `Merge (fast-forward when possible)`, `Merge, always with a merge commit`, `Rebase`, `Rebase, keeping merge commits`, `Fast-forward only` unchecked, no `PullOther`, no `PullNotices`; `Merge tool`: `MergeToolValue` `Not set`, `Choose…` enabled at once (the fixed catalogue, no kept list, D202, 12.9), `MergeToolNote` `Used by Merge tool… in the conflict resolver, and by git mergetool in a terminal.`, no `MergeToolStatus`, no notices. The picture: the Pick dialog stacked over it (D104), `PickDialog` (wide card, 460, x 320 to 780): `Choose a merge tool` (`PickTitle`); `PickFilter` (placeholder `Filter`) with the keyboard focus; `PickList` (text — detail): `Not set` — `Merge tool… stays disabled` (selected); `kdiff3` — `found on this computer`; `meld` — `found on this computer`; `araxis` — `not found on this computer`; `bc` — `not found on this computer`; `p4merge` — `not found on this computer`; `winmerge` — `not found on this computer`; no `vimdiff` (8.10's filter); details in `VcTextSecondaryBrush` (contain `#9BA3B0` / `#5C6572`), 12 after the text; `Cancel` and `Choose`. The settings card behind the Pick dialog's backdrop. After the two Escs: no dialog; `settings.json` unchanged; the session's global file still absent (nothing written); no git write in the call log.<br>**After step 6, `l31f-enabled-tooltip-dark` and `-light`:** `FetchButton`'s tooltip open: `Fetch all remotes (Ctrl+Shift+F)` (6.8, D215; macOS `Cmd+Shift+F`), its help text the same, its `AcceleratorKey` `Ctrl+Shift+F`; the tooltip in 11.12's colours as L27's step 4.<br>**After step 7, `l31d-settings-light` (a single picture):** the first Save: `settings.json` `"theme": "Light"`; the log `Theme changed to Light.`; the window light while the fake is still dark (`ShowsDarkTheme` false; Light ignores the system, 11.4); `ThemeSwitch` `IconMoon`, tooltip and help text `Switch to the dark theme` (11.6). The screen opened again by its key on General: as step 4's light picture but `SettingsThemeLight` checked and `SettingsThemeSystem` unchecked. After Esc: no dialog. Git unchanged throughout (`ExactStateAsync`).<br>**After step 8, `l31g-credentials-dark` and `-light`:** `CredentialsDialog` (card 400) over the backdrop: `Sign in to 127.0.0.1:47394`; `CredentialsText` `Git needs a user name and a password or token for http://127.0.0.1:47394.` (both through `Phase6Expectations.WithPort`, 17.2); `User name` over `CredentialUsername` (empty, the keyboard focus), `Password` over `CredentialPassword` (empty); `Cancel` and `Sign in` (disabled while the user name is empty); no repository line (one tab, no dashboard: `NamesRepositoryInDialogs` false, 3.8). Behind the backdrop the status bar `Fetching from origin…` with its bar and `Cancel`. `server.Requests`: one `GET /remote.git/info/refs?service=git-upload-pack` without a user, answered 401.<br>**After step 9, `l31h-operation-held-dark` and `-light`:** no second dialog (the password prompt answered from the kept answer, phase 3 design 3.2); `server.Requests` then `GET …/info/refs` (alice, 200), `POST …/git-upload-pack` `ls-refs` (alice, 200), `POST …/git-upload-pack` `fetch` (alice, held). The status bar (`VcChromeBackgroundBrush` `#20242C` / `#EBEDF1`): `Fetching from origin… Counting objects 60%` (secondary: contains `#9BA3B0` / `#5C6572`), the determinate bar at 60 %, `Cancel` (`OperationCancelButton`, tooltip `Stop git`), `OperationWaiting` `1 waiting`; `OperationStatus.Waiting` 1; the toolbar's Fetch queued, not started (no second `info/refs` request). No ref moved; no `*.lock` under `.git`.<br>**After step 10:** the recorder's texts have `Fetch origin cancelled` before `Fetching from all remotes…` (the first lasts only until the waiting Fetch starts, so it is read from the recorder, not waited for) and end with `Fetch cancelled`: the waiting Fetch ran and asked: the credentials dialog of step 8 again (nothing is stored: no credential helper); after its `Cancel`: no dialog and no error dialog; the status bar `Fetch cancelled` (secondary); AUTH0's end state (refs, status, no lock file); neither `s3cret` nor `alice`'s password in any log line or call record. | | | `l31a-activity-log-dark`, `l31a-activity-log-light`, `l31b-delete-question-dark`, `l31b-delete-question-light`, `l31c-palette-dark`, `l31c-palette-light`, `l31d-settings-system-dark`, `l31d-settings-system-light`, `l31e-settings-git-dark`, `l31e-settings-git-light`, `l31f-enabled-tooltip-dark`, `l31f-enabled-tooltip-light`, `l31d-settings-light`, `l31g-credentials-dark`, `l31g-credentials-light`, `l31h-operation-held-dark`, `l31h-operation-held-light` | RW: L34, whose four captures have `l31a-activity-log-light`, `l31b-delete-question-light`, `l31c-palette-light` and `l31d-settings-light` as their twins (System's light draws Light's pixels, `System_and_Light_draw_the_same_pixels`, 11.15); part B is scripted only (phase 3's checks 17, 21 and 31 picture the prompt and the held fetch in the real window, dark). Varies by nature: `l31c`'s, `l31e`'s (`PickFilter`) and `l31g`'s focused boxes (a blinking caret, drawn in one picture of a pair and not the other); `l31g`'s status bar bar behind the backdrop while it is indeterminate; nothing else (the port is 47394 unless the server fell back, which the report then says, D97). Design 2.9, 3.8, 5.3, 5.4, 5.7, 6.1, 6.8, 7.1 to 7.4, 7.7, 7.8, 8.10, 11.4 to 11.7, 11.11, 11.12, 11.14, 12.9; D97, D150, D194, D200, D202, D212, D215, D216, D223, D225. Decisions LD1, LD14, LD15, LD16, LD17, LD18, LD19. |
| L32 | The long folder (13.2, D227; plan.md's "From phase 1"): the welcome page's error for a plain folder and the clone form's error for a missing URL, each drawn line breaking only after a separator or at a space, nothing cut off, the texts unchanged; both themes; empty tab, System (fake dark), 1100×700 (`Check_L32_long_folder_in_both_themes`, one start) | 1. Set-up as the methods table says; start with the fake dark; wait until ready. Let the fake folder dialog answer `{plain}`; click `Open`; wait until `WelcomeError` shows. `ShotInBothThemesAsync(app, "l32a-not-a-repository")`. 2. Click `Clone`; type the URL `new Uri(clones.Combine("does-not-exist")).AbsoluteUri` into `CloneUrlBox`, `{clones}` into `CloneParentBox` and `cloned` into `CloneNameBox` (as phase 1's `TypeCloneFormAsync` does, a private helper of phase 1's project, copied into `Phase6Checks.Support.cs`); click `Clone` (`CloneStartButton`); wait until the form is no longer cloning and `CloneError` shows. `ShotInBothThemesAsync(app, "l32b-clone-failed")`. | **After step 1, `l32a-not-a-repository-dark` and `-light`:** `WelcomeError` (a `PathTextBlock`, `TextBlock.error`, centred, wrapping) under the buttons' row and above the Workspaces section (`No workspaces`): its `Text`, its automation name and what tests read are exactly `{plain} is not a git repository.` (no mark in them); its drawn lines (`DrawnLines.Of`): every line but the last ends with `\` (or `/` off Windows) or a space; no line is `C:` or ends with `C:` (U+FEFF joins `C`, `:` and the separator); no folder name of `{plain}` is split across lines (`plain-<12 hex>` whole on one line); the lines joined, U+FEFF and U+200B removed, equal `Text`; nothing is cut off (the block's desired size within its bounds, `AssertNoClippedText`). On the development machine (`C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests\plain-<12 hex>`, 392 px) that gives two lines, `C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests\` and `plain-<12 hex> is not a git repository.` (13.2's case, 16.1's `11a`; the inspection checks it, the assertion is the rule above, the folder differing per machine). The text contains `VcDangerBrush` `#E5606B` / **`#B7323D`** (11.10). The tab still `New tab`.<br>**After step 2, `l32b-clone-failed-dark` and `-light`:** the clone form filled in and enabled (`CloneUrlBox` holds the URL); `CloneError` (a `PathTextBlock`, `TextBlock.error`, left-aligned, wrapping): its `Text` equals what `Phase6Checks.Support.cs`'s `CloneErrorTextAsync(url, clones)` returned before the app started: it runs `git clone --progress -- <url> cloned` in `{clones}` with the tests' git, in the isolated environment the app's calls get, asserts that the call failed and left no folder `cloned`, and returns `GitOutputCleaner.Clean(standardError, standardOutput)`, the app's own cleaning (`GitRepository.Clone.cs:81`), trimmed as `WelcomeViewModel` trims it (phase 1's `GitCloneErrorAsync` returns only the first `fatal:` line, which phase 1's check 14 asserts with `Assert.Contains`, and is private to phase 1's project); on 2.36 and 2.55 that is the four lines `fatal: '{path}' does not appear to be a git repository` / `fatal: Could not read from remote repository.` / `Please make sure you have the correct access rights` / `and the repository exists.` (`{path}` the URL's path, `C:/Users/…/clones-<12 hex>/does-not-exist` on Windows); its drawn lines: every line that does not end one of the text's own lines ends with `/` or a space, no line ends with `C:` or `'C:`, no folder name of the path is split, the lines joined (marks removed, the text's line feeds between them) equal `Text`; nothing cut off; contains `#E5606B` / `#B7323D`. No folder `{clones}\cloned`. | | | `l32a-not-a-repository-dark`, `l32a-not-a-repository-light`, `l32b-clone-failed-dark`, `l32b-clone-failed-light` | n/a: the drawn lines are the layout's (`TextLayout`), which UI Automation does not expose, and the folder differs per machine; phase 1's real-window `11a` (inspected by eye) shows `WelcomeError` in the real window and changes with this (16.1). Both pairs vary by nature: they show the temporary folders `plain-<12 hex>` and `clones-<12 hex>`. Design 13.2, 13.5; D227; plan.md's phase 6 "From phase 1" item. Decisions LD1, LD20. |
| L33 | The tab strip shrinks, then scrolls (13.3, 13.4; D187, D205, D228): 8 tabs of 120 at 1000 wide in today's places; the Web dashboard, its nine repositories that can open and one new tab at 1100: six of 97, five of 96, "+" at the end; a twelfth tab: 89 or 88, no scrolling; from 14 tabs 80 each, scrolled with the active tab whole; Ctrl+Tab round to the first brings it into view; "Web", Dark, 1000×700 then 1100×700 (`Check_L33_tab_strip_shrinks_then_scrolls`, one start) | 1. Set-up as the methods table says. `StartWithTabs(data, tabs, width: 1000, height: 700)`; wait until scratch's graph is drawn. Screenshot `l33a-eight-tabs-1000`. 2. Set the window to 1100×700 (`app.Window.Width = 1100`, `Settle()`). Press Ctrl+Shift+P, type `open repository design-system`, press Enter; wait for design-system's graph. Press Ctrl+Shift+P, type `open repository notes`, press Enter; wait for notes' graph. Click "+" (`AddRepoButton`). Screenshot `l33b-eleven-tabs`. 3. Click "+". Screenshot `l33c-twelve-tabs`. 4. Click "+"; click "+". Screenshot `l33d-fourteen-tabs-scrolled`. 5. Press Ctrl+Tab; wait for the dashboard's read (`WaitForDashboardReadAsync`). Screenshot `l33e-first-tab-in-view`. | Tabs are read from `ShellDriver.Phase6.TabStrip`'s snapshot (each tab's bounds, title as drawn, tooltip, automation id, the active one; "+"'s bounds; `Offset`) and asserted to ±0.5 px.<br>**After step 1, `l33a-eight-tabs-1000`:** TS8: `Web` (`DashboardTab`, `ItemStatus` `Workspace`, `TabIcon` `IconWorkspace` 12×12 at 14 from its left in `VcTextSecondaryBrush` `#9BA3B0` (inactive), the title 6 after it), then `api`, `web-app`, `docs`, `mobile`, `billing`, `infra`, `scratch` (`RepoTab`), each 120 wide, tab k at x 120 k (the places phase 1's `StackPanel` gave), every title whole; `scratch` active (its accent line, `VcWindowBackgroundBrush`); every tab with its close button; "+" at x 962 to 996, named `Open a new tab`, tooltip `Open, clone or init a repository (Ctrl+T)`; `Offset` 0; the main area scratch's graph.<br>**After step 2, before the palette:** TS8w: the same widths and places, "+" at x 962 to 996 (natural widths fit 1062). After design-system: TS9, 118 each, design-system (index 8) active, "+" at x 1064 to 1098. After notes: TS10, two 107 (Web, api) and eight 106. **`l33b-eleven-tabs`:** TS11: `Web`, `api`, `web-app`, `docs`, `mobile`, `billing` 97 wide (x 0 to 582), `infra`, `scratch`, `design-system`, `notes`, `New tab` 96 (x 582 to 1062); the last `New tab` active, its welcome page in the graph area (x 260 to 700), its texts in reading order (`VisibleTexts.In`) `No repository open`, `Open, clone or init a repository to see its history.`, `Open`, `Clone`, `Init`, `Workspaces`, `New workspace…`, `Web`, `10 repositories`, `Recent repositories`, `notes`, `{root}\notes`, `design-system`, `{root}\design-system`: the recent list holds only the two the palette opened, `notes` (opened last) first, since 5.10's open is `RepoTabViewModel.OpenAsync(path)`, which puts a repository first in the recent list, while the restored tabs pass `remember: false` (`MainWindowViewModel.cs:163`) and the dashboard's rows open no tab; each folder (as the recent list holds it, `copy.PathOf(name)`) cut with "…" at the item's right edge and whole in its tooltip (phase 1's `10a`); the Workspaces section with its one `WorkspaceItem` `Web` and `10 repositories`, its tooltip as L26's; "+" at x 1064 to 1098; `Offset` 0; `Web` drawn whole (28 for 26.9), `web-app` and `design-system` cut with "…"; each tab's tooltip its whole title; every close button kept (13.3).<br>**After step 3, `l33c-twelve-tabs`:** TS12: six of 89 (x 0 to 534), six of 88 (x 534 to 1062); no scrolling (`Offset` 0); `Web`'s title cut with "…" (20 px for 26.9); "+" at x 1064 to 1098; the new tab active, its welcome page as `l33b`'s.<br>**After step 4:** after the thirteenth tab TS13 (nine 82, four 81, `Offset` 0); **`l33d-fourteen-tabs-scrolled`:** TS14: fourteen of 80, `Offset` 58, the active (the fourth `New tab`, index 13) whole at x 982 to 1062, the first tab drawn x −58 to 22 (its last 22 px; the panel clips at x 0), the dashboard's title "…" alone; "+" whole at x 1064 to 1098; nothing drawn of a tab right of x 1062; the main area the active `New tab`'s welcome page as `l33b`'s.<br>**After step 5, `l33e-first-tab-in-view`:** TS14f: Ctrl+Tab went round to the first tab (6.5): `Web` active (its icon `VcTextPrimaryBrush` `#E4E7EC`), `Offset` 0 (the least move that brings it whole into view), tab k at x 80 k, the last tab's first 22 px at x 1040 to 1062; "+" at x 1064 to 1098; the dashboard in the main area with scenario facts 2.3's ten rows (the nine with tabs following them, old-site `Folder not found`). Git unchanged: no operation ran. | | | `l33a-eight-tabs-1000`, `l33b-eleven-tabs`, `l33c-twelve-tabs`, `l33d-fourteen-tabs-scrolled`, `l33e-first-tab-in-view` | n/a: the widths and `Offset` are the panel's arithmetic, which both harnesses compute alike, and 17.4 gives the strip no real-window step; W1, W9 and K10's real-window steps show a dashboard tab beside repository tabs. Dark only: 11.14 leaves the overflowing strip in the light theme unpictured (the tabs' tokens are light in S15's pictures). The tab-strip region of every earlier picture must not change (13.3, 16.1). `l33b`, `l33c` and `l33d` show the welcome page's recent folders `{root}\notes` and `{root}\design-system`, cut with "…": as L26 says of `l26a`, they vary by nature only if a run shows the random part of `workspace-<12 hex>` (on the development machine phase 1's `10a`, whose `…\VisualCommit.Tests\graph-<12 hex>` items with names as short are cut before it in the same 392, suggests the cut falls before it, `workspace-` being longer than `graph-`; a shorter temporary folder, as CI's Linux `/tmp`, can show it, and the inspection says which). Nothing else varies by nature. Design 13.3, 13.4, 5.10, 6.5; D187, D205, D212, D213, D228. Decisions LD21, LD22. |
| L34 | Real window only (11.8, D224): the app started with `"theme": "Light"`, whatever the owner's mode, on "ops": the activity log open, the deletion question, the palette by a real Ctrl+Shift+P and the settings screen by its key, each against its scripted twin; "ops", Light, 1100×700 (`Check_L34_light_overlays_against_their_twins`, one launch) | 1. A new data folder with `"theme": "Light"` and a session of one tab on a fresh "ops" copy; `RealApp.Launch(data, session)` (`VISUALCOMMIT_RECORD_CLOCK=frozen`, `VISUALCOMMIT_BACKUP_CLOCK=fixed`, `VISUALCOMMIT_TOOL_CATALOGUES=fixed`, the session's git environment, 17.2); wait for the graph. 2. Click `FetchButton` with the real mouse; wait for the log line `ops: Fetch: Succeeded after <N> ms: Fetched from all remotes`. Click `ActivityLogToggle`; move the pointer to the neutral point; capture `l31a-activity-log-light`. Click `ActivityLogToggle`. 3. Click the folder `feature` under Local branches; right-click its `alpha`; click `Delete feature/alpha…` (by name); wait for `ConfirmationDialog`; move the pointer to the neutral point; capture `l31b-delete-question-light`. Click `Cancel`. 4. Move the pointer to the neutral point (as L31's step 3: the click on `Cancel` left it over where the palette's list opens, 5.8). Press Ctrl+Shift+P on the real keyboard (`PressKeys(CONTROL, SHIFT, KEY_P)`); wait for `CommandPalette`; capture `l31c-palette-light`. Press Esc. 5. Move the pointer to the neutral point (as L31's step 7). Press Ctrl+, ; wait for `SettingsDialog`; capture `l31d-settings-light`. Press Esc. | **After step 1** (after the launch, before any input, as 11.8 reads S15's start): the window light (the chrome, panel and window colours of the light variant sampled as phase 0's `AssertShell`: `#EBEDF1`, `#F5F6F8`, `#FFFFFF`; the first frame itself, which the pass cannot sample, is left to S15's scripted `s15d-restarted-first-frame-light` and `SystemThemeStartTests`, 11.15); `ThemeSwitch`'s help text `Switch to the dark theme`; the log `Theme: Light.`; the owner's app mode and high contrast neither read nor changed by this method.<br>**After step 2:** the `Fetch` operation's row in the log list (UI Automation: `ActivityLogOperation` named `Fetch`); the capture against the scripted `l31a-activity-log-light`, at most 3 % of the pixels differing (`run.txt`).<br>**After step 3:** `ConfirmTitle` `Delete branch?`, `ConfirmQuestion` `Delete the branch feature/alpha? It has commits that main does not have.`, `ConfirmDetail` `A backup is saved first, so this can be undone.` (UI Automation); the capture against `l31b-delete-question-light` (≤ 3 %); after Cancel no dialog and `feature/alpha` still at `ce5ea6a`.<br>**After step 4:** the palette's first item `CommandPaletteItem` named `Fetch all remotes`, selected, its `AcceleratorKey` `Ctrl+Shift+F`; `CommandPaletteBox` focused; the capture against `l31c-palette-light` (≤ 3 %).<br>**After step 5:** `SettingsDialog` on General, `SettingsThemeLight` toggled on, `SettingsThemeSystem` off (UI Automation's Toggle pattern); the capture against the scripted single `l31d-settings-light` (≤ 3 %). At the end the exact state of the copy equals the one read before the launch; no backup ref; each launch's global git file inside its own data folder (`<data>/harness/global.gitconfig`, 8.13). | | | real window: `l31a-activity-log-light`, `l31b-delete-question-light`, `l31c-palette-light`, `l31d-settings-light` (and their difference pictures) | RW: all its steps; it is the real window's own (11.8): the twins are L31's pictures, taken under System with the fake light, which draw Light's pixels (11.15's `System_and_Light_draw_the_same_pixels`). What only the real window shows: the light window from the launch through the real platform (sampled after the launch, before any input), a real Ctrl+Shift+P and Ctrl+, reaching the router, and the light overlays drawn by the real renderer at 200 %. Not run anywhere: following a change of Windows' app mode, the by-hand acceptance line (11.8). The palette's caret may differ between the twins (within D33's 3 %). Design 11.8, 11.15, 17.4; D224. Decision LD23. |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say
"n/a" with the reason.

## What proves each Delivers item

Every item of phase 6's "Delivers" list is proven by an automated test or by a visual check (the
plan's rule). This is design 17.5's table with this report's checks; the tests are those design 17.5
and the sections' test lists (1.12, 2.12, 3.9, 4.13, 5.12, 6.12, 7.11, 8.13, 9.17, 10.9, 11.15, 12.10,
13.5) name. Each part's own table, with the design's decisions D203 to D231 it proves, follows under
"By part".

| Delivers (plan.md, phase 6; design 17.5) | Visual checks | Automated tests |
|---|---|---|
| Workspaces (repo groups) kept in `workspaces.json` (D189), and a dashboard showing each repository's branch, ahead / behind and uncommitted changes, with bulk fetch and pull; the dashboard a tab of its own at the start of the strip (D187); opening a workspace shows its dashboard only, and a repository opens as a tab when opened from there (D188) | W1 (the welcome page's Workspaces section; a new workspace opening as its dashboard alone in the empty tab's place; adding by folder and by the scan's checklist, read with no F5; `Add open tabs`; rename; delete; `workspaces.json` and `session.json` after every edit), W2 (the ten rows' cells, tones and tooltips; the row menus; `Locate…` on the gone folder; `Remove from workspace`; Alt+Up), W3 (the Folder column at 1920×1080, light), W4 to W7 (the bulk runs), W8 (repositories opened from their rows as tabs beside the dashboard; the rows' reads), W9 (the file and the dashboard kept across a restart, first in the strip); K13 methods 2 and 3 (the dashboard by the keyboard), K10 method 2 (`Open workspace Web` activating its dashboard); L30 (the dashboard, its row menu and the scan's checklist in both themes), L33 (the dashboard tab first in the strip, with its repositories' tabs) | 1.12's (`JsonWorkspaceStoreTests`, `WorkspaceCommandsTests`, `RepositoryScannerTests`, `AddRepositoriesDialogViewModelTests`, and `WorkspaceFolderProbeTests`, by which alone a saved folder that lies inside another repository is proven), 2.12's (`DashboardTabTests`, `DashboardSessionTests`, `DashboardColumnsTests`, `DashboardViewTests`, `DashboardTextsTests`, `DashboardRefreshTests`, `DashboardActivityLogTests`; 2.8's branch part, the bindings on a dashboard tab, `OperationStatusViewModel.Compact*`), 3.9's (`SummaryModeTests`, `PromotionTests`, `OpenRepositoriesTests`, `RepoTabsTests`), 4.13's (`BulkRunTests`, `BulkSlotsTests`, `DashboardToolbarTests` for 4.10's states and reasons, `DashboardBulkViewModelTests` for the status bar's run part, `PromptDialogsTests`, `OperationQueueTests`, `AskPassTests`), 13.5's `WelcomeWorkspacesTests`; `Phase6ScenarioTests`, `AuthRemoteServerTests`, `FetchHoldTests` |
| Command palette (Ctrl+Shift+P, Cmd+Shift+P on macOS; the app's commands and the objects they act on, D191) and keyboard shortcuts: a fixed set, shown in the palette, the menus and the tooltips and listed in the settings screen (D192) | K10 (the palette: opening, commands with keys at once, filtering by every word, tiers, a destructive and a disabled entry, running after it closes, the pointer, `No matches`, Esc and a press outside, `Open repository {name}`, `Open workspace {name}`; RW: real Ctrl+Shift+P and typing), K11 (keys in menus and tooltips, the commit button's `Commit 1 file (Ctrl+Enter)`, a disabled button's reason alone), K12 (every main action by keys alone), K13 (the keys in the resolver and on a dashboard); S14 (the Keyboard shortcuts list, 6.11's 50 rows); L31 (`l31c` the palette and `l31f` an enabled tooltip with its key, both themes), L27 (`l27e`, a menu's key column, both themes), L34 (the palette by a real Ctrl+Shift+P, light) | 5.12's and 6.12's: `AppShortcutsTests` (the set, the free keys), `ShortcutTextTests` (the macOS words), `KeyRouterTests` (every tunnelling key, both keys of each row), `TabKeysTests`, `RegionKeysTests`, `CommitKeyTests`, `MenuKeyColumnTests`, `ShellToolTipTests`, `PaletteMatcherTests`, `CommandPaletteViewModelTests`, `PaletteEntriesTests`, `CommandPaletteViewTests` (the timings at 70,000 items, both themes' row colours); 7.11's `SettingsDialogViewTests` |
| Settings screen (identity, pull mode, auto-fetch interval, external tools) and theme polish; the identity, the pull mode and the merge tool are git's own configuration, global and, in a "This repository" section, the repository's own (D190); the theme can also follow the system (D194) | S14 (the screen: its sections, entries, first focus, keys, Cancel and Esc, both sizes, the Git section on `All repositories (global)`, `This repository` one click away and disabled with its reason on an empty tab; RW: the button, `s14a`, Windows' file dialog for `Browse…`), S15 (System followed live, left by a Theme click, chosen in the palette and on the screen, kept across a restart; RW at a real start), S16 (auto-fetch's minutes; the stash setting), S17 (Open file's extensions; the editor), S18 (the identity at both scopes, in effect, on commits), S19 (the pull mode at both scopes, per-key precedence, `branch.<b>.rebase`, an unusual value), S20 (the merge tool), S21 (Keep ignored files on and off); L26 to L31 and L34 (theme polish: every main screen in both themes, each pair a live follow of the system's change; `l31d`, `l31e` the screen in both themes; L34 Light in the real window) | 7.11's, 8.13's (alone for an included value's "Still" notice, 8.4, a held `.git/config.lock`, 8.5, and the `includeIf` split of the scopes' reads: `GitConfigReadTests`, `GitConfigWriteTests`, `GitSettingsViewModelTests`), 11.15's (`SystemThemeTests`, `SystemThemeStartTests`, `ThemeServiceTests`, `ThemeTokensTests` with the contrast ratios and the dark exceptions left to phase 8, `ToolTipStyleTests`, `DragLayerTests`, `System_and_Light_draw_the_same_pixels`), 12.10's; 17.2's isolation tests (`ShellTests`, `GitIsolationTests`) |
| From phase 1: a long folder in the welcome page's error wraps at awkward places ("C:" alone on a line, a name split); nothing is cut off | L32 (the welcome page's error and the clone form's, both themes, asserted on the drawn lines), L30's `l30c` and W1's `w1e` (the scan dialog's folder line by 13.2's rule) | 13.5's `PathTextBlockTests`; phase 1's check 11 gains the drawn-lines assertion |
| From phase 3: the settings screen covers `AutoFetchMinutes` (D85), `StashIncludeUntracked` and Open file's extensions (D130); its pull mode works through git's `pull.rebase`, `pull.ff` and `branch.<name>.rebase`, which Pull follows (D78, D84); its external tools give the file menus an "Open in editor" beside "Open file" | S16 (a file's 120 kept, 61 refused, 15 saved, off saved as 0; the stash dialog following the setting both ways), S17 (`.py` added: Open file asks; Visual Studio Code chosen: `Open in editor` after `Open file` in every file menu, its program and the file's path), S19 (the modes through `pull.rebase` and `pull.ff` at both scopes, `branch.main.rebase` noticed and removed, Pull refusing an unusual value before its fetch) | 7.11's and 12.10's (`FileMenuEditorTests`, `RepositoryOperationsFilesTests`), `AutoFetchSchedulerTests` (the interval from the setting, which neither harness shows: both force `VISUALCOMMIT_AUTO_FETCH_MINUTES=0`), `PullConfigurationTests`, `PullModesTests`; `PullParityTests`, `PullRebaseParityTests`, `PullFailureParityTests` unchanged |
| From phase 4: the external merge tool is git's own `merge.tool` (D149), chosen on the settings screen; without it "Merge tool…" and "Open in merge tool" are disabled with their reason | S20 (`Merge tool…` disabled with `Set merge.tool in git's configuration to use a merge tool`; the Pick dialog's tools; `slow` saved enabling it in the open resolver; `Not set` disabling it again) | 8.13's `MergeToolHelpTests` (the kept list and the background lookup of D202 with `AppSessionTests`), `GitSettingsViewModelTests`, `GitSettingsChangedTests` |
| From phase 3: the dashboard's bulk fetch and pull run through each repository's `OperationQueue`, as the toolbar's do (D81), with the same titles, Cancel and error dialog; auto-fetch fetches only the active tab's repository (D79, D85) | W4 (the toolbar's Fetch per row: its running text with git's percentage, its `Cancel`; RW), W5 (Cancel all; a row's own Cancel, its queue's), W6 (the identical error dialog by `Details…`; one sign-in for two repositories; a single row's Fetch opening its dialog at once), W7 (Pull's results and dialogs; the pull recorded for undo in the row's view model), W8 (one queue per worktree: a row's fetch going on in the tab that adopts it, one record) | 4.13's `BulkRunTests`, `RepositoryOperationsFetchTests`, `RepositoryOperationsPullTests`, `AutoFetchSchedulerTests` (auto-fetch, which both harnesses turn off); 3.9's `PromotionTests` |
| From phase 3: no code read `remote.<r>.fetch`, so the push of a drop onto a remote branch and a remote branch's "Delete <b> on <r>…" took the branch's name on the remote from its remote-tracking name (design 10: every fetch line is read and mapped back, and `DropFacts.RemoteMapsBranchesDifferently` gives way to the mapping, 10.7, D222) | G25 (a renamed branch's drop push and its deletion by its name on the remote, with the lease and the backup by the mapping, its checkout disabled on HEAD's branch; a ref fetched from two refs (R2): its pushes and deletion disabled, its checkout without an upstream (D201) and its double-click, the push guard; a ref that is not a branch (R4): its deletion disabled, its checkout tracking it as git does) | 10.9's `FetchRefspecsTests`, `FetchRefspecsParityTests`, `RefReadingTests`, `RepositoryMenusTests`, `RepositoryOperationsRemotesTests`, `RepositoryOperationsPushTests`, `LeftPanelViewModelTests`; by tests alone: R1 and R3, B2's remote rows, C2, the force-push restore's fallback, `MergedRemoteBranch`, "Set upstream…"'s list, "Create branch here…" with `--no-track`, Force push's and Push's guard |
| From phase 5 (design 9.1, 14.2): every checkout keeps ignored files by default (D193): `--no-overwrite-ignore` on every checkout, a refused checkout told with D179's ignored-files text, a setting on by default to turn it off; the other operations that can overwrite ignored files (D196: every merge, the pull's included; every rebase, that is onto, "Check out and rebase", interactive, the squash and the pull's; cherry-pick, revert, stash apply and pop; not the hard reset) | G22 (every checkout path: a local branch by its label, a remote branch into its local branch and as a new tracking branch, "Create branch" with its box ticked, an ignored folder, a commit off the branch detached from its menu), G23 (part A: a merge, a rebase onto a branch, a cherry-pick, a revert, a stash's apply and pop, each refused by the app's check before git; part B: the pull's rebase and merge refused inside its integration, a merge that adds nothing let through, git's `--ff-only` refusal winning), G24 (off in `settings.json`: phase 5's commands, git overwriting and deleting), S21 (the switch on the screen, on and off; D179's checkout still refused while off); P24 to P27 changed on purpose ("Changes to expected results") | 9.17's `KeepIgnoredCheckoutTests`, `IgnoredInTheWayTests`, `IntegratePullTests`, `RepositoryOperationsKeepIgnoredTests`, `RebaseTodoTests`, `OperationTextsTests`, `JsonSettingsStoreTests`; by tests alone: the interactive rebase, the squash, "Check out and rebase", "Check out and merge", the drop's merge, "Fast-forward {cur} to {b}", lists of picks and reverts, "Stash changes and check out", the backups list's and the reflog page's checkouts, Abort's return, and design 0.4's cases (S2 to S4, T5 to T7, ST1 to ST3, m15, m16, U2, U4, a criss-cross, long lists) |

The plan's visual checks and its "Done when":

- **The dashboard with ten repos in mixed states; bulk fetch progress**: W2 (all ten rows read on one
  screen, RW), W3 (at 1920×1080, light), W4 (three percentages, the done count and the bar, RW), L30
  (both themes).
- **Command palette search and run**: K10 (`k10a` to `k10f`; RW: real Ctrl+Shift+P, typing and Enter).
- **Settings changes take effect**: S15 to S21; G24 (the setting off from `settings.json`).
- **A sweep of every main screen in both themes**: L26 to L31 (29 pairs and a single picture, each
  pair also a live follow of the system's change on that screen), L34 (four light overlays in the real
  window against their twins); what design 11.14 leaves unpictured stays so, covered by the light
  pictures it names and by App tests (`DialogTests.Pictures_of_the_dialogs_in_both_themes`,
  `PromptDialogTests`).
- **"Ten repos can be checked and updated from one screen"**: W2 (all ten read on one screen), W4 (one
  Fetch all decides every row: all eight with a remote fetched, none failing, the two others left out
  with their reasons), W7 (one Pull all decides every row: the five that can be pulled pulled or refused
  with git's reason, the five that cannot left out with 4.2's). This is design 17.5's reading of
  `plan.md`'s "ten repos in mixed states", which the handoff states for the owner to confirm at
  acceptance.
- **"Every main action is reachable from the keyboard"**: K12 (its two methods press 34 of design 6.1's
  35 rows, each in a state reached by keys alone), K10 (the 35th, Ctrl+Shift+P, and the palette from the
  keyboard alone), K13 (the keys in the resolver and on a dashboard).
- **"The visual gate passes"**: this report's checks and design 17.7's inspection of the earlier
  pictures.

Harness items that are files are proven by the files and their tests (the handoff names them): the
project in `VisualCommit.slnx`, the real-window link, the timing script's default phase, `CLAUDE.md`'s
rows; the scenarios by `Phase6ScenarioTests`, ports 47393 and 47394 and the folder server by `AuthRemoteServerTests`,
the hold by `FetchHoldTests`, the isolation by `ShellTests` and `GitIsolationTests`, the drivers by
theirs (`Phase6SupportTests`, `ShellDriverCheckSupportTests`).

**The owner's decisions D187 to D202** and the checks that show each:

| Decision | Shown by |
|---|---|
| D187: a workspace's dashboard is a tab of its own at the start of the strip; it lists each repository's branch, ahead and behind and uncommitted changes and runs the bulk fetch and pull; a repository opened from it opens in a repo tab beside it | W1 (step 3: the empty tab becomes the dashboard; steps 4 to 6: its rows), W2, W4 to W7, W8 (web-app, docs and mobile opened beside the dashboard), W9 (restored first), K13 (Mod+1 to the first tab, a dashboard; Enter opening docs after the dashboards), L33 (the dashboard tab first in the strip) |
| D188: opening a workspace shows its dashboard only; a repository opens as a tab only when opened from the dashboard | W1 (steps 3 and 8: no repository tab opened, no second dashboard), K10 method 2 (`Open workspace Web` activating its open dashboard), W8 and K13 (a tab only from a row) |
| D189: workspaces in `workspaces.json`, a file of its own | W1 (the file after every edit), W2 (Locate, Remove, Alt+Up), W9 (across a restart), K13 method 3 (Alt+Down, Delete) |
| D190: the identity, the pull mode and the merge tool edited in git's own configuration, global and "This repository" | S18 (identity), S19 (pull mode), S20 (merge tool), S14 (both scopes) |
| D191: the command palette on Ctrl+Shift+P, listing commands and the objects they act on | K10 (RW), L31's `l31c`, L34 |
| D192: a fixed set of keys, shown in the palette, the menus and the tooltips, listed in the settings screen | K10 to K13, S14 (the list) |
| D193: every checkout keeps ignored files; a setting turns it off | G22, S21, G24 |
| D194: Dark, Light and System, System followed while the app runs | S15 (live with the fake; RW at a real start; by hand at acceptance: the checklist below), L26 to L32 (every pair), L34 |
| D195: a bulk run's failures stay in their rows with `Details…`; a single row's Fetch or Pull opens its dialog at once | W6, W7 |
| D196: which operations Keep ignored files covers | G23 (merge, rebase, cherry-pick, revert, stash apply and pop, the pull's rebase and merge), G22, G24, S21; the rest by 9.17's tests |
| D197: the Settings button at the start of the toolbar's right group, left of Search | S14 (step 1), K11 (its tooltip and key); every earlier window picture ("Changes to expected results") |
| D198: the light lane colours 1, 2, 4 and 7; the dark danger button keeps white text | L27, L28 (lanes 1 and 2), phase 1's `02-graph-light-1920x1080` and S15's `s15b-system-light` (lane 4), `ThemeTokensTests` (lane 7, and every lane at 4.5:1); L31's `l31b` (the danger button's white label in both themes) |
| D199: design 6.1's key table as it is, the commands left without a key named, keys in the commit button's, the resolver's, the interactive list's Cancel and the search bar's match tooltips | K10 (the commands without a key, such as `Show backups` and `New workspace…`), K11 (`Commit 1 file (Ctrl+Enter)`), K13 (`Next conflict (Alt+Down)` and the resolver's other buttons), S14 (the list), S18 (step 6) |
| D200: the Git section always opens on "All repositories (global)", "This repository" one click away, edits in both kept while switching | S14 (steps 4 to 6, 13 and 15), S18, S19, L31's `l31e` |
| D201: a ref one remote fetches from two refs is checked out into its local branch, or one made without an upstream, saying so | G25 (steps 4 and 6) |
| D202: the merge tools git knows looked up in the background and kept, so `Choose…` is ready at once | S20 and S14 (ready at once through the fixed catalogue), L31's `l31e`; the lookup and the kept list by `MergeToolHelpTests` and `AppSessionTests` |

### By part

What each part proves, as the part gives it (its Delivers rows, the decisions it proves and the tests
that prove what it does not show).

#### Part W

| Delivers item or decision (plan.md phase 6; design 17.5) | Part W's checks | And beyond the pictures |
|---|---|---|
| Workspaces kept in `workspaces.json` (D189), and a dashboard showing each repository's branch, ahead / behind and uncommitted changes, with bulk fetch and pull; the dashboard a tab of its own at the start of the strip (D187); opening a workspace shows its dashboard only, a repository opens as a tab when opened from there (D188) | W1 (the file after every edit; a workspace created from the welcome page and opening as its dashboard alone; adding by folder and by scan; `Add open tabs`; rename; delete), W2 (the ten rows' cells; the row menus; `Locate…` on the gone folder; `Remove from workspace`; Alt+Up), W3 (the Folder column, the light theme), W4 to W7 (the bulk runs), W8 (a repository opened from its row as a tab beside the dashboard), W9 (the file and the dashboard kept across a restart, the dashboard first) | 1.12's tests (`WorkspaceFolderProbeTests` alone for a saved folder inside another repository), 2.12's, 3.9's, 4.13's; `Phase6ScenarioTests` |
| From phase 3: bulk fetch and pull through each repository's `OperationQueue`, as the toolbar's (D81), with the same titles, Cancel and error dialog; auto-fetch only for the active tab (D79, D85) | W4 (the toolbar's Fetch per row: its running text with git's percentage, its `Cancel`), W5 (Cancel all; a row's own Cancel, which is its queue's), W6 (the identical error dialog by `Details…`; a single row's Fetch opening it at once), W7 (Pull's results and dialogs; the pull recorded for undo in the row's view model), W8 (one queue per worktree: a row's fetch going on in the adopting tab, one record) | auto-fetch only by `AutoFetchSchedulerTests` (the harnesses turn it off); `BulkRunTests`, `RepositoryOperationsFetchTests`, `RepositoryOperationsPullTests`, `PromotionTests` |
| The visual checks: "the dashboard with ten repos in mixed states; bulk fetch progress" | W2 and W3 (ten rows in mixed states, one screen at each size), W4 (progress: three percentages, the done count and the bar) | L30 (the same in both themes) |
| Done when: "ten repos can be checked and updated from one screen" | W2 (all ten read on one screen), W4 (one Fetch all decides every row: all eight with a remote fetched, none failing, the two others left out with their reasons), W7 (one Pull all decides every row: the five that can be pulled pulled or refused with git's reason, the five that cannot left out with 4.2's) | the reading for the owner to confirm, 17.5 |
| D195 (failures stay in their rows with `Details…`; a single row's Fetch or Pull opens the dialog at once) | W6, W7 | `BulkRunTests`, `RepositoryOperationsFetchTests` |
| D203, D204 (the file, names, ids; saved folders, the scan, the folder states, Locate, Remove) | W1, W2 | `JsonWorkspaceStoreTests`, `WorkspaceCommandsTests`, `RepositoryScannerTests`, `AddRepositoriesDialogViewModelTests`, `WorkspaceFolderProbeTests` |
| D205, D206 (the dashboard tab, its icon, id and item status; the session; its layout and columns) | W1, W2, W3, W9 | `DashboardTabTests`, `DashboardSessionTests`, `DashboardColumnsTests`, `DashboardViewTests` |
| D207 (when rows are read: the add trigger, the 10 s rule, a background dashboard) | W1 (trigger 6), W8 (triggers 1 and 2 with the read clock; rows following tabs never read), W9 (no read in the background) | `DashboardRefreshTests` |
| D208 (the toolbar, the status bar and the activity log on a dashboard tab) | W1 to W8 (TD, TR), W8 (the log following the selected row; its reasons) | `DashboardToolbarTests`, `DashboardBulkViewModelTests`, `DashboardActivityLogTests` |
| D209 (summary mode, adoption, one queue per worktree) | W7 (the adopted history offering the pull's undo), W8 (adoption with a running fetch; a row following its tab's live refs) | `SummaryModeTests`, `PromotionTests`, `OpenRepositoriesTests` |
| D210, D211 (which rows a run acts on; four slots; one prompt group; Cancel all; the run's texts; `PullNotFastForward`) | W4, W5, W6, W7 | `BulkSlotsTests`, `PromptDialogsTests`, `OperationQueueTests`, `AskPassTests` |
| D227 (the welcome page's Workspaces section) | W1 (with none and with "Web"; a click on its `WorkspaceItem` activating the open dashboard, step 8) | `WelcomeWorkspacesTests` (also the item's menu, `WorkspaceMenus.ForWelcomeItem`, and a click opening a workspace whose dashboard is not open in the page's own tab, 13.1) |
| D229 (the workspace scenario; the folder server on 47393 with holds at 25, 75 and 45; the fetch hold in each repository's own `.git/config`) | W4 (the folder server's holds), W5 and W8 (the fetch hold), W6 (the folder server with sign-in and the request gate) | `Phase6ScenarioTests`, `AuthRemoteServerTests`, `FetchHoldTests` |
| C1 (repo groups, each with a dashboard in a tab of its own); Q4 (a change made in a terminal shows when the user comes back) | W1 to W9; Q4: W8 (the window's activation reading a row 10 s after its last read) | `DashboardRefreshTests` |

#### Part K

| Delivers or Done when (plan.md, phase 6; design 17.5) | Part K's checks |
|---|---|
| Command palette (Ctrl+Shift+P; the app's commands and the objects they act on, D191) | K10 (opening, commands with keys at once, the first highlighted, PageDown and wrapping, items only with a query, every word matched in label or detail, the tiers, enabled before disabled, the session's runs first, a destructive and a disabled entry, Enter on a disabled one, Enter running after the palette closed, the pointer (a move highlighting a row, a click on a disabled row doing nothing and on an enabled one running it, the wheel scrolling), `No matches`, Tab staying in the box, a dialog from the palette and the focus back, Esc and a press outside, the window's keys declining, Mod+Shift+P selecting the query, `Open repository {name}` and `Open workspace {name}`, a repository's commands naming it while a dashboard is open); K12 step 25 (Mod+Shift+D opening it at `workspace`) |
| Keyboard shortcuts: a fixed set, shown in the palette, the menus and the tooltips (D192, D199) | K10 (keys in the palette and in UI Automation), K11 (keys in the empty area's, the working changes', the newest stash's menus, none on another branch's push; every toolbar and shell button's tooltip, help text and `AcceleratorKey`; the commit button's `Commit 1 file (Ctrl+Enter)`; the close button's key on the active tab only), K12 part B (the checked-out branch's keyed `Pull from origin/main` and `Push main to origin/main`), K11 step 2 and `k11b` (Push's tooltip with its key), K13 (the resolver's `Next conflict (Alt+Down)` and its other buttons', the disabled file buttons' kept; the dashboard's Fetch and Refresh tooltips with their keys). The settings screen's list is S14's |
| Done when: every main action is reachable from the keyboard | K12 (both methods, 34 of 6.1's 35 rows, each from a state reached by keys alone: New tab, Open, the tab keys, the regions, Stage all, the commit message and Mod+Enter, a file's diff by Enter and its history and blame by Mod+Shift+H and Mod+Shift+B, undo and redo, Shift+F10 and a menu's items, Fetch, Pull, Push, Create branch, Pop, Stash, Search with F3 and Shift+F3 between its matches, Show reflog, the Esc chain, Mod+Shift+D, Mod+, Init, Clone, Mod+W, F5, a disabled key consumed), K13 (the keys in the resolver and on a dashboard), K10 (the palette from the keyboard alone, its 35th row) |
| Visual check: command palette search and run | K10 (`k10a` to `k10f`) |
| From the dashboard's Delivers (D187, D188, D208): the dashboard by the keyboard | K13 methods 2 and 3 (Fetch all by its key, Mod+Shift+D round the dashboards with the shared results; F5, F6, Home and Down, Mod+B and Mod+Z consumed, Mod+J for the selected row, Alt+Down, Enter opening beside the dashboards, Mod+1 to the first dashboard, End and Delete), K10 method 2 (`Open workspace Web` activating the open dashboard) |

Decisions it proves: D191, D192, D199, D212 (the palette: dialog layer without dimming, the run after
closing, the matching and ranking, the disabled entries, the opening rule for repositories), D213 (the
key table, Ctrl+Tab in the strip's order and declining in the resolver's result, Ctrl+PageDown
everywhere, Mod+1 to Mod+9, Mod+Shift+D, F6 and Shift+F6 with each region's last element), D214 (one
formatter's words, the tunnelling router, the bubbling undo and redo, the keys declining while a dialog
or the palette is open, a disabled key consumed, Mod+F included, and the other keys on a dashboard), D215 (keys in menus only on the same
object, enabled tooltips with their keys and disabled ones with their reasons, help texts, the commit
button's key, the close button's key on the active tab, Mod+Enter from the message box), D163 (the undo
keys in the graph and in a text box), D205 (the dashboards first in the strip: Mod+1; a workspace opened
from the palette activates its open dashboard), D208 (a dashboard's Fetch key runs Fetch all) and D216
(Mod+, opens the settings screen on General). The `AppShortcutsTests`, `ShortcutTextTests`,
`KeyRouterTests`, `TabKeysTests`, `RegionKeysTests`, `CommitKeyTests`, `MenuKeyColumnTests`,
`ShellToolTipTests`, `PaletteMatcherTests`, `CommandPaletteViewModelTests`, `PaletteEntriesTests` and
`CommandPaletteViewTests` of 5.12 and 6.12 prove what part K does not show: the macOS words, every
tunnelling key pressed, the free keys, the timings at 70,000 items, `ReplaceItems`, both themes' row
colours.

#### Part S

| Delivers (plan.md, phase 6) | Part S's checks | Automated tests (design) |
|---|---|---|
| Settings screen (identity, pull mode, auto-fetch interval, external tools) and theme polish; the identity, the pull mode and the merge tool are git's own configuration, global and, in a "This repository" section, the repository's own (D190); the theme can also follow the system (D194) | S14 (the screen, its entries, sections, first focus, keys, Cancel and Esc, both sizes, `This repository` disabled with its reason), S15 (System followed, left, chosen in the palette and on the screen, kept across a restart; RW at a real start), S16 (auto-fetch), S17 (external tools: the editor), S18 (identity at both scopes, in effect, on commits), S19 (pull mode at both scopes, per-key precedence, `branch.<b>.rebase`, Other), S20 (merge tool) | 7.11's, 8.13's, 11.15's, 12.10's tests; 8.13's alone for an included value's "Still" notice (8.4), a held `.git/config.lock` (8.5) and the split of the scopes' reads (8.1, 8.2: the global scope from the outside read, so that a global save never replaces a value the screen did not show, D217's reason): `GitConfigReadTests`' live `includeIf "gitdir:"` reads (applying only inside its repository; 0.3's shape, the outside read without the `includeIf`'s `user.email`, the repository read ending with it), `GitConfigWriteTests`' global write of `user.email` under that shape (read back in the outside read as written, the repository's winner still the `includeIf`'s) and `GitSettingsViewModelTests`' pair of reads (the global field showing the global file's value with `In {repository}, git uses {value} from {file}.`, a global save judged by the outside read) |
| The keyboard shortcuts "listed in the settings screen" (D192) | S14 (Keyboard shortcuts: 6.11's 50 rows and keys, the Extend row on two lines) | 6.12's `AppShortcutsTests`, 7.11's `SettingsDialogViewTests` |
| From phase 3: `AutoFetchMinutes`, `StashIncludeUntracked`, Open file's extensions, the pull mode through `pull.rebase`, `pull.ff` and `branch.<name>.rebase` (D78, D84), Open in editor | S16, S17, S19 | `AutoFetchSchedulerTests` (the interval), `PullParityTests` unchanged, `PullConfigurationTests`, `PullModesTests`, `FileMenuEditorTests`, `RepositoryOperationsFilesTests` |
| From phase 4: `merge.tool` chosen on the screen (D149) | S20 | `MergeToolHelpTests`, `GitSettingsViewModelTests`, `GitSettingsChangedTests` |
| From phase 5: "a setting, on by default, turns it off" (D193; D221's reading: off restores phase 5, D179's checkout kept) | S21 (the switch on the screen, on and off, the next operation reading it) | `RepositoryOperationsKeepIgnoredTests` (off: phase 5's commands and no read; D179's checkout still refused), `JsonSettingsStoreTests` |
| Visual checks: "Settings changes take effect" | S15 to S21 | — |

Decisions proven, wholly or in the part named: D190 (S18, S19, S20), D192's listing (S14), D193's
switch and D221's "off" (S21), D194 and D223 (S15), D224 (S15's real window), D197 (S14), D199's
`Open settings (Ctrl+,)` and `Commit 1 file (Ctrl+Enter)` (S14, S18), D200 (S14, S18, S19), D202's
"ready at once" through the fixed list (S14, S20; the kept list itself is 8.13's), D216 (S14 to S16),
D217 (S18 to S20: one read per scope, only changed keys written, `--replace-all` and `--unset-all`, the
global writes in the data folder, the repository's as one queue operation, read back, `merge.tool` read
again in an open resolver; the `includeIf` split of the scopes' reads 8.13's tests alone, the Delivers
row above), D218 (S18, S19), D219 (S19 step 11: refused before the fetch), D220 (every
part S method: each session's own global file, the fixed catalogues), D226 (S17), D84 as D219 amends it
(S19), D168 (S18: no undo entry for a settings save).

#### Part G

| Delivers (plan.md, phase 6; design 17.5) | Part G's checks | Automated tests (design 9.17, 10.9) |
|---|---|---|
| From phase 5: ignored files kept by every checkout and the operations 9.2 adds, a setting to turn it off (D193, D196: every checkout; every merge, the pull's included; every rebase; cherry-pick, revert, stash apply and pop; not the hard reset) | G22 (a local branch, a remote branch into its local branch and as a new tracking branch, "Create branch" with checkout, an ignored folder, a commit off the branch detached from its menu: git's option and D179's words); G23 part A (a merge, a rebase onto a branch, a cherry-pick, a revert, a stash's apply and pop: the app's check, nothing written); G23 part B (the pull's rebase, keeping merges and plain, of a branch only ahead with a local merge, through the branch points; a merge whose source adds nothing let through after one read; a diverged merge refused; git's `--ff-only` refusal winning); G24 (Off: phase 5's commands, git overwriting and deleting); with S21 (part S: D179's checkout under Off, the setting on its screen) | `KeepIgnoredCheckoutTests`, `IgnoredInTheWayTests`, `IntegratePullTests`, `RepositoryOperationsKeepIgnoredTests`, `RebaseTodoTests`, `OperationTextsTests`, `JsonSettingsStoreTests`; by tests alone: the interactive rebase, the squash, "Check out and rebase", "Check out and merge", the drop's merge, "Fast-forward {cur} to {b}", lists of picks and reverts, "Stash changes and check out", the backups list's and reflog page's checkouts, Abort's return; 0.4's S2 to S4, T5 to T7, ST1 to ST3, m15, m16, U2, U4, a criss-cross, long lists; P24 to P27 changed on purpose (below) |
| From phase 3: `remote.<r>.fetch` for the drop's push and "Delete <b> on <r>…" | G25 (a renamed branch's drop push and deletion by its name on the remote, the lease and backup by the mapping, its checkout disabled on HEAD's branch; R2's pushes and deletion disabled, its checkout without an upstream and its double-click; the push guard; R4's deletion disabled, its checkout and its upstream named in Pull's tooltip by its tracking ref, GD15) | `FetchRefspecsTests`, `FetchRefspecsParityTests`, `RefReadingTests`, `RepositoryMenusTests`, `RepositoryOperationsRemotesTests`, `RepositoryOperationsPushTests`, `LeftPanelViewModelTests`; by tests alone: R1 and R3, B2's remote rows, C2, the force-push restore's fallback, `MergedRemoteBranch`, "Set upstream…"'s list, "Create branch here…" with `--no-track`, Force push's and Push's guard |
| The visual checks: settings taking effect | G24 (the setting off, from `settings.json`) | `JsonSettingsStoreTests` (`keepIgnoredFiles: false` round-trips) |

Decisions it proves: D193 and D196 (G22 to G24), D221 (G24: Off is phase 5), D179's text on every
checkout path (G22), D74 (`check-ignore` exits 0 with the path, G22 to G23, which git refuses with
exit 128 under `GIT_LITERAL_PATHSPECS=1`: the call log keeps no environment), D171 (no pop backup before
the check, G23 step 7), D211 (`PullNotFastForward`, G23 step 14), D219 (8.9's read before the fetch,
G23 part B's call order), D201 and D222 (G25), D93 as amended (G22 step 3, G25 steps 4, 6 and 11),
D100 (the lease, by the mapping, G25 step 10), D229 (the scenarios).

#### Part L

| Delivers (plan.md phase 6; design 17.5) | Part L's checks | Decisions proved |
|---|---|---|
| Settings screen ... and **theme polish**; the theme can also follow the system (D194) | L26 to L31 (every main screen of 11.14's table in both themes, each pair a live follow of the system's change both ways on that screen, with its menu, dialog, page, drag or tooltip open); L31's `l31d` (the Theme radios, System checked, then Light saved and shown), `l31e` (the Git section on `All repositories (global)`, the Pick dialog ready at once); L34 (Light in the real window, four light overlays against their twins) | D194, D198, D223, D224, D225 (light warning, success and danger at 4.5:1 in `l28a`, `l28c`, `l29a`, `l30a`, `l31a`, `l27c`, `l31b`, `l32`; light lanes 1 and 2 in `l27a`, `l27b`, `l27d`, `l27e` (rows 1 and 2), `l28b`, `l28d`, `l28f`; light lanes 4 and 7, which no part L picture shows ("ops" and "history" use colours 0 to 3): `ThemeTokensTests` (`VcLabelTextBrush` on every lane at 4.5:1, 11.15) for both, and lane 4 also in the light pictures of "graph" (colour 4 on `feature/login`'s rows): phase 1's `02-graph-light-1920x1080`, which 16.1 lists as changing, and part S's `s15b-system-light`; tooltips in the app's colours in `l27d`, `l27e`, `l31f`; `VcDangerTextBrush` in `l31b`); D200, D202, D216 (`l31d`, `l31e`) |
| From phase 1: a long folder in the welcome page's error | L32 (the welcome error and the clone error, both themes, on the drawn lines); L30's `l30c` (the scan dialog's folder line) | D227 |
| Workspaces ... and a dashboard ... The dashboard is a tab of its own at the start of the tab strip (D187) | L33 (the dashboard tab first, with its icon, `ItemStatus` and width rule, beside its nine repositories' tabs and new tabs; shrinking, then scrolling with the active tab in view; "+" at the end); L30 (the dashboard, its row menu and the scan's checklist in both themes) | D187, D205, D206, D228; D204 (`Already in this workspace`) |
| Command palette and keyboard shortcuts, shown in the palette, the menus and the tooltips (D191, D192) | L31's `l31c` (the palette's look in both themes), `l31f` (an enabled tooltip with its key), L27's `l27e` (a menu's key column) | D199, D212, D215 (their look; their behaviour is part K's) |
| The visual checks: "A sweep of every main screen in both themes" | L26 to L31, L34; what 11.14 leaves unpictured is covered by the light pictures it names and by App tests (`DialogTests.Pictures_of_the_dialogs_in_both_themes`, `PromptDialogTests`) | D194 |

The tests that prove the rest of section 11 and 13 without a picture are 11.15's (`SystemThemeTests`,
`ThemeServiceTests`, `ThemeTokensTests` with the contrast ratios and the dark exceptions left to phase 8,
`ToolTipStyleTests`, `DragLayerTests`, `System_and_Light_draw_the_same_pixels`) and 13.5's
(`PathTextBlockTests`, `RepoTabStripPanelTests`, `WelcomeWorkspacesTests`, phase 1's check 11 on the drawn
lines).

## Changes to expected results

Expected results of earlier phases that phase 6 changes on purpose (design 16.1, and what this
report's writers found beyond it), by phase and check, each with its reason (the design section and
decision); then the changes that show in no picture, what stays as it is, and anything changed in this
report after it was first committed (with the reason). The tests change in the same commit as the code
that changes them (plan.md's gate, step 6): WP-IA for the checkouts, the merges and the ignored-files
reads, WP-RA for the refspecs, the other work packages for their sections (design section 18).

**Earlier pictures are compared by region** (design 16.1, 17.7), against reference pictures made on
`master` before the gate, as phase 5's gate did: the Settings button changes every window picture of
phases 0 to 5 in one toolbar box, so `phase-5-pictures.sha256` names them all as changed and says
nothing more; `scripts/compare-pictures.py` places each change in its region, and a change outside the
regions named here for a picture is a defect. **A change in an earlier picture's tab-strip region is a
defect too** (13.3: every tab and "+" keep their places while the tabs fit at their natural widths, and
no earlier picture has more than 3 tabs). The references on `master` lack the phase branch's fix before
WP0, `43751d0` (phase 2 below), which is compared like every change on the phase branch.

### Every phase, 0 to 5

- **The toolbar's `Settings` button left of Search** (design 7.2, D197): every window picture of phases 0
  to 5, scripted and real-window, changes in one box of about 56×52 in the toolbar region, directly left
  of Search; Search and Theme keep their places and nothing else in the toolbar moves. Phase 0's
  `ShellExpectations.ToolbarLabels` (`ShellExpectations.cs:31`) gains `Settings` before `Search`, and the
  enabled list on an empty tab (`:96`) becomes `Settings`, `Theme`, with the messages at `:97-98`;
  `Phase0RealWindowPass.cs:134-141` (Theme within 20 of the right edge, `:141`) and `ShellTests.cs:99`
  (Settings enabled) stay; phase 5's `PartA.ToolbarButtons` and `EmptyTabToolbar` stay, since they name
  the buttons they check.
- **Tooltips in the app's colours** (11.12): in the 56 earlier pictures with a tooltip (listed by phase
  below; phases 0 to 2 have none) only the colours inside each tooltip's rectangle change (Fluent's
  `ToolTipBackground`, `ToolTipForeground` and `ToolTipBorderBrush` set per variant in `Tokens.axaml`):
  the tooltip's size, place, padding, border, font and text stay, but where a phase 5 row below adds a
  key. No test asserts Fluent's colours (`tests/` has no `ToolTipBackground` or `2B2B2B`).
- **Menus with keyed items get wider where a keyed item is their widest** (6.7): the menu's right edge
  moves right, its height and items stay; every keyed item's key shows at its right in the secondary
  colour; a menu whose widest item has no key keeps its width. The pictures are listed by phase below,
  with their real-window twins. `RepositoryMenusTests.Window.AssertWholeLabels` (`:509-537`) counts a
  keyed item as label + 24 + key; `Phase3Expectations.*`, `Phase4Expectations.*` and
  `Phase5Expectations.*` change their menu widths where they assert one.
- **`--no-overwrite-ignore` on every checkout and merge, and the ignored-files check's reads** (9.4,
  9.11, 9.16; D193, D196, D221): every checkout and merge command line of an earlier check gains the
  option just before its positional argument, and every operation of 9.2 gains its check's reads before
  git (a merge 9.4's `--diff-filter=A` read, and a `check-ignore` only when a path it lists is on disk;
  a cherry-pick and a revert 9.5's `diff-tree --stdin` and `check-ignore`; a rebase 9.6's four reads and
  `check-ignore`, seven with a merge in the range, whatever the rebase's options; the interactive list's
  and the squash's 9.7's two or three, then `check-ignore`). A longer command line can wrap in the error
  dialog and change its height; every activity-log picture with "Show reads" that holds a covered
  operation shows its check's reads. The lines are listed by phase below. App tests: `DialogTests` (347,
  356, 685, 695, 865), `RepositoryOperationsCheckoutTests` (66, 167), `RepositoryOperationsProtectTests`
  (51, 86, 107, 243, 250, 392), `RepositoryOperationsRebaseTests` (251, 332, 362, 439),
  `ShellDriverCheckSupportTests` (242, 249), `UndoRecordingTests` (449), `ActivityLogViewTests` (114,
  170), `RepositoryOperationsPullTests` (106), `UndoStepTests` (108); Git tests
  `BranchOperationTests.cs:217`, `DashBranchNameTests.cs:78`. Unchanged: the automatic stash's commands,
  undo's own switches (`UndoSwitch`, `UndoDetach`), `FailureClassifierTests`, `Phase5ScenarioTests`.
- **A refs read runs `git config --null --get-regexp ^remote\.` in place of `git remote`**, still five
  processes (10.2, D222): wherever an earlier picture shows a refs read's call, phase 5's `a2d-show-reads`
  (already varying by nature) and any activity-log row whose calls hold a refresh's refs read. No test
  asserts the app's `git remote` read (`tests/` runs `git remote` only as the tests' own git calls);
  `RefReadingTests` gains the parity with `git remote` (10.9).

### Phase 0

- **Checks 1 to 4** ("What the shell must show": the toolbar's labels and which are enabled): the labels
  gain `Settings` before `Search`; on an empty tab `Settings` and `Theme` are enabled (7.2, D197; the
  tests in the first row above).
- **The welcome page's Workspaces section** (13.1, D227): `01-shell-dark-1100x700`,
  `02-shell-dark-1920x1080`, `03-shell-light-1100x700`, `04-shell-light-1920x1080`, `05a` to `05c`,
  `06a` to `06d`, `07a` to `07c`, and the real window's `A-first-start-dark`,
  `B-after-theme-click-light`, `C-restarted-light`: the section (`Workspaces`, `New workspace…`, `No
  workspaces`, 68 high) between `Init` and `Recent repositories`, and the vertically centred stack 34 px
  higher (less where the page scrolls); the inspectors check that one layout against 13.1 once for each
  window size and theme, and any other difference in the welcome region is a defect (17.7 step 3).
  `ShellExpectations.WelcomeTexts` (`Phase0/ShellExpectations.cs:38-42`) gains `Workspaces`, `New
  workspace…` and `No workspaces` after `Init`; perhaps `Phase0RealWindowPass.AssertShell` (`:119-125`).
- **Checks 3 to 6 and real-window steps A to C keep their meaning** (11.4, 11.6, D223): they start Dark
  or with an explicit Light, so their clicks, tooltips, icons and saved values stay and no picture
  changes from the theme service; `ThemeSwitch` gains `AutomationProperties.HelpText` equal to its
  tooltip (no earlier real-window check asserts its absence). Phase 0's light pictures do not change from
  the light colours (11.10).

### Phase 1

- **Checks 10 and 11, the welcome page's Workspaces section** (13.1, D227): `10a-welcome-page`,
  `10b-second-tab`, `10c-second-tab-closed`, `11a-not-a-repository`, `11c-recent-in-new-tab`,
  `16a-before-closing`, `16b-restarted`, `17a-wide-panels`, `17b-narrow-window`, `17c-wide-again`, and
  the real window's `11a` (inspected by eye) and `16b`, as phase 0's. Check 10
  (`Phase1Checks.Shell.cs:61`) reads `WelcomeTexts[..9]` instead of `[..6]`, check 11 (`:123`)
  `recent[9..11]` instead of `recent[6..8]`. Unchanged: `13a`, `13b` and `14b` (the clone form hides the
  section).
- **Check 11, a long folder breaks only after a separator** (`PathTextBlock`, 13.2, D227; part L's L32):
  `11a-not-a-repository`, scripted and real-window, in the welcome region: its lines become
  `C:\Users\…\Temp\VisualCommit.Tests\` and `plain-<12 hex> is not a git repository.`; perhaps
  `14a-clone-failed` (the clone form's region), where a line may move, and only through 13.2's line
  breaking. The phase 1 report's note on check 11's wrap ("C:" alone on the first line, the folder's name
  split) is resolved. Check 11 gains an assertion on the drawn lines (`DrawnLines.Of`, shared with
  `PathTextBlockTests`): every line of `WelcomeError` but the last ends with a separator or a space.
- **The light colours** (11.10, 11.13, D198, D225): warning and success in `04a`, `04b`, `04c`; danger in
  `11a` (scripted and real-window); the lanes in `02-graph-light-1920x1080` and `04a` to `04c`; only those
  pixels' colours, where those letters, texts, marks and lanes lie. `Phase1Expectations.cs:52` and
  `:64-66` take 11.10's and 11.13's values in the commit that changes the tokens.
- **Check 15's tab strip** stays: `15a`'s three tabs keep their places (13.3, D228;
  `RepoTabStripPanelTests` places three tabs at 1100 exactly as the old `StackPanel` did).

### Phase 2

- **The light colours** (11.10, 11.13, D198): warning and success in `02a`, `02b`, `04a` to `04f`; danger
  in `04c`, `04d`; the lanes in `02a`, `02b`.
- **The stage lists keep every row a status read did not change** (`43751d0`, the phase branch's fix
  before WP0: `ChangedFileList.Show(…, keepFolders: true)` rebuilds with `keepRows`,
  `Rows.ReplaceAll(rows, ShowsSameAs)`, `ChangedFileRow.ShowsSameAs`, as the left panel does): no picture
  is expected to change (a kept row keeps its container, so only a row's hover or focus state could
  differ): phase 2's walk-through, run with the change just before it was committed, matched
  `phase-5-pictures.sha256` in 65 of its 66 pictures, `17c-restored` differing by 8 anti-aliased pixels.
  `StageListRefreshTests` (new with the fix).

### Phase 3

- **Command lines** (9.11, 9.16): check 06, `Phase3Checks.A.Pointer.cs:37` `git switch --no-guess
  feature/ff` → `git switch --no-guess --no-overwrite-ignore feature/ff` (also `Phase3SupportTests.cs:75`),
  `:111` `git switch --create feature/alpha --track refs/remotes/origin/feature/alpha` → `git switch
  --create feature/alpha --track --no-overwrite-ignore refs/remotes/origin/feature/alpha`; `:288`'s
  prefix `git merge --no-edit --no-log -m ` stays. Check 13, `Phase3Checks.C.cs:83` `git switch --create
  hotfix <Main work>` → `git switch --create hotfix --no-overwrite-ignore <Main work>`. Check 14,
  `Phase3Checks.C.Checkout.cs:65` `git switch --no-guess --no-overwrite-ignore feature/addfile`
  (`14b-untracked-in-the-way`: the refusal for the untracked file gains `git check-ignore --stdin -z`,
  exit 1, its summary unchanged; `14b1-checkout-failed`, refused before git, unchanged), `:88` `…
  feature/other` (`14c-checkout-blocked`, real-window `14c`), `:110` (2.31's push, `git switch --no-guess
  --no-overwrite-ignore feature/other`, pop), `:140`, `:158`, `:160` `… feature/clash`
  (`14f-clash-blocked`, `14g-reapplied-with-conflicts`), each picture's `ErrorCommand`. Check 18,
  `Phase3Expectations.D.cs:170` `git merge --no-edit FETCH_HEAD` → `git merge --no-edit
  --no-overwrite-ignore FETCH_HEAD` (`18c`), `:240` `git merge --ff-only FETCH_HEAD` → `git merge
  --ff-only --no-overwrite-ignore FETCH_HEAD` (`18b3-pull-refused`'s `ErrorCommand`), each pull's merge
  preceded by 9.4's `--diff-filter=A HEAD FETCH_HEAD` read, and each pull's configuration reads moving
  before its fetch (8.9; Phase 4 below). Check 23, `Phase3Checks.A.Toolbar.cs:46` `git
  switch --create topic/new <sha>` → `git switch --create topic/new --no-overwrite-ignore <sha>`. Part B,
  `Phase3Expectations.B.cs:772` `CheckoutCommand` → `git switch --no-guess --no-overwrite-ignore
  feature/alpha`, which the "not called" assertion at `Phase3Checks.B.cs:270` follows.
- **Check 18b, step 4: a pull refused by `pull.ff = only` gets its own summary** (4.8, D211; parts W and
  S show the text): `18b3-pull-refused` (scripted only): its `ErrorSummary` changes from `Git could not
  pull origin/main into main.` to `main and origin/main have diverged, and pull.ff is set to only: merge
  origin/main into main or rebase main onto origin/main.` (`OperationTexts.PullNotFastForward`), and the
  dialog may grow by the longer sentence's lines; its command as above.
  `Phase3Expectations.D.PullRefusedDialog` (`:236-243`) and
  `RepositoryOperationsPullTests.Pull_refused_by_pull_ff_only_shows_gits_message_after_the_fetch_ran`
  (`:94-114`) change with it. A plain merge refused by `merge.ff=only` or `branch.<b>.mergeOptions`
  keeps today's summary.
- **Check 23, tooltips with keys and help texts on the shell's buttons** (6.8, D215): part A's
  `AssertToolbar` (`Phase3Checks.A.Helpers.cs:293-323`), in check 23's six methods and every caller: an
  enabled toolbar button's tooltip and help text become 6.8's with the key (`Fetch all remotes
  (Ctrl+Shift+F)`, `Pull from {up} (Ctrl+Shift+L)`, `Push {b} to {up} (Ctrl+Shift+K)`, `Create a branch
  at HEAD (Ctrl+B)`, `Stash your changes (Ctrl+Shift+S)`, `Pop the newest stash (Ctrl+Shift+O)`), where
  today they are null; the disabled-tooltip pictures `23b`, `23h`, `23j`, `23k`, `23l` and `23m` keep
  their texts (a disabled button keeps its reason) and change only in the tooltip's colours.
- **Check C's enabled `Continue cherry-pick`** (D199): `Phase3Checks.C.Banners.cs:245` (`AssertTip(commit,
  null)`) becomes `Continue cherry-pick (Ctrl+Enter)`; no picture shows it.
- **Keyed menus** (6.7): `03f-empty-area-menu` (200 → about 223, as part K's `k11a`),
  `23i-unborn-empty-area-menu`, `04l-working-row-menu` and `04m-working-row-menu-all-staged` (about 223,
  as `k11c`), `18b1-current-branch-menu` and every menu of the checked-out branch (about 263 with `Push
  main to origin/main [Ctrl+Shift+K]`, as `k12e`), `03b-stash-menu` on `stash@{0}` (`Pop stash
  [Ctrl+Shift+O]`), the sections' and the diff's menus (`04f`, `04g`, `04h`, `04k`, `26d`), and their
  real-window twins.
- **Tooltips in the app's colours** (11.12): scripted `01c`, `02c`, `02e`, `03d`, `04m`, `09b`, `09c`,
  `09d`, `11a`, `11b2`, `11e`, `13l`, `23b`, `23h`, `23j`, `23k`, `23l`, `23m`, `26g`, `30f`, `30g`;
  real-window `01c`, `09b`, `23b`.
- **The light colours** (11.10, 11.13, D198): warning, success and the lanes in `27a` to `27d`; danger in
  `27b` and `27d`, where a danger button's background darkens in light while its label stays white
  (`VcDangerTextBrush`, 11.11). `Phase3Expectations.Common.cs:111-113`, `:121`;
  `Phase3Checks.D.Light.cs:106`.
- **Unchanged**: the Pick dialog's pictures (`28b` and the "Set upstream for {b}" steps) apart from the
  toolbar's box (7.7: its options have no detail, so its rows draw as before); `07d-drop-onto-remote-branch`
  (every earlier scenario uses the identity mapping; `DropFacts` loses its flag, below); `07a` (dark: the
  drag layer's redraw on a theme change shows in no earlier check).

### Phase 4

- **Command lines and reads** (9.4 to 9.7, 9.11, 9.16): `Phase4Expectations.C.cs:823` `SwitchToRebaseCall`
  → `git switch --no-guess --no-overwrite-ignore feature/rebase`, `:825` `SwitchToMainCall` → `git switch
  --no-guess --no-overwrite-ignore main`; `Phase4Expectations.A.cs:762` `SwitchToOtherCommand` → `git
  switch --no-guess --no-overwrite-ignore feature/other`; every rebase's call log gains 9.6's four reads
  and `check-ignore` (seven calls with a merge in the range, whatever the rebase's options), the
  interactive list's and the squash's 9.7's, every merge's 9.4's first read.
- **Pull reads its configuration before the fetch** (8.9, D219, D231): every pull's call log gains `git
  config --get pull.ff`, `git config --get branch.<b>.rebase` and, when that is unset, `git config --get
  pull.rebase`, each followed by its `git config --type=bool --get` for a value that is none of git's
  common boolean words (`only`, `preserve`), before its `git fetch` (they ran after it until now); "Pull
  from {up} (rebase)" gains `git config --get pull.ff` before its fetch and refuses a value `git pull
  --rebase` refuses. Part A's pull checks, which assert their calls by name, follow; no picture changes.
  `RepositoryOperationsPullTests.Pull_with_a_setting_the_app_does_not_understand_says_which_and_changes_nothing`
  (`:115`) gains: `origin/main` not fetched and no `fetch` among the operation's calls; `PullParityTests`,
  `PullRebaseParityTests` and `PullFailureParityTests` compile and stay unchanged (`PullIntegrationOptions`
  is optional).
- **The commit button's key in its enabled tooltip** (D199): `C14To17.AssertCommitButton`
  (`Phase4Checks.C.EditingSupport.cs:259-265`), whose enabled callers gain `{its text} (Ctrl+Enter)`:
  `Phase4Checks.C.Editing.cs:214`, `Phase4Checks.C.Kinds.cs:147`, `Phase4Checks.C.MergeTool.cs:122`;
  `WorkingChangesInProgressTests.cs:63` (`Commit merge (Ctrl+Enter)`), `:108` (`Continue cherry-pick
  (Ctrl+Enter)`) and `:86` where the button is enabled. No picture: `c12c-stage-panel` and every earlier
  picture of the commit button's tooltip show a disabled button's reason, which stays.
- **The resolver's fixed tooltips** (D199): `Previous conflict`, `Next conflict`, `Previous file`, `Next
  file` and `Close` become `… (Alt+Up)`, `… (Alt+Down)`, `… (Alt+Left)`, `… (Alt+Right)` and `Close (Esc)`
  wherever an earlier expectation reads them; no earlier picture shows them. The interactive list's
  Cancel gains its key likewise.
- **Keyed menus** (6.7): `c14b-result-menu`, `c14c-pane-menu`, the interactive list's row menus, and
  their real-window twins.
- **Tooltips in the app's colours** (11.12): scripted `a1a`, `a1b`, `a1h`, `b6f`, `b9b`, `b9e`, `c10b`,
  `c10c`, `c10d`, `c11b`, `c12c`, `c17b`; real-window `a1a`, `b6f`, `c11b`.
- **The light colours** (11.10, 11.13, D198): `d18a` to `d18d`. `Phase4Expectations.Common.cs:128-130`,
  `:145`; `Phase4Expectations.D.cs:188-190`.

### Phase 5

- **A1** (7.2, 13.1, 6.8): `a1a-empty-tab`, scripted and real-window, gains the welcome page's Workspaces
  section as phase 0's pictures do; step 1's "an empty tab: only Theme enabled, nothing with a tooltip"
  reads: Settings and Theme enabled, Settings with its tooltip `Open settings (Ctrl+,)`;
  `Phase5Expectations.A.WelcomeTexts` (`:210-214`) gains the three texts and
  `Phase5RealWindowPass.A.AssertEmptyTab` (`:559`) leaves out `New workspace…`. The toolbar's texts:
  `OpenToolbar`, `CommittedToolbar` (Fetch to Stash), `UndoCommit` + ` (Ctrl+Z)`, `ShowActivityLog` and
  `HideActivityLog` + ` (Ctrl+J)` (`Phase5Expectations.A.cs:158-185`), scripted and real-window
  (`Phase5RealWindowPass.cs:292-318`); `a1f`'s tooltip grows by ` (Ctrl+Z)` (A1's tooltips are scripted
  only). A1's pictures keep their three tabs' places (13.3).
- **A2** (9.11, 9.16, 10.2): `a2c-checkout-failed-opened`'s `ErrorCommand`
  (`Phase5Expectations.A.cs:433` `CheckoutCommand` → `git switch --no-guess --no-overwrite-ignore
  feature/kinds`) and A2's merge row and picture (`:460` `MergeCommand` → `git merge --no-edit --no-log -m
  "Merge branch 'feature/theirs'" --no-overwrite-ignore refs/heads/feature/theirs`); `a2d-show-reads`'s
  refs read; the real window's `Phase5RealWindowPass.A.cs:144`, `:157`, `:259-277`.
- **A4 and F** (6.8, D199): the real window's help texts of Undo and Redo gain their keys; the search
  bar's `Next match (Enter)` becomes `Next match (Enter, F3)` and `Previous match (Shift+Enter)` becomes
  `Previous match (Shift+Enter, Shift+F3)`: `SearchBarTests.cs:57-58`, `Phase5Expectations.A.cs:582`,
  `:584` (read at `Phase5Checks.A.Keys.cs:251` and as help text by `Phase5RealWindowPass.A.cs:657`),
  `Phase5Expectations.F.cs:185-186` (`Phase5Checks.F.Helpers.cs:161`); no picture shows them
  (`a1d-search-tooltip` is the toolbar's Search, `Search commits (Ctrl+F)`, unchanged).
- **Parts B and F, keyed menus** (6.7): `b5f-diff-menu`, `b8a-section-menu` and the menus of parts B and
  F with keyed items, and their real-window twins.
- **P24 to P27** (9.11, 9.16): `Phase5Expectations.P.cs:422` `SwitchOtherCommand` → `git switch
  --no-guess --no-overwrite-ignore other` (P24's refusals: TP1 and TP3 unchanged; the refusals for the
  untracked `notes` and the folder `notes` gain `git check-ignore --stdin -z`, exit 1, with their
  summaries unchanged; every dialog picture's `ErrorCommand`; real window too); `:424` `CreateTopicCommand`
  → `git switch --create topic --no-overwrite-ignore 19a90447e124bf3a74124a5673df655601c53d5b`; `:426`
  `SwitchMainCommand` → `git switch --no-guess --no-overwrite-ignore main` (P25's way back by hand);
  `:429` `DetachCommand(sha)` → `git switch --detach --no-overwrite-ignore <sha>`: P26 step 2 (`…
  4f5e8215c9321acae3a2ea386bda05107915f2da`, exit 1, TP1d, its text and "Stash changes and check out"
  unchanged, no `check-ignore`: a tracked block; git prints the same block with the option) and step 3
  (`… 41f8398f95fa73a5614e32760cc511673aa6a80d`, HEAD's own commit, the same result); `:449`
  `MergeOtherCommand` → `git merge --no-edit --no-log -m "Merge branch 'other'" --no-overwrite-ignore
  refs/heads/other` (P27 steps 2 and 6, each preceded by `git diff-tree -r -z --no-renames --name-only
  --diff-filter=A HEAD refs/heads/other --`, `notes\0`, and no `check-ignore`: `notes` is not on disk;
  git's refusals TP7 and TP9 unchanged); P27's cherry-picks of Other edits (steps 3, 7) preceded by `git
  diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT` and `git
  check-ignore --stdin -z` (exit 1: tracked files), its reverts of Describe the app (steps 4, 8) the same
  with `-R`; the stash step, summaries, titles and step 8's reflog entry kept; P27 part B unchanged
  (D179's count refuses first). `Phase5ExpectationTests.P.cs:201`, `:203` follow;
  `Phase5RealWindowPass.P.cs:125` reads the tooltip with its key.
- **U16, U18, U19 and U23** (6.8, 9.11): part U's `UndoRedoButtons.CanUndo` / `CanRedo`
  (`ShellDriver.Phase5.Undo.cs:109-119`) gain ` (Ctrl+Z)` / ` (Ctrl+Y)` in every U check; `u16a`'s tooltip
  grows by ` (Ctrl+Z)` (scripted and real-window), while `u23a` and `u23c` keep their reasons and `u23b`
  shows no tooltip; the forward checkouts' and merges' command lines: `Phase5Checks.U18.cs:77` (`git
  switch --create hotfix --no-overwrite-ignore <sha>`), `:230` (`feature/ff`), `:278` (`git switch
  --create feature/alpha --track --no-overwrite-ignore refs/remotes/origin/feature/alpha`), `:381`
  (`main`), `:483`, `:493` (`feature/other`); `Phase5Checks.U19.cs:311` (`main`, then `MergeOtherCall`);
  `Phase5Expectations.U.cs:567` `MergeFfCall`, `:660` `MergeOtherCall`, `Phase5Expectations.U2.cs:399`
  `MergeFf`, each `… -m "Merge branch '…'" --no-overwrite-ignore refs/heads/…`, with
  `Phase5ExpectationTests.U16-U19.cs:75` and `Phase5ExpectationTests.U2.cs:19`; undo's own commands
  unchanged. The real-window passes read only the commit button's name (`Phase5RealWindowPass.U.cs:90`)
  and stay.
- **Tooltips in the app's colours** (11.12): scripted `a1c`, `a1d`, `a1f`, `b6b`, `b6f`, `b8c`, `b8d`,
  `b9b`, `b9d`, `b9p`, `f14b`, `f14e`, `u16a`, `u23a`, `u23c`; real-window `b8d`, `u16a`.
- **The light colours** (11.10, 11.13, D198): `b5k`, `f10h` (warning, success; `f10h`'s lanes).
  `Phase5Expectations.Common.cs:132-134`, `:150`.

### Changes that show in no picture

- **App tests of the toolbar and status bar** (6.8): `ToolbarTests.cs:69` (`StashToolTip`) and `:231`
  (`PullToolTip`) become the enabled texts (the reasons at `:75`, `:135`, `:147`, `:157`, `:212`, `:222`
  stay); `ActivityLogViewModelTests.cs:377`, `:383`, `:387`; `StatusBarTests.cs:47`;
  `ToolbarViewModel.SearchGestureText` reads `KeyTexts` (`Search commits (Ctrl+F)` unchanged);
  `ConflictResultEditorTests.cs:51` and `CommitGraphControlTests.cs:998` take the light values (11.10,
  11.13).
- **"+"'s automation name `Open a new tab`** (was `Add repository`) **and a dashboard tab's `ItemStatus`
  `Workspace`** (2.1, 13.4, D205): UI Automation only; no test reads "+"'s name.
- **A disabled Search's key is consumed** (6.3): `WindowKeysTests`' six tests keep passing, now through
  the router; part K's K13 step 3 shows it in the resolver (phase 5's A4 method 2 pressed Ctrl+F there
  and expected nothing to open, which still holds). Every key an earlier check presses keeps its effect
  (part K found no further change: phase 3's check 5's keys in menus are not the router's; phase 4's
  resolver and interactive-list keys are their views'; phase 5's A4 keys keep phase 5's behaviour through
  the router; no earlier check presses Ctrl+Tab, Ctrl+W, Ctrl+T, F5 or F6 in the window).
- **The theme service** (`Setting`, `IsDark`, `Changed`, `Apply`), **`UseTheme` and the Theme switch's help
  text** (11.4, 11.6, D223): `MainWindowViewModelTests.cs:143-154` (`FakeThemes` gains `Setting`, `IsDark`,
  `Changed` and loses `Current`; the toggle test reads `themes.Setting`); `ShellTests.cs:86` stays true;
  `JsonSettingsStoreTests` loads `"theme": "System"` and still sets `{ "theme": "Purple" }` aside.
- **`VcDangerTextBrush` replaces the danger button's fixed `#FFFFFF`** (11.11, D198): the label stays white
  in both themes (light 5.94:1 on `#B7323D`); the token is new, `ThemeTokensTests` pins it.
- **`DragLayer` redraws on `ActualThemeVariantChanged`** (11.14, 15.3): no earlier check changes the theme
  during a drag; `DragLayerTests` (new).
- **The tab strip's panel** (`RepoTabStripPanel`, the strip's `DockPanel`, a tab's `MinWidth` 80; 13.3,
  D228): no earlier picture changes (every one has at most 3 tabs); `RepoTabStripPanelTests`.
- **Test isolation: a git environment per session, the safety net, the identity variables and `MSYSTEM`
  removed, `PATHEXT` fixed** (8.12, 17.2, D220): no picture is expected to change; the owner's
  `~/.config/git/ignore` no longer reaches the app under test (a scenario file it ignored would have shown
  untracked; none is known).
  `ShellTests.Git_calls_of_the_app_under_test_do_not_see_the_configuration_of_the_machine` (`:175-191`) is
  extended; `GitIsolationTests` gains the variables (`GIT_AUTHOR_DATE` kept);
  `Git_calls_that_end_after_the_session_do_not_bring_its_deleted_data_folder_back` (`:247-268`) still
  passes.
- **`IOperationQueue.Cancel`, the prompt group, `AskPassBroker.MaxConnections` 16** (4.4, 4.5, D210): the
  fakes gain the members (`RecordingQueue`, `RepositoryOperationsTests.cs:649`; `KeyRecordingQueue`,
  `RepositoryOperationsPushTests.cs:415`; `RecordingBroker`, `PromptDialogsTests.cs:237` and
  `OperationQueueTests.cs:857`); `AskPassTests.Several_prompts_at_once_in_different_scopes_are_all_answered`
  (`:309-334`): its comment's "(four)" becomes 16.
- **The status bar's source typed `IStatusBarOperation`** (2.8, 4.9, D208): the bindings' paths stay;
  `ShellDriver.Checks.cs:404-410` (`StatusTextRecorder`) takes the interface; `Phase3Checks.D.cs:190` and
  `:252` read `Waiting` and compile unchanged.
- **`DropFacts` loses `RemoteMapsBranchesDifferently`** (10.3, 10.7, D222):
  `RepositoryMenusTests.cs:798` `A_remote_that_maps_its_branches_differently_offers_no_push_by_drop` is
  rewritten with refspecs in the snapshot; `RefReadingTests`' `team/fork/main` keeps `team/fork`.
- **A tab opened on a worktree whose closed tab's or released summary's queue still runs waits for it**
  (`Waiting for {title} to finish…`, then opens; 3.3, D209): no earlier check reopens a tab while its
  operation runs; no test pins today's behaviour; `RepoTabsTests` gains the case (3.9).
- **A repository opens on the thread pool** (`GitRepositoryProvider.OpenAsync` and `InitAsync` wrap the
  whole open in `Task.Run`; 2.10, D207): nothing pins the thread; the restored tabs' and the welcome
  page's openings keep their texts and order.
- **A dashboard's session field** (`TabState(string? RepositoryPath, string? WorkspaceId = null)`; 1.11,
  D205): `JsonSessionStoreTests` gains a dashboard's round trip; every test of `TabState(path)` compiles
  unchanged.
- **`NamesRepositoryInDialogs` with a dashboard tab open** (3.8): no earlier check has a dashboard tab, so
  every earlier dialog's repository line stays; with one open, every operation's dialog names its
  repository (W6, W7).
- **`TempRepo`'s blank git folders without `hooks/*.sample`; `TempRepo.CopyAsync` leaving a relative URL
  alone** (17.1, D229): `Phase5ScenarioTests` and every scenario test unchanged (the default run measured
  before and after, 17.1).

### Stays as it is

Region by region, within the comparison's rule above (design 16.1):

- the tab-strip region of every earlier picture (13.3);
- every region of a repository tab's picture that no item above names: the dashboard's bindings give
  every other tab today's values (2.4), the status bar's branch icon stays `IconBranch` (2.8), and
  `NamesRepositoryInDialogs` changes only while a dashboard tab is open (3.8);
- phase 3's Pick dialog pictures apart from the toolbar's box (7.7);
- every file menu picture apart from a keyed diff menu's width: no earlier check chooses an editor, so no
  `Open in editor` item shows (12.1, D226), and `RepositoryMenusTests`' expected file menus are unchanged;
- the reflog and backups pages (the dashboard's list style is a copy of `pageList`, 2.6);
- every dialog that uses `UrlTextBlock` (the error dialog's summary, the confirmation, the prompts: 13.2
  leaves them);
- the error dialog's layout for every earlier caller (its repository line stays, 8.3) and every dialog's
  first focus (`FocusesTextBoxOnOpening` is true for all of them, 7.1);
- phase 0's checks 3 to 6 and real-window steps A to C as behaviour (11.6).

### The earlier tests and expectations each work package changed, as merged

The lines each package changed, as its builder reported them at its merge, for the gate to check
against the subsections above (16.1's rows); a line beyond them is named with its reason.

- **WP-T (the theme), merged in `e7144ec`.** Light lanes 1, 2, 4 and 7 become `#1C8655`, `#A16A1B`,
  `#1D7EA6`, `#59811C`, and Success, Warning and Danger `#187248`, `#895A17`, `#B7323D` in
  `Phase1Expectations.cs:52`, `:64-66`, `Phase3Expectations.Common.cs:111-113`, `:121`,
  `Phase4Expectations.Common.cs:128-130`, `:145` and `Phase5Expectations.Common.cs:132-134`, `:150`;
  `Phase3Checks.D.Light.cs:106` (`#B7791F` → `#895A17`); `Phase4Expectations.D.cs:188` (Warning) and
  `:190` (Danger), `:189` (the banner) unchanged; `ConflictResultEditorTests.cs:51` (`LightWarning`);
  `CommitGraphControlTests.cs:998` (the light lanes); `MainWindowViewModelTests`' `FakeThemes` (11.4's
  members). Re-run on its branch: phase 1's checks 1 and 2, 4 and 11, phase 2's 02 and 04, phase 3's
  27 and 23, phase 4's D18, phase 5's B5 light and F10 light. Its comparison of their light pictures with
  the main checkout's placed every change on a light A, M or D letter, a warning, danger, banner, marker
  or status text, light lanes 1, 2 and 4, or inside a tooltip; `11a` and `d18b` vary by nature.
- **WP-W (workspaces on screen), merged in `0da0eec`.** `Phase0/ShellExpectations.WelcomeTexts` gains
  `Workspaces`, `New workspace…`, `No workspaces` after `Init`; `Phase1Checks.Shell.cs` check 10
  `[..6]` → `[..9]`, check 11 `recent[6..8]` → `recent[9..11]` and an assertion of `WelcomeError`'s drawn
  lines (`DrawnLines.Of`); `Phase5Expectations.A.WelcomeTexts` gains the three texts; in the real-window
  pass (WP-R's files, accepted by the lead as 16.1's row asks) `Phase5RealWindowPass.A.AssertEmptyTab`
  leaves out `New workspace…` and checks the button's name `New workspace`, and
  `Phase0RealWindowPass.AssertShell`'s list gains `Workspaces` and `No workspaces`. The welcome pictures
  move up 34 px ("Changed in this report after its first commit").
- **WP-IA (keeping ignored files, the app half), merged in `cc3aed3`.** Phase 3: `Phase3Expectations.B.cs:772`
  (`CheckoutCommand`; the "not called" assertion at `Phase3Checks.B.cs:270` follows it),
  `Phase3SupportTests.cs:75`, `Phase3Checks.A.Pointer.cs:37`, `:111`, `Phase3Checks.C.cs:83`,
  `Phase3Checks.A.Toolbar.cs:46`, `Phase3Checks.C.Checkout.cs:65`, `:88`, `:110`, `:140`, `:158`, `:160`,
  and 14b gains one `git check-ignore --stdin -z` in the checkout's operation, exit 1 (16.1's 14b row;
  `:288` unchanged, it matches by prefix). Phase 4: `Phase4Expectations.C.cs:823`, `:825`,
  `Phase4Expectations.A.cs:762`. Phase 5: `Phase5Expectations.P.cs` (`SwitchOtherCommand`,
  `CreateTopicCommand`, `SwitchMainCommand`, `DetachCommand(sha)`, `MergeOtherCommand`),
  `Phase5ExpectationTests.P.cs:201`, `:203`; `Phase5Expectations.A.cs` (`CheckoutCommand`,
  `MergeCommand`: 16.1's `:433`, `:460`, moved by WP-W's lines); `Phase5Expectations.U.cs`
  (`MergeFfCall`, `MergeOtherCall`), `Phase5Expectations.U2.cs` (`MergeFf`),
  `Phase5ExpectationTests.U16-U19.cs:75`, `Phase5ExpectationTests.U2.cs:19`, `Phase5Checks.U18.cs:77`,
  `:230`, `:278`, `:381`, `:483`, `:493`, `Phase5Checks.U19.cs:311`. App tests: `DialogTests` 347, 356,
  685, 695, 865; `RepositoryOperationsCheckoutTests` 66, 167; `RepositoryOperationsProtectTests` 51, 86
  (with P24's untracked `notes` gaining its check-ignore, exit 1, found by the checkout's operation),
  107, 243, 250, 392; `RepositoryOperationsRebaseTests` 251, 332, 362, 439; `ShellDriverCheckSupportTests`
  242, 249; `UndoRecordingTests` 451; `ActivityLogViewTests` 114, 170; `UndoStepTests` 108. The pull's
  lines (`Phase3Expectations.D.cs:170`, `:240`, `RepositoryOperationsPullTests:106`) are WP-B's. Beyond
  16.1, in no earlier check: "Stash changes and check out" with nothing left to stash now shows a
  checkout's refusal as every checkout does (9.11), so an untracked file in the way gives "Checkout
  blocked" with phase 5's conflicts text, not "Checkout failed". Pictures expected to change: the
  `ErrorCommand` of the checkout and merge dialogs (`14b`, `14c`, `14f`, `14g`, P24 to P27, `a2c`, a longer
  line that may wrap) and activity-log pictures with "Show reads".

### Changed in this report after its first commit

A method split when the checks first run (the run-time rule of "Conventions of every part"), a mark for
the real-window pass changed after it was written, or an expected result changed for a reason the build
or the gate finds, goes here with its reason.

- **Part L's tooltip text colour (L27d, and L27e and L31f, which assert "as L27's step 4"), 2026-10-22.**
  Written as "the text `ToolTipForeground` contains `#E4E7EC` / `#1B1F27`". WP-T's build found that a
  tooltip's 12-px text is drawn with subpixel smoothing: the light tooltip `There are no changes to
  stash` has no pixel of exactly `#1B1F27` (the nearest `#1B3947`), while each channel's least value
  within the text equals it. Its review added that macOS (CoreText) and Linux (FreeType's LCD filter)
  draw the embedded Inter differently again, as phase 5's `TextColourTolerance` of 8 records. So a
  tooltip's text is asserted by `TextInk.Problems` (part L's "Colours" convention): within 8 per channel
  of the token and more than 8 from Fluent's `#FFFFFF` / `#000000`, which the token replaced (11.12).
  `ToolTipStyleTests` and `BlameViewTests` use the same helper.
- **The welcome page's Workspaces section (W1, L26, the conventions' "The welcome page's Workspaces
  section", "Changes to expected results" for phases 0, 1 and 5), 2026-10-22.** Written from design
  13.1's estimates: the heading row 19 high, a `WorkspaceItem` 25, the section 66 with no workspace
  and 51 + 25 × N, the stack 33 px higher. WP-W's build lays a line of 12.5-px text out 16 high, as
  every line of the welcome page already was (a recent item is 26 high with the same padding), so the
  heading row is 20, `NewWorkspaceButton` 133 × 20, an item 26, the section 68 and 52 + 26 × N, and the
  stack moves up 34; with three workspaces the old numbers would be 4 px off, beyond the ±2 the checks
  allow. Design 13.1 takes the same values; positions the checks give relative to the centred stack
  are unchanged.

## How the expected git states were derived

Every git state in the checks was derived on the Windows machine with plain Git 2.36.0.windows.1
(`C:\Program Files\Git`), in Git Bash, on copies of the scenario repositories made by the merged
scenario code itself (`Scenarios.*Async` of `tests/VisualCommit.Testing` at `b9c29d1`, through
`TempRepo` and `TempWorkspace`), whose every SHA equals the value `Phase6ScenarioTests` pins and, for
the earlier scenarios, the earlier phases' reports give; each part copied the copies again into a
folder of its own before running anything in them, and each operation ran on a fresh copy. Each
operation the app makes ran with the command lines the merged git layer builds, or the design gives
where the code is not built yet (`--no-overwrite-ignore`, the ignored-files reads, the `--no-track`
checkout, the mapped refs and the push guard, Pull's configuration read before the fetch), and with the
app's environment: `GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL` (or, for part S, a file
standing for the session's global file), `HOME` and `XDG_CONFIG_HOME` in the scratch folder,
`LC_ALL=en_US.UTF-8`, `LANGUAGE=en`, `GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`, `GIT_LITERAL_PATHSPECS=1`
but for `check-ignore`, `GIT_OPTIONAL_LOCKS=0` for the reads, `MSYSTEM` and the identity variables
unset, D72's dates (`GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` 2026-01-02T09:00:00+00:00) for the app's
own writes, and the copy's clock for a step made outside the app. The graphs were laid out by the app's
own `GraphLayout` over `GitRepository.ReadRefsAsync`, `ReadStatusAsync` and `LoadCommitsAsync`, from a
throw-away console program referencing the built `tests/VisualCommit.Testing`, on the states plain git
made; the rows' texts, the run's texts and the order of reads are the design's, not measured, since no
phase 6 UI existed.

The two gits: every git text the checks quote, and every state of the parts' operations, was replayed
with Visual Studio's Git 2.55.0.windows.5, its `mingw64/bin` first on `PATH` (scenario facts 8, item 8:
without it every fetch fails). The only differences that reach a check are `advice.diverging`'s nine
`hint:` lines before a refused fast-forward (W7, S19, G23's `g23i`), a fetch's and a push's `pack-reused 0
(from 0)` (W4, G25), the way `git rebase` joins its two lines (S19) and the rebases' counts (G's table of
what git does without the check, informative only); the conventions of every part say how the checks
assert git's texts on any version. CI's jobs run Git 2.56.0 (Windows, macOS), 2.55.0 (`ubuntu-latest`)
and 2.43.0 (the two containers); nothing here was derived with those, nor with Git 2.30, nor on a Mac or
a Linux desktop.

The derivations' copies, programs, scripts and logs lived in the session's scratch folder and are not
kept in the repository; the names below are theirs. Nothing in the repository was changed.

### Scenario facts

Scenario facts 9 says how the facts were derived: three throw-away programs over copies the merged
scenario code made at `b9c29d1`, with `[core] longpaths = true` added to `TempRepo`'s two blank git folders
for the scratch folder's long path (it changes no object, ref, status or output examined), checked
against every pin of `Phase6ScenarioTests` (no difference) and the earlier reports' SHAs (no
difference).

### Part W

**How these states were derived.** Lab copies of a template copy that scenario facts 9's program made
with the merged `Scenarios.WorkspaceAsync` at `b9c29d1` (an untouched copy, its relative remote URLs
and states checked against scenario facts 2.2 first), in the session's scratch folder, isolated as the
app's git is (`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME` and `XDG_CONFIG_HOME` in
the lab, `GIT_CEILING_DIRECTORIES` at the lab, `LC_ALL=en_US.UTF-8`, `LANGUAGE=en`,
`GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`, D72's dates 2026-01-02T09:00:00+00:00, `MSYSTEM` unset, reads
with `GIT_OPTIONAL_LOCKS=0`), each operation on a fresh copy, with Git 2.36.0.windows.1 and replayed
with Visual Studio's Git 2.55.0.windows.5 (its `mingw64/bin` first on `PATH`): docs' fetch and `git
merge --no-edit --no-overwrite-ignore FETCH_HEAD` (scenario facts 2.6's output, exit 0), then the
undo's calls ("Scenarios and graphs of part W"); mobile's fetch and, under `pull.ff = only`, `git merge --ff-only
--no-overwrite-ignore FETCH_HEAD` (exit 128 on both, 2.55 with its nine hints); the web-app commit
(`git add -A -- .`, then `git commit --file=- --cleanup=whitespace` fed the summary); the copied notes
repository's reads; scratch's untracked file. For this revision design-system's pull was replayed on
both gits with git's two streams kept apart: `git merge --no-edit --no-overwrite-ignore FETCH_HEAD`
writes `Updating 98ba313..948f7d1` to standard output and the four lines of its refusal to standard
error, exit 1. The graphs MF, WC, DP and DU and the rows' refs and statuses came from a throw-away
console program referencing the built `tests/VisualCommit.Testing` at `b9c29d1`
(`GitRepository.OpenAsync`, `ReadRefsAsync`, `ReadStatusAsync`, `LoadCommitsAsync`, `GraphLayout`), as
scenario facts 9's did. Everything else (the rows' texts, the run's texts, the order of reads) is the
design's, not measured: no phase 6 UI exists.

### Part K

**How these were derived.** On 2026-10-19, with Git 2.36.0.windows.1 in Git Bash, on copies of the
copies the scenario facts' derivation had built through the merged scenario code (`ops`, `graph`,
`conflicts` and a "Web" copy whose docs and mobile still had their file remotes, `origin` repointed),
copied again into this part's own scratch folder so that no other part's copy changed, with the app's
environment (`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME` and `XDG_CONFIG_HOME` in the
folder, `LC_ALL=en_US.UTF-8`, `LANGUAGE=en`, `GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`,
`GIT_LITERAL_PATHSPECS=1` where the app sets it, D72's dates) and the app's command lines (the merged
git layer's: `GitRepository.Writes.cs` `add --all --`, `commit --file=- --cleanup=whitespace`;
`GitRepository.Remotes.cs` `push --progress <r> refs/heads/<b>:<ref>`; `GitRepository.Stash.cs` `stash
push --include-untracked --message`; the design's `--no-overwrite-ignore`). The graphs were laid out by
the app's own `GraphLayout` over `LoadCommitsAsync` (scenario facts 9's reader). Every command was
replayed with Git 2.55.0.windows.5, its `mingw64/bin` first on `PATH`, on fresh copies: the same exits,
texts, SHAs and states (`git switch` writes its two lines on two streams, which a terminal interleaves
in either order). K-OPSk's history and blame (`git log --follow`, `git blame --porcelain` at
`9522dae`) and K-OPSs' search (`git log --no-walk=unsorted --stdin -F --regexp-ignore-case
--grep=alpha` over the rows' SHAs) were run on fresh copies of "ops" with both gits after the commit,
the pop and the stash were replayed there. Text widths were read with fontTools from the Inter faces
inside `Avalonia.Fonts.Inter` 12.1.3 (0.8's measure).

### Part S

On copies of the scenario copies the facts' derivation built (`tests/VisualCommit.Testing` at
`b9c29d1`: "settings", "ignored", "conflicts"; their SHAs equal the pins of `Phase6ScenarioTests`, and
their `.git/config` differs from the app's copies only by `core.longpaths = true`, which the facts'
program added and which changes no object, ref or output used here), in a work folder of this part's
own, with git isolated as the app under test is (`GIT_CONFIG_NOSYSTEM=1`, `HOME` and `XDG_CONFIG_HOME`
in the work folder, `GIT_CEILING_DIRECTORIES` above the copies, `LC_ALL=en_US.UTF-8`, `LANGUAGE=en`,
`GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`, the identity variables and `MSYSTEM` unset, D72's dates
`GIT_AUTHOR_DATE` = `GIT_COMMITTER_DATE` = `2026-01-02T09:00:00+00:00`), each check's session global file
emulated by `GIT_CONFIG_GLOBAL` pointing at a file of its own in a `harness` folder, with Git
2.36.0.windows.1:

- S14 and S18: the seed's outside read in the emulated data folder (with the ceiling above it) and the
  repository read; `git var GIT_AUTHOR_IDENT` and `GIT_COMMITTER_IDENT` (exit 128, the `useConfigOnly`
  refusal); `git config --global --replace-all user.name "Ann Example"` and `user.email`; the file's
  bytes (`cat -A`); `git var` again; `notes.txt` written, `git add -A`, `printf 'Add a third note' |
  git commit --file=- --cleanup=whitespace` → `3e519fb…`; Pull's three reads with S14's seed (each
  exit 1); on a fresh copy with part B's seed, `git var` (Ann Example), `git config --local
  --replace-all` of the repository's identity, `git var` (Bo Local), `.git/config`'s bytes, the same
  `notes.txt` and commit → `ead110c…` (tree `d8e00c9`, the tree of `3e519fb`, which was made again on a third copy to compare), on 2.36 and 2.55.
- S19: with the seed, Pull's reads (`git config --get pull.ff` → `only`; `git config --type=bool --get
  pull.ff` → `fatal: bad boolean config value 'only' for 'pull.ff'`, 128; exit 1; exit 1); `git fetch
  --progress origin` (exit 0, nothing printed); `FETCH_HEAD`; the `--diff-filter=A HEAD FETCH_HEAD`
  read (`remote.txt`); `git merge --ff-only --no-overwrite-ignore FETCH_HEAD` (128); the two `--local
  --replace-all` writes and the repository read; the three reads again (`true`, exit 1, `true`); the
  fetch; `git merge-base --is-ancestor HEAD FETCH_HEAD` (1); `git rebase` (0) → `b4253bb…` (the facts'
  SHA), with its reflog; on a second copy the two keys set, both `--unset-all` (the emptied `[pull]`
  removed), `branch.main.rebase false` read (`false`, `pull.rebase` not read), its `--unset-all`,
  `pull.rebase preserve`, the reads (`only`, the `--type=bool` read of `pull.ff` (128), exit 1,
  `preserve`), `git config --type=bool --get pull.rebase` (128) and, for comparison, `git pull` (128
  before fetching); then, for this part's review, Pull's reader replayed in order at each of those
  states on 2.36 and 2.55 (the table above: identical, the `--type=bool` read of `pull.ff` after every
  `only`).
- S20: the seed's outside read (`mergetool.slow.cmd`), `--replace-all merge.tool slow` and its bytes;
  on a copy of "conflicts" `git merge feature/theirs` (exit 1, `UU notes.txt`), `git config --get
  merge.tool` (`slow`); `--unset-all merge.tool` (the emptied `[merge]` removed) and `--get` again (1).
- S21: the scenario facts 4.2 and 4.4 (derived there on 2.36 and replayed on 2.55): the refusals of
  `git switch --no-guess --no-overwrite-ignore vendor` and `git switch --detach --no-overwrite-ignore
  1ac759d…`, `git check-ignore --stdin -z`, and `git switch --no-guess vendor` without the option.

Replayed with Visual Studio's Git 2.55.0.windows.5 (its `mingw64/bin` first on `PATH`), each on a fresh
copy: the `useConfigOnly` refusal's last line, the global file's bytes after the two writes, both
commits' SHAs, the fetch, the fast-forward refusal (2.55 adds the nine `hint:` lines), `git rebase`
(`b4253bb…`; its two lines joined by a carriage return without `ESC[K`) and the two `--type=bool` refusals:
otherwise identical. Not run: the app itself (no picture, no measured position: every position is
derived from design 7.1 and the scenario facts' window geometry), Git 2.30, CI's 2.56 and 2.43, a real
Mac or Linux desktop.

### Part G

**How part G's states were derived.** On 2026-10-19 on the Windows machine with Git 2.36.0.windows.1
(`C:\Program Files\Git`), in Git Bash, on copies of the scenario copies the merged scenario code built
at `b9c29d1` (scenario facts 9; `cp -r` of a pristine copy for every case, its reflogs, refs and
configuration checked first), with the app's environment (scenario facts 9's list, `GIT_LITERAL_PATHSPECS=1`
but for `check-ignore`, `GIT_OPTIONAL_LOCKS=0` for the reads, D72's dates for the app's own writes and
the copy's clock, 2026-01-01 12:07, for IG1's construction) and the command lines the merged git layer
builds (`GitRepository.Branches.cs`, `GitRepository.KeepIgnored.cs`, `GitRepository.PullConfig.cs`,
`GitRepository.Remotes.cs` at `eef3a7f`) or the design gives where the code is not built yet (the
`--no-track` checkout, the deletion's and push's mapped refs, the push guard). After every call the
exit code, both streams, HEAD, the refs, the remote repositories' refs, the branch keys, the status
with `--ignored` and the files' bytes were printed. Every case was replayed with Visual Studio's Git
2.55.0.windows.5, its `mingw64/bin` first on `PATH` (scenario facts 8, item 8): identical but the
`hint:` lines of TG3 and the rebases' counts in the table above. G22 step 7's detached
checkout (TG1, then `check-ignore`), TR6's reads (5) and (6) once more ((5) in the same order on
both; (6)'s output the same with its input lines in either order) and `check-ignore` under
`GIT_LITERAL_PATHSPECS=1` (exit 128) were derived the same way on the same day, alike on both gits. The graphs IG1, IG2, IG3 and RS1 to
RS5 were laid out by the app's own `GitRepository.ReadRefsAsync`, `LoadCommitsAsync` and `GraphLayout`
(the throw-away reader of scenario facts 9) on the states plain git made. Nothing in the repository
was changed.

### Part L

**How these were derived.** A folder of this part's own (`p6-report/t/partL`) got copies of the
scenario facts' "ops" and "history" copies (built by the merged scenario code at `b9c29d1`), in the
environment of the scenario facts' section 9 (`env/env.sh`: the app's and the tests' isolation, D72's
dates). There: `git status --porcelain=v2 --branch`, `git for-each-ref` and `git merge-base
--is-ancestor refs/heads/feature/alpha HEAD` (exit 1) on "ops"; `git fetch --progress origin` (no output,
exit 0, refs unchanged); on "history" `docs/todo.md` written, the snapshot rebuilt with `GIT_INDEX_FILE`,
`read-tree HEAD`, `update-index --add --cacheinfo`, `write-tree` and `commit-tree -m "VisualCommit:
snapshot before discarding 1 file" -m "src/Reader.cs"` under the VisualCommit identity, giving
`0bd36b6c3aded07487326d5d4f98d9eb72d11334`; HS1's `git log` with the solo revisions; the failed clone with
Git 2.36.0 and, with its `mingw64/bin` first on `PATH`, Git 2.55.0 (`git clone --progress -- <url>
cloned`, replayed once more in `p6-report/t/partL-rev` for L32's whole text: the same six lines on both,
exit 128, no folder `cloned`). The pill's and the outline's points of L27 step 1 are phase 3's measured
label centres (`Phase3Expectations.A.cs:812-821`) with `DragLayer.cs`'s geometry and
`CommitGraphControl.Render.cs`'s label top (`(RowHeight − LabelHeight) / 2`). The tab widths are 13.3's
arithmetic over the scenario facts' natural widths; the rest is the earlier reports' measured facts.

## Decisions this report makes

Values the design leaves open, chosen by this report's writers to fit the design and the codebase's
conventions, for the lead to fold into the design (or change, with the expected results that follow,
before the UI is built). They are numbered per part (WD, KD, SD, GD, LD), 138 in all; each names in
parentheses the design section it fills, and the checks cite them by these numbers. Two pairs give one
rule in two parts and agree: WD8 and KD11 (a row moved by Alt+Up or Alt+Down stays selected and keeps
the focus) and WD27 and KD10 (a read pass's count includes a folder-state row's check); part K's waits
on a dashboard use part W's WD37, and part S's S21 and part G's G22 picture one refusal once (SD13).
The lead's rulings on the decisions that asked for one, or that amend a design section's wording, are
under "The lead's rulings on this report's open points".

### Part W (WD1 to WD37)

- **WD1** (1.7, 1.9). **After the checklist's `Add {N} repositories` the selection stays where it
  was** (W1: api, selected by the single folder's add). 1.7 selects only the row of a single folder's
  add; selecting the first of eight would scroll a long list for no reason.
- **WD2** (2.5). **An empty workspace shows no column headings and no list**: the empty state's stack
  takes the area below the header, centred there as the welcome page's stack is in its area.
- **WD3** (4.7, 4.9). **A row's own Fetch or Pull outside a run leaves the header's run part and the
  dashboard tab's status bar as they were** (`Ready` before any run): they show only the dashboard's
  runs (4.9's status-bar source is `DashboardBulkViewModel`); the row's Operation cell and its view
  model's status show the operation.
- **WD4** (2.6; scenario facts 8 item 1). **The Ahead / behind cell tests "no remote" before "no
  upstream"**: a repository without remotes shows `No remote` (`This repository has no remotes`), so
  notes reads as W2 expects; Pull's reason keeps the toolbar's order (`main has no upstream: set one
  first`), so the cell and the reason name different things there. `DashboardTextsTests` pins both.
  Folded into design 2.6 (the lead's ruling 5).
- **WD5** (2.6; scenario facts 8 item 2). **The unstaged count leaves out untracked and conflicted
  entries** of `WorkingTreeStatus.Unstaged` (each counted by its own kind): design-system `1 staged, 2
  unstaged, 1 untracked`, billing `1 conflict`, scratch with one new file `1 untracked`.
  `DashboardTextsTests` pins the three. Folded into design 2.6 (the lead's ruling 5).
- **WD6** (17.3, 17.4). **W1 is two methods**: after the checklist every repository of `{root}` is in
  "Web", so 17.4's `Add open tabs` with two repo tabs could add nothing; part B starts on "Web" less
  web-app and docs with their tabs restored (WD19). With W5's split (WD36) part W is eleven methods
  and twelve app starts; 17.3 counted ten for it, of 38 in all; with every part as written the
  walk-through has 47 in two projects ("Conventions of every part", the lead's ruling 1).
- **WD7** (1.10, 17.4 W2's "a repository the check makes"). **Locate's new place is
  `{root}\archive\old-site`, a file-by-file copy of `{root}\notes` made before the start** (no git
  process, no new scenario): a repository on `main`, no remote, clean, at its own top, so the row reads
  Ready with notes' cells under the name `old-site`.
- **WD8** (1.10, 2.7). **Alt+Up and Alt+Down keep the moved row selected and the keyboard focus in the
  list**, as phase 4's interactive list keeps its moved row; `workspaces.json` is saved at once.
- **WD9** (1.6, 17.1). **W1 names its workspace `Web`**, the scenario's `ToWorkspace()` name, so that
  W1's rows and every other check's read alike.
- **WD10** (2.5, 15.1). **`DashboardMore`'s menu opens below the button, moved left to stay 4 inside
  the window**: the menu layer's rule for a button's menu (phase 4's 14.3), at the window's right edge.
- **WD11** (1.6, 3.8). **The workspace's name dialog and its deletion question show no repository
  line** (`DialogRepository` hidden): they are no operation's dialogs, which 3.8's `In {repository}`
  is for; the workspace flows' errors name the workspace in the error dialog's own line (1.5).
- **WD12** (4.5, 4.9). **`Cancelling: waiting for {r} repositories…` is asserted from the recorded
  header texts, not pictured**: it lasts only until the killed fetches end (about a process start).
- **WD13** (4.9). **`{k} done` counts every row that has ended, whatever its outcome**, a cancelled or
  failed one included (4.9 writes "`{k}` ended"); the bar is `⌊100 × {k} / {n}⌋`: W5's `1 done` at 12,
  W6's `6 done` at 75.
- **WD14** (4.4). **W6 pictures the sign-in once the six other rows have ended and mobile's first
  request has been answered 401**, so that what the dimmed dashboard shows behind the dialog does not
  depend on timing.
- **WD15** (3.3). **A tab that adopts a row's summary-mode view model shows that view model's last
  result in its status bar** (the same `OperationStatus`: W7's `Pulled 4 commits into main
  (fast-forward)`), as the queue and the undo history carry over.
- **WD16** (1.6). **W1 renames "Web" to `Web team`**: a valid name of another spelling, not a change
  of case alone.
- **WD17** (W8). **The commit W8 makes**: `contact.html` (`<h1>Contact</h1>\n`), summary `Add the
  contact page`, no description: `092116c01a4d62eefc50c08ac9f1e9109fbe0059` on 2.36 and 2.55.
- **WD18** (W8). **The file W8 writes in scratch**: `todo.txt` (`Try it\n`), untracked.
- **WD19** (1.11, W1 part B). **Part B starts on "Web" less web-app and docs** (eight paths, WB8) with
  the session [the dashboard, web-app's tab, docs' tab] and the dashboard active, so that `Add open
  tabs` adds two rows that follow restored tabs.
- **WD20** (1.7). **`Add open tabs` selects no row**: it adds many at once and reports nothing (1.7's
  selection belongs to a single folder's add).
- **WD21** (2.5). **The empty state's buttons show their disabled reasons as tooltips**
  (`ToolTip.ShowOnDisabled`), as the header's buttons do: `Add open tabs`' `No tab has a repository
  open`.
- **WD22** (2.7). **The checks click, double-click and right-click a row in its Repository cell at x
  66**, the cell's centre at 1100 and 1920, where no button or tooltip of another cell lies.
- **WD23** (2.8, scenario facts 1). **The neutral point stays (760, 687) on a dashboard tab**: at
  1100×700 in the status bar right of the run's text, bar, Cancel and waiting count (they end left of
  x 450 for every text of part W) and left of `GitStatus`; at 1920×1080 in the list below the tenth
  row. `ShellDriverCheckSupportTests` confirms it on a dashboard tab with W4's running text.
- **WD24** (4.4, 17.2). **W6 signs in with the folder server's credentials, `alice` and `s3cret`**,
  typed into the dialog.
- **WD25** (1.11, 2.10, 3.4). **W9 asserts the background dashboard only by the reads it does not
  start (WD37) and the git calls and log lines it does not make**, not by its rows' texts: 2.10 says a restored background dashboard reads
  nothing, but not whether a row whose folder a restored tab holds follows that tab before the
  dashboard's first read; the rows are asserted after the activation.
- **WD26** (1.1, 1.3). **A new workspace's id is asserted as 32 lowercase hex digits**
  (`Guid.ToString("N")`), unique in the file, never shown.
- **WD27** (2.10, 14.14). **The pass line's `{n}` counts every row the pass read**, a folder-state
  row's check included (W2: `Web: read 10 repositories`, old-site's folder check among them), never a
  row that follows a tab, which no pass reads (WD35); asserted in W1's adds, W2, W8 step 9 (seven) and
  W9 step 5 (nine), and elsewhere only the line.
- **WD28** (1.6). **`New workspace` has no context line** (`NameDialogContext` hidden): 1.6 names one
  only for the rename (`Rename {name}`).
- **WD29** (2.6, 17.4 W3). **W3's Folder cells hold the saved text** (`copy.PathOf(name)`, never
  rewritten, 1.2), asserted equal to it; they are whole on the development machine (its temporary
  folder is short enough for 632 px) and may be cut with "…" elsewhere (macOS's longer temporary
  folder), whole in their tooltips.
- **WD30** (1.9). **W1's checklist is pictured as it opens, not scrolled**: eight rows in view, web-app
  below them; the check reads all nine from `FoundList`'s items.
- **WD31** (4.9). **On a dashboard tab the status bar's Cancel is `OperationCancelButton`**, still
  named `Cancel`, its tooltip the run's (`Cancel the rest of Fetch all`), hidden from Cancel all's
  click to the run's end.
- **WD32** (2.9). **While a folder-state row is selected the log's panel and splitter are hidden and
  the list takes their room** (y 152 to 674); the toggle is disabled with the row's State tooltip and
  loses `selected`; the window's open state is kept, so the panel comes back on a row with a view model.
- **WD33** (2.10). **W5's F5 is pressed with the list focused, after a click on api's row**: Refresh's
  key reads the active dashboard wherever the focus is (6.9), but the click keeps the step independent
  of where Cancel all left the focus.
- **WD34** (2.6). **The Branch cell's text is primary**, as the Repository cell's (2.6 gives the
  Branch cell no tone); `Detached at {short}` likewise.
- **WD35** (2.10, 14.14). **A trigger that finds no row to read starts no pass and logs no line**: a
  row to read is one without a tab that, for triggers 1 and 2, was never read or was last read 10 s
  ago or more; a row that follows a tab is never read and never counted in a pass's `{n}` (WD27). So
  W1's `Add open tabs` (both added rows follow tabs) and W8's activations before the read clock moves
  log no pass line, and `WaitForDashboardReadAsync(since)` is ended only by a pass that read. 2.10 says
  only that a pass "logs its line with the rows that finished"; the lead folded this into it (the
  lead's ruling 5), and `DashboardRefreshTests` pins it (trigger 1 within 10 s, trigger 6 whose rows
  all follow tabs: no pass, no line).
- **WD36** (17.4 W5, 17.6). **W5 is two methods on fresh copies**: `Check_W5_cancel_all` (steps 1 to
  4: a run, Cancel all, F5 under the hold) and `Check_W5_row_cancel` (steps 5 and 6: a new start, a run
  in which docs' own Cancel stops docs only). As one method it makes about 220 git processes, about 4
  minutes of git at 17.6's rate under load, past the hang watchdog's 3 minutes. The row's Cancel needs
  nothing from Cancel all, so 17.4's "second run (`Arm()` first)" becomes a first run on a fresh hold;
  two runs under one hold, `Arm()` between them, are `FetchHoldTests`' (17.2).
- **WD37** (2.10, 17.2). **A check asserts what a trigger decided at once, on the UI thread**:
  `DashboardSnapshot()` gives each row's `IsReading` (its one outstanding read, running or waiting for
  a slot, 2.10) and the dashboard's `IsPassRunning`, and `DashboardSnapshot(workspaceId)` reads a
  dashboard that is not active. A trigger starts its reads on the UI thread, so right after the input
  and a settle "no row reading, no pass" proves that it started none, where the call log cannot (it
  records a call only when it ends) and a wrong read would also satisfy the later steps (2.10: a row
  with an outstanding read starts no other, so W8's step 9 would wait for step 8's read). The call-log
  and app-log assertions stay as a second check. W1 step 10, W8 steps 7 and 8, W9 step 4.

### Part K (KD1 to KD23)

- **KD1** (design 5.4, 5.5, 5.6). **Source order.** The commands' order is 5.4's table, row by row,
  the labels of a row in the order it lists them; 5.6's "the catalogue's order" for an empty box is
  read as that order, not 6.1's. The items' order is 5.5's first table (local branches with the
  checked-out first, then the left panel's order; remote branches in the refs' order; tags; stashes;
  remotes; the selection; the working changes; the open diff; the page's row), then the window's
  items (tabs, repositories, workspaces). Disabled commands keep their places in an empty box: 5.6's
  "enabled before disabled" ranks matches of a query only.
- **KD2** (5.4, 3.8). **A repository's commands' detail** is the repository's name, while
  `NamesRepositoryInDialogs` holds, for 5.4's rows 1 to 10 on a repository tab only; on a dashboard
  `Refresh` and the other commands have no detail (`Close tab`'s is its own, the tab's title).
- **KD3** (5.4 row 16, 6.5). **`Go to the next dashboard` with one dashboard open and active** names
  that dashboard's workspace (`Web`) as its detail: "the next one after the active tab among them,
  wrapping" is itself, and running it changes nothing.
- **KD4** (5.5). **`Open repository {name}`'s detail** is the folder as saved (a workspace's path in
  `workspaces.json`, Windows separators on Windows), cut at its end by the `TrimmedTextBlock` with the
  whole folder as `ItemStatus`. A workspace repository whose folder does not exist (old-site) is listed
  too, as 5.5 words it; no check runs it.
- **KD5** (17.3, 17.4 K12 and K13, 17.6). **K12 and K13's dashboard are two methods each.** K12's part
  A (tabs, regions, the commit, pages, undo) and part B (menus, remotes, stash, search, pages, dialogs)
  each run at most six operations. K13's dashboard as one method ran a Fetch all of eight fetch
  operations (four at once), two passes over nine rows (the start's, F5's), Tools' read and the reads
  of three activations, about 220 git processes (part W's measure: 73 for a start, 6 or 7 per row's
  refresh), the count at which part W splits W5 (its WD36); so method 2 (Fetch all, Mod+Shift+D
  round, the shared results) and method 3 (F5, the list's keys, the consumed keys, Mod+J, Enter, Mod+1,
  Delete), about 150 each, as part W's W4, plus up to about 60 when an activation finds the rows 10 s
  old. Part K has eight methods (K10 two, K11 one, K12 two, K13 three), not 17.3's six. With every part
  as written the walk-through has 47 methods ("Conventions of every part"), above 17.3's 40, at which
  17.3 moves part L to `VisualCommit.VisualTests.Phase6Look` (the lead's ruling 1).
- **KD6** (17.4 K13). **"Ctrl+Z in a text box undoes typing only" is K12 part A's step 11**, where Undo
  is enabled (`Undo commit "Keys only"`): in K13's resolver and dashboard states Undo is disabled, so
  the assertion could not fail there.
- **KD7** (6.6). **A region with nothing focusable is skipped**: the right panel's
  `DetailsPlaceholder` (no commit selected) holds no focusable element, so F6 from the graph with
  nothing selected passes it; K12 selects the working-changes row with the graph's Home first.
- **KD8** (6.6, phase 3 design 4c). **Inside a region the keyboard moves by Tab**: from `FilterBox`,
  Tab reaches the left panel's rows in their order, each row's button a stop (the section header, the
  folder, the branch), as Avalonia's Tab navigation of the view gives it. No earlier check presses Tab
  there and a template may add a stop (a focusable scroll viewer of `RefList`'s), so K12 part B presses
  Tab until `main` has the focus, at most six times, and asserts the order of the stops it names
  (`Local branches`, `feature`, `main`), not their count.
- **KD9** (6.12, 17.2). **Keys are pressed as literal gestures**, not through `PressShortcut`.
- **KD10** (2.10). **The read pass counts every row it read**, a row in a folder state included
  (old-site's check returned): `Web: read 10 repositories in {ms} ms`.
- **KD11** (1.10, 2.7). **A row moved by Alt+Up or Alt+Down stays selected and keeps the focus**, as
  phase 4's interactive list's row does.
- **KD12** (3.4, 2.6). **Two dashboards share a repository's summary-mode view model**, so a row of
  the second shows the first's results in its Operation cell (`Fetched from all remotes` in Tools after
  Web's Fetch all).
- **KD13** (5.3). **The box's placeholder is 490.1 px** at 12.5 (0.8's measure; 5.3 estimated "about
  443"), and fits the 582-wide box less its padding: no change to the text or the box.
- **KD14** (5.7). **A disabled entry's reason is drawn at the detail's size, 11.5**, in
  `VcTextDisabledBrush`, and is the row's tooltip; an enabled row has a tooltip only when its label is
  cut.
- **KD15** (5.2, 5.8). **A press outside the card cancels on the press** and is handled, so neither
  the press nor its release reaches the control under it (the graph's selection unchanged).
- **KD16** (6.5, 5.2). **Mod+Shift+D without a dashboard** opens the palette with `workspace` in its
  box, the caret at its end, the text not selected; `Command palette opened` is logged as for any
  opening.
- **KD17** (6.6). **The keyboard focus frame is the theme's own** (Fluent's `FocusAdorner` with
  `SystemControlFocusVisualPrimaryBrush` and `…SecondaryBrush`, a focused text box's border in
  `TextControlBorderBrushFocused`, an activity log row's own `:focus-visible` look); no app token is
  added and the checks assert which element has the focus, not the frame's colours.
- **KD18** (17.4). **Part K's pictures are 17** (17.4 estimated 14): six for K10, four for K11, five
  for K12, two for K13. None repeats another part's: Fetch all's end state is W4's `w4b-fetched`,
  Fetch's tooltip with its key L31's `l31f-enabled-tooltip`.
- **KD19** (17.4). **The values the checks choose**: K11's change `src/core.txt` "core\nmain
  work\nmore\n" and summary `Note more core work`; K12's change "core\nmain work\nkeys\n", summary `Keys
  only` (commit `9522dae`), stash message `Experiment again` (stash `9b4f935`), the filter text `fe`,
  the search query `alpha`; K10's queries `zzz`, `delete main`, `check out feature/alpha`, `create
  branch`, `open repository web-app`, `open workspace web`, the wheel at (550, 297) and the rows clicked
  at (550, 185) and (550, 157); K13's second workspace "Tools" (id `tools`, infra and scratch); the
  empty area's point (484, 360) and the press outside the card at (480, 600).
- **KD20** (6.3, 5.8). **Mod+Shift+P while the palette is the top dialog selects the box's whole text**
  (`SelectionStart` 0, `SelectionEnd` its length) and leaves the query until typing replaces it.
- **KD21** (17.4 K12). **"Down and Enter in a branch's menu"** is `main`'s menu: Down twice, the
  second passing over a separator to `Push main to origin/main`, then Enter, so that the push the key
  starts is a harmless one (`Everything up-to-date`).
- **KD22** (17.4 K10's RW). **The real window's F6 starts from `FilterBox`** (Ctrl+Shift+E first), so
  its target, `GraphRows`, does not depend on where the tab switch left the focus.
- **KD23** (17.4 K11, 6.8). **K11 pictures Push's tooltip with its key**, `Push main to
  origin/main (Ctrl+Shift+K)`, where 17.4 proposes Fetch's: part L's L31 pictures Fetch's in both
  themes (`l31f-enabled-tooltip`), and Push's text also puts the branch and its upstream into 6.8's
  words. Fetch's tooltip, help text and key are asserted structurally in K11 step 2.

### Part S (SD1 to SD30)

- **SD1** (17.3, 17.4, 17.6). **Methods and starts**: thirteen test methods, fourteen app starts (the
  table in the conventions): S14 is two methods by window size (a resize inside a method would picture
  a card laid out once at 1100×700 and then stretched, which proves less than a start at 1920×1080),
  S15 two (the second the restart), S18 two (split at the repository's identity, the second starting
  from the global file the first writes, SD3), S19 three (split before and after the rebasing pull,
  the second and third starting from the `.git/config` the first writes, SD15); the others one each.
  So no method passes about 85 git processes (the run-time paragraph): S18 as one method made about
  119 and S19's steps 1 to 6 as one about 123, some 145 s each at 1.2 s per process, near the hang
  watchdog's 3 minutes under the gate's load. Against design 17.3 (part S 9 methods) and 17.4 (S 28 pictures): **4 methods
  and 14 pictures more** (42 pictures, about 67 s more of the UI thread at 4.8 s per picture than
  17.6's 700 to 900 s holds); with every part as written the walk-through has **47 methods**
  ("Conventions of every part"), past the 40 at which 17.3 moves part L into
  `VisualCommit.VisualTests.Phase6Look`. Folded into 17.3 and 17.6 (the lead's ruling 1).
- **SD2** (conventions; phase 5's neutral point). **The rest point** is (760, 687) at 1100×700 and
  (760, 1000) at 1920×1080: at 1920×1080 the neutral point (760, 687) lies inside the settings card
  (x 530 to 1390, y 200 to 880), over its controls; (760, 1000) is on the backdrop below the card and,
  without a dialog, in the graph area below every scenario's last row.
- **SD3** (8.6, 17.1, 17.2). **The seeds of the session's global file**: S14 and S18 part A `[user]` /
  `⇥useConfigOnly = true` (S14 too: without it the repository scope's in-effect line would read git's
  guess from the machine where its auto-detection succeeds, a CI runner with a full host name, 8.6);
  S18 part B the file part A's step 4 leaves, `[user]` / `⇥useConfigOnly = true` / `⇥name = Ann
  Example` / `⇥email = ann@example.com` (the same bytes, asserted there);
  S19 (all three parts) `[user]` / `⇥name = Test Author` / `⇥email = author@example.com` / `[pull]` / `⇥ff = only` (17.4's
  "an identity" as the scenario's own author, so that the rebased commit is the scenario facts'
  `b4253bb`); S20 `[mergetool "slow"]` / `⇥cmd = true` (17.1's `mergetool.slow.cmd`, a command that
  does nothing; no check runs it). S15, S16, S17 and S21 seed nothing.
- **SD4** (8.6, 17.4 S18). **The identities typed**: global `Ann Example` / `ann@example.com`; the
  repository's `Bo Local` / `bo@example.org`; refused `Ann <Example>`; warned about `ann.example.com`.
- **SD5** (17.4 S18). **The changes and messages**: in each part `notes.txt` `one` `two` `three`
  before the start, staged by `Stage all`; summary `Add a third note`, no description. The two commits
  have the same parent (`73f8cad`) and tree (`d8e00c9`); their SHAs follow from D72's dates and the
  identity: `3e519fb…` as Ann Example, `ead110c…` as Bo Local (derived on 2.36 and 2.55).
- **SD6** (7.7). **A Pick dialog opens** with its filter box focused (phase 3's) and the option of the
  field's value as it stands, edited or saved, selected: `None` with no editor, `Not set` with no
  `merge.tool`, the tool's name with one (`slow`; `araxis` once chosen and not yet saved, S20 step 4).
- **SD7** (7.7, 8.10). **The merge-tool options' order**: `Not set`; the tools git found, in the
  list's order; the user-defined tools, in the read's order; the tools git knows but did not find, in
  the list's order; the current value last when none of these holds it. 7.7 names the groups in this
  order and no order within them.
- **SD8** (8.4). **`{repository} sets its own: {value}.`**, shown in the global scope under the field
  it concerns (`UserNameOrigin`, `UserEmailOrigin`, `PullNotices`, `MergeToolNotices`), secondary:
  `{repository}` the repository's name as its tab is titled; `{value}` the value itself for the name,
  the email and the merge tool, and for the pull mode the label the repository scope's radio shows
  (`Rebase`; `Other: pull.rebase = preserve`). 8.4 leaves `{value}` open for a pair of keys.
- **SD9** (7.1, 7.8). **The section's error dot** shows while its section shows an error: an edit that
  corrects the section's last error removes it at once (S16 step 4, S18 step 4); showing another
  section keeps it (S14 step 9 onwards would keep Tools'); Save validates again. A warning
  (`UserEmailWarning`) neither marks the section nor stops Save (8.6).
- **SD10** (7.8). **A save of global keys only shows no result in the status bar**: it is no
  operation of any queue (8.3), so the status bar keeps what it showed; a save with the repository's
  keys shows its operation's result, `Saved the repository's settings`. 7.8 names no result for the
  global writes.
- **SD11** (11.4, 11.14, 17.2, 17.4 S15). **The two flips after the click** are made on the fake
  itself, `app.SystemTheme.Set(true)` then `Set(false)` (`ShellDriver.SystemTheme`, the session's
  `FakeSystemTheme`, on the UI thread), each followed by a wait for the theme service's log line
  (`The system's theme changed to dark; the app keeps the dark theme.`, then `…to light; the app keeps
  the dark theme.`, 11.4 rule 3) and the layout's settle: 17.2's `SetSystemThemeAsync(dark)` waits
  until the window shows the variant asked for, which a Dark setting never shows for light, so it
  would end on its timeout. They are proven by captures compared byte for byte with `s15c`'s in
  memory, not saved: they have no names of their own and `Shot` refuses a name saved twice.
- **SD12** (17.4 S15, 11.5, 11.7). **The restart method's first start chooses System on the settings
  screen** (`Light` clicked first, to show that a radio changes nothing until Save, 7.8) rather than
  writing `settings.json`, so the screen's Save of a theme (11.7's second paragraph) has a picture-free
  proof here, and the second start is a restart in D32's sense.
- **SD13** (17.4 S21). **S21 is one start, on first, then off**: the refusals change nothing, so after
  the refused `Check out vendor` the switch is turned off on the screen in the same session (7.9: the
  next such operation reads it) and the older commit's refusal comes before the checkout of `vendor`,
  which overwrites `secret.env` and would leave no ignored file in the way. 17.4 orders it off first,
  then on "on a fresh copy". G22 pictures the same refusal on its own copy (`g22a-vendor-blocked`), so
  S21 asserts it without a picture.
- **SD14** (17.4 S16, 7.3). **S16 starts from `settings.json` with `autoFetchMinutes` 120 and
  `stashIncludeUntracked` false**, so that the file's value above 60 is shown and the stash setting's
  effect is seen both ways (its default is on, so "on: the box checked" alone would prove nothing);
  one save is made by Enter in `AutoFetchMinutesBox` (7.8: "Enter saves, also from a text box").
- **SD15** (17.4 S19). **S19 parts B and C start with the repository's `pull.rebase = true` and
  `pull.ff = true` written before the start**, the state part A's step 4 leaves (asserted there byte
  for byte), so that the rebasing pull runs in a method of its own (SD1) and `Same as global` unsets
  both on a copy of its own.
- **SD16** (12.5, 12.10, 17.4 S14). **S14's validation state** is Tools' `Other…` with `Browse…`
  answering a program that does not exist (`C:\Tools\Missing\Ed.exe`; `/opt/tools/missing/ed` off
  Windows): `There is no file at this path.` at Save, with the section's dot. 17.4 asks for "a
  validation state"; S16 and S18 picture General's and Git's.
- **SD17** (17.4 S14's RW mark). **S14's real-window picture** is `s14a-general` (General at
  1100×700, dark) on the scripted twin's session and seed; the launch also reads `SettingsButton`'s
  help text and key and presses a real Ctrl+,.
- **SD18** (8.7). **The in-effect line names the key that decides the mode**: `pull.rebase` for
  Rebase and Rebase keeping merge commits (8.7's example, though Rebase writes `pull.ff = true` too),
  `pull.ff` for Fast-forward only, Merge commit and, with `pull.rebase` unset or false, Merge.
- **SD19** (7.4). **`GitReading` is not pictured**: the reads take about 0.1 s; every check waits for
  its end (`OpenSettingsAsync`).
- **SD20** (12.1, 17.4 S17). **The file menus S17 checks**: a commit file's (`ForCommitFile`), an
  unstaged file's, a staged file's and the staged diff's (`ForDiff`); each one's last group is asserted
  exactly. Every other builder shares `FileItems` (12.1) and is `FileMenuEditorTests`'.
- **SD21** (6.11). **The Keyboard shortcuts rows' actions** are 6.11's group table's names (`Close the
  diff, a page or the search bar`, not 6.1's longer second-table text with its parenthesis; `Next
  region` without its section number), their keys 6.1's.
- **SD22** (17.4 S20). **S20 sets and unsets `merge.tool` in the global scope** (the section opens
  there, D200), with the resolver open before the screen opens; `Merge tool…`'s state is read from
  `ResolverMergeTool`'s `IsEnabled` and tip, not from a picture of its tooltip (phase 4 asserts the
  tip, a disabled button showing none).
- **SD23** (17.4 S17). **`.py` is added through `RunnableExtraBox`** as a single entry; `run.py` is
  opened from Initial commit's file list (the scenario's only `.py` file, in no change).
- **SD24** (11.8, 17.4 S15). **`s15d-restarted-first-frame-light` varies by nature**: whether git has
  been found and the tab has loaded at the first frame (phase 0's `06c` precedent); only the variant
  and the three samples are asserted.
- **SD25** (17.4 S18). **The name's refusal is shown in the global scope before the first save**, with
  the email's warning in the same picture, rather than last.
- **SD26** (7.2, 17.4 S14). **The palette's entries S14 runs**: `Open settings…`, `Show keyboard
  shortcuts…` and `Choose an editor…` (7.2's three), each by its words typed and Enter.
- **SD27** (D190, 8.6). **S18's repository identity is saved while the global one stays**, so the
  global scope's `sets its own` notices and the repository's own in-effect line show side by side; no
  check unsets the global identity.
- **SD28** (7.1, 7.4). **S14's session has an empty second tab**, so the disabled `This repository`
  and its reason are shown in the same method as the enabled one.
- **SD29** (8.7, 8.1, 7.8 step 6). **Pull's reader runs with the screen's other reads**: on a
  repository's tab whose HEAD is on a branch (8.7 gives no in-effect line for a detached HEAD or an
  unborn branch), `IGitRepository.ReadPullConfigurationAsync(<b>)` starts when the screen opens, beside
  8.1's two reads and 8.6's identity read, whatever section the screen opens on, and again in every
  read back (7.8 step 6, beside the reads it names), so that `PullInEffect` always speaks of the reads
  it sits beside, also when a Save leaves the screen open (7.8 step 8); a scope switch, a section
  change or a radio's click reads nothing. Its calls are Pull's, in its order (8.9; the conventions'
  "The screen's reads" lists them): three to five in the working tree. 8.7 named the reader for
  `PullInEffect` but not when it runs; folded into 8.7 (the lead's ruling 5).
- **SD30** (8.4, 8.2). **The read back compares within the written scope**: for each changed key, the
  last entry of the scope written, in that scope's read (local scope in the repository read, global
  scope in the outside read: 8.2's value of the scope), against what was written; a key unset is done
  when that scope holds no entry for it. Read literally, 8.4's `GitConfigRead.Winner` is the last entry
  over every scope of the read, so a repository key that `Same as global` unsets would stay decided by
  the global entry beneath it (S19 step 7: the global `pull.ff = only`), which is what `Same as global`
  asks for and must not hold the screen open. 8.4's two "Still" cases are files of the written scope
  that the app does not write (an include read later, the XDG file); another scope's entry keeps its
  informational notice (8.4's table) and holds nothing open. Folded into 8.4 (the lead's ruling 5).

### Part G (GD1 to GD24)

- **GD1** (design 17.3, 17.4). **Six test methods** (one app start each): G22 one; G23 two, part A
  the menus' operations and part B the pull, which needs a state built before the start; G24 one;
  G25 two, part B on a fresh copy for the deletion (GD12). No method runs more than six operations.
  Only this count is binding here. With every part as written (W 11, K 8, S 13, G 6, L 9 scripted)
  the walk-through has 47 methods, past 17.3's 40, so by 17.3's rule part L moves to
  `VisualCommit.VisualTests.Phase6Look` (the lead's ruling 1); part G's class stays in
  `VisualCommit.VisualTests.Phase6`.
- **GD2** (design 9.2, 9.11, 17.4's G22). **The checkout paths G22 takes**: a graph label's
  double-click (`vendor`), the remote branch menu for an existing local branch (`origin/vendor`, D93)
  and for a new tracking branch (`origin/vendor-remote`), the local branch menu's "Create branch
  here…" with its box ticked, the local branch menu's "Check out" (`tools`), and the graph commit
  menu's "Check out this commit" on a commit `main` does not reach (`133a796`: D193's detached
  checkout, named by its short SHA; the automatic stash's checkout of a commit the branch reaches is
  D179's and part S's S21). The others of 9.2's first row ("Stash changes and check out", the drop's
  "Check out", "Check out and merge", "Check out and rebase", the interactive list's, Abort's return,
  the backups list's and the reflog page's "Check out this commit", a tag) are 9.17's
  `RepositoryOperationsKeepIgnoredTests`.
- **GD3** (design 17.4's G22). **The new branch's name** is `vendor-copy`.
- **GD4** (design 9.11; scenario facts 4.2). **"Create branch" starts from vendor's branch menu**: its
  start is `refs/heads/vendor` (`DescribeStartAsync`), the dialog's context `From vendor`, the
  command `git switch --create vendor-copy --no-overwrite-ignore refs/heads/vendor` and the summary's
  name `vendor`. Scenario facts 4.2 give the full SHA as the start with `vendor` in the text; the full
  SHA is the commit menu's start, whose dialog says `From 133a796 "Add the vendor secret"` and whose
  text would name `133a796` (9.11: "the start point as the dialog names it"). Git refuses both the same
  way (TG1).
- **GD5** (design 17.4's G23). **G23 part A's paths**: vendor's left-panel menu for the merge ("Merge
  vendor into main", `MergeMode.Default`) and the rebase ("Rebase main onto vendor", onto vendor's
  SHA); the commit menus of rows 3 and 4 for the pick and the revert; the stash's left-panel menu for
  the apply; the toolbar's Pop for the pop (as phase 5's P27).
- **GD6** (design 9.10, 17.1, 17.5; D196). **The pull is checked in the app** (G23 part B), although
  17.1 and 17.5 leave the pull's merge and rebase to 9.17's tests and 17.4's G23 has no pull: D196
  names "every merge (the pull's included)" and "every rebase (… the pull's)", 9.10 gives the pull its
  own check inside `IntegratePullAsync` and its own dialog, and the wave 1 findings that 0.4 and 9.6
  record (U5's branch only ahead of its upstream with a local merge, the side branch from a commit
  outside the range) are the cases a user cannot see coming. The check takes five pulls on one
  copy, the configuration changed outside the app between them. 9.17's `IntegratePullTests` stay
  (they also cover U5 with the stop-tracking commit inside the range, an unborn branch, and a read
  that fails).
- **GD7** (design 0.4, 9.6). **IG1's construction**: an empty commit on Track the secret
  (`commit-tree` of its own tree) on a branch `side`, merged into `main` with `--no-ff` before the app
  starts. Empty, so that nothing is written to the working tree before the start (the ignored
  `secret.env` stays `LOCAL`), and so that no commit of `origin/main..main` writes the file: only the
  branch point, a parent outside the range, has it, which only 9.6's (5) and (6) see. It is the side
  branch shape of 0.4's row "found while building WP1" with Track the secret as `U0`, Stop tracking the
  secret as `U1` and `origin/main` as `<onto>`, and U5's shape (a branch only ahead of its upstream with
  a merge, which git replays). Under `pull.rebase=true` git's plain rebase would keep the file here; the
  app refuses all the same, as 9.6's last sentence says it does when a boundary commit tracks an
  ignored file on disk. The scenario stays 17.1's; nothing is added to `Phase6ScenarioTests` (the
  construction is the check's own, as phase 5's P27 part B made its stash).
- **GD8** (design 9.10, 9.15, 14.4). **The pull's refusal dialog** is the default display of
  `PullOutcome.IgnoredFilesInTheWay`: `Pull failed`, 14.4's text with `rebase` when the pull chose its
  rebase (`WouldRebase`) and `merge` otherwise, no git output, no command, "Close" only; the status bar
  `Pull failed` in the danger colour.
- **GD9** (design 17.4's G24). **G24's checkout is `tools`'s**, after the merge: an ignored folder
  deleted where the target has a file. S21 (part S) shows `vendor`'s checkout with the setting off
  (scenario facts 4.4), so G24 does not repeat it.
- **GD10** (design 9.1, D221). **"Phase 5 exactly" is asserted on the call log**: with the setting
  off the merge's calls are `git fmt-merge-msg` and the merge without the option, with none of 9.4's
  reads; the checkout has no option and no `check-ignore` follows it. 9.17's
  `RepositoryOperationsKeepIgnoredTests` assert it for every operation of 9.2.
- **GD11** (design 10.4, 17.4's G25). **G25 works in the left panel**: RS0's row 1 draws `main` ✓ and
  `+3`, so `fork/up/main`, `origin/feature/x` and `origin/main` have no drawn label, and the left panel
  has every ref; the drags are phase 3's left-panel drags (check 7, step 5), the folders `up` and `pr`
  opened first.
- **GD12** (design 17.4's G25). **The renamed ref's menu and deletion are on a fresh copy** (part B),
  HEAD on `main`, so that its checkout is disabled with `main is already checked out` and the lease is
  scenario facts 3.3's `8394a5e…`; part A's push leaves fork's `main` at `de4a87c`.
- **GD13** (design 10.4's push guard). **The push dialog's choices**: the remote `origin` clicked (the
  dialog checks the first, `fork`), the name `pr/1` and "Set as upstream" as the dialog fills them.
- **GD14** (design 14.2, D201). **D201's result is longer than the status bar draws** (about 100
  characters, some 590 px at 11.5 px, past `OperationStatus`'s `MaxWidth` 520): the check asserts the
  bound text and the tooltip whole; the picture shows it cut with "…". The design gives no shorter
  text.
- **GD15** (design 10.4, 14; scenario facts 8, item 5; the lead's ruling 3). **G25 asserts
  `upstream/pr/7` in Pull's tooltip after `Check out upstream/pr/7`** (step 12): an upstream is named
  by its remote-tracking ref when the fetch lines map it to one (the first of
  `FetchRefspecs.DestinationsOf(remote, remoteRef)` under `refs/remotes/`, shown without
  `refs/remotes/`), as `git status` names it; only when no line maps it, `{remote}/{remote ref}`. So
  `pr/7`, whose upstream today's `UpstreamText` gives as `upstream/refs/pull/7/head`, shows
  `upstream/pr/7` in Pull's tooltip (`Pull from upstream/pr/7 (Ctrl+Shift+L)`), the push dialog and a
  dashboard row; with the default lines every upstream reads as today. D101 still holds (the three
  values are read; the text is only display). WP-RA builds it in the place that makes `UpstreamText`.
- **GD16** (left panel). **The Remotes section counts remote branches**, as `LeftPanelViewModel` does
  (`remoteBranches.Count`): RS0 shows 5, RS4 4; "ignored" 3, as scenario
  facts 4.1 say.
- **GD17** (exact state). **The status of "ignored" with `--untracked-files=all --ignored`** is `!
  build/out.bin`, `! secret.env` (scenario facts 4.1 write `! build/`, git's form without
  `--untracked-files=all`).
- **GD18** (design 9.6). **Reads run side by side are asserted as a set**: 9.6's (1) to (4) in any
  order, then (5), (6) and `check-ignore` in this order; every other read of TR1 to TR9 in its order.
- **GD19** (pictures). **One picture per refusal dialog and per menu or question a check is about**,
  and one after G24's two operations and G25's push and R2 checkout; G23's steps 11 and 12 and G25's
  steps 5, 6, 10, 11's checkout and 12 are asserted without a picture. 26 pictures where 17.4 estimated 14
  for part G (the pull, the second G25 method and G22's detached checkout).
- **GD20** (design 8.9). **The pull's configuration reads** are named in G23 (`git config --get
  pull.ff`, `branch.main.rebase`, `pull.rebase`, `--type=bool` for an uncommon value, in 8.9's order,
  before the fetch); their subject is part S's (S19).
- **GD21** (design 10.4 #10, D201). **The double-click on an R2 ref whose branch exists** gives phase
  3's 2.2 result for a local branch that tracks something else or nothing: `Checked out pr/1, which
  has no upstream`.
- **GD22** (waits). **After an outside write of `.git/config`** the check waits for the app's next refs
  read (`git config --null --get-regexp ^remote\.` after the mark) and idle, and where the upstream
  changes until Pull's tooltip names it; the pull reads its own configuration at its start (8.9), so
  no wait on a setting's effect is needed.
- **GD23** (design 9.8). **The stash's reads run once per session**: `ReadStashPathsAsync` keeps them
  by the stash's SHA, so the pop's step runs only D179's status read and `check-ignore`; where the
  apply's three reads run (before the operation, by the undo history, or in D179's count) is not
  asserted, only that they precede its `check-ignore`.
- **GD24** (design 10.4 #8, 10.5, 14.1; settled by the lead's ruling 2). **The drop's push onto a renamed remote branch has the
  operation title `Push {A} to {T}`**: `Push topic to fork/up/main`, which G25 step 2 waits for. 10.4
  #8 says for a renamed branch that the "title, running text and result name `{T}`", and 14.1's Title
  column gives `Push {A} to {T}` for this row while it writes "unchanged" for what stays (its error
  title and summary); its "(label unchanged)" says that the drop menu's item keeps its label, which is
  the same words. Today's code titles every push `Push {b}` (`OperationTexts.Push(branch, target)`);
  the lead's ruling 2 settles the difference with the design's words: WP-RA gives this push the title
  `Push {A} to {T}`, and every other push keeps `Push {b}` (the toolbar's, the menus', the push
  dialog's, the drop's onto an identity-mapped branch: 10.4's Identity column "as today", and 16.1
  names no earlier check whose title changes); a failure of it would show `Push topic to
  fork/up/main failed`.

### Part L (LD1 to LD24)

- **LD1** (11.14, 17.2's `ShotInBothThemesAsync`). **The token pixel of each pair.** "One sampled light
  token" is the status bar's background at the neutral point (760, 687), `VcChromeBackgroundBrush`
  (`#20242C` / `#EBEDF1`); while a dialog with a backdrop is open, the top dialog card's background 10 px
  inside its top-left corner, `VcPanelBackgroundBrush` (`#1A1D24` / `#F5F6F8`), since the backdrop blends
  everything else. The pair also asserts `ThemeSwitch`'s icon and tooltip, the two log lines, `settings.json`'s
  `"System"` and "the screen still up" as the conventions say.
- **LD2** (11.14's L26, which names no workspace or recent list). **L26's data folder**: the recent list of
  phase 1's `10a` (`graph` newest, then `linear`) and one workspace, "Web" from the workspace copy (ten
  folders, so its tooltip shows the eight-name cut and `, …`); no tab, so the one empty tab shows the page.
- **LD3** (13.1). **The welcome page's position** is asserted as the stack's vertical centre at the graph
  area's (y 381 ± 1) while the page does not scroll, rather than as absolute y values, which 13.1 gives only
  as an estimate (`33 px higher`); the inspection compares the layout once per theme with 13.1.
- **LD4** (11.14's L27, "three rows selected" and "a branch menu, 'Delete feature/alpha…' under the
  pointer"). **The three rows are 5, 6 and 7** (Alpha one, Add core, Initial commit), and the danger item is
  reached from the label `feature/alpha` of row 4, which is outside that selection: a right-click on a label
  of a multi-selected row keeps the selection and gives the several-commits menu (phase 3's 4b), so rows
  including row 4 would show no branch menu.
- **LD5** (11.14's L27, "a disabled toolbar button's" and "a disabled menu item's" tooltips). **Stash** on
  "ops" (`There are no changes to stash`, phase 3's `23b`) and **`Delete main…`** in `main`'s label menu
  (`You cannot delete the branch you are on`, phase 3's `02c`), whose menu also shows phase 6's key column
  (`Ctrl+Shift+L`, `Ctrl+Shift+K`) in both themes.
- **LD6** (11.14's L27, "Create branch…", "the push dialog", "Manage remotes…"). The name dialog from the
  toolbar's `Branch` (phase 3's check 23, `From 88d6b91 "Main work"`); the push dialog from `feature/ff`'s
  label menu (`Push feature/ff…`, one remote, so `To` is text); the remotes dialog from the `Remotes`
  section header's menu; each closed unconfirmed.
- **LD7** (11.14's L28, "after 'Discard changes', then Restore"). **`docs/todo.md` (`# To do\n`) is written
  untracked before the start**, as phase 5's U16 part C does: in H0 the only change is `src/Reader.cs`, and
  discarding it would leave no working changes, so the working-changes row and with it the stage panel and
  its restore bar would go. The discard is the diff toolbar's `Discard file`; the backup is the session's
  first, `discard-20260102T090001000Z`, at `0bd36b6`.
- **LD8** (11.14's L28, "two commits, Ctrl+click"; "All files"). The comparison of phase 5's F12 step 1 (row
  7, then Ctrl+click row 0); `All files` on row 0, `c0bc2dd` (phase 5's B7 step 2).
- **LD9** (11.14's L28, "`feature/compare` hidden and another branch soloed"). **`main` is the soloed
  branch** ("history" has no other local branch), soloed after `feature/compare` is hidden (Hidden is kept in
  solo mode, phase 5 design 8.1), with both `feature` folders opened so that the eye-off and solo toggles of
  the local and the remote rows show.
- **LD10** (11.14's L28, "inline diff with C# syntax colours"). The colours asserted are those phase 2's report
  measured (`public`, `int`, `=>`); the doc comment's and the type names' scopes are not asserted.
- **LD11** (11.14's L29). The pages are opened from the `Local branches` header's menu (phase 5's B8, B9),
  not by Ctrl+Shift+R, which part K covers.
- **LD12** (11.14's L30, "the scan's checklist ... on a folder holding repositories"). **L30's scan runs on the
  full "Web"**, whose ten rows `l30a` needs, so every one of the nine candidates is `Already in this
  workspace` (checked, disabled) and the button is the disabled `Add repositories` (1.9: "also when every row
  found is already in the workspace"); W1 pictures the checklist with eight to add (dark).
- **LD13** (11.14's L30, "a row's menu"). **old-site's** row menu: it has disabled items, `Locate…` and the
  folder-state reason, where web-app's (W2's picture) has only enabled ones.
- **LD14** (11.14's L31, "l31a-activity-log (open)"; 11.8). **The toolbar's Fetch runs before the log opens**, in
  L31 and in L34 alike: on "ops" no operation runs at the start (the clean-up runs only when something is
  due), so the log would open empty; one `Fetch` row shows the success glyph's colour, and the frozen record
  clock makes its time and duration the same in both harnesses.
- **LD15** (11.8's "'Delete feature/alpha…' from the branch's menu"). **From the left panel's row** (Local
  branches › feature › alpha), which selects nothing (D114), in L31 and L34 alike, so that the twins show the
  same graph; the pointer goes to the neutral point before each capture and before each key that opens
  the palette or the settings screen, in both harnesses (the palette's highlight follows the pointer, 5.8).
- **LD16** (11.14's L31). The first opening of the settings screen is by `SettingsButton` (proving the
  button in both themes); Light is chosen and saved through Ctrl+, and `Save`, and the single
  `l31d-settings-light` is taken after opening it again by Ctrl+, (L34's way).
- **LD17** (11.14's L31, "the merge tool's Pick stacked on it once 8.10's lookup has ended"). The Pick dialog
  is opened from the global scope's `Choose…`, which is enabled at once with `ToolCatalogues.Fixed` (no wait,
  no kept list); its options are 7.7's in 12.9's fixed order.
- **LD18** (11.14's L31 "auth", "the fetch held at its percentage"). **The hold is at 60**
  (`HoldNextFetchPack(60)`, D150's split), so the status bar's text is fixed (`Counting objects 60%`) where
  phase 3's unsplit hold varied between 40 and 60; the held fetch is `Fetch origin` from the remote's menu and
  the waiting operation is the toolbar's `Fetch` (phase 3's check 31); after Cancel the waiting Fetch runs and
  asks again (no credential helper stores the answer), and the check cancels that prompt. A port fallback
  (`PortWasTaken`) replaces 47393 (part L's 47394) in every expected text through `WithPort` and is noted in the report (D97).
- **LD19** (11.12). **"Only colours change"** is asserted as: the tooltip's bounds equal (±0.5) in a pair's two
  pictures and its background, border and text the token values of each variant; that Fluent's old colours
  are gone is asserted in both pictures, each with its own variant's old background and text: in the dark
  one no pixel `#2B2B2B` or `#FFFFFF` inside the tooltip's rectangle, in the light one none `#F2F2F2` or
  `#000000` (no light token of a tooltip, nor of what lies behind part L's tooltips, has either). Fluent's
  old borders (about `#1C1C1C` / `#D0D0D0`) are not looked for, their values being approximate, nor are the
  dark values in the light picture, where `#FFFFFF` is the window's own colour and can show at the
  tooltip's rounded corners.
- **LD20** (13.2, 13.5; 17.4's L32, "both themes; the clone error likewise"). L32 runs as the sweep (System,
  the fake dark) so that its pictures come in pairs by the same helper; its URL and folders are phase 1's
  checks 11 and 14's, its expected git text the whole cleaned error (`CloneErrorTextAsync`, step 2), not
  check 14's first `fatal:` line; a line of the clone error that ends one of the text's own lines (a
  line feed) is exempt from "ends with a separator or a space"; "none is `C:`" is asserted as "no line ends
  with `C:` or `'C:`".
- **LD21** (13.3, 17.4's L33, which says neither how the nine repositories open nor how one method has two
  window sizes). **One start at 1000×700** with a restored session of the dashboard and the seven repositories
  of natural width 120 (`design-system`, 139, and `notes` left out), `scratch` active; the window then set to
  1100 wide as phase 1's check 17 sets it; `design-system` and `notes` opened by the palette's `Open repository
  {name}` (a new tab at the end, D212); the eleventh to fourteenth tabs from "+". Every natural width is at
  least 120, so the order changes no width (13.3); the 9- and 10-tab states are asserted without pictures.
- **LD22** (13.4, which gives a dashboard tab's title room at 97 and 80 only). At 89 or 88 (`Web` 20 or 19 px)
  and 82 or 81 the title is asserted cut with "…" (`TrimmedTextBlock`), whole in its tooltip; no other repository
  title's cut is asserted but `web-app`'s and `design-system`'s, whose widths the scenario facts give.
- **LD23** (11.8). **L34's steps repeat L31 part A's in its order up to each capture** (the Fetch, the log
  closed, the `feature` folder opened, the question cancelled, the palette closed), so that each twin shows
  the same state; L34 picks nothing in the Git section and saves nothing.
- **LD24** (17.4's "about 66" pictures for part L). Part L takes **68 scripted pictures** (29 pairs and the
  single `l31d-settings-light` for the sweep, L32's two pairs, L33's five single pictures, dark) and L34 four
  captures with four difference pictures.

## Found by the gate

Filled in when the gate runs: the faults the gate found, and the commit that fixed each.

## Could not run

Filled in when the gate runs: the checks or parts of the gate that could not run on the Windows
machine either, why, and what that leaves unproven. A real-window check that was not run only
because the session was not on Windows does not belong here: it stays "Not run: needs Windows" in the
table, and the gate is unfinished until a Windows session has run it.

## Other platforms

Filled in when the gate runs: the CI run of the gate's commit and its result in each job (the three
platforms and the two bare Ubuntu containers), and which of the screenshots CI took on macOS and Linux
were opened, among them the light pictures of the sweep (L26 to L31), whose fonts differ there
(design 17.7, step 4).

## macOS checklist for the owner

To try by hand on a Mac (the scripted run covers macOS on CI, headless, with Control as its command
modifier and a fake system theme; these are what only a person on a Mac sees):

1. **System follows the Mac's appearance** (D194, design 11.8, 11.9): choose System in the settings
   screen's General (or the palette's `Follow the system's theme`), then switch System Settings >
   Appearance between Light and Dark while the app runs: the window and its open views follow within
   a second, with no restart; the Theme switch's tooltip reads `Switch to the dark theme (stops
   following the system)` while light, and a click on it leaves System. Quit and start again with the
   Mac dark: the first frame is already dark.
2. **The keys with Cmd** (D192, design 6.1): Cmd+Shift+P opens the palette, whose keys, like the menus'
   and the tooltips', read `Cmd+…` (`Fetch all remotes` `Cmd+Shift+F`); Cmd+, opens the settings
   screen, whose Keyboard shortcuts list shows the macOS column (`Cmd+Shift+P`, `Option+Up`,
   `Cmd+Delete`); Cmd+T, Cmd+W (the tab, not the window), Cmd+1 to Cmd+9; Ctrl+Tab and Ctrl+Shift+Tab
   switch tabs (Cmd+Tab stays the system's); Cmd+Enter commits from the message box; Cmd+Z, Cmd+Shift+Z
   and Cmd+Y undo and redo a git action outside a text box; Cmd+R and F5 refresh; F6, Shift+F6 and
   Shift+F10 need Fn unless the function keys are set as standard keys; Cmd+Q and Cmd+H stay the
   system's.
3. **On a dashboard** (D187, design 2.7, 6.9): Cmd+Shift+F runs Fetch all and Cmd+Shift+D goes to the
   next dashboard; in the list Enter opens the selected repository beside the dashboard, Option+Up and
   Option+Down move it, Cmd+Delete (Cmd and the key labelled "delete") removes it without a question.
4. **The editor detection** (D226, design 12.2, 12.3): with Visual Studio Code, Sublime Text or a
   JetBrains IDE in /Applications or ~/Applications, Tools > Choose… lists each with the program inside
   its bundle (such as `Visual Studio Code.app/Contents/Resources/app/bin/code`); TextEdit is listed,
   Xcode only when /Applications/Xcode.app exists; once one is chosen, a file menu's `Open in editor`
   (after `Open file`) opens the file there, and Other… with Browse… takes a program from the Mac's
   file dialog.
5. **The merge tools** (D202, design 8.10): Git > Merge tool > Choose… lists the tools git knows (the
   first lookup on the Mac may take a while; later openings are ready at once), and a tool chosen there
   enables the conflict resolver's `Merge tool…` without reopening it.
6. **The Mac's folder dialog** (design 1.7 to 1.9, 13.2): a dashboard's `Add repository…` on a folder
   holding several repositories gives the scan's checklist, its folder line breaking only after a `/`;
   the welcome page's error for a long folder that is not a repository breaks likewise, nothing cut off.
7. **Open file's question** (design 12.8): a file marked as a program, and a file whose extension was
   added under Also ask for, asks before it opens.

**By hand on Windows at acceptance** (D194, design 11.8; the real-window pass never changes the
desktop's theme): with System chosen in the settings screen, switch Windows' app mode (Settings >
Personalization > Colors > Choose your app mode) while the app runs. The window, its title bar and the
open views follow within a second. Switch back.

## The lead's rulings on this report's open points

The lead ruled on the five points the assembly left open (2026-10-19). The report above follows the
rulings, and the design takes them in.

1. **The project's size** (design 17.3, 17.4, 17.6, D230; settles WD6, KD5, SD1, GD1, GD19 and LD24's
   counts). By 17.3's own rule (past 40 methods) part L moves whole to its own walk-through project
   `tests/VisualCommit.VisualTests.Phase6Look`, which links the base, the support and the common
   expectations of `VisualCommit.VisualTests.Phase6` as `Phase5Undo` links `Phase5`'s and saves its
   pictures in phase 6's one folder; the real-window project's link pattern
   (`..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs`) covers both. `Phase6` keeps parts W,
   K, S and G (38 methods), `Phase6Look` part L (9). WP-V's part L creates the project (WP-S's
   recipe); the lead adds it to `CLAUDE.md`'s Commands and `plan.md`'s gate step 3 when it exists.
   The design's 17.3, 17.4 and 17.6 take this report's counts ("Conventions of every part"): 47
   methods, 49 app starts, 198 scripted pictures, 26 real-window captures in 8 methods and 9 launches;
   17.6's estimate takes the about 200 s more of UI-thread time that the 42 pictures beyond 17.4's 156
   add at phase 5's 4.8 s each.
2. **GD24 stands.** The drop's push onto a renamed remote branch is titled `Push {A} to {T}` (`Push
   topic to fork/up/main`, which G25 step 2 waits for), as design 10.4 #8 and 14.1 say; every other
   push keeps `Push {b}`. WP-RA builds it.
3. **GD15: an upstream is named by its remote-tracking ref** when the fetch lines map it to one (the
   first of `FetchRefspecs.DestinationsOf(remote, remoteRef)` under `refs/remotes/`, shown without
   `refs/remotes/`), as `git status` names it; only when no line maps it, `{remote}/{remote ref}`. So
   `pr/7`, checked out from `upstream/pr/7`, shows `upstream/pr/7` in Pull's tooltip, the push dialog
   and a dashboard row; with the default lines every upstream reads as today. D101 still holds (the
   three values are read; the text is only display). WP-RA builds it in the place that makes
   `UpstreamText`. G25 asserts `upstream/pr/7` in Pull's tooltip after the checkout (step 12). Design
   10.4 and 14 gain the rule.
4. **K10's `k10e`** (design 5.3). A palette entry without a key shows no key column: the key's
   `TextBlock` is hidden, its 16-px gap with it, and the detail runs to the row's padding (design 5.3
   gains the sentence). `k10e` stays listed as varying by nature: its detail shows a part of a
   temporary folder's name (K10's Notes).
5. **The design amendments** of WD35 (2.10), SD29 (8.7), SD30 (8.4), WD4 and WD5 (2.6) are accepted:
   each is folded into its design section as the decision words it, with the decision's id in
   brackets.
