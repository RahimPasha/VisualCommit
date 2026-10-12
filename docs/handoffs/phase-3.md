# Phase 3 handoff — Branch, remote, stash and tag operations

Written at the end of phase 3 for the session that runs phase 4. That session has not seen any
of this phase's conversation. How the app is built is in [architecture.md](../architecture.md),
why in [decisions.md](../decisions.md) (D77 to D133 are this phase's); this file says what was
proven, what to copy, and what to watch out for.

## Result

Everything in the plan's "Delivers" list was built. The check numbers are those of
[test-reports/phase-3.md](../test-reports/phase-3.md) (41 checks: 1 to 32 and 5b, 9b, 11b, 13b,
14b, 15b, 17b, 18b, 21b); its section "What proves each Delivers item and each interaction row"
is the longer form of this table.

| Delivered | Proven by |
|---|---|
| Checkout, create, rename, delete; merge and fast-forward; reset; cherry-pick; revert | Visual checks 6, 8, 9, 10, 11, 11b, 12, 13, 13b, 30, 32; `BranchOperationTests`, `HistoryOperationTests`, `RefNameRulesTests`, `DashBranchNameTests`, `RepositoryOperationsCheckoutTests`, `RepositoryOperationsBranchesTests`, `RepositoryOperationsMergeTests`, `RepositoryOperationsHistoryTests` |
| Fetch, pull, push, safe force-push, set upstream, manage remotes, auto-fetch | Visual checks 17, 17b, 18, 18b, 19, 20, 22, 28, 29, 30, 31 (auto-fetch by tests only: the harnesses turn it off); `RemoteOperationTests`, the nine `Pull*ParityTests` classes, `AuthenticatedRemoteTests`, `AutoFetchSchedulerTests`, `RepositoryOperationsFetchTests`, `RepositoryOperationsPullTests`, `RepositoryOperationsPushTests`, `RepositoryOperationsRemotesTests` |
| Stash save, apply, pop, drop; tag create, delete, push | Visual checks 15, 15b, 16, 29; `StashTagTests`, `RepositoryOperationsStashTagsTests` |
| Context menus on every object, double-click actions, drag a branch onto a branch, multi-select commits | Visual checks 1 to 7, 5b, 11, 11b; `RepositoryMenusTests` (and `.Window.cs`), `RepositoryMenuContextTests`, `ContextMenuAdapterTests`, `BranchDragTests`, `GraphSelectionTests` |
| Operation queue with progress, cancel and git's own output; it takes over phase 2's writes | Visual checks 17, 17b, 18, 19, 31; `OperationQueueTests` (Core, without git), `OperationQueueUiTests`, `RepositoryOperationsQueueTests`, `ProgressParserTests`, `FailureClassifierTests`, `GitOutputCleanerTests`, `StatusBarTests`, `OperationStatusViewModelTests`; phase 2's `WritingTests` and `WorkingChangesTests` still pass |
| Stage, unstage and discard in a file's right-click menu (from phase 2, D70) | Visual check 4; `RepositoryMenusTests`, `RepositoryOperationsFilesTests` |
| The toolbar's Fetch, Pull, Push, Branch, Stash and Pop, and the status bar's operation status (from phase 1, D53) | Visual checks 17, 18, 23; `ToolbarTests`, `StatusBarTests` |
| Credential and ssh passphrase prompts as in-app dialogs | Visual checks 21, 21b; `PromptClassifierTests`, `AskPassTests`, `AskPassRunnerTests`, `AuthenticatedRemoteTests`, `PromptDialogTests`, `PromptDialogsTests`, `AskPassHelperTests` (in the phase 3 walk-through project) |
| ssh's own prompts answered in the app or turned off (from phase 1) | Visual check 21b; `AskPassTests` (deny mode for auto-fetch), `PromptDialogTests` (recorded host-key and passphrase texts through the real helper), `FakeSshTests`. A real ssh server's host-key question and Git Credential Manager's own window: by hand only (D97) |
| The left panel's selection follows the graph's (from phase 1) | Visual check 24; `LeftPanelViewModelTests` (D98) |
| Backup refs before destructive operations | Visual checks 9, 10, 13, 15, 16, 20, 22, 30, 32 (asserted with `TempRepo.BackupRefsAsync`); `BackupRefTests` (and `.Collisions.cs`) |
| Basic conflict state: a banner with Abort, conflicted files flagged | Visual checks 9, 9b, 11b, 12, 14, 15b, 25; `InProgressTests`, `ConflictBannerTests`, `WorkingChangesInProgressTests`, `RerereTests` |

Every check passed in both passes on the gate's commit: the scripted pass ran all 41, and the
real-window pass repeated the steps the report marks RW (check 27, at 1920×1080, is scripted
only, because that size does not fit this screen). The gate's earlier runs and its inspection found six faults, each fixed before
the final run and listed in the report's "Found by the gate": three in the product (the error
dialog's output box, the radio button's focus frame and its unchecked ring: `2ddec83`,
`54722ed`), one in when the scripted pictures of a selected commit were taken (`2ddec83`), and
two timing faults in phase 1's checks 12 and 18 (`674e79d`, `594f9fb`).

Not delivered, by decision, and where each went:

- The interaction table's actions of later phases are left out of the menus, not shown disabled
  (D77, D92): rebase onto and rebase by drag, squash (phase 4), compare, file history and blame
  (phase 5), create pull request (phase 7). Folder rows, "Pull requests" and repository tabs
  have no menu.
- Continue and Skip for a stopped merge, pick, revert, rebase or `git am`, and the resolver:
  phase 4 (D89). For a rebase or `git am`, and a pick sequence started in a terminal, the banner
  says to continue in a terminal.
