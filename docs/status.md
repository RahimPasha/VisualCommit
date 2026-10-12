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
| 3. Branch, remote, stash and tag operations | Done | `phase/3-operations` | [phase-3](handoffs/phase-3.md) | [phase-3](test-reports/phase-3.md) |
| 4. Rebase and conflict resolution | Not started | `phase/4-rebase-conflicts` | | |
| 5. History, traceability and undo | Not started | `phase/5-history` | | |
| 6. Multi-repo and productivity | Not started | `phase/6-multi-repo` | | |
| 7. Hosting | Not started | `phase/7-hosting` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

None in progress. Phase 3 was accepted by the owner on 2026-10-11 and merged into `master`
(merge commit `8b33745`); its gate ran on `594f9fb`. Phase 2 was accepted on 2026-10-10 (merge
commit `e039070`), phase 1 on 2026-10-10 (merge commit `b5eb1ed`) and phase 0 on 2026-10-06 (merge
commit `b5f4723`). The next phase to start is phase 4.

Gate commit: none yet. When a phase is set to "Awaiting acceptance", this line names the commit
that both passes of the gate ran on; "Phase N accepted" checks that nothing but docs changed
after it.

### Progress

Empty until a phase starts. At the start of a phase this becomes a checklist: the phase's
"Delivers" items from `plan.md` split into small steps, then its visual checks, then the closing
steps from `CLAUDE.md` and a step for the CI result that each phase's "Done when" asks for. Each
item is ticked in the commit that completes it. Number the steps plainly; do not label them C1,
C2 and so on, which are requirement numbers.

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

Wait for the owner to say "Start phase 4". Its first steps, and the open points to settle before
its checks are written, are in the [phase 3 handoff](handoffs/phase-3.md) under "For the next
phase".

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

- Nothing is in flight. `master` holds everything; the working tree was clean when phase 3 was
  merged. Left on the Windows machine: 29 local branches `worktree-wf_*` that the worktree tool
  made, all at `d3814bd` and merged; delete them with `git branch -d` (the phase 3 handoff, "For
  the next phase", step 2).
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
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- Answered on 2026-10-12, for phase 4 to build: git's own list of commits for several picks
  (D136, replacing a sentence of D88); automatic cleanup of the backups after 30 days or when
  more than a set number exist (D137): phase 4 proposes the number and the moment.
- The virus-scanner exclusion on the development machine (the repository folder and
  `C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests`) was set up by the owner on
  2026-10-12. Time the next full default run and compare it with the 19 minutes of phase 3's gate.
- Still open, with the handoff's recommendation to keep things as they are unless the owner says
  otherwise: the working-changes row's dashed line crossing another branch's node (06f);
  case-only branch renames on Windows and macOS (D131); an ssh timeout for auto-fetch (D125).
- When you have a Mac at hand: the eleven-step checklist at the end of the
  [phase 3 test report](test-reports/phase-3.md), the seven-step one at the end of the
  [phase 2 test report](test-reports/phase-2.md), the nine-step one at the end of the
  [phase 1 test report](test-reports/phase-1.md) and the eight-step one at the end of the
  [phase 0 test report](test-reports/phase-0.md). The owner will do them at the end, when a Mac
  is at hand (2026-10-11).
- Whether to make phase 1's `Check_13_clone_with_progress` deterministic (see "CI"): recommended
  as one of phase 4's first commits, so that no phase starts from a `master` whose CI can fail by
  chance. Without an answer, "Start phase 4" does it.
- When the project is over: whether a CI run of 30 minutes or more is reasonable or needs another
  solution (D134).

Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors", and
the repository was made public, which lifts the limit on CI minutes.

Settled on 2026-10-09 (D41): the owner deleted the GitHub repository and a session created it
again, empty and public, and pushed `master` and `phase/0-foundation` unchanged. The commits from
before the rewrite of the history (D40), which GitHub had kept serving by their IDs, now answer
"not found"; every commit on GitHub carries the owner's personal address. The session's
permission system refuses to delete a repository, so a deletion is always the owner's to do.
