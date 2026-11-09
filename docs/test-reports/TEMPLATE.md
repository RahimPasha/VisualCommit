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

Since phase 7a (`docs/plan.md`'s Visual test gate, step 4; D250, D335), and for Azure DevOps
Services and gitlab.com too since phase 7b (D364): the opt-in command against the real services
(`tests/VisualCommit.LiveHostingTests`), with the owner's tokens for their sandbox repositories,
the owner told before each run. Filled in per provider from its `run.txt`
(`artifacts/live/github/`, `artifacts/live/azure-devops/`, `artifacts/live/gitlab/`) for each run:
the date and time, the commit (and the code it built, when they differ), the outcome (tests
passed of those run, and the time), what it made in the sandbox and removed again, its findings
about the service's answers with what each changed in that provider's fake, and the search of
run.txt and the console output for the token. Since phase 7b each run with the tokens follows a
run of the offline classes alone on the same commit, which is named too. Its runs: any made
during the phase; at the gate, one before the gate's pictures, whose corrections to the fakes are
committed before the passes, and one on the counted round, after the real-window pass and the
list of pictures. The next run overwrites run.txt, so each run's copy is kept outside the
repository and named here. The tokens, the owner's addresses, the Azure DevOps organization's
name and GitLab's namespace are written here in no form (D346, D352, D384).

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
