# Phase N visual test report — <phase name>

The Check, Steps and Expected columns are written before the UI they test is built: since D244
each part's checks are written and committed just before its work package is built. The result
columns are filled in when the gate runs.

## Run

Filled in when the gate runs, for each of its two passes: date, app commit, the machine (its
operating system and version), the display scaling of the real-window pass, the screenshot
folders, and how many pictures were inspected. The gate's two passes are run on one commit by a
session on the Windows machine, except that a commit changing only
`tests/VisualCommit.RealWindowTests` re-runs only the real-window pass, and the default run of the
last commit it ran on stands for its own (D246; `docs/plan.md`'s Visual test gate, step 6). Each
round lists the commit of each pass. The two runs of the new phase's real-window checks on their
own before the first counted round are listed too, marked "before the gate". Since phase 7a so
are the live command's two runs at the gate, each with its commit (D335); "Live command" gives
what they found.

If a cloud session ran the scripted pass earlier, its run is listed here too, marked "cloud
session, before the gate", and its results stand in the Scripted column until the Windows
session replaces them with its own. It does not count as the gate's scripted pass.

## Live command

Since phase 7a (`docs/plan.md`'s Visual test gate, step 4; D250, D335): the opt-in command
against real GitHub (`tests/VisualCommit.LiveHostingTests`), with the owner's fine-grained token
for their private sandbox repository, the owner told before each run. Filled in from its
`artifacts/live/github/run.txt` for each run: the date and time, the commit (and the code it
built, when they differ), the outcome (tests passed of those run, and the time), what it made in
the sandbox and removed again, its findings about GitHub's answers with what each changed in the
fake of GitHub, and the search of run.txt and the console output for the token. Its runs: any
made during the phase; at the gate, one before the gate's pictures, whose corrections to the fake
are committed before the passes, and one on the counted round, after the real-window pass and
the list of pictures. The next run overwrites run.txt, so each run's copy is kept outside the
repository and named here. The token is written here in no form.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes).

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| 1 | | | | | | | |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say "n/a".
Say for each check, when writing it, whether it is repeated there.

## What proves each Delivers item

`docs/plan.md`'s "Delivers" list for the phase, one row per item, the items carried in from
earlier phases included: the checks that prove it (a phase in parts gives each part a column, as
phase 7a's report does) and, under "Elsewhere", the automated tests, classes or files that do.
Every item has at least one check or test; an item that is only a file or a doc is proven by the
file, named here and in the handoff (CLAUDE.md's Rules).

| Delivers item | Checks | Elsewhere |
|---|---|---|
| | | |

## Changes to expected results

Any expected result changed after it was first committed, with the reason. This includes
expected results of earlier phases that this phase changed on purpose, and any check whose mark
for the real-window pass was changed after it was written.

## Found by the gate

Faults the gate found, and the commit that fixed each.

## Could not run

Checks or parts of the gate that could not run on the Windows machine either, why, and what that
leaves unproven. A real-window check that was not run only because the session was not on
Windows does not belong here: it stays "Not run: needs Windows" in the table, and the gate is
unfinished until a Windows session has run it.

## Other platforms

The CI run of the gate's commit and its result in each job (the three platforms and the two
bare Ubuntu containers), and which of the screenshots CI took on macOS and Linux were opened.

## macOS checklist for the owner

A short list of things to try by hand on a Mac for this phase.
