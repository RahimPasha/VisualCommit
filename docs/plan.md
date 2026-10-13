# VisualCommit — Phased Plan

Scope and feature numbers (C1, O1, T1, Q1, R1 ...) are defined in [requirements.md](requirements.md).

## Architecture

The design and solution layout are in [architecture.md](architecture.md); the reasons behind the
main choices are in [decisions.md](decisions.md).

## Workflow

The project is built in phases, normally one phase per session; a phase built in a cloud session
needs a second session on the Windows machine to close it (D39). A new session remembers nothing
from earlier ones, so everything it needs is in the repo. [CLAUDE.md](../CLAUDE.md) is loaded automatically and holds
the start-of-session steps, the rules and the phase-closing steps. It points to these files:

| File | Purpose | Updated |
|---|---|---|
| [status.md](status.md) | Which phase is current and exactly where work stopped | In every commit that completes a step |
| [architecture.md](architecture.md) | How the app is built, as it actually stands | End of each phase |
| [decisions.md](decisions.md) | Decisions and their reasons, so they are not reopened | When a decision is made |
| `handoffs/phase-N.md` | What phase N built and what the next phase must know | End of each phase |
| `test-reports/phase-N.md` | Visual checks: expected results, then outcomes | Start of each phase (expectations) and end (outcomes) |

How context passes from one phase to the next:

- **Within a phase.** The phase's deliverables are copied into `status.md` as a checklist and
  ticked commit by commit, and "Next step" there is kept accurate in every commit. If a session
  ends early, a new one resumes from "Next step".
- **Between phases.** The closing steps update `architecture.md`, move anything undelivered into
  the phase of this plan that now owns it, and write a handoff from
  [handoffs/TEMPLATE.md](handoffs/TEMPLATE.md): what was built, the exact build and test commands,
  the patterns to follow, known issues, and what the next phase should do first.
- **Checked, not assumed.** Before a phase closes, a fresh agent with no conversation context
  reads only the repo and explains how it would build, test and start the next phase. Gaps it
  hits are fixed in the docs.

Branches and commits:

- Each phase is built on its own branch in small commits. Branch names are listed in `status.md`.
- A phase branch is merged into `master` after it passes the visual test gate and the owner accepts it.
- Commits are pushed to `origin` on GitHub (github.com/RahimPasha/VisualCommit): the phase branch
  during a phase, `master` after a merge. No force-pushes.

## Visual test gate

No phase is finished until it passes this gate. Automated unit and integration tests are required
too, but they do not replace it.

1. **Expectations first.** At the start of the phase, before the UI is built, every visual check
   listed under the phase is written into `docs/test-reports/phase-N.md` (from
   [test-reports/TEMPLATE.md](test-reports/TEMPLATE.md)) with concrete steps and a concrete
   expected result: what is on screen and, where relevant, the git state. Expectations come from
   `requirements.md` and this plan, not from what the finished app happens to show. Checks can be
   added later; changing an expected result afterwards needs a reason in the report.
2. **Scenario repos.** Scripts build small repos with known content for each check (for example,
   two branches set up to conflict), so every run starts from the same state.
3. **Scripted walk-through.** `VisualCommit.VisualTests` (phases 0 to 2; since then a project per
   phase, `VisualCommit.VisualTests.Phase3` and `.Phase4`, D133, D151) drives the full app UI in
   Avalonia's headless mode, with real rendering and simulated mouse and keyboard input (click, right-click,
   double-click, drag, typing), against a scenario repo, and saves a screenshot after every step.
   This runs without taking over the desktop, so it is repeatable and also runs in CI. Standard
   window sizes are 1100×700 and 1920×1080 in logical (scaled) pixels; a check names the theme it
   uses. A "restart" in a scripted check means closing the app and starting a new instance on the
   same temporary data folder; the new instance gets a new application object in the same test
   process (D32).
4. **Real-window pass.** `VisualCommit.RealWindowTests` launches the built app on Windows and repeats
   the key flows in the real window through Windows UI Automation (FlaUI), with real mouse and
   keyboard input and screenshots of the actual window. It is Windows-only and opt-in: it has its
   own command, is never part of the default `dotnet test` run or CI, and the owner is told
   before it starts. The harness lives in the repo, so it does not depend on what tools a session
   happens to have. This catches what the scripted run cannot: window frame, display scaling,
   native dialogs, start-up. It needs the desktop unlocked and uses the real mouse while it runs:
   half a minute for phase 0's pass, longer as phases add checks. It uses the 1100×700 size, plus 1920×1080 when that fits the screen at its display
   scaling; the report records the scaling. A real-window screenshot "matches" when it shows the
   same layout, text and state as the scripted screenshot of the same step; the window frame and
   small font-rendering differences are expected. The pass also measures this: it fails when more
   than 3% of a screenshot's pixels differ from the scripted one (D33). Which checks it repeats
   is decided when the phase's checks are written, and marked in the report: at least one
   screenshot of every new screen, and everything the scripted run cannot prove, such as native
   dialogs, the window's own size and position, and a restart of the real process. A session
   that is not on the Windows machine cannot run this pass and puts nothing in its place: a
   virtual display on Linux is not a real window. It writes the test code for the pass, leaves
   the results as "Not run: needs Windows", and hands the phase to a Windows session (see
   "Sessions away from the Windows machine" in `CLAUDE.md`).
