# VisualCommit — Architecture

This file describes what is actually built, and it is updated at the end of every phase. Where
the code and this file disagree, fix this file. "Design" is the plan for the whole app; parts of
it are not built yet, and "As built" says which are. The reasons behind the main choices are in
[decisions.md](decisions.md).

## Design

- **One process, all C#.** An Avalonia desktop app using MVVM (CommunityToolkit.Mvvm). The UI calls
  the git layer directly; there is no web server or API layer in between.
- **Git engine: the real `git` executable.** A process runner starts git, streams its output and
  parses the machine-readable formats (`log` with a custom format, `status --porcelain=v2`,
  `blame --porcelain`, unified diffs). Hunk and line staging build a patch and apply it to the index.
- **Operation queue per repo.** One writing operation at a time, with progress, cancellation and a
  record in the activity log (T4).
- **Safety net.** Before a destructive operation the app stores a backup reference under
  `refs/visualcommit/backup/`. Undo/redo (O2) restores from these and from the reflog.
- **Prompts.** Git's credential, passphrase and rebase-editor prompts are redirected to small
  helper hooks that talk to the app, so they appear as in-app dialogs.
- **Editors.** Diff, blame and conflict views are built on AvaloniaEdit with TextMate grammars for
  syntax highlighting.
- **Hosting.** One provider interface with GitHub, Azure DevOps and GitLab implementations.
  Tokens live in the OS keychain.
- **Storage.** Settings and session state as JSON in the per-user app-data folder. Logs are local files.
  The data folder can be overridden (`VISUALCOMMIT_DATA_DIR`). Every test uses a temporary one, so
  tests never touch the user's real settings.
- **Tests.** xUnit. Git-layer tests run real git against temporary repos. UI tests use Avalonia's
  headless mode and capture screenshots.
- **CI.** GitHub Actions builds and tests on Windows, macOS and Linux on every push.
- **Packaging.** Velopack for installers and auto-update on all three platforms.

## Solution layout

`VisualCommit.slnx`, `Directory.Build.props`, `Directory.Packages.props` and `global.json` sit at
the repo root. App projects are under `src/`, test projects under `tests/`. A project is created
in the phase that first needs it. Package versions are pinned centrally in
`Directory.Packages.props`.

| Project | Contents | State |
|---|---|---|
| `src/VisualCommit.Core` | Domain models and interfaces (git calls, settings, logging, data-folder paths); no UI, no process calls | Built |
| `src/VisualCommit.Git` | Git runner, git locator, call log. Later: output parsers, operation queue, repo watcher | Built |
| `src/VisualCommit.App` | Avalonia views, view models, theme, custom controls; settings store and log file | Built |
| `src/VisualCommit.Hosting` | GitHub, Azure DevOps and GitLab clients; token store | Phase 7 |
| `tests/VisualCommit.Testing` | Shared test helpers: temporary-repo builder, scenario repos, helpers that drive the app in headless mode. Not a test project | Built |
| `tests/VisualCommit.Git.Tests` | Tests of Core and Git against real git and temporary repos | Built |
| `tests/VisualCommit.App.Tests` | Settings, log and view-model tests, and headless UI tests of behaviour | Built |
| `tests/VisualCommit.VisualTests` | Scripted walk-throughs in headless mode with screenshots, for the visual test gate. Part of the default test run and CI | Built |
| `tests/VisualCommit.RealWindowTests` | The real-window pass (FlaUI). Windows-only and opt-in. It is **not in the solution file**; that is what keeps it out of `dotnet build`, `dotnet test` and CI test runs (D25) | Built |

All projects target `net10.0`, except `VisualCommit.RealWindowTests` (`net10.0-windows`).
`Directory.Build.props` turns on nullable reference types, implicit usings and warnings as errors
for every project.

## As built (after phase 0)

Phase 0 built the foundation: the app starts to an empty shell. There are no repositories,
commits or git operations in the UI yet.

### Start-up and composition

```
Program.Main                      src/VisualCommit.App/Program.cs
  └─ App (Avalonia Application)   App.axaml(.cs): loads theme resources, Fluent theme, control styles
       └─ AppSession.Start        AppSession.cs: the composition root
            ├─ FileAppLog         one log for the session
            ├─ JsonSettingsStore  loads settings.json
            ├─ ThemeService       applies the saved theme before the window exists
            ├─ GitCallLog         the record of every git call
            ├─ MainWindowViewModel
            └─ MainWindow         created, not shown; the desktop lifetime shows it
```

