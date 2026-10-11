# Status

Last updated: 2026-10-10

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git, a public repository. The local
folder on the development machine is still named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Done | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
| 1. Repos and commit graph | Done | `phase/1-graph` | [phase-1](handoffs/phase-1.md) | [phase-1](test-reports/phase-1.md) |
| 2. Diff and commit workflow | Done | `phase/2-diff-commit` | [phase-2](handoffs/phase-2.md) | [phase-2](test-reports/phase-2.md) |
| 3. Branch, remote, stash and tag operations | In progress | `phase/3-operations` | | [phase-3](test-reports/phase-3.md) (expected results only) |
| 4. Rebase and conflict resolution | Not started | `phase/4-rebase-conflicts` | | |
| 5. History, traceability and undo | Not started | `phase/5-history` | | |
| 6. Multi-repo and productivity | Not started | `phase/6-multi-repo` | | |
| 7. Hosting | Not started | `phase/7-hosting` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 3, Branch, remote, stash and tag operations, is in progress on `phase/3-operations`. It was
started on 2026-10-10 on the Windows machine, from `master` at `d3814bd`. Phase 2 was accepted on
2026-10-10 and merged into `master` (merge commit `e039070`); phase 1 on 2026-10-10 (merge commit
`b5eb1ed`) and phase 0 on 2026-10-06 (merge commit `b5f4723`).

Gate commit: none yet. When the phase is set to "Awaiting acceptance", this line names the
commit that both passes of the gate ran on; "Phase N accepted" checks that nothing but docs
changed after it.

### Progress

Each item is ticked in the commit that completes it. The numbers are plain step numbers, not
requirement numbers.

Start:

- [x] 1. Phase branch created, phase set to "In progress", this checklist.
- [x] 2. Open points from the phase 2 handoff decided and recorded in `decisions.md` (D77 to
  D115; the design behind them is `docs/phase-3-design.md`, working notes that close the phase
  by being folded into `architecture.md` and deleted). The owner
  answered four questions of taste on 2026-10-10 (later phases' actions are left out of the
  menus; pull follows git's own configuration; auto-fetch is on, every 5 minutes; a checkout
  blocked by local changes offers to stash, check out and re-apply). The rest is decided by the
  session: where the operation queue lives, credential and ssh prompts, which operations
  confirm and what their backup refs are called, what the toolbar buttons do, how context menus
  and drag-and-drop work in both harnesses, how far the conflict banner goes.
- [x] 3. Harness (WP-S2, WP5; the real-window helpers are compiled, not run): whatever the two passes need to open a context menu, drag a branch and read
  an operation's progress; `TempRepo` helpers for remotes if they are missing.
- [x] 4. Scenario repos for the operations (diverged branches, remotes ahead and behind, tags,
  stashes, conflicts, a remote that asks for credentials), pinned in `ScenarioTests`: work
  package WP-S1, merged in `efbbf83`.
- [ ] 5. Phase 3's visual checks written into `docs/test-reports/phase-3.md`, each marked for
  the real-window pass, reviewed independently, and committed before the UI they test.
- [x] 6. The flaky `DiffHighlighterTests` docstring test (failed once in the baseline run under
  load, passes alone) fixed at its cause, not by loosening it: `d72e500`. The first line a
  grammar tokenizes compiles its regular expressions, and TextMateSharp counts that against the
  one-second limit; a line that overran it is now tokenized once more.

Git layer:

- [x] 7. Operation queue per repository (one write at a time, watcher paused, progress, cancel,
  git's own output in the error), and backup refs before destructive operations. Tests. (WP0,
  WP1)
- [x] 8. Branches (WP1): checkout (local, remote into a tracking branch, a commit), create, rename,
  delete (local and remote), set upstream. Tests.
- [x] 9. Merge (both kinds; WP1) and fast-forward, reset (soft, mixed, hard), cherry-pick, revert,
  and reading the in-progress state (conflicted files, which operation). Tests.