5. **Inspection.** Every screenshot is opened and compared with the expected result in the
   report: layout, text, colours and state. A check passes only when the screenshot shows the
   expected result and the repo's real git state matches it.
6. **Fix and re-run.** Failures are fixed, then the phase's checks and all earlier phases' checks
   are run again. Earlier phases' checks stay in the default test run for this reason. When a
   later phase changes on purpose what an earlier check shows (a placeholder becomes real
   content), that check's test is updated in the same commit, and the change and its reason go
   under "Changes to expected results" in the report of the phase that made it.
7. **Report.** `docs/test-reports/phase-N.md` records the outcome and screenshot file for every
   check. Screenshots stay local under `artifacts/visual/phase-N/` (git-ignored to keep the repo
   small). The report also names the CI run of the gate's commit and which of the screenshots CI
   took on macOS and Linux were looked at, and ends with a short checklist for testing by hand
   on macOS. The session that runs the real-window pass runs `dotnet test` on the same commit
   first and inspects the pictures of both passes itself: pictures a cloud session inspected
   were drawn on Linux and are gone with its machine.

## Phases

Sizes are relative: S, M, L.

### Phase 0 — Foundation (S)

Delivers:
- Solution, projects, `.editorconfig`, `.gitignore`, `.gitattributes`, README, central package versions.
- App shell: the main window laid out as in "Main window layout" in `requirements.md`, with placeholder content in each region; dark and light themes with a theme switch. The theme's colour tokens are recorded in `architecture.md`.
- Git runner: locate git, check its version, run asynchronously, cancel, stream output, record each call.
- Settings store (the chosen theme survives a restart) and local log file.
- Test harness: temporary-repo builder and headless UI tests.
- Visual test harness: scripted walk-through with screenshots, and the real-window pass, both proven to work on this machine.
- CI workflow that builds and tests on all three platforms, run on GitHub by pushing the phase branch.
- The Commands section of `CLAUDE.md` filled in with the real build, run and test commands.

Visual checks:
- The shell in dark and light themes, at both standard window sizes.
- Clicking the theme switch changes the theme, and the screenshot shows it.
- After a restart the app opens in the theme chosen before.
- A real-window screenshot matches the scripted one for the same step.

Done when: the app launches to an empty shell on Windows, all tests pass locally, the visual gate passes, and CI is green on all three platforms.

Outcome: delivered in full. What was built and proven is in [handoffs/phase-0.md](handoffs/phase-0.md);
two follow-ups it found are now listed under phase 1.

### Phase 1 — Repos and commit graph, read-only (L)

Covers C1, C2, C3, Q1, Q4.

Delivers:
- Open, init and clone (with progress); recent repos; several repo tabs, restored on restart.
- Commit loading streamed in pages; lane layout; custom-drawn virtualised graph with ref labels and author, date and SHA columns.
- Left panel: branches (as folders), remotes, tags, stashes; ahead/behind counts; filter box.
- Commit details panel: message, author, changed-file list (tree or flat).
- File watcher that refreshes on outside changes.
- From phase 0: the window's size and position and the widths of the two side panels are saved and restored with the tabs. Today they reset on every start.
- From phase 0: when the window is too narrow for the side panels at their current widths, the panels give way. Today a panel widened in a large window is cut off after the window is made small.

Visual checks:
- The graph of a scenario repo with branches, merges, tags and a stash: lanes, colours and labels match the known history.
- A 100k-commit repo at the top, middle and bottom of its history: no blank or misdrawn rows.
- Left panel folders, ahead/behind counts, and the filter narrowing the list.
- Clicking a commit shows the right message and files.
- Clone progress; three repo tabs; tabs restored after a restart.
- A commit made in a terminal appears in the graph.

Done when: a 100k-commit repo shows its first graph within about 2 seconds and scrolls smoothly, and the visual gate passes. The two seconds and the smoothness are judged on the Windows development machine; a time measured in a cloud session or on a CI runner is recorded as an indication, with the machine named.

Outcome: delivered in full, and the gate passed on `5673c3f`. Q1 measured 1002 ms to the first rows of the 100k repo, and the frames after the load had a 95th percentile of 1.1 ms. What was built and proven is in [handoffs/phase-1.md](handoffs/phase-1.md), the gate in [test-reports/phase-1.md](test-reports/phase-1.md). Two follow-ups it found are now listed under phase 3, and a cosmetic one under phase 6.

### Phase 2 — Diff and commit workflow (L)

Covers C4, C5.

Delivers:
- Diff viewer: side-by-side and inline, syntax highlighting, word-level highlights, image diff, fallbacks for binary and very large files.
- Working-changes row in the graph; staged and unstaged lists.
- Stage, unstage and discard by file, hunk and line.
- Commit and amend, with a summary and description editor.
- A recoverable snapshot before every discard.

Visual checks:
- Both diff modes for a modified, added, deleted and renamed file, an image, a binary file and a very large file.
- Staging one hunk and one line: lists and diff update, and the index matches.
- Commit and amend: the graph updates correctly.
- Discard, then restore the discarded changes.

