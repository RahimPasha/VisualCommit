# Review before phase 7: why phase 6 cost so much, and what to change

> Applied on 2026-10-28: D1 and D2 as D239; T3, N1, D7 and D6 as D240; T11/P9 and T1/P10 as D241; T2/P1, T9 and T8/P12's models as D242; P2/T5 and P4 as D243; T6/P3 as D244; S2/N2 and S1 as D245; N3 and R3 as D246; T7/P11's smaller batches, without triage, as D247; and question 8 (the laptop's git, now 2.56) as D248.
> Every other proposal here is still only a proposal. This file is kept as history; its brief, `docs/before-phase-7-review.md`, was deleted (it is in `be63d89`).

Findings for the owner, written on 2026-10-26 on `master` at `be63d89`, after phase 6's acceptance
and before "Start phase 7". The brief was `docs/before-phase-7-review.md`; this file answers its
"What to deliver", item 1. Nothing has been changed yet. Each proposal waits for the owner's answer,
and each answer is then recorded as a decision (D239 on). When the review is done, this file and the
brief are deleted, or what stays true is moved into the docs that own it.

## 1. Summary

- **Cost.** Phase 6 cost about $2,720 at API prices: 6.73 billion tokens, 97% of them cache reads.
  Phase 5 cost $1,977 and phase 4 $795. Sub-agents used 89% of phase 6. ($ in this file is an
  API-price equivalent, not the owner's bill: how the plan's limits weigh tokens is not known.)
- **A big phase, and a process that costs more every phase.** Both. Per 1,000 lines added to `src`,
  phases 3 to 6 cost $41, $54, $87 and $108. The docs grow about 1.6 times per phase (5.1 MB now),
  and the default test run at the gates went from 19 to 56 minutes over phases 3 to 6.
- **Where the tokens went.**
  - 20 agents that each made 200 or more calls, carrying about 500k tokens of context per call:
    $1,127 (41%).
  - Five-minute prompt caches that expired while agents waited on builds and tests, so their whole
    context was written again: $597 (21%), mostly in the same long agents.
  - The design stage, many agent rounds that produced a 775 KB design: $514 (19%).
  - Documents make up about 26% of everything the agents carried.
- **The 74-hour stop.** The weekly limit hit on 2026-10-19, 31.6 hours into phase 6, after phase 6
  had used $1,081. Phase 5's last days had already used $1,568 of the same week.
- **The tests.** Almost all of their time is git process starts on Windows, on a laptop that the run
  saturates. The same 5,034 tests take 5 minutes on CI's Ubuntu runner and 32 on CI's Windows runner.
  During the build, agents ran about 37 whole-project test runs in worktrees, several at once, beside
  the lead's full runs. That load caused failures that then had to be fixed.
- **The rounds.** Red CI runs had 21 distinct causes. Only 4 were app defects; the other 17 were tests
  that assumed something about the machine (git versions, macOS paths and file watcher, timing). The
  same kinds recur since phase 3. The gate took 9 h 53 min from its first run to its final pass, about
  7 hours more than a clean round.
- **The disks.** 94.8% of the build output is the same SkiaSharp and HarfBuzzSharp native files for
  21 platforms, with their debug symbols, copied into 16 output folders. A built worktree is 5.8 to
  7.0 GB, and phase 6 had up to three at once. A build setting measured in a worktree cuts a built
  worktree by 90.6% (6.985 to 0.658 GB).
- **What to do first** (section 5):
  - the build trim (D1);
  - one-hour prompt caches for sub-agents, a Claude Code setting worth $229 of a phase-6-sized phase
    (T3, simulated on phase 6's calls);
  - splitting phase 7, and starting phases and stages only with enough of the week left (T11/P9);
  - self-contained briefs and bounded agents (T2/P1);
  - a shorter design with a lighter review (P2/T5, P4);
  - test classes instead of whole projects during the build, one heavy run at a time (S2/N2).

  Together the token proposals should save $900 to $1,300 of a phase-6-sized phase (33 to 48%),
  allowing for their overlap. That is an estimate, not a measurement.

## 2. What was measured

### 2.1 How the review worked

Five areas were measured on 2026-10-26: tokens (T), tests (S), fix rounds (R), disk (D), and process,
docs and phase size (P). A full test timing run and a disk inventory were added. Three skeptics then
re-measured the areas' numbers and proposals: one for tokens and process, one for tests and rounds,
one for disk. Where a skeptic corrected a number, this file gives the corrected number and says so.

Sources, by the label used below:

| Label | What it is |
|---|---|
| [T] | One script over every lead and sub-agent transcript of the project (about 3 GB), each API call counted once per message id, checked against Claude Code's own cost records |
| [T-sk] | The tokens skeptic's own scan: 761 transcript files, 54,501 API calls; Claude Code 2.1.310's settings schema; the phase 6 workflow records |
| [P] | Scripts over git history (`git ls-tree -l`, `git diff --shortstat` at each merge) and the same transcripts |
| [S] | Test runs with per-test times (xunit reports), git's trace2 events, CI run 40073425553's logs |
| [S-sk] | The tests skeptic's re-runs on a quiet laptop (06:13 to 07:06), counts from the transcripts and the sources |
| [R] | `gh run list` and `gh run view` for `phase/6-multi-repo`, the failed logs, the fix commits, the report and handoff |
| [D] | The disk inventory (Python `os.scandir`, apparent sizes) and the disk experiments in a worktree |
| [D-sk] | The disk skeptic's re-measurement, 06:00 to 06:20 |

Units and conventions:

- **$.** Anthropic's prices for Opus 5.5 per million tokens: $4 input, $20 output, $5 for a 5-minute
  cache write, $8 for a one-hour write, $0.20 for a cache read. Sonnet 5.5 is counted at half; its
  real price is unclear (section 6).
- **Output tokens.** The transcripts written by the Claude Code version phase 6 used log 3.2 to 4.4
  times too few output tokens, so output is corrected per session from Claude Code's cost records.
  Cache reads and writes match those records to 0-1%. Records written by 2.1.310 carry the final
  output, so a fixed correction factor must not be applied to new transcripts.
- **Mw.** Some process figures are "Mw", million weighted tokens: input 1, cache write 1.25, cache
  read 0.1, output 5, with output not corrected. The skeptic found that this weighs a cache read at
  twice Opus 5.5's real ratio (0.05), so context-size savings look about 1.5 times larger in Mw than
  in $. This file uses $ wherever it can.
- **Sizes and times.** GB means 1024^3 bytes, as Explorer shows, and sizes are sums of file lengths.
  Times are UTC.

**The test timings were disturbed by the review itself.** Today's full run (03:01 to 03:54) overlapped
the disk inventory's scans. The tests area's runs alone (04:56 to 05:43) overlapped the disk
experiments' builds. They also traced git (`GIT_TRACE2_EVENT`), which doubles the time of a git-heavy
test. The tests skeptic ran Git.Tests and App.Tests alone again on a quiet laptop, and their times are
the ones used here. Process counts taken from the traces are still valid.

**The data behind these numbers is temporary.** Every script and data file is under
`C:/Users/rpkha/AppData/Local/Temp/claude/d--Projects-Personal-Visual-Git/85307141-833d-4adc-9b4b-8b721f1e64ab/scratchpad/review/`
(called `<scratch>` below), in the folders `tokens/final`, `test-timings`, `tests-S`, `rounds`, `disk`,
`disk-exp`, `process`, `skeptic-TP`, `skeptic-SR` and `disk-skeptic`. The folder holds about 180 MB,
plus about 175 MB of git traces in `tests-S/runs`, and is deleted after the review. This file is the
record. The token scripts are the starting point for T1.

### 2.2 Tokens

**Per phase** ([T] `tokens/final/report_T.txt` section 1; phases 5 and 6 re-measured by [T-sk]; Mw by
[P] `process/perphase.txt`). Each API call counts in the phase whose interval, from the previous merge
to this merge, contains it.

| Phase | $ | Tokens | Mw | Agents' share of $ |
|---|---|---|---|---|
| 0 | | | 15 | |
| 1 | 189 | | 94 | |
| 2 | 121 | | 42 | |
| 3 | 872 | 2.13 B | 302 | 94% |
| 4 | 795 | 2.14 B | 270 | 81% |
| 5 | 1,977 | 5.00 B | 660 | 91% |
| 6 | 2,723 (T: 2,732) | 6.73 B (T: 6.75 B) | 918 | 89% ($2,439) |

**Phase 6 in more detail** ([T] sections 1 and 10, [T-sk]):

- **Calls.** 19,277 API calls, 1,193 of them the lead's, and 247 agent transcripts.
- **By kind of token:**

  | Kind | Tokens | $ | Share |
  |---|---|---|---|
  | Cache reads | 6,534 M | 1,307 | 47% |
  | Cache writes | 186.5 M | 971 | 35% |
  | Output, after correction | about 22.6 M | 451 | 16% |
  | Uncached input | about 0 | | |

  The cache writes were 174.7 M five-minute writes, all by agents, and 11.8 M one-hour writes, all by
  the lead.
- **Model and effort.** Every call ran Opus 5.5 at effort "xhigh" (19,274 of 19,277 records; the
  other 3 are synthetic). The workflow scripts named the model `opus` 29 times. Thinking was 55% of
  output (Claude Code's records: 6.44 M of 11.63 M).

**Phase 6 by activity** ([T] section 2; activities from each agent's description and workflow phase)

| Activity | Phase 6 | Phase 5 |
|---|---|---|
| Building | $892 (32%) | $592 |
| Fixing | $354 (13%) | $338 |
| The lead | $292 (11%) | $180 |
| Code review | $260 (10%) | $240 |
| Design drafting and editing | $255 (9%) | design in all: $151 |
| Writing the visual checks | $223 (8%) | $190 |
| Design review | $152 (6%) | (in design) |
| Picture inspection | $118 to $137 (5%) | $114 |
| Closing docs | $82 (3%) | |
| Design research | $80 (3%) | (in design) |

**Phase 6 by stage** ([T] sections 2c and 2d; stage bounds from the ticks in `docs/status.md` on the
phase branch):

| Stage | Steps | $ |
|---|---|---|
| Design | 1 to 4 | 514 |
| Build, harness and checks | 5 to 21 | 1,471 |
| Code review and fixes | 22 | 529 |
| Gate | 23 to 24 | 202 |
| Closing | 25 to 30 | 11 |

The architecture and handoff drafting fell into step 22's window, which is why closing looks so cheap.

What drove the cost:

- **Long agents with large contexts** ([T] section 9, re-measured by [T-sk]). Every call reads the
  whole context again, so an agent's cost is roughly its context size times its calls.

  | Calls per agent | Agents | $ | Average context per call |
  |---|---|---|---|
  | 0 to 49 | 113 | 169 | 123k |
  | 50 to 99 | 80 | 608 | 242k |
  | 100 to 199 | 34 | 525 | 342k |
  | 200 or more | 20 (5,834 calls) | 1,127 | 503k (T: 486k) |

  Six agents compacted at about 967k, among them "Build WP-SET" and one "Fix review findings". Average
  agent context per call grew phase by phase: 253k, 293k, 302k and 335k for phases 3 to 6.
- **Orientation before work** ([P] `orient.py`, re-measured by [T-sk]). Builders started at 51k to 55k
  of context and reached their first edit at 306k to 557k, after 53 to 98 calls (WP-D 557k, WP-B
  466k, WP-SET 461k, WP-W 424k, WP0 419k). The median added before the first edit, walk-through
  writers included, was 362k. That reading stays in the context for every later call.
- **Expired caches** ([T] `rewrites.csv` and `drill.txt`, re-measured by [T-sk]).
  - A rewrite here is a cache write of more than half the previous context.
  - Agents made 231 rewrites (T: 234), 104.4 M tokens, after gaps of: under 5 min 44 times, 5 to 15 min
    162, 15 to 60 min 23, an hour or more 2.
  - The call before a gap was a wait loop (29 times), a test run wrapped in a timeout (25), a turn
    that ended waiting for background work (23, plus 11 sub-agent handbacks), `dotnet build` (8) or
    `dotnet test` (3).
  - The lead's one-hour cache expired 15 times (9.3 M tokens, median gap 72 min).
  - Together $597, 21% of phase 6 (phase 5 17%, phase 4 9%, phase 3 21%). 80.7 M of the rewritten
    tokens ($404) were in the 20 long agents, so this cause and the previous one overlap by about 77%.
- **What fills the agents' contexts** ([T] `context_parts.json`; [P] `carry2.txt`). These shares are
  approximate: each tool result's characters divided by 2.35, times the calls that carry it.

  | Part | Share of agent context, phase 6 |
  |---|---|
  | Docs, all | 26% (20% in phases 3 to 5) |
  | The design document alone | 18% (phase 5: 11%) |
  | The fixed start context | 16 to 17% |
  | Code read and searched | 12% |
  | Other shell output | 17% |
  | Pictures | 1.5% |

- **How agents read the design.** Agents made 903 Reads of `docs/phase-6-design.md` (144 agents), and
  only 1 of them had no limit, so they already read it by section ([T-sk]; P counted 1,292). They also
  ran 844 `sed` and 890 `grep` commands on .md files ([T]). The design document cost about $340 of the
  agents' cache cost ([T-sk]).
- **The lead.** 1,191 calls, mean context 528k (median 541k), $293 ([T-sk]). It compacted twice, on
  2026-10-22 at 15:42 and 2026-10-23 at 17:43, each at about 968k ([T-sk]; P said 4, but it counted
  the "continued from a previous conversation" text). Phase 5's lead compacted once.
- **The fixed start context.** An agent's first call is a median 51k tokens.
  - CLAUDE.md is 33,970 bytes, about 12.7k tokens at 2.67 characters per token. Carried by every call,
    it costs about $63 per phase ([T-sk]).
  - Every agent's start also carries a 27k-character list of about 540 deferred tools from unused
    claude.ai connectors (Higgsfield, Figma, Gmail, Indeed, Chrome) and an 18k-character skill list
    ([T-sk]). Their token count was not measured.
- **Design stage, phase 6** ([T] agents grouped by workflow and phase):
  - drafting $101, compiling $10, editing to rulings $42, recompiling $8, revising $39,
    consolidating $12;
  - review: 6 lenses $60, plus 93 verifiers (one per finding) $92;
  - research (10 topics) $80.

  The review raised 93 findings: 52 confirmed, 39 partly, 2 refuted. As raised, 4 were blockers and
  15 majors; `docs/status.md` step 3 counts 1 blocker and 10 majors among those it took as confirmed.
  The verifiers amended or refuted 41 of the 93 (44%), including 9 of the 15 majors and 1 of the 4
  blockers. Only the 2 refutations, both minor, were wasted ([T-sk] from the workflow record
  `wf_876a1b1c-290.json`).
- **Writing the visual checks**, $223 in all: writers $96, review $61, fixes $40, facts $16,
  assembling $10. "What the checks share" is 3,140 of the report's 5,028 lines ([T], [T-sk]).
- **Picture inspection** ([T] section 7, [T-sk]).
  - A 1100×700 picture is about 1,043 tokens. All 1,521 picture reads of phase 6 were about 1.7 M
    tokens (about $28).
  - The inspection over all rounds cost $118 to $137 (18 agents). The re-inspection round after the
    fixes cost about $7.
  - One agent ("Inspect earlier real-window captures", 422 pictures, 216 calls) cost $39 to $44.
    Inspectors with 60 to 130 calls cost $3 to $7 each.
- **Mechanical agents.** Compile, assemble, consolidate, apply rulings and similar: 12 agents, $113
  ([T]).

**The usage limits** ([T] `limits.py` over the lead transcripts, corrected by [T-sk]):

| When | What |
|---|---|
| 2026-10-06 22:29 and 10-07 00:29 | Session limit, then weekly limit, at about $241 of use (between phases 0 and 1) |
| 2026-10-13 11:21 | Monthly spend cap reached, in phase 5. The week of 10-08 used $2,410 including paid extra usage |
| 2026-10-19 06:59 | Weekly limit in phase 6, reset 10-22 09:00: about 74 hours with no work |

The week that ended at 10-19 06:59 began at 10-15 09:00 and held $2,650. By call time, phase 5's tail
(10-15 09:00 to 10-17 23:21) used $1,568 of it, and phase 6 used $1,081 in 31.6 hours, which matches
Claude Code's records for that process exactly ($1,081.76). T's split ($1,364 for phase 5, the rest
phase 6) was wrong. The stop came from where phase 6 started in the week, as well as from its size.

Two things to know about these measurements:

- **The brief's 67 M figure is the wrong measure.** `<subagent_tokens>` in a completion notice is
  the agent's final context size, not what it consumed. The WP0 builder was notified at 837,408 with
  a context of 833,038, but it read 156.8 M tokens from cache over 279 calls ([T], [T-sk]). The design
  review workflow reported 12.33 M and consumed 278 M ([P]).
- **Markdown is about 2.35 characters per token** on Opus 5.5 (median of 1,473 samples in [T], 755 in
  [P]), not 4. All docs, 5.1 MB, are about 2.2 M tokens.

### 2.3 Tests

**The default run at each gate** (CLAUDE.md, `docs/plan.md` risk 12, the brief):

| Gate | Tests | Projects | Default run | Real-window pass | CI Windows Test step |
|---|---|---|---|---|---|
| Phase 3 | 1,756 | 4 | 19 min 15 s | 11 min 11 s | |
| Phase 4 | 2,411 | 5 | 21 min 52 s | 14 min 4 s | 13 min 9 s |
| Phase 5 | 3,233 | 7 | 43 min 22 s | 24 min 57 s | 25 min 15 s |
| Phase 6 | 5,037 | 9 | 56 min 25 s | 27 min 39 s | 24 min 37 s to 35 min |

**Growth, counted at each merge** ([S]: `git grep` and `git show` at `b5eb1ed`, `e039070`, `8b33745`,
`ce5f337`, `c299939` and `3f34c87`):

| At the merge of phase | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| Git.Tests test methods | 121 | 180 | 505 | 744 | 1,081 | 1,348 |
| App.Tests test methods | 155 | 228 | 680 | 916 | 1,174 | 1,811 |
| Walk-through test methods | 22 | 47 | 122 | 187 | 339 | 461 |
| Lines of tests | 11.6k | 18.1k | 60.6k | 91.2k | 137.0k | 197.7k |
| Lines of `src` | 8.5k | 14.7k | 33.9k | 47.4k | 68.2k | 90.5k |

**Per project.**

- *Today's full run* is from [S] `test-timings/project_summary.csv`. The laptop was loaded, and
  App.Tests was killed by the hang watchdog 1,294 s in.
- *Alone, clean* is the tests skeptic's re-run on a quiet laptop ([S-sk]). The tests area's own runs
  alone were 2,049 s and 2,446 s; they were disturbed (2.1), and these replace them.
- *CI* is run 40073425553 on `master`; its logs give MTP's time per project ([S] `ci_pivot.csv`).

| Project | Tests | Today's full run, wall | Alone, clean | CI Windows | CI macOS | CI Ubuntu |
|---|---|---|---|---|---|---|
| App.Tests | 2,167 | killed at 1,294 s | 1,358 s | 24 m 25 s | 23 m 54 s | 3 m 22 s |
| Git.Tests | 2,326 | 3,133 s | 928 s | 26 m 29 s | 17 m 8 s | 1 m 44 s |
| VisualTests (phases 0-2) | 50 | 944 s | | 3 m 0 s | 2 m 13 s | 1 m 32 s |
| .Phase3 | 82 | 2,813 s | | 13 m 53 s | 6 m 9 s | 2 m 6 s |
| .Phase4 | 90 | 1,900 s | | 9 m 2 s | 2 m 42 s | 1 m 24 s |
| .Phase5 | 99 | 1,909 s | | 7 m 22 s | 3 m 40 s | 1 m 45 s |
| .Phase5Undo | 72 | 2,954 s | | 13 m 47 s | 7 m 6 s | 1 m 53 s |
| .Phase6 | 132 | 2,646 s | | 13 m 18 s | 9 m 21 s | 1 m 51 s |
| .Phase6Look | 19 | 823 s | | 3 m 50 s | 1 m 45 s | 40 s |
| Job | 5,037 (CI: 5,034 elsewhere) | 52 m 15 s | | 31 m 48 s | 25 m 19 s | 5 m 6 s |

What the runs show:

- **Today's full run failed on load.** The watchdog killed App.Tests on a test that takes 20 s alone,
  while other tests waited for the filters scenario's template. `Phase6ChecksW.Check_W1` failed with
  "read 7 repositories in 26977 ms" where 8 were expected; it was not rerun. The processors averaged
  99.5%, and free memory fell to 595 MB ([S] `full-run-attempt1.log`, `sampler_summary.json`). Part of
  that load was the review's own disk scans.
- **The laptop is saturated by one project.** Git.Tests alone kept the processors at 100% in 34 of 42
  samples, with 8.2 git processes alive on average ([S-sk]). With 7.4 tests in flight each test ran
  about 3.6 times slower than alone (UndoStepFileTests: 56 to 61 s serially, 211 s in Git.Tests
  alone), so 8 threads give about twice the throughput of one ([S-sk]).
- **Each project's share of the processor work** is roughly Git.Tests 28%, App.Tests 30% and the
  walk-throughs 40%. This assumes the work adds up across projects and was not measured per process
  ([S-sk]; the tests area's 55 to 65% for Git.Tests came from the disturbed runs).
- **App.Tests has a serial floor.** Its 692 headless tests run one at a time on one UI thread. They
  sum to 922 s, 68% of its clean 1,358 s ([S-sk]).
- **Git process starts.** Git.Tests starts 22,892 git processes, 3,305 of them started by another git
  ([S] trace2 events).
  - The commonest commands: `config` 3,157, `rev-parse` 3,105, `for-each-ref` 1,383, `add` 1,210,
    `commit` 1,092.
  - `config --get-regexp ^remote\..*\.url$` runs once per scenario copy in `TempRepo.CopyAsync`, 676
    times.
  - The pull settings are read key by key, 534 times.
  - A file-undo test starts 29 to 102 git processes; an App.Tests undo test starts up to 176.
- **What trace2 does to the timings.** Tracing to a folder roughly doubles a git-heavy test's time
  (UndoStepFileTests untraced 56 and 61 s, traced 105 and 118 s, 870 git processes either way). So
  git's share of a test's time is not established: the tests area's 72% counted nested git processes
  twice, and counting each moment once gives 55% in the trace ([S-sk]).
- **CI's Windows runner is about six times slower than Linux on the same tests** (31 m 48 s against
  5 m 6 s; Git.Tests 26 m 29 s against 1 m 44 s) ([S]).
- **The walk-throughs' slowest tests are mostly waiting.** A test's time includes its wait for the
  headless UI thread ([S] `steps.py` over the timings folders):
  - `Check_W9_restart` took 691 s, of which 53 s was app time.
  - `Check_B7` 291 s against about 5 s; `Check_A3` 262 s against 9.5 s.
  - The walk-through projects average 0.98 to 2.1 tests in flight.
  - Phases 3 to 6 start the app 229 times, and the first picture comes a median 7 to 17 s after each
    start, under load.
- **The earlier phases' walk-throughs** (phases 0 to 5: 393 tests) take 51.5% of the local project
  time and 41% of CI Windows' ([S]).
- **Phase 6's expectation tests run real git:** 56 tests summing 2,172 s, plus 10 summing 225 s. The
  earlier phases' expectation tests take 3 s or less per project ([S]).
- **Templates.** There are 18 scenario templates, built once per process: 75 builds per full run
  ([S-sk]; S counted 76).
- **The git launcher.** `rev-parse HEAD` takes 40 ms through `Git\cmd\git.exe` and 22 to 24 ms
  through `mingw64\bin\git.exe` on a quiet laptop ([S-sk]). The gate's runs start from Git Bash, which
  uses the second, so they do not pay this.

**Full local runs in phase 6** ([S-sk] from the transcripts):

- The lead ran `dotnet test` at the root once at the session start (10-18 01:52), 4 times during the
  build (10-19 05:08, 10-22 20:54, 10-23 03:10, 10-23 12:16) and 6 times at the gate.
- Sub-agents made about 37 unfiltered whole-project runs in worktrees, about 25 of App.Tests and 12
  of Git.Tests, several within minutes of each other (10-23 00:05, 00:09 and 00:11, three agents).
- The lead's failed run on `125e792` (10-22 20:54) began 37 minutes after an agent started a whole
  App.Tests run.
- CLAUDE.md's start step 6 asks for a full run at every session start. Phase 6 had one lead session,
  so that was one run.

### 2.4 Fix rounds

**CI on `phase/6-multi-repo`**, 10-18 03:40 to 10-24 03:42 ([R], re-counted by [S-sk]):

- 45 runs: 19 passed (2 of them only on a second attempt), 16 failed, 10 were cancelled by a newer
  push. With the failed first attempts of 3 runs, 19 attempts were red.
- Windows jobs took up to 38.1 min and macOS jobs up to 45.3 min; ubuntu-latest took 4.2 to 7.7 min
  and the containers 4.4 to 8.2 min.
- Git versions: the laptop has 2.36.0.windows.1; CI has 2.43.0 in the containers, 2.55.0 on
  ubuntu-latest, 2.56.0.windows.2 on Windows and 2.56.0 on macOS. The design derived its git facts on
  2.36 and 2.55 only.

**The 21 distinct causes of red attempts**, by group ([R] `rounds/catalogue.tsv`, corrected by [S-sk]
where marked). 4 were app defects: a lost click in the stage lists (`43751d0`), Git 2.56's
orphaned-worktree wording (`431fb42`), the scan dialog shown before its search (`431fb42`), and a
duplicate palette entry under macOS's `/var` (`f9db5b0`, `c6e1cb2`).

| Group | Cause | Phase 6 cost |
|---|---|---|
| A | Git version and install differences: 2.56's push tracking refs, worktree words, `blame --abbrev` width; 2.55/2.56 refuse `remote add team/fork`; git-shell on Linux and macOS | 5 causes, 7 red runs, about 4 to 5 h |
| B | macOS reaches temp folders through `/var`, git writes `/private/var` | 2 causes in 3 red runs. Only C12 (1 run) was a test-side mismatch; C16 (2 runs) was a real app defect ([S-sk]) |
| C | macOS's watcher reports late; tests counted git calls or read state too early | 3 causes, 4 red attempts, about 3 h (C1 was an app defect, flaky since phase 2) |
| D | Wall-clock ratios, the runner's process priority | 3 causes, 4 red attempts and 1 local failure, about 3 h |
| E | Missing waits, and wait limits that did not fit the load | 4 CI causes in 6 red attempts, 3 failed local full runs, 2 failed gate default runs; about 6 h |
| F | Rendering and layout: FreeType's ink on Linux; the CI runner's temp path is 6 characters longer; an unexplained macOS glyph failure | 3 causes, 2 red runs, about 1.5 h |
| G | Integration of packages built in parallel | 1 cause, 1 red run on all 5 jobs |
| H | Windows' Open dialog keeps state between real-window runs; the new real-window code never ran before the gate | 2 gate rounds, about 3.5 h of laptop time |
| I | Real app defects found only by inspecting pictures | 3 defects, 1 extra gate round |
| J | Expected results that guessed pixel widths, positions and colours | 20 corrections (11 during the build, 9 at the inspection), no reruns |

**The gate** ([R]; report "Found by the gate"; [S-sk]):

- 6 default runs, 2 of which failed before round 1 (the watchdog, and check 23's 30 s wait; D237
  followed). 4 real-window passes, 2 of which failed on S14.
- From the first gate run to the final pass took about 9 h 53 min (10-23 19:34 to 10-24 05:27). A
  clean round with its inspection takes about 2 h 45 min.
- Rounds 3 and 4 fixed only `tests/VisualCommit.RealWindowTests/RealApp.cs` (`639db4a`, `b561b7c`).
  The default run does not build that project, but each round still re-ran it (53 min 13 s and
  56 min 25 s) because both passes have to run on one commit ([S-sk]).

**More findings on the rounds:**

- **Lead tokens** ([R] `usage.py`, re-counted by [S-sk]). The lead made 1,191 turns in phase 6: 626.6 M
  tokens raw, 613.8 M of them cache reads, about 81 M weighted by price. Between each CI failure
  notice and its fix's push: 294 turns, about 181 M raw. That is an upper bound, since the windows
  overlap other work.
- **The same groups recur since phase 3.** Phase 3 had CI-only Git 2.55 differences and four gate
  runs. Phase 4 had Git 2.55 differences, races only CI showed, and three gate runs. Phase 5 had
  Git 2.56 arriving mid-phase and a headless double-click clock ([R], the handoffs' "Things that cost
  time"). The lessons live in long handoff prose, and nothing in the code enforces them.
- **The Git 2.56 check existed but did not stop failures.** The phase 6 handoff already tells
  sessions to check 2.56 with MinGit 2.56.0.windows.2, and the lead had a copy from 10-22 11:58. Two
  2.56 causes (C15, C19) still reached CI on 10-23 ([S-sk]).
- **The Wheel hit test failed twice.** Run 39825749271 was never recorded in any doc; run 40059391404
  failed on the gate commit. Its fix is already listed for phase 7 in `docs/plan.md` ([R], [S-sk]).
- **Short waits are still everywhere** ([S-sk] `shortwaits.txt`). 111 lines in the tests hold waits
  of 5 to 30 s: 65 in App.Tests, 18 in Git.Tests, 8 in RealWindowTests (whose default wait is 10 s)
  and 20 in the walk-throughs and Testing. `OutsideChangeTimeout` alone is 30 s in four walk-through
  support files, with 47 uses.
- **Reviews during the build.** 29 commits fixed review findings during the build ([R]). Step 22's
  review led to 55 fixes, the worst a data-loss-class overwrite of an ignored file by
  directory-rename detection, plus 3 majors (handoff).

### 2.5 Disk

**Free space** ([D], [D-sk]):

- C: had 23.2 to 24.7 GB free of 350 GB on 10-26. The brief measured 27 GB earlier that day, and the
  handoff 15.7 GB at phase 6's start.
- D: had 18.0 to 18.1 GB free of 124.85 GB.
- C: and D: are two partitions of one SSD, a 477 GB Samsung.

**The project's share by drive** ([D] section 7; sizes from [D-sk] where re-measured):

| Drive | Item | Size |
|---|---|---|
| D: | Repo folder (9.24 GB of it `bin` and `obj`; tracked files 19.8 MB; `.git` 28.9 MB; `artifacts` 110 MB) | 9.40 GB |
| D: | `gate5-*` and `gate6-*` folders (`gate6-inspection` 592 MB, of it a CI download of 395 MB) | 0.83 GB |
| C: | Transcripts, `~/.claude/projects/d--Projects-Personal-Visual-Git` (92% sub-agent transcripts) | 3.34 GB |
| C: | Agent scratch, `%TEMP%\claude\d--Projects-Personal-Visual-Git` (phase 6's session 2.54 GB, of it `p6-report/build` 1.61 GB) | 3.62 GB (3.76 GB with this review's scratch) |
| C: | This project's packages in `~/.nuget/packages` (85% SkiaSharp and HarfBuzzSharp) | 1.24 GB of 11.54 GB |
| C: | This project's packages in NuGet's http cache | 0.31 GB of 2.76 GB |
| C: | `%TEMP%\VisualCommit.Tests` (`shared` 34.6 MB, the 100k-commit repository) | 0.05 GB |
| C: | Crash dumps of the test processes and git | 0.02 GB |

**What fills `bin`** ([D], re-measured by [D-sk]):

- There are 24 `bin/<configuration>` folders. 16 of them hold the same 558 MB `runtimes/` folder:
  App, App.Tests, Git.Tests, RealWindowTests and VisualTests in Debug and Release, and the six later
  walk-throughs in Debug. That is 8.7 GB, 94.8% of all `bin`.
- One test output (VisualTests Debug) is 588.6 MB. Of that, `runtimes/` is 558.3 MB, the packages'
  native `.pdb` files are 303.6 MB, and win-x64's libraries without their `.pdb` are 18.5 MB.
- `libSkiaSharp.pdb` ships inside the NuGet package at 84.0, 86.8 and 84.9 MB for win-x64, win-x86
  and win-arm64. Over all outputs it is 3.8 GB in 48 copies.
- Outputs that nothing local runs: the Release builds of App.Tests, Git.Tests and VisualTests, and
  RealWindowTests in Debug. Together 2.27 GB ([D-sk]; D said 2.33).
- A worktree built at the root is about 5.8 GB. With the real-window Release chain it is 6.985 GB.
  Three such worktrees add 17 to 21 GB, about all of D:'s free space when phase 6 started. That total
  is computed; the peak itself was not measured. `docs/plan.md` risk 27 still says about 5 GB.

**The experiments** ([D] `disk-exp`: a clean `dotnet build` at the root plus
`dotnet build tests/VisualCommit.RealWindowTests -c Release`, in a worktree at `be63d89`):

| Build | One built worktree | One test output |
|---|---|---|
| Today | 6.985 GB | 588.6 MB |
| (a) The packages' native `.pdb` files left out | 3.427 GB | 285.0 MB |
| (b) Only this machine's runtimes (win-x64, win, any) | 1.831 GB | 148.8 MB |
| (a) and (b) | 0.658 GB (-90.6%) | 48.7 MB |

What else the experiments showed:

- **An ordinary incremental build removes the old copies.** Over a built worktree, with the targets
  file added, a plain build gave 0.659 GB: MSBuild's IncrementalClean deleted them.
- **The trimmed builds work.** Smoke runs passed on every variant: SettingsDialogViewTests 11/11,
  Phase6 Check_W3 1/1 (which draws with Skia), Git.Tests *IgnoreTests 29/29, and the hang-diagnostics
  exe path from CLAUDE.md.
  - The trimmed `.deps.json` lists exactly the files present.
  - The real-window pass's app path does not change.
  - A picture from the trimmed build matches the untrimmed one pixel for pixel, except in one region
    that also differs between two trimmed runs ([D-sk]).
- **Building one test project** instead of the solution: 1.197 GB, or 0.142 GB trimmed.
- **A scratch tool** that references Avalonia.Skia: 542.8 MB, or 20.1 MB with the targets file passed
  in.
- **Build time.** A clean root Debug build took 108 s and 83 s with the trim (mean of 2 each),
  started at 17 to 36% processor load. Treat it as an indication only.
- **Alternatives tried and rejected.**
  - `AllowedReferenceRelatedFileExtensions` keeps the native `.pdb` files and drops the managed ones
    instead.
  - A `RuntimeIdentifier=win-x64` build reaches the size of (b), but changes the output layout.

**Other findings** ([D], [D-sk]):

- **The draft clean-up script** (`<scratch>/disk-exp/clean-disk.py`) listed 14.87 GB removable,
  counting overlaps once. Its removal code failed on git's read-only files ([D-sk], reproduced), and
  it marked a working agent's worktree removable (section 4, D3).
- **NuGet's http cache** (`%LOCALAPPDATA%\NuGet\v3-cache`) is 2.76 GB. 1.6 GB of it has not been
  written for over 180 days.
- **Transcripts look pruned already.** None in any project is older than 2026-10-05, while
  `%TEMP%\claude` still holds folders from 2026-07-04.
- **C:'s swings are not the project's.** C: moved by 1.4 to 1.5 GB within minutes with no project
  writes, and the page file did not change. Other writers seen: Visual Studio's installer payload in
  `%TEMP%\hpnxoufx` (2.4 GB, written 10-26 01:05) and `C:\ProgramData\Package Cache` (written 10-25).
  The project's 8.7 GB on C: grows slowly and explains neither the swings nor C: filling up.
- **Memory commit** ([D-sk], at 06:10 with no test running):
  - 34,419 MB was committed against a limit of 38,793 MB: 16.3 GB of RAM plus a 22 GB system-managed
    page file on C:.
  - `WUDFHost` (PID 10968, running since the boot on 10-14) held 9,569 MB private, which looks like a
    leaking driver host.
  - A full test run then needs more commit on top. That makes the page file grow on C:, or
    allocations fail.
- **Should the tests' temporary data move to D:?** No change ([D-sk]).
  - `Path.GetTempPath()` already follows TMP and TEMP.
  - C: and D: share one SSD, so there is nothing to gain in I/O.
  - The leftovers are 48 MB.
  - The owner's Defender exclusion for `%TEMP%\VisualCommit.Tests` (2026-10-12) would be lost.
  - A different temp path length brings back phase 6's failures over the temporary folder's length.
- **Large items on C: that are not the project's,** for the owner to decide on:
  - `%APPDATA%\Claude\vm_bundles`, 10.1 GB, last written 2026-07-08;
  - other projects' packages in `~/.nuget/packages`, 10.3 GB;
  - old .NET SDKs and a 1.0 GB NuGetFallbackFolder in `C:\Program Files\dotnet`;
  - `%TEMP%\hpnxoufx`, 2.4 GB.

### 2.6 Process, docs and phase sizes

**Docs at each phase's merge** ([P] `docsizes.tsv`, `git ls-tree -l`; CLAUDE.md included):

| Merge of phase | 0 | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|---|
| All docs | 110 KB | 256 KB | 390 KB | 1,193 KB | 1,898 KB | 3,078 KB | 5,097 KB |
| `architecture.md` | 19 KB | 35 KB | 53 KB | 134 KB | 194 KB | 300 KB | 426 KB |
| `decisions.md` | 12 KB | 30 KB | 42 KB | 88 KB | 106 KB | 126 KB | 183 KB |
| Phase design | | | | 220 KB | 170 KB | 210 KB | 775 KB |
| Phase report | | | | 325 KB | 322 KB | 640 KB | 770 KB |

CLAUDE.md grew from 9.5 KB to 34 KB, and `plan.md` from 18 KB to 78 KB.

- **Docs outgrew code in phase 6.** It added 2,018 KB of docs against 1,137 KB of `src` ([P]).
- **The reading at a session's start keeps growing.** What CLAUDE.md's start steps 2 to 5 ask a fresh
  session to read was 165 KB at phase 3's start, 329 KB at phase 4's, 397 KB at phase 5's and 556 KB at
  phase 6's. It is 777 KiB now, about 319k tokens ([P], [T-sk]). The phase 6 lead had read 197k tokens
  by its first edit.
- **Churn during phase 6.** The design took 22 commits (+940 KB, -190 KB) and the report 23 commits
  (+1,138 KB, -393 KB) ([P]).
- **History in the working docs.** About 4.2 of the 5.1 MB is finished phases' material ([P]).
  - The phase 6 design's section 19 (47.7 KB) repeats D195 to D230 from `decisions.md`.
  - Table rows over 1,000 characters hold 44% of `decisions.md` and 41% of the report, and a grep hit
    returns the whole row.
  - 1,595 lines in 851 files under `src`, `tests` and `scripts` cite the earlier designs and reports
    by path ([T-sk]).

**Phase size against cost** ([P]: items from `docs/plan.md`, commits and lines from git; $ from [T]
and [T-sk]):

| Phase | Plan size | Items (carried over) | Commits | `src` lines added | $ | Mw | $ per 1,000 `src` lines |
|---|---|---|---|---|---|---|---|
| 0 | S | 8 | 16 | 2.4k | | 15 | |
| 1 | L | 7 (2) | 42 | 7.7k | 189 | 94 | |
| 2 | L | 5 | 33 | 7.4k | 121 | 42 | |
| 3 | L | 12 (4) | 194 | 21.1k | 872 | 302 | 41 |
| 4 | L | 10 (4) | 193 | 14.8k | 795 | 270 | 54 |
| 5 | L | 12 (5) | 263 | 22.9k | 1,977 | 660 | 87 |
| 6 | M | 9 (6) | 387 | 25.2k | 2,723 | 918 | 108 |

The plan says only "Sizes are relative: S, M, L", and the letters do not track cost. Phase 6, an M,
cost 1.4 times phase 5, an L. It ran 6.2 days from its first to its last branch commit, about 3.1 of
them active ([P]).

**Phase 7** in `docs/plan.md`:

- It is sized L and has 10 "Delivers" items, 6 of them carried over from phases 3 and 6.
- "Done when" asks for sign-in, clone, and pull-request list and create for each of GitHub, Azure
  DevOps and GitLab.
- Risks 4 (a test account or token per provider) and 5 (app registrations) are still open.
- By analogy with phases 5 and 6, not by measurement, it would cost 1.1 to 1.6 weeks of the allowance
  under the current process ([P]).

## 3. Causes, ranked

Ranked by their share of phase 6's cost, or by the time and space they took. The shares overlap.

1. **Agents carry very large contexts through hundreds of calls.** Every call re-reads the whole
   context. 20 agents with 200 or more calls cost $1,127 (41%). Builders read 306k to 557k tokens
   before their first edit, and fixers' contexts reached 820k. Work packages and fix batches were too
   big for one agent.
2. **Agents' five-minute caches expired while they waited** on builds, test runs and their own
   background work. Each expiry wrote the whole context again at 25 times the price of a cache read:
   $597 (21%), about 77% of it in the agents of cause 1. Claude Code has a setting for one-hour caches
   for sub-agents, which phase 6 did not use.
3. **The design and the report were multi-round, multi-agent productions.**
   - The design ($514, 19%): research, 8 drafters, a compiler, editors, revisers, and a review of
     6 lenses with a verifier per finding.
   - The report checks ($223, 8%).
   - The design is 775 KB, 3.7 times phase 5's, and 18% of what agents carried.
4. **Phases are not sized or scheduled against the weekly allowance.** Phase 6 cost $2,723. It started
   after phase 5's tail had used $1,568 of the week, so it stopped for 74 hours after 31.6 hours of
   work. Carried-over items swell phases: 6 of phase 6's 9 items were carried over.
5. **The docs grow about 1.6 times per phase and are read piecemeal.**
   - Docs are 26% of agent context.
   - The start reading is 777 KiB.
   - CLAUDE.md costs about $63 per phase, and the unused connector and skill lists ride in every
     start context.
6. **The tests start git processes on Windows, on a saturated laptop, and the build added load.** Nine
   projects run side by side, and a git call took 10 to 15 times as long as alone (D237). Agents ran
   about 37 whole-project runs, several at once, beside the lead's full runs. Waits broke under that
   load, and today's full run failed on load again.
7. **Tests assume things about the machine, and the lessons are not enforced.** 17 of the 21 CI causes
   were of this kind: git versions, macOS's `/var` and late watcher, wall-clock bounds, missing or
   short waits. They recur since phase 3, and 111 short fixed waits are still in the tests.
8. **Gate rules and untried real-window code made extra rounds.** The real-window code never ran
   before the gate. Fixes to the real-window project alone re-ran the 53 to 56 minute default run, and
   rounds 3 and 4 cost about 3.5 h.
9. **Build output copies the same native files into every output folder.** That is 8.7 GB of the
   9.2 GB of `bin`, and 5.8 to 7.0 GB per built worktree. Agents' scratch tools carry full copies
   (1.6 GB in phase 6). Nothing removes old gate folders, CI downloads or scratch.
10. **The lead keeps a large context:** $293 (11%), with a mean of 528k per call. It compacts only near
    the 1M window.
11. **One model at the highest effort for everything.** Every call was Opus 5.5 at xhigh, including
    $113 of mechanical agents, which CLAUDE.md's Models rule gives to Sonnet 5.5. What the effort level
    costs was not measured.
12. **The laptop itself.** Memory commit is near its limit with no test running: a driver host holds
    9.5 GB, and the page file sits on C:. C:'s swings come from other software.

## 4. Proposals

Ranked by saving against risk. IDs: T tokens, S tests, R rounds, D disk, P process. N marks a
proposal the skeptics added. Where two areas proposed the same thing, the entries are merged
(T2/P1). Savings are per phase of phase 6's size, with the skeptics' corrections. "Owner" says
whether the owner must decide; anything that changes how a phase is built or gated, the tooling, or
a recorded decision needs the owner's yes.

| # | Id | Proposal | What it changes | Saves | Risks | Owner |
|---|---|---|---|---|---|---|
| 1 | D1 | Trim the native assets in local builds | A new `Directory.Build.targets` at the root (below); one line each in CLAUDE.md, `architecture.md` and plan's phase 8; a decision; a CI run | Per worktree 6.985 → 0.658 GB (measured); main tree about 6.97 → 0.66 GB on its next build (computed); 3 worktrees add about 2 GB instead of 17-21 GB | Real window and CI not yet run on it; a portable `dotnet publish` on Windows would carry only win-x64; Skia's native symbols gone (both can be switched back) | Yes |
| 2 | T3 | One-hour prompt cache for sub-agents | Claude Code setting `"subagentPromptCacheTtl": "1h"` in the owner's user settings on the Windows machine | $229 (8.4%), simulated on phase 6's agent calls | How the plan counts one-hour writes is not known; in the repo's settings it would also apply to cloud and API-key sessions | Yes |
| 3 | T11/P9 | Split phase 7; start a phase or stage only with enough of the week left | `docs/plan.md`: 7a and 7b (below); Workflow: read the week's used share before a phase or stage | Avoids a stop like phase 6's 74 h; a second gate and closing cost about $250-300 and a gate's machine time | 7a's provider model may change for 7b; the allowance is known from one sample only | Yes |
| 4 | T2/P1 | A self-contained brief per agent, and bounded agents | Workflow: the design's section for a package (≤ 25 KB) is its builder's brief; about 150 calls or 300k context per agent, then a note and a fresh agent; 5-6 findings per fixer | $200-300 (estimate; overlaps T3) | A brief that misses a contract surfaces in review; a hand-off without a brief costs $5-10 of re-reading; no per-agent cap exists to enforce it | Yes |
| 5 | S2/N2 | Test classes during the build; one heavy run at a time | Workflow and CLAUDE.md: builders run their classes; whole-project and root runs by the lead only; a wrapper script with a lock; the full run at the gate | Phase 6: the lead's 4 build-time full runs (about 3.7 h) and agents' about 37 whole-project runs (App.Tests 22.6 min, Git.Tests 15.5 min each, clean), and the load failures they caused (total not measured) | Load-only and integration failures move to the gate (the build-time runs found 4); pushed package branches would look like phase work to start step 1 | Yes |
| 6 | N3 | A real-window-only fix re-runs only the real-window pass | CLAUDE.md and the gate: a commit that changes only `tests/VisualCommit.RealWindowTests` re-runs the pass; the phase's sha256 list proves the scripted pictures unchanged | About 1.8 h (phase 6's rounds 3 and 4) | Relaxes "both passes on one commit" | Yes |
| 7 | R3 | Run the new phase's real-window checks twice before the counted round | The gate and closing steps; the owner told first | Probably 1-2 rounds of about 1.75 h, against two mouse takeovers of 5-16 min | Needs the owner present; a race may not show | Yes |
| 8 | P2/T5 | A shorter design; rulings before drafting | Workflow: a design of 150-200 KB (phases 3-5's size); decisions only in `decisions.md`; rulings asked before drafting, so no edit, revise or consolidate rounds | $100-150 (estimate) | Vague contracts move problems to the build; phase 7 needs complete provider and keychain facts; the owner is needed at the start | Yes |
| 9 | P4 | Review the design with lenses chosen by risk; verify blockers and majors only | Workflow: 3-4 lenses, git safety and tests/gate always; one verifier per blocker or major (19 of 93 in phase 6) | $60-80 | Verifiers amended 44% of phase 6's findings, so minors' fixes go unchecked | Yes |
| 10 | T1/P10 | Record the week's usage at each checkpoint | `docs/status.md`: the plan's weekly share (`/usage`, or the status line's seven-day percentage) at each Progress checkpoint; a token-ledger script for after a phase | Nothing directly; makes 3 and any budget work | The ledger reads files outside the repo and must handle both transcript formats | Yes |
| 11 | D2 | Remove the unused Release outputs, once | The main tree: App.Tests, Git.Tests and VisualTests Release and RealWindowTests Debug | 2.27 GB on D: (measured) | None: CI builds its own, and a later Release build rebuilds them | Yes |
| 12 | D7 | Clear NuGet's http cache, once | `dotnet nuget locals http-cache --clear` | 2.76 GB on C: (measured; the project's share is 0.31 GB) | All .NET projects; a package missing from the global folder is downloaded again | Yes |
| 13 | D5 | Trim agents' scratch .NET tools too | A `Directory.Build.targets` in `%TEMP%\claude\d--Projects-Personal-Visual-Git\` that imports the repo's file | 542.8 → 20.1 MB per tool (measured); phase 6's 1.6 GB of tools → about 60 MB (computed) | Lives outside the repo; needs D1 | Yes |
| 14 | T9 | Keep the lead's context lean | Practice: compact at stage boundaries (`/compact`, or the `autoCompactWindow` setting); long output into files; long loops to short agents | $40-70 | Re-reading after a compaction eats part of it; details must be in `docs/status.md`'s Notes | Practice: no; a committed setting: yes |
| 15 | N1 | Turn off unused claude.ai connectors in this project's sessions | The owner's account or Claude Code settings | About $40-50 (estimate): a 27k-character list of about 540 connector tools and an 18k-character skill list in every agent's start | Those connectors are gone from these sessions; the switch (`ENABLE_CLAUDEAI_MCP_SERVERS`) has not been tested | Yes |
| 16 | S1 | No full local run at every session start | CLAUDE.md start step 6: build, run the changed areas' classes, read CI for the commit; a full run only when the machine changed (git, SDK, Windows update) or the tree has unpushed code | About 55 min of laptop time per session start (one in phase 6) | The laptop's own state (git 2.36, its load) shows first at the gate | Yes |
| 17 | T10/P7 | A slimmer CLAUDE.md | Move procedures (cloud sessions, the commands' notes, closing detail) into docs read when needed, with pointers; the owner's rules stay | About $40 | A moved procedure is missed; parts proposed for moving are rules (identity, cloud turn-end, acceptance) and must stay | Yes |
| 18 | T6/P3 | Write each part's checks just before its package | The gate's step 1: per part, still committed before its UI; one writer, one reviewer; shared conventions ≤ 30 KB (phase 6: 291 KB) | $60-100 | Changes "at the start of the phase"; checks less independent of the design; serializes ahead of parallel builds | Yes |
| 19 | T8/P12 | Sonnet for mechanical agents; try a lower effort | Name the model on every agent; Sonnet for reading CI, listing, running commands and reporting; one wave at effort high against xhigh | $30-60; effort's effect not measured | Sonnet's price and its separate weekly limit are unclear; writing or changing docs stays Opus | Effort: yes |
| 20 | R4 | Run changed git-facing test classes on Git 2.56 automatically | A `scripts/test-with-git` script, MinGit 2.56 kept outside `%TEMP%\claude`; design research checks facts on 2.36, 2.43, 2.55 and 2.56 | 2-4 red CI runs (corrected from 5) | Forgotten unless mechanical; if the laptop moves to 2.56, nothing tests 2.36 | Yes |
| 21 | R9 | The newest git in a Linux CI job | Upgrade git in ubuntu-latest, or a sixth job | Waiting: a 2.56 failure in 6-8 min instead of 22-45 (3 causes, 4 red runs in phase 6) | Not checked whether the PPA ships 2.56; loses 2.55, or adds a job | Yes |
| 22 | T7/P11 | Smaller inspector batches; triage only with the owner's yes | Split the big batches (one inspector held 422 pictures); optionally skip pictures whose change a rule explains | $20-30 by splitting; $30-60 with triage | Triage conflicts with "every one inspected" (CLAUDE.md, the owner's preference) | Splitting: no; triage: yes |
| 23 | S6 | Scenario templates shared across processes and kept between runs | Testing: each template built once under `%TEMP%\VisualCommit.Tests\shared\`, keyed by its sources, the git version and the tests' git environment; a lock and an atomic rename | About 57 of 75 template builds per run, roughly 1-2 min, and no waits for a template at process start | A stale template if the key misses an input; races between processes | Yes |
| 24 | S7 | Cheaper scenario copies | `TempRepo`: record the remote URLs when the template is built | 676 of Git.Tests' 22,892 git starts (3%), roughly 0.5-1 min | Low | Yes |
| 25 | R1/R6 | A guard test for the recurring test faults | A fast test that scans every test project's sources for short fixed waits, `Thread.Sleep`, wall-clock asserts, the OS standing in for a machine property, exact git-call counts with a live watcher; an allow-list with reasons; one wait helper bounded by the watchdog | Not measured; 17 of phase 6's 21 CI causes were of these kinds, recurring since phase 3 | Keeping the allow-list; some hits are the behaviour under test | Yes |
| 26 | R10 | A ledger of failures | `docs/status.md`'s CI section: one short line per CI or default-run failure; a test that fails twice is a bug before the gate | Small (the Wheel test's first failure went unrecorded) | A little writing | Yes |
| 27 | D6 | At acceptance, remove the older gate folders and the CI downloads | The handoff template or D3: after a merge, remove `gate<N-1>-*` and CI downloads; keep `gate<N>` until the next gate has made its reference | 0.55 GB on D: now; about 0.68 GB per phase | Removed too early, a single real-window run that empties its folder would leave the last gate's pictures nowhere | Yes |
| 28 | D3 | A clean-up script, after the skeptic's five fixes | `scripts/clean-disk.py`: lists by default; `--remove <group>` with a yes per group | Beyond D1, D2 and D7: about 2.9 GB of old agent scratch on C: and 0.56 GB on D: | It deletes; the draft failed on git's read-only files and marked a working agent's worktree removable | Yes |
| 29 | S5 | CI split into shards | `ci.yml`: Git.Tests, App.Tests and the walk-throughs as separate jobs per platform | CI wait per push about 32 → 16-20 min on Windows (estimate) | Queueing on the free plan's limits (5 macOS jobs at once); more jobs to read; artifact names change; reopens D134 and D232 | Yes |
| 30 | R7 | Triage CI failures in small fresh agents | Sonnet fetches and extracts the logs; an Opus fixer gets the failing test and its log; the lead rules and merges | About 90 M raw lead tokens, about 10 M weighted by price | Classifying is judgment (Opus); coordination | Yes |
| 31 | S10 | Measure a cap on side-by-side work | One full run with `--max-parallel-test-modules 5` (and Git.Tests at 4 threads on the command line), on a quiet laptop | Not measured: fewer load failures expected | One 55-minute run; the same setting in `xunit.runner.json` would change macOS CI | Yes |
| 32 | D4 | Rules for worktrees | Workflow: build and test only the touched projects, the solution once before the merge; no Release in worktrees; risk 27's numbers updated | After D1, about 0.4 GB per worktree; build time not measured | Another project's tests can break unseen until the gate | Yes |
| 33 | N4 | Free the laptop's leaked memory | The machine: a reboot; watch `WUDFHost` | Not measured; memory was 34.4 of 38.8 GB committed with no test running | None to the project | Yes |
| 34 | S11 | Linux for the inner loop, as an option | A cloud session, or WSL2 on the laptop | CI's Linux jobs run the suite in 4-6 min; a WSL2 run not measured | WSL2 takes 5-10 GB on C:; Windows-only behaviour found later | Yes |
| 35 | D8 | Shorter transcript retention, after this review | `cleanupPeriodDays` in the owner's Claude Code settings | 566 MB now at 14 days; 2,265 MB at 7 | Later cost reviews lose their data | Yes |
| 36 | R8 | Imitate macOS's late watcher on Windows | A test switch that delays the watcher's events by 1-2 s; app code, so in phase 7 | Group C: 4 red macOS attempts in phase 6 | The app gains a test switch; a rough imitation | Yes |

### Notes on the leading proposals

- **D1.** The file below is the experiment's file (`<scratch>/disk-exp/proposed-Directory.Build.targets`)
  without its comments. Both switches default to false.
  - `VisualCommitNativeSymbols=true` copies the native `.pdb` files again.
  - `VisualCommitAllRuntimes=true` copies every platform's libraries again, as a portable publish
    needs. Phase 8 must know this.
  - The removal runs before the copy and before the `.deps.json` is written.
  - No code, test or script depends on the `runtimes/` layout ([D-sk]).
  - If CI's Windows job or the next real-window pass shows any trouble with part (b), part (a) alone is
    the fallback: no platform logic, 51% saved.
  - Measure the main tree before and after: output size, and free space on D:.

  ```xml
  <Project>
    <PropertyGroup>
      <VisualCommitNativeSymbols Condition="'$(VisualCommitNativeSymbols)' == ''">false</VisualCommitNativeSymbols>
      <VisualCommitAllRuntimes Condition="'$(VisualCommitAllRuntimes)' == ''">false</VisualCommitAllRuntimes>
    </PropertyGroup>
    <Target Name="VisualCommitTrimNativeAssets" AfterTargets="ResolvePackageAssets">
      <ItemGroup Condition="'$(VisualCommitNativeSymbols)' != 'true'">
        <RuntimeTargetsCopyLocalItems Remove="@(RuntimeTargetsCopyLocalItems)" Condition="'%(RuntimeTargetsCopyLocalItems.Extension)' == '.pdb'" />
        <NativeCopyLocalItems Remove="@(NativeCopyLocalItems)" Condition="'%(NativeCopyLocalItems.Extension)' == '.pdb'" />
      </ItemGroup>
      <ItemGroup Condition="'$(VisualCommitAllRuntimes)' != 'true' and '$(RuntimeIdentifier)' == '' and $([MSBuild]::IsOSPlatform('Windows'))">
        <RuntimeTargetsCopyLocalItems Remove="@(RuntimeTargetsCopyLocalItems)"
                                      Condition="'%(RuntimeTargetsCopyLocalItems.RuntimeIdentifier)' != '$(NETCoreSdkPortableRuntimeIdentifier)' and '%(RuntimeTargetsCopyLocalItems.RuntimeIdentifier)' != 'win' and '%(RuntimeTargetsCopyLocalItems.RuntimeIdentifier)' != 'any'" />
      </ItemGroup>
    </Target>
  </Project>
  ```

- **T3.** Claude Code 2.1.310's settings schema has `subagentPromptCacheTtl` ("5m" or "1h"). The
  environment variable `CLAUDE_CODE_SUBAGENT_PROMPT_CACHE_TTL` takes precedence over it.
  - The skeptic replayed phase 6's agent calls with gaps of 5 to 60 minutes as cache reads and every
    agent write priced at $8. The agents' cache cost fell from $2,057 to $1,828 ([T-sk]
    `skeptic-TP/ttl.py`).
  - With the setting on, rearranging waits for cache reasons is no longer needed.
  - Put it in the user settings on the Windows machine, not in the repo.
- **T11/P9.**
  - **7a, "Hosting foundation and GitHub" (M):** the provider model; tokens in the OS keychain on
    Windows, macOS and Linux; sign-in with a personal access token (GitHub's device flow only if the
    owner registers an app, risk 5); browse and clone; the pull-request list, create from the branch
    menus and the drop menu, the badge, open in the browser; avatars; and all 6 carried items. Tested
    against a local fake GitHub API; real accounts by hand.
  - **7b, "Azure DevOps and GitLab" (M):** both providers on 7a's model, self-hosted GitLab included,
    with checks that reuse 7a's screens once per provider. If its design outgrows a week, it is split
    per provider.
  - **The budget** is checked against the plan's own weekly percentage, not against API dollars: a
    $1,500 phase still stops if it starts late in a week.
  - **Before 7a's design:** decide risks 4 and 5 (test accounts, app registrations).
- **T2/P1.** The designer writes each package's section of the design as the brief. That is cheaper
  than the lead writing 15 briefs, and it is the direct fix for orientation reads of 306k to 557k.
  Without briefs, splitting agents only repeats the orientation. Claude Code has an `autoCompactWindow`
  setting (100k to 1M), but whether it applies to sub-agents was not checked.
- **S2/N2.** The heavy test runs of phase 6 were mostly agents' whole-project runs in worktrees, not
  the lead's. The lock (one whole-project or root run on the laptop at a time) addresses time and
  load failures together.

### Proposals the skeptics rejected

- **T4, design and report as small files per package.** Agents already read the design by section
  (903 Reads, 1 without a limit), so splitting the file alone does not cut what they read. The saving
  belongs to P2/T5 and T2/P1. 1,595 code lines cite the designs by path.
- **T5 as stated** (at most 5 research topics, a design of at most $200, saving $300). The target and
  the saving are not supported, and phase 7 needs research on three providers and three keychains.
  Its sound parts are in P2/T5 and P4.
- **T7 as stated** ($70 per gate round). Phase 6's whole inspection was $118 to $137 over all rounds,
  and the re-inspection round about $7. The sha256 and comparison steps are already practice.
  Narrowed into T7/P11.
- **P5, a new lead session at each stage.** Each new session reads about 320k tokens of start
  reading, so this can cost more than it saves. T9 does the job inside one session.
- **P6, start reading by index, `architecture.md` split by area.** It saves under $30 per phase now,
  because phase 6 had one lead session, and it is a large rewrite that can drift. Its value is
  stopping the growth of about 40% per phase; reconsider it if the start reading keeps growing.
- **P8, finished phases' docs moved to an archive.** The saving was not measured. 1,595 lines in 851
  `src`, `tests` and `scripts` files cite these paths, and so do the sha256 lists and the scripts. An
  untested cheaper idea is a ripgrep ignore file.
- **P12 as stated.** Applying amendments, correcting docs from findings and the cold reader's fixes are
  doc changes, which the Models rule gives to Opus. The rest is in T8/P12.
- **S3, a quick command without the cross-checks.** The parity tests test the app's own operations.
  The saving is about 4 minutes of a 15.5-minute run, not 9 of 34, and S2 mostly makes it redundant.
- **S4, earlier phases' walk-throughs out of local runs.** With S1 and S2, the only local full run
  left is the gate's, which must keep them. It changes gate rule 6.
- **S8, git-call budgets and batched config reads.** This is app code, which belongs in a phase. The
  5 to 8% was not measured, and exact counts with a live watcher failed twice on macOS (C18). Suggested
  for phase 7a's first package, as upper bounds taken with the watcher quiet.
- **S9, App.Tests' headless chain split in two.** Its estimate came from a disturbed run: App.Tests
  alone takes 22.6 min, not 41. Re-measure before deciding.
- **S12, timing tests with git's trace.** The trace doubles a git-heavy test's time. Count git starts
  from the trace, and time an untraced run.
- **S13, the git launcher.** There is no local saving, since the gate's runs from Git Bash already use
  `mingw64\bin\git.exe`. Pointing the harness at it would drop the path users run. Only one step is
  worth keeping: printing `where git` in CI.
- **S14, a time budget for phase 7's tests.** The saving is not supported, and shared app launches
  conflict with D181. A per-check budget in git starts is the cheap form.
- **R1 as stated** (every short wait raised to 60 s). D237 already removed its claimed saving, waits of
  60 s also failed under self-made load, and App.Tests' 65 short waits were left out. Narrowed into
  R1/R6.
- **R2, a canonical test root.** Only 1 red run (C12) was test-side; C16 was a real app defect, which
  a canonical root would hide. Moving the root to D: loses the Defender exclusion.
- **R5, wait-by-default lookups now.** The Wheel fix is already in phase 7's plan, and the Phase 2
  sleep never failed. Confirming a folder picker with Enter may navigate instead of choosing (not
  verified).
- **R6 as stated** (a rules page checked in review). The handoffs already held these lessons, and the
  step 22 lens missed S19. Replaced by the guard test, R1/R6.
- **R11, pixel values left to be measured at build time.** The 20 corrections caused no reruns, and
  the change weakens gate rule 1.
- **R12, push discipline.** The saving is small, and CLAUDE.md already batches pushes.
- **D3 as drafted.** Five defects:
  1. `shutil.rmtree` fails on git's read-only files.
  2. It counts a working agent's fresh worktree as merged and clean.
  3. `--yes` lets an agent skip the owner's yes.
  4. Nothing guards the current gate's CI downloads.
  5. The stale-runtimes group means anything only after D1.

  It is in the table only in its corrected form.

## 5. What to do first, and what to try during phase 7

**Before phase 7**, on the owner's yes, in this order:

1. **D1.** The build trim, then one root build and one real-window build in the main tree. Measure the
   output size and the free space before and after, and read CI's run on `master`.
2. **D2, D7 and D6 now.** Remove `gate5-*` and the CI download `ci-40027897278`, 0.55 GB. Each item is
   listed with its size first.
3. **D5.** The targets file above the agents' scratch folders.
4. **T3.** One-hour caches for sub-agents, in the user settings.
5. **N1.** Unused connectors off, after checking that the switch works.
6. **T11/P9 and T1/P10.** Split phase 7 in `docs/plan.md`, decide risks 4 and 5, and add a weekly-share
   line to `docs/status.md`'s checkpoints.
7. **T2/P1, P2/T5 and P4.** Workflow changes that shape 7a's design: briefs, bounded agents, a shorter
   design, a lighter review.
8. **S2/N2 and S1.** Test classes during the build, the lock script, and no full run at a session
   start without a reason.
9. **N3 and R3.** The two gate rules for real-window rounds.
10. **T9.** A lean lead, as practice.
11. **Re-measure the default run once, on a quiet laptop,** as the "before" number. Today's runs were
    disturbed. Then delete `<scratch>` (this review's files).

**During phase 7**, tried and measured with T1/P10:

- **T6/P3** for 7a's checks, if the owner agrees.
- **T8/P12.** Sonnet where the Models rule already says so, and one wave at effort high against xhigh.
- **T7/P11.** Smaller inspector batches at 7a's gate.
- **T10/P7.** A slimmer CLAUDE.md, if the owner agrees.
- **R4, R9, R1/R6 and R10.** The 2.56 checks, the guard test and the failure ledger, from the first
  CI run.
- **S6, S7 and S10.** As tooling changes, with S10's one measuring run on a quiet laptop.
- **In 7a's first package** (app code): S8's git-call budgets as upper bounds, R8's watcher lag, the
  Wheel fix already planned, and a look at W1's failure.
- **D3, D4, S5 and R7.** D3 corrected, D4 in 7a's work breakdown, S5 and R7 if CI waits and triage
  still hold up the work.
- **N4, S11 and D8.** Left to the owner.

## 6. Open questions for the owner

1. **How do the plan's limits count tokens?** In particular cache reads, cache writes and output, and
   Sonnet against Opus: `/usage` shows a separate "Current week (Sonnet only)". Claude Code's records
   price Sonnet 5.5's cache read at $0.10, and Anthropic's table at $0.20, the same as Opus. Every $
   saving here is an API-price equivalent.
2. **Paid extra usage.** Phase 5 reached the monthly spend cap on 10-13. Keep the cap as it is, change
   it, or use no extra usage?
3. **Phase 7.** Two phases (7a hosting foundation, GitHub and the carried items; 7b Azure DevOps and
   GitLab) or three (one per provider)? Which test accounts and app registrations (risks 4 and 5) will
   exist before 7a's design?
4. **Expected results per part.** May they be written per part, just before that part's package and
   still before its UI (T6/P3), instead of all at the phase's start?
5. **The gate's real-window rounds.** May a commit that changes only the real-window project re-run
   only the real-window pass (N3)? And may the new phase's real-window checks run twice before the
   counted round, with the owner present (R3)?
6. **Picture inspection.** Split batches only, or also skip pictures whose change a rule explains
   (T7/P11)? Skipping conflicts with "every one inspected".
7. **Session start.** Read CI's result instead of a full local run (S1)?
8. **The laptop's default git.** Stay on 2.36, or move to 2.56 with 2.36 tested by script (R4)? The
   app's minimum is 2.30.
9. **Settings.** One-hour sub-agent caches (T3) and unused connectors off (N1), in the user settings
   on the Windows machine?
10. **CI shards (S5).** Shorter CI waits at the cost of more jobs to read, which reopens D134 and
    D232?
11. **W1.** `Phase6ChecksW.Check_W1` failed once under load today ("read 7 repositories" where 8 were
    expected), and it was not rerun. Look at it before phase 7, or in 7a's first package?
12. **The machine.**
    - A reboot to free `WUDFHost`'s 9.5 GB?
    - The 22 GB page file on C:?
    - The non-project space on C: (`vm_bundles` 10.1 GB, old .NET SDKs, `%TEMP%\hpnxoufx` 2.4 GB)?
    - How long to keep transcripts (D8)?
