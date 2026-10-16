# Phase 5 handoff — History, traceability and undo

Written at the end of phase 5 for the session that runs phase 6. That session has not seen any
of this phase's conversation. How the app is built is in [architecture.md](../architecture.md),
why in [decisions.md](../decisions.md) (D153 to D185 are this phase's); the phase's design, kept
as history, is [phase-5-design.md](../phase-5-design.md), whose sections 15.8 and 15.9 say what the
report, the build, the review and the gate settled. The report,
[test-reports/phase-5.md](../test-reports/phase-5.md), holds the 27 checks and its own decisions
(AD, BD, FD, UD and PD), which bind where they differ from the design (D183). This file says what
was proven, what to copy, and what to watch out for.

## Result

Everything in the plan's "Delivers" list was built. The check names are those of the report (27
checks in five parts: A the shell and the activity log, B the graph area's pages, F search,
compare, solo and hide, U undo and redo, P protected changes); its section "What proves each
Delivers item" is the longer form of this table, with every destructive action of phases 2 to 4
and the check that undoes or restores it.

| Delivered | Proven by |
|---|---|
| File history following renames and blame, in place of the graph, with click-through to commits (D158); the details panel's "All files" (D164), so any line of any file is two clicks from its commit | B5, B6, B7; `FileHistoryTests`, `BlameParserTests`, `BlameTests` (Git), `FileHistoryViewModelTests`, `BlameViewModelTests` |
| The reflog page with restore, and the backups list in it; the activity log panel under the graph, opened from the status bar (D156) | B8, B9, A2, A3; `ReflogTests`, `BackupReaderTests` (Git), `ReflogViewModelTests`, `KeptCommitGoneTests`, `UndoRestoreTests`, `ActivityLogViewModelTests`, `ActivityLogViewTests`, `OperationLogTests`, `GitCallLogTests` |
| Commit search by message, author, SHA and file, and inside changes; matches marked in the graph (D157) | F10, F11, A4; `SearchTests` (Git), `SearchViewModelTests`, `SearchBarTests`, `CommitGraphControlTests` (marks) |
| Compare two commits or branches; two selected commits compared at once (D162) | F12, F13, F14 step 5; `CompareTests` (Git), `CompareViewModelTests`, `CompareViewTests` |
| Undo and redo of the app's git actions, many steps back within the session (D154); a change to a remote asked about, then pushed back with a lease (D155, D170); Ctrl+Z and Ctrl+Y outside text boxes (D163) | U16 to U23, P25 part B, A1, A4; `UndoHistoryTests`, `UndoSequenceTests`, `UndoScopeTests` (of Core's code, in `VisualCommit.Git.Tests`), `UndoEffectTests`, `UndoStepTests`, `UndoStepBranchTests`, `UndoStepFileTests`, `UndoStepRemoteTests`, `UndoStepRefusalTests` (Git), `UndoRecordingTests`, `UndoRestoreTests`, `UndoStepTests`, `UndoToolbarTests`, `WindowKeysTests` (App) |
| Solo and hide, remembered per repository across restarts (D159) | F14; `GraphFilterTests`, `GraphFilterArgumentsTests`, `CommitPagerTests` (Git), `CommitGraphDataTests`, `LeftPanelViewModelTests`, `GraphFilterChangesTests`, `HideAndSoloTests`, `RepositoryViewModelFilterTests` |
| C12 (D153): the refused checkout with git's count and "Stash changes and check out"; the automatic stash, kept and drawn as a ring; every stash's line dashed (D161) | P24 to P27, F15; `ProtectedChangesTests`, `FailureClassifierTests`, `GraphLayoutTests` (`IsDashed`), `Phase5ScenarioTests` (Git), `RepositoryOperationsProtectTests`, `RepositoryOperationsCheckoutTests`, `CommitGraphControlTests` |
| Phase 4's leftovers (D160): the details panel's SHA, the resolver's disabled arrows, Cancel's tooltip while the merge tool runs, stale comments, the watcher's quiet period on a clock | phase 4's `a3d`, `b8c`, `c11j` and its resolver pictures with one conflicted file; A2 step 8 (the tooltip); the leftovers' tests (`279dd8c`) |
| Undo reads the backups; the clean-up keeps what an undo needs; the clean-up's lines in the activity log | B8, B9, A2, U16 to U23; `BackupReaderTests`, `UndoRecordingTests` (the keep set), `RepositoryOperationsBackupCleanupTests` |
| The activity log reads `OperationLog` and `IGitCallLog`, with the background "Clean up backups" and a success that is a warning | A2; `OperationLogTests`, `ActivityLogViewModelTests` |
| From phase 3's menus: compare on a two-commit selection, file history and blame in the file menus, solo and hide in the branch menus; the toolbar's Undo, Redo and Search made real | F12, F13, B5, B6, B7, F14, A1, A4, F10; `RepositoryMenusTests`, `UndoToolbarTests`, `ToolbarTests` |