- Pull with rebase is partly delivered here: pull follows `pull.rebase` and
  `branch.<name>.rebase` (D78, D84), and a pull that rebases and stops shows the banner with
  Abort. Phase 4 keeps rebase onto, continue and skip (also after a pull's rebase).
- The gate moved nothing to a later phase. The cosmetic things its inspection noticed and left
  are under Known issues; the questions it raised are under Waiting on the owner.

## State of the repo

The visual test gate passed on `594f9fb`, the gate's commit: `dotnet test` (1,756 tests, all
passed, 19 min 15 s) and the real-window pass (32 tests, all passed, 11 min 11 s) on the same
commit, every picture of both inspected. The real-window count is the 14 tests of phases 0 to
2, phase 3's 15 and the 3 `Phase3ExpectationsTests`, which come along with the
`Phase3Expectations*.cs` files the project links. The final run's pictures are 388 scripted
(phase 0: 14, phase 1: 43, phase 2: 66, phase 3: 265) and 77 real-window captures, each with
its difference picture (the largest difference 1.49 percent, the limit 3). Over the gate's
rounds the inspectors opened first the 361 scripted pictures that phase 3 added or changed and
all 77 real-window ones, then the 35 and 16 scripted and 11 real-window pictures that later
fixes changed, and finally the 77 real-window captures of the final run again; the other 27
scripted pictures are byte-identical to those the gates of phases 1 and 2 inspected. Q1 measured 1189 ms to
the first rows (limit 2000). The report is [test-reports/phase-3.md](../test-reports/phase-3.md).