Done when: edit, review, partial stage, commit and amend all work in the app, and the visual gate passes.

Outcome: delivered in full, and the gate passed on `2a13804`. Before the gate, an independent
review of the code found 14 problems, among them two that could lose work (patches around a
missing final line feed, and Restore over newer work); all were fixed with tests (D74 to D76).
Q1 measured 1058 ms in the gate's real-window pass. What was built and proven is in
[handoffs/phase-2.md](handoffs/phase-2.md), the gate in
[test-reports/phase-2.md](test-reports/phase-2.md). Two follow-ups are now listed under phase 3.

### Phase 3 — Branch, remote, stash and tag operations (L)

Covers C6, C8, C9, C11, Q2, Q3 and the interaction model.

Delivers:
- Checkout, create, rename, delete; merge and fast-forward; reset; cherry-pick; revert.
- Fetch, pull, push, safe force-push, set upstream, manage remotes, auto-fetch.
- Stash save, apply, pop, drop; tag create, delete, push.
- Context menus on every object, double-click actions, drag a branch onto a branch, multi-select commits.
- Operation queue with progress, cancel, and error messages that include git's own output. It takes over phase 2's writes, which already run one at a time with the watcher paused (D71, D76).
- From phase 2 (D70): stage, unstage and discard in a file's right-click menu, in both stage lists and in the diff view. Today they are buttons.
- From phase 1 (D53): the toolbar's Fetch, Pull, Push, Branch, Stash and Pop buttons, and the status bar's operation status, become real. Undo, Redo and Search stay placeholders until phase 5.
- Credential and SSH passphrase prompts as in-app dialogs.
- From phase 1: ssh's own prompts (an unknown host key, a key's passphrase) are answered in the app or turned off. Today a clone over ssh can wait for an answer on the terminal the app was started from, until it is cancelled.
- From phase 1: the left panel's selection follows the graph's. Today a clicked ref keeps its selection background after the graph selection moves on.
- Backup references before destructive operations.
- Basic conflict state: a banner with abort, and conflicted files flagged. The full resolver comes in phase 4.

Visual checks:
- Every context menu (commit, branch, remote branch, tag, stash, file) opened and checked.
- Double-click checkout; dragging a branch onto a branch shows the action menu.
- Before and after graphs for each operation: both kinds of merge, each reset mode, cherry-pick, revert, stash, tag.
- Fetch, pull and push against a local remote: progress and ahead/behind counts.
- A rejected push shows git's message; destructive actions show a confirmation.

Done when: a normal day of branch and remote work needs no terminal, every row of the interaction table works, and the visual gate passes.

Outcome: delivered, except what follows. Left out on purpose (D77, D92), not shown disabled:
rebase onto (from the menus and by drag), squash, compare, file history, blame and create pull
request; they are now listed under phases 4, 5 and 7, and the interaction table's rows work apart
from these items. Known gaps: an auto-fetch over ssh has no timeout, so a stalled connection holds
the repository's queue until the user cancels it from the status bar (D125); under the reftable
ref format (Git 2.45 and later) a stopped cherry-pick or revert is not detected and shows no
banner (D128); no code reads `remote.<r>.fetch`, so the push of a drop onto a remote branch and a remote branch's
"Delete <b> on <r>…" take the branch's name on the remote from its remote-tracking name
(`DropFacts.RemoteMapsBranchesDifferently` is never set); a rename that changes only the case of
a branch's name is refused on Windows and macOS (D131); `AutoFetchMinutes` has no screen and is
edited in `settings.json`. Before the gate, an independent review of the code found, among
others, a hard reset that deleted untracked files no backup held, a force push whose question
named a branch other than the one it rewrote on the remote, and a second Push folded into a running one so that a new
commit was never pushed; all were fixed with tests (D123 to D131). The gate passed on `594f9fb`:
the scripted pass (`dotnet test`, 1,756 tests) and the real-window pass ran on that commit, every
picture of both was inspected, and CI run 38206493320 passed in all five jobs. Q1 measured
1189 ms in the gate's real-window pass, but up to 2268 ms the same day on a warmer laptop (risk
10). What was built and proven is in [handoffs/phase-3.md](handoffs/phase-3.md), the gate in
[test-reports/phase-3.md](test-reports/phase-3.md). The follow-ups are now listed under phases 4
to 8.

### Phase 4 — Rebase and conflict resolution (L)

Covers C7, C10, O1.

