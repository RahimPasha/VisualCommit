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
| `test-reports/phase-N.md` | Visual checks: expected results, then outcomes | Before each part's package is built (expectations, D244) and at the end of the phase (outcomes) |

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

The project's practice since phase 3. No rule in `CLAUDE.md` asks for these; phases 3 to 6 each
did them:

- **A design before the checks.** The phase's design is written into `docs/phase-N-design.md`
  before its code and before the report's checks: every git fact derived with real git, the
  texts, the layouts, the tests and harness, the work packages and the design's own decisions,
  which go into `decisions.md`. Independent reviews check it first (phase 5: five reviews, each
  finding checked by an adversarial verifier, status step 3), and the questions of taste they
  raise go to the owner. The work packages are built by sub-agents in git worktrees, contracts
  and stubs first (section 18 of `phase-6-design.md`, the latest model, and section 16 of
  `phase-5-design.md` before it; the phase 3 to 6 handoffs, "Things that cost time"). The file
  is kept as history; its last sections record what the build, the reviews and the gate settled
  (`phase-5-design.md` 15.8 and 15.9; `phase-6-design.md` records it in its sections' "As built"
  notes). The report's checks are reviewed independently too before they are committed. Since
  2026-10-28 the design is shorter and its review lighter, and the checks are written per part
  (D243, D244, below).
- **An independent review of the code before the gate.** Agents that did not build the code
  review it, each from one angle, and every confirmed finding is fixed with tests before the gate
  runs. Phase 3: six reviewers, 49 findings, one of them a data-loss blocker (D123 to D132; the
  phase 3 handoff, "State of the repo"). Phase 4: five reviewers, each finding verified by a
  second agent, 31 confirmed (design 14.5 of `phase-4-design.md`). Phase 5: six lenses (git safety, threads and lifetimes, performance,
  conformance with the report and design, code quality, test quality), each finding verified by
  a skeptic, 43 confirmed and 3 refuted (status step 25; `phase-5-design.md` 15.9; the phase 5
  handoff, "State of the repo"). Phase 6: four reviewers (git safety and data loss, threads and
  lifetimes, integration and design fit, tests and CI robustness), whose findings the lead ruled
  on and four fixers fixed by area, 55 in all (status step 22; the phase 6 handoff, "State of the
  repo"). It is a step of the Progress checklist, before the gate.

How a phase is built since 2026-10-28 (D241 to D245). They come from the review of phase 6's
cost, [review-before-phase-7.md](review-before-phase-7.md): phase 6 cost about $2,720 at API
prices and stopped for 74 hours at the plan's weekly limit.

- **Within the week's allowance (D241).** The owner pays no extra usage. A phase, and each stage
  of one (its design, a part's checks, a wave of packages, the review, the gate), starts only when
  the share of the plan's weekly allowance that is left covers it, judged from the plan's own
  weekly percentage (`/usage`, or the status line's seven-day percentage), not from API prices.
  Each Progress checkpoint in `status.md` records the share of the week used. Phase 7 is split
  into 7a and 7b for this reason (below).
- **Briefs and bounded agents (D242).** The designer writes each work package's section of the
  design as its builder's brief: self-contained, at most about 25 KB. An agent stops at about 150
  calls or 300k tokens of context and writes a hand-off note, and a fresh agent continues from
  the brief and the note. A fixer takes 5 to 6 findings. The lead keeps its own context lean: it
  compacts at stage boundaries, once `status.md`'s Notes hold what it needs, writes long output to
  files and gives long loops to short agents. The mechanical agents run on Sonnet 5.5, as
  `CLAUDE.md`'s Models rule says, and every agent's model is named.
- **The design (D243).** About 150 to 200 KB, phases 3 to 5's size (phase 6's was 775 KB). Its
  decisions are in `decisions.md` only, not copied into it. The owner's rulings are asked before
  drafting, so no edit, revise or consolidate rounds follow. Its git facts are taken on the
  development machine's Git 2.56, with 2.43 and 2.55 from CI noted where versions differ (D248).
  Its review uses 3 or 4 lenses chosen by the phase's risks, git safety and tests/gate always
  among them, and a verifier only for each blocker and major.
- **Checks per part (D244).** A part's visual checks are written and committed just before its
  package is built, still before its UI (the Visual test gate below, step 1).
- **Test runs (D245).** Builders and fixers run the test classes they touch. Whole-project runs
  and the run at the root are the lead's, one at a time on the laptop through
  `scripts/heavy-run.sh`, whose lock makes a second run wait for the first. The full default run
  runs at the gate, and when the lead merges a wave, one at a time. A session's start runs it only
  when the machine changed or the tree holds code that is not pushed (`CLAUDE.md`, "Start of every
  session", step 6). In phase 6 agents ran about 37 whole-project runs in worktrees, several at
  once, beside the lead's full runs, and that load made tests fail.

Branches and commits:

- Each phase is built on its own branch in small commits. Branch names are listed in `status.md`.
- A phase branch is merged into `master` after it passes the visual test gate and the owner accepts it.
- Commits are pushed to `origin` on GitHub (github.com/RahimPasha/VisualCommit): the phase branch
  during a phase, `master` after a merge. No force-pushes.

## Visual test gate

No phase is finished until it passes this gate. Automated unit and integration tests are required
too, but they do not replace it.

1. **Expectations first.** Before the UI is built, every visual check
   listed under the phase is written into `docs/test-reports/phase-N.md` (from
   [test-reports/TEMPLATE.md](test-reports/TEMPLATE.md)) with concrete steps and a concrete
   expected result: what is on screen and, where relevant, the git state. Since 2026-10-28 (D244)
   a part's checks are written and committed just before its work package is built, still before
   its UI (D15), by one writer and one reviewer, instead of all at the start of the phase; the
   report's shared conventions stay at most about 30 KB (phase 6's were 291 KB). Expectations come from
   `requirements.md` and this plan, not from what the finished app happens to show. Checks can be
   added later; changing an expected result afterwards needs a reason in the report.
2. **Scenario repos.** Scripts build small repos with known content for each check (for example,
   two branches set up to conflict), so every run starts from the same state.
3. **Scripted walk-through.** `VisualCommit.VisualTests` (phases 0 to 2; since then projects of
   their own, `VisualCommit.VisualTests.Phase3` and `.Phase4`, for phase 5 `.Phase5` and
   `.Phase5Undo`, and for phase 6 `.Phase6` and `.Phase6Look`, D133, D151, D181, phase 6's design
   17.3) drives the full app UI in
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
   native dialogs, start-up. It needs the desktop unlocked and Windows' high contrast off (the pass
   never changes it, D224), and uses the real mouse while it runs:
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
   "Sessions away from the Windows machine" in `CLAUDE.md`). Before the gate's first counted
   round (a round whose passes stand as the gate if they pass), the new phase's real-window checks
   are run twice on their own, with the owner told and present (D246), once the phase's scripted
   walk-through has saved its pictures on that machine: the real-window checks compare their
   captures with them and fail without them. The two runs are made once, not again before the
   rounds that follow a fix. Phase 6's real-window code never ran before its gate, and two of its
   rounds, about 3.5 hours of laptop time, failed only on S14's Windows Open dialog.