CI run [38206493320](https://github.com/RahimPasha/VisualCommit/actions/runs/38206493320) built
and tested that commit with the same 1,756 tests, and all five jobs succeeded: windows-latest in
10 min 56 s, macos-latest 8 min 35 s, ubuntu-latest 3 min 11 s, and the bare ubuntu containers
4 min 1 s (SDK from Ubuntu's packages) and 3 min 25 s (SDK from Microsoft's installer). The
scripted pictures CI took on macOS and Linux were compared with the Windows ones (97 and 36 of
each platform's 388 opened); what they showed is in the report's "Other platforms".

The commits after the gate's commit change only docs. The phase is "Awaiting acceptance" on
`phase/3-operations`; nothing is merged into `master` yet. Only the real window at 1920×1080
could not run, as the plan allows on this screen.

Before the gate, an independent review of the code found problems in the queue, the remote
operations, the git layer, the UI and the tests; they were fixed on `wip/p3-fix-*` branches and
merged in `a570dca`, `c8dbab6`, `326e282`, `21feb56` and `c98b78b`, each with tests, and D123 to
D132 record the changes of behaviour. Then the test projects were rearranged for run time
(`2310d10`, D133).

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300 on an Intel Core i7-8650U laptop; screen 3000×2000 at 200% scaling (1500×952 logical work area). The whole phase was built on this machine; no cloud session worked on it |
| .NET SDK | 10.0.303 (`global.json` stays at 10.0.100 with `latestFeature`) |
| Git | 2.36.0.windows.1 locally, with `core.fsmonitor=true` in the owner's global configuration; 2.55.0 on the CI runners |
| Packages | Unchanged since phase 2: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0 |
| New project | `src/VisualCommit.AskPass`, the prompt helper `visualcommit-askpass` (D82): framework-dependent, no references, referenced by `VisualCommit.Git`, so its app host lands next to the app and in every test project's output |
| Screenshots | Scripted: scaling 1. Real window: 200% and a work area of 1500×952 logical pixels, as `run.txt` records them |

## Build, run and test

The table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands). New in
this phase: phase 3's scripted walk-through is a project of its own, and a script times scripted
checks alone.

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
dotnet test --project tests/VisualCommit.VisualTests.Phase3
dotnet test --project tests/VisualCommit.VisualTests.Phase3 --filter-class "*Phase3ChecksD"
dotnet test --project tests/VisualCommit.App.Tests --filter-class "*RepositoryOperationsPullTests"
bash scripts/time-scripted-check.sh "*Check_01_commit_menu"
dotnet test --project tests/VisualCommit.RealWindowTests -c Release
```

- The default run took 19 min 15 s at the gate, on a rested laptop with every other program
  stopped (side by side: `VisualCommit.Git.Tests` 13 min 16 s, `VisualCommit.App.Tests` 16 min
  34 s, `VisualCommit.VisualTests` 11 min 22 s, `VisualCommit.VisualTests.Phase3` 19 min 13 s).
  Earlier in the phase, with other programs busy, it took 21 to 26 minutes, and it takes longer
  when the laptop is hot. It is bound by the processors (git's processes and the virus scanner),
  not by how many tests run side by side (D133). Alone, `VisualCommit.Git.Tests` takes about 7
  minutes, `VisualCommit.App.Tests` 9, the phase 3 walk-through 8 and phases 0 to 2's 2.5. On
  CI the gate's jobs took 10 min 56 s (Windows), 8 min 35 s (macOS), 3 min 11 s (Linux) and
  about 3.5 and 4 minutes (the two containers), build included.
- While working, run one project or one class (`--filter-class`), not the whole run.
  `scripts/time-scripted-check.sh` builds the walk-through project of the phase it is given (3
  when left out) and prints each test's time and the time of each step a phase 3 check noted
  (`artifacts/visual/phase-N/timings/`). Like every partial run, it empties the phase's picture
  folder first.
- The phase 3 checks' classes are `Phase3ChecksA` to `Phase3ChecksD`, one per part of the report;
  a test is named `Check_<NN>[<suffix>]_<what>` (`Check_14b_checkout_failed`), so
  `--filter-method "*Check_14b*"` runs one check.

## Running the visual gate

As in phase 2's handoff, with phase 3's own project and a much longer real-window pass.

0. **Clean up first** (part of the gate). `git worktree list`: every sub-agent worktree under
   `.claude/worktrees/` whose branch is merged is removed with `git worktree remove <path>`
   (without force) and its branch with `git branch -d <branch>`. With `core.fsmonitor=true` in
   the owner's global configuration, git starts a file-system monitor daemon for every working
   tree it runs in, and the daemon outlives the agent that started it and watches every file the
   builds and tests write there: stop each with `git -C <path> fsmonitor--daemon stop` before
   removing the worktree (`git -C <path> fsmonitor--daemon status` says whether one runs). A
   built working tree's `bin` and `obj` folders take about 5.7 GB. The test repositories are
   isolated from the global configuration and start no daemon.
1. Let the laptop rest a few minutes, then `dotnet test` at the repo root. It writes
   `artifacts/visual/phase-{0,1,2,3}/scripted/`, emptying each first. A test that runs over 3
   minutes (shared preparations such as scenario templates not counted, D132), or a run in which
   nothing happens for that long, stops the run with `HANG WATCHDOG` and the names of the tests
   that were running; `VISUALCOMMIT_TEST_HANG_SECONDS` raises the limit for a test that really
   needs it.
2. Rest three minutes, **tell the owner**, and run
   `dotnet test --project tests/VisualCommit.RealWindowTests -c Release` (never `--no-build`).
   It takes over the real mouse and keyboard, needs the desktop unlocked, and compares its
   captures with the scripted pictures of step 1, so nothing may run a partial walk-through in
   between. It took 11 min 11 s at phase 3's gate (11 min 42 s with the build; the estimate had
   been 13 to 16). Phases 0 to 2 took about 6; phase 3 adds 15 tests repeating 19 checks, every
   real input waits 400 ms, and the first context menu of a launch takes up to about 2 s. Never
   start it while other work runs on the machine: sub-agents building or testing take the
   processors and the mouse waits time out. It also times Q1, which a warm laptop can fail by
   itself (Known issues): the rest before it matters.
3. Open every picture of both passes. Before any new run, save `sha256sum` of the pictures you
   inspected: the next run empties the folders. `docs/test-reports/phase-3-pictures.sha256`
   lists the 542 files of phase 3's final run: 388 scripted pictures and 154 real-window files
   (77 captures and their difference pictures);
   `tr -d '\r' < docs/test-reports/phase-3-pictures.sha256 | sha256sum -c` shows which pictures
   of a later run changed. Against `phase-2-pictures.sha256` (which holds phases 0 to 2), 96
   scripted pictures of phases 1 and 2 differ, as the report's "Changes to expected results"
   expects: the enabled toolbar, phase 1's `08d` and `08e` (the left panel follows the graph),
   and phase 1's `11a`, `13b`, `14a` and `14b`, which already differed before phase 3's code.
   The other 27 (all 14 of phase 0, 11 of phase 1, 2 of phase 2) are the same files. Ten
   pictures of the final run vary by nature from run to run (a clone's or a held fetch's
   percentage, an animated progress bar or caret, a temporary folder's random name; the
   report's Run section). Real-window pictures are never byte-identical and must all be opened.

The phase 3 part of the real-window pass is `Phase3RealWindowPass.cs` (the run, `Phase3Run`) and
`Phase3RealWindowPass.A.cs` to `.D.cs`, with the helpers in `RealApp.Phase3.cs` and
`RealApp.Checks.cs`. Until the gate all of it was compiled (CI's Windows job builds it) but never
run; its first run, at the gate, needed no change to it (nothing in the project, or in the
expectation files it links, changed between `2310d10` and the gate's commit). In the real window
it waits for an operation's log line `ops: <Title>: <outcome> after <N> ms: <message>`
(`RealApp.OperationEnded`), finds menus and their items as `ContextMenu` and
`ContextMenuItem`, takes graph rows below a banner from the `ConflictBanner` element's bounds
(D122), moves the pointer to the neutral point (760, 687) before a capture, and never clicks an
item that reaches outside the window ("Copy …", "Open file", "Show in folder"). A held fetch's
percentage varies between runs, so the status bar's progress in `17b`, `31a` and `31b` may differ
from the scripted pictures.

## What changed in the code

The operations sit in three layers: git commands in `GitRepository.<area>.cs`
(`VisualCommit.Git`), the queue in `VisualCommit.Core/Operations`, and the app's
`RepositoryOperations` (`VisualCommit.App/ViewModels/Operations`), which asks the questions,
saves the backups, opens the dialogs, runs the work through the queue and shows the result.
Menus are data built by `RepositoryMenus` and drawn by `MenuLayer` with Avalonia's stock
`MenuFlyout` on the window's overlay layer (D105).

| To do this | Start here |
|---|---|
| Add an operation | Its git calls as an `IGitRepository` member (`src/VisualCommit.Core/Git/IGitRepository.cs`), implemented in the matching `GitRepository.<area>.cs` (`Branches`, `History`, `Remotes`, `Stash`, `Tags`, `Ignore`, `InProgress`, `Backups`) and refused in `FakeRepositoryBase`; its four texts (title, running text, error title, failure summary) as an `OperationText` in `OperationTexts.<part>.cs`; the method on `IRepositoryActions` and its body in `RepositoryOperations.<part>.cs` (`Branches`, `Remotes`, `StashTags`, `Files`), built from the helpers in `RepositoryOperations.Run.cs`: `RunAsync` (through the queue, with the result in the status bar and a failure in the error dialog), `ConfirmAndRunAsync` (confirmation, backup refs, then the work), `ShowDialogAsync`, `GuardAsync`. An operation that waited in the queue checks HEAD again when it runs (D124) |
| Add a menu item | The builder of the object's menu in `ViewModels/Menus/RepositoryMenus.cs` (`ForCommit`, `ForStash`, the branch, tag, remote, file and drop menus): a label, the `IRepositoryActions` call on the full ref name or SHA the menu captured, and a disabled reason as its tooltip. What the builder may read is `RepositoryMenuContext`; a menu that needs a git read first goes through `RepositoryViewModel.ReadMenuContextAsync` and opens only if nothing came between (D131). Test it in `RepositoryMenusTests` with the `RecordingActions` fake |
| Give a new object a menu | An `Open<Thing>Menu` method in `RepositoryViewModel.Menus.cs`, called from the view's `ContextRequested` handler through `Views/Shell/ContextMenuRequests.cs` (which finds the window's `MenuLayerViewModel` and the active repository). Shift+F10 and the Menu key come with it (D106) |
| Add a dialog | A view model deriving from `DialogViewModel<TResult>` in `ViewModels/Dialogs/` and a view named after it in `Views/Dialogs/` (`NameDialogViewModel` → `NameDialogView.axaml`): `DialogViewLocator` finds it by name, so no shared XAML changes. Show it with `RepositoryOperations.ShowDialogAsync` or `DialogHostViewModel.ShowAsync`; dialogs stack (D104). Confirmations are `ConfirmationRequest` and `ConfirmAsync`. Long texts with a URL use `UrlTextBlock`; masked input uses `SecretTextBox` (D130) |
| Add a git read | A method in `GitRepository.<area>.cs`; every read runs with `GIT_OPTIONAL_LOCKS=0` and must never write (D47). The in-progress state is read from files in the git folder without git calls (`GitRepository.InProgress.cs`, D89) |
| Add a git write | As a read, taking `NetworkOptions` (prompt variables, progress, `-c` settings) so that a hook or filter can prompt (D107). `GitRunner` adds `LC_ALL`, `LANGUAGE=en`, `GIT_TERMINAL_PROMPT=0`, `GIT_EDITOR=:`, `GIT_SEQUENCE_EDITOR=:` and `GIT_LITERAL_PATHSPECS=1` to every call (D30, D68, D74, D102, D128; the exceptions `check-ignore` and `stash push`, D108). Put `--end-of-options` before names that may start with "-" (D128) |
| Add a prompt kind | `GitPromptKind` in `Core/Operations/GitPrompts.cs`; `GitPromptClassifier` (`Git/Prompts`) recognises git's or ssh's text; `PromptDialogs` (`App/Services`) maps the kind to its dialog view model in `PromptDialogViewModels.cs`; the view by naming convention. Test with recorded texts through the real helper (`AskPassRunner`, `PromptDialogTests`) |
| Add a banner state | `InProgressKind` and `InProgressState` (`Core/Git/InProgressState.cs`), read in `GitRepository.InProgress.cs`; the text, the Abort label and the Abort question in `ConflictBannerViewModel` (`TextFor`, `AbortButtonText`, `AbortConfirmation`); the abort itself through `IInProgressActions`. Phase 4's Continue and Skip go here |
| Add a background operation | Like `AutoFetchScheduler`: a `GitOperation` with `IsBackground = true`, started with `IOperationQueue.TryStartInBackground`, which starts only when the queue is idle; prompts are denied; it is never pre-empted, and the user may cancel it only while an operation of theirs waits (D85, D125) |
| Add a toolbar button | `ToolbarViewModel` (its enabled state and the disabled reason as tooltip) and `IToolbarActions`, implemented by `RepositoryOperations`; `ToolbarView.axaml` |
| Add a visual check | Write it into the phase's report first (D15). Phase 3's are `tests/VisualCommit.VisualTests.Phase3/Phase3Checks.<part>.*.cs`; its expected values in `Phase3Expectations.<part>.cs`, which the real-window pass compiles too; pictures only through `Shot(app, "<check><step>-<what>")`; the conventions are at the top of `Phase3Checks.Support.cs`. The driver is `ShellDriver` (`ShellDriver.Phase3.cs`, `.Pointer.cs`, `.Log.cs`, `.Checks.cs`) |
| Add a scenario repo | `tests/VisualCommit.Testing/Scenarios.Phase3.cs` (`OperationsAsync`, `StoppedAsync`, `RemoteSyncAsync`, `PushFromElsewhereAsync`, `CheckoutBlockedAsync`, `EdgeCasesAsync`, `AuthRemoteAsync`), pinned in `Phase3ScenarioTests`. A remote that asks for credentials is `AuthRemoteServer` |

## Patterns to follow

| For | Example |
|---|---|
| A git write and its test | `GitRepository.Tags.cs` with `StashTagTests`: a scenario repo, the real `GitRepository`, the result read back with plain git |
| A destructive operation | `RepositoryOperations.DropStashAsync` (`RepositoryOperations.StashTags.cs`): `ConfirmAndRunAsync` with a `BackupRequest` of kind `stash`; the texts in `OperationTexts.StashTags.cs`. Backup refs are `refs/visualcommit/backup/<kind>-<UTC time>[/<name>]`, kinds `reset`, `detached`, `branch`, `remote-branch`, `remote`, `force-push`, `tag`, `remote-tag`, `stash`, `abort` (D83, D127) |
| An operation tested in the app with real git | `RepositoryOperationsStashTagsTests`: `OperationsHarness` opens a scenario, `ScriptedDialogs` answers the dialogs, the status bar and git are asserted. Helpers in `RepositoryOperationsTestSupport` |
| Something that happens while git runs | Hold it, never sleep: `AuthRemoteServer.HoldNextFetchPack` holds a fetch at its pack request; `RepositoryOperationsTestSupport.HoldQueue` holds the queue with a running operation; `RepositoryViewModel.PendingMenu` is the task of a menu waiting for its read (`RepositoryMenusTests.Window.cs`) |
| Parity with a git command | `PullParityTests` (nine classes): the app's pull and `git pull` on copies of the same template, compared for each setting, on Git 2.36 and on CI's 2.55 |
| Credentials end to end | `AuthenticatedRemoteTests` with `AuthRemoteServer` (D97) and `ScriptedPromptHandler`; ssh prompts with `FakeSsh` through `core.sshCommand` (D115) |
| A menu | `RepositoryMenus.ForCommit` and its tests in `RepositoryMenusTests`; how it is drawn, kept on the overlay and invoked through UI Automation: `ContextMenuAdapterTests` |
| A dialog | `NameDialogViewModel` and `NameDialogView.axaml` (validation as the user types with `RefNameRules`, D96); tests in `DialogTests` |
| A scripted visual check | `Phase3Checks.C.Tags.cs` with `Phase3Expectations.C.cs` |
| A real-window check | `Phase3RealWindowPass.C.cs`: one launch shared by checks 13 and 16, waits on `RealApp.OperationEnded`, rows found as `RefItem` |

Rules worth keeping:

- **Two gits.** The development machine runs Git 2.36, CI 2.55, and they word messages
  differently (2.45 added "Disable this message with ..." hints). A check asserts git's lines word
  for word only when `git --version` is 2.36; on any other version it asserts the classifying
  line (` ! [rejected]`, `fatal: ...`), the dialog's title, summary and buttons. The Git-layer
  tests run on both. What a scenario does on git was always derived by running git 2.36, never
  from memory.
- **Never compare git's dates as text.** Git 2.55 writes UTC in `%aI`, `%cI` and
  `--date=iso-strict` as `Z`, 2.36 as `+00:00`. Use `GitIsoDates` (or `AssertGitDate` in the
  checks), or ask git for `%at` and `%ct`.
- **Paths as git names them.** On macOS the temp folder is `/private/var/...` to git and the
  app; expected paths come from `git rev-parse --show-toplevel` (`WorkingTreeAsync`), never from
  `TempRepo.Path`. A path shown to the user keeps the platform's form (backslashes on Windows).
- **Wait for a condition, never for a duration**: `WaitForAsync`, `WaitForIdleAsync`,
  `WaitForLogLineAsync`, `WaitForDialogAsync`, `WaitForToolTipAsync` (which waits for the
  tooltip's fade-in to end); in the real window, the operation's log line. A chain of git
  commands under a full default run needed 60 seconds (`OperationChainTimeout`), where it takes
  18 alone; the default 10 seconds of a headless wait is for one step.
- **The tests' two clocks.** The app's commits and ref updates are dated 2026-01-02 09:00 UTC
  (D72), `TempRepo`'s from 2026-01-01 12:00. A step made through `TempRepo` that must look newer
  than an app operation moves `TempRepo`'s clock first (`TempRepo.MoveClockTo`, D111).
- **Isolation.** The tests and the app the real-window pass starts get no askpass, ssh, proxy or
  auto-fetch variables from the machine (D112, D116). A new variable that git or ssh reads from
  the environment goes into `GitIsolation` and `RealApp.Launch` too.
- **Run time is designed.** xunit runs the tests of one class (or `[Collection]`) one after
  another and different classes side by side; every headless test of an assembly shares that
  assembly's one UI thread. Split a long class by subject, one class per file
  (`RepositoryOperations*Tests`, the `Pull*ParityTests`), and give a large walk-through a project
  of its own (D133). A shared template is awaited through
  `HangWatchdogAttribute.SharedPreparationAsync`, so that its build does not count against the
  test that comes first (D132).
- **The report is the source of truth** for expected results; a test copies them into
  `Phase3Expectations*.cs`. Changing one after the UI is built needs a reason in the report.
- **A string a test compares with the screen** must be what is in view: a text cut with "…"
  is asserted as cut (phase 2's lesson still holds).

## Deviations from the plan

| What | Decision |
|---|---|
| Later phases' actions left out of the menus; what has no menu | D77, D92 (owner's choice) |
| Pull follows git's configuration: a cancellable fetch, then the merge or rebase `git pull` would choose | D78 (owner's choice), D84 |
| Auto-fetch on by default, every 5 minutes, active tab, quiet; one fetch per remote; a timeout for stalled HTTP | D79 (owner's choice), D85, D109, D125 |
| "Stash changes and check out" for a checkout blocked by local changes | D80 (owner's choice), D91 |
| The operation queue lives in Core, not in `VisualCommit.Git`; only the user stops git; stale locks reported | D81, D107, D123, D124, D126 |
| Prompts through an askpass helper and a pipe; `GIT_TERMINAL_PROMPT=0` stays | D82, D118 |
| Which operations confirm and save backup refs | D83, D127 |
| Context menus: the stock `MenuFlyout` on the overlay layer through an adapter; Shift+F10 | D86, D105, D106, D114, D131 |
| Branch drag inside the window | D87, D120 |
| Multi-selection by SHAs; one pick or revert per commit, no sequencer | D88 |
| Basic conflict state; texts for terminal sequences; no stash during an operation; a banner that wraps | D89, D110, D117, D119, D122 |
| Toolbar buttons; double-click map | D90, D93 |
| Merge messages in git's words; fast-forward another branch with `git fetch .`; `-m 1` | D94 |
| Results in the status bar, failures in the error dialog; titles and texts | D95, D113 |
| Name validation in C# | D96 |
| Credential tests against a local server; check 21b through a fake ssh | D97, D115 |
| The left panel follows the graph | D98 |
| Fetch with neither `--prune` nor `--tags` | D99 |
| Force push and remote deletions with leases | D100, D111, D129 |
| Upstreams read as three values; local upstreams | D101 |
| `GIT_SEQUENCE_EDITOR=:` on every call | D102 |
| Credentials hidden in records and on screen; masked boxes; runnable files | D103, D129, D130 |
| Dialogs stack | D104 |
| `git stash push` without literal pathspecs | D108 |
| Test isolation from askpass, ssh and proxy variables; the hang watchdog's shared preparations | D112, D116, D132 |
| Tags peeled to their commit | D121 |
| `LANGUAGE=en`; `--end-of-options`; stops that `rerere` resolved | D128 |
| Phase 3's walk-through in its own project; test classes split | D133 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| Under the reftable ref format (Git 2.45 and later, opt-in), `CHERRY_PICK_HEAD` and `REVERT_HEAD` are no files, so a stopped pick or revert shows no banner | Low today (reftable is not git's default); phase 4's in-progress handling must read the state another way | `GitRepository.InProgress.cs`, `GitRepository.History.cs` (D128) |
| No ssh timeout for auto-fetch: a stalled ssh connection holds the queue until the user cancels it (the status bar offers Cancel while an operation of theirs waits). HTTP ends by itself after 60 s below 1 byte/s | Low; the only way to set one through git would replace the user's `core.sshCommand`. Whether to keep it so is put to the owner | `AutoFetchScheduler`, `NetworkOptions.Configuration` (D125); plan risk 19 |
| `DropFacts.RemoteMapsBranchesDifferently` is never set (nothing reads `remote.<r>.fetch`): a drop onto a branch of a remote with a non-default fetch refspec offers the push as for the default one | Low; such refspecs are rare | `ViewModels/Menus/RepositoryMenuContext.cs` |
| A rename that changes only the case of a branch is refused on Windows and macOS ("A branch named Topic exists in another case."), because Git 2.36 refuses it for a loose ref | Low; it can be done in a terminal in two renames. Whether to keep refusing it is put to the owner | `NameDialogViewModel`, `RefNameRules` (D131) |
| The error dialog's git output is a text box that wraps inside a URL (check 21d: "fatal: Authentication failed for 'http://127.0.0.1:47391/remote.git/'"); only the summaries and prompt texts keep URLs whole (`UrlTextBlock`) | Cosmetic | `ErrorDialogView.axaml` (`ErrorOutput`) |
| The first context menu of a process takes up to about 2 s to appear (Avalonia's flyout on its first use; 1.7 s measured on a loaded machine); later ones are fast | Noticeable once per start; the harnesses wait for it | `MenuLayer` |
| Menus are 200 to 520 wide; a longer label is cut with "…" and shows whole in its tooltip, a header at 340 | Cosmetic, for very long branch names | `Theme/Menus.axaml`, `MenuLayer.axaml.cs` |
| After a closed tab or "Quit now" during a fetch or push, lock files git left are only logged; the next fetch fails with git's `File exists` until they are removed (after Cancel the app offers "Remove lock files") | Low | `LockFiles`, `RepositoryOperations.Run.cs` (D81, D126) |
| Local operations cannot be cancelled: a git-lfs download during a checkout runs to its end; it can prompt, though | Low; not tested automatically (no LFS server) | D81, D82 |
| On macOS and Linux, Cancel kills git only; ssh and the askpass helper end when their pipes break (D37). Not tried on a Mac | Unknown until the Mac checklist | `GitRunner` |
| OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE`: started from a terminal, such an ssh still asks there (Ubuntu 20.04 ships 8.2) | Low | D82 |
| The askpass helper is framework-dependent: it needs the app's .NET runtime | Phase 8's self-contained packaging must include it | `src/VisualCommit.AskPass` |
| Port 47391 of the visual checks' server: if taken, the server falls back and check 21's pictures differ in the port | Low | `AuthRemoteServer` (D97) |
| The detached-HEAD question counts commits that only a stash or a backup ref reaches as left behind | Asks more often than needed, the safe way | `RepositoryOperations.Branches.cs` |
| `RepositoryOperations.Run.cs` keeps two unused `NotBuilt` helpers from the work packages' stubs | Debt, harmless | `RepositoryOperations.Run.cs` |
| The first tokenization of a grammar that embeds others (Markdown) reads TextMateSharp's grammar list without a shared lock while another thread may add to it; no test has failed on it | Low | `DiffHighlighter` (`Controls/Diff/DiffSyntax.cs`) |
| Noticed by the gate's inspection and left (the pictures match their expected results): the in-progress banner wraps the git command at its hyphen (25a, 25e); a disabled menu item's tooltip opens below the pointer and covers the next item, and a long worktree path in a tooltip wraps at any character (30g); the sixth row of the unstaged list is cut by the stage panel's fixed split (26a); in the real window a hover colour stays on a left-panel row after a right-click (28a) | Cosmetic | The report's "Found by the gate" |
| The working-changes row's dashed line is drawn through the node of another branch that uses the same lane when HEAD is not the top row (06f; D60's drawing rule) | Cosmetic and rare; a question of the graph's layout, put to the owner | `Controls/CommitGraphControl.Render.cs`; the report's "Found by the gate" |
| Q1 varies with the laptop's temperature more than its margin allows: 1189 ms at the gate on a rested laptop, but 1438 to 2268 ms in other runs the same day on a warmer one. The real-window test fails above 2000 ms, so a warm laptop can fail the gate by itself. Run alternately on the same machine, the phase 2 build (`master` at `d3814bd`) needed 1688 to 1911 ms and phase 3's 1618 to 2002 ms: phase 3 costs no more than the noise | Can fail a gate that is otherwise green. Rest the laptop before the real-window pass; to tell a slower build from a warm machine, run the old and the new build alternately on the same machine | `Phase1RealWindowPass.cs` (the 2000 ms limit); the report's Run section; plan risk 10 |
| The default run is bound by the processors, and the virus scanner takes a large share of them: 19 min 15 s at the gate rested and alone, 21 to 26 minutes with other programs busy | Every full run and gate; an exclusion for the repository folder and `%TEMP%\VisualCommit.Tests` is put to the owner | Windows Security on the development machine; plan risk 12 |
| Durations written before phase 3's gate are stale: phases 0 to 2's 5 to 6 minutes for the default run and about 6 for the real-window pass, and CI's time per job "not measured since". Measured at the gate: 19 min 15 s, 11 min 11 s, and CI's jobs 3 min 11 s to 10 min 56 s (State of the repo) | Misleading when planning a run, wherever a doc still gives the old figures | CLAUDE.md's Commands section (the real-window pass's "about 6 minutes", CI's time per job); phase 2's handoff keeps its own figures as history |
| CI's time limits were raised for phase 3's tests: 22 minutes per job and 14 per test step (25 for the container jobs). A hung job now runs that long before it is stopped | Low; whether to keep them is put to the owner | `.github/workflows/ci.yml` |
| Earlier phases' issues that remain: the welcome page's wrapping of a long folder (phase 6; 11a), the side-by-side diff's overlay scroll bars (phase 2's 07d), type changes without hunk actions, untracked nested repositories, old discard snapshots without UI, the Linux watcher's inotify use: see phase 1's and phase 2's handoffs | As there | |

