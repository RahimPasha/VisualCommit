# Status

Last updated: 2026-10-06

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git, a public repository. The local
folder on the development machine is still named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Done | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
| 1. Repos and commit graph | Not started | `phase/1-graph` | | |
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

None in progress. Phase 0 was accepted by the owner on 2026-10-06 and merged into `master`
(merge commit `b5f4723`). The next phase to start is phase 1.

Gate commit: none, because no phase is awaiting acceptance. When a phase is set to "Awaiting
acceptance", this line names the commit that both passes of the gate ran on; "Phase N accepted"
checks that nothing but docs changed after it.

### Progress

Empty until a phase starts. At the start of a phase this becomes a checklist: the phase's
"Delivers" items from `plan.md` split into small steps, then its visual checks, then the closing
steps from `CLAUDE.md` and a step for reading the CI run of the gate's commit, which closing
step 6 and the test report record. Each item is ticked in the commit that completes it. Number the steps plainly; do not label them C1,
C2 and so on, which are requirement numbers.

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

Wait for the owner to say "Start phase 1". Its first steps are in the phase 0 handoff under
"For the next phase".

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

- Nothing is in flight. `master` holds everything; the working tree was clean when phase 0 closed.
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
