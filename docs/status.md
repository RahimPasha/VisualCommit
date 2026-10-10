# Status

Last updated: 2026-10-10

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git, a public repository. The local
folder on the development machine is still named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Done | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
| 1. Repos and commit graph | Done | `phase/1-graph` | [phase-1](handoffs/phase-1.md) | [phase-1](test-reports/phase-1.md) |
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

None in progress. Phase 1 was accepted by the owner on 2026-10-10 and merged into `master`
(merge commit `b5eb1ed`); its gate ran on `5673c3f`. Phase 0 was accepted on 2026-10-06 (merge
commit `b5f4723`). The next phase to start is phase 2.

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

The last run on `phase/1-graph` was
[38071012182](https://github.com/RahimPasha/VisualCommit/actions/runs/38071012182), for the
gate's commit `5673c3f`: all five jobs succeeded, each with 379 tests passed. The commits after
it changed only docs. The run for the merge on `master` is added below once it has been read.

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

Wait for the owner to say "Start phase 2". Its first steps, and the open points to settle before
its checks are written, are in the [phase 1 handoff](handoffs/phase-1.md) under "For the next
phase".

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

- Nothing is in flight. `master` holds everything; the working tree was clean when phase 1 was
  merged.
- Phase 1 was built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored), each on a `wip/p1-*` branch merged with `--no-ff` into `phase/1-graph`; the
  handoff's "Things that cost time" says what to watch for. All of them are merged. One worktree,
  `.claude/worktrees/agent-a60aa26c7fe44ae22` on `wip/p1-git-fixes`, is still locked by the
  process of an earlier session; once no session uses it, `git worktree remove --force --force`
  it and delete the local branches `wip/p1-git-fixes` and `worktree-agent-a60aa26c7fe44ae22`
  (`git branch -d`; both are merged). None of these branches is on `origin`.
- The next phase can be started on the Windows machine or in a cloud session; see
  [cloud-sessions.md](cloud-sessions.md). No cloud session has worked on the repo yet, and that
  page lists what the first one should check.
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
