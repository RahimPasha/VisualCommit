# Tracery

A cross-platform desktop Git client in the spirit of GitKraken. Avalonia UI, .NET 10, all C#,
driving the real `git` executable. Open source (MIT).

The project is built in phases, one phase per session. A session starts with no memory of the
earlier ones, so the repo is the only source of context. Keep it that way: anything the next
session needs goes into the files below, not just into the conversation.

## Start of every session

1. Find the right branch. Run `git status` and `git branch --list "phase/*" --no-merged master`.
   If an unmerged phase branch exists, check it out: its `docs/status.md` is the current one,
   and the copy on `master` is out of date.
2. Read `docs/status.md`: which phase is current and where work stopped.
3. Read `docs/plan.md`: the Workflow, Visual test gate and Risks sections, and the section for the current phase.
4. Read `docs/architecture.md`, `docs/decisions.md`, and the parts of `docs/requirements.md` the phase covers.
5. Read the previous phase's handoff, `docs/handoffs/phase-<N-1>.md`. None exists until Phase 0 is closed.
6. If code exists, build it and run the default tests to see the state you inherited, before
   changing anything. If they fail, fix or report that first.
7. If the working tree has uncommitted changes, read the Notes in `docs/status.md` and the diff,
   then finish or commit them before starting anything new. Do not discard work you do not understand.

Then, to **start a phase**:

1. Check that the previous phase is merged into `master`. If it is still "Awaiting acceptance",
   stop and ask the owner. Never start from a `master` that lacks the previous phase.
2. Create the phase branch from `master`, using the branch name listed for the phase in `docs/status.md`.
3. In `docs/status.md`, set the phase to "In progress" and build the Progress checklist: the
   phase's "Delivers" items split into steps small enough to finish in one sitting, then its
   visual checks, then the closing steps below. Commit.
4. Write the phase's visual checks into `docs/test-reports/phase-N.md` from the template, each
   with concrete steps and a concrete expected result. Commit this before building the UI the
   checks test.

To **resume a phase**: continue from "Next step" in `docs/status.md`.

## What the owner says and what it means

| Owner says | Do |
|---|---|
| "Start phase N" | The start steps above, then build phase N through to its closing steps |
| "Continue" | Resume the phase whose Progress checklist in `docs/status.md` still has unticked items (state "In progress" or "Blocked") |
| "Phase N accepted" | `git merge --no-ff` the phase branch into `master`, set the phase to "Done" in `docs/status.md`, commit |

## Rules

- Work only on the phase the owner asked for. Do not start the next phase unasked.
- Build each phase on its own branch (names are in `docs/status.md`). Merge into `master` only
  after the owner accepts the phase. Never push; the owner does that.
- Changes to planning docs made outside a phase are committed directly on `master`.
- Commit at every working state. In each commit keep "Next step" in `docs/status.md` accurate and
  tick the items that are complete. Before stopping for any reason, commit and fill in the Notes
  section, so a new session can resume from that commit.
- Every item in a phase's "Delivers" list must be proven by at least one automated test or one
  visual check. Items that are only files or docs (README, CI workflow, the Commands section) are
  proven by the file existing; the handoff names it.
- The default test run (`dotnet test` at the repo root) must never touch the real desktop or the
  user's real settings. The real-window pass is a separate, Windows-only, opt-in command. It takes
  over the mouse, so run it only as part of the gate and tell the owner before starting it.
- A phase is not done until the visual test gate in `docs/plan.md` has passed: screenshots taken
  and every one inspected. Passing unit tests is not enough. If part of the gate could not run, say so.
- Expected results for visual checks are written before the UI exists. Changing one afterwards
  needs a reason recorded in the test report.
- CI runs only after the owner pushes the repo. Until then, report CI as unverified, never as green.
- Record every decision that changes the plan, scope or architecture in `docs/decisions.md`, with
  its reason. Do not reopen a recorded decision without the owner.
- Add each command to the Commands section below the first time it works.
- Feature and requirement numbers (C4, O1, T2, Q1, R3 ...) are defined in `docs/requirements.md`.

## Closing a phase

1. Run the visual test gate and complete `docs/test-reports/phase-N.md`.
2. Update `docs/architecture.md` so it describes what is actually built.
3. Update `docs/plan.md`: move anything this phase did not deliver into the phase that now owns it, and update the risks.
4. Write `docs/handoffs/phase-N.md` from `docs/handoffs/TEMPLATE.md`.
5. Cold-read check: start a fresh agent with no conversation context and give it only the repo.
   Tell it to assume this phase has been accepted and merged, and ask it how to build, run and
   test the app and how it would begin the next phase. Fix every gap it hits in the docs. Commit.
6. Update `docs/status.md`: links to the handoff and report, and anything waiting on the owner.
   Set the phase to "Awaiting acceptance" only if the whole gate passed. If a check failed or
   part of the gate could not run and the owner must decide, set it to "Blocked" and say why. Commit.
7. Tell the owner what passed, what did not, and what needs their decision.

## Commands

Nothing is built yet. Phase 0 fills this in: build, run, unit tests, scripted visual walk-through,
real-window pass.

Prerequisites on the development machine: .NET 10 SDK, Git 2.30 or newer.

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
