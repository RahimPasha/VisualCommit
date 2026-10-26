# Before phase 7: why phase 6 cost so much, and what to change

The brief for a session the owner starts after phase 6's acceptance and before "Start phase 7".
Written on 2026-10-26 by the session that built and closed phase 6, from what it measured then.
Delete this file, or move what is still true into the proper docs, when the review is done.

## What the owner asked

Phase 6 took a long time and burnt a lot of tokens: the owner ran out of tokens several times.
Many bugs and fixes came up along the way. It is not clear whether phase 6 was simply a big phase or
whether the project now costs more with every phase, because the tests keep growing. Both disks, C:
and D:, kept running out of space. The owner wants a session to investigate this, propose what can
be improved, and apply the solutions the owner agrees to, before phase 7 starts.

## How to work

- This is work outside a phase: commit on `master` (CLAUDE.md, "Rules"). A change to the project's
  tooling or test set-up is the owner's to approve (CLAUDE.md: a tooling change the owner asks for is
  made on `master` and starts a CI run, which is read and recorded in `docs/status.md`). Ask the owner
  before each change that alters how a phase is built or gated (CLAUDE.md, `docs/plan.md`'s Workflow
  and Visual test gate), and record each agreed change as a decision in `docs/decisions.md` (D239
  on), with its reason. Do not reopen a recorded decision without the owner.
- Measure before and after. Every claim of a saving gives the number it was measured with.
- The default `dotnet test` at the root takes about 55 minutes and loads the laptop; do not run it
  more than the investigation needs, and never beside another run you want to measure. Never run
  the real-window pass unless the owner asks and is told first (it takes over the mouse).
- Delete nothing outside the repository without listing it with its size and getting the owner's
  yes, except this session's own scratch files.
- Opus 5.5 for the analysis and the changes; Sonnet 5.5 for mechanical measuring (CLAUDE.md, the
  models rule). Keep sub-agents few: this review must not cost what it is meant to save.

## What was measured (2026-10-26)

### The phase

