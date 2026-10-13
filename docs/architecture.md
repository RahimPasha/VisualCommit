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
- **Operation queue per repo (D81).** Every write to a repository runs through its queue
  (`OperationQueue`, Core): one at a time, first in first out, on the UI thread's context, with
  the watcher paused and a refresh afterwards. Network operations show progress and can be
  cancelled; a local one, once started, runs all its steps to the end, except the external merge
  tool, which only waits for another program and can be cancelled too (D149). A background slot
  runs auto-fetch and the backups' clean-up when a repository opens (D139). Every operation is
  recorded (`IOperationLog`), and every git call names its operation, for the activity log (T4,
  phase 5).
- **Safety net.** Before a destructive operation the app stores a backup reference under
  `refs/visualcommit/backup/`: a snapshot commit before every discard (D67, phase 2), since
  phase 3 a backup batch before every reset, branch, tag or remote deletion, force push, stash
  drop, abort and checkout that leaves detached commits behind (D83), and since phase 4 one
  before every rebase and every Skip (D142, D144). Since phase 4 they are cleaned up
  automatically: a backup goes after 30 days, or when more than 50 exist, the oldest first, never
  while an operation is in progress (D139). Undo/redo (O2, phase 5) restores from what is left
  and from the reflog.
- **Prompts.** Git's and ssh's credential, passphrase and host-key prompts reach the app through
  a helper program, `visualcommit-askpass`, set as `GIT_ASKPASS` and `SSH_ASKPASS`; it asks the
  running app over a named pipe, and the app answers through a dialog in the window (D82).
  Editors never open: every call runs with `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:` (D68,
  D102). The interactive rebase alone sets its own sequence editor, for its one call: a shell
  function that copies a todo and message files the app wrote (D143).
