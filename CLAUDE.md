# VisualCommit

A cross-platform desktop Git client in the spirit of GitKraken. Avalonia UI, .NET 10, all C#,
driving the real `git` executable. Open source (MIT).

The project is built in phases, normally one phase per session. A session starts with no memory
of the earlier ones, so the repo is the only source of context. Keep it that way: anything the
next session needs goes into the files below, not just into the conversation.

A session runs either on the owner's Windows machine or in a Linux cloud session. Everything
below holds for both; what differs away from Windows is in "Sessions away from the Windows
machine".

## Start of every session

1. Find the right branch.
   - Run `git remote set-branches origin '*'`: a clone made for a single branch cannot otherwise
     see or check out the others, and on a normal clone it changes nothing. Then fetch:
     `git fetch --unshallow origin` if `git rev-parse --is-shallow-repository` prints `true` (a
     shallow clone shows merged branches as unmerged), `git fetch origin` otherwise. Then run
     `git status` and `git branch -r --no-merged origin/master`.
   - If `git status` shows uncommitted changes, do step 7 before switching branches.
   - Every branch the last command lists holds work that is not in `master` yet: a phase branch
     (`origin/phase/...`) or a branch that a cloud session made. If it lists one, check out the
     local branch of that name, without `origin/` in front (`git checkout phase/1-graph`), and
     if it is behind `origin`, fast-forward it (`git merge --ff-only origin/<branch>`). Its
     `docs/status.md` is the current one; the copy on `master` is out of date.
   - If it lists nothing, run `git branch --no-merged origin/master` too. A local branch other
     than `master` that it lists holds commits that were never pushed, such as a phase branch
     whose first push did not happen: check it out, push it (`git push -u origin <branch>`)
     and treat it as a listed branch. If that lists nothing either, no phase is under way and
     the current `docs/status.md` is the one on `origin/master`: check out `master` and
     fast-forward it (`git merge --ff-only origin/master`), also when `git status` showed
     another branch.
   - If the branch you end up on has commits that `origin` lacks, push them before anything
     else. If each side has commits the other lacks, stop and ask the owner: do not merge,
     rebase or force-push.
   - A listed branch named `docs/...` is no phase and is never the branch to work on: it holds
     a change made outside a phase that a cloud session could not push to `master`. On the
     Windows machine, bring it into `master` first (`git checkout master`,
     `git merge --ff-only origin/master`, `git merge --ff-only origin/docs/<short-name>`,
     `git push origin master`); any other session leaves it alone. Either way, go on as if it
     were not listed.
   - A listed branch that is contained in another listed one
     (`git merge-base --is-ancestor origin/<older> origin/<newer>` succeeds) is an earlier
     stage of the same phase, left behind when a cloud session had to continue on a branch of
     its own: go by the newer one. If more than one branch is still left after that, or the
     one left is named in no `docs/status.md`, and the `docs/status.md` files do not make clear
     which is current, ask the owner.
2. Read `docs/status.md`: which phase is current and where work stopped.
3. Read `docs/plan.md`: the Workflow, Visual test gate and "Risks and open decisions" sections, and the section for the current phase.
4. Read `docs/architecture.md`, `docs/decisions.md`, and the parts of `docs/requirements.md` the phase covers.
5. Read the previous phase's handoff, `docs/handoffs/phase-<N-1>.md`. None exists until Phase 0 is closed.
   Phase 7 is split in two (D241): the phases run 6, 7a, 7b, 8. Wherever this file counts phases
   (the previous phase, N-1, N+1), the phase before 7a is 6, the one before 7b is 7a and the one
   before 8 is 7b, and the docs write `7a` and `7b` for N (`docs/handoffs/phase-7a.md`,
   `docs/test-reports/phase-7a.md`, "Start phase 7a", "Phase 7a accepted"); how the code and the
   test harness name them is for 7a's design (the phase 6 handoff, "For the next phase").
