# Status

Last updated: 2026-10-24

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git, a public repository. The local
folder on the development machine is still named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Done | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
| 1. Repos and commit graph | Done | `phase/1-graph` | [phase-1](handoffs/phase-1.md) | [phase-1](test-reports/phase-1.md) |
| 2. Diff and commit workflow | Done | `phase/2-diff-commit` | [phase-2](handoffs/phase-2.md) | [phase-2](test-reports/phase-2.md) |
| 3. Branch, remote, stash and tag operations | Done | `phase/3-operations` | [phase-3](handoffs/phase-3.md) | [phase-3](test-reports/phase-3.md) |
| 4. Rebase and conflict resolution | Done | `phase/4-rebase-conflicts` | [phase-4](handoffs/phase-4.md) | [phase-4](test-reports/phase-4.md) |
| 5. History, traceability and undo | Done | `phase/5-history` | [phase-5](handoffs/phase-5.md) | [phase-5](test-reports/phase-5.md) |
| 6. Multi-repo and productivity | Awaiting acceptance | `phase/6-multi-repo` | [phase-6](handoffs/phase-6.md) | [phase-6](test-reports/phase-6.md) |
| 7. Hosting | Not started | `phase/7-hosting` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 6, multi-repo and productivity: **Awaiting acceptance** since 2026-10-24, on
`phase/6-multi-repo`, built on the Windows machine from 2026-10-18 from `master` at `acdac12` (phase
5's merge `c299939` and the status commits after it). Its visual test gate passed on `b561b7c`: the
default run's 5,037 tests and the real-window pass's 67, every picture inspected; what was built and
proven is in the [handoff](handoffs/phase-6.md), the gate in the [report](test-reports/phase-6.md). Phase 5 was accepted by the owner on 2026-10-17 and merged into `master` (merge
commit `c299939`); its gate ran on `1b2e734`. Phase 4 was accepted on 2026-10-12 (merge commit
`ce5f337`), phase 3 on 2026-10-11 (merge commit `8b33745`), phase 2 on 2026-10-10 (merge commit
`e039070`), phase 1 on 2026-10-10 (merge commit `b5eb1ed`) and phase 0 on 2026-10-06 (merge commit
`b5f4723`).

Gate commit: `b561b7c`, the commit both passes of the gate ran on (with the branch at `45cc63f`,
whose commits after `b561b7c` change docs only); "Phase 6 accepted" checks that nothing but docs
changed after it.

### Progress

Each item is ticked in the commit that completes it. The numbers are plain step numbers, not
requirement numbers. The design (step 3) may split the Git-layer and App steps further; it then
says how its work packages map onto them.

Start:

- [x] 1. Phase branch created, phase set to "In progress", this checklist; the owner's answers to
  the open points of phase 5's handoff recorded (D187 to D194) and carried into `plan.md` and
  `requirements.md`. (2026-10-18: the dashboard is a tab of its own; opening a workspace shows
  only its dashboard; workspaces in `workspaces.json`; git's settings global with a "This
  repository" section; the palette on Ctrl+Shift+P; a fixed set of keys; every checkout keeps
  ignored files by default, a setting turns it off; the theme can follow the system.)
- [x] 2. Baseline: the default run on `master` (`acdac12`) as inherited, before anything changes:
  3,233 of 3,233 passed in 37 min 29 s (2026-10-18, the laptop otherwise idle; phase 5's gate:
  43 min 22 s). The projects ended after: `VisualCommit.VisualTests` 10 min 14 s,
  `.Phase5` 18 min 56 s, `.Phase4` 20 min 10 s, `VisualCommit.Git.Tests` 27 min 31 s,
  `.Phase5Undo` 29 min 31 s, `.Phase3` 32 min 8 s, `VisualCommit.App.Tests` 37 min 26 s.
- [x] 3. Design in `docs/phase-6-design.md`: every git fact derived with Git 2.36 (and 2.55 where
  words or exit codes may differ), the texts, the layouts, the work packages, an estimate of what
  the phase adds to the default run (45 minutes) and to the real-window pass (25). Independent
  reviews, each finding checked by an adversarial verifier; the design revised from every
  confirmed one; questions of taste the reviews raise go to the owner; the design's own decisions
  recorded. (Ten research notes, eight section drafts settled by the lead's rulings, first version
  `9d6d2e3`. Six reviews (git safety, code fit, what the user sees, tests and gate, performance and
  threads, completeness) found 93 problems; adversarial verifiers confirmed 91 in full or in part
  (1 blocker: the global identity edited from an in-repository read that `includeIf` changes; 10
  major) and refuted 2; all 91 applied. The owner answered nine questions on 2026-10-18 (D195 to
  D202, and D221's reading of D193). The design's decisions are D203 to D230. Its work packages
  (section 18) map onto the steps below as its "Status steps" table says.)
- [x] 4. Leftovers named in phase 5's handoff: the real-window csproj's stale comment on the pass's
  length; `scripts/time-scripted-check.sh` for phase 6; plan risk 28 (seven fetch and pull tests
  that fail locally with Visual Studio's Git 2.55) understood before fetch and pull change for the
  dashboard. (Risk 28 explained on 2026-10-18: Visual Studio's Git has no `git http-backend`, which
  the test server `AuthRemoteServer` runs; 6 of 49 fetch, pull, push and remotes tests failed with
  it, all against that server; the research named a seventh, a FakeSsh test, as crashing under an
  inherited `MSYSTEM=MINGW64`, which a check on 2026-10-18 did not reproduce; plan risk 28. The server
  now fails at its start with a message naming the missing `http-backend`; the csproj comment says
  25 minutes for phases 0 to 5; the timing script sends phase 6 and later to their own project.
  Removing `MSYSTEM` in the tests' isolation goes with the design's isolation changes.)
- [x] 5. Harness: phase 6's scenario repositories (a workspace of ten repositories in mixed
  states: clean, ahead, behind, diverged, uncommitted changes, a conflict in progress, detached,
  no upstream, no remote, a folder that is gone; a fetch refspec that maps names differently),
  pinned by tests; the project `VisualCommit.VisualTests.Phase6` in the solution; the timing
  script and CLAUDE.md's Commands; the real-window project's link to `Phase6Expectations*.cs`; a
  fixed port of its own for a test git server, if the bulk-fetch checks use one (amending D97 as
  D150 did); the driver helpers the checks need.
  (WP-L done in `8dca0d3`. WP0, the lead's contracts, stubs and call points of the design's
  section 18, done in the main checkout on 2026-10-18 in five commits, the last `8e79628`: no
  change of behaviour or pictures; the default run 3,235 of 3,236 in 39 min 4 s, the one failure
  a load-sensitive `ShellDriverTests` case that passes alone. WP-S merged in `80fc877` (built,
  reviewed and fixed in its own worktree): the scenarios pinned by `Phase6ScenarioTests`, the
  folder server on port 47393, `FetchHold`, the per-session git environment and its tripwire,
  `FakeSystemTheme`, the fixed tool catalogues, below-normal priority for the merge-tool lookup, the
  project `VisualCommit.VisualTests.Phase6`, the timing script's default of 6, the hook samples
  left out of `TempRepo`'s blank git folders (200 copies 7.8 to 8.4 s with them, 3.8 to 4.5 s
  without); its default run, rested: 3,299 of 3,299 in 43 min 33 s (43 min 49 s for 3,236 before
  it). 17.3's measurement: `VisualCommit.App.Tests` alone 21 min 16 s, its 449 headless tests 8 min
  26 s (40 %), so phase 6's view tests go into it by subject, no project of their own. Left to the
  lead: `Phase6Checks.Support.cs`' dashboard and theme helpers, once WP-D, WP-W and WP-T are
  merged and before WP-V's parts W, S and L.)
- [x] 6. Phase 6's visual checks written into `docs/test-reports/phase-6.md`, each marked for the
  real-window pass or not, reviewed independently, and committed before the UI they test.
  (2026-10-22, before any of wave 2's UI: 34 checks in five parts, W1 to W9, K10 to K13, S14 to
  S21, G22 to G25 and L26 to L34, in 47 test methods saving 198 pictures, 26 real-window captures
  in 8 methods; each part drafted from the scenario facts, reviewed independently and revised, then
  assembled. The lead's rulings on its open points are at its end: part L goes whole into a
  project of its own, `VisualCommit.VisualTests.Phase6Look` (design 17.3's 40-method rule), with
  its own test server port 47394 (`AuthRemoteServer.Phase6LookVisualChecksPort`); an upstream is
  named by its tracking ref (GD15, WP-RA's `UpstreamNameTests`); the drop's push onto a renamed
  branch keeps GD24's title; a palette entry without a key shows no key column. The design took
  the report's values and those rulings in the same commit.)

Git layer and Core:

- [x] 7. Workspaces (repository groups): the model, where they are kept, add, remove, rename,
  and opening one. Tests. (WP4's part merged in `1d99be3`: `WorkspacePaths`, `WorkspaceEdits`,
  `RepositoryScanner`, `JsonWorkspaceStore`, `TabState.WorkspaceId`; the session keeps them in
  `workspaces.json`, `fef0569`. WP-W's commands, dialogs and the session's dashboards, built,
  reviewed and fixed, merged in `0da0eec`.)
- [x] 8. Each repository's status for the dashboard (branch, ahead and behind, uncommitted
  changes, an operation in progress), read cheaply for ten repositories without opening them as
  tabs. Tests. (WP4's `OpenedAtTopAsync` merged; WP-D's summary mode, the reads off the UI thread
  four at a time, built, reviewed and fixed, merged in `125e792`.)
- [x] 9. Bulk fetch and pull through each repository's `OperationQueue` (D81), with the
  toolbar's titles, Cancel and error dialog; what is recorded for undo and where the activity
  log shows it. Tests. (WP4's `OperationQueue.Cancel`, prompt groups and `MaxConnections` 16
  merged. WP-B, built, reviewed and fixed, merged in `e974c35`: the run, the window's four slots,
  the prompt group, Cancel all and a row's Cancel, failures kept in rows, the header and status bar,
  Pull's settings read before the fetch with the chosen rebase reading pull.ff alone (D231), the
  queue telling a dropped waiting operation as Finished (4.5), auto-fetch skipping a common folder a
  run fetches.)
- [x] 10. The settings that are git's own, read and written through git's configuration: the
  identity, the pull mode (`pull.rebase`, `pull.ff`, `branch.<name>.rebase`, D78, D84), the merge
  tool (`merge.tool`, D149). Tests, the pull-parity tests unchanged. (WP2, built, reviewed and
  fixed, merged in `5b8b7a8`: the two reads that keep conditional includes out of the global
  fields, the writes, read-back and locks, the pull modes, Pull reading its configuration before
  the fetch (D219) and the chosen rebase's `pull.ff` (D231), the merge-tool list kept in the data
  folder (D202); the parity tests pass unchanged.)
- [x] 11. Fetch refspecs read (design 10): the drop's push, `Delete <b> on <r>…` and checkout's
  local name follow the mapping, reasons R1 to R4, R2's checkout without an upstream (D201);
  `DropFacts.RemoteMapsBranchesDifferently` removed. Tests. (WP3's mapping merged in `3146d8d`: `FetchRefspecs`, the refs read's one `git config`
  call, `RemoteName` from the mapping, parity tests against git. WP-RA's app half, built,
  reviewed and fixed, merged in `0c91d01`: the menus, pushes, deletions, checkouts and force-push
  restore by the branch's name on its remote, R1 to R4, the push guard, D201's checkout without an
  upstream, an upstream named by git's own tracking ref, GD15 as built: design 10.4, 10.8 #8, #9.)
- [x] 12. Whether every checkout keeps ignored files (`--no-overwrite-ignore`, design 14.2 of
  phase 5, D179), as the owner decides (D196). Tests. (Decided: D193, D196, D221. WP1's git half merged
  in `31f525b`: the option on checkouts and merges, the app's check before every covered merge,
  rebase, pick, revert and stash apply, including the `--rebase-merges` branch points. WP-IA's app
  half, built, reviewed and fixed, merged in `cc3aed3`: the setting read when each operation runs,
  every checkout's refusal in D179's words, the app's check before every covered operation, Off
  restoring phase 5 with the automatic stash's and undo's guards kept. The pull's app path is
  WP-B's.)

App:

- [x] 13. Workspaces on screen and the dashboard: each repository's branch, ahead and behind and
  uncommitted changes; opening a repository from it. (WP-W merged in `0da0eec`, WP-D in `125e792`,
  each built, reviewed and fixed; the bulk run's parts of the dashboard are WP-B's.)
- [x] 14. Bulk fetch and pull from the dashboard: progress per repository, Cancel, errors. (WP-B,
  merged in `e974c35`.)
- [x] 15. The command palette: search and run the app's commands and the menus' actions, with
  their keys shown. (WP-K, built, reviewed and fixed, merged in `73ac284`.)
- [x] 16. Keyboard shortcuts for every main action, keeping what phase 5 bound (D163's undo and
  redo, Ctrl+F, `EscapeOrder`). (WP-K, merged in `73ac284`.)
- [x] 17. The settings screen: identity, pull mode, auto-fetch interval (`AutoFetchMinutes`,
  D85), `StashIncludeUntracked`, the extensions "Open file" asks about (D130), external tools
  ("Open in editor" beside "Open file"), the merge tool, and the checkout setting of step 12; each
  change takes effect. (WP-SET, built, reviewed and fixed, merged in `eae7edc`; its Keyboard
  shortcuts section fills with WP-K's list.)
- [x] 18. Theme polish: a sweep of every main screen in both themes (phase 5's new screens were
  pictured in the dark theme only), and the welcome page's long folder that wraps at awkward
  places (phase 1's follow-up). (WP-T, built, reviewed and fixed, merged in `e7144ec`: the theme
  follows the system while System is chosen, the light status colours and lanes 1, 2, 4 and 7 at
  4.5:1, `VcDangerTextBrush`, tooltips in the app's colours, the drag layer's redraw, `TextInk`;
  the lead's start-up line and the service's disposal in `AppSession`. WP-W's `PathTextBlock`
  merged in `0da0eec`. Part L's sweep pictures: WP-V's part L, step 20.)
- [x] 19. Phases 0 to 5's checks updated for what phase 6 changes on purpose, recorded under
  "Changes to expected results" in the phase 6 report. (Each package's lines as merged are listed in
  the report's "The earlier tests and expectations each work package changed, as merged"; the gate
  checks the pictures against 16.1's regions, 17.7.)

Visual checks:

- [x] 20. Scripted walk-through in `VisualCommit.VisualTests.Phase6`, screenshots under
  `artifacts/visual/phase-6/scripted/`. (WP-V's parts G `da4a956`, W `48df7af`, S `5d0a442`, K
  `e0f3c4a` and L `fb4aa59`, part L in `VisualCommit.VisualTests.Phase6Look`; each part written by its
  own writer with every picture inspected and every expected result that did not hold ruled on and
  recorded in the report; Phase6 130 of 130 and Phase6Look 19 of 19 on `fb4aa59`.)
- [x] 21. Real-window pass for the checks marked for it (`Phase6RealWindowPass`). (WP-R: 26 captures
  in 8 methods and 9 launches, written and built, never run before the gate.)

Closing:

- [x] 22. An independent review of the code before the gate; what it finds fixed, with tests.
  (Its findings fixed by four Opus fixers, each merged with its notes in the design and the report:
  C `15b00c8`, B `1dfdd6d`, A `d6a634d`, D `da54d8b`.)
- [x] 23. Visual test gate: `dotnet test` and the real-window pass on one commit, every picture
  inspected, report completed. (Passed on `b561b7c`, its fourth round: `dotnet test` 5,037 of 5,037
  in 56 min 25 s, the real-window pass 67 of 67 in 27 min 39 s, Q1 1,805 ms; the 1,329 files of the
  first round, on `774464d`, judged by thirteen Opus inspectors, and the 66 that changed since in
  full by two more; what it found, and the fixes, in the report's "Found by the gate".)
- [x] 24. CI run of the gate's commit read, every job. (Run 40059391404 on `b561b7c`: all five jobs
  green, one container on its second attempt.)
- [x] 25. `architecture.md` describes what is built. (`174ec4e`, with the review's and the gate's
  changes after it.)
- [x] 26. `plan.md`: anything not delivered moved, risks updated. (`290353b`, `e78d2e5`.)
- [x] 27. Handoff `docs/handoffs/phase-6.md`.
- [x] 28. Cold-read check by a fresh agent; gaps fixed. (22 gaps, all in the docs; fixed in `54a432c`
  but the real-window csproj's stale comment, for phase 7's first code commit.)
- [x] 29. `status.md`: links, CI, gate commit, waiting on the owner, state.
- [x] 30. Owner told what passed, what did not, and what needs a decision. (2026-10-24.)

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

The run for phase 6's gate commit `b561b7c` is
[40059391404](https://github.com/RahimPasha/VisualCommit/actions/runs/40059391404): all five jobs
succeeded, Windows with 5,037 tests and the others with 5,034 (three tests are Windows' own):
`windows-latest` (26 min 47 s; its Test step 24 min 37 s of 55, and its step "Build the real-window
pass" green), `macos-latest` (25 min 22 s; Test step 23 min 33 s), `ubuntu-latest` (5 min 50 s; Test
step 4 min 21 s) and the two "bare ubuntu container" jobs (7 min 16 s and 7 min 45 s; Test steps 4 min
56 s and 5 min 50 s). The container with the SDK from Ubuntu's packages passed on its second attempt:
its first failed `ShellDriverTests.Wheel_reaches_the_control_under_the_mouse_with_the_sign_of_a_real_wheel`
(a hit test before the window's first frame on the slow container; for phase 7), as phase 4's flaky
test did on `master`. The commits after it change docs only, which start no run. The gate's earlier
rounds had runs 40027897278 (`774464d`), 40044430603 (`b7da110`) and 40051904024 (`639db4a`), all five
jobs green; macOS's Test step reached 43 min 12 s in run 40019921605 on `a937ca6`, near D232's 44.
CI's Test step on Windows, macOS and Linux has a limit of 55 minutes and each of their jobs 65
(D232); the containers keep 14 and 25. The owner looks again at CI's duration when the project is
over (D134). The runs of the phase before the gate, and what each found, are in "Notes for whoever
resumes".

The run for phase 5's merge on `master` is
[39078809291](https://github.com/RahimPasha/VisualCommit/actions/runs/39078809291), for `7bcb5c0`
(the merge commit `c299939` and the commit that set phase 5 to "Done"): all five jobs succeeded,
each with 3,233 tests passed: `windows-latest` (16 min 51 s; its Test step 14 min 59 s of 40),
`macos-latest` (19 min 43 s; Test step 17 min 40 s), `ubuntu-latest` (5 min 19 s; Test step 3 min
50 s) and the two "bare ubuntu container" jobs (4 min 29 s and 5 min 30 s; Test steps 2 min 58 s
and 3 min 48 s). The Windows Test step's 15 minutes against the gate run's 25 for the same tests
is the runners' spread.

The last run on `phase/5-history` was
[38931127856](https://github.com/RahimPasha/VisualCommit/actions/runs/38931127856), for the
gate's commit `1b2e734`: all five jobs succeeded, each with 3,233 tests passed: `windows-latest`
(27 min 50 s; its Test step 25 min 15 s, and its step "Build the real-window pass" green),
`macos-latest` (17 min 43 s; Test step 16 min 13 s), `ubuntu-latest` (4 min 48 s; Test step 3 min
26 s) and the two "bare ubuntu container" jobs (5 min 38 s and 6 min 2 s; Test steps 3 min 56 s
and 4 min 14 s). The commits after it change only docs, which start no run. The gate's first run,
on `4281f4c`, had run 38909397699: all five jobs green with 3,227 tests. CI's Test step on
Windows, macOS and Linux has a limit of 40 minutes and each of their jobs 50 (D184, amending
D152); the lead raises the limit when the Windows or macOS Test step passes 32 minutes, and
records the new one as a decision (D186). The two container jobs keep 14 minutes for the Test
step and 25 per job. The owner looks again at CI's duration when the project is over (D134).

Earlier on `phase/5-history`:
[38831177542](https://github.com/RahimPasha/VisualCommit/actions/runs/38831177542), for `5bdd1de`
(the second wave, the lead's follow-ups and the 30-second default wait): all five jobs succeeded
with 3,046 tests each; the Windows Test step took 17 min 54 s of its then 24 and macOS's 14 min
18 s. The runs that failed after it on the way to the gate, and what each found, are in the phase 5
report's "Other platforms".
On the way, runs 38816004590 (`e11ea7d`: macOS path keys in WP7's tests, check 23's stash wait)
and 38820513395 (`40cb5f5`: check 23 waiting for real Undo states) failed, as fixed in `fe34b62`.
Before that,
[38772677755](https://github.com/RahimPasha/VisualCommit/actions/runs/38772677755) for `7e7d99d`
(the test speed-ups, whose `TempRepo` every test project uses): all five jobs succeeded with 2,765
tests each; the Windows Test step took 16 min 38 s of its 24 and macOS's 11 min 45 s. The run
before it, [38748240435](https://github.com/RahimPasha/VisualCommit/actions/runs/38748240435) for
`4b5716e` (the first wave merged: WP0, WP2a, WP1, WP-S, WP3's git half and D182), had all five
jobs green with 2,749 tests each (Windows Test step 16 min 17 s, macOS 14 min 14 s).
Since 2026-10-15 the jobs run different gits: Windows 2.56.0.windows.2, macOS 2.56.0 (Homebrew),
`ubuntu-latest` 2.55.0, the two containers 2.43.0. Two runs failed on the way and were fixed:
38723225901 (phase 3's check 15b on Git 2.56, `281dd69`) and 38738421993 (a phase 5 scenario test
that took Git 2.55.0's `--no-walk` regression for the rule, `499b90c`).

Phase 5's first run is
[38350713964](https://github.com/RahimPasha/VisualCommit/actions/runs/38350713964), for `279dd8c`
(phase 4's leftovers, D160): all five jobs succeeded with 2,421 tests each: `windows-latest` (16 min
7 s; its Test step 13 min 51 s), `macos-latest` (13 min 28 s; Test step 12 min 3 s),
`ubuntu-latest` (3 min 36 s) and the two "bare ubuntu container" jobs (4 min 14 s and 4 min 11 s).
GitHub announced that `ubuntu-latest` moves to Ubuntu 26 from 2026-10-19; the Linux job and the
containers may then see other package versions.

The last run on `phase/4-rebase-conflicts` was
[38320711773](https://github.com/RahimPasha/VisualCommit/actions/runs/38320711773), for the
gate's commit `6badab7`: all five jobs succeeded, each with 2,411 tests passed: `windows-latest`
(14 min 59 s; its Test step 13 min 9 s), `macos-latest` (15 min 14 s; Test step 13 min 57 s),
`ubuntu-latest` (3 min 6 s) and the two "bare ubuntu container" jobs (3 min 54 s and 4 min 5 s).
The commits after it changed only docs, which start no run. CI's Test step then had a limit of 24
minutes and each of the three platforms' jobs 32 (D152, amending D134). During phase 4 CI found
what the Windows machine could not (Git 2.55's own files and words, a background clean-up that
held the queue, checks that read the screen too early); the runs were red until each was fixed
(phase 4's report, "Other platforms", and its handoff, "Things that cost time").

The run for phase 4's merge on `master` is
[38336234442](https://github.com/RahimPasha/VisualCommit/actions/runs/38336234442), for `50cf35c`
(the merge commit `ce5f337` and the commit that set phase 4 to "Done"). Its first attempt had four
jobs succeed with 2,411 tests each (`macos-latest` 15 min 33 s, `ubuntu-latest` 3 min 40 s, the
two "bare ubuntu container" jobs 3 min 48 s and 3 min 7 s) and `windows-latest` fail: 2,410 of
2,411 passed, and the one failure was phase 1's
`RepositoryWatcherTests.A_burst_of_changes_raises_changed_once` (a burst of 20 file writes raised
the watcher's Changed twice, not once: on the loaded runner a pause between the writes outlasted
the 1-second quiet period). Re-running that job passed, 2,411 of 2,411 (15 min 40 s), so all five
jobs are green on attempt 2. Phase 4 did not change the watcher or that test; the test depends on
the wall clock, and the proper fix (the watcher's quiet period on a clock the test holds) is listed
for phase 5's first code commits in phase 4's handoff.

Before phase 4, the last run on `master` was
[38213858149](https://github.com/RahimPasha/VisualCommit/actions/runs/38213858149), for `d74c99e`
(phase 3's merge commit `8b33745` and the commit that set phase 3 to "Done"): all five jobs
succeeded with 1756 tests each, `ubuntu-latest` on its second attempt after a flake of phase 1's
check 13 that phase 4 removed (D150). Earlier: run
[38116890189](https://github.com/RahimPasha/VisualCommit/actions/runs/38116890189) for phase 2's
merge (557 tests) and run
[38077727040](https://github.com/RahimPasha/VisualCommit/actions/runs/38077727040) for phase 1's
merge (379 tests), all five jobs green.

The run numbers from before 2026-10-09 went with the deleted repository (D41), so those that
older commits and the phase 0 report and handoff mention no longer open; what they showed is as
written there and in this file's history.

Earlier in phase 0 the test step froze the whole job on macOS in 4 of 9 runs. The freezes
stopped with the commit that is now `0784112` (decision D37). If a macOS job ever sits in its
Test step for minutes again, read "Known issues" in the phase 0 handoff first.

### Next step

Phase 6 is awaiting the owner's acceptance ("Waiting on the owner"). On "Phase 6 accepted", follow
CLAUDE.md's row for it, with the gate commit `b561b7c`, and record the owner's answers to the
handoff's "At acceptance, to confirm or answer" as decisions (D239 on). Then wait for "Start phase
7"; the handoff's "For the next phase" says how to begin it.

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

- Phase 6's first CI run, [39094444954](https://github.com/RahimPasha/VisualCommit/actions/runs/39094444954)
  on `43f52c4` (the leftovers of step 4): four jobs succeeded with 3,233 tests (`windows-latest`
  27 min 2 s, its Test step 24 min 38 s; `ubuntu-latest`; both containers); `macos-latest` failed
  1 of 3,233: phase 2's `Check_10_stage_one_hunk` waited 30 s for the staged diff of
  `src/Calculator.cs` after a click on its staged row, 1.5 minutes into the run. The commit
  touched nothing that check uses; the helper's comment says macOS CI saw the same once before
  (run 38122509278). The cause was the app's: every status read replaced every row of both stage
  lists, so a read between a click's press and its release (on macOS the watcher's late refresh
  after a write) lost the click. Fixed in `43751d0` (the lists keep the rows a read did not change,
  `StageListRefreshTests`). Its run,
  [39098031906](https://github.com/RahimPasha/VisualCommit/actions/runs/39098031906): all five
  jobs green with 3,235 tests (Windows' Test step 19 min 9 s, macOS's 22 min 50 s, Ubuntu's 2 min
  59 s, the containers' about 4 min 10 s).
- CI since `855ef39`: green on `125e792`, `8c7401b`, `cc3aed3`, `ba63f48`, `eae7edc` and `9ed9ba3`.
  Run 39936260636 on `48df7af` (WP-K) failed `PaletteEntriesTests` on macOS (git's `/private/var`
  spelling against `/var`) and its growth test on Windows (collections inflating the measure; now
  `PaletteGrowthTests`, alone in a no-GC region); run 39943973579 on `5d0a442`, cancelled by a newer
  push, had macOS's Test step past 32 minutes (D232: 55 and 65). Run 39950853606 on `fb4aa59` failed
  L28 on Linux and both containers (FreeType's thin `=>` ink, skipped by the ink test) and K's
  expectation of blame on Windows and macOS (Git 2.56's `--abbrev` width; `blame -l`). Run
  39958314029 on `f9db5b0` failed `PaletteEntriesTests` and K10 on macOS (paths compared as git spells
  them; `c6e1cb2`). Runs 39962856421 on `c6e1cb2` and 39980785501 on `15b00c8` failed only
  `KeyRouterTests.Every_key_of_the_set_runs_its_command_once_and_is_consumed` on Windows, both times
  with the selections 5, 5, 4, 4 (fixer D). Run
  [39997105336](https://github.com/RahimPasha/VisualCommit/actions/runs/39997105336) on `1dfdd6d`:
  four jobs green (Windows' Test step 24 min 0 s, Ubuntu's 5 min 0 s, the containers' 5 min 41 s and
  5 min 34 s); macOS failed S19 alone (its Test step 28 min 52 s): "The screen's reads are 13 calls,
  not 7", a watcher refresh after Save's `git config --local` write (fixer D's item).
  Run 40004153661 on `57d5c7b` (fixer A merged): both containers green; Windows, macOS and
  `ubuntu-latest` failed fixer A's new push-back test, whose `git remote add team/fork` beside `team`
  Git 2.55 and 2.56 refuse (`is a subset of existing remote`), and macOS S19 again; the test writes the
  remote as configuration since `6fe269c`, and S19 waits for the watchers since `da54d8b`.
  Run 40011779192 on `9cb2ec9` (all four fixers): `ubuntu-latest` and both containers green; Windows
  failed W3 alone (its Test step 32 min 0 s): the runner's temporary folder brings design-system's
  folder to the Folder cell's 632 px and the scenario's random name tips it either way, while the
  check asserted the cell whole on every Windows machine (WD29 asks it only where it fits); W3 now
  asserts the cell cut exactly when the folder is wider (`a937ca6`). macOS failed phase 2's
  `Check_11_stage_one_line_then_unstage_it` once with Avalonia's `Could not create glyphTypeface.`
  `Font family: $Default` while the diff's editor took its document (its Test step 21 min 33 s);
  nothing phase 6 changed is on that path, and no earlier run showed it: watch it.
- The gate's first default run, on `1b7b316` (the code of `da54d8b`), with two agents writing docs
  beside it: every project passed but `VisualCommit.Git.Tests`, which the hang watchdog stopped 658 s
  in: `ProtectedChangesTests.A_refused_checkout_of_each_form_throws_gits_refusal_and_changes_nothing`,
  five refused checkouts on five scenarios in one test (12 s alone), had run 180 s under the load,
  while tests beside it took 80 to 103 s; it is one case per form since `a937ca6`. The other eight
  projects passed 3,033 of 3,033 in 49 min 22 s (App.Tests the last). The second run, on `a937ca6`,
  was stopped too (below).
- The gate's second default run, on `a937ca6`: the hang watchdog stopped `VisualCommit.Git.Tests` 1,481 s
  in on the reflog scenario's template (18 s alone with its test, past 180 s under the load), and
  phase 3's check 23 waited 30 s for a pop of 24 git calls; every other test passed. D237 (`774464d`):
  shared preparations held to 10 minutes of their own, the driver's default wait and phases 2's and
  3's write waits 60 s. CI run 40019921605 on `a937ca6` and 40027897278 on `774464d`: all five jobs
  green (on `774464d` Windows' Test step 35 min 0 s, macOS's 24 min 39 s; on `a937ca6` macOS's had
  taken 43 min 12 s, near D232's 44).
- **The gate's passes, on `774464d`**, after a 10-minute rest each: `dotnet test` 5,034 of 5,034 in
  55 min 14 s (the projects ended after: phases 0 to 2's walk-through 11 min 44 s, Phase6Look 8 min
  28 s, phase 4's 19 min 16 s, phase 5's 24 min 15 s, Phase6 28 min 45 s, Phase5Undo 37 min 23 s,
  phase 3's 38 min 0 s, Git.Tests 45 min 59 s, App.Tests 54 min 4 s); the real-window pass 67 of 67
  in 25 min 29 s (26 min 8 s with its build), at its first attempt, Q1 1,264 ms (467 frames, 95th
  percentile 0.9 ms, longest 7.4 ms), 200 % scaling, work area 1500×952. The picture list
  (`docs/test-reports/phase-6-pictures.sha256`, 1,329 files) was written right after; the comparison
  with 17.7's references (`D:/Projects/Personal/gate6-inspection/compare/`): 1,080 earlier files
  changed (630 in the toolbar alone), 249 new. Thirteen Opus inspectors judge every picture
  (verdicts in `D:/Projects/Personal/gate6-inspection/verdicts/`).
- The inspection of the passes on `774464d`: all 1,329 files judged by thirteen Opus inspectors, and
  48 of CI's pictures by a fourteenth (`other-platforms.md`); the lead's rulings in
  `D:/Projects/Personal/gate6-inspection/findings-log.md`. Three app defects, fixed with tests: after
  a refused Save the wrong box's text drawn selected (s16b, s18a; `d8fd9fb`); a disabled ticked box
  drawn like an enabled one (w1e, l30c; `d8fd9fb`); dashboard rows drawn hovered after tabs opened
  from the dashboard (w8d, w8f, w8g, w8h: Avalonia 12.1.3 recycles a list's container with its
  `IsPointerOver`; `59b2316`). The rest were the lists of changed earlier pictures and a few texts of
  the report (merged in `d0635ec`), and rendering accepted by nature. The passes run again on
  `59b2316`; the first run's list is kept as
  `D:/Projects/Personal/gate6-inspection/run1-774464d-pictures.sha256`.
- The gate's second round, on `59b2316` (code; `bcbdb3f`): `dotnet test` 5,037 of 5,037 in 44 min
  34 s; the real-window pass 66 of 67 in 27 min 15 s: S14 found Windows' Open dialog by its title
  before its Open button existed, so the harness waits for a dialog's controls (`639db4a`). CI run
  40044430603 on `b7da110` (the code of `59b2316`): all five jobs green. The third round, on
  `639db4a`: `dotnet test` 5,037 of 5,037 in 53 min 13 s; the real-window pass 66 of 67 in 28 min
  5 s, S14 again, 30 s waiting for the Open button: the dialog opens again in the system folder,
  where the first pass chose notepad.exe, and a search for the button walks its whole file list
  first; Enter in the file name box confirms the path now (`b561b7c`; S14 alone passed in 28 s).
  CI run 40051904024 on `639db4a`: all five jobs green. The second round's pictures are kept in
  `D:/Projects/Personal/gate6-inspection/run2-artifacts/` (74 changed against the first round's).
- 17.7's references, made on 2026-10-23: phases 0 to 5's walk-throughs on `master` (`acdac12`) in a
  worktree of their own (`dotnet test`, 2,295 of 2,295 in 58 min 16 s), copied with the 306 real-window
  files to `D:/Projects/Personal/gate6-reference/` (1,080 pictures, `scripted-pictures.sha256`); 752 of
  the 774 scripted pictures match `phase-5-pictures.sha256`, and the 22 that differ were inspected in
  full (`reference-verdicts.md` there): 21 vary by nature (a temporary folder's name, a caret, a
  progress bar, the number of reads, a few anti-aliased pixels, most proven by re-encoding to the
  gate's hash), and `u23f` differs by the machine's change, matching U23 step 19 in full. Phase 1's
  `14a`'s first `fatal:` line breaks by the temporary name's width. Note for the gate's inspectors:
  pictures with the focus in a text box (`f10b` to `f10f` too) can catch the caret either way.
- The default run on `eae7edc` (WP-B and WP-SET merged), with two agents building beside it: 4,689
  of 4,689 in 68 min 59 s. CI run
  [39893475197](https://github.com/RahimPasha/VisualCommit/actions/runs/39893475197) on `eae7edc`: all
  five jobs green; Windows' Test step 29 min 0 s (D186's threshold is 32), macOS's 20 min 41 s,
  Ubuntu's 4 min 44 s, the containers' 4 min 42 s and 4 min 8 s. Phase 6's walk-throughs are still to
  come, so the Windows Test step is expected to pass 32 minutes: raise the limit then (D186).
- The default run on `125e792` (WP-D merged), with two builders beside it: 4,340 of 4,341 in 60 min
  48 s; the one failure, `ShellDriverTests`' fresh-application case, gave its own work 60 s to start
  on the headless UI thread, which the dashboard's tests' line exceeded; it now waits as a turn off
  the watchdog (`8c7401b`). CI: run 39852863982 on `125e792` and
  [39861559850](https://github.com/RahimPasha/VisualCommit/actions/runs/39861559850) on `8c7401b`,
  all five jobs green both times (on `8c7401b` Windows' Test step 16 min 18 s, macOS's 16 min 14 s,
  Ubuntu's 4 min 26 s, the containers' about 3 min 43 s).
- CI after the WP-W merge: run 39831538867 on `378260b` failed WP-W's `WorkspaceFolderProbeTests` on
  Windows and macOS (Git 2.56 words an orphaned worktree's `.git` file "gitfile does not point to a
  valid repository", which `OpenAsync` now takes as no repository; checked locally with MinGit
  2.56.0.windows.2, kept in the session's scratch folder) and a `WorkspaceCommandsTests` case in one
  container (the scan's dialog showed before its search started), fixed in `431fb42`; run 39837374552
  on it failed phase 4's `ConflictResolverViewTests` double-click case on macOS (the wait for a layout
  lay between the two presses, which Avalonia counts on the real clock), fixed in `855ef39`; run
  [39843589646](https://github.com/RahimPasha/VisualCommit/actions/runs/39843589646) on `855ef39`:
  all five jobs green; Windows' Test step 25 min 22 s, macOS's 22 min 4 s, Ubuntu's 4 min 9 s, the
  containers' about 4 min 5 s.
- WP-W edited two real-window files that section 18 gives WP-R, as 16.1's welcome-page row asks:
  `Phase0RealWindowPass.cs:124` (`AssertShell`'s list gains `Workspaces` and `No workspaces`) and
  `Phase5RealWindowPass.A.cs:559-561` (`AssertEmptyTab` leaves out `New workspace…` and checks the
  button's name `New workspace`); the lead accepted them, so WP-R starts from them. WP-W also added
  `tests/VisualCommit.App.Tests/WorkspaceTestSupport.cs` (name it in the handoff).
- `b6d1d01` (the diff view's growth test counting the UI thread's work): CI run
  [39820477316](https://github.com/RahimPasha/VisualCommit/actions/runs/39820477316), all five jobs
  green; Windows' Test step 25 min 46 s, macOS's 20 min 40 s, Ubuntu's 3 min 59 s, the containers'
  3 min 14 s and 4 min 25 s.
- The report's commit `c46eea2`: CI run
  [39808018633](https://github.com/RahimPasha/VisualCommit/actions/runs/39808018633), all five jobs
  green; Windows' Test step 18 min 32 s, macOS's 16 min 2 s, Ubuntu's 3 min 47 s, the containers' 4 min
  21 s and 2 min 57 s. The 306 real-window reference pictures of 17.7 were copied on 2026-10-22 to
  `D:/Projects/Personal/gate6-reference/` (33 MB) and match `phase-5-pictures.sha256`.
- After WP3's and WP4's merges: CI run 39207577529 on `80689ef` failed 16 of about 4,040 on
  Windows and macOS (Git 2.56), all of them the cases of WP3's
  `FetchRefspecsParityTests.Gits_fetch_fills_exactly_the_tracking_refs_DestinationsOf_gives`, whose
  setup pushed to the server by its path and relied on that push writing no tracking refs, which
  2.56 does; the setup now deletes them (`6c7f37c`). Run
  [39211028657](https://github.com/RahimPasha/VisualCommit/actions/runs/39211028657) on `6c7f37c`:
  all five jobs green; Windows' Test step 26 min 36 s, macOS's 20 min 25 s, Ubuntu's 3 min 56 s,
  the containers' about 3 min 30 s.
- After wave 1's merges (WP-S, WP1, WP2): CI run 39190992859 on `249a554` failed 1 of 3,749 on
  Windows only, WP-S's `GitRunnerPriorityTests.A_tree_that_started_before_its_priority_was_set_is_lowered_whole`,
  which assumed git's tree starts at normal priority while CI's runner runs the test host below
  normal (Windows then gives the children its class); the test sets the tree to normal first
  (`e2fb327`; run from `cmd`'s `start /belownormal` the old test fails as on CI and the new one
  passes). Run [39194263924](https://github.com/RahimPasha/VisualCommit/actions/runs/39194263924)
  on `e2fb327`: all five jobs green; Windows' Test step 25 min 52 s, macOS's 20 min 44 s, Ubuntu's
  3 min 16 s, the containers' about 4 min 10 s (D186's threshold is 32 minutes).
- After WP0 (`8e79628`, full run 3,235 of 3,236 in 38 min 28 s): `ShellDriverTests.A_fresh_applications_wait_for_its_turn_on_the_ui_thread_does_not_count_against_the_test`
  failed once under the full run's load (the shared headless UI thread did not take its work within
  60 s) and passes alone; watch it. CI run 39153491762 on `8e79628`: Windows green; the other four
  failed WP-L's isolation test, whose alias was named `shell`, a built-in git command where git-shell
  is installed (fixed in `717bd8a`); macOS also failed `DiffTextViewTests.Building_and_drawing_a_diff_grows_in_step_with_its_lines`
  once (369 ms against its 250), a timing test that WP0 did not touch: watch it. CI run
  39158437982 on `717bd8a`: its first attempt failed that test on macOS again (705 ms against 7 ms)
  and phase 5's `Check_U23_terminal_commit` on Windows (a working-changes row a terminal commit's
  half-done state left for one refresh; the check now waits for the clean tree it asserts,
  `161e298`); its second attempt passed all five jobs with 3,236 tests. The timing test failed 2
  of 3 macOS attempts: if it fails again, count its work instead of timing it. It failed again on
  2026-10-22 under wave 2's side-by-side builds (689 ms against 27 ms, a ratio of 25.5); it now compares
  the UI thread's own work (`ThreadWork`: its cycles on Windows, its processor time elsewhere), 5.7 to
  7.3 times as much for ten times the lines under the same load.
- Phase 5's `u23f-ignored-in-the-way` differs from `phase-5-pictures.sha256` (`f238e0d…` at the gate,
  `0ce3ae4…` now) on `master` (`acdac12`) too, with the gate's code: the machine changed since
  2026-10-16, not phase 6. The gate's references, made on `master`, take it as it is now.
- Phase 6 started on 2026-10-18 from a clean `master` (`acdac12`): no worktree and no `wip/*` or
  `worktree-*` branch was left. Ten git fsmonitor daemons from phase 5's worktrees were still
  running and were stopped (phase 4's handoff gives the commands). Free space at the start: 15.7 GB
  on C:, 20 GB on D: (plan risk 27: a built worktree takes about 5 GB). The phase 5 gate's
  reference pictures and inspection verdicts lie outside the repository in
  `D:/Projects/Personal/gate5-reference/` and `D:/Projects/Personal/gate5-inspection/`; phase 6's
  gate makes its own references on `master` (phase 5's handoff, "Running the visual gate").
- **CI's gits differ from the development machine's.** Since 2026-10-15 the jobs run Windows
  2.56.0.windows.2 (2.55.0 before), macOS 2.56.0 (Homebrew), `ubuntu-latest` 2.55.0 and the two
  containers 2.43.0. On 2.56 a `git stash pop` refused by local changes exits 128, not 1: phase 3's
  check 15b accepts both (`281dd69`), as phase 5's report records under "Changes to expected
  results" (phase 3's report says "exit 1" in 15b's step 2 and in T5). Only 2.36 and 2.55 are on
  the development machine; CI is the only check on 2.43 and 2.56 (plan risk 26), unless a session
  downloads MinGit 2.56 for a check by hand (phase 6's handoff, "Things that cost time").
- **Git 2.55 (CI's version) is on the development machine**, inside Visual Studio:
  `C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TeamFoundation/Team Explorer/Git/mingw64/bin/git.exe`.
  Put that `mingw64/bin` first on `PATH` (otherwise its child processes fall back to the 2.36 on
  `PATH`). Phase 5's research used it to check what 2.55 writes differently, which phase 4 could only
  learn from CI runs. With it first on `PATH`, 6 of the 49 tests of `VisualCommit.App.Tests`'
  fetch, pull, push and remotes classes, those against the credential test server, fail locally
  and pass on CI: that trimmed git has no `git http-backend`, which the server runs (explained in
  phase 6; plan risk 28). It also lacks `sleep`, so phase 6's fetch hold does not hold with it.
- Phases 1 to 6 were built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 3, 4, 5 and 6 handoffs' "Things that cost time" say what to watch
  for: a worktree starts from `master` and its agent must create its branch from the phase
  branch first; merged worktrees must be removed at once (a built worktree took about 6.4 GB in
  phase 6, plan risk 27); a global `core.fsmonitor=true` leaves a git daemon per worktree running
  (phase 4's handoff says how to list and stop them before a gate).
- The default run takes about 55 minutes on the development laptop (56 min 25 s at phase 6's gate,
  rested, 5,037 tests in nine projects; 43 min 22 s at phase 5's, 3,233 tests in seven). On CI at
  phase 6's gate (run 40059391404) the Windows job took 26 min 47 s (its Test step 24 min 37 s of
  55), macOS's 25 min 22 s (Test step 23 min 33 s; 43 min 12 s in run 40019921605 before the gate),
  `ubuntu-latest`'s 5 min 50 s (Test step 4 min 21 s) and the containers' 7 min 45 s and 7 min 16 s.
  The real-window pass takes about 28 minutes (27 min 39 s for phases 0 to 6 at phase 6's gate),
  after a rest (a hot laptop fails Q1). See phase 6's handoff, "Running the visual gate", for how
  to run both and what to stop first.
- The next phase can be started on the Windows machine or in a cloud session; see
  [cloud-sessions.md](cloud-sessions.md). No cloud session has worked on the repo yet, and that
  page lists what the first one should check.
- On the Windows development machine only: Windows 11 on an Intel Core i7-8650U laptop, .NET SDK
  10.0.303, Git 2.36.0.windows.1, `gh` signed in as RahimPasha. Its screen is 3000×2000 at 200%
  scaling: 1500×1000 logical pixels, of which 1500×952 is the work area. A 1920×1080 window does
  not fit, so the real-window pass can only use sizes up to 1400×900 there. The laptop is slower
  when hot: let it rest a few minutes before the gate's `dotnet test` and again before the
  real-window pass, which times Q1 (limit 2,000 ms; 937 ms on phase 4's gate commit, rested; at
  phase 5's gate 1,749 ms and 1,826 ms in its two passes, 1,147 ms alone after a rest, and
  2,613 ms on the laptop hot from the 43-minute default run, which failed that attempt; up to
  2,268 ms on a warm machine during phase 3; at phase 6's gate, each pass after a 10-minute rest,
  1,264 ms in round 1's pass and 1,805 ms in the counted one; plan risk 10).
- Packages: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and
  TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI.UIA3 5.0.0. Phases 3, 4, 5 and 6 added no packages.
- Git 2.55 on CI writes its own files and messages differently from 2.36 here; phase 4's
  handoff, "Rules worth keeping", lists what it found and how the checks compare git's files,
  phase 5's adds the exit codes and side effects that differ between versions, and phase 6's
  "Things that cost time" what 2.55 and 2.56 did to its tests.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- **Phase 6's acceptance**: the gate passed on `b561b7c` (2026-10-24); the phase is "Awaiting
  acceptance". Say "Phase 6 accepted" to merge it into `master`. At acceptance, to confirm or answer
  (the handoff's "Waiting on the owner"): design 17.5's reading of "ten repos can be checked and
  updated from one screen" (one dashboard of the ten, one Fetch all and one Pull all deciding every
  row); the report's by-hand System step (switch Windows' app mode while the app runs with System
  chosen, D224); and the move of the text boxes' placeholder contrast (Fluent's watermark opacity,
  about 2.5:1 dark and 2.1:1 light) to phase 8's accessibility pass, which the lead made at the
  gate's inspection rather than change nearly every picture.
- **Decisions made during phase 6 without the owner**, which the owner may want to look at: the
  design's D203 to D230 (after its reviews); D231 (the rebase chosen for one pull reads `pull.ff`
  alone); D232 (CI's limits, 55 and 65 minutes); D233 (git calls leave out the repository-selecting
  variables); D234 (the session's end cancels every dialog; the window waits for the settings
  screen's global writes); D235 (Amend refuses when HEAD moved); D236 (the app's own files flushed
  before their rename); D237 (the default run's waits fit nine projects side by side); D238 (part
  L's own test server port); and the lead's rulings recorded in the design and the report.
- **For phase 7** (plan risks 4 and 5): a test account or token for each of GitHub, Azure DevOps and
  GitLab, and whether public sign-in needs app registrations or personal access tokens are enough.
- **Phase 5's acceptance**: given on 2026-10-17; merged into `master` (`c299939`). The decisions
  made during the phase without the owner, which the owner may want to look at: D182 (tooltips take
  no input), D183 (the report's rulings), D184 and D186 (CI's time limits, 40 and 50 minutes, raised
  past 32), D185 (the wait for git's merge tool not counted by the hang watchdog).
- Answered during phase 5's design on 2026-10-12 (D164 to D166), after its reviews: the details
  panel gains an "All files" switch so that any line of any file is two clicks from its commit;
  an Abort is undone from the backups list, not by Undo; undoing the automatic stash's checkout
  switches back and pops that stash.
- Answered at the start of phase 5 on 2026-10-12 (D153 to D163): C12 is built in phase 5, its
  refusal keeps the "Stash changes and check out" button, the automatic stash is taken always
  when there are changes and only for a commit the current branch reaches, and it is kept until
  popped or dropped; undo reaches many steps back within a session; undoing a change to a remote
  asks, then pushes back with a lease; the activity log is a panel under the graph; search marks
  its matches in the graph; file history and blame open in place of the graph; hidden and soloed
  branches are remembered across restarts; the cosmetic issues phase 4 left are fixed first;
  every stash's line to its commit is dashed; two selected commits are compared at once in the
  right panel; Ctrl+Z and Ctrl+Y undo and redo git actions outside text boxes.
- Answered for phase 4 on 2026-10-12, before its design: git's
  own list of commits for several picks (D136, replacing a sentence of D88); automatic cleanup of
  the backups after 30 days or when more than a set number exist (D137); then, asked at the start
  of phase 4: 50 backups are kept besides the 30 days, the clean-up says nothing on screen (the
  log records it), the conflict resolver shows ours and theirs side by side above the editable
  result across the graph and right-panel area, and the interactive rebase's list replaces the
  graph with the newest commit on top.
- The virus-scanner exclusion on the development machine (the repository folder and
  `C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests`) was set up by the owner on
  2026-10-12. Whether it is in effect cannot be read without administrator rights; phase 5's gate
  ran the default tests in 43 min 22 s with seven test projects (phase 4's: 21 min 52 s with
  five; phase 3's: 19 min with four).
- Settled on 2026-10-12: the working-changes row's dashed line crossing another branch's node
  (06f) is left for now; case-only branch renames on Windows and macOS stay refused (D131);
  auto-fetch gets no ssh timeout, only Cancel by hand (D125).
- When you have a Mac at hand: the seven-step checklist at the end of the
  [phase 6 test report](test-reports/phase-6.md), the thirteen-step one at the end of the
  [phase 5 test report](test-reports/phase-5.md), the eight-step one at the end of the
  [phase 4 test report](test-reports/phase-4.md), the eleven-step one at the end of the
  [phase 3 test report](test-reports/phase-3.md), the seven-step one at the end of the
  [phase 2 test report](test-reports/phase-2.md), the nine-step one at the end of the
  [phase 1 test report](test-reports/phase-1.md) and the eight-step one at the end of the
  [phase 0 test report](test-reports/phase-0.md). The owner will do them at the end of the
  project, when a Mac is at hand (2026-10-11, again on 2026-10-12 with phase 4's acceptance).
- When the project is over: whether a CI run of 30 minutes or more is reasonable or needs another
  solution (D134).

Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors", and
the repository was made public, which lifts the limit on CI minutes.

Settled on 2026-10-09 (D41): the owner deleted the GitHub repository and a session created it
again, empty and public, and pushed `master` and `phase/0-foundation` unchanged. The commits from
before the rewrite of the history (D40), which GitHub had kept serving by their IDs, now answer
"not found"; every commit on GitHub carries the owner's personal address. The session's
permission system refuses to delete a repository, so a deletion is always the owner's to do.
