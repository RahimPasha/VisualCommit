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
  recorded (`IOperationLog`), and every git call names its operation; the activity log under the
  graph area shows both (T4, D156, phase 5). Since phase 6 a workspace's dashboard runs Fetch all
  and Pull all through each repository's own queue, at most four repositories at once (D209, D210).
- **Safety net.** Before a destructive operation the app stores a backup reference under
  `refs/visualcommit/backup/`: a snapshot commit before every discard (D67, phase 2), since
  phase 3 a backup batch before every reset, branch, tag or remote deletion, force push, stash
  drop, abort and checkout that leaves detached commits behind (D83), and since phase 4 one
  before every rebase and every Skip (D142, D144). Since phase 4 they are cleaned up
  automatically: a backup goes after 30 days, or when more than 50 exist, the oldest first, never
  while an operation is in progress (D139). Since phase 5 the app's own actions are undone and
  redone many steps back within a session, per worktree (O2, D154, D167); an older action is
  restored from the reflog page or the backups list (D172), and the clean-up keeps every backup
  an undo entry needs (D171).
- **Protected changes (C12, phase 5).** A checkout that git would refuse because uncommitted
  changes are in the way says so with git's count of the paths ("1 conflict prevents
  checkout. …", D153); a checkout of a commit the current branch reaches stashes every change
  first, without asking (D179). Since phase 6 every checkout, merge, rebase, cherry-pick, revert
  and stash apply or pop refuses to overwrite or delete an ignored file, a setting that is on by
  default (D193, D196, D221).
- **Prompts.** Git's and ssh's credential, passphrase and host-key prompts reach the app through
  a helper program, `visualcommit-askpass`, set as `GIT_ASKPASS` and `SSH_ASKPASS`; it asks the
  running app over a named pipe, and the app answers through a dialog in the window (D82).
  Since phase 7a the same helper is git's only credential helper for the URLs bound to a signed-in
  GitHub account, answered from the account's token without a dialog (D255).
  Editors never open: every call runs with `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:` (D68,
  D102). The interactive rebase alone sets its own sequence editor, for its one call: a shell
  function that copies a todo and message files the app wrote (D143).
- **Editors.** Diff, blame and conflict views are built on AvaloniaEdit, with TextMate grammars
  (TextMateSharp, called by the app's own code) for syntax highlighting (D64); the conflict
  resolver's ours and theirs panes have it, its editable result has none (D148).
- **Hosting.** One provider interface with GitHub, Azure DevOps and GitLab implementations.
  Tokens live in the OS keychain. Since phase 7a the interface and GitHub's provider are built, for
  github.com only (D258), with one account at a time (D263), whose token is kept in the keychain or,
  where none can be reached, in memory for the session (D253); nothing is asked of GitHub while no
  account is signed in (D257). Azure DevOps and GitLab come in phase 7b (D241).
- **Storage.** Settings and session state as JSON in the per-user app-data folder, and since phase
  6 the workspaces in a file of their own (D189). Logs are local files.
  The data folder can be overridden (`VISUALCOMMIT_DATA_DIR`). Every test uses a temporary one, so
  tests never touch the user's real settings.
- **Tests.** xUnit. Git-layer tests run real git against temporary repos, remotes included: bare
  repositories on disk, and a small HTTP server in the tests (`AuthRemoteServer`) for sign-ins.
  UI tests use Avalonia's headless mode and capture screenshots; the real-window pass repeats
  some checks on the real desktop. Since phase 7a that server also stands in for GitHub: git, the
  REST API, avatars and web pages on one loopback port (`FakeGitHub`, D250, D266), so no test of
  the default run or CI reaches a real host or keychain; an opt-in command, Windows-only, tries
  real GitHub with the owner's token for a sandbox repository.
- **CI.** GitHub Actions builds and tests on Windows, macOS and Linux on every push that changes
  more than docs, with a time limit on each job and on its test step; since phase 7a it also tests
  the real keychain stores against throw-away entries (D259, D270).
- **Packaging.** Velopack for installers and auto-update on all three platforms.

## Solution layout

`VisualCommit.slnx`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`
and `global.json` sit at the repo root. App projects are under `src/`, test projects under
`tests/`. A project is created in the phase that first needs it. Package versions are pinned
centrally in `Directory.Packages.props`.

| Project | Contents | State |
|---|---|---|
| `src/VisualCommit.Core` | Domain models and interfaces (git calls, repositories, commits, refs, the working-tree status with its conflicts and the operation in progress, rebases, lists of picks, the backups' retention, the lane layout, settings, session state, logging, data-folder paths, how dates are shown); diffs: parsing them, building patches for chosen lines, comparing the words of paired lines; ref-name rules (`RefNameRules`); the interactive rebase's todo (`Git/RebaseTodo`); `Conflicts/`: a conflicted file's blocks (`ConflictText`); `Operations/`: the operation queue, its contracts and the operation log, failure classification and output cleaning, the prompt contracts, the lock-file scan; phase 5's `Undo/` (the undo history, its scopes, effects, sequences and checks), `Graph/GraphFilter` (what the graph hides or solos), the result records of file history, blame, reflog, backups, compare and search, the record and backup clocks (`AppClocks`, `Git/BackupClock`) and the worktree key; phase 6's `Workspaces/` (the workspaces, `IWorkspaceStore`, `WorkspacePaths`, `WorkspaceEdits`, the folder scan `RepositoryScanner`), `Git/FetchRefspecs` (the remotes' fetch lines and their mapping), the contracts and records of git's own settings (`IGitConfiguration`, `GitConfigLocations`, `PullConfiguration`, `PullModes`, `GitBoolean`), `Operations/PromptGroup` (a bulk run's prompts), `ToolCatalogues` (what the machine has installed) and `DurableFile` (how the app's own files are written); phase 7a's `Hosting/AccountKey` (an account's provider, host and login), the account's bindings for git and the credential git gets (`Operations/CredentialBinding`), git's URL rewrites (`Git/UrlRewrites`) and the remotes' configured URLs (`Git/RemoteUrl`), and the status bar's offer (`OperationOffer`); no UI, no process calls | Built |
| `src/VisualCommit.Git` | Git runner, git locator, call log; reading a repository and writing to it (`GitRepository`: status, diffs, stage, unstage, patches, snapshots, discard, commit; phase 3's branches, history, in-progress state, backups, remotes and the network, stash, tags, ignore; phase 4's rebases, lists of picks and reverts, Continue and Skip, conflicts and the merge tool, the backups' clean-up; phase 5's file history, blame, reflog, backups list, compare, search, the filtered graph, C12's checkout and stash members, and the undo history's reads and steps; phase 6's `OpenedAtTopAsync`, the refs read's fetch lines, the reads of Keep ignored files, the pull's settings and the repository's own configuration writes; phase 7a's remotes' URLs in the refs read, the read of git's URL rewrites and a pull request's fetch into `FETCH_HEAD`), cloning, the repository watcher (since phase 7a with a test switch that holds its events, D262), the progress parser, the blame parser; since phase 6 `GitConfiguration` (git's own settings as the settings screen reads and writes them, and the merge-tool lookup) and `GitMergeTools`; `Prompts/`: the askpass broker (since phase 7a also answering git's credential requests for the URLs bound to the hosting account), its locator and the prompt classifier | Built |
| `src/VisualCommit.AskPass` | The askpass helper `visualcommit-askpass` (D82): a small framework-dependent console program with no project references, run by git and ssh; its pipe protocol (`AskPassProtocol`) is shared with the broker. Since phase 7a also git's credential helper for the URLs bound to the hosting account (`CredentialMode`, run as `--vc-credential <action>`, D255), whose request line, reading of git's input and answer text `AskPassProtocol` holds too. `VisualCommit.Git` references it, so its app host lands next to the app and in every test project's output | Built |
| `src/VisualCommit.App` | Avalonia views, view models, theme, custom controls (the commit graph, the diff text, the conflict panes and result, the blame's text and gutter, the context menu's item, the masked text box), the code font; settings and session stores, log file, folder dialog, file launcher, clipboard, prompt dialogs; the operations of every repository (`RepositoryOperations`), menus, dialogs, toolbar, status bar, conflict banner, auto-fetch; the interactive rebase's list, the conflict resolver, the message dialog; phase 5's undo and redo, the activity log, the graph area's pages (file history, blame, reflog and backups), the search bar, the comparison, hide and solo, C12's automatic stash and refusals; phase 6's workspaces (the welcome page's section, the dashboard tab, summary mode and the registry `OpenRepositories`), the bulk fetch and pull, the command palette and the keys (`ViewModels/Commands`, `Views/KeyRouter`), the settings screen (`ViewModels/Settings`), Open in editor, the workspace store, the System theme, the tab strip's overflow; phase 7a's Accounts section of the settings screen and its sign-in dialog, "Clone from GitHub" and its repository browser, the pull requests' section, their badges in the graph, their checkout and the create form (`ViewModels/Hosting`, `RepositoryOperations.PullRequests.cs` and `.CreatePullRequest.cs`), the authors' avatars in the graph and the details panel, and the status bar's offer. Its assembly and executable are named `VisualCommit` (`VisualCommit.exe`); its namespace is `VisualCommit.App` | Built |
| `src/VisualCommit.Hosting` | The hosting services, since phase 7a: the provider model (`IHostingProvider`, `HostingOptions` with `GitHubEndpoints`, the records of `HostingRecords.cs`, `HostingRules`); `GitHub/`: `GitHubProvider`, its REST client `GitHubClient` (kept answers, redirects, paging, failures), recognition `GitHubUrls` and GitHub's JSON shapes; the session (`IHostingSession`, `HostingSession`, `JsonAccountStore` for `accounts.json`), each repository's hosting (`RepositoryHosting`), the avatars' images (`AvatarCache`) and e-mail maps (`AuthorMapStore`); `Tokens/`: the keychain stores and `TokenVault`. References Core only, and `Tmds.DBus.Protocol` for Linux's Secret Service; no UI and no git process. The App references it | Built: GitHub (phase 7a). Azure DevOps and GitLab in 7b (D241) |
| `tests/VisualCommit.Testing` | Shared test helpers: temporary-repo builder, scenario repos, the 100k-commit repo, the credential test server, fakes (file launcher, clipboard, prompt handler, ssh), a clock tests move by hand (`ManualTimeProvider`), the hang watchdog, helpers that drive the app in headless mode, the window layout both harnesses share; since phase 6 the workspace "Web" of ten repositories (`TempWorkspace`, built by `LeanRepositories`), the fetch hold (`FetchHold`), the system's theme as a fake (`FakeSystemTheme`), the contrast and ink readers (`Contrast`, `TextInk`); since phase 7a (it references `VisualCommit.Hosting`) the fake of GitHub (`Hosting/`: `FakeGitHub`, its in-process handler, `LoopbackOnlyHandler`, `TokenTripwire`), `AuthRemoteServer`'s host mode, the scenario "hosting" (`Scenarios.Phase7a.cs`), the fake keychain (`FakeTokenStore`), the git-call budgets (`GitCallBudget`) and `RecordingCredentialSource`. Not a test project | Built |
| `tests/VisualCommit.Git.Tests` | Tests of Core and Git against real git and temporary repos, the askpass helper and the test server included; since phase 6 the workspace model and the folder scan, the fetch lines against git, git's own settings, Keep ignored files' reads; since phase 7a the scenario "hosting" (`Phase7aScenarioTests`), `AuthRemoteServer`'s host mode, the watcher's lag, a pull request's fetch, and the credential helper, its bindings, the URL rewrites and the prompt channel (see "Prompts and credentials") | Built |
| `tests/VisualCommit.App.Tests` | Settings, session, log, data-folder and view-model tests, the operations against real git through scenarios (since phase 5 the undo history's recording, steps and restores, and C12), and headless UI tests of behaviour (menus, dialogs, drag, selection, toolbar, status bar, banner, and since phase 5 the activity log, the pages, the search bar, the comparison, the window's keys; since phase 6 the workspaces, the dashboard, summary mode, the bulk runs, the palette, the keys, the settings screen, the editor and the theme); since phase 7a `GitCallBudgetTests`, the watcher lag's log line (`GitRepositoryProviderTests`), the credential tests of the operations, the dashboard and the push guard, and a pull request's fetch whose `FETCH_HEAD` another fetch rewrote (`PullRequestFetchHeadTests`); 7a's view-model, menu and view tests are in `VisualCommit.VisualTests.Phase7a` and `VisualCommit.Hosting.Tests` instead, so that App.Tests, which ends last in the default run, gained only 7a's operation, credential and budget tests (design 0.6) | Built |
| `tests/VisualCommit.Hosting.Tests` | Phase 7a: the hosting's tests (the options, the token stores, GitHub's client and provider, the avatar cache, the hosting session, the fake of GitHub and the fake keychain) and the six classes with real git that D334 moved there (five start the app through `ShellDriver`; `RepositoryHostingWiringTests` builds a tab's `TabServices` by hand with a `HostingSession`), on a headless UI thread of its own beside the 7a walk-through's; every fake there on port 0 or in process (D265). Part of the default test run and CI; CI's keychain steps run its real-store classes | Built |
| `tests/VisualCommit.VisualTests` | Scripted walk-throughs of phases 0 to 2 in headless mode with screenshots, for the visual test gate. Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase3` | Phase 3's scripted walk-through, a project of its own so that it runs beside phases 0 to 2's with a headless UI thread of its own. Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase4` | Phase 4's scripted walk-through, a project of its own for the same reason (D151). Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase5` | Phase 5's scripted walk-through of parts A, B, F and P (the shell and the activity log, the pages, finding, protected changes; D181). Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase5Undo` | Phase 5's part U, undo and redo, one action per test method: a project of its own beside `Phase5`, whose base class, support and common expectations it compiles; both save their pictures in phase 5's one folder (D181). Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase6` | Phase 6's scripted walk-through of parts W (workspaces and the dashboard), K (keys and the palette), S (settings and the theme's following) and G (ignored files and refspecs), 38 test methods (40 since phase 7a's first package split G23's part B into three, D264, D283), with the base class, support and common expectations (D230). Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase6Look` | Phase 6's part L, the look (every main screen in both themes, the light colours, the long folder, the tab strip), 9 test methods: a project of its own beside `Phase6`, whose base class, support and common expectations it compiles; both save their pictures in phase 6's one folder, and its test server has a port of its own (the report's ruling 1, D230). Part of the default test run and CI | Built |
| `tests/VisualCommit.VisualTests.Phase7a` | Phase 7a's scripted walk-through of parts A (accounts and sign-in), B (browse and clone), P (pull requests), G (the graph's badge and avatars) and N (a new pull request), 27 test methods, against the fake of GitHub on port 47395 (D265), and 7a's expectation, view-model, menu and headless view tests, linking App.Tests' `RecordingActions.cs` and `RecordingLog.cs` (design 0.6, 2.8). Part of the default test run and CI | Built |
| `tests/VisualCommit.RealWindowTests` | The real-window pass (FlaUI), since phase 7a with a part for 7a that serves the fake of GitHub on 47395 in the test process. Windows-only and opt-in. It is **not in the solution file**; that is what keeps it out of `dotnet build`, `dotnet test` and CI test runs (D25); CI's Windows job builds it (D337) | Built |
| `tests/VisualCommit.LiveHostingTests` | Phase 7a's opt-in command against real GitHub with the owner's token for a sandbox repository (D250, "The live command"). Windows-only; **not in the solution file**, run only by its own command; CI's Windows job builds it and never runs it (D337) | Built |

All projects target `net10.0`, except `VisualCommit.RealWindowTests` and, since phase 7a,
`VisualCommit.LiveHostingTests` (`net10.0-windows`).
`Directory.Build.props` turns on nullable reference types, implicit usings and warnings as errors
for every project.

`Directory.Build.targets` trims the platform-specific assets that packages bring under
`runtimes/` to the app and every test project, above all the native libraries of SkiaSharp and
HarfBuzzSharp, which Avalonia.Skia brings, for 21 platforms (D239); the part for Windows applies to
every package's (Avalonia's ANGLE, Avalonia.Native's and Onigwrap's too).
Its target `VisualCommitTrimNativeAssets` runs after `ResolvePackageAssets`, before anything is
copied or written into the `.deps.json`, so the output folder and its `.deps.json` agree. On every
platform it removes the packages' native `.pdb` files (from `RuntimeTargetsCopyLocalItems` and, for
a build with a `RuntimeIdentifier`, `NativeCopyLocalItems`); on Windows, in a build without a
`RuntimeIdentifier`, it keeps only `runtimes/$(NETCoreSdkPortableRuntimeIdentifier)` (`win-x64` on
the laptop), `runtimes/win` and `runtimes/any`. Two switches, false by default and set with
`-p:<name>=true` or as environment variables, undo one part each: `VisualCommitNativeSymbols`
copies the `.pdb` files again, and `VisualCommitAllRuntimes` every platform's libraries, which a
portable `dotnet publish` needs (phase 8). A test project's `bin/Debug` is about 49 MB with it,
about 589 without (2026-10-28), and its `runtimes/` holds only `win-x64/native`
(`libSkiaSharp.dll`, `libHarfBuzzSharp.dll`, `av_libglesv2.dll`, `libonigwrap.dll`); the
real-window project keeps `runtimes/win` for FlaUI's `System.Management.dll`. On macOS and Linux
only the `.pdb` part applies, so their builds, CI's included, keep every platform's libraries.

## As built (after phase 7a)

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
the reftable ref format too. Phase 5 added history, traceability and undo: undo and redo of the
app's own actions, many steps back per worktree, from the toolbar and from Ctrl+Z and Ctrl+Y
(Cmd+Z and Cmd+Shift+Z on macOS), a remote put back with a lease after a question; the activity
log, a panel under the graph area with every operation and its git calls; three pages that open
in place of the graph (a file's history following renames, its blame with click-through to
commits, and the reflog of HEAD or a branch with the backups list and its restores); "All files"
in the details panel; search over the graph's rows (message, author, SHA, a file, the changes'
text) with marks and dimmed rows; two selected commits, or two branches, compared in the right
panel; hide and solo of branches, kept in the session; every stash's line drawn dashed; and C12:
the refusal of a checkout that would overwrite uncommitted changes, with git's count, and the
automatic stash before checking out a commit the current branch reaches. Phase 6 added several
repositories at once and productivity: workspaces, named lists of repositories kept in
`workspaces.json`, each shown by a dashboard tab at the start of the tab strip with every
repository's branch, ahead and behind, changes and state, read without opening the repository in a
tab; Fetch all and Pull all from the dashboard through each repository's own queue, four at a
time, a failure kept in its row; the command palette (Ctrl+Shift+P, Cmd+Shift+P on macOS) over the
app's commands, the menus' actions, the tabs, repositories and workspaces; a fixed set of keyboard
shortcuts for every main action, shown in the palette, the menus and the tooltips; the settings
screen, with the identity, the pull mode and the merge tool kept in git's own configuration, global
or the repository's, beside auto-fetch, the stash, Open file's extensions, the editor of a new
"Open in editor" and Keep ignored files; ignored files kept by every checkout, merge, rebase,
cherry-pick, revert and stash apply or pop; the remotes' fetch refspecs read and followed; the
theme's System choice, which follows the operating system; the light theme's colours at 4.5:1; and
a tab strip that shrinks its tabs, then scrolls them. Phase 7a added the hosting foundation and
GitHub, for github.com and one account at a time: a sign-in with a personal access token, kept in
the system's keychain (in memory for the session where none can be reached or a save fails), which
GitHub's REST API uses and, while its "Use for git over HTTPS" is on, git for the account's
repositories, through the app's own credential helper; a browser of the account's repositories to
clone from; a repository's open pull requests in the left panel, with a badge on their branches in
the graph, a checkout of each and a new pull request from a branch; and the authors' GitHub avatars
in the graph and the details panel. The tests stand in for GitHub with a fake of their own
(`FakeGitHub`), and an opt-in command, outside the default run and CI, tries real GitHub with the
owner's token for a sandbox repository.

### Start-up and composition

```
Program.Main                        src/VisualCommit.App/Program.cs
  └─ App (Avalonia Application)     App.axaml(.cs): loads theme resources, Fluent theme, control,
       │                            menu and dialog styles; tooltips on the overlay layer (D105)
       └─ AppSession.Start          AppSession.cs: the composition root
            ├─ KeyTexts.Current     phase 6: every key text in the platform's words, Ctrl or Cmd
            │                       by the platform's command modifier (ShortcutKeys, D214)
            ├─ FileAppLog           one log for the session
            ├─ JsonSettingsStore    loads settings.json (preferences); since phase 6 the log says
            │                       when VISUALCOMMIT_AUTO_FETCH_MINUTES overrides the interval
            ├─ ThemeService         applies the saved theme before the window exists; since phase 6
            │                       over ISystemTheme, and logs "Theme: …" (D223)
            ├─ JsonSessionStore     loads session.json (tabs, recent list, window, panels; D46;
            │                       since phase 5 the graph filters and the activity log's height;
            │                       since phase 6 a dashboard tab by its workspace's id)
            ├─ JsonWorkspaceStore   phase 6: loads workspaces.json (D189, D203)
            ├─ the clocks           the record clock and the backup clock (AppClocks: the
            │                       system's, or a harness's fixed ones; D173)
            ├─ OperationLog, GitCallLog
            │                       the record of every operation and of every git call; the call
            │                       log keeps an operation's calls as long as its record (D173)
            ├─ GitAccess            the pending search for git (D45)
            ├─ DialogHostViewModel  the window's dialog stack (D69, D104)
            ├─ PromptDialogPresenter, PromptDialogs
            │                       git's and ssh's prompts as dialogs ("Prompts and credentials")
            ├─ HostingSession       phase 7a: GitHub's endpoints (HostingOptions) and the token
            │                       store (TokenStores), from the process's environment unless
            │                       given; the account, its token, GitHub's provider ("Hosting:
            │                       the session and the account")
            ├─ AskPassBroker        the session's prompt channel (PromptDialogs.CreateBroker); since
            │                       phase 7a with the hosting session as its credential source
            ├─ GitConfiguration     phase 6: git's own settings over the session's runner, with
            │                       the kept list of merge tools (merge-tools.json; D202, D217)
            ├─ ToolCatalogues       phase 6: the machine's editors, merge tools and PATHEXT
            │                       (EditorCatalog.MachineCatalogues), or the harnesses' Fixed (D220)
            ├─ TabServices          what every tab uses: GitRepositoryProvider (which gives every
            │                       repository it opens the backup clock), the folder dialog,
            │                       settings, the session, DateDisplay, the log,
            │                       RecentRepositories, the dialog host, the OperationLog, the
            │                       prompt broker, FileLauncher, ClipboardService, the call log;
            │                       since phase 5 the record clock, the session's UndoHistories
            │                       (one per worktree) and the GraphFilterStore; since phase 6 the
            │                       workspace store, WorkspaceItems, WorkspaceCommands, the
            │                       registry OpenRepositories, BulkSlots, GitSettings, Tools and
            │                       ReadClock; since phase 7a Hosting, the hosting session
            ├─ MainWindowViewModel  the tabs, restored from the session (dashboards first); the
            │                       menu layer (MenuLayerViewModel), the ActivityLogViewModel (which
            │                       follows the active tab) and the AutoFetchScheduler, started;
            │                       since phase 6 the window's commands (Commands: the keys and the
            │                       palette) and the settings screen's entry
            └─ MainWindow           created, given the session (UseSession), not shown
```

- `AppSession` is one running instance of the app on one data folder. New services are created
  and wired there, by hand; there is no dependency-injection container.
- `App.OnFrameworkInitializationCompleted` starts a session only when there is a desktop lifetime,
  and disposes it when the desktop exits. Headless tests have none: they call `AppSession.Start`
  themselves with a temporary data folder and fakes
  (`AppSession.Start(paths, application, folderPicker, launcher, clipboard, promptHandler,
  recordClock, backupClock)`; all but the first two are optional: the first four replace the
  folder dialog, the file launcher, the clipboard and the prompt dialogs, and the clocks default
  to those the environment asks for, `AppClocks.RecordClockFromEnvironment` and
  `BackupClockFromEnvironment`: the system's, unless `VISUALCOMMIT_RECORD_CLOCK=frozen` and
  `VISUALCOMMIT_BACKUP_CLOCK=fixed`, which the real-window pass sets).
- Phase 5 (D173): `AppSession.Start` makes one `OperationLog` and hands it to the `GitCallLog`
  (`new GitCallLog(operations: operations)`), so that an operation's calls leave the call log when
  its record leaves the operation log, and to `TabServices`; the record clock goes to git's
  search (`GitLocator.DetectAsync`, hence every runner) and through `TabServices.RecordClock` to
  every queue. `MainWindowViewModel` makes the `ActivityLogViewModel` from `TabServices.Operations`
  and `GitCalls` once the tabs are restored (`ActivityLog.Follow(this)`), and `CloseAll` disposes
  it.
- Phase 6 (D220, design 17.2): `AppSession.Start` also takes `gitEnvironment`, the session's own
  git environment, which `GitLocator.DetectAsync` gives the session's one `GitRunner` and which it
  applies after its defaults (both harnesses put `GIT_CONFIG_GLOBAL` and `XDG_CONFIG_HOME` in the
  session's data folder; the desktop app passes none); `systemTheme`, the operating system's mode
  (null: the platform's `PlatformSystemTheme`, which the session makes, owns and disposes; the
  headless harness passes `FakeSystemTheme`); `tools`, the `ToolCatalogues` (null:
  `ToolCatalogues.FromEnvironment()`, which is `Fixed` when `VISUALCOMMIT_TOOL_CATALOGUES=fixed`, as
  the real-window pass sets it, else the machine's); `readEnvironment`, the reader of
  `VISUALCOMMIT_AUTO_FETCH_MINUTES` that the start-up line and the auto-fetch scheduler share; and
  `readClock`, the clock of a dashboard's 10 s rule (`TabServices.ReadClock`; null: the session's
  clock). `AppSession.Workspaces` and `AppSession.Themes` expose the store and the theme service.
- Phase 7a (design 1.7, 7.11): `AppSession.Start` also takes `hosting` (`HostingOptions`) and
  `tokens` (`ITokenStore`); null reads each from the process's environment
  (`HostingOptions.FromEnvironment` and `TokenStores.FromEnvironment` over
  `Environment.GetEnvironmentVariable`), never through `readEnvironment`, which stays auto-fetch's
  reader. Every test process starts with `VISUALCOMMIT_GITHUB_URL=none` and
  `VISUALCOMMIT_KEYCHAIN=memory` (see "Tests"). The session makes one `HostingSession` over them,
  with `Dispatcher.UIThread.Post` as its post delegate and `TimeProvider.System` as its clock,
  before the prompt broker, which takes it as its `IGitCredentialSource`, and hands it to
  `TabServices` (`Hosting`; `NullHostingSession.Instance` where tests build `TabServices` by hand).
  The constructor starts `AppSession.HostingStarted`, the hosting session's `StartAsync` on the
  thread pool, once `Initialization` has begun and the window exists: it reads `accounts.json` and
  at most one keychain entry and asks GitHub nothing (ruling 6). `ShellDriver.Start` pumps the UI
  thread until it completes.
- `AppSession.Initialization` is `MainWindowViewModel.InitializeAsync`: it looks for git, then
  opens the repositories of the tabs restored from the session and loads their whole history.
  Since phase 6 it also awaits the first read of a restored dashboard that is the active tab, beside
  those openings, and starts the lookup of the merge tools git knows once the first restored tab's
  load ends, or at once without one (D202). Tests await it before they look at the window.
- Services that need git hold the `GitAccess` and ask it for the runner when they need it
  (`GetRunnerAsync`), which waits for the search and throws `GitUnavailableException` when git
  was not found. Nothing assumes git is there.
- `AppSession.Dispose` confirms the close (`MainWindowViewModel.ConfirmClose`: the window does
  not wait for running operations) and closes the window (which saves its placement), then
  `MainWindowViewModel.CloseAll`, which since phase 6 first cancels every open dialog, the top one
  first (`DialogHostViewModel.CancelAll`, D234: a folder scan, an editor search, the settings
  screen's reads and writes stop before their next step), then stops auto-fetch, disposes every tab
  (logging each graph's frame times, D50; a tab's queue stops its network operation; a dashboard
  releases its rows' repositories), disposes what the registry still holds
  (`OpenRepositories.DisposeAll`) and waits a few seconds for a cancelled clone to remove its folder;
  then it disposes, since phase 7a, the hosting session (the tabs' hostings went with their view
  models; its lifetime is cancelled, which stops the start's read, a sign-in's second half, the
  refreshes, the refusal check and the avatar runs, and its two HTTP clients are disposed; a begun
  sign-out goes on), then the prompt broker, git's settings (which stops the merge-tool lookup) and
  the theme service before the platform's theme it owns, and closes the log (`FileAppLog.Close`), so
  that a git call that ends later writes nothing (D132).
- `Program.Main` logs unhandled exceptions to the same log file.

### Repository tabs (D45)

| Type | Role |
|---|---|
| `MainWindowViewModel` | `Tabs` (never empty) and `ActiveTab`; `NewTab`, `ActivateTab`, `CloseTab`; saves the tabs and the active one to the session whenever they change; the theme switch and the git status as in phase 0; `Dialogs`, the window's dialog layer; `Menu`, the menu layer; `AutoFetch`; `NamesRepositoryInDialogs` (more than one tab has a repository: dialogs name their repository, "In &lt;repo&gt;"); since phase 5 `ActivityLog` (the panel's view model; its being open is the window's, see "Activity log"); `TryClose` and `ConfirmClose` (closing the window while operations run, see "Operation queue"). A tab closed while an operation the user started runs in it is kept until its queue is idle. Since phase 6, in partial files: `.Workspaces.cs` (`IWorkspaceHost.ShowWorkspace`, a row's open, a deleted workspace's tab, what the close waits for), `.Session.cs` (the restore with the dashboards first, `ActiveTabIndex`), `.Commands.cs` (`Commands`, the palette, the repositories a key or the palette opens), `.Settings.cs` (`OpenSettingsCommand`, the merge-tool lookup) and `.Theme.cs` (`Theme`, `UseTheme`); `NamesRepositoryInDialogs` is true while a dashboard tab is open or more than one tab has a repository (design 3.8); `OnWindowActivated` (the window's `Activated`) reads the active dashboard's stale rows. Since phase 7a `.Hosting.cs` (`SignInToGitHubAsync`, the window's sign-in entry, see "The settings screen") and, in `.Commands.cs`, `CloneFromGitHubAsync` (see "Clone from GitHub") |
| `RepoTabViewModel` | One tab: `Title` ("New tab" or the repository's folder name), `CanClose`, the `Welcome` page, the open `Repository` or null. `LeftPanel`, `Details`, `Changes` (the stage panel), `ShowsWorkingChanges`, `BranchText`, `Toolbar` and `OperationStatus` are the repository's, or empty ones (an empty toolbar has every command disabled; an empty status says "Ready"). Since phase 4 `ShowsRightPanel` (false while the conflict resolver is open), `ShowsDetailsPanel`, `ShowsStagePanel` and `GraphColumnSpan` (2 while the resolver is open, else 1) say what the right-hand columns show (D140). A tab restored from the session shows its folder's name at once (`ShowPending`) and opens it once git is found; one that fails keeps its folder in the session (`FailedPath`) until the user opens something else in it. Since phase 6 a tab can be a workspace's dashboard (`ShowDashboard(workspace)`, once): `Dashboard`, `IsDashboard`, `ShowsDashboard`, `ShowsLeftPanel` (false on a dashboard), `GraphColumn` (0 on a dashboard, else 1), `SavedWorkspaceId` and `TabAutomationId` (`DashboardTab` or `RepoTab`); on a dashboard `Title` and `BranchText` are the workspace's name and follow a rename, `GraphColumnSpan` is 3, `ShowsRightPanel` is false, `Toolbar` is the dashboard's (`ToolbarViewModel.ForDashboard`) and `OperationStatus`, typed `IStatusBarOperation`, its `DashboardBulkViewModel`. `CloseToolTip` and `CloseKeyText` give the close button Mod+W on the active tab only. `ShowAsync` waits while a closed tab's or a released summary's queue still runs on the worktree (`WaitingText`, "Waiting for {title} to finish…"), then adopts the worktree's summary-mode view model when a dashboard holds one (see "Summary mode, the registry and adoption") and registers what it shows |
| `WelcomeViewModel` | The welcome page (D42): Open and Init through `IFolderPicker`, the clone form with its progress (`CloneProgress` from git's stderr) and Cancel, the shared recent list. A clone opens a prompt scope ("Clone", `PromptMode.Ask`) and passes its variables to `CloneAsync`, so an ssh or HTTPS clone asks in the window. Since phase 6 its "Workspaces" section (see "Workspaces"), and `InitAtAsync`, which the palette's and the key's Init share. Since phase 7a the form's "From GitHub…" while signed in (`ShowsGitHubButton`, `BrowseGitHubCommand`), and a GitHub URL's binding to the account in the clone's prompt scope (see "Clone from GitHub") |
| `RepositoryViewModel` | One open repository: its `Graph` (`CommitGraphData`), `SelectedIndex`, `Selection` (`GraphSelection`, by SHA, D88) and `MultiSelection`, `IsWorkingRowSelected` and `WorkingRowText`, `ScrollOffset` (kept per tab), `LeftPanel`, `Details`, `Status` (`WorkingTreeStatus`) with `HasWorkingChanges`, `Changes` (the stage panel), the open `Diff`, `Refs` as last read, `BranchText`, the watcher and the refreshes, the Q1 measurements; phase 3's `Queue`, `Operations` (`RepositoryOperations`), `OperationStatus`, `Toolbar`, `Banner` (`ConflictBannerViewModel`), `MenuContext` and the menus' entry points; phase 4's `InteractiveRebase` (the list, or null), `Resolver` and `IsResolverOpen`, `ShowsGraph`, `ShowsInteractiveRebase` and `ShowsDiff` (what the graph area shows), `MergeTool`, `BackupsToKeep` (see "The graph area: the list, the resolver and the pages"); phase 5's `UndoHistory` (its worktree's, from `TabServices.UndoHistories`), `Page` and `ShowsPage`, `Search` and `SearchMarks`, `Comparison`, `Filters`, `GraphFilter` and `IsReloadingGraph`, `ShowCommit`, `ShowInGraph`, `Reveal`. Split over `RepositoryViewModel.cs`, `.Menus.cs` (menus, double-clicks, the drop), `.Selection.cs` (multi-selection), `.InProgress.cs` (banner with Abort, Continue and Skip and their questions, working-row text, left-panel sync, the stopped commit's message, the questions about conflict markers), `.GraphArea.cs` (phase 4's hooks: the list, the resolver, leaving the graph area, the focus when they close, `merge.tool`, the clean-up at opening), and phase 5's `.Pages.cs` (the pages and their host, a commit shown from a page, the search bar's place and marks, the filter's reload, the restore bar's tie to the undo history), `.Find.cs` (the search's and the comparison's hosts) and `.Filters.cs` (hide and solo), and phase 6's `.Palette.cs` (the palette's items from the menus) and `.Settings.cs` (whether an editor is chosen, for the file menus). Since phase 6 it has a summary mode for a dashboard's rows (`ForSummary`, `IsSummary`, `Promote`, `FirstRead` and `HasFirstRead`; see "Summary mode, the registry and adoption"), and `IsWatcherQuiet`, which the harnesses wait for (see "Watching and refreshing"). Disposing it (`IsClosed` from then on) disposes its queue and cancels all its other work |
| `RecentRepositories` | The recent list, newest first, at most 10, kept in the session |
| `GitRepositoryProvider` | `IRepositoryProvider` with real git: `OpenAsync`, `InitAsync`, `CloneAsync` (with the clone's prompt variables) through `GitRepository`. Since phase 6 an open and an init run whole on the thread pool, so that `GitRepository.OpenAsync`'s own folder check never holds the UI thread, also for a folder on a share that does not answer (design 2.10, D59) |
| `DashboardViewModel` | Phase 6: a workspace's dashboard inside its tab (`RepoTabViewModel.Dashboard`): its rows, their reads, its header, its menus and its bulk runs (`Bulk`); see "The dashboard tab" |
| `OpenRepositories` | Phase 6: the registry of the session's repository view models by worktree (`TabServices.OpenRepositories`); see "Summary mode, the registry and adoption" |

The window's regions show the active tab: `MainWindow.axaml` binds the left region to
`ActiveTab.LeftPanel`, the graph region (`CommitGraphView`) to `ActiveTab`, and the right region
to `ActiveTab.Details` (`RightPanelView`, while `ShowsDetailsPanel`) or, while the working-changes
row is selected, `ActiveTab.Changes` (`WorkingChangesView`, while `ShowsStagePanel`); the status
bar's branch to `ActiveTab.BranchText`. `CommitGraphView` shows `WelcomeView` for a tab without a
repository, and for one with a repository one of: `RepositoryGraphView` (`ShowsGraph`), the
interactive rebase's list `InteractiveRebaseView` (`ShowsInteractiveRebase`), `DiffView`
(`ShowsDiff`, also over the list or a page), the conflict resolver `ConflictResolverView`
(`IsResolverOpen`) or, since phase 5, a page (`ShowsPage`: `PageSlot`, a `ContentControl` whose
template is `DialogViewLocator`, which finds `FileHistoryView`, `BlameView` or `ReflogView` for
`Repository.Page`), with the conflict banner above whichever shows, and nothing while a restored
tab is still opening. The pages' panel comes after the diff's and the resolver's in
`CommitGraphView.axaml`, so that a lookup by name finds the main diff's parts before the history
page's own diff. Since phase 5 `CommitGraphView` is a
grid of three rows: that content, then the activity log's splitter (`ActivityLogSplitter`, 5
high, with a 1-px `VcBorderBrush` line) and `ActivityLogView`, both bound to the window's
`ActivityLog.IsShown`; a closed panel takes no room. While the resolver is open,
`CommitGraphView`'s `Grid.ColumnSpan` is bound to `GraphColumnSpan` and covers the right panel's
column too, which keeps its width, and the right panel and `RightSplitter` are hidden (D140), so
`PanelLayout.Fit` and the widths in the session are unchanged. The toolbar binds to
`ActiveTab.Toolbar`, the status bar's operation part to `ActiveTab.OperationStatus`, its Activity
log toggle to the window's `ActivityLog`. Over the main grid lie, in this order, the menu layer
(`MenuLayer`), the drag layer (`DragLayer`) and the dialog layer (`DialogLayer`, an
`ItemsControl` over `Dialogs.Open`), shown while a dialog is open.

Since phase 6 (D206, design 2.4) a dashboard tab covers the three columns: the left panel and
`LeftSplitter` follow `ActiveTab.ShowsLeftPanel`, `CommitGraphView`'s `Grid.Column` and
`Grid.ColumnSpan` are bound to `GraphColumn` and `GraphColumnSpan`, and the right panels and
`RightSplitter` follow `ShowsDetailsPanel`, `ShowsStagePanel` and `ShowsRightPanel`, all false
there. The column definitions, `PanelLayout.Fit` and the session's panel widths are untouched, so a
repo tab comes back with its panels as they were. `CommitGraphView` shows `DashboardView` in a slot
of its own (`DashboardSlot`, while `ShowsDashboard`), with the activity log under it, and the
centred `TabWaitingText` while a tab waits for a closed queue on its worktree.

#### Summary mode, the registry and adoption (phase 6, D209, design 3)

A dashboard row's Fetch and Pull run through an ordinary `RepositoryViewModel`'s queue and
`RepositoryOperations`, so they have the toolbar's titles, Cancel, error dialog, dedupe and undo
recording. A row whose worktree is open in a tab uses that tab's view model; any other row gets one
in summary mode.

- **Summary mode** (`RepositoryViewModel.ForSummary(repository, services)`, `IsSummary`): the usual
  constructor, so the view model has its queue, `Operations`, `OperationStatus`, `Toolbar`, undo
  history (`TabServices.UndoHistories`, shared with any tab on the worktree) and `BackupsToKeep`;
  its `StartAsync` reads only the refs and the status, side by side, applies them as a tab does but
  for what nothing shows (the left panel, the stage panel, the resolver), completes `FirstRead`
  and logs `{name}: summary read in {ms} ms` at Debug. It makes no watcher (D207 amends D52 and
  D71), loads no history, leaves the session's graph filter alone, runs no clean-up at opening and
  logs none of Q1's lines; its `RefreshAsync` reads the refs and the status again, never a history,
  and `GitSettingsChanged` does nothing for it. `FirstRead` (both modes) completes once the first
  start has applied the refs and the status, or has failed, or the view model closed; a row that
  follows a tab counts as not read until it does, and a bulk run waits for it.
- **The registry** (`ViewModels/Workspaces/OpenRepositories.cs`, `TabServices.OpenRepositories`),
  keyed by `WorktreeKey`, used on the UI thread only: `RegisterTab` and `UnregisterTab` (a tab's
  full view model, in registration order), `TabFor(key)` and `TabForFolder(folder)` (the first
  registered tab still open, by key or by `WorkspacePaths.Same`), `AcquireSummaryAsync(repository,
  holder, lifetime)` (the worktree's summary-mode view model with the dashboard added as a holder,
  made when there is none, so two dashboards share one; null when a tab holds the worktree, the
  holder's lifetime ended or `DisposeAll` ran), `ReleaseSummary(viewModel, holder)` (at the last
  holder's release the view model is disposed, by D81's rules), `TryAdopt(key)`, `WhenIdleAsync(key)`
  and `ClosedRunningOn(key)`, `NetworkQueueOn(commonKey, except)` (a queue the registry holds on that
  common git folder that runs a network operation, for the bulk slots and auto-fetch), `Summaries`,
  `Closing`, `Tabs`, `Changed`, `DisposeAll`.
- **A worktree is held while any queue on it runs** an operation, a background one included: a tab
  that unregisters and a summary released while its queue runs (kept in `Closing`) leave the queue's
  idle task with the key, and `ShowAsync` and `AcquireSummaryAsync` wait for it, so no new queue
  runs beside a running one on one worktree (D81 stays literal; the lead's ruling at status step
  22). The window's close waits only for operations of the user.
- **Adoption** (design 3.3): a tab that opens a worktree whose summary-mode view model the registry
  holds takes that object (`TryAdopt`), drops the repository it had just opened and calls
  `Promote(stopwatch)` with its own stopwatch, so Q1's lines and auto-fetch's first interval count
  from the user's open; a summary read still running then applies nothing (a read generation), and
  a first read that a summary failure completed starts again. The full `StartAsync` follows, and
  while it loads a promoted view model puts off every refresh asked for (the adopted operation's
  end, a watcher's event) to one at its end, so no second history loads beside the first. The
  queue's watcher pause belongs to the view model (`PauseWatcher`, counted under a lock), so a
  watcher the full start makes while an adopted operation runs is paused from its creation until
  that run ends. A running operation, a dashboard's pull say, goes on in the tab. An adoption raises
  no `Changed`; the adopting tab's registration does, so the rows keep the same object and what
  they kept of it.
- **Rows that use a tab's view model** follow its live `Refs`, `Status` and `BranchText`, kept by
  the tab's watcher; the dashboard never reads them, and their operations run in the tab's queue,
  so the tab's status bar, auto-fetch and D123's dedupe see them. The first registered tab serves
  the row, not the leftmost. When the last tab on a row's worktree closes, the row keeps its values
  and counts as never read; its next read acquires a summary once the closed queue is idle.
- **Closing the window** (design 3.7): `TryClose`, "Finishing operations" and `CloseAll` take the
  tabs' view models, closed tabs' that still run, `OpenRepositories.Summaries` and `.Closing`, each
  once; before the queues drop what waits, every dashboard's bulk run starts no more rows
  (`StopStarting`).

### From a repository to the screen

1. `RepoTabViewModel.OpenAsync(path)` starts a stopwatch for Q1 and asks the provider, which
   waits for git and calls `GitRepository.OpenAsync`: one `git rev-parse` gives the working tree,
   the git folder and the common git folder, or `NotARepositoryException`.
2. `RepositoryViewModel.StartAsync` starts the watcher, then reads the refs
   (`ReadRefsAsync`: `for-each-ref`, `stash list`, `symbolic-ref`, `rev-parse HEAD` and, since
   phase 6 in place of `git remote`, `config --null --get-regexp ^remote\.`, which gives the remotes
   and their fetch lines; in parallel) and shows them in the left panel and the status bar. Since phase 5 HEAD's commit is
   that of HEAD's branch as `for-each-ref` listed it, so that HEAD and its branch come from one
   read; `rev-parse HEAD` stands only for a detached HEAD, an unborn branch and a branch the list
   missed (found by phase 5's gate: a commit made in a terminal between the two reads gave HEAD the
   new commit and its branch the old one, and the graph showed the new row without its label for
   one refresh). A change made between the refs' read and git log's can still show so for one
   refresh, which that change's own refresh corrects. The working-tree status
   (`ReadStatusAsync`) is read beside them and shown once the refs are; it decides the
   working-changes row.
3. At the same time `LoadCommitsAsync(Task<RepoRefs>, GraphFilter?, ...)` runs one `git log
   --date-order` (D47), which does not need the refs (it names `HEAD` with `--ignore-missing`, for
   an unborn HEAD). Without a filter its revisions are D47's (`GraphFilter.Unfiltered`); with one,
   those of "Solo and hide". `CommitPager` parses each line as git prints it, waits for the refs
   before the first page, merges the stashes in by date (D54) and hands over pages: 100 commits,
   then 2,000.
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
| `GitCommand` | One call: arguments (passed one by one, no shell quoting), working directory, optional standard input, extra environment variables (a null value removes a variable), optional line handlers for streamed output, and the `Encoding` of standard input and output (UTF-8, or `GitCommand.Latin1` for byte-exact content, D66). `DisplayText`, used in logs and errors, hides the credentials of a URL; `HideCredentials(text)` turns every URL's user information into `***`, `RemoveCredentials(text)` removes it (what "Copy URL" copies, D103). Since phase 7a an authority ends at a control character too (`EndsAuthority(c)`: `/`, `?`, `#`, white space or a control character; public for the runner's cut, `RecordedOutput`), so the `\0` between `config --null`'s entries ends a URL that has no trailing `/`, and the next entry's user information is hidden as well (design 4.2, 4.3). Since phase 5 `RecordsStandardInput`: the call's record keeps its standard input; set only for `update-ref --stdin`, whose lines name refs and SHAs and carry no secret. Since phase 6 `BelowNormalPriority`: on Windows the runner puts git, and what it has started by then, at below-normal priority right after the start (the merge-tool lookup, design 8.10); ignored elsewhere |
| `IGitRunner` / `GitRunner` | Runs a command without blocking. A non-zero exit code is returned in the `GitResult`, not thrown; `GitResult.EnsureSuccess` turns it into a `GitException` that carries git's own standard error and, since phase 3, its standard output (`StandardOutput`: a merge writes its messages there). Since phase 5 it takes the record clock (`GitRunner(path, callLog, log, recordClock)`) for the times the activity log shows; since phase 6 also the session's git environment (`environment`, kept as a copy in `SessionEnvironment`), applied to every call after the runner's defaults and before the command's own (D220), and its defaults remove the variables that select another repository (D233, below) |
| `GitResult` | Exit code, standard output, standard error, duration |
| `GitCallRecord` | The record of a call: command, folder, outcome, exit code, start and duration by the system clock, the first 16 KiB of each output stream with credentials hidden (D103), and `OperationId`, the queue's operation the call belongs to, or null for a read outside one. Phase 5 (D173): `Sequence` (a number from `RecordSequence`, the process's one counter, which operations take theirs from too, taken when the call starts), `DisplayedStart` and `DisplayedDuration` (by the record clock), `StandardInput` (for `RecordsStandardInput`, cut and with credentials hidden as the output is) and `OnTemporaryIndex` (the call ran with `GIT_INDEX_FILE` set: a snapshot's index) |
| `IGitCallLog` / `GitCallLog` | Keeps the session's call records in two stores (phase 5, D173): an operation's calls as long as its `OperationRecord` stays in the `OperationLog` (it listens to `Evicted`), within a budget of 64 MB for all of them that drops the oldest operation's calls first (`HasDroppedCalls`); the reads, calls outside an operation, in a ring of their own of the newest 2,000. So a burst of reads never takes an operation's commands away. Events `Added` and `Removed` |
| `GitLocator`, `GitSearchContext`, `GitDetection`, `GitVersion` | Finding git and checking its version |
| `IGitRepository` / `GitRepository` | One open repository. Reads: `ReadRefsAsync`, `ReadHeadAsync`, `LoadCommitsAsync`, `ReadCommitDetailsAsync`, `ReadStatusAsync`, `ReadDiffAsync`, `ReadFileAsync`, `ReadFileSizeAsync`. Writes: `StageAsync`, `UnstageAsync`, `ApplyPatchAsync`, `SaveSnapshotAsync`, `DiscardAsync`, `RestoreSnapshotAsync`, `CommitAsync`. `CreateWatcher`; static `OpenAsync`, `InitAsync`, `CloneAsync`. Since phase 5 `OpenedFolder`: the folder the repository was opened from (a subfolder, or a path through a link, which git's `--show-toplevel` resolves; the activity log counts reads made there as the worktree's), and `BackupClock`, which the provider sets. Phase 3's to phase 7a's members are listed in "Phase 3's repository members" to "Phase 7a's repository members" below. Split over `GitRepository.cs`, `.Refs.cs`, `.Commits.cs`, `.Clone.cs`, `.Changes.cs` (status, diffs, file versions), `.Writes.cs`, phase 3's `.Backups.cs`, `.Branches.cs`, `.History.cs`, `.InProgress.cs`, `.Remotes.cs`, `.Stash.cs`, `.Tags.cs` and `.Ignore.cs`, phase 4's `.Rebase.cs`, `.Sequence.cs` and `.Conflicts.cs`, phase 5's `.FileHistory.cs`, `.Reflog.cs`, `.Compare.cs`, `.Search.cs` and `.Undo.cs`, phase 6's `.KeepIgnored.cs`, `.PullConfig.cs` and `.Config.cs`, and phase 7a's `.Hosting.cs` and `.PullRequests.cs`. Since phase 6 a `.git` file whose folder is gone (an orphaned worktree) opens as `NotARepositoryException` both in git's words up to 2.55 ("not a git repository") and from 2.56 ("gitfile does not point to a valid repository") |
| `WorkingTreeStatus` | The staged files (HEAD → index), the unstaged ones (index → working tree, untracked as added, conflicted as `FileChangeKind.Conflicted`) and which are untracked, each list in byte order of the paths; from `git status --porcelain=v2 -z --untracked-files=all --renames`. `InProgress` (`InProgressState`) is the operation in progress, read from git's files (see "Toolbar, status bar, conflict banner and auto-fetch"). Since phase 4 `Conflicts` (a `ConflictEntry` per `u` line), `Submodules` (the paths whose `sub` field starts with `S`), `HasUnstagedTrackedChanges` (a tracked file that is not conflicted and not a submodule has unstaged changes: git's rebase Continue refuses then), `FindConflict(path)` and `SameAs` (both lists, the conflicts and the state compared) |
| `InProgressState`, `InProgressKind` | What is in progress: `Kind` (None, Merge, CherryPick, Revert, Rebase, ApplyMailbox), the commit and its subject, a rebase's branch and new base, `Remaining` (the commits a terminal's sequence or a rebase has left), `Empty` (a pick or revert that changes nothing), `AbortKeepsHead` (D110). Phase 4's init properties: `Sequenced` (a `sequencer/` folder: a list), `HasPickHead` (a list with no pick head is paused between commits), for a rebase `StopKind` (`RebaseStopKind`), `NextIsSquash` (the stopped commit is the target of a fixup or squash) and `Group` (the app's group the stop is in), and `Done` and `Total` (a rebase's commit commands done and in all; a list's commits made and in all) |
| `MergeResult`, `MergeMode`, `ApplyResult`, `ResetMode`, `StashApplyMode`, `PullOutcome`, `PullResult`, `PullIntegration` | How phase 3's writes ended or what they run: a merge `UpToDate`, `FastForward`, `Merged` or `Conflicts`; a cherry-pick, revert or stash apply `Applied`, `Conflicts` or `Empty`; a pull's integration `UpToDate`, `FastForward`, `Merged`, `Rebased`, `Conflicts` (every stop of its rebase, with or without conflicted files) or `UpstreamGone`, with the commits HEAD gained, and since phase 4 `LeftOutByForkPoint` and `AutostashConflicted`, since phase 6 `IgnoredFilesInTheWay` (nothing integrated) with `PullResult.IgnoredFiles` and `WouldRebase`; `PullIntegration` `AsConfigured` (the toolbar's Pull) or `Rebase` (chosen for one pull, D147) |
| `RebaseResult`, `RebaseOutcome`, `RebaseStopKind` | How a rebase the app ran ended (phase 4 design 1.4): `UpToDate`, `Rebased` (with the new tip, and `AutostashConflicted` when `rebase.autoStash`'s changes conflicted when put back) or `Stopped` (with the conflicted files); why a rebase stopped: `Pick`, `Rescheduled` (an untracked file was in the way), `ExecFailed`, `OwnAmendFailed` (the app's guarded amend failed: a hook refused the new message), `Edit`, `Empty` |
| `RebaseAction`, `RebaseStep`, `RebasePlan`, `RebaseList`, `RebaseListEntry` | The interactive rebase: an action per commit (Pick, Reword, Squash, Fixup, Drop), a plan (onto, the branch's expected tip and full name, the steps oldest first with a reword's or a group's message) and the list read when it opens (onto, the branch's tip, the commits oldest first with subject, author and whole message, the merge count) |
| `RebaseTodo` (Core) | Pure: the todo and message files of a plan (design 2.5, D143): full SHAs, LF, UTF-8 without BOM, an explicit `drop` per dropped commit; every reworded row and every group, fixup-only groups included, as `label vc-<n>`, `pick <target>`, `fixup <member>`… and the guarded amend (`GuardedAmend(n)`: `exec` a `git commit --amend --only --allow-empty --cleanup=whitespace --quiet --file=<rebase-merge/vc-message-n>` that runs only when `HEAD^` is `refs/rewritten/vc-<n>`), the message in `vc-message-<n>`. `SquashMessage` and `SquashPlan` build a squash of selected commits. Since phase 6 `LeadingPicks(plan)` (the targets of the first groups written as plain `pick` lines, which git may skip) and `AppliedCommits(plan)` (every `pick` or `fixup` line's commit in todo order), for Keep ignored files' check (design 9.7). `RebaseTodoTests` |
| `SequenceResult`, `SequenceStop`, `ContinueResult`, `ContinueOutcome` | How a list of picks or reverts ended (`None`, `Conflicts`, `Empty`, `Refused`, with the commits applied and the list's length, D145); how Continue or Skip ended (`Done` or `Stopped`, the new tip, `AutostashConflicted`) |
| `ConflictEntry`, `ConflictKind`, `ConflictSide`, `ConflictContent`, `MergeToolResult` | A conflicted path as `git status --porcelain=v2` prints its `u` line: the kind in git's words (both modified, both added, deleted by them or us, added by us or them, both deleted), whether it is a submodule, each stage's mode and id (`HasBase`, `HasOurs`, `HasTheirs`); what the resolver reads of it (the working-tree bytes, the marker size, whether a side is binary or a symbolic link); whether the merge tool staged it |
| `ConflictText` (Core, `Conflicts/`) | Pure: a conflicted file split into common segments and blocks (`Parse(text, markerSize)`: a `<` line, an optional `|` base section, one `=` line, a `>` line, each of the marker size, as strictly as git's rerere reads them; anything else is common text), each block's ours, theirs and base lines and labels, `Choose(block, BlockChoice)` (`Ours`, `Theirs`, `OursThenTheirs`, `TheirsThenOurs`), the whole ours and theirs files, `HasMarkerLines` (any marker line of the size, the markers question's test), `Decode` and `Encode` byte for byte (UTF-8, else Latin-1, line ends kept). `ConflictTextTests` |
| `BackupRetention`, `RemovedBackup`, `BackupRemovalReason` | The backups' clean-up (D139): `BackupRetention.Default` is 30 days and 50 backups; what it removed and why (older than the days, beyond the count) |
| `NetworkOptions` | What a phase 3 call is given beyond its token: the prompt scope's variables (`Environment`), where progress goes (`Progress`), settings passed as `git -c` before a remote command (`Configuration`: auto-fetch's network timeout, D125), and `PromptCancelled`. `NetworkOptions.From(context)` builds it from a running operation. Local writes take it too: a filter or hook they run can prompt (git-lfs). Since phase 7a the `Environment` of a network operation bound to the hosting account holds its scope's credential pairs (see "Prompts and credentials"), and `Configuration` never holds a `credential.*` or `include.*` key, which git would read after those pairs (D329) |
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
| `RepoRefs`, `GitRef`, `HeadState`, `StashEntry` | What the refs are at one moment. HEAD's commit is its branch's as `for-each-ref` listed it (phase 5's gate, see "From a repository to the screen"). `RepoRefs.Fingerprint` changes when HEAD, a ref's target or the stashes change (D52). A local branch's upstream is read as three values (D101): `UpstreamRef` (`%(upstream)`, `refs/remotes/origin/main`, or `refs/heads/main` for a local upstream), `UpstreamRemote` (`%(upstream:remotename)`, `.` for a local upstream: `HasLocalUpstream`) and `UpstreamRemoteRef` (`%(upstream:remoteref)`, the branch's name on the remote); phase 1's short `Upstream` stays for the left panel. Never cut out of `%(upstream:short)`. Since phase 6 (D222, design 10.2) `Refspecs`, every remote's fetch lines as the refs read read them (null in fakes and hand-built snapshots), `SourceOf(trackingRef)` and `SourceOf(remoteBranch)` (where a remote-tracking ref is fetched from: `Refspecs.SourceOf`, or while `Refspecs` is null exactly phase 5's assumption, the default line's mapping) and `LocalNameOf(remoteBranch)` and `LocalNameOf(trackingRef)` (the local branch a remote branch checks out into, design 10.6; the second for a ref the snapshot may not hold); a remote branch's `GitRef.RemoteName` is the remote whose lines fill it, and only when two remotes or none do `RemoteBranchNames.RemoteOf` (the longest configured name its name starts with, else its first part) |
| `CommitInfo`, `CommitKind` | A row of the graph: a commit or a stash (one parent: the commit it was made on) |
| `CommitDetails`, `ChangedFile`, `FileChangeKind` | What the details panel shows; files compared with the first parent, renames detected |
| `CommitPager` | Internal: turns `git log` lines into pages and merges the stashes in. A date git cannot print (an ident without a time zone) is read as 1970-01-01 UTC, as git does, so such a commit is not lost. For a filtered graph (phase 5, D178, amending D54 while a filter is on) it keeps the commits it knows will be shown (the shown refs' tips and HEAD's, then the parents of every commit added while a stash waits): a stash whose base is known is placed by date as before, any other waits and goes in just above its base, and one whose base never comes is left out |
| `CloneProgressParser`, `CloneProgress` | Git's clone progress lines as stage and percentage |
| `RepositoryWatcher` | `IRepositoryWatcher` (D52, D71): `FileSystemWatcher`s on the git folder, the common folder and the working tree. In the git folders, changes under `objects/` and to `*.lock` files are ignored; in the working tree, changes under `.git` are left to the others, and a burst whose paths `git check-ignore` calls all ignored is dropped. 300 ms of quiet before `Changed`, at most 2 s while changes keep coming, measured on a `TimeProvider` (the system's; since phase 5 a test passes `ManualTimeProvider` and moves it by hand, so that a slow machine cannot split a burst, D160); `RepositoryChangedEventArgs` says whether the git folder or the working tree changed. `Pause()` stops it reporting while an operation of the repository's queue runs (the queue calls it). Changes under the file-system monitor's `fsmonitor--daemon/` are ignored too: it writes a cookie there for every `git status`. `Start()` walks the working tree's folders on Linux, so the view model calls it on a background thread. Since phase 6 `IsQuiet` (a default member of `IRepositoryWatcher` that returns true): false from a burst's first change until its raise has ended, so that a harness can wait for the refresh a change starts (see "Watching and refreshing") |
| `WindowsJob` | Internal: a Windows job object, used on Windows to stop git together with everything it started |
| `FileHistoryRow`, `BlameResult`, `BlameLine`, `BlameCommit`, `BlameIgnoreRevsFileMissingException` (Core); `BlameParser`, `GitQuotedPaths`, `GitRawDates` (Git) | Phase 5's file history and blame (design 4): a history row is the commit as the graph's `CommitInfo`, the entry's status, the path at that commit and the old path of an `R` or `C`; a blame is the lines' bytes as Latin-1 strings (D66), each with its commit (SHA, author, e-mail, author time and zone, summary, the path at that commit, `previous`, `boundary`; the zero SHA for lines not committed yet), and `IsSubmodule` for a gitlink, which has no lines. `BlameParser` reads `--porcelain` as a three-state machine (a header, metadata lines, a tab line) and checks that the final line numbers run 1 to N; `GitQuotedPaths` unquotes git's C-quoted paths; `GitRawDates` reads `--date=raw`'s `<seconds> <zone>` |
| `ReflogResult`, `ReflogEntry`, `BackupInfo`, `BackupRefInfo` | Phase 5's reflog page (design 5): the entries newest first (selector, number, commit, parents, git's message, the commit's subject, the entry's own time), how many numbers git skipped (`Skipped`: entries that name no commit that exists), `More` (the walk went past the limit), `ForcePushLease` (a force push's lease from the remote-tracking reflog); a backup is one operation's refs (kind, time, retry number), each ref with its object, message and the commit it keeps |
| `CompareResult`, `CompareBase`, `CompareCounts` | Phase 5's comparison (design 6): both SHAs as resolved, which side the list was read from (`Trees`, `MergeBase`, `SeveralMergeBases`, `NoCommonHistory`), that side's SHA, the changed files, whether `diff.renameLimit` stopped the rename search, and for branches the commits only on each side |
| `SearchMode` | `Commits`, `Files`, `Changes` (design 3.2) |
| `PushOutcome`, `PushedRef` | What a push changed (phase 5, D170): each remote ref with its old value (null for a new ref) and new value, read from git's summary line, never from the remote-tracking ref |
| `StashPaths` | The paths a stash would write (phase 5): `Changed` (its working-tree commit against its base, both sides of a rename), `Untracked` (its third parent's files), `Staged` (what its index commit changes) and `Added` (the paths its base lacks); since phase 6 `Deleted` (the changed paths with status `D`, which Keep ignored files' check leaves out) |
| `HardResetSnapshot` | What `SnapshotForHardResetWithFilesAsync` saved (phase 5): the snapshot commit, its index commit, the paths and those it holds a file for, which the undo recording takes instead of reading them back |
| `WorktreeKey` (Core) | How the app tells worktrees apart (phase 5): the worktree's own git folder (`.git/worktrees/<n>` for a linked one), full, without a trailing separator, compared ignoring case on Windows and macOS. The undo histories and the activity log key by it |
| `BackupClock` (Core), `AppClocks` (Core) | The backups' clock (D139, phase 5's D173): `NextName()` names each new backup ref and discard snapshot, `Now` is the clean-up's "now"; `BackupClock.System`, or `BackupClock.Fixed(time)`, which names each backup one second after the last, the first one second after `time` (the harnesses give 2026-01-02 09:00:00 UTC), and whose "now" follows the last name, so a harness's backups get the same names in every run, in order. `AppClocks` holds that time (`HarnessTime`), the environment variables and `FrozenClock`, the record clock that stands still |
| `WorkingTreeFiles` (Core) | `IsInTheWayOfFile(root, path)`: a folder holding files stands where a file is to be written, or a file where one of its folders should be; `git restore --worktree` would delete either without a word (phase 5's review) |
| `FetchRefspec`, `FetchRefspecs`, `RemoteSource`, `RemoteSourceKind` (Core, `Git/FetchRefspecs.cs`) | Phase 6's fetch refspecs (D222, design 10.1), pure. `FetchRefspec.Parse(remote, value)`: one `remote.<r>.fetch` value read as git reads a fetch refspec (force, negative, a pattern with one `*` on both sides; an exact line without a destination maps nothing; null for a value git refuses or one without a source). `FetchRefspecs`: every remote's lines in configuration order (`Parse`, `Default` for tests); `SourceOf(trackingRef, remote)` the same for that remote's own lines alone (`Mapped`, `AmbiguousRefs` or `NotFetched`, whatever another remote maps it to: the push guard's question); since phase 7a `SourceOf(trackingRef, remote, sourceCanExist)`, the same with the candidate sources the predicate refuses left out first (none left: `NotFetched`; a predicate always true gives the two-argument answer; ruling 8, `FetchRefspecsSourcePredicateTests`); `SourceOf(trackingRef)`, the reverse mapping as git's tracking setup makes it: `Mapped` (one remote from one ref: `Remote`, `RemoteRef`, `IsBranch`, `Branch`, `IsIdentity`), `AmbiguousRemotes` (two or more remotes fetch it: git's "ambiguous information"), `AmbiguousRefs` (one remote fetches it from two or more refs: stricter than git, which takes the first) or `NotFetched`; a negative line of a remote that matches a candidate makes that remote map nothing; `DestinationsOf(remote, remoteRef)`, the forward mapping that the fetch uses. `FetchRefspecsTests`, and `FetchRefspecsParityTests` against `git branch --track` and a real fetch |
| `RemoteBranchNames` (Core) | Phase 6 (the review at status step 22, design 10.6): the one place a remote-tracking ref's name is split by its remote's name where the fetch lines are not asked or do not tell, remote names holding slashes as they may: `WithoutRemote(name, remote)`, `RemotesOf(name, remotes)` (every configured remote it starts with, longest first; more than one means the name alone does not tell), `LongestRemoteOf`, `RemoteOf` (the longest, else the first part). The refs read, `RepoRefs` (its phase 5 assumption and its local names), `GraphFilter.RemoteOf`, the left panel, the menus, the backups list's texts and the push backs call it. `RemoteBranchNamesTests` |
| `PullConfiguration`, `PullFastForwardSetting`, `PullRebaseSetting`, `PullIntegrationOptions` (Core, `Git/PullConfiguration.cs`) | Phase 6: Pull's settings as read before its fetch (D219, design 8.9): what `pull.ff` asks, what the rebase setting asks and which key decided it, the values as set, and `Problem` and `ProblemKey` for a value Pull does not understand, whose `NotUnderstood()` is the failure Pull reports (command `git config --get <key>`). `PullIntegrationOptions(Configuration, KeepIgnored)` is `IntegratePullAsync`'s optional last option (null: phase 4's pull) |
| `PullModes`, `PullMode`, `GitBoolean` (Core) | Phase 6: the settings screen's pull modes (D218, design 8.7): `PullModes.Read` names the mode `pull.rebase` and `pull.ff` give together, by their effect, and `Writes(mode)` the two keys a mode writes; `GitBoolean.TryParse`, git's reading of a boolean (the words ignoring case, integers with a unit), which the pull modes and the identity's `user.useConfigOnly` use |
| `IGitConfiguration` (Core), `GitConfiguration` (Git), `GitConfigRead`, `GitConfigValue`, `GitConfigScope`, `GitConfigWrite`, `GitIdentityRead`, `MergeToolHelp`, `MergeTool`, `GitConfigLocations` | Phase 6: git's own settings as the settings screen reads and writes them, and the merge-tool lookup (D190, D202, D217); see "Git's own settings" |
| `UrlRewrite`, `UrlRewrites`, `RemoteEffectiveUrls` (Core, `Git/UrlRewrites.cs`); `GitRepository.ReadUrlRewritesAsync` (`GitRepository.Hosting.cs`) | Phase 7a: git's URL rewrites (design 4.2, 4.3), by which the URLs bound to the hosting account are git's own (see "Prompts and credentials"). `Parse(entries)`: each `url.<base>.insteadOf` or `.pushInsteadOf` entry (the key without case, the base as written; an entry without a value skipped), in order. `Apply(remoteUrls)`, of `RepoRefs.RemoteUrls` (the refs read's `url` and `pushurl` values as configured, each a `RemoteUrl`): per remote, in order of first appearance, the URLs git uses, as measured on 2.56: the fetch URLs, each `url` after `insteadOf`; the push URLs, each `pushurl` after `insteadOf`, else the `pushInsteadOf` rewrites of the `url` values when any has one (those alone), else the fetch URLs. A rule is a plain case-sensitive prefix (an empty one matches every URL), the longest wins, a tie goes to the base first seen and then to its earlier value, nothing is rewritten twice, and `pushInsteadOf` never applies after `insteadOf`; a remote without `url` has no fetch URL. `ReadUrlRewritesAsync`: one `git config --null --get-regexp ^url\.` in the working folder as a read, with `GIT_CONFIG_PARAMETERS` removed as in a bound scope (D329), so that it sees the rules the bound calls apply; exit 1 is `UrlRewrites.None`, another failure throws. It runs only while the account binds git (a tab's read after its refs, a dashboard row's network operation, D290, D332), so no earlier count of git calls moves. `UrlRewritesTests`, `UrlRewritesParityTests` (against `git remote get-url --all` and `--push --all`) |

Rules the runner follows (see D30, D31, D36, D37, D56 and D59):

- Every call gets `LC_ALL` and `LANG` set to `en_US.UTF-8` and `LANGUAGE=en` (which gettext puts
  before `LC_ALL`, D128), so git's messages are English and parseable, `GIT_TERMINAL_PROMPT=0`,
  so git fails rather than waits for a terminal (prompts reach the app through askpass instead,
  D82), `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:`, so a command that wants an editor goes on
  without one (D68, D102), and `GIT_LITERAL_PATHSPECS=1`, so a path is never read as a pattern
  (D74). Three calls run without it: `git check-ignore`, which refuses it (exit 128 on 2.36 and
  2.55; since phase 5 every such call goes through one helper, `IgnoredAmongAsync`), `git stash
  push` (D108: git removes the stashed untracked files with `git clean … :/`, which the variable
  would turn into a file name; the app passes stash push no path), and since phase 5 the search's
  Files mode, whose glob pathspecs the variable would read as file names. Since phase 6 (D233)
  the defaults also remove the variables that point git at another repository, index, object
  store or namespace than the command's working directory names (`GIT_DIR`, `GIT_WORK_TREE`,
  `GIT_COMMON_DIR`, `GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES`,
  `GIT_NAMESPACE`, `GIT_PREFIX`, `GIT_IMPLICIT_WORK_TREE`, each set to null in
  `GitRunner.DefaultEnvironment`), so that an app started from a hook, a `git` alias or a shell that
  set one does not run every call against that repository. Since phase 6 the session's own git
  environment (`GitRunner.SessionEnvironment`, a copy of what the constructor was given, D220: the
  harnesses' global configuration in the data folder, empty in the desktop app; nothing in the app
  reads it back, the tests do) is applied after these defaults. A command's own variables come
  last and override both for that call only, and may set one of D233's again (a snapshot's
  temporary `GIT_INDEX_FILE`): the interactive rebase's
  `GIT_SEQUENCE_EDITOR` (D143) and the stopped commit's `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL` and
  `GIT_AUTHOR_DATE` for a rebase's Continue (D144); since phase 7a those of a prompt scope bound to
  the hosting account, whose `GIT_CONFIG_PARAMETERS` set to null thus drops the session's value and
  the process's (D329; see "Prompts and credentials"). Input and output are UTF-8, unless the
  command asks for Latin-1 (`GitCommand.Encoding`, D66): diffs, patches and blobs then travel byte
  for byte. Standard error is always UTF-8.
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
  pipes to git break. Since phase 6 the stop itself is queued to the thread pool
  (`ThreadPool.UnsafeQueueUserWorkItem`), not run on the thread that cancels, often the UI thread,
  which Cancel all makes cancel up to four calls at once (D59); a stop that comes after the call
  has let go of git finds nothing to stop. Stopping never throws, and a cancelled call returns
  within 10 seconds even if git could not be stopped. A cancel that comes after git has exited by itself (or so late that
  git still exits with success) and after its output was read stops nothing: the call keeps its
  result, so a push that went through is not reported as cancelled.
- The record of a call (`GitCallRecord`) has the credentials of every URL in its output hidden
  before the output is cut to 16 KiB, and a URL's authority that runs on to the cut is left out
  (D103, D129). It names the operation the call belongs to: `GitOperationScope.Current`, an
  `AsyncLocal` the queue sets around an operation's delegate, which flows into the runner's
  thread-pool work. Since phase 5 the call takes its sequence number and its record clock's time
  when it starts; its own `StartedAt` and `Duration`, which the log file's line uses, stay the
  system clock's.
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
| Branches (`.Branches.cs`) | `CheckoutBranchAsync` (`switch --no-guess <b>`), `CheckoutTrackingBranchAsync` (`switch --create <b> --track refs/remotes/<r>/<b>`), `CheckoutDetachedAsync` (`switch --detach`), `CreateBranchAsync` (`branch <name> <start>`, or `switch --create` to check it out), `RenameBranchAsync` (`branch --move`), `DeleteBranchAsync` (`branch --delete --force`), `FastForwardBranchAsync` (`fetch --no-auto-maintenance --no-write-fetch-head . <target>:refs/heads/<b>`, which refuses a non-fast-forward and a branch checked out in any worktree, D94), `MergeAsync` (the message from `git fmt-merge-msg` for the short name, then `merge --no-edit --no-log -m <message> <full ref>`, with `--no-ff` or `--ff-only` for the other modes, D94). Since phase 6 the checkouts, `CreateBranchAsync` and `MergeAsync` take `keepIgnored` (and `CreateBranchAsync` `noTrack`): see "Phase 6's repository members" |
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
each setting. Since phase 6 (D219, amending D84) the app's Pull reads these settings before its
fetch (`ReadPullConfigurationAsync`): a value it does not understand fails the pull before anything
is fetched, as `git pull` refuses before it fetches, and the configuration read then decides the
integration after the fetch (`PullIntegrationOptions.Configuration`), so a terminal's change during
the fetch is not seen, as with `git pull`. Phase 3's overload without options reads them itself.

Since phase 4 (D147) `IntegratePullAsync(branch, integration, beforeRebase)` takes the
integration: `AsConfigured` is the above (the toolbar's Pull, and phase 3's overload without a
callback); `Rebase` ("Pull from {up} (rebase)") follows none of git's pull settings, as `git pull
--rebase` ignores them (since phase 6, D231, it reads `pull.ff` all the same, before the fetch, and
refuses a value `git pull --rebase` refuses, with Pull's text; `pull.ff=only` and `false` give way
to the rebase, and no rebase setting is read): `merge --ff-only <head>` when the branch has nothing of its own,
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

#### Phase 5's repository members

As phase 3's and phase 4's. The reads run with `GIT_OPTIONAL_LOCKS=0` (D47), outside the queue,
except the undo history's and C12's, which run inside the operation they serve. C12's writes and
an undo step run inside one operation of the queue and take its `NetworkOptions`.
`IGitRepository.cs` names each member's git command and design section.

| Group | Members and their git commands |
|---|---|
| File history, blame, a tree (`.FileHistory.cs`, D175) | `ReadFileHistoryAsync(startSha, path, onRow)`: `git -c core.quotePath=true log --follow --root -M --name-status` with the graph's format and plain-log options, `<start> -- <path>`; not `-z`, with which a commit's entries share a line with the next header and each row came one commit late. Each row is handed over on its entry line, its paths C-unquoted (`GitQuotedPaths`). `--follow` lists no merge (git's own rule). `ReadBlameAsync(sha or null, path)`: `git -c i18n.logOutputEncoding=UTF-8 blame --porcelain --no-textconv [<sha>] -- <path>`, read as Latin-1 (D66) and parsed by `BlameParser`; the user's `blame.ignoreRevsFile` is honoured, and one naming a missing file throws `BlameIgnoreRevsFileMissingException`; a gitlink (mode 160000 in `ls-tree`, or in `ls-files --stage` for the working tree) returns a result with `IsSubmodule`. `ReadTreeFilesAsync(sha)`: `ls-tree -r -z --name-only <sha>` ("All files") |
| Reflog and backups (`.Reflog.cs`, D180) | `ReadReflogAsync(fullRef, limit)`: two walks side by side, `log --walk-reflogs --ignore-missing -z -n <limit + 1> --format=%gD%x1f%H%x1f%P%x1f%gs%x1f%s <ref> --` and the same with `--date=raw --format=%gD%x1f%H` (the selector then names the entry's own time, which no placeholder gives), zipped by position; when they list different commits (the ref moved between them) both are read once more, and a second disagreement fails the read. Never `.git/logs`, which reftable lacks. `FindLostCommitsAsync(shas)`: one `rev-list --ignore-missing --stdin --not --branches --tags --remotes HEAD --` with the SHAs on standard input (the `--` because a file named `HEAD` makes `HEAD` ambiguous). `ReadBackupsAsync()`: one `for-each-ref` of `refs/visualcommit/backup/` with each ref's object, its type, what a tag object points at and the messages, grouped into backups by phase 4's name scheme |
| Compare (`.Compare.cs`, D177) | `CompareAsync(from, to, threeDot, countCommits)`: both names to commits with one `rev-parse <from>^{commit} <to>^{commit} --`, kept from then on; for three-dot `merge-base --all` (the first base is the one `git diff A...B` uses; exit 1: no common history; more than one: a criss-cross); `diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id <old> <to>` with two SHAs, never a range (`diff-tree A...B` prints nothing); for branches `rev-list --left-right --count <from>...<to> --` beside it |
| Search (`.Search.cs`, D174) | `SearchAsync(mode, text, shas, onMatch, progress)`: `git log --no-walk=unsorted --stdin --encoding=UTF-8` with the plain-log options and the graph's rows' SHAs on standard input, the first 2,000, then 20,000 per call, one call after another, each match handed over as git prints it. Commits: `-F --regexp-ignore-case --grep=<text>` (the app matches authors and SHA prefixes in memory). Files: `--format=%x1e%H --first-parent --root --no-renames --name-only -- ":(glob,icase)**/*<t>*" ":(glob,icase)**/*<t>*/**"` without `GIT_LITERAL_PATHSPECS`; a commit matches when a path line follows its own line, since Git 2.55.0 (and Git for Windows 2.56.0.windows.2) ignore the path under `--no-walk`. Changes: `--first-parent --diff-merges=first-parent --root --find-renames --no-textconv -s --regexp-ignore-case -S<text>`, `--diff-merges` left out for good once git refuses it (older than 2.31). `fatal: bad object` ends the search quietly; an empty text runs nothing |
| Filtered graph (`.Commits.cs`, D178) | `LoadCommitsAsync(refs, filter, onPage)`: see "Solo and hide" |
| C12 (`.Branches.cs`, `.Stash.cs`, D179) | `CheckoutDetachedAsync(commit, noOverwriteIgnore)`: `switch --detach --no-overwrite-ignore <sha>` (since phase 6 the one `CheckoutDetachedAsync(commit, keepIgnored)`, see "Phase 6's repository members"). `ReadStashPathsAsync(stash)`: three reads side by side, `diff-tree -r -z --no-renames --name-status <s>^1 <s>`, `diff-tree -r -z --no-renames --name-only <s>^1 <s>^2` and `rev-parse <s>^3` (then `ls-tree -r -z --name-only` of it), kept per full SHA for the repository's life (a commit never changes; a name such as `stash@{0}` is read each time), shared by C12's apply check, the undo recording and an undo step's pop. `FindIgnoredAsync(paths)`: `check-ignore`, a folder asked about by its files. `RestoreWorkingTreeFilesAsync(source, paths)`: `restore --source=<s> --worktree --pathspec-from-file=- --pathspec-file-nul`. `PutBackUnclearedStashAsync(stash)`: see "Protected changes". `StashAsync` with untracked files makes again the empty untracked folders that `stash push`'s clean removes (`ls-files -z --others --exclude-standard --directory` before it) |
| Shared reads (`.InProgress.cs`) | `ReadInProgressStateAsync`: what is in progress, read from the git folder alone (no git call while nothing is, D89; it decides no empty stop); `ReadConflictedPathsAsync`: `ls-files --unmerged -z`. Used since phase 5's review wherever only these matter, in place of a whole status |
| Undo (`.Undo.cs`, D167, D169, D170) | `ReadRefStatesAsync(scope)` and `RunUndoStepAsync(step)`: see "Undo and redo" |
| Other | `CommitExistsAsync(sha)`: `cat-file -e <sha>^{commit}`. `SnapshotForHardResetWithFilesAsync(target)`: phase 3's hard-reset snapshot with its index commit, its paths and those it holds a file for (`HardResetSnapshot`). `PushAsync` and `PushTagAsync` return a `PushOutcome` read from git's summary lines (`ReadPushSummary`), each abbreviated value expanded with `rev-parse --disambiguate`; a value this repository lacks leaves that ref out of the outcome |

#### Phase 6's repository members

As phase 3's to phase 5's: reads run with `GIT_OPTIONAL_LOCKS=0` (D47), writes inside one
operation of the queue. `IGitRepository.cs` names each member's git command and design section;
`FakeRepositoryBase` answers the new reads with nothing found and no pull setting, and refuses the
configuration write.

| Group | Members and their git commands |
|---|---|
| Opening (`GitRepository.cs`, D204) | `OpenedAtTopAsync()`: whether `OpenedFolder` is the working tree's top, true with no git call when `WorktreeKey.Comparer` finds it equal to `WorkingDirectory` (an 8.3 short name was expanded by the opening's `Path.GetFullPath`), otherwise one `rev-parse --show-prefix` in it: an empty line is the top spelled another way (a junction, a symbolic link such as macOS's `/var`), anything else a folder inside; the interface's default answers true. A workspace saves a folder only at its top |
| The refs read (`.Refs.cs`, D222, design 10.2) | `ReadRefsAsync` reads the remotes and their fetch lines with one `config --null --get-regexp ^remote\.` in place of `git remote` (not `--local`: git fetches with global lines too), so a refs read stays at five processes: a key with a subsection names a remote, each once, sorted ordinally (the names `git remote` lists); the `.fetch` values go to `FetchRefspecs.Parse`, and every line the mapping leaves out but an empty value (an empty source, which git accepts; a key without a value or a line git refuses, after which git's own `for-each-ref` fails and the refs read with it) is logged at Warning once per repository object; exit 1 is no remote. `RepoRefs.Refspecs` holds them and `GitRef.RemoteName` follows them. `ReadFetchRefspecsAsync()` makes that one call alone, for the push guard inside its operation (the interface's default answers null: not read). The Remotes dialog's `ReadRemotesAsync` keeps `git remote` |
| Pull's settings (`.PullConfig.cs`, D219, D231) | `ReadPullConfigurationAsync(branch)`: `pull.ff`, then `branch.<b>.rebase` or, unset, `pull.rebase`, booleans read as git reads them; a value Pull does not understand fills `Problem` instead of throwing. `ReadPullFastForwardSettingAsync()`: `pull.ff` alone, as `git pull --rebase` reads it (`config --get pull.ff`, and `config --type=bool --get pull.ff` for an uncommon value). `IntegratePullAsync(…, pullOptions)`: with `AsConfigured` the configuration given decides instead of a read of its own (its `Problem` throws `NotUnderstood()` either way); with `Rebase` only a `pull.ff` problem counts; Keep ignored files' check runs once the integration is chosen, before anything is written (see "The ignored-files guard") |
| The repository's settings (`.Config.cs`, design 8.3) | `WriteConfigAsync(writes)`: per write `config --local --replace-all <key> <value>`, or `--unset-all <key>` for a null value (exit 5, an absent key, counting as done), in the working tree, in order; the first failure (a held `.git/config.lock`: exit 255) stops them and throws |
| Keep ignored files (`.KeepIgnored.cs`, `.Branches.cs`, D193, D196, D221) | The five reads `FindIgnoredInTheWayOfMergeAsync(into, source)`, `FindIgnoredInTheWayOfCommitsAsync(commits, revert, mergeParent)`, `FindIgnoredInTheWayOfRebaseAsync(tip, onto)`, `FindIgnoredInTheWayOfRebaseAsync(plan)` and `FindIgnoredInTheWayOfStashAsync(stash)`, and the required `keepIgnored` of `CheckoutBranchAsync`, `CheckoutTrackingBranchAsync`, `CheckoutDetachedAsync` (one overload in place of phase 5's two), `CreateBranchAsync` and `MergeAsync`, which put `--no-overwrite-ignore` just before the positional arguments; `CreateBranchAsync` also takes a required `noTrack` (`git branch --no-track` or `switch --create <b> --no-track`, D201). See "The ignored-files guard" |

#### Phase 7a's repository members

As phase 3's to phase 6's: reads run with `GIT_OPTIONAL_LOCKS=0` (D47), the fetch inside one
network operation of the queue. `FakeRepositoryBase` finds no URL rewrite and throws at the fetch
into `FETCH_HEAD`.

| Group | Members and their git commands |
|---|---|
| The refs read (`.Refs.cs`, design 1.2) | The one `config --null --get-regexp ^remote\.` of `ReadRefsAsync` also keeps every remote's `url` and `pushurl` values as configured, before `insteadOf`, in its order (`RepoRefs.RemoteUrls`, each a `RemoteUrl(Remote, Url, IsPushUrl)`; a key without a value gives none; never shown), so the refs read stays at five processes. The hosting recognises GitHub's remotes and binds git's URLs from them |
| URL rewrites (`.Hosting.cs`, D290, D329) | `ReadUrlRewritesAsync()`: see `UrlRewrites` above |
| A pull request's head (`.PullRequests.cs`, D305, design 10.5) | `FetchToFetchHeadAsync(remote, sourceRef, options)`, for `refs/pull/<digits>/head` only (anything else is an `ArgumentException`): one network call `fetch --progress --no-tags --refmap= --write-fetch-head <remote> <sourceRef>`, in which `--refmap=` keeps the remote's fetch lines from writing a ref, `--no-tags` its `tagOpt` from fetching tags, and no `--prune` lets a `fetch.prune` delete anything (D99): no ref and no configuration is written. It then reads the worktree's own `FETCH_HEAD`, never through a `rev-parse FETCH_HEAD`: the SHA when the file is one line of a full SHA, an empty second field and `'<sourceRef>' of …`, else null, since another fetch can rewrite it in between (plan.md's risk 33) |

**Git versions** that the code handles. The development machine runs Git 2.56.0.windows.4 since
2026-10-28 (D248); it ran 2.36.0.windows.1 before, on which phases 3 to 6 derived their facts. CI
runs Git 2.56.0.windows.2 (Windows, since 2026-10-15), 2.56.0 (macOS, and `ubuntu-latest` since
2026-10-29; 2.55.0 before) and 2.43.0 (the two containers), so since then no CI job runs 2.55;
Git 2.55 is still on the development machine, inside Visual
Studio (phase 5's research used it; a trimmed one, plan risk 28). Phase 4 found Git 2.55's
differences: at a conflict stop `rebase-merge/message` ends with a `# Conflicts:` block, left out
of the message the stage panel fills; `done` and `git-rebase-todo` write each subject as a comment
(`pick <sha> # Subject`), and the app reads only the command and the SHA; an autostash that
conflicts is told in other words, so the app tells it by the conflicted files left after an
exit 0; a rescheduled pick has no
`stopped-sha` and is recognised by `done` ending with the same command; a reftable repository can
exist (Git 2.45 and later). Phase 5 found: Git 2.55.0's `--no-walk` ignores a path (the Files
search above); from Git 2.34 `read-tree -m -u` overwrites or deletes ignored files without a word
(the undo's ignored-files guard); a merge refused for local changes exits 2 on 2.55, not 128, with
a last line `Merge with strategy ort failed.` and `error:` before the staged-file message (the
classifier keys on lines both gits print), and one refused for something staged writes, from Git
2.38, a no-op entry `merge <ref>: updating HEAD` into HEAD's reflog (2.32.0, 2.34.1, 2.36.0 and
2.37.3 write none; the undo recording never reads HEAD's reflog, so it records nothing); `git
stash pop` refused by local changes exits 128 on 2.56, 1 before (phase 3's check 15b accepts
both); Git 2.55's `remote add`
refuses a name that is a prefix of an existing remote's, or has one as its prefix, which 2.36
allows; reflog messages differ (2.36 adds `(continue)` and `(skip)` to a rebase's after a Continue
or a Skip, a rebase's abort and the app's guarded amend are worded otherwise on 2.55, and a fetch
on 2.55 also writes `refs/remotes/<r>/HEAD`), which the reflog page shows as git wrote them.
Phase 6 found: Git 2.56 words a `.git` file whose folder is gone "gitfile does not point to a
valid repository", where earlier gits say "not a git repository" (the opening takes both); from Git
2.42 `rebase.rebaseMerges` makes a plain `git rebase` keep merges, which 2.36 ignores (Keep ignored
files' rebase check reads the side branches' points whenever the range holds a merge); a unit
letter alone as a boolean (`k`) is false on 2.36 and refused by newer gits, and the settings screen
shows such a value as Other.

### Lane layout (D48, D54, D55)

`GraphLayout` (Core, `Graph/`) lays out one commit at a time in `git log --date-order` order and
returns a `GraphRow`: the node's lane and colour number and the `GraphLine`s of the row
(`PassThrough`, `IntoNode`, `OutOfNode`). A commit takes the leftmost lane that waits for it, or
else the leftmost free lane or a new one with a new colour; lanes that waited for it end in its
node and are free again before its parents are placed; the first parent continues in the node's
lane, a further parent joins a lane already waiting for it or else takes a free lane with a new
colour. Colour numbers only grow; the graph draws palette entry `colour % 8`. 100,000 commits lay
out in well under a second.

Since phase 5 (D161) a stash's lane is a stash lane down to the commit it was made on: the stash
row's `OutOfNode` line, the `PassThrough` lines below it and the `IntoNode` line at its base carry
`GraphLine.IsDashed` (an init property, false by default); from the base on the lane is the base's
own and solid. When another commit's line to a further parent joins a stash lane, the lane leads
to a real parent from there and is solid. Lanes, colours and nodes are as before.

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
  (`CommandModifiers`: Avalonia's `HotkeyConfiguration.CommandModifiers`, since phase 6 through
  `ShortcutKeys.CommandModifierOf`, Ctrl on Windows and Linux, Cmd on macOS, Control when the graph
  is in no window or the platform names none, never a test of the operating system) with a click
  adds or removes a row;
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
- **Dashed stash lines (phase 5, D161).** A line with `IsDashed` is drawn with dashes and gaps of
  3, as the working-changes row's line. Every dashed line takes its pattern from one origin, the
  bottom of a ring in the first drawn row (`DashOrigin`), so the pattern depends only on the
  line's absolute y: drawn row by row and cut by the view's edges it stays one pattern, a curve
  into the base's node carries it along its length, and where a stash made on HEAD's commit
  shares a lane with the working-changes row's line the two dash as one. Dashed pens for whole
  pixels are made once per theme and lane colour.
- **Search marks (phase 5, D157, `CommitGraphControl.Search.cs`).** The styled property `Search`
  (`GraphSearchMarks`: the matching SHAs and whether a search is active with a text) is bound to
  the repository's `SearchMarks`. While it is active, a matching row gets a 3-px bar of
  `VcSearchMatchBrush` at its left edge and its message in semibold; every other commit row has
  its node, labels and texts at 35 % opacity (`SearchDimmedOpacity`), its lanes at full strength,
  so the history's shape stays readable. The working-changes row is neither marked nor dimmed, and
  the selection and hover backgrounds are unchanged. `SearchStateOf(index)` is the tests' view of
  it. With no active search the control draws as before.
- **Around the rows (phase 5).** `RepositoryGraphView` is a grid of the search bar's slot
  (`SearchSlot`, holding `SearchBarView` while `Repository.Search` is set, which pushes the column
  headers and rows down by its 36), the column headers and the rows. While the graph reloads for a
  change of filter, `GraphReloading`, a 2-px indeterminate `ProgressBar` in `VcAccentBrush`, lies
  under the column headers across the whole width, over the scroll bar too, and takes no input. A
  search's match is scrolled into view without centring it (`ScrollIntoViewRequested` →
  `ScrollIntoView`); `RepositoryViewModel.Reveal(sha)` scrolls a row to the middle of the view
  without selecting it (a comparison's links).
- **Pull-request badges (phase 7a, D309, `CommitGraphControl.Badges.cs`).** The styled property
  `PullRequests` (the open pull requests by full ref name) is bound to the repository's
  `HostingState.ByRef`; a new map lays the rows out again only when a ref gains or loses a pull
  request or its number, title or draft changes (an equal map keeps the row cache, as `Search`
  does). A `CurrentBranch`, `LocalBranch` or `RemoteBranch` label whose ref the map holds carries,
  inside the label after its name, Lucide's "git-pull-request" icon (`IconPullRequest`, 10 px, 6
  after the name, `BadgeGap`) and, 3 further (`BadgeIconGap`), `#1` in the label's font and
  colours. Only a row's first label of each pull request gets one (local before remote), so a branch
  level with its upstream shows one badge and one ahead of it a badge on each row; a label folded
  into "+N" has none. `PlaceLabels` counts the badge in the label's width, so labels fold into "+N"
  sooner, and the first label's name is cut with "…" to make room, never the badge. The label's
  bounds include the badge: a click, right-click, drag or drop on it is one on its label.
  `PlacedLabel` gains a last member `Badge` (`PullRequestBadge(Number, Title, IsDraft)`); `Text` and
  `FullText` stay the branch's name, which the menus, the drag and the drop use. The labels' tooltip
  follows each label whose ref has a pull request with " (pull request #1: Add the login form)" or
  " (draft pull request #2: …)", also for labels in "+N". `BadgeInRow(index, label)`
  (`PlacedBadge`) gives the tests the places the drawing uses. With no map, or an empty one, every
  label is drawn as before.
- **Avatars (phase 7a, D252, D310, D322; `CommitGraphControl.Avatars.cs`).** The styled property
  `Avatars` (`AuthorAvatars`: the local images of the authors GitHub knows, by lower-case e-mail;
  `None` by default) is bound to `RepositoryViewModel.Avatars`. On a change the control decodes the
  files it has not decoded yet, together, on the thread pool (`Controls/AvatarBitmaps.DecodeAsync`:
  `Bitmap.DecodeToWidth(stream, 32)`, null for a missing or unreadable file, which is not logged;
  never in `Render`, D59), keeps those that decoded and, unless a newer change came meanwhile,
  clears the row and author caches, draws again and raises `AvatarsApplied` with the number of
  authors whose image decoded, whether the Author column shows or not (0 for a change to none). Only
  images that decoded are kept, for the control's life; a file that did not is tried again at the
  next change. With an image and the Author column shown, a row draws it 16 × 16, 8 right of the
  column's x and 5 below the row's top, clipped to a circle and scaled in high quality, and the name
  22 px further right in a layout 22 px narrower (the author cache is keyed by the name and whether
  an image is drawn); a row the search dims dims its image with its text. Rows without an image are
  laid out as before. `ShowsAvatarFor(email)` and `AuthorInRow(index)` (`PlacedAuthor`) are the
  tests' view. `RepositoryGraphView` hands `AvatarsApplied` to
  `RepositoryViewModel.NoteAvatarsShown`, which logs `{repo}: showing GitHub avatars for {n}
  authors` at Debug, the second of D322's two lines, after which pictures of an opened GitHub
  repository are taken (the first, the list's, is the hosting's); only the active tab writes it,
  since the window has one graph.
- **The authors told** (`RepositoryViewModel.Avatars.cs`; D311, D339). The tab tells its hosting
  which authors to look up (`Hosting.ShowAvatarsFor`; what the hosting asks GitHub and keeps is in
  "Hosting: a repository's pull requests and avatars") only in full mode and while the section is
  `Loading`, `Failed` or `Loaded` (signed in, with a GitHub remote, D257); only once its graph is
  whole or has 2,000 rows (a loading graph is followed through `CommitGraphData.Changed` until
  then); and only when the e-mails differ from those sent last, which a sign-out or the loss of the
  GitHub remote forgets. `AuthorsOnGitHub` makes the set in one pass over the first 2,000 rows: the
  tips of the GitHub remotes' remote-tracking branches start it, a reached row adds its parents, and
  each reached row's author e-mail, lower-case, is taken once, newest first, at most 200, with its
  newest reached commit and the repository of the first remote in `HostingRemoteOrder` that reaches
  it (`AuthorCommit`). The address of a commit that is only local, or only on another remote, is
  never sent. The e-mails are the graph's, after `.mailmap` (`%aE`). The partial's
  `OnPropertyChanged`, the class's only override, also hands `Avatars` to the details panel.

### Left panel and commit details

- `ViewModels/Panels/LeftPanelViewModel` turns `RepoRefs` into one flat list of rows
  (`RefSectionRow`, `RefItemRow`) that `Views/Shell/LeftPanelView` shows in a virtualised list
  (`RefList`): sections, remotes, folders from `/` in names (folders first, then items, each by
  name), ahead/behind counts, the filter, the branch HEAD is on in the accent colour. Which
  sections, remotes and folders are open is kept by key, so a refresh does not undo it. Clicking
  a ref raises `RefActivated` with its commit; the repository view model selects and reveals it.
  Since phase 6 a remote's row holds the remote branches its fetch lines fill (`GitRef.RemoteName`
  from the mapping, D222): `refs/remotes/mirror/main` that `origin` fetches is listed under
  `origin`, and no `mirror` remote row, whose Fetch or Remove would fail, is made.
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
  Since phase 5 that is for three commits or more: with exactly two the slot shows their
  comparison (`CompareViewModel`, `CompareView`, D162; see "Compare").
- Since phase 5 (D160) the details' 40-character SHA is drawn just small enough to stay on one
  line when it would wrap (`RightPanelView.FitShaOnOneLine`), and at the panel's font size
  otherwise.
- **"All files" (phase 5, D164).** `AllFilesButton`, a third `FileListToggle` beside Flat and
  Tree (class `selected` while on), shown for a commit and not for a stash. While on
  (`AppSettings.ShowAllFiles`, kept as the user left it) the list shows every file of the commit's
  tree (`ReadTreeFilesAsync`, read with the details): the changed files with their letters, its
  deletions at their place, the unchanged ones without a letter, in the Flat or Tree arrangement;
  the title reads "All files ({N})", N the rows listed. A click on an unchanged file opens its blame
  at that commit (`BlameRequested`, a page); every row's menu is `ForCommitFile`, with File history
  and Blame. The switch reads the commit asked for last again, so that a newer selection is never
  undone.
- **Hidden and soloed branches (phase 5, D159).** Every ref stays listed. A hidden branch's row
  draws its name in `VcTextDisabledBrush` and an eye button (`RefHiddenToggle`, `IconEyeOff`,
  "Hidden in the graph. Click to show it") before its counts; a soloed one a solo button
  (`RefSoloToggle`, `IconSolo`, `VcAccentBrush`, "Soloed. Click to stop soloing it"); in solo mode
  the rows of branches neither soloed nor HEAD's are drawn disabled; a remote hidden as a whole has
  the eye on its row. The toggles are `VcIconButton`s 18 by 18 marked `RefDrag.IsExcluded`, so a
  press on one never arms a branch drag and a double-click on one never checks out. A click on a
  hidden branch whose commit is no row of the graph selects nothing (`NothingActivated`: no commit,
  no working row, a diff closed, the details placeholder) and its tooltip says "{b} is hidden in the
  graph".
- **Pull requests (phase 7a).** With a GitHub remote the "Pull requests" section lists the open pull
  requests or says why it does not, its menu and its rows' menus are real, a click on a pull request
  selects its head commit and a double-click checks it out, and a branch row shows its open pull
  request's `#n` (`RefPullRequest`); see "Pull requests". Without one the section is phase 1's
  closed, empty header and no row changes.
- **The author's avatar (phase 7a, D310).** `CommitDetailsViewModel.Avatars` (set by the repository
  when its avatars change, `None` by default) gives `AuthorAvatarFile`, the local image of the shown
  commit's author e-mail, case ignored; null without one and while no commit is shown. In
  `RightPanelView` the author's value is a `DockPanel` of `DetailsAuthorAvatar`, a 16 × 16 `Ellipse`
  6 left of the text, collapsed at first, and `DetailsAuthor`, which fills the rest: collapsed, the
  ellipse takes no room, and the text keeps its width, place and wrapping. The code-behind follows
  `AuthorAvatarFile`: null hides the ellipse at once; a file is decoded off the UI thread
  (`AvatarBitmaps`, kept for the view's life once decoded) and, unless a newer file came meanwhile,
  fills the ellipse (`ImageBrush`, `UniformToFill`), which shows; a file that does not decode hides
  it. The ellipse is no control element to UI Automation (an `Ellipse`'s peer in Avalonia 12.1.3,
  design 18 item 1), so the real-window pass reads the screen for it.

### Working changes and writes (C4, D62, D67 to D71, D74 to D76)

- `ViewModels/Panels/WorkingChangesViewModel` is the stage panel (`Views/Shell/WorkingChangesView`):
  the unstaged and staged lists (two `ChangedFileList`s, flat or tree as D44 and D70 say), their
  titles, the summary and description with the counter of what is left of 72, the amend box
  (which fills empty boxes with HEAD's message), the commit button's label and state, git's
  message after a failed write (`ErrorText`, shown as `CommitError`), and the restore bar. It
  only shows what `Update(status)` gives it; every action goes to its `IWorkingChangesHost`.
  Since phase 6 a status read keeps every row that shows what it showed (`ChangedFileList.Show`
  with `keepFolders`, `ChangedFileRow.ShowsSameAs`, as the left panel does), so a read that changed
  nothing touches no row: a click on a row that a read reaches between its press and its release
  still counts. macOS's watcher delivers the index's change after a write's own refresh, and the
  read it started used to replace every row, which lost a click of phase 2's check 10 on CI
  (`StageListRefreshTests`). Since phase 6 the commit button's tooltip is "{its text} ({key})"
  (`CommitToolTip`; its reason `CommitDisabledReason`, which the palette's commit entry reads),
  Mod+Enter in the message boxes commits (see "Keyboard shortcuts"), and a ticked Amend keeps the
  HEAD it was ticked on, against which the commit's operation checks HEAD first (D235, see
  "Operations").
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
  commit whose message is found (a read that finds none, because git names the stop before it
  writes the message, is made again at the stop's next status; phase 7a's gate), when the summary
  and description are empty, they are filled with the operation's
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
- Since phase 5 `WriteAsync` takes the write's undo scope and records it as every operation of
  `RepositoryOperations.RunAsync` does (see "Undo and redo"): Commit and Amend, a discard of files,
  a hunk, lines or all, and Restore are recorded; stage, unstage and applying lines to the index
  are not (D168). When the write saved a snapshot, the clean-up follows the recording, which keeps
  what the entry needs. The restore bar is tied to its discard's entry (7.9, D172, the report's
  UD18): Undo of that discard closes the bar, Redo shows it again with the same snapshot, and its
  Restore runs as the entry's Undo (`TryRestoreAsUndoAsync`) while the discard is the entry before
  the cursor, Undo is enabled for it, no operation of the user runs or waits, and its files are as
  the discard left them; otherwise as in phase 2, D75's question included, recorded as an entry of
  its own. An Undo of a commit or an amend fills empty summary and description boxes with the
  undone commit's message; its Redo empties boxes that still hold it.
- Restore and every restore of files refuse (`RestoreSnapshotAsync` throws "Nothing was restored:
  …") where a folder holding files stands at a snapshot's file, or a file in one of its folders'
  place (`WorkingTreeFiles.IsInTheWayOfFile`): `git restore --worktree` would delete either without
  a word. The bar's hashes record such a path as in the way, so it counts as changed.
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
- Since phase 5 the file history page hosts a `DiffView` of its own (design 4.1): its
  `DiffViewModel` has `ShowsClose` false (no `CloseDiffButton`: it closes with its page), is reused
  for every row (`LoadAsync(target, sideLabel)`) and reads through a host that writes nothing
  (`PageDiffHost`); the view's `TakesFocusOnOpen` is false, so the focus stays in the page's list.
  A comparison's file opens in the main diff with the side label `{old short}..{to short}`.
  `CodeTextEditor.PointAt(line, column)` places a click in the blame's text and the resolver's
  result alike, and `CodeNumberMargin` is the one number column both draw (the result's with its
  line tints).
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

Since phase 5 a refresh compares the filter's key (`GraphFilter.Key`) with the fingerprint, so a
change of what the graph hides or solos loads the new graph off screen and swaps it in, as an
outside change does (8.2); every refs read tells the open page (`IGraphAreaPage.OnRefsRead`: the
reflog page reads again) and every status read the undo history (`NoteUndoState`: a pending entry
whose operation ended outside the app goes, and the toolbar reads its states again). A search's
marks and count stay over a swap (see "Search").

Since phase 6 (D207, amending D52 and D71) a repository that a dashboard row holds in summary mode
has no watcher: the dashboard's reads stand for it (see "The dashboard tab"), and its queue's
refresh after an operation reads its refs and status without a history. A change of `.git/config`
reaches a tab's refs through the git folder's watcher, and `ApplyRefs` applies them even with an
unchanged fingerprint, so the menus follow a changed fetch line.

Since phase 6 a harness can tell when nothing the watcher saw is still on its way to the screen.
`RepositoryWatcher.IsQuiet` is false from a burst's first change until its raise has ended: the
wait for the quiet period, the ignore check and `Changed`'s handlers (the burst counts as raised
before it leaves the wait, so no moment in between reads true). `IRepositoryWatcher.IsQuiet` is a
default member that returns true, for a watcher that holds no burst.
`RepositoryViewModel.IsWatcherQuiet` adds the `Changed` events posted to the UI thread whose
refresh has not been asked for yet, and a refresh or status read still running, whoever asked for
it. A change the system has not reported
yet is not known: macOS reports a write a while after it, so its refresh can still start later.
`ShellDriver.WatchersQuiet` and `WaitForWatchersQuietAsync()` read it over every open tab (see
"Tests").

Since phase 7a `IsWatcherQuiet` also waits for a URL-rewrites read the hosting started
(`IsHostingQuiet`, D281), which no refresh awaits, so a harness's quiet mark never straddles one
(see "Hosting: a repository's pull requests and avatars").

Since phase 7a a test switch holds every watcher's file-system events (R8, D262, D282):
`VISUALCOMMIT_WATCHER_LAG_MS`, whole milliseconds from 0 to 10,000; anything else (empty, a sign, a
space, a decimal point, more than 10,000) and 0 are no lag. `GitRepository.WatcherLag` reads it once
per process (`ParseWatcherLag`), with no harness parameter, so it reaches every harness and the real
app, and `CreateWatcher` passes it as `RepositoryWatcher`'s `eventLag`. Without a lag `Watch` wires
its handlers as before. With one, each `Created`, `Changed`, `Deleted`, `Renamed` and `Error` event
goes, with its due time on the system's clock (`Stopwatch`, never the watcher's `TimeProvider`),
into one unbounded single-reader channel, and one consumer on the thread pool, started by `Start`,
delivers them in arrival order once due. The pause check, the folder's existence and `EventCounted`
run at delivery, late, as on macOS: a write made inside `Pause()` whose pause ended before delivery
counts as an outside change. `IsQuiet` does not see held events. `Dispose` completes the channel and
the consumer drops what it holds; a delivery that throws is logged at Error, "A held event of the
repository watcher failed.", and the consumer goes on. The watcher counts held and delivered events
(internal, for its tests). `GitRepositoryProvider` logs once per session at Info, when the lag is
positive: `VISUALCOMMIT_WATCHER_LAG_MS=1500: every repository watcher holds each file-system event
for 1500 ms before it counts it (a test switch, D262).` Nothing in the app sets it; the lead runs
the lagged list with it (see "Tests"); CI runs without it (D262), and its macOS job meets the real
late watcher.

### Operation queue (D81, D107, D123, D125, D126, D149)

Contracts and the queue are in `src/VisualCommit.Core/Operations` (namespace
`VisualCommit.Core.Operations`). The queue only schedules: it runs delegates, pauses and
refreshes through delegates it is given, and never starts a process, so it is tested without git
or a UI (`OperationQueueTests`, `OperationQueueUiTests`).

| Type | Role |
|---|---|
| `GitOperation` | One operation: `Title` ("Push main", "Fetch origin": the log line, the status bar's "&lt;Title&gt; failed" and "&lt;Title&gt; cancelled", the finishing dialog), `RunningText` ("Fetching from origin…"), `Kind` (`Local` or `Network`), `Refresh` (`RefreshScope`: `None`, `Status`, `Refs`), `Run` (the delegate). Init properties: `DedupeKey`, `Errors` (`ErrorDisplay.Dialog`, or `StagePanel` for phase 2's writes), `IsBackground` (auto-fetch, the backups' clean-up at opening), `Prompts` (`PromptMode`; null: `Ask`, or `Deny` for a background operation), `HasSeveralSteps`, and since phase 4 `CanBeCancelled`: a local operation that only waits for another program (the merge tool, D149); since phase 6 `PromptGroup`, the bulk run it belongs to, which the queue passes to `IGitPromptBroker.Begin`; since phase 7a `CredentialBindings`, the URLs whose credentials come from the hosting account (empty by default), which it passes there too (see "Prompts and credentials") |
| `OperationContext` | What `Run` is given: `OperationId` (`op-<n>`, unique in the process; also the prompt scope's id), `CancellationToken` (`CancellationToken.None` for a local operation that cannot be cancelled, and after `EndCancelWindow`), `Environment` (the prompt scope's variables), `Progress` (at most one update per 100 ms reaches the queue's `Changed`), `EndCancelWindow()` (from now on Cancel is no longer offered; it throws `OperationCanceledException` when the user had already cancelled, so a pull never integrates after Cancel), `PromptCancelled`, and since phase 4 `SetRunningText(text)`: an operation of several steps names the step it is at ("Checking out {A}…", then "Rebasing {A} onto {B}…"); the progress of the step before is dropped, the title and log lines stay |
| `OperationReport` | What `Run` returns: `Done(message)`, `Warning(message)` (since phase 4: a success the status bar shows in the warning colour, logged `Succeeded`, such as a pull whose rebase left out commits the upstream once had), `Conflicts(message)` (a stop with conflicts is no failure, D95), `Failed(message, GitFailure)` (a failure the operation reports itself); since phase 7a `Offer`, what the status bar offers beside a success (`OperationOffer(Label, Run)`, D317; see "Toolbar, status bar, conflict banner and auto-fetch") |
| `OperationResult`, `OperationSnapshot`, `OperationFinishedEventArgs` | How an operation ended (outcome `Succeeded`, `Conflicts`, `Failed` or `Cancelled`, the status bar's message, `IsWarning`, the `GitFailure`, and `StaleLockFiles` after a cancel; since phase 7a `RefusedAccount`, the account whose token git refused through the app's helper in a failed operation's scope, D273, and `Offer`, a succeeded report's offer for the status bar, D317); what runs now (title, running text or the step's, kind, `IsBackground`, progress, `CanCancel`, `HasSeveralSteps`) |
| `IOperationQueue` / `OperationQueue` | `RunAsync(operation)`, `TryStartInBackground(operation)`, `WhenIdleAsync()`, `Current`, `Waiting`, since phase 5 `FirstWaitingTitle` (the toolbar's "Wait for {title} to finish"), `LastResult` (the last non-background result), `CancelCurrent()`, `DropWaiting()`, since phase 6 `Cancel(operation)` (below), the events `Changed` (any change, progress included) and `Finished` (every ended operation, background ones too), both raised on any thread. `OperationQueue(repositoryName, gitDirectory, pauseWatcher, refresh, prompts, operations, log, findLockFiles, recordClock)`; the queue keys its records by `WorktreeKey.Of(gitDirectory)` |
| `GitFailure`, `GitFailureKind`, `GitFailureClassifier`, `GitOutputCleaner` | A failure as the error dialog shows it (C11): `Kind`, `Summary`, `GitOutput`, `CommandText`, `ExitCode`. `GitOutputCleaner.Clean(stderr, stdout)`: standard error split at carriage returns and line feeds, without progress lines that carry a percentage, `Cloning into` and empty lines, line ends trimmed (a leading space stays: ` ! [rejected]`), credentials hidden; the standard output cleaned the same way when nothing is left; never shortened. `GitFailureClassifier.Classify(stderr, stdout, exitCode)` reads git's English text: `CheckoutBlockedByUntrackedFiles` (untracked files in the way, alone or with tracked ones), `CheckoutBlockedByLocalChanges` (tracked files only), `PushRejectedStale` (a push's ` ! [rejected]` line with `stale info` or `remote ref updated since checkout`), `PushRejected` (`fetch first`, `non-fast-forward`; a rejected line counts only with `failed to push some refs`, so a refused `git fetch .` is no rejected push), `AuthenticationFailed`, `NotFastForward`, `IndexLocked`, `Other`. Since phase 5 also `CheckoutBlockedByUntrackedFolder`, `MergeBlockedByLocalChanges` and `MergeBlockedByStagedChanges`, and `GitFailure.BlockedPaths`, the paths git named (see "Protected changes and the automatic stash") |
| `GitOperationScope` | The operation of the current asynchronous flow, which the runner records with each call |
| `IOperationLog` / `OperationLog`, `OperationRecord`, `OperationStart`, `RecordSequence` | The session's record of operations (T4): the last 500 in memory, each with its id, repository, title, start, duration, outcome and message; created in `AppSession` and handed to every queue through `TabServices`, and to the `GitCallLog`. Phase 5 (design 2.3, D173): a record also has `RepositoryKey` (its worktree), `Sequence` (taken from `RecordSequence` when it starts), `IsWarning`, `Kind`, `IsBackground`, `Errors`, `Notes` (the clean-up's "Removed backup …" lines), `FailureSummary` (the error dialog's, set after the record by `RepositoryOperations.ShowResultAsync` through `SetFailureSummary`), `DisplayedStart` and `DisplayedDuration` (by the record clock). Events `Started` (an `OperationStart`, so that the activity log shows it running), `Added`, `Updated` (a note or a summary) and `Evicted` (past the 500), raised on the thread that acts. A note or a summary for an operation without a record yet waits for it, at most 10 minutes. The activity log groups the call records under these by id |
| `LockFiles` | The lock-file scan after a cancel (below) |
| `IGitPromptBroker`, `PromptScope`, `IGitPromptHandler`, `GitPrompt`, `PromptAnswer`, `NullGitPromptBroker` | The prompt contracts (see "Prompts and credentials"); since phase 6 `Begin` takes the operation's prompt group and every `GitPrompt` of the scope carries it (`Group`); since phase 7a `Begin` takes the operation's bindings too (`NullGitPromptBroker` ignores them), `GitPrompt.Account` names the account of the binding that a user name or password prompt is for, and `PromptScope.RefusedAccount` the account git refused in the scope (D273) |
| `PromptGroup` | Phase 6 (D210, design 4.4): the prompts of one bulk run, one per run from its start to its end (`Id` `bulk-<n>`, `Ended`, `Dispose` ends it); since phase 7a `AnswersVersion` and `ForgetAnswers()`, after which no answer kept before is used (ruling 9) |
| `CredentialBinding`, `GitCredential`, `IGitCredentialSource`, `NullGitCredentialSource` | Phase 7a (D255): a URL bound to the hosting account, the credential git gets for it and where that comes from; see "Prompts and credentials" |

Each `RepositoryViewModel` creates its queue in its constructor, with its watcher's pause (since
phase 6 the view model's own `PauseWatcher`, design 3.3) and its `RefreshAsync` /
`RefreshStatusAsync` as the delegates, and disposes it with the tab. Since phase 6 a dashboard
row's summary-mode view model has a queue too, which a tab that opens the worktree adopts, and no
new queue is made on a worktree while another one there still runs an operation (see "Summary
mode, the registry and adoption"). Its rules:

- **The UI thread's context.** `RunAsync` is called on the UI thread; the queue captures the
  caller's `SynchronizationContext` and runs the operation and its refresh on it, never through
  `Task.Run` (phase 2's delegates change bound view-model state). An operation starts inline when
  the caller is on that context, and the next one in line is posted. Every git call still leaves
  the UI thread inside `GitRunner` (D59). Unlike the rest of Core, the queue's awaits keep their
  context on purpose.
- **One at a time, first in first out.** An operation runs when it is first in line and nothing
  runs. Its steps: a prompt scope is opened (mode `Prompts`, or `Ask`, or `Deny` in the background;
  none for `None`) with the operation's id, prompt group and bindings; `GitOperationScope` and the
  watcher's pause are entered
  around `Run`; the outcome is the report, or `Cancelled` for an `OperationCanceledException`,
  `Failed` with a `GitFailure` for a `GitException` (the summary is "&lt;Title&gt; failed." until the
  caller replaces it), `Failed` with the message for an `IOException` or
  `UnauthorizedAccessException`, and "Unexpected error: …" (logged at Error) for anything else; the
  queue never dies with its operation. A failure after the user cancelled a prompt of the scope is
  `Cancelled`; since phase 7a a failure whose scope recorded git's refusal of the account's token
  carries that account (`RefusedAccount`). Then the scope closes, the stale locks are looked for
  (below), the `Refresh` runs
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
- **Cancel of one operation (phase 6, D210, design 4.5).** `Cancel(operation)` acts on that
  operation alone, whatever else the queue holds, where `CancelCurrent` and `DropWaiting` would also
  stop a tab's own push: a waiting one is removed and completes `Cancelled` without running, and so
  does the next in line whose start was posted but has not run; unlike `DropWaiting`, its result
  becomes the last result and raises `Finished`, so its tab's status bar says "{Title} cancelled". A
  running one is cancelled as `CancelCurrent` does while it can be, and otherwise runs to its end.
  An operation that D123's dedupe merged into a waiting entry is kept with it (`Entry.Merged`) and
  stands for it, so both callers get the cancelled result. A background operation is left alone.
  `RunOptions.Cancellation` calls it for a bulk run's Cancel all and the settings screen's Cancel.
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
  do not run. A background auto-fetch never holds the window. Since phase 6 the close also waits
  for the dashboards' summary-mode view models and those released while running (design 3.7), each
  dashboard's bulk run first starts no more rows, and the window waits for an open settings
  screen's global git writes, which no queue holds ("Saving the settings — all repositories", with
  the remaining-steps line when more than one key is written; the screen starts no further step,
  D234).
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
  Since phase 5 the queue tells the log when an operation starts (`IOperationLog.Start`), with its
  sequence number and its record clock's time; its own start time (the lock-file scan's, D126) and
  the log file's `after <N> ms` stay the system clock's.

### Operations (`RepositoryOperations`, D113, D124, D135 to D147)

`ViewModels/Operations/RepositoryOperations` builds and starts every operation of one repository:
it asks the confirmations, opens the dialogs, saves the backups inside the operation, runs it
through the queue with its texts, and shows the result. One per `RepositoryViewModel`
(`Operations`). It implements `IRepositoryActions` (every public operation; the menus and the
double-clicks call it through `RepositoryViewModel.MenuActions`), `IToolbarActions` (the
toolbar's six buttons), `IRemoteActions` (the Remotes dialog), `IInProgressActions` (the
banner's Abort, Continue and Skip) and, since phase 4, `IConflictActions` (the conflict
resolver's reads and writes). Phase 5 adds to `IRepositoryActions` the pages (`ShowFileHistoryAsync`,
`ShowBlameAsync`, `ShowReflogAsync`, `ShowBackupsAsync`), `ShowInGraphAsync`, `CompareBranchesAsync`,
hide and solo, `RestoreBackupAsync`, the actions on a kept commit (`CreateBranchAtKeptCommitAsync`,
`CheckoutKeptCommitAsync`, `ResetToKeptCommitAsync`, `CherryPickKeptCommitAsync`) and `CopyTextAsync`
(the activity log's copies), and to `IToolbarActions` Undo and Redo. Phase 6 adds to
`IRepositoryActions` `OpenInEditorAsync` (the file menus' "Open in editor", see "Open in editor and
Open file"), and phase 7a the pull requests' actions (`CreatePullRequestAsync`,
`OpenPullRequestAsync`, `CheckOutPullRequestAsync`, `CopyPullRequestLinkAsync`,
`RefreshPullRequestsAsync`, `OpenPullRequestsInBrowserAsync`) and `SignInToGitHubAsync` (see "Pull
requests"). Tests give recording fakes for these interfaces.

| File | What it holds |
|---|---|
| `RepositoryOperations.cs` | The constructor and the interfaces' mappings |
| `RepositoryOperations.Run.cs` | The helpers every operation shares (below) |
| `RepositoryOperations.Branches.cs` | Checkouts (local, a commit or tag detached), "Stash changes and check out", "Check out and merge" (the drop), create, rename, delete, fast-forward, merge, reset, cherry-pick and revert (one, or several as one git call with the list), abort (with the return to the branch a "Check out and rebase" left) |
| `RepositoryOperations.RemoteBranches.cs` | Phase 6, moved out of `.Branches.cs`: the checkout of a remote branch into its local branch (`CheckoutRemoteBranchAsync`, its local name by the fetch lines, D201's creation without an upstream), the refusal before git of one two remotes fetch or none, the branch a checkout may create for the undo history |
| `RepositoryOperations.FetchPull.cs` | Phase 6, moved out of `.Remotes.cs`: the toolbar's Fetch of every remote, Pull and Pull with rebase, their bulk entry points `FetchAllAsync(BulkPart)` and `PullAsync(BulkPart)`, Pull's settings read before the fetch (D219, D231), `pull.ff=only`'s own refusal (D211) and the pull's ignored files |
| `RepositoryOperations.Remotes.cs` | Fetch of one remote, push (to the upstream, through the push dialog, onto a dropped remote branch), force push, set and stop tracking an upstream, delete a remote branch, the Remotes dialog and its add, rename, change URL and remove, push a tag, delete a tag on a remote; since phase 6 the push guard of the fetch lines (D222) |
| `RepositoryOperations.Settings.cs` | Phase 6: the settings screen's writes of "This repository" (`SaveRepositorySettingsAsync`) |
| `RepositoryOperations.StashTags.cs` | Stash (the stash dialog), apply, pop, pop the newest, drop, create a tag (the tag dialog), delete a tag |
| `RepositoryOperations.Files.cs` | The file menus' actions: stage, unstage and discard (through phase 2's writes), ignore a file or its extension, open a file, show in folder, copy a SHA, SHAs, a path, a ref name or a remote's URL |
| `RepositoryOperations.Rebase.cs` | Phase 4: rebase onto (`RebaseAsync(RebaseTarget)`), "Check out and rebase" (`CheckoutAndRebaseAsync`), opening the interactive list (`OpenInteractiveRebaseAsync`) and its Start (`RunInteractiveRebaseAsync`), squash (`SquashAsync`), Continue and Skip (`IInProgressActions`), the return file, the selection after a rebase |
| `RepositoryOperations.Conflicts.cs` | Phase 4: "Resolve conflict" and "Open in merge tool" from the file menu, `IConflictActions` (mark resolved with the "changed on disk" check, take a side, delete, the merge tool, the sides' names), the questions about conflict markers (staging, Continue), the paths that were conflicted at the stop |
| `RepositoryOperations.Undo.cs` | Phase 5: the undo history's recording around every recorded operation, the toolbar's Undo and Redo as one operation each with their states and questions, what each status read does to the history, the restore bar's tie, the backups the histories keep from the clean-up, the backups list's restores (see "Undo and redo") |
| `RepositoryOperations.Protect.cs` | Phase 5: C12, the automatic stash's checkout, the refusals before git, the refusal message of every checkout path and of the merges, picks and reverts git refuses for changes in the way, the check before a stash's apply or pop (see "Protected changes"); since phase 6 Keep ignored files' setting (`KeepIgnored`), every checkout's refusal display (`CheckoutRefusalDisplayAsync`) and the app's own check before git (`RefuseIfIgnoredInTheWayAsync`, see "The ignored-files guard") |
| `RepositoryOperations.Pages.cs`, `.Filters.cs` | Phase 5: opening the pages and "Show in graph" (none is an operation: a page only reads); a comparison of branches, hide and solo (a filter reloads the graph through the refresh, no operation either) |
| `RepositoryOperations.PullRequests.cs`, `.CreatePullRequest.cs` | Phase 7a: a pull request's "Open in browser" and "Copy link" (neither an operation), "Refresh pull requests" (outside the queue), its checkout (`PullRequestCheckout`'s plan; a new branch through the operation "Fetch pull request #n"), the section's "Open pull requests in browser" and "Sign in to GitHub…"; a new pull request: `CreatePullRequestAsync` and the form's side, the nested `PullRequestForm` (`ICreatePullRequestActions`: the head's place, the bases, the push and the POST as one network operation); see "Pull requests" |
| `DeferredDialog.cs` | Phase 6: `DeferredDialog`, a result's dialog a bulk run's row keeps for its Details…, and `BulkPart`, a row's part in a run (its cancellation, prompt group and deferral) |
| `OperationTexts.cs` and `.Branches.cs`, `.Menus.cs`, `.Remotes.cs`, `.StashTags.cs`, `.Rebase.cs`, `.Conflicts.cs`, phase 5's `.Undo.cs`, `.Protect.cs`, `.Menus.ActivityLog.cs`, `.Menus.Pages.cs`, `.Menus.Filters.cs`, and phase 6's `.Bulk.cs`, `.Menus.Refspecs.cs` (R1 to R4), `.Settings.cs`, and phase 7a's `.Hosting.Accounts.cs`, `.Hosting.Clone.cs`, `.Hosting.Credentials.cs`, `.Hosting.PullRequests.cs` and `.Hosting.Create.cs` (the texts several parts share are in `ViewModels/Hosting/HostingTexts.cs`) | Every text: an `OperationText(Title, RunningText, ErrorTitle, FailureSummary)` per operation (design 2.33, phase 4 design section 10), the success, conflict and refusal messages, the confirmations, the error dialogs' own titles and summaries, the menus' labels and disabled reasons. Counts use the singular for one ("1 file has conflicts", D113) |

The helpers of `.Run.cs`:

- `RunAsync(text, kind, refresh, run, options)` queues a `GitOperation` with the text's title and
  running text, then shows what is left to show: a failure in the error dialog (with
  `RunOptions.DescribeFailure`'s title, summary and extra button, or else the text's `ErrorTitle`
  and `FailureSummary`, unless the operation reported its own summary), or the lock files of a
  cancel. A success shows in the status bar only; a conflict in the banner. After a deduplicated
  second call only the first caller shows the result. After the tab closed it starts nothing.
  `RunOptions`: `DedupeKey`, `HasSeveralSteps`, `Prompts`, `ShowsErrorDialog` (false for the
  Remotes form, which shows git's message in `RemoteError`), `DescribeFailure`, since phase 4
  `CanBeCancelled` (the merge tool), since phase 5 `Undo`, the operation's `UndoScope`
  (`UndoScope.None` by default: not recorded), and since phase 6 `Cancellation` (registered to call
  `Queue.Cancel(operation)` until the operation returns; a token already cancelled queues nothing
  and gives "{Title} cancelled"), `PromptGroup` (copied into the operation) and `Defer` (a bulk run's
  row: no dialog opens, `ShowResultAsync` hands what it would show to the row as a `DeferredDialog`
  with the lock files' stamps taken then, and `ShowDeferredAsync` opens the identical dialog later;
  D195, D211). Inside the operation, around `run`, the undo
  history's recording reads what the scope names before the work (`BeginRecordingAsync`), an
  `UndoCapture` collects what the reads cannot see while it runs, and the recording reads again
  and adds the entry after it, also when the work failed half-way (`EndRecordingAsync`; neither
  ever fails the operation: a failure is logged and nothing is recorded). An operation that saved a
  backup (`SaveBackupAsync` marks it, and since phase 5 hands the refs to the capture) ends with
  the backups' clean-up inside it, after its work and after the recording, so that it keeps what
  the new entry needs, whether the work succeeded or not (`CleanUpBackupsAsync`, D139): phase 3's
  resets, deletions, force pushes, drops and aborts too. Each backup it removes is a note of the
  operation (`IOperationLog.Note`), which the activity log shows.
- Since phase 7a (D255, D329, D332, design 4.7) `RunAsync` gives an `OperationKind.Network`
  operation its `CredentialBindings`: the tab's `Repository.Hosting.Bindings` as they are when it
  is queued, or, for a summary-mode view model (a dashboard row without a tab, whose hosting is the
  null object) while `IHostingSession.BindsGit`, `BindingsFor(Refs.RemoteUrls, rules)` after one
  `ReadUrlRewritesAsync` made before it queues (cancelled by `RunOptions.Cancellation` alone; a
  failed read binds nothing and logs a Warning; the closed and cancelled checks are made again
  after it). A local operation gets none: its git calls carry no credential pair and keep an
  inherited `GIT_CONFIG_PARAMETERS`, and a hook or a git-lfs filter it runs asks the user's helpers
  in D82's order, as before phase 7a. A failed result that carries `RefusedAccount` shows, in
  `ShowResultAsync` and in a bulk row's `DeferredOf`, the refused credentials' dialog before
  `DescribeFailure` (`RefusedDisplay`; see "Prompts and credentials").
- `ReadHeadAtStartAsync()` (phase 5's review): inside a recorded operation, HEAD as the
  recording read it just before the work, otherwise read now; the operation's own checks of HEAD
  made before anything can move it share that read (D124's "HEAD moved while this waited", C12's
  choice made again, the detached commits' check, the commit a reset or a rebase starts from). A
  pull's check after its fetch reads HEAD again.
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
  `CheckoutBlockedByLocalChanges` only (not after a drop's "Check out and merge", "Create branch"
  or an automatic stash's refusal; since phase 5 under C12's "Checkout blocked", D153); "Pull" after a
  rejected push of the current branch to its upstream, "Fetch &lt;r&gt;" after any other rejected
  push; "Remove lock files" (`RemoveLockFilesAsync`, which deletes a listed file only when its time
  and length are unchanged since it was listed, D126; a file that cannot be deleted opens
  "Removing lock files failed"). The lock-file dialog lists the files relative to the working
  tree with `/` and shows the operation's last git call as its command.

Kinds and prompts: fetch, pull (local when its upstream is a local branch), push, force push,
deleting a remote branch, pushing and deleting a tag on a remote are network operations; every
other operation is local; the merge tool is a local one that can be cancelled. Since phase 5 an
undo or redo step is a network operation when it changes a remote (a `RemoteRef` effect), else
local, and so is a restore of the backups list (a push back is network). Phase 3's, phase 4's
and phase 5's operations ask for prompts (`Ask`); phase 2's writes, `FastForwardBranchAsync`,
every read, the watcher's `check-ignore` and the background clean-up get none.

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
refuses. A refusal is a failure without git output. Since phase 5: where only whether an
operation is in progress or files conflict matters (the in-progress refusals of a merge, a pick
and a revert, C12's checkout refusals, a rebase's), the operation reads the git folder
(`ReadInProgressStateAsync`) and `ls-files --unmerged -z` (`ReadConflictedPathsAsync`) instead of
a whole status; every checkout makes C12's choice again inside its operation (see "Protected
changes"); and the reflog page's and the backups list's "Create branch here…", "Check out this
commit", "Reset {cur} to this commit…" and "Cherry-pick onto {cur}" check first that the commit
still exists (`CommitExistsAsync`, before the reset's dialog and the checkout's choice too): "The
commit {short} no longer exists.", nothing changed. The graph's menus make no such call.

Notable operations: "Stash changes and check out" (D91, several steps) stashes the tracked
changes only (`--message "VisualCommit: before checking out <target>"`), checks out, and pops with
`--index` (a plain pop when git refuses the index part); a failed checkout pops the stash back at
once; a pop that conflicts ends `Conflicts` and opens "Changes re-applied with conflicts" (the one
conflict that opens a dialog, without git's output); a pop that fails otherwise opens "Changes
not re-applied"; every outcome names the stash. Several commits are cherry-picked oldest first
and reverted newest first with one git call that takes the whole list (D136, D145), so that git's
sequencer keeps what is left for the banner's Continue, Skip and Abort; a stop says "Cherry-picked
{K} of {N} commits; stopped at {short} with conflicts in {M} files" (or "…; {short} changes
nothing", "…; git could not pick {short}"), in the warning colour. A list git refused at its
start (the git layer has cleared it with `--quit`, which keeps the user's changes) says why in
C12's words since phase 5: "Your staged changes prevent the cherry-pick. Commit, stash, or unstage
them first." or "{N} conflicts prevent the cherry-pick. Commit, stash, or discard your changes
first." (the revert's alike); phase 4's "Git would overwrite your changes: commit or stash them
first. Nothing was cherry-picked." (or "reverted") stays for a refusal that names no conflict and
no staged change. A multi-revert with a merge commit is disabled. A drop's "Check out and merge"
checks out and merges in one operation. The toolbar's Pop, clicked again while the pop of the same
stash has not ended, gets that pop's task instead of popping the next stash. "Open file" asks a
plain confirmation first for a file that can run programs (`CanRunPrograms`: a fixed list of
extensions on every platform, or an executable bit, D130). "Show in folder" starts `explorer.exe`,
`open`, `dbus-send` (with `xdg-open` as the fallback) by absolute path (`FileLauncher`), never by
bare name.

Phase 6's changes to these operations:

- **Fetch refspecs** (D201, D222, design 10.4 to 10.6). A remote branch checks out into its
  branch's name on its remote (`RepoRefs.LocalNameOf`: `origin/up/main` from `main` into `main`); a
  mapped ref that is no branch keeps phase 5's name (`origin/pr/1` into `pr/1`); a ref one remote
  fetches from two refs (R2) checks out the local branch of that name, or creates it without an
  upstream (`git switch --create <n> --no-track <ref>`), and says so ("Created pr/1 at origin/pr/1
  without an upstream: origin/pr/1 is fetched from more than one ref of origin"); a ref two remotes
  fetch, or none (R1, R3), is refused before git when a stale menu reaches it. A remote branch the
  refs no longer hold (a menu built before a fetch removed it) is named by the fetch lines too
  (`RepoRefs.LocalNameOf(string)`, `SourceOf(string)`: `fork/up/feature` gives `feature`). "Create
  branch here…" from an R1 or R2 ref passes `--no-track`. The drop's two pushes and "Delete {b} on {r}…"
  act on the branch's name on its remote (`git push <r> refs/heads/<A>:refs/heads/<y>`, the lease
  and the backup `remote-branch-<t>/<r>/<y>` by that name; the renamed drop push is titled "Push {A}
  to {T}"; `SplitRemoteBranch` gives the ref's `RemoteSource`). The push guard
  (`RefuseIfPushStopsFetchAsync`, before git inside Push, the push dialog's push, the drop's
  pushes, Force push, the backups list's push backs of a branch (`PushBackAsync`, also from
  `PushForcePushBackAsync`), and undo's and redo's steps that write a branch on a remote, `RefuseIfStepStopsFetchAsync`) reads
  the fetch lines inside the operation (`IGitRepository.ReadFetchRefspecsAsync`, one more `git
  config --null --get-regexp ^remote\.` per such push, so a line added after the snapshot counts)
  and asks the push's remote alone (`FetchRefspecs.SourceOf(trackingRef, remote)`, so another
  remote's mirror line does not hide this one's two sources): a push whose destination would give
  a tracking ref a second source on that remote, so that every later fetch of it fails, is refused
  with "Pushing {A} to {target} would stop every fetch of {r}: …", unless one of its tracking refs is
  already filled by a branch the remote has. Since phase 7a (ruling 8, D279) the guard asks
  `FetchRefspecs.SourceOf(trackingRef, remote, sourceCanExist)` with
  `IHostingSession.SourceCanExist(url, source)` for the remote's first configured `url` (before
  `insteadOf`; a remote without one keeps phase 6's question): on a remote recognised as GitHub's,
  which needs no account and makes no request, `refs/pull/<x>/head` and `/merge` count as sources
  only when `<x>` is all digits (`GitHubUrls.SourceCanExist`), so `pr/fix-typo` pushed beside
  `+refs/pull/*/head:refs/remotes/origin/pr/*` is no longer refused, while `pr/7` still is; no git
  call is added (`PushGuardGitHubTests`). Push and Force push to an upstream that is no branch
  on its remote (`UpstreamRemoteRef` outside `refs/heads/`) are disabled with R4
  (`ToolbarViewModel.UpstreamNotABranchReason`, in the toolbar and the branch's menu) and refused
  before git with R4 as a sentence (`PushToNotABranch`). A remote branch's or tag's backup is pushed
  back to the one configured remote its name starts with (`RemoteBranchNames.RemotesOf`); when more
  than one fits, the item is disabled and the push back refused with `RemoteOfBackupAmbiguous` ("The
  remote this backup came from cannot be told: team/fork/main fits team/fork and team"). A force
  push's backup restored when no local branch tracks it finds its remote and branch by the fetch
  lines, else is refused with "The remote this backup came from cannot be found". The resolver's
  remote branch name (`MergedRemoteBranch`) matches by the mapping. An upstream is named by git's `%(upstream)`
  without `refs/remotes/` (`ToolbarViewModel.UpstreamText`, design 10.4's GD15), else
  `{remote}/{remote ref}`, so `pr/7` tracking `refs/pull/7/head` on `upstream` reads
  `upstream/pr/7` in every text that names an upstream.
- **Pull** reads its settings inside its operation before its fetch (D219, D231; see "Phase 3's
  repository members") and refuses there with Pull's text for a value it does not understand,
  nothing fetched; a `git merge --ff-only` that `pull.ff=only` asked for and that git refused
  because the branches diverged says "{b} and {up} have diverged, and pull.ff is set to only: merge
  {up} into {b} or rebase {b} onto {up}." (D211; `PullRefusalDisplay`).
- **Amend** (D235): ticking "Amend previous commit" keeps the HEAD it was ticked on, and the
  commit's operation reads HEAD first and refuses in D124's words when it has moved since ("{b}
  moved while this waited: it was at {old} and is at {new} now. Nothing was amended."; "Commit
  failed" in the status bar, the reason in the stage panel), rather than amend another commit with
  this one's message.
- **Keep ignored files** is read inside every covered operation when it runs (D124), see "The
  ignored-files guard".

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
  "Waiting for {tool} to close…", `CanBeCancelled`: the status bar's Cancel, whose tooltip is
  "Stop git and the merge tool" since phase 5 (D160), stops git and the tool): "Resolved {file}
  in {tool}" or "{tool} closed without resolving {file}". See "The graph area: the list, the
  resolver and the pages".

### Backup refs (D67, D83, D127, D139)

Every backup is a ref under `refs/visualcommit/backup/` (`GitRepository.BackupRefPrefix`). They are
outside `--branches --remotes --tags` (D47), so neither the graph nor the left panel shows them,
and they keep their commits from git's garbage collection until the clean-up removes them (below,
since phase 4); phase 5's undo history and the backups list read what is left. Since phase 5 the
backups' clock (`BackupClock`) names them; the harnesses fix it (see "Tests").

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
| `stash-<time>` | the stash commit (its index and untracked parents with it; `git stash store` brings it back) | dropping a stash; since phase 5 popping one too, so that the popped stash outlives git's pruning for its undo (D171) |
| `tag-undone-<time>/<t>` | the tag's object (phase 5, D171) | the undo of a tag's creation, which deletes the tag; its redo creates it again from this object |
| `abort-<time>` and `abort-<time>-changes` | HEAD's commit (so commits a terminal's sequence made stay reachable); `SnapshotTrackedChangesAsync()`, every changed tracked file ("VisualCommit: snapshot before an abort") | aborting a merge, cherry-pick, revert, rebase or `git am` |
| `rebase-<time>/<b>` | the branch's old tip, read inside the operation (phase 4, D142) | every rebase the app starts: rebase onto, "Check out and rebase", an interactive rebase, a squash, a pull that rebases (configured or chosen, through `IntegratePullAsync`'s callback). Saved before git runs, so a rebase git refuses or finds up to date leaves one too |
| `skip-<time>` and `skip-<time>-changes` | HEAD's commit; `SnapshotTrackedChangesAsync()` (phase 4, D144) | Skip of a stopped commit (its resolution is lost) |
| `discard-<time>` from the resolver | D67's snapshot, saved by `TakeSideAsync` when a binary or link file differs from the side written over it, and by `RemoveConflictedAsync` when the file matches none of its stages (phase 4, D148) | "Use ours" or "Use theirs" on a binary file, "Delete it". The stage panel's restore bar does not offer these |

What phase 5 reads of them (`ReadBackupsAsync`, the backups list, and the undo's restores): the
prefix and the name scheme above (kind, time, optional retry number, optional name; `-changes`
pairs with the ref of the same kind, time and number); that a snapshot's first parent is HEAD at
the time and, for a hard reset, its second parent the index; that the snapshot's paths are in its
message; that a remote branch's or tag's backup was also the lease of the deletion, so the remote
held exactly that object when it was deleted; and that the clean-up removes them, so the backups
list offers only what is left (D137). `BackupRefTests` cover the batches, the retry naming, case
clashes and every case of the hard reset's snapshot; `BackupReaderTests` the reader;
`TempRepo.BackupRefsAsync()` lists the refs for tests.

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
  `keep` never goes: the app passes `RepositoryViewModel.BackupsToKeep`, the restore bar's
  snapshot and, since phase 5, every backup the undo histories of the repository's worktrees need
  (`UndoHistories.BackupsOf`, D171). They still count toward the 50: a long session keeps more, on
  purpose.
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
- **What it says**: nothing in the status bar (the owner's answer). The app log gets one Info line
  per backup removed, `<repo>: Removed backup <kind>-<time>[-<n>] (older than 30 days)` or `(more
  than 50 backups)`. Since phase 5 each removal is also a note of the operation it ran in, with the
  same words, which the activity log shows under that operation, and the background operation's
  message is `Removed {N} backups` (`Removed 1 backup`).
- `BackupCleanupTests` (Git, with a fixed clock) and `RepositoryOperationsBackupCleanupTests` (App:
  at opening, after a backup, a failing clean-up shows nothing).

### Undo and redo (O2, D154, D155, D163, D165 to D172)

Phase 5. Three parts: the history and the sequences in Core (`src/VisualCommit.Core/Undo`, pure,
tested without git), the reads and the steps in the git layer (`GitRepository.Undo.cs`), and the
recording, the toolbar, the questions and the restores in the app
(`RepositoryOperations.Undo.cs`). Design section 7 and its 7.12 and 15.9 say what each part
settled; `docs/test-reports/phase-5.md`'s UD1 to UD20 bind them (D183).

**The history (Core).**

| Type | Role |
|---|---|
| `UndoHistory` | One worktree's history for the app's session: the entries and a cursor between them. Undo applies the entry before the cursor backwards and moves the cursor back (`Undone`); Redo applies the entry after it forwards (`Redone`); a new recorded action drops every entry after the cursor, then is added (`Add`). At most 100 entries (`Capacity`), the oldest dropped. At most one entry is `Pending`, always the last: an action that stopped with the banner showing (a merge, a pick, a revert, a rebase); Continue, Skip or the stage panel's Commit that end it complete it (`Complete`), Abort removes it (`RemovePending`, D165), and so does a status read that finds the operation ended outside the app. `Backups`: the backup refs every entry needs. `TakeTurnAsync`: two tabs on one worktree share the history but not a queue, so a step holds the history's turn from its check of the entry at the cursor to its cursor move, and a recording around its writes; the other tab's entry then lands after the move. Nothing is written to disk |
| `UndoHistories` | The session's histories (in `TabServices`), keyed by `WorktreeKey`: two tabs on two worktrees of one repository never share a history, two tabs on one worktree share one, and a tab closed and opened again in the session keeps it. `BackupsOf(commonDirectory)`: the union of the backups the histories of a repository's worktrees need (they share the backup refs) |
| `UndoEntry`, `UndoKind`, `UndoEntryState` | An entry: its kind (one of 40: commit and amend, the three checkout kinds, the branch, merge, reset, pick, revert, rebase, pull, stash, tag, discard, restore, ignore, upstream, remote and push kinds; the backups list's restores are recorded as the kind whose sequences undo them, with two kinds of their own, `RestoreStash` and `RestoreStagedChanges`), its text (`UndoTexts`, 7.6: `commit "Fix the parser"`, `checkout of main`, `pop of stash "Save my work"`, never `stash@{n}`), the operation it was recorded in, its `Before` and `After` states, what was captured, its scope and its state |
| `UndoScope`, `ConfigScope` | What a recorded action's operation declares (`RunOptions.Undo`): the kind and text, refs by exact full name or by prefix (whole path components: `refs/remotes/origin/` is not `origin2`'s), the stash list, configuration keys, files and their index entries, a stash's files, the whole status, the `.gitignore`. A factory per recorded kind (`UndoScope.Commit`, `.Checkout`, `.AutoStashCheckout`, …); `None` (not recorded: stage, unstage, applying lines, fetch, the clean-up, the resolver, undo and redo themselves, D168), `Continuation` (Continue and Skip complete the pending entry) and `Abort` (it removes it). The scope of a plain rebase and of "Check out and rebase" holds every `refs/heads/` ref, so branches a user's `rebase.updateRefs` moves are part of its entry. `KeepsFileBytes`, `BeforeAction()`, `AfterAction(changedFiles)`: see the recording below |
| `UndoState`, `RecordedFile`, `UndoStash`, `ConfigEntry`, `GitignoreState` | What is read before and after an action: HEAD (branch and commit), the scope's refs with their objects, its symbolic refs (`%(symref)`: a remote's `refs/remotes/<r>/HEAD`), the worktree each branch is checked out in (`%(worktreepath)`), the stash list, the configuration keys in `config --local --list` order, the files (path, SHA-256, a blob when its bytes were kept; `RecordedFile.InTheWay` for a path where something other than a file stands), their index entries as `ls-files --stage -z` prints them, the whole status (`status --porcelain=v2 -z --untracked-files=all`) for the stash kinds, the `.gitignore` (existence, length, SHA-256) |
| `UndoCapture`, `UndoCaptured` | What the reads cannot see, collected while the action runs (an `AsyncLocal`, thread-safe, so the git layer's pool threads report too): a reset's mode, its snapshot and index commit, the stash a stash made, a push's `PushOutcome`, the snapshot a discard saved or a restore put back, an index a backups-list restore put back, the files a hard reset changed, every backup ref the action saved. The operations report through static methods that do nothing outside a capture |
| `UndoEffect` | The closed set of effects (7.3, 7.12), each with the values its check compares: `MoveHead(branch, from, to, Soft or Keep)` (a null `To` deletes the branch: the initial commit's undo), `MoveDetachedHead`, `SetRef` (with `CreateReflog` for a branch brought back), `SymbolicRef`, `Switch`, `RenameBranch`, `BranchConfig`, `RemoteConfig` (removing a remote also deletes, in the step's transaction, the refs a fetch made under `refs/remotes/<r>/`), `RemoteRename`, `RemoteUrl`, `IndexTo` (optionally for some paths only), `FilesTo`, `RestoreSnapshot`, `StashPush`, `StashPop` (with `Onto`: a stash is popped back only onto the commit it was made on), `StashStore`, `StashDrop`, `IndexEntries`, `TagRef`, `GitignoreBytes`, `RemoteRef`, `KeepTag` (the `tag-undone-…` backup) and `HardReset` (a hard reset's redo, with a new snapshot) |
| `UndoSequence`, `UndoStep` | `UndoSequence.For(entry, direction)`: each kind's fixed sequence of effects, never a generic reverse (D167): a commit, an amend or a soft reset `MoveHead(B, N, O, Soft)`; a mixed reset that, then `IndexTo` its index snapshot (or `O`); a hard reset `MoveHead(B, N, O, Keep)`, `IndexTo(<snapshot>^2, O)`, `RestoreSnapshot` in that order, its redo `HardReset`; a merge, pick, revert, rebase of any kind, squash or pull `MoveHead(B, N, O, Keep)`, a plain rebase also `SetRef` back for every other branch git moved; a checkout `Switch` (with `SetRef` and `BranchConfig` for a tracking branch it created); the automatic stash's checkout `Switch(target, branch)` then `StashPop` (D166); a stash `StashPop`; a pop `StashStore` first, then `IndexEntries` and `FilesTo`; a drop `StashStore`; a tag's creation `KeepTag` then `TagRef` delete; a discard `RestoreSnapshot`, its redo `FilesTo` the bytes after the discard (a hunk's discard keeps the file's other changes); every push kind `RemoteRef` first, then `BranchConfig`; redo the mirror unless said. `UndoStep(entry, direction, effects)`, `ChangesRemote` (a `RemoteRef`: a network operation that asks first), `Message` (a backups-list restore's own reflog message) |
| `UndoChecks`, `UndoRefusal`, `UndoRefusalKind` | `ScopeOf(step)`: what a step's operation reads (the entry's scope and what its effects check). `FirstRefusalInMemory(step, refs, status, worktrees)`: the checks that need no git (HEAD, tips, tags, stashes, staged files, worktrees' branches, remotes), made after every refresh for the toolbar. `FirstRefusal(step, now)`: the simulation of the whole step inside its operation, each effect's check against the state the effects before it leave (HEAD after a `Switch`, the status after a `StashPush`). A refusal names its kind (in progress, conflicts, HEAD not on the branch or not at the commit, a branch moved, a ref changed, a branch, tag or remote that exists again, settings changed, checked out elsewhere, local changes or ignored files in the way, the index or files changed, the stash gone or listed again, the status changed, the commits gone, the remote changed, a flag git cannot restore, git refused) with what its text names; the app words it (7.6) |

**The git half** (`GitRepository.Undo.cs`, D167, D169, D170).

- `ReadRefStatesAsync(scope)` reads, side by side and writing nothing but kept blobs: HEAD with one
  call (`rev-parse HEAD --symbolic-full-name HEAD`, or `symbolic-ref -q HEAD` while the branch is
  unborn); one `for-each-ref` of the scope's refs with `%(symref)` and `%(worktreepath)` (exact
  names filtered in code); the stash list; the scope's `config --local` keys; the files (their
  SHA-256 read as a stream; `hash-object -w --no-filters` only when the scope keeps bytes) and
  their index entries (`ls-files --stage -z`, in chunks of at most 24,000 characters of paths); the
  status; the `.gitignore`.
- `RunUndoStepAsync(step)` (`UndoStepRun`) runs inside the step's operation, in this order:
  1. Refused while an operation is in progress (the git folder read: `update-ref` refuses nothing
     during a merge, a rebase or a pick) or files conflict (`ls-files --unmerged`), except the
     undo of an apply or a pop whose recorded files hold every conflicted path.
  2. What the effects check is read (`ReadRefStatesAsync(UndoChecks.ScopeOf(step))`).
  3. The step is simulated (`UndoChecks.FirstRefusal`); then one `cat-file --batch-check` looks up
     every object the step writes or points a ref at (a commit an outside `gc` pruned: "the
     commits no longer exist"); then the checks only git can make, before anything is written, for
     every effect whose state the earlier effects do not change: a Keep move's ignored-files
     guard, the index against a commit (`diff-index --cached --quiet`), the flags of index entries
     (`ls-files -t`, and an intent-to-add `.A` in the status: `update-index --index-info` cannot
     write either back), what stands in a pop's or a restore's way. A hard reset's undo checks its
     index right after its Keep move, which carries the index. The others are made at their place.
  4. The effects are applied: a `RemoteRef` first (only another machine can refuse it), then the
     rest in the step's order, what git can refuse locally (`switch`, `read-tree`) and the index
     and file writes at their place. Ref writes (soft moves, `SetRef`, tags) wait for one
     `update-ref -m <message> --stdin` transaction with expected values, all or none, written when
     a later effect needs one of its refs (a switch to a branch the step creates) or at the end;
     a symbolic ref is written after it (`symbolic-ref -m`: 2.36's `--stdin` has no symref
     commands) or deleted with `update-ref --no-deref -d`; configuration and `.gitignore` writes
     come after the transaction and any switch. When an effect refuses after earlier ones were
     applied, those are reversed in reverse order (with no token, so a cancel cannot stop it) and
     the step is refused; the only write a refused step may leave is `update-index --refresh`'s
     stat data.
  5. The entry is rewritten with what the step made (a redo's stash, a redo's hard-reset snapshot,
     a `tag-undone-…` backup) and with the bytes of the files it overwrote, which its own read kept,
     so that the opposite step writes back exactly what this one left.
- Reflog messages: `VisualCommit: undo <text>` and `VisualCommit: redo <text>`, or a restore's
  `VisualCommit: <entry text>` (`VisualCommit: restore of branch doomed`); a `switch` keeps git's
  own message, so `@{-1}` keeps working; `GIT_REFLOG_ACTION` is never set. Refs are always named
  in full (`refs/heads/<b>`), never through `HEAD`'s link.
- **The Keep move** (D169): `update-index -q --refresh` (git's own checkout refreshes first;
  without it an unchanged file with stale stat data refuses with `not uptodate`), then `read-tree
  -m -u <from> <to>`, which keeps staged, partly staged and unstaged work and refuses, changing
  nothing, when a file the move changes has local changes or an untracked file or folder is in the
  way; then the compare-and-swap `update-ref -m <msg> refs/heads/<b> <to> <from>` (`--no-deref
  HEAD` for a detached HEAD). When that fails (a race), `read-tree -m -u <to> <from>` puts the
  files back exactly. Never `reset --merge`, `reset --keep` or `switch -C`. Git names only the
  first path in the way, so the app counts them: the paths `diff-tree -r -z --no-renames
  --name-only <from> <to>` lists that the status shows changed or untracked, a folder holding such
  once, an untracked file where the move puts a folder once. A `read-tree` that fails after it
  began writing (a file held open on Windows; it writes no index) has the move's other files put
  back from the index (`checkout-index -f`) and the files only the target has deleted where they
  hold its version; the step then fails with git's line.
- **The undo's ignored-files guard**: from Git 2.34 `read-tree -m -u` overwrites an ignored file where
  the target adds one and deletes an ignored folder where it adds a file, without a word, and no
  option keeps them. So before a Keep move the paths it adds (`diff-tree -r -z --no-renames
  --name-only --diff-filter=A <from> <to>`), the files inside a folder at one of them, and their
  parent folders that are files on disk, are asked about with `check-ignore` (`IgnoredAmongAsync`,
  without `GIT_LITERAL_PATHSPECS`; since phase 6 through `FindIgnoredAtAsync`, which Keep ignored
  files shares: see "The ignored-files guard" under "Protected changes"); any ignored one refuses ("1 ignored file is in the way: {name}.
  Move it away first."). The undo's `switch --no-guess --no-overwrite-ignore` refuses in that case
  by itself; its refusal is read with `GitFailureClassifier.BlockedPaths`, a folder git names asked
  about by its files (`FilesUnder`): only ignored files gives the ignored-files text, anything else
  is local work.
- **What else stands in the way** (phase 5's review): a pop's check reads the stash's paths once
  (`ReadStashPathsAsync`): its untracked files that exist again refuse (git's own pop would refuse
  only after it restored the others), and the guard above runs for the paths the stash adds
  (`StashPaths.Added`: `stash pop` overwrites an ignored file where the stash adds a staged one);
  a `RestoreSnapshot` refuses where a folder holding files, or a file in one of its folders'
  place, stands at one of its files (`WorkingTreeFiles.IsInTheWayOfFile`), also after a hard
  reset's Keep move and for the backups list's restores, which record no state to compare; an
  empty folder is replaced and loses nothing.

**The app half** (`RepositoryOperations.Undo.cs`).

- **Recording** (7.7). Each operation names its scope (`RunOptions.Undo`; the stage panel's
  writes through `WriteAsync`). Inside the operation, before the work, `BeginUndoRecordingAsync`
  reads `ReadRefStatesAsync(scope.BeforeAction())`; after it, `EndUndoRecordingAsync` takes the
  history's turn, completes what the capture lacks (a hard reset's snapshot read back from its
  commit when the reset did not hand it over; the bytes an ignore appended to `.gitignore`), reads
  again with `scope.AfterAction(changedFiles)`, and adds an entry when anything changed: `Done`, or
  `Pending` for a kind that can stop and left an operation in progress. An action that changed
  nothing (refused, up to date, nothing to stash) records nothing, and so does one whose sequences
  cannot be made from what was read (logged at Warning), D91's "Stash changes and check out" whose
  pop conflicted (its stash is kept and named) and a checkout that left HEAD where it was and
  created nothing. The stage panel's Commit that ends a stopped operation completes its pending
  entry, and records nothing for one started outside the app. The entry is added on the UI
  context, not carried through `OperationReport`.
- **Bytes kept only where an undo writes them back**: the read before an apply, a pop or a restore
  keeps the files' raw bytes as blobs (`UndoScope.KeepsFileBytes`); every other read records the
  SHA-256 alone, read as a stream (a file over 2 GB is recorded). A discard of a file under a filter
  (Git LFS, `core.autocrlf`) copies no raw content into the object store; its undo copies the
  content it overwrites, once. A `FilesTo` whose bytes were never kept is refused, and a file is
  never deleted for want of its bytes.
- **The toolbar's state** (7.4): `ReadUndoAvailability()`. Undo is enabled with `Undo {text}`
  when the entry before the cursor is done and passes the in-memory checks against the refs and
  status last read; otherwise it is disabled with the first reason that holds: `Wait for {title} to
  finish` (a user operation runs or waits, also behind a background one; a background one alone
  gives none), `Finish or abort the {kind} first`, `Resolve the conflicts first` (not for an apply's
  or a pop's undo whose recorded files hold every conflicted path), `Close the interactive rebase
  first`, `Nothing to undo`, or the check's own text (`main has moved since (it is at 9f8e7d6)`).
  Redo likewise with `Redo {text}` and `Nothing to redo`. The order is strict: an entry that
  cannot be undone blocks the older ones, and the backups list is the way past it; outside changes
  never remove entries. The checks are kept while the entry, the refs and the status are the same
  objects, so the toolbar reads the states on every `Changed` of the queue for little. Before the
  refs are read, and after the tab closed, it gives both disabled without a tooltip, which the
  toolbar shows as `Nothing to undo` and `Nothing to redo`.
- **A step** (7.5): Undo and Redo (the toolbar, or Ctrl+Z and Ctrl+Y) run as one operation of the
  queue each, titled `Undo {text}` or `Redo {text}` (running text `Undoing {text}…` or
  `Redoing {text}…`), refresh
  `Refs`, prompts `Ask`; a second click for the same entry while its step asks, waits or runs gets
  that step's task. Inside: the history's turn, the entry checked to be still at the cursor (else
  the operation ends cancelled, having changed nothing: another tab on the worktree moved the
  history), the git step, the cursor moved over the rewritten entry; the status bar then
  says `Undid {text}` or `Redid {text}`. A refusal opens "Undo failed" or "Redo failed" with `Can't
  undo {text}: {reason}` and git's output only when git refused.
- **Questions for a remote** (D155, D170): a step with a `RemoteRef` asks first, naming the remote,
  with `Undo` or `Redo` as its button, not in the destructive style: "Undo the push to {r}?" ("{b}
  on {r} goes back to {short}, which it was before the push. Anyone who fetched {short2} keeps
  it."), "Delete {b} on {r}?" for a push that created the branch, "Undo the deletion of {b} on
  {r}?", their redos, and the same for a tag (an annotated tag named by the commit it points to,
  D183). A lease that fails shows the push's error dialog with "{b} on {r} has changed since:
  nothing was changed."
- **After a step**: a discard's Undo hides the restore bar and its Redo shows it again for the
  same snapshot (7.9); an undone commit's message fills empty boxes of the stage panel, and its
  Redo empties boxes that still hold it; the graph filter follows branch changes (an undone rename
  moves its entries back, an undone creation removes them, a branch brought back in solo mode is
  soloed).
- **Backups kept**: `UndoBackupsToKeep()` feeds `RepositoryViewModel.BackupsToKeep` (D171).
- **The backups list's restores** (7.10, D172; `RestoreBackupAsync(backup)`, chosen by the
  backup's kind): `Restore branch {b}` (the branch at the backup's commit with a reflog of its own;
  the backup holds no upstream keys), `Restore tag {t}` (the same tag object), `Restore stash`
  (`stash store` with the commit's subject, back as `stash@{0}`), `Restore the discarded files…`
  and `Restore the changes…` (D75's question when a file would be overwritten, decided again inside
  the operation, the newer version saved first; for a hard reset's changes the index first, for
  the snapshot's own paths, when nothing is staged now, then the files), `Restore the staged
  changes` (a mixed reset's index snapshot, for its own paths: the `diff-tree` list less the paths
  whose version equals HEAD's, BD16) and `Push {b} back to {r}…` (7.5's question, then a push
  whose lease says the branch must not exist, or, for a force push's backup, that it still holds
  the commit the force push left, read from the remote-tracking reflog when the list loads). Each
  is one operation, recorded like any action as the kind whose sequences undo it. A backup whose
  commit a `gc` pruned refuses with "Can't restore …: the commits no longer exist".
- **The cost per operation** after step 25's review (git calls inside the operation, Git 2.55,
  the ops scenario): commit 6, checkout of a branch 6, the automatic stash's checkout 13, merge 10,
  a cherry-pick that stops 10, mixed reset 10, hard reset with a local change 18, stash 9, apply
  14, pop 16 (22 before the review), a pop C12 refuses 11, push 7, discard 12, tag 5, branch
  deletion 9; undo and redo of an apply 10 each, of a discard 6 each, of a stash 8; undo of a hard
  reset 13, its redo 16. Design 7.7 had expected about two reads before and two after. The review
  shared HEAD's read with the operations' own checks (`ReadHeadAtStartAsync`), had a hard reset
  hand over its snapshot's paths and index, read the in-progress state and the conflicted paths
  without a whole status, and read a stash's paths once per stash commit.
- Tests: `UndoHistoryTests`, `UndoScopeTests`, `UndoSequenceTests` (of Core, without git) and
  `UndoEffectTests`, `UndoStepTests`, `UndoStepBranchTests`, `UndoStepFileTests`,
  `UndoStepRefusalTests`, `UndoStepRemoteTests` (against real git), all in Git.Tests;
  `UndoRecordingTests`, `UndoRestoreTests`, `UndoStepTests`, `UndoToolbarTests`,
  `KeptCommitGoneTests` (App); the walk-through's part U.

### Protected changes and the automatic stash (C12, D138, D153, D166, D179, D193, D196, D221)

Phase 5, with phase 6's Keep ignored files (below). `RepositoryOperations.Protect.cs`, the checkout
paths in `.Branches.cs` and `.RemoteBranches.cs`, the pull's in `.FetchPull.cs`, the stash's in
`.StashTags.cs`; texts in `OperationTexts.Protect.cs`.

- **Which checkouts stash** (9.1, `AutoStashTargetAsync`): a checkout of a commit or a tag (never a
  branch or a remote branch) while HEAD is on a branch at another commit that reaches the target
  through any parent: the graph answers for a loaded row of HEAD's graph
  (`IsReachableFromHead`), `merge-base --is-ancestor <sha> HEAD` otherwise. Such a checkout always
  runs `git switch --detach --no-overwrite-ignore` (an old commit that tracks a file now ignored
  would overwrite it, and the way back would delete it), with the automatic stash first when the
  status has changes (staged, unstaged, untracked or conflicted; ignored files are none). Every
  other checkout, HEAD's own commit included (D183's ruling), runs as before, with git's default
  for ignored files, since phase 6 only while Keep ignored files is off (otherwise with
  `--no-overwrite-ignore` too, D193); an undo's switch never stashes.
- **The automatic stash** (9.2, `CheckoutWithAutoStashAsync`): one local operation of several
  steps titled as the checkout, which never asks. Inside it: a reflog row's or a backup's commit
  checked to exist; HEAD where the click saw it, or still on a branch at another commit that
  reaches the target, else refused ("HEAD moved while this waited: it was on {a} and is on {b} now.
  Nothing was checked out."); a fresh status, refused before any write while an operation is in
  progress ("Finish or abort the {kind} first": a stash during a merge without conflicts would
  delete `MERGE_HEAD`) or files conflict ("Resolve the conflicts first"); with changes, running
  text "Stashing your changes…" and `git stash push --include-untracked --message "Auto stash
  before checking out {name}"` (without `GIT_LITERAL_PATHSPECS`, D108; `{name}` the short SHA or
  the tag); then "Checking out {name}…" and `git switch --detach --no-overwrite-ignore <sha>`.
  Done, the status bar says `Your changes are in the stash "Auto stash before checking out
  {name}".` The stash is drawn as every stash, a ring on the branch's tip with a dashed line;
  nothing pops it later (D153); Undo switches back and pops it (D166).
- **A refusal after the stash**: what was in the way is read, then the stash is popped back
  (`stash pop --index`, D91's plain-pop fallback), and "Checkout blocked" shows without "Stash
  changes and check out": the ignored-files text when every path git named is ignored
  (`FindIgnoredAsync`, a folder by its files: "1 ignored file is in the way: {name} tracks a file
  of the same name. Move it away first."), D153's sentence otherwise. When the pop back fails:
  "Git could not check out {name}, and your changes could not be re-applied. They are kept in the
  stash …".
- **The half-made stash** (0.7, 15.9): a file held open on Windows can stop `git stash push` after
  it saved the stash: an untracked one stops its clean after it deleted the other untracked files,
  a tracked one its reset after it rewrote the other tracked files. When `refs/stash` moved,
  `PutBackUnclearedStashAsync` restores the missing untracked files from `<stash>^3` and the
  tracked files that differ from the stash (`git diff --name-only -z --no-renames <stash>`, then
  `restore --source=<stash> --worktree`); nothing is checked out, the stash is kept, and "Checkout
  failed" says "Git could not clear your changes from the working tree, so nothing was checked
  out. They are also saved in the stash …" only when every path matches again, otherwise "Your
  changes are saved in the stash … Keep the stash until you have checked your files." The undo's
  own `StashPush` puts back the same before its step fails. When `refs/stash` did not move, git
  saved and changed nothing, and the checkout's default dialog shows. The stash `StashAsync`
  makes, the automatic one included, also makes again the empty untracked folders git's clean
  removed.
- **The refusal message** (9.3, D153 (1)): `GitFailureClassifier` reads standard error and output
  together and counts the distinct paths under git's headers (`GitFailure.BlockedPaths`, from
  `GitFailureClassifier.BlockedPaths`). Kinds: `CheckoutBlockedByLocalChanges`,
  `CheckoutBlockedByUntrackedFiles` (`would be overwritten by checkout` or `would be removed by
  checkout`), `CheckoutBlockedByUntrackedFolder` (`Updating the following directories would lose
  untracked files in them`), `MergeBlockedByLocalChanges` (the `… by merge:` blocks) and
  `MergeBlockedByStagedChanges`. Every checkout path (phase 3's checkouts, "Create branch" with its
  box ticked, a drop's "Check out and merge", "Check out and rebase") shows "Checkout blocked" with
  "1 conflict prevents checkout. Commit, stash, or discard your changes first." or "{N} conflicts
  prevent checkout. …" and git's output below it, which names the paths (`CheckoutFailureDisplay`);
  "Stash changes and check out" is offered for tracked changes alone, on phase 3's checkouts.
  Before git, an operation in progress or conflicted files refuse with that title and no git
  output ("Finish or abort the {kind} first", "Resolve the conflicts first"). A merge, a drop's
  merge, a pull's merge or fast-forward (every block git prints, the folder's included), a
  cherry-pick or a revert (one or a list) refused for changes in the way says "{N} conflicts
  prevent the merge. …" (or "the cherry-pick", "the revert"), or for something staged "Your staged
  changes prevent the merge. Commit, stash, or unstage them first.", under the operation's own
  error title. A rebase keeps phase 4's "Git rebases only a clean working tree: commit or stash
  your changes first."; a pull's rebase refused at its own checkout keeps the pull's summary.
- **C12's choice made again** (15.9): every checkout's operation reads HEAD again
  (`ReadHeadAtStartAsync`); a checkout the click gave no automatic stash is refused with D124's
  text when HEAD has meanwhile moved onto a branch that reaches the target
  (`RefuseIfNowAutoStashTargetAsync`), as the automatic stash's own path refuses a target it no
  longer reaches.
- **A stash's apply or pop** (the menus, the toolbar's Pop, a double-click) is checked by the app
  before git (`CountStashConflictsAsync`): the stash's changed paths (both sides of a rename) that
  have a local change in a fresh status, and its untracked files that exist: "1 conflict prevents
  applying the stash. Commit, stash, or discard your changes first."; git is not run. Stricter than
  git on purpose: git's refusal restores the untracked files anyway, and a staged overlap becomes a
  conflict. D91's own pop after "Stash changes and check out" still goes to git.
- Tests: `ProtectedChangesTests`, `FailureClassifierTests` (Git); `RepositoryOperationsProtectTests`
  (App); the walk-through's part P.

**Keep ignored files** (phase 6, D193, D196, D221, design 9). Git's `--no-overwrite-ignore` is
exact for a checkout, works for a merge only when it fast-forwards, and does not exist for rebase,
cherry-pick, revert or stash, each of which overwrites an ignored file it writes; a rebase can
delete one. So checkouts and merges pass git's option, and every other covered operation runs the
app's own check before git.

- **The setting**: `AppSettings.KeepIgnoredFiles`, on by default (also for an older file), the
  settings screen's `KeepIgnoredCheckBox`. `RepositoryOperations.KeepIgnored` reads it inside each
  operation when it runs (on without settings, in view-model tests), so a queued operation takes the
  value at its start (D124), and Pull all runs the same pull.
- **What it covers** (D196): every checkout (a local branch, a remote branch, a commit or tag that
  detaches, HEAD's own commit included, "Create branch" with its box ticked, "Stash changes and
  check out", the checkouts of "Check out and merge", "Check out and rebase" and the interactive
  list, Abort's return after "Check out and rebase", the pages' "Check out this commit") passes
  `--no-overwrite-ignore`; every merge (both kinds, the fast-forward of the current branch, the
  drop's, "Check out and merge"'s, the pull's) passes it too and runs the check; every rebase (onto,
  "Check out and rebase", interactive, squash, the pull's), cherry-pick and revert (one or a list)
  and stash apply and pop run the check alone. Not covered: the hard reset (its
  `reset-<time>-changes` snapshot saves the ignored files it overwrites, D127), the other resets,
  discards, the resolver's sides, the restore bar and the backups list's restores, undo and redo
  (D169's guards, whatever the setting), D91's pop and the automatic stash's pop back; nothing is
  checked again on Continue, Skip or Abort (design 9.13, 9.14).
- **The app's check** (`RefuseIfIgnoredInTheWayAsync`, `.Protect.cs`; design 9.9): inside the
  operation, after its own refusals before git and before its first write (a backup, the undo
  capture, git), one of the git layer's five reads lists the ignored files the operation would
  overwrite or delete. With any, the operation is refused under its own error title ("Merge
  failed", "Rebase failed", "Cherry-pick failed", "Applying the stash failed", …) without git
  output, naming the files (up to three, then ", …": "1 ignored file is in the way of the merge:
  {file}. Move it away first.", "{N} ignored files are in the way of the cherry-pick: {names}. Move
  them away first.", "… in the way of applying the stash: …"); nothing is written, saved or
  recorded. A read that fails is logged and the operation goes on to git. A stash's check comes
  after D179's count. The pull's check runs inside `IntegratePullAsync` after the fetch, once the
  fast-forward, merge or rebase is chosen (`PullOutcome.IgnoredFilesInTheWay`: "Pull failed" with the
  merge's or the rebase's sentence); a `--ff-only` pull of diverged branches is left to git, which
  refuses it as not a fast-forward and writes nothing.
- **A refused checkout** (`CheckoutRefusalDisplayAsync`, design 9.11): every checkout path catches
  git's refusal inside its operation; for an untracked-files or folder block while the option was
  passed, the paths git named are asked about (`FindIgnoredAsync`, a folder by its files), and when
  every one is ignored the refusal says D179's text with git's count ("1 ignored file is in the way:
  feature/alpha tracks a file of the same name. Move it away first.") and offers no "Stash changes
  and check out" (a stash keeps no ignored file); anything else is phase 5's display. "Stash changes
  and check out" asks about the paths before it pops its stash back.
- **Off** restores phase 5 exactly (D221): no option, no check and no `check-ignore` after a
  refusal, while the automatic stash's checkout keeps D179's `--no-overwrite-ignore` and its
  `check-ignore`, and undo and redo keep D169's guards.
- Between the check and git's write about one process start passes; a file another program makes
  there meanwhile is overwritten as git would, except in a fast-forward, which git's option refuses
  (design 9.12).

**The ignored-files guard** (`GitRepository.KeepIgnored.cs`; phase 5's guard of the undo's Keep
move, moved there in phase 6 and shared). `FindIgnoredAtAsync(written)` takes the paths an
operation may write and looks on disk, for each, at every folder above it that is a file or a
symbolic link, at the path itself when it is a file or a link, and at every file under it when it
is a folder (links not followed); nothing is asked under a folder that does not exist. The
candidates go, in byte order, to one `git check-ignore --stdin -z` (`IgnoredAmongAsync`, without
`GIT_LITERAL_PATHSPECS`), which leaves tracked files out; no candidate on disk, no process. It
returns pairs of a written path and an ignored file, so that a merge's check can keep or drop each
by the path it writes. The five reads (`IGitRepository`, read environment, no pathspec, every tree
list ended by `--`, lists of unbounded length on standard input, since Windows starts no process
past 32,767 characters) list the written paths from git's trees:

- **a merge** (`FindIgnoredInTheWayOfMergeAsync(into, source)`, `into` being `HEAD` or, before
  "Check out and merge"'s checkout, `refs/heads/<A>`): what the source has and `into` lacks
  (`diff-tree --no-renames --diff-filter=A`); only when something is in the way, per merge base
  (`merge-base --all`) what the source added, modified or changed in type since it, a pair kept when
  some base lists its path (otherwise `into` deleted it and the merge keeps the deletion); no base and
  a fast-forward keep every pair. Not `merge-tree --write-tree`, which would be exact but needs Git
  2.38;
- **a cherry-pick or revert** (`FindIgnoredInTheWayOfCommitsAsync(commits, revert, mergeParent)`):
  one `diff-tree --stdin --root --no-renames --diff-filter=AMT` (`-R` for a revert) fed the commits'
  full SHAs in the order git applies them (`<sha> <first parent>` for a merge reverted with `-m 1`);
- **a rebase** (`FindIgnoredInTheWayOfRebaseAsync(tip, onto)`, `tip` being `HEAD` or the branch's
  full ref before "Check out and rebase"'s checkout, `onto` the menu's SHA or the pull's upstream):
  four reads side by side (`merge-base --is-ancestor`, what `onto` adds against the tip, the paths
  `log --no-merges --diff-filter=AMT <onto>..<tip>` lists, a merge in the range); an ancestor `onto`
  and no merge mean git writes nothing; with a merge in the range also what each boundary commit
  (`rev-list --topo-order --boundary`) adds against the tip, where `--rebase-merges` or Git 2.42's
  `rebase.rebaseMerges` resets. A superset of what git writes;
- **an interactive rebase or a squash** (`FindIgnoredInTheWayOfRebaseAsync(plan)`): from where git
  starts, moved past the leading picks git skips (`RebaseTodo.LeadingPicks`, read with `rev-list
  --stdin --no-walk=unsorted --parents`), what that start lacks of the tip and every applied commit's
  change after it (`RebaseTodo.AppliedCommits`, the picks' `diff-tree --stdin`);
- **a stash's apply or pop** (`FindIgnoredInTheWayOfStashAsync`): its changed paths less
  `StashPaths.Deleted`, and its untracked part's paths, from `ReadStashPathsAsync`'s kept read, so
  no process runs before `check-ignore`.
- **directory renames** (design 9.6's "Directory renames"; the review at status step 22): git's
  merge-ort writes a file one side added under a folder the other side renamed whole at its place
  under the new folder, over an ignored file there (a merge, a pick, a revert, a rebase; a stash
  apply on 2.55 and 2.56, while 2.36 writes it beside), and none of the reads above lists that path.
  So when a read found nothing else in the way, and a test of the disk with no process passes
  (`MayMoveThroughADirectoryRename`: a path it writes is missing on disk and lies in a folder, or
  lies at the top while a top-level folder holds a file of that name), one `git diff-tree -r -z -M
  --name-status <ours> <theirs> --` is read and `DirectoryRenames` (Git, pure) gives the paths a
  rename either side made may write, by merge-ort's rules where the read can tell them (a folder
  goes where most of its moved files went, counted at each level whose trailing names agree; into
  the top too, never the top itself; no rename while the renaming side still has a file in the
  folder) and every candidate where it cannot (a tie, folders whose other files are alike on both
  sides); those paths go to `FindIgnoredAtAsync` too. The sides: a merge `into` against `source`,
  its result dropped when either is an ancestor of the other; a pick HEAD against each commit, one
  read per commit side by side; a revert HEAD against each commit's parent (or `mergeParent`), none
  for a root commit; a rebase `onto` against the tip; a stash HEAD against the stash. Without such a
  folder the reads make the calls they made before. Not covered: a folder renamed into the top from
  deeper than the top level, a side the disk does not show (the branch of "Check out and rebase"),
  and the interactive rebase and the squash.

The undo's Keep move and pop take the distinct ignored files of `FindIgnoredAtAsync`
(`IgnoredWhereAddedAsync`, see "Undo and redo"). Tests: `IgnoredInTheWayTests`,
`DirectoryRenamesTests`, `KeepIgnoredCheckoutTests`, `IntegratePullTests`, `RebaseTodoTests` (Git);
`RepositoryOperationsKeepIgnoredTests` (App); the walk-through's G22 to G24 and S21.

### Prompts and credentials (D82, D115, D118, D130, D255, D273 to D279)

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
- **The helper's credential mode** (phase 7a, `CredentialMode.cs`, D255, D273, design 4.5): with
  `--vc-credential` as its first argument the helper is git's credential helper for the URLs bound
  to the hosting account (below); git runs it through `sh` with `get`, `store` or `erase` appended
  and its request on standard input. It reads standard input to the end (at most 64 KiB kept, the
  rest drained, so that git's write never meets a closed pipe) and keeps `protocol`, `host`,
  `username` and `password` of git's `key=value` lines, ignoring every other key (`capability[]`,
  `wwwauth[]`, `path`, …). `store`, an unknown action, too much input, a missing `protocol` or
  `host`, a missing pipe or secret, and an `erase` without a password do nothing. Otherwise it
  connects as a prompt does (5 s; the pipe's owner checked on Windows), sends one
  `AskPassCredentialRequest` line (`token`, `operation`, `credential` `get` or `erase`, `protocol`,
  `host`, `username` and, for an erase, `secretHash`: the lower-case hex SHA-256 of the refused
  password, never the password) and waits at most 30 s (`CredentialAnswerTimeout`) for the answer.
  A `get` answered with a token prints `username=x-access-token` and `password=<token>`, each with
  a line feed, in UTF-8; a token holding CR, LF or NUL prints nothing, and it never announces
  `capability[]`. It always exits 0 and writes nothing to standard error, so that git goes on as
  with a helper that knows nothing. `VISUALCOMMIT_ASKPASS_DENY` does not stop this mode: a `Deny`
  scope's bound URLs are answered too (D274). `AskPassProtocol.ParseRequest` refuses its line, and
  `ParseCredentialRequest` a prompt's.
- **The broker** (`VisualCommit.Git/Prompts/AskPassBroker`, `IGitPromptBroker`, one per app
  session, made by `PromptDialogs.CreateBroker` in `AppSession.Start`): a named pipe `vc-<16 hex>`
  (on macOS and Linux .NET makes it a Unix socket under the temp folder, whose path must stay
  under 104 characters), up to `MaxConnections` connections (4 until phase 6, 16 since: a bulk
  run's four git processes can each wait for their group's turn with room beside them for every
  tab's own prompt and a clone's; on Windows one more connection waits for a free instance, and the
  helper gives up after its 5 seconds; on macOS and Linux it connects at once and waits in the
  socket's queue, D210), one request per connection (10 s to arrive, 64 KiB at most), for the
  current user only (on Windows an ACL that gives the token's user full control and makes it the
  owner). A request is answered only when its token matches the session's secret (32 random bytes,
  compared in fixed time) and its operation id names an open scope; otherwise null. `Begin(title,
  repositoryName, mode, operationId)` opens a `PromptScope` whose `Environment` is, for `Ask`:
  `GIT_ASKPASS` and `SSH_ASKPASS` (the helper), `SSH_ASKPASS_REQUIRE=force`, the pipe, the token and
  the operation id; for `Deny`: the helper, `SSH_ASKPASS_REQUIRE=force`,
  `VISUALCOMMIT_ASKPASS_DENY=1`, `GCM_INTERACTIVE=never`; for `None`: nothing. Each mode removes the
  other's variables. `GIT_TERMINAL_PROMPT=0` stays as the fallback. Disposing a scope refuses its
  open prompt and closes its dialog; a helper that goes away (git stopped it) closes the dialog too.
  The token and the answers are never logged; the log names a prompt by its kind. Since phase 7a
  `PromptDialogs.CreateBroker` also gives it the hosting session as its `IGitCredentialSource`, the
  session's git environment and a function that gives the detected git's version; a scope `Begin`
  opens with bindings it uses adds their `GIT_CONFIG_COUNT` pairs to `Ask`'s variables, or to
  `Deny`'s with the pipe, the secret and its id, and only such a `Deny` scope is registered (D274);
  a credential request line is answered without UI (below). On macOS and Linux, where .NET backs
  every instance of the pipe with one listening socket that it closes with the last instance, the
  broker holds one instance more while it listens, the keeper, which never waits for a connection,
  so that the socket stays open while an answer without UI disposes its instance and a helper queued
  meanwhile is served; every instance there names `MaxConnections + 1`, and no instance left while
  it listens is a Warning ("The prompt channel closed its socket between two connections; …")
  (design 16 item 11).
- **Which calls ask:** user-started network operations and clone (`Ask`), since phase 6 a bulk
  run's fetches and pulls among them (D210); phase 3's, phase 4's and
  phase 5's local operations (`Ask`, not cancellable but for the merge tool: a filter or hook such
  as git-lfs may ask, and a rebase's guarded amend runs the user's hooks), undo and redo steps
  included (one that changes a remote is a network operation); auto-fetch (`Deny`); reads, phase
  2's writes, the fast-forward's `git fetch .` and the background clean-up of the backups (`None`). Credential
  helpers, Git Credential Manager included, are left as configured and asked first by git (Q5),
  except, since phase 7a, by a network operation's or a clone's calls to a URL bound to the hosting
  account, which ask the app's helper alone (D255, a second exception to Q5 beside D193);
  auto-fetch's `Deny` scope is answered from the account too.
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
- **A bulk run's prompts** (phase 6, D210, design 4.4) form a group (`PromptGroup`, through
  `GitOperation.PromptGroup`, `IGitPromptBroker.Begin` and `GitPrompt.Group`): `PromptDialogs`
  shows one prompt of a group at a time, and a prompt text the group was answered before (git's
  text names the URL and the user) is answered from that answer at once, a Cancel included, until
  the run ends (at most `KeepAnswersFor`), so ten repositories on one host ask once; a notice is
  never held back, a dialog closed because its scope ended keeps nothing, and a prompt whose group
  ended before its turn is answered as one without a group. A sign-in dialog names its repository
  while a dashboard tab is open (design 3.8). Since phase 7a (ruling 9, design 4.7) a group keeps an
  answer under the prompt's account and text (`GitPrompt.Account`'s id, empty for none), and the
  password typed with a user name under the account git's next prompt will carry (the prompt's
  account when the typed user is none, the login or `x-access-token`:
  `AskPassBroker.IsAccountUser`), so one text under two accounts asks twice; a row that fails with
  `AuthenticationFailed` calls `PromptGroup.ForgetAnswers()`, after which no answer kept before, a
  Cancel or a password included, is used.
- **Masked boxes** (`CredentialPassword`, `PassphraseBox`, a masked `PromptBox`) are
  `Controls/SecretTextBox`: a stock text box whose automation peer reports an empty value and
  raises no text change while it is masked, so no UI Automation client can read the secret (D130).
- **What only the real-window pass and hand checks show:** the real app's own helper, pipe and
  broker in the real process environment (check 21's sign-in against `AuthRemoteServer`, check
  21b's prompts through a fake `core.sshCommand`, D115). A real ssh server's host-key question and
  Git Credential Manager's own window are checked by hand (the report's manual checklist). Since
  phase 7a the pass's part B clones the fake's private repository through the real helper's
  credential mode, and the opt-in live command's `Check_2` clones a private sandbox repository on
  github.com so (D326); both look for the broker's "answered from the account" line.

#### The account's credentials in git (phase 7a, D255, D273 to D279, D329 to D332, D340)

Since phase 7a (D255, amending D82), while a GitHub account is signed in with its token in hand and
its "Use for git over HTTPS" on, the app's own git calls to a URL bound to it take their credentials
from the account through the helper's credential mode (above), and from none of the user's
credential helpers: clone, fetch, pull, push, auto-fetch, the dashboard's Fetch all and Pull all, a
pull request's checkout fetch and the create form's push. Nothing is written to a git configuration
or a credential store, and git in a terminal still uses the user's helpers. The token is in no
argument, URL, configuration value, environment variable, log line or file: it goes from the
hosting session to the helper over the prompt pipe, and from the helper to git on its standard
output. ssh, other hosts, and github.com while nobody is signed in or the switch is off keep D82's
order. The account, its token and git's `get` and `erase` on the session's side are in "Hosting:
the session and the account"; where tokens are kept, in "Tokens and the keychains".

- **Bindings** (`Core/Operations/CredentialBinding.cs`). `CredentialBinding(Url, Account)`: a URL
  whose credentials come from the account, git's URL after `insteadOf` with only `user@` removed.
  `CredentialBinding.Select(remotes, webBase, account)` (D278) takes every fetch and push URL of the
  remotes, in order, that parses as an absolute URI with the web address's scheme, host (without
  case) and port (the default port the same as none) and whose user information is empty or exactly
  the login (without case); a password, another user, another scheme, host or port leaves it
  unbound (ruling 1). A binding is then dropped when another URL of the remotes on that scheme, host
  and port names a user other than the login with a path equal to the binding's or under it at a
  `/`, since git's key without a user would answer that user too. Distinct by `Url`, ordinal.
  `GitCredential` is what git gets: the user name `x-access-token`, never the login, and the token
  (its text `GitCredential(***)`); `IGitCredentialSource` (`GetAsync(account, ct)` for git's `get`,
  `Refused(account)` for its `erase`) is where it comes from: the hosting session, or
  `NullGitCredentialSource`, which answers nothing. Which URLs a repository binds, and when, is the
  hosting's (`HostingRules.Bindings`: none unless signed in with the token in hand, the switch on
  and the user's URL rewrites known, D290; see "Hosting: a repository's pull requests and
  avatars"); the URLs are git's after those rewrites (`UrlRewrites`, see "Git layer").
- **The calls that carry them.** `GitOperation.CredentialBindings` goes through the queue to
  `IGitPromptBroker.Begin`. `RepositoryOperations.RunAsync` gives them to network operations only
  (the tab's, or a dashboard row's after its own read of the rewrites, D332, with a Debug line of
  how many URLs it bound; see "Operations"), and `AutoFetchScheduler.CreateOperation(repository,
  bindings)` to auto-fetch's `Deny` scope (bound URLs take the token and still never prompt, D79). A
  clone opens its own scope with the bindings of `WelcomeViewModel.BindingsForCloneAsync`: none
  unless `IHostingSession.BindingsForClone` binds the URL as typed; then git's configuration is read
  once with phase 6's outside read (`GitConfiguration.ReadAsync(null)`, local and worktree entries
  dropped, since `git clone` applies none of a repository around its folder) and the URL its `url.`
  rules give is bound (D331); a failed read binds nothing and logs a Warning. Local operations,
  reads and phase 2's writes carry none.
- **The scope's variables** (`AskPassBroker.Begin`; D274, D277, D329, D340). A scope uses its
  bindings only when the helper was found, a credential source was given, the inherited
  `GIT_CONFIG_COUNT` is valid, the mode is `Ask` or `Deny` and the detected git is 2.31 or newer (a
  version not yet known counts as 2.31: an older git ignores the pairs anyway); each URL once.
  Otherwise the scope's variables are phase 3's, byte for byte. The inherited count is the
  session's git environment's `GIT_CONFIG_COUNT` when it names the variable, else the process's;
  empty or absent is 0, and any other value that is not a whole number turns every binding off with
  one Warning ("GIT_CONFIG_COUNT is not a number git accepts; GitHub's token is not given to git.").
  A scope with used bindings gets `Ask`'s variables, or `Deny`'s with the pipe, the secret and its
  id (only such a `Deny` scope is registered; its prompts stay refused), and for binding `i`,
  numbered after the inherited count `n`:

  | Pair | `GIT_CONFIG_KEY_<pair>` | `GIT_CONFIG_VALUE_<pair>` |
  |---|---|---|
  | `n+3i` | `credential.<url>.helper` | empty: clears every helper git has collected for the URL, the user's URL-scoped ones included |
  | `n+3i+1` | `credential.<url>.helper` | `!'<helper>' --vc-credential` (the helper's full path, `\` as `/`, each `'` as `'\''`), which git runs through `sh` with `get`, `store` or `erase` appended: the only helper asked |
  | `n+3i+2` | `http.<url>.proactiveAuth` | `basic` (D340): git 2.46 and later ask the helper before the URL's first request and send the token with it; 2.31 to 2.45 ignore the key |

  then `GIT_CONFIG_COUNT` `n+3k` for `k` bindings, `GIT_TRACE_REDACT=1` (ruling 7) and
  `GIT_CONFIG_PARAMETERS` removed (D329). The command's environment is applied last, so the
  removal drops the session's value and the process's. Debug lines give how many of a scope's
  URLs it binds, and say when an inherited `GIT_CONFIG_PARAMETERS` was left out.
- **Why these variables** (measured on 2.56, design 4.2 and 19 item 4). Git reads the pairs after
  every configuration file and before an inherited `GIT_CONFIG_PARAMETERS` and `-c`, so a helper
  listed there, or brought in there by an `include.path`, would be asked after the app's and get its
  `store` and `erase` with the token, and an empty `credential.helper` there would clear the app's;
  git rebuilds the variable from its own `-c` values, so the app's `NetworkOptions.Configuration`,
  which never holds a `credential.*` or `include.*` key, still applies. The empty value clears every
  helper collected for a matching URL; `credential.<url>` keys match the scheme exactly, the host
  without case, `:443` as no port and the path as a prefix ending at `/`, its case counting; a key
  without a user matches every user of the URL, hence `Select`'s drop. A helper's `username=`
  replaces the URL's, so the requests go as `x-access-token`. Git always lets libcurl read a netrc
  file, and libcurl answers a request's 401 from a `.netrc` or `_netrc` entry for the host before
  git asks any helper: without `proactiveAuth` (git 2.31 to 2.45) a bound call's first request goes
  without credentials and a netrc entry answers its 401; with it (2.46 and later) git runs `get`
  first, every request carries the token, a refused token gets one 401 and git's `erase` with no
  netrc retry, and when the helper has nothing git asks for a user name before any request. From
  2.46 on a bound call to a public repository therefore sends the token too, and a revoked token
  fails it where it used to succeed anonymously. With `GIT_TRACE_CURL` or `GIT_CURL_VERBOSE` git
  prints `Authorization: Basic <redacted>`; an inherited `GIT_TRACE_REDACT=0`, which the scope's
  `GIT_TRACE_REDACT=1` overrides, would put the token's base64 in the standard error that the call
  record keeps. An `http.<url>.extraHeader` with an `Authorization` header (left in `.git/config`
  by actions/checkout) replaces the helper's credential, and git then erases a valid token; a
  redirect to another host does the same.
- **Answering git** (`AskPassBroker.AnswerCredentialAsync`; D273, D275). A line that
  `AskPassProtocol.ParseCredentialRequest` reads is answered without UI on the connection's own
  task: nothing unless the secret matches (compared in fixed time; a wrong one is a Warning) and the
  scope is open with used bindings. The binding is the first whose `scheme://host[:port]` (lower
  case, the scheme's default port dropped) equals the request's `protocol` and `host`; with none,
  or with a `username` other than none, the login (without case) or `x-access-token`
  (`AskPassBroker.IsAccountUser`, the one rule for the broker and the prompt group), git gets
  nothing and asks through the dialog (`Ask`) or fails (`Deny`, D275). A `get` for an account git
  refused in this scope gets nothing; otherwise the source's `GetAsync(account)`, cancelled when the
  scope closes: the session waits at most 10 s for its start's keychain read and answers only while
  that account binds git. A token holding CR, LF or NUL is not given (a Warning); the SHA-256 of a
  token given is kept in the scope's handed-out set. An `erase` whose hash is in that set marks the
  account refused in the scope, once: `PromptScope.RefusedAccount`, the source's `Refused(account)`
  and the Info line "{repo}: {title}: {host} refused the account's token."; any other erase marks
  nothing, so a token replaced during an operation is never marked by the old one. Debug lines name
  each outcome ("answered from the account", "has no token", "is not bound", "names another user"),
  never a URL's path, the token or its hash. An answer holds a connection at most as long as the
  source waits (10 s), so `MaxConnections`' 16 stay enough.
- **Prompts in a bound scope** (`Scope.AskAsync`). A `Deny` scope's prompt is refused at once,
  without the handler. A `Username` or `Password` prompt whose URL's origin is a binding's and whose
  user is the account's (by `IsAccountUser`) carries the account (`GitPrompt.Account`), by which a
  prompt group keeps its answers (above); once git has refused the account in the scope, such a
  prompt is refused without a dialog, and the error dialog follows. No prompt is ever answered from
  the account (D277).
- **A refusal** (ruling 4, D276, D330). The queue copies a failed operation's scope's
  `RefusedAccount` into its result (`OperationResult.RefusedAccount`), and
  `RepositoryOperations.ShowResultAsync` and a bulk row's `DeferredOf` show it before any other
  description (`RefusedDisplay`): the operation's error title,
  `OperationTexts.GitCredentialsRefused(login)` ("GitHub refused git's credentials for {login}. If
  the token has expired or was revoked, sign in to GitHub again in Settings, Accounts.", with the
  signed-in login in its own case when its key is the refused one), git's output, and "Open
  settings…", which opens the settings screen on Accounts, when the window can open it; auto-fetch
  shows nothing new, and the create form's error line says the same for its push (see "Pull
  requests"). The text names git's credentials, not the token, since a stale `extraHeader` or a
  redirect makes git erase a valid token: the session's `Refused` checks the token with one `GET
  /user` and marks the account "needs sign-in" only on its 401 (D330). The API's own 401 is certain
  and says `OperationTexts.TokenRefused(login)`, "GitHub refused the token of {login}. Sign in to
  GitHub in Settings, Accounts." (both in `OperationTexts.Hosting.Credentials.cs`).
- **Limits** (D275, D277; plan risks 35 and 39). Git older than 2.31 ignores `GIT_CONFIG_COUNT`, so
  no binding is used there and the token never reaches git; the Accounts section says that the
  option needs Git 2.31 (see "The settings screen"). Answering git's prompts from the account there
  (ruling 3's fallback) is not built, since after an answer that succeeds git sends it to every
  helper of the list with `store`, which would put the token in Git Credential Manager or a plain
  file. On 2.31 to 2.45 a netrc entry for github.com answers a bound call's first 401 before the
  app's helper, and git-lfs's own HTTP client reads neither the key nor `NETRC` and asks HOME's
  netrc before any helper on every version (risk 35): design 4.1's "and from nothing else" holds for
  git's own requests from 2.46 on (D340). When the session refuses the account through the API, or
  signs out, after an operation bound its calls and before git's credential request, the helper has
  nothing, git asks for a user name and the app shows git's "Sign in to {host}" dialog in a bound
  tab, since no erase put the account in the scope's refused set (risk 39; the user can cancel it).
  A user whose configuration names another login for the host (`credential.<url>.username`) gets
  git's dialog in bound repositories until the switch is turned off (D275).
- **Tests**: `CredentialHelperTests` (the helper's mode through `AskPassRunner.RunCredentialAsync`),
  `AskPassBrokerBindingTests` (the variables), `AskPassChannelTests` (a `get`, an `erase` and a
  prompt queued behind a credential answer), `AccountCredentialTests` (fetch, push and clone
  against `AuthRemoteServer` with the token: a revoked token, an unbound URL, a `Deny` scope, an
  inherited `GIT_CONFIG_COUNT` pair, an inherited `GIT_CONFIG_PARAMETERS` or `include.path` helper,
  curl's trace, what git older than 2.31 sees, a netrc entry from 2.46 on, a stale `Authorization`
  header; the user's `store` helper's file never holds the token), `CredentialBindingSelectTests`,
  `UrlRewritesTests`, `UrlRewritesParityTests` (Git); `HostingSessionCredentialTests` (Hosting);
  `AutoFetchCredentialTests`, `DashboardCredentialTests`, `RefusedTokenDisplayTests`,
  `PromptGroupAccountTests`, `BulkRunForgetAnswersTests` (App, through `HostingSessionStub` and
  `RecordingCredentialSource`); part A pictures the refused credentials' dialog, part B a clone
  through the helper (see "Tests").

### Context menus (D86, D92, D105, D106, D114, D131, D142)

- **Menus are data.** `ViewModels/Menus/MenuModel` (no Avalonia types): `MenuHeader(Text,
  ToolTip)`, `MenuSeparator`, `MenuItem(Label, Invoke, IsEnabled, DisabledReason, IsDestructive)`.
  `RepositoryMenus` builds every menu as a pure function of a `RepositoryMenuContext` (an
  immutable snapshot taken when the menu opens: refs, status with what is in progress, the
  selected commits as `CommitMenuInfo` with `IsReachableFromHead`, the toolbar's states, whether
  an operation runs, the other worktrees, a drop's `DropFacts`) and an `IRepositoryActions`:
  `ForCommit`, `ForCommits`, `ForStash`, `ForWorkingChanges`, `ForLocalBranch`, `ForRemoteBranch`,
  `ForRemote`, `ForTag`, `ForSection` (null for "Pull requests" without a GitHub remote),
  `ForUnstagedFile`, `ForStagedFile`, `ForCommitFile`, `ForDiff`, `ForEmptyGraphArea`, `ForDrop`.
  Items act on the full ref names and SHAs captured when the menu opened, never on rows. Items for
  later phases are left out, not disabled (D77, D92); folder rows, repository tabs and, without a
  GitHub remote, "Pull requests" have no menu. `Build(header, groups…)` never leaves a separator
  after the header, at the end or two in a
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
- **Phase 5's items** (design 13.1; built in the partial files `RepositoryMenus.Pages.cs`,
  `.Filters.cs` and `.ActivityLog.cs`): the file menus (`ForUnstagedFile`, `ForStagedFile`,
  `ForCommitFile`, `ForDiff`) gain a group "File history", "Blame" before "Open file" (disabled
  "This file is in no commit yet" or "Make the first commit first"; the resolver's header menu
  leaves them out); a local branch's menu a group before "Copy branch name": "Compare {b} with
  {cur}" (not on the current branch), "Show reflog of {b}", "Hide {b}" or "Show {b}" (disabled on
  the checked-out branch: "The checked-out branch is always shown"), "Solo {b}" or "Stop soloing
  {b}"; a remote branch the same without the reflog; a tag "Compare {t} with {cur}"; a remote
  "Hide every branch of {r}" or "Show every branch of {r}"; the Local branches section "Show
  reflog", "Show backups", "Show all hidden branches" (disabled "No branch is hidden") and "Stop
  soloing" (disabled "No branch is soloed"), the Remotes section the last two; the
  working-changes row and the empty graph area "Show reflog" and "Show backups"; a drop "Compare
  {A} with {B}" in a group of its own. New builders: `ForActivityLogRow` ("Copy command", "Copy
  output", "Copy title"), `ForFileHistoryRow` ("Show in graph", "Blame at this commit", "Copy
  SHA"), `ForBlameRun` ("Show in graph", "Blame before this commit", "File history", "Copy SHA"),
  `ForReflogEntry`, `ForBackup` (the backup's restore by kind, then "Create branch here…", "Check
  out this commit", "Copy SHA"), `ForReflogSource` (the reflog page's source menu: HEAD, the local
  branches, Backups) and `ForCompareFile` ("File history", "Blame", then "Open file", "Show in
  folder", "Copy path"); the search bar's mode menu is the search's own. Two selected commits
  compare without a menu (D162). "Show in graph" is disabled "This commit is not in the graph" for
  a commit the graph lacks. The taller menus moved phase 3's and phase 4's pictures and heights (in
  phase 3's "ops" scenario `feature/alpha`'s menu is 571 high, 458 before phase 5, and the
  checked-out `main`'s 441).
- **Phase 6's items and keys** (design 6.7, 10.4, 12.1; D199, D201, D215, D222, D226). An item that
  runs a keyed command on the same object carries the command (`MenuItem.Command`), and one whose
  key its own view handles carries the key's text (`MenuItem.KeyText`: the dashboard row's Enter,
  Alt+Up, Alt+Down and Delete); a key on an item acting on another object would mislead, so no
  other item shows one. Every file menu gains "Open in editor" after "Open file" while an editor is
  chosen (`RepositoryMenuContext.HasEditor`, from the settings with no disk access; disabled for a
  file not in the working tree). A remote branch's "Check out {T}" and "Delete {y} on {r}…" and a
  drop's two pushes follow the fetch lines: disabled with R1 to R4 (`OperationTexts.Menus.Refspecs.cs`:
  "{T} is fetched from {remotes}", "{T} is fetched from more than one ref of {r}", "{T} is not
  fetched from any remote", "{T} is {ref} on {r}, not a branch"), R2's checkout enabled (D201); a
  local branch's Push and Force push are disabled with R4 when its upstream is no branch on its
  remote; "Set upstream…" lists only remote branches mapped from a branch; a remote branch's or
  tag's backup whose name more than one configured remote fits has its "Push {b} back to {r}…"
  disabled with `RemoteOfBackupAmbiguous`. `DropFacts` is
  `DropFacts(TargetIsAncestorOfSource)`: phase 5's `RemoteMapsBranchesDifferently` and its reason
  went (design 10.7). New builders: `DashboardMenus.ForRow` and `ForHeader` (see "The dashboard
  tab") and `WorkspaceMenus.ForWelcomeItem` (Open, Rename workspace…, Delete workspace…).
- **Phase 7a's items** (D306, D307; `RepositoryMenus.Hosting.cs`, pure functions of the context,
  D86). `RepositoryMenuContext.Hosting` is the repository's `HostingState`, null without a GitHub
  remote, when every new builder and hook gives null and every earlier menu is as it was.
  `ForPullRequest(context, number, actions, shown)`: the header `#1 Add the login form` (cut by the
  view, whole in its tooltip), then "Open in browser" and "Check out {head}" (the checkout plan's
  label, disabled with its reason: already checked out, or "Add a remote for {owner/name} to check
  out this pull request"), then "Copy link"; the pull request is the listed one, else the row's own
  (the rows a failed refresh keeps), else the header `#n` alone. `ForSection` gives "Pull requests"
  a menu (D92 amended): signed out "Sign in to GitHub…" and "Open pull requests in browser", which
  asks GitHub nothing (D257); otherwise "Refresh pull requests" (disabled "Pull requests are loading"
  while they load) and "Open pull requests in browser". A branch's item is "Open pull request #n"
  when `ByRef` holds the branch (a local branch through an upstream of the same name, D313), else
  "Create pull request…", never disabled (signed out, the create signs in first): in
  `ForLocalBranch`'s push group after Push and Force push, and in a group of its own after
  `ForRemoteBranch`'s delete group, left out for a remote branch of a remote GitHub does not have;
  graph labels share these builders. A drop's is in a group of its own between the rebase items and
  "Compare {A} with {B}", in all three shapes of `ForDrop`, only onto a GitHub remote's branch or
  onto a local branch whose upstream is a GitHub remote's branch of the same name: "Open pull
  request #n" when the source has one, else "Create pull request…" from the source into the target.
  `RepositoryViewModel.OpenRefRowMenu` opens `ForPullRequest` for a `PullRequestRow`; a right-click
  there, as on every left-panel row, selects nothing (D114). See "Pull requests" for what the items
  do.
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
  cell). Since phase 6 a menu opened by a button (`MenuOpenedBy.Button`: the dashboard's
  `DashboardMore`) has its top-left at the button's bottom-left corner and is only moved to stay 4
  inside, never flipped.
- **Look** (`Theme/Menus.axaml`, styles on the stock controls, class `vcContextMenu`):
  `VcPanelBackgroundBrush`, a 1 px `VcBorderBrush` border, corner radius 6, `VcMenuShadow`; 200 to
  520 wide, as wide as the longest label needs (a longer label is cut with "…" by
  `Controls/TrimmedTextBlock`, its whole text as its tooltip); items 26 high with 10 px side
  padding, the header 24 high in `VcTextSecondaryBrush` (cut with "…" past 340, never
  highlighted), separators 9 high; the highlighted item `VcHoverBackgroundBrush`; a destructive
  item in `VcDangerBrush` while enabled; a disabled item in `VcTextDisabledBrush`. A menu that fits
  is 8 + 24 + 26 per item + 9 per separator high (`WindowLayout.MenuHeight`). Since phase 6 a keyed
  item's header is the app's own (`MenuLayer`): the label, then at least 24 to its right and
  right-aligned at the item's edge the key's text in the platform's words (class `vcMenuKey`,
  font size 12.5, `VcTextSecondaryBrush`, `VcTextDisabledBrush` while disabled), never cut, a long label
  cut instead; Avalonia's own gesture text stays hidden (D214, D215).
- **Shift+F10 (D106).** Avalonia raises the context request on key-up for every gesture of the
  platform's `HotkeyConfiguration.OpenContextMenu` (`Apps` headless, `Apps, Shift+F10` on Win32).
  `MainWindow`'s tunnelling handler raises it itself, on key-down, on the focused element, only when
  that list lacks Shift+F10 (`ContextMenuKeys.AppRaisesShiftF10`), so each press opens one menu on
  every platform.
- **Tooltips on the overlay layer, app-wide.** `App`'s static constructor sets
  `ToolTip.ShouldUseOverlayLayer` on every control whose tooltip opens (unless it sets its own), so
  every tooltip, a disabled item's reason included, is drawn inside the window and in the
  real-window pass's capture (D105). Since phase 5 tooltips take no input (D182: `ToolTip {
  IsHitTestVisible: False }` in `Theme/Controls.axaml`): drawn there, a tooltip slides back inside
  the window at its edge, and a status bar control's lay over the pointer, so a click on Cancel
  after resting on it only closed the tooltip. A press now reaches the control beneath, which
  keeps its hover look.

### Dialogs (D69, D104, D114)

- **The stack (D104).** `DialogHostViewModel` keeps the open dialogs in `Open` (the bottom one
  first) and the top one in `Current`; `ShowAsync(dialog)` puts a dialog on top and completes with
  its answer; `ConfirmAsync(ConfirmationRequest)` is phase 2's confirmation, unchanged;
  `CancelCurrent()` (Esc) goes to the top dialog only (`DialogViewModel.HandleEscape`: it cancels,
  or goes back a step, as the Remotes form does). The window's `DialogLayer` is an `ItemsControl`
  over `Open` with a `Panel` as its items panel, so every open dialog is drawn, each over a
  backdrop of its own; the one below takes no input. `MainWindow` keeps the element that had the
  focus when each dialog opened and gives it back when the dialog above closes; when the last one
  closes, its view is hidden before it leaves the window. Since phase 6 `CancelAll()` (the session's
  end, D234) cancels every open dialog, the top one first, past what `HandleEscape` would refuse,
  so that each dialog's own token stops its work and whoever awaits a dialog gets its cancelled
  answer.
- **Views by type.** `DialogViewModel` (title, `RepositoryName` for the "In &lt;repo&gt;" line
  `DialogRepository`, `CancelCommand`, a completion task) and `DialogViewModel<TResult>`.
  `Views/Dialogs/DialogViewLocator` finds a view registered with `Register<TViewModel>` or by name
  (`ViewModels.X.FooViewModel` → `Views.X.FooView`, base types included); no dialog is registered
  in phase 3. Since phase 5 the locator also finds the views of the graph area's pages (by name:
  `ViewModels.Pages.FileHistoryViewModel` → `Views.Pages.FileHistoryView`) and of the comparison,
  and `RepositoryGraphView` registers `SearchBarView` for `SearchViewModel`.
  `Views/Dialogs/DialogView` is every dialog's root: on opening the focus goes to the first text
  box (or the control the dialog names: the confirmation's Cancel; since phase 6, with
  `FocusesTextBoxOnOpening` false, the settings screen's section list), Enter runs
  `ConfirmButton` unless the focus is in a multi-line box or on a button (since phase 6 Mod+Enter
  runs it from a multi-line box, D215), and its automation peer
  makes the root a control element (a pane named by the title). `DialogRow` is a list row with
  buttons of its own (the Remotes dialog's `RemoteRow`), a list item to UI Automation. Every
  dialog's buttons are `CancelButton` and `ConfirmButton` (`VcPrimaryButton`, or `VcDangerButton`
  for a destructive one). `Theme/Dialogs.axaml` holds the look: a card 400 wide at most (460, class
  `wide`, for the Remotes and Pick dialogs, and since phase 7a "Clone from GitHub" and "Create pull
  request"), the title, text, error, box, button row and code
  classes, and a radio button with a second line; since phase 6 also the settings screen's large
  card (class `settings`, at most 860 × 680) with its row classes, a warning under a box
  (`dialogWarning`), and the palette's backdrop and card.

| Dialog (automation id) | View model / view | What it is |
|---|---|---|
| `ConfirmationDialog` | `ConfirmationViewModel` / `ConfirmationView` | Every confirmation (phase 2's, the backups' "A backup is saved first, so this can be undone.", "ssh asks") |
| `NameDialog` | `BranchNameDialogViewModel`, `TagDialogViewModel`, `StashDialogViewModel` (base `NameDialogViewModel`) / `NameDialogView` | "Create branch" or "Rename branch" (context line, `NameBox`, `NameError` checked as typed by `DialogText` and `RefNameRules`, shown only once the box has been edited, D114; "Check out the new branch"), "Create tag" (name and an optional message, which makes an annotated tag), "Stash changes" (message, "Include untracked files", which starts with `AppSettings.StashIncludeUntracked`) |
| `ResetDialog` | `ResetDialogViewModel` / `ResetDialogView` | "Reset main to a1b2c3d?": Soft, Mixed (checked) or Hard, each with its line; the button "Reset" in the danger style while Hard is chosen; the only confirmation of a reset |
| `PickDialog` | `PickDialogViewModel` / `PickDialogView` | "Set upstream for &lt;b&gt;": a filter and the remote branches; since phase 6 an item may have a detail drawn after it in the secondary colour, which the filter matches too (`PickOption`: the settings screen's "Choose an editor" and "Choose a merge tool", stacked over it); since phase 7a also "Choose the branch to merge into", stacked over the create form, each base with its repository as detail (see "Pull requests") |
| `ChoiceDialog` | `ChoiceDialogViewModel` / `ChoiceDialogView` | A choice of remote ("Push v1.0 to…", "Delete v1.0 on…") |
| `PushDialog` | `PushDialogViewModel` / `PushDialogView` | "Push &lt;b&gt;" for a branch without an upstream: the remote, the branch's name there, "Set as upstream" |
| `RemotesDialog` | `RemotesDialogViewModel` / `RemotesDialogView` | The remotes (`RemoteRow`) with their shown URLs (`RemoteUrl`, credentials hidden, D103); Edit and Add switch the card to a form whose Save waits for its operation (git's refusal in `RemoteError`); "Fetch now" queues a fetch it does not wait for; Remove opens its confirmation on top |
| `ErrorDialog` | `ErrorDialogViewModel` / `ErrorDialogView` | `ErrorTitle`, `ErrorSummary`, `ErrorRepository` ("In &lt;repo&gt;", always; since phase 6 a workspace's errors name the workspace, and a write of git's global settings, which belongs to no repository, has no such line), `ErrorOutput` (git's cleaned output in the code font), `ErrorCommand`, `ErrorCopyButton` ("Copy output"), the optional `ErrorExtraButton`, "Close". Answers true when the extra button was clicked. A dialog without git output leaves out the output, the command and "Copy output" (D114) |
| `FinishingDialog` | `FinishingDialogViewModel` / `FinishingDialogView` | "Finishing operations": `FinishingItem` lines, "Keep open" and "Quit now" |
| `MessageDialog` | `MessageDialogViewModel` (`DialogViewModel<string?>`) / `MessageDialogView` | Phase 4's message dialog (the `wide` card): "Reword {short}", "Message of the squashed commit" or "Squash {N} commits"; `MessageSummary` with `MessageSummaryCounter` (what is left of 72), `MessageDescription` (wrapping, accepting Return); "Save" or "Squash", disabled while the summary is blank ("Write a summary"). The message is split as the stage panel splits it and joined with a blank line between summary and description (the summary alone when the description is blank) |
| `CredentialsDialog`, `PassphraseDialog`, `HostKeyDialog`, `PromptDialog` | `CredentialsViewModel`, `PassphraseViewModel`, `HostKeyViewModel`, `PromptViewModel` (base `PromptDialogViewModel`) / `CredentialsView`, `PassphraseView`, `HostKeyView`, `PromptView` | git's and ssh's prompts (see "Prompts and credentials") |
| `NameDialog` (phase 6) | `WorkspaceNameDialogViewModel` (a `NameDialogViewModel`) / `NameDialogView` | "New workspace" and "Rename workspace" (`WorkspaceNameError`: blank, over 100 characters, a control character, a name another workspace has ignoring case) |
| `AddRepositoriesDialog` (phase 6) | `AddRepositoriesDialogViewModel` / `AddRepositoriesDialogView` | "Add repositories to {workspace}": the folder scan's progress, then the checklist of what it found (`FoundList`, `FoundRepository`), see "Workspaces" |
| `SettingsDialog` (phase 6) | `SettingsDialogViewModel` / `SettingsDialogView` | The settings screen, see "The settings screen" |
| `CommandPalette` (phase 6) | `CommandPaletteViewModel` / `CommandPaletteView` | The command palette, see "The command palette" |
| `SignInDialog` (phase 7a) | `SignInDialogViewModel` (`DialogViewModel<AccountStatus?>`) / `SignInDialogView` | "Add GitHub account": a personal access token, masked, checked with GitHub; see "The settings screen" |
| `CloneFromGitHubDialog` (phase 7a) | `CloneFromGitHubDialogViewModel` (`DialogViewModel<string?>`) / `CloneFromGitHubDialogView` | "Clone from GitHub": the account's repositories, a filter and "Choose"; see "Clone from GitHub" |
| `CreatePullRequestDialog` (phase 7a) | `CreatePullRequestDialogViewModel` (`DialogViewModel<bool>`) / `CreatePullRequestDialogView` | "Create pull request": From, Into, the title, the description, Draft and the error line; see "Pull requests" |

Each title has an automation id of its own (`ConfirmTitle`, `NameDialogTitle`, `ResetTitle`,
`PickTitle`, `ChoiceTitle`, `PushTitle`, `RemotesTitle`, `ErrorTitle`, `FinishingTitle`,
`MessageDialogTitle`, and `DialogTitle` for the prompt dialogs), so both harnesses find the same
elements. Since phase 4 a `ConfirmationRequest` has `CancelText` ("Cancel", or "Keep editing" for
the questions about leaving changed work), and a confirmation without a question hides its
question line (the list's "Discard the changes to this list?"). Phase 4's questions (design 7.2):
Skip ("Skip {short}?"), leaving a changed list, staging or marking resolved with markers left,
Continue with markers staged, leaving the resolver with a modified result, the merge tool over a
modified result, Mark resolved over a file changed on disk. Phase 5's: an undo or redo that
changes a remote, and the backups list's "Push {b} back to {r}…" ("Push {b} back to {r}?", button
"Push"), not in the destructive style since they restore (see "Undo and redo"); the backups
list's "Restore the discarded files…" and "Restore the changes…" ask D75's "Restore discarded
changes?" when a file would be overwritten. Nothing else of phase 5 asks: Undo, Redo and the
automatic stash change only what can be put back, and refuse rather than overwrite. Phase 6's one
question is "Delete workspace?" (destructive, "Delete"; "Its dashboard tab closes." while it is
open); removing a repository from a workspace, the bulk runs and the settings screen's Save ask
nothing. Phase 6's titles are `AddRepositoriesTitle` and `SettingsTitle`. Phase 7a's one question
is "Sign out of GitHub?" (button "Sign out", primary, not destructive: nothing in a repository is
lost, D296); the create form shows its failures on its own line, not in the error dialog (D312).
Phase 7a's titles are `CloneFromGitHubTitle` and `CreatePullRequestDialogTitle`; "Add GitHub
account"'s is `DialogTitle`, as the prompt dialogs'.

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
source, its rebase items in a group of their own (see "Context menus"), and since phase 5
  "Compare {A} with {B}" in a group of its own at the end. Release elsewhere, Esc or losing the
  pointer cancels. `PressFollowsDrag` keeps the press after a drag from counting as a
  double-click. Since phase 5 a press on a control marked `RefDrag.IsExcluded` (the left panel's
  eye and solo buttons) never arms a drag.

### The graph area: the list, the resolver and the pages (D140, D141, D148, D176)

Phase 4 puts two more things in the graph area besides the graph and the diff, and phase 5 three
pages (below). All belong to the `RepositoryViewModel`, like the diff, so a tab switch keeps them
(one view serves every tab).

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
- **The pages** (phase 5, T1, T2, T3, design 11, D158, D176). `RepositoryViewModel.Page` is an
  `IGraphAreaPage` (`Title`, which names its pane, `Close()`, `RequestFocus()` with
  `FocusRequested`, `OnRefsRead(refs)`, `Dispose()`), one of `FileHistoryViewModel`,
  `BlameViewModel` and `ReflogViewModel` (`ViewModels/Pages`), on the base `GraphAreaPageViewModel`
  (the host, a lifetime that cancels every read of the page when it or its tab closes). A page
  holds no work, so leaving it never asks; at most one is open, and a page and the interactive
  list exclude each other. `ShowsPage`: a page, no diff, no resolver; `ShowsGraph` is false while
  a page is open. Opening one (`RepositoryOperations.Pages.cs`, then `OpenPageAsync`) goes through
  `TryLeaveGraphAreaAsync`, then `LeaveGraphArea` (which closes a page too), then sets `Page`; a
  page opened from a page (Blame from the history) replaces it, and `OpenResolverAsync` closes it.
  A page reads outside the queue (D47) through its host (`IGraphAreaPageHost`, the repository:
  the repository's reads, settings, dates, log, the menus' actions and context, `HasCommit`,
  `IsOnHead`, `ShowCommit`, `ShowInGraph`, `Close`). Opening a page does not change the right
  panel; the user's first click or key on a row does, through `ShowCommit(sha)` (11.3): it leaves
  the working-changes row (the stage panel's draft stays), selects the commit behind the page when
  the graph has it, without closing a diff, and otherwise clears the graph's selection and shows the
  commit's details directly, kept as the one shown; the zero SHA of a line not committed yet
  selects the working-changes row. `ShowInGraph(sha)` closes the page, selects the row and reveals
  it ("Show in graph", a double-click). A diff from the right panel opens over a page and gives it
  the focus back when it closes; a diff from the stage panel leaves the page first. Esc
  (`EscapeOrder.Page`, 370, one rule for the pages, `PageEscape`) closes the page unless a diff is
  over it or a text box has the focus; the graph takes the focus, and the row the user picked on
  the page is revealed once the graph shows again.
  - **File history** (`FileHistoryView`, automation id `FileHistory`; design 4.1, D175): the commits
    that touched a file, newest first, following renames, from a start resolved to a full SHA when
    the page opens and fixed from then on (HEAD for a file of the stage lists, the commit for a
    commit's file, a stash's base for a stash's file). Rows reach the list in batches, one UI job
    for whatever came since the last (one job per row kept the window from taking input on a file
    with thousands of commits). Header 36: `IconHistory`, "History of {name}" (`FileHistoryTitle`),
    the folder, "from {short}" when the start is not HEAD, "Blame" (`FileHistoryBlame`) and the
    close button. Rows 26 (`FileHistoryRow`, named "{short} {subject}"): the status letter, the
    short SHA, the subject, "renamed from {old}", "copied from {path}", "added", "deleted" or "as
    {path}", the author and the date, which go first when the width runs short. The selected row's
    change shows in the hosted diff (`FileHistoryDiff`, see "Diff view"): beside the list when the
    page is at least 760 wide (`SideBySideWidth`; the list 45 %), under it otherwise (the list 40 %
    of the height, at least 3 rows). The first row, selected when it arrives, fills only the diff.
    "Loading…" until the first row, "No commit has touched {name} yet", or git's failure.
  - **Blame** (`BlameView`, `Blame`; design 4.2): the file at a commit or in the working tree, each
    line with the commit that last changed it, grouped in runs of consecutive lines of one commit
    (`BlameRun`). Header 36: `IconBlame`, "Blame of {name}" (`BlameTitle`), its caption ("at {short}
    {subject}", "in the working tree", "at HEAD", "before {short}"; `BlameCaption`), "File history"
    (`BlameHistory`) and the close button; title and caption are `TrimmedTextBlock`s, and at
    1100×700 a long subject in the caption is cut with "…" (found by phase 5's gate). The text
    (`BlameText`, `Controls/Blame/BlameTextEditor` on `CodeTextEditor`): read-only, the code font at
    12, the diff's syntax colours (a file over 1,000 lines coloured on a background thread, as the
    diff is; none for a file over D65's limit), `CodeNumberMargin`, no caret, a Document to UI
    Automation. The gutter (`Controls/Blame/BlameGutter`, a control element of its own) left of
    the numbers is 200 wide on a page at least 700 wide and 130 otherwise. A run's first line shows
    the short SHA, the author (in the wide gutter only; the narrow one has it in the tooltip) and
    the date (`yyyy-MM-dd`), "Not committed yet" in `VcWarningBrush` for such lines;
    runs alternate between `VcPanelBackgroundBrush` and the window's background, the picked run's
    lines `VcSelectionBrush`; a line's run is found by a binary search over the runs. A click on a
    run, in the gutter or on its text, shows its commit (`ShowCommit`), a double-click shows it in
    the graph. "Loading…" until git ends; "{name} is a binary file: it has no lines to blame", "{name}
    is a submodule: it has no lines to blame", "{name} is empty", git's failure, or the sentence for
    a `blame.ignoreRevsFile` that names a missing file.
  - **Reflog and backups** (`ReflogView`, `Reflog`; design 5, D180): one reflog at a time (HEAD's
    or a local branch's), or the backups list, chosen with the header's source button
    (`ReflogSource`, menu `ForReflogSource`). A reflog's rows 26 (`ReflogRow`, named by the
    selector): the selector, the entry's own time, git's message, the commit's short SHA and
    subject, and for a lost commit `IconAlert` (`ReflogLostMark`, "No branch, tag or remote branch
    reaches this commit"), filled in after the list shows (`FindLostCommitsAsync`; a read again for
    the same refs and commits takes the last walk's marks). At most 500 entries are read (the
    footer `ReflogFooter` then says "Only the newest 500 entries are shown"), and the numbers git
    skipped are counted there ("1 entry is not shown: it names no commit that exists"). The page
    reads again on every refresh while it is open (some entries move no ref) and never while it is
    closed; a newer read cancels the one it replaces; a read again keeps the rows it did not change,
    with their marks, the selection and the focus. The backups list's rows (`BackupRow`, named by the
    backup's name): the time from the name in `DateDisplay`'s zone (a column 136 wide), the backup
    in words by kind ("Deleted branch {b}", "Before a hard reset to {short}", "Dropped or popped
    stash "{subject}"", …), the commit it keeps; a force push's backup has its lease read when the
    list loads. The rows' menus are `ForReflogEntry` and `ForBackup` (the restores: see "Undo and
    redo").
  - Tests: `FileHistoryTests`, `BlameTests`, `BlameParserTests`, `ReflogTests`, `BackupReaderTests`
    (Git); `FileHistoryViewModelTests`, `BlameViewModelTests`, `BlameViewTests`,
    `ReflogViewModelTests`, `KeptCommitGoneTests`, `GraphAreaHooksTests` (App); checks B5 to B9.

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
  a local branch", and since phase 6 an upstream that is no branch on its remote (R4,
  `UpstreamNotABranchReason`: "upstream/pr/7 is refs/pull/7/head on upstream, not a branch");
  Branch "Make the first commit first"; Stash (`StashReason`, shared with every
  "Stash changes…" item and the stash operation, D119): unborn, an operation in progress, conflicts
  without one ("Resolve the conflicts first"), "There are no changes to stash"; Pop "There are no
  stashes". A button stays enabled while its operation runs: a second click queues another run, or
  none by the operation's dedupe key. Since phase 5 Undo, Redo and Search are real (design 1.1):
  `UndoCommand`, `RedoCommand` with `UndoToolTip` and `RedoToolTip` from
  `RepositoryOperations.ReadUndoAvailability` (read again on `UpdateUndoStates` and on every
  `Changed` of the repository's queue; "Nothing to undo" and "Nothing to redo" while they give no
  tooltip; see "Undo and redo"), and `SearchCommand` with `SearchToolTip` ("Search commits
  (Ctrl+F)", Cmd+F on macOS from the platform's command modifier), enabled while the tab has a
  repository and neither the interactive list ("Close the interactive rebase first") nor the
  resolver ("Close the conflict resolver first") is open. They break phase 3's rule that an
  enabled toolbar button has no tooltip, on purpose: what Undo and Redo would act on is seen
  nowhere else. Their tooltips are also their automation help text, which the real-window pass
  reads. An empty tab keeps every button but Theme disabled.
- **The toolbar since phase 6** (D197, D199, D208, D215; design 4.10, 6.8, 7.2). `SettingsButton`
  ("Settings", `IconSettings`) at the start of the right group, left of Search, never disabled
  ("Open settings ({key})"). Every enabled button whose command has a key says `{what it does}
  ({key})` ("Fetch all remotes (Ctrl+Shift+F)"), a disabled one its reason alone, and its help text
  is its tooltip; the reasons are properties of their own (`FetchDisabledReason` …
  `SearchDisabledReason`), which `ToolbarMenuState.Of` reads for the menus, so phase 3's rule that an
  enabled button has no tooltip is gone. The buttons' `AcceleratorKey` is the key's text
  (`MainWindowViewModel.Keys`, a `ShellKeys`). A dashboard tab's toolbar is
  `ToolbarViewModel.ForDashboard(IDashboardToolbar)`, a static factory over a private constructor:
  Fetch and Pull run Fetch all and Pull all ("Fetch every repository of {workspace} ({key})"),
  disabled with the dashboard's first reason; every other button is disabled as on an empty tab.
- **The window's keys (phase 5, design 1.3, D163).** `MainWindow` handles the platform's
  `HotkeyConfiguration.Undo` (Ctrl+Z; Cmd+Z) and every gesture of its `Redo` (Ctrl+Y and
  Ctrl+Shift+Z; Cmd+Shift+Z) on the bubbling `KeyDown`, so a text box or the resolver that took
  the key first keeps it; they decline while a dialog or a menu is open, a branch drag runs, or
  the focus is in a `TextBox` or anywhere in the conflict resolver. A disabled Undo or Redo takes
  its key and does nothing. The command modifier with F is the toolbar's Search, taken in the
  tunnelling phase as Shift+F10 is (D106), unless a dialog or a menu is open; in the search box it
  selects the box's text. The gestures are read at each press, never from a test of the operating
  system (`WindowKeysTests`). Since phase 6 the window's `KeyRouter` handles both passes (see
  "Keyboard shortcuts"): undo and redo keep these rules on the bubbling pass, and Mod+F is one of
  the fixed set on the tunnelling pass.
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
  indeterminate without a percentage), `OperationCancelButton` ("Cancel", tooltip "Stop git", or
  "Stop git and the merge tool" while the merge tool runs, D160; while `CanCancel`),
  `OperationWaiting` ("1 waiting"). While ssh shows a notice its text replaces the operation text.
  Since phase 5 the status bar's `ActivityLogToggle` ("Activity log", a `VcFlatButton`) is real:
  enabled while the active tab has a repository, class `selected` while the panel shows, its
  tooltip and help text "Show every git command the app has run" or "Hide the activity log" (since
  phase 6 with the key: "… ({key})").
- **The status bar since phase 6** (D206, D208; design 2.6, 2.8, 4.9). Its operation part binds to
  `ActiveTab.OperationStatus`, typed `IStatusBarOperation` (the bound members and `Waiting`), which
  is a repository's `OperationStatusViewModel` or a dashboard's `DashboardBulkViewModel` (the run's
  text, tone and determinate progress, Cancel as Cancel all, "{w} waiting"). On a dashboard tab the
  branch part shows the workspace's name after `IconWorkspace` (`StatusWorkspaceIcon`, shown only
  there). `OperationStatusViewModel` also gives a dashboard row its compact form: `CompactText` (the
  running text with git's percentage, no stage and never ssh's notice, or the result), `CompactTone`,
  `CompactToolTip` (the whole text), `RunningTitle` and `RunningIsCancellable` (for the row menu's
  "Cancel {title}").
- **The status bar's offer** (phase 7a, D317; design 12.2). Core's `OperationOffer(Label, Run)`
  rides on `OperationReport.Offer`, and the queue copies a succeeded report's into
  `OperationResult.Offer` (none for any other outcome); no hosting type reaches Core or the bar.
  `IStatusBarOperation` gains `OfferText`, `HasOffer` and `OfferCommand`. `OperationStatusViewModel`
  keeps a non-background result's offer with its text (none for the stage panel's results and for
  "Auto-fetch failed"), so it lasts as the text does: a background success keeps it, the user's next
  operation clears it, and it is hidden while an operation of the user runs, while "Waiting for
  auto-fetch…" shows and under ssh's notice; `OfferCommand` awaits the offer's `Run`. A dashboard's
  `DashboardBulkViewModel` has none (null, false, a command that never runs), and `CompactText` is
  unchanged. `StatusBarView`'s `OperationOfferButton` (a `VcFlatButton`, its text at 11.5 and its
  automation name the offer's label) lies 6 after `OperationStatus`, before `OperationProgress`;
  hidden, it takes no room, so a status bar without an offer is as before. In 7a only a created pull
  request makes an offer: "Open in browser", which opens it (see "Pull requests").
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
  opens "Auto-fetch cancelled". Tests call `Tick` with chosen times. Since phase 6 a tick is also
  skipped while another queue the registry holds, or a bulk run's slot, fetches from the
  repository's common git folder (`CommonFolderBusy`, which the window gives as
  `BulkSlots.IsCommonFolderBusy`: two fetches of one common folder that overlap lose one of them on
  its remote-tracking refs, D210), and the interval's environment variable is read through
  `ReadEnvironment`, the session's reader; auto-fetch looks only at the active tab's repository, so
  a dashboard tab has none. The settings screen sets the interval (D85's "no screen" ended).

### Activity log (T4, D156, D173)

Phase 5. `ViewModels/Panels/ActivityLogViewModel` (one per window,
`MainWindowViewModel.ActivityLog`; its rows in `ActivityLogRows.cs`) and
`Views/Shell/ActivityLogView` (with `ActivityLogParts.cs`), the panel under the graph area (its
place in `CommitGraphView`: see "Repository tabs").

- **What it reads.** The session's structured records, never the log file: `IOperationLog`
  (`Started`, `Added`, `Updated`, `Evicted`) and `IGitCallLog` (`Added`, `Removed`), from the
  session's start, whether the panel is open or not. Records arrive on any thread and are taken
  over on the UI thread in batches; rows change only where they must.
- **Which worktree.** The active tab's (`Follow(shell)`, `Show(ActivityLogSource)`). An operation
  is a worktree's by its `RepositoryKey`; a read (a call of no operation) by the folder it ran in:
  the worktree's working tree, or the folder its repository was opened from
  (`IGitRepository.OpenedFolder`: the opening's first `rev-parse` runs there, and through a link
  git reports the working tree by another path; found by CI's macOS job, where the temp folder is
  reached through `/var`, a link to `/private/var`). Each worktree (`ActivityLogWorktree`) keeps
  which rows are open and where its list was scrolled, and switching tabs shows the other's at
  once. The rows of a worktree not shown are not kept (they held call records past the call log's
  bounds) and are made again when it shows. Since phase 6 (D208, design 2.9) on a dashboard tab the
  panel follows the selected row's worktree (`DashboardViewModel.SelectedRepository`, the row's own
  view model or a tab's), so it shows every operation on that worktree whichever queue ran it; with
  nothing to follow the panel hides and the toggle is disabled with the dashboard's
  `ActivityLogReason`, the first that holds: "{workspace} has no repositories", "Select a repository
  to see its activity", "Wait for {name} to be read", or the selected row's State tooltip.
- **The panel.** Closed when the app starts. The status bar's toggle opens and closes it; the
  panel's close button (`ActivityLogClose`) and Esc with the focus inside it
  (`EscapeOrder.ActivityLog`, 340: before the resolver, the list and a page) close it. Being open
  is the window's state, kept while a tab without a repository hides the panel. Closing it with the
  focus inside gives the focus to what the graph area shows (the graph, a page, the list, a diff or
  the resolver), never to a banner. Its height is 200 by default and at least 120, and the graph
  area keeps 160 above the 5-high splitter (`ActivityLogViewModel.Fit`): when the window is too low
  for both, the panel gives way first, down to its 30-high header, and it takes its height back
  when the window grows. Letting go of the splitter saves the height in the session
  (`SessionState.ActivityLogHeight`). It spans what the graph area spans, both columns while the
  resolver is open.
- **The rows.** A header 30 high (`ActivityLogHeader`): "Activity log", the repository's name, the
  check box "Show reads" (`ActivityLogShowReads`, unchecked) and the close button. Then a
  virtualised list (`ActivityLogList`) in the order the operations and calls started (their
  sequence numbers, whatever their times say), newest last; it shows its end and stays there while
  rows come, unless the user scrolled up. An operation's row (`ActivityLogOperation`, named by its
  title), 24 high: a chevron, the outcome (`IconCheck` in `VcSuccessBrush`, `IconAlert` in
  `VcWarningBrush` for a warning or a stop with conflicts, `IconClose` in `VcDangerBrush`,
  `IconMinus` for a cancel; "Running…" while it runs), the time (`HH:mm:ss` in `DateDisplay`'s
  zone), the title, the status bar's message for it, the duration. Opened by a click or Right, it
  shows the failure's summary, its notes ("Removed backup reset-… (older than 30 days)"), its git
  calls (`ActivityLogCall`, named by its command, 22 high: the exit code, the command in the code
  font with "(on a temporary index)" where it applies, the duration), each opening to what it was
  given (`update-ref --stdin`'s lines) and printed, in a read-only box (`ActivityLogOutput`) or "No
  output"; or "Its git commands are no longer kept." With "Show reads", the worktree's calls of no
  operation show as rows of their own between the operations, the opening's reads included (D183);
  a read made inside an operation is always one of its calls. A row's menu is `ForActivityLogRow`.
  The log never shows an operation dropped before it started, nor the welcome page's open, init and
  clone, which run before a tab has a repository (the log file has them).
- **Times.** The record clock's (`DisplayedStart`, `DisplayedDuration`). Both harnesses freeze it
  at 2026-01-02 09:00:00 UTC, so every time reads 09:00:00 and every duration 0 ms, and the
  pictures repeat; the sequence numbers keep the order.
- Tests: `ActivityLogViewModelTests`, `ActivityLogViewTests` (App); `OperationLogTests`,
  `OperationLogRecordTests`, `GitCallLogTests`, `GitRunnerRecordTests` (Git); check A2.

### Search (T5, O7, D157, D174)

Phase 5. `ViewModels/Graph/SearchViewModel` (`RepositoryViewModel.Search` while the bar is open),
`Views/Graph/SearchBarView` in `RepositoryGraphView`'s search slot, and its host `ISearchHost`
(the repository, `RepositoryViewModel.Find.cs`).

- **Opening.** The toolbar's Search or its key (`OpenSearch`): the graph shows (a diff or a page
  closes; they hold no work) and the bar opens with the focus in its box, the text selected.
  Nothing while the interactive list or the resolver is open. The mode is kept per repository for
  the session, Commits at first.
- **The bar** (36 high, `VcChromeBackgroundBrush` with a 1-px line along its bottom, pushing the
  column headers and rows down): the mode button `SearchModeButton` (Commits, Files or Changes,
  its menu through the menu layer), the box `SearchBox` (placeholders "Message, author or SHA",
  "File name or path (case ignored for A–Z)", "Text added or removed (case ignored for A–Z)"), the
  count `SearchCount` ("Searching…", "{n} so far…", "No matches", "{k} of {n}", "{n} matches", the
  singular for one; cut with "…", then hidden, when the bar is narrow), `SearchPrevious` and
  `SearchNext` (`VcIconButton`s, disabled without matches) and `SearchClose`. A Changes search
  shows a 2-px progress bar along the bar's bottom (`SearchProgress`, "Searched {k} of {n}
  commits").
- **Running.** The box searches 300 ms (Commits) or 600 ms (Files, Changes) after the last key,
  and at once on Enter when nothing ran for the text yet; a new text or mode cancels the running
  search. Git is given exactly the graph's rows (with the filter applied, so hidden branches are
  not searched), the loaded ones first and the later ones as they come (`SearchAsync`, see "Phase
  5's repository members"); Commits also matches authors and SHA prefixes (4 hex digits or more)
  over the rows in memory. Matches are applied in batches at most every 100 ms. Enter and
  Shift+Enter go to the next and the previous match in row order from the selected row, wrapping
  round; the match is selected as a click selects it and scrolled into view without centring.
  While a search runs they move once git has searched every row between the selection and the
  target, or when it ends. Esc in the bar (`EscapeOrder.Search`, 390) closes it: the marks go, the
  selection stays, the graph takes the focus.
- **Across reloads** (3.4, D174): git's results are kept by SHA for the mode and the text; when a
  refresh swaps the graph the marks and the count stay, authors and SHAs are matched again in
  memory, and git is given only the new graph's rows not searched yet (a pass that gives git the
  old graph's rows ends after its running call). Only a new text or mode starts over; closing the
  tab cancels the search.
- **The marks** go to the graph as `GraphSearchMarks` (see "Commit graph").
- Tests: `SearchTests` (Git); `SearchViewModelTests`, `SearchBarTests`, `CommitGraphControlTests`
  (App); checks F10 and F11.

### Compare (O6, D162, D177)

Phase 5. `ViewModels/Shell/CompareViewModel` and `Views/Shell/CompareView`, in the right panel's
slot (`RepositoryViewModel.MultiSelection`, read as `Comparison`), with its host `ICompareHost`.

- **Two selected commits** (D88's multi-selection, never a stash or the working row) show their
  comparison at once; with three or more the list shows as before. The lower row is From, the
  upper To, and their trees are compared (two-dot). Selecting anything else ends it; a reload that
  maps or drops a multi-selection is not the user selecting.
- **Branches**: "Compare {b} with {cur}" (local and remote branches, and tags) and a drop's
  "Compare {A} with {B}": From is the current branch (or B), To the menu's (or A); by default the
  changes on To since it left From (three-dot, from the merge base), with the switch "All
  differences" (`CompareAllDifferences`) for the two trees; the counts "{a} commits only on {To},
  {b} only on {From}"; a close button (`CompareClose`). Opening one leaves the working-changes row
  (the stage panel's draft stays). It stays until it is closed, a single commit or the working row
  is selected, another comparison opens, or a page shows a commit the graph lacks.
- **Reads.** The names are resolved to SHAs once and kept (`CompareAsync`); each kind of list read
  is kept, so the switch and a swap back read nothing again, and the counts are kept apart from the
  lists. With no common history three-dot is not offered and the switch is not shown ("No common
  history: showing all differences"); several merge bases say "Several merge bases: changes since
  {short}"; the same commit on both sides "Both are {short}: nothing to compare"; a rename search
  `diff.renameLimit` stopped "Renames are shown as removed and added files."
- **The view**: a header 36 high (`CompareTitle`, `CompareSwap`, `CompareClose`), `CompareFrom` and
  `CompareTo`, links that reveal their commit's row without selecting it (disabled "This commit is
  not in the graph"), `CompareCounts`, "Changed files ({N})" with Flat and Tree, and
  `CompareFileList` (phase 1's rows). A click on a file opens its diff in the graph area from the
  side the list was read from (the merge base, or From) to To, labelled `{old short}..{to short}`;
  its menu is `ForCompareFile`, whose Blame of a file the comparison deletes is that side's,
  captioned "before {To}". "Loading…" until the list is read, "No differences" for equal trees.
- Tests: `CompareTests` (Git); `CompareViewModelTests`, `CompareViewTests` (App); checks F12 and
  F13.

### Solo and hide (O8, D159, D178)

Phase 5. The arguments in `Core/Graph/GraphFilter`; what hiding and soloing do to a filter in
`ViewModels/Graph/GraphFilterChanges` (pure); the session's part in `Services/GraphFilterStore`
(`TabServices.GraphFilters`); the repository's in `RepositoryViewModel.Filters.cs`; the actions in
`RepositoryOperations.Filters.cs` (none is an operation of the queue).

- **What.** Local and remote branches (tags stay as they are) and whole remotes. Per repository a
  `RepositoryGraphFilter(RepositoryPath, Hidden, Soloed, HiddenRemotes, Upstreams, Remotes,
  LastUsed)` of full ref names (short names collide across kinds). A ref is never both hidden and
  soloed. A local branch's remote-tracking upstream follows it ("Hide {b}" hides both, "Solo {b}"
  solos both), recorded in `Upstreams` so that "Show {b}" undoes both unless the upstream was shown
  or hidden on its own since; a local upstream never follows, and an upstream two branches track
  stays hidden until both are shown. The checked-out branch cannot be hidden; a hidden branch that
  becomes the checked-out one shows with its label, through `HEAD` on git's command line, and hides
  again when HEAD leaves it. Solo mode while anything is soloed: the soloed refs' histories and
  HEAD's; `Hidden` waits until it ends. A hidden remote hides every branch of it, expanded to its
  branches at each read. The app's own create, rename and delete of a branch, the undo's steps and
  "Restore branch" update the filter inside their operations (`Follow…`): a rename moves its
  entries, a delete removes them, a branch created in solo mode is soloed.
- **What git loads** (8.2). Without a filter D47's revisions, byte for byte (`Unfiltered`), so Q1
  and the real-window pass's waits are unchanged. Hide: `--exclude=<b>` … `--branches`,
  `--exclude=<r>/<b>` … and `--exclude=<r>/HEAD` for every remote with a hidden branch (the symref
  otherwise keeps its target's history), then `--remotes --tags --ignore-missing HEAD --`; never
  `^ref`, which removes shared history, never `*/HEAD` (a branch can be named `feature/HEAD`).
  Solo: `--ignore-missing <full name> … HEAD --`. Past 30,000 characters of arguments
  (`MaxArgumentLength`: `Process.Start` fails on Windows past about 32,760, some 800 hidden refs):
  `--ignore-missing --stdin --` with plain full names on standard input, which every git from 2.30
  accepts there (solo's names and HEAD; for hide the full names of every visible ref, which waits
  for the refs). The filter comes from the session, so git starts before the refs are read, as
  before (Q1). `CommitPager` leaves out a stash whose base is hidden (see "Git layer").
  `CommitGraphData` takes the filter: a hidden branch has no label, in solo mode only the soloed
  branches and HEAD's keep theirs, tags keep theirs on the rows shown; its `Refs` stays whole for
  the details panel and the HEAD walks.
- **A change of filter** is written to the session at once; the refresh then sees the filter's key
  change and loads the new graph off screen; the left panel shows the new state at once, and
  `GraphReloading` runs under the column headers until the new graph shows.
- **The session** (D159): `SessionState.GraphFilters`, keyed by the working tree's path as the tabs
  are, apart from them so that closing a tab keeps its filter; the 100 most recently used are kept
  (`SessionState.MaxGraphFilters`), an entry whose folder no longer exists is dropped at start-up,
  and one that hides and solos nothing is removed. After a repository's first refs read, names
  that no longer exist are dropped, and solo mode ends when no soloed name is left; a branch
  renamed outside the app shows again under its new name.
- The left panel's marks and toggles and the menus' items: see "Left panel and commit details" and
  "Context menus".
- Tests: `GraphFilterTests`, `GraphFilterArgumentsTests`, `CommitPagerTests` (Git);
  `GraphFilterChangesTests`, `HideAndSoloTests`, `RepositoryViewModelFilterTests`,
  `CommitGraphDataTests`, `LeftPanelViewModelTests` (App); check F14, and F15 for the dashed stash
  lines.

### Workspaces (C1, D187 to D189, D203, D204, D227)

Phase 6. A workspace is a named, ordered list of repositories checked and updated together. It is
the app's own data: an edit changes nothing on disk but `workspaces.json`, and is not in the undo
history (D154). Core's `Workspaces/` is pure but for `WorkspacePaths.Same`'s expansion of short
names; the flows are in `ViewModels/Workspaces/`.

- **The model** (`Core/Workspaces/Workspace.cs`): `WorkspaceList(Version, Workspaces)` with
  `Find(id)`, `Workspace(Id, Name, Repositories)` and `WorkspaceRepository(Path)`. An id is a GUID
  (`"N"` form) never shown, by which the session names an open dashboard, so a rename keeps it; a
  name is trimmed, 1 to 100 characters, without control characters, unique ignoring case; a path is
  the working tree's top as git reports it; a repository may be in several workspaces, once in each,
  and a linked worktree is a repository of its own.
- **The file** (`AppPaths.WorkspacesFile`; D189, D203): `{ "version": 1, "workspaces": [ { "id",
  "name", "repositories": [ { "path" } ] } ] }`, camelCase and indented. `IWorkspaceStore` (Core,
  shaped as `ISettingsStore`: `Current`, `Update`, `Changed`, and `IsReadOnly`, `ReadOnlyCause`,
  `Notice`, `SaveFailed`) and `JsonWorkspaceStore` (App), which never writes over a file it could not
  read: no file is an empty list; bad JSON or a read that fails (an `IOException` tried twice more,
  100 ms apart) moves the file aside to the first free name of `workspaces.unreadable.json`,
  `workspaces.unreadable-2.json`, … with a notice, and when the move fails too the store is read-only
  for the session (`Unreadable`) and the next start reads the file again; a newer version's file is
  read, never saved (`NewerVersion`), also when it cannot be parsed; a failed save keeps the change
  for the session and says so (`SaveFailed`, on the welcome page and the dashboard) until a save
  succeeds. What it reads is cleaned as a hand-edited file needs (blank and repeated ids, blank
  names and paths, a path a workspace holds already). Writes go through `DurableFile` (D236). Tests
  fake the disk through `IWorkspaceFiles`.
- **The same repository** (design 1.2): every match of a folder (the workspace code, the dashboard's
  rows and pending tabs, the palette) goes through `WorkspacePaths.Same`, `Path.GetFullPath` and
  `WorktreeKey.Comparer` (ignoring case on Windows and macOS), so `C:\Users\RUNNER~1` and macOS's
  `/var` match what git reports. A saved folder is a repository only at its top
  (`IGitRepository.OpenedAtTopAsync`, D204), and `WorkspaceMembership.Contains` also asks the open
  rows' working directories, which covers a folder saved through a junction or a link.
- **The edits** (`WorkspaceEdits`, pure): `Create`, `Rename`, `Delete`, `Append` (in the order given,
  each path not held yet), `Remove`, `Move` (by one, nothing at either end) and `Replace` (Locate…);
  each returns the same object when it changes nothing, so the store neither saves nor raises
  `Changed`.
- **The commands** (`WorkspaceCommands`, one per session in `TabServices`): every flow, so the
  welcome page, the dashboard and the palette run the same code, each with its reason (null while
  it can run) and a run that checks again and never throws; while the store is read-only every
  command but Open is disabled with `EditReason`. New workspace… and Rename workspace… use the name
  dialog (`WorkspaceNameDialogViewModel`); Delete workspace… asks (see "Dialogs") and closes the
  workspace's dashboard (`DashboardViewModel.WorkspaceDeleted`), leaving the tabs opened from it;
  Add repository… (the folder dialog "Add a repository to {workspace}") appends a working tree's
  top at once and scans any other folder; Add open tabs appends the tabs' working trees the workspace
  does not hold, in tab order (`IWorkspaceHost.OpenRepositoryFolders`); Remove from workspace asks
  nothing and is disabled while a bulk run holds the row; Move up and Move down; Locate… (the folder
  dialog "Locate {folder}") replaces a row's saved folder with the chosen folder's repository top.
  Errors use the error dialog with the workspace's name as its "In" line.
- **The folder scan** (`Core/Workspaces/RepositoryScanner.cs`, file system only, on `Task.Run`):
  breadth-first to depth 4 (`ScanOptions(MaxDepth = 4, MaxFolders = 10_000)`), a folder with a `.git`
  folder or file a candidate that is not opened; left out below the root are `node_modules`,
  `bower_components`, `vendor`, `packages`, `bin`, `obj`, `target`, `build`, `dist`, `out`, names
  starting with `.`, links (other reparse points such as OneDrive's are followed), on Windows Hidden
  or System folders, and folders whose listing fails; at most 10,000 folders. The "Add repositories
  to {workspace}" dialog (`AddRepositoriesDialogViewModel`) shows the scan's progress, opens each
  candidate (`OpensAtOnce` 4 at a time) and keeps those at their top, and lists them checked, the
  repository holding the chosen folder first and unchecked, "Already in this workspace" checked and
  disabled; its answer is appended.
- **Folders that are gone** (`WorkspaceFolderProbe.ProbeAsync`, design 1.10): a saved folder is
  sorted before anything reads it into `Ready`, `FolderNotFound` (not fully qualified, or the folder
  check, on the thread pool, says it does not exist; no git call), `NotARepository` (also a folder
  inside another working tree, whose outer repository is dropped at once and never read or acted
  on) or `NotOpened` (git refused, or no usable git). A row that is not `Ready` stays, with `Locate…`
  and `Remove from workspace`, its Open, Fetch and Pull disabled with its state's tooltip.
- **The welcome page's section** (D227, design 13.1): "Workspaces" between the buttons and the
  recent list, inside the buttons' panel (the clone form hides it): `NewWorkspaceButton`, then
  `WorkspaceList` with a `WorkspaceItem` per workspace sorted by name (`TabServices.WorkspaceItems`,
  shared by every welcome page and rebuilt on `Changed`), each with its count and a menu (Open,
  Rename workspace…, Delete workspace…), or "No workspaces"; the store's notice above it
  (`WorkspacesNotice`). A click opens the workspace from the page's own tab.
- **How a workspace opens** (`IWorkspaceHost.ShowWorkspace(id, from)`, which `MainWindowViewModel`
  implements: the one way, from the welcome page, `WorkspaceCommands` and the palette; D205): its
  open dashboard is activated; else an empty `from` tab becomes the dashboard
  (`RepoTabViewModel.ShowDashboard`) and moves after the open dashboards, so a welcome page that
  opened a workspace leaves no "New tab" behind; else a dashboard tab is inserted there. Then it is
  activated and the tabs are saved. No repository of the workspace opens in a tab (D188).
- **In the session** (design 1.11): a dashboard is saved as `TabState(null, WorkspaceId)`. The
  restore (`MainWindowViewModel.Session.cs`) drops a tab whose workspace the store lacks and a second
  dashboard of one workspace, puts the dashboards first in their saved order and the other tabs
  after in theirs (a stable partition), and maps the saved active tab to its tab. A restored
  dashboard has no pending folder, so `InitializeAsync` opens no repository for it; an older version
  sees an empty tab, which its next save drops.
- Tests: `WorkspaceEditsTests`, `WorkspacePathsTests`, `RepositoryScannerTests`,
  `GitRepositoryOpenTests` (Git); `JsonWorkspaceStoreTests`, `JsonSessionStoreTests`,
  `DashboardSessionTests`, `WorkspaceNameErrorTests`, `WorkspaceCommandsTests` (with
  `WorkspaceTestSupport`), `AddRepositoriesDialogViewModelTests`, `WorkspaceFolderProbeTests`,
  `WelcomeWorkspacesTests` (App); checks W1, W2 and W9.

### The dashboard tab (D187, D188, D205 to D208)

Phase 6. `ViewModels/Workspaces/DashboardViewModel.cs` (its rows `DashboardRowViewModel`, its texts
`DashboardTexts`, its menus `ViewModels/Menus/DashboardMenus.cs`), `Views/Workspaces/DashboardView`
with `DashboardColumns` and `DashboardRowPanel`, in a `RepoTabViewModel` (see "Repository tabs").

- **Its place**: a `RepoTabViewModel` holding a `DashboardViewModel`, not a tab type of its own, so
  every `ActiveTab.*` binding keeps one type. The open dashboards are the first tabs of the strip,
  in the order opened, one per workspace; nothing puts another tab before one. Its tab has
  `IconWorkspace` before the title, the automation id `DashboardTab` and the `ItemStatus`
  `Workspace`, and always a close button; closing it closes only the dashboard (its run is cancelled
  as by Cancel all, its rows' summaries released), and the tabs it opened stay. "+" is named "Open a
  new tab".
- **The header** (`DashboardHeader`, 36 high, laid out by `DashboardHeaderPanel`, which never wraps
  it: the run's text keeps at least 120 while cut, the title giving way first): `IconWorkspace`,
  the name (`DashboardTitle`), the
  count (`DashboardCount`); from the right `DashboardMore` (`IconMore`, its menu
  `DashboardMenus.ForHeader`: Add open tabs, Rename workspace…, Delete workspace…),
  `DashboardRefresh` ("Refresh", F5; Cmd+R on macOS), `DashboardPullAll`, `DashboardFetchAll` (the
  toolbar's states and tooltips) and `DashboardAddRepository`; before them the run's text
  (`DashboardBulkText`), its 80-by-4 progress (`DashboardBulkProgress`) and `DashboardCancelAll`
  while a run runs. While a save of `workspaces.json` fails `DashboardNotice` says so under the
  header. An empty workspace shows `DashboardEmpty`: "{workspace} has no repositories yet.", Add
  repository… and Add open tabs.
- **The rows** (D206, design 2.6): `DashboardColumnHeaders` (28 high) and `DashboardList`, a
  virtualised single-selection `ListBox` of 26-high rows (`DashboardRow`, named by the repository)
  with the cells `DashboardRowName`, `DashboardRowFolder`, `DashboardRowBranch`,
  `DashboardRowAheadBehind`, `DashboardRowChanges`, `DashboardRowState`, `DashboardRowOperation`.
  `DashboardColumns.Compute(width)` (pure, shared by the headings and every row through
  `DashboardRowPanel`, as `GraphColumns` is): Repository 132, Folder, Branch 150, Ahead / behind 130,
  Changes 220, State 180, Operation; with `rest = width − 812`, from `rest ≥ 468` (1280 wide)
  Operation is `clamp(300 + (rest − 468) / 2, 300, 460)` and Folder the rest, below that Folder is
  hidden and Operation `max(rest, 180)`. Every cell is a `TrimmedTextBlock`. A container the list
  prepares for a row starts without a hover (`DashboardView.ClearHover` on `ContainerPrepared`):
  Avalonia 12.1.3 recycles a container with its `IsPointerOver` when the list is emptied (another tab
  active) and hands it to another row when the dashboard shows again (the gate's W8). `DashboardTexts` (pure)
  makes the cells from `RepoRefs` and `WorkingTreeStatus`: the branch ("Detached at {short}", a
  rebase's branch), ahead and behind as the left panel draws them or "Up to date", "{up} is gone",
  "No remote" (tested before "No upstream"), "No commits yet"; the changes ("Clean", or the counts
  of conflicts, staged, unstaged (without untracked and conflicted entries) and untracked, in
  `VcDangerBrush` while a file conflicts); the state ("Reading…", an operation in progress with a
  rebase's "({k} of {n})", a folder state, "Git could not open it"); the operation (the status's
  compact text with `DashboardRowCancel` while it can be cancelled, a bulk run's text, or a kept
  failure with `DashboardRowDetails`). A row copies its values on each change of `Refs`, `Status`
  and `BranchText` instead of binding through the view model, so they stay while the view model is
  replaced, and stops listening when it leaves it.
- **Selection, menu and keys** (design 2.7, 6.7): no row is selected when a dashboard first shows,
  and the list takes the focus when it becomes active. `DashboardMenus.ForRow` opens at once on a
  right-click (which selects the row), Shift+F10 or the Menu key: Open (Enter), Fetch, Pull, "Cancel
  {title}" while an operation of the user that could be cancelled runs (disabled "{title} can no
  longer be cancelled" past its cancel window), "Error details…" while a failure's dialog is kept,
  Show in folder, Copy path, Move up (Alt+Up), Move down (Alt+Down), "Locate…" in a folder state,
  Remove from workspace (Delete; Cmd+Backspace, written "Cmd+Delete", first on macOS). Each disabled
  item has its reason ("Wait for {name} to be read", the toolbar's reasons, "Wait for Fetch all to
  finish"). A double-click or Enter opens; after a removal no row is selected and the focus stays
  on the list, or goes to `DashboardEmptyAddRepository` when the last row went. The menu layer's
  log line names the workspace.
- **Opening a repository from a row** (D188, design 2.3; `DashboardViewModel.OpenRequested`, which
  the window handles): the leftmost tab that shows the worktree is activated (its view model is the
  row's or is registered for the row's worktree, or its pending folder is the row's folder);
  otherwise a new tab is inserted after the dashboards and the still-open tabs this dashboard opened
  this session (`OpenedTabs`, not saved), activated, and opens the folder with `remember: true`,
  adopting the row's summary-mode view model. A row without a repository opens nothing.
- **When rows are read** (D207, design 2.10): no row has a file watcher. A row whose worktree is open
  in a tab follows that tab and is never read by the dashboard (it counts as not read until the
  tab's `FirstRead`). The others are read when the dashboard becomes the active tab and when the
  window is activated with it active (`OnWindowActivated`, Q4), both only for rows never read or
  last read `RereadAfter` (10 s) ago or more by `TabServices.ReadClock`; on F5 and Refresh (every row
  without a tab); after the row's own operation, by the queue's refresh; for a running bulk run that
  waits for them; and, while the dashboard is active, for rows the workspace gains (Add repository…,
  the checklist, Add open tabs, Locate…). A dashboard in the background starts no read of its own,
  so a restored one never slows a restored tab's first graph (Q1). At most `MaxReads` (4) reads run
  at once (`ReadSlots`, held only around a read's git calls); a trigger during a pass folds into one
  more pass, judged as of when it was asked. A row's read runs on the UI thread between its awaits:
  a row with its own view model checks its folder and git folder on the thread pool (gone: the view
  model is released and the folder probed again, so a `.git` deleted inside another repository is
  never read as that one), then refreshes; a row without one probes its folder
  (`WorkspaceFolderProbe`) and acquires its summary-mode view model from the registry, or follows a
  tab registered meanwhile. A folder check that has not returned holds no slot and no pass: a pass
  ends without it after `CheckPatience` (2 s), and without a read whose git call has run for
  `GitCallPatience` (10 s, by the read clock), which keeps its slot and ends later, so a share that
  stops answering cannot hold F5. Each row's token is linked to the dashboard's lifetime and
  cancelled when the row is removed or its path replaced; after each await a read applies its
  result only while the row still holds the token, and otherwise releases what it acquired. A pass
  that read logs `{workspace}: read {n} repositories in {ms} ms` at Info; a trigger that finds no row
  to read starts no pass and logs nothing.
- **The toolbar, the status bar and the activity log on a dashboard tab** (D208): see "Toolbar,
  status bar, conflict banner and auto-fetch" and "Activity log". `DashboardBulkViewModel`
  (`DashboardViewModel.Bulk`) is both the status bar's source and the toolbar's `IDashboardToolbar`.
- **Disposing** (its tab closed, or `CloseAll`): the lifetime is cancelled, the run abandoned as by
  Cancel all, the rows' summary-mode view models released, and the dashboard stops listening to the
  store, the registry and every followed view model.
- Tests: `DashboardColumnsTests`, `DashboardTextsTests`, `DashboardMenusTests`, `DashboardTabTests`,
  `DashboardRefreshTests`, `DashboardActivityLogTests`, `DashboardViewTests`, `SummaryModeTests`,
  `PromotionTests`, `OpenRepositoriesTests`, `MainWindowCloseTests` (App, with
  `DashboardTestSupport`); checks W2, W3, W8 and L30.

### Bulk fetch and pull (D195, D209 to D211)

Phase 6. `ViewModels/Workspaces/BulkRun.cs`, `BulkSlots.cs`, `DashboardBulkViewModel.cs`; the
operations are the toolbar's own (`RepositoryOperations.FetchAllAsync(BulkPart)`,
`PullAsync(BulkPart)`), through each row's repository's queue, with the toolbar's titles, running
texts, Cancel, error dialog, dedupe keys and undo recording.

- **A run** (`BulkRun`, one per click of Fetch all or Pull all, on the UI thread, one at a time per
  dashboard): every row is decided at the start, in row order, by the toolbar's rule
  (`ToolbarViewModel.StatesFor`): left out with the toolbar's reason (`Skipped`), still being read
  (`Reading`: the run waits for its first read, or reads a row without a view model itself), or in
  line. A row in line asks the window's slots for its turn; at its turn its folder and git folder
  are checked off the UI thread again (a folder that is gone, or lies inside another working tree
  now, is left out, D204, D207) and it is decided again from its current view model and state, never
  the one of the run's start; with its slot it calls the toolbar's operation with the run's
  `BulkPart` and gives the slot back when the call returns. Each pull fetches first (D84).
- **The slots** (`BulkSlots`, `TabServices.BulkSlots`, one per window): at most `Count` (4) rows of
  every run of the window at once, first come first served, never two at once to one common git
  folder, nor one to a common folder another queue is fetching from
  (`OpenRepositories.NetworkQueueOn`, the row's own queue excepted): such a request is passed over
  and looked at again when a slot comes back or that queue changes. A Fetch, Pull or Push the user
  starts beside a run is not kept apart (risk 33). Auto-fetch skips a tick while a slot or another
  queue fetches from its common folder.
- **Prompts** form one group per run (`BulkRun.PromptGroup`; see "Prompts and credentials"); since
  phase 7a a row that fails with `AuthenticationFailed` makes the group forget its kept answers
  (ruling 9), and a row without a tab binds its fetch or pull to the hosting account as a tab does
  (D332).
- **Cancel all** (`DashboardCancelAll`, the status bar's Cancel on a dashboard tab, and closing the
  dashboard tab) stops starting rows (`StopStarting`: rows in line or being read end "Fetch
  cancelled" or "Pull cancelled", their slot requests withdrawn) and cancels the run's own
  operations through `RunOptions.Cancellation` (`IOperationQueue.Cancel`), a tab's Fetch or Pull that
  D123's dedupe merged with a row's included; a pull past its fetch finishes. A row's own Cancel is
  its status's (`queue.CancelCurrent`). The window's close calls every run's `StopStarting` before
  the queues drop what waits.
- **Failures stay in their rows** (D195, D211): during a run no dialog opens for a result; a failed
  row shows "{Title} failed" in the danger tone with "Details…", which opens the identical error
  dialog later (`DeferredDialog`, kept by the row; likewise a cancel's lock files, a pull's rebase
  stop with nothing to look at and an unexpected exception). A single row's Fetch or Pull from its
  menu opens its dialog at once, as the toolbar does. A result that comes after its dashboard
  closed opens its dialog in the view model that ran it. A failure outside the queue shows "Fetch
  failed" or "Pull failed" with the message in the tooltip.
- **The texts** (`OperationTexts.Bulk.cs`): while it runs "Fetching {n} repositories… {k} done"
  (`{n}` the rows acted on, `{k}` those ended or stopped in line) with a determinate progress, then
  "Cancelling: waiting for {r} repositories…" after Cancel all, and at the end its counts, failures
  first ("Pulled 6 repositories; 1 failed; 2 skipped"), in the danger tone when a row failed, the
  warning tone when one stopped; the status bar shows the same and "{w} waiting" for rows in line.
  The run logs its start and its result.
- **The toolbar on a dashboard** (D208, design 4.10): Fetch and Pull, their keys and their palette
  entries run Fetch all and Pull all, enabled while rows are being read, disabled while a run runs,
  for an empty workspace and when no row could, each with the first reason that holds.
- **The rows a run holds** (in line, reading or running) cannot be removed from the workspace; a
  removed row that the run skipped or ended leaves the run's counts as they were.
- Tests: `BulkSlotsTests`, `BulkRunTests` (with `BulkTestSupport`, and against real git),
  `DashboardBulkViewModelTests`, `DashboardToolbarTests`, `RepositoryOperationsFetchTests`,
  `RepositoryOperationsPullTests`, `PromptDialogsTests` (App); `OperationQueueTests`, `AskPassTests`
  (Git); checks W4 to W7.

### The command palette (D191, D212)

Phase 6. `ViewModels/Commands/CommandPaletteViewModel.cs`, `PaletteEntry.cs`, `PaletteMatcher.cs`,
`AppCommands.cs` and `AppCommands.Palette.cs`, `RepositoryViewModel.Palette.cs`,
`Views/Commands/CommandPaletteView`; opened by `MainWindowViewModel.OpenPaletteAsync(query)`.

- **A dialog of the dialog layer** (`CommandPaletteViewModel : DialogViewModel<PaletteEntry?>`), so
  it has the layer's Esc, focus return, stacking and UI Automation root (`CommandPalette`, a pane
  named "Command palette"); unlike other dialogs it has no dimming (`Panel.paletteBackdrop`,
  transparent, takes a press outside the card, which cancels), no buttons and no drawn title. Its
  card (`Border.paletteCard`, `Theme/Dialogs.axaml`) is 600 wide at the top centre, 8 below the
  toolbar: the box `CommandPaletteBox` (placeholder "Search commands, branches, tags, stashes,
  remotes, repositories and workspaces"), then `CommandPaletteList` (rows 28 high, at most 12 shown,
  `MaxHeight` 336) or `CommandPaletteEmpty` ("No matches"). A row has `CommandPaletteItemLabel`
  (cut at 380, in `VcDangerBrush` for an enabled destructive entry), `CommandPaletteItemDetail` and,
  when the entry has a key, `CommandPaletteItemKey` right-aligned; the highlighted row lies on
  `VcSelectionBrush`.
- **Entries** (`PaletteEntry`, no Avalonia types): `Label` (as the menu item or command says it),
  `Detail`, `KeyText`, `IsEnabled`, `DisabledReason`, `IsDestructive`, `Kind` (`Command` or `Item`),
  `Order`, `Run`, and `Key` (the label, then U+001F and the detail), by which the highlight across a
  rebuild, the session's runs and duplicates go. An entry shares its label, state and reason with
  the menu item or button that does the same: the same code builds both.
- **The commands** (`AppCommands.PaletteCommands`, shown at once, also in an empty box): on a
  repository tab the repository's (Fetch all remotes, Pull, Push, Create branch…, Stash changes…,
  Pop the newest stash, Undo, Redo, Search commits, Next and Previous match while the search bar is
  open, Stage all and Unstage all with working changes, Go to the commit message, the commit button's
  own text as an entry (D199), Show reflog, Show backups, Refresh, Create tag at HEAD…, Add remote…,
  Manage remotes…, Show all hidden branches, Stop soloing, Filter branches and tags; their detail is
  the repository's name while dialogs name it); on a dashboard tab Refresh, the toolbar's Fetch and
  Pull (Fetch all, Pull all) and the workspace's own edits (Add repository…, Add open tabs, Rename
  workspace…, Delete workspace…); then the window's (New tab, Close tab, Next and Previous tab, Go to
  the next dashboard while one is open, Open, Clone and Init repository… (since phase 7a "Clone from
  GitHub…" after Clone while GitHub is not off, see "Clone from GitHub"), New workspace…, Show or
  hide the activity log, Open settings…, Show keyboard shortcuts…, "Choose an editor…" while none is
  chosen, and the two themes and System not chosen now). Each state comes from `AppCommands`, which
  the keys share, and a state taken from a menu builder from that builder on the same
  `MenuContext` snapshot, so a key, its entry and its menu item are enabled together.
- **The items** (`AppCommands.PaletteItems`, once the user types; an empty box would list some 1,500
  "Copy SHA" lines): on a repository tab the items of its menus, built by the `RepositoryMenus`
  builders on one snapshot (`RepositoryViewModel.PaletteItems`: every local branch, the checked-out
  one first then the left panel's order, every remote branch, tag, stash and remote, since phase 7a
  every listed pull request's menu (`ForPullRequest`, all 300 when the list is full), the selection's
  commit or commits, the working changes when there are any, the open diff's menu and the open page's
  selected row's), each with its menu's header as detail; what a menu leaves out the palette lacks
  too, an item whose command is a listed command of the same label is left out, and items of one key
  are one. Then the window's: "Go to {title}" for every tab but the active one (detail "Tab {n}",
  with Mod+{n} up to 8 and Mod+9 for a last tab at 9 or later), "Open repository {name}" for every
  recent repository and then every workspace's repository not listed yet, but the active tab's (the
  detail its folder, a workspace's as saved; a folder its open dashboard found in a folder state
  disabled with the row's reason), and "Open workspace {name}" (detail its count). The palette opens
  with the worktrees last read and reads them again (`ReadMenuContextAsync`); when they differ while it
  is still open, the items are built again (`ReplaceItems`). `RepositoryMenuContext.FindRef` looks a
  ref up in an index by full name, built at the first call, so the build is linear in the refs.
- **Matching** (`PaletteMatcher`, pure, D212): the query is trimmed and split at white space; an
  empty one lists the commands only, those run this session first; otherwise an entry matches when
  every word occurs in its label or detail, ignoring case, and the matches rank by tier (0 the label
  starts with the whole query, 1 every word starts a word of the label, 2 every word is in the label,
  3 a word only in the detail), then enabled before disabled, then the session's runs (at most 20
  keys, newest first, never saved), then source order, commands before items. No fuzzy matching.
- **Disabled entries** show, dimmed, with the menus' reason in the detail's place and as tooltip;
  Enter or a click does nothing and the palette stays.
- **Keys and pointer**: on the box's tunnelling pass Up and Down move the highlight, wrapping, PageUp
  and PageDown 11 rows (`PageRows`), Enter runs the highlighted enabled entry, Mod+Shift+P selects the
  box's text (the router raises `SelectQueryRequested`); Esc is the dialog layer's; the window's other
  keys decline while it is open, so Ctrl+Z in the box is the box's. The pointer over a row highlights
  it and a click runs it.
- **Running an entry** (design 5.9): once the palette has closed and the focus is back, the entry's
  key goes first in the session's runs, `{repo}: Command palette ran: {label} ({detail})` is logged at
  Debug (the opening logs `Command palette opened`), and the entry runs. A repository opened from the
  palette or a key goes to the tab that shows that worktree, else to the active tab when it is empty,
  else to a new tab at the end (`OpenRepositoryAtAsync`); a workspace's saved folder opens only as the
  top of its repository. "Open workspace {name}" is `ShowWorkspace(id, ActiveTab)`.
- Tests: `PaletteMatcherTests`, `CommandPaletteViewModelTests`, `PaletteEntriesTests`,
  `CommandPaletteViewTests`, `PaletteGrowthTests` (App); check K10; since phase 7a
  `PaletteCloneFromGitHubTests` (Hosting.Tests), `RepositoryMenusHostingTests` (Phase7a) and checks
  B7, B8 and P11.

### Keyboard shortcuts (D163, D192, D199, D213 to D215)

Phase 6. The fixed key table is data (`ViewModels/Commands/AppShortcuts.cs`); the window routes keys
by it (`Views/KeyRouter.cs`); every key text comes from one formatter; no screen changes a key.

- **The types** (`ViewModels/Commands/`, no Avalonia types): `AppCommand` (every command with a key
  or a palette entry, and the keys only drawn and listed, which their views handle: the context menu,
  the text editors' undo, redo, cut, copy, paste and select all, the interactive list's letters, the
  resolver's Alt keys and Esc); `Shortcut(ShortcutKey Key, ShortcutModifiers Modifiers)` with
  `Command` (the platform's command modifier), `Shift`, `Alt` and `Control` (the Control key itself,
  for Ctrl+Tab and Ctrl+PageDown on macOS); `CommandKey` (`Ctrl` or `Cmd`, whose words a text uses);
  `ShortcutBinding(Command, Keys, Pass, ShownOnCtrl, ShownOnCmd)` with `KeyPass` `Tunnel` (the
  router's), `Bubble` (undo and redo) or `View`.
- **The table** (`AppShortcuts.Bindings`, design 6.1): Mod+Shift+P the palette, Mod+, the settings,
  Mod+J the activity log, F5 (Mod+R) Refresh, Mod+T and Mod+W new and close tab, Ctrl+Tab and
  Ctrl+PageDown, Ctrl+Shift+Tab and Ctrl+PageUp the next and previous tab (Control on every platform),
  Mod+1 to Mod+8 a tab by its place and Mod+9 the last, Mod+Shift+D the next dashboard, Mod+O,
  Mod+Shift+C and Mod+Shift+I open, clone and init, Mod+Shift+F, L, K Fetch, Pull and Push, Mod+B a
  branch, Mod+Shift+S and Mod+Shift+O stash and pop, Mod+Z and Mod+Y (Mod+Shift+Z) undo and redo,
  Mod+F search, F3 (Mod+G) and Shift+F3 (Mod+Shift+G) the next and previous match, Mod+Shift+R the
  reflog, Mod+Shift+A and Mod+Shift+X stage and unstage all, Mod+Shift+M the commit message, Mod+Enter
  commit, Mod+Shift+H and Mod+Shift+B the open diff's file history and blame, Mod+Shift+E the filter,
  F6 and Shift+F6 the regions. `Route(shortcut, platform)` gives a key's command on the tunnelling
  pass (with its argument, the place for Mod+1 to Mod+8), `Shown` the key a text shows (Refresh F5 but
  Cmd+R on macOS, Redo Ctrl+Y but Cmd+Shift+Z, the matches F3 but Cmd+G), `All`, `ShownFirst`, and
  `Groups(platform)` the settings screen's list. The commands left without a key (D199, D213) are
  reached through the palette; since phase 7a "Clone from GitHub…" (`AppCommand.CloneFromGitHub`,
  after `FollowSystemTheme`) is one of them (D299). The keys left free are D213's.
- **One formatter** (D214): `ShortcutText.Format(shortcut, platform)` writes every key text in words,
  also on macOS, joined by `+` (`Ctrl+Shift+P`, `Cmd+Shift+P`, `Option+Up`, `Ctrl+,`, `Esc`; on macOS
  Backspace as `Delete` and Delete as `Forward Delete`), and `FormatAll` joins several (`Ctrl+1 to
  Ctrl+8`); it is the text of the palette, the menus, the tooltips, the settings list and UI
  Automation's `AcceleratorKey`. Avalonia's `InputGesture` and `HotKey` stay unused. `KeyTexts`
  (`Of(command)`, `WithKey(text, command)`, "{text} ({key})") holds one platform's texts;
  `KeyTexts.Current` is set by `AppSession.Start` before the window is made, from the platform's
  command modifier (`ShortcutKeys.CommandKeyOf`: Cmd for Meta, else Ctrl). A view model that writes
  key texts holds its `KeyTexts` in a property of that name: `ToolbarViewModel.KeyTexts` and
  `WorkingChangesViewModel.KeyTexts`, which a test of another platform's texts sets, and
  `AppCommands.KeyTexts`, given to its constructor. Each key text has one source. `ShellKeys` holds
  only the key texts that views bind to `AcceleratorKey`: the toolbar's buttons, the tab strip's "+"
  and the status bar's activity log toggle (`MainWindowViewModel.Keys`), and the dashboard header's
  Fetch all, Pull all and Refresh (`DashboardViewModel.Keys`). The active tab's close button takes
  Mod+W's text from its tab (`RepoTabViewModel.CloseKeyText`). `ShellToolTips` holds the shell's and
  the views' tooltip texts with their keys, and the key texts of the buttons outside the shell: the
  welcome page's Open, Clone and Init, the resolver's buttons and the interactive list's Cancel.
- **The router** (`KeyRouter`, which `MainWindow.Keys.cs` hands both passes; design 6.2, 6.3). On the
  tunnelling pass, with `handledEventsToo`: the key in the app's words (`Views/ShortcutKeys.cs`,
  `FromEvent`, with the command modifier read at each press; `ToGesture` the reverse, for the
  harnesses), then `AppShortcuts.Route` with that modifier's `CommandKey`, or the key goes on.
  `ShortcutKeys` is the app's one source of the command modifier: `CommandModifierOf` gives the
  platform hotkey configuration's `CommandModifiers`, or Control when there is no configuration or
  it names none, and `CommandKeyOf` gives Cmd for Meta, else Ctrl. `AppSession.Start`, the router,
  `DialogView`'s Mod+Enter, the dashboard's list (`DashboardView.PlatformCommandKey`) and the graph's
  multi-selection (`CommitGraphControl.CommandModifiers`) read it through them. The router declines,
  in order: while a dialog is open (but Mod+Shift+P with the palette the top dialog, which selects
  its box's text); while a menu is open or a branch drag runs; Ctrl+Tab
  and Ctrl+Shift+Tab in the conflict resolver's result (Tab with Control leaves the result there);
  Mod+Enter outside `CommitSummary` and `CommitDescription`. Otherwise
  `MainWindowViewModel.Commands.Run(command, argument)` runs it when it applies and is enabled, and
  the key is handled either way: a disabled command's key is consumed, Mod+F included (D163's rule).
  On the bubbling pass phase 5's undo and redo keep their rules (see "Toolbar, status bar, conflict
  banner and auto-fetch"). The router logs nothing. The Esc chain (`EscapeOrder`) and Shift+F10 (D106)
  stay as they were. What moves the focus goes through `IWindowKeyActions`, which `MainWindow`
  implements: `FocusCommitMessage`, `FocusFilter`, `MoveFocusToRegion`.
- **Tabs** (design 6.5): Ctrl+Tab and Ctrl+Shift+Tab go round the tabs in the strip's order
  (dashboards first), wrapping; Mod+W closes the active tab only, so only that tab's close button
  shows the key (`RepoTabViewModel.CloseKeyText`); Mod+Shift+D goes to the next dashboard, or with
  none opens the palette at "workspace".
- **F6 and Shift+F6** (design 6.6, `Views/KeyRegions.cs`): the focus goes round the regions shown,
  the left panel, the graph area, the right panel and the activity log, wrapping, each back to its
  last focused element while that is still visible, enabled and inside; else to the element marked
  `KeyRegion.IsMain` that the region shows (`RefList`, `GraphRows`, `DiffText`, a page's list, the
  resolver's result, the dashboard's list, the stage lists, `ActivityLogList`, …). Each region's root
  carries `KeyRegion.Role`.
- **On a dashboard** (design 6.9): Fetch's and Pull's keys run Fetch all and Pull all, F5 and Mod+R
  refresh the rows, Mod+J shows the selected row's activity; the other repository keys are consumed;
  Enter, Delete (Cmd+Backspace on macOS), Alt+Up, Alt+Down and Shift+F10 are the list's.
- **Commit** (design 6.10): Mod+Enter in the message boxes runs the stage panel's commit command,
  taken on the way down (a text box that accepts Return types a line break for Enter with any
  modifier); the commit button's tooltip says "{its text} ({key})" (`CommitToolTip`, its reason now
  `CommitDisabledReason`). A dialog takes Mod+Enter in a box of several lines as its confirm.
- **Shown keys** (D215, design 6.7, 6.8): menus as "Context menus" says; every keyed button's
  tooltip, the resolver's five buttons ("Previous conflict (Alt+Up)", …, "Close (Esc)"), the
  interactive list's "Cancel (Esc)" and the search bar's "Next match (Enter, F3)" and "Previous match
  (Shift+Enter, Shift+F3)" included, with the tooltip as help text and the key as `AcceleratorKey`.
  The search box's own Enter and Shift+Enter in those two are written by `ShortcutText.Format` too,
  and the search bar's close button says "Close the search (Esc)" with Close's key
  (`SearchViewModel.CloseToolTipFor`, through `KeyTexts.WithKey`), as tooltip and help text, with no
  `AcceleratorKey`.
- **Kept from phase 5**: D163's undo and redo on the bubbling pass, Mod+F (now one of the set, still
  selecting the search box's text when the bar is open), the Esc chain and its `EscapeOrder`.
- Tests: `ShortcutTextTests`, `AppShortcutsTests`, `KeyRouterTests`, `TabKeysTests`,
  `RegionKeysTests`, `CommitKeyTests`, `MenuKeyColumnTests`, `RepositoryMenusKeyTests`,
  `ToolbarToolTipTests`, `ShellToolTipTests`, `WindowKeysTests` (App, with `KeysTestSupport`); checks
  K11 to K13.

### The settings screen (D197, D200, D216)

Phase 6. `ViewModels/Dialogs/SettingsDialogViewModel.cs` with `ViewModels/Settings/`
(`GeneralSettingsViewModel`, `GitSettingsViewModel`, `ToolSettingsViewModel`),
`Views/Dialogs/SettingsDialogView`, `MainWindowViewModel.Settings.cs`. Since phase 7a also
`ViewModels/Settings/AccountsSettingsViewModel.cs`, the dialog "Add GitHub account"
(`ViewModels/Dialogs/SignInDialogViewModel.cs`, `Views/Dialogs/SignInDialogView`), the window's
sign-in entry (`MainWindowViewModel.Hosting.cs`) and their texts
(`OperationTexts.Hosting.Accounts.cs`, `AccountTexts`).

- **A large dialog of the dialog layer** (`SettingsDialog`, `DialogViewModel<bool>`: true after a
  Save that closed it), so both harnesses capture and drive it and "This repository" stays fixed
  while it is open: the card at most 860 × 680 (class `settings`), the title `SettingsTitle`, the
  section list `SettingsNav` (`SettingsNavGeneral`, `SettingsNavGit`, `SettingsNavTools`,
  `SettingsNavShortcuts`, since phase 7a `SettingsNavAccounts` last, which has no error dot; a
  section with an error after Save gets `SettingsNavError`), the shown
  section in a `ScrollViewer` (`SettingsSection`), Cancel and Save, and `SettingsSaving` while a save
  waits. No combo box and no menu over it: radios, check boxes, text boxes, and the Pick dialog stacked
  on top for a long list. The first focus is the shown section's button.
- **Where it opens from** (D197, D216): the toolbar's `SettingsButton`, Mod+, (both on General), the
  palette's "Open settings…", "Show keyboard shortcuts…" and "Choose an editor…", and the editor
  failure's "Open settings…" (on Tools); every entry runs `OpenSettingsCommand(section)` through
  `ShowSettingsAsync`, which gives the screen the settings store, the shell's `UseTheme`, the active
  tab's repository (none on an empty or a dashboard tab, in summary mode or before its refs are read),
  git's settings, the tool catalogues and the key list; since phase 7a also the session's hosting,
  the window's sign-in entry and the detected git's version (`SettingsScreenServices.Hosting`,
  `SignIn`, `GitVersion`). The error dialog of git's credentials that GitHub refused opens the
  screen on Accounts ("Open settings…", see "Prompts and credentials").
- **General**: the theme (Dark, Light, System), auto-fetch (`AutoFetchCheckBox` and
  `AutoFetchMinutesBox`, 1 to 60 minutes; 0 off; a file's value above 60 kept until edited), Keep
  ignored files (`KeepIgnoredCheckBox`), "Include untracked files when stashing"
  (`StashUntrackedCheckBox`), and the extensions Open file asks about (`RunnableList`, the fixed
  list; `RunnablePathExt`, `PATHEXT`'s other entries on Windows; `RunnableExtraBox`, the user's own,
  saved lower-cased in `ExtraRunnableExtensions`). **Git**: git's own settings (see "Git's own
  settings"), always opening on "All repositories (global)", "This repository" one click away, edits
  in both kept while switching (D200). **Tools**: the editor of Open in editor (`EditorValue`,
  `EditorChooseButton`, Other's `EditorProgramBox`, `EditorBrowseButton` and `EditorArgumentsBox`;
  see "Open in editor and Open file"). **Keyboard shortcuts**: `AppShortcuts.Groups` read-only, a
  `ShortcutGroup` heading per group and a `ShortcutRow` per action, the keys in the code font (D192).
- **Accounts** (since phase 7a; D256, D277, D294, D296, D297; design 8.3). `AccountsSettingsViewModel`
  (`Accounts`, made with the screen, disposed at its Cancel and at a Save that closes it) reads the
  session's account when the screen opens and follows `AccountChanged`; it makes no git call and no
  request then. The section (`SettingsAccounts`, inside a `Panel` that carries `ShowsAccounts`) holds
  `AccountsIntro` and two rows with the screen's 132-px label column. "GitHub": with GitHub off
  `AccountsOff` ("GitHub is turned off for this session.") and no button; signed out
  `AccountsSignedOut` ("Not signed in.") and `AccountsSignInButton` ("Sign in…"); with an account
  `AccountLogin` (semibold), then `AccountTokenPlace` (`settingsNote`: "The token is kept in this
  computer's keychain.", D253's no-keychain text for a token kept for the session, or the failed
  save's note) or, while it needs sign-in, `AccountNeedsSignIn` (`dialogWarning`: the token refused,
  gone from the keychain, unreadable there, or kept for the last session only), and
  `AccountsSignOutButton` ("Sign out…"), with "Sign in…" beside it while the account needs sign-in
  (D294). "Sign in…" runs the window's sign-in entry, whose dialog stacks over the screen, and then,
  signed in, focuses "Sign out…". "Sign out…" asks "Sign out of GitHub?" (design 8.2's question,
  naming the login and saying that the token and the saved avatars go, D291; its last sentence, on
  git's own helpers, left out with a detected git older than 2.31; the button "Sign out", primary,
  D296), then shows `AccountsBusy` ("Signing out…") with both buttons disabled while
  `IHostingSession.SignOutAsync` runs with `CancellationToken.None`: sign-in and sign-out act at
  once, and the screen's Cancel cannot undo them (D256). A reason the keychain kept its entry shows
  as `AccountsSignOutNote` until the next sign-in or the screen's close; the focus goes to "Sign
  in…". "Git", with an account: `AccountUseForGit` ("Use for git over HTTPS") and
  `AccountUseForGitNote`, which with a detected git older than 2.31 reads "This needs Git 2.31 or
  newer. …" (D277; the switch stays enabled, kept for a newer git). The switch is a setting: it
  starts as the account's, resets when `AccountChanged` brings another account, and Save applies it
  (`IsChanged`; `IHostingSession.SetUseForGit`, which the session writes off the UI thread); Cancel
  and Esc drop it. No scope or expiry is shown or warned of (D297).
- **"Add GitHub account"** (`SignInDialog`; D253, D288, D295, D297, D298; design 8.4).
  `SignInDialogViewModel : DialogViewModel<AccountStatus?>` with its own `CancellationTokenSource`,
  which Cancel, Esc and `CancelAll` cancel. At its opening it asks `KeychainAvailableAsync`
  (`Opened`) and, when no keychain can be reached, shows `SignInNoKeychain` (D253's text) before
  anything is typed. The 400-wide card holds the title (`DialogTitle`); `SignInHelp` (the access a
  fine-grained or a classic token needs); the link `SignInTokenLink` ("Create a token on GitHub", a
  `Button` with its own theme `SignInLinkButton` in `VcAccentBrush`, underlined on pointer-over and
  while focused, reached by Tab, with a tooltip), which opens GitHub's page for a new fine-grained
  token (`settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write`
  on the web base, D295) through `IFileLauncher.OpenUrlAsync`; the label "Token" and `SignInToken`,
  a `SecretTextBox` masked with the prompts' character, whose automation peer hides the text (D103,
  D130), the first focus; the lines of its state; Cancel and "Sign in". "Sign in" (Enter too) is
  enabled while the trimmed token is not empty and no check runs. A trimmed token with any
  character outside printable ASCII (U+0021 to U+007E) is refused before any request (D298). Otherwise
  `SignInChecking` ("Checking the token…") shows, with the box and "Sign in" disabled, while
  `IHostingSession.SignInAsync` runs. A refusal shows `SignInError` ("GitHub refused the token: Bad
  credentials.", a network failure's text as it is, or "GitHub answered: {message}."; an unexpected
  exception "Could not reach GitHub.", its words in the log only) and gives the box the focus with
  its text selected. A sign-in closes the dialog with the account's status and clears the box; one
  whose keychain save failed (ruling 19) keeps it open with `SignInSaveFailed` ("Signed in as
  {login}, but the token could not be saved …"), the box disabled and cleared, Cancel hidden and
  one "Close", focused, which closes it signed in, as Enter and Esc do. Cancel during the check
  closes it and cancels the request; if GitHub had already answered, the session completes the
  sign-in (D288). It has no "In {repo}" line, and nothing typed is logged; the token's own rules are
  in "Prompts and credentials".
- **The window's sign-in entry** (design 8.5). `MainWindowViewModel.SignInToGitHubAsync`
  (`MainWindowViewModel.Hosting.cs`), set as `TabServices.SignInToGitHub` and given to the screen as
  `SignIn`: GitHub off, false without a dialog; signed in already, true without a dialog; a sign-in
  dialog already open, its end awaited; else "Add GitHub account" on top of whatever is open (D104).
  It answers whether an account is signed in afterwards; a failure is logged and gives false. Every
  sign-in of the app ends here: Accounts' "Sign in…", the "Pull requests" section's sign-in row and
  "Sign in to GitHub…" item, "Create pull request…" and the palette's "Clone from GitHub…" while
  signed out. No other palette command signs in, and none opens Accounts (D299): "Clone from
  GitHub…" opens the sign-in dialog only as its own first step.
- **Save** (design 7.8), in order: validate every section (an error marks its section, shows it and
  focuses its first wrong field; the editor's disk checks run off the UI thread); the app's settings
  in one `ISettingsStore.Update`; since phase 7a Accounts' switch when changed
  (`IHostingSession.SetUseForGit`); the theme through `UseTheme`; the global git writes, one at a time
  through the app-level writer; this repository's writes as one operation of its queue
  (`RepositoryOperations.SaveRepositorySettingsAsync`, no undo entry; while it waits behind another
  operation `SettingsSaving` says "Waiting for {title} to finish…", and Cancel drops it through
  `RunOptions.Cancellation`); the read back; the tabs told (`TabServices.RaiseGitSettingsChanged`:
  every full-mode view model reads `merge.tool` again while it shows conflicts); then the close,
  unless a changed key is still decided by another file of its scope or a write failed, which leaves
  the screen open on Git with its notices. A failed git write opens the error dialog over the screen
  ("Saving the settings failed" for the global writes, without an "In" line). Cancel and Esc change
  nothing but Accounts' sign-in and sign-out, which act at once (D256). `ISettingsStore` has no change
  event: readers read `Current` when they need it.
- **Its work ends with it** (D234): Cancel, Esc and the session's end (`DialogHostViewModel.CancelAll`)
  cancel one lifetime token, which stops the reads, the editor search and the global writes before
  their next key; while the global writes run, `GlobalWrites` lets the window's close wait for them
  ("Saving the settings — all repositories"), and `StopStarting` keeps this repository's writes from
  starting.
- **When each change takes effect** (design 7.9): the theme at Save; auto-fetch within its next 30-s
  tick; Keep ignored files, the stash's check box and Open file's extensions at the next such
  operation; the editor at the next file menu; the identity at the next commit, also in a terminal;
  the pull mode at the next pull; the merge tool at once in an open resolver; since phase 7a a
  sign-in or a sign-out at once, and "Use for git over HTTPS" from Save (see
  "Hosting: the session and the account").
- Tests: `SettingsDialogViewModelTests`, `SettingsDialogViewTests`, `PickDialogViewModelTests`,
  `SettingsEntryTests`, `GitSettingsViewModelTests`, `RepositoryOperationsSettingsTests`,
  `GitSettingsChangedTests`, `JsonSettingsStoreTests`, `AppSessionTests` (App); checks S14 and S16
  to S21. Since phase 7a `AccountsSettingsViewModelTests`, `SignInDialogViewModelTests` (Phase7a),
  `SignInEntryTests` (Hosting.Tests, D334) and checks A1 to A11.

### Git's own settings (D190, D200, D202, D217 to D219)

Phase 6. The identity, the pull mode and the merge tool are git's configuration, never the app's
files: the global configuration and, in "This repository", the repository's own, which wins as
git's rules say. `IGitConfiguration` (Core) and `GitConfiguration` (Git), one per session over its
runner (`TabServices.GitSettings`); the screen's half is `GitSettingsViewModel`.

- **Reading** (design 8.1): `ReadAsync(workingTree)` is one `git config --show-scope --show-origin -z
  --list` (`GitConfigRead`: every `GitConfigValue` with its scope and file, in git's reading order;
  `Winner(key)` the last, which git uses). The global fields, their baseline and the read back come
  from the outside read (`workingTree` null), which runs in the data folder with
  `GIT_CEILING_DIRECTORIES` set to its parent, so that no repository is found above it and no
  repository's `includeIf` applies; on a repository's tab a second read in its working tree gives the
  repository scope. Never `git config --global --get` or `--list`, which skip the XDG file and
  includes, nor a global value read inside a repository, which can be an `includeIf`'s.
  `ReadIdentityAsync(workingTree)` is `git var GIT_AUTHOR_IDENT` and `GIT_COMMITTER_IDENT`, in the
  repository scope only (outside a repository git names the machine's user and computer).
- **Files by what they are** (design 8.2, `GitConfigLocations`): `GlobalFile` (what `git config
  --global` writes: `GIT_CONFIG_GLOBAL` when set, else `~/.gitconfig` when it exists, else the XDG
  file when it exists, else `~/.gitconfig`, the home folder found as Git for Windows finds it) and
  `XdgFile`, worked out by the app off the UI thread, never asked of git, and
  shown by what they are with the full path in a tooltip; `Variable(name)` reads the session's git
  environment over the process's.
- **Writing** (design 8.3, 8.5): only changed keys, by `git config <scope> --replace-all <key>
  <value>` or `--unset-all <key>` (exit 5, an absent key, counting as done): `WriteGlobalAsync` one at a
  time across the app (a semaphore), in the data folder; the repository's through
  `IGitRepository.WriteConfigAsync`, in its working tree, as one operation of its queue. A held
  `config.lock` (`could not lock config file`, exit 255) fails the write with git's words. An empty
  value is never written: an empty field unsets.
- **Read back** (design 8.4): every Save reads again and each read becomes its scope's new baseline;
  a value that another file of the scope still decides (an include after the file written) is said
  in a notice and keeps the screen open.
- **Identity** (D218, design 8.6): a name or email is trimmed; `<`, `>`, line breaks and a name of
  punctuation only are refused; an email without `@` warns and is saved; in the repository scope
  `IdentityInEffect` shows who git will commit as, and `IdentityNotices` what decides it.
- **Pull modes** (D218, design 8.7, `Core/Git/PullModes.cs`): one-line radios that write both
  `pull.rebase` and `pull.ff` at the chosen scope (Merge `false`/`true`, Merge commit `false`/`false`,
  Rebase `true`/`true`, Rebase keeping merges `merges`/`true`, Fast-forward only unset/`only`, Not set
  unset/unset); any value is read by its effect, booleans as git reads them (`GitBoolean`), and only
  `interactive`, `i`, `m`, a unit letter alone and values git refuses show as Other with a note. In the
  repository scope `PullInEffect` says what Pull will do (Pull's own reader), and a winning
  `branch.<b>.rebase` is noticed and removable (`BranchRebaseButton`).
- **The merge tool** (D202, D217, design 8.10): `MergeToolValue` and "Choose…", a Pick dialog of the
  tools `git mergetool --tool-help` lists (`ReadToolHelpAsync`, `GitMergeTools` parses it: found, not
  found, the user's own `mergetool.<name>.cmd` tools; terminal tools left out), run once per session
  in the background at below-normal priority once start-up has settled
  (`MainWindowViewModel.LookUpMergeToolsAfterAsync`), and the last list is kept in the data folder
  (`merge-tools.json`, `AppPaths.MergeToolsFile`, keyed by git's path and version, through
  `DurableFile`), so "Choose…" is ready at once; only the first lookup on a machine waits. No path
  or command editing; `merge.guitool` is left alone.
- **The harnesses' own configuration** (D220, design 8.12): each app session gets its own
  `GIT_CONFIG_GLOBAL` and `XDG_CONFIG_HOME` in its data folder (`GitIsolation.SessionGitEnvironment`,
  given to `AppSession.Start`), while the test process's `GIT_CONFIG_GLOBAL` names a file in a folder
  never made, so a global write outside a session fails where it happens (exit 255); see "Tests".
- Tests: `GitConfigReadTests`, `GitConfigWriteTests`, `GitConfigLocationsTests`, `GitIdentityTests`,
  `PullConfigurationTests`, `PullFastForwardSettingTests`, `MergeToolHelpTests`, `PullModesTests`,
  `GitRunnerPriorityTests` (Git, with `GitConfigTestSupport`); checks S14, S18 to S20.

### Open in editor and Open file (D130, D226)

Phase 6. `Services/KnownEditors.cs` (the catalogue per platform), `EditorCatalog.cs` (the editors on
this computer), `EditorCommands.cs` (how one starts), `FileLauncher.OpenInEditorAsync`;
`RepositoryOperations.Files.cs`.

- **Finding editors** (design 12.2, 12.3): a fixed catalogue per platform (`KnownEditors`) of install
  folders, looked at with file checks and folder listings off the UI thread, never by a `PATH` name
  on Windows and only as `.exe` or `.com` there, never in the registry; a known editor whose stored
  program is gone and that is found under its id elsewhere is updated in the setting.
  `EditorCatalog.MachineCatalogues` backs the session's `ToolCatalogues`; the harnesses give
  `ToolCatalogues.Fixed` (three editors under a root that does not exist, a fixed merge-tool list,
  Windows' default `PATHEXT`), so no picture shows what one machine has.
- **The setting** (`AppSettings.Editor`, an `EditorSetting(Id, Program, Arguments)`, null while none is
  chosen): a known editor's id, or `custom` for Other… with a program and arguments holding `{path}`
  and `{line}`. git's `core.editor` only marks a matching editor ("(git's editor)").
- **The menus** (D226): every file menu shows "Open in editor" after "Open file" only while an editor
  is chosen (`RepositoryMenuContext.HasEditor`, from the settings with no disk access); it never asks
  first, and while none is chosen the palette lists "Choose an editor…".
- **Starting it** (`EditorCommands.Build`, pure): the program by its absolute path, the arguments as a
  list with no shell anywhere, the file's absolute path, the program's folder (Windows) or the home
  folder as working directory, the app's environment without `ELECTRON_RUN_AS_NODE`. A program that
  is gone, or does not start, opens an error dialog with "Open settings…" (on Tools).
- **Open file's question** (D130 extended, design 12.8): it asks first for a file with an extension of
  the fixed list (which cannot be removed), the user's extra extensions
  (`AppSettings.ExtraRunnableExtensions`) or, on Windows, `PATHEXT`'s entries, and on Linux and macOS
  for a file marked as a program. Its file check and the read of the file's Unix mode run on the
  thread pool (`TabServices.FileExists`, D59), and it stops when the tab closed meanwhile.
- Tests: `KnownEditorsTests`, `EditorCatalogTests`, `EditorCommandsTests`, `FileMenuEditorTests`,
  `FileLauncherTests`, `RepositoryOperationsFilesTests` (App); check S17.

### Hosting: the provider and GitHub's API (D252, D257, D258, D284 to D286)

Phase 7a. `src/VisualCommit.Hosting` references Core alone (and `Tmds.DBus.Protocol` for Linux's
Secret Service, see "Tokens and the keychains"): no UI and no git process. Its namespaces are
`VisualCommit.Hosting` (the model, the session, `RepositoryHosting`, `AvatarCache`,
`AuthorMapStore`, `JsonAccountStore`), `.GitHub` (the provider, its client, recognition, GitHub's
JSON shapes) and `.Tokens`. `AssemblyInfo.cs` opens its internal types (the client, `GitHubUrls`,
`AuthorMapStore`, the stores' parsers) to `VisualCommit.Hosting.Tests`. Only github.com is
supported (D258).

- **Where GitHub is** (`HostingOptions.cs`): `GitHubEndpoints(WebBase, ApiBase)`, the web address
  (which also serves git and the fake's avatars) and the REST API's: `GitHubCom`
  (`https://github.com/`, `https://api.github.com/`) or `Local(port)` (`http://127.0.0.1:<port>/`
  and its `api/v3/`, the tests' fake); `Host` is the web address's authority, `github.com` or
  `127.0.0.1:<port>`. `HostingOptions(GitHub, CreateHandler)`: the endpoints, null while GitHub is
  off, and the handler every client is built on. `FromEnvironment` reads `VISUALCOMMIT_GITHUB_URL`:
  unset, empty or blank, github.com; `none` (any case), `Off`; `http://127.0.0.1:<port>/`, the final
  `/` optional and the port not 80 (no user part, path, query or fragment), `Local(port)`; anything
  else `Off` with a warning that names the variable, never its value. `Off`'s handler refuses every
  request. `DefaultHandler`: a `SocketsHttpHandler` that follows no redirect, takes every
  compression, keeps no cookies, has 10 s to connect, and uses no proxy for a loopback API (the
  system's otherwise).
- **The model** (`IHostingProvider.cs`, `HostingRecords.cs`, Core's `Hosting/AccountKey.cs`):
  `HostingProvider` (`GitHub`; 7b adds two); `AccountKey(Provider, Host, Login)`, host and login
  lower-cased, `Id` `github:github.com:octocat` (D263); `HostedRepository(Provider, Host, Owner,
  Name)`, equal without case as GitHub compares, `FullName` `owner/name`; `HostingAccount` (the key,
  the login in its own case, the user's id, name and avatar, `UseForGit`; never a token);
  `RepositoryInfo` (A2, A3: default branch, private, fork, archived, description, clone and web
  URLs, `UpdatedAt`, `CanPush`, a fork's `Parent` and `ParentDefaultBranch`); `RepositoryList`
  (`MoreExist`, `SomeHiddenBySso`) and `PullRequestList` (`MoreExist`); `PullRequest`
  (`HeadRepository` null for a deleted fork, `IsFromFork`); `NewPullRequest` (`HeadOwner` only
  across forks); `AuthorAvatar`, `AuthorMap` and `AuthorCommit(Email, Sha, Repository)` (D339);
  `HostingResult<T>`, a value or a `HostingFailure(Kind, Message, Detail, RetryAt, Url, RequestId)`
  of kind `Network`, `BadCredentials`, `SsoRequired`, `RateLimited`, `Forbidden`, `NotFound`,
  `ApiVersion`, `Validation`, `ServerError` or `Malformed`. `IHostingProvider` has `Kind`,
  `Endpoints`, `Recognise(url)`, `SourceCanExist(remoteRef)` and A1 to A6 below, each taking the
  token as an argument; the provider keeps none. The repository browser, the pull requests' section
  and the create form word a failure through `HostingTexts.Describe` (D338); the sign-in dialog has
  texts of its own.
- **Recognition** (`GitHub/GitHubUrls.cs`, internal, through `GitHubProvider.Recognise`): no
  network, and read by hand, not through `Uri`, which unescapes and normalises where git does not.
  On github.com the host is `github.com` or `www.github.com` (any case) in
  `https://[user[:password]@]host[:443]/o/r`, `http://[user@]host[:80]/o/r`,
  `ssh://[user@]host[:22]/o/r`, `ssh://[user@]ssh.github.com:443/o/r`, scp-like `user@host:o/r` and
  `git://host/o/r`; a password is accepted in `https` only. On other endpoints (the fake) only the
  web address's own scheme, host and port with no user part, so the tests' other loopback servers
  (47391, 47393) are never GitHub (ruling 23). The path is exactly two segments, then `.git` at most
  once and a `/`, with no query, fragment, `%` or `\`; the owner matches
  `^(?=.{1,39}$)[A-Za-z0-9][A-Za-z0-9-]*(?:_[A-Za-z0-9]+)?$` (an Enterprise Managed User's
  `mona-cat_octo` too, design 19.4) and the name `^[A-Za-z0-9._-]{1,100}$`, never `.` or `..`. The
  owner and name are kept as written, the user part never. An SSH or `git://` URL is recognised (the
  API goes by owner and name) but never bound (see "Prompts and credentials"). `SourceCanExist` is
  the push guard's rule (ruling 8): false only for `refs/pull/<x>/head` or `/merge` whose `<x>` is
  not all ASCII digits.
- **The client** (`GitHub/GitHubClient.cs`, internal, D285): one per provider, so one per session,
  over one `HttpClient` with no time-out of its own. `SendAsync` (one request, a body serialised
  through `GitHubJsonContext`) and `GetPagesAsync` (a list's pages) run on the thread pool.
  - Every request sends `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2026-03-10`
    (`GitHubApi`) and `User-Agent: VisualCommit/<version>` (the Hosting assembly's informational
    version up to its `+`); `Authorization: Bearer <token>` goes only to the API's own scheme, host
    and port.
  - One request at a time (a `SemaphoreSlim(1)` waited with the caller's cancellation), as GitHub
    asks. From then on a request has 30 s in all, for its hops and its body: the caller's
    cancellation throws `OperationCanceledException`, the 30 s running out is `Network` "GitHub did
    not answer within 30 seconds.", and no answer at all (or a cancellation that is neither, such as
    the handler's connect time-out) `Network` "Could not reach GitHub." (D284). A body over 10 MB is
    `Malformed`.
  - Redirects by hand: 301, 302, 307 and 308 are repeated to `Location` with the same method and
    body, at most three hops, and only while the target keeps the API's scheme, host and port; a
    target elsewhere, a fourth redirect or one without `Location` is `Malformed`, and nothing is
    sent there. A renamed or transferred repository's 301 to `repositories/<id>` is followed so.
  - Pages: `Link: rel="next"` is followed as given while it stays on the API's origin and under its
    path (else `Malformed`), up to the caller's cap; `MoreExist` when a next page is left; a failing
    page fails the list.
  - **Kept answers and conditional requests** (D285; design 16.10 and 19.2's leak-1): a GET's 200
    with an `ETag` is kept in memory under (the token's SHA-256 fingerprint, the URL that answered),
    with its body and its `Link`, `X-GitHub-SSO` and `X-OAuth-Scopes` headers. The next GET of that
    URL with that token sends `If-None-Match`, and a 304, which costs nothing of GitHub's rate
    limit, gives the kept body and the kept headers it lacks, so a repeated list still pages on and
    keeps "more exist". At most 512 answers and 32 MB of their bodies are kept, the oldest stored
    going first; a new 200 for a kept URL takes the newest place, and a body larger than the whole
    budget is not kept. Never on disk. A sign-out forgets them all (`GitHubProvider.ForgetKept`); an
    answer that a request still in flight brings is kept under its own token's fingerprint, which no
    other token's request finds, so a sign-in that replaces the account forgets nothing.
  - Classification, in this order: 401 `BadCredentials`; 403 with `X-GitHub-SSO: required; url=…`
    `SsoRequired` (`Detail` the URL); 403 or 429 with `X-RateLimit-Remaining: 0` `RateLimited`
    (`RetryAt` from `X-RateLimit-Reset`); 403 or 429 with `Retry-After`, or with a message naming
    the secondary rate limit, `RateLimited` (`RetryAt` now plus `Retry-After`'s seconds, at most a
    day, or its date, or 60 s); any other 403 `Forbidden` (`Detail`
    `X-Accepted-GitHub-Permissions`); 404 `NotFound`; 410, or a 400 whose first `errors` item or
    else whose `message` names `X-GitHub-Api-Version` (GitHub's refusal of a version: "Bad Request"
    with its explanation in `errors` as one string, D342), `ApiVersion` with that text; 422
    `Validation` (`Detail` the first error's `message`,
    else `<field> is invalid`, `is missing` or `already exists` by its code, else GitHub's
    `message`); 5xx `ServerError`; anything else (a 429 with none of the above included) and a body
    that does not parse `Malformed`, "GitHub's answer could not be read.". `errors` is read by
    `GitHubErrorsConverter` as an array of objects, an array of strings or one string, each string an
    item with that `message`; any other value is skipped, so it never costs the body its `message`.
    `Message` is GitHub's own
    `message` (ruling 15), else the reason phrase or `HTTP <status>` (a 5xx's "GitHub had a problem:
    HTTP <status>."); `Url` the address that answered, after any redirects (a renamed repository's
    `repositories/<id>`), without its query; `RequestId` `X-GitHub-Request-Id`. A 409 is no failure:
    its `Status` without a value (A6's empty repository). "Now" is the client's clock.
  - GitHub's JSON (`GitHub/GitHubJson.cs`): internal classes read through the source-generated
    `GitHubJsonContext` (snake_case, unknown fields ignored, nullable annotations respected); a
    field the app needs is `required`, so an answer without it is `Malformed`, never a half-filled
    record.
- **The provider** (`GitHub/GitHubProvider.cs`): paths relative to the API's address, each segment
  escaped. A token that is blank, or holds anything but visible ASCII, is refused without a request
  (`BadCredentials`, "The token is empty." or "Bad credentials").
  - A1 `CheckTokenAsync`: `GET user`, the account keyed by the endpoints' host and its login,
    `UseForGit` true.
  - A2 `ListRepositoriesAsync`: `GET user/repos?per_page=100&sort=updated`, never `type`, up to 10
    pages (1,000, ruling 14); `SomeHiddenBySso` when an answer's `X-GitHub-SSO` begins
    `partial-results`.
  - A3 `ReadRepositoryAsync`: `GET repos/{o}/{r}`, a fork with its `parent` and the parent's default
    branch; a moved repository is followed through the client's redirects and named as GitHub names
    it now.
  - A4 `ListPullRequestsAsync`: `GET repos/{o}/{r}/pulls?state=open&per_page=100`, up to 3 pages
    (300, ruling 14).
  - A5 `CreatePullRequestAsync`: one `POST repos/{o}/{r}/pulls` on the base repository, never
    retried: `head` the branch, or `owner:branch` across forks with `maintainer_can_modify: true`.
  - A6 `ReadAuthorsAsync(token, repository, branch, lookups)` (D252, D339): with a branch, the first
    page of `repos/{o}/{r}/commits?sha={branch}&per_page=100`, each commit's lower-cased author
    e-mail to its GitHub user (the first commit of an e-mail wins; null where GitHub links none), a
    409 (an empty repository) ending the call with an empty map and no lookup; without a branch, no
    page. Then at most 20 lookups (`MaxAuthorLookups`) of the given `AuthorCommit`s whose e-mail the
    map lacks, in order, each `repos/{owner}/{name}/commits?sha={sha}&author={e-mail}&per_page=1` in
    its own repository from its own commit. A lookup answered 409, or 404 in a repository other than
    the listed one, leaves out the rest of that repository's in the call; one answered 422, a 5xx or
    404 in the listed repository (a commit GitHub lacks: github.com answers it 500 with an empty body
    beside `author`) leaves out its own address and the others go on (D342); any other failure stops
    them. An address left out is not in the map, so it is asked again at a later run.
  - Avatars' addresses are kept only when `https` on `avatars.githubusercontent.com` (github.com) or
    on the web address's own origin (the fake); any other makes the author unknown, with one warning
    per provider that names that address's scheme, host and port only.
- **The avatars' images** (`AvatarCache.cs`, D286; one per session, made anew at each sign-out):
  `FileOfAsync(AuthorAvatar)` gives the local image's path or null. Images are `avatars/<user
  id>.png` or `.jpg` beside `index.json` (`{"users":{"1003":{"file":"1003.png","fetchedAt":"…"}}}`,
  camelCase through a context of its own, D336). A kept image is used while its fetch date, by the
  session's clock, is under 7 days old; otherwise it is fetched again, and a failed refresh keeps
  it. A download asks for the avatar URL with `s=64` in its query, with `Accept: image/png,
  image/jpeg`, the client's `User-Agent` and never a token, on an `HttpClient` of its own (not the
  API client's one request at a time); 30 s, redirects by hand only on the avatar URL's own scheme,
  host and port (at most three), at most 1 MB, kept only with PNG's or JPEG's signature; four
  downloads at once and one per user. The bytes go to a temporary file, flushed, then, under the
  index's lock, are moved over the image, the other type's image is removed and the index rewritten
  through `DurableFile.Replace`; a cancelled call writes neither. The index is read once: an entry
  that is not a user's id naming that user's image is dropped with a warning, one whose image is
  gone silently (one listing of the folder, design 19.3's perf-2). Every file operation runs on the
  thread pool; a failure is a warning that names the user's id (and a redirect's scheme, host and
  port), never the image's address, and null.
- Tests: `HostingOptionsTests`, `GitHubRecognitionTests`, `GitHubClientTests`,
  `GitHubProviderTests`, `AvatarCacheTests`, `HostingSmokeTests` (`VisualCommit.Hosting.Tests`, over
  `FakeGitHub`'s in-process handler or a recording one; see "The fake of GitHub").

### Hosting: the session and the account (D253, D255, D256, D263, D287, D288, D291, D292, D330)

Phase 7a. `IHostingSession` (`IHostingSession.cs`) is the app session's hosting, one per
`AppSession` (`TabServices.Hosting`), and `HostingSession` (`HostingSession.cs`) the one
implementation; `NullHostingSession` (GitHub off: no account, no binding, nothing recognised, no
request) stands in where tests build `TabServices` by hand. The interface extends Core's
`IGitCredentialSource`, so the prompt broker asks the session for a bound URL's credential and
tells it of git's `erase` (see "Prompts and credentials").

- **Off and on**: `HostingSession(paths, options, tokens, log, post, clock)`. While `options.GitHub`
  is null (`IsOff`) it makes nothing and logs "GitHub: off.": nothing is recognised, no store is
  touched, `StartAsync` reads nothing, `ForRepository` gives `NullRepositoryHosting.Instance`. On,
  it logs "GitHub: {host}." and makes one `GitHubProvider`, one `TokenVault` over the store (see
  "Tokens and the keychains"), one `AvatarCache` over `avatars/` and one `AuthorMapStore` over
  `avatars/authors/`.
- **`accounts.json`** (`JsonAccountStore.cs`; D287, D292, D336): `{"version": 1, "accounts":
  [{"provider": "github", "host": "github.com", "login": …, "id": …, "name": …, "avatarUrl": …,
  "useForGit": true, "tokenState": "keychain"}]}`, camelCase and indented, comments and trailing
  commas read; never a token, nor a field that could hold one. `tokenState` (`AccountTokenStates`)
  is `keychain` (saved in the keychain), `session` (kept in memory only) or `refused` (GitHub
  refused it); missing or unknown reads as `keychain`, and a missing `useForGit` as true
  (`AccountRecord.UseForGit` has a setter, not `init`, for that). The session uses the first
  `github` record of its host (`AccountsFile.GitHubOn`); a save replaces that record or appends one,
  and other hosts' records are kept and ignored. A missing file is no account; an unreadable one is
  moved to `accounts.unreadable.json` with a warning and the session starts with none. The file is
  written whole through `DurableFile.Replace`, under the session's gate and off the UI thread; a
  failed write is logged as an error and the state in memory changes anyway.
- **The slot and the gate** (design 7.3): the account is one `AccountSlot` (internal: its
  `AccountStatus`, the token while signed in, the record's `tokenState`; a class whose `ToString`
  gives the login and the state only), swapped whole (`Volatile.Write`) and read without a lock, so
  git's credential requests and the bindings never wait on anything. `AccountStatus(Account, State,
  Place, NoteKind, Note)`: `SignedIn` or `NeedsSignIn`; `TokenPlace.Keychain` or `Session`; which of
  the window's notes applies (`AccountNoteKind`: `NoKeychain`, `SaveFailed`, `Refused`,
  `TokenMissing`, `ReadFailed`, `SessionEnded`) with the store's reason. One `SemaphoreSlim(1)` runs
  the start's read, a sign-in's second half, a sign-out, a refusal and the switch one at a time; no
  request to GitHub runs under it. A change writes the record first, then the slot
  (`SetSlotLocked`), and posts `AccountChanged` and every live `RepositoryHosting`'s
  `OnAccountChanged` through the post delegate (`Dispatcher.UIThread.Post` in the app), which drops
  them once the session is disposed.
- **`StartAsync`** (ruling 6, D287), once (`AppSession.HostingStarted`; a second call waits for the
  first): under the gate it loads `accounts.json`. Without a record for the host no store is touched
  ("GitHub: no account on {host}."). A `session` record needs sign-in (`SessionEnded`), a `refused`
  one too (`Refused`), neither touching the store; for a `keychain` one the vault reads the store,
  the run's only read: a token signs it in (`Keychain`), none gives `TokenMissing`, a failed read
  `ReadFailed` with the store's reason. No request to GitHub. Logged as "GitHub account {login} on
  {host}: token read from the keychain." or the outcome's other words. Until it ends `Account` is
  null.
- **`SignInAsync(token)`** (D256: at once; D263: replaces; D288): A1, under the caller's
  cancellation linked to the session's lifetime and outside the gate; a failure changes nothing (a
  new token's 401 never marks the current account) and is a warning. Once GitHub has answered, the
  rest runs under the session's lifetime alone, so a dialog's Cancel no longer stops it: it waits
  for the start, takes the gate, forgets the previous record's keychain entry when that login
  differs, carries the previous record's `useForGit` (true without one), and keeps the token through
  the vault: in the keychain (`keychain`); in memory where no keychain exists (`session`,
  `NoKeychain`, D253); or in memory after a failed save (`session`, `SaveFailed`, the reason as note
  and as `SignInResult.SaveFailedReason`, ruling 19). Logged as "… signed in; the token is kept in
  the keychain." or "… kept for this session: {reason}.".
- **`SignOutAsync`** (D291), on the thread pool: it waits for the start and the gate under the
  caller's cancellation, and from then on neither that nor the session's disposal stops it (the
  process's exit can: an exit during the store's delete leaves the record and its entry, and the
  next start is signed in; design 19.2's life-1). The vault forgets the entry and its reason is
  returned; a run without a keychain that finds a `keychain` record returns
  `TokenStoreReasons.NoKeychain` and warns, naming the entry to remove by hand (tokens-1). The
  record goes even when the entry stays; the slot is cleared, the client's kept answers forgotten
  and the events posted. Then, still under the gate, the avatar runs are cancelled and awaited, the
  e-mail maps leave memory (their lookup counts stay), the `AvatarCache` is replaced by a new one,
  and `avatars/` is deleted whole; a file that stays gets one warning naming it.
- **`SetUseForGit`** (D216, D255; the settings screen's Save): on the thread pool, after the switch
  before it; under the gate, no slot or the same value changes nothing; else the record, the slot
  and the events, and every binding follows.
- **git's `get` and `erase`** (rulings 4 and 6, D330): `GetAsync(account)` waits for the start's
  read at most 10 s by the session's clock (then a warning and null) and gives
  `GitCredential(token)` only while that account is signed in with "Use for git over HTTPS" on.
  Through git no `get` comes before the read ends, since nothing is bound until the slot holds the
  token (design 19.2); the wait guards a direct call. `Refused(account)` returns at once; for the
  signed-in account it starts one A1 with its token, one at a time (an `erase` meanwhile joins it;
  `RefusalCheck` for tests): a 401 marks the account refused; a 200 keeps it signed in with the
  warning "… git was refused, yet the token is valid (an http.extraHeader, a proxy or a
  redirect?).", any other answer with "… git was refused; the token could not be checked: …".
- **Calls with the account's token** (`CallAsync`, internal; D257): every API request after the
  sign-in but the refusal check's A1 (above, which marks the account itself) goes through it. Off,
  or without a signed-in token, it fails at once without a request ("GitHub is turned off.", or
  `BadCredentials` "Not signed in."). Otherwise the provider runs with the account's token, under
  the caller's cancellation linked to the session's lifetime. A `BadCredentials` answer marks the
  account (`MarkRefusedAsync`, under the gate and only while the same key and the same token are
  signed in, so a stale call's 401 never marks a newer token: `NeedsSignIn`, `Refused`, the token
  dropped, `tokenState` `refused`, a warning naming its source, "git" or "the API, request {id}");
  any other failure is a warning naming what was asked. `ListRepositoriesAsync` (A2, the repository
  browser's) and `RepositoryHosting`'s reads and create go through it.
- **For the rest of the app**: `Recognise(url)`; `SourceCanExist(remoteUrl, remoteRef)` (true while
  off or for a URL that is not GitHub's); `BindsGit` (signed in, the token in hand, "Use for git
  over HTTPS" on); `BindingsForClone(url)` (a clone's URL after the user's `insteadOf`, D331) and
  `BindingsFor(remoteUrls, rewrites)` (a dashboard row without a tab, D332), each
  `CredentialBinding.Select` with the account's key while `BindsGit`, else empty;
  `KeychainAvailableAsync` (the vault's cached probe, which never prompts); `ForRepository`.
- **Avatar work** (`RunAvatarWork`, internal): a tab's avatar run starts on the thread pool only
  while signed in and not disposed, under the tab's lifetime linked to the session's avatar work,
  and is tracked, so that a sign-out cancels and awaits it before deleting `avatars/` (D291).
- **`Dispose`** (`AppSession.Dispose`, after the tabs) cancels the lifetime (the start's read, a
  sign-in's second half, the refreshes, the refusal check) and the avatar work, disposes the API's
  and the avatars' clients and posts nothing more; a begun sign-out goes on (above).
- **What the hosting logs, and never** (design 7.12, 17.1): "GitHub: {host}." or "GitHub: off." at
  the start (the real-window pass waits for it); the start's outcome, a sign-in with the token's
  place and any reason, a sign-out, the switch, each as "GitHub account {login} on {host}: …";
  warnings for a failed sign-in ("GitHub: signing in on {host} failed: {kind}, {message} (request
  {id})"), for a refusal (its source and request id), for git's `erase` of a token the check found
  valid or could not check, for a `get` that went without the token after the 10 s wait for the
  keychain's read, for a keychain entry kept (a replaced account's, or one to remove by hand), for a
  failed call ("GitHub: {what} failed: {kind}, {message} (request {id})"), for a tab's unexpected
  failure of its pull requests or avatars ("{repo}: the pull requests could not be read: …", "… the
  authors' avatars could not be read: …") and, from the client, one per failed request: `GitHub:
  <method> <path> failed: <status or kind> (request <id>).`, the path without its query (so no
  e-mail address of A6's lookups) and, for a request that got no answer, the exception; the avatars'
  warnings name the user's id, and the provider warns once of an avatar on another host; Debug
  `{repo}: listed {n} open pull requests of {owner/name}` (D322). Never a token, a `GitCredential`,
  a slot, an `Authorization` value, a query or an HTTP request or response: of an answer only its
  status, GitHub's `message`, the request id and the scheme, host and port of a refused `Location`
  or `Link` or of an avatar's address. The API's requests are not git calls: the call log and the
  activity log hold none of them.
- Tests: `JsonAccountStoreTests`, `HostingSessionStartTests`, `HostingSessionSignInTests`,
  `HostingSessionCredentialTests` (`VisualCommit.Hosting.Tests`, through `HostingSessionSetup`: the
  fake in process, a `FakeTokenStore`, a data folder of its own, a `ManualTimeProvider` and a
  `HostingTestThread` as the post delegate); `AppSessionTests` (App: a session in the tests'
  environment logs "GitHub: off.").

### Hosting: a repository's pull requests and avatars (D251, D289, D290, D293, D311, D339)

Phase 7a. `RepositoryHosting` (`RepositoryHosting.cs`, `.Avatars.cs`) is a repository's hosting
behind `IRepositoryHosting`, one per full-mode `RepositoryViewModel`, made by
`IHostingSession.ForRepository(workingDirectory)` and told of the account's changes until its
`Dispose`. While GitHub is off, in summary mode (until a tab adopts the view model) and without
services the view model holds `NullRepositoryHosting.Instance`, which is `NotGitHub`, binds nothing
and never changes. `HostingRules` holds its pure functions, and the session's `AuthorMapStore` the
e-mail maps.

- **What it holds**: `State`, a `RepositoryHostingState` of `PullRequests` (`NotGitHub`,
  `SignedOut`, `Loading`, `Failed`, `Loaded`), `Remotes` (each GitHub remote to its repository),
  `Repository` (the listed one), `List`, `Failure` and `ByRef`; `Bindings` (which auto-fetch's
  operations also carry); `Avatars` (`AuthorAvatars`, lower-case e-mail to a local PNG or JPEG);
  `WantsUrlRewrites` (signed in with the token in hand). The first three are swapped whole and read
  from any thread; they change on the UI thread, and `Changed` is posted only when one of them
  differs, compared by content, or `WantsUrlRewrites` does. Network work runs off the UI thread
  under the hosting's lifetime, linked to the session's and cancelled at `Dispose`, and comes back
  through the post delegate.
- **`Update(refs, rewrites)`**, after each refs read, on the UI thread (design 7.8): the URLs git
  uses (`rewrites.Apply(refs.RemoteUrls)`), the rewrites counting as known only when they were
  wanted for this read (D290); each remote recognised by its first fetch URL after the rewrites,
  else by its first configured `url` (`HostingRules.Remotes`); the candidate, the first GitHub
  remote in `HostingRemoteOrder` (`origin`, `upstream`, then by ordinal name;
  `HostingRules.ListedCandidate`). A changed candidate drops the list and its failure and, while
  signed in, starts a refresh (D289). A candidate gone also stops the avatar runs and drops the
  avatars (D252); a change to another GitHub repository keeps them, since the tab sends its authors
  again only after `NotGitHub` or a sign-out.
- **States**: no candidate `NotGitHub`; not signed in `SignedOut`, with no request (D257); signed
  in, `Loaded` with a list, else `Failed` with a failure, else `Loading`. A refresh shows `Loading`
  over an earlier failure but keeps a held list (`Loaded`) until its answer, and a failure carries
  no list (the left panel keeps its rows itself; see "Left panel and commit details").
- **The listed repository** (`ListedOf`, `RemotesOf`; design 19.2's integration-1): the candidate
  before A3 has answered for it; then A3's parent when A3 says fork, else A3's own repository, which
  is the candidate under the name GitHub gives it now (renamed or transferred, the client having
  followed GitHub's 301). Once A3 names the candidate otherwise, every remote equal to it takes A3's
  name in `State.Remotes` (equal without case; the listed repository shows GitHub's case), so the
  list, `ByRef`, a pull request's checkout, the create form and the avatars compare one name with
  GitHub's answers; the recognised remotes and the candidate stay as the URLs say, so an `Update`
  does not change with A3. Not covered: a renamed parent reached through an `upstream` remote under
  its old name (plan.md's risk 36). A3's last answer is kept with its candidate for the hosting's
  life (a failure is not kept), and one read in flight is shared; `ReadRepositoryAsync`, which the
  create form calls, gives it.
- **`RefreshPullRequestsAsync`** (D289): not signed in or without a candidate, nothing. One run at a
  time: A3 (kept), then A4 on the listed repository through the session's `CallAsync`; a call during
  a run asks for one more, whose applied answer completes its task, and the caller's cancellation
  ends only its wait. The answer is applied on the UI thread while the candidate is still the run's
  and an account is signed in, and a list then writes the Debug line `{repo}: listed {n} open pull
  requests of {owner/name}` after the state's `Changed` (D322). An unexpected exception becomes a
  `Network` failure with its message, never a section left loading. The triggers, never a timer: the
  first `Update` with a candidate while signed in, a changed candidate, a sign-in
  (`OnAccountChanged`), each succeeded network operation of the view model's queue (auto-fetch and a
  create included; the repeated read is answered 304), and the section's menu.
- **Bindings** (`HostingRules.Bindings`, D290): none unless signed in with the token in hand, "Use
  for git over HTTPS" on and the rewrites known for the last `Update`; else
  `CredentialBinding.Select` of the URLs git uses on GitHub's web address with the account's key
  (D278; see "Prompts and credentials"). The tab's network operations carry them.
- **`ByRef`** (`HostingRules.ByRef`, D313): the open pull requests by head repository and head ref,
  the lowest number winning and a deleted fork's mapping nothing; a local branch `refs/heads/x` maps
  through an upstream of the same name on a GitHub remote, a remote branch through the source branch
  of its fetch line (`RepoRefs.SourceOf`); one pass over the refs. The graph's badges, the branch
  rows and the menus read it.
- **`CreatePullRequestAsync`** (A5 through `CallAsync`, D293): a pull request created on the listed
  repository goes on top of the held list at once, before the refresh that confirms it.
- **`OnAccountChanged`**, on the UI thread: signed in, the bindings and the state again and a
  refresh; signed out or needing sign-in, the list, its failure, `ByRef`, the avatars and the
  bindings go, and the rewrites count as unknown until an `Update` that wants them.
- **Avatars** (`ShowAvatarsFor(IReadOnlyCollection<AuthorCommit>)`; D252, D311, D339; design 7.9,
  19.3): the tab gives the authors of the first 2,000 rows of its graph whose commits a GitHub
  remote's remote-tracking branch reaches, at most 200, each with its newest such commit and that
  remote's repository (see "Commit graph"); copied, lower-cased, one per e-mail. Signed out, nothing
  happens. One run at a time, a call during a run folding into one more with the latest set; a run
  applies nothing once a sign-out, the tab's close or a lost GitHub remote has moved its generation.
  Off the UI thread: A3 (kept); the branch, the parent's default branch for a fork, else the default
  branch; `AuthorMapStore.Plan` for the listed repository; when anything is due, one A6 with the
  branch when the commits page is due and the due e-mails' `AuthorCommit`s as lookups, its answer
  taken into the map (`AuthorMapStore.Merge`, which gives back the lookups not reached, all of them
  when A6 failed); a failed A6 applies nothing. Then `AvatarCache.FileOfAsync` for each e-mail the
  map links to a user, four at a time; the files found become `Avatars`. An address GitHub does not
  know, or whose image could not be had, gets no entry and looks as before phase 7a.
- **The e-mail maps** (`AuthorMapStore.cs`, internal; D291, D336): one per listed repository, shared
  by its tabs, in `avatars/authors/github_<host>_<owner>_<name>.json` (lower case, `:` as `_`):
  `{"fetchedAt": …, "authors": [{"email", "userId", "login", "avatarUrl", "checkedAt"}]}`, the
  user's fields null for an address GitHub does not know. The commits page is due when the map is
  missing or its page is 7 days old; an address when it is missing or its answer is 7 days old, as
  many as the repository's 20 lookups per app session have left (the addresses passed into the one
  A6 count, those the page then answers included; design 17.1). Read from the disk at first use (an
  unreadable file is a warning and made again), written whole through `DurableFile.Replace` under
  one lock; a sign-out drops the maps from memory but not the counts.
- **The tab's side** (`ViewModels/RepositoryViewModel.Hosting.cs`, design 7.10): after each refs
  read `ApplyHosting` gives the refs to `Update`, signed out with no rewrites. Signed in it gives
  them with the user's URL rewrites as last read and reads those again (one `git config --null
  --get-regexp ^url\.`, D281, off the UI thread; a newer read drops an older one's answer), calling
  `Update` once more after its first read and whenever they changed; before its first read it calls
  nothing, so it has no bindings from URLs not yet rewritten (D290). A sign-in while the tab is open
  starts the read, and `IsWatcherQuiet` is false while one runs (`IsHostingQuiet`). A succeeded
  network operation of the queue refreshes the pull requests. A summary-mode view model gets a real
  hosting when a tab adopts it (`PromoteHosting`).
- Tests: `HostingRulesTests`, `RepositoryHostingTests` (a renamed repository among them, through the
  fake's `Rename`), `RepositoryHostingAvatarsTests`, `RepositoryHostingWiringTests`
  (`VisualCommit.Hosting.Tests`); `RepositoryAvatarsTests` (`VisualCommit.VisualTests.Phase7a`);
  parts P, N and G of the walk-through show them.

### Tokens and the keychains (D249, D253, D259, D268 to D272, D333)

Phase 7a. `src/VisualCommit.Hosting/Tokens/`, namespace `VisualCommit.Hosting.Tokens`. A GitHub
token is kept in this computer's keychain and never in a file (R3); where no keychain can be
reached, or a save fails, it is kept in memory for the app session only (D253). The stores are the
app's own: advapi32's credential calls on Windows, `/usr/bin/security` on macOS, the Secret Service
over D-Bus on Linux (Tmds.DBus.Protocol 0.94.1). The hosting session holds the one `TokenVault`,
over the store `AppSession.Start` was given or chose (see "Hosting: the session and the account");
nothing else calls a store, and no request of git reads one (ruling 6).

| Type | Role |
|---|---|
| `TokenKey`, `TokenStoreProbe`, `TokenStoreOutcome`, `TokenStoreResult`, `TokenRead` (`ITokenStore.cs`) | Which token an entry holds: provider, host and account, in lower case (`TokenKey.Of(AccountKey)`, text `github:github.com:octocat`); whether a store can keep tokens, and why not; `Done`, `NotFound` or `Failed` with a reason; a read's outcome and token, whose text never shows the token |
| `ITokenStore` | `Name` (for the log only: no shown text names a store, ruling 17), `KeepsTokensAcrossRestarts`, `ProbeAsync` (bounded; it never prompts and never unlocks, creates or reads an entry), `SaveAsync`, `ReadAsync`, `DeleteAsync`. An expected outcome other than success (not found, locked, refused, too long, a time limit) is a result, never an exception: `NotFound`, or `Failed` with a `TokenStoreReasons` text; every wait honours the call's cancellation token. A constructor does nothing (no process, P/Invoke or D-Bus connection), so a store is touched only when the vault first needs it. No call blocks its caller's thread (Q2) |
| `TokenStores` | The choice (below): `Variable` (`VISUALCOMMIT_KEYCHAIN`), `TestsVariable` (`VISUALCOMMIT_KEYCHAIN_TESTS`), `FromEnvironment` |
| `MemoryTokenStore` | A dictionary of the process; `KeepsTokensAcrossRestarts` false. `MemoryTokenStore(true)`, "memory", is available; `MemoryTokenStore(false)`, "memory, no keychain", answers its probe with `NoKeychain` and fails every save with it |
| `WindowsCredentialStore`, `WindowsCredentialNative` | Windows Credential Manager ("Windows Credential Manager", design 3.4): one generic credential per key, kept for the local machine (`CRED_PERSIST_LOCAL_MACHINE`), its blob the token's UTF-16LE bytes, through `CredWriteW` (which replaces an entry of the same target), `CredReadW`, `CredDeleteW` and `CredFree`, each on the thread pool. A save first asks `Fits`: a token over 1,280 characters (2,560 bytes, `CRED_MAX_CREDENTIAL_BLOB_SIZE`) is `TooLong` before any call. The probe calls `CredGetSessionTypes` and is available when the logon session keeps generic credentials at local machine, else `CannotUse`. Error 1168 is `NotFound`, 1312 (no logon session) `CannotUse`, any other `Refused`; a blob that is not whole UTF-16 is `Unreadable`. The unmanaged blob and every buffer that held the token are cleared. Off Windows every call past the length check answers `NoKeychain` |
| `MacKeychainStore`, `ITokenProcessRunner`, `TokenProcessRunner`, `TokenProcessResult` | The macOS keychain ("macOS keychain", design 3.5) through `/usr/bin/security`, always by full path: one generic password per key, whose secret is the token's UTF-8 in base64. A save runs `security -i` and writes one line to its standard input, `add-generic-password -U -s '<service>' -a '<account>' -l '<label>' -w '<base64>'`, so the token is never an argument and the quotes hold only base64; `security -i`'s exit code after a failed command is not known, so the entry is read back and compared (another token or none is `Refused`, a read that fails gives its own reason), unless standard error said "User interaction is not allowed" (`CannotUse`) or "User canceled the operation" (an unlock dialog dismissed, `Locked`). A read is `find-generic-password -s … -a … -w`, its output strict base64 of strict UTF-8 (else `Unreadable`); a delete `delete-generic-password -s … -a …`; exit 44 or "could not be found" is `NotFound`. Each command has 60 s, since macOS may show its own unlock dialog (past it `NoAnswer`). The probe: no `security` is `NoKeychain`; else `security default-keychain -d user` within 5 s, available on exit 0 with output. A keychain file (the real-store test's) is named in every command and is available when it exists; a path holding `'`, `\` or a control character is refused. `TokenProcessRunner` starts the program with `ArgumentList` and UTF-8 streams, kills its process tree at the limit (the result then has no exit code) or on a cancel, and logs nothing; `TokenProcessResult`'s text gives the exit code only, since a read's output is the token in base64. Tests put a recording runner in its place |
| `SecretServiceStore`, `SecretServiceOptions`, `SecretServiceLimits`, `SecretServiceAddress`, `SecretServiceCalls` | The Secret Service on Linux ("Secret Service", design 3.6, D271) through Tmds.DBus.Protocol's message reader and writer. The bus (`SecretServiceAddress.Resolve`): a non-empty `DBUS_SESSION_BUS_ADDRESS`, else `unix:path=$XDG_RUNTIME_DIR/bus` when that socket exists, else none, so a machine without a bus finds no keychain at once (never Tmds.DBus's own lookup, whose X11 fallback this store must not try). The probe connects (2 s), asks `NameHasOwner("org.freedesktop.secrets")`, else `ListActivatableNames` (5 s each), and opens and closes a `plain` session (10 s); it never unlocks, creates or reads, so it shows no dialog. Each operation opens one connection and one `plain` session, closed in `finally`; each call has 25 s and each prompt of the service (an unlock, a new collection) 120 s, past which, or on a cancel, the prompt is dismissed. A read searches every collection by four attributes (`application`, `provider`, `host`, `account`), unlocks only when only locked items match, and reads the first item's secret as strict UTF-8 (else `Unreadable`). A save goes into the `default` alias's collection, else `collection/login` when its `Locked` can be read, else a new collection "Login" made the default; it unlocks a locked one, then `CreateItem(…, replace: true)` with all five attributes, the item's path taken from the prompt's result when the call gives none. A delete searches, unlocks the locked matches and deletes every one. A dismissed prompt or `IsLocked` is `Locked`, any other error reply `Refused`, a limit `NoAnswer`, a failed connection `NoKeychain`; the secret's bytes are cleared |
| `TokenEntryNames` | Every entry's names (design 3.3, below), all starting with the application's name and never with `git:` or `gh:`, so the app never touches an entry of Git Credential Manager or GitHub CLI. A key part that is empty or holds white space, a control character, `'` or `\` is refused before any call (`Failed`, `Refused`): it could break `security -i`'s quoting or name another entry. `ForLog(key)` gives the log's `{host}/{account}`, a control, separator or format character and `\` written as `\uXXXX` |
| `TokenStoreReasons` | Why a store could not keep, read or delete a token: one of seven fixed texts that name no store and no operating system (D268), so that the Accounts section may show one and the pictures are the same on every system: `NoKeychain` "no keychain was found on this computer", `CannotUse` "the keychain cannot be used in this session", `Locked` "the keychain stayed locked", `NoAnswer` "the keychain did not answer in time", `TooLong` "the token is too long for the keychain" (Windows), `Unreadable` "the keychain's entry could not be read", `Refused` "the keychain refused it" |
| `TokenVault` | Where the hosting session keeps tokens: memory first, the store behind it (below) |

The names of `TokenKey("github", "github.com", "octocat")`'s entry:

| Store | Names |
|---|---|
| Windows | target `VisualCommit:github:github.com:octocat` (`TokenEntryNames.Entry`, also how the log names an entry for whoever removes it by hand), user `octocat`, comment `GitHub token for octocat, kept by VisualCommit` |
| macOS | service (`-s`) `VisualCommit:github:github.com`, account (`-a`) `octocat`, label (`-l`) `VisualCommit: GitHub (octocat)` |
| Linux | attributes `application=VisualCommit`, `provider=github`, `host=github.com`, `account=octocat` and, on an item the app creates, `xdg:schema=io.github.rahimpasha.VisualCommit.Token`; label `VisualCommit: GitHub token for octocat` |

The fake of GitHub's host gives `VisualCommit:github:127.0.0.1:47395:octocat`; the real stores'
tests name their entries with the application `VisualCommit-test:<guid>`.

- **The choice** (`TokenStores.FromEnvironment(readEnvironment, log)`, D253, D269):
  `VISUALCOMMIT_KEYCHAIN` unset or empty gives the platform's store (`WindowsCredentialStore`,
  `MacKeychainStore`, `SecretServiceStore`; elsewhere `MemoryTokenStore(false)`), `memory` a
  `MemoryTokenStore(true)` and `unavailable` a `MemoryTokenStore(false)`, each value trimmed and
  without case, logged as "Token store: {Name}."; any other value gives `MemoryTokenStore(true)` and
  the Warning "VISUALCOMMIT_KEYCHAIN is neither memory nor unavailable: tokens are kept in memory.",
  which never repeats the value (a pasted token could be one), and never a real store. No store is
  touched. `AppSession.Start` passes the process's environment unless it is given a store (see
  "Start-up and composition"); every test process has `VISUALCOMMIT_KEYCHAIN=memory`, and the
  headless harness gives the session a `FakeTokenStore` (see "The fake of GitHub").
- **The vault** (`TokenVault(store, log)`, D272, design 3.7 and 16 item 4). The store's probe is
  made once per run, without any caller's token, and logged once ("Keychain: available." or
  "Keychain: not available ({reason})."); `KeychainAvailableAsync` gives its answer. A key's keeps
  and forgets reach the store in the order they were called, each after the one before has ended,
  and a store read waits for the key's last one, so memory and the keychain end alike.
  - `KeepAsync(key, token, ct)` puts the token in memory at once and drops the key's recorded read;
    without a keychain it gives `(Session, NoKeychain)`; else it calls the store's save, which from
    then on no caller's token stops (only the store's own limits), so no entry is left half-written
    and no second prompt of the system follows: `(Keychain, null)` (Debug), or `(Session, reason)`
    (Info) after a failed save, which leaves the store alone: an older entry may stay, and its
    token is refused at its first use.
  - `ReadAsync(key, ct)` answers from memory; else from the key's one store read of the run, shared
    by concurrent callers and kept whatever it found (ruling 6: a store is read at most once per key
    and run); without a keychain `Failed(NoKeychain)`. A token read goes into memory unless a keep
    or a forget came since the read began. The caller's token ends its wait, never the read.
  - `ForgetAsync(key, ct)` drops memory and the read at once, then deletes the entry: null when it
    was deleted, was not there, or no keychain is reachable; else the store's reason, with the
    Warning "The keychain kept the entry {VisualCommit:github:github.com:octocat}: {reason}. Remove
    it by hand.". The caller's token ends its wait, never the delete.
- **The hosting session's calls** (see "Hosting: the session and the account"): the start reads
  the store once, and only for a record whose token state is `keychain`, so a user who never signs
  in never touches a keychain; a sign-in keeps the token; a sign-out, and a sign-in that replaces
  another account, forget its key, and when this run reached no keychain while the record says
  `keychain` the session warns with the entry's name, since the entry may stay (design 19 item 2).
- **Secrets in records** (design 3.2): no token, nor its base64, in a log line, an exception's
  message, a process argument, an environment variable, a file or a `ToString()`. Each store logs
  its platform's detail itself, a failure at Warning and a success at Debug, naming the key as
  `{host}/{account}`: the Win32 error; `security`'s exit code and first error line with the token
  and its base64 replaced by `***`; the D-Bus error's name. Of an unexpected exception during a
  save, a read or a delete the macOS and Linux stores log the type alone, since its text could
  quote what was sent.
- **macOS's exposure** (ruling 20, plan risk 34): an item made through `security` trusts
  `/usr/bin/security`, so any process of the user can read it through `security` without a prompt,
  as on Windows and Linux; accepted until phase 8 signs the app.
- **Tests** (D270): in `tests/VisualCommit.Hosting.Tests/Tokens/`, `TokenEntryNamesTests`,
  `TokenVaultTests` (a scripted store with held calls), `TokenStoresTests`,
  `WindowsCredentialStoreTests` (`Fits` as a pure method; on Windows no call reaches the Credential
  Manager: the only calls made are refused before it, a save that `Fits` refuses and the calls of a
  key that cannot name an entry), `MacKeychainStoreTests` (a recording runner: the program by full
  path, the standard input line, the read-back, the error texts) and `SecretServiceStoreTests` (the
  address, a missing socket, a silent bus); none reaches a keychain, and each asserts that no log
  line holds the token. The real stores' own classes, `WindowsTokenStoreRealTests`,
  `MacKeychainTokenStoreRealTests` and `SecretServiceTokenStoreRealTests`, skip
  (`Assert.SkipUnless`) unless `VISUALCOMMIT_KEYCHAIN_TESTS` names them (`windows`, `macos`,
  `secret-service`): under a `VisualCommit-test:<guid>` application with dummy secrets they probe,
  read nothing, save, read, save another, read, delete, read nothing and delete nothing, and delete
  again in `finally` (`RealTokenStoreSteps`); Windows' also saves 1,280 characters, and macOS's
  makes a keychain file of its own and deletes it. A default run shows four skipped tests on Windows
  (`WindowsCredentialStoreTests.Off_Windows_every_call_finds_no_keychain` among them, whose calls
  would reach the Credential Manager there) and three on macOS and Linux. Only CI's three keychain
  steps run the real classes, also after a failed Test step (see "CI"); on their first run the
  Windows runner kept credentials (no error 1312), so D333's fallback of taking the class off that
  job was not needed.

### Clone from GitHub (D261, D299 to D302, D331)

Phase 7a. `ViewModels/Dialogs/CloneFromGitHubDialogViewModel.cs`,
`Views/Dialogs/CloneFromGitHubDialogView`, `WelcomeViewModel` (the form's button and the clone's
binding), `MainWindowViewModel.Commands.cs` (`CloneFromGitHubAsync`),
`ViewModels/Operations/OperationTexts.Hosting.Clone.cs` (`CloneTexts`).

- **The form's "From GitHub…"** (D261, D302). The clone form's URL row is a `DockPanel`:
  `CloneFromGitHubButton` ("From GitHub…", `VcActionButton`, 6 right of the box, no tooltip) docked
  right of `CloneUrlBox`. It shows only while the account is signed in
  (`WelcomeViewModel.ShowsGitHubButton`; an account that needs sign-in hides it too), raised again
  at every `AccountChanged`; collapsed it takes no room, so the form signed out or with GitHub off
  is laid out as before. It is enabled while `CanEditClone` and runs `BrowseGitHubCommand`, an
  `AsyncRelayCommand`, so it is drawn disabled while its browser is open. The command makes the
  browser on the session's `ListRepositoriesAsync`, starts its load (`Start()`) before
  `DialogHost.ShowAsync`, and on an answer, unless a clone started meanwhile, clears `ErrorText` and
  sets `CloneUrl`, which fills the folder name unless the user typed one; the user then presses
  Clone as for a typed URL. After "Choose" no control of the form has the keyboard focus (design 17
  item 5; left to phase 8's keyboard pass).
- **The browser** (`CloneFromGitHubDialog`, D301), a sibling of `PickDialogViewModel`, which stays
  as it was. `CloneFromGitHubDialogViewModel : DialogViewModel<string?>` takes the list's delegate,
  the log and an optional `Func<string?>` for the signed-in login, read when a failure is applied
  (after the API's 401 the account keeps its login). `Start()` begins the one load (`Load`) on the
  dialog's own `CancellationTokenSource`, which Cancel, Esc and `CancelAll` (D234) cancel; the
  answer is applied on the UI thread, and one that arrives after the dialog closed changes nothing.
  `Rows`, the rows the filter lets through in GitHub's order (most recently updated first), is
  rebuilt in place as the pick dialog's items are; `Filter` keeps a row whose full name or detail
  holds it, ignoring case, a selected row it hides is no longer selected, and it is enabled while
  loading, so what it holds filters the rows when they come. A `GitHubRepositoryRow(FullName,
  Detail, CloneUrl)`: `Detail` joins with " · " the flags `Private`, `Fork` and `Archived` (each
  when true, in that order) and the description, its runs of white space and line ends made one
  space (null when nothing is left); `CloneUrl` is GitHub's `clone_url` with any user part removed
  (`GitCommand.RemoveCredentials`, ruling 5); `ToolTip` is the name, then the detail on a second
  line. "Choose" (`ConfirmCommand`, enabled with a selection; Enter too) answers the row's
  `CloneUrl`. Under the list one line says the list's state:

| State | Line |
|---|---|
| Loading | `CloneFromGitHubStatus`: "Loading repositories…" |
| Listed, none | `CloneFromGitHubStatus`: "No repositories to list" |
| Failed | `CloneFromGitHubError`: `HostingTexts.Describe(failure, login)` (see "Pull requests", D338); a Warning `Listing GitHub repositories failed: {Kind}: {Message} (request {id})`, the request left out without an id |
| An exception, or neither list nor failure | `CloneFromGitHubError`: "The repositories could not be loaded.", logged as an Error |

- **The browser's other lines and its view.** `CloneFromGitHubMore` ("Only the 1,000 most recently
  updated repositories are listed.", when the provider stopped at its cap) and
  `CloneFromGitHubSso` ("Some organisations' repositories are hidden until the token is authorised
  for single sign-on.") follow, each only with text, all four lines in `settingsNote` but the error
  (`dialogError`). The view copies the pick dialog's on the
  `wide` card (460): `CloneFromGitHubTitle`; the filter `CloneFromGitHubFilter` (placeholder and
  name "Filter", the first focus); the bordered `CloneFromGitHubList` (`MaxHeight` 232, `MinHeight`
  58, no horizontal scroll bar: eight rows before it scrolls), its items `CloneFromGitHubItem`, 28
  high in the pick list's colours, named by the full name with the detail as help text and the
  tooltip above, each a `PickRowPanel` of the name and, 12 apart, the detail at 11.5 in
  `VcTextSecondaryBrush`, cut at its end and hidden when null; `CancelButton` and `ConfirmButton`
  ("Choose").
- **The clone's credentials** (D331). When the account binds the URL as typed
  (`Hosting.BindingsForClone(url)` is not empty), the clone first reads git's configuration once
  with the settings screen's outside read (`GitSettings.ReadAsync(null)`, its local and worktree
  entries dropped, since `git clone` applies none of a repository around its folder), applies the
  `insteadOf` rules (`UrlRewrites`) and binds the URL git will use; a rule that rewrites it to
  another user leaves it unbound (D82's order). A failed read binds nothing and logs one Warning
  (`Cloning {url}: git's configuration could not be read; the clone goes without the account's
  token.`, the URL's credentials hidden by `GitCommand.HideCredentials`); signed out nothing is
  read. The read runs under the clone's own cancellation, and `RunningClone` is a completed task
  while it runs, so that no opening takes the tab. The bindings go into the clone's prompt scope
  (`Prompts.Begin("Clone", name, PromptMode.Ask, bindings)`); what git does with them is in "Prompts
  and credentials".
- **The palette's "Clone from GitHub…"** (D299, D300): `AppCommand.CloneFromGitHub`, after
  `FollowSystemTheme` so that no earlier value moves, a command without a key, listed right after
  "Clone repository…" and only while GitHub is not off (`AppCommands.Resolve`). It runs
  `MainWindowViewModel.CloneFromGitHubAsync`: unless an account is signed in, the window's sign-in
  entry first (see "The settings screen"), and a cancelled or failed sign-in stops there, with no
  tab and no form; then, as "Clone repository…", the active tab when it is empty, else a new tab at
  the end (`TabForOpening`), opens the clone form and awaits `BrowseGitHubCommand`. Cancelling the
  browser leaves the form open. No other palette command signs in to GitHub, and none opens
  Accounts, in 7a (D299).
- Tests: `CloneFromGitHubDialogViewModelTests`, `CloneFromGitHubDialogViewTests`,
  `HostingTextsDescribeTests` (Phase7a); `WelcomeCloneFromGitHubTests`,
  `PaletteCloneFromGitHubTests` (Hosting.Tests, D334); `AppShortcutsTests` (App); checks B1 to B9.

### Pull requests (D251, D303 to D318, D338)

Phase 7a. The left panel's "Pull requests" section, the branch rows' `#n`, a pull request's
actions and checkout, and a new pull request. The graph's badge is in "Commit graph", the menus'
items in "Context menus", the status bar's offer in "Toolbar, status bar, conflict banner and
auto-fetch". Files: `ViewModels/Panels/PullRequestRows.cs` and `LeftPanelViewModel.cs`,
`Views/Shell/LeftPanelView`, `ViewModels/Hosting/PullRequestCheckout.cs`,
`ViewModels/Menus/RepositoryMenus.Hosting.cs`,
`ViewModels/Operations/RepositoryOperations.PullRequests.cs` and `.CreatePullRequest.cs`,
`ViewModels/Dialogs/CreatePullRequestDialogViewModel.cs`,
`Views/Dialogs/CreatePullRequestDialogView`, the texts `OperationTexts.Hosting.PullRequests.cs` and
`OperationTexts.Hosting.Create.cs`, and `ViewModels/Hosting/HostingTexts.cs` and
`HostingTexts.Describe.cs`.

- **What they show.** `RepositoryViewModel.HostingState`, a `RepositoryHostingState` (the section's
  state `PullRequests`: `NotGitHub`, `SignedOut`, `Loading`, `Failed` or `Loaded`; the `List` with
  `MoreExist`; `ByRef`, each open pull request by its branch's full ref name, a local branch only
  through an upstream of the same name, D313; the GitHub `Remotes`, the listed `Repository` and the
  `Failure`), is the hosting's state as last applied on the UI thread; how the hosting lists and
  refreshes it is in "Hosting: a repository's pull requests and avatars". At every change the tab
  passes it to its left panel (`LeftPanel.UpdatePullRequests(state, account)`, not in summary mode),
  the graph binds its `ByRef`, and `MenuContext.Hosting` carries it for the menus (null while
  `NotGitHub`). A repository without a GitHub remote, and every tab while GitHub is off, stays
  `NotGitHub`: its panel, graph and menus are as before phase 7a.

**The section** (D303). `LeftPanelViewModel.HostingState` is `NotGitHub` until the first
`UpdatePullRequests`, so a panel that never had hosting is phase 1's. `AddPullRequests` builds the
section:

| State | Header (`section:PullRequests`) | Rows while open, at depth 1 |
|---|---|---|
| `NotGitHub` | phase 1's: closed, 0, `CanToggle` false | none |
| `SignedOut` | 0 | `Sign in to GitHub to see pull requests…`, clickable |
| `Loading`, no list | 0 | `Loading pull requests…` |
| `Loading`, a list (the state's or the kept one) | the matches | the rows and no message: a refresh does not flicker |
| `Failed` | the kept list's matches, else 0 | `HostingTexts.Describe(failure, null)` as it is (its tooltip that text and GitHub's `Detail` on a second line), then the kept rows |
| `Loaded`, none open | 0 | `No open pull requests` |
| `Loaded` | the matches | the matching rows, newest first, then `More than 300 open pull requests` when the list stopped at 300 |

- With a GitHub remote the header opens and closes as the other sections' do, open at first.
  Message rows show while filtering too; a pull request matches when its `#n`, title, author or head
  label holds the filter, ignoring case. The panel keeps the last list it showed (`_keptList`),
  tagged with its repository and the account that listed it (design 17 item 3, ruling 23): a
  loading or failed refresh of that repository with that account shows its rows, while the hosting's
  state then holds no list; a sign-out, no GitHub remote, another repository or another account
  drops it.
- **Rows** (`PullRequestRows.cs`). `PullRequestRow : RefListRow` (key `pr:{n}`, text `#{n}
  {title}`) holds the `PullRequest`, `NumberText`, `Title`, `Author` (empty when GitHub gave none),
  `IsDraft`, `HeadLabel` (the branch, `{owner}:{branch}` for a fork, the branch alone for a deleted
  fork), `TargetSha` (the head) and `ToolTip` (`#1 Add the login form`, then `{author} · {head
  label} → {base ref}`, the author left out when there is none, and ` · Draft` for a draft), and the
  observable `IsSelected`. `PullRequestMessageRow` (key `pr-message:{Kind}`, `PullRequestMessageKind`
  `SignIn`, `Loading`, `Failed`, `None` or `More`) holds `Text` and `ToolTip` (the "more" row's says
  "Open pull requests in browser"); only `SignIn` and `More` can be activated. `ShowsSameAs`
  compares every shown member, so a rebuild replaces only rows that changed. In `LeftPanelView` both
  templates are `RefRowButton`s 26 high, with the indent and the chevron's 18 px: `PullRequestItem`
  (named by its text, no `RefDrag.Source`, so no drag starts there) docks at its right the author
  (`PullRequestAuthor`, at most 90, cut with "…") and `Draft` (`PullRequestDraft`, a draft only), at
  its left `#1` (`PullRequestNumber`), all three in the secondary colour, then the title
  (`PullRequestTitle`), which is cut first; its tooltip lies on the row's `DockPanel`.
  `PullRequestMessage` holds `PullRequestMessageText`, secondary and cut with "…"; a row that takes
  no click is a disabled button, which keeps its look and, through `ToolTip.ShowOnDisabled`, its
  tooltip. No new token or icon.
- **Click, double-click, menu** (D304). A click on a pull request selects and reveals its head
  commit (`RefActivated(HeadSha)`), and the row keeps the selection background while the graph's
  single selection is that commit (`SyncSelection`, D98): the panel's one selected key spans
  branches, tags, stashes and `pr:` keys (`MarkSelected`), and `TargetOf` and `Update` know a listed
  pull request's key, so the selection survives `Update` and `UpdatePullRequests`. A head that is no
  row of the graph (a fork's head not fetched) selects nothing (`NothingActivated`, FD19's rule); a
  pull request no longer listed is no longer selected. A click on the sign-in or "more" row of the
  active repository's panel goes, through a `Button.ClickEvent` handler on `RefList`, to
  `RepositoryViewModel.ActivatePullRequestMessage`: the sign-in row runs the window's sign-in entry
  (see "The settings screen"), the "more" row opens the list in the browser. A double-click checks
  the pull request out as its menu's item does, unless that item would be disabled
  (`DoubleTapRefRow`). `RefRowTarget.PullRequest` opens `ForPullRequest`; the section's header is
  `RefRowTarget.Section` with a GitHub remote and `None` without one (`TargetOf`). The palette lists
  every listed pull request's items (see "The command palette").
- **The branch row's `#n`** (D308). `RefItemRow` takes an optional `(Number, Title)` for a local or
  remote branch whose full name `HostingState.ByRef` holds: `RefPullRequest` (`#1`, classes
  `secondary count`, 6 after the name, left of the toggles and counts, tooltip `Pull request #1: Add
  the login form`), docked right before `RefName`, so the name is cut first. Collapsed without a
  pull request, it takes no room, and no earlier row changes.
- **The actions** (`RepositoryOperations.PullRequests.cs`, D251, D307). "Open in browser" hands the
  pull request's `HtmlUrl` to `IFileLauncher.OpenUrlAsync` and "Copy link" puts it on the
  clipboard, neither an operation, a failure logged. `IFileLauncher.OpenUrlAsync` (since phase 7a)
  opens only an absolute `http` or `https` address (else `ArgumentException`, from
  `FileLauncher.ThrowIfNotWebAddress`, which the tests' fake shares) through Avalonia's
  `TopLevel.Launcher.LaunchUriAsync` on the UI thread, and logs a Warning when the system could not
  open it; it is the app's only way into the default browser (D321). "Refresh pull requests" runs
  the hosting's refresh outside the queue, a failure logged as a Warning. "Open pull requests in
  browser" opens `{web base}{owner}/{name}/pulls` of the listed repository (a fork's parent), else of
  the first GitHub remote's in `HostingRemoteOrder`, also signed out, asking GitHub nothing (D257);
  an Info line when neither is known. "Sign in to GitHub…" runs `TabServices.SignInToGitHub`.
- **Checkout** (D305). `PullRequestCheckout.Plan(state, refs, pullRequest)` is pure; the menu, the
  double-click and the operation share it. Remotes are tried in `HostingRemoteOrder`; two
  repositories are the same when host and full name are equal ignoring case. (1) The head on a
  remote: the first remote of the head's repository whose `refs/remotes/{r}/{head ref}` its fetch
  lines map from that remote's `refs/heads/{head ref}`: label `Check out {local}`, the ref's local
  name, disabled "{local} is already checked out" while HEAD is on it; it runs phase 3's checkout of
  that remote branch (a tracking branch, D93, D201), after phase 3's fetch of that remote when the
  ref is not read yet, and only if the ref then exists. (2) Otherwise from the base's remote, the
  first whose repository is the base: label `Check out {owner}:{head ref}` (`{head ref}` alone for a
  deleted fork, whose owner is then `pr-{n}`); without a remote for the base the item is disabled
  with `Add a remote for {owner/name} to check out this pull request` (a fork cloned without its
  parent; there is no fetch by URL). The local name is the first of `{head ref}`, `{owner}-{head
  ref}`, `{owner}-{head ref}-2`, `-3`, … that no local branch takes or conflicts with as a path or in
  another case, or a local branch of that exact name already at the head, which is reused: its
  checkout alone, disabled while it is current. A new name runs the network operation "Fetch pull
  request #n" (`Fetching pull request #n from {remote}…`, `RefreshScope.Refs`, undo
  `UndoScope.CreateBranch`): `Git.FetchToFetchHeadAsync(remote, "refs/pull/{n}/head")`, which reads
  its own line of `FETCH_HEAD` and gives null when another fetch rewrote it (see "Git layer"), then
  `CreateBranchAsync` without upstream at that SHA, reported `Fetched #5 into fix-typo`; a null fails
  it with `Another fetch changed FETCH_HEAD; try again.` and creates no branch. On success phase 3's
  checkout of the new branch follows as its own operation, with its automatic stash, detached-HEAD
  question, blocked dialog and undo. No `refs/pull` fetch line and no `refs/remotes/<r>/pr/<n>` ref
  is ever written. A plan with a reason does nothing when it runs (a stale menu) and logs an Info
  line.
- **A new pull request: how it starts** (`RepositoryOperations.CreatePullRequestAsync(headRef,
  baseRef)`, D313). From a branch's or a drop's "Create pull request…" (see "Context menus"), on the
  UI thread: without a GitHub remote nothing starts (an Info line); signed out, the window's sign-in
  entry comes first, and a refused or cancelled one stops it; the head, from the refs now, must be a
  local branch or a remote branch of a GitHub remote that its fetch line maps from a branch (else an
  Info line). The form's side is `PullRequestForm : ICreatePullRequestActions`, a private class of
  `RepositoryOperations`; the form itself never runs git or HTTP. The form opens with the head's tip's
  subject as its title (`ReadSubjectAsync`, empty without one) and, for a drop, the target as its
  base (a GitHub remote's branch, or a local branch whose upstream is a GitHub remote's branch of the
  same name; else an Info line and no form), through `ShowDialogAsync`, which names the repository
  when dialogs do; its answer is not used.
- **The head's place on GitHub** (D313), computed from the refs when the form opens, again at every
  Create and after every outcome but a success, never kept from before (design 19 item 4). A remote
  branch: its remote's repository and branch, never pushed. A local branch `x` whose upstream is a
  branch of a GitHub remote under the same name: that repository and branch, pushed first to its
  upstream without `--set-upstream` while it is ahead (`2 commits are not on GitHub yet: Create
  pushes them to origin/feature first.`) or its upstream is gone (`origin/feature is gone: Create
  pushes the branch to it again first.`). Any other local branch: the first GitHub remote in
  `HostingRemoteOrder`, under its own name; without an upstream it is always pushed first with
  `--set-upstream` (`Not on GitHub yet: Create pushes it to origin/local-work first and sets that as
  its upstream.`); with an upstream elsewhere (a local one, another host's, a GitHub branch of
  another name, as "Create branch here" on `origin/main` makes) the upstream is left as it is, and
  the branch is pushed only when that remote lacks it (`Not on GitHub yet: Create pushes it to
  origin/topic first.`) or lacks commits of it, which `Git.CountCommitsAsync` counts only when the
  remote branch exists and is not at the branch's tip. That note is the form's `HeadNote()`, null
  when GitHub has the head as it is.
- **The bases** (D318). Without a drop's base, the default comes from `StartAsync`: A3 of the
  hosting (`Hosting.ReadRepositoryAsync`), the parent's default branch for a fork, else the
  repository's own, shown as the remote branch of the remote that is that repository when the refs
  hold it, else as `owner:branch` (`octo:main`); a failed read leaves Into empty, with the failure in
  `HostingTexts.Describe`'s words on the error line. "Choose…" stacks phase 6's Pick dialog ("Choose
  the branch to merge into", D104; Esc closes the Pick only) over every remote branch of a GitHub
  remote that its fetch line maps from a branch, in the refs' order, each with its repository as its
  detail, and, when no GitHub remote is the listed repository (a fork cloned without its parent),
  that repository's default branch as `owner:branch`. A `PullRequestEnd(Text, Repository)` carries
  `Branch`, GitHub's name of the branch, for the POST and for `IsSameBranch`: head and base one
  branch of one repository is refused in the form before GitHub is asked.
- **The form** (`CreatePullRequestDialogViewModel : DialogViewModel<bool>`, true when created).
  States: loading (`StartAsync` on the form's own token, which Cancel, Esc and `CancelAll` cancel;
  Into reads `Reading GitHub…` in the secondary colour; a base chosen meanwhile wins; a drop's
  given base skips it), ready, creating (`IsCreating`: every control disabled, Create reads
  `Creating…`, Esc ignored), failed (the error line, the controls enabled again, the head's note
  read again) and done. Changing the title, the description, the base or Draft clears the error
  line and the existing pull request's button; `Choose a branch other than {branch} to merge into.`
  stays while the base is the head's own branch. Create runs when not creating, with a base, a title
  that is not blank and not the same branch; its disabled reason is its tooltip
  (`ToolTip.ShowOnDisabled`: `Choose the branch to merge into.`, the same branch's text, `Enter a
  title.`). The title and the description are sent with trailing white space trimmed; nothing else
  is checked locally. The form's Cancel (`CancelFormCommand`) closes it when idle; while creating it
  is enabled only inside the create's cancel window, the push (`ICreatePullRequestActions.
  CanCancelCreate`, design 17 item 8), where it cancels the operation through
  `RunOptions.Cancellation` and the form stays open with `Cancelled before the pull request was
  created.`: the status bar's Cancel lies under the form's backdrop (D69).
- **Its view** (`CreatePullRequestDialog`, the `wide` card, 460, about 386 high, centred): the title
  `CreatePullRequestDialogTitle` ("Create pull request") and `DialogRepository` when shown; a grid of
  48 and the rest with "From" (`PullRequestHead`, at most 220 and cut, its whole name as tooltip;
  `PullRequestHeadRepository` in the secondary colour; under them `PullRequestHeadNote`) and "Into"
  (`PullRequestBase`, `PullRequestBaseRepository` and `PullRequestBaseChooseButton` "Choose…", as
  the merge tool's row); `PullRequestTitle` (32 high, placeholder and name "Title", the first focus
  with the caret at its end; Enter creates); `PullRequestDescription` (120 high, wrapping, accepting
  Return; Mod+Enter creates, D215); `PullRequestDraft` ("Draft", unticked); `PullRequestError`
  (`dialogError`); `PullRequestOpenExisting` ("Open pull request #n", after a 422 that found the
  pull request open: it opens it and closes the form with false); `CancelButton` and `ConfirmButton`
  ("Create", `VcPrimaryButton`). Existing tokens only.
- **The operation** (D312, D314, D315, D316). `PullRequestForm.CreateAsync` runs one operation of
  the queue, `Create pull request for {head}` (the local branch's name, or the remote branch's,
  `origin/topic/no-pr`): `OperationKind.Network`, so the hosting's refresh follows it (see "Hosting:
  a repository's pull requests and avatars"); `RefreshScope.Refs` when it pushes, else `None`;
  `PromptMode.Ask`, `ShowsErrorDialog = false`, no dedupe key, and when it pushes the undo entry of
  any push (`UndoScope.Push`, D314); the create itself has none. Pushing, its running text is the
  push's from the start, and it runs the guards a push to a remote runs (`RefuseIfNotABranch`,
  `RefuseIfPushStopsFetchAsync`), then `Git.PushAsync` with the tab's bindings (see "Prompts and
  credentials"). Then `Creating pull request…` and `EndCancelWindow()` (D315): Cancel is offered
  during the push only, and the POST (`Hosting.CreatePullRequestAsync`; a head in another repository
  than the base, a fork's, with its owner) runs to its answer or its 30-s limit with
  `CancellationToken.None`. Created: the list is read again when the base is the listed repository
  (the section and the badge follow), and the report `Created pull request #9` (`Created draft pull
  request #9`) carries the status bar's offer "Open in browser" (D317); the browser never opens by
  itself. A 422 "A pull request already exists" reads the list again and gives `Pull request #1 is
  already open from feature/login into origin/main.` with "Open pull request #1" when it is open; a
  `Network` or `ServerError` failure, after which the POST may have arrived, reads the list again
  and counts a pull request from the head into the base that was not listed before as created
  (D316). Every failure's words go to the form's error line (D312). In the report a guard's refusal
  is the guard's own and git's failure of the push is git's, as for any push; a failure of GitHub's
  is a stopped failure with the form's words. A failure of git's or GitHub's makes the status bar
  say "Create pull request for topic failed" in `VcDangerBrush`; git's output stays in the activity
  log. Nothing of it holds the UI thread; the window's close waits for the operation, and
  `CancelAll` closes the form, never the operation.

| When | The form's error line |
|---|---|
| Cancelled during the push | `Cancelled before the pull request was created.` |
| A push guard refused | the guard's own summary |
| The push rejected | `OperationTexts.PushRejected(remote, branch)` |
| Git's credentials refused during the push (`RefusedAccount`) | `OperationTexts.GitCredentialsRefused(login)` (D276, D330) |
| Any other git failure of the push | `Git could not push topic to origin/topic: {line}`, the first line of git's cleaned output that is neither a pack line nor `To {url}` (`PushReportLine`) |
| 422, already open and found in the list | `Pull request #1 is already open from feature/login into origin/main.` |
| 422 "No commits between" | `origin/main already has every commit of topic: there is nothing to merge.` |
| 422 "head is invalid" | `GitHub does not accept me:topic as the branch to merge from. It must be in octo/hello or in a fork of it.` |
| 422 "base is invalid" | `GitHub has no branch release in octo/hello. Choose another branch to merge into.` |
| Any other 422, or "already exists" not found | GitHub's words, `{Message}: {Detail}` or the message alone (ruling 15) |
| 403, head and base in different repositories | `GitHub refused to create a pull request in octo/hello with this token. A fine-grained token cannot open pull requests in another owner's repository; sign in with a classic token that has the repo scope to do that.` |
| Any other 403 | `GitHub refused to create a pull request in octo/hello with this token.`, and ` It needs the permission pull_requests=write.` when GitHub named it |
| 404 | `This account cannot see octo/hello on GitHub.` |
| Anything else, a 401 included | `HostingTexts.Describe(failure, login)` |

- Logged: Info `{repo}: Create pull request for {head}: into {owner/name} {branch}, draft yes|no,
  push to {target} first` (or `no push`) and `… created #{n} in {owner/name}.`; a Warning with the
  failed request's kind, GitHub's message and request id; an Info line for each create not started.
  Neither the title nor the description is logged, nor a token, a request or a response.
- **One wording of GitHub's failures** (D338). `HostingTexts.Describe(failure, login)` is the text
  the browser, the section's failed row and the form show: `Network` as it is (the client's fixed
  texts, D284); `BadCredentials` as `OperationTexts.TokenRefused(login)` with a login, else the
  message and `Sign in to GitHub in Settings, Accounts.`; `RateLimited` with the time to try again
  after (`RetryAt` in local time, `HH:mm`) or "in a few minutes"; `SsoRequired` naming the
  organisation after `orgs/` in the detail's URL, else "The organisation"; the app's fixed sentences
  for `ApiVersion`, `ServerError` and `Malformed`; `GitHub answered: {message}.` for the rest. The
  sign-in dialog keeps its own texts. `HostingTexts` holds the texts that more than one part shows
  ("GitHub", "Accounts", "this computer's keychain", "Add GitHub account", "Sign in to GitHub…",
  "Sign in to GitHub in Settings, Accounts."); each part's others are in
  `OperationTexts.Hosting.<Area>.cs`.
- Tests: `PullRequestSectionTests`, `LeftPanelPullRequestsViewTests`, `PullRequestCheckoutTests`,
  `RepositoryMenusHostingTests`, `CreatePullRequestDialogViewModelTests`, `StatusBarOfferTests`,
  `HostingTextsDescribeTests` (Phase7a); `PullRequestOperationsTests`,
  `CreatePullRequestOperationTests` (Hosting.Tests, D334); `PullRequestFetchHeadTests` (App);
  `GitRepositoryPullRequestsTests`, `OperationQueueOfferTests` (Git); checks P1 to P15 and N1 to N10.

### Q1 measurements (D50)

The app logs, per repository: `<name>: first graph rows drawn after <N> ms` (from the start of
opening to the first frame with rows), `<name>: loaded <N> commits in <N> ms`, and when the tab
closes or the app exits `<name>: graph frames while loading: ...` and `<name>: graph frames:
<N> drawn, 95th percentile <N.N> ms, longest <N.N> ms` (frames after the load, D57). At Debug
level it also logs when the repository was opened, when the refs were read and when the first
commits came from git, measured from the same start. The real-window pass reads these lines from
the log.

On the Windows development machine phase 1's gate measured the 100k repo's first rows after 1002 ms.
Most of that is git's own work: without a commit-graph file, `git log --date-order` reads the
whole history before it prints its first commit (0.6 to 0.9 seconds there); opening and the refs
take about 0.2 seconds each, side by side with it. The machine's speed varies with its
temperature, so the pass is started after a rest (see phase 1's test report). Phase 4's gate
measured 937 ms rested. At phase 5's gate the pass on the first run's commit measured 1,749 ms.
On the gate commit the pass first failed Q1 at 2,613 ms (limit 2,000), on a laptop hot from the
default run before it; Q1 alone and rested then measured 1,147 ms, and the pass run again rested
measured 1,826 ms, with the graph's frames after the load at a 95th percentile of 1.1 ms and a
longest of 9.8 ms. Phase 5 left the graph's command unchanged when nothing is hidden or soloed
(D178).

### Settings, session, log and data folder

- `AppPaths` (Core) names everything in the data folder: `settings.json`, `session.json` and
  `logs/`, since phase 6 `workspaces.json` (`WorkspacesFile`, D189) and `merge-tools.json`
  (`MergeToolsFile`, the kept list of merge tools, D202), and since phase 7a `accounts.json`
  (`AccountsFile`, the hosting account's record, never its token, D287) and `avatars/`
  (`AvatarsDirectory`: the avatars' images and `index.json`, and the e-mail maps in `authors/`;
  D286, D291). `AppPaths.Resolve()` uses `VISUALCOMMIT_DATA_DIR` when it is set, otherwise the
  per-user folder (`%APPDATA%\VisualCommit`, `~/Library/Application Support/VisualCommit`,
  `~/.config/VisualCommit`).
- `AppSettings` (Core) is an immutable record of preferences: `Theme`, `FileList` (D44),
  `DiffMode` (D61), `AutoFetchMinutes` (default 5, 0 turns auto-fetch off; no screen sets it
  before phase 6, D85), `StashIncludeUntracked` (default true: the stash dialog's check box
  starts with the last choice, saved when the dialog is confirmed) and, since phase 5,
  `ShowAllFiles` (the details panel's "All files", default false, D164). Since phase 6 `Theme` may be
  `System` (D194), the settings screen sets `AutoFetchMinutes`, and `KeepIgnoredFiles` (default
  true, D193), `Editor` (an `EditorSetting`, null while none is chosen, D226) and
  `ExtraRunnableExtensions` (the extensions Open file asks about beyond its fixed list,
  space-separated, empty by default) are new. Every property needs a default, so that older files
  still load; a setting missing from `settings.json` gets its
  default (`JsonSettingsStore` lays the file's values over the defaults' JSON before reading it,
  D107), not 0 or false. `ISettingsStore.Update(settings => settings with { ... })` changes and
  saves it. Phase 7a added no setting: "Use for git over HTTPS", which the settings screen's
  Accounts section saves, is the account's, in `accounts.json` (D255, D287).
- `SessionState` (Core) is what changes as the app is used (D46): `Tabs` and `ActiveTab`,
  `Recent`, `Window` (`WindowPlacement`: position, client size, maximised) and the panels'
  preferred widths. `JsonSessionStore` (App) keeps it like the settings: written through a
  temporary file, an unreadable file set aside as `session.unreadable.json`, a tab with a blank
  folder read as an empty tab. `JsonSessionStore.Write` lets tests start the app with tabs open.
  Phase 3 added nothing to the session, and phase 4 nothing to the session or the settings (the
  merge tool is git's own `merge.tool`, D149). Phase 5 added `ActivityLogHeight` (null for the
  default) and `GraphFilters` (the hidden and soloed branches per repository, at most 100, D159;
  see "Solo and hide"); an older file without them still loads. The undo histories are not kept:
  they last for the app's session (D154). Phase 6 added `TabState.WorkspaceId`: a dashboard tab is
  kept as `TabState(null, WorkspaceId)` (see "Workspaces"); the workspaces themselves are in
  `workspaces.json`, never in the session (D189). Phase 7a added nothing to the session.
- Since phase 7a (D287, D291, D292): `accounts.json` holds the GitHub account's record (an
  unreadable file is set aside as `accounts.unreadable.json`), and `avatars/` the avatars' images,
  their `index.json` and the e-mail maps; see "Hosting: the session and the account", "Hosting: a
  repository's pull requests and avatars" (the e-mail maps) and "Hosting: the provider and GitHub's
  API". A sign-out deletes `avatars/` whole and removes the record. No file of the data folder ever
  holds a token (the tests' `TokenTripwire`, see "The fake of GitHub").
- `JsonSettingsStore` (App) writes camel-case JSON with enums as text, through a temporary file,
  so a crash cannot leave half a file. Since phase 6 every file of the app's own (`settings.json`,
  `session.json`, `workspaces.json`, `merge-tools.json`, and since phase 7a `accounts.json`,
  `avatars/index.json` and the e-mail maps, camel-case too, D336) is written through
  `Core/DurableFile`: the text to `<file>.tmp`, flushed to the disk (`Flush(flushToDisk: true)`),
  then moved over the file, so that a power cut leaves the old file or the new one, never an empty
  one (D236); an avatar's image is written the same way. A file it cannot read is moved to
  `settings.unreadable.json` and the defaults apply. A failed save is logged and does not throw.
- `DateDisplay` (Core) shows dates as `yyyy-MM-dd HH:mm` in the local zone, or in the zone named
  by `VISUALCOMMIT_TIME_ZONE` (D43); both harnesses set `UTC`.
- `IAppLog` (Core) is the log interface, with `Debug`, `Info`, `Warning` and `Error` helpers.
  `FileAppLog` (App) appends to `logs/visualcommit-yyyyMMdd.log`, one line per entry
  (`2026-10-06 01:34:44.722 -07:00 [INF] message`), opening the file for each entry, so several
  instances can share it. Files older than 14 days are deleted at start-up. A log that cannot
  write never throws. `Close()` (at the session's end) makes it write nothing more. An entry's
  exception is written whole after its line. What the hosting writes there, and never, is in
  "Hosting: the session and the account".

### Shell and views

`Views/MainWindow.axaml` lays the window out as "Main window layout" in `requirements.md`. Each
region is a `UserControl` in `Views/Shell`.

| Region | View | Size | Automation id | State |
|---|---|---|---|---|
| Repo tabs | `RepoTabsView` | 36 high | `RepoTabs` | Real: one tab per repository (`RepoTab` buttons named by title, `CloseTabButton`), and "+" (`AddRepoButton`, since phase 6 named "Open a new tab") for a new tab. Since phase 6 a dashboard's tab first (`DashboardTab`, `TabIcon` `IconWorkspace`, `ItemStatus` `Workspace`), and the tabs laid out by `RepoTabStripPanel` (D228): they keep 120 to 220 while they and "+" fit, then the widest shrink first down to 80, then they scroll with the active tab whole in view and "+" at the right end (a wheel notch 80, no arrow buttons) |
| Toolbar | `ToolbarView` | 52 high | `Toolbar` | `FetchButton`, `PullButton`, `PushButton`, `BranchButton`, `StashButton`, `PopButton` are real (see "Toolbar, status bar, conflict banner and auto-fetch"); since phase 5 `UndoButton`, `RedoButton` and `SearchButton` too, with their tooltips as automation help text; since phase 6 `SettingsButton` at the start of the right group, every button's tooltip as help text and its key as `AcceleratorKey`. `ThemeSwitch` works |
| Left panel | `LeftPanelView` | 260 wide by default; 180 to 520; hidden on a dashboard tab | `LeftPanel` | Real: `FilterBox` and the list `RefList` (`Section`, `RefItem`), with menus, double-clicks and branch drags, and since phase 5 the marks of hidden and soloed branches (`RefHiddenToggle`, `RefSoloToggle`); since phase 7a, with a GitHub remote, the "Pull requests" section's rows (`PullRequestItem` with `PullRequestNumber`, `PullRequestTitle`, `PullRequestDraft`, `PullRequestAuthor`; `PullRequestMessage`) and a branch row's `RefPullRequest`, see "Pull requests"; without one "Pull requests" stays phase 1's closed, empty header |
| Commit graph | `CommitGraphView` | The rest; at least 320; while the resolver is open, the right panel's column too; on a dashboard tab all three columns | `CommitGraph` | Real: `WelcomeView` (`OpenRepoButton`, `CloneRepoButton`, `InitRepoButton`, the clone form, since phase 7a with `CloneFromGitHubButton` while signed in, since phase 6 the Workspaces section `WorkspacesHeading`, `NewWorkspaceButton`, `WorkspaceList`, `WorkspaceItem`, `NoWorkspacesText`, `WorkspacesNotice`), since phase 6 `DashboardView` (`Dashboard` and its parts, see "The dashboard tab") and `TabWaitingText`, `RepositoryGraphView` (`GraphRows`, `GraphScrollBar`, and since phase 5 the search bar `SearchBar` with `SearchModeButton`, `SearchBox`, `SearchCount`, `SearchProgress`, `SearchPrevious`, `SearchNext`, `SearchClose`, and `GraphReloading`), or in its place `InteractiveRebaseView` (`InteractiveRebaseView`, `RebaseRow`, …), `DiffView` (`DiffView`, `DiffText`, see "Diff view"), `ConflictResolverView` (`ConflictResolver`, `ConflictOurs`, `ConflictTheirs`, `ConflictResult`, …, see "The graph area") or a page of phase 5 (`FileHistory`, `Blame`, `Reflog` and their parts); above any of them, while one shows, `ConflictBanner` (`BannerText`, `BannerContinueButton`, `BannerSkipButton`, `BannerAbortButton`); under them, while the window has it open, the activity log (`ActivityLogSplitter`, `ActivityLog` with `ActivityLogHeader`, `ActivityLogShowReads`, `ActivityLogClose`, `ActivityLogList`, `ActivityLogOperation`, `ActivityLogCall`, `ActivityLogOutput`) |
| Right panel | `RightPanelView` or `WorkingChangesView` | 400 wide by default; 280 to 720; hidden while the resolver is open and on a dashboard tab | `RightPanel` or `StagePanel` | Real: commit details (`DetailsSubject`, `ParentLink`, `FlatFilesButton`, `TreeFilesButton`, `ChangedFilesTitle`, `FileList` with `ChangedFile` rows, and since phase 5 `AllFilesButton`; since phase 7a `DetailsAuthorAvatar` before `DetailsAuthor`), `CompareView` while two commits are selected or branches are compared (`Compare`, `CompareFrom`, `CompareTo`, `CompareSwap`, `CompareClose`, `CompareAllDifferences`, `CompareCounts`, `CompareFileList`), `MultiSelectionView` while three or more are selected, or the placeholder; while the working-changes row is selected, the stage panel (`UnstagedFileList` and `StagedFileList` with `StageFileRow` rows and their `RowStageButton`, `RowDiscardButton`, `RowUnstageButton`; `CommitSummary`, `CommitDescription`, `AmendCheckBox`, `CommitButton`, which reads "Continue …" during a list or a rebase, `RestoreBar`) |
| Status bar | `StatusBarView` | 26 high | `StatusBar` | `CurrentBranch`, `GitStatus`, `OperationStatus`, `OperationProgress`, `OperationCancelButton` and `OperationWaiting` are real, and since phase 5 `ActivityLogToggle`; since phase 6 `StatusWorkspaceIcon` before the workspace's name on a dashboard tab; since phase 7a `OperationOfferButton` after `OperationStatus` while a result carries an offer (see "Toolbar, status bar, conflict banner and auto-fetch") |

- The window opens at 1280×800, or at the size and place the session saved, and cannot be made
  smaller than 1000×560. `MainWindow.UseSession` restores and saves the placement (a window
  restored maximised keeps its normal size and position).
- The panels are resized with two `GridSplitter`s (`LeftSplitter`, `RightSplitter`) that lie over
  the panel edges. Letting go of an edge makes that panel's width its preferred width, saved in
  the session. `PanelLayout.Fit` gives the drawn widths: when the window is too narrow for both
  preferred widths and the graph's 320, both panels give way in proportion to their room above
  their minimums, and take their preferred widths back when it grows.
- The diff view replaces the graph while a file's diff is open (D63), and so do the interactive
  rebase's list, the conflict resolver (D140, D141) and since phase 5 the pages (D158); the
  activity log lies under all of them (D156); the menu layer, the drag layer and the dialog layer
  lie over the whole window, in that order (D69, D104, D105, D87).
- `MainWindow`'s Esc chain (`AddEscapeHandler(IEscapeHandler, order)`, `Views/IEscapeHandler`):
  on Esc the handlers are asked in their `EscapeOrder` (Menu 100, Drag 200, Dialog 300, and since
  phase 5 ActivityLog 340, Resolver 350, InteractiveRebase 360, Page 370, Search 390, Diff 400)
  until one takes it. The menu layer, the drag layer, the resolver, the list, the pages, the search
  bar and the activity log register their own; the list's and a page's decline while a diff is
  open over them, the activity log's unless the focus is inside it, the search bar's unless the
  focus is in it.
- `MainWindow` also handles the window's keys of phase 5 (see "Toolbar, status bar, conflict
  banner and auto-fetch"), since phase 6 through its `KeyRouter` (`MainWindow.Keys.cs`, see
  "Keyboard shortcuts"), and calls `MainWindowViewModel.OnWindowActivated` from its `Activated`
  event (a dashboard's reads, Q4).
- Since phase 6 a dashboard tab shows `DashboardView` across the three columns (see "Repository
  tabs"); the palette and the settings screen are dialogs of the dialog layer, and since phase 7a so
  are "Add GitHub account", "Clone from GitHub" and "Create pull request".

Custom controls, in `Controls/`:

- `Icon` draws one of the outlines in `Theme/Icons.axaml` as a stroke in the inherited text
  colour. To add an icon, add a `StreamGeometry` on a 24×24 grid to `Icons.axaml` (and to
  `THIRD-PARTY-NOTICES.md` if it is not our own drawing).
- `ToolbarButton` is a button with an icon above a label. Its control theme is in
  `Theme/Controls.axaml`; its label is also its name for screen readers and UI Automation.
- `CommitGraphControl`: see "Commit graph". Since phase 7a `AvatarBitmaps` (a static class, not a
  control) decodes an author's avatar on the thread pool for the graph and the details panel.
- `ContextMenuItem`: see "Context menus". `SecretTextBox`: see "Prompts and credentials".
- `Diff/CodeTextEditor`, `Diff/DiffPane` and `Diff/DiffTextView`: see "Diff view";
  `Conflicts/ConflictPanes` and `Conflicts/ConflictResultEditor` (with `ConflictTint`, the
  background a row or line is drawn on, which tests read instead of pixels): see "The graph area".
  Since phase 5 `Diff/CodeNumberMargin`, the one number column of the resolver's result and the
  blame's text, and `Blame/BlameTextEditor` and `Blame/BlameGutter`: see "The graph area".
- `TrimmedTextBlock` shows its whole text as its tooltip while it is cut with "…" (a menu
  item's label); `UrlTextBlock` wraps without breaking a line inside a URL or an ssh address
  (the confirmation's question, the error dialog's summary, the prompt dialogs' texts). Since phase
  6 `PathTextBlock` (D227, design 13.2) wraps a rooted path only after a directory separator: it puts
  U+FEFF (no break) between a path's characters and U+200B (a break) after each `\` or `/`, so the
  welcome page's errors and the scan dialog no longer break at `C:` or inside a folder name; only
  the drawn text changes, `Text` and the automation name stay as given. All three are styled as
  plain text blocks.
- Phase 6's panels: `Views/Shell/RepoTabStripPanel` (the tab strip's layout, see "Shell and views")
  and `Views/Workspaces/DashboardRowPanel` (the dashboard's columns, from `DashboardColumns`).

Control themes in `Theme/Controls.axaml`: `VcFlatButton` (since phase 5 with a class `selected`:
primary text on `VcControlBackgroundBrush`, the status bar's Activity log while its panel shows),
`VcTabButton`, `VcActionButton`, `FileListToggle` (small text buttons: Flat, Tree and since phase
5 All files, the stage panel's and diff view's actions; class `selected`), `FileFolderButton` (a
file or folder row), `VcIconButton` (22 by 22: the stage rows' buttons, the panels' and pages'
close buttons, the resolver's arrows, the search bar's; since phase 5's gate a disabled one draws
in `VcTextDisabledBrush`, as every other button theme does, and the resolver's own rule for its
arrows, D160's `Button.arrow:disabled`, went), `VcPrimaryButton` (the commit button),
`VcDangerButton` (since phase 6 its text `VcDangerTextBrush`), `VcCheckBox` (20 high), and the text classes `heading`, `secondary` and `error`.
The same file holds since phase 5 the `ToolTip` style that takes no input (D182). Phase 3's menus
and dialogs have files of their own, `Theme/Menus.axaml` and `Theme/Dialogs.axaml`, included in
`App.axaml`.

### Theme

- `IThemeService` / `ThemeService` (App, `Services/`) switches the theme by setting the
  application's theme variant. Since phase 6 (D194, phase 6 design 11) the setting is Dark, Light
  or System: `Setting` is the choice, `IsDark` what shows, `Changed` is raised only when either
  changes; while System is chosen the service follows the system's theme through `ISystemTheme`
  (`Services/SystemTheme.cs`; `PlatformSystemTheme` reads Avalonia's platform settings and posts
  their change to the UI thread; the harness passes `FakeSystemTheme`), and while Dark or Light is
  chosen it ignores the system. `AppSession.Start` applies the saved theme and logs `Theme: …`
  before the window is created, so the first frame has it, and its `Dispose` disposes the service
  before the platform's theme. The view model's `Theme` and `IsDarkTheme` come from the service
  (`MainWindowViewModel.Theme.cs`); the toolbar's switch, and `UseTheme`, choose Dark or Light and
  save it (from System, the opposite of what shows, its tooltip then adding "(stops following the
  system)"). `UseTheme(AppTheme)` is the one way to change the theme: the switch, the palette's three
  entries and the settings screen's Save (D223). `ThemeVariant.Default` is unused; Linux without a
  portal reads as Light.
- All colours are tokens in `Theme/Tokens.axaml`, one set per theme. Views use them only as
  `{DynamicResource ...}`, never as fixed colours, so switching recolours everything at once. The
  graph control looks them up for its theme variant and redraws when the variant changes, as do
  the drag layer's pill and outline (`DragLayer`, mid-drag) and the custom-drawn diff, blame and
  conflict views. Phase 6 brought the light status colours to 4.5:1 against every background they
  are drawn on (`VisualCommit.Testing/Contrast.cs`, `ThemeTokensTests`), gave the danger button's
  text a token of its own and drew tooltips in the app's colours through Fluent's `ToolTip…` keys.
- The Fluent theme is the base for standard controls, with its accent set to ours. Its text-box
  colours are replaced by `TextControl...` keys in `Tokens.axaml`.
- The typeface is Inter, shipped with Avalonia, on every platform (D28). Code in the diff view
  is JetBrains Mono NL (OFL), shipped with the app in `Assets/Fonts` and named by the resource
  `VcCodeFontFamily` (D64).
- Phase 3 added the icon `IconAlert` (Lucide's triangle-alert, in `THIRD-PARTY-NOTICES.md`), the
  conflict banner's. Menus have no icons. Phase 4 added `IconGrip` (grip-vertical, the list's drag
  handle), `IconChevronLeft` and `IconChevronUp` (the resolver's previous file and previous
  conflict), all Lucide's, and the five conflict tokens below. Phase 5 added `IconHistory`
  ("history", the file history page), `IconBlame` ("text-search", the blame page), `IconEyeOff`
  ("eye-off", a hidden branch) and `IconSolo` ("crosshair", a soloed one), all Lucide's (ISC, in
  `THIRD-PARTY-NOTICES.md`), and the token `VcSearchMatchBrush`. A search's dimmed rows use
  opacity; the blame gutter, the activity log, the progress bars and the toggles use existing
  brushes. Phase 6 added `IconWorkspace` (Lucide's "layout-grid", a dashboard's tab, header and
  status bar), `IconSettings` ("settings", the toolbar's button), `IconRefresh` ("rotate-cw", the
  dashboard's Refresh) and `IconMore` ("ellipsis", the dashboard's `…` button), and the token
  `VcDangerTextBrush`; the dashboard, the palette and the settings screen use existing brushes.
  Phase 7a added `IconPullRequest` (Lucide's "git-pull-request", the graph's badge; in
  `THIRD-PARTY-NOTICES.md` among the icons derived from Feather) and no token: the Accounts section,
  the three dialogs, the section's rows, the badge, the avatars and the offer use existing brushes.

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
| `VcTextDisabledBrush` | `#5F6775` | `#A1A8B3` | Disabled buttons; since phase 5 a hidden branch's name in the left panel, and in solo mode the branches left out |
| `VcAccentBrush` | `#7C8CFF` | `#4353D8` | The line on the active tab, focus borders, the branch HEAD is on in the left panel, renamed files, parent links; since phase 5 the solo button, the search's progress bar and the graph's reload bar |
| `VcAccentTextBrush` | `#10121A` | `#FFFFFF` | Text on an accent background: the primary button's label, a check box's tick, the dot of a dialog's radio button |
| `VcSuccessBrush` | `#3FB97F` | `#187248` | Added files (the light value darkened in phase 6, 11.10) |
| `VcWarningBrush` | `#E0A23B` | `#895A17` | Modified files (the light value darkened in phase 6) |
| `VcDangerBrush` | `#E5606B` | `#B7323D` | Errors, deleted files; the danger button (the light value darkened in phase 6) |
| `VcDangerTextBrush` | `#FFFFFF` | `#FFFFFF` | Text on the danger button (`VcDangerButton`, phase 6, 11.11) |
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
| `VcSearchMatchBrush` | `#F2C94C` | `#B98900` | The 3-px mark at the left edge of a search's matching row (phase 5) |
| `VcLane0Brush` ... `VcLane7Brush` | `#7C8CFF` `#3FB97F` `#E0A23B` `#E5606B` `#4FB3D9` `#B07CE8` `#D97EB6` `#8FB84A` | `#4353D8` `#1C8655` `#A16A1B` `#C93A46` `#1D7EA6` `#8048C7` `#B54A8C` `#59811C` | Lane colours 0 to 7 (D48; light lanes 1, 2, 4 and 7 darkened in phase 6, D198) |
| `ToolTipBackground`, `ToolTipForeground`, `ToolTipBorderBrush` (Fluent's keys) | `#1A1D24` `#E4E7EC` `#323845` | `#F5F6F8` `#1B1F27` `#D2D6DE` | Tooltips, in the panel's colours (phase 6, 11.12) |

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
  its rows `RebaseListRow`, the `ConflictResolver` and its card, `ConflictPanes`, and since phase 5
  every page's root, the activity log, the comparison and `BlameGutter`), as `CommitGraphControl`
  has; the resolver's result and the blame's text are Documents with a read-only Value pattern. A
  button whose tooltip says what it would do (phase 5's Undo, Redo, Search and Activity log) has
  the same text as its `AutomationProperties.HelpText`.
- Every write to a repository is an operation of its queue. A new operation is a method of
  `RepositoryOperations` built on `RunAsync`, `ConfirmAndRunAsync` and `GuardAsync`, with its texts
  in `OperationTexts` (an `OperationText` per operation) and its git calls in `GitRepository`; it
  never throws, and it checks what it acts on when it runs, not at the click (D124). A menu item
  calls it through `IRepositoryActions`. One that destroys something saves its backup with
  `SaveBackupAsync` inside the operation, which also schedules the backups' clean-up (D139). Since
  phase 5 every operation names its undo scope (`RunOptions.Undo`: a factory of `UndoScope` for a
  recorded kind, `UndoScope.None` for one that is not, D168) and reports what its reads cannot see
  through `UndoCapture`; a new recorded kind also needs its sequences in `UndoSequence` and its
  checks in `UndoChecks`, from the closed set of effects. Its checks of HEAD before it writes take
  `ReadHeadAtStartAsync`, and where only what is in progress or what conflicts matters it reads
  `ReadInProgressStateAsync` and `ReadConflictedPathsAsync`, not the whole status.
- A view model that can be replaced in the graph area while it holds work (the list, the
  resolver) is left through `RepositoryViewModel.TryLeaveGraphAreaAsync`, never closed directly. A
  page holds no work and reads only, outside the queue; it opens through `OpenPageAsync` and
  shows a commit through `ShowCommit`, never by setting the selection itself.
- Async code in the Core and Git projects uses `ConfigureAwait(false)`, and so do App services
  whose tasks the app may wait for while it closes (`GitRepositoryProvider`); view models do not,
  so they continue on the UI thread.
- Services that can fail on the user's machine (settings, session, log) log the failure and carry
  on; they do not throw into the UI. A failure the user caused or can fix (a folder that is not a
  repository, git's own error) is shown where they acted: on the welcome page, in the clone form,
  in the details panel, in the stage panel for its own writes, and in the error dialog for any
  other operation (C11: git's own output, the command, "Copy output").
- Text that may be too long for its place is cut with "…" and shows whole in a tooltip.
- Since phase 6: a command with a key or a palette entry is an `AppCommand`, its keys a binding in
  `AppShortcuts`, its state and run a case of `AppCommands`; a menu item that runs a keyed command
  on the same object sets `MenuItem.Command`; every key text comes from `ShortcutText.Format`
  through `KeyTexts`, never from Avalonia's gestures; an enabled keyed button's tooltip is `{what it
  does} ({key})`, a disabled one's its reason alone, kept in an `XDisabledReason` property that the
  menus read, and its help text is its tooltip (D214, D215).
- Since phase 6 no folder or file check runs on the UI thread (D59): a folder on a share that does
  not answer can block for minutes, so the dashboard's folder checks, the provider's open, Open in
  editor's, Open file's (with its read of the file's Unix mode) and the open diff's file checks
  (`TabServices.FileExists`) and the settings screen's program checks run on the thread pool.
- Since phase 6 the app's own files are written through `DurableFile` (D236), and a
  `workspaces.json` that cannot be read is never written over (D203).

### Tests

Tests run on xunit.v3 with Microsoft Testing Platform (D24). The commands are in `CLAUDE.md`.

| Helper | Where | What it is for |
|---|---|---|
| `TempDirectory` | `tests/VisualCommit.Testing` | A folder under the system temp folder, deleted on dispose |
| `TempRepo` | same | Builds a real git repo for a test, cut off from the machine's git configuration, with a fixed author and a clock that advances one minute per commit, so the same steps give the same SHAs everywhere; `user.name` and `user.email` in the repo's own configuration give the app's commits an identity (D72). Helpers for text and byte files, commits, branches, tags (lightweight and annotated), merges, stashes, a bare remote next to the repo and pushes; `CopyAsync` makes an independent copy with its remotes pointed at the copy (http URLs left alone, linked worktrees joined to the copy) and a clock that carries on from the template's; anything else goes through `GitAsync`, `GitWithInputAsync` or `GitAllowingFailureAsync` (a step meant to stop). Phase 3 added `ConfigAsync`, `RefTargetAsync`, `BackupRefsAsync`, `StashListAsync`, `CloneSiblingAsync` and `OpenSibling` (siblings share the clock), `IsolatedEnvironment(home)`, `ClockTime` and `MoveClockTo` (forward only, for a step that must look newer in the reflog than an app operation, D111). Its git calls drop the machine's askpass, ssh and proxy variables too (D116), and since phase 4 run with `GIT_EDITOR=:` and `GIT_SEQUENCE_EDITOR=:`, so that a step such as `git rebase --continue` never starts the machine's editor (D151); `GitWithEnvironmentAsync(extra, args)` and its "allowing failure" twin add variables for one call (a sequence editor that writes a todo) and keep the clock's dates. Phase 5's speed-ups (`7e7d99d`), which every test project gets: a repository is a copy of a blank git folder that `git init` and the repository settings made once per test process and initial branch (a bare remote likewise), which saves six git processes per repository; a commit's SHA is read from git's summary line (`CommitArguments`, which adds `-c core.abbrev=no`, and `CommittedSha`), with no `rev-parse` after it; `maintenance.auto=false` in every repository and `receive.autogc=false` on bare remotes, so no commit, merge or push starts git's automatic maintenance. `ExactStateAsync()` is part P's "exact state" (HEAD, every ref, the index, the status with untracked and ignored files, every file's contents), which the scenario pins (`Phase5ScenarioTests`), both harnesses and the App and Git tests compare. Phase 6: the blank git folders leave out git's `hooks/*.sample` (a copy then has some 220 files fewer, D229), `AddBareRemoteAsync(name, relative: true)` gives the remote the URL `../<name>.git`, so a copy fetches from its own remote with nothing rewritten, and `Over(root, path, clock)` wraps a repository made by other means (the lean builder) with the class's isolation. Phase 7a: `CommitFileAsAsync(path, content, message, authorName, authorEmail)` commits as another author, the clock advancing as for `CommitFileAsync`; the isolation (`GitIsolation.MachineIndependence`) also removes `NETRC`, and the calls' `HOME` was always an empty folder of the class's (D340) |
| `Scenarios` | same | The scenario repos: `LinearAsync` (phase 0), `GraphAsync` (phase 1: branches in folders, a merge, tags, a remote with ahead and behind, a stash), `ChangesAsync` (phase 2: staged, unstaged and untracked changes of every kind the diff view shows; its contents in `Scenarios.ChangesFiles`) `CrlfAsync` (phase 2: `core.autocrlf` in the repo's own configuration) and `ConflictAsync` (phase 2: a merge stopped with a conflict). Phase 3's, in `Scenarios.Phase3.cs`: `OperationsAsync` ("ops": branches, tags, a stash, a bare `origin`), `StoppedAsync(kind)` (an ops copy stopped in a merge, cherry-pick sequence, revert, rebase, or with stash conflicts, as a terminal would leave it; built each time), `RemoteSyncAsync` ("sync": published, unpushed and local branches) with `PushFromElsewhereAsync`, `CheckoutBlockedAsync` ("blocked"), `EdgeCasesAsync` ("edges": a detached HEAD, a linked worktree, a local upstream, a branch named like a remote branch, a tag named like a branch), `AuthRemoteAsync(server)` ("auth": a clone of what `AuthRemoteServer` serves). Phase 4's, in `Scenarios.Phase4.cs`: `RebaseAsync` ("rebase": `feature/topic` and `feature/other` to rebase onto `main` without a conflict, a bare `origin`), `InteractiveAsync` ("interactive": one line of eight commits on `main`, one a fixup candidate, one with a `#42 is the issue` body line), `ConflictsAsync` ("conflicts": `main` and `feature/theirs`, `feature/rebase`, `feature/picks`, `feature/kinds`, with notes.txt, other.txt, image.png and new.txt) and `ConflictsStoppedAsync(ConflictsStop.Merge | Rebase | CherryPicks | MergeKinds, mergeTool)` (stopped as a terminal would leave it, built each time, optionally with the fake merge tool of `ConfigureFakeMergeToolAsync`); their files in `RebaseFiles`, `InteractiveFiles` and `ConflictsFiles`, whose lines are short enough for a resolver pane at 1100×700. `GraphAsync`, `ChangesAsync`, phase 3's and phase 4's (except the stopped ones) are built once per test process as templates and copied for each test; `Phase3ScenarioTests` and `Phase4ScenarioTests` pin their refs, SHAs, graphs and stopped states. Phase 5's, in `Scenarios.Phase5.cs`, built once per process as templates and pinned by `Phase5ScenarioTests`: `HistoryAsync` ("history": three authors, `src/Parser.cs` renamed to `src/Reader.cs` with an edit, blame lines from four commits, a file deleted and added again, a merge, words only in commit bodies, a pickaxe string added and removed, `main` and `feature/compare` diverged), `FiltersAsync` ("filters": branches to hide and solo, their upstreams, `origin/HEAD`, two stashes), `ReflogAsync` ("reflog": lost commits, a gap in HEAD's reflog, a deleted branch, backups of every kind the list shows, dated against the fixed backup clock; built with 88 git calls), `ReflogManyBackupsAsync` (built each time) and `AddOldAndManyBackupsAsync` (old and more than 50 backups, for the clean-up's notes), `ProtectAsync` ("protect": a branch whose checkout the checks' own changes block, a file `main` ignores that the two oldest commits track); their files in `HistoryFiles`, `ReflogFiles`, `ProtectFiles`. `Scenarios.Phase5.Shell.cs`: a merge tool that waits until released (`ConfigureWaitingMergeToolAsync`). `Scenarios.MergeToolTimeout` (120 s, D185) bounds every wait for `git mergetool` to start. Phase 6's, in `Scenarios.Phase6.cs` (D229), built once per process and pinned by `Phase6ScenarioTests`: `WorkspaceAsync` (the workspace "Web" of ten repositories in mixed states, a `TempWorkspace`), `ServeWorkspaceAsync(copy, requireAuth, names)` (its remotes over `AuthRemoteServer`'s folder mode), `RenamingRefspecAsync` ("refspecs"), `IgnoredAsync` ("ignored") and `GitSettingsAsync` ("settings"). Phase 7a's, in `Scenarios.Phase7a.cs`, built once per process and pinned by `Phase7aScenarioTests`: `HostingAsync` ("hosting": `hello/`, `tool/` and the fake's six bare repositories in `github/`) and `ServeHostingAsync(hello, port, inProcess, ct, ownsCopy)` (the fake of GitHub on them, a `ServedHosting`); see "The fake of GitHub" |
| `TestImages` | same | Small PNGs whose bytes depend only on their pixels (stored deflate blocks), for scenario repos |
| `FakeRepositoryBase` | same | The start of a fake `IGitRepository` for view-model tests: a clean working tree, every write refused; phase 3's and phase 4's reads give empty results (no remotes, no worktrees, nothing detached, no merge tool, no operation message, a clean-up that removes nothing) and their writes throw `NotSupportedException`; phase 5's reads find nothing (no history, blame, reflog, backups, differences, matches, ignored files or stash paths), the undo history's read sees HEAD alone, so that no action leaves an entry, and an undo step and C12's writes throw; phase 6's reads find no ignored file in the way and no pull setting, `OpenedAtTopAsync` answers true, and the configuration write throws; phase 7a's read finds no URL rewrite and its fetch of a pull request's head into `FETCH_HEAD` throws |
| `LargeHistory` | same | The 100k-commit repo (D51), built once per machine with `git fast-import` under `%TEMP%/VisualCommit.Tests/shared/` and shared read-only; what each row shows follows from its number. Since phase 4 `GetBareAsync()` gives a bare copy made once beside it (`large-history-v1.git`), which `AuthRemoteServer` serves in place for phase 1's clone checks (D150) |
| `GitIsolation` | same | Cuts the test process off from the machine: no system or user git configuration, no search for a repository above the tests' own temp folder (`GIT_CEILING_DIRECTORIES`), none of the variables a surrounding process can hand git, and dates in UTC; since phase 3 also no askpass program or ssh command of the machine (`GIT_ASKPASS`, `SSH_ASKPASS`, `SSH_ASKPASS_REQUIRE`, `GCM_INTERACTIVE`, `GIT_SSH`, `GIT_SSH_COMMAND`, `GIT_SSH_VARIANT`), no proxy (`no_proxy` set to `127.0.0.1,localhost`) and auto-fetch off (`VISUALCOMMIT_AUTO_FETCH_MINUTES=0`) (D112, D116); since phase 4 no editor of the machine (`EDITOR`, `VISUAL`, `GIT_EDITOR`, `GIT_SEQUENCE_EDITOR`, D151). `ApplyMachineIndependence` does the same for a process about to start (the real-window pass's app). The UI test assemblies call it through `HeadlessTestApp.IsolateFromTheMachine` in a module initializer, `VisualCommit.Git.Tests` directly. Phase 6 (D220, design 17.2): the test process's `GIT_CONFIG_GLOBAL` names a file in a folder nobody makes (`NoGlobalConfigFile`), so reads see no global configuration and a global write outside an app session fails with exit 255 in the test that made it, and its `XDG_CONFIG_HOME` is an empty folder of the tests' (`ProcessXdgConfigHome`, `%TEMP%/VisualCommit.Tests/isolation/xdg`), so the user's `~/.config/git/ignore` and `attributes` reach no call; `GIT_AUTHOR_*`, `GIT_COMMITTER_*`, `EMAIL` and `MSYSTEM` are removed and `PATHEXT` is Windows' default; `SessionGitEnvironment(data)` gives an app session its own `GIT_CONFIG_GLOBAL` (`<data>/harness/global.gitconfig`) and `XDG_CONFIG_HOME`, which both harnesses pass; the tripwire `GlobalConfigurationWrittenOutsideASession` lists the traces a global write outside a session leaves in the isolation folder, which every test process shares (`%TEMP%/VisualCommit.Tests/isolation`: its `no-global` folder made, a `git` folder in its `xdg`, the old shared `gitconfig` not empty); `GitIsolationTripwireTests` (in `GitIsolationTests.cs`) and `Phase6TripwireTests` (in `Phase6SupportTests.cs`) assert that it lists nothing, each in a collection with parallelization off, which xunit runs after the parallel tests, so that every git call of their process has ended and what the projects beside them wrote until then is seen too; `ShellTests` and `Phase6SupportTests` also assert it at the end of a test of a session's git environment. Phase 7a (design 1.8, 19 item 4; ruling 24, D340): `MachineIndependence` also sets `VISUALCOMMIT_GITHUB_URL=none` and `VISUALCOMMIT_KEYCHAIN=memory` and removes `GH_TOKEN`, `GITHUB_TOKEN`, `GH_ENTERPRISE_TOKEN`, `GITHUB_ENTERPRISE_TOKEN`, `GH_HOST` and the live command's two variables (`HostingTokenVariables`), so every test process and the app the real-window pass starts begin with GitHub off and tokens in memory, a check's own variables overriding; it removes `NETRC` (`NetrcVariables`) and leaves `VISUALCOMMIT_WATCHER_LAG_MS` alone; `SessionGitEnvironment(data)` also gives `HOME = <data>/harness/home` (`SessionHomeFolder`), empty, so that the curl of a session's git calls never reads the machine's `.netrc` or `_netrc`, which answers a server's 401 before any credential helper. Runners that use neither `TempRepo`'s environment nor a session's (App.Tests' `OperationsHarness`, static runners, `LargeHistory`, `AuthRemoteServer`'s own) still see the machine's `HOME` (plan.md's risk 37) |
| `AuthRemoteServer` | same | The credential test server (D97): a `TcpListener` on 127.0.0.1 serving `git http-backend` through `GitRunner` as CGI for one bare repository (its own `remote.git`, or an existing one in place), asking for HTTP Basic auth (alice/s3cret) unless `RequireAuth` is false. Port 0 (the system picks) by default; `StartForVisualChecksAsync` uses 47391 (`VisualChecksPort`) so the pictures do not change, or since phase 4 47392 (`Phase1VisualChecksPort`) for phase 1's clone checks, which run in another process at the same time as phase 3's (D150), falling back to a free port (`PortWasTaken`). `Requests`, `Errors`; `HoldNextFetchPack(atPercent)` holds the next fetch's pack until `Release()`, so a check can show and cancel a running fetch: with 0 after its first progress line with a percentage, with a percentage right after the first whole progress line at or above it, the packet re-framed so that git's last line shows exactly that percentage (phase 1's clone is held at 45 %, so it cannot race). Since phase 6 (D229) `StartForFolderAsync(folder, port)` serves a folder of bare repositories, each at its own URL with its own hold at a chosen percentage (`UrlOf`), on `Phase6VisualChecksPort` 47393 for phase 6's walk-through and the real window, and on `Phase6LookVisualChecksPort` 47394 for `VisualCommit.VisualTests.Phase6Look`, which runs beside it; its own tests take a system port. Since phase 7a (D266) `StartForHostAsync(folder, options, port)` stands in for a git host, `Serve("owner/name")` registering each repository, with Basic auth by token (`LoginOfToken`), anonymous reads of public repositories, pushes by pushers only, a hook for the routes it claims and `GivenUserName` in `AuthRemoteRequest`; `Phase7aVisualChecksPort` is 47395 (D265). See "The fake of GitHub" |
| `FakeFileLauncher`, `FakeClipboard` | `tests/VisualCommit.Testing/Headless` | Record what the app asked to open, show or copy, and touch nothing on the machine; `ShellDriver.Start` injects them (`Launcher`, `Clipboard`). Since phase 7a the launcher records "Open in browser" as `FileLaunchKind.OpenUrl` with the address, and fails an address that is not `http` or `https` with the real launcher's `ArgumentException`, recording nothing |
| `ScriptedPromptHandler` | `tests/VisualCommit.Testing` | Answers prompts from a queue of answers and records every prompt; a prompt without an answer is refused. Injected through `AppSession.Start` or `ShellDriver.Start` (`promptHandler`), or given to a broker |
| `AskPassRunner` | same | Runs the real helper as OpenSSH runs `SSH_ASKPASS` (the prompt as its argument, `SSH_ASKPASS_PROMPT` when ssh sets it, an open scope's environment) for the prompt dialogs no ssh server can reach; since phase 7a `RunCredentialAsync` runs it as git runs a credential helper (`--vc-credential` and the action, the input written and closed), for the credential tests (see "Prompts and credentials") |
| `FakeSsh` | same | A shell script set as a remote's `core.sshCommand` that runs `$SSH_ASKPASS` with recorded OpenSSH texts (passphrase, unknown host, PIN), writes the answers to a file and exits 1 (D115) |
| `AppLogFile` | same | The app's log as checks read it, and the patterns of the lines they wait for or count, in both harnesses. Since phase 5 every test reads the log only through it (`ReadText` opens the file sharing it for writing): a plain `File.ReadAllText` is refused on Windows while the app writes an entry. Since phase 7a `PullRequestsListed(repo)` and `AvatarsShown(repo)` match the Debug lines `{repo}: listed {n} open pull requests of {owner/name}` and `{repo}: showing GitHub avatars for {n} authors` (groups `count` and `repository`), each written on the UI thread once the list or the avatars are applied (the second by the active tab's graph only); both harnesses take a picture of an opened GitHub repository while signed in only after both (D322) |
| `WindowLayout` | same | The window's fixed layout in logical pixels, copied from the reports (tab strip, toolbar, panels, column headers, rows 26 high, the banner's minimum, the menu's heights and gaps) and the neutral point (760, 687) where both harnesses rest the pointer before a capture that is not about a hover. Both harnesses compute positions from it |
| `HeadlessTestApp` | `tests/VisualCommit.Testing/Headless` | Builds the real `App` for headless mode with Skia rendering. Each UI test assembly names it in `AssemblyInfo.cs` |
| `ShellDriver` | same | Starts an `AppSession` on a data folder (`Start`, or `StartWithTabs` with repositories open through the session file), shows its window at a size, and drives it with simulated input: click, right-click, double-click, drag, wheel, hover, typing and keys. `Folders` is the fake folder dialog; `WaitForAsync` waits for a condition while the UI thread keeps running; `Capture()` returns a `Screenshot`. Partial since phase 3: `ShellDriver.Phase3.cs` (clicks with modifiers, a drag in steps, `Menu()` as a `MenuSnapshot`, `ClickMenuItem`, `MenuItemBounds`, the open dialog, `TypeInto`, `WaitForIdleAsync` for the queue and its refresh), `.Pointer.cs` (the neutral point, resting the pointer until a tooltip opens), `.Log.cs` (the app's log lines, git's call records, step times), `.Checks.cs` (opening a repository, rows below the banner, left-panel and file rows, the error dialog as an `ErrorDialogSnapshot`, progress, closing the window); phase 4's `.Phase4.cs` (`Undo()` and `Redo()` by the first gesture of `HotkeyConfiguration.Undo` and `.Redo`, the banner's Continue and Skip, the result's caret held still: `ConflictResultEditor.CaretBlinks = false` in `Start`), `.Phase4.Rebase.cs` (the list as a snapshot, a row's point, a drag of a row in steps, a row's action through its menu) and `.Phase4.Conflicts.cs` (the resolver as a snapshot, a bar's button, the result's text, caret and modified state, typing into it, a click at a line and column). `WaitForIdleAsync` also waits for the stage panel's read of the stopped commit's message (`StagePanelSettled`: `IsFillingMessage` false), which fills its boxes after the status is shown. `FindByAutomationId` looks in the open menu first, then in the top dialog, then in the whole window. Phase 5: `Start` gives the session a frozen record clock and a fixed backup clock at 2026-01-02 09:00:00 UTC (`AppClocks.HarnessTime`); `DefaultWaitTimeout` is 60 s since phase 6's gate (D237: with nine test projects side by side a git call took 10 to 15 times as long as alone, and a pop of 24 calls ran past 30 s; it was 10 s before phase 5, 30 s from it); `DoubleClick` stops the window's input clock for its two presses (`HeadlessInputClock`), so that a busy first click cannot split a double-click into two clicks, and since phase 7a (CI's run on WP-P's merge) it first waits, running the dispatcher's jobs, until that clock (`HeadlessInputClock.Read`) stands more than the double-tap time of 500 ms past the last press in the window, by anyone, when that press lay within the double-tap size of 4 by 4 pixels around its spot: Avalonia's `MouseDevice` counts a press on from the last one there whatever control either went to, so a double-click right after a click at its spot came as that click's second and third press (`LeftPanelPullRequestsViewTests`' double-click on pull request #5, which had taken the clicked sign-in row's place); `DoubleClick(Visual)` reads the control's centre again after that wait, and `DoubleClick` throws an `InvalidOperationException` naming the counts and the press before when its presses are not counted 1 and 2; `Click` and `RightClick` do not wait, so two presses at one spot within 500 ms are a double-click there as for a user (phase 7a's design 17 item 9); `.Phase5.Shell.cs` (`PressSearch`, `PressGitUndo`, `PressGitRedo`, the activity log as a snapshot, `WaitForOperationRecordAsync`), `.Phase5.Find.cs` (the search bar and its marks read from the graph control's state, never from pixels, the comparison and `WaitForComparisonShownAsync`, the left panel's marks, the waits for a search and a filter's reload), `.Phase5.Graph.cs` (a lane's centre read as dashed or solid), `.Phase5.Pages.cs` (the pages as snapshots and their waits), `.Phase5.Protect.cs` (the status bar's text as laid out, the stash calls since a mark, the wait for a status read) and `.Phase5.Undo.cs` (the toolbar's Undo and Redo as both harnesses read them). Phase 4's `Undo()` and `Redo()` stay the text undo of a text box and the resolver. Phase 6 (design 17.2): `Start` gives the session `GitIsolation.SessionGitEnvironment` on its data folder (`GlobalGitConfigPath`, `SeedGlobalGitConfig` writes it before a start; a restart on the folder keeps it), `ToolCatalogues.Fixed` (or `tools`, such as `HeldMergeTools`), a `FakeSystemTheme` dark unless `systemDark` is false (`SystemTheme`), and an optional `readClock` (`ReadClock`); its parts `.Phase6.Workspaces.cs` (`WriteWorkspaces`, `StartWithTabs` with `TabState`s, `StartWithWorkspace`, the welcome page's section, the Add repositories dialog), `.Phase6.Dashboard.cs` (the dashboard as a `DashboardSnapshot`, `WaitForDashboardReadAsync` on the pass's line, `ActivateWindow`, a row's point, menu and open, `CallMark` and `CallsSince`), `.Phase6.Bulk.cs` (Fetch all, Pull all, Cancel all, a row's percentage, Details… and Cancel), `.Phase6.Palette.cs` (`PressPalette`, `TypeInPalette`, `PaletteSnapshot`, `RunPaletteEntryAsync`), `.Phase6.Keys.cs` (`PressShortcut` from the app's own `Shortcut`, the focused region, `AcceleratorKey` and help texts, a menu item's key), `.Phase6.Settings.cs` (the screen as a `SettingsSnapshot`, its sections, scopes, fields and Save), `.Phase6.Theme.cs` (`SetSystemThemeAsync`, a token's colour) and `.Phase6.TabStrip.cs` (`TabStrip()`); and in `.Phase3.cs` `WatchersQuiet` and `WaitForWatchersQuietAsync()`, every open tab's `RepositoryViewModel.IsWatcherQuiet`, which part S waits for before it marks and reads a step's git calls. Phase 7a (design 2.5, 1.9): `Start` and `StartWithTabs` take, last, `gitHub:` (a `FakeGitHub` on a port), `tokens:` (a `FakeTokenStore`, new per start unless given) and `hosting:` (options as given), refuse both together and an in-process fake as `gitHub:`, put every HTTP client but `hosting:`'s behind a `LoopbackOnlyHandler`, and pump the UI thread until `Session.HostingStarted` has completed (see "The fake of GitHub"); the session's git environment has its own empty `HOME`. `.Phase7a.cs`: `Tokens`, `GitHub`, `Hosting`, `RefusedRequests`, `SignInAsync(token)` (through the session, no dialog; anything but signed in throws with the failure's kind and message, never the token); `.Phase7a.Accounts.cs`: the Accounts section and the "Add GitHub account" dialog (`OpenAccountsAsync`, `OpenSignInDialogAsync`, `TypeToken`, `ConfirmSignInAsync`, `WaitForSignInCheckAsync`, `SignOutAsync`, `WaitForAccountAsync`); `.Budget.cs`: `QuietCallMarkAsync()` and `QuietCallsSinceAsync(mark, inFolder)` (S8, `GitCallBudget`); `.Pointer.cs`: `WaitForControlAtAsync(Point)`, a hit test polled until the window has drawn a control there, since `WaitUntilReadyAsync` renders no frame (the wheel test failed without it in a bare Ubuntu container) |
| `FakeFolderPicker` | same | Answers the app's folder dialogs with folders the test queued (D42) |
| `Screenshot` | same | A captured frame: save as PNG, read pixel colours |
| `LayoutAudit` | same | `FindClippedText` lists every visible text that is not shown in full; with `allowShortenedWithToolTip` it lets through text cut with "…" on purpose whose whole text is in a tooltip, and with `allowScrolledOutOfView` text cut only by a scrolling list's edge (since phase 5 also a list's partly shown row where a page around the list ends too). A wrapped line's trailing space is not counted |
| `FreshApplication` | same | Runs part of a test with a new Avalonia application object: how a scripted check crosses a restart (D32). Its wait for its turn on the headless UI thread does not count against the test (D132) |
| `VisualRun`, `VisibleTexts` | same | Where the walk-throughs save their pictures (`artifacts/visual/phase-N/scripted/`); the texts a region shows, as a person reads them. Shared by the walk-through projects. Since phase 5 a phase's folder is emptied once per run, not once per process (`RunKey`: the `dotnet` process that started the test process, named by its id and start; a `.run` file in the folder names the run whose pictures it holds, and the processes take turns at it), so phase 5's two projects fill one folder, and phase 6's two (`Phase6`, `Phase6Look`) theirs, emptied once by one `dotnet test`. Since phase 7a a phase is a string (`FolderFor(string)`, `Save(Screenshot, string, string)`; the `int` forms delegate, as `Screens` and `RealWindowRun` do in the real-window project), so 7a's pictures go to `artifacts/visual/phase-7a/` (design 2.7) |
| `TempWorkspace`, `LeanRepositories` | `tests/VisualCommit.Testing` | Phase 6 (D229, design 17.1): the workspace "Web", ten repositories in the dashboard's order (`api` … `notes`, and `old-site`, listed and never made), eight with a bare remote under `remotes/<name>.git` reached by the relative URL `../remotes/<name>.git`, so a copy is files only and fetches from its own remotes. Built once per test process the lean way (`LeanRepositories`: `git fast-import` and configuration written as text in a copy of the blank git folder, 28 git processes for the ten instead of about 80), then copied for each test |
| `FetchHold` | same | Phase 6: holds the fetches of chosen repositories from their file remotes until a test lets them go: a shell script set as the remote's `remote.origin.uploadpack` in each held repository's own configuration (never the session's global one), which waits for a file `release` before it hands over to `git upload-pack` |
| `FakeSystemTheme`, `HeldMergeTools`, `DrawnLines` | `tests/VisualCommit.Testing/Headless` | Phase 6: the operating system's mode for a headless session (`Set` flips it); merge-tool catalogues that hold their list back until released, for the settings screen's loading state; the lines a text block draws, read from its layout, where it wrapped and where it cut (the long folder's checks), without `UrlTextBlock`'s and `PathTextBlock`'s marks |
| `Contrast`, `TextInk` | `tests/VisualCommit.Testing` | Phase 6 (design 11.10 to 11.15): WCAG 2's contrast ratio of two colours (`ThemeTokensTests` holds every light text token to 4.5:1 on each background it lies on); the colour a small text is drawn in, read from a picture: per channel the extreme toward the foreground within the text's rectangle, which must lie within `Tolerance` (8) of the token's colour, since subpixel smoothing rarely draws a pixel of exactly that colour |
| `WorkspaceTestSupport`, `DashboardTestSupport`, `BulkTestSupport`, `KeysTestSupport` | `tests/VisualCommit.App.Tests` | Phase 6: a session's services on a workspace store in memory with real git behind the provider and a fake folder dialog, a host that records what it is asked, a tree of real repositories and a folder link as each platform makes one; the dashboard's, the bulk runs' and the keys' fakes and builders |
| `ThreadWork` | `tests/VisualCommit.App.Tests` | Phase 6: the work the calling thread has done, as the processor counts it (its cycles on Windows, its processor time elsewhere), which does not grow while the thread waits for a processor other test projects keep busy, so two runs on one thread can be compared under load as a ratio (`PaletteGrowthTests`, `DiffTextViewGrowthTests`) |
| `ManualTimeProvider` | same | A clock that moves only when the test moves it (`Advance`), with timers that fire against it in order: the watcher's quiet period (D160) |
| `HeadlessInputClock` | `tests/VisualCommit.Testing/Headless` | Stops a headless window's input clock (Avalonia.Headless's internal `Stopwatch`, which stamps every pointer and key event in real time) while `ShellDriver.DoubleClick` presses twice: Avalonia counts a second press as a double-click only within 500 ms of the first, and under the default run's load the work between the presses took longer (P25's double-click came as two clicks). `Read` gives the clock's time, which `DoubleClick` waits on after a press at its spot, and throws while the clock is paused, where such a wait would never end. Throws when the window is not a headless one or an Avalonia upgrade moved the clock |
| `HangWatchdogAttribute`, `HangClock` | `tests/VisualCommit.Testing` | `[assembly: HangWatchdog]` in each default test assembly. If a test runs for more than 3 minutes, or nothing starts or finishes for that long, it prints which tests were running and stops the test process. `VISUALCOMMIT_TEST_HANG_SECONDS` changes the limit. A test's wait for a shared preparation (`SharedPreparationAsync`: a scenario template, the 100k-commit repo, the pull-parity templates) and for its turn on the headless UI thread (`WaitingForTurn`) is left out of its time; a preparation is held to a limit of its own, since phase 6's gate 10 minutes (`SharedPreparationLimit`, D237: the reflog scenario's template, 18 s alone, ran past 180 s with nine projects side by side; D132). Since phase 5 so is a test's wait for git's merge tool to start (`WaitingForSlowProgram`, D185): `git mergetool` is a shell script of over a hundred process starts, about 3 s alone and 32 to 64 s with the default run's projects side by side; the wait has its own limit of 120 s (`Scenarios.MergeToolTimeout`). Since phase 6 such a wait may name a limit of its own in place of the run's (`WaitingForSlowProgram(what, limit)`, for `git mergetool --tool-help`, over 180 s under the default run's load), and while it is within it the "nothing starts or finishes" rule waits for it. `HangClock` holds these rules apart from the watchdog, so they are tested with a clock moved by hand |
| `GitIsoDates` | same | Reads git's strict ISO dates as instants: Git 2.36 writes `+00:00` for UTC, Git 2.55 (CI's `ubuntu-latest` until 2026-10-28) `Z` |
| `FakeGitHub`, `FakeGitHubHandler`, `LoopbackOnlyHandler`, `TokenTripwire`; `FakeTokenStore` | `tests/VisualCommit.Testing/Hosting`; `FakeTokenStore` in `Headless` | Phase 7a: the fake of GitHub (on a port or in process), the harness's network tripwire, the token's tripwire over files and git calls, and the harness's keychain; see "The fake of GitHub" |
| `GitCallBudget`, `RefreshForm` | `tests/VisualCommit.Testing/Headless` | Phase 7a's S8 (D280, D281, design 5.3): with a live watcher a count of git calls is marked and read with the watchers quiet (`ShellDriver.QuietCallMarkAsync()`, `QuietCallsSinceAsync`; App.Tests' `OperationsHarness` has the same pair over `WaitForWatcherQuietAsync`, and `WaitForOpeningCleanUpReadAsync` for a mark right after opening), and a refresh the watcher started for a change reported late is set aside only whole. `RefreshForms`: the refs read's five forms and the status read, moved from phase 6's part S helpers, and, optional (counted 0 times or as often as the others), the URL-rewrites read a signed-in tab makes after each refs read (`git config --null --get-regexp ^url\.`). `NotWholeRefreshes` says what keeps calls from being whole refreshes (a call of a form inside an operation, a required form less often than another); `AssertOnlyWholeRefreshes` fails printing the calls; `AssertAtMost(calls, budget, what, setAsideWholeRefreshes)` holds a count that is a cost to a budget. Three counts that are a cost became budgets (`IgnoredInTheWayTests`, `RefReadingTests`, `Phase6ScenarioTests`); seven with a live watcher are marked and read quiet with whole refreshes set aside; counts that are a test's meaning stay exact (D280). 7a's checks count git calls only through it |
| `RecordingCredentialSource` | `tests/VisualCommit.Testing` | Phase 7a: a credential source for the app's helper that answers every `get` with `Token` (null: none) and records each `get` and refusal by account, never showing the token (see "Prompts and credentials") |
| `HostingSessionSetup`, `HostingTestThread`, `RecordingHttpHandler` | `tests/VisualCommit.Hosting.Tests` | Phase 7a (design 7.13): a hosting session over a copy of "hosting" served in process, made and not started: the fake's handler behind a recorder, a `FakeTokenStore` unless given, a data folder of its own, a `ManualTimeProvider` at the harness's time and a `HostingTestThread` (one thread with a `SynchronizationContext` of its own, where tests call what the app calls on its UI thread) as the post delegate; `NetworkDown` fails requests before the fake. Its disposal runs `TokenTripwire` for the scenario's three tokens over the data folder and asserts no log line holds `_FAKE-`, no error on the thread and none in the fake |

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
  prints each step's time (its second argument picks the phase and the project: 7a when left out,
  since phase 7a, which runs `VisualCommit.VisualTests.Phase7a`, and a phase it does not know exits
  2 before anything is built; for phase 5 the filter picks the project, `Phase5Undo` for part U and
  `Phase5` otherwise, and for phase 6 `Phase6Look` for part L and `Phase6` otherwise).
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
- Phase 5's walk-through is two projects (D181): `VisualCommit.VisualTests.Phase5` for parts A
  (the shell and the activity log), B (the pages and "All files"), F (search, compare, hide and
  solo, dashed stashes) and P (protected changes), 34 test methods, and
  `VisualCommit.VisualTests.Phase5Undo` for part U (undo and redo), 55 test methods, one action
  with its undo and redo each on a fresh copy, so that none comes near the hang watchdog's 3
  minutes. The part letters avoid C, so that a check's name never reads as the requirement C12.
  Both are built as phase 4's (`Phase5Checks.cs` and `Phase5Checks.Support.cs` the base and its
  support, a class per part over files `Phase5Checks.<part>.<topic>.cs`, expectations in
  `Phase5Expectations.Common.cs` and one file per part, their tests named `Phase5ExpectationTests.*`
  and `Phase5SupportTests.cs` so that the real-window project's link pattern
  `..\VisualCommit.VisualTests.Phase5*\Phase5Expectations*.cs` does not take them); the undo
  project compiles the other's base class, support and common expectations, and both save their
  pictures in `artifacts/visual/phase-5/scripted/`. The 27 checks are in phase 5's report, whose
  "Decisions this report makes" bind the build (D183).
- Phase 6's walk-through is two projects (D230, the report's ruling 1):
  `VisualCommit.VisualTests.Phase6` for parts W (workspaces and the dashboard, W1 to W9), K (keys and
  the palette, K10 to K13), S (settings and the theme's following, S14 to S21) and G (ignored files
  and refspecs, G22 to G25), 38 test methods (40 since phase 7a, below), and
  `VisualCommit.VisualTests.Phase6Look` for part L
  (L26 to L33: every main screen in both themes, the long folder, the tab strip), 9 test methods,
  which compiles the other's `Phase6Checks.cs`, `Phase6Checks.Support.cs` and
  `Phase6Expectations.Common.cs`; both save their pictures in `artifacts/visual/phase-6/scripted/`.
  They are built as phase 5's (a class per part, `Phase6ChecksW` to `Phase6ChecksL`, partial over
  `Phase6Checks.<part>.<topic>.cs`; expectations per part; `Phase6ExpectationTests.<part>.cs` and
  `Phase6SupportTests.cs` named so that the real-window project's link pattern
  `..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs` does not take them). The support adds
  the dashboard's assertions, `OpenWorkspaceAsync` and `ShotInBothThemesAsync` (a picture in each
  theme by the fake system theme). Part S's read-backs (`Phase6Checks.S.Helpers.cs`) wait until
  every watcher is quiet (`ShellDriver.WaitForWatchersQuietAsync`) before they mark a step's git
  calls (`QuietMarkAsync`) and before they read them, and let through, besides a step's own reads
  and writes, only whole refreshes outside every operation (`NotWholeRefreshes` over
  `RefreshForms`: the refs read's five forms and the status read, each as often as the others):
  macOS reports the app's own `git config --local` after its operation, so the refresh it starts
  can land in a later step's calls (run 39948457690). Each project's test server has a port of its
  own (47393 and 47394), since the two run side by side. On `fb4aa59` `Phase6` ran 130 tests and `Phase6Look` 19.
  The timing script sends phase 6 to `Phase6` and its part L to `Phase6Look`. Phase 7a's first
  package (D254) changed some of its checks, with their steps, expected results and pictures kept:
  W1 (both methods), W2 and W9's second start give the app a held read clock (`ManualTimeProvider`,
  never moved), as W8 did, so that a dashboard pass ends only when its reads have and no patience
  timer fires under load (W1 had logged 7 reads of 8 under a full run); and G23's part B, five pulls
  in one test that the hang watchdog stopped in all four default runs on Git 2.56 before the split,
  is three tests of at most two pulls, steps 9 to 11, 9 and 12, and 9, 13 and 14, each building its
  state before its own start, the third on IG1v (D264, D283).
- Phase 7a's walk-through is one project, `VisualCommit.VisualTests.Phase7a` (design 0.5, 2.8): 27
  test methods in one class per part, `Phase7aChecksA` (accounts and sign-in, 8), `Phase7aChecksB`
  (browse and clone, 4), `Phase7aChecksP` (pull requests, 4), `Phase7aChecksG` (the graph's badge
  and avatars, 4) and `Phase7aChecksN` (a new pull request, 7); the part letters avoid C, O, Q, R
  and T. It is built as phase 6's: the base `Phase7aChecks`, its support `Phase7aChecks.Support.cs`,
  the expectations in `Phase7aExpectations.Common.cs` (`Phase = "7a"`, `ServerPort`, `WithPort`, the
  scenario's names and tokens) and one file per part with its nested `PartA` … `PartN`, and
  `PartG`'s and `PartN`'s `PrepareAsync`, which both harnesses call on the copy before the app
  opens; `Phase7aExpectationTests.<part>.cs` and `Phase7aSupportTests.cs` are named so that the
  real-window project's link pattern does not take them, and the `*.G.cs` files are marked as no
  generated code in `.editorconfig`, as phase 6's. Every check starts on a fresh copy of "hosting"
  served by the fake on 47395 (`ServeAsync`) and opens it with `OpenAsync` (one tab on `hello` or
  `tool`, `gitHub:`, a sign-in with `OctoToken` through the session unless `signIn: false`, then a
  wait until the watchers are quiet, the sign-in's URL-rewrites read included) or `StartEmptyAsync`
  (one empty tab, waiting for the start-up work, whose `git --version` had landed after A1's quiet
  mark on CI's Windows job, design 17 item 2). xunit runs the part classes side by side, and a check
  that finds 47395 taken falls back to a free port (`b06a` showed 59807 in a run at WP-V's merge),
  so every expected text that holds the port goes through `WithPort`. `Shot` saves under
  `artifacts/visual/phase-7a/scripted/` and the step times under `phase-7a/timings/`; before the
  capture it waits for the details panel's read and, while the details name an author's image the
  panel does not show yet, for that image; `ClaimPictureName` refuses a name a step of the run saved
  already. A picture of an opened GitHub repository while signed in waits for D322's two log lines
  (`AppLogFile`). Call assertions take a `long` mark from `QuietCallMarkAsync` and prove presence
  and order only; counts go through `GitCallBudget`. Every check ends with `RefusedRequests` empty,
  the fake's `Errors` empty, no API request answered 400 or 415, and the scenario's tokens absent
  from the data folder, the clones and the session's git calls (`TokenTripwire`). The project also
  holds 7a's tests that need no real git beside the checks (the view models of the sign-in dialog,
  the Accounts section, the repository browser and the create form; the pull-request section and its
  checkout plan; the menus; the graph's badge and avatars; the details panel's avatar; the status
  bar's offer; `HostingTexts.Describe`), linking App.Tests' `RecordingActions.cs` and
  `RecordingLog.cs`, so that 7a's menu tests live outside App.Tests (design 0.6). Its checks are in
  phase 7a's report.
- `VisualCommit.Hosting.Tests` (phase 7a; design 1.1, D334) holds the hosting's unit tests by
  subject: the fake's own (`FakeGitHubTests`, `FakeGitHubGitTests`, `FakeTokenStoreTests`), the
  options (`HostingOptionsTests`, `HostingSmokeTests`), the client and provider
  (`GitHubClientTests`, `GitHubProviderTests`, `GitHubRecognitionTests`, `AvatarCacheTests`), the
  rules (`HostingRulesTests`), the session (`HostingSession*Tests`, `JsonAccountStoreTests`,
  `RepositoryHosting*Tests`) and the token stores (`Tokens/`). Its three real-store classes skip
  unless `VISUALCOMMIT_KEYCHAIN_TESTS` names them (CI's keychain steps, see "CI"), and
  `WindowsCredentialStoreTests`' off-Windows test skips on Windows, so a default run skips four
  tests on Windows and three on macOS and Linux (D270). By D334 it also holds six classes with real
  git (five start the app through `ShellDriver`; `RepositoryHostingWiringTests` builds a tab's
  `TabServices` by hand with a `HostingSession`), 44 test methods with clones, checkouts and pushes
  (`RepositoryHostingWiringTests`, `SignInEntryTests`, `WelcomeCloneFromGitHubTests`,
  `PaletteCloneFromGitHubTests`, `PullRequestOperationsTests`, `CreatePullRequestOperationTests`):
  in this assembly their `[AvaloniaFact]`s run on a headless UI thread of their own beside the
  walk-through's rather than on its thread. Every fake there is on port 0 or in process (D265).
- **The gits** (phase 3's rule, carried into phases 4 and 5): the reports quote git's own texts and
  files as Git 2.36 writes them, and the checks assert them word for word only on that git
  (`Phase4Expectations.IsReportGitAsync()`); on any other git (CI's 2.43 to 2.56) they assert the
  classifying parts, and the `rebase-merge/` files are compared after 2.55's additions are taken
  out (the subject written as a comment in `done` and `git-rebase-todo`, the `# Conflicts:` block
  at the end of `message`). Nothing is skipped: the version decides what is asserted. The
  Git-layer tests follow the same rule (`PseudoRefTests` asserts on 2.36 that git refuses
  `--ref-format`). Phase 5's facts were derived with Git 2.36 and replayed with the Git 2.55 inside
  Visual Studio on the development machine (put its `mingw64/bin` first on `PATH`); the
  differences the code handles are under "Git versions" in "Git layer". With that Git 2.55
  first on `PATH` the tests against `AuthRemoteServer` fail: it is a trimmed distribution without
  `git http-backend`, which the server runs, and since phase 6 the server says so when it starts
  (plan risk 28); every CI job passes them, as `ubuntu-latest`'s Git 2.55 did until that job moved
  to 2.56.0 on 2026-10-29 (run 40848974456). Phase 6 checked Git 2.56 behaviours locally with
  MinGit 2.56.0.windows.2 first on `PATH`. Since 2026-10-28 the development machine itself runs
  Git 2.56 (D248), so no machine runs 2.36: the word-for-word
  assertions that `IsReportGitAsync()` keeps for 2.36 run nowhere, and the classifying parts run on
  every git; a design's git facts are taken on 2.56, with 2.43 and 2.55 from CI where versions
  differ (D248; since 2026-10-29 no CI job runs 2.55, so only the containers' 2.43
  differs).
- xunit runs the tests of one class (or of one `[Collection]`) one after another and different
  ones side by side, so big test classes are split by subject: `ScenarioTests` and
  `Phase3ScenarioTests`, the pull parity classes by group of settings, and
  `RepositoryOperations*Tests` (helpers in `RepositoryOperationsTestSupport`), whose twelve
  classes run in four collections, one per WP7 sub-package; phase 4's are split the same way (in
  Git.Tests `ContinueSkipTests` and `ContinueSkipRebaseTests`, `InteractiveRebaseTests`,
  `InteractiveRebaseGuardTests`, `InteractiveRebaseRefusalTests`, `InteractiveRebaseListParityTests`,
  the three pull-rebase parity classes; in App.Tests `RepositoryOperationsRebaseTests`,
  `…SequenceTests`, `…PullRebaseTests`, `…ConflictsTests`, `…BackupCleanupTests`, the resolver's
  and the list's view-model, view and control tests); phase 5's likewise (the undo tests in
  Git.Tests by subject: `UndoEffectTests`, `UndoStepTests`, `UndoStepBranchTests`,
  `UndoStepFileTests`, `UndoStepRefusalTests`, `UndoStepRemoteTests`; in App.Tests
  `UndoRecordingTests`, `UndoRestoreTests`, `UndoStepTests`, `UndoToolbarTests` and a class per
  page, panel and driver part). The default run is bound by the processors (git's processes and
  the virus scanner): more git-heavy tests side by side only make every git call slower, and the
  waits of the headless tests (30 seconds by default since phase 5, 60 since phase 6's gate, D237)
  closer to their limit. After
  phase 5 the default run is seven test projects and 3,233 tests, and takes about 45 minutes on the
  development laptop (43 min 22 s at phase 5's gate; 21 min 52 s at phase 4's, with five projects
  and 2,411 tests): two more walk-through projects, and the undo history's reads in every
  operation of every check. CI's Windows Test step took 25 min 15 s on the gate's commit (D184).
  Phase 6 adds two more walk-through projects, so nine test projects run side by side; after its
  harness, rested, 3,299 tests took 43 min 33 s (2026-10-19), before its walk-through parts and
  most of its App tests. Phase 6's view tests stayed in `VisualCommit.App.Tests` (its 449 headless
  tests took 40 % of its 21 min 16 s alone), by subject. At phase 6's gate the default run was nine
  projects and 5,037 tests in 56 min 25 s, rested (CLAUDE.md gives each project's end), with a git
  call 10 to 15 times as long as alone (D237), and the real-window pass of phases 0 to 6 took 27 min
  39 s (67 tests). Phase 7a adds two projects, `VisualCommit.VisualTests.Phase7a` and
  `VisualCommit.Hosting.Tests`, so eleven run side by side (D334). The lead's rested whole run on
  `7015753` (2026-10-30, the review's last code commit before the gate) passed 5,891 of 5,895 tests,
  4 skipped (the keychains' real stores, D270), in 1 h 12 min 6 s. Since D343, at phase 7a's gate,
  the laptop's whole run through `scripts/heavy-run.sh` runs at most four projects at once: the
  gate's counted run (`a9d02c1`'s code) passed 5,948 of 5,952 in 1 h 12 min 18 s, and the
  real-window pass of phases 0 to 7a 72 tests in 31 min 6 s (`a2c312a`).
- **Wait limits** (D341, phase 7a). D237's 60 s applies to every wait of the default run whose
  condition holds only once git, a refresh the watcher starts, a scenario's git or work that slows
  with the machine's load has finished, also where a call site, a helper's default, a field or a
  polling loop had set a shorter limit: such a wait takes `ShellDriver.DefaultWaitTimeout` or 60 s.
  A negative wait, a limit under test, an assertion of elapsed time and a fixed hold keep theirs; a
  test's own limit stays the watchdog's 180 s (D264); and every wait still ends as soon as its
  condition holds. A sweep gave 106 such waits the 60 s in 37 test files (`745ce9a`; design 19 item
  8), after phase 3's check 14b had given a checkout with the user's changes 30 s, a step of 2.7 s
  alone and 25.8 to 28.9 s on one busy core.
- **Heavy runs** (D245). Whole-project runs and the run at the root are the lead's, one at a time on
  the laptop, through `scripts/heavy-run.sh`, which runs `dotnet test` with the arguments it is
  given at the repo root under a lock: the folder `heavy-run.lock` in the tests' temporary root
  (`VisualCommit.Tests` under .NET's `Path.GetTempPath`), made by `mkdir`, whose file `holder` names
  the script's process, its command line and, once started, the `dotnet test` process. A second run
  says once who holds the lock and looks again every 10 seconds; it takes over a lock whose holder
  and test process both no longer run (`tasklist` on Windows, `kill -0` elsewhere), or a lock folder
  without a holder file once that folder is more than a minute old, and places its own holder file
  with `ln`, which never replaces one. TERM, INT and HUP stop the run's `dotnet test` and every
  process it started (`taskkill /T /F` on Windows, the process group elsewhere) and free the lock.
  The exit code is `dotnet test`'s, 75 when `VISUALCOMMIT_HEAVY_RUN_WAIT_SECONDS` ran out, 1 when
  the script itself could not run. Builders and fixers run the classes they touch with plain `dotnet
  test`; CI does not use the script, which has run only on Windows.
- **The lagged list** (R8, D262; design 0.4, 16 item 1). At each wave's merge the lead runs, through
  `scripts/heavy-run.sh` with `VISUALCOMMIT_WATCHER_LAG_MS=1500` (see "Watching and refreshing"):
  phases 0 to 2's `Phase2Checks`; App.Tests' `StageListRefreshTests`,
  `RepositoryOperationsProtectTests`, `CompareViewTests`, `SearchBarTests`, `UndoStepTests`,
  `RepositoryOperationsFetchTests`, `GitCallBudgetTests` and `GitRepositoryProviderTests` (the lag's
  Info line); phase 6's part S, K10, K13, W1 and W8; the whole 7a walk-through; and D334's six
  Hosting.Tests classes; each command with filters of one kind, since xunit v3 ANDs filters of
  different kinds. A failure under the lag is the fault class it imitates, macOS's late events, and
  gets a fix, never a looser test. `RepositoryWatcherTests` is not on it: its "nothing within the
  settle time" checks prove less under a lag (D282). It first ran at WP1's merge (`a4a388e`) and
  passed in all five commands on `7015753`.
- **Timing tests under load** (phase 6): a test that compares how the work grows measures the
  thread's own work (`ThreadWork`), not a stopwatch, which the other projects' load stretches, and
  compares ratios of two runs on one thread, each the best of three, interleaved.
  `PaletteGrowthTests` (the matcher over 300 against 3,000 entries, under 25 times; the build and a
  one-letter query against 2,500 and 10,000 tags, under the items' growth to the power 1.5) and
  `DiffTextViewGrowthTests` (the diff view's build and drawing of 6,000 against 60,000 lines, under
  25 times; moved out of `DiffTextViewTests`) run in `GrowthCollection`, a collection with
  parallelization off, which xunit runs after the parallel tests, so no other test of the process
  runs beside them. Each measure runs inside a region without garbage collection
  (`GrowthMeasures.WithoutCollection(budget, work)`: a full collection, then
  `GC.TryStartNoGCRegion(budget)`; the test fails when a collection ran during the measure), since
  the collector's work grows faster than the items and follows the processor's caches (with
  collections in the measure, 1,000 against 10,000 tags grew 29 times on CI's Windows runner in run
  39936260636, 7 to 9 times on the development laptop). The budgets are 64 MB for the matcher,
  200 MB for the build, and for the diff view twice what a warm-up measure allocated, at least
  64 MB. The stopwatch's times are reported and held only to a gross ceiling: 1 s for the matcher,
  20 s for the build and for the diff view.
- `GitIsolation` also sets `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` to 2026-01-02 09:00 UTC for
  the app under test, and `RealApp.Launch` hands it the same (D72): a commit the app makes gets
  the same id in both passes and on every machine. Since phase 5 (D173) both harnesses also fix
  the app's two clocks at that moment, `ShellDriver.Start` through `AppSession.Start`'s
  `recordClock` and `backupClock`, `RealApp.Launch` through `VISUALCOMMIT_RECORD_CLOCK=frozen` and
  `VISUALCOMMIT_BACKUP_CLOCK=fixed`: the activity log's times read 09:00:00 and its durations 0 ms,
  the backups the checks make get the same names in every run and both passes, one second apart
  and in order, and a scenario's backups are aged against a fixed day. Tests that age backups by
  the clock they are given (`RepositoryOperationsBackupCleanupTests`) follow it.
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
  `Phase3Expectations*.cs`, `Phase4Expectations*.cs`, `Phase5Expectations*.cs`, since phase 6
  `..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs` (both phase 6 projects) and, since
  phase 7a, `..\VisualCommit.VisualTests.Phase7a*\Phase7aExpectations*.cs`, so both harnesses use
  the same constants, texts and `PrepareAsync` calls. Its tests never
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
- Phase 5's pass: `Phase5RealWindowPass.cs` (the run, `Phase5Run`, launching on a scenario and
  again on the same data folder for a restart, waiting for an operation's log line and for the
  backups' clean-up, `Compare`, what the parts share) and `.A.cs` (A1 to A4), `.B.cs` (B5 to B9),
  `.F.cs` (F10, F12, F14), `.U.cs` (U16), `.P.cs` (P24, P25): 16 test methods, each one launch
  but A3's and F14's, which restart the app for real and so launch it twice; 48 captures for the
  15 checks the report marks "RW", all at 1100×700 in the dark theme (the 1920×1080 steps do not
  fit the development machine's screen and are scripted only). `RealApp.Phase5.cs` finds the help
  texts, the keys, the activity log's rows by automation id and name and its output through the
  Value pattern, the search bar, the pages, the comparison and the left panel's toggles;
  `RealApp.Launch` also sets `VISUALCOMMIT_RECORD_CLOCK=frozen` and
  `VISUALCOMMIT_BACKUP_CLOCK=fixed`. The project compiles both phase 5 projects'
  `Phase5Expectations*.cs`.
- Phase 6's pass: `Phase6RealWindowPass.cs` (the run `Phase6Run`, one folder and one `run.txt`; the
  methods' order `Phase6Order`; launching on a scenario or on the workspace "Web", again on the same
  data folder for a restart; waiting for a dashboard's read pass; `Compare`; what the parts share)
  and `.W.cs` (W1 with the native folder dialog on the copy's root, W2, W4, W9 with its real
  restart), `.S.cs` (S14, S15), `.K.cs` (K10) and `.L.cs` (L34, a launch with Theme explicitly
  Light): 8 test methods, 9 launches and 26 captures, all at 1100×700; part G has no real-window
  step. The pass never changes the operating system's theme (D224): S15 starts with System, reads
  the app mode and high contrast read-only (`NativeMethods.SystemParametersInfo`) and compares with
  the scripted twin of that variant. `RealApp.Launch` gives every launch its own git environment
  (`GitIsolation.SessionGitEnvironment` on its data folder) and `VISUALCOMMIT_TOOL_CATALOGUES=fixed`;
  `RealApp.Phase6.cs` (the files a launch starts from, `WriteWorkspaces`, the tab strip, the
  read-only reads of the desktop), `.Phase6.Dashboard.cs`, `.Phase6.Settings.cs`, `.Phase6.Palette.cs`
  (a real Ctrl+Shift+P), and `ChooseFileInDialog` in `RealApp.cs` (Windows' Open dialog) find and
  drive them. The platform dialogs' helpers wait for the dialog's controls, not only its window, and
  `ChooseFileInDialog` confirms with Enter in the file name box: the dialog opens again where the
  last run chose a file (the system folder), and a search for its Open button walks the whole file
  list first (the gate's rounds 2 and 3). The project compiles
  `..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs`.
- Phase 7a's pass (design 13, 18 item 1; D319 to D322): `Phase7aRealWindowPass.cs` (the run
  `Phase7aRun`, one folder and one `run.txt`; the order `Phase7aOrder`, A, B, N, G, P, so that P's
  browser step is the pass's last input; `LaunchServed`; the waits on the fake's records and on
  D322's two log lines; `Compare`; the menus; `accounts.json`; `AssertCleanEnd`) and `.A.cs`,
  `.B.cs`, `.N.cs`, `.G.cs`, `.P.cs`: one test method and one launch per part, 9 captures (A 3, B 1,
  N 2, G 1, P 2), all at 1100×700 in the dark theme. Each launch serves a fresh copy of "hosting"
  with the fake on 47395 in the test process (run.txt notes a fallback port), so the 7a walk-through
  never runs beside it; calls `PartG.PrepareAsync` or `PartN.PrepareAsync` where its twin does; and
  starts the app with `RealApp.HostingEnvironment(port)` applied last (`VISUALCOMMIT_GITHUB_URL=
  http://127.0.0.1:<port>/`, `VISUALCOMMIT_KEYCHAIN=memory`), so the real process reaches the fake
  through its own handler. Every launch signs in by typing `OctoToken` into the real "Add GitHub
  account" dialog (D320: B through the palette's "Clone from GitHub…", the others through Settings,
  Accounts), whose box's Value pattern stays empty while masked (`RealApp.SecretBoxValue`); nothing
  is seeded and nothing restarts, since the store is memory. B clones `octo/hello` in the real
  process through `visualcommit-askpass.exe` beside the app (`RealApp.HelperPath`, whose path holds
  the laptop's space, run by Git for Windows' `sh`): no credentials dialog, the broker's "answered
  from the account" line, git requests given the user name `x-access-token` only, then the plain URL
  and no `credential` key in the clone; run.txt names the helper's path. G waits for the avatars'
  requests without a token and reads the details panel's image on the screen (the author's text 22
  right of the date, octo's colour beside the image's white centre), since `DetailsAuthorAvatar`, a
  plain `Ellipse`, is no control element in UI Automation. P clicks "Open in browser" once
  (`OpensTheBrowser = true`, D321): the proof is the fake's `page:octo/hello` request for
  `octo/hello/pull/1` without a token within 60 s; a real click on the status bar's empty middle
  then takes the activation back, and run.txt says the tab is left open for the owner to close
  before the next run. Before each capture the window's element names are checked for a token; text
  from the app, git, the log or the fake reaches a failure's message only through a token check
  (`Checked`, `Recorded`) and log lines only as counts; no "Copy …", "Open file", "Open in editor",
  "Show in folder" or the status bar's "Open in browser" is clicked. `AssertCleanEnd`: the app exits
  0, `TokenTripwire` finds none of the scenario's three tokens in the data folder, `hello/`, `tool/`
  and the given folders, the fake's `Errors` is empty, no API request was answered 400 or 415 and no
  avatar request carried a token. `RealApp.Phase7a.cs` (the hosting environment, `HelperPath`, the
  Accounts section and the sign-in dialog, the browser's and the left panel's rows) and
  `RealApp.Phase6.Settings.cs` (Accounts in the section list) drive it. Every launch's git
  environment has, since phase 7a, its own empty `HOME` (`GitIsolation.SessionGitEnvironment`,
  D340). Its first runs were D246's two of the 7a class alone (`--filter-class
  "*Phase7aRealWindowPass"`) at phase 7a's gate, with the owner present: the first failed only
  `Check_N`'s lookup of the title box, which now looks inside the form, and the second passed 5 of 5
  in 2 min 55 s; the whole pass of phases 0 to 7a passed 72 of 72 in 31 min 6 s on `a2c312a`, whose
  `RealApp.TitleOf` reads a closing dialog's title without throwing (ruling 30, CLAUDE.md).
- The whole pass, phases 0 to 7a, runs 72 tests: 68 test methods of the pass (phase 0 1, phase 1 5,
  phase 2 8, phase 3 15, phase 4 10, phase 5 16, phase 6 8, phase 7a 5) and the 4 of
  `Phase3ExpectationsTests`,
  which the link pattern `Phase3Expectations*.cs` compiles in with phase 3's expectations. Phases 0
  to 5 took about 25 minutes at phase 5's gate, phases 0 to 6 27 min 39 s at phase 6's. Phase 5 changed two earlier passes: phase 2's rests the pointer at the
  neutral point at 1100×700 (it rested at (130, 687), on the status bar's operation text, which
  since phase 5 can carry a tooltip: the restore bar's Restore is the discard's Undo, and 17c
  showed "Undid discard of Calculator.cs"; its rests at 1400×900 stay), and phase 3's check 11
  expects two selected commits' comparison (D162).
- `RealApp.PressKeys` sends a navigation key held in a chord (an arrow, Home, End, Page Up or
  Down, Insert, Delete) as its extended scan code, as the keyboard's own keys do: FlaUI sends a
  virtual key without the extended flag, which Windows takes for the number pad's key, and Shift
  with a number-pad arrow while Num Lock is on cancels the Shift (Shift+Down moved the caret
  without selecting, found by phase 4's gate).
- **The gate's comparison of pictures** (phase 5, design 15.7): `python
  scripts/compare-pictures.py <reference folder> <new folder> <output folder>` takes two folders
  laid out as `artifacts/visual` (`phase-N/scripted/`, `phase-N/real-window/`) and writes, for
  every picture with a twin, a difference picture (the new one dimmed, every changed pixel red)
  and the boxes of changed pixels with the window's regions they lie in (from `WindowLayout`'s
  sizes), and a `summary.md`; identical files are only counted. It needs Pillow and NumPy and is no
  part of the build or the tests. Phase 5's gate compared phases 0 to 4's pictures with references
  made on `master` and kept outside the repository, so that the inspectors judged differences
  rather than re-reading every picture; the pictures it inspected are listed in
  `docs/test-reports/phase-5-pictures.sha256` (1,080).

### The fake of GitHub (D250, D265 to D267)

No test of the default run or of CI reaches GitHub (D250). Every one of them that talks to GitHub
talks to `FakeGitHub` (`tests/VisualCommit.Testing/Hosting/`, partial over `FakeGitHub.cs`,
`.Router.cs`, `.Json.cs`, `.Git.cs` and `.Model.cs`): git, the REST API under `api/v3/`, avatars and
web pages on one host. It runs in one of two ways:

| Start | What answers | Endpoints | Used by |
|---|---|---|---|
| `FakeGitHub.StartAsync(root, port, fallBackToFreePort)` | `AuthRemoteServer` in host mode on 127.0.0.1: git through `git http-backend`, the API, avatars and pages through its route hook | `GitHubEndpoints.Local(port)`: `http://127.0.0.1:<port>/` and its `api/v3/` | the 7a walk-through and its real-window twin on 47395 (D265, below), every other test on port 0 |
| `FakeGitHub.InProcess(root)` | `CreateHandler()`, a `FakeGitHubHandler` that serves a request by its path whatever its host and records it as over TCP; git's paths get 404; a dropped answer is an `HttpRequestException` | `Local(1)` (`InProcessPort`), never listened on | the client's and provider's tests, `HostingSessionSetup` |

It is stricter than GitHub (D266): every API request needs a token and the pinned API version, so a
forgotten header or a request before the sign-in (D257) fails in the default run. Its state (users,
tokens, repositories, pull requests, linked e-mails, holds, faults, each login's request count) is
kept under one lock; git runs outside it through `GitRunner` with `TempRepo.IsolatedEnvironment` on
a home folder of its own, a repository's ref sync and create one at a time (a semaphore per
repository). Disposing it releases every hold, then stops the server; nothing under `root` is
deleted.

**What a test adds** (`FakeGitHub.Model.cs`). `AddUser(login, id, name)` gives a `FakeUser`
(settable `IsOrganization`; `AvatarBytes`, by default a 64×64 PNG of the user's colour, 1001
`#2F81F7`, 1002 `#8250DF`, 1003 `#1F883D`, others `#BF3989`, with a white square of 24 in its
centre). `AddToken(login, kind, scopes, expires)` makes `github_pat_FAKE-<login>-<n>`, or for a
classic token `ghp_FAKE-<login>-<n>` with `repo` and `workflow` unless other scopes are given, `n`
counting from 1 across the fake's tokens; `Revoke(token)` refuses the next request with it, git's
included. `AddRepositoryAsync(owner, name, sourceRepository, isPrivate, forkOf, description)` makes
`<root>/<owner>/<name>.git` (one `git clone --bare --no-hardlinks` of a source, an existing bare
repository in place, or `git init --bare --initial-branch main`), with an id from 3001, its default
branch read from `HEAD` and its owner a pusher when a user, and serves it at once (`FakeRepository`:
settable `IsArchived`, `UpdatedAt`, `AddPusher`, `RemovePusher`). `AddListedRepositories(owner,
count)` adds API-only records `listed-0001` and up, without a folder, for the long lists.
`AddPullRequest(…)` keeps a pull request in memory (number given or the repository's highest plus
one; a head repository the fake does not know is a deleted fork); `LinkEmail(email, login)` lets the
commits API show the user for that address. `MaxPerPage` (100) caps a page. Tests read the fake's
state through `Repository(fullName)`, which any name of a renamed repository finds, and
`PullRequests(fullName)`.

**Ref sync** (D267). Before a list of pull requests, a create and every git ref listing of a
repository (the server's `BeforeRefsAsync` hook, on `info/refs` and protocol v2's `ls-refs`), the
fake brings each open pull request's `refs/pull/<n>/head` in step with its head: one `git
for-each-ref` in the repository and one per fork with open pull requests, then one `git update-ref
--stdin` for the heads in the repository itself and one fetch per fork of its branches into the
base's `refs/pull/<n>/head`, the objects copied as GitHub's shared network does. A deleted fork's
head is the ref it already has; an open pull request without one is an `Errors` line. A test can run
it itself, without a request: `SyncRefsAsync(fullName)`.

**Routes** (`FakeGitHub.Router.cs`). The API's base is `<web>api/v3/`; owner and name ignore case;
`repositories/<id>/…` is read as the repository's current name. A route's key, lower-case, names it
in `HoldNext`, `FailNext` and the records:

| Key | Request | Answer |
|---|---|---|
| `user` | `GET api/v3/user` | the token's user, with `name` |
| `user/repos` | `GET api/v3/user/repos` | paged: what the login owns or may push to, and the listed records; `sort` (`full_name` by default; `updated`, `pushed`, `created` by `UpdatedAt`), `direction`, `visibility`, `affiliation`; `type` with either is 422 |
| `repo:o/r` | `GET api/v3/repos/o/r` | the repository, with `parent` and `source` for a fork |
| `pulls:o/r` | `GET …/pulls` | paged, number descending; `state` (`open` by default), `head` as `owner:ref`, `base` |
| `create:o/r` | `POST …/pulls` | 201, author the token's login, `refs/pull/<n>/head` written; 422 "Validation Failed" for a missing title, a base or head that is no branch, an open pull request with that head and base ("A pull request already exists for …") or nothing ahead ("No commits between …"); 403 "Resource not accessible by personal access token" with `X-Accepted-GitHub-Permissions: pull_requests=write` for a fine-grained token whose login cannot push to the base |
| `update:o/r` | `PATCH …/pulls/<n>` | 200; `{"state":"closed"}` closes |
| `commits:o/r` | `GET …/commits` | one `git log` from `sha` (the default branch when absent), filtered by `author` (an e-mail, or a login through `LinkEmail`), paged; a revision git cannot resolve 404 "Not Found", or beside `author` 500 with an empty body (both as github.com answers, D342); 409 "Git Repository is empty." |
| `avatar:<id>` | `GET avatars/u/<id>` (any query) | the user's `AvatarBytes`, without auth |
| `page:o/r` | `GET o/r`, `o/r/pulls`, `o/r/pull/<n>` (and `page:settings` for `settings/…`) | `text/html`, `<!doctype html><title>…</title>` |
| `git:o/r` | git, over TCP only | `AuthRemoteServer`'s host mode (below) |

API requests are checked in order: no `User-Agent`, 403; `X-GitHub-Api-Version` other than
`GitHubApi.Version`, 400 "Bad Request" with the explanation in `errors` as one string: GitHub's own
words for a version GitHub does not support, the fake's own for a missing header or `2022-11-28`,
which GitHub accepts and only D266 refuses; `Accept` without
`application/vnd.github+json`, 415; a pending fault; no `Authorization` or a `Basic` one, 401
"Requires authentication"; a `Bearer` or `token` with an unknown or revoked token, 401 "Bad
credentials"; a private repository the login cannot see (neither owner nor pusher, or a classic
token without `repo`), 404 "Not Found". Errors are GitHub's shape (`message`, `documentation_url`,
`status`, and a 422's `errors`), the bodies snake_case with UTC times. Every answer of the API and
the pages, and an avatar's 404, has `X-GitHub-Request-Id: FAKE-<4 digits>` (an avatar's 200 has only
`Content-Type: image/png` and `Cache-Control: max-age=300`); a JSON success a weak `ETag` (the first
32 hex digits of the body's SHA-256), and a matching `If-None-Match` gets 304 without a body and is
not counted; a signed-in request the `X-RateLimit-*` headers (5,000 per login, reset an hour after
`AppClocks.HarnessTime`); a page `Link` (`next`, `last`, and `prev`, `first` past page 1); a classic
token's answer `X-OAuth-Scopes`; a token with an expiry `GitHub-Authentication-Token-Expiration`,
except on a 304. Errors carry the generic documentation URL, except where GitHub's own was recorded:
the `type` 422, the create's 422s (whose `errors` items carry `resource` first) and the commits 404.
Avatars and pages are answered before any check; an avatar request that carries `Authorization` is
answered and recorded `TokenPresent`, which the checks assert never happens. The texts of the
version, `type`, unknown-SHA and duplicate-create answers are GitHub's own, as the live command
recorded them on 2026-10-30 (D342), and its runs at phase 7a's gate found every probe the same; the empty repository's 409, `ApiVersionGone`'s 410 and the
`ServerError` fault's body are still from memory (its sandbox has a commit, and GitHub's one 500 had
no body). A fine-grained token reaches every repository its login owns or may push to: GitHub's "Only
select repositories" is not modelled.

**Holds, faults and renames.** `HoldNext(route)` returns a `FakeGitHubHold`: the next request of
that route waits before anything is decided about it until the hold is disposed, at most 120 s
(then it goes on with an `Errors` line). `Arrived` completes when a request waits there; `Abandoned`
(design 19 item 5) when a request held in process is given up by its sender, so a test proves a
cancelled download or a sign-out's dropped request without a fixed sleep; over a served fake it sees
only the server's stop. `git:o/r` delegates to `AuthRemoteServer.HoldNextRequest`, on a server only.
`FailNext(route, fault)` answers that route's next request once with a `FakeGitHubFault`, after the
header checks and before auth: `BadCredentials`, `PrimaryRateLimit`, `SecondaryRateLimit`
(`Retry-After: 60`), `SsoRequired`, `SsoPartialResults` (the list as normal with `X-GitHub-SSO:
partial-results`), `Forbidden`, `NotFound`, `ApiVersionGone` (410), `ServerError`, `BadGateway`
(502, HTML), `Malformed` (200 with a cut body), `CreatedThenDropped` (the create made in full, the
connection then closed without an answer), `Moved` and `MovedTo(uri)` (301). Git, avatar and page
routes refuse a fault with `NotSupportedException`: they are answered before faults are taken
(design 16 item 2). `Rename(fullName, newName)` (design 19 item 2) renames within the same owner:
the API serves and answers with the new name (`FakeRepository.CurrentFullName`), and a request by an
older name gets 301, or 307 for a POST or a PATCH, to `api/v3/repositories/<id>/…` with its query
kept; git keeps the old folder and URL, as GitHub keeps git's old URL working. A transfer to another
owner is not modelled.

**Records.** `Requests` holds every API, avatar and page request in the order its answer was
decided, as `FakeGitHubRequest(Method, Host, Path, Query, Route, TokenPresent, Login, Conditional,
Status, Body)`, the body for a POST or a PATCH only and never a token; `GitRequests` the server's
git requests (`AuthRemoteRequest`, with the user name git gave, `GivenUserName`); `Errors` the
fake's and the server's, which every check asserts empty. All three are snapshots, safe from any
thread.

**`AuthRemoteServer`'s host mode** (D266, design 2.2). `StartForHostAsync(folder,
AuthRemoteHostOptions, port, fallBackToFreePort)` serves a folder of bare repositories in two
levels, each registered by `Serve("owner/name")`, also while the server runs (it runs no git; each
segment of letters, digits, `.`, `-` and `_`); `/o/r.git/…` and `/o/r/…` both name
`<folder>/o/r.git` (`http-backend` tries `.git` itself), with `GIT_PROJECT_ROOT` the folder. A path
the routes claim (`IAuthRemoteRoutes.Claims`, decided from the request line) skips the repository's
gate and auth and gets the routes' answer with `Connection: close`. A git request signs in by Basic
auth with any non-empty user name and a password `LoginOfToken` knows, as that login
(`REMOTE_USER`), recording the user name given, also for a token it does not know, never the
password; a header it cannot read and an unknown or revoked token get 401 with `Basic
realm="GitHub"` on every path it does not claim; with no header, an upload-pack of a repository that
`IsPublic` allows is served as `anonymous`, as GitHub does; a receive-pack needs `MayPush`, else 403
"Permission to octo/hello.git denied to ada.". `BeforeRefsAsync` runs before git lists a
repository's refs. The other starts behave as before.

**Ports** (D265, amending D97 as D150, D229 and D238 did). `AuthRemoteServer.Phase7aVisualChecksPort`
is 47395, whose URLs the 7a pictures show; only the 7a walk-through and its real-window twin bind it,
falling back to a free port when it is taken (`PortWasTaken`), and 47396 is reserved for a second
7a walk-through project. Every other test, `VisualCommit.Hosting.Tests` included, uses port 0 or the
in-process handler. `AuthRemoteServerTests` pins 47391 to 47395.

**The harness's seams** (design 1.9, 2.5). Every test process and the app the real-window pass
starts begin with GitHub off and tokens in memory (`GitIsolation.MachineIndependence`:
`VISUALCOMMIT_GITHUB_URL=none`, `VISUALCOMMIT_KEYCHAIN=memory`, no token variable).
`ShellDriver.Start` and `StartWithTabs` take, last, `gitHub:` (a fake on a port), `tokens:` (a
`FakeTokenStore`) and `hosting:` (options as given) and pass the options and the store to
`AppSession.Start`:

| Given | The session's hosting options |
|---|---|
| neither | `HostingOptions.Off`, its handler a `LoopbackOnlyHandler` over a `SocketsHttpHandler` without proxy or redirects |
| `gitHub:` | the fake's `Endpoints`, each handler a `LoopbackOnlyHandler` over `HostingOptions.DefaultHandler(endpoints)` |
| `hosting:` | as given, with no loopback filter: an in-process fake's `new HostingOptions(fake.Endpoints, fake.CreateHandler)`, or the live command's github.com |
| both, or an in-process fake as `gitHub:` | `ArgumentException` (design 16 item 3) |

`LoopbackOnlyHandler` lets through only a request whose host parses as a loopback IP address
(`localhost` does not) and records any other in the session's `RefusedRequests`, failing it with
`HttpRequestException` "The test harness lets only loopback requests through."; every check ends
with `RefusedRequests` empty. A start without `tokens:` gets a new `FakeTokenStore`; one store handed
to two starts is the scripted restart. Before `Start` returns it runs the UI thread's jobs every
15 ms until `AppSession.HostingStarted` has completed (the hosting session's read of its account and
token, whose posts need the UI thread), at most `ShellDriver.DefaultWaitTimeout`, rethrowing what the
start threw. `ShellDriver.SignInAsync(token)` signs in through the session with no dialog and
throws, with the failure's kind and message and never the token, unless the account is signed in.

`FakeTokenStore` (`tests/VisualCommit.Testing/Headless/`) is the harness's keychain: tokens in
memory that, unlike the app's memory store, count as kept across restarts (`Name` "fake",
`KeepsTokensAcrossRestarts` true). A check sets `Available` false (the probe then says "The fake
keychain is set unavailable." and every save fails), makes one save or one delete fail
(`FailNextSave`, `FailNextDelete`), holds the next call (`HoldNext()`, whose `Arrived` completes when
a call waits), asks whether a key is kept (`Holds`) or takes a token away behind the app's back
(`Remove`). `Calls` names each call with its key (`save github:127.0.0.1:47395:octo`), never a token.
Safe from any thread. No test of the default run reaches a real keychain; CI's keychain steps test
the platform stores (see "CI" and "Tokens and the keychains").

`TokenTripwire.AssertAbsent(token, folders, calls)` searches every file under the folders, `.git`
included, for the token as UTF-8, UTF-16LE, URL-escaped, base64 of `x-access-token:<token>` (git's
Basic header) and base64 of the token (the macOS store's form), and each git call's command,
recorded standard input, output and error for the text forms. A find names the file or the call and
the form, never the token: a call whose command holds it is named by its words before the first
that does, then "…" and its sequence number (design 18 item 2). A file is read sharing read, write
and delete; one still unreadable after five tries 100 ms apart fails as unreadable, since it may hold
the token, and one deleted first passes. The checks run it over the data folder and the clones at
their end, `HostingSessionSetup`'s disposal over its data folder, the real-window pass and the live
command over theirs.

**The scenario "hosting"** (`Scenarios.Phase7a.cs`, design 2.6). `Scenarios.HostingAsync()` copies,
with one git process, a template built once per test process: `hello/` (the returned repository),
`tool/` beside it and the fake's folder `github/`. Commits a minute apart from 2026-01-01 12:00 UTC
by Octo Cat `octo@example.com`, Ada Lovelace `ada@example.com` and `TempRepo`'s Test Author, whom
GitHub does not know (`TempRepo.CommitFileAsAsync` commits as a given author):

- `github/octo/hello.git` (private): `main` with three commits; one commit each on `feature/login`,
  `docs/readme`, `feature/a-very-long-branch-name-that-the-graph-must-cut` and `topic/no-pr`;
  `refs/pull/1` to `5/head` written. `github/ada/hello.git`, ada's fork, adds `fix-typo`, which is
  octo/hello's `refs/pull/5/head`.
- `github/acme/tool.git` (the organisation acme): `main`; its fork `github/octo/tool.git` adds
  `fix-typo` (acme's `refs/pull/7/head`); the deleted fork `gone/tool`'s `idea` is acme's
  `refs/pull/8/head`, with no folder.
- `github/octo/notes.git` (private) and `github/octo/website.git`: empty.
- `hello/`: `main` and `feature/login` tracking `origin`, local-only `local-work`, on `main`, clean;
  `tool/`: a clone of octo/tool with `upstream` acme/tool and `fix-typo` tracking `origin`. The
  remotes' URLs are `http://127.0.0.1:47395/<owner>/<name>.git`, written last.

`Scenarios.ServeHostingAsync(hello, port = 47395, inProcess, ct, ownsCopy)` gives a `ServedHosting`
(`GitHub`, `Hello`, `Tool`, `OctoToken`, `OctoClassicToken`, `AdaToken`): the fake on the copy's
`github/`, users octo 1001 "Octo Cat", acme 1002 (an organisation) "Acme" and ada 1003 "Ada
Lovelace", the tokens `github_pat_FAKE-octo-1`, `ghp_FAKE-octo-2` and `github_pat_FAKE-ada-3`, the
six repositories with their `UpdatedAt` (acme/tool's pusher ada; octo/tool described "Small
command-line tools."), octo/hello's pull requests #1 to #5 (#2 a draft, #4 closed, #5 from ada's
fork) and acme/tool's #7 (from octo/tool) and #8 (from the deleted fork), and octo's and ada's
e-mails linked. Only on a port other than 47395 does it rewrite the clones' remotes (`git remote
set-url`, D265). In process the clones keep 47395's URLs, which are not the fake's endpoints, so the
app recognises neither remote and git reaches nothing: a test that needs either serves on port 0
(design 16 item 3). `Phase7aScenarioTests` (Git.Tests) pins the layout, authors, upstreams, URLs,
`refs/pull` and the lists; the fake's own tests are `FakeGitHubTests` (in process: every route,
check, header and fault), `FakeGitHubGitTests` (port 0: a private clone with a token and refused once
revoked, a push refused, an anonymous clone, a pull request's head fetched into `FETCH_HEAD` only, a
pushed head's `refs/pull` moved at the next listing) and `FakeTokenStoreTests`, in
`VisualCommit.Hosting.Tests`; `Phase7aSupportTests` (the 7a project) tries the safety nets, the
tripwires and the seams once.

### The live command (D250, D260, D323 to D328, D335, D337)

Real GitHub is tried by an opt-in command of its own (D250): `tests/VisualCommit.LiveHostingTests`
(`net10.0-windows`, `Exe`; App, Core, Git, Hosting, Testing; `xunit.v3`, `Avalonia.Headless.XUnit`)
runs the whole app in Avalonia's headless mode against github.com with the owner's fine-grained token
for one private sandbox repository on the owner's account (D326: then the clone too proves that the
token reached git). It is in no solution (`VisualCommit.slnx`'s comment says why), so `dotnet build`,
`dotnet test` and CI's test steps never touch it; CI's Windows job builds it and never runs it (D337,
"CI"). It runs only by its own command, `bash scripts/heavy-run.sh --project
tests/VisualCommit.LiveHostingTests -c Release` (CLAUDE.md), under the heavy-run lock, which the
owner is told of because it acts on their account: at a gate once before its pictures, its
corrections to the fake committed before the passes, once on the counted round after the real-window
pass and the pictures' list, and alone on a later commit that changes only this project, which then
keeps the Gate commit where it was (D328, D335). It takes no screenshot and writes nothing under
`artifacts/visual`.

**The variables** (D260). `VISUALCOMMIT_LIVE_GITHUB_TOKEN` (the token) and
`VISUALCOMMIT_LIVE_GITHUB_REPO` (`owner/name`) are Windows user variables that the owner sets once.
The assembly's module initializer runs before `Main`, any test and Avalonia's set-up:
`LiveSettings.CaptureAndRemove()` reads both, trimmed, once and removes them from the process, so
that no git or app process inherits them; then `HeadlessTestApp.IsolateFromTheMachine()` (whose
`MachineIndependence` removes both from every other test process too); and the hang watchdog's
limit becomes 600 s unless the owner set one (`Check_2`'s step limits add up to about 510 s).
`LiveSettings.Require()` gives a `LiveSandbox(Owner, Name, Token)`, whose `ToString()` leaves the
token out, or throws one message naming the variable that is missing or not `owner/name`; every test
calls it first and none is skipped, so without the variables all three fail at once (a skip could pass
for green at a gate). Tests run one at a time (`DisableTestParallelization`), in `GitHubLiveOrder`'s
order.

**`Check_1_the_sandbox_and_its_leftovers`** (`[Fact]`) reads the sandbox through the test's own
client, `GitHubLiveApi` (an `HttpClient` without redirects, 30 s a call, GitHub's headers and the
pinned version): it must answer 200 with `permissions.push`, and a public sandbox, a fork or a token
that may not push fails, naming D326 or design 14.6. It then removes what a stopped run left (D324):
open pull requests whose head is a `visualcommit-live/` branch of the sandbox itself are closed and
the branches under `refs/heads/visualcommit-live/` (compared ordinally, `LiveLeftovers`) deleted;
anything else GitHub's `matching-refs` returns is noted as not touched, and a refused clean-up fails
the test. Last, read-only probes (`LiveProbes.cs`) record GitHub's answers to what the fake imitates
(an unknown API version, `user/repos?type=owner&visibility=all`, an unknown SHA without and beside
`author`, beside a linked address and an unlinked one, a second unknown SHA that is not all f's, a
branch GitHub lacks, and the avatars' lookup `commits?sha=&author=` for the latest commit's author and
for an address with no commit, D339), each with its Content-Type, `X-GitHub-Api-Version-Selected`
and `X-RateLimit-Remaining`; an empty body is noted "(empty)", a 2xx body by its shape only, and no
address is named. Each probe is also answered by an in-process `FakeGitHub` (with a token of its own,
5 s a call) and noted "the fake: same" or the fake's status, message and `errors` where they differ,
so a change of GitHub's or of the fake shows at the next run. The answers the app relies on are
premises that fail the run once noted: the version's 400 names the header in `message` or `errors`;
the lookups answer an array of 1 and of 0; an unknown SHA beside `author` answers 422, a 5xx or 404
(D342).

**`Check_2_sign_in_clone_list_push_and_create`** (`[AvaloniaFact]`) starts the app with
`ShellDriver.StartWithTabs(data, [null], hosting: new HostingOptions(GitHubEndpoints.GitHubCom, …))`
and its default `FakeTokenStore`, so the token stays in memory and no keychain is touched; each
handler the session makes is wrapped in `LiveRequestLog`. In order: the log says "GitHub:
github.com." and no request has left; the sign-in through Settings, Accounts and the "Add GitHub
account" dialog, as a user signs in, is exactly one `GET https://api.github.com/user` answered 200
with `Authorization`, and the login must be the sandbox's owner; the repository browser lists the
sandbox (only counts of the other rows are recorded) and Choose fills
`https://github.com/<owner>/<name>.git`; the clone opens within 120 s with no credentials dialog,
the broker's line "…: Clone: git's credential get for github.com answered from the account." since
the clone's mark (the proof D326 counts on: a netrc entry would clone the private sandbox too,
D340), the plain URL and no `credential` key in the clone's configuration, and no
`credential.helper` at all, so Git Credential Manager never starts; the pull requests are listed
(D322's line) and, when GitHub links an account to the sandbox's latest commit, the avatars' run
ends; the test commits a file on a new branch `visualcommit-live/<UTC time>` as "VisualCommit live
test" `<live-test@visualcommit.invalid>` (D325), and "Create pull request…" on its row pushes it and
creates a draft through the form (when GitHub refuses the draft with a text that names drafts, the
text is recorded and the same form creates a ready pull request, design 18 item 2); the test reads
it back, tries the same create again and records GitHub's answer (422 expected, whose first error
must start "A pull request already exists", D316's premise; a 201 closed in the
clean-up), then in `finally` closes it and deletes the branch, each call on its own, a failure
failing the test after it is recorded. After the app is disposed, `TokenTripwire` searches the data
folder, the clone, the test identity's home and the app's git calls; every recorded request must be
`https`, every one with `Authorization` must have gone to `api.github.com`, and none may precede the
sign-in (D327). A watchdog stop ends the process mid-method: the clean-up does not run, and the next
`Check_1` removes the leftovers.

**`Check_3_the_providers_answers_from_github`** (`[Fact]`, D342) runs the product's own
`GitHubProvider` against `api.github.com` through `LiveRequestLog`: its A1 with
`X-GitHub-Api-Version` rewritten to `2000-01-01` by a handler of the test's must fail as `ApiVersion`
with a message that names the header, and its A6 lookups alone (no branch, so that the commits page
cannot answer the second address first), an unknown SHA beside a made-up address and then the latest
commit beside its author's, must map the second to the sandbox's owner and leave the first out; each
step needs its requests in the log, and the tripwires are `Check_2`'s.

**What keeps the token in** (D323, design 14.5, 18 item 2). `LiveRequestLog`, a `DelegatingHandler`,
records per request the time, method, scheme, host, path without the query, status, whether
`Authorization` was sent, and of the answer only `X-GitHub-Request-Id`,
`X-GitHub-Api-Version-Selected`, whether `X-OAuth-Scopes` came and the token's expiry date; it refuses
before it leaves, as an `HttpRequestException`, a request that is not HTTPS or that carries
`Authorization` to any host but `api.github.com:443`. `LiveRun`, the class fixture, keeps the notes
in order and writes them at its end to `artifacts/live/github/run.txt`, outside `artifacts/visual`
(always, `No note was taken: …` when the tests failed before their first step). Each note, run.txt's
text and a failing step's exception are searched for the token as written, URL-escaped, as base64 of
`x-access-token:<token>` and of the token, and as any 16 of its characters in a row: a note that holds
it is kept as "A note was withheld: it held the token.", a file that holds it is not written, and an
exception that holds it is replaced by one naming the step and the exception's type
(`LiveRun.FailureOf`); each find fails the run. The token is sent only by the dialog's typing, the
test's own API calls and `Check_3`'s provider, all to `https://api.github.com/` alone (`GitHubLiveApi`
refuses the owner's token with any other API base), compared only with `string.Equals` or
`string.Contains`, and never passed to an assertion, a message, a git argument or environment, a
file or the in-process fake. `LiveSafetyTests` (109) prove the search, the request log, the
clean-up's selection, the probes' notes beside the fake, the premises and the version rewriter
offline, with made-up tokens and an in-process handler, by their own command (CLAUDE.md), with no
variable and no network.

Its runs with the token are in the 7a report's "Live command" and design 20: the first (status step
16, 2026-10-30) found GitHub's version refusal and its answer to an unknown SHA beside `author`
unlike the fake's, each hiding a defect of the app's (D342); the second, on the merged corrections,
passed with every probe "the fake: same" (63 s with its build) and stands as the gate's run before
its pictures.

### CI

`.github/workflows/ci.yml` builds the solution in Release and runs `dotnet test` on
`windows-latest`, `macos-latest` and `ubuntu-latest` for every push and pull request that changes
more than docs. On Windows it also builds in Release, but does not run, the two projects the
solution leaves out, in the step "Build the opt-in projects": the real-window pass and, since phase
7a, the live command (D337), so that neither goes stale unnoticed. The step runs in bash, so a
failure of the first build fails it, and whenever the build passed and the run was not cancelled,
also after a failed test or keychain step (design 19 item 4), so a failing test does not hide a
compile break. Each job uploads `artifacts/visual/` as `visual-<os>`, so the scripted screenshots of
all three platforms can be downloaded and looked at. A newer push to the same branch cancels the run
before it (`concurrency`). Time limits: 65 minutes for each of these jobs with 55 for its test step,
and 25 minutes for each container job with 14 for its test step, so that a hang cannot use up the
job's time. D232 raised the first two from D184's 50 and 40 in phase 6 (macOS's test step had passed
32 minutes, D186's threshold, with phase 6's last walk-through parts still to come); the lead raises
them again when the Windows or macOS test step passes 44 minutes, four fifths of 55. D184 had raised
them from D152's 32 and 24: phase 5's two walk-through projects and the undo history's reads took
Windows' test step to the 24-minute limit. On phase 5's gate commit the test step took 25 min 15 s
on Windows, 16 min 13 s on macOS, 3 min 26 s on `ubuntu-latest` and about 4 minutes in each
container. On phase 6's gate commit (run 40059391404) it took 24 min 37 s on Windows, 23 min 33 s on
macOS, 4 min 21 s on `ubuntu-latest`, and 5 min 50 s and 4 min 56 s in the containers; macOS's had
reached 43 min 12 s in run 40019921605 (on `a937ca6`, before the gate's first round), near the 44 at
which the limit is raised. A test project added to the solution is picked up with no change to the
workflow. The build trim (`Directory.Build.targets`, D239, "Solution layout") applies in full on the
Windows job, whose Release build and real-window build copy only win-x64's, `win`'s and `any`'s
native libraries; the macOS, Linux and container jobs lose only the packages' native `.pdb` files
and keep every platform's libraries.

Since phase 7a every job but `ubuntu-latest` also runs the real keychain store's own tests (D259,
D270; see "Tokens and the keychains") in a step of its own after the Test step, with a limit of 5
minutes, which runs whenever the build passed and the run was not cancelled, so that a failed Test
step does not hide its result: on Windows "Keychain (Windows Credential Manager)"
(`VISUALCOMMIT_KEYCHAIN_TESTS=windows`, `--filter-class "*WindowsTokenStoreRealTests"`; were the
runner to refuse the Credential Manager with error 1312, D333 takes the class off the job's list and
the phase's report records it), on macOS "Keychain (macOS keychain)" (`macos`,
`*MacKeychainTokenStoreRealTests`, which makes and deletes a keychain file of its own), and in each
container "Keychain (Secret Service)" (`secret-service`, `*SecretServiceTokenStoreRealTests`) under
`dbus-run-session`, with `gnome-keyring-daemon --unlock --components=secrets` given a throw-away
password and `XDG_DATA_HOME` a temporary folder. The classes use entries named
`VisualCommit-test:<guid>` with dummy secrets, deleted in `finally`. The containers run with
`--cap-add=IPC_LOCK`: Ubuntu's gnome-keyring gives its daemon that file capability when `setcap` is
installed, and a container without it in its bounding set cannot start the daemon (design 16 item
6). Each keychain step passed on its first run, on `8b02805`.

Phase 7a's two projects and its walk-through lengthened the Test steps: the longest in its runs were
35 min 21 s on Windows (run 40993671970) and 37 min 57 s on macOS (run 41038505814), under the 44 at
which the limit is raised. On the review's last code commit before the gate (`7015753`, run
41111820880) the Test step took 29 min 9 s on Windows, 30 min 58 s on macOS and 6 min 1 s on
`ubuntu-latest`, the containers' jobs 7 min 25 s and 7 min 19 s in all, and "Build the opt-in
projects" 22 s. On the gate's commit (`a2c312a`, run 41190572421) it took 21 min 48 s on Windows,
32 min 16 s on macOS and 4 min 54 s on `ubuntu-latest`.

Two more jobs, both named "bare ubuntu container", start from an empty `ubuntu:24.04`
container, run `scripts/setup-linux.sh` in it twice (the second run has to find everything in
place) and then build and test. The script installs the .NET 10 SDK and the system libraries
the tests need (ICU, fontconfig with a font, git, `openssh-client` for the tests of ssh's
passphrase prompt, a locale, and since phase 7a `dbus` and `gnome-keyring` for the Secret Service
store's tests, D259). It takes the SDK from the system's
own package servers when they carry it, as Ubuntu 24.04's do, and from Microsoft's installer
otherwise; one job runs each way (`SETUP_LINUX_SDK_SOURCE=installer` forces the second). The
script is what a Linux cloud session runs to prepare its machine (D39), and these jobs keep it
proven. What they cannot show is whether the downloads get through a cloud session's network
allowlist: a CI runner's network is open. The first way exists for that reason, because the
default allowlist names Ubuntu's package servers but not the server Microsoft's installer
downloads from.

CI's gits: since 2026-10-15 Windows runs 2.56.0.windows.2 and macOS 2.56.0, and the two containers
2.43.0 (Ubuntu 24.04's); `ubuntu-latest` ran 2.55.0 until 2026-10-28 (run 40679200453) and runs
2.56.0 on its `ubuntu-24.04` image from 2026-10-29 (run 40848974456 on, the gate's 41190572421
among them). So no CI run of phase 7a ran 2.55, and only the containers' 2.43 differs from 2.56
(plan risk 26). The development machine runs
2.56.0.windows.4 since 2026-10-28 (D248; 2.36 before). Tests must work on all of them; CI is the
only check on 2.43.

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
12.0.0 and the list on Avalonia's own controls. Phase 5 added none: the blame's text is built on
AvaloniaEdit, the pages, the activity log and the search bar on Avalonia's own controls. Phase 6
added none: the dashboard, the palette, the settings screen and the tab strip are built on
Avalonia's own controls, and the theme's System choice on Avalonia's platform settings. Phase 7a
added one package, Tmds.DBus.Protocol 0.94.1, the D-Bus client that `VisualCommit.Hosting` uses
for the Secret Service's token store on Linux (see "Tokens and the keychains"), at the version
Avalonia 12.1.3 resolves, so it moves with `AvaloniaVersion`, as xunit does (D24); GitHub's client,
the avatars, the fake of GitHub and the live command use .NET's own `HttpClient` and
`System.Text.Json`, and the new test projects use the test packages already there (`xunit.v3`,
`Avalonia.Headless.XUnit`).
`scripts/compare-pictures.py`, the gate's comparison of pictures, needs Python with Pillow and
NumPy on the machine that runs it; nothing in the build or the tests does.