Every check passed in both passes on the gate's commit (the real-window pass on its second
attempt, below). The gate's first run, on `4281f4c`, found five problems, fixed before the second
run (the report's "Found by the gate"):

- `f11f`: after a commit made outside the app with the search open, the graph showed the new row
  with the old refs' labels for one refresh. `ReadRefsAsync` ran `for-each-ref` and `rev-parse
  HEAD` side by side, and a commit landing between them gave HEAD the new commit and `main` the
  old one. HEAD's commit now comes from its branch's line of the same read (`042c7d9`).
- `f10a`, `f11a`, `f11d`: the search bar's disabled Previous and Next were drawn as enabled; the
  `VcIconButton` theme had no disabled style. Fixed on the theme (`260b658`), which also changed
  phase 4's `c14h` and made D160's own rule for the resolver's arrows redundant (removed).
- Seven earlier pictures caught the two-commit comparison (D162) still loading: phase 3's `11d`,
  `11g`, `12b`, `24g` and phase 4's `a1g`, `a1h`, `b8a`. The checks now wait for its file list,
  `ShellDriver.WaitForComparisonShownAsync` (`d8ed002`).
- Phase 2's real-window `17c` caught a status-bar tooltip: the restore bar's Restore is now the
  discard's Undo, whose longer status text has a tooltip, and the pass rested the pointer on that
  text. It rests at the neutral point now (`9be8bac`).
- `b7b`, `b7c`, `b7e`, `b7f`: the blame header's caption cut with "…". By design (a
  `TrimmedTextBlock` with its tooltip); the report now says so (`aa1b7c2`).

The gate's second run adds a sixth entry there, Q1 on a hot laptop, which needed no change (State
below).

Nothing was moved to a later phase. One follow-up that design 14.2 proposed is now listed under
phase 6 in plan.md: whether every checkout keeps ignored files. C12 passes `--no-overwrite-ignore`
only when checking out a commit the current branch reaches, other than HEAD's own (D179, D183);
the settings screen may offer it for every checkout. Limits by design, not faults: the undo
history lives for the app's session, at most 100 entries per worktree (D154, D167); staging,
unstaging, applying lines, fetches, the backups' clean-up, the resolver's resolutions and Abort
are not recorded (D168); an Abort is undone from the backups list, without git's stopped state
(D165); the reflog page reads one reflog at a time, HEAD's or a local branch's, up to its newest
500 entries (D180); file history leaves out merges (D175); the activity log keeps the last 500
operations, their calls within 64 MB, and a ring of 2,000 reads (D173); search does not see hidden
branches (D174); the filters of the 100 most recently used repositories are kept (D178).

## State of the repo

Branch `phase/5-history`, built from 2026-10-12 to 2026-10-16 on the Windows machine: 257 commits
on top of `master` (`0883669`) up to the gate's commit. The visual test gate passed on `1b2e734`,
the gate's commit (2026-10-16):

- `dotnet test`: 3,233 of 3,233 passed in 43 min 22 s (16:20 to 17:03 UTC). Phase 4's gate had
  2,411 tests.
- The real-window pass: its first attempt failed one test of 59, Q1 at 2,613 ms (limit 2,000), on a
  laptop hot from the default run. Alone and rested, Q1 measured 1,147 ms (phase 4's pass: 937 ms),
  so the pass was run again rested: 59 of 59 in 24 min 57 s (17:49 to 18:14 UTC), Q1 1,826 ms
  within the pass, graph frames 1.1 ms at the 95th percentile and 9.8 ms at the longest.
- Pictures: `docs/test-reports/phase-5-pictures.sha256` lists the 1,080 the gate inspected: 774
  scripted (phase 5: 264; phases 0 to 4: 510), 155 real-window captures (phase 5: 48, from 15 RW
  checks; phases 0 to 4: 107) and 151 difference pictures. The first run's inspection (twelve Opus
  inspectors) judged all 1,080; it opened every one but the 24 earlier scripted pictures that are
  byte-identical to their reference, which keep phase 4's verdict. Fifteen scripted pictures and
  three real-window captures showed the five problems above (phase 3's real-window `11d` and `11g`
  were right, their scripted twins were not), and every other picture passed. The second run's
  inspection opened the 32 scripted pictures and 16
  real-window captures whose bytes changed since the first run, with their 17 changed difference
  pictures, and all passed (the fixes, and variance by nature: temporary names, progress, carets,
  a few anti-aliased pixels); the other 1,015 files are byte-identical and keep the first run's
  verdict. The inspectors' verdicts are kept outside the repo, in
  `D:/Projects/Personal/gate5-inspection/` (`run1-verdicts.json`, the run lists).
- The gate's first run, on `4281f4c`: `dotnet test` 3,227 of 3,227 in 45 min 37 s, the real-window
  pass 59 of 59 in 26 min 26 s (Q1 1,749 ms), CI run 38909397699 green in all five jobs.

CI run [38931127856](https://github.com/RahimPasha/VisualCommit/actions/runs/38931127856) built
and tested `1b2e734` with the same 3,233 tests, and all five jobs succeeded: the Test step took
25 min 15 s on `windows-latest` (limit 40, D184; the job 27 min 50 s, its "Build the real-window
pass" step green), 16 min 13 s on `macos-latest`, 3 min 26 s on `ubuntu-latest` and about 4
minutes in each bare ubuntu container. The report's "Other platforms" names the eleven of CI's
macOS and Linux pictures that were opened.

Before the gate, an independent review of the code (step 25: six reviewers, for git safety,
threads and lifetimes, performance, conformance with the report and design, code quality and test
quality, each finding verified by a skeptic) confirmed 43 findings, 1 blocker, 9 major and 33
minor, and refuted 3. The blocker: an undo or a restore that puts a snapshot back deleted, without
a word, what stood in its way (a folder holding files where the snapshot has a file, or a file
where it has a folder), because `git restore --worktree` writes with force. All were fixed with
tests (`b175509`, `4281f4c`); design 15.9 records every behaviour they changed.

The commits after the gate's commit change only docs. The phase is "Awaiting acceptance": the
owner has not yet said "Phase 5 accepted", and `phase/5-history` is not merged into `master`. Only
the real window at 1920×1080 could not run, as the plan allows on this screen (B5's
`Check_B5_side_by_side_light` and F10's `Check_F10_search_light_1920` are scripted only).

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300 on an Intel Core i7-8650U laptop; screen 3000×2000 at 200% scaling (1500×952 logical work area). The whole phase was built on this machine; no cloud session worked on it |
| .NET SDK | 10.0.303 (`global.json` stays at 10.0.100 with `latestFeature`) |
| Git | 2.36.0.windows.1 locally, with `core.fsmonitor=true` in the owner's global configuration. Visual Studio's 2.55.0.windows.5 is on the machine too and was used to replay git's texts (status.md's Notes give its path; put its `mingw64/bin` first on `PATH`). On CI since 2026-10-15: Windows 2.56.0.windows.2 (2.55.0 before), macOS 2.56.0 (Homebrew), `ubuntu-latest` 2.55.0, the two containers 2.43.0 |
| Packages | Unchanged since phase 2: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0. New Lucide icons; the two that derive from Feather (crosshair, eye-off) are added to `THIRD-PARTY-NOTICES.md` |
| New projects | `tests/VisualCommit.VisualTests.Phase5` (parts A, B, F and P, 34 test methods) and `tests/VisualCommit.VisualTests.Phase5Undo` (part U, 55 methods), D181: seven test processes now |
| Tools | `scripts/compare-pictures.py` needs Python with Pillow and NumPy |
| Screenshots | Scripted: scaling 1. Real window: 200% and a work area of 1500×952 logical pixels, as `run.txt` records them |

## Build, run and test

The table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands). New in
this phase: phase 5's two walk-through projects, `scripts/time-scripted-check.sh` taking phase 5
when no phase is given (part U goes to the undo project), and `scripts/compare-pictures.py`.

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
dotnet test --project tests/VisualCommit.VisualTests.Phase5
dotnet test --project tests/VisualCommit.VisualTests.Phase5 --filter-class "*Phase5ChecksB"
dotnet test --project tests/VisualCommit.VisualTests.Phase5Undo --filter-method "*Check_U17*"
dotnet test --project tests/VisualCommit.App.Tests --filter-class "*UndoRecordingTests"
bash scripts/time-scripted-check.sh "*Check_U16*"
python scripts/compare-pictures.py <reference folder> artifacts/visual <output folder>
dotnet test --project tests/VisualCommit.RealWindowTests -c Release
```

- The default run took 43 min 22 s on the gate's commit and 45 min 37 s on the first run's
  (phase 4's gate: 21 min 52 s); a full run during the build took 50 minutes (`4bec368`). Side by
  side on the gate's commit: `VisualCommit.VisualTests` 12 min 8 s,
  `VisualCommit.VisualTests.Phase4` 26 min 15 s, `VisualCommit.VisualTests.Phase5` 29 min 50 s,
  `VisualCommit.Git.Tests` 38 min 19 s, `VisualCommit.VisualTests.Phase5Undo` 41 min 43 s,
  `VisualCommit.VisualTests.Phase3` 41 min 46 s, `VisualCommit.App.Tests` 43 min 8 s. It is bound
  by the processors; with seven projects side by side a step takes 5 to 8 times as long as
  alone, which is why the headless harness's default wait is 30 s now
  (`ShellDriver.DefaultWaitTimeout`, `5bdd1de`).
- While working, run one class or method. Several `--filter-method` options in one run combine
  with OR; `--filter-class` and `--filter-method` together select their intersection (often
  nothing). A partial run that takes screenshots empties that phase's picture folder first (for
  phase 5, the one folder both projects write), so a gate's pictures are gone after any partial
  run of the same phase: run the gate last.
- Phase 5's check classes are `Phase5ChecksA`, `B`, `F` and `P` (Phase5 project) and
  `Phase5ChecksU` (Phase5Undo); a test is named `Check_<id>_<what>`
  (`Check_A1_toolbar_and_status_bar`). Every U check is several methods, one action with its undo
  and redo each, on a fresh copy of its scenario.

## Running the visual gate

As in phase 4's handoff (which gives the commands that list and stop the git fsmonitor daemons)
and phase 3's (its step 0: worktrees removed), with two more projects, longer passes, and
reference pictures for the earlier phases (design 15.7):

1. **Before the gate, make the references.** Phase 5 changed nearly every earlier picture (the
   toolbar, the status bar, the details panel's buttons, new menu items). The lead ran phases 0
   to 4's walk-throughs on `master` in a worktree (510 pictures; 485 matched phase 4's list and
   25 vary by nature) and copied phase 4's 210 real-window files before phase 5's first
   real-window pass, into `D:/Projects/Personal/gate5-reference/`, outside the repo.
   `scripts/compare-pictures.py` then made a difference picture per changed picture and a
   `summary.md` grouped by the window regions the changes lie in: 24 of the 510 earlier scripted
   pictures were byte-identical, 486 changed as the report's "Changes to expected results" lists.
2. Let the laptop rest, then `dotnet test` at the repo root: about 45 minutes. It writes
   `artifacts/visual/phase-{0,1,2,3,4,5}/scripted/`, emptying each first.
3. Let the laptop rest well, **tell the owner**, and run
   `dotnet test --project tests/VisualCommit.RealWindowTests -c Release` (never `--no-build`):
   about 25 minutes for phases 0 to 5 (59 tests) with the real mouse, on an unlocked desktop. It
   compares its captures with step 2's pictures, so nothing may run a partial walk-through in
   between; its captures, difference pictures and `run.txt` land under
   `artifacts/visual/phase-N/real-window/`. After the 43-minute default run the laptop was still
   hot, and Q1 failed at 2,613 ms. Phase 5's part is `Phase5RealWindowPass.cs` and `.A.cs`,
   `.B.cs`, `.F.cs`, `.U.cs`, `.P.cs`, with `RealApp.Phase5.cs`; `RealApp.Launch` sets the frozen
   record clock and the fixed backup clock (`VISUALCOMMIT_RECORD_CLOCK=frozen`,
   `VISUALCOMMIT_BACKUP_CLOCK=fixed`). Q1 is phase 1's
   `Checks_8_and_9_the_100k_commit_repo_and_Q1`; running it alone with `--filter-method` empties
   phase 1's `real-window/` folder, so the whole pass runs again afterwards.
4. Save `sha256sum` of the pictures before anything else runs (CLAUDE.md gives the command), then
   inspect. Phase 5's gate gave twelve Opus inspectors, started side by side by one Claude Code
   workflow, a batch each: phase 5's scripted pictures by part (A, B, F, P, U16 to U19, U20 to
   U23), its real-window captures, phases 0 to 2's scripted, phase 3's in two halves, phase 4's,
   and the earlier real-window captures. Phase 5's pictures were judged in full against the
   report; the earlier ones against their reference, their difference picture and the changes the
   report lists, and in full where a change lies outside those regions.
