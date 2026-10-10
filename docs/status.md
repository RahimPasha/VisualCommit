# Status

Last updated: 2026-10-09

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git, a public repository. The local
folder on the development machine is still named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Done | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
| 1. Repos and commit graph | In progress | `phase/1-graph` | | [phase-1](test-reports/phase-1.md) (expected results) |
| 2. Diff and commit workflow | Not started | `phase/2-diff-commit` | | |
| 3. Branch, remote, stash and tag operations | Not started | `phase/3-operations` | | |
| 4. Rebase and conflict resolution | Not started | `phase/4-rebase-conflicts` | | |
| 5. History, traceability and undo | Not started | `phase/5-history` | | |
| 6. Multi-repo and productivity | Not started | `phase/6-multi-repo` | | |
| 7. Hosting | Not started | `phase/7-hosting` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 1, Repos and commit graph, is in progress on `phase/1-graph`. It was started on
2026-10-09 on the Windows machine, from `master` at `6d39f1f`. Phase 0 was accepted on
2026-10-06 and merged into `master` (merge commit `b5f4723`).

Gate commit: none yet. When the phase is set to "Awaiting acceptance", this line names the
commit that both passes of the gate ran on; "Phase N accepted" checks that nothing but docs
changed after it.

### Progress

Each item is ticked in the commit that completes it. The numbers are plain step numbers, not
requirement numbers.

Start:

- [x] 1. Phase branch created, phase set to "In progress", this checklist.
- [x] 2. Open points from the phase 0 handoff decided and recorded in `decisions.md`: the open,
  clone and init screens, date format, file list, how a repo tab is modelled, where session
  state is kept, how services get the git runner, how Q1 is measured, whether the 100k check
  runs by default, how the watcher tells the app's own work from outside changes.
- [x] 3. Scenario repos: the graph scenario (branches with folders, merges, tags, a stash, a
  local remote with ahead and behind counts) and the 100k-commit repo; `TempRepo` helpers for
  stashes, remotes and clones; SHAs pinned in `ScenarioTests`.
- [x] 4. Harness: `ShellDriver` starts the app with repos open (session state written by the
  test), scrolls with the wheel and waits for a condition; `RealApp` gets right-click,
  double-click, typing, the wheel and scenario repos.
- [x] 5. Phase 1's visual checks written into `docs/test-reports/phase-1.md`, each marked for
  the real-window pass, and committed before the UI they test.

Git layer:

- [x] 6. Reading a repository: open (top-level folder, git folder), init, refs with upstream
  and ahead/behind, HEAD, stashes. Tests against the scenario repos.
- [x] 7. Commit log streamed in pages from one `git log --date-order`, with stashes merged in.
  Tests, including the 100k repo.
- [x] 8. Lane layout: incremental, rows with their line segments and colours. Tests on the graph
  scenario and on made-up histories.
- [x] 9. Commit details: message, author, committer, parents, changed files with status and
  renames, for root, normal and merge commits. Tests.
- [x] 10. Clone with progress and cancel. Tests against a local repo.
- [x] 11. Repo watcher: changes in the git folder, debounced into one refresh; the app's own
  reads do not trigger it. Tests.

App:

- [x] 12. Session store (`session.json`): open tabs, active tab, recent repos, window placement,
  panel widths. Tests.
- [x] 13. Repo tabs: one view model per tab, tab strip with close buttons, "+" for a new tab,
  switching, tabs restored on restart.
- [x] 14. Welcome page in the graph area of an empty tab: Open (folder picker), Init, Clone form
  with progress and cancel, recent list.
- [x] 15. Commit graph control: custom-drawn and virtualised; lanes, nodes, ref labels, message,
  author, date and SHA columns; selection by click and keys; wheel and scroll bar.
- [x] 16. Lane colours as theme tokens, in `Tokens.axaml` and `architecture.md`.
- [x] 17. Left panel: branches as folders, remotes, tags, stashes, counts, ahead/behind, filter
  box; clicking a ref selects its commit.
- [x] 18. Commit details panel: message, author, date, SHA, parents, changed files flat or as a
  tree, with the choice remembered.
- [x] 19. Status bar shows the current branch.
- [x] 20. File watcher wired to each open repo: refs and graph refresh on outside changes and
  keep the selection.
- [x] 21. Window size and position and panel widths saved and restored.
- [x] 22. Panels give way when the window is too narrow, and take their widths back when it grows.
- [x] 23. The app records the time to the first graph frame and the graph's frame times (how Q1
  is measured).
- [x] 24. Phase 0's checks updated for what phase 1 changed on purpose, recorded under "Changes
  to expected results" in the phase 1 report.

Visual checks:

- [x] 25. Scripted walk-through: one test per check in `Phase1Checks`, screenshots under
  `artifacts/visual/phase-1/scripted/`.
- [x] 26. Real-window pass for the checks marked for it (`Phase1RealWindowPass`).

Closing:

- [ ] 27. Visual test gate: `dotnet test` and the real-window pass on one commit, every picture
  inspected, report completed.
- [ ] 28. CI run of the gate's commit read, every job.
- [ ] 29. `architecture.md` describes what is built.
- [ ] 30. `plan.md`: anything not delivered moved, risks updated.
- [ ] 31. Handoff `docs/handoffs/phase-1.md`.
- [ ] 32. Cold-read check by a fresh agent; gaps fixed.
- [ ] 33. `status.md`: links, CI, gate commit, waiting on the owner, state.
- [ ] 34. Owner told what passed, what did not, and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

