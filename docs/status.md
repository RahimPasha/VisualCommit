# Status

Last updated: 2026-10-12

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
| 5. History, traceability and undo | In progress | `phase/5-history` | | [phase-5](test-reports/phase-5.md) (to be written) |
| 6. Multi-repo and productivity | Not started | `phase/6-multi-repo` | | |
| 7. Hosting | Not started | `phase/7-hosting` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 5, History, traceability and undo, is in progress on `phase/5-history`, started on
2026-10-12 from `master` at `0883669` on the Windows machine. At its start the owner settled C12
and the matter-of-taste points (D153 to D163): C12 is built in this phase; undo reaches many steps
back within a session; undoing a change to a remote asks, then pushes back with a lease; the
activity log is a panel under the graph; search marks its matches in the graph; file history and
blame open in place of the graph; hidden and soloed branches are remembered across restarts; the
cosmetic issues phase 4 left are fixed first; every stash's line to its commit is dashed; two
selected commits are compared at once; Ctrl+Z and Ctrl+Y undo and redo git actions outside text
boxes.

Phase 4 was accepted by the owner on 2026-10-12 and merged into `master` (merge commit
`ce5f337`); its gate ran on `6badab7`. Phase 3 was accepted on 2026-10-11 (merge commit
`8b33745`), phase 2 on 2026-10-10 (merge commit `e039070`), phase 1 on 2026-10-10 (merge commit
`b5eb1ed`) and phase 0 on 2026-10-06 (merge commit `b5f4723`).

Gate commit: none yet. When a phase is set to "Awaiting acceptance", this line names the commit
that both passes of the gate ran on; "Phase N accepted" checks that nothing but docs changed
after it.

### Progress

Each item is ticked in the commit that completes it. The numbers are plain step numbers, not
requirement numbers. The design (step 3) may split the Git-layer and App steps further; it then
says how its work packages map onto them.

Start:

- [x] 1. Phase branch created, phase set to "In progress", this checklist; the owner's answers
  recorded (D153 to D163) and carried into `plan.md` and `requirements.md`.
- [x] 2. Baseline: the default run on `master` (`0883669`) as inherited: 2,409 of 2,411 passed in
  35 min 27 s, while six code-reading agents ran beside it (phase 4's gate: 21 min 52 s). The two
  failures were waits of 10 s that ran out under that load, and both passed alone (8 s and 15 s):
  phase 2's `Check_19_discard_a_hunk_and_a_line` (the diff after a discard, which also saves a
  snapshot and runs the backups' clean-up, as check 18 did before `a7e9db3`) and
  `RepositoryOperationsMergeTests.In_the_window_merge_abort_reset_create_delete_and_an_empty_pick_show_as_the_checks_say`
  (the abort, which saves two backups and runs the clean-up). Step 4 gives such waits the longer
  timeout phase 4 gave check 18.
- [x] 3. Design in `docs/phase-5-design.md`: every git fact derived with Git 2.36 (and 2.55 where
  words may differ), the texts, the layouts, the work packages. Five independent reviews (git
  safety, code fit, UX, tests and gate, completeness) found 97 issues, each checked by an
  adversarial verifier; the design was revised from every confirmed one. Three questions the
  reviews raised went to the owner (D164 to D166); the design's own decisions are D167 to D181.
  Its work packages map onto the steps below: WP-S step 5; WP1 steps 7 to 10; WP2a and WP2b the
  graph's part of steps 12, 17 and 21 (the filter, the search marks, the dashed stash lines); WP3
  steps 11 and 19; WP4 steps 13 and 21; WP5 step 14 and the toolbar's Search; WP6 steps 15 and 16
  and "All files" (D164); WP7 steps 17, 18 and 20; WP8 steps 22 and 23; WP9 step 24. WP0 (the
  contracts and stubs every package builds against) comes first.
- [x] 4. Phase 4's leftovers (D160), merged in `279dd8c`: the watcher's quiet period and maximum
  delay on a `TimeProvider` (`ManualTimeProvider` in `VisualCommit.Testing`; both burst tests on
  it); the details panel's SHA drawn just small enough to stay on one line when it would wrap (only
  then: other pictures unchanged); the resolver's disabled arrows in `VcTextDisabledBrush`; Cancel's
  tooltip "Stop git and the merge tool" while the tool runs; three stale comments; the waits after a
  write that saves a backup at 30 s (phase 2's checks 13, 17, 19, and the App merge window test).
  Its worktree run: 2,421 tests passed in 26 min 56 s. Pictures changed: `a3d`, `b8c`, `c11j` (the
  SHA), and the resolver pictures with one conflicted file (`c10e`–`c10j`, `c11d`, `c11e`, `c11h`,
  `c11i`, `c12d`–`c12f`, `c14a`–`c14h`, `c15d`, `c16b`, `d18c`, `d18d`: the file arrows dimmer).
  Found to vary from run to run on master too (for the gate's list of pictures that vary by
  nature): phase 1 `13b`, `14b` and phase 3 `30g` (a temporary folder's name), phase 3 `13j`,
  `27d`, `32b`, and 1 to 6 anti-aliased pixels in phase 3 `06e`, `10f`, `13l`, `22d`, `26e`, `28d`,
  `32d` and phase 4 `a1a`.
- [ ] 5. Harness: phase 5's scenario repos (a file renamed across its history, blame with lines
  from several commits, a reflog with commits no ref reaches, branches to solo and hide, commits
  to search and compare, changes that block a checkout), pinned by tests; the projects
  `VisualCommit.VisualTests.Phase5` and `VisualCommit.VisualTests.Phase5Undo` in the solution
  (D181), `scripts/time-scripted-check.sh` and CLAUDE.md's Commands; the real-window project's
  link to `Phase5Expectations*.cs`; the record and backup clocks both harnesses fix; the driver
  helpers the checks need.
- [ ] 6. Phase 5's visual checks written into `docs/test-reports/phase-5.md`, each marked for the
  real-window pass or not, reviewed independently, and committed before the UI they test.

Git layer:

- [ ] 7. File history following renames, a file's diff at each of its commits, and blame. Tests.
- [ ] 8. The reflog of HEAD and of each branch, and restoring from it. Tests.
- [ ] 9. Commit search by message, author, SHA and file, and inside changes. Tests.
- [ ] 10. Compare two commits or branches: the files changed between them and a file's diff.
  Tests.
- [ ] 11. Undo and redo (D154, D155): each app action recorded with what it changed and how it
  is reversed, from the backups and what was recorded; a step refused when the repository has
  changed since; a remote put back with a lease after a question. Tests for each destructive
  action of phases 2 to 4.
- [ ] 12. Solo and hide: the history of the chosen refs only. Tests.
- [ ] 13. C12 (D153): the refusal before a checkout that would overwrite uncommitted changes, and
  the automatic stash before checking out a commit the current branch reaches. Tests.

App:

- [ ] 14. The activity log panel under the graph, from the status bar (D156): operations, their
  git calls with output, exit code and time, warnings, the backups' clean-up lines.
- [ ] 15. File history and blame in place of the graph (D158), from the file menus, with
  click-through to commits.
- [ ] 16. The reflog view with restore.
- [ ] 17. Search over the graph (D157): the toolbar's Search and Ctrl+F, matches marked, the rest
  dimmed, a count, next and previous; search inside changes.
- [ ] 18. Compare: two selected commits, and a branch with another; the files in the right panel
  and their diffs.
- [ ] 19. The toolbar's Undo and Redo, naming the action they would undo or redo.
- [ ] 20. Solo and hide in the branch menus and the left panel, kept in the session (D159).
- [ ] 21. C12 on screen: the refusal's message and button, the automatic stash in the graph and
  the left panel.
- [ ] 22. Phases 0 to 4's checks updated for what phase 5 changes on purpose (new menu items, the
  toolbar's Undo, Redo and Search, the status bar's toggle), recorded under "Changes to expected
  results" in the phase 5 report.

