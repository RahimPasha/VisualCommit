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
| 4. Rebase and conflict resolution | In progress | `phase/4-rebase-conflicts` | | |
| 5. History, traceability and undo | Not started | `phase/5-history` | | |
| 6. Multi-repo and productivity | Not started | `phase/6-multi-repo` | | |
| 7. Hosting | Not started | `phase/7-hosting` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 4, rebase and conflict resolution, is in progress on `phase/4-rebase-conflicts`, started
on 2026-10-12 on the Windows machine from `master` at `3731b31`. Phase 3 was accepted on
2026-10-11 and merged into `master` (merge commit `8b33745`); its gate ran on `594f9fb`.

Gate commit: none yet. When a phase is set to "Awaiting acceptance", this line names the commit
that both passes of the gate ran on; "Phase N accepted" checks that nothing but docs changed
after it.

### Progress

Each item is ticked in the commit that completes it. The numbers are plain step numbers, not
requirement numbers.

Start:

- [x] 1. Phase branch created, phase set to "In progress", this checklist.
- [x] 2. Baseline: the default run on `master` (`3731b31`) passes: 1756 of 1756 in 23 min 21 s
  (VisualTests 11 min 27 s, Git.Tests 14 min 26 s, App.Tests 19 min 55 s, VisualTests.Phase3
  23 min 19 s). Not a clean comparison with phase 3's 19 min 15 s: six research agents ran git
  experiments beside it and the laptop had not rested. Whether the owner's virus-scanner
  exclusion is in effect cannot be read without administrator rights. A clean timing (rested,
  nothing else running) is taken before the gate.
- [x] 3. Phase 3's leftovers removed: the 29 `worktree-*` branches (all merged, deleted with
  `-d`) and seven git file-system monitor daemons of deleted worktrees.
- [x] 4. Open points decided and recorded: the owner's answers of 2026-10-12 (50 backups kept
  besides the 30 days, the clean-up says nothing on screen, the resolver with ours and theirs
  above the result across the graph and right-panel area, the interactive rebase's list in the
  graph area with the newest commit on top); the design in `docs/phase-4-design.md`, its git
  facts derived with Git 2.36, reviewed independently; decisions from D139 on. Done: the design
  (`74dda42`) after four reviews (git safety, UI and harnesses, coverage, code fit), D139 to D151.
- [x] 5. Phase 1's `Check_13_clone_with_progress` made deterministic (a clone held at a
  percentage instead of a race), and the real-window project's comment on its duration corrected
  (phase 3 handoff, "For the next phase", steps 3 and 5). Done in WP-C13 (merged in `a6788d8`,
  D150): a bare copy of the 100k-commit repository served by `AuthRemoteServer` on 127.0.0.1:47392,
  held at 45 % (`Counting objects 45%`); checks 13 and 14 changed, recorded in phase 4's report.
- [x] 6. WP0: the contracts of design section 14 (types, `IGitRepository` members with stubs per
  package, the queue's `CanBeCancelled`, the app's interfaces and their test fakes, the menu
  context's facts, `EscapeOrder`, `CancelText`, the icons and tokens, the editor variables) and
  the spike of design 11.3 (an editable AvaloniaEdit result and the generalised `DiffPane`, headless,
  phase 2's pictures unchanged). Done: the spike merged in `a7ce01f` (phase 2's 66 pictures
  byte-identical), the contracts in `6090f8f` (the full default run passed, 1809 tests, 17 min 34 s
  beside another build); design 14.1 says what they settled.
- [x] 7. Harness: phase 4's scenario repos (a plain rebase, an interactive rebase, conflicts in a
  merge, in a rebase with two conflicting commits and in a cherry-pick, the conflict kinds the
  resolver shows), pinned by tests; the project `VisualCommit.VisualTests.Phase4` in the
  solution, CI and `scripts/time-scripted-check.sh`; the driver helpers the checks need. Done in
  WP-S (merged in `d64e2b7`); the drivers of the list and the resolver come with WP5 and WP6.
- [x] 8. Phase 4's visual checks written into `docs/test-reports/phase-4.md`, each marked for
  the real-window pass, reviewed independently, and committed before the UI they test. Done: 18
  checks (A1 to A5, B6 to B9, C10 to C17, D18), 141 steps, 122 planned pictures of which 28 in the
  real window; every git state derived with Git 2.36 on replicas of the scenarios; each part
  reviewed adversarially and revised, then the whole cross-checked; the lead's decisions in design
  14.3.

