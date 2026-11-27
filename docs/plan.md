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

The project's practice since phase 3. No rule in `CLAUDE.md` asks for these; phases 3 to 8a each
did them:

- **A design before the checks.** The phase's design is written into `docs/phase-N-design.md`
  before its code and before the report's checks: every git fact derived with real git, the
  texts, the layouts, the tests and harness, the work packages and the design's own decisions,
  which go into `decisions.md`. Independent reviews check it first (phase 5: five reviews, each
  finding checked by an adversarial verifier, status step 3), and the questions of taste they
  raise go to the owner. The work packages are built by sub-agents in git worktrees, contracts
  and stubs first (sections 0 and 1 of `phase-8a-design.md`, its section 1 built first by the lead
  as the phase's first package, WP0, then a brief per package, the latest model, with
  `phase-7b-design.md` and `phase-7a-design.md` before it, and section 18 of `phase-6-design.md`
  and section 16 of `phase-5-design.md` before those; the phase 3 to 8a handoffs, "Things that
  cost time"). The file is kept as history; its last sections record what the build, the reviews
  and the gate settled
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
  repo"). Phase 7a: five Opus reviewers, 18 findings, six skeptics refuted 3, and four fixers by
  area fixed the 15 and one that CI found (status step 17; `phase-7a-design.md` 19). Phase 7b: six
  Opus reviewers by subject (tokens, git, lifetimes, providers, screens, tests and CI), 19 findings,
  Opus refuters let 9 stand, and four fixers by area fixed six of them with tests (status step 17;
  D391, D392; `phase-7b-design.md` 16.3). Phase 8a: ten reviewers by area and three across the
  phase, over the 35 items the notes had kept for the review, a verifier for each major and one per
  reviewer for its minors: 48 findings, 43 confirmed, five of them major, and five refuted; 38 were
  fixed in eight packages, each read by an independent reader, and three more rounds answered what
  the fixers and readers raised (status step 19; `phase-8a-design.md` 13.1, rulings 99 to 103;
  D462 to D466). It is a step of the Progress checklist, before the gate.

"Status step N" in this file, in `architecture.md` and in a phase's design, report, handoff and
decisions is step N of that phase's Progress checklist. Each acceptance resets `master`'s `status.md` (CLAUDE.md's "Phase N
accepted"), so the checklist, the Notes and the runs of the CI section that a phase's docs cite live
in `status.md` on the phase's branch, the second parent of its merge: "phase 7b's `status.md`" is
`git show origin/phase/7b-hosting-azure-gitlab:docs/status.md`, phase 7a's
`git show origin/phase/7a-hosting-github:docs/status.md`, and phase 8a's
`git show origin/phase/8a-release-readiness:docs/status.md`.

How a phase is built since 2026-10-28 (D241 to D245). They come from the review of phase 6's
cost, [review-before-phase-7.md](review-before-phase-7.md): phase 6 cost about $2,720 at API
prices and stopped for 74 hours at the plan's weekly limit.

- **Within the week's allowance (D241).** The owner pays no extra usage. A phase, and each stage
  of one (its design, a part's checks, a wave of packages, the review, the gate), starts only when
  the share of the plan's weekly allowance that is left covers it, judged from the plan's own
  weekly percentage (`/usage`, or the status line's seven-day percentage), not from API prices.
  Each Progress checkpoint in `status.md` records the share of the week used. A session cannot
  read `/usage` itself (phase 7a's checkpoints that record the week, its steps 2, 3 and 16, say
  "share not read", the first two with the owner's word that enough was left before the reset),
  so it asks the owner for the share at the phase's start and before each stage, and records the
  answer. The allowance resets on Thursdays at 2:00 (phase 7a's Progress,
  `git show origin/phase/7a-hosting-github:docs/status.md`). Phase 7 is split into 7a and 7b for this
  reason (below). For phase 7b, until the reset of Thursday 2026-11-05, the owner lets the work use
  all that is left of that week and wait for the reset if it runs out (D344): until then its stages
  start without that check, and the session commits and pushes at every working state; after it,
  D241's check applies again unless the owner extends D344. For the week from that reset the owner
  gave the phase about all of its allowance (2026-11-05), and its checkpoints record "share not
  read" (the Progress of phase 7b's `status.md`, `git show origin/phase/7b-hosting-azure-gitlab:docs/status.md`).
  Phase 8 is split into 8a and 8b for the same reason (D399). Phase 8a's start runs through the
  reset of Thursday 2026-11-19 without that check until its design is reviewed (D415: under 50 %
  of the old week was used at 22:00 on 2026-11-18); from the stage after, D241's check applies
  again unless the owner extends D415. After the design's review the lead asked before each
  stage: at wave 2's close (2026-11-20, 70 to 85 % used) the work ran only that close's runs and
  the comparison of its pictures and stopped; the owner then gave all of wave 3 (2026-11-21) and
  all of wave 4, the review before the gate with it (2026-11-23; "Waiting on the owner" in phase
  8a's `status.md`).
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
  development machine's Git 2.56, with 2.43 and 2.55 from CI noted where versions differ (D248;
  since 2026-10-29, phase 7a's first CI run 40848974456 on, no job runs 2.55, so only the
  containers' 2.43 differs, risk 26; since phase 8a the floor's job runs 2.34.1, the app's minimum,
  D406 and D420, and phase 8a's design took what differs there from that job's first run,
  44479788558). Phase 8a's design was 305 KB as drafted and 356 KB after its review, over this
  size as phase 7a's and 7b's were (phase 8a's status step 3).
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
   `.Phase5Undo`, for phase 6 `.Phase6` and `.Phase6Look`, for phase 7a `.Phase7a`, for phase 7b
   `.Phase7b` and for phase 8a `.Phase8a`, D133, D151, D181, D416, phase 6's design 17.3, the
   designs 0.3 of phases 7a, 7b and 8a) drives the full app UI in
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
   rounds, about 3.5 hours of laptop time, failed only on S14's Windows Open dialog. Since phase 8a
   the pass also needs `winget` on the PATH, or its launches 7 and 8 fail (D449, D453); it starts a
   second process of the app beside the first, and F4's Open dialog leaves entries in the user's
   lists of recent files, as phase 6's S14 does; and the harness finds every window by its handle,
   from Windows' own list of windows, not by a search of the desktop through UI Automation, which
   asks every program on the desktop (phase 8a's design 13.1, ruling 105).
   Since phase 7a the gate also runs the opt-in live command against real GitHub, and since phase
   7b against Azure DevOps and GitLab too (`tests/VisualCommit.LiveHostingTests`, D250, D346),
   which acts on the owner's sandbox repositories with their tokens, so the owner is told first; it
   takes no mouse and writes nothing under `artifacts/visual`. It runs once before the gate's
   pictures, and what it finds about the services' answers goes into their fakes, committed before
   the passes; then once on the counted round, after the real-window pass and the list of pictures
   (D328, D335). The phase's report records each provider's `run.txt` in a "Live command" section.
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
   `phase-5-design.md`; the phase 5 handoff, "Running the visual gate"). Phase 8a's contrast
   round, which by its scan changes 1,333 of the 1,350 pictures of phase 7b's counted round, scanned
   those pictures for each fix's reach before its checks were written (`scripts/scan-contrast.py`)
   and compared wave 1's whole run with them through `compare-pictures.py --expected`, whose boxes
   tell the changes a merge is meant to make from the rest (D419); each later close compared its
   pictures with the references of the close before, and the pictures of step 19's second whole
   run, read and found without fault, became the gate's references (phase 8a's design 13.1,
   ruling 109). Since 2026-10-28 (D247) an inspector's batch holds at most about 150 pictures
   (one of phase 6's held 422), and every picture is still inspected: none is skipped because a
   rule explains its change.
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
   prints nothing, `git diff --stat <that commit> <this one> -- LICENSE THIRD-PARTY-NOTICES.md notices`
   prints nothing too (the app embeds those files since phase 8a's About, and `*.md` leaves two
   of them out of the first command, D433) and the list of pictures written after the round before
   (`phase-N-pictures.sha256`, checked as the Commands in `CLAUDE.md` say, before the pass) shows
   the scripted pictures unchanged; the list is written again after the pass. Phase 6's rounds 3 and 4 changed only `RealApp.cs` and still ran the
   default run again, about 1.8 hours. A second exception (D335): a commit after the counted round
   that changes only `tests/VisualCommit.LiveHostingTests` re-runs only the live command, on that
   commit, and the gate's commit stays the passes' commit; neither pass builds or references that
   project. A correction to the fake, which both passes see, starts a new counted round.
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
adds Azure DevOps and GitLab. Plan risks 4 and 5 (test accounts, app registrations) were decided
with the owner on 2026-10-28, before this phase's design (D249, D250); the owner's other answers
of that day, before the design, are D251 to D254, and those given after the design's research,
still before its drafting, D255 to D263: github.com only (D258), one account at a time (D263),
signed in and out in the settings screen's new Accounts section (D256), no request to GitHub while
none is signed in (D257), and the app's own git calls to github.com taking the account's token in
place of the user's credential helpers (D255, an exception to Q5).

Delivers:
- The provider model that phase 7b builds Azure DevOps and GitLab on.
- Sign-in to GitHub (github.com, one account at a time; D258, D263) with a personal access token (D249; no app registration, no device flow), in the settings screen's Accounts section (D256); tokens in the OS keychain on Windows, macOS and Linux (R3), and for the session only where no keychain can be reached (D253); the macOS and Linux stores tested on CI against throw-away keychains (D259).
- The account's token used by the app's own git calls to github.com over HTTPS, through the app's own credential helper in place of the user's helpers for that host, while the account's "Use for git over HTTPS" switch is on (D255).
- Browse and clone repos from a GitHub account (the clone form's "From GitHub…" while an account is signed in, D261).
- Pull-request list per repo, in the left panel's "Pull requests" section with a menu (open in browser, check out, copy link; D251); create a pull request from the branch menus and from the menu of a branch dropped onto a branch; pull-request badge on branches; open in browser.
- Author avatars, from GitHub only; other authors and repositories look as before (D252).
- An opt-in command, Windows-only and never in the default run or CI, that signs in, clones, lists pull requests and creates one on real GitHub with the owner's fine-grained token for a sandbox repository, read from an environment variable (D250).
- From phase 3 (D77, D92): "Create pull request" in the branch menus (`RepositoryMenus.ForLocalBranch`, `ForRemoteBranch`) and in the menu of a branch dropped onto a branch (`ForDrop`), and a menu for the left panel's "Pull requests" section, which has none.
- From phase 3: a provider's token reaches git in one of the two ways git asks for credentials: the credential helpers configured for it, asked first (Git Credential Manager included, Q5), or the app's own prompt path, the askpass helper and `AskPassBroker` (D82), which today answers through the credentials dialog. Auto-fetch never prompts (D79, D82), so where git cannot get the token without a prompt, auto-fetch fails with only "Auto-fetch failed" in the status bar (D85). Settled on 2026-10-28 by D255: for github.com with a signed-in account, the app's own helper in place of the user's helpers.
- From phase 6 (design 10.8 #8, 10.4): the push guard (`RefuseIfPushStopsFetchAsync` in `RepositoryOperations.Remotes.cs`) refuses some pushes that would not stop the fetch: a new branch such as `pr/fix-typo` under GitHub's `+refs/pull/*/head:refs/remotes/origin/pr/*` line, and any new branch under two pattern lines of one remote that rename into two namespaces. Kept on purpose in phase 6 (a terminal push still works); a pull request's branch is pushed through these paths, so phase 7a decides whether the guard lets such pushes through.
- From phase 6 (design 4.4): a bulk run's prompt group (Core's `Operations/PromptGroup.cs`, the App's `Services/PromptDialogs.cs`) reuses an answer by its prompt text across the run's repositories (a Cancel included) until the run ends; git's text names the URL and the user, but two repositories of one host signed in as different users through one prompt text get the first answer. Accounts per provider make that likelier: phase 7a decides whether the group keeps answers per account.
- From phase 6 (its gate, run 40059391404): `ShellDriverTests.Wheel_reaches_the_control_under_the_mouse_with_the_sign_of_a_real_wheel` (`tests/VisualCommit.App.Tests/ShellDriverTests.cs`) hit-tests before the window's first frame and can find no control on a slow machine; it failed once in the container with the SDK from Ubuntu's packages. It waits for the hit test to find the control.
- From phase 6: the comment at the top of `tests/VisualCommit.RealWindowTests/VisualCommit.RealWindowTests.csproj` gives the pass of phases 0 to 5 as about 25 minutes and phase 6's part as design 17.6's estimate; the pass of phases 0 to 6 took 27 min 39 s at phase 6's gate, which could not change the file after its gate's commit. Corrected with phase 7a's first code commit.
- From the review before phase 7 (its section 5; the owner's yes of 2026-10-28, D254), in 7a's first package: S8, tests that pin counts of git calls assert upper bounds taken with the watcher quiet; R8 (proposal 36), a test switch that delays the file watcher's events by 1 to 2 s; and a look at `Phase6ChecksW`'s check W1, which failed once under load on 2026-10-26 ("read 7 repositories" where 8 were expected; the review's question 11), and its fix.

The default run and CI test against a local fake of GitHub's API, and real GitHub is tried by the opt-in command above (D250), as the review's note on T11/P9 proposed.

Visual checks:
- For GitHub: sign-in dialog, repo browser, pull-request list, create-pull-request form, badge on the branch.

Done when: for GitHub, sign-in, clone, list pull requests and create a pull request all work, and the visual gate passes.

Outcome: delivered in full; nothing moves to a later phase but what the build, the review and the gate
left (below). The report's "What proves each Delivers item" maps every item to its checks and tests:
52 checks in parts A, B, P, G and N, 27 check methods in a new walk-through project
(`VisualCommit.VisualTests.Phase7a`, 205 tests with its view-model, menu and view classes; design 0.5,
0.6; D334), a new project for the hosting layer's tests (`VisualCommit.Hosting.Tests`, D334), and 11
checks repeated in the real window in five launches, one per part (D319;
nine captures with their difference pictures). The "Done when" items: signing in,
cloning, listing pull requests and creating one work against the fake of GitHub in parts A, B, P and N,
and against real GitHub in the opt-in live command, which passed with the owner's token on the private
sandbox at status step 16 and again, after D342's corrections, as the gate's run before its pictures
(design 20; the report's "Live command") and on the counted round; the gate passed on `a2c312a`. The owner's answers (D249 to D264 before the design, and
those to its review, recorded among its decisions) set its shape; the design's decisions are D265 to
D338, those of the review before the gate and the runs after it D339 to D341, the live run's D342 and
the gate's D343. The two choices phase
6 left to 7a: on a remote recognised as GitHub the push guard counts a `refs/pull/<x>/head` source only
when `<x>` is all digits, so a branch such as `pr/fix-typo` is pushed, and the refusal for two
namespaces stays; a bulk run's prompt group keeps its answers per account and forgets them after a
refused row (design 0.2). Known limits, by decision: github.com only, one account at a time and no
device flow (D249, D258, D263; the backlog); on git older than 2.31 the account is not used for git
(D277), and on 2.31 to 2.45 a user's netrc entry answers a bound call before the app's helper (D340,
risk 35); on macOS any process of the user can read the token through `security` (risk 34). Before the
gate, an independent review of the code by five Opus reviewers (the token's and the account's safety,
git's safety, threads, lifetimes and performance, the design's fit and GitHub's behaviour, tests and
CI) found 18 problems; six skeptics refuted 3, and four fixers by area fixed the other 15, two of them by a comment
or a pinning test only, and one that CI found (design 19), among them a repository renamed on GitHub
that broke its badges, checkout and Create, and a user's netrc that answered a bound call before the
app's helper. The live command's first run with the token found two faults of the app that the fake's
answers, written from memory, had hidden: GitHub's refusal of the API version read as an unreadable
answer, and one commit GitHub lacks stopping every later avatar lookup for good (D342). The gate ran on the Windows machine from
2026-10-30. Two rested default runs of its counted round stopped on the hang watchdog alone, each in a
slow test, not a hang: D237's rule split 11 of `VisualCommit.Git.Tests`' 41 tests of 25 s or more and
kept 30 (design 21 item 1), and the owner chose D343, the laptop's whole run with at most four projects
at once. D246's two runs of 7a's real-window checks, the owner present, failed once on a check's own
lookup and then passed 5 of 5 (design 21 item 3). The third default run passed (5,946 of 5,950, 4
skipped, in 1 h 8 min 39 s), but CI's Windows job on the same commit found a fault of the app: the
stage panel never read a stopped commit's message again once a status read had come before git wrote
it. It was fixed in `a9d02c1`, with the other failure, a test that asserted the banner a torn read
showed (design 21 item 4).
The counted round: the default run on `a9d02c1`'s code passed 5,948 of 5,952 (4 skipped) in 1 h 12 min
18 s; its first real-window pass failed W1 on the harness's read of a closing dialog's title, fixed in
`a2c312a`, and Q1 at 2,456 ms while other programs' scheduled tasks ran at 2 am (design 21 item 5), and
the pass on `a2c312a`, after a rest, passed 72 of 72 in 31 min 6 s with Q1 at 1,400 ms (D246: only
the real-window project changed); the live command passed 112 of 112; 16 Opus inspectors passed every
one of the list's 1,432 files and 45 of CI's pictures (D247). The gate's commit is `a2c312a`, and CI's
run 41190572421 on it is green on all five jobs.
Moved: two observations to phase 8's keyboard-accessibility pass, the macOS store's access list to
phase 8's code signing, the netrc note to the git versions phase 8's release supports and the real
keychains and the browser in the app's own process to phase 8's checks by hand (risk 41), all among
its "From phase 7a" items, and what the design left out to the backlog; left to the owner: a sign-in
dialog in a bound tab (risk 39) and the banner's brief state while a terminal's merge writes (risk 40).
What was built and proven is in [handoffs/phase-7a.md](handoffs/phase-7a.md), the gate in
[test-reports/phase-7a.md](test-reports/phase-7a.md).

### Phase 7b — Azure DevOps and GitLab (M)

Covers O4 for Azure DevOps and GitLab, and R3 for their tokens.

The second half of phase 7 (D241). If its design outgrows a week of the allowance (judged from the
share the owner gives, Workflow above), it is split per provider (the review's note on T11/P9).
Such a split is a decision that amends D241 and names the phases and their branches; the same
commit changes `CLAUDE.md`'s counting of the phases ("Start of every session", step 5), which runs
6, 7a, 7b, 8.

The owner answered what 7a's decisions left open for it (the phase 7a handoff, "For the next
phase") on 2026-10-31, after 7a's acceptance and before this phase's start, by accepting the
lead's recommendations: personal access tokens and no app registration (D345, risk 5); a local
fake of each provider's API, and the live command extended to both providers in the same project,
`tests/VisualCommit.LiveHostingTests`, so that D335's exception holds as written, each token
reaching its sandbox alone (D346, risk 4); Azure DevOps Services and gitlab.com
only, with Azure DevOps Server and self-managed GitLab in the backlog (D347); one account per
provider (D348); avatars from GitHub alone (D349); D255 and D257 for the new hosts (D350); GitLab's
"merge request", and 7a's GitHub-worded screens and commands naming the provider they act on,
which changes 7a's pictures (D351, risk 32). The phase may use all that is left of the week's
allowance until the reset of 2026-11-05 (D344).

Delivers:
- Azure DevOps (Azure DevOps Services: `dev.azure.com` and `<organization>.visualstudio.com`) and GitLab (gitlab.com) on phase 7a's provider model (D347): sign-in with a personal access token (D345), one account per provider (D348), tokens in the OS keychain, the account's token on the app's git calls to the remotes it covers, for Azure DevOps its own organization's (D350), browse and clone, the pull-request list (GitLab's merge requests, D351), create from the branch menus and the drop menu, the badge, open in browser, as 7a built them for GitHub. Author avatars stay GitHub's alone (D349).
- 7a's screens and commands that name GitHub name the provider of the account or remote they act on (D351).
- A local fake of each provider's API for the default run and CI, and the opt-in live command extended to Azure DevOps and GitLab (D346).

Visual checks:
- For each of Azure DevOps and GitLab: sign-in dialog, repo browser, pull-request list (GitLab's merge requests), create-pull-request form, badge on the branch, on 7a's screens, once per provider.
- The Accounts section with an account of each provider, and 7a's screens and commands with their new wording (D351).

Done when: for Azure DevOps and GitLab, sign-in, clone, list pull requests and create a pull request all work, against their fakes and against the real services (the live command, D346), and the visual gate passes.

Outcome: delivered in full, as one phase (D365); nothing moves to a later phase but what the owner's
answers, the design, the review before the gate and the gate left (below). The report's "What proves
each Delivers item" maps every item to its checks and tests: 45 checks in parts A (14), B (8), P (14)
and N (9), 29 check methods in a new walk-through project (`VisualCommit.VisualTests.Phase7b`, 179
tests with its view-model, menu, view and expectation classes; design 0.3, 0.5), the new providers'
classes in `VisualCommit.Hosting.Tests`, `VisualCommit.Git.Tests` and `VisualCommit.App.Tests`, and 11
checks repeated in the real window (N1 in part) in five launches, two per provider and one for the
three accounts (design 14.2, ruling 29; 13 captures with their difference pictures). The visual checks:
for each new provider the sign-in dialog (A5, A6), the repository browser (B3), the list of pull or
merge requests with the badge (P1, P14), the create form (N2) and the request created (N6), on 7a's
screens; the Accounts section with an account of each provider (A2); 7a's screens and commands in
their new wording, GitHub's in 7a's words (D351, D362), which changed only the intro of 7a's 23 part A
pictures and of 3 of its real-window ones (D353; the report's "Changes to expected results"). The
"Done when" items: signing in, cloning, listing pull or merge requests and creating one work against
the fakes of Azure DevOps and GitLab in parts A, B, P and N and in the lagged list's classes (design
0.4), on the laptop and in CI's five jobs, and against the real services in the live command: stage 1's
`Check_2` per provider (2026-11-05, and GitLab's again on 2026-11-06 with D385's token), stage 2's
app-driven `Check_3` (2026-11-08), the gate's run before its pictures and the counted round's (design
15; the report's "Live command"); and the whole visual gate passed on the Gate commit, `cb9af6d`
(below).
The owner's answers (D344 to D351 before the start, D352 at it, D353
to D356 on the research's questions of taste, D373 to D376 and D367's amendment of D252 and D349 on the
design) set its shape; the design's decisions are D357 to D372, its review's D377 and D378, the build's
D379 to D390, the review before the gate's D391 and D392, the owner's choice for the live command's
GitLab guard D393, the gate's D394 and D395, and the close's D396 (from the cold read). Phase 7a's Known issue, `scripts/heavy-run.sh`'s header
comment, was fixed in the first code commit (L1, `a88b84d`).
Fourteen work packages in five waves (design 0.4), each reviewed before its merge, the core, the
credentials, the clients, the session and the four parts also hunted by adversarial agents. What they
and CI found changed the design in places: wave 1's adversarial checks found remote URL spellings that
gave the token past the bindings' `Select` (percent-encoded hosts, `http://`, `www.` and `.git`-less
redirects, white space in a user part, hosts outside ASCII, git's `<name>::` remote helpers) and .NET's
exception messages that put a proxy's credentials or a server's bytes into the log (D379 to D382);
WP-S's hunts, a replaced account's list shown under the new account (D383); WP-N's hunt, a Create
pressed right after a sign-in that pushed without the token on every provider, 7a's GitHub included
(D388); CI, git's console host at normal priority on Windows (D386) and Homebrew's git on macOS, which
loses a URL's user after the first 401 (D389, risk 43). The live command's stage 1 found that a GitLab
fine-grained token needs Project: Read among its user permissions (D385) and eleven Azure DevOps answers
that differed from its fake; WP-LF put what it found into the fakes before part A's checks were written,
keeping git's `info/refs` refusals as differences (design 16.1). Before the gate, an independent review
of the code by six Opus reviewers by subject (tokens, git, lifetimes, providers, screens, tests and CI)
found 19 problems; Opus refuters let 9 stand, all minor but one, a clone's bindings that missed git's
conditional includes (D391). Six were fixed with tests that fail without them (merged at `6333de6`),
design 0.6's timing report was taken (no test needed D237's split), phase 2's commit boxes went to
phase 8 and GitLab's rate limit became risk 44 (D392).
The gate ran on the Windows machine on 2026-11-08, the owner at the machine. The live command's run
before the pictures, on `ee0aeeb`, passed 595 of 596: GitLab's `Check_2` stopped twice on stalls of the
network to gitlab.com and passed alone the third time (risk 42). Its probes found that github.com now
answers a commit it lacks beside `author` with 404, not D342's 500; the fake of GitHub was corrected
(D394), and the review of that fix found that a fork's tab with one stale branch then lost every later
author's picture on the fork in every session, D342's defect back, fixed by one read of the repository
after such a 404 (D395, the owner's approval); both were merged at `cb9af6d` before any picture (the
report's "Found by the gate"). D246's two runs of 7b's real-window checks, the owner present, passed 5
of 5 each. The counted round, both passes on `cb9af6d`'s code: the default run passed 7,888 of 7,892
tests (4 skipped) in 1 h 31 min 48 s at four projects at once (risk 12); the real-window pass, started
straight after it, 77 of 77 in 33 min 35 s with Q1 at 1,827 ms (risks 10, 18); the live command 596 of
596, with nothing new for the fakes. Of the list's 1,551 files, 1,112 are the same files as the
references made on `master` (design 0.7), 320 changed and 119 are new (risk 32); 19 Opus inspectors
opened every file of the list in batches of at most 150 (D247): phase 7b's 93 scripted pictures in four
by part, its 26 real-window files in one, phases 0 to 7a's 1,057 scripted pictures in nine and their 375
real-window files in four, and 45 of CI's pictures in one. 1,594 of the 1,596 passed and none failed;
the two in doubt, phase 5's `a2g-merge-output-opened` and its real-window difference picture, where the
undo recording's two reads swapped places in the activity log, three Opus skeptics judged no defect, as
the two reads start together and their order is a race (the report's "Run").
The Gate commit is `cb9af6d`, the code both passes ran on: the branch stood at `af9b563` for the
default run and at `3dc0887` for the rest, both changing only docs after it. CI's run on that code,
42522049151 on `9b044af`, passed in all five jobs: Windows ran 7,892 tests with 4 skipped, macOS,
Linux and the two containers 7,889 with 3 skipped (risk 8).
Moved to phase 8: phase 2's commit boxes, which stay editable while "Committing…" runs (D392); the new
hosts' netrc entries, in its item on the git versions the release supports (risk 35); the new
providers' tokens on macOS, in its code-signing item (risk 34); and the new providers' sign-ins in the
real keychains and the links that open the browser, its "From phase 7b" checks by hand (risk 41). To
the backlog: what the owner's answers, the design and the review before the gate left out (below).
Left to the owner: risk 39's sign-in dialog in a bound tab, now for three providers and wider on
dev.azure.com, risk 40's banner, and whether to harden the app against a git that loses a URL's user
(risk 43). Recorded as risks: GitLab's rate limit against the first lists (44), what the bindings
cannot see (45), phase 6's palette test that fails rarely on CI (46) and D386's one open case (47).
What was built and proven is in [handoffs/phase-7b.md](handoffs/phase-7b.md), the gate in
[test-reports/phase-7b.md](test-reports/phase-7b.md).

### Phase 8a — Release readiness in the app (M)

Covers R4, R5, R6 and R7, and Q6 for what the app itself shows.

Phase 8, "Public release" (M), was split in two on 2026-11-19 (D399, the owner's answer of
2026-11-09): this phase is the work inside the app, which needs no signing account, certificate or
published release, and phase 8b is the packaging. The owner's answers before its design are D400 to
D415 (2026-11-18 and 19). Of them 8a builds D405 and D406 (the first run and the git floor), D407
(the icon), D408 (the guide), D409 (the About screen), D410 and D411 (contrast and accessibility)
and D412 (the name's search and note); D404's note went into the README with the split; D413 and
D414 close older questions, and D415 covers the phase's start. Two workflows of 2026-11-18 prepared
them: seven readers and a critic mapped what the repository holds for phase 8, and six researchers
with three skeptics checked the release questions on the web. Their briefs are in
`D:/Projects/Personal/phase8-work/research/`, outside the repository (`repo-*.md`, `web-*.md`);
`repo-owed.md` is the inventory of every item an earlier document left for phase 8, each with its
source, and `repo-critic.md` says what the readers missed. 8a's design carries what it uses of them
into the repository.

Delivers:
- First-run experience (R4, D405): Git detection and guidance when git is missing, too old or not working, each state with its own text; "Check again" without a restart; "Choose git.exe"; on Windows, after a confirm, a winget install of Git for Windows in a visible terminal where winget exists; the Command Line Tools or Homebrew on macOS and the distribution's package on Linux. No git is bundled. A missing askpass helper shows there too: today it is only an Error in the log.
- The git floor (D406, D420): `GitVersion.Minimum` becomes 2.34.1, with the "too old" texts, `requirements.md`, the README and CLAUDE.md's prerequisites, and D277's branch for a git below 2.31 goes; a CI job on stock Ubuntu 22.04 runs the tests on git 2.34.1, and what it finds is fixed. Nothing below 2.43 has run before.
- Keyboard-accessibility pass (R7, D411): the keyboard gaps the pass's own reading confirms (the repo reader of 2026-11-18 lists, none proven by a test yet: staging a hunk or chosen lines from the diff, choosing a blame run, a key or palette entry for the running operation's Cancel, the stage rows' buttons shown on keyboard focus); an accessible name on every control; the graph's selected row exposed to UI Automation (D49); and one walk-through with Windows Narrator by hand, by the owner. A palette made for high contrast is not built (D411; Backlog).
- The contrast fixes in one round (D410): the placeholder, the dark danger colour with the dark label, and the dark accent on hovered and selected rows; the items "From phase 6" below give the colours and the earlier pictures. Before the checks are written, a pixel scan of phase 7b's pictures counts what each fix changes.
- Performance pass: what it measures, on which machine and where it ends are the design's. The repo reader's candidates: a start-up measure with one and with several restored tabs, a memory measure on the 100k-commit repository, the reload after an outside commit, the batched config reads the review before phase 7 called S8, and risk 44's GitLab rate limit against restored tabs. Q1 stays asserted.
- Third-party notices, complete for what the app ships and reachable from the app: every package with its licence text, the native libraries (SkiaSharp and what Skia bundles, HarfBuzzSharp, ANGLE), the fonts (Inter's OFL text is not in its package), and the TextMate grammars and themes, whose licences D64 left for this phase.
- The About screen (R6, D398, D409): a sixth, last section of the settings screen, a palette command and the macOS application menu's item; the owner's name, the version, the copyright line, the licence, the notices and the repository's link.
- Icons and branding (R5, D407), after the search for the name (D412): the icon's SVG, the tool that makes the `.ico`, the `.icns` and the PNG files from it, and the window's and the executable's icon; the README's "Name" note then names the icon too.
- User guide (D408): Markdown pages in the repository, a site on GitHub Pages built by a workflow, pictures in both themes from the walk-through's harness, and the app's links to it.
- From phase 3: OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE`, so when the app was started from a terminal such an ssh still asks there (Ubuntu 20.04 ships 8.2; Git for Windows 2.36 bundles 8.9, and what 2.30 bundles was not checked). The first-run experience says so, or the minimum versions are raised.
- From phase 6 (design 8.5): a broken `~/.gitconfig` shows its own message. Today `git --version` fails and the app says "Git is not working"; showing git's `fatal: bad config line …` is first-run work.
- From phase 6 (design 11.3, 11.10, 11.11, D198): a palette made for high contrast, and the dark theme's text below 4.5:1, left with the owner's consent: `VcDangerBrush` 3.64:1 on a hovered and 3.76:1 on a selected row, `VcAccentBrush` 4.13:1 on a hovered and 4.26:1 on a selected row (the renamed file's "R"), and the danger button's white label 3.38:1. A lighter dark danger colour also lowers that label (2.70:1 at about `#EA7D86`), so it comes with the dark label `#10121A`, which changes only the label in 35 earlier dark pictures: scripted phase 2 `17a`, `18a`, `19a`, `19c`; phase 3 `04b`, `09f`, `10f`, `11b3`, `12g`, `13i`, `13j`, `16h`, `16k`, `20c`, `22e`, `25c`, `25g`, `29d`, `30d`, `31a`; phase 4 `b9c`, `c13d`, `c13g`, `c13k`, `c14h`, `c16b`; phase 5 `b9i`, `b9m`, `u16i`; real-window phase 2 `17a`; phase 3 `09f`, `10f`, `22e`, `31a`; phase 4 `c16b`.
- From phase 6 (its gate's inspection), for the accessibility pass, the move here for the owner to confirm: every text box's placeholder (the left panel's `Filter` box since phase 1, the palette's, the settings screen's, the Pick dialog's `Filter`) is drawn in `VcTextSecondaryBrush` at Fluent's watermark opacity, 0.5: `#616773` on `#272C36` dark (about 2.5:1), `#ADB2B8` on `#FFFFFF` light (about 2.1:1, lighter than the disabled colour). Phase 6 left it, as raising it would change nearly every picture with a text box.
- From phase 6 (design 15.5), for the accessibility pass: the icon buttons without an automation name (only the page close buttons set one): `ResolverPreviousFile`, `ResolverNextFile`, `ResolverCloseButton`, `ResolverPreviousConflict`, `ResolverNextConflict`, `CloseDiffButton`, `ActivityLogClose`, `SearchPrevious`, `SearchNext`, `SearchClose`, `CompareSwap`, `CompareClose`, `RefHiddenToggle`, `RefSoloToggle`, `DismissRestoreButton`, `RowStageButton`, `RowDiscardButton`, `RowUnstageButton`.
- From phase 7a (design 17 item 5), for the keyboard-accessibility pass: after "Choose" closes the repository browser ("Clone from GitHub"), no control of the clone form has the keyboard focus (part B's `b06a`), so a keyboard user must Tab into the parent folder's box; D104 gives the focus back only to a dialog below, and phase 7a's report does not assert where it goes (B6).
- From phase 7a (design 17 item 11), for the keyboard-accessibility pass: a diff that is still loading keeps a pending focus and takes the focus when its text arrives, even when the user has moved the focus elsewhere meanwhile (`DiffView.axaml.cs:131-135`, `:146-149`; `DiffTextView.cs:209-213`, `:605-617`). It is phase 6's behaviour, found when a test's race on macOS was investigated.
- From phase 7a (D340, design 19 item 4; risk 35), with the git versions the release supports: on git 2.31 to 2.45 a user's `.netrc` or `_netrc` entry for github.com answers a bound call's first 401 before the app's helper, so that call goes as the entry's user, and git-lfs's own HTTP client asks HOME's netrc before any helper on every git version (its source read, not measured). Since phase 7b the same holds for an entry for gitlab.com, dev.azure.com or `<organization>.visualstudio.com`, which on 2.31 to 2.45 answers the first 401 of a bound URL with no user too (phase 7b's design 8.6, measured on 2.31.1, 2.43.0 and 2.56). Whether the Accounts section says so, as D277's note does below 2.31, is decided with the minimum version.
- From phase 7b's review before the gate (D392; phase 2's): while "Committing…" runs, the summary and description boxes and Amend stay editable (`WorkingChangesView.axaml` binds no `IsEnabled` on them), and the commit's end clears them, so what is typed meanwhile is lost and an Amend ticked meanwhile is unticked. Bind the boxes and Amend to `!IsCommitting`, or clear only what was committed; no picture captures that moment.
- From the owner's licence answer (D397): `Directory.Build.props`' `Authors` and `Copyright`, which stamp every assembly, still read "VisualCommit contributors"; they take the owner's name as `LICENSE` has it ("Rahim Khajei") in the phase's first code commit.
- From the owner's answer of 2026-11-18 (D398; R6): an About screen that names the owner as the app's creator, "Rahim Khajei". Where it opens from, what else it shows (the version, the copyright line, the licence, the third-party notices) and its texts, layout and visual checks are the design's.
- From the 7b handoff's Known issues: `VisualCommit.slnx`'s comment on the live project still says it acts on the owner's GitHub account; since 7b it acts on three services (D346). It changes in this phase's first code commit.
- From the repo readers of 2026-11-18, to settle in the design: nothing stops two instances of the app, and each rewrites `session.json` whole, so the last writer wins; and the docs give the macOS data folder as `~/Library/Application Support/VisualCommit` while the code uses `SpecialFolder.ApplicationData` with no macOS branch, which .NET probably maps to `~/.config`: one log line in CI's macOS job settles it, and the code or the docs are corrected.

Visual checks, written per part by the design (D244):
- The first-run states: git missing, too old, not working (a broken `~/.gitconfig` among them), and found again by "Check again".
- The About screen, and the icon in the window and the taskbar.
- The contrast fixes in both themes, with the earlier pictures they change compared against phase 7b's references.
- The keyboard paths the pass adds, and the accessible names read through UI Automation in the real window.

Done when: a user whose git is missing, too old or broken is led to a working git by the app's own guidance; every main action is reached by keyboard and every control has an accessible name; no text pair of either theme is left below 4.5:1 in `ThemeTokensTests`, but for the three texts the app dims on purpose, which the owner kept as recorded exceptions with their figures pinned there (D469); the About screen, the icon, the complete notices and the user guide exist; and the visual gate passes.

Outcome: delivered, but for what the lead's rulings, the review before the gate and the gate left
(below); [LEAD: the gate's final state: passed on `750eb33` or not]. The report's "What proves
each Delivers item" maps every item to its checks and tests: 45 checks in parts L (6), I (3), F
(14), K (8), B (2), A (6), U (3) and G (3), 45 check methods in a new walk-through project
(`VisualCommit.VisualTests.Phase8a`, D416; design 0.3, 0.5), which also holds `KeyboardPassTests`
and the names' sweep, `AccessibleNameSweepTests`; new classes in `VisualCommit.App.Tests` and
`VisualCommit.Git.Tests` (`SingleInstanceGuardTests`, `IconFilesTests`, `ThirdPartyNoticesTests`,
`RestoreOrderTests`, `GitWordsTests` and `TempFolderCleanerTests` among them) and earlier ones
extended (`ThemeTokensTests` with its dimmed texts, `GitLocatorTests`); and 21 checks repeated in
the real window in 13 launches (`Phase8aRealWindowPass`, design 12). The performance pass has no
part: its series are the report's "The performance series" (D457, D458, D467). The visual checks:
the first-run states, git missing (F1), too old (F5), not working with a broken `~/.gitconfig` (F6,
F6b) or not started (F7), the chosen git gone (F3) and the helper missing (F11), and git found again
by "Check again" (F4); About (A1 to A6) and the icon in the window and the taskbar (B1, B2); the
contrast fixes in both themes (L1 to L6), with wave 1's pictures compared with phase 7b's references
inside the scan's boxes (1,141 of 1,150 earlier scripted pictures changed, 1,083 only inside their
boxes, the other 58 read one by one; D419); the keyboard paths (K1 to K8) and the names read through
UI Automation (U1 to U3; in the real window U1 and U3). The scan's eight lists, in
`docs/test-reports/phase-8a-contrast/`, replaced the 35 label pictures of the item from phase 6,
which missed 6 (ruling 31). Delivered in another form than the items read: an OpenSSH older than 8.4
is named in the log once per session and in the guide, not on the first-run screen, and no minimum
version was raised for it (D424; ruling 7 as ruling 100 amends it; risk 15); and the 18 icon buttons
the item from phase 6 lists took their names from their tooltips since 2026-10-10, so the names were
weak, not missing: they got names of their own, and the true gaps, the activity log's toggle and the
three splitters, were named (D435, ruling 34; U3). The "Done when" items: a missing, too-old or
broken git is led to a working one by the first-run screen (part F; winget for a missing and a
too-old git, not for one that does not work, D453, amending D405); every main action of design
6.3.9's inventory is reached by keyboard (`KeyboardPassTests`), commits that are not next to each
other staying the mouse's (ruling 50), and every control has a name (`AccessibleNameSweepTests`, 14
states); no text pair of either theme is below 4.5:1 in `ThemeTokensTests` but D469's three
exceptions, pinned there; About, the icon, the complete notices and the user guide's 33 pages with
`guide.yml` exist; the gate: below.
The owner's answers (D397 to D415 before the design, D453 on winget, D460 on the Narrator listen and
D469 on the dimmed texts) set its shape; the design's decisions are D416 to D446, its review's D447
to D452, and the lead's during the build, the review before the gate and the gate are D454 to D459,
D461 to D468 and D470 to D473; the lead's rulings, 116 by 2026-11-27, are in design 13.1. The lead's
first package (WP0, `fc8a063`) and eleven packages in four waves (design 0.4), each reviewed before
its merge, and in wave 4 ruling 76's restore order (design 14). What they and CI found changed the
design in places: the design's review found, among others, the winget install started through
`cmd.exe` with a bare `winget`, which a cloned repository's `winget.cmd` could have answered, now
started by the full path found (D449); CI's floor job failed 20 of 7,889 tests on git 2.34.1 before
the floor's package (run 44479788558), one group a fault of the app, a fast-forward of a branch
checked out in another worktree, which git refuses only from 2.35, and passed on its first round
after it (run 44727459663; D420; risk 49); the performance series fired four triggers, of which the
reload after an outside commit and after a fetch and Pull's settings reads went to the backlog and
the start with several restored tabs got the restore order, which its first alternated series did
not prove in full (D461) and the series taken again proved (D457, D458, D467; risk 10); step
19's whole runs met Windows' Storage Sense emptying the tests' temporary folders on a nearly full C:
(D468, risk 48), and CI's Windows job a held fetch with no percentage (D470, risk 51). Before the
gate, the independent review of the code (Workflow above) found 48 problems, 43 confirmed, five of
them major (the keyboard focus lost at a change of tabs and at a fold, the blame's arrows going on
from the hidden caret and not from the run a click picked, the status bar's live region told of
every progress tick); 38 were fixed in eight packages, and three more rounds answered what the
fixers and readers raised, the last with one rule for the whole window where an action by the keys
takes the focus away (D462 to D466; design 13.1, rulings 99 to 103).
The gate ran on the Windows machine on 2026-11-26 and 27. The live command's run before the
pictures, on `fab4073`, passed 596 of 596 after its 582 offline tests, with nothing to correct in
the fakes (ruling 112), and D246's two runs of 8a's real-window checks, on `e4b323c`, passed 13 of
13 each (5 min 41 s and 5 min 5 s); both are made once and stand. The first counted round, on
`e4b323c`, does not stand (ruling 115). Its default run's first attempt lost two projects to the
hang watchdog while other work of the lead's ran beside it on the laptop, with no assertion failed
and the cause not established, so nothing runs beside a counted run on the laptop (ruling 115; risk
12); the second attempt, alone, passed 8,920 of 8,927 tests in 1 h 48 min 52 s, the real-window pass
119 of 124 with Q1 at 1,912 ms, and the live command 596 of 596. Its inspection, 28 Opus inspectors
on all 1,696 pictures and 45 of CI's (D247), found one fault of the app, in the real-window
`f5a-too-old` and `f6a-config-fault`: the first-run screen's path, which on the laptop holds the
space of the repository's folder `Visual Git`, broke at that space and after the hyphen of
`net10.0-windows`, because `PathTextBlock` ends a path at white space, D227's rule for a path inside
running text. Its fix, merged at `ee3cdc4` (D472): `PathTextBlock.IsOnePath`, set on
`GitSetupPath`, lets a text that is one path break only after `\` or `/`, the default mode
unchanged. The guide's site was read from the `guide-site` artifact of the first round's code, and
two faults of its pages were corrected in docs, every picture's width and one alt text (D471,
ruling 114). The counted round that stands ran on 2026-11-27 (ruling 116). Its default run, on
`0f9099a`, whose code is the fix's merge's, passed 8,929 of 8,936 tests (7 skipped) in 1 h 53 min
8 s with nothing else running (risk 12). Its first real-window pass, on the same commit, failed in
phase 3's check 22 alone, with no fault of the app: one look of a wait met a `COMException` from UI
Automation while the Remotes dialog built its rows again, and the harness let it end the check. The
harness's fix, test code alone, merged at `750eb33` (D473): a look that fails in UI Automation
counts as "not yet" (`RealApp.Poll`, `WaitForRuleTests`; risk 3). The pass ran again alone on
`750eb33`, as D246 allows, the scripted pictures the same files: 140 of 145 tests passed, the
performance series' five facts skipped, in 40 min 56 s, with Q1 at 1,626 ms (risks 10 and 18), at a
scaling of 200% in a work area of 1500×952, so nothing ran at 1920×1080 (risk 3); its `f5a` and
`f6a` break the path twice, each time after a `\`. The list holds 1,696 pictures, 1,239 scripted and
457 real-window files, and the live command passed 596 of 596 on `750eb33`. The Gate commit is
`750eb33`. CI's run on it, 46007767846, passed in all six jobs at its first attempt (risk 8), and
the guide's workflow, started by hand on the branch as D451 says for a Gate commit whose push starts
none (46024262689 on `6120f86`, docs alone after the Gate commit), passed, its site's 84 pictures
and 35 pages the same files as those the lead read for ruling 114. The counted round's inspection
(D247; 07:20 to 07:46), 28 Opus inspectors, CI's batch among them, judged every picture against the
first round's: all 1,696 passed, and the 45 of CI's from run 46007767846; none was in doubt, so no
second look was called (ruling 116). [LEAD: whether the whole gate passed.] [LEAD: the checks by
hand with the owner: the Narrator walk-through of 21 steps (risk 53), phase 6's System step, the
icon beside its look-alikes, a second start from an isolating start file.] Moved to phase 8b, among
its "From phase 8a" items: the checks by hand on a Mac and on Linux that the phase sent there, the
bundles' icon files, the notices of what 8b's packaging adds, the updater's place before the
single-instance guard and D401's question beside the first-run screen, the start before the window
opens in the published build, and the guide's install and reporting pages. To the backlog: what the
lead's rulings and the review before the gate left (below), beside D457's three rows (the reload
after the refs change, Pull's settings reads and design 10.3.6's summary mode), D469's de-emphasis
and what D473 leaves of the real-window harness's waits, and, for phase 8b, D472's three places of a
path in text and D471's test of the guide's picture widths. Left as they were recorded: risk 44's
GitLab rate limit against restored tabs, a candidate the Delivers item named for the performance
pass, and risk 38's lookups, both outside the phase (ruling 2). Left to the owner: the lead's 116
rulings and the decisions made without the owner, the click that carries the focus frame (ruling
107), and GitHub Pages' source at "Phase 8a accepted" (ruling 64, D451); the handoff's "Waiting on
the owner" gives each. Recorded as risks: the temp folder's cleaner (48), git 2.34's worktree guard
(49), the single-instance guard's limits (50), the held fetch with no percentage (51), the guide's
words against the app's (52) and the graph's row for a screen reader (53). What was built and proven
is in [handoffs/phase-8a.md](handoffs/phase-8a.md), the gate in
[test-reports/phase-8a.md](test-reports/phase-8a.md).

### Phase 8b — Packaging and public beta (M)

Covers O19, R1 and R2, and Q6 on the three platforms.

The second half of phase 8 (D399). The owner's answers so far: D400 (the installers, the updates and
the beta on GitHub Releases, each release approved by the owner), D401 (the update check is asked
at the first start), D402 (Windows ships its beta unsigned, and the signing route is chosen before
the first stable release) and D403 (macOS is signed with the owner's Developer ID and notarised).
Still to ask before its design, each with the research's recommendation
(`D:/Projects/Personal/phase8-work/research/`, `web-installers.md`, `web-macos.md`,
`web-channels.md`, `repo-ship.md`, `repo-gate.md`, `repo-critic.md`): the installers per platform
(recommended: Velopack, MIT, with a per-user `Setup.exe` on Windows, a `.pkg` and a `.zip` per
architecture on macOS and a self-updating AppImage on Linux, each self-contained per runtime, not
single-file and not trimmed, the askpass helper self-contained beside the app); the macOS token
store (risk 34: the research would keep `/usr/bin/security` until a move is tried on a real Mac);
the platforms, architectures and oldest systems shipped (.NET 10 supports macOS 15 and later and
no consumer Windows 10); the version scheme, stamped from a tag that the owner's identity makes
(D40); what proves "receive an update" and when the beta becomes 1.0; from which commit a public
beta is built, since phase work reaches `master` only on acceptance; the shape of the regression
against the installed build; whether the opt-in install checks may install a test-labelled package
in the owner's own Windows profile; what an uninstall removes; the channels for bug and security
reports; and when a Mac is at hand for the 78 steps of the macOS checklists of phases 0 to 7b and
phase 8a's lines ("From phase 8a, by hand" below; five of them in its report's "macOS checklist for
the owner").

Delivers:
- Installers and auto-update on all three platforms (R1, O19), with D401's question at the first start, its switch in the settings screen, the "Check for updates" command and `PRIVACY.md`.
- Code signing and notarisation (R2): macOS with the owner's Developer ID, notarised (D403); Windows unsigned for the beta, with the route chosen before the first stable release (D402).
- The release workflow, which waits for the owner's approval before it signs and publishes on GitHub Releases (D400), and the beta release.
- From phase 3: the askpass helper `visualcommit-askpass` (project `src/VisualCommit.AskPass`) is a framework-dependent program that runs on the .NET runtime the app uses (it inherits `DOTNET_ROOT`), and `AskPassLocator` expects it next to `VisualCommit.Git`'s assembly, or in the app's base folder when the assembly has no location (single-file). The installers must ship it in a form that runs there, self-contained packages included; without it every credential and ssh prompt fails. Since phase 7a it is also the account's credential helper (`--vc-credential`, D255): without it the account never binds git (`AskPassBroker.HelperFound`, phase 7a's design 0.2), and D82's order holds instead (risk 14). The repo reader of 2026-11-18 found that no code in `src/` sets `DOTNET_ROOT`, so in a self-contained package on a machine without a global .NET 10 runtime the framework-dependent helper would not start: the package ships it self-contained for the same runtime in the same folder, or compiled ahead of time, with a test that the published helper starts.
- From phase 3: on the Mac, by hand, cancelling a fetch over ssh. Cancel kills only git there (D37); ssh and the askpass helper end when their pipes break, which no automated run proves.
- From phase 6 (design 12.2, 11.9), by hand: the macOS and Linux editor install paths and the macOS native theme observer; the macOS checklist gains design 11.8's by-hand System step.
- From the review before phase 7 (D239): `Directory.Build.targets` copies no native `.pdb` file (all of them SkiaSharp's and HarfBuzzSharp's) and, on Windows in a build without a `RuntimeIdentifier`, only the build machine's platform's files from every package's `runtimes/` folder. A portable publish (no `-r`) must set `-p:VisualCommitAllRuntimes=true`, or a package built on Windows carries only win-x64's libraries; a `dotnet publish -r <rid>` loses only the `.pdb` files.
- From phase 7a (ruling 20, design 3.10; risk 34), with the code signing: on macOS each account's token (GitHub's, and since phase 7b GitLab's and Azure DevOps') is a login-keychain item made by `/usr/bin/security`, which any process of the user can read through `security` without a prompt, as on Windows and Linux. Signing the app decides whether the store moves to Security.framework for a per-app access list.
- From phase 7a (risk 41), by hand: a sign-in kept in Windows' Credential Manager across a restart of the installed app; on a Linux desktop with a Secret Service, its unlock prompt and the new "Login" collection (D271), and "Create a token on GitHub" and "Open in browser" opening the default browser; the Mac's are phase 7a's macOS checklist.
- From phase 7b (risk 41), by hand: sign-ins to GitLab and Azure DevOps kept in Windows' Credential Manager across a restart of the installed app, beside GitHub's, and on a Linux desktop with a Secret Service its three entries; the default browser opened by "Create a fine-grained token on GitLab", "Or create a legacy token, filled in" and "Create a token on Azure DevOps", by the sections' "Open merge requests in browser" and "Open pull requests in browser", by a request's "Open in browser" and by the status bar's "Open in browser" after a create, none of which a real window opens in phase 7b (its design 14.2, ruling 29; parts A, P and N's scripted checks hand them to a fake launcher); the Mac's are phase 7b's macOS checklist.
- From phase 8a, by hand (its design 0.5, 4.6, 4.9, 5.9, 7.9 item 6 and 8.9 item 2; ruling 85; the second start, About, F1, the Command Line Tools and the Dock icon are also in its report's "macOS checklist for the owner"): on a Mac, a second start of the app (`open -n`, and from Terminal), macOS bringing the first window forward and the single-instance socket under a real `TMPDIR`; "About VisualCommit" in the application menu opening About, with no "About Avalonia"; F1; the first-run screen's Command Line Tools path (`xcode-select -p`, the shim, `/usr/bin/xcode-select --install`; F9); an unbundled app's icon in the Dock and `icon-macos.svg` against macOS 26's plate. On Linux, X11's window icon.
- From phase 8a (ruling 17, D447; its design 7.1), with the installers: macOS 26's layered `.icon`, `Info.plist`, the Linux `.desktop` file and hicolor tree, and the installers' images; the macOS bundle takes the committed `assets/brand/generated/VisualCommit.icns`.
- From phase 8a (ruling 15, D431): the third-party notices of what 8b's packages add beyond the build output: the .NET runtime's, where a package carries it, and the installers' own. The app's embedded `LICENSE`, `THIRD-PARTY-NOTICES.md` and `notices/` ship unchanged, and `ThirdPartyNoticesTests` holds them to the build output (`VisualCommit.deps.json` and the grammars' assembly).
- From phase 8a (ruling 67, D421; its design 4.1, 5.1), with the updater: its bootstrap comes before the single-instance guard in `Program.Main`, since its hooks start the app with their own arguments, with a second key by install folder and the guard's macOS limits (risk 50); and D401's question at the first start is asked only while the first-run screen is not shown.
- From phase 8a (its design 10.1, 14.1): the time before the window opens, 2.7 to 4.9 s in the lead's probes of the restore order on the laptop's Release build, measured in the published build, and whether a build compiled ahead of time (ReadyToRun) shortens it (the Notes of phase 8a's `status.md`); and the performance measures on the installed build (D436 to D438, D457).
- From phase 8a (its design 11.1): the user guide's `install.md`, a frame until the installers exist, and `troubleshooting/data-and-logs.md`'s "Reporting a problem", which waits for the channels of bug and security reports; each changes with the app's behaviour it states (risk 52).
- From the research and the repo readers of 2026-11-18, to settle in the design: a switch that turns the update check off in the tests' isolation and in the real-window pass; the real-window harness's `AppExecutable`, fixed at build time to the build output, pointed at the installed app; the files the app writes read across an update and a downgrade; `SECURITY.md`, a changelog and the README's install steps; a check that every shipped executable file is signed where the package is signed; and a written list, with expected results, of what "Linux by hand or through WSL" checks.

Visual checks:
- Full regression: every check from phases 0–8a re-run against the installed build.
- Install, first run and update on Windows.
- The complete macOS checklist run by hand by the owner; Linux by hand or through WSL.

Done when: a new user on each platform can install, open a repo and receive an update without help.

## Backlog (not scheduled)

O10 submodules, O11 worktrees, O12 Git LFS, O13 built-in terminal, O14 built-in file editor,
O15 AI commit messages, O16 profiles, O17 commit signing, O18 patches.

Left out of phase 8 by the owner's answers of 2026-11-18 and 19: a git bundled with the Windows
package (D405); a palette made for high contrast, which the app follows as light or dark (D411);
a registered trademark for the name (D412); fixes for risks 39, 40 and 43, which stay recorded
until a report (D413); and, until the owner chooses a route before the first stable release, a
signature on the Windows files and the Microsoft Store as a channel (D402). Not decided, so in no
phase yet: winget and the other channels beyond GitHub Releases (D400).

Left out of phase 8a by its performance pass (D457; design 10.3.6), with the numbers of the laptop's
three series of 2026-11-22. An incremental reload of the graph after the refs change: a
100,000-commit history reloads in 2.5 to 4.0 s after an outside commit or a fetch (the medians of
the reload's own log line; the trigger was 2 s), about nine tenths of it the one `git log
--date-order` of D47, with the old graph usable meanwhile and no stall of the window seen; a change
amends D47 or D52. With it: an operation that moves a ref keeps its running text until the reload
ends, because the queue awaits the refresh, so a fetch in such a history shows as running for about
6 s. Smaller ideas the panel named and nobody measured: `GIT_FLUSH=0` for the log, a cheaper parse,
the log started beside the refs read. S8's batch of Pull's settings reads, three `git config --get`
processes made one (7a had taken only the budgets): the three took 316 to 465 ms (the trigger was
250 ms); the batch would save 2 of a Pull's 23 git calls, about 0.2 s of a plain Pull on Windows and
nothing of Pull (rebase), and would rewrite the reader that decides how Pull integrates and about 19
earlier tests in 11 files; a rested median of about 600 ms, or the owner, reopens it. Background
tabs restored in summary mode and promoted when shown, design 10.3.6's own candidate for the start
with several restored tabs (ruling 76 orders the openings instead): it would also save the
background histories' memory (about 800 MB of working set with ten 100,000-commit tabs, against 286
MB with one), but it breaks `InitializeAsync`'s contract and the checks that wait for every restored
tab's load, puts a whole load on the first switch to such a tab and moves D289's reads.

Kept as they are in phase 8a by the owner's answer of 2026-11-26 (D469), their contrast figures
pinned in `ThemeTokensTests`: the three texts the app dims on purpose below 4.5:1 on rows the user
still acts on. A de-emphasis that keeps 4.5:1 would retire the exceptions and their pins. For a
hidden branch's name, solo's left-out branches and a dropped rebase row, the secondary text colour
reaches 4.54:1 or more on every row state in both themes: a hidden branch is still marked by its eye
and a dropped row by its strikethrough, solo's left-out branches are then told from the soloed one
by that colour and the soloed branch's mark alone, and every earlier picture with a hidden branch, a
solo or a dropped row changes. For the rows a search dims no opacity that still dims does it (the
message would need about 65 %, the date, SHA and author about 90 % in light), and such a row's
branch labels read 1.2 to 1.5:1 on their lane's colour, so the search needs a form of its own: the
matches marked, the other rows left at full contrast.

Left out of phase 8a by the lead's rulings and the review before its gate (its design 0.1, 13.1,
13.4 item 15, 14.1): keyboard selection of commits that are not next to each other, which stays the
mouse's, as the guide says (ruling 50); an item status in UI Automation on the left panel's rows and
on the graph's list, and the left panel as a tree with levels (ruling 36; design 9.9 item 1); a help
text for the hunk bars' repeated `Stage hunk` and `Discard hunk` names, which lie in no items
container, so the sweep does not flag them (13.4 item 15; `DiffTextView.cs`); "Check again"
reopening the tabs whose restore failed through the restore order, where it reopens them all at once
(`MainWindowViewModel.OpenWhatWaitedForGitAsync`; ruling 90, design 14.1); git found behind Git for
Windows' launcher, `cmd\git.exe`, which a user's PATH gives and which starts git as a child, a
process more per git call: Q1 read 1,771 to 2,813 ms in the lead's probes started so, where the
real-window pass and the performance series, started from Git Bash, find git itself (D457; ruling
99's S19-22; risk 10); the overlay scroll bar over the graph's widest dates and over a conflict's
last line at the end of the resolver's panes, the app's look (S19-07, S19-10); a repository picked
under Recent repositories opening a second tab on a worktree that another tab shows, the state
phases 5 and 6 accepted (S19-08, D209), and Open not finding a tab through a link's spelling of its
folder, such as macOS's `/var` for `/private/var` (`WorkspacePaths.Same`; ruling 104); a list row
that itself shows something new losing its press (S19-26); and the virtualizing list's estimate
after a change above the rows in view, older than the phase: a branch created above the rows in view
of a scrolled left panel draws them 22 px lower, 4 px above their true places, with new controls, so
a press held across it loses its click (S19-28; ruling 100, `LeftPanelScrollTests`).

Accepted by the same rulings as limits of the keyboard's focus: a text that changes while its button
keeps the focus is spoken nowhere, such as a failed start of winget or of the Command Line Tools on
the first-run screen's error line, which describes Choose (ruling 101); after a search that finds
git and closes the first-run screen the graph area's target, and after a Choose that passes Check
again, take the focus without the frame, also after a press by the keys (ruling 102); while a
press's own run disables its control the focus is on nothing until the run ends, but for the clone
form's Clone, which hands it to its Cancel (so for Fetch all and Pull all, the toolbar's Fetch and
Pull on a dashboard, the rebase's Start, the resolver's Merge tool, Undo and Redo), and a loss with
no press before it is not followed (an operation's end hiding the status bar's Cancel, a bulk run's
end hiding Cancel all, a refresh replacing focused pull request rows), nor a command run from a
palette opened over an unframed focus (ruling 103); `GitSetupView.GiveFocusBack` would take a
focused but hidden "Use the Git on the PATH" for a kept focus when a search ends within the key
press, unreachable today (ruling 105). And three focus frames (ruling 101): at a scaling of 1 Dark's
Theme frame lies under two faint ink pixels of the note's "follows", and at other scalings Dark's
and System's touch the note's ink or keep one row from it, where clearing it would move the rest of
General 2 px in every picture of it; GitLab's legacy token link's frame lies one device row from the
token link's descenders and touches them at 150 %, where the 6 px between the links that would clear
it move the lower half of phase 7b's dialog; and a radio button's frame is cut by the scroller when
Tab brings it to the edge of a scrolled section.

For phase 8b, left by D472 at phase 8a's gate, where the first-run screen's path became one path
(`PathTextBlock.IsOnePath`; its design 13.1, ruling 115: they stay as they are until phase 8b), each
named by the fix's readers and none seen wrong in a gate picture: a path inside running text in a
plain text block (the first-run screen's body, guidance, git's words and command; the settings row's
status); a tooltip whose text is a path alone (what `TrimmedTextBlock` shows when it cuts its text);
and a UNC path written with forward slashes, which `PathTextBlock`'s default mode lets break between
its two leading slashes.

Left by D473 at phase 8a's gate, where a wait of the real-window harness came to take a look that
fails in UI Automation as "not yet" (`RealApp.Poll`, `RealApp.IsFailedLook`; its design 13.1,
ruling 116), each named by the fix's package: about 70 wait conditions that reach `Find` or a LINQ
`Single` and still end a wait at once when an element is briefly missing or listed twice; loops
that act and read between their actions (`RealApp.Phase7b`'s `ScrollAccountsTo` and
`ScrollAccountsToEnd` among them); reads after a time-out in some catch blocks; and FlaUI's
`CheckBox.IsChecked` and `TextBox.Text` on an element that has gone.

For phase 8b (D471; phase 8a's design 13.1, ruling 114): a test that holds the widths of the user
guide's image tags, each `width` half its picture's pixel width, rounded up (`branch-menu` is 1,095
pixels wide). `GuideCatalogueTests` in `tests/VisualCommit.GuideShots` reads `src`, `srcset` and
`alt` only, so until then a picture added to a page needs its two widths by hand.

Left out of phase 7a by the owner's answers of 2026-10-28: GitHub Enterprise Server and GHE.com
(D258); several GitHub accounts at once (D263); GitHub's device flow, which needs a registered
OAuth app (D249); pull requests and avatars of a public repository without signing in (D257). Left out
by its design (design 0.1, 0.2; D297, D299): a page, column or timer for pull requests; a "Create pull
request…" for the current branch in the section's menu; a palette entry for Accounts or for signing in
(the owner's answer of 2026-10-29); a scope or expiry warning at sign-in; S8's batched config reads, of
which 7a took only the budgets (design 5.1; D254, D280); a pull request from a fork in the same
organisation, which needs `head_repo` (design 12.6: its 422 shows GitHub's words); and letting through a
new branch under two pattern lines of one remote that rename into two namespaces, which the push guard
still refuses (a terminal push still works).

Left out of phase 7b by the owner's answers of 2026-10-31: Azure DevOps Server on premises and
self-managed GitLab (D347); several accounts of one provider at once (D348); author avatars from
Azure DevOps and GitLab (D349); a browser sign-in or an app registration for Azure DevOps and GitLab,
as GitHub's device flow (D345); and a request to a new provider while no account of it is signed in,
so no list of a public repository's requests without signing in (D350, as D257 for GitHub; D360 sends
no anonymous request either, resourceAreas' lookup at sign-in included). Left out by the owner's
answer of 2026-11-01: GitHub avatars for a repository whose listed remote is another provider's but
which also has a GitHub remote (D367). Left out by its design (design 0.1, 16.3): binding SSH remotes,
which are recognised and never bound, as 7a's; checking out an Azure DevOps fork's pull request whose
fork is no remote of the repository, whose "Check out" item names the fork to add as a remote (D361);
quick actions in a GitLab description, which the app sends as typed and the service runs (ruling 20;
the form's note says so); and a repository whose name holds a literal `%`, which is neither recognised
nor bound. Accepted at its review before the gate (D392; design 16.3): an Azure DevOps 401 that is a
scope failure signs the account out as any API 401; a request queued for its provider's turn when the
account signs out or is replaced goes with that account's token; the providers' own refusal of another
provider's token reads the whole value, while the sign-in dialog finds such a token inside a pasted
value (D387), the one path by which a pasted value reaches a provider; and GitLab's "already open"
refusal in the create form wraps inside `origin/main` (`n05b`, whole).

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
| 1 | The real-window pass happens only on the Windows machine; development can also happen in a Linux cloud session (D39), which then leaves that pass to a Windows session. macOS and Linux are covered by CI builds and the scripted walk-through until the owner tests on a Mac by hand. | Mac testing: when the owner chooses; at the latest phase 8b |
| 2 | Settled in phase 4: the conflict resolver is built on AvaloniaEdit as planned (D64, D140, D148). Its panes are phase 2's `DiffPane`, generalised over `IDiffPaneHost` with phase 2's 66 pictures byte-identical, and its result an editable `ConflictResultEditor`. Still true: it leans on details of AvaloniaEdit 12.0.0 (built against Avalonia 12.0.0, used with 12.1.3) that an update can change: undo and redo go through the document's `UndoStack` with the platform's gestures from `HotkeyConfiguration`, because the editor's own routed bindings are removed and pick the modifier by the operating system; typed text is undone one character per step; the caret is the app's own, because AvaloniaEdit's blink cannot be stopped. `ConflictResultEditorTests`, `ConflictPanesTests` and phase 2's pictures catch a change. The result has no syntax colours (D148). | Whenever AvaloniaEdit or Avalonia is updated |
| 3 | Settled in phase 0: the real-window pass works. The in-repo harness finds the app through Windows UI Automation, clicks with the real mouse and captures the window; since phase 8a it finds each of the app's windows by its handle, from Windows' own list of windows (`RealApp.TopLevelElementsOf`), because a UI Automation search of the desktop's children asks every program on the desktop, and on 2026-11-24 such a search held launch 6's wait past its 15 s in about two runs of five (phase 8a's design 13.1, ruling 105); phases 0, 1 and 2 together take about 6 minutes, and phase 3 adds more (risk 18). Its comparison passes small differences such as a hover background or a tooltip (phase 1's report; at phase 5's gate a status-bar tooltip in phase 2's 17c), so only the inspection of the pictures catches them. Still true: it needs an unlocked desktop, and the development screen (a work area of 1500×952 logical pixels at 200% scaling) is too small for its 1920×1080 size, so that size is only ever proven by the scripted walk-through unless a larger screen is used; so at phase 7b's gate too (200%, 1500×952), where phase 0's check D was not run, and at phase 8a's gate (200%, 1500×952) in all three of its passes, the counted one on `750eb33` among them, where nothing ran at 1920×1080: neither phase 0's check D nor A1's second capture, which runs only where that size fits. Since phase 8a a wait of the harness takes a look that fails in UI Automation as "not yet" (`RealApp.Poll`, `RealApp.IsFailedLook`; `WaitForRuleTests`, 21 tests with no window), because at phase 8a's gate one look of phase 3's check 22 walked the app's automation tree while the Remotes dialog built its rows again, and the `COMException` it met ended the check (D473; risk 18). What D473 leaves is in the Backlog. | Each phase's gate |
| 4 | Hosting checks need a test account or token for each of GitHub, Azure DevOps and GitLab. Settled for GitHub on 2026-10-28 (D250): the default run and CI use a local fake of GitHub's API, and an opt-in command tries real GitHub with the owner's fine-grained token for a sandbox repository, from an environment variable. Built in phase 7a: the fake is `FakeGitHub`, `AuthRemoteServer` in a host mode (D266), and the command is `tests/VisualCommit.LiveHostingTests` (its design 14), whose first run with the token found two faults of the app that the fake's answers, written from memory, had hidden (D342; risk 42); it ran at status step 16, again as the gate's run before its pictures (its design 20.7) and on the counted round (112 of 112, a draft made and removed each time). Settled for Azure DevOps and GitLab on 2026-10-31 (D346): a local fake of each one's API in the default run and CI, and the live command extended to both, against a sandbox in an organization of the owner's on dev.azure.com that holds nothing but the sandbox (an Azure DevOps token cannot be limited to one repository) and in a new gitlab.com account that holds only the sandbox, with tokens in Windows user variables. Built in phase 7b: the fakes `FakeAzureDevOps` and `FakeGitLab` on a core taken out of `FakeGitHub` (D363), and the live command's classes `AzureDevOpsLiveTests` and `GitLabLiveTests` (D364; its design 15), with the owner's tokens in four more Windows user variables (set on 2026-11-05, as its design 15.8 says). The Azure DevOps sandbox lies in an organization that holds other work, which the owner accepted (D352, replacing D346's condition), so D371's guard keeps the command's requests to the sandbox, but for the organization's sign-in reads it names (connectionData and the core resource area) and D374's one read per run of a made-up organization's connectionData; the GitLab account lists four projects besides the sandbox, so D393's guard admits the sandbox project alone, beside the account's project list (the owner's choice). Stage 1 (2026-11-05 and 06) found that a GitLab fine-grained token needs Project: Read among its user permissions (D385) and eleven Azure DevOps answers that differed from the fake, which WP-LF put into the fakes before part A's checks, git's `info/refs` refusals kept as differences (its design 16.1); stage 2's app-driven `Check_3` passed on both providers (2026-11-08); at the gate the run before the pictures passed 595 of 596 (GitLab's `Check_2` stopped by the network, risk 42) and the counted round's 596 of 596. What is still not measured, the tokens' dates and the network: risk 42. | Settled; each gate's live runs (risk 42) |
| 5 | Public sign-in needs app registrations with GitHub, GitLab and Microsoft; personal access tokens work without them. Settled for GitHub on 2026-10-28 (D249): a personal access token, no app registration. Built in phase 7a: the "Add GitHub account" dialog takes a fine-grained or a classic token, and its "Create a token on GitHub" opens GitHub's fine-grained token page with the app's name and permissions filled in (D295). Settled for Azure DevOps and GitLab on 2026-10-31 (D345): a personal access token each (GitLab's fine-grained or legacy kind), no app registration. Built in phase 7b: "Add GitLab account" takes either kind, its help names the fine-grained token's permissions, Project: Read among its user permissions included (D385), and its links "Create a fine-grained token on GitLab" and "Or create a legacy token, filled in" open GitLab's fine-grained form, which takes no prefill, and its legacy form filled in with the name VisualCommit and the `api` scope; "Add Azure DevOps account" asks for the organization above the token and checks it before any request (D360), and its "Create a token on Azure DevOps" opens that organization's token page; each dialog refuses another provider's token form without a request (D378, D387). | Settled |
| 6 | Code-signing certificate and Apple Developer membership cost money. | Phase 8b. The Apple membership exists (D403); Windows ships its beta unsigned and its route is chosen before the first stable release (D402) |
| 7 | A built-in terminal (O13) is harder in Avalonia than in web technology. | Backlog |
| 8 | Settled on 2026-10-06: the owner made the GitHub repo public, so CI no longer runs on a monthly allowance of minutes (D38). While it was private, phase 0 used the allowance up. Still true: a job that hangs wastes time, so `ci.yml` keeps a time limit on every job and its test step, raised as the tests grow (in phase 4 to 24 minutes for the test step and 32 per job of the three platforms, D152; in phase 5 to 40 and 50, D184; in phase 6 to 55 and 65, D232, when macOS's Test step passed 32 minutes; risk 12; in phase 7b to 70 and 80, D377, before the two new providers' tests, which were projected to bring macOS's worst run past 44 minutes), and raised again when a Test step passes four fifths of its limit, now 56 minutes, but never after the counted round's commit (D186, D377). At phase 6's gate the Windows Test step took 24 min 37 s and macOS's 23 min 33 s (run 40059391404), but macOS's reached 43 min 12 s in run 40019921605 the same day: the runners' spread comes near the 44. During phase 7a, with eleven test projects (`VisualCommit.VisualTests.Phase7a` and `VisualCommit.Hosting.Tests` new), the Windows Test step took 21 min 22 s to 35 min 21 s (run 40993671970) and macOS's 20 min 52 s to 37 min 57 s (run 41038505814), under the 44; the jobs' keychain steps and Windows' "Build the opt-in projects" run after the Test step (D259, D337). The gate commit's run (41190572421): Windows' Test step 21 min 48 s and macOS's 32 min 16 s. During phase 7b, with twelve test projects (`VisualCommit.VisualTests.Phase7b` new), its 29 runs from 41277720820 to 42522049151 (the CI section of phase 7b's `status.md`) took 20 min 49 s to 41 min 6 s in Windows' Test step (runs 41290125478 and 42480966359), 19 min 34 s to 46 min 21 s in macOS's (42459591309 and 42487421403: past the old 44, as projected), 4 min 4 s to 6 min 47 s in `ubuntu-latest`'s and at most 7 min 7 s in the containers', each under four fifths of its limit (56 minutes; 11 min 12 s of 14), so no limit was raised; the longest jobs took 44 min 21 s on Windows and 49 min 9 s on macOS. The last of them, 42522049151 on `9b044af`, whose code is the counted round's (`cb9af6d`): Windows' Test step 23 min 41 s, macOS's 33 min 9 s. That is the run of the Gate commit's code, all five jobs green: `ubuntu-latest`'s Test step took 6 min 23 s and the containers' 5 min 28 s and 5 min 51 s, every Test step under four fifths of its limit (56 minutes; 11 min 12 s in a container), and no limit changed after the counted round's commit (D377). During phase 8a, with six jobs (the sixth since `e04762b`, "ubuntu 22.04 container, git 2.34 (the floor)", with limits of its own, 25 minutes for its Test step and 35 for the job, D420, and failing the run since `bba59c7`) and thirteen test projects (`VisualCommit.VisualTests.Phase8a` new), the branch's 30 runs from 44479788558 to 46007767846 (the CI section of phase 8a's `status.md`) took 22 min 53 s to 45 min 28 s in Windows' Test step (44479788558's first attempt and 46007767846), 20 min 7 s to 47 min 44 s in macOS's (44750615552 and 45528731262), 4 min 59 s to 8 min 12 s in `ubuntu-latest`'s, at most 8 min 51 s in the containers' (45515065583) and 5 min 7 s to 8 min 27 s in the floor's, each under four fifths of its limit (56 minutes; 11 min 12 s in a container, 20 minutes in the floor's job), so no limit was raised (phase 8a's design 13.1, ruling 21). On 2026-11-19 Ubuntu's package servers were slow: both "bare ubuntu container" jobs of 44479788558's first attempt were cancelled at their 25-minute job limit, after a set-up of 18 min 27 s in one and in the set-up step in the other, and the floor's job's set-up took 19 min 3 s; such a job is run again (ruling 74; phase 8a's design 0.6). The run of the code of phase 8a's first counted round, 45917903492 on `53e07c4`, passed in all six jobs at its first attempt: Windows' Test step 34 min 2 s, macOS's 34 min 28 s, `ubuntu-latest`'s 5 min 50 s, the floor's 7 min 2 s and the containers' 6 min 48 s and 6 min 47 s. So did the run of the fix's merge, 45965143178 on `e632e72` (phase 8a's design 13.1, ruling 115): Windows' Test step 40 min 34 s, macOS's 44 min 0 s, `ubuntu-latest`'s 6 min 34 s, the floor's 6 min 17 s and the containers' 5 min 50 s and 6 min 10 s. So did the run of the Gate commit, 46007767846 on `750eb33` (ruling 116): Windows' Test step 45 min 28 s, the phase's highest, macOS's 26 min 53 s, `ubuntu-latest`'s 6 min 47 s, the floor's 7 min 14 s and the containers' 7 min 3 s and 7 min 45 s. Windows' Test step thus read 34 min 2 s, 40 min 34 s and 45 min 28 s on 2026-11-26 and 27 for the same tests but nine, the runners' spread, the highest 10 min 32 s under the 56 minutes at which a limit is raised; every Test step stayed under four fifths of its limit, and no limit changed after the counted round's commit (D377). | Settled |
| 9 | The tests are pinned to xunit.v3 3.2.2 because Avalonia's headless test package does not work with xunit.v3 4.x (D24). Check for a newer Avalonia.Headless.XUnit when Avalonia is updated. Since phase 7a `Directory.Packages.props` also pins Tmds.DBus.Protocol 0.94.1, the D-Bus client of the Secret Service store, to the version Avalonia 12.1.3 resolves; it moves with `AvaloniaVersion`, as xunit does (phase 7a's design 1.1). Since phase 8a it also pins the icon tool's packages (D428), which only `tools/VisualCommit.IconBuilder` references: Svg.Skia 4.9.1, since 5.x needs SkiaSharp 4.x, which Avalonia 12.1.3 does not take, and SkiaSharp 3.119.4, SkiaSharp.NativeAssets.Linux 3.119.4 and HarfBuzzSharp.NativeAssets.Linux 8.3.1.3, the versions Avalonia.Skia 12.1.3 resolves, which move with `AvaloniaVersion`. An update of a shipped package also changes `THIRD-PARTY-NOTICES.md`: `ThirdPartyNoticesTests` fails when a shipped package is not named there with its version (D431). The guide's workflow pins mdBook 0.5.4 by its Linux archive's SHA-256 (D439). | Whenever packages are updated |
| 10 | Q1 (the test fails above 2000 ms) had about 0.8 s to spare in the gates' real-window passes up to phase 4 (1002 ms at phase 1's gate, 1058 ms at phase 2's, 1189 ms at phase 3's, 937 ms at phase 4's), but only 0.17 to 0.25 s at phase 5's: 1,749 ms in the first run's pass, which started straight after the default run (the laptop warm), and 1,826 ms in the second run's pass, run after a rest. Measured alone after that rest, it was 1,147 ms. A hot laptop fails a gate by itself: the second run's first attempt, started straight after the 43-minute default run (risk 12), measured 2,613 ms (the pass's only failure), and on phase 3's gate day other runs measured 1438 to 2268 ms. Run alternately on the same machine that day, the phase 2 build needed 1688 to 1911 ms and phase 3's 1618 to 2002 ms: the spread is the machine's temperature, and phase 3 costs no more than that noise. With no branch hidden, phase 5's graph loads with the same git command as phase 4's (design 8.2); whether the rise (1,147 ms alone and 1,826 ms in the pass, against 937 ms in phase 4's pass) is the build or the machine, only the two builds run alternately can tell, and phase 5's gate made no such run. Rest the laptop before the real-window pass, and to tell a slower build from a warm machine, run the old and the new build alternately on the same machine. Most of the time is `git log --date-order`, which reads the whole history before its first commit when the repository has no commit-graph file. A much larger repository, or a slower machine, could need another way to the first page. At phase 6's gate, each pass after a 10-minute rest: 1,264 ms in round 1's pass (`774464d`) and 1,805 ms in round 4's (`b561b7c`), which counts; between them the laptop ran three more default runs and passes, so the spread is again the machine's warmth, and phase 6 adds nothing to Q1's path (design 10.2). Signed out, phase 7a adds no git call to Q1's path either (its design 0.2: the URL rules are read only while an account is signed in). At phase 7a's gate, each pass after a 10-minute rest: 2,456 ms in the counted round's first pass (`a9d02c1`), started at 02:18 while other programs' scheduled tasks ran (Disk Cleanup, Office's and Visual Studio's updates, Zoom's updater; its design 21 item 5), and 1,400 ms in the pass that counts (`a2c312a`), at 03:04 with the machine quiet: run the pass away from the night's scheduled tasks. At phase 7b's gate the counted pass (`cb9af6d`'s code) measured 1,827 ms, 173 ms under the limit and about phase 5's 1,826 ms, the closest a counted pass has come: it started at 16:00 straight after the 1 h 32 min default run, without a rest (the report's "Run"). Phase 7b adds no git call to Q1's path, which the pass starts with the three providers off (its design 9.11: with them off nothing changes), so the rise from 7a's rested 1,400 ms points to the warm laptop, which only a rested pass, or the two builds run alternately, can tell; rest the laptop before the pass. Phase 8a's performance pass measured more than Q1 (its design 10.3.4; D457; the report's section "The performance series"), in three series of five runs on 2026-11-22, none of them quiet throughout: from the process's start to the first rows with one restored 100,000-commit tab 4,621 ms on the quiet take (7,825 and 8,184 ms straight after a Release build), with ten such tabs 6,759 to 9,367 ms; 286 to 287 MB of working set after the load and 346 to 351 MB at the peak after a reload (about 800 MB with ten tabs); the reload after an outside commit 2.8 to 3.2 s from git's return, and after a fetch 5.9 to 6.6 s from the click; Pull's three settings reads 316 to 465 ms. What disturbed them: a Release build within two minutes before (the command builds again when a commit was made since the last build, because the build is stamped with the commit), and Office's updater, a task of Windows' Task Scheduler with an idle trigger, which a rested and untouched laptop invites. So before a measured run: build on the very commit ten minutes ahead, commit nothing in between, and read the scheduler, not only the clock (CLAUDE.md's row and note). Several restored tabs share the processors at the start: by the tab's own clock the first rows of a 100,000-commit tab came after 1.8 to 2.8 s when it was restored alone and after 4.3 to 7.8 s beside nine other tabs, large or small, in the lead's probes, with the window's thread answering after up to 1.1 s; ruling 76 (D457) orders the openings: the active restored tab first and alone, the others at its first page, at most four at once. It was proven and merged on 2026-11-23 (D458; the report's "The restore order's proof"): in the lead's probe the active tab's first rows came after 1.8 to 2.0 s beside nine large tabs and after 1.9 to 2.9 s beside nine small ones, as soon as alone, where the build before it took 5.1 to 7.8 s; in the alternated series of M2 and M3 (five undisturbed pairs, one counted run each) the start with ten tabs read 6,876 ms with the order against 10,078 ms without (medians; M2's 5,736 and 4,942 ms), and counted from the window's opening 2.5 s against 5.7 s. That hour the one-tab start read from 4.6 to 7.6 s on the laptop in its ordinary state, so M2's bound went from 6,100 to 9,500 ms, and M3's is 8,900 ms. The review before phase 8a's gate found that this series could not vouch for the run that carried D457 (5)'s third condition, a base run begun 15 s after a scheduled task had started (D461), so the series was taken again on 2026-11-24 with a script that waits out what the scheduler has due and reads it from 60 s before each run to its end: on ten kept pairs the start with ten tabs read a median of 3,684.5 ms with the order against 7,212 ms without, after the window's opening 1.4 s against 4.8 s, and the one-tab start 3,656 and 3,810 ms; the conditions hold, the bounds stay, and these numbers replace D458 (1)'s (D467). Four of the first series' nine pairs met a task of Windows or Office that the scheduler had not shown as due and were taken again, as were two pairs of the retake (D467): read the scheduler after each measured run as well as before it. Those probes started the app as a user does, with `cmd\git.exe` first on the PATH: Git for Windows' launcher, which starts git as a child. The real-window pass and the series start it from Git Bash, whose PATH finds git itself (`ucrt64\bin\git.exe`). So the pass's Q1 (1,400 and 1,827 ms at phase 7a's and 7b's gates) is not what an installed user gets (1,771 to 2,813 ms in the probes, on a laptop that had worked all night; 1,970 ms in a probe at 00:59). The review before phase 8a's gate kept the test host's PATH for the pass and the series, as every gate since phase 0 measured, so that Q1's limit and the bounds stay comparable, and git found behind the launcher is a Backlog row (phase 8a's design 13.1, ruling 99's S19-22). A series' numbers are the laptop's of that hour: on 2026-11-24 Adobe's Desktop Service was found to have used a full processor core without pause since 2026-11-11, so every measure of phase 8a before that day, the "before" series and the first alternated series among them, was taken under it (D467); and on 2026-11-26, the day of phase 8a's first counted round, Snagit Editor was found holding a full core with one thread for about 23 hours, until it stopped by itself minutes before the real-window pass, so every run of that day before about 22:30 was made under it, and the counted round of 2026-11-27 ran without it (phase 8a's design 13.1, ruling 115). Before a measured run look for a process that uses a core without pause (two readings of `Get-Process`' `CPU` a few seconds apart; CLAUDE.md's note on the series), and read the scheduler before and after it. Phase 8a's gate measured Q1 in three passes, on code that is the same for it (its design 13.1, rulings 115 and 116): 1,912 ms in the first round's pass on `e4b323c`, after a rest of 14 minutes that followed the 1 h 49 min default run, and 1,938 ms, 62 ms under the limit, in the counted round's first pass on `0f9099a`, after a rest of nine minutes that followed the 1 h 53 min default run, neither of which counts (the first round fell to D472's fault, the second pass to the harness's, D473); and 1,626 ms in the pass that stands, on `750eb33`, which began nearly two hours after the default run's end and 66 minutes after the pass before it. So in these passes a rest of 9 or 14 minutes after a default run of nearly two hours left Q1 about 300 ms less to spare than a pass that came long after it: give the laptop a longer rest between the default run and the real-window pass. | Each phase's gate; whenever the graph or its loading changes |
| 11 | GitHub moves the `ubuntu-latest` CI runner to Ubuntu 26 from 2026-10-19 (an annotation on every run). The Linux job and the set-up script may need attention then, and its git may change (risk 26). By run 40059391404 (2026-10-24, phase 6's gate) the move had not happened yet for this project: `ubuntu-latest` still ran the `ubuntu-24.04` image with Git 2.55.0, and the run still showed GitHub's migration notice. In run 41190572421 (2026-10-31, phase 7a's gate) it still ran the `ubuntu-24.04` image, now with Git 2.56.0, as in every run of phase 7a's branch from its first, 40848974456 (2026-10-29); master's 40679200453 of 2026-10-28 still showed 2.55.0 (risk 26). During phase 7b the move came: `ubuntu-latest` ran the `ubuntu-24.04` image in the branch's first 12 runs, 41277720820 to 42216604663 (WP-LF's merge, 2026-11-06), and the `ubuntu-26.04` image (20261101.186.1, still Git 2.56.0) in the other 17, from 42285038375 on `972c144` the same day to 42522049151. The Linux job and the set-up script needed no change, and none of those 17 runs failed on the image: `ubuntu-latest`'s one failure among them, in 42301846503, was a race in the app's window close, fixed at `85d599c` (the "Runner Image" group of each run's `ubuntu-latest` log; the runs in the CI section of phase 7b's `status.md`). | Settled; risk 26 follows its git |
| 12 | The default test run took about 45 minutes at phase 5's gate on the development laptop, about twice phase 4's: 45 min 37 s for 3,227 tests on `4281f4c` and 43 min 22 s for 3,233 on the gate commit `1b2e734`, where the seven test projects, running side by side, ended after 12 min 8 s (phases 0 to 2's walk-through), 26 min 15 s (phase 4's), 29 min 50 s (`VisualCommit.VisualTests.Phase5`), 38 min 19 s (`VisualCommit.Git.Tests`), 41 min 43 s (`VisualCommit.VisualTests.Phase5Undo`), 41 min 46 s (phase 3's) and 43 min 8 s (`VisualCommit.App.Tests`); design 15.6 had expected 30 minutes or more. Phase 4's gate had taken 21 min 52 s for 2,411 tests in five projects (its report gives each project's time), phase 3's 19 min 15 s for 1,756 in four. Phase 5 added two walk-through projects (89 test methods, D181; part U alone is 55 methods and about 170 operations, design 15.8) and records every action for undo, which adds git calls to every operation of every earlier check (risk 25). On CI, on the gate commit, the Windows Test step took 25 min 15 s, macOS's 16 min 13 s, `ubuntu-latest`'s 3 min 26 s and the two containers' about 4 minutes. The Windows Test step had reached its limit of 24 minutes in run 38853641791, so D184 raised the three platforms' limits to 40 minutes for the Test step and 50 per job (amending D152, which had raised them from 14 and 22 in phase 4); when the project is over, the owner decides whether a CI run of 30 minutes or more is reasonable (D134). The run is bound by the processors, which git's processes and the virus scanner keep at about 90 percent, so running more tests side by side only slows each git call; other load or a hot laptop lengthens it. The owner set up the virus-scanner exclusion for the repository folder and `%TEMP%\VisualCommit.Tests` on 2026-10-12. Within that, each test assembly has one headless UI thread and xunit runs the tests of one class one after another, so the longest project sets a floor. Under the run's load every process start grows: `git mergetool`, a shell script that starts over a hundred processes, took 32 to 64 s to start its tool (2.8 s alone), and the two presses of a scripted double-click lay further apart than the 500 ms that make one (design 15.9). Done in phase 3 (D132, D133): the phase 3 walk-through is the project `VisualCommit.VisualTests.Phase3`, the largest test classes are split (`RepositoryOperationsTests` into 12 classes in 4 collections, `ScenarioTests` into 2, the pull-parity tests into 9), shared scenario templates are built once per process, and the hang watchdog leaves a test's wait for them out of its time. Done in phase 4 (D151): its walk-through is a fifth project, `VisualCommit.VisualTests.Phase4`, and its Git-layer and App tests are split by subject as phase 3's are. Done in phase 5: its walk-through is two projects, with every undo check one action per test method (D181); `TempRepo`, which every test project uses, copies a blank git folder made once per process and starts fewer git processes, and the reflog scenario is built with 88 git calls instead of 145 (`7e7d99d`); the headless harness waits 30 s by default (`5bdd1de`); a wait for `git mergetool` to start has its own limit of 120 s and does not count against the hang watchdog (D185); the headless double-click stops the window's input clock for its presses (`HeadlessInputClock`, risk 22); and step 25's review cut the undo history's reads (risk 25). Still possible: scripted checks sharing an app launch where their scenarios allow, keeping the pull-parity templates between runs as the 100k-commit repository is kept, and the walk-throughs in more projects, which helps only where a project's single UI thread, not the processors, sets the length. Checks that waited 5 seconds for an outside change (phase 1's check 18, phase 2's check 20) timed out under load, and checks that looked at the view before it was drawn failed under it (phase 1's checks 12 and 18 during phase 3's gate); since `594f9fb` the outside-change waits are 30 seconds and those checks wait for what they assert, and since phase 4 phase 1's clone checks are held at a fixed percentage instead of racing the clone (D150). Let the laptop rest before a gate's run. At phase 6's gate the run was nine projects and 5,037 tests, 56 min 25 s on the gate's commit (44 min 34 s to 56 min 25 s over four rounds; the projects' ends in CLAUDE.md), with a git call 10 to 15 times as long as alone (phase 5's seven projects: 5 to 8): two runs failed on that alone, a test of five scenarios past the watchdog's 180 s and a shared template past it, and a pop past check 23's 30 s; D237 holds shared preparations to 10 minutes of their own and makes the walk-throughs' waits 60 s, and a test that holds several scenarios' work is split. Fewer tests side by side would lengthen CI, where the runners have fewer processors. During phase 6's build, agents ran about 37 whole-project runs in worktrees, several at once, beside the lead's 4 full runs (about 3.7 hours), and that load made tests fail (the review before phase 7, 2.3). Since D245 (2026-10-28) builders and fixers run only the test classes they touch, whole-project runs and the run at the root are the lead's and go one at a time through `scripts/heavy-run.sh`'s lock, the full default run is the gate's (and a wave's merge's), and a session's start makes one only when the machine changed or the tree holds unpushed code; failures that only load or the whole run shows then come first at the gate. At phase 7a's gate, with eleven projects, eight at once, on a rested laptop, the watchdog stopped `VisualCommit.Git.Tests` in a test of 14 s alone (35.9 s in the project alone): D237's rule was applied to every test of that project of 25 s or more in the project alone (41; 11 split, 30 kept; phase 7a's design 21 item 1). Still at risk there: single undo chains of 30 to 40 s in the project alone (`UndoStepBranchTests`, `UndoStepFileTests`, `UndoStepTests`), which no split shortens. The next rested run stopped `VisualCommit.App.Tests` in one such chain (`UndoRecordingTests`' hard reset, undone and redone), and the owner chose fewer projects side by side over a limit of their own (D264 kept the limit for G23): since D343 the laptop's whole run through `scripts/heavy-run.sh` runs at most four projects at once, where `dotnet test` ran eight. With it the third rested run passed in 1 h 8 min 39 s (5,950 tests, no stop), where the rested run on `7015753` with eight at once had taken 1 h 12 min 6 s (5,895). Phase 1's in-memory layout of 100k commits (`GraphLayoutTests.A_100k_commit_history_with_a_merge_every_50_commits_lays_out_in_under_2_seconds`, no git) took 2,620 ms against its 2 s once under the run's load (WP-B's whole run on `e40ddc8`) and passed alone three times right after; no later whole run recorded in `docs/status.md` failed it. The counted round's default run on `a9d02c1`'s code passed 5,948 of 5,952 (4 skipped) in 1 h 12 min 18 s, rested, at most four projects at once, with no stop; `VisualCommit.App.Tests` ended last, at 72 min 15 s (59 min 31 s of its own). At phase 7b's gate, with twelve projects (`VisualCommit.VisualTests.Phase7b` new, 179 tests), four at once, started straight after D246's two runs: 7,888 of 7,892 tests passed (4 skipped) in 1 h 31 min 48 s with no watchdog stop, where its design 0.6 estimated 78 to 85 minutes. `VisualCommit.Hosting.Tests` and `VisualCommit.App.Tests` ran from the run's start and ended after 53 min 14 s and 85 min 59 s (alone on `6333de6`, 13 min 42 s and 28 min 24 s), the 7b walk-through took 15 min 33 s of its own (3 min 47 s alone), and phase 5's undo walk-through ended last, at 91 min 41 s (each project's end in the report's "Run"). The order in which the test platform starts the projects is not fixed: the whole runs at waves 1, 2 and 3 (1 h 16 min 56 s, 1 h 14 min 20 s and 1 h 20 min 21 s) each began with another four (its design 16.2), and WP-N's took 1 h 37 min 45 s for 7,817 tests, `VisualCommit.App.Tests` 1 h 32 min 56 s of its own. Design 0.6's split by D237's rule found no test of 25 s or more alone but one whose 18 s are the shared template's, so nothing was split (the Notes of phase 7b's `status.md`, "The lead's runs after the review's fixes"). At phase 8a, with thirteen projects (`VisualCommit.VisualTests.Phase8a` new), four at once: the whole runs at the waves' closes took 1 h 32 min 7 s (7,993 tests, wave 1), 1 h 46 min 41 s (8,283, wave 2) and 2 h 9 min 24 s (8,436, wave 3, the docs' agents reading and writing beside its first 25 minutes; `VisualCommit.App.Tests` 1 h 45 min 13 s of its own), and 1 h 44 min 41 s for 8,514 tests after the row list's fix; step 19's three took 1 h 40 min 18 s (8,882 tests, 2 failed), 1 h 43 min 44 s (8,903, 3 failed) and 1 h 35 min 2 s (8,907, none failed), where its design 0.6 had estimated 1 h 44 min to 1 h 52 min. The five failures were Windows' Storage Sense emptying the temp folder's empty folders while C: was low (D468; risk 48). In a run on `cb31275` that counted for nothing the watchdog stopped `UndoRecordingTests`' hard reset at 181 s, the test whose stop at phase 7a's gate led to D343; the review before the gate kept four projects at once (D343; phase 8a's design 13.1, ruling 99's S19-32), and `OperationsHarness`' limits now name the step that runs out. At phase 8a's first counted round, on `e4b323c`, which does not stand (ruling 115), the first default run lost two projects to the watchdog while the lead's other work ran beside it on the laptop (eight read-only agents, two picture inspectors, a download of CI's pictures and, from about 19:16, a headless browser; both stops began within a minute of a browser's start): phase 3's walk-through in `Phase3ChecksD.Check_19_push_rejected` and `VisualCommit.App.Tests` in a fact of `RepositoryOperationsProtectTests`, with no assertion failed and the cause not established; the second, with nothing else running, passed 8,920 of 8,927 tests (7 skipped) in 1 h 48 min 52 s with no stop. A counted run runs alone, with no agent, browser or download beside it (phase 8a's design 13.1, ruling 115); before that second run five leftover `git fsmonitor--daemon` processes were stopped, as the earlier handoffs' gate steps say. The default run of the counted round that stands, on `0f9099a` (ruling 116), ran so, with the closing docs' drafts set aside, the idle build servers shut down and two leftover `git fsmonitor--daemon` processes stopped: 8,929 of 8,936 tests passed (7 skipped) in 1 h 53 min 8 s (7,004 s with the build and the lock), four projects at once, with no stop. Each project's time from its start to its end, the others beside it: `VisualCommit.App.Tests` 1 h 35 min 25 s, `VisualCommit.Git.Tests` 1 h 10 min 35 s, `VisualCommit.Hosting.Tests` 1 h 9 min 54 s, phase 5's undo walk-through 52 min 29 s, phase 8a's 33 min 53 s, phase 3's 24 min 1 s, phase 5's 20 min 45 s, phase 7b's 16 min 24 s, phase 7a's 16 min 23 s, phase 6's 16 min 17 s, phase 4's 13 min 9 s, phases 0 to 2's 7 min 3 s and part L's 4 min 59 s. | Each phase's gate |
| 13 | On Linux, .NET's file watcher puts a watch on every folder of the working tree, ignored ones such as `node_modules` included. A very large tree can use up the user's inotify limit, which other programs share; the watcher then logs a warning and changes made outside the app show only after the next refresh. Phase 2 starts the watcher off the UI thread, so it no longer holds opening up. | When watching changes, or Linux users report it |
| 14 | The askpass helper (`src/VisualCommit.AskPass`, D82) is framework-dependent: it runs on the .NET runtime the app uses and must lie where `AskPassLocator` looks. A package that leaves it out, or ships it without a runtime it can use, makes every credential and ssh prompt fail; nothing but a prompt shows it. Since phase 7a the same program is also the app's credential helper for the account (`--vc-credential`, D255), which git runs through its shell (in the real window through Git for Windows' `sh` from a folder with a space); the account binds git only while the broker finds it (`AskPassBroker.HelperFound`, phase 7a's design 0.2), so a package without it also keeps the token from git. Since phase 7b it is the helper for GitLab's and Azure DevOps' accounts too (D350, D359), and the real window runs it through Git for Windows' `sh` for both new hosts (part B's two clones). Since phase 8a a missing helper is no longer shown by a prompt alone: it is a state of the first-run screen, `Part of VisualCommit is missing`, with the path looked at and `Continue without it`, and the status bar reads `Git {version} (helper missing)` (phase 8a's rulings 9 and 43; F11, proven by the scripted check only). | Phase 8b |
| 15 | OpenSSH older than 8.4 ignores `SSH_ASKPASS_REQUIRE`: on macOS and Linux, when the app was started from a terminal, such an ssh asks there instead of in the app, and the operation waits until it is answered there or cancelled (Ubuntu 20.04 ships 8.2); on Windows the app starts git without a console window, so no terminal shows the question, and nothing was measured with an old OpenSSH there: ssh may wait or fail. Since phase 8a (D424; ruling 7 as ruling 100 amends it) `AskPassBroker` writes one Info line per session, at its first Ask or Deny scope, since it cannot tell ssh from HTTPS without the remote's URL, in the running system's words, and the guide's `troubleshooting/sign-in-and-ssh.md` says what to do; there is no `ssh -V` probe, which would cost a process at every operation (D424), and the first-run screen says nothing of it (phase 8a's design 13.4 item 6). | A report |
| 16 | A network operation the user stops can leave lock files, which make every later fetch fail (seen on Git 2.36, D81). After a Cancel the app lists only what a fetch or push can lock (`refs/**`, `packed-refs.lock`, `shallow.lock`, `config.lock` of the git folders), only those unchanged 0.5 s later, and removes one only if its time and length are still the same (D126). A lock that a slow process of another program holds without writing to it for that long would be listed too; the user decides. A network operation stopped by "Quit now" leaves its locks with no report. | When users report locks |
| 17 | Settled in phase 4 (D146): `CHERRY_PICK_HEAD` and `REVERT_HEAD`, refs rather than files under the reftable ref format (Git 2.45 and later), are read in one place (`GitRepository.ReadPseudoRefAsync`): as files, and through git (`rev-parse --verify --quiet --symbolic-full-name`, so that a branch of that name does not answer) only when the repository's configuration names reftable and `MERGE_MSG` or `sequencer/` exists, so a files repository with nothing in progress makes no git call. `PseudoRefTests` run a real reftable repository with a stopped pick on CI's gits from 2.45 on (2.55 and 2.56 from 2026-10-15, 2.56 alone since 2026-10-29 (run 40848974456 on); on the containers' 2.43 the test asserts that git refuses the option, risk 26); the development machine's Git 2.36 could not make one, so only CI proved it until the machine moved to Git 2.56 on 2026-10-28 (D248). | Settled |
| 18 | The real-window pass grew from about 6 minutes (phases 0 to 2) to 11 min 11 s with phase 3's checks at its gate (11 min 42 s with the build), to 14 min 4 s for phases 0 to 4 (43 tests) at phase 4's gate, which added 10 launches (the design had estimated 5 to 7 minutes more), and to about 25 minutes for phases 0 to 5 (59 tests) at phase 5's gate: 26 min 26 s on `4281f4c` and 24 min 57 s on `1b2e734` (design 15.6 had estimated 8 to 11 minutes more). Every real input waits 400 ms and the first menu of a launch takes up to about 2 s. It takes over the mouse for all that time, needs an unlocked desktop and runs only on the Windows machine, so a phase is closed only by a session there (risk 1). A commit that changes more than docs after an inspection means both passes again (since D246 the real-window pass alone when the commit changes only `tests/VisualCommit.RealWindowTests`, and the new phase's real-window checks run twice before the counted round), so phase 3's gate ran the pass three times, and so did phase 5's (once on `4281f4c`, twice on `1b2e734`, where the first failed on Q1 alone on a hot laptop, risk 10): collect all fixes before running it again, and rest the laptop before it. Shared launches keep it down; later phases add to it. Phase 6's gate: 67 tests (phases 0 to 6) in 25 min 29 s to 27 min 39 s, four times (once on `774464d`, then on `59b2316`, `639db4a` and `b561b7c`): the fixes of the first round's inspection, and twice S14's Windows Open dialog, which the harness now waits for and confirms with Enter, since the dialog opens again in the folder the last run chose a file in (the system folder, whose long file list a search for the Open button walks first). The operating system's dialogs keep such state between runs. Phase 7a's part has one launch per part (D319): 2 min 55 s alone in D246's second run (3 min 17 s with the build), where its design 0.6 estimated 3 to 5 minutes; near its end it opens one tab in the owner's default browser on the fake's page (D321), which the owner closes before the next run. D246's two runs, made for the first time, found a fault of a check's own lookup in the first, fixed before the counted round (phase 7a's design 21 item 3). The whole pass of phases 0 to 7a: 72 tests in 31 min 6 s (1,920 s with its build) at phase 7a's gate, within design 0.6's 31 to 33 minutes. Phase 7b's part has five launches, two per provider and one for the three accounts (its design 14.2), 13 captures and no browser: 3 min 50 s and 3 min 26 s alone in D246's two runs (250 s and 214 s with the build and the lock), under its design 0.6's 4 to 6 minutes, and both passed at once. The whole pass of phases 0 to 7b: 77 tests in 33 min 35 s (2,091 s with its build) at phase 7b's gate, straight after the default run, under its design 0.6's 36 to 37 minutes. Phase 8a's part (`Phase8aRealWindowPass`, its design 12) has 13 launches, one of them with a second process of the app, and needs `winget` on the PATH, or launches 7 and 8 fail (D449, D453); `Phase8aOrderTests` (10 tests, no window) run in the whole pass and alone under their own `--filter-class "*Phase8aOrderTests"`, not under the part's filter of D246's runs. The part took 4 min 44 s alone on the merged tree on 2026-11-24 (294 s with the lock and the build up to date) and 5 min 41 s and 5 min 5 s in D246's two runs at the gate, where its design 0.6 estimated 10 to 12 minutes; its first run on merged code failed launch 6, as about two runs in five then did, on the harness's search of the desktop through UI Automation, and the harness now finds every window by its handle (ruling 105; risk 3). The whole pass of phases 0 to 8a at phase 8a's first counted round, on `e4b323c`: 124 tests, 119 passed and the performance series' five facts skipped, in 41 min 34 s, under its design 0.6's 43 to 45 minutes. In the counted round's first pass, on `0f9099a`, phase 3's check 22, which had passed in every pass before, failed with no fault of the app: a look of its wait for the Remotes dialog's rows met a `COMException` while the dialog built them again, and the wait let it end the check (D473; risk 3). The harness's fix changed `tests/VisualCommit.RealWindowTests` alone, so the pass ran again alone on `750eb33`, as D246 allows, with the scripted pictures the same files (phase 8a's design 13.1, ruling 116): 145 tests, the 124 of before and `WaitForRuleTests`' 21 with no window, 140 passed and the five facts skipped, in 40 min 56 s (2,484 s with the lock and the build). | Each phase's gate |
| 19 | Auto-fetch over ssh has no timeout (D125): the only way to set one through git is `core.sshCommand`, which would replace the user's own. A stalled ssh connection holds the repository's queue until the user cancels the auto-fetch from the status bar, which offers Cancel while a user operation waits behind it. HTTP gives up by itself after 60 seconds below 1 byte per second (`http.lowSpeedLimit`, `http.lowSpeedTime`). | When users report it |
| 20 | "Open file" asks before opening a file that can run programs by a fixed list of extensions on every platform, plus the executable bit on Unix (D130, `RepositoryOperations.Files.cs`). Since phase 6 the settings screen adds the user's own extensions to that fixed floor, and Windows' `PATHEXT` is read too (`ToolCatalogues.PathExtensions`); the system's file associations and macOS bundles other than `.app` are not, so a runnable type known only there opens without a question (phase 6's design 12.8). | When a missing type is found |
| 21 | Tooltips are drawn on the window's overlay layer everywhere since phase 3 (D105), so in the real window a tooltip is inside the window and its capture. A phase 0 to 2 real-window picture that showed a tooltip as a window of its own can now differ from the one its gate inspected; the phase 3 report lists such differences under "Changes to expected results". | Phase 3's gate |
| 22 | Phase 3 relies on Avalonia 12.1.3 details that an update can change: the menu adapter's automation peer, which offers Invoke through `MenuBase.Close()` and `MenuItem.ClickEvent` (D105, `ContextMenuAdapterTests` catch a change); Shift+F10, whose gesture list was read on Win32 and headless only, so on macOS and Linux the app raises the request itself if the list lacks it, which CI's headless runs cannot tell apart (D106); and `SecretTextBox`, whose peer hides typed secrets because Avalonia cannot report a password box (D130). Since phase 5 the headless harness's double-click stops the input clock of Avalonia.Headless's internal `HeadlessWindowImpl` (its private `_st`) through reflection, and throws when an update moves it (`HeadlessInputClock`, design 15.9). Since phase 7a it also relies on how Avalonia's `MouseDevice.MouseDown` counts presses: a press counts on from the last one when it lies within the double-tap size (a 4 by 4 rectangle) around it and no more than the double-tap time (500 ms) after it by the window's clock, whatever control either went to; so `DoubleClick` waits out that time after a press at its spot. An update that changed the rule would make the wait needless (it costs up to 500 ms after such a press) or not enough; `DoubleClick` throws when its presses are not counted 1 and 2, which catches a wait that is not enough at once, and `ShellDriverTests`' double-clicks at the rectangle's corner and just outside it catch a changed rectangle. Phase 6's keys rely on more of them, read from the assemblies' IL only (phase 6's design 0.7): `KeyboardNavigationHandler` taking Tab with any modifier, `TextBox` typing a line break for Enter with a modifier when `AcceptsReturn` is set, and `MenuItemAutomationPeer`'s accelerator key; none of them was checked on a Mac or a Linux desktop (IBus and Fcitx take Ctrl+Shift+E; the macOS application menu). Phase 8a relies on more of them: Fluent's `TextControlPlaceholderOpacity` resource key, which the app's tokens set to 1 (D418); Avalonia making a peer's UI Automation node only when a client reaches it, and only a node forwarding its events, so the graph's selected row speaks only to a client that has read below the graph (D455, risk 53); the macOS application menu's About item through `NativeMenu` (`App.axaml`; `AboutEntryTests` run it under the headless platform, no Mac has); and the virtualizing list's estimate after a change above the rows in view, which can draw the rows in view at other places with new controls (phase 8a's design 1.17; `LeftPanelScrollTests`). | Whenever Avalonia is updated |
| 23 | Pull is the app's own fetch and integration, chosen as `git pull` would choose it (D84), and since phase 4 also a rebase chosen for one pull, as `git pull --rebase` (D147). Git may change its rules; `PullParityTests` and phase 4's `PullRebaseParityTests` (with its settings and fork-point classes) run on CI's newer git and catch that. Not mirrored: submodule recursion after a pull, and options that exist only on pull's command line. Fetching all remotes runs one `git fetch` per remote, one after another, so `fetch.parallel` no longer applies and many remotes fetch more slowly (D109). Since phase 6 Pull reads its configuration before the fetch (D219), and a rebase chosen for one pull reads `pull.ff` alone, as `git pull --rebase` does (D231); the parity tests compile and compare unchanged, because `PullIntegrationOptions` is optional. Since phase 8a Pull reads a boolean setting by the value in effect: when `git config --type=bool --get <key>` fails because a lower scope holds a value that is no boolean (a global `pull.ff = only` under a repository's `pull.ff = 1`), a second read takes the value in effect alone (D465). | Whenever git or the pull rules change |
| 24 | Settled in phase 4 (D139): the backup refs under `refs/visualcommit/backup/` (D67, D83), which keep their commits and snapshots from git's garbage collection, are cleaned up automatically. A backup (one operation's refs) goes after 30 days, then the oldest while more than 50 remain; nothing goes while an operation is in progress in any worktree, nor the restore bar's snapshot, nor a ref that fits no known scheme. It runs at the end of every operation that saved a backup and in the background when a repository opens, says nothing on screen and logs a line per backup removed, which since phase 5 the activity log shows too (D156). Since phase 5 the clean-up also keeps the backups that the session's undo entries need, which still count toward the 50 (D171), and the backups list restores what is left (D172). | Settled |
| 25 | The undo history's recording costs git calls in every operation it records: the refs and state that the action's kind declares are read before and after the action, inside its own operation (design 7.7, D167). The design expected about two git calls before and after; built, a pop made 22 git calls inside its operation, and a stash 17 in all with the refresh after it (1.2 s alone on the laptop, 3 s on CI). Step 25's review cut them (a pop to 16, a merge from 14 to 10, a hard reset with a local change from 25 to 18; design 15.9 lists each kind): one read of HEAD shared with the operation's own checks, the git folder read instead of the whole status where only an operation in progress matters, a stash's paths read once, and a file's bytes kept only where an undo writes them back. The reads still lengthen every recorded operation, the default run and CI (risk 12); an operation a later phase adds and records adds its own. Phase 6 adds the ignored-files check's reads before each covered merge, pick, revert, rebase and stash apply or pop, and none to a checkout or to the option on a fast-forward: merge 1, pick, revert and a list 2, rebase 5, or 7 with a merge in the range, interactive rebase 3 or 4, squash 3, the stash 1 after D179's count (phase 6's design 9.4 to 9.8); a `--ff-only` pull whose check finds a file adds `merge-base --is-ancestor` (9.10); a directory-rename read only when nothing else is in the way and a test of the disk passes (9.6); and every push of a branch, and undo's and redo's steps that write a branch on a remote, one `git config --null --get-regexp ^remote\.` (10.4). `IgnoredInTheWayTests` and `RepositoryOperationsKeepIgnoredTests` pin the counts. Phase 7a records the push that Create makes as any push (D314); signed in, a tab's refs read also reads the URL rules, one `git config --null --get-regexp ^url\.` (D281, an optional form of `GitCallBudget`), and a dashboard row without a tab reads them at each fetch or pull's start while the account binds git (D332); signed out nothing is added (its design 0.2). Phase 7b keeps these reads for the three providers' accounts and adds two: a tab's network operation started inside D290's window, before any of the tab's reads of the URL rules has answered, reads them itself, once, when no read is in flight (D388, D332's read), and a clone reads each conditional include's file, one git call per `includeif.*.path` and none without one (D391). Phase 8a adds `git worktree list --porcelain` before a fast-forward of a branch that is not checked out here, on every git (D420; `GitRepository.FastForwardBranchAsync`), and a second `git config` read of one of Pull's boolean keys only when the first read fails (D465). | Whenever an operation is added or its recording changes |
| 26 | The gits on CI drift and differ: they come with the runners' images, not from the project. From 2026-10-15 Windows ran 2.56.0.windows.2, macOS 2.56.0 and the two containers 2.43.0; `ubuntu-latest` ran 2.55.0 until 2026-10-28 (master's run 40679200453) and 2.56.0 on its `ubuntu-24.04` image from 2026-10-29 (every run of phase 7a's branch, 40848974456 to the gate's 41190572421; risk 11), and on its `ubuntu-26.04` image, still 2.56.0, from phase 7b's run 42285038375 (2026-11-06) on, so no CI run of phase 7a ran 2.55 and only the containers' 2.43 differed from 2.56; the development machine runs 2.56.0.windows.4 since 2026-10-28, 2.36.0 before (D248), and has Visual Studio's 2.55.0 for checks by hand (risk 28). Until phase 8a no CI job ran a git older than 2.43, and neither the app's minimum (2.30) nor the tests' (2.32) was run; since phase 8a a sixth job, "ubuntu 22.04 container, git 2.34 (the floor)", runs the default tests on Ubuntu 22.04's 2.34.1, the app's minimum and the tests' (D406, D420): before phase 8a's floor package it failed 20 of 7,889 tests (run 44479788558); it first passed in run 44727459663, and since then it failed only in run 45421164866, on the guard's file-modes test, which failed in every job off Windows (D464 (4)). The tests that assert git's own words only on 2.36 still assert them on no machine; for one behaviour, P27's refused merge, the review before phase 5's gate tried gits from 2.32.0 to 2.39.0 (design 15.9). What differs, and what the code and the checks now allow for: a `git stash pop` refused by local changes exits 128 on 2.56 and 1 before (phase 3's check 15b, `281dd69`); a merge refused for something staged writes a no-op entry into HEAD's reflog on 2.38 and later and none before (P27, design 15.9); Git 2.55.0 and Git for Windows 2.56.0.windows.2 ignore a path under `--no-walk`, which 2.36 and macOS's 2.56.0 honour, so the Files search lists the paths itself (design 0.3; a scenario test that took 2.55's behaviour for the rule failed on macOS in run 38738421993); a merge refused for local changes exits 2 with other words on 2.55 (the report's PD10); `read-tree` overwrites ignored files without a word from 2.34 on, and D169's ignored-files guard makes undo the same on every git. A new git on a runner can fail a check that passed the day before, as phase 3's check 15b did in run 38723225901 on 2026-10-15. Phase 6's facts were taken on 2.36 and 2.55 (its design 0.3 to 0.5), and from phase 7a a design's facts are taken on 2.56, with 2.43 and 2.55 from CI where versions differ (D248; 2.43 alone since 2.55 left CI); its tests that depend on the git version follow the git that runs (`m` read with `--type=bool`, `GlobalFile`, and `FetchRefspecsParityTests`, which leaves out the symbolic `refs/remotes/origin/HEAD` that Git 2.48 and later create from the server's HEAD) or recorded outputs (`--tool-help`, 2.36's `name consists only of disallowed characters`). CI's 2.56 showed two more differences: it refuses a worktree whose branch has no commit with `gitfile does not point to a valid repository`, which the app takes as "not a git repository" (design 0.1), and `blame --abbrev` prints longer hashes, so the tests read `blame -l`. Git 2.55 and 2.56 refuse `git remote add team/fork` beside a remote `team` (`is a subset of existing remote`), which 2.36 and 2.43 allow, so a test writes such names as configuration, as an older git or a hand edit leaves them (run 40004153661). Phase 7a's credential path follows the version: below 2.31 the account is not used for git (D277), from 2.46 `http.<url>.proactiveAuth` sends the token with a bound URL's first request (D340), and between them a netrc entry answers first (risk 35), a branch that on CI only the containers' 2.43 ran until phase 8a and the floor's 2.34.1 runs too since (`AccountCredentialTests` read the version git reports), while D277's branch below 2.31 is gone with the floor (D420); its design's git facts measured on 2.56 alone (the fake's `http-backend` paths, a fork's `refs/pull/<n>/head` fetched into `FETCH_HEAD`) held on CI's 2.43 to 2.56 (its design 15 point 3; 17 item 7 for the `FETCH_HEAD` fetch; for the `http-backend` paths, CI's runs from wave 1's 40878208671 on, where no job failed a test of the fake's git). Since the development machine's move to 2.56 its status bar shows `Git 2.56.0`, so every earlier picture that shows the status bar differs in that box from the lists of pictures of phases 1 to 6's gates (phase 1's `01` and `02` in 386 and 376 pixels of a 54×10 box, 2026-10-28); a later gate compares such pictures by region (risk 32). Phase 7b's credential tests met three more differences: below 2.46 an `<organization>@` URL's first request carries no credentials and its retry the user with an empty password before git asks the helper (the containers' 2.43, run 42339567989; phase 7b's design 8.8's As built, on 8.6's first row); behind a relative 301, Git for Windows 2.56.0.windows.2 (libcurl 8.22.0) and Homebrew's 2.56.0 keep another user's name where 2.56.0.windows.4, Ubuntu's 2.56.0 and 2.43 drop it (run 41311809889; the same As built); and Homebrew's 2.56.0_2 over curl 8.23.0 loses a URL's user after the first 401 (risk 43). CI's Windows runner ran 2.56.0.windows.2 in run 41311809889 (2026-11-01) and 2.56.0.windows.4 in 42454621322 (2026-11-08), as `GitUrlUser`'s `artifacts/visual/git-url-user.txt`, which CI uploads for each job, records (D389). What git 2.34.1 words or does otherwise, from git's sources (phase 8a's design 3.3.3): a branch made with tracking and ambiguous tracking are worded otherwise before 2.35; `merge.conflictStyle=zdiff3` exits 128 before 2.35; `fetch . a:refs/heads/b` moves a branch checked out in another worktree before 2.35 (risk 49); and below 2.36 a stash is named after the part of its branch after the last `/`. `GitWords` gives the tests each version's words, the scenarios' stashes are rebuilt so that the ids the reports pin hold on every git, and the app's own stashes are compared in git's own words. Ubuntu's 2.34.1 carries backports (`safe.directory`, `protocol.file.allow`), so "tested on 2.34.1" means Ubuntu's (its design 3.9). In phase 8a's last runs, 45917903492 on `53e07c4`, 45965143178 on `e632e72` and 46007767846 on `750eb33`, the Gate commit, each job's `git-url-user.txt` reads Windows' 2.56.1.windows.1 (runner image 20261116.315.1 in all three), macOS's Homebrew 2.56.1, `ubuntu-latest`'s 2.56.1, the containers' 2.43.0 and the floor's job's 2.34.1, each of them `Kept`. | Every CI run; whenever a runner's git, or the development machine's, changes (risk 11); the floor's review in May 2027, when Ubuntu 22.04's standard support ends (D406) |
| 27 | Disk space while sub-agents build in parallel worktrees: a built worktree under `.claude/worktrees/` takes about 5 GB on D:, and the tests' repositories, the shared 100k-commit one included, lie in `%TEMP%\VisualCommit.Tests` on C:. During phase 5 the development machine's disks came close to full (under 10 GB free on C:, under 20 GB on D:). Remove a merged worktree at once (section 16 of `phase-5-design.md`, and the phase 3 and 4 handoffs) and look at the free space before starting several worktrees or a gate. In phase 6 a built worktree took about 6.4 GB, and at most 3 were built at once (phase 6's design 18). The review before phase 7 (2.5) found 94.8% of `bin` to be SkiaSharp's and HarfBuzzSharp's native files for 21 platforms with their debug symbols, 558 MB in each of 16 output folders. Since 2026-10-28 the build trims them, with every package's files for other platforms on Windows (D239): measured in the main tree, the 40 `bin` and `obj` folders under `src/` and `tests/` that were built again went from 6.96 to 0.64 GB (a test project's `bin/Debug` from about 589 to about 49 MB), and D: gained 8.60 GB, 2.27 GB of it the 8 folders of outputs nothing local runs, removed and not built again, to 27.2 GB free; the review measured a built worktree with the real-window Release chain at 0.658 GB in place of 6.985 GB, so three worktrees add about 2 GB instead of 17 to 21 GB. NuGet's http cache was cleared the same day (2.8 GB on C:, D240). C: and D: are two partitions of one SSD. During phase 8a, on 2026-11-24, C: had 4.7 GB free of 350 GB (1.3 %), and Windows' Storage Sense, on with "delete temporary files that my apps aren't using", emptied the empty folders under `%TEMP%` three times in about eighteen hours while C: was low, which failed five tests of step 19's whole runs (D468; risk 48). On 2026-11-26 the owner had freed C: to 58.5 GB (16.7 %); D:, which holds the repository and the lead's working files, then had 5.8 GB free of 125 GB, 3.7 GB during the closing docs' drafting on 2026-11-27 and 3.3 GB after the gate's counted round that day, where it had 17.2 GB on 2026-11-24 and 27.2 GB on 2026-10-28; a whole run's pictures take about 120 MB. Storage Sense stays on: look at both drives before a gate (`Get-PSDrive C`; CLAUDE.md's note on the cleaner). | Each phase built in worktrees; each gate |
| 28 | Explained on 2026-10-18 (phase 6, step 4): Visual Studio's Git 2.55.0.windows.5 is a trimmed distribution without `git http-backend` (`git: 'http-backend' is not a git command`), which the credential test server `AuthRemoteServer` runs as its CGI through the same git. With that git first on `PATH`, every test against the server fails: 6 of the 49 tests of `RepositoryOperationsFetchTests`, `…PullTests`, `…PullRebaseTests`, `…PushTests` and `…RemotesTests` (the held fetches time out waiting for their percentage, the credentials tests see the server's errors), while the tests against bare remotes on disk pass. Neither the app nor Git 2.55 is at fault; CI's gits ship `http-backend`. Phase 6 makes the server say so when it starts, instead of a test timing out. A local run with that git still says nothing about the HTTP paths; CI does. The tests' isolation removes `MSYSTEM` (phase 6's design 8.12, 17.2); that git also lacks `sleep`, so phase 6's held fetch and phase 5's waiting merge tool do not hold with it first on `PATH`. | Settled; whenever a trimmed git is used for local runs |
| 29 | Under `GIT_OPTIONAL_LOCKS=0` (D47) a stale index makes every status read hash the touched files again: 3.1 s per read in a 20,000-file tree with `core.fscache=true` (6.1 s without) until something writes the index. The dashboard's reads and the tabs' pay it alike (phase 6's design 0.1). Refreshing the index would be a write, which is the owner's call. | When users report slow reads |
| 30 | `remote.<r>.mirror = true` makes every push the app makes to that remote fail with `fatal: --mirror can't be combined with refspecs` (Push, the push dialog, tags, deletions, undo's `RemoteRef`); the error dialog shows git's words (phase 6's design 10.8 #2). | A report |
| 31 | The ignored-files check is the app's, not git's (D196): a window of one process start lies between its `check-ignore` and git's write (phase 6's design 9.12); nothing is checked again on Continue, Skip or Abort (9.14); a rebase onto an upstream that moved back, and git's own range, leave fork-point gaps (9.6); a plain rebase over a merge is refused for a boundary commit's file, which git would keep (9.6). Git 2.30 to 2.33's rules for ignored files were never tried (0.4); since phase 8a the floor is 2.34.1 (D420), which reverts 2.34.0's fault in `.gitignore`'s directory patterns that the guards rely on, so those versions are refused as too old, and CI's floor job runs the ignored-files tests on 2.34.1. | A report |
| 32 | Settled at phase 6's gate: it inspected nearly every earlier picture again (the Settings button in every toolbar, the welcome page, the tooltips' colours, menus with keys, the light colours; phase 6's design 16.1), and phase 5's method carried it: reference pictures made on `master` before the gate (`D:/Projects/Personal/gate6-reference/`, outside the repo), the comparison by window region (`scripts/compare-pictures.py`), thirteen Opus inspectors on all 1,329 files of round 1 and two more on the 66 that changed by round 4 (17.7; the phase 6 handoff, "State of the repo"). A later phase that changes earlier pictures in many places does the same (the phase 6 handoff, "Running the visual gate"). Phase 7a's gate made its references so before any 7a real-window run (`D:/Projects/Personal/gate7a-reference/`: phases 0 to 6's walk-throughs on `master` with git 2.56, 972 scripted pictures, and phase 6's gate's 364 real-window files); against phase 6's gate 5 of the 972 are the same files, 927 differ only in the status bar's git box (D248), and the other 40 vary by nature (31) or show git 2.56's own words (9; `docs/status.md`, Notes). Phase 7a's gate compared all 1,432 files with these (899 the same file, 430 changed, 103 new) and 16 Opus inspectors opened every one in batches of at most 150 (D247): every change was the Accounts button of D256 in the 33 pictures "Changes to expected results" lists, the git box against phase 6's real-window files, or one that varies by nature; none failed (the report's "Run"). Phase 7b's gate made its references so before any 7b real-window run (`D:/Projects/Personal/gate7b-reference/`: phases 0 to 7a's walk-throughs on `master` with git 2.56, and phase 7a's gate's real-window files; its design 0.7), where design 0.7 expected D353's intro in 7a's 23 part A pictures and 3 real-window ones, `b06a`'s port where the fake fell back and the pictures that vary by nature, and nothing else. Of the counted round's 1,551 files, 1,112 are the same files as the references, 320 changed and 119 are new (phase 7b's 93 scripted pictures and 26 real-window files), none in the references alone; 19 Opus inspectors opened every one, and 45 of CI's pictures, in batches of at most 150 (D247): 209 of the 320 are real-window files changed only in the window's two bottom corners, where the desktop shows through its rounded corners, and the rest the kinds design 0.7 expected: D353's intro (7a's 23 part A pictures and its real-window `a01a`, `a02b` and `a05a` with their difference pictures), the fake's port where it fell back (7a's `b06a`, and `a09b`, whose git output names the fake's URL) or what varies by nature, phase 5's `a2g` now among them; none failed (the report's "Run"). | Settled |
| 33 | Two fetches of one common git folder that overlap while the remote has something new lose one of them, with `cannot lock ref '<ref>': is at <new> but expected <old>` or a held `.lock` (phase 6's design 0.2). The bulk runs keep their rows apart, keep off another queue's running network operation and keep auto-fetch off both (4.3). A Fetch, Pull or Push the user starts in a tab, or a row's own (4.7), while another worktree of the repository, or a second tab on the same worktree (3.5), fetches is not kept apart, as in phases 3 to 5: one ends "Fetch failed" (or "Pull failed", "Auto-fetch failed") with git's lines, nothing is left behind and the next fetch works. A cancelled fetch's stale-lock scan covers the common folder, so it can list another worktree's live lock; the stamps (4.6) and the "still exists" check keep its removal harmless. A window-wide fetch gate in every queue would close it. Since phase 7a a pull request's checkout fetches `refs/pull/<n>/head` into `FETCH_HEAD` and reads that file itself; another fetch in the same worktree in between (a second tab on it, a terminal) rewrites it, and the checkout then fails rather than take the wrong commit (D305). Since phase 7b a GitLab merge request's checkout fetches `refs/merge-requests/<iid>/head` the same way (D361); Azure DevOps' checks out through a remote's branch alone and fetches no request ref. | A report |
| 34 | macOS keeps each account's token (GitHub's, and since phase 7b GitLab's and Azure DevOps', its design 9.11) as a login-keychain item made by `/usr/bin/security`; any process of the user can read it through `security` without a prompt, as on Windows and Linux (phase 7a's ruling 20, design 3.10). Accepted until phase 8 signs the app, which may move to Security.framework for a per-app access list (phase 8's "From phase 7a" items). The real store's class passes in CI's macOS keychain step (first on `8b02805`), against a throw-away keychain that never prompts (risk 41). | Phase 8b |
| 35 | A user's `.netrc` or `_netrc` and the account's token (D340): on git 2.31 to 2.45 (Ubuntu 24.04's 2.43 among them) an entry for github.com answers a bound call's first 401 before the app's helper, so that call goes as the entry's user; git-lfs's own HTTP client asks HOME's netrc before any helper on every git version (its source read, not measured). Design 4.1's "from nothing else" (D255) holds for git's own requests from 2.46 on. Since phase 7b the same holds for the new hosts (its design 8.6, measured on 2.31.1, 2.43.0 and 2.56): on 2.31 to 2.45 a netrc entry for gitlab.com, dev.azure.com or `<organization>.visualstudio.com` answers a bound URL's first 401, a URL with no user too, and from 2.46 never; inside a bound operation git-lfs asks the user's helpers, sending no path (D359, accepted). Since phase 8a (ruling 8, D424) the Accounts section shows one note while an account is signed in with "Use for git over HTTPS" on, the git found is older than 2.46 and the home folder git is given holds a `.netrc` or `_netrc` file: `Your home folder has a .netrc or _netrc file. With Git {version}, an entry in that file for a host can answer git's requests to it before your signed-in account. …`; the app reads only whether the file exists, never what it holds, and the note can be dismissed for the session (F14, `AccountsNetrcNoteTests`). It looks in git's home folder only: curl's `NETRC`, or Windows without `HOME`, can escape it, which the guide says (phase 8a's design 5.9). D277's note below 2.31 is gone with the floor (D420); with a floor at 2.46 or later the note would no longer show. | Settled as a note (D424); the floor's review in May 2027 (D406) |
| 36 | A repository renamed on GitHub is followed through A3's answer for the remotes that name it under its old name (phase 7a's design 19 item 2), but a fork whose renamed parent is reached through an `upstream` remote under the old name is not mapped, so that parent's pull requests can miss their badges, checkout and the create form's base; a transfer to another owner is not in the fake. | A report |
| 37 | Test runners that use neither TempRepo's environment nor an app session's still see the machine's HOME: App.Tests' `OperationsHarness` when it is given no session environment and App.Tests' static runners, and Git.Tests' `RepositoryTestSupport.NewRunner`, whose calls inherit the test process's environment. Their calls to the tests' loopback servers (`AuthRemoteServer`, which `RepositoryOperationsFetchTests` and `RemoteOperationTests` fetch from) could be answered by a machine netrc entry with `default` or `machine 127.0.0.1`: `GitIsolation` removes `NETRC` from the test process, not HOME. Corrected at phase 8a's close: this row named `LargeHistory` and `AuthRemoteServer`'s own runner too, but both give their calls `TempRepo.IsolatedEnvironment`, whose HOME is a folder of their own, and did so before the row was written, all but the server's check that git has `http-backend` (`CheckHttpBackendAsync`), which sends no request. Setting HOME for the whole test process would also move .NET's UserProfile folder on Linux and macOS (phase 7a's design 19 item 4). | When a machine shows it |
| 38 | The avatars' lookups after the live runs (D339, D342, D394; phase 7a's design 20.3 and 20.6): github.com combines `commits?sha=<sha>&author=<e-mail>` as assumed, and has answered an unknown SHA beside `author` in two ways: 500 with an empty body on 2026-10-30 and 31, likely a fault of GitHub's, and 404 "Not Found", its answer without `author`, on 2026-11-08 (D394). A lookup answered 422, 5xx or 404 in the listed repository leaves out its own address alone, and that address is asked again at every run: 20 such addresses at the head of the newest-first list still keep the later ones waiting, and while GitHub fails a run costs up to 20 failed lookups and 20 warnings. Since GitHub answers a commit it lacks 404, a fork's lookup of such a commit presumably gets the 404 an unreadable repository gives (the live runs asked the listed repository alone), so a 404 in a repository not known to be readable is followed by one read of that repository (D395), and only one whose read answers 404, 403 or a 5xx leaves out its lookups for the call: a fork's tab, whose lookups go mostly to the fork (origin comes first), takes the fork as readable from A3 and still finds the addresses after one whose commit GitHub lacks, such as a stale remote-tracking branch's, with no request more; another remote's 404 costs one read a call, and an unreadable remote two requests and two warnings a run. That an unreadable repository answers 404 beside `author` is not measured either. The live command's probes show a change of GitHub's at each gate's runs: on phase 7b's counted round the four lookups of a commit GitHub lacks, with and without `author`, answered as the fake does (D394), and GitHub's lines of phase 8a's run before its pictures (2026-11-26) matched those of phase 7b's counted round, and differed from the run before 7b's pictures only in the two lines D394 corrected, request ids, times, branch stamps and SHAs aside (phase 8a's design 13.1, ruling 112). Phase 8a's performance pass left this risk alone, and the phase changed nothing under `src/VisualCommit.Hosting` (its ruling 2). | A report |
| 39 | A bound tab whose account the session refuses through the API (D330), or signs out of, after an operation has bound its git calls and before git's credential request: the helper has nothing, git asks for a user name (before any request since D340), and since no erase put the account into the scope's refused set (D273), the app shows git's "Sign in to <host>" dialog in a bound tab, where ruling 9 has a bound repository never prompt. D340 records the sign-out case; phase 7a's design 19 item 8 met the API case through a test's race (a window of under about a second after the operation starts). The user can cancel the dialog. Refusing such a prompt without a dialog, from what the session knows, would amend D273 and D340. The gate needs no answer. Phase 7b keeps 7a's behaviour for each of the three providers' accounts (its design 9.11, 16.4), and on dev.azure.com it is wider (its design 8.3): with git's `useHttpPath` off, as on macOS, Linux and in every test run, a Username prompt there names no organization, so after a refusal in a scope it carries no account and shows git's dialog; typing the organization or `x-access-token` as the user name there makes the next Password prompt the account's, which D273 refuses without a dialog, and the operation ends with the refused-credentials text. | A report: the owner left it as recorded on 2026-11-19 (D413) |
| 40 | A status read in the middle of a terminal's git (phase 7a's design 21 item 4, found by CI on `391f7f2`): a refresh reads `git status` before it looks at `MERGE_HEAD`, so one that straddles the index of a merge started in a terminal shows "Conflicts resolved: commit to finish the merge." for about 0.3 s and one refresh, with Continue enabled (git refuses to commit while files are unmerged), until the watcher's next refresh, which always follows. Removing that state would take a status read that checks the in-progress files before and after `git status` and reads again when they differ; no requirement asks for it (Q4 asks for a refresh; phase 6's design 3.3 accepts in-between states of an outside operation). The same gate's fix of the stage panel (a stop whose message git has not written yet is read again at its next status, `a9d02c1`) reads a stop whose message tidies to nothing (a commit made with `--allow-empty-message`) at every status, one `git config --get core.commentChar` each time. | A report: the owner left it as recorded on 2026-11-19 (D413) |
| 41 | The real keychains and the browser in the app's own process: no run uses a real keychain there. The real-window pass keeps tokens in memory (phase 7a's design 0.5), CI's steps test each store's class alone with test names and dummy secrets, on macOS and Linux in throw-away keychains that are unlocked and never prompt (D259, D270, D333), and the laptop never touches the owner's store (`CLAUDE.md`). So a sign-in kept in Windows' Credential Manager across a restart of the real app, macOS's prompt for a locked login keychain and its dismissal (`Locked`, D268), and the Secret Service's prompts on a Linux desktop (an unlock, or the new "Login" collection, D271) are tried by no machine: `security`'s answers are tested only as a recording runner's outputs, and the Secret Service's prompts not at all (phase 7a's design 3.9). The macOS checklist of phase 7a's report covers the Mac by hand (risk 1); nothing covers Linux, where no provider of the Secret Service but gnome-keyring (in CI's containers) was tried. A real browser is opened only by part P's last step in Windows' real window ("Open in browser", D321) and by the macOS checklist, on Linux by nothing. Since phase 7b a store holds an entry per signed-in provider, up to three (`TokenEntryNames` names them), and CI's real-store steps include an `azure-devops` key (its design 1.10, D270); its real-window launches sign in to the three providers with the tokens in memory and open no browser (ruling 29), so the GitLab and Azure DevOps sign-ins in a real keychain, their dialogs' token links and their requests' "Open in browser" are tried by no machine (phase 8's "From phase 7b" item; on a Mac, phase 7b's macOS checklist by hand). | Phase 8b (the checks by hand on each platform) |
| 42 | The fakes stand for github.com, dev.azure.com and gitlab.com in the default run and CI (D250, D346); the live command, the only check against the services, runs only on the Windows machine, the owner told: with the tokens during the phase (phase 7a's status step 16; phase 7b's stages 1 and 2, its step 16) and twice at each gate (D335), with the owner's tokens for the three sandboxes, which expire on 2026-11-29 (GitHub's) and on 2026-12-04 (Azure DevOps' and GitLab's), so a gate after a date needs that token made again first, as phase 7a's design 14.6 and phase 7b's 15.8 say. The runs corrected the fakes' answers, written from memory or from the services' documents (D342, D385, WP-LF's in phase 7b's design 16.1, D394), but some are still not measured. GitHub's (phase 7a's design 20.1, 20.8): its empty repository's 409 (the sandbox has a commit), a retired API version's status (`ApiVersionGone`'s 410), the `ServerError` fault's body, a lookup in a repository the token cannot read (D339's 404, risk 38), and git over HTTPS with a select-only fine-grained token on a public repository outside its selection (D340's case; the fake does not model "Only select repositories"). Azure DevOps' and GitLab's, kept as READ or ASSUMED in their fakes (phase 7b's design 3.7, 4.7, 16.1): the bodies of an expired and of a revoked token, a write with a read-only token, an Azure DevOps fork's pull-request shapes and a legacy GitLab token's sign-in (D375: no fork and no second token were made), other organizations, TF401027, 429 and 5xx answers, and how Azure DevOps' `remoteUrl` writes a space, a non-ASCII or a reserved character of a name (the sandbox's names hold none; the credential twin of phase 7b's design 8.2 binds either spelling, its 16.2); the fake of GitLab does not model a fine-grained token limited to selected projects (D370), and git's `info/refs` refusals of made-up values stay a known difference (the fakes' git sends one challenge, Azure DevOps three; the helper reads no `wwwauth[]`). The services can change any answer between gates, as github.com's commits lookup did between 2026-10-31 and 2026-11-08 (D394). A live run can also stop on the network: at phase 7b's gate's first run two of three runs of GitLab's class stopped on stalls of the network to gitlab.com (a push past the test's 60 s, a request past the client's 30 s), which a loop of plain requests beside the third run met too (20 s without an answer); the third passed, and nothing changed in the app or the command. Phase 8a changed nothing under `src/VisualCommit.Hosting` and made no commit in the live project (its ruling 2): its run before the gate's pictures (2026-11-26, on `fab4073`, the owner told) passed 596 of 596 after the 582 offline tests, its three `run.txt` the same lines as phase 7b's gate runs, request ids, times, branch stamps and SHAs taken out, but for the made-up organization's name, Azure DevOps' own dates and two GitHub lines that read "the fake: same" where the run before 7b's pictures had found the fake's 500 (D394), so nothing was corrected (its design 13.1, ruling 112); its first round's run passed 596 of 596 in 3 minutes, and the run of the counted round that stands, on `750eb33` on 2026-11-27, 596 of 596 in 3 min 4 s after its 582 offline tests, each `run.txt` differing from the runs before only in what changes from run to run and holding no token, organization or namespace (ruling 116). The tokens were not made again for phase 8a, whose live runs all came before GitHub's token expires, and are made again before phase 8b's gate. | Each gate's live runs; new tokens before phase 8b's gate (GitHub's expires on 2026-11-29, Azure DevOps' and GitLab's on 2026-12-04) |
| 43 | D275's refusal of another user rests on git keeping a URL's user in its credential request (D389). Git up to 2.56.0 without its commit 76524c2604bc, over a libcurl that strips the user from its effective URL (curl 8.23.0 and later, the fix of CVE-2026-94266, or a distribution's curl with that fix), takes its own request for a redirect after the first 401 and asks the app's helper with no user, which `IsAccountUser` takes for the account. Measured on the laptop with made-up credentials against a loopback stub: on such a git a binding whose key git's credential view matches to another user's URL gives that URL the token without a prompt. On 2.46 and later that happens only where D340's proactive fill does not cover the same URL, as with design 16.3's reserved character escaped in the binding and raw in the other URL; on 2.31 to 2.45 also with 16.3's trailing `/` and unreserved escape, and with a stale binding (a rule added since the tab's last read, outside D388's window; bindings taken when an operation is queued and used when it runs, so a URL changed in a terminal and a Fetch clicked before the watcher's refs read; an `includeIf onbranch:` rule for the one read after a branch switch; the review before the gate, D392). A clone's conditional includes are read since D391. The token reaches only the bound key's host, and GitLab's and Azure DevOps' `SelectCovered` drops in git's own view, so GitHub's raw comparison is the exposed one. CI's macOS job ran such a git (Homebrew's 2.56.0_2 over curl 8.23.0, the reserved case only) until Homebrew's 2.56.1, which phase 8a's last three runs ran: its `git-url-user.txt` reads Kept in runs 45917903492, 45965143178 and 46007767846; Git for Windows from 2.56.0.windows.3 and Ubuntu's noble git of 2026-10-19 carry git's fix (jammy's by its date, not read); no 2.31 to 2.45 git over a stripping curl was found in CI or Ubuntu's LTS releases; Debian, RHEL and Apple's own git are not checked. `GitUrlUser` writes each run's kind of git to `artifacts/visual/git-url-user.txt`, which CI uploads for each job. D278's own test case gives no token on any git. | A report: the owner left it as recorded on 2026-11-19 (D413); git 2.57 or a 2.56.1 in Homebrew retires the macOS case |
| 44 | GitLab's measured burst limit, 100 requests a minute with 304s counted (16.1, WP-LIVE's stage 1), against the app's lists: a GitLab tab's first list costs up to 24 requests (A3, up to 3 pages and up to 20 fork lookups, kept only for the run and not past the cap), every restored tab lists at the app's start, and nothing holds a provider's requests until a 429's `RetryAt`, so about five restored GitLab tabs whose open merge requests come from 20 or more forks pass it; the later tabs then show "GitLab's rate limit was reached" until their next refresh, and each refresh meanwhile meets the 429 again. No data is lost and the account stays signed in (a 429 is `RateLimited`, D392). Phase 8a's performance pass left this risk alone, and the phase changed nothing under `src/VisualCommit.Hosting` (its ruling 2). Since phase 8a the restore order opens the active restored tab first and the others at its first page, at most four at once (ruling 76, D457 (5), D458), which spreads the restored tabs' openings over the start; how that meets this limit was not measured. | A report: the restored tabs' first lists spread out further, the provider's requests held until `RetryAt`, fewer fork lookups a call |
| 45 | What the account's bindings cannot see, accepted in phase 7b (its design 16.3; D379, D381, D392). The new providers' `Select` fails closed on every remote URL spelling that wave 1's adversarial checks found, but: (1) 7a's github.com rule stays as 7a froze it (phase 7b's design 1.2), so its D278 drop compares raw paths and misses an unreserved escape (`%52`), a character raw on one side and escaped on the other, a reserved character escaped in the binding and raw in the other URL, and a trailing `/` on one side (measured on 2.31.1, 2.43.0 and 2.56): the binding is kept, its key answers that user's URL, git asks only the app's helper, which refuses the user (D275), and the user's own helpers are not asked (on a git that loses the URL's user the token answers, risk 43). Another user's github.com URL spelt with a trailing dot on the host, with `www.`, with `http://`, with white space or a control character in its user part, with a host outside printable ASCII or behind a remote helper's `<name>::` prefix also keeps the binding beside it, and git reaches github.com and asks for the credential without that user, so D82's order is lost for it. Either way the token reaches github.com only. (2) Submodules (O10): the scope's `GIT_CONFIG_COUNT` pairs reach a submodule's fetch through git's on-demand recursion, so a submodule URL at or under a bound URL takes the token, an out-of-form one under an Azure DevOps binding (`…/r/..%5C..%5Cotherorg/…`) included, whose path the service may read as another organization's; the token reaches only dev.azure.com. (3) A remote on a host no coverage covers that redirects into an out-of-form path under a binding gets the token there, because git builds the credential again from the redirect's target, which `Select` never sees (measured on 2.43 and 2.56); stopping it (`http.followRedirects=false` with `initial` per bound URL) costs D340's pair count and every unbound remote's redirect in a bound scope. (4) D379's "any port" makes any local server's URL whose first segment is outside the organization's grammar fail the Azure DevOps fake's host closed, so in a test or a hand run pointed at a fake a bound fake remote can lose its binding beside it; never a token. Beside them, 7a's GitHub client keeps what phase 7b's design 7.7's As built lists: `GitHubUrls`' authority gaps, A1 taking any username, A2 and A4 throwing on `[null]`, and the HTTP core following a non-GET's same-origin redirect for GitHub and Azure DevOps, so a create's POST would be sent again on a 301 or 302 (none measured). | The owner, or a later phase that moves GitHub onto `SelectCovered` and the core's checks; O10 (submodules); a measured case of (3) that reaches another service, or of a redirected API POST |
| 46 | Phase 6's `PaletteGrowthTests.The_build_and_a_one_letter_query_grow_in_step_with_the_refs` fails rarely on CI, its ratio just past its bound of 6.3: 7.9 in run 42301846503 (the container with Ubuntu's SDK packages), 6.7 in 42386428809 (macOS) and 6.5 in 42454621322 (the same container), 3 of phase 7b's 29 runs, each time in one job. Not explained: the skeptics ruled out page faults and reproduced 7.9 by taking time from the measuring thread that its clock still counts, as a guest's thread clock counts what the hypervisor takes; a first fix was refuted as a loosening, and in 414 paired trials no other measure held (each failed linear work more often or passed superlinear work more often than the test's, but one tried too few times to decide), so the test and its bound stay (the Notes of phase 7b's `status.md`, "The two Linux failures of 42301846503"). Since the review before the gate (D392) each run appends its measures to `artifacts/visual/palette-growth.txt`, which CI uploads for each job, and a failure names what ended the region without collections and gives every measure's work. A CI run of a gate's commit can fail on it alone. In phase 8a it failed once in the branch's 30 runs (44479788558 to 46007767846), for the first time in Windows' job: 8.1 against 6.3 in the first attempt of run 44479788558 (on `e04762b`), which passed when the job was run again. | When its records show a cause; whenever the palette's build or the test changes |
| 47 | Git at below-normal priority on Windows (phase 6's design 8.10, D386): a `BelowNormalPriority` call's git joins a job that holds Windows' priority class at below normal before the runner lowers anything, and the runner adds every descendant it finds to that job, since CI run 42216604663 found git's console host at normal priority. One case stays open: a process whose creation read git's job before git joined it and that shows in a snapshot only after the runner's last round runs at normal priority to its end; none did in about 480 trials. If Windows refuses the job's limit, the call keeps the lowering alone. `GitRunnerPriorityTests` on CI's Windows job would show such a process, as it showed the console host. | A failure of `GitRunnerPriorityTests`, or a report |
| 48 | Windows cleans the temp folder under the tests when a disk is low (D468): Storage Sense, on with "delete temporary files that my apps aren't using", removes empty folders under `%TEMP%`, and a git folder whose refs are all packed, or that has no commit yet, is no repository without its empty `refs` folders. On 2026-11-24 and 25, with 1.3 % of C: free, it struck three times in about eighteen hours: it failed phase 1's checks 13 and 14 in phase 8a's first whole run of step 19, through the shared bare 100,000-commit copy, and three of phase 7b's checks in the second, which were copying their kept template at that moment. Since D468 the test support code proves the shared copies through git before use and makes them again when they are damaged, gives every copy of a blank git folder or of a kept template the folders its source had, and walks a copy past a folder the cleaner is deleting at that instant (`TempFolderCleanerTests`, 25 tests). Accepted (D468 (4)): a repository of a test in flight, and a template or the shared bare copy stripped after its record or proof, break that test or that process, and the next process heals. Any Windows machine with Storage Sense on can meet it. | Each gate: read the disk first (`Get-PSDrive C`; CLAUDE.md's note), and read a run that fails in many set-ups at one moment against it |
| 49 | On git 2.34, the floor (D420), git itself does not refuse to move a branch checked out in another worktree. Before a fast-forward of a branch that is not checked out here the app reads the worktrees, on every git, and refuses a branch checked out in another worktree with that worktree's path (`GitRepository.FastForwardBranchAsync`); git's own refusal from 2.35 is the second guard. On 2.34 the read sees only the worktrees' branch lines, so it misses a branch being rebased or bisected in another worktree, whose HEAD is detached, and a checkout between its read and the fetch: such a rebase's end then cannot update the moved branch and leaves that worktree on a detached HEAD with the rebased commits, the branch at the fast-forwarded commit, nothing lost. A worktree path that holds an empty line (never on Windows) loses its branch line too. A fetch refspec into `refs/heads/*`, as a mirror's `+refs/*:refs/*`, can move a branch checked out in another worktree on 2.34, as in a terminal: not fixed (D420; ruling 75; phase 8a's design 3.9). | A report; the floor's review in May 2027 (D406) |
| 50 | One instance per data folder (D421, D464), accepted as built in phase 8a (its design 4.9, 13.4 item 5): a first instance hung before its window keeps answering "starting"; a second start waits up to 30 s, unseen, while the first closes; and a data folder on a network share depends on the mount's locks. Accepted at the review's fixes (D464; its design 4.3.2, 13.1's ruling 100): a new holder that keeps the lock while its endpoint refuses holds a waiting start until the closing wait ends, and the log names that case; and a start whose ask is in flight when a closing holder's server is cancelled gets no answer, and if another start takes the lock within its 250 ms it shows the notice instead of asking that start, which Try again then hands over to. Two instances on two data folders still run. The socket path under a real macOS `TMPDIR` and macOS bringing the first window forward are untried (phase 8b's checks by hand). The headless harness never runs `Program.Main`, where the guard sits: `SingleInstanceGuardTests` test the guard's class, and only the real window, which starts the built app, puts the guard to work, in phase 8a's launches 4 to 6, with its exit codes and its notice. | A report; phase 8b (the updater's own key, the Mac) |
| 51 | A held fetch that showed no percentage in CI (D470): twice in phase 8a's 29 Windows jobs counted on 2026-11-26, both on the runner image 20261116.315.1 with git 2.56.1.windows.1, a fetch the tests' server held showed no percentage in 60 s (run 45586070342 in `RemoteOperationTests`, run 45871534398 in phase 3's check 17), and the laptop showed the same once, on 2026-11-20, in a lagged run; each passed when run again. A probe on the laptop found that Git for Windows 2.56 writes a progress line of odd length in two writes, its last byte apart, and that once in 510 recorded fetches the first packet with a `%` ended before that byte, so git had no whole line with a percentage to show while the server held. Since D470 a hold with no chosen percentage ends with the packet that completes the first whole line with one, and a wait for such a hold says in its timeout whether the hold began, what the server sent and the last whole line git could show; phase 3's part D waits for the server to hold each fetch before it waits for the percentage. That this was CI's cause is likely and not shown: the runner's git was not measured, and no dependence on load was shown either way. A CI run of a gate's commit can fail on it. | Another such failure in CI or at a gate: its message read first (phase 8a's design 13.1, ruling 113) |
| 52 | The user guide (`docs/guide/`, 33 pages; D408, D439) states the app's words, keys and behaviour, and every statement was checked against the source before its merge (phase 8a's design 13.1, rulings 94 and 95). Tests hold only the pages and anchors the app links to (`AppLinksTests`) and the shortcuts page's tables (`GuideShortcutsPageTests`), which the guide's workflow runs too (D451), and the pictures, made at every build of the site by `tests/VisualCommit.GuideShots`. A change to a text, a key or a behaviour that a page states needs that page's change, and nothing else catches it. | Every change to the app's words, keys or behaviour |
| 53 | The graph's selected row speaks to a screen reader only once the reader has read below the graph (D455): Avalonia makes a peer's UI Automation node only when a client reaches it, and only a node forwards its events, so a client that never looks below the list hears nothing (the spike's cold case), and the builder found no public way in Avalonia 12.1.3 to make the row's node exist without a client. The route is the public one (D435, D455), with no `[PrivateApi]`; launch 13 of the real window, whose client reads the graph's first child first, receives one LiveRegionChanged from the row at every move. Whether Narrator reads below the graph by itself is what the gate's Narrator walk-through shows (D460); if Narrator is silent there, a new decision names the route, and its fix comes after the gate's first round. VoiceOver and Orca are not tried (D411). | Phase 8a's Narrator walk-through; whenever Avalonia is updated |
