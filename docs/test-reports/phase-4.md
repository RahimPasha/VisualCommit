# Phase 4 visual test report — Rebase and conflict resolution

The Check, Steps and Expected columns of the table below are written at the start of the phase,
before the UI they test is built (D15), from `requirements.md`, `plan.md`, `docs/phase-4-design.md`
and decisions D139 to D151. Every git state in them is derived by running real git (2.36) on the
scenario repos as `Scenarios.Phase4.cs` builds them, and the finished text is reviewed
independently before it is committed. The result columns are filled in when the gate runs.

How to read it: "What the checks share" holds what the checks of each part rely on (the scenario
repos and their graphs, the texts the design fixes, git's own texts); "Checks" is the one table of
all checks; "Changes to expected results" lists what phase 4 changes in earlier phases' checks on
purpose. "RW" marks a check, or steps of it, that the real-window pass repeats; "n/a" says why it
does not.

## Run

Filled in when the gate runs, for each of its two passes: date, app commit, the machine, the display
scaling of the real-window pass, the screenshot folders, and how many pictures were inspected.

## What the checks share

### Conventions of every part

- **Commits** are named by their subjects, and by their short SHA (the first 7 characters) where a
  text on screen shows it; those SHAs are the ones `Phase4ScenarioTests` pins. A commit the app makes
  is named by its subject only, unless its id is stated as the one plain git gives for the same
  commit. Refs are named by their short names.
- **Times** are UTC. A scenario's commits are dated by `TempRepo`'s clock, 2026-01-01 12:00 plus one
  minute per commit; commits the app makes or rewrites have the committer date 2026-01-02 09:00 and
  the identity "Test Author" <author@example.com> (D72); a rebase keeps each commit's author date.
- **Lanes** count from 0 at the left; **colours** are lane colour numbers, as in phase 1's report
  (dark: 0 `#7C8CFF`, 1 `#3FB97F`, 2 `#E0A23B`, 3 `#E5606B`, 4 `#4FB3D9`).
- **Menus** are written header first, items in order, "—" for a separator; "(danger)" marks an item
  drawn in `VcDangerBrush`, "(disabled: "…")" a disabled item with its tooltip. The scripted run
  reads a menu with `ShellDriver.Menu()` and clicks with `ClickMenuItem`; the real-window pass with
  `RealApp.WaitForMenu`, `MenuItemNames` and `ClickMenuItem` (phase 3's conventions).
- **Resting the pointer** on an item, button or row, **waiting for a change made outside the app**,
  **"no menu is open"**, **log lines**, **paths** and **the pointer before a capture** (the neutral
  point (760, 687) at 1100×700) are as phase 3's report defines them.
- **One test per app start**: a check whose steps start the app again is one test method per start.
- **The banner** is described by its text, its buttons, whether the text takes "one line" or "more",
  and whether the buttons stand beside the text or on a row of their own (design 5.2); never by a
  measured height.
- **Undo and redo** in the resolver are "the platform's undo" and "the platform's redo" (Ctrl+Z and
  Ctrl+Y on Windows and Linux, the platform's own on macOS: `ShellDriver.Undo()`, `Redo()`).
- **The result's text** is read through the editor's test surface (`ConflictResultEditor.Text`) in
  the scripted run and through its Value pattern in the real window; its caret does not blink in
  either harness (D151).
- **Git's own texts** given word for word are asserted only when `git --version` is 2.36; on any
  other version a check asserts the classifying line and the dialog's title, summary and buttons
  (phase 3's rule).

### Part A: rebase from the menus and by drag, pull with rebase

Written with the checks.

### Part B: the interactive rebase and squash

Written with the checks.

### Part C: conflicts, Continue, Skip and Abort, the resolver

Written with the checks.

### Part D: the light theme at 1920×1080

Written with the checks.

## Checks

Result values: Pass, Fail, Not run (with the reason in Notes).

| # | Check | Steps | Expected result | Scripted | Real window | Screenshots | Notes |
|---|---|---|---|---|---|---|---|

## What proves each Delivers item

Written with the checks (design 13.5 is the outline).

## Changes to expected results

Expected results of earlier phases that phase 4 changes on purpose (design 12.1), and anything
changed in this report after it was first committed (with the reason). The tests change in the same
commit as the code that changes them.

- **Phase 1, checks 13 and 14** (D150, `a6788d8`). Check 13 failed once on CI's `ubuntu-latest`
  (run 38213858149): it cloned the 100k-commit repository from a `file://` URL and had to catch a
  picture with a percentage between 1 and 99, and a fast runner finished the clone between two
  pictures. The checks now clone a bare copy of that repository (`large-history-v1.git`, made once
  per machine beside the working copy) served by `AuthRemoteServer` on 127.0.0.1:47392, a second
  fixed port of the visual checks (D97 amended), and the server holds the clone's pack at 45 %.
  - Check 13, step 3: the URL typed is `http://127.0.0.1:47392/large-history-v1.git`.
  - Check 13, step 4: "wait until the progress shows the held percentage; screenshot; the server
    lets go". `13b` shows the URL above, the progress bar at 45 % and the text exactly `Counting
    objects 45%` (git's last line `remote: Counting objects:  45% (45001/100002)`), the fields
    disabled and Cancel enabled. Only the temporary parent folder's name varies between runs.
  - Check 13 after step 5 and check 14's results are unchanged; check 14 clicks Cancel while the
    clone is held.
  - CI run 38232105795 (`b433936`) passed every job with the new checks.
- **Phase 3's menus that gain items** (design 7.1): checks 1, 2, 5 (the keyboard walk of the commit
  menu), 6, 7, 8 to 12 (the two-commit menu gains `Squash 2 commits…`, disabled there), 9b (the
  conflicted file's menu gains `Resolve conflict` and `Open in merge tool`), 13, 13b, 16, 18b, 20,
  27, 28 and 30 (the drop menus too); the real-window pass's parts B and C; `RepositoryMenusTests`,
  `RepositoryMenusTests.Window` and `ContextMenuAdapterTests`: item lists, heights, and the first
  enabled item where it moves. Check 3's menus do not change. The exact new lists are written with
  phase 4's checks and copied into phase 3's expectation files by the package that adds the items.
- **Phase 3's banner** (design 5.2): the Continue and Skip buttons and the layout rule (checks 9, 9b,
  25 and 27); the icon's tooltip (checks 9, 14, 15b and 25, the real-window pass's part B); the
  texts marked "new" in design 5.2 (check 12 step E's empty pick, check 25's list, rebase and empty
  texts). The merge and single-pick texts do not change.
- **Phase 3's stage panel** (design 5.3): the conflicted row's tooltip `Conflict: click to resolve
  it` (checks 9 step 4 and 25 step 2); the empty pick's commit tooltip `This cherry-pick changes
  nothing: skip it`; the message filled from `MERGE_MSG` keeps a commit's own `#` lines.
- **Phase 3's check 11b**: one git call for the list, so `sequencer/` is there at the stop; the Abort
  question adds that the commits already made are dropped too; after Abort `main` is where it was
  before the first pick.
- **Phase 3's check 25 step 4**: the stopped pick is committed in a terminal (the check runs `git
  commit --no-edit` through `TempRepo`); the check then shows the paused banner, Continue enabled,
  Skip disabled with its reason and the Abort that keeps HEAD; a new step on a fresh copy shows the
  stage panel's `Continue cherry-pick` (step 2's button text, "Stage files to commit" before).
- **Phase 2's conflicted-file diff body** ("This file has a merge conflict") is replaced by the
  resolver; no phase 2 check shows it, and phase 2's pictures do not change (the spike kept all 66
  byte-identical, `a7ce01f`).

## How the expected git states were derived

Written with the checks.

## Found by the gate

Faults the gate found, and the commit that fixed each.

## Could not run

Checks or parts of the gate that could not run on the Windows machine either, why, and what that
leaves unproven.

## Other platforms

The CI run of the gate's commit and its result in each job, and which of the screenshots CI took on
macOS and Linux were opened.

## macOS checklist for the owner

Written at the gate.
