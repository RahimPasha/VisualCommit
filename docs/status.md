# Status

Last updated: 2026-10-10

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git, a public repository. The local
folder on the development machine is still named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Done | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
| 1. Repos and commit graph | Done | `phase/1-graph` | [phase-1](handoffs/phase-1.md) | [phase-1](test-reports/phase-1.md) |
| 2. Diff and commit workflow | In progress | `phase/2-diff-commit` | | [phase-2](test-reports/phase-2.md) (expected results only) |
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

Phase 2, Diff and commit workflow, is in progress on `phase/2-diff-commit`. It was started on
2026-10-10 on the Windows machine, from `master` at `cec9c0c`. Phase 1 was accepted on
2026-10-10 and merged into `master` (merge commit `b5eb1ed`); phase 0 on 2026-10-06 (merge
commit `b5f4723`).

Gate commit: none yet. When the phase is set to "Awaiting acceptance", this line names the
commit that both passes of the gate ran on; "Phase N accepted" checks that nothing but docs
changed after it.

### Progress

Each item is ticked in the commit that completes it. The numbers are plain step numbers, not
requirement numbers.

Start:

- [x] 1. Phase branch created, phase set to "In progress", this checklist.
- [x] 2. Open points from the phase 1 handoff decided and recorded in `decisions.md`: the
  working-changes row, the default diff mode, how a discard is confirmed and restored, how a
  diff opens (asked of the owner); the diff viewer's technology after trying AvaloniaEdit 12.0.0
  on Avalonia 12.1.3, the font for code, when a file counts as very large, how stage and discard
  are offered before phase 3's context menus, how the watcher sees the working tree and keeps
  out of the app's own writes (owner told), no editor for git, how a discard's snapshot keeps
  untracked files, in-app dialogs.
- [x] 3. Harness: `TempRepo` writes bytes (a binary file, an image) and makes a PNG; scenario
  repos get a local identity; the app's commits get fixed dates in both harnesses.
- [x] 4. Scenario repos with known working-tree changes (modified, added, deleted, renamed,
  binary, an image, a very large file; staged and unstaged; one with `core.autocrlf`), pinned in
  `ScenarioTests`.
- [x] 5. Phase 2's visual checks written into `docs/test-reports/phase-2.md`, each marked for
  the real-window pass, reviewed, and committed before the UI they test.

Git layer:

- [x] 6. Working-tree status (`git status --porcelain=v2`): staged, unstaged, untracked,
  renamed and conflicted files. Tests.
- [x] 7. Diffs: a file's unstaged, staged and untracked changes and a commit's change to a file;
  the unified-diff parser; binary files, images and file sizes. Tests.
- [x] 8. Patches for a hunk or chosen lines, to stage, unstage and discard, applied with
  `git apply`. Tests, including a repository with `core.autocrlf`.
- [x] 9. Stage, unstage and discard whole files; commit and amend with the message passed
  explicitly and no editor. Tests.
- [x] 10. A snapshot before every discard, untracked files included, kept under
  `refs/visualcommit/backup/`, and restoring it. Tests.
- [x] 11. The watcher sees the working tree (not `.git`, not ignored files), and the app's own
  writes do not set it off. Tests.

App:

- [x] 12. Working-changes row at the top of the graph when there are changes: drawing,
  selection, keys. Tests.
- [x] 13. Stage-and-commit panel in the right panel: unstaged and staged lists (flat or tree),
  stage, unstage and discard by file and all, summary and description editor, amend, commit.
- [x] 14. Confirmation dialog in the window; the restore bar after a discard.
- [ ] 15. Diff view in place of the graph: header with the file and its actions, close; opened
  from the stage lists and from a commit's files.
- [ ] 16. Diff text, inline and side by side: line numbers, added and removed backgrounds, the
  font for code, theme colours; the mode remembered.
- [ ] 17. Syntax highlighting and word-level highlights.
- [ ] 18. Stage, unstage and discard a hunk or chosen lines from the diff view.
- [ ] 19. Image diff; binary and very large files.
- [ ] 20. Phase 1's checks updated for anything phase 2 changes on purpose, recorded under
  "Changes to expected results" in the phase 2 report.

Visual checks:

- [ ] 21. Scripted walk-through: one test per check in `Phase2Checks`, screenshots under
  `artifacts/visual/phase-2/scripted/`.
- [ ] 22. Real-window pass for the checks marked for it (`Phase2RealWindowPass`).

Closing:

- [ ] 23. Visual test gate: `dotnet test` and the real-window pass on one commit, every picture
  inspected, report completed.
