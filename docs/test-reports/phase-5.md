# Phase 5 visual test report — History, traceability and undo

The Check, Steps and Expected columns of the table below were written at the start of the phase,
before the UI they test was built (D15), from `requirements.md`, `plan.md`, `docs/phase-5-design.md`
and decisions D153 to D182. The scenario facts were derived first, from the scenario repositories
as the merged scenario code builds them; the checks were then drafted part by part, each part
reviewed independently and revised. Every git state in them was derived with Git 2.36 on copies of
the scenario repositories built by the scenario code itself, whose SHAs equal those
`Phase5ScenarioTests` (and, for the earlier scenarios, the earlier phases' scenario tests) pin, and
every git text was replayed with Git 2.55. Values the design leaves open are chosen in "Decisions
this report makes", for the lead to fold into the design. The result columns are filled in when
the gate runs.

How to read it: "What the checks share" holds what every check relies on (the conventions of every
part, the scenario facts, then each part's conventions, scenarios and graphs, and the texts the
design fixes); "Checks" is the one table of all 27 checks, each named by its part's letter and its
number in design 15.4; "What proves each Delivers item" maps the plan's list to checks and tests,
with every destructive action of phases 2 to 4 and the check that undoes or restores it; "Changes
to expected results" lists what phase 5 changes in earlier phases' checks on purpose; "How the
expected git states were derived" says how every git state in the checks was obtained; "Decisions
this report makes" lists the values the report chose, each with the design section it fills. "RW"
marks a check, or steps of it, that the real-window pass repeats; "n/a" says why it does not.

## Run

Filled in when the gate runs, for each of its two passes: date, app commit, the machine (its
operating system and version), the display scaling of the real-window pass, the screenshot
folders, and how many pictures were inspected. The gate's two passes are run on one commit by a
session on the Windows machine.

Phase 5's scripted checks are in two projects (design 15.3, D181), both run by `dotnet test` at the
repository root beside the earlier ones: `tests/VisualCommit.VisualTests.Phase5` (parts A, B, F and
P) and `tests/VisualCommit.VisualTests.Phase5Undo` (part U). Both save their pictures under
`artifacts/visual/phase-5/scripted/`, which the first picture of a run empties once for both
projects. The real-window pass (`dotnet test --project tests/VisualCommit.RealWindowTests -c
Release`) saves its captures of the RW steps, its difference pictures and `run.txt` under
`artifacts/visual/phase-5/real-window/`. Nearly every picture of phases 1 to 4 changes too ("Changes
to expected results"): before the gate the lead makes their reference pictures again on `master`
and the inspection compares them as design 15.7 says; the Run records that comparison as well.

If a cloud session ran the scripted pass earlier, its run is listed here too, marked "cloud
session, before the gate", and its results stand in the Scripted column until the Windows session
replaces them with its own. It does not count as the gate's scripted pass.

## What the checks share

### Conventions of every part

What every part relies on. Each part's own conventions follow under its heading and add to these;
where a part's convention is narrower, it says so.

- **Parts and projects** (design 15.3, 15.4, D181). **A** the shell and the activity log (A1 to
  A4), **B** the graph area's pages and "All files" (B5 to B9), **F** search, compare, solo and
  hide, and dashed stashes (F10 to F15), **U** undo and redo (U16 to U23), **P** protected changes
  and the automatic stash, C12 (P24 to P27). The letters avoid C, so that a check's name never
  reads as the requirement C12. Parts A, B, F and P are in `tests/VisualCommit.VisualTests.Phase5`
  (one class per part, such as `Phase5ChecksB`, `Phase5ChecksF` and `Phase5ChecksP`; expectations
  in `Phase5Expectations.<part>.cs`, which the real-window pass links, and their tests in
  `Phase5ExpectationTests.<part>.cs`); part U is in `tests/VisualCommit.VisualTests.Phase5Undo`,
  which links the support and the common expectations. The real-window pass's code is
  `Phase5RealWindowPass*.cs` and `RealApp.Phase5*.cs`.
- **One test method per app start** (phase 3's rule, `FreshApplication`), each on a fresh copy of
  its scenario, so a check with several starts is several methods; the methods are named in each
  check's row (and, for parts A and U, in their conventions). A check that restarts the app on one
  data folder (A3, B7's second method, F14's part C) is a plain `[Fact]` with two
  `FreshApplication.RunAsync` halves, as phase 0's check 6 and phase 1's check 15 restart it (D32).
  No method comes near the hang watchdog's 3 minutes: A2 and every U check are split into methods
  for that (decisions AD7, UD17), and no U method runs more than six operations.
- **Defaults.** Unless a check says otherwise, a method writes a session with one tab on a fresh
  copy of the named scenario, starts the app at 1100×700 in the dark theme
  (`Phase5Checks.OpenAsync`), waits until the graph has drawn its rows, the status is shown and the
  queue is idle, and leaves the pointer at the neutral point. Each check's row names its scenario,
  window size and theme.
- **The clocks** (design 2.3, D173). Both harnesses freeze the **record clock** at 2026-01-02
  09:00:00 UTC, so every time the activity log shows reads `09:00:00` and every duration `0 ms`
  (the rows' order comes from sequence numbers), and fix the **backup clock**: it judges a backup's
  age against 2026-01-02 09:00:00 UTC and names the first backup the app makes in a session
  `<kind>-20260102T090001000Z`, each later one a second after the last, so the checks assert the
  names of the backups the app makes exactly. `ShellDriver.Start` passes both (`recordClock`,
  `backupClock`); `RealApp.Launch` sets `VISUALCOMMIT_RECORD_CLOCK=frozen` and
  `VISUALCOMMIT_BACKUP_CLOCK=fixed`.
- **The clean-up at opening.** Two scenarios hold backups due for the clean-up: "reflog" (two older
  than 30 days: `Removed 2 backups`) and A2's "conflicts" with old and many backups (`Removed 3
  backups`). On them both harnesses wait, after the repository has opened and before the first
  input, for the background `Clean up backups` record (design 15.2; `ShellDriver`'s
  `WaitForOperationRecordAsync("Clean up backups")`, in the real window the queue's log line
  `<repo>: Clean up backups: Succeeded after <N> ms: <message>`). The clean-up says nothing in the
  status bar (D139). Every other scenario holds no backup due, so its clean-up removes nothing and
  no `Removed backup` note appears.
- **Dates and identities.** Dates are shown in UTC (`VISUALCOMMIT_TIME_ZONE=UTC`, D43). A scenario's
  commits are dated by `TempRepo`'s clock, 2026-01-01 12:00 plus one minute per commit; a step made
  "outside the app" uses the copy's next minute, which each part names. What the app writes has the
  commit date 2026-01-02 09:00 UTC and "Test Author" <author@example.com> as committer (D72): a new
  commit or stash also as author and author date, so it has the same SHA in every run and in both
  harnesses; an amend, a pick and a rebase keep each commit's author and author date. Snapshot
  commits (`discard-…`, `…-changes`) are by VisualCommit <snapshot@visualcommit.invalid> and are
  asserted by their parents, message and content (phase 4's rule); where a check also names a
  snapshot's id (B9's two new `discard-` snapshots), it is the id plain git gives with D72's dates,
  the same on Git 2.36 and 2.55.
- **Rows, lanes and colours** are phase 4's: rows from 0 at the top, as `SelectedIndex` counts
  commit (and stash) rows; while the working-changes row shows it is display row 0 and commit row r
  is display row r + 1. With no banner and no search bar, display row d's vertical centre is
  y 129 + 26 d at 1100×700 and 1920×1080 (the graph control starts at y 116). A row is clicked "in
  its Message column", 30 px into that column: x 468 with one or two lanes, 484 with three, 500 with
  four, 516 with five. Lane L's centre is x 406 + 16 L. Lane colours are numbers (dark: 0 `#7C8CFF`,
  1 `#3FB97F`, 2 `#E0A23B`, 3 `#E5606B`, 4 `#4FB3D9`). The checks take every position from the
  controls' bounds (`GraphRowPoint`, `GraphLabelBounds`, the pages' and the bar's own bounds), as
  D122's rule says; the numbers in the rows are what those bounds give with the default panel
  widths, and each check asserts them ±2 where it states them. A graph is named by a short name
  that its part's scenarios section defines (H0, FL1, RL0, P1, …); the names are unique across the
  report, and phase 3's and 4's names (G0, R0, GC-M, …) keep their meaning.
- **Labels** are listed in phase 1's order (a detached `HEAD`, the branch HEAD is on with ✓, other
  local branches, remote branches, tags, stashes); "drawn" is what the 130-wide Branch / Tag column
  draws at 1100×700 (a name cut with "…", phase 1's "+N"). A label is pressed, right-clicked or
  double-clicked at its centre, and only a drawn one.
- **Left panel.** Paths are written Local branches › feature › alpha, Remotes › origin › main,
  Stashes › On main: Experiment. A right-click on a left-panel row opens its menu only: nothing is
  selected (D114). A folder that does not hold HEAD's branch starts closed; a check opens it first.
- **Menus** are written header first, items in order, "—" for a separator; "(danger)" an enabled
  item in `VcDangerBrush`, "(disabled: "…")" a disabled item with its tooltip, "(highlighted)" the
  item with the focus, which is the first enabled one when a menu opens (D105). A menu's height is
  8 + 24 + 26 × items + 9 × separators (±2), and it lies at least 4 px inside the window; a menu
  opened by a right-click has its top-left corner 2 right of and 2 below the pointer, moved to stay
  inside the window (phase 3's 4a); a menu opened by a button opens at the button's bottom-left
  corner (phase 4's 14.3). A check asserts a menu's position only where it says so.
- **The pointer.** Resting the pointer on an item, a button or a row, "no menu is open" and "no
  dialog" are phase 3's. Before a capture with no menu open both harnesses move the pointer to the
  neutral point (760, 687), unless the step rests it on something; at 1100×700 that point lies in the
  status bar left of the Activity log toggle, at 1920×1080 in the graph area below every phase 5
  scenario's last row. A capture with a menu or a dialog open leaves the pointer where the click
  that opened it was.
- **Waits** are on conditions, never on a fixed time: `WaitForIdleAsync` (its refresh included) and
  the queue's log line `<repo>: <Title>: <outcome> after <N> ms: <message>` for an operation; the
  page, search, comparison, filter and status-read waits each part names; a change made outside the
  app while it runs is followed by a wait on the condition the step names, with
  `OutsideChangeTimeout` (30 s).
- **The toolbar and the status bar** in every phase 5 picture are as part A gives them for an open
  repository (Search and the Activity log toggle enabled; Undo and Redo as the undo history
  stands), and the details panel has part B's `All files` button; a check asserts them where they
  are its subject. Part U writes the toolbar's Undo and Redo states as T0, T+ and T− (its
  conventions).
- **Git's own texts.** The checks quote Git 2.36.0.windows.1's texts (the call log's output, the error
  dialog's `ErrorOutput`, reflog messages) and assert them word for word only when `git --version`
  is 2.36; on any other version a check asserts the line marked "key", that `ErrorOutput` is
  `GitOutputCleaner.Clean` of the failed call's own output, and the dialog's title, summary and
  buttons (phase 3's rule). Every quoted text was replayed with Git 2.55.0.windows.5; where 2.55
  differs (the merge refusals of part P, the Files search's raw output of part F), the check says
  so, and where it came out the same, a part may assert it on 2.55 too (part B's reflog messages,
  which the scenario's own git wrote). Git's states (refs, objects, files, the index, `git status`,
  the stash list, the in-progress
  files, the backups) are asserted on every version. CI's jobs run other gits (Windows 2.56.0,
  macOS 2.56.0, `ubuntu-latest` 2.55.0, the two containers 2.43.0; `status.md`), which this rule
  covers.
- **Pictures** are named by `Phase5Checks.PictureName`'s rule,
  `^[abfpu]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$`: the part's letter, the check's number,
  a step letter (and digits where needed), then words (`a2c-checkout-failed-opened`,
  `u16b-commit-undone`); a check's letters run on through its methods in order. Every name is unique
  in the report. A picture whose content varies by nature from run to run (a blinking caret, an
  indeterminate progress bar, a temporary folder's name, the number of reads) says so in its
  check's Notes.
- **The real window** repeats the steps marked RW in a real process with the real mouse and
  keyboard (FlaUI), finds controls by automation id and name, reads texts through UI Automation
  (a tooltip's text as the control's `HelpText` where the check says so, a text box's or an output
  box's through its Value pattern), and saves each capture under the scripted picture's name in
  `artifacts/visual/phase-5/real-window/`, with a difference picture against its scripted twin and
  the share of differing pixels in `run.txt` (at most 3 percent, as in phases 0 to 4). A capture of
  a state that only the real window reaches is compared with the scripted picture of the same state
  (P25's `p25e-undone`). 1920×1080 does not fit the development machine's screen, so steps at that
  size are scripted only (the plan's conditional part).

### Scenario facts (every part)

The facts of phase 5's four scenarios (and of phase 1's "graph" scenario's stash) that the checks of
every part rely on, read through the app's own git layer and measured in the running app at
1100×700 and 1920×1080, and checked against every pin of `Phase5ScenarioTests` (no difference). How
they were derived is under "How the expected git states were derived". The parts below refer to
them as "scenario facts N" (their section numbers). Each part's own scenarios section repeats what
its checks need, with the columns they use; the two never differ. Positions marked "derived from
the design" are not built yet: the checks take them from the controls' bounds.

#### 0. Conventions used below

- **Rows** count from 0 at the top; they are commit (and stash) rows, `SelectedIndex`'s numbering.
  When the working-changes row shows (history only), it is display row 0 above commit row 0.
- **Lanes** count from 0 at the left; **colours** are lane colour numbers (dark: 0 `#7C8CFF`,
  1 `#3FB97F`, 2 `#E0A23B`, 3 `#E5606B`, 4 `#4FB3D9`, as phase 4's report).
- **Dates** are author dates in UTC as the Date column shows them (`2026-01-01 12:13`); the commit
  date is named only where it differs.
- **Labels** are in drawing order (detached `HEAD`, the current branch with ✓, other local
  branches, remote branches, tags, stashes); "drawn" is what `LabelsInRow` reports at 1100×700:
  a name cut with "…" and "+N" are drawn texts, the full name in brackets.
- **Lines** "lane L (colour C) from row a's node into row b's node" as phase 4's report writes
  them; **dashed** marks a line with `GraphLine.IsDashed` (design 10).

#### 1. Window geometry

Measured in the app (`ShellDriver`, default panel widths, no banner, no search bar, the activity
log closed):

| | 1100×700 | 1920×1080 |
|---|---|---|
| Graph control (`GraphRows`) | window (260, 116), 440 × 558 | window (260, 116), 1260 × 938 |
| Display row d (0 at the top) | y 116 + 26 d to 142 + 26 d, centre **129 + 26 d** | the same |
| Rows wholly in view | 21 (d 0 to 20; d 21 cut at 674) | 36 |
| Lane L's centre | x **406 + 16 L** | the same |
| Branch / Tag column | x 260 to 390; first label from x 266, 18 high, its top 4 below the row's top | the same |
| Graph column | 16 per lane + 16, at least 48: x 390 + that | the same |
| 2 lanes (history, reflog, protect) | Message x 438, 142 wide; Date x 580 to 700; Author, SHA hidden | Message 438, 750 wide; Author 1188 (140); Date 1328 (120); SHA 1448 (72) |
| 3 lanes (filters hidden or soloed, phase 1's graph) | Message x 454, 126 wide; Date 580 to 700 | Message 454, 734 wide; Author 1188; Date 1328; SHA 1448 |
| 5 lanes (filters as built) | Message x 486, 214 wide; **no Date column** | Message 486, 702 wide; Author 1188; Date 1328; SHA 1448 |
| A row "in its Message column" (Message x + 30) | x 468 (2 lanes), 484 (3), 516 (5) | the same |
| Right panel | x 700 to 1100 | x 1520 to 1920 |
| Status bar | y 674 to 700; the neutral point (760, 687) in it | y 1054 to 1080; (760, 687) lies in the graph area below every phase 5 scenario's last row (bottom at most y 506) |

So with no working-changes row, commit row r's centre is y **129 + 26 r**; in "history" (working
row shown) commit row r is display row r + 1, centre y **155 + 26 r**, and the working row's centre
is y 129.

Derived from the design only (none of it is built yet; the checks must take these from the
controls' bounds, as D122's rule says for the banner):

- **Search bar open** (3.1): 36 high under the banner, above the column headers: the graph control
  starts at y 152, display row d's centre **165 + 26 d** (1100×700 and 1920×1080).
- **Activity log open** (2.1, 2.2): the panel spans the graph area's width (x 260 to 700 at
  1100×700; 260 to 1520 at 1920×1080), 200 high by default above the status bar, with a 5-high
  splitter above it: at 1100×700 panel y 474 to 674 (header 474 to 504, then rows of 24: row k's
  centre y 516 + 24 k), splitter 469 to 474, graph control 116 to 469 (353 high: display rows 0 to
  12 whole, 13 cut). At 1920×1080: panel 854 to 1054, splitter 849 to 854, graph control 116 to 849
  (733 high). The minimum height is 120, the maximum the graph area's height less 160 (decision AD1 reads it
  as less 165, so that the content above the 5-px splitter keeps 160: 421 at 1100×700).
- **Pages** (4, 5, 11): in the graph area (x 260 to 700 and y 88 to 674 at 1100×700; x 260 to 1520
  and y 88 to 1054 at 1920×1080), header 36 (y 88 to 124), rows 26 from y 124: a page row k's
  centre is y **137 + 26 k** if no column header row is added (the design names none; decision BD1
  adds none). The file
  history page is 440 wide at 1100×700 (< 760: list above, 40 % of the height, at least 3 rows; diff
  below) and 1260 at 1920×1080 (side by side: list 45 % = 567, diff 55 % = 693). The blame gutter is
  130 wide at 1100×700 (page < 700) and 200 at 1920×1080.

#### 2. "history" (`Scenarios.HistoryAsync`, folder and tab `history`)

##### 2.1 The scenario and its graph

Three authors: Test Author <author@example.com>, **Mira Kovac** <mira@example.com>, **Jonas
Berg** <jonas@example.com> (author and committer alike, so commit date = author date). HEAD on
`main`; `main` tracks `origin/main`, `feature/compare` tracks `origin/feature/compare`, both 0 and
0. `origin` is the bare `origin.git` next to the working tree (heads `main`, `feature/compare`).
`feature/readme` was deleted after its merge. No stash, no tag.

**H0, "history" as built.** 2 lanes, Graph column 48. The working-changes row "Working changes" is
display row 0 (ring in lane 0, colour 0, its dashed line to row 0's node); commit rows start at
y 142.

| Row | Labels (drawn at 1100×700) | Node: lane, colour | Message | Date | SHA | Author | y centre at 1100×700 |
|---|---|---|---|---|---|---|---|
| 0 | `main` ✓, `origin/main` (drawn `main` ✓ x 266 to 317, `+1` x 321 to 345) | 0, 0 | Read whole lines | 2026-01-01 12:13 | `c0bc2dd` | Jonas Berg | 155 |
| 1 | | 0, 0 | Stop tracing | 2026-01-01 12:12 | `20d7fbe` | Test Author | 181 |
| 2 | | 0, 0 | Write new notes | 2026-01-01 12:11 | `e765529` | Mira Kovac | 207 |
| 3 | | 0, 0 | Merge branch 'feature/readme' | 2026-01-01 12:10 | `f513566` | Test Author | 233 |
| 4 | | 1, 1 | Explain the reader | 2026-01-01 12:09 | `c1b6375` | Jonas Berg | 259 |
| 5 | | 0, 0 (lane 1 ends here) | Remove the notes | 2026-01-01 12:08 | `884b462` | Test Author | 285 |
| 6 | `feature/compare`, `origin/feature/compare` (drawn `feature/comp…` x 266 to 359, `+1` x 363 to 388) | 1, 2 | Compare whole lines | 2026-01-01 12:07 | `245c395` | Jonas Berg | 311 |
| 7 | | 0, 0 | Rename the parser to reader | 2026-01-01 12:06 | `0575c7f` | Mira Kovac | 337 |
| 8 | | 1, 2 | Add the comparer | 2026-01-01 12:05 | `d0d3c77` | Jonas Berg | 363 |
| 9 | | 0, 0 (lane 1 ends here) | Add tracing | 2026-01-01 12:04 | `14a5c6c` | Test Author | 389 |
| 10 | | 0, 0 | Handle empty input | 2026-01-01 12:03 | `365482b` | Jonas Berg | 415 |
| 11 | | 0, 0 | Add the logo | 2026-01-01 12:02 | `775113f` | Test Author | 441 |
| 12 | | 0, 0 | Add the parser | 2026-01-01 12:01 | `1d5a7cf` | Mira Kovac | 467 |
| 13 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `7829335` | Test Author | 493 |

Lines: lane 0 (colour 0) runs from row 0 to row 13; lane 1 (colour 1) from row 3's node (the
merge's second parent) into row 4's node, then into row 5's node; lane 1 (colour 2) from row 6's
node through row 7 into row 8's node, then into row 9's node. Node x: lane 0 406, lane 1 422. A row
clicked "in its Message column" at 1100×700 and 1920×1080: (468, 155 + 26 r). At 1920×1080 the same
rows, labels and y, every column shown.

Full SHAs: Read whole lines c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9; Stop tracing
20d7fbe203a4a8ceeb5ef00da13ceda91ed70739; Write new notes e765529af7bef5e017fb41b2de5e384d1eebc858;
Merge branch 'feature/readme' f5135668320ac7e48c60dd634a18f4eb3906f160 (parents 884b462 c1b6375);
Explain the reader c1b63754e14cab0e146b0811ca11371ba2bbce55; Remove the notes
884b4622b22f86236cbaae78b04234e63b4a6a88; Compare whole lines 245c395674db6ae89390be2db018db5356b446fe;
Rename the parser to reader 0575c7f6631b3ba6c9d623f53b28489405230cfd; Add the comparer
d0d3c77aa36bc7ad3ef71cff57e454453d4463b2; Add tracing 14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c;
Handle empty input 365482be62488b4c1259f0fb7dfa1631c797470b; Add the logo
775113f4ae456d7dc7dc5380c669b0d2950289e2; Add the parser 1d5a7cf999727e7f724054348c81866a62025184;
Initial commit 7829335c71bb8f1161b668a29afb602632228425.

Messages: every commit's message is its subject alone except Handle empty input (body `The
tokenizer stops at an empty text now.`) and Compare whole lines (body `It splits the texts with the
tokenizer first.`). No subject holds "tokenizer".

Left panel when it opens: Local branches `2` (folder `feature`, closed; `main`), Remotes `2`
(`origin` › `feature` closed, `main`), Pull requests `0`, Tags `0`, Stashes `0`. `CurrentBranch`:
`main`.

##### 2.2 Working tree

`git status --porcelain`: ` M src/Reader.cs` only (nothing staged, nothing untracked or ignored).
Its diff against HEAD (blob 7061deb → 3056624), two hunks:

```
@@ -1,6 +1,6 @@
 namespace History;
 
-/// <summary>Reads the words of a text.</summary>
+/// <summary>Reads the lines of a text.</summary>
 public sealed class Reader
 {
     private readonly string _text;
@@ -19,4 +19,6 @@ public sealed class Reader
 
         return _text.Split('\n');
     }
+
+    public int Length => _text.Length;
 }
```

##### 2.3 Each commit's change and tree (the details panel; `ReadCommitDetailsAsync`, `ReadTreeFilesAsync`)

| Commit | Changed files (against the first parent) | All files (tree) |
|---|---|---|
| `c0bc2dd` Read whole lines | (1) M src/Reader.cs | (6) LICENSE, README.md, assets/logo.png, docs/notes.md, src/Config.cs, src/Reader.cs |
| `20d7fbe` Stop tracing | (1) M src/Config.cs | the same 6 |
| `e765529` Write new notes | (1) A docs/notes.md | the same 6 |
| `f513566` Merge branch 'feature/readme' | (1) M README.md | (5) LICENSE, README.md, assets/logo.png, src/Config.cs, src/Reader.cs |
| `c1b6375` Explain the reader | (1) M README.md | the same 5 |
| `884b462` Remove the notes | (1) D docs/notes.md | the same 5 |
| `245c395` Compare whole lines | (1) M src/Comparer.cs | (7) LICENSE, README.md, assets/logo.png, docs/notes.md, src/Comparer.cs, src/Config.cs, src/Parser.cs |
| `0575c7f` Rename the parser to reader | (1) R src/Reader.cs (from src/Parser.cs; git `R084`) | (6) LICENSE, README.md, assets/logo.png, docs/notes.md, src/Config.cs, src/Reader.cs |
| `d0d3c77` Add the comparer | (2) M README.md, A src/Comparer.cs | the same 7 as `245c395` |
| `14a5c6c` Add tracing | (1) A src/Config.cs | (6) LICENSE, README.md, assets/logo.png, docs/notes.md, src/Config.cs, src/Parser.cs |
| `365482b` Handle empty input | (1) M src/Parser.cs | (5) LICENSE, README.md, assets/logo.png, docs/notes.md, src/Parser.cs |
| `775113f` Add the logo | (1) A assets/logo.png | the same 5 |
| `1d5a7cf` Add the parser | (1) A src/Parser.cs | (4) LICENSE, README.md, docs/notes.md, src/Parser.cs |
| `7829335` Initial commit | (3) A LICENSE, A README.md, A docs/notes.md | (3) LICENSE, README.md, docs/notes.md |

So with "All files" on, `main`'s commit shows `All files (6)`: src/Reader.cs with `M`, the other
five without a letter. LICENSE is the same blob in every commit and only Initial commit adds it.

##### 2.4 File history (`ReadFileHistoryAsync`; newest first, no merges)

| Opened for | Start | Rows: status, short SHA, subject, path at that commit, author, date |
|---|---|---|
| src/Reader.cs (stage panel, or `c0bc2dd`'s file) | `c0bc2dd` (HEAD) | M `c0bc2dd` Read whole lines, src/Reader.cs, Jonas Berg, 12:13 · R `0575c7f` Rename the parser to reader, src/Reader.cs from src/Parser.cs (git `R084`), Mira Kovac, 12:06 · M `365482b` Handle empty input, **src/Parser.cs**, Jonas Berg, 12:03 · A `1d5a7cf` Add the parser, **src/Parser.cs**, Mira Kovac, 12:01 (4 rows) |
| src/Reader.cs from `0575c7f`'s file | `0575c7f` | R `0575c7f`, M `365482b`, A `1d5a7cf` (3 rows; header `from 0575c7f`) |
| docs/notes.md | HEAD | A `e765529` Write new notes · D `884b462` Remove the notes · A `7829335` Initial commit (two lives) |
| README.md | HEAD | M `c1b6375` Explain the reader · A `7829335` Initial commit (the merge `f513566` is not listed) |
| README.md | `feature/compare` | M `d0d3c77` Add the comparer · A `7829335` |
| LICENSE | HEAD | A `7829335` Initial commit (1 row) |
| assets/logo.png | HEAD | A `775113f` Add the logo |
| src/Config.cs | HEAD | M `20d7fbe` Stop tracing · A `14a5c6c` Add tracing |
| src/Comparer.cs | `feature/compare` | M `245c395` Compare whole lines · A `d0d3c77` Add the comparer |
| src/Parser.cs | `feature/compare` or `365482b` | M `365482b` · A `1d5a7cf` |

Each step's diff of src/Reader.cs (the diff part):

- `c0bc2dd` Read whole lines (db5a108 → 7061deb): one hunk `@@ -10,13 +10,13 @@ public sealed class
  Reader`: `-    public string[] Words()` / `+    public string[] Lines()` and
  `-        return _text.Split(' ');` / `+        return _text.Split('\n');`.
- `0575c7f` the rename (`similarity index 84%`, `rename from src/Parser.cs`, `rename to
  src/Reader.cs`, 51e5085 → db5a108): one hunk `@@ -1,11 +1,11 @@`: `-public sealed class Parser` /
  `+public sealed class Reader`, `-    public Parser(string text)` / `+    public Reader(string text)`.
- `365482b` Handle empty input (src/Parser.cs, 76b3c45 → 51e5085): one hunk `@@ -7,11 +7,16 @@ public
  sealed class Parser`: `_text = text;` → `_text = text ?? string.Empty;`, and five lines added
  (`if (_text.Length == 0)`, `{`, `return [];`, `}`, an empty line).
- `1d5a7cf` Add the parser: a new file of 17 lines (`@@ -0,0 +1,17 @@`).

##### 2.5 Blame (`ReadBlameAsync`)

**src/Reader.cs at `main` (`c0bc2dd`)**, 22 lines. Per line: commit, author, date:

| Lines | Commit | Author | Run's date (`yyyy-MM-dd`) | Text of the run's lines |
|---|---|---|---|---|
| 1–3 | `1d5a7cf` Add the parser | Mira Kovac | 2026-01-01 | `namespace History;` / (empty) / `/// <summary>Reads the words of a text.</summary>` |
| 4 | `0575c7f` Rename the parser to reader | Mira Kovac | 2026-01-01 | `public sealed class Reader` |
| 5–7 | `1d5a7cf` | Mira Kovac | 2026-01-01 | `{` / `    private readonly string _text;` / (empty) |
| 8 | `0575c7f` | Mira Kovac | 2026-01-01 | `    public Reader(string text)` |
| 9 | `1d5a7cf` | Mira Kovac | 2026-01-01 | `    {` |
| 10 | `365482b` Handle empty input | Jonas Berg | 2026-01-01 | `        _text = text ?? string.Empty;` |
| 11–12 | `1d5a7cf` | Mira Kovac | 2026-01-01 | `    }` / (empty) |
| 13 | `c0bc2dd` Read whole lines | Jonas Berg | 2026-01-01 | `    public string[] Lines()` |
| 14 | `1d5a7cf` | Mira Kovac | 2026-01-01 | `    {` |
| 15–19 | `365482b` | Jonas Berg | 2026-01-01 | `        if (_text.Length == 0)` / `        {` / `            return [];` / `        }` / (empty) |
| 20 | `c0bc2dd` | Jonas Berg | 2026-01-01 | `        return _text.Split('\n');` |
| 21–22 | `1d5a7cf` | Mira Kovac | 2026-01-01 | `    }` / `}` |

12 runs (0 to 11), alternating backgrounds starting with run 0. Per commit: `1d5a7cf` path
src/Parser.cs, no `previous` (disabled "Blame before this commit": `This is the file's first
commit`); `0575c7f` path src/Reader.cs, previous `14a5c6c` src/Parser.cs; `365482b` path
src/Parser.cs, previous `775113f` src/Parser.cs; `c0bc2dd` path src/Reader.cs, previous `20d7fbe`
src/Reader.cs. Lines whose path differs from src/Reader.cs (`1d5a7cf`'s and `365482b`'s) come from
src/Parser.cs (the tooltip's `as src/Parser.cs`). Times: `1d5a7cf` 12:01, `0575c7f` 12:06, `365482b`
12:03, `c0bc2dd` 12:13; e-mails as in 2.1.

**src/Reader.cs in the working tree**, 24 lines, 15 runs: as at `main` except line 3 (`/// <summary>Reads
the lines of a text.</summary>`) and lines 22–23 (empty, `    public int Length => _text.Length;`),
which are not committed yet (40-zero SHA, author `Not Committed Yet` <not.committed.yet>, summary
`Version of src/Reader.cs from src/Reader.cs`, previous `c0bc2dd` src/Reader.cs); old line 22 `}` is
line 24 (`1d5a7cf`). Runs: 1–2 `1d5a7cf`, 3 uncommitted, 4 `0575c7f`, 5–7 `1d5a7cf`, 8 `0575c7f`,
9 `1d5a7cf`, 10 `365482b`, 11–12 `1d5a7cf`, 13 `c0bc2dd`, 14 `1d5a7cf`, 15–19 `365482b`, 20
`c0bc2dd`, 21 `1d5a7cf`, 22–23 uncommitted, 24 `1d5a7cf`. **The uncommitted lines' time is git's
wall clock in the local zone** (2026-10-15 13:17 UTC, `-0700`, in this run), not the scenario's or
the app's date (see 7, item 1).

**"Blame before this commit"** from a run:

- from `0575c7f`'s run → src/Parser.cs at `14a5c6c` (Add tracing), 22 lines, 5 runs: 1–9 `1d5a7cf`, 10
  `365482b`, 11–14 `1d5a7cf`, 15–19 `365482b`, 20–22 `1d5a7cf` (line 4 `public sealed class
  Parser`, line 8 `    public Parser(string text)`, line 13 `    public string[] Words()`, line 20
  `        return _text.Split(' ');`);
- from `c0bc2dd`'s run → src/Reader.cs at `20d7fbe` (Stop tracing), 22 lines, 9 runs: 1–3 `1d5a7cf`,
  4 `0575c7f`, 5–7 `1d5a7cf`, 8 `0575c7f`, 9 `1d5a7cf`, 10 `365482b`, 11–14 `1d5a7cf`, 15–19
  `365482b`, 20–22 `1d5a7cf` (the same as at the rename `0575c7f`);
- from `365482b`'s run → src/Parser.cs at `775113f` (= Add the parser's content): 17 lines, one run
  `1d5a7cf`.

Other files at `main`: LICENSE 6 lines, one run `7829335` Initial commit (Test Author, boundary);
README.md 5 lines: 1–3 `7829335`, 4–5 `c1b6375` Explain the reader (Jonas Berg; previous `884b462`
README.md; the side branch's commit, not the merge); docs/notes.md 3 lines, one run `e765529` Write
new notes (Mira Kovac; no previous: its second life starts there); src/Config.cs 6 lines, one run
`14a5c6c` Add tracing; assets/logo.png is binary (NUL bytes): `logo.png is a binary file: it has no
lines to blame`.

##### 2.6 Search (over H0's 14 rows; `SearchAsync` for the git part)

| Mode, text | Matches (row, SHA, subject) | Count when nothing is selected |
|---|---|---|
| Commits `tokenizer` (any case) | 6 `245c395` Compare whole lines, 10 `365482b` Handle empty input (message bodies only) | `2 matches` |
| Commits `Jonas` | message: none; author (in memory): 0 `c0bc2dd`, 4 `c1b6375`, 6 `245c395`, 8 `d0d3c77`, 10 `365482b` | `5 matches` |
| Commits `Mira` / `kovac` | author: 2 `e765529`, 7 `0575c7f`, 12 `1d5a7cf` | `3 matches` |
| Commits `parser` | message: 7, 12 | `2 matches` |
| Commits `notes` | message: 2, 5 | `2 matches` |
| Commits SHA prefix `0575` (4 to 40 hex) | 7 `0575c7f` | `1 match` |
| Files `parser` | 7 `0575c7f` (the rename's old name), 10 `365482b`, 12 `1d5a7cf` | `3 matches` |
| Files `reader` (any case, also `Reader.cs`) | 0 `c0bc2dd`, 7 `0575c7f` (the new name) | `2 matches` |
| Files `notes` | 2, 5, 13 | `3 matches` |
| Files `readme` | 3 `f513566` (the merge: its first-parent change touches README.md), 4, 8, 13 | `4 matches` |
| Files `src/` and `src\` | 0, 1, 6, 7, 8, 9, 10, 12 | `8 matches` |
| Files `logo` | 11 | `1 match` |
| Changes `TraceEverything` (any case) | 1 `20d7fbe` Stop tracing (removed), 9 `14a5c6c` Add tracing (added) | `2 matches` |
| Changes `Width` | 9 | `1 match` |
| Changes `Lines` | 0 `c0bc2dd`, 3 `f513566` (the merge's first-parent change adds `The reader splits a text into lines.`), 4 `c1b6375` | `3 matches` |
| Changes `tokenizer` | none | `No matches` |

No two rows share a 4-hex prefix. First two characters per row: 0 c0, 1 20, 2 e7, 3 f5, 4 c1, 5 88,
6 24, 7 05, 8 d0, 9 14, 10 36, 11 77, 12 1d, 13 78.

##### 2.7 Compare (`CompareAsync`)

| From → To | Kind | Base | Files | Counts |
|---|---|---|---|---|
| `0575c7f` Rename (row 7) → `c0bc2dd` Read whole lines (row 0) | two commits, two-dot | trees, old `0575c7f` | (4) M README.md, M docs/notes.md, M src/Config.cs, M src/Reader.cs | none |
| `c0bc2dd` → `0575c7f` (swapped) | two-dot | old `c0bc2dd` | the same 4, all M (contents reversed) | none |
| `d0d3c77` → `245c395` | two-dot | | (1) M src/Comparer.cs | |
| `14a5c6c` → `20d7fbe` | two-dot | | (4) M README.md, M docs/notes.md, M src/Config.cs, R src/Reader.cs from src/Parser.cs | |
| `245c395` (row 6) → `c0bc2dd` (row 0) | two-dot | | (5) M README.md, M docs/notes.md, D src/Comparer.cs, M src/Config.cs, R src/Reader.cs from src/Parser.cs | |
| `main` → `feature/compare` ("Compare feature/compare with main") | branches, three-dot | merge base `14a5c6c` Add tracing | (2) M README.md, A src/Comparer.cs | only on feature/compare 2, only on main 7: `2 commits only on feature/compare, 7 only on main` |
| the same with `All differences` | two-dot of the tips | old `c0bc2dd` | (5) M README.md, M docs/notes.md, A src/Comparer.cs, M src/Config.cs, R src/Parser.cs from src/Reader.cs (git `R067`: main's rename reversed) | the same counts |
| `feature/compare` → `main` | three-dot | `14a5c6c` | (4) M README.md, M docs/notes.md, M src/Config.cs, R src/Reader.cs from src/Parser.cs | only on main 7, only on feature/compare 2 |
| `main` → `origin/feature/compare` | three-dot | `14a5c6c` | as `feature/compare` | 2 / 7 |
| `main` → `main` | | | none: `Both are c0bc2dd: nothing to compare` | |

Three-dot README.md diff (`14a5c6c` 30ac432 → `245c395` f950130): `@@ -1,3 +1,5 @@`, adds an
empty line and `Compare two texts with Comparer.`. Two-dot `0575c7f`→`c0bc2dd` src/Reader.cs (db5a108
→ 7061deb): the same hunk as Read whole lines' step.

#### 3. "filters" (`Scenarios.FiltersAsync`, folder and tab `filters`)

##### 3.1 The scenario and its graph

All by Test Author. HEAD on a clean `main` (no untracked, no ignored files). `main` tracks
`origin/main` (0, 0), `feature/beta` tracks `origin/feature/beta` (0, 0), `feature/alpha` tracks
`origin/feature/alpha` (behind 1), `feature/merged` local only. `refs/remotes/origin/HEAD` →
`origin/feature/beta`. Tag `v1` (lightweight) on Add the core. Stashes: `stash@{0}` "On
feature/alpha: Alpha experiment" (`d3f16e8`, base Alpha one `7c60fc4`, 12:14) and `stash@{1}` "On
feature/beta: Beta experiment" (`c8f3db3`, base Beta six `89f891e`, 12:13).

**FL0, "filters" as built.** 5 lanes, Graph column 96: at 1100×700 the columns are Branch / Tag,
Graph, Message (214) and no Date.

| Row | Labels (drawn at 1100×700) | Node: lane, colour | Message | Date | SHA | y centre |
|---|---|---|---|---|---|---|
| 0 | `stash@{0}` | 0, 0 (ring) | On feature/alpha: Alpha experiment | 12:14 | `d3f16e8` | 129 |
| 1 | `stash@{1}` | 1, 1 (ring) | On feature/beta: Beta experiment | 12:13 | `c8f3db3` | 155 |
| 2 | `feature/beta`, `origin/feature/beta` (drawn `feature/beta`, `+1`) | 1, 1 | Beta six | 12:12 | `89f891e` | 181 |
| 3 | | 1, 1 | Beta five | 12:11 | `ac357ab` | 207 |
| 4 | `origin/feature/alpha` | 2, 2 | Alpha two | 12:10 | `e4d9976` | 233 |
| 5 | | 1, 1 | Beta four | 12:09 | `1cdde63` | 259 |
| 6 | `main` ✓, `origin/main` (drawn `main` ✓, `+1`) | 3, 3 | Merge branch 'feature/merged' | 12:08 | `4c0aa3c` | 285 |
| 7 | | 3, 3 | Main work | 12:07 | `ef7bfda` | 311 |
| 8 | | 1, 1 | Beta three | 12:06 | `0c13077` | 337 |
| 9 | `feature/merged` | 4, 4 | Merged work | 12:05 | `629bc39` | 363 |
| 10 | | 1, 1 | Beta two | 12:04 | `8fc77ea` | 389 |
| 11 | `feature/alpha` | 0, 0 (lane 2 ends here) | Alpha one | 12:03 | `7c60fc4` | 415 |
| 12 | | 1, 1 | Beta one | 12:02 | `2f6e977` | 441 |
| 13 | `v1` | 0, 0 (lanes 1, 3 and 4 end here) | Add the core | 12:01 | `369f471` | 467 |
| 14 | | 0, 0 | Initial commit | 12:00 | `67e5696` | 493 |

(Dates are 2026-01-01.) Lines: lane 0 (colour 0) **dashed** from row 0's ring down through rows 1
to 10 into row 11's node (Alpha one), then solid from row 11 through row 12 into row 13 and on to
row 14; lane 1 (colour 1) **dashed** from row 1's ring into row 2's node (Beta six), then solid
from row 2 through rows 3 to 12 (nodes at 3, 5, 8, 10, 12) into row 13's node; lane 2 (colour 2)
from row 4's node through rows 5 to 10 into row 11's node; lane 3 (colour 3) from row 6's node into
row 7's node, then through rows 8 to 12 into row 13's node; lane 4 (colour 4) from row 6's node (the
merge's second parent) through rows 7 and 8 into row 9's node, then through rows 10 to 12 into row
13's node. Node x: 406, 422, 438, 454, 470 for lanes 0 to 4. The alpha stash's dashed line lies
alone in lane 0 at x 406 from y 129 to y 415, with no node of its own between (rows 1 to 10 hold no
node in lane 0): the place to sample the dash pattern (F15).

Message clicks at (516, 129 + 26 r). Left panel: Local branches `4` (folder `feature`, closed;
`main`), Remotes `3` (`origin` › `feature` closed, `main`), Pull requests `0`, Tags `1` (`v1`),
Stashes `2` (`On feature/alpha: Alpha experiment`, `On feature/beta: Beta experiment`).
`CurrentBranch` `main`.

Full SHAs: d3f16e894a9e233889e358ddb6bef18db6f42a35 (stash, parents 7c60fc4 f1c3392),
c8f3db3730dfbc63b5a9efeaabf097394b8b7e11 (stash, parents 89f891e 163b928), Beta six
89f891e3d1f5273b565a2076e6e86a8dab81599b, Beta five ac357ab1c799e34af527a1e70ddf39b2c09f7df3, Alpha
two e4d99768331d8969d2acecf85c7e040113f04cd2, Beta four 1cdde63d7ffab3fedbe2fdbfc81f0a4e151eb0ab,
Merge 4c0aa3c354e1cda550d852d90b110d93df6b08cf (parents ef7bfda 629bc39), Main work
ef7bfda666483d15f3388b8b6015895b1aa42e86, Beta three 0c1307714ab2065444193283ddf3e875bf0ebe60,
Merged work 629bc397e58a737cfc39b635ca7240a85b54ea6e, Beta two
8fc77ea6820b42b875b426aa180d79514f1190ef, Alpha one 7c60fc46a4178e4f2f88df0f73c4b51808be230a, Beta
one 2f6e9779338410155ca0429ce8188b49852312d6, Add the core 369f471b9087f5d12eb03bd8f5ebee5d2ae6d9b8,
Initial commit 67e569677152fbdf61c8cb717219d428421312a4. Changed files: each commit adds or changes
its one file (beta/one.txt to beta/six.txt A; src/alpha.txt A then M; src/merged.txt A, and the
merge's first-parent change A src/merged.txt; src/core.txt A then M; README.md A); the alpha stash M
src/alpha.txt, the beta stash M beta/one.txt.

##### 3.2 FL1: `Hide feature/alpha` (with its upstream `origin/feature/alpha`)

Arguments: `--exclude=feature/alpha --branches --exclude=origin/feature/alpha
--exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --`. 12 rows, 3 lanes (Graph 64,
Message x 454, 126 wide, Date 580 to 700). Alpha one, Alpha two and the alpha stash are gone (its
base is hidden); `stash@{1}` keeps its name.

| Row | Labels (drawn) | Node: lane, colour | Message | SHA | y centre |
|---|---|---|---|---|---|
| 0 | `stash@{1}` | 0, 0 (ring) | On feature/beta: Beta experiment | `c8f3db3` | 129 |
| 1 | `feature/beta`, `origin/feature/beta` (`feature/beta`, `+1`) | 0, 0 | Beta six | `89f891e` | 155 |
| 2 | | 0, 0 | Beta five | `ac357ab` | 181 |
| 3 | | 0, 0 | Beta four | `1cdde63` | 207 |
| 4 | `main` ✓, `origin/main` (`main` ✓, `+1`) | 1, 1 | Merge branch 'feature/merged' | `4c0aa3c` | 233 |
| 5 | | 1, 1 | Main work | `ef7bfda` | 259 |
| 6 | | 0, 0 | Beta three | `0c13077` | 285 |
| 7 | `feature/merged` | 2, 2 | Merged work | `629bc39` | 311 |
| 8 | | 0, 0 | Beta two | `8fc77ea` | 337 |
| 9 | | 0, 0 | Beta one | `2f6e977` | 363 |
| 10 | `v1` | 0, 0 (lanes 1 and 2 end here) | Add the core | `369f471` | 389 |
| 11 | | 0, 0 | Initial commit | `67e5696` | 415 |

Lines: lane 0 (colour 0) dashed from row 0's ring into row 1's node, then solid from row 1 to row 11
(passing rows 4, 5 and 7); lane 1 (colour 1) from row 4's node into row 5's node, then through rows
6 to 9 into row 10's node; lane 2 (colour 2) from row 4's node through rows 5 and 6 into row 7's
node, then through rows 8 and 9 into row 10's node. Message clicks at (484, 129 + 26 r).

##### 3.3 FL2: `Solo feature/beta` (with `origin/feature/beta`)

Arguments: `--ignore-missing refs/heads/feature/beta refs/remotes/origin/feature/beta HEAD --`.
The same 12 rows, lanes, colours, lines and positions as FL1; the labels differ: row 4 has `main` ✓
alone (the `origin/main` label is left out), row 7 (Merged work) has **no** label
(`feature/merged`'s is left out); rows 0, 1 and 10 as FL1 (`stash@{1}`, `feature/beta` with `+1`,
`v1`). The alpha stash (`stash@{0}`, base Alpha one) is left out. With `feature/alpha` also hidden,
solo mode gives exactly FL2 (the hidden set waits).

Informative (no check needs them): hiding `feature/alpha` **without** its upstream keeps 15 rows
and 5 lanes, Alpha one and Alpha two stay, and the alpha stash is drawn at row 4 (just below Alpha
two, once its base has been seen as a parent) with a dashed lane 2 down to row 11; hiding
`feature/beta` with its upstream and `origin/HEAD` leaves 8 rows, 4 lanes (stash@{0}, Alpha two,
Merge, Main work, Merged work, Alpha one, Add the core, Initial commit).

#### 4. "reflog" (`Scenarios.ReflogAsync`, folder and tab `reflog`)

##### 4.1 The scenario and its graph

All by Test Author. HEAD on a clean `main` (no stash). `main` tracks `origin/main` (ahead 2),
`topic` tracks `origin/topic` (0, 0), `clash` and `feature/renamed` have no upstream. Tag `v0.8`
(lightweight, Initial commit). `origin` (`origin.git`) has `main` at `6d7a40b` and `topic` at
`362fbd6` only (no `feature/gone`, no tag). The removed remote's folder `mirror.git` still exists
next to the working tree.

**RL0, "reflog" as built.** 2 lanes, Graph 48, Message x 438 (142), Date 580 to 700. No
working-changes row.

| Row | Labels (drawn) | Node: lane, colour | Message | Date (author) | SHA | y centre |
|---|---|---|---|---|---|---|
| 0 | `feature/renamed` | 0, 0 | Draft work | 12:25 | `6f8d627` | 129 |
| 1 | `main` ✓ | 0, 0 | Write the notes | 12:21 (commit date 12:22) | `9dd61fd` | 155 |
| 2 | `topic`, `origin/topic` (both drawn) | 1, 1 | Topic work | 12:02 (commit date 12:09) | `362fbd6` | 181 |
| 3 | | 0, 0 (lane 1 ends here) | Clash notes | 12:05 (commit date 12:08) | `316166a` | 207 |
| 4 | `clash` | 1, 2 | Clash notes | 12:05 | `fe14262` | 233 |
| 5 | | 1, 2 | Clashing work | 12:04 | `3b2501a` | 259 |
| 6 | `origin/main` | 0, 0 | Main work | 12:03 | `6d7a40b` | 285 |
| 7 | | 0, 0 (lane 1 ends here) | Add the app | 12:01 | `7dd8e8f` | 311 |
| 8 | `v0.8` | 0, 0 | Initial commit | 12:00 | `5c9c1cb` | 337 |

Lines: lane 0 (colour 0) runs from row 0 to row 8; lane 1 (colour 1) from row 2's node into row 3's
node; lane 1 (colour 2) from row 4's node into row 5's node, through row 6, into row 7's node.
Message clicks (468, 129 + 26 r). Left panel: Local branches `4` (folder `feature`, closed; `clash`;
`main` ↑2; `topic`), Remotes `2` (`origin` › `main`, `topic`), Pull requests `0`, Tags `1` (`v0.8`),
Stashes `0`.

Full SHAs: Draft work 6f8d6279babf558e68666938b188d1b4ad0298ff, Write the notes
9dd61fde06301292b9b14d0911560b6d987f645d, Topic work 362fbd633bc18c7553bf271ad7d31f1db055af2d,
Clash notes (main) 316166adc8e550ebb513a7f8c0ea99ecbdd88983, Clash notes (clash)
fe14262437a8f9b4785cebdb62c67a5749b54c76, Clashing work 3b2501a2ae5ac34924741649c069cbaae5097077, Main
work 6d7a40b76bb9156c0969e533b292cbe2357e6783, Add the app 7dd8e8f7958138d967be47cb167b84bac7fd7467,
Initial commit 5c9c1cb91ada4146b5fb15a7229c98b6d79dbd58.

**Lost commits** (no branch, tag, remote branch or HEAD reaches them; none is a graph row):

| Commit | Subject | Date | Parent | Kept by |
|---|---|---|---|---|
| `44e646b` (44e646bdcb1d920659f2b28b1b44e19f6ae45a7b) | Doomed work | 12:26 | `9dd61fd` | HEAD@{1}; `branch-…/doomed` |
| `d07c677` (d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f) | Try a shortcut | 12:23 | `9dd61fd` | HEAD@{9}, main@{1}; `reset-20260101T122401000Z` |
| `c7bf508` (c7bf50881e2d9ebc82c5adf905a4bb441b0664bb) | Draft the notes | 12:21 | `316166a` | HEAD@{11}, main@{3} |
| `7e2ce3b` (7e2ce3b66ecd286fc7ce6694d1f57ca6acf4f0d3) | Detached experiment | 12:10 | `316166a` | HEAD@{17}; `detached-…` |
| `f7a69be` (f7a69be38486b8b82fa0f12b0a6c0482af8a0424) | Topic work (before the rebase) | 12:02 | `7dd8e8f` | HEAD@{23}, HEAD@{33}, topic@{1}, origin/topic@{1}; `rebase-…/topic`, `force-push-…/origin/topic` |

##### 4.2 HEAD's reflog (`ReadReflogAsync("HEAD", 500)`)

36 entries, `Skipped` 1 (HEAD@{5}, the null entry the rename of the checked-out branch wrote): the
footer reads `1 entry is not shown: it names no commit that exists`. Lost rows marked: HEAD@{1},
{9}, {11}, {17}, {23}, {33}.

| Selector | Time | Action | Commit | Lost |
|---|---|---|---|---|
| HEAD@{0} | 2026-01-01 12:27 | checkout: moving from doomed to main | `9dd61fd` Write the notes | |
| HEAD@{1} | 2026-01-01 12:26 | commit: Doomed work | `44e646b` Doomed work | yes |
| HEAD@{2} | 2026-01-01 12:26 | checkout: moving from main to doomed | `9dd61fd` Write the notes | |
| HEAD@{3} | 2026-01-01 12:26 | checkout: moving from feature/renamed to main | `9dd61fd` Write the notes | |
| HEAD@{4} | 2026-01-01 12:26 | Branch: renamed refs/heads/draft to refs/heads/feature/renamed | `6f8d627` Draft work | |
| HEAD@{6} | 2026-01-01 12:25 | commit: Draft work | `6f8d627` Draft work | |
| HEAD@{7} | 2026-01-01 12:25 | checkout: moving from main to draft | `9dd61fd` Write the notes | |
| HEAD@{8} | 2026-01-01 12:25 | reset: moving to 9dd61fde06301292b9b14d0911560b6d987f645d | `9dd61fd` Write the notes | |
| HEAD@{9} | 2026-01-01 12:23 | commit: Try a shortcut | `d07c677` Try a shortcut | yes |
| HEAD@{10} | 2026-01-01 12:22 | commit (amend): Write the notes | `9dd61fd` Write the notes | |
| HEAD@{11} | 2026-01-01 12:21 | commit: Draft the notes | `c7bf508` Draft the notes | yes |
| HEAD@{12} | 2026-01-01 12:21 | checkout: moving from topic to main | `316166a` Clash notes | |
| HEAD@{13} | 2026-01-01 12:21 | checkout: moving from main to topic | `362fbd6` Topic work | |
| HEAD@{14} | 2026-01-01 12:20 | reset: moving to 316166adc8e550ebb513a7f8c0ea99ecbdd88983 | `316166a` Clash notes | |
| HEAD@{15} | 2026-01-01 12:12 | reset: moving to HEAD | `316166a` Clash notes | |
| HEAD@{16} | 2026-01-01 12:12 | checkout: moving from 7e2ce3b66ecd286fc7ce6694d1f57ca6acf4f0d3 to main | `316166a` Clash notes | |
| HEAD@{17} | 2026-01-01 12:10 | commit: Detached experiment | `7e2ce3b` Detached experiment | yes |
| HEAD@{18} | 2026-01-01 12:10 | checkout: moving from main to 316166adc8e550ebb513a7f8c0ea99ecbdd88983 | `316166a` Clash notes | |
| HEAD@{19} | 2026-01-01 12:10 | checkout: moving from topic to main | `316166a` Clash notes | |
| HEAD@{20} | 2026-01-01 12:09 | rebase (finish): returning to refs/heads/topic | `362fbd6` Topic work | |
| HEAD@{21} | 2026-01-01 12:09 | rebase (pick): Topic work | `362fbd6` Topic work | |
| HEAD@{22} | 2026-01-01 12:09 | rebase (start): checkout 316166adc8e550ebb513a7f8c0ea99ecbdd88983 | `316166a` Clash notes | |
| HEAD@{23} | 2026-01-01 12:09 | checkout: moving from main to topic | `f7a69be` Topic work | yes |
| HEAD@{24} | 2026-01-01 12:08 | cherry-pick: Clash notes | `316166a` Clash notes | |
| HEAD@{25} | 2026-01-01 12:08 | reset: moving to 6d7a40b76bb9156c0969e533b292cbe2357e6783 | `6d7a40b` Main work | |
| HEAD@{26} | 2026-01-01 12:07 | reset: moving to HEAD | `6d7a40b` Main work | |
| HEAD@{27} | 2026-01-01 12:06 | checkout: moving from clash to main | `6d7a40b` Main work | |
| HEAD@{28} | 2026-01-01 12:05 | commit: Clash notes | `fe14262` Clash notes | |
| HEAD@{29} | 2026-01-01 12:04 | commit: Clashing work | `3b2501a` Clashing work | |
| HEAD@{30} | 2026-01-01 12:04 | checkout: moving from main to clash | `7dd8e8f` Add the app | |
| HEAD@{31} | 2026-01-01 12:03 | commit: Main work | `6d7a40b` Main work | |
| HEAD@{32} | 2026-01-01 12:03 | checkout: moving from topic to main | `7dd8e8f` Add the app | |
| HEAD@{33} | 2026-01-01 12:02 | commit: Topic work | `f7a69be` Topic work | yes |
| HEAD@{34} | 2026-01-01 12:02 | checkout: moving from main to topic | `7dd8e8f` Add the app | |
| HEAD@{35} | 2026-01-01 12:01 | commit: Add the app | `7dd8e8f` Add the app | |
| HEAD@{36} | 2026-01-01 12:00 | commit (initial): Initial commit | `5c9c1cb` Initial commit | |

The graph rows of the commits: `9dd61fd` row 1, `6f8d627` row 0, `362fbd6` row 2, `316166a` row 3,
`fe14262` row 4, `3b2501a` row 5, `6d7a40b` row 6, `7dd8e8f` row 7, `5c9c1cb` row 8; the lost ones
none ("Show in graph" disabled, `This commit is not in the graph`).

**Branches' reflogs** (the reader returns git's `%gD`, `refs/heads/main@{0}`; the page shows
`main@{0}`):

- `main`, 8 entries, none skipped, lost {1} and {3}: {0} 12:25 `reset: moving to
  9dd61fde06301292b9b14d0911560b6d987f645d` `9dd61fd`; {1} 12:23 `commit: Try a shortcut` `d07c677`
  (lost); {2} 12:22 `commit (amend): Write the notes` `9dd61fd`; {3} 12:21 `commit: Draft the notes`
  `c7bf508` (lost); {4} 12:08 `cherry-pick: Clash notes` `316166a`; {5} 12:03 `commit: Main work`
  `6d7a40b`; {6} 12:01 `commit: Add the app` `7dd8e8f`; {7} 12:00 `commit (initial): Initial
  commit` `5c9c1cb`.
- `feature/renamed`, 3: {0} 12:26 `Branch: renamed refs/heads/draft to refs/heads/feature/renamed`
  `6f8d627`; {1} 12:25 `commit: Draft work` `6f8d627`; {2} 12:25 `branch: Created from HEAD`
  `9dd61fd`.
- `topic`, 3, lost {1}: {0} 12:09 `rebase (finish): refs/heads/topic onto
  316166adc8e550ebb513a7f8c0ea99ecbdd88983` `362fbd6`; {1} 12:02 `commit: Topic work` `f7a69be`
  (lost); {2} 12:02 `branch: Created from HEAD` `7dd8e8f`.
- `clash`, 3: {0} 12:05 `commit: Clash notes` `fe14262`; {1} 12:04 `commit: Clashing work`
  `3b2501a`; {2} 12:02 `branch: Created from HEAD` `7dd8e8f`.
- `doomed` (deleted): no entries (`--ignore-missing`, exit 0).
- Not in the page's source menu, for 7.10: `origin/topic` {0} 12:10 `update by push` `362fbd6`, {1}
  12:06 `update by push` `f7a69be` (lost); `origin/main` {0} 12:06 `update by push` `6d7a40b`.

##### 4.3 Backups

`ReadBackupsAsync` lists 17 backups (22 refs). The two dated 2025-11-20 (`reset-20251120T090001000Z`
→ `9dd61fd`, `branch-20251120T090002000Z/old-idea` → `9dd61fd`) are older than 30 days by the
harness's backup clock (2026-01-02 09:00:00 UTC): the clean-up at opening removes exactly those
two (`Removed backup reset-20251120T090001000Z (older than 30 days)`, `Removed backup
branch-20251120T090002000Z (older than 30 days)`), leaving 15. The backups list then shows,
newest first (time from the name; "What" by design 12.3; "Commit" the first ref's commit):

| # | Name | Time | What (12.3) | Commit | Refs (target) |
|---|---|---|---|---|---|
| 0 | `branch-20260101T122701000Z` | 2026-01-01 12:27:01 | `Deleted branch doomed` | `44e646b` Doomed work | `/doomed` → 44e646b |
| 1 | `reset-20260101T122401000Z` | 2026-01-01 12:24:01 | `Before a hard reset to 9dd61fd` | `d07c677` Try a shortcut | → d07c677; `-changes` → 21c5a38 (parents d07c677, f174bf8 "VisualCommit: index before a hard reset"; message `VisualCommit: snapshot before a hard reset to 9dd61fd` / README.md; README.md = `# Reflog\n\nAn edit the hard reset took away.\n`; ^2's tree has src/shortcut.txt) |
| 2 | `discard-20260101T122001000Z` | 2026-01-01 12:20:01 | `Before discarding 2 files` | `9849838` VisualCommit: snapshot before discarding 2 files | → 9849838 (parent 316166a; notes/todo.txt `todo\n` (A), src/app.txt `app\nmain work\nstaged\n` (M)) |
| 3 | `reset-20260101T121901000Z` | 2026-01-01 12:19:01 | `Before a mixed reset` | `316166a` Clash notes | → 316166a; `-changes` → 57a5e6e (`VisualCommit: index before a mixed reset`, parent 316166a, src/app.txt `app\nmain work\nstaged\n`) |
| 4 | `remote-20260101T121801000Z` | 2026-01-01 12:18:01 | `Removed remote mirror` | `316166a` Clash notes | `/mirror/main` → 316166a, `/mirror/topic` → 362fbd6 |
| 5 | `remote-branch-20260101T121701000Z` | 2026-01-01 12:17:01 | `Deleted feature/gone on origin` | `7dd8e8f` Add the app | `/origin/feature/gone` → 7dd8e8f |
| 6 | `remote-tag-20260101T121601000Z` | 2026-01-01 12:16:01 | `Deleted tag v0.8 on origin` | `5c9c1cb` Initial commit | `/origin/v0.8` → 5c9c1cb |
| 7 | `tag-undone-20260101T121501000Z` | 2026-01-01 12:15:01 | `Tag v1.0-rc, its creation undone` | `316166a` Clash notes | `/v1.0-rc` → tag object 7abc965 ("Release candidate") |
| 8 | `tag-20260101T121401000Z` | 2026-01-01 12:14:01 | `Deleted tag v0.9` | `7dd8e8f` Add the app | `/v0.9` → tag object fa4c0b7 ("Version 0.9") |
| 9 | `stash-20260101T121301000Z` | 2026-01-01 12:13:01 | `Dropped or popped stash "On main: Try a README"` | `39daf6c` On main: Try a README | → 39daf6c (parents 316166a 840e499; README.md `# Reflog\n\nTried in a stash.\n`) |
| 10 | `detached-20260101T121101000Z` | 2026-01-01 12:11:01 | `Commits a detached HEAD left behind` | `7e2ce3b` Detached experiment | → 7e2ce3b |
| 11 | `force-push-20260101T120901000Z` | 2026-01-01 12:09:01 | `Before a force push of origin/topic` | `f7a69be` Topic work | `/origin/topic` → f7a69be |
| 12 | `rebase-20260101T120801000Z` | 2026-01-01 12:08:01 | `Before rebasing topic` | `f7a69be` Topic work | `/topic` → f7a69be |
| 13 | `skip-20260101T120701000Z` | 2026-01-01 12:07:01 | `Before skipping a commit` | `6d7a40b` Main work | → 6d7a40b; `-changes` → 946fb4b (`VisualCommit: snapshot before an abort` / src/app.txt; src/app.txt `app\nresolved, then skipped\n`) |
| 14 | `abort-20260101T120601000Z` | 2026-01-01 12:06:01 | `Before aborting` | `6d7a40b` Main work | → 6d7a40b; `-changes` → 08b41a4 (`VisualCommit: snapshot before an abort` / docs/clash.txt, src/app.txt; docs/clash.txt `clash notes\n` (A against 6d7a40b), src/app.txt `app\nmain work and clash\n`) |

Full ids: 08b41a45cd0d6f8571d150651e979bd6be73a9c4, 9849838c39b980387b976a8a864b7c6d1ffda865,
57a5e6edfbd470dc411b4ea3bb4eaabc405aa571, 21c5a38946ba1dc3ce852e135922ae8f676a0ce4,
946fb4b9d6331facefff73c01cdd1e8271276db2, 39daf6c3dee7c1739b2f1753ad2011de84e70373, tag objects
fa4c0b7909d4018e79be918d089b6fdad3dde3a7 and 7abc9657b90c937f6854cb988f34c830461fb289.

**Many backups** (`ReflogManyBackupsAsync`): 53 backups: the 15 young ones above, 36 fillers
`reset-20251220T090001000Z` to `reset-20251220T090036000Z` (each → `9dd61fd`), and the 2 old ones.
The clean-up removes `reset-20251120T090001000Z (older than 30 days)`, `branch-20251120T090002000Z
(older than 30 days)` and `reset-20251220T090001000Z (more than 50 backups)`: 50 remain.

**Conflicts with old and many backups** (check A2: `Scenarios.ConflictsAsync()` then
`AddOldAndManyBackupsAsync`): 53 backups, all of HEAD's commit `da31c97` ("Ours edits"): the 2 old
ones (2025-11-20 09:00:01 and :02) and 51 fillers `reset-20251220T090001000Z` to
`reset-20251220T090051000Z`. `CleanUpBackupsAsync` returns, in this order: `Removed backup
reset-20251120T090001000Z (older than 30 days)`, `Removed backup branch-20251120T090002000Z (older
than 30 days)`, `Removed backup reset-20251220T090001000Z (more than 50 backups)`; 50 remain. The
background operation's message is then `Removed 3 backups` (2.3).

##### 4.4 States after the reflog page's and the backups list's actions (B8, B9)

Each on a fresh copy, with the app's command (`update-ref`, `restore`, `push`) and environment:

- **Create branch `shortcut` at `d07c677`** (HEAD@{9}): 10 rows, 2 lanes: 0 `feature/renamed` Draft
  work (0, 0); **1 `shortcut` Try a shortcut `d07c677` (lane 1, colour 1), 2026-01-01 12:23**; 2
  `main` ✓ Write the notes (0, 0; lane 1 ends here); 3 `topic`, `origin/topic` Topic work (1, 2); 4
  Clash notes `316166a` (0, 0; lane 1 ends); 5 `clash` Clash notes (1, 3); 6 Clashing work (1, 3); 7
  `origin/main` Main work (0, 0); 8 Add the app (0, 0; lane 1 ends); 9 `v0.8` Initial commit. Lines:
  lane 0 colour 0 rows 0 to 9; lane 1 colour 1 from row 1's node into row 2's node; lane 1 colour 2
  from row 3's node into row 4's node; lane 1 colour 3 from row 5's node into row 6's node, through
  row 7, into row 8's node. The new row's centre y 155.
- **Create branch at `44e646b`** (HEAD@{1}) or **Restore branch doomed** (`update-ref --create-reflog
  refs/heads/doomed 44e646b ""`): 10 rows: **0 `doomed` (or the new name) Doomed work `44e646b`
  (0, 0), 12:26**; 1 `feature/renamed` Draft work (1, 1); 2 `main` ✓ Write the notes (0, 0; lane 1
  ends); 3 `topic`, `origin/topic` Topic work (1, 2); 4 Clash notes (0, 0; lane 1 ends); 5 `clash`
  (1, 3); 6 Clashing work (1, 3); 7 `origin/main` (0, 0); 8 Add the app (0, 0; lane 1 ends); 9 `v0.8`.
  No upstream for the restored branch (the config is not in the backup).
- **Restore the discarded files** (`restore --source=<discard> --worktree -- notes/todo.txt
  src/app.txt`): status ` M src/app.txt` (`app\nmain work\nstaged\n`), `?? notes/todo.txt`
  (`todo\n`); the working-changes row appears; the graph otherwise RL0.
- **Restore the changes** of the abort: docs/clash.txt equals HEAD's already; status ` M
  src/app.txt` (`app\nmain work and clash\n`, diff `-main work` / `+main work and clash`). Of the
  skip: ` M src/app.txt` (`app\nresolved, then skipped\n`).
- **Restore the staged changes** of the mixed reset as 7.10 words it (`IndexTo(<snapshot>, HEAD)` =
  `git restore --staged --source=<snapshot> -- .`): the index becomes the snapshot's whole tree,
  which is `316166a`'s plus the staged src/app.txt, while HEAD is `9dd61fd`: status `D 
  docs/notes.txt`, `MM src/app.txt`, `?? docs/notes.txt` (see 7, item 2).
  This is the design's literal form; decision BD16 stages only the snapshot's own paths, and B9
  expects that: `MM src/app.txt` only.
- **Restore the changes** of the hard reset ("when nothing is staged now, the index from its second
  parent first", then the files): `git restore --staged --source=<snapshot>^2 -- .` stages `A
  src/shortcut.txt`, which the working tree lacks; then README.md is written: status ` M README.md`,
  `AD src/shortcut.txt` (see 7, item 2).
  Under decision BD16 the index is restored only for the paths in which the snapshot's two parents
  differ (none here); no check restores this backup.
- **Push feature/gone back to origin** (`push --porcelain --force-with-lease=refs/heads/feature/gone:
  origin 7dd8e8f…:refs/heads/feature/gone`): exit 0, `*	7dd8e8f…:refs/heads/feature/gone	[new
  branch]`; `origin` has `feature/gone` at `7dd8e8f`; `refs/remotes/origin/feature/gone` appears,
  label `origin/feature/gone` on row 7 (Add the app). **Push v0.8 back**: `[new tag]`, origin has
  `refs/tags/v0.8` → 5c9c1cb; no ref changes locally (the graph stays RL0).
- **Restore tag v0.9** (`update-ref refs/tags/v0.9 fa4c0b7… ""`) and **Restore stash**
  (`stash store -m "On main: Try a README" 39daf6c…`): `stash@{0}: On main: Try a README`; the graph
  then has 10 rows and 3 lanes: the stash is row 2 (lane 1, colour 1, ring, dashed through row 3 into
  row 4's node `316166a`), `topic` moves to row 3 (lane 2, colour 2), `clash`'s rows 5 and 6 are in
  lane 1, colour 3, and the label `v0.9` is on Add the app (row 8).

#### 5. "protect" (`Scenarios.ProtectAsync`, folder and tab `protect`)

##### 5.1 The scenario and its graph

All by Test Author. HEAD on `main`; `other` has no upstream; no remote; tag `v1.1` (annotated
"Version 1.1", object a2f81f7936a088a58e61025fc7fd9adfe842b307) on Describe the app. Status with
ignored files: `!! settings.local.json` only (content `{\n  "theme": "dark"\n}\n`).

**P0, "protect" as built.** 2 lanes, Graph 48, Message x 438, Date 580 to 700. No working row.

| Row | Labels (drawn) | Node: lane, colour | Message | Date | SHA | y centre |
|---|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Main work | 12:06 | `41f8398` | 129 |
| 1 | `other` | 1, 1 | Other files | 12:05 | `19a9044` | 155 |
| 2 | | 1, 1 | Other edits | 12:04 | `4f5e821` | 181 |
| 3 | `v1.1` | 0, 0 | Describe the app | 12:03 | `f39ea2d` | 207 |
| 4 | | 0, 0 (lane 1 ends here) | Stop tracking the local settings | 12:02 | `64166f7` | 233 |
| 5 | | 0, 0 | Add the guide | 12:01 | `73e0bab` | 259 |
| 6 | | 0, 0 | Initial commit | 12:00 | `035a2f5` | 285 |

Lines: lane 0 (colour 0) rows 0 to 6; lane 1 (colour 1) from row 1's node into row 2's node, through
row 3, into row 4's node. Message clicks (468, 129 + 26 r). Left panel: Local branches `2` (`main`,
`other`), Remotes `0`, Pull requests `0`, Tags `1` (`v1.1`), Stashes `0`.

Full SHAs: Main work 41f8398f95fa73a5614e32760cc511673aa6a80d, Other files
19a90447e124bf3a74124a5673df655601c53d5b, Other edits 4f5e8215c9321acae3a2ea386bda05107915f2da,
Describe the app f39ea2d9addec45f926151cd2267cb57ea0b16aa, Stop tracking the local settings
64166f7723b59f263065d3ff2ee199263469846e, Add the guide 73e0babca7d61aaa9088f167ddbf0951f5f13744,
Initial commit 035a2f5b2cca78bd25cc95f5af43b0daf9a28a0c.

| Commit | `merge-base --is-ancestor <it> HEAD` | Changed files | Tree | `diff HEAD <it>` (what a checkout changes) |
|---|---|---|---|---|
| `41f8398` Main work (HEAD) | 0 | M README.md | .gitignore, README.md, docs/guide.txt, docs/old.txt, src/app.txt, src/lib.txt | nothing |
| `19a9044` Other files | 1 | D docs/old.txt, A notes | .gitignore, README.md, docs/guide.txt, notes, src/app.txt, src/lib.txt | M README.md, M docs/guide.txt, D docs/old.txt, A notes, M src/app.txt, M src/lib.txt |
| `4f5e821` Other edits | 1 | M docs/guide.txt, M src/app.txt, M src/lib.txt | as Main work's | M README.md, M docs/guide.txt, M src/app.txt, M src/lib.txt |
| `f39ea2d` Describe the app | 0 | M README.md | as Main work's | M README.md |
| `64166f7` Stop tracking | 0 | A .gitignore, D settings.local.json | as Main work's | M README.md |
| `73e0bab` Add the guide | 0 | A docs/guide.txt, A docs/old.txt | README.md, docs/guide.txt, docs/old.txt, **settings.local.json**, src/app.txt, src/lib.txt | D .gitignore, M README.md, A settings.local.json |
| `035a2f5` Initial commit | 0 | A README.md, A settings.local.json, A src/app.txt, A src/lib.txt | README.md, **settings.local.json**, src/app.txt, src/lib.txt | D .gitignore, M README.md, D docs/guide.txt, D docs/old.txt, A settings.local.json |

##### 5.2 Checking out `other` (`switch --no-guess other`; every refusal exit 1 and changes nothing)

| Change made by the check | Git 2.36's stderr (2.55 identical) | Count |
|---|---|---|
| src/app.txt `app\nlocal\n` (unstaged) | `error: Your local changes to the following files would be overwritten by checkout:` / `\tsrc/app.txt` / `Please commit your changes or stash them before you switch branches.` / `Aborting` | 1 |
| the same staged | the same text | 1 |
| src/app.txt, src/lib.txt `lib\nlocal\n`, docs/guide.txt `guide\nlocal\n` | the same header, then `\tdocs/guide.txt`, `\tsrc/app.txt`, `\tsrc/lib.txt` | 3 |
| README.md or docs/old.txt changed | the same header with that one path | 1 |
| untracked file `notes` (`my own notes\n`) | `error: The following untracked working tree files would be overwritten by checkout:` / `\tnotes` / `Please move or remove them before you switch branches.` / `Aborting` | 1 (no stash button) |
| folder `notes` with `notes/today.txt` (`today\n`) | `error: Updating the following directories would lose untracked files in them:` / `\tnotes` / (an empty line) / `Aborting` | 1 (no button) |
| src/app.txt changed and untracked `notes` | both blocks, the local-changes block first, one `Aborting` | 2 |
| an untracked file `other` lacks (scratch.txt) | none: exit 0, `Switched to branch 'other'`, scratch.txt carried | |

"Create branch" with its box ticked at other's tip (`switch --create topic 19a9044…`) with
src/app.txt changed: the local-changes block naming src/app.txt, exit 1; no `topic` branch is made.

##### 5.3 The automatic stash (P25): "Check out this commit" on Describe the app (row 3)

Changes made first (the pin's): src/lib.txt `lib\nstaged\n` staged, then `lib\nstaged\nand more\n`
in the working tree; src/app.txt `app\nunstaged\n` (unstaged); scratch.txt `scratch\n` (untracked).
Status before: ` M src/app.txt`, `MM src/lib.txt`, `?? scratch.txt`, `!! settings.local.json`.

`git stash push --include-untracked --message "Auto stash before checking out f39ea2d"` (exit 0,
`Saved working directory and index state On main: Auto stash before checking out f39ea2d`), then
`git switch --detach --no-overwrite-ignore f39ea2d…` (exit 0, `HEAD is now at f39ea2d Describe the
app`). Afterwards:

- `stash@{0}` = **256077f** (256077fbd990adad7fe00df31789590d7370aff0), message `On main: Auto stash
  before checking out f39ea2d`, Test Author, 2026-01-02 09:00; parents `41f8398` (main's tip),
  f632741c7c7e1ac98bda443a390dc123e8fda26c (`index on main: 41f8398 Main work`),
  9d22397dd9081ff84ccfc699db779eafbfaedb8c (`untracked files on main: 41f8398 Main work`). Its
  details' changed files: M src/app.txt, M src/lib.txt (scratch.txt is only in ^3).
- HEAD detached at `f39ea2d`; status `!! settings.local.json` only (its content unchanged); HEAD's
  reflog gains `reset: moving to HEAD` then `checkout: moving from main to
  f39ea2d9addec45f926151cd2267cb57ea0b16aa`. `CurrentBranch`: `Detached at f39ea2d`.

**P1, the graph after it** (measured in the app at 1100×700). 2 lanes. No working row.

| Row | Labels (drawn) | Node: lane, colour | Message | Date | SHA | y centre |
|---|---|---|---|---|---|---|
| 0 | `stash@{0}` | 0, 0 (ring) | On main: Auto stash before checking out f39ea2d | 2026-01-02 09:00 | `256077f` | 129 |
| 1 | `main` (no check mark) | 0, 0 | Main work | 12:06 | `41f8398` | 155 |
| 2 | `other` | 1, 1 | Other files | 12:05 | `19a9044` | 181 |
| 3 | | 1, 1 | Other edits | 12:04 | `4f5e821` | 207 |
| 4 | `HEAD` (detached), `v1.1` (both drawn) | 0, 0 | Describe the app | 12:03 | `f39ea2d` | 233 |
| 5 | | 0, 0 (lane 1 ends here) | Stop tracking the local settings | 12:02 | `64166f7` | 259 |
| 6 | | 0, 0 | Add the guide | 12:01 | `73e0bab` | 285 |
| 7 | | 0, 0 | Initial commit | 12:00 | `035a2f5` | 311 |

Lines: lane 0 (colour 0) **dashed** from row 0's ring into row 1's node (y 129 to 155 at x 406), then
solid from row 1 to row 7; lane 1 (colour 1) from row 2's node through row 3's node, row 4, into row
5's node. Left panel: Stashes `1` (`On main: Auto stash before checking out f39ea2d`), no branch
accented.

The way back (`switch main`, `stash pop --index stash@{0}`): exit 0, `Dropped stash@{0}
(256077fbd990adad7fe00df31789590d7370aff0)`; status again ` M src/app.txt`, `MM src/lib.txt`, `??
scratch.txt`, `!! settings.local.json`, the index and files byte-identical (`diff --cached`: src/lib.txt
`+staged`; `diff`: src/app.txt `+unstaged`, src/lib.txt `+and more`).

Variants with the same changes: on `v1.1` (the tag's "Check out v1.1", name `v1.1`): stash
ffe07971f3b3f345f0907e53270d64e776589dae `On main: Auto stash before checking out v1.1`, HEAD at
`f39ea2d`; on Stop tracking (`64166f7`): stash 0a93f5e8f2e336b72cee5b61212ee09452dcc0f7, HEAD at
`64166f7`, settings.local.json untouched. On a clean tree any of these: `No local changes to save`,
no stash, the checkout succeeds.

##### 5.4 When the automatic stash is not taken, or is put back (P26)

- **HEAD's own commit** (`41f8398`, row 0) with src/app.txt changed: no stash; `switch --detach
  41f8398…` exits 0, prints `M	src/app.txt` on stdout and `HEAD is now at 41f8398 Main work`; HEAD
  is detached at `41f8398`, the change kept (` M src/app.txt`).
- **A commit only `other` reaches** (Other edits `4f5e821`, row 2; `is-ancestor` exit 1) with
  src/app.txt changed: no stash; `switch --detach 4f5e821…` refused, exit 1, the local-changes
  block naming src/app.txt: 9.3's `1 conflict prevents checkout. Commit, stash, or discard your
  changes first.` Other files (`19a9044`) with an untracked `notes`: the untracked block naming
  `notes`.
- **Add the guide** (`73e0bab`, row 5; it tracks settings.local.json) with the P25 changes: the
  stash `On main: Auto stash before checking out 73e0bab` (f0c95a478241bba9511b657bc7279278402915f1)
  is made; `switch --detach --no-overwrite-ignore 73e0bab…` is refused, exit 1:
  `error: The following untracked working tree files would be overwritten by checkout:` /
  `\tsettings.local.json` / `Please move or remove them before you switch branches.` / `Aborting`;
  `stash pop --index stash@{0}` exits 0 (`Dropped stash@{0} (f0c95a4…)`): the status, index and files
  are as before (`porcelain=v2 --ignored` identical), the stash list empty, settings.local.json
  still `{\n  "theme": "dark"\n}\n`, HEAD still `main`; HEAD's reflog gains two `reset: moving to
  HEAD` entries. 9.3's text: `1 ignored file is in the way: 73e0bab tracks a file of the same name.
  Move it away first.`
- **Add the guide on a clean tree**: `No local changes to save` (no stash), then the same refusal,
  exit 1. **Initial commit** (`035a2f5`, row 6) behaves the same in both cases (its stash
  091c16fafb18976c4727441775ce421e474899ce, popped back).

##### 5.5 Other refusals (P27; each changes nothing: refs, index, files, status, no `MERGE_HEAD`)

| Operation (the app's command) | Change | Git 2.36 | Exit |
|---|---|---|---|
| merge `other` (`merge --no-edit --no-log …`) | src/app.txt unstaged | `error: Your local changes to the following files would be overwritten by merge:` / `\tsrc/app.txt` / `Please commit your changes or stash them before you merge.` / `Aborting` | 128 |
| merge `other` | src/app.txt, src/lib.txt, docs/guide.txt unstaged | the same, 3 paths | 128 |
| merge `other` | README.md `# Protect\n\nStaged.\n` staged | `Your local changes to the following files would be overwritten by merge:` / `  README.md` (two spaces, no `error:`, no final line feed) | 128 |
| merge `other` | src/app.txt staged | the same one-line form with `  src/app.txt` | 128 |
| merge `other` | untracked `notes` | `error: The following untracked working tree files would be overwritten by merge:` / `\tnotes` / `Please move or remove them before you merge.` / `Aborting` | 128 |
| merge `other` | untracked scratch.txt only | succeeds (`Merge made by the 'ort' strategy.`) | 0 |
| cherry-pick Other edits (`other~1`) | src/app.txt unstaged | the `by merge:` block as above, then `fatal: cherry-pick failed` | 128 |
| cherry-pick Other edits | README.md or src/app.txt staged | `error: your local changes would be overwritten by cherry-pick.` / `hint: commit your changes or stash them to proceed.` / `fatal: cherry-pick failed` | 128 |
| revert Describe the app (`revert --no-edit main~1`) | README.md unstaged | the `by merge:` block naming README.md, then `fatal: revert failed` | 128 |
| revert Describe the app | src/app.txt staged | `error: your local changes would be overwritten by revert.` / `hint: …` / `fatal: revert failed` | 128 |

##### 5.6 Git 2.55.0 against 2.36.0 (texts replayed on a replica of "protect")

Identical: every checkout text (5.2, 5.4), the cherry-pick and revert texts and exits, `switch
--detach` to HEAD's own commit. Different, for merge only: **exit 2** (2.36: 128), every refusal
ends with an extra line `Merge with strategy ort failed.`, and the staged form reads `error: Your
local changes to the following files would be overwritten by merge:` / `  README.md` / `Merge with
strategy ort failed.` (2.36: no `error:` and no final line feed); the staged refusal also writes a
no-op entry `merge refs/heads/other: updating HEAD` into HEAD's reflog (from 2.38 on: 2.38.0,
2.39.0, 2.43, 2.55 and 2.56; 2.32.0, 2.34.1, 2.36.0 and 2.37.3 none; found after the report's first
commit, design 15.9). Also both versions: `git
check-ignore` exits 128 with `fatal: settings.local.json: pathspec magic not supported by this
command: 'literal'` while `GIT_LITERAL_PATHSPECS=1` is set, and answers `.gitignore:1:settings.local.json
settings.local.json` without it.

#### 6. Phase 1's "graph" scenario's stash (F15)

Part F's GS0. As phase 1's report gives it, now with the dashes (measured in the app at 1100×700; 3
lanes, Message
x 454): row 0 `stash@{0}` "On main: Work in progress on README" `95daa96`
(95daa96b4c242f0f653cadd7d0218a76679f0cf5, parents 47e6ec7 583f825), lane 0, colour 0, ring, y 129;
row 1 `feature/search` Highlight matches `f463b85` (1, 1); row 2 `main` ✓ Add settings page `47e6ec7`
(0, 0). Lane 0 (colour 0) is **dashed** from row 0's ring through row 1 into row 2's node (x 406,
y 129 to 181), solid from row 2 on. Every other line of rows 0 to 12 is solid and as phase 1's
table (lanes and colours unchanged).

#### 7. Surprises: git behaviour or facts the design does not foresee

1. **Blame's uncommitted lines carry the wall clock.** For lines not committed yet, git's porcelain
   gives author `Not Committed Yet`, `<not.committed.yet>` and `author-time` = the current time in
   the machine's zone (2.36; `GIT_COMMITTER_DATE` and the file's mtime make no difference). 4.2's
   gutter shows `Not committed yet` there (no date), but its run tooltip lists "the date": for an
   uncommitted run the tooltip must not show git's time, or a picture of it changes every run.
   Decision BD11 settles it: such a run's tooltip shows `Not committed yet` alone.
2. **The backups list's index restores do not fit "reflog" as built.** 7.10's `Restore the staged
   changes…` runs `IndexTo(<snapshot>, HEAD)` = `git restore --staged --source=<snapshot> -- .`,
   which makes the whole index the snapshot's tree. The mixed reset's snapshot was taken on
   `316166a`; HEAD has since moved to `9dd61fd` (Write the notes adds docs/notes.txt), so the
   restore stages `D docs/notes.txt` besides `M src/app.txt` (status `D  docs/notes.txt`, `MM
   src/app.txt`, `?? docs/notes.txt`). Likewise the hard reset's `Restore the changes…` puts the
   index from `^2` (the tree of `d07c677`) onto `9dd61fd` and stages `A src/shortcut.txt`, a file the
   working tree lacks (`AD src/shortcut.txt`, ` M README.md`). Inside an undo (after the move back)
   these effects are exact; restored outside the session onto a moved HEAD they undo later commits
   in the index. This report settles it with decision BD16 (the backups list's index restores
   stage only the snapshot's own paths), which B9 expects; the lead folds it into 7.10.
3. **`git check-ignore` refuses under `GIT_LITERAL_PATHSPECS=1`** (exit 128, `pathspec magic not
   supported by this command: 'literal'`, 2.36 and 2.55): 9.3's ignored-files classification
   (`check-ignore --stdin -z`) must remove the variable for that call, as `AllIgnoredAsync` and the
   pins do. The design does not say so for 9.3; P26 asserts the call without the variable (D74).
4. **Merge refusals differ between 2.36 and 2.55** (5.6): exit 128 vs 2, the extra `Merge with
   strategy ort failed.`, and the staged form's `error:` prefix. The classifier and the checks'
   word-for-word lines must allow both; checkout, pick and revert texts are the same (part P's TP7,
   TP9 and decision PD10).
5. **The reflog reader returns `%gD` in full for a branch** (`refs/heads/main@{0}`); 5.2's page shows
   `{b}@{n}` (`main@{0}`): the view model shortens it (HEAD's are `HEAD@{n}` already).
6. **A detached checkout of HEAD's own commit is not a no-op**: it detaches HEAD (status bar
   `Detached at 41f8398`, HEAD's reflog `checkout: moving from main to 41f8398…`) and prints the
   carried change (`M	src/app.txt`) on stdout. 9.1 only says no stash is taken there
   (P26 step 3, decision PD7).
7. **Merges match the Files and Changes searches** by their first-parent change: Files `readme`
   includes the merge `f513566`, Changes `Lines` includes it too (its first-parent change adds `The
   reader splits a text into lines.`), as D174's rule implies; and `README.md`'s blame names the side
   branch's `c1b6375`, while its file history omits the merge.
8. **A hidden branch's stash can land mid-graph** when only the local branch is hidden (its upstream
   keeps the base on screen): the filtered pager places the alpha stash at row 4, right below Alpha
   two, with a dashed lane 2 down to row 11 (3.3, informative; F14 hides the upstream too, so the
   stash is simply left out there).
9. **Solo drops labels of shown rows**: under `Solo feature/beta`, `main`'s row loses its
   `origin/main` label and Merged work (still a row) loses `feature/merged` (8.2's rule, but easy to
   miss in an expected result).
10. Reflog's Write the notes (`9dd61fd`) has author date 12:21 and commit date 12:22 (the amend);
    Topic work (`362fbd6`) author 12:02, commit 12:09; main's Clash notes (`316166a`) author 12:05,
    commit 12:08. The Date column shows the author dates; the row order follows the commit dates.

### Part A: the shell and the activity log

Part A covers design section 1 (the toolbar's Undo, Redo and Search, the status bar's Activity log,
the keys of 1.3) and section 2 (the activity log: where it is, what it shows, what its records carry,
the record and backup clocks of 2.3): checks A1 to A4 of design 15.4. The search bar itself is part
F's (A4 only opens and closes it by its key), undoing every recorded action is part U's (A1 and A4
use one commit), the pages are part B's (A2 opens the backups list only to show that Esc in the log
leaves a page alone), and C12's refusal dialog is part P's (A2 shows how a refused checkout looks in
the log). Every git state below was derived with Git 2.36.0.windows.1 on copies of the scenario
repositories built by the scenario code itself, each operation run with the command lines the
merged git layer runs and the app's environment, and replayed with Git 2.55.0.windows.5 (see "How
the expected git states were derived", part A).

#### Conventions of part A

- **Defaults.** Every check runs at 1100×700 in the dark theme. An app start writes a session with
  one tab on a fresh copy of the named scenario, starts the app and waits for the repository
  (`Phase5Checks.OpenAsync`: graph drawn, status shown, queue idle), the pointer at the neutral point
  (760, 687), which at 1100×700 lies in the status bar left of the Activity log toggle (x 1005 to
  1100). A3's second app start is the one exception: it writes no session and starts on the one the
  first start left. Each app start is one test method (table below), except A3's two. No step waits
  a fixed time.
- **The clocks (design 2.3).** Both harnesses freeze the record clock at 2026-01-02 09:00:00 UTC, so
  every time the activity log shows reads `09:00:00` and every duration `0 ms`; the backup clock is
  fixed there too: it judges a backup's age against 2026-01-02 09:00:00 and names the first backup
  the app makes `…-20260102T090001000Z`, each later one a second after the last.
  `ShellDriver.Start` passes them (`recordClock`, `backupClock`); `RealApp.Launch` sets
  `VISUALCOMMIT_RECORD_CLOCK=frozen` and `VISUALCOMMIT_BACKUP_CLOCK=fixed`.
- **The clean-up at opening.** When a scenario holds backups due for the clean-up, the check waits,
  after the repository has opened and before its first input, until `IOperationLog` has the record
  `Clean up backups` (design 15.2; new `ShellDriver.WaitForOperationRecordAsync(title)`, in the real
  window the queue's log line `<repo>: Clean up backups: Succeeded after <N> ms: Removed 3 backups`).
  The clean-up says nothing in the status bar (D139): it stays `Ready`.
- **Toolbar and status bar states** are asserted per button: enabled or not (`IsEffectivelyEnabled`),
  its tooltip (`ToolTip.GetTip`, "no tooltip" meaning null) and, for Undo, Redo, Search and the
  Activity log toggle, its `AutomationProperties.HelpText`, which equals the tooltip (design 1.1,
  13.4; empty where there is no tooltip). In part A the real-window pass reads the same through UI
  Automation (`IsEnabled`, `HelpText`) and captures no tooltip (part U's U16 captures Undo's). A
  picture of a tooltip rests the pointer on
  the button as phase 3's conventions do (`RestPointerOnAsync`: one move, the tooltip open by
  `ToolTip.GetIsOpen`, settled once); a disabled toolbar button shows its tooltip too
  (`ToolTip.ShowOnDisabled`, phase 3's 4d). The step after a tooltip's capture first moves the
  pointer to the neutral point.
- **The Activity log toggle** (`ActivityLogToggle`, x 1005 to 1100, y 675 to 700 at 1100×700) is
  `VcFlatButton`: enabled, its icon and label `Activity log` in `VcTextSecondaryBrush` (`#9BA3B0`) on
  no background; disabled, in `VcTextDisabledBrush` (`#5F6775`); while the panel is open it has the
  class `selected`: the background `VcControlBackgroundBrush` (`#272C36`, sampled at (1008, 678),
  inside the button's left padding) and the label in `VcTextPrimaryBrush` (`#E4E7EC`) (design 1.2).
- **The panel's geometry at 1100×700** (design 2.1, derived from the bounds the app has, D122's
  rule: the checks read every position from the controls' bounds and assert these values ±2): the
  graph area is x 260 to 700, y 88 to 674 (586 high). With the panel open at height h it lies at
  y 674 − h to 674, x 260 to 700; the splitter (`ActivityLogSplitter`) is the 5 px above it, with
  its 1-px `VcBorderBrush` (`#323845`) line; the graph area's content (banner, column headers, rows)
  keeps y 88 to 669 − h. The header (`ActivityLogHeader`) is the panel's first 30 px; the list
  (`ActivityLogList`) the rest. The left panel (x 0 to 260) and the right panel (x 700 to 1100) keep
  their full height (y 88 to 674). With the panel closed nothing of it is in the layout: every
  position of phases 0 to 4 holds.
- **Reading the log.** The scripted run reads the panel as a snapshot (new
  `ShellDriver.ActivityLog()`: the header's texts, `Show reads`' state, and the list's rows top to
  bottom, each with its kind (operation, failure summary, note, call, output box), its indent, its
  automation name and its parts as drawn: chevron, outcome glyph and its brush, time, title,
  message and its brush, duration, exit text and its brush, command, the output box's titled parts,
  whether it is open). A row is clicked at its centre (new `ShellDriver.ActivityLogRow(name,
  occurrence)` from its bounds: the row with that automation name, `occurrence` counting from 0
  among the rows shown, top to bottom; the step fails if the row is not in the list's view). The
  real window finds `ActivityLogOperation` by its name (the title) and `ActivityLogCall` by its
  name (the command), and reads the box `ActivityLogOutput`'s Value pattern. Rows are written below
  as `[glyph] time · title · message · duration` for an
  operation and `exit · command · duration` for a call; "(open)" marks an open row.
- **An operation's calls.** An opened operation lists, under its failure summary and its notes,
  exactly the records of `IGitCallLog` whose `OperationId` is the operation's id, in the order of
  their `Sequence` (the order they started, design 2.3), each once. The check compares the rows with
  the call log, so the expected results name only the calls that matter to the step ("among them").
  "Exactly" is said only of operations that 7.2 does not record (the backups' clean-up and the merge
  tool), whose calls 7.7's recording reads never join: there the list is every call they make. For a
  recorded operation (the checkout, the merge) the calls are always "among them": 7.7 adds its reads
  (`rev-parse HEAD --symbolic-full-name HEAD`, `for-each-ref`) before and after `run`, also when git
  refuses, so a list made on `e33f846` is given as information only. A call made with
  `GIT_INDEX_FILE` set reads `(on a temporary index)` after its command.
- **The order of rows** is the order the operations and calls started (their sequence numbers), not
  their times, which are all `09:00:00`: oldest at the top, newest last (design 2.2). The list starts
  scrolled to its end and stays there while rows come; opening a row while the list is at its end
  keeps it at its end, so that what the row opens shows (decision AD15). No step scrolls it with
  the pointer or the wheel; A2's steps rely on this to find the call rows they click in view.
  An opened call's box can scroll the row it is under out of view: a step closes the call
  before it clicks that row, and step 7 reaches the merge's row with 2.2's Left, which brings
  it into view and so scrolls the list ("Changed in this report after its first commit").
- **Text that may be cut.** An operation's title and message, a failure's summary, each note and a
  call's command are `TrimmedTextBlock`s: drawn whole or cut with "…", the whole text in the tooltip
  (design 2.2 for the titles and messages, and for cutting the commands; decision AD3
  for the summary, the notes and the command's tooltip). At 440 px many are cut: the summary
  `1 conflict prevents checkout. Commit, stash, or
  discard your changes first.`, the note `Removed backup branch-20251120T090002000Z (older than 30
  days)` and most commands in the code font. Which ones are cut is not asserted; the snapshot
  asserts the whole texts, and `AssertNoClippedText` (with `allowShortenedWithToolTip`) leaves these
  trimmed blocks out, as it does for phase 3's. Every other text of the panel (the header, the
  times, the exit texts, the durations) is drawn whole.
- **Waiting for an operation** is `WaitForIdleAsync` (its refresh included), and in the real window
  the queue's log line `<repo>: <Title>: <outcome> after <N> ms: <message>`. "No dialog" is phase 3's
  absence rule.
- **Keys.** "The platform's undo" and "the platform's redo" are `HotkeyConfiguration.Undo` and every
  gesture of `HotkeyConfiguration.Redo` (design 1.3): Ctrl+Z, and Ctrl+Y then Ctrl+Shift+Z, on
  Windows and in the headless platform (measured on the development machine, Avalonia 12.1.3); the
  command modifier with F is Ctrl+F. Where the platform's command modifier is Cmd (CI's macOS job,
  `ShellDriver.CommandModifier`), the gestures and the tooltip's `{gesture}` follow it. New driver
  helpers send them: `PressSearch()`, `PressGitUndo()`, `PressGitRedo(int gesture = 0)` (one of
  `HotkeyConfiguration.Redo`'s gestures). Phase 4's `Undo()` and `Redo()` stay the text-undo
  gestures; they send the same keys, and the check uses them where the key is meant for a text box or
  the resolver, so that the code says which handler should take it.
- **Typing.** `TypeInto` delivers its text as one text input in the scripted run; the real window
  types one key per character. Avalonia's text box undoes one text input per press of the platform's
  undo (measured: `Next` typed as four inputs needs four presses, typed as one needs one).
- **Git's own texts** quoted below (the call log's output and error, the error dialog's
  `ErrorOutput`) were the same on Git 2.36.0 and 2.55.0 for every call of this part; as phase 3's
  rule says, a check asserts them word for word only when `git --version` is 2.36, and on any other
  git asserts the classifying line and that what the log shows is the call's own record. Refs,
  objects, files and `git status` are asserted on every version.
- **Commits the app makes** have the author and committer "Test Author" <author@example.com> and
  both dates 2026-01-02 09:00 UTC (D72). Snapshot commits (`…-changes`) are asserted by parent and
  tree, never by id (phase 4's rule).
- **Pictures** are named `a<check><step>-<what>` (`Phase5Checks.PictureName`), saved under
  `artifacts/visual/phase-5/scripted/`, and in the real window under `…/real-window/` with the same
  name. A picture whose content varies by nature says so in its check's Notes.

#### Test methods and app starts (part A)

| Check | Test method (one app start each, unless said) | Scenario as the check starts it | Window, theme |
|---|---|---|---|
| A1 | `Check_A1_toolbar_and_status_bar` | `Scenarios.HistoryAsync()` | 1100×700, dark |
| A2 | `Check_A2_activity_log` (steps 1 to 6) | `Scenarios.ConflictsAsync()`, then `Scenarios.AddOldAndManyBackupsAsync(repo)`, then other.txt written (`TempRepo.WriteFile`, `other file\nchanged on main\nchanged here\n`), all before the app starts; the session also holds `ActivityLogHeight` 320 (`ShellDriver.WriteSession`, then `ShellDriver.Start`; `RealApp.Launch(data, session)`) | 1100×700, dark |
| A2 | `Check_A2_activity_log_stop_cancel_abort` (steps 7 to 11) | as `Check_A2_activity_log`'s, on a fresh copy, with the waiting merge tool (`Scenarios.ConfigureWaitingMergeToolAsync(repo)`, new) configured before other.txt is written | 1100×700, dark |
| A3 | `Check_A3_panel_height` (a plain `[Fact]`: two app starts on one data folder, each in `FreshApplication.RunAsync`, as phase 0's check 6: the first as every check's, `Phase5Checks.OpenAsync`, which writes the session; the second with `ShellDriver.Start(data.Path)` and `WaitForRepositoryAsync`, writing no session, so that the height the first saved is what the app reads; in the real window `RealApp.Launch(data, session)`, then `RealApp.Launch(data)`) | `Scenarios.HistoryAsync()` | 1100×700, dark |
| A4 | `Check_A4_keys` | `Scenarios.HistoryAsync()`, then `scratch.txt` (`scratch\n`) written in the working tree before the app starts | 1100×700, dark |
| A4 | `Check_A4_keys_in_the_resolver` | `Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)` | 1100×700, dark |

**The waiting merge tool** (`ConfigureWaitingMergeToolAsync`, new in `Scenarios.Phase5.cs`, the
form of `MergeToolTests.A_cancelled_tool_is_stopped…`): the copy's own configuration
`merge.tool=slow`, `mergetool.slow.trustExitCode=true` and `mergetool.slow.cmd` =
`touch ../tool-started; i=0; while [ ! -f ../tool-release ] && [ $i -lt 600 ]; do sleep 0.1; i=$((i+1)); done; exit 1`.
Git runs the command in the working tree's top folder, so the two files lie beside it, in the
`TempRepo`'s folder (`Scenarios.WaitingMergeToolStarted(repo)`, `ReleaseWaitingMergeTool(repo)`).
The tool writes nothing to the file; it waits until it is stopped or released, or until it has run
600 rounds of `sleep 0.1`, and then fails (exit 1). The 600 rounds sleep 60 s, but on Windows each
`sleep` is a process of its own: the loop alone (`sh -c` with the same loop, no tool-release file)
took 111 s in Git 2.36.0's `sh` on the development machine, measured once with another test run
beside it. A cancel that does not reach the tool therefore shows as the bounded wait of step 9
(30 s) failing, never as a pass and never as a wait for the tool's own end. The check releases the
tool after the cancel and in a `finally`, so that where only git is
killed (macOS, Linux) the tool's shell ends at once.

#### Scenarios and graphs of part A

**"history"** (`Scenarios.HistoryAsync`, folder and tab `history`), as `Phase5ScenarioTests` pins it
and scenario facts 2.1 give it: HEAD on `main` at `c0bc2dd`, which
tracks `origin/main` (ahead 0, behind 0); `feature/compare` tracks `origin/feature/compare`; the
bare `origin` beside the working tree; no stash, no tag; `git status --porcelain` prints
` M src/Reader.cs` only (blob `7061deb` in HEAD, `3056624` in the working tree). Commits by three
authors from 2026-01-01 12:00, one minute apart.

**H0, "history" as built** (A1 at the start; A4 at the start, with its `scratch.txt`). Two lanes:
Graph column 48, Message column x 438 to 580, Date x 580 to 700, no Author or SHA column at
1100×700. The working-changes row "Working changes" is display row 0 (ring in lane 0, colour 0, its
dashed line into row 0's node); commit row r is display row r + 1, centre y 155 + 26 r; a row is
clicked "in its Message column" at x 468.

| Row | Labels (drawn at 1100×700) | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓, `origin/main` (drawn `main` ✓ and `+1`) | 0, 0 | Read whole lines | 2026-01-01 12:13 | `c0bc2dd` |
| 1 | | 0, 0 | Stop tracing | 2026-01-01 12:12 | `20d7fbe` |
| 2 | | 0, 0 | Write new notes | 2026-01-01 12:11 | `e765529` |
| 3 | | 0, 0 | Merge branch 'feature/readme' | 2026-01-01 12:10 | `f513566` |
| 4 | | 1, 1 | Explain the reader | 2026-01-01 12:09 | `c1b6375` |
| 5 | | 0, 0 (lane 1 ends here) | Remove the notes | 2026-01-01 12:08 | `884b462` |
| 6 | `feature/compare`, `origin/feature/compare` (drawn `feature/comp…` and `+1`) | 1, 2 | Compare whole lines | 2026-01-01 12:07 | `245c395` |
| 7 | | 0, 0 | Rename the parser to reader | 2026-01-01 12:06 | `0575c7f` |
| 8 | | 1, 2 | Add the comparer | 2026-01-01 12:05 | `d0d3c77` |
| 9 | | 0, 0 (lane 1 ends here) | Add tracing | 2026-01-01 12:04 | `14a5c6c` |
| 10 | | 0, 0 | Handle empty input | 2026-01-01 12:03 | `365482b` |
| 11 | | 0, 0 | Add the logo | 2026-01-01 12:02 | `775113f` |
| 12 | | 0, 0 | Add the parser | 2026-01-01 12:01 | `1d5a7cf` |
| 13 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `7829335` |

Lines: lane 0 (colour 0) from row 0 to row 13; lane 1 (colour 1) from row 3's node into row 4's node,
then into row 5's node; lane 1 (colour 2) from row 6's node through row 7 into row 8's node, then
into row 9's node. Left panel: Local branches `2` (folder `feature`, closed; `main` in the accent
colour), Remotes `2` (`origin` with the closed folder `feature` and `main`), Pull requests `0`, Tags
`0`, Stashes `0`. The status bar's branch `main`.

**H-A1, "history" after the commit "Describe the lines"** (A1 from step 4; A4 after its commit and
after each redo). The app's commit, which plain git makes alike, is `d1e23a3`
(`d1e23a37b6b08f4dc9662fab930f232a66ecd8fc`): message `Describe the lines` and a line feed, parent
`c0bc2dd`, the tree of HEAD with src/Reader.cs as the working tree held it (blob `3056624`), author
and committer Test Author <author@example.com>, both dates 2026-01-02 09:00 UTC. 15 rows; the same
two lanes and columns. In A1 the tree is clean afterwards, so there is no working-changes row and
row r's centre is y 129 + 26 r; in A4 `scratch.txt` keeps the working-changes row "Working changes"
as display row 0 (ring in lane 0, colour 0, dashed into row 0's node), and row r is display row
r + 1, centre y 155 + 26 r.

| Row | Labels (drawn at 1100×700) | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ (drawn whole) | 0, 0 | Describe the lines | 2026-01-02 09:00 | `d1e23a3` |
| 1 | `origin/main` (drawn whole) | 0, 0 | Read whole lines | 2026-01-01 12:13 | `c0bc2dd` |
| 2 to 14 | H0's rows 1 to 13, with their labels, lanes and colours | | | | |

Lines: lane 0 (colour 0) from row 0 to row 14; lane 1 (colour 1) from row 4's node into row 5's node,
then into row 6's node; lane 1 (colour 2) from row 7's node through row 8 into row 9's node, then
into row 10's node. Left panel: as H0, with `main` ↑1. `git status -sb`: `## main...origin/main [ahead 1]`
(and `?? scratch.txt` in A4). HEAD's reflog newest entry: `commit: Describe the lines`.

**H-A0u, "history" after the commit's undo** (A4 after each Ctrl+Z): H0's rows, labels, lanes and
lines with the working-changes row "Working changes" as display row 0; `d1e23a3` is no longer a row
(no ref reaches it; it still exists). `git status --porcelain=v1 -b`: `## main...origin/main`,
`M  src/Reader.cs`, `?? scratch.txt`. HEAD's and `main`'s reflogs' newest entry:
`VisualCommit: undo commit "Describe the lines"` at `c0bc2dd`; after a redo,
`VisualCommit: redo commit "Describe the lines"` at `d1e23a3`.

**"conflicts" for A2** (`ConflictsAsync`, as phase 4's report gives it under "The scenario's graphs
(checks C10 to C13)", GC0, with A2's additions). HEAD on `main` at `da31c97` ("Ours edits"); no
remote, no tag, no stash. Five lanes: Graph column 96, Message column x 486 to 700, no Date column at
1100×700. With other.txt changed (` M other.txt`), the working-changes row "Working changes" is
display row 0 (ring in lane 0, colour 0, dashed into Ours edits' node) and GC0's rows 0 to 9 are
display rows 1 to 10:

| Display row | Labels | Node: lane, colour | Message | SHA |
|---|---|---|---|---|
| 0 | | ring 0, 0 | Working changes | |
| 1 | `main` ✓ | 0, 0 | Ours edits | `da31c97` |
| 2 | `feature/kinds` | 1, 1 | Kinds edits | `3a9de3e` |
| 3 | `feature/picks` | 2, 2 | Pick last | `5c4c257` |
| 4 | | 2, 2 | Pick conflicting | `01fd976` |
| 5 | | 2, 2 | Pick clean | `01d2046` |
| 6 | `feature/rebase` | 3, 3 | Second change | `305f7f7` |
| 7 | | 3, 3 | First change | `70e8c78` |
| 8 | `feature/theirs` | 4, 4 | Theirs edits | `5c77ffb` |
| 9 | | 0, 0 (lanes 1, 2, 3 and 4 end here) | Base | `195768b` |
| 10 | | 0, 0 | Initial commit | `6bd579f` |

Lines: lane 0 (colour 0) from the ring to display row 10; lanes 1 to 4 from their tips into display
row 9's node, as GC0's. With the log open at 320 (A2), the graph area's content is y 88 to 349:
display rows 0 to 7 whole (y 116 to 324) and row 8 cut, no banner; while the merge is stopped the
banner takes the top of it and rows are found from the control's bounds (D122). Left panel: Local
branches `5` (the closed folder `feature` with `kinds`, `picks`, `rebase`, `theirs`; `main` in the
accent colour), Remotes `0`, Pull requests `0`, Tags `0`, Stashes `0`.

**A2's merge stop**: the app's
`git merge --no-edit --no-log -m "Merge branch 'feature/theirs'" refs/heads/feature/theirs`
exits 1 and leaves `UU notes.txt` and ` M other.txt` (the merge touches notes.txt only, so git
carries the change to other.txt): stages 1 `624b469`, 2 `439eb6b`, 3 `6216606`; `MERGE_HEAD`
`5c77ffb0ec78abf986d121f42013eee14422e5a5`; `MERGE_MSG` `Merge branch 'feature/theirs'`, an empty
line, `# Conflicts:`, `#⇥notes.txt`; notes.txt is phase 4's FM with `>>>>>>> refs/heads/feature/theirs`
in its lines 6 and 18 (the app's merge names the full ref, phase 3's part B). The graph is the
table above with display row 0 reading "Working changes · 1 conflict"; the banner
`Merging: "Merge branch 'feature/theirs'". 1 file has conflicts.` with `Continue` (disabled:
`Resolve the conflicts first (1 file)`) and `Abort merge` on a row of their own under the text;
the stage panel `Unstaged files (2, 1 with conflicts)` with U `notes.txt` (U in `VcDangerBrush`) and
M `other.txt`, `Staged files (0)`, the summary `Merge branch 'feature/theirs'`.

**A2's backups.** `AddOldAndManyBackupsAsync` leaves 53 backups (53 refs), every one of HEAD's commit
`da31c97`: `reset-20251120T090001000Z` and `branch-20251120T090002000Z/old-idea` (dated 2025-11-20,
older than 30 days by the fixed clock) and the 51 fillers `reset-20251220T090001000Z` to
`reset-20251220T090051000Z`. The clean-up at opening removes, in this order,
`reset-20251120T090001000Z` (older than 30 days), `branch-20251120T090002000Z` (older than 30 days)
and `reset-20251220T090001000Z` (more than 50 backups), in one
`git update-ref --stdin` transaction of three `delete <ref> da31c977c80e9becdc908c74d973669aad1b3b8d`
lines (exit 0, no output); 50 remain, `reset-20251220T090002000Z` to `reset-20251220T090051000Z`.
The abort of A2's step 10 (method 2) saves `abort-20260102T090001000Z` (→ `da31c97`) and
`abort-20260102T090001000Z-changes` (a snapshot commit by VisualCommit <snapshot@visualcommit.invalid>
with the parent `da31c97` and the tree `912dd413f445e484334bad13e5f44a294ebc2dfc`: Ours edits' files
with notes.txt as the merge left it and other.txt as changed), which makes 51 backups, so its
clean-up removes `reset-20251220T090002000Z` (more than 50 backups): 50 backups (51 refs) remain.
After `git merge --abort`: `## main`, ` M other.txt` (unchanged: `other file`, `changed on main`,
`changed here`), no `MERGE_HEAD`, HEAD's reflog's newest entry `reset: moving to HEAD`, `main` still
`da31c97`.

**"conflicts" stopped in a merge for A4's resolver method** (`ConflictsStoppedAsync(ConflictsStop.Merge)`):
phase 4's GC-M (display row 0 "Working changes · 1 conflict"), FM (notes.txt, blob `cdd7b39`, with
`>>>>>>> feature/theirs`: the merge was started in a terminal), PM (the panes) and R1 (16 lines,
after `Use ours` on block 1), exactly as phase 4's report gives them under "The conflicted files,
the panes and the results (checks C10 to C13)"; no `merge.tool`; no backup ref.

#### Texts these checks rely on (part A)

| Where (design section) | Text |
|---|---|
| Undo, enabled (1.1, 7.6) | tooltip and help text `Undo {text}`; here `Undo commit "Describe the lines"` |
| Redo, enabled (1.1, 7.6) | `Redo {text}`; here `Redo commit "Describe the lines"` |
| Undo and Redo, disabled (1.1, 7.4) | the first reason that holds, in this order: `Wait for {title} to finish` while a user operation of the repository runs or waits, a background one (the clean-up) giving none (here `Wait for Merge tool for notes.txt to finish`); `Finish or abort the {kind} first` (here `Finish or abort the merge first`); `Resolve the conflicts first`; `Close the interactive rebase first`; `Nothing to undo` / `Nothing to redo`; the entry's own check (7.6) |
| Search (1.1, 11.2) | enabled: `Search commits ({gesture})`, here `Search commits (Ctrl+F)`; disabled: `Close the interactive rebase first`, `Close the conflict resolver first` |
| An empty tab's toolbar (1.1, phase 3's 4d) | every button but Theme disabled, none with a tooltip or help text; Theme `Switch to the light theme` |
| Activity log toggle (1.2) | label `Activity log`; tooltip and help text `Show every git command the app has run` while the panel is closed, `Hide the activity log` while it is open; enabled only on a tab with a repository |
| Keys (1.3) | Ctrl+Z (the platform's `HotkeyConfiguration.Undo`), Ctrl+Y and Ctrl+Shift+Z (its `Redo`), Ctrl+F (the command modifier with F) |
| The panel's header (2.2) | `Activity log`, then the repository's name in `VcTextSecondaryBrush` (`history`, `conflicts`), then at the right the check box `Show reads` (unchecked) and the close button (`IconClose`) |
| An operation's row (2.2) | chevron `IconChevronRight` (closed) or `IconChevronDown` (open); the outcome glyph, 10 px: `IconCheck` in `VcSuccessBrush` `#3FB97F` (succeeded), `IconAlert` in `VcWarningBrush` `#E0A23B` (a warning or a stop with conflicts), `IconClose` in `VcDangerBrush` `#E5606B` (failed), `IconMinus` in `VcTextSecondaryBrush` `#9BA3B0` (cancelled), none while running; the time `HH:mm:ss` (`09:00:00`); the title; the message, the status bar's text in its tone (`Running…` while it runs; empty for the stage panel's writes); the duration `{N} ms` below a second, `{N.N} s` above, secondary, right-aligned (`0 ms`) |
| An opened operation (2.2) | indented 24, in this order: the failure's summary in `VcDangerBrush`; its notes, secondary (the summary and each note one line, cut with "…", the whole text in its tooltip: decision AD3); its calls; `Its git commands are no longer kept.` when they are gone |
| A call's row (2.2) | chevron; the exit: `exit 0` secondary, another code (`exit 1`) in `VcDangerBrush`, `cancelled`, `failed to start`; the command in the code font (`… (on a temporary index)` for a call on a temporary index), cut with "…", the whole command in its tooltip (decision AD3); the duration |
| An opened call (2.2) | a read-only box in the code font (`ActivityLogOutput`), at most 12 lines before it scrolls, with the parts `Input` (only `update-ref --stdin`'s), `Output` and `Error`, each titled and shown when not empty; `No output` when all are empty |
| The clean-up's notes (2.3) | `Removed backup {name} ({reason})`: `Removed backup reset-20251120T090001000Z (older than 30 days)`, `Removed backup branch-20251120T090002000Z (older than 30 days)`, `Removed backup reset-20251220T090001000Z (more than 50 backups)`, `Removed backup reset-20251220T090002000Z (more than 50 backups)` |
| The clean-up's operation (2.3, phase 4's 9) | title `Clean up backups`; message `Removed 1 backup` / `Removed {N} backups` (here `Removed 3 backups`), never in the status bar |
| Undo and Redo of a commit (7.3, 7.5, 7.6, 12.1, 12.2) | titles `Undo commit "Describe the lines"`, `Redo commit "Describe the lines"`; running texts `Undoing commit "Describe the lines"…`, `Redoing commit "Describe the lines"…`; status `Undid commit "Describe the lines"`, `Redid commit "Describe the lines"` (secondary); the reflog message `VisualCommit: undo commit "Describe the lines"` / `VisualCommit: redo commit "Describe the lines"`; the undone commit's message put in the stage panel's summary and description when both are empty, taken out on the redo when they still hold it (7.5) |
| The search bar (3.1), A4 only | `SearchBar` 36 high; the mode button `Commits`; the box `SearchBox` with the placeholder `Message, author or SHA`, focused; `SearchCount` empty; `SearchPrevious` (tooltip `Previous match (Shift+Enter)`) and `SearchNext` (`Next match (Enter)`) disabled; `SearchClose` (`Close the search (Esc)`) |
| The refused checkout (9.3, phase 3's 2.1) | operation `Check out feature/kinds`, status `Check out feature/kinds failed` (danger); dialog title `Checkout blocked`, summary `1 conflict prevents checkout. Commit, stash, or discard your changes first.`, `ErrorCommand` `git switch --no-guess feature/kinds`, buttons `Copy output`, `Stash changes and check out`, `Close` |
| The merge (phase 3's 2.8, phase 4) | operation `Merge feature/theirs`, running `Merging feature/theirs into main…`, status `Merge stopped: 1 file has conflicts` (warning); the banner of "A2's merge stop" above |
| The merge tool (phase 4's 11.9) | operation `Merge tool for notes.txt`, running `Waiting for slow to close…`; the status bar's Cancel (`OperationCancelButton`, tooltip `Stop git and the merge tool`); after a cancel `Merge tool for notes.txt cancelled` (secondary, the queue's `{Title} cancelled`) and no dialog |
| The abort (phase 3's 2.15, phase 4) | the question: title `Abort the merge?`, question `Abort merging "Merge branch 'feature/theirs'"? Changes made to resolve conflicts are lost.`, detail `A backup is saved first, so this can be undone.`, `Cancel` and `Abort merge` (danger); operation `Abort the merge`, running `Aborting the merge…`, status `Aborted the merge` |
| The backups list (5.1, 5.3, 12.3), A2 only | the page `Reflog` with `ReflogSource` reading `Backups`; rows `BackupRow` named by the backup's name, newest first: time `yyyy-MM-dd HH:mm:ss`, what (`Before a reset` for a `reset-…` without `-changes`), commit (`da31c97 Ours edits`) |
| Shell (phases 0 to 3) | the empty tab's title `New tab`; the welcome page `No repository open`, `Open, clone or init a repository to see its history.`, `Open`, `Clone`, `Init`, `Recent repositories`, `No recent repositories`; the status bar `No repository` (branch text on an empty tab), `Ready`, `Git ` and the version `git --version` gives (`Git 2.36.0` on the development machine), `Activity log`; the right panel `Commit details`, `Select a commit to see its details.` |
| Git, checkout refused (Git 2.36 and 2.55 alike) | standard error, four lines: `error: Your local changes to the following files would be overwritten by checkout:`, `⇥other.txt`, `Please commit your changes or stash them before you switch branches.`, `Aborting`; no standard output; exit 1 |
| Git, merge stopped (2.36 and 2.55 alike) | standard output, three lines: `Auto-merging notes.txt`, `CONFLICT (content): Merge conflict in notes.txt`, `Automatic merge failed; fix conflicts and then commit the result.`; exit 1 |

### Part B: the graph area's pages: file history, blame, "All files", the reflog and the backups

Part B covers checks B5 to B9 of design 15.4: a file's history across a rename, opened from the
stage panel, from a commit's file and from a diff's menu (B5); the blame with its gutter, a click and
a double-click on a run, `Blame before this commit` and `File history` (B6); the details panel's
`All files` and an unchanged file's blame in one and in two clicks, kept as a setting (B7); the
reflog page with its lost marks and its gap line, a branch's reflog, and `Create branch here…` from a
lost commit (B8); and the backups list with its restores of a deleted branch, discarded files, an
abort's and a skip's changes, a mixed reset's staged changes and a deleted remote branch (B9). It was
written before the UI it tests, from `docs/phase-5-design.md` sections 4, 5, 7.5, 7.6, 7.10, 11,
12, 13 and 15, decisions D153 to D181, the code WP0, WP1, WP2a, WP3's git half and WP-S merged
(`IGraphAreaPage`, `RepositoryMenus.Pages.cs`'s hooks, `GitRepository.FileHistory.cs`, `.Reflog.cs`,
`.Undo.cs`, `UndoTexts`, `Scenarios.Phase5.cs`) and the scenario facts derived for this report.
Every git state below was produced by plain Git 2.36.0.windows.1 on copies of the scenario
repositories made by the merged scenario code itself (their SHAs are the pinned ones), and again by
Git 2.55.0.windows.5 on copies that 2.55 built ("How the expected git states were derived", part B).
Where design 15.4's row was not enough for a concrete result, the value chosen is listed under
"Decisions this report makes" (BD1 to BD24).

#### Conventions of part B

- **Defaults.** Unless a check says otherwise, a test method writes a session with one tab on a
  fresh copy of the named scenario, starts the app at 1100×700 in the dark theme
  (`Phase5Checks.OpenAsync`), and waits until the graph has drawn its rows, the status is read and
  the queue is idle. On "reflog", whose two backups dated 2025-11-20 are older than 30 days by the
  harnesses' backup clock (2026-01-02 09:00:00 UTC, design 2.3), the check also waits, before its
  first input, for the background `Clean up backups` record whose message is `Removed 2 backups`
  (design 2.3, 15.2): the log then has the Info lines `reflog: Removed backup
  reset-20251120T090001000Z (older than 30 days)` and `reflog: Removed backup
  branch-20251120T090002000Z (older than 30 days)` (`IGitRepository.CleanUpBackupsAsync`'s
  `<repo>: Removed backup …`), and 15 backups are left. "History" holds no backup, so nothing is
  waited for there.
- **Test methods and app starts.** One test method per app start (phase 3's rule): B5
  `Check_B5_file_history` and `Check_B5_side_by_side_light`; B6 `Check_B6_blame`; B7
  `Check_B7_all_files` and `Check_B7_all_files_kept_after_a_restart` (a plain `[Fact]` with two app
  instances on one data folder through `FreshApplication.RunAsync`, as phase 1's check 15: D32's
  restart); B8 `Check_B8_reflog_and_a_branch_from_a_lost_commit`; B9
  `Check_B9_backups_list_branch_and_push_back`, `Check_B9_discarded_files_with_the_question`,
  `Check_B9_abort_and_skip_changes` and `Check_B9_staged_changes_of_a_mixed_reset`, each on a fresh
  copy. All of them are in `tests/VisualCommit.VisualTests.Phase5`, class `Phase5ChecksB`.
- **Clocks.** The scenarios' commits are dated by `TempRepo`'s clock (2026-01-01 12:00 plus a minute
  per step); what the app writes has the author and committer date 2026-01-02 09:00 UTC (D72) and
  the identity "Test Author" <author@example.com>, a snapshot "VisualCommit"
  <snapshot@visualcommit.invalid>. The harnesses' backup clock names the first backup the app makes
  in a run `…-20260102T090001000Z` and each later one a second after the last (design 2.3); the
  record clock is frozen at 2026-01-02 09:00:00 UTC. Dates are shown in UTC
  (`VISUALCOMMIT_TIME_ZONE=UTC`, D43).
- **Graph rows** are numbered from 0 at the top as in phase 4's report: with no working-changes row,
  commit row r's centre is at window y 129 + 26 r; in "history", where the working-changes row is
  display row 0, commit row r's centre is at y 155 + 26 r. A graph row is clicked "in its Message
  column": x 468 (2 lanes: 260 + 130 + 48 + 30). The working-changes row is clicked at
  `ShellDriver.WorkingRowPoint()`, (468, 129) here.
- **The pages** (design 11.1) take the graph area's place, its column headers included: window x 260
  to 700 and y 88 to 674 at 1100×700 (x 260 to 1520, y 88 to 1054 at 1920×1080). Each has a header
  36 high (y 88 to 124) and its content from y 124 (decision BD1). A page row (history, reflog,
  backups) is 26 high, row k from y 124 + 26 k, its centre at y 137 + 26 k; "click the row X" means
  a click at its centre on window x 420 (decision BD1), where no button lies. Positions are read from
  the page's bounds in both harnesses (D122's rule); the numbers here are what they come to with
  the default panel widths.
- **Text that may be cut.** A page's header is 440 wide at 1100×700. Its title (`FileHistoryTitle`,
  `BlameTitle`) and the blame's caption (`BlameCaption`) are `TrimmedTextBlock`s, as the history's
  subjects (design 4.1) and the reflog's action and commit (5.2) are: drawn whole or cut with "…",
  the whole text in the tooltip. A check names the whole text (`at c0bc2dd Read whole lines`); B7's
  captions are drawn cut (found by the gate's inspection; design 15.9). Which texts are cut is not
  asserted; `AssertNoClippedText` lets a cut text through only when it is trimmed and its tooltip
  holds it, as part A's convention of the same name says.
- **The blame's lines** (design 4.2, 13.4): the text (`BlameText`) and the gutter (`BlameGutter`) start
  at the page's content top (y 124) and share the code font's line height, 15.84: line L's centre is
  at y = top + (L − 0.5) × 15.84 (131.9 for line 1, 179.4 for 4, 274.5 for 10, 322.0 for 13, 432.9
  for 20, 464.6 for 22). "Click line L in the gutter" means x = `BlameGutter`'s left + 40 (window x
  300 at 1100×700); "click line L, column C of the text" means x = `BlameText`'s left + 30.4 + 2 +
  7.2 × (C − 1) (phase 4's `ResultPoint`; 30.4 is the number column for up to 99 lines), so column
  1 is at x 422.4 at 1100×700, where the gutter is 130 wide (x 260 to 390, design 4.2) and the text
  310 (x 390 to 700). A right-click or a double-click is made at the same point.
- **Menus** are written as phase 3's report writes them: header first, "—" for a separator,
  "(disabled: "…")" for a disabled item with its tooltip, "(highlighted)" for the item that has the
  focus, which is the first enabled one when a menu opens (D105). A menu that fits is 8 + 24 + 26 ×
  items + 9 × separators high (±2) and lies at least 4 px inside the window; opened by a right-click
  its top-left corner is 2 right of and 2 below the pointer, moved to stay inside the window
  (phase 3's 4a), and a check asserts its position only where it says so. The reflog's source menu
  opens with its top-left corner at the bottom-left corner of `ReflogSource` (decision BD7, as phase
  4's action button menus, 14.3). A right-click on a page row or a blame run first selects it as a
  click does (decision BD9; phase 3's 4b for graph rows).
- **Resting the pointer**, **"no menu is open"**, **"no dialog"**, **log lines** and **the pointer
  before a capture** (the neutral point (760, 687) at 1100×700 and 1920×1080, unless the step rests
  it on something or a menu is open) are phase 3's. The neutral point lies in the status bar at
  1100×700 and below every page row at 1920×1080.
- **Waits.** After opening a page the check waits until its rows (or lines) are loaded: `Loading…`
  gone and the row count the check states reached (the history streams its rows; the blame and
  the reflog arrive at once). After an operation it waits for the queue's log line `<repo>:
  <Title>: Succeeded after <N> ms: <message>` and until idle; after a click on a page row, until
  the details panel is not reading (`ShellDriver.WaitForCommitDetails`). The reflog page, with any
  of its sources (a reflog or `Backups`), reads again on every refresh while it is open (design 5.2,
  `IGraphAreaPage.OnRefsRead`), so after an operation B8 and B9 wait for that page's next read
  before they assert it. The history and the blame do not read again: their start is resolved when
  the page opens and stays fixed (design 4), so no check waits for a read of theirs after opening.
- **The right panel.** "Shows X" means the details panel with X's subject, body, author
  `Name <email>`, Date (author date), a "Commit date" row only when it differs, Parents (short
  SHAs) and `Changed files (N)` (or `All files (N)`) with each file's status letter, name and
  folder, as phase 1's report gives it. "The stage panel" is phase 2's, with phase 4's titles.
  Opening a page never changes the right panel; the user's click or key on a row, a run or a
  backup does (design 4.1, 11.1, 11.3, D176).
- **The keyboard focus** is asserted where the design or decision BD10 fixes it: on the history list's
  first row when it opens, on the blame text when it opens, on the reflog list (no row selected) when
  it opens, on a row after a click or a key on it, back on the page after a diff over it closes
  (11.1), and on the graph after a page closes (11.1's `GraphFocusRequested(Graph)`).
- **The toolbar's Undo** after a recorded action: enabled, its tooltip (and automation help text)
  `Undo {text}` with 7.6's entry text (part A's convention). Part B asserts it, the queue's log
  line and the status bar's text when done ("Operations" below) after each operation it starts;
  everything else of the toolbar and the status bar is part A's.
- **Git's own texts** on screen in this part are reflog messages, which git wrote when the scenario
  was built, and git's push output in the call log. The reflog texts and gaps came out the same on
  Git 2.36 and 2.55 (derivation); they are asserted word for word on 2.36 and 2.55, and on any
  other version a check asserts the selectors, commits, lost marks and the footer only (phase 3's
  rule). Git's states (refs, files, index, status, backups) are asserted on every version.
- **Pictures** are saved by `Shot(app, name)` under `artifacts/visual/phase-5/scripted/`, named
  `b<check><step letter>-<what>` (`PictureName()`'s rule); the real-window pass saves its captures
  of the same steps under the same names in `artifacts/visual/phase-5/real-window/`.

**Harness this part needs** (design 15.2; new helpers in `ShellDriver.Phase5.Pages.cs` and
`RealApp.Phase5.Pages.cs`, each tested in App.Tests as phase 4's are):

| Used for | ShellDriver (scripted) | RealApp (real window) |
|---|---|---|
| Waiting for a page | `WaitForPageAsync<TView>()` (the page's view shown and loaded) | `WaitForPage(string title)` (the pane named `History of …`, `Blame of …`, `Reflog of …`, `Backups`) |
| The history page | `FileHistorySnapshot()`: header texts, rows (letter and its brush, short SHA, subject, secondary text, author, date, which columns are visible, bounds, selected, focused), the hosted diff's `DiffViewModel` (header texts, side label, hunks) and the layout (list and diff bounds); `FileHistoryRowPoint(int row)` | `FileHistoryRows()` (by `FileHistoryRow` names, top to bottom), `FileHistoryRowPoint(string name)`; texts by automation id |
| The blame page | `BlameSnapshot()`: title, caption, the text, the runs (first and last line, commit, gutter texts, background brush, selected) and the gutter's width; `BlameGutterPoint(int line)`, `BlameTextPoint(int line, int column)` | `BlameText()` (the Document's Value), `BlameGutter()`'s bounds, `BlameGutterPoint(int line)`, `BlameTextPoint(int line, int column)` (this part's formula) |
| The reflog page and the backups list | `ReflogSnapshot()`: source label, rows (selector, time, action, commit, lost mark, visible columns, bounds, selected) or backup rows (name, time, words, commit), the footer; `ReflogRowPoint(string selector)`, `LostMarkBounds(string selector)`, `BackupRowPoint(string name)`, `BackupWordsBounds(string name)` | `ReflogRows()`, `BackupRows()` (by names), `ReflogRowPoint(string selector)`, `BackupRowPoint(string name)`, `ReflogFooterText()` |
| "All files" | `AllFilesButton` (`FindByAutomationId`), `DetailsFileRows()` (the details list's rows: letter, name, folder) | `AllFilesButton()` by id; the rows by `ChangedFile` names |
| The clean-up at opening | `WaitForOperationRecordAsync("Clean up backups")` (part A's helper) | `WaitForLogLine` of the record's log line |
| Everything else | phase 3's and 4's: `FileRowAsync`, `RefRowAsync`, `Menu()`, `ClickMenuItem`, `RestPointerOnAsync`, `RestPointerOnMenuItemAsync`, `WaitForToolTipAsync`, `DoubleClick`, `PressKey`, `TypeInto`, `WaitForDialogAsync`, `LogLines`; phase 3's `DiffRowOfLine` and `DiffTextPoint` (private to `Phase3Checks` today: moved into the driver or copied); part A's `ClickActivityLogToggle()` | phase 3's and 4's: `StageFileRow`, `RefRow`, `WaitForMenu`, `ClickMenuItem`, `RestPointerOn`, `ReplaceText`, `WaitForLogLine` |

#### Scenarios and graphs of part B

**"history"** (`Scenarios.HistoryAsync`, folder and tab `history`), as `Phase5ScenarioTests` pins it.
Three authors: "Test Author" <author@example.com>, **Mira Kovac** <mira@example.com>, **Jonas
Berg** <jonas@example.com> (author and committer alike). HEAD on `main` (tracking `origin/main`,
0 and 0); `feature/compare` tracks `origin/feature/compare`; `feature/readme` was deleted after its
merge; no stash, no tag. The working tree has ` M src/Reader.cs` only (its summary line changed and
a `Length` property added, not staged).

**H0, "history" as built** (2 lanes, Graph column 48; at 1100×700 the columns Branch / Tag, Graph,
Message (x 438, 142 wide) and Date (x 580 to 700); at 1920×1080 also Author and SHA). Display row 0 is
the working-changes row "Working changes" (ring in lane 0, colour 0, dashed line to row 0's node).

| Row | Labels | Node: lane, colour | Message | Date | SHA | Author | y |
|---|---|---|---|---|---|---|---|
| 0 | `main` ✓, `origin/main` (drawn `main` ✓ and `+1`) | 0, 0 | Read whole lines | 2026-01-01 12:13 | `c0bc2dd` | Jonas Berg | 155 |
| 1 | | 0, 0 | Stop tracing | 12:12 | `20d7fbe` | Test Author | 181 |
| 2 | | 0, 0 | Write new notes | 12:11 | `e765529` | Mira Kovac | 207 |
| 3 | | 0, 0 | Merge branch 'feature/readme' | 12:10 | `f513566` | Test Author | 233 |
| 4 | | 1, 1 | Explain the reader | 12:09 | `c1b6375` | Jonas Berg | 259 |
| 5 | | 0, 0 (lane 1 ends) | Remove the notes | 12:08 | `884b462` | Test Author | 285 |
| 6 | `feature/compare`, `origin/feature/compare` (drawn `feature/comp…` and `+1`) | 1, 2 | Compare whole lines | 12:07 | `245c395` | Jonas Berg | 311 |
| 7 | | 0, 0 | Rename the parser to reader | 12:06 | `0575c7f` | Mira Kovac | 337 |
| 8 | | 1, 2 | Add the comparer | 12:05 | `d0d3c77` | Jonas Berg | 363 |
| 9 | | 0, 0 (lane 1 ends) | Add tracing | 12:04 | `14a5c6c` | Test Author | 389 |
| 10 | | 0, 0 | Handle empty input | 12:03 | `365482b` | Jonas Berg | 415 |
| 11 | | 0, 0 | Add the logo | 12:02 | `775113f` | Test Author | 441 |
| 12 | | 0, 0 | Add the parser | 12:01 | `1d5a7cf` | Mira Kovac | 467 |
| 13 | | 0, 0 | Initial commit | 12:00 | `7829335` | Test Author | 493 |

Lines: lane 0 (colour 0) from row 0 to row 13; lane 1 (colour 1) from row 3's node (the merge's
second parent) into row 4's node, then into row 5's; lane 1 (colour 2) from row 6's node into row 8's,
then into row 9's. Full SHAs of the commits this part names: Read whole lines
c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9, Stop tracing 20d7fbe203a4a8ceeb5ef00da13ceda91ed70739,
Explain the reader c1b63754e14cab0e146b0811ca11371ba2bbce55, Remove the notes
884b4622b22f86236cbaae78b04234e63b4a6a88, Rename the parser to reader
0575c7f6631b3ba6c9d623f53b28489405230cfd, Add tracing 14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c,
Handle empty input 365482be62488b4c1259f0fb7dfa1631c797470b, Add the logo
775113f4ae456d7dc7dc5380c669b0d2950289e2, Add the parser 1d5a7cf999727e7f724054348c81866a62025184,
Initial commit 7829335c71bb8f1161b668a29afb602632228425. Every message is its subject alone except
Handle empty input's (body `The tokenizer stops at an empty text now.`) and Compare whole lines'.

The commits' details as this part shows them (author date = commit date for every one):

| Commit | Author | Date | Parents | Changed files | All files (the tree, by path) |
|---|---|---|---|---|---|
| `c0bc2dd` Read whole lines | Jonas Berg <jonas@example.com> | 2026-01-01 12:13 | `20d7fbe` | (1) M Reader.cs src | (6) LICENSE, README.md, logo.png assets, notes.md docs, Config.cs src, Reader.cs src |
| `c1b6375` Explain the reader | Jonas Berg <jonas@example.com> | 12:09 | `884b462` | (1) M README.md | (5) LICENSE, README.md, logo.png assets, Config.cs src, Reader.cs src |
| `0575c7f` Rename the parser to reader | Mira Kovac <mira@example.com> | 12:06 | `14a5c6c` | (1) R Reader.cs src, renamed from src/Parser.cs (git `R084`) | (6) as `c0bc2dd`'s |
| `365482b` Handle empty input | Jonas Berg <jonas@example.com> | 12:03 | `775113f` | (1) M Parser.cs src | (5) LICENSE, README.md, logo.png assets, notes.md docs, Parser.cs src |
| `1d5a7cf` Add the parser | Mira Kovac <mira@example.com> | 12:01 | `7829335` | (1) A Parser.cs src | (4) LICENSE, README.md, notes.md docs, Parser.cs src |
| `7829335` Initial commit | Test Author <author@example.com> | 12:00 | none | (3) A LICENSE, A README.md, A notes.md docs | (3) the same |

**The file histories** (`git -c core.quotePath=true log --follow --root -M --name-status … <start> --
<path>`, newest first, no merge), each row as the page shows it: status letter, short SHA, subject,
secondary text (decision BD13: the first that applies in 4.1's order), author, date:

| Opened for | Start | Rows |
|---|---|---|
| src/Reader.cs | HEAD (`c0bc2dd`) | 0 `M` `c0bc2dd` Read whole lines, —, Jonas Berg, 2026-01-01 12:13 · 1 `R` `0575c7f` Rename the parser to reader, `renamed from src/Parser.cs`, Mira Kovac, 12:06 · 2 `M` `365482b` Handle empty input, `as src/Parser.cs`, Jonas Berg, 12:03 · 3 `A` `1d5a7cf` Add the parser, `added`, Mira Kovac, 12:01 |
| src/Reader.cs | `0575c7f` | rows 1 to 3 above, as rows 0 to 2 |
| src/Parser.cs | `365482b` | 0 `M` `365482b` Handle empty input, — · 1 `A` `1d5a7cf` Add the parser, `added` |
| src/Parser.cs | `14a5c6c` | the same two rows |

Each row's diff (the diff part; against the first parent, with the path at that commit):
`c0bc2dd` src/Reader.cs (blob db5a108 → 7061deb): one hunk `@@ -10,13 +10,13 @@ public sealed class
Reader`, 15 rows: `        _text = text ?? string.Empty;`, `    }`, an empty line, `-    public string[]
Words()`, `+    public string[] Lines()`, `    {`, `        if (_text.Length == 0)`, `        {`,
`            return [];`, `        }`, an empty line, `-        return _text.Split(' ');`, `+
return _text.Split('\n');`, `    }`, `}`. `0575c7f` (src/Parser.cs → src/Reader.cs, `similarity index
84%`, 51e5085 → db5a108): one hunk `@@ -1,11 +1,11 @@`: `-public sealed class Parser` / `+public
sealed class Reader` and `-    public Parser(string text)` / `+    public Reader(string text)`, the rest
context. `365482b` src/Parser.cs (76b3c45 → 51e5085): one hunk `@@ -7,11 +7,16 @@ public sealed class
Parser`: `-        _text = text;` / `+        _text = text ?? string.Empty;` and five added rows (`
if (_text.Length == 0)`, `        {`, `            return [];`, `        }`, an empty line). `1d5a7cf`
src/Parser.cs: a new file, one hunk `@@ -0,0 +1,17 @@`, 17 added rows from `namespace History;`.

**The blames** (`git -c i18n.logOutputEncoding=UTF-8 blame --porcelain --no-textconv [<sha>] --
<path>`); a run is consecutive lines of one commit; runs alternate between `VcPanelBackgroundBrush`
(run 0, 2, 4, …) and the window's background (run 1, 3, …):

| Blame | Lines | Runs: lines, commit |
|---|---|---|
| src/Reader.cs in the working tree | 24 | 0: 1–2 `1d5a7cf` · 1: 3 not committed yet · 2: 4 `0575c7f` · 3: 5–7 `1d5a7cf` · 4: 8 `0575c7f` · 5: 9 `1d5a7cf` · 6: 10 `365482b` · 7: 11–12 `1d5a7cf` · 8: 13 `c0bc2dd` · 9: 14 `1d5a7cf` · 10: 15–19 `365482b` · 11: 20 `c0bc2dd` · 12: 21 `1d5a7cf` · 13: 22–23 not committed yet · 14: 24 `1d5a7cf` |
| src/Parser.cs at `14a5c6c` (Add tracing), and at `365482b` | 22 | 0: 1–9 `1d5a7cf` · 1: 10 `365482b` · 2: 11–14 `1d5a7cf` · 3: 15–19 `365482b` · 4: 20–22 `1d5a7cf` |
| LICENSE at `c0bc2dd` | 6 | 0: 1–6 `7829335` (boundary) |
| README.md at `c0bc2dd` | 5 | 0: 1–3 `7829335` · 1: 4–5 `c1b6375` (Explain the reader, the side branch's commit, not the merge) |

The working tree's src/Reader.cs, line by line: 1 `namespace History;`, 2 empty, 3 `/// <summary>Reads
the lines of a text.</summary>`, 4 `public sealed class Reader`, 5 `{`, 6 `    private readonly string
_text;`, 7 empty, 8 `    public Reader(string text)`, 9 `    {`, 10 `        _text = text ??
string.Empty;`, 11 `    }`, 12 empty, 13 `    public string[] Lines()`, 14 `    {`, 15 `        if
(_text.Length == 0)`, 16 `        {`, 17 `            return [];`, 18 `        }`, 19 empty, 20 `
return _text.Split('\n');`, 21 `    }`, 22 empty, 23 `    public int Length => _text.Length;`, 24
`}`. src/Parser.cs at `14a5c6c` is the same as at `365482b`: lines 4 `public sealed class Parser`, 8
`    public Parser(string text)`, 13 `    public string[] Words()`, 20 `        return
_text.Split(' ');`. Per commit: `1d5a7cf` Mira Kovac, 2026-01-01 12:01, path src/Parser.cs, no
`previous` (Blame before this commit disabled); `0575c7f` Mira Kovac, 12:06, path src/Reader.cs,
previous `14a5c6c` src/Parser.cs; `365482b` Jonas Berg, 12:03, path src/Parser.cs, previous
`775113f` src/Parser.cs; `c0bc2dd` Jonas Berg, 12:13, path src/Reader.cs, previous `20d7fbe`
src/Reader.cs; `7829335` Test Author, 12:00, boundary; `c1b6375` Jonas Berg, 12:09, previous
`884b462` README.md. The lines not committed yet have the zero SHA, author `Not Committed Yet`,
summary `Version of src/Reader.cs from src/Reader.cs`, previous `c0bc2dd` src/Reader.cs, and an
author time that is git's wall clock (2.36 and 2.55): it is never shown (decision BD11). LICENSE's
text: `MIT License`, empty, `Copyright (c) 2026 The History Authors`, empty, `Permission is hereby
granted, free of charge, to any person obtaining a copy`, `of this software, to deal in the software
without restriction.`; README.md's at `c0bc2dd`: `# History`, empty, `A repository for the history
checks.`, empty, `The reader splits a text into lines.`

**"reflog"** (`Scenarios.ReflogAsync`, folder and tab `reflog`), as `Phase5ScenarioTests` pins it.
All by Test Author. HEAD on a clean `main`, which tracks `origin/main` (ahead 2); `topic` tracks
`origin/topic` (0, 0); `clash` and `feature/renamed` have no upstream; tag `v0.8` (lightweight) on
Initial commit; no stash. `origin` (`origin.git` next to the working tree) has `main` at `6d7a40b`
and `topic` at `362fbd6` only (no `feature/gone`, no tag).

**RL0, "reflog" as built** (2 lanes, Message x 438, Date x 580 to 700; no working-changes row):

| Row | Labels | Node: lane, colour | Message | Date | SHA | y |
|---|---|---|---|---|---|---|
| 0 | `feature/renamed` | 0, 0 | Draft work | 2026-01-01 12:25 | `6f8d627` | 129 |
| 1 | `main` ✓ | 0, 0 | Write the notes | 12:21 (commit date 12:22) | `9dd61fd` | 155 |
| 2 | `topic`, `origin/topic` | 1, 1 | Topic work | 12:02 (commit date 12:09) | `362fbd6` | 181 |
| 3 | | 0, 0 (lane 1 ends) | Clash notes | 12:05 (commit date 12:08) | `316166a` | 207 |
| 4 | `clash` | 1, 2 | Clash notes | 12:05 | `fe14262` | 233 |
| 5 | | 1, 2 | Clashing work | 12:04 | `3b2501a` | 259 |
| 6 | `origin/main` | 0, 0 | Main work | 12:03 | `6d7a40b` | 285 |
| 7 | | 0, 0 (lane 1 ends) | Add the app | 12:01 | `7dd8e8f` | 311 |
| 8 | `v0.8` | 0, 0 | Initial commit | 12:00 | `5c9c1cb` | 337 |

Lines: lane 0 (colour 0) from row 0 to row 8; lane 1 (colour 1) from row 2's node into row 3's; lane 1
(colour 2) from row 4's node into row 5's, through row 6, into row 7's. Left panel: Local branches `4`
(▸ `feature`, `clash`, `main` ↑2, `topic`), Remotes `2` (`origin` › `main`, `topic`), Pull requests
`0`, Tags `1`, Stashes `0`. Full SHAs: Draft work 6f8d6279babf558e68666938b188d1b4ad0298ff, Write the
notes 9dd61fde06301292b9b14d0911560b6d987f645d, Topic work 362fbd633bc18c7553bf271ad7d31f1db055af2d,
Clash notes (main) 316166adc8e550ebb513a7f8c0ea99ecbdd88983, Main work
6d7a40b76bb9156c0969e533b292cbe2357e6783, Add the app 7dd8e8f7958138d967be47cb167b84bac7fd7467,
Initial commit 5c9c1cb91ada4146b5fb15a7229c98b6d79dbd58.

**Lost commits** (no branch, tag, remote branch or HEAD reaches them; none is a graph row): `44e646b`
Doomed work (44e646bdcb1d920659f2b28b1b44e19f6ae45a7b; 12:26; parent `9dd61fd`; A docs/doomed.txt),
`d07c677` Try a shortcut (d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f; 12:23; parent `9dd61fd`; A
src/shortcut.txt), `c7bf508` Draft the notes, `7e2ce3b` Detached experiment, `f7a69be` Topic work
(the rebase's old commit).

**HEAD's reflog** (design 5.2's two walks; 36 entries, `HEAD@{5}` skipped: the null entry the rename of
the checked-out branch wrote). k is the page row; its centre is at y 137 + 26 k; rows 0 to 19 are wholly
in view above the footer (decision BD5). The time column is hidden at 1100×700 (decision BD3) and is
given for the test surface:

| k | Selector | Time | Action | Commit | Lost |
|---|---|---|---|---|---|
| 0 | `HEAD@{0}` | 2026-01-01 12:27 | checkout: moving from doomed to main | `9dd61fd` Write the notes | |
| 1 | `HEAD@{1}` | 12:26 | commit: Doomed work | `44e646b` Doomed work | yes |
| 2 | `HEAD@{2}` | 12:26 | checkout: moving from main to doomed | `9dd61fd` Write the notes | |
| 3 | `HEAD@{3}` | 12:26 | checkout: moving from feature/renamed to main | `9dd61fd` Write the notes | |
| 4 | `HEAD@{4}` | 12:26 | Branch: renamed refs/heads/draft to refs/heads/feature/renamed | `6f8d627` Draft work | |
| 5 | `HEAD@{6}` | 12:25 | commit: Draft work | `6f8d627` Draft work | |
| 6 | `HEAD@{7}` | 12:25 | checkout: moving from main to draft | `9dd61fd` Write the notes | |
| 7 | `HEAD@{8}` | 12:25 | reset: moving to 9dd61fde06301292b9b14d0911560b6d987f645d | `9dd61fd` Write the notes | |
| 8 | `HEAD@{9}` | 12:23 | commit: Try a shortcut | `d07c677` Try a shortcut | yes |
| 9 | `HEAD@{10}` | 12:22 | commit (amend): Write the notes | `9dd61fd` Write the notes | |
| 10 | `HEAD@{11}` | 12:21 | commit: Draft the notes | `c7bf508` Draft the notes | yes |
| 11 | `HEAD@{12}` | 12:21 | checkout: moving from topic to main | `316166a` Clash notes | |
| 12 | `HEAD@{13}` | 12:21 | checkout: moving from main to topic | `362fbd6` Topic work | |
| 13 | `HEAD@{14}` | 12:20 | reset: moving to 316166adc8e550ebb513a7f8c0ea99ecbdd88983 | `316166a` Clash notes | |
| 14 | `HEAD@{15}` | 12:12 | reset: moving to HEAD | `316166a` Clash notes | |
| 15 | `HEAD@{16}` | 12:12 | checkout: moving from 7e2ce3b66ecd286fc7ce6694d1f57ca6acf4f0d3 to main | `316166a` Clash notes | |
| 16 | `HEAD@{17}` | 12:10 | commit: Detached experiment | `7e2ce3b` Detached experiment | yes |
| 17 | `HEAD@{18}` | 12:10 | checkout: moving from main to 316166adc8e550ebb513a7f8c0ea99ecbdd88983 | `316166a` Clash notes | |
| 18 | `HEAD@{19}` | 12:10 | checkout: moving from topic to main | `316166a` Clash notes | |
| 19 | `HEAD@{20}` | 12:09 | rebase (finish): returning to refs/heads/topic | `362fbd6` Topic work | |
| 20 | `HEAD@{21}` | 12:09 | rebase (pick): Topic work | `362fbd6` Topic work | |
| 21 | `HEAD@{22}` | 12:09 | rebase (start): checkout 316166adc8e550ebb513a7f8c0ea99ecbdd88983 | `316166a` Clash notes | |
| 22 | `HEAD@{23}` | 12:09 | checkout: moving from main to topic | `f7a69be` Topic work | yes |
| 23 | `HEAD@{24}` | 12:08 | cherry-pick: Clash notes | `316166a` Clash notes | |
| 24 | `HEAD@{25}` | 12:08 | reset: moving to 6d7a40b76bb9156c0969e533b292cbe2357e6783 | `6d7a40b` Main work | |
| 25 | `HEAD@{26}` | 12:07 | reset: moving to HEAD | `6d7a40b` Main work | |
| 26 | `HEAD@{27}` | 12:06 | checkout: moving from clash to main | `6d7a40b` Main work | |
| 27 | `HEAD@{28}` | 12:05 | commit: Clash notes | `fe14262` Clash notes | |
| 28 | `HEAD@{29}` | 12:04 | commit: Clashing work | `3b2501a` Clashing work | |
| 29 | `HEAD@{30}` | 12:04 | checkout: moving from main to clash | `7dd8e8f` Add the app | |
| 30 | `HEAD@{31}` | 12:03 | commit: Main work | `6d7a40b` Main work | |
| 31 | `HEAD@{32}` | 12:03 | checkout: moving from topic to main | `7dd8e8f` Add the app | |
| 32 | `HEAD@{33}` | 12:02 | commit: Topic work | `f7a69be` Topic work | yes |
| 33 | `HEAD@{34}` | 12:02 | checkout: moving from main to topic | `7dd8e8f` Add the app | |
| 34 | `HEAD@{35}` | 12:01 | commit: Add the app | `7dd8e8f` Add the app | |
| 35 | `HEAD@{36}` | 12:00 | commit (initial): Initial commit | `5c9c1cb` Initial commit | |

**Branch reflogs** (git prints `refs/heads/main@{0}`; the page shows `main@{0}`, design 5.2): `main`
(8, none skipped): `main@{0}` 12:25 reset: moving to 9dd61fde06301292b9b14d0911560b6d987f645d,
`9dd61fd` Write the notes; `main@{1}` 12:23 commit: Try a shortcut, `d07c677` Try a shortcut (lost);
`main@{2}` 12:22 commit (amend): Write the notes, `9dd61fd`; `main@{3}` 12:21 commit: Draft the
notes, `c7bf508` Draft the notes (lost); `main@{4}` 12:08 cherry-pick: Clash notes, `316166a`;
`main@{5}` 12:03 commit: Main work, `6d7a40b`; `main@{6}` 12:01 commit: Add the app, `7dd8e8f`;
`main@{7}` 12:00 commit (initial): Initial commit, `5c9c1cb`. `topic` (3): `topic@{0}` 12:09 rebase
(finish): refs/heads/topic onto 316166adc8e550ebb513a7f8c0ea99ecbdd88983, `362fbd6` Topic work;
`topic@{1}` 12:02 commit: Topic work, `f7a69be` Topic work (lost); `topic@{2}` 12:02 branch: Created
from HEAD, `7dd8e8f` Add the app. The source menu's branches: `clash`, `feature/renamed`, `main`,
`topic` (and `shortcut` once B8 has made it).

**The backups list after the clean-up at opening** (`ReadBackupsAsync`; 15 backups, 20 refs; newest
first; k is the row, its centre at y 137 + 26 k; time from the name, UTC; words by design 12.3;
commit = the first ref's):

| k | Name (`BackupRow`) | Time | What | Commit | Refs |
|---|---|---|---|---|---|
| 0 | `branch-20260101T122701000Z` | 2026-01-01 12:27:01 | Deleted branch doomed | `44e646b` Doomed work | `/doomed` → 44e646b |
| 1 | `reset-20260101T122401000Z` | 2026-01-01 12:24:01 | Before a hard reset to 9dd61fd | `d07c677` Try a shortcut | → d07c677; `-changes` → 21c5a38 (parents d07c677 and f174bf8 "VisualCommit: index before a hard reset"; message `VisualCommit: snapshot before a hard reset to 9dd61fd`, paths README.md) |
| 2 | `discard-20260101T122001000Z` | 2026-01-01 12:20:01 | Before discarding 2 files | `9849838` VisualCommit: snapshot before discarding 2 files | → 9849838 (parent 316166a; notes/todo.txt `todo\n` (A), src/app.txt `app\nmain work\nstaged\n` (M)) |
| 3 | `reset-20260101T121901000Z` | 2026-01-01 12:19:01 | Before a mixed reset | `316166a` Clash notes | → 316166a; `-changes` → 57a5e6e ("VisualCommit: index before a mixed reset", parent 316166a, M src/app.txt `app\nmain work\nstaged\n`) |
| 4 | `remote-20260101T121801000Z` | 2026-01-01 12:18:01 | Removed remote mirror | `316166a` Clash notes | `/mirror/main` → 316166a, `/mirror/topic` → 362fbd6 |
| 5 | `remote-branch-20260101T121701000Z` | 2026-01-01 12:17:01 | Deleted feature/gone on origin | `7dd8e8f` Add the app | `/origin/feature/gone` → 7dd8e8f |
| 6 | `remote-tag-20260101T121601000Z` | 2026-01-01 12:16:01 | Deleted tag v0.8 on origin | `5c9c1cb` Initial commit | `/origin/v0.8` → 5c9c1cb |
| 7 | `tag-undone-20260101T121501000Z` | 2026-01-01 12:15:01 | Tag v1.0-rc, its creation undone | `316166a` Clash notes | `/v1.0-rc` → tag object 7abc965 |
| 8 | `tag-20260101T121401000Z` | 2026-01-01 12:14:01 | Deleted tag v0.9 | `7dd8e8f` Add the app | `/v0.9` → tag object fa4c0b7 |
| 9 | `stash-20260101T121301000Z` | 2026-01-01 12:13:01 | Dropped or popped stash "On main: Try a README" | `39daf6c` On main: Try a README | → 39daf6c |
| 10 | `detached-20260101T121101000Z` | 2026-01-01 12:11:01 | Commits a detached HEAD left behind | `7e2ce3b` Detached experiment | → 7e2ce3b |
| 11 | `force-push-20260101T120901000Z` | 2026-01-01 12:09:01 | Before a force push of origin/topic | `f7a69be` Topic work | `/origin/topic` → f7a69be |
| 12 | `rebase-20260101T120801000Z` | 2026-01-01 12:08:01 | Before rebasing topic | `f7a69be` Topic work | `/topic` → f7a69be |
| 13 | `skip-20260101T120701000Z` | 2026-01-01 12:07:01 | Before skipping a commit | `6d7a40b` Main work | → 6d7a40b; `-changes` → 946fb4b (message `VisualCommit: snapshot before an abort`, path src/app.txt, its src/app.txt `app\nresolved, then skipped\n`) |
| 14 | `abort-20260101T120601000Z` | 2026-01-01 12:06:01 | Before aborting | `6d7a40b` Main work | → 6d7a40b; `-changes` → 08b41a4 (message `VisualCommit: snapshot before an abort`, paths docs/clash.txt and src/app.txt; docs/clash.txt `clash notes\n`, src/app.txt `app\nmain work and clash\n`) |

Full ids: 9849838c39b980387b976a8a864b7c6d1ffda865, 57a5e6edfbd470dc411b4ea3bb4eaabc405aa571,
21c5a38946ba1dc3ce852e135922ae8f676a0ce4, 946fb4b9d6331facefff73c01cdd1e8271276db2,
08b41a45cd0d6f8571d150651e979bd6be73a9c4. At HEAD (`9dd61fd`) src/app.txt holds `app\nmain work\n` and
docs/clash.txt `clash notes\n`; docs/notes.txt (added by Write the notes) holds `notes\nwritten\n`.
The snapshot 9849838's details: subject `VisualCommit: snapshot before discarding 2 files`, body
`notes/todo.txt`, `src/app.txt`, author `VisualCommit <snapshot@visualcommit.invalid>`, Date
2026-01-01 12:20, Parents `316166a`, Changed files (2): A todo.txt notes, M app.txt src.

**Graphs after this part's actions** (laid out by D48, D54 and D55 from `git log --date-order
--branches --remotes --tags --ignore-missing HEAD --`; the backup refs are not loaded, D47):

- **RL-B8, after `Create branch here…` named `shortcut` at `d07c677`** (B8): 10 rows, 2 lanes: 0
  `feature/renamed` Draft work (0, 0); 1 `shortcut` Try a shortcut `d07c677` (1, 1), 2026-01-01 12:23,
  y 155; 2 `main` ✓ Write the notes (0, 0; lane 1 ends); 3 `topic`, `origin/topic` Topic work (1, 2);
  4 Clash notes `316166a` (0, 0; lane 1 ends); 5 `clash` Clash notes (1, 3); 6 Clashing work (1, 3); 7
  `origin/main` Main work (0, 0); 8 Add the app (0, 0; lane 1 ends); 9 `v0.8` Initial commit (0, 0).
  Lines: lane 0 (colour 0) rows 0 to 9; lane 1 (colour 1) from row 1's node into row 2's; lane 1 (colour
  2) from row 3's node into row 4's; lane 1 (colour 3) from row 5's node into row 6's, through row 7,
  into row 8's. HEAD's reflog unchanged (36 entries); lost now: `44e646b`, `c7bf508`, `7e2ce3b`,
  `f7a69be` (`d07c677` is reached by `shortcut`).
- **RL-B9a, after `Restore branch doomed`** (B9): 10 rows: 0 `doomed` Doomed work `44e646b` (0, 0),
  2026-01-01 12:26, y 129; 1 `feature/renamed` Draft work (1, 1); 2 `main` ✓ Write the notes (0, 0;
  lane 1 ends); 3 `topic`, `origin/topic` Topic work (1, 2); 4 Clash notes (0, 0; lane 1 ends); 5 `clash`
  Clash notes (1, 3); 6 Clashing work (1, 3); 7 `origin/main` Main work (0, 0); 8 Add the app (0, 0;
  lane 1 ends); 9 `v0.8` Initial commit. Lines: lane 0 (colour 0) rows 0 to 9; lane 1 (colour 1)
  from row 1's node into row 2's; lane 1 (colour 2) from
  row 3's into row 4's; lane 1 (colour 3) from row 5's into row 6's, through row 7, into row 8's.
  **RL-B9b**, after the push back too: the same, with the label `origin/feature/gone` on row 8 (Add the
  app); Remotes `3` (`origin` › ▸ `feature`, `main`, `topic`).

#### Texts these checks rely on (part B)

**File history** (design 4.1): the header `History of {name}` (`FileHistoryTitle`), the folder in the
secondary colour, `from {short}` "when it starts at a commit other than HEAD", the buttons `Blame`
(`FileHistoryBlame`) and close (`FileHistoryClose`); a row's secondary text `renamed from {old path}`
(R), `copied from {path}` (C), `added` or `deleted`, or `as {path}`; its automation name `{short}
{subject}`; the row menu (`ForFileHistoryRow`) `Show in graph` (disabled `This commit is not in the
graph`), `Blame at this commit`, `Copy SHA`; `Loading…`; `No commit has touched {name} yet`.

**Blame** (4.2): `Blame of {name}` (`BlameTitle`), `at {short} {subject}` or `in the working tree`
(secondary), the button `File history` (`BlameHistory`), close (`BlameClose`); the gutter's `Not
committed yet` in `VcWarningBrush`; the run menu (`ForBlameRun`) `Show in graph`, `Blame before this
commit` (disabled `This is the file's first commit`), `File history`, `Copy SHA`, and on a run not
committed yet only `Show in graph`; the tooltip's parts (the SHA, the author and e-mail, the date,
the subject, `as {path}`); `{name} is a binary file: it has no lines to blame`, `{name} is empty`.

**The file menus** (4.4, 13.1): `File history` and `Blame`, a group before `Open file`; disabled
`This file is in no commit yet` and `Make the first commit first`; a deleted file's blame titled
`at HEAD` or `before {short}`. Phase 3's items around them (`OperationTexts.MenuTexts`): `Stage`,
`Discard changes…`, `Ignore {name}`, `Ignore all *.{ext} files` (disabled `Only untracked files can be
ignored`), `Open file` (disabled `The file is not in the working tree`), `Show in folder`, `Copy
path`.

**All files** (4.5): the button `All files` (`AllFilesButton`, class `selected` while on), the title
`All files ({N})`; phase 1's `Changed files ({N})`, `Flat`, `Tree`.

**The reflog page** (5.1, 5.2): `Show reflog`, `Show backups`, `Show reflog of {b}`; the source
`HEAD`, a branch's name, `Backups`; a row's selector `HEAD@{n}` or `{b}@{n}`; the lost mark's tooltip
`No branch, tag or remote branch reaches this commit`; the entry menu (`ForReflogEntry`) `Create
branch here…`, `Check out this commit`, `Reset {cur} to this commit…`, `Cherry-pick onto {cur}`, `Show
in graph`, `Copy SHA`; the footer `1 entry is not shown: it names no commit that exists` / `{N}
entries are not shown: they name no commit that exists`; `{name} has no reflog`. The Local branches
section's menu after phases 3 and 5 (13.1; `ForSection`): `Create branch…`, `Show reflog`, `Show
backups`, `Show all hidden branches` (disabled `No branch is hidden`), `Stop soloing` (disabled `No
branch is soloed`).

**Backups in words** (12.3), as the list above gives them; the restores (7.10, 12.4): `Restore branch
{b}` (disabled `A branch named {b} exists`), `Restore the discarded files…`, `Restore the changes…`,
`Restore the staged changes` (disabled `Unstage your changes first`), `Push {b} back to {r}…`
(disabled `{r} no longer exists`), and for every backup that holds a commit `Create branch here…`,
`Check out this commit`, `Copy SHA`.

**Questions** (13.2): the push back (7.5): title `Push {b} back to {r}?`, text `{r} gets {b} back at
{short}.`, confirm `Push`, not destructive in style. D75's question (`RepositoryViewModel.RestoreAsync`):
title `Restore discarded changes?`, question `{file name} has changed since the discard. Put back the
version from before the discard?` / `{N} files have changed since the discard. Put back the versions
from before the discard?`, detail `A snapshot of the newer changes is saved first.`, `Cancel` (with
the keyboard focus) and `Restore` (danger, as the restore bar's).

**Operations** (12.1, 12.2, 7.6; log line `reflog: <Title>: Succeeded after <N> ms: <status>`):

| Item | Title | Running text | Status when done | Undo's tooltip after it |
|---|---|---|---|---|
| `Restore branch doomed` | `Restore branch doomed` | `Restoring doomed…` | `Restored branch doomed` | `Undo restore of branch doomed` |
| `Push feature/gone back to origin…` | `Push feature/gone back to origin` | `Pushing feature/gone back to origin…` | `Pushed feature/gone back to origin` | `Undo push of feature/gone back to origin` |
| `Restore the discarded files…` | `Restore the discarded files` | `Restoring the discarded files…` | `Restored 2 files` | `Undo restore of the discarded files` |
| `Restore the changes…` (abort's; skip's) | `Restore the changes` | `Restoring the changes…` | `Restored 2 files`; `Restored 1 file` | `Undo restore of the changes` |
| `Restore the staged changes` | `Restore the staged changes` | `Restoring the staged changes…` | `Restored the staged changes` | `Undo restore of the staged changes` |
| `Create branch here…` (phase 3) | `Create branch shortcut` | `Creating shortcut…` | `Created shortcut` | `Undo creation of shortcut` |

Phase 3's name dialog (2.4): title `Create branch`, `NameDialogContext` `From d07c677 "Try a
shortcut"`, `NameBox` (placeholder `Branch name`, the keyboard focus), `NameDialogOption` `Check out
the new branch` (ticked), `Cancel` and `Create branch` (disabled until a name is typed).

**Colours** (dark; light in brackets where a check uses it): status letters A `VcSuccessBrush`
`#3FB97F` (`#1E8E5A`), M `VcWarningBrush` `#E0A23B` (`#B7791F`), D `VcDangerBrush` `#E5606B`, R and C
`VcAccentBrush` `#7C8CFF` (`#4353D8`); selection `#2A3150` (`#DDE1FA`); panel background `#1A1D24`;
window background `#14161B`; secondary text `#9BA3B0` (`#5C6572`); the lost mark and `Not committed
yet` `#E0A23B`.

### Part F: search, compare, solo and hide, and dashed stashes

Part F covers design sections 3 (search: the bar, the three modes, the marks, results kept across
reloads), 6 (compare: two selected commits, two branches from a branch menu and from a drop, the
view), 8 (solo and hide: what the graph loads, the left panel, the session) and 10 (every stash's
line dashed), with 11.1 and 11.2 (Search closes a diff), 11.3 (`Reveal`), 12.4 (the new disabled
reasons), 13.1 (the compare, hide and solo items, and the comparison's file menu `ForCompareFile`),
13.3 (`VcSearchMatchBrush`) and 13.4 (the
automation ids): checks F10 to F15 of design 15.4, and decisions D157, D159, D161, D162, D174, D177
and D178. It was written on 2026-10-15, before the UI it tests (WP2b's drawing, WP7's search,
compare and filters), from `docs/phase-5-design.md`, the merged git layer (WP1's
`GitRepository.Search.cs` and `.Compare.cs`, WP2a's `GraphFilter` and dashed lanes) and the
menu builders as WP0 left them (`RepositoryMenus.cs` with the hooks of `RepositoryMenus.Filters.cs`
and `.Pages.cs`). Every git state below was derived with Git 2.36.0.windows.1, and the search's
calls, the comparisons' `rev-parse` and the drop's ancestor read also with Git 2.55.0.windows.5, on
copies of the scenarios built by the scenario code itself
(`Scenarios.HistoryAsync`, `FiltersAsync`, phase 1's `GraphAsync`), whose SHAs equal the pinned
ones (see "How the expected git states were derived", part F). Geometry comes from
the scenario facts' measurements of the running app, except the search bar's, which is not built yet
and is derived from design 3.1.

#### Conventions of part F

- **Defaults.** Unless a check says otherwise, a test method writes a session with one tab on a
  fresh copy of the named scenario, starts the app at 1100×700 in the dark theme
  (`Phase5Checks.OpenAsync`), waits until the graph has drawn its rows, the status is shown and the
  queue is idle (`WaitForRepositoryAsync`), and rests the pointer at the neutral point. The test
  methods, all in the class `Phase5ChecksF` of `VisualCommit.VisualTests.Phase5` (files
  `Phase5Checks.F*.cs`, expectations in `Phase5Expectations.F.cs`, nested class `PartF`):
  `Check_F10_search_commits` and `Check_F10_search_light_1920` (one start each),
  `Check_F11_search_files_and_changes`, `Check_F12_compare_two_commits`,
  `Check_F13_compare_branches`, `Check_F14_hide_and_show`, `Check_F14_solo_and_stop_soloing` (one
  start each), `Check_F14_hidden_after_a_restart` (two starts on one data folder, each half a
  `FreshApplication.RunAsync` with a new application, as phase 1's check 15 does a restart, D32),
  `Check_F15_graph_scenario_stash_dashed` and `Check_F15_filters_stashes_dashed` (one start each).
  None makes an operation of the queue except what the app runs for itself; none comes near the hang
  watchdog's 3 minutes.
- **Rows and positions.** No part F check shows a banner. Rows are numbered from 0 at the top as
  commit (and stash) rows, `SelectedIndex`'s numbering; in "history" the working-changes row is
  display row 0 above commit row 0. The graph control (`GraphRows`) starts at window y 116 with the
  search bar closed and at y 152 with it open (the bar is 36 high under the toolbar, which ends at
  y 88, and pushes the 28-high column headers and the rows down, design 3.1). Rows are 26 high, so:
  without a working-changes row commit row r's centre is y 129 + 26 r (bar closed); in "history"
  the working-changes row's centre is y 129 and commit row r's 155 + 26 r with the bar closed, y 165
  and 191 + 26 r with it open. Lane L's centre is x 406 + 16 L. The Message column starts at x 438
  (2 lanes), 454 (3) or 486 (5); a row is clicked "in its Message column" 30 px into it, at x 468,
  484 or 516. These numbers are what the controls' bounds give; the checks take every position
  from the bounds (`GraphRowPoint`, `GraphLabelBounds`, `RefRowAsync`, the bar's controls), as
  D122's rule says, never from constants.
- **Labels, the left panel, selection.** Labels are in phase 1's drawing order; "drawn" is what the
  graph draws at 1100×700 (a name cut with "…", "+N"). A label is pressed or right-clicked at its
  centre, and only a drawn one. A right-click or press on a graph row or label selects that row
  (phase 3's 4b, 4g); a right-click on a left-panel row opens its menu only and selects nothing
  (D114), which is why part F opens branch menus from the left panel. Left-panel paths are written
  Local branches › feature › alpha and Remotes › origin › feature › alpha; a folder that does not
  hold HEAD's branch starts closed, so a check clicks it open first. Ctrl+click is the platform's
  command modifier (`ShellDriver.CommandModifier`).
- **Menus** are written header first, items in order, "—" for a separator; "(danger)" an enabled
  item in `VcDangerBrush`, "(disabled: "…")" a disabled item with its tooltip, "(highlighted)" the
  item with the focus (D105: the first enabled one when a menu opens). Every check asserts the
  height, 8 + 24 + 26 × items + 9 × separators (±2), and that the menu lies at least 4 px inside the
  window; only the search's mode menu has its position stated (FD5). Phase 5's group of compare,
  reflog, hide and solo items stands before "Copy branch name" (13.1); its "Show reflog of {b}",
  "Show reflog" and "Show backups" are part B's items, listed here because they are in the same
  menus.
- **The pointer.** Resting the pointer on an item, a button or a row, and the neutral point
  (760, 687) before a capture with no menu open that is not about a hover, are as phase 3's report
  defines them. At 1920×1080 the neutral point lies in the graph area below the last row of every
  part F scenario (with the bar open, "history"'s last row ends at y 542): nothing there has a hover
  or a tooltip. A capture with a menu open leaves the pointer where the right-click or release
  opened it.
- **Waits.** `WaitForIdleAsync` for the queue; for a search, `WaitForSearchAsync` (new, WP-S): until
  the search of the box's current text and mode has ended and its matches are applied (the box
  searches 300 ms after the last key in the Commits mode and 600 ms in the others, design 3.1; the
  wait is on the condition, never a fixed time); for a filter, `WaitForGraphReloadAsync` (new): until
  `GraphReloading` is hidden and the graph's data is the one the new filter loads, then
  `WaitForIdleAsync`; for a comparison, `WaitForCompareAsync` (new): until `Loading…` is gone and,
  for branches, the counts are shown; for a change made outside the app, polling on the condition
  the step names with `OutsideChangeTimeout` (30 s).
- **Search marks** are read from the graph control's state (`CommitGraphControl.Search`, a
  `GraphSearchMarks`, and the control's per-row state: marked, dimmed), never from pixels (design
  15.2), through `ShellDriver.SearchMarks()` (new). Pixels are sampled only where a step says so,
  each to ±3 per channel. "Dimmed" is 35 % opacity over the row's background (3.3); a dimmed node is
  sampled 3 px right of its centre, off the lane's line, which stays at full strength (FD6).
- **Other new helpers** (design 15.2, WP-S, each tested in App.Tests): `ShellDriver.SearchBar()`
  (mode, text, placeholder, whether the box has the focus, count, Previous and Next enabled and
  their tooltips, the close button's tooltip, `SearchProgress` shown, the parts' bounds);
  `ChooseSearchMode(label)` (a click on `SearchModeButton`, then `ClickMenuItem`);
  `ShellDriver.Compare()` (`CompareTitle`, `CompareFrom` and `CompareTo` with their texts, enabled
  state and tooltips, `CompareCounts`, `CompareAllDifferences` shown and checked, `CompareSwap` and
  `CompareClose` shown, the file rows as phase 1's `ChangedFileList` rows) and
  `ClickCompareFile(path)`; `ShellDriver.RefRowMarks(path)` (the name's brush, which toggle the row
  shows, its bounds and tooltip) and `RefRowNamePoint(path)`. The session is read from the data
  folder with `JsonSessionStore`. The support file gains `AssertDashedPixels` and `AssertSolidPixels`
  (FD15), tested in `Phase5SupportTests`. `RealApp.Phase5*.cs` finds the same controls by automation
  id and name.
- **Pictures** are named `f<check><step letter>-<what>` (`PictureName()`'s rule), taken with
  `Shot(app, name)` (the pointer moved to the neutral point unless a menu is open or the step rests
  it), and saved under `artifacts/visual/phase-5/scripted/`.
- **The call log** (`IGitCallLog`): "the call log has `git …`" means a `GitCallRecord` whose
  `CommandText` is exactly that (the arguments joined by spaces; none of part F's needs quoting),
  with the exit code and output stated. Searches, comparisons and the graph's loads are reads, not
  operations. A comparison names its two sides to git as FD20 says: two commits by their full SHAs,
  branches by their full ref names (`refs/heads/main`), and the drop's ancestor read by the drop's
  full ref names, as phase 4 built it.
- **Git's version.** Part F asserts no message of git word for word. The refs, commits, SHAs,
  matches and lists below are the same on Git 2.36 and 2.55; the one call whose raw output differs
  (the Files mode's, see F11) is asserted by its command, exit code and matches only.
- **The details panel** shows a commit as phases 1 and 3 do, with phase 5's `All files` button
  (off) beside Flat and Tree (design 4.5; part B checks it). The history scenario's commits have the
  same author and committer and the same dates, so no Committer and no Commit date row shows.

#### Scenarios and graphs of part F

**"history"** (`Scenarios.HistoryAsync`, folder and tab `history`), as scenario facts 2 give it
and `Phase5ScenarioTests` pins it: three authors (Test Author <author@example.com>, Mira Kovac
<mira@example.com>, Jonas Berg <jonas@example.com>, each author and committer of their commits);
HEAD on `main`, which tracks `origin/main`; `feature/compare` tracks `origin/feature/compare`; both
0 ahead and 0 behind; `origin` is the bare `origin.git` next to the working tree; no tag, no stash.
`git status --porcelain`: ` M src/Reader.cs` only (its summary line changed and a `Length` property
added, not staged). The copy's clock stands at 2026-01-01 12:14 (`TempRepo.ClockTime`).

**H0, "history" as built.** 2 lanes, Graph column 48; at 1100×700 the columns are Branch / Tag
(260 to 390), Graph (390 to 438), Message (438 to 580) and Date (580 to 700); at 1920×1080 also
Author (1188), Date (1328) and SHA (1448). Display row 0 is the working-changes row "Working
changes": its ring in lane 0, colour 0, its dashed line down to row 0's node (D60); centre y 129
(bar closed) or 165 (bar open).

| Row | Labels (drawn at 1100×700) | Node: lane, colour | Message | Date | SHA | Author | y, bar closed | y, bar open |
|---|---|---|---|---|---|---|---|---|
| 0 | `main` ✓, `origin/main` (drawn `main` ✓, `+1`) | 0, 0 | Read whole lines | 2026-01-01 12:13 | `c0bc2dd` | Jonas Berg | 155 | 191 |
| 1 | | 0, 0 | Stop tracing | 2026-01-01 12:12 | `20d7fbe` | Test Author | 181 | 217 |
| 2 | | 0, 0 | Write new notes | 2026-01-01 12:11 | `e765529` | Mira Kovac | 207 | 243 |
| 3 | | 0, 0, merge | Merge branch 'feature/readme' | 2026-01-01 12:10 | `f513566` | Test Author | 233 | 269 |
| 4 | | 1, 1 | Explain the reader | 2026-01-01 12:09 | `c1b6375` | Jonas Berg | 259 | 295 |
| 5 | | 0, 0 (lane 1 ends here) | Remove the notes | 2026-01-01 12:08 | `884b462` | Test Author | 285 | 321 |
| 6 | `feature/compare`, `origin/feature/compare` (drawn `feature/comp…`, `+1`) | 1, 2 | Compare whole lines | 2026-01-01 12:07 | `245c395` | Jonas Berg | 311 | 347 |
| 7 | | 0, 0 | Rename the parser to reader | 2026-01-01 12:06 | `0575c7f` | Mira Kovac | 337 | 373 |
| 8 | | 1, 2 | Add the comparer | 2026-01-01 12:05 | `d0d3c77` | Jonas Berg | 363 | 399 |
| 9 | | 0, 0 (lane 1 ends here) | Add tracing | 2026-01-01 12:04 | `14a5c6c` | Test Author | 389 | 425 |
| 10 | | 0, 0 | Handle empty input | 2026-01-01 12:03 | `365482b` | Jonas Berg | 415 | 451 |
| 11 | | 0, 0 | Add the logo | 2026-01-01 12:02 | `775113f` | Test Author | 441 | 477 |
| 12 | | 0, 0 | Add the parser | 2026-01-01 12:01 | `1d5a7cf` | Mira Kovac | 467 | 503 |
| 13 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `7829335` | Test Author | 493 | 529 |

Lines: lane 0 (colour 0) runs from row 0 to row 13; lane 1 (colour 1) from row 3's node (the
merge's second parent) into row 4's node, then into row 5's node; lane 1 (colour 2) from row 6's
node through row 7 into row 8's node, then into row 9's node. The `main` label is drawn from x 266
to 317 and `feature/comp…` from x 266 to 359 (each 18 high, its top 4 below the row's top). Full
SHAs: Read whole lines c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9, Stop tracing
20d7fbe203a4a8ceeb5ef00da13ceda91ed70739, Write new notes e765529af7bef5e017fb41b2de5e384d1eebc858,
Merge branch 'feature/readme' f5135668320ac7e48c60dd634a18f4eb3906f160 (parents 884b462 c1b6375),
Explain the reader c1b63754e14cab0e146b0811ca11371ba2bbce55, Remove the notes
884b4622b22f86236cbaae78b04234e63b4a6a88, Compare whole lines
245c395674db6ae89390be2db018db5356b446fe, Rename the parser to reader
0575c7f6631b3ba6c9d623f53b28489405230cfd, Add the comparer d0d3c77aa36bc7ad3ef71cff57e454453d4463b2,
Add tracing 14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c, Handle empty input
365482be62488b4c1259f0fb7dfa1631c797470b, Add the logo 775113f4ae456d7dc7dc5380c669b0d2950289e2, Add
the parser 1d5a7cf999727e7f724054348c81866a62025184, Initial commit
7829335c71bb8f1161b668a29afb602632228425. Every message is its subject alone except Handle empty
input (body `The tokenizer stops at an empty text now.`) and Compare whole lines (body `It splits the
texts with the tokenizer first.`); no subject and no author holds "tokenizer", and no message holds
"Jonas" or "0575". Each commit's changed files (against its first parent) are as scenario facts 2.3
give them; part F names those it shows. Left panel when it opens: Local branches `2` (folder
`feature`, closed, holding `compare`; `main` in the accent colour, no arrows), Remotes `2`
(`origin` › `feature` closed, `main`), Pull requests `0`, Tags `0`, Stashes `0`; `CurrentBranch`
`main`; nothing selected; the details panel "Select a commit to see its details.".

**H-F1, "history" after F11's commit made outside the app** ("Trace again", `f612569`, see F11 step
6), with the bar open. The working-changes row stays (src/Reader.cs is still changed). 2 lanes.

| Row | Labels | Node: lane, colour | Message | Date | SHA | y, bar open |
|---|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Trace again | 2026-01-01 12:14 | `f612569` | 191 |
| 1 | `origin/main` | 0, 0 | Read whole lines | 2026-01-01 12:13 | `c0bc2dd` | 217 |
| 2 to 14 | H0's rows 1 to 13, with their labels, lanes and colours | | | | | 191 + 26 r |

Lines: lane 0 (colour 0) runs from row 0 to row 14; lane 1 (colour 1) from row 4's node into row
5's node, then into row 6's node; lane 1 (colour 2) from row 7's node through row 8 into row 9's
node, then into row 10's node. Full SHA of Trace again f612569a47849c0326a3022a420c241d56d7ddde
(parent c0bc2dd, Test Author, author and commit date 2026-01-01 12:14; its change `M src/Config.cs`,
adding `    public const bool TraceEverything = true;` and an empty line). The left panel's `main`
shows ↑1; `git status -sb` prints `## main...origin/main [ahead 1]` and ` M src/Reader.cs`.

**What part F's searches match** (over H0's rows, git's part with 2.36 and 2.55 alike; the author
and SHA parts in memory, design 3.2):

| Mode, text | Matches (rows) | Count with nothing selected |
|---|---|---|
| Commits `tokenizer` | 6 Compare whole lines, 10 Handle empty input (message bodies) | `2 matches` |
| Commits `Jonas` | 0, 4, 6, 8, 10 (author Jonas Berg; no message) | `5 matches` |
| Commits `0575` | 7 Rename the parser to reader (SHA prefix; no message) | `1 match` |
| Files `parser` | 7 (the rename's old name, D src/Parser.cs without rename detection), 10 (M src/Parser.cs), 12 (A src/Parser.cs) | `3 matches` |
| Files `reader` | 0 (M src/Reader.cs), 7 (the rename's new name, A src/Reader.cs) | `2 matches` |
| Files `TraceEverything` | none | `No matches` |
| Changes `TraceEverything` | 1 Stop tracing (removed), 9 Add tracing (added) | `2 matches` |
| Changes `TraceEverything` over H-F1 | 0 Trace again (added), 2 Stop tracing, 10 Add tracing | `3 matches` |

**What part F's comparisons list** (`GitRepository.CompareAsync`'s calls run by hand; flat, in
git's order, as phase 1's `ChangedFileList` keeps it):

| Comparison | From → To | Old side | Files | Counts |
|---|---|---|---|---|
| F12, two commits | `0575c7f` (row 7) → `c0bc2dd` (row 0), two-dot | `0575c7f` | (4) M README.md, M docs/notes.md, M src/Config.cs, M src/Reader.cs | none |
| F12, swapped | `c0bc2dd` → `0575c7f` | `c0bc2dd` | the same 4, all M | none |
| F13, `Compare feature/compare with main` | `main` (`c0bc2dd`) → `feature/compare` (`245c395`), three-dot | the merge base `14a5c6c` Add tracing | (2) M README.md, A src/Comparer.cs | `2 commits only on feature/compare, 7 only on main` (`rev-list` prints `7⇥2`) |
| F13, the same with `All differences` | two-dot of the tips | `c0bc2dd` | (5) M README.md, M docs/notes.md, A src/Comparer.cs, M src/Config.cs, R src/Parser.cs from src/Reader.cs (git `R067`) | the same |
| F13, the drop's `Compare main with feature/compare` | `feature/compare` → `main`, three-dot | `14a5c6c` | (4) M README.md, M docs/notes.md, M src/Config.cs, R src/Reader.cs from src/Parser.cs (git `R067`) | `7 commits only on main, 2 only on feature/compare` (`2⇥7`) |
| F14, `Compare feature/alpha with main` ("filters", feature/alpha hidden) | `main` (`4c0aa3c`) → `feature/alpha` (`7c60fc4`), three-dot | the merge base `369f471` Add the core | (1) A src/alpha.txt | `1 commit only on feature/alpha, 3 only on main` (`3⇥1`) |

Two diffs the checks open: src/Reader.cs from `0575c7f` to `c0bc2dd` (db5a108 → 7061deb), one hunk
`@@ -10,13 +10,13 @@ public sealed class Reader`: context 10 `        _text = text ?? string.Empty;`,
11 `    }`, 12 (empty); removed 13 `    public string[] Words()`, added 13 `    public string[]
Lines()`; context 14 `    {`, 15 `        if (_text.Length == 0)`, 16 `        {`, 17 `            return
[];`, 18 `        }`, 19 (empty); removed 20 `        return _text.Split(' ');`, added 20 `        return
_text.Split('\n');`; context 21 `    }`, 22 `}`. README.md from the merge base `14a5c6c` to
`245c395` (30ac432 → f950130), one hunk `@@ -1,3 +1,5 @@`: context 1 `# History`, 2 (empty), 3 `A
repository for the history checks.`; added 4 (empty), 5 `Compare two texts with Comparer.`.

**"filters"** (`Scenarios.FiltersAsync`, folder and tab `filters`), as scenario facts 3 give it:
all by Test Author; HEAD on a clean `main`; `main` tracks `origin/main` (0, 0), `feature/beta`
`origin/feature/beta` (0, 0), `feature/alpha` `origin/feature/alpha` (behind 1); `feature/merged`
local only; `refs/remotes/origin/HEAD` → `origin/feature/beta`; tag `v1` (lightweight) on Add the
core; `stash@{0}` "On feature/alpha: Alpha experiment" (`d3f16e8`, made on Alpha one `7c60fc4`) and
`stash@{1}` "On feature/beta: Beta experiment" (`c8f3db3`, made on Beta six `89f891e`).

**FL0, "filters" as built** (scenario facts 3.1). 5 lanes, Graph column 96: at 1100×700 Branch /
Tag, Graph (390 to 486) and Message (486 to 700), no Date column. All dates 2026-01-01.

| Row | Labels (drawn) | Node: lane, colour | Message | Time | SHA | y |
|---|---|---|---|---|---|---|
| 0 | `stash@{0}` | 0, 0, ring | On feature/alpha: Alpha experiment | 12:14 | `d3f16e8` | 129 |
| 1 | `stash@{1}` | 1, 1, ring | On feature/beta: Beta experiment | 12:13 | `c8f3db3` | 155 |
| 2 | `feature/beta`, `origin/feature/beta` (drawn `feature/beta`, `+1`) | 1, 1 | Beta six | 12:12 | `89f891e` | 181 |
| 3 | | 1, 1 | Beta five | 12:11 | `ac357ab` | 207 |
| 4 | `origin/feature/alpha` | 2, 2 | Alpha two | 12:10 | `e4d9976` | 233 |
| 5 | | 1, 1 | Beta four | 12:09 | `1cdde63` | 259 |
| 6 | `main` ✓, `origin/main` (drawn `main` ✓, `+1`) | 3, 3, merge | Merge branch 'feature/merged' | 12:08 | `4c0aa3c` | 285 |
| 7 | | 3, 3 | Main work | 12:07 | `ef7bfda` | 311 |
| 8 | | 1, 1 | Beta three | 12:06 | `0c13077` | 337 |
| 9 | `feature/merged` | 4, 4 | Merged work | 12:05 | `629bc39` | 363 |
| 10 | | 1, 1 | Beta two | 12:04 | `8fc77ea` | 389 |
| 11 | `feature/alpha` | 0, 0 (lane 2 ends here) | Alpha one | 12:03 | `7c60fc4` | 415 |
| 12 | | 1, 1 | Beta one | 12:02 | `2f6e977` | 441 |
| 13 | `v1` | 0, 0 (lanes 1, 3 and 4 end here) | Add the core | 12:01 | `369f471` | 467 |
| 14 | | 0, 0 | Initial commit | 12:00 | `67e5696` | 493 |

Lines: lane 0 (colour 0) **dashed** from row 0's ring through rows 1 to 10 into row 11's node (Alpha
one), then solid from row 11 through row 12 into row 13 and on to row 14; lane 1 (colour 1)
**dashed** from row 1's ring into row 2's node (Beta six), then solid from row 2 through rows 3 to
12 into row 13's node; lane 2 (colour 2) from row 4's node through rows 5 to 10 into row 11's node;
lane 3 (colour 3) from row 6's node into row 7's node, then through rows 8 to 12 into row 13's
node; lane 4 (colour 4) from row 6's node (the merge's second parent) through rows 7 and 8 into row
9's node, then through rows 10 to 12 into row 13's node. Rows 1 to 10 hold no node in lane 0, so
the alpha stash's dashed line runs alone at x 406 from y 129 to y 415. Full SHAs: scenario facts 3.1
(stash `d3f16e894a9e233889e358ddb6bef18db6f42a35`, Alpha one
`7c60fc46a4178e4f2f88df0f73c4b51808be230a`, the merge `4c0aa3c354e1cda550d852d90b110d93df6b08cf`, Add
the core `369f471b9087f5d12eb03bd8f5ebee5d2ae6d9b8`). Its left panel as it opens: Local branches
`4` (folder `feature`, closed; `main` in the accent colour), Remotes `3` (`origin` › `feature`
closed, `main`), Pull requests `0`, Tags `1` (`v1`), Stashes `2`; with both `feature` folders
opened by a check:

```
▾ Local branches          4
    ▾ feature
        alpha            ↓1
        beta
        merged
      main                    (accent colour)
▾ Remotes                 3
    ▾ origin
        ▾ feature
            alpha
            beta
          main
▸ Pull requests           0
▾ Tags                    1
      v1
▾ Stashes                 2
      On feature/alpha: Alpha experiment
      On feature/beta: Beta experiment
```

**FL1, "Hide feature/alpha" (with its upstream `origin/feature/alpha`)** (scenario facts 3.2).
The revisions `--exclude=feature/alpha --branches --exclude=origin/feature/alpha
--exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --`. 12 rows, 3 lanes, Graph column
64: Message 454 to 580, Date 580 to 700. Alpha one, Alpha two and the alpha stash are left out (the
stash's base is hidden, 8.2); `stash@{1}` keeps its name.

| Row | Labels (drawn) | Node: lane, colour | Message | SHA | y |
|---|---|---|---|---|---|
| 0 | `stash@{1}` | 0, 0, ring | On feature/beta: Beta experiment | `c8f3db3` | 129 |
| 1 | `feature/beta`, `origin/feature/beta` (drawn `feature/beta`, `+1`) | 0, 0 | Beta six | `89f891e` | 155 |
| 2 | | 0, 0 | Beta five | `ac357ab` | 181 |
| 3 | | 0, 0 | Beta four | `1cdde63` | 207 |
| 4 | `main` ✓, `origin/main` (drawn `main` ✓, `+1`) | 1, 1, merge | Merge branch 'feature/merged' | `4c0aa3c` | 233 |
| 5 | | 1, 1 | Main work | `ef7bfda` | 259 |
| 6 | | 0, 0 | Beta three | `0c13077` | 285 |
| 7 | `feature/merged` | 2, 2 | Merged work | `629bc39` | 311 |
| 8 | | 0, 0 | Beta two | `8fc77ea` | 337 |
| 9 | | 0, 0 | Beta one | `2f6e977` | 363 |
| 10 | `v1` | 0, 0 (lanes 1 and 2 end here) | Add the core | `369f471` | 389 |
| 11 | | 0, 0 | Initial commit | `67e5696` | 415 |

Lines: lane 0 (colour 0) dashed from row 0's ring into row 1's node, then solid from row 1 to row
11; lane 1 (colour 1) from row 4's node into row 5's node, then through rows 6 to 9 into row 10's
node; lane 2 (colour 2) from row 4's node through rows 5 and 6 into row 7's node, then through rows
8 and 9 into row 10's node. Message clicks at (484, 129 + 26 r).

**FL2, "Solo feature/beta" (with `origin/feature/beta`)** (scenario facts 3.3). The revisions
`--ignore-missing refs/heads/feature/beta refs/remotes/origin/feature/beta HEAD --`. FL1's 12 rows,
lanes, colours, lines and positions; the labels differ: row 4 has `main` ✓ alone (`origin/main`'s
label left out, 8.2), row 7 (Merged work) has no label (`feature/merged`'s left out); rows 0, 1 and
10 as FL1 (`stash@{1}`; `feature/beta` with `+1`; `v1`, tags keep their labels). The alpha stash is
left out.

**GS0, phase 1's "graph" scenario** (`Scenarios.GraphAsync`), as phase 1's report gives it (13 rows,
3 lanes, Message 454 to 580, Date 580 to 700), now with its stash line dashed (measured in the app,
WP2a): row 0 `stash@{0}` "On main: Work in progress on README" (`95daa96`,
95daa96b4c242f0f653cadd7d0218a76679f0cf5, made on Add settings page `47e6ec7`), lane 0, colour 0,
ring, y 129; row 1 `feature/search` Highlight matches `f463b85` (lane 1, colour 1, y 155); row 2
`main` ✓ Add settings page `47e6ec7` (lane 0, colour 0, y 181); row 3 `origin/main` Fix typo in docs
(lane 2, colour 2, y 207); row 4 `v0.2` Bump version (lane 0, colour 0, y 233); rows 5 to 12 as
phase 1's table. Lane 0 (colour 0) is **dashed** from row 0's ring through row 1 into row 2's node
(row 0's OutOfNode, row 1's PassThrough and row 2's IntoNode lines), and solid from row 2's node
on; lane 1 (colour 1) runs solid from row 1's node through row 6's node into row 7's (the merge's)
node; every other line of rows 0 to 12 is solid, its lanes and colours as phase 1's table.

#### Texts these checks rely on (part F)

**The search bar** (design 3.1; the toolbar's Search, 1.1; counts in both forms, 12):

| Part | Text |
|---|---|
| The toolbar's Search (`SearchButton`) | enabled with a repository; tooltip `Search commits (Ctrl+F)` (`Cmd+F` on macOS); disabled reasons `Close the interactive rebase first`, `Close the conflict resolver first` (1.1, 12.4; part A asserts them) |
| Mode button (`SearchModeButton`) | `Commits`, `Files` or `Changes`, with `IconChevronDown` |
| Mode menu | header `Search` (FD5); `Commits: message, author or SHA`, `Files: a path or file name`, `Changes: text added or removed` |
| Box (`SearchBox`) placeholder | `Message, author or SHA`; `File name or path (case ignored for A–Z)`; `Text added or removed (case ignored for A–Z)` |
| Count (`SearchCount`), the first that holds | empty while the box is empty; `Searching…`; `{n} so far…` (`1 so far…`); `No matches`; `{k} of {n}` while the selected commit is the k-th match in row order; `{n} matches` (`1 match`) |
| Previous, next, close | tooltips `Previous match (Shift+Enter)`, `Next match (Enter)` (both disabled without matches), `Close the search (Esc)` |
| Progress (`SearchProgress`, Changes mode only) | help text and tooltip `Searched {k} of {n} commits`, `Searched {k} of 1 commit` for a graph of one commit (D113) |

**Compare** (design 6.2 to 6.4, 12.4, 13.1):

| Where | Text |
|---|---|
| A local branch's menu (not the current branch's), a remote branch's, a tag's | `Compare {b} with {cur}`, `Compare {r}/{b} with {cur}`, `Compare {t} with {cur}`; disabled `HEAD is unborn` |
| A drop's menu, a group of its own at the end | `Compare {A} with {B}` (From is B, To is A) |
| The view's header | `CompareTitle` `Compare`; `CompareSwap` (`IconArrowDown` and `IconArrowUp`, tooltip `Swap From and To`, FD10); `CompareClose` (a branch comparison only, tooltip `Close the comparison`, FD10) |
| From and To (`CompareFrom`, `CompareTo`) | two commits `From {short} {subject}`, `To {short} {subject}`; branches `From {name}`, `To {name}`; a link to a commit the graph lacks is disabled with `This commit is not in the graph` |
| Counts (`CompareCounts`, branches) | `{a} commits only on {To}, {b} only on {From}`, the singular `1 commit only on {To}, …`; the switch `All differences` (`CompareAllDifferences`) |
| The list | `Changed files ({N})` with Flat and Tree (no All files); `Loading…` until read; `No differences` |
| Three-dot's other cases (none shown by part F) | `No common history: showing all differences`; `Several merge bases: changes since {short}`; `Renames are shown as removed and added files.`; `Both are {short}: nothing to compare` |
| A file's diff from the list | phase 2's diff view with the side label `{old short}..{to short}` |
| A file's menu in the list (`ForCompareFile`, 6.4) | header the file name; `File history`, `Blame` (at To); —; `Open file`, `Show in folder`, `Copy path` (part B's texts; `Open file` disabled with `The file is not in the working tree` when it is not) |

**Hide and solo** (design 8.1, 8.3, 12.4, 13.1):

| Where | Text |
|---|---|
| A local or remote branch's menu | `Hide {b}` or `Show {b}`; `Solo {b}` or `Stop soloing {b}` (`{r}/{b}` for a remote branch); `Hide {b}` disabled on the checked-out branch: `The checked-out branch is always shown` |
| The Local branches and Remotes sections' menus | `Show all hidden branches` (disabled `No branch is hidden`), `Stop soloing` (disabled `No branch is soloed`); the Local branches section also gains part B's `Show reflog` and `Show backups` before them |
| A remote's menu (not shown by part F) | `Hide every branch of {r}` or `Show every branch of {r}` |
| The left panel's toggles | `RefHiddenToggle` (`IconEyeOff`) tooltip `Hidden in the graph. Click to show it`; `RefSoloToggle` (`IconSolo`, `VcAccentBrush`) tooltip `Soloed. Click to stop soloing it` |
| A hidden branch's row whose commit is no row | tooltip `{b} is hidden in the graph` |

**Earlier texts the checks show**: the details panel's `Select a commit to see its details.`; phase
3's `{N} commits selected` and the hint `Right-click a selected commit for what can be done with all
of them.`; the menu items of phases 3 and 4 as `OperationTexts.Menus` gives them (`Check out {b}`,
`Merge {b} into {cur}`, `… (create a merge commit)`, `Rebase {cur} onto {b}`, `Interactive rebase of
{cur} onto {b}…`, `Fast-forward {b} to {up}`, `Push {b} to {up}`, `Force push {b}…`, `Create branch
here…`, `Create tag here…`, `Rename {b}…`, `Delete {b}…`, `Change upstream…`, `Stop tracking {up}`,
`Copy branch name`, `Pull from {up}`, `Pull from {up} (rebase)`, `Create branch…`; the drop's
`{A} → {B}`, `Fast-forward {B} to {A}`, `Check out {B} and merge {A} into it`; the reasons `{b} is
up to date with {up}`, `{b} has commits that {other} does not have`, `You cannot delete the branch
you are on`); part B's `Show reflog of {b}` (5.1).

**Colours** (design 13.3, the token table): `VcSearchMatchBrush` dark #F2C94C, light #B98900;
`VcTextDisabledBrush` dark #5F6775; `VcChromeBackgroundBrush` dark #20242C, light #EBEDF1;
`VcBorderBrush` dark #323845, light #D2D6DE; the window background dark #14161B, light #FFFFFF; lane
colours dark 0 #7C8CFF, 1 #3FB97F, 2 #E0A23B, 3 #E5606B, 4 #4FB3D9, light 0 #4353D8. Dimmed (35 %
over the window background, FD6): dark colour 0 #383F6B, colour 1 #234F3E, colour 2 #5B4726; light
colour 0 #BDC3F1.

### Part U: undo and redo

This part covers checks U16 to U23 of design 15.4: undo and redo of every action the history
records (design 7.2), each undone and redone once in the app, with the questions for remote steps
(7.5, D155, D170) and the refusals (7.4, 7.6). It was written before the UI it tests, from
`docs/phase-5-design.md` 1.1, 7, 9.1, 12.1, 12.2 and 13.2, decisions D153 to D155, D163, D165 to
D172 and D181, the undo code WP0 merged (`VisualCommit.Core.Undo`: `UndoKind`, `UndoTexts`,
`UndoSequence`, `UndoScope`) and, for the command lines of each effect, WP3's git half (read on
its branch at `a7a312c`, merged into `phase/5-history` as `b0e8710` while this part was written,
with design 7.12 saying what it settled; where its command lines or their order differ from the
design's 7.3 and 7.5, the checks assert what the merged code runs and the difference is listed
under "Decisions this report makes", UD20). Every git
state below was derived with Git 2.36.0.windows.1 on copies of the scenario repositories built by
the real scenario code, whose SHAs equal those `ScenarioTests`, `Phase4ScenarioTests` and
`Phase5ScenarioTests` pin, each action and each undo and redo run with the git command lines the
app runs and the harness's environment ("How the expected git states were derived", part U).
The keys Ctrl+Z and Ctrl+Y are part A's (A4); the backups list's restores are part B's (B9); the
automatic stash's undo is part P's (P25).

#### Conventions of part U

- **One action per test method** (design 15.4, D181): each method writes a session with one tab on
  a fresh copy of the named scenario, made "outside the app" first where it says so, starts the
  app at 1100×700 in the dark theme (`Phase5Checks.OpenAsync`: graph drawn, status shown, queue
  idle, pointer at the neutral point), does one action with the app, then Undo, then Redo, and
  disposes the app; where a method does more (D75's case in `Check_U16_discard_file`, two
  actions undone and redone in a row in `Check_U16_many_steps`, the refusals of U23), its steps
  say so, and no method runs more than six operations. No method starts the app twice. The methods,
  in the `VisualCommit.VisualTests.Phase5Undo`
  project, by check:
  - U16 (`history`): `Check_U16_commit`, `Check_U16_amend`, `Check_U16_discard_file`,
    `Check_U16_discard_hunk`, `Check_U16_discard_lines`, `Check_U16_discard_all`,
    `Check_U16_restore_recorded`, `Check_U16_many_steps`.
  - U17 (`ops`): `Check_U17_soft_reset`, `Check_U17_mixed_reset`, `Check_U17_mixed_reset_with_staged_changes`,
    `Check_U17_hard_reset`.
  - U18: `Check_U18_create_branch`, `Check_U18_create_and_check_out`, `Check_U18_rename`,
    `Check_U18_delete_with_upstream`, `Check_U18_check_out_branch`, `Check_U18_tracking_branch`,
    `Check_U18_check_out_commit` (`ops`); `Check_U18_leave_detached_commit`,
    `Check_U18_fast_forward` (`edges`); `Check_U18_stash_and_check_out` (`blocked`).
  - U19: `Check_U19_merge_fast_forward`, `Check_U19_merge_commit`, `Check_U19_cherry_pick_two`,
    `Check_U19_revert` (`ops`); `Check_U19_rebase`, `Check_U19_check_out_and_rebase`,
    `Check_U19_check_out_and_merge` (`rebase`); `Check_U19_interactive_rebase`,
    `Check_U19_squash` (`interactive`); `Check_U19_pull_with_rebase` (`sync`).
  - U20 (`ops`): `Check_U20_stash`, `Check_U20_apply`, `Check_U20_pop`, `Check_U20_drop`,
    `Check_U20_create_annotated_tag`, `Check_U20_delete_tag`.
  - U21: `Check_U21_push_new_branch`, `Check_U21_force_push`, `Check_U21_delete_remote_branch`,
    `Check_U21_lease_fails` (`sync`); `Check_U21_push_tag`, `Check_U21_delete_remote_tag` (`ops`).
  - U22: `Check_U22_set_upstream`, `Check_U22_add_remote`, `Check_U22_rename_remote`,
    `Check_U22_change_url`, `Check_U22_remove_remote` (`sync`); `Check_U22_ignore` (`ops`).
  - U23: `Check_U23_terminal_commit`, `Check_U23_merge_in_progress`, `Check_U23_stale_stat_data`
    (`ops`); `Check_U23_changes_in_the_way` (`rebase`); `Check_U23_ignored_file_in_the_way` (`ops`).

  55 methods and about 170 operations; the run-time note is under "Decisions this report makes".
- **Outside the app** is a git call or a file write through `TempRepo`, as a terminal would make it,
  on the scenario's clock (the copy's next minute: `ops`, `interactive` 12:08, `rebase` 12:07,
  `sync` 12:05, `history` 12:14, `edges`, `blocked` 12:04). A step made outside while the app runs
  is followed by a wait on the condition it names (the graph shows a row, the working-changes row
  shows), with phase 5's `OutsideChangeTimeout` (30 s), never a fixed time.
- **Rows and points** are phase 4's: rows from 0 at the top; while the working-changes row shows
  it is display row 0 and commit row r is display row r + 1. With no banner and no search bar,
  display row d's vertical centre is y 129 + 26 d at 1100×700, and a row is clicked or
  right-clicked "in its Message column" 30 px into it: x 468 with 1 or 2 lanes (Message from 438),
  484 with 3 (454), 500 with 4 (470). While a banner shows, no y is fixed (D122): rows come from
  `ShellDriver.GraphRowPoint`. A label is pressed or right-clicked at its centre
  (`GraphLabelBounds`), only the first label of a row. Left-panel rows are found by their text
  (`RefRowAsync`, `RefItem` in UI Automation) and written as paths (Local branches › feature ›
  ff, Remotes › origin › published, Tags › v1.0, Stashes › On main: Experiment).
- **Menus, dialogs and the stage panel** are as phases 2 to 4 give them, found by label or
  automation id (`ShellDriver.Menu()`, `ClickMenuItem`; `NameDialog`, `NameBox`,
  `NameDialogOption`, `MessageBox`, `ResetDialog` with `ResetSoft`/`ResetMixed`/`ResetHard`,
  `ConfirmationDialog` with `ConfirmTitle`, `ConfirmQuestion`, `ConfirmDetail`, `CancelButton`,
  `ConfirmButton`; `ErrorDialog` with `ErrorTitle`, `ErrorSummary`, `ErrorRepository`,
  `ErrorOutput`, `ErrorCommand`, `ErrorCopyButton`, `ErrorExtraButton`; `PickDialog`,
  `PickFilter`; `PushDialog`; `RemotesDialog` with `AddRemoteButton`, `RemoteNameBox`,
  `RemoteUrlBox`, `RemoteFetchOption`, `RemoteEditButton`, `RemoteRemoveButton`; the stage
  panel's `StageAllButton`, `DiscardAllButton`, `RowDiscardButton`, `CommitSummary`,
  `SummaryCounter`, `CommitDescription`, `AmendCheckBox`, `CommitButton`, `RestoreBarText`,
  `RestoreDiscardButton`; the diff's `DiscardHunkButton` (the hunks' buttons in order),
  `DiscardLinesButton`). The checks of this part assert menus by the items they click only; the
  menus themselves are phase 3's, 4's and part F's subject. A dialog's text this part does not
  quote is the earlier phase's, unchanged.
- **The toolbar's Undo and Redo** (design 1.1, 13.4) are `UndoButton` and `RedoButton`; their
  tooltip, enabled or not, is also their `AutomationProperties.HelpText`. The scripted run reads
  each button's `IsEnabled` and `ToolTip.GetTip` through a new `ShellDriver.UndoRedoState()`
  (WP8, in `ShellDriver.Phase5.cs`: both buttons' enabled state and tooltip text), clicks them
  with new `ClickUndo()` and `ClickRedo()` (a click at the button's centre, then
  `WaitForIdleAsync`), and rests the pointer on one with phase 3's `RestPointerOnAsync`; the real
  window clicks them by automation id and reads `HelpText` (`RealApp.Phase5*.cs`). The state is
  written in three forms, the text being the entry's (7.6):
  - **T0**: Undo disabled, tooltip `Nothing to undo`; Redo disabled, `Nothing to redo`;
  - **T+ x**: Undo enabled, `Undo x`; Redo disabled, `Nothing to redo`;
  - **T− x**: Undo disabled, `Nothing to undo`; Redo enabled, `Redo x`.
  A state with an older entry before the cursor is written out in full. Every method starts at
  T0 (the session's history is empty, nothing has run), which it asserts before its action.
- **A step ran** ("Undo ran", "Redo ran"): one operation of the queue titled `Undo x` or `Redo x`
  (running text `Undoing x…` / `Redoing x…`, not pictured), no question unless the step changes
  a remote, no dialog after it, the queue's log line `<repo>: Undo x: Succeeded after <N> ms:
  Undid x` (or `Redo x: … Redid x`), and the status bar `Undid x` / `Redid x` in the secondary
  colour (12.2). The scripted run waits for that line and then until idle; the real window for the
  same line (`RealApp.OperationEnded`). A refused or failed step: the line's outcome `Failed`,
  the error dialog of the check, and the status bar `Undo x failed` / `Redo x failed` in
  `VcDangerBrush` (phase 3's "<Title> failed").
- **What a step leaves in git** is asserted after the step, on every git version, with
  `git for-each-ref --format=%(refname) %(objectname)` (the refs the check names), `git status
  --porcelain=v1 -b --untracked-files=all`, `git stash list --format=%gd %H %gs`, the files' bytes,
  `git config --local --get-regexp` of the keys the check names, `git ls-remote origin` for a
  remote, and the newest entries of HEAD's and the branch's reflog (`git log -g -n <k>
  --format=%gd %h %gs`). From U23's step 23 to its step 24 these reads, and every other git read
  that check makes, run with `GIT_OPTIONAL_LOCKS=0` (`TempRepo.GitWithEnvironmentAsync`), as the
  app's own reads do (`IGitRepository`): a plain `git status` refreshes stale stat data and writes
  the index, which would take away what U23 part E tests (UD15). The call log (`IGitCallLog`)
  holds the effects' commands; "the call log has X" means that argument list, with full SHAs where
  the report writes `c0bc2dd…` (an `update-ref -m` message is one argument, written unquoted:
  `-m VisualCommit: undo x`; the lines of an `update-ref --stdin` transaction are its recorded
  input, design 2.3). A step's ref writes (a soft move's, a branch's, a tag's) are lines of its one
  `update-ref -m <msg> --stdin` transaction, written at the step's end unless a later effect needs
  one of its refs first; every other effect runs at its place in the sequence (a remote's first,
  7.5 step 4), except configuration and `.gitignore` writes, which follow the transaction (UD20);
  a Keep move writes its own positional `update-ref`. Every step also runs the history's reads
  before and after (7.7: `rev-parse`, `for-each-ref`, the status), which no check counts.
  `update-ref` writes the reflog message `VisualCommit: undo x` / `VisualCommit: redo x` (7.3) on
  the ref it moves, and on HEAD's reflog too when the ref is the branch HEAD is on (a
  transaction's line as the positional form, 2.36 and 2.55); a `switch` writes git's own
  `checkout: moving from A to B`.
- **Backups.** The harness's backup clock (design 2.3) names the first backup of a session
  `<kind>-20260102T090001000Z` and each later one a second after the last, so part U asserts
  backup names exactly (`git for-each-ref refs/visualcommit/backup`, ref and target). No method
  makes more than four backups, and the scenarios of part U hold none at the start, so the
  clean-up removes nothing and no `Removed backup` note appears. Undo and redo never delete a
  backup. Snapshot commits (`discard-…`, `reset-…-changes`) are asserted by their parents, subject
  and content, and the ids given here only as what plain git gives (they are fixed by D72's
  dates).
- **Commits the app makes** have "Test Author" <author@example.com> as committer and the commit
  date 2026-01-02 09:00 UTC (D72); an amend, a pick and a rebase keep the author and author date.
  An undo or redo moves refs to commits that exist and makes none, except a redone stash or hard
  reset (which, with the fixed dates, gives the same objects again: "Commits the checks make").
- **The working-changes row and the selection.** An action, undo or redo keeps the graph's
  selection on its commit by SHA wherever its row moves (phase 3's 4h); a selected commit that
  leaves the graph leaves nothing selected and the details panel says "Select a commit to see its
  details.". A step that needs the stage panel clicks the working-changes row first.
- **Questions** (7.5, 13.2): phase 3's confirmation card, `ConfirmTitle` and `ConfirmQuestion` as
  7.5's table gives them, no `ConfirmDetail`, `CancelButton` "Cancel" with the keyboard focus and
  `ConfirmButton` "Undo" or "Redo" in the primary style (`VcPrimaryButton`), not the danger style:
  they restore. The step's operation starts only after the click; Cancel changes nothing.
- **Pictures** are named `u<check><letter>-<what>` (phase 5's `PictureName()`), letters running on
  through a check's methods in order. Before a capture both harnesses move the pointer to the
  neutral point (760, 687), unless the step rests it on something (then it stays there); a
  capture with a dialog open keeps the dialog's pointer rule (phase 3's).
- **Git's own texts** quoted word for word (`ErrorOutput`, the call log's push lines) are Git
  2.36.0.windows.1's and are asserted word for word only when `git --version` is 2.36; on another
  git a check asserts the line marked "key" and that `ErrorOutput` is `GitOutputCleaner.Clean` of
  the failed call's own output (phase 3's rule). The texts part U quotes were also replayed with
  Git 2.55.0.windows.5 and are the same there. A remote's folder path is in git's `To` and
  `error:` lines, so a picture showing them varies by nature in the folder's random name.

#### Scenarios and graphs of part U

Every scenario is its earlier report's, as pinned; this section gives the states part U starts
from and reaches. Lanes, colours, labels and lines were laid out by the app's own `GraphLayout`
and `CommitGraphData` (merged WP2a, with stashes' dashed lines) on the derived repositories.
Labels are listed in phase 1's order; phase 1's "+N" rule decides how many a 130-wide Branch / Tag
column draws. "Dashed" marks a line with `GraphLine.IsDashed` (design 10). Dates are author
dates in UTC.

**"history"** (`Scenarios.HistoryAsync`, scenario facts 2): H0 with its working-changes row (display
row 0, ring in lane 0, colour 0, dashed line to row 0's node); `## main...origin/main`, ` M
src/Reader.cs` (two hunks: `@@ -1,6 +1,6 @@` line 3 "Reads the words" → "Reads the lines";
`@@ -19,4 +19,6 @@` lines 22 (empty) and 23 `    public int Length => _text.Length;` added);
blobs: committed `7061deb`, working `3056624`. 2 lanes, Message from x 438.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓, `origin/main` | 0, 0 | Read whole lines | 2026-01-01 12:13 | `c0bc2dd` |
| 1 | | 0, 0 | Stop tracing | 12:12 | `20d7fbe` |
| 2 | | 0, 0 | Write new notes | 12:11 | `e765529` |
| 3 | | 0, 0 | Merge branch 'feature/readme' | 12:10 | `f513566` |
| 4 | | 1, 1 | Explain the reader | 12:09 | `c1b6375` |
| 5 | | 0, 0 (lane 1 ends here) | Remove the notes | 12:08 | `884b462` |
| 6 | `feature/compare`, `origin/feature/compare` | 1, 2 | Compare whole lines | 12:07 | `245c395` |
| 7 | | 0, 0 | Rename the parser to reader | 12:06 | `0575c7f` |
| 8 | | 1, 2 | Add the comparer | 12:05 | `d0d3c77` |
| 9 | | 0, 0 (lane 1 ends here) | Add tracing | 12:04 | `14a5c6c` |
| 10 to 13 | | 0, 0 | Handle empty input, Add the logo, Add the parser, Initial commit | 12:03 to 12:00 | `365482b`, `775113f`, `1d5a7cf`, `7829335` |

**H-U1, after U16's commit** (no working-changes row; 15 rows, 2 lanes): row 0 `main` ✓ "Describe
the reader and add Length", 2026-01-02 09:00, `8d845e8`, lane 0, colour 0; row 1 `origin/main`
"Read whole lines" `c0bc2dd` (0, 0); rows 2 to 14 are H0's rows 1 to 13 with H0's lanes and
colours. Left panel: `main` ↑1.

**H-U2, after U16's amend** (15 rows, 2 lanes): row 0 `main` ✓ "Read whole lines" `bd74e66`
(0, 0, author date 12:13); row 1 `origin/main` "Read whole lines" `c0bc2dd` (1, 1); row 2 "Stop
tracing" (0, 0; lane 1 ends here: both "Read whole lines" have it as parent); rows 3 to 14 are H0's
rows 2 to 13 with the side branches' colours one higher (Explain the reader 1, 2; Compare whole
lines and Add the comparer 1, 3). Left panel: `main` ↑1 ↓1.

**"ops"** (`Scenarios.OperationsAsync`), phase 3's G0, with the stash's line dashed (design 10,
14.1): 3 lanes, Message from x 454.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `stash@{0}` | 0, 0, ring | On main: Experiment | 2026-01-01 12:07 | `0e95aca` |
| 1 | `feature/conflict` | 1, 1 | Conflicting change | 12:06 | `078ef05` |
| 2 | `feature/ff` | 2, 2 | Fast-forward me | 12:05 | `d93814d` |
| 3 | `main` ✓, `origin/main`, `v1.1` | 0, 0 (lane 2 ends here) | Main work | 12:04 | `88d6b91` |
| 4 | `feature/alpha`, `origin/feature/alpha` | 2, 3 | Alpha two | 12:03 | `ce5ea6a` |
| 5 | | 2, 3 | Alpha one | 12:02 | `dff8fbf` |
| 6 | `v1.0` | 0, 0 (lanes 1 and 2 end here) | Add core | 12:01 | `8beca34` |
| 7 | | 0, 0 | Initial commit | 12:00 | `4ef1f7c` |

Lines: lane 0 (colour 0) **dashed** from row 0's ring through rows 1 and 2 into row 3's node,
solid from row 3 to row 7; lane 1 (colour 1) from row 1's node into row 6's node; lane 2 (colour
2) from row 2's node into row 3's node; lane 2 (colour 3) from row 4's node through row 5 into row
6's node. Left panel: Local branches 4 (▸ `feature`: `alpha`, `conflict`, `ff`; `main` accent),
Remotes 2 (`origin` › ▸ `feature`, `main`), Pull requests 0, Tags 2, Stashes 1. `## main...origin/main`.
Graphs derived from G0:

- **G0+w**: G0 with the working-changes row above it (display row 0, ring in lane 0, colour 0,
  its dashed line down lane 0 to "Main work"'s node, display row 4, under the stash's ring and
  dashed line).
- **G1** (`main` fast-forwarded to Fast-forward me): G0 with `main` ✓ and `feature/ff` on row 2,
  and `origin/main`, `v1.1` on row 3; lanes, colours and lines as G0 (the stash's dashed line
  still ends in row 3's node, its base). `main` ↑1.
- **G2** (the merge commit `22a08ae`), phase 3's table, 4 lanes (Message from 470, no Date
  column): 0 `main` ✓ Merge branch 'feature/ff' (0, 0, merge node); 1 `stash@{0}` (2, 2, ring);
  2 `feature/conflict` (3, 3); 3 `feature/ff` (1, 1); 4 `origin/main`, `v1.1` Main work (0, 0);
  5 `feature/alpha`, `origin/feature/alpha` (1, 4); 6 Alpha one (1, 4); 7 `v1.0` (0, 0); 8 Initial
  commit. The stash's line, lane 2, is dashed from row 1's ring into row 4's node. `main` ↑2.
- **G6** (`main` reset to Add core, changes in the working tree): the working-changes row, then
  G0's rows one lower, `origin/main` and `v1.1` on Main work (display row 4), `main` ✓ and `v1.0`
  on Add core (display row 7); the ring in lane 0, colour 0, its dashed line down lane 0 to Add
  core's node. `main` ↓1.
- **G8** (two picks, `c0f718d` and `d1f9fb5`), 4 lanes: 0 `main` ✓ Alpha two (0, 0); 1 Alpha one
  (0, 0); 2 `stash@{0}` (1, 1, ring, dashed into row 5's node); 3 `feature/conflict` (2, 2); 4
  `feature/ff` (3, 3); 5 `origin/main`, `v1.1` Main work (0, 0); 6 `feature/alpha`,
  `origin/feature/alpha` (1, 4); 7 Alpha one (1, 4); 8 `v1.0` (0, 0); 9 Initial commit. `main` ↑2.
- **G9** (the revert `13c773d`), 4 lanes: 0 `main` ✓ Revert "Main work" (0, 0); 1 `stash@{0}` (1,
  1, ring, dashed through rows 2 and 3 into row 4's node); 2 `feature/conflict` (2, 2); 3
  `feature/ff` (3, 3); 4 `origin/main`, `v1.1` Main work (0, 0); 5 `feature/alpha`,
  `origin/feature/alpha` Alpha two (1, 4); 6 Alpha one (1, 4); 7 `v1.0` Add core (0, 0); 8 Initial
  commit. `main` ↑1.
- **G0-s** (no stash, after a pop or a drop), 3 lanes, 7 rows: 0 `feature/conflict` (0, 0); 1
  `feature/ff` (1, 1); 2 `main` ✓, `origin/main`, `v1.1` Main work (1, 1); 3 `feature/alpha`,
  `origin/feature/alpha` (2, 2); 4 Alpha one (2, 2); 5 `v1.0` Add core (0, 0; lanes 1 and 2 end
  here); 6 Initial commit. No dashed line.
- **G0-S** (two stashes, phase 3's check 15 step 3), 4 lanes: 0 `stash@{0}` On main: Save my work
  (0, 0, ring, `b80f1ca`, 2026-01-02 09:00); 1 `stash@{1}` On main: Experiment (1, 1, ring); 2
  `feature/conflict` (2, 2); 3 `feature/ff` (3, 3); 4 `main` ✓, `origin/main`, `v1.1` (0, 0); 5
  `feature/alpha`, `origin/feature/alpha` (1, 4); 6 Alpha one (1, 4); 7 `v1.0` (0, 0); 8 Initial
  commit. Lanes 0 and 1 dashed from the rings into row 4's node.
- **G0-T** (`ops` with "Terminal work" committed outside on top of G1): 9 rows, 3 lanes: 0 `main` ✓
  Terminal work 12:08 `c7d8c71` (0, 0); 1 `stash@{0}` (1, 1, ring, dashed through rows 2 and 3
  into row 4's node); 2 `feature/conflict` (2, 2); 3 `feature/ff` Fast-forward me (0, 0); 4
  `origin/main`, `v1.1` Main work (0, 0); 5 `feature/alpha`, `origin/feature/alpha` (1, 3); 6
  Alpha one (1, 3); 7 `v1.0` (0, 0); 8 Initial commit. `main` ↑2.
- Label-only changes of G0 (rows, lanes and lines as G0): **G0 hotfix** (`main` ✓, `hotfix`,
  `origin/main`, `v1.1` on row 3), **G0 hotfix✓** (`hotfix` ✓, `main`, `origin/main`, `v1.1`),
  **G0 conflicting** (`feature/conflicting` on row 1), **G0 −alpha** (row 4 `origin/feature/alpha`
  only; Local branches 3), **G0 ff✓** (`feature/ff` ✓ on row 2; `main` on row 3 without ✓),
  **G0 alpha✓** (`feature/alpha` ✓, `origin/feature/alpha` on row 4; `main` without ✓), **G0 HEAD
  at Add core** (the filled `HEAD` label first on row 6, then `v1.0`; `main` without ✓),
  **G0 v1.2** (row 4 `feature/alpha`, `origin/feature/alpha`, `v1.2`), **G0 −v1.0** (row 6 no label).

**"edges"** (`Scenarios.EdgeCasesAsync`), phase 3's E0 and E1. **E0**: 2 lanes: 0 the filled `HEAD`
Detached work `419190f` (0, 0); 1 `feature/alpha` (branch) Alpha work `a024e50` (1, 1); 2 `main`,
`origin/main` (remote), `feature/alpha` (tag) Main work `8e16408` (0, 0; lane 1 ends here); 3 `loc`,
`origin/main` (local branch), `side` Initial commit `691f8cf` (0, 0). `CurrentBranch` "Detached at
419190f"; `side` checked out in `edges-wt`; `loc` ↓1. **E1** (on `main`): 1 lane: 0
`feature/alpha` Alpha work; 1 `main` ✓, `origin/main`, `feature/alpha` (tag) Main work; 2 `loc`,
`origin/main`, `side` Initial commit. **E0 loc** (`loc` fast-forwarded): E0 with `loc`, `main`,
`origin/main` (remote), `feature/alpha` (tag) on row 2 and `origin/main` (local), `side` on row
3; `loc` without arrows.

**"blocked"** (`Scenarios.CheckoutBlockedAsync`), phase 3's G14a: the working-changes row, then 0
`feature/addfile` Add newfile (0, 0); 1 `feature/clash` (1, 1); 2 `feature/other` Other change (0,
0); 3 `main` ✓ Add notes (0, 0; lane 1 ends here); ` M notes.txt` (line 1 "one (local)"), `??
newfile.txt` ("mine"). **G14b** (on `feature/other`, phase 3's check 14 step 5): the same rows with
`feature/other` ✓ and `main` without ✓; the ring in lane 0, its dashed line to "Other change"'s
node; `notes.txt` "one (local)" … "seven (other)".

**"rebase"** (`Scenarios.RebaseAsync`), phase 4's R0 (HEAD on `feature/topic`), R1 (after "Rebase
feature/topic onto main": 0 `feature/topic` ✓ Topic two `5f71ab8`; 1 Topic one `befae26`; 2
`feature/other` (1, 1); 3 `main`, `origin/main`; 4 Main work; 5 `origin/feature/topic` Topic two
`b0dfa3f` (2, 2); 6 Topic one (2, 2); 7 Add core; 8 Initial commit; `topic` ↑4 ↓2) and R3 (after
"Check out feature/other and rebase it onto main": 2 lanes, 0 `feature/other` ✓ Other one
`14e607b`; 1 `main`, `origin/main`; 2 Main work; 3 `feature/topic`, `origin/feature/topic` (1, 1);
4 Topic one (1, 1); 5 Add core; 6 Initial commit). **R4**, after "Check out main and merge
feature/other into it" (3 lanes): 0 `main` ✓ Merge branch 'feature/other' 2026-01-02 09:00
`5e5d815` (0, 0, merge node; its second line to lane 1); 1 `feature/other` Other one (1, 1); 2
`origin/main` Main more (0, 0); 3 Main work (0, 0); 4 `feature/topic`, `origin/feature/topic`
Topic two (2, 2); 5 Topic one (2, 2); 6 Add core (0, 0; lanes 1 and 2 end here); 7 Initial commit.
`main` ↑2.

**"interactive"** (`Scenarios.InteractiveAsync`), phase 4's G-I (one lane: Five `57f089d`, Four
`eef5c15`, Three `ff42e7f`, Fix one `ed58902`, Two `938844a`, One `fc6c8fd`, Base `4b4622c`,
Initial commit `da32da3`; `main` ✓ on Five) and G-B8 (Five `eb6f702`, Three `f34248d`, Fix one,
Two, One, Base, Initial commit). **G-U19**, after U19's interactive rebase (one lane, 7 rows): 0
`main` ✓ Four 12:06 `4ad730d`; 1 Three 12:05 `ad3ce4e`; 2 Fix one 12:04 `4e61f14`; 3 Two reworded
12:03 `97e6de9`; 4 One 12:02 `fc6c8fd`; 5 Base; 6 Initial commit.

**"sync"** (`Scenarios.RemoteSyncAsync`), phase 3's (2 lanes): 0 `topic` Topic work `77d782e` (0,
0); 1 `published` Unpushed work `8e65315` (1, 1); 2 `origin/published` Published work `8c6f545`
(1, 1); 3 `main` ✓, `origin/main` Shared work `a7f2cfd` (0, 0; lane 1 ends here); 4 Initial commit
`6688774`. Left panel: `main` (accent), `published` ↑1, `topic`; Remotes 2 (`origin` (open) with
`main`, `published`). `## main...origin/main`. States:

- **S1, S2** (phase 4's A4 part A): S1 with "Elsewhere work" `b2e9a9e` on `origin.git`'s `main` and
  "Local work" `f4f8c06` on `main`, before the fetch; S2 after "Pull from origin/main (rebase)":
  0 `main` ✓ Local work `8a1f07b` (0, 0); 1 `origin/main` Elsewhere work (0, 0); 2 `topic` (1, 1);
  3 `published` (2, 2); 4 `origin/published` (2, 2); 5 Shared work; 6 Initial commit; `main` ↑1.
- **S2u** (S2's pull undone, `origin/main` still fetched): 4 lanes (Message from 470, no Date
  column): 0 `main` ✓ Local work `f4f8c06` (0, 0); 1 `origin/main` Elsewhere work (1, 1); 2 `topic`
  (2, 2); 3 `published` (3, 3); 4 `origin/published` (3, 3); 5 Shared work (0, 0; lanes 1, 2 and
  3 end here); 6 Initial commit. `main` ↑1 ↓1.
- **S-topic** (topic pushed): the sync graph with `topic`, `origin/topic` on row 0; Remotes 3.
- **S-F0** (phase 3's check 20 step 1, "Rewritten work" `4f6a372` on `published`, 3 lanes): 0
  `published` Rewritten work 12:05 (0, 0); 1 `topic` (1, 1); 2 `origin/published` Published work
  (2, 2); 3 `main` ✓, `origin/main` Shared work (0, 0); 4 Initial commit. `published` ↑1 ↓1.
  **S-F1** (after the force push, 2 lanes): 0 `published`, `origin/published` Rewritten work; 1
  `topic` (1, 1); 2 `main` ✓, `origin/main`; 3 Initial commit. `published` no arrows.
- **S-del** (published deleted on origin): the sync graph without `origin/published` (row 2 no
  label); Remotes 1 (`origin` with `main`); `published` shows no mark (its upstream is gone).
- **S-pushed** (`published` pushed): row 1 `published`, `origin/published`; row 2 no label;
  `published` no arrows.
- **S-up** (the remote renamed `upstream`): labels `upstream/published`, `upstream/main`; Remotes
  2 (`upstream` (open) with `main`, `published`). **S-none** (the remote removed): row 2 and row 3
  without remote labels (`main` ✓ only on row 3); Remotes 0; no arrows anywhere.

**Commits and objects the checks make.** Plain git, run as the app runs it with D72's dates, makes
these; the checks expect them.

| Made in | Object | What | Parent(s) | Dates (author / commit) |
|---|---|---|---|---|
| U16 commit | `8d845e8` (8d845e81333e9fd0e441c4d1813368bfc68aa722) | "Describe the reader and add Length", blank, "The summary says lines now." | `c0bc2dd` | 2026-01-02 09:00 / 09:00, Test Author |
| U16 amend | `bd74e66` (bd74e66213b5ef2b87762cf21cd6ecff5321ff75) | "Read whole lines" (tree `5c95587`, as `8d845e8`'s) | `20d7fbe` | 2026-01-01 12:13 (Jonas Berg) / 2026-01-02 09:00 (Test Author) |
| U19 merge commit | `22a08ae` (22a08ae80b78afd6ee714929be263ada7ad53abd) | "Merge branch 'feature/ff'" | `88d6b91`, `d93814d` | 09:00 / 09:00 |
| U19 picks | `d1f9fb5` (d1f9fb5fa4728b88c1d36136f0f65fb8456ac586), `c0f718d` (c0f718d25dff3ea0ef8d67ada2d3411c9ab45b94) | "Alpha one", "Alpha two" | `88d6b91`; `d1f9fb5` | 12:02, 12:03 / 09:00 |
| U19 revert | `13c773d` (13c773dd19dbe8f7d2261eb3a2d7c2392670cd22) | Revert "Main work" | `88d6b91` | 09:00 / 09:00 |
| U19 rebase | `befae26` (befae2677a3a259f2c9423912af47575e319c852), `5f71ab8` (5f71ab8bad51a15fdfdfd19fc1b0084ef6e956c5) | Topic one, Topic two | `1dd58b8`; `befae26` | 12:02, 12:03 / 09:00 |
| U19 check out and rebase | `14e607b` (14e607bf248c8917ab93680c48cba392f60546ff) | Other one | `1dd58b8` | 12:06 / 09:00 |
| U19 check out and merge | `5e5d815` (5e5d815290fed817a30eb0225e708a088a714a5c) | "Merge branch 'feature/other'" | `1dd58b8`, `2d4d79a` | 09:00 / 09:00 |
| U19 interactive | `97e6de9` (97e6de90635da55e64fe6984d4e2c5af607c5646), `4e61f14` (4e61f14e45d3cd2f9ca088ce6dde78fa791e0cef), `ad3ce4e` (ad3ce4ed230d6994185c37167bbc61009aa84be7), `4ad730d` (4ad730da7dcb81136e051ae3e0ee8d80bc5d368d) | "Two reworded", Fix one, Three (with its body), Four; One `fc6c8fd` kept (git fast-forwards it), Five dropped | `fc6c8fd`; `97e6de9`; `4e61f14`; `ad3ce4e` | 12:03 to 12:06 / 09:00 |
| U19 squash | `f34248d` (f34248d062c44eadf27e78a47435b55161bf75d7), `eb6f702` (eb6f702889d08c7879c1daba704553fea072a42c) | phase 4's B8 | `ed58902`; `f34248d` | phase 4's |
| U19 pull, before the app starts | `b2e9a9e` (pinned), `f4f8c06` (f4f8c060b1e68a3e560113b942f1f984c6b5d4f0) | Elsewhere work (origin), Local work | `a7f2cfd` | 12:05, 12:06 |
| U19 pull | `8a1f07b` (8a1f07b0ca2c177d76be50610feaafb872a34863) | Local work | `b2e9a9e` | 12:06 / 09:00 |
| U20 stash, and its redo | `b80f1ca` (b80f1ca8abb5f1eadcd6c56b76e1b13034251dd5) | stash "On main: Save my work" | `88d6b91`, index `a5da893`, untracked `f2136f2` | 09:00 |
| U20 tag | tag object `bbc27cb` (bbc27cb2e708f4ae0fa2559b4c2205f10898f4cb) | `v1.2`, "#42 hotfix", tagger Test Author 09:00 | object `ce5ea6a` | |
| U21 force push, before the app starts | `4f6a372` (4f6a37288fadb5c453061cefedee620a9f98121e) | Rewritten work (src/published.txt "rewritten\n") | `a7f2cfd` | 12:05 |
| U21 lease, outside | `c16ddfa` (c16ddfa18af735846643ca9f16750d778b536fe0) | Published elsewhere (src/published.txt "published\nunpushed\nelsewhere\n"), on `origin.git` only | `8e65315` | 12:05 |
| U23 terminal commit, outside | `c7d8c71` (c7d8c711db8fe72237a33847de1e3961895c0b4a) | Terminal work (src/terminal.txt "terminal\n") | `d93814d` | 12:08 |
| U18 D91, transient | stashes `1bd29db` (the action's), `6cfd0e0` "WIP on feature/other: 850a7c0 Other change" (the undo's), `18df809` "WIP on main: 106efba Add notes" (the redo's) | each popped at once | | 09:00 |

Full SHAs of the scenarios used in commands: `c0bc2dd` c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9;
`88d6b91` 88d6b91e821a43cd39c94aa7892c8095b628caf8; `8beca34`
8beca345049bf7e8bab3a7b4f1427eff941b87e3; `d93814d` d93814d4ea6e21d1a70dd93066dbb23512c03b56;
`ce5ea6a` ce5ea6ab16759bede02ce706dd5c6865443c2051; `dff8fbf`
dff8fbf3d84b0feb999ef8b3ea0e22c20d898be4; `0e95aca` 0e95aca13f7975ef4a1fe029a8b5e9497f31028d;
`v1.0`'s tag object c8e997667b6705e41b3450489f8e0dda2da5c069; `b0dfa3f`
b0dfa3f405dffff59bea3f4f434b16e5490cc597; `1dd58b8` 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146;
`2d4d79a` 2d4d79a03112c8a5e2f9a783318d588df519af5d; `57f089d`
57f089d442eea23fee65c387f560002ab1ccd1fc; `a7f2cfd` a7f2cfd9a076c4c746e13caaa60f4fa2f0d76a4c;
`8c6f545` 8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6; `8e65315`
8e653159ca5e460a187117d51b3f18bdf81f5c96; `77d782e` 77d782ebfce02f1fe2b29c2ee2b6b43a1f682a02;
`419190f` 419190f40391bb6b4e317c793b9dd6c9939af6a0; `8e16408`
8e164080822465a9cb64c0278d3b30598acbeabf; `691f8cf` 691f8cfa93e845f8ab8993d2595d227e967caff5;
`850a7c0` 850a7c0db4553015af300aeed2cd7d0c6a095912; `106efba` 106efba4a731d81895cbb9db6f3ef02453078d9a.

#### Texts these checks rely on (part U)

**The toolbar** (design 1.1, 7.4): Undo enabled `Undo {text}`, Redo enabled `Redo {text}`; the
disabled reasons, the first that holds: `Wait for {title} to finish`; `Finish or abort the {kind}
first` (D119's kinds: `merge`, `cherry-pick`, `revert`, `rebase`); `Resolve the conflicts first`;
`Close the interactive rebase first`; `Nothing to undo` / `Nothing to redo`; the entry's check
without its prefix, e.g. `main has moved since (it is at 9f8e7d6)`. A disabled Undo or Redo makes
its key do nothing (1.3).

**Entry texts** (7.6; `UndoTexts`, a file by its name without its folder):

| Action | Entry text in part U |
|---|---|
| Commit, amend | `commit "Describe the reader and add Length"`, `amend "Read whole lines"` |
| Discard | `discard of Reader.cs`; `discard of lines in Reader.cs` (a hunk and lines alike); `discard of 2 files` |
| The restore bar's Restore, recorded | `restore of the discarded files` |
| Reset | `reset of main to 8beca34 (soft)` / `(mixed)` / `(hard)` |
| Branches | `creation of hotfix`, `creation of keep`, `rename of feature/conflict to feature/conflicting`, `deletion of feature/alpha`, `fast-forward of loc` |
| Checkouts (every kind) | `checkout of feature/ff`, `checkout of origin/feature/alpha`, `checkout of 8beca34`, `checkout of main`, `checkout of feature/other` |
| History | `merge of feature/ff into main`, `merge of feature/other into main`, `cherry-pick of 2 commits`, `revert of 88d6b91`, `rebase of feature/topic onto main`, `rebase of feature/other onto main`, `interactive rebase of main`, `squash of 2 commits`, `pull into main` |
| Stashes, tags | `stash "On main: Save my work"`, `apply of stash "On main: Experiment"`, `pop of stash "On main: Experiment"`, `drop of stash "On main: Experiment"`, `creation of tag v1.2`, `deletion of tag v1.0` |
| Remotes | `push of topic to origin`, `push of published to origin`, `force push of published to origin`, `deletion of published on origin`, `push of tag v1.1 to origin`, `deletion of tag v1.1 on origin` |
| Configuration | `upstream change of topic`, `addition of remote mirror`, `rename of remote origin to upstream`, `URL change of origin`, `removal of remote origin`, `ignore of /logs/run.log` |

A stash's `{message}` is the stash list's `%gs` (`On main: Experiment`), never `stash@{n}`.

**Operations and results** (12.1, 12.2, 7.5): title `Undo {text}` / `Redo {text}`; running text
`Undoing {text}…` / `Redoing {text}…`; error title `Undo failed` / `Redo failed`; failure summary
`Git could not undo {text}.` / `Git could not redo {text}.`, or the check's reason prefixed `Can't
undo {text}: ` / `Can't redo {text}: `; status `Undid {text}` / `Redid {text}`; an undone pop or
drop brings its stash back as `stash@{0}`. Undoing a commit or an amend fills the stage panel's
summary and description with the undone commit's message when both are empty; redoing it clears
them when they still hold that message (7.5). The restore bar (7.9): Undo of its discard closes
it, Redo of it shows it again with the same snapshot; its Restore runs as that entry's Undo when
the discard is the entry before the cursor, and is otherwise recorded as `restore of the discarded
files`.

**Questions** (7.5; confirm `Undo` or `Redo`; the tag forms are 7.5's "same four for a tag", the
word `tag` before the name):

| Step | `ConfirmTitle` | `ConfirmQuestion` |
|---|---|---|
| Undo the push that created `topic` | `Delete topic on origin?` | `Undoing the push deletes topic on origin. Anyone who fetched it keeps it.` |
| Redo it | `Push topic to origin again?` | `origin gets topic back at 77d782e.` |
| Undo a push that updated `published` (the force push, the lease check) | `Undo the push to origin?` | `published on origin goes back to 8c6f545, which it was before the push. Anyone who fetched 4f6a372 keeps it.` (the force push); `… goes back to 8c6f545, … Anyone who fetched 8e65315 keeps it.` (the lease check) |
| Redo the force push | `Redo the push to origin?` | `published on origin moves to 4f6a372 again.` |
| Undo the deletion of `published` on origin | `Undo the deletion of published on origin?` | `origin gets published back at 8c6f545.` |
| Redo it | `Delete published on origin again?` | `published is deleted on origin again.` |
| Undo the push of tag `v1.1` | `Delete tag v1.1 on origin?` | `Undoing the push deletes tag v1.1 on origin. Anyone who fetched it keeps it.` |
| Redo it | `Push tag v1.1 to origin again?` | `origin gets tag v1.1 back at 88d6b91.` |
| Undo the deletion of tag `v1.1` on origin | `Undo the deletion of tag v1.1 on origin?` | `origin gets tag v1.1 back at 88d6b91.` |
| Redo it | `Delete tag v1.1 on origin again?` | `tag v1.1 is deleted on origin again.` |

A lease failure: the error dialog with git's output and the summary `{b} on {r} has changed since:
nothing was changed.` (7.5).

**Check failures** (7.6), in part U: `main has moved since (it is at c7d8c71)` (tooltip);
`2 conflicts prevent it. Commit, stash, or discard your changes first.` (the app's count, git's
line below); `1 ignored file is in the way: docs/ff.txt. Move it away first.`; `published on origin
has changed since` (the lease, worded by 7.5's summary above).

**Texts of earlier phases the actions show** (unchanged; quoted where a check asserts them): the
status texts "Reset main to 8beca34 (soft)" (and `(mixed)`, `(hard)`), "Created hotfix", "Created
hotfix and checked it out", "Created keep", "Renamed feature/conflict to feature/conflicting",
"Deleted feature/alpha", "Checked out feature/ff", "Created feature/alpha tracking
origin/feature/alpha and checked it out", "HEAD is now detached at 8beca34", "Checked out main",
"Fast-forwarded loc to main", "Checked out feature/other and re-applied your changes",
"Fast-forwarded main to feature/ff", "Merged feature/ff into main", "Merged feature/other into
main", "Cherry-picked 2 commits onto main", "Reverted 88d6b91", "Rebased feature/topic onto main",
"Rebased feature/other onto main", "Rebased main: 5 commits", "Squashed 2 commits into f34248d",
"Pulled and rebased main onto origin/main", "Stashed the changes", "Applied stash@{0}", "Popped
stash@{0}", "Dropped stash@{0}", "Created tag v1.2", "Deleted tag v1.0", "Pushed topic to
origin/topic, and set it as upstream", "Force-pushed published to origin/published", "Deleted
published on origin", "Pushed published to origin/published", "Pushed tag v1.1 to origin",
"Deleted tag v1.1 on origin", "topic now tracks origin/published", "Added remote mirror",
"Renamed remote origin to upstream", "Changed the URL of origin", "Removed remote origin",
"Added /logs/run.log to .gitignore"; the C12 checkout refusal (design 9.3): `ErrorTitle` `Checkout
blocked`, `ErrorSummary` `1 conflict prevents checkout. Commit, stash, or discard your changes
first.`, the button `Stash changes and check out`.

**Git's texts** (Git 2.36.0.windows.1, the same on 2.55.0.windows.5; "/" separates lines; `<origin>`
is the copy's `origin.git` folder as `git remote get-url origin` prints it):

- **TU1** (`git read-tree -m -u <from> <to>` with a changed file the move changes, exit 128,
  standard error): `error: Entry 'docs/main.txt' not uptodate. Cannot merge.` (key). Git names the
  first path only.
- **TU2** (a push whose lease fails, exit 1, standard error), three lines:
  - `To <origin>`
  - ` ! [rejected]        8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6 -> published (stale info)` (key)
  - `error: failed to push some refs to '<origin>'`
- **TU3** (the undo and redo pushes that succeed: the summary line of each, under `To <origin>`;
  git names a pushed SHA in full):
  - U21 push, undo: ` - [deleted]         topic`; redo:
    ` * [new branch]      77d782ebfce02f1fe2b29c2ee2b6b43a1f682a02 -> topic`
  - U21 force push, undo:
    ` + 4f6a372...8c6f545 8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6 -> published (forced update)`;
    redo: ` + 8c6f545...4f6a372 4f6a37288fadb5c453061cefedee620a9f98121e -> published (forced update)`
  - U21 deletion of `published`, undo:
    ` * [new branch]      8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6 -> published`; redo:
    ` - [deleted]         published`
  - U21 tag, push undone: ` - [deleted]         v1.1`; redone:
    ` * [new tag]         88d6b91e821a43cd39c94aa7892c8095b628caf8 -> v1.1` (the deletion's undo
    prints the same `[new tag]` line, its redo the same `[deleted]` line)

### Part P: protected changes and the automatic stash (C12)

Part P covers design section 9 (which checkouts do what, 9.1; the automatic stash, 9.2; the refusal
message, 9.3), the owner's answers D153 and D166 and the design's decision D179: checks P24 to P27
of design 15.4. A checkout that would overwrite uncommitted changes does not start and says why in
D153's words, with git's count, on every checkout path (P24); checking out a commit the current
branch reaches stashes every change first, without a question, and the stash stays until the user
pops it, or Undo switches back and pops it (P25); when the automatic stash is not taken, or is
taken and put back because an ignored file is in the way (P26); and the same words for merge,
cherry-pick and revert, and the app's own check before a stash is applied (P27). The dash pattern
of every stash's line is F15's; the undo of every other action is part U's. This part was written
before the UI it tests, from `docs/phase-5-design.md` 0.7, 7.2 to 7.6, 9, 12 and 13, decisions
D153, D166, D171 and D179, and phase 3's and 4's reports for what stays as it was. Every git state
below was derived with Git 2.36.0.windows.1, and every git text replayed with Git 2.55.0.windows.5,
on a replica of "protect" whose SHAs equal the pinned ones (see "How the expected git states were
derived", part P).

#### Conventions of part P

- **Project, class and test methods.** The checks are in `tests/VisualCommit.VisualTests.Phase5`,
  class `Phase5ChecksP` (`Phase5Checks.P.cs`), with what they compare against in
  `Phase5Expectations.P.cs` (which the real-window pass links) and its tests in
  `Phase5ExpectationTests.P.cs`. One test method per app start (`FreshApplication`), each on a fresh
  copy, named as the report's parts: `Check_P24_tracked_changes_block_a_checkout` (P24 part A),
  `Check_P24_untracked_files_block_a_checkout` (P24 part B),
  `Check_P25_automatic_stash_and_the_way_back` (P25 part A),
  `Check_P25_undo_and_redo_of_the_automatic_stash` (P25 part B),
  `Check_P26_no_stash_at_head_or_off_the_branch` (P26 part A),
  `Check_P26_ignored_file_in_the_way` (P26 part B), `Check_P27_merge_pick_and_revert_refused` (P27
  part A), `Check_P27_stash_apply_refused_by_the_app` (P27 part B): eight app starts. The
  real-window pass repeats P24 part A's first steps and P25 (two launches, design 15.6), in
  `Phase5RealWindowPass.P.cs`.
- **Defaults.** A test method writes a session with one tab on a fresh copy of "protect"
  (`Scenarios.ProtectAsync()`), starts the app at 1100×700 in the dark theme
  (`Phase5Checks.OpenAsync`), and waits until the graph has drawn its rows, the status is shown and
  the queue is idle; the pointer then rests at the neutral point. The harness's clocks (design 2.3)
  are on: the frozen record clock and the fixed backup clock, whose first backup name in a session
  is `…-20260102T090001000Z`.
- **"Before the app starts"** is a file write or a git call through `TempRepo` on the copy, as a
  terminal makes it, on the copy's clock (2026-01-01 12:07, the template's after "Main work").
  "Stage" is `TempRepo.GitAsync("add", "--", <path>)`.
- **"Outside the app"** while it runs is the same, then a wait for the app's next status read: the
  check counts the Debug lines `protect: working tree: <s> staged, <u> unstaged`
  (`AppLogFile.WorkingTreeRead("protect")`) before the write and waits (`OutsideChangeTimeout`) for
  a later one with the counts the step names, then `WaitForIdleAsync`. Where the counts do not change
  (P24 step 7), it waits for a later line and until the repository's status
  (`RepositoryViewModel.Status`) lists the path the step names.
- **Rows.** Commit rows are numbered as `SelectedIndex` and `ShellDriver.GraphRowPoint` count them,
  from 0 at the top, without the working-changes row. Every graph of this part has two lanes (Graph
  column 48), so the Message column starts at x 438 and a row is clicked "in its Message column" at
  x 468. Rows are 26 high from y 116: without the working-changes row commit row r's centre is
  y 129 + 26 r; while it shows (display row 0, centre y 129), commit row r's centre is y 155 + 26 r.
  No banner shows in any step. The real window takes the same points from `RealApp.GraphRowPoint`.
- **Labels** are listed in phase 1's order (a detached `HEAD`, the branch HEAD is on with ✓, other
  local branches, tags, stashes); the drawn widths below were measured in the running app (headless,
  1100×700) when the scenarios were pinned: `main` ✓ 50.8, `main` 37.8, `other` 40.6, `v1.1` 43.7,
  `HEAD` 42.8, `stash@{0}` 68.3 pixels, 4 apart, from x 266.
- **Left-panel paths** are written Local branches › other and Stashes › On main: App experiment
  (`ShellDriver.RefRowAsync`). A double-click on a left-panel branch selects that branch's row in
  the panel and its commit in the graph with its first click, then checks out (phase 3's 4c); a
  right-click there opens the menu only (D114). A double-click on a graph label checks that branch
  out (phase 3's 4c; `GraphLabelBounds`).
- **Menus** are phase 3's and 4's; part P asserts only the item it clicks (enabled, no reason),
  except the stash menu of P27 part B, asserted whole with phase 3's 4a height rule (8 + 24 + 26 ×
  items + 9 × separators). Phase 5 adds nothing to the commit and stash menus (design 13.1).
- **Dialogs.** The error dialog (`ErrorDialog`) is asserted with `AssertErrorDialogAsync`:
  `ErrorTitle`, `ErrorSummary`, `ErrorRepository` "In protect", `ErrorCommand`, the visible buttons
  left to right ("Copy output" `ErrorCopyButton`, the extra action `ErrorExtraButton` if any, "Close"
  `ConfirmButton`) and `ErrorOutput`. A refusal the app makes before git runs has no `ErrorOutput`,
  no `ErrorCommand` and no "Copy output": "Close" only (D114). "No question" and "no dialog" are
  `AssertNoDialogAsync` after the operation ended.
- **Waiting for an operation**: `WaitForOperationAsync(app, repo, "<Title>", mark)`: the queue's
  log line `protect: <Title>: <Succeeded|Failed> after <N> ms…` and the refresh; then, for a
  failure, `WaitForDialogAsync<ErrorDialogViewModel>`. Each step names the title and the outcome.
- **The status bar** is `OperationStatus` (its tone: secondary, or the danger colour for
  `<Title> failed`) and `CurrentBranch`. Running texts are asserted with `RecordStatusTexts()`
  (every text the status showed, in order). The status text after the automatic stash is asserted
  whole: not cut with "…" (the text `OperationStatus` lays out is the whole text; it is at most 520
  wide). `AssertNoClippedText` would not tell, since the text's tooltip holds it whole.
- **The toolbar's Undo and Redo** (`UndoButton`, `RedoButton`, design 1.1) are asserted by
  `IsEnabled` and their tooltip (`ToolTip.GetTip`, which is also their `HelpText` in the real window)
  after every step: `Nothing to undo` / `Nothing to redo` when no entry, `Undo {text}` / `Redo {text}`
  with 7.6's entry texts. A refused action records nothing (7.2), so every refusal of this part
  leaves Undo as it was. Every picture shows the toolbar and status bar as part A gives them for an
  open repository (Search and the Activity log toggle enabled), and the details panel with part B's
  `All files` switch; those are not this part's subject.
- **The call log** (`IGitCallLog`, `CallsSince`, `AssertCalledInOrder`, `ShellDriver.LastCall`) is
  where a check reads which git commands ran, with their exit codes and output; a command is written
  as the call log's `CommandText` renders it (phase 3's: an argument with spaces in double quotes);
  "no `git stash` call" means no call whose arguments start with `stash push`, `stash pop` or
  `stash apply` since the step began.
- **Git's state**, asserted on every git version: HEAD (`git symbolic-ref -q HEAD`, `git rev-parse
  HEAD`), the refs (`git for-each-ref`), `git stash list`, `git status --porcelain=v1
  --untracked-files=all --ignored`, the files' bytes, HEAD's reflog (`git log -g --format=%gs
  HEAD`), the in-progress files (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `sequencer/`) and
  the backup refs (`git for-each-ref refs/visualcommit/backup`). "The exact state" is what
  `TempRepo.ExactStateAsync` reads (the pins, the scripted run, the real-window pass and the App
  and Git tests share it), joined into one text: HEAD (`git rev-parse HEAD`), every ref (`git
  for-each-ref --format="%(refname) %(objectname)"`), the index (`git ls-files --stage`), the status
  (`git status --porcelain=v2 --untracked-files=all --ignored`) and every file of the working tree
  outside `.git` with its contents, in ordinal order; it is compared whole with the one read at the
  point the step names.
- **Git's own texts** below (`ErrorOutput`, the call log's lines) are Git 2.36.0.windows.1's and
  are asserted word for word only when `git --version` is 2.36; on another git a check asserts the
  line marked "key", that `ErrorOutput` is `GitOutputCleaner.Clean` of the failed call's own output
  (`AssertGitOutputShown`), and the dialog's title, summary and buttons (phase 3's rule). Where Git
  2.55.0 prints otherwise the text says so; the key lines are printed by both.
- **Dates** are UTC (`VISUALCOMMIT_TIME_ZONE=UTC`). The scenario's commits are dated 2026-01-01
  12:00 to 12:06; a stash the app makes has the app's commit date, 2026-01-02 09:00 (D72), as author
  and committer date, so it has the same SHA in every run and in both harnesses.
- **The pointer** moves to the neutral point (760, 687) before every capture (`Shot`); no step of
  this part pictures a tooltip.
- **New harness code** (design 15.2; WP8 writes it to these checks): in `Phase5Checks.Support.cs`
  `AssertDashedLines(app, params (int Row, GraphLineKind Kind, int Lane)[] lines)` (the lines of
  `CommitGraphData.RowAt(row).Lines` that carry `GraphLine.IsDashed`, exactly; F15 uses it too),
  `ExactStateAsync(TempRepo)` (the same reads as the pins' private helper of that name, above) and
  `WaitForStatusReadAsync(app, staged, unstaged,
  mark)` (phase 4's inline wait of A4, as a helper). Everything else exists: the toolbar buttons
  through `ToolbarButton(app, "UndoButton")`, the left panel through `RefRowAsync`, labels through
  `GraphLabelBounds`; in the real window `RealApp.Find("UndoButton").HelpText` (FlaUI) and
  `DoubleClickWithMouse`.

#### The scenario and its graphs (part P)

**"protect"** (`Scenarios.ProtectAsync`, folder and tab `protect`), as `Phase5ScenarioTests`
pins it. All commits by "Test Author" <author@example.com>. HEAD on `main`; `other` has no upstream;
no remote; the tag `v1.1` is annotated ("Version 1.1", tag object a2f81f7) on Describe the app.
`settings.local.json` is in the working tree, ignored (`.gitignore` holds `settings.local.json`),
with `{\n  "theme": "dark"\n}\n`; Initial commit and Add the guide track a file of that name
(`{\n  "theme": "light"\n}\n`). `git status --porcelain=v1 --untracked-files=all --ignored`
prints `!! settings.local.json` only. Files on `main`: `README.md` "# Protect\n\nAn app to
protect.\n\nMain work.\n", `src/app.txt` "app\n", `src/lib.txt` "lib\n", `docs/guide.txt`
"guide\n", `docs/old.txt` "old notes\n", `.gitignore`. `other` changes `src/app.txt` ("app\nother\n"),
`src/lib.txt` ("lib\nother\n") and `docs/guide.txt` ("guide\nother\n") (Other edits), adds `notes`
("Notes of the other branch.\n") and deletes `docs/old.txt` (Other files).

**P0, "protect" as built.** 2 lanes, Graph 48; Message x 438 (142 wide), Date x 580 to 700; no
working-changes row.

| Row | Labels (drawn at 1100×700) | Node: lane, colour | Message | Date | SHA | y centre |
|---|---|---|---|---|---|---|
| 0 | `main` ✓ (x 266 to 316.8) | 0, 0 | Main work | 2026-01-01 12:06 | `41f8398` | 129 |
| 1 | `other` (x 266 to 306.6) | 1, 1 | Other files | 2026-01-01 12:05 | `19a9044` | 155 |
| 2 | | 1, 1 | Other edits | 2026-01-01 12:04 | `4f5e821` | 181 |
| 3 | `v1.1` (x 266 to 309.7) | 0, 0 | Describe the app | 2026-01-01 12:03 | `f39ea2d` | 207 |
| 4 | | 0, 0 (lane 1 ends here) | Stop tracking the local settings | 2026-01-01 12:02 | `64166f7` | 233 |
| 5 | | 0, 0 | Add the guide | 2026-01-01 12:01 | `73e0bab` | 259 |
| 6 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `035a2f5` | 285 |

Lines: lane 0 (colour 0) from row 0 to row 6; lane 1 (colour 1) from row 1's node into row 2's node,
through row 3, into row 4's node. Nodes at x 406 (lane 0) and 422 (lane 1). Left panel: Local
branches 2 (`main` in the accent colour, `other`), Remotes 0, Pull requests 0, Tags 1 (`v1.1`),
Stashes 0. `CurrentBranch` `main`.

Full SHAs: Main work 41f8398f95fa73a5614e32760cc511673aa6a80d, Other files
19a90447e124bf3a74124a5673df655601c53d5b, Other edits 4f5e8215c9321acae3a2ea386bda05107915f2da,
Describe the app f39ea2d9addec45f926151cd2267cb57ea0b16aa, Stop tracking the local settings
64166f7723b59f263065d3ff2ee199263469846e, Add the guide 73e0babca7d61aaa9088f167ddbf0951f5f13744,
Initial commit 035a2f5b2cca78bd25cc95f5af43b0daf9a28a0c.

| Commit | `git merge-base --is-ancestor <it> HEAD` | Tracks `settings.local.json` | What checking it out changes (`git diff --name-status HEAD <it>`) |
|---|---|---|---|
| Main work (HEAD) | 0 (HEAD's own commit) | no | nothing |
| Other files | 1 | no | M README.md, M docs/guide.txt, D docs/old.txt, A notes, M src/app.txt, M src/lib.txt |
| Other edits | 1 | no | M README.md, M docs/guide.txt, M src/app.txt, M src/lib.txt |
| Describe the app (`v1.1`) | 0 | no | M README.md |
| Stop tracking the local settings | 0 | no | M README.md |
| Add the guide | 0 | **yes** | D .gitignore, M README.md, A settings.local.json |
| Initial commit | 0 | **yes** | D .gitignore, M README.md, D docs/guide.txt, D docs/old.txt, A settings.local.json |

The graphs the steps make (each derived from P0 by the layout rules D48, D54, D55 and D60, and,
for P1, measured in the app on the state plain git made):

- **P0w: P0 below the working-changes row** (any uncommitted change). Display row 0 (y 116 to 142,
  centre 129) is the working-changes row "Working changes": its ring in lane 0, colour 0 (Main
  work's lane and colour), at (406, 129), and its dashed line down lane 0 into Main work's node. The
  commit rows are P0's, one lower: commit row r's centre is y 155 + 26 r, clicked at (468, 155 + 26 r):
  Main work 155, Other files 181, Other edits 207, Describe the app 233, Stop tracking the local
  settings 259, Add the guide 285, Initial commit 311. The label `other` lies at x 266 to 306.6,
  y 172 to 190.
- **P0h: P0w with HEAD detached at Main work** (P26 part A). As P0w, with row 0's labels `HEAD`
  (detached) and `main` (no check mark), both drawn: `HEAD` x 266 to 308.8 and `main` x 312.8 to
  350.6, which end left of where P1's `HEAD`, `v1.1` end (356.5). Left panel: no branch accented.
  `CurrentBranch` `Detached at 41f8398`.
- **P1: after the automatic stash and the checkout of Describe the app** (measured in the app at
  1100×700). 2 lanes; no working-changes row.

  | Row | Labels (drawn) | Node: lane, colour | Message | Date | SHA | y centre |
  |---|---|---|---|---|---|---|
  | 0 | `stash@{0}` (x 266 to 334.3) | 0, 0 (ring) | On main: Auto stash before checking out f39ea2d (cut with "…", secondary colour) | 2026-01-02 09:00 | `256077f` | 129 |
  | 1 | `main` (no check mark; x 266 to 303.8) | 0, 0 | Main work | 2026-01-01 12:06 | `41f8398` | 155 |
  | 2 | `other` | 1, 1 | Other files | 2026-01-01 12:05 | `19a9044` | 181 |
  | 3 | | 1, 1 | Other edits | 2026-01-01 12:04 | `4f5e821` | 207 |
  | 4 | `HEAD` (detached; x 266 to 308.8), `v1.1` (x 312.8 to 356.5), both drawn | 0, 0 | Describe the app | 2026-01-01 12:03 | `f39ea2d` | 233 |
  | 5 | | 0, 0 (lane 1 ends here) | Stop tracking the local settings | 2026-01-01 12:02 | `64166f7` | 259 |
  | 6 | | 0, 0 | Add the guide | 2026-01-01 12:01 | `73e0bab` | 285 |
  | 7 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `035a2f5` | 311 |

  Lines: lane 0 (colour 0) **dashed** from row 0's ring into row 1's node (x 406, y 129 to 155:
  `IsDashed` on row 0's `OutOfNode` and row 1's `IntoNode` in lane 0, D161), then solid from row 1
  to row 7; lane 1 (colour 1) from row 2's node through row 3's node and row 4 into row 5's node.
  No other line is dashed. Left panel: Local branches 2 (`main`, `other`, neither accented), Remotes
  0, Pull requests 0, Tags 1 (`v1.1`), Stashes 1 ("On main: Auto stash before checking out
  f39ea2d"). `CurrentBranch` `Detached at f39ea2d`. The stash: 256077fbd990adad7fe00df31789590d7370aff0,
  parents 41f8398 (main's tip), f632741c7c7e1ac98bda443a390dc123e8fda26c (`index on main: 41f8398
  Main work`) and 9d22397dd9081ff84ccfc699db779eafbfaedb8c (`untracked files on main: 41f8398 Main
  work`); its changed files (against its first parent, as the details panel shows them) M
  `src/app.txt`, M `src/lib.txt`; `scratch.txt` only in its third parent.
- **P1m: P1 with `main` checked out again.** P1's rows, lanes, lines (the stash's still dashed) and
  positions; row 1's label is `main` ✓ (x 266 to 316.8); row 4's is `v1.1` alone (no `HEAD`). Left
  panel: `main` accented, Stashes 1 as P1. `CurrentBranch` `main`.
- **P0s: "protect" with the stash "On main: App experiment" and a changed file** (P27 part B). The
  working-changes row at display row 0 (ring in lane 0, colour 0), then P1m's rows one lower, with
  row 0 the stash `cc3e07a` "On main: App experiment" (ring, lane 0, colour 0, dated 2026-01-01
  12:07, `stash@{0}`), its line dashed into Main work's node (commit row 1, display row 2); the
  working-changes ring's dashed line runs down lane 0 to Main work's node under the stash's ring, the
  shape phase 3's check 15 shows after its step 5. Left panel: Stashes 1 ("On main: App experiment").
  The stash: cc3e07a6f75cacfae5f8d8edaec55844a4f2c428, parents 41f8398,
  9c08a4212288dc9f1418cbe06ecf0e9fb98c6295
  (`index on main: 41f8398 Main work`) and a058536929790a82d5316990cb3664692068bc9a (`untracked
  files on main: 41f8398 Main work`, holding `todo.txt`); it changes `src/app.txt` ("app\nstashed\n").

#### Texts these checks rely on (part P)

**The refusal message** (design 9.3, D153 (1), D179; the error dialog's title `Checkout blocked` for
every row of 9.3's first table):

| Git said (classified from standard error and output together, with the count of distinct tab lines under git's headers, 0.7) | `ErrorSummary` | Extra button |
|---|---|---|
| only `Your local changes to the following files would be overwritten by checkout:` blocks | `1 conflict prevents checkout. Commit, stash, or discard your changes first.` / `{N} conflicts prevent checkout. Commit, stash, or discard your changes first.` | `Stash changes and check out` (`ErrorExtraButton`, D80, D91) where phase 3 offers it (`offerStash`: the checkouts of `CheckoutAsync`); never after an automatic stash's refusal |
| an untracked block (`untracked working tree files would be overwritten by checkout:`, `would be removed by checkout:`, `Updating the following directories would lose untracked files in them:`) whose paths are not all ignored | the same sentence; git's output below names the files | none (D91) |
| untracked blocks whose paths are all ignored (`check-ignore --stdin -z`) | `1 ignored file is in the way: {short} tracks a file of the same name. Move it away first.` / `{N} ignored files are in the way: …` (the design writes the plural only so far; no check of this part shows it) | none |
| `you need to resolve your current index first` | refused before git: `Resolve the conflicts first` | none |
| `cannot switch branch while …` | refused before git: `Finish or abort the {kind} first` | none |

**The same words for other operations** (9.3's second table; each keeps its own error title and
status text, phase 3's 2.33): `1 conflict prevents the merge. Commit, stash, or discard your changes
first.` / `{N} conflicts prevent the merge. …`, likewise `the cherry-pick` and `the revert`, for git's
`by merge:` blocks; `Your staged changes prevent the merge. Commit, stash, or unstage them first.`,
likewise `the cherry-pick` and `the revert`, for ort's one-line form or `your local changes would be
overwritten by cherry-pick.` / `by revert.`; a stash's apply or pop, checked by the app before git
(the stash's changed paths that have a local change, plus its untracked paths that exist):
`1 conflict prevents applying the stash. Commit, stash, or discard your changes first.` / `{N}
conflicts prevent applying the stash. Commit, stash, or discard your changes first.`; git is not run.
Rebase keeps phase 4's `Git rebases only a clean working tree: commit or stash your changes first.`

**The automatic stash** (9.2, 12.1, 12.2): the operation `Check out {name}` (phase 3's title, several
steps), running texts `Stashing your changes…` then `Checking out {name}…`; the stash's message
`Auto stash before checking out {name}`, which `git stash list` shows as `On {branch}: Auto stash
before checking out {name}`; `{name}` the short SHA (or the tag's name); the result in the status bar
and its tooltip `Your changes are in the stash "Auto stash before checking out {name}".`; a refusal
after the stash: the stash popped back, then 9.3's message without the button. (The texts of a
half-made stash, `Git could not clear your changes from the working tree, …` and `Your changes are
saved in the stash …`, are not shown by any check: Windows file locks are 9.4's Git tests.)

**Undo and redo** (1.1, 7.4 to 7.6, 12.1, 12.2): tooltips `Undo {text}`, `Redo {text}`, `Nothing to
undo`, `Nothing to redo`; entry texts `checkout of {name}`, `pop of stash "{message}"` (the stash's
listed message); the operations `Undo {text}` and `Redo {text}`, running `Undoing {text}…` and
`Redoing {text}…`, results `Undid {text}` and `Redid {text}`. The automatic stash's checkout is
undone by `Switch(target, branch)` then `StashPop(sha)` (`git switch --no-guess --no-overwrite-ignore
<b>`, `git stash pop --index stash@{n}`) and redone by `StashPush(--include-untracked, its message)`
then `Switch(branch, target)` (7.3, D166); no question (13.2).

**Phase 3's texts that stay** (2.33, D113): titles `Check out {name}`, `Create branch {b}`,
`Merge {source}`, `Cherry-pick {short}`, `Revert {short}`, `Apply stash@{n}`, `Pop stash@{n}`; error
titles `Merge failed`, `Cherry-pick failed`, `Revert failed`, `Applying the stash failed`; status
`<Title> failed` in the danger colour, `HEAD is now detached at {short}`, `Checked out {b}`, `Popped
stash@{n}`; `CurrentBranch` `Detached at {short}`; the name dialog's `Create branch` with `From
{short} "{subject}"` and "Check out the new branch"; the toolbar's `There are no changes to stash`
and `There are no stashes`.

**Git's texts** (Git 2.36.0.windows.1, standard error unless said; ⇥ is a tab, " / " separates the
lines `ErrorOutput` shows after `GitOutputCleaner.Clean`):

- **TP1** (`git switch --no-guess other` with `src/app.txt` changed, exit 1; standard output empty):
  `error: Your local changes to the following files would be overwritten by checkout:` (key) /
  `⇥src/app.txt` / `Please commit your changes or stash them before you switch branches.` /
  `Aborting`. `git switch --create topic 19a90447e124bf3a74124a5673df655601c53d5b` (**TP2**) and
  `git switch --detach 4f5e8215c9321acae3a2ea386bda05107915f2da` (**TP1d**) with the same change print
  exactly these lines, exit 1. 2.55: identical.
- **TP3** (`git switch --no-guess other` with `src/app.txt`, `src/lib.txt` and `docs/guide.txt`
  changed, exit 1): the key line / `⇥docs/guide.txt` / `⇥src/app.txt` / `⇥src/lib.txt` / `Please
  commit your changes or stash them before you switch branches.` / `Aborting`. 2.55: identical.
- **TP4** (`git switch --no-guess other` with the untracked file `notes`, exit 1): `error: The
  following untracked working tree files would be overwritten by checkout:` (key) / `⇥notes` /
  `Please move or remove them before you switch branches.` / `Aborting`. 2.55: identical.
- **TP5** (`git switch --no-guess other` with the untracked `notes/today.txt`, exit 1): `error:
  Updating the following directories would lose untracked files in them:` (key) / `⇥notes` /
  `Aborting` (git prints an empty line before `Aborting`, which the cleaning drops). 2.55: identical.
- **TP6** (`git switch --detach --no-overwrite-ignore 73e0babca7d61aaa9088f167ddbf0951f5f13744`,
  exit 1, with or without other changes): `error: The following untracked working tree files would
  be overwritten by checkout:` (key) / `⇥settings.local.json` / `Please move or remove them before
  you switch branches.` / `Aborting`. 2.55: identical. `git check-ignore --stdin -z` given
  `settings.local.json` prints `settings.local.json` (exit 0) without `GIT_LITERAL_PATHSPECS` (D74); with it
  both gits fail, `fatal: settings.local.json: pathspec magic not supported by this command:
  'literal'` (128).
- **TP7** (`git merge --no-edit --no-log -m "Merge branch 'other'" refs/heads/other` with
  `src/app.txt` changed, exit 128): `error: Your local changes to the following files would be
  overwritten by merge:` (key) / `⇥src/app.txt` / `Please commit your changes or stash them before you
  merge.` / `Aborting`. 2.55: exit 2 and a fifth line `Merge with strategy ort failed.`.
- **TP8** (`git cherry-pick 4f5e8215c9321acae3a2ea386bda05107915f2da` with `src/app.txt` changed,
  exit 128): TP7's four lines (key: its first) / `fatal: cherry-pick failed`. 2.55: identical.
- **TP9** (the merge of TP7 with `README.md` staged, exit 128): `Your local changes to the following
  files would be overwritten by merge:` / `  README.md` (key; two leading spaces, no tab; git ends it
  without a line feed and prints no `error:`). 2.55: exit 2, `error: Your local changes to the
  following files would be overwritten by merge:` / `  README.md` / `Merge with strategy ort
  failed.`: the same first line as TP7's, so only the names' indent tells the staged form.
- **TP10** (the cherry-pick of TP8 with `README.md` staged, exit 128): `error: your local changes
  would be overwritten by cherry-pick.` (key) / `hint: commit your changes or stash them to proceed.` /
  `fatal: cherry-pick failed`. 2.55: identical.
- **TP11** (`git revert --no-edit f39ea2d9addec45f926151cd2267cb57ea0b16aa` with `README.md` staged,
  exit 128): `error: your local changes would be overwritten by revert.` (key) / `hint: commit your
  changes or stash them to proceed.` / `fatal: revert failed`. 2.55: identical.
- **TP12** (the revert of TP11 with `README.md` changed and not staged, `src/app.txt` changed too,
  exit 128; standard output empty): `error: Your local changes to the following files would be
  overwritten by merge:` (key) / `⇥README.md` / `Please commit your changes or stash them before you
  merge.` / `Aborting` / `fatal: revert failed`. 2.55: identical.
- **Successful calls**: `git stash push --include-untracked --message "Auto stash before checking
  out <name>"` prints on standard output `Saved working directory and index state On main: Auto
  stash before checking out <name>` (exit 0); `git switch --detach --no-overwrite-ignore <sha>`
  prints on standard error `HEAD is now at <short> <subject>` (exit 0); `git switch --detach
  41f8398f95fa73a5614e32760cc511673aa6a80d` (HEAD's own commit) with `src/app.txt` changed prints
  `M⇥src/app.txt` on standard output and `HEAD is now at 41f8398 Main work` on standard error
  (exit 0); `git switch --no-guess
  main` from the detached Describe the app prints `Previous HEAD position was f39ea2d Describe the
  app` / `Switched to branch 'main'`; `git stash pop [--index] stash@{0}` prints git's status and
  ends `Dropped stash@{0} (<the stash's full SHA>)` on standard output (exit 0). 2.55: identical,
  and the stash has the same SHA.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). "Real window" says which steps the
real-window pass repeats (RW), or "n/a" with the reason; "Screenshots" lists the planned pictures,
the scripted run's and then the real window's. A check's scenario, theme and window size are named
at the end of its "Check" column; its test methods and app starts in its Steps (and, for parts A
and U, in their conventions). Part U's T0, T+ and T− and "Undo ran" / "Redo ran" are defined under
"Conventions of part U"; the graphs each row names are defined in its part's scenarios section.

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| A1 | The toolbar and the status bar: an empty tab (only Theme enabled, nothing with a tooltip); a repository open (Search and Activity log enabled, Undo and Redo disabled with `Nothing to undo` and `Nothing to redo`); after a commit, Undo enabled at once with `Undo commit "Describe the lines"`; "history", dark, 1100×700 | One app start (`Check_A1_toolbar_and_status_bar`). 1. "History" scenario. Start the app with one tab on it. Click the tab strip's `+` (`AddRepoButton`). Screenshot. 2. Click the tab `history` (`RepoTab` named `history`). Wait until idle. Screenshot. 3. Rest the pointer on Undo (`UndoButton`) and wait for its tooltip. Screenshot. Move the pointer to the neutral point; rest it on Redo (`RedoButton`) and wait for its tooltip. Move it to the neutral point; rest it on Search (`SearchButton`) and wait for its tooltip. Screenshot. Move it to the neutral point; rest it on `Activity log` (`ActivityLogToggle`) and wait for its tooltip. Move it to the neutral point. 4. Click the working-changes row (display row 0, (468, 129)). Click `Stage all` (`StageAllButton`); wait until idle. Type `Describe the lines` into the summary (`TypeInto("CommitSummary", …)`). Click the commit button (`CommitButton`); wait until idle. Screenshot. 5. Rest the pointer on Undo and wait for its tooltip. Screenshot. Move the pointer to the neutral point. 6. Click the tab `New tab`. 7. Click the tab `history`. | **After step 1:** a second tab `New tab` is active, the tab `history` beside it on its left; the welcome page in the graph area (`No repository open`, `Open, clone or init a repository to see its history.`, `Open`, `Clone`, `Init`, `Recent repositories`, `No recent repositories`); the left panel's five sections each `0`; the right panel `Commit details` and `Select a commit to see its details.`. Toolbar: Undo, Redo, Fetch, Pull, Push, Branch, Stash, Pop and Search disabled, in `VcTextDisabledBrush`, none with a tooltip or a help text; Theme enabled, tooltip `Switch to the light theme`. Status bar, left to right: `No repository`, `Ready`, then at the right `Git 2.36.0` (the machine's version) and `Activity log`, the toggle disabled (`#5F6775`), its tooltip `Show every git command the app has run`, no `selected` class. The toolbar and status bar are phase 0's empty shell's (phase 0's "What the shell must show"), which phase 5 leaves as they were (design 1.1, 1.2).<br>**After step 2:** H0 with nothing selected; the right panel `Select a commit to see its details.`; `CurrentBranch` `main`; status `Ready`. Toolbar: Undo disabled, tooltip and help text `Nothing to undo`; Redo disabled, `Nothing to redo`; Fetch, Pull, Push, Branch and Stash enabled with no tooltip (phase 3's 4d: the branch tracks `origin/main`, the tree has a change); Pop disabled, `There are no stashes`; Search enabled, its icon and label in `VcTextPrimaryBrush`, tooltip and help text `Search commits (Ctrl+F)`; Theme as before. The Activity log toggle enabled, its label `#9BA3B0`, tooltip and help text `Show every git command the app has run`, no `selected` class. No activity log panel: the graph area's content still reaches y 674, rows as H0 gives them.<br>**After step 3:** the tooltip `Nothing to undo` drawn inside the window under the pointer, the button still disabled (pictured); Redo's tooltip `Nothing to redo` (asserted, not pictured); Search's tooltip `Search commits (Ctrl+F)` (pictured); the toggle's `Show every git command the app has run` (asserted). Resting changed nothing else: no menu, no operation, nothing selected.<br>**After step 4:** two operations ran, `Stage` and `Commit`, both from the stage panel, so the status bar still says `Ready`; no dialog. Git: `main` is `d1e23a37b6b08f4dc9662fab930f232a66ecd8fc` (H-A1's commit: message `Describe the lines`, parent `c0bc2dd`, Test Author, author and commit date 2026-01-02 09:00 UTC); `git status -sb` prints `## main...origin/main [ahead 1]` and nothing else; HEAD's reflog's newest entry `commit: Describe the lines`. The graph is H-A1 without a working-changes row; the new commit (row 0) is selected and its details shown (phase 2's rule: once a commit leaves no changes, the new commit is selected); the left panel's `main` ↑1. Toolbar, read right after the wait for idle, with no other refresh awaited ("at once", design 7.4's reason 1 is gone once the queue's `Changed` follows `Finish`): Undo enabled, its icon and label in `VcTextPrimaryBrush`, tooltip and help text `Undo commit "Describe the lines"`; Redo disabled, `Nothing to redo`; Search enabled as before; Stash disabled, `There are no changes to stash`; Pop disabled, `There are no stashes`; Fetch, Pull, Push and Branch enabled. Status bar: `main`, `Ready`, the toggle enabled.<br>**After step 5:** the tooltip `Undo commit "Describe the lines"` drawn inside the window, under the pointer, below the button. Resting ran nothing: `main` still `d1e23a3`, no record titled `Undo commit "Describe the lines"` in `IOperationLog`.<br>**After step 6:** the empty tab's toolbar and status bar again, as after step 1: Undo disabled with no tooltip or help text (the toolbar acts on the active tab, design 1.1), the toggle disabled.<br>**After step 7:** the toolbar as after step 4 (Undo enabled, `Undo commit "Describe the lines"`), H-A1 again with the new commit selected: switching tabs kept the repository's undo history (design 7.1). | Not run yet | RW: steps 1, 2, 4, 6 and 7 with the real mouse and keyboard: the click on `+` and on the tabs, on the working-changes row, `Stage all`, the summary typed key by key, the commit button; the buttons' `IsEnabled` and `HelpText` read through UI Automation after each step (the texts of steps 1, 2 and 4, as the scripted run asserts them); the captures after steps 1, 2 and 4 compared with `a1a-empty-tab`, `a1b-repository-open`, `a1e-committed`. What only the real window shows: the help text a screen reader gets, and Undo enabled after a commit made with real input. The tooltips themselves (steps 3 and 5) are scripted only: the pass reads `HelpText` instead | `a1a-empty-tab`, `a1b-repository-open`, `a1c-undo-tooltip-nothing`, `a1d-search-tooltip`, `a1e-committed`, `a1f-undo-tooltip`; real window: `a1a-empty-tab`, `a1b-repository-open`, `a1e-committed` | Design 1.1, 1.2, 7.4, 7.6, 13.4; D163, D167. The stage panel's operations `Stage` and `Commit` leave the status bar `Ready` (phase 2). The empty tab's states are pinned also by `ToolbarTests` and phase 0's `ShellExpectations`, which phase 5 does not change (14.1). |
| A2 | The activity log on the conflicts scenario with old and many backups: opened from the status bar under the graph; the clean-up's `Removed backup … (older than 30 days)` and `(more than 50 backups)` notes; operations with each outcome (succeeded, failed with the dialog's summary, a stop with conflicts, the merge tool running and then cancelled); a row opened to its git calls; calls opened to their input, error and output; `Show reads` (asserted by structure); Esc in the log with a page open closes only the log; the abort's backup and its clean-up note; "conflicts", dark, 1100×700, the panel 320 high; two test methods, each on a fresh copy | Method 1 (`Check_A2_activity_log`, one app start). 1. "Conflicts" with old and many backups and other.txt changed; the session's `ActivityLogHeight` 320. Start the app; wait until the record `Clean up backups` exists and the queue is idle. Click `Activity log` in the status bar. Screenshot. 2. Click the row `Clean up backups`. Click its call row `git update-ref --stdin`. Screenshot. 3. Click the call row `git update-ref --stdin` (its box closes), then the row `Clean up backups` (it closes). In the left panel click the folder `feature` under Local branches, then double-click its `kinds`. Wait for the error dialog; click `Close`. Click the row `Check out feature/kinds`, then its call row `git switch --no-guess feature/kinds`. Screenshot. 4. Tick `Show reads` (`ActivityLogShowReads`). Screenshot. Untick it. 5. Right-click the Local branches section's header and click `Show backups`; wait until the page lists its rows. Click the call row `git switch --no-guess feature/kinds` in the log (its box closes), then the row `Check out feature/kinds` (it closes; the keyboard focus is now in the log). Screenshot. 6. Press Esc. Screenshot. Press Esc. Method 2 (`Check_A2_activity_log_stop_cancel_abort`, one app start). 7. The same scenario on a fresh copy, with the waiting merge tool, and the same session. Start the app; wait as in step 1. Click `Activity log`. Click the folder `feature` under Local branches, right-click its `theirs` and click `Merge feature/theirs into main`; wait until idle. Click the row `Merge feature/theirs`, then its call row `git merge --no-edit --no-log -m "Merge branch 'feature/theirs'" refs/heads/feature/theirs`. Screenshot. Click the call row `git merge --no-edit --no-log -m "Merge branch 'feature/theirs'" refs/heads/feature/theirs` (its box closes; the focus is on it); press Left (the focus moves to the row `Merge feature/theirs`, which the list brings into view) and Left again (it closes). 8. Click the working-changes row (display row 0, from the graph's bounds under the banner). Right-click the unstaged `notes.txt` and click `Open in merge tool`. Wait until the tool has started (`tool-started` beside the working tree) and the status bar shows Cancel. Screenshot. 9. Click Cancel (`OperationCancelButton`); wait until idle, at most 30 s; release the tool (`tool-release`). Click the row `Merge tool for notes.txt`. Screenshot. 10. Click the row `Merge tool for notes.txt` (it closes). Click the banner's `Abort merge`; in the question click `Abort merge`; wait until idle. Click the row `Abort the merge`. Screenshot. 11. Click the call row `git update-ref --stdin` under `Abort the merge` that comes before `git merge --abort` (`ActivityLogRow("git update-ref --stdin", 0)`: with the clean-up's row closed, the rows shown hold two `git update-ref --stdin`, both the abort's, and this is the first). Screenshot. | **At the start (both methods):** the graph as "conflicts for A2" gives it (display row 0 "Working changes"); nothing selected; status `Ready` (the clean-up shows nothing, D139); `CurrentBranch` `main`; no dialog. Toolbar: Undo `Nothing to undo`, Redo `Nothing to redo` (disabled: the clean-up is a background operation and gives no reason, 7.4), Search enabled. Git: 50 backups, `reset-20251220T090002000Z` to `reset-20251220T090051000Z` (`git for-each-ref refs/visualcommit/backup` prints 50 refs, each → `da31c97`); `## main`, ` M other.txt`. The log file has `Removed backup reset-20251120T090001000Z (older than 30 days)` and the two other lines of the clean-up (phase 4's 9). Method 2's copy also holds `merge.tool=slow` in its own configuration, which nothing shows.<br>**After step 1:** the panel (`ActivityLog`) open under the graph area: x 260 to 700, y 354 to 674; the splitter y 349 to 354 with its 1-px line; the graph area's content y 88 to 349 (the column headers y 88 to 116, display rows 0 to 7 whole); the left and right panels unchanged at full height. Header y 354 to 384: `Activity log`, `conflicts` (secondary), at the right `Show reads` unchecked and the close button (`ActivityLogClose`). The list y 384 to 674 holds one row: `[IconCheck, #3FB97F] 09:00:00 · Clean up backups · Removed 3 backups · 0 ms` (message secondary), closed, 24 high, y 384 to 408. The toggle has the class `selected`: background `#272C36` at (1008, 678), label `#E4E7EC`; tooltip and help text `Hide the activity log`.<br>**After step 2:** the clean-up row open (`IconChevronDown`); under it, indented 24, three notes in `VcTextSecondaryBrush`: `Removed backup reset-20251120T090001000Z (older than 30 days)`, `Removed backup branch-20251120T090002000Z (older than 30 days)`, `Removed backup reset-20251220T090001000Z (more than 50 backups)`; then exactly two calls (the clean-up is not recorded, 7.2, so no read of 7.7 joins them), each 22 high: `exit 0 · git for-each-ref "--format=%(refname) %(objectname)" refs/visualcommit/backup/ · 0 ms` and `exit 0 · git update-ref --stdin · 0 ms` (both `exit 0` secondary, the commands in the code font). The update-ref call open: a box (`ActivityLogOutput`) with one part titled `Input` holding three lines, `delete refs/visualcommit/backup/reset-20251120T090001000Z da31c977c80e9becdc908c74d973669aad1b3b8d`, `delete refs/visualcommit/backup/branch-20251120T090002000Z/old-idea da31c977c80e9becdc908c74d973669aad1b3b8d`, `delete refs/visualcommit/backup/reset-20251220T090001000Z da31c977c80e9becdc908c74d973669aad1b3b8d`, and no `Output`, `Error` or `No output` (the input is not empty). The box's three input lines are wider than the box and wrap, so the list, kept at its end (AD15), has scrolled the clean-up's row out of view (the picture shows the notes, the calls and the box). The rows match the records: the `Clean up backups` record has `IsBackground` true, `Kind` Local, `Outcome` Succeeded, the three notes in that order; its two calls carry its id.<br>**After step 3:** the double-click's first click selected `kinds` and, in the graph, "Kinds edits" (display row 2); the double-click ran `git switch --no-guess feature/kinds`, which git refused (its four error lines, exit 1). The error dialog (part P's P24 asserts it in full): title `Checkout blocked`, summary `1 conflict prevents checkout. Commit, stash, or discard your changes first.`, `ErrorCommand` `git switch --no-guess feature/kinds`, buttons `Copy output`, `Stash changes and check out`, `Close`. After `Close`: no dialog; status `Check out feature/kinds failed` (danger); nothing changed in git (`main` checked out, `## main`, ` M other.txt`); Undo still `Nothing to undo` (a refused action records nothing, 7.2). The log: the clean-up row closed, then `[IconClose, #E5606B] 09:00:00 · Check out feature/kinds · Check out feature/kinds failed · 0 ms`, the message in `#E5606B`, open: first, indented 24, the failure's summary in `#E5606B`, `1 conflict prevents checkout. Commit, stash, or discard your changes first.` (the record's `FailureSummary`, set after the dialog chose it, 2.3); no note; then its calls as the call log has them, among them `exit 1 · git switch --no-guess feature/kinds · 0 ms` with `exit 1` in `#E5606B`. A checkout is recorded (7.2), so 7.7's reads join its calls, before and after the switch, although git refused it; on `e33f846`, without them, the calls were `git symbolic-ref -q HEAD`, `git rev-parse -q --verify HEAD` and the switch (information only). The switch call open: one part titled `Error` with the four lines of git's refusal; no `Output`. The list keeps to its end as the rows open (AD15): with 7.7's reads among the calls, the checkout's row has scrolled out of view and its summary is the first row whole in view (the picture shows the summary, the calls around the switch and its box); so the row, as drawn, is asserted once the row has opened and before its call opens, while it is in view, and the summary then and now.<br>**After step 4:** `Show reads` checked. Asserted by structure (the picture varies with the number of reads the app happened to make): the list's top-level rows are, in the order of their sequence numbers, every operation of this worktree (`IOperationLog`, `RepositoryKey` the copy's `.git`) and every read of `IGitCallLog` (no `OperationId`) that design 2.3 gives this worktree, each once; the read `git for-each-ref "--format=%(refname) %(objectname)" refs/visualcommit/backup/` without an operation id (the one that found the backups due) comes before `Clean up backups`; `git --version` (run with no working folder) is not shown; no call that carries an operation's id is a top-level row (each stays under its operation); the operations keep their order and their open or closed state. Whether the opening's own reads (from `git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir` to the first refresh's `git log …`, made by `RepoTabViewModel.OpenAsync` before the tab has its repository) are among the rows is not asserted until the lead rules on it (decision AD11). Unticked: the two operation rows only, as before.<br>**After step 5:** the page `Reflog` in the graph area (y 88 to 349), its source `Backups`, its rows the 50 backups newest first, the first `reset-20251220T090051000Z` (`2025-12-20 09:00:51`, `Before a reset`, `da31c97 Ours edits`), the last `reset-20251220T090002000Z`; the right panel as it was before the page opened (opening a page does not change it, 11.1); the log still under it, both rows closed, the focus inside `ActivityLog`. Nothing ran: no new operation row.<br>**After step 6:** the first Esc closed only the log (`EscapeOrder.ActivityLog` 340 comes before the page's 370): `ActivityLog` hidden, the splitter gone, the graph area's content again y 88 to 674; the toggle without `selected`, tooltip `Show every git command the app has run`; the page still open with its 50 rows, now reaching y 674; the keyboard focus out of the log and in no text box (on the page). The second Esc closed the page: the graph again as at the start, with the selection and the right panel it had before the page opened. No text in the window is cut except the trimmed texts the convention "Text that may be cut" names (`AssertNoClippedText`).<br>**After step 7:** once the merge has stopped (the wait for idle, before the row is opened): the merge stopped as "A2's merge stop" gives it: status `Merge stopped: 1 file has conflicts` (warning), the banner, display row 0 "Working changes · 1 conflict"; Undo and Redo disabled, `Finish or abort the merge first` (7.4's reason 2: no operation runs now). The log: `Clean up backups` closed, then `[IconAlert, #E0A23B] 09:00:00 · Merge feature/theirs · Merge stopped: 1 file has conflicts · 0 ms` (the message in `#E0A23B`). Opened: no summary (a stop is no failure: the record's `FailureSummary` is empty), no note; its calls, among them `exit 1 · git merge --no-edit --no-log -m "Merge branch 'feature/theirs'" refs/heads/feature/theirs · 0 ms` with `exit 1` in `#E5606B`. A merge is recorded (7.2), so 7.7's reads join its calls; on `e33f846`, without them, it made ten (information only): `git status --porcelain=v2 -z --untracked-files=all --renames`, `git symbolic-ref -q HEAD`, `git rev-parse -q --verify HEAD`, `git rev-parse --verify --quiet refs/heads/feature/theirs^{commit}`, `git rev-parse --verify --quiet HEAD`, `git ls-files --unmerged -z`, `git fmt-merge-msg`, the merge, `git ls-files --unmerged -z`, `git status --porcelain=v2 -z --untracked-files=all --renames`. The merge call open: a box with one part titled `Output` holding git's three lines, `Auto-merging notes.txt`, `CONFLICT (content): Merge conflict in notes.txt`, `Automatic merge failed; fix conflicts and then commit the result.` (word for word on Git 2.36; on another git the `CONFLICT (content): Merge conflict in notes.txt` line and that the box shows the call's own record), and no `Input`, `Error` or `No output` part: git wrote nothing to standard error (measured on 2.36.0 and 2.55.0). The list stays at its end as rows open (decision AD15), so the merge call's row and its box are whole in the picture. Closed again: two closed rows.<br>**After step 8:** the merge tool runs: status `Waiting for slow to close…`, the indeterminate progress bar, Cancel shown with the tooltip `Stop git and the merge tool`; the banner; display row 0 "Working changes · 1 conflict" selected, the stage panel as "A2's merge stop" gives it. Undo and Redo disabled, `Wait for Merge tool for notes.txt to finish`: 7.4's reason 1, which comes before reason 2, because the merge tool is a user operation of the repository (its record has `IsBackground` false; only a background operation gives no reason). The log: `Clean up backups` and `Merge feature/theirs` closed, then the running row `Merge tool for notes.txt` with no outcome glyph, the message `Running…` and no duration (the queue raised `IOperationLog.Started` for it; it has no record yet).<br>**After step 9:** the operation ended within the bound (in the writer's eight runs on `6f77e57`, idle 0.6 to 2.0 s after the click); no dialog (no lock file was left); status `Merge tool for notes.txt cancelled` (secondary); Cancel gone; Undo and Redo back to `Finish or abort the merge first`: the merge tool records nothing (7.2). Git: `UU notes.txt` and ` M other.txt`, notes.txt byte for byte as the merge left it (the app put back git's copy), no `notes_BASE_*`, `notes_LOCAL_*`, `notes_REMOTE_*` or `notes_BACKUP_*` file, `MERGE_HEAD` still `5c77ffb…`. The row `[IconMinus, #9BA3B0] 09:00:00 · Merge tool for notes.txt · Merge tool for notes.txt cancelled · 0 ms`, open: no summary, no note, its calls exactly (the merge tool is not recorded, so no read of 7.7 joins them): `exit 0 · git ls-files --unmerged -z -- notes.txt · 0 ms`, `cancelled · git -c mergetool.keepBackup=false mergetool --tool=slow --no-prompt -- notes.txt · 0 ms` and `exit 0 · git ls-files --unmerged -z -- notes.txt · 0 ms` again.<br>**After step 10:** the question was `Abort the merge?` / `Abort merging "Merge branch 'feature/theirs'"? Changes made to resolve conflicts are lost.` / `A backup is saved first, so this can be undone.` / `Cancel`, `Abort merge` (danger). Status `Aborted the merge`; the banner gone; the graph as at the start (display row 0 "Working changes"); Undo and Redo `Nothing to undo`, `Nothing to redo` (the abort removed the merge's pending entry and records none, D165). Git as "A2's backups" gives it after the abort: `abort-20260102T090001000Z` and its `-changes`, 50 backups, ` M other.txt`. The log: `Clean up backups`, `Merge feature/theirs` and `Merge tool for notes.txt` closed, then the new last row `[IconCheck, #3FB97F] 09:00:00 · Abort the merge · Aborted the merge · 0 ms`, open: no summary; one note, `Removed backup reset-20251220T090002000Z (more than 50 backups)`; its calls, among them in this order three calls marked `(on a temporary index)` (`git read-tree da31c977c80e9becdc908c74d973669aad1b3b8d (on a temporary index)`, `git add --all --force --pathspec-from-file=- --pathspec-file-nul (on a temporary index)`, `git write-tree (on a temporary index)`), `git commit-tree <tree> -p da31c977c80e9becdc908c74d973669aad1b3b8d`, `git update-ref --stdin` (the backup), `git merge --abort`, `git for-each-ref "--format=%(refname) %(objectname)" refs/visualcommit/backup/` and `git update-ref --stdin` (the clean-up), every one `exit 0` (on `e33f846` eleven calls: these eight after `git rev-parse --verify --quiet HEAD`, `git status --porcelain=v2 -z --untracked-files=all --renames` and `git rev-parse --verify --quiet HEAD`). The list stays at its end as the row opens (decision AD15): the abort's last call in view; with its eleven calls (`e33f846`'s eleven: whether the merge ended is read from the git folder, without a `git status` after `git merge --abort`) the row `Abort the merge` is the first row in view, whole, its note under it.<br>**After step 11:** the backup's update-ref open: one part titled `Input`, two lines, `create refs/visualcommit/backup/abort-20260102T090001000Z da31c977c80e9becdc908c74d973669aad1b3b8d` and `create refs/visualcommit/backup/abort-20260102T090001000Z-changes <id>`, `<id>` being what `git rev-parse refs/visualcommit/backup/abort-20260102T090001000Z-changes` prints (the snapshot is asserted by parent and tree, never by id); no `Output`, `Error` or `No output`. The list stays at its end: the call row and its box in view. No text in the window is cut except the trimmed texts the convention "Text that may be cut" names (`AssertNoClippedText`). | Not run yet | RW: every step of both methods with the real mouse and keyboard (the toggle, the rows, the left panel's double-click, the menus, the two Left presses of step 7, Cancel, `Show reads`, the section menu, two real Esc presses, the banner and the question); the rows found by automation id and name, the boxes read through their Value pattern; captures after steps 1, 2, 3, 5, 6, 7, 9, 10 and 11 compared with `a2a-log-open`, `a2b-clean-up-opened`, `a2c-checkout-failed-opened`, `a2e-page-and-log`, `a2f-log-closed-page-kept`, `a2g-merge-output-opened`, `a2i-merge-tool-cancelled`, `a2j-abort-opened`, `a2k-abort-backup-input`. What only the real window shows: the panel and its rows as UI Automation exposes them (the pass's own record of names), a real Esc's routing with the focus in the log, a real click on Cancel while the tool runs, and the frozen times of a real process (`VISUALCOMMIT_RECORD_CLOCK`). Steps 4 and 8 are not captured there: their pictures vary by nature | `a2a-log-open`, `a2b-clean-up-opened`, `a2c-checkout-failed-opened`, `a2d-show-reads`, `a2e-page-and-log`, `a2f-log-closed-page-kept`, `a2g-merge-output-opened`, `a2h-merge-tool-running`, `a2i-merge-tool-cancelled`, `a2j-abort-opened`, `a2k-abort-backup-input`; real window: the same but `a2d` and `a2h` | Design 1.2, 2.1 to 2.3, 5.1, 5.3, 7.2, 7.4, 9.3, 11.1, 12.3; D139, D156, D165, D173, D182. Two methods on fresh copies, so that neither comes near the hang watchdog's 3 minutes (decision AD7): the writer's single test of all these operations, without captures, `Show reads` or the page, took 32 to 49 s on a quiet machine and up to 2 min 20 s beside other runs. `a2d` varies by nature (the number of reads: refreshes the file watcher started), `a2h` by the indeterminate progress bar. The cancel of step 9: in the writer's trials on `e33f846`, 2 of 8 clicks on Cancel did not cancel; that was D182's fault (a tooltip that opened under the pointer took the press; fixed in `28716c4`, merged at `3dcba5d`), and on `6f77e57` 8 of 8 cancelled. The wait after the click is bounded (30 s), so a click that misses fails the step at once instead of waiting for the tool's own end (111 s measured). |
| A3 | The panel's height: the default, the splitter dragged, the height kept across a restart of the app, the splitter's limits; "history", dark, 1100×700 | A plain `[Fact]`, `Check_A3_panel_height`, two app starts on one data folder. First start: 1. "History" scenario. Start the app. Click `Activity log` in the status bar. Screenshot. 2. Press the left button at the splitter's centre (480, 471), move it to (480, 371) with the button held, in 5 equal steps, release. Screenshot. 3. Close the app (`ShellDriver.Dispose`). Second start, on the same data folder: 4. Start the app with `ShellDriver.Start(data.Path)`, writing no session (`RealApp.Launch(data)` in the real window), so that the session the first start saved restores the tab `history` and the height; wait for the repository (`WaitForRepositoryAsync`). Click `Activity log`. Screenshot. 5. Drag the splitter from its centre (480, 371) to (480, 71), release. Screenshot. 6. Drag the splitter from its centre (480, 250) to (480, 650), release. Screenshot. | **After step 1:** the panel at its default height, 200: y 474 to 674; the splitter y 469 to 474; the graph area's content y 88 to 469 (the column headers and H0's display rows 0 to 12 whole, 13 cut at y 469); header `Activity log`, `history`, `Show reads` unchecked, the close button; the list (y 504 to 674) empty, with no text: "history" had no operation and the clean-up found nothing to remove. `session.json`, read with `JsonSessionStore`, has `activityLogHeight` null (the default; the start wrote the key with a null value, as `JsonSessionStore` writes every property).<br>**After step 2:** the panel 300 high (±2): y 374 to 674; the splitter y 369 to 374; the graph area's content y 88 to 369. Letting go saved it: `session.json` holds `"activityLogHeight": 300` (±2), read with `JsonSessionStore`. The graph's rows are unchanged in place (row positions from the top did not move; only fewer show).<br>**After step 4:** the panel closed when the app starts (design 2.1): no panel, the graph area's content to y 674. After the click it opens 300 high (±2), y 374 to 674, as the session said; its list empty: the log holds the app session's records only (D173), and this session has run no operation.<br>**After step 5:** the panel at its maximum, 421 (±2): the graph area's height, 586, less 165, so that the content above the 5-px splitter keeps 160 (decision AD1, on 2.1's "less 160"): y 253 to 674, the splitter y 248 to 253, the graph area's content y 88 to 248 (the column headers and display rows 0 to 4 whole). `session.json` holds `"activityLogHeight": 421` (±2).<br>**After step 6:** the panel at its minimum, 120 (±2): y 554 to 674, the splitter y 549 to 554; the header and 90 px of list show. `session.json` holds `"activityLogHeight": 120` (±2). Nothing ran in git: `git status` as H0's. | Not run yet | RW: steps 1 to 6 in a real process: the splitter dragged with the real mouse (`RealApp.DragWithMouse`, the points scaled by the window's DPI), the app closed (`RealApp.Close`) and launched again on the same data folder (`RealApp.Launch`); the panel's bounds read through UI Automation (`ActivityLog`, ±2 logical px); captures after steps 1, 2, 4, 5 and 6 compared with `a3a-default-height`, `a3b-dragged-to-300`, `a3c-restarted`, `a3d-at-the-maximum`, `a3e-at-the-minimum`. What only the real window shows: a drag with real pointer capture across the splitter's grab area, and the height kept across a real restart of the process | `a3a-default-height`, `a3b-dragged-to-300`, `a3c-restarted`, `a3d-at-the-maximum`, `a3e-at-the-minimum`; real window: `a3a-default-height`, `a3b-dragged-to-300`, `a3c-restarted`, `a3d-at-the-maximum`, `a3e-at-the-minimum` | Design 2.1 (height 200, at least 120, at most the graph area's less 160; saved on release in `SessionState.ActivityLogHeight`; closed at start), D156. The maximum's 165 is decision AD1, the empty list's having no text decision AD2. |
| A4 | The keys: Ctrl+F opens the search bar; Ctrl+Z in the commit summary undoes typing only, also when it has nothing left to undo; Ctrl+Z in the graph undoes a commit, Ctrl+Y and Ctrl+Shift+Z redo it; in the resolver Ctrl+Z and Ctrl+Y are the resolver's, and Ctrl+F does nothing; "history" with an untracked file, and "conflicts" stopped in a merge, dark, 1100×700 | Method 1 (`Check_A4_keys`). 1. "History" scenario with `scratch.txt`. Start the app. Press Ctrl+F (`PressSearch()`). Screenshot. Press Esc. 2. Click the working-changes row (display row 0, (468, 129)). Click the Stage button (`RowStageButton`) of the unstaged `src/Reader.cs`; wait until idle. Type `Describe the lines` into the summary; click the commit button; wait until idle. 3. Click the working-changes row. Type `Next` into the summary (`TypeInto`). Press the platform's undo (`ShellDriver.Undo()`) until the summary is empty. Press it once more. Click the working-changes row. Screenshot. 4. Press Ctrl+Z (`PressGitUndo()`); wait until idle. Screenshot. 5. Press Ctrl+Y (`PressGitRedo(0)`); wait until idle. Screenshot. 6. Press Ctrl+Z; wait until idle. Press Ctrl+Shift+Z (`PressGitRedo(1)`); wait until idle. Method 2 (`Check_A4_keys_in_the_resolver`). 7. `ConflictsStoppedAsync(ConflictsStop.Merge)`. Start the app. Click the working-changes row; click the unstaged `notes.txt`; wait until the resolver shows it. Press Ctrl+F. 8. Click `Use ours` in the ours pane's bar of block 1. With the keyboard focus in the result (no click: the choice puts it there), press the platform's undo (`ShellDriver.Undo()`). Screenshot. Press the platform's redo (`ShellDriver.Redo()`). | **At the start of method 1:** H0 with display row 0 "Working changes" (` M src/Reader.cs`, `?? scratch.txt`); nothing selected; Undo `Nothing to undo`, Redo `Nothing to redo`, Search `Search commits (Ctrl+F)`.<br>**After step 1:** the search bar (`SearchBar`) under the toolbar, above the column headers, x 260 to 700, y 88 to 124, `VcChromeBackgroundBrush` (`#20242C`) with a 1-px `#323845` line along its bottom: the mode button `Commits`, the box `SearchBox`, empty, its placeholder `Message, author or SHA`, with the keyboard focus (its caret may be caught on or off: the picture varies there by nature); `SearchCount` empty; `SearchPrevious` and `SearchNext` disabled, their icons in `VcTextDisabledBrush` (#5F6775); `SearchClose`. The column headers moved down to y 124 to 152 and the rows by 36: the working-changes row's centre at y 165, commit row r's at 191 + 26 r. No row is marked or dimmed. The toolbar's Search stays enabled. Esc closed the bar: no `SearchBar`, the rows back at H0's places, the focus in the graph (`CommitGraphControl`), nothing selected (design 3.1).<br>**After step 2:** git as H-A1 and `?? scratch.txt` (`## main...origin/main [ahead 1]`, `?? scratch.txt`); the graph H-A1 with display row 0 "Working changes"; the stage panel `Unstaged files (1)` with A `scratch.txt`, `Staged files (0)` with "No staged changes", the summary and the description empty. Undo enabled, `Undo commit "Describe the lines"`.<br>**After step 3:** the summary read `Next`, then emptied by the box's own undo: one press in the scripted run (one text input), four in the real window (one per key); the extra press with the box empty did nothing. After every press: no record titled `Undo commit "Describe the lines"` in `IOperationLog`, no `git update-ref` call in `IGitCallLog` since the commit, `main` still `d1e23a3`, Undo still enabled with `Undo commit "Describe the lines"` (the window declines the key while a text box has the focus, design 1.3). The picture: the summary empty with its placeholder `Summary`, the working-changes row selected, the focus in the graph.<br>**After step 4:** one operation `Undo commit "Describe the lines"` (running text `Undoing commit "Describe the lines"…`, not pictured), no question, no dialog; status `Undid commit "Describe the lines"` (secondary). Among its calls `git update-ref -m "VisualCommit: undo commit \"Describe the lines\"" --stdin` with the input `update refs/heads/main c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9 d1e23a37b6b08f4dc9662fab930f232a66ecd8fc`, exit 0 (7.3's soft move, written in 7.5's one transaction). Git as H-A0u: `main` `c0bc2dd`, `M  src/Reader.cs`, `?? scratch.txt`; `d1e23a3` still exists (`git cat-file -e`). The graph H0 with the working-changes row, still selected; the stage panel `Unstaged files (1)` A `scratch.txt`, `Staged files (1)` M `src/Reader.cs`; the summary `Describe the lines` and the description empty (7.5: both were empty); the commit button `Commit 1 file`. Undo disabled, `Nothing to undo`; Redo enabled, `Redo commit "Describe the lines"`; the left panel's `main` without an arrow.<br>**After step 5:** one operation `Redo commit "Describe the lines"`; status `Redid commit "Describe the lines"`; among its calls `git update-ref -m "VisualCommit: redo commit \"Describe the lines\"" --stdin` with `update refs/heads/main d1e23a37b6b08f4dc9662fab930f232a66ecd8fc c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9`. Git as after step 2 (`main` `d1e23a3`, `?? scratch.txt`); HEAD's reflog newest first: `VisualCommit: redo commit "Describe the lines"`, `VisualCommit: undo commit "Describe the lines"`, `commit: Describe the lines`. The graph H-A1 with the working-changes row; the summary empty again (it still held the undone message); Undo `Undo commit "Describe the lines"`, Redo `Nothing to redo`.<br>**After step 6:** the Ctrl+Z gave step 4's state again and Ctrl+Shift+Z step 5's: four `VisualCommit:` entries in HEAD's reflog above `commit: Describe the lines` (redo, undo, redo, undo, newest first), `main` `d1e23a3`; every gesture of the platform's redo list redoes (1.3).<br>**After step 7:** GC-M, the banner; Undo and Redo disabled, `Finish or abort the merge first`; before the resolver opened Search was enabled (`Search commits (Ctrl+F)`), and with the resolver open it is disabled, `Close the conflict resolver first`; Ctrl+F opened nothing (no `SearchBar`), the resolver unchanged.<br>**After step 8:** `Use ours` gave phase 4's R1 (16 lines, `1 conflict left`) and moved the keyboard focus into the result (`ConflictResultEditor` has it, the caret at the start of the replaced text: phase 4's 11.5), which the step asserts before the undo, so that the key is pressed in the resolver's result as design 15.4 says; the undo, taken by the resolver at its root in the tunnel phase (phase 4's 11.5) before the window's bubbling handler, which declines anywhere in the resolver (1.3), gave FM back: 20 lines, both blocks, `2 conflicts left`, block 1's bars with their buttons again (pictured); the redo gave R1 again. No operation ran (no `Undo …` or `Redo …` record), and git is as at the start: `UU notes.txt`, notes.txt on disk still blob `cdd7b39`, `MERGE_HEAD` `5c77ffb…` (the resolver writes nothing before `Mark resolved`). | Not run yet | RW: method 1, steps 1 to 5 with real keys (Ctrl+F, Esc, the summary typed key by key and four real Ctrl+Z presses then one more, Ctrl+Z and Ctrl+Y with the focus in the graph); the states read through UI Automation as in the scripted run; captures compared with `a4a-search-by-key` (its caret may differ), `a4b-summary-typing-undone`, `a4c-undone-by-key`, `a4d-redone-by-key`. What only the real window shows: the operating system's key events through the window's tunnelling and bubbling handlers, and Avalonia's text box taking a real Ctrl+Z before the window. Step 6 and method 2 are scripted only: Ctrl+Shift+Z is the same handler as Ctrl+Y, and the resolver's own keys are phase 4's real-window C10 (`PressUndo`) | `a4a-search-by-key`, `a4b-summary-typing-undone`, `a4c-undone-by-key`, `a4d-redone-by-key`, `a4e-resolver-undo`; real window: `a4a-search-by-key`, `a4b-summary-typing-undone`, `a4c-undone-by-key`, `a4d-redone-by-key` | Design 1.3, 3.1, 7.3, 7.5, 7.6, 11.2; D148, D163; phase 4's 11.5. `a4a` varies by nature: the search box's caret may be caught on or off (`ShellDriver` holds only the resolver's caret still). The redo gestures are the headless platform's list as measured (Ctrl+Y, Ctrl+Shift+Z); a platform with another list gets every gesture of its own pressed. |
| B5 | File history across a rename, from the stage panel's file, from a commit's file and from a diff's menu: the rows with `renamed from`, `as` and `added`, a step's diff, the right panel unchanged until a row is clicked and then showing it, the list walked with Down, Esc on a diff over the page, `Show in graph`, Esc: the clicked commit revealed (with the activity log open, so that it lies out of view until then); then the side-by-side layout at 1920×1080 in the light theme; "history", dark, 1100×700 | `Check_B5_file_history`: 1. "History" scenario. Click the working-changes row (468, 129). 2. Right-click `src/Reader.cs` in `UnstagedFileList`. Screenshot. 3. Click "File history"; wait for 4 rows. Screenshot. 4. Click the row `0575c7f Rename the parser to reader` (row 1, (420, 163)). Screenshot. 5. Right-click `src/Reader.cs` in the details' `FileList`; click "File history"; wait for 3 rows. Screenshot. 6. Press Down, Down. Screenshot. Press Up. 7. Click `src/Parser.cs` in `FileList`. Press Esc. Click `src/Parser.cs` again; right-click the text of its new line 10 (`        _text = text ?? string.Empty;`, the first added line of `@@ -7,11 +7,16 @@`), at phase 3's `DiffTextPoint` of the row whose new number is 10 (the driver's `DiffRowOfLine(10)`; the removed old line 10 just above it has no new number): 240 right of the row's left edge, on its text, not on its line numbers. Screenshot. 8. Click "File history"; wait for 2 rows. Screenshot. 9. Right-click the row `1d5a7cf Add the parser` (row 1, (420, 163)). Screenshot. 10. Click "Show in graph". Screenshot. 11. Click `Activity log` (`ActivityLogToggle`) in the status bar. Click the working-changes row (468, 129); right-click `src/Reader.cs` in `UnstagedFileList`; click "File history"; wait for 4 rows; click the row `1d5a7cf Add the parser` (row 3, (420, 215)); press Esc. Screenshot.<br>`Check_B5_side_by_side_light` (1920×1080, light): 12. Click the graph row "Read whole lines" (468, 155). Right-click `src/Reader.cs` in `FileList`; click "File history"; wait for 4 rows; click the row `0575c7f Rename the parser to reader` (420, 163). Screenshot. | At the start: H0, nothing selected, the details placeholder.<br>**After step 1:** the working-changes row selected; the stage panel: `Unstaged files (1)` with `M` `Reader.cs` `src`, `Staged files (0)` with "No staged changes".<br>**After step 2:** the menu, 293 high (9 items, 3 separators): header `Reader.cs`; "Stage" (highlighted); "Discard changes…"; —; "Ignore Reader.cs" (disabled: "Only untracked files can be ignored"); "Ignore all *.cs files" (disabled: "Only untracked files can be ignored"); —; "File history"; "Blame"; —; "Open file"; "Show in folder"; "Copy path".<br>**After step 3:** no menu; the page `FileHistory` (pane `History of Reader.cs`) in the graph area (x 260 to 700, y 88 to 674; no column headers): header `IconHistory`, `History of Reader.cs`, `src` (secondary), no `from` text, `Blame`, close; the list (y 124 to 344, decision BD2) with the rows of "The file histories" for src/Reader.cs from HEAD: row 0 (y 137) `M` (`#E0A23B`) `c0bc2dd` "Read whole lines" and no secondary text; row 1 (163) `R` (`#7C8CFF`) `0575c7f` "Rename the parser to reader" `renamed from src/Parser.cs`; row 2 (189) `M` `365482b` "Handle empty input" `as src/Parser.cs`; row 3 (215) `A` (`#3FB97F`) `1d5a7cf` "Add the parser" `added`; their `FileHistoryRow` names `c0bc2dd Read whole lines` … `1d5a7cf Add the parser`; the author and date columns hidden at 440 (decision BD2; the surface holds Jonas Berg 2026-01-01 12:13, Mira Kovac 12:06, Jonas Berg 12:03, Mira Kovac 12:01). Row 0 selected (`#2A3150`) with the keyboard focus. The diff part (`FileHistoryDiff`, y 344 to 674): phase 2's header `M` `Reader.cs` `src`, side label `c0bc2dd`, no close button; Inline (selected) and Side by side; no action buttons; `c0bc2dd`'s hunk of "Each row's diff", from its header row down. The right panel is still step 1's stage panel and the working-changes row stays selected behind the page. Git unchanged: `git status --porcelain` prints ` M src/Reader.cs`.<br>**After step 4:** row 1 selected with the focus; the diff part: `R` `Reader.cs` `src` `renamed from src/Parser.cs`, side label `0575c7f`, the rename's hunk `@@ -1,11 +1,11 @@`. The right panel shows `0575c7f` (Mira Kovac <mira@example.com>, 2026-01-01 12:06, Parents `14a5c6c`, `Changed files (1)`: `R` `Reader.cs` `src` `renamed from src/Parser.cs`); the working-changes row is no longer selected; behind the page the graph has row 7 selected.<br>**After step 5:** the menu was 171 high: header `Reader.cs`; "File history" (highlighted); "Blame"; —; "Open file"; "Show in folder"; "Copy path". The new page: `History of Reader.cs`, `src`, `from 0575c7f`; 3 rows: `R` `0575c7f` (`renamed from src/Parser.cs`), `M` `365482b` (`as src/Parser.cs`), `A` `1d5a7cf` (`added`); row 0 selected with the focus; the diff part the rename's; the right panel still `0575c7f`.<br>**After step 6:** the first Down showed `365482b` in the right panel and the diff part; after the second, row 2 is selected: the diff part `A` `Parser.cs` `src`, side label `1d5a7cf`, hunk `@@ -0,0 +1,17 @@` with 17 added rows from `namespace History;`; the right panel shows `1d5a7cf` (Mira Kovac, 2026-01-01 12:01, Parents `7829335`, `Changed files (1)`: `A` `Parser.cs` `src`); the graph behind has row 12. After Up: row 1 (`365482b`) selected; the right panel `365482b`: "Handle empty input", body `The tokenizer stops at an empty text now.`, `Jonas Berg <jonas@example.com>`, 2026-01-01 12:03, Parents `775113f`, `Changed files (1)`: `M` `Parser.cs` `src`; the graph behind has row 10.<br>**After step 7:** the first click opened phase 2's diff over the page (`DiffView`: `M` `Parser.cs` `src`, side label `365482b`, `CloseDiffButton`, `365482b`'s hunk `@@ -7,11 +7,16 @@ public sealed class Parser`), the file row selected; Esc closed only the diff: the page as after step 6, row 1 selected, the focus in its list (11.1). After the second click and the right-click: the menu, 171 high: header `Parser.cs`; "File history" (highlighted); "Blame"; —; "Open file" (disabled: "The file is not in the working tree"); "Show in folder"; "Copy path".<br>**After step 8:** the diff closed; the page `History of Parser.cs`, `src`, `from 365482b`: row 0 `M` `365482b` "Handle empty input" (no secondary text) selected with the focus, row 1 `A` `1d5a7cf` "Add the parser" `added`; the diff part `365482b`'s; the right panel still `365482b`.<br>**After step 9:** row 1 selected (the right panel `1d5a7cf`, the diff part its diff); the menu, 119 high, its top-left at (422, 165): header `1d5a7cf Add the parser`; "Show in graph" (highlighted); "Blame at this commit"; —; "Copy SHA".<br>**After step 10:** no menu, no page: the graph H0 with row 12 "Add the parser" (y 467) selected and wholly in view; the right panel `1d5a7cf`; the focus on the graph. Every row of H0 fits at full height, so "in view" cannot fail here (`Select(sha, reveal: true)` is phase 3's, pinned by `RepositoryViewModelTests`); step 11 is the one that needs a reveal.<br>**After step 11:** the activity log open at its default height (part A's A3): the panel y 474 to 674, the splitter y 469 to 474, its list empty ("history" ran no operation and its clean-up removed nothing); the graph area's content y 88 to 469, where H0's display rows 0 to 12 are whole and display row 13, "Add the parser", is cut at y 469 (it lies at y 454 to 480). The page of step 3 opened again in that space: x 260 to 700, y 88 to 469, the header y 88 to 124, the list y 124 to 262 (40 % of 345, decision BD2), the diff part y 262 to 469; 4 rows, row 0 selected. The click on row 3 selected it and showed `1d5a7cf` in the right panel (graph row 12 selected behind the page). Esc closed the page only (the focus was on the page's row, so the log's handler declined, design 2.1). No page: H0 with row 12 "Add the parser" selected and revealed (11.1's "Revealing on the way back"): `CommitGraphControl.Reveal` centres a row as far as the end of the history allows, so the graph is scrolled to its end, offset 37 (15 display rows × 26 − the view's 353): display row r's centre at y 92 + 26 r, "Add the parser" at y 417 to 443 (centre 430), wholly in view, "Initial commit" at y 443 to 469, the working-changes row and half of "Read whole lines" above the view's top (y 116). Without the reveal "Add the parser" would stay cut at y 469. The right panel `1d5a7cf` (not the stage panel); the focus on the graph; the log still open. Git unchanged throughout: refs as built, ` M src/Reader.cs`, nothing under `refs/visualcommit/`; no operation ran (no log line `history: …: Succeeded`).<br>**After step 12** (1920×1080, light): the page fills x 260 to 1520, y 88 to 1054: the header `IconHistory`, `History of Reader.cs`, `src` (secondary) and no `from` text, because its start, `c0bc2dd`'s full SHA, is HEAD's commit (decision BD24), then `Blame` and close; the list on the left (x 260 to 827, 45 %), the diff part on the right (x 827 to 1520), both from y 124 down (decision BD2); the rows as step 3's with the author shown and the date hidden (the list is 567 wide): row 1 `R` (`#4353D8`) `0575c7f` "Rename the parser to reader" `renamed from src/Parser.cs` "Mira Kovac", selected (`#DDE1FA`); the diff part the rename's diff (side label `0575c7f`); the right panel `0575c7f`; the light theme's colours throughout. | Not run yet | RW: method 1's steps 1 to 4 at 1100×700 in one launch: the stage panel's row by `StageFileRow`, the menu items by name, the page's rows by their `FileHistoryRow` names and row 1 clicked with the real mouse at its point; the captures compared with `b5b-history` and `b5c-rename-row`; the texts by automation id (`FileHistoryTitle`, the hosted diff's `DiffSideLabel` inside `FileHistory`). What only the real window shows: the new page and its hosted diff in the real window and scaling, and the focus after a real click. Method 2 is scripted only: 1920×1080 does not fit the development machine's screen (`FullSize`) | `b5a-stage-file-menu`, `b5b-history`, `b5c-rename-row`, `b5d-from-commit`, `b5e-walked`, `b5f-diff-menu`, `b5g-from-diff`, `b5h-row-menu`, `b5i-shown-in-graph`, `b5j-back-to-graph`, `b5k-side-by-side-light`; real window: `b5b-history`, `b5c-rename-row` | Design 4 (its introduction), 4.1, 4.4, 11.1, 11.3, 13.1, 13.4, 2.1 (the log's Esc); D158, D175, D176. Decisions BD1, BD2, BD8, BD9, BD10, BD13, BD24. Step 11 opens the activity log at its default height (part A's A3) so that the commit picked on the page lies out of view when the page closes: in the full graph area every row of H0 fits, and a missing reveal could not be seen. The history's commands from 0.1 as WP1 built them (`-c core.quotePath=true`, no `-z`). Opening a page from the stage panel keeps the stage panel (4.1's first row "fills only the diff part"); a key on a row is the user's and changes the right panel (4.1). The diff over the page uses phase 2's names; the driver looks for the main diff first (11.1's tree order). `ForStagedFile`'s items are proven by `RepositoryMenusTests` (no part B check stages a file). |
| B6 | Blame: the gutter's runs, dates and backgrounds, `Not committed yet` lines and their menu, a run's tooltip, a click on a run in the gutter and on its text (the right panel and the graph behind), `Blame before this commit` across the rename and its disabled reason on the first commit, `File history` and the history's `Blame`, a double-click on a run (the graph, the commit revealed); "history", dark, 1100×700 | `Check_B6_blame`: 1. "History" scenario. Click the working-changes row (468, 129); right-click `src/Reader.cs` in `UnstagedFileList`; click "Blame"; wait for 24 lines. Screenshot. 2. Rest the pointer on line 1 in the gutter (300, 131.9). Screenshot. 3. Rest the pointer on line 22 in the gutter (300, 464.6). Right-click line 3 in the gutter (300, 163.6); press Esc. 4. Click line 13 in the gutter (300, 322.0). Screenshot. 5. Click line 10, column 9 of the text (480.0, 274.5). 6. Right-click line 4 in the gutter (300, 179.4). Screenshot. 7. Click "Blame before this commit"; wait for 22 lines. Screenshot. 8. Right-click line 1 in the gutter (300, 131.9); rest the pointer on "Blame before this commit". Screenshot. Press Esc. 9. Click `File history` (`BlameHistory`); wait for 2 rows. Screenshot. 10. Click `Blame` (`FileHistoryBlame`); wait for 22 lines. 11. Double-click line 20 in the gutter (300, 432.9). Screenshot. | **After step 1:** the page `Blame` (pane `Blame of Reader.cs`): header `IconBlame`, `Blame of Reader.cs` (`BlameTitle`), `in the working tree` (secondary), `File history` (`BlameHistory`), close; the gutter 130 wide (x 260 to 390, the page is under 700 wide) and the text (x 390 to 700) from y 124: `BlameText`'s value is the working tree's src/Reader.cs exactly (24 lines, as "The blames" gives them), line numbers 1 to 24, the C# colours, no caret, lines longer than the text's width cut at its edge; the gutter's 15 runs of "The blames" (working tree): each run's first line shows its short SHA in the code font and `2026-01-01` (no author at this width, design 4.2), both whole, laid out as decision BD12 gives, its other lines nothing; the first lines of runs 1 (line 3) and 13 (lines 22–23) show `Not committed yet` in `#E0A23B` with no SHA or date, and line 23 shows nothing, as every run's other lines (decision BD12); backgrounds alternate `#1A1D24` (runs 0, 2, …, 14) and `#14161B` (runs 1, 3, …, 13). The keyboard focus in the text. The right panel is still the stage panel (opening a page changes nothing there).<br>**After step 2:** the tooltip, five lines: `1d5a7cf999727e7f724054348c81866a62025184`, `Mira Kovac <mira@example.com>`, `2026-01-01 12:01`, `Add the parser`, `as src/Parser.cs` (decision BD11).<br>**After step 3:** the tooltip on line 22 reads `Not committed yet` alone (no date: git gives the wall clock there, decision BD11). The menu on line 3, 58 high: header `Not committed yet`; "Show in graph" (highlighted); the right-click selected the working-changes row, so the stage panel still shows.<br>**After step 4:** the right panel shows `c0bc2dd` "Read whole lines" (`Jonas Berg <jonas@example.com>`, 2026-01-01 12:13, Parents `20d7fbe`, `Changed files (1)`: `M` `Reader.cs` `src`); the working-changes row is no longer selected; behind the page the graph has row 0 selected; line 13's gutter has `#2A3150` (the clicked run's lines, design 4.2), the other runs keep their backgrounds.<br>**After step 5:** the right panel shows `365482b` "Handle empty input"; line 10's gutter has the selection background, line 13's its own again; the graph behind has row 10.<br>**After step 6:** the right panel `0575c7f` (the right-click selected the run; graph row 7 behind); the menu, 154 high, its top-left at (302, 181.4): header `0575c7f Rename the parser to reader`; "Show in graph" (highlighted); —; "Blame before this commit"; "File history"; —; "Copy SHA".<br>**After step 7:** a new page: `Blame of Parser.cs`, `at 14a5c6c Add tracing`; 22 lines, src/Parser.cs at `14a5c6c` (line 4 `public sealed class Parser`, line 8 `    public Parser(string text)`, line 13 `    public string[] Words()`, line 20 `        return _text.Split(' ');`); 5 runs: 1–9 `1d5a7cf` (`#1A1D24`), 10 `365482b` (`#14161B`), 11–14 `1d5a7cf`, 15–19 `365482b`, 20–22 `1d5a7cf`; the right panel still `0575c7f`.<br>**After step 8:** the right panel `1d5a7cf` (graph row 12 behind); the menu: header `1d5a7cf Add the parser`; "Show in graph" (highlighted); —; "Blame before this commit" (disabled: "This is the file's first commit"), its tooltip shown; "File history"; —; "Copy SHA".<br>**After step 9:** the page `History of Parser.cs`, `src`, `from 14a5c6c`: row 0 `M` `365482b` "Handle empty input" selected with the focus, row 1 `A` `1d5a7cf` "Add the parser" `added`; the diff part `365482b`'s; the right panel still `1d5a7cf`.<br>**After step 10:** `Blame of Parser.cs`, `at 365482b Handle empty input`; 22 lines and the 5 runs of step 7.<br>**After step 11:** no page: the graph H0 with row 12 "Add the parser" (y 467) selected and wholly in view (every row of H0 fits at full height, so "in view" cannot fail here; B5's step 11 is the check that needs a reveal); the right panel `1d5a7cf`; the focus on the graph. Git unchanged throughout (` M src/Reader.cs`, refs as built); no operation ran. | Not run yet | RW: steps 1, 4, 6, 7, 9, 10 and 11 in one launch: the blame opened from the stage panel's menu, the capture compared with `b6a-blame-working-tree`; line 13 clicked with the real mouse at the gutter's point (from `BlameGutter`'s bounding rectangle), compared with `b6c-run-clicked`; `BlameText`'s Value read through UI Automation; the run menu's items, `BlameHistory` and `FileHistoryBlame` clicked by name and id; then **a real double-click** on line 20 in the gutter, the capture compared with `b6h-shown-in-graph`. Only the real window shows that a real double-click on the gutter (two presses from the OS with its own double-click time) does `Show in graph` once and closes the page | `b6a-blame-working-tree`, `b6b-run-tooltip`, `b6c-run-clicked`, `b6d-run-menu`, `b6e-blame-before`, `b6f-first-commit`, `b6g-file-history`, `b6h-shown-in-graph`; real window: `b6a-blame-working-tree`, `b6c-run-clicked`, `b6h-shown-in-graph` | Design 4.2, 4.3, 4.4, 11.1, 11.3, 13.4; D158, D175, D176. Decisions BD1, BD6, BD8, BD9, BD10, BD11, BD12. The blame command and porcelain of 0.1. Lines not committed yet carry the wall clock in git's porcelain (scenario facts 7, item 1), so neither the gutter nor the tooltip shows their date. "Blame before this commit" follows `previous` across the rename (`0575c7f` → `14a5c6c` src/Parser.cs). |
| B7 | "All files": the details panel's button, an unchanged file's blame in one click and through its menu in two, a line traced to its commit, a changed file still opening its diff, and the setting kept when another commit is shown and after a restart; "history", dark, 1100×700 | `Check_B7_all_files`: 1. "History" scenario. Click the graph row "Read whole lines" (468, 155). 2. Click `All files` (`AllFilesButton`). Screenshot. 3. Click `LICENSE` in `FileList`; wait for 6 lines. Screenshot. 4. Click line 3 in the gutter (300, 163.6). Screenshot. 5. Press Esc. Click the graph row "Read whole lines" (468, 155). 6. Right-click `README.md` in `FileList`. Screenshot. 7. Click "Blame"; wait for 5 lines. Screenshot. 8. Click line 5, column 5 of the text (451.2, 195.3). Screenshot. 9. Press Esc. Click `README.md` in `FileList`. Press Esc.<br>`Check_B7_all_files_kept_after_a_restart`: 10. First app instance: click the graph row "Read whole lines"; click `All files`; close the app. 11. Second instance on the same data folder: click the graph row "Read whole lines". Screenshot. Click `All files`. | **After step 1:** the right panel shows `c0bc2dd` "Read whole lines"; `Changed files (1)`: `M` `Reader.cs` `src`; right of the title the buttons `Flat` (selected), `Tree` and `All files` (not selected), in that order (decision BD14).<br>**After step 2:** `All files` has the `selected` class; the title reads `All files (6)`; the flat list in path order: `LICENSE`, `README.md`, `logo.png` `assets`, `notes.md` `docs`, `Config.cs` `src` (no status letter), `M` `Reader.cs` `src`; `settings.json` in the data folder has `"showAllFiles": true` (the store writes camelCase, as phase 2's `fileList`; read back through `JsonSettingsStore` as `AppSettings.ShowAllFiles`); git unchanged.<br>**After step 3:** the page `Blame of LICENSE`, `at c0bc2dd Read whole lines` (the caption drawn cut with "…" at this width, its whole text in its tooltip: part B's convention "Text that may be cut"; so in `b7b`, `b7c`, `b7e` and `b7f`): the 6 lines of LICENSE, one run (lines 1–6) `7829335` `2026-01-01` on `#1A1D24`; the row `LICENSE` has the selection background in the list (decision BD14); the right panel still `c0bc2dd`. No diff opened.<br>**After step 4:** the right panel shows `7829335` "Initial commit" (`Test Author <author@example.com>`, 2026-01-01 12:00, no parent) with `All files (3)`: `A` `LICENSE`, `A` `README.md`, `A` `notes.md` `docs` (all files are changed in the root commit); the graph behind has row 13; lines 1–6 of the gutter have `#2A3150`.<br>**After step 5:** the page closed; row 13 was revealed and selected; then row 0 selected again: `c0bc2dd` with `All files (6)` (the setting stays on).<br>**After step 6:** the menu, 171 high: header `README.md`; "File history" (highlighted); "Blame"; —; "Open file"; "Show in folder"; "Copy path" (every row's menu is `ForCommitFile`, 4.5).<br>**After step 7:** `Blame of README.md`, `at c0bc2dd Read whole lines` (the caption cut as after step 3): lines `# History`, empty, `A repository for the history checks.`, empty, `The reader splits a text into lines.`; runs: 1–3 `7829335` `2026-01-01` (`#1A1D24`), 4–5 `c1b6375` `2026-01-01` (`#14161B`).<br>**After step 8:** the right panel shows `c1b6375` "Explain the reader" (`Jonas Berg <jonas@example.com>`, 2026-01-01 12:09, Parents `884b462`) with `All files (5)`: `LICENSE`, `M` `README.md`, `logo.png` `assets`, `Config.cs` `src`, `Reader.cs` `src`; the graph behind has row 4; lines 4–5 of the gutter have `#2A3150`. The line's commit was reached in two clicks from the file's row (plan.md's "Done when").<br>**After step 9:** after Esc the graph with row 4 "Explain the reader" (y 259) selected and in view; the click on the changed `README.md` opens phase 2's diff (D63: `M` `README.md`, side label `c1b6375`, one hunk `@@ -1,3 +1,5 @@`), not a blame; Esc closes it.<br>**After step 11:** the second instance shows `c0bc2dd` with `All files (6)` at once and `All files` selected; after the click: `Changed files (1)` and `"showAllFiles": false` in `settings.json`. Git unchanged throughout. | Not run yet | RW: `Check_B7_all_files`'s steps 1 to 3 and 5 to 8 in one launch: `AllFilesButton` clicked by id, the file rows clicked by their `ChangedFile` names inside `FileList`, the menu item by name, line 5 clicked with the real mouse at its point from `BlameText`'s bounding rectangle; the captures compared with `b7a-all-files`, `b7b-license-blame`, `b7e-readme-blame` and `b7f-traced`. The restart stays scripted: the setting is phase 1's `settings.json` (a restart of the real process is pictured by A3 and F14) | `b7a-all-files`, `b7b-license-blame`, `b7c-initial-commit`, `b7d-file-menu`, `b7e-readme-blame`, `b7f-traced`, `b7g-after-restart`; real window: `b7a-all-files`, `b7b-license-blame`, `b7e-readme-blame`, `b7f-traced` | Design 4.2, 4.4, 4.5, 11.1, 11.3; D164 (the switch, "two clicks"), D175, D32 (the restart). Decisions BD1, BD9, BD14. A stash's details and the comparison's list have no "All files" (4.5): no stash in "history", so `CommitDetailsViewModelTests` prove it. |
| B8 | The reflog from the Local branches section's menu: HEAD's with the lost marks and the gap line, the lost mark's tooltip, a lost commit in the right panel and its entry menu, `Create branch here…` from it (the branch in the graph, the mark gone), the source menu, a branch's reflog by the source menu and by `Show reflog of {b}`; Esc: the commit in view; "reflog", dark, 1100×700 | `Check_B8_reflog_and_a_branch_from_a_lost_commit`: 1. "Reflog" scenario; wait for the clean-up. Right-click the section header "Local branches" in the left panel. Screenshot. 2. Click "Show reflog"; wait for 36 rows and their lost marks. Screenshot. 3. Rest the pointer on the lost mark of `HEAD@{1}` (682, 163). Screenshot. 4. Click the row `HEAD@{9}` (420, 345). 5. Right-click the row `HEAD@{9}`; rest the pointer on "Show in graph". Screenshot. 6. Click "Create branch here…". Screenshot. 7. Type `shortcut`; click "Check out the new branch"; click "Create branch"; wait for the log line and the page's next read. Screenshot. 8. Click the row `HEAD@{9}` (420, 345). 9. Click `ReflogSource`. Screenshot. Click "main"; wait for 8 rows. Screenshot. 10. Right-click `topic` under Local branches; click "Show reflog of topic"; wait for 3 rows. Screenshot. 11. Press Esc. Screenshot. | At the start: RL0, nothing selected, the details placeholder; 15 backups left (the clean-up's two `Removed backup` lines in the log).<br>**After step 1:** the menu, 162 high (5 items, no separator): header `Local branches`; "Create branch…" (highlighted); "Show reflog"; "Show backups"; "Show all hidden branches" (disabled: "No branch is hidden"); "Stop soloing" (disabled: "No branch is soloed").<br>**After step 2:** no menu; the page `Reflog` (pane `Reflog of HEAD`): header with `ReflogSource` labelled `HEAD` and a chevron, and `ReflogClose` (decision BD6); the list (y 124 to 648) with the 36 rows of "HEAD's reflog", newest first, their `ReflogRow` names `HEAD@{0}` … `HEAD@{36}` (no `HEAD@{5}`); at 440 the columns (decision BD3) selector (code font, secondary, x 268 to 340), action (x 348 to 496, cut with "…" where it does not fit, its whole text as tooltip), commit (short SHA in the code font and the subject, x 504 to 664) and the lost mark (x 672 to 692); the time column hidden; rows 0 to 19 (`HEAD@{0}` to `HEAD@{20}`) wholly in view; `IconAlert` in `#E0A23B` on `HEAD@{1}` (y 163), `HEAD@{9}` (y 345), `HEAD@{11}` (y 397) and `HEAD@{17}` (y 553) and, below the view, `HEAD@{23}` and `HEAD@{33}`, on no other row; the footer `ReflogFooter` (y 648 to 674, decision BD5): `1 entry is not shown: it names no commit that exists`. No row selected; the list has the keyboard focus; the right panel still the placeholder. The call log has `git log --walk-reflogs --no-show-signature --no-color --encoding=UTF-8 --ignore-missing -z -n 501 --format=%gD%x1f%H%x1f%P%x1f%gs%x1f%s HEAD --`, the same walk with `--date=raw --format=%gD%x1f%H`, and `git rev-list --ignore-missing --stdin --not --branches --tags --remotes HEAD --` (5.2). Git unchanged.<br>**After step 3:** the tooltip `No branch, tag or remote branch reaches this commit`.<br>**After step 4:** `HEAD@{9}` selected; the right panel shows `d07c677` "Try a shortcut" (`Test Author <author@example.com>`, 2026-01-01 12:23, Parents `9dd61fd`, `Changed files (1)`: `A` `shortcut.txt` `src`) directly, the graph having no such row: nothing is selected in the graph (11.3).<br>**After step 5:** the menu, 206 high (decision BD8): header `d07c677 Try a shortcut`; "Create branch here…" (highlighted); "Check out this commit"; —; "Reset main to this commit…"; "Cherry-pick onto main"; —; "Show in graph" (disabled: "This commit is not in the graph"), its tooltip shown; "Copy SHA".<br>**After step 6:** phase 3's name dialog: `Create branch`, `From d07c677 "Try a shortcut"`, `NameBox` empty with the focus, `Check out the new branch` ticked, `Create branch` disabled.<br>**After step 7:** no dialog; the log has `reflog: Create branch shortcut: Succeeded after <N> ms: Created shortcut`; the status bar `Created shortcut`; Undo's tooltip `Undo creation of shortcut`. Git: the app ran `git branch shortcut d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f`; `refs/heads/shortcut` → d07c677…, its reflog `shortcut@{0}` `branch: Created from d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f`; HEAD still on `main` (`## main...origin/main [ahead 2]`), HEAD's reflog still 36 entries; the graph behind is RL-B8; the left panel Local branches `5` (▸ `feature`, `clash`, `main`, `shortcut`, `topic`). The page read again: the same 36 rows; the lost mark gone from `HEAD@{9}`; marks on `HEAD@{1}`, `HEAD@{11}`, `HEAD@{17}`, `HEAD@{23}`, `HEAD@{33}`; the footer unchanged.<br>**After step 8:** the graph has `d07c677` now: row 1 is selected behind the page; the right panel `d07c677`.<br>**After step 9:** the source menu, 223 high, its top-left at `ReflogSource`'s bottom-left corner (decision BD7): header `Reflog and backups`; "HEAD" (highlighted); "clash"; "feature/renamed"; "main"; "shortcut"; "topic"; —; "Backups". Then the page `Reflog of main` (`ReflogSource` reads `main`): 8 rows `main@{0}` … `main@{7}` as "Branch reflogs" gives them, the lost mark on `main@{3}` only (`main@{1}`'s `d07c677` is `shortcut`'s now), no footer; the right panel unchanged.<br>**After step 10:** the branch menu holds "Show reflog of topic" (enabled, in phase 5's group before "Copy branch name", 13.1; the menu's other items are phase 4's and part F's); the page `Reflog of topic`: `topic@{0}` (rebase (finish): refs/heads/topic onto 316166adc8e550ebb513a7f8c0ea99ecbdd88983, `362fbd6` Topic work), `topic@{1}` (commit: Topic work, `f7a69be` Topic work, lost), `topic@{2}` (branch: Created from HEAD, `7dd8e8f` Add the app); the right-click selected nothing in the left panel (D114).<br>**After step 11:** no page: RL-B8 with row 1 `shortcut` "Try a shortcut" (y 155) selected (step 8's click) and wholly in view; the right panel `d07c677`; the focus on the graph. Every row of RL-B8 fits at full height, so "in view" holds whether or not a reveal is asked for (nothing was picked on the page step 10 showed); B5's step 11 is the check that needs 11.1's reveal. Git as after step 7. | Not run yet | RW: steps 1, 2, 4, 5, 6, 7, 8 and 11 in one launch on a fresh copy, after `RealApp`'s wait for the clean-up's record: the section header by `RefRow`, the rows by their `ReflogRow` names and clicked with the real mouse, the menu items by name, the name typed with the real keyboard (`ReplaceText`) and the check box clicked; the captures compared with `b8b-head-reflog`, `b8d-entry-menu` and `b8j-back-to-graph`; git's state asserted as in the scripted run. What only the real window shows: the reflog page and its footer in a real window, the tooltip of a disabled item drawn inside it | `b8a-section-menu`, `b8b-head-reflog`, `b8c-lost-tooltip`, `b8d-entry-menu`, `b8e-name-dialog`, `b8f-branch-created`, `b8g-source-menu`, `b8h-main-reflog`, `b8i-topic-reflog`, `b8j-back-to-graph`; real window: `b8b-head-reflog`, `b8d-entry-menu`, `b8j-back-to-graph` | Design 5.1, 5.2, 11.1, 11.3, 13.1, 13.4; D154 (older actions restored from the reflog view), D176, D180; phase 3's 2.4 (Create branch). Decisions BD1, BD3, BD5, BD6, BD7, BD8, BD9, BD10, BD18, BD23. The lost commit `d07c677` is the one a hard reset left behind: B8 recovers a reset's commit through the reflog. The entry menu's reasons come from the row (5.2): `d07c677` is not on `main`, so "Cherry-pick onto main" is enabled. |
| B9 | The backups list from `Show backups` and its restores: the list, a cut text's tooltip, a deleted branch restored (then its item disabled), a deleted remote branch pushed back (the question), discarded files restored with the question when a file changed (the newer version saved first), an abort's changes restored without a question and a skip's with it, a mixed reset's staged changes restored (then its item disabled); "reflog", dark, 1100×700 | `Check_B9_backups_list_branch_and_push_back`: 1. "Reflog" scenario; wait for the clean-up. Right-click "Local branches"; click "Show backups"; wait for 15 rows. Screenshot. 2. Rest the pointer on the words of row 9 (464, 371). Screenshot. 3. Right-click row 0 `branch-20260101T122701000Z` (420, 137). Screenshot. 4. Click "Restore branch doomed"; wait for the log line. Right-click row 0 again; rest the pointer on "Restore branch doomed". Screenshot. Press Esc. 5. Right-click row 5 `remote-branch-20260101T121701000Z` (420, 267). Screenshot. 6. Click "Push feature/gone back to origin…". Screenshot. 7. Click "Push"; wait for the log line. Press Esc. Screenshot.<br>`Check_B9_discarded_files_with_the_question`: 8. Before the app starts, write src/app.txt as `app\nmy own edit\n` (`TempRepo`). Start; wait for the clean-up. Right-click "Local branches"; click "Show backups"; right-click row 2 `discard-20260101T122001000Z` (420, 189). Screenshot. 9. Click "Restore the discarded files…". Screenshot. 10. Click "Restore"; wait for the log line. Screenshot.<br>`Check_B9_abort_and_skip_changes`: 11. Start on a fresh copy; wait for the clean-up; open the backups list. Right-click row 14 `abort-20260101T120601000Z` (420, 501). Screenshot. Click "Restore the changes…"; wait for the log line. Screenshot. 12. Right-click row 13 `skip-20260101T120701000Z` (420, 475); click "Restore the changes…". Screenshot. Click "Restore"; wait for the log line. Screenshot.<br>`Check_B9_staged_changes_of_a_mixed_reset`: 13. Start on a fresh copy; wait for the clean-up; open the backups list. Right-click row 3 `reset-20260101T121901000Z` (420, 215). Screenshot. 14. Click "Restore the staged changes"; wait for the log line. Right-click row 3 again; rest the pointer on "Restore the staged changes". Screenshot. 15. Press Esc twice. Click the working-changes row (468, 129). Screenshot. | **After step 1:** the page `Reflog` (pane `Backups`): `ReflogSource` reads `Backups`; 15 rows (`BackupRow`, named by the backup's name) as "The backups list after the clean-up at opening" gives them, newest first, each with its time (x 268 to 404), its words (x 412 to 524) and its commit (x 532 to 692; decision BD4), the words and subject cut with "…" where they do not fit; all 15 in view (row 14 at y 501); no footer; no lost marks; the right panel the placeholder.<br>**After step 2:** the words of row 9 are cut with "…"; the tooltip reads `Dropped or popped stash "On main: Try a README"`.<br>**After step 3:** row 0 selected and its commit `44e646b` "Doomed work" shown in the right panel directly (no graph row; decision BD9); the menu, 154 high (decision BD8): header `Deleted branch doomed`; "Restore branch doomed" (highlighted); —; "Create branch here…"; "Check out this commit"; —; "Copy SHA".<br>**After step 4:** no question; `reflog: Restore branch doomed: Succeeded after <N> ms: Restored branch doomed`; the status bar `Restored branch doomed`; Undo's tooltip `Undo restore of branch doomed`. Git: one `git update-ref -m "VisualCommit: restore of branch doomed" --create-reflog --stdin` with `create refs/heads/doomed 44e646bdcb1d920659f2b28b1b44e19f6ae45a7b` (design 7.3's `SetRef`; decision BD17); `refs/heads/doomed` → 44e646b…, its reflog `doomed@{0}` `VisualCommit: restore of branch doomed`; no `branch.doomed.*` key (no upstream: 7.10); HEAD on `main`, clean; the backup still listed. The graph behind is RL-B9a; Local branches `5`. The menu of row 0 again: "Restore branch doomed" (disabled: "A branch named doomed exists"), its tooltip shown; the right-click selected `44e646b`, now graph row 0, behind the page.<br>**After step 5:** the right panel `7dd8e8f` "Add the app" (graph row 8 behind); the menu, 154 high: header `Deleted feature/gone on origin`; "Push feature/gone back to origin…" (highlighted); —; "Create branch here…"; "Check out this commit"; —; "Copy SHA".<br>**After step 6:** the question: `ConfirmTitle` `Push feature/gone back to origin?`, `ConfirmQuestion` `origin gets feature/gone back at 7dd8e8f.`, no detail; `Cancel` with the focus and `Push` (not danger). Git unchanged.<br>**After step 7:** `reflog: Push feature/gone back to origin: Succeeded after <N> ms: Pushed feature/gone back to origin`; the status bar `Pushed feature/gone back to origin`; Undo's tooltip `Undo push of feature/gone back to origin`. Git: the app ran `git push --progress --force-with-lease=refs/heads/feature/gone: origin 7dd8e8f7958138d967be47cb167b84bac7fd7467:refs/heads/feature/gone` (exit 0, `* [new branch]      7dd8e8f7958138d967be47cb167b84bac7fd7467 -> feature/gone`); `git ls-remote origin` lists `refs/heads/feature/gone` 7dd8e8f…, `main` 6d7a40b…, `topic` 362fbd6…; locally `refs/remotes/origin/feature/gone` → 7dd8e8f… (reflog `update by push`). After Esc: RL-B9b with row 8 "Add the app" (y 337) selected and in view (the right-click of step 5 selected it), the label `origin/feature/gone` on it; the right panel `7dd8e8f` (`Test Author <author@example.com>`, 2026-01-01 12:01, Parents `5c9c1cb`, `Changed files (1)`: `A` `app.txt` `src`); Remotes `3`.<br>**After step 8:** the working-changes row shows behind the page (` M src/app.txt`); the right panel shows the snapshot `9849838` (its details as "reflog" gives them); the menu, 154 high (decision BD8; the snapshot is a commit, so it has 7.10's generic items as every backup that holds one): header `Before discarding 2 files`; "Restore the discarded files…" (highlighted); —; "Create branch here…"; "Check out this commit"; —; "Copy SHA".<br>**After step 9:** D75's question: `Restore discarded changes?`, `app.txt has changed since the discard. Put back the version from before the discard?`, detail `A snapshot of the newer changes is saved first.`, `Cancel` with the focus and `Restore` (danger). Only src/app.txt counts as changed: notes/todo.txt is absent in the working tree and in HEAD (decision BD15). Git unchanged.<br>**After step 10:** `reflog: Restore the discarded files: Succeeded after <N> ms: Restored 2 files`; the status bar `Restored 2 files`; Undo's tooltip `Undo restore of the discarded files`. Git: first a new backup `refs/visualcommit/backup/discard-20260102T090001000Z` → a snapshot commit with parent `9dd61fd`, author and committer `VisualCommit <snapshot@visualcommit.invalid>` at 2026-01-02 09:00, message `VisualCommit: snapshot before discarding 1 file`, an empty line and `src/app.txt`, whose src/app.txt is `app\nmy own edit\n`: the commit 27c8989ccb8c834f2788d01bd7ce7358ddd61c30 (asserted by its id: D72's dates fix it in both harnesses, and Git 2.36 and 2.55 made the same); then `git restore --source=9849838c39b980387b976a8a864b7c6d1ffda865 --worktree -- notes/todo.txt src/app.txt`: src/app.txt is `app\nmain work\nstaged\n`, notes/todo.txt `todo\n`; `git status --porcelain` ` M src/app.txt`, `?? notes/todo.txt`; nothing staged; the clean-up removed nothing (16 backups). The list read again: 16 rows, row 0 `discard-20260102T090001000Z`, `2026-01-02 09:00:01`, `Before discarding 1 file`, `27c8989 VisualCommit: snapshot before discarding 1 file`; the others one row lower.<br>**After step 11:** the menu, 154 high: header `Before aborting`; "Restore the changes…" (highlighted); —; "Create branch here…"; "Check out this commit"; —; "Copy SHA" (the right panel `6d7a40b` "Main work", graph row 6 behind). No question (neither docs/clash.txt nor src/app.txt has a change); `reflog: Restore the changes: Succeeded after <N> ms: Restored 2 files` (every path of the snapshot counts, docs/clash.txt too, though its content was already equal: decision BD20); the status bar `Restored 2 files`; Undo's tooltip `Undo restore of the changes`. Git: `git restore --source=08b41a45cd0d6f8571d150651e979bd6be73a9c4 --worktree -- docs/clash.txt src/app.txt`; docs/clash.txt unchanged (it equals HEAD's), src/app.txt `app\nmain work and clash\n`; status ` M src/app.txt`; no new backup (15).<br>**After step 12:** the menu was the same with header `Before skipping a commit`; then D75's question with `app.txt has changed since the discard. Put back the version from before the discard?` (src/app.txt differs from HEAD's and from the backup's); after Restore: `reflog: Restore the changes: Succeeded after <N> ms: Restored 1 file`; the status bar `Restored 1 file`; Undo's tooltip `Undo restore of the changes`; git: a new backup `refs/visualcommit/backup/discard-20260102T090001000Z` (the run's first backup) → the commit c424282c53a875a664e97e94bd148e389057a6e4 (asserted by its id, as step 10's: parent `9dd61fd`, `VisualCommit <snapshot@visualcommit.invalid>` at 2026-01-02 09:00, message `VisualCommit: snapshot before discarding 1 file`, an empty line and `src/app.txt`, its src/app.txt `app\nmain work and clash\n`), then `git restore --source=946fb4b9d6331facefff73c01cdd1e8271276db2 --worktree -- src/app.txt`: src/app.txt `app\nresolved, then skipped\n`; status ` M src/app.txt`. The list read again: 16 rows, row 0 `discard-20260102T090001000Z`, `2026-01-02 09:00:01`, `Before discarding 1 file`, `c424282 VisualCommit: snapshot before discarding 1 file`; the others one row lower.<br>**After step 13:** the menu, 154 high: header `Before a mixed reset`; "Restore the staged changes" (highlighted); —; "Create branch here…"; "Check out this commit"; —; "Copy SHA" (the right panel `316166a` "Clash notes", Date 2026-01-01 12:05, Commit date 2026-01-01 12:08, graph row 3 behind).<br>**After step 14:** no question; `reflog: Restore the staged changes: Succeeded after <N> ms: Restored the staged changes`; the status bar `Restored the staged changes`; Undo's tooltip `Undo restore of the staged changes`. Git (decision BD16): `git restore --staged --source=57a5e6edfbd470dc411b4ea3bb4eaabc405aa571 -- src/app.txt`; `git ls-files --stage src/app.txt` `100644 99a66a7dafbd532316960597a6609b8fe32bccae 0`, its content `app\nmain work\nstaged\n`; the working tree unchanged; `git status --porcelain` `MM src/app.txt` only (`git diff --cached`: `+staged`; `git diff`: `-staged`); docs/notes.txt untouched; no new backup. The menu of row 3 again: "Restore the staged changes" (disabled: "Unstage your changes first"), its tooltip shown.<br>**After step 15:** no menu, no page; the graph RL0 under the working-changes row, which is selected: the stage panel `Unstaged files (1)` with `M` `app.txt` `src` and `Staged files (1)` with `M` `app.txt` `src`. | Not run yet | RW: the list: step 1 of `Check_B9_backups_list_branch_and_push_back` in one launch after `RealApp`'s wait for the clean-up's record, the section header by `RefRow`, "Show backups" by name, the 15 rows checked by their `BackupRow` names in order, the capture compared with `b9a-backups`. The restores are scripted only: their git calls are design 7.3's effects (7.10), which `UndoEffectTests` prove on real repositories, and their questions are phase 3's confirmation dialog, which earlier real-window checks picture | `b9a-backups`, `b9b-cut-words-tooltip`, `b9c-branch-menu`, `b9d-restore-disabled`, `b9e-remote-branch-menu`, `b9f-push-question`, `b9g-pushed-back`, `b9h-discard-menu`, `b9i-restore-question`, `b9j-files-restored`, `b9k-abort-menu`, `b9l-abort-restored`, `b9m-skip-question`, `b9n-skip-restored`, `b9o-mixed-reset-menu`, `b9p-staged-disabled`, `b9q-stage-panel`; real window: `b9a-backups` | Design 5.3, 7.5, 7.6, 7.10, 12.1 to 12.4, 13.2; D154, D155, D165 (an abort undone from the backups list), D171, D172; D75 (the question and the newer version's snapshot), D139 (the clean-up). Decisions BD4, BD6, BD8, BD9, BD15, BD16, BD17, BD19, BD20, BD21, BD23. The design's literal `IndexTo(<snapshot>, HEAD)` would also have staged `D docs/notes.txt` here, since HEAD has moved since the reset (scenario facts 7, item 2): decision BD16. |
| F10 | Search, Commits mode: a word only in commit bodies, an author, a SHA prefix: the bar, the marks and the dimmed rows, the count in each of its forms, Enter and Shift+Enter (the selection, the right panel, wrapping round), Esc; the bar and the marks in the light theme at full width. Part A: "history", dark, 1100×700 (`Check_F10_search_commits`); part B: "history", light, 1920×1080 (`Check_F10_search_light_1920`); one start each | Part A: 1. "History" scenario. Click the toolbar's Search (`SearchButton`). Screenshot. 2. Type `tokenizer` (the box has the focus). Wait until the search has ended (`WaitForSearchAsync`). Screenshot. 3. Press Enter. Screenshot. 4. Press Enter; press Enter again; press Shift+Enter. Screenshot. 5. Replace the box's text with `Jonas` (`ReplaceText("SearchBox", "Jonas")`: the command modifier with A, then typing). Wait until the search has ended. Screenshot. 6. Replace the box's text with `0575`. Wait until the search has ended. Press Enter. Screenshot. 7. Press Esc. Screenshot. 8. Press Down. Part B (a new copy, a new start): 9. "History" scenario at 1920×1080 in the light theme. Click Search, type `tokenizer`, wait until the search has ended, press Enter. Screenshot. | **At the start:** H0 with the bar closed; nothing selected; the details panel "Select a commit to see its details."; `CurrentBranch` `main`.<br>**After step 1:** The search bar (`SearchBar`) at the top of the graph area, under the toolbar and above the column headers: x 260 to 700, y 88 to 124 (36 high), in `VcChromeBackgroundBrush` (#20242C at (262, 90)) with a 1-px `VcBorderBrush` line along its bottom (#323845 at (262, 123)). Left to right, 8 from the bar's edges and 6 apart: `SearchModeButton` "Commits" with `IconChevronDown` (FD1); `SearchBox`, empty, its placeholder "Message, author or SHA" in the secondary colour, at least 120 wide, with the keyboard focus; `SearchCount`, empty; `SearchPrevious` and `SearchNext` (22 wide, `IconChevronUp` and `IconChevronDown`), disabled, their icons in `VcTextDisabledBrush` (#5F6775), their tooltips "Previous match (Shift+Enter)" and "Next match (Enter)"; `SearchClose` (22 wide, `IconClose`, tooltip "Close the search (Esc)"), its right edge at x 692. No `SearchProgress`. The column headers moved down by 36 (y 124 to 152) and the rows with them: the working-changes row's centre y 165, commit row r's 191 + 26 r (H0's "bar open" column); the columns as before (Branch / Tag, Graph 48, Message 142, Date). The box is empty, so nothing is marked or dimmed: `SearchMarks()` reports no row marked or dimmed and `GraphRows.Search.IsActive` is false; row 0's node at full strength (#7C8CFF at (409, 191)). No `git log --no-walk=unsorted …` call has run.<br>**After step 2:** The count reads "2 matches" (nothing is selected); Previous and Next enabled. Marked: rows 6 (Compare whole lines) and 10 (Handle empty input): each a 3-px bar of `VcSearchMatchBrush` at its left edge over the Branch / Tag column's first 3 px (#F2C94C at (261, 347) and (261, 451)) and its message in SemiBold. Dimmed: the 12 other commit rows (0 to 5, 7 to 9, 11 to 13), their nodes, labels and texts at 35 % opacity: row 0's node sampled at (409, 191) is #383F6B, row 4's at (425, 295) #234F3E, row 8's at (425, 399) #5B4726; a lane passing through a dimmed row stays at full strength (lane 0's line at (406, 295), in row 4, is #7C8CFF). The marked rows' nodes are at full strength (row 6's at (425, 347) #E0A23B, row 10's at (409, 451) #7C8CFF). The working-changes row is neither marked nor dimmed (FD2). The call log has `git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%H -F --regexp-ignore-case --grep=tokenizer --`, exit 0, its output `245c395674db6ae89390be2db018db5356b446fe` then `365482be62488b4c1259f0fb7dfa1631c797470b` (row order); "tokenizer" is no hex and in no author, so the matches are the two bodies'.<br>**After step 3:** Row 6 (Compare whole lines) selected as a click selects it (the selection background over the whole row, `SelectedIndex` 6), in view; the count "1 of 2"; the keyboard focus stays in the box (FD3); the marks as after step 2. The details panel: "Compare whole lines"; the body "It splits the texts with the tokenizer first."; Author "Jonas Berg <jonas@example.com>"; Date "2026-01-01 12:07"; SHA `245c395674db6ae89390be2db018db5356b446fe`; Parents `d0d3c77`; "Changed files (1)": M `Comparer.cs` `src`.<br>**After step 4:** The first Enter selected row 10 ("2 of 2"); the second wrapped round to row 6 ("1 of 2"); Shift+Enter wrapped back to row 10: row 10 (Handle empty input) selected, "2 of 2". Its details: "Handle empty input"; the body "The tokenizer stops at an empty text now."; Author "Jonas Berg <jonas@example.com>"; Date "2026-01-01 12:03"; SHA `365482be62488b4c1259f0fb7dfa1631c797470b`; Parents `775113f`; "Changed files (1)": M `Parser.cs` `src`. No search call ran since step 2.<br>**After step 5:** "Jonas" matches by author, in memory: rows 0 (Read whole lines), 4 (Explain the reader), 6, 8 (Add the comparer) and 10; the new call `git log --no-walk=unsorted … --grep=Jonas --` exited 0 and printed nothing. Row 10 is still selected (a new text keeps the selection) and is the fifth match in row order: "5 of 5". Rows 0, 4, 6, 8 and 10 marked; rows 1, 2, 3, 5, 7, 9, 11, 12 and 13 dimmed; row 0's node now at full strength (#7C8CFF at (409, 191)).<br>**After step 6:** Before Enter the count read "1 match": `0575` (four hex digits) matches row 7 by its SHA `0575c7f…`, in memory, and row 10, still selected, is no match; the call `… --grep=0575 --` printed nothing. Enter selected row 7: "1 of 1"; row 7 marked, the 13 others dimmed. The details: "Rename the parser to reader"; no body; Author "Mira Kovac <mira@example.com>"; Date "2026-01-01 12:06"; SHA `0575c7f6631b3ba6c9d623f53b28489405230cfd`; Parents `14a5c6c`; "Changed files (1)": R `Reader.cs` `src` renamed from `src/Parser.cs`.<br>**After step 7:** The bar is closed (no `SearchBar` shown); the column headers back at y 88 to 116 and the rows at H0's "bar closed" places (row 7's centre y 337); nothing marked or dimmed (`Search.IsActive` false; row 0's node #7C8CFF at (409, 155)); row 7 still selected with its details; the keyboard focus in `GraphRows` (3.1: closing clears the marks, keeps the selection and gives the focus to the graph).<br>**After step 8:** Row 8 (Add the comparer) selected: the graph had the focus. Git: nothing changed (`git status --porcelain` ` M src/Reader.cs`; every ref as built).<br>**After step 9:** At 1920×1080 the bar spans x 260 to 1520 (y 88 to 124), in the light chrome (#EBEDF1 at (262, 90)) with the light border line (#D2D6DE at (262, 123)); all six graph columns below it; the count "1 of 2", whole, not cut; row 6 selected; rows 6 and 10 marked in the light `VcSearchMatchBrush` (#B98900 at (261, 347) and (261, 451)); row 0's dimmed node at (409, 191) #BDC3F1; the details of Compare whole lines in the light colours. | Not run yet | RW: steps 1 to 3 and Esc: the real click on Search, `tokenizer` typed key by key on the real keyboard, the wait until `SearchCount`'s automation name reads "2 matches", the capture compared with `f10b-tokenizer`; the real Enter, the capture compared with `f10c-first-match`; the real Esc, after which no `SearchBar` element remains and `DetailsSha` still shows `245c395…`. What only the real window shows: the box's delay on real key presses and Enter in a real text box | `f10a-bar-open`, `f10b-tokenizer`, `f10c-first-match`, `f10d-previous`, `f10e-author`, `f10f-sha`, `f10g-closed`, `f10h-light-wide`; real window: `f10b-tokenizer`, `f10c-first-match` | Design 1.1, 3.1 to 3.4, 11.2, 13.3, 13.4; D157, D174; FD1 to FD3, FD6, FD17, FD18. The search bar's positions are derived from 3.1 (the bar is not built yet): the checks take them from the bounds. Every picture but `f10g` is taken with the focus in the search box, whose caret blinks: a capture may catch it on or off (as phase 4's `b7f`), the scripted ones too (over six scripted runs `f10b` to `f10f` and `f10h` were caught both ways), the real window's two within the 3 % limit. |
| F11 | Search, Files and Changes modes: the mode menu; a part of a file name (both names of the renamed file); a text no path holds; a string added in one commit and removed in a later one; a commit made outside the app while the search is open: the marks stay and only the new row is searched; "history", dark, 1100×700 (`Check_F11_search_files_and_changes`, one start) | 1. "History" scenario. Click Search. Click the mode button (`SearchModeButton`). Screenshot. 2. Click "Files: a path or file name". Type `parser`. Wait until the search has ended. Screenshot. 3. Replace the box's text with `reader`. Wait until the search has ended. Screenshot. 4. Replace the box's text with `TraceEverything`. Wait until the search has ended. Screenshot. 5. Choose "Changes: text added or removed" (`ChooseSearchMode`). Wait until the search has ended. Screenshot. 6. Note the call log's length. Outside the app, through `TempRepo`: write src/Config.cs as `HistoryFiles.ConfigTraced`, then `GitAsync("add", "--", "src/Config.cs")` and `GitAsync("commit", "--quiet", "--message", "Trace again")` (Test Author, the copy's clock 2026-01-01 12:14; only src/Config.cs staged, FD8). Wait until the graph's row 0 is "Trace again" (`OutsideChangeTimeout`) and the search has ended. Screenshot. | **After step 1:** The bar as F10 step 1. The mode menu, opened through the menu layer with its top-left at the mode button's bottom-left corner (±2) and 4 px inside the window, 110 high (3 items, no separator): header "Search"; "Commits: message, author or SHA" (highlighted); "Files: a path or file name"; "Changes: text added or removed"; all enabled (FD5).<br>**After step 2:** The menu closed; the mode button reads "Files"; the box has the focus again (FD4) and, while empty, showed the placeholder "File name or path (case ignored for A–Z)". "parser" matches rows 7 (Rename the parser to reader, by the old name src/Parser.cs), 10 (Handle empty input, M src/Parser.cs) and 12 (Add the parser, A src/Parser.cs): "3 matches"; those rows marked, the 11 other commit rows dimmed. The call log has `git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%x1e%H --first-parent --root --no-renames --name-only -- :(glob,icase)**/*parser* :(glob,icase)**/*parser*/**`, exit 0 (run without `GIT_LITERAL_PATHSPECS`, under which the glob would be read as a file name and match nothing, 0.3).<br>**After step 3:** "reader" matches rows 0 (Read whole lines, M src/Reader.cs) and 7 (the rename, by its new name src/Reader.cs): "2 matches"; the rename's row matched by its old name in step 2 and by its new one here.<br>**After step 4:** No path holds "traceeverything": the count "No matches"; all 14 commit rows dimmed, none marked; Previous and Next disabled, in `VcTextDisabledBrush`.<br>**After step 5:** The mode button reads "Changes"; the box keeps "TraceEverything" (FD4), its placeholder (`SearchBox`'s, not shown while it holds text) "Text added or removed (case ignored for A–Z)"; the Changes search ran for the same text: rows 1 (Stop tracing, which removes `    public const bool TraceEverything = true;`) and 9 (Add tracing, which adds it): "2 matches"; no `SearchProgress` once it ended. The call log has `git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%H --first-parent --diff-merges=first-parent --root --find-renames --no-textconv -s --regexp-ignore-case -STraceEverything --`, exit 0, its output `20d7fbe203a4a8ceeb5ef00da13ceda91ed70739` then `14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c`.<br>**After step 6:** Git: `main` is `f612569a47849c0326a3022a420c241d56d7ddde` ("Trace again", parent `c0bc2dd`, Test Author, author and commit date 2026-01-01 12:14, `M src/Config.cs`); `git status --porcelain` still ` M src/Reader.cs`; `git status -sb` prints `## main...origin/main [ahead 1]` and ` M src/Reader.cs`. The graph is H-F1: the working-changes row, then 15 commit rows, "Trace again" with `main` ✓ at row 0, Read whole lines with `origin/main` at row 1; the left panel's `main` ↑1. The bar is still open with "Changes" and "TraceEverything"; the count "3 matches": rows 0 (Trace again), 2 (Stop tracing) and 10 (Add tracing) marked, the other 12 commit rows dimmed. Since the step's mark the call log has exactly one search call, step 5's command again, exit 0, whose output is the one line `f612569a47849c0326a3022a420c241d56d7ddde`: git was given only the new row, the older rows' results being kept by SHA (3.4); given all 15 rows it would have printed the two older matches too. | Not run yet | n/a: F10 repeats the bar, real typing and Enter in the real window; the modes differ only in git's arguments, which `SearchTests` run on Git 2.36 and 2.55; the refresh follows the file watcher, which the scripted run drives on the real file system | `f11a-mode-menu`, `f11b-files-parser`, `f11c-files-reader`, `f11d-no-matches`, `f11e-changes`, `f11f-after-refresh` | Design 3.1, 3.2, 3.4, 0.3; D174; FD4, FD5, FD7, FD8. On Git 2.55 the Files call prints all 14 commits' header lines and path lines only under the 3 matching ones (2.36 prints only the 3), the regression design 0.3 names; the matches are the same, so the check asserts that call's command, exit code and matches, not its raw output. Every picture but `f11a` is taken with the focus in the search box, whose caret blinks: a capture may catch it on or off, as F10's (`f11b` to `f11d` were caught both ways over seven scripted runs). |
| F12 | Compare two selected commits (two-dot): the right panel at once, a file's diff from it, Search closing that diff, Swap, the list's file menu, a link (the comparison stays), a third commit selected and taken out again, a single selection ending it; "history", dark, 1100×700 (`Check_F12_compare_two_commits`, one start) | 1. "History" scenario. Click the row "Rename the parser to reader" (row 7) in its Message column, then Ctrl+click the row "Read whole lines" (row 0). Screenshot. 2. In the comparison's file list (`CompareFileList`) click `Reader.cs` (`ClickCompareFile("src/Reader.cs")`). Screenshot. 3. Click the toolbar's Search. Press Esc. 4. Click the swap button (`CompareSwap`). Screenshot. Right-click `Reader.cs` in `CompareFileList`. Screenshot. Press Esc. 5. Click the To link (`CompareTo`). 6. Ctrl+click the row "Merge branch 'feature/readme'" (row 3); Ctrl+click it again. 7. Click row 3. | **After step 1:** Rows 7 and 0 have the selection background; the working-changes row is not selected. The right panel shows the comparison (`Compare`, D162) in place of phase 3's "2 commits selected": a 36-high header with `CompareTitle` "Compare" and the swap button (`CompareSwap`), no close button (6.4: a branch comparison's only); `CompareFrom` "From 0575c7f Rename the parser to reader" (the lower row is From, 6.1) and `CompareTo` "To c0bc2dd Read whole lines", both links enabled; no `CompareCounts`, no `All differences`; "Changed files (4)" with Flat (selected) and Tree, no All files; `CompareFileList`: M `README.md`; M `notes.md` `docs`; M `Config.cs` `src`; M `Reader.cs` `src`. The call log has `git rev-parse 0575c7f6631b3ba6c9d623f53b28489405230cfd^{commit} c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9^{commit} --` (output the two SHAs, then `--`; FD20) and `git diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id 0575c7f6631b3ba6c9d623f53b28489405230cfd c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9`, both exit 0; no `merge-base` and no `rev-list --left-right` call (two commits compare their trees, 6.3).<br>**After step 2:** Phase 2's diff view in the graph area: M `Reader.cs` `src`, the side label "0575c7f..c0bc2dd" (6.4), the close button; the toolbar with Inline (selected) and Side by side and no actions; the hunk `@@ -10,13 +10,13 @@ public sealed class Reader` without buttons, rows as "What part F's comparisons list" gives src/Reader.cs's diff (context 10 to 12, removed and added 13 `Words()` / `Lines()`, context 14 to 19, removed and added 20 `Split(' ')` / `Split('\n')`, context 21 and 22). The row `Reader.cs` in the comparison's list has the selection background; rows 0 and 7 stay selected and the comparison stays.<br>**After step 3:** Search closed the diff (it holds no work, 11.1) and opened the bar over the graph, rows 0 and 7 still selected, the comparison still shown and its file row no longer selected; Esc closed the bar.<br>**After step 4:** `CompareFrom` "From c0bc2dd Read whole lines", `CompareTo` "To 0575c7f Rename the parser to reader"; "Changed files (4)": the same four, all M (each change reversed); the call log has `git rev-parse c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9^{commit} 0575c7f6631b3ba6c9d623f53b28489405230cfd^{commit} --` and `git diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9 0575c7f6631b3ba6c9d623f53b28489405230cfd`, both exit 0. The graph's selection is unchanged. The right-click on `Reader.cs` opened the comparison's file menu (`ForCompareFile`, 6.4) only, selecting no file row and opening no diff: 171 high (5 items, 1 separator): header `Reader.cs`; "File history" (highlighted); "Blame"; —; "Open file" (enabled: src/Reader.cs is in the working tree); "Show in folder"; "Copy path". Esc closed the menu only: the comparison as after the swap.<br>**After step 5:** The link revealed row 7 without selecting it alone (11.3's `Reveal`; it was in view, so the scroll offset stays 0): rows 0 and 7 stay selected, the comparison as after step 4.<br>**After step 6:** With row 3 added three commits are selected and the right panel shows phase 3's list: "3 commits selected", `c0bc2dd` Read whole lines, `f513566` Merge branch 'feature/readme', `0575c7f` Rename the parser to reader, and the hint "Right-click a selected commit for what can be done with all of them."; with row 3 taken out again the comparison is back as a new one, in the graph's order: "From 0575c7f Rename the parser to reader", "To c0bc2dd Read whole lines" (FD9).<br>**After step 7:** Row 3 alone is selected: the comparison has ended (6.1) and the details show "Merge branch 'feature/readme'": Author "Test Author <author@example.com>"; Date "2026-01-01 12:10"; SHA `f5135668320ac7e48c60dd634a18f4eb3906f160`; Parents `884b462` `c1b6375`; "Changed files (1)": M `README.md`. Git: nothing changed (` M src/Reader.cs`; every ref as built). | Not run yet | RW: steps 1 and 2: the real click and Ctrl+click on rows 7 and 0, the capture compared with `f12a-two-commits`; the real click on `Reader.cs` (`ChangedFile` named `Reader.cs` inside `CompareFileList`, the list's only `Reader.cs`: a row's automation name is the file name alone, `ChangedFileRow.Name`), the capture compared with `f12b-file-diff`; Esc. The new right-panel view and its diff in the real window | `f12a-two-commits`, `f12b-file-diff`, `f12c-swapped`, `f12d-compare-file-menu`; real window: `f12a-two-commits`, `f12b-file-diff` | Design 6.1, 6.3, 6.4, 11.1, 11.3, 13.1; D162, D177; FD9, FD10, FD12, FD20, FD21. The headless `ClickCompareFile("src/Reader.cs")` takes the path, matched to the row's key (`ChangedFileRow`'s path), since a name can repeat in a flat list. F12 pictures the file menu and runs none of its items: File history and Blame open part B's pages (B5, B6), and that they start at To is a fact for WP7's view-model tests. |
| F13 | Compare branches: from a local branch's menu while the working-changes row is selected (three-dot: the merge base's diff of a file, the counts), with "All differences", a link, closing (the stage panel's draft kept); from a drop, the other way round; "history", dark, 1100×700 (`Check_F13_compare_branches`, one start) | 1. "History" scenario. Click the working-changes row; click the summary box (`CommitSummary`) and type `Keep this draft`. 2. In the left panel click the folder `feature` under Local branches, then right-click its `compare`. Screenshot. 3. Click "Compare feature/compare with main". Wait until the comparison is read (`WaitForCompareAsync`). Screenshot. 4. Click `README.md` in the comparison's list. Screenshot. Press Esc. 5. Click the check box "All differences" (`CompareAllDifferences`). Wait until the comparison is read. Screenshot. 6. Click the To link (`CompareTo`). 7. Click the comparison's close button (`CompareClose`). Click the working-changes row. Screenshot. 8. Press the left mouse button at the centre of the label `main` on row 0 ("Read whole lines") and, holding it, move to the centre of the label `feature/comp…` on row 6 ("Compare whole lines") in 5 equal steps (`MoveMouseHeld(…, steps: 5)`, as phase 3's check 7); release. Screenshot. 9. Click "Compare main with feature/compare". Wait until the comparison is read. Screenshot. 10. Click the comparison's close button. | **After step 1:** The working-changes row selected; the stage panel: "Unstaged files (1)": M `Reader.cs` `src`; "No staged changes"; the summary "Keep this draft", the counter "57"; the disabled "Stage files to commit".<br>**After step 2:** The folder is open, `compare` under it. The right-click opened the menu only (D114): the working-changes row stays selected and the stage panel shows. The menu (571 high, 19 items, 5 separators): header "feature/compare"; "Check out feature/compare" (highlighted); "Merge feature/compare into main"; "Merge feature/compare into main (create a merge commit)"; "Rebase main onto feature/compare"; "Interactive rebase of main onto feature/compare…"; "Fast-forward feature/compare to origin/feature/compare" (disabled: "feature/compare is up to date with origin/feature/compare"); —; "Push feature/compare to origin/feature/compare"; "Force push feature/compare…" (danger); —; "Create branch here…"; "Create tag here…"; "Rename feature/compare…"; "Delete feature/compare…" (danger); —; "Change upstream…"; "Stop tracking origin/feature/compare"; —; "Compare feature/compare with main"; "Show reflog of feature/compare"; "Hide feature/compare"; "Solo feature/compare"; —; "Copy branch name".<br>**After step 3:** The working-changes row is no longer selected (no selection background on it, `IsWorkingRowSelected` false, 6.2) and nothing is selected in the graph. The right panel: header "Compare" with Swap and Close (`CompareClose`); `CompareFrom` "From main" and `CompareTo` "To feature/compare", both links enabled; `CompareCounts` "2 commits only on feature/compare, 7 only on main"; "All differences" unchecked; "Changed files (2)": M `README.md`; A `Comparer.cs` `src` (three-dot: feature/compare's changes since it left main, from the merge base `14a5c6c` Add tracing; one base, so no "Several merge bases" line). The call log has `git rev-parse refs/heads/main^{commit} refs/heads/feature/compare^{commit} --` (From first, by full ref names, FD20; output `c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9`, `245c395674db6ae89390be2db018db5356b446fe`, then `--`), `git merge-base --all c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9 245c395674db6ae89390be2db018db5356b446fe` (output `14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c`), `git diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id 14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c 245c395674db6ae89390be2db018db5356b446fe` and `git rev-list --left-right --count c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9...245c395674db6ae89390be2db018db5356b446fe --` (output `7⇥2`), all exit 0.<br>**After step 4:** The diff view: M `README.md`, the side label "14a5c6c..245c395" (the merge base against To, 6.4); the hunk `@@ -1,3 +1,5 @@` without buttons: context 1/1 `# History`, 2/2 (empty), 3/3 `A repository for the history checks.`; added 4 (empty) and 5 `Compare two texts with Comparer.`; `README.md` selected in the comparison's list. Esc closed the diff: the graph again, the comparison unchanged, no file row selected.<br>**After step 5:** "All differences" checked: the two tips' trees (6.3): "Changed files (5)": M `README.md`; M `notes.md` `docs`; A `Comparer.cs` `src`; M `Config.cs` `src`; R `Parser.cs` `src` renamed from `src/Reader.cs` (main's rename seen backwards); the counts unchanged; a new `git diff-tree … c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9 245c395674db6ae89390be2db018db5356b446fe` call, exit 0, and no new `merge-base` call.<br>**After step 6:** The link revealed row 6 (in view already: the scroll offset stays 0) without selecting it: nothing selected, the comparison as after step 5.<br>**After step 7:** Closing showed the placeholder "Select a commit to see its details." (nothing was selected, 6.2) and gave the keyboard focus to the graph; the click on the working-changes row showed the stage panel with the summary "Keep this draft" and the counter "57", kept in memory meanwhile (6.2); "Unstaged files (1)": M `Reader.cs` `src`; "No staged changes".<br>**After step 8:** The press selected row 0 (Read whole lines; phase 3's 4g), so the working-changes row is no longer selected and the details of Read whole lines show behind the menu. Before the menu the app read the drop's facts with the drop's full ref names, target first (phase 4's `ReadDropFactsAsync`): `git merge-base --is-ancestor refs/heads/feature/compare refs/heads/main` exits 1, with no output. The menu (180 high, 5 items, 2 separators): header "main → feature/compare"; "Fast-forward feature/compare to main" (disabled: "feature/compare has commits that main does not have"); "Check out feature/compare and merge main into it" (highlighted); —; "Rebase main onto feature/compare"; "Interactive rebase of main onto feature/compare…"; —; "Compare main with feature/compare".<br>**After step 9:** From is B and To is A (6.2): "From feature/compare", "To main"; "7 commits only on main, 2 only on feature/compare"; "All differences" unchecked (a new comparison starts three-dot, FD9); "Changed files (4)": M `README.md`; M `notes.md` `docs`; M `Config.cs` `src`; R `Reader.cs` `src` renamed from `src/Parser.cs`. The call log has `git rev-parse refs/heads/feature/compare^{commit} refs/heads/main^{commit} --` (output `245c395674db6ae89390be2db018db5356b446fe`, `c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9`, then `--`), `git merge-base --all 245c395674db6ae89390be2db018db5356b446fe c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9` (`14a5c6c…`), `git diff-tree … 14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9` and `git rev-list --left-right --count 245c395674db6ae89390be2db018db5356b446fe...c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9 --` (`2⇥7`), exit 0. Row 0 stays selected.<br>**After step 10:** The details of the graph's single selection, Read whole lines: Author "Jonas Berg <jonas@example.com>"; Date "2026-01-01 12:13"; SHA `c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9`; Parents `20d7fbe`; "Changed files (1)": M `Reader.cs` `src`. Git: nothing changed (` M src/Reader.cs`; every ref as built; nothing staged). | Not run yet | n/a: the comparison's view is F12's, which the real window pictures; the left panel's menu and the drag are phase 3's and 4's input paths, which their real-window checks drive (phase 4's `a3c-drop-other-onto-main`); what F13 adds is git's three-dot list and counts, which `CompareTests` prove on Git 2.36 and 2.55 | `f13a-branch-menu`, `f13b-three-dot`, `f13c-merge-base-diff`, `f13d-all-differences`, `f13e-draft-kept`, `f13f-drop-menu`, `f13g-drop-compare` | Design 6.2 to 6.4, 13.1; D114, D162, D177; FD9 to FD11, FD20. |
| F14 | Hide and solo: the current branch's menu (Hide disabled); Hide feature/alpha with its upstream (the graph without them and without the stash on its commit, the left panel's marks, a click and the tooltip on the hidden row, a comparison whose To is no row), Show; Solo feature/beta (the graph, the labels left out, the left panel), Stop soloing from the section's menu; a restart keeps the hidden branch and its toggle shows it again; "filters", dark, 1100×700 (`Check_F14_hide_and_show`, `Check_F14_solo_and_stop_soloing`: one start each; `Check_F14_hidden_after_a_restart`: two starts on one data folder) | Part A: 1. "Filters" scenario. In the left panel click the folder `feature` under Local branches, then the folder `feature` under Remotes › origin. Screenshot. 2. Right-click `main` under Local branches; rest the pointer on "Hide main". Screenshot. Press Esc. 3. Right-click Local branches › feature › alpha. Screenshot. Click "Hide feature/alpha". Wait until the graph has reloaded (`WaitForGraphReloadAsync`). Screenshot. 4. Click Local branches › feature › alpha; rest the pointer on its name (`RefRowNamePoint`). Screenshot. Rest the pointer on its eye button (`RefHiddenToggle`). 5. Right-click Local branches › feature › alpha and click "Compare feature/alpha with main". Wait until the comparison is read. Screenshot. Click the comparison's close button. 6. Right-click Local branches › feature › alpha and click "Show feature/alpha". Wait until the graph has reloaded. Screenshot. Part B (a new copy, a new start): 7. "Filters" scenario. Open the two `feature` folders as in step 1. Right-click Local branches › feature › beta and click "Solo feature/beta". Wait until the graph has reloaded. Screenshot. 8. Right-click the section header "Local branches". Screenshot. Click "Stop soloing". Wait until the graph has reloaded. Right-click "Local branches" again. Press Esc. Part C (a new copy, a new data folder): 9. "Filters" scenario. Click the folder `feature` under Local branches, right-click its `alpha` and click "Hide feature/alpha"; wait until the graph has reloaded. Close the app. 10. Start a new instance on the same data folder (1100×700, dark) and wait until it is ready. Open the two `feature` folders. Screenshot. 11. Click the eye button (`RefHiddenToggle`) of Local branches › feature › alpha. Wait until the graph has reloaded. Screenshot. | **After step 1:** FL0 (15 rows, 5 lanes, no Date column), nothing selected, the left panel as FL0's with both folders open; `CurrentBranch` `main`.<br>**After step 2:** The right-click opened the menu only (D114). The menu (441 high, 14 items, 5 separators): header "main"; "Pull from origin/main" (highlighted; enabled as the toolbar's Pull); "Pull from origin/main (rebase)"; —; "Push main to origin/main"; "Force push main…" (danger); —; "Create branch here…"; "Create tag here…"; "Rename main…"; "Delete main…" (disabled: "You cannot delete the branch you are on"); —; "Change upstream…"; "Stop tracking origin/main"; —; "Show reflog of main"; "Hide main" (disabled: "The checked-out branch is always shown"); "Solo main"; —; "Copy branch name". No "Compare main with main" (13.1: not on the current branch). The pointer rests on "Hide main": its tooltip "The checked-out branch is always shown" shows inside the window and the highlight stays on "Pull from origin/main".<br>**After step 3:** The menu (571 high, 19 items, 5 separators): header "feature/alpha"; "Check out feature/alpha" (highlighted); "Merge feature/alpha into main"; "Merge feature/alpha into main (create a merge commit)"; "Rebase main onto feature/alpha"; "Interactive rebase of main onto feature/alpha…"; "Fast-forward feature/alpha to origin/feature/alpha" (enabled: it is 1 behind); —; "Push feature/alpha to origin/feature/alpha"; "Force push feature/alpha…" (danger); —; "Create branch here…"; "Create tag here…"; "Rename feature/alpha…"; "Delete feature/alpha…" (danger); —; "Change upstream…"; "Stop tracking origin/feature/alpha"; —; "Compare feature/alpha with main"; "Show reflog of feature/alpha"; "Hide feature/alpha"; "Solo feature/alpha"; —; "Copy branch name". After "Hide feature/alpha": the left panel shows the new state at once (8.2); the call log has `git log --date-order --format=%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%s --no-show-signature --no-color --encoding=UTF-8 --exclude=feature/alpha --branches --exclude=origin/feature/alpha --exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --`, exit 0 (8.2's hide form; `origin/HEAD` too, since a branch of `origin` is hidden, 0.6); `GraphReloading` is hidden once loaded. The graph is FL1: 12 rows, 3 lanes, the Date column back; Alpha one, Alpha two and the alpha stash left out; `stash@{1}` at row 0, its dashed line into Beta six; nothing selected. The left panel lists every ref, its counts unchanged (Local branches 4, Remotes 3, Tags 1, Stashes 2); Local branches › feature › alpha and Remotes › origin › feature › alpha are drawn in `VcTextDisabledBrush` (#5F6775), each with an eye button (`RefHiddenToggle`, `IconEyeOff`, 18 by 18) at its right, the local one's before its "↓1"; the other rows as before. The session (`session.json`): one `GraphFilters` entry for the repository's path: Hidden `refs/heads/feature/alpha`, `refs/remotes/origin/feature/alpha`; Soloed none; HiddenRemotes none; Upstreams (`refs/heads/feature/alpha`, `refs/remotes/origin/feature/alpha`); Remotes `origin`; LastUsed not asserted (no part F clock freezes it). Git: nothing changed (`git for-each-ref` and `.git/config` byte-identical to before the step).<br>**After step 4:** The click selected nothing (8.3: Alpha one is no row): the details panel "Select a commit to see its details.", no left-panel row highlighted (FD19). Resting on the row's name shows its tooltip "feature/alpha is hidden in the graph"; resting on the eye button "Hidden in the graph. Click to show it".<br>**After step 5:** The menu was step 3's with "Show feature/alpha" in place of "Hide feature/alpha" (571 high). The comparison (From main, To feature/alpha, three-dot): "From main" (link enabled: main's tip, the merge, is FL1's row 4); "To feature/alpha" (link disabled, tooltip "This commit is not in the graph": Alpha one is hidden, 6.4); "1 commit only on feature/alpha, 3 only on main" (the singular); "Changed files (1)": A `alpha.txt` `src`. The call log has `git rev-parse refs/heads/main^{commit} refs/heads/feature/alpha^{commit} --` (FD20; output `4c0aa3c354e1cda550d852d90b110d93df6b08cf`, `7c60fc46a4178e4f2f88df0f73c4b51808be230a`, then `--`: a hidden branch's ref still resolves), `git merge-base --all 4c0aa3c354e1cda550d852d90b110d93df6b08cf 7c60fc46a4178e4f2f88df0f73c4b51808be230a` (output `369f471b9087f5d12eb03bd8f5ebee5d2ae6d9b8`), `git diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id 369f471b9087f5d12eb03bd8f5ebee5d2ae6d9b8 7c60fc46a4178e4f2f88df0f73c4b51808be230a` and `git rev-list --left-right --count 4c0aa3c354e1cda550d852d90b110d93df6b08cf...7c60fc46a4178e4f2f88df0f73c4b51808be230a --` (`3⇥1`), exit 0. Closing it showed the placeholder.<br>**After step 6:** The graph reloaded with nothing hidden: the log call's revisions are `--branches --remotes --tags --ignore-missing HEAD --`, as without a filter (8.2); the graph is FL0 again (the alpha stash at row 0, its dashed line down to Alpha one); both alpha rows in their usual colours, no eye button; the session's `GraphFilters` holds no entry for the repository's path (FD14: an entry left with nothing hidden or soloed is removed).<br>**After step 7:** Beta's menu was step 3's with feature/beta (its fast-forward disabled: "feature/beta is up to date with origin/feature/beta"; 571 high). The call log has the graph's `git log --date-order …` with the revisions `--ignore-missing refs/heads/feature/beta refs/remotes/origin/feature/beta HEAD --` (8.2's solo form), exit 0. The graph is FL2: FL1's rows, lanes and lines with FL2's labels (row 4 `main` ✓ alone, row 7 none); the alpha stash left out. The left panel: Local branches › feature › beta and Remotes › origin › feature › beta in their usual colour with the solo button (`RefSoloToggle`, `IconSolo` in `VcAccentBrush`, tooltip "Soloed. Click to stop soloing it"); Local branches › feature › alpha (still "↓1"), Local branches › feature › merged, Remotes › origin › feature › alpha and Remotes › origin › main in `VcTextDisabledBrush` (neither soloed nor HEAD's, 8.3); `main` in the accent colour; Tags and Stashes as before. Session: one `GraphFilters` entry for the repository's path: Soloed `refs/heads/feature/beta`, `refs/remotes/origin/feature/beta`; Upstreams (`refs/heads/feature/beta`, `refs/remotes/origin/feature/beta`); Hidden none; HiddenRemotes none; Remotes `origin`.<br>**After step 8:** The section's menu (162 high, 5 items, no separator): header "Local branches"; "Create branch…" (highlighted); "Show reflog"; "Show backups"; "Show all hidden branches" (disabled: "No branch is hidden"); "Stop soloing". After "Stop soloing": the log call with the unfiltered revisions; FL0; no solo button and no row in the disabled colour; the session's `GraphFilters` holds no entry for the repository's path (FD14). The menu opened again: "Show all hidden branches" (disabled: "No branch is hidden"), "Stop soloing" (disabled: "No branch is soloed").<br>**After step 9:** FL1 as after step 3; when the app closes, the session holds one `GraphFilters` entry for this copy's path with step 3's Hidden, Soloed, HiddenRemotes, Upstreams and Remotes (LastUsed not asserted).<br>**After step 10:** The new instance's first `git log --date-order …` call has step 3's hide revisions (the filter comes from the session, so git starts before the refs are read, 8.2): FL1 from its first rows, without a reload; the left panel (folders opened by the step) marks both alpha rows as after step 3; the session's entry for the repository has the same Hidden, Soloed, HiddenRemotes, Upstreams and Remotes as after step 9 (LastUsed not asserted: `RepositoryGraphFilter.LastUsed` is also when the repository was last opened, so the new instance may write it).<br>**After step 11:** The eye button showed the branch with its upstream (8.1: showing it undoes both): the unfiltered log call, FL0, no eye button; the session's `GraphFilters` holds no entry for the repository's path (FD14). The press on the button started no branch drag and selected nothing (`RefDrag.IsExcluded`, 8.3): nothing selected, the details panel's placeholder. | Not run yet | RW: part C: the real click on the folder, the real right-click on Local branches › feature › alpha and a click on "Hide feature/alpha" (`ContextMenuItem` by name), the window closed by its close button (the process ends and saves the session), the app launched again on the same data folder (`RealApp.Launch`), the real clicks opening both folders, the capture compared with `f14j-after-restart`; the real click on the eye button, the capture compared with `f14k-shown-by-toggle`; `session.json` read between the two processes. What only the real window shows: the filter kept across a real process restart (D159) and a real press on the toggle that starts no drag | `f14a-filters-folders-open`, `f14b-current-branch-menu`, `f14c-alpha-menu`, `f14d-alpha-hidden`, `f14e-hidden-row-tooltip`, `f14f-compare-hidden`, `f14g-alpha-shown`, `f14h-beta-soloed`, `f14i-section-menu`, `f14j-after-restart`, `f14k-shown-by-toggle`; real window: `f14j-after-restart`, `f14k-shown-by-toggle` | Design 8.1 to 8.4, 6.4, 12.4, 13.1; D159, D178, D114; FD13, FD14, FD16, FD19, FD20. |
| F15 | Stashes drawn dashed: phase 1's graph scenario's stash and the filters scenario's two, one of whose lines crosses ten rows in one unbroken pattern; every other line solid; "graph" and "filters", dark, 1100×700 (`Check_F15_graph_scenario_stash_dashed`, `Check_F15_filters_stashes_dashed`, one start each) | 1. Phase 1's "graph" scenario (`Scenarios.GraphAsync`). Screenshot. 2. "Filters" scenario (a new start). Screenshot. | **After step 1:** GS0: phase 1's 13 rows, 3 lanes, labels, lanes and colours unchanged (`AssertGraph` with phase 1's table), `stash@{0}` (`95daa96`) at row 0 a ring in lane 0, colour 0 (#7C8CFF, the row's background inside: `AssertRing` at (406, 129)). In the layout (`GraphRows.Data.RowAt(r).Lines`) row 0's OutOfNode 0→0, row 1's PassThrough 0→0 and row 2's IntoNode 0→0 (all colour 0) have `IsDashed`; no other line of rows 0 to 12 has it, row 2's OutOfNode 0→0 included (from the base, Add settings page, the lane is the base's own and solid, 10). In the picture: at x 406 from y 136 to 175 (FD15: 2 below the ring's bottom at 134, 1 above the base node's top at 176, no other line curving into row 2's node; across the row boundaries at y 142 and 168) dashes and gaps of 3 (FD15: at least 5 dashes); solid: x 406 from y 187 to 219 all #7C8CFF (lane 0 below Add settings page) and x 422 from y 161 to 219 all #3FB97F (lane 1 below Highlight matches). No working-changes row.<br>**After step 2:** FL0, with rings at (406, 129) in colour 0 (#7C8CFF) and (422, 155) in colour 1 (#3FB97F). In the layout `IsDashed` is set on lane 0's lines from row 0's ring to row 11's node (row 0's OutOfNode, rows 1 to 10's PassThrough, row 11's IntoNode 0→0) and on lane 1's from row 1's ring to row 2's node (row 1's OutOfNode, row 2's IntoNode 1→1), and on no other line (row 2's OutOfNode 1→1, row 11's OutOfNode 0→0 and IntoNode 2→0 among them). In the picture: x 406 from y 136 to 404 dashed across the boundaries of rows 1 to 10 in one unbroken pattern (the dashes' starts 6 ± 1 apart throughout, at least 40 dashes): the line drawn row by row keeps its phase (10); the range ends 2 below row 11's top (402), not 1 above Alpha one's node (410), because lane 2's IntoNode curve comes into that node in row 11 (FD15); x 422 from y 162 to 175 dashed (2 below the ring's bottom at 160, 1 above Beta six's top at 176; at least 2 dashes); solid: x 422 from y 187 to 201 all #3FB97F (Beta six to Beta five) and x 406 from y 421 to 452 all #7C8CFF (below Alpha one). | Not run yet | n/a: the dash pattern is sampled pixel by pixel, which the real window's display scaling resamples; the real window pictures dashed stash lines in phase 1's `01-graph-dark-1100x700` and in P25 | `f15a-graph-stash`, `f15b-filters-stashes` | Design 10, 14.1; D161 (C12's "drawn … joined to its commit", D153); FD15. |
| U16 | Undo and redo of the stage panel's writes: a commit and an amend (the message back in the boxes, cleared again by Redo), a discard of a file, of a hunk (the other hunk kept on redo), of lines and of all, the restore bar going and coming back and its Restore as the discard's Undo, the bar's Restore recorded as an entry of its own, D75's question and a recorded Restore when the file changed since the discard (UD18), and two entries undone and two redone in a row; "history", dark, 1100×700 | Part A (`Check_U16_commit`): 1. "History" scenario. 2. Click the working-changes row (468, 129); click "Stage all" (`StageAllButton`); click `CommitSummary` and type `Describe the reader and add Length`; click `CommitDescription` and type `The summary says lines now.`; click `CommitButton` ("Commit 1 file"). Wait until idle. 3. Rest the pointer on `UndoButton`. Screenshot. 4. Click Undo. 5. Click the working-changes row. Screenshot. 6. Click the row "Read whole lines" (display row 1, (468, 155)). Click Redo. Screenshot.<br>Part B (`Check_U16_amend`): 7. "History". Click the working-changes row; click "Stage all"; tick `AmendCheckBox`; click `CommitButton` ("Amend previous commit"). Wait until idle. 8. Click Undo. Click the working-changes row. Screenshot. 9. Click the row "Read whole lines" (display row 1). Click Redo. Screenshot.<br>Part C (`Check_U16_discard_file`; before the app starts, outside it, `docs/todo.md` "# To do\n" written, untracked; parts D, E, G and H start the same way): 10. Click the working-changes row. Rest the pointer on the unstaged row `src/Reader.cs` and click its `RowDiscardButton`, then "Discard" in the dialog. Wait until idle. 11. Click Undo. Screenshot. 12. Click Redo. Screenshot. 13. Click the restore bar's "Restore" (`RestoreDiscardButton`). Wait until idle. Screenshot. 14. Discard `src/Reader.cs` again as in step 10. Wait until idle. Outside the app, append the line `// Changed outside.` (with its line feed) to `src/Reader.cs`; wait until the stage panel lists M `Reader.cs` `src` under Unstaged files again. 15. Click the restore bar's "Restore". Wait for the question. Screenshot. Click "Restore" (`ConfirmButton`). Wait until idle.<br>Part D (`Check_U16_discard_hunk`): 16. Click the working-changes row, then the unstaged `src/Reader.cs` (its diff opens over the graph). Click the second hunk's "Discard hunk" (the second `DiscardHunkButton` in order, as phase 2's check 19), then "Discard". Wait until idle. Screenshot. 17. Click Undo. Screenshot. 18. Click Redo. Screenshot.<br>Part E (`Check_U16_discard_lines`): 19. Open the diff as in step 16. Click the line number 23 (the added `    public int Length => _text.Length;`), then "Discard lines" (`DiscardLinesButton`), then "Discard". Wait until idle. 20. Click Undo. Screenshot. 21. Click Redo. Screenshot.<br>Part F (`Check_U16_discard_all`; before the app starts: `docs/todo.md` as part C, and `README.md` written as "# History", "", "A repository for the history checks.", "", "Staged." (each line ending in a line feed) and staged with `git add README.md`): 22. Click the working-changes row; click "Discard all" (`DiscardAllButton`), then "Discard". Wait until idle. 23. Click Undo. Screenshot. 24. Click Redo. Screenshot.<br>Part G (`Check_U16_restore_recorded`): 25. Discard `src/Reader.cs` as in step 10. 26. Right-click the row "Read whole lines" (display row 1) in its Message column, click "Create branch here…", type `keep`, untick `NameDialogOption`, click "Create branch". Wait until idle. 27. Click the working-changes row; click the restore bar's "Restore". Wait until idle. 28. Click Undo. Screenshot. 29. Click Redo. Screenshot.<br>Part H (`Check_U16_many_steps`): 30. Discard `src/Reader.cs` as in step 10, then create `keep` as in step 26, waiting until idle after each. 31. Click Undo twice, waiting until idle after each. Screenshot. 32. Click Redo twice, waiting until idle after each. Screenshot. | **After step 1:** H0 with its working-changes row; nothing selected; T0.<br>**After step 2:** the stage panel had Staged files (1) M `Reader.cs` `src` and "Commit 1 file"; the commit ran (`git commit --file=- --cleanup=whitespace`; a stage-panel write: the status stays "Ready"). `main` is `8d845e8`: parent `c0bc2dd`, Test Author, author and commit date 2026-01-02 09:00, message "Describe the reader and add Length", a blank line, "The summary says lines now.". `git status -sb` prints `## main...origin/main [ahead 1]` and nothing else. The graph is H-U1, its row 0 selected with its details shown (phase 2's rule when nothing is left to commit); the boxes are empty; T+ `commit "Describe the reader and add Length"`.<br>**After step 3:** the tooltip `Undo commit "Describe the reader and add Length"` is open under the button, inside the window.<br>**After step 4:** Undo ran. The call log has `git update-ref -m VisualCommit: undo commit "Describe the reader and add Length" --stdin` with the recorded input `update refs/heads/main c0bc2dd… 8d845e8…`, and no `read-tree` (a soft move, 7.3, written in the step's transaction, UD20). Git: `main` at `c0bc2dd`; `## main...origin/main` and `M  src/Reader.cs` (the index holds blob `3056624`, the working file is unchanged); the newest entry of `main`'s and of HEAD's reflog is `VisualCommit: undo commit "Describe the reader and add Length"`; `8d845e8` still exists (`main@{1}`). The graph is H0 with its working-changes row; nothing is selected (the selected commit left the graph); T− `commit "Describe the reader and add Length"`.<br>**After step 5:** the working-changes row is selected; the stage panel: "No unstaged changes" under Unstaged files (0); Staged files (1): M `Reader.cs` `src`; `CommitSummary` "Describe the reader and add Length", `SummaryCounter` "38"; `CommitDescription` "The summary says lines now." (7.5: both boxes were empty); `AmendCheckBox` unticked; `CommitButton` "Commit 1 file", enabled.<br>**After step 6:** Redo ran (`git update-ref -m VisualCommit: redo commit "Describe the reader and add Length" --stdin` with `update refs/heads/main 8d845e8… c0bc2dd…`). Git as after step 2; the reflogs' newest entry `VisualCommit: redo commit "Describe the reader and add Length"`. The graph is H-U1 with "Read whole lines" (row 1) still selected; no working-changes row; the stage panel's summary and description are empty again (`WorkingChangesViewModel.Summary` and `.Description`: they still held the filled message); T+ `commit "Describe the reader and add Length"`.<br>**After step 7:** ticking Amend filled the summary with "Read whole lines" (no description: the commit has no body). `main` is `bd74e66`: parent `20d7fbe`, author Jonas Berg <jonas@example.com> 2026-01-01 12:13, committer Test Author 2026-01-02 09:00, message "Read whole lines"; HEAD's reflog's newest entry `commit (amend): Read whole lines`; `## main...origin/main [ahead 1, behind 1]`; the graph H-U2; T+ `amend "Read whole lines"`.<br>**After step 8:** Undo ran (`git update-ref -m VisualCommit: undo amend "Read whole lines" --stdin` with `update refs/heads/main c0bc2dd… bd74e66…`): `main` at `c0bc2dd`, `## main...origin/main`, `M  src/Reader.cs`; the graph H0 with its working-changes row; T− `amend "Read whole lines"`. The stage panel: Staged files (1) M `Reader.cs` `src`; `CommitSummary` "Read whole lines" (`SummaryCounter` "56"); `CommitDescription` empty; Amend unticked; "Commit 1 file".<br>**After step 9:** Redo ran (`git update-ref -m VisualCommit: redo amend "Read whole lines" --stdin` with `update refs/heads/main bd74e66… c0bc2dd…`): `main` `bd74e66` again, clean; H-U2 with "Read whole lines" `c0bc2dd` (row 1) still selected; the summary empty again; T+ `amend "Read whole lines"`.<br>**After step 10:** the dialog was phase 2's ("Discard changes?", "Discard the changes to Reader.cs?"). The stage panel: the restore bar `Discarded changes to Reader.cs.`; Unstaged files (1): A `todo.md` `docs`; Staged files (0). Git: `## main...origin/main` and `?? docs/todo.md`; `src/Reader.cs` is the committed text (blob `7061deb`); one backup ref, `refs/visualcommit/backup/discard-20260102T090001000Z`, at a commit with parent `c0bc2dd`, author VisualCommit <snapshot@visualcommit.invalid>, subject `VisualCommit: snapshot before discarding 1 file` and body `src/Reader.cs`, whose `src/Reader.cs` is blob `3056624` (`0bd36b6` as plain git makes it). T+ `discard of Reader.cs`.<br>**After step 11:** Undo ran; the call log has `git restore --source=<the snapshot's full id> --worktree -- src/Reader.cs`. `src/Reader.cs` is blob `3056624` again (` M src/Reader.cs`, `?? docs/todo.md`); the restore bar is gone (7.9); Unstaged files (2): A `todo.md` `docs`, M `Reader.cs` `src`; the backup ref stays; T− `discard of Reader.cs`.<br>**After step 12:** Redo ran: `src/Reader.cs` is the committed text again, written from the recorded bytes (no `git restore` among the step's calls); the restore bar shows `Discarded changes to Reader.cs.` again, for the same snapshot (still one backup ref); Unstaged files (1); T+ `discard of Reader.cs`.<br>**After step 13:** no question (the file has not changed since the discard); the bar's Restore ran as the entry's Undo (7.9): the operation `Undo discard of Reader.cs`, its log line, the status `Undid discard of Reader.cs`; `src/Reader.cs` blob `3056624`; the bar gone; Unstaged files (2); T− `discard of Reader.cs` (no new entry: Undo says `Nothing to undo`).<br>**After step 14:** the discard ran as at step 10 and was recorded as a new entry, which drops the undone one (7.1): T+ `discard of Reader.cs`; the bar `Discarded changes to Reader.cs.` for a second snapshot, `refs/visualcommit/backup/discard-20260102T090002000Z`, the same commit as the first (`0bd36b6` as plain git makes it: the same file, parent, message and D72 dates); `src/Reader.cs` the committed text. After the outside write `src/Reader.cs` is the committed text followed by the line `// Changed outside.` (blob `597f7de`): ` M src/Reader.cs`, `?? docs/todo.md`; Unstaged files (2): A `todo.md` `docs`, M `Reader.cs` `src`; the bar still shows (D62); Undo still enabled, `Undo discard of Reader.cs` (file hashes are checked only inside a step, 7.4).<br>**After step 15:** the click ran no `Undo …` operation: the discard is the entry before the cursor, but its file changed since (UD18). D75's question showed (phase 2's confirmation card, the picture): `ConfirmTitle` `Restore discarded changes?`, `ConfirmQuestion` `Reader.cs has changed since the discard. Put back the version from before the discard?`, `ConfirmDetail` `A snapshot of the newer changes is saved first.`, `ConfirmButton` `Restore`. After "Restore": one operation, phase 2's Restore, recorded as an entry of its own. A third backup ref, `refs/visualcommit/backup/discard-20260102T090003000Z`, at a commit with parent `c0bc2dd`, author VisualCommit <snapshot@visualcommit.invalid>, subject `VisualCommit: snapshot before discarding 1 file` and body `src/Reader.cs`, whose `src/Reader.cs` is the newer file, blob `597f7de` (`d76813b` as plain git makes it); `src/Reader.cs` blob `3056624` again (` M src/Reader.cs`, `?? docs/todo.md`); the bar gone; the status "Ready" (a stage-panel write); T+ `restore of the discarded files`.<br>**After step 16:** the diff showed `@@ -1,6 +1,6 @@` and `@@ -19,4 +19,6 @@`; the dialog said "Discard this hunk of Reader.cs?". After Discard the diff shows only `@@ -1,6 +1,6 @@` (line 3 `/// <summary>Reads the lines of a text.</summary>`); `src/Reader.cs` is blob `f4e302e` (the committed text with line 3 changed); the restore bar `Discarded changes to Reader.cs.`; one backup ref `discard-20260102T090001000Z`, its `src/Reader.cs` blob `3056624`; T+ `discard of lines in Reader.cs`.<br>**After step 17:** Undo ran (`git restore --source=<the snapshot> --worktree -- src/Reader.cs`): the diff, still open, shows both hunks again; the file is blob `3056624`; no bar; T− `discard of lines in Reader.cs`.<br>**After step 18:** Redo ran: the file is blob `f4e302e` again, the first hunk kept (the redo writes the bytes the discard left, not the committed text, 7.3); the diff shows only `@@ -1,6 +1,6 @@`; the bar shows again; T+ `discard of lines in Reader.cs`.<br>**After step 19:** the dialog said "Discard 1 selected line of Reader.cs?". After Discard the file is blob `14bf8a8`, the working text without line 23 (23 lines; the added empty line 22 stays); the diff shows `@@ -1,6 +1,6 @@` and `@@ -19,4 +19,5 @@` with the one added empty line 22; the bar `Discarded changes to Reader.cs.`; T+ `discard of lines in Reader.cs`.<br>**After step 20:** Undo ran: the file blob `3056624`, both hunks as at the start, no bar; T− `discard of lines in Reader.cs`.<br>**After step 21:** Redo ran: the file blob `14bf8a8`, the diff as after step 19, the bar back; T+ `discard of lines in Reader.cs`.<br>**After step 22:** before the click the stage panel listed Unstaged files (2): A `todo.md` `docs`, M `Reader.cs` `src`, and Staged files (1): M `README.md`; the dialog said "Discard the changes to 2 files?". After Discard: "No unstaged changes"; Staged files (1) M `README.md`; the bar `Discarded changes to 2 files.`. Git: `## main...origin/main` and `M  README.md` only (`docs/todo.md` deleted, `src/Reader.cs` the committed text, the index untouched); one backup ref `discard-20260102T090001000Z` at a commit (parent `c0bc2dd`) with subject `VisualCommit: snapshot before discarding 2 files` and the body lines `docs/todo.md` and `src/Reader.cs`, holding `docs/todo.md` "# To do\n" and `src/Reader.cs` blob `3056624`; T+ `discard of 2 files`.<br>**After step 23:** Undo ran (`git restore --source=<the snapshot> --worktree -- docs/todo.md src/Reader.cs`): `M  README.md`, ` M src/Reader.cs`, `?? docs/todo.md`, every file's bytes as at the start, the index unchanged (README.md's staged lines); no bar; the stage panel as before step 22; T− `discard of 2 files`.<br>**After step 24:** Redo ran: `docs/todo.md` deleted and `src/Reader.cs` the committed text again (the recorded state: absent, and blob `7061deb`); `M  README.md` only; the bar `Discarded changes to 2 files.`; T+ `discard of 2 files`.<br>**After step 26:** status "Created keep"; `refs/heads/keep` at `c0bc2dd`, no upstream; HEAD still on `main`; the bar still shows (D62: it stays until closed or replaced); T+ `creation of keep` (the discard is now the older entry).<br>**After step 27:** no question; the bar's Restore ran as phase 2's (the discard is not the entry before the cursor, 7.9) and was recorded: `src/Reader.cs` blob `3056624`, the bar gone, the status "Ready" (a stage-panel write); T+ `restore of the discarded files`.<br>**After step 28:** Undo ran: `src/Reader.cs` is the committed text again (the bytes the restore found, written back: no `git restore` among the step's calls); `?? docs/todo.md` only; the bar stays closed; `keep` unchanged. Undo enabled, `Undo creation of keep`; Redo enabled, `Redo restore of the discarded files`.<br>**After step 29:** Redo ran (`git restore --source=<the discard's snapshot> --worktree -- src/Reader.cs`): the file blob `3056624`, ` M src/Reader.cs`; no bar; T+ `restore of the discarded files`.<br>**After step 30:** the bar `Discarded changes to Reader.cs.`; `src/Reader.cs` the committed text, `?? docs/todo.md` only; one backup ref, `discard-20260102T090001000Z`; `refs/heads/keep` at `c0bc2dd`, HEAD still on `main`; T+ `creation of keep` (the discard is the older entry).<br>**After step 31:** two steps ran, newest entry first: `Undo creation of keep` (the `update-ref --stdin` with `delete refs/heads/keep c0bc2dd…`), then `Undo discard of Reader.cs` (`git restore --source=<the snapshot> --worktree -- src/Reader.cs`), each with its log line; the status `Undid discard of Reader.cs`. Git: ` M src/Reader.cs` (blob `3056624`), `?? docs/todo.md`, no `refs/heads/keep` and no reflog of it; the backup ref stays; no bar (7.9); Undo disabled, `Nothing to undo`; Redo enabled, `Redo discard of Reader.cs`.<br>**After step 32:** two steps ran: `Redo discard of Reader.cs` (the file the committed text again, blob `7061deb`, written from the recorded bytes; the bar `Discarded changes to Reader.cs.` shown again for the same snapshot, 7.9), then `Redo creation of keep` (the `update-ref --stdin` with `create refs/heads/keep c0bc2dd…`: `keep` at `c0bc2dd`, its reflog one entry `VisualCommit: redo creation of keep`); `?? docs/todo.md` only; still one backup ref; the status `Redid creation of keep`; T+ `creation of keep` (the cursor after both entries). | Not run yet | RW: part A, steps 2 to 6, with the real mouse and keyboard (the boxes typed with the real keyboard; `UndoButton` and `RedoButton` clicked by automation id): the tooltip of step 3 found in the window's UI Automation tree (`RealApp.WaitForToolTip`) and the capture compared with `u16a-undo-tooltip`; the captures after steps 5 and 6 compared with `u16b-commit-undone` and `u16c-commit-redone`; each button's `HelpText` read after every step (T+, T−); git's state asserted as in the scripted run. Parts B to H are scripted only: the same two buttons, and the stage panel, the restore bar and the diff are pictured in the real window by phase 2's checks | `u16a-undo-tooltip`, `u16b-commit-undone`, `u16c-commit-redone`, `u16d-amend-undone`, `u16e-amend-redone`, `u16f-discard-undone`, `u16g-discard-redone`, `u16h-bar-restore-undid`, `u16i-restore-question`, `u16j-hunk-discarded`, `u16k-hunk-undone`, `u16l-hunk-redone`, `u16m-lines-undone`, `u16n-lines-redone`, `u16o-all-undone`, `u16p-all-redone`, `u16q-restore-undone`, `u16r-restore-redone`, `u16s-two-undone`, `u16t-two-redone`; real window: `u16a-undo-tooltip`, `u16b-commit-undone`, `u16c-commit-redone` | Design 1.1, 7.2, 7.3 (Commit and amend; Discard; Restore), 7.5 (the message put back), 7.6, 7.9, 12.2; D62, D75, D154, D168, D172. The row clicked before Redo (steps 6 and 9) is one that stays in the graph, so that the expected selection does not depend on where a vanishing working-changes row leaves it. Decisions UD1 to UD4, UD18 and UD20. Steps 31 and 32 leave "Read whole lines" selected (step 30's right-click on it creates `keep`), so `u16s-two-undone` and `u16t-two-redone` show its details, not the stage panel: the restore bar of those steps ("no bar", then shown again for the same snapshot) is asserted on the stage panel's state, `WorkingChangesViewModel.RestoreText`, which it keeps while a commit is selected, and is not pictured. |
| U17 | Undo and redo of resets: soft, mixed with nothing staged, mixed with staged, partly staged and untracked work, and hard with the files and the index back from its snapshot (its redo saving a new one); "ops", dark, 1100×700 | Part A (`Check_U17_soft_reset`): 1. "Ops" scenario. Right-click the row "Add core" (row 6, (484, 285)) in its Message column, click "Reset main to this commit…", click `ResetSoft`, then "Reset". Wait until idle. 2. Click Undo. Screenshot. 3. Click Redo. Screenshot.<br>Part B (`Check_U17_mixed_reset`): 4. As step 1, leaving Mixed checked. 5. Click Undo. Screenshot. 6. Click Redo. Screenshot.<br>Part C (`Check_U17_mixed_reset_with_staged_changes`; before the app starts, outside it, phase 3's check 10 part C changes: `src/core.txt` "core", "main work", "staged line" staged, then "core", "main work", "staged line", "unstaged line" not staged; `README.md` "# Operations", "", "Local edit."; `scratch.txt` "scratch" staged; `todo.txt` "todo" untracked; every line ending in a line feed; part D starts the same way): 7. Right-click "Add core" (display row 7, (484, 311)), click "Reset main to this commit…", then "Reset" (Mixed). Wait until idle. 8. Click Undo. Click the working-changes row. Screenshot. 9. Click Redo. Screenshot.<br>Part D (`Check_U17_hard_reset`): 10. Right-click "Add core" (display row 7), click "Reset main to this commit…", click `ResetHard`, then "Reset". Wait until idle. 11. Click Undo. Click the working-changes row. Screenshot. 12. Click Redo. Screenshot. | **After step 1:** phase 3's soft reset: status "Reset main to 8beca34 (soft)"; `main` `8beca34`; `## main...origin/main [behind 1]` and `M  src/core.txt`; the graph G6; "Add core" (display row 7) selected; one backup ref, `refs/visualcommit/backup/reset-20260102T090001000Z` → `88d6b91`, no `-changes`; T+ `reset of main to 8beca34 (soft)`.<br>**After step 2:** Undo ran; the call log has `git update-ref -m VisualCommit: undo reset of main to 8beca34 (soft) --stdin` with the recorded input `update refs/heads/main 88d6b91… 8beca34…` (UD20), and no `read-tree` or `restore`. Git: `main` `88d6b91`; `## main...origin/main` and nothing else (the index already held Main work's tree); HEAD's reflog's newest entry `VisualCommit: undo reset of main to 8beca34 (soft)`; the backup stays. G0, "Add core" (row 6) still selected; T−.<br>**After step 3:** Redo ran (`update-ref -m VisualCommit: redo reset of main to 8beca34 (soft) --stdin` with `update refs/heads/main 8beca34… 88d6b91…`): as after step 1, and still one backup ref; T+.<br>**After step 4:** status "Reset main to 8beca34 (mixed)"; `## main...origin/main [behind 1]`, ` M src/core.txt`; G6; one backup `reset-20260102T090001000Z` → `88d6b91` and no `-changes` (the index equalled HEAD: nothing to keep, 7.3); T+ `reset of main to 8beca34 (mixed)`.<br>**After step 5:** Undo ran: `git restore --staged --source=88d6b91… -- .` (no snapshot: the source is the commit moved back to), then the transaction `git update-ref -m VisualCommit: undo reset of main to 8beca34 (mixed) --stdin` with `update refs/heads/main 88d6b91… 8beca34…` (the index is written at its place, the ref transaction at the step's end, UD20). `## main...origin/main`, clean; G0; T−.<br>**After step 6:** Redo ran: `git restore --staged --source=8beca34… -- .` first, then the transaction `git update-ref -m VisualCommit: redo reset of main to 8beca34 (mixed) --stdin` with `update refs/heads/main 8beca34… 88d6b91…`; as after step 4; T+.<br>**Before step 7:** G0+w; the stage panel (phase 3's check 10 step 7): Unstaged files (3): M `README.md`, M `core.txt` `src`, A `todo.txt`; Staged files (2): A `scratch.txt`, M `core.txt` `src`. Git: ` M README.md`, `A  scratch.txt`, `MM src/core.txt`, `?? todo.txt`.<br>**After step 7:** status "Reset main to 8beca34 (mixed)"; `## main...origin/main [behind 1]`, ` M README.md`, ` M src/core.txt`, `?? scratch.txt`, `?? todo.txt` (the index is Add core's, the working tree untouched); G6; two backup refs: `reset-20260102T090001000Z` → `88d6b91` and `reset-20260102T090001000Z-changes` → a commit with the one parent `88d6b91` and subject `VisualCommit: index before a mixed reset`, whose tree is the index before the reset: `README.md` "# Operations", `scratch.txt` "scratch", `src/core.txt` "core", "main work", "staged line" (`e947640` as plain git makes it); T+ `reset of main to 8beca34 (mixed)`.<br>**After step 8:** Undo ran: `git restore --staged --source=<the -changes commit's full id> -- .`, then the transaction (`… --stdin` with `update refs/heads/main 88d6b91… 8beca34…`). Git: `## main...origin/main`, ` M README.md`, `A  scratch.txt`, `MM src/core.txt`, `?? todo.txt`: the index's `src/core.txt` holds the three staged lines and `scratch.txt` is staged again; the working tree is unchanged ("Local edit.", the four lines, `todo.txt` "todo"). The stage panel lists exactly the files listed before step 7; G0+w; both backups stay; T−.<br>**After step 9:** Redo ran: `git restore --staged --source=8beca34… -- .`, then the transaction (`update refs/heads/main 8beca34… 88d6b91…`); as after step 7, no new backup; T+.<br>**After step 10:** phase 3's hard reset: status "Reset main to 8beca34 (hard)"; `## main...origin/main [behind 1]` and `?? todo.txt` only; `README.md` "# Operations", `src/core.txt` "core", `scratch.txt` gone, `todo.txt` "todo"; G6 (its working-changes row for `todo.txt`); two refs: `reset-20260102T090001000Z` → `88d6b91` and `…-changes` → the snapshot, whose first parent is `88d6b91` and second a commit (parent `88d6b91`) `VisualCommit: index before a hard reset` holding the index (`a0351fc`); subject `VisualCommit: snapshot before a hard reset to 8beca34`, body `README.md`, `scratch.txt`, `src/core.txt`; its tree has `README.md` with "Local edit.", `scratch.txt` "scratch", `src/core.txt` with the four lines, and no `todo.txt` (`dfd67f8`); T+ `reset of main to 8beca34 (hard)`.<br>**After step 11:** Undo ran, in this order (7.3): `git update-index -q --refresh`; `git read-tree -m -u 8beca34… 88d6b91…` (exit 0); `git update-ref -m VisualCommit: undo reset of main to 8beca34 (hard) refs/heads/main 88d6b91… 8beca34…`; `git restore --staged --source=<the index commit's full id> -- .`; `git restore --source=<the snapshot's full id> --worktree -- README.md scratch.txt src/core.txt`. Git: `## main...origin/main`, ` M README.md`, `A  scratch.txt`, `MM src/core.txt`, `?? todo.txt`; every file's bytes and every index entry as before step 10 (`git diff --cached`: `scratch.txt` added, `src/core.txt` + "staged line"; `git diff`: `README.md` + "", "Local edit.", `src/core.txt` + "unstaged line"); `todo.txt` untouched. The stage panel as before step 10; G0+w; "Add core" (display row 7) still selected; T−.<br>**After step 12:** Redo ran: the app's own hard reset with a new snapshot (7.3): two more refs, `reset-20260102T090002000Z` → `88d6b91` and `reset-20260102T090002000Z-changes` → the same snapshot commit as the first (the same content, parents and D72 date give the same id; the entry takes it, 7.5 step 5); then `git reset --hard 8beca34… --`, whose reflog entry is git's own `reset: moving to 8beca34…` (the redo is a reset, not an `update-ref`). The state as after step 10; four backup refs; T+ `reset of main to 8beca34 (hard)`. | Not run yet | n/a: the reset dialog and G6 are pictured in the real window by phase 3; what these steps add is git's state and the stage panel, which both harnesses read alike, and a click on Undo or Redo, which U16 makes in the real window | `u17a-soft-undone`, `u17b-soft-redone`, `u17c-mixed-undone`, `u17d-mixed-redone`, `u17e-mixed-staged-undone`, `u17f-mixed-staged-redone`, `u17g-hard-undone`, `u17h-hard-redone` | Design 7.3 (the reset rows, `IndexTo`, the Keep move), 7.5 step 5, 0.4; D67, D139, D169, D171. Phase 3's check 10 for the dialog and the changes. Decisions UD5 and UD20. |
| U18 | Undo and redo of branch operations: create, create and check out, rename, delete (its upstream back), a checkout of a branch, a checkout that created a tracking branch, a checkout of a commit, a checkout that left a detached HEAD's commit, a fast-forward of a branch not checked out, D91's "Stash changes and check out"; "ops", "edges", "blocked", dark, 1100×700 | Part A (`Check_U18_create_branch`): 1. "Ops" scenario. Right-click the row "Main work" (row 3, (484, 207)) in its Message column, click "Create branch here…", type `hotfix`, untick `NameDialogOption`, click "Create branch". Wait until idle. 2. Click Undo. Screenshot. 3. Click Redo. Screenshot.<br>Part B (`Check_U18_create_and_check_out`): 4. As step 1, leaving `NameDialogOption` ticked. 5. Click Undo. Screenshot. 6. Click Redo. Screenshot.<br>Part C (`Check_U18_rename`): 7. "Ops". Right-click the label `feature/conflict` (row 1), click "Rename feature/conflict…", select all in `NameBox` and type `feature/conflicting`, click "Rename". Wait until idle. 8. Click Undo. Screenshot. 9. Click Redo. Screenshot.<br>Part D (`Check_U18_delete_with_upstream`): 10. "Ops". Right-click the label `feature/alpha` (row 4), click "Delete feature/alpha…", then "Delete". Wait until idle. 11. Click Undo. Screenshot. 12. Click Redo. Screenshot.<br>Part E (`Check_U18_check_out_branch`): 13. "Ops". Click the folder Local branches › `feature`, then double-click its `ff`. Wait until idle. 14. Click Undo. Screenshot. 15. Click Redo. Screenshot.<br>Part F (`Check_U18_tracking_branch`; before the app starts, outside it, `git branch --delete --force feature/alpha`, as phase 3's check 6): 16. Click the folder Remotes › origin › `feature`, then double-click its `alpha`. Wait until idle. 17. Click Undo. Screenshot. 18. Click Redo. Screenshot.<br>Part G (`Check_U18_check_out_commit`): 19. "Ops". Right-click the row "Add core" (row 6, (484, 285)), click "Check out this commit". Wait until idle. 20. Click Undo. Screenshot. 21. Click Redo. Screenshot.<br>Part H (`Check_U18_leave_detached_commit`): 22. "Edges" scenario. Double-click `main` under Local branches; click "Check out" in the question. Wait until idle. 23. Click Undo. Screenshot. 24. Click Redo. Screenshot.<br>Part I (`Check_U18_fast_forward`): 25. "Edges". Right-click `loc` under Local branches, click "Fast-forward loc to main". Wait until idle. 26. Click Undo. Screenshot. 27. Click Redo. Screenshot.<br>Part J (`Check_U18_stash_and_check_out`): 28. "Blocked" scenario. Click the folder Local branches › `feature`, double-click its `other`; in the error dialog click "Stash changes and check out" (`ErrorExtraButton`). Wait until idle. 29. Click Undo. Screenshot. 30. Click Redo. Screenshot. | **After step 1:** status "Created hotfix"; the app ran `git branch hotfix 88d6b91…`; `refs/heads/hotfix` `88d6b91`, no `branch.hotfix.*` key; HEAD on `main`; "Main work" selected; G0 hotfix; T+ `creation of hotfix`.<br>**After step 2:** Undo ran: the call log has `git update-ref -m VisualCommit: undo creation of hotfix --stdin` with the recorded input `delete refs/heads/hotfix 88d6b91…` (design 2.3), and no `config` call (the branch had no keys). Git: no `refs/heads/hotfix` and no reflog of it (`git reflog exists refs/heads/hotfix` exits 1); `## main...origin/main`. G0, "Main work" still selected; Local branches 4; T−.<br>**After step 3:** Redo ran (`update-ref … --stdin` with `create refs/heads/hotfix 88d6b91…`): `hotfix` at `88d6b91`, its reflog one entry `VisualCommit: redo creation of hotfix`; G0 hotfix; T+.<br>**After step 4:** status "Created hotfix and checked it out"; `git switch --create hotfix 88d6b91…`; `## hotfix`; `CurrentBranch` `hotfix`; G0 hotfix✓; T+ `creation of hotfix`.<br>**After step 5:** Undo ran, in this order (7.3): `git switch --no-guess --no-overwrite-ignore main`, then the `update-ref --stdin` with `delete refs/heads/hotfix 88d6b91…` (a branch is deleted only after HEAD left it). `## main...origin/main`; `CurrentBranch` `main`; HEAD's reflog's newest entry `checkout: moving from hotfix to main` (git's own); no `hotfix`; G0; T−.<br>**After step 6:** Redo ran: the `update-ref --stdin` with `create refs/heads/hotfix 88d6b91…` first, then `git switch --no-guess --no-overwrite-ignore hotfix`; `## hotfix`; HEAD's reflog's newest entry `checkout: moving from main to hotfix`; G0 hotfix✓; T+.<br>**After step 7:** status "Renamed feature/conflict to feature/conflicting"; "Conflicting change" (row 1) selected; G0 conflicting; T+ `rename of feature/conflict to feature/conflicting`.<br>**After step 8:** Undo ran: `git branch --move feature/conflicting feature/conflict`; `git branch` lists `feature/alpha`, `feature/conflict`, `feature/ff`, `* main`; `feature/conflict`'s reflog (moved with the branch) has the newest entry `Branch: renamed refs/heads/feature/conflicting to refs/heads/feature/conflict`; G0, row 1 still selected; T−.<br>**After step 9:** Redo ran: `git branch --move feature/conflict feature/conflicting`; G0 conflicting; T+.<br>**After step 10:** the confirmation was phase 3's: "Delete branch?", "Delete the branch feature/alpha? It has commits that main does not have.". After Delete: status "Deleted feature/alpha"; backup `refs/visualcommit/backup/branch-20260102T090001000Z/feature/alpha` → `ce5ea6a`; `git branch --delete --force feature/alpha` ran; no `branch.feature/alpha.*` key and no reflog; G0 −alpha (its commits stay: `origin/feature/alpha` reaches them); Local branches 3; T+ `deletion of feature/alpha`.<br>**After step 11:** Undo ran: `git update-ref -m VisualCommit: undo deletion of feature/alpha --create-reflog --stdin` with `create refs/heads/feature/alpha ce5ea6a…`, then `git config --local --add branch.feature/alpha.remote origin` and `git config --local --add branch.feature/alpha.merge refs/heads/feature/alpha` (the upstream back). Git: `feature/alpha` at `ce5ea6a`, its upstream `origin/feature/alpha` (`%(upstream:short)`), 0 ahead and 0 behind; its reflog one entry `VisualCommit: undo deletion of feature/alpha`; the backup stays. G0; Local branches 4, `alpha` without arrows; T−.<br>**After step 12:** Redo ran: the `update-ref --stdin` with `delete refs/heads/feature/alpha ce5ea6a…`, then `git config --local --remove-section branch.feature/alpha` (configuration after the transaction, UD20); as after step 10, still one backup ref; T+.<br>**After step 13:** the first click selected `ff` and "Fast-forward me"; `git switch --no-guess feature/ff`; status "Checked out feature/ff"; `## feature/ff`; G0 ff✓; T+ `checkout of feature/ff`.<br>**After step 14:** Undo ran: `git switch --no-guess --no-overwrite-ignore main`; `## main...origin/main`; HEAD's reflog's newest entry `checkout: moving from feature/ff to main`; G0; "Fast-forward me" still selected; T−.<br>**After step 15:** Redo ran: `git switch --no-guess --no-overwrite-ignore feature/ff`; G0 ff✓; T+.<br>**After step 16:** at the start G0 −alpha (Local branches 3). The double-click ran `git switch --create feature/alpha --track refs/remotes/origin/feature/alpha`; status "Created feature/alpha tracking origin/feature/alpha and checked it out"; `## feature/alpha...origin/feature/alpha`; keys `branch.feature/alpha.remote` origin and `branch.feature/alpha.merge` refs/heads/feature/alpha; G0 alpha✓; T+ `checkout of origin/feature/alpha`.<br>**After step 17:** Undo ran: `git switch --no-guess --no-overwrite-ignore main`, then the `update-ref --stdin` with `delete refs/heads/feature/alpha ce5ea6a…`, then `git config --local --remove-section branch.feature/alpha`. Git: `## main...origin/main`; no local `feature/alpha` and no key of it; G0 −alpha; Local branches 3; T−.<br>**After step 18:** Redo ran: the `update-ref --stdin` with `create refs/heads/feature/alpha ce5ea6a…` (written before the switch, which needs the branch), then `git switch --no-guess --no-overwrite-ignore feature/alpha`, then the two `config --local --add` keys (UD20); `## feature/alpha...origin/feature/alpha`; G0 alpha✓; T+.<br>**After step 19:** no question; "Add core" is on `main` and the tree is clean, so the checkout is 9.1's `git switch --detach --no-overwrite-ignore 8beca34…` with no `git stash` call; status "HEAD is now detached at 8beca34"; `CurrentBranch` "Detached at 8beca34"; `## HEAD (no branch)`; G0 HEAD at Add core; T+ `checkout of 8beca34`.<br>**After step 20:** Undo ran: `git switch --no-guess --no-overwrite-ignore main`; `## main...origin/main`; HEAD's reflog's newest entry `checkout: moving from 8beca345049bf7e8bab3a7b4f1427eff941b87e3 to main`; G0; T−.<br>**After step 21:** Redo ran: `git switch --detach --no-overwrite-ignore 8beca34…`; detached again; G0 HEAD at Add core; T+.<br>**After step 22:** at the start E0. The question was phase 3's "Leave these commits?" (1 commit). After "Check out": backup `refs/visualcommit/backup/detached-20260102T090001000Z` → `419190f`; `git switch --no-guess main`; status "Checked out main"; E1; `## main...remotes/origin/main`; T+ `checkout of main`.<br>**After step 23:** Undo ran with no question (13.2): `git switch --detach --no-overwrite-ignore 419190f…`; HEAD detached at "Detached work" again, `CurrentBranch` "Detached at 419190f"; HEAD's reflog's newest entry `checkout: moving from main to 419190f…`; E0; the backup stays and no other is made; the linked worktree `edges-wt` still has `side` checked out; T−.<br>**After step 24:** Redo ran, again without the question and without a second backup: `git switch --no-guess --no-overwrite-ignore main`; E1; T+ `checkout of main`.<br>**After step 25:** status "Fast-forwarded loc to main"; `git fetch --no-auto-maintenance --no-write-fetch-head . refs/heads/main:refs/heads/loc` (`   691f8cf..8e16408  main       -> loc`); `loc` at `8e16408`, without arrows; HEAD still detached at `419190f`; E0 loc; T+ `fast-forward of loc`.<br>**After step 26:** Undo ran: `git update-ref -m VisualCommit: undo fast-forward of loc --stdin` with `update refs/heads/loc 691f8cf… 8e16408…`; `loc` at `691f8cf`, ↓1; `loc`'s reflog's newest entry `VisualCommit: undo fast-forward of loc`; E0; T−.<br>**After step 27:** Redo ran (`update refs/heads/loc 8e16408… 691f8cf…`); E0 loc; T+.<br>**After step 28:** at the start G14a. The double-click's first click selected `other` and "Other change"; the dialog: `ErrorTitle` "Checkout blocked", `ErrorSummary` "1 conflict prevents checkout. Commit, stash, or discard your changes first." (C12, design 9.3), `ErrorOutput` phase 3's T2, `ErrorCommand` `git switch --no-guess feature/other`, the buttons "Copy output", "Stash changes and check out" and "Close". After the button: D91's operation (`git stash push --message "VisualCommit: before checking out feature/other"`, `git switch --no-guess feature/other`, `git stash pop --index stash@{0}`); status "Checked out feature/other and re-applied your changes"; `## feature/other`, ` M notes.txt` ("one (local)" … "seven (other)"), `?? newfile.txt`; no stash; G14b; T+ `checkout of feature/other`.<br>**After step 29:** Undo ran (7.3, D91's row): `git stash push` (tracked files only, no message: `WIP on feature/other: 850a7c0 Other change`), `git switch --no-guess --no-overwrite-ignore main`, `git stash pop --index stash@{0}` (found by that stash's id; `Dropped stash@{0} (6cfd0e0…)`). Git: `## main`, ` M notes.txt` ("one (local)" … "seven", byte for byte the start's), `?? newfile.txt` "mine" (untouched throughout); `git stash list` empty; HEAD's reflog's newest entries `checkout: moving from feature/other to main` and, before it, `reset: moving to HEAD` (the stash's); G14a; T−.<br>**After step 30:** Redo ran: `git stash push` (`WIP on main: 106efba Add notes`), `git switch --no-guess --no-overwrite-ignore feature/other`, `git stash pop --index stash@{0}`; as after step 28 (the same `notes.txt` bytes); no stash; G14b; T+. | Not run yet | n/a: each action is phase 3's, its dialogs and graphs pictured in the real window there; each undo and redo is a click on the toolbar's button, which U16 makes in the real window, and a git state both harnesses read alike | `u18a-create-undone`, `u18b-create-redone`, `u18c-create-checkout-undone`, `u18d-create-checkout-redone`, `u18e-rename-undone`, `u18f-rename-redone`, `u18g-delete-undone`, `u18h-delete-redone`, `u18i-checkout-undone`, `u18j-checkout-redone`, `u18k-tracking-undone`, `u18l-tracking-redone`, `u18m-commit-checkout-undone`, `u18n-commit-checkout-redone`, `u18o-detached-undone`, `u18p-detached-redone`, `u18q-ff-undone`, `u18r-ff-redone`, `u18s-stash-checkout-undone`, `u18t-stash-checkout-redone` | Design 7.3 (Checkout; created tracking branch; Create a branch; Rename; Delete; Fast-forward; D91's row), 9.1, 9.3, 13.2; D91, D153, D169, D171. The actions are phase 3's checks 6, 13, 13b, 14, 30 and 32. Decisions UD6, UD7 and UD20. |
| U19 | Undo and redo of history operations, each a Keep move of the branch (and a switch for the "Check out and …" ones): a merge as a fast-forward and with a merge commit, a cherry-pick of two commits, a revert, a rebase, "Check out and rebase", "Check out and merge", an interactive rebase with a drop and a reword, a squash, a pull that rebased; "ops", "rebase", "interactive", "sync", dark, 1100×700 | Part A (`Check_U19_merge_fast_forward`): 1. "Ops" scenario. Right-click the label `feature/ff` (row 2), click "Merge feature/ff into main". Wait until idle. 2. Click Undo. Screenshot. 3. Click Redo. Screenshot.<br>Part B (`Check_U19_merge_commit`): 4. As step 1, clicking "Merge feature/ff into main (create a merge commit)". 5. Click Undo. Screenshot. 6. Click Redo. Screenshot.<br>Part C (`Check_U19_cherry_pick_two`): 7. "Ops". Click row 5 ("Alpha one"), Ctrl+click row 4 ("Alpha two"), right-click row 4, click "Cherry-pick 2 commits onto main". Wait until idle. 8. Click Undo. Screenshot. 9. Click Redo. Screenshot.<br>Part D (`Check_U19_revert`): 10. "Ops". Right-click row 3 ("Main work"), click "Revert this commit". Wait until idle. 11. Click Undo. Screenshot. 12. Click Redo. Screenshot.<br>Part E (`Check_U19_rebase`): 13. "Rebase" scenario. Right-click `main` under Local branches, click "Rebase feature/topic onto main". Wait until idle. 14. Click Undo. Screenshot. 15. Click Redo. Screenshot.<br>Part F (`Check_U19_check_out_and_rebase`): 16. "Rebase". Drag the label `feature/other` (row 0) onto the label `main` (row 1), as phase 4's check A3 (`MoveMouseHeld(…, steps: 5)`); release; click "Check out feature/other and rebase it onto main". Wait until idle. 17. Click Undo. Screenshot. 18. Click Redo. Screenshot.<br>Part G (`Check_U19_check_out_and_merge`): 19. "Rebase". The same drag; click "Check out main and merge feature/other into it". Wait until idle. 20. Click Undo. Screenshot. 21. Click Redo. Screenshot.<br>Part H (`Check_U19_interactive_rebase`): 22. "Interactive" scenario. Right-click the row "Base" (row 6, (468, 285)), click "Interactive rebase of main from here…" (the list opens with Five selected); press D; click the list row Two (x 600, y 259) and press R; in the message dialog select all in `MessageSummary`, type `Two reworded` and click "Save"; click "Start rebase". Wait until idle. 23. Click Undo. Screenshot. 24. Click Redo. Screenshot.<br>Part I (`Check_U19_squash`): 25. "Interactive". Phase 4's check B8: click row 2 ("Three"), Ctrl+click row 1 ("Four"), right-click row 1, click "Squash 2 commits…", then "Squash" in the message dialog (its message as it opens). Wait until idle. 26. Click Undo. Screenshot. 27. Click Redo. Screenshot.<br>Part J (`Check_U19_pull_with_rebase`; "sync" with phase 4's check A4 part A made before the app starts: `PushFromElsewhereAsync(sync, "main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere work")` (12:05), then `CommitFileAsync("src/local.txt", "local\n", "Local work")` on `main` (12:06)): 28. Right-click `main` under Local branches, click "Pull from origin/main (rebase)". Wait until idle. 29. Click Undo. Screenshot. 30. Click Redo. Screenshot. | Each undo below is a Keep move (7.3, D169): the call log has `git update-index -q --refresh`, then `git read-tree -m -u <now> <before>` (exit 0), then `git update-ref -m VisualCommit: undo <text> refs/heads/<b> <before> <now>`; each redo the same back (`read-tree -m -u <before> <now>`, `update-ref -m VisualCommit: redo <text> refs/heads/<b> <now> <before>`). Neither merges, picks or rebases again: the branch goes back to the commits that exist.<br>**After step 1:** status "Fast-forwarded main to feature/ff"; `git merge --no-edit --no-log -m "Merge branch 'feature/ff'" refs/heads/feature/ff` (phase 3's merge, which fast-forwards here); `main` `d93814d`; `## main...origin/main [ahead 1]`; HEAD's reflog's newest entry `merge refs/heads/feature/ff: Fast-forward (no commit created; -m option ignored)`; G1; T+ `merge of feature/ff into main`.<br>**After step 2:** Undo ran (`read-tree -m -u d93814d… 88d6b91…`, `update-ref … refs/heads/main 88d6b91… d93814d…`): `main` `88d6b91`, `## main...origin/main`, no `docs/` folder; G0; T−.<br>**After step 3:** Redo ran: G1, `docs/ff.txt` "ff"; T+.<br>**After step 4:** status "Merged feature/ff into main"; `main` `22a08ae` (parents `88d6b91`, `d93814d`); `## main...origin/main [ahead 2]`; G2; T+ `merge of feature/ff into main`.<br>**After step 5:** Undo ran (`22a08ae…` → `88d6b91…`); G0, clean; `22a08ae` has left the graph; T−.<br>**After step 6:** Redo ran: `main` `22a08ae` again, the same commit; G2; T+.<br>**After step 7:** status "Cherry-picked 2 commits onto main"; `git cherry-pick dff8fbf… ce5ea6a…`; `main` `c0f718d` (Alpha two, parent `d1f9fb5` Alpha one, parent `88d6b91`; author dates 12:03 and 12:02, commit dates 2026-01-02 09:00); `[ahead 2]`; G8; the two selected commits ("Alpha two" and "Alpha one" of `feature/alpha`, rows 6 and 7) still selected; T+ `cherry-pick of 2 commits`.<br>**After step 8:** Undo ran (`c0f718d…` → `88d6b91…`; `src/alpha.txt` removed): G0 with rows 4 and 5 still selected; clean; T−.<br>**After step 9:** Redo ran: G8; T+.<br>**After step 10:** status "Reverted 88d6b91"; `main` `13c773d` (Revert "Main work"); `src/core.txt` "core"; `[ahead 1]`; G9; T+ `revert of 88d6b91`.<br>**After step 11:** Undo ran (`13c773d…` → `88d6b91…`): `src/core.txt` "core", "main work"; G0; T−.<br>**After step 12:** Redo ran: G9; T+.<br>**After step 13:** phase 4's check A2 part A: status "Rebased feature/topic onto main"; `feature/topic` `5f71ab8`; backup `refs/visualcommit/backup/rebase-20260102T090001000Z/feature/topic` → `b0dfa3f`; `## feature/topic...origin/feature/topic [ahead 4, behind 2]`; R1; T+ `rebase of feature/topic onto main`.<br>**After step 14:** Undo ran: the Keep move `5f71ab8…` → `b0dfa3f…` on `refs/heads/feature/topic` (`docs/main.txt` removed, `src/core.txt` back to "core"); `## feature/topic...origin/feature/topic`; HEAD's reflog's newest entry `VisualCommit: undo rebase of feature/topic onto main`; no other branch written (`main`, `feature/other` unchanged); the backup stays; R0; T−.<br>**After step 15:** Redo ran: R1; T+.<br>**After step 16:** phase 4's check A3: status "Rebased feature/other onto main"; HEAD on `feature/other` at `14e607b`; backup `rebase-20260102T090001000Z/feature/other` → `2d4d79a`; R3; T+ `rebase of feature/other onto main`.<br>**After step 17:** Undo ran, in this order (7.3, "Check out and rebase"): the Keep move of `refs/heads/feature/other` `14e607b…` → `2d4d79a…` while HEAD is still on it, then `git switch --no-guess --no-overwrite-ignore feature/topic`. Git: `## feature/topic...origin/feature/topic`; `feature/other` `2d4d79a`; HEAD's reflog's newest entries `checkout: moving from feature/other to feature/topic`, then `VisualCommit: undo rebase of feature/other onto main`; R0; T−.<br>**After step 18:** Redo ran: `git switch --no-guess --no-overwrite-ignore feature/other` first, then the Keep move to `14e607b…`; HEAD's reflog's newest entries `VisualCommit: redo rebase of feature/other onto main`, then `checkout: moving from feature/topic to feature/other`; R3; T+.<br>**After step 19:** the drop menu was phase 4's check A3 step 4's; the operation ran `git switch --no-guess main`, then `git merge --no-edit --no-log -m "Merge branch 'feature/other'" refs/heads/feature/other`; status "Merged feature/other into main"; HEAD on `main` at `5e5d815` (parents `1dd58b8`, `2d4d79a`); `## main...origin/main [ahead 2]`; R4; T+ `merge of feature/other into main`.<br>**After step 20:** Undo ran: the Keep move of `refs/heads/main` `5e5d815…` → `1dd58b8…` (`other/one.txt` removed), then `git switch --no-guess --no-overwrite-ignore feature/topic`; `## feature/topic...origin/feature/topic`; `main` `1dd58b8`; R0; T−.<br>**After step 21:** Redo ran: `git switch --no-guess --no-overwrite-ignore main`, then the Keep move to `5e5d815…`; R4; T+.<br>**After step 22:** the list was phase 4's L0; after D, Five's button read `Drop`; after R the dialog `Reword 938844a` opened; after Save, Two's second line read `New message: Two reworded`. "Start rebase" ran `git rebase -i 4b4622c…` with the app's todo (`pick` One, `label vc-1`, `pick` Two, the guarded amend `exec` with its message file, `pick` Fix one, Three and Four, `drop` Five); status "Rebased main: 5 commits"; `main` `4ad730d`; backup `rebase-20260102T090001000Z/main` → `57f089d`; `## main`; `src/five.txt` gone; the list closed; G-U19; T+ `interactive rebase of main`.<br>**After step 23:** Undo ran (`4ad730d…` → `57f089d…`; `src/five.txt` "five" back); G-I; T−.<br>**After step 24:** Redo ran: G-U19, `src/five.txt` gone; T+.<br>**After step 25:** phase 4's check B8: status "Squashed 2 commits into f34248d"; `main` `eb6f702`; backup `rebase-20260102T090001000Z/main` → `57f089d`; G-B8; T+ `squash of 2 commits`.<br>**After step 26:** Undo ran (`eb6f702…` → `57f089d…`; the two trees are the same, so no file changes); G-I; T−.<br>**After step 27:** Redo ran: G-B8; T+.<br>**After step 28:** at the start phase 4's S1. Phase 4's check A4 part A: the fetch (`   a7f2cfd..b2e9a9e  main       -> origin/main`), backup `rebase-20260102T090001000Z/main` → `f4f8c06`, `git rebase`; status "Pulled and rebased main onto origin/main"; `main` `8a1f07b`; `## main...origin/main [ahead 1]`; S2; T+ `pull into main`.<br>**After step 29:** Undo ran (`8a1f07b…` → `f4f8c06…`; `src/elsewhere.txt` removed, `src/local.txt` kept). `origin/main` stays at `b2e9a9e` (a fetch is not undone, 7.2): `## main...origin/main [ahead 1, behind 1]`; S2u; T−.<br>**After step 30:** Redo ran: S2; T+. | Not run yet | n/a: the actions are phases 3 and 4's, pictured in the real window there; their undo is the toolbar's button, which U16 clicks in the real window, and a Keep move or a switch whose result both harnesses read alike | `u19a-ff-undone`, `u19b-ff-redone`, `u19c-merge-undone`, `u19d-merge-redone`, `u19e-pick-undone`, `u19f-pick-redone`, `u19g-revert-undone`, `u19h-revert-redone`, `u19i-rebase-undone`, `u19j-rebase-redone`, `u19k-checkout-rebase-undone`, `u19l-checkout-rebase-redone`, `u19m-checkout-merge-undone`, `u19n-checkout-merge-redone`, `u19o-interactive-undone`, `u19p-interactive-redone`, `u19q-squash-undone`, `u19r-squash-redone`, `u19s-pull-undone`, `u19t-pull-redone` | Design 7.3 (the history row, "Check out and merge", "Check out and rebase"), 0.4 (refresh before read-tree), 7.2 (fetches are not recorded); D142, D169. The actions are phase 3's checks 8, 11, 12 and phase 4's A2, A3, A4, B7 (the keys D and R) and B8. Decision UD8. |
| U20 | Undo and redo of stash, apply, pop and drop (each stash back as `stash@{0}` with its id and message) and of tags (an annotated tag created, kept in a `tag-undone-` backup and brought back as the same tag object; a tag deleted and brought back); "ops", dark, 1100×700 | Part A (`Check_U20_stash`; before the app starts, outside it, as phase 3's check 15: `src/core.txt` "core", "main work", "more work" and an untracked `todo.txt` "Later"): 1. Click the toolbar's Stash (`StashButton`), type `Save my work` into `MessageBox` ("Include untracked files" ticked, the default), click "Stash". Wait until idle. 2. Click Undo. Screenshot. 3. Click Redo. Screenshot.<br>Part B (`Check_U20_apply`): 4. "Ops" scenario. Right-click Stashes › "On main: Experiment", click "Apply stash". Wait until idle. 5. Click Undo. Screenshot. 6. Click Redo. Screenshot.<br>Part C (`Check_U20_pop`): 7. "Ops". Right-click Stashes › "On main: Experiment", click "Pop stash". Wait until idle. 8. Click Undo. Screenshot. 9. Click Redo. Screenshot.<br>Part D (`Check_U20_drop`): 10. "Ops". Right-click Stashes › "On main: Experiment", click "Drop stash…", then "Drop". Wait until idle. 11. Click Undo. Screenshot. 12. Click Redo. Screenshot.<br>Part E (`Check_U20_create_annotated_tag`): 13. "Ops". Right-click the row "Alpha two" (row 4, (484, 233)), click "Create tag here…", type `v1.2` into `NameBox`, click `MessageBox` and type `#42 hotfix`, click "Create tag". Wait until idle. 14. Click Undo. Screenshot. 15. Click Redo. Screenshot.<br>Part F (`Check_U20_delete_tag`): 16. "Ops". Right-click Tags › `v1.0`, click "Delete v1.0…", then "Delete". Wait until idle. 17. Click Undo. Screenshot. 18. Click Redo. Screenshot. | **After step 1:** at the start G0+w. The app ran `git stash push --include-untracked --message "Save my work"` (without `GIT_LITERAL_PATHSPECS`, D108); status "Stashed the changes"; `git stash list`: `stash@{0}` `b80f1ca…` `On main: Save my work`, `stash@{1}` `0e95aca…` `On main: Experiment`; `git status` clean; G0-S; Stashes 2; T+ `stash "On main: Save my work"`.<br>**After step 2:** Undo ran: `git stash pop --index stash@{0}` (found by `b80f1ca`, `Dropped stash@{0} (b80f1ca…)`). Git: ` M src/core.txt` ("core", "main work", "more work"), `?? todo.txt` ("Later"), nothing staged; `git stash list`: `stash@{0}` `0e95aca…` `On main: Experiment`; G0+w; Stashes 1; T−.<br>**After step 3:** Redo ran: `git stash push --include-untracked --message "Save my work"`; the new stash is `b80f1ca` again (the same trees, parents and D72 date), and the entry keeps it; as after step 1; T+.<br>**After step 4:** status "Applied stash@{0}"; `git stash apply stash@{0}`; ` M README.md` ("# Operations", "", "Experiment."); the stash list unchanged; G0+w; T+ `apply of stash "On main: Experiment"`.<br>**After step 5:** Undo ran (7.3, Apply: `IndexEntries`, then `FilesTo`): `git update-index -z --index-info`, which wrote `README.md`'s entries back: `git ls-files --stage` reads the index as before, `README.md` at `100644 114493d… 0` (nothing was staged), and `README.md` written back to "# Operations" from its recorded blob; clean; the stash list unchanged; G0; no `git stash` call; T−.<br>**After step 6:** Redo ran: the entries and `README.md`'s bytes after the apply written again; ` M README.md`; G0+w; T+.<br>**After step 7:** status "Popped stash@{0}"; a backup `refs/visualcommit/backup/stash-20260102T090001000Z` → `0e95aca` saved before `git stash pop stash@{0}` (D171: a pop saves one, as a drop does); ` M README.md`; `git stash list` empty; G0-s with the working-changes row above it (its ring in lane 1, colour 1, Main work's lane, display row 3, its dashed line down lane 1 to that node); Stashes 0; T+ `pop of stash "On main: Experiment"`.<br>**After step 8:** Undo ran (7.3, Pop): `git stash store -m "On main: Experiment" 0e95aca…` first, then the index entries and `README.md`'s bytes back. `git stash list`: `stash@{0}` `0e95aca…` `On main: Experiment` (12.2: back as `stash@{0}`); its reflog entry `On main: Experiment`; clean; G0; Stashes 1; the backup stays; T−.<br>**After step 9:** Redo ran: the entries and bytes forward, then `git stash drop stash@{0}` (found by `0e95aca`); as after step 7, still one backup ref; T+.<br>**After step 10:** the confirmation was phase 3's ("Drop stash?", `Drop stash@{0} "On main: Experiment"?`); backup `stash-20260102T090001000Z` → `0e95aca`; `git stash drop stash@{0}`; status "Dropped stash@{0}"; no stash; G0-s; Stashes 0; T+ `drop of stash "On main: Experiment"`.<br>**After step 11:** Undo ran: `git stash store -m "On main: Experiment" 0e95aca…`; `git stash list`: `stash@{0}` `0e95aca…` `On main: Experiment`; G0; T−.<br>**After step 12:** Redo ran: `git stash drop stash@{0}`; G0-s; T+.<br>**After step 13:** status "Created tag v1.2"; `git tag --annotate --cleanup=whitespace --file=- v1.2 ce5ea6a…^{commit}`; `refs/tags/v1.2` → the tag object `bbc27cb` (`object ce5ea6a…`, `type commit`, `tag v1.2`, `tagger Test Author <author@example.com> 1767344400 +0000`, message `#42 hotfix`); G0 v1.2; Tags 3; T+ `creation of tag v1.2`.<br>**After step 14:** Undo ran (7.3, Create a tag): first a backup `refs/visualcommit/backup/tag-undone-20260102T090001000Z/v1.2` → `bbc27cb`, then `git update-ref -m VisualCommit: undo creation of tag v1.2 --stdin` with `delete refs/tags/v1.2 bbc27cb…`; `git tag` lists `v1.0`, `v1.1`; G0; Tags 2; T−.<br>**After step 15:** Redo ran (`update-ref … --stdin` with `create refs/tags/v1.2 bbc27cb…`): `v1.2` is the same tag object `bbc27cb` (`git cat-file -t v1.2` prints `tag`); the `tag-undone-` backup stays; G0 v1.2; T+.<br>**After step 16:** the confirmation was phase 3's ("Delete tag?", "Delete the tag v1.0 here? It stays on remotes it was pushed to."); backup `refs/visualcommit/backup/tag-20260102T090001000Z/v1.0` → `c8e9976…`; `git tag --delete v1.0`; status "Deleted tag v1.0"; G0 −v1.0; Tags 1; T+ `deletion of tag v1.0`.<br>**After step 17:** Undo ran (`update-ref … --stdin` with `create refs/tags/v1.0 c8e9976…`): `v1.0` is the annotated tag object again (`tag v1.0`, message "Version 1.0", tagger date 2026-01-01 12:02); G0; Tags 2; T−.<br>**After step 18:** Redo ran (`delete refs/tags/v1.0 c8e9976…`); G0 −v1.0; the backup stays; T+. | Not run yet | n/a: the stash and tag dialogs are phase 3's, pictured in the real window there; the steps' results are stash lists, refs and files that both harnesses read alike | `u20a-stash-undone`, `u20b-stash-redone`, `u20c-apply-undone`, `u20d-apply-redone`, `u20e-pop-undone`, `u20f-pop-redone`, `u20g-drop-undone`, `u20h-drop-redone`, `u20i-tag-undone`, `u20j-tag-redone`, `u20k-tag-delete-undone`, `u20l-tag-delete-redone` | Design 7.3 (Stash, Apply, Pop, Drop, Create a tag, Delete a tag), 7.12 (a stash popped back only onto the commit it was made on: HEAD has not moved here), 0.5, 7.8, 12.2; D108, D171. The actions are phase 3's checks 15 and 16. |
| U21 | Undo and redo of changes to a remote, against the scenarios' local bare `origin`, each with its question: a push that created a branch (its upstream keys too), a force push, a branch deleted on the remote, a tag pushed, a tag deleted on the remote; and the undo of a push whose lease fails because someone pushed since; "sync", "ops", dark, 1100×700 | Part A (`Check_U21_push_new_branch`): 1. "Sync" scenario. Right-click `topic` under Local branches, click "Push topic…", then "Push" in `PushDialog` ("Set as upstream" ticked, phase 3's check 29). Wait until idle. 2. Click Undo. Screenshot. 3. Click "Undo" in the question. Wait until idle. Screenshot. 4. Click Redo. Screenshot. 5. Click "Redo". Wait until idle. Screenshot.<br>Part B (`Check_U21_force_push`; "sync" with phase 3's check 20 rewrite made before the app starts: `CheckoutAsync("published")`, `GitAsync("reset", "--hard", <a7f2cfd's full SHA>)`, `CommitFileAsync("src/published.txt", "rewritten\n", "Rewritten work")` (12:05), `CheckoutAsync("main")`): 6. Right-click `published` under Local branches, click "Force push published…", then "Force push". Wait until idle. 7. Click Undo. Screenshot. 8. Click "Undo". Wait until idle. Screenshot. 9. Click Redo. Screenshot. 10. Click "Redo". Wait until idle. Screenshot.<br>Part C (`Check_U21_delete_remote_branch`): 11. "Sync". Right-click `published` under Remotes › origin, click "Delete published on origin…", then "Delete on origin". Wait until idle. 12. Click Undo. Screenshot. 13. Click "Undo". Wait until idle. Screenshot. 14. Click Redo. Screenshot. 15. Click "Redo". Wait until idle. Screenshot.<br>Part D (`Check_U21_push_tag`): 16. "Ops" scenario. Right-click Tags › `v1.1`, click "Push v1.1 to origin". Wait until idle. 17. Click Undo. Screenshot. 18. Click "Undo". Wait until idle. Screenshot. 19. Click Redo. Screenshot. 20. Click "Redo". Wait until idle. Screenshot.<br>Part E (`Check_U21_delete_remote_tag`; "ops" with `v1.1` pushed before the app starts, outside it: `GitAsync("push", "--quiet", "origin", "refs/tags/v1.1")`): 21. Right-click Tags › `v1.1`, click "Delete v1.1 on origin…", then "Delete on origin". Wait until idle. 22. Click Undo. Screenshot. 23. Click "Undo". Wait until idle. Screenshot. 24. Click Redo. Screenshot. 25. Click "Redo". Wait until idle. Screenshot.<br>Part F (`Check_U21_lease_fails`): 26. "Sync". Right-click `published` under Local branches, click "Push published to origin/published". Wait until idle. 27. Outside the app, `PushFromElsewhereAsync(sync, "published", "src/published.txt", "published\nunpushed\nelsewhere\n", "Published elsewhere")` (12:05); the app does not fetch. 28. Click Undo. Screenshot. 29. Click "Undo". Wait for the dialog. Screenshot. 30. Click "Close". | **After step 1:** phase 3's check 29 step 2: status "Pushed topic to origin/topic, and set it as upstream"; `git push --progress --set-upstream origin refs/heads/topic:refs/heads/topic` (` * [new branch]      topic -> topic`); `origin.git` has `topic` at `77d782e`; `refs/remotes/origin/topic` `77d782e`; keys `branch.topic.remote` origin and `branch.topic.merge` refs/heads/topic; S-topic; T+ `push of topic to origin`.<br>**After step 2:** no operation has started: the question `Delete topic on origin?` / `Undoing the push deletes topic on origin. Anyone who fetched it keeps it.`, "Cancel" with the focus, "Undo" in the primary style, no detail line.<br>**After step 3:** Undo ran as a network operation (7.5): `git push --progress --force-with-lease=refs/heads/topic:77d782e… origin --delete refs/heads/topic` first (TU3), then `git config --local --remove-section branch.topic`. Git: `git ls-remote --heads origin` lists `main` and `published` only; `refs/remotes/origin/topic` gone (git's push removed it); no `branch.topic.*` key; `topic` still `77d782e`; the sync graph; Remotes 2; `topic` without arrows; T− `push of topic to origin`.<br>**After step 4:** the question `Push topic to origin again?` / `origin gets topic back at 77d782e.`.<br>**After step 5:** Redo ran: `git push --progress --force-with-lease=refs/heads/topic: origin 77d782e…:refs/heads/topic` (an empty lease: the branch must not exist; TU3), then `git config --local --add branch.topic.remote origin` and `git config --local --add branch.topic.merge refs/heads/topic`; as after step 1; T+.<br>**After step 6:** at the start S-F0. The confirmation was phase 3's ("Force push published?"); backup `refs/visualcommit/backup/force-push-20260102T090001000Z/origin/published` → `8c6f545`; `git push --progress --force-with-lease=refs/heads/published --force-if-includes origin refs/heads/published:refs/heads/published` (` + 8c6f545...4f6a372 published -> published (forced update)`); status "Force-pushed published to origin/published"; origin's `published` `4f6a372`; S-F1; T+ `force push of published to origin`.<br>**After step 7:** the question `Undo the push to origin?` / `published on origin goes back to 8c6f545, which it was before the push. Anyone who fetched 4f6a372 keeps it.`.<br>**After step 8:** Undo ran: `git push --progress --force-with-lease=refs/heads/published:4f6a372… origin 8c6f545…:refs/heads/published` (TU3). Git: origin's `published` and `refs/remotes/origin/published` `8c6f545` (git's push moved the remote-tracking ref); the local `published` still `4f6a372`, ↑1 ↓1; S-F0; the backup stays; T−.<br>**After step 9:** the question `Redo the push to origin?` / `published on origin moves to 4f6a372 again.`.<br>**After step 10:** Redo ran: `git push --progress --force-with-lease=refs/heads/published:8c6f545… origin 4f6a372…:refs/heads/published`; S-F1; no new backup; T+.<br>**After step 11:** phase 3's check 30 step 4 (the confirmation "Delete published on origin?"); backup `remote-branch-20260102T090001000Z/origin/published` → `8c6f545`; `git push --progress --force-with-lease=refs/heads/published:8c6f545… origin --delete refs/heads/published`; status "Deleted published on origin"; `origin.git` has `main` only; S-del; T+ `deletion of published on origin`.<br>**After step 12:** the question `Undo the deletion of published on origin?` / `origin gets published back at 8c6f545.`.<br>**After step 13:** Undo ran: `git push --progress --force-with-lease=refs/heads/published: origin 8c6f545…:refs/heads/published` (TU3). Git: origin's `published` `8c6f545`; `refs/remotes/origin/published` back at `8c6f545`; `published` tracks it again, ↑1; the sync graph; Remotes 2; T−.<br>**After step 14:** the question `Delete published on origin again?` / `published is deleted on origin again.`.<br>**After step 15:** Redo ran: `git push --progress --force-with-lease=refs/heads/published:8c6f545… origin --delete refs/heads/published`; S-del; still one backup ref; T+.<br>**After step 16:** status "Pushed tag v1.1 to origin"; `git push --progress origin refs/tags/v1.1:refs/tags/v1.1` (` * [new tag]         v1.1 -> v1.1`); `git ls-remote --tags origin` prints `88d6b91… refs/tags/v1.1`; G0 (a remote's tags are not drawn); T+ `push of tag v1.1 to origin`.<br>**After step 17:** the question `Delete tag v1.1 on origin?` / `Undoing the push deletes tag v1.1 on origin. Anyone who fetched it keeps it.`.<br>**After step 18:** Undo ran: `git push --progress --force-with-lease=refs/tags/v1.1:88d6b91… origin --delete refs/tags/v1.1` (TU3); `git ls-remote --tags origin` prints nothing; the local `v1.1` stays; G0; T−.<br>**After step 19:** the question `Push tag v1.1 to origin again?` / `origin gets tag v1.1 back at 88d6b91.`.<br>**After step 20:** Redo ran: `git push --progress --force-with-lease=refs/tags/v1.1: origin 88d6b91…:refs/tags/v1.1`; origin has `v1.1` again; T+.<br>**After step 21:** at the start `origin.git` has `refs/tags/v1.1` (`88d6b91`). The confirmation was phase 3's ("Delete v1.1 on origin?", "Delete the tag v1.1 on origin? Everyone who uses origin loses it."); backup `refs/visualcommit/backup/remote-tag-20260102T090001000Z/origin/v1.1` → `88d6b91`; `git push --progress --force-with-lease=refs/tags/v1.1:88d6b91… origin --delete refs/tags/v1.1`; status "Deleted tag v1.1 on origin"; T+ `deletion of tag v1.1 on origin`.<br>**After step 22:** the question `Undo the deletion of tag v1.1 on origin?` / `origin gets tag v1.1 back at 88d6b91.`.<br>**After step 23:** Undo ran: `git push --progress --force-with-lease=refs/tags/v1.1: origin 88d6b91…:refs/tags/v1.1`; origin has `v1.1` (`88d6b91`); T−.<br>**After step 24:** the question `Delete tag v1.1 on origin again?` / `tag v1.1 is deleted on origin again.`.<br>**After step 25:** Redo ran: `git push --progress --force-with-lease=refs/tags/v1.1:88d6b91… origin --delete refs/tags/v1.1`; origin has no tag; the backup stays; T+.<br>**After step 26:** status "Pushed published to origin/published"; `git push --progress origin refs/heads/published:refs/heads/published` (`   8c6f545..8e65315  published -> published`, the line the entry takes the remote's old value from, 7.7); S-pushed; T+ `push of published to origin`.<br>**After step 27:** `origin.git`'s `published` is `c16ddfa`; the app's `refs/remotes/origin/published` is still `8e65315`; the graph unchanged; Undo still enabled with the same tooltip (the lease is checked only inside the step, 7.4).<br>**After step 28:** the question `Undo the push to origin?` / `published on origin goes back to 8c6f545, which it was before the push. Anyone who fetched 8e65315 keeps it.`.<br>**After step 29:** the step ran `git push --progress --force-with-lease=refs/heads/published:8e65315… origin 8c6f545…:refs/heads/published`, which git refused (exit 1, TU2). The error dialog: `ErrorTitle` `Undo failed`; `ErrorSummary` `published on origin has changed since: nothing was changed.`; `ErrorRepository` "In sync"; `ErrorOutput` TU2; `ErrorCommand` that push; "Copy output" and "Close". Status `Undo push of published to origin failed` in `VcDangerBrush`; the log line `sync: Undo push of published to origin: Failed after <N> ms: ` followed by git's first line of output (`To <origin>`). Nothing changed: origin's `published` `c16ddfa`; the local refs and keys as after step 26.<br>**After step 30:** no dialog; the entry stays: T+ `push of published to origin` (outside changes never remove an entry, 7.4). | Not run yet | n/a: the questions are phase 3's confirmation card with other texts, read by automation id in both harnesses; what the steps change is the remote, which both read with git; the error dialog's output names the copy's folder, which differs in every run | `u21a-push-undo-question`, `u21b-push-undone`, `u21c-push-redo-question`, `u21d-push-redone`, `u21e-force-undo-question`, `u21f-force-undone`, `u21g-force-redo-question`, `u21h-force-redone`, `u21i-delete-undo-question`, `u21j-delete-undone`, `u21k-delete-redo-question`, `u21l-delete-redone`, `u21m-tag-undo-question`, `u21n-tag-undone`, `u21o-tag-redo-question`, `u21p-tag-redone`, `u21q-tag-delete-undo-question`, `u21r-tag-delete-undone`, `u21s-tag-delete-redo-question`, `u21t-tag-delete-redone`, `u21u-lease-question`, `u21v-lease-failed` (varies by nature: the folder in `ErrorOutput`) | Design 7.3 (`RemoteRef` first, then `BranchConfig`), 7.5 (the questions, the lease failure), 7.7 (`PushOutcome`: the old value from git's summary line), 0.5; D155, D170. The actions are phase 3's checks 16, 20, 29 and 30 against the folder remote. Decisions UD9 to UD11. |
| U22 | Undo and redo of local configuration: an upstream set, a remote added, renamed, given another URL and removed (its keys, its branches' upstreams and its remote branches back), a file ignored; "sync", "ops", dark, 1100×700 | Part A (`Check_U22_set_upstream`): 1. "Sync" scenario. Right-click `topic` under Local branches, click "Set upstream…", type `pub` into `PickFilter`, click `origin/published`, click "Set upstream". Wait until idle. 2. Click Undo. Screenshot. 3. Click Redo. Screenshot.<br>Part B (`Check_U22_add_remote`): 4. "Sync". Right-click the Remotes section header, click "Manage remotes…", click "Add remote" (`AddRemoteButton`), type `mirror` into `RemoteNameBox` and, into `RemoteUrlBox`, the URL `git remote get-url origin` prints for the copy (its `origin.git` folder); leave "Fetch now" (`RemoteFetchOption`) ticked; click "Save". Wait until idle (the fetch runs as an operation of its own). Click "Close". 5. Click Undo. Screenshot. 6. Click Redo. Screenshot.<br>Part C (`Check_U22_rename_remote`): 7. "Sync". Open "Manage remotes…" as in step 4, click `origin`'s "Edit" (`RemoteEditButton`), select all in `RemoteNameBox` and type `upstream`, click "Save". Wait until idle. Click "Close". 8. Click Undo. Screenshot. 9. Click Redo. Screenshot.<br>Part D (`Check_U22_change_url`): 10. "Sync". Open "Manage remotes…", click `origin`'s "Edit", select all in `RemoteUrlBox` and type `https://example.invalid/sync.git`, click "Save". Wait until idle. Click "Close". 11. Click Undo. Screenshot. 12. Click Redo. Screenshot.<br>Part E (`Check_U22_remove_remote`): 13. "Sync". Open "Manage remotes…", click `origin`'s "Remove" (`RemoteRemoveButton`), then "Remove" in the confirmation stacked over the dialog. Wait until idle. Click "Close". 14. Click Undo. Screenshot. 15. Click Redo. Screenshot.<br>Part F (`Check_U22_ignore`; "ops" with `logs/run.log` "run\n" written before the app starts, outside it): 16. Click the working-changes row; right-click the unstaged `logs/run.log`, click "Ignore run.log". Wait until idle. 17. Click Undo. Screenshot. 18. Click Redo. Screenshot. | **After step 1:** phase 3's check 28 step 3: status "topic now tracks origin/published"; `git branch --set-upstream-to=refs/remotes/origin/published topic`; keys `branch.topic.remote` origin and `branch.topic.merge` refs/heads/published; `topic` ↑1 ↓1; the graph unchanged; T+ `upstream change of topic`.<br>**After step 2:** Undo ran: `git config --local --remove-section branch.topic`; no `branch.topic.*` key; `topic` without arrows; T−.<br>**After step 3:** Redo ran: `git config --local --add branch.topic.remote origin`, then `… --add branch.topic.merge refs/heads/published`; ↑1 ↓1; T+.<br>**After step 4:** `git remote add -- mirror <the folder>` ran ("Added remote mirror"), then the fetch of phase 3's check 22 as an operation of its own (`git fetch --progress mirror`: ` * [new branch]      main       -> mirror/main` and ` * [new branch]      published  -> mirror/published`), whose status "Fetched from mirror" is the one shown. Keys `remote.mirror.url` the folder and `remote.mirror.fetch` `+refs/heads/*:refs/remotes/mirror/*`; refs `refs/remotes/mirror/main` `a7f2cfd` and `refs/remotes/mirror/published` `8c6f545`. The left panel's Remotes 4: `mirror` (open) with `main` and `published`, then `origin` (open) with `main` and `published`; in the graph "Published work" carries `mirror/published`, `origin/published` and "Shared work" `main` ✓, `mirror/main`, `origin/main`. T+ `addition of remote mirror` (the fetch records nothing, 7.2).<br>**After step 5:** Undo ran (7.12, "Add a remote" after "Fetch now"): `git config --local --remove-section remote.mirror`, and in the step's `git update-ref -m VisualCommit: undo addition of remote mirror --stdin` transaction `delete refs/remotes/mirror/main a7f2cfd…` and `delete refs/remotes/mirror/published 8c6f545…` (the fetched refs, their reflogs with them). `git remote` prints `origin`; no `refs/remotes/mirror/*`; `.git/config` byte for byte as at the start; the sync graph; Remotes 2; T−.<br>**After step 6:** Redo ran: `git config --local --add remote.mirror.url <the folder>` and `… --add remote.mirror.fetch +refs/heads/*:refs/remotes/mirror/*`, and no fetch: the state right after the add, without the fetched refs (7.12). Remotes 2: `mirror` (open, nothing under it), then `origin` (open) with `main` and `published`; the sync graph; T+.<br>**After step 7:** status "Renamed remote origin to upstream"; `git remote rename origin upstream`: the refs `refs/remotes/upstream/main` and `…/published`, `branch.main.remote` and `branch.published.remote` `upstream`; S-up; T+ `rename of remote origin to upstream`.<br>**After step 8:** Undo ran: `git remote rename upstream origin`; `.git/config` byte for byte as at the start (0.5); the sync graph; T−.<br>**After step 9:** Redo ran: `git remote rename origin upstream`; S-up; T+.<br>**After step 10:** status "Changed the URL of origin"; `git remote get-url origin` prints `https://example.invalid/sync.git`; the graph unchanged; T+ `URL change of origin`.<br>**After step 11:** Undo ran: `git remote set-url -- origin <the URL read at the start: the copy's origin.git folder>`; `git remote get-url origin` equals that value again; T−.<br>**After step 12:** Redo ran: the URL `https://example.invalid/sync.git` again; T+.<br>**After step 13:** the confirmation was phase 3's ("Remove remote origin?", "Remove the remote origin and its 2 remote branches? …"); backups `refs/visualcommit/backup/remote-20260102T090001000Z/origin/main` → `a7f2cfd` and `…/origin/published` → `8c6f545`; `git remote remove origin`; status "Removed remote origin"; no `remote.origin.*`, `branch.main.remote`, `branch.main.merge`, `branch.published.remote` or `branch.published.merge` key and no `refs/remotes/origin/*`; S-none; T+ `removal of remote origin`.<br>**After step 14:** Undo ran (7.3, the remote's keys and branches from the entry): `git config --local --add remote.origin.url <the folder>`, `… --add remote.origin.fetch +refs/heads/*:refs/remotes/origin/*`, `… --add branch.main.remote origin`, `… --add branch.main.merge refs/heads/main`, `… --add branch.published.remote origin`, `… --add branch.published.merge refs/heads/published`, and `git update-ref -m VisualCommit: undo removal of remote origin --stdin` with `create refs/remotes/origin/main a7f2cfd…` and `create refs/remotes/origin/published 8c6f545…`. Git: `git remote` prints `origin`; `git config --local --list` holds the same keys and values as at the start (in this scenario `.git/config` is even byte for byte the same); `## main...origin/main`; `published` ↑1; the sync graph; Remotes 2; the backups stay; T−.<br>**After step 15:** Redo ran: the two refs deleted (`update-ref --stdin` with `delete` lines), then `git config --local --remove-section` of `branch.main`, `branch.published` and `remote.origin`; S-none; T+.<br>**After step 16:** before the click Unstaged files (1): A `run.log` `logs`. Status "Added /logs/run.log to .gitignore"; `.gitignore` created, exactly `/logs/run.log` and a line feed (14 bytes); `?? .gitignore` only; Unstaged files (1): A `.gitignore`; T+ `ignore of /logs/run.log`.<br>**After step 17:** Undo ran: `.gitignore` deleted (the app had created it, 7.3); `?? logs/run.log`; Unstaged files (1): A `run.log` `logs`; T−.<br>**After step 18:** Redo ran: `.gitignore` "/logs/run.log\n" again; as after step 16; T+. | Not run yet | n/a: the pick dialog and the Remotes dialog are phase 3's, pictured in the real window there; the steps change configuration and refs that both harnesses read with git | `u22a-upstream-undone`, `u22b-upstream-redone`, `u22c-add-undone`, `u22d-add-redone`, `u22e-rename-undone`, `u22f-rename-redone`, `u22g-url-undone`, `u22h-url-redone`, `u22i-remove-undone`, `u22j-remove-redone`, `u22k-ignore-undone`, `u22l-ignore-redone` | Design 7.3 (Ignore; Set or stop tracking; Add, rename, remove a remote; change its URL), 7.12 (the fetched refs of an added remote; "sync" has no `refs/remotes/origin/HEAD`, so no symbolic ref comes into play), 0.5 (remote removal and replay, `.git/config`); D83, D168. The actions are phase 3's checks 22, 26 and 28. Decision UD12. |
| U23 | Refusals: Undo after a terminal commit on the same branch (the tooltip, the button disabled, the key doing nothing; enabled again once the branch is back), during a merge in progress (and after its Abort), a Keep move over two changed files in the way (the app's count), an ignored file in the way of a redo; a Keep undo with stale stat data succeeds; "ops", "rebase", dark, 1100×700 | Part A (`Check_U23_terminal_commit`): 1. "Ops" scenario. Right-click the label `feature/ff` (row 2), click "Merge feature/ff into main". Wait until idle. 2. Outside the app, `CommitFileAsync("src/terminal.txt", "terminal\n", "Terminal work")` on `main` (12:08). Wait until the graph shows "Terminal work". 3. Rest the pointer on `UndoButton`. Screenshot. 4. Click row 3 ("Fast-forward me") and press the platform's undo key (`PressGitUndo()`: Ctrl+Z; Cmd+Z on macOS). 5. Outside the app, `GitAsync("reset", "--quiet", "--hard", <d93814d's full SHA>)`. Wait until the graph no longer shows "Terminal work". 6. Click Undo. Screenshot.<br>Part B (`Check_U23_merge_in_progress`): 7. "Ops". Right-click the row "Main work" (row 3), click "Create branch here…", type `hotfix`, untick `NameDialogOption`, click "Create branch". Wait until idle. 8. Right-click the label `feature/conflict` (row 1), click "Merge feature/conflict into main". Wait until idle. 9. Rest the pointer on `UndoButton`. Screenshot. Then click the row "Conflicting change" (from `GraphRowPoint`: the banner shows) and press the platform's undo key. 10. Click the banner's "Abort merge", then "Abort merge" in the dialog. Wait until idle. Screenshot. 11. Click Undo.<br>Part C (`Check_U23_changes_in_the_way`): 12. "Rebase" scenario. Right-click `main` under Local branches, click "Rebase feature/topic onto main". Wait until idle. 13. Outside the app, write `src/core.txt` "core\nmain work\nmine\n" and `docs/main.txt` "main more\nmine\n". Wait until the working-changes row shows. 14. Click Undo. Wait for the dialog. Screenshot. 15. Click "Close". 16. Outside the app, `GitAsync("restore", "--worktree", "--", "docs/main.txt", "src/core.txt")`; wait until the working-changes row is gone. Click Undo.<br>Part D (`Check_U23_ignored_file_in_the_way`): 17. "Ops". Merge `feature/ff` as in step 1. Click Undo. 18. Outside the app, append the line `docs/ff.txt` to `.git/info/exclude` and write `docs/ff.txt` "my ff notes\n". 19. Click Redo. Wait for the dialog. Screenshot. 20. Click "Close". 21. Outside the app, delete `docs/ff.txt`. Click Redo.<br>Part E (`Check_U23_stale_stat_data`): 22. "Ops". Merge `feature/ff` as in step 1. 23. Outside the app, write `docs/ff.txt` with its own bytes ("ff\n") and set its last-write time one hour later (`File.SetLastWriteTimeUtc`), so that the index's stat data no longer matches it. 24. Click Undo. Screenshot. | **After step 1:** as U19's step 1: G1; T+ `merge of feature/ff into main`.<br>**After step 2:** G0-T; `main` `c7d8c71`. The in-memory check fails (7.4 reason 5): Undo disabled with the tooltip `main has moved since (it is at c7d8c71)`; Redo disabled, `Nothing to redo`.<br>**After step 3:** that tooltip shows under the disabled button, inside the window (`ToolTip.ShowOnDisabled`). <br>**After step 4:** the key was handled and did nothing (1.3): no operation started (no `Undo …` log line), no dialog; `main` `c7d8c71`; "Fast-forward me" selected.<br>**After step 5:** G1 again ("Terminal work" left the graph); the state the entry left is back, so Undo is enabled again: T+ `merge of feature/ff into main` (the entry was never removed, 7.4).<br>**After step 6:** Undo ran as U19's step 2: G0; T−.<br>**After step 7:** status "Created hotfix"; G0 hotfix; T+ `creation of hotfix`.<br>**After step 8:** phase 3's check 9 step 2: the banner `Merging: "Merge branch 'feature/conflict'". 1 file has conflicts.` with "Abort merge"; `UU src/core.txt`; status "Merge stopped: 1 file has conflicts" (warning colour); the merge's entry is pending (7.2). Undo disabled with `Finish or abort the merge first`; Redo disabled with the same reason (7.4: reason 2 comes before "Nothing to redo").<br>**After step 9:** the tooltip `Finish or abort the merge first` under the disabled Undo. The key did nothing: no operation; `MERGE_HEAD` present; `hotfix` still there.<br>**After step 10:** phase 3's abort: status "Aborted the merge"; no banner, no working-changes row; G0 hotfix; `## main...origin/main`; backups `refs/visualcommit/backup/abort-20260102T090001000Z` → `88d6b91` and `abort-20260102T090001000Z-changes` (one parent `88d6b91`, its `src/core.txt` the conflict-marked file). The abort removed the pending entry and recorded none (D165): T+ `creation of hotfix`.<br>**After step 11:** Undo ran: `hotfix` deleted (as U18's step 2); G0; T−.<br>**After step 12:** R1; T+ `rebase of feature/topic onto main`.<br>**After step 13:** ` M docs/main.txt`, ` M src/core.txt`; the working-changes row above R1's rows; Undo still enabled (local changes in a move's way are checked only inside the step, 7.4).<br>**After step 14:** the step ran `git update-index -q --refresh`, then `git read-tree -m -u 5f71ab8… b0dfa3f…`, which git refused (exit 128, TU1: it names `docs/main.txt` only), and counted the paths in the move's way itself (7.6: `git diff-tree -r -z --no-renames --name-only 5f71ab8… b0dfa3f…` lists `docs/main.txt` and `src/core.txt`, both changed). The error dialog: `ErrorTitle` `Undo failed`; `ErrorSummary` `Can't undo rebase of feature/topic onto main: 2 conflicts prevent it. Commit, stash, or discard your changes first.`; `ErrorRepository` "In rebase"; `ErrorOutput` TU1, below the summary; `ErrorCommand` `git read-tree -m -u 5f71ab8… b0dfa3f…`; "Copy output" and "Close". Status `Undo rebase of feature/topic onto main failed` in `VcDangerBrush`. Nothing changed but the index's stat data: `feature/topic` `5f71ab8`; both files hold "mine"; HEAD's reflog's newest entry still `rebase (finish): returning to refs/heads/feature/topic`; no `update-ref` in the step's calls.<br>**After step 15:** no dialog; Undo still enabled, `Undo rebase of feature/topic onto main` (the entry stays).<br>**After step 16:** the files are R1's again (clean); Undo ran as U19's step 14: R0; T−.<br>**After step 17:** G0 after the Undo (`docs/` gone); T− `merge of feature/ff into main`.<br>**After step 18:** nothing the app shows changed (`git status --porcelain --ignored` prints `!! docs/`); Redo still enabled, `Redo merge of feature/ff into main` (the ignored-files guard runs only inside the step).<br>**After step 19:** the step ran `git diff-tree -r -z --no-renames --name-only --diff-filter=A 88d6b91… d93814d…` (`docs/ff.txt`) and `git check-ignore --stdin -z` (without `GIT_LITERAL_PATHSPECS`), which named `docs/ff.txt`; then no `read-tree` and no `update-ref`. The error dialog: `ErrorTitle` `Redo failed`; `ErrorSummary` `Can't redo merge of feature/ff into main: 1 ignored file is in the way: docs/ff.txt. Move it away first.`; "In ops"; no `ErrorOutput`, no `ErrorCommand` and no "Copy output" (the app's own check, D95): "Close" only. Status `Redo merge of feature/ff into main failed`. `docs/ff.txt` still "my ff notes", `main` `88d6b91`. (`git read-tree -m -u 88d6b91… d93814d…` alone overwrites it with "ff" and exits 0, on 2.36 and on 2.55: what the guard prevents, D169.)<br>**After step 21:** Redo ran: G1, `docs/ff.txt` "ff"; T+.<br>**After step 23:** every git read the check makes from here to step 24 runs with `GIT_OPTIONAL_LOCKS=0` (`TempRepo.GitWithEnvironmentAsync`, `…AllowingFailureAsync` for the dry run), the conventions' reads after the step included, as the app's own reads do: a plain `git status` would refresh the stat data and write the index, and the Undo would find nothing stale (2.36 and 2.55, UD15). Git's view of the file is stale: `git diff-files --name-only` lists `docs/ff.txt`; `git status --porcelain=v1 -b --untracked-files=all` prints `## main...origin/main [ahead 1]` only (the bytes are the committed ones); `git read-tree -n -m -u d93814d… 88d6b91…` (a dry run) exits 128 with `error: Entry 'docs/ff.txt' not uptodate. Cannot merge.`; `git diff-files --name-only` run again still lists `docs/ff.txt` (none of these reads wrote the index).<br>**After step 24:** Undo ran: the call log has `git update-index -q --refresh` before `git read-tree -m -u d93814d… 88d6b91…` (exit 0); G0, clean, `docs/` gone; T−. | Not run yet | n/a: the refusals are the toolbar's tooltip and the error dialog, read by automation id in both harnesses; the keys are A4's in the real window | `u23a-moved-since`, `u23b-undone-after-reset`, `u23c-merging-tooltip`, `u23d-aborted`, `u23e-changes-in-the-way`, `u23f-ignored-in-the-way`, `u23g-stale-undone` | Design 1.3 (a disabled Undo's key), 7.2 (pending entries, Abort), 7.4, 7.5, 7.6, 0.4 (stale stat data, read-tree and ignored files); D165, D169. Decisions UD13 to UD15. |
| P24 | Refused checkouts (C12, design 9.3): one tracked change by a double-click on a branch (`1 conflict prevents checkout…` with "Stash changes and check out"), "Create branch" with its box ticked (the same sentence, no button), three tracked changes by the branch menu's "Check out other" (`3 conflicts…`), an untracked file by a double-click on the graph's label (no button), a folder in the way (no button); nothing changes, no stash is made and nothing is recorded for Undo; "protect", dark, 1100×700 | **Part A** (`Check_P24_tracked_changes_block_a_checkout`): 1. "Protect" scenario; before the app starts write `src/app.txt` "app\nlocal\n" (not staged). Start. Screenshot. 2. Double-click `other` under Local branches. Wait for the operation "Check out other" (Failed) and its dialog. Screenshot. Click "Close". 3. Right-click the row "Other files" (commit row 1, (468, 181)) in its Message column and click "Create branch here…". Type `topic` into the name box (`NameBox`), leave "Check out the new branch" ticked and click "Create branch" (`ConfirmButton`). Wait for "Create branch topic" (Failed) and its dialog. Screenshot. Click "Close". 4. Outside the app write `src/lib.txt` "lib\nlocal\n" and `docs/guide.txt` "guide\nlocal\n"; wait for the status read `0 staged, 3 unstaged`. 5. Right-click `other` under Local branches and click "Check out other". Wait for "Check out other" (Failed) and its dialog. Screenshot. Click "Close".<br>**Part B** (`Check_P24_untracked_files_block_a_checkout`, a new copy, a new start): 6. "Protect" scenario; before the app starts write the untracked file `notes` "my own notes\n". Start. Double-click the label `other` of the row "Other files" (commit row 1; the label at x 266 to 306.6, y 172 to 190: `GraphLabelBounds(1, "other")`). Wait for "Check out other" (Failed) and its dialog. Screenshot. Click "Close". 7. Outside the app delete `notes` and write `notes/today.txt` "today\n"; wait for the next status read after the write (`0 staged, 1 unstaged`, as before it) and until the repository's status lists `notes/today.txt` and not `notes`. 8. Double-click `other` under Local branches. Wait for "Check out other" (Failed) and its dialog. Screenshot. Click "Close". | **After step 1:** P0w; nothing selected; the status bar "Ready", `CurrentBranch` `main`; P0's left panel; `git status --porcelain` prints ` M src/app.txt`. Undo disabled with `Nothing to undo`, Redo disabled with `Nothing to redo`.<br>**After step 2:** the double-click's first click selected `other` in the left panel and "Other files" (commit row 1) in the graph, whose details show behind the dialog. The call log has `git switch --no-guess other`, exit 1 (TP1), and no `git stash` call. The error dialog: `ErrorTitle` "Checkout blocked"; `ErrorSummary` "1 conflict prevents checkout. Commit, stash, or discard your changes first."; `ErrorRepository` "In protect"; `ErrorOutput` TP1; `ErrorCommand` `git switch --no-guess other`; "Copy output", "Stash changes and check out" (`ErrorExtraButton`, enabled) and "Close". The status bar "Check out other failed" in the danger colour. Nothing changed: HEAD on `main` at 41f8398; `src/app.txt` "app\nlocal\n" (` M src/app.txt`); `git stash list` prints nothing; HEAD's reflog's newest entry is still `commit: Main work`; the refs are P0's. Undo still disabled with `Nothing to undo` (a refused action records nothing, 7.2).<br>**After step 3:** the right-click kept "Other files" selected; the name dialog had `NameDialogTitle` "Create branch", `NameDialogContext` `From 19a9044 "Other files"`, the focus in `NameBox`, "Check out the new branch" (`NameDialogOption`) ticked, "Cancel" and "Create branch". The call log has `git switch --create topic 19a90447e124bf3a74124a5673df655601c53d5b`, exit 1 (TP2). The error dialog: "Checkout blocked"; "1 conflict prevents checkout. Commit, stash, or discard your changes first."; "In protect"; `ErrorOutput` TP2; `ErrorCommand` `git switch --create topic 19a90447e124bf3a74124a5673df655601c53d5b`; "Copy output" and "Close", no `ErrorExtraButton`. The status bar "Create branch topic failed" in the danger colour. No branch `topic` (`git show-ref --verify refs/heads/topic` fails; Local branches 2); HEAD on `main`; ` M src/app.txt`; HEAD's reflog unchanged; Undo `Nothing to undo`.<br>**After step 4:** the working-changes row stays; `git status --porcelain` prints ` M docs/guide.txt`, ` M src/app.txt`, ` M src/lib.txt`.<br>**After step 5:** the right-click opened `other`'s menu without selecting anything (D114), "Check out other" first and enabled. The call log has `git switch --no-guess other`, exit 1 (TP3). The error dialog: "Checkout blocked"; "3 conflicts prevent checkout. Commit, stash, or discard your changes first."; "In protect"; `ErrorOutput` TP3; `ErrorCommand` `git switch --no-guess other`; "Copy output", "Stash changes and check out" and "Close". The status bar "Check out other failed" in the danger colour. Nothing changed: HEAD on `main`, the three files as written, `git stash list` empty, HEAD's reflog unchanged, `git for-each-ref refs/visualcommit/backup` prints nothing. Undo `Nothing to undo`.<br>**At the start of part B:** P0w (the working-changes row for `?? notes`); Undo `Nothing to undo`.<br>**After step 6:** the double-click's first click selected "Other files". The call log has `git switch --no-guess other`, exit 1 (TP4), and no `git stash` call. The error dialog: "Checkout blocked"; "1 conflict prevents checkout. Commit, stash, or discard your changes first." (one path under git's untracked header; `notes` is not ignored: `git check-ignore` exits 1 for it); "In protect"; `ErrorOutput` TP4; `ErrorCommand` `git switch --no-guess other`; "Copy output" and "Close", no extra button. The status bar "Check out other failed" in the danger colour. Nothing changed: HEAD on `main`, `notes` "my own notes\n" (`?? notes`).<br>**After step 8:** the call log has `git switch --no-guess other`, exit 1 (TP5). The error dialog: "Checkout blocked"; "1 conflict prevents checkout. Commit, stash, or discard your changes first." (the folder `notes`, one path); "In protect"; `ErrorOutput` TP5 (three lines); `ErrorCommand` `git switch --no-guess other`; "Copy output" and "Close", no extra button. The status bar "Check out other failed". Nothing changed: HEAD on `main`, `notes/today.txt` "today\n" (`?? notes/today.txt`), HEAD's reflog unchanged, no stash and no backup ref. Undo `Nothing to undo`. | Not run yet | RW: part A, steps 1 and 2: a real double-click on Local branches › other; the dialog's `ErrorTitle`, `ErrorSummary`, `ErrorExtraButton` and `ErrorOutput` read through UI Automation; `p24a-changed-file` and `p24b-one-conflict` compared with their scripted twins (the real window shows the dialog's card and the button at the real scaling); then a real click on "Close" | `p24a-changed-file`, `p24b-one-conflict`, `p24c-create-branch-blocked`, `p24d-three-conflicts`, `p24e-untracked-file`, `p24f-folder-in-the-way`; real window: `p24a-changed-file`, `p24b-one-conflict` | D153 (1), D179; design 9.3 (the count is the distinct tab lines under git's headers; the button only where phase 3 offers it, `offerStash`), 0.7 (a refusal changes nothing; `--create` makes no branch), 7.2. Four checkout paths of 9.1's second row: a left-panel double-click, a label double-click, the branch menu, "Create branch". The drops' checkouts are left to App tests: "Check out other and merge main into it" is refused at its checkout with the same sentence and no button (`RepositoryOperationsMergeTests` 199, 9.4); "Check out other and rebase it onto main" is refused by phase 4's own check before its checkout (phase 4's A5 step 3). Phase 3's check 14 step 5 clicks "Stash changes and check out" from this dialog; its flow (D91) is unchanged. Decisions PD1 to PD6. |
| P25 | The automatic stash (design 9.2, D153 (2) and (3), D166): "Check out this commit" on an older commit of `main` with staged, unstaged and untracked changes: no question; the stash `Auto stash before checking out f39ea2d` drawn as a ring on main's tip with a dashed line; the status text; the detached HEAD; the ignored file untouched; `main` checked out again (the stash kept); the toolbar's Pop; and, on a fresh copy, Undo of that checkout (switched back, every change back, the index too), Redo and Undo again; "protect", dark, 1100×700 | **Part A** (`Check_P25_automatic_stash_and_the_way_back`): 1. "Protect" scenario; before the app starts write `src/lib.txt` "lib\nstaged\n" and stage it, then write `src/lib.txt` "lib\nstaged\nand more\n", `src/app.txt` "app\nunstaged\n" and the untracked `scratch.txt` "scratch\n". Start. Click the working-changes row (`WorkingRowPoint()`, (468, 129)). Screenshot. 2. Right-click the row "Describe the app" (commit row 3, (468, 233)) in its Message column and click "Check out this commit". Wait for "Check out f39ea2d" (Succeeded). Screenshot. 3. Double-click `main` under Local branches. Wait for "Check out main" (Succeeded). Screenshot. 4. Click the toolbar's Pop (`PopButton`). Wait for "Pop stash@{0}" (Succeeded). Click the working-changes row. Screenshot.<br>**Part B** (`Check_P25_undo_and_redo_of_the_automatic_stash`, a new copy, a new start): 5. As steps 1 and 2, without their screenshots; the check reads the exact state after step 1's changes, before the app starts. 6. Click the toolbar's Undo (`UndoButton`). Wait for "Undo checkout of f39ea2d" (Succeeded). Screenshot. 7. Click the toolbar's Redo (`RedoButton`). Wait for "Redo checkout of f39ea2d" (Succeeded). Screenshot. 8. Click Undo again and wait for "Undo checkout of f39ea2d". Click the working-changes row. Screenshot. | **After step 1:** P0w; the working-changes row selected; the stage panel: "Unstaged files (3)": A `scratch.txt`, M `app.txt` `src`, M `lib.txt` `src`; "Staged files (1)": M `lib.txt` `src`. `git status --porcelain=v1 --untracked-files=all --ignored` prints ` M src/app.txt`, `MM src/lib.txt`, `?? scratch.txt`, `!! settings.local.json`. Undo `Nothing to undo`.<br>**After step 2:** the commit menu (header `f39ea2d Describe the app`) had "Check out this commit" first and enabled. No question and no dialog. One operation, "Check out f39ea2d": the status bar showed `Stashing your changes…`, then `Checking out f39ea2d…`, then, in the secondary colour and whole, `Your changes are in the stash "Auto stash before checking out f39ea2d".`, which is also its tooltip; the log line `protect: Check out f39ea2d: Succeeded after <N> ms: Your changes are in the stash "Auto stash before checking out f39ea2d".`. The call log has, in this order, `git stash push --include-untracked --message "Auto stash before checking out f39ea2d"` (exit 0; standard output `Saved working directory and index state On main: Auto stash before checking out f39ea2d`) and `git switch --detach --no-overwrite-ignore f39ea2d9addec45f926151cd2267cb57ea0b16aa` (exit 0; `HEAD is now at f39ea2d Describe the app`), and no `git stash pop` or `git stash apply`. `CurrentBranch` `Detached at f39ea2d`. The graph P1, its stash line dashed and no other; Describe the app (row 4) selected and shown in the right panel; no working-changes row; P1's left panel. Undo enabled, `Undo checkout of f39ea2d`; Redo disabled, `Nothing to redo`; Pop enabled; Stash disabled, `There are no changes to stash`. Git: HEAD detached at f39ea2d; `git stash list` prints `stash@{0}: On main: Auto stash before checking out f39ea2d`, the stash 256077f… with P1's parents, author and committer "Test Author" at 2026-01-02 09:00; `git ls-tree -r --name-only stash@{0}^3` prints `scratch.txt`, and `scratch.txt` is gone from the working tree (the push ran without `GIT_LITERAL_PATHSPECS`, D108); the status `!! settings.local.json` only, the file still `{\n  "theme": "dark"\n}\n`; `main` at 41f8398; HEAD's reflog's newest entries `checkout: moving from main to f39ea2d9addec45f926151cd2267cb57ea0b16aa`, then `reset: moving to HEAD` (the stash's); no backup ref.<br>**After step 3:** the double-click's first click selected `main` and Main work (row 1). No "Leave these commits?" question (Describe the app is on `main`: 2.1's count is 0). The call log has `git switch --no-guess main` (exit 0; `Previous HEAD position was f39ea2d Describe the app` / `Switched to branch 'main'`). The status bar "Checked out main"; `CurrentBranch` `main`. The graph P1m: the stash is still listed and drawn (nothing pops it, D153 (3)); Stashes 1; `main` accented. Undo `Undo checkout of main`. Git: HEAD on `main` at 41f8398; the status `!! settings.local.json` only; `git stash list` as after step 2; HEAD's reflog's newest entry `checkout: moving from f39ea2d9addec45f926151cd2267cb57ea0b16aa to main`.<br>**After step 4:** the app's check before the pop found nothing in the way (no local change; the stash's untracked `scratch.txt` absent), and the pop saved `refs/visualcommit/backup/stash-20260102T090001000Z` → 256077f… (D171; the fixed backup clock's first name). The call log has `git stash pop stash@{0}` (phase 3's plain pop, 2.25; exit 0; standard output ends `Dropped stash@{0} (256077fbd990adad7fe00df31789590d7370aff0)`). The status bar "Popped stash@{0}". The graph P0w (no stash row); Stashes 0; the working-changes row selected; the stage panel: "Unstaged files (3)": A `scratch.txt`, M `app.txt` `src`, M `lib.txt` `src`; "Staged files (0)" with "No staged changes": the plain pop gives `src/lib.txt`'s staged version back unstaged, every file's contents unchanged. Undo `Undo pop of stash "On main: Auto stash before checking out f39ea2d"`; Pop disabled, `There are no stashes`. Git: HEAD on `main`; the status ` M src/app.txt`, ` M src/lib.txt`, `?? scratch.txt`, `!! settings.local.json`; `git diff --cached` prints nothing; `src/app.txt` "app\nunstaged\n", `src/lib.txt` "lib\nstaged\nand more\n", `scratch.txt` "scratch\n", `settings.local.json` as before; `git stash list` prints nothing.<br>**After step 5:** as after step 2.<br>**After step 6:** before the click Undo was enabled with `Undo checkout of f39ea2d`. No question. One operation: the status bar showed `Undoing checkout of f39ea2d…`, then `Undid checkout of f39ea2d`. The call log has, in this order, `git switch --no-guess --no-overwrite-ignore main` (exit 0) and `git stash pop --index stash@{0}` (exit 0; standard output ends `Dropped stash@{0} (256077fbd990adad7fe00df31789590d7370aff0)`), and no `git stash push`. `CurrentBranch` `main`; the graph P0w with Describe the app (commit row 3, display row 4) still selected; Stashes 0; `main` accented. Undo disabled, `Nothing to undo`; Redo enabled, `Redo checkout of f39ea2d`. Git: HEAD on `main` at 41f8398; the status ` M src/app.txt`, `MM src/lib.txt`, `?? scratch.txt`, `!! settings.local.json`, and the exact state equal to the one read before the app started; `git stash list` empty; HEAD's reflog's newest entries `reset: moving to HEAD` (the pop with `--index`), then `checkout: moving from f39ea2d9addec45f926151cd2267cb57ea0b16aa to main`; no backup ref.<br>**After step 7:** before the click Redo was enabled with `Redo checkout of f39ea2d`. The status bar showed `Redoing checkout of f39ea2d…`, then `Redid checkout of f39ea2d`. The call log has `git stash push --include-untracked --message "Auto stash before checking out f39ea2d"` (exit 0), then `git switch --detach --no-overwrite-ignore f39ea2d9addec45f926151cd2267cb57ea0b16aa` (exit 0). The state of step 2 again: the graph P1 (Describe the app selected), the stash again 256077f… (the same trees, parents, message and dates), the status `!! settings.local.json` only, `CurrentBranch` `Detached at f39ea2d`. Undo `Undo checkout of f39ea2d`; Redo disabled, `Nothing to redo`; no backup ref.<br>**After step 8:** the calls of step 6 again; the status bar `Undid checkout of f39ea2d`; the working-changes row selected; the stage panel: "Unstaged files (3)": A `scratch.txt`, M `app.txt` `src`, M `lib.txt` `src`; "Staged files (1)": M `lib.txt` `src` (the index is back, which part A's plain pop does not give); the exact state equal to the one read before the app started. | Not run yet | RW: part A, steps 1 and 2 (a real click on the working-changes row, a real right-click on Describe the app, a real click on "Check out this commit"; `p25a-changes`, `p25b-auto-stashed`), then, in the same launch, part B's step 6: the `UndoButton`'s `HelpText` `Undo checkout of f39ea2d` read through UI Automation and a real click on it; `p25e-undone` compared with part B's scripted picture (the same state) | `p25a-changes`, `p25b-auto-stashed`, `p25c-main-again`, `p25d-popped`, `p25e-undone`, `p25f-redone`, `p25g-index-back`; real window: `p25a-changes`, `p25b-auto-stashed`, `p25e-undone` | D153 (2), (3); D166; D179 (`--no-overwrite-ignore`, the stash first); D161 (the dashed line; F15 samples its pattern); D171 (the pop's backup). Design 9.1 row 1, 9.2 steps 1 to 6, 7.3 (the automatic stash's checkout's sequences), 7.4, 7.5, 7.6, 12.1, 12.2, 13.2 (no question). The toolbar's Pop is phase 3's plain pop (decision PD12). Decisions PD1, PD2, PD11 to PD14. |
| P26 | When the automatic stash is not taken, or is put back (design 9.1, 9.2 step 5, 9.3): HEAD's own commit (no stash; HEAD detached, the change kept); a commit only `other` reaches (no stash; refused with 9.3's sentence and the button); a commit that tracks the now-ignored `settings.local.json`, on a clean tree (no stash; refused with the ignored-files text) and with changes (stashed, refused, popped back, everything as before, the file intact); "protect", dark, 1100×700 | **Part A** (`Check_P26_no_stash_at_head_or_off_the_branch`): 1. "Protect" scenario; before the app starts write `src/app.txt` "app\nlocal\n" (not staged). Start. 2. Right-click the row "Other edits" (commit row 2, (468, 207)) in its Message column and click "Check out this commit". Wait for "Check out 4f5e821" (Failed) and its dialog. Screenshot. Click "Close". 3. Right-click the row "Main work" (commit row 0, (468, 155)) and click "Check out this commit". Wait for "Check out 41f8398" (Succeeded). Screenshot.<br>**Part B** (`Check_P26_ignored_file_in_the_way`, a new copy, a new start): 4. "Protect" scenario, no change. Start. Right-click the row "Add the guide" (commit row 5, (468, 259)) and click "Check out this commit". Wait for "Check out 73e0bab" (Failed) and its dialog. Screenshot. Click "Close". 5. Outside the app make P25 step 1's changes (stage `src/lib.txt` "lib\nstaged\n", then write `src/lib.txt` "lib\nstaged\nand more\n", `src/app.txt` "app\nunstaged\n", `scratch.txt` "scratch\n"); wait for the status read `1 staged, 3 unstaged`; the check reads the exact state. 6. Right-click the row "Add the guide" (commit row 5, now (468, 285)) and click "Check out this commit". Wait for "Check out 73e0bab" (Failed) and its dialog. Screenshot. Click "Close". | **After step 1:** P0w; Undo `Nothing to undo`.<br>**After step 2:** "Other edits" selected (the right-click). Other edits is not on `main` (no stash: 9.1's second row). The call log has `git switch --detach 4f5e8215c9321acae3a2ea386bda05107915f2da`, exit 1 (TP1d), and no `git stash` call. The error dialog: "Checkout blocked"; "1 conflict prevents checkout. Commit, stash, or discard your changes first."; "In protect"; `ErrorOutput` TP1d; `ErrorCommand` `git switch --detach 4f5e8215c9321acae3a2ea386bda05107915f2da`; "Copy output", "Stash changes and check out" and "Close". The status bar "Check out 4f5e821 failed" in the danger colour. Nothing changed: HEAD on `main` at 41f8398, ` M src/app.txt`, `git stash list` empty, HEAD's reflog unchanged. Undo `Nothing to undo`.<br>**After step 3:** the commit menu (header `41f8398 Main work`) had "Check out this commit" enabled (HEAD is on `main`, not detached at it: phase 3's `HEAD is already at this commit` does not apply). No question and no dialog. The call log has `git switch --detach 41f8398f95fa73a5614e32760cc511673aa6a80d` (exit 0; standard output `M⇥src/app.txt`; standard error `HEAD is now at 41f8398 Main work`): HEAD's own commit is 9.1's second row, run as today, without `--no-overwrite-ignore`; and no `git stash` call (never at HEAD's own commit, D179). The status bar "HEAD is now detached at 41f8398" (phase 3's text: no stash was made); `CurrentBranch` `Detached at 41f8398`. The graph P0h; Main work (commit row 0) selected; no branch accented in the left panel; Stashes 0. Undo enabled, `Undo checkout of 41f8398`. Git: HEAD detached at 41f8398; `src/app.txt` still "app\nlocal\n" (` M src/app.txt`); `git stash list` empty; HEAD's reflog's newest entry `checkout: moving from main to 41f8398f95fa73a5614e32760cc511673aa6a80d`; `main` at 41f8398; no backup ref.<br>**After step 4:** P0 before the click (no working-changes row: an ignored file is no change). "Add the guide" selected. The call log has `git switch --detach --no-overwrite-ignore 73e0babca7d61aaa9088f167ddbf0951f5f13744`, exit 1 (TP6), and no `git stash` call (nothing to stash, 9.1, 9.2 step 1); the app asked which blocked paths are ignored, `git check-ignore --stdin -z` (without `GIT_LITERAL_PATHSPECS`, D74; exit 0, `settings.local.json`). The error dialog: "Checkout blocked"; "1 ignored file is in the way: 73e0bab tracks a file of the same name. Move it away first."; "In protect"; `ErrorOutput` TP6; `ErrorCommand` `git switch --detach --no-overwrite-ignore 73e0babca7d61aaa9088f167ddbf0951f5f13744`; "Copy output" and "Close", no extra button. The status bar "Check out 73e0bab failed" in the danger colour. Nothing changed: HEAD on `main`; `settings.local.json` `{\n  "theme": "dark"\n}\n`; the status `!! settings.local.json` only; HEAD's reflog unchanged; no stash. Undo `Nothing to undo`.<br>**After step 5:** P0w; `git status --porcelain=v1 --untracked-files=all --ignored` prints ` M src/app.txt`, `MM src/lib.txt`, `?? scratch.txt`, `!! settings.local.json`.<br>**After step 6:** no question. The call log has, in this order, `git stash push --include-untracked --message "Auto stash before checking out 73e0bab"` (exit 0; `Saved working directory and index state On main: Auto stash before checking out 73e0bab`), `git switch --detach --no-overwrite-ignore 73e0babca7d61aaa9088f167ddbf0951f5f13744` (exit 1, TP6) and `git stash pop --index stash@{0}` (exit 0; standard output ends `Dropped stash@{0} (f0c95a478241bba9511b657bc7279278402915f1)`). The error dialog as after step 4: the same title, summary, `ErrorOutput` and `ErrorCommand`, and no "Stash changes and check out" (9.2 step 5). The status bar "Check out 73e0bab failed". Git: the exact state equal to the one read in step 5; `settings.local.json` still `{\n  "theme": "dark"\n}\n`; `scratch.txt` "scratch\n"; `git stash list` empty; HEAD on `main` at 41f8398; HEAD's reflog gained two entries, both `reset: moving to HEAD` (the stash and the pop); no backup ref. Undo `Nothing to undo` (HEAD, the refs, the stash list and the status are as before the action: no entry, 7.2, 7.7). | Not run yet | n/a: the error dialog is pictured in the real window by P24 and phase 3's check 14; these differ in their texts and the git calls behind them, which both harnesses read alike; the menus and graphs are earlier phases' and P25's | `p26a-off-the-branch`, `p26b-head-own-commit`, `p26c-ignored-file`, `p26d-ignored-after-stash` | D179 ("never at HEAD's own commit"; `--no-overwrite-ignore` for every commit the branch reaches other than HEAD's own, 9.1's first row); D74 (`git check-ignore` runs without `GIT_LITERAL_PATHSPECS`); design 0.7 (an old commit would overwrite the ignored file, the way back delete it), 9.1 (HEAD's own commit falls in its second row: as today), 9.2 steps 1 and 5, 9.3 (the ignored-files text; `check-ignore --stdin -z`). A detached checkout of HEAD's own commit detaches HEAD and carries the change (derived here; 9.1 says only that it changes no file and takes no stash). Decisions PD7 to PD9, PD11. |
| P27 | Other refusals (design 9.3's second table): a merge, a cherry-pick and a revert over a changed file (`1 conflict prevents the merge…`, `…the cherry-pick…`, `…the revert…`), the three over a staged file (`Your staged changes prevent the …`); a stash's apply and pop refused by the app's own check before git (`1 conflict…`, `2 conflicts prevent applying the stash…`, git not run); nothing changes; "protect", dark, 1100×700 | **Part A** (`Check_P27_merge_pick_and_revert_refused`): 1. "Protect" scenario; before the app starts write `src/app.txt` "app\nlocal\n" (not staged). Start. 2. Right-click `other` under Local branches and click "Merge other into main". Wait for "Merge other" (Failed) and its dialog. Screenshot. Click "Close". 3. Right-click the row "Other edits" (commit row 2, (468, 207)) and click "Cherry-pick onto main". Wait for "Cherry-pick 4f5e821" (Failed) and its dialog. Screenshot. Click "Close". 4. Outside the app write `README.md` "# Protect\n\nLocal.\n" (not staged); wait for the status read `0 staged, 2 unstaged`. Right-click the row "Describe the app" (commit row 3, (468, 233)) and click "Revert this commit". Wait for "Revert f39ea2d" (Failed) and its dialog. Screenshot. Click "Close". 5. Outside the app run `git checkout -- src/app.txt`, write `README.md` "# Protect\n\nStaged.\n" and stage it; wait for the status read `1 staged, 0 unstaged`. 6. As step 2. Screenshot. Click "Close". 7. As step 3. Screenshot. Click "Close". 8. Right-click the row "Describe the app" (commit row 3, (468, 233)) and click "Revert this commit". Wait for "Revert f39ea2d" (Failed) and its dialog. Screenshot. Click "Close".<br>**Part B** (`Check_P27_stash_apply_refused_by_the_app`, a new copy, a new start): 9. "Protect" scenario; before the app starts write `src/app.txt` "app\nstashed\n" and the untracked `todo.txt` "todo\n", run `git stash push --include-untracked --message "App experiment"` without `GIT_LITERAL_PATHSPECS` (`TempRepo.GitWithEnvironmentAsync`, as the pins do; on the copy's clock, 12:07), then write `src/app.txt` "app\nlocal\n". Start. Right-click Stashes › On main: App experiment. Click "Apply stash". Wait for "Apply stash@{0}" (Failed) and its dialog. Screenshot. Click "Close". 10. Outside the app write `todo.txt` "mine\n"; wait for the status read `0 staged, 2 unstaged`. 11. Click the toolbar's Pop (`PopButton`). Wait for "Pop stash@{0}" (Failed) and its dialog. Screenshot. Click "Close". | **After step 1:** P0w; Undo `Nothing to undo`.<br>**After step 2:** the menu opened without selecting anything (D114). The call log has `git merge --no-edit --no-log -m "Merge branch 'other'" refs/heads/other`, exit 128 (TP7; 2.55: exit 2 and its fifth line). The error dialog: `ErrorTitle` "Merge failed"; `ErrorSummary` "1 conflict prevents the merge. Commit, stash, or discard your changes first."; "In protect"; `ErrorOutput` TP7; `ErrorCommand` `git merge --no-edit --no-log -m "Merge branch 'other'" refs/heads/other`; "Copy output" and "Close". The status bar "Merge other failed" in the danger colour. No banner. Nothing changed: no `MERGE_HEAD`; HEAD on `main` at 41f8398; ` M src/app.txt` ("app\nlocal\n"); HEAD's reflog unchanged. Undo `Nothing to undo`.<br>**After step 3:** "Other edits" selected; "Cherry-pick onto main" was enabled. The call log has `git cherry-pick 4f5e8215c9321acae3a2ea386bda05107915f2da`, exit 128 (TP8). The error dialog: "Cherry-pick failed"; "1 conflict prevents the cherry-pick. Commit, stash, or discard your changes first."; "In protect"; `ErrorOutput` TP8; `ErrorCommand` `git cherry-pick 4f5e8215c9321acae3a2ea386bda05107915f2da`; "Copy output" and "Close". The status bar "Cherry-pick 4f5e821 failed". No banner; no `CHERRY_PICK_HEAD`, no `sequencer`; HEAD and the files as before.<br>**After step 4:** before the click `git status --porcelain` printed ` M README.md`, ` M src/app.txt`. "Describe the app" selected; "Revert this commit" was enabled. The call log has `git revert --no-edit f39ea2d9addec45f926151cd2267cb57ea0b16aa`, exit 128 (TP12). The error dialog: `ErrorTitle` "Revert failed"; `ErrorSummary` "1 conflict prevents the revert. Commit, stash, or discard your changes first." (one path, `README.md`: the revert touches no other file); "In protect"; `ErrorOutput` TP12; `ErrorCommand` `git revert --no-edit f39ea2d9addec45f926151cd2267cb57ea0b16aa`; "Copy output" and "Close". The status bar "Revert f39ea2d failed" in the danger colour. No banner; no `REVERT_HEAD`, no `sequencer`; HEAD on `main` at 41f8398; ` M README.md` ("# Protect\n\nLocal.\n"), ` M src/app.txt` ("app\nlocal\n"); HEAD's reflog unchanged. Undo `Nothing to undo`.<br>**After step 5:** P0w; `git status --porcelain` prints `M  README.md`.<br>**After step 6:** the merge's call again, exit 128 (TP9; 2.55: exit 2, three lines). The error dialog: "Merge failed"; "Your staged changes prevent the merge. Commit, stash, or unstage them first."; "In protect"; `ErrorOutput` TP9 (key `  README.md`); the merge's `ErrorCommand`; "Copy output" and "Close". The status bar "Merge other failed". No `MERGE_HEAD`; `M  README.md`; HEAD unchanged.<br>**After step 7:** the cherry-pick's call again, exit 128 (TP10). The error dialog: "Cherry-pick failed"; "Your staged changes prevent the cherry-pick. Commit, stash, or unstage them first."; `ErrorOutput` TP10; the cherry-pick's `ErrorCommand`; "Copy output" and "Close". The status bar "Cherry-pick 4f5e821 failed". No `CHERRY_PICK_HEAD`.<br>**After step 8:** "Describe the app" selected; "Revert this commit" was enabled. The call log has `git revert --no-edit f39ea2d9addec45f926151cd2267cb57ea0b16aa`, exit 128 (TP11). The error dialog: "Revert failed"; "Your staged changes prevent the revert. Commit, stash, or unstage them first."; "In protect"; `ErrorOutput` TP11; `ErrorCommand` `git revert --no-edit f39ea2d9addec45f926151cd2267cb57ea0b16aa`; "Copy output" and "Close". The status bar "Revert f39ea2d failed". Nothing changed in the whole part: HEAD on `main` at 41f8398, `M  README.md` ("# Protect\n\nStaged.\n"), no `MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD` or `sequencer`, HEAD's reflog unchanged before Git 2.38, 2.36 among them (2.38 and later add one no-op entry for step 6's merge, `merge refs/heads/other: updating HEAD`, its old and new value both 41f8398; design 15.9), no backup ref. Undo `Nothing to undo`.<br>**After step 9:** at the start P0s, Stashes 1 ("On main: App experiment"), ` M src/app.txt` only (`todo.txt` is in the stash). The stash's menu: header `stash@{0} On main: App experiment`; "Apply stash" (highlighted), "Pop stash", "Drop stash…" (danger), a separator, "Copy SHA"; 145 high. After the click no `git stash apply` and no `git stash pop` ran: the app's check read the stash's paths (`src/app.txt` changed by it; `todo.txt` in its untracked part) and found `src/app.txt` changed locally. The error dialog: `ErrorTitle` "Applying the stash failed"; `ErrorSummary` "1 conflict prevents applying the stash. Commit, stash, or discard your changes first."; "In protect"; no `ErrorOutput`, no `ErrorCommand`, no "Copy output": "Close" only. The status bar "Apply stash@{0} failed" in the danger colour. Nothing changed: `git stash list` prints `stash@{0}: On main: App experiment`; ` M src/app.txt` ("app\nlocal\n"); `todo.txt` does not exist (git's own `git stash apply stash@{0}` here would refuse the tracked change and still write `todo.txt`, design 0.5). Undo `Nothing to undo`.<br>**After step 11:** Pop was enabled. No `git stash pop` ran. The error dialog: "Applying the stash failed"; "2 conflicts prevent applying the stash. Commit, stash, or discard your changes first." (`src/app.txt` changed locally and `todo.txt` existing); "In protect"; "Close" only. The status bar "Pop stash@{0} failed" in the danger colour. Nothing changed: the stash kept; ` M src/app.txt`, `?? todo.txt` ("mine\n"); no backup ref (the check refuses before the pop's backup, D171); Undo `Nothing to undo`. | Not run yet | n/a: the error dialog is pictured in the real window by P24 and phase 3's check 14; these differ in their texts and, for the stash's refusals, in having no git output, which both harnesses read alike; the menus are phase 3's | `p27a-merge-blocked`, `p27b-pick-blocked`, `p27c-revert-blocked`, `p27d-merge-staged`, `p27e-pick-staged`, `p27f-revert-staged`, `p27g-apply-refused`, `p27h-pop-refused` | D179 (the sentence on merge, pick and revert refusals; the stash's apply checked by the app before git); design 0.5, 0.7, 9.3; the merge refusals differ on 2.55 (TP7, TP9). The revert, in both forms, is added to design 15.4's list (the same rows of 9.3). Decisions PD1, PD10, PD11, PD15. |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say
"n/a" with the reason.

## What proves each Delivers item

Every item of phase 5's "Delivers" list is proven by an automated test or by a visual check (the
plan's rule). This is design 15.5's table with this report's checks; the tests are those design
15.5, 7.11, 8.5 and 9.4 name.

| Delivers (plan.md, phase 5; design 15.5) | Visual checks | Automated tests |
|---|---|---|
| File history (following renames) and blame, with click-through to commits, in place of the graph (D158) | B5 (the history across the rename `R084`, opened from the stage panel's file, a commit's file and a diff's menu; a step's diff; the right panel unchanged until a row is clicked; the list walked with Down; `Show in graph`; Esc revealing the commit; the side-by-side layout at 1920×1080, light), B6 (the gutter's runs and dates, `Not committed yet`, a click and a real double-click on a run, `Blame before this commit` across the rename and its disabled reason, the history from the blame and back), B7 (an unchanged file's blame, a line traced to its commit) | `FileHistoryTests`, `BlameParserTests`, `BlameTests` (Git), `FileHistoryViewModelTests`, `BlameViewModelTests` |
| Reflog viewer with restore; activity log panel under the graph, opened from the status bar (D156) | B8 (HEAD's reflog with its lost marks and the gap's footer, a branch's by the source menu and by `Show reflog of {b}`, a lost commit restored as a branch), B9 (the backups list and its restores of a deleted branch, a deleted remote branch, discarded files, an abort's and a skip's changes and a mixed reset's staged changes); A2 (where the panel is, every row kind, each outcome's glyph and tone, the failure's summary, the notes, calls with their input, error and output, `Show reads` by structure, the order by sequence with frozen times, Esc with a page open), A3 (its height, its limits, kept across a restart) | `ReflogTests` (Git: both calls, gaps, missing reflogs, lost), `ReflogViewModelTests`, `BackupReaderTests`, `ActivityLogViewModelTests`, `OperationLogTests`, `GitCallLogTests`, `ActivityLogViewTests` |
| Commit search by message, author, SHA and file; search inside changes; matches marked in the graph (D157) | F10 (a word only in message bodies, an author, a SHA prefix; the bar, the marks and dimmed rows, the count's forms, Enter and Shift+Enter wrapping round, Esc keeping the selection; the light token at 1920×1080), F11 (the mode menu; Files with both names of a rename and `No matches`; Changes with a string added and removed; a reload that keeps the marks and searches only the new row); A4 (the bar opened by Ctrl+F and closed by Esc) | `SearchTests` (Git: each mode, chunks, merges, stashes, the empty text), `SearchViewModelTests`, `CommitGraphControlTests` (marks) |
| Compare any two commits or branches: two selected commits show what changed between them at once (D162) | F12 (two-dot: the right panel at once, a file's diff, Search closing it, Swap, the list's file menu, a link, a third commit and back, a single selection ending it), F13 (three-dot from a branch menu with the working-changes row selected, the merge base's file diff, `All differences`, the counts, closing with the draft kept, from a drop the other way), F14 step 5 (a link disabled for a commit the graph lacks; the counts' singular) | `CompareTests` (Git: two-dot, three-dot, unrelated, criss-cross, renames), `CompareViewModelTests` |
| Undo and redo of the app's git actions, many steps back within the session (D154); undoing a change to a remote asks, then pushes back with a lease (D155); the platform's keys outside text boxes (D163) | U16 to U23 (every recorded kind of design 7.2 that phases 2 to 4 let a user make, undone and redone once in the app; many steps: U16 part H, two entries undone and two redone in a row, and U23 part B; every remote change asked about and pushed back with an explicit lease both ways, and a lease that fails: U21; the refusals of 7.4 and 7.6: U23), P25 part B (the automatic stash's checkout undone, redone and undone again, D166); A1 (Undo enabled at once after a recorded action), A4 (Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z in the graph, declined in a text box and in the resolver) | 7.11's tests: `UndoHistoryTests`, `UndoSequenceTests` (Core), `UndoEffectTests` (Git), `UndoRecordingTests`, `UndoToolbarTests` (App) |
| Solo and hide branches in the graph, remembered per repository across restarts (D159) | F14 (Hide with the upstream, the left panel's marks, a hidden row's click and tooltip, Show; Solo, the labels left out, Stop soloing; a restart keeps the hidden branch, its toggle shows it again) | 8.5's tests: `GraphFilterTests` (Git), `CommitGraphDataTests`, `LeftPanelViewModelTests`, `GraphFilterChangesTests` (the store: `The_store_keeps_one_entry_per_repository_removes_an_empty_one_and_bounds_the_list`, `A_session_file_without_filters_or_with_lists_left_out_is_read`, `At_start_up_the_entries_of_folders_that_no_longer_exist_are_dropped`), `HideAndSoloTests` (the toggle's view tests: `A_double_click_on_a_toggle_checks_nothing_out`, `A_hidden_branch_stays_hidden_after_a_restart_and_its_eye_shows_it_without_a_drag`), `RepositoryViewModelFilterTests` (the app's own create, rename and delete, their undo; design 15.9) |
| C12, protect uncommitted changes (D138, D153): the refused checkout with its message and the "Stash changes and check out" button; the automatic stash, kept and drawn as a hollow circle joined to its commit; every stash's line dashed (D161) | P24 (one and three conflicts with the button, by a double-click and the branch menu; "Create branch" with its box, no button; an untracked file and a folder, no button), P25 (the automatic stash with staged, unstaged and untracked changes: no question, the ring on `main`'s tip with its dashed line, the status text, the detached HEAD; kept when `main` is checked out again; popped by the user), P26 (not taken at HEAD's own commit and off the branch; an ignored file in the way, on a clean tree and after the stash, popped back), P27 (merge, cherry-pick and revert refusals; a stash's apply and pop refused by the app's check); F15 (the dash pattern of every stash's line) | 9.4's tests (Git and App layers: `FailureClassifierTests`, `RepositoryOperationsCheckoutTests`, the refused forms changing nothing, the automatic stash's round trip), `GraphLayoutTests` (`IsDashed`), `CommitGraphControlTests` |
| From phase 4 (D160): the details panel's SHA that wrapped, the resolver's arrow buttons, the status bar's Cancel tooltip while the merge tool runs, two stale comments, the watcher's quiet period | no phase 5 check: phase 4's pictures `a3d`, `b8c`, `c11j` and the resolver's pictures with one conflicted file, as "Changes to expected results" gives them; A2 step 8 shows Cancel's tooltip text `Stop git and the merge tool` | the leftovers' tests (`279dd8c`) |
| From phases 3 and 4: undo reads the backup refs that phases 2 to 4 write; the clean-up keeps what undo needs; the clean-up's lines in the activity log | B8 and B9 (the clean-up at opening leaves 15 of 17; the list reads and words every kind; restores outside the session, recorded in it), A2 (the clean-up's notes at opening and after the abort's backup), U16 to U23 (every backup an action makes, named exactly, kept through its undo and redo; `tag-undone-…` and a redone hard reset's new pair made) | `BackupReaderTests`, `UndoRecordingTests` (the keep set), `RepositoryOperationsBackupCleanupTests` |
| From phase 3: the activity log reads `OperationLog` and `IGitCallLog`; from phase 4: the background "Clean up backups" and a success that is a warning | A2 (every row compared with the two logs; the background `Clean up backups`; the warning tone through a stop with conflicts) | `OperationLogTests`, `ActivityLogViewModelTests` (also the glyph of a success with `IsWarning`, which no check makes) |
| From phase 3 (D77, D92): compare on a multi-selection, file history and blame in the file menus, solo and hide in the branch menus | F12 (compare on a two-commit selection, without a menu), F13 (`Compare {b} with {cur}` and a drop's `Compare {A} with {B}`), B5 (`ForUnstagedFile`, `ForCommitFile`, `ForDiff`), B6 (`ForUnstagedFile`'s Blame), B7 (`ForCommitFile` of an unchanged file), F14 (Hide, Show, Solo; "Hide main" disabled; the sections' "Show all hidden branches" and "Stop soloing") | `RepositoryMenusTests` (every changed builder, `ForStagedFile` included, the resolver's header without the items) |
| From phases 1 and 3: the toolbar's Undo, Redo and Search become real | A1 (their states, tooltips and help texts on an empty tab, on a repository, after a commit, across tabs), A4 (Search disabled with the resolver open), F10 (Search opens the bar), U16 to U23 (Undo's and Redo's states after every step) | `UndoToolbarTests`, `ToolbarTests` |

The plan's visual checks and its "Done when":

- File history across a rename; blame gutter and click-through: B5, B6. Reflog restore: B8, B9;
  activity log entries: A2; search results: F10, F11; compare view: F12, F13. Undo and redo for each
  destructive action from phases 2 to 4: the list below. Solo and hide change the graph: F14. C12, a
  refused checkout's message and its button: P24 (`p24b-one-conflict`, `p24d-three-conflicts`); the
  automatic stash in the graph after an older commit of the branch is checked out: P25
  (`p25b-auto-stashed`); both also in the real window.
- "Any line of any file can be traced to its commit in two clicks": B7 (one click on an unchanged
  file, or its menu's Blame, then one click on the line). "Each destructive action can be undone":
  the list below. "No checkout loses uncommitted changes": P24 (every refusal leaves the files, the
  index, the refs, the stash list and HEAD's reflog), P25 (the way back by hand gives every change
  back; Undo gives the exact state back; the ignored `settings.local.json` untouched throughout),
  P26 (the ignored file intact on a clean tree and after the stash; the exact state after the stash
  is popped back), P27 (merge, pick, revert and a stash's apply and pop change nothing).

**Every destructive action of phases 2 to 4** (design 15.5's list, 7.2's table) and the check that
undoes or restores it:

| Destructive action | Undone (part U) | Restored outside the session, or by other means |
|---|---|---|
| Discard of a file, a hunk, lines, all | U16 parts C to F | B9 (`Restore the discarded files…`, with D75's question) |
| Reset, each mode | U17 (soft, mixed, mixed with staged changes, hard) | B9 (`Restore the staged changes` of a mixed reset); B8 (a hard reset's lost commit through the reflog's `Create branch here…`) |
| Delete a branch | U18 part D | B9 (`Restore branch doomed`) |
| Delete a branch on a remote | U21 part C | B9 (`Push feature/gone back to origin…`, with its question and a "must not exist" lease) |
| Remove a remote | U22 part E | |
| Force push | U21 part B | |
| Delete a tag; delete a tag on a remote | U20 part F; U21 part E | |
| Drop a stash | U20 part D | |
| Leaving a detached HEAD's commits | U18 part H | |
| Rebase of every kind | U19 parts E (onto), F ("Check out and rebase"), H (interactive), I (squash), J (a pull's) | |
| Abort | records no entry (D165; U23 part B shows the pending entry removed) | B9 (`Restore the changes…` of `abort-…-changes`); A2 shows the abort's backup made and kept by the clean-up |
| Skip | | B9 (`Restore the changes…` of `skip-…-changes`) |
| The resolver's binary side and "Delete it" | | B9's discard restore (the same `discard-` path); `UndoRecordingTests` |

D179's points (C12's rules) and where each is proven: `--no-overwrite-ignore` for every commit the
branch reaches other than HEAD's own: P25 step 2, P26 steps 4 and 6 (HEAD's own commit is run
without it: P26 step 3); the stash first when there are changes: P25 step 2, P26 step 6; never from
a detached HEAD: App tests (9.4), not pictured; never at HEAD's own commit: P26 step 3; never for an
undo: P25 steps 6 and 8; the sentence with git's count: P24 (one and three), P27 steps 2 to 4; the
ignored-files text: P26 steps 4 and 6; a stash's apply checked by the app before git: P27 steps 9
and 11; a half-made automatic stash puts back what git deleted, and an unstaged deletion a checkout
undoes is accepted as git does: 9.4's Git tests only (Windows file locks; not pictured).

Proven by tests only, and where: the activity log rows' menu (`Copy command`, `Copy output`, `Copy
title`, `ForActivityLogRow`) by `RepositoryMenusTests` and `ActivityLogViewTests`; a call `failed to
start`, `Its git commands are no longer kept.`, the 64 MB budget and the 2,000 reads by
`ActivityLogViewModelTests` and `GitCallLogTests`; two tabs' logs by `ActivityLogViewModelTests`
(design 2.4); `ForStagedFile`'s `File history` and `Blame` by `RepositoryMenusTests` (no part B
check stages a file); a stash's and a comparison's details without "All files" by
`CommitDetailsViewModelTests` (4.5); a stopped action's pending entry completed by Continue, Skip or
the stage panel's Commit by `UndoRecordingTests` (7.11; U23 part B shows the pending entry's refusal
and its removal by Abort).

## Changes to expected results

Expected results of earlier phases that phase 5 changes on purpose (design 14.1, and what this
report's writers found beyond it), two changes made before this report, and anything changed in this
report after it was first committed (with the reason). The tests change in the same commit as the
code that changes them (plan.md's gate, step 6); WP8 changes the earlier phases' test and
expectation files, WP9 their real-window twins. Nearly every picture of phases 1 to 4 changes,
most of them only in the toolbar, the status bar and the details panel's buttons; the gate's
inspection compares them with their references as design 15.7 says and takes every difference
listed here as expected.

### Design 14.1's rows

- **The toolbar and the status bar** (14.1's first row; design 1.1, 1.2, 7.4; parts A and U). In
  every window picture of phases 1 to 4 with a repository open, Search is enabled (its icon and
  label in `VcTextPrimaryBrush` instead of `VcTextDisabledBrush`), and the status bar's `Activity
  log` is enabled (its icon and label in `VcTextSecondaryBrush` instead of `VcTextDisabledBrush`).
  Redo stays disabled, with `Nothing to redo`, in every picture of phases 1 to 4 (no check of
  theirs undoes a git action: their only undo keys are phase 4's C10 and C14 in the resolver, which
  keeps them). Undo is disabled, with `Nothing to undo`, until the check's first recorded action
  (7.2: a commit, a checkout, a branch's creation, a merge, …); in the pictures after it Undo is
  enabled, its icon and label in `VcTextPrimaryBrush`, with `Undo {text}`, except where 7.4 gives a
  reason: while a user operation runs or waits (`Wait for {title} to finish`), while an operation
  is in progress or conflicts remain (every banner picture: `Finish or abort the {kind} first`,
  `Resolve the conflicts first`; during phase 3's check 9, a merge in progress, Undo and Redo are
  disabled with `Finish or abort the merge first`), while the interactive list is open (`Close the
  interactive rebase first`), and when the entry's check fails after an outside change (phase 3's
  terminal commits: 7.6's texts); in those pictures it stays disabled, drawn in
  `VcTextDisabledBrush` as today. Phase 3's toolbar rule ("Undo, Redo and Search stay disabled
  placeholders with no tooltip") is replaced by design 1.1 and 7.4. Their tooltips show in no
  earlier picture (the pointer rests at the neutral point). Nothing moves: the closed activity log
  takes no room, so every position of phases 0 to 4 holds. Tests that pin today's state and change:
  `ToolbarTests` (`In_the_window_the_buttons_follow_the_active_tab…`, lines 289 and 311: after the
  repository opens, Search enabled with `Search commits (Ctrl+F)`, Undo and Redo disabled with
  `Nothing to undo` and `Nothing to redo`, where today all three are disabled without a tooltip),
  `Phase3Checks.A.Helpers.cs` (line 313: the same three buttons, and Undo enabled after a check's
  recorded action), `StatusBarTests` (line 38: the toggle enabled with a repository; its texts
  stay), and their real-window twins.
- **Phase 0's empty shell is unchanged** (14.1's first row; design 1.1, 1.2): only Theme enabled,
  nothing with a tooltip, the toggle disabled. `ShellExpectations.cs` (the empty tab's states and the
  status bar's texts) and `Phase0RealWindowPass` (lines 125 and 134) keep their expectations; A1
  step 1 asserts the same from a tab added with `+`.
- **The details panel's `All files` button** (14.1's second row; design 4.5; part B): every picture
  of phases 1 to 4 that shows a commit's details gains a third toggle right of `Tree`, unselected
  (the setting is off by default), in the header row of `Changed files (N)`; the list itself is
  unchanged. Tests: `PanelViewTests`, `CommitDetailsViewModelTests`, and the checks that assert the
  panel's buttons.
- **Every stash's line dashed** (14.1's third row; design 10, D161; part F). The pictures of phase
  1's graph scenario in which its stash row is in view (and their real-window twins, among them
  `01-graph-dark-1100x700`), phase 2's check 23, and phase 3's pictures of the ops scenario with its
  stash row: the lane from the stash's ring down to the node of the commit it was made on becomes
  dashed (dashes and gaps of 3); the ring, the lanes, colours and rows stay as they were. Tests:
  `GraphLayoutTests` (their `IsDashed` facts), `CommitGraphControlTests` (line 115's colour sample
  in the stash's lane, which a gap can now hit: it samples a dash), and `Phase3Checks.B.Support.cs`'s
  `ExpectedLines` (already changed: "Made before this report" below).
- **New menu items** (14.1's fourth row; design 13.1; parts B and F). Every earlier picture, item
  list and height of a changed builder's menu gains phase 5's items; a menu that no longer fits
  below the pointer opens above it or is moved (phase 3's 4a), so its position can change too. The
  pictures (and their real-window twins): `ForLocalBranch` (phase 3's `02a`, `02b`, `02c`, `02f`,
  `02g`, `05e`, `08b`, `09d`, `13f`, `13l`, `18b1`, `20b`, `28a`, `28c`, `30a`, `30f`, `32c`; phase
  4's `a1d`, `a1f`), `ForRemoteBranch` (`02d`, `02e`, `02h`, `06d`, `08f`, `08g`, `13b1`, `30c`;
  `a1e`), `ForRemote` (`03c`), `ForTag` (`03a`, `16f`), `ForEmptyGraphArea` (`03f`, `05b1`, `23i`),
  `ForWorkingChanges` (`04l`, `04m`), the file menus (`04a`, `04c`, `04d`, `04f`, `04g`, `04h`,
  `04j`, `04k`, `05b2`, `09b2`, `26b`, `26d`, `26f`, `26g`; phase 4's `c16a` only where the stage
  list's menu is pictured, the resolver's header menu having no new items) and `ForDrop` (`07b`,
  `07c`, `07d`, `30g`; `a3a`, `a3b`, `a3c`, `c13i`). What each gains:
  - **The file menus** gain `File history` and `Blame` (4.4), a group of two before `Open file`, so
    each grows by 61 px (2 items and a separator): `04a` (an unstaged tracked file: 232 → 293);
    `04c` and `26b` (an untracked file: the two items disabled with `This file is in no commit
    yet`); `04d` (a staged new file, `config/settings.json`: 145 → 206, the two items disabled with
    `This file is in no commit yet`); `04j` (a commit's file: 110 → 171); the diff menus `04f`, `04g`,
    `04h`, `26d` and the commit diff's `04k`; the conflicted files' `05b2`, `09b2`; `26f`, `26g`.
  - **`Show reflog` and `Show backups`** (5.1) join the last group of the working-changes row's menu
    (`04l`, `04m`: +52 px, before `Show in folder`) and of the empty graph area's menu (`03f`,
    `05b1`, `23i`: +52 px, before `Manage remotes…`; enabled in an unborn repository too, decision
    BD22).
  - **A local branch's menu** gains phase 5's group before `Copy branch name`: `Compare {b} with
    {cur}` (not on the current branch), part B's `Show reflog of {b}`, `Hide {b}` and `Solo {b}`,
    with its separator: a branch that is not checked out +113 (phase 4's 458 becomes 571, 380
    becomes 493), the checked-out branch +87 (3 items and a separator: 354 becomes 441). A remote
    branch +87 (`Compare {r}/{b} with {cur}`, Hide, Solo and a separator: 293 becomes 380); a tag +35
    (`Compare {t} with {cur}` and a separator); a remote +35 (`Hide every branch of {r}` and a
    separator); a drop +35 (`Compare {A} with {B}` and a separator: 145 becomes 180, 171 becomes
    206, 110 becomes 145).
  - **The sections' menus** (`ForSection`): the Local branches section +104 (part B's `Show reflog`
    and `Show backups`, part F's `Show all hidden branches` and `Stop soloing`, in the section's one
    group, no separator), the Remotes section +52 (part F's two). The Local branches section's menu
    has no picture in phase 3, but two earlier expectations list it and change with it: phase 3's
    check 3 step 4 (`Phase3Expectations`' `PartA.LocalBranchesHeaderMenu`, `["Local branches",
    "Create branch…"]`, asserted by `Phase3Checks.A.Branches.cs`) and `RepositoryMenusTests`'
    `["Local branches", "Create branch…"]` (line 479); both end as B8 step 1's list. Design 14.1
    names `03d` under `ForSection`, but `03d` pictures the Stashes section's menu, which phase 5
    does not change: `03d` changes only in the toolbar and the status bar.

  Tests: `RepositoryMenusTests`, `Phase3Expectations.*` and `Phase4Expectations.*` (lists and
  heights; the real-window pass links them).
- **Two selected commits show their comparison** (14.1's fifth row; D162; part F). Every earlier
  picture taken while exactly two commits are selected shows, in the right panel, the comparison of
  design 6.4 (From the lower row, To the upper, their changed files) in place of `2 commits
  selected`: among them phase 3's checks 11 (`11d-two-selected` and its later steps with two
  selected), 11b, 12 (`12e-two-reverted`) and 24 (`24g-multi-selection-clears`), and phase 4's A1
  steps 8 and 9 (`a1g-squash-enabled`, `a1h-squash-not-a-chain`) and B8 (`b8a-commits-menu`); WP8
  settles the list against the pictures. Tests: `GraphSelectionTests` and the checks that assert
  `MultiSelectionView` with two commits; three or more selected commits still show phase 3's list
  (F12 step 6).
- **C12's refusal message** (14.1's sixth row; design 9.3; part P). The toolbar, status bar,
  details panel and dashed-stash changes of the same pictures are the rows above.
  - **Phase 3, check 14** (`blocked`). Step 2 (`14b-untracked-in-the-way`): `ErrorSummary` "2
    conflicts prevent checkout. Commit, stash, or discard your changes first." (T1 names `notes.txt`
    under the local-changes header and `newfile.txt` under the untracked one: two distinct paths;
    `newfile.txt` is not ignored), in place of "Untracked files are in the way: feature/addfile has
    files with the same names. Move or delete them, then check out again."; title "Checkout
    blocked", T1, the command and "Copy output" and "Close" without an extra button, as before.
    Step 4 (`14c-checkout-blocked`, and the real window's `14c-checkout-blocked`): `ErrorSummary` "1
    conflict prevents checkout. Commit, stash, or discard your changes first." in place of "Your
    changes to these files would be overwritten by checking out feature/other."; "Stash changes and
    check out" kept. Step 7 (`14f-clash-blocked`): the same sentence in place of "… checking out
    feature/clash."; the button kept. Steps 5, 6 and 8 (`14d`, `14e`, `14g`, `14h`): no C12 change
    (D91's flow and its "Changes re-applied with conflicts" dialog stay).
  - **Phase 3, check 14b** step 2 (`14b1-checkout-failed`): `notes.txt` is conflicted with no
    operation in progress, so the app refuses before git (9.3: `you need to resolve your current
    index first` → refused before git): no `git switch --no-guess main` call; the error dialog:
    title "Checkout blocked", `ErrorSummary` "Resolve the conflicts first", no `ErrorOutput`,
    `ErrorCommand` or "Copy output": "Close" only; the status bar "Check out main failed" in the
    danger colour; nothing changed. The check's name ("Checkout failed") and its note on T4 no longer
    fit; T4 is shown by no dialog now.
  - **Phase 3, check 15b** step 2 (`15b2-pop-refused`; design 14.1's "check 15 when a stash's apply
    is refused by the app first"): the app's check refuses the pop before git: `README.md` has a
    local change and the stash "On main: Experiment" (`0e95aca`: two parents, no untracked part)
    changes `README.md`. No `git stash pop stash@{0}` call; the error dialog: title "Applying the
    stash failed", `ErrorSummary` "1 conflict prevents applying the stash. Commit, stash, or discard
    your changes first.", no `ErrorOutput`, `ErrorCommand` or "Copy output": "Close" only; the status
    bar "Pop stash@{0} failed" in the danger colour; nothing changed. Step 5 (no local change: git's
    pop conflicts) is unchanged. Check 15's own applies and pops find nothing in the way and are
    unchanged by C12 (its backups change by D171, below).
  - **Unchanged by C12**: phase 4's A5 (a rebase's refusals keep phase 4's texts, 9.3's rebase row;
    its untracked-file case, `Untracked files are in the way: move or remove them first.`, is the
    rebase's own text, not one of 9.1's checkouts) and A5 step 3 (the app's own refusal before
    "Check out … and rebase it"); phase 3's checks with a merge, pick or revert that git does not
    refuse for local changes.
  - **Tests that pin today's state**: those 14.1 names (`OperationTextsTests` 287 to 289,
    `RepositoryOperationsCheckoutTests` 63, 119, 332, 343, 376 and 377, `RepositoryOperationsTests`
    107, 133 and 151 (the summaries 9.3 replaces), `DialogTests` 345 and 862,
    `FailureClassifierTests`, `RepositoryOperationsBranchesTests` 318,
    `RepositoryOperationsStashTagsTests` 255, `Phase3Expectations.C.cs` 286 to 311 (14.1's range: the
    git texts T1, T2 and T4, which stay, though no dialog shows T4 now) and 316 and 318
    (`UntrackedSummary`, `BlockedSummary`: the summaries 9.3 replaces), `Phase3Checks.C.Checkout.cs`
    256, `Phase3Checks.C.Stash.cs` 196), and those 14.1 does not name: `RepositoryOperationsMergeTests`
    199 ("Check out and merge"'s refused checkout: 9.3's sentence, no button), and the ones the next
    section names.
- **Backup names from the fixed backup clock in both harnesses** (14.1's seventh row; design 2.3;
  parts A and U): no earlier picture shows a backup's name. The checks and App tests of phases 3 and
  4 that read backup times or age backups by the wall clock change with WP0's clock:
  `Phase4Checks.C.SkipAbort.cs` compares the backups' order, which the fixed clock keeps (each name a
  second after the last); `RepositoryOperationsBackupCleanupTests` dates its old backups against the
  clock it is given. Earlier checks assert a backup's kind, its target and that its time parses,
  which stays true.
- **Phase 4's leftovers** (14.1's eighth row; D160, merged in `279dd8c`): `a3d`, `b8c` and `c11j`
  show the details panel's 40-character SHA on one line; the resolver's pictures with one
  conflicted file show its file arrows dimmer. Changed with their tests in `279dd8c`.

### Beyond design 14.1 (found by this report's writers)

- **Phase 3, check 9** step 7 (`09e-checkout-refused`) and **check 27** step 4 (`27d-error-light`),
  by C12 (part P): a merge is in progress, so the app refuses the checkout of `feature/alpha` before
  git (9.3: `cannot switch branch while …` → refused before git): no `git switch --no-guess
  feature/alpha` call; the error dialog: title "Checkout blocked", `ErrorSummary` "Finish or abort
  the merge first", no `ErrorOutput`, `ErrorCommand` or "Copy output": "Close" only; the status bar
  "Check out feature/alpha failed" in the danger colour; the merge still in progress.
  `Phase3Expectations.B.cs` (743 to 747: `CheckoutFailedTitle` "Checkout failed", now "Checkout
  blocked"; `CheckoutFailedSummary`, now "Finish or abort the merge first"; `CheckoutCommand` and
  `CheckoutWhileMergingLines`, which no dialog shows now) and `RepositoryOperationsCheckoutTests`
  (141 to 143: the title and summary, git's output and the command) pin today's state.
- **Phase 3, check 15** step 8 (`15h-drop-question`, `15i-dropped`), by D171 (design 7.8: "a pop
  saves one now"), not by C12 (part P). Step 6's toolbar Pop of "Save my work" saves a backup first,
  so after step 8's Cancel `git for-each-ref refs/visualcommit/backup` prints one ref,
  `refs/visualcommit/backup/stash-20260102T090001000Z` → the "Save my work" stash (the SHA step 6's
  `Dropped stash@{0} (…)` names), in place of none; and after the Drop it prints two, in this order:
  that one and `refs/visualcommit/backup/stash-20260102T090002000Z` → `0e95aca` ("On main:
  Experiment", parents "Main work" and "index on main: 88d6b91 Main work", as before), in place of
  one. The names are the fixed backup clock's first two of the session (design 2.3). Nothing
  pictured changes. `Phase3Checks.C.Stash.cs` pins today's state: line 132 (`Assert.Empty` of the
  backup refs after Cancel) and line 143 (`Assert.Single` after Drop). No App test of a pop asserts
  the backup refs: `RepositoryOperationsStashTagsTests`' drop test (351 to 378) pops nothing, and
  its test at 321 refuses the pop before anything runs (343's `Assert.Empty` stays).
- **The restore bar's Restore** (design 7.9, D172; part U). Phase 2's checks 17 (step 5), 18 (step
  5) and 19 (step 4) click Restore right after the discard, which is then the entry before the
  cursor, so the Restore now runs as that entry's Undo: one queue operation `Undo discard of
  Calculator.cs`, `Undo discard of 7 files`, `Undo discard of lines in Calculator.cs`. The files end
  the same, but the status bar then reads `Undid discard of Calculator.cs` (`17c` and its
  real-window twin), `Undid discard of 7 files` (`18c`) and `Undid discard of lines in
  Calculator.cs` (under the dialog of `19c`), and Redo is enabled (`Redo discard of …`) until the
  next discard (17's step 6, 19's step 6) records a new entry. WP8 changes those checks' status
  assertions, if they have any, and the gate's inspection takes these differences as listed here.
- **Phase 2's D75 test**
  (`WorkingChangesTests.Restore_asks_when_the_file_changed_since_the_discard_and_keeps_the_newer_version_in_a_snapshot`,
  App tests; part U): it discards `src/Calculator.cs`, rewrites the file and calls `RestoreAsync`
  while the discard is still the entry before the cursor, and expects D75's `Restore discarded
  changes?` and the newer version in a second snapshot. Under decision UD18, which U16 step 15 pins,
  it stays as it is. Under 7.9 as written that Restore would be the discard's Undo and be refused
  (`1 file has changed since: …`), and this test would change, with U16 step 15's expected result.
  The lead's answer to UD18 decides which.
- **D62's list of when the bar goes** is amended (7.9, D172; part U): Undo of its discard closes it,
  and Redo of that discard shows it again. No check of phases 2 to 4 clicks Undo, so nothing else
  changes.
- **The history's reads** (7.7; part U): every recorded operation runs `rev-parse HEAD
  --symbolic-full-name HEAD`, a `for-each-ref` of its scope and, where its scope needs it, the
  status, the stash list or the configuration, before and after its own commands. Earlier checks'
  "the call log has X" assertions and their counts of HEAD's reflog entries are unchanged (the reads
  write nothing; phase 4's A5 counts the reflog); an assertion that compares an operation's whole
  call list, if WP8 finds one, gains these reads.
- **No change where nothing is searched, hidden or soloed** (part F): with no search the graph draws
  exactly as before (3.3), and with no filter the graph's command is byte-identical to D47's (8.2),
  so Q1's measurement and the real-window pass's `loaded N commits` waits stand. No earlier picture
  shows a page, the reflog or a backup's name.

- **Found while building phase 5 (second wave), not in design 14.1**: `RepositoryOperationsMergeTests`
  (about line 150): "Check out and merge" during a merge shows "Checkout blocked", "Finish or abort
  the merge first" (C12, before git) instead of "Merge failed"; `RepositoryOperationsSequenceTests`
  (line 540): a list of picks refused at its start says "Your staged changes prevent the
  cherry-pick. …"; `RepositoryOperationsStashTagsTests`' second pop test
  (`A_pop_whose_untracked_files_exist_is_refused_before_git_and_changes_nothing`): git's partial
  apply is refused before git (D179); `ShellDriverCheckSupportTests` and `Phase3SupportTests`
  replay check 9 step 7 as C12 refuses it (WP4, `43e8586`). Phase 3's check 23 states Undo and
  Redo at every step (WP5). Two selected commits show their comparison in phase 3's checks 11 and
  24 and phase 4's A1 and B8 (WP7, D162), and every branch menu gains part B's and part F's items
  (heights: a local branch 571, the checked-out branch 441, check 13's hotfix 328, `loc` 380, the
  drops 180, a conflicted file 345; WP6 and WP7, `e11ea7d`).
- **Dashed stashes beyond 14.1's third row** (design 10, D161; WP2b): the dashed stash lines also
  change phase 1's `18a` and `18b` (a stash in lane 1, with a dashed curve into its base) and, in
  phase 3, every picture built on the ops scenario with its stash (the `StoppedAsync` checks 09,
  11b, 12, 14 and 25) and the pictures where the app makes a stash (`15c` to `15h`, `23e` to
  `23g`); phase 2's check 23's dashed line now runs dashed from the working ring to `main` in one
  pattern.

- **The headless harness's default wait** (`ShellDriver.DefaultWaitTimeout`, which phase 3's
  report names as 10 s) is 30 s from `5bdd1de`: in the full run's seven projects side
  by side, steps took 5 to 8 times as long as alone, and four waits for an operation's result ran
  out at 10 s (phase 2's check 24, phase 3's checks 06 and 07, the App window test of a merge);
  each passed alone in 5 to 15 s. Phase 5 records every action for undo, so each operation makes
  more git calls (a stash 17). No expected result changes; a wait ends as soon as its condition
  holds.

- **A2 step 4, the reads of a tab opened through a link** (found by CI's macOS job, run
  38847538648): on macOS the scenario's folder is reached through a link (`/var` to `/private/var`),
  so the opening's first `rev-parse` runs in the folder as opened, which git's top level does not
  hold as text; the activity log left that read out, and the first top-level row was the second
  `rev-parse --git-common-dir`. The app now counts a read made in the folder a tab opened the
  repository from as its worktree's (design 2.3, `15a252f`), and A2's model of the expected top
  level counts the same folders. No expected result changes; where no link is involved the two
  folders are the same.

### Made before this report

- **Phase 3, check 15b, a pop refused by git exits 128 on Git 2.56** (`281dd69`). CI's Windows
  runner moved to Git 2.56.0.windows.2 on 2026-10-15, where `git stash pop stash@{0}` refused by
  local changes exits 128, not 1 (run
  [38723225901](https://github.com/RahimPasha/VisualCommit/actions/runs/38723225901) failed on it).
  Check 15b now accepts both exit codes. Phase 3's report still says "exit 1" in 15b's step 2 and
  in T5; read both as "exit 1 up to Git 2.55, 128 from Git 2.56". Phase 5's C12 then changes step 2
  again (above): the app refuses that pop before git, and no `git stash pop` runs there.
- **Phase 3 part B's expected lines dash a stash's line down to its base** (D161, with WP2a's merge
  `7316ad0`): `Phase3Checks.B.Support.cs`'s `ExpectedLines` marks a line leaving a stash as dashed
  down to the commit the stash was made on, so the layout's `IsDashed` facts that WP2a added hold in
  phase 3's checks. The pictures change only when WP2b draws the dashes (the "Every stash's line
  dashed" row above).

### Changed in this report after its first commit

- **U19 part A, "After step 1"** (found by WP3, pinned in `UndoStepTests`): the menu's "Merge
  feature/ff into main" runs phase 3's merge, `git merge --no-edit --no-log -m "Merge branch
  'feature/ff'" refs/heads/feature/ff` (2.8: git's merge as configured), which fast-forwards here
  with the same status ("Fast-forwarded main to feature/ff") and the same reflog line (`merge
  refs/heads/feature/ff: Fast-forward`). The report said `git merge --ff-only
  refs/heads/feature/ff`, which is the drop menu's "Fast-forward main to feature/ff". U19's row
  now names the merge's command; U23, which merges "as in step 1", follows it.
- **U19 part A, "After step 1", HEAD's reflog** (found by WP8's `Check_U19_merge_fast_forward`):
  the newest entry is `merge refs/heads/feature/ff: Fast-forward (no commit created; -m option
  ignored)`, not `merge refs/heads/feature/ff: Fast-forward`. The merge the entry above names
  passes `-m`, and git notes in the reflog that a fast-forward made no commit for it; the shorter
  line was `git merge --ff-only`'s, and the entry above carried it over without deriving it again.
  Replayed on 2026-10-16 with plain Git 2.36.0.windows.1 and 2.55.0.windows.5 (`git merge
  --no-edit --no-log -m "Merge branch 'ff'" refs/heads/ff` fast-forwarding a fresh repository):
  both print the longer line. U19's row now gives it; the check asserts it on every git, as part
  U's conventions assert reflogs.
- **U20 part B, "After step 5"** (found by the review of WP8's scripted checks): the report
  expected the undo's `git update-index -z --index-info` with `README.md`'s removal line and its
  entry `100644 114493d… 0` as its input. The call log keeps the input of `update-ref --stdin`
  alone (design 2.3; `GitCommand.RecordsStandardInput`), so neither harness can read those lines.
  The row now asserts what they wrote instead: the command, and the index read back with `git
  ls-files --stage` as before the apply, `README.md` at `100644 114493d… 0`. The lines themselves
  are the merged effect's (`WriteIndexEntriesAsync`, 0.5); `UndoEffectTests` reads the index back
  after the same effect.
- **A1, steps 4 and 7, the selection after the commit** (found by WP8, part A): the commit leaves
  no changes, and phase 2's rule (its report, "the working-changes row stays selected while there
  are changes, and when there are none the new commit is selected instead"; its check 24) selects
  the new commit and shows its details. The report said nothing was selected and the right panel
  showed `Select a commit to see its details.`; A1 now expects row 0, `Describe the lines`,
  selected with its details, after step 4 and again after step 7. `a1e-committed` and
  `a1f-undo-tooltip` show it.
- **A2, steps 3, 5 and 7, a row closed after the call opened under it** (found by WP8, part A):
  the convention "The order of rows" relies on every row a step clicks being in the list's view
  without a scroll, which the list keeping to its end (AD15) does not give once a call's box is
  open: the clean-up's three input lines are wider than the box (about 374 px) and wrap (three
  lines each), and the checkout and the merge carry 7.7's reads among their calls, so their open box
  scrolls the operation's row out of view and a click on it would miss. Steps 3 and 5 now close
  the call first (its box closes; the list is then short enough to show the operation's row);
  step 7, whose merge has too many calls for that, closes the call by its click and then uses 2.2's
  keys: Left moves the focus to the merge's row, which the list brings into view, and Left again
  closes it (the real-window column names the two Left presses). The expected results of steps
  2, 3 and 10 now say what the list's end shows in `a2b`, `a2c` and `a2j` (the opened
  operation's row above the view); the abort makes twelve calls, not `e33f846`'s eleven (a `git
  status` after `git merge --abort`). The keys of step 7 found an app defect, fixed: a list
  scrolled up that closed rows until its end showed did not keep to its end again, so the abort's
  row opened at the list's top with its last calls out of view (`ActivityLogView.OnScrollChanged`,
  pinned by
  `ActivityLogViewTests.A_list_scrolled_up_then_shortened_to_its_end_keeps_to_its_end_again`).
  The convention "The order of rows" and AD7 now say that step 7's Left scrolls the list and
  that opened rows do not all fit. As the checkout's row and its summary are out of view when
  step 3 ends, "After step 3" now says that they are asserted as drawn (the glyph, the message's
  and the summary's `#E5606B`, the summary's indent) once the row has opened and before its call
  opens, while both are in view (found by the review of WP8, part A). Writing A2 also found a
  second app defect, fixed: a click on the list's last call, whose box opens at the list's end,
  left nothing with the keyboard focus, because the list's scroll to its end gave the call's
  container to another row and the focus went with it. A row clicked or reached by a key now
  keeps its container, and the list no longer takes a row the keys scroll to back to its end on
  the way (`ActivityLogView.FocusPanel` and `FocusRow`, pinned by
  `ActivityLogViewTests.A_call_opened_by_a_click_at_the_lists_end_keeps_the_focus`).
- **BD4 and B9, the backup row's time 136 and the words at least 112** (found by WP8, part A,
  A2 step 6): the time column's 128 holds `2025-12-20 09:00:51` (125.4 px in Inter at 12.5) but not
  `2025-12-20 09:00:44` (128.1 px), which was cut with "…" and no tooltip; the widest digit, 4,
  makes any time at most 133.6 px. The app's column is 136 now (`ReflogView.axaml`), the words
  keep at least 112 at 440, and `ShellDriverPhase5PagesTests` pins the new bounds. B9's "After
  step 1" now gives them, the time x 268 to 404 and the words x 412 to 524 (it said 268 to 396
  and 404 to 524); what B9 says of the words still holds at 112: row 9's
  `Dropped or popped stash "On main: Try a README"` is cut, and its steps' points on the rows
  (x 420 and 464) lie in the words column. A2's step 6 also found that `LayoutAudit` reported a
  list's last row, which the list shows in part at its edge, as cut when a page around the list
  ends there too; it now lets through what only the list's view cuts (pinned by
  `LayoutAuditTests.A_row_cut_at_a_lists_edge_is_let_through_also_where_a_page_around_the_list_ends_there`).
- **F10 and F11, Notes: the search box's caret in the scripted pictures** (found by WP8, part F):
  the Notes said only of the real window's two captures that the box's blinking caret may be
  caught on or off; the scripted pictures taken with the focus in the box vary the same way
  (runs of `Check_F10_*` and `Check_F11_*` gave two versions each of `f10b` to `f10f`, `f10h`
  and `f11b` to `f11d`; each pair of `f10b` to `f10f` and `f10h` compared whole differs only in
  the caret's 16 pixels). Both Notes now say so, as the conventions ask of a picture that varies
  by nature. No expected result changed.
- **B5, step 7, the row of new line 10** (found by the review of WP8, part B): the step named the
  row that `DiffRowOfLine(10)` finds, and that helper, a copy of phase 3's, matched a removed
  line by its old number, so it found the removed old line 10 (`-        _text = text;`), which
  comes first in the change, not the added new line 10 the step means. The menu is the same on
  both rows, so the check passed on the wrong one (`b5f-diff-menu` showed the menu at the removed
  row). The driver's `DiffRowOfLine` now finds a row by its new number only, the step names the
  row whose new number is 10, and the check asserts that row is the added
  `        _text = text ?? string.Empty;` before it right-clicks. No expected result changes.
- **P27, "After step 8", HEAD's reflog** (found by CI run 38853641791, which failed
  `Check_P27_merge_pick_and_revert_refused` on Git 2.43, 2.55 and 2.56 alike): when a later git
  than 2.36 refuses a merge for something staged (step 6: exit 2, `Merge with strategy ort
  failed.`), it writes a no-op entry into HEAD's reflog, `merge refs/heads/other: updating HEAD`,
  whose old and new value are both Main work (41f8398); Git 2.36 writes none. A merge refused for
  an unstaged change (step 2) writes none on any git, so "After step 2" and "After step 4" stay as
  they are. Replayed on 2026-10-16 with plain Git 2.36.0.windows.1 and MinGit 2.56.0.windows.2 on a
  fresh repository: only 2.56 wrote the entry, and only for the staged refusal. The report's
  derivations replayed git's texts with 2.55, not HEAD's reflog after this refusal. "After step 8"
  now says "unchanged on Git 2.36" and names the entry later gits add; the check asserts the entry
  on gits other than 2.36 (`AssertReflogAfterStagedMergeRefusalsAsync`), its old and new value
  read from `logs/HEAD`, and still asserts HEAD, the refs, the files and the status unchanged.
  `Phase5ScenarioTests.Protect_merge_and_cherry_pick_of_others_work_are_refused_over_a_changed_or_a_staged_file`
  pins both refusals' reflogs with plain git.
- **A2, "After step 3" and "After step 10", what the list's end shows** (step 25's review of the
  undo history's cost, design 15.9): the checkout now reads HEAD once before its work, the
  recording's read, which its own checks share (7.7), and learns that nothing is in progress from
  the git folder and `git ls-files --unmerged -z` instead of a full status; the abort learns that
  its merge ended from the git folder, without the `git status` after `git merge --abort`. The
  checkout lists six calls (`rev-parse HEAD --symbolic-full-name HEAD` and the `for-each-ref` of
  its scope before and after the switch, `ls-files --unmerged -z`, the switch), not eight, and the
  abort eleven, not twelve. With the switch's box open the checkout's row is still out of view,
  but its summary is now the first row whole in view (`a2c`); with the abort's row open, the row
  is now the first row in view, whole, its note under it (`a2j`). Both rows say so now, and the
  check asserts the summary and the abort's row as drawn there. The calls themselves are "among
  them", as the convention "An operation's calls" has them, so no other expected result changes;
  the pictures `a2c` and `a2j` change, `a2k` with them (the list one call shorter), and `a2g`,
  whose merge no longer reads the whole status after it stopped (its last call, below the list's
  end before, is gone).
- **P27, "After step 8", HEAD's reflog, the version that adds the entry** (found by the review
  before the gate): the check and the pin expected the no-op entry on every git but 2.36, so they
  would fail on 2.32 to 2.35 and 2.37, which the tests support (CLAUDE.md: 2.32 or newer; Ubuntu
  22.04 ships 2.34.1). The review replayed the staged refusal with MinGit 2.32.0, 2.34.1, 2.36.0,
  2.37.3, 2.38.0, 2.39.0 and 2.55.0 (exit 128 and no entry up to 2.37.3; exit 2, `Merge with
  strategy ort failed.` and the entry from 2.38.0, whose `git merge` restores the state after a
  refused strategy). "After step 8" now says "unchanged before Git 2.38"; the check
  (`AssertReflogAfterStagedMergeRefusalsAsync`) and the pin gate on 2.38.
- **B8, "After step 2", the reflog's walks** (found by the review before the gate): the page reads
  one entry past its 500 (`-n 501`), so that it can tell whether the reflog has more than it shows,
  which the footer then says (`Only the newest 500 entries are shown`, before BD5's skipped-entries
  text; design 15.9). B8's walks are `-n 501` now; its reflog has 36 entries, so its pictures and
  its footer are unchanged.
- **The search bar's progress text** (row "Progress" of part F's texts; D113, found by the review
  before the gate): `Searched {k} of 1 commit` for a graph of one commit. No check searches so
  small a graph.
- **B7, "After step 3" and "After step 7", the blame's caption** (found by the gate's inspection of
  `b7b`, `b7c`, `b7e` and `b7f` on `4281f4c`): at 1100×700 the page's header is 440 wide, and
  `at c0bc2dd Read whole lines` does not fit beside `Blame of LICENSE` (or `Blame of README.md`),
  `File history` and the close button: the caption is drawn cut with "…", its whole text in its
  tooltip. Neither design 4.2 nor B7 said it could be cut. The page headers' titles and the
  blame's caption are `TrimmedTextBlock`s, as the history's subjects and the reflog's action and
  commit are (design 4.1, 5.2: the app's rule for text in a narrow area), and design 4.2's `at
  {short} {subject}` keeps the subject, so the report says so now rather than the app dropping it:
  part B's new convention "Text that may be cut", and B7's two rows. No picture changes.
- **A4, F10 and F11, the search bar's previous and next while disabled** (found by the gate's
  inspection of `f10a-bar-open`, `f11a-mode-menu` and `f11d-no-matches` on `4281f4c`): they were
  drawn as when enabled (#848B97): the `VcIconButton` theme had no disabled look, while every other
  button theme of the app draws a disabled control in `VcTextDisabledBrush`. The theme has it now
  (design 15.9), and A4's, F10's and F11's rows say "disabled, in `VcTextDisabledBrush`" where they
  said "disabled". The rule holds for every icon button, so the pictures where one shows disabled
  change, those of earlier phases too; a run of every walk-through that listed each such button at
  each capture found these (the resolver's arrows, disabled in that colour already by D160, aside):
  phase 5's `a4a-search-by-key`, `f10a-bar-open`, `f11a-mode-menu` and `f11d-no-matches` (previous
  and next, x 621 to 656, y 102 to 107) and phase 4's `c14h-leave-question` (the resolver's close
  button behind the question, while its command asks, x 1078 to 1085, y 138 to 145); in the real
  window, phase 5's `a4a-search-by-key`. Nothing else in those pictures changes.

## How the expected git states were derived

Every git state in the checks was derived on 2026-10-15 on the Windows machine with plain Git
2.36.0.windows.1, in Git Bash, on copies of the scenario repositories made by the merged scenario
code itself (`Scenarios.*Async` of `tests/VisualCommit.Testing`, through `TempRepo`), whose every SHA
equals the value `Phase5ScenarioTests` (and, for the earlier scenarios, `ScenarioTests`,
`Phase3ScenarioTests` and `Phase4ScenarioTests`) pins; part P also rebuilt "protect" as a replica
whose SHAs equal the pinned ones. Each check part ran on a fresh copy. Each operation the app makes
ran with the command lines the merged git layer builds (WP1, WP2a, WP3's git half; WP0's contracts)
or the design gives where the code is not built yet, and with the app's environment
(`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME` the copy's own, `LANGUAGE=en`,
`LC_ALL=en_US.UTF-8`, `GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:`,
`GIT_LITERAL_PATHSPECS=1` except where the app removes it, `GIT_AUTHOR_DATE` and
`GIT_COMMITTER_DATE` 2026-01-02T09:00:00+00:00, the repository's own identity "Test Author", and
`VisualCommit <snapshot@visualcommit.invalid>` for a snapshot); each step "outside the app" ran as
`TempRepo` makes it, on the copy's clock. The graphs were measured in the running app (headless,
`ShellDriver`) or laid out from the app's own log command by `GraphLayout`'s and `CommitGraphData`'s
rules (D48, D54, D55, D60, D161), as each part says.

The two gits: every git text the checks quote, and every state of part P's and part U's calls, was
replayed with Git 2.55.0.windows.5 (Visual Studio's, its `mingw64/bin` first on `PATH`). The only
differences that reach a check are the merge refusals (exit 2 and an extra line, part P's TP7 and
TP9) and the Files search's raw output (part F's F11); the conventions say how the checks assert
git's texts on any version. CI's jobs run Git 2.56.0 (Windows, macOS), 2.55.0 (`ubuntu-latest`) and
2.43.0 (the two containers); nothing here was derived with those.

The derivations' copies, scripts, dumps and logs lived in the session's scratch folder and are not
kept in the repository; the names below are theirs. Throw-away tests and worktrees used to build
the scenarios through their own code or to measure the app were never committed and were removed
afterwards.

### Scenario facts

Derived from `phase/5-history` at `ceb5f81`, in a throw-away worktree (removed), with Git
2.36.0.windows.1. A temporary test in `tests/VisualCommit.VisualTests.Phase5` built each scenario
through the real scenario code (`Scenarios.HistoryAsync`, `FiltersAsync`, `ReflogAsync`,
`ReflogManyBackupsAsync`, `ProtectAsync`, phase 4's `ConflictsAsync` with
`AddOldAndManyBackupsAsync`, phase 1's `GraphAsync`) and read it two ways:

- **through the app's own git layer** (merged WP1 and WP2a): `LoadCommitsAsync` (with and without a
  `GraphFilter`) fed page by page to `GraphLayout` and `CommitGraphData` (labels), as the view model
  does; `ReadCommitDetailsAsync`, `ReadTreeFilesAsync`, `ReadFileHistoryAsync`, `ReadBlameAsync`,
  `SearchAsync`, `CompareAsync`, `ReadReflogAsync`, `FindLostCommitsAsync`, `ReadBackupsAsync`,
  `FindBackupsToRemoveAsync`/`CleanUpBackupsAsync` with `BackupClock.Fixed(AppClocks.HarnessTime)`;
- **through the app itself**, headless, started by phase 5's `OpenAsync` (`ShellDriver`) at
  1100×700 and 1920×1080, dark: the graph control's `Columns`, `RowBounds`, `LaneCenterX`,
  `LabelsInRow` (the labels as drawn, "+N" included), `ShowsWorkingRow`, the left panel's texts and
  `CurrentBranch`, all translated into window coordinates.

Git states that a check produces (the automatic stash, refusals, restores) were made with plain git
on fresh copies, with the app's environment (`GIT_LITERAL_PATHSPECS` removed for `stash push`, as
D108 says) and the app's command lines. The refusal texts were also replayed with Git
2.55.0.windows.5 (the differences are in scenario facts 5.6). The raw dumps (`app-*.txt`,
`git-*.txt`), the dump test (`ZzScenarioDump.cs`) and the text replay (`texts.sh`, with its outputs
for both gits) lived in the scratch folder.

**Against the pins**: every fact `Phase5ScenarioTests` pins (graphs, lanes and "ends", refs,
upstreams, status, files, follow lists with `R084`, blame lines, search matches, merge base,
name-status lists with `R067`, counts `7⇥2`, stash bases, filter sets, HEAD's reflog with the gap at
`HEAD@{5}` and its times, the branch reflogs, the lost set, the 22 backup refs and their targets,
the clean-up's removals, the protect refusals) came out the same. No difference.

### Part A

- **When, what, which git.** On 2026-10-15, from `phase/5-history` at `e33f846` (WP-S's scenarios
  merged at `ceb5f81`, WP1, WP2a and WP3's git half after it), on the Windows machine, with Git
  2.36.0.windows.1, and again with Git 2.55.0.windows.5 (Visual Studio's, its `mingw64/bin` first on
  `PATH`). After an independent review, on the same day: the cancel's trial again on `6f77e57`
  (below), and the merge stop of A2's step 7 replayed with Git 2.36.0 on a copy of the "conflicts"
  scenario with its backups (exit 1, git's three lines on standard output, nothing on standard
  error, `UU notes.txt`, ` M other.txt`).
- **The scenario repositories.** A throw-away worktree of `e33f846` in the session's scratch folder,
  removed afterwards (its file-system monitor stopped first), held a temporary test in
  `tests/VisualCommit.VisualTests.Phase5` (never committed) that built "history" with
  `Scenarios.HistoryAsync()`, "conflicts" with `Scenarios.ConflictsAsync()` and
  `Scenarios.AddOldAndManyBackupsAsync()`, and the merge stop with
  `Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`, and copied each `TempRepo` folder (working
  tree, bare remote, home) whole. Their commits are the pinned ones (`c0bc2dd`, `da31c97`, …; the
  scenario facts checked every pin and found no difference).
- **Plain git on copies**, in Git Bash, with the tests' isolation (`GIT_CONFIG_NOSYSTEM=1`, an empty
  `GIT_CONFIG_GLOBAL`, `HOME` the copy's `home`, `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`, `GIT_EDITOR=:`,
  `GIT_SEQUENCE_EDITOR=:`, `GIT_TERMINAL_PROMPT=0`, `GIT_LITERAL_PATHSPECS=1`, no author or committer
  variables, so the repository's own identity Test Author applies) and the app's dates
  (`GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` 2026-01-02T09:00:00+00:00), each part on a fresh copy:
  - A1 and A4: `git add --all -- src/Reader.cs`, then `git commit --file=- --cleanup=whitespace` with
    `Describe the lines` on standard input (the app's command lines, from the call log): commit
    `d1e23a37b6b08f4dc9662fab930f232a66ecd8fc`, parent `c0bc2dd`, `## main...origin/main [ahead 1]`;
    with `scratch.txt` written first, the same commit and `?? scratch.txt` after it. The undo as WP3's
    git half writes it: `git update-ref -m 'VisualCommit: undo commit "Describe the lines"' --stdin`
    with `update refs/heads/main <c0bc2dd…> <d1e23a3…>` (exit 0; `M  src/Reader.cs`; HEAD's and
    `main`'s reflogs gain the message), the redo with the values swapped (exit 0; the tree clean but
    `scratch.txt`), and a second redo on top refused with `fatal: cannot lock ref
    'refs/heads/main': is at d1e23a3… but expected c0bc2dd…` (128), which is what makes the step's
    check strict. Git 2.55 gave the same commit, status and reflog lines.
  - A2: other.txt written; the clean-up's transaction of three `delete` lines (exit 0, 50 refs
    left, the oldest `reset-20251220T090002000Z`); `git switch --no-guess feature/kinds` refused
    with the four lines quoted above (exit 1; HEAD, status and refs unchanged); `git merge --no-edit
    --no-log -m "Merge branch 'feature/theirs'" refs/heads/feature/theirs` (exit 1, the three lines,
    `UU notes.txt`, ` M other.txt`, the stages, `MERGE_HEAD`, `MERGE_MSG`, the markers naming
    `refs/heads/feature/theirs`); the abort's snapshot as `CommitSnapshotAsync` makes it (a temporary
    index read from `da31c97`, `add --all --force` of notes.txt and other.txt, `write-tree`: tree
    `912dd413f445e484334bad13e5f44a294ebc2dfc`); `git merge --abort` (exit 0, ` M other.txt` kept,
    no `MERGE_HEAD`, HEAD's reflog `reset: moving to HEAD`). The snapshot commit's id depends on the
    app's message and dates, so it is asserted by parent and tree. Git 2.55 printed the same texts
    and left the same states.
  - A4's resolver method: the copy stopped by `ConflictsStoppedAsync`: `UU notes.txt`, `MERGE_HEAD`
    `5c77ffb…`, notes.txt blob `cdd7b39` with `>>>>>>> feature/theirs`, no `merge.tool`.
- **The app itself, headless**, from the same temporary test (the merged code of `e33f846`, at
  1100×700, dark, with the harness's frozen clocks): the toolbar's and status bar's states and
  tooltips on "history", on a tab added with `+` and after the commit (the stage panel's `Stage`
  and `Commit` operations, their calls, the commit `d1e23a3`, H-A1's rows and labels); on "conflicts"
  the clean-up's record (`Clean up backups`, background, Succeeded, `Removed 3 backups`, the three
  notes in order) and calls (`for-each-ref`, then `update-ref --stdin` with the three `delete`
  lines as recorded input); the refused checkout's record (Failed, `Check out feature/kinds
  failed`, its calls, the error dialog's title, command and buttons; its summary today is phase 3's
  `Your changes to these files would be overwritten by checking out feature/kinds.`, which 9.3
  replaces with D153's sentence); the merge's record (Conflicts, `Merge stopped: 1 file has
  conflicts`, no failure summary) and its ten calls (the merge call with git's three lines as its
  output and no error); the merge tool's running state (`IsBackground` false)
  (`Waiting for slow to close…`, Cancel with
  `Stop git and the merge tool`), its cancelled record (`Merge tool for notes.txt cancelled`, no
  dialog, the mergetool call `Cancelled`, git's copies removed, `UU notes.txt` kept); the abort's
  question, record, the note `Removed backup reset-20251220T090002000Z (more than 50 backups)` and
  calls (three on a temporary index, the backup's `update-ref --stdin` with its two `create` lines,
  `merge --abort`, the clean-up's two calls); the backups after it (51 refs). The undo half, the
  activity log and the search bar are not built yet: their texts and layout come from the design.
  Avalonia's text box undo was measured there too: `Next` typed as four text inputs took four
  presses of Ctrl+Z, typed as one took one, and a fifth press in the empty box left it empty; the
  headless platform's `HotkeyConfiguration` gave Undo Ctrl+Z and Redo Ctrl+Y, Ctrl+Shift+Z.
- **The cancel's trial.** The merge tool step ran eight times in the temporary test with the
  waiting tool of "Test methods" (its `tool-started` file awaited before the click), first on
  `e33f846`: six cancels ended the operation within 1 to 4 seconds; in two, the click on the enabled
  Cancel (bounds 309, 675, 54 × 25, the hit test on its own text) logged no `cancelling the running
  operation.`, and the operation had not ended when the trial's own wait gave up 90 s after the
  click (the whole test then took 2 min 23 s and 2 min 45 s). That was D182's fault: Cancel's
  tooltip, opening under the pointer at the window's bottom edge between the move and the press,
  took the press (fixed in `28716c4`, merged at `3dcba5d`). The same test, on a copy of `6f77e57`
  taken with `git archive` (no worktree), ran eight more times: 8 of 8 clicks cancelled, `cancelling
  the running operation.` logged and the queue idle 0.6 to 2.0 s after the click, the operation
  `Cancelled after` 8.0 to 12.1 s from its start; the whole test took 32 to 49 s each time. The tool's
  own limit (600 rounds of `sleep 0.1`) was timed once on its own: 111 s in Git 2.36.0's `sh`.
- **Geometry** of the panel and the search bar is derived from the design (2.1, 3.1) on the
  measured layout of today's app (graph area x 260 to 700, y 88 to 674; status bar y 674 to 700; the
  toggle at x 1005 to 1100), as scenario facts 1 does.

### Part B

- **Git**: 2.36.0.windows.1 (on `PATH`) in Git Bash on the Windows machine, 2026-10-15; and
  2.55.0.windows.5 (Visual Studio's, its `mingw64/bin` first on `PATH`) for the comparison below.
- **Repositories**: copies of the scenario templates made by the merged scenario code itself. A
  throw-away console program (`tmp-B/dump/`, referencing the built `VisualCommit.Testing.dll` of
  `phase/5-history` at `ceb5f81`, never in the repository) called `Scenarios.ReflogAsync()` and
  `Scenarios.HistoryAsync()` and copied each copy's whole folder (working tree, `origin.git`,
  `mirror.git`, `home`) to `tmp-B/scenarios/`; the same program run with 2.55 first on `PATH` built
  them with 2.55 into `tmp-B/scenarios-255/`. Their refs and backups are the pinned ones (`git
  for-each-ref` printed the 22 backup refs of `Phase5ScenarioTests` with their targets; HEAD's reflog
  the pinned 36 entries). Each derivation ran on a fresh copy (`fresh` in `tmp-B/env.sh`, remote URLs
  pointed at the copy).
- **Environment**: as the app runs git (`GitRunner` and `GitIsolation`): `GIT_CONFIG_NOSYSTEM=1`, an
  empty `GIT_CONFIG_GLOBAL`, `HOME` in the scratch folder, `LC_ALL=en_US.UTF-8`, `LANGUAGE=en`,
  `GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:`, `GIT_LITERAL_PATHSPECS=1`, and the
  app's dates `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` 2026-01-02T09:00:00+00:00; the repository's
  own identity ("Test Author"); a snapshot with `VisualCommit <snapshot@visualcommit.invalid>` as
  `SnapshotIdentity` sets it.
- **B5 to B7** (`tmp-B/scripts/b5-b7.sh`, output `tmp-B/b5-b7.log`): the history command of WP1
  (`git -c core.quotePath=true log --follow --root -M --name-status --no-show-signature --no-color
  --encoding=UTF-8 … <start> -- <path>`) from HEAD, `0575c7f`, `365482b`, `14a5c6c` and `1d5a7cf`;
  each row's diff (`git diff --find-renames <parent> <commit> -- <new> <old>`, `diff-tree -p --root`
  for the root); `git -c i18n.logOutputEncoding=UTF-8 blame --porcelain --no-textconv [<sha>] --
  <path>` for the working tree, `c0bc2dd`, `14a5c6c`, `365482b` and LICENSE and README.md at
  `c0bc2dd`, read into runs, authors, dates, paths, `previous` and `boundary`; `git ls-tree -r -z
  --name-only` and `diff-tree --name-status -M --root` for "All files" and the details; `git log
  --date-order … --branches --remotes --tags --ignore-missing HEAD --` for the rows. They agree with
  the scenario facts the report was given and with the pins.
- **B8** (`tmp-B/scripts/b8.sh`, `tmp-B/b8.log`): the clean-up at opening as `git update-ref --stdin`
  deleting the two backups dated 2025-11-20 (15 backups left); design 5.2's two walks of HEAD's
  reflog exactly as WP1 runs them (`--ignore-missing -z -n 500`, the second with `--date=raw`), 36
  entries with `HEAD@{5}` missing; the lost set from `git rev-list --ignore-missing --stdin --not
  --branches --tags --remotes HEAD --` with the entries' SHAs; `main`'s walk; `git for-each-ref
  refs/heads`; then `git branch shortcut d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f` (the app's command
  with "Check out the new branch" unticked), its reflog, the status, the graph's log (RL-B8) and the
  lost set again (`d07c677` gone from it); `cat-file -e d07c677^{commit}` (exit 0).
- **B9** (`tmp-B/scripts/b9.sh`, `tmp-B/b9.log`): on separate fresh copies after the clean-up:
  `update-ref -m "VisualCommit: restore of branch doomed" --create-reflog --stdin` with `create
  refs/heads/doomed 44e646b…` (its reflog, no `branch.doomed.*`, the graph RL-B9a); `git push --progress
  --force-with-lease=refs/heads/feature/gone: origin 7dd8e8f…:refs/heads/feature/gone` (exit 0, `*
  [new branch]`, `ls-remote`, the remote-tracking ref and its `update by push` reflog; a second push
  prints `Everything up-to-date`, exit 0, design 0.5's "already there"); the discard restore: the
  outside edit, the "changed" rule of decision BD15 per path (`hash-object --no-filters` of the file
  against `HEAD:<path>` and `<backup>:<path>`), the newer version's snapshot made as
  `SaveSnapshotAsync` makes it (a temporary index read from HEAD, `add --all --force`, `write-tree`,
  `commit-tree -p HEAD` with the app's message, `update-ref <name> <commit> ""`), then `git restore
  --source=<backup> --worktree -- <paths>` as `RestoreSnapshotAsync` runs it; the abort's and the
  skip's restores in one copy, in the check's order; the mixed reset's staged changes with decision
  BD16's paths and, for comparison, with the design's literal `-- .`; the hard reset's for decision BD16;
  the force push's backup's upstream and remote-tracking reflog (for decision BD8's menu, not a step).
  Each state was read with `git status --porcelain=v1 --untracked-files=all`, `git diff --cached`,
  `git diff`, `git ls-files --stage` and file contents.
- **The two gits.** `b5-b7.sh`, `b8.sh` and `b9.sh` were run again with 2.55 on the 2.55-built copies
  (`tmp-B/*-255.log`): every reflog text, the gap, the lost sets, the backups and their words' sources,
  each restore's refs, files, index and status, the snapshot ids (`27c8989`, `c424282`), the push's
  summary line, the file histories and the blames came out the same. The only differences were in the
  scripts' own formatting (`--decorate` showing backup refs on 2.36 only, the push's `pack-reused 0
  (from 0)`, `--date=iso-strict` writing `Z`) and the wall-clock time of the blame's uncommitted
  lines. CI's Windows job now runs Git 2.56.0 (status.md): not checked here; the checks' rule for
  git's own texts (conventions) covers it.
- **Graphs** are RL0, H0 and the scenario facts' RL-B8 and RL-B9a, whose row order `git log --date-order`
  confirmed here; the label `origin/feature/gone` on Add the app after the push from the
  `--decorate` output.
- **What git did that the design does not spell out**: restoring the design's whole index from a
  backup onto a HEAD that has moved since stages later commits' files backwards (`D docs/notes.txt`
  for the mixed reset; `AD src/shortcut.txt` for the hard reset, whose `^2` holds src/shortcut.txt
  while the working tree lacks it): decision BD16; the skip's `-changes` snapshot carries the message
  `VisualCommit: snapshot before an abort` (phase 4's), so the list's words for it come from its
  kind (`Before skipping a commit`), not from the message; a push that must create a branch that
  exists already prints `Everything up-to-date` and exits 0.

### Part F

- **Git**: 2.36.0.windows.1, in Git Bash on the Windows machine, on 2026-10-15; the searches, the
  comparisons' `rev-parse` calls and the drop's ancestor read also with 2.55.0.windows.5 (Visual
  Studio's, its `mingw64/bin` first on `PATH`).
- **Repositories**: not replicas but the scenarios themselves: a small console program
  (`report5/tmp-F/tool/`, referencing the built `VisualCommit.Testing.dll`) called
  `Scenarios.HistoryAsync`, `Scenarios.FiltersAsync` and `Scenarios.GraphAsync` and copied each
  copy's folder, with its bare `origin.git`, to `report5/tmp-F/scen/` (the remote URLs pointed at
  the copies afterwards). Their SHAs equal the pinned ones (`c0bc2dd`, `4c0aa3c`, `47e6ec7` and every
  other named here); the program also printed each copy's clock (history 2026-01-01 12:14, filters
  12:15, graph 12:13). Every git call ran with the tests' isolation (`report5/tmp-F/env.sh`:
  `GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME`, `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`,
  `GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:`, `GIT_TERMINAL_PROMPT=0`, `GIT_OPTIONAL_LOCKS=0`,
  `GIT_LITERAL_PATHSPECS=1`, removed where the app removes it).
- **Search** (`tmp-F/f10-f11-search.sh`, log `tmp-F/logs/f10-f11-search.log`; on 2.55
  `tmp-F/f11-git255.sh`, `logs/f11-git255.log`): the graph's rows from the app's own log command
  (`git log --date-order --branches --remotes --tags --ignore-missing HEAD --`), then each mode's
  call exactly as `GitRepository.SearchCommand` builds it, with the rows' SHAs on standard input:
  Commits `-F --regexp-ignore-case --grep=<text>` (tokenizer: rows 6 and 10; `Jonas` and `0575`:
  nothing); the author part and the SHA part in memory as 3.2 says (`%aN <%aE>` containing "jonas",
  case ignored: rows 0, 4, 6, 8, 10; SHA prefix `0575`: row 7); Files with `GIT_LITERAL_PATHSPECS`
  removed (`parser`: 7, 10, 12; `reader`: 0, 7; `TraceEverything`: nothing); Changes `-STraceEverything`
  (1, 9). On 2.55 the matches are the same; its Files call prints the header line of all 14 commits
  given (2.36: 3) and path lines only under the matching ones.
- **The refresh** (`tmp-F/f11-outside-commit.sh`, `logs/f11-outside-commit.log`): on another copy,
  src/Config.cs written as `ConfigTraced`, `git add -- src/Config.cs` and `git commit --quiet
  --message "Trace again"` with Test Author and 2026-01-01T12:14:00+00:00 as author and committer
  date (what `TempRepo.GitAsync` gives on the copy): commit `f612569a47849c0326a3022a420c241d56d7ddde`,
  status ` M src/Reader.cs`, `## main...origin/main [ahead 1]`; the Changes call given only its SHA
  prints it; given all 15 rows it prints f612569, 20d7fbe and 14a5c6c. Its rows and layout (H-F1) follow
  from H0 by `GraphLayout`'s rules (D48, D54, D55: one more commit at the top of lane 0, colour 0).
- **Compare** (`tmp-F/f12-f13-compare.sh`, `logs/f12-f13-compare.log`; `tmp-F/f14-compare-hidden.sh`,
  `logs/f14-compare-hidden.log`): `GitRepository.CompareAsync`'s calls by hand: `rev-parse
  <from>^{commit} <to>^{commit} --`, `merge-base --all`, `diff-tree -r -z --name-status -M --no-ext-diff
  --no-color --no-commit-id <old> <new>` and `rev-list --left-right --count <from>...<to> --`; the
  file diffs with `git diff --find-renames <parent> <commit> -- <path>`; the drop's fact `merge-base
  --is-ancestor <B ref> <A ref>` as phase 4's `ReadDropFactsAsync` runs it, with the drop's full ref
  names (`refs/heads/feature/compare refs/heads/main`: exit 1, no output). The `rev-parse` calls with
  FD20's names (F12's two full SHAs both ways round; `refs/heads/main^{commit}
  refs/heads/feature/compare^{commit} --`, the drop's the other way, and the filters scenario's
  `refs/heads/main^{commit} refs/heads/feature/alpha^{commit} --`) and the drop's ancestor read were
  run again on 2026-10-15 with 2.36 and 2.55: each `rev-parse` prints the two SHAs and then `--`,
  exit 0, on both; the ancestor read exits 1 on both. The two-commit list, the swapped one, both
  three-dot lists, the
  "All differences" list, the counts and the filters scenario's hidden-branch comparison are as the
  tables give them.
- **Hide and solo** (`tmp-F/f14-filters.sh`, `logs/f14-filters.log`): the graph's log command with
  `GraphFilter`'s hide and solo revisions (`GraphFilter.HideArguments`, `SoloArguments` as merged)
  lists FL1's and FL2's 11 commits; the stash list and each stash's first parent show that the alpha
  stash's base (Alpha one) is in neither, the beta stash's (Beta six) in both. The rows, lanes,
  labels and lines of FL0, FL1 and FL2 are the scenario facts', measured in the app through WP2a's
  filtered pager and `CommitGraphData`'s label filter; the session's contents follow
  `RepositoryGraphFilter` as WP0 declared it. `git for-each-ref` and `.git/config` were hashed to
  give the checks their "nothing changed in git" baseline.
- **Dashes** (`tmp-F/f15-stashes.sh`, `logs/f15-stashes.log`): each stash's first parent (graph:
  `47e6ec7`; filters: `7c60fc4` and `89f891e`) and where it lies in the graph; which lines carry
  `IsDashed` is the scenario facts' measurement of WP2a's `GraphLayout`; the sampled ranges follow from
  the measured row and lane positions, the ring's and the node's 10-px size (phase 1) and design
  10's 3-px dashes.
- **Menus**: from `RepositoryMenus.cs` as merged (the builders, their groups, phase 3's and 4's
  reasons, `OperationTexts.Menus`), with design 13.1's items in the hooks' places, applied to the
  scenarios' refs (upstreams and their ahead and behind counts from `git for-each-ref
  --format=%(upstream:track)`). The comparison's file menu (6.4) as FD21 builds it, with part B's
  `File history` and `Blame` (its B5 and B7 menus, 171 high) and `FileItems`' texts.
- **What git did that the design does not spell out**: in a three-dot comparison towards `main`
  the rename shows as `R067`, not the rename commit's own `R084`, because the merge base's diff also
  holds Read whole lines' edit; the outside commit's src/Config.cs is the same blob as Add tracing's;
  "Jonas" and "0575" are in no message, so F10's author and SHA matches come from the app's in-memory
  part alone. Nothing contradicted the design.

### Part U

All with Git 2.36.0.windows.1 (`git --version`), on 2026-10-15, in throw-away folders outside the
repository; nothing of the repository was changed (the worktree below was removed afterwards, with
its two throw-away tests, which dumped the undo states and graphs).

1. **The scenarios, built by their own code.** A detached worktree of `phase/5-history` at `ceb5f81`
   (`tmp-U/wt`) got a throw-away test, `tests/VisualCommit.Git.Tests/ZzUndoDump.cs`, that built
   `Scenarios.OperationsAsync`, `RemoteSyncAsync`, `RebaseAsync`, `InteractiveAsync`,
   `EdgeCasesAsync`, `CheckoutBlockedAsync`, `HistoryAsync`, `ConflictsAsync` and `ProtectAsync`
   and copied each one's whole folder (bare remotes and the linked worktree included) to
   `tmp-U/scenarios/` (`dotnet test --project tests/VisualCommit.Git.Tests --filter-method
   "*ZzUndoDump*"`, passed). Every SHA used here matched the pins (`ops` `88d6b91`, `sync`
   `a7f2cfd`, `rebase` `b0dfa3f`, `interactive` `57f089d`, `edges` `419190f`, `blocked` `106efba`,
   `history` `c0bc2dd`).
2. **Each method on a fresh copy, with the app's commands.** `tmp-U/tools/env.sh` copies a scenario
   to `tmp-U/runs/<method>/`, points its remotes at the copy (and repairs `edges`' worktree), and
   runs git as the tests isolate it (`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME`,
   `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`, `GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:`,
   `GIT_TERMINAL_PROMPT=0`): the app's calls with `GIT_LITERAL_PATHSPECS=1` (stash calls without it,
   D108) and `GIT_AUTHOR_DATE` = `GIT_COMMITTER_DATE` = 2026-01-02T09:00:00+00:00 (D72); the steps
   "outside the app" with Test Author's identity and the scenario clock's time. The actions use the
   git layer's command lines as merged (`GitRepository.Writes.cs`, `.Branches.cs`, `.History.cs`,
   `.Rebase.cs` with `RebaseTodo`'s todo, labels and guarded `exec` amend, `.Remotes.cs`,
   `.Stash.cs`, `.Tags.cs`, `.Backups.cs` for the snapshots' form), and each undo and redo the
   effects of `UndoSequence` with WP3's command lines (as merged in `b0e8710`, design 7.12)
   (`update-ref -m "VisualCommit: undo …"`,
   `update-ref --stdin`, `update-index -q --refresh` then `read-tree -m -u` for a Keep move, `switch
   --no-guess --no-overwrite-ignore`, `switch --detach --no-overwrite-ignore`, `restore --staged
   --source=… -- .`, `restore --source=<snapshot> --worktree -- …`, `stash push`/`pop
   --index`/`store -m`/`drop`, `config --local --add`/`--remove-section`, `remote rename`/`set-url`,
   `push --progress --force-with-lease=<ref>:<value> …`). The scripts: `u16.sh` and `u16b.sh` (the
   commit, the amend, the four discards with their snapshots), `u17.sh` (the four resets with their
   index commits and snapshots, the hard reset's redo), `u18.sh` and `u18b.sh` (the branch methods,
   D91's round trip), `u19.sh`, `u19b.sh`, `u19c.sh`, `u19d.sh` (merges, picks, revert, rebases,
   the interactive rebase and the squash through the app's todo, the pull with rebase), `u20.sh`
   (stashes, tags), `u21.sh` and `u21b.sh` (the pushes and their undo and redo against the copy's
   `origin.git`, the lease failure, the force push), `u22.sh` and `u22b.sh` (upstream, remotes, the
   added remote's fetched refs deleted, `.git/config` compared with `cmp`, the ignore), `u23.sh`
   (the terminal commit, the read-tree refusal and the
   app's count from `diff-tree`, `check-ignore --stdin -z` without `GIT_LITERAL_PATHSPECS`, what
   `read-tree` alone does to an ignored file, stale stat data with `diff-files` and a `read-tree -n`
   dry run before and after `update-index --refresh`), `extra.sh` (intermediate states for the
   graphs). Each printed `git status --porcelain=v1 -b --untracked-files=all`, the refs, the stash
   list, the reflogs, the files and `git ls-remote origin` after the action, the undo and the redo.
3. **The graphs.** A second throw-away test in the same worktree,
   `tests/VisualCommit.VisualTests.Phase5Undo/ZzUndoGraphs.cs`, opened each derived repository with
   `GitRepository.OpenAsync` and laid out its graph as the view model does (`LoadCommitsAsync` page by
   page into `GraphLayout` and `CommitGraphData`, with WP2a's dashed stash lines): rows, labels,
   lanes, colours and lines for every graph of "Scenarios and graphs of part U" (`tmp-U/graphs-1.txt`,
   `tmp-U/graphs.txt`). The pristine scenarios came out as their reports give them (G0, R0, G-I, E0,
   G14a, the sync graph), with the stash's line dashed.
4. **Git 2.55.** `tools/v255.sh` replayed with Git 2.55.0.windows.5 (Visual Studio's, `mingw64/bin`
   first on `PATH`) the texts part U quotes and the behaviours it relies on: TU1 and TU2 are the same
   word for word; `read-tree -m -u` overwrites an ignored file without a word there too; the dry run
   refuses stale stat data the same way.
5. **After the review**, in `report5/tmp-fix-U/` with both gits (2.36.0.windows.1 and
   2.55.0.windows.5, the same results): `soft.sh` replayed the soft moves as the merged code writes
   them (UD20): the commit's undo and redo as `update-ref -m … --stdin` transactions (`8d845e8`
   again on redo; the message in `main`'s and HEAD's reflogs), and the mixed reset with staged
   changes undone and redone with `restore --staged` before the transaction (the states of U17
   steps 7 to 9); `stale.sh` showed that a plain `git status` writes the refreshed index (then
   `diff-files` lists nothing and `read-tree -n -m -u d93814d… 88d6b91…` exits 0), while with
   `GIT_OPTIONAL_LOCKS=0` the stat data stays stale (`diff-files` lists `docs/ff.txt`, `read-tree
   -n` exits 128) until the Undo's `update-index -q --refresh`, after which the Undo's `read-tree`
   exits 0; `u16fix.sh` derived U16 steps 14, 15 and 30 to 32 (the second snapshot the same commit
   `0bd36b6` as the first, the newer file `597f7de`, the third snapshot `d76813b`).

Found while deriving, and written into the checks: a force push of a branch whose remote-tracking
ref a fetch moved but the branch never held is stopped by `--force-if-includes` (`remote ref updated
since checkout`), so U21 uses phase 3's check 20 rewrite, where the remote-tracking ref is in the
branch's reflog; with D72's fixed dates a redone stash and a redone hard reset's snapshot are the
same objects as the first (`b80f1ca`, the snapshot commit), and the hard reset's redo writes git's
own `reset: moving to …` reflog entry; an `update-ref` of the branch HEAD is on writes HEAD's
reflog too; an `update-ref --stdin` delete removes the branch's reflog and a later create writes a
new one (`core.logAllRefUpdates`).

### Part P

- **Git**: 2.36.0.windows.1 (`/mingw64/bin/git`), in Git Bash on the Windows machine, on
  2026-10-15; every text and state of the checks' calls replayed with 2.55.0.windows.5 (Visual
  Studio's, its `mingw64/bin` first on `PATH`).
- **Repository**: a replica of "protect" built by `tools-P/common.sh` (`build_protect`) exactly as
  `Scenarios.BuildProtectAsync` and `TempRepo` build it: `git init --quiet --initial-branch main`,
  the repository settings of `TempRepo.RepositorySettings` (`core.autocrlf=false`,
  `commit.gpgsign=false`, `gc.auto=0`, `user.name` Test Author, `user.email` author@example.com),
  the isolated environment (`GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `HOME`, the
  author and committer variables), each commit `git add --all` then `git commit --quiet --allow-empty
  --message <m>` dated by the clock from 2026-01-01T12:00:00+00:00 plus one minute per commit, `git rm
  --cached --quiet settings.local.json`, `git branch other HEAD`, the annotated tag at 12:04, `git
  checkout --quiet` between the branches. Its SHAs equal the pinned ones: 41f8398, 19a9044, 4f5e821,
  f39ea2d, 64166f7, 73e0bab, 035a2f5, tag object a2f81f7 (with both gits). Every case ran on a fresh
  copy (`cp -r`), the copy's clock at 12:07.
- **The app's calls** ran with GitRunner's environment (`LC_ALL` and `LANG` en_US.UTF-8, `LANGUAGE=en`,
  `GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:`, `GIT_LITERAL_PATHSPECS=1`, removed
  for `stash push` as `StashCommand(literalPathspecs: false)` does), the harness's commit dates
  (`GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` 2026-01-02T09:00:00+00:00, `GitIsolation.AppCommitDates`)
  and the argument lists the git layer builds or design 9 and 7.3 give: `switch --no-guess <b>`,
  `switch --create <b> <sha>`, `switch --detach <sha>`, `switch --detach --no-overwrite-ignore <sha>`,
  `stash push --include-untracked --message <m>`, `stash pop [--index] stash@{0}`, `switch --no-guess
  --no-overwrite-ignore main`, `merge --no-edit --no-log -m "Merge branch 'other'" refs/heads/other`
  (the message from `git fmt-merge-msg` given `<sha>\t\tbranch 'other' of .`, as `MergeMessageAsync`
  builds it), `cherry-pick <sha>`, `revert --no-edit <sha>`, `check-ignore --stdin -z`, `diff-tree -r
  -z --no-renames --name-only <s>^1 <s>`, `ls-tree -r -z --name-only <s>^3`, `merge-base --is-ancestor`.
  After every call the scripts printed exit code, both streams with control characters shown (`cat
  -A`), HEAD, the refs, `git status --porcelain=v1 --untracked-files=all --ignored`, the stash list,
  HEAD's reflog, `git ls-files --stage` and the in-progress files; the exact state was compared with
  `cmp`, first as the pair `status --porcelain=v2 --untracked-files=all --ignored` and `ls-files
  --stage`, then, after the review, whole as the conventions define it (HEAD, refs, index, status
  and every file's contents; `tools-fix-P/verify.sh`, both gits): equal after P25's Undo, after its
  second Undo and after P26 step 6's pop-back.
- **Scripts** (in the session's scratch folder, `report5/tools-P/`, not kept in the repository):
  `p24.sh` (the four refused checkouts of `other` and "Create branch" with checkout, each changing
  nothing; `notes` not ignored), `p25.sh` (the automatic stash: stash 256077f with its parents and
  its untracked part, the detached checkout, the way back with `switch main` and the plain pop, which
  leaves `src/lib.txt` unstaged; on a fresh copy the undo's `switch` and `stash pop --index`, the
  exact state back (`cmp`: equal), the redo's stash again 256077f, HEAD's reflog at each point),
  `p26.sh` (Other edits refused without a stash; HEAD's own commit detaches and carries the change;
  Add the guide refused on a clean tree, `git stash push` there would print `No local changes to save`;
  with changes stash f0c95a4, refused, popped back, the exact state equal, two `reset: moving to HEAD`;
  `check-ignore` under and without `GIT_LITERAL_PATHSPECS`), `p27.sh` (the merge, pick and revert
  refusals over a changed and a staged file, each changing nothing; the revert of Describe the app
  over an unstaged `src/app.txt` is not refused but conflicts on `README.md`, so step 4 changes
  `README.md`; the stash cc3e07a with its parents, the app's two reads, git's own apply and pop
  for contrast (the apply writes `todo.txt` anyway), a stash without untracked files has two parents
  and `ls-tree <s>^3` fails, one made with `--include-untracked` and no untracked file has an empty
  third), `v255.sh` (the same with Git 2.55). Their outputs are `report5/tmp-P/p24.out` to
  `p27.out` and `v255.out`. After the review, `report5/tools-fix-P/verify.sh` (with both gits;
  outputs `report5/tmp-fix-P/verify.out` and `verify255.out`, identical but for the version line):
  the whole exact state after P25's Undo, its second Undo and P26 step 6's pop-back; P26 step 3's
  plain `switch --detach` of HEAD's own commit; P27 step 4's revert with `README.md` and
  `src/app.txt` changed (TP12: exit 128, nothing changed, no `REVERT_HEAD`, HEAD's reflog
  unchanged); `check-ignore` with and without the variable.
- **Graphs**: P0 and P1 as measured in the running app (headless, `ShellDriver`, 1100×700) when the
  scenarios were pinned, P1 on the state plain git made with the app's environment: the graph
  control's rows, lanes, labels as drawn and `IsDashed` lines; P0w, P0h, P1m and
  P0s laid out from them by D48, D54, D55 and D60 (the working-changes ring in HEAD's node's lane and
  colour, `CommitGraphControl.WorkingRowLane`; a stash at the top, in lane 0, joined to its base),
  with the label widths measured there.
- **What git did that the design does not spell out** (D74's exception for `git check-ignore` held
  on both gits: with `GIT_LITERAL_PATHSPECS=1` it fails, 128): a detached checkout of HEAD's own
  commit detaches HEAD and prints the carried change; on 2.55 the merge's staged refusal starts with
  the same `error: …` line
  as the unstaged block (only the two-space indent of the names tells them apart) and every merge
  refusal exits 2 and adds `Merge with strategy ort failed.`; `stash push --include-untracked` with no
  untracked file still makes an (empty) third parent, while a stash made without the option has two
  parents and `git ls-tree <s>^3` fails (`fatal: Not a valid object name`, 128); `stash push` and
  `stash pop --index` each write `reset: moving to HEAD` into HEAD's reflog, a plain pop writes
  nothing; the redo's stash has the first stash's SHA. Nothing contradicted the design.

## Decisions this report makes

Values the design leaves open, chosen by this report's writers to fit the design and the codebase's
conventions, for the lead to fold into the design (or change, with the expected results that
follow, before the UI is built). They are numbered per part (AD, BD, FD, UD, PD); each names the
design section it fills, and the checks cite them by these numbers. The values the scenario facts
proposed for the checks (the changes P24 to P27 make, the files, branches, search texts and commits
of B5, B8, B9, F10, F11 and F12, a history row's secondary text, an uncommitted run's tooltip) are
the parts' decisions PD1, PD3, PD7, PD15, BD11, BD13, BD18, FD7 and FD12, and the checks' own
steps.

The lead ruled on the questions on 2026-10-15 (D183), and the checks follow the rulings: **AD11** as
proposed (the opening's reads are rows under `Show reads`, the `rev-parse` first; A2 step 4 asserts
that first top-level row too); **BD16** as proposed; **BD21** the item is `Restore the staged
changes`, without "…", since it opens no question (phase 3's rule); **UD10** the questions name, for
an annotated tag, the short SHA of the commit it points to (the checks use the lightweight `v1.1`,
so no expected result changes); **UD17** part U keeps its 55 methods, and the run time is watched as
D152 says; **UD18** as proposed (U16 step 15 as written); **UD20** as proposed, folded into design
15.8; **PD7** as proposed: D179's "every checkout of a commit the current branch reaches" excludes
HEAD's own commit, as design 9.1 does.

### Part A (AD1 to AD15)

- **AD1** (design 2.1). **The panel's maximum** is the graph area's height less 165, so that the
  content above the 5-px splitter keeps 160 (2.1 says "less 160" and "the banner and the graph keep
  at least that"; the two differ by the splitter): 421 at 1100×700, 801 at 1920×1080.
- **AD2** (design 2.2). **An empty activity log shows no text** (2.2 names none). If the lead adds
  an empty-state text, A3's steps 1 and 4 gain it.
- **AD3** (design 2.2). **Lines under an opened operation** (the failure's summary and each note)
  are single lines 22 high, as a call row, indented 24; the order is 2.2's (summary, notes, calls).
  The summary and each note are `TrimmedTextBlock`s, cut with "…" with the whole text in the
  tooltip, as 2.2 makes the title and the message; so is the call's command, which 2.2 cuts with "…"
  without naming a tooltip, and this decision gives it the whole command as its tooltip. At the
  440-px panel most of them do not fit (the convention "Text that may be cut"), and
  `AssertNoClippedText` lets a cut text through only when it is trimmed and its tooltip holds it.
- **AD4** (design 2.2). **A running operation** shows no duration until its record comes.
- **AD5** (design 2.1). **The rows' open state is kept** while the panel is closed and opened again
  (as 2.1 keeps it per worktree across tab switches). No step of part A relies on it any more (A2's
  methods never open the panel a second time); `ActivityLogViewModelTests`' per-worktree state can
  pin it.
- **AD6** (design 2.1 and 11.1). **Closing the log from inside it while a page shows** gives the
  keyboard focus to the page, the graph area's visible content (2.1's `GraphFocusRequested(Graph)`
  with the graph hidden).
- **AD7** (design 15.4's A2 and 15.6). **A2's scenario and steps**: "conflicts" with
  `AddOldAndManyBackupsAsync`; other.txt changed to `other file\nchanged on main\nchanged here\n`
  before the start; the failure is the double-click checkout of `feature/kinds` (which deletes
  other.txt), so D153's sentence and the stash button show; the stop is the app's merge of
  `feature/theirs`; the cancelled operation is the merge tool (a new scenario helper
  `Scenarios.ConfigureWaitingMergeToolAsync`, the tool `slow`, its command as given under "Test
  methods", the files `tool-started` and `tool-release` beside the working tree, released after the
  cancel and in a `finally`; the wait after the click on Cancel bounded at 30 s); the session's
  `ActivityLogHeight` 320, so that most of an opened operation shows (not all of it: an open
  call's box and 7.7's reads scroll the operation's own row out of view, "Changed in this report
  after its first commit"); the page is the backups list from the Local
  branches section's `Show backups`; the abort at the end adds a user operation that succeeded with
  a clean-up note. A2 is two test methods, each on a fresh copy, so that neither comes near the hang
  watchdog's 3 minutes under the full run's load (design 15.6 expects 8 to 15 s per operation
  there): `Check_A2_activity_log` (steps 1 to 6: the clean-up, the refused checkout, `Show reads`,
  the page and Esc) and `Check_A2_activity_log_stop_cancel_abort` (steps 7 to 11: the merge and its
  call's output, the merge tool running and cancelled, the abort). The call opened to its output
  (design 15.4's "input and output") is the stopped merge's own `git merge` (step 7), whose output
  is git's three lines and which has no error.
- **AD8** (design 15.4's A1 and A4). **A1's and A4's commit**: the summary `Describe the lines`, no
  description; A1 stages with `Stage all`, A4 stages src/Reader.cs alone with its row's
  `RowStageButton` and keeps the untracked `scratch.txt` (`scratch\n`, written before the start), so
  that the working-changes row and the stage panel stay after the commit; the summary's typing in A4
  is `Next`.
- **AD9** (design 15.4's A3 and 2.1). **A3's drags**: from the splitter's centre, 100 up (to 300),
  then after the restart 300 up (to the maximum) and 400 down (to the minimum), each in 5 steps, ±2
  px on every height.
- **AD10** (design 1.3). **A4 presses every redo gesture** of the platform's list in turn (Ctrl+Y,
  then Ctrl+Shift+Z on Windows and headless), each after its own undo.
- **AD11** (design 2.2 and 2.3; a question for the lead). **For the lead to rule on: are the
  opening's reads shown?** The design reads two ways. 2.2's "What it never shows" lists "the welcome
  page's open, init and clone, which run before a repository's tab has one (their calls are in the
  log file)"; 2.3 gives a read to the worktree whose working tree holds its working folder, and says
  "the first `rev-parse` of `OpenAsync` can run in a subfolder", as if that read were shown. A
  session-restored tab and the welcome page's Open run the same code (`MainWindowViewModel` line 171
  and `WelcomeViewModel` lines 91 and 125 call `RepoTabViewModel.OpenAsync`, which calls
  `Repositories.OpenAsync`), so whichever reading holds holds for both. This report proposes 2.3's:
  the opening's reads (from `git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir
  --git-common-dir` to the first refresh) are rows with `Show reads` checked, the `rev-parse` first
  among them, and 2.2's sentence then means only that a welcome page has no log of its own. Until
  the ruling is folded into 2.2, A2's step 4 asserts only what both readings share: the reads that
  2.3 gives the worktree, in sequence order; the backups' `for-each-ref` read before `Clean up
  backups`; `git --version` (no working folder) never shown; no call with an operation id at the top
  level. Once ruled, step 4 also asserts the first top-level row the ruling gives.
- **AD12** (design 15.2). **New harness helpers** (design 15.2, written to these checks):
  `ShellDriver.ActivityLog()` (the snapshot), `ActivityLogRow(name, occurrence)`,
  `ClickActivityLogToggle()`, `DragActivityLogSplitter(dy)`, `WaitForOperationRecordAsync(title)`,
  `PressSearch()`, `PressGitUndo()`, `PressGitRedo(int gesture = 0)`, `SearchBar()` (the bar's
  parts, shared with part F); `Scenarios.ConfigureWaitingMergeToolAsync`, `WaitingMergeToolStarted`,
  `ReleaseWaitingMergeTool`; and in the real window `RealApp.ActivityLogRows()`, the toggle, the
  splitter and the keys by automation id.
- **AD13** (design 15.3 and 15.4). **Picture names and test methods** as the table and the
  Screenshots column give them; A3 is a plain `[Fact]` with two `FreshApplication.RunAsync` starts
  (phase 0's check 6's form), the second writing no session (`ShellDriver.Start(data.Path)`;
  `RealApp.Launch(data)`), so that it reads the height the first saved.
- **AD14** (design 15.4 and 15.6). **RW marks**: A1 steps 1, 2, 4, 6, 7; A2 every step of both
  methods, captured except `a2d` and `a2h`; A3 every step, all five pictures captured; A4 method 1's
  steps 1 to 5. In part A, tooltips are read as help text in the real window, never captured there
  (part U's U16 captures one, UD16).
- **AD15** (design 2.2). **The list keeps to its end as it grows.** While the list is scrolled to
  its end, anything that makes it longer keeps it at its end: a new row (2.2) and also a row or a
  call opened, so that what opens below the last rows shows. When the user has scrolled up, opening
  a row keeps the rows above it in place. A2's steps 7, 10 and 11 rely on it (the merge's call, the
  abort's row and its backup's call are clicked, and pictured, without a scroll).

### Part B (BD1 to BD24)

- **BD1** (design 11.1). **Page geometry.** Every page has a header 36 high at the graph area's top
  (y 88 to 124) and its content from y 124, with no column header row; page rows are 26 high (row
  k's centre y 137 + 26 k). The checks click a page row at window x 420, in its subject, action or
  words column at every size this part uses.
- **BD2** (design 4.1). **The history page's layout.** Under 760 wide the list takes 40 % of the
  height below the header (220 at 1100×700: y 124 to 344) and the diff part the rest; from 760 the
  list takes 45 % of the width (567 at 1920×1080: x 260 to 827) and the diff part the rest, a 1-px
  `VcBorderBrush` line between them. A row: 8 padding, the status letter 16, 8, the short SHA 56
  (code font), 8, the subject (at least 120, cut with "…"), 8, the secondary text (at most 170, cut
  with "…"), 8, the author 110, 8, the date 100 (`yyyy-MM-dd HH:mm`), 8 padding. The date shows from
  a list width of 628, the author from 520, the secondary text from 402: at 440 the author and date
  are hidden, at 567 the date.
- **BD3** (design 5.2). **The reflog row's layout.** 8, the selector 72 (code font, secondary), 8,
  the time 112, 8, the action (at least 140), 8, the commit 160 (short SHA 56 in the code font, 6,
  the subject), 8, the lost mark 20 (`IconAlert` 12, centred), 8. The time shows from a page width
  of 552, the selector from 432: at 440 the time is hidden and the action gets 148.
- **BD4** (design 5.3). **The backup row's layout.** 8, the time 136 (`yyyy-MM-dd HH:mm:ss`, whole for
  every time), 8, the words (at least 112), 8, the commit 160, 8; nothing is hidden at 440 (136 and
  112 since the report's first commit: "Changed in this report after its first commit").
- **BD5** (design 5.2). **The reflog's footer** (`ReflogFooter`) is a fixed line 26 high along the
  page's bottom, under the list, shown only when git skipped entries (so at 1100×700 the list ends
  at y 648).
- **BD6** (design 5.2, 13.3 and 13.4). **The reflog page's header**: `ReflogSource` (label: the
  source's name and `IconChevronDown`) 8 from the left, `ReflogClose` 8 from the right, no other
  text; its pane name (`IGraphAreaPage.Title`) `Reflog of HEAD`, `Reflog of {b}` or `Backups`.
  Blame's header icon is `IconBlame`, the history's `IconHistory` (13.3).
- **BD7** (design 5.2). **The source menu**: header `Reflog and backups`; `HEAD`, the local branches
  by their full names in ordinal order (design 5.2's "by name"; the order of `git for-each-ref
  refs/heads`, so `feature/renamed` comes between `clash` and `main`, not first as the left panel's
  folder `feature` does), —, `Backups`; all enabled; it opens with its top-left corner at
  `ReflogSource`'s bottom-left corner.
- **BD8** (design 13.1, for 4.1, 4.2, 5.2 and 5.3). **The pages' menus**: `ForFileHistoryRow`:
  header `{short} {subject}`; `Show in graph`, `Blame at this commit`, —, `Copy SHA`. `ForBlameRun`:
  header `{short} {subject}` (`Not committed yet` for a run not committed yet, whose menu holds
  `Show in graph` alone); `Show in graph`, —, `Blame before this commit`, `File history`, —, `Copy
  SHA`. `ForReflogEntry`: header `{short} {subject}`; `Create branch here…`, `Check out this
  commit`, —, `Reset {cur} to this commit…`, `Cherry-pick onto {cur}`, —, `Show in graph`, `Copy
  SHA`. `ForBackup`: header the backup's words (12.3); its restore items, —, `Create branch here…`,
  `Check out this commit`, —, `Copy SHA`, the last three on every backup that holds a commit (5.3,
  7.10's last row), a `discard-…` snapshot included (it is a commit). This decision gives only the
  header, the order and the separators; the items are the design's.
- **BD9** (design 11.3). **Selecting on a page.** A right-click on a history row, a reflog row, a
  backup row or a blame run selects it as a click does, then opens its menu. Selecting a backup row
  shows its first ref's commit in the right panel (11.3's `ShowCommit`), as a reflog row does.
- **BD10** (design 11.1). **The focus when a page opens**: the history list on its first row; the
  blame text; the reflog list or the backups list with no row selected.
- **BD11** (design 4.2). **The blame tooltip**: five lines, the full SHA, `{author} <{email}>`,
  `yyyy-MM-dd HH:mm`, the subject, and `as {path}` when the run's path differs from the page's; a
  run not committed yet shows `Not committed yet` alone (git gives its lines the wall clock).
- **BD12** (design 4.2). **The blame gutter**: at 130 wide the first line of a run shows, left to
  right: 4 padding, the short SHA in the code font at 12 in the primary text colour (7 characters,
  50.4), 6, the date `yyyy-MM-dd` in the UI font (Inter) at 11 in the secondary colour (61.1 by the
  font's advances), 4 padding: 125.5 of 130, so both show whole. At 200 wide (a page at least 700
  wide, 4.2) the author, in the UI font at 11 in the secondary colour, goes between them with 6 on
  each side and takes the rest (68.5), cut with "…". If the date ever does not fit it is cut with
  "…"; the SHA never is. A run not committed yet is a run like the others: its first line shows `Not
  committed yet` (the UI font at 11, `VcWarningBrush`, 97.2 wide, from the left padding) and its
  other lines nothing. Run 0 has `VcPanelBackgroundBrush`; the gutter and the text share their top
  and line height. A click selects the clicked run's lines only (`VcSelectionBrush`, as 4.2 words
  it).
- **BD13** (design 4.1). **A history row's secondary text** takes the first that applies in 4.1's
  order: `renamed from`, `copied from`, `added` or `deleted`, then `as {path}` (so Add the parser
  reads `added`).
- **BD14** (design 4.5). **"All files"**: the button right of `Tree`, labelled `All files` without
  an icon, tooltip `Show every file of the commit`; the flat list in path order (git's); a click on
  an unchanged file gives its row the selection background, as a diff's file row has.
- **BD15** (design 7.10 and 13.2, with D75). **The backups list's question**: a path of a `discard-`
  or `-changes` backup counts as changed when its working-tree file differs both from HEAD's version
  (a missing file counts as one) and from the backup's; D75's question is asked when any does, with
  D75's texts word for word (also "since the discard" for an abort's, a skip's and a hard reset's
  backup); on Restore the changed paths are first saved in a `discard-<time>` snapshot of their own
  (D75), the restore follows, then the clean-up.
- **BD16** (design 7.10 and 7.3; a question for the lead). **Index restores from the backups list
  stage only the snapshot's own paths** (resolves the scenario facts 7, item 2): `Restore the staged
  changes…` runs `git restore --staged --source=<snapshot> -- <the paths diff-tree -r -z
  --no-renames --name-only <snapshot>^1 <snapshot> lists>`; a hard reset's `Restore the changes…`
  puts back "the index from its second parent first" for the paths `<snapshot>^1` and `<snapshot>^2`
  differ in (none in "reflog"). The undo's `IndexTo` inside a session (7.3) stays `-- .`, which is
  exact there, but the redo of `restore of the staged changes` uses the same paths as the restore.
- **BD17** (design 7.10 and 7.3). **Reflog messages of the backups list's ref writes**:
  `VisualCommit: <entry text>`, for example `VisualCommit: restore of branch doomed` (as the undo's
  `VisualCommit: undo <text>`).
- **BD18** (design 15.4's B8). **B8's branch**: `shortcut`, with "Check out the new branch"
  unticked, so HEAD stays on `main`.
- **BD19** (design 15.4's B9). **B9's outside edit**: src/app.txt written as `app\nmy own edit\n`
  before the app starts.
- **BD20** (design 7.10 and 12.2). **The backups list's file restores** (`Restore the discarded
  files…` and `Restore the changes…`) count every path of the snapshot in their status, also one
  whose content was already equal (B9: `Restored 2 files` for the discard's and for the abort's,
  whose docs/clash.txt already equals HEAD's; `Restored 1 file` for the skip's). `Restore the
  discarded files…` records `restore of the discarded files` (`UndoTexts.Restore`, as the restore
  bar's), `Restore the changes…` `restore of the changes`.
- **BD21** (design 12.4 and 13.2; ruled by the lead, D183). **`Restore the staged changes`** has
  no "…": no question follows it (13.2), and phase 3's rule keeps "…" for items that open a
  dialog. Design 7.10 and 12.4 are changed to match.
- **BD22** (design 5.1). **`Show reflog` and `Show backups` are enabled in an unborn repository**
  (the reflog page then says `HEAD has no reflog`), as "menus that always exist" (5.1) suggests;
  this fixes phase 3's `23i` picture.
- **BD23** (design 15.2). **The clean-up's wait**: on "reflog" both harnesses wait for the
  background `Clean up backups` record (message `Removed 2 backups`) before the first input (15.2).
- **BD24** (design 4.1). **The history's `from {short}`** (4.1's "when it starts at a commit other
  than HEAD") compares the start, resolved to a full SHA when the page opens, with HEAD's commit by
  full SHA: a history opened from HEAD's own commit (its file in the details panel, as B5 step 12)
  shows no `from` text, like one opened from the working tree.

### Part F (FD1 to FD21)

- **FD1** (design 3.1). The search's mode is Commits the first time the bar opens in a repository's
  session; afterwards the last chosen one (3.1 keeps it per repository for the session).
- **FD2** (design 3.3). While a search is active the working-changes row is neither marked nor
  dimmed: it is no commit and is not searched (3.3 dims "every other commit row").
- **FD3** (design 3.1). Enter and Shift+Enter in the box keep the keyboard focus in the box, so the
  next Enter goes on; the match is selected as a click selects it and the right panel shows it.
- **FD4** (design 3.1). Choosing a mode keeps the box's text, gives the focus back to the box with
  its text selected, and searches that text in the new mode after the mode's delay.
- **FD5** (design 3.1 and 13.1). The mode menu's header is `Search` (every menu has a header, phase
  3's 4a); its three items are always enabled and none is marked; the first is highlighted (D105). A
  click on the mode button opens it with its top-left at the button's bottom-left corner, as phase
  4's action-button menus open (phase 4 design 14.3).
- **FD6** (design 3.3). "Dimmed" is checked as 35 % opacity over the row's background, ±3 per
  channel: dark #383F6B (colour 0), #234F3E (colour 1), #5B4726 (colour 2) over #14161B; light
  #BDC3F1 (colour 0) over #FFFFFF; a node is sampled 3 px right of its centre, off the lane's line.
- **FD7** (design 15.4's F10 and F11). Search texts: F10 `tokenizer`, `Jonas`, `0575`; F11 Files
  `parser`, `reader` and `TraceEverything` (for `No matches`), then the Changes mode with the same
  `TraceEverything`.
- **FD8** (design 15.4's F11 and 3.4). F11's reload is a commit made outside the app with only
  src/Config.cs staged ("Trace again", `f612569`, 12:14 on the copy's clock), through
  `TempRepo.WriteFile` and `TempRepo.GitAsync("add", "--", "src/Config.cs")` and `GitAsync("commit",
  "--quiet", "--message", "Trace again")`, not `CommitFileAsync`, whose `add --all` would also
  commit src/Reader.cs; "only the new rows are given to git" is proven by the one search call's
  output.
- **FD9** (design 6.1 to 6.3). A new comparison starts in its default: two commits newly selected
  (also after a third was taken out again) in the graph's order, a swap not being remembered; a
  branch comparison three-dot, "All differences" unchecked.
- **FD10** (design 6.4 and 13.4). `CompareSwap`'s tooltip is `Swap From and To` and `CompareClose`'s
  `Close the comparison`; the whole "From …" and "To …" texts are the links.
- **FD11** (design 15.4's F13). F13's drop drags `main` onto `feature/compare`, so that the drop's
  comparison runs the other way from the menu's (From feature/compare, To main); F13's draft summary
  is `Keep this draft`.
- **FD12** (design 15.4's F12). F12 compares Rename the parser to reader (row 7, From) with Read
  whole lines (row 0, To) and adds Merge branch 'feature/readme' (row 3) as the third.
- **FD13** (design 15.4's F14 and 8.3). F14 opens every branch menu from the left panel (D114: a
  right-click there selects nothing), after opening the `feature` folders, and reads a hidden row's
  tooltip with the pointer on its name (`RefRowNamePoint`), not on its toggle.
- **FD14** (design 8.4). A `RepositoryGraphFilter` whose Hidden, Soloed and HiddenRemotes are all
  empty is removed from `SessionState.GraphFilters` when it is written (its Upstreams are then empty
  too, since an upstream is kept only beside its hidden or soloed branch; its Remotes and LastUsed
  serve only a filter that exists). So Show, the eye toggle or Stop soloing that leaves nothing
  hidden or soloed removes the repository's entry, and "no filter" is the same as "no entry" (8.4
  does not say which; this keeps the 100-entry bound for filters that do something).
- **FD15** (design 10 and 15.2). The dash rule the checks sample (`AssertDashedPixels`): along a
  lane's centre, each pixel is classed by the nearer of the lane colour and the row's background
  (sum of channel differences); every run of either class is 2 to 4 pixels long, except the first
  and the last, which the range cuts; consecutive dashes start 6 ± 1 pixels apart throughout the
  range, row boundaries included; the range runs from 2 below the ring's bottom (the ring's centre +
  7) to 1 above the base node's top (the node's centre − 6), except that when another line curves
  into the base node in its row (an IntoNode line from another lane), it ends 2 below that row's top
  instead, before the curve can reach the lane's centre. So: GS0's lane 0 from 136 to 175, FL0's
  lane 1 from 162 to 175, FL0's lane 0 from 136 to 404 (lane 2's IntoNode 2→0 comes into Alpha one
  in row 11, whose top is 402). `AssertSolidPixels`: every pixel of the range within 3 per channel
  of the lane colour.
- **FD16** (design 15.4's F14 and 6.4). F14 compares the hidden `feature/alpha` with `main` to
  picture 6.4's disabled link (`This commit is not in the graph`) and the counts' singular form.
- **FD17** (design 15.4's F10). F10 has a second method at 1920×1080 in the light theme, so that the
  light `VcSearchMatchBrush` (13.3) and the bar at full width are pictured.
- **FD18** (design 3.1). The positions with the search bar open (the bar y 88 to 124, the column
  headers 124 to 152, the rows from y 152) are derived from 3.1's 36 px and the measured graph area,
  since the bar is not built yet; the checks take them from the controls' bounds.
- **FD19** (design 8.3). A click on a hidden branch's row whose commit is no row selects nothing and
  highlights no left-panel row (8.3's "nothing is selected").
- **FD20** (design 6.2 and 6.3). What the comparison passes `CompareAsync` (so what its `rev-parse
  <from>^{commit} <to>^{commit} --` names, From first): two selected commits by their full SHAs; a
  branch or tag comparison by full ref names, the names the menus already carry (`GitRef.FullName`,
  the drop's `SourceRef` and `TargetRef`): from a menu, From the current branch's
  (`refs/heads/{cur}`) and To the menu's ref (`refs/heads/…`, `refs/remotes/…`, `refs/tags/…`); from
  a drop, From the target's and To the source's. A full name cannot be taken for a tag or a file of
  the same short name. So F13: `git rev-parse refs/heads/main^{commit}
  refs/heads/feature/compare^{commit} --` and, from the drop, `git rev-parse
  refs/heads/feature/compare^{commit} refs/heads/main^{commit} --`; F14: `git rev-parse
  refs/heads/main^{commit} refs/heads/feature/alpha^{commit} --`. Git prints the two SHAs and then
  `--` (2.36 and 2.55), which `CompareAsync` reads past.
- **FD21** (design 6.4 and 13.1). `ForCompareFile` (6.4) is built as `ForCommitFile` is: header the
  file name; `File history` and `Blame` as one group; then, after a separator, `Open file`, `Show in
  folder`, `Copy path` (`FileItems`, `Open file` disabled with `The file is not in the working tree`
  when the path is not there). A right-click on a row of `CompareFileList` opens the menu only,
  selecting no row and opening no diff, as on a commit's file. F12 pictures it on `Reader.cs` after
  the swap (`f12d-compare-file-menu`, 171 high).

### Part U (UD1 to UD20)

- **UD1** (design 15.4's U16). **U16's values.** The scenario "history"; the commit's message
  `Describe the reader and add Length` / `The summary says lines now.`; the amend's message is
  HEAD's own ("Read whole lines", filled by Amend); the branch `keep`; the untracked `docs/todo.md`
  "# To do\n"; for "Discard all" `README.md` with its last line (`The reader splits a text into
  lines.`) replaced by `Staged.`, and staged; for D75's case (step 14) the line `// Changed
  outside.` appended to `src/Reader.cs`.
- **UD2** (design 15.4's U16 and 7.9). **U16 keeps the stage panel in view.** The discard methods
  start with `docs/todo.md` untracked (and "Discard all" with `README.md` staged), so that a change
  is left after the discard and the working-changes row, the stage panel and its restore bar stay on
  screen; "history" alone would leave a clean tree, and the bar lives in the stage panel.
- **UD3** (design 7.9). **Undoing a recorded Restore leaves the bar closed** (7.9 says nothing): the
  bar belongs to its discard's entry, which that undo does not touch. U16 step 28.
- **UD4** (design 7.5). **Selections before Redo.** Where a step's result would depend on where a
  vanishing working-changes row leaves the selection, the check first selects a row that stays (U16
  steps 6 and 9); the boxes cleared by Redo are asserted through `WorkingChangesViewModel`.
- **UD5** (design 15.4's U17 and 7.3). **U17's values.** The resets of phase 3's check 10 (to "Add
  core"); the staged case uses its part C changes. A redone hard reset makes a new `reset-…` pair
  whose `-changes` ref points at the same snapshot commit as the first (D72), and its reflog entry
  is git's `reset: moving to …`.
- **UD6** (design 15.4's U18 and 7.6). **U18's values.** The fast-forward uses `edges`' `loc`, whose
  upstream is the local `main` (no fetch needed); the commit checkout is "Add core" on a clean tree
  (9.1's path, no stash); the tracking checkout uses phase 3's check 6 setup. The entry text of a
  remote branch's checkout names what was clicked (`checkout of origin/feature/alpha`, WP0's plan
  name) and a commit's its short SHA (`checkout of 8beca34`).
- **UD7** (design 7.8 and 13.2). **Undo and redo of "leaving a detached HEAD's commits" ask nothing
  and make no backup**: 13.2 has only remote steps ask; the action's own `detached-…` backup is kept
  by the entry (7.8).
- **UD8** (design 15.4's U19). **U19's values.** The interactive rebase drops Five and rewords Two
  to `Two reworded` (keys D and R, phase 4's B7), giving `4ad730d` and "Rebased main: 5 commits";
  the squash is phase 4's B8; the pull is phase 4's A4 part A pull with rebase, so that 15.5's
  "rebase of every kind (a pull's)" is undone once (a fast-forward pull is the same Keep move).
- **UD9** (design 7.5, 7.6 and 12.1). **The lease failure's dialog.** 7.5 and 7.6 differ on it; the
  check takes 12.1's title `Undo failed`, 7.5's summary `published on origin has changed since:
  nothing was changed.` without 7.6's `Can't undo …: ` prefix, git's output (TU2) and command, "Copy
  output" and "Close", and the status `Undo push of published to origin failed`. The entry stays,
  Undo enabled.
- **UD10** (design 7.5 and 13.2; a question for the lead). **The questions' form and the tag
  questions.** No `ConfirmDetail` line (7.5 gives a title and a text), the confirm button in the
  primary style. The tag forms put the word `tag` before the name in title and text, as in the U21
  table above. `{short}` is the short SHA of the value the remote ref holds or will hold; the checks
  use the lightweight `v1.1`, whose value is the commit `88d6b91`. For an annotated tag that value
  is the tag object's id; the lead may prefer the commit's in the text.
- **UD11** (design 15.4's U21). **U21's force push** uses phase 3's check 20 rewrite ("Rewritten
  work" `4f6a372` on `published`, made before the app starts); the lease check pushes `published`
  (an update) and then pushes "Published elsewhere" (`c16ddfa`) from the other clone without a fetch
  in the app.
- **UD12** (design 15.4's U22 and 7.12). **U22's added remote fetches.** "Fetch now" stays ticked
  and the URL is the copy's own `origin.git` folder, so that the undo deletes the fetched
  `refs/remotes/mirror/*` as design 7.12 settled ("Add a remote" after "Fetch now") and the redo
  leaves the state right after the add; the folder shows only in the Remotes dialog, which no
  picture shows. The URL change uses `https://example.invalid/sync.git`, never fetched.
- **UD13** (design 7.5 and 7.6). **U23's refusal texts.** For local changes in a move's way the
  dialog has 7.6's prefix and the app's count in its summary and git's line (TU1) in `ErrorOutput`
  with its `ErrorCommand` (7.6's "git's line goes below the summary" over 7.5's "no git output");
  the ignored-files refusal is the app's own check and shows no git output. `{name}` in `1 ignored
  file is in the way: {name}` is the path relative to the working tree (`docs/ff.txt`), as git names
  paths.
- **UD14** (design 7.4 and 1.3). **U23's flows.** The terminal commit is reset away outside the app
  at the end, to show the entry undoable again once the state is back (7.4); during the merge in
  progress Redo's tooltip is `Finish or abort the merge first` too (7.4's order puts reason 2 before
  `Nothing to redo`); the platform's undo key is pressed with the graph focused to show that a
  disabled Undo's key does nothing (1.3).
- **UD15** (design 15.4's U23 and 7.3). **Stale stat data is made by the check** (U23 part E): the
  action's own git calls refresh the index, so the fresh copy's stale stat data would be gone; the
  check rewrites `docs/ff.txt` with its own bytes and moves its last-write time one hour on, and
  asserts with git that the data is stale before the Undo. Every git read the check makes from that
  write to the Undo, the conventions' reads after step 23 included, runs with
  `GIT_OPTIONAL_LOCKS=0`, as the app's reads do: a plain `git status` refreshes the stat data and
  writes the index (2.36 and 2.55: afterwards `read-tree -n` exits 0), which would leave the Undo's
  `update-index -q --refresh` nothing to do.
- **UD16** (design 15.2). **New driver helpers** (WP8, `ShellDriver.Phase5.cs`): `UndoRedoState()`
  (both buttons' enabled state and tooltip), `ClickUndo()`, `ClickRedo()`, beside design 1.3's
  `PressGitUndo()`; `RealApp` clicks `UndoButton` and `RedoButton` by automation id and reads their
  `HelpText` (WP9, for U16 only).
- **UD17** (design 15.4 and 15.6; a question for the lead). **Scope, pictures and run time.** Part U
  adds methods beyond 15.4's rows so that every kind 7.2 records and a user reaches in phases 2 to 4
  is undone and redone once in the app (a branch checkout, a commit checkout, "Check out and merge",
  "Check out and rebase", a tag push, adding, renaming and re-pointing a remote, the bar's Restore
  recorded, D75's case, several steps in a row): 55 methods, about 170 operations and 120 pictures
  (two per method, undone and redone, plus the questions, tooltips and refusals). At about 15 s a
  method alone (a start and three operations) that is about 14 minutes alone and 25 or more under
  the full run's load, beyond 15.6's estimate (about 30 methods, 15 to 25 minutes); the lead may
  move the added kinds to `UndoRecordingTests` (7.11) if the run grows too long. No method runs more
  than six operations (`Check_U16_discard_file` and `Check_U16_many_steps`: at 15.6's 8 to 15 s an
  operation under load, at most about 90 s and the app's start), so each stays well under the hang
  watchdog's 3 minutes (15.4); "several steps in a row" is therefore two entries undone and two
  redone, in a method of their own. The real window repeats U16's commit method only.
- **UD18** (design 7.9, with D75; a question for the lead). **7.9 against D75, a question for the
  lead, pinned by U16 step 15.** When the discard is the entry before the cursor but a file changed
  since, 7.9 as written makes the bar's Restore that entry's Undo, which is refused (`Can't undo
  discard of Reader.cs: 1 file has changed since: …`, 7.6), where D75 asks `Restore discarded
  changes?` and saves the newer version first. Proposed, and what U16 step 15 expects: the bar's
  Restore runs as the Undo only while the files are as the discard left them (the bar's own check,
  `RepositoryViewModel.ChangedSinceDiscardAsync`, made first: `RestoreAsync` now calls
  `TryRestoreAsUndoAsync` before it); otherwise D75's question and a Restore recorded as an entry of
  its own (`restore of the discarded files`). This keeps phase 2's
  `WorkingChangesTests.Restore_asks_when_the_file_changed_since_the_discard_and_keeps_the_newer_version_in_a_snapshot`
  as it is ("Changes to expected results"). If the lead keeps 7.9 as written, that test changes, and
  U16 step 15 expects the refused Undo instead: no question, the error dialog `Undo failed` with
  that summary, the status `Undo discard of Reader.cs failed`, the entry kept.
- **UD19** (design 7.5). **The order of a remote removal's undo** in the call log is not asserted:
  WP0's sequence puts the keys before the refs, 7.5 step 4 groups the `update-ref --stdin`
  transaction before configuration writes; the result is the same either way (U22 step 14 asserts
  the commands and the end state).
- **UD20** (design 7.3, 7.5 and 7.12; a question for the lead). **A step's calls are asserted as the
  merged git half runs them** (`b0e8710`, `GitRepository.Undo.cs`, `ApplyAllAsync`;
  `UndoEffectTests` pins the `--stdin` form), where 7.3's table and 7.5 step 4 say otherwise. A soft
  move (`MoveHead` or `MoveDetachedHead` with `Soft`: the undo and redo of a commit, an amend, a
  soft and a mixed reset) is a line of the step's one `update-ref -m <msg> --stdin` transaction
  (`update refs/heads/main <to> <from>`), not 7.3's positional `update-ref -m <msg> refs/heads/<b>
  <to> <from>`; a tag's write is a line of it too (7.3 gives it positionally), as a branch's is in
  7.3. The transaction is written at the step's end unless a later effect needs one of its refs
  first (a switch to a branch the step creates: U18 steps 6 and 18). `IndexTo` runs at its place, so
  a mixed reset's undo runs `restore --staged` before the transaction (U17 steps 5 and 8), where 7.5
  step 4 puts index and file writes after it. Configuration and `.gitignore` writes come after the
  transaction and any switch (U18 steps 12 and 18), whatever the sequence's order. A Keep move keeps
  its positional `update-ref` (7.3, D169; U17 step 11, U19). The end states are the same either way:
  on 2.36 and 2.55 a transaction's update of the branch HEAD is on writes the message to the
  branch's and HEAD's reflogs, and the mixed reset's undo and redo leave the same index and refs in
  either order. The lead folds this into 7.3's table and 7.5 step 4 (or 7.12); if WP3's code is
  changed to 7.3's forms instead, these lines change with it (U16 steps 4, 6, 8 and 9; U17 steps 2,
  3, 5, 6, 8 and 9; U18 steps 12 and 18).

### Part P (PD1 to PD17)

- **PD1** (design 15.4's P24 to P27). **The changes each check makes** are those
  `Phase5ScenarioTests` pins where it pins one: P24 `src/app.txt` "app\nlocal\n", then also
  `src/lib.txt` "lib\nlocal\n" and `docs/guide.txt` "guide\nlocal\n"; the untracked `notes` "my own
  notes\n", then `notes/today.txt` "today\n"; P25 and P26 part B: `src/lib.txt` "lib\nstaged\n"
  staged, then "lib\nstaged\nand more\n", `src/app.txt` "app\nunstaged\n", `scratch.txt`
  "scratch\n"; P27 part A `src/app.txt` "app\nlocal\n", then also `README.md` "#
  Protect\n\nLocal.\n" (not staged; no pin has it, chosen here), then `src/app.txt` restored and
  `README.md` "# Protect\n\nStaged.\n" staged; P27 part B the stash "App experiment" (`src/app.txt`
  "app\nstashed\n" and the untracked `todo.txt` "todo\n", made before the app starts) with
  `src/app.txt` "app\nlocal\n", then `todo.txt` "mine\n". The new branch's name in P24 is `topic`.
- **PD2** (design 15.3 and 15.4). **Test methods and starts**: two test methods per check (part A
  and part B), eight app starts; each check's second part needs a fresh state (a clean tree, a
  stash, an undo history whose last entry is the automatic stash's checkout).
- **PD3** (design 9.1). **The checkout paths P24 uses**: a left-panel double-click, a graph label's
  double-click, the branch menu's "Check out other" and the commit menu's "Create branch here…" with
  its box ticked; P25 and P26 use "Check out this commit" (the tag's "Check out v1.1" and the
  reflog's and backups' checkouts are the same operation and are left to App tests). The drop's
  "Check out other and merge main into it" (`main` dropped on `other`) is refused at its checkout
  with 9.3's sentence and no button and is left to App tests (`RepositoryOperationsMergeTests` 199,
  9.4); the drop's "Check out other and rebase it onto main" (`other` dropped on `main`) never
  reaches its checkout with a change, since phase 4's own refusal comes first (its A5 step 3).
- **PD4** (design 9.3). **"Create branch" with checkout** shows 9.3's message with title "Checkout
  blocked" and **no** "Stash changes and check out": phase 3 offers that button only for
  `CheckoutAsync`'s checkouts, and D91's action checks out an existing target, which the refused
  branch is not.
- **PD5** (design 9.3 and 12.1). **The title "Checkout blocked"** is 9.3's for every row of its
  first table, the refusals before git included (`Resolve the conflicts first`, `Finish or abort the
  {kind} first`), and for the refusal after an automatic stash (9.2 step 5: "the refusal's
  message"); 12.1's "Checkout failed" stays the title of the automatic stash's other failures (the
  half-made stash) and of checkouts git refuses for other reasons.
- **PD6** (design 9.3 and 12.1). **Merge, cherry-pick and revert refusals** keep their operations'
  error titles ("Merge failed", "Cherry-pick failed", "Revert failed") and status texts; 9.3 changes
  the summary only.
- **PD7** (design 9.1, with D179; a question for the lead). **HEAD's own commit** is checked out as
  9.1's second row says, as today: `git switch --detach <sha>` without `--no-overwrite-ignore`, and
  no stash; its status text is phase 3's `HEAD is now detached at {short}`. D179's wording ("every
  checkout of a commit the current branch reaches uses `--no-overwrite-ignore`") read alone would
  include HEAD's own commit; 9.1's first row excludes it ("the target is not HEAD's own commit"),
  and P26 step 3 follows 9.1. Both commands give the same result there (exit 0, the same streams,
  the same reflog entry; checked with both gits), so only the asserted command line depends on it.
  For the lead: align D179's wording with 9.1 ("other than HEAD's own commit").
- **PD8** (design 9.1 and 9.2). **A clean tree** (an ignored file only) makes no `git stash push`
  call at all (9.1 "when the status has changes", 9.2 step 1), so git's `No local changes to save`
  never shows.
- **PD9** (design 9.3). **The ignored-files text for one path** is `1 ignored file is in the way:
  73e0bab tracks a file of the same name. Move it away first.` with `{short}` the target's short SHA
  (the tag's name for a tag's checkout, as `{name}` in 9.2).
- **PD10** (design 9.3 and 0.7). **The merge's staged refusal is told by its names' two-space
  indent**, not by a missing `error:`: on 2.55 it has TP7's first line; its key line on both gits is
  `  README.md`. 9.3's count of a `by merge:` block counts tab lines only.
- **PD11** (design 7.3, 7.8 and 9.2). **No backup ref** is made by part P's steps except the user's
  Pop (D171: `stash-20260102T090001000Z` in P25 step 4): Undo's and Redo's `StashPop` and
  `StashPush` save none (7.3 names none), the automatic stash's pop-back after a refusal saves none
  (9.2 names none), and a pop refused by the app's check saves none (the check comes before the
  pop's backup).
- **PD12** (design 9.2, with phase 3's 2.25). **The toolbar's Pop of the automatic stash is phase
  3's plain `git stash pop stash@{0}`** (2.25): the staged part comes back unstaged, the contents
  identical, which meets 9.2's "gives the changes back without a conflict"; only Undo (D166)
  restores the index (`--index`). P25 asserts both. If the owner wants the user's Pop to keep the
  index too, that is a change to 2.25, not part of C12.
- **PD13** (design 9.2 and 7.5). **The selection** after the automatic stash, Undo and Redo stays on
  the commit the user right-clicked (Describe the app), kept by SHA as phase 1's refresh keeps it;
  nothing in design 9 or 7.5 moves it.
- **PD14** (design 9.2). **The status text after the automatic stash is asserted whole** (not
  trimmed), as design 9.2 step 6 says it fits the status bar; `OperationStatus` is at most 520 wide
  (`MaxWidth`) and starts about 150 from the left, so it ends left of the neutral point (760, 687),
  which stays free of tooltips.
- **PD15** (design 15.4's P27 and 9.3). **P27 adds the revert**, in both forms, to design 15.4's
  merge and cherry-pick (the same rows of 9.3), so that each of the three operations' sentences is
  pictured: with `README.md` changed and not staged git refuses the revert of Describe the app with
  the `by merge:` block (step 4, `1 conflict prevents the revert. …`); with `README.md` staged, with
  the one-line form (step 8). With only `src/app.txt` changed the revert is not refused: it touches
  `README.md` alone and conflicts there (exit 1, `UU README.md`, `REVERT_HEAD`), which is why step 4
  changes `README.md`. The stash's pre-check (9.3) reads `<s>^3` only when the stash commit has
  three parents (phase 3's "On main: Experiment" in check 15b has two, and `git ls-tree <s>^3` fails
  on it).
- **PD16** (design 15.2). **Waits after a change made outside the app** use the status read's counts
  (`1 staged, 3 unstaged`, `0 staged, 3 unstaged`, `0 staged, 2 unstaged`, `1 staged, 0 unstaged`)
  and, where the counts do not change (P24 step 7), the status's paths; the new support helpers are
  `WaitForStatusReadAsync`, `ExactStateAsync` and `AssertDashedLines`.
- **PD17** (design 15.4). **The real window** repeats P24 part A's steps 1 and 2 and P25 part A's
  steps 1 and 2 with part B's Undo in the same launch (`p25e-undone` compared with part B's scripted
  picture of the same state); P26 and P27 are n/a.

## Found by the gate

Faults the gate found, and the commit that fixed each. Filled in when the gate runs.

## Could not run

Checks or parts of the gate that could not run on the Windows machine either, why, and what that
leaves unproven. A real-window check that was not run only because the session was not on Windows
does not belong here: it stays "Not run: needs Windows" in the table, and the gate is unfinished
until a Windows session has run it. Planned already, as the plan allows: the steps at 1920×1080
(B5's `Check_B5_side_by_side_light`, F10's `Check_F10_search_light_1920`) are scripted only, since
a 1920×1080 window does not fit the development screen's work area at 200 percent.

## Other platforms

The CI run of the gate's commit and its result in each job (the three platforms and the two bare
Ubuntu containers), and which of the screenshots CI took on macOS and Linux were opened. Filled in
when the gate runs.

## macOS checklist for the owner

To try by hand on a Mac (the scripted run covers macOS on CI; these are what only a person sees):

1. The platform's undo and redo outside a text box: Cmd+Z undoes the last git action (a commit, say)
   and Cmd+Shift+Z redoes it; the toolbar's Undo and Redo tooltips name the action and the keys do
   nothing while a button is disabled.
2. Cmd+Z inside the commit summary undoes the typing only, also when the summary is already empty;
   inside the conflict resolver's result it is the resolver's undo.
3. Cmd+F opens the search bar (the Search button's tooltip reads `Search commits (Cmd+F)`); Enter
   and Shift+Enter go through the matches; Esc closes the bar and gives the graph the focus.
4. The activity log: open it from the status bar, drag its splitter with the trackpad, quit and
   start the app again: the height is kept; Esc inside the log closes it before anything else.
5. Cmd+click two commits: the comparison shows at once; Cmd+click a third: the list of three.
6. The blame page: a double-click on a run with the trackpad shows the commit in the graph once;
   the gutter's dates and `Not committed yet` fit at the window's width.
7. Hide and solo from a branch's menu; the eye and solo buttons in the left panel with the trackpad
   start no drag; quit and start again: the hidden branch is still hidden.
8. "Check out this commit" on an older commit of the current branch with changes: no question, the
   stash drawn as a ring with a dashed line, and Undo (Cmd+Z) switching back with every change back.
