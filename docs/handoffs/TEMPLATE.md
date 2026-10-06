# Phase N handoff — <phase name>

Written at the end of phase N for the session that runs phase N+1. That session has not seen any
of this phase's conversation. Keep it short: link to code and docs rather than copying them, and
leave out anything `architecture.md` already says.

## Result

- What the phase delivered, one line per item in the plan's "Delivers" list, with the test or
  visual check that proves it.
- What it did not deliver, and where each item went (which phase of `plan.md`, backlog, or dropped).

## State of the repo

- Branch and last commit. Merged into `master` or awaiting acceptance.
- Unit and integration tests: how many, all passing or which fail.
- Visual test gate: passed or not, link to the report, any check that could not run.
- CI: the result of the last run on GitHub for each platform, with the run link, or unverified and why.

## Environment

.NET SDK version, Avalonia and other key package versions, Git version, Windows version, and the
display scaling the screenshots were taken at.

## Build, run and test

Exact commands, copied from a terminal where they worked.

## Running the visual gate

The commands for the scripted walk-through and the real-window pass, how long each takes, where
the screenshots land, and what the real-window pass needs (unlocked desktop, free mouse).

## What changed in the code

The main things added this phase and where they live. Name the entry points the next phase will
touch.

## Patterns to follow

How things are done here, with one example file for each: adding a git command, a view and its
view model, a context-menu action, a scenario repo, a visual check.

## Deviations from the plan

What was done differently and why. Link each to its entry in `decisions.md`.

## Known issues

Bugs, shortcuts and debt, each with how much it matters and where it is.

## Things that cost time

Tool quirks, dead ends and surprises the next session should not rediscover.

## For the next phase

What to reuse, what to do first, and the risks to watch.

## Waiting on the owner

Open questions and anything that needs their decision or their accounts.
