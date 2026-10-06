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

States: Not started, In progress, Blocked (cannot continue or close without the owner; the reason
is under "Waiting on the owner"), Awaiting acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

None in progress. Phase 0 was accepted by the owner on 2026-10-06 and merged into `master`
(merge commit `2e55748`). The next phase to start is phase 1.

### Progress

Empty until a phase starts. At the start of a phase this becomes a checklist: the phase's
"Delivers" items from `plan.md` split into small steps, then its visual checks, then the closing
steps from `CLAUDE.md` and a step for the CI result that each phase's "Done when" asks for. Each
item is ticked in the commit that completes it. Number the steps plainly; do not label them C1,
C2 and so on, which are requirement numbers.

### CI

Recorded here at the end of each phase and after each merge: the last CI run, its commit and its
result on each platform.

The last run on the phase 0 branch was [37446300297](https://github.com/RahimPasha/VisualCommit/actions/runs/37446300297)
for commit `2806273`: success on Windows, macOS and Linux. The run for the merge on `master` is
added below once it has been read.

Earlier in phase 0 the test step froze the whole job on macOS in 4 of 9 runs. The freezes
stopped with commit `b907071` (decision D37), and the test step ran 13 times on macOS without a
freeze after it. If a macOS job ever sits in its Test step for minutes again, read "Known
issues" in the phase 0 handoff first.

### Next step

Wait for the owner to say "Start phase 1". Its first steps are in the phase 0 handoff under
"For the next phase".

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

- Nothing is in flight. `master` holds everything; the working tree was clean when phase 0 closed.
- Development machine: Windows 11, .NET SDK 10.0.303, Git 2.36.0.windows.1, `gh` signed in as
  RahimPasha. Its screen is 3000×2000 at 200% scaling: 1500×1000 logical pixels, of which
  1500×952 is the work area. A 1920×1080 window does not fit, so the real-window pass can only
  use 1100×700 there.
- Packages: Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI 5.0.0.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once.

## Waiting on the owner

- When you have a Mac at hand: the eight-step checklist at the end of the
  [phase 0 test report](test-reports/phase-0.md). The owner said this will be looked at later.

Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors", and
the repository was made public, which lifts the limit on CI minutes.
