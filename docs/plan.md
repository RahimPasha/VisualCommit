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
3. **Scripted walk-through.** `VisualCommit.VisualTests` drives the full app UI in Avalonia's headless
   mode, with real rendering and simulated mouse and keyboard input (click, right-click,
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

### Phase 3 — Branch, remote, stash and tag operations (L)

Covers C6, C8, C9, C11, Q2, Q3 and the interaction model.

Delivers:
- Checkout, create, rename, delete; merge and fast-forward; reset; cherry-pick; revert.
- Fetch, pull, push, safe force-push, set upstream, manage remotes, auto-fetch.
- Stash save, apply, pop, drop; tag create, delete, push.
- Context menus on every object, double-click actions, drag a branch onto a branch, multi-select commits.
- Operation queue with progress, cancel, and error messages that include git's own output.
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
- From phase 1: a long folder in the welcome page's error wraps at awkward places ("C:" alone on a line, a name split). Nothing is cut off.

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
| 1 | The real-window pass happens only on the Windows machine; development can also happen in a Linux cloud session (D39), which then leaves that pass to a Windows session. macOS and Linux are covered by CI builds and the scripted walk-through until the owner tests on a Mac by hand. | Mac testing: when the owner chooses; at the latest phase 8 |
| 2 | The diff viewer and conflict resolver are custom-built; they are the largest UI effort in the project. | Phases 2 and 4 |
| 3 | Settled in phase 0: the real-window pass works. The in-repo harness finds the app through Windows UI Automation, clicks with the real mouse and captures the window; phases 0 and 1 together take about 3 minutes. Its comparison passes small differences such as a hover background or a tooltip (phase 1's report), so only the inspection of the pictures catches them. Still true: it needs an unlocked desktop, and the development screen (a work area of 1500×952 logical pixels at 200% scaling) is too small for its 1920×1080 size, so that size is only ever proven by the scripted walk-through unless a larger screen is used. | Each phase's gate |
| 4 | Hosting checks need a test account or token for each of GitHub, Azure DevOps and GitLab. | Phase 7 |
| 5 | Public sign-in needs app registrations with GitHub, GitLab and Microsoft; personal access tokens work without them. | Phase 7 |
| 6 | Code-signing certificate and Apple Developer membership cost money. | Phase 8 |
| 7 | A built-in terminal (O13) is harder in Avalonia than in web technology. | Backlog |
| 8 | Settled on 2026-10-06: the owner made the GitHub repo public, so CI no longer runs on a monthly allowance of minutes (D38). While it was private, phase 0 used the allowance up. Still true: a job that hangs wastes time, so `ci.yml` keeps its time limits low. | Settled |
| 9 | The tests are pinned to xunit.v3 3.2.2 because Avalonia's headless test package does not work with xunit.v3 4.x (D24). Check for a newer Avalonia.Headless.XUnit when Avalonia is updated. | Whenever packages are updated |
| 10 | Q1 has about a second to spare on the development laptop (1002 ms at phase 1's gate), but the laptop is slower when hot, and most of the time is `git log --date-order`, which reads the whole history before its first commit when the repository has no commit-graph file. A much larger repository, or a slower machine, could need another way to the first page. | Whenever the graph or its loading changes |
| 11 | GitHub moves the `ubuntu-latest` CI runner to Ubuntu 26 from 2026-10-19 (an annotation on every run). The Linux job and the set-up script may need attention then. | The first CI run after 2026-10-19 |