Git layer:

- [x] 9. Rebase a branch onto a commit or branch (the current branch or another), with a backup
  of the old tip first and no confirmation (D135); the rebase's stopped state read fully; Continue,
  Skip and Abort for a rebase. Tests.
- [x] 10. Interactive rebase with the app's own sequence editor: reorder, squash, fixup, reword,
  drop; squash of several selected commits. Tests.
- [x] 11. Several commits picked and reverted with one git command (D136); Continue and Skip for
  a merge, a cherry-pick and a revert, one or a list, also one started in a terminal; the
  in-progress state read so that the reftable ref format works too (D128). Tests.
- [x] 12. Pull with rebase chosen for one pull, whatever git's configuration says; Continue and
  Skip after a pull's rebase. Tests, with `PullParityTests`.
- [x] 13. Conflicts: per file the base, ours and theirs, the kind of conflict and its hunks;
  taking a whole file from one side; writing a result and marking it resolved; the external merge
  tool. Tests for every conflict kind.
- [x] 14. Backups cleaned up automatically (D137): after 30 days or beyond 50, the oldest first,
  never one an operation in progress needs, when a repository is opened and after every new
  backup; logged, nothing on screen. Tests.

  Steps 9 to 14 are done in the Git layer: WP1 (`bd18b84`), WP2 (`0a4fdc6`), WP3 (`81769a6`); design
  14.2 says what they settled. The app's halves (saving the rebase's backup, scheduling the
  clean-up, the operations that call these members) are WP4's, in step 15.

App:

- [ ] 15. Menus: "Rebase onto" in the commit and branch menus, rebase and interactive rebase in
  the drop menu, squash on a multi-selection, pull with rebase, interactive rebase from a commit.
  Built in WP4 (merged in `9242ed8`); its review's findings are being fixed.
- [ ] 16. The banner's Continue, Skip and Abort for every kind; the stage panel during a stop
  (its message, and its button for a rebase or a list of picks). Built in WP4; being fixed as 15.
- [x] 17. The interactive rebase list in the graph area, newest on top: an action per commit,
  drag to reorder, the new message of a reword or squash, Start and Cancel. WP5 (`2e9b722`) and
  the lead's hooks (`d7b3c20`); design 14.4.
- [x] 18. The conflict resolver across the graph and right-panel area: ours and theirs above the
  editable result, ours, theirs or both per conflict, a whole file from one side, mark resolved,
  the next and previous conflicted file, the external merge tool. WP6 (`60cf77a`) and the lead's
  hooks (`f184e95`, the clean-up at opening `51fb51f`); design 14.4.