Visual checks:

- [ ] 23. Scripted walk-through: the checks of parts A, B, F and P in
  `VisualCommit.VisualTests.Phase5`, part U in `VisualCommit.VisualTests.Phase5Undo` (one action
  per test method), screenshots under `artifacts/visual/phase-5/scripted/`.
- [ ] 24. Real-window pass for the checks marked for it (`Phase5RealWindowPass`).

Closing:

- [ ] 25. An independent review of the code before the gate; what it finds fixed, with tests.
- [ ] 26. Visual test gate: `dotnet test` and the real-window pass on one commit, every picture
  inspected, report completed.
- [ ] 27. CI run of the gate's commit read, every job.
- [ ] 28. `architecture.md` describes what is built.
- [ ] 29. `plan.md`: anything not delivered moved, risks updated.
- [ ] 30. Handoff `docs/handoffs/phase-5.md`.
- [ ] 31. Cold-read check by a fresh agent; gaps fixed.
- [ ] 32. `status.md`: links, CI, gate commit, waiting on the owner, state.
- [ ] 33. Owner told what passed, what did not, and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

The last run on `phase/4-rebase-conflicts` was
[38320711773](https://github.com/RahimPasha/VisualCommit/actions/runs/38320711773), for the
gate's commit `6badab7`: all five jobs succeeded, each with 2,411 tests passed: `windows-latest`
(14 min 59 s; its Test step 13 min 9 s), `macos-latest` (15 min 14 s; Test step 13 min 57 s),
`ubuntu-latest` (3 min 6 s) and the two "bare ubuntu container" jobs (3 min 54 s and 4 min 5 s).
The commits after it changed only docs, which start no run. CI's Test step has a limit of 24
minutes and each of the three platforms' jobs 32 (D152, amending D134); the owner looks again at
CI's duration when the project is over (D134). During phase 4 CI found what the Windows machine
could not (Git 2.55's own files and words, a background clean-up that held the queue, checks that
read the screen too early); the runs were red until each was fixed (phase 4's report, "Other
platforms", and its handoff, "Things that cost time").

Phase 5's first run is
[38350713964](https://github.com/RahimPasha/VisualCommit/actions/runs/38350713964), for `279dd8c`
(phase 4's leftovers, D160): all five jobs succeeded with 2,421 tests each: `windows-latest` (16 min
7 s; its Test step 13 min 51 s), `macos-latest` (13 min 28 s; Test step 12 min 3 s),
`ubuntu-latest` (3 min 36 s) and the two "bare ubuntu container" jobs (4 min 14 s and 4 min 11 s).
GitHub announced that `ubuntu-latest` moves to Ubuntu 26 from 2026-10-19; the Linux job and the
containers may then see other package versions.

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

WP0 (design 16) is merged (`e5e25ee`): the contracts, records, stubs and every operation's undo
call points, built, reviewed three ways (19 findings, all fixed) and run in full in its worktree
(2,470 tests passed in 25 min 13 s; the walk-through pictures unchanged but for those step 4 names).
The first wave runs in worktrees under `.claude/worktrees/p5-*`, each built, reviewed and fixed by
its own agents: WP-S (`wip/p5-s`, step 5), WP1 (`wip/p5-wp1`), WP2a (`wip/p5-wp2a`), WP3's git half
(`wip/p5-wp3g`) and the flaky test below (`wip/p5-flaky`). Then: merge each with `--no-ff`, remove
its worktree, run the full default tests once on the merged branch, push, read CI. Then step 6 (the
report's checks, from design 15.4, on the scenarios WP-S pinned), reviewed and committed before
the second wave builds any UI.

### Notes for whoever resumes

- **A flaky test, older than phase 5**: `StatusBarTests.While_the_merge_tool_runs_Cancel_says_it_stops_git_and_the_merge_tool`
  (added with the leftovers in `279dd8c`) hangs until the 3-minute watchdog kills App.Tests in
  about half the runs under load, also on `c319578` before WP0: the click on Cancel does not reach
  the button. Being fixed on `wip/p5-flaky`. If a full run dies with `HANG WATCHDOG` naming it,
  run it again; it is not a failure of what was changed.
- If a session resumes while `.claude/worktrees/p5-*` exist: each holds a package's unmerged
  work on its `wip/p5-*` branch (local only, not pushed). Merge the finished ones with `--no-ff`
  after building them; ask the owner before discarding any.
- Phase 5 started on 2026-10-12 on the Windows machine. No worktree, `wip/*` or `worktree-*`
  branch existed at its start; two git file-system monitor daemons were running.
- **Git 2.55 (CI's version) is on the development machine**, inside Visual Studio:
  `C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TeamFoundation/Team Explorer/Git/mingw64/bin/git.exe`.
  Put that `mingw64/bin` first on `PATH` (otherwise its child processes fall back to the 2.36 on
  `PATH`). Phase 5's research used it to check what 2.55 writes differently, which phase 4 could only
  learn from CI runs.
- Phases 1 to 4 were built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 3 and phase 4 handoffs' "Things that cost time" say what to watch
  for: a worktree starts from `master` and its agent must create its branch from the phase
  branch first; merged worktrees must be removed at once (each build folder is about 580 MB per
  project); a global `core.fsmonitor=true` leaves a git daemon per worktree running (phase 4's
  handoff says how to list and stop them before a gate).
- The default run takes about 22 minutes (up to 27 under load) on the development laptop and
  about 15 minutes per job on CI's Windows and macOS; the real-window pass 14 minutes. See phase
  4's handoff, "Running the visual gate", for how to run both and what to stop first.
- The next phase can be started on the Windows machine or in a cloud session; see
  [cloud-sessions.md](cloud-sessions.md). No cloud session has worked on the repo yet, and that
  page lists what the first one should check.
- On the Windows development machine only: Windows 11 on an Intel Core i7-8650U laptop, .NET SDK
  10.0.303, Git 2.36.0.windows.1, `gh` signed in as RahimPasha. Its screen is 3000×2000 at 200%
  scaling: 1500×1000 logical pixels, of which 1500×952 is the work area. A 1920×1080 window does
  not fit, so the real-window pass can only use sizes up to 1400×900 there. The laptop is slower
  when hot: let it rest a few minutes before the gate's `dotnet test` and again before the
  real-window pass, which times Q1 (limit 2000 ms; 937 ms on phase 4's gate commit, rested; up
  to 2268 ms on a warm machine during phase 3).
- Packages: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and
  TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI.UIA3 5.0.0. Phases 3 and 4 added no packages.
- Git 2.55 on CI writes its own files and messages differently from 2.36 here; phase 4's
  handoff, "Rules worth keeping", lists what it found and how the checks compare git's files.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

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
  2026-10-12. Whether it is in effect cannot be read without administrator rights; phase 4's gate
  ran the default tests in 21 min 52 s with five test projects (phase 3's: 19 min with four).
- Settled on 2026-10-12: the working-changes row's dashed line crossing another branch's node
  (06f) is left for now; case-only branch renames on Windows and macOS stay refused (D131);
  auto-fetch gets no ssh timeout, only Cancel by hand (D125).
- When you have a Mac at hand: the eight-step checklist at the end of the
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