Last run: [38026062572](https://github.com/RahimPasha/VisualCommit/actions/runs/38026062572) on
`master`, for commit `edb811e`, the first run in the re-created repository (D41). All five jobs
succeeded: `windows-latest`, `macos-latest`, `ubuntu-latest`, and the two "bare ubuntu
container" jobs. Commits after it changed only docs.

The runs before 2026-10-09 went with the deleted repository, so the run numbers that older
commits and the phase 0 report and handoff mention no longer open. What they showed is as
written there and in this file's history: every run from the phase 0 merge onwards succeeded on
Windows, macOS and Linux, and the two runs before the re-creation, for `b1d2ed0` and
`fd5099a`, succeeded in all five jobs; in both "bare ubuntu container" jobs
`scripts/setup-linux.sh` prepared the empty container in under half a minute, its second run
changed nothing, the build had no warnings and all 118 tests passed (with .NET SDK 10.0.112
from Ubuntu's package servers and 10.0.401 from Microsoft's installer).

Earlier in phase 0 the test step froze the whole job on macOS in 4 of 9 runs. The freezes
stopped with the commit that is now `0784112` (decision D37), and the test step ran 14 times on
macOS without a freeze after it, up to the merge. If a macOS job ever sits in its Test step for
minutes again, read "Known issues" in the phase 0 handoff first.

### Next step

Step 27, the visual test gate: run `dotnet test` and then the real-window pass on the same
commit (the one after this), and inspect every picture of both passes. A first run of the
real-window pass on `e5c8da7` failed three comparisons on letter edges only; the comparison now
allows a one-pixel shift (D58, the owner's choice) and the mouse is moved away before the 100k
End and Home screenshots. The run on `a19b5a3` failed Q1 alone (first rows after 2962 ms): git
calls started from the UI thread held it while the window drew its first frame, which D59
removes. This laptop slows down when hot, and the pass starts right after the 2-minute
`dotnet test`, so let the machine rest a few minutes between the two and say so in the report.

This always names the single next thing to do and is kept accurate in every commit.

### Notes for whoever resumes

- How phase 1 is being built: the main session writes the contracts, the shell and the
  integration; independent parts are built by sub-agents in git worktrees under
  `.claude/worktrees/` (git-ignored), each on a `wip/p1-*` branch that is merged with
  `--no-ff` into `phase/1-graph`. Merged so far: `wip/p1-lane-layout`, `wip/p1-harness`,
  `wip/p1-git-layer`. A worktree is not necessarily created from the phase branch; the agents
  create their branch from `phase/1-graph` first.
- Built and tested in the main session so far: the session store, the tab, welcome and
  repository view models, the tab strip, welcome page and graph-area switch, window placement and
  panel widths (`MainWindow.UseSession`, `PanelLayout`), `GitAccess`, the repository provider
  and the fake folder dialog of the headless harness (`ShellDriver.Folders`).
- Measured on the Windows machine on 2026-10-09, for a 100k-commit repo made with
  `git fast-import`: building it takes about 5 seconds when the commits share one tree (23
  seconds when every commit writes a file). `git log --date-order` over all refs prints its
  first 200 commits after 1.1 seconds and all 100,000 after 2 seconds; with a commit-graph file
  (`git commit-graph write --reachable`) the first 200 come after 0.08 seconds. Without
  `--date-order` the first lines come at once, but then a parent can come before a child.
- The change for cloud sessions (`b1d2ed0`) touched code after phase 0's gate:
  `AppPaths.DefaultDataDirectory` no longer creates the folder it names, and `RealApp.Launch`
  clears the git variables it inherits. CI built and tested it in every job, and on 2026-10-06
  the owner had the real-window pass run on `2730b57` (same code) on the Windows machine: it
  passed, with each of its three pictures within 0.65% of the scripted one, and the pictures
  were inspected. So phase 0's checks hold with that change.
- The next phase can be started on the Windows machine or in a cloud session; see
  [cloud-sessions.md](cloud-sessions.md). No cloud session has worked on the repo yet, and that
  page lists what the first one should check.
- On the Windows development machine only: Windows 11, .NET SDK 10.0.303, Git 2.36.0.windows.1,
  `gh` signed in as RahimPasha. Its screen is 3000×2000 at 200% scaling: 1500×1000 logical
  pixels, of which 1500×952 is the work area. A 1920×1080 window does not fit, so the
  real-window pass can only use 1100×700 there.
- Packages: Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI 5.0.0.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- When you have a Mac at hand: the eight-step checklist at the end of the
  [phase 0 test report](test-reports/phase-0.md). The owner said this will be looked at later.
Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors", and
the repository was made public, which lifts the limit on CI minutes.

Settled on 2026-10-09 (D41): the owner deleted the GitHub repository and a session created it
again, empty and public, and pushed `master` and `phase/0-foundation` unchanged. The commits from
before the rewrite of the history (D40), which GitHub had kept serving by their IDs, now answer
"not found"; every commit on GitHub carries the owner's personal address. The session's
permission system refuses to delete a repository, so a deletion is always the owner's to do.
