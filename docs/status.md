# Status

Last updated: 2026-10-18

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
| 6. Multi-repo and productivity | In progress | `phase/6-multi-repo` | | |
| 7. Hosting | Not started | `phase/7-hosting` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 6, multi-repo and productivity: **In progress** since 2026-10-18, on `phase/6-multi-repo`,
built on the Windows machine from `master` at `acdac12` (phase 5's merge `c299939` and the status
commits after it). Phase 5 was accepted by the owner on 2026-10-17 and merged into `master` (merge
commit `c299939`); its gate ran on `1b2e734`. Phase 4 was accepted on 2026-10-12 (merge commit
`ce5f337`), phase 3 on 2026-10-11 (merge commit `8b33745`), phase 2 on 2026-10-10 (merge commit
`e039070`), phase 1 on 2026-10-10 (merge commit `b5eb1ed`) and phase 0 on 2026-10-06 (merge commit
`b5f4723`).

Gate commit: none yet. When a phase is set to "Awaiting acceptance", this line names the commit
that both passes of the gate ran on; "Phase N accepted" checks that nothing but docs changed
after it.

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
- [ ] 3. Design in `docs/phase-6-design.md`: every git fact derived with Git 2.36 (and 2.55 where
  words or exit codes may differ), the texts, the layouts, the work packages, an estimate of what
  the phase adds to the default run (45 minutes) and to the real-window pass (25). Independent
  reviews, each finding checked by an adversarial verifier; the design revised from every
  confirmed one; questions of taste the reviews raise go to the owner; the design's own decisions
  recorded.
- [x] 4. Leftovers named in phase 5's handoff: the real-window csproj's stale comment on the pass's
  length; `scripts/time-scripted-check.sh` for phase 6; plan risk 28 (seven fetch and pull tests
  that fail locally with Visual Studio's Git 2.55) understood before fetch and pull change for the
  dashboard. (Risk 28 explained on 2026-10-18: Visual Studio's Git has no `git http-backend`, which
  the test server `AuthRemoteServer` runs; 6 of 49 fetch, pull, push and remotes tests failed with
  it, all against that server; the seventh, the FakeSsh test, fails because that git crashes running
  sh under an inherited `MSYSTEM=MINGW64` (the research, not run here); plan risk 28. The server
  now fails at its start with a message naming the missing `http-backend`; the csproj comment says
  25 minutes for phases 0 to 5; the timing script sends phase 6 and later to their own project.
  Removing `MSYSTEM` in the tests' isolation goes with the design's isolation changes.)
- [ ] 5. Harness: phase 6's scenario repositories (a workspace of ten repositories in mixed
  states: clean, ahead, behind, diverged, uncommitted changes, a conflict in progress, detached,
  no upstream, no remote, a folder that is gone; a fetch refspec that maps names differently),
  pinned by tests; the project `VisualCommit.VisualTests.Phase6` in the solution; the timing
  script and CLAUDE.md's Commands; the real-window project's link to `Phase6Expectations*.cs`; a
  fixed port of its own for a test git server, if the bulk-fetch checks use one (amending D97 as
  D150 did); the driver helpers the checks need.
- [ ] 6. Phase 6's visual checks written into `docs/test-reports/phase-6.md`, each marked for the
  real-window pass or not, reviewed independently, and committed before the UI they test.

Git layer and Core:

- [ ] 7. Workspaces (repository groups): the model, where they are kept, add, remove, rename,
  and opening one. Tests.
- [ ] 8. Each repository's status for the dashboard (branch, ahead and behind, uncommitted
  changes, an operation in progress), read cheaply for ten repositories without opening them as
  tabs. Tests.
- [ ] 9. Bulk fetch and pull through each repository's `OperationQueue` (D81), with the
  toolbar's titles, Cancel and error dialog; what is recorded for undo and where the activity
  log shows it. Tests.
- [ ] 10. The settings that are git's own, read and written through git's configuration: the
  identity, the pull mode (`pull.rebase`, `pull.ff`, `branch.<name>.rebase`, D78, D84), the merge
  tool (`merge.tool`, D149). Tests, the pull-parity tests unchanged.
- [ ] 11. `remote.<r>.fetch` read: `DropFacts.RemoteMapsBranchesDifferently` set, and the push of
  a drop onto a remote branch and "Delete <b> on <r>…" act on the branch's name on the remote.
  Tests.
- [ ] 12. Whether every checkout keeps ignored files (`--no-overwrite-ignore`, design 14.2 of
  phase 5, D179), as the owner decides. Tests.

App:

- [ ] 13. Workspaces on screen and the dashboard: each repository's branch, ahead and behind and
  uncommitted changes; opening a repository from it.
