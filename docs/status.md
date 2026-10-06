# Status

Last updated: 2026-10-06

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git. The local folder is still
named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Not started | `phase/0-foundation` | | |
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

None in progress. Planning is complete; the next phase to start is Phase 0.

### Progress

Empty until a phase starts. At the start of a phase this becomes a checklist: the phase's
"Delivers" items from `plan.md` split into small steps, then its visual checks, then the closing
steps from `CLAUDE.md`. Each item is ticked in the commit that completes it.

### Next step

Wait for the owner to say "Start phase 0".

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

Nothing yet. Use this for work in flight: what is half-done, what was tried and failed, what to do next.

## Waiting on the owner

- The copyright line in `LICENSE` reads "VisualCommit contributors". Change it if the owner wants their own name there.