## Things that cost time

- **Sub-agents in worktrees.** The phase was built in work packages by parallel agents, each in
  a git worktree under `.claude/worktrees/` on a `wip/p3-*` branch, merged with `--no-ff`. A new
  worktree starts from `master`, not from the phase branch: the agent must create its branch from
  `phase/3-operations` first, or it builds on phase 2. Give each package the files it owns and
  keep the shared ones (`IGitRepository.cs`, `FakeRepositoryBase.cs`, `RepositoryViewModel.cs`,
  `MainWindow.axaml(.cs)`, `GitRunner.cs`, the theme's tokens) with the lead: partial classes
  (`RepositoryViewModel.Menus.cs`, `.Selection.cs`, `.InProgress.cs`), one `OperationTexts`
  part per package and `DialogViewLocator`'s naming kept the merges apart. Even so, two parts of
  the real-window pass each defined a `WaitForDetails` and the merge stopped compiling
  (`fdb63a2`): build after every merge.
- **Disk and stale processes.** Each built worktree holds its own `bin` and `obj` (about 580 MB
  per project, about 5.7 GB for this solution), and the worktrees of parallel agents filled the
  disk. Every worktree also kept a git fsmonitor daemon running after its agent ended (the
  owner's global `core.fsmonitor=true`): 79 were found before the gate, running for days. Stop
  the daemon and remove the worktree as soon as its branch is merged, and check that none runs
  before the gate (step 0 of "Running the visual gate").
- **The default run grew to 21 to 26 minutes** (19 min 15 s at the gate, rested and alone). One
  big test class (`RepositoryOperationsTests`) and one walk-through project were long serial
  chains; splitting them (D133) cut each project's own time but not the whole run, which is
  bound by the processors. Waits that were generous alone timed out under the full
  run (`c8e0323`), and the hang watchdog charged a shared template's build to whichever test came
  first (D132).
- **CI limits.** The test step's 5-minute limit cut the Windows and macOS jobs once phase 3's
  tests ran (run 38162210730); the limits are now 22 minutes per job and 14 per test step (25 for
  the container jobs) in `.github/workflows/ci.yml`. A whole run took about 10 to 11 minutes
  late in the phase; its longest job on the gate's commit, Windows, took 10 min 56 s.
- **Failures only CI found.** Git 2.55's `Z` dates (`f22d7ea`, `e721323`); macOS's
  `/private/var` paths (`e721323`); two macOS waits that assumed one layout pass was enough
  (`07203b7`, `106378e`); the Windows runner's elevated process, whose pipe owner the MSYS2 ssh
  could not match (D118); `U+2060` missing from Inter, so Linux and macOS drew URLs with another
  font's line metrics (`2470ed3`); timing faults in new and old tests, such as phase 1's check
  18, which looked at the first row's labels before they were laid out (run 38203733545,
  Windows, during the gate; `594f9fb`). Read every job of every run; a failure on one platform
  is usually a wrong assumption in the test, not flakiness.
- **The gate ran four times**, the real-window pass three times. Each CI flake and each finding
  of the inspection led to a commit that changed more than docs, and after such a commit both
  passes run again on the new commit and their pictures are inspected again (CLAUDE.md). Collect
  all fixes first: finish inspecting every picture of a run and read every CI job, make all the
  fixes, then run the gate once more.
- **Shell quirks**, as in phase 2: in the Bash tool a heredoc fails on apostrophes; PowerShell 5.1
  garbles dashes and backticks in docs; write docs with the file tools.

## For the next phase

Reuse: the queue and `RepositoryOperations` for every rebase operation (`RunAsync`,
`ConfirmAndRunAsync` with a backup, `OperationText`s); `InProgressState` (with `Remaining` and
`AbortKeepsHead`, D110) and `ConflictBannerViewModel` for Continue and Skip;
`IInProgressActions` for the banner's buttons; the `abort-<time>` backup and D67's snapshot
before an abort; `GraphSelection`'s SHA-based multi-selection for squash (D88); the drop menu
(`BranchDrop`, `RepositoryMenus`) and the branch menus for "Rebase onto"; `DiffTextView` and
`PatchBuilder` for the resolver; `Scenarios.StoppedAsync(InProgressKind)` and
`Scenarios.ConflictAsync` for conflict states; `PullParityTests` for a pull that rebases.

Do first:

1. Read the Known issues above and phase 2's, and check the latest CI runs on `master`.
2. Check that no worktree, `wip/*` branch or fsmonitor daemon is left from phase 3
   (`git worktree list`, `git branch --list "wip/*"`).
3. Write phase 4's checks into `docs/test-reports/phase-4.md` before the UI, as phase 3's report
   does: scenario graphs derived by running git, menu notation, git's texts for 2.36.

Open points for phase 4 (record each as a decision; ask the owner those that are a matter of
taste):

- **Where phase 4's scripted checks run.** The default run is already 19 to 26 minutes on the
  laptop (19 min 15 s at phase 3's gate, rested and alone). A project of its own
  (`VisualCommit.VisualTests.Phase4`) gets a UI thread of its own, but the run is bound by the
  processors; decide what to add, and what to measure, before writing the checks.
- **The sequencer.** Phase 3 never starts git's sequencer (D88) and sets
  `GIT_SEQUENCE_EDITOR=:` on every call (D102). Interactive rebase sets its own sequence editor;
  decide how the todo list reaches git, and whether a multi-commit pick should now use the
  sequencer so that Continue works on it.
- **Confirmation and backups for a rebase** (Q3): D83 confirms only the abort; a rebase rewrites
  a branch, so decide whether it saves a backup ref of the old tip (and of what kind).
- **Continue and Skip** for each `InProgressKind`, including a pick sequence started in a
  terminal (D110) and a pull's rebase; the banner's texts now say "continue in a terminal".
- **Reftable** (D128): if the resolver depends on `CHERRY_PICK_HEAD` and `REVERT_HEAD`, read them
  in a way that works under both ref formats.
- **The resolver's control**: `DiffTextView` on AvaloniaEdit (D64) is the planned base (plan
  risk 2).

Risks to watch:

- The run time of the default tests and of the real-window pass (plan risk 12): the laptop is
  CPU-bound with phase 3's tests, and the real-window pass took 11 minutes at phase 3's gate.
- Q1 on a warm laptop (plan risk 10): it measured up to 2268 ms on phase 3's gate day, over the
  2000 ms limit. Rest the laptop before the real-window pass, and compare builds alternately on
  the same machine before blaming the code.
- The menu adapter's Invoke peer relies on `MenuBase.Close()` and `MenuItem.ClickEvent` as in
  Avalonia 12.1.3; `ContextMenuAdapterTests` catch a change when Avalonia is updated.
- Git's texts and rules differ between 2.36 and 2.55 (and change again); a rebase's messages and
  its todo format are among them. Derive every expected state with real git and run the Git-layer
  tests on CI's git.

## Waiting on the owner

- Accept phase 3, or say what to change: "Phase 3 accepted" merges `phase/3-operations` into
  `master`. A session on any machine can do that and what follows it.
- When you have a Mac at hand: the ten-step checklist at the end of the
  [phase 3 test report](../test-reports/phase-3.md) (among its steps cancelling a fetch, the
  first context menu's latency, and Cmd for selecting several commits), and the seven-, nine-
  and eight-step ones of phases 2, 1 and 0, still open.
- Questions the gate left, each with a recommendation (Known issues has the details):
  - **CI's time limits.** Keep 22 minutes per job and 14 per test step (25 for the container
    jobs)? Recommended: keep them. The gate's Windows job took 10 min 56 s, and the old 5-minute
    step limit cut the jobs once phase 3's tests ran (run 38162210730).
  - **The dashed line through another branch's node (06f).** Change how the working-changes
    row's line is drawn now? Recommended: no. It shows only when HEAD is not the top row; settle
    it with the next change to the graph's layout.
  - **A virus-scanner exclusion.** Exclude the repository folder and `%TEMP%\VisualCommit.Tests`
    from Windows Defender on the development machine? Recommended: try it for those two folders
    only, which hold the project's builds and test repositories, and measure the default run
    before and after. It is a security setting of your machine, so only you can make it.
  - **Renaming a branch in case only** on Windows and macOS. Keep refusing it (D131)?
    Recommended: keep. It is rare, and two renames in a terminal do it; the app can do the two
    renames itself later if users ask.
  - **No ssh timeout for auto-fetch** (D125). Keep it so? Recommended: keep. A timeout through
    git would replace the user's own `core.sshCommand`, and the status bar offers Cancel for a
    stalled auto-fetch as soon as an operation of the user's waits behind it (plan risk 19).
- By hand on any machine, when convenient: a real ssh server's host-key question and Git
  Credential Manager's own window (D97), which no automated test can reach.
