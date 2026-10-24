# Phase 6 handoff — Multi-repo and productivity

Written at the end of phase 6 for the session that runs phase 7. That session has not seen any
of this phase's conversation. How the app is built is in [architecture.md](../architecture.md),
why in [decisions.md](../decisions.md) (D187 to D236 are this phase's: D187 to D202 the owner's
answers, D203 to D230 the design's, D231 to D236 the build's and the review's); the phase's design,
kept as history, is [phase-6-design.md](../phase-6-design.md), whose "As built" notes and the lead's
rulings say what the build and the review before the gate settled, and whose section 16.2 says what
moved to phase 8 and the backlog. The report, [test-reports/phase-6.md](../test-reports/phase-6.md),
holds the 34 checks, its own decisions (WD, KD, SD, GD and LD, which the design took in) and the
lead's rulings on its open points. This file says what was proven, what to copy, and what to watch
out for.

Markers of the form [GATE-TBD: …] stand for what the gate, running while this was written, decides,
and [LEAD-TBD: …] for what the repo does not record; the closing steps replace them.

## Result

Everything in the plan's "Delivers" list was built. The check names are those of the report (34
checks in five parts: W1 to W9 workspaces and the dashboard, K10 to K13 the palette and the keys,
S14 to S21 the settings screen and the theme's following, G22 to G25 ignored files and fetch
refspecs, L26 to L34 every main screen in both themes, the long folder and the tab strip); its
section "What proves each Delivers item" is the longer form of this table, with the owner's
decisions D187 to D202 and the checks that show each.

| Delivered | Proven by |
|---|---|
| Workspaces kept in `workspaces.json` (D189); a dashboard tab at the start of the strip (D187) with each repository's branch, ahead and behind and uncommitted changes, and Fetch all and Pull all; opening a workspace shows its dashboard only, and a repository opens as a tab from there (D188) | W1 to W9, K10 (its second method), K13, L30, L33; `JsonWorkspaceStoreTests`, `WorkspaceCommandsTests`, `RepositoryScannerTests`, `AddRepositoriesDialogViewModelTests`, `WorkspaceFolderProbeTests` (alone for a saved folder inside another repository), `WelcomeWorkspacesTests`, `DashboardTabTests`, `DashboardSessionTests`, `DashboardColumnsTests`, `DashboardViewTests`, `DashboardRefreshTests`, `DashboardActivityLogTests`, `SummaryModeTests`, `PromotionTests`, `OpenRepositoriesTests`, `BulkRunTests`, `BulkSlotsTests`, `DashboardToolbarTests`, `DashboardBulkViewModelTests`; `Phase6ScenarioTests` |
| The command palette on Ctrl+Shift+P (Cmd+Shift+P on macOS), with the app's commands and the objects they act on (D191, D212); a fixed set of keys, shown in the palette, the menus and the tooltips and listed in the settings screen (D192, D199, D213 to D215) | K10 to K13, S14 (the list's 50 rows), L27, L31, L34; `AppShortcutsTests`, `ShortcutTextTests`, `KeyRouterTests`, `TabKeysTests`, `RegionKeysTests`, `CommitKeyTests`, `MenuKeyColumnTests`, `ShellToolTipTests`, `ToolbarToolTipTests`, `RepositoryMenusKeyTests`, `PaletteMatcherTests`, `CommandPaletteViewModelTests`, `PaletteEntriesTests`, `CommandPaletteViewTests`, `PaletteGrowthTests` |
| The settings screen (D216); the identity, the pull mode and the merge tool in git's own configuration, global and "This repository" (D190, D200, D217, D218); theme polish and a theme that follows the system (D194, D223, D225) | S14 to S21, L26 to L31, L34; `SettingsDialogViewModelTests`, `SettingsDialogViewTests`, `SettingsEntryTests`, `PickDialogViewModelTests`, `GitSettingsViewModelTests`, `GitConfigReadTests`, `GitConfigWriteTests`, `GitConfigLocationsTests`, `GitIdentityTests`, `SystemThemeTests`, `SystemThemeStartTests`, `ThemeServiceTests`, `ThemeTokensTests`, `ToolTipStyleTests`, `DragLayerTests`; the isolation in `ShellTests` and `GitIsolationTests` |
| From phase 1: a long folder in the welcome page's error breaks only after a separator (D227) | L32, W1's `w1e` and L30's `l30c` (the scan dialog's folder line); `PathTextBlockTests`, phase 1's check 11 on the drawn lines |
| From phase 3: `AutoFetchMinutes`, `StashIncludeUntracked` and Open file's extra extensions on the screen; the pull mode through `pull.rebase`, `pull.ff` and `branch.<name>.rebase` (D78, D84, D219); "Open in editor" after "Open file" (D226) | S16, S17, S19; `AutoFetchSchedulerTests`, `PullConfigurationTests`, `PullModesTests`, `PullFastForwardSettingTests`, `KnownEditorsTests`, `EditorCatalogTests`, `EditorCommandsTests`, `FileMenuEditorTests`, `RepositoryOperationsFilesTests`, `RepositoryOperationsSettingsTests`; `PullParityTests`, `PullRebaseParityTests` and `PullFailureParityTests` unchanged |
| From phase 4: git's `merge.tool` chosen on the screen (D149), the tools git knows looked up in the background and kept (D202) | S20; `MergeToolHelpTests`, `GitSettingsViewModelTests`, `GitSettingsChangedTests`, `AppSessionTests` |
| From phase 3: the bulk runs through each repository's `OperationQueue`, with the toolbar's titles, Cancel and error dialog (D81; failures kept in their rows, D195, D211); auto-fetch for the active tab only | W4 to W8; `BulkRunTests`, `RepositoryOperationsFetchTests`, `RepositoryOperationsPullTests`, `AutoFetchSchedulerTests`, `PromotionTests`, `PromptDialogsTests`, `OperationQueueTests`, `AskPassTests` |
| From phase 3: the fetch refspecs read; the drop's push, `Delete <b> on <r>…` and checkout's local name follow the mapping, with the reasons R1 to R4 and R2's checkout without an upstream (D201, D222) | G25; `FetchRefspecsTests`, `FetchRefspecsParityTests`, `RefReadingTests`, `RemoteBranchNamesTests`, `RepositoryMenusTests`, `RepositoryOperationsRemotesTests`, `RepositoryOperationsPushTests`, `RepositoryOperationsRefspecFolderTests`, `LeftPanelViewModelTests`, `UpstreamNameTests` |
| From phase 5: Keep ignored files, on by default, for every checkout, merge, rebase, cherry-pick, revert and stash apply or pop, not the hard reset; a setting turns it off (D193, D196, D221) | G22 to G24, S21; P24 to P27 changed on purpose; `KeepIgnoredCheckoutTests`, `IgnoredInTheWayTests`, `DirectoryRenamesTests`, `IntegratePullTests`, `RebaseTodoTests`, `RepositoryOperationsKeepIgnoredTests`, `OperationTextsTests`, `JsonSettingsStoreTests`; by these tests alone: the interactive rebase, the squash, "Check out and rebase" and the pull's merge and rebase |

The harness items that are files are proven by the files and their tests: the two walk-through
projects in `VisualCommit.slnx`, the real-window project's link
`..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs`, `scripts/time-scripted-check.sh` (phase
6 by default, part L to `Phase6Look`) and CLAUDE.md's rows; the scenarios by `Phase6ScenarioTests`,
the ports 47393 and 47394 and the folder server by `AuthRemoteServerTests`, the hold by
`FetchHoldTests`, the isolation by `ShellTests` and `GitIsolationTests` (with the tripwires
`GitIsolationTripwireTests` and `Phase6TripwireTests`), the drivers by `Phase6SupportTests` and
`ShellDriverCheckSupportTests`.

The plan's "Done when":

