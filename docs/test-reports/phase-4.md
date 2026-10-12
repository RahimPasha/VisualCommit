# Phase 4 visual test report — Rebase and conflict resolution

The Check, Steps and Expected columns of the table below were written at the start of the phase,
before the UI they test was built (D15), from `requirements.md`, `plan.md`, `docs/phase-4-design.md`
and decisions D139 to D151. Four writers drafted them part by part; every git state in them was
derived with Git 2.36 on replicas of the scenario repositories whose SHAs equal those
`Phase4ScenarioTests` pins; each part was reviewed adversarially and revised, and the lead's
decisions on the writers' questions are in design 14.3. The result columns are filled in when the
gate runs.

How to read it: "What the checks share" holds what the checks of each part rely on (the scenario
repos and their graphs, the texts the design fixes, git's own texts); "Checks" is the one table of
all checks, each named by its part's letter and its number in design 13.4; "What proves each
Delivers item" maps the plan's list to checks and tests; "Changes to expected results" lists what
phase 4 changes in earlier phases' checks on purpose, phase 3's menus among them; "How the expected
git states were derived" says how every git state in the checks was obtained. "RW" marks a check, or
steps of it, that the real-window pass repeats; "n/a" says why it does not.

## Run

Filled in when the gate runs, for each of its two passes: date, app commit, the machine, the display
scaling of the real-window pass, the screenshot folders, and how many pictures were inspected.

## What the checks share

### Conventions of every part

- **Commits** are named by their subjects, and by their short SHA (the first 7 characters) where a
  text on screen shows it; those SHAs are the ones `Phase4ScenarioTests` pins. A commit the app makes
  is named by its subject only, unless its id is stated as the one plain git gives for the same
  commit. Refs are named by their short names.
- **Times** are UTC. A scenario's commits are dated by `TempRepo`'s clock, 2026-01-01 12:00 plus one
  minute per commit; commits the app makes on a branch or rewrites have the committer date
  2026-01-02 09:00 and the identity "Test Author" <author@example.com> (D72); the app's backup
  snapshots (`…-changes`, `discard-…`) are by VisualCommit <snapshot@visualcommit.invalid> and are
  asserted by parent and content, never by id; a rebase keeps each commit's author date.
- **Lanes** count from 0 at the left; **colours** are lane colour numbers, as in phase 1's report
  (dark: 0 `#7C8CFF`, 1 `#3FB97F`, 2 `#E0A23B`, 3 `#E5606B`, 4 `#4FB3D9`).
- **Menus** are written header first, items in order, "—" for a separator; "(danger)" marks an item
  drawn in `VcDangerBrush`, "(disabled: "…")" a disabled item with its tooltip. The scripted run
  reads a menu with `ShellDriver.Menu()` and clicks with `ClickMenuItem`; the real-window pass with
  `RealApp.WaitForMenu`, `MenuItemNames` and `ClickMenuItem` (phase 3's conventions). A menu that
  fits below the pointer opens with its top-left 2 right of and 2 below it (phase 3's 4a); a list
  row's menu opened by its action button, Shift+F10 or the Menu key opens at the button's
  bottom-left corner (14.3). Every check asserts the height (±2) and the 4-px margin, and the
  position only where its expected result states it.
- **Resting the pointer** on an item, button or row, **waiting for a change made outside the app**,
  **"no menu is open"**, **log lines**, **paths** and **the pointer before a capture** (the neutral
  point (760, 687) at 1100×700) are as phase 3's report defines them.
- **One test per app start**: a check whose steps start the app again is one test method per start.
- **The banner** is described by its text, its buttons, whether the text takes "one line" or "more",
  and whether the buttons stand beside the text or on a row of their own (design 5.2); never by a
  measured height.
- **Undo and redo** in the resolver are "the platform's undo" and "the platform's redo" (Ctrl+Z and
  Ctrl+Y on Windows and Linux, the platform's own on macOS: `ShellDriver.Undo()`, `Redo()`).
- **The result's text** is read through the editor's test surface (`ConflictResultEditor.Text`) in
  the scripted run and through its Value pattern in the real window; its caret does not blink in
  either harness (D151).
- **Git's own texts** given word for word are asserted only when `git --version` is 2.36; on any
  other version a check asserts the classifying line and the dialog's title, summary and buttons
  (phase 3's rule).

### Part A: rebase from the menus and by drag, pull with rebase

Part A covers design sections 1 (rebase onto a commit or a branch, from the menus and by drag), 6
(pull with rebase, chosen for one pull), the new items of 7.1 in the commit, several-commits,
branch, remote-branch and drop menus with their disabled reasons (1.1, 2.1, 3, 6, 10.4), the
rebase's backup (1.2), how a rebase ends (1.4, 10.1, 10.2), and a pull's rebase that stops, with
Continue and Skip (5.1 to 5.4, 7.2): checks A1 to A5 of design 13.4. The list itself and the
squash are part B's; the resolver is part C's. Every git state below was derived with Git
2.36.0.windows.1 on replicas of the scenarios whose SHAs equal the pinned ones, each operation run
with the git command lines the merged git layer runs (WP1, WP2) and the app's environment (see "How
the expected git states were derived").

#### Conventions of part A

- **Defaults.** Unless a check says otherwise, a test method writes a session with one tab on a
  fresh copy of the named scenario, starts the app at 1100×700 in the dark theme, and waits until the
  graph has drawn its rows and the queue is idle (`WaitForIdleAsync`). Each part of a check that
  starts the app again is a test method of its own (`FreshApplication`): `Check_A1_new_menu_items`,
  `Check_A2_rebase_onto_a_branch`, `Check_A2_rebase_onto_a_commit`, `Check_A3_rebase_by_drag`,
  `Check_A4_pull_with_rebase`, `Check_A4_pull_with_rebase_stopped_and_continued`,
  `Check_A4_pull_with_rebase_stopped_and_skipped`, `Check_A5_refused_for_a_changed_file`,
  `Check_A5_refused_for_an_untracked_file`.
- **"Outside the app"** is a git call or a file write through `TempRepo`, as a terminal would make it,
  on the scenario's clock. Every such step is made before the app starts, except A4 step 5's file
  write, after which the check waits for the app's next status read (one more Debug line
  `<repo>: working tree: <s> staged, <u> unstaged`, `AppLogFile.WorkingTreeRead`, counted before
  the write, with `OutsideChangeTimeout`) and until idle, as phase 3's "waiting for a change made
  outside the app" says and its check 25 step 4 does.
- **Rows** are numbered from 0 at the top. While the working-changes row shows, it is row 0 and
  every commit row moves down by one; a step that names a commit by its row then says so. With no
  banner, rows start at window y 116 (graph area top 88 + headers 28) and are 26 high, so row r's
  vertical centre is at y 129 + 26 r; the Message column starts at x 454 with three lanes (260 + 130
  + 64) and at x 438 with two (260 + 130 + 48). While a banner is shown no position is fixed: rows
  and labels are found from the graph control's bounds (`ShellDriver.GraphRowPoint`,
  `GraphLabelBounds`), as phase 3's D122 rule says.
- **A row is right-clicked "in its Message column"; a label is pressed or right-clicked at its
  centre**, and only the first label of a row, which phase 1's "+N" rule always shows. Labels are
  listed in phase 1's order (a detached `HEAD`, the branch HEAD is on with its check mark ✓, other
  local branches, remote branches, tags); a row's label list is the full list, and phase 1's "+N"
  rule (measured with the app's font, as phase 3's tests measure it) decides how many show.
- **Left-panel paths** are written Local branches › feature › topic and Remotes › origin › main. A
  right-click on a left-panel row opens its menu only: nothing is selected or highlighted and the
  graph's selection does not change (D114).
- **Ctrl+click** adds a row to the selection or takes it out, as in phase 3's check 11 (the
  harness's `ShellDriver.CommandModifier`: Cmd on CI's macOS job).
- **Menus** are written header first, items in order, "—" for a separator; "(danger)" an enabled item
  in `VcDangerBrush`, "(disabled: "…")" an item in `VcTextDisabledBrush` with that tooltip,
  "(highlighted)" the item with the focus and `VcHoverBackgroundBrush`, which is always the first
  enabled item when a menu opens (D105). A menu's height is 8 + 24 + 26 × items + 9 × separators
  (phase 3's 4a): with phase 4's items a commit menu is **284** (9 items, 2 separators), a
  several-commits menu **145** (4, 1), a local branch with an upstream that is not checked out
  **458** (15, 4), one without an upstream **380** (12, 4), the current branch with an upstream
  **354** (11, 4), a remote branch **293** (9, 3), a drop onto the current branch **171** (5, 1),
  any other drop with rebase items **145** (4, 1), a drop of a remote branch onto the current branch
  **110** (3, 0). A test asserts the height (±2) and that the menu lies at least 4 px inside the
  window, and none of part A's steps states its position (phase 3's rule: a menu that would cross
  the bottom opens above the pointer and is then moved to stay inside).
- **Resting the pointer** on a menu item moves it there in one move (the real window: outside the
  menu, 2 px beyond its nearest edge, to the item's vertical centre, then sideways), waits until the
  item's tooltip is open (`ToolTip.GetIsOpen`, the default limit) and settles once; a disabled item
  takes no highlight. **Before a capture with no menu open** both harnesses move the pointer to the
  neutral point (760, 687) (phase 3's conventions). A capture with a menu open leaves the pointer
  where the click that opened the menu was.
- **Waiting for an operation**: `WaitForIdleAsync` (its refresh included), and the queue's log line
  `<repo>: <Title>: <outcome> after <N> ms: <message>` (outcome `Succeeded`, `Conflicts` or
  `Failed`; a failure's line, at Warning, ends with git's first output line). "No dialog" and "no
  question" are asserted after that, as phase 3's "Absence": no dialog is open
  (`MainWindowViewModel.Dialogs.IsOpen` false). A rebase asks nothing before it runs (D135).
- **The call log** (`IGitCallLog`) is where a check reads which git commands ran, with their exit
  codes and output; "the call log has `git rebase <sha>`" means exactly that argument list.
- **Commits the app rewrites** keep their author and author date and get the commit date
  2026-01-02 09:00 UTC; "Test Author" <author@example.com> is author and committer (D72). The Date
  column shows the author date; `git log --date-order` orders by commit date, so a rewritten commit
  is drawn above older-dated ones. A commit the app rewrites is named by its subject and by the id
  plain git gives for it (the table below).
- **Backup refs** are `refs/visualcommit/backup/rebase-<UTC time>/<branch>` (design 1.2) and
  `refs/visualcommit/backup/skip-<UTC time>` with `skip-<UTC time>-changes` (5.1). A check asserts
  `git for-each-ref --format=%(refname) %(objectname) refs/visualcommit/backup` (the exact set of
  refs), each ref's kind and target, and that its time parses (`Phase4Expectations.BackupRef.Parse`,
  through `Phase4Checks.BackupsAsync`), never the time itself. Every operation here leaves at most
  three backups, so the clean-up (section 9) removes nothing and no `Removed backup` line is logged.
- **Git's own texts** below (the call log's lines, `ErrorOutput`) are Git 2.36.0.windows.1's and are
  asserted word for word only when `git --version` is 2.36; on another git a check asserts the line
  marked "key" and that `ErrorOutput` is `GitOutputCleaner.Clean` of the failed call's own output
  (phase 3's rule). Refs, parents, trees, files, the rebase folder and `git status` are asserted on
  every version.
- **Dates** are shown in UTC (`VISUALCOMMIT_TIME_ZONE=UTC`, D43).

#### Scenarios and graphs of part A

**"rebase"** (`Scenarios.RebaseAsync`, folder and tab `rebase`), as pinned by
`Phase4ScenarioTests`. Files: README.md "# Rebase\n"; src/core.txt "core\n" ("core\nmain work\n" on
main); topic/one.txt "topic one\n", topic/two.txt "topic two\n" (feature/topic); docs/main.txt
"main more\n" (main); other/one.txt "other one\n" (feature/other). A bare `origin` (`origin.git`
next to `rebase`, a folder remote) has `main` and `feature/topic`; `main` tracks `origin/main` and
`feature/topic` tracks `origin/feature/topic`, both 0 ahead and 0 behind; `feature/other` has no
upstream. HEAD is on a clean `feature/topic`, so `{cur}` is `feature/topic`. Full SHAs: Initial
commit 7bce6bf8d12d1b2fac2dae78ef358ce48d0df22c, Add core 996fdc12d7467abcae213421b92ad016c5ed26f9,
Topic one 1219e45225ea55ad2c7a0e9412adaeeffd07a43c, Topic two
b0dfa3f405dffff59bea3f4f434b16e5490cc597, Main work dd6a6fc8095ffa05dc783ce011e322a7241551a4, Main
more 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146, Other one 2d4d79a03112c8a5e2f9a783318d588df519af5d.

**R0, "rebase" as built.** 3 lanes, Graph column 64: at 1100×700 the columns are Branch / Tag,
Graph, Message (126) and Date.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `feature/other` | 0, 0 | Other one | 2026-01-01 12:06 | `2d4d79a` |
| 1 | `main`, `origin/main` | 1, 1 | Main more | 2026-01-01 12:05 | `1dd58b8` |
| 2 | | 1, 1 | Main work | 2026-01-01 12:04 | `dd6a6fc` |
| 3 | `feature/topic` ✓, `origin/feature/topic` | 2, 2 | Topic two | 2026-01-01 12:03 | `b0dfa3f` |
| 4 | | 2, 2 | Topic one | 2026-01-01 12:02 | `1219e45` |
| 5 | | 0, 0 (lanes 1 and 2 end here) | Add core | 2026-01-01 12:01 | `996fdc1` |
| 6 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `7bce6bf` |

Lines: lane 0 (colour 0) runs from row 0 to row 6; lane 1 (colour 1) from row 1's node through row
2 into row 5's node; lane 2 (colour 2) from row 3's node through row 4 into row 5's node. HEAD's
linear first-parent line (design 2.1, `CommitGraphData.LinearLineFromHead`) is Topic two (depth 0),
Topic one (1), Add core (2), Initial commit (3); the tips HEAD reaches (`TipsOnHead`) are those of
`feature/topic` and `origin/feature/topic`.

Left panel when the repository opens (a folder that holds HEAD's branch starts open):

```
▾ Local branches          3
    ▾ feature
        other
        topic                (accent colour, no arrows)
      main
▾ Remotes                 2
    ▾ origin
        ▸ feature
          main
```

then Pull requests 0, Tags 0 and Stashes 0, drawn as phase 3's "sync" draws its empty sections.

The status bar's branch (`CurrentBranch`) reads `feature/topic`; nothing is selected and the details
panel shows "Select a commit to see its details.".

**R1, after "Rebase feature/topic onto main" (A2 part A).** 3 lanes.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `feature/topic` ✓ | 0, 0 | Topic two | 2026-01-01 12:03 | `5f71ab8` |
| 1 | | 0, 0 | Topic one | 2026-01-01 12:02 | `befae26` |
| 2 | `feature/other` | 1, 1 | Other one | 2026-01-01 12:06 | `2d4d79a` |
| 3 | `main`, `origin/main` | 0, 0 | Main more | 2026-01-01 12:05 | `1dd58b8` |
| 4 | | 0, 0 | Main work | 2026-01-01 12:04 | `dd6a6fc` |
| 5 | `origin/feature/topic` | 2, 2 | Topic two | 2026-01-01 12:03 | `b0dfa3f` |
| 6 | | 2, 2 | Topic one | 2026-01-01 12:02 | `1219e45` |
| 7 | | 0, 0 (lanes 1 and 2 end here) | Add core | 2026-01-01 12:01 | `996fdc1` |
| 8 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `7bce6bf` |

Lines: lane 0 (colour 0) runs from row 0 to row 8; lane 1 (colour 1) from row 2's node into row 7's
node; lane 2 (colour 2) from row 5's node through row 6 into row 7's node. The old Topic one and
Topic two stay in the graph because `origin/feature/topic` still reaches them. Left panel: `topic`
↑4 ↓2.

**R2, after "Rebase feature/topic onto this commit" on Main work (A2 part B).** 3 lanes.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `feature/topic` ✓ | 0, 0 | Topic two | 2026-01-01 12:03 | `09b923a` |
| 1 | | 0, 0 | Topic one | 2026-01-01 12:02 | `c09318c` |
| 2 | `feature/other` | 1, 1 | Other one | 2026-01-01 12:06 | `2d4d79a` |
| 3 | `main`, `origin/main` | 2, 2 | Main more | 2026-01-01 12:05 | `1dd58b8` |
| 4 | | 0, 0 (lane 2 ends here) | Main work | 2026-01-01 12:04 | `dd6a6fc` |
| 5 | `origin/feature/topic` | 2, 3 | Topic two | 2026-01-01 12:03 | `b0dfa3f` |
| 6 | | 2, 3 | Topic one | 2026-01-01 12:02 | `1219e45` |
| 7 | | 0, 0 (lanes 1 and 2 end here) | Add core | 2026-01-01 12:01 | `996fdc1` |
| 8 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `7bce6bf` |

Lines: lane 0 (colour 0) runs from row 0 to row 8; lane 1 (colour 1) from row 2's node into row 7's
node; lane 2 (colour 2) from row 3's node into row 4's node; lane 2 again (colour 3) from row 5's
node through row 6 into row 7's node. Left panel: `topic` ↑3 ↓2.

**R3, after "Check out feature/other and rebase it onto main" (A3).** 2 lanes, Graph column 48.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `feature/other` ✓ | 0, 0 | Other one | 2026-01-01 12:06 | `14e607b` |
| 1 | `main`, `origin/main` | 0, 0 | Main more | 2026-01-01 12:05 | `1dd58b8` |
| 2 | | 0, 0 | Main work | 2026-01-01 12:04 | `dd6a6fc` |
| 3 | `feature/topic`, `origin/feature/topic` | 1, 1 | Topic two | 2026-01-01 12:03 | `b0dfa3f` |
| 4 | | 1, 1 | Topic one | 2026-01-01 12:02 | `1219e45` |
| 5 | | 0, 0 (lane 1 ends here) | Add core | 2026-01-01 12:01 | `996fdc1` |
| 6 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `7bce6bf` |

Lines: lane 0 (colour 0) runs from row 0 to row 6; lane 1 (colour 1) from row 3's node through row
4 into row 5's node. The old Other one (`2d4d79a`) is reached by no ref but the backup and has left
the graph. Left panel: `other` in the accent colour, no arrows; `topic` not accented, no arrows.

**"sync"** (phase 3's `Scenarios.RemoteSyncAsync`, folder and tab `sync`, `origin` the folder
`origin.git`: a file remote, no server) as phase 3's report gives it: HEAD on `main` at Shared
work; `main` tracks `origin/main` (0, 0), `published` tracks `origin/published` (1 ahead), `topic`
has no upstream; its clock stands at 12:05. Full SHAs: Initial commit
6688774dc2d9715638c7c8ef1c976c97ad44d0c5, Shared work a7f2cfd9a076c4c746e13caaa60f4fa2f0d76a4c,
Published work 8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6, Unpushed work
8e653159ca5e460a187117d51b3f18bdf81f5c96 (src/published.txt "published\nunpushed\n"), Topic work
77d782ebfce02f1fe2b29c2ee2b6b43a1f682a02. A4 changes it before the app starts, in its own order
(any other order or content gives other SHAs):

- A4 part A: `PushFromElsewhereAsync(sync, "main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere
  work")` (12:05, `b2e9a9e`, pinned; `origin`'s `main` only: the copy has not fetched it), then
  `CommitFileAsync("src/local.txt", "local\n", "Local work")` on `main` (12:06, `f4f8c06`).
- A4 parts B and C: `CheckoutAsync("published")`, then `PushFromElsewhereAsync(sync, "published",
  "src/published.txt", "published\nelsewhere\n", "Published elsewhere")` (12:05, `899ce97`;
  `origin`'s `published` only).

**S1, A4 part A when the app starts.** 3 lanes.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Local work | 2026-01-01 12:06 | `f4f8c06` |
| 1 | `topic` | 1, 1 | Topic work | 2026-01-01 12:04 | `77d782e` |
| 2 | `published` | 2, 2 | Unpushed work | 2026-01-01 12:03 | `8e65315` |
| 3 | `origin/published` | 2, 2 | Published work | 2026-01-01 12:02 | `8c6f545` |
| 4 | `origin/main` | 0, 0 (lanes 1 and 2 end here) | Shared work | 2026-01-01 12:01 | `a7f2cfd` |
| 5 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6688774` |

Lines: lane 0 (colour 0) runs from row 0 to row 5; lane 1 (colour 1) from row 1's node into row 4's
node; lane 2 (colour 2) from row 2's node through row 3 into row 4's node. Left panel: Local
branches 3: `main` (accent) ↑1, `published` ↑1, `topic`; Remotes 2: `origin` (open) with `main` and
`published`; Pull requests 0, Tags 0, Stashes 0. `git status -sb`: `## main...origin/main [ahead 1]`.

**S2, after "Pull from origin/main (rebase)" (A4 part A).** 3 lanes.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Local work | 2026-01-01 12:06 | `8a1f07b` |
| 1 | `origin/main` | 0, 0 | Elsewhere work | 2026-01-01 12:05 | `b2e9a9e` |
| 2 | `topic` | 1, 1 | Topic work | 2026-01-01 12:04 | `77d782e` |
| 3 | `published` | 2, 2 | Unpushed work | 2026-01-01 12:03 | `8e65315` |
| 4 | `origin/published` | 2, 2 | Published work | 2026-01-01 12:02 | `8c6f545` |
| 5 | | 0, 0 (lanes 1 and 2 end here) | Shared work | 2026-01-01 12:01 | `a7f2cfd` |
| 6 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6688774` |

Lines: lane 0 (colour 0) runs from row 0 to row 6; lane 1 (colour 1) from row 2's node into row 5's
node; lane 2 (colour 2) from row 3's node through row 4 into row 5's node. The old Local work
(`f4f8c06`) has left the graph. Left panel: `main` ↑1 (no ↓).

**S3, A4 parts B and C when the app starts.** 2 lanes (the pinned sync graph, HEAD on `published`).

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `topic` | 0, 0 | Topic work | 2026-01-01 12:04 | `77d782e` |
| 1 | `published` ✓ | 1, 1 | Unpushed work | 2026-01-01 12:03 | `8e65315` |
| 2 | `origin/published` | 1, 1 | Published work | 2026-01-01 12:02 | `8c6f545` |
| 3 | `main`, `origin/main` | 0, 0 (lane 1 ends here) | Shared work | 2026-01-01 12:01 | `a7f2cfd` |
| 4 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6688774` |

Lines: lane 0 (colour 0) runs from row 0 to row 4; lane 1 (colour 1) from row 1's node through row
2 into row 3's node. Left panel: `main` (no arrows), `published` (accent) ↑1, `topic`.
`CurrentBranch` `published`; `git status -sb`: `## published...origin/published [ahead 1]`.

**S4, A4 parts B and C while the pull's rebase is stopped.** 3 lanes; row 0 is the working-changes
row, "Working changes · 1 conflict" (cut with "…" in the 126-wide Message column, the whole text in
the graph's tooltip), its ring in lane 0, colour 0 (the lane of HEAD's commit), its dashed line down
lane 0 to the node of row 1.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 1 | the filled `HEAD`, `origin/published` | 0, 0 | Published elsewhere | 2026-01-01 12:05 | `899ce97` |
| 2 | `topic` | 1, 1 | Topic work | 2026-01-01 12:04 | `77d782e` |
| 3 | `published` (no check mark) | 2, 2 | Unpushed work | 2026-01-01 12:03 | `8e65315` |
| 4 | | 0, 0 (lane 2 ends here) | Published work | 2026-01-01 12:02 | `8c6f545` |
| 5 | `main`, `origin/main` | 0, 0 (lane 1 ends here) | Shared work | 2026-01-01 12:01 | `a7f2cfd` |
| 6 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6688774` |

Lines: lane 0 (colour 0) runs from row 1 to row 6; lane 1 (colour 1) from row 2's node into row 5's
node; lane 2 (colour 2) from row 3's node into row 4's node. `CurrentBranch` reads "Detached at
899ce97". Left panel: no local branch is accented (HEAD is detached); `published` ↑1 ↓1 (its tip
`8e65315` against `origin/published`'s `899ce97`); `main` and `topic` no arrows.

**S5, after Continue (A4 part B).** 2 lanes.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `published` ✓ | 0, 0 | Unpushed work | 2026-01-01 12:03 | `cd8de5c` |
| 1 | `origin/published` | 0, 0 | Published elsewhere | 2026-01-01 12:05 | `899ce97` |
| 2 | `topic` | 1, 1 | Topic work | 2026-01-01 12:04 | `77d782e` |
| 3 | | 0, 0 | Published work | 2026-01-01 12:02 | `8c6f545` |
| 4 | `main`, `origin/main` | 0, 0 (lane 1 ends here) | Shared work | 2026-01-01 12:01 | `a7f2cfd` |
| 5 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6688774` |

Lines: lane 0 (colour 0) runs from row 0 to row 5; lane 1 (colour 1) from row 2's node into row 4's
node. Left panel: `published` (accent) ↑1.

**S6, after Skip (A4 part C).** 2 lanes.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `published` ✓, `origin/published` | 0, 0 | Published elsewhere | 2026-01-01 12:05 | `899ce97` |
| 1 | `topic` | 1, 1 | Topic work | 2026-01-01 12:04 | `77d782e` |
| 2 | | 0, 0 | Published work | 2026-01-01 12:02 | `8c6f545` |
| 3 | `main`, `origin/main` | 0, 0 (lane 1 ends here) | Shared work | 2026-01-01 12:01 | `a7f2cfd` |
| 4 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6688774` |

Lines: lane 0 (colour 0) runs from row 0 to row 4; lane 1 (colour 1) from row 1's node into row 3's
node. Left panel: `published` (accent), no arrows.

**Commits the checks make.** Plain git, given the same parents, tree, message, identity and dates,
makes these; the checks expect them (full ids for the tests).

| Made in | Commit | Message | Parent | Author date | Commit date |
|---|---|---|---|---|---|
| A2 part A | `befae26` (befae2677a3a259f2c9423912af47575e319c852) | "Topic one" | `1dd58b8` | 2026-01-01 12:02 | 2026-01-02 09:00 |
| A2 part A | `5f71ab8` (5f71ab8bad51a15fdfdfd19fc1b0084ef6e956c5) | "Topic two" | `befae26` | 2026-01-01 12:03 | 2026-01-02 09:00 |
| A2 part B | `c09318c` (c09318c0e69377cb1150a1ebb1f19c6d4af2dd61) | "Topic one" | `dd6a6fc` | 2026-01-01 12:02 | 2026-01-02 09:00 |
| A2 part B | `09b923a` (09b923a8b8da60f7ba4f86e6d4cab9221a78326e) | "Topic two" | `c09318c` | 2026-01-01 12:03 | 2026-01-02 09:00 |
| A3 | `14e607b` (14e607bf248c8917ab93680c48cba392f60546ff) | "Other one" | `1dd58b8` | 2026-01-01 12:06 | 2026-01-02 09:00 |
| A4 part A, before the app starts | `b2e9a9e` (b2e9a9e3724a696d2565eaa632dc7b04bca768a0, pinned) | "Elsewhere work" (adds src/elsewhere.txt "elsewhere\n") | `a7f2cfd` | 2026-01-01 12:05 | 2026-01-01 12:05 |
| A4 part A, before the app starts | `f4f8c06` (f4f8c060b1e68a3e560113b942f1f984c6b5d4f0) | "Local work" (adds src/local.txt "local\n") | `a7f2cfd` | 2026-01-01 12:06 | 2026-01-01 12:06 |
| A4 part A | `8a1f07b` (8a1f07b0ca2c177d76be50610feaafb872a34863) | "Local work" | `b2e9a9e` | 2026-01-01 12:06 | 2026-01-02 09:00 |
| A4 parts B and C, before the app starts | `899ce97` (899ce97113fa266c09b5a8ab4025f6d78568c497) | "Published elsewhere" (src/published.txt "published\nelsewhere\n") | `8c6f545` | 2026-01-01 12:05 | 2026-01-01 12:05 |
| A4 part B | `cd8de5c` (cd8de5c655a6ad19814e84d96c6d83bb00a2c9eb) | "Unpushed work" (src/published.txt "published\nelsewhere\nunpushed\n") | `899ce97` | 2026-01-01 12:03 | 2026-01-02 09:00 |

#### Texts these checks rely on (part A)

**Menu items and their disabled reasons** (design 1.1, 2.1, 3, 6, 7.1, 10.4; `{cur}` what HEAD is
on, `HEAD` when detached; the singular for one):

| Menu | New items and where | Disabled reasons, in order |
|---|---|---|
| A commit | after "Revert this commit": `Rebase {cur} onto this commit`, `Interactive rebase of {cur} from here…` | Rebase: `This commit is already on {cur}` (reachable from HEAD); `Check out a branch to rebase`; `Make the first commit first`; `Finish or abort the {kind} first` / `Resolve the conflicts first`. From here: `{cur} has no commits after this one` (HEAD's commit); `This commit is not on {cur}`; `A merge lies between this commit and {cur}`; then the rebase's last three |
| Several commits | first: `Squash {N} commits…` | `Merge commits cannot be squashed`; `Only commits of {cur} can be squashed`; `Select commits that follow one another`; `A merge lies between these commits and {cur}`; `The first commit cannot be squashed`; `Check out a branch to squash`; `Finish or abort the {kind} first`; `Resolve the conflicts first` |
| A local branch `{b}`, not checked out | after the two merges, before "Fast-forward {b} to {up}": `Rebase {cur} onto {b}`, `Interactive rebase of {cur} onto {b}…` | Rebase: `{cur} is up to date with {b}` (HEAD reaches {b}'s tip); then 1.1's 3 to 5. Interactive: 1.1's 3 to 5 only |
| A remote branch `{r}/{b}` | after the two merges: `Rebase {cur} onto {r}/{b}`, `Interactive rebase of {cur} onto {r}/{b}…` | as for a local branch, with `{r}/{b}` |
| The current branch with an upstream `{up}` | right after "Pull from {up}": `Pull from {up} (rebase)` | as the toolbar's Pull (phase 3's 4d) |
| Drop: the current branch A onto local B | a group after phase 3's: `Rebase {A} onto {B}`, `Interactive rebase of {A} onto {B}…` | Rebase: `{A} is up to date with {B}` (`DropFacts.SourceContainsTarget`); then 1.1's 3 to 5 |
| Drop: local A, not current, onto local B (current or not) | a group after phase 3's: `Check out {A} and rebase it onto {B}`, `Check out {A} and rebase it onto {B} interactively…` | `{A} is up to date with {B}` (not the interactive item); `{A} is checked out in another worktree ({folder})`; then 1.1's 4 and 5 (not 3: a detached HEAD does not disable these items, which check a branch out first, 14.3) |
| Drop: local A onto a remote branch `{r}/{x}` | after the two push items: `Rebase {A} onto {r}/{x}`, `Interactive rebase of {A} onto {r}/{x}…` (the "Check out {A} and …" pair when A is not current) | as above |
| Drop: a remote branch onto a local branch | none: phase 3's items only | |

**Operations** (design 10.1; `{b}` the rebased branch, `{target}` a branch's short name, `{r}/{b}`
or a commit's short SHA):

| Operation | Title | Running text | Error title | Failure summary |
|---|---|---|---|---|
| Rebase onto | `Rebase {b} onto {target}` | `Rebasing {b} onto {target}…` | `Rebase failed` | `Rebase {b} onto {target} failed.`; for local changes `Git rebases only a clean working tree: commit or stash your changes first.`; for an untracked file in the way `Untracked files are in the way: move or remove them first.` |
| Check out and rebase | `Check out {A} and rebase it onto {B}` | `Checking out {A}…`, then `Rebasing {A} onto {B}…` | `Checkout failed` or `Rebase failed` | phase 3's checkout summary, or the rebase's |
| Pull with rebase | `Pull (rebase)` | phase 3's `Pulling {up} into {b}…` | `Pull failed` | phase 3's `Git could not pull {up} into {b}.` |
| Continue | `Continue the rebase` | `Continuing the rebase…` | `Continue failed` | `Git could not continue the rebase.` |
| Skip | `Skip {short}` | `Skipping {short}…` | `Skip failed` | `Git could not skip {short}.` |

**Status bar** (10.2, phase 3's pull texts): `Rebased {b} onto {target}`; `Pulled and rebased {b}
onto {up}` (phase 3's `PulledRebased`, the text of a pull whose rebase ran: 10.2, 14.3); `Pull
stopped: {N} files have conflicts` (phase 3's, warning colour: every stop of a pull's rebase is
`PullOutcome.Conflicts`, section 6); after Continue or Skip ends the rebase `Rebased {b} onto
{onto}`; a failure `<Title> failed` in `VcDangerBrush`; a stage-panel operation leaves `Ready`.

**The banner of a rebase stop** (5.2, 10.3, 10.4; `{onto}` is a local branch at `onto` if there is
one, else a remote branch, else the short SHA, 14.3): text `Rebasing {b} onto {onto}: stopped at
{short} "{subject}" ({k} of {n}).` followed by ` {N} files have conflicts.` or, once nothing
conflicts and something is staged, ` Conflicts resolved: continue to commit it and go on.`; buttons
left to right "Continue" (`BannerContinueButton`), "Skip" (`BannerSkipButton`), "Abort rebase"
(`BannerAbortButton`), `VcActionButton`, 26 high, 6 apart, 8 from the right edge, a disabled one
with its reason as its tooltip; Continue's reason while files conflict `Resolve the conflicts first
({N} files)`. They stand beside the text only while the text column keeps at least 240 px (5.2): in
the 440-wide graph area at 1100×700, with the banner's 36-px icon column, three buttons leave it
less, so they stand on a row of their own under the text, right-aligned, 8 above and below. The
tooltip of the icon and text (design 10.3's "icon tooltip"; it is set on `BannerInfo`, the border
over the icon and text columns, read with `ToolTip.GetTip(BannerInfo)`): `Continue commits the
resolved files and replays the rest. Skip leaves this commit out. Abort puts {b} back where it was
before the rebase.` The stage panel at such a stop (5.3): `CommitButton` reads `Continue rebase`,
enabled as Continue is and with its reason as tooltip; `CommitSummary` holds
`rebase-merge/message`'s first line; `AmendCheckBox` is disabled with `Finish or abort the rebase
first`; a conflicted row's tooltip is `Conflict: click to resolve it`.

**The Skip question** (7.2): `ConfirmTitle` `Skip {short}?`; `ConfirmQuestion` `"{subject}" is left
out of the rebase, and changes made to resolve its conflicts are lost.`; `ConfirmDetail` `A backup is
saved first, so this can be undone.`; "Cancel" (`CancelButton`, with the focus) and "Skip"
(`ConfirmButton`, danger).

**Git's texts** (Git 2.36.0.windows.1; "/" separates lines, ⇥ is a tab):

- **TA1** (`git rebase <sha>` with an unstaged change to a tracked file, exit 1, standard error;
  standard output empty): `error: cannot rebase: You have unstaged changes.` (key) / `error: Please
  commit or stash them.`. With a staged change git says `error: cannot rebase: Your index contains
  uncommitted changes.` and the same second line (no check here shows it).
- **TA2** (`git rebase <sha>` with an untracked file where the target has one, exit 1, standard
  error; standard output empty): `error: The following untracked working tree files would be
  overwritten by checkout:` (key) / `⇥docs/main.txt` / `Please move or remove them before you switch
  branches.` / `Aborting` / `error: could not detach HEAD`.
- **TA3** (a clean `git rebase <sha>`, exit 0, standard error, after the runner's split at `\r` and
  the removal of `ESC[K`): `Rebasing (1/2)`, `Rebasing (2/2)`, `Successfully rebased and updated
  refs/heads/<b>.` (one `Rebasing (1/1)` for one commit).
- **TA4** (the pull's `git rebase` that stops, exit 1): standard output `Auto-merging
  src/published.txt` / `CONFLICT (content): Merge conflict in src/published.txt`; standard error
  `Rebasing (1/1)` / `error: could not apply 8e65315... Unpushed work` (git separates these two with
  a carriage return, so the runner's split and `GitOutputCleaner.Clean` make them two lines) / four
  `hint:` lines / `Could not apply 8e65315... Unpushed work`. No dialog shows it (D95).

### Part B: the interactive rebase and squash

This part covers checks B6 to B9 of design 13.4: the interactive rebase's list opened from a
commit's menu (B6), editing it and starting it (B7: a reorder by drag, a fixup, a squash with an
edited message, a reword, a drop), squashing two selected commits (B8), and leaving a changed list
(B9). It was written before the UI it tests, from `docs/phase-4-design.md` 2.1 to 2.5, 3, 7.1,
7.2, 7.4, 10.1 to 10.4 and 13.4, decisions D141 and D143, and the code WP0 and WP1 merged
(`RebaseTodo`, `GitRepository.Rebase.cs`, `MessageDialogViewModel`, `EscapeOrder`). Every git state
below was produced by plain Git 2.36.0.windows.1 on replicas of `Scenarios.InteractiveAsync` whose
SHAs equal those `Phase4ScenarioTests` pins, each operation run as design 2.4, 2.5 and 3 say the
app runs it, with the app's environment ("How the expected git states were derived").

#### Conventions of part B

- **Rows.** Graph rows are numbered from 0 at the top, as in phase 3's report; no check of this part
  has a working-changes row or a banner, so graph row r has its vertical centre at window y
  129 + 26 × r at 1100×700 (rows from y 116, 26 high). "Right-click the row X in its Message column"
  is phase 3's: window x 468 (the Message column starts at 260 + 130 + 48 = 438 with one lane, plus
  `MessageClickInset` 30). The list's rows are named by their commit's subject ("the row Fix one").
- **A list row's point** is its vertical centre at window x 600: in the subject column, on no
  button, at every indent. "Click the row X" and "right-click the row X" mean a click there. A click
  selects the row and gives it the keyboard focus; a right-click selects the row and opens its menu,
  and a click on a row's action button selects the row and opens its menu (14.3). The checks still
  select a row first (a click, or the arrow keys) before they drag it, right-click it or click its
  action button, so that both harnesses reach each menu from the same state. A row is never
  left-pressed at the point of the left-click just before it: Avalonia would count the press as a
  double-click. Before a drag the row is therefore selected with the keys. A right-click right after
  a click at the same point is phase 3's (check 4 step 9): it opens the row's menu and selects
  nothing more.
- **Finding rows.** The scripted run reads the list through WP5's test surface
  (`InteractiveRebaseView.Rows`: each row's commit, action, indent, opacity, strike-through, second
  line and bounds; the drag line's gap) and drags a row in steps (`ShellDriver.Phase4.Rebase.cs`).
  The real-window pass finds `InteractiveRebaseView`, `RebaseRow` (named `{short} {subject}`, top to
  bottom), `RebaseActionButton`, `RebaseRowSecondLine`, `RebaseEditMessageButton`, `RebaseBaseRow`,
  `RebaseListHeader`, `RebaseListCount`, `RebaseCancelButton` and `RebaseStartButton` through UI
  Automation (design 7.4); it clicks graph rows at the constants' points (D49).
- **A row's action button** is the `RebaseActionButton` inside that row; it is not focusable (2.2), so
  a click on it leaves the keyboard focus where it was. A menu item is clicked by its label (phase 3's
  `ClickMenuItem`).
- **Keys and typing.** "Select all" in a text box is the platform's select-all (the command modifier
  and A: Ctrl+A on Windows and Linux, Cmd+A on macOS); "type" types the given text as keys. "Press
  S", "press D", "press R" mean the letter key without a modifier. Ctrl+click is a click with the
  platform's command modifier held (D88; `ShellDriver.CommandModifier`, Cmd on macOS;
  `VirtualKeyShort.CONTROL` in the real window). Shift+F10 opens the focused element's context menu,
  once per press (D106).
- **Menus** are written as phase 3's report writes them; a menu that fits the window is 8 + 24 + 26
  × items + 9 × separators high (±2) and lies at least 4 px inside the window. A menu opened by a
  right-click has its top-left corner 2 right of and 2 below the pointer (phase 3's 4a). The menu
  that a list row's action button, Shift+F10 or the Menu key opens has its top-left corner at the
  bottom-left corner of the row's action button (14.3). Esc closes a menu and gives the focus back
  to the element focused before it opened (phase 3's 4a).
- **Absence** ("no dialog", "no menu", "no question") is asserted as phase 3's report says, once the
  tab is idle after the input. "Git unchanged" means: `refs/heads/main` at `57f089d`
  (57f089d442eea23fee65c387f560002ab1ccd1fc), `git status --short --branch` prints exactly `## main`,
  `git for-each-ref refs/visualcommit/` prints nothing, and there is no `rebase-merge/` or
  `rebase-apply/` in `.git`.
- **The keyboard focus** is asserted only where the design fixes it: on the list's top row when the
  list opens, on a row after a click on it, on the graph when the list closes (2.2), on a dialog's
  first box or its cancel button (7.2, phase 3), and back on the element focused before after a menu
  closes (phase 3's 4a); back on the list's selected row when a diff over the list closes, and
  unchanged by a click on the base row, which changes nothing (14.3).
- **After an operation** the scripted run waits for the queue's log line
  `interactive: <Title>: Succeeded after <N> ms: <status text>`, the real window for the same line
  (`RealApp.OperationEnded`).
- **Backups.** A rebase the app starts first saves `refs/visualcommit/backup/rebase-<time>/main`
  pointing at the branch's old tip (design 1.2). A check asserts the kind, that `<time>` parses as
  D67's time, and what the ref points at, never the time itself. The clean-up that follows the
  operation (design 9) removes nothing here: the backup is new and the only one.
- **The pointer before a capture** is phase 3's: the neutral point (760, 687), unless the step rests
  it on something or a menu is open (then it stays where it clicked). During a drag it stays where
  the drag is.
- **Git's own texts.** No picture of this part shows git's output: every expected text is the app's,
  asserted word for word on every git version. Git's states (SHAs, messages, dates, trees, refs) are
  asserted on every version: they do not depend on git's version, since the dates are fixed (D72).
- **One test per app start**: `Check_B6_interactive_list`, `Check_B7_edit_list_and_start`,
  `Check_B8_squash_two_commits`, `Check_B9_leave_changed_list` and `Check_B9_head_moved_while_open`
  (B9's part B starts the app again).

#### The scenario and its graphs (part B)

**"interactive"** (`Scenarios.InteractiveAsync`, tab and log name `interactive`): one line of history
on `main`, no remote, no tag, no stash, HEAD on a clean `main`; the copy's clock stands at 12:08.
Every commit is by "Test Author" <author@example.com>, author and committer date the time shown.

| Commit | Full SHA | Changes | Message as git keeps it |
|---|---|---|---|
| Initial commit | da32da349cff2210ec951624b25f62825dd435ec | A README.md `# Interactive` | `Initial commit` |
| Base | 4b4622c8ca70c5cfe305043f40ea99c288fbefb3 | A src/base.txt `base` | `Base` |
| One | fc6c8fd1a2ee786e8602d9a0ca51ec84f48bfa7b | A src/one.txt `one` | `One` |
| Two | 938844a7a995d3769fbac6d4e9ff4a3095f15898 | A src/two.txt `two` | `Two` |
| Fix one | ed58902397c0b9512ef667d9c4299a2cc8757906 | M src/one.txt `one`, `fixed` | `Fix one` |
| Three | ff42e7fddcaf41bcac67a9bd8990e3cb6f9d4ae3 | A src/three.txt `three` | `Three`, a blank line, `#42 is the issue` |
| Four | eef5c1549f0e0f729bf7e63734e97eeb0988ba3c | A src/four.txt `four` | `Four` |
| Five | 57f089d442eea23fee65c387f560002ab1ccd1fc | A src/five.txt `five` | `Five` |

(Each file's lines end in a line feed; each message ends in one line feed.)

The graphs below are laid out by the lane rules (D47, D48, D54, D55): the commits come in the order
of `git log --date-order --branches --remotes --tags HEAD`; each is a single chain of first parents
with no second parent, so every commit takes lane 0, the lane the first commit started, and keeps its
colour 0 (`#7C8CFF` dark); lane 0 runs from the top row to the bottom row. One lane makes the Graph
column 48 wide (16 × 1 + 16, at least 48), so at 1100×700 the graph area (440) shows Branch / Tag
(130), Graph (48), Message (142) and Date (120). Labels follow phase 1's order: the branch HEAD is on,
with its check mark. Dates are author dates in UTC (D43). A backup ref is not loaded by the graph
(D47 names `--branches --remotes --tags HEAD` only), so commits only a backup reaches are not shown.

**G-I, the scenario as built** (B6, B7 before Start, B8 before the squash, B9):

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Five | 2026-01-01 12:07 | `57f089d` |
| 1 | | 0, 0 | Four | 2026-01-01 12:06 | `eef5c15` |
| 2 | | 0, 0 | Three | 2026-01-01 12:05 | `ff42e7f` |
| 3 | | 0, 0 | Fix one | 2026-01-01 12:04 | `ed58902` |
| 4 | | 0, 0 | Two | 2026-01-01 12:03 | `938844a` |
| 5 | | 0, 0 | One | 2026-01-01 12:02 | `fc6c8fd` |
| 6 | | 0, 0 | Base | 2026-01-01 12:01 | `4b4622c` |
| 7 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `da32da3` |

**G-B7, after B7's Start:**

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Three and four | 2026-01-01 12:05 | `a081bff` |
| 1 | | 0, 0 | Two reworded | 2026-01-01 12:03 | `b3507d3` |
| 2 | | 0, 0 | One | 2026-01-01 12:02 | `ab4b49d` |
| 3 | | 0, 0 | Base | 2026-01-01 12:01 | `4b4622c` |
| 4 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `da32da3` |

**G-B8, after B8's squash:**

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Five | 2026-01-01 12:07 | `eb6f702` |
| 1 | | 0, 0 | Three | 2026-01-01 12:05 | `f34248d` |
| 2 | | 0, 0 | Fix one | 2026-01-01 12:04 | `ed58902` |
| 3 | | 0, 0 | Two | 2026-01-01 12:03 | `938844a` |
| 4 | | 0, 0 | One | 2026-01-01 12:02 | `fc6c8fd` |
| 5 | | 0, 0 | Base | 2026-01-01 12:01 | `4b4622c` |
| 6 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `da32da3` |

**G-B9, after B9 part B's commit made outside the app:** G-I with one more row on top: row 0 `main` ✓,
lane 0, colour 0, "Six", 2026-01-01 12:08, `c3cc217`; then G-I's rows 0 to 7 as rows 1
to 8 ("Five" without a label).

**Commits the checks make.** Plain git, run as the app runs it, makes these (full SHAs for the tests
to compare); each is by "Test Author" <author@example.com>:

| Made in | Commit | Message, exactly | Author date | Commit date | Parent | Its changes |
|---|---|---|---|---|---|---|
| B7 | `ab4b49d` (ab4b49d005eb36535c58e54b01db091946b74b2e) | `One` + LF | 2026-01-01 12:02 | 2026-01-02 09:00 | `4b4622c` | A src/one.txt, holding `one`, `fixed` (Fix one folded in) |
| B7 | `b3507d3` (b3507d37adfe56baf58cf29ec0912ff444225b8c) | `Two reworded` + LF | 2026-01-01 12:03 | 2026-01-02 09:00 | `ab4b49d` | A src/two.txt |
| B7 | `a081bff` (a081bffd034ab3d4fda4e1c073fe510a24f7c0d7) | `Three and four`, LF, LF, `#42 is the issue`, LF | 2026-01-01 12:05 | 2026-01-02 09:00 | `b3507d3` | A src/four.txt, A src/three.txt |
| B8 | `f34248d` (f34248d062c44eadf27e78a47435b55161bf75d7) | `Three`, LF, LF, `#42 is the issue`, LF, LF, `Four`, LF | 2026-01-01 12:05 | 2026-01-02 09:00 | `ed58902` | A src/four.txt, A src/three.txt |
| B8 | `eb6f702` (eb6f702889d08c7879c1daba704553fea072a42c) | `Five` + LF | 2026-01-01 12:07 | 2026-01-02 09:00 | `f34248d` | A src/five.txt |
| B9 part B, through `TempRepo.CommitFileAsync` | `c3cc217` (c3cc217816ade59e0b8ccf262d83050f491ba812) | `Six` + LF | 2026-01-01 12:08 | 2026-01-01 12:08 | `57f089d` | A src/six.txt `six` |

A rebase keeps each commit's author date and gives every commit it writes the harness's commit date
(D72, design 0.5); the details panel therefore shows a "Commit date" row, 2026-01-02 09:00, for every
commit B7 and B8 make, and none for the scenario's own commits.

#### The list's geometry at 1100×700 (design 2.2)

The list (`InteractiveRebaseView`) takes the graph area's place, the graph's column headers included:
window x 260 to 700 (440 wide), y 88 to 674. The left panel, the right panel (400) and the status
bar stay.

- **Header**, y 88 to 124 (`RebaseListHeader`): the text on the left, `{N} commits`
  (`RebaseListCount`) on the right; the two do not overlap, and a text cut with "…" has its whole
  text as tooltip.
- **Rows**, 30 high from y 124, newest on top. In each row (window x): the drag handle column 260 to
  284 with `IconGrip` (14, `VcTextSecondaryBrush` `#9BA3B0`, centred in the column); the action
  button (`VcActionButton`, 84 × 22) from x 288, holding the action's name and a 10 × 10 chevron on
  its right, inside the row's 30; the short SHA from x 380 (64 wide, code font 12, `#9BA3B0`); the
  subject from x 452, cut with "…" and with its whole text as tooltip when it does not fit. No
  author column: the list is 440 wide, less than 520. A squash or fixup row's whole content (its
  grip, button, SHA and subject) stands 16 further right (the grip column from x 276, the button
  from 304, the SHA from 396, the subject from 468), and a 2-px line in `VcLane0Brush` (`#7C8CFF`)
  at window x 272 runs from its vertical centre to the top edge of its target row (2.2, 14.3). A
  dropped row's SHA and subject are struck through in `VcTextDisabledBrush` (`#5F6775`). The
  selected row has `VcSelectionBrush` (`#2A3150`).
- **A second line**, 22 high (`RebaseRowSecondLine`), right under a row whose message will change,
  with `Edit…` (`RebaseEditMessageButton`) at its right; the rows below move down 22.
- **The base row**, 30 high (`RebaseBaseRow`), right under the last commit row: `Base` in
  `#9BA3B0`, the short SHA `4b4622c` and the subject `Base`; not selectable.
- **Footer**, y 630 to 674: `Cancel` (`RebaseCancelButton`) and `Start rebase`
  (`RebaseStartButton`, `VcPrimaryButton`), right-aligned, 8 apart, `Start rebase`'s right edge at
  x 688 (12 from the list's right edge).

**L0, the list as it opens on Base** (B6, B7, B9):

| Row of the list | y (window) | Action | SHA | Subject | `RebaseRow` name |
|---|---|---|---|---|---|
| Five (selected, with the keyboard focus) | 124 to 154 | Pick | `57f089d` | Five | `57f089d Five` |
| Four | 154 to 184 | Pick | `eef5c15` | Four | `eef5c15 Four` |
| Three | 184 to 214 | Pick | `ff42e7f` | Three | `ff42e7f Three` |
| Fix one | 214 to 244 | Pick | `ed58902` | Fix one | `ed58902 Fix one` |
| Two | 244 to 274 | Pick | `938844a` | Two | `938844a Two` |
| One | 274 to 304 | Pick | `fc6c8fd` | One | `fc6c8fd One` |
| the base row | 304 to 334 | — | `4b4622c` | Base | |

These are the commits git's own todo holds for `git rebase -i 4b4622c` (`pick fc6c8fd One` …
`pick 57f089d Five`, oldest first), shown newest first. Header `Interactive rebase of main from
4b4622c`, count `6 commits`. `Start rebase` is disabled (tooltip `Change an action or the order
first`), `Cancel` enabled.

**L7, the list after B7's edits** (step 9, at its screenshot):

| Row of the list | y (window) | Action button | Indent (the whole row) | Drawn |
|---|---|---|---|---|
| Five (selected) | 124 to 154 | Drop | none | `57f089d` and "Five" struck through in `#5F6775` |
| Four | 154 to 184 | Squash | 16 | a line at x 272 from y 169 (its centre) to y 184 (Three's top edge) |
| Three | 184 to 214 | Pick | none | |
| its second line | 214 to 236 | | | `Message: Three and four`, `Edit…` |
| Two | 236 to 266 | Reword | none | |
| its second line | 266 to 288 | | | `New message: Two reworded`, `Edit…` |
| Fix one | 288 to 318 | Fixup | 16 | a line at x 272 from y 303 (its centre) to y 318 (One's top edge) |
| One | 318 to 348 | Pick | none | no second line (a group of fixups only keeps One's message) |
| the base row | 348 to 378 | | | `Base`, `4b4622c`, `Base` |

#### Texts these checks rely on (part B)

**Base's commit menu** (design 7.1, B6, B7, B9): header `4b4622c Base`; "Check out this commit"
(highlighted); "Create branch here…"; "Create tag here…"; —; "Cherry-pick onto main" (disabled:
"This commit is already on main"); "Revert this commit"; "Rebase main onto this commit" (disabled:
"This commit is already on main", design 1.1 reason 1); "Interactive rebase of main from here…";
"Reset main to this commit…"; —; "Copy SHA". 9 items and 2 separators: 284 high (part A's commit
menu). "From here" is enabled: Base is on HEAD's linear first-parent line and is not HEAD's own
commit (2.1).

**The several-commits menu of Four and Three** (B8): header `2 commits`; "Squash 2 commits…"
(highlighted); "Cherry-pick 2 commits onto main" (disabled: "Some of these commits are already on
main"); "Revert 2 commits"; —; "Copy SHAs". 145 high. Squash is enabled: the two commits are a chain
on HEAD's linear line (Four's first parent is Three), neither is a merge or the root, HEAD is on
`main`, nothing is in progress (design 3).

**A list row's menu** (2.2, the same from its action button, as its context menu and from
Shift+F10): header `{short} {subject}`, then "Pick", "Reword…", "Squash", "Fixup", "Drop" without
the row's current action, no separator: 4 items, 136 high. Opened by the action button, Shift+F10 or
the Menu key, its top-left corner is at the bottom-left corner of the row's action button; opened by
a right-click, 2 right of and 2 below the pointer (14.3, phase 3's 4a). On the bottom commit row
"Squash" and "Fixup" are disabled with `The oldest commit has nothing below it to join`. The action
button shows the action's name: `Pick`, `Reword` (a reworded row's, 14.3), `Squash`, `Fixup`,
`Drop`.

**The list's texts**: header `Interactive rebase of main from 4b4622c`, count `6 commits` (the
listed rows, whatever their actions, 14.3); second lines `Message: {subject}` (the target of a group
with a squash) and `New message: {subject}` (a reworded row); `Edit…`; `Cancel`, `Start rebase`;
Start's disabled reasons `Change an action or the order first` and `HEAD moved: open the list again`
(10.4).

**The message dialog** (`MessageDialog`, 7.2): the wide card (460) over the backdrop; title
(`MessageDialogTitle`) `Message of the squashed commit` (button `Save`), `Reword 938844a` (`Save`) or
`Squash 2 commits` (`Squash`); no "In …" line (one tab); `MessageSummary` with the keyboard focus and
the caret at its end; `MessageSummaryCounter` = 72 less the summary's length; `MessageDescription`;
`Cancel` (`CancelButton`) and the confirm button (`ConfirmButton`, primary), disabled with the tooltip
`Write a summary` while the summary is blank. It is filled by splitting the message: its first line is
the summary, the rest without the blank lines around it the description, no line removed (5.3,
D68). No placeholder text is asserted: design 7.2 gives none.

**The question on leaving a changed list** (7.2; asked by Esc and by `Cancel` alike, only while the
rows' actions or order differ from the list's first state, 14.3): `ConfirmTitle` `Discard the
changes to this list?`, no question text, no detail; `Keep editing` (`CancelButton`, with the
keyboard focus) and `Discard` (`ConfirmButton`, danger).

**Operations** (10.1, 10.2):

| Check | Title | Running text | Status when done (secondary) | Log line |
|---|---|---|---|---|
| B7 | `Interactive rebase of main` | `Rebasing main…` | `Rebased main: 3 commits` | `interactive: Interactive rebase of main: Succeeded after <N> ms: Rebased main: 3 commits` |
| B8 | `Squash 2 commits` | `Squashing 2 commits…` | `Squashed 2 commits into f34248d` | `interactive: Squash 2 commits: Succeeded after <N> ms: Squashed 2 commits into f34248d` |

"3 commits" counts the commits on `main` after the list's base (`git rev-list --count
4b4622c..main` after the rebase prints 3). `f34248d` is the squashed commit, which is
`NewTip~(3 − 2)` (the list from Three's parent holds Three, Four and Five; two were squashed).

**The details panel** shows a commit as phase 1 and 3 do: the subject, the body under it, the author
`Test Author <author@example.com>`, Date, a "Commit date" row only when it differs from Date, Parents,
and "Changed files (N)" with each file's status letter, name and folder. Two or more selected commits
show `MultiSelectionView`: `2 commits selected`, the commits in row order (`eef5c15 Four`, `ff42e7f
Three`) and the hint `Right-click a selected commit for what can be done with all of them.`.

### Part C: conflicts, Continue, Skip and Abort, the resolver

Checks C10 to C13 of design 13.4 cover the banner's Continue, Skip and Abort (design 5.1, 5.2,
10.3) for a merge, a rebase and a list of picks; the conflict resolver on text conflicts (11.1 to
11.7, 11.10): its layout, titles and panes, a choice per block, the platform's undo, typing, Mark
resolved; Continue from the banner and from the stage panel (5.3, 5.4); Skip with its question and
backup (5.1, 7.2); Abort of a rebase, of a list of picks and of a "Check out … and rebase it" (1.3,
phase 3's 2.15); and git's list for several picks (section 4, D136). Checks C14 to C17 cover editing
the result freely, with the questions that guard it, the resolver's menus, conflicts without text,
the external merge tool and a rebase started in a terminal (2.2, 5.1 to 5.4, 7.1 to 7.4, 10, 11;
D140, D141, D144, D148, D149). Every git state below was derived with Git 2.36.0.windows.1 on
replicas of the scenario repositories whose SHAs equal those `Phase4ScenarioTests` pins, each
operation run as the design says the app runs it, in the app's environment (D72: author and
committer date 2026-01-02 09:00 UTC, the identity from the repository's configuration), and each
step "outside the app" as `TempRepo` runs it; see "How the expected git states were derived". The
graphs are laid out by D48, D54 and D55, as `GraphLayout` does.

#### Test methods and app starts (part C)

| Check | Test method (one app start each) | Scenario as the check starts it | Window, theme |
|---|---|---|---|
| C10 | `Check_C10_merge_conflict_end_to_end` | `ConflictsStoppedAsync(ConflictsStop.Merge)` | 1100×700, dark |
| C11 | `Check_C11_rebase_with_two_conflicting_commits` | `ConflictsAsync()`, then `TempRepo.CheckoutAsync("feature/rebase")` before the app starts | 1100×700, dark |
| C12 | `Check_C12_cherry_pick_list_stopped_and_continued` | `ConflictsAsync()` | 1100×700, dark |
| C13 | `Check_C13_skip_then_abort_a_rebase` | `ConflictsStoppedAsync(ConflictsStop.Rebase)` | 1100×700, dark |
| C13 | `Check_C13_abort_a_pick_list` | `ConflictsStoppedAsync(ConflictsStop.CherryPicks)` | 1100×700, dark |
| C13 | `Check_C13_abort_check_out_and_rebase` | `ConflictsAsync()` | 1100×700, dark |
| C14 | `Check_C14_editing_freely` | `ConflictsStoppedAsync(ConflictsStop.Merge)` | 1100×700, dark |
| C15 | `Check_C15_conflicts_without_text` | `ConflictsStoppedAsync(ConflictsStop.MergeKinds)` | 1100×700, dark |
| C16 | `Check_C16_merge_tool` | `ConflictsStoppedAsync(ConflictsStop.Merge, mergeTool: true)` | 1100×700, dark |
| C17 | `Check_C17_terminal_rebase` | `ConflictsAsync()`; the rebase is started outside the app while the app is open | 1100×700, dark |

#### Conventions of checks C10 to C13

- **Defaults.** Every check runs at 1100×700 in the dark theme. Each app start writes a session
  with one tab on a fresh copy of the named scenario, starts the app and waits for the repository
  (`Phase4Checks.OpenAsync`: graph drawn, status shown, queue idle), the pointer at the neutral
  point. After a click that starts an operation the check waits until the queue is idle and its
  refresh is done (`WaitForIdleAsync`), with `OperationChainTimeout` (60 s) for the operations
  that run a chain of git calls: a rebase, a list of picks, Continue, Skip and the "Check out … and
  rebase it". Each app start is one test method, named in the check's steps. No step waits a fixed
  time.
- **After a tooltip.** After a capture that keeps the pointer on a tooltip, the next step first
  moves the pointer to the neutral point (phase 3's check 9): the tooltip, open below the pointer,
  can lie over what the next click needs (the banner's tooltip lies over the working-changes row at
  1100×700).
- **Who makes each stop.** C10 starts from `ConflictsStoppedAsync(ConflictsStop.Merge)`: the merge
  was started in a terminal, so its theirs marker reads `feature/theirs`, as design 11.2's sketch
  shows (a merge the app starts passes the full ref, and git then writes
  `>>>>>>> refs/heads/feature/theirs`: phase 3's report, part B; phase 3's check 9 covers the
  app's own merge that stops). C11 rebases from the app's branch menu, so that the rebase's backup
  exists. C12 picks the list from the app's several-commits menu. C13 uses two terminal stops
  (`ConflictsStop.Rebase` and `ConflictsStop.CherryPicks`, 12:10) and the app's own "Check out
  feature/rebase and rebase it onto main". The conflicts scenario has no remote, no tag and no
  stash.
- **Dates.** Commits the app writes get the committer date 2026-01-02 09:00. A rebased or picked
  commit keeps its author date, and so does the commit the app makes at a stopped pick (git takes
  the author from `CHERRY_PICK_HEAD`, also with the app's `GIT_AUTHOR_DATE` set) or at a stopped
  rebase (the app passes `rebase-merge/author-script`'s date, design 5.4). The merge commit of
  C10 is new: author and committer date 2026-01-02 09:00. The graph's Date column shows the author
  date; with 4 or more lanes it is hidden at 1100×700 (phase 3's "Window sizes": 440 − 130 − 80 −
  120 < 120), so the dates below are as git and the details panel show them, the commit date in
  brackets where it differs.
- **Graphs** are written as phase 3's: rows from the top, labels in phase 1's order (a detached
  `HEAD` first, then the branch HEAD is on with ✓, then the other local branches by name), node
  lane and colour number (dark: 0 `#7C8CFF`, 1 `#3FB97F`, 2 `#E0A23B`, 3 `#E5606B`, 4 `#4FB3D9`).
  When the working-changes row shows it is display row 0 and every commit row moves down by one;
  its ring is in lane 0, colour 0, with a dashed line to the node of HEAD's commit right below it.
  While a banner shows, rows are found from the graph control's bounds (`ShellDriver.GraphRowPoint`,
  `GraphLabelBounds`), never from fixed positions (D122).
- **The banner** (`ConflictBanner`) is described by its text (`BannerText`), its buttons left to
  right (Continue `BannerContinueButton` labelled `Continue`, Skip `BannerSkipButton` labelled
  `Skip`, Abort `BannerAbortButton` with phase 3's label), each shown or not, enabled or not, a
  disabled one with its reason as its tooltip (`ToolTip.ShowOnDisabled`), and the layout rule of
  5.2 (D144, amending D122): the buttons 26 high, 6 apart, the last 8 from the banner's right edge,
  beside the text while the text keeps at least 240 px, otherwise on a row of their own under it,
  right-aligned. The banner is as wide as the graph area (440) while the graph shows, and as wide
  as the resolver (840) while the resolver is open ("across the resolver's width", 11.2). 5.2's
  rule fixes where the buttons stand in every banner of this part. While the graph shows (440 wide)
  they cannot stand beside the text: with phase 3's icon (12 + 16 + 8), its text margin (8) and
  its button padding (10 each side, 1-px border), `Continue` and `Abort merge` alone would leave
  the text about 211 px, `Continue`, `Skip` and `Abort rebase` about 155, and `Continue`, `Skip`
  and `Abort cherry-pick` about 127, all under 240 (see "How the expected git states … were
  derived"). They are on a row of their own under the text, right-aligned, so the banner takes more
  than one line (`AssertBanner(…, oneLine: false, placement: BannerButtonPlacement.OwnRow)`). While
  the resolver is open (840 wide) they stand beside the text (`placement: BesideText`); whether
  the text takes one line there depends on measured text widths and is not stated (`oneLine:
  null`). Always: no text cut, nothing overlapping. Its background is
  `VcBannerBackgroundBrush` (`#33291A`) with a 1-px `VcWarningBrush` line along its bottom; its
  icon and text share the tooltip of 10.3.
- **The resolver** (`ConflictResolver`, 11.2) is described by:
  - where it is: over the graph and the right panel's columns, x 260 to 1100 (840 wide) with the
    session's default widths; the left panel and `LeftSplitter` stay; the right panel's views and
    `RightSplitter` are hidden; the banner above it;
  - its header (36 high): `ResolverFileStatus` `U` in `VcDangerBrush` (`#E5606B`),
    `ResolverFileName`, `ResolverFolder` (empty for notes.txt, at the working tree's top),
    `ResolverKind`, `ResolverFilePosition` with `ResolverPreviousFile`, `ResolverNextFile`, and
    `ResolverCloseButton` at the right;
  - its toolbar (36 high): `ResolverConflictPosition` with `ResolverPreviousConflict` and
    `ResolverNextConflict`, `Take ours` (`ResolverTakeOurs`), `Take theirs` (`ResolverTakeTheirs`),
    `Merge tool…` (`ResolverMergeTool`; disabled in every check of this part with `Set merge.tool
    in git's configuration to use a merge tool`, asserted on its tip: only C16's copy configures a
    tool), and at the right `Mark resolved` (`ResolverMarkResolved`, `VcPrimaryButton`);
  - the top half: the ours pane (`ConflictOurs`, left) and the theirs pane (`ConflictTheirs`,
    right), each half the width with a 1-px `VcBorderBrush` line between, each under a 26-high
    title strip (`ConflictOursTitle`, `ConflictTheirsTitle`); the bottom half: the result
    (`ConflictResult`) under its strip, `Result` (`ConflictResultTitle`) and at the right the count
    (`ConflictResultCount`); the halves split the height equally with a 1-px line between; the text
    is the code font at 12 (about 13 rows of text per half at this size, 11.2); one number column,
    no sign column.
  The pane rows are written as tables: a numbered row is a line of the pane (its own numbering), a
  "bar" row is a block bar (26 high) with its buttons: in the ours pane `Use ours`
  (`ConflictUseOurs`) and `Use both` (`ConflictUseBothOursFirst`), in the theirs pane `Use theirs`
  (`ConflictUseTheirs`) and `Use both` (`ConflictUseBothTheirsFirst`); after a choice a bar shows
  `Used in the result` or `Not used` (`ConflictBarState`, secondary text) instead. Every block of
  this part has one line per side, so the panes have no filler rows and line up row for row.
  - **Tints** (7.3, dark): an unresolved block's ours rows in the ours pane and its ours section in
    the result `VcConflictOursBrush` `#15323A`; its theirs rows and section `VcConflictTheirsBrush`
    `#2C2140`; the result's marker lines `VcConflictMarkerBrush` `#3A2E16` with their text in
    `VcWarningBrush` `#E0A23B`; a side used in the result `VcConflictUsedBrush` `#1A2E24` (its rows
    in the panes); a side not used loses its tint. The result tints no chosen line (7.3, 14.3).
    Scripted: `ConflictResultEditor.TintAt(line)` and the panes' rows
    (`ConflictPanes.RowsOf(side)`); the pictures are inspected for the same.
  - **The result's text** is asserted as a whole (`ConflictResultEditor.Text` scripted, the Value
    pattern in the real window), written below as numbered lines, each ended by LF as git wrote the
    file. The caret is asserted as a line and column (`CaretPosition`); it does not blink (D151).
    "Modified" is `IsModified`.
  - **`Conflict {k} of {m}`**: `m` is the number of blocks of the file as the resolver read it and
    `k` the block last reached (on opening, by the conflict arrows, Alt+Up or Alt+Down; a choice
    does not move on); with no block left the toolbar says `No conflicts left` (11.2, 14.3). The
    checks assert it when the resolver opens, after the next-conflict button, and after each choice
    and undo.
  - **A bar's button** is clicked through `ConflictPanes.BarButton(block, side, choice)` in the
    scripted run, and in the real window by its automation id among the bars in view, block 1's
    being the upper one.
  - **Reaching a block** (opening, the conflict arrows) puts the caret at the start of the block's
    `<<<<<<<` line in the result and scrolls each top pane so that the block's bar is the third
    visible row, and the result so that the `<` line is, each with fewer rows above it near the
    start and stopping at its end (11.5); the checks assert that the block's bars and its lines in
    the result are in view, not which row is first, except when the resolver opens on block 1: then
    nothing scrolls (fewer rows above it near the start), so each pane's first row is `line 1` and
    the result's first line is line 1 (C10 step 5).
- **Mark resolved** (11.7) asks nothing in this part: no check leaves a block in the result, and
  nothing changes the file on disk while the resolver is open. It writes the result to notes.txt
  (LF, as read) and runs `git add -- notes.txt` in one operation `Resolve notes.txt` (status
  `Resolved notes.txt`, secondary), then closes the resolver (each stop has one conflicted file):
  the graph, the right panel and `RightSplitter` come back, and the keyboard focus goes to the
  banner's Continue (`BannerContinueButton`), which is shown and enabled after every Mark resolved
  of this part (11.1).
- **Continue, Skip and Abort** run as phase 3's local operations do, with design 10.1's and 10.2's
  texts (in "Texts", below). Before Continue runs, the app checks the staged paths that were
  conflicted at the stop for markers (`git diff --cached --check -- notes.txt`); in this part it
  finds none, so Continue asks nothing. A stop opens no dialog (D95); its status text is in the
  warning colour.
- **Dialogs** are phase 3's confirmation (`ConfirmTitle`, `ConfirmQuestion`, `ConfirmDetail`, the
  shared `CancelButton` at the left with the keyboard focus, `ConfirmButton` at the right).
- **Backups** are asserted with `git for-each-ref refs/visualcommit/backup` (`Phase4Checks`'s
  backup list): each ref's kind, that its time parses (D67's format), and what it points at; a
  `-changes` snapshot by its parents and its notes.txt, never by id or message. Neither the graph
  nor the left panel shows them.
- **The selection after an operation** stays on the commits it had, by SHA (phase 3's 4h), with
  1.5's rules: a rebase that ends rebased selects the branch's new tip when one of the branch's
  commits was selected, and a selected working-changes row that goes with a clean tree leaves
  nothing selected (14.3). A check that needs a commit's details clicks its row.
- **Git's own lines** quoted in this part (`CONFLICT (content): …`, `Successfully rebased …`) are
  never on screen: no check of this part opens an error dialog. They are asserted on the call log's
  records only when `git --version` is 2.36; git's states (refs, parents, files, the in-progress
  files) on every version.

#### Conventions of checks C14 to C17 and D18

The conventions of every part (the report's first section) and those of phase 3's report (its part
C's "Defaults", its part D's "Waiting" and "Absence") apply. These are added:

- **Start.** Each test writes a session with one tab on a fresh copy of its scenario, starts the app
  at the size and theme above, and waits until the graph has drawn its rows and the queue is idle
  (`WaitForIdleAsync`). "Wait until idle" after a click that starts an operation means the queue is
  idle and its refresh is done, with `OperationChainTimeout` (60 s) for Continue, which runs a chain
  of git calls. The real-window pass waits for the queue's log line
  `<repo>: <Title>: <outcome> after <N> ms: <message>` instead.
- **Outside the app** means a git call or a file write through `TempRepo`, as a terminal would make
  it (`GitAsync`, `GitAllowingFailureAsync` for a call that exits 1, `WriteFile`). It uses the copy's
  clock (12:10 for every copy of "conflicts"), which such a call does not move. A wait for the change it
  makes polls the condition the step names, word for word, through the app's file watcher, with
  phase 3's 30-second limit for outside changes. No step waits a fixed time.
- **The pointer before a capture** goes to the neutral point (760, 687) at both sizes, except before
  a capture about a tooltip and before a capture with a menu open, where it stays where the click
  that opened the menu was (phase 3's rule; `c14b-result-menu`, `c14c-pane-menu`, `c16a-file-menu`).
  At 1100×700 that point is in the status bar. At 1920×1080 it is below the interactive list's last
  row, or over the result's text, where nothing has a hover background or a tooltip.
- **The working-changes row** while a banner is shown is found from the graph control's bounds
  (`ShellDriver.GraphRowPoint`). In the real window it is the `ConflictBanner` element's bottom plus
  the column headers (28) and half a row (13). Phase 3's D122 rule applies: no fixed y while a
  banner is shown.
- **The banner** (design 5.2) is described by its text, its buttons and where they stand. The banner
  is as wide as the graph area while the graph shows (440 at 1100×700). It is as wide as the resolver
  while the resolver is open (840 at 1100×700, 1660 at 1920×1080).
  - Two or three buttons (`Continue` with `Abort merge`, or with `Skip` and `Abort rebase`) leave the
    text column less than 240 px at 440 (at most 219, or 211 inside the text's 8-px right margin), so
    the buttons stand on a row of their own **below the text**, right-aligned.
  - At 840 and 1660 they stand **beside the text** (the merge banner's text column is at least 610
    at 840).
  - At 440 the buttons on a row of their own make the banner more than one line (`oneLine: false,
    placement: OwnRow`, as in checks C10 to C13). At 840 whether the text wraps is not asserted
    (`oneLine: null, placement: BesideText`). Everywhere the whole text shows, nothing is cut with
    "…" and nothing overlaps.
  - At 1660 the merge banner takes one line (`oneLine: true`; by design 5.2 that is the 36-high
    banner, so no height is stated). Phase 3's check 27 asserted the same kind of text on one line in
    the narrower 1260-wide graph area at 1920×1080.
  - A disabled button shows its reason as its tooltip (`ToolTip.ShowOnDisabled`). A reason that no
    step pictures is asserted through `ToolTip.GetTip`.
- **Confirmations** (design 7.2) are phase 3's confirmation dialog: `ConfirmTitle`, `ConfirmQuestion`,
  `ConfirmDetail` (absent where the design gives "—"), `CancelButton` at the left with the keyboard
  focus and the label the design gives (`Cancel` or `Keep editing`), and `ConfirmButton` at the right.
  "(danger)" means the button is drawn in `VcDangerBrush`. "(primary)" means the accent background.
- **The resolver** (design 11.1, 11.2):
  - Once open, it spans the graph and right-panel columns: x 260 to 1100 at 1100×700, x 260 to 1920
    at 1920×1080. The right panel and `RightSplitter` are hidden, the left panel stays, and the banner
    is on top across its width.
  - Header: `ResolverFileStatus` `U` in `VcDangerBrush`, `ResolverFileName`, `ResolverFolder` (empty:
    every file of these checks is at the repository's root), `ResolverKind`, `ResolverFilePosition`,
    the previous and next file buttons, and `ResolverCloseButton`.
  - Toolbar: `ResolverConflictPosition` with the previous and next conflict buttons, `ResolverTakeOurs`
    `Take ours`, `ResolverTakeTheirs` `Take theirs`, `ResolverMergeTool` `Merge tool…`, and at the
    right `ResolverMarkResolved` `Mark resolved`, a primary button.
  - **Pane rows** are listed top to bottom. "n `text`" is a text row: the pane's line n. "bar" is a
    block bar (26 high) with its two buttons. "Line n" of a pane means its text row n, never a
    position among the rows. A tint is named by its token. A text row with no tint named has none; a
    bar row is drawn on `VcDiffHunkBrush` (`RowsOf` gives `Bar`).
  - **The result's text** is given line by line, numbered from 1. `ConflictResultEditor.Text` is these
    lines joined by LF, with a final LF.
  - "N lines" counts the result's lines.
  - "Nothing written" means: the working-tree file is byte-identical to what git wrote,
    `git status --porcelain=v1 --untracked-files=all` and `git ls-files -u` are as before, and no
    `Resolve …` operation was logged.
- **Tints** (design 7.3) are read in the scripted run through the test surface: the result's lines
  with `ConflictResultEditor.TintAt(line)`, and the panes' rows with `ConflictPanes.RowsOf(side)`.
  The result tints only an unresolved block's lines: a chosen or typed line has no tint (`None`;
  7.3, 14.3). In D18 the colours of backgrounds and lines are also sampled from the picture.
- **The conflict position** (`Conflict {k} of {m}`): `m` counts the blocks of the file as the
  resolver read it and `k` is the block last reached (on opening, by the conflict arrows, Alt+Up or
  Alt+Down; a choice does not move on); with no block left it reads `No conflicts left` (11.2,
  14.3). After a choice, an edit or an undo it is asserted with the result title's count
  (`ConflictResultCount`), which design 11.5 calls "the truth".
- **The caret.** Reaching a block when the resolver opens puts the caret at the start of the block's
  `<<<<<<<` line; after a choice the caret is at the start of the replaced text, since a choice does
  not move on (11.5, 14.3). Otherwise it is asserted only where typing put it.
- **Clicking a bar's button** uses `ConflictPanes.BarButton(block, side, choice)` in the scripted run.
  In the real window it uses the element with the button's automation id (`ConflictUseOurs`, …). When
  both blocks' bars are in view, two elements share the id, and the pass takes the one whose bounding
  rectangle is higher (block 1's).
- **Clicking in the result at line L, column C** uses `ConflictResultEditor.PointAt(L, C)` in the
  scripted run. In the real window the point comes from the `ConflictResult` element, the result
  editor itself: the Document that holds the Value pattern. The title strip `ConflictResultTitle`
  lies above it and is not part of it.
  - x = its left + 30.4 + 2. 30.4 is the number column for up to 99 lines, 8 + 7.2 × 2 + 8.
  - y = its top + (L − 0.5) × 15.84.
  - The result is at its top then: opening on block 1 scrolls nothing (11.5).
- **The stage panel** is phase 3's: the titles `Unstaged files (N)` or `Unstaged files (N, M with
  conflicts)` and `Staged files (N)`, "No unstaged changes" and "No staged changes", and a conflicted
  row with `U` in `VcDangerBrush`. The conflicted row's tooltip is now `Conflict: click to resolve it`
  (design 5.3). The commit button reads `Stage files to commit` (disabled), `Commit 1 file` or
  `Commit 2 files` during a merge, and `Continue rebase` at a rebase stop. Writes made from the stage
  panel leave the status "Ready".
- **Status** means `OperationStatus`'s text, in the secondary colour unless said otherwise.

#### Texts of checks C10 to C13

From design 5.2, 5.3, 7.1, 7.2, 10.1 to 10.4 and 11.2, and phase 3's code where its texts stay
(`ConflictBannerViewModel.AbortConfirmation`, `OperationTexts.Abort`, `WorkingChangesViewModel`).
Counts use the singular for one (D113).

| Where | Text |
|---|---|
| Banner, merge (C10) | `Merging: "Merge branch 'feature/theirs'". 1 file has conflicts.`; resolved: `Merging: "Merge branch 'feature/theirs'". Conflicts resolved: commit to finish the merge.` (phase 3's) |
| Banner, rebase (C11, C13) | `Rebasing feature/rebase onto main: stopped at 70e8c78 "First change" (1 of 2). 1 file has conflicts.`; resolved: `Rebasing feature/rebase onto main: stopped at 70e8c78 "First change" (1 of 2). Conflicts resolved: continue to commit it and go on.`; second stop: `Rebasing feature/rebase onto main: stopped at 305f7f7 "Second change" (2 of 2). 1 file has conflicts.` (`main` names the rebase's `onto`, da31c97; `(k of n)` counts the pick commands in `done` and `git-rebase-todo`) |
| Banner, a pick in a list (C12, C13) | `Cherry-picking 01fd976 "Pick conflicting". 1 file has conflicts. 1 more commit waits: Continue picks it.`; resolved: `Cherry-picking 01fd976 "Pick conflicting". Conflicts resolved: continue to commit it. 1 more commit waits: Continue picks it.` |
| Banner tooltip (10.3) | merge: `Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.`; rebase: `Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts feature/rebase back where it was before the rebase.`; list: `Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out and goes on. Abort puts main back where it was before the cherry-pick: the commits it already made are dropped too.` |
| Banner buttons (5.1) | merge: `Continue`, `Abort merge` (no Skip); rebase: `Continue`, `Skip`, `Abort rebase`; list: `Continue`, `Skip`, `Abort cherry-pick`. Continue is disabled while a file conflicts: `Resolve the conflicts first (1 file)` (10.4; the design's conventions: `{N} files` means `1 file` for one); Skip is enabled at every stop of this part |
| Stage panel (5.3, phase 3) | titles `Unstaged files (1, 1 with conflicts)`, `Unstaged files (0)` with "No unstaged changes", `Staged files (0)` with "No staged changes", `Staged files (1)`; the conflicted row `U` `notes.txt` (U in the danger colour), tooltip `Conflict: click to resolve it`; commit button during the merge `Stage files to commit` (disabled, nothing staged) then `Commit 1 file`; during a rebase `Continue rebase`, during a list `Continue cherry-pick`, enabled as Continue is, with Continue's reason as tooltip; Amend disabled during a rebase or a list: `Finish or abort the rebase first`, `Finish or abort the cherry-pick first`; the summary filled with `Merge branch 'feature/theirs'` (merge, `MERGE_MSG` less git's block), `First change` then `Second change` (rebase, `rebase-merge/message`), `Pick conflicting` (list), the description empty |
| Resolver header and toolbar | `U`, `notes.txt`, `Both modified`, `File 1 of 1`; `Conflict 1 of 2`, `Conflict 2 of 2`, `Conflict 1 of 1`, `No conflicts left`; `Take ours`, `Take theirs`, `Merge tool…`, `Mark resolved` |
| Resolver titles (11.4) | merge: `Ours: main`, `Theirs: feature/theirs` (from `MERGE_MSG`'s `Merge branch 'feature/theirs'`); rebase: `Ours: main (the new base)`, `Theirs: 70e8c78 First change (your commit)`, at the second stop `Theirs: 305f7f7 Second change (your commit)`; pick: `Ours: main`, `Theirs: 01fd976 Pick conflicting`; result: `Result`, count `2 conflicts left`, `1 conflict left`, `No conflicts left` |
| Bars (11.5) | `Use ours`, `Use both` (ours pane); `Use theirs`, `Use both` (theirs pane); `Used in the result`, `Not used` |
| Skip question (7.2) | title `Skip 70e8c78?`; question `"First change" is left out of the rebase, and changes made to resolve its conflicts are lost.`; detail `A backup is saved first, so this can be undone.`; `Cancel`, `Skip` (danger) |
| Abort questions (phase 3's `AbortConfirmation`, design 1.3, 12.1, 14.3) | a rebase: title `Abort the rebase?`, question `Abort the rebase of feature/rebase? The branch goes back to where it was before it started.`; the same after "Check out feature/rebase and rebase it onto main": `Abort the rebase of feature/rebase? The branch goes back to where it was before it started, and main is checked out again.`; a list with `sequencer/` and HEAD at `abort-safety`: title `Abort the cherry-pick?`, question `Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost. Commits this cherry-pick already made are dropped too.`; every one with the detail `A backup is saved first, so this can be undone.`, `Cancel` and `Abort rebase` or `Abort cherry-pick` (danger) |
| Operations: title / running text / status | `Resolve notes.txt` / `Resolving notes.txt…` / `Resolved notes.txt` (secondary, 14.3); `Continue the merge` / `Continuing the merge…` / `Merge committed`; `Continue the rebase` / `Continuing the rebase…` / at a stop `Rebase stopped: 1 file has conflicts` (warning), at the end `Rebased feature/rebase onto main`; `Continue the cherry-pick` / `Continuing the cherry-pick…` / `Cherry-pick finished`; `Rebase feature/rebase onto main` / `Rebasing feature/rebase onto main…` / `Rebase stopped: 1 file has conflicts` (warning); `Cherry-pick 3 commits` / `Cherry-picking 3 commits…` / `Cherry-picked 1 of 3 commits; stopped at 01fd976 with conflicts in 1 file` (warning); `Skip 70e8c78` / `Skipping 70e8c78…` / at the next stop `Rebase stopped: 1 file has conflicts` (warning); `Check out feature/rebase and rebase it onto main` / `Checking out feature/rebase…`, then `Rebasing feature/rebase onto main…` / `Rebase stopped: 1 file has conflicts` (warning); `Abort the rebase` / `Aborting the rebase…` / `Aborted the rebase`; `Abort the cherry-pick` / `Aborting the cherry-pick…` / `Aborted the cherry-pick` |
| Menus (7.1, phase 3) | several commits: header `3 commits`, `Squash 3 commits…` (disabled: "Only commits of main can be squashed"), `Cherry-pick 3 commits onto main`, `Revert 3 commits`, —, `Copy SHAs`; the drop of `feature/rebase` onto `main`: header `feature/rebase → main`, `Merge feature/rebase into main`, `Merge feature/rebase into main (create a merge commit)`, `Fast-forward main to feature/rebase` (disabled: "main has commits that feature/rebase does not have"), —, `Check out feature/rebase and rebase it onto main`, `Check out feature/rebase and rebase it onto main interactively…` |

#### Texts of checks C14 to C17

The texts are word for word from design 5.2, 7.1, 7.2, 10 and 11, and from phase 3's code where
phase 3's text stays. Counts use the singular for one.

| Where | Text |
|---|---|
| Banner, merge (phase 3) | `Merging: "Merge branch 'feature/theirs'". 1 file has conflicts.`; `Merging: "Merge branch 'feature/kinds'". 3 files have conflicts.` (`2 files have conflicts.`, `1 file has conflicts.`); resolved: `Merging: "Merge branch 'feature/theirs'". Conflicts resolved: commit to finish the merge.` (the same with `feature/kinds`) |
| Banner, rebase (5.2) | `Rebasing feature/picks onto main: stopped at 01fd976 "Pick conflicting" (2 of 3). 1 file has conflicts.`; resolved: `Rebasing feature/picks onto main: stopped at 01fd976 "Pick conflicting" (2 of 3). Conflicts resolved: continue to commit it and go on.` `{onto}` is `main`, the local branch that points at `rebase-merge/onto` (`da31c97`; 14.3). `(2 of 3)` counts the commands that apply a commit: `done` holds two picks and `git-rebase-todo` one |
| Banner buttons | `BannerContinueButton` `Continue`, `BannerSkipButton` `Skip` (rebase only: git has no skip for a merge), `BannerAbortButton` `Abort merge` or `Abort rebase` |
| Continue's disabled reason (10.4) | `Resolve the conflicts first (1 file)`, `(2 files)`, `(3 files)` |
| Banner icon's tooltip (10.3) | merge: `Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.`; rebase: `Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts feature/picks back where it was before the rebase.` |
| Resolver header (11.2) | kinds `Both modified`, `Both added`, `Deleted by them`; `File 1 of 1`, `File 3 of 3`, `File 1 of 2` |
| Resolver toolbar (11.2, 10.4) | `Conflict 1 of 2`, `Conflict 1 of 1`, `No conflicts left`; `Take ours`, `Take theirs`, `Merge tool…`, `Mark resolved`; `Merge tool…` disabled without `merge.tool`: `Set merge.tool in git's configuration to use a merge tool` |
| Titles and count (11.2, 11.4) | `Ours: main`, `Theirs: feature/theirs`, `Theirs: feature/kinds`; `Result` with `2 conflicts left`, `1 conflict left`, `No conflicts left` (`ConflictResultCount`) |
| Bars (11.5) | the ours pane `Use ours` (`ConflictUseOurs`) and `Use both` (`ConflictUseBothOursFirst`); the theirs pane `Use theirs` (`ConflictUseTheirs`) and `Use both` (`ConflictUseBothTheirsFirst`); the states `Used in the result` and `Not used` (`ConflictBarState`, secondary text) |
| Cards (11.8) | UD: `main changed other.txt; feature/kinds deleted it.` with `Keep the file` (`ConflictKeepFile`) and `Delete it` (`ConflictDeleteFile`); binary: `image.png is a binary file: its two versions cannot be merged line by line.` with `Use ours` (`ConflictUseOursFile`) and `Use theirs` (`ConflictUseTheirsFile`). A card that offers nothing in the toolbar keeps the toolbar row, empty, and a snapshot a card saves is not offered by the stage panel's restore bar, which is for discards (14.3) |
| Questions (7.2) | **markers when staging or marking resolved**: `notes.txt still has conflict markers.` / `Mark it resolved anyway? The markers are committed with it.` / — / `Mark resolved` (primary), `Cancel`. **Continue with markers staged**: `1 file still has conflict markers.` / `Continue anyway? The markers are committed with it.` (7.2's one-file form) / — / `Continue` (primary), `Cancel`; a merge's or a single pick's stage-panel Commit asks it too (14.3). **Leaving a modified result**: `Discard your changes to notes.txt?` / `The result has not been marked resolved.` / — / `Discard` (danger), `Keep editing`. **The merge tool with a modified result**: `Discard your changes to notes.txt?` / `The merge tool starts from the file as it is on disk.` / — / `Discard` (danger), `Keep editing` |
| The conflicted file's menu (7.1) | header `notes.txt`; `Resolve conflict`, `Open in merge tool`, `Mark as resolved`, `Discard changes…` (disabled: "Resolve the conflict first"), —, `Ignore notes.txt` (disabled: "Only untracked files can be ignored"), `Ignore all *.txt files` (disabled: "Only untracked files can be ignored"), —, `Open file`, `Show in folder`, `Copy path` |
| The result's and the panes' menus (11.12, 14.3) | the result's: header `notes.txt (result)`; `Undo` (disabled with nothing to undo: "Nothing to undo"); `Redo` (disabled with nothing to redo: "Nothing to redo"); —; `Cut` and `Copy` (disabled without a selection: "Select some text first"); `Paste`; `Select all` (these two always enabled): 6 items and 1 separator, 197 high. A pane's: header its title (`Ours: main`, `Theirs: feature/theirs`); `Copy` (disabled without a selection: "Select some text first"): 58 high |
| Stage panel (5.3) | the conflicted row's tooltip `Conflict: click to resolve it`; the rebase stop's button `Continue rebase`; Amend's (`AmendCheckBox`) reason during a rebase stop `Finish or abort the rebase first` |
| Operations (10.1) and status (10.2; `Resolved {file}` and `Resolved {file} in {tool}` are secondary, 14.3) | `Resolve other.txt` → `Resolved other.txt` (likewise `image.png`, `new.txt`); `Merge tool for notes.txt` (running text `Waiting for fake to close…`) → `Resolved notes.txt in fake`; `Continue the rebase` (`Continuing the rebase…`) → `Rebased feature/picks onto main` |

No git output is shown in a dialog in this part, so no line of git's needs the "two gits" rule.
Git's states (refs, blobs, `git status`, `MERGE_HEAD`, `rebase-merge/`) are asserted on every
version. `git diff --cached --check`'s exact lines (C14) are asserted only on 2.36. On other versions
the check asserts that `ReadStagedConflictMarkersAsync` named notes.txt.

#### The scenario's graphs (checks C10 to C13)

**GC0, "conflicts" as built** (`ConflictsAsync`; HEAD on a clean `main`). 5 lanes: the Graph column
is 96 (16 per lane plus 16), the Message column 214, and the Date column hidden at 1100×700.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Ours edits | 2026-01-01 12:09 | `da31c97` |
| 1 | `feature/kinds` | 1, 1 | Kinds edits | 2026-01-01 12:08 | `3a9de3e` |
| 2 | `feature/picks` | 2, 2 | Pick last | 2026-01-01 12:07 | `5c4c257` |
| 3 | | 2, 2 | Pick conflicting | 2026-01-01 12:06 | `01fd976` |
| 4 | | 2, 2 | Pick clean | 2026-01-01 12:05 | `01d2046` |
| 5 | `feature/rebase` | 3, 3 | Second change | 2026-01-01 12:04 | `305f7f7` |
| 6 | | 3, 3 | First change | 2026-01-01 12:03 | `70e8c78` |
| 7 | `feature/theirs` | 4, 4 | Theirs edits | 2026-01-01 12:02 | `5c77ffb` |
| 8 | | 0, 0 (lanes 1, 2, 3 and 4 end here) | Base | 2026-01-01 12:01 | `195768b` |
| 9 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6bd579f` |

Lines: lane 0 (colour 0) runs from row 0 to row 9; lane 1 (colour 1) from row 1 into row 8's node;
lane 2 (colour 2) from row 2 through rows 3 and 4 into row 8's node; lane 3 (colour 3) from row 5
through row 6 into row 8's node; lane 4 (colour 4) from row 7 into row 8's node. The status bar
shows `main`; Local branches 5 (the closed folder `feature` holding kinds, picks, rebase and
theirs, then `main` in the accent colour).

**GC0r, GC0 with `feature/rebase` checked out** (C11 before its rebase; C13 after step 5): the same
rows, lanes, colours and lines; row 0's label is `main` without a check mark and row 5's is
`feature/rebase` ✓. The status bar shows `feature/rebase`.

**GC-M, the merge stopped** (C10 steps 1 to 11; also C14, C15, C16 and D18's resolver, as "The
scenarios, graphs and files of checks C14 to C17" details it): display row 0 is the
working-changes row, "Working changes · 1 conflict" (in C10 after step 11 "Working changes"), ring
in lane 0, colour 0, its
dashed line into Ours edits' node right below; display rows 1 to 10 are GC0's rows 0 to 9 with the
same labels, lanes and colours. HEAD is on `main` at `da31c97`.

**GC-R1, a rebase stopped with HEAD detached at Ours edits** (C11 steps 2 to 7; C13 steps 1 to 4,
also after the skip; C13 steps 10 and 11): display row 0 is the working-changes row "Working
changes · 1 conflict" (after C11 step 7 "Working changes"), ring in lane 0, colour 0, dashed line
into Ours edits' node; display rows 1 to 10 are GC0's rows with these labels: display row 1 (Ours
edits) the filled label `HEAD`, then `main` without a check mark; display row 6 (Second change)
`feature/rebase` without a check mark; the others as in GC0. Lanes, colours and lines are GC0's. No
local branch is in the accent colour; the status bar shows `Detached at da31c97`.

**GC-R2, C11's second stop** (steps 8 to 10). HEAD is detached at the new First change.

| Display row | Labels | Node: lane, colour | Message | Date (commit date) | SHA |
|---|---|---|---|---|---|
| 0 | | ring 0, 0 | Working changes · 1 conflict | | |
| 1 | `HEAD` | 0, 0 | First change | 2026-01-01 12:03 (2026-01-02 09:00) | `99070f8` |
| 2 | `main` | 0, 0 | Ours edits | 2026-01-01 12:09 | `da31c97` |
| 3 | `feature/kinds` | 1, 1 | Kinds edits | 2026-01-01 12:08 | `3a9de3e` |
| 4 | `feature/picks` | 2, 2 | Pick last | 2026-01-01 12:07 | `5c4c257` |
| 5 | | 2, 2 | Pick conflicting | 2026-01-01 12:06 | `01fd976` |
| 6 | | 2, 2 | Pick clean | 2026-01-01 12:05 | `01d2046` |
| 7 | `feature/rebase` | 3, 3 | Second change | 2026-01-01 12:04 | `305f7f7` |
| 8 | | 3, 3 | First change | 2026-01-01 12:03 | `70e8c78` |
| 9 | `feature/theirs` | 4, 4 | Theirs edits | 2026-01-01 12:02 | `5c77ffb` |
| 10 | | 0, 0 (lanes 1, 2, 3 and 4 end here) | Base | 2026-01-01 12:01 | `195768b` |
| 11 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6bd579f` |

Lines: the dashed line from the ring into row 1's node; lane 0 from row 1 to row 11; lane 1 from
row 3 into row 10's node; lane 2 from row 4 through rows 5 and 6 into row 10's node; lane 3 from
row 7 through row 8 into row 10's node; lane 4 from row 9 into row 10's node. Two rows read "First
change": the new `99070f8` (row 1, newest by commit date) and the original `70e8c78`, which
`feature/rebase` still reaches. The status bar shows `Detached at 99070f8`.

**GC-Rend, C11 after the rebase** (step 11). 4 lanes: Graph column 80, no Date column.

| Row | Labels | Node: lane, colour | Message | Date (commit date) | SHA |
|---|---|---|---|---|---|
| 0 | `feature/rebase` ✓ | 0, 0 | Second change | 2026-01-01 12:04 (2026-01-02 09:00) | `2083808` |
| 1 | | 0, 0 | First change | 2026-01-01 12:03 (2026-01-02 09:00) | `99070f8` |
| 2 | `main` | 0, 0 | Ours edits | 2026-01-01 12:09 | `da31c97` |
| 3 | `feature/kinds` | 1, 1 | Kinds edits | 2026-01-01 12:08 | `3a9de3e` |
| 4 | `feature/picks` | 2, 2 | Pick last | 2026-01-01 12:07 | `5c4c257` |
| 5 | | 2, 2 | Pick conflicting | 2026-01-01 12:06 | `01fd976` |
| 6 | | 2, 2 | Pick clean | 2026-01-01 12:05 | `01d2046` |
| 7 | `feature/theirs` | 3, 3 | Theirs edits | 2026-01-01 12:02 | `5c77ffb` |
| 8 | | 0, 0 (lanes 1, 2 and 3 end here) | Base | 2026-01-01 12:01 | `195768b` |
| 9 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6bd579f` |

Lines: lane 0 from row 0 to row 9; lane 1 from row 3 into row 8's node; lane 2 from row 4 through
rows 5 and 6 into row 8's node; lane 3 from row 7 into row 8's node. The original First change and
Second change are gone (no ref the graph loads reaches them). The status bar shows `feature/rebase`.

**GC10, C10 after Continue** (step 12). 5 lanes.

| Row | Labels | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0, merge (7 across) | Merge branch 'feature/theirs' | 2026-01-02 09:00 | `8a12c69` |
| 1 | | 0, 0 | Ours edits | 2026-01-01 12:09 | `da31c97` |
| 2 | `feature/kinds` | 2, 2 | Kinds edits | 2026-01-01 12:08 | `3a9de3e` |
| 3 | `feature/picks` | 3, 3 | Pick last | 2026-01-01 12:07 | `5c4c257` |
| 4 | | 3, 3 | Pick conflicting | 2026-01-01 12:06 | `01fd976` |
| 5 | | 3, 3 | Pick clean | 2026-01-01 12:05 | `01d2046` |
| 6 | `feature/rebase` | 4, 4 | Second change | 2026-01-01 12:04 | `305f7f7` |
| 7 | | 4, 4 | First change | 2026-01-01 12:03 | `70e8c78` |
| 8 | `feature/theirs` | 1, 1 | Theirs edits | 2026-01-01 12:02 | `5c77ffb` |
| 9 | | 0, 0 (lanes 1, 2, 3 and 4 end here) | Base | 2026-01-01 12:01 | `195768b` |
| 10 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6bd579f` |

Lines: lane 0 from row 0 to row 10; from row 0's node a line goes to lane 1 (colour 1), which runs
past rows 1 to 7 into row 8's node and on into row 9's node; lane 2 (colour 2) from row 2 into row
9's node; lane 3 (colour 3) from row 3 through rows 4 and 5 into row 9's node; lane 4 (colour 4)
from row 6 through row 7 into row 9's node. (The merge's second parent takes lane 1 and colour 1,
so Kinds edits, the next tip, takes lane 2 and colour 2, and so on: D48, D55.)

**GC-P, C12's list stopped** (steps 2 to 7). HEAD is on `main` at the copy of Pick clean.

| Display row | Labels | Node: lane, colour | Message | Date (commit date) | SHA |
|---|---|---|---|---|---|
| 0 | | ring 0, 0 | Working changes · 1 conflict | | |
| 1 | `main` ✓ | 0, 0 | Pick clean | 2026-01-01 12:05 (2026-01-02 09:00) | `25c817e` |
| 2 | | 0, 0 | Ours edits | 2026-01-01 12:09 | `da31c97` |
| 3 | `feature/kinds` | 1, 1 | Kinds edits | 2026-01-01 12:08 | `3a9de3e` |
| 4 | `feature/picks` | 2, 2 | Pick last | 2026-01-01 12:07 | `5c4c257` |
| 5 | | 2, 2 | Pick conflicting | 2026-01-01 12:06 | `01fd976` |
| 6 | | 2, 2 | Pick clean | 2026-01-01 12:05 | `01d2046` |
| 7 | `feature/rebase` | 3, 3 | Second change | 2026-01-01 12:04 | `305f7f7` |
| 8 | | 3, 3 | First change | 2026-01-01 12:03 | `70e8c78` |
| 9 | `feature/theirs` | 4, 4 | Theirs edits | 2026-01-01 12:02 | `5c77ffb` |
| 10 | | 0, 0 (lanes 1, 2, 3 and 4 end here) | Base | 2026-01-01 12:01 | `195768b` |
| 11 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6bd579f` |

Lines: the dashed line from the ring into row 1's node; lane 0 from row 1 to row 11; lane 1 from
row 3 into row 10's node; lane 2 from row 4 through rows 5 and 6 into row 10's node; lane 3 from
row 7 through row 8 into row 10's node; lane 4 from row 9 into row 10's node. After step 7 the
working-changes row reads "Working changes".

**GC-PB, the terminal's list stopped** (C13 steps 6 and 7): GC-P with display row 1 being `d71cdc4`
"Pick clean" (2026-01-01 12:05, commit date 2026-01-01 12:10, as `ConflictsStoppedAsync` makes it)
in place of `25c817e`; every other row, label, lane, colour and line is GC-P's.

**GC-Pend, C12 after Continue** (step 8). 5 lanes.

| Row | Labels | Node: lane, colour | Message | Date (commit date) | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Pick last | 2026-01-01 12:07 (2026-01-02 09:00) | `9d814c4` |
| 1 | | 0, 0 | Pick conflicting, resolved | 2026-01-01 12:06 (2026-01-02 09:00) | `d512905` |
| 2 | | 0, 0 | Pick clean | 2026-01-01 12:05 (2026-01-02 09:00) | `25c817e` |
| 3 | | 0, 0 | Ours edits | 2026-01-01 12:09 | `da31c97` |
| 4 | `feature/kinds` | 1, 1 | Kinds edits | 2026-01-01 12:08 | `3a9de3e` |
| 5 | `feature/picks` | 2, 2 | Pick last | 2026-01-01 12:07 | `5c4c257` |
| 6 | | 2, 2 | Pick conflicting | 2026-01-01 12:06 | `01fd976` |
| 7 | | 2, 2 | Pick clean | 2026-01-01 12:05 | `01d2046` |
| 8 | `feature/rebase` | 3, 3 | Second change | 2026-01-01 12:04 | `305f7f7` |
| 9 | | 3, 3 | First change | 2026-01-01 12:03 | `70e8c78` |
| 10 | `feature/theirs` | 4, 4 | Theirs edits | 2026-01-01 12:02 | `5c77ffb` |
| 11 | | 0, 0 (lanes 1, 2, 3 and 4 end here) | Base | 2026-01-01 12:01 | `195768b` |
| 12 | | 0, 0 | Initial commit | 2026-01-01 12:00 | `6bd579f` |

Lines: lane 0 from row 0 to row 12; lane 1 from row 4 into row 11's node; lane 2 from row 5 through
rows 6 and 7 into row 11's node; lane 3 from row 8 through row 9 into row 11's node; lane 4 from row
10 into row 11's node.

#### Commits the checks make (checks C10 to C13)

Plain git, given the same parents, tree, message, identity and dates, makes these commits; the
checks expect them (full ids for the tests).

| Made in | Commit | Message | Parents | Author date | Commit date |
|---|---|---|---|---|---|
| C10 step 12 (Continue) | `8a12c69` (`8a12c6942e100029b4018deb672c2ea8b11fe54d`) | `Merge branch 'feature/theirs'` | `da31c97`, `5c77ffb` | 2026-01-02 09:00 | 2026-01-02 09:00 |
| C11 step 8 (Continue at the first stop) | `99070f8` (`99070f8227e308c2edf7885287586b947f173eb1`) | `First change` | `da31c97` | 2026-01-01 12:03 | 2026-01-02 09:00 |
| C11 step 11 (Continue at the second stop) | `2083808` (`2083808b1f1a77b23b0b354211dd317f02388cb2`) | `Second change` | `99070f8` | 2026-01-01 12:04 | 2026-01-02 09:00 |
| C12 step 2 (the list's first pick) | `25c817e` (`25c817e0f548bb169036f2bd921a4d01cb8d9f80`) | `Pick clean` | `da31c97` | 2026-01-01 12:05 | 2026-01-02 09:00 |
| C12 step 8 (the stage panel's Continue) | `d512905` (`d5129054a81713f8517a53a815ffd34a351e4a88`) | `Pick conflicting, resolved`, a blank line, `#7 Line 2 keeps main's text.` | `25c817e` | 2026-01-01 12:06 | 2026-01-02 09:00 |
| C12 step 8 (`git cherry-pick --continue`) | `9d814c4` (`9d814c45b70681eef953b6b62d2fc5a05b9de00a`) | `Pick last` | `d512905` | 2026-01-01 12:07 | 2026-01-02 09:00 |
| C13 part B, made through `TempRepo` before the app starts (pinned) | `d71cdc4` (`d71cdc4e9eb7fa80dc917e6f859204a0286e78ac`) | `Pick clean` | `da31c97` | 2026-01-01 12:05 | 2026-01-01 12:10 |

Every one is by Test Author <author@example.com>, author and committer.

#### The conflicted files, the panes and the results (checks C10 to C13)

**FM, notes.txt at the merge's stop** (C10, C14, C16; its blob and git's files under "The files as
git leaves them"; `ConflictsFiles.NotesMergeConflicted`; stages 1 `624b469` Base, 2 `439eb6b` main,
3 `6216606` feature/theirs). As the result shows it when the resolver opens (R0), with its tints:

| Line | Text | Tint |
|---|---|---|
| 1 | `line 1` | |
| 2 | `<<<<<<< HEAD` | marker |
| 3 | `line 2 ours` | ours |
| 4 | `=======` | marker |
| 5 | `line 2 theirs` | theirs |
| 6 | `>>>>>>> feature/theirs` | marker |
| 7 to 9 | `line 3`, `line 4`, `line 5` | |
| 10 | `line 6 theirs` (merged cleanly by git) | |
| 11 to 13 | `line 7`, `line 8`, `line 9` | |
| 14 | `<<<<<<< HEAD` | marker |
| 15 | `line 10 ours` | ours |
| 16 | `=======` | marker |
| 17 | `line 10 theirs` | theirs |
| 18 | `>>>>>>> feature/theirs` | marker |
| 19, 20 | `line 11`, `line 12` | |

**PM, the merge's panes** (C10, C14, C16, D18; 11.3: the file with every block's ours side, and
with every block's theirs side; the common text comes from the working-tree file, so both panes
read `line 6 theirs`):

| Pane row | Ours pane (`Ours: main`) | Theirs pane (`Theirs: feature/theirs`) |
|---|---|---|
| 1 | `line 1` | `line 1` |
| bar (block 1) | `Use ours`, `Use both` | `Use theirs`, `Use both` |
| 2 | `line 2 ours` (ours tint) | `line 2 theirs` (theirs tint) |
| 3 to 5 | `line 3`, `line 4`, `line 5` | the same |
| 6 | `line 6 theirs` | `line 6 theirs` |
| 7 to 9 | `line 7`, `line 8`, `line 9` | the same |
| bar (block 2) | `Use ours`, `Use both` | `Use theirs`, `Use both` |
| 10 | `line 10 ours` (ours tint) | `line 10 theirs` (theirs tint) |
| 11, 12 | `line 11`, `line 12` | the same |

**C10's results**: R1 (after `Use ours` on block 1, and again after the undo), 16 lines: `line 1`,
`line 2 ours`, `line 3`, `line 4`, `line 5`, `line 6 theirs`, `line 7`, `line 8`, `line 9`,
`<<<<<<< HEAD` (10, marker), `line 10 ours` (11, ours), `=======` (12, marker), `line 10 theirs`
(13, theirs), `>>>>>>> feature/theirs` (14, marker), `line 11`, `line 12`. R2 (after `Use theirs` on
block 2), 12 lines, no tint: `line 1`, `line 2 ours`, `line 3`, `line 4`, `line 5`, `line 6
theirs`, `line 7`, `line 8`, `line 9`, `line 10 theirs`, `line 11`, `line 12`. R3 (after `Use both`
from the ours pane on block 2), 13 lines, no tint: R2 with `line 10 ours` then `line 10 theirs` as
lines 10 and 11. R3 is what is committed.

**FR1, notes.txt at the rebase's first stop** (`ConflictsFiles.NotesRebaseFirstConflicted("70e8c78")`;
stages 1 `624b469` Base, 2 `439eb6b` da31c97, 3 `f1c22af` First change): `line 1`, `<<<<<<< HEAD`
(2, marker), `line 2 ours` (3, ours), `=======` (4, marker), `line 2 first` (5, theirs), `>>>>>>>
70e8c78 (First change)` (6, marker), `line 3` to `line 9` (7 to 13), `line 10 ours` (14), `line 11`,
`line 12`: 16 lines. **PR1**: the ours pane (`Ours: main (the new base)`) `line 1`, bar, `line 2 ours`
(ours tint), `line 3` to `line 9`, `line 10 ours`, `line 11`, `line 12`; the theirs pane (`Theirs:
70e8c78 First change (your commit)`) the same rows with `line 2 first` (theirs tint) as row 2 (row
10 is `line 10 ours` in both: First change does not touch line 10). After `Use theirs` (C11 step 6),
12 lines, no tint: `line 1`, `line 2 first`, `line 3` to `line 9`, `line 10 ours`, `line 11`,
`line 12` (`ConflictsFiles.NotesRebaseFirstResolved`).

**FR2, notes.txt at C11's second stop** (`ConflictsFiles.NotesRebaseSecondConflicted("305f7f7")`;
stages 1 `f1c22af` First change, 2 the new First change, 3 Second change): `line 1`, `line 2 first`,
`line 3` to `line 9` (3 to 9), `<<<<<<< HEAD` (10, marker), `line 10 ours` (11, ours), `=======`
(12, marker), `line 10 second` (13, theirs), `>>>>>>> 305f7f7 (Second change)` (14, marker),
`line 11`, `line 12`: 16 lines. **PR2**: the ours pane (`Ours: main (the new base)`) `line 1`, `line 2
first`, `line 3` to `line 9`, bar, `line 10 ours` (ours tint), `line 11`, `line 12`; the theirs pane
(`Theirs: 305f7f7 Second change (your commit)`) the same with `line 10 second` (theirs tint) as row
10. After `Use both` from the theirs pane (C11 step 10), 13 lines, no tint: `line 1`, `line 2 first`,
`line 3` to `line 9`, `line 10 second`, `line 10 ours`, `line 11`, `line 12`.

**FS2, notes.txt at the second stop after skipping First change** (C13 step 3; stages 1 `f1c22af`
First change, 2 `439eb6b` da31c97, 3 `72a0708` Second change): `line 1`, `line 2 ours` (First change
is left out, and main's line 2 stays), `line 3` to `line 9`, `<<<<<<< HEAD`, `line 10 ours`,
`=======`, `line 10 second`, `>>>>>>> 305f7f7 (Second change)`, `line 11`, `line 12`: 16 lines.

**FP, notes.txt at the list's stop** (`ConflictsFiles.NotesPicksConflicted("01fd976")`; stages 1
`624b469` Base, 2 `439eb6b` main, 3 `9aa4b99` Pick conflicting): `line 1`, `<<<<<<< HEAD` (2,
marker), `line 2 ours` (3, ours), `=======` (4, marker), `line 2 picked` (5, theirs), `>>>>>>> 01fd976
(Pick conflicting)` (6, marker), `line 3` to `line 9`, `line 10 ours`, `line 11`, `line 12`: 16
lines. **PP**: the ours pane (`Ours: main`) `line 1`, bar, `line 2 ours` (ours tint), `line 3` to
`line 9`, `line 10 ours`, `line 11`, `line 12`; the theirs pane (`Theirs: 01fd976 Pick
conflicting`) the same with `line 2 picked` (theirs tint) as row 2. After `Use ours` (C12 step 5),
12 lines, no tint: `ConflictsFiles.NotesOurs` (`line 1`, `line 2 ours`, `line 3` to `line 9`, `line
10 ours`, `line 11`, `line 12`); after the typed edit (step 6) line 2 is `line 2 ours and picked`.

#### The scenarios, graphs and files of checks C14 to C17

**"conflicts"** (`ConflictsAsync`; SHAs as pinned). It has no remote, so the left panel shows
Remotes 0, Tags 0 and Stashes 0. The files at "Base" (`195768b`) are notes.txt (`line 1` to `line 12`),
other.txt `other file`, and image.png (32×32 indigo with a white centre).

- main's "Ours edits" changes notes.txt lines 2 and 10 (`line 2 ours`, `line 10 ours`), other.txt
  (`other file`, `changed on main`), image.png (green) and adds new.txt (`new file`, `added on main`).
- feature/theirs' "Theirs edits" changes notes.txt lines 2, 6 and 10 (`line 2 theirs`,
  `line 6 theirs`, `line 10 theirs`). Its notes.txt is blob `6216606`.
- feature/picks has "Pick clean" (pick.txt), "Pick conflicting" (notes.txt line 2 `line 2 picked`)
  and "Pick last" (last.txt).
- feature/kinds' "Kinds edits" deletes other.txt, changes image.png (red) and adds new.txt
  (`new file`, `added on feature/kinds`).
- Every file has LF line ends.

**Graph GC-M** (defined with checks C10 to C13) is also the graph of C14, C15 and C16 and of D18's
resolver at 1920×1080: "conflicts" as built, with a working-changes row while a merge is stopped.
What these checks rely on:

- It has 5 lanes, so the Graph column is 96 wide. At 1100×700 the graph area (440) shows Branch / Tag
  (130), Graph and Message (214) and no Date column (phase 1's column rule). At 1920×1080 every
  column shows.
- The dates are the author dates, as the details show them. All are on 2026-01-01, UTC.
- Lane 0 runs from the working-changes ring to row 10. Lane 1 runs from row 2 to the node of row 9,
  lane 2 from row 3, lane 3 from row 6 and lane 4 from row 8, each to the node of row 9.
- The ring's dashed line runs in lane 0 to the node of row 1 (phase 2).
- A merge stop makes no commit: the commits and refs are those `Phase4ScenarioTests` pins
  (`Stopped_states_keep_the_graph_except_the_picked_commit`).

| Display row | Labels, in phase 1's order | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | | ring 0, 0 | `Working changes · 1 conflict` (C14, C16), `Working changes · 3 conflicts` (C15); `Working changes` once nothing conflicts. At 1100×700 it is drawn whole or cut with "…" in the 214-wide Message column by phase 1's rule, which is not asserted; `WorkingRowText` holds the whole text | | |
| 1 | `main` ✓ | 0, 0 | Ours edits | 12:09 | `da31c97` |
| 2 | `feature/kinds` | 1, 1 | Kinds edits | 12:08 | `3a9de3e` |
| 3 | `feature/picks` | 2, 2 | Pick last | 12:07 | `5c4c257` |
| 4 | | 2, 2 | Pick conflicting | 12:06 | `01fd976` |
| 5 | | 2, 2 | Pick clean | 12:05 | `01d2046` |
| 6 | `feature/rebase` | 3, 3 | Second change | 12:04 | `305f7f7` |
| 7 | | 3, 3 | First change | 12:03 | `70e8c78` |
| 8 | `feature/theirs` | 4, 4 | Theirs edits | 12:02 | `5c77ffb` |
| 9 | | 0, 0 (lanes 1, 2, 3 and 4 end here) | Base | 12:01 | `195768b` |
| 10 | | 0, 0 | Initial commit | 12:00 | `6bd579f` |

**Graph K1**: C17's terminal rebase, stopped at "Pick conflicting".

- `git switch feature/picks` then `git rebase main` ran through `TempRepo` at 12:10. Git replayed
  "Pick clean" onto main as `d71cdc4` (`d71cdc4e9eb7fa80dc917e6f859204a0286e78ac`), with author date
  12:05 and committer date 2026-01-01 12:10. It is the same commit that
  `ConflictsStoppedAsync(CherryPicks)` makes: same parent, tree, message and dates.
- Git then stopped on "Pick conflicting". HEAD is detached at `d71cdc4`.
- `git log --date-order` lists the same commits in the same order as the pinned CherryPicks stop, so
  the lanes are the pinned ones ("d71cdc4 Pick clean · lane 0" above the conflicts graph). Only the
  labels differ: `HEAD` instead of `main` on `d71cdc4`, and no check mark anywhere.
- It has 5 lanes and no Date column at 1100×700. Lanes end as in GC-M, in the node of row 10.

| Display row | Labels, in phase 1's order | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | | ring 0, 0 | `Working changes · 1 conflict` (`Working changes` once resolved; drawn whole or cut as in GC-M) | | |
| 1 | the filled `HEAD` | 0, 0 | Pick clean | 12:05 | `d71cdc4` |
| 2 | `main` | 0, 0 | Ours edits | 12:09 | `da31c97` |
| 3 | `feature/kinds` | 1, 1 | Kinds edits | 12:08 | `3a9de3e` |
| 4 | `feature/picks` | 2, 2 | Pick last | 12:07 | `5c4c257` |
| 5 | | 2, 2 | Pick conflicting | 12:06 | `01fd976` |
| 6 | | 2, 2 | Pick clean | 12:05 | `01d2046` |
| 7 | `feature/rebase` | 3, 3 | Second change | 12:04 | `305f7f7` |
| 8 | | 3, 3 | First change | 12:03 | `70e8c78` |
| 9 | `feature/theirs` | 4, 4 | Theirs edits | 12:02 | `5c77ffb` |
| 10 | | 0, 0 (lanes 1, 2, 3 and 4 end here) | Base | 12:01 | `195768b` |
| 11 | | 0, 0 | Initial commit | 12:00 | `6bd579f` |

**Graph K2**: C17 after the app's Continue.

- feature/picks is `f048313` → `e436626` → `d71cdc4` → `da31c97`.
- The originals "Pick last", "Pick conflicting" and "Pick clean" (`5c4c257`, `01fd976`, `01d2046`)
  are reachable from no ref any more and leave the graph.
- `git log --date-order` lists `f048313`, `e436626` (both with committer date 2026-01-02 09:00),
  `d71cdc4` (12:10), then `da31c97` and the rest.
- The lanes were laid out by D48, D54 and D55. It has 4 lanes, so the Graph column is 80, and at
  1100×700 there is no Date column (440 − 130 − 80 − 120 < 120).
- Lane 0 runs from row 0 to row 9. Lane 1 runs from row 4, lane 2 from row 5 and lane 3 from row 7,
  each to the node of row 8.

| Row | Labels, in phase 1's order | Node: lane, colour | Message | Date (author) | Commit date | SHA |
|---|---|---|---|---|---|---|
| 0 | `feature/picks` ✓ | 0, 0 | Pick last | 2026-01-01 12:07 | 2026-01-02 09:00 | `f048313` |
| 1 | | 0, 0 | Pick conflicting | 2026-01-01 12:06 | 2026-01-02 09:00 | `e436626` |
| 2 | | 0, 0 | Pick clean | 2026-01-01 12:05 | 2026-01-01 12:10 | `d71cdc4` |
| 3 | `main` | 0, 0 | Ours edits | 2026-01-01 12:09 | (the same) | `da31c97` |
| 4 | `feature/kinds` | 1, 1 | Kinds edits | 2026-01-01 12:08 | | `3a9de3e` |
| 5 | `feature/rebase` | 2, 2 | Second change | 2026-01-01 12:04 | | `305f7f7` |
| 6 | | 2, 2 | First change | 2026-01-01 12:03 | | `70e8c78` |
| 7 | `feature/theirs` | 3, 3 | Theirs edits | 2026-01-01 12:02 | | `5c77ffb` |
| 8 | | 0, 0 (lanes 1, 2 and 3 end here) | Base | 2026-01-01 12:01 | | `195768b` |
| 9 | | 0, 0 | Initial commit | 2026-01-01 12:00 | | `6bd579f` |

The full SHAs: `f048313649af31430afc5dce1fa3e2baefdcd547` (Pick last) and
`e4366268fbb7c6e83e1f4f9d1cd77dd9b5299da0` (Pick conflicting). Both are by Test Author
<author@example.com>, and their messages are exactly `Pick last\n` and `Pick conflicting\n`.

**The files as git leaves them.**

- **FM** (as under checks C10 to C13, here with its blob and git's files): notes.txt after
  `git merge feature/theirs` (C10, C14, C16). It has 20 lines, two blocks, and line 6 merged
  cleanly: 1 `line 1`, 2 `<<<<<<< HEAD`, 3 `line 2 ours`, 4
  `=======`, 5 `line 2 theirs`, 6 `>>>>>>> feature/theirs`, 7 `line 3`, 8 `line 4`, 9 `line 5`, 10
  `line 6 theirs`, 11 `line 7`, 12 `line 8`, 13 `line 9`, 14 `<<<<<<< HEAD`, 15 `line 10 ours`, 16
  `=======`, 17 `line 10 theirs`, 18 `>>>>>>> feature/theirs`, 19 `line 11`, 20 `line 12`.
  - Its blob is `cdd7b39` (`cdd7b3996a63184d1ed12272da6ed2320e72c59a`).
  - `git check-attr conflict-marker-size -- notes.txt` prints `unspecified`, so the marker size is 7.
  - Stage 2 is main's notes.txt (`439eb6b`), stage 3 feature/theirs' (`6216606`).
  - MERGE_HEAD is `5c77ffb…`. `MERGE_MSG` is `Merge branch 'feature/theirs'`, a blank line,
    `# Conflicts:`, `#⇥notes.txt`.
- **PM** (as under checks C10 to C13): the panes for FM (design 11.3).
  - The ours pane, titled `Ours: main`: 1 `line 1`; bar [`Use ours`, `Use both`]; 2 `line 2 ours`
    (`VcConflictOursBrush`); 3 `line 3`; 4 `line 4`; 5 `line 5`; 6 `line 6 theirs`; 7 `line 7`;
    8 `line 8`; 9 `line 9`; bar [`Use ours`, `Use both`]; 10 `line 10 ours` (`VcConflictOursBrush`);
    11 `line 11`; 12 `line 12`.
  - The theirs pane, titled `Theirs: feature/theirs` (from `MERGE_MSG`'s `Merge branch
    'feature/theirs'`): the same rows with `line 2 theirs` and `line 10 theirs`
    (`VcConflictTheirsBrush`), and bars [`Use theirs`, `Use both`].
  - Each block has one line on each side, so there are no filler rows.
  - At 1100×700, when the resolver opens on block 1, block 1's bar is the second row (only `line 1` is
    above it). Block 2's bar is the eleventh: 9 text rows of 15.84 and 2 bars of 26 make 194.6 px,
    within the about 212 px of design 11.2. So both bars are in view without scrolling.
- **The kinds merge** (C15): `git status --porcelain=v1` prints `UU image.png`, `AA new.txt` and
  `UD other.txt`, in that order. That is also the stage list's order.
  - image.png: stages 1, 2 and 3 are `e73f509` (indigo), `c22a1df` (green) and `23eb33f` (red).
    The working tree holds the green picture, `c22a1df`, with no markers. Every stage has NUL bytes
    in its first 8000, so it is binary.
  - new.txt: stages 2 and 3 are `0608103` and `8240019`. The working tree holds 6 lines: 1 `new file`,
    2 `<<<<<<< HEAD`, 3 `added on main`, 4 `=======`, 5 `added on feature/kinds`,
    6 `>>>>>>> feature/kinds`.
  - other.txt: stages 1 and 2 are `595dfd6` and `ec92a8c`, with no stage 3. The working tree holds
    `other file`, `changed on main`.
  - MERGE_HEAD is `3a9de3e…`. `MERGE_MSG` is `Merge branch 'feature/kinds'`, a blank line,
    `# Conflicts:`, `#⇥image.png`, `#⇥new.txt`, `#⇥other.txt`.

#### The tints (design 7.3, `Tokens.axaml`)

| Token | Dark | Light | Where these checks expect it |
|---|---|---|---|
| `VcConflictOursBrush` | `#15323A` | `#DCF1F5` | an unresolved block's ours lines in the ours pane; its ours section in the result |
| `VcConflictTheirsBrush` | `#2C2140` | `#F0E6FA` | the theirs lines in the theirs pane; the theirs section in the result |
| `VcConflictBaseBrush` | `#24272F` | `#EEF0F3` | a diff3 base section in the result (D18 only) |
| `VcConflictMarkerBrush` | `#3A2E16` | `#FCEFD2` | an unresolved block's marker lines in the result, their text in `VcWarningBrush` (`#E0A23B` dark, `#B7791F` light) |
| `VcConflictUsedBrush` | `#1A2E24` | `#E6F6EC` | a side's lines in its pane once the side is used in the result (never in the result) |
| `VcDiffHunkBrush` | `#1C2130` | `#EEF0FB` | every block bar in the panes (phase 2's hunk header colour) |

A side whose bar says `Not used` has no tint on its lines (design 11.5). That holds after the other
side's choice, and on both sides of a block whose markers were edited away.

### Part D: the light theme at 1920×1080

Check D18 of design 13.4 shows the interactive list with a dropped and a squashed row, the
message dialog and the resolver in the light theme at 1920×1080, with every tint of design 7.3
sampled from the pictures. It follows the conventions of checks C14 to C17 (part C), starts from
part B's "interactive" scenario and part C's "conflicts" scenario, and uses the light column of
part C's tints table. It is scripted only: 1920×1080 does not fit the development screen (13.4).

#### Test methods (part D)

| Check | Test method (one app start each) | Scenario as the check starts it | Window, theme |
|---|---|---|---|
| D18 | `Check_D18_interactive_list_light` | `InteractiveAsync()` | 1920×1080, light |
| D18 | `Check_D18_resolver_light` | `ConflictsAsync()`, then, through `TempRepo` before the app starts, `git config merge.conflictStyle diff3` and `git merge feature/theirs` (exit 1) | 1920×1080, light |

#### Graph G-I at 1920×1080 and the diff3 file N3

**Graph G-I at 1920×1080**: part B's G-I, "interactive" as built (pinned), as D18 shows it. It has
one lane, colour 0, and the Graph column is 48. At 1920×1080 every column shows.

| Row | Labels, in phase 1's order | Node: lane, colour | Message | Date | SHA |
|---|---|---|---|---|---|
| 0 | `main` ✓ | 0, 0 | Five | 12:07 | `57f089d` |
| 1 | | 0, 0 | Four | 12:06 | `eef5c15` |
| 2 | | 0, 0 | Three | 12:05 | `ff42e7f` |
| 3 | | 0, 0 | Fix one | 12:04 | `ed58902` |
| 4 | | 0, 0 | Two | 12:03 | `938844a` |
| 5 | | 0, 0 | One | 12:02 | `fc6c8fd` |
| 6 | | 0, 0 | Base | 12:01 | `4b4622c` |
| 7 | | 0, 0 | Initial commit | 12:00 | `da32da3` |

Three's message is `Three\n\n#42 is the issue\n`. Four adds src/four.txt `four`.

The file D18's resolver shows:

- **N3**: notes.txt after `git merge feature/theirs` with `merge.conflictStyle=diff3` (D18). It has 24
  lines. Git labels the base section with the merge base's short SHA:
  1 `line 1`, 2 `<<<<<<< HEAD`, 3 `line 2 ours`, 4 `||||||| 195768b`, 5 `line 2`, 6 `=======`,
  7 `line 2 theirs`, 8 `>>>>>>> feature/theirs`, 9 `line 3`, 10 `line 4`, 11 `line 5`,
  12 `line 6 theirs`, 13 `line 7`, 14 `line 8`, 15 `line 9`, 16 `<<<<<<< HEAD`, 17 `line 10 ours`,
  18 `||||||| 195768b`, 19 `line 10`, 20 `=======`, 21 `line 10 theirs`, 22 `>>>>>>> feature/theirs`,
  23 `line 11`, 24 `line 12`.
  Its panes are PM, rows and tints alike: the panes show no base section.

#### Texts these checks rely on (part D)

The banner's, the resolver's and the stage panel's texts are part C's (checks C14 to C17); the
list's are part B's. Besides them:

| Where | Text |
|---|---|
| Interactive list (2.1, 2.2) | the commit menu's `Interactive rebase of main from here…`; header `Interactive rebase of main from 4b4622c`, count `6 commits`; actions `Pick`, `Squash`, `Drop`; second line `Message: Three` with `Edit…`; base row `Base`; footer `Cancel`, `Start rebase` |
| Message dialog (7.2, 2.2) | title `Message of the squashed commit`; the group's message starts as git's: Three's message, a blank line, Four's (`Three\n\n#42 is the issue\n\nFour`), split into the summary `Three` and the description `#42 is the issue`, an empty line, `Four`; counter `67` (72 − 5); buttons `Cancel`, `Save` |

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes). "Real window" says which steps the
real-window pass repeats (RW), or "n/a" with the reason; "Screenshots" lists the planned pictures,
the scripted run's and then the real window's.

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| A1 | The new menu items: a commit not on HEAD, a commit on HEAD's line, HEAD's own commit, a local and two remote branches, the current branch with "Pull … (rebase)", several commits (a chain that can be squashed and three selections that cannot); "rebase", dark, 1100×700 | 1. "Rebase" scenario. Right-click the row "Main more" (row 1) in its Message column; rest the pointer on "Interactive rebase of feature/topic from here…". Screenshot. Press Esc. 2. Right-click the row "Topic one" (row 4); rest the pointer on "Rebase feature/topic onto this commit". Screenshot. Press Esc. 3. Right-click the row "Topic two" (row 3). Screenshot. Press Esc. 4. In the left panel right-click `main` under Local branches. Screenshot. Press Esc. 5. Right-click `main` under Remotes › origin. Screenshot. Press Esc. 6. Under Remotes › origin click the folder `feature`, then right-click its `topic`. Press Esc. 7. Right-click `topic` under Local branches › feature. Screenshot. Press Esc. 8. Click row 3 ("Topic two"), Ctrl+click row 4 ("Topic one"), right-click row 4. Screenshot. Press Esc. 9. Ctrl+click row 4 (it leaves the selection), Ctrl+click row 5 ("Add core"), right-click row 5; rest the pointer on "Squash 2 commits…". Screenshot. Press Esc. 10. Click row 1 ("Main more"), Ctrl+click row 2 ("Main work"), right-click row 2. Press Esc. 11. Click row 5 ("Add core"), Ctrl+click row 6 ("Initial commit"), right-click row 6. Press Esc. | At the start: R0, its left panel, nothing selected, `CurrentBranch` `feature/topic`.<br>**After step 1:** row 1 selected; the menu (284 high): header "1dd58b8 Main more"; "Check out this commit" (highlighted); "Create branch here…"; "Create tag here…"; —; "Cherry-pick onto feature/topic"; "Revert this commit"; "Rebase feature/topic onto this commit"; "Interactive rebase of feature/topic from here…" (disabled: "This commit is not on feature/topic"); "Reset feature/topic to this commit…"; —; "Copy SHA". The pointer rests on the disabled item, its tooltip "This commit is not on feature/topic" shows inside the window, and "Check out this commit" stays highlighted. The log has one line `rebase: Context menu opened: 1dd58b8 Main more by pointer`.<br>**After step 2:** row 4 selected; header "1219e45 Topic one"; "Check out this commit" (highlighted); "Create branch here…"; "Create tag here…"; —; "Cherry-pick onto feature/topic" (disabled: "This commit is already on feature/topic"); "Revert this commit"; "Rebase feature/topic onto this commit" (disabled: "This commit is already on feature/topic"); "Interactive rebase of feature/topic from here…" (enabled: Topic one is HEAD's first parent, depth 1 on the linear line); "Reset feature/topic to this commit…"; —; "Copy SHA"; 284 high; the tooltip "This commit is already on feature/topic" shows; the highlight stays on "Check out this commit".<br>**After step 3:** row 3 selected; header "b0dfa3f Topic two"; "Check out this commit" (highlighted; enabled, HEAD being on a branch); "Create branch here…"; "Create tag here…"; —; "Cherry-pick onto feature/topic" (disabled: "This commit is already on feature/topic"); "Revert this commit"; "Rebase feature/topic onto this commit" (disabled: "This commit is already on feature/topic"); "Interactive rebase of feature/topic from here…" (disabled: "feature/topic has no commits after this one", the first of its reasons); "Reset feature/topic to this commit…"; —; "Copy SHA"; 284 high.<br>**After step 4:** the right-click opened the menu only: row 3 stays the graph's selection and no left-panel row is highlighted (D114). The menu (458 high): header "main"; "Check out main" (highlighted); "Merge main into feature/topic"; "Merge main into feature/topic (create a merge commit)"; "Rebase feature/topic onto main"; "Interactive rebase of feature/topic onto main…"; "Fast-forward main to origin/main" (disabled: "main is up to date with origin/main"); —; "Push main to origin/main"; "Force push main…" (danger); —; "Create branch here…"; "Create tag here…"; "Rename main…"; "Delete main…" (danger); —; "Change upstream…"; "Stop tracking origin/main"; —; "Copy branch name". The two rebase items stand after the merges and before the fast-forward (7.1); the rebase is enabled because HEAD does not reach main's tip `1dd58b8`.<br>**After step 5:** header "origin/main"; "Check out origin/main" (highlighted; enabled: HEAD is on feature/topic, not on main); "Merge origin/main into feature/topic"; "Merge origin/main into feature/topic (create a merge commit)"; "Rebase feature/topic onto origin/main"; "Interactive rebase of feature/topic onto origin/main…"; —; "Create branch here…"; "Create tag here…"; —; "Delete main on origin…" (danger); —; "Copy branch name"; 293 high.<br>**After step 6:** the remote folder is open with `topic`; header "origin/feature/topic"; "Check out origin/feature/topic" (disabled: "feature/topic is already checked out"); "Merge origin/feature/topic into feature/topic" (highlighted, the first enabled item); "Merge origin/feature/topic into feature/topic (create a merge commit)"; "Rebase feature/topic onto origin/feature/topic" (disabled: "feature/topic is up to date with origin/feature/topic": its tip is HEAD's, one of `TipsOnHead`); "Interactive rebase of feature/topic onto origin/feature/topic…" (enabled: the onto items do not take reason 1, design 2.1); —; "Create branch here…"; "Create tag here…"; —; "Delete feature/topic on origin…" (danger); —; "Copy branch name"; 293 high.<br>**After step 7:** header "feature/topic"; "Pull from origin/feature/topic" (highlighted); "Pull from origin/feature/topic (rebase)" (enabled, as the toolbar's Pull is); —; "Push feature/topic to origin/feature/topic"; "Force push feature/topic…" (danger); —; "Create branch here…"; "Create tag here…"; "Rename feature/topic…"; "Delete feature/topic…" (disabled: "You cannot delete the branch you are on"); —; "Change upstream…"; "Stop tracking origin/feature/topic"; —; "Copy branch name"; 354 high. No rebase item (a branch is not rebased onto itself).<br>**After step 8:** rows 3 and 4 selected; the right panel's `MultiSelectionView`: "2 commits selected", listing `b0dfa3f` Topic two and `1219e45` Topic one. The menu (145 high): header "2 commits"; "Squash 2 commits…" (highlighted; enabled: Topic two's first parent is Topic one, both on HEAD's linear line, neither the root, HEAD on a branch, nothing in progress); "Cherry-pick 2 commits onto feature/topic" (disabled: "Some of these commits are already on feature/topic"); "Revert 2 commits"; —; "Copy SHAs".<br>**After step 9:** rows 3 and 5 selected ("2 commits selected": `b0dfa3f` Topic two, `996fdc1` Add core); header "2 commits"; "Squash 2 commits…" (disabled: "Select commits that follow one another"; its tooltip shows); "Cherry-pick 2 commits onto feature/topic" (disabled: "Some of these commits are already on feature/topic"); "Revert 2 commits" (highlighted); —; "Copy SHAs"; 145 high.<br>**After step 10:** rows 1 and 2 selected; header "2 commits"; "Squash 2 commits…" (disabled: "Only commits of feature/topic can be squashed"); "Cherry-pick 2 commits onto feature/topic" (highlighted); "Revert 2 commits"; —; "Copy SHAs"; 145 high.<br>**After step 11:** rows 5 and 6 selected; header "2 commits"; "Squash 2 commits…" (disabled: "The first commit cannot be squashed"); "Cherry-pick 2 commits onto feature/topic" (disabled: "Some of these commits are already on feature/topic"); "Revert 2 commits" (highlighted); —; "Copy SHAs"; 145 high.<br>No menu has a Compare item (a later phase, D77). Git's state is unchanged throughout: the refs as built, HEAD on `feature/topic` at `b0dfa3f`, no ref under `refs/visualcommit/backup/`. |  | RW: step 1 with the real mouse: the right-click on "Main more" found by `ContextMenu` and its items by `ContextMenuItem` (names, `IsEnabled`, the disabled item's `HelpText`), the pointer brought onto "Interactive rebase of feature/topic from here…" by the conventions' path, the tooltip drawn inside the window (`RealApp.WaitForToolTip`), the capture compared with `a1a-commit-not-on-head`, then Esc | Planned: `a1a-commit-not-on-head`, `a1b-commit-on-head-line`, `a1c-head-commit`, `a1d-local-branch`, `a1e-remote-branch`, `a1f-current-branch`, `a1g-squash-enabled`, `a1h-squash-not-a-chain`; real window: `a1a-commit-not-on-head` |  |
| A2 | A plain rebase from the branch menu ("Rebase feature/topic onto main") and from the commit menu (onto "Main work"): graphs before and after, the status, the backup ref and its target, the new commits' dates; "rebase", dark, 1100×700 | Part A: 1. "Rebase" scenario. Screenshot. 2. In the left panel right-click `main` under Local branches and click "Rebase feature/topic onto main". Wait until idle. Screenshot. 3. Click row 0. Screenshot. Part B (a new copy, a new start): 4. Right-click the row "Main work" (row 2) in its Message column and click "Rebase feature/topic onto this commit". Wait until idle. Screenshot. | **After step 1:** R0; nothing selected; the details panel "Select a commit to see its details."; `topic` in the accent colour with no arrows; status bar "Ready".<br>**After step 2:** no question before it and no dialog after it (D135). The operation "Rebase feature/topic onto main" ran (running text "Rebasing feature/topic onto main…", not pictured). The call log has `git rebase 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146`: main's tip as the menu read it, no branch argument and no option (1.3), exit 0, TA3. Status "Rebased feature/topic onto main" (secondary); the log line `rebase: Rebase feature/topic onto main: Succeeded after <N> ms: Rebased feature/topic onto main`; no `Removed backup` line. The graph is R1; nothing is selected (nothing was before); the left panel shows `topic` ↑4 ↓2; `CurrentBranch` `feature/topic`. Git: `git log --format=%s feature/topic` prints Topic two, Topic one, Main more, Main work, Add core, Initial commit; `feature/topic` is `5f71ab8`, whose parent is `befae26`, whose parent is `1dd58b8`; `git log -2 --format="%ad %cd" --date=iso-strict feature/topic` gives author dates 2026-01-01T12:03:00+00:00 and 12:02:00 and commit dates 2026-01-02T09:00:00+00:00 for both; author and committer "Test Author" <author@example.com>; `git status -sb` prints `## feature/topic...origin/feature/topic [ahead 4, behind 2]` and nothing else; no `rebase-merge` or `rebase-apply` folder; `ORIG_HEAD` is `b0dfa3f…`; HEAD's reflog, newest first: `rebase (finish): returning to refs/heads/feature/topic`, `rebase (pick): Topic two`, `rebase (pick): Topic one`, `rebase (start): checkout 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146`. `git for-each-ref refs/visualcommit/backup` prints one ref, `refs/visualcommit/backup/rebase-<UTC time>/feature/topic`, pointing at `b0dfa3f405dffff59bea3f4f434b16e5490cc597`, the branch's old tip (1.2). Every other ref is unchanged (`main` and `origin/main` `1dd58b8`, `feature/other` `2d4d79a`, `origin/feature/topic` `b0dfa3f`), and so is `origin.git` (`git ls-remote --heads origin`: `feature/topic` `b0dfa3f`, `main` `1dd58b8`).<br>**After step 3:** row 0 selected; the details: "Topic two"; Author "Test Author <author@example.com>"; Date "2026-01-01 12:03"; no Committer row; Commit date "2026-01-02 09:00"; SHA `5f71ab8bad51a15fdfdfd19fc1b0084ef6e956c5` (`DetailsSha`: the commit plain git makes); Parents `befae26`; "Changed files (1)": A `two.txt` `topic`.<br>**After step 4:** the right-click selected "Main work" (row 2) and opened its commit menu (A1 step 1's items with header "dd6a6fc Main work", "Rebase feature/topic onto this commit" enabled, "Interactive rebase of feature/topic from here…" disabled with "This commit is not on feature/topic"). No question, no dialog. The call log has `git rebase dd6a6fc8095ffa05dc783ce011e322a7241551a4`, exit 0, TA3. Status "Rebased feature/topic onto dd6a6fc" (`{target}` is the commit's short SHA, design 10). The graph is R2; "Main work" is still the selected commit, now row 4 (it was not one of feature/topic's commits before the rebase, so the selection stays on it, by its SHA: 1.5, phase 3's 4h); the left panel shows `topic` ↑3 ↓2. Git: `git log --format=%s feature/topic` prints Topic two, Topic one, Main work, Add core, Initial commit; `feature/topic` is `09b923a`, parent `c09318c`, parent `dd6a6fc`; author dates 12:03 and 12:02, commit dates 2026-01-02 09:00; `git status -sb`: `## feature/topic...origin/feature/topic [ahead 3, behind 2]`; HEAD's reflog starts `rebase (finish): returning to refs/heads/feature/topic` … `rebase (start): checkout dd6a6fc8095ffa05dc783ce011e322a7241551a4`; one backup ref, `rebase-<UTC time>/feature/topic` → `b0dfa3f`; `main` still at `1dd58b8`. |  | RW: part A with the real mouse: the capture of step 1 compared with `a2a-before`; the right-click on `main` in the left panel and the click on "Rebase feature/topic onto main" (`ContextMenuItem` by name), the wait for the queue's log line, the pointer to the neutral point, the capture compared with `a2b-rebased-onto-main`; the click on row 0 and the capture compared with `a2c-rebased-commit-details`; git's state asserted as in the scripted run. Part B is scripted only: its menu is the commit menu A1 pictures in the real window, and its item starts the same operation | Planned: `a2a-before`, `a2b-rebased-onto-main`, `a2c-rebased-commit-details`, `a2d-rebased-onto-commit`; real window: `a2a-before`, `a2b-rebased-onto-main`, `a2c-rebased-commit-details` |  |
| A3 | Rebase by drag: the drop menus with their rebase items (the current branch onto `main` and onto `origin/main`; a remote branch onto the current branch, which offers none; `feature/other`, not current, onto `main`), then "Check out feature/other and rebase it onto main": graph, HEAD, backup; "rebase", dark, 1100×700 | 1. "Rebase" scenario. Press the left mouse button at the centre of the label `feature/topic` on row 3 ("Topic two") and, holding it, move to the centre of the label `main` on row 1 ("Main more") in 5 equal steps (`MoveMouseHeld(…, steps: 5)`, as phase 3's check 7; the first step is more than 4 px from the press and starts the drag); every drag of this check moves so. Release. Screenshot. Press Esc. 2. In the left panel drag the row `topic` (Local branches › feature) onto the row `main` under Remotes › origin; release. Screenshot. Press Esc. 3. Drag the row `main` under Remotes › origin onto the row `topic` under Local branches › feature; release. Press Esc. 4. Drag the label `feature/other` on row 0 ("Other one") onto the label `main` on row 1; release. Screenshot. 5. Click "Check out feature/other and rebase it onto main". Wait until idle. Screenshot. | Before each menu the app read the drop's facts: `git merge-base --is-ancestor <B> <A>` exits 1 for every drop of this check (B is not an ancestor of A), so `DropFacts.SourceContainsTarget` is false and every rebase item below is enabled, and phase 3's fast-forward items get its "has commits that" reason.<br>**After step 1:** the press selected "Topic two"; the menu opened (145 high; phase 3's two items were 84): header "feature/topic → main"; "Fast-forward main to feature/topic" (disabled: "main has commits that feature/topic does not have"); "Check out main and merge feature/topic into it" (highlighted); —; "Rebase feature/topic onto main"; "Interactive rebase of feature/topic onto main…". Esc closes it; nothing ran.<br>**After step 2:** header "feature/topic → origin/main"; "Push feature/topic to origin/main" (highlighted); "Push feature/topic to origin/main and track it"; —; "Rebase feature/topic onto origin/main"; "Interactive rebase of feature/topic onto origin/main…"; 145 high. The left-panel row got no click: "Topic two" is still the only selection.<br>**After step 3:** header "origin/main → feature/topic"; "Merge origin/main into feature/topic" (highlighted); "Merge origin/main into feature/topic (create a merge commit)"; "Fast-forward feature/topic to origin/main" (disabled: "feature/topic has commits that origin/main does not have"); 110 high: no rebase item, because a remote branch is never rebased (1.1).<br>**After step 4:** the press selected "Other one" (row 0); header "feature/other → main"; "Fast-forward main to feature/other" (disabled: "main has commits that feature/other does not have"); "Check out main and merge feature/other into it" (highlighted); —; "Check out feature/other and rebase it onto main"; "Check out feature/other and rebase it onto main interactively…"; 145 high, no label cut.<br>**After step 5:** no question and no dialog. The operation "Check out feature/other and rebase it onto main" (several steps; running texts "Checking out feature/other…", then "Rebasing feature/other onto main…", not pictured) ran, per the call log: `git config --type=bool --get rebase.autoStash` (exit 1, no output: not set) and the status (clean), then `git switch --no-guess feature/other` (exit 0), then `git rebase 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146` (exit 0, `Rebasing (1/1)`, `Successfully rebased and updated refs/heads/feature/other.`). Status "Rebased feature/other onto main"; `CurrentBranch` `feature/other`. The graph is R3, with row 0 (`14e607b` Other one) selected: the press had selected `2d4d79a`, one of feature/other's commits before the rebase (it has left the graph), so the branch's new tip is selected (1.5); its details: "Other one", Date "2026-01-01 12:06", no Committer row, Commit date "2026-01-02 09:00", SHA `14e607bf248c8917ab93680c48cba392f60546ff`, Parents `1dd58b8`, "Changed files (1)": A `one.txt` `other`. Git: HEAD is `refs/heads/feature/other` at `14e607b`, message "Other one", parent `1dd58b8`; `git status -sb` prints `## feature/other`; `feature/topic` is still `b0dfa3f` and `main` `1dd58b8`; no rebase folder; HEAD's reflog, newest first: `rebase (finish): returning to refs/heads/feature/other`, `rebase (pick): Other one`, `rebase (start): checkout 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146`, `checkout: moving from feature/topic to feature/other`. One backup ref: `refs/visualcommit/backup/rebase-<UTC time>/feature/other` → `2d4d79a03112c8a5e2f9a783318d588df519af5d`. No `rebase-merge/visualcommit-return` is written (the rebase did not stop, 1.3). |  | RW: step 4's menu with the real mouse: the press on the label `feature/other`, 5 moves with the button held, the release over `main`; the menu found by `ContextMenu`, its items by `ContextMenuItem`, the capture compared with `a3c-drop-other-onto-main`; then Esc (the button released in a `finally`); nothing changed in git | Planned: `a3a-drop-current-onto-main`, `a3b-drop-onto-remote-branch`, `a3c-drop-other-onto-main`, `a3d-checked-out-and-rebased`; real window: `a3c-drop-other-onto-main` |  |
| A4 | Pull with rebase against a file remote: a clean one (before and after, backup, status), and one whose rebase stops, then is resolved and continued, or skipped; "sync" changed as each part says, dark, 1100×700 | Part A: 1. "Sync" scenario; before the app starts, `PushFromElsewhereAsync(sync, "main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere work")` (12:05) and `CommitFileAsync("src/local.txt", "local\n", "Local work")` on `main` (12:06). Start. Screenshot. 2. Right-click `main` under Local branches and click "Pull from origin/main (rebase)". Wait until idle. Screenshot. Part B (a new copy, a new start): 3. "Sync" scenario; before the app starts, `CheckoutAsync("published")` and `PushFromElsewhereAsync(sync, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere")` (12:05). Start. Right-click `published` under Local branches and click "Pull from origin/published (rebase)". Wait until idle. Screenshot. 4. Click the working-changes row (row 0). Screenshot. 5. Count the log lines `sync: working tree: …` (`AppLogFile.WorkingTreeRead("sync")`). Outside the app, write `src/published.txt` "published\nelsewhere\nunpushed\n" (three lines, LF; `TempRepo.WriteFile`); wait until one more such line is logged, `sync: working tree: 0 staged, 1 unstaged` (`OutsideChangeTimeout`; the status itself does not change, still `UU`), and until idle. Right-click the unstaged `src/published.txt` and click "Mark as resolved". Wait until idle. Screenshot. 6. Click the banner's "Continue". Wait until idle. Screenshot. Part C (a new copy, a new start): 7. As step 3 (no screenshot). 8. Click the banner's "Skip". Screenshot. 9. Click "Skip" in the dialog. Wait until idle. Screenshot. | **After step 1:** S1, nothing selected, `CurrentBranch` `main`.<br>**After step 2:** the menu was A1 step 7's for `main` ("Pull from origin/main" (highlighted), "Pull from origin/main (rebase)", …, 354 high). No dialog. The operation "Pull (rebase)" (running text "Pulling origin/main into main…") ran, per the call log: `git fetch --progress origin` (exit 0; its last line `   a7f2cfd..b2e9a9e  main       -> origin/main`); then, `main` having a commit of its own (`git merge-base --is-ancestor f4f8c060b1e68a3e560113b942f1f984c6b5d4f0 FETCH_HEAD` exits 1), `git merge-base --fork-point refs/remotes/origin/main HEAD` (`a7f2cfd…`) and the two counts of design 14.2, `git rev-list --count --no-merges --right-only --cherry-pick refs/remotes/origin/main...HEAD --` (1) and the same with `^a7f2cfd9a076c4c746e13caaa60f4fa2f0d76a4c` (1): git leaves nothing out; then the backup, then `git rebase` with no other argument (section 6), exit 0, `Rebasing (1/1)`, `Successfully rebased and updated refs/heads/main.`. Status "Pulled and rebased main onto origin/main" (secondary, no warning); the log line `sync: Pull (rebase): Succeeded after <N> ms: Pulled and rebased main onto origin/main`. The graph is S2; nothing selected; `main` ↑1. Git: `main` is `8a1f07b` ("Local work", parent `b2e9a9e`, author date 2026-01-01 12:06, commit date 2026-01-02 09:00), whose tree holds `src/elsewhere.txt` "elsewhere\n" and `src/local.txt` "local\n"; `git status -sb`: `## main...origin/main [ahead 1]`; no rebase folder; HEAD's reflog, newest first: `rebase (finish): returning to refs/heads/main`, `rebase (pick): Local work`, `rebase (start): checkout refs/remotes/origin/main`. One backup ref: `rebase-<UTC time>/main` → `f4f8c060b1e68a3e560113b942f1f984c6b5d4f0` (saved through the pull's callback before the rebase, D147). `published`, `topic` and `origin/published` are unchanged, and so is `origin.git` (`main` `b2e9a9e`, `published` `8c6f545`).<br>**After step 3:** at the start S3. The call log: `git fetch --progress origin` (`   8c6f545..899ce97  published  -> origin/published`), `git merge-base --is-ancestor 8e653159ca5e460a187117d51b3f18bdf81f5c96 FETCH_HEAD` (exit 1), `git merge-base --fork-point refs/remotes/origin/published HEAD` (`8c6f545…`) and design 14.2's two counts, `git rev-list --count --no-merges --right-only --cherry-pick refs/remotes/origin/published...HEAD --` (1) and the same with `^8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6` (1): nothing left out; then the backup, then `git rebase`, exit 1 with TA4 and `.git/rebase-merge/` present: stopped (1.4). Status "Pull stopped: 1 file has conflicts" in the warning colour; no dialog (D95); the log line `sync: Pull (rebase): Conflicts after <N> ms: Pull stopped: 1 file has conflicts`. The banner (`ConflictBanner`, `VcBannerBackgroundBrush`, the warning icon): `BannerText` `Rebasing published onto origin/published: stopped at 8e65315 "Unpushed work" (1 of 1). 1 file has conflicts.` on more than one line, nothing cut; "Continue" (disabled, tooltip "Resolve the conflicts first (1 file)"), "Skip" (enabled), "Abort rebase", on a row of their own under the text, right-aligned; the tooltip of the icon and text (`ToolTip.GetTip(BannerInfo)`) `Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts published back where it was before the rebase.`. The graph is S4 below the banner; nothing is selected (the right-click on a left-panel row selected nothing, D114) and the details panel shows "Select a commit to see its details."; `CurrentBranch` "Detached at 899ce97"; the left panel shows `published` ↑1 ↓1, not in the accent colour (HEAD is detached), `main` and `topic` without arrows. The toolbar's Pull is disabled with "HEAD is detached: check out a branch to pull" (4d's first condition: HEAD is detached at the stop), Push with "HEAD is detached: check out a branch to push", and Stash with "Finish or abort the rebase first" (D119); all three read with `ToolTip.GetTip`; Fetch and Branch are enabled, Pop disabled with "There are no stashes". Git: `git status --porcelain` prints `UU src/published.txt`; HEAD is detached at `899ce97`; `.git/rebase-merge/head-name` is `refs/heads/published`, `onto` `899ce97…`, `orig-head` `8e65315…`, `msgnum` 1, `end` 1, `stopped-sha` `8e65315…`, `message` "Unpushed work\n\n" (the subject and an empty line), `done` `pick 8e653159ca5e460a187117d51b3f18bdf81f5c96 Unpushed work`, `git-rebase-todo` empty, `author-script` with `GIT_AUTHOR_DATE='@1767268980 +0000'`; `REBASE_HEAD` is `8e65315…`; `MERGE_MSG` is "Unpushed work", a blank line, "# Conflicts:", "#⇥src/published.txt"; `src/published.txt` reads `published`, `<<<<<<< HEAD`, `elsewhere`, `=======`, `unpushed`, `>>>>>>> 8e65315 (Unpushed work)`. One backup ref: `rebase-<UTC time>/published` → `8e653159ca5e460a187117d51b3f18bdf81f5c96`.<br>**After step 4:** the working-changes row selected; the stage panel: "Unstaged files (1, 1 with conflicts)": U `published.txt` `src` in the danger colour (its tooltip "Conflict: click to resolve it", `ToolTip.GetTip`); "Staged files (0)"; `CommitSummary` "Unpushed work" (from `rebase-merge/message`, 5.3), `CommitDescription` empty; `CommitButton` "Continue rebase", disabled, tooltip "Resolve the conflicts first (1 file)"; `AmendCheckBox` disabled with "Finish or abort the rebase first".<br>**After step 5:** the right-click opened the conflicted file's menu only (7.1: header "published.txt"; "Resolve conflict" (highlighted); "Open in merge tool" (disabled: "Set merge.tool in git's configuration to use a merge tool"); "Mark as resolved"; "Discard changes…" (disabled: "Resolve the conflict first"); —; "Ignore published.txt" and "Ignore all *.txt files" (both disabled: "Only untracked files can be ignored"); —; "Open file"; "Show in folder"; "Copy path"; 284 high). "Mark as resolved" asked nothing (the file has no conflict markers, 7.1) and ran `git add --all -- src/published.txt`: `git status --porcelain` prints `M  src/published.txt`. The banner: `Rebasing published onto origin/published: stopped at 8e65315 "Unpushed work" (1 of 1). Conflicts resolved: continue to commit it and go on.`, "Continue" and "Skip" enabled, "Abort rebase". The working-changes row reads "Working changes"; "Unstaged files (0)"; "Staged files (1)": M `published.txt` `src`; the summary still "Unpushed work"; "Continue rebase" enabled; status "Ready".<br>**After step 6:** no question: before Continue ran, its marker check `git diff --cached --check --no-color --no-ext-diff -- src/published.txt` (the path that was conflicted at the stop; exit 0, no output) reported no marker (5.1). The operation "Continue the rebase" (running text "Continuing the rebase…") ran, per the call log, in this order: `git commit --no-verify --cleanup=whitespace --file=-` (exit 0), then `git rebase --continue` (exit 0, `Successfully rebased and updated refs/heads/published.`); between the marker check and the commit come the git layer's own reads (`git ls-files --unmerged -z`, no output; `git diff-index --cached --quiet HEAD --`, exit 1: something is staged), which the check does not assert. The call log keeps neither standard input nor the environment; the commit shows them: its message is "Unpushed work" (the boxes held what was filled, so the app passed no message and the git layer sent `rebase-merge/message` without its trailing empty line, 5.3; `--cleanup=whitespace` gives the same commit either way) and its author is "Test Author" <author@example.com> dated 2026-01-01 12:03, taken from `author-script` (5.4; without it the author date would be 2026-01-02 09:00). Status "Rebased published onto origin/published"; the log line `sync: Continue the rebase: Succeeded after <N> ms: Rebased published onto origin/published`. No banner and no working-changes row; the graph is S5; `CurrentBranch` `published`; `published` ↑1. Nothing is selected: the working-changes row that was selected is gone, so the selection goes with it and the details panel shows "Select a commit to see its details." (phase 2's rule, `RepositoryViewModel.ApplyStatus`, which 1.5 and 14.3 keep after Continue and Skip). Git: `published` is `cd8de5c`, message "Unpushed work" with no body, parent `899ce97`, author "Test Author" <author@example.com> 2026-01-01 12:03 (kept), commit date 2026-01-02 09:00, `src/published.txt` "published\nelsewhere\nunpushed\n"; `git status -sb`: `## published...origin/published [ahead 1]`; no rebase folder (a stale `REBASE_HEAD` stays, design 0.1); HEAD's reflog, newest first: `rebase (continue) (finish): returning to refs/heads/published`, `commit: Unpushed work`, `rebase (start): checkout refs/remotes/origin/published`. The backup refs are still only `rebase-<UTC time>/published` → `8e65315` (Continue saves none).<br>**After step 8:** the confirmation over the backdrop: `ConfirmTitle` "Skip 8e65315?"; `ConfirmQuestion` `"Unpushed work" is left out of the rebase, and changes made to resolve its conflicts are lost.`; `ConfirmDetail` "A backup is saved first, so this can be undone."; "Cancel" (with the focus) and "Skip" (danger). Nothing has run: the call log has no `rebase --skip`.<br>**After step 9:** the operation "Skip 8e65315" (running text "Skipping 8e65315…") saved its backup first, then ran `git rebase --skip` (exit 0, `Successfully rebased and updated refs/heads/published.`). Status "Rebased published onto origin/published" (a Skip that ends the rebase shows Continue's done text, 10.2); the log line `sync: Skip 8e65315: Succeeded after <N> ms: Rebased published onto origin/published`. No banner, no working-changes row; the graph is S6; `CurrentBranch` `published`, no arrows; nothing is selected (nothing was in part C) and the details panel shows "Select a commit to see its details.". Git: `published` is `899ce97`, equal to `origin/published`; `git status -sb` prints `## published...origin/published` and nothing else; `src/published.txt` "published\nelsewhere\n"; HEAD's reflog starts `rebase (skip) (finish): returning to refs/heads/published`. `git for-each-ref refs/visualcommit/backup` prints three refs: `rebase-<UTC time>/published` → `8e65315…`; `skip-<UTC time>` → `899ce97…` (HEAD at the stop); `skip-<UTC time>-changes` → a snapshot commit whose parent is `899ce97` and whose `src/published.txt` is the conflicted file, markers included (`SnapshotTrackedChangesAsync`). `8e65315` has left the graph. |  | n/a: the item is a plain item of the current branch's menu (pictured by A1 step 7 in the scripted run), the remote is a folder, and a rebase stop's banner with Continue and Skip is pictured in the real window by C11 | Planned: `a4a-before`, `a4b-pulled-with-rebase`, `a4c-pull-stopped`, `a4d-stop-stage-panel`, `a4e-resolved`, `a4f-continued`, `a4g-skip-question`, `a4h-skipped` |  |
| A5 | Rebases git refuses: a changed tracked file (the error dialog's title, summary and git's lines; nothing changed), "Check out … and rebase it" with a changed file (refused by the app before the checkout: still on the branch the user was on), and an untracked file in the way; "rebase", dark, 1100×700 | Part A: 1. "Rebase" scenario; before the app starts, write `src/core.txt` "core\nlocal edit\n" (`TempRepo.WriteFile`, not staged). Start. 2. Right-click `main` under Local branches and click "Rebase feature/topic onto main". Wait for the dialog. Screenshot. Click "Close". 3. Drag the label `feature/other` (row 1, "Other one") onto the label `main` (row 2, "Main more"), as A3; release; click "Check out feature/other and rebase it onto main". Wait for the dialog. Screenshot. Click "Close". Part B (a new copy, a new start): 4. "Rebase" scenario; before the app starts, write the untracked `docs/main.txt` "mine\n". Start. Right-click `main` under Local branches and click "Rebase feature/topic onto main". Wait for the dialog. Screenshot. Click "Close". | **At the start of part A:** the working-changes row is row 0 ("Working changes"), its ring in Topic two's lane and colour (lane 2, colour 2), its dashed line down lane 2 to Topic two's node (row 4); R0's rows one lower; `git status --porcelain` prints ` M src/core.txt`.<br>**After step 2:** the item was enabled (uncommitted changes disable no rebase item, 1.1). The call log has `git rebase 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146`, exit 1, and no rebase folder after it: refused, nothing changed (1.4). The error dialog (`ErrorDialog`): `ErrorTitle` "Rebase failed"; `ErrorSummary` "Git rebases only a clean working tree: commit or stash your changes first."; `ErrorRepository` "In rebase"; `ErrorOutput` TA1; `ErrorCommand` `git rebase 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146`; "Copy output" (`ErrorCopyButton`) and "Close". Status "Rebase feature/topic onto main failed" in the danger colour; the log line (Warning) `rebase: Rebase feature/topic onto main: Failed after <N> ms: error: cannot rebase: You have unstaged changes.`. Git: HEAD on `feature/topic` at `b0dfa3f`; no `rebase-merge` folder and no `ORIG_HEAD`; `src/core.txt` still "core\nlocal edit\n" (` M src/core.txt`); the graph as at the start. The backup was saved before git ran (1.3): `git for-each-ref refs/visualcommit/backup` prints `rebase-<UTC time>/feature/topic` → `b0dfa3f…` (a rebase git refuses leaves its backup, which the clean-up takes in time: 14.3).<br>**After step 3:** the press selected "Other one"; the drop menu was A3 step 4's (the changed file disables nothing). The call log has `git config --type=bool --get rebase.autoStash` (exit 1: not set) and the status read, then no `git switch` and no `git rebase`: the app refused before the checkout (1.3, step 1). The error dialog: `ErrorTitle` "Rebase failed"; `ErrorSummary` "Git rebases only a clean working tree: commit or stash your changes first."; `ErrorRepository` "In rebase"; no git output, so no `ErrorOutput`, no `ErrorCommand` and no "Copy output" (D114); "Close" only. Status "Check out feature/other and rebase it onto main failed" in the danger colour. Nothing changed: `CurrentBranch` `feature/topic`, HEAD's reflog has no new line, ` M src/core.txt`, `feature/other` at `2d4d79a`; the backup refs are still only step 2's one. (Git's own checkout would have carried the change along: `git switch --no-guess feature/other` succeeds and leaves `M src/core.txt`, which is why the app checks first, 1.1.)<br>**After step 4:** the working-changes row shows (the untracked file), as in part A; `git status --porcelain` prints `?? docs/`. The call log has `git rebase 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146`, exit 1, no rebase folder. The error dialog: "Rebase failed"; `ErrorSummary` "Untracked files are in the way: move or remove them first."; "In rebase"; `ErrorOutput` TA2; `ErrorCommand` `git rebase 1dd58b8cfc17d4d9b80550eddd80b11ebf94c146`; "Copy output" and "Close". Status "Rebase feature/topic onto main failed"; the log line ends `error: The following untracked working tree files would be overwritten by checkout:`. Git: HEAD on `feature/topic` at `b0dfa3f`; `docs/main.txt` still "mine\n"; `git status -sb` prints `## feature/topic...origin/feature/topic` and `?? docs/`; one backup ref, `rebase-<UTC time>/feature/topic` → `b0dfa3f…`. |  | n/a: the error dialog is phase 3's, pictured in the real window by phase 3's checks; what differs here is its text, which both harnesses read alike, and the drop menu is A3's | Planned: `a5a-refused-changed-file`, `a5b-check-out-and-rebase-refused`, `a5c-refused-untracked-file` |  |
| B6 | The interactive list opened from the commit menu on Base: the menu, the header and count, rows newest on top, the base row, the right panel on a selected row, a commit's diff over the list closed by Esc, a row's action menu and Shift+F10, the bottom row's disabled Squash and Fixup, Cancel; interactive, dark, 1100×700 | 1. Interactive scenario (`Scenarios.InteractiveAsync`), 1100×700, dark. Right-click the row "Base" (row 6) in its Message column, at (468, 285). Screenshot. 2. Click "Interactive rebase of main from here…". Wait until the queue is idle and `InteractiveRebaseView` shows. Screenshot. 3. Press Down twice. Click the base row (`RebaseBaseRow`) at (600, 319). Screenshot. 4. In the right panel click the file row `three.txt`. Screenshot. Press Esc. Click the row "Three" (at (600, 199)). 5. Click the action button of the row "Three". Screenshot. Press Esc. Press Shift+F10. Press Esc. 6. Click the row "One" (at (600, 289)). Press S. Right-click the row "One" at the same point; move the pointer straight onto "Squash" and rest it there. Screenshot. Press Esc. 7. Click "Cancel" (`RebaseCancelButton`). Screenshot. | After step 1: G-I; "Base" (row 6) is selected and the details panel shows it. Base's commit menu as "Texts these checks rely on" gives it, its top-left at (470, 287), 284 high, "Check out this commit" highlighted. The log has one line `interactive: Context menu opened: 4b4622c Base by pointer`. After step 2: no menu, no dialog. The graph area shows the list L0 (see "The list's geometry"): header `Interactive rebase of main from 4b4622c` on the left and `6 commits` on the right, not overlapping (a header text that does not fit ends with "…" and has its whole text as tooltip, 2.2); six rows top to bottom Five, Four, Three, Fix one, Two, One, each with the grip, a `Pick` button, its short SHA and its subject at the positions given there; no author column; the base row `Base` `4b4622c` `Base` under One (y 304 to 334); the footer with `Cancel` and `Start rebase` right-aligned, Start's right edge at x 688, `Start rebase` disabled with the tooltip `Change an action or the order first` (read from the control, not pictured). The graph and its column headers do not show. Five (y 124 to 154) has the selection background `#2A3150` and the keyboard focus (2.2). The graph's selection moved to Five (`RepositoryViewModel.Selection` holds Five's full SHA alone), so the details panel shows Five: subject "Five", no body, Date 2026-01-01 12:07, no "Commit date" row, Parents `eef5c15`, Changed files (1): A `five.txt` `src`. Git unchanged. After step 3: the two Downs selected Three (y 184 to 214), with the keyboard focus (asserted before the click on the base row); Five and Four have no selection background. The click on the base row changed nothing (it is not selectable, 2.2, 14.3): Three is still selected and keeps the keyboard focus. The graph's selection is Three, and the details panel shows Three: subject "Three", the body `#42 is the issue`, Date 2026-01-01 12:05, Parents `ed58902`, Changed files (1): A `three.txt` `src`. Git unchanged. After step 4: the diff view (`DiffView`) covers the graph area over the list: header `A` `three.txt` `src`, the side label `ff42e7f`, the close button; one hunk adding the line `three`; the list is hidden behind it, the details panel still shows Three with the row `three.txt` selected. Esc closed the diff, not the list (while a diff is open over the list, the list's Esc handler declines and the diff's closes it, 14.3): no diff; the list is back as after step 3: Three selected, every action Pick, `Start rebase` disabled; no question was asked; the keyboard focus is back on Three's row, the list's selected row (14.3). The click on Three keeps it selected with the keyboard focus, as the real-window pass's click on it does before its step 5. After step 5: Three's menu: header `ff42e7f Three`; "Reword…" (highlighted); "Squash"; "Fixup"; "Drop"; all enabled, no "Pick" (Three's current action); 136 high, its top-left corner at the bottom-left corner of Three's action button (14.3). Esc closes it and gives the focus back to Three's row (phase 3's 4a; the action button is not focusable, 2.2); nothing changed (Three still Pick and selected). Shift+F10 opened the same menu (header `ff42e7f Three`, the same four items with "Reword…" highlighted, 136 high, at the same place); the log has exactly one line `interactive: Context menu opened: ff42e7f Three by keyboard` (D106); Esc closed it; nothing changed. After step 6: One is selected; S changed nothing (on the bottom row a squash cannot apply: One stays `Pick`, no menu, no dialog, `Start rebase` still disabled). One's menu, its top-left at (602, 291): header `fc6c8fd One`; "Reword…" (highlighted); "Squash" (disabled: "The oldest commit has nothing below it to join"); "Fixup" (disabled: "The oldest commit has nothing below it to join"); "Drop"; 136 high. The pointer rests on "Squash": both disabled items are in `VcTextDisabledBrush` (`#5F6775`), Squash's tooltip `The oldest commit has nothing below it to join` is shown inside the window, and "Reword…" is still highlighted (the pointer crossed no enabled item). Esc closes the menu; nothing changed. After step 7: no question (nothing was changed) and no dialog; the list is closed and the graph area shows G-I with its column headers. "One" (row 5), the commit last selected in the list, is selected, and the details panel shows One (Date 2026-01-01 12:02, Parents `4b4622c`, Changed files (1): A `one.txt` `src`); the keyboard focus is on the graph (2.2). Git unchanged; the call log has no `git rebase` call. |  | RW: steps 1, 2, 5, 6 and 7 in one launch on a fresh copy. Step 1: Base's row right-clicked with the real mouse at the constants' point (468, 285), the menu found by `ContextMenu` and compared with `b6a`. Step 2: the item clicked by its `ContextMenuItem`, `InteractiveRebaseView` awaited, its rows found by their `RebaseRow` names, the capture compared with `b6b`. Step 5: Three's row clicked with the real mouse at (600, 199), as the scripted step 4 ends, so both harnesses reach step 5 by the same input; its `RebaseActionButton` clicked, the menu compared with `b6e`, Esc (Shift+F10 is scripted only). Step 6: One's row clicked and right-clicked at its point; the pointer rests on "Squash" by phase 3's path (outside the menu, then sideways); the capture shows the tooltip inside the window and is compared with `b6f`; Esc. Step 7: `RebaseCancelButton` clicked, the graph compared with `b6g` | Planned: `b6a-commit-menu`, `b6b-list`, `b6c-three-selected`, `b6d-diff-over-list`, `b6e-action-menu`, `b6f-squash-disabled`, `b6g-list-closed`; real window: `b6a-commit-menu`, `b6b-list`, `b6e-action-menu`, `b6f-squash-disabled`, `b6g-list-closed` | Design 2.1, 2.2, 7.1, 10.4, 14.3; D141. Added to 13.4's B6: the commit menu itself (2.1), the diff over the list and Esc closing it (2.2, 14.3), the key `s` on the bottom row, Shift+F10 on a list row (2.2, D106) and Cancel on an unchanged list. The focus is asserted only where 2.2, 14.3 and phase 3's 4a fix it (design 0.6). |
| B7 | Editing the list and starting it: Fix one dragged right above One and made its fixup, Squash on Four (joins Three) with its message edited keeping `#42 is the issue`, Reword on Two (Cancel, then the dialog by its key), Drop on Five by its key, Start rebase: the history, the messages, the backup; interactive, dark, 1100×700 | 1. Interactive scenario, 1100×700, dark. Open the list as B6 steps 1 and 2. 2. Press Down three times. Press the left mouse button at the row "Fix one"'s point (600, 229) and, holding it, move to (600, 270) in 5 equal steps (`MoveMouseHeld(…, steps: 5)`: the first step is 8.2 px, past the 4 px threshold). Screenshot (button still held). 3. Release. Screenshot. 4. Click the action button of the row "Fix one". Screenshot. Click "Fixup". 5. Click the row "Four" (at (600, 169)); right-click it at the same point; click "Squash". 6. Click "Edit…" (`RebaseEditMessageButton`) on the second line under "Three". Screenshot. 7. In `MessageSummary` select all and press Delete. Type `Three and four`. Click `MessageDescription`, select all and type `#42 is the issue`. Screenshot. Click "Save". 8. Click the row "Two" (at (600, 251)); click its action button; click "Reword…". Screenshot. Press Esc. Click the row "Two" again (at (600, 251)) and press R; in `MessageSummary` select all, type `Two reworded` and press Enter. 9. Click the row "Five" (at (600, 139)). Press D. Screenshot. Click the row "Three" (at (600, 199)). 10. Click "Start rebase" (`RebaseStartButton`). Wait for the operation's log line. Screenshot. | After step 1: L0, Five selected. After step 2: the keys selected Fix one (y 214 to 244; the details panel shows Fix one: Date 2026-01-01 12:04, Parents `938844a`, Changed files (1): M `one.txt` `src`). While the button is held at (600, 270): Fix one is drawn at opacity 0.5 in its own place; a 2-px line in `VcDragTargetBrush` (`#7C8CFF`) marks the gap between Two and One (window y 274), the gap nearest the pointer; no row has moved; no menu, no dialog. After step 3: no drag line; the rows top to bottom are Five (124 to 154), Four (154 to 184), Three (184 to 214), Two (214 to 244), Fix one (244 to 274), One (274 to 304), then the base row (304 to 334); Fix one at full opacity, still selected, the details panel still on Fix one; every button still `Pick`; `Start rebase` enabled (the order changed). Git unchanged. After step 4: Fix one's menu: header `ed58902 Fix one`; "Reword…" (highlighted); "Squash"; "Fixup"; "Drop"; all enabled (Fix one is no longer the bottom row); 136 high, its top-left corner at the bottom-left corner of Fix one's action button (14.3). After "Fixup": no menu; Fix one's button reads `Fixup`, and its grip, button, SHA and subject stand 16 further right (the grip column from x 276, the button from 304, the SHA from 396, the subject from 468); a 2-px line in `VcLane0Brush` (`#7C8CFF`) at window x 272 runs from Fix one's vertical centre (y 259) to the top edge of One's row (y 274); One stays `Pick` with no second line (a group of fixups only keeps its target's message); nothing else moved. After step 5: Four is selected (the details panel shows Four); its menu had the header `eef5c15 Four` and "Reword…", "Squash", "Fixup", "Drop". After "Squash": Four's button reads `Squash`, indented as Fix one's, with a line at x 272 from Four's centre (y 169) to the top edge of Three's row (y 184); under Three a second line (y 214 to 236) reads `Message: Three`, with `Edit…` at its right; Two (236 to 266), Fix one (266 to 296), One (296 to 326) and the base row (326 to 356) moved down 22, and Fix one's line now runs from y 281 to y 296. After step 6: the message dialog over the window: title `Message of the squashed commit`; `MessageSummary` `Three`, with the keyboard focus and the caret at its end; `MessageSummaryCounter` `67`; `MessageDescription` holds three lines, `#42 is the issue`, an empty line, `Four` (git's squash message, Three's then Four's joined by a blank line, with Three's `#` line kept, D68); `Cancel` and `Save` (primary, enabled). After step 7: while the summary was empty the counter read `72` and `Save` was disabled with the tooltip `Write a summary` (read from the control); in the picture the summary is `Three and four`, the counter `58`, the description the one line `#42 is the issue`, `Save` enabled. After "Save": no dialog; Three's second line reads `Message: Three and four`; nothing else changed. After step 8: Two is selected and the details panel shows it (Date 2026-01-01 12:03, Parents `fc6c8fd`, Changed files (1): A `two.txt` `src`); its menu had the header `938844a Two` and its top-left corner at the bottom-left corner of Two's action button. The picture: the dialog titled `Reword 938844a`, `MessageSummary` `Two` with the focus and the caret at its end, the counter `69`, `MessageDescription` empty (its text is the empty string; its placeholder is not asserted: design 7.2 gives none), `Cancel` and `Save` (enabled). Esc closed it (the dialog takes Esc before the list does): Two's button still read `Pick` and Two had no second line (Cancel leaves the action as it was). R on the selected Two opened the same dialog (`Reword 938844a`, summary `Two`); Enter in the summary confirmed it: no dialog; Two's button reads `Reword`; under Two a second line (y 266 to 288) reads `New message: Two reworded`, with `Edit…` at its right; Fix one (288 to 318), One (318 to 348) and the base row (348 to 378) moved down 22 more, Fix one's line now from y 303 to y 318. After step 9: in the picture the list is L7 (see "The list's geometry"): Five selected, its button `Drop` and its SHA and subject struck through in `#5F6775`; the details panel shows Five; header `Interactive rebase of main from 4b4622c` and `6 commits` unchanged; `Start rebase` and `Cancel` enabled. Git is still unchanged: no write call has run. The click on Three then selected it (the details panel shows Three); nothing else changed. After step 10: no dialog and no banner; the list is closed and the graph area shows G-B7; "Three and four" (row 0) is selected (Three, the commit last selected, was one of main's commits before the rebase, so the new tip is selected, 1.5) and the keyboard focus is on the graph; the details panel shows subject "Three and four", the body `#42 is the issue`, Date 2026-01-01 12:05, Commit date 2026-01-02 09:00, Parents `b3507d3`, Changed files (2): A `four.txt` `src`, A `three.txt` `src`. The status bar says `Rebased main: 3 commits`; the log has `interactive: Interactive rebase of main: Succeeded after <N> ms: Rebased main: 3 commits`. Git: the app ran `git rebase -i 4b4622c8ca70c5cfe305043f40ea99c288fbefb3` once (exit 0); `git log --format=%H main` prints a081bffd034ab3d4fda4e1c073fe510a24f7c0d7, b3507d37adfe56baf58cf29ec0912ff444225b8c, ab4b49d005eb36535c58e54b01db091946b74b2e, 4b4622c8ca70c5cfe305043f40ea99c288fbefb3, da32da349cff2210ec951624b25f62825dd435ec; the messages (`git cat-file commit`, after the header) are exactly `Three and four` LF LF `#42 is the issue` LF, `Two reworded` LF and `One` LF; author dates 12:05, 12:03, 12:02 and commit dates 2026-01-02 09:00 for those three, author and committer Test Author <author@example.com>; a081bff adds src/four.txt and src/three.txt, b3507d3 adds src/two.txt, ab4b49d adds src/one.txt holding `one`, `fixed`; no commit of `main` has src/five.txt. HEAD is `ref: refs/heads/main`; `git status --short --branch` prints `## main`; no `rebase-merge/`, no `rebase-apply/`, nothing under `refs/rewritten/`. `git for-each-ref refs/visualcommit/` prints exactly one ref, `refs/visualcommit/backup/rebase-<time>/main`, pointing at 57f089d442eea23fee65c387f560002ab1ccd1fc (main's old tip). |  | RW: the whole check in one launch on a fresh copy, because Start needs every edit. Step 2's drag with the real mouse and keyboard: Down three times, the press at (600, 229), 5 moves with the button held, the capture compared with `b7a`, the release (in a `finally`), the result compared with `b7b`. The rows found by their `RebaseRow` names, the action and `Edit…` buttons by their ids, the menu items by `ContextMenuItem` names, Four right-clicked at its point. The dialogs typed with the real keyboard; Two's row clicked before its action button and again before R, which is pressed with the real keyboard; the reword dialog of step 8 compared with `b7f`. Three's row clicked, `RebaseStartButton` clicked, the operation's log line awaited, the graph compared with `b7h`, and git's state asserted as in the scripted run (the same SHAs: `RealApp.Launch` sets the same dates) | Planned: `b7a-drag-fix-one`, `b7b-fix-one-moved`, `b7c-fix-one-menu`, `b7d-squash-message`, `b7e-squash-message-edited`, `b7f-reword-dialog`, `b7g-list-edited`, `b7h-rebased`; real window: `b7a-drag-fix-one`, `b7b-fix-one-moved`, `b7f-reword-dialog`, `b7h-rebased` | Design 2.2, 2.4, 2.5, 7.2, 10.1, 10.2; the backup 1.2, the selection after it 1.5; D141, D143; dates D72. Proves the app's own sequence editor (13.5): the `#42` line kept. Added to 13.4's B7: Reword's Cancel (2.2), the letters `r` and `d`, the empty summary's disabled Save (7.2). Three is selected before Start; 1.5 would select the new tip after it for the dropped Five too. |
| B8 | Squash two selected commits: the several-commits menu, the dialog filled with both messages (the `#` line kept), the result with the later commit replayed; interactive, dark, 1100×700 | 1. Interactive scenario, 1100×700, dark. Click the row "Four" (row 1) in its Message column, at (468, 155); Ctrl+click the row "Three" (row 2) at (468, 181). 2. Right-click the row "Three" at (468, 181). Screenshot. 3. Click "Squash 2 commits…". Screenshot. 4. Click "Squash". Wait for the operation's log line. Screenshot. | After step 1: "Four" and "Three" are selected (both rows with the selection background); the details panel shows `MultiSelectionView`: `2 commits selected`, `eef5c15 Four`, `ff42e7f Three`, and the hint. After step 2: both stay selected (the right-clicked row is part of the multi-selection, phase 3's 4b). The several-commits menu as "Texts these checks rely on" gives it, its top-left at (470, 183), 145 high: header `2 commits`; "Squash 2 commits…" (highlighted); "Cherry-pick 2 commits onto main" (disabled: "Some of these commits are already on main"); "Revert 2 commits"; —; "Copy SHAs". After step 3: no menu; the message dialog: title `Squash 2 commits`; `MessageSummary` `Three` with the keyboard focus and the caret at its end; `MessageSummaryCounter` `67`; `MessageDescription` three lines, `#42 is the issue`, an empty line, `Four` (the commits' messages oldest first, joined by a blank line, Three's `#` line kept); `Cancel` and `Squash` (primary, enabled). The graph behind it is G-I with both rows selected; git unchanged. After step 4: no dialog, no banner; the graph is G-B8; "Three" `f34248d` (row 1), the squashed commit, is selected alone (design 3, the exception to 1.5: 14.3); the details panel shows subject "Three", the body `#42 is the issue`, an empty line, `Four`, Date 2026-01-01 12:05, Commit date 2026-01-02 09:00, Parents `ed58902`, Changed files (2): A `four.txt` `src`, A `three.txt` `src`. The status bar says `Squashed 2 commits into f34248d`; the log has `interactive: Squash 2 commits: Succeeded after <N> ms: Squashed 2 commits into f34248d`. Git: the app ran `git rebase -i ed58902397c0b9512ef667d9c4299a2cc8757906` once (from Three's parent, exit 0); `main` is at eb6f702889d08c7879c1daba704553fea072a42c ("Five", author 2026-01-01 12:07, commit date 2026-01-02 09:00, parent f34248d); f34248d062c44eadf27e78a47435b55161bf75d7's message is exactly `Three` LF LF `#42 is the issue` LF LF `Four` LF (git's own squash would have lost the `#` line, design 0.2), author 2026-01-01 12:05, commit date 2026-01-02 09:00, parent ed58902397c0b9512ef667d9c4299a2cc8757906; Fix one, Two, One, Base and Initial commit keep their SHAs; `git status --short --branch` prints `## main`; no `rebase-merge/`, nothing under `refs/rewritten/`; `git for-each-ref refs/visualcommit/` prints exactly one ref, `refs/visualcommit/backup/rebase-<time>/main` → 57f089d442eea23fee65c387f560002ab1ccd1fc. |  | n/a: the message dialog is pictured in the real window by B7 (the reword dialog) and the several-commits menu by phase 3's check 11, with one more item; the squash runs the same git call as B7's Start, whose result the real window asserts | Planned: `b8a-commits-menu`, `b8b-squash-dialog`, `b8c-squashed` | Design 3, 7.1, 7.2, 10.1, 10.2; the backup 1.2; D143; the multi-selection with the platform's command modifier D88. Git's own squash would lose the `#42` line (0.2). |
| B9 | Leaving the list: a row drag cancelled by Esc, Alt+Down and Alt+Up, Start disabled in the first order, Esc and Cancel on a changed list (the question, Keep editing, Discard: nothing changed), Esc on an unchanged list; then HEAD moved while the list was open; interactive, dark, 1100×700 | Part A (`Check_B9_leave_changed_list`): 1. Interactive scenario, 1100×700, dark. Open the list as B6 steps 1 and 2. 2. Press the left mouse button at the row "Five"'s point (600, 139) and, holding it, move to (600, 210) in 5 equal steps. While holding it, press Esc. Release. 3. Press Alt+Down. Screenshot. 4. Press Alt+Up. Rest the pointer on "Start rebase". Screenshot. 5. Press Alt+Down. Press Esc. Screenshot. 6. Click "Keep editing". 7. Click "Cancel" (`RebaseCancelButton`). Click "Discard". Screenshot. 8. Right-click the row "Base" (row 6) in its Message column, at (468, 285), and click "Interactive rebase of main from here…". Wait until the queue is idle and `InteractiveRebaseView` shows. Press Esc. Part B (a new copy, a new start, `Check_B9_head_moved_while_open`): 9. Interactive scenario, 1100×700, dark. Open the list as B6 steps 1 and 2. Press D. 10. Outside the app, commit through `TempRepo`: `CommitFileAsync("src/six.txt", "six\n", "Six")` (12:08). Wait until "Start rebase" is disabled (a condition wait, as phase 3's conventions say for a change made outside the app). Rest the pointer on "Start rebase". Screenshot. 11. Press Esc. Click "Discard". Screenshot. | After step 2: while held at (600, 210), Five was drawn at opacity 0.5 in its place and the drag line marked the gap between Three and Fix one (y 214). Esc cancelled the drag first: the line went, Five is drawn at full opacity where it was, no question opened, the list stayed open (2.2). The release moved nothing: the list is L0, Five selected, `Start rebase` disabled; no menu, no dialog. After step 3: Five moved one row down: Four (124 to 154), Five (154 to 184, still selected), Three, Fix one, Two, One, the base row, all `Pick`; `Start rebase` enabled; the details panel still shows Five. After step 4: the first order again (L0, Five on top and selected), so the list is no longer changed (14.3); `Start rebase` is disabled and its tooltip `Change an action or the order first` is shown inside the window. After step 5: Four above Five again; the question over the list: `ConfirmTitle` `Discard the changes to this list?`, no question text and no detail, `Keep editing` (`CancelButton`, with the keyboard focus) and `Discard` (`ConfirmButton`, danger). After step 6: no dialog; the list is still open as after step 5 (Four, Five, Three, Fix one, Two, One; Five selected; `Start rebase` enabled). After step 7: `Cancel` asked the same question (it asks as Esc does, 14.3); after "Discard": no dialog; the list is closed and the graph area shows G-I; "Five" (row 0), the commit last selected, is selected; the keyboard focus is on the graph. Git unchanged; the call log has no `git rebase` call. After step 8: the list opened (L0, Five selected) and Esc closed it at once: no question and no dialog (nothing was changed); G-I with Five selected. After step 9: Five's button reads `Drop`, its SHA and subject struck through; `Start rebase` enabled. After step 10: `main` is at c3cc217816ade59e0b8ccf262d83050f491ba812 ("Six", 2026-01-01 12:08, parent 57f089d). The list did not change: the same rows and actions, Five `Drop`, header `Interactive rebase of main from 4b4622c`, `6 commits` (it was read once, when it opened); `Start rebase` is disabled and its tooltip `HEAD moved: open the list again` is shown inside the window. No dialog. After step 11: the question as in step 5; after "Discard": no dialog, the list is closed, the graph is G-B9 (Six on top with `main`), "Five" (row 1) is still selected and the details panel shows it. Git: `main` at c3cc217, `## main`, nothing under `refs/visualcommit/`, no `rebase-merge/`; the call log has no `git rebase` call (nothing was started). |  | n/a: the question is phase 3's confirmation card with another cancel text, and the keys, Esc and a commit made outside need no real window (design 13.4) | Planned: `b9a-moved-by-keys`, `b9b-start-disabled`, `b9c-discard-question`, `b9d-discarded`, `b9e-head-moved`, `b9f-discarded-after-head-moved` | Design 2.2, 2.3, 7.2, 10.4; D141. Added to 13.4's B9: a drag cancelled by Esc, Alt+Up and Alt+Down, Keep editing, Cancel, an unchanged list, and part B (HEAD moved while the list is open). |
| C10 | A merge conflict end to end: the banner with Continue disabled, the resolver (layout, titles, both panes), a choice per block, the platform's undo, Use both, Mark resolved, Continue; dark, 1100×700 | One app start (`Check_C10_merge_conflict_end_to_end`). 1. `Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)`: a "conflicts" copy on which `git merge feature/theirs` stopped, run through `TempRepo` as a terminal would (12:10). Start the app at 1100×700, dark. Screenshot. 2. Rest the pointer on the banner's Continue and wait for its tooltip. Screenshot. 3. Move the pointer to the neutral point. Rest the pointer on the banner's text and wait for its tooltip. Screenshot. 4. Move the pointer to the neutral point (step 3's tooltip, open below the pointer, lies over the working-changes row and would take the click). Click the working-changes row (display row 0). Rest the pointer on the unstaged `notes.txt` and wait for its tooltip. Screenshot. 5. Move the pointer to the neutral point. Double-click the unstaged `notes.txt`; wait until the resolver shows it. Screenshot. 6. Click `Use ours` in the ours pane's bar of block 1. Screenshot. 7. Click the toolbar's next-conflict button (`ResolverNextConflict`). Screenshot. 8. Click `Use theirs` in the theirs pane's bar of block 2. Screenshot. 9. Press the platform's undo. Screenshot. 10. Click `Use both` in the ours pane's bar of block 2 (`ConflictUseBothOursFirst`). Screenshot. 11. Click `Mark resolved`; wait until idle. Screenshot. 12. Click the banner's Continue; wait until idle. Click display row 0 (the new merge commit). Screenshot. | After step 1: no dialog. The banner above the column headers, 440 wide: the warning icon; `BannerText` `Merging: "Merge branch 'feature/theirs'". 1 file has conflicts.`; `Continue` shown and disabled, its tooltip `Resolve the conflicts first (1 file)`; no Skip (git has no skip for a merge, 5.1); `Abort merge` enabled; the buttons in the order Continue, Abort merge, on a row of their own under the text, right-aligned, so the banner takes more than one line (this part's conventions); no text cut, nothing overlapping. The graph is GC-M; nothing selected; status "Ready"; the status bar's branch `main`. Git: `git status --porcelain` prints `UU notes.txt`; `.git/MERGE_HEAD` is `5c77ffb0ec78abf986d121f42013eee14422e5a5`; `MERGE_MSG` is `Merge branch 'feature/theirs'`, a blank line, `# Conflicts:`, `#⇥notes.txt`; notes.txt is FM; every ref as in GC0; no backup ref. After step 2: the tooltip `Resolve the conflicts first (1 file)` drawn inside the window under the pointer. After step 3: the tooltip `Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.` After step 4: the working-changes row selected; the stage panel: `Unstaged files (1, 1 with conflicts)` with U `notes.txt` (U in `VcDangerBrush`, no folder), its tooltip `Conflict: click to resolve it`; `Staged files (0)` and "No staged changes"; the summary `Merge branch 'feature/theirs'` and the description empty (5.3: `MERGE_MSG` less git's block); the commit button `Stage files to commit`, disabled. Resting on the row opened nothing. After step 5: the resolver over the graph and right-panel columns (x 260 to 1100, 840 wide), the left panel unchanged, `RightSplitter` and the right panel hidden; the banner above it, as wide as the resolver, with step 1's text and buttons, the buttons beside the text (whether the text takes one line is not stated); no text cut, nothing overlapping. Header: `U`, `notes.txt`, `ResolverFolder` empty, `Both modified`, `File 1 of 1`, the close button. Toolbar: `Conflict 1 of 2` with its arrows; `Take ours` and `Take theirs` enabled; `Merge tool…` disabled with `Set merge.tool in git's configuration to use a merge tool`; `Mark resolved` enabled. Titles `Ours: main` and `Theirs: feature/theirs`; the panes are PM (12 numbered rows each, a bar before blocks 1 and 2, no filler, the rows aligned); `Result` with `2 conflicts left`; the result's text is FM (R0) with FM's tints; the keyboard focus in the result (no focus outline) with the caret at the start of line 2 (block 1's `<<<<<<< HEAD`), not blinking; the panes show block 1's bar with only line 1 above it and the result starts at line 1. The double-click's second press changed nothing: the result is not modified, no `Resolve notes.txt` operation ran, and `git status --porcelain` still prints `UU notes.txt`. After step 6: the result is R1 and modified; `1 conflict left`; the toolbar reads `Conflict 1 of 2` (block 1 is still the block last reached: a choice does not move on); block 1's ours bar reads `Used in the result` and its row `line 2 ours` has `VcConflictUsedBrush`; block 1's theirs bar reads `Not used` and `line 2 theirs` has no tint; block 2's bars keep their buttons and its rows their ours and theirs tints; the focus in the result with the caret at the start of line 2 (`line 2 ours`). Nothing written: notes.txt on disk is still FM, `UU notes.txt`. After step 7: the toolbar reads `Conflict 2 of 2`; the caret at the start of line 10 (block 2's `<<<<<<< HEAD`); block 2's bars are in view in both panes and the result's lines 10 to 14 are in view (11.5). After step 8: the result is R2; `No conflicts left` in the result's title and in the toolbar; block 2's theirs bar `Used in the result`, `line 10 theirs` with the used tint; its ours bar `Not used`, `line 10 ours` untinted; block 1 as after step 6; nothing tinted in the result; the caret at the start of line 10. After step 9: the result is R1 again (one undo step), modified; `1 conflict left`; the toolbar reads `Conflict 2 of 2` again (block 2, reached in step 7, is the block last reached); block 2's bars show their buttons again and its rows their ours and theirs tints; block 1 as after step 6. After step 10: the result is R3; `No conflicts left`; both of block 2's bars read `Used in the result`, `line 10 ours` and `line 10 theirs` with the used tint; block 1 as after step 6. After step 11: no question (no block left, the file unchanged on disk). The app wrote R3 to notes.txt and ran `git add -- notes.txt`; status `Resolved notes.txt` (secondary). The resolver closed: the graph (GC-M, its working-changes row now "Working changes") and the right panel are back, the working-changes row still selected; the banner `Merging: "Merge branch 'feature/theirs'". Conflicts resolved: commit to finish the merge.` with `Continue` enabled and `Abort merge`, no Skip, the buttons on a row of their own under the text (more than one line); the stage panel: `Unstaged files (0)` and "No unstaged changes", `Staged files (1)` with M `notes.txt`, the summary `Merge branch 'feature/theirs'`, the button `Commit 1 file` enabled; the keyboard focus on the banner's Continue (`BannerContinueButton`, shown and enabled: 11.1). Git: `git status --porcelain` prints `M  notes.txt`; `MERGE_HEAD` is still there; notes.txt is R3; `git diff --cached --check -- notes.txt` prints nothing. After step 12: Continue asked nothing; the app ran `git commit --file=- --cleanup=whitespace` with `Merge branch 'feature/theirs'` (exit 0). No banner and no working-changes row; status `Merge committed`. The graph is GC10, display row 0 selected; its details: the message `Merge branch 'feature/theirs'`, Date 2026-01-02 09:00 (no Commit date row), Parents `da31c97` and `5c77ffb`, Changed files (1): M `notes.txt`. Git: `main` at `8a12c69`, message `Merge branch 'feature/theirs'` with no body, parents `da31c97` then `5c77ffb`, author and committer Test Author 2026-01-02 09:00; its notes.txt is R3; no `MERGE_HEAD` or `MERGE_MSG`; `git status --short --branch` prints only `## main`; no backup ref. | | RW: steps 1 and 4 to 12 in one launch on the same stopped copy, step 5 with a real double-click on the file (the result unchanged and not modified, `File 1 of 1`), the bars' buttons, the next-conflict button, Mark resolved and Continue clicked with the real mouse and the undo pressed with the real keyboard; the texts read by automation id and the result through its Value pattern | Planned: `c10a-banner`, `c10b-continue-reason`, `c10c-banner-tooltip`, `c10d-stage-panel`, `c10e-resolver-open`, `c10f-block1-ours`, `c10g-conflict-2`, `c10h-block2-theirs`, `c10i-undone`, `c10j-block2-both`, `c10k-resolved`, `c10l-merged`; real window: `c10a-banner`, `c10e-resolver-open`, `c10h-block2-theirs`, `c10i-undone`, `c10j-block2-both`, `c10k-resolved`, `c10l-merged` | Design 13.4 C10; 5.1, 5.2, 5.3, 5.4, 10.3, 10.4, 11.1 to 11.5, 11.7; D140 (the resolver across the graph and right-panel columns, the banner across it), D144 (Continue; the banner's layout, amending D122), D148 (the resolver works on the working-tree file; a click or double-click on a conflicted file opens it). The merge was started in a terminal (`ConflictsStoppedAsync(Merge)`) so that the marker reads `feature/theirs`, as 11.2's sketch shows. Step 5's double-click shows that a double-click on the file opens the resolver once and changes nothing. In the scripted run both presses reach the stage panel before the resolver shows (`ShellDriver.DoubleClick` sends them back to back), and in the real window it depends on timing, so this check does not prove 11.1's 500-ms guard itself: an automated test of the resolver proves it, by sending a press with click count 2 onto the open resolver's `ConflictUseTheirs` within 500 ms of its opening. D113 (singular counts). D122: rows found from the graph's bounds while the banner shows. Steps 3, 4 and 5 first move the pointer to the neutral point (this part's conventions, phase 3's check 9). |
| C11 | A rebase with two conflicting commits: the first stop's banner (1 of 2, Continue disabled, Skip), the panes with the rebase's titles, Use theirs, Mark resolved, Continue to the second stop, Use both, Mark resolved, Continue to the end: the graph and the backup; dark, 1100×700 | One app start (`Check_C11_rebase_with_two_conflicting_commits`). 1. `Scenarios.ConflictsAsync()`; before the app starts, `TempRepo.CheckoutAsync("feature/rebase")` (`git checkout --quiet feature/rebase`, as a terminal would). Start the app at 1100×700, dark. 2. Right-click the row `main` under Local branches in the left panel and click `Rebase feature/rebase onto main`; wait until idle. Screenshot. 3. Rest the pointer on the banner's text and wait for its tooltip. Screenshot. 4. Move the pointer to the neutral point (step 3's tooltip, open below the pointer, lies over the working-changes row and would take the click). Click the working-changes row (display row 0). Screenshot. 5. Click the unstaged `notes.txt`; wait until the resolver shows it. Screenshot. 6. Click `Use theirs` in the theirs pane's bar of block 1. Screenshot. 7. Click `Mark resolved`; wait until idle. Screenshot. 8. Click the banner's Continue; wait until idle. Screenshot. 9. Click the working-changes row, then the unstaged `notes.txt`; wait until the resolver shows it. Screenshot. 10. Click `Use both` in the theirs pane's bar of block 1 (`ConflictUseBothTheirsFirst`). Screenshot. 11. Click `Mark resolved`; wait until idle. Click the banner's Continue; wait until idle. Click row 0. Screenshot. | After step 1: the graph is GC0r; no banner, no working-changes row; nothing selected. After step 2: the right-click selected nothing (a left-panel row, D114). The app saved `refs/visualcommit/backup/rebase-<time>/feature/rebase` → `305f7f7d23b8d07c0a12c592b9b83d989a67a71a`, then ran `git rebase da31c977c80e9becdc908c74d973669aad1b3b8d` (exit 1; standard output `Auto-merging notes.txt` / `CONFLICT (content): Merge conflict in notes.txt`). No dialog. Status `Rebase stopped: 1 file has conflicts` in the warning colour. The banner, 440 wide: `Rebasing feature/rebase onto main: stopped at 70e8c78 "First change" (1 of 2). 1 file has conflicts.`; `Continue` disabled with `Resolve the conflicts first (1 file)`; `Skip` enabled; `Abort rebase` enabled; in that order, on a row of their own under the text, right-aligned (more than one line); no text cut, nothing overlapping. The graph is GC-R1; the status bar's branch `Detached at da31c97`. Git: `UU notes.txt`; HEAD detached at `da31c97`; `.git/rebase-merge/`: `head-name` `refs/heads/feature/rebase`, `onto` `da31c97…`, `orig-head` `305f7f7…`, `msgnum` 1, `end` 2, `stopped-sha` `70e8c78…`, `done` the one line `pick 70e8c78df98b8c7261e991c307061184f314fc78 First change`, `git-rebase-todo` the one line `pick 305f7f7d23b8d07c0a12c592b9b83d989a67a71a Second change`, `message` `First change` and an empty line, `author-script` with `GIT_AUTHOR_DATE='@1767268980 +0000'` (12:03); `REBASE_HEAD` `70e8c78…`; `MERGE_MSG` `First change`, a blank line, `# Conflicts:`, `#⇥notes.txt`; notes.txt is FR1; `feature/rebase` still at `305f7f7` and every other ref as in GC0. After step 3: the tooltip `Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts feature/rebase back where it was before the rebase.` After step 4: the stage panel: `Unstaged files (1, 1 with conflicts)` with U `notes.txt` (its tip `Conflict: click to resolve it`); `Staged files (0)`; the summary `First change` (`rebase-merge/message`), the description empty; the commit button `Continue rebase`, disabled, its tip `Resolve the conflicts first (1 file)`; Amend disabled with the tip `Finish or abort the rebase first` (both through `ToolTip.GetTip`, not pictured). After step 5: the resolver as in C10 step 5 except: `Conflict 1 of 1`; titles `Ours: main (the new base)` and `Theirs: 70e8c78 First change (your commit)`; the panes PR1; `1 conflict left`; the result FR1 with its tints, the caret at the start of line 2; the banner across the resolver with step 2's text and buttons, the buttons beside the text (whether the text takes one line is not stated); no text cut, nothing overlapping. After step 6: the result is the 12 lines of FR1's resolution (`line 2 first`, `line 10 ours`), modified; `No conflicts left` in the result's title and the toolbar; the theirs bar `Used in the result` with `line 2 first` in the used tint; the ours bar `Not used`, `line 2 ours` untinted. After step 7: no question; the app wrote the result and ran `git add -- notes.txt`; status `Resolved notes.txt`; the resolver closed; the banner `Rebasing feature/rebase onto main: stopped at 70e8c78 "First change" (1 of 2). Conflicts resolved: continue to commit it and go on.` with `Continue` and `Skip` enabled and `Abort rebase`, the buttons on a row of their own under the text (more than one line); the keyboard focus on the banner's Continue (as in C10 step 11); the working-changes row "Working changes"; the stage panel: `Unstaged files (0)`, `Staged files (1)` with M `notes.txt`, `Continue rebase` enabled. Git: `M  notes.txt`; still stopped at `70e8c78` (msgnum 1). After step 8: the app ran `git commit --no-verify --cleanup=whitespace --file=-` with `First change` and `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_AUTHOR_DATE` from `author-script` (Test Author, author@example.com, `@1767268980 +0000`), which made `99070f8`, then `git rebase --continue` (exit 1, `CONFLICT (content): Merge conflict in notes.txt`). No dialog. Status `Rebase stopped: 1 file has conflicts` (warning). The banner `Rebasing feature/rebase onto main: stopped at 305f7f7 "Second change" (2 of 2). 1 file has conflicts.` with `Continue` disabled (`Resolve the conflicts first (1 file)`), `Skip` and `Abort rebase` enabled, the buttons on a row of their own under the text (more than one line). The graph is GC-R2; the status bar's branch `Detached at 99070f8`. Git: `UU notes.txt`; HEAD `99070f8` (parent `da31c97`, author 2026-01-01 12:03, committer 2026-01-02 09:00, its notes.txt the step 6 result); `msgnum` 2, `end` 2, `stopped-sha` `305f7f7…`, `done` the two pick lines, `git-rebase-todo` empty, `rewritten-list` `70e8c78… 99070f8…`; `message` `Second change` and an empty line; notes.txt is FR2. After step 9: the stage panel's summary is `Second change`; the banner across the resolver with step 8's text and buttons, the buttons beside the text (whether the text takes one line is not stated); the resolver: `Conflict 1 of 1`; titles `Ours: main (the new base)` and `Theirs: 305f7f7 Second change (your commit)`; the panes PR2 (the bar before row 10); `1 conflict left`; the result FR2 with its tints, the caret at the start of line 10, block 1's bars and the result's lines 10 to 14 in view. After step 10: the result is the 13 lines of FR2's resolution (`line 10 second`, then `line 10 ours`), modified; `No conflicts left`; both bars `Used in the result`, both rows with the used tint. After step 11: Mark resolved asked nothing and ran `git add -- notes.txt` (status `Resolved notes.txt` until Continue); Continue asked nothing and ran `git commit --no-verify --cleanup=whitespace --file=-` with `Second change` and the author of `author-script` (`@1767269040 +0000`, 12:04), which made `2083808`, then `git rebase --continue` (exit 0; standard error ends `Successfully rebased and updated refs/heads/feature/rebase.`). No banner, no working-changes row, no dialog; status `Rebased feature/rebase onto main`; the status bar's branch `feature/rebase`. The graph is GC-Rend, row 0 selected; its details: Date 2026-01-01 12:04, a Commit date row 2026-01-02 09:00, Parents `99070f8`, Changed files (1): M `notes.txt`. Git: `git status --short --branch` prints only `## feature/rebase`; `git log --format="%h %s" -3 feature/rebase` prints `2083808 Second change`, `99070f8 First change`, `da31c97 Ours edits`; the tip's notes.txt is step 10's result; no `rebase-merge/`; `main` and the other branches as in GC0. `git for-each-ref refs/visualcommit/backup` prints exactly one ref, `refs/visualcommit/backup/rebase-<time>/feature/rebase` → `305f7f7d23b8d07c0a12c592b9b83d989a67a71a` (the old tip; its time parses). | | RW: steps 1 to 3 in one launch (the first stop's banner with Continue disabled, Skip and Abort rebase over the real graph area, and its tooltip under the real pointer); the left panel's row and the menu item found by name | Planned: `c11a-first-stop`, `c11b-banner-tooltip`, `c11c-stage-panel`, `c11d-resolver-first`, `c11e-first-theirs`, `c11f-first-resolved`, `c11g-second-stop`, `c11h-resolver-second`, `c11i-second-both`, `c11j-rebased`; real window: `c11a-first-stop`, `c11b-banner-tooltip` | Design 13.4 C11; 1.1 to 1.3 (the branch menu's item, the backup and no question, `git rebase <sha>`), 5.1 to 5.4, 10.1 to 10.4, 11.4; D135 (no question; the backup of the old tip), D142, D144 (Continue commits with `author-script`'s author and `--no-verify`, as git's own Continue). Before the app starts the copy checks out `feature/rebase` as a terminal would, so that `main`'s branch menu offers `Rebase feature/rebase onto main` and the app's own rebase saves its backup. Step 4 first moves the pointer to the neutral point (this part's conventions, phase 3's check 9). |
| C12 | A cherry-pick of three commits that stops at the second: the menu, the banner (1 more commit waits), the stage panel's `Continue cherry-pick`, the panes with the pick's titles, Use ours then a typed edit, Mark resolved, a message edited in the stage panel, Continue: the history and the edited message; dark, 1100×700 | One app start (`Check_C12_cherry_pick_list_stopped_and_continued`). 1. `Scenarios.ConflictsAsync()`; start the app at 1100×700, dark. Click row 2 ("Pick last") in its Message column, Shift+click row 4 ("Pick clean"), right-click row 4 in its Message column. Screenshot. 2. Click `Cherry-pick 3 commits onto main`; wait until idle. Screenshot. 3. Click the working-changes row (display row 0). Rest the pointer on the stage panel's commit button and wait for its tooltip. Screenshot. 4. Move the pointer to the neutral point. Click the unstaged `notes.txt`; wait until the resolver shows it. Screenshot. 5. Click `Use ours` in the ours pane's bar of block 1. Screenshot. 6. Press End; type ` and picked` (a space first); wait until the result's line 2 is `line 2 ours and picked` and the resolver has parsed the typed text (`ConflictResolverViewModel.IsParsePending` false, 14.3; never a fixed wait). Screenshot. 7. Click `Mark resolved`; wait until idle. Screenshot. 8. Select all in the summary box (`CommitSummary`) and type `Pick conflicting, resolved` (`ShellDriver.ReplaceText`); click the description box (`CommitDescription`) and type `#7 Line 2 keeps main's text.`; click `Continue cherry-pick`; wait until idle. Click row 1. Screenshot. | After step 1: rows 2, 3 and 4 selected (`VcSelectionBrush`), the right panel `3 commits selected`; the menu opened with its top-left 2 right of and 2 below the pointer, 145 high, header `3 commits`: `Squash 3 commits…` (disabled: "Only commits of main can be squashed"), `Cherry-pick 3 commits onto main` (highlighted, with the focus), `Revert 3 commits`, —, `Copy SHAs`. After step 2: the app made one `git cherry-pick` call, `git cherry-pick 01d2046adb60575079cdbe3d889a2ab3f408c643 01fd97607f151aa6e4877c0fa026633e97b65288 5c4c2576f7402a420fa3b4ce83fc72b9b40804e4` (oldest first; exit 1; standard output `[main 25c817e] Pick clean` … `CONFLICT (content): Merge conflict in notes.txt`). No dialog. Status `Cherry-picked 1 of 3 commits; stopped at 01fd976 with conflicts in 1 file` (warning). The banner `Cherry-picking 01fd976 "Pick conflicting". 1 file has conflicts. 1 more commit waits: Continue picks it.` with `Continue` disabled (`Resolve the conflicts first (1 file)`), `Skip` and `Abort cherry-pick` enabled, the buttons on a row of their own under the text, right-aligned (more than one line); no text cut, nothing overlapping. The graph is GC-P; the three original commits (display rows 4 to 6) still selected, `3 commits selected`. Git: `main` at `25c817e` (parent `da31c97`, its pick.txt `pick clean`); `CHERRY_PICK_HEAD` `01fd976…`; `MERGE_MSG` `Pick conflicting`, a blank line, `# Conflicts:`, `#⇥notes.txt`; `.git/sequencer/head` `da31c97…`, `abort-safety` `25c817e…`, `todo` `pick 01fd976 Pick conflicting` / `pick 5c4c257 Pick last`; `UU notes.txt`; notes.txt is FP; no last.txt. After step 3: the working-changes row selected; the stage panel: `Unstaged files (1, 1 with conflicts)` with U `notes.txt`; `Staged files (0)`; the summary `Pick conflicting`, the description empty; the commit button `Continue cherry-pick`, disabled, its tooltip `Resolve the conflicts first (1 file)` shown under the pointer; Amend disabled with `Finish or abort the cherry-pick first` (its tip, not pictured); the banner's tip `Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out and goes on. Abort puts main back where it was before the cherry-pick: the commits it already made are dropped too.` (`ToolTip.GetTip`). After step 4: the resolver as in C10 step 5 except: `Conflict 1 of 1`; titles `Ours: main` and `Theirs: 01fd976 Pick conflicting`; the panes PP; `1 conflict left`; the result FP with its tints, the caret at the start of line 2; the banner across the resolver with step 2's text and buttons, the buttons beside the text (whether the text takes one line is not stated); no text cut, nothing overlapping. After step 5: the result is `ConflictsFiles.NotesOurs` (12 lines), modified; `No conflicts left`; the ours bar `Used in the result` with `line 2 ours` in the used tint; the theirs bar `Not used`, `line 2 picked` untinted; the caret at the start of line 2. After step 6: the result's line 2 is `line 2 ours and picked`, every other line as after step 5; `No conflicts left`; modified; nothing written (notes.txt on disk is still FP, `UU notes.txt`). After the parse block 1's bars still show its choice (the typing was inside the chosen text and no undo brought its markers back, 14.3): the ours bar `Used in the result` with `line 2 ours` in the used tint, the theirs bar `Not used` with `line 2 picked` untinted; no line of the result is tinted. The picture is inspected for no text cut and nothing overlapping. After step 7: no question; the app wrote the result and ran `git add -- notes.txt`; status `Resolved notes.txt`; the resolver closed; the banner `Cherry-picking 01fd976 "Pick conflicting". Conflicts resolved: continue to commit it. 1 more commit waits: Continue picks it.` with `Continue`, `Skip` and `Abort cherry-pick` enabled, the buttons on a row of their own under the text (more than one line); the keyboard focus on the banner's Continue (as in C10 step 11); the working-changes row "Working changes"; the stage panel: `Unstaged files (0)`, `Staged files (1)` with M `notes.txt`, the summary `Pick conflicting`, `Continue cherry-pick` enabled. Git: `M  notes.txt`; `CHERRY_PICK_HEAD` still there. After step 8: no question (no staged marker); the app ran `git commit --file=- --cleanup=whitespace` with `Pick conflicting, resolved`, a blank line, `#7 Line 2 keeps main's text.` (which made `d512905`), then `git cherry-pick --continue` (exit 0, standard output `[main 9d814c4] Pick last` …). No banner, no working-changes row; status `Cherry-pick finished`. The graph is GC-Pend, row 1 selected; its details: the message `Pick conflicting, resolved` with the description `#7 Line 2 keeps main's text.` (its `#` line kept, D68), Date 2026-01-01 12:06 (git kept Pick conflicting's author date), a Commit date row 2026-01-02 09:00, Parents `25c817e`, Changed files (1): M `notes.txt`. Git: `git status --short --branch` prints only `## main`; `git log --format="%h %s" -4 main` prints `9d814c4 Pick last`, `d512905 Pick conflicting, resolved`, `25c817e Pick clean`, `da31c97 Ours edits`; `git cat-file commit d512905` ends with the message `Pick conflicting, resolved` / (blank) / `#7 Line 2 keeps main's text.`; its notes.txt is step 6's result; last.txt is `pick last`; no `CHERRY_PICK_HEAD`, `MERGE_MSG` or `sequencer/`; no backup ref. | | n/a: the banner, the stage panel and the resolver are pictured in the real window by C10 and C11, and real typing into the result is C14's | Planned: `c12a-menu`, `c12b-stopped`, `c12c-stage-panel`, `c12d-resolver`, `c12e-ours`, `c12f-typed`, `c12g-resolved`, `c12h-finished` | Design 13.4 C12; section 4 (one call for the list, its stop read from git's files), 5.1 to 5.4, 7.1, 11.4, 11.5; D110 (the commits that wait: `sequencer/todo` less one), D136, D145; D144 (the stage panel's `Continue cherry-pick`); D68 (the description's `#` line kept in the commit). Step 4 first moves the pointer to the neutral point (this part's conventions). Step 6 waits on `ConflictResolverViewModel.IsParsePending` (14.3). |
| C13 | Skip and Abort: a rebase stopped at First change skipped (its question, the `skip` backup, the stop at Second change) then aborted; a list of picks stopped and aborted (the whole list undone); "Check out feature/rebase and rebase it onto main" stopped and aborted (the branch the user was on checked out again); dark, 1100×700 | Three app starts. Part A (`Check_C13_skip_then_abort_a_rebase`): 1. `Scenarios.ConflictsStoppedAsync(ConflictsStop.Rebase)`: on `feature/rebase`, `git rebase main` stopped at First change, through `TempRepo` as a terminal would (12:10). Start the app at 1100×700, dark. Screenshot. 2. Click the banner's Skip. Screenshot. 3. Click the dialog's `Skip`; wait until idle. Screenshot. 4. Click the banner's `Abort rebase`. Screenshot. 5. Click the dialog's `Abort rebase`; wait until idle. Screenshot. Part B (`Check_C13_abort_a_pick_list`): 6. `Scenarios.ConflictsStoppedAsync(ConflictsStop.CherryPicks)`: on `main`, `git cherry-pick` of the three `feature/picks` commits stopped at Pick conflicting (12:10). Start the app. Screenshot. 7. Click the banner's `Abort cherry-pick`. Screenshot. 8. Click the dialog's `Abort cherry-pick`; wait until idle. Screenshot. Part C (`Check_C13_abort_check_out_and_rebase`): 9. `Scenarios.ConflictsAsync()`; start the app. Press the left mouse button at the centre of the label `feature/rebase` on row 5 ("Second change") and, holding it, move to the centre of the label `main` on row 0 ("Ours edits") in 5 equal steps (`MoveMouseHeld(…, steps: 5)`); release. Screenshot. 10. Click `Check out feature/rebase and rebase it onto main`; wait until idle. Screenshot. 11. Click the banner's `Abort rebase`. Screenshot. 12. Click the dialog's `Abort rebase`; wait until idle. Screenshot. | After step 1: the banner `Rebasing feature/rebase onto main: stopped at 70e8c78 "First change" (1 of 2). 1 file has conflicts.` (the terminal named `main`; `onto` is its tip) with `Continue` disabled (`Resolve the conflicts first (1 file)`), `Skip` and `Abort rebase` enabled, the buttons on a row of their own under the text, right-aligned (more than one line); no text cut, nothing overlapping. The graph is GC-R1; status "Ready"; the status bar's branch `Detached at da31c97`. Git: as C11 after step 2 (the same `rebase-merge/` files, `UU notes.txt`, notes.txt FR1); no backup ref. After step 2: the question: `ConfirmTitle` `Skip 70e8c78?`; `ConfirmQuestion` `"First change" is left out of the rebase, and changes made to resolve its conflicts are lost.`; `ConfirmDetail` `A backup is saved first, so this can be undone.`; `Cancel` with the keyboard focus and `Skip` (danger). Nothing has changed; no backup ref. After step 3: the app saved `refs/visualcommit/backup/skip-<time>` → `da31c977c80e9becdc908c74d973669aad1b3b8d` (HEAD) and `skip-<time>-changes` → a commit whose only parent is `da31c97` and whose notes.txt is FR1, markers included (every other file as at `da31c97`), then ran `git rebase --skip` (exit 1, `CONFLICT (content): Merge conflict in notes.txt`). No dialog. Status `Rebase stopped: 1 file has conflicts` (warning). The banner `Rebasing feature/rebase onto main: stopped at 305f7f7 "Second change" (2 of 2). 1 file has conflicts.` with `Continue` disabled (`Resolve the conflicts first (1 file)`), `Skip` and `Abort rebase` enabled, the buttons on a row of their own under the text (more than one line). The graph is GC-R1 again (the skip made no commit; HEAD is still `da31c97`). Git: `UU notes.txt`; HEAD detached at `da31c97`; `msgnum` 2, `end` 2, `stopped-sha` `305f7f7…`, `done` both pick lines (the skipped one included), `git-rebase-todo` empty, `rewritten-list` `70e8c78… da31c97…`; notes.txt is FS2 (`line 2 ours`: First change is left out); `feature/rebase` still at `305f7f7`. After step 4: `ConfirmTitle` `Abort the rebase?`; `ConfirmQuestion` `Abort the rebase of feature/rebase? The branch goes back to where it was before it started.` (no return sentence: a terminal started this rebase); `ConfirmDetail` `A backup is saved first, so this can be undone.`; `Cancel` with the focus and `Abort rebase` (danger). After step 5: the app saved `abort-<time>` → `da31c97…` and `abort-<time>-changes` → a commit whose only parent is `da31c97` and whose notes.txt is FS2, then ran `git rebase --abort` (exit 0, no output). No banner, no working-changes row; status `Aborted the rebase`. The graph is GC0r; the status bar's branch `feature/rebase`. Git: `git status --short --branch` prints only `## feature/rebase`; HEAD on `feature/rebase` at `305f7f7`; notes.txt is Second change's (`ConflictsFiles.NotesSecond`: `line 2 first`, `line 10 second`); no `rebase-merge/`; every branch as in GC0. `git for-each-ref refs/visualcommit/backup` prints four refs, in this order: `abort-<t2>`, `abort-<t2>-changes`, `skip-<t1>`, `skip-<t1>-changes`, with t1 before t2. After step 6: the banner `Cherry-picking 01fd976 "Pick conflicting". 1 file has conflicts. 1 more commit waits: Continue picks it.` with `Continue` disabled (`Resolve the conflicts first (1 file)`), `Skip` and `Abort cherry-pick` enabled, the buttons on a row of their own under the text (more than one line). The graph is GC-PB; status "Ready"; the status bar's branch `main`. Git: `main` at `d71cdc4`; `CHERRY_PICK_HEAD` `01fd976…`; `sequencer/head` `da31c97…`, `abort-safety` `d71cdc4…`, `todo` the two pick lines; `UU notes.txt`; notes.txt is FP. After step 7: `ConfirmTitle` `Abort the cherry-pick?`; `ConfirmQuestion` `Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost. Commits this cherry-pick already made are dropped too.` (`sequencer/` exists and HEAD equals `abort-safety`); the detail; `Cancel` with the focus and `Abort cherry-pick` (danger). After step 8: the app saved `abort-<time>` → `d71cdc4e9eb7fa80dc917e6f859204a0286e78ac` and `abort-<time>-changes` → a commit whose only parent is `d71cdc4` and whose notes.txt is FP, then ran `git cherry-pick --abort` (exit 0). No banner, no working-changes row; status `Aborted the cherry-pick`. The graph is GC0 (Pick clean's copy is gone from it; only the backup ref reaches it). Git: `## main`; `main` at `da31c97`; no pick.txt; notes.txt is `ConflictsFiles.NotesOurs`; no `CHERRY_PICK_HEAD`, `MERGE_MSG` or `sequencer/`. After step 9: the press selected row 5 ("Second change"); the drop menu opened with its top-left 2 right of and 2 below the release point, 171 high: header `feature/rebase → main`; `Merge feature/rebase into main` (highlighted); `Merge feature/rebase into main (create a merge commit)`; `Fast-forward main to feature/rebase` (disabled: "main has commits that feature/rebase does not have"); —; `Check out feature/rebase and rebase it onto main`; `Check out feature/rebase and rebase it onto main interactively…`. After step 10: one operation `Check out feature/rebase and rebase it onto main`: the app read `git config --type=bool --get rebase.autoStash` (unset) and a clean status, ran `git switch --no-guess feature/rebase`, saved `refs/visualcommit/backup/rebase-<time>/feature/rebase` → `305f7f7…`, ran `git rebase da31c977c80e9becdc908c74d973669aad1b3b8d` (exit 1) and wrote `.git/rebase-merge/visualcommit-return` holding `refs/heads/main` and a line feed (14.3). No dialog. Status `Rebase stopped: 1 file has conflicts` (warning). The banner as after step 1 (the buttons on a row of their own under the text). The graph is GC-R1, Second change (display row 6) still selected; the status bar's branch `Detached at da31c97`. Git: the `rebase-merge/` files as in C11 after step 2; notes.txt FR1. After step 11: `ConfirmTitle` `Abort the rebase?`; `ConfirmQuestion` `Abort the rebase of feature/rebase? The branch goes back to where it was before it started, and main is checked out again.`; the detail; `Cancel` with the focus and `Abort rebase` (danger). After step 12: the app read `visualcommit-return`, saved `abort-<time>` → `da31c97…` and `abort-<time>-changes` → a commit whose only parent is `da31c97` and whose notes.txt is FR1, ran `git rebase --abort` (exit 0), then `git switch --no-guess main` (exit 0). No banner, no working-changes row; status `Aborted the rebase`. The graph is GC0 (`main` ✓), Second change still selected; the status bar's branch `main`. Git: `## main`; `main` at `da31c97`, `feature/rebase` at `305f7f7`; clean; no `rebase-merge/`. `git for-each-ref refs/visualcommit/backup` prints three refs: `abort-<t2>`, `abort-<t2>-changes`, `rebase-<t1>/feature/rebase` → `305f7f7…`, with t1 before t2. | | n/a: no screen the real window has not shown by then (C11 pictures the banner with Skip, C10 the resolver, phase 3's check 9 the confirmation layer, A3 the drop menu), and the stopped states come from `TempRepo` or from menus the scripted run drives the same way | Planned: `c13a-rebase-stopped`, `c13b-skip-question`, `c13c-second-stop`, `c13d-abort-question`, `c13e-aborted`, `c13f-picks-stopped`, `c13g-abort-question`, `c13h-picks-aborted`, `c13i-drop-menu`, `c13j-rebase-stopped`, `c13k-abort-question`, `c13l-returned` | Design 13.4 C13; 1.3 (the Abort of a rebase the app began with a checkout, `visualcommit-return`), 5.1, 7.2, 10.1, 10.2; D83 (Abort asks and saves `abort-<time>` and its `-changes`), D110 (the list's Abort question while HEAD is at `abort-safety`), D144 (Skip asks and saves `skip-<time>` and its `-changes`, amending D83's "Nothing else confirms"), D142 (Abort checks out again the branch the user was on). Parts A and B start from terminal stops (`ConflictsStoppedAsync`), so their Abort questions have no return sentence. |
| C14 | Editing the result freely: typing, the platform's undo and redo, Esc inside the result, the result's and a pane's menus, Take theirs and its undo, Mark resolved with a block left, leaving with a modified result, Stage all and Continue with markers left; the merge of `feature/theirs` stopped, dark, 1100×700 | 1. `ConflictsStoppedAsync(ConflictsStop.Merge)`, 1100×700, dark. Click the working-changes row; click `notes.txt` in the unstaged list. 2. Click in the result at line 2, column 1; press Shift+Down five times; type `line 2 edited by hand`; press Enter. Wait until the resolver has parsed the typed text (`ConflictResolverViewModel.IsParsePending` false, 14.3; the real window, which cannot read it, waits until `ConflictResultCount` reads `1 conflict left`). Screenshot (`c14a-typed`). 3. Press Esc. Settle; wait until idle. Right-click in the result at line 5, column 1. Screenshot (`c14b-result-menu`). Press Esc. Right-click the ours pane's text row 1 (`line 1`). Screenshot (`c14c-pane-menu`). Press Esc. 4. In block 2's theirs bar, click `Use theirs`. Screenshot (`c14d-used-theirs`). 5. The platform's undo. Screenshot (`c14e-undone`). 6. The platform's redo; then the platform's undo again. 7. Click `Take theirs` (`ResolverTakeTheirs`). Screenshot (`c14f-take-theirs`). The platform's undo. 8. Click `Mark resolved`. Screenshot (`c14g-markers-question`). Click `Cancel`. 9. Click the close button (`ResolverCloseButton`). Screenshot (`c14h-leave-question`). Click `Keep editing`. Click the close button again; click `Discard`. Screenshot (`c14i-closed`). 10. Click `Stage all` (`StageAllButton`). Screenshot (`c14j-stage-all-question`). Click `Cancel`. Click `Stage all` again; click `Mark resolved`. Wait until idle. Screenshot (`c14k-staged-with-markers`). 11. Click the banner's `Continue`. Screenshot (`c14l-continue-question`). Click `Cancel`. | **When the app has started:** graph GC-M with `Working changes · 1 conflict`. The banner reads `Merging: "Merge branch 'feature/theirs'". 1 file has conflicts.`, with `Continue` (disabled: `Resolve the conflicts first (1 file)`) and `Abort merge` below the text. Git: `UU notes.txt`; notes.txt is FM. **After step 1:** the resolver is open over the graph and right-panel columns (x 260 to 1100). The banner runs across it with its buttons beside the text. Header: `U`, `notes.txt`, `Both modified`, `File 1 of 1`. Toolbar: `Conflict 1 of 2`, `Take ours` and `Take theirs` enabled, `Merge tool…` disabled (`ToolTip.GetTip`: `Set merge.tool in git's configuration to use a merge tool`), `Mark resolved` enabled. The panes are PM. The result is FM (20 lines) with `2 conflicts left`: lines 2, 4, 6, 14, 16 and 18 are on `#3A2E16` with their text in `#E0A23B`, lines 3 and 15 on `#15323A`, lines 5 and 17 on `#2C2140`. The keyboard focus is in the result, the caret at the start of line 2 (block 1's `<<<<<<< HEAD`). **After step 2:** the result has 16 lines:<br>1 `line 1`, 2 `line 2 edited by hand`, 3 `line 3`, 4 `line 4`, 5 `line 5`, 6 `line 6 theirs`, 7 `line 7`, 8 `line 8`, 9 `line 9`, 10 `<<<<<<< HEAD`, 11 `line 10 ours`, 12 `=======`, 13 `line 10 theirs`, 14 `>>>>>>> feature/theirs`, 15 `line 11`, 16 `line 12`.<br>`1 conflict left`, and `ResolverConflictPosition` reads `Conflict 1 of 2` (`m` counts the blocks as read; block 1, reached on opening, is the block last reached: 14.3). Block 1's markers were edited away, so both its bars say `Not used`, and its lines (`line 2 ours` in the ours pane, `line 2 theirs` in the theirs pane) have no tint. Block 2's bars show their buttons; `line 10 ours` is on `#15323A` and `line 10 theirs` on `#2C2140`. In the result, lines 10, 12 and 14 are on `#3A2E16` with `#E0A23B` text, line 11 on `#15323A`, line 13 on `#2C2140`, and the other lines have no tint. The panes still show the file as it was read. The caret (a 1-px `#E4E7EC` line, not blinking) is at line 3, column 1, the focus is in the result, and `IsModified` is true. Nothing written: notes.txt is still FM and git says `UU notes.txt`. **After step 3:** Esc changed nothing: the resolver is open, with no dialog and no menu; the result is step 2's, the caret is at line 3, column 1, and the focus is still in the result. The right-click in the result opened the result's menu (11.12, 14.3), 197 high and at least 4 px inside the window; it does not fit below the pointer at line 5, so it opens above it (phase 3's 4a): its left edge 2 right of the pointer, its bottom edge 2 above it: header `notes.txt (result)`; `Undo` (highlighted: the typing can be undone); `Redo` (disabled: "Nothing to redo"); —; `Cut` (disabled: "Select some text first"); `Copy` (disabled: "Select some text first"); `Paste`; `Select all`. Esc closed it; the result is still step 2's. The right-click on the ours pane opened its menu: header `Ours: main`; `Copy` (disabled: "Select some text first"); 58 high; no item highlighted. Esc closed it; nothing changed. **After step 4:** the result has 12 lines: 1 to 9 as after step 2, 10 `line 10 theirs`, 11 `line 11`, 12 `line 12`. `ConflictResultCount` and `ResolverConflictPosition` both read `No conflicts left`. Block 2's theirs bar says `Used in the result`, and `line 10 theirs` in the theirs pane is on `#1A2E24`. Its ours bar says `Not used`, and `line 10 ours` has no tint. Block 1's bars still say `Not used`. No line of the result is tinted (`TintAt` gives `None` for each). The focus is in the result, with no focus outline, and the caret is at the start of line 10, the start of the replaced text (a choice does not move on). Nothing written. **After step 5:** the undo took back only the choice, one step. The result is step 2's 16 lines again, with the hand edit kept, `1 conflict left`, and the toolbar's `Conflict 1 of 2` (block 1 is still the block last reached). Block 2's bars show their buttons again, its lines have their side's tint, and the result's lines 10 to 14 are tinted as after step 2. **After step 6:** the redo gives step 4's 12 lines and `No conflicts left` (in the result's title and the toolbar); the undo gives step 5's 16 lines, `1 conflict left` and `Conflict 1 of 2`. **After step 7:** `Take theirs` replaced the whole result with feature/theirs' notes.txt (stage 3, blob `6216606`, read with `git cat-file --filters --path=notes.txt`), 12 lines: 1 `line 1`, 2 `line 2 theirs`, 3 `line 3`, 4 `line 4`, 5 `line 5`, 6 `line 6 theirs`, 7 `line 7`, 8 `line 8`, 9 `line 9`, 10 `line 10 theirs`, 11 `line 11`, 12 `line 12`. `ConflictResultCount` and `ResolverConflictPosition` read `No conflicts left`, and no line of the result is tinted. Every block's bars are as if `Use theirs` had been chosen in it (11.6, 14.3): both theirs bars say `Used in the result`, and `line 2 theirs` and `line 10 theirs` in the theirs pane are on `#1A2E24`; both ours bars say `Not used`, and `line 2 ours` and `line 10 ours` have no tint. Nothing written: notes.txt is still FM, and git says `UU notes.txt`. The undo, one step, gives step 5's 16 lines, `1 conflict left` and `Conflict 1 of 2`, with step 5's bars and tints (block 1 `Not used` on both bars, block 2's buttons and side tints). **After step 8:** the question over the backdrop: `ConfirmTitle` `notes.txt still has conflict markers.`; `ConfirmQuestion` `Mark it resolved anyway? The markers are committed with it.`; no `ConfirmDetail`; `Cancel` with the keyboard focus, and `Mark resolved` (primary, not danger). After Cancel: no dialog, the result is unchanged (16 lines), and nothing is written. **After step 9:** the question: `Discard your changes to notes.txt?` / `The result has not been marked resolved.` / no detail / `Keep editing` with the focus, and `Discard` (danger). After Keep editing: the resolver is still open with the result unchanged. After Discard, the resolver has closed without writing:<br>- graph GC-M is back, its working-changes row `Working changes · 1 conflict` selected;<br>- the stage panel is back: `Unstaged files (1, 1 with conflicts)` with U `notes.txt` (tooltip `Conflict: click to resolve it`, through `GetTip`), and `Staged files (0)` with "No staged changes";<br>- the summary is `Merge branch 'feature/theirs'` and the description is empty;<br>- the button `Stage files to commit` is disabled;<br>- the keyboard focus is on the graph: the banner's Continue is shown but disabled (11.1);<br>- the banner is as when the app started, buttons below the text.<br>Git is unchanged: `UU notes.txt`, notes.txt FM. **After step 10:** the question: `notes.txt still has conflict markers.` / `Mark it resolved anyway? The markers are committed with it.` / no detail / `Cancel` with the focus, and `Mark resolved` (primary). After Cancel, nothing is staged (`UU notes.txt`). After Mark resolved, the app ran `git add --all -- notes.txt` (exit 0):<br>- `git status --porcelain=v1 --untracked-files=all` prints `M  notes.txt`;<br>- the staged blob is `cdd7b39`, FM with its markers;<br>- MERGE_HEAD is still `5c77ffb…` and HEAD is `da31c97`.<br>The banner reads `Merging: "Merge branch 'feature/theirs'". Conflicts resolved: commit to finish the merge.`, with `Continue` (enabled, no tooltip) and `Abort merge` below the text. The working-changes row reads `Working changes`. `Unstaged files (0)` with "No unstaged changes"; `Staged files (1)`: M `notes.txt`. The button `Commit 1 file` is enabled. Status "Ready". **After step 11:** before any operation, the app ran `git diff --cached --check --no-color --no-ext-diff -- notes.txt`, which exits 2 with `notes.txt:2: leftover conflict marker` and the same for lines 4, 6, 14, 16 and 18. It then asks: `1 file still has conflict markers.` / `Continue anyway? The markers are committed with it.` / no detail / `Cancel` with the focus, and `Continue` (primary). After Cancel, nothing is committed: HEAD is `da31c97` on main, MERGE_HEAD is present, `M  notes.txt`, the banner is unchanged, and no `Continue the merge` operation was logged. | | RW: steps 1 to 3 at 1100×700 (step 3's two right-clicks are scripted only). A real click on the working-changes row and on `notes.txt`, a real click at line 2, column 1, then Shift+Down, typing and Enter on the real keyboard, and a real Esc: `c14a-typed`. Before and after Esc, the result's Value pattern holds step 2's 16 lines, and after Esc the `ConflictResolver` element is still there with no dialog element | Planned: `c14a-typed`, `c14b-result-menu`, `c14c-pane-menu`, `c14d-used-theirs`, `c14e-undone`, `c14f-take-theirs`, `c14g-markers-question`, `c14h-leave-question`, `c14i-closed`, `c14j-stage-all-question`, `c14k-staged-with-markers`, `c14l-continue-question`; real window: `c14a-typed` | Design 13.4 C14; 5.1 (Continue's marker check), 5.3, 7.1 and 7.2 (the questions), 11.1, 11.2, 11.5, 11.6, 11.7, 11.10, 11.12, 14.3; D148 (Esc does nothing inside the result; staging a file with markers asks wherever it is done). Added to 13.4's C14: the result's and a pane's menus, `Take theirs` and its undo, Continue with markers staged. Step 2 waits on `ConflictResolverViewModel.IsParsePending` (14.3). |
| C15 | Conflicts without text: the merge of `feature/kinds` with a file deleted by them, a binary file and a file added by both; the cards, Keep the file, Use theirs, the add/add file in the panes, every file resolved; dark, 1100×700 | 1. `ConflictsStoppedAsync(ConflictsStop.MergeKinds)`, 1100×700, dark. Click the working-changes row. Screenshot (`c15a-kinds-stage-panel`). 2. Click `other.txt` in the unstaged list. Screenshot (`c15b-deleted-by-them`). 3. Click `Keep the file`. Wait until idle. Screenshot (`c15c-binary-card`). 4. Click `Use theirs` (`ConflictUseTheirsFile`). Wait until idle. Screenshot (`c15d-both-added`). 5. In the ours pane's bar, click `Use both`; click `Mark resolved`. Wait until idle. Screenshot (`c15e-all-resolved`). | **After step 1:** the banner reads `Merging: "Merge branch 'feature/kinds'". 3 files have conflicts.`, with `Continue` (disabled: `Resolve the conflicts first (3 files)`) and `Abort merge` below the text. Graph GC-M, its working-changes row `Working changes · 3 conflicts` selected. The stage panel shows `Unstaged files (3, 3 with conflicts)`: U `image.png`, U `new.txt`, U `other.txt`, each `U` in the danger colour and each row with the tooltip `Conflict: click to resolve it` (`GetTip`). `Staged files (0)` shows "No staged changes". The summary is `Merge branch 'feature/kinds'` and the description is empty. `Stage files to commit` is disabled. Git: `UU image.png`, `AA new.txt`, `UD other.txt`, as listed under "The kinds merge". **After step 2:** the resolver is open on other.txt over the graph and right-panel columns, the banner across it with its buttons beside the text. Header: `U`, `other.txt`, `Deleted by them`, `File 3 of 3`. The toolbar shows none of the conflict position and arrows, `Take ours`, `Take theirs`, `Merge tool…` or `Mark resolved` (git runs no tool for a deletion); the toolbar row stays, 36 high and empty (14.3). There are no panes and no result. `ConflictCard` sits in the middle of the body: `ConflictCardText` `main changed other.txt; feature/kinds deleted it.`, then `Keep the file` and `Delete it`. Git is unchanged. **After step 3:** the operation `Resolve other.txt` ran `git add -- other.txt` (exit 0). The file was there, so there was no checkout. Status `Resolved other.txt`. The working-tree other.txt is unchanged (`other file`, `changed on main`), and the index holds `ec92a8c` at stage 0. That is main's blob, so `git status` lists other.txt no more. No backup ref. The banner now ends `2 files have conflicts.`, Continue's reason `Resolve the conflicts first (2 files)`. The resolver moved on to the next conflicted file in the stage list's order, wrapping, which is image.png. Header: `U`, `image.png`, `Both modified`, `File 1 of 2`. The toolbar shows only `Merge tool…`, disabled (`GetTip`: `Set merge.tool in git's configuration to use a merge tool`). The card reads `image.png is a binary file: its two versions cannot be merged line by line.`, with `Use ours` and `Use theirs`. **After step 4:** the operation `Resolve image.png` ran three things:<br>- The working-tree picture (green, `c22a1df`) differs from the stage about to be written (red, `23eb33f`), so it first saved a snapshot: `refs/visualcommit/backup/discard-<time>` → a commit by VisualCommit <snapshot@visualcommit.invalid>. Its only parent is `da31c97`, and its image.png is the green picture `c22a1df`. It is asserted by parent and content, not by id.<br>- Then `git checkout --theirs -- image.png`.<br>- Then `git add -- image.png`.<br>Status `Resolved image.png`. The working-tree image.png is feature/kinds' red picture byte for byte (`23eb33f`). Git: `M  image.png`, `AA new.txt`. The banner ends `1 file has conflicts.`. The resolver moved on to new.txt. Header: `U`, `new.txt`, `Both added`, `File 1 of 1`. Toolbar: `Conflict 1 of 1`, `Take ours` and `Take theirs` enabled (both sides have the file), `Merge tool…` disabled as before, `Mark resolved`. The ours pane `Ours: main` has 1 `new file`; bar [`Use ours`, `Use both`]; 2 `added on main` (`#15323A`). The theirs pane `Theirs: feature/kinds` has 1 `new file`; bar [`Use theirs`, `Use both`]; 2 `added on feature/kinds` (`#2C2140`). The result has 6 lines: 1 `new file`, 2 `<<<<<<< HEAD`, 3 `added on main`, 4 `=======`, 5 `added on feature/kinds`, 6 `>>>>>>> feature/kinds`. Lines 2, 4 and 6 are on `#3A2E16`, line 3 on `#15323A`, line 5 on `#2C2140`. `1 conflict left`. The focus is in the result, the caret at the start of line 2. **After step 5:** Use both (ours first) left 3 lines: `new file`, `added on main`, `added on feature/kinds`, none tinted; `No conflicts left`; the caret at the start of line 2. Mark resolved then asked nothing: no block was left, and the file on disk was as read. The operation `Resolve new.txt` wrote new.txt (`new file\nadded on main\nadded on feature/kinds\n`, LF, blob `b14fd8d`) and ran `git add -- new.txt`. Status `Resolved new.txt`. No conflicted file is left, so the resolver closed:<br>- graph GC-M and the stage panel are back, the keyboard focus on the banner's Continue, shown and enabled (11.1);<br>- the working-changes row reads `Working changes`, selected;<br>- the banner reads `Merging: "Merge branch 'feature/kinds'". Conflicts resolved: commit to finish the merge.`, with `Continue` (enabled) and `Abort merge` below the text;<br>- `Unstaged files (0)` with "No unstaged changes";<br>- `Staged files (2)`: M `image.png`, M `new.txt`;<br>- the summary is `Merge branch 'feature/kinds'`, and `Commit 2 files` is enabled.<br>Git:<br>- `git status --porcelain=v1 --untracked-files=all` prints `M  image.png` and `M  new.txt`; `git ls-files -u` prints nothing;<br>- the index holds image.png `23eb33f`, new.txt `b14fd8d`, other.txt `ec92a8c`, notes.txt `439eb6b` (main's) and README.md;<br>- MERGE_HEAD is `3a9de3e…`, and HEAD is `da31c97` on main;<br>- the one backup ref is step 4's snapshot.<br>The stage panel shows no restore bar: a snapshot a card saves is not offered there (that bar is for discards, 14.3). | | RW: steps 1 to 3. A real click on the working-changes row and on `other.txt`: `c15b-deleted-by-them`. A real click on `Keep the file` (`ConflictKeepFile`), then a wait for the log line of `Resolve other.txt` and for `ResolverFileName` to read `image.png`: `c15c-binary-card` | Planned: `c15a-kinds-stage-panel`, `c15b-deleted-by-them`, `c15c-binary-card`, `c15d-both-added`, `c15e-all-resolved`; real window: `c15b-deleted-by-them`, `c15c-binary-card` | Design 13.4 C15; 11.2, 11.3, 11.7, 11.8, 14.3 (a card's empty toolbar row; a card's snapshot is not offered by the restore bar); D148 (non-text conflicts resolved at once from a card; a snapshot before a binary side is written over a changed file). |
| C16 | The external merge tool, with the fake tool of `ConflictsStoppedAsync(mergeTool: true)`: the conflicted file's menu, the question for a modified result, the tool resolving the file, the resolver closing, the status; dark, 1100×700 | 1. `ConflictsStoppedAsync(ConflictsStop.Merge, mergeTool: true)`, 1100×700, dark. Click the working-changes row. Right-click `notes.txt` in the unstaged list. Screenshot (`c16a-file-menu`). Press Esc. 2. Click `notes.txt`. In block 1's ours bar, click `Use ours`. Click `Merge tool…`. Screenshot (`c16b-tool-question`). 3. Click `Keep editing`. Settle; wait until idle. 4. Click `Merge tool…` again; click `Discard`. Wait until idle. Screenshot (`c16c-tool-resolved`). | **When the app has started:** as in C14, with `merge.tool` set to `fake`, `mergetool.fake.cmd` `sh "$(git rev-parse --git-common-dir)/vc-fake-merge-tool.sh" "$MERGED"` and `mergetool.fake.trustExitCode` `true`. **After step 1:** the row is not selected and no resolver opens. The menu has header `notes.txt`, then `Resolve conflict` (highlighted, with the keyboard focus), `Open in merge tool` (enabled), `Mark as resolved`, `Discard changes…` (disabled: "Resolve the conflict first"), —, `Ignore notes.txt` (disabled: "Only untracked files can be ignored"), `Ignore all *.txt files` (disabled: "Only untracked files can be ignored"), —, `Open file`, `Show in folder`, `Copy path`. It is 284 high (8 + 24 + 26 × 9 + 9 × 2, ±2) and lies at least 4 px inside the window. After Esc no menu is open. **After step 2:** the resolver opened as in C14 step 1, except that `Merge tool…` is enabled. After `Use ours` the result has 16 lines: 1 `line 1`, 2 `line 2 ours`, 3 `line 3`, 4 `line 4`, 5 `line 5`, 6 `line 6 theirs`, 7 `line 7`, 8 `line 8`, 9 `line 9`, 10 `<<<<<<< HEAD`, 11 `line 10 ours`, 12 `=======`, 13 `line 10 theirs`, 14 `>>>>>>> feature/theirs`, 15 `line 11`, 16 `line 12`. `1 conflict left`, and the toolbar reads `Conflict 1 of 2` (block 1 is the block last reached; a choice does not move on); the caret is at the start of line 2. Block 1's ours bar says `Used in the result`, with `line 2 ours` on `#1A2E24`. Its theirs bar says `Not used`, with `line 2 theirs` untinted. The result's line 2 has no tint. The result is modified, so `Merge tool…` asks first: `Discard your changes to notes.txt?` / `The merge tool starts from the file as it is on disk.` / no detail / `Keep editing` with the keyboard focus, and `Discard` (danger). **After step 3:** the tool did not start: no `Merge tool for notes.txt` operation was logged, notes.txt is still FM, and git says `UU notes.txt`. The resolver is open with step 2's result (16 lines, `1 conflict left`). **After step 4:** the app discarded the result and ran the operation `Merge tool for notes.txt`: `git -c mergetool.keepBackup=false mergetool --tool=fake --no-prompt -- notes.txt`, exit 0. Its standard output is `Merging:` / `notes.txt` / an empty line / `Normal merge conflict for 'notes.txt':` / `  {local}: modified file` / `  {remote}: modified file`, and standard error is empty: git's own text, not shown and not asserted. Git staged the file, so the resolver moved on, found no conflicted file left and closed:<br>- graph GC-M and the stage panel are back, the keyboard focus on the banner's Continue, shown and enabled (11.1);<br>- the working-changes row reads `Working changes`;<br>- the banner reads `Merging: "Merge branch 'feature/theirs'". Conflicts resolved: commit to finish the merge.`, with `Continue` (enabled) and `Abort merge` below the text;<br>- `Unstaged files (0)`; `Staged files (1)`: M `notes.txt`;<br>- the summary is `Merge branch 'feature/theirs'`, and `Commit 1 file` is enabled;<br>- status `Resolved notes.txt in fake`, and the queue's log line for `Merge tool for notes.txt` ends `: Resolved notes.txt in fake`.<br>Git:<br>- notes.txt is the one line `Resolved by the fake merge tool.` (LF), staged as blob `17dfe0d`;<br>- `git status --porcelain=v1 --untracked-files=all` prints only `M  notes.txt`: no `notes_BACKUP_*`, `_BASE_`, `_LOCAL_`, `_REMOTE_` or `.orig` file is left;<br>- `git ls-files -u` prints nothing; MERGE_HEAD is `5c77ffb…`, and HEAD is `da31c97`. | | RW: steps 2 and 4, on a fresh copy. Real clicks on the working-changes row, on `notes.txt`, on the `ConflictUseOurs` whose bounding rectangle is higher (block 1's), on `ResolverMergeTool`: `c16b-tool-question`. A real click on `Discard`. Git then runs a real `sh` process with the fake tool, and the pass waits for the queue's log line of `Merge tool for notes.txt`: `c16c-tool-resolved` | Planned: `c16a-file-menu`, `c16b-tool-question`, `c16c-tool-resolved`; real window: `c16b-tool-question`, `c16c-tool-resolved` | Design 13.4 C16; 7.1 (the file menu's `Open in merge tool`), 7.2 (the question for a modified result), 10.1, 10.2, 11.9; D149. |
| C17 | A rebase started in a terminal while the app is open: the banner offers Continue and Skip (no "terminal" text), the stage panel's `Continue rebase`, the conflict resolved outside the app, Continue from the banner finishes the rebase; dark, 1100×700 | 1. `ConflictsAsync()`, 1100×700, dark. 2. Outside the app: `git switch feature/picks`, then `git rebase main` (exit 1). Wait until `BannerText` is `Rebasing feature/picks onto main: stopped at 01fd976 "Pick conflicting" (2 of 3). 1 file has conflicts.` and the top row reads `Working changes · 1 conflict` (30-second limit). Screenshot (`c17a-terminal-rebase`). 3. Rest the pointer on the banner's `Continue` and wait for its tooltip. Screenshot (`c17b-continue-tooltip`). 4. Move the pointer to the neutral point and wait until the tooltip has closed (`ToolTip.GetIsOpen` false). Click the working-changes row. Screenshot (`c17c-stage-panel`). 5. Outside the app: write notes.txt as `line 1`, `line 2 picked`, `line 3` … `line 9`, `line 10 ours`, `line 11`, `line 12` (12 lines, LF), then `git add notes.txt`. Wait until `BannerText` is `Rebasing feature/picks onto main: stopped at 01fd976 "Pick conflicting" (2 of 3). Conflicts resolved: continue to commit it and go on.` (30-second limit). Screenshot (`c17d-resolved-outside`). 6. Click the banner's `Continue`. Wait until idle (`OperationChainTimeout`, 60 s: Continue runs a chain of git calls). Screenshot (`c17e-rebased`). | **After step 1:** GC0, `main` ✓ on "Ours edits". `CurrentBranch` reads `main`. No banner. **After step 2:** no dialog. The banner reads `Rebasing feature/picks onto main: stopped at 01fd976 "Pick conflicting" (2 of 3). 1 file has conflicts.`. Below the text, right-aligned, are `Continue` (disabled), `Skip` (enabled) and `Abort rebase`. Neither the text nor the tooltips name a terminal. The icon's tooltip (`GetTip`) is `Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts feature/picks back where it was before the rebase.`. The graph is K1. `CurrentBranch` reads `Detached at d71cdc4`, and no local branch is accented in the left panel. Git:<br>- HEAD is detached at `d71cdc4` "Pick clean" (author 2026-01-01 12:05, committer 12:10, parent `da31c97`); `git status --porcelain=v1` prints `UU notes.txt`;<br>- `rebase-merge/`: `head-name` `refs/heads/feature/picks`, `onto` `da31c97…`, `orig-head` `5c4c257…`, `msgnum` 2, `end` 3, `stopped-sha` and `REBASE_HEAD` `01fd976…`;<br>- `done` holds `pick 01d2046… Pick clean` and `pick 01fd976… Pick conflicting`, and `git-rebase-todo` holds `pick 5c4c257… Pick last`;<br>- `message` is `Pick conflicting`, a blank line; `author-script` has `GIT_AUTHOR_DATE='@1767269160 +0000'` (12:06); `MERGE_MSG` is `Pick conflicting`, a blank line, `# Conflicts:`, `#⇥notes.txt`;<br>- notes.txt is `line 1`, `<<<<<<< HEAD`, `line 2 ours`, `=======`, `line 2 picked`, `>>>>>>> 01fd976 (Pick conflicting)`, `line 3` … `line 9`, `line 10 ours`, `line 11`, `line 12`.<br>**After step 3:** the tooltip `Resolve the conflicts first (1 file)` shows over the disabled `Continue`, inside the window. **After step 4:** the stage panel shows:<br>- `Unstaged files (1, 1 with conflicts)` with U `notes.txt` (tooltip `Conflict: click to resolve it`, `GetTip`); `Staged files (0)` with "No staged changes";<br>- the summary `Pick conflicting` and an empty description, filled from `rebase-merge/message` with no line removed;<br>- the button reads `Continue rebase`, disabled, with the tooltip `Resolve the conflicts first (1 file)` (`GetTip`);<br>- `AmendCheckBox` is disabled, its tooltip (`GetTip`) `Finish or abort the rebase first`.<br>**After step 5:** git says `M  notes.txt`. The banner reads `Rebasing feature/picks onto main: stopped at 01fd976 "Pick conflicting" (2 of 3). Conflicts resolved: continue to commit it and go on.`, with `Continue` (enabled, no tooltip), `Skip` and `Abort rebase` below the text. The working-changes row reads `Working changes`. `Unstaged files (0)`; `Staged files (1)`: M `notes.txt`. `Continue rebase` is enabled. The summary is still `Pick conflicting`. The graph is otherwise K1. **After step 6:** before the operation, the app ran `git diff --cached --check --no-color --no-ext-diff -- notes.txt` (exit 0, no output: no question). The operation `Continue the rebase` then ran:<br>- `git commit --no-verify --cleanup=whitespace --file=-` with `rebase-merge/message` (`Pick conflicting`, a blank line). The author came from `author-script`: Test Author <author@example.com>, 2026-01-01 12:06. It made `e436626` with committer date 2026-01-02 09:00.<br>- `git rebase --continue` (exit 0, `Successfully rebased and updated refs/heads/feature/picks.`). It replayed "Pick last" as `f048313`, author date 12:07, committer date 2026-01-02 09:00.<br>No banner and no working-changes row remain. The graph is K2. `CurrentBranch` reads `feature/picks`. Status `Rebased feature/picks onto main`. Nothing is selected: the working-changes row was selected, and the tree becoming clean clears that selection (phase 2's rule, `RepositoryViewModel.ApplyStatus`, which 1.5 and 14.3 keep after Continue). Git:<br>- `git status --porcelain=v1 --untracked-files=all` prints nothing; there is no `rebase-merge/`; HEAD is `refs/heads/feature/picks` at `f048313`, and main is still `da31c97`;<br>- the messages are `Pick last\n`, `Pick conflicting\n` and `Pick clean\n`;<br>- notes.txt at `e436626` is step 5's file; feature/picks' tree has README.md, image.png, last.txt, new.txt, notes.txt, other.txt and pick.txt;<br>- `git for-each-ref refs/visualcommit/` prints nothing: the app started no rebase, and Continue saves no backup. | | n/a: an outside change; the rebase banner with Continue and Skip is pictured in the real window by C11 | Planned: `c17a-terminal-rebase`, `c17b-continue-tooltip`, `c17c-stage-panel`, `c17d-resolved-outside`, `c17e-rebased` | Design 13.4 C17; 5.1 to 5.4, 10.2, 10.3, 14.3 (`{onto}`: a local branch first); D144 (Continue commits with `author-script`'s author and `--no-verify`). The rebase is started outside the app while it is open, so steps 2 and 5 wait for the banner's exact text through the file watcher (phase 3's 30-second limit for outside changes). |
| D18 | The light theme at 1920×1080: the interactive list with a dropped and a squashed row, the message dialog, and the resolver with every tint of design 7.3 sampled; light, 1920×1080 | **Part 1** (`Check_D18_interactive_list_light`). 1. `InteractiveAsync()`, light, 1920×1080. 2. Right-click the row "Base" in its message cell; click `Interactive rebase of main from here…`. Wait until `InteractiveRebaseView` shows its rows. 3. Press `d`; press Down; press `s`. Screenshot (`d18a-list-light`). 4. Click `Edit…` (`RebaseEditMessageButton`) on Three's second line. Wait for `MessageDialog`. Screenshot (`d18b-message-dialog-light`). Click `Cancel`. **Part 2** (`Check_D18_resolver_light`). 5. `ConflictsAsync()`; before the app starts, outside the app: `git config merge.conflictStyle diff3`, then `git merge feature/theirs` (exit 1). Start the app, light, 1920×1080. 6. Click the working-changes row; click `notes.txt` in the unstaged list. Screenshot (`d18c-resolver-light`). 7. In block 2's ours bar, click `Use ours`. Screenshot (`d18d-used-light`). | Everything is in the light tokens. Each colour named "sampled" is a background or a line, read from the picture: a background at a point of the row with no glyph, a line on its pixels. Its RGB must equal the token's exactly, with no alpha. A text's or an icon's colour is read from the control's `Foreground` and inspected in the picture, never sampled: glyphs are anti-aliased. **After step 2:** the list replaces the graph in the graph area (x 260 to 1520, 1260 wide). The left panel stays, and the right panel shows the selected commit. Header `Interactive rebase of main from 4b4622c`, with `6 commits` at the right. **After step 3:** rows of 30, newest on top, each with the grip (`IconGrip`, `VcTextSecondaryBrush` `#5C6572`), the action button (84 × 22, the action's name and a chevron), the short SHA in the code font, the subject, and `Test Author` in the 140-wide author column (the list is wider than 520):<br>1. `57f089d Five`: `Drop`. Its SHA and subject are struck through in `VcTextDisabledBrush` `#A1A8B3`.<br>2. `eef5c15 Four`: `Squash`. Its grip, button, SHA and subject sit 16 px further right. It is selected with the keyboard focus; the background is sampled `#DDE1FA` at window (900, 169), Four's vertical centre, between its subject and the author column. A 2-px line in `VcLane0Brush` at x 12 from the list's left edge runs from Four's vertical centre to the top edge of Three's row (2.2, 14.3); it is sampled `#4353D8` at the pixel column 12 from the list's left edge (window x 272), 6 px above the top of Three's row (in Four's row, below its centre; Four's moved grip lies right of the line).<br>3. `ff42e7f Three`: `Pick`, then the second line (22 high) `Message: Three` with `Edit…` at its right.<br>4. `ed58902 Fix one`, 5. `938844a Two`, 6. `fc6c8fd One`: each `Pick`.<br>The base row reads `Base` in `#5C6572`, then `4b4622c`, then `Base`. The footer has `Cancel` and `Start rebase`, enabled. `Start rebase`'s background is sampled `#4353D8`; its label's `Foreground` is `#FFFFFF` (read from the control, not sampled). The right panel shows Four's details (`eef5c15`, "Four", Test Author, 2026-01-01 12:06, one changed file, A `four.txt` `src`). Git is unchanged: main is `57f089d`. **After step 4:** `MessageDialog` on the wide card (460) over the backdrop (`#66000000`): title `Message of the squashed commit`. `MessageSummary` holds `Three`, with the keyboard focus and the caret at its end; `MessageSummaryCounter` reads `67`. `MessageDescription` holds `#42 is the issue`, an empty line and `Four`. `Cancel`, and `Save` (enabled; its background sampled `#4353D8`, its label's `Foreground` `#FFFFFF`, not sampled). After Cancel: no dialog, and the list is as after step 3. **After step 5:** git says `UU notes.txt`, and notes.txt is N3. The graph is GC-M with `Working changes · 1 conflict`, and every column shows. **After step 6:**<br>- The resolver spans x 260 to 1920. Above it, the banner runs the whole 1660 width: one line (`oneLine: true`, buttons beside the text), background sampled `#FFF4DE`, a 1-px bottom line sampled `#B7791F`, the icon in `VcWarningBrush` `#B7791F`, the text `Merging: "Merge branch 'feature/theirs'". 1 file has conflicts.`, and `Continue` (disabled) with `Abort merge` beside it.<br>- Header: `U` in `VcDangerBrush` `#C93A46`, `notes.txt`, `Both modified`, `File 1 of 1`.<br>- Toolbar: `Conflict 1 of 2`, `Take ours`, `Take theirs`, `Merge tool…` (disabled), and `Mark resolved` (background sampled `#4353D8`, its label's `Foreground` `#FFFFFF`, not sampled).<br>- The panes are PM under `Ours: main` and `Theirs: feature/theirs`. Every row of both is in view.<br>- The result holds N3's 24 lines, all in view, titled `Result` with `2 conflicts left`. The focus is in the result, the caret at the start of line 2.<br>Sampled at each line's vertical centre, at the horizontal middle of its pane or of the result, where no line of N3 reaches:<br>- `VcConflictOursBrush` `#DCF1F5`: the ours pane's lines 2 (`line 2 ours`) and 10 (`line 10 ours`), and the result's lines 3 and 17.<br>- `VcConflictTheirsBrush` `#F0E6FA`: the theirs pane's lines 2 (`line 2 theirs`) and 10 (`line 10 theirs`), and the result's lines 7 and 21.<br>- `VcConflictBaseBrush` `#EEF0F3`: the result's lines 5 and 19 (`line 2`, `line 10`).<br>- `VcConflictMarkerBrush` `#FCEFD2`: the result's lines 2, 4, 6, 8, 16, 18, 20 and 22 (`<<<<<<< HEAD`, `\|\|\|\|\|\|\| 195768b`, `=======`, `>>>>>>> feature/theirs`). Their text is in `VcWarningBrush` `#B7791F` (the `Marker` tint's colorizer; inspected in the picture, not sampled).<br>- The other text rows and lines have no tint: their background equals the pane's or the result's own at `line 1`. The four bars are on `VcDiffHunkBrush` (`RowsOf` gives `Bar`).<br>`TintAt` and the panes' rows give the same tokens. **After step 7:**<br>- The ours pane's line 10 (`line 10 ours`) is sampled `VcConflictUsedBrush` `#E6F6EC`, and block 2's ours bar reads `Used in the result` in `VcTextSecondaryBrush` (`#5C6572`, not sampled).<br>- The theirs pane's line 10 (`line 10 theirs`) has lost its tint, and block 2's theirs bar reads `Not used`.<br>- Block 1's lines and bars are as after step 6.<br>- The result has 18 lines: 1 `line 1`; 2 to 8 block 1 as in N3 with the same tints; 9 `line 3`, 10 `line 4`, 11 `line 5`, 12 `line 6 theirs`, 13 `line 7`, 14 `line 8`, 15 `line 9`, 16 `line 10 ours`, 17 `line 11`, 18 `line 12`. `1 conflict left`.<br>- The result's line 16 (`line 10 ours`, the chosen text) has no tint. `TintAt(16)` is `None`, and its colour sampled at its centre equals the result's at `line 1`. `ResolverConflictPosition` reads `Conflict 1 of 2` (block 1, reached on opening, is the block last reached; a choice does not move on), and the caret is at the start of line 16, the start of the replaced text.<br>Nothing written: notes.txt is still N3, and git says `UU notes.txt`. | | n/a: scripted only; 1920×1080 does not fit the development screen (design 13.4) | Planned: `d18a-list-light`, `d18b-message-dialog-light`, `d18c-resolver-light`, `d18d-used-light` | Design 13.4 D18; 2.2 (the list), 7.2 (the message dialog), 7.3 (the light tokens), 11.2, 11.3 (a diff3 file's panes show no base section), 11.5, 14.3; D141, D148. Scripted only: 1920×1080 does not fit the development screen. |

## What proves each Delivers item

Every item of phase 4's "Delivers" list is proven by an automated test or by a visual check (the
plan's rule). This is design 13.5's table with this report's checks:

| Delivers (plan.md, phase 4; design 13.5) | Automated tests | Visual checks |
|---|---|---|
| Rebase onto, from menus and by drag (phase 3's "Rebase onto" in the commit and branch menus and in the drop menu, D77, D92) | `RebaseOperationTests` (Git), `RepositoryOperationsRebaseTests`, `RepositoryMenusTests` | A1 (the items and their reasons in the commit, local-branch and remote-branch menus), A2 (from the branch menu and the commit menu: the graphs, the status, the dates, the backup), A3 (the drop menus, none for a remote source, "Check out … and rebase it": the graph, HEAD, the new tip selected, the backup), A5 (git's refusals with their summaries, and the app's own refusal before a checkout), C11 (the branch menu's rebase that stops twice and is finished by Continue), C13 part C (by drag, stopped and aborted: the branch the user was on is checked out again) |
| Pull with rebase, also chosen for one pull; Continue and Skip after a pull's rebase | `PullRebaseParityTests`, `RepositoryOperationsRebaseTests` and `RepositoryOperationsPullRebaseTests` (a configured and a chosen pull whose rebase stops, with and without a conflicted file, finished by Continue and by Skip; the fork point's left-out commits, also at a stop; an autostash that conflicted) | A1 (the item in the current branch's menu, enabled as Pull), A4 (a clean pull with rebase; a stop with its banner and stage panel, resolved and continued; a stop skipped with its question) |
| Interactive rebase: reorder, squash, fixup, reword, drop; squash a multi-selection | `RebaseTodoTests` (Core, pure), `InteractiveRebaseTests` (Git: the delivery with paths with spaces and apostrophes, two message files, the guarded amend in every case of 0.2, hooks), `InteractiveRebaseListParityTests`, `InteractiveRebaseViewModelTests`, `MessageDialogTests` | A1 and A3 (where the items are and when they are enabled: the commit, branch, remote-branch and drop menus; Squash enabled for a chain and disabled with three of its reasons), B6 (the list from the commit menu, its rows and base row, the right panel and a diff over the list, the action menu and Shift+F10, the bottom row's disabled Squash and Fixup), B7 (a reorder by drag, a fixup, a squash with an edited message, a reword by the menu and by its key, a drop by key, Start and the history it makes), B8 (`Squash 2 commits…` on a multi-selection and its result), B9 (leaving a changed list asks, Alt+arrows, Start's two disabled reasons, an unchanged list closes without a question), D18 (the list and the message dialog in the light theme) |
| The banner's Continue, Skip, Abort for merge, rebase, cherry-pick and revert | `ContinueSkipTests` (Git, every row of 5.1), `ConflictBannerTests`, `WorkingChangesInProgressTests` | merge: C10 (Continue disabled with its reason, then Continue), C14 (Continue asks with markers staged), C15 and C16 (Continue enabled once every file is resolved); rebase: A4 (after a pull: Continue, Skip), C11 (Continue at two stops), C13 part A (Skip with its question and backup, then Abort), C13 part C (the Abort of a rebase the app began with a checkout), C17 (a rebase started in a terminal: Continue and Skip offered, no "terminal" text); a cherry-pick list: C12 (Continue from the stage panel), C13 part B (Abort undoes the whole list); revert: no phase 4 check runs one; phase 3's checks 12 (steps 8 and 9) and 25 (step 5) picture a single revert's banner as phase 4 changes it, and the tests prove its Continue, Skip and Abort |
| The resolver: three panes, per block, editing, a whole file, mark resolved, the merge tool (and staging a file with markers asks wherever it is staged from, 7.1, D148) | `ConflictTextTests` (Core), `ConflictResolverViewModelTests`, `ConflictResultEditorTests`, `ConflictPanesTests`, `ConflictOperationTests` (Git: every kind of 0.4) | C10 (the layout, titles and both panes, `Use ours`, `Use theirs`, the platform's undo, `Use both` ours first, Mark resolved), C11 (the rebase's titles, `Use theirs`, `Use both` theirs first, two stops), C12 (the pick's titles, `Use ours` then typing, Mark resolved), C14 (typing, undo and redo of a choice, Esc inside the result, the result's and a pane's menus, `Take theirs`, Mark resolved with a block left, leaving with a modified result, Stage all and Continue with markers), C15 (the deletion card's `Keep the file`, the binary card's `Use theirs` with its snapshot, an add/add file per block, moving on and closing), C16 (the merge tool, its question for a modified result), D18 (every tint of 7.3 in the light theme) |
| Several picks and reverts with git's list (D136) | `HistoryOperationTests` (every row of 4), `RepositoryOperationsHistoryTests` | C12 (one `git cherry-pick` of three commits, stopped at the second, the third picked by Continue), C13 part B (Abort undoes the whole list, the commit already picked included); a pick list that stops also in phase 3's check 11b as phase 4 changes it; reverts with git's list: phase 3's check 12 step 7 (`Revert 2 commits`, now one `git revert --no-edit` call with both commits, newest first, which makes the same two commits: same parents, trees, messages and dates) and the tests; no check stops a revert list (check 25 step 5's revert is a single one) |
| Reftable (D128) | `PseudoRefTests` (the probe's rules; a real reftable repository on git 2.45 or later) | none: nothing on screen differs |
| The sequence editor of the app's own | `InteractiveRebaseTests` | B7 and B8 (git's results equal those of 2.5's todo and message files handed over by the app's shell-function editor; the `#` line kept, which git's own squash loses) |
| Backups: the rebase's and Skip's, the automatic clean-up | `BackupCleanupTests` (Git, with a fixed clock), `RepositoryOperationsBackupCleanupTests` (App: at opening, after a backup, a failing clean-up shows nothing) | the rebase's: A2, A3, A4 (through the pull's callback too), A5 (saved before git refuses), B7, B8, C11, C13 part C; Skip's (`skip-<time>` and its `-changes`): A4 part C, C13 part A; no backup when nothing was started: B6, B9. The clean-up removes nothing in any of them (no `Removed backup` line) and says nothing on screen, so only the tests prove it |
| Phase 1's check 13 made deterministic (D150) | `AuthRemoteServerTests` (the hold at a percentage) | phase 1's checks 13 and 14, as "Changes to expected results" gives them |

## Changes to expected results

Expected results of earlier phases that phase 4 changes on purpose (design 12.1), and anything
changed in this report after it was first committed (with the reason). The tests change in the same
commit as the code that changes them.

- **Phase 1, checks 13 and 14** (D150, `a6788d8`). Check 13 failed once on CI's `ubuntu-latest`
  (run 38213858149): it cloned the 100k-commit repository from a `file://` URL and had to catch a
  picture with a percentage between 1 and 99, and a fast runner finished the clone between two
  pictures. The checks now clone a bare copy of that repository (`large-history-v1.git`, made once
  per machine beside the working copy) served by `AuthRemoteServer` on 127.0.0.1:47392, a second
  fixed port of the visual checks (D97 amended), and the server holds the clone's pack at 45 %.
  - Check 13, step 3: the URL typed is `http://127.0.0.1:47392/large-history-v1.git`.
  - Check 13, step 4: "wait until the progress shows the held percentage; screenshot; the server
    lets go". `13b` shows the URL above, the progress bar at 45 % and the text exactly `Counting
    objects 45%` (git's last line `remote: Counting objects:  45% (45001/100002)`), the fields
    disabled and Cancel enabled. Only the temporary parent folder's name varies between runs.
  - Check 13 after step 5 and check 14's results are unchanged; check 14 clicks Cancel while the
    clone is held.
  - CI run 38232105795 (`b433936`) passed every job with the new checks.
- **Phase 3's menus that gain items** (design 7.1, 14.3): checks 1, 2, 5 (the keyboard walk of the
  commit menu), 5b, 6, 7, 8 to 12 (the several-commits menus gain `Squash {N} commits…`: disabled in
  checks 11, 11b and 12 step 4, enabled and highlighted first in check 12 step 7, whose two selected
  commits form a chain on main's line), 9b (the conflicted file's menu gains `Resolve conflict` and
  `Open in merge tool`), 13, 13b, 16, 18b, 19, 20, 25, 27, 28, 29, 30 (the drop menus too) and 32;
  the real-window pass's parts B and C; `RepositoryMenusTests`, `RepositoryMenusTests.Window` and
  `ContextMenuAdapterTests`: item lists, heights, and the first enabled item where it moves. Check
  3's menus do not change. The exact new lists are in "Phase 3's menus after phase 4" below; the
  package that adds the items copies them into phase 3's expectation files.
- **Phase 3's banner** (design 5.1, 5.2): the Continue and Skip buttons, shown as 5.1 says, and the
  layout rule.
  - At 1100×700 (checks 9, 9b and 25) the buttons stand on a row of their own under the text,
    because beside them the 440-wide banner would leave its text less than 240 px.
  - At 1400×900 the 740-wide banner keeps more than 240 px for its text, so the buttons stand
    beside it; the line count there is not stated (phase 3's rule). This holds for check 11b steps 2
    and 4, a pick of two commits that is now git's list: `Continue` disabled with `Resolve the
    conflicts first (1 file)`, `Skip`, `Abort cherry-pick`; the text stays, since no commit waits.
    It holds too for check 12 steps 8 and 9, a single revert: the same `Continue`, `Skip`, `Abort
    revert`.
  - At 1920×1080 (check 27) `Continue` joins `Abort merge` beside the text and the banner keeps one
    line (`oneLine: true`, as phase 3 asserts it).
  - The icon's tooltip changes in checks 9, 14, 15b and 25 and in the real-window pass's part B.
  - The texts marked "new" in design 5.2: check 25's list texts (steps 1 to 4), its rebase text
    (step 6) and its empty-pick text (step 7). On a git that keeps `REVERT_HEAD` after an empty
    revert (the alternative check 12 step 11 gives; on Git 2.36 that step stays the error dialog
    `Revert failed`, D117), the empty revert's text ends `Skip it to go on.`, with `Continue`
    (disabled: `This revert changes nothing: skip it`) and `Skip`.
  - The merge and single-pick texts do not change.
- **Phase 3's stage panel** (design 5.3): the conflicted row's tooltip `Conflict: click to resolve
  it` (checks 9 step 4, 14 step 8, 15b step 6 and 25 steps 2, 5 and 8: every step where phase 3's
  tests assert `ConflictRowToolTip` or `ConflictRowTip`); the empty pick's commit tooltip `This
  cherry-pick changes nothing: skip it`; the message filled from `MERGE_MSG` keeps a commit's own
  `#` lines.
- **Phase 3's check 11b**: one git call for the list, so `sequencer/` is there at the stop. The
  status and its tooltip (steps 2 and 3, `11b2-status-tooltip`) read 10.2's `Cherry-picked 1 of 2
  commits; stopped at 078ef05 with conflicts in 1 file` (warning). The banner gains Continue and
  Skip ("Phase 3's banner"). The Abort question adds that the commits already made are dropped too.
  After Abort `main` is where it was before the first pick.
- **Phase 3's check 25 step 4**: the stopped pick is committed in a terminal (the check runs `git
  commit --no-edit` through `TempRepo`); the check then shows the paused banner, Continue enabled,
  Skip disabled with its reason and the Abort that keeps HEAD; a new step on a fresh copy shows the
  stage panel's `Continue cherry-pick` (step 2's button text, "Stage files to commit" before).
- **Phase 3's check 25 step 7, the empty pick's dialog** (added on 2026-10-12, after WP4's review):
  the dialog `Cherry-pick stopped` (`25m-empty-pick-dialog`) agrees with the banner, which now
  offers Skip (5.2): its summary reads `dff8fbf changes nothing on main: its changes are already
  there. Skip it to go on.` where phase 3 had "Abort the cherry-pick to go on."
  (`Phase3Expectations.C.cs` `Check25EmptySummary`; `OperationTextsTests`, and the empty picks of
  `RepositoryOperationsHistoryTests` and `RepositoryOperationsMergeTests`). The revert's dialog
  does not change: on Git 2.36 `Revert failed` opens only when nothing is in progress (D117), so
  there is nothing to skip.
- **Phase 2's conflicted-file diff body** ("This file has a merge conflict") is replaced by the
  resolver; no phase 2 check shows it, and phase 2's pictures do not change (the spike kept all 66
  byte-identical, `a7ce01f`).

### Phase 3's menus after phase 4

Phase 4 adds the items of design 7.1 to phase 3's menus. Below, every phase 3 check step whose menu
gains items, with the whole new list in that step's state, its height (8 + 24 + 26 × items + 9 ×
separators) and the highlighted item; menus not listed (check 3's tag, stash, remote,
section-header and empty-area menus, check 4's and 26's file menus, the current branch without an
upstream) do not change. Scenario facts used: in "ops" HEAD is on `main` at Main work (`88d6b91`),
whose linear first-parent line is Main work, Add core, Initial commit; Alpha one, Alpha two,
Fast-forward me and Conflicting change are not on `main`. The heights constants in
`Phase3Expectations` that change: `CommitMenuHeight` 232 → 284, `FeatureAlphaMenuHeight` 406 → 458,
`OriginFeatureAlphaMenuHeight` 241 → 293, `DropMenuHeight` 110 → 171, and part D's
`AlphaTwoMenuHeight` 232 → 284 (check 27).

Three commit menus recur (ops, HEAD on `main`):

- **OC1, a commit not on main** (Alpha two `ce5ea6a`, Alpha one `dff8fbf`, Fast-forward me
  `d93814d`, Conflicting change `078ef05`): header "<short> <subject>"; "Check out this commit"
  (highlighted); "Create branch here…"; "Create tag here…"; —; "Cherry-pick onto main"; "Revert this
  commit"; "Rebase main onto this commit"; "Interactive rebase of main from here…" (disabled: "This
  commit is not on main"); "Reset main to this commit…"; —; "Copy SHA". 284 high.
- **OC2, a commit on main's line below HEAD** (Add core `8beca34`): "Check out this commit"
  (highlighted); "Create branch here…"; "Create tag here…"; —; "Cherry-pick onto main" (disabled:
  "This commit is already on main"); "Revert this commit"; "Rebase main onto this commit" (disabled:
  "This commit is already on main"); "Interactive rebase of main from here…"; "Reset main to this
  commit…"; —; "Copy SHA". 284 high.
- **OC3, HEAD's own commit** (Main work `88d6b91`): as OC2, with "Interactive rebase of main from
  here…" (disabled: "main has no commits after this one"). 284 high.

Per check:

- **Check 1.** Steps 1, 2, 3 and 5 (`01a`, real window `01a`): OC1 for "ce5ea6a Alpha two". Step 4
  (`01c`, real window `01c`): OC2 for "8beca34 Add core"; the pointer still rests on "Cherry-pick
  onto main", the first item of the second group, and "Check out this commit" stays highlighted.
  Step 6 (`01d`, edges, HEAD detached at Detached work): header "419190f Detached work"; "Check out
  this commit" (disabled: "HEAD is already at this commit"); "Create branch here…" (highlighted);
  "Create tag here…"; —; "Cherry-pick onto HEAD" (disabled: "This commit is already on HEAD");
  "Revert this commit"; "Rebase HEAD onto this commit" (disabled: "This commit is already on HEAD");
  "Interactive rebase of HEAD from here…" (disabled: "HEAD has no commits after this one"); "Reset
  HEAD to this commit…"; —; "Copy SHA"; 284 high. Step 7 (`01e`, the graph scenario, HEAD on `main`
  at "Add settings page", whose linear line runs Add settings page, Bump version and ends at the
  merge): header "f7919b8 Merge branch 'feature/login'"; "Check out this commit" (highlighted);
  "Create branch here…"; "Create tag here…"; —; "Cherry-pick onto main" (disabled: "Merge commits
  cannot be cherry-picked here"); "Revert this merge commit"; "Rebase main onto this commit"
  (disabled: "This commit is already on main"); "Interactive rebase of main from here…" (enabled:
  the line's merge commit may be the base, 2.1); "Reset main to this commit…"; —; "Copy SHA"; 284
  high. "No menu of this check has a rebase, squash or compare item" becomes "no compare item".
- **Check 2.** Step 1 (`02a`, `feature/alpha`): header "feature/alpha"; "Check out feature/alpha"
  (highlighted); "Merge feature/alpha into main"; "Merge feature/alpha into main (create a merge
  commit)"; "Rebase main onto feature/alpha"; "Interactive rebase of main onto feature/alpha…";
  "Fast-forward feature/alpha to origin/feature/alpha" (disabled: "feature/alpha is up to date with
  origin/feature/alpha"); —; "Push feature/alpha to origin/feature/alpha"; "Force push
  feature/alpha…" (danger); —; "Create branch here…"; "Create tag here…"; "Rename feature/alpha…";
  "Delete feature/alpha…" (danger); —; "Change upstream…"; "Stop tracking origin/feature/alpha"; —;
  "Copy branch name"; 458 high. Step 2 (`02b`, `feature/ff`): "Check out feature/ff" (highlighted);
  "Merge feature/ff into main"; "Merge feature/ff into main (create a merge commit)"; "Rebase main
  onto feature/ff"; "Interactive rebase of main onto feature/ff…"; —; "Push feature/ff…"; —; "Create
  branch here…"; "Create tag here…"; "Rename feature/ff…"; "Delete feature/ff…" (danger); —; "Set
  upstream…"; —; "Copy branch name"; 380 high. Step 3 (`02c`, `main`): "Pull from origin/main"
  (highlighted); "Pull from origin/main (rebase)"; —; "Push main to origin/main"; "Force push main…"
  (danger); —; "Create branch here…"; "Create tag here…"; "Rename main…"; "Delete main…" (disabled:
  "You cannot delete the branch you are on"); —; "Change upstream…"; "Stop tracking origin/main";
  —; "Copy branch name"; 354 high. Step 4 (`02d`, `origin/feature/alpha`): "Check out
  origin/feature/alpha" (highlighted); "Merge origin/feature/alpha into main"; "Merge
  origin/feature/alpha into main (create a merge commit)"; "Rebase main onto origin/feature/alpha";
  "Interactive rebase of main onto origin/feature/alpha…"; —; "Create branch here…"; "Create tag
  here…"; —; "Delete feature/alpha on origin…" (danger); —; "Copy branch name"; 293 high. Step 5
  (`02e`, `origin/main`): "Check out origin/main" (disabled: "main is already checked out"); "Merge
  origin/main into main" (highlighted); "Merge origin/main into main (create a merge commit)";
  "Rebase main onto origin/main" (disabled: "main is up to date with origin/main"); "Interactive
  rebase of main onto origin/main…"; —; "Create branch here…"; "Create tag here…"; —; "Delete main
  on origin…" (danger); —; "Copy branch name"; 293 high. Step 6 (`02f`, edges with `loc` checked
  out): "Pull from main" (highlighted); "Pull from main (rebase)"; —; "Create branch here…"; "Create
  tag here…"; "Rename loc…"; "Delete loc…" (disabled: "You cannot delete the branch you are on"); —;
  "Change upstream…"; "Stop tracking main"; —; "Copy branch name"; 293 high (was 267). Step 7
  (`side`, at the commit `loc` is on): "Check out side" (highlighted); "Merge side into loc"; "Merge
  side into loc (create a merge commit)"; "Rebase loc onto side" (disabled: "loc is up to date with
  side"); "Interactive rebase of loc onto side…"; —; "Push side…"; —; "Create branch here…";
  "Create tag here…"; "Rename side…"; "Delete side…" (danger); —; "Set upstream…"; —; "Copy branch
  name"; 380 high (no longer "all enabled"). Step 8 (`02g`, the local branch `origin/main`, at the
  commit `loc` is on): as step 7 with `origin/main`, "Rebase loc onto origin/main" (disabled: "loc
  is up to date with origin/main") and "Interactive rebase of loc onto origin/main…"; 380 high. Step
  9 (`02h`, the remote branch `origin/main`, at Main work): "Check out origin/main" (highlighted);
  "Merge origin/main into loc"; "Merge origin/main into loc (create a merge commit)"; "Rebase loc
  onto origin/main" (enabled); "Interactive rebase of loc onto origin/main…"; —; "Create branch
  here…"; "Create tag here…"; —; "Delete main on origin…" (danger); —; "Copy branch name"; 293 high.
- **Check 5.** Steps 1 to 3: OC1 for "ce5ea6a Alpha two", by the keyboard; Down still highlights
  "Create branch here…", End "Copy SHA", Up from Home wraps to "Copy SHA". Step 4: OC3 for "88d6b91
  Main work"; the three Downs still end on "Revert this commit" (the new items come after it, and
  both are disabled there). Step 5: step 3 of check 2's `main` menu (354 high), "Pull from
  origin/main" highlighted, top-left at x 4.
- **Check 5b.** Steps 4 and 5 open OC1 menus (Alpha two, Conflicting change); the step asserts their
  headers only.
- **Check 6.** Step 5 (`06d`, `origin/feature/alpha` with no local `feature/alpha`): check 2 step 4's
  list, "Check out origin/feature/alpha" highlighted; 293 high (was 241).
- **Check 7.** Steps 2 and 7 (`07b`, real window `07b`): header "feature/alpha → main"; "Merge
  feature/alpha into main" (highlighted); "Merge feature/alpha into main (create a merge commit)";
  "Fast-forward main to feature/alpha" (disabled: "main has commits that feature/alpha does not
  have"); —; "Check out feature/alpha and rebase it onto main"; "Check out feature/alpha and rebase
  it onto main interactively…"; 171 high (was 110); "No rebase or pull-request item" becomes "no
  pull-request item". Step 3 (`07c`): header "feature/ff → main"; "Merge feature/ff into main"
  (highlighted); "Merge feature/ff into main (create a merge commit)"; "Fast-forward main to
  feature/ff"; —; "Check out feature/ff and rebase it onto main" (disabled: "feature/ff is up to date
  with main"); "Check out feature/ff and rebase it onto main interactively…"; 171 high. Step 4:
  header "feature/alpha → feature/conflict"; "Fast-forward feature/conflict to feature/alpha"
  (disabled: "feature/conflict has commits that feature/alpha does not have"); "Check out
  feature/conflict and merge feature/alpha into it" (highlighted); —; "Check out feature/alpha and
  rebase it onto feature/conflict"; "Check out feature/alpha and rebase it onto feature/conflict
  interactively…"; 145 high. Step 5 (`07d`): header "main → origin/feature/alpha"; "Push main to
  origin/feature/alpha" (highlighted); "Push main to origin/feature/alpha and track it"; —; "Rebase
  main onto origin/feature/alpha"; "Interactive rebase of main onto origin/feature/alpha…"; 145
  high.
- **Check 8.** Steps 2 and 4: check 2 step 2's `feature/ff` list, 380 high (was 328). Step 6's
  first menu: check 2 step 5's `origin/main` list, 293 high; its second menu: check 2 step 4's
  `origin/feature/alpha` list, 293 high. Step 9 (after the merge of `origin/feature/alpha`, which
  `main` now reaches): check 2 step 1's list with "Rebase main onto feature/alpha" (disabled: "main
  is up to date with feature/alpha") and "Interactive rebase of main onto feature/alpha…" enabled;
  458 high.
- **Check 9.** Step 5 (merge in progress): check 2 step 1's list with "Merge feature/alpha into
  main", "Merge feature/alpha into main (create a merge commit)", "Rebase main onto feature/alpha"
  and "Interactive rebase of main onto feature/alpha…" all disabled with "Finish or abort the merge
  first"; "Check out feature/alpha" highlighted; 458 high (was 406; it may be moved above the
  pointer and inside the window, as phase 3's rule says).
- **Check 9b.** Step 3 (the conflicted `src/core.txt`): header "core.txt"; "Resolve conflict"
  (highlighted); "Open in merge tool" (disabled: "Set merge.tool in git's configuration to use a
  merge tool"); "Mark as resolved"; "Discard changes…" (disabled: "Resolve the conflict first"); —;
  "Ignore core.txt" (disabled: "Only untracked files can be ignored"); "Ignore all *.txt files"
  (disabled: "Only untracked files can be ignored"); —; "Open file"; "Show in folder"; "Copy path";
  284 high (was 232). The highlight moves from "Mark as resolved" to "Resolve conflict".
- **Check 10.** Steps 2 and 8: OC2 for "8beca34 Add core" (the working-tree changes of step 8 change
  nothing, 1.1).
- **Check 11.** Step 2: OC3 for "88d6b91 Main work"; the pointer still moves straight onto
  "Cherry-pick onto main". Step 3: OC1 for "dff8fbf Alpha one". Step 6: header "3 commits"; "Squash 3
  commits…" (disabled: "Only commits of main can be squashed"); "Cherry-pick 3 commits onto main"
  (disabled: "Some of these commits are already on main"); "Revert 3 commits" (highlighted); —;
  "Copy SHAs"; 145 high (was 119); the pointer reaches "Cherry-pick 3 commits onto main" crossing
  only the disabled Squash; "no Squash or Compare item" becomes "no Compare item". Step 10: header
  "2 commits"; "Squash 2 commits…" (disabled: "Only commits of main can be squashed"); "Cherry-pick
  2 commits onto main" (highlighted); "Revert 2 commits"; —; "Copy SHAs"; 145 high (no longer "all
  enabled").
- **Check 11b.** Step 2 (Conflicting change and Alpha one): as check 11 step 10's list, 145 high.
- **Check 12.** Steps 2 and 10: OC3. Step 3: check 2 step 1's list, 458 high. Step 4 (the merge and
  Main work): header "2 commits"; "Squash 2 commits…" (disabled: "Merge commits cannot be
  squashed"); "Cherry-pick 2 commits onto main" (disabled: "Merge commits and stashes cannot be
  cherry-picked"); "Revert 2 commits" (disabled: "Revert merge commits one at a time"); —; "Copy
  SHAs" (highlighted, the only enabled item); 145 high. Step 5 (the merge `f19f50a`, HEAD's
  commit): "Check out this commit" (highlighted); "Create branch here…"; "Create tag here…"; —;
  "Cherry-pick onto main" (disabled: "Merge commits cannot be cherry-picked here"); "Revert this
  merge commit"; "Rebase main onto this commit" (disabled: "This commit is already on main");
  "Interactive rebase of main from here…" (disabled: "main has no commits after this one"); "Reset
  main to this commit…"; —; "Copy SHA"; 284 high. Step 7 (Main work and Add core): header "2
  commits"; "Squash 2 commits…" (highlighted: a chain on main's linear line, not the root);
  "Cherry-pick 2 commits onto main" (disabled: "Some of these commits are already on main"); "Revert
  2 commits"; —; "Copy SHAs"; 145 high; the step still clicks "Revert 2 commits". Step 8 (G12, HEAD
  on Core again) and step 11 (HEAD on the revert `13c773d`): OC2 for "88d6b91 Main work" (Main work
  is HEAD's first parent there).
- **Check 13.** Step 1: OC3. Step 6 (HEAD on `hotfix` at Main work): header "feature/conflict";
  "Check out feature/conflict" (highlighted); "Merge feature/conflict into hotfix"; "Merge
  feature/conflict into hotfix (create a merge commit)"; "Rebase hotfix onto feature/conflict";
  "Interactive rebase of hotfix onto feature/conflict…"; —; "Push feature/conflict…"; —; "Create
  branch here…"; "Create tag here…"; "Rename feature/conflict…"; "Delete feature/conflict…"
  (danger); —; "Set upstream…"; —; "Copy branch name"; 380 high. Step 8 (`main`, whose tip `hotfix`
  is on): "Check out main" (highlighted); "Merge main into hotfix"; "Merge main into hotfix (create
  a merge commit)"; "Rebase hotfix onto main" (disabled: "hotfix is up to date with main");
  "Interactive rebase of hotfix onto main…"; "Fast-forward main to origin/main" (disabled: "main is
  up to date with origin/main"); —; "Push main to origin/main"; "Force push main…" (danger); —;
  "Create branch here…"; "Create tag here…"; "Rename main…"; "Delete main…" (danger); —; "Change
  upstream…"; "Stop tracking origin/main"; —; "Copy branch name"; 458 high. Step 9 (`feature/ff`):
  as check 2 step 2's list with `hotfix` for `main` ("Merge feature/ff into hotfix", …, "Rebase
  hotfix onto feature/ff", "Interactive rebase of hotfix onto feature/ff…"); 380 high. Step 10
  (`hotfix`, current, no upstream): unchanged, 241 high.
- **Check 13b.** Step 1: check 2 step 4's `origin/feature/alpha` list, 293 high. Step 4's tag menu:
  unchanged.
- **Check 16.** Steps 1 and 5: OC1 for "ce5ea6a Alpha two" and "d93814d Fast-forward me".
- **Check 18b.** Step 1, the menu of `main` under Remotes › origin (sync, nothing fetched yet):
  "Check out origin/main" (disabled: "main is already checked out"); "Merge origin/main into main"
  (highlighted); "Merge origin/main into main (create a merge commit)"; "Rebase main onto
  origin/main" (disabled: "main is up to date with origin/main"); "Interactive rebase of main onto
  origin/main…"; —; "Create branch here…"; "Create tag here…"; —; "Delete main on origin…" (danger);
  —; "Copy branch name"; 293 high. Step 1's `18b1-current-branch-menu`: "Pull from origin/main"
  (highlighted); "Pull from origin/main (rebase)"; —; then phase 3's items; 354 high.
- **Check 19.** The step that clicks "Push published to origin/published" opens a menu with
  "Rebase main onto published" and "Interactive rebase of main onto published…" (both enabled) after
  the merges; not asserted.
- **Check 20.** Steps 2 (`20b`) and 5: header "published"; "Check out published" (highlighted);
  "Merge published into main"; "Merge published into main (create a merge commit)"; "Rebase main
  onto published"; "Interactive rebase of main onto published…"; "Fast-forward published to
  origin/published" (disabled: "published has commits that origin/published does not have"); —;
  "Push published to origin/published"; "Force push published…" (danger); —; "Create branch here…";
  "Create tag here…"; "Rename published…"; "Delete published…" (danger); —; "Change upstream…";
  "Stop tracking origin/published"; —; "Copy branch name"; 458 high.
- **Check 25.** Step 7's two right-clicks open OC1 menus for Alpha one (the second while `main` is
  one pick ahead: still not on `main` by SHA); not asserted.
- **Check 27** (light, 1920×1080). Step 1 (`27a`): OC1 for "ce5ea6a Alpha two", 284 high, no
  longer "all enabled" ("Interactive rebase of main from here…" disabled with "This commit is not on
  main"). Step 2: OC2 for Add core. Step 3: `feature/conflict` as check 13 step 6's list with `main`
  for `hotfix`; 380 high.
- **Check 28.** Step 1 (`28a`): header "topic"; "Check out topic" (highlighted); "Merge topic into
  main"; "Merge topic into main (create a merge commit)"; "Rebase main onto topic"; "Interactive
  rebase of main onto topic…"; —; "Push topic…"; —; "Create branch here…"; "Create tag here…";
  "Rename topic…"; "Delete topic…" (danger); —; "Set upstream…"; —; "Copy branch name"; 380 high.
  Step 4 (`28c`, `topic` tracking `origin/published`): the same five first items, then
  "Fast-forward topic to origin/published" (disabled: "topic has commits that origin/published does
  not have"); —; "Push topic to origin/published"; "Force push topic…" (danger); —; "Create branch
  here…"; "Create tag here…"; "Rename topic…"; "Delete topic…" (danger); —; "Change upstream…";
  "Stop tracking origin/published"; —; "Copy branch name"; 458 high. Step 5 (`topic` tracking
  `origin/main`): as step 4 with `origin/main` ("Fast-forward topic to origin/main" disabled: "topic
  has commits that origin/main does not have"); 458 high.
- **Check 29.** The step that clicks "Push topic…" opens step 1 of check 28's list (380 high); not
  asserted. The tag menus do not change.
- **Check 30.** Step 1 and step 3 (`30a`): check 20's list, with "Fast-forward published to
  origin/published" enabled at step 3; 458 high. Step 4 (`30c`): header "origin/published"; "Check
  out origin/published" (highlighted); "Merge origin/published into main"; "Merge origin/published
  into main (create a merge commit)"; "Rebase main onto origin/published"; "Interactive rebase of
  main onto origin/published…"; —; "Create branch here…"; "Create tag here…"; —; "Delete published
  on origin…" (danger); —; "Copy branch name"; 293 high. Step 5 (`30f`, the upstream gone): check
  20's list with "Fast-forward published to origin/published" (disabled: "origin/published no longer
  exists"), "Push published to origin/published" enabled and "Force push published…" (disabled:
  "origin/published no longer exists: push normally"); 458 high; the resting pointer's item is 52 px
  lower than before. Step 6 (`30g`, edges, HEAD detached, `main` dropped onto `side`): header "main
  → side"; "Fast-forward side to main" (disabled: "side is checked out in another worktree (<the
  folder of `edges-wt`>)"); "Check out side and merge main into it" (disabled: the same); —; "Check
  out main and rebase it onto side" (disabled: "main is up to date with side": `git merge-base
  --is-ancestor side main` exits 0); "Check out main and rebase it onto side interactively…"
  (highlighted, the only enabled item: a detached HEAD does not disable the "Check out … and rebase
  it" items, which check a branch out first, 14.3); 145 high (was 84).
- **Check 32.** Step 2: check 1 step 6's list for "419190f Detached work", 284 high (was 232). Step
  3 (`main` while HEAD is detached on its child): header "main"; "Check out main" (highlighted);
  "Merge main into HEAD" (disabled: "Check out a branch to merge into"); "Merge main into HEAD
  (create a merge commit)" (disabled: the same); "Rebase HEAD onto main" (disabled: "HEAD is up to
  date with main"); "Interactive rebase of HEAD onto main…" (disabled: "Check out a branch to
  rebase"); "Fast-forward main to origin/main" (disabled: "main is up to date with origin/main"); —;
  "Push main to origin/main"; "Force push main…" (danger); —; "Create branch here…"; "Create tag
  here…"; "Rename main…"; "Delete main…" (danger); —; "Change upstream…"; "Stop tracking
  origin/main"; —; "Copy branch name"; 458 high (was 406). Step 7 (HEAD on `main` again): the commit
  menu of Initial commit is OC2's shape ("Interactive rebase of main from here…" enabled); not
  asserted.
- **The real-window pass**: the pictures with these menus (`01a`, `01c`, `07b`) and the item lists
  its tests read change with them; parts B and C of the pass compare against the new lists.

### Changed in this report after its first commit

- **C14 step 3, where the result's menu opens** (changed on 2026-10-12, after the scripted check
  ran). The row said the menu's top-left corner is 2 right of and 2 below the pointer. At
  1100×700 line 5 of the result is at y 533, and 533 + 2 + 197 is past the window's bottom less
  4, so the menu cannot open below the pointer: phase 3's rule 4a opens it above, its bottom edge
  2 above the pointer, and the app does exactly that (`c14b-result-menu`). The row now says so;
  the part's conventions promise "2 below the pointer" only for a menu that fits there.

## How the expected git states were derived

Every git state in the checks was derived with plain Git 2.36.0.windows.1, in Git Bash on the
Windows machine, on 2026-10-12, on replicas of the scenario repositories: `bash` scripts that rebuild
"rebase", "interactive", "conflicts", the four stopped states of `ConflictsStoppedAsync` (with the
fake merge tool) and phase 3's "sync" with `PushFromElsewhereAsync` exactly as `TempRepo` builds
them (the same files, messages, identity, isolated environment and clock), and whose every commit
was checked SHA by SHA against the values `Phase4ScenarioTests` and `Phase3ScenarioTests` pin (the
last check of all of them ended `ALL PASS`). Each check ran on fresh copies of its own; each
operation the app makes ran with the git command lines the merged git layer builds and the app's
environment (`GitIsolation.AppCommitDates`, author and committer date 2026-01-02T09:00:00+00:00,
the repository's own identity, `GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`, `GIT_EDITOR=:`,
`GIT_SEQUENCE_EDITOR=:`, `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`, `GIT_LITERAL_PATHSPECS=1`), and each
step "outside the app" as `TempRepo` makes it. The graphs were laid out from the app's own log
command by `GraphLayout`'s rules (D48, D54, D55) and reproduce the graphs the scenario tests pin.
The replicas and the writers' scripts and logs lived in the session's scratch folder and are not
kept in the repository; the script names below are theirs.

The two gits: CI runs a newer git (2.55), which words some messages differently. As in phase 3, the
git texts this report gives word for word (the call log's lines, `ErrorOutput`, the lines of `git
diff --cached --check`) are asserted only when `git --version` is 2.36; on any other version a check
asserts the classifying ("key") line, that `ErrorOutput` is `GitOutputCleaner.Clean` of the failed
call's own output, and the dialog's title, summary and buttons. Git's states (refs, parents, trees,
files, the in-progress folders and files, `git status`) are asserted on every version: with the
dates fixed (D72) they do not depend on git's version. The gate's inspection compares the pictures
with the 2.36 texts, and the Git-layer tests run on both versions.

### Part A

- **Git**: 2.36.0.windows.1, in Git Bash on the Windows machine, 2026-10-12.
- **Repositories**: "rebase" and "sync" rebuilt with plain git exactly as `TempRepo` builds them
  (`Scenarios.RebaseAsync`, `Scenarios.RemoteSyncAsync`: the same files, messages, identity, dates
  on the scenario's clock and the same bare `origin`); their SHAs equal the pinned ones. Each check
  part ran on a fresh copy.
- **Operations**: every call the app makes was run with the app's environment (`GIT_AUTHOR_DATE`
  and `GIT_COMMITTER_DATE` 2026-01-02T09:00:00+00:00 as `GitIsolation.AppCommitDates` sets them, no
  author or committer variables, the repository's own identity, `GIT_CONFIG_NOSYSTEM=1`, an empty
  `GIT_CONFIG_GLOBAL`, `GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:`, `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`,
  `GIT_LITERAL_PATHSPECS=1`) and the argument lists the merged git layer builds:
  `GitRepository.RebaseAsync` (`git rebase <full sha>`), `CheckoutBranchAsync` (`git switch
  --no-guess <b>`), `IntegratePullAsync` with `PullIntegration.Rebase` (`git fetch --progress
  origin`, the fast-forward test `git merge-base --is-ancestor <old tip> FETCH_HEAD`, then
  `PullRebaseAsync`: the fork point `git merge-base --fork-point <upstream> HEAD` and the two
  `rev-list --count` calls of design 14.2, then `git rebase` alone), phase 2's stage of a resolved
  file (`git add --all -- <path>`), Continue's marker check (`git diff --cached --check --no-color
  --no-ext-diff -- <path>`), Continue at a rebase stop (`ContinueRebaseAsync`: `git commit
  --no-verify --cleanup=whitespace --file=-` with the message "Unpushed work" on standard input and
  `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_AUTHOR_DATE` taken from `rebase-merge/author-script`,
  then `git rebase --continue`), and `git rebase --skip`. The steps "before the app starts" were
  made as a terminal makes them, on the scenario's clock, as `TempRepo`'s `CheckoutAsync`,
  `PushFromElsewhereAsync` and `CommitFileAsync` make them. The backup refs were not made (they
  change no SHA); their targets are the old tips read before each rebase.
- **Graphs**: laid out from the app's own log command (`git log --date-order --branches --remotes
  --tags --ignore-missing HEAD --`) with `GraphLayout`'s rules (D48: the leftmost waiting lane, the
  lanes that wait for the commit end in its node, the first parent continues the lane and colour;
  D54: a commit no lane waits for takes the leftmost free lane, or a new one at the right, with the
  next colour number, and lanes ending at a commit are free before its parents get lanes; D55: a
  further parent joins a lane that already waits for it; trailing free lanes dropped; the lane count
  the widest row) and the label order of `CommitGraphData.BuildLabels` (kind, then name). Before any
  operation this reproduced exactly the graphs that `Phase4ScenarioTests` pins for "rebase" and
  `Phase3ScenarioTests` for "sync". The working-changes row's lane and colour are HEAD's node's
  (`CommitGraphControl.WorkingRowLane`).
- **Menus**: from design 7.1 and the reasons of 1.1, 2.1, 3 and 6, applied to the graph's facts
  (reachability from HEAD, HEAD's linear line, the tips HEAD reaches) and the drop facts git gave
  (`merge-base --is-ancestor`, all exit 1 in A3); phase 3's items from `RepositoryMenus.cs` as it
  stands.
- **What git did that the design does not spell out**: a clean rebase leaves `AUTO_MERGE` and the
  stopped-and-continued one `REBASE_HEAD` behind (design 0.1 says neither means "in progress");
  `git switch` writes a `checkout: moving from …` reflog line; `git rebase` without arguments names
  the upstream ref in its reflog (`rebase (start): checkout refs/remotes/origin/main`), where a menu
  rebase names the SHA; the untracked-file refusal prints `Aborting` before `error: could not detach
  HEAD`; `git rebase --continue` after the app's own commit makes no commit of its own and prints
  only `Successfully rebased and updated refs/heads/published.`; `git config --type=bool --get
  rebase.autoStash` exits 1 with no output when the key is unset; `rebase-merge/message` holds the
  subject and an empty line ("Unpushed work\n\n"), which the git layer trims before it commits.
  Nothing contradicted the design.

### Part B

Run on 2026-10-12 with Git 2.36.0.windows.1 in Git Bash on the Windows machine, on replicas built by
`report/replica/build-interactive.sh` (each copy's eight SHAs were compared with those
`Phase4ScenarioTests` pins and are equal), one fresh copy per check under `report-git/writer-B/`.
The scripts are `report-git/writer-B/b-run.sh` and `b-extra.sh`; their logs `run1.log` and
`extra.log`. A reviewer re-derived every state independently from the app's code
(`report-git/reviewer-B/rv-run.sh`, log `run.log`) and got the same SHAs, messages, dates and refs.

- **Environments.** What the app does ran with `env-app.sh`'s environment: `GIT_AUTHOR_DATE` and
  `GIT_COMMITTER_DATE` both `2026-01-02T09:00:00+00:00` (exactly `GitIsolation.AppCommitDates`), no
  `GIT_AUTHOR_*`/`GIT_COMMITTER_*` names (the identity is the repository's `user.name` Test Author,
  `user.email` author@example.com), `GIT_CONFIG_NOSYSTEM=1`, an empty `GIT_CONFIG_GLOBAL`,
  `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`, `GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:` (overridden for the
  rebase call only), `GIT_LITERAL_PATHSPECS=1`, `GIT_TERMINAL_PROMPT=0`. What a check does outside
  the app (B9's "Six") ran as `TempRepo.CommitFileAsync` does (`commit-file.sh`: `add --all`,
  `commit --quiet --allow-empty --message Six` at the copy's clock, 12:08).
- **The list** (B6): `git log --no-show-signature --no-color --encoding=UTF-8 --reverse
  --topo-order <Base>..<Five> --` (full SHAs; the range and order options of
  `ReadRebaseListAsync(onto, branch, fromCommit: true)`, 2.3, with a readable format) printed One,
  Two, Fix one, Three, Four, Five; `git rev-list --merges --count` printed 0. Git's own todo for
  `git rebase -i 4b4622c…`, captured by a sequence editor that copies it and exits 1 (nothing
  started; `## main` after it), held the same six picks in the same order.
- **B7.** The plan the list makes (oldest first: One Pick; Fix one Fixup; Two Reword with
  `Two reworded`; Three Pick with the edited message; Four Squash; Five Drop) written as
  `RebaseTodo.Build` writes it (read in `src/VisualCommit.Core/Git/RebaseTodo.cs`): `label vc-1`,
  `pick fc6c8fd1…`, `fixup ed589023…`, the guarded amend of group 1, `label vc-2`,
  `pick 938844a7…`, the guarded amend of group 2, `label vc-3`, `pick ff42e7fd…`,
  `fixup eef5c154…`, the guarded amend of group 3, `drop 57f089d4…` (full SHAs; each amend line
  exactly `RebaseTodo.GuardedAmend(n)`, which `guarded-amend.sh` prints). The message files:
  `vc-message-1` = One's own message as `git cat-file commit` gives it after the header (`One` LF: a
  group of fixups only keeps its target's exact bytes); `vc-message-2` = `Two reworded` without a line
  feed (`MessageDialogViewModel.Join` of a summary alone); `vc-message-3` = `Three and four` LF LF
  `#42 is the issue` (`Join` of the edited summary and description). They were written as UTF-8
  without a BOM with LF into a folder `visualcommit-rebase-<id>`, and git was run as
  `GitRepository.InteractiveRebaseAsync` runs it: the backup first (`git update-ref --stdin` with
  `create refs/visualcommit/backup/rebase-<time>/main 57f089d…`), then `git rebase -i 4b4622c…` with
  `GIT_SEQUENCE_EDITOR` set to WP1's function `vc() { cp '<folder>/'vc-message-* "$(dirname "$1")/" &&
  cp '<folder>/todo' "$1"; }; vc`. Exit 0, standard output empty, standard error `Rebasing (1/12)` …
  three `Executing: <the guarded amend>` lines … `Successfully rebased and updated refs/heads/main.`
  The result equals `examples.sh`'s and the pinning test's (`a081bff`, `b3507d3`, `ab4b49d`).
- **B8.** The list from Three's parent (`git log … -z --format=%H%x00%an%x00%s%x00%B --reverse
  --topo-order <Fix one>..<Five> --`, the app's own options) held Three (`%B` = `Three` LF LF
  `#42 is the issue` LF), Four and Five; git's own todo for `git rebase -i ed58902…` held the same
  three picks. `RebaseTodo.SquashPlan` makes Three a Pick with the dialog's message, Four a Squash
  and Five a Pick, so the todo was `label vc-1`,
  `pick ff42e7fd…`, `fixup eef5c154…`, the guarded amend of group 1, `pick 57f089d4…`, with
  `vc-message-1` = `Three` LF LF `#42 is the issue` LF LF `Four` (no final line feed: `Join` of the
  dialog's summary and description as filled, which are `MessageDialogViewModel.Split` of
  `RebaseTodo.SquashMessage` of Three's and Four's messages). Run as for B7, from `ed58902…`: exit 0,
  `Rebasing (1/5)` … `Successfully rebased and updated refs/heads/main.`
- **Graphs**: from `git log --date-order` of each result, laid out by D47, D48, D54 and D55 as the
  section above says (one chain: lane 0, colour 0 throughout).

What git did that a check must allow for:

- Git counts the `label` and `exec` lines in its progress (`Rebasing (n/12)` for B7, `(n/5)` for B8)
  and prints `Executing: …` for every amend line (design 0.2). None of it is shown when the rebase
  succeeds.
- After B8's rebase git leaves `.git/AUTO_MERGE` (and after both rebases `ORIG_HEAD`). Neither means
  "in progress" (design 0.1), so the checks look only for `rebase-merge/` and `rebase-apply/`.
- B7's group 1 guarded amend gives One the message it already has: the amended commit is byte for
  byte the commit git's fixup made (same tree, parent, author, message and the fixed commit date), so
  its SHA stays `ab4b49d`, and the reflog has no entry for that amend. B7's groups 2 and 3 are amended
  (`rebase: Two reworded`, `rebase: Three and four` in the reflog), and so is B8's group 1
  (`c9ea099 rebase (fixup): Three`, then `f34248d rebase: Three`: the fixup kept Three's message, the
  amend gave it the dialog's).
- Git's own squash, for comparison (`pick` Three, `squash` Four, `pick` Five with `GIT_EDITOR=:`),
  gives Three the message `Three` LF LF `Four` LF (`01c5913`): the `#42 is the issue` line is lost, as
  design 0.2 found. The app's message file keeps it (B7's `a081bff`, B8's `f34248d`).
- The rebases kept every author date and wrote every commit with the commit date 2026-01-02 09:00,
  the amends included. A leading pick whose parent is already the base keeps its commit: git
  fast-forwards over it (reflog `rebase: fast-forward`: B7's One `fc6c8fd`, B8's Three `ff42e7f`)
  before the fixup after it makes a new commit. Commits at or below the base are not touched (B8:
  Fix one, Two, One, Base and Initial commit keep their SHAs).

### Part C, checks C10 to C13

- **Git and tools.** Git 2.36.0.windows.1 in Git Bash on the Windows machine. The scenarios were
  built with the replica scripts (`report/replica/`, whose `verify.sh` ended `ALL PASS` against
  `Phase4ScenarioTests`' pins): `build-conflicts.sh` (C11, C12, C13 part C) and
  `conflicts-stopped.sh ROOT merge | rebase | cherrypicks` (C10, C13 parts A and B). C11's checkout
  before the app starts was `term-git.sh ROOT conflicts checkout --quiet feature/rebase`
  (`TempRepo.CheckoutAsync`). The scripts of this part and their logs are in
  `report-git/writer-C1/` (`scripts/c10.sh` to `scripts/c13.sh`, `c10.log` to `c13.log`, and
  `c10-stop-lanes.log` for the merge stop's graph).
- **The app's calls** went through `app-git.sh`, the app's environment: `GIT_AUTHOR_DATE` and
  `GIT_COMMITTER_DATE` 2026-01-02T09:00:00+00:00, no `GIT_AUTHOR_NAME`/`EMAIL` or committer
  variables (the repository's `user.name` and `user.email`), `GIT_EDITOR=:`,
  `GIT_SEQUENCE_EDITOR=:`, `LANGUAGE=en`, `LC_ALL=en_US.UTF-8`, `GIT_CONFIG_NOSYSTEM=1`, an empty
  `GIT_CONFIG_GLOBAL`, `GIT_LITERAL_PATHSPECS=1`:
  - C10: the result R3 written to notes.txt, `git add -- notes.txt`, `git commit --file=-
    --cleanup=whitespace` with `Merge branch 'feature/theirs'` (5.3, 5.4: the stage panel's boxes
    still hold what was filled, so Continue passes no message and the git layer reads `MERGE_MSG`
    less git's block).
  - C11: `git rebase <main's full SHA>` (1.3); at each stop the resolution written, `git add --
    notes.txt`, `git commit --no-verify --cleanup=whitespace --file=-` with `rebase-merge/message`
    less its trailing empty line, with `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL` and `GIT_AUTHOR_DATE`
    exported from `rebase-merge/author-script` (`app-git.sh --author-script`), then `git rebase
    --continue` (5.4, as `GitRepository.ContinueRebaseAsync` does).
  - C12: one `git cherry-pick` with the three full SHAs, oldest first (section 4); the resolution,
    `git add -- notes.txt`, `git commit --file=- --cleanup=whitespace` with the edited message
    (summary, a blank line, description), `git cherry-pick --continue` (5.4).
  - C13: `git rebase --skip` then `git rebase --abort` (A); `git cherry-pick --abort` (B); `git
    switch --no-guess feature/rebase`, `git rebase <main's full SHA>`, `git rebase --abort`, `git
    switch --no-guess main` (C, 1.3).
  - The backup refs, the `-changes` snapshots and `visualcommit-return` were not made: they are
    refs, commits outside every branch and a file the app's own code writes, none of which changes
    a SHA, a file or a ref the checks show; their targets are the HEAD values read at those
    moments (HEAD before the skip and before each abort, the branch's tip before the rebase).
- **The graphs** come from `lanes.py`, a transcription of `GraphLayout` (D48, D54, D55: colour
  numbers handed out in the order lanes start, a merge's further parent joining a lane that waits
  for it, free lanes reused leftmost first) fed with `git log --date-order` over `--branches
  --remotes --tags --ignore-missing HEAD`, as `GitRepository.LoadCommitsAsync` asks; labels in
  phase 1's order. It gives `Phase4ScenarioTests`' pinned graph of "conflicts" row for row (lanes
  and the lanes ending at Base) and the pinned graphs of the merge, rebase and cherry-pick stops.
  The Graph column and the hidden Date column follow phase 3's "Window sizes".
- **Where the banner's buttons stand** follows from 5.2's rule and phase 3's banner
  (`ConflictBanner.axaml`: the icon 16 wide with margins 12 and 8, the text's right margin 8,
  `BannerAbortButton` a `VcActionButton` with padding 10 each side and a 1-px border; the new
  buttons "in the same style", 5.2). The labels were measured in Inter Regular at 12.5 (the main
  window's `FontSize`; the font taken from the resources of `Avalonia.Fonts.Inter` 12.1.3,
  `report-git/writer-C1/font/measure.py`, advance widths without kerning): `Continue` 53, `Skip` 25,
  `Abort merge` 74, `Abort rebase` 77, `Abort cherry-pick` 104, so the buttons are about 75, 47, 96,
  99 and 126 wide. With 6 between buttons and 8 at the right, the text keeps about 211 (merge), 155
  (rebase) and 127 (list) beside them in a banner 440 wide, under 240 (with `VcActionButton`'s own
  padding of 12 the buttons are wider still), and about 611, 554 and 527 in one 840 wide. These
  numbers decide only the placement; the checks assert the rule (`AssertBanner`), never a width.
- **What git did that is worth knowing:**
  - `rebase-merge/message` holds the message followed by an empty line (`First change\n\n`); the
    stage panel's summary and the app's commit use it trimmed (`Tidy`), so the commit's message is
    `First change` alone.
  - The commit the app makes at a stopped pick keeps the picked commit's author date although the
    app's environment sets `GIT_AUTHOR_DATE`: git takes the author from `CHERRY_PICK_HEAD`. At a
    stopped rebase the author date comes from `author-script` only because the app passes it.
  - `git rebase --continue` after the app's own commit makes no other commit and maps the stopped
    commit to it in `rewritten-list` (`70e8c78 99070f8`).
  - `git rebase --skip` leaves the skipped pick in `done` (so the banner counts `2 of 2`) and writes
    `70e8c78 da31c97` into `rewritten-list`, mapping the skipped commit to HEAD.
  - After First change is skipped, Second change's merge base is First change, whose line 2 equals
    Second change's, so main's `line 2 ours` stays and only line 10 conflicts (FS2).
  - The merge stopped in a terminal labels the theirs side with the name as typed
    (`>>>>>>> feature/theirs`); git merged line 6 from feature/theirs into the working-tree file, so
    the resolver's ours pane shows `line 6 theirs` too.
  - `git cherry-pick` with three commits writes `sequencer/head`, `abort-safety` and `todo` and no
    `opts` file; `todo` names the commits by 7-character SHAs with their subjects.
  - After C11's rebase `REBASE_HEAD` and `ORIG_HEAD` stay (no `AUTO_MERGE`), after C12's list
    `AUTO_MERGE`, after C10's merge and every abort of C13 `ORIG_HEAD`; none of them means "in
    progress" (design 0.1), and no check reads them.
  - `git rebase --abort` and `git cherry-pick --abort` printed nothing and exited 0.

### Part C, checks C14 to C17, and part D

The runs used Git 2.36.0.windows.1 in Git Bash on the Windows machine, on 2026-10-12. They ran on
replica scripts kept outside the repository, which rebuild the scenarios as `TempRepo` does (every
pinned SHA matched). Each check ran on fresh replicas of its own; "conflicts" and "interactive" were
never built in one root, since a shared clock gives other SHAs.

- **Starting states.**
  - The stops of C14, C15 and C16 are fresh copies of "conflicts" stopped by git as
    `ConflictsStoppedAsync` stops them (`Merge`, `MergeKinds`, and `Merge` with the fake merge
    tool), at 12:10.
  - C17 and D18 start from "conflicts" as built. Their steps outside the app ran as `TempRepo`'s
    `GitAsync` runs them: `switch feature/picks`, `rebase main` and `add notes.txt`;
    `config merge.conflictStyle diff3` and `merge feature/theirs`.
- **The app's steps** ran in the app's environment: `GitIsolation.AppCommitDates`
  (`GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` set to 2026-01-02T09:00:00+00:00), the identity from
  the repository's configuration, `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:`, `LANGUAGE=en`,
  `LC_ALL=en_US.UTF-8`, `GIT_CONFIG_NOSYSTEM=1`, and an empty `GIT_CONFIG_GLOBAL`. The commands
  match the app's code:
  - C14: `add --all -- notes.txt`, as `StageAsync` runs it, and `diff --cached --check --no-color
    --no-ext-diff -- notes.txt`, as `ReadStagedConflictMarkersAsync` runs it. Take theirs reads
    stage 3, `6216606`; `git cat-file -p 6216606` gave the 12 lines of step 7.
  - C15: `add -- other.txt`. Then the snapshot, made as `SaveSnapshotAsync` makes it: a temporary
    index filled by `read-tree HEAD` and `add --all --force -- image.png`, then `write-tree`, and
    `commit-tree -p HEAD` with VisualCommit's identity and the app's dates. Then `checkout --theirs --
    image.png`, `add -- image.png`, the resolved new.txt written, and `add -- new.txt`.
  - C16: `-c mergetool.keepBackup=false mergetool --tool=fake --no-prompt -- notes.txt`.
  - C17: `commit --no-verify --cleanup=whitespace --file=-` with `rebase-merge/message` on standard
    input and the author's name, e-mail and date from `rebase-merge/author-script`, then
    `rebase --continue`.
- **The results.** Every SHA, blob, file, porcelain line and `rebase-merge/` file in the expected
  results was copied from the runs' output.
  - GC-M is the pinned graph.
  - K1's lanes are the pinned ones of `Stopped_states_keep_the_graph_except_the_picked_commit`
    ("d71cdc4 Pick clean · lane 0" above the conflicts graph). The terminal rebase gives the same
    commits, in the same `--date-order`, as the CherryPicks stop.
  - K2 and G-I were laid out by hand from `git log --date-order --branches --remotes --tags HEAD` with
    D48, D54 and D55. A colour is the counter of lanes started (`GraphLayout._nextColor`). The labels
    follow phase 1's order.
- **The banner's button rows.** These follow 5.2's rule with the banner's layout from
  `ConflictBanner.axaml`: an icon column of 12 + 16 + 8 = 36, the text's right margin of 8, and
  buttons with padding 10 and border 1 on each side, 6 apart and 8 from the edge. The labels were
  measured in Inter Regular at the window's 12.5. The font was read from the copy embedded in
  Avalonia.Fonts.Inter 12.1.3, with fontTools' advance widths:

  | Label | Width | Button |
  |---|---|---|
  | `Continue` | 53.2 | 75.2 |
  | `Skip` | 25.4 | 47.4 |
  | `Abort merge` | 74.1 | 96.1 |
  | `Abort rebase` | 76.9 | 98.9 |

  - At 440, `Continue` with `Abort merge` takes 185.3 with its gap and right margin. With the icon
    column that leaves a text column of 218.7, or 210.7 inside the text's right margin: under 240
    either way. With `Skip` and `Abort rebase` the buttons take 241.5 and the text column is 162.5
    (154.5). So the buttons go below the text.
  - At 840 the merge banner's text column is 618.7 (610.7), and at 1660 more, so the buttons stand
    beside the text.
  - At 1660 the 353-px merge text fits one line many times over.
  - These are measurements of the font, not of the built app. Only the side the buttons go to is
    asserted.

What git did that one might not expect:

- **The rebase.**
  - A terminal rebase's first pick and the CherryPicks stop's pick are the same commit, `d71cdc4`:
    same parent, tree, message, author date 12:05 and committer date 12:10.
  - `rebase-merge/message` ends with a blank line (`Pick conflicting\n\n`). `--cleanup=whitespace`
    drops it, so the commit's message is `Pick conflicting\n`.
  - git's stdout at the stop was `Auto-merging notes.txt` / `CONFLICT (content): Merge conflict in
    notes.txt`. Its stderr was `Rebasing (1/3)`, `Rebasing (2/3)`, `error: could not apply 01fd976...
    Pick conflicting`, four `hint:` lines, and `Could not apply 01fd976... Pick conflicting`. Between
    `Rebasing (1/3)` and the stop, `rebase-merge/` already exists with one command done and no
    conflict, which is why C17's step 2 waits for the stop's exact banner text.
- **diff3.** Git 2.36's diff3 labels the base section of a merge with the merge base's short SHA
  (`||||||| 195768b`), not with "merged common ancestors". The two blocks stay apart, and line 6
  still merges cleanly.
- **The snapshot before "Use theirs".** Its tree is HEAD's own (`ba439dd`), because the working-tree
  image.png is ours, which equals HEAD's. The design's rule saves it anyway, because it compares
  with the stage about to be written (theirs). With the app's dates its id was `82e508b`, but the
  check asserts it by parent and content.
- **"Keep the file" on other.txt** stages main's own blob. After it, `git status` no longer lists
  other.txt at all.
- **`git mergetool`** with the fake tool exited 0. It printed only on stdout (`Merging:`, …, with an
  empty line after the file's name), left no file behind (`keepBackup=false`, `trustExitCode=true`),
  and staged the file. MERGE_HEAD stays.
- **`git diff --cached --check`** with FM staged reports all six marker lines and exits 2.

## Found by the gate

Faults the gate found, and the commit that fixed each.

## Could not run

Checks or parts of the gate that could not run on the Windows machine either, why, and what that
leaves unproven.

## Other platforms

The CI run of the gate's commit and its result in each job, and which of the screenshots CI took on
macOS and Linux were opened.

## macOS checklist for the owner

Written at the gate.