- [ ] 24. CI run of the gate's commit read, every job.
- [ ] 25. `architecture.md` describes what is built.
- [ ] 26. `plan.md`: anything not delivered moved, risks updated.
- [ ] 27. Handoff `docs/handoffs/phase-2.md`.
- [ ] 28. Cold-read check by a fresh agent; gaps fixed.
- [ ] 29. `status.md`: links, CI, gate commit, waiting on the owner, state.
- [ ] 30. Owner told what passed, what did not, and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

The last run on `phase/1-graph` was
[38071012182](https://github.com/RahimPasha/VisualCommit/actions/runs/38071012182), for the
gate's commit `5673c3f`: all five jobs succeeded, each with 379 tests passed. The commits after
it changed only docs.

The run for the merge on `master`:
[38077727040](https://github.com/RahimPasha/VisualCommit/actions/runs/38077727040), for
`eb626ba` (the merge commit `b5eb1ed` and the commit that set phase 1 to "Done"). All five jobs
succeeded, each with 379 tests passed: `windows-latest`, `macos-latest`, `ubuntu-latest`, and
the two "bare ubuntu container" jobs.

Before phase 1, the last run on `master` was
[38026062572](https://github.com/RahimPasha/VisualCommit/actions/runs/38026062572), for `edb811e`,
the first run in the re-created repository (D41); all five jobs succeeded. The runs before
2026-10-09 went with the deleted repository, so the run numbers that older commits and the
phase 0 report and handoff mention no longer open; what they showed is as written there and in
this file's history.

Earlier in phase 0 the test step froze the whole job on macOS in 4 of 9 runs. The freezes
stopped with the commit that is now `0784112` (decision D37). If a macOS job ever sits in its
Test step for minutes again, read "Known issues" in the phase 0 handoff first.

### Next step

Merge the diff text control from the sub-agent's branch `wip/p2-diff-text` (a worktree under
`.claude/worktrees/`, not pushed) with `--no-ff`, then wire it into `Views/Diff/DiffView`
(`DiffTextHost`): `Diff`, `Mode`, `FilePath`, `Highlighting`, `HunkActions` (from the view
model's side), `SelectedChanges` into `DiffViewModel.SelectedChanges`, `HunkActionRequested`
into `DiffViewModel.RunHunkActionAsync`, `SelectionResetRequested` to `ClearSelection()`, and
`FocusBody()` when a diff opens. Then steps 15 to 19 and the remaining checks (3 to 8, 10 to
13, 19 to 21).

### Notes for whoever resumes

- The default tests passed (379) on `master` at `aead41b` before the phase started.
- The owner answered the four questions of taste on 2026-10-10, each with the recommended option
  (D60 to D63). The technical decisions are D64 to D72. AvaloniaEdit 12.0.0 was tried in a
  throwaway headless program outside the repo: it drew an editor with TextMate colours and
  JetBrains Mono on Avalonia 12.1.3. The plain JetBrains Mono draws `=>` as an arrow, hence the
  NL cut (D64).
- Phase 1 was built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 1 handoff's "Things that cost time" says what to watch for. One
  worktree, `.claude/worktrees/agent-a60aa26c7fe44ae22` on `wip/p1-git-fixes`, is still there:
  this session's permission system refused `git worktree remove --force --force` on it. The owner
  can remove it and the merged local branches `wip/p1-git-fixes` and
  `worktree-agent-a60aa26c7fe44ae22` (`git branch -d`). None of them is on `origin`.
- No cloud session has worked on the repo yet; [cloud-sessions.md](cloud-sessions.md) lists what
  the first one should check.
- On the Windows development machine only: Windows 11 on an Intel Core i7-8650U laptop, .NET SDK
  10.0.303, Git 2.36.0.windows.1, `gh` signed in as RahimPasha. Its screen is 3000×2000 at 200%
  scaling: 1500×1000 logical pixels, of which 1500×952 is the work area. A 1920×1080 window does
  not fit, so the real-window pass can only use 1100×700 there. The laptop is slower when hot:
  let it rest a few minutes between `dotnet test` and the real-window pass, which times Q1.
- Packages: Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI 5.0.0.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- When you have a Mac at hand: the nine-step checklist at the end of the
  [phase 1 test report](test-reports/phase-1.md) and the eight-step one at the end of the
  [phase 0 test report](test-reports/phase-0.md). The owner said this will be looked at later.

Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors", and
the repository was made public, which lifts the limit on CI minutes.

Settled on 2026-10-09 (D41): the owner deleted the GitHub repository and a session created it
again, empty and public, and pushed `master` and `phase/0-foundation` unchanged. The commits from
before the rewrite of the history (D40), which GitHub had kept serving by their IDs, now answer
"not found"; every commit on GitHub carries the owner's personal address. The session's
permission system refuses to delete a repository, so a deletion is always the owner's to do.