- **Editors.** Diff, blame and conflict views are built on AvaloniaEdit, with TextMate grammars
  (TextMateSharp, called by the app's own code) for syntax highlighting (D64); the conflict
  resolver's ours and theirs panes have it, its editable result has none (D148).
- **Hosting.** One provider interface with GitHub, Azure DevOps and GitLab implementations.
  Tokens live in the OS keychain.
- **Storage.** Settings and session state as JSON in the per-user app-data folder. Logs are local files.
  The data folder can be overridden (`VISUALCOMMIT_DATA_DIR`). Every test uses a temporary one, so
  tests never touch the user's real settings.
- **Tests.** xUnit. Git-layer tests run real git against temporary repos, remotes included: bare
  repositories on disk, and a small HTTP server in the tests (`AuthRemoteServer`) for sign-ins.
  UI tests use Avalonia's headless mode and capture screenshots; the real-window pass repeats
  some checks on the real desktop.
- **CI.** GitHub Actions builds and tests on Windows, macOS and Linux on every push that changes
  more than docs, with a time limit on each job and on its test step.
- **Packaging.** Velopack for installers and auto-update on all three platforms.

## Solution layout

`VisualCommit.slnx`, `Directory.Build.props`, `Directory.Packages.props` and `global.json` sit at
the repo root. App projects are under `src/`, test projects under `tests/`. A project is created
in the phase that first needs it. Package versions are pinned centrally in
`Directory.Packages.props`.

| Project | Contents | State |
|---|---|---|
| `src/VisualCommit.Core` | Domain models and interfaces (git calls, repositories, commits, refs, the working-tree status with its conflicts and the operation in progress, rebases, lists of picks, the backups' retention, the lane layout, settings, session state, logging, data-folder paths, how dates are shown); diffs: parsing them, building patches for chosen lines, comparing the words of paired lines; ref-name rules (`RefNameRules`); the interactive rebase's todo (`Git/RebaseTodo`); `Conflicts/`: a conflicted file's blocks (`ConflictText`); `Operations/`: the operation queue, its contracts and the operation log, failure classification and output cleaning, the prompt contracts, the lock-file scan; no UI, no process calls | Built |
| `src/VisualCommit.Git` | Git runner, git locator, call log; reading a repository and writing to it (`GitRepository`: status, diffs, stage, unstage, patches, snapshots, discard, commit; phase 3's branches, history, in-progress state, backups, remotes and the network, stash, tags, ignore; phase 4's rebases, lists of picks and reverts, Continue and Skip, conflicts and the merge tool, the backups' clean-up), cloning, the repository watcher, the progress parser; `Prompts/`: the askpass broker, its locator and the prompt classifier | Built |
| `src/VisualCommit.AskPass` | The askpass helper `visualcommit-askpass` (D82): a small framework-dependent console program with no project references, run by git and ssh; its pipe protocol (`AskPassProtocol`) is shared with the broker. `VisualCommit.Git` references it, so its app host lands next to the app and in every test project's output | Built |
| `src/VisualCommit.App` | Avalonia views, view models, theme, custom controls (the commit graph, the diff text, the conflict panes and result, the context menu's item, the masked text box), the code font; settings and session stores, log file, folder dialog, file launcher, clipboard, prompt dialogs; the operations of every repository (`RepositoryOperations`), menus, dialogs, toolbar, status bar, conflict banner, auto-fetch; the interactive rebase's list, the conflict resolver, the message dialog. Its assembly and executable are named `VisualCommit` (`VisualCommit.exe`); its namespace is `VisualCommit.App` | Built |
| `src/VisualCommit.Hosting` | GitHub, Azure DevOps and GitLab clients; token store | Phase 7 |
| `tests/VisualCommit.Testing` | Shared test helpers: temporary-repo builder, scenario repos, the 100k-commit repo, the credential test server, fakes (file launcher, clipboard, prompt handler, ssh), the hang watchdog, helpers that drive the app in headless mode, the window layout both harnesses share. Not a test project | Built |
| `tests/VisualCommit.Git.Tests` | Tests of Core and Git against real git and temporary repos, the askpass helper and the test server included | Built |
| `tests/VisualCommit.App.Tests` | Settings, session, log, data-folder and view-model tests, the operations against real git through scenarios, and headless UI tests of behaviour (menus, dialogs, drag, selection, toolbar, status bar, banner) | Built |
| `tests/VisualCommit.VisualTests` | Scripted walk-throughs of phases 0 to 2 in headless mode with screenshots, for the visual test gate. Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase3` | Phase 3's scripted walk-through, a project of its own so that it runs beside phases 0 to 2's with a headless UI thread of its own. Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase4` | Phase 4's scripted walk-through, a project of its own for the same reason (D151). Part of the default test run and CI | Built |
| `tests/VisualCommit.RealWindowTests` | The real-window pass (FlaUI). Windows-only and opt-in. It is **not in the solution file**; that is what keeps it out of `dotnet build`, `dotnet test` and CI test runs (D25) | Built |

All projects target `net10.0`, except `VisualCommit.RealWindowTests` (`net10.0-windows`).
`Directory.Build.props` turns on nullable reference types, implicit usings and warnings as errors
for every project.

## As built (after phase 4)

Phase 0 built the foundation: an empty shell, the git runner, settings, the log and both test
harnesses. Phase 1 added repositories in tabs: open, init and clone on a welcome page, the commit
graph of the whole history, the left panel of refs, the commit details, the session that restores
tabs and the window, and a watcher that follows outside changes. Phase 2 added the working tree:
the working-changes row in the graph, the stage panel with commit and amend, the diff view
(inline and side by side, syntax and word-level highlights, images, binary and very large
files), staging, unstaging and discarding by file, hunk and line, a snapshot before every
discard with a way to restore it, and a confirmation dialog. Phase 3 added the operations on
branches, remotes, stashes and tags: an operation queue per repository that also takes over
phase 2's writes; checkout, create, rename, delete, merge, fast-forward, reset, cherry-pick,
revert and abort; fetch, pull, push, force push with lease, upstreams, remotes and auto-fetch;
stash, apply, pop, drop; tags; ignoring a file; backup refs before every destructive operation;
git's and ssh's prompts as dialogs in the window; context menus on every object, double-click
actions, dragging a branch onto a branch, multi-selection of commits; the toolbar's and the
status bar's operation parts; the conflict banner; stacked dialogs. Phase 4 added rebase and
conflict resolution: rebase onto a commit or a branch from the menus and by drag, pull with
rebase chosen for one pull, the interactive rebase's list in place of the graph (reorder, pick,
reword, squash, fixup, drop) and the squash of selected commits; several picks and reverts as one
git call that git's sequencer keeps; Continue, Skip and Abort in the banner for a merge, a
cherry-pick, a revert and a rebase, and the stage panel's Continue; the conflict resolver across
the graph and right-panel area (ours and theirs above the editable result, a choice per block, a
whole side, a card for conflicts without text, mark resolved, the external merge tool); the
backups of rebases and skips and the automatic clean-up of every backup; pick heads read under
the reftable ref format too. Undo, the activity log and search are phase 5's.

### Start-up and composition

```
Program.Main                        src/VisualCommit.App/Program.cs
  └─ App (Avalonia Application)     App.axaml(.cs): loads theme resources, Fluent theme, control,
       │                            menu and dialog styles; tooltips on the overlay layer (D105)
       └─ AppSession.Start          AppSession.cs: the composition root
            ├─ FileAppLog           one log for the session
            ├─ JsonSettingsStore    loads settings.json (preferences)
            ├─ ThemeService         applies the saved theme before the window exists
            ├─ JsonSessionStore     loads session.json (tabs, recent list, window, panels; D46)
            ├─ GitCallLog           the record of every git call
            ├─ GitAccess            the pending search for git (D45)
            ├─ DialogHostViewModel  the window's dialog stack (D69, D104)
            ├─ PromptDialogPresenter, PromptDialogs
            │                       git's and ssh's prompts as dialogs ("Prompts and credentials")
            ├─ AskPassBroker        the session's prompt channel (PromptDialogs.CreateBroker)
            ├─ TabServices          what every tab uses: GitRepositoryProvider, the folder
            │                       dialog, settings, the session, DateDisplay, the log,
            │                       RecentRepositories, the dialog host, the OperationLog, the
            │                       prompt broker, FileLauncher, ClipboardService, the call log
            ├─ MainWindowViewModel  the tabs, restored from the session; the menu layer
            │                       (MenuLayerViewModel) and the AutoFetchScheduler, started
            └─ MainWindow           created, given the session (UseSession), not shown
```

- `AppSession` is one running instance of the app on one data folder. New services are created
  and wired there, by hand; there is no dependency-injection container.
- `App.OnFrameworkInitializationCompleted` starts a session only when there is a desktop lifetime,
  and disposes it when the desktop exits. Headless tests have none: they call `AppSession.Start`
  themselves with a temporary data folder and fakes
  (`AppSession.Start(paths, application, folderPicker, launcher, clipboard, promptHandler)`; the
  last four are optional and replace the folder dialog, the file launcher, the clipboard and the
  prompt dialogs).
- `AppSession.Initialization` is `MainWindowViewModel.InitializeAsync`: it looks for git, then
  opens the repositories of the tabs restored from the session and loads their whole history.
  Tests await it before they look at the window.
- Services that need git hold the `GitAccess` and ask it for the runner when they need it
  (`GetRunnerAsync`), which waits for the search and throws `GitUnavailableException` when git
  was not found. Nothing assumes git is there.
- `AppSession.Dispose` confirms the close (`MainWindowViewModel.ConfirmClose`: the window does
  not wait for running operations) and closes the window (which saves its placement), then
  `MainWindowViewModel.CloseAll`, which stops auto-fetch, disposes every tab (logging each graph's
  frame times, D50; a tab's queue stops its network operation) and waits a few seconds for a
  cancelled clone to remove its folder; then it disposes the prompt broker and closes the log
  (`FileAppLog.Close`), so that a git call that ends later writes nothing (D132).
- `Program.Main` logs unhandled exceptions to the same log file.

### Repository tabs (D45)

| Type | Role |
|---|---|
| `MainWindowViewModel` | `Tabs` (never empty) and `ActiveTab`; `NewTab`, `ActivateTab`, `CloseTab`; saves the tabs and the active one to the session whenever they change; the theme switch and the git status as in phase 0; `Dialogs`, the window's dialog layer; `Menu`, the menu layer; `AutoFetch`; `NamesRepositoryInDialogs` (more than one tab has a repository: dialogs name their repository, "In &lt;repo&gt;"); `TryClose` and `ConfirmClose` (closing the window while operations run, see "Operation queue"). A tab closed while an operation the user started runs in it is kept until its queue is idle |
| `RepoTabViewModel` | One tab: `Title` ("New tab" or the repository's folder name), `CanClose`, the `Welcome` page, the open `Repository` or null. `LeftPanel`, `Details`, `Changes` (the stage panel), `ShowsWorkingChanges`, `BranchText`, `Toolbar` and `OperationStatus` are the repository's, or empty ones (an empty toolbar has every command disabled; an empty status says "Ready"). Since phase 4 `ShowsRightPanel` (false while the conflict resolver is open), `ShowsDetailsPanel`, `ShowsStagePanel` and `GraphColumnSpan` (2 while the resolver is open, else 1) say what the right-hand columns show (D140). A tab restored from the session shows its folder's name at once (`ShowPending`) and opens it once git is found; one that fails keeps its folder in the session (`FailedPath`) until the user opens something else in it |
| `WelcomeViewModel` | The welcome page (D42): Open and Init through `IFolderPicker`, the clone form with its progress (`CloneProgress` from git's stderr) and Cancel, the shared recent list. A clone opens a prompt scope ("Clone", `PromptMode.Ask`) and passes its variables to `CloneAsync`, so an ssh or HTTPS clone asks in the window |
| `RepositoryViewModel` | One open repository: its `Graph` (`CommitGraphData`), `SelectedIndex`, `Selection` (`GraphSelection`, by SHA, D88) and `MultiSelection`, `IsWorkingRowSelected` and `WorkingRowText`, `ScrollOffset` (kept per tab), `LeftPanel`, `Details`, `Status` (`WorkingTreeStatus`) with `HasWorkingChanges`, `Changes` (the stage panel), the open `Diff`, `Refs` as last read, `BranchText`, the watcher and the refreshes, the Q1 measurements; phase 3's `Queue`, `Operations` (`RepositoryOperations`), `OperationStatus`, `Toolbar`, `Banner` (`ConflictBannerViewModel`), `MenuContext` and the menus' entry points; phase 4's `InteractiveRebase` (the list, or null), `Resolver` and `IsResolverOpen`, `ShowsGraph`, `ShowsInteractiveRebase` and `ShowsDiff` (what the graph area shows), `MergeTool`, `BackupsToKeep` (see "The graph area: the list and the resolver"). Split over `RepositoryViewModel.cs`, `.Menus.cs` (menus, double-clicks, the drop), `.Selection.cs` (multi-selection), `.InProgress.cs` (banner with Abort, Continue and Skip and their questions, working-row text, left-panel sync, the stopped commit's message, the questions about conflict markers) and `.GraphArea.cs` (phase 4's hooks: the list, the resolver, leaving the graph area, the focus when they close, `merge.tool`, the clean-up at opening). Disposing it (`IsClosed` from then on) disposes its queue and cancels all its other work |
| `RecentRepositories` | The recent list, newest first, at most 10, kept in the session |
| `GitRepositoryProvider` | `IRepositoryProvider` with real git: `OpenAsync`, `InitAsync`, `CloneAsync` (with the clone's prompt variables) through `GitRepository` |

The window's regions show the active tab: `MainWindow.axaml` binds the left region to
`ActiveTab.LeftPanel`, the graph region (`CommitGraphView`) to `ActiveTab`, and the right region
to `ActiveTab.Details` (`RightPanelView`, while `ShowsDetailsPanel`) or, while the working-changes
row is selected, `ActiveTab.Changes` (`WorkingChangesView`, while `ShowsStagePanel`); the status
bar's branch to `ActiveTab.BranchText`. `CommitGraphView` shows `WelcomeView` for a tab without a
repository, and for one with a repository one of: `RepositoryGraphView` (`ShowsGraph`), the
interactive rebase's list `InteractiveRebaseView` (`ShowsInteractiveRebase`), `DiffView`
(`ShowsDiff`, also over the list) or the conflict resolver `ConflictResolverView`
(`IsResolverOpen`), with the conflict banner above whichever shows, and nothing while a restored
tab is still opening. While the resolver is open, `CommitGraphView`'s `Grid.ColumnSpan` is bound
to `GraphColumnSpan` and covers the right panel's column too, which keeps its width, and the right
panel and `RightSplitter` are hidden (D140), so `PanelLayout.Fit` and the widths in the session
are unchanged. The toolbar binds to `ActiveTab.Toolbar`, the status bar's operation part to
`ActiveTab.OperationStatus`. Over the main grid lie, in this order, the menu layer (`MenuLayer`),
the drag layer (`DragLayer`) and the dialog layer (`DialogLayer`, an `ItemsControl` over
`Dialogs.Open`), shown while a dialog is open.

### From a repository to the screen

1. `RepoTabViewModel.OpenAsync(path)` starts a stopwatch for Q1 and asks the provider, which
   waits for git and calls `GitRepository.OpenAsync`: one `git rev-parse` gives the working tree,
   the git folder and the common git folder, or `NotARepositoryException`.
2. `RepositoryViewModel.StartAsync` starts the watcher, then reads the refs
   (`ReadRefsAsync`: `for-each-ref`, `remote`, `stash list`, `symbolic-ref`, `rev-parse HEAD`, in
   parallel) and shows them in the left panel and the status bar. The working-tree status
   (`ReadStatusAsync`) is read beside them and shown once the refs are; it decides the
   working-changes row.
3. At the same time `LoadCommitsAsync(Task<RepoRefs>, ...)` runs one `git log --date-order`
   (D47), which does not need the refs (it names `HEAD` with `--ignore-missing`, for an unborn
   HEAD). `CommitPager` parses each line as git prints it, waits for the refs before the first
   page, merges the stashes in by date (D54) and hands over pages: 100 commits, then 2,000.
4. On the loader's thread each page is laid out by `GraphLayout` (D48, D55) into `GraphRow`s;
   the page and its rows are then posted to the UI thread and appended to `CommitGraphData`,
   which raises `Changed`.
5. `CommitGraphControl` draws the rows in view. The first frame with rows tells the view model,
   which logs the time since step 1 (D50).
6. Selecting a row loads `ReadCommitDetailsAsync` into the details panel; selecting the
   working-changes row shows the stage panel instead.

View models post to the UI thread through the `SynchronizationContext` they were created on, not
through Avalonia's dispatcher, so that they stay testable without a UI; without a context (plain
unit tests) the work runs at once.

### Git layer

Contracts are in `src/VisualCommit.Core/Git`, implementations in `src/VisualCommit.Git`.

| Type | Role |
|---|---|
| `GitCommand` | One call: arguments (passed one by one, no shell quoting), working directory, optional standard input, extra environment variables (a null value removes a variable), optional line handlers for streamed output, and the `Encoding` of standard input and output (UTF-8, or `GitCommand.Latin1` for byte-exact content, D66). `DisplayText`, used in logs and errors, hides the credentials of a URL; `HideCredentials(text)` turns every URL's user information into `***`, `RemoveCredentials(text)` removes it (what "Copy URL" copies, D103) |
| `IGitRunner` / `GitRunner` | Runs a command without blocking. A non-zero exit code is returned in the `GitResult`, not thrown; `GitResult.EnsureSuccess` turns it into a `GitException` that carries git's own standard error and, since phase 3, its standard output (`StandardOutput`: a merge writes its messages there) |
| `GitResult` | Exit code, standard output, standard error, duration |
| `GitCallRecord`, `IGitCallLog` / `GitCallLog` | The record of every call, the last 2,000 kept in memory (`OperationLog` keeps the last 500 operations, so an old operation loses its calls first) (command, folder, outcome, exit code, timing, the first 16 KB of each output stream with credentials hidden, D103, and `OperationId`, the queue's operation the call belongs to, or null for a read outside one). The activity log (T4, phase 5) will show these, grouped by operation |
| `GitLocator`, `GitSearchContext`, `GitDetection`, `GitVersion` | Finding git and checking its version |
| `IGitRepository` / `GitRepository` | One open repository. Reads: `ReadRefsAsync`, `ReadHeadAsync`, `LoadCommitsAsync`, `ReadCommitDetailsAsync`, `ReadStatusAsync`, `ReadDiffAsync`, `ReadFileAsync`, `ReadFileSizeAsync`. Writes: `StageAsync`, `UnstageAsync`, `ApplyPatchAsync`, `SaveSnapshotAsync`, `DiscardAsync`, `RestoreSnapshotAsync`, `CommitAsync`. `CreateWatcher`; static `OpenAsync`, `InitAsync`, `CloneAsync`. Phase 3's and phase 4's members are listed in "Phase 3's repository members" and "Phase 4's repository members" below. Split over `GitRepository.cs`, `.Refs.cs`, `.Commits.cs`, `.Clone.cs`, `.Changes.cs` (status, diffs, file versions), `.Writes.cs`, phase 3's `.Backups.cs`, `.Branches.cs`, `.History.cs`, `.InProgress.cs`, `.Remotes.cs`, `.Stash.cs`, `.Tags.cs` and `.Ignore.cs`, and phase 4's `.Rebase.cs`, `.Sequence.cs` and `.Conflicts.cs` |
| `WorkingTreeStatus` | The staged files (HEAD → index), the unstaged ones (index → working tree, untracked as added, conflicted as `FileChangeKind.Conflicted`) and which are untracked, each list in byte order of the paths; from `git status --porcelain=v2 -z --untracked-files=all --renames`. `InProgress` (`InProgressState`) is the operation in progress, read from git's files (see "Toolbar, status bar, conflict banner and auto-fetch"). Since phase 4 `Conflicts` (a `ConflictEntry` per `u` line), `Submodules` (the paths whose `sub` field starts with `S`), `HasUnstagedTrackedChanges` (a tracked file that is not conflicted and not a submodule has unstaged changes: git's rebase Continue refuses then), `FindConflict(path)` and `SameAs` (both lists, the conflicts and the state compared) |
| `InProgressState`, `InProgressKind` | What is in progress: `Kind` (None, Merge, CherryPick, Revert, Rebase, ApplyMailbox), the commit and its subject, a rebase's branch and new base, `Remaining` (the commits a terminal's sequence or a rebase has left), `Empty` (a pick or revert that changes nothing), `AbortKeepsHead` (D110). Phase 4's init properties: `Sequenced` (a `sequencer/` folder: a list), `HasPickHead` (a list with no pick head is paused between commits), for a rebase `StopKind` (`RebaseStopKind`), `NextIsSquash` (the stopped commit is the target of a fixup or squash) and `Group` (the app's group the stop is in), and `Done` and `Total` (a rebase's commit commands done and in all; a list's commits made and in all) |
| `MergeResult`, `MergeMode`, `ApplyResult`, `ResetMode`, `StashApplyMode`, `PullOutcome`, `PullResult`, `PullIntegration` | How phase 3's writes ended or what they run: a merge `UpToDate`, `FastForward`, `Merged` or `Conflicts`; a cherry-pick, revert or stash apply `Applied`, `Conflicts` or `Empty`; a pull's integration `UpToDate`, `FastForward`, `Merged`, `Rebased`, `Conflicts` (every stop of its rebase, with or without conflicted files) or `UpstreamGone`, with the commits HEAD gained, and since phase 4 `LeftOutByForkPoint` and `AutostashConflicted`; `PullIntegration` `AsConfigured` (the toolbar's Pull) or `Rebase` (chosen for one pull, D147) |
| `RebaseResult`, `RebaseOutcome`, `RebaseStopKind` | How a rebase the app ran ended (phase 4 design 1.4): `UpToDate`, `Rebased` (with the new tip, and `AutostashConflicted` when `rebase.autoStash`'s changes conflicted when put back) or `Stopped` (with the conflicted files); why a rebase stopped: `Pick`, `Rescheduled` (an untracked file was in the way), `ExecFailed`, `OwnAmendFailed` (the app's guarded amend failed: a hook refused the new message), `Edit`, `Empty` |
| `RebaseAction`, `RebaseStep`, `RebasePlan`, `RebaseList`, `RebaseListEntry` | The interactive rebase: an action per commit (Pick, Reword, Squash, Fixup, Drop), a plan (onto, the branch's expected tip and full name, the steps oldest first with a reword's or a group's message) and the list read when it opens (onto, the branch's tip, the commits oldest first with subject, author and whole message, the merge count) |
| `RebaseTodo` (Core) | Pure: the todo and message files of a plan (design 2.5, D143): full SHAs, LF, UTF-8 without BOM, an explicit `drop` per dropped commit; every reworded row and every group, fixup-only groups included, as `label vc-<n>`, `pick <target>`, `fixup <member>`… and the guarded amend (`GuardedAmend(n)`: `exec` a `git commit --amend --only --allow-empty --cleanup=whitespace --quiet --file=<rebase-merge/vc-message-n>` that runs only when `HEAD^` is `refs/rewritten/vc-<n>`), the message in `vc-message-<n>`. `SquashMessage` and `SquashPlan` build a squash of selected commits. `RebaseTodoTests` |
| `SequenceResult`, `SequenceStop`, `ContinueResult`, `ContinueOutcome` | How a list of picks or reverts ended (`None`, `Conflicts`, `Empty`, `Refused`, with the commits applied and the list's length, D145); how Continue or Skip ended (`Done` or `Stopped`, the new tip, `AutostashConflicted`) |
| `ConflictEntry`, `ConflictKind`, `ConflictSide`, `ConflictContent`, `MergeToolResult` | A conflicted path as `git status --porcelain=v2` prints its `u` line: the kind in git's words (both modified, both added, deleted by them or us, added by us or them, both deleted), whether it is a submodule, each stage's mode and id (`HasBase`, `HasOurs`, `HasTheirs`); what the resolver reads of it (the working-tree bytes, the marker size, whether a side is binary or a symbolic link); whether the merge tool staged it |
| `ConflictText` (Core, `Conflicts/`) | Pure: a conflicted file split into common segments and blocks (`Parse(text, markerSize)`: a `<` line, an optional `|` base section, one `=` line, a `>` line, each of the marker size, as strictly as git's rerere reads them; anything else is common text), each block's ours, theirs and base lines and labels, `Choose(block, BlockChoice)` (`Ours`, `Theirs`, `OursThenTheirs`, `TheirsThenOurs`), the whole ours and theirs files, `HasMarkerLines` (any marker line of the size, the markers question's test), `Decode` and `Encode` byte for byte (UTF-8, else Latin-1, line ends kept). `ConflictTextTests` |
| `BackupRetention`, `RemovedBackup`, `BackupRemovalReason` | The backups' clean-up (D139): `BackupRetention.Default` is 30 days and 50 backups; what it removed and why (older than the days, beyond the count) |
| `NetworkOptions` | What a phase 3 call is given beyond its token: the prompt scope's variables (`Environment`), where progress goes (`Progress`), settings passed as `git -c` before a remote command (`Configuration`: auto-fetch's network timeout, D125), and `PromptCancelled`. `NetworkOptions.From(context)` builds it from a running operation. Local writes take it too: a filter or hook they run can prompt (git-lfs) |
| `PushRequest` | One push of a local branch: remote, branch, the branch's full name on the remote, `SetUpstream`, `ForceWithLease` |
| `RemoteInfo`, `WorktreeInfo` | A remote with its URLs as configured (`ShownUrl` hides credentials); a worktree and the branch it has checked out |
| `BackupRequest`, `BackupEntry`, `BackupResult` | A backup batch (see "Backup refs") |
| `RefNameRules` (Core) | Branch, tag and remote names checked in C# by git's check-ref-format rules, plus no leading `-` and not `HEAD` (`BranchNameProblem`, `TagNameProblem`, `RemoteNameProblem`, each a reason such as "it contains a space"), and `FindClash` with existing names: the same name, another case, a folder clash in either direction (D96). `NamesIgnoreCase` is true on Windows and macOS. `RefNameRulesTests` compare the rules with `git check-ref-format` |
| `GitProgressParser` | A line of git's progress (`Receiving objects:  45% (9/20)`, `remote: Counting objects: …`, `Updating files: …`) as an `OperationProgress` (stage, percent, text); any other line gives null. `CloneProgressParser` keeps phase 1's contract and uses the same rule |
| `DiffTarget`, `DiffSide`, `FileVersion` | Which diff to read: a file's unstaged, staged or commit change, with its kind, old path, whether it is untracked, the commit and its first parent; and where its versions before and after are (working tree, index, a commit) |
| `FileDiff`, `DiffHunk`, `DiffLine`, `DiffParser` (Core, `Diff/`) | One file's diff as git prints it, parsed from Latin-1 text: git's header lines, the hunks with their ranges, and lines that keep their bytes (`Raw`) beside the text shown (`Text`, decoded as UTF-8, or Latin-1 when the file is not valid UTF-8). `IsBinary`, `IsNewFile`, `IsDeletedFile`, the modes, `IsVeryLarge` (D65) and `HasSameLines`. A type change, which git prints as a deletion and a creation, is read by `ParseTypeChange` into one diff of both, for showing only (D76) |
| `PatchBuilder` (Core) | The patch for chosen added and removed lines (a whole hunk is all of its lines), forward to stage, in reverse to unstage or discard (D66). Unchosen changes stay as they are on the side the patch is applied to; hunks without a chosen line are left out and the ranges after them moved. Git's header lines are kept, except that a rename's, or a creation or deletion applied only in part, becomes the header of a change to the file at its path, and a patch of only some lines leaves a change of mode out. "\ No newline at end of file" stays on the side applied to as git wrote it; on the side built, a line lacks its line feed only while it is still that side's last line, and a kept line that ends the file on one side only is written as a removed and an added line. `PatchFuzzTests` checks random choices against git |
| `WordDiff` (Core) | The words of a removed line and its paired added line that differ: a longest common sequence of words, ties matched early in the added line |
| `DiscardSnapshot` | A snapshot taken before a discard (D67): its ref under `refs/visualcommit/backup/discard-<UTC time>` (the time from the backups' clock since phase 4, D139), its commit, the paths, and which of them existed |
| `RepoRefs`, `GitRef`, `HeadState`, `StashEntry` | What the refs are at one moment. `RepoRefs.Fingerprint` changes when HEAD, a ref's target or the stashes change (D52). A local branch's upstream is read as three values (D101): `UpstreamRef` (`%(upstream)`, `refs/remotes/origin/main`, or `refs/heads/main` for a local upstream), `UpstreamRemote` (`%(upstream:remotename)`, `.` for a local upstream: `HasLocalUpstream`) and `UpstreamRemoteRef` (`%(upstream:remoteref)`, the branch's name on the remote); phase 1's short `Upstream` stays for the left panel. Never cut out of `%(upstream:short)` |
| `CommitInfo`, `CommitKind` | A row of the graph: a commit or a stash (one parent: the commit it was made on) |
| `CommitDetails`, `ChangedFile`, `FileChangeKind` | What the details panel shows; files compared with the first parent, renames detected |
| `CommitPager` | Internal: turns `git log` lines into pages and merges the stashes in. A date git cannot print (an ident without a time zone) is read as 1970-01-01 UTC, as git does, so such a commit is not lost |
| `CloneProgressParser`, `CloneProgress` | Git's clone progress lines as stage and percentage |
| `RepositoryWatcher` | `IRepositoryWatcher` (D52, D71): `FileSystemWatcher`s on the git folder, the common folder and the working tree. In the git folders, changes under `objects/` and to `*.lock` files are ignored; in the working tree, changes under `.git` are left to the others, and a burst whose paths `git check-ignore` calls all ignored is dropped. 300 ms of quiet before `Changed`, at most 2 s while changes keep coming; `RepositoryChangedEventArgs` says whether the git folder or the working tree changed. `Pause()` stops it reporting while an operation of the repository's queue runs (the queue calls it). Changes under the file-system monitor's `fsmonitor--daemon/` are ignored too: it writes a cookie there for every `git status`. `Start()` walks the working tree's folders on Linux, so the view model calls it on a background thread |
| `WindowsJob` | Internal: a Windows job object, used on Windows to stop git together with everything it started |

Rules the runner follows (see D30, D31, D36, D37, D56 and D59):

- Every call gets `LC_ALL` and `LANG` set to `en_US.UTF-8` and `LANGUAGE=en` (which gettext puts
  before `LC_ALL`, D128), so git's messages are English and parseable, `GIT_TERMINAL_PROMPT=0`,
  so git fails rather than waits for a terminal (prompts reach the app through askpass instead,
  D82), `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:`, so a command that wants an editor goes on
  without one (D68, D102), and `GIT_LITERAL_PATHSPECS=1`, so a path is never read as a pattern
  (D74). Two calls run without it: `git check-ignore`, which refuses it, and `git stash push`
  (D108: git removes the stashed untracked files with `git clean … :/`, which the variable would
  turn into a file name; the app passes stash push no path). A command's own variables override
  these defaults for that call only: the interactive rebase's `GIT_SEQUENCE_EDITOR` (D143) and the
  stopped commit's `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL` and `GIT_AUTHOR_DATE` for a rebase's
  Continue (D144). Input and output
  are UTF-8, unless the command asks for Latin-1 (`GitCommand.Encoding`, D66): diffs, patches and
  blobs then travel byte for byte. Standard error is always UTF-8.
- Calls that only read also get `GIT_OPTIONAL_LOCKS=0` (from `GitRepository`), so a read never
  rewrites the index and cannot set off the app's own watcher (D47). `git diff` refreshes and
  writes the index even so (Git 2.36), so a file's unstaged diff is read with the plumbing
  `git diff-files -p`; a staged one with `git diff --cached`, an untracked one with
  `git diff --no-index -- /dev/null <path>`. Every diff passes `--no-color --no-ext-diff
  --no-textconv --src-prefix=a/ --dst-prefix=b/ --unified=3`, so the user's configuration cannot
  change what is parsed and applied.
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
  if git could not be stopped. A cancel that comes after git has exited by itself (or so late that
  git still exits with success) and after its output was read stops nothing: the call keeps its
  result, so a push that went through is not reported as cancelled.
- The record of a call (`GitCallRecord`) has the credentials of every URL in its output hidden
  before the output is cut to 16 KiB, and a URL's authority that runs on to the cut is left out
  (D103, D129). It names the operation the call belongs to: `GitOperationScope.Current`, an
  `AsyncLocal` the queue sets around an operation's delegate, which flows into the runner's
  thread-pool work.
- Once git has exited, its output is read for as long as it keeps coming or a line handler is
  still busy with it, and given up 2 seconds after the last of it (D56). A process that git left
  behind (a hook's background job) cannot hold a call up, and git's own output is never dropped
  because a handler was slow.

#### Phase 3's repository members

Each runs inside one operation of the queue (see "Operation queue"); `IGitRepository.cs`
names each member's git command and design section in its doc comment. A write throws
`GitException` with git's own text when git refuses; a merge's, pick's, revert's, pull's or
stash apply's conflicts are returned, not thrown: git failed and the index now has unmerged
entries that it did not have before. A merge, pick or revert that git leaves in progress with
nothing conflicted (rerere's `autoUpdate`, a hook that refused) counts as stopped too (D128).
Names that start with `-` go after `--end-of-options` (`Positional`; `git reset` refuses the
marker and checks its revision itself). Calls that talk to a remote (`RemoteCommand`) get the
operation's prompt variables, `NetworkOptions.Configuration` as `-c` settings, and report git's
progress lines; local writes (`WriteCommand`) get the prompt variables only, and report no
progress.

| Group | Members and their git commands |
|---|---|
| Reads (`GIT_OPTIONAL_LOCKS=0`) | `ReadHeadAsync` (HEAD's branch and commit only), `CountDetachedCommitsAsync` (`rev-list --count HEAD --not --branches --remotes --tags`), `IsAncestorAsync` (`merge-base --is-ancestor`), `CountCommitsAsync`, `ResolveAsync` (`rev-parse --verify --quiet`, unpeeled), `ReadWorktreesAsync` (`worktree list --porcelain`), `ReadRemotesAsync` (`git remote` and `git config --null --get-regexp` of every `remote.<name>.url` and `.pushurl`: URLs as configured, never `git remote -v`, which applies `insteadOf` rules, D129) |
| Backups (`.Backups.cs`) | `SaveBackupRefsAsync`, `SnapshotForHardResetAsync`, `SnapshotIndexAsync`, `SnapshotTrackedChangesAsync` (see "Backup refs") |
| Branches (`.Branches.cs`) | `CheckoutBranchAsync` (`switch --no-guess <b>`), `CheckoutTrackingBranchAsync` (`switch --create <b> --track refs/remotes/<r>/<b>`), `CheckoutDetachedAsync` (`switch --detach`), `CreateBranchAsync` (`branch <name> <start>`, or `switch --create` to check it out), `RenameBranchAsync` (`branch --move`), `DeleteBranchAsync` (`branch --delete --force`), `FastForwardBranchAsync` (`fetch --no-auto-maintenance --no-write-fetch-head . <target>:refs/heads/<b>`, which refuses a non-fast-forward and a branch checked out in any worktree, D94), `MergeAsync` (the message from `git fmt-merge-msg` for the short name, then `merge --no-edit --no-log -m <message> <full ref>`, with `--no-ff` or `--ff-only` for the other modes, D94) |
| History (`.History.cs`, `.InProgress.cs`) | `ResetAsync` (`reset --soft <sha> --`, `reset --mixed --quiet <sha> --` or `reset --hard <sha> --`), `CherryPickAsync(commit)` (`cherry-pick <sha>`), `RevertAsync(commit, isMerge)` (`revert --no-edit [-m 1] <sha>`) for one commit; several go through phase 4's list overloads with git's sequencer (D136, below). `Empty` for a pick that changes nothing (`CHERRY_PICK_HEAD` left), while Git 2.36's empty revert leaves nothing and throws (D117). `AbortAsync(kind)` (`merge`, `cherry-pick`, `revert`, `rebase` or `am --abort`) |
| Remotes and the network (`.Remotes.cs`) | `FetchAsync(remote)`: `fetch --progress <r>`; for null one such call per remote in `git remote`'s order, without remotes whose `skipFetchAll` is set, every call but the last with `--no-auto-maintenance` (all of them for auto-fetch), going on after a failure and throwing one `GitException` at the end with an `error: Could not fetch <r>` line per failed remote; a prompt the user cancelled stops it (D109). Never `--prune` or `--tags` (D99). `IntegratePullAsync(branch)`: pull's second step (below). `PushAsync(PushRequest)`: `push --progress [--set-upstream] [--force-with-lease=<remote ref> --force-if-includes] <r> refs/heads/<b>:<remote ref>` (D100). `SetUpstreamAsync`, `UnsetUpstreamAsync`. `DeleteRemoteBranchAsync` and `DeleteRemoteTagAsync`: `push --progress --force-with-lease=<ref>:<expected> <r> --delete <ref>` (D100). `AddRemoteAsync` (`remote add -- <name> <url>`), `RenameRemoteAsync`, `SetRemoteUrlAsync`, `RemoveRemoteAsync`, `PushTagAsync` (`push --progress <r> refs/tags/<t>:refs/tags/<t>`). A local upstream (remote `.`) is never pushed to |
| Stash (`.Stash.cs`) | `StashAsync` (`stash push [--include-untracked] [--message <m>]`, without `GIT_LITERAL_PATHSPECS`, D108; returns the new stash commit, or null for "No local changes to save"), `ApplyStashAsync` (`stash apply`, `stash pop`, or `stash pop --index` followed by a plain pop when git refuses the index part and changed nothing, D91), `DropStashAsync`. A stash is passed by its commit and found again in `git stash list` as `stash@{n}`; one no longer listed fails with "This stash no longer exists." |
| Tags (`.Tags.cs`) | `CreateTagAsync` (`tag <t> <target>^{commit}`, or annotated `tag --annotate --cleanup=whitespace --file=- <t> <target>^{commit}` with the message on standard input; the target is peeled, D121), `DeleteTagAsync` (`tag --delete`) |
| Ignore (`.Ignore.cs`) | `AppendToGitignoreAsync(pattern)`: no git call; appends the escaped line to the working tree's `.gitignore` (creating it, with the file's own line ending, after a line feed when it lacks one, nothing when the line is there); refuses a `.gitignore` that is a symbolic link (D128) |

Pull (D84) is `FetchAsync(<upstream's remote>)` (skipped for a local upstream), then
`OperationContext.EndCancelWindow()`, then `IntegratePullAsync`, which does what `git pull` would
after its fetch, chosen from `pull.ff`, `branch.<b>.rebase` and `pull.rebase` (booleans read as
git reads them): `pull.ff=only` → `merge --ff-only <head>`; a rebase setting (`true`,
`interactive`, `merges`) → `merge --ff-only <head>` when the branch has nothing of its own,
otherwise `rebase [--rebase-merges]`; otherwise, also when nothing is set (D78),
`merge --no-edit [--ff | --no-ff] <head>`. `<head>` is `FETCH_HEAD`, so a merge commit gets
`git pull`'s message; for a local upstream it is `UpstreamRef`, with the message
`git fmt-merge-msg` writes. A value the app does not know (`preserve`, a typo) fails before
anything changes ("pull.rebase is set to preserve, which this app does not understand."). When
the fetch marked nothing for merge (the upstream was deleted on the remote), nothing runs and
the result is `UpstreamGone` (D129). `PullParityTests` compare the result with `git pull`'s for
each setting.

Since phase 4 (D147) `IntegratePullAsync(branch, integration, beforeRebase)` takes the
integration: `AsConfigured` is the above (the toolbar's Pull, and phase 3's overload without a
callback); `Rebase` ("Pull from {up} (rebase)") reads none of git's pull settings, as `git pull
--rebase` ignores them: `merge --ff-only <head>` when the branch has nothing of its own,
otherwise `git rebase` without arguments, which replays onto the upstream from its fork point
(`git rebase FETCH_HEAD` would replay commits a force push removed upstream). Before either pull
rebases, `beforeRebase(oldTip)` is called, where the app saves the rebase's backup. Both classify
the rebase with `ClassifyRebaseAsync` (below), so every stop is `Conflicts`, with or without
conflicted files. Before the rebase the commits git's fork point will leave out are counted:
`rev-list --count --no-merges --right-only --cherry-pick <upstream>...HEAD` less the same count
with `^$(git merge-base --fork-point <upstream> HEAD)`, kept as `PullResult.LeftOutByForkPoint`
(0 without a fork point; counted before the rebase, so that a commit the rebase drops as empty is
not taken for one the fork point left out). `PullRebaseParityTests`, `PullRebaseSettingsParityTests`
and `PullRebaseForkPointParityTests` compare the chosen rebase pull with `git pull --rebase` (no
settings, `pull.ff=only`, `pull.rebase=false`, `pull.rebase=merges`, a stop without a conflicted
file, an autostash that conflicts, a force-pushed upstream, the fork point) on both gits; phase
3's class for `pull.rebase=true` is now `PullRebaseTrueParityTests`.

#### Phase 4's repository members

As phase 3's: each runs inside one operation of the queue, takes the operation's `NetworkOptions`
(a filter or hook can prompt) and a token, throws `GitException` with git's own text when git
refuses and changes nothing, and returns a stop. `IGitRepository.cs` names each member's git
command and design section.

| Group | Members and their git commands |
|---|---|
| Rebase (`.Rebase.cs`) | `RebaseAsync(onto)`: `rebase <sha>`, never git's `<branch>` argument (`git rebase <upstream> refs/heads/<b>` rebases a detached HEAD, and `<b>` checks the branch out and stays there even after `--abort`) and no option that changes git's configured behaviour (`rebase.autoStash`, `rebase.backend`, `rebase.updateRefs`, the hooks are the user's, D142). `ReadRebaseAutoStashAsync` (`config --type=bool --get rebase.autoStash`). `ReadRebaseListAsync(onto, branch, fromCommit)`: "from here" `log --reverse --topo-order <onto>..<tip>`; onto a target `log --reverse --topo-order --no-merges --right-only --cherry-mark <onto>...<tip>`, less the commits marked `=` (their patch is upstream) that do not start empty, as git's sequencer leaves them out (an empty commit of the branch is kept), each with its subject, author and whole message (`%B`, re-encoded to UTF-8); `rev-list --merges --count <onto>..<tip>` for the merge check. `InteractiveRebaseAsync(plan)` (below) |
| Lists (`.History.cs`) | `CherryPickAsync(IReadOnlyList<string>)` (`cherry-pick <A> <B> <C>`, oldest first) and `RevertAsync(IReadOnlyList<string>)` (`revert --no-edit <C> <B> <A>`, newest first, no merge commits), one call for the whole list (D136); how it ended is read from git's files (D145): exit 0 `None`; a pick head with conflicted files `Conflicts`; a pick head, nothing conflicted and the index equal to HEAD `Empty`, the index not equal (rerere, a refusing hook) `Conflicts` with 0 files; no pick head, `sequencer/` and `MERGE_MSG` an empty revert, `Empty`; no pick head, no `MERGE_MSG`, HEAD at `sequencer/head`: refused at its start, so `--quit` removes the `sequencer/` git wrote anyway and keeps the user's changes (`--abort` would drop staged ones), and git's failure is thrown; HEAD moved: `Refused` (a later commit git could not apply without a conflict). `QuitSequenceAsync(kind)` |
| Continue and Skip (`.Sequence.cs`) | `ContinueAsync(state, message)`: a merge, or a pick or revert with its pick head, is committed with the message (`commit --file=- --cleanup=whitespace`, phase 3's commit; a list then runs `cherry-pick --continue` or `revert --continue`, which leaves the committed line out); a paused list runs `--continue` (one stopped on a refused commit throws: git would leave it out without a word); a rebase as below. `SkipAsync(state)`: `cherry-pick --skip`, `revert --skip`, `rebase --skip`, or for `OwnAmendFailed` `rebase --continue` without the new message. `ReadOperationMessageAsync(state)`: `MERGE_MSG` without git's trailing comment block (from a `{c} Conflicts:` line to the end, `{c}` being `core.commentChar`; the commit's own `#` lines stay, D68); a rebase's `rebase-merge/message` with no line removed beyond newer git's `# Conflicts:` block, or the group's `rebase-merge/vc-message-<n>` inside a group the app wrote, or without git's comment lines after a `fixup` or `squash` from elsewhere; null for a paused list and `git am` |
| In progress (`.InProgress.cs`, `.Branches.cs`) | `ReadInProgressAsync` (part of `ReadStatusAsync`, below) and `ReadPseudoRefAsync(name)`, the one place that reads `CHERRY_PICK_HEAD` and `REVERT_HEAD` (D146): the file first; only when it is missing, `MERGE_MSG` or `sequencer/` exists and the repository's `extensions.refStorage` is `reftable` (read once per repository, lazily), `rev-parse --verify --quiet --symbolic-full-name <name>` must print exactly the name (a branch named `CHERRY_PICK_HEAD` must not answer), then `rev-parse --verify --quiet <name>`. A files repository with nothing in progress makes no git call. `PseudoRefTests` run a real reftable repository on a git of 2.45 or later (CI's) and assert on 2.36 that git refuses `--ref-format`; `PseudoRefProbeTests` check the rules with a recorded runner |
| Conflicts (`.Conflicts.cs`) | `ReadConflictAsync(entry)`: the working-tree bytes (a link's target; none for a missing file), the marker size (`check-attr -z conflict-marker-size`, default 7), whether a side's blob is binary (a NUL in its first 8000 bytes, `cat-file blob`) or a symbolic link. `ReadConflictSideAsync(entry, side)`: `cat-file --filters --path=<p> <stage id>`, the working-tree form, writing nothing. `WriteResolutionAsync(path, bytes)`: writes the file byte for byte (never through a symbolic link) and `add -- <p>`. `TakeSideAsync(path, side)`: with both sides present (binary, link) `checkout --ours|--theirs -- <p>` then `add`, after a D67 snapshot when the file differs from that side (`hash-object`); with one side ("Keep the file") the file git left is staged as it is, and checked out from that side only when missing. `RemoveConflictedAsync(path)`: `rm -- <p>`, after a D67 snapshot when the file matches none of the stages. Never `git restore --ours`, which deletes a file whose side is missing. `ReadMergeToolAsync` (`config --get merge.tool`). `RunMergeToolAsync(path, tool)` (below). `ReadStagedConflictMarkersAsync(paths)`: `diff --cached --check -- <paths>`, its `leftover conflict marker` lines (exit 2 is a result) |
| Backups (`.Backups.cs`) | `CleanUpBackupsAsync(retention, keep)` and its read half `FindBackupsToRemoveAsync` (see "Backup refs") |

**The interactive rebase** (`InteractiveRebaseAsync(plan)`, D143): it refuses
(`RebaseHeadMovedMessage`) unless HEAD is on the plan's branch at the tip the list was read from;
reads the own messages the todo needs (`log -1 --encoding=UTF-8 -z --format=%B`, so a message in
another encoding is not mangled); writes `RebaseTodo`'s todo and message files, UTF-8 without BOM,
into a new folder `visualcommit-rebase-<guid>` under the system's temp folder; and runs `git rebase
-i <onto>` with `GIT_SEQUENCE_EDITOR` set for this call to a shell function, `vc() { cp
'<folder>/'vc-message-* "$(dirname "$1")/" && cp '<todo>' "$1"; }; vc` (git runs the editor as
`sh -c '<editor> "$@"'`, so a plain chain of `cp`s would get the todo path as a third operand; one
`cp` for every message file keeps the value under Windows' 32,767 characters; paths in forward
slashes, single-quoted, `'` written `'\''`). The messages then live in `rebase-merge/` and go
with it, so a Continue from a terminal works the same; the temp folder is deleted when git
returns. When git refused the app's own todo (exit 1, a rebase folder it made, no `done`, `error:
invalid line` or `rebase.missingCommitsCheck`'s warning), the app logs it at Error, runs `rebase
--abort` and throws git's output. The todo has no `update-ref` lines, so other branches never
move, even with `rebase.updateRefs`. A squash of selected commits is an interactive rebase of
`RebaseTodo.SquashPlan`; the squashed commit is `NewTip~(list length − N)`.

**How a rebase ended** (`ClassifyRebaseAsync`, `.Branches.cs`, shared by every rebase command:
onto, interactive, squash, Continue, Skip, a pull's): whether `rebase-merge/` or `rebase-apply/`
existed is read before the call. A folder after the call is `Stopped` with the conflicted files of
`ls-files --unmerged`, whatever the exit code (an `edit` stop exits 0); when a folder was there
before (Continue, Skip), a fatal exit (128 or more) and git's `You must edit all merge conflicts`
throw instead, as they changed nothing. No folder, exit 0 and a whole line of standard output
`Current branch <b> is up to date.` or `HEAD is up to date.` (never those words inside a subject
git printed) is `UpToDate`; otherwise `Rebased` with HEAD as the new tip, and
`AutostashConflicted` when files conflict after it (only putting back `rebase.autoStash`'s changes
can leave them; told by the state, not by git's words, which differ between versions). No folder
and a non-zero exit throws. Git's `ESC[K` before `Successfully rebased` is removed from both
streams.

**Continue of a rebase** (5.4, D144): first git's own check of `--continue` is made, so that the
app never commits for a Continue git then refuses: `update-index -q --refresh`, then `diff-files
--name-only -z --ignore-submodules`; a changed tracked file that is not conflicted refuses with
`RebaseUnstagedChangesMessage` and the paths. Inside a group the app wrote, a given message is
written to `rebase-merge/vc-message-<n>` first, so that the group's amend applies it. Then:
`OwnAmendFailed` runs the amend again when `HEAD^` is `refs/rewritten/vc-<n>` (hooks run; a
refusal leaves the rebase stopped) and removes the amend git rescheduled at the head of the todo
under `rebase.rescheduleFailedExec`, then `rebase --continue`; a pick or reword stop with
something staged and nothing conflicted is committed with `commit --no-verify
--cleanup=whitespace --file=-` and the stopped commit's author from `rebase-merge/author-script`
(as git's own Continue, which runs no hook there but strips the message's `#` lines), then `rebase
--continue`, which takes that commit for the stopped one; anything else is `rebase --continue`.

**The merge tool** (`RunMergeToolAsync`, D149): refuses a path that is not unmerged or is a
submodule, then runs `git -c mergetool.keepBackup=false mergetool --tool=<tool> --no-prompt -- <p>`
(the `-c` must come before the subcommand) and waits; `Staged` when the path is no longer unmerged
afterwards. A tool git does not know or cannot find throws. Afterwards, also after a cancel or a
failure, the files git leaves beside the file (`<name>_BACKUP_<n>.<ext>`, `_BASE_`, `_LOCAL_`,
`_REMOTE_`) are deleted when they did not exist before, and after a cancel git's `_BACKUP_` copy
goes back over the file while the path is unmerged. Cancelling stops git and, on Windows, the tool
with it (the job object). Tests use a fake tool, `mergetool.fake.cmd` with `trustExitCode=true`
(`MergeToolTests`).

**Git 2.55's differences** that the code handles (CI runs 2.55, the development machine 2.36): at a
conflict stop `rebase-merge/message` ends with a `# Conflicts:` block, left out of the message the
stage panel fills; `done` and `git-rebase-todo` write each subject as a comment (`pick <sha> #
Subject`), and the app reads only the command and the SHA; an autostash that conflicts is told in
other words, so the app tells it by the conflicted files left after an exit 0; a rescheduled pick
has no `stopped-sha` and is recognised by `done` ending with the same command; a reftable
repository can exist (Git 2.45 and later).

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
- **Several commits (D88, `CommitGraphControl.Selection.cs`).** The platform's command modifier
  (`CommandModifiers`: Avalonia's `HotkeyConfiguration.CommandModifiers`, Ctrl on Windows and
  Linux, Cmd on macOS, never a test of the operating system) with a click adds or removes a row;
  Shift with a click selects from the anchor to the row; Shift with Up, Down, Page Up, Page Down,
  Home or End extends from the anchor. The working-changes row and stash rows never join a
  multi-selection (a modifier on them acts as a plain click). The selection is the two-way styled
  property `Selection`, a `GraphSelection` (`ViewModels/Graph`): an immutable, ordered list of
  SHAs with an anchor and a focus (the last row clicked that is still selected). `SelectedIndex`
  stays the single selection that phases 1 and 2 bind to and is -1 while several are selected.
  Every selected row gets `VcSelectionBrush`. `RepositoryViewModel.Selection.cs` maps the
  selection again by its SHAs when a reloaded history replaces the graph (`OnGraphSwapped`): SHAs
  no longer there are dropped, and with fewer than two left it becomes a single selection. A press
  on a row of a multi-selection keeps it, and the row is selected alone on a release without a
  drag.
- **Hit tests and events.** `HitTest(point)` gives a `GraphHit`: a ref label (with its
  `PlacedLabel`, the "+N" label included), the rest of a commit's row, the working-changes row,
  or nothing. A right-click (or the Menu key, or Shift+F10, without a position: at the bottom-left
  of the selected row's message cell) raises `ContextRequestedAt` with the hit and its point; a
  right-click selects what it is on, keeping a multi-selection it is part of. A double-click on a
  label raises `LabelDoubleTapped` (D93: a local branch checks out, a remote branch checks out
  into a tracking branch; a tag, a stash or a commit row does nothing more than a click).
  `RepositoryGraphView` turns both into the repository's menus and double-click actions.
- **Drag (`CommitGraphControl.Drag.cs`, D87).** A press on a local or remote branch label hands
  the press to the window's `BranchDragService`, which starts a drag once the pointer has moved
  far enough (see "Drag a branch onto a branch"); the source label is drawn at 50% opacity while
  it is dragged, and `DropTargetAt(point)` finds a label to drop on.
- `CommitGraphData.IsReachableFromHead(sha)` (a walk over the parents from HEAD's commit through
  the rows loaded so far, cached per graph) tells the menus whether a commit is already on the
  current branch; a commit the walk has not reached counts as not on it, so its item stays
  enabled and git decides.
- `SelectedIndex` and `ScrollOffset` bind two ways to the tab's `RepositoryViewModel`, so each
  tab keeps its own. `ScrollOffset` is clamped when read, not coerced, because a coerced value
  would be written back through the binding into the other tab's view model when the view
  switches tabs.
- UI Automation sees the graph as one element (`GraphRows`, "Commit graph"); rows are not
  exposed yet (D49).
- `RowsDrawn` and `FrameDrawn` (the time `Render` took) feed the Q1 measurements.
- **The working-changes row (D60).** With `ShowsWorkingRow` (bound to the view model's
  `HasWorkingChanges`) the control draws one more row above the first commit and counts rows as
  drawn ("display rows"): the working-changes row is display row 0 and commit `i` is display
  row `i + 1`. `SelectedIndex`, `RowBounds`, `Reveal` and `ScrollIntoView` keep counting commits,
  so phase 1's code and tests are unchanged; `WorkingRowBounds` gives the row's place and
  `IsWorkingRowSelected` (two-way) its selection. The row draws a ring in the lane and colour of
  HEAD's commit, "Working changes" in the secondary colour, and a dashed line (dashes and gaps of
  3) from the ring to HEAD's node, drawn before the rows' own lines and nodes so that they lie
  over it. When the row comes or goes while the graph is scrolled away from the top, the view
  model moves `ScrollOffset` by a row so that the rows in view stay put. Its text is the styled
  property `WorkingRowText`, bound to the view model's: "Working changes · 2 conflicts" while
  files conflict (section 5 of the design; "· 1 conflict" for one).

### Left panel and commit details

- `ViewModels/Panels/LeftPanelViewModel` turns `RepoRefs` into one flat list of rows
  (`RefSectionRow`, `RefItemRow`) that `Views/Shell/LeftPanelView` shows in a virtualised list
  (`RefList`): sections, remotes, folders from `/` in names (folders first, then items, each by
  name), ahead/behind counts, the filter, the branch HEAD is on in the accent colour. Which
  sections, remotes and folders are open is kept by key, so a refresh does not undo it. Clicking
  a ref raises `RefActivated` with its commit; the repository view model selects and reveals it.
  A panel that was never updated is phase 0's five closed sections.
- The clicked ref keeps its selection background only while the graph's single selected commit
  is that ref's target (D98): `SyncSelection(sha)`, called from the repository's
  `OnGraphSelectionChanged` with the single selected SHA (null for none or several), clears it
  otherwise. A selection made in the graph highlights no ref, since several refs can point at one
  commit.
- `LeftPanelView`'s code-behind adds the rows' menus (a right-click, the Menu key or Shift+F10
  opens the row's menu only, selecting and highlighting nothing, D114), the double-clicks (D93: a
  local branch checks out, a remote branch checks out into a tracking branch, a stash applies;
  anything else does what a click does), and `RefRowAt(point)`, the drag's drop-target lookup.
  `LeftPanelViewModel.Actions.cs` says what a row stands for (`TargetOf`, `RefNameOf`,
  `RemoteNameOf`). Branch rows carry `RefDrag.Source`, which makes them drag sources.
- `ViewModels/Panels/CommitDetailsViewModel` loads `CommitDetails` for the selected commit (a
  newer selection cancels an older load) into `Views/Shell/RightPanelView`: subject, body,
  author, date, committer and commit date when they differ, the id, parents as links
  (`ParentActivated`; a stash lists only its base), the stash's name, and the changed files flat
  or as a tree (`AppSettings.FileList`, D44). The file rows are built by `ChangedFileList`,
  which the stage panel shares. A click on a file raises `FileActivated`: the repository opens
  that file's diff for the commit (against its first parent), and the row keeps the selection
  background while the diff is open (`SelectedFilePath`).
- With two or more commits selected (`CommitDetailsViewModel.IsMultiSelection`) the right panel
  shows `Views/Shell/MultiSelectionView` in its `MultiSelectionSlot` (found by
  `DialogViewLocator` from `RepositoryViewModel.MultiSelection`, a `MultiSelectionViewModel`):
  `MultiSelectionTitle` "3 commits selected", the list `MultiSelectionList` with one
  `MultiSelectionItem` per commit in row order (short SHA and subject), and the hint "Right-click a
  selected commit for what can be done with all of them." (`MultiSelectionHint`). No file list.

### Working changes and writes (C4, D62, D67 to D71, D74 to D76)

- `ViewModels/Panels/WorkingChangesViewModel` is the stage panel (`Views/Shell/WorkingChangesView`):
  the unstaged and staged lists (two `ChangedFileList`s, flat or tree as D44 and D70 say), their
  titles, the summary and description with the counter of what is left of 72, the amend box
  (which fills empty boxes with HEAD's message), the commit button's label and state, git's
  message after a failed write (`ErrorText`, shown as `CommitError`), and the restore bar. It
  only shows what `Update(status)` gives it; every action goes to its `IWorkingChangesHost`.
- `RepositoryViewModel` is that host, and the diff view's (`IDiffHost`). Every write runs through
  `WriteAsync`, which queues it as a local operation of the repository's queue (titles "Stage",
  "Unstage", "Discard", "Restore", "Commit", "Apply lines"; `ErrorDisplay.StagePanel`,
  `PromptMode.None`, no dedupe key): one at a time, in order (D76, D81), with the watcher paused
  (D71), then the status is read again (and for a commit the refs and history too), and git's
  message, if any, goes to the stage panel, not to a dialog, and the status bar keeps "Ready". A
  write asked for after the tab closed (a confirmation was still open) does nothing; one that has
  started runs to its end, also when the tab closes. Writes: stage and unstage whole files (a
  rename unstages both its paths; a path that has vanished from the working tree and the index is
  passed over), apply a patch made by
  `PatchBuilder` for a hunk or chosen lines, discard (after `IDialogService.ConfirmAsync` and
  `SaveSnapshotAsync`; every line of an untracked file deletes it, some of its lines go through
  `git apply --reverse` like any other file's), restore the last snapshot, and commit or amend
  (`git commit --file=- --cleanup=whitespace [--amend]`, D68). After a commit that leaves the
  tree clean, the new commit is selected.
- During an operation in progress (D89, D144): the unstaged list's title becomes "Unstaged files
  (N, M with conflicts)" and a conflicted row has the tooltip "Conflict: click to resolve it"
  (`ChangedFileRow.RowToolTip`); a click on it opens the conflict resolver. Once per stopped
  commit, when the summary and description are empty, they are filled with the operation's
  message (`IWorkingChangesHost.ReadOperationMessageAsync`, the git layer's
  `ReadOperationMessageAsync`: `MERGE_MSG` without git's comment block, so a commit's own `#`
  lines stay, a rebase's own message, or a group's), split as the message dialog splits it
  (`SplitMessage`); `IsFillingMessage` is true while that read is pending. When the operation goes
  on or ends, a message still as it was filled is cleared. A merge with no conflict left can be
  committed with nothing staged; committing concludes a merge or a single pick or revert, as in a
  terminal. For a list of picks or reverts, or a rebase, the commit button is the banner's Continue
  instead (`ContinuesOperation`): "Continue cherry-pick", "Continue revert" or "Continue rebase",
  enabled as Continue is, with its reason as tooltip, and the summary may be empty;
  `ContinueMessage` is null (the operation's own message) while the boxes hold what was filled,
  else the joined message. Amend is disabled during a list or a rebase stop ("Finish or abort the
  {kind} first"). A pick or revert that changes nothing (`Empty`) keeps Commit disabled with "This
  cherry-pick changes nothing: skip it". Skip clears a message the user typed for the skipped
  commit (`SkipClearingMessageAsync`). Staging a conflicted file whose working-tree file still has
  conflict markers, and the commit that concludes an operation with markers staged, ask first
  through `IConflictMarkerQuestions` (D148).
- Phase 3 added the files' context menus (see "Context menus"): stage, discard, ignore, open,
  show in folder, copy path, and phase 4 "Resolve conflict" and "Open in merge tool" for a
  conflicted file; the hover buttons of D70 stay.
- Discard and restore (D75): files with a conflict and nested repositories (untracked paths
  ending in `/`) are left out of a discard, with a word in the question or the panel's message.
  After a discard that succeeded, the view model keeps the snapshot and a SHA-256 of each of its
  files as the discard left them, and shows the restore bar. Restore compares the files again:
  when one has changed, it asks, and saves the changed files in a snapshot of their own before
  `RestoreSnapshotAsync`; it hides the bar only when the files are back. A discard, a line discard
  and a Restore that saved a snapshot end with the backups' clean-up (D139), which keeps the
  restore bar's snapshot.
- `RefreshStatusAsync` reads the status (folded like `RefreshAsync`: a call during a run makes it
  run once more), shows it, logs `<name>: working tree: <N> staged, <N> unstaged` at Debug level
  (the real-window pass waits for it), and reads the open diff again; a working-tree diff whose
  file has left its list closes.
- Snapshots (D67, D75): `GitRepository.SaveSnapshotAsync` reads HEAD into a temporary index
  (`GIT_INDEX_FILE`), adds the files that exist and takes out those that do not, writes its tree, commits it with HEAD as parent
  under a fixed identity ("VisualCommit"), and keeps it as `refs/visualcommit/backup/discard-<UTC
  time>` (an empty old value to `update-ref`, so two never share a ref). `RestoreSnapshotAsync`
  runs `git restore --source=<snapshot> --worktree` for the files that existed and deletes the
  others. Discard runs `git restore --worktree` for tracked files and deletes untracked ones,
  with the folders they leave empty; one already gone is passed over.
- The discard confirmation is `ConfirmationViewModel` in `Views/Dialogs/ConfirmationView`, which
  covers the window with `VcBackdropBrush` and shows the card with the Cancel button focused; Tab
  goes round the card's buttons only (`KeyboardNavigation.TabNavigation="Cycle"`). Phase 3 made
  the dialog layer a stack (see "Dialogs"). Esc goes down the window's Esc chain: an open menu, a
  drag, the top dialog, then the active tab's diff unless a text box has the focus.

### Diff view (C5, D61, D63 to D65, D76)

- `ViewModels/Diff/DiffViewModel`: one open diff. Its `Target` (`DiffTarget`), the header's texts,
  `Mode` (the `DiffMode` setting), and `Body`: `Text`, `VeryLarge` (until "Show diff", which
  turns `Highlighting` off), `Binary` (sizes), `Image` (both versions' bytes, read whole up to
  20 MB), `Empty` or `Error`. Phase 2's `Conflict` body ("This file has a merge conflict") is gone:
  `RepositoryViewModel.OpenDiff` of a conflicted unstaged file opens the conflict resolver, and an
  open diff whose file becomes conflicted closes (D148). It builds the actions on the file, a hunk or the
  selected lines (`SelectedChanges`, which the view keeps up to date) and hands them to the host.
  `LoadAsync(target)` reads it again for the file as the status now lists it; a newer load wins,
  and the selected lines are dropped unless the lines read are the same (`FileDiff.HasSameLines`,
  as the text control keeps its selection then). A hunk or line action ignores clicks while one
  runs. A type change has the file's actions only (`ActsOnLines`, D76); a nested repository's
  "diff" is the `Error` body with a sentence that says what it is.
- `Views/Diff/DiffView`: the header (status letter, name, folder, origin, what is compared,
  `CloseDiffButton`), the toolbar (`InlineDiffButton`, `SideBySideDiffButton`, the file's or
  the lines' buttons) and the bodies. Images are decoded in its code-behind, which writes their
  captions. It hosts the text control with `TextDiff` (the diff only while the body is text) and
  gives it the keyboard focus when a diff opens. Every tab's diff shares the one view, so when
  its `DataContext` changes the text's selection is cleared, and both view models' too.
- `Controls/Diff/DiffTextView`, on AvaloniaEdit (D64): one pane inline, two side by side (a
  `*,1,*` grid, the halves scrolling together). `DiffLayout.Build(diff, mode)` turns the diff into
  display rows (`DiffDisplayRow`: a hunk header, a line with a cell per side, or a no-newline
  marker; a side with fewer lines in a change gets filler cells). Each pane is a `TextEditor`
  (read-only, no caret, `LineHeightFactor` 1, so a row is 15.84 high in JetBrains Mono NL at
  12) whose document has one line per display row; a margin draws the gutter (numbers and
  sign, which do not scroll sideways), a background renderer the row colours, the selection and
  the word highlights, and a colorizer the syntax colours. Hunk headers: an element generator
  makes the header line an empty object 26 high, and a layer above the text holds one bar per
  header in view with its text and real buttons (`StageHunkButton`, `DiscardHunkButton`,
  `UnstageHunkButton`); the layer is also a logical child, so the buttons get their templates.
  Selection is the view's own: a click on a number selects a row, Shift extends it, a text drag
  counts the rows it touches; `SelectedChanges` holds their added and removed lines. A new path
  scrolls to the top; a new diff for the same path keeps the scroll position; a change of mode
  keeps the line at the top of the view at the top. AvaloniaEdit's
  search panel is removed (it took Esc even while closed).
- Since phase 4 the panes are shared with the conflict resolver: `DiffPane` asks its owner
  through the internal `IDiffPaneHost` (palette, syntax runs, word highlights, each row's
  background, what each header row's bar holds with its buttons and automation ids, whether rows
  can be selected and whether the sign column shows), which `DiffTextView` and `ConflictPanes`
  both implement; `CodeTextEditor` is the editor base both use (AvaloniaEdit's `TextEditor` with
  the search panel removed and scrolling through its scroll viewer), and `DiffPalette` the colours
  both look up. Phase 2's 66 pictures stayed byte-identical through the change.
- `DiffHighlighter`/`DiffSyntax`: TextMateSharp's registry, grammars and the Dark+ and Light+
  themes are loaded once per process; each side of a diff is tokenized on its own, in order, and
  a token's style is the first matching rule's colour and font style (D64). Diffs of up to 1,000
  lines are tokenized before they are drawn, larger ones in the background. A line may take at
  most a second (a safety net), and lines over 5,000 characters stay plain.
  `DiffWordHighlights` pairs the lines of each change and asks `WordDiff`, within a budget of
  pairs of words (D76): 2 million before drawing, 40 million for a diff of more than 1,000 lines,
  whose words are compared in the background with its tokens.
- The test hooks the scripted walk-through uses: `DiffTextView.Rows`, `RowBounds`,
  `LineNumberPoint`, `TextRangeBounds`, `ForegroundAt` and `FontWeightAt`.
- A right-click on the diff's text or header opens the diff's menu (`DiffView`'s code-behind,
  `RepositoryViewModel.OpenDiffMenu` with the hunk under the pointer): the selected lines' and the
  hunk's actions (`ViewModels/Diff/DiffMenuActions`, the diff view model's own actions), the
  file's, the ignore items for an untracked file, then open file, show in folder, copy path. A
  right-press does not change the text's selection.

### Watching and refreshing (D52)

`RepositoryViewModel` starts a `RepositoryWatcher`, on a background thread, as it first reads the
refs. A change that
comes during the first load is remembered and refreshed once the load is done. A change in a git
folder runs `RefreshAsync`; one in the working tree alone runs `RefreshStatusAsync` (D71).
`RefreshAsync` reads the refs; when their fingerprint is unchanged only the left panel and status
bar are updated; otherwise the whole history is loaded into a new `CommitGraphData`, which
replaces the old one when it is complete, with the selected commit selected again. It then reads
the status. Refreshes that come while one runs make it run once more. Every operation of the
repository's queue pauses the watcher while it runs and refreshes once when it ends, with the
`RefreshScope` it names (`Refs`: `RefreshAsync`; `Status`: `RefreshStatusAsync`), also after a
failure or a cancel (D71, D81); the app's reads never write (D47), so nothing refreshes in a loop.
The files that say an operation is in progress (`MERGE_HEAD`, `rebase-merge/`, …) are in the git
folder, so a merge started in a terminal refreshes the refs and the status, and the banner shows.

### Operation queue (D81, D107, D123, D125, D126, D149)

Contracts and the queue are in `src/VisualCommit.Core/Operations` (namespace
`VisualCommit.Core.Operations`). The queue only schedules: it runs delegates, pauses and
refreshes through delegates it is given, and never starts a process, so it is tested without git
or a UI (`OperationQueueTests`, `OperationQueueUiTests`).

| Type | Role |
|---|---|
| `GitOperation` | One operation: `Title` ("Push main", "Fetch origin": the log line, the status bar's "&lt;Title&gt; failed" and "&lt;Title&gt; cancelled", the finishing dialog), `RunningText` ("Fetching from origin…"), `Kind` (`Local` or `Network`), `Refresh` (`RefreshScope`: `None`, `Status`, `Refs`), `Run` (the delegate). Init properties: `DedupeKey`, `Errors` (`ErrorDisplay.Dialog`, or `StagePanel` for phase 2's writes), `IsBackground` (auto-fetch, the backups' clean-up at opening), `Prompts` (`PromptMode`; null: `Ask`, or `Deny` for a background operation), `HasSeveralSteps`, and since phase 4 `CanBeCancelled`: a local operation that only waits for another program (the merge tool, D149) |
| `OperationContext` | What `Run` is given: `OperationId` (`op-<n>`, unique in the process; also the prompt scope's id), `CancellationToken` (`CancellationToken.None` for a local operation that cannot be cancelled, and after `EndCancelWindow`), `Environment` (the prompt scope's variables), `Progress` (at most one update per 100 ms reaches the queue's `Changed`), `EndCancelWindow()` (from now on Cancel is no longer offered; it throws `OperationCanceledException` when the user had already cancelled, so a pull never integrates after Cancel), `PromptCancelled`, and since phase 4 `SetRunningText(text)`: an operation of several steps names the step it is at ("Checking out {A}…", then "Rebasing {A} onto {B}…"); the progress of the step before is dropped, the title and log lines stay |
| `OperationReport` | What `Run` returns: `Done(message)`, `Warning(message)` (since phase 4: a success the status bar shows in the warning colour, logged `Succeeded`, such as a pull whose rebase left out commits the upstream once had), `Conflicts(message)` (a stop with conflicts is no failure, D95), `Failed(message, GitFailure)` (a failure the operation reports itself) |
| `OperationResult`, `OperationSnapshot`, `OperationFinishedEventArgs` | How an operation ended (outcome `Succeeded`, `Conflicts`, `Failed` or `Cancelled`, the status bar's message, `IsWarning`, the `GitFailure`, and `StaleLockFiles` after a cancel); what runs now (title, running text or the step's, kind, `IsBackground`, progress, `CanCancel`, `HasSeveralSteps`) |
| `IOperationQueue` / `OperationQueue` | `RunAsync(operation)`, `TryStartInBackground(operation)`, `WhenIdleAsync()`, `Current`, `Waiting`, `LastResult` (the last non-background result), `CancelCurrent()`, `DropWaiting()`, the events `Changed` (any change, progress included) and `Finished` (every ended operation, background ones too), both raised on any thread. `OperationQueue(repositoryName, gitDirectory, pauseWatcher, refresh, prompts, operations, log, findLockFiles)` |
| `GitFailure`, `GitFailureKind`, `GitFailureClassifier`, `GitOutputCleaner` | A failure as the error dialog shows it (C11): `Kind`, `Summary`, `GitOutput`, `CommandText`, `ExitCode`. `GitOutputCleaner.Clean(stderr, stdout)`: standard error split at carriage returns and line feeds, without progress lines that carry a percentage, `Cloning into` and empty lines, line ends trimmed (a leading space stays: ` ! [rejected]`), credentials hidden; the standard output cleaned the same way when nothing is left; never shortened. `GitFailureClassifier.Classify(stderr, stdout, exitCode)` reads git's English text: `CheckoutBlockedByUntrackedFiles` (untracked files in the way, alone or with tracked ones), `CheckoutBlockedByLocalChanges` (tracked files only), `PushRejectedStale` (a push's ` ! [rejected]` line with `stale info` or `remote ref updated since checkout`), `PushRejected` (`fetch first`, `non-fast-forward`; a rejected line counts only with `failed to push some refs`, so a refused `git fetch .` is no rejected push), `AuthenticationFailed`, `NotFastForward`, `IndexLocked`, `Other` |
| `GitOperationScope` | The operation of the current asynchronous flow, which the runner records with each call |
| `IOperationLog` / `OperationLog`, `OperationRecord` | The session's record of operations (T4): the last 500 in memory, each with its id, repository, title, start, duration, outcome and message; created in `AppSession` and handed to every queue through `TabServices`. Phase 5's activity log groups the call records under these by id |
| `LockFiles` | The lock-file scan after a cancel (below) |
| `IGitPromptBroker`, `PromptScope`, `IGitPromptHandler`, `GitPrompt`, `PromptAnswer`, `NullGitPromptBroker` | The prompt contracts (see "Prompts and credentials") |

Each `RepositoryViewModel` creates its queue in its constructor, with its watcher's `Pause` and
its `RefreshAsync` / `RefreshStatusAsync` as the delegates, and disposes it with the tab. Its
rules:

- **The UI thread's context.** `RunAsync` is called on the UI thread; the queue captures the
  caller's `SynchronizationContext` and runs the operation and its refresh on it, never through
  `Task.Run` (phase 2's delegates change bound view-model state). An operation starts inline when
  the caller is on that context, and the next one in line is posted. Every git call still leaves
  the UI thread inside `GitRunner` (D59). Unlike the rest of Core, the queue's awaits keep their
  context on purpose.
- **One at a time, first in first out.** An operation runs when it is first in line and nothing
  runs. Its steps: a prompt scope is opened (mode `Prompts`, or `Ask`, or `Deny` in the background;
  none for `None`) with the operation's id; `GitOperationScope` and the watcher's pause are entered
  around `Run`; the outcome is the report, or `Cancelled` for an `OperationCanceledException`,
  `Failed` with a `GitFailure` for a `GitException` (the summary is "&lt;Title&gt; failed." until the
  caller replaces it), `Failed` with the message for an `IOException` or
  `UnauthorizedAccessException`, and "Unexpected error: …" (logged at Error) for anything else; the
  queue never dies with its operation. A failure after the user cancelled a prompt of the scope is
  `Cancelled`. Then the scope closes, the stale locks are looked for (below), the `Refresh` runs
  (also after a failure or a cancel, not after the queue was disposed), the operation is recorded,
  and the next one starts.
- **Dedupe, opt-in (D123).** A `RunAsync` whose `DedupeKey` matches an operation that still
  waits returns that operation's task; it is never merged with the running one (which has already
  read what it acts on) nor with one whose cancel was asked, so a second click then queues one
  more run. Only Fetch, Pull and Push without choices set a key: `fetch:*`, `fetch:<r>`,
  `pull:<full branch ref>`, `push:<full branch ref>-><r>/<remote ref>`; everything else always
  runs, so two stages of two files are two operations.
- **Cancel.** `CancelCurrent` cancels the running operation's token while `CanCancel`: a network
  operation, or a local one that `CanBeCancelled` (the merge tool, D149, amending D81), before
  `EndCancelWindow`, not cancelled yet. Any other local operation has no token: once started it
  runs every step to its end. The runner stops git (D31, D36, D37), and on Windows everything git
  started, the merge tool included.
- **The background slot (D85, D125, D139).** `TryStartInBackground` starts an operation only when
  nothing runs or waits. A background operation is never pre-empted: the user's operations wait
  for it, and for auto-fetch the status bar says "Waiting for auto-fetch…". While one of theirs
  waits, the user can cancel it (`CanCancel`, the status bar's Cancel). It shows no running text
  or progress and opens no dialog. Two operations use it: auto-fetch and, since phase 4, the
  backups' clean-up when a repository opens ("Clean up backups", see "Backup refs"); the status
  bar's auto-fetch texts are shown only for auto-fetch's own operation, by its title.
- **Tab close (`Dispose`).** The waiting operations end `Cancelled` without running (so does the
  next one whose start was posted but has not run); a running network operation the user started
  is cancelled; a running local one goes on to its end, all its steps, without the refresh, the
  merge tool too; a background one is left to finish; the running operation's prompt scope closes,
  which closes its dialog. `MainWindowViewModel` keeps a closed tab whose operation still runs
  until its queue is idle.
- **App exit (1.8, D126).** `MainWindow.OnClosing` asks `MainWindowViewModel.TryClose`. While a
  repository of an open tab, or of a tab closed meanwhile, runs an operation that is not in the
  background, the close is cancelled: every open tab's waiting operations are dropped
  (`DropWaiting`; they do not come back), and "Finishing operations" (`FinishingDialogViewModel`)
  lists one line per running operation, "&lt;Title&gt; — &lt;repo&gt;", with "Its remaining steps will
  not run if you quit now" under a local operation of several steps. When the queues are idle the
  dialog closes and the window closes by itself. "Keep open" closes the dialog only; "Quit now"
  closes the window, and the session's disposal then disposes every tab, which cancels network
  operations; a local one's running git call ends after the process has gone, but its later steps
  do not run. A background auto-fetch never holds the window.
- **Stale locks after a cancel (D126).** A git that is killed while it updates refs leaves
  `<name>.lock` files, after which every fetch fails. After a network operation, or a local one
  that can be cancelled, ends `Cancelled` (a background one only when the user cancelled it), the
  queue calls `findLockFiles(start)` off
  the UI thread: `LockFiles.FindSince` over the git folder and the common git folder lists only
  what a fetch or push can lock (`refs/**/*.lock`, `packed-refs.lock`, `shallow.lock`,
  `config.lock`), written since the operation started (a second's margin) and unchanged 0.5 s
  later. The files go with the result (`StaleLockFiles`) for the "&lt;Title&gt; cancelled" dialog,
  or to the log at Warning when the tab has closed.
- **The record.** The app log gets `<repo>: <Title>: started` (Debug) and
  `<repo>: <Title>: <Outcome> after <N> ms: <Message>` (Info; Warning with git's first output line
  for a failure), which the real-window pass waits for, and `IOperationLog` an `OperationRecord`.

### Operations (`RepositoryOperations`, D113, D124, D135 to D147)

`ViewModels/Operations/RepositoryOperations` builds and starts every operation of one repository:
it asks the confirmations, opens the dialogs, saves the backups inside the operation, runs it
through the queue with its texts, and shows the result. One per `RepositoryViewModel`
(`Operations`). It implements `IRepositoryActions` (every public operation; the menus and the
double-clicks call it through `RepositoryViewModel.MenuActions`), `IToolbarActions` (the
toolbar's six buttons), `IRemoteActions` (the Remotes dialog), `IInProgressActions` (the
banner's Abort, Continue and Skip) and, since phase 4, `IConflictActions` (the conflict
resolver's reads and writes). Tests give recording fakes for these interfaces.

| File | What it holds |
|---|---|
| `RepositoryOperations.cs` | The constructor and the interfaces' mappings |
| `RepositoryOperations.Run.cs` | The helpers every operation shares (below) |
| `RepositoryOperations.Branches.cs` | Checkouts (local, remote into a tracking branch, a commit or tag detached), "Stash changes and check out", "Check out and merge" (the drop), create, rename, delete, fast-forward, merge, reset, cherry-pick and revert (one, or several as one git call with the list), abort (with the return to the branch a "Check out and rebase" left) |
| `RepositoryOperations.Remotes.cs` | Fetch (one remote or all), pull and since phase 4 pull with rebase, push (to the upstream, through the push dialog, onto a dropped remote branch), force push, set and stop tracking an upstream, delete a remote branch, the Remotes dialog and its add, rename, change URL and remove, push a tag, delete a tag on a remote |
| `RepositoryOperations.StashTags.cs` | Stash (the stash dialog), apply, pop, pop the newest, drop, create a tag (the tag dialog), delete a tag |
| `RepositoryOperations.Files.cs` | The file menus' actions: stage, unstage and discard (through phase 2's writes), ignore a file or its extension, open a file, show in folder, copy a SHA, SHAs, a path, a ref name or a remote's URL |
| `RepositoryOperations.Rebase.cs` | Phase 4: rebase onto (`RebaseAsync(RebaseTarget)`), "Check out and rebase" (`CheckoutAndRebaseAsync`), opening the interactive list (`OpenInteractiveRebaseAsync`) and its Start (`RunInteractiveRebaseAsync`), squash (`SquashAsync`), Continue and Skip (`IInProgressActions`), the return file, the selection after a rebase |
| `RepositoryOperations.Conflicts.cs` | Phase 4: "Resolve conflict" and "Open in merge tool" from the file menu, `IConflictActions` (mark resolved with the "changed on disk" check, take a side, delete, the merge tool, the sides' names), the questions about conflict markers (staging, Continue), the paths that were conflicted at the stop |
| `OperationTexts.cs` and `.Branches.cs`, `.Menus.cs`, `.Remotes.cs`, `.StashTags.cs`, `.Rebase.cs`, `.Conflicts.cs` | Every text: an `OperationText(Title, RunningText, ErrorTitle, FailureSummary)` per operation (design 2.33, phase 4 design section 10), the success, conflict and refusal messages, the confirmations, the error dialogs' own titles and summaries, the menus' labels and disabled reasons. Counts use the singular for one ("1 file has conflicts", D113) |

The helpers of `.Run.cs`:

- `RunAsync(text, kind, refresh, run, options)` queues a `GitOperation` with the text's title and
  running text, then shows what is left to show: a failure in the error dialog (with
  `RunOptions.DescribeFailure`'s title, summary and extra button, or else the text's `ErrorTitle`
  and `FailureSummary`, unless the operation reported its own summary), or the lock files of a
  cancel. A success shows in the status bar only; a conflict in the banner. After a deduplicated
  second call only the first caller shows the result. After the tab closed it starts nothing.
  `RunOptions`: `DedupeKey`, `HasSeveralSteps`, `Prompts`, `ShowsErrorDialog` (false for the
  Remotes form, which shows git's message in `RemoteError`), `DescribeFailure`, and since phase 4
  `CanBeCancelled` (the merge tool). An operation that saved a backup (`SaveBackupAsync` marks it)
  ends with the backups' clean-up inside it, after its work, whether the work succeeded or not
  (`CleanUpBackupsAsync`, D139): phase 3's resets, deletions, force pushes, drops and aborts too.
- `ConfirmAndRunAsync(confirmation, text, kind, refresh, backup, run)` asks the confirmation,
  then inside the operation saves the batch `backup` returns (`SaveBackupAsync`) and runs `run`;
  when the backup fails nothing else runs. The detail of every such confirmation is "A backup is
  saved first, so this can be undone."
- `GuardAsync(text, body)`: a public operation never throws. An exception outside the queue (a
  read before it, a dialog) opens the error dialog with the text's title and summary; a cancel
  shows nothing.
- `FailureOf(GitException, summary)` builds a `GitFailure` as the queue does;
  `FailureOfLastCall(context, isCall, summary)` takes the output and command of the operation's
  failed call from the session's call log (`TabServices.GitCalls`), for a git method that reports
  a failure only as an outcome (an empty cherry-pick).
- `ShowDialogAsync`, `ConfirmAsync`: on the window's dialog layer, naming the repository when
  `NamesRepositoryInDialogs`; nothing (the default answer) after the tab closed; `ConfirmAsync`
  answers true without dialog services (view-model tests).
- The error dialog's extra buttons (D95): "Stash changes and check out" for
  `CheckoutBlockedByLocalChanges` only (not after a drop's "Check out and merge"); "Pull" after a
  rejected push of the current branch to its upstream, "Fetch &lt;r&gt;" after any other rejected
  push; "Remove lock files" (`RemoveLockFilesAsync`, which deletes a listed file only when its time
  and length are unchanged since it was listed, D126; a file that cannot be deleted opens
  "Removing lock files failed"). The lock-file dialog lists the files relative to the working
  tree with `/` and shows the operation's last git call as its command.

Kinds and prompts: fetch, pull (local when its upstream is a local branch), push, force push,
deleting a remote branch, pushing and deleting a tag on a remote are network operations; every
other operation is local; the merge tool is a local one that can be cancelled. Phase 3's and
phase 4's operations ask for prompts (`Ask`); phase 2's writes, `FastForwardBranchAsync`, every
read, the watcher's `check-ignore` and the background clean-up get none.

What an operation checks when it runs (D124), since it may have waited in the queue: merge,
reset, cherry-pick, revert, pull and every rebase read HEAD inside the operation and refuse when it
is no longer on the branch (or detached state) the user saw ("HEAD moved while this waited: …";
reset also when its commit moved; pull also when the branch's upstream changed, and again after
its fetch; an interactive rebase or a squash also when the branch's tip moved since the list was
read); merge, cherry-pick, revert, "Check out and merge" and every rebase read a fresh status and
refuse while an operation is in progress ("Finish or abort the merge first") or, for a rebase,
while files conflict; Continue and Skip refuse when the state changed since the click (another
kind, another stopped commit, conflicts back); the stash checks its reason again
(`ToolbarViewModel.StashReason`). A checkout from a detached HEAD whose commits nothing else
reaches confirms first ("Leave these commits?", from `CountDetachedCommitsAsync`) and saves
`detached-<time>` inside the operation; a checkout that finds such commits it never asked about
refuses. A refusal is a failure without git output.

Notable operations: "Stash changes and check out" (D91, several steps) stashes the tracked
changes only (`--message "VisualCommit: before checking out <target>"`), checks out, and pops with
`--index` (a plain pop when git refuses the index part); a failed checkout pops the stash back at
once; a pop that conflicts ends `Conflicts` and opens "Changes re-applied with conflicts" (the one
conflict that opens a dialog, without git's output); a pop that fails otherwise opens "Changes
not re-applied"; every outcome names the stash. Several commits are cherry-picked oldest first
and reverted newest first with one git call that takes the whole list (D136, D145), so that git's
sequencer keeps what is left for the banner's Continue, Skip and Abort; a stop says "Cherry-picked
{K} of {N} commits; stopped at {short} with conflicts in {M} files" (or "…; {short} changes
nothing", "…; git could not pick {short}"), in the warning colour, and a list git refused at its
start says "Git would overwrite your changes: commit or stash them first. Nothing was
cherry-picked." A multi-revert with a merge commit is disabled. A drop's "Check out and merge"
checks out and merges in one operation. The toolbar's Pop, clicked again while the pop of the same
stash has not ended, gets that pop's task instead of popping the next stash. "Open file" asks a
plain confirmation first for a file that can run programs (`CanRunPrograms`: a fixed list of
extensions on every platform, or an executable bit, D130). "Show in folder" starts `explorer.exe`,
`open`, `dbus-send` (with `xdg-open` as the fallback) by absolute path (`FileLauncher`), never by
bare name.

Phase 4's operations (design sections 1 to 6, D142 to D147), each one local operation that asks
nothing before it starts (D135) except Skip:

- **Rebase onto** ("Rebase {b} onto {target}", `RebaseTarget(Sha, Name)`: the SHA read when the
  menu opened, the name for the texts): the refusals above, the backup `rebase-<time>/<b>` of the
  branch's old tip, `git rebase <sha>`, and 1.4's outcome in the status bar: "Rebased {b} onto
  {target}", "{b} is already up to date with {target}", a stop ("Rebase stopped: {N} files have
  conflicts", warning; the banner shows it) or a failure in "Rebase failed" with git's output and
  10.1's special summaries (a dirty tree, an untracked file in the way). A stop with nothing to look
  at (rescheduled, a failed exec, the app's amend, a refused list) also opens the error dialog with
  git's output ("Rebase stopped", or "Cherry-pick stopped" and "Revert stopped" for a list; D144
  amends D95 for these). After a rebase that ended rebased,
  started here or finished by Continue or Skip, the branch's new tip is selected when the selected
  commit was one of the branch's (design 1.5).
- **Check out and rebase** ("Check out {A} and rebase it onto {B}", several steps): unless
  `rebase.autoStash` is set (`ReadRebaseAutoStashAsync`), it refuses a working tree with tracked
  changes (submodules left out) before anything changes; then phase 3's checkout of A
  (`SetRunningText` "Checking out {A}…"), the backup and the rebase ("Rebasing {A} onto {B}…"). When
  the rebase stops, it writes the branch the user was on, as a full ref name and a line feed, into
  the rebase folder as `visualcommit-return` (`ReturnFileName`); the app's Abort reads it before
  `git rebase --abort` and checks that branch out again afterwards, and its question says so. An
  abort from a terminal ignores the file, which goes with the folder.
- **Interactive rebase**: `OpenInteractiveRebaseAsync(InteractiveRebaseRequest(Onto, BranchRef,
  FromHere, CheckoutFirst))` reads the list (`ReadRebaseListAsync`) and opens it in the graph area
  (see "The graph area"); its Start runs `RunInteractiveRebaseAsync(plan, request)`: "Interactive
  rebase of {b}", with the dirty-tree check and the checkout first for a drop's source that is not
  checked out, the backup, then `InteractiveRebaseAsync`. The list closes when the rebase is done
  or stopped, and stays open as it was when git refused before anything started ("Interactive
  rebase failed"). Done: "Rebased {b}: {N} commits".
- **Squash** ("Squash {N} commits"): the message dialog filled with git's squash message (the
  commits' messages oldest first, joined by a blank line), then the HEAD and tip checks, the backup
  and an interactive rebase from the oldest selected commit's parent with `RebaseTodo.SquashPlan`;
  "Squashed {N} commits into {short}", and the squashed commit is selected.
- **Pull with rebase** ("Pull (rebase)", dedupe key `pull:<full branch ref>:rebase`, D147):
  phase 3's pull steps with `PullIntegration.Rebase`; every pull that rebases, configured or
  chosen, saves the backup through `IntegratePullAsync`'s callback. "Pulled and rebased {b} onto
  {up}"; with commits the fork point left out "…; git left out {k} commits that {up} once had"
  (`OperationReport.Warning`), also at a stop. Auto-fetch counts it as a fetch.
- **Continue** ("Continue the {kind}") and **Skip** ("Skip {short}"), from the banner or the stage
  panel: Continue first asks when the paths that were conflicted at the stop have conflict
  markers staged (`ReadStagedConflictMarkersAsync`), then runs the git layer's `ContinueAsync` with
  the stage panel's message (null for the operation's own); Skip's question is the repository's
  (as Abort's), and inside the operation it saves `skip-<time>` (HEAD) and `skip-<time>-changes`
  (`SnapshotTrackedChangesAsync`) before `SkipAsync` (D144 amends D83's "Nothing else confirms").
  Each ends done ("Merge committed", "Cherry-pick finished", "Revert finished", "Rebased {b} onto
  {onto}"), at the next stop, or failed ("Continue failed", "Skip failed"; a rebase's Continue
  refused for other unstaged files says "Stage or stash the other changed files first: git
  continues a rebase only when nothing else is unstaged.").
- **An autostash that conflicted** is reported by every rebase the app runs (onto, interactive,
  squash, pull, Continue, Skip): "…; your changes conflicted when put back: they are also kept in
  the stash", as a stop with conflicts.
- **Resolving** ("Resolve {file}"): mark resolved, take a side, keep or delete a file, each one
  operation, "Resolved {file}"; and **the merge tool** ("Merge tool for {file}", running text
  "Waiting for {tool} to close…", `CanBeCancelled`: the status bar's Cancel, whose tooltip stays
  "Stop git", stops git and the tool): "Resolved {file} in {tool}" or "{tool} closed without
  resolving {file}". See "The graph area: the list and the resolver".

### Backup refs (D67, D83, D127, D139)

Every backup is a ref under `refs/visualcommit/backup/` (`GitRepository.BackupRefPrefix`). They are
outside `--branches --remotes --tags` (D47), so neither the graph nor the left panel shows them,
and they keep their commits from git's garbage collection until the clean-up removes them (below,
since phase 4); phase 5's undo (O2) reads what is left.

- **Names.** `refs/visualcommit/backup/<kind>-<UTC time>[-<n>][/<name>]`, the time as
  `yyyyMMdd'T'HHmmssfff'Z'`. One operation takes one timestamp and writes all its refs
  (`SaveBackupRefsAsync(BackupRequest)`) in one `git update-ref --stdin` transaction of `create`
  lines, all or none. A request has one entry without a name, or only named ones
  (`BackupEntry(Name, Target)`), and an optional `ChangesCommit`, written as
  `<kind>-<time>[-<n>]-changes`. When a name is taken (the same millisecond, or a ref or folder of
  that name; looked up with `for-each-ref`, ignoring case on Windows and macOS), the whole batch is
  retried with `-2` to `-20` on the kind-and-time part, never on the whole ref, so a name with
  slashes cannot clash with a folder and a `-changes` ref keeps its pair's name. Names of one batch
  that meet ignoring case (`origin/Foo` and `origin/foo`) get a number on the clashing part
  (`origin/foo-2`), on every platform. `BackupResult` returns the time with its suffix and the refs.
- **Snapshots** are commits made D67's way, without a ref, then saved as `ChangesCommit`: a
  temporary index read from HEAD, the paths added as they are in the working tree (one that is
  gone is removed), its tree committed with HEAD as first parent under the fixed identity
  "VisualCommit" (`snapshot@visualcommit.invalid`), the paths listed in the message's body.

| Ref | Points at | Saved before |
|---|---|---|
| `discard-<time>` | D67's snapshot (its own scheme, `SaveSnapshotAsync`) | a discard |
| `reset-<time>` | HEAD's commit | every reset |
| `reset-<time>-changes` | Hard: `SnapshotForHardResetAsync(target)`, "VisualCommit: snapshot before a hard reset to &lt;sha&gt;": every staged or unstaged tracked file, every file staged as new, and every file the index does not track that the reset overwrites or deletes, ignored ones included (one at a path the target tracks, every file in a working-tree folder where the target has a file, a file where the target has a folder), with a second parent that is a commit of the real index's tree ("VisualCommit: index before a hard reset") when the index has no conflict. Mixed: `SnapshotIndexAsync()`, a commit of the real index's tree with HEAD as parent ("VisualCommit: index before a mixed reset"), when it differs from HEAD and has no conflict | a hard reset, a mixed reset, when there is anything to save. A repository nested in a folder a hard reset would delete makes it refuse before it starts |
| `detached-<time>` | HEAD's commit | a checkout while HEAD is detached on commits nothing else reaches |
| `branch-<time>/<b>` | the branch's commit | deleting a local branch |
| `remote-branch-<time>/<r>/<b>` | the remote-tracking commit, read inside the operation; also the lease's expected value | deleting a branch on a remote |
| `remote-<time>/<r>/<b>` | each remote branch of the remote, one batch | removing a remote |
| `force-push-<time>/<r>/<b>` | the upstream's remote-tracking commit (named by its remote-tracking name), read just before the push | a force push |
| `tag-<time>/<t>` | the tag object (an annotated tag keeps its message) | deleting a tag |
| `remote-tag-<time>/<r>/<t>` | the local tag object; also the lease's expected value | deleting a tag on a remote |
| `stash-<time>` | the stash commit (its index and untracked parents with it; `git stash store` brings it back) | dropping a stash |
| `abort-<time>` and `abort-<time>-changes` | HEAD's commit (so commits a terminal's sequence made stay reachable); `SnapshotTrackedChangesAsync()`, every changed tracked file ("VisualCommit: snapshot before an abort") | aborting a merge, cherry-pick, revert, rebase or `git am` |
| `rebase-<time>/<b>` | the branch's old tip, read inside the operation (phase 4, D142) | every rebase the app starts: rebase onto, "Check out and rebase", an interactive rebase, a squash, a pull that rebases (configured or chosen, through `IntegratePullAsync`'s callback). Saved before git runs, so a rebase git refuses or finds up to date leaves one too |
| `skip-<time>` and `skip-<time>-changes` | HEAD's commit; `SnapshotTrackedChangesAsync()` (phase 4, D144) | Skip of a stopped commit (its resolution is lost) |
| `discard-<time>` from the resolver | D67's snapshot, saved by `TakeSideAsync` when a binary or link file differs from the side written over it, and by `RemoveConflictedAsync` when the file matches none of its stages (phase 4, D148) | "Use ours" or "Use theirs" on a binary file, "Delete it". The stage panel's restore bar does not offer these |

What phase 5 needs to read them: the prefix and the name scheme above (kind, time, optional
retry number, optional name; `-changes` pairs with the ref of the same kind, time and number);
that a snapshot's first parent is HEAD at the time and, for a hard reset, its second parent the
index; that the snapshot's paths are in its message; that a remote branch's or tag's backup was
also the lease of the deletion, so the remote held exactly that object when it was deleted; and
that since phase 4 the clean-up removes them, so undo offers only what is left (D137).
`BackupRefTests` cover the batches, the retry naming, case clashes and every case of the hard
reset's snapshot; `TempRepo.BackupRefsAsync()` lists the refs for tests.

**The clean-up (phase 4, D139)**: `GitRepository.CleanUpBackupsAsync(retention, keep)`.

- **A backup** is one operation's refs: the part of a name after the prefix is read with
  `^(?<kind>[a-z]+(?:-[a-z]+)*)-(?<time>\d{8}T\d{9}Z)(?:-(?<n>\d+))?(?<rest>-changes|/.+)?$`, and
  the refs that share kind, time and number are one backup (D83's scheme, D67's
  `discard-<time>[-<n>]`, `rebase` and `skip`). Its time is the one in its name; a time later than
  now counts as now. A ref that fits no scheme is never deleted and never counted. Counting
  operations, not refs, keeps one removed remote (a ref per remote branch) from pushing out fifty
  other backups.
- **The rule** (`BackupRetention.Default`: 30 days, 50 backups): with "now" from the backups'
  clock (`BackupClock`, which also names discard snapshots since phase 4, so that a test's fixed
  clock orders and ages them all), every backup older than 30 days goes, then the oldest while more
  than 50 remain. Nothing goes while an operation is in progress in any worktree of the repository
  (the git folder and every `<common git folder>/worktrees/*/`: `rebase-merge/`, `rebase-apply/`,
  `MERGE_HEAD`, `sequencer/`, a pick head); a Debug line says why it waits. A backup with a ref in
  `keep` never goes: the app passes the restore bar's snapshot (`RepositoryViewModel.BackupsToKeep`).
- **How**: one `for-each-ref --format=%(refname) %(objectname) refs/visualcommit/backup/`, then
  one `update-ref --stdin` transaction of `delete <ref> <old value>` lines: a ref changed since the
  read fails the whole transaction, nothing is deleted, and a Warning is logged.
  `FindBackupsToRemoveAsync` is its read half, which deletes nothing.
- **When**: at the end of every operation that saved a backup, inside it, after its work
  (`RepositoryOperations.CleanUpBackupsAsync`, which logs a failure at Warning and never fails the
  operation); after a discard, a line discard and a Restore that saved a snapshot; and when a
  repository has opened and its first load is done (`OnFirstLoadCompleted`): the backups due are
  read outside the queue with `FindBackupsToRemoveAsync`, and only when there are any does the
  clean-up run, as the background operation "Clean up backups" (`IsBackground`, prompts `None`, no
  refresh, `TryStartInBackground`: when the queue is busy then, the next backup or opening does
  it). Its `Run` never ends failed or cancelled. The resolver's own snapshots (above) are saved
  inside the git layer and start no clean-up; the next backup or opening takes them into account.
- **What it says**: nothing on screen (the owner's answer). The app log gets one Info line per
  backup removed, `<repo>: Removed backup <kind>-<time>[-<n>] (older than 30 days)` or `(more than
  50 backups)`, which phase 5's activity log is to show.
- `BackupCleanupTests` (Git, with a fixed clock) and `RepositoryOperationsBackupCleanupTests` (App:
  at opening, after a backup, a failing clean-up shows nothing).

### Prompts and credentials (D82, D115, D118, D130)

- **The helper** (`src/VisualCommit.AskPass`, `visualcommit-askpass <prompt>`): git runs it as
  `GIT_ASKPASS` and OpenSSH as `SSH_ASKPASS`, directly, without a shell, with the prompt as its one
  argument (a path with spaces works). With `VISUALCOMMIT_ASKPASS_DENY=1`, or without
  `VISUALCOMMIT_ASKPASS_PIPE` and `VISUALCOMMIT_ASKPASS_TOKEN`, it exits 1 at once, printing
  nothing. Otherwise it connects to the pipe (5 seconds at most) and checks that the pipe is its
  user's own: on Windows it compares the pipe's owner with the user of its process token
  (`WindowsPipeUser`, D118; `PipeOptions.CurrentUserOnly` compares the tokens' default owners,
  which differ in an elevated app and in a helper that Git for Windows' ssh starts); on macOS and
  Linux `PipeOptions.CurrentUserOnly`. It sends one line of JSON (`token`, `operation` from
  `VISUALCOMMIT_OPERATION_ID`, `prompt`, `sshPrompt` from `SSH_ASKPASS_PROMPT`), waits as long as
  the user takes for one line back (`answer`), prints the answer and a line feed and exits 0, or
  exits 1 for null, a closed pipe or a malformed line. `AskPassProtocol` holds both ends' format.
  `AskPassLocator` expects the helper next to the `VisualCommit.Git` assembly; a missing helper is
  logged at Error at start-up, and every prompt then fails as git or ssh fails without an answer.
- **The broker** (`VisualCommit.Git/Prompts/AskPassBroker`, `IGitPromptBroker`, one per app
  session, made by `PromptDialogs.CreateBroker` in `AppSession.Start`): a named pipe `vc-<16 hex>`
  (on macOS and Linux .NET makes it a Unix socket under the temp folder, whose path must stay
  under 104 characters), up to 4 connections, one request per connection (10 s to arrive, 64 KiB
  at most), for the current user only (on Windows an ACL that gives the token's user full control
  and makes it the owner). A request is answered only when its token matches the session's secret
  (32 random bytes, compared in fixed time) and its operation id names an open scope; otherwise
  null. `Begin(title, repositoryName, mode, operationId)` opens a `PromptScope` whose `Environment`
  is, for `Ask`: `GIT_ASKPASS` and `SSH_ASKPASS` (the helper), `SSH_ASKPASS_REQUIRE=force`, the
  pipe, the token and the operation id; for `Deny`: the helper, `SSH_ASKPASS_REQUIRE=force`,
  `VISUALCOMMIT_ASKPASS_DENY=1`, `GCM_INTERACTIVE=never`; for `None`: nothing. Each mode removes
  the other's variables. `GIT_TERMINAL_PROMPT=0` stays as the fallback. Disposing a scope refuses
  its open prompt and closes its dialog; a helper that goes away (git stopped it) closes the
  dialog too. The token and the answers are never logged; the log names a prompt by its kind.
- **Which calls ask:** user-started network operations and clone (`Ask`); phase 3's and phase 4's
  local operations (`Ask`, not cancellable but for the merge tool: a filter or hook such as git-lfs
  may ask, and a rebase's guarded amend runs the user's hooks); auto-fetch (`Deny`); reads, phase
  2's writes, the fast-forward's `git fetch .` and the background clean-up of the backups (`None`). Credential
  helpers, Git Credential Manager included, are left as configured and asked first by git (Q5).
- **Classification** (`GitPromptClassifier.Classify(prompt, sshPrompt)`, pure,
  `PromptClassifierTests`): `Username for '<url>': ` → `Username`; `Password for '<url>': ` →
  `Password` (with the URL's user); `Enter passphrase…` → `Passphrase` (with the key's path when
  ssh names it); a text with "Are you sure you want to continue connecting" → `HostKey`;
  `SSH_ASKPASS_PROMPT=confirm` → `Confirm`; `none` → `Notice`; anything else → `Other`, masked when
  it mentions a password, passphrase, PIN or token (`IsMasked`).
- **The dialogs** (`App/Services/PromptDialogs`, `IGitPromptHandler`): `PromptDialogPresenter`
  shows them on the window's dialog layer, on top of any open dialog (D104), posting to the UI
  thread: "Sign in to &lt;host&gt;" (`CredentialsViewModel`: user name and password for a `Username`
  prompt, the user as text for a `Password` prompt; "Sign in"), "Key passphrase"
  (`PassphraseViewModel`; "Unlock"), "Unknown host" (`HostKeyViewModel`: ssh's text in the code
  font, "Trust this host and connect?"; "Trust and connect", answers `yes`), "Git asks"
  (`PromptViewModel`; "OK"), and for `Confirm` phase 2's confirmation titled "ssh asks" with "Yes"
  (D115). A `Notice` gets no dialog: it is put in `DialogHostViewModel.Notices`
  (`PromptNotices`), which the status bar shows until the scope ends. Within one scope an answer
  is kept for at most 2 minutes for the same prompt text, and the password typed with a user name
  answers git's password prompt that follows, so a pull's second contact or a multi-remote fetch
  does not ask twice; what a scope kept goes when it ends. Cancel or Esc answers
  `PromptAnswer.Cancelled`: the scope records `UserCancelled`, the queue reports the operation as
  cancelled, and a fetch of all remotes stops before the next remote.
- **Masked boxes** (`CredentialPassword`, `PassphraseBox`, a masked `PromptBox`) are
  `Controls/SecretTextBox`: a stock text box whose automation peer reports an empty value and
  raises no text change while it is masked, so no UI Automation client can read the secret (D130).
- **What only the real-window pass and hand checks show:** the real app's own helper, pipe and
  broker in the real process environment (check 21's sign-in against `AuthRemoteServer`, check
  21b's prompts through a fake `core.sshCommand`, D115). A real ssh server's host-key question and
  Git Credential Manager's own window are checked by hand (the report's manual checklist).

### Context menus (D86, D92, D105, D106, D114, D131, D142)

- **Menus are data.** `ViewModels/Menus/MenuModel` (no Avalonia types): `MenuHeader(Text,
  ToolTip)`, `MenuSeparator`, `MenuItem(Label, Invoke, IsEnabled, DisabledReason, IsDestructive)`.
  `RepositoryMenus` builds every menu as a pure function of a `RepositoryMenuContext` (an
  immutable snapshot taken when the menu opens: refs, status with what is in progress, the
  selected commits as `CommitMenuInfo` with `IsReachableFromHead`, the toolbar's states, whether
  an operation runs, the other worktrees, a drop's `DropFacts`) and an `IRepositoryActions`:
  `ForCommit`, `ForCommits`, `ForStash`, `ForWorkingChanges`, `ForLocalBranch`, `ForRemoteBranch`,
  `ForRemote`, `ForTag`, `ForSection` (null for "Pull requests"), `ForUnstagedFile`,
  `ForStagedFile`, `ForCommitFile`, `ForDiff`, `ForEmptyGraphArea`, `ForDrop`. Items act on the
  full ref names and SHAs captured when the menu opened, never on rows. Items for later phases
  are left out, not disabled (D77, D92); folder rows, "Pull requests" and repository tabs have no
  menu. `Build(header, groups…)` never leaves a separator after the header, at the end or two in a
  row. A disabled item's reason is its tooltip; reasons, where several apply, follow the design's
  order (the object's own first, then HEAD's state). Items that open a dialog or ask first end in
  "…". Tested without a UI (`RepositoryMenusTests`).
- **Phase 4's items** (phase 4 design 7.1, D142, D147): the commit menu gains "Rebase {cur} onto
  this commit" and "Interactive rebase of {cur} from here…" after "Revert this commit"; several
  commits "Squash {N} commits…" first; a local branch other than the current one and a remote
  branch "Rebase {cur} onto {b}" and "Interactive rebase of {cur} onto {b}…" after the two merges;
  the current branch "Pull from {up} (rebase)" after "Pull from {up}", enabled as the toolbar's
  Pull; a drop of a local branch A its rebase items in a group of their own ("Rebase {A} onto {B}"
  and "Interactive rebase of {A} onto {B}…" when A is checked out, else "Check out {A} and rebase it
  onto {B}" and "… interactively…", which check A out first and so stay enabled while HEAD is
  detached; none for a remote source); a conflicted file "Resolve conflict" and, for text and
  binary conflicts only, "Open in merge tool" (disabled without `merge.tool`: "Set merge.tool in
  git's configuration to use a merge tool"). The facts they need: `RepositoryMenuContext.TipsOnHead`
  (the branch tips HEAD reaches in the loaded graph: "{b} is up to date with {target}"),
  `RepositoryMenuContext.MergeTool`, `CommitMenuInfo.HeadLineDepth` (the distance from HEAD on its
  linear first-parent line, from `CommitGraphData.DepthOnHeadLine`; null off the line: "from here"
  and Squash need it) and `DropFacts.SourceContainsTarget` (`merge-base --is-ancestor <B> <A>`,
  read before a drop menu opens; null leaves the items enabled). `ConflictMenus` builds the
  resolver's own menus: the result's (header `{file} (result)`; Undo, Redo; Cut, Copy, Paste,
  Select all) and a pane's (its title; Copy). The interactive list's row menu
  (`InteractiveRebaseViewModel.MenuFor`) opens through the same layer.
- **Entry points.** Views handle Avalonia's `ContextRequested` (a right-click, the Menu key,
  Shift+F10) and call the repository's `OpenCommitMenu`, `OpenLabelMenu`,
  `OpenWorkingChangesMenu`, `OpenEmptyGraphAreaMenu`, `OpenRefRowMenu`, `OpenFileMenu`,
  `OpenDiffMenu`, or `OpenDropMenuAsync` for a drop (`Views/Shell/ContextMenuRequests` finds the
  layer and the active repository and the menu's position). A right-click on a graph row or
  label selects the row (keeping a multi-selection it is part of); one on a left-panel row or a
  file row selects nothing (D114). A local branch's menu whose fast-forward item would be enabled,
  and a drop's menu, wait for a read (the worktrees, `git merge-base`) through
  `MenuLayerViewModel.Request`: the menu opens only if nothing cancelled it since (Esc, another
  menu or request, a press outside, a dialog, another tab) and only over its own tab (D131).
- **The menu layer.** `MainWindowViewModel.Menu` (`MenuLayerViewModel`: `Open`, `Close`,
  `Current`, `Position`, `OpenedBy`) is the one place a menu opens; at most one is open. It logs
  `<repo>: Context menu opened: <header> by pointer` (or `by keyboard`) at Debug, which the
  real-window pass counts. `Views/Shell/MenuLayer` draws it with Avalonia's stock `MenuFlyout` on
  the window's overlay layer (`Popup.ShouldUseOverlayLayer`, D105), so the menu is inside the
  client area, in the window's UI Automation tree and in a capture of it on every platform. The
  adapter adds: items of `Controls/ContextMenuItem`, a `MenuItem` whose automation peer offers
  Invoke (it closes the menu, then clicks the item; a disabled item throws); the presenter as
  control type Menu with the automation id `ContextMenu`, the header `ContextMenuHeader` (Text) and
  the items `ContextMenuItem`; `PlacementMode.Custom` with the position rules below and a
  `MaxHeight` of the client height less 8, so a tall menu scrolls; the first enabled item
  highlighted and focused when the menu opens, however it was opened (a menu without an enabled
  item, such as a pane's Copy with nothing selected, keeps the focus on its presenter, so that it
  does not go to the window's first focusable control, and gives it back where it was when it
  closes, phase 4); closing the menu when a
  dialog opens, the active tab changes, the wheel turns outside it or the window loses activation.
  The stock flyout brings the keyboard (Down and Up skip the header, separators and disabled items
  and wrap; Home, End, Enter, Esc), light dismiss (a press outside closes it and is not
  passed on) and the focus's return; moving the pointer over an enabled item moves the highlight
  and the focus there. After a dialog that opened over a menu closes, the focus returns to the
  element that had it before the menu.
- **Position** (`MenuLayer.PlaceMenu`): opened by the pointer, the menu's top-left is 2 right of
  and 2 below it; past the right edge it opens to the left (its right edge 2 left of the pointer),
  past the bottom above it; then it is moved to stay 4 inside the window. Opened by the keyboard,
  its top-left is at the focused row's bottom-left corner (the graph's selected row's message
  cell).
- **Look** (`Theme/Menus.axaml`, styles on the stock controls, class `vcContextMenu`):
  `VcPanelBackgroundBrush`, a 1 px `VcBorderBrush` border, corner radius 6, `VcMenuShadow`; 200 to
  520 wide, as wide as the longest label needs (a longer label is cut with "…" by
  `Controls/TrimmedTextBlock`, its whole text as its tooltip); items 26 high with 10 px side
  padding, the header 24 high in `VcTextSecondaryBrush` (cut with "…" past 340, never
  highlighted), separators 9 high; the highlighted item `VcHoverBackgroundBrush`; a destructive
  item in `VcDangerBrush` while enabled; a disabled item in `VcTextDisabledBrush`. A menu that fits
  is 8 + 24 + 26 per item + 9 per separator high (`WindowLayout.MenuHeight`).
- **Shift+F10 (D106).** Avalonia raises the context request on key-up for every gesture of the
  platform's `HotkeyConfiguration.OpenContextMenu` (`Apps` headless, `Apps, Shift+F10` on Win32).
  `MainWindow`'s tunnelling handler raises it itself, on key-down, on the focused element, only when
  that list lacks Shift+F10 (`ContextMenuKeys.AppRaisesShiftF10`), so each press opens one menu on
  every platform.
- **Tooltips on the overlay layer, app-wide.** `App`'s static constructor sets
  `ToolTip.ShouldUseOverlayLayer` on every control whose tooltip opens (unless it sets its own), so
  every tooltip, a disabled item's reason included, is drawn inside the window and in the
  real-window pass's capture (D105).

### Dialogs (D69, D104, D114)

- **The stack (D104).** `DialogHostViewModel` keeps the open dialogs in `Open` (the bottom one
  first) and the top one in `Current`; `ShowAsync(dialog)` puts a dialog on top and completes with
  its answer; `ConfirmAsync(ConfirmationRequest)` is phase 2's confirmation, unchanged;
  `CancelCurrent()` (Esc) goes to the top dialog only (`DialogViewModel.HandleEscape`: it cancels,
  or goes back a step, as the Remotes form does). The window's `DialogLayer` is an `ItemsControl`
  over `Open` with a `Panel` as its items panel, so every open dialog is drawn, each over a
  backdrop of its own; the one below takes no input. `MainWindow` keeps the element that had the
  focus when each dialog opened and gives it back when the dialog above closes; when the last one
  closes, its view is hidden before it leaves the window.
- **Views by type.** `DialogViewModel` (title, `RepositoryName` for the "In &lt;repo&gt;" line
  `DialogRepository`, `CancelCommand`, a completion task) and `DialogViewModel<TResult>`.
  `Views/Dialogs/DialogViewLocator` finds a view registered with `Register<TViewModel>` or by name
  (`ViewModels.X.FooViewModel` → `Views.X.FooView`, base types included); no dialog is registered
  in phase 3. `Views/Dialogs/DialogView` is every dialog's root: on opening the focus goes to the
  first text box (or the control the dialog names: the confirmation's Cancel), Enter runs
  `ConfirmButton` unless the focus is in a multi-line box or on a button, and its automation peer
  makes the root a control element (a pane named by the title). `DialogRow` is a list row with
  buttons of its own (the Remotes dialog's `RemoteRow`), a list item to UI Automation. Every
  dialog's buttons are `CancelButton` and `ConfirmButton` (`VcPrimaryButton`, or `VcDangerButton`
  for a destructive one). `Theme/Dialogs.axaml` holds the look: a card 400 wide at most (460, class
  `wide`, for the Remotes and Pick dialogs), the title, text, error, box, button row and code
  classes, and a radio button with a second line.

| Dialog (automation id) | View model / view | What it is |
|---|---|---|
| `ConfirmationDialog` | `ConfirmationViewModel` / `ConfirmationView` | Every confirmation (phase 2's, the backups' "A backup is saved first, so this can be undone.", "ssh asks") |
| `NameDialog` | `BranchNameDialogViewModel`, `TagDialogViewModel`, `StashDialogViewModel` (base `NameDialogViewModel`) / `NameDialogView` | "Create branch" or "Rename branch" (context line, `NameBox`, `NameError` checked as typed by `DialogText` and `RefNameRules`, shown only once the box has been edited, D114; "Check out the new branch"), "Create tag" (name and an optional message, which makes an annotated tag), "Stash changes" (message, "Include untracked files", which starts with `AppSettings.StashIncludeUntracked`) |
| `ResetDialog` | `ResetDialogViewModel` / `ResetDialogView` | "Reset main to a1b2c3d?": Soft, Mixed (checked) or Hard, each with its line; the button "Reset" in the danger style while Hard is chosen; the only confirmation of a reset |
| `PickDialog` | `PickDialogViewModel` / `PickDialogView` | "Set upstream for &lt;b&gt;": a filter and the remote branches |
| `ChoiceDialog` | `ChoiceDialogViewModel` / `ChoiceDialogView` | A choice of remote ("Push v1.0 to…", "Delete v1.0 on…") |
| `PushDialog` | `PushDialogViewModel` / `PushDialogView` | "Push &lt;b&gt;" for a branch without an upstream: the remote, the branch's name there, "Set as upstream" |
| `RemotesDialog` | `RemotesDialogViewModel` / `RemotesDialogView` | The remotes (`RemoteRow`) with their shown URLs (`RemoteUrl`, credentials hidden, D103); Edit and Add switch the card to a form whose Save waits for its operation (git's refusal in `RemoteError`); "Fetch now" queues a fetch it does not wait for; Remove opens its confirmation on top |
| `ErrorDialog` | `ErrorDialogViewModel` / `ErrorDialogView` | `ErrorTitle`, `ErrorSummary`, `ErrorRepository` ("In &lt;repo&gt;", always), `ErrorOutput` (git's cleaned output in the code font), `ErrorCommand`, `ErrorCopyButton` ("Copy output"), the optional `ErrorExtraButton`, "Close". Answers true when the extra button was clicked. A dialog without git output leaves out the output, the command and "Copy output" (D114) |
| `FinishingDialog` | `FinishingDialogViewModel` / `FinishingDialogView` | "Finishing operations": `FinishingItem` lines, "Keep open" and "Quit now" |
| `MessageDialog` | `MessageDialogViewModel` (`DialogViewModel<string?>`) / `MessageDialogView` | Phase 4's message dialog (the `wide` card): "Reword {short}", "Message of the squashed commit" or "Squash {N} commits"; `MessageSummary` with `MessageSummaryCounter` (what is left of 72), `MessageDescription` (wrapping, accepting Return); "Save" or "Squash", disabled while the summary is blank ("Write a summary"). The message is split as the stage panel splits it and joined with a blank line between summary and description (the summary alone when the description is blank) |
| `CredentialsDialog`, `PassphraseDialog`, `HostKeyDialog`, `PromptDialog` | `CredentialsViewModel`, `PassphraseViewModel`, `HostKeyViewModel`, `PromptViewModel` (base `PromptDialogViewModel`) / `CredentialsView`, `PassphraseView`, `HostKeyView`, `PromptView` | git's and ssh's prompts (see "Prompts and credentials") |

Each title has an automation id of its own (`ConfirmTitle`, `NameDialogTitle`, `ResetTitle`,
`PickTitle`, `ChoiceTitle`, `PushTitle`, `RemotesTitle`, `ErrorTitle`, `FinishingTitle`,
`MessageDialogTitle`, and `DialogTitle` for the prompt dialogs), so both harnesses find the same
elements. Since phase 4 a `ConfirmationRequest` has `CancelText` ("Cancel", or "Keep editing" for
the questions about leaving changed work), and a confirmation without a question hides its
question line (the list's "Discard the changes to this list?"). Phase 4's questions (design 7.2):
Skip ("Skip {short}?"), leaving a changed list, staging or marking resolved with markers left,
Continue with markers staged, leaving the resolver with a modified result, the merge tool over a
modified result, Mark resolved over a file changed on disk.

### Drag a branch onto a branch (D87, D120)

- `Views/Shell/DragLayer` is a `Canvas` over everything but the dialog layer that takes no input.
  It makes the window's `BranchDragService`, puts it in the Esc chain and draws what it says: a
  pill with the branch's name in its label style (filled with the lane colour for a local branch,
  outlined for a remote one) at 85% opacity, 8 right of and 8 below the pointer, and a 2 px outline
  in `VcDragTargetBrush` around a valid target.
- Sources are local and remote branch labels in the graph and branch rows in the left panel
  (`RefDrag.Source`). A press asks `BranchDragService.TryBegin`; the press does what a click does
  first (the graph selects its row). The drag starts when the pointer moves more than 4 px with the
  left button held; the service then captures the pointer on the drag layer, so the left panel's
  button gets no click and the graph's selection changes no further. Targets are local branches
  other than the source, and remote branches when the source is local; tags, stashes, HEAD and the
  "+N" label are none. Release over a target raises `Dropped` and calls the repository's
  `BranchDropMenu` (made by `RepositoryGraphView` from `RepositoryViewModel.DropMenuOpener`), which
  reads the drop's facts and opens `RepositoryMenus.ForDrop` at the release point: onto the current
  branch merge, merge with a merge commit and fast-forward; onto another local branch fast-forward
  and "Check out &lt;B&gt; and merge &lt;A&gt; into it" (both disabled when B is checked out in another
  worktree, D120); onto a remote branch push and push-and-track; and since phase 4, for a local
source, its rebase items in a group of their own (see "Context menus"). Release elsewhere, Esc or losing
  the pointer cancels. `PressFollowsDrag` keeps the press after a drag from counting as a
  double-click.

### The graph area: the list and the resolver (D140, D141, D148)

Phase 4 puts two more things in the graph area besides the graph and the diff. Both belong to the
`RepositoryViewModel`, like the diff, so a tab switch keeps them (one view serves every tab).

- **The hooks** (`RepositoryViewModel.GraphArea.cs`). `ShowsGraph`, `ShowsInteractiveRebase` and
  `ShowsDiff` say what the area shows (a diff can open over the list; the resolver covers both).
  Every path that would replace the list or the resolver (another list, the resolver, a diff from
  the stage panel) first awaits `TryLeaveGraphAreaAsync()`, which asks the list's question when it
  was changed and the resolver's when its result is modified, returns false on "Keep editing" and
  declines at once, without a question, while the list's Start waits or runs; it closes nothing
  itself. A diff of a commit's file from the right panel opens over the list without asking, and
  closing it gives the focus back to the list; a selection from the left panel, a parent link or a
  refresh moves the graph's selection behind the list or the resolver and closes neither. When the
  list or the resolver closes, `GraphFocusRequested` (`Graph`, or `ContinueFirst`: the banner's
  Continue when it is shown and enabled) tells `CommitGraphView` where the keyboard focus goes.
  `OpenDiff` of a conflicted unstaged file opens the resolver (`OpenResolver`), and an open diff
  whose file became conflicted closes. Every status read feeds the resolver (`Update(status,
  currentName, ontoName)`) and, while files conflict, reads `merge.tool` again (`MergeTool`, kept
  for the menus and the resolver).
- **The interactive rebase's list** (`ViewModels/Rebase/InteractiveRebaseViewModel`,
  `Views/Rebase/InteractiveRebaseView`, design 2.2, D141). It replaces the graph, newest commit on
  top, and never runs git: everything goes through its `IInteractiveRebaseHost` (the repository:
  `StartAsync(list, plan)`, `ShowDetails(sha)`, `ConfirmAsync`, `AskMessageAsync`, `Close(list)`;
  Start and Close act only for the list the repository shows). Header 36 (`RebaseListHeader`,
  `RebaseListCount`), rows 30 (`RebaseListRow`, automation id `RebaseRow`, named "{short}
  {subject}": the grip `IconGrip`, the action button `RebaseActionButton`, the short SHA, the
  subject, the author from 520 wide), a second line 22 under a reworded row or a group's target
  ("New message: …" or "Message: …" with `Edit…`), the base row (`RebaseBaseRow`, never moved), the
  footer 44 (`RebaseCancelButton`, `RebaseStartButton`); a list that cannot be made (merges in the
  range, nothing of its own, more than 1,000 commits) shows `RebaseListMessage` and only Cancel.
  The action button and the row's context menu open the actions through the menu layer (Pick,
  Reword…, Squash, Fixup, Drop; Squash and Fixup disabled on the bottom row and on a row whose rows
  below are all dropped); on the selected row git's letters `p`, `r`, `s`, `f`, `d` do the same.
  Squash and fixup join the nearest Pick or Reword below, indented 16 with a 2-px `VcLane0Brush`
  line to it; a group with a squash has a message (git's own, or edited with `Edit…` in the message
  dialog), a fixup-only group keeps its target's. A row moves by a drag (4 px threshold, a
  `VcDragTargetBrush` line at the gap, scrolling within 20 px of an edge) or Alt+Up and Alt+Down.
  Selecting a row selects its commit in the graph behind (`ShowDetails`), so the right panel shows
  it. Start is disabled while nothing changed ("Change an action or the order first") and when
  HEAD moved ("HEAD moved: open the list again"); it builds a `RebasePlan` and closes the list when
  the rebase is done or stopped. Esc (`EscapeOrder.InteractiveRebase`, 360) cancels a row drag
  first, else closes the list, asking first when it was changed ("Discard the changes to this
  list?", "Discard", "Keep editing"); Cancel asks the same. `InteractiveRebaseView.Rows` and
  `Snapshot()` (`InteractiveRebaseSnapshot`) are its test surface.
- **The conflict resolver** (`ViewModels/Conflicts/ConflictResolverViewModel`,
  `Views/Conflicts/ConflictResolverView`, design section 11, D140, D148). It opens on a click on a
  conflicted file in the stage panel, the file menu's "Resolve conflict" or `OpenDiff` of one, and
  spans the graph and right-panel columns (see "Repository tabs"); the banner stays above it. It
  writes nothing itself: its reads and every write go through `IConflictActions`, which
  `RepositoryOperations` implements with queued operations ("Resolve {file}", "Merge tool for
  {file}"), so that it is tested with a recording fake. It works on the working-tree file, which
  git wrote in the user's conflict style and which holds a rerere resolution or the user's edits
  (D148): read as bytes, decoded as the diff decodes (UTF-8, else
  Latin-1), parsed by `ConflictText` with the path's marker size. Header 36 (`ResolverFileStatus`
  "U", `ResolverFileName`, `ResolverFolder`, `ResolverKind` in git's words, `ResolverFilePosition`
  "File {i} of {n}" with previous and next in the stage list's order, `ResolverCloseButton`),
  toolbar 36 (`ResolverConflictPosition` "Conflict {k} of {m}" or "No conflicts left" with previous
  and next, `ResolverTakeOurs`, `ResolverTakeTheirs`, `ResolverMergeTool` "Merge tool…",
  `ResolverMarkResolved`), then two halves of equal height: `ConflictPanes` (the ours pane
  `ConflictOurs` and the theirs pane `ConflictTheirs` side by side, each titled with its side's
  name: "Ours: main", "Theirs: feature/x"; in a rebase "Ours: {onto} (the new base)" and "Theirs:
  {short} {subject} (your commit)") above `ConflictResultEditor` (`ConflictResult`, titled
  "Result" with "{m} conflicts left").
  - The panes show the file with every block's ours side and with every block's theirs side,
    their rows lined up with filler rows, a 26-high bar before each block (`ConflictUseOurs` and
    `ConflictUseBothOursFirst` in the ours pane, `ConflictUseTheirs` and
    `ConflictUseBothTheirsFirst` in the theirs pane, or `ConflictBarState` "Used in the result" /
    "Not used" once the block is decided), scrolling together, syntax-coloured as the diff (a
    synthetic one-hunk diff), tinted `VcConflictOursBrush`, `VcConflictTheirsBrush` and, for a side
    used in the result, `VcConflictUsedBrush`. `RowsOf(side)`, `BarButton(block, side, choice)` and
    `Bars` are their test surface.
  - The result is the file itself, editable, in the code font without syntax colours: an
    unresolved block's marker lines on `VcConflictMarkerBrush` in `VcWarningBrush`, its sections on
    the ours, theirs and `VcConflictBaseBrush` tints; a chosen or typed line has no tint. A choice
    replaces the block's text (from its `<` line to its `>` line) as one undo step, and so does
    "Take ours" or "Take theirs" (the side's working-tree form, `ReadConflictSideAsync`; disabled
    with "This side deleted the file" for a missing side). The document and its undo stack belong
    to the view model; undo and redo are the resolver's own (`HotkeyConfiguration.Undo` and
    `.Redo`, taken in the tunnel phase, applied through the document's `UndoStack`, never
    AvaloniaEdit's routed bindings); typed text is parsed again 300 ms after the last key
    (`IsParsePending`), and the bars, tints and counts follow (result blocks are matched to the
    panes' blocks by their sides and surroundings, in order). AvaloniaEdit's caret is hidden and the
    editor draws its own, blinking every 530 ms only while `ConflictResultEditor.CaretBlinks` is
    true (false when `VISUALCOMMIT_CARET_BLINK=0` at start-up, or set by a test, D151). Its
    automation peer is a Document whose Value is the text. `IsModified` compares the text with the
    document's original.
  - **Mark resolved** takes the result's text at the click; with blocks left (any marker line,
    `ConflictText.HasMarkerLines`) it asks first. It is one queued operation "Resolve {file}"
    (`IConflictActions.MarkResolvedAsync(entry, content, expected)`) that reads the file again just
    before it writes: when the file changed on disk since the resolver read it, nothing is written,
    the status says "{file} changed on disk: nothing was written" (warning) and the resolver asks
    "Overwrite it with the result?"; otherwise it writes the result (a side taken whole byte for
    byte, else in the encoding the file was read in, with the document's line ends) and stages it.
    While it runs the result is read-only and the buttons, Esc and the close button do nothing.
    Then the resolver moves to the next conflicted file, or closes when none is left, with the
    focus on the banner's Continue.
  - **Conflicts without text** (not both modified or both added, or binary, a symbolic link, a
    submodule, a file moved aside by a file/folder clash) show a card (`ConflictCard`,
    `ConflictCardText`) whose buttons (`ConflictCardButton`) resolve the file at once: "Keep the
    file" or "Keep it" (`TakeSideAsync`: the file git left is staged as it is), "Delete it"
    (`RemoveConflictedAsync`), "Use ours" or "Use theirs" for a binary file or a link, "Mark
    resolved" for a file moved aside; a submodule has none ("resolve it in a terminal").
  - **The merge tool** ("Merge tool…", for text and binary conflicts, disabled without
    `merge.tool`): asks first over a modified result, then runs `IConflictActions.RunMergeToolAsync`;
    while the tool runs (also one started from the file menu, `RunningMergeTool`,
    `MergeToolStarted`) the result is read-only and its title says "Waiting for {tool} to close…";
    afterwards it moves on when git staged the file, or reads the file again.
  - **Leaving and refreshes**: the close button or Esc closes it, asking "Discard your changes to
    {file}?" when the result is modified; Esc (`EscapeOrder.Resolver`, 350) does nothing while the
    focus is inside the result, so a slip never loses work. Alt+Up and Alt+Down go to the previous
    and next conflict, Alt+Left and Alt+Right to the previous and next file. For 500 ms after it
    opens a press with a click count of 2 or more is ignored, so a double-click's second press does
    not land on its buttons. A refresh that leaves the file conflicted reads an unmodified result
    again and keeps a modified one; a file resolved elsewhere moves on, or with a modified result
    stays with "Marked resolved elsewhere"; a refresh that ends the operation closes it. The
    header's right-click opens the conflicted file's menu, the result's and a pane's their own
    (`ConflictMenus`).

### Toolbar, status bar, conflict banner and auto-fetch

- **Toolbar (D90, D119).** `ViewModels/ToolbarViewModel`, one per repository (an empty tab's has
  every command disabled), binds Fetch, Pull, Push, Branch, Stash and Pop; each starts an
  operation through `IToolbarActions` (`RepositoryOperations`; tests give a fake through
  `Actions`): fetch all remotes, pull the current branch, push the current branch (to its
  upstream, or through the push dialog), create a branch at HEAD (checked out), the stash dialog,
  pop `stash@{0}`. No dropdowns. The states follow the refs and the status; before the refs are
  read every button is disabled without a reason. A disabled button's tooltip
  (`ToolTip.ShowOnDisabled`) is the first reason that holds: Fetch "This repository has no
  remotes"; Pull detached, no
  upstream, a gone upstream, an operation in progress ("Finish or abort the &lt;kind&gt; first"), no
  remotes unless the upstream is local; Push detached, unborn, "Add a remote first", "&lt;b&gt; tracks
  a local branch"; Branch "Make the first commit first"; Stash (`StashReason`, shared with every
  "Stash changes…" item and the stash operation, D119): unborn, an operation in progress, conflicts
  without one ("Resolve the conflicts first"), "There are no changes to stash"; Pop "There are no
  stashes". A button stays enabled while its operation runs: a second click queues another run, or
  none by the operation's dedupe key. Undo, Redo and Search stay disabled placeholders (phase 5).
- **Status bar (D95).** `ViewModels/Operations/OperationStatusViewModel`, one per repository,
  follows its queue's events on the UI thread: `Text` (`OperationStatus`): "Ready"; the running text,
  then the progress's stage and percentage ("Fetching from origin… Receiving objects 45%");
  "Waiting for auto-fetch…"; or the last result, which stays until the repository's next operation
  starts (no timer); while an operation of several steps runs, the step's text
  (`OperationContext.SetRunningText`). `Tone`: secondary, `VcWarningBrush` for a stop with
  conflicts and, since phase 4, for a success reported as `OperationReport.Warning`, `VcDangerBrush`
  for "&lt;Title&gt; failed"; "&lt;Title&gt; cancelled" is secondary. A stage-panel operation leaves
  "Ready"; a background operation shows nothing, except that auto-fetch's own says "Auto-fetch
  failed" when it fails (git's first error line as its tooltip) and "Waiting for auto-fetch…"
  while an operation of the user waits for it; the backups' clean-up never changes the text.
  `OperationProgress` (80 by 4, visible while a user operation runs,
  indeterminate without a percentage), `OperationCancelButton` ("Cancel", tooltip "Stop git",
  while `CanCancel`), `OperationWaiting` ("1 waiting"). While ssh shows a notice its text replaces
  the operation text.
- **Conflict banner (D89, D110, D122, D144).** `ViewModels/Operations/ConflictBannerViewModel`
  (`RepositoryViewModel.Banner`, constructed with the repository's Abort, Continue and Skip) shows
  each status read (`OnStatusApplied` → `Show(state, conflicts, ontoName, currentName,
  otherUnstaged)`: `ontoName` is a local branch at a rebase's new base, else a remote one, else
  null for its short SHA), and `Views/Shell/ConflictBanner` draws it above the graph, the list, the
  diff or the resolver while an operation is in progress or files conflict: `IconAlert`,
  `BannerText`, and since phase 4 `BannerContinueButton` ("Continue") and `BannerSkipButton`
  ("Skip") left of `BannerAbortButton` ("Abort merge", "Abort cherry-pick", "Abort revert", "Abort
  rebase", "Abort" for `git am`; none for conflicts without an operation), each shown, enabled or
  disabled with its reason as tooltip as design 5.1 says per state (`ContinueFor`, `SkipFor`:
  merge, single pick or revert, a pick or revert in a list, an empty one, a paused list, a list
  stopped on a refused commit, and a rebase stopped on a pick, a group's target, an empty commit, a
  rescheduled pick, the app's failed amend, another failed exec, an `edit`). Continue is disabled
  while files conflict ("Resolve the conflicts first ({N} files)") and, at a rebase stop, while
  other tracked files are unstaged; it is never offered where git would drop a commit without a
  word, nor Skip where it would join commits to the wrong one. `git am` keeps phase 3's "continue in
  a terminal"; a rebase or a list started in a terminal is continued from the app.
  `VcBannerBackgroundBrush` with a 1 px `VcWarningBrush` line along its bottom. The layout is
  `BannerLayoutPanel` (amends D122): the buttons (`VcActionButton`, 26 high, 6 apart, 8 from the
  right edge) stand right of the text while it keeps at least 240 px, otherwise on a row of their
  own under it, right-aligned, with 8 above and below; the text wraps and the banner grows: at least
  36 high, exactly 36 when text and buttons fit on one line; the graph's rows move down by its
  height. The texts, the icon's tooltip (design 10.3) and the Abort question are its static methods
  (`TextFor`, `ToolTipFor`, `AbortConfirmation` with D110's `AbortKeepsHead`; the rebase texts say
  "Rebasing {b} onto {onto}: stopped at {short} "{subject}" ({k} of {n})."), which
  `RepositoryOperations` and the stage panel share. Abort asks the question (for a rebase begun
  with "Check out and rebase": "…, and {branch} is checked out again") and hands the abort to
  `IInProgressActions.AbortAsync` (backups `abort-<time>` and `-changes`); Skip asks "Skip
  {short}?" and hands it to `SkipAsync`; Continue hands the stage panel's `ContinueMessage` to
  `ContinueAsync`. The root is a UI Automation control element (`ConflictBanner`).
- **What is in progress** is read by `GitRepository.ReadStatusAsync` (`ReadInProgressAsync`) from
  the repository's own git folder, with no git call unless something is there, in this order:
  `rebase-merge/` (Rebase: `head-name`, `onto`, `stopped-sha` and its subject, and since phase 4
  the stop's kind, read in this order: `Rescheduled` when the todo's first line is pick-like and
  names `stopped-sha` (on newer git, which writes no `stopped-sha` then, when `done` ends with the
  same command); `ExecFailed` when `done` ends with an `exec` and there is no `stopped-sha`
  (`OwnAmendFailed` when that exec names `vc-message-`, also when `rebase.rescheduleFailedExec`
  put it back first in the todo); `Edit` when `amend` exists and `done` ends with `edit`; `Empty`
  when `CHERRY_PICK_HEAD` sits beside the folder, or `done` ends with a pick-like command for
  `stopped-sha` with nothing staged and nothing conflicted; `Pick` otherwise; `NextIsSquash`,
  `Group`, and `Done` and `Total` counting the commit commands, not `label` and `exec`),
  `rebase-apply/` (with `applying`: `git am`; otherwise Rebase under the apply backend),
  `MERGE_HEAD` (Merge, `MERGE_MSG`'s first line), the pick heads `CHERRY_PICK_HEAD` and
  `REVERT_HEAD` (through `ReadPseudoRefAsync`, so a reftable repository is read too, D146),
  `sequencer/todo` alone (a list paused between commits, an empty revert of a list with
  `MERGE_MSG`, or a list stopped on a commit git could not apply; its `Commit` is the todo's first
  line, resolved). `Sequenced` is set while `sequencer/` exists; `Remaining` for a pick or revert is
  `sequencer/todo`'s lines less one; a list's `Done` is `rev-list --count <sequencer/head>..HEAD`
  and `Total` adds the commands left (git keeps no record of a skipped commit, so a Skip makes `N`
  smaller); `AbortKeepsHead` is set when HEAD differs from `sequencer/abort-safety` (D110); `Empty`
  when the index equals HEAD's tree without a conflict.
- **Auto-fetch (D79, D85, D109, D125).** `ViewModels/Operations/AutoFetchScheduler`, one per
  window, made and started by `MainWindowViewModel`: every 30 seconds of its `TimeProvider` it
  calls `Tick(now)`, which, for the active tab's repository only, starts the auto-fetch with
  `TryStartInBackground` when the interval is above 0, the repository has a remote, its queue is
  idle, no dialog is open and the last fetch (a fetch or pull that succeeded, an auto-fetch
  started, or the moment opening the repository began) is at least one interval ago. The interval is
  `AppSettings.AutoFetchMinutes` (default 5, 0 off), overridden by the environment variable
  `VISUALCOMMIT_AUTO_FETCH_MINUTES` (both harnesses set 0). The operation, titled "Auto-fetch", is
  the fetch of every remote, every call with `--no-auto-maintenance` and the network timeout
  `-c http.lowSpeedLimit=1 -c http.lowSpeedTime=60`, `PromptMode.Deny`, a refresh of the refs
  afterwards. It learns of fetches from the queues' `Finished` events (a Fetch, a Pull, and since
  phase 4 a "Pull (rebase)", by title or dedupe key); after a user's cancel that left lock files it
  opens "Auto-fetch cancelled". Tests call `Tick` with chosen times.

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
- `AppSettings` (Core) is an immutable record of preferences: `Theme`, `FileList` (D44),
  `DiffMode` (D61), `AutoFetchMinutes` (default 5, 0 turns auto-fetch off; no screen sets it
  before phase 6, D85) and `StashIncludeUntracked` (default true: the stash dialog's check box
  starts with the last choice, saved when the dialog is confirmed). Every property needs a
  default, so that older files still load; a setting missing from `settings.json` gets its
  default (`JsonSettingsStore` lays the file's values over the defaults' JSON before reading it,
  D107), not 0 or false. `ISettingsStore.Update(settings => settings with { ... })` changes and
  saves it.
- `SessionState` (Core) is what changes as the app is used (D46): `Tabs` and `ActiveTab`,
  `Recent`, `Window` (`WindowPlacement`: position, client size, maximised) and the panels'
  preferred widths. `JsonSessionStore` (App) keeps it like the settings: written through a
  temporary file, an unreadable file set aside as `session.unreadable.json`, a tab with a blank
  folder read as an empty tab. `JsonSessionStore.Write` lets tests start the app with tabs open.
  Phase 3 added nothing to the session, and phase 4 nothing to the session or the settings (the
  merge tool is git's own `merge.tool`, D149).
- `JsonSettingsStore` (App) writes camel-case JSON with enums as text, through a temporary file,
  so a crash cannot leave half a file. A file it cannot read is moved to
  `settings.unreadable.json` and the defaults apply. A failed save is logged and does not throw.
- `DateDisplay` (Core) shows dates as `yyyy-MM-dd HH:mm` in the local zone, or in the zone named
  by `VISUALCOMMIT_TIME_ZONE` (D43); both harnesses set `UTC`.
- `IAppLog` (Core) is the log interface, with `Debug`, `Info`, `Warning` and `Error` helpers.
  `FileAppLog` (App) appends to `logs/visualcommit-yyyyMMdd.log`, one line per entry
  (`2026-10-06 01:34:44.722 -07:00 [INF] message`), opening the file for each entry, so several
  instances can share it. Files older than 14 days are deleted at start-up. A log that cannot
  write never throws. `Close()` (at the session's end) makes it write nothing more.

### Shell and views

`Views/MainWindow.axaml` lays the window out as "Main window layout" in `requirements.md`. Each
region is a `UserControl` in `Views/Shell`.

| Region | View | Size | Automation id | State |
|---|---|---|---|---|
| Repo tabs | `RepoTabsView` | 36 high | `RepoTabs` | Real: one tab per repository (`RepoTab` buttons named by title, `CloseTabButton`), and "+" (`AddRepoButton`) for a new tab |
| Toolbar | `ToolbarView` | 52 high | `Toolbar` | `FetchButton`, `PullButton`, `PushButton`, `BranchButton`, `StashButton`, `PopButton` are real (see "Toolbar, status bar, conflict banner and auto-fetch"); `UndoButton`, `RedoButton` and `SearchButton` are disabled placeholders (phase 5). `ThemeSwitch` works |
| Left panel | `LeftPanelView` | 260 wide by default; 180 to 520 | `LeftPanel` | Real: `FilterBox` and the list `RefList` (`Section`, `RefItem`), with menus, double-clicks and branch drags; "Pull requests" stays empty until phase 7 |
| Commit graph | `CommitGraphView` | The rest; at least 320; while the resolver is open, the right panel's column too | `CommitGraph` | Real: `WelcomeView` (`OpenRepoButton`, `CloneRepoButton`, `InitRepoButton`, the clone form), `RepositoryGraphView` (`GraphRows`, `GraphScrollBar`), or in its place `InteractiveRebaseView` (`InteractiveRebaseView`, `RebaseRow`, …), `DiffView` (`DiffView`, `DiffText`, see "Diff view") or `ConflictResolverView` (`ConflictResolver`, `ConflictOurs`, `ConflictTheirs`, `ConflictResult`, …, see "The graph area"); above any of them, while one shows, `ConflictBanner` (`BannerText`, `BannerContinueButton`, `BannerSkipButton`, `BannerAbortButton`) |
| Right panel | `RightPanelView` or `WorkingChangesView` | 400 wide by default; 280 to 720; hidden while the resolver is open | `RightPanel` or `StagePanel` | Real: commit details (`DetailsSubject`, `ParentLink`, `FlatFilesButton`, `TreeFilesButton`, `ChangedFilesTitle`, `FileList` with `ChangedFile` rows), `MultiSelectionView` while several commits are selected, or the placeholder; while the working-changes row is selected, the stage panel (`UnstagedFileList` and `StagedFileList` with `StageFileRow` rows and their `RowStageButton`, `RowDiscardButton`, `RowUnstageButton`; `CommitSummary`, `CommitDescription`, `AmendCheckBox`, `CommitButton`, which reads "Continue …" during a list or a rebase, `RestoreBar`) |
| Status bar | `StatusBarView` | 26 high | `StatusBar` | `CurrentBranch`, `GitStatus`, `OperationStatus`, `OperationProgress`, `OperationCancelButton` and `OperationWaiting` are real. `ActivityLogToggle` is a disabled placeholder (phase 5) |

- The window opens at 1280×800, or at the size and place the session saved, and cannot be made
  smaller than 1000×560. `MainWindow.UseSession` restores and saves the placement (a window
  restored maximised keeps its normal size and position).
- The panels are resized with two `GridSplitter`s (`LeftSplitter`, `RightSplitter`) that lie over
  the panel edges. Letting go of an edge makes that panel's width its preferred width, saved in
  the session. `PanelLayout.Fit` gives the drawn widths: when the window is too narrow for both
  preferred widths and the graph's 320, both panels give way in proportion to their room above
  their minimums, and take their preferred widths back when it grows.
- The diff view replaces the graph while a file's diff is open (D63), and so do the interactive
  rebase's list and the conflict resolver (D140, D141); the menu layer, the drag layer and the
  dialog layer lie over the whole window, in that order (D69, D104, D105, D87).
- `MainWindow`'s Esc chain (`AddEscapeHandler(IEscapeHandler, order)`, `Views/IEscapeHandler`):
  on Esc the handlers are asked in their `EscapeOrder` (Menu 100, Drag 200, Dialog 300, Resolver
  350, InteractiveRebase 360, Diff 400) until one takes it. The menu layer, the drag layer, the
  resolver and the list register their own; the list's declines while a diff is open over it.

Custom controls, in `Controls/`:

- `Icon` draws one of the outlines in `Theme/Icons.axaml` as a stroke in the inherited text
  colour. To add an icon, add a `StreamGeometry` on a 24×24 grid to `Icons.axaml` (and to
  `THIRD-PARTY-NOTICES.md` if it is not our own drawing).
- `ToolbarButton` is a button with an icon above a label. Its control theme is in
  `Theme/Controls.axaml`; its label is also its name for screen readers and UI Automation.
- `CommitGraphControl`: see "Commit graph".
- `ContextMenuItem`: see "Context menus". `SecretTextBox`: see "Prompts and credentials".
- `Diff/CodeTextEditor`, `Diff/DiffPane` and `Diff/DiffTextView`: see "Diff view";
  `Conflicts/ConflictPanes` and `Conflicts/ConflictResultEditor` (with `ConflictTint`, the
  background a row or line is drawn on, which tests read instead of pixels): see "The graph area".
- `TrimmedTextBlock` shows its whole text as its tooltip while it is cut with "…" (a menu
  item's label); `UrlTextBlock` wraps without breaking a line inside a URL or an ssh address
  (the confirmation's question, the error dialog's summary, the prompt dialogs' texts). Both are
  styled as plain text blocks.

Control themes in `Theme/Controls.axaml`: `VcFlatButton`, `VcTabButton`, `VcActionButton`,
`FileListToggle` (small text buttons: Flat and Tree, the stage panel's and diff view's actions;
class `selected`), `FileFolderButton` (a file or folder row), `VcIconButton` (22 by 22),
`VcPrimaryButton` (the commit button), `VcDangerButton`, `VcCheckBox` (20 high), and
the text classes `heading`, `secondary` and `error`. Phase 3's menus and dialogs have files of
their own, `Theme/Menus.axaml` and `Theme/Dialogs.axaml`, included in `App.axaml`.

### Theme

- `IThemeService` / `ThemeService` (App, `Services/`) switches the theme by setting the
  application's theme variant. The view model's `ToggleThemeCommand` applies the theme and saves
  it; `AppSession.Start` applies the saved theme before the window is created.
- All colours are tokens in `Theme/Tokens.axaml`, one set per theme. Views use them only as
  `{DynamicResource ...}`, never as fixed colours, so switching recolours everything at once. The
  graph control looks them up for its theme variant and redraws when the variant changes.
- The Fluent theme is the base for standard controls, with its accent set to ours. Its text-box
  colours are replaced by `TextControl...` keys in `Tokens.axaml`.
- The typeface is Inter, shipped with Avalonia, on every platform (D28). Code in the diff view
  is JetBrains Mono NL (OFL), shipped with the app in `Assets/Fonts` and named by the resource
  `VcCodeFontFamily` (D64).
- Phase 3 added the icon `IconAlert` (Lucide's triangle-alert, in `THIRD-PARTY-NOTICES.md`), the
  conflict banner's. Menus have no icons. Phase 4 added `IconGrip` (grip-vertical, the list's drag
  handle), `IconChevronLeft` and `IconChevronUp` (the resolver's previous file and previous
  conflict), all Lucide's, and the five conflict tokens below.

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
| `VcAccentTextBrush` | `#10121A` | `#FFFFFF` | Text on an accent background: the primary button's label, a check box's tick, the dot of a dialog's radio button |
| `VcSuccessBrush` | `#3FB97F` | `#1E8E5A` | Added files |
| `VcWarningBrush` | `#E0A23B` | `#B7791F` | Modified files |
| `VcDangerBrush` | `#E5606B` | `#C93A46` | Errors, deleted files |
| `VcSelectionBrush` | `#2A3150` | `#DDE1FA` | The selected graph row and left-panel item |
| `VcRowHoverBrush` | `#1C1F27` | `#F3F4F7` | The graph row under the pointer |
| `VcLabelTextBrush` | `#10121A` | `#FFFFFF` | Text on a filled branch label |
| `VcDiffAddedBrush` | `#1A2E24` | `#E6F6EC` | An added line of a diff, gutter included |
| `VcDiffAddedWordBrush` | `#2B5A3F` | `#B4E5C6` | The changed words of an added line |
| `VcDiffRemovedBrush` | `#331D23` | `#FBE9EB` | A removed line of a diff, gutter included |
| `VcDiffRemovedWordBrush` | `#6A2D37` | `#F3BAC1` | The changed words of a removed line |
| `VcDiffHunkBrush` | `#1C2130` | `#EEF0FB` | A hunk's header row |
| `VcDiffFillerBrush` | `#181A20` | `#F3F4F6` | Side by side: the empty side of a row |
| `VcBackdropBrush` | `#99000000` | `#66000000` | The dimmed window behind a dialog |
| `VcMenuShadow` (`BoxShadows`) | `0 6 16 0 #80000000` | `0 6 16 0 #33000000` | The context menu's shadow |
| `VcBannerBackgroundBrush` | `#33291A` | `#FFF4DE` | The conflict banner |
| `VcDragTargetBrush` | `#7C8CFF` | `#4353D8` | The outline of a drop target while a branch is dragged (the accent's colour, a token of its own so that it can change apart from it); the gap line while a row of the interactive list is dragged |
| `VcConflictOursBrush` | `#15323A` | `#DCF1F5` | The ours side of a conflict block: its rows in the ours pane and its section of the result |
| `VcConflictTheirsBrush` | `#2C2140` | `#F0E6FA` | The theirs side, likewise |
| `VcConflictBaseBrush` | `#24272F` | `#EEF0F3` | A `diff3` base section in the result |
| `VcConflictMarkerBrush` | `#3A2E16` | `#FCEFD2` | The marker lines of an unresolved block in the result (their text in `VcWarningBrush`) |
| `VcConflictUsedBrush` | `#1A2E24` | `#E6F6EC` | In the ours and theirs panes, a block's side once it is used in the result (`VcDiffAddedBrush`'s colours) |
| `VcLane0Brush` ... `VcLane7Brush` | `#7C8CFF` `#3FB97F` `#E0A23B` `#E5606B` `#4FB3D9` `#B07CE8` `#D97EB6` `#8FB84A` | `#4353D8` `#1E8E5A` `#B7791F` `#C93A46` `#1F86B0` `#8048C7` `#B54A8C` `#5F8A1E` | Lane colours 0 to 7 (D48) |

### Conventions

- View models derive from `ObservableObject` and use the toolkit's `[ObservableProperty]` on
  partial properties and `[RelayCommand]`. They take their services through the constructor and do
  not touch Avalonia types, so they are tested without a UI; they reach the UI thread through the
  `SynchronizationContext` they were created on.
- XAML uses compiled bindings: a view that binds declares its `x:DataType`.
- Anything the real-window pass needs to find has an `AutomationProperties.AutomationId`, because
  UI Automation can find nothing else; anything a user can act on has an accessible name. UI
  Automation does not see a text inside a button, so buttons carry their text as their name. A
  `Panel` or templated control is no UI Automation control element by default, so a root the pass
  waits for has an automation peer that makes it one (every dialog's `DialogView`, the
  `ConflictBanner`, `MultiSelectionView`, `DialogRow`, and since phase 4 `InteractiveRebaseView`,
  its rows `RebaseListRow`, the `ConflictResolver` and its card, `ConflictPanes`), as
  `CommitGraphControl` has; the resolver's result is a Document with a read-only Value pattern.
- Every write to a repository is an operation of its queue. A new operation is a method of
  `RepositoryOperations` built on `RunAsync`, `ConfirmAndRunAsync` and `GuardAsync`, with its texts
  in `OperationTexts` (an `OperationText` per operation) and its git calls in `GitRepository`; it
  never throws, and it checks what it acts on when it runs, not at the click (D124). A menu item
  calls it through `IRepositoryActions`. One that destroys something saves its backup with
  `SaveBackupAsync` inside the operation, which also schedules the backups' clean-up (D139).
- A view model that can be replaced in the graph area while it holds work (the list, the
  resolver) is left through `RepositoryViewModel.TryLeaveGraphAreaAsync`, never closed directly.
- Async code in the Core and Git projects uses `ConfigureAwait(false)`, and so do App services
  whose tasks the app may wait for while it closes (`GitRepositoryProvider`); view models do not,
  so they continue on the UI thread.
- Services that can fail on the user's machine (settings, session, log) log the failure and carry
  on; they do not throw into the UI. A failure the user caused or can fix (a folder that is not a
  repository, git's own error) is shown where they acted: on the welcome page, in the clone form,
  in the details panel, in the stage panel for its own writes, and in the error dialog for any
  other operation (C11: git's own output, the command, "Copy output").
- Text that may be too long for its place is cut with "…" and shows whole in a tooltip.

### Tests

Tests run on xunit.v3 with Microsoft Testing Platform (D24). The commands are in `CLAUDE.md`.

| Helper | Where | What it is for |
|---|---|---|
| `TempDirectory` | `tests/VisualCommit.Testing` | A folder under the system temp folder, deleted on dispose |
| `TempRepo` | same | Builds a real git repo for a test, cut off from the machine's git configuration, with a fixed author and a clock that advances one minute per commit, so the same steps give the same SHAs everywhere; `user.name` and `user.email` in the repo's own configuration give the app's commits an identity (D72). Helpers for text and byte files, commits, branches, tags (lightweight and annotated), merges, stashes, a bare remote next to the repo and pushes; `CopyAsync` makes an independent copy with its remotes pointed at the copy (http URLs left alone, linked worktrees joined to the copy) and a clock that carries on from the template's; anything else goes through `GitAsync`, `GitWithInputAsync` or `GitAllowingFailureAsync` (a step meant to stop). Phase 3 added `ConfigAsync`, `RefTargetAsync`, `BackupRefsAsync`, `StashListAsync`, `CloneSiblingAsync` and `OpenSibling` (siblings share the clock), `IsolatedEnvironment(home)`, `ClockTime` and `MoveClockTo` (forward only, for a step that must look newer in the reflog than an app operation, D111). Its git calls drop the machine's askpass, ssh and proxy variables too (D116), and since phase 4 run with `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:`, so that a step such as `git rebase --continue` never starts the machine's editor (D151); `GitWithEnvironmentAsync(extra, args)` and its "allowing failure" twin add variables for one call (a sequence editor that writes a todo) and keep the clock's dates |
| `Scenarios` | same | The scenario repos: `LinearAsync` (phase 0), `GraphAsync` (phase 1: branches in folders, a merge, tags, a remote with ahead and behind, a stash), `ChangesAsync` (phase 2: staged, unstaged and untracked changes of every kind the diff view shows; its contents in `Scenarios.ChangesFiles`) `CrlfAsync` (phase 2: `core.autocrlf` in the repo's own configuration) and `ConflictAsync` (phase 2: a merge stopped with a conflict). Phase 3's, in `Scenarios.Phase3.cs`: `OperationsAsync` ("ops": branches, tags, a stash, a bare `origin`), `StoppedAsync(kind)` (an ops copy stopped in a merge, cherry-pick sequence, revert, rebase, or with stash conflicts, as a terminal would leave it; built each time), `RemoteSyncAsync` ("sync": published, unpushed and local branches) with `PushFromElsewhereAsync`, `CheckoutBlockedAsync` ("blocked"), `EdgeCasesAsync` ("edges": a detached HEAD, a linked worktree, a local upstream, a branch named like a remote branch, a tag named like a branch), `AuthRemoteAsync(server)` ("auth": a clone of what `AuthRemoteServer` serves). Phase 4's, in `Scenarios.Phase4.cs`: `RebaseAsync` ("rebase": `feature/topic` and `feature/other` to rebase onto `main` without a conflict, a bare `origin`), `InteractiveAsync` ("interactive": one line of eight commits on `main`, one a fixup candidate, one with a `#42 is the issue` body line), `ConflictsAsync` ("conflicts": `main` and `feature/theirs`, `feature/rebase`, `feature/picks`, `feature/kinds`, with notes.txt, other.txt, image.png and new.txt) and `ConflictsStoppedAsync(ConflictsStop.Merge | Rebase | CherryPicks | MergeKinds, mergeTool)` (stopped as a terminal would leave it, built each time, optionally with the fake merge tool of `ConfigureFakeMergeToolAsync`); their files in `RebaseFiles`, `InteractiveFiles` and `ConflictsFiles`, whose lines are short enough for a resolver pane at 1100×700. `GraphAsync`, `ChangesAsync`, phase 3's and phase 4's (except the stopped ones) are built once per test process as templates and copied for each test; `Phase3ScenarioTests` and `Phase4ScenarioTests` pin their refs, SHAs, graphs and stopped states |
| `TestImages` | same | Small PNGs whose bytes depend only on their pixels (stored deflate blocks), for scenario repos |
| `FakeRepositoryBase` | same | The start of a fake `IGitRepository` for view-model tests: a clean working tree, every write refused; phase 3's and phase 4's reads give empty results (no remotes, no worktrees, nothing detached, no merge tool, no operation message, a clean-up that removes nothing) and their writes throw `NotSupportedException` |
| `LargeHistory` | same | The 100k-commit repo (D51), built once per machine with `git fast-import` under `%TEMP%/VisualCommit.Tests/shared/` and shared read-only; what each row shows follows from its number. Since phase 4 `GetBareAsync()` gives a bare copy made once beside it (`large-history-v1.git`), which `AuthRemoteServer` serves in place for phase 1's clone checks (D150) |
| `GitIsolation` | same | Cuts the test process off from the machine: no system or user git configuration, no search for a repository above the tests' own temp folder (`GIT_CEILING_DIRECTORIES`), none of the variables a surrounding process can hand git, and dates in UTC; since phase 3 also no askpass program or ssh command of the machine (`GIT_ASKPASS`, `SSH_ASKPASS`, `SSH_ASKPASS_REQUIRE`, `GCM_INTERACTIVE`, `GIT_SSH`, `GIT_SSH_COMMAND`, `GIT_SSH_VARIANT`), no proxy (`no_proxy` set to `127.0.0.1,localhost`) and auto-fetch off (`VISUALCOMMIT_AUTO_FETCH_MINUTES=0`) (D112, D116); since phase 4 no editor of the machine (`EDITOR`, `VISUAL`, `GIT_EDITOR`, `GIT_SEQUENCE_EDITOR`, D151). `ApplyMachineIndependence` does the same for a process about to start (the real-window pass's app). The UI test assemblies call it through `HeadlessTestApp.IsolateFromTheMachine` in a module initializer, `VisualCommit.Git.Tests` directly |
| `AuthRemoteServer` | same | The credential test server (D97): a `TcpListener` on 127.0.0.1 serving `git http-backend` through `GitRunner` as CGI for one bare repository (its own `remote.git`, or an existing one in place), asking for HTTP Basic auth (alice/s3cret) unless `RequireAuth` is false. Port 0 (the system picks) by default; `StartForVisualChecksAsync` uses 47391 (`VisualChecksPort`) so the pictures do not change, or since phase 4 47392 (`Phase1VisualChecksPort`) for phase 1's clone checks, which run in another process at the same time as phase 3's (D150), falling back to a free port (`PortWasTaken`). `Requests`, `Errors`; `HoldNextFetchPack(atPercent)` holds the next fetch's pack until `Release()`, so a check can show and cancel a running fetch: with 0 after its first progress line with a percentage, with a percentage right after the first whole progress line at or above it, the packet re-framed so that git's last line shows exactly that percentage (phase 1's clone is held at 45 %, so it cannot race) |
| `FakeFileLauncher`, `FakeClipboard` | `tests/VisualCommit.Testing/Headless` | Record what the app asked to open, show or copy, and touch nothing on the machine; `ShellDriver.Start` injects them (`Launcher`, `Clipboard`) |
| `ScriptedPromptHandler` | `tests/VisualCommit.Testing` | Answers prompts from a queue of answers and records every prompt; a prompt without an answer is refused. Injected through `AppSession.Start` or `ShellDriver.Start` (`promptHandler`), or given to a broker |
| `AskPassRunner` | same | Runs the real helper as OpenSSH runs `SSH_ASKPASS` (the prompt as its argument, `SSH_ASKPASS_PROMPT` when ssh sets it, an open scope's environment) for the prompt dialogs no ssh server can reach |
| `FakeSsh` | same | A shell script set as a remote's `core.sshCommand` that runs `$SSH_ASKPASS` with recorded OpenSSH texts (passphrase, unknown host, PIN), writes the answers to a file and exits 1 (D115) |
| `AppLogFile` | same | The app's log as checks read it, and the patterns of the lines they wait for or count, in both harnesses |
| `WindowLayout` | same | The window's fixed layout in logical pixels, copied from the reports (tab strip, toolbar, panels, column headers, rows 26 high, the banner's minimum, the menu's heights and gaps) and the neutral point (760, 687) where both harnesses rest the pointer before a capture that is not about a hover. Both harnesses compute positions from it |
| `HeadlessTestApp` | `tests/VisualCommit.Testing/Headless` | Builds the real `App` for headless mode with Skia rendering. Each UI test assembly names it in `AssemblyInfo.cs` |
| `ShellDriver` | same | Starts an `AppSession` on a data folder (`Start`, or `StartWithTabs` with repositories open through the session file), shows its window at a size, and drives it with simulated input: click, right-click, double-click, drag, wheel, hover, typing and keys. `Folders` is the fake folder dialog; `WaitForAsync` waits for a condition while the UI thread keeps running; `Capture()` returns a `Screenshot`. Partial since phase 3: `ShellDriver.Phase3.cs` (clicks with modifiers, a drag in steps, `Menu()` as a `MenuSnapshot`, `ClickMenuItem`, `MenuItemBounds`, the open dialog, `TypeInto`, `WaitForIdleAsync` for the queue and its refresh), `.Pointer.cs` (the neutral point, resting the pointer until a tooltip opens), `.Log.cs` (the app's log lines, git's call records, step times), `.Checks.cs` (opening a repository, rows below the banner, left-panel and file rows, the error dialog as an `ErrorDialogSnapshot`, progress, closing the window); phase 4's `.Phase4.cs` (`Undo()` and `Redo()` by the first gesture of `HotkeyConfiguration.Undo` and `.Redo`, the banner's Continue and Skip, the result's caret held still: `ConflictResultEditor.CaretBlinks = false` in `Start`), `.Phase4.Rebase.cs` (the list as a snapshot, a row's point, a drag of a row in steps, a row's action through its menu) and `.Phase4.Conflicts.cs` (the resolver as a snapshot, a bar's button, the result's text, caret and modified state, typing into it, a click at a line and column). `WaitForIdleAsync` also waits for the stage panel's read of the stopped commit's message (`StagePanelSettled`: `IsFillingMessage` false), which fills its boxes after the status is shown. `FindByAutomationId` looks in the open menu first, then in the top dialog, then in the whole window |
| `FakeFolderPicker` | same | Answers the app's folder dialogs with folders the test queued (D42) |
| `Screenshot` | same | A captured frame: save as PNG, read pixel colours |
| `LayoutAudit` | same | `FindClippedText` lists every visible text that is not shown in full; with `allowShortenedWithToolTip` it lets through text cut with "…" on purpose whose whole text is in a tooltip, and with `allowScrolledOutOfView` text cut only by a scrolling list's edge. A wrapped line's trailing space is not counted |
| `FreshApplication` | same | Runs part of a test with a new Avalonia application object: how a scripted check crosses a restart (D32). Its wait for its turn on the headless UI thread does not count against the test (D132) |
| `VisualRun`, `VisibleTexts` | same | Where the walk-throughs save their pictures (`artifacts/visual/phase-N/scripted/`); the texts a region shows, as a person reads them. Shared by the walk-through projects |
| `HangWatchdogAttribute`, `HangClock` | `tests/VisualCommit.Testing` | `[assembly: HangWatchdog]` in each default test assembly. If a test runs for more than 3 minutes, or nothing starts or finishes for that long, it prints which tests were running and stops the test process. `VISUALCOMMIT_TEST_HANG_SECONDS` changes the limit. A test's wait for a shared preparation (`SharedPreparationAsync`: a scenario template, the 100k-commit repo, the pull-parity templates) and for its turn on the headless UI thread (`WaitingForTurn`) is left out of its time; a preparation is held to the limit on its own (D132). `HangClock` holds these rules apart from the watchdog, so they are tested with a clock moved by hand |
| `GitIsoDates` | same | Reads git's strict ISO dates as instants: Git 2.36 writes `+00:00` for UTC, CI's Git 2.55 `Z` |

- `VisualCommit.VisualTests` has a folder per phase for phases 0 to 2. `PhaseNChecks` holds a test for every
  numbered check of the phase's test report (phase 1's are split over `Phase1Checks.Shell.cs` and
  `Phase1Checks.Graph.cs`, phase 2's over `Phase2Checks.*.cs` by subject); each saves its screenshots as
  `artifacts/visual/phase-N/scripted/<check><step>-<what>.png` (`VisualRun`, in
  `tests/VisualCommit.Testing/Headless`) and asserts the expected result.
  The first screenshot of a run empties the phase's folder. Expected values are copied from the
  report into the tests (`Phase0/ShellExpectations.cs`, `Phase1/Phase1Expectations.cs`,
  `Phase2/Phase2Expectations.cs`), not read from the app. Phase 0's checks expect the empty shell
  as phase 1 changed it (the welcome page, "New tab").
- Phase 3's walk-through is the project `VisualCommit.VisualTests.Phase3` (`Phase3ChecksA` to
  `Phase3ChecksD`, `Phase3Expectations*.cs`, which the real-window pass compiles too, and their
  support), with the same pictures folder rule. Every headless test of an assembly runs on that
  assembly's one UI thread, one at a time, whatever its class; a second project is a second test
  process with its own UI thread, which `dotnet test` runs beside the first. What the two share
  (`VisualRun`, `VisibleTexts`) is in `VisualCommit.Testing`.
- Inside it (D133): `Phase3Checks.Support.cs` holds the abstract base class `Phase3Checks`, with
  what every check uses (the default start on a scenario at 1100×700, the server scenarios
  `ServerScenario`, `Shot(app, "<check><step>-<what>")`, which moves the pointer to the neutral point
  first unless a menu is open, a drag runs or the capture is about a hover, and the assertions the
  checks repeat: `AssertMenu`, `AssertNoMenuAsync`, `AssertErrorDialogAsync`, `AssertStatus`, …).
  Each part of the report is one class, `Phase3ChecksA` to `Phase3ChecksD`, partial over files
  `Phase3Checks.<part>.<topic>.cs`, so the four parts run side by side; only part D starts servers
  (port 47391), so its checks never meet. Its expected texts, graphs and positions are in
  `Phase3Expectations.Common.cs` and `Phase3Expectations.A.cs` to `.D.cs` (nested `PartA` to
  `PartD`). A test is named `Check_<NN>[<suffix>]_<what>`, one per app start; every wait is on a
  condition, never a fixed time. `scripts/time-scripted-check.sh` times chosen checks alone and
  prints each step's time (its second argument picks the phase and the project: 4 when left out).
- Phase 4's walk-through is the project `VisualCommit.VisualTests.Phase4` (D151), built the same
  way: the abstract base `Phase4Checks` (`Phase4Checks.Support.cs`, a copy of what phase 3's
  support offers, plus what phase 3 repeated in each part: a graph assertion, the backups,
  `AssertCalled`, `WaitForOperationAsync`, and `AssertBanner`, which knows Continue, Skip and the
  banner's layout), one class per part (`Phase4ChecksA` to `Phase4ChecksD`, partial over files
  `Phase4Checks.<part>.<topic>.cs`), 26 test methods for its 18 checks, one per app start
  (`Check_A2_rebase_onto_a_branch`, …), and its expectations in `Phase4Expectations.Common.cs` and
  `.A.cs`, `.B.cs`, `.C.cs`, `.C2.cs`, `.D.cs`. The tests of the expectations and the support are
  named so that the real-window project's link pattern (`Phase4Expectations*.cs`) does not take
  them (`Phase4ExpectationTests.<part>.cs`, `Phase4SupportTests.cs`). No part starts a server.
- **The two gits** (phase 3's rule, carried into phase 4's checks): the report quotes git's own
  texts and files as Git 2.36 writes them, and the checks assert them word for word only on that
  git (`Phase4Expectations.IsReportGitAsync()`); on any other git (CI's 2.55) they assert the
  classifying parts, and the `rebase-merge/` files are compared after 2.55's additions are taken
  out (the subject written as a comment in `done` and `git-rebase-todo`, the `# Conflicts:` block
  at the end of `message`). Nothing is skipped: the version decides what is asserted. The
  Git-layer tests follow the same rule (`PseudoRefTests` asserts on 2.36 that git refuses
  `--ref-format`).
- xunit runs the tests of one class (or of one `[Collection]`) one after another and different
  ones side by side, so big test classes are split by subject: `ScenarioTests` and
  `Phase3ScenarioTests`, the pull parity classes by group of settings, and
  `RepositoryOperations*Tests` (helpers in `RepositoryOperationsTestSupport`), whose twelve
  classes run in four collections, one per WP7 sub-package; phase 4's are split the same way (in
  Git.Tests `ContinueSkipTests` and `ContinueSkipRebaseTests`, `InteractiveRebaseTests`,
  `InteractiveRebaseGuardTests`, `InteractiveRebaseRefusalTests`, `InteractiveRebaseListParityTests`,
  the three pull-rebase parity classes; in App.Tests `RepositoryOperationsRebaseTests`,
  `…SequenceTests`, `…PullRebaseTests`, `…ConflictsTests`, `…BackupCleanupTests`, the resolver's
  and the list's view-model, view and control tests). The default run is bound by the
  processors (git's processes and the virus scanner): more git-heavy tests side by side only make
  every git call slower, and the waits of the headless tests (10 seconds by default) closer to
  their limit.
- `GitIsolation` also sets `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` to 2026-01-02 09:00 UTC for
  the app under test, and `RealApp.Launch` hands it the same (D72): a commit the app makes gets
  the same id in both passes and on every machine.
- Small text is anti-aliased so that no pixel need have its exact colour; checks read the brush a
  small text is drawn with, and sample colours of shapes and large text only.
- `VisualCommit.RealWindowTests`: `RealApp` starts the built app with the same isolation as the
  headless tests (also `VISUALCOMMIT_TIME_ZONE=UTC`, and `GitIsolation.ApplyMachineIndependence`:
  no askpass, ssh, proxy or editor variables of the machine, auto-fetch off; since phase 4 also
  `VISUALCOMMIT_CARET_BLINK=0`), optionally with a session file
  (`Launch(data, session)`), finds elements through UI Automation, gives real mouse, wheel and
  keyboard input, chooses a folder in the native folder dialog (`ChooseFolderInDialog`), moves
  and sizes the window, reads the app's log, and captures the window from the screen.
  `RealWindowRun` is one run of a pass: it empties the phase's folder, compares each capture with
  the scripted screenshot of the same step (at most 3% of pixels may differ, a pixel counting only when nothing within a pixel of it matches: D33, D58), saves a
  picture of the differing pixels, and writes `run.txt`. The project references
  `VisualCommit.Testing`, so it uses the same scenario repos, and compiles the scripted run's
  `Phase3Expectations*.cs` and `Phase4Expectations*.cs`, so both harnesses use the same constants
  and texts. Its tests never
  run in parallel (they share the desktop and the mouse).
- Phase 3's pass: `Phase3RealWindowPass.cs` (the run, `Phase3Run`, one folder and one `run.txt`
  for the whole pass; launching the app on a scenario and at a server; waiting for an operation's
  log line; `Compare(app, "<name>")` against the scripted picture of the same name) and
  `Phase3RealWindowPass.A.cs` to `.D.cs`, one per part, one test method per launch (checks that
  share a launch are one method, such as checks 23, 1, 5 and 7 on one ops copy). `RealApp` is
  partial: `RealApp.Phase3.cs` (clicks with keys held, a drag in steps whose button is always
  released, the open menu through `ContextMenu` and `ContextMenuItem` and their Invoke, waiting
  for the queue's log line) and `RealApp.Checks.cs` (the neutral point, set by moving the cursor
  in one jump so that it crosses no menu item, resting the pointer until a tooltip shows, replacing
  a box's text, rows below the banner from `ConflictBanner`'s bounds, left-panel rows by path, the
  status bar's progress, the window's close button). The graph's rows are not in UI Automation
  (D49): the pass right-clicks and drags them at positions computed from `WindowLayout` and the
  expectations. No step invokes an item that acts outside the window ("Copy …", "Open file",
  "Show in folder").
- Phase 4's pass: `Phase4RealWindowPass.cs` (the run, `Phase4Run`, launching on a scenario,
  waiting for an operation's log line, `Compare`) and `.A.cs` (A1 to A3), `.B.cs` (B6, B7), `.C.cs`
  (C10, C11), `.C2.cs` (C14 to C16): 10 launches, one test method each, for the steps the report
  marks "RW" (a picture of every new screen, a real drag of a list row, real typing into the
  result, a real double-click on a conflicted file, a real process for the merge tool), all at
  1100×700. `RealApp.Phase4.cs` (the banner), `.Phase4.Rebase.cs` (the list), `.Phase4.Conflicts.cs`
  (the resolver) and `.Phase4.Checks.cs` (the pass's own waits) find them through the automation
  ids of phase 4 design 7.4; the project compiles `Phase4Expectations*.cs` too. `RealApp.Launch`
  sets `VISUALCOMMIT_CARET_BLINK=0` and removes the machine's editor variables.
- `RealApp.PressKeys` sends a navigation key held in a chord (an arrow, Home, End, Page Up or
  Down, Insert, Delete) as its extended scan code, as the keyboard's own keys do: FlaUI sends a
  virtual key without the extended flag, which Windows takes for the number pad's key, and Shift
  with a number-pad arrow while Num Lock is on cancels the Shift (Shift+Down moved the caret
  without selecting, found by phase 4's gate).

### CI

`.github/workflows/ci.yml` builds the solution in Release and runs `dotnet test` on
`windows-latest`, `macos-latest` and `ubuntu-latest` for every push and pull request that changes
more than docs. On Windows it also builds, but does not run, the real-window pass. Each job
uploads `artifacts/visual/` as `visual-<os>`, so the scripted screenshots of all three platforms
can be downloaded and looked at. A newer push to the same branch cancels the run before it
(`concurrency`). Time limits: 32 minutes for each of these jobs with 24 for its test step (D152,
amending D134: the Windows test step passed 12 of the old 14 minutes once phase 4's fifth test
process ran), and 25 for each container job with 14 for its test step, so that a hang cannot use
up the job's time. A test project added to the solution is picked up with no change to the
workflow.

Two more jobs, both named "bare ubuntu container", start from an empty `ubuntu:24.04`
container, run `scripts/setup-linux.sh` in it twice (the second run has to find everything in
place) and then build and test. The script installs the .NET 10 SDK and the system libraries
the tests need (ICU, fontconfig with a font, git, `openssh-client` for the tests of ssh's
passphrase prompt, a locale). It takes the SDK from the system's
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
Phase 2 added Avalonia.AvaloniaEdit 12.0.0 (built against Avalonia 12.0.0, used with 12.1.3),
TextMateSharp 2.0.4 and TextMateSharp.Grammars 2.0.4 (with Onigwrap, which ships Oniguruma for
every platform CI runs on), and the font JetBrains Mono NL 2.304 as a file in the repo. Phase 3
added no packages: the askpass helper, the menus (Avalonia's own `MenuFlyout`) and the test
server use what was there. Phase 4 added none either: the resolver is built on AvaloniaEdit
12.0.0 and the list on Avalonia's own controls.