- [ ] 19. Phases 0 to 3's checks updated for anything phase 4 changes on purpose, recorded under
  "Changes to expected results" in the phase 4 report. Phase 3's walk-through and real-window
  files updated in WP4 (81 of 81 and phases 0 to 2's 50 of 50 pass on `51fb51f`); what WP4's fixes
  change is still to be recorded.

Visual checks:

- [ ] 20. Scripted walk-through: one test per check in `VisualCommit.VisualTests.Phase4`,
  screenshots under `artifacts/visual/phase-4/scripted/`.
- [ ] 21. Real-window pass for the checks marked for it (`Phase4RealWindowPass`).

Closing:

- [ ] 22. An independent review of the code before the gate; what it finds fixed, with tests.
- [ ] 23. Visual test gate: `dotnet test` and the real-window pass on one commit, every picture
  inspected, report completed.
- [ ] 24. CI run of the gate's commit read, every job.
- [ ] 25. `architecture.md` describes what is built.
- [ ] 26. `plan.md`: anything not delivered moved, risks updated.
- [ ] 27. Handoff `docs/handoffs/phase-4.md`.
- [ ] 28. Cold-read check by a fresh agent; gaps fixed.
- [ ] 29. `status.md`: links, CI, gate commit, waiting on the owner, state.
- [ ] 30. Owner told what passed, what did not, and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

The last run on `phase/3-operations` was
[38206493320](https://github.com/RahimPasha/VisualCommit/actions/runs/38206493320), for the
gate's commit `594f9fb`: all five jobs succeeded, each with 1756 tests passed: `windows-latest`
(10 min 56 s), `macos-latest` (8 min 35 s), `ubuntu-latest` (3 min 11 s) and the two "bare
ubuntu container" jobs (4 min 1 s and 3 min 25 s). The commits after it changed only docs, which
start no run. CI's Test step has a limit of 14 minutes now (the default run took 3.5 minutes
before phase 3); the owner said to let it run as long as it needs and to look again at CI's
duration when the project is over (D134).

The run for the merge on `master` is
[38213858149](https://github.com/RahimPasha/VisualCommit/actions/runs/38213858149), for `d74c99e`
(the merge commit `8b33745` and the commit that set phase 3 to "Done"). Its first attempt had four
jobs succeed with 1756 tests each (`windows-latest` 11 min 28 s, `macos-latest` 8 min 22 s, the
two "bare ubuntu container" jobs 3 min 50 s and 3 min 0 s) and `ubuntu-latest` fail: 1755 of
1756 passed, and the one failure was phase 1's `Check_13_clone_with_progress` ("Timed out after
90 s waiting for a screenshot of the clone showing a percentage between 1 and 99"). Re-running
that job passed, 1756 of 1756, so all five jobs are green on attempt 2. The failure is a
timing flake that phase 3 did not cause: the check has to catch a local clone of the 100k-commit
repository part-way through, and a fast runner can finish the clone between two screenshots
(phase 1's checks 12 and 18 flaked the same way during phase 3). It passed in the gate's own run
of `ubuntu-latest` (38206493320). The proper fix is to hold the clone at a percentage, as phase 3's
held fetch does, instead of racing it; the owner is asked whether that is phase 4's first
commit (see "Waiting on the owner").

CI found faults on the way that this machine could not (git 2.55 prints dates as `Z`, macOS
physical paths, a time limit, timing in some tests): the runs before the gate's last commit were
red until each was fixed; they are listed in the phase 3 handoff.

Before phase 3, the last run on `master` was
[38116890189](https://github.com/RahimPasha/VisualCommit/actions/runs/38116890189), for
`7aa3349` (the merge commit `e039070` and the commit that set phase 2 to "Done"): all five jobs
succeeded, each with 557 tests passed. Earlier: run
[38077727040](https://github.com/RahimPasha/VisualCommit/actions/runs/38077727040) for phase 1's
merge, 379 tests in all five jobs.

The run numbers from before 2026-10-09 went with the deleted repository (D41), so those that
older commits and the phase 0 report and handoff mention no longer open; what they showed is as
written there and in this file's history.

Earlier in phase 0 the test step froze the whole job on macOS in 4 of 9 runs. The freezes
stopped with the commit that is now `0784112` (decision D37). If a macOS job ever sits in its
Test step for minutes again, read "Known issues" in the phase 0 handoff first.

### Next step

WP4 is fixing its review's findings and the lead's decisions on them (squash refusal texts, a
list's refused stop after Continue or Skip, 10.2's list statuses, the banner's Continue with the
stage panel's changed message, `rebase.autoStash` read, a running text that changes mid-operation,
a warning outcome for the fork point, the empty pick's dialog text, the merge tool through the
resolver's question, `RepositoryOperationsBackupCleanupTests`) on `wip/p4-wp4`. Beside it, step 20
is written by four agents in worktrees (WP7: part A, part B, C10 to C13, C14 to C17 with D18), each
recording where the app differs from the report. Then: merge them, triage what they found (fix the
app, or change an expected result with its reason in the report), record WP4's changes to earlier
expectations (step 19), WP8 (the real-window pass's code), the review (step 22) and the gate.

This always names the single next thing to do and is kept accurate in every commit.

### Notes for whoever resumes

- Phase 4 started on 2026-10-12. The research for its design (code maps and git experiments
  with Git 2.36) was done by sub-agents into the session's scratch folder, which a new session
  does not have; what the design relies on goes into `docs/phase-4-design.md` section 0.
- Phases 1 to 3 were built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 3 handoff's "Things that cost time" says what to watch for: a worktree
  starts from `master` and its agent must create its branch from the phase branch first; merged
  worktrees must be removed at once (each build folder is about 580 MB per project); a global
  `core.fsmonitor=true` leaves a git daemon per worktree running (stop them before the gate).
- The default run takes about 19 minutes (up to 26 under load) on the development laptop and
  about 11 minutes on CI; the real-window pass 11 minutes. See the handoff's "Running the visual
  gate" for how to run both and what to stop first.
- The next phase can be started on the Windows machine or in a cloud session; see
  [cloud-sessions.md](cloud-sessions.md). No cloud session has worked on the repo yet, and that
  page lists what the first one should check.
- On the Windows development machine only: Windows 11 on an Intel Core i7-8650U laptop, .NET SDK
  10.0.303, Git 2.36.0.windows.1, `gh` signed in as RahimPasha. Its screen is 3000×2000 at 200%
  scaling: 1500×1000 logical pixels, of which 1500×952 is the work area. A 1920×1080 window does
  not fit, so the real-window pass can only use sizes up to 1400×900 there. The laptop is slower
  when hot: let it rest a few minutes before the gate's `dotnet test` and again before the
  real-window pass, which times Q1 (limit 2000 ms; 1189 ms on the gate commit, up to 2268 ms on a
  warm machine with the phase 2 build too).
- Packages: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and
  TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI 5.0.0. Phase 3 added no packages.
- CI's Test step was raised to 24 minutes and the OS jobs to 32 (D152, amending D134): the
  Windows test step took 12 min 8 s on `8a2e2c9`. Git 2.55 on CI differs from 2.36 here in two
  ways found so far (design 0.1): the `# Conflicts:` block in `rebase-merge/message`, and the
  autostash's wording. Phase 3's `Check_04` timed out once on `ubuntu-latest` waiting for a
  diff (run 38258811117); `8a2e2c9` makes a timeout save a picture and the app's log, and its run
  passed.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- Nothing waits on the owner for phase 4 now. Answered on 2026-10-12, before its design: git's
  own list of commits for several picks (D136, replacing a sentence of D88); automatic cleanup of
  the backups after 30 days or when more than a set number exist (D137); then, asked at the start
  of phase 4: 50 backups are kept besides the 30 days, the clean-up says nothing on screen (the
  log records it), the conflict resolver shows ours and theirs side by side above the editable
  result across the graph and right-panel area, and the interactive rebase's list replaces the
  graph with the newest commit on top.
- The virus-scanner exclusion on the development machine (the repository folder and
  `C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests`) was set up by the owner on
  2026-10-12. Phase 4's step 2 times the default run with it (phase 3's gate: 19 minutes).
- Settled on 2026-10-12: the working-changes row's dashed line crossing another branch's node
  (06f) is left for now; case-only branch renames on Windows and macOS stay refused (D131);
  auto-fetch gets no ssh timeout, only Cancel by hand (D125).
- Recorded for a later phase, not phase 4 (C12, D138): protect uncommitted changes as GitKraken
  does, with automatic stashes drawn in the graph. The owner says which phase when phases 5 and
  later are planned; the proposal is phase 5.
- When you have a Mac at hand: the eleven-step checklist at the end of the
  [phase 3 test report](test-reports/phase-3.md), the seven-step one at the end of the
  [phase 2 test report](test-reports/phase-2.md), the nine-step one at the end of the
  [phase 1 test report](test-reports/phase-1.md) and the eight-step one at the end of the
  [phase 0 test report](test-reports/phase-0.md). The owner will do them at the end, when a Mac
  is at hand (2026-10-11).
- Phase 1's `Check_13_clone_with_progress` (see "CI") is made deterministic in phase 4's step 5,
  as the phase 3 handoff recommended; no answer came, so "Start phase 4" does it.
- When the project is over: whether a CI run of 30 minutes or more is reasonable or needs another
  solution (D134).

Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors", and
the repository was made public, which lifts the limit on CI minutes.

Settled on 2026-10-09 (D41): the owner deleted the GitHub repository and a session created it
again, empty and public, and pushed `master` and `phase/0-foundation` unchanged. The commits from
before the rewrite of the history (D40), which GitHub had kept serving by their IDs, now answer
"not found"; every commit on GitHub carries the owner's personal address. The session's
permission system refuses to delete a repository, so a deletion is always the owner's to do.
