# Phase N visual test report — <phase name>

The Check, Steps and Expected columns are written at the start of the phase, before the UI they
test is built. The result columns are filled in when the gate runs.

## Run

Filled in when the gate runs, for each of its two passes: date, app commit, the machine (its
operating system and version), the display scaling of the real-window pass, the screenshot
folders, and how many pictures were inspected. The gate's two passes are run on one commit by a
session on the Windows machine.

If a cloud session ran the scripted pass earlier, its run is listed here too, marked "cloud
session, before the gate", and its results stand in the Scripted column until the Windows
session replaces them with its own. It does not count as the gate's scripted pass.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes).

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| 1 | | | | | | | |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say "n/a".
Say for each check, when writing it, whether it is repeated there.

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

The CI run of the gate's commit and its result on each platform, and which of the screenshots CI
took on macOS and Linux were opened.

## macOS checklist for the owner

A short list of things to try by hand on a Mac for this phase.