Delivers:
- Rebase a branch onto another from the context menu or by drag-and-drop; pull with rebase.
- Interactive rebase: reorder, squash, fixup, reword, drop. Squash a multi-selection of commits.
- In-progress banner for merge, rebase, cherry-pick and revert: continue, skip, abort.
- Conflict resolver: three panes (ours, theirs, result); pick ours, theirs or both per hunk; edit the result freely; take a whole file from one side; mark resolved; launch an external merge tool.
- From phase 3 (D77, D92): "Rebase onto" in the commit and branch menus, rebase in the menu of a branch dropped onto a branch, and squash on a multi-selection. The menus are built as data by `RepositoryMenus` (`ForCommit`, `ForLocalBranch`, `ForDrop`, `ForCommits`); the new items go there. Squash builds on the multi-selection kept as SHAs (D88).
- From phase 3: Pull already rebases when `pull.rebase` or `branch.<name>.rebase` asks for it (D78, D84), and a pull whose rebase stops shows the banner with Abort. Left here: a pull with rebase chosen for one pull, whatever git's configuration says, and continue and skip for a pull's rebase.
- From phase 3: Continue and Skip in the in-progress banner (`ConflictBanner`, `ConflictBannerViewModel`, read from `InProgressState`), which offers Abort only and, for a rebase or a cherry-pick or revert sequence started in a terminal, says to continue in a terminal (D89, D110). Several commits are picked and reverted with one git command that takes the whole list, so that git's sequencer keeps it and Continue, Skip and Abort act on the list (D136, replacing D88's one command per commit; phase 3's code still runs one command per commit and the status bar only names the picks a stop left, until this phase changes it). The stage panel's Commit button does not finish such a pick; Continue does.
- From phase 3: under the reftable ref format (Git 2.45 and later) `CHERRY_PICK_HEAD` and `REVERT_HEAD` are refs, not files, so `GitRepository.InProgress.cs` and `GitRepository.History.cs` miss a stopped pick or revert, which then gets no banner of its own (D128, risk 17). Read them as refs.
- Interactive rebase sets its own sequence editor: every git call runs with `GIT_SEQUENCE_EDITOR=:` and `GIT_EDITOR=:` (D68, D102).
- From the owner's answers of 2026-10-11 and 12 (D135, D137): a rebase asks for no confirmation; when it or a merge stops on a conflict the user resolves it or aborts and is back where everything was. A rebase saves a backup ref of the branch's old tip first. Backups under `refs/visualcommit/backup/` are cleaned up automatically: a backup goes after 30 days or when more than a set number exist (the oldest first), never the backup of an operation still in progress. Propose the number, the moment of the cleanup and the app's message about it before building it.

Visual checks:
- Before and after graphs for a plain rebase.
- The interactive rebase screen: reorder by drag, squash, reword, drop, and the resulting history.
- A conflict in a merge, in a rebase with two conflicting commits, and in a cherry-pick: banner, the three panes' content, picking ours, theirs and both, the result pane, mark resolved, continue.
- Abort returns the repo to its starting state.

Done when: the scripted conflict scenarios are resolved end to end in automated tests, and the visual gate passes.

Outcome: delivered in full; nothing moves to a later phase. Design 13.5 and the report's "What
proves each Delivers item" map every item to its tests and checks (18 checks in the scripted
walk-through, 10 launches in the real window). Decided in the design and not built, on purpose: an
interactive rebase that includes the root commit (no `--root`, D143), syntax colours in the
resolver's editable result (D148), a splitter between the resolver's halves, and Continue and
Skip for `git am`, which the banner still leaves to a terminal. Known limits: AvaloniaEdit undoes
typed text one character per step (a choice or a whole side is one step), and an interactive
rebase or a squash never moves other branches, even with `rebase.updateRefs` (D143). Before the
gate, an independent review of the code (five reviewers, each finding verified by a second agent)
confirmed 31 findings, 5 major and none a blocker, among them a rebase's Continue that committed
before git refused to continue for other unstaged files, an interactive list that silently left
out an empty commit of the branch, and a Mark resolved that could write over a file changed while
it waited in the queue; all were fixed with tests (design 14.5). The gate passed on `6badab7`: the
default run's 2,411 tests in 21 min 52 s, and the real-window pass of phases 0 to 4 (43 tests) in
14 min 4 s, every picture inspected; CI run 38320711773 on that commit succeeded in all five jobs. What was built and proven is in [handoffs/phase-4.md](handoffs/phase-4.md), the gate in
[test-reports/phase-4.md](test-reports/phase-4.md).

### Phase 5 — History, traceability and undo (L)

Covers T1–T5, O2, O6, O7, O8, C12.

The owner's answers of 2026-10-12, given when the phase started, are D153 to D163.

