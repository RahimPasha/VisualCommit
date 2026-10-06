# Tracery — Phased Plan

Scope and feature numbers (C1, O1, T1, Q1, R1 ...) are defined in [requirements.md](requirements.md).

## Architecture

- **One process, all C#.** An Avalonia desktop app using MVVM (CommunityToolkit.Mvvm). The UI calls
  the git layer directly; there is no web server or API layer in between.
- **Git engine: the real `git` executable.** A process runner starts git, streams its output and
  parses the machine-readable formats (`log` with a custom format, `status --porcelain=v2`,
  `blame --porcelain`, unified diffs). Hunk and line staging build a patch and apply it to the index.
- **Operation queue per repo.** One writing operation at a time, with progress, cancellation and a
  record in the activity log (T4).
- **Safety net.** Before a destructive operation the app stores a backup reference under
  `refs/tracery/backup/`. Undo/redo (O2) restores from these and from the reflog.
- **Prompts.** Git's credential, passphrase and rebase-editor prompts are redirected to small
  helper hooks that talk to the app, so they appear as in-app dialogs.
- **Editors.** Diff, blame and conflict views are built on AvaloniaEdit with TextMate grammars for
  syntax highlighting.
- **Hosting.** One provider interface with GitHub, Azure DevOps and GitLab implementations.
  Tokens live in the OS keychain.
- **Storage.** Settings and session state as JSON in the per-user app-data folder. Logs are local files.
- **Tests.** xUnit. Git-layer tests run real git against temporary repos. UI tests use Avalonia's
  headless mode and capture screenshots.
- **CI.** GitHub Actions builds and tests on Windows, macOS and Linux on every push.
- **Packaging.** Velopack for installers and auto-update on all three platforms.

## Solution layout

| Project | Contents |
|---|---|
| `Tracery.Core` | Domain models and interfaces; no UI, no process calls |
| `Tracery.Git` | Git runner, output parsers, operation queue, repo watcher |
| `Tracery.Hosting` | GitHub, Azure DevOps and GitLab clients; token store |
| `Tracery.App` | Avalonia views, view models, theme, custom controls (graph, diff, resolver) |
| `Tracery.Git.Tests` | Integration tests against temporary repos |
| `Tracery.App.Tests` | View-model tests and headless UI tests |
| `Tracery.VisualTests` | Scenario repos, scripted walk-throughs and screenshot capture for the visual test gate |

## Workflow

- Each phase is built on its own branch (`phase/0-foundation`, `phase/1-graph`, ...) in small commits.
- A phase branch is merged into the main branch after it passes the visual test gate and the owner accepts it.
- Nothing is pushed to a remote until the owner asks.

## Visual test gate

No phase is finished until it passes this gate. Automated unit and integration tests are required
too, but they do not replace it.

1. **Scenario repos.** Scripts build small repos with known content for each check (for example,
   two branches set up to conflict), so every run starts from the same state.
2. **Scripted walk-through.** Each check drives the full app UI with simulated mouse and keyboard
   input (click, right-click, double-click, drag, typing) against a scenario repo and saves a
   screenshot after every step. This runs without taking over the desktop, so it is repeatable
   and also runs in CI on all three platforms.
3. **Real-window pass.** The built app is launched on Windows and its key flows are repeated in
   the real window with real input, with screenshots of the actual window. This catches what the
   scripted run cannot: window chrome, display scaling, native dialogs, start-up. It needs the
   desktop unlocked and uses the real mouse for a few minutes.
4. **Inspection.** Every screenshot is looked at and compared with the expected result written in
   the phase's checklist: layout, text, colours and state. A check passes only when the
   screenshot shows the expected result and the repo's real git state matches it.
5. **Fix and re-run.** Failures are fixed, then the phase's checks and all earlier phases' checks
   are run again.
6. **Report.** `docs/test-reports/phase-N.md` lists every check, its expected result, the outcome
   and the screenshot file. Screenshots stay local under `artifacts/visual/phase-N/` (git-ignored
   to keep the repo small). The report ends with a short checklist for testing by hand on macOS.

## Phases

Sizes are relative: S, M, L.

### Phase 0 — Foundation (S)

Delivers:
- Solution, projects, `.editorconfig`, `.gitignore`, README, central package versions.
- App shell: main window, repo tab strip, left / centre / right panes as placeholders, dark and light theme tokens.
- Git runner: locate git, check its version, run asynchronously, cancel, stream output, record each call.
- Settings store and local log file.
- Test harness: temporary-repo builder and headless UI tests.
- Visual test harness: scripted walk-through with screenshots, and the real-window pass, both proven to work on this machine.
- CI build and test on all three platforms.

Visual checks:
- The shell in dark and light themes, at a small and a large window size.
- A simulated click changes the UI, and the screenshot shows the change.
- A real-window screenshot matches the scripted one.

Done when: the app launches to an empty shell on Windows, CI is green on all three platforms, the runner tests pass and the visual gate passes.

### Phase 1 — Repos and commit graph, read-only (L)

Covers C1, C2, C3, Q1, Q4.

Delivers:
- Open, init and clone (with progress); recent repos; several repo tabs, restored on restart.
- Commit loading streamed in pages; lane layout; custom-drawn virtualised graph with ref labels and author, date and SHA columns.
- Left panel: branches (as folders), remotes, tags, stashes; ahead/behind counts; filter box.
- Commit details panel: message, author, changed-file list (tree or flat).
- File watcher that refreshes on outside changes.