5. **Inspection.** Every screenshot is opened and compared with the expected result in the
   report: layout, text, colours and state. A check passes only when the screenshot shows the
   expected result and the repo's real git state matches it. The practice since phase 3: Opus
   inspector agents each judge a batch (the new phase's pictures by part
   against the report, the earlier phases' pictures, the real-window captures with their scripted
   twins). Earlier pictures are compared rather than read afresh. Phases 3 and 4 opened again
   those whose bytes differ from the lists of the earlier gates (`phase-N-pictures.sha256`, the
   Commands in `CLAUDE.md`). Phase 5, which changed nearly every earlier picture, made reference
   pictures of phases 0 to 4 before its gate (their walk-throughs run on `master` in a worktree,
   and a copy of the last gate's real-window files) and compared them with
   `scripts/compare-pictures.py`, which makes a difference picture per changed picture and a
   summary by window region; the inspectors judged each change against the report's "Changes to
   expected results", and in full where it lay outside those regions (design 15.7 of
   `phase-5-design.md`; the phase 5 handoff, "Running the visual gate"). Since 2026-10-28 (D247)
   an inspector's batch holds at most about 150 pictures (one of phase 6's held 422), and every
   picture is still inspected: none is skipped because a rule explains its change.
6. **Fix and re-run.** Failures are fixed, then the phase's checks and all earlier phases' checks
   are run again. Earlier phases' checks stay in the default test run for this reason. When a
   later phase changes on purpose what an earlier check shows (a placeholder becomes real
   content), that check's test is updated in the same commit, and the change and its reason go
   under "Changes to expected results" in the report of the phase that made it. Both passes run
   on one commit, with one exception (D246): a commit that changes only
   `tests/VisualCommit.RealWindowTests` re-runs only the real-window pass, and the default run of
   the last commit it ran on stands for its own (after two such fixes in a row, still the commit
   of the last default run, not the commit just before), when
   `git diff --stat <that commit> <this one> -- . ':(exclude)tests/VisualCommit.RealWindowTests' ':(exclude)docs' ':(exclude)*.md'`
   prints nothing and the list of pictures written after the round before
   (`phase-N-pictures.sha256`, checked as the Commands in `CLAUDE.md` say, before the pass) shows
   the scripted pictures unchanged; the list is written again after the pass. Phase 6's rounds 3 and 4 changed only `RealApp.cs` and still ran the
   default run again, about 1.8 hours.
