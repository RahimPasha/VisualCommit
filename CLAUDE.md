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
3. Read `docs/plan.md`: the Workflow, Visual test gate and Risks sections, and the section for the current phase.
4. Read `docs/architecture.md`, `docs/decisions.md`, and the parts of `docs/requirements.md` the phase covers.
5. Read the previous phase's handoff, `docs/handoffs/phase-<N-1>.md`. None exists until Phase 0 is closed.
6. If code exists, build it and run the default tests to see the state you inherited, before
   changing anything. If they fail, fix or report that first. On a Linux machine where
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
   visual checks, then the closing steps below. Commit.
4. Write the phase's visual checks into `docs/test-reports/phase-N.md` from the template, each
   with concrete steps and a concrete expected result, and each marked with whether it is
   repeated in the real window. Commit this before building the UI the checks test.

To **resume a phase**: continue from "Next step" in `docs/status.md`.

## What the owner says and what it means

| Owner says | Do |
|---|---|
| "Start phase N" | The start steps above, then build phase N through to its closing steps |
| "Continue" | Resume the phase whose Progress checklist in `docs/status.md` still has unticked items (state "In progress" or "Blocked") |
| "Phase N accepted" | First `git fetch origin`, check out the branch that `docs/status.md` names for the phase and fast-forward it (`git merge --ff-only origin/<branch>`), so that what is checked is what `origin` holds now. Only when `docs/status.md` on that branch says the phase is "Awaiting acceptance", and nothing but docs changed after the "Gate commit" it names (run at the repo root, `git diff --stat <gate commit> <branch> -- . ':(exclude)docs' ':(exclude)*.md'` prints nothing): check out `master`, fast-forward it to `origin/master`, `git merge --no-ff` that branch, set the phase to "Done" in `docs/status.md`, commit, push `master`. Setting it to "Done" also resets the file for the next phase, as phase 0's acceptance (`c033b58`) did: "Current phase" says none is in progress and names the merge commit; Progress goes back to its paragraph on how the checklist is built; the CI section names the branch's last run; "Next step" says to wait for "Start phase N+1" and points to the handoff's "For the next phase"; the phase's working notes leave "Notes for whoever resumes". Then read the CI run that the push of `master` started and record it in the CI section (a docs-only commit). In any other case, change nothing more, say what is still open and ask |

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
  rewrites "Next step" (both passes of the gate again on one commit on the Windows machine,
  then closing step 6 with the new "Gate commit") and, when "Blocked", "Waiting on the owner".
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

1. Run the visual test gate and complete `docs/test-reports/phase-N.md`.
2. Update `docs/architecture.md` so it describes what is actually built.
3. Update `docs/plan.md`: move anything this phase did not deliver into the phase that now owns it, and update the risks.
4. Write `docs/handoffs/phase-N.md` from `docs/handoffs/TEMPLATE.md`.
5. Cold-read check: start a fresh agent with no conversation context and give it only the repo.
   Tell it to assume this phase has been accepted and merged, and ask it how to build, run and
   test the app and how it would begin the next phase. Fix every gap it hits in the docs. Commit.
6. Update `docs/status.md`: links to the handoff and report, the last CI run of the branch with
   its result in each job, the "Gate commit" (the commit both passes of the gate ran on), and
   anything waiting on the owner.
   Set the phase to "Awaiting acceptance" only if the whole gate passed: a session on the
   Windows machine has run `dotnet test` and the real-window pass on the last commit that
   changed anything other than docs, and has inspected the pictures of both. The only part that
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
  2. run `dotnet test` and the real-window pass on one commit, fix what fails and run both
     again, and inspect every picture of both passes. The real-window code, with the harness
     items that `docs/status.md` marks "written, not run", has only been compiled until then;
  3. judge the phase's "Done when" items that are judged on the Windows machine, named one by
     one; if that changes anything other than docs, do step 2 again on the new commit;
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
| Tests chosen by name | `dotnet test --project tests/VisualCommit.Git.Tests --filter-method "*Linear*"` |
| One phase's scripted checks | `dotnet test --project tests/VisualCommit.VisualTests --filter-class "*Phase1Checks"` |
| Scripted visual walk-through only | `dotnet test --project tests/VisualCommit.VisualTests` |
| Real-window pass (Windows only) | `dotnet test --project tests/VisualCommit.RealWindowTests -c Release` |
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
- The default tests take about 2 to 2.5 minutes on the Windows machine, most of it in phase 1's
  checks that load and clone a 100k-commit repo (built once into
  `%TEMP%/VisualCommit.Tests/shared/` and reused). They use real git and a temporary folder for
  every repo and data folder; they never open a window or touch the user's settings.
- The scripted walk-through saves its screenshots under `artifacts/visual/phase-N/scripted/`.
  Every run that takes screenshots, `dotnet test` at the root included, empties that folder
  first and writes it again. `docs/test-reports/phase-N-pictures.sha256` lists the pictures that
  phase N's gate inspected; in Git Bash at the repo root,
  `tr -d '\r' < docs/test-reports/phase-N-pictures.sha256 | sha256sum -c` shows which pictures of
  a later run are the same files (the `tr` removes the line endings a Windows checkout adds,
  which `sha256sum` would take as part of each name).
- The real-window pass is Windows-only and is not part of `dotnet test` at the root. It opens the
  app on the desktop and moves the real mouse for about 3 minutes (phases 0 and 1): tell the
  owner before starting it, and run it only as part of the gate. It needs an unlocked desktop
  and the scripted screenshots, so run `dotnet test` first. The command builds the app and the
  pass in Release; do not add `--no-build`: the solution leaves the pass's project out, so
  `dotnet build` at the root does not rebuild it, and a stale build of the pass would run. Its
  screenshots, the pictures of the differences and `run.txt` (scaling, sizes, how much each
  screenshot differs from the scripted one) land under `artifacts/visual/phase-N/real-window/`.
- To run the app without touching the real settings, set the environment variable
  `VISUALCOMMIT_DATA_DIR` to an empty folder first. Without it the app uses the per-user data
  folder: `%APPDATA%\VisualCommit` on Windows, `~/.config/VisualCommit` on Linux,
  `~/Library/Application Support/VisualCommit` on macOS.
- A test run that hangs stops itself after 3 minutes and prints `HANG WATCHDOG` with the names of
  the tests that were running. A test that really needs longer must raise the limit: the
  environment variable `VISUALCOMMIT_TEST_HANG_SECONDS` sets it. To look closer at a hang, run the
  test project's own program with `-diagnostics -longRunning 10`:
  `tests\<project>\bin\Debug\net10.0\<project>.exe` on Windows,
  `tests/<project>/bin/Debug/net10.0/<project>` elsewhere.
- CI has five jobs: one each for Windows, macOS and Linux, and two named "bare ubuntu
  container", one for each way the set-up script has of getting the .NET SDK. A push that
  changes only docs does not start a run.
- In Windows PowerShell write `curl.exe` for the two `curl` commands: plain `curl` is a
  different command there.

Prerequisites on any machine: .NET 10 SDK, Git 2.30 or newer (the app's minimum; the tests need
2.32 or newer, which can leave out the user's git configuration), and a way to read CI results: the
GitHub CLI (`gh`), signed in, or `curl`. `scripts/setup-linux.sh` installs what a Linux machine
lacks.

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
