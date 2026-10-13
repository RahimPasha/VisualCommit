# Phase 4 handoff — Rebase and conflict resolution

Written at the end of phase 4 for the session that runs phase 5. That session has not seen any
of this phase's conversation. How the app is built is in [architecture.md](../architecture.md),
why in [decisions.md](../decisions.md) (D139 to D152 are this phase's); the phase's design, kept
as history, is [phase-4-design.md](../phase-4-design.md), whose sections 14.1 to 14.5 say what the
build and the review settled. This file says what was proven, what to copy, and what to watch out
for.

## Result

Everything in the plan's "Delivers" list was built. The check names are those of
[test-reports/phase-4.md](../test-reports/phase-4.md) (18 checks: A1 to A5, B6 to B9, C10 to
C17, D18); its section "What proves each Delivers item" is the longer form of this table.

| Delivered | Proven by |
|---|---|
| Rebase onto a commit or a branch from the commit, branch and drop menus, also by drag; pull with rebase for one pull | A1 to A5; `RebaseOperationTests` (Git), `RepositoryOperationsRebaseTests`, `RepositoryOperationsPullRebaseTests`, `PullRebaseParityTests`, `RepositoryMenusTests` |
| Interactive rebase (reorder, squash, fixup, reword, drop) in a list in the graph area; squash of selected commits | B6 to B9, D18; `RebaseTodoTests` (of Core's pure code, in `VisualCommit.Git.Tests`), `InteractiveRebaseTests`, `InteractiveRebaseListParityTests` (Git), `InteractiveRebaseViewModelTests`, `InteractiveRebaseViewTests`, `MessageDialogViewModelTests`, `GraphAreaHooksTests` |
| The banner's Continue, Skip and Abort for merge, rebase, cherry-pick and revert, one commit or a list, also an operation started in a terminal; the stage panel's Continue | C10 to C13, C17, and phase 3's checks 9, 9b, 11b, 12, 25 as changed; `ContinueSkipTests`, `InProgressTests` (Git), `ConflictBannerTests`, `WorkingChangesInProgressTests`, `RepositoryOperationsSequenceTests` |
| The conflict resolver: ours and theirs above the editable result, per block, a whole side, mark resolved, the next file, the external merge tool, cards for conflicts without text | C10 to C16, D18; `ConflictTextTests` (of Core's pure code, in `VisualCommit.Git.Tests`), `ConflictOperationTests`, `MergeToolTests` (Git), `ConflictResolverViewModelTests`, `ConflictResolverViewTests`, `ConflictResolverRealGitTests`, `ConflictPanesTests`, `ConflictResultEditorTests`, `RepositoryOperationsConflictsTests` |
| Several picks and reverts with git's list (D136) | C12, C13; `HistoryOperationTests`, `RepositoryOperationsHistoryTests`, `RepositoryOperationsSequenceTests` |
| Pick heads under the reftable ref format (D128, D146) | `PseudoRefTests` (a real reftable repository on CI's git 2.55; on 2.36, which has no reftable, it asserts that git refuses the option), `PseudoRefProbeTests` |
| The app's own sequence editor | B7, B8; `InteractiveRebaseTests` |
| Backups: the rebase's and Skip's; their automatic clean-up (D139) | A2 to A4, C13; `BackupCleanupTests` (Git), `RepositoryOperationsBackupCleanupTests`, `GraphAreaHooksTests` (at opening) |
| Phase 1's check 13 made deterministic (D150) | phase 1's checks 13 and 14; `AuthRemoteServerTests` |

Every check passed in both passes on the gate's commit. The gate's earlier runs found two faults,
fixed before the final run (the report's "Found by the gate"): a phase 2 check's wait too short
under load once a discard ran the backups' clean-up (`a7e9db3`), and the real-window driver's
Shift+arrow, which Windows turned into a number-pad arrow (`6badab7`).

Nothing was moved to a later phase. Left out on purpose and put in plan.md's Backlog: an
interactive rebase that includes the root commit, syntax colours in the resolver's editable
result, a splitter between the resolver's halves, and Continue and Skip for `git am` (which the
banner leaves to a terminal). The cosmetic things the inspection noticed are under Known issues.

## State of the repo

The visual test gate passed on `6badab7`, the gate's commit: `dotnet test` (2,411 tests, all
passed, 21 min 52 s) and the real-window pass (43 tests, all passed, 14 min 4 s) on the same
commit, every picture of both inspected. The final run's pictures are 510 scripted (phase 0: 14,
phase 1: 43, phase 2: 66, phase 3: 265, phase 4: 122) and 107 real-window captures with 103
difference pictures (the largest difference 1.49 percent, the limit 3). The inspectors opened
317: phase 4's 122, the 88 of phases 1 and 3 that differ from what phase 3's gate inspected, and
all 107 captures; the other 300 scripted pictures are byte-identical to phase 3's gate's. None
contradicts its expected result; 21 vary by nature (the report's Run section lists them). Q1
measured 937 ms to the first rows (limit 2000). `phase-4-pictures.sha256` lists the 510 scripted
and 210 real-window files.

CI run [38320711773](https://github.com/RahimPasha/VisualCommit/actions/runs/38320711773) built
and tested that commit with the same 2,411 tests, and all five jobs succeeded: windows-latest
(test step 13 min 9 s, job 15 min), macos-latest (test step 13 min 57 s), ubuntu-latest (1 min
58 s) and the two bare ubuntu containers (2 min 23 s and 2 min 31 s). Four of CI's macOS and
Linux pictures were opened (the report's "Other platforms").

The commits after the gate's commit change only docs. The phase is "Awaiting acceptance" on
`phase/4-rebase-conflicts`; nothing is merged into `master` yet. Only the real window at 1920×1080
(D18) could not run, as the plan allows on this screen.

Before the gate, an independent review of the code (a workflow of five reviewers, each finding
verified by a second agent) confirmed 31 findings, 5 major and 26 minor, none a blocker; they were
fixed with tests in `d1abbc4`, `68a5275` and `d0b4372`, and design 14.5 records every behaviour
they changed.

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300 on an Intel Core i7-8650U laptop; screen 3000×2000 at 200% scaling (1500×952 logical work area). The whole phase was built on this machine; no cloud session worked on it |
| .NET SDK | 10.0.303 (`global.json` stays at 10.0.100 with `latestFeature`) |
| Git | 2.36.0.windows.1 locally, with `core.fsmonitor=true` in the owner's global configuration; 2.55.0 on the CI runners |
| Packages | Unchanged since phase 2: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0 |
| New project | `tests/VisualCommit.VisualTests.Phase4`, phase 4's scripted walk-through (D151): a fifth test process beside the others |
| Screenshots | Scripted: scaling 1. Real window: 200% and a work area of 1500×952 logical pixels, as `run.txt` records them |

## Build, run and test

The table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands). New in
this phase: phase 4's walk-through is a project of its own, and `scripts/time-scripted-check.sh`
takes phase 4 when no phase is given.

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
dotnet test --project tests/VisualCommit.VisualTests.Phase4
dotnet test --project tests/VisualCommit.VisualTests.Phase4 --filter-class "*Phase4ChecksB"
dotnet test --project tests/VisualCommit.VisualTests.Phase4 --filter-method "*Check_C14*"
dotnet test --project tests/VisualCommit.App.Tests --filter-class "*ConflictResolverViewModelTests"
bash scripts/time-scripted-check.sh "*Check_B7*"
dotnet test --project tests/VisualCommit.RealWindowTests -c Release
```

- The default run took 21 min 52 s at the gate, rested and alone (side by side:
  `VisualCommit.VisualTests` 7 min 59 s, `VisualCommit.VisualTests.Phase4` 16 min 33 s,
  `VisualCommit.Git.Tests` 18 min 13 s, `VisualCommit.App.Tests` 21 min 30 s,
  `VisualCommit.VisualTests.Phase3` 21 min 52 s). With the phase's agents busy beside it, 25 to
  27 minutes. It is bound by the processors.
- While working, run one class or method. Several `--filter-method` options in one run combine
  with OR; `--filter-class` and `--filter-method` together select their intersection (often
  nothing). A partial run that takes screenshots empties that phase's picture folder first, so a
  gate's pictures are gone after any partial run of the same phase: run the gate last.
- Phase 4's checks' classes are `Phase4ChecksA` to `Phase4ChecksD` (one per part; part C is one
  class written in several files); a test is named `Check_<id>_<what>` (`Check_C14_editing_freely`).

## Running the visual gate

As in phase 3's handoff (step 0 there: worktrees removed, the git fsmonitor daemons stopped), with
a fifth project and a longer real-window pass. This phase found 27 daemons still running before its
gate, most for worktrees already removed; in PowerShell,
`Get-CimInstance Win32_Process -Filter "Name='git.exe'" | Where-Object { $_.CommandLine -match 'fsmonitor--daemon run' }`
lists them and `| ForEach-Object { Stop-Process -Id $_.ProcessId -Force }` stops them (git starts
them again when needed; they only cache file changes).

1. Let the laptop rest a few minutes, then `dotnet test` at the repo root: about 22 minutes. It
   writes `artifacts/visual/phase-{0,1,2,3,4}/scripted/`, emptying each first.
2. Rest three minutes, **tell the owner**, and run
   `dotnet test --project tests/VisualCommit.RealWindowTests -c Release` (never `--no-build`):
   14 minutes for phases 0 to 4 (43 tests). It compares its captures with step 1's pictures, so
   nothing may run a partial walk-through in between. Phase 4's part is
   `Phase4RealWindowPass.cs` and `.A.cs` to `.C2.cs`, with `RealApp.Phase4*.cs`; a capture that
   times out saves what the window showed under `real-window/failures/` (C14 only so far).
3. Save `sha256sum` of the pictures before anything else runs (CLAUDE.md gives the command that
   writes `phase-N-pictures.sha256`), then open every picture of both passes that changed:
   `tr -d '\r' < docs/test-reports/phase-4-pictures.sha256 | sha256sum -c --quiet` names those of a
   later run that differ from phase 4's gate (a new picture is not named: open those too).
   Real-window pictures are never byte-identical and must all be opened. Phase 4's gate inspected
   317 pictures with 12 inspector agents started side by side by one Claude Code workflow (a
   script that runs sub-agents), each batch given its pictures and the report's sections to
   compare with; the report's Run section says how they were split (the new phase's pictures by
   part, the changed ones of earlier phases, the real-window captures with their scripted twins).

Running only part of the real-window pass works with `--filter-method`: at the gate,
`dotnet test --project tests/VisualCommit.RealWindowTests -c Release --filter-method "*Check_C14_typing_into_the_result"`
ran one test (about 40 seconds of the mouse). It empties that phase's `real-window/` folder, so the
whole pass runs again afterwards; tell the owner first, as for the whole pass.

## What changed in the code

| To do this | Start here |
|---|---|
| Run or change a rebase | `GitRepository.Rebase.cs` (`RebaseAsync`, `InteractiveRebaseAsync`, `ReadRebaseListAsync`, `ReadRebaseAutoStashAsync`), `GitRepository.Branches.cs` (`ClassifyRebaseAsync`), `Core/Git/RebaseTodo.cs` (the todo and message files the app's sequence editor delivers), `RepositoryOperations.Rebase.cs` (the operations, Continue and Skip), `OperationTexts.Rebase.cs` |
| Continue or Skip a stop | `GitRepository.Sequence.cs` (`ContinueAsync`, `SkipAsync` per state, design 5.4), `InProgressState` and `GitRepository.InProgress.cs` (stop kinds, a list's `Done`/`Total`), `ConflictBannerViewModel.ContinueFor` and `SkipFor` (what each state offers, 5.1) |
| Pick or revert several commits | `GitRepository.History.cs` (one call with the list, D136), `RepositoryOperations.Branches.cs` (`RunListAsync`) |
| Read or resolve a conflict | `GitRepository.Conflicts.cs`, `Core/Conflicts/ConflictText.cs` (parsing markers), `RepositoryOperations.Conflicts.cs` (`IConflictActions`: Mark resolved, sides, the merge tool) |
| The resolver's UI | `ViewModels/Conflicts/ConflictResolverViewModel.cs` (blocks, bars, undo, Mark resolved, cards, refreshes), `Views/Conflicts/ConflictResolverView.axaml(.cs)`, `Controls/Conflicts/ConflictPanes.cs` and `ConflictResultEditor.cs` (AvaloniaEdit), `ViewModels/Menus/ConflictMenus.cs` |
| The interactive list's UI | `ViewModels/Rebase/InteractiveRebaseViewModel.cs` (rows, joins, the plan), `Views/Rebase/InteractiveRebaseView.axaml(.cs)`, `IInteractiveRebaseHost` (implemented by `RepositoryViewModel`) |
| What the graph area shows | `ViewModels/RepositoryViewModel.GraphArea.cs`: the list, the resolver, `TryLeaveGraphAreaAsync`, `ShowsGraph`, focus after closing, `merge.tool`, the clean-up at opening; `RepoTabViewModel.ShowsRightPanel` and `GraphColumnSpan`; `Views/Shell/CommitGraphView.axaml` |
| Backups and their clean-up | `GitRepository.Backups.cs` (`CleanUpBackupsAsync`, `FindBackupsToRemoveAsync`, D139); `RepositoryOperations.CleanUpBackupsAsync` after every operation that saved one; `RepositoryViewModel.BackupsToKeep` |
| An operation of several steps or a warning | `OperationContext.SetRunningText`, `OperationReport.Warning` (Core/Operations) |
| A visual check of phase 4 | `tests/VisualCommit.VisualTests.Phase4/Phase4Checks.<part>*.cs` with `Phase4Expectations.<part>.cs`; the drivers `ShellDriver.Phase4.cs`, `.Phase4.Rebase.cs`, `.Phase4.Conflicts.cs`; scenarios in `tests/VisualCommit.Testing/Scenarios.Phase4.cs` (`RebaseAsync`, `InteractiveAsync`, `ConflictsAsync`, `ConflictsStoppedAsync`) |

## Patterns to follow

| For | Example |
|---|---|
| A git sequence and its outcome | `ClassifyRebaseAsync`: read the files git left (`rebase-merge/`, `sequencer/`, pick heads), never git's words, to tell a stop from a failure |
| Delivering a todo to git | `RebaseTodo` + the shell-function sequence editor in `GitRepository.Rebase.cs`: prepared files copied in; a message written by a guarded `exec git commit --amend` (design 2.5) |
| A UI component tested headless with a fake host | `InteractiveRebaseViewTests` (the view over the graph area's size, a recording `IInteractiveRebaseHost`), `ConflictResolverViewTests` (a recording `IConflictActions`, `ManualTime` for the double-click guard) |
| The whole app on a real repository | `GraphAreaHooksTests`: `ShellDriver.StartWithTabs` on a scenario, the view model's members, the driver's waits |
| Git's own files in a check | `Phase4Checks.C.cs`'s `AssertRebaseFiles`: compared word for word on git 2.36 (`IsReportGitAsync`), with 2.55's additions taken out elsewhere |
| A wait for the stage panel | `ShellDriver.StagePanelSettled` (the panel's message is filled after a read of its own) |

Rules worth keeping (phase 3's still hold: two gits, dates, paths, waits, clocks, isolation):

- **Git 2.55 writes its own files differently**: `# Conflicts:` in `rebase-merge/message`, `pick
  <sha> # subject` in the todo files, `rebase (finish)` in the reflog, other autostash words.
  Classify by files and states, never by git's sentences; compare git's files word for word on
  2.36 only.
- **A background operation is visible to tests.** The queue's background slot is shared: anything
  queued at opening holds operations, dedupes and closing waits. Read first, queue only when work
  is due (`FindBackupsToRemoveAsync`).
- **Key chords in the real window**: arrows, Home, End, Page Up/Down, Insert and Delete go as
  extended scan codes (`RealApp.PressKeys`); a virtual key without the extended flag is the
  number pad's, and Shift is cancelled with Num Lock on.
- **Tests that hold time**: a guard that counts milliseconds takes a `TimeProvider`
  (`ConflictResolverViewModel`), so its test does not race the machine.

## Deviations from the plan

| What | Decision |
|---|---|
| The backups' clean-up: 30 days or beyond 50, nothing on screen, at opening and after each backup | D139 (owner's answers) |
| The resolver across the graph and right-panel columns, ours and theirs above the result | D140, D148 (owner's answer) |
| The interactive list in the graph area, newest on top, an action button per row | D141 (owner's answer) |
| Rebase: the target's SHA, a backup, no question; check out first for a drop's source | D142 |
| The app's own sequence editor and the guarded amend | D143 |
| Continue and Skip per state; Skip asks and backs up | D144 (amends D83, D95, D122) |
| Several picks and reverts with git's list | D145, D136 |
| Pick heads through git only under reftable | D146 |
| Pull with rebase for one pull | D147 |
| The external merge tool as a cancellable local operation | D149 (amends D81) |
| Phase 1's check 13 against a held server | D150 (amends D97) |
| A fifth test project; the editor variables; the result's caret held still in tests | D151 |
| CI's Test step 24 minutes, OS jobs 32 | D152 (amends D134) |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| In the details panel a 40-character SHA wraps one character onto a second line when the "Commit date" row widens the label column (`a3d`, `b8c`, `c11j`) | Cosmetic; every rebased commit shows it | `Views/Shell/RightPanelView.axaml` (phase 1's details panel) |
| The resolver's enabled and disabled arrow buttons (files, conflicts) look the same | Cosmetic; the tooltip and the click tell | `Views/Conflicts/ConflictResolverView.axaml` |
| Start rebase is 2 px narrower when enabled than disabled, which moves Cancel by 2 px | Cosmetic | `Views/Rebase/InteractiveRebaseView.axaml` |
| A button whose question is open is drawn disabled behind the backdrop (its command waits) | Cosmetic; consistent everywhere | the async commands |
| The result shows an empty numbered line after the file's last line feed | Cosmetic; AvaloniaEdit's model | `ConflictResultEditor` (design 14.4) |
| After a Skip in a list, `{K} of {N}` counts N without the skipped commit: git keeps no record of it | Low | `GitRepository.InProgress.cs` (design 14.5) |
| The resolver's own snapshots (`Delete it`, a binary side) run no clean-up; the next backup or opening makes up for it | Low | design 14.4 |
| `rebase.reapplyCherryPicks` (newer gits) is not followed by the list | Low; the list matches git's default | `GitRepository.Rebase.cs` (design 14.5) |
| A text box's caret blinks in the real window, so a capture may catch it off (`b7f`) | Varies pictures only | D151 holds still only the result's caret |
| A commit message in an encoding other than UTF-8 is not kept byte for byte by the interactive rebase's own-message copy | Low | design 14.2 |
| The status bar's Cancel button's tooltip says "Stop git" while the merge tool runs; design 11.9 gives "Stop git and the merge tool" | Cosmetic; found by the docs' check after the gate, left so as not to change the gate's commit | `Views/Shell/StatusBarView.axaml` (line 73); fix with phase 5's first code commit |
| `IGitRepository.ReadRebaseListAsync`'s doc comment still names `--cherry-pick`; the code reads `--cherry-mark` and keeps commits that start empty (design 14.5) | A stale comment | `src/VisualCommit.Core/Git/IGitRepository.cs`; fix with phase 5's first code commit |
| The comment at the top of `tests/VisualCommit.RealWindowTests/VisualCommit.RealWindowTests.csproj` says the pass takes about 11 minutes with phases 0 to 3; it is about 14 with phases 0 to 4 | A stale comment | fix with phase 5's first code commit |
| `scripts/time-scripted-check.sh` sends every phase from 4 up to the Phase 4 project (`if [ "$phase" -ge 4 ]`) | Wrong for phase 5 until changed | change it when phase 5's walk-through project is added |
| Earlier phases' issues that remain (the tooltips that cover the next item, the first context menu's latency, the error output's URL wrapping, Q1 on a warm laptop): see phase 3's handoff | As there | |

## Things that cost time

- **Parallel packages on one laptop.** Six build packages, four check writers, three review-fix
  agents and one real-window writer ran in worktrees, three or four at a time. Each merge was
  clean, but the lead's hooks (`RepositoryViewModel.GraphArea.cs`) had to wait for the packages
  they join; stub the hooks first (`fe60c44`) so that packages build against each other.
- **The clean-up at opening broke CI for three runs.** It held the queue in every test that
  opened a repository; the local walk-throughs passed, the full App tests did not run until
  later. Run the whole App test project after any change to the queue or to what opening does.
- **Git 2.55 on CI** found the four differences above, one by one, over several runs: read every
  job of every run, and look for the same kind of difference in the other checks before pushing.
- **Races only CI showed**: the stage panel's message filled after the panel shows, a refresh
  between a terminal's switch and its rebase, the double-click guard's clock, a phase 2 wait
  under load. Each was a test that read too early; fix the wait, not the timeout.
- **The gate ran three times** (a phase 2 wait, then the real window's Shift+Down). Collect all
  fixes before rerunning; a partial run of a phase empties its picture folder.
- **Shell quirks**: in the Bash tool a heredoc mangles backslashes and apostrophes; write Python
  helpers with the file tools. `sed -i` drops a CRLF file's carriage returns.

## For the next phase

Reuse: `ConflictText` and the resolver for any merge-like view; `RebaseTodo` if undo replays
commits; the backups' clean-up rules (`FindBackupsToRemoveAsync`) when undo lists what it can
offer; the graph area's hooks (`TryLeaveGraphAreaAsync`, `GraphColumnSpan`) for a new view in
place of the graph (compare, file history); `OperationReport.Warning` and `SetRunningText` for
operations of several steps.

Do first:

1. Read the Known issues above and phase 3's, and check the latest CI runs on `master`.
2. Check that no worktree, `wip/*` or `worktree-*` branch or fsmonitor daemon is left
   (`git worktree list`, `git branch --list "wip/*" "worktree-*"`).
3. Phase 5's undo reads the backups (D67, D83, D127, and phase 4's `rebase-<time>/<b>` and
   `skip-<time>` with its `-changes`), which phase 4 now cleans up (D139): undo offers only
   what is left, and its activity log is to show the app log's `Removed backup` lines (plan.md,
   phase 5).
4. Write phase 5's checks into `docs/test-reports/phase-5.md` before the UI, deriving every git
   state with real git 2.36, and decide which steps the real window repeats.
5. Give phase 5's walk-through a project of its own, as phase 4 did (D133, D151; WP-S in design
   13.3): `tests/VisualCommit.VisualTests.Phase5` with its csproj, `AssemblyInfo.cs` and
   `Phase5Checks.Support.cs` copied from phase 4's, added to `VisualCommit.slnx` (CI picks up
   every test project of the solution with no change); a `Phase5Expectations*.cs` link in
   `tests/VisualCommit.RealWindowTests/VisualCommit.RealWindowTests.csproj` beside phase 4's;
   `scripts/time-scripted-check.sh` taught phase 5; CLAUDE.md's Commands rows for it.

Open points for phase 5 (record each as a decision; ask the owner those that are a matter of
taste, before its design, as phases 3 and 4 did; phase 4 wrote its design into
`docs/phase-4-design.md` and phase 5 may do the same):

- **C12, protecting uncommitted changes** (D138, plan.md's Backlog): the owner proposed it for
  phase 5 and decides when phase 5 is planned. Ask first.
- **Undo's scope**: O2 and the plan's Delivers say "undo and redo of the last git action", while
  "Done when" says "each destructive action can be undone". Settle which, and what redo means.
- **Undo of what reached a remote**: a force push, a deleted remote branch or tag. The backups
  hold the old commits (`force-push`, `remote-branch`, `remote-tag`), but undoing means pushing
  again; decide whether and how it asks.
- **The activity log's sources**: it reads `OperationLog` (the last 500 records, in memory) and
  `IGitCallLog` (the last 2,000 calls); the clean-up's `Removed backup` lines are only in the app's
  log file, and a clean-up inside another operation has no record of its own; `OperationRecord`
  does not carry `OperationReport.Warning` yet. Decide where the log reads them from.
- **Where the activity log and search show**: the toolbar's Search and the status bar's
  "Activity log" are placeholders (D53, D90).
- **The cosmetic issues above** (the details panel's SHA, the resolver's disabled arrows) if the
  owner wants them in phase 5.

Risks to watch:

- The run time (plan risks 12 and 18): 22 minutes for the default run on the laptop, 14 for the
  real-window pass, 13 to 14 for CI's Windows and macOS test steps against a 24-minute limit.
- Q1 on a warm laptop (plan risk 10): 937 ms at this gate, rested.
- Git's texts and files keep changing between versions; derive with real git and run the
  Git-layer tests on CI's git.

## Waiting on the owner

- Phase 4 is "Awaiting acceptance": say "Phase 4 accepted" to merge it into `master`.
- When you have a Mac at hand: the eight-step checklist at the end of the
  [phase 4 test report](../test-reports/phase-4.md), and the checklists of phases 0 to 3, still
  open.
- The cosmetic issues above (the details panel's SHA, the resolver's disabled arrows) can go into
  phase 5's first commits if you agree; none affects a result.
- When the project is over: whether a CI run of 30 minutes or more is reasonable (D134, D152).