- [x] 10. Remotes (WP2; the auto-fetch scheduler is WP9): fetch, pull (as git is configured), push, safe force-push, set upstream,
  add, rename, change and remove remotes; auto-fetch. Tests against a local remote.
- [x] 11. Credential (WP3a, WP3b), passphrase and ssh host-key prompts answered in the app, and turned off
  where there is no one to ask (auto-fetch). Tests with a local server that asks for credentials.
- [x] 12. Stash save (WP4), apply, pop, drop; tag create, delete, push (and delete on the remote). Tests.

App:

- [x] 13. The operation queue in the app: it takes over `WriteAsync`, shows progress and Cancel
  in the status bar, and shows a failure with git's own output (WP0, WP6; the operations
  themselves are WP7).
- [x] 14. In-window context menu control and the dialogs the operations need (name input,
  choice, error with git's output, credentials, remotes): WP5, WP6, WP3b.
- [ ] 15. Context menus on every object: commit, local branch, remote branch, tag, stash, file
  (both stage lists, the diff view and a commit's files), remote.
- [ ] 16. Double-click actions (checkout a branch or remote branch, apply a stash...) and the
  toolbar's Fetch, Pull, Push, Branch, Stash and Pop buttons become real.
- [ ] 17. Drag a branch onto a branch: the action menu.
- [ ] 18. Ctrl and Shift selection of several commits in the graph; cherry-pick several.
- [ ] 19. Confirmations for destructive operations; stash and re-apply for a blocked checkout.
- [ ] 20. Basic conflict state: the banner with abort, conflicted files flagged.
- [x] 21. The left panel's selection follows the graph's (phase 1 follow-up).
- [ ] 22. Phases 0 to 2's checks updated for anything phase 3 changes on purpose, recorded under
  "Changes to expected results" in the phase 3 report.

Visual checks:

- [ ] 23. Scripted walk-through: one test per check in `Phase3Checks`, screenshots under
  `artifacts/visual/phase-3/scripted/`.
- [ ] 24. Real-window pass for the checks marked for it (`Phase3RealWindowPass`).

Closing:

- [ ] 25. An independent review of the code before the gate; what it finds fixed, with tests.
- [ ] 26. Visual test gate: `dotnet test` and the real-window pass on one commit, every picture
  inspected, report completed.
- [ ] 27. CI run of the gate's commit read, every job.
- [ ] 28. `architecture.md` describes what is built.
- [ ] 29. `plan.md`: anything not delivered moved, risks updated.
- [ ] 30. Handoff `docs/handoffs/phase-3.md`.
- [ ] 31. Cold-read check by a fresh agent; gaps fixed.
- [ ] 32. `status.md`: links, CI, gate commit, waiting on the owner, state.
- [ ] 33. Owner told what passed, what did not, and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

The last run on `phase/2-diff-commit` was
[38097840078](https://github.com/RahimPasha/VisualCommit/actions/runs/38097840078), for the
gate's commit `2a13804`: all five jobs succeeded (`windows-latest`, `macos-latest`,
`ubuntu-latest` and the two "bare ubuntu container" jobs), each with 557 tests passed. The
commits after it changed only docs.

The run for the merge on `master`:
[38116890189](https://github.com/RahimPasha/VisualCommit/actions/runs/38116890189), for
`7aa3349` (the merge commit `e039070` and the commit that set phase 2 to "Done"). All five jobs
succeeded, each with 557 tests passed: `windows-latest`, `macos-latest`, `ubuntu-latest`, and
the two "bare ubuntu container" jobs.

Before phase 2, the last run on `master` was
[38077727040](https://github.com/RahimPasha/VisualCommit/actions/runs/38077727040), for
`eb626ba` (phase 1's merge commit `b5eb1ed` and the commit that set phase 1 to "Done"): all five
jobs succeeded, each with 379 tests passed. The changes made on `master` after it, up to
`cec9c0c`, were docs only.

The run numbers from before 2026-10-09 went with the deleted repository (D41), so those that
older commits and the phase 0 report and handoff mention no longer open; what they showed is as
written there and in this file's history.

Earlier in phase 0 the test step froze the whole job on macOS in 4 of 9 runs. The freezes
stopped with the commit that is now `0784112` (decision D37). If a macOS job ever sits in its
Test step for minutes again, read "Known issues" in the phase 0 handoff first.

### Next step

Step 5: the four drafts of the visual checks (`docs/phase-3-checks-A.md` to `-D.md`, uncommitted
working files) are being finalised against the settled design; then they are joined into
`docs/test-reports/phase-3.md`, reviewed independently and committed. No UI work package (WP5 to
WP9, and the dialogs of WP3 and WP6) starts before that commit. Meanwhile the Git-layer
packages WP1, WP2, WP3a, WP4 and the harness package WP-S2 are built in worktrees
(`.claude/worktrees/`, branches `wip/p3-wp1` and so on) and merged by the lead.

This always names the single next thing to do and is kept accurate in every commit.

### Notes for whoever resumes

- Phase 3 is built by work packages (design section 8). Done and merged: WP0 (operation queue,
  contracts, app plumbing: `cde2d28`) and WP-S1 (scenarios and the credential test server:
  `efbbf83`). Each other package is built on a `wip/p3-*` branch in a git worktree and merged with
  `--no-ff`; a worktree starts from the phase branch's newest commit, and its agent creates its
  branch from `phase/3-operations` first. The menu spike (design 4a) found that the stock
  `MenuFlyout` on the overlay layer works on real Win32 (D105).
- The default tests on `master` at `d3814bd`, before the phase started: 556 of 557 passed. The
  one failure, `DiffHighlighterTests.A_python_docstring_has_the_string_colour_and_def_and_the_function_name_theirs`
  (a colour came back null), passed when its class ran alone: a timing flake of the
  highlighter's one-second safety net per line when three test projects run side by side. Step 6
  fixed it. Not fixed, found on the way: the first tokenization of a grammar that embeds other
  languages (Markdown) reads TextMateSharp's grammar list with no shared lock while another
  thread may be adding a grammar to it; no test has failed on it.
- The owner answered four questions of taste on 2026-10-10: later phases' actions (rebase onto,
  squash, compare, file history, blame, create pull request) are left out of phase 3's menus;
  pull follows git's own configuration (`pull.rebase`, `pull.ff`) and merges when nothing is
  set; auto-fetch is on by default, every 5 minutes; a checkout blocked by local changes offers
  "stash, check out, re-apply".
- Phases 1 and 2 were built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 1 handoff's "Things that cost time" says what to watch for. All of
  them are merged and removed, with their local branches. A worktree starts from `master`, not
  from the phase branch: an agent must create its branch from `phase/3-operations` first.
- The session that started phase 3 ran on Sonnet 5.5 and, as the owner asked, chose the model per
  task: Opus 5.5 sub-agents for designing and writing code and for reviews, Sonnet 5.5 for
  mechanical work.
- The next phase can be started on the Windows machine or in a cloud session; see
  [cloud-sessions.md](cloud-sessions.md). No cloud session has worked on the repo yet, and that
  page lists what the first one should check.
- On the Windows development machine only: Windows 11 on an Intel Core i7-8650U laptop, .NET SDK
  10.0.303, Git 2.36.0.windows.1, `gh` signed in as RahimPasha. Its screen is 3000×2000 at 200%
  scaling: 1500×1000 logical pixels, of which 1500×952 is the work area. A 1920×1080 window does
  not fit, so the real-window pass can only use sizes up to 1400×900 there. The laptop is slower
  when hot: let it rest a few minutes before the gate's `dotnet test` and again before the
  real-window pass, which times Q1.
- Packages: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and
  TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI 5.0.0.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- When you have a Mac at hand: the seven-step checklist at the end of the
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