7. **Report.** `docs/test-reports/phase-N.md` records the outcome and screenshot file for every
   check. Screenshots stay local under `artifacts/visual/phase-N/` (git-ignored to keep the repo
   small). The report also names the CI run of the gate's commit and which of the screenshots CI
   took on macOS and Linux were looked at, and ends with a short checklist for testing by hand
   on macOS. The session that runs the real-window pass runs `dotnet test` on the same commit
   first (or, by step 6's exception, on the last commit the default run ran on) and inspects the pictures of both
   passes itself: pictures a cloud session inspected
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

Outcome: delivered in full; nothing moves to a later phase, and one follow-up the design proposed
is now listed under phase 6. Design 15.5 and the report's "What proves each Delivers item" map
every item to its checks and tests: 27 checks in parts A, B, F, U and P, 89 test methods in two
new walk-through projects (`VisualCommit.VisualTests.Phase5` and `.Phase5Undo`, D181), and 15 of
the checks repeated in the real window (48 captures). The "Done when" items: any line of any file
is two clicks from its commit through the details panel's "All files" (D164, check B7); every
destructive action of phases 2 to 4 is undone by Undo (U16 to U23) or restored from the backups
list (B9): an abort (D165) and the resolver's binary side and "Delete it" only from the list,
which also holds a skip's changes; no checkout loses uncommitted changes (P24 to P27). The
owner's answers (D153 to D166) and the design's decisions (D167 to D181) set its shape; the
report's own decisions bind where they differ from the design (D183). Known limits, by decision:
Undo reaches back within a session only, at most 100 entries per worktree, and records neither
staging, fetches nor an abort (D154, D165, D167, D168); the reflog page reads one reflog at a
time, HEAD's or a local branch's, up to its newest 500 entries (D180, design 15.9); file history
leaves out merges (D175); search leaves out hidden branches (D174); of the checkouts a user
starts, only that of an older commit of the current branch keeps ignored files, and the others
overwrite them as git does (D179; the phase 6 item). A test that hung under load found that a
status-bar tooltip took the click meant for Cancel; tooltips take no input now (D182). Before the gate, an independent review
of the code (six lenses, each finding verified by a second agent) confirmed 43 findings, 1 a
blocker and 9 major, among them an undo of a discard or a hard reset that deleted without a word a
folder holding files where the snapshot had a file, and an undo of a stash that let its pop
overwrite an ignored file; all were fixed with tests, and the undo history's git calls were cut (a
pop's from 22 to 16; design 15.9, risk 25). The gate's first run, on `4281f4c`, passed both
passes, and the inspection of its 1,080 pictures found five problems, fixed in `1b2e734`: after an
outside commit one refresh drew the new commit without its branch's label (HEAD and the branch were
read side by side); the search bar's disabled Previous and Next looked enabled; seven earlier
pictures showed the two-commit comparison still loading; phase 2's real-window 17c caught a
status-bar tooltip; and the blame header's caption is cut with "…", by design, which the report now
says. Nearly every earlier picture changed on purpose (486 of the 510 earlier scripted pictures, as
the report's "Changes to expected results" lists), and the inspection compared them with reference
pictures made on `master` (design 15.7). The gate passed on `1b2e734`: the default run's 3,233
tests in 43 min 22 s, and the real-window pass of phases 0 to 5 (59 tests) in 24 min 57 s with Q1
at 1,826 ms, run again after a rest when its first attempt, straight after the default run, failed
on Q1 alone (2,613 ms on the hot laptop; risk 10); the 65 files that changed since the first run
(48 pictures and 17 difference pictures) were inspected in full, and the other 1,015 of the 1,080
are byte-identical to the first run's; CI run 38931127856 on that commit succeeded in all five
jobs. What was built and proven is in
[handoffs/phase-5.md](handoffs/phase-5.md), the gate in
[test-reports/phase-5.md](test-reports/phase-5.md).

### Phase 6 — Multi-repo and productivity (M)

Covers C1 (workspaces), O3, O5, O9.

The owner's answers of 2026-10-18, given when the phase started, are D187 to D194; those given the
same day after the design's reviews are D195 to D202.

Delivers:
- Workspaces (repo groups), kept in a file of their own, `workspaces.json` (D189), and a dashboard showing each repo's branch, ahead/behind and uncommitted changes, with bulk fetch and pull. The dashboard is a tab of its own at the start of the tab strip (D187); opening a workspace shows its dashboard only, and a repository opens as a tab when it is opened from there (D188).
- Command palette (Ctrl+Shift+P, Cmd+Shift+P on macOS; the app's commands and the objects they act on, D191) and keyboard shortcuts: a fixed set, shown in the palette, the menus and the tooltips and listed in the settings screen (D192).
- Settings screen (identity, pull mode, auto-fetch interval, external tools) and theme polish. The identity, the pull mode and the merge tool are git's own configuration, global and, in a "This repository" section, the repository's own (D190). The theme can also follow the system (D194).
- From phase 1: a long folder in the welcome page's error wraps at awkward places ("C:" alone on a line, a name split). Nothing is cut off.
- From phase 3: the settings screen covers `AutoFetchMinutes` (default 5, 0 turns auto-fetch off; only in `settings.json` today, and `VISUALCOMMIT_AUTO_FETCH_MINUTES` overrides it, D85), `StashIncludeUntracked` (today the stash dialog remembers its last choice), and the extensions "Open file" asks about before opening a file (a fixed list in `RepositoryOperations.Files.cs`, plus the executable bit on Unix, D130). Its pull mode has to work through git's own `pull.rebase`, `pull.ff` and `branch.<name>.rebase`, which Pull follows (D78, D84), or D78 is reopened with the owner. Its external tools give the file menus an "Open in editor" beside "Open file", which uses the system's application for the file (requirements' "open in editor or file manager").
- From phase 4: the external merge tool is git's own `merge.tool` (D149); without it the resolver's "Merge tool…" and the file menu's "Open in merge tool" are disabled with "Set merge.tool in git's configuration to use a merge tool". The settings screen's Git section is where the user chooses it, through git's configuration, so that `git mergetool` in a terminal and the app use the same tool (Q5).
- From phase 3: the dashboard's bulk fetch and pull run through each repository's `OperationQueue`, as the toolbar's do (D81), with the same titles, Cancel and error dialog. Auto-fetch fetches only the active tab's repository (D79, D85).
- From phase 3: no code reads `remote.<r>.fetch`, so the push of a drop onto a remote branch and a remote branch's "Delete <b> on <r>…" take the branch's name on the remote from its remote-tracking name; with a fetch refspec that maps names differently they act on the wrong name. Read the refspec and set `DropFacts.RemoteMapsBranchesDifferently`, which the drop menu (`RepositoryMenus.ForDrop`) already turns into a disabled item with its reason.
- From phase 5 (design 9.1, 14.2): every checkout keeps ignored files by default (D193, the owner's answer). Today, of the checkouts a user starts, only that of a commit the current branch reaches, other than HEAD's own, passes git's `--no-overwrite-ignore` (D179, D183; undo's switches pass it too, D169); every other checkout follows git's default (Q5), which overwrites an ignored file the target tracks and deletes an ignored folder where the target has a file, without a word (design 0.7). From this phase every checkout passes it, refused checkouts say so with D179's ignored-files text, and a setting, on by default, turns it off. The design proposes which other operations that can overwrite ignored files the setting covers.

Dropped from this list: renaming a branch to a name that differs only in case, which the name
dialog refuses on Windows and macOS because Git 2.36 refuses it for a loose ref. Settled with the
owner on 2026-10-12: it stays refused (D131); two renames in a terminal do it.

Visual checks:
- The dashboard with ten repos in mixed states; bulk fetch progress.
- Command palette search and run.
- Settings changes take effect.
- A sweep of every main screen in both themes.

Done when: ten repos can be checked and updated from one screen, every main action is reachable from the keyboard, and the visual gate passes.

Outcome: delivered in full; nothing moves to a later phase but what the design proposed and the gate
found (below). The report's "What proves each Delivers item" maps every item to its checks and
tests: 34 checks in parts W, K, S, G and L, 47 test methods in two new walk-through projects
(`VisualCommit.VisualTests.Phase6` and `.Phase6Look`, D230), and 8 checks repeated in the real window
(26 captures). The "Done when" items: the ten repositories of the "Web" workspace are read on one
dashboard, and one Fetch all and one Pull all decide every row (W2, W4 to W7; design 17.5's reading);
every main action runs from the keyboard alone, each asserted by what it ran (K12, K13); the gate
passed. The owner's answers (D187 to D194 when the phase started, D195 to D202 after the design's
reviews) set its shape; the design's decisions are D203 to D230, the build's and the review's D231
to D236, the gate's D237, and D238, recorded at the cold read, gives part L's walk-through a server
port of its own. The design's two "From phase 3" items on fetch refspecs
and checkouts became design 10 (D222) and D196's Keep ignored files, as planned. Before the gate, an
independent review of the code by four Opus reviewers (git safety, threads and lifetimes, integration,
tests) gave its findings to four fixers by area (55 in all), among them git's directory-rename
detection writing over an ignored file, a stale error kept by a tab that adopted a dashboard row, and
a mirror fetch line that overwrites local branches (a known issue). The gate ran four rounds on the
Windows machine: the first, on `774464d`, passed both passes, and the inspection of its 1,329 files
found three faults of the app (a refused Save's selected text, a disabled ticked box drawn enabled,
dashboard rows keeping a recycled container's hover), fixed in `d8fd9fb` and `59b2316`; the second and
third failed only S14's Windows Open dialog in the real window, fixed in the harness (`639db4a`,
`b561b7c`); before them two default runs failed on the load of nine projects side by side alone
(D237). The gate passed on `b561b7c`: the default run's 5,037 tests in 56 min 25 s and the real-window
pass of phases 0 to 6 (67 tests) in 27 min 39 s with Q1 at 1,805 ms; the 66 files that changed since
the first round were inspected in full, and the other 1,263 of the 1,329 are byte-identical to it; CI
run 40059391404 on that commit succeeded in all five jobs (one container on its second attempt, a
flaky test before the window's first frame). Moved: the placeholders' contrast and the dark theme's
gaps to phase 8's accessibility pass (D198; the placeholders for the owner to confirm), the activity
log's red `exit 1` of a normal answer and design 16.2's list to the backlog, and four follow-ups to
phase 7 (the push guard's `pr/*` refusals, the prompt group's answers per host, a flaky test and a
stale comment; its "From phase 6" items). What was built and proven
is in [handoffs/phase-6.md](handoffs/phase-6.md), the gate in [test-reports/phase-6.md](test-reports/phase-6.md).

### Phase 7a — Hosting foundation and GitHub (M)

Covers O4 for GitHub, and R3.

Phase 7, "Hosting: GitHub, Azure DevOps, GitLab" (L), was split in two on 2026-10-28 (D241, the
review's note on T11/P9 in [review-before-phase-7.md](review-before-phase-7.md)): this phase
builds the provider model and GitHub on it, and takes every item carried into phase 7; phase 7b
adds Azure DevOps and GitLab. Plan risks 4 and 5 (test accounts, app registrations) are decided
with the owner before this phase's design.

Delivers:
- The provider model that phase 7b builds Azure DevOps and GitLab on.
- Sign-in to GitHub (the review proposes a personal access token, and GitHub's device flow only if the owner registers an app: risk 5, decided before the design); tokens in the OS keychain on Windows, macOS and Linux (R3).
- Browse and clone repos from a GitHub account.
- Pull-request list per repo; create a pull request from the branch menus and from the menu of a branch dropped onto a branch; pull-request badge on branches; open in browser.
- Author avatars.
- From phase 3 (D77, D92): "Create pull request" in the branch menus (`RepositoryMenus.ForLocalBranch`, `ForRemoteBranch`) and in the menu of a branch dropped onto a branch (`ForDrop`), and a menu for the left panel's "Pull requests" section, which has none.
- From phase 3: a provider's token reaches git in one of the two ways git asks for credentials: the credential helpers configured for it, asked first (Git Credential Manager included, Q5), or the app's own prompt path, the askpass helper and `AskPassBroker` (D82), which today answers through the credentials dialog. Auto-fetch never prompts (D79, D82), so where git cannot get the token without a prompt, auto-fetch fails with only "Auto-fetch failed" in the status bar (D85).
- From phase 6 (design 10.8 #8, 10.4): the push guard (`RefuseIfPushStopsFetchAsync` in `RepositoryOperations.Remotes.cs`) refuses some pushes that would not stop the fetch: a new branch such as `pr/fix-typo` under GitHub's `+refs/pull/*/head:refs/remotes/origin/pr/*` line, and any new branch under two pattern lines of one remote that rename into two namespaces. Kept on purpose in phase 6 (a terminal push still works); a pull request's branch is pushed through these paths, so phase 7a decides whether the guard lets such pushes through.
- From phase 6 (design 4.4): a bulk run's prompt group (Core's `Operations/PromptGroup.cs`, the App's `Services/PromptDialogs.cs`) reuses an answer by its prompt text across the run's repositories (a Cancel included) until the run ends; git's text names the URL and the user, but two repositories of one host signed in as different users through one prompt text get the first answer. Accounts per provider make that likelier: phase 7a decides whether the group keeps answers per account.
- From phase 6 (its gate, run 40059391404): `ShellDriverTests.Wheel_reaches_the_control_under_the_mouse_with_the_sign_of_a_real_wheel` (`tests/VisualCommit.App.Tests/ShellDriverTests.cs`) hit-tests before the window's first frame and can find no control on a slow machine; it failed once in the container with the SDK from Ubuntu's packages. It waits for the hit test to find the control.
- From phase 6: the comment at the top of `tests/VisualCommit.RealWindowTests/VisualCommit.RealWindowTests.csproj` gives the pass of phases 0 to 5 as about 25 minutes and phase 6's part as design 17.6's estimate; the pass of phases 0 to 6 took 27 min 39 s at phase 6's gate, which could not change the file after its gate's commit. Corrected with phase 7a's first code commit.
The review's section 5 also suggests for 7a's first package its S8 (budgets of git calls asserted
as upper bounds taken with the watcher quiet), its R8 (a test switch that delays the file
watcher's events, proposal 36) and a look at `Phase6ChecksW`'s check W1, which failed once under
load on 2026-10-26 (its question 11). None of them is approved: they are not part of 7a unless the
owner says so (`status.md`, "Waiting on the owner").

The review's note on T11/P9 has the default run and CI test against a local fake of GitHub's API, with real accounts tried by hand; how the providers are stood in for is settled with risks 4 and 5 before the design.

Visual checks:
- For GitHub: sign-in dialog, repo browser, pull-request list, create-pull-request form, badge on the branch.

Done when: for GitHub, sign-in, clone, list pull requests and create a pull request all work, and the visual gate passes.

### Phase 7b — Azure DevOps and GitLab (M)

Covers O4 for Azure DevOps and GitLab, and R3 for their tokens.

The second half of phase 7 (D241). If its design outgrows a week of the allowance, it is split per
provider (the review's note on T11/P9).

Delivers:
- Azure DevOps and GitLab, self-hosted GitLab included, on phase 7a's provider model: sign-in, tokens in the OS keychain, browse and clone, the pull-request list, create from the branch menus and the drop menu, the badge, open in browser, and author avatars, as 7a built them for GitHub.

Visual checks:
- For each of Azure DevOps and GitLab: sign-in dialog, repo browser, pull-request list, create-pull-request form, badge on the branch, on 7a's screens, once per provider.

Done when: for Azure DevOps and GitLab, sign-in, clone, list pull requests and create a pull request all work, and the visual gate passes.

### Phase 8 — Public release (M)

Covers O19, R1, R2, R4–R7, Q6.

Delivers:
- Installers and auto-update on all three platforms; code signing and notarisation.
- First-run experience: Git detection and guidance; decide whether to bundle Git on Windows.
- Performance pass and keyboard-accessibility pass; the dark theme's text below 4.5:1 on hovered and selected rows and the dark danger button's label (D198; phase 6's design 11.10).
- Third-party notices, user guide.
- Icons and branding; beta release.
- From phase 3: the askpass helper `visualcommit-askpass` (project `src/VisualCommit.AskPass`) is a framework-dependent program that runs on the .NET runtime the app uses (it inherits `DOTNET_ROOT`), and `AskPassLocator` expects it next to `VisualCommit.Git`'s assembly, or in the app's base folder when the assembly has no location (single-file). The installers must ship it in a form that runs there, self-contained packages included; without it every credential and ssh prompt fails.
- From phase 3: OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE`, so when the app was started from a terminal such an ssh still asks there (Ubuntu 20.04 ships 8.2; Git for Windows 2.36 bundles 8.9, and what 2.30 bundles was not checked). The first-run experience says so, or the minimum versions are raised.
- From phase 3: on the Mac, by hand, cancelling a fetch over ssh. Cancel kills only git there (D37); ssh and the askpass helper end when their pipes break, which no automated run proves.
- From phase 6 (design 8.5): a broken `~/.gitconfig` shows its own message. Today `git --version` fails and the app says "Git is not working"; showing git's `fatal: bad config line …` is first-run work.
- From phase 6 (design 11.3, 11.10, 11.11, D198): a palette made for high contrast, and the dark theme's text below 4.5:1, left with the owner's consent: `VcDangerBrush` 3.64:1 on a hovered and 3.76:1 on a selected row, `VcAccentBrush` 4.13:1 on a hovered and 4.26:1 on a selected row (the renamed file's "R"), and the danger button's white label 3.38:1. A lighter dark danger colour also lowers that label (2.70:1 at about `#EA7D86`), so it comes with the dark label `#10121A`, which changes only the label in 35 earlier dark pictures: scripted phase 2 `17a`, `18a`, `19a`, `19c`; phase 3 `04b`, `09f`, `10f`, `11b3`, `12g`, `13i`, `13j`, `16h`, `16k`, `20c`, `22e`, `25c`, `25g`, `29d`, `30d`, `31a`; phase 4 `b9c`, `c13d`, `c13g`, `c13k`, `c14h`, `c16b`; phase 5 `b9i`, `b9m`, `u16i`; real-window phase 2 `17a`; phase 3 `09f`, `10f`, `22e`, `31a`; phase 4 `c16b`.
- From phase 6 (its gate's inspection), for the accessibility pass, the move here for the owner to confirm: every text box's placeholder (the left panel's `Filter` box since phase 1, the palette's, the settings screen's, the Pick dialog's `Filter`) is drawn in `VcTextSecondaryBrush` at Fluent's watermark opacity, 0.5: `#616773` on `#272C36` dark (about 2.5:1), `#ADB2B8` on `#FFFFFF` light (about 2.1:1, lighter than the disabled colour). Phase 6 left it, as raising it would change nearly every picture with a text box.
- From phase 6 (design 15.5), for the accessibility pass: the icon buttons without an automation name (only the page close buttons set one): `ResolverPreviousFile`, `ResolverNextFile`, `ResolverCloseButton`, `ResolverPreviousConflict`, `ResolverNextConflict`, `CloseDiffButton`, `ActivityLogClose`, `SearchPrevious`, `SearchNext`, `SearchClose`, `CompareSwap`, `CompareClose`, `RefHiddenToggle`, `RefSoloToggle`, `DismissRestoreButton`, `RowStageButton`, `RowDiscardButton`, `RowUnstageButton`.
- From phase 6 (design 12.2, 11.9), by hand: the macOS and Linux editor install paths and the macOS native theme observer; the macOS checklist gains design 11.8's by-hand System step.
- From the review before phase 7 (D239): `Directory.Build.targets` copies no native `.pdb` file (all of them SkiaSharp's and HarfBuzzSharp's) and, on Windows in a build without a `RuntimeIdentifier`, only the build machine's platform's files from every package's `runtimes/` folder. A portable publish (no `-r`) must set `-p:VisualCommitAllRuntimes=true`, or a package built on Windows carries only win-x64's libraries; a `dotnet publish -r <rid>` loses only the `.pdb` files.

Visual checks:
- Full regression: every check from phases 1–7b re-run against the installed build.
- Install, first run and update on Windows.
- The complete macOS checklist run by hand by the owner; Linux by hand or through WSL.

Done when: a new user on each platform can install, open a repo and receive an update without help.

## Backlog (not scheduled)

O10 submodules, O11 worktrees, O12 Git LFS, O13 built-in terminal, O14 built-in file editor,
O15 AI commit messages, O16 profiles, O17 commit signing, O18 patches.

Left out of phase 4 on purpose (design 14.4, D143, D148): an interactive rebase that includes the
root commit (`--root`); syntax colours in the resolver's editable result; a splitter between the
resolver's halves; Continue and Skip for `git am`, which the banner leaves to a terminal.

Left out of phase 6 (design 16.2): reordering workspace rows by a drag (design 1.10); "Open in editor" at
a line from blame and the diff (12.1); editing `mergetool.<tool>.path` or `.cmd` and `merge.guitool`
(8.10); branch keys other than `branch.<b>.rebase` (8.8); keys the user can change (D192); a write that
refreshes a stale index (risk 29, the owner's call); undo of a remote's removal under a namespace outside
`refs/remotes/<r>/` (10.8 #1); a fetch line that writes into `refs/heads/` (`git remote add
--mirror=fetch`) overwrites local branches on every fetch, Fetch all and auto-fetch included, as `git
fetch` does, with no backup: skipping or backing up such lines (10.8 #10). Profiles (O16) stay in the
backlog.

Found at phase 6's gate: the activity log draws a read whose exit 1 is git's normal answer as a
failure, as phase 5's rule in `ActivityLogRows` draws every exit code other than 0 in the danger colour;
since phase 6 that includes the refs read's `git config --null --get-regexp ^remote\.` in a repository
without a remote (phase 5's `a2d-show-reads`), as it already did `git symbolic-ref -q HEAD` on a
detached HEAD and a `git config --get` of an unset key. Drawing such a read as a success needs each
read to say which exit codes are its normal answers.

## Risks and open decisions

| # | Item | Needed by |
|---|---|---|
| 1 | The real-window pass happens only on the Windows machine; development can also happen in a Linux cloud session (D39), which then leaves that pass to a Windows session. macOS and Linux are covered by CI builds and the scripted walk-through until the owner tests on a Mac by hand. | Mac testing: when the owner chooses; at the latest phase 8 |
| 2 | Settled in phase 4: the conflict resolver is built on AvaloniaEdit as planned (D64, D140, D148). Its panes are phase 2's `DiffPane`, generalised over `IDiffPaneHost` with phase 2's 66 pictures byte-identical, and its result an editable `ConflictResultEditor`. Still true: it leans on details of AvaloniaEdit 12.0.0 (built against Avalonia 12.0.0, used with 12.1.3) that an update can change: undo and redo go through the document's `UndoStack` with the platform's gestures from `HotkeyConfiguration`, because the editor's own routed bindings are removed and pick the modifier by the operating system; typed text is undone one character per step; the caret is the app's own, because AvaloniaEdit's blink cannot be stopped. `ConflictResultEditorTests`, `ConflictPanesTests` and phase 2's pictures catch a change. The result has no syntax colours (D148). | Whenever AvaloniaEdit or Avalonia is updated |
| 3 | Settled in phase 0: the real-window pass works. The in-repo harness finds the app through Windows UI Automation, clicks with the real mouse and captures the window; phases 0, 1 and 2 together take about 6 minutes, and phase 3 adds more (risk 18). Its comparison passes small differences such as a hover background or a tooltip (phase 1's report; at phase 5's gate a status-bar tooltip in phase 2's 17c), so only the inspection of the pictures catches them. Still true: it needs an unlocked desktop, and the development screen (a work area of 1500×952 logical pixels at 200% scaling) is too small for its 1920×1080 size, so that size is only ever proven by the scripted walk-through unless a larger screen is used. | Each phase's gate |
| 4 | Hosting checks need a test account or token for each of GitHub, Azure DevOps and GitLab. | Before phase 7a's design (D241): GitHub's for 7a, the others for 7b |
| 5 | Public sign-in needs app registrations with GitHub, GitLab and Microsoft; personal access tokens work without them. | Before phase 7a's design (D241) |
| 6 | Code-signing certificate and Apple Developer membership cost money. | Phase 8 |
| 7 | A built-in terminal (O13) is harder in Avalonia than in web technology. | Backlog |
| 8 | Settled on 2026-10-06: the owner made the GitHub repo public, so CI no longer runs on a monthly allowance of minutes (D38). While it was private, phase 0 used the allowance up. Still true: a job that hangs wastes time, so `ci.yml` keeps a time limit on every job and its test step, raised as the tests grow (in phase 4 to 24 minutes for the test step and 32 per job of the three platforms, D152; in phase 5 to 40 and 50, D184; in phase 6 to 55 and 65, D232, when macOS's Test step passed 32 minutes; risk 12), and raised again when the Windows or macOS Test step passes four fifths of its limit, now 44 minutes (D186, D232). At phase 6's gate the Windows Test step took 24 min 37 s and macOS's 23 min 33 s (run 40059391404), but macOS's reached 43 min 12 s in run 40019921605 the same day: the runners' spread comes near the 44. | Settled |
| 9 | The tests are pinned to xunit.v3 3.2.2 because Avalonia's headless test package does not work with xunit.v3 4.x (D24). Check for a newer Avalonia.Headless.XUnit when Avalonia is updated. | Whenever packages are updated |
| 10 | Q1 (the test fails above 2000 ms) had about 0.8 s to spare in the gates' real-window passes up to phase 4 (1002 ms at phase 1's gate, 1058 ms at phase 2's, 1189 ms at phase 3's, 937 ms at phase 4's), but only 0.17 to 0.25 s at phase 5's: 1,749 ms in the first run's pass, which started straight after the default run (the laptop warm), and 1,826 ms in the second run's pass, run after a rest. Measured alone after that rest, it was 1,147 ms. A hot laptop fails a gate by itself: the second run's first attempt, started straight after the 43-minute default run (risk 12), measured 2,613 ms (the pass's only failure), and on phase 3's gate day other runs measured 1438 to 2268 ms. Run alternately on the same machine that day, the phase 2 build needed 1688 to 1911 ms and phase 3's 1618 to 2002 ms: the spread is the machine's temperature, and phase 3 costs no more than that noise. With no branch hidden, phase 5's graph loads with the same git command as phase 4's (design 8.2); whether the rise (1,147 ms alone and 1,826 ms in the pass, against 937 ms in phase 4's pass) is the build or the machine, only the two builds run alternately can tell, and phase 5's gate made no such run. Rest the laptop before the real-window pass, and to tell a slower build from a warm machine, run the old and the new build alternately on the same machine. Most of the time is `git log --date-order`, which reads the whole history before its first commit when the repository has no commit-graph file. A much larger repository, or a slower machine, could need another way to the first page. At phase 6's gate, each pass after a 10-minute rest: 1,264 ms in round 1's pass (`774464d`) and 1,805 ms in round 4's (`b561b7c`), which counts; between them the laptop ran three more default runs and passes, so the spread is again the machine's warmth, and phase 6 adds nothing to Q1's path (design 10.2). | Each phase's gate; whenever the graph or its loading changes |
| 11 | GitHub moves the `ubuntu-latest` CI runner to Ubuntu 26 from 2026-10-19 (an annotation on every run). The Linux job and the set-up script may need attention then, and its git may change (risk 26). By run 40059391404 (2026-10-24, phase 6's gate) the move had not happened yet for this project: `ubuntu-latest` still ran the `ubuntu-24.04` image with Git 2.55.0, and the run still showed GitHub's migration notice. Keep watching. | The first CI run on Ubuntu 26 |
| 12 | The default test run took about 45 minutes at phase 5's gate on the development laptop, about twice phase 4's: 45 min 37 s for 3,227 tests on `4281f4c` and 43 min 22 s for 3,233 on the gate commit `1b2e734`, where the seven test projects, running side by side, ended after 12 min 8 s (phases 0 to 2's walk-through), 26 min 15 s (phase 4's), 29 min 50 s (`VisualCommit.VisualTests.Phase5`), 38 min 19 s (`VisualCommit.Git.Tests`), 41 min 43 s (`VisualCommit.VisualTests.Phase5Undo`), 41 min 46 s (phase 3's) and 43 min 8 s (`VisualCommit.App.Tests`); design 15.6 had expected 30 minutes or more. Phase 4's gate had taken 21 min 52 s for 2,411 tests in five projects (its report gives each project's time), phase 3's 19 min 15 s for 1,756 in four. Phase 5 added two walk-through projects (89 test methods, D181; part U alone is 55 methods and about 170 operations, design 15.8) and records every action for undo, which adds git calls to every operation of every earlier check (risk 25). On CI, on the gate commit, the Windows Test step took 25 min 15 s, macOS's 16 min 13 s, `ubuntu-latest`'s 3 min 26 s and the two containers' about 4 minutes. The Windows Test step had reached its limit of 24 minutes in run 38853641791, so D184 raised the three platforms' limits to 40 minutes for the Test step and 50 per job (amending D152, which had raised them from 14 and 22 in phase 4); when the project is over, the owner decides whether a CI run of 30 minutes or more is reasonable (D134). The run is bound by the processors, which git's processes and the virus scanner keep at about 90 percent, so running more tests side by side only slows each git call; other load or a hot laptop lengthens it. The owner set up the virus-scanner exclusion for the repository folder and `%TEMP%\VisualCommit.Tests` on 2026-10-12. Within that, each test assembly has one headless UI thread and xunit runs the tests of one class one after another, so the longest project sets a floor. Under the run's load every process start grows: `git mergetool`, a shell script that starts over a hundred processes, took 32 to 64 s to start its tool (2.8 s alone), and the two presses of a scripted double-click lay further apart than the 500 ms that make one (design 15.9). Done in phase 3 (D132, D133): the phase 3 walk-through is the project `VisualCommit.VisualTests.Phase3`, the largest test classes are split (`RepositoryOperationsTests` into 12 classes in 4 collections, `ScenarioTests` into 2, the pull-parity tests into 9), shared scenario templates are built once per process, and the hang watchdog leaves a test's wait for them out of its time. Done in phase 4 (D151): its walk-through is a fifth project, `VisualCommit.VisualTests.Phase4`, and its Git-layer and App tests are split by subject as phase 3's are. Done in phase 5: its walk-through is two projects, with every undo check one action per test method (D181); `TempRepo`, which every test project uses, copies a blank git folder made once per process and starts fewer git processes, and the reflog scenario is built with 88 git calls instead of 145 (`7e7d99d`); the headless harness waits 30 s by default (`5bdd1de`); a wait for `git mergetool` to start has its own limit of 120 s and does not count against the hang watchdog (D185); the headless double-click stops the window's input clock for its presses (`HeadlessInputClock`, risk 22); and step 25's review cut the undo history's reads (risk 25). Still possible: scripted checks sharing an app launch where their scenarios allow, keeping the pull-parity templates between runs as the 100k-commit repository is kept, and the walk-throughs in more projects, which helps only where a project's single UI thread, not the processors, sets the length. Checks that waited 5 seconds for an outside change (phase 1's check 18, phase 2's check 20) timed out under load, and checks that looked at the view before it was drawn failed under it (phase 1's checks 12 and 18 during phase 3's gate); since `594f9fb` the outside-change waits are 30 seconds and those checks wait for what they assert, and since phase 4 phase 1's clone checks are held at a fixed percentage instead of racing the clone (D150). Let the laptop rest before a gate's run. At phase 6's gate the run was nine projects and 5,037 tests, 56 min 25 s on the gate's commit (44 min 34 s to 56 min 25 s over four rounds; the projects' ends in CLAUDE.md), with a git call 10 to 15 times as long as alone (phase 5's seven projects: 5 to 8): two runs failed on that alone, a test of five scenarios past the watchdog's 180 s and a shared template past it, and a pop past check 23's 30 s; D237 holds shared preparations to 10 minutes of their own and makes the walk-throughs' waits 60 s, and a test that holds several scenarios' work is split. Fewer tests side by side would lengthen CI, where the runners have fewer processors. During phase 6's build, agents ran about 37 whole-project runs in worktrees, several at once, beside the lead's 4 full runs (about 3.7 hours), and that load made tests fail (the review before phase 7, 2.3). Since D245 (2026-10-28) builders and fixers run only the test classes they touch, whole-project runs and the run at the root are the lead's and go one at a time through `scripts/heavy-run.sh`'s lock, the full default run is the gate's (and a wave's merge's), and a session's start makes one only when the machine changed or the tree holds unpushed code; failures that only load or the whole run shows then come first at the gate. | Each phase's gate |
| 13 | On Linux, .NET's file watcher puts a watch on every folder of the working tree, ignored ones such as `node_modules` included. A very large tree can use up the user's inotify limit, which other programs share; the watcher then logs a warning and changes made outside the app show only after the next refresh. Phase 2 starts the watcher off the UI thread, so it no longer holds opening up. | When watching changes, or Linux users report it |
| 14 | The askpass helper (`src/VisualCommit.AskPass`, D82) is framework-dependent: it runs on the .NET runtime the app uses and must lie where `AskPassLocator` looks. A package that leaves it out, or ships it without a runtime it can use, makes every credential and ssh prompt fail; nothing but a prompt shows it. | Phase 8 |
| 15 | OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE`: when the app was started from a terminal, such an ssh asks there instead of in the app, and the operation waits until it is cancelled (Ubuntu 20.04 ships 8.2). Which OpenSSH the oldest supported Git for Windows (2.30) bundles was not checked. | Phase 8 (first run) |
| 16 | A network operation the user stops can leave lock files, which make every later fetch fail (seen on Git 2.36, D81). After a Cancel the app lists only what a fetch or push can lock (`refs/**`, `packed-refs.lock`, `shallow.lock`, `config.lock` of the git folders), only those unchanged 0.5 s later, and removes one only if its time and length are still the same (D126). A lock that a slow process of another program holds without writing to it for that long would be listed too; the user decides. A network operation stopped by "Quit now" leaves its locks with no report. | When users report locks |
| 17 | Settled in phase 4 (D146): `CHERRY_PICK_HEAD` and `REVERT_HEAD`, refs rather than files under the reftable ref format (Git 2.45 and later), are read in one place (`GitRepository.ReadPseudoRefAsync`): as files, and through git (`rev-parse --verify --quiet --symbolic-full-name`, so that a branch of that name does not answer) only when the repository's configuration names reftable and `MERGE_MSG` or `sequencer/` exists, so a files repository with nothing in progress makes no git call. `PseudoRefTests` run a real reftable repository with a stopped pick on CI's gits from 2.45 on (2.55 and 2.56 since 2026-10-15; on the containers' 2.43 the test asserts that git refuses the option, risk 26); the development machine's Git 2.36 could not make one, so only CI proved it until the machine moved to Git 2.56 on 2026-10-28 (D248). | Settled |
| 18 | The real-window pass grew from about 6 minutes (phases 0 to 2) to 11 min 11 s with phase 3's checks at its gate (11 min 42 s with the build), to 14 min 4 s for phases 0 to 4 (43 tests) at phase 4's gate, which added 10 launches (the design had estimated 5 to 7 minutes more), and to about 25 minutes for phases 0 to 5 (59 tests) at phase 5's gate: 26 min 26 s on `4281f4c` and 24 min 57 s on `1b2e734` (design 15.6 had estimated 8 to 11 minutes more). Every real input waits 400 ms and the first menu of a launch takes up to about 2 s. It takes over the mouse for all that time, needs an unlocked desktop and runs only on the Windows machine, so a phase is closed only by a session there (risk 1). A commit that changes more than docs after an inspection means both passes again (since D246 the real-window pass alone when the commit changes only `tests/VisualCommit.RealWindowTests`, and the new phase's real-window checks run twice before the counted round), so phase 3's gate ran the pass three times, and so did phase 5's (once on `4281f4c`, twice on `1b2e734`, where the first failed on Q1 alone on a hot laptop, risk 10): collect all fixes before running it again, and rest the laptop before it. Shared launches keep it down; later phases add to it. Phase 6's gate: 67 tests (phases 0 to 6) in 25 min 29 s to 27 min 39 s, four times (once on `774464d`, then on `59b2316`, `639db4a` and `b561b7c`): the fixes of the first round's inspection, and twice S14's Windows Open dialog, which the harness now waits for and confirms with Enter, since the dialog opens again in the folder the last run chose a file in (the system folder, whose long file list a search for the Open button walks first). The operating system's dialogs keep such state between runs. | Each phase's gate |
| 19 | Auto-fetch over ssh has no timeout (D125): the only way to set one through git is `core.sshCommand`, which would replace the user's own. A stalled ssh connection holds the repository's queue until the user cancels the auto-fetch from the status bar, which offers Cancel while a user operation waits behind it. HTTP gives up by itself after 60 seconds below 1 byte per second (`http.lowSpeedLimit`, `http.lowSpeedTime`). | When users report it |
| 20 | "Open file" asks before opening a file that can run programs by a fixed list of extensions on every platform, plus the executable bit on Unix (D130, `RepositoryOperations.Files.cs`). Since phase 6 the settings screen adds the user's own extensions to that fixed floor, and Windows' `PATHEXT` is read too (`ToolCatalogues.PathExtensions`); the system's file associations and macOS bundles other than `.app` are not, so a runnable type known only there opens without a question (phase 6's design 12.8). | When a missing type is found |
| 21 | Tooltips are drawn on the window's overlay layer everywhere since phase 3 (D105), so in the real window a tooltip is inside the window and its capture. A phase 0 to 2 real-window picture that showed a tooltip as a window of its own can now differ from the one its gate inspected; the phase 3 report lists such differences under "Changes to expected results". | Phase 3's gate |
| 22 | Phase 3 relies on Avalonia 12.1.3 details that an update can change: the menu adapter's automation peer, which offers Invoke through `MenuBase.Close()` and `MenuItem.ClickEvent` (D105, `ContextMenuAdapterTests` catch a change); Shift+F10, whose gesture list was read on Win32 and headless only, so on macOS and Linux the app raises the request itself if the list lacks it, which CI's headless runs cannot tell apart (D106); and `SecretTextBox`, whose peer hides typed secrets because Avalonia cannot report a password box (D130). Since phase 5 the headless harness's double-click stops the input clock of Avalonia.Headless's internal `HeadlessWindowImpl` (its private `_st`) through reflection, and throws when an update moves it (`HeadlessInputClock`, design 15.9). Phase 6's keys rely on more of them, read from the assemblies' IL only (phase 6's design 0.7): `KeyboardNavigationHandler` taking Tab with any modifier, `TextBox` typing a line break for Enter with a modifier when `AcceptsReturn` is set, and `MenuItemAutomationPeer`'s accelerator key; none of them was checked on a Mac or a Linux desktop (IBus and Fcitx take Ctrl+Shift+E; the macOS application menu). | Whenever Avalonia is updated |
| 23 | Pull is the app's own fetch and integration, chosen as `git pull` would choose it (D84), and since phase 4 also a rebase chosen for one pull, as `git pull --rebase` (D147). Git may change its rules; `PullParityTests` and phase 4's `PullRebaseParityTests` (with its settings and fork-point classes) run on CI's newer git and catch that. Not mirrored: submodule recursion after a pull, and options that exist only on pull's command line. Fetching all remotes runs one `git fetch` per remote, one after another, so `fetch.parallel` no longer applies and many remotes fetch more slowly (D109). Since phase 6 Pull reads its configuration before the fetch (D219), and a rebase chosen for one pull reads `pull.ff` alone, as `git pull --rebase` does (D231); the parity tests compile and compare unchanged, because `PullIntegrationOptions` is optional. | Whenever git or the pull rules change |
| 24 | Settled in phase 4 (D139): the backup refs under `refs/visualcommit/backup/` (D67, D83), which keep their commits and snapshots from git's garbage collection, are cleaned up automatically. A backup (one operation's refs) goes after 30 days, then the oldest while more than 50 remain; nothing goes while an operation is in progress in any worktree, nor the restore bar's snapshot, nor a ref that fits no known scheme. It runs at the end of every operation that saved a backup and in the background when a repository opens, says nothing on screen and logs a line per backup removed, which since phase 5 the activity log shows too (D156). Since phase 5 the clean-up also keeps the backups that the session's undo entries need, which still count toward the 50 (D171), and the backups list restores what is left (D172). | Settled |
| 25 | The undo history's recording costs git calls in every operation it records: the refs and state that the action's kind declares are read before and after the action, inside its own operation (design 7.7, D167). The design expected about two git calls before and after; built, a pop made 22 git calls inside its operation, and a stash 17 in all with the refresh after it (1.2 s alone on the laptop, 3 s on CI). Step 25's review cut them (a pop to 16, a merge from 14 to 10, a hard reset with a local change from 25 to 18; design 15.9 lists each kind): one read of HEAD shared with the operation's own checks, the git folder read instead of the whole status where only an operation in progress matters, a stash's paths read once, and a file's bytes kept only where an undo writes them back. The reads still lengthen every recorded operation, the default run and CI (risk 12); an operation a later phase adds and records adds its own. Phase 6 adds the ignored-files check's reads before each covered merge, pick, revert, rebase and stash apply or pop, and none to a checkout or to the option on a fast-forward: merge 1, pick, revert and a list 2, rebase 5, or 7 with a merge in the range, interactive rebase 3 or 4, squash 3, the stash 1 after D179's count (phase 6's design 9.4 to 9.8); a `--ff-only` pull whose check finds a file adds `merge-base --is-ancestor` (9.10); a directory-rename read only when nothing else is in the way and a test of the disk passes (9.6); and every push of a branch, and undo's and redo's steps that write a branch on a remote, one `git config --null --get-regexp ^remote\.` (10.4). `IgnoredInTheWayTests` and `RepositoryOperationsKeepIgnoredTests` pin the counts. | Whenever an operation is added or its recording changes |
| 26 | The gits on CI drift and differ: they come with the runners' images, not from the project. Since 2026-10-15 Windows runs 2.56.0.windows.2, macOS 2.56.0, `ubuntu-latest` 2.55.0 and the two containers 2.43.0; the development machine runs 2.56.0.windows.4 since 2026-10-28, 2.36.0 before (D248), and has Visual Studio's 2.55.0 for checks by hand (risk 28). No CI job runs a git older than 2.43, so since D248 the oldest git the tests run on is the containers' 2.43, the tests that assert git's own words only on 2.36 assert them on no machine, and neither the app's minimum (2.30) nor the tests' (2.32) is run; for one behaviour, P27's refused merge, the review before phase 5's gate tried gits from 2.32.0 to 2.39.0 (design 15.9). What differs, and what the code and the checks now allow for: a `git stash pop` refused by local changes exits 128 on 2.56 and 1 before (phase 3's check 15b, `281dd69`); a merge refused for something staged writes a no-op entry into HEAD's reflog on 2.38 and later and none before (P27, design 15.9); Git 2.55.0 and Git for Windows 2.56.0.windows.2 ignore a path under `--no-walk`, which 2.36 and macOS's 2.56.0 honour, so the Files search lists the paths itself (design 0.3; a scenario test that took 2.55's behaviour for the rule failed on macOS in run 38738421993); a merge refused for local changes exits 2 with other words on 2.55 (the report's PD10); `read-tree` overwrites ignored files without a word from 2.34 on, and D169's ignored-files guard makes undo the same on every git. A new git on a runner can fail a check that passed the day before, as phase 3's check 15b did in run 38723225901 on 2026-10-15. Phase 6's facts were taken on 2.36 and 2.55 (its design 0.3 to 0.5), and from phase 7a a design's facts are taken on 2.56, with 2.43 and 2.55 from CI where versions differ (D248); its tests that depend on the git version follow the git that runs (`m` read with `--type=bool`, `GlobalFile`, and `FetchRefspecsParityTests`, which leaves out the symbolic `refs/remotes/origin/HEAD` that Git 2.48 and later create from the server's HEAD) or recorded outputs (`--tool-help`, 2.36's `name consists only of disallowed characters`). CI's 2.56 showed two more differences: it refuses a worktree whose branch has no commit with `gitfile does not point to a valid repository`, which the app takes as "not a git repository" (design 0.1), and `blame --abbrev` prints longer hashes, so the tests read `blame -l`. Git 2.55 and 2.56 refuse `git remote add team/fork` beside a remote `team` (`is a subset of existing remote`), which 2.36 and 2.43 allow, so a test writes such names as configuration, as an older git or a hand edit leaves them (run 40004153661). Since the development machine's move to 2.56 its status bar shows `Git 2.56.0`, so every earlier picture that shows the status bar differs in that box from the lists of pictures of phases 1 to 6's gates (phase 1's `01` and `02` in 386 and 376 pixels of a 54×10 box, 2026-10-28); a later gate compares such pictures by region (risk 32). | Every CI run; whenever a runner's git, or the development machine's, changes (risk 11) |
| 27 | Disk space while sub-agents build in parallel worktrees: a built worktree under `.claude/worktrees/` takes about 5 GB on D:, and the tests' repositories, the shared 100k-commit one included, lie in `%TEMP%\VisualCommit.Tests` on C:. During phase 5 the development machine's disks came close to full (under 10 GB free on C:, under 20 GB on D:). Remove a merged worktree at once (section 16 of `phase-5-design.md`, and the phase 3 and 4 handoffs) and look at the free space before starting several worktrees or a gate. In phase 6 a built worktree took about 6.4 GB, and at most 3 were built at once (phase 6's design 18). The review before phase 7 (2.5) found 94.8% of `bin` to be SkiaSharp's and HarfBuzzSharp's native files for 21 platforms with their debug symbols, 558 MB in each of 16 output folders. Since 2026-10-28 the build trims them, with every package's files for other platforms on Windows (D239): measured in the main tree, the 40 `bin` and `obj` folders under `src/` and `tests/` that were built again went from 6.96 to 0.64 GB (a test project's `bin/Debug` from about 589 to about 49 MB), and D: gained 8.60 GB, 2.27 GB of it the 8 folders of outputs nothing local runs, removed and not built again, to 27.2 GB free; the review measured a built worktree with the real-window Release chain at 0.658 GB in place of 6.985 GB, so three worktrees add about 2 GB instead of 17 to 21 GB. NuGet's http cache was cleared the same day (2.8 GB on C:, D240). C: and D: are two partitions of one SSD. | Each phase built in worktrees |
| 28 | Explained on 2026-10-18 (phase 6, step 4): Visual Studio's Git 2.55.0.windows.5 is a trimmed distribution without `git http-backend` (`git: 'http-backend' is not a git command`), which the credential test server `AuthRemoteServer` runs as its CGI through the same git. With that git first on `PATH`, every test against the server fails: 6 of the 49 tests of `RepositoryOperationsFetchTests`, `…PullTests`, `…PullRebaseTests`, `…PushTests` and `…RemotesTests` (the held fetches time out waiting for their percentage, the credentials tests see the server's errors), while the tests against bare remotes on disk pass. Neither the app nor Git 2.55 is at fault; CI's gits ship `http-backend`. Phase 6 makes the server say so when it starts, instead of a test timing out. A local run with that git still says nothing about the HTTP paths; CI does. The tests' isolation removes `MSYSTEM` (phase 6's design 8.12, 17.2); that git also lacks `sleep`, so phase 6's held fetch and phase 5's waiting merge tool do not hold with it first on `PATH`. | Settled; whenever a trimmed git is used for local runs |
| 29 | Under `GIT_OPTIONAL_LOCKS=0` (D47) a stale index makes every status read hash the touched files again: 3.1 s per read in a 20,000-file tree with `core.fscache=true` (6.1 s without) until something writes the index. The dashboard's reads and the tabs' pay it alike (phase 6's design 0.1). Refreshing the index would be a write, which is the owner's call. | When users report slow reads |
| 30 | `remote.<r>.mirror = true` makes every push the app makes to that remote fail with `fatal: --mirror can't be combined with refspecs` (Push, the push dialog, tags, deletions, undo's `RemoteRef`); the error dialog shows git's words (phase 6's design 10.8 #2). | A report |
| 31 | The ignored-files check is the app's, not git's (D196): a window of one process start lies between its `check-ignore` and git's write (phase 6's design 9.12); nothing is checked again on Continue, Skip or Abort (9.14); a rebase onto an upstream that moved back, and git's own range, leave fork-point gaps (9.6); a plain rebase over a merge is refused for a boundary commit's file, which git would keep (9.6); Git 2.30 to 2.33's rules for ignored files were not tried (0.4). | Phase 8 (the git versions the release supports) |
| 32 | Settled at phase 6's gate: it inspected nearly every earlier picture again (the Settings button in every toolbar, the welcome page, the tooltips' colours, menus with keys, the light colours; phase 6's design 16.1), and phase 5's method carried it: reference pictures made on `master` before the gate (`D:/Projects/Personal/gate6-reference/`, outside the repo), the comparison by window region (`scripts/compare-pictures.py`), thirteen Opus inspectors on all 1,329 files of round 1 and two more on the 66 that changed by round 4 (17.7; the phase 6 handoff, "State of the repo"). A later phase that changes earlier pictures in many places does the same (the phase 6 handoff, "Running the visual gate"). | Settled |
| 33 | Two fetches of one common git folder that overlap while the remote has something new lose one of them, with `cannot lock ref '<ref>': is at <new> but expected <old>` or a held `.lock` (phase 6's design 0.2). The bulk runs keep their rows apart, keep off another queue's running network operation and keep auto-fetch off both (4.3). A Fetch, Pull or Push the user starts in a tab, or a row's own (4.7), while another worktree of the repository, or a second tab on the same worktree (3.5), fetches is not kept apart, as in phases 3 to 5: one ends "Fetch failed" (or "Pull failed", "Auto-fetch failed") with git's lines, nothing is left behind and the next fetch works. A cancelled fetch's stale-lock scan covers the common folder, so it can list another worktree's live lock; the stamps (4.6) and the "still exists" check keep its removal harmless. A window-wide fetch gate in every queue would close it. | A report |