- `AppSession` is one running instance of the app on one data folder. New services are created
  and wired there, by hand; there is no dependency-injection container.
- `App.OnFrameworkInitializationCompleted` starts a session only when there is a desktop lifetime.
  Headless tests have none: they call `AppSession.Start` themselves with a temporary data folder.
- `AppSession.Initialization` is the task of the start-up work that runs after the window is
  created (finding git). Tests await it before they look at the window.
- `Program.Main` logs unhandled exceptions to the same log file.

### From a git command to the screen

The only git call so far is the version check, and it already takes the whole path:

1. `AppSession` gives the view model a function that calls `GitLocator.DetectAsync`.
2. `GitLocator` finds the executable (first on the `PATH`, then the usual install folders of the
   platform) and runs `git --version` through a `GitRunner`.
3. `GitRunner.RunAsync` starts the process, collects the output, records the call in the
   `IGitCallLog` and the app log, and returns a `GitResult`.
4. `GitLocator` parses the version (`GitVersion`), compares it with the minimum (2.30) and returns
   a `GitDetection`: `Available` with a ready runner, or `NotFound`, `TooOld`, `Broken`.
5. `MainWindowViewModel.InitializeAsync` turns that into `GitStatusText`, which the status bar
   binds to.

### Git layer

Contracts are in `src/VisualCommit.Core/Git`, implementations in `src/VisualCommit.Git`.

| Type | Role |
|---|---|
| `GitCommand` | One call: arguments (passed one by one, no shell quoting), working directory, optional standard input, extra environment variables, optional line handlers for streamed output |
| `IGitRunner` / `GitRunner` | Runs a command without blocking. A non-zero exit code is returned in the `GitResult`, not thrown; `GitResult.EnsureSuccess` turns it into a `GitException` that carries git's own error text |
| `GitResult` | Exit code, standard output, standard error, duration |
| `GitCallRecord`, `IGitCallLog` / `GitCallLog` | The record of every call (command, folder, outcome, exit code, timing, the first 16 KB of each output stream). The activity log (T4, phase 5) will show these |
| `GitLocator`, `GitSearchContext`, `GitDetection`, `GitVersion` | Finding git and checking its version |
| `WindowsJob` | Internal: a Windows job object, used to stop git together with everything it started |

Rules the runner follows (see D30 and D31):

- Every call gets `LC_ALL` and `LANG` set to `en_US.UTF-8`, so git's messages are English and
  parseable, and `GIT_TERMINAL_PROMPT=0`, so git fails rather than waits for a terminal. Input and
  output are UTF-8.
- Standard input is always closed, after writing `GitCommand.StandardInput` if there is one.
- With `OnOutputLine` set, standard output is delivered line by line as it arrives and not kept
  in the result. `OnErrorLine` also treats a carriage return as the end of a line, because that
  is how git writes progress. Handlers run on thread-pool threads: a view model must move to the
  UI thread itself.
- Cancelling the token stops git and all its child processes, records the call as `Cancelled` and
  throws `OperationCanceledException`.