Delivers:
- File history (following renames) and blame, with click-through to commits, in place of the graph (D158).
- Reflog viewer with restore; activity log panel under the graph, opened from the status bar (D156).
- Commit search by message, author, SHA and file; search inside changes. Matches are marked in the graph (D157).
- Compare any two commits or branches: two selected commits show what changed between them at once (D162).
- Undo and redo of the app's git actions, many steps back within the session (D154); undoing a change to a remote asks, then pushes back with a lease (D155); the platform's undo and redo keys do the same outside text boxes (D163).
- Solo and hide branches in the graph, remembered per repository across restarts (D159).
- From the backlog (D138, D153): C12, protect uncommitted changes. A checkout that would overwrite them does not start and says why ("1 conflict prevents checkout. Commit, stash, or discard your changes first.") with a "Stash changes and check out" button; checking out a commit the current branch reaches stashes them automatically ("Auto stash before checking out …"), and that stash is kept, drawn in the graph as a hollow circle joined to its commit, until the user pops or drops it. Every stash's line to its commit is dashed (D161).
- From phase 4 (D160): the details panel's SHA that wraps one character, the resolver's arrow buttons that look the same when disabled, the status bar's Cancel tooltip while the merge tool runs, two stale comments, and the watcher's quiet period on a clock its test holds (a flaky test on CI).
- From phases 3 and 4: undo reads the backup refs that phases 2 to 4 write before every destructive operation, `refs/visualcommit/backup/<kind>-<UTC time>[-<n>][/<name>]`, one timestamp per operation (D67, D83, D127). The kinds are `discard` (also the snapshots the conflict resolver saves before it overwrites or deletes a file it cannot otherwise keep, D148), `reset` (with `reset-<time>-changes` after a hard reset, and after a mixed one when the index differed from HEAD), `abort` (with its own `-changes`), `detached`, `branch`, `stash`, `tag`, `force-push`, `remote-branch`, `remote`, `remote-tag`, and phase 4's `rebase` (`rebase-<time>/<b>`, the branch's old tip before every rebase, D142) and `skip` (with its own `-changes`, D144); `architecture.md` says what each holds. They keep their commits from git's garbage collection, and phase 4 cleans them up (D139): a backup goes after 30 days, then the oldest while more than 50 exist, never while an operation is in progress in any worktree; undo offers what is left. The clean-up says nothing on screen: the activity log is to show the app log's `Removed backup <kind>-<time>[-<n>] (older than 30 days)` and `(more than 50 backups)` lines.
- From phase 3: the activity log reads `OperationLog` (`IOperationLog`: the last 500 `OperationRecord`s of the session, in memory, with title, start, duration, outcome and the status bar's message) and groups the git call records of `IGitCallLog` under them by `GitCallRecord.OperationId`. From phase 4: the background operation "Clean up backups" is recorded there too (with an empty message: what it removed is only in the app log), and a success can be a warning (`OperationReport.Warning`, `OperationResult.IsWarning`), which `OperationRecord` does not carry yet.
- From phase 3 (D77, D92): compare on a multi-selection (`RepositoryMenus.ForCommits`), file history and blame in the file menus (`ForUnstagedFile`, `ForStagedFile`, `ForCommitFile`, `ForDiff`), and solo and hide in the branch menus.
- From phases 1 and 3: the toolbar's Undo, Redo and Search become real; they are placeholders (D53, D90).

Visual checks:
- File history across a rename; blame gutter and click-through.
- Reflog restore; activity log entries; search results; compare view.
- Undo and redo for each destructive action from phases 2–4.
- Solo and hide change the graph as expected.
- C12: a refused checkout's message and its button; the automatic stash in the graph after an older commit of the branch is checked out.

Done when: any line of any file can be traced to its commit in two clicks, each destructive action can be undone, no checkout loses uncommitted changes, and the visual gate passes.

### Phase 6 — Multi-repo and productivity (M)

Covers C1 (workspaces), O3, O5, O9.

Delivers:
- Workspaces (repo groups) and a dashboard showing each repo's branch, ahead/behind and uncommitted changes, with bulk fetch and pull.
- Command palette and keyboard shortcuts.
- Settings screen (identity, pull mode, auto-fetch interval, external tools) and theme polish.
- From phase 1: a long folder in the welcome page's error wraps at awkward places ("C:" alone on a line, a name split). Nothing is cut off.
- From phase 3: the settings screen covers `AutoFetchMinutes` (default 5, 0 turns auto-fetch off; only in `settings.json` today, and `VISUALCOMMIT_AUTO_FETCH_MINUTES` overrides it, D85), `StashIncludeUntracked` (today the stash dialog remembers its last choice), and the extensions "Open file" asks about before opening a file (a fixed list in `RepositoryOperations.Files.cs`, plus the executable bit on Unix, D130). Its pull mode has to work through git's own `pull.rebase`, `pull.ff` and `branch.<name>.rebase`, which Pull follows (D78, D84), or D78 is reopened with the owner. Its external tools give the file menus an "Open in editor" beside "Open file", which uses the system's application for the file (requirements' "open in editor or file manager").
- From phase 4: the external merge tool is git's own `merge.tool` (D149); without it the resolver's "Merge tool…" and the file menu's "Open in merge tool" are disabled with "Set merge.tool in git's configuration to use a merge tool". The settings screen's external tools are where the user chooses it, through git's configuration, so that `git mergetool` in a terminal and the app use the same tool (Q5).
- From phase 3: the dashboard's bulk fetch and pull run through each repository's `OperationQueue`, as the toolbar's do (D81), with the same titles, Cancel and error dialog. Auto-fetch fetches only the active tab's repository (D79, D85).
- From phase 3: renaming a branch to a name that differs only in case, which the name dialog refuses on Windows and macOS because Git 2.36 refuses it for a loose ref (D131).
- From phase 3: no code reads `remote.<r>.fetch`, so the push of a drop onto a remote branch and a remote branch's "Delete <b> on <r>…" take the branch's name on the remote from its remote-tracking name; with a fetch refspec that maps names differently they act on the wrong name. Read the refspec and set `DropFacts.RemoteMapsBranchesDifferently`, which the drop menu (`RepositoryMenus.ForDrop`) already turns into a disabled item with its reason.

Visual checks:
- The dashboard with ten repos in mixed states; bulk fetch progress.
- Command palette search and run.
- Settings changes take effect.
- A sweep of every main screen in both themes.

Done when: ten repos can be checked and updated from one screen, every main action is reachable from the keyboard, and the visual gate passes.

### Phase 7 — Hosting: GitHub, Azure DevOps, GitLab (L)

Covers O4, R3.

Delivers:
- Sign-in per provider; tokens in the OS keychain.
- Browse and clone repos from an account.
- Pull-request list per repo; create a pull request from the branch menu or by drag-and-drop; pull-request badge on branches; open in browser.
- Author avatars.
- From phase 3 (D77, D92): "Create pull request" in the branch menus (`RepositoryMenus.ForLocalBranch`, `ForRemoteBranch`) and in the menu of a branch dropped onto a branch (`ForDrop`), and a menu for the left panel's "Pull requests" section, which has none.
- From phase 3: a provider's token reaches git in one of the two ways git asks for credentials: the credential helpers configured for it, asked first (Git Credential Manager included, Q5), or the app's own prompt path, the askpass helper and `AskPassBroker` (D82), which today answers through the credentials dialog. Auto-fetch never prompts (D79, D82), so where git cannot get the token without a prompt, auto-fetch fails with only "Auto-fetch failed" in the status bar (D85).

Visual checks (for each provider):
- Sign-in dialog, repo browser, pull-request list, create-pull-request form, badge on the branch.

Done when: for each of the three providers, sign-in, clone, list pull requests and create a pull request all work, and the visual gate passes.

### Phase 8 — Public release (M)

Covers O19, R1, R2, R4–R7, Q6.

Delivers:
- Installers and auto-update on all three platforms; code signing and notarisation.
- First-run experience: Git detection and guidance; decide whether to bundle Git on Windows.
- Performance pass and keyboard-accessibility pass.
- Third-party notices, user guide.
- Icons and branding; beta release.
- From phase 3: the askpass helper `visualcommit-askpass` (project `src/VisualCommit.AskPass`) is a framework-dependent program that runs on the .NET runtime the app uses (it inherits `DOTNET_ROOT`), and `AskPassLocator` expects it next to `VisualCommit.Git`'s assembly, or in the app's base folder when the assembly has no location (single-file). The installers must ship it in a form that runs there, self-contained packages included; without it every credential and ssh prompt fails.
- From phase 3: OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE`, so when the app was started from a terminal such an ssh still asks there (Ubuntu 20.04 ships 8.2; Git for Windows 2.36 bundles 8.9, and what 2.30 bundles was not checked). The first-run experience says so, or the minimum versions are raised.
- From phase 3: on the Mac, by hand, cancelling a fetch over ssh. Cancel kills only git there (D37); ssh and the askpass helper end when their pipes break, which no automated run proves.

Visual checks:
- Full regression: every check from phases 1–7 re-run against the installed build.
- Install, first run and update on Windows.
- The complete macOS checklist run by hand by the owner; Linux by hand or through WSL.

Done when: a new user on each platform can install, open a repo and receive an update without help.

## Backlog (not scheduled)

O10 submodules, O11 worktrees, O12 Git LFS, O13 built-in terminal, O14 built-in file editor,
O15 AI commit messages, O16 profiles, O17 commit signing, O18 patches.

Left out of phase 4 on purpose (design 14.4, D143, D148): an interactive rebase that includes the
root commit (`--root`); syntax colours in the resolver's editable result; a splitter between the
resolver's halves; Continue and Skip for `git am`, which the banner leaves to a terminal.

## Risks and open decisions

| # | Item | Needed by |
|---|---|---|
| 1 | The real-window pass happens only on the Windows machine; development can also happen in a Linux cloud session (D39), which then leaves that pass to a Windows session. macOS and Linux are covered by CI builds and the scripted walk-through until the owner tests on a Mac by hand. | Mac testing: when the owner chooses; at the latest phase 8 |
| 2 | Settled in phase 4: the conflict resolver is built on AvaloniaEdit as planned (D64, D140, D148). Its panes are phase 2's `DiffPane`, generalised over `IDiffPaneHost` with phase 2's 66 pictures byte-identical, and its result an editable `ConflictResultEditor`. Still true: it leans on details of AvaloniaEdit 12.0.0 (built against Avalonia 12.0.0, used with 12.1.3) that an update can change: undo and redo go through the document's `UndoStack` with the platform's gestures from `HotkeyConfiguration`, because the editor's own routed bindings are removed and pick the modifier by the operating system; typed text is undone one character per step; the caret is the app's own, because AvaloniaEdit's blink cannot be stopped. `ConflictResultEditorTests`, `ConflictPanesTests` and phase 2's pictures catch a change. The result has no syntax colours (D148). | Whenever AvaloniaEdit or Avalonia is updated |
| 3 | Settled in phase 0: the real-window pass works. The in-repo harness finds the app through Windows UI Automation, clicks with the real mouse and captures the window; phases 0, 1 and 2 together take about 6 minutes, and phase 3 adds more (risk 18). Its comparison passes small differences such as a hover background or a tooltip (phase 1's report), so only the inspection of the pictures catches them. Still true: it needs an unlocked desktop, and the development screen (a work area of 1500×952 logical pixels at 200% scaling) is too small for its 1920×1080 size, so that size is only ever proven by the scripted walk-through unless a larger screen is used. | Each phase's gate |
| 4 | Hosting checks need a test account or token for each of GitHub, Azure DevOps and GitLab. | Phase 7 |
| 5 | Public sign-in needs app registrations with GitHub, GitLab and Microsoft; personal access tokens work without them. | Phase 7 |
| 6 | Code-signing certificate and Apple Developer membership cost money. | Phase 8 |
| 7 | A built-in terminal (O13) is harder in Avalonia than in web technology. | Backlog |
| 8 | Settled on 2026-10-06: the owner made the GitHub repo public, so CI no longer runs on a monthly allowance of minutes (D38). While it was private, phase 0 used the allowance up. Still true: a job that hangs wastes time, so `ci.yml` keeps a time limit on every job and its test step, raised as the tests grow (in phase 4 to 24 minutes for the test step and 32 per job of the three platforms, D152; risk 12). | Settled |
| 9 | The tests are pinned to xunit.v3 3.2.2 because Avalonia's headless test package does not work with xunit.v3 4.x (D24). Check for a newer Avalonia.Headless.XUnit when Avalonia is updated. | Whenever packages are updated |
| 10 | Q1 has about 0.8 s to spare on the rested development laptop (1002 ms at phase 1's gate, 1058 ms at phase 2's, 1189 ms at phase 3's), but much less when the laptop is warm: on phase 3's gate day other runs measured 1438 to 2268 ms, and the test fails above 2000 ms, so a warm laptop can fail a gate by itself. Run alternately on the same machine that day, the phase 2 build needed 1688 to 1911 ms and phase 3's 1618 to 2002 ms: the spread is the machine's temperature, and phase 3 costs no more than that noise. Rest the laptop before the real-window pass, and to tell a slower build from a warm machine, run the old and the new build alternately on the same machine. Most of the time is `git log --date-order`, which reads the whole history before its first commit when the repository has no commit-graph file. A much larger repository, or a slower machine, could need another way to the first page. | Whenever the graph or its loading changes |
| 11 | GitHub moves the `ubuntu-latest` CI runner to Ubuntu 26 from 2026-10-19 (an annotation on every run). The Linux job and the set-up script may need attention then. | The first CI run after 2026-10-19 |
| 12 | The default test run took 21 to 22 minutes at phase 4's gate on the development laptop: 21 min 52 s on the gate commit `6badab7`, 2,411 tests, where the five projects, running side by side, ended after 8 minutes (phases 0 to 2's walk-through), 16.5 (phase 4's walk-through), 18 (`VisualCommit.Git.Tests`), 21.5 (`VisualCommit.App.Tests`) and 22 (phase 3's walk-through). Phase 3's gate had taken 19 min 15 s for 1,756 tests, and runs during phase 3 21 to 26 minutes while other programs took one to three of the laptop's eight logical processors. On CI, on the gate commit, the Windows job took 15 minutes (its Test step 13 min 9 s), macOS 15 minutes (its Test step 13 min 57 s), and Linux and the two containers 3 to 4 minutes (Test steps 2 to 2.5); the Windows Test step had passed 12 of its 14 minutes during phase 4, so D152 raised the limits of the three platforms' jobs to 24 minutes for the Test step and 32 per job (amending D134); when the project is over, the owner decides whether a CI run of 30 minutes or more is reasonable (D134). The run is bound by the processors, which git's processes and the virus scanner keep at about 90 percent, so running more tests side by side only slows each git call; other load or a hot laptop lengthens it. The owner set up the virus-scanner exclusion for the repository folder and `%TEMP%\VisualCommit.Tests` on 2026-10-12. Within that, each test assembly has one headless UI thread and xunit runs the tests of one class one after another, so the longest project sets a floor. Done in phase 3 (D132, D133): the phase 3 walk-through is the project `VisualCommit.VisualTests.Phase3`, the largest test classes are split (`RepositoryOperationsTests` into 12 classes in 4 collections, `ScenarioTests` into 2, the pull-parity tests into 9), shared scenario templates are built once per process, and the hang watchdog leaves a test's wait for them out of its time. Done in phase 4 (D151): its walk-through is a fifth project, `VisualCommit.VisualTests.Phase4`, and its Git-layer and App tests are split by subject as phase 3's are. Still possible: scripted checks sharing an app launch where their scenarios allow, keeping the pull-parity templates between runs as the 100k-commit repository is kept, and the walk-throughs in more projects, which helps only where a project's single UI thread, not the processors, sets the length. Checks that waited 5 seconds for an outside change (phase 1's check 18, phase 2's check 20) timed out under load, and checks that looked at the view before it was drawn failed under it (phase 1's checks 12 and 18 during phase 3's gate); since `594f9fb` the outside-change waits are 30 seconds and those checks wait for what they assert, and since phase 4 phase 1's clone checks are held at a fixed percentage instead of racing the clone (D150). Let the laptop rest before a gate's run. | Each phase's gate |
| 13 | On Linux, .NET's file watcher puts a watch on every folder of the working tree, ignored ones such as `node_modules` included. A very large tree can use up the user's inotify limit, which other programs share; the watcher then logs a warning and changes made outside the app show only after the next refresh. Phase 2 starts the watcher off the UI thread, so it no longer holds opening up. | When watching changes, or Linux users report it |
| 14 | The askpass helper (`src/VisualCommit.AskPass`, D82) is framework-dependent: it runs on the .NET runtime the app uses and must lie where `AskPassLocator` looks. A package that leaves it out, or ships it without a runtime it can use, makes every credential and ssh prompt fail; nothing but a prompt shows it. | Phase 8 |
| 15 | OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE`: when the app was started from a terminal, such an ssh asks there instead of in the app, and the operation waits until it is cancelled (Ubuntu 20.04 ships 8.2). Which OpenSSH the oldest supported Git for Windows (2.30) bundles was not checked. | Phase 8 (first run) |
| 16 | A network operation the user stops can leave lock files, which make every later fetch fail (seen on Git 2.36, D81). After a Cancel the app lists only what a fetch or push can lock (`refs/**`, `packed-refs.lock`, `shallow.lock`, `config.lock` of the git folders), only those unchanged 0.5 s later, and removes one only if its time and length are still the same (D126). A lock that a slow process of another program holds without writing to it for that long would be listed too; the user decides. A network operation stopped by "Quit now" leaves its locks with no report. | When users report locks |
| 17 | Settled in phase 4 (D146): `CHERRY_PICK_HEAD` and `REVERT_HEAD`, refs rather than files under the reftable ref format (Git 2.45 and later), are read in one place (`GitRepository.ReadPseudoRefAsync`): as files, and through git (`rev-parse --verify --quiet --symbolic-full-name`, so that a branch of that name does not answer) only when the repository's configuration names reftable and `MERGE_MSG` or `sequencer/` exists, so a files repository with nothing in progress makes no git call. `PseudoRefTests` run a real reftable repository with a stopped pick on CI's git (2.55); the development machine's Git 2.36 cannot make one, so only CI proves it. | Settled |
| 18 | The real-window pass grew from about 6 minutes (phases 0 to 2) to 11 min 11 s with phase 3's checks at its gate (11 min 42 s with the build), and to 14 min 4 s for phases 0 to 4 (43 tests) at phase 4's gate, which added 10 launches (the design had estimated 5 to 7 minutes more): every real input waits 400 ms and the first menu of a launch takes up to about 2 s. It takes over the mouse for all that time, needs an unlocked desktop and runs only on the Windows machine, so a phase is closed only by a session there (risk 1). A commit that changes more than docs after an inspection means both passes again, so phase 3's gate ran the pass three times: collect all fixes before running it again. Shared launches keep it down; later phases add to it. | Each phase's gate |
| 19 | Auto-fetch over ssh has no timeout (D125): the only way to set one through git is `core.sshCommand`, which would replace the user's own. A stalled ssh connection holds the repository's queue until the user cancels the auto-fetch from the status bar, which offers Cancel while a user operation waits behind it. HTTP gives up by itself after 60 seconds below 1 byte per second (`http.lowSpeedLimit`, `http.lowSpeedTime`). | When users report it |
| 20 | "Open file" asks before opening a file that can run programs by a fixed list of extensions on every platform, plus the executable bit on Unix (D130, `RepositoryOperations.Files.cs`). A runnable type missing from the list (Windows' `PATHEXT`, the system's file associations and macOS bundles are not read) opens without a question; the list is not a setting. | Phase 6 (settings), or when a missing type is found |
| 21 | Tooltips are drawn on the window's overlay layer everywhere since phase 3 (D105), so in the real window a tooltip is inside the window and its capture. A phase 0 to 2 real-window picture that showed a tooltip as a window of its own can now differ from the one its gate inspected; the phase 3 report lists such differences under "Changes to expected results". | Phase 3's gate |
| 22 | Phase 3 relies on Avalonia 12.1.3 details that an update can change: the menu adapter's automation peer, which offers Invoke through `MenuBase.Close()` and `MenuItem.ClickEvent` (D105, `ContextMenuAdapterTests` catch a change); Shift+F10, whose gesture list was read on Win32 and headless only, so on macOS and Linux the app raises the request itself if the list lacks it, which CI's headless runs cannot tell apart (D106); and `SecretTextBox`, whose peer hides typed secrets because Avalonia cannot report a password box (D130). | Whenever Avalonia is updated |
| 23 | Pull is the app's own fetch and integration, chosen as `git pull` would choose it (D84), and since phase 4 also a rebase chosen for one pull, as `git pull --rebase` (D147). Git may change its rules; `PullParityTests` and phase 4's `PullRebaseParityTests` (with its settings and fork-point classes) run on CI's newer git and catch that. Not mirrored: submodule recursion after a pull, and options that exist only on pull's command line. Fetching all remotes runs one `git fetch` per remote, one after another, so `fetch.parallel` no longer applies and many remotes fetch more slowly (D109). | Whenever git or the pull rules change |
| 24 | Settled in phase 4 (D139): the backup refs under `refs/visualcommit/backup/` (D67, D83), which keep their commits and snapshots from git's garbage collection, are cleaned up automatically. A backup (one operation's refs) goes after 30 days, then the oldest while more than 50 remain; nothing goes while an operation is in progress in any worktree, nor the restore bar's snapshot, nor a ref that fits no known scheme. It runs at the end of every operation that saved a backup and in the background when a repository opens, says nothing on screen and logs a line per backup removed. Phase 5's undo offers what is left. | Settled |