5. After a fix, run both passes again on the new commit and compare the new list with the last
   run's: only pictures whose bytes changed need inspecting again. Between phase 5's two runs on
   the same machine 139 of 155 real-window captures were byte-identical; the 16 that changed were
   the fixes and pictures that vary by nature (a text box's caret, a temporary folder's name,
   progress).

## What changed in the code

| To do this | Start here |
|---|---|
| Record a git action for undo, or change how one is undone | `RunOptions.Undo` (`RepositoryOperations.Run.cs`) with an `UndoScope` (`Core/Undo/UndoScope.cs`, one factory per kind, such as `UndoScope.Commit`); the kinds, sequences, effects and checks in `Core/Undo/` (`UndoKind.cs`, `UndoSequence.cs`, `UndoEffect.cs`, `UndoChecks.cs`, `UndoHistory.cs`); the reads and effects in `GitRepository.Undo.cs` (`ReadRefStatesAsync`, `RunUndoStepAsync`); the recording, the steps, the toolbar's state and the backups list's restores in `RepositoryOperations.Undo.cs`; texts in `OperationTexts.Undo.cs`; one history per worktree in `AppSession.cs` and `TabServices.cs` |
| The activity log | Records: `OperationLog` (Core/Operations), `GitCallLog`, `GitRunner`, `GitCallRecord`; the panel: `ViewModels/Panels/ActivityLogViewModel.cs`, `ActivityLogRows.cs`, `Views/Shell/ActivityLogView.axaml(.cs)`, `ActivityLogParts.cs`; its place and splitter in `Views/Shell/CommitGraphView.axaml`; its height in `SessionState`; its row menu `RepositoryMenus.ActivityLog.cs` |
| A page in place of the graph | `ViewModels/Pages/IGraphAreaPage.cs` (with `IGraphAreaPageHost`), `GraphAreaPageViewModel.cs`, `FileHistoryViewModel.cs`, `BlameViewModel.cs`, `ReflogViewModel.cs` (with the backups list); `Views/Pages/`; blame's gutter and editor in `Controls/Blame/`; `RepositoryViewModel.Pages.cs` (open, close, `ShowCommit`, `ShowInGraph`, `Reveal`); `RepositoryMenus.Pages.cs`; git's reads in `GitRepository.FileHistory.cs` and `.Reflog.cs` |
| Search and compare | `RepositoryViewModel.Find.cs` (`ISearchHost`, `ICompareHost`); `ViewModels/Graph/SearchViewModel.cs`, `Views/Graph/SearchBarView.axaml(.cs)`, `Controls/CommitGraphControl.Search.cs`, `GraphSearchMarks.cs`, `GitRepository.Search.cs`; `ViewModels/Shell/CompareViewModel.cs`, `Views/Shell/CompareView.axaml(.cs)`, `GitRepository.Compare.cs` |
| Solo and hide | `Core/Graph/GraphFilter.cs` (passed to `LoadCommitsAsync` and `CommitPager`), `ViewModels/Graph/GraphFilterChanges.cs`, `Services/GraphFilterStore.cs` (the session), `RepositoryViewModel.Filters.cs`, `RepositoryOperations.Filters.cs`, `RepositoryMenus.Filters.cs`, the left panel's marks and toggles (`LeftPanelViewModel.cs`, `LeftPanelView.axaml`) |
| C12 and the automatic stash | `GitRepository.Branches.cs` (`--no-overwrite-ignore`, the stash's steps), `GitRepository.Stash.cs`, `Core/Operations/GitFailureClassifier.cs` (git's count, the folder and ignored-file kinds), `RepositoryOperations.Branches.cs` (every checkout path), `RepositoryOperations.Protect.cs`, `OperationTexts.Protect.cs`, the apply check in `RepositoryOperations.StashTags.cs` |
| The refs as one snapshot | `GitRepository.Refs.cs`, `ReadRefsAsync` (HEAD's commit from its branch's line of the same `for-each-ref`) |
| Dashed stash lines | `Core/Graph/GraphLayout.cs` (`GraphLine.IsDashed`), `Controls/CommitGraphControl.Render.cs` |
| The window's keys | `Views/MainWindow.axaml.cs` (Ctrl+F, Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z; design 1.3), `Views/IEscapeHandler.cs` (`EscapeOrder` for the new panels) |
| Clocks the harnesses fix | `Core/AppClocks.cs` (the record clock), `Core/Git/BackupClock.cs` (backup names and the clean-up's "now") |
| A visual check of phase 5 | `tests/VisualCommit.VisualTests.Phase5/Phase5Checks.<part>*.cs` with `Phase5Expectations.<part>.cs`, and the same in `VisualCommit.VisualTests.Phase5Undo`; the drivers `ShellDriver.Phase5.*.cs` (Find, Graph, Pages, Protect, Shell, Undo); scenarios in `tests/VisualCommit.Testing/Scenarios.Phase5.cs` (`HistoryAsync`, `FiltersAsync`, `ReflogAsync`, `ProtectAsync`), pinned by `Phase5ScenarioTests` |

## Patterns to follow

| For | Example |
|---|---|
| A git action that can be undone | Give its `RunAsync` call an `UndoScope` naming what it may change; the recording reads that state before and after inside the action's own operation (design 7.7). A new kind of action needs an `UndoKind` with its entry text and its undo and redo sequences in `UndoSequence.cs`; an action left out says why (D168) |
| A view in place of the graph | `FileHistoryViewModel`: an `IGraphAreaPage` that holds no work, changes the right panel only when a row is picked, and reveals that commit on close (D176); `PageEscape` for Esc |
| Rows that git streams | `FileHistoryViewModel`: rows reach the list in batches (`RowCollection.AddRange`), never one UI job per row |
| A commit kept only by a reflog or a backup | `CreateBranchAtKeptCommitAsync` and its siblings: `CommitExistsAsync` inside the operation before anything reads the commit (`KeptCommitGoneTests`) |
| Text that may be cut | `TrimmedTextBlock` with the whole text in its tooltip; `LayoutAudit.FindClippedText` with `allowShortenedWithToolTip` lets it through |
| A refusal that must change nothing | P24 and P27 with `TempRepo.ExactStateAsync` (HEAD, every ref, the index, the status with untracked and ignored files, every file with its contents, as one text), which the scenario pins, both harnesses and the App and Git tests all read; HEAD's reflog is asserted beside it |
| Times on screen and in backup names | The record clock and the backup clock (`AppSession.Start(..., recordClock, backupClock)` in `ShellDriver.Start`, the variables in `RealApp.Launch`); `ManualTimeProvider` for a quiet period or a guard (the watcher, D160) |

Rules worth keeping (phases 3 and 4's still hold: two gits, dates, paths, waits, clocks, isolation,
classify by files and states, never by git's sentences):

- **A snapshot comes from one read.** Two git reads side by side can straddle an outside change
  (the refs race above). Take related values from the same call's output.
- **Git differs between versions in exit codes and side effects, not only in words.** A refused
  `git stash pop` exits 128 on 2.56 and 1 before; from 2.38 a merge refused for something staged
  writes a no-op entry into HEAD's reflog; on 2.55.0 (and Git for Windows 2.56.0.windows.2) a
  pathspec under `--no-walk` no longer limits the rows (design 0.3). On 2.36 and 2.55 alike,
  `git check-ignore` refuses to run with `GIT_LITERAL_PATHSPECS=1` (design 15.8). Derive with
  real git, and read every CI job: CI is the only check on 2.43 and 2.56 (plan risk 26).
- **A test waits for what it asserts, and a wait for an outside program is not a hang.** `git
  mergetool` starts in about 3 s alone and 32 to 64 s under the default run's load; such a wait has
  its own limit (`Scenarios.MergeToolTimeout`, 120 s) and is marked
  `HangWatchdogAttribute.WaitingForSlowProgram` (D185).
- **A scripted double-click holds the input clock.** Avalonia counts two presses as one only within
  500 ms of the window's input clock; `ShellDriver.DoubleClick` stops it for its presses
  (`HeadlessInputClock`).
- **Long sequences are one action per test method** (D181), so that none nears the hang watchdog's
  3 minutes, and a scenario template is built once per process (`TempRepo` copies a blank git
  folder, `maintenance.auto=false`; `7e7d99d`).
- **A button theme needs a disabled style.** Every theme draws disabled in `VcTextDisabledBrush`
  now, `VcIconButton` included.
- **Tooltips take no input** (D182): a press under an open tooltip reaches the control beneath.

## Deviations from the plan

Phase 5's decisions are D153 to D185, in groups:

| What | Decision |
|---|---|
| The owner's answers at the phase's start: C12 built now; undo many steps back within the session; a remote's undo asks and pushes with a lease; the activity log a panel under the graph; search marks matches in the graph; pages in place of the graph; filters kept across restarts; phase 4's leftovers first; every stash's line dashed; two commits compared at once; Ctrl+Z and Ctrl+Y | D153 to D163 (D154 replaces "the last git action" in the plan and O2; D162 replaces D88's list for two commits) |
| The owner's answers after the design's reviews: "All files"; an Abort undone from the backups list; the automatic stash's checkout undone by switching back and popping | D164 to D166 |
| The undo history: per worktree and session, 100 entries, fixed sequences from a closed set of effects, read inside the action's operation; what is not recorded; moving a checked-out branch back without losing work or ignored files; every remote change asked about with explicit leases; backups kept while an entry needs them; the backups list restores with the undo's effects | D167 to D172 (D170 extends D155; D172 amends D62) |
| The activity log reads structured records, with clocks the harnesses fix | D173 |
| Search over the graph's rows; file history and blame as git computes them; the pages' rules; compare two-dot and three-dot; solo and hide by branch with the upstream; C12's rules; the reflog read through git | D174 to D180 (D178 amends D47 and D54 while a filter is on) |
| Two walk-through projects, check parts A, B, F, U and P | D181 (as D133, D151) |
| Tooltips take no input | D182 (adds to D105) |
| The report's decisions bind the builders; the lead's rulings on its questions | D183 |
| CI's Test step 40 minutes, the three platforms' jobs 50 | D184 (amends D152) |
| The wait for `git mergetool` to start does not count against the hang watchdog | D185 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| With Visual Studio's Git 2.55 first on `PATH`, 7 fetch and pull tests of `VisualCommit.App.Tests` fail locally (one of them also on the code before step 25), while CI's `ubuntu-latest`, on Git 2.55.0, passes them. Unexplained; which seven is not recorded | Medium for research with that git; no effect on the default run or CI. Until it is explained, a local run with that git says nothing about fetch or pull (plan risk 28) | Not recorded; the fetch and pull test classes (`RepositoryOperationsFetchTests`, `RepositoryOperationsPullTests`, `RepositoryOperationsPullRebaseTests`) are a place to start |
| The default run takes about 45 minutes on the laptop (3,233 tests, seven projects), CI's Windows Test step 25 of its 40 minutes, the real-window pass 25 minutes | Every full run, the gate and CI | plan.md risks 12 and 18; D134 is the owner's at the project's end |
| The undo recording reads the action's scope before and after inside its operation. After step 25's cuts the operation makes, in all, 6 git calls for a commit, 6 for a checkout of a branch, 16 for a pop (22 before), 14 for an apply, 18 for a hard reset with a local change; design 7.7 had expected the recording to add about two git calls before and after | Every recorded action is slower; part of why the runs grew (plan risk 25) | design 15.9 ("The undo history's reads, cut"); `GitRepository.Undo.cs`, `RepositoryOperations.Undo.cs` |
| Q1 measured 1,749 ms and 1,826 ms within the gate's two passes and 1,147 ms alone after a rest, against 937 ms in phase 4's pass, and failed at 2,613 ms on the hot laptop. No alternate run of phase 4's and phase 5's builds was made to tell the code from the machine (plan risk 10's method) | A hot laptop fails a gate by itself; within the pass only 0.17 to 0.25 s remain below the limit | plan.md risk 10 |
| A second outside change made between the refs' read and git log's can still show for one refresh | Low; that change's own refresh corrects it | `GitRepository.Refs.cs` (design 15.9) |
| The blame header's caption is cut at 1100×700 when the subject is long | Cosmetic, by design; the tooltip has the whole text | `Views/Pages/BlameView.axaml` |
| In the activity log a cancelled call's command starts about 23 px to the right of the `exit 0` rows' (`a2i`) | Cosmetic; noticed by the inspection | `Views/Shell/ActivityLogView.axaml` |
| A disabled menu item's tooltip partly covers the next item (`b9d`, `b9p`) | Cosmetic, as in earlier phases | the tooltips |
| The search box's caret blinks, so a capture may catch it off (F10, F11) | Varies pictures only | `Views/Graph/SearchBarView.axaml` |
| From Git 2.38 a merge refused for something staged adds a no-op entry to HEAD's reflog, which the reflog page shows as `git reflog` does | Low; the undo recording ignores HEAD's reflog | design 15.9 |
| The comment at the top of `tests/VisualCommit.RealWindowTests/VisualCommit.RealWindowTests.csproj` gives the pass's length as about 14 minutes with phases 0 to 4 and phase 5's estimate (9 to 12 minutes more); it took 24 min 57 s with phases 0 to 5 | A stale comment | fix with phase 6's first code commit |
| Six of phase 5's real-window captures differ from their scripted twins by more than 3 percent when compared in place (`a2c`, `a2f`, `a2g`, `a2j`, `a2k`, `b8d`; 4.05 at most); the pass's own measure, which allows a shift of one pixel, stays within its limit of 3 (2.34 at most) | Low; their difference pictures mark only text and 1-pixel lines | the report's "Found by the gate" |
| `scripts/time-scripted-check.sh` sends every phase from 5 up to phase 5's projects (`if [ "$phase" -ge 5 ]`) | Wrong for phase 6 until changed | change it when phase 6's walk-through project is added |
| Phase 4's issues outside D160's list remain (Start rebase 2 px narrower when enabled, a button drawn disabled behind a question's backdrop, the result's empty last line, the count after a Skip, the resolver's snapshots without a clean-up, `rebase.reapplyCherryPicks`, the real window's caret blink, the own-message copy's encoding), and earlier phases' (see phase 3's handoff) | As there | phase 4's handoff |

## Things that cost time

- **The spend limit stopped the first wave.** On 2026-10-13 four of the first wave's nine agents
  (the builders of WP-S and WP3's git half, WP1's fixes after its review, and the flaky Cancel
  test's fix) failed with "You've hit your monthly spend limit"; the work stood for about two days,
  until 2026-10-15. What each builder had committed on its `wip/p5-*` branch in its own worktree
  was kept. To resume, the lead saved each interrupted builder's trail (its actions and their
  results, read from the agent's transcript) to a file, and new agents continued each package in
  the same worktree from where its trail stopped. Before a large wave, check what is left of the
  limit; have builders commit at every working state.
- **Packages that touch the same lists conflict.** WP6 and WP7 both added items to the same menus,
  so WP7's merge had twelve conflicts, all in tests (expectation lists, menu heights and
  placements of phases 3 and 4), and three tests that did not conflict were wrong after it
  (`26e0aa5`, `83deb63`). Each package alone was red for earlier checks (WP6's `c546200` eight of
  phase 3's, WP7's `b494d96` seven, WP4's `e26f320` two support tests), fixed in later commits
  (`4705121`, `604c672`, `e666b65`): bisecting lands on such commits. Step 25's two fix branches
  conflicted in four files (`UndoState.cs`'s recorded files, `GitRepository.Undo.cs`,
  `ReflogViewModel.cs` and the report) and were merged by hand (`ceb663e`); the report's change
  entries conflicted whenever walk-through parts merged. Merge the packages that share a list
  early, or give one of them the list.
- **Git moved under CI mid-phase.** The Windows runner went to Git 2.56 on 2026-10-15 (phase 3's
  check 15b, run 38723225901, `281dd69`); 2.55.0's `--no-walk` regression broke a scenario test that
  took one git's behaviour for the rule (run 38738421993, `499b90c`); P27's reflog entry on 2.38 and
  later failed on all three newer gits (run 38853641791, `c8cd3dc`). macOS's `/var` link broke the
  activity log's worktree match for a repository opened through a link (run 38847538648, `da00111`).
- **Heat and Q1.** See State above: after a 43-minute default run the laptop failed Q1. Rest it,
  and measure Q1 alone when in doubt.
- **Disks.** With several built worktrees, C: fell under 10 GB free and D: under 20 GB; a built
  worktree takes about 5 GB. Check free space before a wave, remove a merged worktree at once
  (plan risk 27).
- **The hang watchdog and the merge tool.** `git mergetool` is a shell script that starts about 30
  git processes and forks its shell about a hundred times: 2.8 s alone, 32 to 64 s with four
  projects side by side, past the 60 s that phase 4's C16 waited. A2's second method took 146 s.
  The waits are 120 s now and outside the watchdog (D185). The full run on `3dcba5d` was stopped
  by the watchdog too: the reflog scenario's template took over 3 minutes under load, until the
  speed-ups (`7e7d99d`: 88 git calls instead of 145). CI's load also failed tests that read the
  app log while the app wrote to it and that held a large diff's time to a fixed limit (run
  38863530446; `91d7f72`).
- **The headless double-click's clock.** Under load the helpers' jobs between two presses took
  longer than 500 ms, P25's double-click came as two single clicks, and the test waited 60 s for a
  checkout that never started (`5c83a01`, `HeadlessInputClock`).
- **The refs read race** was found only by the gate's inspection (`f11f`); it likely also caused
  phase 1's flake in CI run 38203733545.
- **The undo recording's cost** was found late: a stash took 17 git calls in all with the refresh
  after it, 1.2 s alone and 3 s on CI; 10-second waits in the walk-throughs ran out under load
  (hence the 30 s default) and CI's Windows Test step reached its 24-minute limit (D184). Measure
  what a cross-cutting hook costs per operation before every operation gets it.
- **Shell quirks** (as phase 4): write Python helpers with the file tools; `sed -i` drops a CRLF
  file's carriage returns; Windows' console needs `PYTHONIOENCODING=utf-8` or a UTF-8 wrapper for
  non-ASCII output.

## For the next phase

Reuse: `IGraphAreaPage` for any view in place of the graph; `RepositoryMenus` (menus built as
data) as a source of a command palette's commands; `UndoScope` for every new
git action; `OperationLog` and `GitCallLog` for what a dashboard runs; `GraphFilterStore`'s pattern
(per repository, bounded by recent use) for per-repository settings; `TrimmedTextBlock`,
`LayoutAudit`, the two clocks and `ManualTimeProvider` in tests; `TempRepo.ExactStateAsync`.

Do first:

1. Read phase 6's section of plan.md and its risks 10, 12, 18 and 25 to 28, the Known issues
   above and phase 4's, and check the latest CI runs on `master` (the merge of phase 5 starts
   one).
2. Check that no worktree, `wip/*` or `worktree-*` branch or fsmonitor daemon is left
   (`git worktree list`, `git branch --list "wip/*" "worktree-*"`; phase 4's handoff lists the
   daemons), and that C: and D: have room for the worktrees planned.
3. With the first code commit: the real-window csproj's stale comment and the timing script for
   phase 6.
4. Write phase 6's checks into `docs/test-reports/phase-6.md` before the UI, deriving every git
   state with real git 2.36, and decide which steps the real window repeats. Estimate what they
   add to the default run (45 minutes now) and to the real-window pass (25) before writing them;
   share app starts where scenarios allow.
5. Give phase 6's walk-through a project of its own, as phases 4 and 5 did (D151, D181; design
   15.3 here): its csproj, `AssemblyInfo.cs` and support copied from phase 5's, in
   `VisualCommit.slnx` (CI picks up every test project of the solution); a `Phase6Expectations*.cs`
   link in the real-window csproj; the timing script; CLAUDE.md's Commands. Two projects can share
   one picture folder (`VisualRun` empties it once per phase and run).

Open points for phase 6 (record each as a decision; ask the owner those that are a matter of
taste, before the design):

- **The dashboard's bulk fetch and pull** run through each repository's queue (plan.md). A pull is
  recorded for undo in its worktree's history (D154 is per worktree and session) and its records go
  to the activity log, which shows the active tab's worktree. Decide whether a dashboard pull of a
  repository with no tab is recorded, and where its log shows.
- **The command palette and keyboard shortcuts** must keep what phase 5 bound in
  `MainWindow.axaml.cs`: the platform's undo and redo keys are the window's only when no dialog,
  menu, drag, text box or the conflict resolver takes them (D163); Ctrl+F (Cmd+F) is Search
  wherever the focus is, unless a dialog or a menu is open; Esc follows `EscapeOrder`.
- **The settings screen**: `--no-overwrite-ignore` for every checkout (design 14.2), `merge.tool`
  (D149), the pull mode through git's own keys (D78).
- **Fetch and pull on Visual Studio's Git 2.55**: the seven unexplained local failures (Known
  issues) are to be understood before phase 6 changes fetch and pull for the dashboard (plan risk
  28).
- **The theme sweep**: phase 5's new screens (the activity log, the pages, the search bar, the
  comparison, C12's message) were pictured in the dark theme, apart from B5's side-by-side history
  and F10's search at 1920×1080 in the light one.

Risks to watch:

- The run time (plan risks 12 and 18): 45 minutes for the default run on the laptop, 25 for the
  real-window pass, 25 of 40 for CI's Windows Test step. D152's rule (past 20 minutes of the old
  24, the lead raises the limit and records it) led to D184; do the same before the step nears 40.
- Q1 on a warm laptop (plan risk 10).
- Git keeps changing between versions, and CI's gits are 2.43 to 2.56 (plan risk 26);
  `ubuntu-latest` moves to Ubuntu 26 from 2026-10-19 (plan risk 11).
- The work-package method (contracts and stubs first, waves of builders in worktrees, the report's
  checks between the waves, a review of six lenses with a skeptic per finding, the gate's
  inspectors) worked, at the cost of about 5 GB of disk per built worktree (plan risk 27), merge
  conflicts where packages share lists, and a spend limit that a large wave can reach.

## Waiting on the owner

- Acceptance of phase 5: the gate passed on `1b2e734`; "Phase 5 accepted" merges
  `phase/5-history` into `master`.
- When you have a Mac at hand: the thirteen-step checklist at the end of the
  [phase 5 test report](../test-reports/phase-5.md), and those of phases 0 to 4, still open. The
  owner will do them at the end of the project.
- When the project is over: whether a CI run of 30 minutes or more is reasonable (D134, D152,
  D184). CI's Windows Test step now takes about 25 minutes (the job 27 min 50 s) and the default
  run on the laptop about 45.
