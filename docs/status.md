# Status

Last updated: 2026-10-11

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git, a public repository. The local
folder on the development machine is still named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Done | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
| 1. Repos and commit graph | Done | `phase/1-graph` | [phase-1](handoffs/phase-1.md) | [phase-1](test-reports/phase-1.md) |
| 2. Diff and commit workflow | Done | `phase/2-diff-commit` | [phase-2](handoffs/phase-2.md) | [phase-2](test-reports/phase-2.md) |
| 3. Branch, remote, stash and tag operations | Awaiting acceptance | `phase/3-operations` | [phase-3](handoffs/phase-3.md) | [phase-3](test-reports/phase-3.md) |
| 4. Rebase and conflict resolution | Not started | `phase/4-rebase-conflicts` | | |
| 5. History, traceability and undo | Not started | `phase/5-history` | | |
| 6. Multi-repo and productivity | Not started | `phase/6-multi-repo` | | |
| 7. Hosting | Not started | `phase/7-hosting` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 3, Branch, remote, stash and tag operations, passed its visual gate and awaits the owner's
acceptance on `phase/3-operations` (not merged into `master` yet). It was started on 2026-10-10 on
the Windows machine, from `master` at `d3814bd`, and built with sub-agents in git worktrees (the
design is in `docs/phase-3-design.md`, kept as history; what is built is in `architecture.md`).
Phase 2 was accepted on 2026-10-10 and merged into `master` (merge commit `e039070`); phase 1 on
2026-10-10 (merge commit `b5eb1ed`) and phase 0 on 2026-10-06 (merge commit `b5f4723`).