- Once git has exited, output is read for at most 2 more seconds. A process that git left behind
  (a hook's background job) cannot hold a call up.

### Settings, log and data folder

- `AppPaths` (Core) names everything in the data folder: `settings.json` and `logs/`.
  `AppPaths.Resolve()` uses `VISUALCOMMIT_DATA_DIR` when it is set, otherwise the per-user folder
  (`%APPDATA%\VisualCommit`, `~/Library/Application Support/VisualCommit`, `~/.config/VisualCommit`).
- `AppSettings` (Core) is an immutable record; so far it holds only `Theme`. Every property needs
  a default, so that older files still load. `ISettingsStore.Update(settings => settings with { ... })`
  changes and saves it.
- `JsonSettingsStore` (App) writes camel-case JSON with enums as text, through a temporary file,
  so a crash cannot leave half a file. A file it cannot read is moved to
  `settings.unreadable.json` and the defaults apply. A failed save is logged and does not throw.
- `IAppLog` (Core) is the log interface, with `Debug`, `Info`, `Warning` and `Error` helpers.
  `FileAppLog` (App) appends to `logs/visualcommit-yyyyMMdd.log`, one line per entry
  (`2026-10-06 01:34:44.722 -07:00 [INF] message`), opening the file for each entry, so several
  instances can share it. Files older than 14 days are deleted at start-up. A log that cannot
  write never throws.

### Shell and views

`Views/MainWindow.axaml` lays the window out as "Main window layout" in `requirements.md`. Each
region is a `UserControl` in `Views/Shell`, so a later phase can fill a region without touching
the others. All of them still use `MainWindowViewModel` as their data context; a region gets its
own view model in the phase that gives it real content.

| Region | View | Size | Automation id | State |
|---|---|---|---|---|
| Repo tabs | `RepoTabsView` | 36 high | `RepoTabs` | Placeholder: a "No repository" tab and a disabled add button (`AddRepoButton`) |
| Toolbar | `ToolbarView` | 52 high | `Toolbar` | Buttons `UndoButton` ... `SearchButton` are disabled placeholders. `ThemeSwitch` works |
| Left panel | `LeftPanelView` | 260 wide; 180 to 520 | `LeftPanel` | Placeholder: filter box (`LeftPanelFilter`) and five empty section headers |
| Commit graph | `CommitGraphView` | The rest; at least 320 | `CommitGraph` | Placeholder: column headers and an empty state |
| Right panel | `RightPanelView` | 400 wide; 280 to 720 | `RightPanel` | Placeholder: "Commit details" and an empty state |
| Status bar | `StatusBarView` | 26 high | `StatusBar` | `GitStatus` is real. `CurrentBranch`, `OperationStatus` and `ActivityLogToggle` are placeholders |

- The window opens at 1280×800 and cannot be made smaller than 1000×560.
- The panels are resized with two `GridSplitter`s (`LeftSplitter`, `RightSplitter`) that lie over
  the panel edges. Panel widths are not saved yet.
- The diff view that replaces the graph does not exist yet (phase 2).

Custom controls, in `Controls/`:

- `Icon` draws one of the outlines in `Theme/Icons.axaml` as a stroke in the inherited text
  colour, so an icon follows its button's enabled, disabled and theme colours. To add an icon, add
  a `StreamGeometry` on a 24×24 grid to `Icons.axaml` (and to `THIRD-PARTY-NOTICES.md` if it is
  not our own drawing).
- `ToolbarButton` is a button with an icon above a label. Its control theme is in
  `Theme/Controls.axaml`; its label is also its name for screen readers and UI Automation.

### Theme

- `IThemeService` / `ThemeService` (App, `Services/`) switches the theme by setting the
  application's theme variant. The view model's `ToggleThemeCommand` applies the theme and saves
  it; `AppSession.Start` applies the saved theme before the window is created.
- All colours are tokens in `Theme/Tokens.axaml`, one set per theme. Views use them only as
  `{DynamicResource ...}`, never as fixed colours, so switching recolours everything at once.
- The Fluent theme is the base for standard controls, with its accent set to ours. Its text-box
  colours are replaced by `TextControl...` keys in `Tokens.axaml`.
- The typeface is Inter, shipped with Avalonia, on every platform (D28).

| Token (brush key) | Dark | Light | Used for |
|---|---|---|---|
| `VcWindowBackgroundBrush` | `#14161B` | `#FFFFFF` | The commit graph area and the selected repo tab |
| `VcPanelBackgroundBrush` | `#1A1D24` | `#F5F6F8` | Left and right panels |
| `VcChromeBackgroundBrush` | `#20242C` | `#EBEDF1` | Repo tabs, toolbar, status bar, column headers |
| `VcControlBackgroundBrush` | `#272C36` | `#FFFFFF` | Inputs; a pressed button |
| `VcHoverBackgroundBrush` | `#2F3542` | `#DEE2E9` | A button under the pointer |
| `VcBorderBrush` | `#323845` | `#D2D6DE` | Lines between regions, input borders |
| `VcTextPrimaryBrush` | `#E4E7EC` | `#1B1F27` | Main text |
| `VcTextSecondaryBrush` | `#9BA3B0` | `#5C6572` | Headings, hints, counts |
| `VcTextDisabledBrush` | `#5F6775` | `#A1A8B3` | Disabled buttons |
| `VcAccentBrush` | `#7C8CFF` | `#4353D8` | The line on the selected tab, focus borders |
| `VcAccentTextBrush` | `#10121A` | `#FFFFFF` | Text on an accent background (not used yet) |
| `VcSuccessBrush` | `#3FB97F` | `#1E8E5A` | Not used yet |
| `VcWarningBrush` | `#E0A23B` | `#B7791F` | Not used yet |
| `VcDangerBrush` | `#E5606B` | `#C93A46` | Not used yet |

Phase 1 adds the lane colours of the commit graph to this table.

### Conventions

- View models derive from `ObservableObject` and use the toolkit's `[ObservableProperty]` on
  partial properties and `[RelayCommand]`. They take their services through the constructor and do
  not touch Avalonia types, so they are tested without a UI.
- XAML uses compiled bindings (`x:DataType` on every view).
- Anything a test or the real-window pass needs to find has an
  `AutomationProperties.AutomationId`; anything a user can act on has an accessible name.
- Async code in the Core and Git projects uses `ConfigureAwait(false)`; view models do not, so
  they continue on the UI thread.
- Services that can fail on the user's machine (settings, log) log the failure and carry on; they
  do not throw into the UI.

### Tests

Tests run on xunit.v3 with Microsoft Testing Platform (D24). The commands are in `CLAUDE.md`.

| Helper | Where | What it is for |
|---|---|---|
| `TempDirectory` | `tests/VisualCommit.Testing` | A folder under the system temp folder, deleted on dispose |
| `TempRepo` | same | Builds a real git repo for a test, cut off from the machine's git configuration, with a fixed author and a clock that advances one minute per commit. The same steps give the same commit SHAs on every platform |
| `Scenarios` | same | The scenario repos: named, known repos that tests and visual checks start from. So far `LinearAsync` |
| `HeadlessTestApp` | `tests/VisualCommit.Testing/Headless` | Builds the real `App` for headless mode with Skia rendering. Each UI test assembly names it in `AssemblyInfo.cs` |
| `ShellDriver` | same | Starts an `AppSession` on a data folder, shows its window at a size, and drives it with simulated mouse input at window positions; `Capture()` returns a `Screenshot` |
| `Screenshot` | same | A captured frame: save as PNG, read pixel colours |
| `LayoutAudit` | same | `FindClippedText` lists every visible text that is not shown in full |
| `FreshApplication` | same | Runs part of a test with a new Avalonia application object: how a scripted check crosses a restart (D32) |
| `HangWatchdogAttribute` | `tests/VisualCommit.Testing` | `[assembly: HangWatchdog]` in each default test assembly. If a test runs for more than 3 minutes, or nothing starts or finishes for that long, it prints which tests were running and stops the test process, so a hang fails quickly and names its test |

- `VisualCommit.VisualTests` has a folder per phase. `PhaseNChecks` holds one test per numbered
  check of the phase's test report; each saves its screenshots as
  `artifacts/visual/phase-N/scripted/<check><step>-<what>.png` and asserts the expected result.
  `Phase0/ShellExpectations.cs` is the expected shell in code, with its numbers and colours copied
  from the report, not from the app.
- `VisualCommit.RealWindowTests`: `RealApp` starts the built app, finds elements through UI
  Automation, clicks with the real mouse and captures the window from the screen. `Screens`
  compares a capture with the scripted screenshot of the same step and saves a picture of the
  differing pixels. The pass writes `run.txt` with the display scaling and the differences.

### CI

`.github/workflows/ci.yml` builds the solution in Release and runs `dotnet test` on
`windows-latest`, `macos-latest` and `ubuntu-latest` for every push and pull request that changes
more than docs. On Windows it also builds, but does not run, the real-window pass. Each job
uploads `artifacts/visual/` as `visual-<os>`, so the scripted screenshots of all three platforms
can be downloaded and looked at.

### Package versions

.NET SDK 10.0 (`global.json` accepts any 10.0 feature band). Avalonia 12.1.3 (with
Avalonia.Desktop, Themes.Fluent, Fonts.Inter, Skia, Headless, Headless.XUnit),
CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2, FlaUI.UIA3 5.0.0.
