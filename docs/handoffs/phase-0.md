# Phase 0 handoff — Foundation

Written at the end of phase 0 for the session that runs phase 1. That session has not seen any
of this phase's conversation. What the app looks like in code is in
[architecture.md](../architecture.md); this file says what was proven, what to copy, and what to
watch out for.

## Result

Everything in the plan's "Delivers" list was delivered.

| Delivered | Proven by |
|---|---|
| Solution, projects, `.editorconfig`, `.gitignore`, `.gitattributes`, README, central package versions | The files exist: `VisualCommit.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `README.md`. `dotnet build` at the root succeeds with warnings as errors |
| App shell laid out as in "Main window layout", placeholder content in each region | Visual checks 1 to 4 and 7; `ShellTests` |
| Dark and light themes with a theme switch; colour tokens recorded in `architecture.md` | Visual checks 3 to 5; the token table in `architecture.md`, "Theme" |
| Git runner: locate git, check its version | `GitLocatorTests`, `GitVersionTests` |
| Git runner: run asynchronously, cancel, stream output, record each call | `GitRunnerTests` (19 tests against real git), `GitCallLogTests` |
| Settings store; the chosen theme survives a restart | `JsonSettingsStoreTests`; visual check 6, and the real process restart in check 8 |
| Local log file | `FileAppLogTests`; check 6 counts the start-up lines in the log |
| Test harness: temporary-repo builder | `TempRepoTests`, `ScenarioTests` |
| Test harness: headless UI tests | `ShellTests` in `VisualCommit.App.Tests` |
| Scripted walk-through with screenshots | `Phase0Checks` in `VisualCommit.VisualTests`: checks 1 to 7 |
| Real-window pass, proven on this machine | `Phase0RealWindowPass`: check 8 |
| CI on all three platforms | `.github/workflows/ci.yml`; the runs are listed in `status.md` |
| Commands section of `CLAUDE.md` | The section exists; every command in it was run |

Not delivered: nothing. Two follow-ups were found and are now in phase 1's list in `plan.md`:
saving the window size and panel widths, and making the panels give way in a narrow window.

Beyond the list, the phase added `THIRD-PARTY-NOTICES.md` (for the icon outlines), a hang
watchdog for test runs, and accessible names on the toolbar buttons.

## State of the repo

- Branch `phase/0-foundation`. `status.md` says whether the owner has accepted it and it has
  been merged into `master`.
- Tests: 117 in the default run (65 git, 45 app, 7 scripted visual checks), all passing, in about
  25 seconds. One more in the real-window pass, passing.
- Visual test gate: passed. It ran on commit `cfa3193`, and again on `45f3e85` and `b907071`
  after later fixes, with the same pictures each time.
  Report: [test-reports/phase-0.md](../test-reports/phase-0.md). One part could not run: the real
  window at 1920×1080 does not fit the development screen.
- CI: the last run of the branch and its result on each platform are in `status.md`. Read
  "Known issues" below about the runs that hung on macOS.

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300; screen 3000×2000 at 200% scaling (1500×952 logical work area) |
| .NET SDK | 10.0.303 (`global.json` accepts any 10.0 feature band) |
| Git | 2.36.0.windows.1 locally; 2.55.0 on the CI runners |
| Packages | Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0 |
| Screenshots | Scripted: scaling 1. Real window: 200% |

## Build, run and test

Copied from a terminal where they worked. The full table with notes is the Commands section of
[CLAUDE.md](../../CLAUDE.md#commands).

```
dotnet build
dotnet run --project src/VisualCommit.App
dotnet test
dotnet test --project tests/VisualCommit.Git.Tests
dotnet test --project tests/VisualCommit.Git.Tests --filter-method "*Linear*"
```

Tests run on Microsoft Testing Platform: a project is passed with `--project`, and
`dotnet test <path>` does not work.

## Running the visual gate

1. `dotnet test` at the repo root. About 25 seconds. The scripted walk-through is part of it and
   writes `artifacts/visual/phase-N/scripted/*.png`. The folder is emptied first, so after a run
   of only some tests it holds only their pictures.
2. Tell the owner, then `dotnet test --project tests/VisualCommit.RealWindowTests -c Release`.
   About 30 seconds. It builds the app in Release, opens it on the desktop, moves and clicks the
   real mouse, and writes screenshots, `*-difference.png` pictures and `run.txt` to
   `artifacts/visual/phase-N/real-window/`. It needs an unlocked desktop, and the scripted
   screenshots from step 1.
3. Open every picture and compare it with the expected result in the report. Files that are
   byte-for-byte the same need opening once: group them with `Get-FileHash`. The same holds for
   a re-run after a fix: a picture whose hash did not change does not need opening again.
4. `gh run download <run id> -D <folder>` fetches the scripted screenshots CI took on macOS and
   Linux. Open at least one per platform.

## What changed in the code

Everything is new. The entry points phase 1 will touch:

| To do this | Start here |
|---|---|
| Create a service and hand it to view models | `AppSession.Start` in `src/VisualCommit.App/AppSession.cs` |
| Run a git command | `IGitRunner.RunAsync(new GitCommand(...))`. The runner is `MainWindowViewModel.Git.Runner`, set only after git was found; see the note below |
| Add a setting | `AppSettings` in `src/VisualCommit.Core/Settings/AppSettings.cs`: a new property with a default |
| Fill a region of the window | Its view in `src/VisualCommit.App/Views/Shell/`; give it a view model of its own and set it as the view's data context from `MainWindowViewModel` |
| Add a colour | `src/VisualCommit.App/Theme/Tokens.axaml`, in both themes, and the table in `architecture.md` |
| Add an icon | `src/VisualCommit.App/Theme/Icons.axaml` |
| Enable a toolbar button | `ToolbarView.axaml`: bind `Command`, remove `IsEnabled="False"` |

The runner is not available when `AppSession.Start` creates the services: git is looked for
after the window exists, and it may not be found. A service created in `AppSession.Start` can
therefore not take an `IGitRunner` in its constructor. Phase 1 has to choose how services get
it; passing the pending detection as a `Task<GitDetection>` is the smallest change.

## Patterns to follow

| For | Example |
|---|---|
| A git command and its test | `GitLocator.DetectAsync` runs `git --version` and parses the result; `GitRunnerTests` shows how to test against real git, including slow and failing commands through a one-off shell alias (`Script(...)`) |
| A view and its view model | `Views/Shell/StatusBarView.axaml` bound to `MainWindowViewModel`; `MainWindowViewModelTests` tests the view model with fakes, no UI |
| A headless UI test | `ShellTests`: `ShellDriver.Start(dataFolder)`, `await app.WaitUntilReadyAsync()`, then `app.Click`, `RightClick`, `DoubleClick`, `Drag`, `Type`, `PressKey`, and `app.Find<T>(name)` |
| A scenario repo | `Scenarios.LinearAsync` in `tests/VisualCommit.Testing/Scenarios.cs`, pinned by `ScenarioTests` |
| A visual check | A test in `tests/VisualCommit.VisualTests/Phase0/Phase0Checks.cs`: do the step, `app.Capture().Save(phase, "NNx-what")`, assert the expected result. Expected values are copied from the report into the test, not read from the app |
| A check that crosses a restart | `Check_6_the_theme_survives_a_restart`: a plain `[Fact]` with one `FreshApplication.RunAsync` per app instance |
| A real-window check | `Phase0RealWindowPass`: `new RealWindowRun(phase)`, `RealApp.Launch`, `SetClientSize`, `Find(automationId)`, `ClickWithMouse`, `CaptureClient`, `run.AssertMatchesScripted` |
| A context-menu action | None yet; phase 3 sets the pattern |

Rules worth keeping:

- Give every control the real-window pass must find an `AutomationProperties.AutomationId`; UI
  Automation can find nothing else. Headless tests also find parts of a view by `Name`.
- Assert `LayoutAudit.FindClippedText(window)` is empty in every visual check. It catches text
  that is cut off or shortened, which is easy to miss in a screenshot.
- Sample colours only at spots that are certainly empty. A sample near text hits the coloured
  edge of a letter.
- Build test repos only with `TempRepo`. It isolates its git calls from the machine's
  configuration and keeps commit SHAs the same everywhere, so screenshots can show them.
- The app under test is isolated separately: `HeadlessTestApp` and `RealApp.Launch` point git's
  system and user configuration at an empty file for the app's own git calls. They do not set an
  author identity. A check in which the app itself commits needs one in the scenario repo's own
  configuration.

## What the test harnesses cannot do yet

Phase 1's checks need more than phase 0 built. Plan this work before writing the checks:

| Missing | Where |
|---|---|
| Starting the app with a repository already open. Neither harness can; phase 1 decides the way (a start-up argument, or saved session state written by the test) | `ShellDriver.Start`, `RealApp.Launch` |
| Mouse-wheel scrolling and "wait until something is true" in the headless driver | `ShellDriver` |
| Right-click, double-click and typing in the real window. FlaUI has them (`Mouse.RightClick`, `Mouse.DoubleClick`, `Keyboard.Type`); they are not wrapped or proven | `RealApp` |
| Scenario repos in the real-window pass. Its project does not reference `VisualCommit.Testing` | `VisualCommit.RealWindowTests.csproj` |
| Helpers for stashes, remotes and clones in the repo builder; they go through `TempRepo.GitAsync` until someone adds them | `TempRepo` |

## Deviations from the plan

| What | Decision |
|---|---|
| xunit.v3 is 3.2.2, not the latest 4.0.1; tests run on Microsoft Testing Platform | D24 |
| The real-window pass is excluded by leaving its project out of the solution | D25 |
| Own file logger, no logging library | D26 |
| Icon outlines come from Lucide | D27 |
| A "restart" in the scripted walk-through is a new application object, not a new process | D32 |
| "Matches" for real-window screenshots has a number: at most 3% of pixels differ | D33 |
| CI skips pushes that change only docs | D34 |
| Test runs have a hang watchdog | D35 |
| On macOS and Linux a cancelled git call stops git itself, not what git started | D37 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| The test step froze the whole CI job on macOS in 4 of 9 runs (among them runs 37436976773, 37438530778, 37442079641 and 37443703094). The runner stopped enforcing time limits and kept no log, and a watcher process started beside the tests froze too, so nothing could be read from a frozen job. The freezes stopped when the git runner stopped using .NET's process-tree kill on macOS and Linux (D37): 12 test steps in a row then ran clean. How the tree kill froze the job was not established; it suspends processes while it walks the process list. The freeze never happened when the tests wrote to a file instead of the step's output, in 42 runs | Medium. Watch the macOS job in the first CI runs of phase 1; `status.md` lists the runs since the fix. Do not bring `Process.Kill(entireProcessTree: true)` back on macOS or Linux, in the app or in tests. On those platforms a helper that git started can outlive a cancelled call briefly; phase 3 should check cancel of a network operation on a Mac | `GitRunner.Kill`, `.github/workflows/ci.yml` |
| The real window at 1920×1080 is unproven: the development screen is too small | Low: the scripted walk-through covers the size, and the pass takes that screenshot by itself on a larger screen | `Phase0RealWindowPass` |
| The window's size and position and the panel widths are not saved | Low; moved to phase 1 | `MainWindow.axaml` |
| A panel widened in a large window is cut off when the window is then made small | Low; moved to phase 1 | `MainWindow.axaml`, the `MainArea` grid |
| The window has Avalonia's default icon | Cosmetic; phase 8 does icons and branding | |
| `Program.Main`'s crash logging has no test | Low | `src/VisualCommit.App/Program.cs` |
| When git is missing or too old the status bar says so, and nothing else happens | Fine for now; phase 8 adds guidance (R4). Phase 1 must not assume `MainWindowViewModel.Git.Runner` is set | `MainWindowViewModel` |

## Things that cost time

- **Avalonia 12, not 11.** Avalonia.Diagnostics has no version 12 (the new developer tools are a
  separate package and were left out). `TextBox.Watermark` is now `PlaceholderText`,
  `Bitmap.Save(path)` needs encoder options, and headless mode needs `.UseSkia().UseHarfBuzz()`
  and `UseHeadlessDrawing = false` to render real pixels.
- **xunit.v3 4.x and Avalonia.Headless.XUnit do not work together.** Tests are not discovered
  ("Method not found ... GetTestCaseDetails"). Stay on 3.2.2 (D24).
- **A plain `[Fact]` must never await `HeadlessUnitTestSession.Dispatch` directly.** The test
  continues on the headless UI thread, and the next UI test then waits on that same thread for
  ever. Use `FreshApplication.RunAsync`.
- **Killing a process tree is not enough on Windows.** Children of git's bundled shell survive
  it; a job object does not miss them. And a process that inherits output pipes keeps a reader
  waiting after its parent is gone: that hung the git runner, and later `dotnet test` itself,
  when the real-window harness left an app window behind. Any code that starts a process must
  redirect its output and must bound its waits (D31, D36).
- **Never hand your own `Process` object to FlaUI.** `Application.Attach(process)` disposes it;
  attach by process id.
- **A real-window test process must make itself DPI-aware** (`NativeMethods.UseRealPixels`), or
  every coordinate and screenshot is scaled.
- **UI Automation does not see text inside a button's template.** A button is found by its
  automation id and read by its name; that is why `ToolbarButton` exposes its label as its name.
- **Git prints dates differently by version** (`+00:00` or `Z`). Compare times as numbers
  (`%at`), not as ISO text.
- **.NET's process-tree kill froze macOS CI jobs.** Four jobs froze completely and left no log;
  finding the cause took most of the phase's CI time. What worked: a temporary CI step that ran
  the tests in a loop, repeated test steps to make the freeze likely, and removing suspects one
  at a time. What did not: time limits on steps and jobs (the frozen runner ignored them), the
  hang watchdog and a side process meant to rescue the job (both froze with it). See D37.
- **A hung or frozen job costs CI minutes fast.** GitHub ends it only 5 minutes after the job's
  own time limit, and macOS minutes count ten times. Keep `timeout-minutes` low in `ci.yml`.
- **Do not edit docs with PowerShell text replacement.** Windows PowerShell 5.1 read a file as
  ANSI and wrote it back as UTF-8, which garbled every dash and multiplication sign in
  `status.md`, and its backtick escapes ate the code formatting. Use the file-editing tools.
- **`dotnet test` output through a pipe hides hangs.** With `| Select-Object -Last N` nothing is
  printed until the run ends. When a run seems stuck, start the test executable directly with
  `-diagnostics -longRunning 10`.

## For the next phase

Reuse: the git runner as it is (give `GitCommand` an `OnOutputLine` handler to stream `git log`;
the output is then not kept in memory), `TempRepo` and `Scenarios` for every repo a test needs,
`ShellDriver` and `LayoutAudit` for UI tests, `RealApp` and `RealWindowRun` for the real window.

Do first:

1. Read the Known issues above and check the latest CI runs.
2. Settle the open points below; the checks cannot be written without them.
3. Build the scenario repo for the graph (branches, merges, tags, a stash, and a local remote
   for the ahead and behind counts) and pin its SHAs in `ScenarioTests`. The graph's expected
   results are written in terms of it, so it comes before the report.
4. Write phase 1's visual checks into `docs/test-reports/phase-1.md`, each marked with whether it
   is repeated in the real window, and commit them before building any UI.
5. Decide how an open repository is modelled (one view model per repo tab, each with its own
   runner calls, watcher and state) and record it in `decisions.md`; every later phase builds on it.

Open points the plan leaves to phase 1. Decide each, with the owner where it is a matter of
taste, and record the decisions:

- What the user sees for open, clone, init and the recent list: a menu on the "+" button, a
  welcome page in the empty graph area, or dialogs.
- Where the open tabs, the window size and the panel widths are stored. `AppPaths` knows only
  `settings.json` and `logs/`; a `session.json` next to them would fit the design.
- Which placeholders become real in phase 1. The plan gives phase 1 the left panel, the graph,
  the commit details and the tabs. The status bar's branch name follows naturally. The toolbar
  buttons, "Ready" and the activity-log toggle belong to phases 3 and 5.
- How "first graph within about 2 seconds" and "scrolls smoothly" are measured, and on what
  machine.
- Whether the 100k-commit check runs in the default tests and in CI. It must stay well under the
  watchdog's 3 minutes or raise the limit (`VISUALCOMMIT_TEST_HANG_SECONDS`), and the default
  run is 25 seconds today. Building the repo once and keeping it between runs, or giving the
  check a command of its own, are the options.
- Which window size and theme each check uses.

Risks to watch:

- The 100k-commit requirement (Q1) decides the graph's design: stream `git log`, lay out lanes
  incrementally, and draw only the visible rows. Build the large test repo with
  `TempRepo.GitWithInputAsync(stream, "fast-import")`, not with 100,000 `git commit` calls.
- Dates in the graph depend on the time zone and on the current time ("3 days ago"). CI runs in
  UTC and the development machine does not. Give the code that formats dates a clock and a time
  zone that tests can fix (`TimeProvider`), or screenshots will differ by machine and by day.
- On macOS the temp folder is a symlink (`/var` to `/private/var`), so a path git reports can
  differ from the path a test created. Compare resolved paths.
- The runner sets `GIT_TERMINAL_PROMPT=0`. A clone that needs credentials fails at once instead
  of prompting; in-app prompts come in phase 3. Phase 1's clone can only be tested against local
  or public repositories.
- The file watcher will see git's own writes. Decide early how the app tells its own operations
  from outside changes.
- CI minutes (risk 8 in `plan.md`): every push of code starts a run on three platforms. Commit
  as often as the rules ask, but push small code commits together when they follow each other
  within minutes.

## Waiting on the owner

- Accept phase 0, or say what to change.
- The copyright line in `LICENSE` reads "VisualCommit contributors". Change it if you want your
  own name there.
- The repo is private, so CI uses a monthly allowance of minutes and macOS counts ten times
  (risk 8 in `plan.md`). Making the repo public removes the limit.
- When you have a Mac at hand: the eight-step checklist at the end of
  [test-reports/phase-0.md](../test-reports/phase-0.md).