- [ ] 14. Bulk fetch and pull from the dashboard: progress per repository, Cancel, errors.
- [ ] 15. The command palette: search and run the app's commands and the menus' actions, with
  their keys shown.
- [ ] 16. Keyboard shortcuts for every main action, keeping what phase 5 bound (D163's undo and
  redo, Ctrl+F, `EscapeOrder`).
- [ ] 17. The settings screen: identity, pull mode, auto-fetch interval (`AutoFetchMinutes`,
  D85), `StashIncludeUntracked`, the extensions "Open file" asks about (D130), external tools
  ("Open in editor" beside "Open file"), the merge tool, and the checkout setting of step 12; each
  change takes effect.
- [ ] 18. Theme polish: a sweep of every main screen in both themes (phase 5's new screens were
  pictured in the dark theme only), and the welcome page's long folder that wraps at awkward
  places (phase 1's follow-up).
- [ ] 19. Phases 0 to 5's checks updated for what phase 6 changes on purpose, recorded under
  "Changes to expected results" in the phase 6 report.

Visual checks:

- [ ] 20. Scripted walk-through in `VisualCommit.VisualTests.Phase6`, screenshots under
  `artifacts/visual/phase-6/scripted/`.
- [ ] 21. Real-window pass for the checks marked for it (`Phase6RealWindowPass`).

Closing:

- [ ] 22. An independent review of the code before the gate; what it finds fixed, with tests.
- [ ] 23. Visual test gate: `dotnet test` and the real-window pass on one commit, every picture
  inspected, report completed.
- [ ] 24. CI run of the gate's commit read, every job.
- [ ] 25. `architecture.md` describes what is built.
- [ ] 26. `plan.md`: anything not delivered moved, risks updated.
- [ ] 27. Handoff `docs/handoffs/phase-6.md`.
- [ ] 28. Cold-read check by a fresh agent; gaps fixed.
- [ ] 29. `status.md`: links, CI, gate commit, waiting on the owner, state.
- [ ] 30. Owner told what passed, what did not, and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

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

Step 3: the design's first version is committed (`docs/phase-6-design.md`, `9d6d2e3`); its six
independent reviews (git safety, code fit, what the user sees, tests and gate, performance and
threads, completeness), each finding checked by an adversarial verifier, run now. Then: revise the
design from every confirmed finding, put the four questions of taste (the design's M1 to M4, and
any the reviews raise) to the owner, record the design's decisions (from D195) in `decisions.md`.

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
  the development machine; CI is the only check on 2.43 and 2.56 (plan risk 26).
- **Git 2.55 (CI's version) is on the development machine**, inside Visual Studio:
  `C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TeamFoundation/Team Explorer/Git/mingw64/bin/git.exe`.
  Put that `mingw64/bin` first on `PATH` (otherwise its child processes fall back to the 2.36 on
  `PATH`). Phase 5's research used it to check what 2.55 writes differently, which phase 4 could only
  learn from CI runs. With it first on `PATH`, 7 fetch and pull tests of `VisualCommit.App.Tests`
  fail locally and pass on CI, for reasons not yet known (plan risk 28).
- Phases 1 to 5 were built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 3, 4 and 5 handoffs' "Things that cost time" say what to watch
  for: a worktree starts from `master` and its agent must create its branch from the phase
  branch first; merged worktrees must be removed at once (a built worktree takes about 5 GB);
  a global `core.fsmonitor=true` leaves a git daemon per worktree running (phase 4's handoff says
  how to list and stop them before a gate).
- The default run takes about 45 minutes on the development laptop (43 min 22 s at phase 5's gate,
  3,233 tests in seven projects). On CI at phase 5's gate (run 38931127856) the Windows job took
  27 min 50 s (its Test step 25 min 15 s of 40), macOS's 17 min 43 s (Test step 16 min 13 s),
  `ubuntu-latest`'s 4 min 48 s (Test step 3 min 26 s) and the containers' 5 min 38 s and 6 min 2 s.
  The real-window pass takes about 25 minutes (24 min 57 s at phase 5's gate), after a rest (a hot
  laptop fails Q1). See phase 5's handoff, "Running the visual gate", for how to run both and what
  to stop first.
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
  2,268 ms on a warm machine during phase 3; plan risk 10).
- Packages: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and
  TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI.UIA3 5.0.0. Phases 3, 4 and 5 added no packages.
- Git 2.55 on CI writes its own files and messages differently from 2.36 here; phase 4's
  handoff, "Rules worth keeping", lists what it found and how the checks compare git's files, and
  phase 5's adds the exit codes and side effects that differ between versions.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

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
- When you have a Mac at hand: the thirteen-step checklist at the end of the
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
