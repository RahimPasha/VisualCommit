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
- **CI.** GitHub Actions builds and tests on Windows, macOS and Linux on every push that changes
  more than docs.
- **Packaging.** Velopack for installers and auto-update on all three platforms.

## Solution layout

`VisualCommit.slnx`, `Directory.Build.props`, `Directory.Packages.props` and `global.json` sit at
the repo root. App projects are under `src/`, test projects under `tests/`. A project is created
in the phase that first needs it. Package versions are pinned centrally in
`Directory.Packages.props`.

| Project | Contents | State |
|---|---|---|
| `src/VisualCommit.Core` | Domain models and interfaces (git calls, repositories, commits, refs, the lane layout, settings, session state, logging, data-folder paths, how dates are shown); no UI, no process calls | Built |
| `src/VisualCommit.Git` | Git runner, git locator, call log; reading a repository (`GitRepository`), cloning, the repository watcher. Later: the operation queue | Built |
| `src/VisualCommit.App` | Avalonia views, view models, theme, custom controls (the commit graph); settings and session stores, log file, folder dialog. Its assembly and executable are named `VisualCommit` (`VisualCommit.exe`); its namespace is `VisualCommit.App` | Built |
| `src/VisualCommit.Hosting` | GitHub, Azure DevOps and GitLab clients; token store | Phase 7 |
| `tests/VisualCommit.Testing` | Shared test helpers: temporary-repo builder, scenario repos, the 100k-commit repo, helpers that drive the app in headless mode. Not a test project | Built |
| `tests/VisualCommit.Git.Tests` | Tests of Core and Git against real git and temporary repos | Built |
| `tests/VisualCommit.App.Tests` | Settings, session, log, data-folder and view-model tests, and headless UI tests of behaviour | Built |
| `tests/VisualCommit.VisualTests` | Scripted walk-throughs in headless mode with screenshots, for the visual test gate. Part of the default test run and CI | Built |
| `tests/VisualCommit.RealWindowTests` | The real-window pass (FlaUI). Windows-only and opt-in. It is **not in the solution file**; that is what keeps it out of `dotnet build`, `dotnet test` and CI test runs (D25) | Built |

All projects target `net10.0`, except `VisualCommit.RealWindowTests` (`net10.0-windows`).
`Directory.Build.props` turns on nullable reference types, implicit usings and warnings as errors
for every project.

## As built (after phase 1)

Phase 0 built the foundation: an empty shell, the git runner, settings, the log and both test
harnesses. Phase 1 added repositories in tabs: open, init and clone on a welcome page, the commit
graph of the whole history, the left panel of refs, the commit details, the session that restores
tabs and the window, and a watcher that follows outside changes. The app does not write to a
repository yet: staging and committing come in phase 2, the other operations in phase 3.

### Start-up and composition

```
Program.Main                        src/VisualCommit.App/Program.cs
  └─ App (Avalonia Application)     App.axaml(.cs): loads theme resources, Fluent theme, control styles
       └─ AppSession.Start          AppSession.cs: the composition root
            ├─ FileAppLog           one log for the session
            ├─ JsonSettingsStore    loads settings.json (preferences)
            ├─ ThemeService         applies the saved theme before the window exists
            ├─ JsonSessionStore     loads session.json (tabs, recent list, window, panels; D46)
            ├─ GitCallLog           the record of every git call
            ├─ GitAccess            the pending search for git (D45)
            ├─ TabServices          what every tab uses: GitRepositoryProvider, the folder
            │                       dialog, settings, DateDisplay, the log, RecentRepositories
            ├─ MainWindowViewModel  the tabs, restored from the session
            └─ MainWindow           created, given the session (UseSession), not shown
```

- `AppSession` is one running instance of the app on one data folder. New services are created
  and wired there, by hand; there is no dependency-injection container.
- `App.OnFrameworkInitializationCompleted` starts a session only when there is a desktop lifetime.
  Headless tests have none: they call `AppSession.Start` themselves with a temporary data folder
  and a fake folder dialog (`AppSession.Start(paths, application, folderPicker)`).
