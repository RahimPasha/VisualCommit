# Phase 1 handoff — Repos and commit graph

Written at the end of phase 1 for the session that runs phase 2. That session has not seen any
of this phase's conversation. How the app is built is in [architecture.md](../architecture.md);
this file says what was proven, what to copy, and what to watch out for.

## Result

Everything in the plan's "Delivers" list was delivered.

| Delivered | Proven by |
|---|---|
| Open, init and clone (with progress) | Visual checks 11 (open, and an error for a plain folder), 12 (init), 13 (clone with progress), 14 (a failed and a cancelled clone); `CloneTests`, `GitRepositoryOpenTests` |
| Recent repos | Visual checks 10 and 11; `RepoTabsTests` (recent list) |
| Several repo tabs, restored on restart | Visual checks 10 and 15 (a real process restart in the real-window pass); `RepoTabsTests` |
| Commit loading streamed in pages | `CommitLogTests` (pages of 100 then 2,000, stashes by date, a slow page handler); visual check 8 (100k commits) |
| Lane layout | `GraphLayoutTests` (the graph scenario's table, a property test over random histories, 100k commits); visual checks 1, 2 and 8 |
| Custom-drawn virtualised graph with ref labels and author, date and SHA columns | Visual checks 1, 2, 8 and 9; `CommitGraphControlTests`, `GraphColumnsTests` |
| Left panel: branches as folders, remotes, tags, stashes; ahead/behind counts; filter box | Visual checks 5, 6 and 7; `LeftPanelViewModelTests`, `PanelViewTests` |
| Commit details panel: message, author, changed-file list (tree or flat) | Visual checks 3 and 4; `CommitDetailsViewModelTests` |
| File watcher that refreshes on outside changes | Visual check 18 (also in the real window); `RepositoryWatcherTests`, `RepositoryViewModelTests` |
| Window size and position and panel widths saved and restored with the tabs | Visual check 16 (also in the real window); `WindowPlacementTests`, `JsonSessionStoreTests` |
| Panels give way in a narrow window | Visual check 17; `PanelLayoutTests`, `WindowPlacementTests` |
| Q1: first graph within about 2 seconds, smooth scrolling | Visual check 9, judged in the real-window pass on the Windows machine (D50); see the report for the numbers |

Not delivered: nothing. Found and moved: ssh's own prompts (host key, key passphrase) are not
yet turned off or shown in the app; phase 3 owns in-app prompts (see "Known issues").

## State of the repo

GATE-STATE

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300; screen 3000×2000 at 200% scaling (1500×952 logical work area). The whole phase was built on this machine; no cloud session worked on it |
| .NET SDK | 10.0.303 (`global.json` accepts any 10.0 feature band) |
| Git | 2.36.0.windows.1 locally; 2.55.0 on the CI runners |
| Packages | Unchanged from phase 0: Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0 |
| Screenshots | Scripted: scaling 1. Real window: 200% |

## Build, run and test

The full table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands);
nothing new was needed.

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
dotnet test --project tests/VisualCommit.VisualTests --filter-class "*Phase1Checks"
dotnet test --project tests/VisualCommit.RealWindowTests -c Release
```

The default run now takes about 1.5 to 2 minutes on the development machine, most of it in the
visual checks that load or clone the 100k-commit repo and in building the graph scenario once
per test process. The first run on a machine also builds the 100k-commit repo (about 5 to 25
seconds, depending on load) into `%TEMP%/VisualCommit.Tests/shared/`, where later runs reuse it.

## Running the visual gate

As in phase 0's handoff, with phase 1's pass added:

1. `dotnet test` at the repo root. The scripted walk-through of both phases writes
   `artifacts/visual/phase-0/scripted/` and `artifacts/visual/phase-1/scripted/`.
2. Tell the owner, then `dotnet test --project tests/VisualCommit.RealWindowTests -c Release`.
   It runs phase 0's and phase 1's passes one after the other (never in parallel). It opens the
   app many times, uses the real mouse, wheel and keyboard, the native folder dialog, and real
   process restarts; it needs an unlocked desktop and the scripted screenshots from step 1.
   Phase 1's pictures and `run.txt` (with the Q1 numbers) land in
   `artifacts/visual/phase-1/real-window/`.
3. Open every picture and compare it with the report. Byte-identical files are opened once.

## What changed in the code

The main entry points the next phase will touch:

| To do this | Start here |
|---|---|
| Add a read of the repository (status, a diff) | `IGitRepository` (Core) and `GitRepository` (Git, split by area: `.Refs.cs`, `.Commits.cs`, `.Clone.cs`); reads run with `GIT_OPTIONAL_LOCKS=0` (`ReadOutputAsync` in `GitRepository.cs`) |
| Show something for the open repository | `RepositoryViewModel` owns everything of one repository; its `Graph`, `LeftPanel` and `Details` are what the regions bind to |
| React to outside changes | `RepositoryViewModel.RefreshAsync` (D52). Phase 2's working-tree changes need the watcher to watch the working tree too: `RepositoryWatcher` takes the folders it watches |
| Put a row at the top of the graph (phase 2's working-changes row) | `CommitGraphData` (what the control draws), `CommitGraphControl` (drawing and input), `GraphLayout` (lanes) |
| Replace the right panel for the working-changes row | `RepoTabViewModel.Details` and `Views/Shell/RightPanelView` |
| A new setting or session value | `AppSettings` (preferences) or `SessionState` (state), each with a default |
| Show a failure to the user | The welcome page (`WelcomeViewModel.ShowError`), the clone form, the details panel; services log and carry on |

## Patterns to follow

| For | Example |
|---|---|
| A git read and its test | `GitRepository.ReadRefsAsync` with `RefReadingTests`: build the repo with `TempRepo` (or `git fast-import` for many commits, see `RepositoryTestSupport`), read it with the real `GitRepository` |
| A view model with real git, no UI | `RepositoryViewModelTests` |
| A view model with fakes | `RepoTabsTests` (fake provider and folder dialog, in-memory session) |
| A custom-drawn control and its test | `CommitGraphControl` and `CommitGraphControlTests` (pixel checks at known positions) |
| A scenario repo | `Scenarios.GraphAsync` (built once per process, copied per test) and its pins in `ScenarioTests` |
| A visual check | `Phase1Checks.Shell.cs` and `Phase1Checks.Graph.cs`: start with `ShellDriver.StartWithTabs`, wait with `WaitForAsync`, screenshot, assert against `Phase1Expectations.cs` |
| A check that crosses a restart | `Check_15_three_tabs_restored_after_a_restart` (two `FreshApplication.RunAsync`) |
| A real-window check | `Phase1RealWindowPass`: launch with a session, wait for a log line, real input, `AssertMatchesScripted` |
| A context-menu action | None yet; phase 3 sets the pattern |

Rules worth keeping:

- Expected values come from the report, never from the app. Measure text with the font the app
  uses (the window's `FontFamily`), not `FontFamily.Default`, or semibold widths come out wrong.
- Small text is anti-aliased: assert its brush, not a pixel. Sample pixels of shapes only, and
  across a ring's width rather than at one spot.
- `RowBounds` of the graph control is already in the control's coordinates, scroll included.
- A path the app shows comes from git and is the physical one (`/private/var` on macOS): compare
  with `git rev-parse --show-toplevel`, not with the path the test created.
- Every git call the app makes in tests is cut off from the machine by the module initializers
  (`GitIsolation`), including `GIT_CEILING_DIRECTORIES` so discovery never leaves the tests' own
  folder.

## Deviations from the plan

| What | Decision |
|---|---|
| Open, clone and init on a welcome page in the graph area | D42 (owner's choice) |
| Absolute dates, in UTC in the tests | D43 (owner's choice) |
| The file list starts flat | D44 (owner's choice) |
| One view model per tab; services reach git through `GitAccess` | D45 |
| `session.json` beside `settings.json` | D46 |
| One `git log --date-order`, pages of 100 then 2,000, stashes by date | D47, D54 |
| Lane rules | D48, D54, D55 |
| One custom-drawn graph control; rows not exposed to UI Automation yet | D49 |
| Q1 measured by the app's own log | D50 |
| The 100k-commit repo in the default run | D51 |
| Watcher on the git folder only | D52 |
| Which placeholders became real | D53 |
| The runner keeps reading output while it still comes | D56 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| ssh's own prompts are not turned off: a clone over ssh from a host not yet in `known_hosts`, or with a key that has a passphrase, can wait for an answer on the terminal the app was started from (macOS and Linux, started from a terminal). Cancel stops it | Low now; phase 3 adds in-app credential and passphrase prompts (C11) and should handle this with them | `GitRepository.Clone.cs`, `GitRunner` |
| Check 13 has to catch the clone between 1% and 99%; a clone of the 100k repo takes about 2 seconds here. A much faster machine could make it flaky | Low; watch CI | `Phase1Checks.Check_13_clone_with_progress` |
| Graph rows are not exposed to UI Automation (screen readers follow the details panel) | Planned for phase 8 (R7, D49) | `CommitGraphControl` |
| The 100k-commit repo stays in `%TEMP%/VisualCommit.Tests/shared/` (about 15 MB) between runs | Intended (D51); delete the folder to rebuild it, or change `LargeHistory.Version` when the generator changes | `LargeHistory` |
| The window has Avalonia's default icon | Cosmetic; phase 8 | |

## Things that cost time

- **Worktrees for parallel agents start from `master`.** The phase was built partly by sub-agents
  in git worktrees (`.claude/worktrees/`, git-ignored). Their worktree was created from `master`,
  not from the phase branch; they must create their branch from `phase/1-graph` first.
- **Two-way bindings and coerced values.** A coerced `ScrollOffset` was written back through the
  binding into the other tab's view model when the view switched tabs; it is clamped when read.
- **The runner's 2-second drain dropped output** when a line handler was slow after git exited
  (D56). Do not hand work that can block to `OnOutputLine` without knowing this.
- **Newer git refuses overlapping remote names** (`team` and `team/fork`) in `remote add`; CI runs
  git 2.55, the machine 2.36. Configure such a remote with `git config` in tests.
- **macOS temp paths**: git reports `/private/var/...`; see "Rules worth keeping".
- **Every git call costs about 100 ms on Windows**, much more under load: build big test repos
  with one `git fast-import`, and build a scenario once per process and copy it.

## For the next phase

Reuse: `IGitRepository`/`GitRepository` for every read, `RepositoryViewModel` as the home of
everything about one repository, `CommitGraphData` and `CommitGraphControl` for the graph,
`Scenarios`, `TempRepo.CopyAsync` and `LargeHistory` for test repos, `ShellDriver` and `RealApp`
for the two passes.

Do first:

1. Read the Known issues above and check the latest CI runs.
2. Decide how the working-changes row sits on top of the graph (a pseudo-row in
   `CommitGraphData` before row 0, with its own drawing) and how the watcher watches the working
   tree (D52 left it to phase 2), including what to ignore (build output, `node_modules`, ignored
   files) so a build does not refresh the app constantly.
3. Build a scenario repo with known working-tree changes (modified, added, deleted, renamed,
   binary, an image, a very large file) and pin it before writing the checks.
4. Write phase 2's checks into `docs/test-reports/phase-2.md` and commit them before the UI.

Risks to watch:

- Phase 2's diff viewer is the largest UI piece so far (risk 2 in `plan.md`). The graph control's
  approach (draw only what is in view, cache laid-out text, measure frame times) applies.
- Writing operations start in phase 2 (stage, commit): the watcher must not refresh the app in a
  loop on its own writes (D52: pause it during a write and refresh once after).

## Waiting on the owner

WAITING