- Phase 6 ran from 2026-10-18 to 2026-10-24 on the Windows machine: 376 commits up to its gate
  commit `b561b7c`, merged in `3f34c87`. It delivered nine features (workspaces and the dashboard,
  bulk fetch and pull, the command palette, keyboard shortcuts, the settings screen with git's own
  settings, theme polish and System, fetch refspecs, Keep ignored files, the welcome page's folder);
  the plan sized it "M".
- Its own docs: `docs/phase-6-design.md` 7,035 lines (782 KB), `docs/test-reports/phase-6.md` 5,028
  lines (775 KB), `docs/handoffs/phase-6.md` 674 lines. All docs together are 4.6 MB, about 1.15
  million tokens; `docs/architecture.md` is 430 KB and `docs/decisions.md` 184 KB (245 lines of very
  long rows), and CLAUDE.md's "Start of every session" asks every session to read both whole.
- The gate ran four rounds (and two default runs failed before them), each round about 1.5 to 2
  hours of machine time: `dotnet test`, the real-window pass and then the inspection. What each found
  is in `docs/test-reports/phase-6.md`, "Found by the gate".

### Tokens

The session's transcript is
`C:\Users\rpkha\.claude\projects\d--Projects-Personal-Visual-Git\85307141-833d-4adc-9b4b-8b721f1e64ab.jsonl`
(63 MB; the session was compacted several times). Each sub-agent's completion notice in it carries
`<subagent_tokens>`, `<tool_uses>` and `<duration_ms>`; summing them gives 68 sub-agents and agent
workflows with about 67 million tokens. The largest:

| Tokens | Hours | What |
|---|---|---|
| 12.3 M | 1.1 | Design review: six lenses, every finding verified by a second agent (workflow) |
| 8.4 M | 3.7 | Writing the 34 visual checks into the report (workflow) |
| 4.7 M | 1.7 | Drafting the design, eight section drafters (workflow) |
| 3.7 M | 1.0 | Design research, ten topics (workflow) |
| 3.7 M | 1.2 | Editing the design drafts to the lead's rulings (workflow) |
| 3.5 M | 6.9 | Wave 1's build: three packages in worktrees, each reviewed (workflow) |
| 2.7 M | 1.0 | Revising the design from 91 confirmed review findings (workflow) |
| 1.7 M | 2.5 | Rest of wave 1 (workflow) |
| 0.6 to 0.9 M each | | Each later package's builder, reviewer and fixer, each walk-through part, each step 22 fixer |

So about 35 of the 67 million went into the design and the report before the code; a package's
build took under a million. The lead's own session is not in the sum, and the 13 + 2 + 1 picture
inspectors of the gate (images are token-heavy) are counted only in part. The script that summed
them is easy to rewrite: parse the jsonl, find `<task-notification>` texts, read the three tags.

### The tests

| At the gate of | Tests | Default run | Real-window pass | Pictures | CI Windows Test step |
|---|---|---|---|---|---|
| Phase 3 | 1,756 (4 projects) | 19 min 15 s | 11 min 11 s | | |
| Phase 4 | 2,411 (5 projects) | 21 min 52 s | 14 min 4 s | | 13 min 9 s |
| Phase 5 | 3,233 (7 projects) | 43 min 22 s | 24 min 57 s | 1,080 | 25 min 15 s |
| Phase 6 | 5,037 (9 projects) | 56 min 25 s | 27 min 39 s | 1,329 | 24 min 37 s to 35 min |

Code: `src` 98,000 lines in 395 C# files, `tests` 198,000 lines in 596 (twice the app). The run is
bound by the processors: nine test processes of up to eight tests each on the laptop's eight
threads, every test starting git processes; at phase 6's gate a git call took 10 to 15 times as
long as alone, which broke waits until D237 lengthened them (`docs/plan.md` risk 12). Every default
run re-runs every earlier phase's whole walk-through, and every gate re-inspects nearly every earlier
picture when a shared part of the window changes (phase 6's Settings button changed 1,080 of them).

### Disk

| Where | Size | What |
|---|---|---|
| C: free | 27 GB of 350 | |
| D: free | 19 GB of 125 | |
| `D:\Projects\Personal\Visual Git` `bin` and `obj` | 9.24 GB | 30 folders: Debug and Release of every project; each test project's output about 0.6 GB |
| One built worktree under `.claude\worktrees\` | about 6.4 GB | Phase 6 had up to three at once (risk 27) |
| Each test project's `bin\…\runtimes\` | 558 MB of its 593 | SkiaSharp's and HarfBuzzSharp's native libraries for every platform (win-x86, win-x64, win-arm64 118 MB each; a dozen Linux and macOS ones); within them the native `.pdb` files are about 300 MB (`libSkiaSharp.pdb` 80 MB per Windows platform) |
| `C:\Users\rpkha\.nuget\packages` | 11.5 GB | All of the owner's projects', not only this one's |
| `C:\Users\rpkha\AppData\Local\NuGet` | 2.9 GB | NuGet's caches |
| `%TEMP%\claude` | 4.0 GB | Agent sessions' scratch folders (the MinGit copy, CI downloads, probes) |
| `%TEMP%\VisualCommit.Tests` | 79 MB | The tests' repositories (they clean up after themselves) |
| `D:\Projects\Personal\gate5-*`, `gate6-*` | 0.8 GB | The gates' reference pictures and inspection verdicts |
| `D:\Dropbox (Personal)` | 80 GB | The owner's, not the project's |

## What to investigate

1. **Where the tokens go, and what to cut.** Read the phase 6 transcript's notices (above) and the
   phase's process in `docs/plan.md` (Workflow), the phase 6 design's sections 17 and 18 and the
   report's size. Questions: does a design of 7,000 lines and a report of 5,000 lines pay for itself;
   would shorter documents per topic, written as the build goes, do; are six review lenses with a
   verifier per finding needed for every design; how much do agents spend reading whole documents
   (CLAUDE.md, `architecture.md`, `decisions.md`, the design) and could they read only their sections
   (an index, smaller files, a short "decisions in force" summary); which tasks could Sonnet 5.5 have
   done (the owner's rule); how many tokens the picture inspection takes and how to spend them only on
   pictures whose changes no rule explains.
2. **Why the tests cost more with every phase.** Measure per project and per test (the slowest
   hundred, with how many git processes each starts), and how the time splits between phases' walk-
   throughs and unit tests. Questions: should the default run keep every earlier phase's whole
   walk-through, or should earlier phases' walk-throughs run at gates and in CI only (a change to the
   rules, the owner's); fewer pictures per check; scenario templates shared further; fewer git
   process starts per test; how many tests run side by side on the laptop (xunit's
   `maxParallelThreads`, `dotnet test --max-parallel-test-modules`) without lengthening CI, where the
   runners have fewer processors; CI's time against D232's limits (macOS reached 43 min 12 s).
3. **Why so many rounds and fixes.** From the report's "Found by the gate", the status file's history
   (`git log -p docs/status.md` on `phase/6-multi-repo`) and the handoff's "Things that cost time",
   group phase 6's failures by cause (load and timing; machine differences: `/var` against
   `/private/var`, the temporary folder's length, Git 2.55 and 2.56's words; harness: a platform
   dialog's state between runs; flaky waits; real app bugs the inspection found) and say what
   prevents each group systemically: a harness rule, a search of the existing tests for the same
   pattern (fixed waits, `OperatingSystem.IsWindows()` assertions, single lookups of a platform
   control), a check in review.
4. **The disks.** Confirm the table above and find the rest of what fills C: and D: (only the
   project's share is the project's to change). Candidates to measure and propose:
   - stop copying native `.pdb` files and other platforms' `runtimes` into every test project's
     output (for example `AllowedReferenceRelatedFileExtensions`, or a RuntimeIdentifier for local
     builds), checking that CI, the real-window pass (which starts the app from its build output)
     and the documented exe paths (CLAUDE.md, "To look closer at a hang") still work;
   - whether worktrees need a full build each, or fewer at once, or `dotnet build` of one project;
   - a clean-up script in `scripts/` that lists and, on the owner's yes, removes: merged worktrees and
     their branches, `bin`/`obj` of Release builds, agent scratch folders older than a week under
     `%TEMP%\claude`, CI picture downloads, the gate folders of accepted phases, NuGet's http cache;
   - where the default run's temporary data goes (`%TEMP%\VisualCommit.Tests` on C:), and whether a
     variable should let it live on D:.
5. **The size of a phase.** Phase 6 delivered nine features as an "M". Look at phase 7 (Hosting,
   sized "L": GitHub, Azure DevOps and GitLab) in `docs/plan.md` and say whether it should be split,
   and how, so that a phase fits a week of the owner's token budget.

## What to deliver

1. A findings document (this file's sections filled in, or a new `docs/review-before-phase-7.md`
   that replaces this brief), with the measurements, the causes and a ranked list of proposals: for
   each, what it saves (tokens, minutes, GB), what it risks, and what it changes (code, tests,
   CI, CLAUDE.md, the plan's Workflow or gate).
2. The owner's answers to the proposals, each recorded as a decision (D239 on).
3. The agreed changes made on `master`, with CI green and recorded in `docs/status.md`, and CLAUDE.md,
   `docs/plan.md` and `docs/architecture.md` updated where they describe what changed.
4. The before and after numbers of what was changed: the default run's time, a test project's
   output size, free space on C: and D:.
5. `docs/status.md`'s "Next step" back to waiting for "Start phase 7", and this file deleted or
   folded into the docs that own its content.