6. If code exists, see the state you inherited before changing anything: build it, run the test
   classes of the areas the latest commits changed, and read the result of CI's last run on the
   branch (a push that changes only docs starts none, so it is the run of the last commit that
   changed anything else, which `docs/status.md`'s CI section names; Commands). Run the whole
   default run (on the Windows machine `bash scripts/heavy-run.sh`) only when the machine has
   changed (git, the .NET SDK, a Windows update) or the tree holds code that is not pushed (D245). If anything fails, fix or report that first. On a Linux machine where
   `dotnet --version` does not print 10.x, run `bash scripts/setup-linux.sh` first.
7. If the working tree has uncommitted changes, read the Notes in `docs/status.md` and the diff,
   then finish or commit them before starting anything new. Do not discard work you do not understand.

Then, to **start a phase**:

1. Check that the previous phase is merged into `master`. If it is still "Awaiting acceptance",
   stop and ask the owner. Never start from a `master` that lacks the previous phase.
2. Create the phase branch from the `master` you just fetched, under the name listed for the
   phase in `docs/status.md`: `git switch -c <branch> --no-track origin/master`. Push it with
   `git push -u origin <branch>` after the first commit. Without `--no-track`, git would compare
   the new branch with `origin/master` from then on.
3. In `docs/status.md`, set the phase to "In progress" and build the Progress checklist: the
   phase's "Delivers" items split into steps small enough to finish in one sitting, then its
   visual checks, then the closing steps below, with a step for reading the CI run of the gate's
   commit. Commit.
4. Write the phase's visual checks into `docs/test-reports/phase-N.md` from the template, each
   with concrete steps and a concrete expected result, and each marked with whether it is
   repeated in the real window. Commit this before building the UI the checks test. Since D244
   each part's checks are written and committed just before its package is built, still before
   its UI (`docs/plan.md`'s Visual test gate, step 1).

To **resume a phase**: continue from "Next step" in `docs/status.md`.

## What the owner says and what it means

| Owner says | Do |
|---|---|
| "Start phase N" | The start steps above, then build phase N through to its closing steps |
| "Continue" | Resume the phase whose Progress checklist in `docs/status.md` still has unticked items (state "In progress" or "Blocked") |
| "Phase N accepted" | First `git fetch origin`, check out the branch that `docs/status.md` names for the phase and fast-forward it (`git merge --ff-only origin/<branch>`), so that what is checked is what `origin` holds now. Only when `docs/status.md` on that branch says the phase is "Awaiting acceptance", and nothing but docs changed after the "Gate commit" it names, apart from the live command's project, whose fixes re-run only that command (D335; run at the repo root, `git diff --stat <gate commit> <branch> -- . ':(exclude)docs' ':(exclude)*.md' ':(exclude)tests/VisualCommit.LiveHostingTests'` prints nothing): check out `master`, fast-forward it to `origin/master`, `git merge --no-ff` that branch, set the phase to "Done" in `docs/status.md` and say in its handoff's "State of the repo" and "Waiting on the owner" that it is merged (with the merge commit), commit, push `master`. Setting it to "Done" also resets the file for the next phase, as phase 1's acceptance (`eb626ba`) did: "Current phase" says none is in progress and names the merge commit; Progress goes back to its paragraph on how the checklist is built (a phase's `docs/status.md` does not hold it: take it from `docs/status.md` on `master` before the merge, as phase 4's acceptance wrote it: `git show 50cf35c:docs/status.md`; phase 5's acceptance added a sentence that points at the phase's own checklist in the merge's second parent, `git show acdac12:docs/status.md`, and D241 added the weekly share of the plan's allowance on 2026-10-28; the current form is always the one on `master` before the merge, `git show HEAD^1:docs/status.md` once the merge is made); the CI section names the branch's last run; "Next step" says to wait for "Start phase N+1" and points to the handoff's "For the next phase"; the phase's working notes leave "Notes for whoever resumes". Then read the CI run that the push of `master` started and record it in the CI section (a docs-only commit). In any other case, change nothing more, say what is still open and ask |

## Rules

- Work only on the phase the owner asked for. Do not start the next phase unasked.
- Build each phase on its own branch (names are in `docs/status.md`). Merge into `master` only
  after the owner accepts the phase. Phase work reaches `master` in no other way: do not open a
  pull request into `master` unless the owner asks for one, and never merge one yourself.
- Changes to planning docs made outside a phase are committed directly on `master`. So is a
  change to the project's tooling that the owner asks for outside a phase (the CI workflow,
  set-up scripts, test isolation); it starts a CI run, which is read and recorded in
  `docs/status.md` like any other.
- Push to `origin` (github.com/RahimPasha/VisualCommit) after committing: the phase branch during
  a phase, `master` after a merge or a change made outside a phase. The owner has authorised
  this. A newer push cancels the CI run of the one before it, so push small code commits that
  follow each other within minutes together. Never force-push or rewrite pushed history. Ask
  before changing anything else on GitHub, such as settings, visibility or releases.
- Every commit has `Rahim <rpkhajei@gmail.com>` as author and as committer (D40); no other
  address of the owner may enter the repo. Before the first commit on any machine or in any new
  clone, run `git var GIT_AUTHOR_IDENT` and `git var GIT_COMMITTER_IDENT`. If either shows
  something else, set both values for this repo only (`git config user.name Rahim`,
  `git config user.email rpkhajei@gmail.com`) and look again. If they still differ, something
  outside the repo sets the identity: tell the owner and do not commit until it is sorted. A
  pushed commit cannot be corrected without rewriting history.
- Commit at every working state. In each commit keep "Next step" in `docs/status.md` accurate and
  tick the items that are complete. Before stopping for any reason, fill in the Notes section,
  then commit and push, so that a new session on any machine can resume from that commit.
- Every item in a phase's "Delivers" list must be proven by at least one automated test or one
  visual check. Items that are only files or docs (README, CI workflow, the Commands section) are
  proven by the file existing; the handoff names it.
- The default test run (`dotnet test` at the repo root) must never touch the real desktop or the
  user's real settings. The real-window pass is a separate, Windows-only, opt-in command. It takes
  over the mouse, so run it only as part of the gate and tell the owner before starting it.
- A phase is not done until the visual test gate in `docs/plan.md` has passed: screenshots taken
  and every one inspected. Passing unit tests is not enough. If part of the gate could not run, say so.
- A phase that is "Awaiting acceptance" has passed the gate on its "Gate commit". A session that
  then changes anything other than docs on it sets the state back in the same commit: to "In
  progress" on the Windows machine, where the gate is then run again, and to "Blocked" anywhere
  else. The same commit unticks the gate and the closing steps in the Progress checklist and
  rewrites "Next step" (both passes of the gate again on one commit on the Windows machine, or
  the real-window pass alone when the change is only to `tests/VisualCommit.RealWindowTests`, as
  `docs/plan.md`'s Visual test gate allows (D246); then closing step 6 with the new "Gate
  commit") and, when "Blocked", "Waiting on the owner". A change only to
  `tests/VisualCommit.LiveHostingTests` re-runs only the live command, on its commit, and leaves
  the "Gate commit" where it was (D335).
- Expected results for visual checks are written before the UI exists. Changing one afterwards
  needs a reason recorded in the test report.
- CI runs on GitHub when a branch is pushed. After pushing, read the result with `gh run list`
  and `gh run view`, or without `gh` as shown under Commands, and report what it actually says.
  If the result cannot be read, report CI as unverified, never as green.
- Record every decision that changes the plan, scope or architecture in `docs/decisions.md`, with
  its reason. Do not reopen a recorded decision without the owner.
- Add each command to the Commands section below the first time it works.
- Feature and requirement numbers (C4, O1, T2, Q1, R3 ...) are defined in `docs/requirements.md`.
- Models (the owner, 2026-10-10): Sonnet 5.5 wherever it does the job as well as Opus 5.5, for
  the main session and for sub-agents alike; Opus 5.5 where the work needs judgment. Opus 5.5:
  building a phase (designing, writing or changing code or docs), reviews and verification,
  inspecting the gate's pictures, and the cold-read check. Sonnet 5.5: tasks that are simple,
  mechanical or fully specified, such as cropping, resizing or comparing images, listing or
  searching files, running commands and reporting their output, or a small edit described
  exactly. Name the model on every sub-agent you start (`opus` or `sonnet`) instead of relying
  on the default, because a built-in agent type can default to another one. A session cannot
  change its own model; the owner does that with `/model`. If your session runs on a model that
  does not fit the work ahead (Sonnet 5.5 for building a phase, say), say so in your first reply
  and ask the owner once whether to switch or carry on; when a long stretch of work that Sonnet
  5.5 does as well lies ahead, you may suggest switching. Fable 5.1 is no longer used.

## Closing a phase

1. Run the visual test gate and complete `docs/test-reports/phase-N.md`. Before its first
   counted round (a round whose passes stand as the gate if they pass), once the phase's scripted
   walk-through has saved its pictures on that machine (the real-window checks compare their
   captures with them and fail without them), run the new phase's real-window checks twice on
   their own, with the owner told and present (D246; `docs/plan.md`'s Visual test gate, step 4).
   The two runs are made once, not again before the rounds that follow a fix. Since phase 7a the
   gate also runs the live command against real GitHub, the owner told: once before the gate's
   pictures and once on the counted round (D335; the same step 4).
2. Update `docs/architecture.md` so it describes what is actually built.
3. Update `docs/plan.md`: move anything this phase did not deliver into the phase that now owns it, and update the risks.
4. Write `docs/handoffs/phase-N.md` from `docs/handoffs/TEMPLATE.md`.
5. Cold-read check: first bring `docs/status.md`'s checklist and "Next step" up to date, because
   the cold reader reads it as it stands. Then start a fresh agent with no conversation context
   and give it only the repo. Tell it to assume this phase has been accepted and merged, and ask
   it how to build, run and test the app and how it would begin the next phase. Fix every gap it
   hits in the docs. Commit.
6. Update `docs/status.md`: links to the handoff and report, the last CI run of the branch with
   its result in each job, the "Gate commit" (the commit both passes of the gate ran on, or, by
   D246, the real-window pass's commit when it changes only `tests/VisualCommit.RealWindowTests`
   after the commit the default run ran on), and anything waiting on the owner.
   Set the phase to "Awaiting acceptance" only if the whole gate passed: a session on the
   Windows machine has run `dotnet test` and the real-window pass on the last commit that
   changed anything other than docs and `tests/VisualCommit.LiveHostingTests` (a fix to that
   project alone re-runs only the live command, on its commit, D335) (or, by D246, the real-window pass on it and `dotnet test`
   on the last commit the default run ran on, when both conditions of `docs/plan.md`'s Visual
   test gate, step 6, hold: the `git diff --stat` between the two that leaves out
   `tests/VisualCommit.RealWindowTests` and the docs prints nothing, and the phase's list of
   pictures shows the scripted pictures unchanged), and has inspected the pictures of both. The only part that
   may be missing is one the plan itself makes conditional, such as the 1920×1080 real window on
   a screen too small for it. If a check failed or any other part of the gate could not run, set
   the phase to "Blocked" and say why and who can unblock it. Commit.
7. Tell the owner what passed, what did not, and what needs their decision.

## Sessions away from the Windows machine

A Claude Code cloud session is a fresh Ubuntu machine with a clone of the repo: no desktop, no
Windows, and none of the files outside the repo that the owner's machine has. Such a session can
build a phase but cannot finish one, because the real-window pass needs Windows. If the owner
says "Continue" there while the phase is "Blocked" because it needs the Windows machine, change
nothing and say that the next step needs a session there, unless the owner asks for more work on
the phase. Otherwise:

- **Set-up.** Run `bash scripts/setup-linux.sh` when `dotnet --version` does not print 10.x, then
  `dotnet build` and `dotnet test`.
- **A replaced machine.** The machine can be replaced while a session sits idle, and nothing
  says so: the conversation goes on, but the new machine holds a fresh clone, possibly on
  another branch, without the identity that was set in the old clone, and whatever was not
  pushed is lost. A missing `dotnet` is a sign of it, but not a sure one, because the owner's
  environment script may have installed it already. So before the first change of every turn,
  run `git status -sb`, `git log -1 --oneline` and `git var GIT_AUTHOR_IDENT`. If the branch or
  the last commit is not the one you left, or the identity is not the owner's, the machine was
  replaced: run the script again if `dotnet` is missing, repeat step 1 of "Start of every
  session", and apply the identity rule above before the next commit.
- **Failures that show only here** are sorted first. If the machine lacks something (a tool, a
  library, a locale, access to a server), fix `scripts/setup-linux.sh` or report it. If the code
  or a test depends on something machines are free to differ in (git's configuration or
  environment variables, folders that may not exist, running as root), remove that dependence
  properly, as `TempRepo` and `HeadlessTestApp` do. Never make a test pass here by skipping it,
  loosening what it asserts or adding a branch for this machine.
- **Branches.** A cloud session may start on a branch made for it (`git status` shows it). Do
  not work there: check out or create the phase's branch as on Windows and push it as on
  Windows. Only if that push is refused because the session may push its own branch alone, work
  on its own branch instead, continued from the phase branch's latest commit if that branch
  exists. Put its name in the Branch column of the Phases table in `docs/status.md` in place of
  the phase's, and tell the owner; from then on it is the phase's branch for every session and
  for "Phase N accepted". A push refused for any other reason is reported, not worked around.
- **Never end a turn with work that is not committed and pushed**, also when the turn ends on a
  question to the owner: the machine can be replaced while the session waits. If the work is
  not in a working state, commit it anyway with a message that starts "WIP:" and say in the
  Notes what is unfinished.
- **`master`.** "Phase N accepted" and changes made outside a phase go on `master` here as on
  Windows. They count as done only when `git push origin master` has succeeded. If that push is
  refused, say so and open no pull request. A "Phase N accepted" merge is then left to a session
  on the Windows machine; nothing is lost, because the phase's branch is on `origin`. Any other
  commit would be lost with the machine, so push it to a branch of its own
  (`git push origin master:docs/<short-name>`) and name that branch in your reply; step 1 of
  "Start of every session" tells a session on the Windows machine how to bring it into
  `master`. If that push is refused too, because the session may push its own branch alone,
  push the commit to that branch instead (`git push origin master:<the session's own branch>`)
  and say in your reply that this branch holds a change for `master` and no phase.
- **What cannot run.** The app itself (`dotnet run` needs a desktop) and the real-window pass
  (its project does not even build on Linux). Look at the app through the scripted walk-through's
  screenshots. CI's Windows job is the only check of this work on Windows, and it also compiles
  the real-window project: read it after every push of code, and when a newer push cancelled a
  run, read the newer one.
- **The gate.** Write every check before the UI, as always. Run the scripted walk-through and
  inspect every picture. Write the real-window test code for the checks marked for it, and leave
  their results as "Not run: needs Windows". Nothing on Linux stands in for the real-window pass,
  and times measured here (such as Q1's two seconds) are indications only.
- **Closing.** Do what does not depend on Windows: closing steps 2, 3 and 4, and in the report
  your scripted run under "Run", marked as a cloud session, the Scripted column, screenshots and
  notes of every check, "Found by the gate" and "Other platforms". If `gh run download` is
  refused (CI's pictures come from a storage server outside the default network list), write
  that under "Other platforms" and leave CI's macOS and Linux pictures to the Windows session.
  In the Progress checklist leave every item that still needs Windows unticked and write "needs
  Windows" after it; mark harness code that was written but could not run "written, not run"
  there. Then
  set the phase to "Blocked" and write under "Waiting on the owner": the real-window pass needs
  the Windows machine; start a session there and say "Continue". Under "Next step" write what
  that session has to do:
  1. set the phase to "In progress";
  2. run the phase's scripted walk-through, so that its pictures exist on that machine, and then
     the new phase's real-window checks twice on their own, with the owner told and present
     (closing step 1); then run `dotnet test` and the real-window pass on one commit, fix what
     fails and run both again (the real-window pass alone when a fix changes only
     `tests/VisualCommit.RealWindowTests`, as `docs/plan.md`'s Visual test gate, step 6, allows,
     D246), and inspect every picture of both passes. The real-window code, with the harness
     items that `docs/status.md` marks "written, not run", has only been compiled until then;
  3. judge the phase's "Done when" items that are judged on the Windows machine, named one by
     one; if that changes anything other than docs, do step 2 again on the new commit, without
     the two runs of the real-window checks on their own, which are made once;
  4. finish the report with its own results, and bring the handoff, `docs/architecture.md` and
     `docs/plan.md` up to date with what the Windows run changed and measured;
  5. closing steps 5 to 7. The cold-read check is left to that session on purpose: it reads the
     finished docs.

What the owner sets up for cloud sessions, and what has not been tried in one yet, is in
`docs/cloud-sessions.md`. The first cloud session checks that list and corrects the page, and the
script if needed, on the phase's branch. If the script needs a correction before that branch
exists (step 6 runs on `master`), keep the change uncommitted until the branch is created in
step 2 of "start a phase" and commit it there; `master` gets it with the phase.

## Commands

Run them from the repo root. Each one was run on Windows when it was added here, except the
Linux set-up script; CI runs the build and the default tests on Windows, macOS and Linux, and
twice more in a bare Ubuntu container that it sets up with that script.

| Purpose | Command |
|---|---|
| Build | `dotnet build` |
| Run the app (needs a desktop) | `dotnet run --project src/VisualCommit.App` |
| Default tests: unit, integration, headless UI and the scripted visual walk-through | `dotnet test` |
| One test project | `dotnet test --project tests/VisualCommit.Git.Tests` |
| A heavy run under the lock, one at a time on the laptop (D245): a whole project or the whole default run | `bash scripts/heavy-run.sh` followed by `dotnet test`'s own arguments, which it runs at the repo root from any folder of the repo, for example `bash scripts/heavy-run.sh --project tests/VisualCommit.Git.Tests --filter-class "*HangClockTests"` or `bash scripts/heavy-run.sh --project tests/VisualCommit.Git.Tests`; with no arguments it is the whole default run, which the script runs with at most four projects side by side (`--max-parallel-test-modules 4`, added when no project, solution or such limit is named; D343). `VISUALCOMMIT_HEAVY_RUN_WAIT_SECONDS=12 bash scripts/heavy-run.sh …` gives up after 12 seconds of waiting with exit code 75 (`0`: at once) |
| Each test's time in a project run alone (xUnit's report; a heavy run, D245) | `bash scripts/heavy-run.sh --project tests/VisualCommit.Git.Tests --report-xunit --report-xunit-filename timing.xml --results-directory <folder>`: `<folder>/timing.xml` gives every test's `time` in seconds, a shared template's wait included (first run at phase 7a's gate, 2026-10-30: 2,495 tests in 22 min 30 s) |
| Who holds the heavy-run lock, and stopping that run (Git Bash) | `cat "$(cygpath -u "$TMP")/VisualCommit.Tests/heavy-run.lock/holder"`; then `kill -TERM <pid>` (or `-INT`, `-HUP`), with the file's `pid=` (not its `windows_pid=`), stops that run's `dotnet test` and frees the lock |
| Tests chosen by name | `dotnet test --project tests/VisualCommit.Git.Tests --filter-method "*Linear*"` |
| One phase's scripted checks | `dotnet test --project tests/VisualCommit.VisualTests --filter-class "*Phase1Checks"` for phase 1 (`"*Phase2Checks"` for phase 2); phases 3 and 4 have a project each: `dotnet test --project tests/VisualCommit.VisualTests.Phase3` (one part of it: `--filter-class "*Phase3ChecksD"`), `dotnet test --project tests/VisualCommit.VisualTests.Phase4`; phase 5 has two: `dotnet test --project tests/VisualCommit.VisualTests.Phase5` (parts A, B, F and P) and `dotnet test --project tests/VisualCommit.VisualTests.Phase5Undo` (part U, the undo checks); phase 6 has two: `dotnet test --project tests/VisualCommit.VisualTests.Phase6` (parts W, K, S and G; one part of it: `--filter-class "*Phase6ChecksW"`) and `dotnet test --project tests/VisualCommit.VisualTests.Phase6Look` (part L, the look: one check `--filter-method "*Check_L27*"`); phase 7a has one: `dotnet test --project tests/VisualCommit.VisualTests.Phase7a` (one part of it: `--filter-class "*Phase7aChecksA"`). To run one of phase 1's checks alone, name its class: `--filter-method "*Phase1Checks.Check_13*"` (phase 2 has a `Check_13` too) |
| Scripted visual walk-through only | `dotnet test --project tests/VisualCommit.VisualTests` (phases 0 to 2), then `dotnet test --project tests/VisualCommit.VisualTests.Phase3` (phase 3), `dotnet test --project tests/VisualCommit.VisualTests.Phase4` (phase 4), `dotnet test --project tests/VisualCommit.VisualTests.Phase5` and `dotnet test --project tests/VisualCommit.VisualTests.Phase5Undo` (phase 5), `dotnet test --project tests/VisualCommit.VisualTests.Phase6` and `dotnet test --project tests/VisualCommit.VisualTests.Phase6Look` (phase 6; both save into phase 6's one folder, so run them one after the other: one `dotnet test` at the root empties the folder once for all its projects, two separate commands at once would each empty it), `dotnet test --project tests/VisualCommit.VisualTests.Phase7a` (phase 7a) |
| Time scripted checks alone (step times from `artifacts/visual/phase-N/timings/`) | `bash scripts/time-scripted-check.sh "<method filter>" [phase]`, for example `bash scripts/time-scripted-check.sh "*Check_01_commit_menu" 3` (the phase is 7a when left out, which runs `VisualCommit.VisualTests.Phase7a`; a phase it does not know exits 2 before anything is built; for phase 6, given as `6`, it runs `VisualCommit.VisualTests.Phase6`, or `VisualCommit.VisualTests.Phase6Look` when the filter names part L, such as `"*Check_L27*"`; for phase 5, given as `5`, it runs `VisualCommit.VisualTests.Phase5Undo` when the filter names part U, such as `"*Check_U16*"`, and `VisualCommit.VisualTests.Phase5` otherwise) |
| Tests with the watcher's events held back (R8, D262; the lead's lagged runs at a wave's merges) | `VISUALCOMMIT_WATCHER_LAG_MS=1500` in front of a test command: every repository watcher then holds each file-system event for 1.5 s (0 to 10,000 ms; the app logs it at Info). The list of tests the lead runs so is `docs/phase-7a-design.md` 0.4 (first run at WP1's merge, `a4a388e`, with plain `dotnet test`), for example `VISUALCOMMIT_WATCHER_LAG_MS=1500 bash scripts/heavy-run.sh --project tests/VisualCommit.VisualTests --filter-class "*Phase2Checks"` |
| The real keychains' own tests (D270): on Windows and macOS only CI's steps "Keychain (…)" run them, because they write in the machine's real store, so never on the owner's laptop; on Linux the Secret Service's under a private bus with a throwaway keyring, safe on any machine (run so far only in CI's two containers, first on `8b02805`) | `export XDG_DATA_HOME="$(mktemp -d)"; mkdir -p -m 700 "$XDG_DATA_HOME/keyrings"; VISUALCOMMIT_KEYCHAIN_TESTS=secret-service dbus-run-session -- sh -c 'echo throwaway \| gnome-keyring-daemon --unlock --components=secrets >/dev/null && dotnet test --project tests/VisualCommit.Hosting.Tests --filter-class "*SecretServiceTokenStoreRealTests"'` (needs `dbus` and `gnome-keyring`, which `scripts/setup-linux.sh` installs; a container needs `--cap-add=IPC_LOCK`). CI's Windows and macOS steps run `--filter-class "*WindowsTokenStoreRealTests"` with `VISUALCOMMIT_KEYCHAIN_TESTS=windows` and `"*MacKeychainTokenStoreRealTests"` with `macos` |
| Real-window pass (Windows only) | `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release`: the pass is a whole-project run, so on the laptop it runs under the heavy-run lock (D245) and no other heavy run starts beside it; without the lock it is `dotnet test --project tests/VisualCommit.RealWindowTests -c Release` (the script has not run the pass yet) |
| One real-window check alone (Windows only) | `dotnet test --project tests/VisualCommit.RealWindowTests -c Release --filter-method "*<test name>*"`, for example `--filter-method "*Check_S14*"` (run so at phase 6's gate for S14) |
| A phase's own real-window checks alone (Windows only; D246's two runs) | `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase7aRealWindowPass"`: it takes the mouse like the pass and empties `artifacts/visual/phase-7a/real-window/` (not run yet: phase 7a's gate runs it first) |
| The opt-in live command against real GitHub (Windows only; the owner's sandbox token, D250, D260) | `bash scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release`: about a minute with its build (63 s on 2026-10-30, 33 s with the build up to date); its findings in `artifacts/live/github/run.txt`; without the variables its three live tests fail at once by design (a session started before the owner set them: "What to know") |
| The live command's offline safety tests (Windows; no variables, no network) | `dotnet test --project tests/VisualCommit.LiveHostingTests -c Release --filter-class "*LiveSafetyTests"`: the token search, the request log and the clean-up's selection with made-up tokens, in process |
| Compare pictures with reference pictures (a gate's inspection; needs Python with Pillow and NumPy) | `python scripts/compare-pictures.py <reference folder> artifacts/visual <output folder>`: a difference picture per changed picture and `summary.md`, grouped by the window regions the changes lie in |
| Prepare a Linux machine: .NET SDK and system libraries | `bash scripts/setup-linux.sh` |
| CI result of a branch | `gh run list --branch <branch> --limit 3`, then `gh run view <run id>` |
| CI result without `gh`: the runs | `curl -s "https://api.github.com/repos/RahimPasha/VisualCommit/actions/runs?branch=<branch>&per_page=3"` shows each run's `id`, `head_sha`, `status` and `conclusion` |
| CI result without `gh`: the jobs of a run | `curl -s "https://api.github.com/repos/RahimPasha/VisualCommit/actions/runs/<run id>/jobs"` shows each job's `name` and `conclusion` |
| Screenshots a CI run took in each job | `gh run download <run id> -D <folder>` (needs `gh`, signed in) |

What to know about them:

- Tests run on Microsoft Testing Platform (set in `global.json`). Pass a project with
  `--project`, as every command here does; `dotnet test <path>` is not relied on.
- `global.json` accepts any 10.0 SDK, and its `version` has to stay in the first feature band
  (10.0.1xx): Ubuntu's packages, which a cloud session builds with, never leave that band,
  while the Windows machine and most CI jobs use newer ones.
- The default tests take about 55 minutes on the Windows machine with nothing else running (56 min
  25 s at phase 6's gate on 2026-10-24, rested, 5,037 tests; 43 min 22 s at phase 5's, 3,233 tests).
  Since D343 (phase 7a's gate) the whole run through `scripts/heavy-run.sh` runs at most four of
  the eleven test projects side by side; plain `dotnet test` and CI run as many as the machine has
  processors (eight on the laptop). Before that,
  nine test projects ran side by side since phase 6 added two walk-throughs (`VisualCommit.VisualTests.Phase6`
  and `.Phase6Look`), and the run is bound by the processors,
  which git's processes and the virus scanner keep busy: more tests side by side do not shorten
  it, and other load or a hot laptop lengthens it (a git call took 10 to 15 times as long as alone
  at phase 6's gate, D237). In that run, side by side, the projects ended after: part L's walk-through
  (`VisualCommit.VisualTests.Phase6Look`) 7 min 30 s, phases 0 to 2's 15 min 14 s, phase 4's 19 min
  56 s, phase 5's (`VisualCommit.VisualTests.Phase5`) 22 min 45 s, phase 6's
  (`VisualCommit.VisualTests.Phase6`) 30 min 36 s, phase 5's undo walk-through
  (`VisualCommit.VisualTests.Phase5Undo`) 35 min 20 s, phase 3's 39 min 22 s, `VisualCommit.Git.Tests`
  47 min 28 s and `VisualCommit.App.Tests` 55 min 14 s. `dotnet build` at the
  root takes about 2 minutes on the laptop. At phase 6's gate (run 40059391404) CI's jobs took
  26 min 47 s on Windows (its Test step 24 min 37 s), 25 min 22 s on macOS (Test step 23 min
  33 s; up to 43 min 12 s in an earlier run that day), 5 min 50 s on Linux (Test step 4 min 21 s)
  and 7 min 45 s and 7 min 16 s in the containers. The Test steps of Windows, macOS and Linux have a limit of 55 minutes and their jobs
  65 (D232, amending D184); the containers' limits stay. The lead raises that limit when the
  Windows or macOS Test step passes 44 minutes, four fifths of it (D232, D186's rule). Phase 1's checks load and clone a 100k-commit repo,
  built once into `%TEMP%/VisualCommit.Tests/shared/` and reused. They use real git and a temporary folder for
  every repo and data folder; they never open a window or touch the user's settings.
- During a phase, builders and fixers run only the test classes they touch, with plain
  `dotnet test`. Whole-project runs and the run at the root are the lead's, one at a time on the
  laptop through `scripts/heavy-run.sh` (D245); the full default run runs at the gate, and when the
  lead merges a wave. The script's lock is the folder `heavy-run.lock` in the tests' temporary root
  (`VisualCommit.Tests` in `%TMP%` on Windows, `$TMPDIR` or `/tmp` elsewhere); a second run waits,
  says once who holds the lock and looks again every 10 seconds, and takes over a lock whose
  script and `dotnet test` have both ended. Its exit code is `dotnet test`'s, 75 when it gave up
  waiting. Stop a run from Git Bash with `kill` (Commands): Claude Code's TaskStop ended only the
  top shell of a background command on the laptop, and a run started with `&` from a shell
  without job control ignores SIGINT. A run ended outright keeps the lock until its `dotnet test`
  ends. CI does not use the script; it has not run on Linux or macOS yet.
- The build trims the platform-specific files that packages bring under `runtimes/`, above all
  SkiaSharp's and HarfBuzzSharp's native libraries for 21 platforms (`Directory.Build.targets`,
  D239): it copies no native `.pdb` file (all of them are those two packages'), and on Windows, in
  a build without a `RuntimeIdentifier`, only the build machine's platform, from every package
  (`runtimes/win-x64`, with `win` and `any`; ANGLE's other Windows builds, Avalonia.Native's macOS
  library and Onigwrap's other platforms' builds go too). A test project's `bin/Debug` is about 49 MB instead of about
  589, and the main tree's `bin` and `obj` 0.64 GB instead of 6.96 GB for the same 40 folders
  (2026-10-28; the other 8, 2.27 GB of Release and Debug outputs nothing local runs, were removed
  and not built again). `-p:VisualCommitNativeSymbols=true`
  copies the `.pdb` files again and `-p:VisualCommitAllRuntimes=true` every platform's libraries
  (either also as an environment variable); a portable `dotnet publish` needs the second (phase
  8). Builds on macOS and Linux keep every platform's libraries.
- The scripted walk-through saves its screenshots under `artifacts/visual/phase-N/scripted/`.
  Every run that takes screenshots, `dotnet test` at the root included, empties that folder
  first and writes it again. `docs/test-reports/phase-N-pictures.sha256` lists the pictures that
  phase N's gate inspected; in Git Bash at the repo root,
  `tr -d '\r' < docs/test-reports/phase-N-pictures.sha256 | sha256sum -c` shows which pictures of
  a later run are the same files (the `tr` removes the line endings a Windows checkout adds,
  which `sha256sum` would take as part of each name); with `--quiet` it prints only those that
  differ, and a picture new since the list is not named at all. A gate writes its list right
  after its two passes, before anything else runs, with
  `find artifacts/visual -name '*.png' | sort | xargs sha256sum > docs/test-reports/phase-N-pictures.sha256`,
  and commits it once the inspection has passed. A round that runs only the real-window pass
  (D246; `docs/plan.md`'s Visual test gate, step 6) first checks the list written after the round
  before: every scripted picture in it must still be the same file
  (`tr -d '\r' < docs/test-reports/phase-N-pictures.sha256 | grep '/scripted/' | sha256sum -c --quiet`
  prints nothing); it writes the list again after that pass.
- The real-window pass is Windows-only and is not part of `dotnet test` at the root. It opens the
  app on the desktop and moves the real mouse for about 28 minutes (phases 0 to 6; 27 min 39 s at phase 6's gate): tell the
  owner before starting it, and run it only as part of the gate. It needs an unlocked desktop,
  Windows' high contrast off and the scripted screenshots, so run `dotnet test` first. The command builds the app and the
  pass in Release; do not add `--no-build`: the solution leaves the pass's project out, so
  `dotnet build` at the root does not rebuild it, and a stale build of the pass would run. Its
  screenshots, the pictures of the differences and `run.txt` (scaling, sizes, how much each
  screenshot differs from the scripted one) land under `artifacts/visual/phase-N/real-window/`.
- One real-window check run alone (`--filter-method`) empties its phase's `real-window/` folder,
  as the whole pass does, and takes over the mouse just the same: tell the owner before starting
  it, and run the whole pass again before a gate writes its list of pictures.
- Phase 7a's part of the pass starts the fake of GitHub (`FakeGitHub`) on port 47395 in the test
  process, so phase 7a's walk-through never runs beside it. Near its end it opens one tab in the
  owner's default browser, on the fake's page (D321); the owner closes it before the next run of
  the pass. The time the part adds is measured at phase 7a's gate.
- The live command (`tests/VisualCommit.LiveHostingTests`, D250) acts on the owner's GitHub
  account: it signs in, clones, lists pull requests and creates one in a private sandbox
  repository, with a fine-grained token for that repository alone. It reads two Windows user
  variables, set in Windows' Environment Variables dialog and never typed in a shell:
  `VISUALCOMMIT_LIVE_GITHUB_TOKEN` (the token) and `VISUALCOMMIT_LIVE_GITHUB_REPO`
  (`<owner>/<name>`); `docs/phase-7a-design.md` 14.6 says how the owner makes them. It runs never
  in CI or the default run: the project is in no solution, and CI's Windows job only builds it
  (D337). At its start it removes what a stopped run left in the sandbox (branches under
  `visualcommit-live/` and their pull requests, D324), and it closes and deletes what it made. The
  owner is told before it runs; at a gate it runs as D335 says (`docs/plan.md`'s Visual test gate,
  step 4). In a session where the two variables are set, every shell inherits the token: never
  print the whole environment (`env`, `printenv`, `set`, `export -p`, `Get-ChildItem Env:`); read
  a variable by its name, or filter the list (`env | grep -v '^VISUALCOMMIT_LIVE_'`), and tell
  every agent of that session so. A session whose VS Code started before the owner set the
  variables does not have them in its shells (a process inherits the environment it was started
  with); then give them to the command's own process alone from the Windows user scope, never
  printed, in PowerShell:
  `$env:VISUALCOMMIT_LIVE_GITHUB_TOKEN = [Environment]::GetEnvironmentVariable('VISUALCOMMIT_LIVE_GITHUB_TOKEN','User'); $env:VISUALCOMMIT_LIVE_GITHUB_REPO = [Environment]::GetEnvironmentVariable('VISUALCOMMIT_LIVE_GITHUB_REPO','User'); & 'C:\Program Files\Git\bin\bash.exe' scripts/heavy-run.sh --project tests/VisualCommit.LiveHostingTests -c Release`
  (Git Bash by its path: PowerShell finds no `bash` on `PATH` there, and a bare `bash` could be
  WSL's). The variables last for that one command. Check that they exist by name, length and
  prefix only (first done on 2026-10-30).
- To run the app without touching the real settings, set the environment variable
  `VISUALCOMMIT_DATA_DIR` to an empty folder first. Without it the app uses the per-user data
  folder: `%APPDATA%\VisualCommit` on Windows, `~/.config/VisualCommit` on Linux,
  `~/Library/Application Support/VisualCommit` on macOS. Since phase 6 the settings screen also
  writes git's own global configuration (D190), which that variable does not move: to keep the
  user's `~/.gitconfig` safe too, also set `GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL` to an empty
  file of its own and `XDG_CONFIG_HOME` to an empty folder, as the test harness does for every app
  session (D220). The app's git calls inherit them.
- A test run that hangs stops itself after 3 minutes and prints `HANG WATCHDOG` with the names of
  the tests that were running; a shared preparation (a scenario template, the 100k-commit repo) is
  held to 10 minutes of its own (D237). A test that really needs longer must raise the limit: the
  environment variable `VISUALCOMMIT_TEST_HANG_SECONDS` sets it. To look closer at a hang, run the
  test project's own program with `-diagnostics -longRunning 10`:
  `tests\<project>\bin\Debug\net10.0\<project>.exe` on Windows,
  `tests/<project>/bin/Debug/net10.0/<project>` elsewhere.
- CI has five jobs: one each for Windows, macOS and Linux, and two named "bare ubuntu
  container", one for each way the set-up script has of getting the .NET SDK. A push that
  changes only docs does not start a run.
- `gh run list` shows the duration of a run's latest attempt only. Each job's time comes from
  `gh run view <run id>`, or from the jobs API (`curl` above), whose `steps` also give each step's
  start and end, such as the Test step's.
- In Windows PowerShell write `curl.exe` for the two `curl` commands: plain `curl` is a
  different command there.

Prerequisites on any machine: .NET 10 SDK, Git 2.30 or newer (the app's minimum; the tests need
2.32 or newer, which can leave out the user's git configuration), and a way to read CI results: the
GitHub CLI (`gh`), signed in, or `curl`. `scripts/setup-linux.sh` installs what a Linux machine
lacks. The Windows development machine runs Git 2.56.0.windows.4 since 2026-10-28, first on
`PATH` (`C:/Program Files/Git/ucrt64/bin/git.exe` in Git Bash, `C:\Program Files\Git\cmd\git.exe`
from PowerShell, the same version), where it ran 2.36 before; CI runs 2.43 to 2.56,
so no machine runs 2.36 any more (D248).

## Docs map

| File | Purpose |
|---|---|
| `docs/requirements.md` | Scope: features, interaction model, quality bars |
| `docs/plan.md` | Phases, workflow, visual test gate, risks |
| `docs/status.md` | Current phase and resume point |
| `docs/architecture.md` | How the app is built |
| `docs/decisions.md` | Decisions and their reasons |
| `docs/handoffs/phase-N.md` | What phase N built and what the next phase must know |
| `docs/test-reports/phase-N.md` | Visual checks for phase N: expected results, then outcomes |
| `docs/cloud-sessions.md` | For the owner: working on the project from a Claude Code cloud session |
| `docs/phase-3-design.md` | Phase 3's design, kept as history; phase 3's decisions (D77 to D133), report and handoff cite its sections as "design N.N" |
| `docs/phase-4-design.md` | Phase 4's design, kept as history; phase 4's decisions (D139 to D152), report, handoff, status and plan outcome cite its sections as "design N.N"; its sections 14.1 to 14.5 say what the build and the review settled |
| `docs/phase-5-design.md` | Phase 5's design, kept as history; phase 5's decisions (D153 to D186), report, handoff, status and plan cite its sections as "design N.N"; its sections 15.8 and 15.9 say what the report, the build, the reviews and the gate settled |
| `docs/phase-6-design.md` | Phase 6's design, kept as history; phase 6's decisions (the design's D203 to D230), report, handoff, status and plan cite its sections as "design N.N"; its "As built" notes and the lead's rulings say what the build and the review settled (what the gate found is in the report's "Found by the gate" and the handoff); its section 16.2 holds the plan's changes, section 18 the work packages |
| `docs/phase-7a-design.md` | Phase 7a's design: its decisions D265 to D338, the shared contracts (section 1), one section per work package (2 to 14) with its files, tests and "done when", the open points (15) and, from section 16 on, what each wave's build and reviews settled; the phase's report, handoff and status cite its sections as "design N.N" |
| `docs/review-before-phase-7.md` | The review of phase 6's cost in time, tokens and disk (2026-10-26), kept as history: what was measured, the causes and the ranked proposals; D239 to D248 cite its sections and proposal ids, and a proposal that no decision names is still only a proposal |
