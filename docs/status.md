# Status

Last updated: 2026-10-05

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Not started | | | |
| 1. Repos and commit graph | Not started | | | |
| 2. Diff and commit workflow | Not started | | | |
| 3. Branch, remote, stash and tag operations | Not started | | | |
| 4. Rebase and conflict resolution | Not started | | | |
| 5. History, traceability and undo | Not started | | | |
| 6. Multi-repo and productivity | Not started | | | |
| 7. Hosting | Not started | | | |
| 8. Public release | Not started | | | |

States: Not started, In progress, Awaiting acceptance (gate passed, not yet merged), Done (merged).

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

- The copyright line in `LICENSE` reads "Tracery contributors". Change it if the owner wants their own name there.
- CI can only run after the repo is pushed to GitHub. Until then macOS and Linux builds are untested. Pushing soon after phase 0 is recommended.