- `AppSession.Initialization` is `MainWindowViewModel.InitializeAsync`: it looks for git, then
  opens the repositories of the tabs restored from the session and loads their whole history.
  Tests await it before they look at the window.
- Services that need git hold the `GitAccess` and ask it for the runner when they need it
  (`GetRunnerAsync`), which waits for the search and throws `GitUnavailableException` when git
  was not found. Nothing assumes git is there.
- `AppSession.Dispose` closes the window (which saves its placement) and then
  `MainWindowViewModel.CloseAll`, which disposes every tab (logging each graph's frame times,
  D50) and waits a few seconds for a cancelled clone to remove its folder.
- `Program.Main` logs unhandled exceptions to the same log file.

### Repository tabs (D45)

| Type | Role |
|---|---|
| `MainWindowViewModel` | `Tabs` (never empty) and `ActiveTab`; `NewTab`, `ActivateTab`, `CloseTab`; saves the tabs and the active one to the session whenever they change; the theme switch and the git status as in phase 0 |
| `RepoTabViewModel` | One tab: `Title` ("New tab" or the repository's folder name), `CanClose`, the `Welcome` page, the open `Repository` or null. `LeftPanel`, `Details` and `BranchText` are the repository's, or empty ones. A tab restored from the session shows its folder's name at once (`ShowPending`) and opens it once git is found; one that fails keeps its folder in the session (`FailedPath`) until the user opens something else in it |
| `WelcomeViewModel` | The welcome page (D42): Open and Init through `IFolderPicker`, the clone form with its progress (`CloneProgress` from git's stderr) and Cancel, the shared recent list |
| `RepositoryViewModel` | One open repository: its `Graph` (`CommitGraphData`), `SelectedIndex`, `ScrollOffset` (kept per tab), `LeftPanel`, `Details`, `BranchText`, the watcher and the refresh, and the Q1 measurements. Disposing it cancels all its work |
| `RecentRepositories` | The recent list, newest first, at most 10, kept in the session |
| `GitRepositoryProvider` | `IRepositoryProvider` with real git: `OpenAsync`, `InitAsync`, `CloneAsync` through `GitRepository` |

The window's regions show the active tab: `MainWindow.axaml` binds the left region to
`ActiveTab.LeftPanel`, the graph region (`CommitGraphView`) to `ActiveTab` and the right region
to `ActiveTab.Details`; the status bar's branch to `ActiveTab.BranchText`. `CommitGraphView`
shows `WelcomeView` for a tab without a repository, `RepositoryGraphView` for one with a
repository, and nothing while a restored tab is still opening.

### From a repository to the screen

1. `RepoTabViewModel.OpenAsync(path)` starts a stopwatch for Q1 and asks the provider, which
   waits for git and calls `GitRepository.OpenAsync`: one `git rev-parse` gives the working tree,
   the git folder and the common git folder, or `NotARepositoryException`.
2. `RepositoryViewModel.StartAsync` starts the watcher, then reads the refs
   (`ReadRefsAsync`: `for-each-ref`, `remote`, `stash list`, `symbolic-ref`, `rev-parse HEAD`, in
   parallel) and shows them in the left panel and the status bar.
3. At the same time `LoadCommitsAsync(Task<RepoRefs>, ...)` runs one `git log --date-order`
   (D47), which does not need the refs (it names `HEAD` with `--ignore-missing`, for an unborn
   HEAD). `CommitPager` parses each line as git prints it, waits for the refs before the first
   page, merges the stashes in by date (D54) and hands over pages: 100 commits, then 2,000.
4. On the loader's thread each page is laid out by `GraphLayout` (D48, D55) into `GraphRow`s;
   the page and its rows are then posted to the UI thread and appended to `CommitGraphData`,
   which raises `Changed`.
5. `CommitGraphControl` draws the rows in view. The first frame with rows tells the view model,
   which logs the time since step 1 (D50).
6. Selecting a row loads `ReadCommitDetailsAsync` into the details panel.

View models post to the UI thread through the `SynchronizationContext` they were created on, not
through Avalonia's dispatcher, so that they stay testable without a UI; without a context (plain
unit tests) the work runs at once.

### Git layer

Contracts are in `src/VisualCommit.Core/Git`, implementations in `src/VisualCommit.Git`.

| Type | Role |
|---|---|
| `GitCommand` | One call: arguments (passed one by one, no shell quoting), working directory, optional standard input, extra environment variables, optional line handlers for streamed output. `DisplayText`, used in logs and errors, hides the credentials of a URL |
| `IGitRunner` / `GitRunner` | Runs a command without blocking. A non-zero exit code is returned in the `GitResult`, not thrown; `GitResult.EnsureSuccess` turns it into a `GitException` that carries git's own error text |
| `GitResult` | Exit code, standard output, standard error, duration |
| `GitCallRecord`, `IGitCallLog` / `GitCallLog` | The record of every call (command, folder, outcome, exit code, timing, the first 16 KB of each output stream). The activity log (T4, phase 5) will show these |
| `GitLocator`, `GitSearchContext`, `GitDetection`, `GitVersion` | Finding git and checking its version |
| `IGitRepository` / `GitRepository` | One open repository: `ReadRefsAsync`, `LoadCommitsAsync`, `ReadCommitDetailsAsync`, `CreateWatcher`; static `OpenAsync`, `InitAsync`, `CloneAsync`. Split over `GitRepository.cs`, `.Refs.cs`, `.Commits.cs`, `.Clone.cs` |
| `RepoRefs`, `GitRef`, `HeadState`, `StashEntry` | What the refs are at one moment. `RepoRefs.Fingerprint` changes when HEAD, a ref's target or the stashes change (D52) |
| `CommitInfo`, `CommitKind` | A row of the graph: a commit or a stash (one parent: the commit it was made on) |
| `CommitDetails`, `ChangedFile`, `FileChangeKind` | What the details panel shows; files compared with the first parent, renames detected |
| `CommitPager` | Internal: turns `git log` lines into pages and merges the stashes in. A date git cannot print (an ident without a time zone) is read as 1970-01-01 UTC, as git does, so such a commit is not lost |
| `CloneProgressParser`, `CloneProgress` | Git's clone progress lines as stage and percentage |
| `RepositoryWatcher` | `IRepositoryWatcher` (D52): `FileSystemWatcher`s on the git folder and the common folder, changes under `objects/` and to `*.lock` files ignored, 300 ms of quiet before `Changed`, at most 2 s while changes keep coming |
| `WindowsJob` | Internal: a Windows job object, used on Windows to stop git together with everything it started |

Rules the runner follows (see D30, D31, D36, D37, D56 and D59):

- Every call gets `LC_ALL` and `LANG` set to `en_US.UTF-8`, so git's messages are English and
  parseable, and `GIT_TERMINAL_PROMPT=0`, so git fails rather than waits for a terminal. Input and
  output are UTF-8.
- Calls that only read also get `GIT_OPTIONAL_LOCKS=0` (from `GitRepository`), so a read never
  rewrites the index and cannot set off the app's own watcher (D47).
- Standard input is always closed, after writing `GitCommand.StandardInput` if there is one.
- With `OnOutputLine` set, standard output is delivered line by line as it arrives and not kept
  in the result. `OnErrorLine` also treats a carriage return as the end of a line, because that
  is how git writes progress. Handlers run on background threads: a view model must move to the
  UI thread itself.
- No part of a call runs on the caller's thread: `RunAsync` queues it on the thread pool, and
  each output stream is read on a thread of its own, which also runs its handler (D59).
- Cancelling the token stops git, records the call as `Cancelled` and throws
  `OperationCanceledException`. On Windows everything git started is stopped with it, through a
  job object. On macOS and Linux only git itself is killed (D37); what it started ends when its
  pipes to git break. Stopping never throws, and a cancelled call returns within 10 seconds even
  if git could not be stopped.
- Once git has exited, its output is read for as long as it keeps coming or a line handler is
  still busy with it, and given up 2 seconds after the last of it (D56). A process that git left
  behind (a hook's background job) cannot hold a call up, and git's own output is never dropped
  because a handler was slow.

### Lane layout (D48, D54, D55)

`GraphLayout` (Core, `Graph/`) lays out one commit at a time in `git log --date-order` order and
returns a `GraphRow`: the node's lane and colour number and the `GraphLine`s of the row
(`PassThrough`, `IntoNode`, `OutOfNode`). A commit takes the leftmost lane that waits for it, or
else the leftmost free lane or a new one with a new colour; lanes that waited for it end in its
node and are free again before its parents are placed; the first parent continues in the node's
lane, a further parent joins a lane already waiting for it or else takes a free lane with a new
colour. Colour numbers only grow; the graph draws palette entry `colour % 8`. 100,000 commits lay
out in well under a second.

### Commit graph (D49)

`Views/Graph/RepositoryGraphView` holds the column headers (text blocks), the custom-drawn
`Controls/CommitGraphControl` and a `ScrollBar` that lies over the rows' right edge.

- `GraphColumns.Compute(width, laneCount)` places the columns: Branch / Tag 130, Graph
  16 per lane plus 16 (48 to 240), Message the rest (at least 120), Author 140, Date 120, SHA 72;
  SHA, then Author, then Date are hidden when the message would get less than 120. The headers
  take the same widths.
- The control draws only the rows in view, 26 high: the selection and hover backgrounds, lanes
  (2 wide, curves between lanes), nodes (circle 10, merge 7, stash a ring), ref labels
  (`CommitGraphData.LabelsAt`, with "+N" and "…" when they do not fit) and the texts of the
  columns, cut with "…". Laid-out text of up to 256 recent rows is cached; the cache is cleared
  when the theme, font, columns, data or dates change.
- Input: click selects and focuses; Up, Down, Page Up, Page Down, Home and End move the
  selection and scroll it into view; the wheel scrolls 3 rows a notch. `Reveal(index)` scrolls a
  row to the middle (used when a ref or a parent is clicked).
- `SelectedIndex` and `ScrollOffset` bind two ways to the tab's `RepositoryViewModel`, so each
  tab keeps its own. `ScrollOffset` is clamped when read, not coerced, because a coerced value
  would be written back through the binding into the other tab's view model when the view
  switches tabs.
- UI Automation sees the graph as one element (`GraphRows`, "Commit graph"); rows are not
  exposed yet (D49).
- `RowsDrawn` and `FrameDrawn` (the time `Render` took) feed the Q1 measurements.

### Left panel and commit details

- `ViewModels/Panels/LeftPanelViewModel` turns `RepoRefs` into one flat list of rows
  (`RefSectionRow`, `RefItemRow`) that `Views/Shell/LeftPanelView` shows in a virtualised list
  (`RefList`): sections, remotes, folders from `/` in names (folders first, then items, each by
  name), ahead/behind counts, the filter, the branch HEAD is on in the accent colour. Which
  sections, remotes and folders are open is kept by key, so a refresh does not undo it. Clicking
  a ref raises `RefActivated` with its commit; the repository view model selects and reveals it.
  A panel that was never updated is phase 0's five closed sections.
- `ViewModels/Panels/CommitDetailsViewModel` loads `CommitDetails` for the selected commit (a
  newer selection cancels an older load) into `Views/Shell/RightPanelView`: subject, body,
  author, date, committer and commit date when they differ, the id, parents as links
  (`ParentActivated`; a stash lists only its base), the stash's name, and the changed files flat
  or as a tree (`AppSettings.FileList`, D44).

### Watching and refreshing (D52)

`RepositoryViewModel` starts a `RepositoryWatcher` before it first reads the refs. A change that
comes during the first load is remembered and refreshed once the load is done. `RefreshAsync`
reads the refs; when their fingerprint is unchanged only the left panel and status bar are
updated; otherwise the whole history is loaded into a new `CommitGraphData`, which replaces the
old one when it is complete, with the selected commit selected again. Refreshes that come while
one runs make it run once more. D52 has writing operations pause the watcher from phase 3, with
the operation queue; phase 2's stage, discard and commit write first, so phase 2 has to decide
how its writes keep the watcher from refreshing in a loop.

### Q1 measurements (D50)

The app logs, per repository: `<name>: first graph rows drawn after <N> ms` (from the start of
opening to the first frame with rows), `<name>: loaded <N> commits in <N> ms`, and when the tab
closes or the app exits `<name>: graph frames while loading: ...` and `<name>: graph frames:
<N> drawn, 95th percentile <N.N> ms, longest <N.N> ms` (frames after the load, D57). At Debug
level it also logs when the repository was opened, when the refs were read and when the first
commits came from git, measured from the same start. The real-window pass reads these lines from
the log.

On the Windows development machine the gate measured the 100k repo's first rows after 1002 ms.
Most of that is git's own work: without a commit-graph file, `git log --date-order` reads the
whole history before it prints its first commit (0.6 to 0.9 seconds there); opening and the refs
take about 0.2 seconds each, side by side with it. The machine's speed varies with its
temperature, so the pass is started after a rest (see phase 1's test report).

### Settings, session, log and data folder

- `AppPaths` (Core) names everything in the data folder: `settings.json`, `session.json` and
  `logs/`. `AppPaths.Resolve()` uses `VISUALCOMMIT_DATA_DIR` when it is set, otherwise the
  per-user folder (`%APPDATA%\VisualCommit`, `~/Library/Application Support/VisualCommit`,
  `~/.config/VisualCommit`).
- `AppSettings` (Core) is an immutable record of preferences: `Theme` and `FileList` (D44). Every
  property needs a default, so that older files still load. `ISettingsStore.Update(settings =>
  settings with { ... })` changes and saves it.
- `SessionState` (Core) is what changes as the app is used (D46): `Tabs` and `ActiveTab`,
  `Recent`, `Window` (`WindowPlacement`: position, client size, maximised) and the panels'
  preferred widths. `JsonSessionStore` (App) keeps it like the settings: written through a
  temporary file, an unreadable file set aside as `session.unreadable.json`, a tab with a blank
  folder read as an empty tab. `JsonSessionStore.Write` lets tests start the app with tabs open.
- `JsonSettingsStore` (App) writes camel-case JSON with enums as text, through a temporary file,
  so a crash cannot leave half a file. A file it cannot read is moved to
  `settings.unreadable.json` and the defaults apply. A failed save is logged and does not throw.
- `DateDisplay` (Core) shows dates as `yyyy-MM-dd HH:mm` in the local zone, or in the zone named
  by `VISUALCOMMIT_TIME_ZONE` (D43); both harnesses set `UTC`.
- `IAppLog` (Core) is the log interface, with `Debug`, `Info`, `Warning` and `Error` helpers.
  `FileAppLog` (App) appends to `logs/visualcommit-yyyyMMdd.log`, one line per entry
  (`2026-10-06 01:34:44.722 -07:00 [INF] message`), opening the file for each entry, so several
  instances can share it. Files older than 14 days are deleted at start-up. A log that cannot
  write never throws.

### Shell and views

`Views/MainWindow.axaml` lays the window out as "Main window layout" in `requirements.md`. Each
region is a `UserControl` in `Views/Shell`.

| Region | View | Size | Automation id | State |
|---|---|---|---|---|
| Repo tabs | `RepoTabsView` | 36 high | `RepoTabs` | Real: one tab per repository (`RepoTab` buttons named by title, `CloseTabButton`), and "+" (`AddRepoButton`) for a new tab |
| Toolbar | `ToolbarView` | 52 high | `Toolbar` | Buttons `UndoButton` ... `SearchButton` are disabled placeholders (phases 3 and 5). `ThemeSwitch` works |
| Left panel | `LeftPanelView` | 260 wide by default; 180 to 520 | `LeftPanel` | Real: `FilterBox` and the list `RefList` (`Section`, `RefItem`); "Pull requests" stays empty until phase 7 |
| Commit graph | `CommitGraphView` | The rest; at least 320 | `CommitGraph` | Real: `WelcomeView` (`OpenRepoButton`, `CloneRepoButton`, `InitRepoButton`, the clone form) or `RepositoryGraphView` (`GraphRows`, `GraphScrollBar`) |
| Right panel | `RightPanelView` | 400 wide by default; 280 to 720 | `RightPanel` | Real: commit details (`DetailsSubject`, `ParentLink`, `FlatFilesButton`, `TreeFilesButton`, `ChangedFilesTitle`, `FileList`) or the placeholder |
| Status bar | `StatusBarView` | 26 high | `StatusBar` | `CurrentBranch` and `GitStatus` are real. `OperationStatus` and `ActivityLogToggle` are placeholders |

- The window opens at 1280×800, or at the size and place the session saved, and cannot be made
  smaller than 1000×560. `MainWindow.UseSession` restores and saves the placement (a window
  restored maximised keeps its normal size and position).
- The panels are resized with two `GridSplitter`s (`LeftSplitter`, `RightSplitter`) that lie over
  the panel edges. Letting go of an edge makes that panel's width its preferred width, saved in
  the session. `PanelLayout.Fit` gives the drawn widths: when the window is too narrow for both
  preferred widths and the graph's 320, both panels give way in proportion to their room above
  their minimums, and take their preferred widths back when it grows.
- The diff view that replaces the graph does not exist yet (phase 2).

Custom controls, in `Controls/`:

- `Icon` draws one of the outlines in `Theme/Icons.axaml` as a stroke in the inherited text
  colour. To add an icon, add a `StreamGeometry` on a 24×24 grid to `Icons.axaml` (and to
  `THIRD-PARTY-NOTICES.md` if it is not our own drawing).
- `ToolbarButton` is a button with an icon above a label. Its control theme is in
  `Theme/Controls.axaml`; its label is also its name for screen readers and UI Automation.
- `CommitGraphControl`: see "Commit graph".

Control themes in `Theme/Controls.axaml`: `VcFlatButton`, `VcTabButton`, `VcActionButton`, and
the text classes `heading`, `secondary` and `error`.

### Theme

- `IThemeService` / `ThemeService` (App, `Services/`) switches the theme by setting the
  application's theme variant. The view model's `ToggleThemeCommand` applies the theme and saves
  it; `AppSession.Start` applies the saved theme before the window is created.
- All colours are tokens in `Theme/Tokens.axaml`, one set per theme. Views use them only as
  `{DynamicResource ...}`, never as fixed colours, so switching recolours everything at once. The
  graph control looks them up for its theme variant and redraws when the variant changes.
- The Fluent theme is the base for standard controls, with its accent set to ours. Its text-box
  colours are replaced by `TextControl...` keys in `Tokens.axaml`.
- The typeface is Inter, shipped with Avalonia, on every platform (D28).

| Token (brush key) | Dark | Light | Used for |
|---|---|---|---|
| `VcWindowBackgroundBrush` | `#14161B` | `#FFFFFF` | The graph area, the active repo tab, the inside of a stash's ring |
| `VcPanelBackgroundBrush` | `#1A1D24` | `#F5F6F8` | Left and right panels |
| `VcChromeBackgroundBrush` | `#20242C` | `#EBEDF1` | Repo tabs, toolbar, status bar, column headers |
| `VcControlBackgroundBrush` | `#272C36` | `#FFFFFF` | Inputs, action buttons, tag and stash labels; a pressed button |
| `VcHoverBackgroundBrush` | `#2F3542` | `#DEE2E9` | A button under the pointer |
| `VcBorderBrush` | `#323845` | `#D2D6DE` | Lines between regions, input borders, tag and stash label borders |
| `VcTextPrimaryBrush` | `#E4E7EC` | `#1B1F27` | Main text, commit messages |
| `VcTextSecondaryBrush` | `#9BA3B0` | `#5C6572` | Headings, hints, counts, author, date and SHA |
| `VcTextDisabledBrush` | `#5F6775` | `#A1A8B3` | Disabled buttons |
| `VcAccentBrush` | `#7C8CFF` | `#4353D8` | The line on the active tab, focus borders, the branch HEAD is on in the left panel, renamed files, parent links |
| `VcAccentTextBrush` | `#10121A` | `#FFFFFF` | Text on an accent background (not used yet) |
| `VcSuccessBrush` | `#3FB97F` | `#1E8E5A` | Added files |
| `VcWarningBrush` | `#E0A23B` | `#B7791F` | Modified files |
| `VcDangerBrush` | `#E5606B` | `#C93A46` | Errors, deleted files |
| `VcSelectionBrush` | `#2A3150` | `#DDE1FA` | The selected graph row and left-panel item |
| `VcRowHoverBrush` | `#1C1F27` | `#F3F4F7` | The graph row under the pointer |
| `VcLabelTextBrush` | `#10121A` | `#FFFFFF` | Text on a filled branch label |
| `VcLane0Brush` ... `VcLane7Brush` | `#7C8CFF` `#3FB97F` `#E0A23B` `#E5606B` `#4FB3D9` `#B07CE8` `#D97EB6` `#8FB84A` | `#4353D8` `#1E8E5A` `#B7791F` `#C93A46` `#1F86B0` `#8048C7` `#B54A8C` `#5F8A1E` | Lane colours 0 to 7 (D48) |

### Conventions

- View models derive from `ObservableObject` and use the toolkit's `[ObservableProperty]` on
  partial properties and `[RelayCommand]`. They take their services through the constructor and do
  not touch Avalonia types, so they are tested without a UI; they reach the UI thread through the
  `SynchronizationContext` they were created on.
- XAML uses compiled bindings: a view that binds declares its `x:DataType`.
- Anything the real-window pass needs to find has an `AutomationProperties.AutomationId`, because
  UI Automation can find nothing else; anything a user can act on has an accessible name. UI
  Automation does not see a text inside a button, so buttons carry their text as their name.
- Async code in the Core and Git projects uses `ConfigureAwait(false)`, and so do App services
  whose tasks the app may wait for while it closes (`GitRepositoryProvider`); view models do not,
  so they continue on the UI thread.
- Services that can fail on the user's machine (settings, session, log) log the failure and carry
  on; they do not throw into the UI. A failure the user caused or can fix (a folder that is not a
  repository, git's own error) is shown where they acted: on the welcome page, in the clone form,
  in the details panel.
- Text that may be too long for its place is cut with "…" and shows whole in a tooltip.

### Tests

Tests run on xunit.v3 with Microsoft Testing Platform (D24). The commands are in `CLAUDE.md`.

| Helper | Where | What it is for |
|---|---|---|
| `TempDirectory` | `tests/VisualCommit.Testing` | A folder under the system temp folder, deleted on dispose |
| `TempRepo` | same | Builds a real git repo for a test, cut off from the machine's git configuration, with a fixed author and a clock that advances one minute per commit, so the same steps give the same SHAs everywhere. Helpers for commits, branches, tags (lightweight and annotated), merges, stashes, a bare remote next to the repo and pushes; `CopyAsync` makes an independent copy with its remotes pointed at the copy; anything else goes through `GitAsync` or `GitWithInputAsync` |
| `Scenarios` | same | The scenario repos: `LinearAsync` (phase 0) and `GraphAsync` (phase 1: branches in folders, a merge, tags, a remote with ahead and behind, a stash), built once per test process and copied for each test |
| `LargeHistory` | same | The 100k-commit repo (D51), built once per machine with `git fast-import` under `%TEMP%/VisualCommit.Tests/shared/` and shared read-only; what each row shows follows from its number |
| `GitIsolation` | same | Cuts the test process off from the machine: no system or user git configuration, no search for a repository above the tests' own temp folder (`GIT_CEILING_DIRECTORIES`), none of the variables a surrounding process can hand git, and dates in UTC. The UI test assemblies call it through `HeadlessTestApp.IsolateFromTheMachine` in a module initializer, `VisualCommit.Git.Tests` directly |
| `HeadlessTestApp` | `tests/VisualCommit.Testing/Headless` | Builds the real `App` for headless mode with Skia rendering. Each UI test assembly names it in `AssemblyInfo.cs` |
| `ShellDriver` | same | Starts an `AppSession` on a data folder (`Start`, or `StartWithTabs` with repositories open through the session file), shows its window at a size, and drives it with simulated input: click, right-click, double-click, drag, wheel, hover, typing and keys. `Folders` is the fake folder dialog; `WaitForAsync` waits for a condition while the UI thread keeps running; `Capture()` returns a `Screenshot` |
| `FakeFolderPicker` | same | Answers the app's folder dialogs with folders the test queued (D42) |
| `Screenshot` | same | A captured frame: save as PNG, read pixel colours |
| `LayoutAudit` | same | `FindClippedText` lists every visible text that is not shown in full; with `allowShortenedWithToolTip` it lets through text cut with "…" on purpose whose whole text is in a tooltip |
| `FreshApplication` | same | Runs part of a test with a new Avalonia application object: how a scripted check crosses a restart (D32) |
| `HangWatchdogAttribute` | `tests/VisualCommit.Testing` | `[assembly: HangWatchdog]` in each default test assembly. If a test runs for more than 3 minutes, or nothing starts or finishes for that long, it prints which tests were running and stops the test process. `VISUALCOMMIT_TEST_HANG_SECONDS` changes the limit |

- `VisualCommit.VisualTests` has a folder per phase. `PhaseNChecks` holds a test for every
  numbered check of the phase's test report (phase 1's are split over `Phase1Checks.Shell.cs` and
  `Phase1Checks.Graph.cs`); each saves its screenshots as
  `artifacts/visual/phase-N/scripted/<check><step>-<what>.png` and asserts the expected result.
  The first screenshot of a run empties the phase's folder. Expected values are copied from the
  report into the tests (`Phase0/ShellExpectations.cs`, `Phase1/Phase1Expectations.cs`), not
  read from the app. Phase 0's checks expect the empty shell as phase 1 changed it (the welcome
  page, "New tab").
- Small text is anti-aliased so that no pixel need have its exact colour; checks read the brush a
  small text is drawn with, and sample colours of shapes and large text only.
- `VisualCommit.RealWindowTests`: `RealApp` starts the built app with the same isolation as the
  headless tests (also `VISUALCOMMIT_TIME_ZONE=UTC`), optionally with a session file
  (`Launch(data, session)`), finds elements through UI Automation, gives real mouse, wheel and
  keyboard input, chooses a folder in the native folder dialog (`ChooseFolderInDialog`), moves
  and sizes the window, reads the app's log, and captures the window from the screen.
  `RealWindowRun` is one run of a pass: it empties the phase's folder, compares each capture with
  the scripted screenshot of the same step (at most 3% of pixels may differ, a pixel counting only when nothing within a pixel of it matches: D33, D58), saves a
  picture of the differing pixels, and writes `run.txt`. The project references
  `VisualCommit.Testing`, so it uses the same scenario repos. Its tests never run in parallel
  (they share the desktop and the mouse).

### CI

`.github/workflows/ci.yml` builds the solution in Release and runs `dotnet test` on
`windows-latest`, `macos-latest` and `ubuntu-latest` for every push and pull request that changes
more than docs. On Windows it also builds, but does not run, the real-window pass. Each job
uploads `artifacts/visual/` as `visual-<os>`, so the scripted screenshots of all three platforms
can be downloaded and looked at.

Two more jobs, both named "bare ubuntu container", start from an empty `ubuntu:24.04`
container, run `scripts/setup-linux.sh` in it twice (the second run has to find everything in
place) and then build and test. The script installs the .NET 10 SDK and the system libraries
the tests need (ICU, fontconfig with a font, git, a locale). It takes the SDK from the system's
own package servers when they carry it, as Ubuntu 24.04's do, and from Microsoft's installer
otherwise; one job runs each way (`SETUP_LINUX_SDK_SOURCE=installer` forces the second). The
script is what a Linux cloud session runs to prepare its machine (D39), and these jobs keep it
proven. What they cannot show is whether the downloads get through a cloud session's network
allowlist: a CI runner's network is open. The first way exists for that reason, because the
default allowlist names Ubuntu's package servers but not the server Microsoft's installer
downloads from.

CI's git is newer than the development machine's (2.55 against 2.36): tests must work on both.

### Package versions

.NET SDK 10.0. `global.json` accepts any 10.0 feature band, and its `version` has to stay in
the first band (10.0.1xx): Ubuntu's packages, which a cloud session and one of the two
container jobs build with, never leave that band (10.0.112 in October 2026), while the Windows
machine and the other CI jobs use newer ones (10.0.303 and 10.0.401 then). Avalonia 12.1.3 (with
Avalonia.Desktop, Themes.Fluent, Fonts.Inter, Skia, Headless, Headless.XUnit),
CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2, FlaUI.UIA3 5.0.0. Phase 1 added no packages.