Visual checks:
- The graph of a scenario repo with branches, merges, tags and a stash: lanes, colours and labels match the known history.
- A 100k-commit repo at the top, middle and bottom of its history: no blank or misdrawn rows.
- Left panel folders, ahead/behind counts, and the filter narrowing the list.
- Clicking a commit shows the right message and files.
- Clone progress; three repo tabs; tabs restored after a restart.
- A commit made in a terminal appears in the graph.

Done when: a 100k-commit repo shows its first graph within about 2 seconds and scrolls smoothly, and the visual gate passes.

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

### Phase 3 — Branch, remote, stash and tag operations (L)

Covers C6, C8, C9, C11, Q2, Q3 and the interaction model.

Delivers:
- Checkout, create, rename, delete; merge and fast-forward; reset; cherry-pick; revert.
- Fetch, pull, push, safe force-push, set upstream, manage remotes, auto-fetch.
- Stash save, apply, pop, drop; tag create, delete, push.
- Context menus on every object, double-click actions, drag a branch onto a branch, multi-select commits.
- Operation queue with progress, cancel, and error messages that include git's own output.
- Credential and SSH passphrase prompts as in-app dialogs.
- Backup references before destructive operations.
- Basic conflict state: a banner with abort, and conflicted files flagged. The full resolver comes in phase 4.

Visual checks:
- Every context menu (commit, branch, remote branch, tag, stash, file) opened and checked.
- Double-click checkout; dragging a branch onto a branch shows the action menu.
- Before and after graphs for each operation: both kinds of merge, each reset mode, cherry-pick, revert, stash, tag.
- Fetch, pull and push against a local remote: progress and ahead/behind counts.
- A rejected push shows git's message; destructive actions show a confirmation.

Done when: a normal day of branch and remote work needs no terminal, every row of the interaction table works, and the visual gate passes.

### Phase 4 — Rebase and conflict resolution (L)

Covers C7, C10, O1.

Delivers:
- Rebase a branch onto another from the context menu or by drag-and-drop; pull with rebase.
- Interactive rebase: reorder, squash, fixup, reword, drop. Squash a multi-selection of commits.
- In-progress banner for merge, rebase, cherry-pick and revert: continue, skip, abort.
- Conflict resolver: three panes (ours, theirs, result); pick ours, theirs or both per hunk; edit the result freely; take a whole file from one side; mark resolved; launch an external merge tool.

Visual checks:
- Before and after graphs for a plain rebase.
- The interactive rebase screen: reorder by drag, squash, reword, drop, and the resulting history.
- A conflict in a merge, in a rebase with two conflicting commits, and in a cherry-pick: banner, the three panes' content, picking ours, theirs and both, the result pane, mark resolved, continue.
- Abort returns the repo to its starting state.

Done when: the scripted conflict scenarios are resolved end to end in automated tests, and the visual gate passes.

### Phase 5 — History, traceability and undo (M)

Covers T1–T5, O2, O6, O7, O8.

Delivers:
- File history (following renames) and blame, with click-through to commits.
- Reflog viewer with restore; activity log panel.
- Commit search by message, author, SHA and file; search inside changes.
- Compare any two commits or branches.
- Undo and redo of the last git action.
- Solo and hide branches in the graph.

Visual checks:
- File history across a rename; blame gutter and click-through.
- Reflog restore; activity log entries; search results; compare view.
- Undo and redo for each destructive action from phases 2–4.
- Solo and hide change the graph as expected.

Done when: any line of any file can be traced to its commit in two clicks, each destructive action can be undone, and the visual gate passes.

### Phase 6 — Multi-repo and productivity (M)

Covers C1 (workspaces), O3, O5, O9.

Delivers:
- Workspaces (repo groups) and a dashboard showing each repo's branch, ahead/behind and uncommitted changes, with bulk fetch and pull.
- Command palette and keyboard shortcuts.
- Settings screen (identity, pull mode, auto-fetch interval, external tools) and theme polish.

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

Visual checks:
- Full regression: every check from phases 1–7 re-run against the installed build.
- Install, first run and update on Windows.
- The complete macOS checklist run by hand by the owner; Linux by hand or through WSL.

Done when: a new user on each platform can install, open a repo and receive an update without help.

## Backlog (not scheduled)

O10 submodules, O11 worktrees, O12 Git LFS, O13 built-in terminal, O14 built-in file editor,
O15 AI commit messages, O16 profiles, O17 commit signing, O18 patches.

## Risks and open decisions

| # | Item | Needed by |
|---|---|---|
| 1 | Development and the real-window pass happen on Windows. macOS and Linux are covered by CI builds and the scripted walk-through until the owner tests on a Mac by hand. | Mac testing: when the owner chooses; at the latest phase 8 |
| 2 | The diff viewer and conflict resolver are custom-built; they are the largest UI effort in the project. | Phases 2 and 4 |
| 3 | The real-window pass depends on driving a desktop window from the development tooling. Phase 0 proves it; if it turns out unreliable, the fallback is agreed with the owner before phase 1. | Phase 0 |
| 4 | Hosting checks need a test account or token for each of GitHub, Azure DevOps and GitLab. | Phase 7 |
| 5 | Public sign-in needs app registrations with GitHub, GitLab and Microsoft; personal access tokens work without them. | Phase 7 |
| 6 | Code-signing certificate and Apple Developer membership cost money. | Phase 8 |
| 7 | A built-in terminal (O13) is harder in Avalonia than in web technology. | Backlog |