Gate commit: `594f9fb` ("Phase 1 check 18 waits for the new commit's label; outside-change
waits are 30 seconds"). Both passes of the gate ran on it on the Windows machine: `dotnet test`
(1756 tests, all passed, 19 min 15 s) and the real-window pass (32 tests, all passed, 11 min 11 s;
Q1 1189 ms), every picture inspected. The commits after it change docs only; "Phase 3 accepted"
checks that nothing but docs changed after the gate commit.

### Progress

Each item is ticked in the commit that completes it. The numbers are plain step numbers, not
requirement numbers.

Start:

- [x] 1. Phase branch created, phase set to "In progress", this checklist.
- [x] 2. Open points from the phase 2 handoff decided and recorded in `decisions.md` (D77 to
  D133; the design behind them is `docs/phase-3-design.md`). The owner answered four questions
  of taste on 2026-10-10 (later phases' actions are left out of the menus; pull follows git's own
  configuration; auto-fetch is on, every 5 minutes; a checkout blocked by local changes offers to
  stash, check out and re-apply).
- [x] 3. Harness (WP-S2, WP5, WP10-core): what the two passes need to open a context menu, drag a
  branch, wait for a tooltip and read an operation's progress and the log.
- [x] 4. Scenario repos for the operations (WP-S1), pinned in `ScenarioTests`.
- [x] 5. Phase 3's visual checks written into `docs/test-reports/phase-3.md`, each marked for
  the real-window pass, reviewed independently, and committed before the UI they test: written
  and committed in `82b622b` before the UI (WP5 onward); the independent review (six reviewers,
  56 findings) corrected in `57134d2`.
- [x] 6. The flaky `DiffHighlighterTests` docstring test fixed at its cause: `d72e500`.

Git layer:

- [x] 7. Operation queue per repository, and backup refs before destructive operations (WP0, WP1).
- [x] 8. Branches: checkout, create, rename, delete, set upstream (WP1).
- [x] 9. Merge, fast-forward, reset, cherry-pick, revert, the in-progress state (WP1).
- [x] 10. Remotes: fetch, pull, push, safe force-push, set upstream, remotes; auto-fetch (WP2, WP9).
- [x] 11. Credential, passphrase and ssh host-key prompts in the app (WP3a, WP3b).
- [x] 12. Stash save, apply, pop, drop; tag create, delete, push (WP4).

App:

- [x] 13. The operation queue in the app: progress, Cancel, git's own output (WP0, WP6, WP7).
- [x] 14. In-window context menu and the dialogs the operations need (WP5, WP6, WP3b).
- [x] 15. Context menus on every object (WP7-menus).
- [x] 16. Double-click actions and the toolbar's buttons (WP6, WP7).
- [x] 17. Drag a branch onto a branch: the action menu (WP8, WP7-menus).
- [x] 18. Ctrl and Shift selection of several commits; cherry-pick several (WP8, WP7).
- [x] 19. Confirmations for destructive operations; stash and re-apply for a blocked checkout (WP7).
- [x] 20. Basic conflict state: the banner with abort, conflicted files flagged (WP9, WP7).
- [x] 21. The left panel's selection follows the graph's (WP9).
- [x] 22. Phases 0 to 2's checks updated for what phase 3 changes on purpose, recorded under
  "Changes to expected results" in the phase 3 report.

Visual checks:

- [x] 23. Scripted walk-through: `Phase3ChecksA` to `D` in `tests/VisualCommit.VisualTests.Phase3`
  (265 pictures under `artifacts/visual/phase-3/scripted/`).
- [x] 24. Real-window pass for the checks marked for it (`Phase3RealWindowPass`; 45 pictures).

Closing:

- [x] 25. An independent review of the code before the gate (six reviewers, 49 findings among
  them one data-loss blocker), fixed with tests (D123 to D132).
- [x] 26. Visual test gate: `dotnet test` and the real-window pass on one commit (`594f9fb`), every
  picture inspected, report completed.
- [x] 27. CI run of the gate's commit read, every job (all five succeeded).
- [x] 28. `architecture.md` describes what is built.
- [x] 29. `plan.md`: anything not delivered moved, risks updated.
- [x] 30. Handoff `docs/handoffs/phase-3.md`.
- [x] 31. Cold-read check by a fresh agent; gaps fixed (the README, the handoff, the dates and the Docs map; one non-doc item left for phase 4, in the handoff's "Do first").
- [x] 32. `status.md`: links, CI, gate commit, waiting on the owner, state.
- [x] 33. Owner told what passed, what did not, and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

The last run on `phase/3-operations` that tested code was
[38206493320](https://github.com/RahimPasha/VisualCommit/actions/runs/38206493320), for the
gate's commit `594f9fb`: all five jobs succeeded, each with 1756 tests passed: `windows-latest`
(10 min 56 s), `macos-latest` (8 min 35 s), `ubuntu-latest` (3 min 11 s) and the two "bare
ubuntu container" jobs (4 min 1 s and 3 min 25 s). The commits after it changed only docs, which
start no run. CI's Test step has a limit of 14 minutes now (the default run took 3.5 minutes
before phase 3).

CI found faults on the way that this machine could not (git 2.55 prints dates as `Z`, macOS
physical paths, a time limit, timing in some tests): the runs before the gate's last commit were
red until each was fixed; they are listed in the phase 3 handoff.

Before the phase, the last run on `master` was
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

Wait for the owner: "Phase 3 accepted" merges `phase/3-operations` into `master` (CLAUDE.md,
"What the owner says"). To start phase 4, wait for "Start phase 4" once phase 3 is merged; its
first steps and open points are in the [phase 3 handoff](handoffs/phase-3.md) under "For the
next phase".

This always names the single next thing to do and is kept accurate in every commit.

### Notes for whoever resumes

- Nothing is in flight. All work packages and fix branches of phase 3 are merged and their
  worktrees and `wip/*` branches removed. The working tree was clean when phase 3 was set to
  "Awaiting acceptance". Left on the Windows machine: 29 local branches `worktree-wf_*` that the
  worktree tool made, all at `d3814bd` and merged; delete them with `git branch -d` (the phase 3
  handoff, "For the next phase", step 2).
- Phase 3 was built mostly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 3 handoff's "Things that cost time" says what to watch for: a worktree
  starts from `master` and its agent must create its branch from the phase branch first; merged
  worktrees must be removed at once (each build folder is about 580 MB per project); a global
  `core.fsmonitor=true` leaves a git daemon per worktree running (stop them before the gate).
- The session ran on Sonnet 5.5 and, as the owner asked, chose the model per task: Opus 5.5
  sub-agents for designing and writing code, for reviews and for inspecting the gate's pictures;
  Sonnet 5.5 for mechanical work.
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
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- Accept phase 3, or say what to change: "Phase 3 accepted" merges `phase/3-operations` into
  `master`.
- Questions with a recommendation each, in the [phase 3 handoff](handoffs/phase-3.md) under
  "Waiting on the owner" (among them: keep CI's longer time limits; the working-changes row's
  dashed line crossing another branch's node; an exclusion of the repository and temp folders in
  Windows Defender for faster test runs; case-only branch renames on Windows and macOS; an ssh
  timeout for auto-fetch).
- When you have a Mac at hand: the eleven-step checklist at the end of the
  [phase 3 test report](test-reports/phase-3.md), the seven-step one at the end of the
  [phase 2 test report](test-reports/phase-2.md), the nine-step one at the end of the
  [phase 1 test report](test-reports/phase-1.md) and the eight-step one at the end of the
  [phase 0 test report](test-reports/phase-0.md). The owner said this will be looked at later.

Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors", and
the repository was made public, which lifts the limit on CI minutes.

Settled on 2026-10-09 (D41): the owner deleted the GitHub repository and a session created it
again, empty and public, and pushed `master` and `phase/0-foundation` unchanged. The commits from
before the rewrite of the history (D40), which GitHub had kept serving by their IDs, now answer
"not found"; every commit on GitHub carries the owner's personal address. The session's
permission system refuses to delete a repository, so a deletion is always the owner's to do.
