# Phase N visual test report — <phase name>

The Check, Steps and Expected columns are written at the start of the phase, before the UI they
test is built. The result columns are filled in when the gate runs.

## Run

Filled in when the gate runs: date, app commit, Windows version, display scaling, screenshot
folders, and how many pictures were inspected.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes).

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|
| 1 | | | | | | | |

"Real window" is filled in only for the checks repeated in the real-window pass; the others say "n/a".
Say for each check, when writing it, whether it is repeated there.

## Changes to expected results

Any expected result changed after it was first committed, with the reason. This includes
expected results of earlier phases that this phase changed on purpose.

## Found by the gate

Faults the gate found, and the commit that fixed each.

## Could not run

Checks or parts of the gate that could not run, why, and what that leaves unproven.

## Other platforms

The CI run of the gate's commit and its result on each platform, and which of the screenshots CI
took on macOS and Linux were opened.

## macOS checklist for the owner

A short list of things to try by hand on a Mac for this phase.