- **Ten repositories checked and updated from one screen.** W2 reads all ten rows on one screen;
  one Fetch all (W4) decides every row (all eight with a remote fetched, none failing, the other two
  left out with their reasons) and one Pull all (W7) too (five pulled or refused with git's reason,
  five left out with design 4.2's). This is design 17.5's reading of the plan's "ten repos in mixed
  states", for the owner to confirm at acceptance.
- **Every main action reachable from the keyboard** (R7). K12's two methods press 34 of the 35 rows
  of design 6.1's table, each in a state reached by keys alone; K10 presses the 35th, Ctrl+Shift+P,
  and runs the palette by the keyboard alone; K13 the resolver's and a dashboard's keys.
- **The visual gate passes.** [GATE-TBD: passed or not, on which commit, and any check that failed
  or could not run.]

[GATE-TBD: what the gate found, each fault with the commit that fixed it, as the report's "Found by
the gate" lists them; or that it found nothing.]

Nothing in the "Delivers" list was moved to a later phase. The follow-ups the design found (design
16.2) are in plan.md: to phase 8, a broken `~/.gitconfig`'s own message (design 8.5), a palette
made for high contrast and the dark theme's text below 4.5:1 with the dark danger button's label,
left with the owner's consent (D198; design 11.10, 11.11), the icon buttons without an automation
name (15.5), and by hand the macOS and Linux editor install paths and the macOS theme observer
(12.2, 11.9); to the backlog, reordering workspace rows by a drag (1.10), "Open in editor" at a line
from blame and the diff (12.1), editing `mergetool.<tool>.path` or `.cmd` and `merge.guitool`
(8.10), branch keys other than `branch.<b>.rebase` (8.8), keys the user can change (D192), a write
that refreshes a stale index (risk 29, the owner's call) and undo of a remote's removal under a
namespace outside `refs/remotes/<r>/` (10.8 #1). Design 10.8 #10 (a fetch line into `refs/heads/`,
Known issues below) is marked there "for the backlog", but plan.md's backlog does not list it.

Limits by design, not faults: a dashboard row has no file watcher; it is read when the dashboard or
the window becomes active (rows last read 10 s ago or more), on F5 and Refresh, after its own
operations and for a run that waits for it, at most four reads at once, and a row whose worktree is
open in a tab follows that tab (D207); the bulk runs share four slots window-wide and never fetch
one common git folder twice at once (D210); a run's prompts are one group, one dialog at a time
(D210); the keys are a fixed set (D192); the settings screen applies its changes at Save, with no
preview (D216); a workspace edit is not in the undo history (D203); Keep ignored files does not
cover the hard reset, the other resets, discards, the resolver's sides, the restores or undo (D196,
D221, design 9.13); the dashboard has no "last fetched" column (D206); with two instances on one
data folder the last save of `workspaces.json` wins, as for `settings.json` and `session.json`.

## State of the repo

Branch `phase/6-multi-repo`, built from 2026-10-18 to [GATE-TBD: the date the gate passed] on the
Windows machine from `master` at `acdac12` (phase 5's merge `c299939` and its status commits): 349
commits on top of it up to `da54d8b`, the last code commit before the gate (2026-10-23).
[GATE-TBD: the gate's commit, and the number of commits up to it.] The merge commits name each work
package (`git log --oneline --merges phase/6-multi-repo ^master`): WP-S `80fc877`, WP1 `31f525b`, WP2
`5b8b7a8`, WP3 `3146d8d`, WP4 `1d99be3`, WP-T `e7144ec`, WP-W `0da0eec`, WP-D `125e792`, WP-IA
`cc3aed3`, WP-RA `0c91d01`, WP-B `e974c35`, WP-SET `eae7edc`, WP-K `73ac284`, WP-V's parts G
`da4a956`, W `48df7af`, S `5d0a442`, K `e0f3c4a` and L `fb4aa59`, WP-R `55b5de0`, and the review's
fixers C `15b00c8`, B `1dfdd6d`, A `d6a634d` and D `da54d8b`; architecture.md as built is merged in
`174ec4e`.

- `dotnet test`: [GATE-TBD: passed of total, the time and the hours in UTC, and when each of the
  nine projects ended]. For comparison: phase 5's gate 3,233 tests in 43 min 22 s; the baseline on
  `acdac12` at the phase's start 3,233 in 37 min 29 s (the laptop idle); after WP-S, rested, 3,299
  in 43 min 33 s; design 17.6 estimated 49 to 54 minutes rested for the whole phase.
- The real-window pass, 67 tests (the 63 methods of phases 0 to 6 and the 4
  `Phase3ExpectationsTests` that its link compiles): [GATE-TBD: passed of 67, the time and the
  attempts; Q1 in ms within the pass, and alone if measured; the graph's frame times]. Phase 5's
  gate: 59 of 59 in 24 min 57 s, Q1 1,826 ms; design 17.6 estimated 30 to 33 minutes.
- Pictures: `docs/test-reports/phase-6-pictures.sha256` lists [GATE-TBD: the number of pictures the
  gate inspected: scripted (phase 6's 198 and phases 0 to 5's 774), real-window captures (phase 6's
  26 and phases 0 to 5's 155) and difference pictures]. [GATE-TBD: how the inspection ran (its
  batches and inspectors), what it found, and where its verdicts are kept outside the repo.]
- CI: [GATE-TBD: the run of the gate's commit with its link, each job's result and test count, and
  the Test steps' times against D232's 55 minutes]. The last runs before the gate: 39997105336 on
  `1dfdd6d` (four jobs green, Windows' Test step 24 min 0 s; macOS failed S19 alone, its Test step
  28 min 52 s), 40004153661 on `57d5c7b` (fixer A's `git remote add team/fork` test failed on Git 2.55
  and 2.56, and macOS's S19 again; fixed in `6fe269c` and `da54d8b`) and 40009544956 on `6fe269c`,
  cancelled by the next push.

Before the gate, status step 22: an independent review of the code, whose findings four Opus fixers
fixed with tests, each merged with its notes in the design and the report: C `15b00c8` (the settings
screen, the session and the texts; D234, D235, D236), B `1dfdd6d` (the dashboard, summary mode, the
registry and the bulk runs), A `d6a634d` (the git layer, refspecs, pushes and the runner, twelve
findings; D233) and D `da54d8b` (the keys, the palette, part S's read-backs and the timing tests).
The review was four Opus reviewers, read-only, on `fb4aa59`, one per angle: git safety and data loss
(13 minor findings; the worst, git's directory-rename detection writing over an ignored file at a
path the app's check never read), threads, lifetimes and leaks (1 major, 8 minor, a nit), integration
and design fit (1 major, 15 minor) and tests and CI robustness (part S's read-backs its major one).
The lead ruled on each: a few became known issues (design 10.8 #8 and #10, the palette's macOS
duplicate, design 1.2), the rest went to the fixers by area: 14 to C, 18 to B, 12 to A and 11 to D.
The design's notes "As built at status step 22's review" record every behaviour the
fixes changed.

[GATE-TBD: the phase's state: "Awaiting acceptance" with the gate's commit, or "Blocked" and why.]
Only the real window at 1920×1080 cannot run on this screen, as the plan allows: W3, S14's second
method and every 1920×1080 step are scripted only.

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300 on an Intel Core i7-8650U laptop; screen 3000×2000 at 200% scaling (1500×952 logical work area). The whole phase was built on this machine, partly by sub-agents in git worktrees; no cloud session worked on it |
| .NET SDK | 10.0.303 (`global.json` stays at 10.0.100 with `latestFeature`) |
| Git | 2.36.0.windows.1 locally, with `core.fsmonitor=true` in the owner's global configuration. Also on the machine: Visual Studio's 2.55.0.windows.5 (status.md's Notes give its path), which lacks `git http-backend` and `sleep` (plan risk 28), and a MinGit 2.56.0.windows.2 that the lead unpacked in a Claude session's scratch folder under `%TEMP%\claude\`, which may be cleared. On CI: Windows 2.56.0.windows.2, macOS 2.56.0 (Homebrew), `ubuntu-latest` 2.55.0, the two containers 2.43.0 |
| Packages | Unchanged since phase 2: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0; phase 6 added none. New Lucide icons: layout-grid, settings, rotate-cw and ellipsis; the three that derive from Feather (settings, rotate-cw, and ellipsis from more-horizontal) are added to `THIRD-PARTY-NOTICES.md` |
| New projects | `tests/VisualCommit.VisualTests.Phase6` (parts W, K, S and G, 38 test methods) and `tests/VisualCommit.VisualTests.Phase6Look` (part L, 9), D230: nine test processes in the default run |
| Tools | `scripts/compare-pictures.py` needs Python with Pillow and NumPy |
| Screenshots | Scripted: scaling 1. Real window: [GATE-TBD: the scaling and work area the gate's `run.txt` records; 200% and 1500×952 logical pixels on this machine] |

## Build, run and test

The table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands). New in
this phase: phase 6's two walk-through projects and `scripts/time-scripted-check.sh` taking phase 6
when no phase is given (part L goes to `Phase6Look`).

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
dotnet test --project tests/VisualCommit.VisualTests.Phase6
dotnet test --project tests/VisualCommit.VisualTests.Phase6 --filter-class "*Phase6ChecksW"
dotnet test --project tests/VisualCommit.VisualTests.Phase6Look
dotnet test --project tests/VisualCommit.VisualTests.Phase6Look --filter-method "*Check_L27*"
bash scripts/time-scripted-check.sh "*Check_W4*"
python scripts/compare-pictures.py <reference folder> artifacts/visual <output folder>
dotnet test --project tests/VisualCommit.RealWindowTests -c Release
```

- The default run: [GATE-TBD: its time at the gate and each project's end]. During the build, runs
  made with two builders beside them took 60 min 48 s (4,341 tests, `125e792`) and 68 min 59 s
  (4,689, `eae7edc`): that is the load, not the code. Rest the laptop and run nothing beside a run
  that is to be measured.
- Design 17.3's measurement, which decided where phase 6's view tests went: `VisualCommit.App.Tests`
  alone took 21 min 16 s for 1,319 tests (the processors at 94.3 %) and its 449 headless tests 8 min
  26 s (40 %, the processors at 55.8 %). The processors set that project's length, not its one UI
  thread, so the view tests went into `VisualCommit.App.Tests` by subject and no view-test project
  was made (D230).
- A partial run that takes screenshots empties that phase's picture folder first. Phase 6's two
  projects share one folder, which `VisualRun` empties once per run key: run them by one `dotnet
  test` at the root or one after the other, never as two `dotnet test` commands at once.
- Phase 6's check classes are `Phase6ChecksW`, `Phase6ChecksK`, `Phase6ChecksS` and `Phase6ChecksG`
  (Phase6 project) and `Phase6ChecksL` (Phase6Look); a test is named `Check_<id>_<what>`
  (`Check_W4_fetch_all_progress`), one per app start: 47 methods, 49 app starts and 198 pictures, 38
  methods and 130 pictures in Phase6, 9 and 68 in Phase6Look. On `fb4aa59` Phase6 ran 130 tests and
  Phase6Look 19, their expectation and support tests included.

## Running the visual gate

As in phase 5's handoff, with two more projects, a longer real-window pass, Windows' high contrast
off, and reference pictures for phases 0 to 5 (design 17.7):

1. **Stop what is left.** Remove every worktree (`git worktree list`) and stop git's fsmonitor
   daemons: phase 4's handoff gives the PowerShell commands (`Get-CimInstance Win32_Process -Filter
   "Name='git.exe'" | Where-Object { $_.CommandLine -match 'fsmonitor--daemon run' }` lists them,
   `| ForEach-Object { Stop-Process -Id $_.ProcessId -Force }` stops them). Under the owner's
   `core.fsmonitor=true` git starts one for every repository a dashboard reads, besides one per
   worktree.
   Look at the free space on C: and D:.
2. **The references**, made on 2026-10-23 after the review (17.7): phases 0 to 5's walk-throughs on
   `master` (`acdac12`) in a worktree of their own (`dotnet test`, 2,295 of 2,295 in 58 min 16 s),
   copied with phase 5's 306 real-window files to `D:/Projects/Personal/gate6-reference/` (1,080
   pictures, `scripted-pictures.sha256`). 752 of the 774 scripted pictures match
   `phase-5-pictures.sha256`; the 22 that differ were inspected in full (`reference-verdicts.md`
   there): 21 vary by nature (a temporary folder's name, a caret, a progress bar, the number of
   reads, a few anti-aliased pixels) and `u23f` differs by a change of the machine since 2026-10-16.
   Pictures with the focus in a text box (`f10b` to `f10f` too) can catch the caret either way.
   Phase 7's gate makes its own references on `master` once phase 6 is merged.
3. Rest the laptop, then `dotnet test` at the repo root: [GATE-TBD: minutes]. It writes
   `artifacts/visual/phase-{0,1,2,3,4,5,6}/scripted/`, emptying each first.
4. Rest the laptop well, **tell the owner**, check that the desktop is unlocked and Windows' high
   contrast is off (D224: S15 fails while it is on, and the pass never changes the desktop's
   theme), and run `dotnet test --project tests/VisualCommit.RealWindowTests -c Release` (never
   `--no-build`): [GATE-TBD: minutes] for phases 0 to 6. Phase 6's part is
   `Phase6RealWindowPass.cs` (the run `Phase6Run`, the order `Phase6Order`) with `.W.cs`, `.K.cs`,
   `.S.cs` and `.L.cs`, and `RealApp.Phase6.cs`, `.Phase6.Dashboard.cs`, `.Phase6.Palette.cs` and
   `.Phase6.Settings.cs`: 8 methods (W1, W2, W4, W9, K10, S14, S15, L34), 9 launches (W9 restarts
   for real) and 26 captures, all at 1100×700. W1 chooses the copy's root in Windows' folder dialog;
   S14 picks `notepad.exe` in Windows' file dialog and cancels; S15 starts with System and compares
   with the scripted twin of the owner's mode, under names of its own; L34 launches with Theme Light.
   Q1 is phase 1's `Checks_8_and_9_the_100k_commit_repo_and_Q1`; run alone with `--filter-method`,
   it empties phase 1's `real-window/` folder, so the whole pass runs again afterwards.
5. Right after the two passes, before anything else runs, write the list (CLAUDE.md gives the
   command) as `docs/test-reports/phase-6-pictures.sha256`, then `python scripts/compare-pictures.py
   D:/Projects/Personal/gate6-reference artifacts/visual <output folder>`. The Settings button
   changes every earlier window picture in one toolbar box, so the earlier pictures are judged by
   region: a change outside the regions that design 16.1 and the report's "Changes to expected
   results" name for a picture is a defect, and so is any change in an earlier picture's tab strip.
   The inspectors' batches (17.7 step 4): phase 6's pictures in full by part (W, K, S, G, and L in
   two); its real-window captures with their twins; the earlier scripted pictures (phases 0 to 2, 3
   in two, 4, 5 in two) against reference, difference picture and 16.1; the earlier real-window
   captures; CI's macOS and Linux light pictures of the sweep, whose fonts differ. [GATE-TBD: how
   the gate ran its inspection, if it differed.]
6. After a fix, run both passes again on the new commit and compare the new list with the last
   run's: only the pictures whose bytes changed need inspecting again.
7. At acceptance, by hand: with System chosen, switch Windows' app mode while the app runs (the
   report's checklist; D224).

## What changed in the code

architecture.md describes each part under the section named here; this table says where to start.

| To do this | Start here |
|---|---|
| Workspaces and `workspaces.json` | Core `Workspaces/` (`Workspace.cs`, `WorkspacePaths.cs` with `Same`, the one comparison of folders, `WorkspaceEdits.cs`, `RepositoryScanner.cs`, `IWorkspaceStore.cs`); `Services/JsonWorkspaceStore.cs`; `ViewModels/Workspaces/WorkspaceCommands.cs`, `WorkspaceFolderProbe.cs`, `WorkspaceMembership.cs`, `WorkspaceItems.cs`; the dialogs `WorkspaceNameDialogViewModel.cs` and `AddRepositoriesDialogViewModel.cs`; `Menus/WorkspaceMenus.cs`; the welcome page's section in `WelcomeViewModel.cs` and `Views/Shell/WelcomeView.axaml`; the session's dashboards in `MainWindowViewModel.Session.cs` (`TabState.WorkspaceId`). Section "Workspaces" |
| The dashboard tab | `ViewModels/Workspaces/DashboardViewModel.cs`, `DashboardRowViewModel.cs`, `DashboardTexts.cs`, `Menus/DashboardMenus.cs`; `Views/Workspaces/DashboardView.axaml(.cs)`, `DashboardColumns.cs`, `DashboardRowPanel.cs`; `MainWindowViewModel.Workspaces.cs` (`ShowWorkspace`, the window's `IWorkspaceHost`); `RepoTabViewModel`'s dashboard members; the strip's `Views/Shell/RepoTabStripPanel.cs`. Section "The dashboard tab" |
| Summary mode, the registry and adoption | `RepositoryViewModel.ForSummary`, `IsSummary` and `Promote(Stopwatch)` (`RepositoryViewModel.cs`); `ViewModels/Workspaces/OpenRepositories.cs` (`AcquireSummaryAsync`, `ReleaseSummary`, `TryAdopt`, `RegisterTab`, `WhenIdleAsync`, `NetworkQueueOn`), the session's one as `TabServices.OpenRepositories`. Section "Summary mode, the registry and adoption" under "Repository tabs (D45)" |
| Bulk fetch and pull | `ViewModels/Workspaces/BulkRun.cs` (`StopStarting`), `BulkSlots.cs`, `DashboardBulkViewModel.cs` (`IStatusBarOperation`, `IDashboardToolbar`), `ToolbarViewModel.Dashboard.cs` (`ForDashboard`), `ViewModels/Operations/DeferredDialog.cs` (`BulkPart`), `RepositoryOperations.FetchPull.cs`, `OperationTexts.Bulk.cs`, `RunOptions.Cancellation` (`RepositoryOperations.Run.cs`); Core's `OperationQueue.Cancel(GitOperation)` and `Operations/PromptGroup.cs`; `Services/PromptDialogs.cs` (the group's turn and kept answers); `AskPassBroker.MaxConnections` 16. Section "Bulk fetch and pull" |
| The command palette | `ViewModels/Commands/CommandPaletteViewModel.cs`, `PaletteEntry.cs`, `PaletteMatcher.cs`, `AppCommands.cs` (each command's state and run), `AppCommands.Palette.cs` (the commands listed); `RepositoryViewModel.Palette.cs` (the items); `MainWindowViewModel.Commands.cs`; `Views/Commands/CommandPaletteView.axaml(.cs)`. Section "The command palette" |
| Keyboard shortcuts | `ViewModels/Commands/AppCommand.cs`, `AppShortcuts.cs` (the fixed table as data), `Shortcut.cs`, `ShortcutText.cs` (every key text, Apple's words on macOS), `KeyTexts.cs` (`WithKey`), `ShellKeys.cs`, `ShellToolTips.cs`, `IWindowKeyActions.cs`; `Views/KeyRouter.cs`, `ShortcutKeys.cs` (`CommandModifierOf`, `CommandKeyOf`), `KeyRegions.cs` (F6's regions), `MainWindow.Keys.cs`. Section "Keyboard shortcuts" |
| The settings screen | `ViewModels/Dialogs/SettingsDialogViewModel.cs`; `ViewModels/Settings/GeneralSettingsViewModel.cs`, `GitSettingsViewModel.cs`, `ToolSettingsViewModel.cs`; `Views/Dialogs/SettingsDialogView.axaml(.cs)`; `MainWindowViewModel.Settings.cs`; `TabServices.OpenSettings` and `SettingsSection`; the new `AppSettings` members (`Theme`'s System, `KeepIgnoredFiles`, `Editor`, `ExtraRunnableExtensions`); `OperationTexts.Settings.cs`. Section "The settings screen" |
| Git's own settings | Core `Git/GitConfiguration.cs` (`IGitConfiguration` and its records), `GitBoolean.cs`, `PullModes.cs`, `PullConfiguration.cs`, `GitConfigLocations.cs`; Git `GitConfiguration.cs` (the reads, `WriteGlobalAsync`, the merge tools kept in `merge-tools.json`), `GitMergeTools.cs`, `GitRepository.Config.cs` (`WriteConfigAsync`), `GitRepository.PullConfig.cs`; `RepositoryOperations.Settings.cs` (`SaveRepositorySettingsAsync`); `TabServices.GitSettingsChanged`. Section "Git's own settings" |
| Open in editor and Open file | `Services/KnownEditors.cs`, `EditorCatalog.cs`, `EditorCommands.cs`, `IFileLauncher.OpenInEditorAsync`; Core `ToolCatalogues.cs`; `RepositoryOperations.Files.cs`, `RepositoryViewModel.Settings.cs`, `RunnableExtensionText.cs`. Section "Open in editor and Open file" |
| Fetch refspecs | Core `Git/FetchRefspecs.cs` (`SourceOf`, `DestinationsOf`), `RepoRefs.Refspecs` and `RepoRefs.LocalNameOf`, `Git/RemoteBranchNames.cs`; Git `GitRepository.Refs.cs` (one `git config --null --get-regexp ^remote\.` in place of `git remote`; `ReadFetchRefspecsAsync` for the push guard); `RepositoryOperations.RemoteBranches.cs`, `RepositoryOperations.Remotes.cs` (`RefuseIfPushStopsFetchAsync`, the push guard), `OperationTexts.Menus.Refspecs.cs` (R1 to R4, `CreatedWithoutUpstream`, `PushWouldStopFetch`), `RepositoryMenuContext.cs` (`DropFacts` without its old flag). "Phase 6's repository members" under "Git layer" |
| Keep ignored files and the ignored-files guard | Git `GitRepository.KeepIgnored.cs` (the five `FindIgnoredInTheWayOf…Async` reads), `DirectoryRenames.cs`, `GitRepository.Branches.cs` (`keepIgnored` as `--no-overwrite-ignore`, `noTrack`); `RepositoryOperations.Protect.cs` (`KeepIgnored`, `CheckoutRefusalDisplayAsync`, `RefuseIfIgnoredInTheWayAsync`). "Keep ignored files" and "The ignored-files guard" under "Protected changes and the automatic stash" |
| The theme following the system | `Services/SystemTheme.cs` (`ISystemTheme`, `PlatformSystemTheme`), `Services/ThemeService.cs`, `MainWindowViewModel.Theme.cs` (`UseTheme(AppTheme)`, the one way to change the theme), `Theme/Tokens.axaml` (the light tokens and lanes, `VcDangerTextBrush`), `Theme/Controls.axaml`, `Views/Shell/DragLayer.cs`. Section "Theme" |
| Long folders, and the app's own files | `Controls/PathTextBlock.cs` (the welcome page's errors and the scan dialog break only after a separator); Core `DurableFile.cs` (`settings.json`, `session.json`, `workspaces.json` and `merge-tools.json` flushed to disk before the rename, D236) |
| The session's start and git's environment | `AppSession.Start(…, gitEnvironment, systemTheme, tools, readEnvironment, readClock)` ("Start-up and composition"); `GitRunner`'s `DefaultEnvironment`, which drops the repository-selecting variables (D233), `SessionEnvironment` and `GitCommand.BelowNormalPriority` |
| A visual check of phase 6 | `tests/VisualCommit.VisualTests.Phase6/Phase6Checks.<part>*.cs` with `Phase6Expectations.<part>.cs` (parts W, K, S, G), the same for part L in `tests/VisualCommit.VisualTests.Phase6Look`; the base and support `Phase6Checks.cs` and `Phase6Checks.Support.cs` (`OpenWorkspaceAsync`, `PrepareSameFolderAsync`, `ShotInBothThemesAsync`); the drivers `ShellDriver.Phase6.*.cs` (Workspaces, Dashboard, Bulk, Palette, Keys, Settings, Theme, TabStrip); the scenarios in `tests/VisualCommit.Testing/Scenarios.Phase6.cs` (`WorkspaceAsync`, `ServeWorkspaceAsync`, `RenamingRefspecAsync`, `IgnoredAsync`, `GitSettingsAsync`), pinned by `Phase6ScenarioTests` |

The test helpers the next phase will use (architecture.md's "Tests" table describes each):
`TempWorkspace` (the workspace "Web", ten repositories, copied with no git process) and
`LeanRepositories` (the lean builder behind it, internal to `VisualCommit.Testing`); `FetchHold`
(holds chosen repositories' fetches from file remotes until released; a held fetch gives up after
120 rounds of `sleep 0.5`, 89 s measured under load, so release within about 60 s);
`AuthRemoteServer`'s folder mode (`StartForFolderAsync`, a hold per repository with
`HoldNextFetchPack(repository, atPercent)`, `HoldNextRequest`), on 47393
(`Phase6VisualChecksPort`) for phase 6's walk-through and the real window and 47394
(`Phase6LookVisualChecksPort`) for `Phase6Look`, beside phase 3's 47391 and phase 1's 47392;
`GitIsolation.SessionGitEnvironment` (a session's own global git configuration) and the tripwire;
`FakeSystemTheme`, `HeldMergeTools`, `DrawnLines` (`tests/VisualCommit.Testing/Headless`);
`Contrast` and `TextInk` (a small text's colour from a picture); `ShellDriver.Phase6.*` with
`WaitForDashboardReadAsync`, `CallMark` and `CallsSince`, `PressShortcut`, `PressPalette`,
`SetSystemThemeAsync` and `WaitForWatchersQuietAsync` (in `.Phase3.cs`); in `VisualCommit.App.Tests`,
`WorkspaceTestSupport.cs` (a session's services on a workspace store in memory with real git and a
fake folder dialog; WP-W added it), `DashboardTestSupport`, `BulkTestSupport`, `KeysTestSupport`,
`ThreadWork` and `GrowthMeasures` (with `GrowthCollection`).

## Patterns to follow

Phase 5's patterns still hold (its handoff's table: an action recorded for undo, a page in place of
the graph, rows that git streams, a commit kept only by a reflog, text that may be cut, a refusal
that must change nothing, fixed clocks), and those of phases 3 and 4 before them. Phase 6 adds:

| For | Example |
|---|---|
| A command with a key, in the palette, the menus and the tooltips | `AppCommand.Fetch`: its key in `AppShortcuts.Bindings` (only from the keys D213 leaves free; Mod+P stays free, D191), its state, reason and run in `AppCommands.Resolve` (so a key and its palette entry are enabled together), its palette entry in `AppCommands.Palette.cs`, its tooltip through `KeyTexts.WithKey` (an enabled button names its key, a disabled one its reason alone, D215), and the menu items that run it on the same object passed `command: AppCommand.Fetch` in `RepositoryMenus.Item`, which draws the key column; on a dashboard the same key runs Fetch all. Tests: `AppShortcutsTests`, `KeyRouterTests`, `PaletteEntriesTests`, `ToolbarToolTipTests`, `RepositoryMenusKeyTests` |
| A setting that is git's own | The pull mode in `GitSettingsViewModel`: read from `IGitConfiguration.ReadAsync` (one `git config --show-scope --show-origin -z --list`, in the data folder for the global scope, in the working tree for the repository's) and parsed by Core's `PullModes.Read`; only changed keys written (`PullModes.Writes`), the global ones through `IGitConfiguration.WriteGlobalAsync` one at a time, the repository's as one operation of its queue (`RepositoryOperations.SaveRepositorySettingsAsync` → `IGitRepository.WriteConfigAsync`); every save read back, the open tabs told (`TabServices.RaiseGitSettingsChanged`). Never `git config --global --get` or `--global --list`: inside a repository a global value can be an `includeIf`'s (D217). Tests: `GitSettingsViewModelTests`, `PullModesTests`, `GitConfigWriteTests`, `RepositoryOperationsSettingsTests` |
| A setting of the app's own that an operation follows | `AppSettings.KeepIgnoredFiles`, edited in `GeneralSettingsViewModel` and read by `RepositoryOperations.KeepIgnored` inside each operation when it runs (D124), never cached |
| A dashboard row's read in summary mode | A row whose worktree a tab has follows that tab (`OpenRepositories.TabFor`) and is never read; otherwise `DashboardRowViewModel.RunReadAsync`, off the UI thread, takes from `OpenRepositories.AcquireSummaryAsync` a `RepositoryViewModel.ForSummary` (refs and status as a tab reads them, no watcher, history or Q1 line), shared by every dashboard that holds the row; a git call still running after `DashboardViewModel.GitCallPatience` (10 s on the read clock) no longer holds the pass. Tests: `SummaryModeTests`, `DashboardRefreshTests`, `OpenRepositoriesTests`, `PromotionTests`; checks wait with `ShellDriver.WaitForDashboardReadAsync` |
| A check the app makes before git, inside the operation | `RefuseIfIgnoredInTheWayAsync` (`RepositoryOperations.Protect.cs`) with a read from `GitRepository.KeepIgnored.cs`: it runs inside the operation before any write, refuses with the operation's own title and changes nothing (`TempRepo.ExactStateAsync` before and after) |
| What the machine has, behind a seam | `ToolCatalogues` (`ToolCatalogues.Fixed` in both harnesses, `VISUALCOMMIT_TOOL_CATALOGUES=fixed` in the real window), `ISystemTheme` with `FakeSystemTheme`, `AppSession.Start`'s `gitEnvironment` with `GitIsolation.SessionGitEnvironment`: no picture shows the machine's editors, merge tools, `PATHEXT` or theme, and no test writes the owner's configuration (D220) |
| A part of the walk-through in a project of its own | `tests/VisualCommit.VisualTests.Phase6Look/VisualCommit.VisualTests.Phase6Look.csproj`: it links the other project's `Phase6Checks.cs`, `Phase6Checks.Support.cs` and `Phase6Expectations.Common.cs`, saves into phase 6's one folder, and has a test server port of its own (47394); its line in `VisualCommit.slnx`, the timing script's route, CLAUDE.md's row and the real-window link pattern `Phase6*` go with it. Split past about 40 methods (D230) |
| A network operation held at a known point in a check | `AuthRemoteServer.StartForFolderAsync` and `HoldNextFetchPack(repository, atPercent)` (W4's three percentages), or `FetchHold` for a file remote (W5); always released in a `finally` |
| A test of how work grows | `PaletteGrowthTests` and `DiffTextViewGrowthTests`: ratios of `ThreadWork` (the thread's own work, not a stopwatch) between two sizes, each the best of three, interleaved, inside `GrowthMeasures.WithoutCollection`, in `GrowthCollection` (alone in the process); the stopwatch only for a gross ceiling |

Rules worth keeping (phases 3 to 5's still hold):

- **Compare paths as git spells them.** macOS's temporary folders are `/var/…`, which git reports
  as `/private/var/…`, and CI's Windows runner has short names (`RUNNER~1`): every folder match goes
  through `WorkspacePaths.Same`, and part W's checks prepare git's spelling first
  (`PrepareSameFolderAsync` before `SameFolder`).
- **After the app writes into `.git`, wait for the watchers before reading the calls.** On macOS
  the watcher reports the app's own `git config --local` after its operation, so the refresh it
  starts can land in a later step (part S's read-backs, `WaitForWatchersQuietAsync`).
- **A behaviour that depends on git's version is tested by parity with the git that runs, or by
  recorded outputs**, and a state a newer git refuses to make (`git remote add team/fork` beside
  `team` on 2.55 and 2.56) is written as configuration, as an older git or a hand edit leaves it.
- **A small text is judged by its ink**, not by a pixel of exactly its colour: subpixel smoothing
  and FreeType's LCD filter rarely draw one (`TextInk`, within 8 per channel).
- **A wait under load waits for the state it needs.** A search's wrapping move waits for git to have
  searched the rows it crosses (phase 5 design 3.1), so a test of its key waits for the selection,
  not for a count of presses.
- **One owner per file and per test class while packages are built side by side** (design 18),
  shared lists passing from one package to the next in merge order.

## Deviations from the plan

Phase 6's decisions are D187 to D236. The owner's answers (D187 to D194 at the start, D195 to D202
after the design's reviews) are the plan as the phase settled it, and plan.md and requirements.md
carry them (`890408b`, `290353b`). What departs from or adds to the plan, and the earlier decisions
it amends:

| What | Decision |
|---|---|
| Keep ignored files covers every merge, rebase, cherry-pick, revert and stash apply or pop besides the checkouts, by git's `--no-overwrite-ignore` where it is exact (checkouts, fast-forwards) and by the app's own check before git elsewhere; not the hard reset, whose snapshot keeps what it overwrites | D196 (the owner's answer, settling the design's open part), D221 |
| Turning the setting off restores phase 5 exactly: D179's checkout of an older commit of the current branch and undo's guards keep ignored files whatever the setting. Read from D193's "checkouts then follow git's own default" | D221 (amends D193) |
| A ref one remote fetches from two refs (GitHub's `refs/pull/*/head` line beside the default) checks out into a branch without an upstream; push and delete onto it stay disabled | D201 (the owner's answer; amends D93) |
| The fetch refspecs are read and mapped back as git does; `DropFacts.RemoteMapsBranchesDifferently` is removed, not set as the plan said; R1 to R4 name why an item is disabled; a push that would stop every fetch of its remote is refused before git | D222 (amends D93) |
| Workspaces' file, folders, the dashboard tab, its layout and its reads: no watcher per row (amends D52 and D71); its toolbar, status bar and activity log (extends D90) | D203 to D208 |
| Summary mode and the registry: a dashboard owns view models without tabs and uses tabs' without owning them | D209 (amends D45) |
| Bulk runs: four slots window-wide, prompt groups, failures kept in their rows (D195 amends D95 for bulk runs) | D210, D211 |
| The palette and the keys: one formatter and a router on the window's tunnelling pass (amends phase 5 design 1.3); an enabled button's tooltip names what it does and its key (amends phase 3 design 4d's "an enabled toolbar button has no tooltip") | D212 to D215 |
| The settings screen and git's own settings: reads that keep conditional includes out of the global fields, writes of changed keys only, read back; Pull reads its configuration before the fetch (amends D84) | D216 to D219 |
| Each app session in both harnesses gets its own git configuration and fixed answers for what the machine has | D220 (amends D112) |
| The theme service, and the real-window pass never changing the operating system's theme: high contrast must be off, and following a change is proven headless and by hand at acceptance | D223, D224 |
| The light text tokens at 4.5:1 and the light lanes 1, 2, 4 and 7 darker | D225 with D198 (amend D48) |
| Open in editor finds editors by a fixed catalogue in their install folders, never by a `PATH` name on Windows | D226 (extends D130) |
| The welcome page's Workspaces section; the tab strip shrinks, then scrolls | D227, D228 |
| The scenarios and the folder server on 47393 (amends D97, as D150 did); two walk-through projects, part L in `Phase6Look` with its server on 47394 (the report's ruling 1 and the lead's ruling of 2026-10-22; 47394 has no entry of its own in decisions.md) | D229, D230 |
| "Pull from {up} (rebase)" reads `pull.ff` before its fetch and refuses a value `git pull --rebase` refuses | D231 (amends D147) |
| CI's Test step 55 minutes and the three platforms' jobs 65 | D232 (amends D184) |
| From the review before the gate: git calls drop the repository-selecting variables; the session's end cancels every open dialog; a ticked Amend remembers its HEAD; the app's files are flushed before their rename | D233 to D236 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| Undo of a remote's removal re-creates refs only under `refs/remotes/<r>/` (`UndoScope.RemoveRemote`'s `RefPrefixes`): refs of a namespace that remote fetches elsewhere (`mirror/*`) do not come back; a fetch brings them back, and the remote's backup keeps their commits | Low; in the backlog | design 10.8 #1 |
| `remote.<r>.mirror = true` makes every push the app makes to that remote fail with `fatal: --mirror can't be combined with refspecs` (Push, the push dialog, tags, deletions, undo's remote steps); the error dialog shows git's words | Low until a report | design 10.8 #2, plan risk 30 |
| A namespace no remote fetches any more (a refspec changed, a remote renamed by hand) still makes a remote row whose Fetch, Rename and Remove fail with git's words; its branches' items say R3 | Low | design 10.8 #3 |
| A short source (`branch.<b>.merge = main`) disables that branch's Push and Force push with R4 | Rare | design 10.8 #4 |
| The push dialog's "Set as upstream" to a branch the remote's lines do not fetch: git writes the keys and says so, while the app shows no upstream | Low | design 10.8 #5 |
| A `remote-branch-…` backup phase 5 saved before a deletion refused under a renaming refspec carries the tracking name; its "Push … back" would create that wrong name on the remote, until the clean-up removes the backup (30 days or more than 50 backups, D139; longer while an undo entry needs it, D171) | Low, and it ages out | design 10.8 #6 |
| A mirror fetch line `+refs/*:refs/*` on a remote beside origin's default line makes every origin tracking ref fetched by two remotes (R1): their checkout, the drop's pushes and the deletion are disabled, and "Set upstream…" leaves them out | Low | design 10.8 #7 |
| The push guard refuses some pushes that would not stop the fetch (a new branch `pr/fix-typo` under GitHub's pull-request line; any new branch under two pattern lines of one remote that rename into two namespaces); kept on purpose, a terminal push still works | Low; phase 7's pull-request work meets it | design 10.8 #8, 10.4 |
| With a mirror line before a renaming line, git names a branch's upstream `main` and the app `origin/main` (GD15's fallback) | Cosmetic | design 10.8 #9 |
| A fetch line that writes into `refs/heads/` (`git remote add --mirror=fetch`) overwrites local branches on every fetch, Fetch all and auto-fetch included, as `git fetch` does, and no backup keeps a local commit it drops. `FetchRefspecs.DestinationsOf` finds such lines; nothing skips or backs them up yet | Medium where such a line exists: work can be lost as on the command line. Not yet in plan.md's backlog | design 10.8 #10 |
| Fork-point gaps of the ignored-files check: in fork-point mode git also rewrites a branch that contains its upstream when the upstream was moved back since, where the check's reads say "up to date" for a linear branch, so an ignored file the old upstream tracks can be overwritten as git would; and git's own range can reset to other commits than the check's boundaries | Low; plan risk 31 | design 9.6 |
| A plain rebase whose range holds a merge is refused when a boundary commit tracks a path that is an ignored file on disk, where git without `rebase.rebaseMerges` would keep the file | Low, the safe side | design 9.6, plan risk 31 |
| Directory renames: the check reads `git diff-tree -M` for a rename that would write an ignored file under a renamed folder, but not for a folder renamed into the root from deeper than the top level, a side the disk does not show (the branch of "Check out and rebase"), or the interactive rebase and the squash; a refusal's count can leave out rename paths; on the safe side, Git 2.36's stash apply is refused where git keeps the file, and ties count every candidate | Low | design 9.6 ("Directory renames") |
| Nothing is checked again on Continue, Skip or Abort: an ignored file made during a stop at a path a later pick writes, commits added at an `edit` stop, a Skip (a reset to HEAD) and an Abort (the original branch's files written back) are not covered | Low | design 9.14 |
| Between the app's `check-ignore` and git's write lies one process start (40 to 300 ms on the laptop): a file another program makes there then is overwritten as git would (but in a fast-forward, which git's option refuses), and one removed then makes a refusal for nothing | Low; plan risk 31 | design 9.12 |
| Git 2.30 to 2.33's rules for ignored files were not tried | For phase 8's supported versions | design 0.4, plan risk 31 |
| A run's prompt group reuses an answer by its prompt text across the run's repositories (a Cancel included) until the run ends: git's text names the URL and the user, so the same text asks for the same credential, but two repositories of one host signed in as different users through one prompt text would get the first answer | Low; phase 7's accounts make it likelier | design 4.4 |
| A Fetch, Pull or Push the user starts in a tab, or a row's own, while another worktree of the repository (or a second tab on the same worktree) fetches, is not kept apart: one ends "Fetch failed" with git's lines; nothing is left behind and the next fetch works | Low | design 0.2, 4.3, plan risk 33 |
| Under the owner's `core.fsmonitor=true` every repository a dashboard reads starts a git fsmonitor daemon; accepted (Q5) | They pile up on the development machine; stop them before a gate | design 0.1, 2.10, D207 |
| Under `GIT_OPTIONAL_LOCKS=0` (D47) a stale index makes every status read hash the touched files again (3.1 s per read in a 20,000-file tree), for the dashboard and the tabs alike | When users report slow reads | design 0.1, plan risk 29 |
| A phase 5 build sets aside a `settings.json` holding `"theme": "System"` as `settings.unreadable.json` and starts with the defaults | Accepted: nothing is released before phase 8 | design 7.10, 11.2 |
| After a failed global write of the settings screen the repository's writes are not made, and that scope's unsaved edits are gone without a word of their own (the read back shows what git holds) | Low; accepted by WP-SET's review | design 7.8 |
| The palette lists `Open repository` twice for a folder that a hand-written `workspaces.json` saves through a link (macOS's `/var` for git's `/private/var`) while that workspace's dashboard is closed; the app itself always saves git's spelling | Low | design 1.2, 5.12 |
| On a Mac keyboard F6, Shift+F6 and Shift+F10 need Fn unless the function keys are set as standard keys, Home, End, PageUp and PageDown are Fn with the arrows, and the dashboard's Remove is Cmd+Backspace; IBus and Fcitx take Ctrl+Shift+E on Linux; the keys rely on Avalonia details read from its IL only | Not checked on a Mac or a Linux desktop | design 6.1, 2.7, 0.7, plan risk 22 |
| Linux without a desktop portal: System means Light; the macOS theme observer and the macOS and Linux editor install paths are unproven | Phase 8 by hand | D223, design 11.9, 12.2 |
| Every text box's placeholder (the `Filter` boxes since phase 1, the palette's in `l31c`, the settings screen's) is drawn in `VcTextSecondaryBrush` at Fluent's watermark opacity, 0.5: `#616773` on `#272C36` dark (about 2.5:1), `#ADB2B8` on `#FFFFFF` light (about 2.1:1, lighter than the disabled colour). Found at the gate's inspection; not changed, as it would change nearly every picture with a text box | Accessibility; moved to phase 8's accessibility pass, for the owner to confirm (Waiting on the owner) | plan.md phase 8; the report's part L "Colours" convention and L31 |
| The activity log draws a read whose exit 1 is git's normal answer as a failure: phase 5's rule in `ActivityLogRows` draws every exit code other than 0 in the danger colour, which since phase 6 includes the refs read's `git config --null --get-regexp ^remote\.` in a repository without a remote (phase 5's `a2d-show-reads`), as for `git symbolic-ref -q HEAD` on a detached HEAD and a `git config --get` of an unset key | Cosmetic, with "Show reads" on; in plan.md's backlog | `ViewModels/Panels/ActivityLogRows.cs`; design 10.2 |
| The run time: [GATE-TBD: the default run's and the real-window pass's times at the gate]; during the build CI's Windows Test step reached 29 min 0 s (run 39893475197) and macOS's passed 32 minutes (run 39943973579, before D232's 55) | Every full run, the gate and CI | plan risks 8, 12, 18; D232, D134 |
| Q1: [GATE-TBD: Q1 within the gate's pass, and how much remains below 2,000 ms] | A hot laptop fails a gate by itself | plan risk 10 |
| The comment at the top of `tests/VisualCommit.RealWindowTests/VisualCommit.RealWindowTests.csproj` gives phase 6's part as design 17.6's estimate, "about 5 to 8 minutes more" | A stale comment, unless the gate updated it: [GATE-TBD: updated or not] | fix with phase 7's first code commit |
| `scripts/time-scripted-check.sh` takes phase 6 when no phase is given, and sends a phase above 6 to `tests/VisualCommit.VisualTests.Phase<N>` | Right for phase 7 once its project exists; the default changes with it | `scripts/time-scripted-check.sh` |
| Phase 5's other known issues are not known to be fixed: its handoff's table, but for the seven local fetch and pull failures (explained as plan risk 28), the csproj's stale comment and the timing script, which phase 6's step 4 fixed; and phase 4's and earlier ones | As there | [phase 5's handoff](phase-5.md) |

## Things that cost time

- **CI's gits differ from the laptop's**: 2.36 locally, 2.43 in the containers, 2.55 on
  `ubuntu-latest`, 2.56 on Windows and macOS, and CI is the only check on 2.43 and 2.56 (plan risk
  26). What they did in phase 6: Git 2.56 words an orphaned worktree's `.git` file "gitfile does not
  point to a valid repository", which `OpenAsync` now takes as no repository (run 39831538867,
  `431fb42`); 2.56's push to a server by its path writes tracking refs that a parity test relied on
  being absent (run 39207577529, `6c7f37c`); 2.56's `blame --abbrev` prints longer hashes, so the
  tests read `blame -l` (run 39950853606); Git 2.55 and 2.56 refuse `git remote add team/fork` beside
  a remote `team` ("is a subset of existing remote"), so the test writes the remote as configuration
  (run 40004153661, `6fe269c`); Git 2.48 and later create a symbolic `refs/remotes/origin/HEAD` from
  the server's HEAD, which `FetchRefspecsParityTests` leaves out. Check a 2.56 behaviour locally
  before pushing: put MinGit 2.56.0.windows.2's folder first on `PATH` (the lead's copy lies in a
  Claude session's scratch folder under `%TEMP%\claude\` and may be gone; Git for Windows' releases
  carry it). A test alias named `shell` failed where git-shell is installed, `shell` being a git
  command there (run 39153491762, `717bd8a`).
- **macOS.** Its temporary folders are reached through `/var`, which git spells `/private/var`: the
  palette's entries and K10 failed until paths were compared as git spells them (runs 39936260636,
  39958314029, `c6e1cb2`). Its watcher reports the app's own `.git/config` write late, so part S's
  read-backs counted a refresh as the screen's reads ("13 calls, not 7") in three runs (39948457690,
  39997105336, 40004153661) until the read-backs waited for every watcher to be quiet (`da54d8b`).
  Earlier, a status read between a click's press and release lost the click (run 39094444954;
  `43751d0` keeps the rows a read did not change), and a wait for a layout between a double-click's
  two presses split it (run 39837374552, `855ef39`).
- **Linux's FreeType** draws L28's `=>` with too thin an ink for the ink test (run 39950853606);
  small text is judged by `TextInk`, never by an exact pixel.
- **Timing tests under load.** A ratio of stopwatch times grew with garbage collections and the
  other projects' load: the palette's growth test failed on Windows CI (run 39936260636: 29 times
  for ten times the items) and the diff view's on macOS (689 ms against 27 ms). They now compare
  `ThreadWork` inside `GrowthMeasures.WithoutCollection`, alone in `GrowthCollection`.
  `GitRunnerPriorityTests` assumed git's tree starts at normal priority, while CI's runner runs the
  test host below normal (run 39190992859, `e2fb327`). `KeyRouterTests` failed twice on Windows CI
  with the selections 5, 5, 4, 4 (runs 39962856421, 39980785501): a search's wrapping move waits for
  git to have searched the rows it crosses, and the test now waits for it.
- **A newer push cancels the running CI run**: run 39943973579 was cancelled at 33 minutes with
  macOS's Test step past D186's threshold (hence D232), and 40009544956 by the push after it. Push
  code commits that follow each other together, and read the newer run.
- **Two `dotnet test` commands for phase 6's two projects at once** each empty phase 6's picture
  folder: run them one after the other or by one `dotnet test` at the root.
- **Load from builders beside a run** stretched it (60 min 48 s and 68 min 59 s against 43 min 33 s
  rested) and pushed `ShellDriverTests`' fresh-application case past its 60 s (`8c7401b` made its wait
  a turn off the watchdog). Measure only on a rested, otherwise idle laptop.
- **Disk.** A built worktree took about 6.4 GB on D:, and at most three were built at once; D: had
  20 GB free at the phase's start and 18.7 GB on 2026-10-23, C: 15.7 GB at the start. Remove a
  merged worktree at once and stop its fsmonitor daemon (plan risk 27).
- **Never write the owner's `~/.gitconfig`.** For an experiment with git's configuration, set
  `GIT_CONFIG_NOSYSTEM=1` and `GIT_CONFIG_GLOBAL` to an empty file of the experiment's own; the
  tests' tripwires fail when a global write escapes a session.
- **Visual Studio's Git 2.55** lacks `git http-backend` and `sleep`: with it first on `PATH` the
  folder server fails at its start with a message naming the missing `http-backend`, and the fetch
  hold and phase 5's waiting merge tool do not hold (plan risk 28).
- **`git mergetool --tool-help`** takes 20 to 60 s alone and over 180 s under the default run's
  load; its wait has a limit of its own (`WaitingForSlowProgram(what, limit)`), and the app runs it
  below normal priority once start-up has settled (D202).
- **Shell quirks** (as phases 4 and 5): write long scripts to a file first; `sed -i` drops a CRLF
  file's carriage returns; in a sub-agent's worktree a Bash command that chains several git calls
  with the repository's path ("Visual Git") can be refused as unverifiable, so run them one by one.

## For the next phase

Reuse:

- **The palette and the key table** for every new command (Patterns above): a command listed in
  `AppCommands.Palette.cs` reaches the palette, and every menu item reaches it too through the same
  `RepositoryMenus` builders (D212), so "Create pull request" in `ForLocalBranch`, `ForRemoteBranch`
  and `ForDrop` (plan.md's phase 7) is in the palette with no more code. A key comes only from the
  keys D213 leaves free.
- **The settings screen** for accounts and tokens: a section beside General, Git, Tools and
  Keyboard shortcuts (`SettingsSection`, `SettingsDialogViewModel`), by D216's rules (a dialog of the
  dialog layer, no combo box, long lists in the Pick dialog, changes at Save). Tokens belong in the
  OS keychain (R3), never in `settings.json`; give the keychain a seam as `ISystemTheme` and
  `ToolCatalogues` have one, with a fake in both harnesses, so that the default run never touches the
  owner's keychain (CLAUDE.md's rule; D19, D220).
- **The dashboard rows** (`DashboardRowViewModel`, `DashboardColumns`) if pull requests show per
  repository; summary mode reads refs and status only, and at 1100 wide no column is spare (D206).
- **The push paths and their guard**: `RepositoryOperations.Remotes.cs` (`PushBranchAsync`,
  `PushBranchToAsync`, `ForcePushAsync`, `DeleteRemoteBranchAsync`, `RefuseIfPushStopsFetchAsync`).
  A pull request's branch is pushed through them.
- **The fetch mapping for pull-request refs**: `FetchRefspecs` (`SourceOf`, `DestinationsOf`),
  `RemoteBranchNames`, `RepoRefs.LocalNameOf` and D201's checkout without an upstream already handle
  GitHub's `+refs/pull/*/head:refs/remotes/origin/pr/*` (R2) and `refs/pull/<n>/head` fetched into a
  remote of its own (R4, G25's `upstream/pr/7`).
- **The credential paths of D82**: git's credential helpers first (Git Credential Manager
  included), then the askpass helper and `AskPassBroker` with the credentials dialog; a bulk run's
  `PromptGroup` asks once per prompt text; `AskPassBroker.MaxConnections` is 16. Auto-fetch never
  prompts (D79, D82, D85).
- **`IFileLauncher`** (`FakeFileLauncher` in tests) opens files and folders and starts an editor;
  "Open in browser" needs a URL method there.
- **`AuthRemoteServer`** for a test git server over HTTP: ports 47391 to 47394 are taken; a new one
  gets a constant beside them, pinned in `AuthRemoteServerTests`, and a decision that amends D97, as
  D150 and D229 did. **`DurableFile`** for any new file in the data folder. **`TempWorkspace`** and
  **`FetchHold`** for many repositories and a held fetch.

Do first:

1. The start-of-session steps; then read phase 7's section of plan.md and its risks 4, 5, 14, 15,
   19 and 26, and 8, 10, 12, 18, 27 and 33; the Known issues above and phase 5's; the latest CI runs
   on `master` (phase 6's merge starts one).
2. Check that no worktree, `wip/*` or `worktree-*` branch or fsmonitor daemon is left (`git worktree
   list`, `git branch --list "wip/*" "worktree-*"`, the daemons as in "Running the visual gate"), and
   that C: and D: have room for the worktrees planned.
3. With the first code commit: `scripts/time-scripted-check.sh`'s default phase 7, and the
   real-window csproj's comment on the pass's length if the gate left design 17.6's estimate there.
4. Write phase 7's checks into `docs/test-reports/phase-7.md` before the UI, deriving every git state
   with real git 2.36 (and 2.55 or 2.56 where words or exit codes may differ), and decide which steps
   the real window repeats. A check cannot sign in to GitHub, Azure DevOps or GitLab from the default
   run or CI: decide early how the providers are stood in for (plan risks 4 and 5). Estimate what the
   checks add to the default run ([GATE-TBD: minutes at phase 6's gate]) and to the real-window pass
   ([GATE-TBD: minutes]) before writing them, and share app starts where scenarios allow.
5. Give phase 7's walk-through a project of its own, as phases 4 to 6 did (D151, D181, D230): its
   csproj, `AssemblyInfo.cs` and support copied from phase 6's, its line in `VisualCommit.slnx` (CI
   picks up every test project of the solution), a `Phase7Expectations*.cs` link in the real-window
   csproj, the timing script and CLAUDE.md's Commands; a second project past about 40 methods.

Open points for phase 7 (record each as a decision; ask the owner those that are a matter of taste,
before the design):

- Sign-in: personal access tokens, which need no app registration, or the providers' own sign-in,
  which needs registrations with GitHub, GitLab and Microsoft (plan risk 5); and the test accounts or
  tokens the checks need (risk 4).
- Where the pull-request list lives: the left panel's "Pull requests" section, which has no menu
  yet (plan.md), a page in place of the graph (`IGraphAreaPage`), or the dashboard.
- How the keychain is reached on each platform, and what the app does where none is available (R3).
- Whether author avatars are fetched from the providers (network calls per author, kept where), and
  what shows without them.

Risks to watch:

- The run time (plan risks 8, 12 and 18): [GATE-TBD: the default run's and the real-window pass's
  minutes at phase 6's gate]; during the build CI's Windows Test step took 16 to 29 minutes and
  macOS's 16 to over 32, against 55 now. The lead raises the limit when the Windows or macOS Test
  step passes 44 minutes and records it as a decision (D232, D186's rule).
- Q1 on a warm laptop (plan risk 10).
- Git keeps changing between versions, and CI's gits are 2.43 to 2.56 (plan risk 26).
- The askpass helper and OpenSSH's `SSH_ASKPASS_REQUIRE` (plan risks 14, 15) and auto-fetch over ssh
  without a timeout (risk 19), which a provider's tokens go through.
- Overlapping fetches of one common git folder (plan risk 33), which a pull request's fetch beside a
  bulk run would meet.
- Disk space with several built worktrees (plan risk 27).

## Waiting on the owner

- **Acceptance of phase 6**: [GATE-TBD: the state and the gate's commit; "Awaiting acceptance" or
  "Blocked" and why].
- **At acceptance, to confirm or answer**:
  - design 17.5's reading of "ten repos can be checked and updated from one screen" (Result above:
    W2 reads the ten rows on one screen, one Fetch all and one Pull all each decide every row);
  - the by-hand step of the report's checklist: with System chosen, switch Windows' app mode while
    the app runs (D224);
  - the move of the text boxes' placeholder contrast to phase 8's accessibility pass (Known issues;
    plan.md phase 8): every placeholder is drawn in `VcTextSecondaryBrush` at Fluent's watermark
    opacity, about 2.5:1 dark and 2.1:1 light, and the lead left it at the gate's inspection, as raising
    it would change nearly every picture with a text box.
- **Decisions made during the phase without the owner**, which the owner may want to look at: the
  design's own, D203 to D230 (after six reviews); D231 (the rebase chosen for one pull reads
  `pull.ff`); D232 (CI's limits 55 and 65 minutes); D233 (git calls drop `GIT_DIR` and the other
  repository-selecting variables); D234 (the session's end cancels every open dialog); D235 (a ticked
  Amend refuses when HEAD moved); D236 (the app's files flushed before their rename); and the lead's
  rulings recorded in the design and the report: part L in a project of its own with its server on
  47394, GD24's drop-push title, GD15's upstream named by its tracking ref and a palette entry
  without a key showing no key column (`k10e`) (the report's rulings of 2026-10-19 and 2026-10-22);
  on 2026-10-23, after the helpers' smoke test and at the review, a dashboard read request judged as
  of the moment it was made and a read whose git call is overdue no longer holding the pass (design
  2.10), a worktree held while any queue on it runs (3.4), the dashboard row's menu showing its keys
  (6.7), the mirror fetch line left for the backlog (10.8 #10) and the palette's `Open repository`
  matching a workspace's folder by its dashboard's reading (1.2).
- **When you have a Mac at hand**: the seven-item checklist at the end of the
  [phase 6 test report](../test-reports/phase-6.md), and those of phases 0 to 5, still open. The
  owner will do them at the end of the project.
- **For phase 7**: test accounts or tokens for GitHub, Azure DevOps and GitLab (plan risk 4), and
  app registrations if the providers' own sign-in is chosen (risk 5).
- **When the project is over**: whether a CI run of 30 minutes or more is reasonable (D134, D152,
  D184, D232). [GATE-TBD: the Windows Test step's time on the gate's commit]; the default run on the
  laptop [GATE-TBD: its time at the gate].
