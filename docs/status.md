# Status

Last updated: 2026-11-01

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git, a public repository. The local
folder on the development machine is still named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Done | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
| 1. Repos and commit graph | Done | `phase/1-graph` | [phase-1](handoffs/phase-1.md) | [phase-1](test-reports/phase-1.md) |
| 2. Diff and commit workflow | Done | `phase/2-diff-commit` | [phase-2](handoffs/phase-2.md) | [phase-2](test-reports/phase-2.md) |
| 3. Branch, remote, stash and tag operations | Done | `phase/3-operations` | [phase-3](handoffs/phase-3.md) | [phase-3](test-reports/phase-3.md) |
| 4. Rebase and conflict resolution | Done | `phase/4-rebase-conflicts` | [phase-4](handoffs/phase-4.md) | [phase-4](test-reports/phase-4.md) |
| 5. History, traceability and undo | Done | `phase/5-history` | [phase-5](handoffs/phase-5.md) | [phase-5](test-reports/phase-5.md) |
| 6. Multi-repo and productivity | Done | `phase/6-multi-repo` | [phase-6](handoffs/phase-6.md) | [phase-6](test-reports/phase-6.md) |
| 7a. Hosting foundation and GitHub | Done | `phase/7a-hosting-github` | [phase-7a](handoffs/phase-7a.md) | [phase-7a](test-reports/phase-7a.md) |
| 7b. Azure DevOps and GitLab | In progress | `phase/7b-hosting-azure-gitlab` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 7b, Azure DevOps and GitLab: in progress since 2026-10-31, on `phase/7b-hosting-azure-gitlab`,
made from `master` at `a29379f`. Its design is `docs/phase-7b-design.md`, its report
`docs/test-reports/phase-7b.md`.

Phase 7a was accepted by the owner on 2026-10-31 and merged into `master` (merge
commit `019e68a`); its gate ran on `a2c312a`. Phase 6 was accepted on 2026-10-24 (merge commit
`3f34c87`), phase 5 on 2026-10-17 (merge commit `c299939`), phase 4 on 2026-10-12 (merge commit
`ce5f337`), phase 3 on 2026-10-11 (merge commit `8b33745`), phase 2 on 2026-10-10 (merge commit
`e039070`), phase 1 on 2026-10-10 (merge commit `b5eb1ed`) and phase 0 on 2026-10-06 (merge commit
`b5f4723`). Phase 7b is the second half of phase 7 (D241).

Gate commit: none yet. When a phase is set to "Awaiting acceptance", this line names the commit
that both passes of the gate ran on (or, by D246, the real-window pass's commit when it changes
only `tests/VisualCommit.RealWindowTests` after the commit the default run ran on); "Phase N accepted" checks that
nothing but docs changed after it.

### Progress

Each item is ticked in the commit that completes it, and each tick, like the start of each stage,
records the share of the plan's weekly allowance used at that point (D241). Until the reset of
Thursday 2026-11-05 at 2:00 the stages started without that check (D344); for the week from then the
owner gives the phase about all of it (2026-11-05), and a checkpoint records the share the owner
gives, or "share not read". The numbers are plain step numbers, not requirement
numbers. The design (step 3) splits the steps below into work packages and says how they map onto
them. Phase 7a's checklist, with a note on each step, is in this file as of `019e68a`'s second
parent (`git show 3a3cc71:docs/status.md`).

Start:

- [x] 1. Phase branch created from `master` at `a29379f`, phase set to "In progress", this
  checklist; the owner's answer to D346's question recorded (D352: the Azure DevOps organization
  holds other work, and the owner accepts the token's wider reach). The owner's answers before the
  design were given before the start (D344 to D351). No baseline run (D245: the machine has not
  changed since 7a's gate, and the tree held no unpushed code); CI's run 41243213784 on the merge
  stands for it, and `dotnet build` passed on `a29379f` (1 min 38 s). (2026-10-31. Week: 50 % used.)

Design:

- [x] 2. Research, each fact taken where it lives: Azure DevOps' and GitLab's REST APIs (sign-in's
  check of a token, an organization's or account's repositories, a repository's pull requests or
  merge requests, creating one, their web pages, errors, paging, versions), their token pages,
  kinds and scopes (D345), their remotes' URL forms (D347), and git 2.56's credential facts for
  them (dev.azure.com's `<organization>@` user, a binding per organization on a shared host, each
  provider's user name; D350), with 2.43 from CI where versions differ (D248); 7a's code mapped
  where it names GitHub. Questions of taste the research raises go to the owner before drafting
  (D243). Done 2026-10-31 by one workflow of 12 agents: seven notes, a critic and four gap rounds
  (r1 to r8 in `D:/Projects/Personal/phase7b-work/research/`, outside the repository, about 660
  KB); git 2.31.1 and 2.43.0 (MinGit, hashes checked) were run beside 2.56 for the credential
  facts, so no CI step was needed for them; only anonymous requests went to the two services.
- [x] 3. Design in `docs/phase-7b-design.md` (D243: about 150 to 200 KB, its decisions only in
  `decisions.md`): the provider model for three providers, both providers' clients and fakes, the
  credential path for their hosts, the texts and layouts that name the provider (D351), the live
  command's extension and its sandbox guard (D346, D352), how the code and the harness name 7b, its
  lagged list (D262), an estimate of what the phase adds to the default run (1 h 12 min 18 s at
  7a's gate) and to the real-window pass (31 min 6 s), and the work packages, each section a
  builder's brief of at most about 25 KB (D242). Whether 7b is split per provider is judged here
  (`plan.md`'s phase 7b). Its review: 3 or 4 lenses, git safety and tests/gate among them, a
  verifier only for each blocker and major; its decisions recorded.
  Done 2026-11-01: drafted by run `wf_4982af69-706` (an architect, nine drafters, an integrator;
  4.55M tokens; D357 to D372), reviewed by run `wf_1792f36a-eb9` (lenses git, token, tests and
  conform, a verifier for each blocker and major; 4.52M tokens: no blocker, 14 majors, 42 minors,
  none refuted), fixed by runs `wf_1371fd83-9fa` (a fixer per section; 3.33M), `wf_0602650a-596`
  (six checkers, 45 residual edits; 2.84M) and `wf_388f074f-240` (two checkers, 12 more; 0.82M).
  The owner's answers of 2026-11-01 are D373 to D376 and D367's amendment of D252 and D349; the
  review's own rows D377 (CI limits) and D378 (another provider's token refused locally). One
  phase, not split (D365). The design is 274.6 KB against D243's 150 to 200: two providers'
  contracts and the review's fixes, accepted by the lead; no package section is over 25 KB
  (section 15, 24.8 KB, the largest), section 1, WP0's brief, is 49.7 KB. Drafts, findings and
  the fix stage's files are in `D:/Projects/Personal/phase7b-work/` (`design/`, `review/`,
  `fix/`). (Week: under 80 % used.)

Build:

- [x] 4. First package: what the design gives it, with `scripts/heavy-run.sh`'s header comment
  (still 55 minutes; the 7a handoff's Known issues) and the contracts and stubs the packages build
  on. Done 2026-11-01: L1 (`a88b84d`, pushed; CI run 41277720820 green in all five jobs, Test
  steps `windows-latest` 27 min 30 s and `macos-latest` 22 min 35 s of 70, `ubuntu-latest` 4 min
  32 s, the containers 6 min 0 s and 5 min 5 s of 14) and WP0 (`e8a275d`, section 1's list,
  1.10). The review of WP0 found no blocker or major and three minors, all fixed in the commit
  that ticks this step: `HostingOptions`' warning takes the names from `HostingProviders`; the
  three 7a tests whose inputs changed are recorded in the design's 1.10, "As built"; this
  checklist and "Next step". The root build and the live project's Release build have no
  warnings, and the classes WP0 touched pass (Notes). WP0 was pushed on 2026-11-01 (`e3a8999`).
  (2026-11-01. Week: share not read.)
- [x] 5. Harness: 7b's walk-through project, the fakes of Azure DevOps' and GitLab's APIs (their
  ports amending D97, as D265 did), the timing script's 7b case and CLAUDE.md's Commands, the
  real-window project's link to 7b's expectations. WP-F (the fakes' shared core, the walk-through
  project, the timing script's case, the real-window project's link, section 2) merged at
  `564dad4` and its CLAUDE.md rows at `900ad1a`; WP-FL (the fake of GitLab, section 4) merged at
  `7780908` and WP-FA (the fake of Azure DevOps, section 3) at `e995fc4`, each with its review's
  fixes. (2026-11-01. Week: share not read.)
- [x] 6. The provider model for three providers: one account per provider (D348), Azure DevOps'
  and GitLab's remotes recognised from their URLs (D347), their tokens in the OS keychain (R3).
  Tests. Recognition is WP-AZ's and WP-GL's (merged, steps 7 and 8); the session's accounts and
  keychain entries per provider are WP-S's, merged at `bdc92c3` (section 9; D383), its lagged list
  and whole default run passed (Notes), CI green (CI section). (2026-11-02. Week: share not read.)
- [x] 7. Azure DevOps' API: a token checked at sign-in, the organization's repositories, a
  repository's pull requests, creating one; against its fake. Tests. WP-AZ (Azure DevOps' client and
  provider, section 6) merged at `767d9b9` on WP-H's core (`3591dc4`) and WP-FA's fake (`e995fc4`),
  with its reviews', hunts' and the lead's fixes, and at its merge (`df365e5` to `b65d0d9`) the
  request's token on its own failures (WP-GL's R1), one warning per failure at A1.
  (2026-11-01. Week: share not read.)
- [x] 8. GitLab's API: a token of either kind checked at sign-in (D345), the account's projects, a
  project's merge requests, creating one; against its fake. Tests. WP-GL (GitLab's client and
  provider, section 7) merged at `e8a5abf`, with its reviews', hunts' and the lead's fixes; the
  core's `MalformedAnswer` and `Failed` take the request's token since (section 7's As built).
  (2026-11-01. Week: share not read.)
- [x] 9. The account's token on the app's git calls to the remotes it covers (D350): gitlab.com's,
  and Azure DevOps' signed-in organization's alone; the push guard and the prompt group for the new
  hosts, as the design decides. Tests. WP-C (`Select` with a coverage, the push guard
  and the prompts, section 8) merged at `6586066`, with wave 1's adversarial fixes (D379, D381);
  the session's union of the coverages is WP-S's, merged at `bdc92c3` (section 9, wave 3); an
  operation started inside D290's window binds once the URL rules are known (D388, WP-N's merge),
  and D278's test reads the URL's user by the measured git (D389, `98cdd86`; plan risk 43). Their
  classes green in every job of CI 42454621322 on `d127fef` (CI section). (2026-11-08. Week: share
  not read.)
- [x] 10. Sign-in dialog and the Accounts section with an account of each provider (D345, D348).
  WP-A (section 10) merged at `b6de3cf`, with its reviews', hunts' and the lead's fixes (D387); its
  merge's classes and lagged list passed and its pictures were inspected (Notes). CI's run on it
  failed two phase 6 tests on Linux that WP-A does not touch, under investigation (CI section).
  (2026-11-06. Week: share not read.)
- [x] 11. Browse and clone the repositories of an Azure DevOps and a GitLab account.
  WP-B (section 11) merged at `9409aa8`, with its reviews', hunts' and the lead's fixes (B7's owner
  ruled `fernhill-labs`); its merge's classes and lagged list passed, 11.8's comparison found only
  what varies by nature, and its pictures were inspected (Notes). CI's run on it failed three tests,
  under investigation (CI section). (2026-11-06. Week: share not read.)
- [x] 12. The left panel's section for each provider (GitLab's merge requests, D351), its menu,
  checkout, the badge on branches, open in browser.
  WP-P (section 12) merged at `1821206`, with its reviews', hunts' and the lead's fixes (`610b05a`:
  the ruling on the two cut message rows, recorded in the report at `c5ba918`); its merge's runs
  passed but phase 2's `Check_15_amend` once under the lag, a race in the check, fixed and merged
  at `26b44c7`; 12.9's comparisons found only what varies by nature; part P's 22 pictures and `a06c`
  inspected; CI 42373090316 green (Notes). (2026-11-07. Week: share not read.)
- [x] 13. Create a pull request or merge request from the branch menus and the drop menu, and its
  form, for each provider.
  WP-N (section 13) merged at `a6952d2`, with its reviews', hunts' and the lead's fixes (D388);
  13.9's runs passed once P11's budget was fixed (D390, `98cdd86`), 13.9's comparison found only
  what varies by nature, and part N's 22 pictures were inspected (Notes, "WP-N's merge runs"); CI
  42454621322 on `d127fef` green but phase 6's open palette flake (CI section). (2026-11-08. Week:
  share not read.)
- [x] 14. 7a's screens and commands that name GitHub name the provider they act on (D351); the
  earlier checks whose pictures change updated in the same commits, under "Changes to expected
  results". Built by WP-A, WP-B, WP-P and WP-N for their surfaces and WP-C for the credential
  texts, all merged; each merge's comparison with 0.7's references found only what varies by
  nature or the report's recorded changes (Notes). (2026-11-08. Week: share not read.)

Checks, live trial and review:

- [x] 15. Each part's visual checks written into `docs/test-reports/phase-7b.md` just before its
  package is built (D244), by one writer and one reviewer, each marked for the real-window pass or
  not, committed before the UI they test. Parts A (`f4ae515`), B (`37b3ea0`), P (`f1ac5ac`) and N (`28ec097`,
  merged at `5561548`).
- [x] 16. The owner's sandboxes and tokens for Azure DevOps and GitLab made as the design says
  (D346, D352), and the live command extended to both, run with them; what it finds goes into the
  fakes (D342). Under way: the sandboxes are made (2026-11-02; their first tokens replaced, Waiting
  on the owner); stage 1 (15.1) merged at `b377aa3`, built, reviewed and hunted, offline classes
  404 of 404; its first run with the tokens (2026-11-05) passed 8 of 9 (Notes, D385), and GitLab's
  again with a new token (2026-11-06) 4 of 4, so stage 1's 9 tests have passed; WP-LF (0.4), which
  put what they found into the fakes, merged at `22fad1b` (Notes); stage 2 (`Check_3`, 15.6) merged
  at `fdb2c05`, its offline classes 428 of 428 there, and run with the tokens on that commit
  (2026-11-08): 11 of 11, `Check_3` passed on both providers, nothing new for the fakes (Notes,
  "WP-LIVE's second stage"). The GitLab account lists five projects, not the sandbox alone
  (Waiting on the owner). (2026-11-08. Week: share not read.)
- [x] 17. An independent review of the code before the gate; every confirmed finding fixed with
  tests. Ran on `fdb2c05` (Notes, "The review before the gate"): 9 of 19 findings stood, ruled in
  D391 and D392; the six fixed are merged at `6333de6` with tests that fail without them (Notes,
  "The review's fixes"), their classes pass plain and lagged, design 0.6's timing report is taken
  and no test needs D237's split (Notes, "The lead's runs after the review's fixes"). (2026-11-08.)

Closing (`CLAUDE.md`, "Closing a phase"):

- [ ] 18. The visual test gate: references for the earlier pictures made on `master` if 7b changes
  them in many places (plan risk 32); the live command before the pictures (D335); the scripted
  walk-through's pictures on this machine, then the new real-window checks twice on their own with
  the owner told and present (D246); `dotnet test` and the real-window pass on one commit; the live
  command on the counted round; every picture inspected, in batches of at most about 150 (D247);
  `docs/test-reports/phase-7b.md` completed.
- [ ] 19. `docs/architecture.md` describes what is built.
- [ ] 20. `docs/plan.md`: what 7b did not deliver moved to the phase that owns it; risks updated.
- [ ] 21. `docs/handoffs/phase-7b.md` written from the template.
- [ ] 22. Cold-read check by a fresh agent; every gap fixed.
- [ ] 23. The CI run of the gate's commit read, with each job's result.
- [ ] 24. `docs/status.md`: links, the CI run, the "Gate commit", and "Awaiting acceptance" only if
  the whole gate passed; the owner told what passed and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

Phase 7b's runs on `phase/7b-hosting-azure-gitlab` so far, every job green, none failed a test (each
Test step against four fifths of its limit, 56 of 70 minutes and 11 min 12 s of the containers' 14;
D377): [41277720820](https://github.com/RahimPasha/VisualCommit/actions/runs/41277720820) on L1's
`a88b84d` (step 4); [41282019992](https://github.com/RahimPasha/VisualCommit/actions/runs/41282019992)
on WP0's `e3a8999`: `windows-latest` 37 min 54 s (Test step 35 min 1 s), `macos-latest` 27 min 29 s
(25 min 37 s), `ubuntu-latest` 6 min 32 s (4 min 47 s), the containers 8 min 14 s and 6 min 54 s
(5 min 57 s and 4 min 57 s); and
[41290125478](https://github.com/RahimPasha/VisualCommit/actions/runs/41290125478) on `900ad1a`
(WP-F's merge `564dad4` and the CLAUDE.md rows of design 2.9): Test steps `windows-latest` 20 min 49
s, `macos-latest` 29 min 22 s, `ubuntu-latest` 4 min 4 s (job 5 min 16 s), the containers 4 min 43 s
and 5 min 56 s (jobs 6 min 43 s and 8 min 18 s). The jobs API gives the Windows and macOS jobs of
that run an end before their start (the night of 2026-11-01's clock change; read an hour later, 22
min 59 s and 31 min 44 s).

[41311809889](https://github.com/RahimPasha/VisualCommit/actions/runs/41311809889) on WP-C's merge
`6586066` failed in all five jobs: `HostingLinks.Next`'s relative-link row on Linux, macOS and both
containers (off Windows .NET reads `/p?page=2` as a file address), and five of
`CoverageGitMatchTests`' redirect cases on Windows (2.56.0.windows.2, libcurl 8.22.0) and macOS
(Homebrew's 2.56.0), whose curl keeps another user's name behind the test host's relative 301; both
are in the design's As built for sections 5 and 8. Its Test steps: `windows-latest` 26 min 25 s,
`macos-latest` 22 min 37 s, `ubuntu-latest` 5 min 59 s, the containers 4 min 36 s and 6 min 12 s,
each under four fifths. The fix is `b0151bb`.

[41321215497](https://github.com/RahimPasha/VisualCommit/actions/runs/41321215497) on `b0151bb`:
the two faults are gone (every `HostingLinks` and `CoverageGitMatchTests` row passed on all five),
and four jobs are green; `ubuntu-latest` failed one test of 7a's,
`RepositoryHostingWiringTests.Signed_out_reads_no_rules_…`, which asserted the tab's "listed 4 open
pull requests" line as soon as it saw the list loaded, while the line, posted after the state
(D322), still waited in the dispatcher's queue: a race in the test since 7a, which now waits for the
line as the walk-throughs do. Test steps: `windows-latest` 35 min 32 s, `macos-latest` 23 min 29 s,
`ubuntu-latest` 5 min 13 s, the containers 6 min 11 s and 5 min 51 s, each under four fifths. The
fix is `08fa49b`.

[41328744135](https://github.com/RahimPasha/VisualCommit/actions/runs/41328744135) on `3218919`
(that fix and WP-FL's merge `7780908`): four jobs green, the wiring test among them; `windows-latest`
failed phase 6's check W3, which expected a Folder cell 632.475 px wide in a room of 632 whole: W3
and K10 allowed half a pixel beyond the room, though phase 6's checks say cut exactly when wider and
the cell cuts so (`DashboardViewTests` asserts it exactly on every platform), and the scenario's
random folder name landed within that half pixel. Both now assert exactly. Test steps:
`windows-latest` 30 min 9 s, `macos-latest` 32 min 38 s, `ubuntu-latest` 4 min 28 s, the containers
6 min 34 s and 5 min 2 s, each under four fifths; the keychain steps green. The fix is `ca52c5d`.

[41332176199](https://github.com/RahimPasha/VisualCommit/actions/runs/41332176199) on `ca52c5d`
(that fix and WP-FA's merge `e995fc4`): all five jobs green, none failed a test, the keychain steps
green. Jobs and Test steps: `windows-latest` 41 min 0 s (37 min 49 s), `macos-latest` 32 min 21 s
(30 min 18 s), `ubuntu-latest` 6 min 37 s (5 min 1 s), the containers 6 min 31 s and 7 min 4 s
(4 min 39 s and 4 min 53 s), each under four fifths.

[41349169942](https://github.com/RahimPasha/VisualCommit/actions/runs/41349169942) on `ea66780`
(WP-GL's merge `e8a5abf`): all five jobs green, none failed a test, the keychain steps green. Jobs
and Test steps: `windows-latest` 40 min 20 s (37 min 6 s), `macos-latest` 32 min 49 s (30 min 53
s), `ubuntu-latest` 6 min 5 s (4 min 44 s), the containers 8 min 28 s and 8 min 26 s (6 min 11 s and
6 min 9 s), each under four fifths.

[41354931421](https://github.com/RahimPasha/VisualCommit/actions/runs/41354931421) on `c0af1be`
(WP-AZ's merge `767d9b9` with the lead's `df365e5` to `b65d0d9`, wave 2's last code): all five jobs
green, none failed a test, the keychain steps green. Jobs and Test steps: `windows-latest` 40 min 52 s
(37 min 39 s), `macos-latest` 40 min 21 s (37 min 4 s), `ubuntu-latest` 6 min 22 s (4 min 55 s), the
containers 8 min 48 s and 6 min 18 s (6 min 31 s and 4 min 33 s), each under four fifths.

[41414339419](https://github.com/RahimPasha/VisualCommit/actions/runs/41414339419) on `a924cda`
(WP-S's merge `bdc92c3`): all five jobs green, none failed a test, the keychain steps green. Jobs and
Test steps: `windows-latest` 39 min 11 s (36 min 5 s), `macos-latest` 32 min 42 s (30 min 27 s),
`ubuntu-latest` 5 min 39 s (4 min 20 s), the containers 9 min 26 s and 7 min 50 s (6 min 26 s and
5 min 32 s), each under four fifths.

[41484799303](https://github.com/RahimPasha/VisualCommit/actions/runs/41484799303) on `9b31004`
(WP-LIVE's stage 1, merged at `b377aa3`, and its CLAUDE.md rows): all five jobs green, none failed a
test; Windows' "Build the opt-in projects" green (the live project, with stage 1, compiles there).
Jobs and Test steps: `windows-latest` 38 min 28 s (35 min 25 s), `macos-latest` 43 min 27 s (41 min
7 s), `ubuntu-latest` 6 min 2 s (4 min 38 s), the containers 8 min 27 s and 8 min 24 s (6 min 7 s and
6 min 11 s), each under four fifths (56 and 11 min 12 s).

[42216604663](https://github.com/RahimPasha/VisualCommit/actions/runs/42216604663) on WP-LF's merge
`22fad1b`: four jobs green; `windows-latest` failed one test of 7,566 (7,561 passed, 4 skipped),
`GitRunnerPriorityTests.A_below_normal_priority_call_starts_git_below_normal_on_windows`, after 60 s:
"Processes git started run at another priority: conhost.exe Normal" (image windows-2025-vs2026
20261030.294.1). WP-LF changed neither the runner nor that test; it passes here 3 of 3, and every
earlier 7b run passed it. Its cause was in the runner: the priority fix, merged at `32eaee3` and
`d62dff8` (D386; Notes). Windows' "Build the
opt-in projects" green. Jobs and Test steps: `windows-latest` 30 min 15 s (27 min 38 s),
`macos-latest` 43 min 8 s (41 min 8 s), `ubuntu-latest` 8 min 1 s (6 min 4 s), the containers 6 min
39 s and 8 min 19 s (4 min 44 s and 6 min 1 s), each under four fifths.

[42285038375](https://github.com/RahimPasha/VisualCommit/actions/runs/42285038375) on `972c144`, the
priority fix merged (`32eaee3`, `d62dff8`; D386): all five jobs green, `GitRunnerPriorityTests` among
them on Windows; Windows' "Build the opt-in projects" green. Jobs and Test steps: `windows-latest`
33 min 17 s (30 min 5 s), `macos-latest` 37 min 58 s (35 min 53 s), `ubuntu-latest` 6 min 10 s (4 min
34 s), the containers 8 min 52 s and 8 min 45 s (6 min 18 s and 6 min 26 s), each under four fifths.

[42301846503](https://github.com/RahimPasha/VisualCommit/actions/runs/42301846503) on `63d2d80`, WP-A's
merge: `windows-latest` and `macos-latest` green (Windows' "Build the opt-in projects" green), the
container with the SDK from Microsoft's installer green; `ubuntu-latest` failed one test of App.Tests,
`MainWindowCloseTests.Closing_the_window_during_Fetch_all_ends_its_rows_in_line_and_waits_for_the_four_running`
(line 113: `closed` 1, not 0: the window closed after "Keep open"; first read here as line 116's
`BulkSlots.Held`), and the container with
Ubuntu's SDK packages another, `PaletteGrowthTests.The_build_and_a_one_letter_query_grow_in_step_with_the_refs`
("10,000 tags took 7.9 times the work of 2,500 … the limit is 6.3"). Both are phase 6's; WP-A
changed nothing they run; each passed in the other four jobs. The first was a race in the window's
close, fixed and merged at `85d599c`; the second is still open (Notes, "The two Linux failures of
42301846503"). Jobs and Test steps:
`windows-latest` 40 min 52 s (37 min 44 s), `macos-latest` 37 min 30 s (35 min 16 s),
`ubuntu-latest` 6 min 27 s (4 min 58 s), the containers 8 min 51 s and 8 min 45 s (6 min 26 s and
6 min 27 s), each under four fifths.

[42335370821](https://github.com/RahimPasha/VisualCommit/actions/runs/42335370821) on `85d599c`, the
close fix's merge: all five jobs green (Windows' keychain step and "Build the opt-in projects"
green, macOS' keychain step green, the containers' Secret Service steps green). Jobs and Test steps:
`windows-latest` 33 min 1 s (29 min 55 s), `macos-latest` 39 min 52 s (37 min 14 s), `ubuntu-latest`
6 min 18 s (4 min 54 s), the containers 8 min 30 s and 8 min 47 s (6 min 6 s and 6 min 17 s), each
under four fifths.

[42339567989](https://github.com/RahimPasha/VisualCommit/actions/runs/42339567989) on `9409aa8`, WP-B's
merge: `ubuntu-latest` and `macos-latest` green; three jobs failed, each in one test:
- Both containers (git 2.43.0): `Phase7bChecksB.Check_B1_B3_B6_B7_azure_sign_in_list_and_clone` at
  `Phase7bChecks.B.cs:247`. Below git 2.46 the clone's first request carried no credentials, where
  the report's B7 expects the URL's user with an empty password.
- The container with the SDK from Microsoft's installer: `AccountsProvidersViewModelTests.A_screen_reopened_while_a_sign_out_runs_shows_the_block_busy_until_it_ends`
  at line 245 (the reopened block's sign-out note null after its busy ended).
- `windows-latest`: 7a's `Phase7aChecksA.Check_A9_needs_sign_in_after_a_refused_token` at
  `Phase7aChecks.A.cs:572` (the pull requests state `Loading`, not `SignedOut`).

Each was investigated in its own worktree from `9409aa8`, each fix put to two skeptics (none
refuted; minors only) and merged (Notes, "The three failures of 42339567989"): B7's expected
requests below 2.46 at `e49bd5c` (`wip/p7b-git243`), the sign-out's end at `eb218a3`
(`wip/p7b-signout-race`), A9's waits at `7ddb928` (`wip/p7b-a9-loading`). Jobs and Test steps:
`windows-latest` 41 min 59 s (38 min 47 s), `macos-latest` 30 min 4 s (28 min 26 s), `ubuntu-latest`
6 min 32 s (5 min 8 s), the containers 9 min 21 s and 7 min 23 s (6 min 42 s and 5 min 14 s), each
under four fifths.

[42351282469](https://github.com/RahimPasha/VisualCommit/actions/runs/42351282469) on `f7ede15`, open
point 8's merge (`20e529e`), before the three fixes: `windows-latest`, `macos-latest` and
`ubuntu-latest` green; both containers failed `Phase7bChecksB.Check_B1_B3_B6_B7_azure_sign_in_list_and_clone`
alone, as on `9409aa8` (the clone's first request `UserName` null, 401: the failure `e49bd5c` fixes).
Jobs and Test steps: `windows-latest` 33 min 44 s (31 min 7 s), `macos-latest` 29 min 21 s (27 min
46 s), `ubuntu-latest` 8 min 5 s (6 min 2 s), the containers 6 min 48 s and 8 min 1 s (5 min 5 s and
5 min 39 s), each under four fifths.

[42354014761](https://github.com/RahimPasha/VisualCommit/actions/runs/42354014761) on `490f77b`, the
three fixes of 42339567989 merged: all five jobs green (Windows' keychain step and "Build the opt-in
projects" green, macOS' keychain step green, the containers' Secret Service steps green; both
containers, git 2.43.0, pass B7's three requests). Jobs and Test steps: `windows-latest` 32 min 23 s
(29 min 44 s), `macos-latest` 31 min 40 s (29 min 54 s), `ubuntu-latest` 7 min 41 s (5 min 58 s), the
containers 8 min 13 s and 6 min 30 s (6 min 3 s and 4 min 31 s), each under four fifths.

[42373090316](https://github.com/RahimPasha/VisualCommit/actions/runs/42373090316) on `c5ba918`,
WP-P's merge `1821206` with the report's ruling: all five jobs green (Windows' keychain step and
"Build the opt-in projects" green, macOS' keychain step green, the containers' Secret Service steps
green). Jobs and Test steps: `windows-latest` 29 min 20 s (27 min 7 s), `macos-latest` 33 min 14 s
(31 min 35 s), `ubuntu-latest` 8 min 11 s (6 min 3 s), the containers 6 min 44 s and 9 min 7 s
(4 min 46 s and 6 min 36 s), each under four fifths.

[42386428809](https://github.com/RahimPasha/VisualCommit/actions/runs/42386428809) on `8cb5ecc`
(phase 2's `Check_15_amend` fix merged at `26b44c7`, with status): `ubuntu-latest` and both
containers green (the containers' Secret Service steps green); `windows-latest` and `macos-latest`
each failed one test that `c5ba918..8cb5ecc` did not touch (their keychain steps green, Windows'
"Build the opt-in projects" green). On Windows phase 1's
`RepositoryWatcherTests.Nothing_is_raised_before_start_or_after_dispose` (line 201: `Changed`
counted 2, not 1, after the dispose; Notes, "The watcher's dispose test on CI"); on macOS
`PaletteGrowthTests.The_build_and_a_one_letter_query_grow_in_step_with_the_refs` ("10,000 tags took
6.7 times the work of 2,500 … the limit is 6.3"), the known open flake (Notes, "The two Linux
failures of 42301846503"), its second failure in about 50 runs and its first on macOS. Jobs and
Test steps: `windows-latest` 39 min 7 s (35 min 59 s), `macos-latest` 45 min 0 s (42 min 2 s),
`ubuntu-latest` 6 min 11 s (4 min 59 s), the containers 7 min 26 s and 8 min 46 s (5 min 14 s and
6 min 15 s), each under four fifths. Not rerun (CLAUDE.md: the owner's).

[42434720040](https://github.com/RahimPasha/VisualCommit/actions/runs/42434720040) on `07c0445`
(the watcher's fix merged at `abc6df7`, with status): all five jobs green. Jobs and Test steps:
`windows-latest` 39 min 21 s (36 min 26 s), `macos-latest` 37 min 58 s (35 min 37 s),
`ubuntu-latest` 8 min 3 s (5 min 59 s), the containers 7 min 23 s and 7 min 15 s (5 min 15 s and
5 min 26 s), each under four fifths.

[42437572741](https://github.com/RahimPasha/VisualCommit/actions/runs/42437572741) on `0a25226`
(WP-N's merge `a6952d2` with D388, and status): `windows-latest`, `ubuntu-latest` and both
containers green; `macos-latest` failed 2 of 7,814 tests, both cases of D388's
`SignInBindingsWindowTests.Inside_the_window_a_remote_the_rules_give_another_user_is_bound_to_no_token`
at its last assertion but one (line 246): git 2.56.0 (Homebrew) asked "Username for
'http://127.0.0.1:<port>': " without `bob@`, where the test expects every prompt to name bob; every
earlier assertion, no `x-access-token` and no 200 among them, passed (Notes, "D388's test on
macOS"). Jobs and Test steps: `windows-latest` 25 min 32 s (23 min 34 s), `macos-latest` 33 min
21 s (31 min 1 s), `ubuntu-latest` 7 min 12 s (5 min 39 s), the containers 9 min 17 s and 8 min
37 s (6 min 38 s and 6 min 12 s), each under four fifths. Not rerun (CLAUDE.md: the owner's).

[42454621322](https://github.com/RahimPasha/VisualCommit/actions/runs/42454621322) on `d127fef`
(the fix of D388's test and P11's budget merged at `98cdd86`, D389 and D390, with docs):
`windows-latest`, `macos-latest`, `ubuntu-latest` and the container with the SDK from Microsoft's
installer green, both D278 cases among them on macOS; the container with Ubuntu's SDK packages failed
1 of 7,817 tests, phase 6's `PaletteGrowthTests.The_build_and_a_one_letter_query_grow_in_step_with_the_refs`
("10,000 tags took 6.5 times the work of 2,500 … the limit is 6.3"), the open flake of 42301846503
(Notes, "The two Linux failures of 42301846503"), which nothing of the fix runs. Each job's
`git-url-user.txt` (D389): `macos-latest` Lost (`/opt/homebrew/bin/git`, 2.56.0, "could not read
Username for 'http://127.0.0.1:<port>'"), the four others Kept (`windows-latest` 2.56.0.windows.4,
`ubuntu-latest` 2.56.0, both containers 2.43.0), as the investigation predicted. Jobs and Test steps:
`windows-latest` 43 min 17 s (40 min 11 s), `macos-latest` 32 min 8 s (29 min 57 s), `ubuntu-latest`
6 min 53 s (5 min 28 s), the containers 9 min 10 s and 7 min 31 s (6 min 36 s and 5 min 22 s), each
under four fifths. Not rerun (CLAUDE.md: the owner's).

[42459591309](https://github.com/RahimPasha/VisualCommit/actions/runs/42459591309) on `fdb2c05`
(WP-LIVE's second stage merged): all five jobs green, Windows' "Build the opt-in projects" among
them (the live project with `Check_3`). Jobs and Test steps: `windows-latest` 43 min 8 s (39 min
40 s), `macos-latest` 20 min 29 s (19 min 34 s), `ubuntu-latest` 8 min 31 s (6 min 37 s), the
containers 9 min 43 s and 7 min 7 s (7 min 5 s and 5 min 5 s), each under four fifths.

[42472105974](https://github.com/RahimPasha/VisualCommit/actions/runs/42472105974) on `6333de6`
(the review's fixes merged): failed on `macos-latest` alone, one test of 7,874:
`ShellDriverTests.WaitForAsync_times_out_with_a_message_that_says_what_it_waited_for`, a stall under
load (Notes, "`ShellDriverTests`' wait on CI"); the other four jobs green. Jobs and Test steps:
`windows-latest` 43 min 14 s (40 min 5 s), `macos-latest` 41 min 22 s (38 min 43 s), `ubuntu-latest`
7 min 8 s (5 min 33 s), the containers 9 min 13 s and 9 min 40 s (6 min 43 s and 7 min 7 s), each
under four fifths. Not rerun (CLAUDE.md: the owner's).

[42477551563](https://github.com/RahimPasha/VisualCommit/actions/runs/42477551563) on `6f7199d`
(`ShellDriverTests`' wait of 2 s): all five jobs green. Jobs and Test steps: `windows-latest` 34 min
41 s (32 min 8 s), `macos-latest` 43 min 20 s (40 min 57 s), `ubuntu-latest` 8 min 36 s (6 min 47 s),
the containers 7 min 35 s and 9 min 0 s (5 min 34 s and 6 min 45 s), each under four fifths.

[42480966359](https://github.com/RahimPasha/VisualCommit/actions/runs/42480966359) on `c233091`
(WP-R's merge, `f016bdf`, and its docs): all five jobs green. Jobs and Test steps: `windows-latest`
44 min 21 s (41 min 6 s), `macos-latest` 31 min 10 s (29 min 2 s), `ubuntu-latest` 7 min 34 s
(5 min 59 s), the containers 7 min 55 s and 8 min 24 s (5 min 45 s and 5 min 56 s), each under four
fifths.

[42487421403](https://github.com/RahimPasha/VisualCommit/actions/runs/42487421403) on `d85d3db`
(D393's guard merged at `ee0aeeb`, and its docs): all five jobs green. Jobs and Test steps:
`windows-latest` 44 min 19 s (41 min 3 s), `macos-latest` 49 min 9 s (46 min 21 s), `ubuntu-latest`
6 min 27 s (5 min 5 s), the containers 8 min 14 s and 7 min 35 s (5 min 53 s and 5 min 21 s), each
under four fifths.

The run for phase 7a's merge on `master` is
[41243213784](https://github.com/RahimPasha/VisualCommit/actions/runs/41243213784), for `b486372`
(the merge commit `019e68a` and the commit that set phase 7a to "Done"): all five jobs succeeded,
none failed a test: `windows-latest` 5,952 tests with 4 skipped, 38 min 17 s (Test step 35 min 9 s
of 55; its keychain step green; "Build the opt-in projects" 29 s), `macos-latest` 5,949 with 3
skipped, 38 min 56 s (Test step 36 min 45 s; its keychain step green), `ubuntu-latest` 5,949 with 3
skipped, 6 min 11 s (Test step 4 min 45 s), and the two "bare ubuntu container" jobs 5,949 with 3
skipped each, 8 min 21 s and 8 min 8 s (Test steps 6 min 8 s and 5 min 54 s; Secret Service steps
green). The commit after it, `aad961e` (the owner's answers for 7b, D344 to D351), changes docs
only and started no run.

The last run on `phase/7a-hosting-github` was
[41190572421](https://github.com/RahimPasha/VisualCommit/actions/runs/41190572421), for the gate's
commit `a2c312a` (the branch at `47506ca`): all five jobs succeeded, none failed a test:
`windows-latest` (git 2.56.0.windows.2) 5,952 tests with 4 skipped, 24 min 6 s (Test step 21 min 48
s of 55; its keychain step green; "Build the opt-in projects", the real-window and live projects,
17 s), `macos-latest` (2.56.0) 5,949 with 3 skipped, 34 min 11 s (Test step 32 min 16 s; its
keychain step green), `ubuntu-latest` (2.56.0) 5,949 with 3 skipped, 6 min 26 s (Test step 4 min 54
s), and the two "bare ubuntu container" jobs (2.43.0) 5,949 with 3 skipped each, 6 min 28 s and 6 min
6 s (Test steps 4 min 43 s and 4 min 21 s; Secret Service steps green). The commits after it change
docs only, which start no run. Before it, run 41172878322 on `391f7f2` failed two tests in its first
Windows attempt, both a status read in the middle of a terminal's git (phase 7a's report, "Found by
the gate", item 4; fixed in `a9d02c1`, whose run 41181591519 had all five jobs green). During phase
7a the Windows Test step took up to 35 min 21 s and macOS's up to 37 min 57 s, under the 44 minutes
at which the lead raised D232's limit of 55 (D186's rule). Phase 7b's first code commit raised the
limits to 70 and 80 (D377): the lead raises one when a Test step passes 56 minutes, at a merge before
the counted round. Each of the phase's runs, with what it
found, is in this file as of the merge's second parent (`git show 3a3cc71:docs/status.md`).

The run for the review's changes on `master` (the build trim `32b238c`, the heavy-run lock
`d56591b` and the docs `abf6ca5`) is
[40679200453](https://github.com/RahimPasha/VisualCommit/actions/runs/40679200453), for `abf6ca5`:
all five jobs succeeded, the first CI run of D239's trim: `windows-latest` (35 min 32 s; its Test
step 32 min 40 s of 55, and "Build the real-window pass" green in 21 s), `macos-latest` (26 min
14 s; Test step 24 min 53 s), `ubuntu-latest` (6 min 47 s; Test step 4 min 57 s) and the two "bare
ubuntu container" jobs (7 min 10 s and 7 min 34 s; Test steps 5 min 22 s and 5 min 26 s).

The run for phase 6's merge on `master` is
[40073425553](https://github.com/RahimPasha/VisualCommit/actions/runs/40073425553), for `3a58bf0`
(the merge commit `3f34c87` and the commit that set phase 6 to "Done"): all five jobs succeeded,
Windows with 5,037 tests and the others with 5,034: `windows-latest` (34 min 22 s; its Test step 31
min 49 s of 55), `macos-latest` (27 min 9 s; Test step 25 min 20 s), `ubuntu-latest` (6 min 58 s;
Test step 5 min 7 s) and the two "bare ubuntu container" jobs (7 min 45 s and 5 min 54 s; Test steps
5 min 42 s and 4 min 23 s).

The run for phase 6's gate commit `b561b7c` is
[40059391404](https://github.com/RahimPasha/VisualCommit/actions/runs/40059391404): all five jobs
succeeded, Windows with 5,037 tests and the others with 5,034 (three tests are Windows' own):
`windows-latest` (26 min 47 s; its Test step 24 min 37 s of 55, and its step "Build the real-window
pass" green), `macos-latest` (25 min 22 s; Test step 23 min 33 s), `ubuntu-latest` (5 min 50 s; Test
step 4 min 21 s) and the two "bare ubuntu container" jobs (7 min 16 s and 7 min 45 s; Test steps 4 min
56 s and 5 min 50 s). The container with the SDK from Ubuntu's packages passed on its second attempt:
its first failed `ShellDriverTests.Wheel_reaches_the_control_under_the_mouse_with_the_sign_of_a_real_wheel`
(a hit test before the window's first frame on the slow container; for phase 7a), as phase 4's flaky
test did on `master`. The commits after it change docs only, which start no run. The gate's earlier
rounds had runs 40027897278 (`774464d`), 40044430603 (`b7da110`) and 40051904024 (`639db4a`), all five
jobs green; macOS's Test step reached 43 min 12 s in run 40019921605 on `a937ca6`, near D232's 44.
CI's Test step on Windows, macOS and Linux had a limit of 55 minutes and each of their jobs 65
(D232) until phase 7b's first code commit set 70 and 80 (D377); the containers keep 14 and 25. The owner looks again at CI's duration when the project is
over (D134). The runs of the phase before the gate, and what each found, are in "Notes for whoever
resumes".

The run for phase 5's merge on `master` is
[39078809291](https://github.com/RahimPasha/VisualCommit/actions/runs/39078809291), for `7bcb5c0`
(the merge commit `c299939` and the commit that set phase 5 to "Done"): all five jobs succeeded,
each with 3,233 tests passed: `windows-latest` (16 min 51 s; its Test step 14 min 59 s of 40),
`macos-latest` (19 min 43 s; Test step 17 min 40 s), `ubuntu-latest` (5 min 19 s; Test step 3 min
50 s) and the two "bare ubuntu container" jobs (4 min 29 s and 5 min 30 s; Test steps 2 min 58 s
and 3 min 48 s). The Windows Test step's 15 minutes against the gate run's 25 for the same tests
is the runners' spread.

The last run on `phase/5-history` was
[38931127856](https://github.com/RahimPasha/VisualCommit/actions/runs/38931127856), for the
gate's commit `1b2e734`: all five jobs succeeded, each with 3,233 tests passed: `windows-latest`
(27 min 50 s; its Test step 25 min 15 s, and its step "Build the real-window pass" green),
`macos-latest` (17 min 43 s; Test step 16 min 13 s), `ubuntu-latest` (4 min 48 s; Test step 3 min
26 s) and the two "bare ubuntu container" jobs (5 min 38 s and 6 min 2 s; Test steps 3 min 56 s
and 4 min 14 s). The commits after it change only docs, which start no run. The gate's first run,
on `4281f4c`, had run 38909397699: all five jobs green with 3,227 tests. CI's Test step on
Windows, macOS and Linux has a limit of 40 minutes and each of their jobs 50 (D184, amending
D152); the lead raises the limit when the Windows or macOS Test step passes 32 minutes, and
records the new one as a decision (D186). The two container jobs keep 14 minutes for the Test
step and 25 per job. The owner looks again at CI's duration when the project is over (D134).

Earlier on `phase/5-history`:
[38831177542](https://github.com/RahimPasha/VisualCommit/actions/runs/38831177542), for `5bdd1de`
(the second wave, the lead's follow-ups and the 30-second default wait): all five jobs succeeded
with 3,046 tests each; the Windows Test step took 17 min 54 s of its then 24 and macOS's 14 min
18 s. The runs that failed after it on the way to the gate, and what each found, are in the phase 5
report's "Other platforms".
On the way, runs 38816004590 (`e11ea7d`: macOS path keys in WP7's tests, check 23's stash wait)
and 38820513395 (`40cb5f5`: check 23 waiting for real Undo states) failed, as fixed in `fe34b62`.
Before that,
[38772677755](https://github.com/RahimPasha/VisualCommit/actions/runs/38772677755) for `7e7d99d`
(the test speed-ups, whose `TempRepo` every test project uses): all five jobs succeeded with 2,765
tests each; the Windows Test step took 16 min 38 s of its 24 and macOS's 11 min 45 s. The run
before it, [38748240435](https://github.com/RahimPasha/VisualCommit/actions/runs/38748240435) for
`4b5716e` (the first wave merged: WP0, WP2a, WP1, WP-S, WP3's git half and D182), had all five
jobs green with 2,749 tests each (Windows Test step 16 min 17 s, macOS 14 min 14 s).
Since 2026-10-15 the jobs run different gits: Windows 2.56.0.windows.2, macOS 2.56.0 (Homebrew),
`ubuntu-latest` 2.55.0, the two containers 2.43.0. Two runs failed on the way and were fixed:
38723225901 (phase 3's check 15b on Git 2.56, `281dd69`) and 38738421993 (a phase 5 scenario test
that took Git 2.55.0's `--no-walk` regression for the rule, `499b90c`).

Phase 5's first run is
[38350713964](https://github.com/RahimPasha/VisualCommit/actions/runs/38350713964), for `279dd8c`
(phase 4's leftovers, D160): all five jobs succeeded with 2,421 tests each: `windows-latest` (16 min
7 s; its Test step 13 min 51 s), `macos-latest` (13 min 28 s; Test step 12 min 3 s),
`ubuntu-latest` (3 min 36 s) and the two "bare ubuntu container" jobs (4 min 14 s and 4 min 11 s).
GitHub announced that `ubuntu-latest` moves to Ubuntu 26 from 2026-10-19; the Linux job and the
containers may then see other package versions.

The last run on `phase/4-rebase-conflicts` was
[38320711773](https://github.com/RahimPasha/VisualCommit/actions/runs/38320711773), for the
gate's commit `6badab7`: all five jobs succeeded, each with 2,411 tests passed: `windows-latest`
(14 min 59 s; its Test step 13 min 9 s), `macos-latest` (15 min 14 s; Test step 13 min 57 s),
`ubuntu-latest` (3 min 6 s) and the two "bare ubuntu container" jobs (3 min 54 s and 4 min 5 s).
The commits after it changed only docs, which start no run. CI's Test step then had a limit of 24
minutes and each of the three platforms' jobs 32 (D152, amending D134). During phase 4 CI found
what the Windows machine could not (Git 2.55's own files and words, a background clean-up that
held the queue, checks that read the screen too early); the runs were red until each was fixed
(phase 4's report, "Other platforms", and its handoff, "Things that cost time").

The run for phase 4's merge on `master` is
[38336234442](https://github.com/RahimPasha/VisualCommit/actions/runs/38336234442), for `50cf35c`
(the merge commit `ce5f337` and the commit that set phase 4 to "Done"). Its first attempt had four
jobs succeed with 2,411 tests each (`macos-latest` 15 min 33 s, `ubuntu-latest` 3 min 40 s, the
two "bare ubuntu container" jobs 3 min 48 s and 3 min 7 s) and `windows-latest` fail: 2,410 of
2,411 passed, and the one failure was phase 1's
`RepositoryWatcherTests.A_burst_of_changes_raises_changed_once` (a burst of 20 file writes raised
the watcher's Changed twice, not once: on the loaded runner a pause between the writes outlasted
the 1-second quiet period). Re-running that job passed, 2,411 of 2,411 (15 min 40 s), so all five
jobs are green on attempt 2. Phase 4 did not change the watcher or that test; the test depends on
the wall clock, and the proper fix (the watcher's quiet period on a clock the test holds) is listed
for phase 5's first code commits in phase 4's handoff.

Before phase 4, the last run on `master` was
[38213858149](https://github.com/RahimPasha/VisualCommit/actions/runs/38213858149), for `d74c99e`
(phase 3's merge commit `8b33745` and the commit that set phase 3 to "Done"): all five jobs
succeeded with 1756 tests each, `ubuntu-latest` on its second attempt after a flake of phase 1's
check 13 that phase 4 removed (D150). Earlier: run
[38116890189](https://github.com/RahimPasha/VisualCommit/actions/runs/38116890189) for phase 2's
merge (557 tests) and run
[38077727040](https://github.com/RahimPasha/VisualCommit/actions/runs/38077727040) for phase 1's
merge (379 tests), all five jobs green.

The run numbers from before 2026-10-09 went with the deleted repository (D41), so those that
older commits and the phase 0 report and handoff mention no longer open; what they showed is as
written there and in this file's history.

Earlier in phase 0 the test step froze the whole job on macOS in 4 of 9 runs. The freezes
stopped with the commit that is now `0784112` (decision D37). If a macOS job ever sits in its
Test step for minutes again, read "Known issues" in the phase 0 handoff first.

### Next step

WP-N (design 13, part N) is merged at `a6952d2`, with the lead's rulings on its hunt 3 applied,
reviewed and verified (D388 recorded at `cf82d50`; Notes, "WP-N's build"); its diff carries the
owner's identity alone and none of the sandboxes' names or tokens. CI's run on `8cb5ecc`
(42386428809) failed two older tests (CI section); the watcher's dispose test was a race in the
test, fixed with one product fix and merged at `abc6df7` (Notes, "The watcher's dispose test on
CI"); its CI run, 42434720040 on `07c0445`, is green in all five jobs, and the WP-N merge is
pushed after it. 13.9's runs on `cf82d50` are done (Notes, "WP-N's merge runs"): the whole
default run and the two runs alone passed, the lagged list but part P's `Check_P11` under the lag
(an exact budget of 0 that a late whole refresh broke). CI's run on the merge, 42437572741, failed
D388's test on macOS alone: git 2.56.0 over curl 8.23.0 loses the URL's user (Notes, "D388's test
on macOS"). Both are fixed and merged at `98cdd86` (D389, D390; plan risk 43), and its classes pass
plain and lagged; 13.9's picture comparison and part N's inspection passed (Notes, "WP-N's merge
runs"). CI 42454621322 on `d127fef` is green but phase 6's open palette flake in one container,
and macOS read Lost and passed (CI section); steps 9, 13 and 14 are ticked. WP-LIVE's second stage
is merged at `fdb2c05` and passed with the tokens (step 16 ticked; Notes, "WP-LIVE's second
stage"); CI 42459591309 on `fdb2c05` is green in all five jobs (CI section). The review before the gate
(step 17) ran on `fdb2c05` (Notes, "The review before the gate"): 9 of its 19 findings stood, the
lead's rulings are D391 and D392. Its fixes are merged at `6333de6` (Notes, "The review's fixes"),
pushed. The lead's runs on `6333de6` all passed, design 0.6's timing report among them, and no test
needs D237's split (Notes, "The lead's runs after the review's fixes"); step 17 is ticked. CI's run on
it, 42472105974, failed one phase 1 harness test on macOS alone (`ShellDriverTests`' 0.3 s wait, a
stall under load), whose wait is now 2 s with its assertions kept (Notes; CI section). WP-R (design
14) is merged at `f016bdf` with CLAUDE.md's rows (Notes, "WP-R"); it has only been compiled. CI 42477551563 on
`6f7199d` is green in all five jobs, and 42480966359 on `c233091` (WP-R's merge) too (CI section).
The owner's answer on the GitLab account's five projects, given on 2026-11-08, is (b): the live
command's GitLab guard narrowed to the sandbox project (D393), built, reviewed and refixed (Notes,
"D393's guard") and merged at `ee0aeeb`, where the offline classes pass (582). The owner said to
start the gate now and is at the machine. The gate (step 18) has begun, in 7a design 13.7's order
with D335's live runs. The live command for the three providers ran on `ee0aeeb`, the owner told:
GitHub's and Azure DevOps' classes passed, and GitLab's after two stalls of the network to gitlab.com
(the report's "Live command", "The gate's first run"). It found one correction for a fake: github.com
now answers a commit it lacks beside `author` 404, not D342's 500. That correction (D394) is merged at
`cb9af6d`, with the fix to the client that GitHub's change called for (D395: a 404 in a repository not
known to be readable is checked against that repository's read; the owner approved it, since it amends
D342 (2)); Notes, "D394 and D395". The lead's runs at that merge passed, phase 7b's walk-through among
them, and `Phase7bRealWindowPass` passed twice on its own, 5 of 5 each time, the owner told and present
(D246; the report's "Run", "Before the gate"). Next, the counted round on one commit: the whole default run
through `scripts/heavy-run.sh`, the real-window pass, the list of pictures, the live command again,
and the inspection of every picture in batches of at most about 150, the earlier ones compared with
`D:/Projects/Personal/gate7b-reference/` (0.7). WP-P (design 12) is merged and step 12 ticked: its merge's runs passed but phase 2's
`Check_15_amend` once under the lag, a race in the check fixed at `040eb65` (merged at `26b44c7`; its
class 25 of 25 with and without the lag through `scripts/heavy-run.sh`), and CI 42373090316 on
`c5ba918` is green; CI's run on the fix's merge, 42386428809, failed the two older tests above.

WP-B (design 11, part B) is merged at `9409aa8`; its classes, lagged list, 11.8's comparison and
picture inspection passed (Notes, "WP-B's merge runs"). CI's run on it (42339567989) failed three
tests; their fixes are merged (`e49bd5c`, `eb218a3`, `7ddb928`; Notes, "The three failures of
42339567989") and their classes pass; CI's run on them (42354014761) is green in all five jobs.
Part N's checks (design 13.8) are committed (`28ec097`, merged at `5561548`; D244), written
while WP-P was built, from the design and part P's checks, never from WP-P's code (Notes).
Part P's checks are committed (`f1ac5ac`, D244); WP-P (section 12) is being
built from that commit in `D:/Projects/Personal/vc-wt/p7b-requests` on `wip/p7b-requests`, then
reviewed and hunted; at its merge the lead runs 12.9's runs (the lagged list, `Phase7aChecksP` and
`Phase7aChecksG` against `phase-7a-pictures.sha256`, phase 3's `*Check_03_other_menus*`, part P, and
part A's `a06c` again). Open point 8 is settled and its changes are merged at `20e529e` (Waiting on
the owner, "Azure DevOps' probes"): the fake keeps connectionData's Anonymous 200 to organizations with
public projects, the live command's fake is public as the sandbox's organization is, and A9 takes the
fault `AnonymousIdentity`; its classes pass at the merge (Hosting.Tests' five Azure DevOps classes 73
of 73, the 7b project's `SignInDialogProvidersTests`, `Phase7bChecksA`, `Phase7bSupportTests` and
`Phase7bExpectationTests` 52 of 52, the live command's offline classes 405 of 405), and part A's 29
pictures are the same files as WP-B's merge run's.

WP-A (design 10, part A) is merged at `b6de3cf`; its classes, lagged list and picture inspection
passed (Notes). CI's run on it (42301846503) failed two phase 6 tests on Linux that WP-A does not
touch. The close race is fixed and merged (`85d599c`; its classes pass; CI's run on it,
42335370821, green in all five jobs). The palette growth test's first fix was refuted as a
loosening and is not merged; the second round found no measure that holds, so the test stays as it
is and its rare Linux failure stays open (Notes, "The two Linux failures of 42301846503"). For the week from 2026-11-05 the owner gives the phase about all of
its allowance.

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

- **The design stage's files** lie outside the repository, in `D:/Projects/Personal/phase7b-work/`:
  `research/` (r1 to r8 with the gap rounds), `design/` (the drafts and the lead's 31 rulings),
  `review/` (`findings.md`, one file per finding in `f/`, `review-raw.json`) and `fix/` (the
  design split per section, the fixers' and checkers' reports). Nothing there is needed to build:
  the design and its decisions hold what the builders need. Wave 1's worktrees and `wip/` branches
  were removed at its merges, and wave 2's at theirs (2026-11-01). Wave 1's adversarial findings
  and the lead's rulings on them lie in `D:/Projects/Personal/phase7b-work/verify/` (`findings-wave1.md`,
  `rulings-wave1.md`); D379 to D382 hold what they settled. Wave 2's hunters' harnesses lie there
  too (`wp-gl-*`, `wp-az-*`, `wp-fa-correctness`); what they found and the lead's rulings on the
  clients' third hunts are in sections 6's and 7's As built, which end with the lead's items for
  the review before the gate (7a's GitHub gaps the same inputs show, and the core's).
- **Stage 1's first run** (2026-11-05, 20:51 UTC, on `8faa839`; the offline classes first, 404 of 404):
  9 tests, 8 passed, 41.9 s. Azure DevOps: `Check_1`, `Check_1b_probes`, `Check_2` (the app's sign-in,
  list, push and create, !5, abandoned and cleaned up) and `Check_4` (a clone through the
  organization's host) passed; its run.txt (631 lines) names 12 answers equal to the fake's and
  differences for WP-LF (a made-up plain token on connectionData answers 200 where the fake answers
  401, the sandbox's info/refs with a made-up token 401 with Basic, Bearer and TFS-Federated
  challenges where the fake answers 404, the 409 and 400 creates' bodies, the abandon's and the
  create's members, and more). GitLab: `Check_1` and its probes passed; `Check_2` failed at the list
  (D385; `Check_2b` had nothing to probe and deleted its branches). Both run.txt files hold neither
  the organization's name, the namespace nor an address, checked fully decoded; `glpat-` appears
  only as the prefix in the command's own words. They lie in `artifacts/live/` (not committed).
- **Stage 1's GitLab run again** (2026-11-06, 05:20 UTC, on `652ad80`, whose code is `8faa839`'s; the
  owner's new token with Project: Read among its user permissions too, D385; checked as 15.8 says;
  the offline classes first, 404 of 404): `--filter-class "*GitLabLiveTests"`, 4 tests, 4 passed,
  47 s with the lock. `Check_2` signed in, listed, cloned through the clone form with the app's
  helper (`x-access-token` holds on gitlab.com, 16.1), pushed two branches and created !1 as a
  draft; `Check_2b` probed it (the 409 and 400 bodies as the fake sends them, the create with
  nothing ahead 201, as the fake), and closed and deleted !1, !2 and both branches. The fake's
  answers to `GET user`, the created request and its probes hold a subset of GitLab's members (for
  WP-LF; the create with nothing ahead has no `first_contribution`); every probed status and body
  matched the fake's. run.txt (48 lines) holds neither the organization's name, the namespace nor
  an address, fully decoded; `glpat-` only in the command's own words, none token-shaped.
- **WP-LF's inputs** (2026-11-06): `D:/Projects/Personal/phase7b-work/live/` holds copies of
  Azure DevOps' run.txt (`azure-devops-run-20261105.txt`, 631 lines: 12 answers equal to the fake's,
  11 not), GitLab's (`gitlab-run-20261106.txt`) and the first run's console output with GitLab's 403
  (`stage1-run1-output-20261105.txt`, D385). They are screened, but still hold the services' request
  ids, ETags, the sandbox's ids and the deployment's host names: nothing of them enters the
  repository; the fakes take made-up copies of the measured shapes and texts.
- **WP-LF** (merged at `22fad1b`, 2026-11-06; design 0.4, 16.1's As built): stage 1's answers in
  `FakeAzureDevOps*.cs` and `FakeGitLab*.cs`, made-up values, each marked MEASURED with its UTC date
  where the run read it and ASSUMED where it was generalised (a private organization's Anonymous 200,
  measured otherwise on 2026-11-07 and changed at `20e529e`,
  the ratelimit values beyond `GET user`, a create with commits ahead's `labels`). Azure DevOps: a Basic
  value in none of the PAT forms (`azdo_FAKE-`, 84 with `AZDO`, the older 52 of base32, ASSUMED) gets
  connectionData's Anonymous 200; errors without `api-version` in `Content-Type`; an empty title's 400 in
  the service's words; the create's `labels`, the abandon's members; the refs POST creates (200) and
  its updates' seven members, names compared without case so every OS answers alike; git's 401 keeps
  g1 G4.1's realm with `.app.`, the API's empty 401 the measured one without. GitLab: `GET user`'s 41
  members and a single request's in the service's order (the create's without `first_contribution`),
  a fork's pair after `prepared_at` (r6 4.1), `burst_traffic_per_user_free_plan` 100, and D385's user
  permissions (`FakeGitLabPermissions.UserSection`, `UserProjectRead`; the default holds both). The
  clients: only messages made one line (CR LF split a log line); A1's refusal of the Anonymous 200 and
  A2's `Forbidden` with `insufficient_granular_scope` pinned. The live command gains one probe, the
  sandbox organization's connectionData without a credential (whether it is private), for stage 2.
  Kept: git's `info/refs` refusals (the fake's `AuthRemoteServer` 401; the helper reads no
  `wwwauth[]`), a name Windows cannot store in the refs POST. Built by one fixer, reviewed with two
  lenses (6 minors fixed), hunted in three rounds (one major confirmed and fixed: the Anonymous 200's
  mark for a private organization), the lead's five minors fixed and verified twice; 720 Hosting.Tests
  of the package's classes, 272 `CredentialBindingSelectTests`, 8 `Phase7bSupportTests` and 404 offline
  live tests passed in its worktree. **For the review before the gate**: GitLab measured a burst limit of
  100 a minute with a token, 304s counted, not 7.3's and 4.4's 2,000; part P's refreshes and A2's and
  A4's requests are weighed against it. The empty folder `D:/Projects/Personal/vc-wt/p7b-live-fix/src/VisualCommit.App`
  is left (a process held it at removal); remove it later.
- **WP-LF's lagged list** (2026-11-06, 05:18 to 05:35 on the laptop, on `22fad1b`, through
  `scripts/heavy-run.sh`, one command at a time, `VISUALCOMMIT_WATCHER_LAG_MS=1500`): all passed,
  phases 0 to 2's part 2 25, the App.Tests classes 66, phase 6's 21, 7a's walk-through 207, 7a's six
  wiring classes 44, 7b's line 2 (only `RepositoryHostingProvidersWiringTests` exists yet). The log
  is `$TMP/p7b-runs/lagged-lf.txt`.
- **Part A's checks** (`f4ae515`, 2026-11-06): A1 to A14 with 29 scripted pictures, written by one
  writer and checked by one reviewer (seven minors, all fixed; no blocker or major); the report's
  shared conventions are about 15 KB (D244's 30). Its open point 8: A9's Anonymous 200 rests on
  WP-LF's ASSUMED reading for a private organization until `Check_1b`'s new probe runs (settled on
  2026-11-07 and merged at `20e529e`: Waiting on the owner, "Azure DevOps' probes").
- **The priority fix** (merged at `32eaee3`, 2026-11-06; D386): CI run 42216604663 found git's console
  host at Normal for the merge-tool lookup's below-normal call. An Opus investigator measured why
  (Windows takes a new process's class from its creator partway through the creation, but puts it in
  the creator's job and lists it only at the end) and fixed it in `WindowsJob` and `GitRunner` (a job
  with Windows' priority-class limit, attached before the lowering, which adds each descendant to it).
  Two Opus skeptics could not refute it (minor points only); a follow-up added
  `GitRunner.StartObserver`, an internal seam for the tests, and a test through `RunAsync` that fails
  3 of 3 under each of four reversions of the wiring, and corrected the comments and
  `docs/architecture.md`. Two more skeptics then checked that test: one ran it 75 times under load
  and found no failure, hang or leftover process; the other showed a lowering round still running
  could lower a late process before the test read it, so a broken wiring could pass the first
  assertion. The second round (`864c900`, merged at `d62dff8`) raises each process the test made
  and reads it back, at once and 100 ms later; with the host below normal the fix passed 15 of 15
  and each of four reversions failed every run (10 to 15 each) with its own message. On each merge,
  `GitRunnerPriorityTests`, `GitRunnerTests`, `GitRunnerRecordTests`, `AskPassRunnerTests` and
  `MergeToolHelpTests` passed 84 of 84 through `scripts/heavy-run.sh`. The experiments and their outputs are in
  `D:/Projects/Personal/phase7b-work/verify/priority/`. One case stays open (D386): a process whose
  creation read git's job before git joined it and that shows only after the last round; none in
  about 480 trials. If CI's signature comes back, that order is the place to look.
- **WP-A** (merged at `b6de3cf`, 2026-11-06; design 10 and its As built): built in
  `D:/Projects/Personal/vc-wt/p7b-accounts`, two reviews and three hunts (no confirmed blocker; the
  third hunt's one major was A12's bound, left failing for the lead). The lead's rulings: A12's `a12b`
  scrolls by less than one line of Azure DevOps' note (measured 7 px, lines 13.9 apart; the report's
  "Changed in this report after its first commit"); Azure DevOps' token link takes the theme's
  disabled look while no organization is given (`a06a`, `a11b`); the dialog finds another provider's
  token inside a pasted value (D387; two verification rounds). Accepted, not changed: GitLab's
  unprefixed tokens read as 20 characters (ASSUMED); a run that starts with the dialog's own prefix is
  not searched; the octal and base64 limits D387 names. For the review before the gate: whether the
  providers' own refusals in Hosting should use the dialog's reading (D387). The verifiers' probes are
  in `D:/Projects/Personal/phase7b-work/verify/wp-a-rulings/` and `wp-a-r3/`. 7a's pictures
  before the merge are copied to `D:/Projects/Personal/phase7b-work/ref-7a-preA/`.
- **WP-A's merge runs** (2026-11-06, 14:35 to 15:00 on the laptop, on the first merge `da7a296`,
  through `scripts/heavy-run.sh`, one command at a time; the redone merge `b6de3cf` differs from it
  only in two string literals of `SignInDialogProvidersTests`, whose 22 tests passed again on it):
  10.9's classes 154 of 154 (`Phase7b` 62, 7a's part A classes 53, `SignInEntryProvidersTests` and
  `SignInEntryTests` 13, `SettingsDialogViewTests` 11, `Phase6ChecksS` 13, `Check_L31*` 2), then the
  lagged list (`VISUALCOMMIT_WATCHER_LAG_MS=1500`) 436 of 436 (`Phase2Checks` 25, App.Tests' eight
  classes 66, Phase6's 21, `Phase7a` 207, Hosting's 7a classes 44, `Phase7b` 62, Hosting's 7b classes
  11). Part A's 29 pictures were the same files with and without the lag. 7a's 85 against the copies
  before the merge (`scripts/compare-pictures.py`, output in `D:/Projects/Personal/phase7b-work/cmp-wpa/`):
  57 the same, the 23 part A pictures changed only in the intro's box (10.6), and `b06a` (the fake's
  port in the clone URL), `n06b` (the caret), `n02b` and `n07a` (the status bar's busy indicator) and
  `a09b` (2 pixels) by nature.
- **Part B's checks** (2026-11-06): B1 to B8 with 20 scripted pictures (`b01a` to `b08c`; `b03a` and
  `b03b` repeated in the real window, and B1's button and B7's clones there with no capture), written by
  one Opus writer and reviewed by one Opus reviewer (three minors, all fixed), open points 11 to 21.
  The lead ruled open point 21: a palette command opens the browser through the form's browse command
  with its provider, so 7a's `b03a-listed` keeps its disabled button. The same commit records the
  inspection's 562 for three signed-in blocks (part A's "Changed in this report after its first
  commit").
- **WP-B's merge runs** (2026-11-06, 21:40 to 22:29 on the laptop, on `9409aa8`, through
  `bash scripts/heavy-run.sh`, one command at a time): the 7b project 107 of 107, 7a's 207, Hosting's
  three clone classes 22, App.Tests' five classes 34, phase 1's `Check_13` and `Check_14`, phase 6's
  `Check_L26`, `Check_L32` and `Check_K10`; then the lagged list (1500 ms), every command green (the 7b
  project 107 again, 7a's 207, the providers' wiring and request classes 20). 11.8's comparison (the
  references saved before the merge in `D:/Projects/Personal/phase7b-work/ref-preB/`, phase 6's
  against 7a's gate copies in `D:/Projects/Personal/gate7a-reference/`): 7a's part B pictures,
  phase 1's `13a` and `13c`, phase 6's `l26a`, `l26b` and `k10a` to `k10d` and `k10f` the same files;
  what differed is what varies by nature: the temporary folder's name (phase 1's `13b`, `14a`,
  `14b`; phase 6's `l32a`, `l32b`, and `k10e`'s palette detail), a caret (`n06b`), the progress bar's
  segment (`n02b`, `n07a`), 7a's fake's fallback port (`b06a`, open point 20) and one-level
  anti-aliasing (`a09b`, `n04a`, back to 7a's gate files). An Opus inspector opened part B's 20
  pictures and judged each difference: no defect, no real change (crops in
  `D:/Projects/Personal/phase7b-work/inspect-wpb/`).
- **Part P's checks** (2026-11-06): P1 to P14 with 22 scripted pictures (7 methods; P1 and P10
  repeated in the real window, 4 captures), written by one Opus writer and reviewed by one Opus
  reviewer (three minors, all fixed), open points 22 to 33; committed at `f1ac5ac`.
- **Part N's checks** (2026-11-07): N1 to N9 in 7 methods with 22 scripted pictures (N2 and N6
  repeated in the real window, 4 captures), written by one Opus writer in
  `D:/Projects/Personal/vc-wt/p7b-ncheck` while WP-P was built, from the design, part P's checks and the
  merged code (never WP-P's), and reviewed by one Opus reviewer in two rounds (one major, N4's config
  check against mobile-app's own `Fernhill-Labs@` URL, and eight minors, all fixed). Open points 34 to
  45; the lead ruled two: the real-window twins create from the prepared `topic/no-request` (35), and
  the phase's 29 methods and 93 scripted pictures stand, past 0.5's and 0.6's estimates and under
  D230's 40 (44). Several points give the expected result the design reads and what changes if WP-N's
  build shows otherwise (36, 37, 43, 45): such a change goes under "Changed in this report after its
  first commit" with the lead's ruling. The report is about 415 KB; its shared conventions stay about
  15 KB.
- **The two Linux failures of 42301846503** (2026-11-06). Investigated by an Opus agent each, every
  fix put to two Opus skeptics; the experiments' files are in
  `D:/Projects/Personal/phase7b-work/verify/close-slots/` and `.../verify/palette-growth/`.
  - `MainWindowCloseTests`, line 113 (`closed`, not `BulkSlots.Held`, which is line 116): a race in
    the app. `MainWindowViewModel.FinishThenCloseAsync` acted on whichever of the operations' end
    and the dialog's answer `Task.WhenAny` reported first, and both reach `WhenAny` through the
    thread pool (their sources run their continuations asynchronously), so "Keep open" could be
    overridden by operations that ended just after it. The dialog's own state now decides, as in
    `TryClose`; a new test forces the reverse order through a held synchronization context and
    failed 3 of 3 before the fix. Both skeptics found no other path (minor notes only). Merged at
    `85d599c` (`wip/p7b-close-slots`); `MainWindowCloseTests`, `MainWindowHooksTests`,
    `DialogHostTests` and the `*DialogTests` classes passed through `heavy-run.sh` (94 of 94).
  - `PaletteGrowthTests` (7.9 against 6.3; once in 47 CI runs since `ac08f43`). Not established
    why; the skeptics ruled out page faults and reproduced 7.9 by emulating time taken from the
    measuring thread that its clock still counts (on Linux `ThreadWork` reads
    `CLOCK_THREAD_CPUTIME_ID`, which in a guest counts what the hypervisor takes). The first fix
    (`wip/p7b-palette-growth`, `b140a0e` to `c90b862`: four small runs a measure, and twelve rounds
    when three are over the limit) was refuted as a loosening: further rounds were taken only
    after a failure, and the four-run small measure undercounts scans over live data, so in paired
    trials superlinear work passed it 22 times in 120 against 15 with three fixed rounds. It is not
    merged; that branch stays unmerged. A second round (`wip/p7b-palette-growth-2`) tries fixed
    rounds with symmetric samples (a median of paired ratios, a robust slope over four sizes, or
    minima over more rounds), accepted only if, paired on the same samples, superlinear work
    passes it no more often than the old procedure and linear work fails it less often under the
    emulated interference. Its outcome (2026-11-07, one Opus agent, 414 paired trials on the loaded
    laptop, each a fresh process): none holds. Medians of per-round ratios (5, 7, 9 rounds), medians
    of each side, Theil-Sen slopes and the smaller measures against 10,000 tags failed linear work
    less often but passed a superlinear cost (a 40 to 50 % scan) more often than the old procedure in
    at least one setting; minima over more rounds failed linear work more often; the forms against
    20,000 tags lost linear trials to collections inside the no-GC region (under memory pressure, not
    a short budget). Only medians of nine at 625 against 20,000 tags improved without an extra
    superlinear pass, on 4 to 7 trials a setting, too few to decide (about 8 to 10 h on a quiet
    machine, and a 20,000-tag context that would amend phase 6's design 5.12). So `63d2d80`'s test
    stays, `wip/p7b-palette-growth-2` is clean at `85d599c` with nothing committed, and the CI
    failure of 42301846503 stays a known, open flake of that test. It failed again in CI
  42454621322 on `d127fef` (2026-11-08), in the same container (Ubuntu's SDK packages): 6.5 against
  6.3 (138,010,834 against 21,166,593, 3.4 times the items). For the review before the gate:
    `WithoutCollection`'s message "the budget is too small" can mislead when a collection comes
    from memory pressure. The data: `D:/Projects/Personal/phase7b-work/verify/palette-growth/round-2/`.
- **WP-P's build and merge** (2026-11-07; design 12 and its As built): built in
  `D:/Projects/Personal/vc-wt/p7b-requests` from `f1ac5ac` by two Opus builders (`34720c2` to
  `7049f5b`), one review with two lenses (one major: the menus checked by `Contains` and Azure DevOps'
  drops untested; nine minors, all fixed at `d2d27a1` and `dbbe196`) and three hunts by lens (tokens,
  checks, state), each serious finding put to fresh Opus skeptics. Hunt 1's state lens found that a
  sign-out while a GitLab list loads still sent the later pages with the signed-out token (refuted
  as a major, fixed with two minors at `726ed0f`); hunt 2's eight minors were fixed at `472a1ba` (one
  request checkout at a time per tab, the D382 echo screen on A4 and A5 lists, gaps in P4, P5, P6,
  P9, P11 and P14). Hunt 3 confirmed one major, which was the open question: methods 4 and 5 failed
  closed on the conventions' audit (D356's row in `p08a`, `p08b` and `p10d`; GitLab's More row in
  `p06a`). The lead ruled the two rows the conventions' one exception and fixed hunt 3's three
  minors (`610b05a`: the fork lookup asserted by its number's path, P11's top label, and
  `OnAccountChanged`'s comment, which now says a replacing sign-in stops the run only after the
  request in flight); part P's 7 methods passed on it. The lead kept `34720c2`'s five areas in one
  commit (splitting would rewrite history) and confirmed `BranchLabelOf`'s third argument (the base,
  required). The diff was searched for token shapes, real addresses and other identities: none.
- **WP-P's merge runs** (2026-11-07, 05:34 to 06:31 on the laptop, on `c5ba918`'s code, through
  `bash scripts/heavy-run.sh`, one command at a time, while WP-N's builder and the build of `master`
  for 0.7's references ran beside them): the 7b project 149 of 149, 7a's 207, App.Tests' seven classes
  131, Hosting's two request classes 20, `GitRepositoryPullRequestsTests` 20, phase 3's
  `Check_03_other_menus`; then the lagged list (1500 ms): every command green (the 7b project 149
  again, 7a's 207, App.Tests' eight classes 66, Phase6's 21, Hosting's 44 and 34) but phase 2's
  `Check_15_amend`, 24 of 25: after the amend's waits for HEAD, the stage panel and the graph, the
  summary box still held the amended subject (`Phase2Checks.Stage.cs:181`); it passed at WP-A's and
  WP-B's merges, and the product clears the box only when the host's `CommitAsync` returns
  (`WorkingChangesViewModel.cs:519`): a race in the check, fixed (below). 12.9's pictures (the plain runs'
  copies in `D:/Projects/Personal/phase7b-work/wpp-pictures/`): 7a's 31 P and G pictures are the same
  files as `phase-7a-pictures.sha256`; of 7a's others only what differed at WP-B's merge differs (the
  23 part A pictures' intro, `b06a`'s port, `n02b`, `n06b`, `n07a`) and `b09a`, whose file differs
  from the list but whose pixels equal the copies from before WP-A and from WP-B's merge. Phase 3's
  `03a` to `03h` differ from `phase-3-pictures.sha256` (phase 3's gate, an older git in the status
  bar's box) and are the same files as 0.7's references. Against those references
  (`scripts/compare-pictures.py`, output in `D:/Projects/Personal/phase7b-work/cmp-wpp/out7a/`), 7a's
  85: 57 the same, the 23 part A pictures in the intro's box alone, `b06a` (the fake's port), `n02b`
  and `n07a` (the progress bar's segment), `n04a` (7 px one level apart) and `a09b`, whose reference
  shows the port 59233 where 7a's list and this run show 47395.
- **Phase 2's `Check_15_amend` under the lag** (2026-11-07; one Opus investigator in
  `D:/Projects/Personal/vc-wt/p7b-amend-wait`, three Opus skeptics by lens: loosening, product,
  reproduction; traces in `D:/Projects/Personal/phase7b-work/verify/amend-wait/`): a race in the
  check, no product defect. A commit ends after its refresh (phase 3's design 1.3, steps 7 and 8), and
  the stage panel empties its boxes, unticks Amend and leaves "Committing…" only then
  (`WorkingChangesViewModel.cs:519-525`). Under the lag the amend's own file events reach the watcher
  after the queue's pause and are folded into the running refresh as one more round
  (`RepositoryViewModel.RefreshAsync`), during which HEAD, the graph and the 6/0 panel already show the
  amend; when that round outlasted the check's two git calls, the assertion ran before the clear (3 of
  60 loaded lagged runs; the unfixed `15b` picture showed the filled box and "Committing…" in 16 of 20
  lagged runs even when the check passed; never without the lag, whose window is about 15 ms). The fix
  (`040eb65`, test only): checks 14 and 15 wait, with the driver's own limit, for the state their
  expected results name and the panel reaches last (check 14 the disabled `Stage files to commit`,
  check 15 Amend not ticked), before their pictures; every assertion stays, and check 15 also asserts
  the empty description ("the boxes empty"). The skeptics refuted nothing; fixed, 35 of 35 lagged runs
  under load passed with `14b` and `15b` the same files as 7a's list. The lead's runs on `26b44c7`
  through `scripts/heavy-run.sh`: `Phase2Checks` 25 of 25 plain (its 66 pictures the same files as 7a's
  list) and 25 of 25 lagged (one picture, `24c`, 21 pixels of the toolbar's labels up to 25 levels
  apart, text anti-aliasing; check 24 is untouched). For the review before the gate: the real-window
  check 14 (`Phase2RealWindowPass.cs:86-95`) saves `14b` after the 7/0 panel without that wait (its
  window without the lag about 15 ms; a change there needs the real-window pass, D246); the boxes and
  Amend stay editable during "Committing…", and the clear then drops what was typed (phase 2's UX,
  older than 7b); a watcher event folded into a commit's refresh keeps "Committing…" for one more
  round (RefreshAsync's contract).
- **Part P's pictures inspected** (2026-11-07, an Opus inspector, the plain run's copies): all 22
  (`p01a` to `p14a`) and part A's `a06c` match their expected results, the two ruled cut rows
  included; `a06c` shows `Sign in to Azu…` behind the card as part P's "Changes to expected results"
  says. One gap in the report's wording, fixed at `8416438`: `manifest`'s one-lane graph keeps its
  Date column by phase 1's rule, which the conventions' sentence on the graph now names.
- **0.7's references** (2026-11-07, in `D:/Projects/Personal/gate7b-reference/`): 7a's gate's
  real-window files, 375 copied from the main checkout's `artifacts/visual/phase-*/real-window/`
  (untouched since 2026-10-31), and phases 0 to 7a's walk-throughs run on `master` (`a29379f`) with
  git 2.56.0.windows.4 in `D:/Projects/Personal/vc-wt/master-ref` through `scripts/heavy-run.sh`
  (`--solution walk.slnx`, the eight walk-through projects, four at once): 750 of 750 in 50 min 25 s,
  1,057 scripted pictures, phase by phase as many as `phase-7a-pictures.sha256` lists. While they ran,
  other runs held 7a's fake's port: the references' `a09b` and `b06a` show a fallback port (59233 in
  `a09b`), which the gate reads as the port's region alone.
- **WP-N's build** (2026-11-07; design 13): built in `D:/Projects/Personal/vc-wt/p7b-create` from
  `c5ba918` by two Opus builders (`b05fabc` to `fefdb6b`: 13.2 to 13.6, the shims of 1.5 removed,
  part N's seven methods), one review with two lenses (eight findings, two major: N8's sign-in dialog
  unasserted, D356 after the sign-in; fixed at `9734d72` and `4184565`) and three hunts by lens
  (tokens, checks, state), each serious finding put to fresh Opus skeptics. Hunt 1 confirmed three
  majors (A3's default branch shown and logged unscreened, D382's echo; D316 against a list not held
  before the POST; N4's git requests asserted for one git version), fixed at `653350a`; hunt 2 seven
  minors, fixed at `6f399d9` to `d831812` (D316 waits for the list read in flight alone). Hunt 3 left
  one confirmed major (open point 45: a drop's given base never asserted; a mutation dropping it
  passed), six minors of the checks lens, and the tokens lens's finding rated minor by its skeptic:
  a network operation started before the tab's first read of its URL rules since a sign-in copies
  D290's empty bindings, so a create pressed within about 85 to 250 ms of a stacked sign-in's form
  pushes without the account's token and git asks for credentials (10 of 10 at once, 9 of 9 after
  300 ms; GitHub on `8cb5ecc` the same, so older than WP-N). The lead's rulings: R1, that window is
  fixed in the product for every network operation (the operation awaits the tab's read in flight,
  or reads the rules at its start as D332's dashboard row does; the tab's own bindings stay empty
  until its read, D290 unchanged), recorded as D388; R2, open point 45's base recorded from the
  form's first moment; R3, the six minors; R4, the report stands on Ada's Login (the fake records it
  on every Azure DevOps request with a valid token but `areas`, `FakeAzureDevOps.Router.cs:164-178`),
  so part N's checks and part P's and B's, where they pass `null` (part P's helper takes it as not
  checked), assert it where the report gives it; R5, accepted as built: `CancelledOf(p)` (a const and
  a method cannot share 7a's name `Cancelled`), `RowRepositoryText` (Choose… at the column's right,
  proven by bounds), the four methods over 0.5's 40 s alone (52, 67, 49 and 50 s on a loaded laptop,
  5.6 to 12.3 s of their own steps; timed again rested after the merge, split by D237 only if their
  own steps pass about 40 s), and `n04a-already-open` (differs from 7a's list, equal to `c5ba918`'s
  own run before WP-N; judged by region in the merge runs). Hunt 3's findings and verdicts:
  `D:/Projects/Personal/phase7b-work/verify/wpn-lead/hunt3.md`.
  The rulings were applied by one fix workflow (two Opus fixers, two Opus reviewers, a fix round
  and an Opus verifier; `8c107c2..774438d`): D388 in the product (`8c107c2`; the auto-fetch's ask
  for the read once an interval after a failed read, `83379c5`, from the review's first finding),
  R2 to R4 in the checks (`d9c20ba`, `426cb83`: `BaseRecorder`, `AssertMenuPlaced`, the brushes on
  every form, the 409 Warnings of the core, the session and the App, `PartN`'s provider names, N8's
  routes with their queries, N7's whole menu, Ada's Login). Each new check and the D388 tests fail
  on mutations or on `d831812`'s code (14 of 14) and pass on the merge's; D388's tests pass on git
  2.43 and 2.56, with and without the lag. The lead's rulings on its questions: the auto-fetch's
  bound of one ask an interval and its Debug line are accepted; the three creates "at once" through
  the app do not always press inside the window (2 of 15 runs in the review did not), so their
  summaries say so and the held and failed variants are D388's proof (a `ShellDriver` seam that
  would hold the app's rules read needs a product change: not now); B7's refresh after a clone keeps
  no Login (its row gives none) and `areas` stays unchecked (the report gives none, the fake reads
  no credential there); the two cases outside `AwaitsUrlRules` that no click reaches, and the wait's
  lack of a time limit of its own (as D332's), are accepted and written into D388. Merged at
  `a6952d2`; D388 recorded and 0.4's lagged list given `SignInBindingsWindowTests` at `cf82d50`.
  Results and evidence: `D:/Projects/Personal/phase7b-work/verify/wpn-lead/`.
- **D388's test on macOS** (2026-11-07, run 42437572741, `macos-latest` alone): both cases of
  `Inside_the_window_a_remote_the_rules_give_another_user_is_bound_to_no_token` failed at
  `Assert.All(prompts, contains "bob@")`: git's one prompt was "Username for
  'http://127.0.0.1:<port>': ", so git no longer had the user the URL rule put in when it asked.
  The test's other assertions passed there (bindings empty, git's requests carry no `x-access-token`
  and none answered 200). Investigated read-only while 13.9's runs held the laptop (one Opus
  investigator, two Opus skeptics, cause and security; notes in
  `D:/Projects/Personal/phase7b-work/verify/ci-42437572741/`). The cause, established and not
  refuted, lies in git and curl, not in the product: git v2.56.0 compares the URL it asked for with
  curl's effective URL as plain text, and curl 8.23.0 (its fix of CVE-2026-94266) strips the user
  from the effective URL, so git takes its own request for bob's URL as a redirect and rebuilds its
  credential from the stripped URL, without bob. Git's fix (`76524c2604bc`, "http: handle curl
  stripping creds from effective url") is on its `master` and in no tag. Homebrew's git 2.56.0_2,
  linked to Homebrew's curl 8.23.0 since 2026-10-28, is CI's macOS git; Git for Windows
  2.56.0.windows.4 (backport), `ubuntu-latest`'s PPA git and the containers' Ubuntu 2.43 carry the
  fix, so macOS alone failed. Reproduced on the laptop with Git for Windows 2.56.0.windows.2 over
  2.56.0.windows.4's curl 8.23.0 DLL. Security: no token went out in this case on any git (D278's
  drop binds nothing for bob's URL). The security skeptic refuted the investigator's wider claim:
  on such a git the broker never sees the other user, so D275's refusal is blind there; design
  16.3's accepted GitHub cases (a reserved character escaped in the binding and raw in the other
  URL) then give the account's token for bob's URL on git 2.56 too (measured on the reproduction),
  and stale bindings or submodule URLs on git 2.31 to 2.45 over such a curl (no CI git is one); the
  token reaches the bound host only. The lead's ruling: a test change (the test measures which kind
  of git runs and asserts the one prompt it gives, git's own `remote get-url`, the server's two
  requests and that no credential request reached the broker) and a risk with D389; no product
  change, since hardening reopens design 16.3, which is the owner's (Waiting on the owner). The fix
  was built in `D:/Projects/Personal/vc-wt/p7b-macos` (`wip/p7b-macos-prompt`, from `93999d6`) by an
  Opus fixer, reviewed by three Opus reviewers by lens (loosening, security, harness) and checked by
  an Opus verifier, and merged at `98cdd86` (D389; D390 for the budgets in the next note).
  `GitUrlUser` measures once per test process whether the tests' git keeps a URL's user (`Kept`) or
  loses it (`Lost`) and writes the measure to `artifacts/visual/git-url-user.txt`, which CI uploads
  for each job (the security reviewer's finding: the change had removed CI's only sign of such a
  git). The D278 test asserts the one prompt the measure predicts, bob's URL from `git remote
  get-url`, the server's exact requests (none, then bob, both 401) and no broker credential line;
  the octo fetch test asserts such a line, so that check cannot pass empty; its other assertions
  are kept word for word. The verifier ran `SignInBindingsWindowTests` 11 of 11 on the laptop's
  2.56.0.windows.4 (Kept), on 2.56.0.windows.2 as shipped (Kept) and on windows.2 over curl 8.23's
  library (Lost, macOS's form), reproduced the old test's failure at `93999d6` on the last (2 of 2
  failed at line 246, as on macOS), and an inverted prediction failed both cases on every git. Notes
  in `D:/Projects/Personal/phase7b-work/verify/ci-42437572741/fix/`. CI 42454621322 on `d127fef`
  passed both cases on macOS, whose measure read Lost, by the server's two requests (the security
  review's open point: the sequence had not been seen on real macOS); every other job read Kept.
  Each job's file is kept in `D:/Projects/Personal/phase7b-work/verify/ci-42454621322/`.
- **WP-LIVE's second stage** (2026-11-08; design 15.6 and its stage 2 As built): built in
  `D:/Projects/Personal/vc-wt/p7b-live2` (`wip/p7b-live2`, from `d127fef`) by the session's
  `phase7b-live2-pkg.js` (the wave-4 script with the package L2): one Opus builder, two Opus reviewers
  (conformance, correctness) whose three minors were fixed with tests (the rehearsal guard's origin
  now checked in its base constructor; GitLab's rehearsal asserts the merge request deleted), and one
  hunt round of three Opus skeptics (screen, guard, flow) that showed nothing: the screen held 26
  Check_3-shaped texts with made-up names; every request `Check_3` sends was traced through the
  guard (no widening needed: the app's 9 requests and the test's own per provider pass it); six
  sandbox states, two injected server errors and a lagged run over the fakes held, each failure still
  cleaning up. `Check_3` (`AppDrivenAsync`) signs in through the provider's dialog, picks the row by
  `FullName` in the browser, clones, checks the section's title, pushes D325's commit, creates a
  draft through the form, reads it back and cleans up in `finally`; `LiveSafetyTests` rehearses the
  same code over `FakeAzureDevOps` and `FakeGitLab` behind loopback-only guards. `LiveSteps.Sums`:
  570 s for each `Check_3`. Merged at `fdb2c05`; the offline classes 428 of 428 there through
  `scripts/heavy-run.sh`; then, the owner told, the classes of Azure DevOps and GitLab with the
  tokens: 11 of 11 in 1 min 26 s (91 s with the build up to date). Azure DevOps: the dialog with
  the Organization box, the browser's one row, the clone (2.4 s, no dialog), the push, draft pull
  request !11 created (201), read back, abandoned and its branch deleted. GitLab: the dialog, the
  browser's five rows, the clone (2.1 s), the push, "Title on GitLab: Draft: …", draft merge request
  !5 created (201), read back, closed, deleted and its branch deleted. Tripwires clean. The lead's
  check of both run.txt files, every decoding searched for the three tokens (whole and any 16
  characters in a row), the organization, the namespace and any address: none. The run files are
  kept in `D:/Projects/Personal/phase7b-work/live/` (`*-stage2-20261108.txt`). Nothing new for the
  fakes: Check_3 sends no probe, and its answers are the shapes stage 1 measured.
- **The review before the gate** (2026-11-08, on `fdb2c05`, the detached worktree
  `D:/Projects/Personal/vc-wt/p7b-review`; the session's workflow `p7b-review-before-gate`, 31 Opus
  agents; results in `D:/Projects/Personal/phase7b-work/review/review-before-gate.json`): six read-only
  reviewers by subject (tokens, git, lifetimes, providers, screens, tests and CI) found 19 problems
  and judged the queued items below; refuters (three for a blocker or major, one for a minor) let 9
  stand: git-1 (a clone's bindings miss conditional includes; one refuter major, two minor; D391),
  tokens-1 (GitLab's next page outside the API path), tokens-2 (7a's GitHub `clone_url` of any
  scheme, a `//host/share` read as a UNC path on Windows), life-1 (a credential request waits for all
  three providers' start reads), life-3 (GitLab's 100 a minute: plan risk 44), ui-1 (D356's row says
  "signed in" for an account that needs sign-in), ui-7 (phase 2's commit boxes: phase 8), tests-2
  (the growth tests' message) and tests-5 (0.6's timing report not taken). Refuted: providers-1 (two
  to one; the design's choice, now in 16.3), life-2, ui-2 to ui-6, tests-1, tests-3, tests-4. The
  queued items' judgements: D387's reading stays the dialog's; the palette flake is not explained by
  the Ubuntu-packages container (it failed on macOS too, 6.7 in 42386428809, and three failures in five
  jobs fall two in one job by chance about half the time), so its runs now leave their measures in a
  file CI uploads; the watcher lag tests cover every new class that starts the app; the default run's
  1 h 37 min is `VisualCommit.App.Tests`' own time under contention (it gained about 13 tests since
  7a; Hosting.Tests grew from 17 to 46 min of its own and Git.Tests to 56); a06c is settled (part P's
  recorded change); a11b stays as built. The lead's rulings: D391 and D392, plan risks 43 (extended)
  and 44, design 16.3's new bullets, the report's open point 5.
- **The review's fixes** (2026-11-08; the session's workflow `p7b-review-fixes`, 14 Opus agents: a
  fixer per area in `D:/Projects/Personal/vc-wt/p7b-fix-{hosting,clone,panel,tests}`, from `77a76cb`,
  each fix reviewed by another agent, refixed where the review found something and checked again).
  Merged with `--no-ff` at `b4117fc` (hosting), `eedffe3` (clone), `8bf5e99` (panel) and `6333de6`
  (tests); design 16.3's appended bullets were the only conflict, both sides kept. Each fix has a test
  shown to fail without it. Hosting: tokens-1 (`HostingHttpProfile.FollowsPage`, GitLab's decoded
  API-path rule), tokens-2 (`GitHubProvider.WebAddress`), life-1 (each provider's `StartRead`); its
  review found nothing. Clone (D391): `IGitConfiguration.ReadIncludedAsync` and `GitConfigIncludes`;
  an include path the app cannot work out as git would (`~user/`, `%(prefix)/`) fails closed as
  unreadable. Its review found two problems, both fixed: the rewrite tests asserted `username=alice` in the
  user's helper, which a git that loses the URL's user (CI's macOS, D389) never sends, so that
  assertion is made only where `GitUrlUser` reads Kept and alice's request at the server is asserted on
  every git (the assertion was the first fixer's, never merged); and two includes that rewrite the URL
  only together, by a tie of equal prefixes, were missed, so any included rule matching with a prefix
  at least as long as the outside's longest now binds nothing too. Panel (ui-1): the texts are in
  design 12.2's table and the report's list; `RepositoryHosting.Apply` now raises `Changed` when the
  listed provider's account alone changes its signed-in state (without it a refusal while GitLab is
  signed in left the row unchanged; no covered tab gets a new `Changed`); its review's minor point,
  the wiring test's two apps not ended through `End` (the token tripwire) and a `using`, is fixed. The
  panel's refixer found the fixer's build stale (a scratch revert put back with its old time, so an
  incremental build kept the old DLL); the main checkout's merge writes every changed file anew, and
  its build is the one the lead's runs use. Tests (tests-2): `GrowthMeasures.WithoutCollection` blames
  the budget only when the measure allocated it, else names the memory load (a fixer's run on the
  laptop saw 16.2 GB against the 15.4 GB high-load threshold); the palette test's measures moved to
  `PaletteGrowthWorks`, kept as each returns, written to `artifacts/visual/palette-growth.txt` and
  given in every failure; its bound, rounds and sizes unchanged; its review's minor point (a failed
  measure dropped its round's small work and its message the data) is fixed. Pictures: 7a's part B
  (8 of 10 the same files, b06a and b09a varying by nature, their pixels equal to earlier copies) and
  7b's part P (22 of 22 the same files at the panel's fix; on the refixer's fresh build 21, `p11a`
  differing in 5 pixels of the status bar's git box, its recorded variation) were run by the fixers;
  no fix changes a scripted picture.
- **D394 and D395** (2026-11-08; the session's workflows `p7b-fake-github-404` and `p7b-d395-review`,
  seven Opus agents in `D:/Projects/Personal/vc-wt/p7b-fake-github`, branch `wip/p7b-fake-github` from
  `d85d3db`; merged with `--no-ff` at `cb9af6d`). The builder (`affc0e4`, `83168b0`, D394): the fake of
  GitHub answers an unknown revision beside `author` 404 with GitHub's body, byte for byte; D342's empty
  500 stays as an option a test sets (`FakeGitHub.UnknownRevisionBesideAuthorAnswers500`), so that the
  two tests that drove the lookups through the fake's 500 are Theories over 404 and 500. A temporary
  record of every commits request through 7a's and 7b's walk-through classes found no lookup of a commit
  the fake lacks, so no picture changes. Its two reviewers: no test loosened, five mutations each caught;
  and, major, on github.com a fork's lookup of a commit the fork lacks now takes the fork as unreadable
  (D339's 404 rule), so in a fork's tab one stale branch leaves every later address on the fork without a
  picture in every session, D342's defect back. The refix (`acefefc`, `2562ad8`): a fork's lookup through
  the fake tested, D394's fork sentence marked unmeasured, plan risk 38 and the architecture brought up to
  date; and D395, a 404 in a repository not known to be readable followed by one read of it (A3's
  request). The owner approved D395 (2026-11-08), asked because it amends D342 (2). Its two reviewers
  (`p7b-d395-review`): bounded (one read a repository and call; the listed repository never read), no
  side effect on the tab, no picture changed (7a's parts A, B, G, N and P at both commits, the
  differences the report's "varies by nature" ones), and three minor points: a read the token fails must
  stop the lookups, a fork's tab need not read its own fork, and `2562ad8`'s message claims too much of its
  500 rows. The refix (`af31667`): the read's 404, 403 and 5xx leave the repository out, any other
  failure stops the lookups; the avatar run passes the candidate and A3's repository as readable, so the
  read is sent only after a 404 on a third remote, which no scenario has; D395's row says exactly which
  tests fail on which code. Its runs: the hosting classes 156 of 156, the live command's offline classes
  582 of 582, 7a's part P pictures all equal to `phase-7a-pictures.sha256`.
- **The gate's first live run** (2026-11-08, from 13:58 UTC, on `ee0aeeb`; the report's "Live command"
  holds each provider's paragraph). The whole project passed 595 of 596 tests; GitLab's `Check_2` failed in
  the push (60 s). The class alone at 14:02 UTC failed `Check_2` in the load (a request with no answer in
  30 s); at 14:05 UTC it passed 5 of 5. A tokenless loop of plain requests to gitlab.com beside the
  third run saw one stall of its own: connected over IPv6, then no answer for 20 s. So the cause was the
  network, not the app or the command, and nothing was changed for it; the counted round's live run may
  meet it again, and is run again if so. GitHub's probes found that a commit it lacks, asked for beside
  `author`, is now 404, where the fake answers D342's 500 (D394, being built). Every run file and console
  output was screened, counts only, by `screen_live.py` in the session's scratchpad: no token, no 16 of
  its characters in a row, no organization or namespace, as written or decoded. Copies are in
  `D:/Projects/Personal/phase7b-work/live-gate/`.
- **D393's guard** (2026-11-08; the session's workflow `p7b-gitlab-guard`, four Opus agents in
  `D:/Projects/Personal/vc-wt/p7b-gitlab-guard`, branch `wip/p7b-gitlab-guard` from `c233091`; merged
  with `--no-ff` at `ee0aeeb`; design 15.9's two "As built" notes). The builder (`67c691e`): below
  `projects/` the guard admits the sandbox alone, by its escaped path (decoded once, ASCII, without
  case) or by its id once learned from a 200 to a GET of the sandbox by path whose own `id` and
  `path_with_namespace` name it (the log reads that one answer's body, buffered whole, 10 MiB at most);
  every number is refused before that and every other number after it; a refusal names the origin
  alone; 79 new cases, all failing against the old rule. Two Opus reviewers: "bypass" (38 crafted
  addresses with each method, 24 mutations in a private copy; no way to another project's path or id)
  found that the query and a write's content were not judged (a `target_project_id` there), minor;
  "live-path" (every request of Check_1 to Check_3 walked, the app's and the command's) found the
  rehearsal did not run as Check_1 leaves the run, where the app's own read alone teaches the id,
  minor. The refix (`9a5800f`): no query on a write; under a project a query parameter that may
  name a project (`id`, `…_id`, `…_ids`, read as Rack reads a key) names the sandbox, as a next
  page's `id` does; content only on POST and PUT, one JSON object as `application/json`, a top-level
  member that may name a project null or the learned id; the rehearsal is a theory, fresh and after
  Check_1, the second shown to fail under the reviewer's regression; 73 new rows, 66 failing without
  the fix. Still refused, by design: a merge request from a fork open in the sandbox (the app's look
  at the fork's project) and a moved sandbox (its redirect to `projects/<id>`). The offline classes
  pass 582 of 582 on `ee0aeeb` (54 s, Release).
- **WP-R** (design 14; 2026-11-08, `D:/Projects/Personal/vc-wt/p7b-real-window`, branch
  `wip/p7b-real-window` from `8649316`, one Opus builder): written without a build while the lead's
  timed runs ran, then built: `dotnet build tests/VisualCommit.RealWindowTests -c Release` and the
  root, 0 warnings, at the first try; `Phase7bExpectationTests` 24 of 24. Five files,
  `Phase7bRealWindowPass{,.Accounts,.GitLab,.AzureDevOps}.cs` and `RealApp.Phase7b.cs`; the five
  launches in `Phase7bOrder`, 13 captures under their twins' names (`a02d`, `a05b`, `a06b`, `b03a`,
  `b03b`, `p01a`, `p01c`, `p10a`, `p10c`, `n02a`, `n02b`, `n06a`, `n06b`), every R step of A2, A5, A6,
  B1, B3, B7, P1, P10, N1, N2 and N6; its departures, some stricter than 14.4, are 14.5's As built. The
  review (the session's workflow `p7b-wpr-review`, three read-only Opus reviewers by lens: conformance
  to 14.4 and the twins, robustness in the real window, safety) found nothing; the robustness lens
  read that Avalonia 12.1.3's Win32 bridge serves the Scroll pattern `ScrollAccountsToEnd` reads.
  Merged at `f016bdf`; CLAUDE.md's rows of 14.5 added. Not run: the pass takes the mouse, so its
  first run is D246's at the gate.
- **The lead's runs after the review's fixes** (2026-11-08 03:35 to 04:42 local time, on `6333de6`,
  one at a time through `scripts/heavy-run.sh`, nothing else on the laptop; log and xUnit reports in
  `D:/Projects/Personal/phase7b-work/timing/`), all passed: `VisualCommit.Hosting.Tests` with its
  timing report 1,522 passed and 4 skipped (the real keychains') in 13 min 42 s alone (46 min of its
  own beside the others in WP-N's default run); `VisualCommit.VisualTests.Phase7b` with its timing report
  179 in 3 min 47 s; 7a's walk-through 207 in 3 min 24 s (its GitHub code changed); `GitConfigIncludeReadTests`
  and `GitConfigReadTests` 35; the growth classes, `GitSettingsViewModelTests` and
  `SettingsDialogViewModelTests` 98; under `VISUALCOMMIT_WATCHER_LAG_MS=1500` 7b's walk-through 179,
  7b's six Hosting classes (0.4) 75 and 7a's D334 six 51; then `VisualCommit.App.Tests` alone with
  its timing report, 2,233 in 28 min 24 s (59 min 31 s of its own at 7a's gate, under contention;
  its slowest, all earlier phases', 30 to 63 s with their templates). Design 0.6's split (the
  review's tests-5): the tests of 25 s or more in the two reports (Hosting.Tests' five, at 61, 46,
  46, 38 and 37 s, and the 7b project's three, at 31, 29 and 25 s, with two just under) were timed
  alone (`--filter-method`, each its own run): only `FakeAzureDevOpsGitTests.A_repository_named_with_a_final_dot_git_is_public_pushed_to_and_given_its_merge_ref`
  took 25 s or more, 28.4 s, of which about 18 s is the "hosting7b" template that the first test of a
  process builds (the same run's GitLab cancel case took 20.5 s, its two Azure DevOps cases 2 s
  each); its own work, one repository's clone, listing and push, is about 10 s, one scenario, so D237
  splits nothing; the others took 11 to 24 s alone, template included. The times in the reports are
  run beside the project's other tests and include the template's wait.
- **`ShellDriverTests`' wait on CI** (run 42472105974 on `6333de6`, macOS alone):
  `WaitForAsync_times_out_with_a_message_that_says_what_it_waited_for` (phase 1's, `84550a8`, never
  failed before) found "It looked at the condition 2 times": its wait of 0.3 s, at a 15 ms interval
  about twenty looks, had room for two under a stall of the UI thread or the thread pool's timer while
  `VisualCommit.App.Tests` ran beside the other projects (37 min 50 s there). The fix, a test-only
  change: the wait is 2 s, its message and both assertions as they were (more than two looks, the
  time up), the failure now lists when each look came. Passed alone in 6 s.
- **For the review before the gate** (step 17; collected 2026-11-01 to 2026-11-08 from the
  packages' hand-offs, the inspections and the CI runs): GitLab's 100 requests a minute against
  the app's lists; whether the providers' own refusals in `src/VisualCommit.Hosting/` should use the
  sign-in dialog's reading (D387); `a11b`'s focus; `a06c`'s "Sign in to Git…"; `WithoutCollection`'s
  "the budget is too small" when a collection comes from memory pressure; phase 2's items
  (real-window check 14, the boxes editable while committing, the length of "Committing…"); the
  `PaletteGrowthTests` flake (two CI failures, both in the container with Ubuntu's SDK packages);
  the windows of the watcher lag tests; the D388 seam in `ShellDriver`; `n05b`'s GitLab error that
  wraps inside `origin/main`; the anti-aliasing forms of `l27e-menu-item-tooltip-dark` and phase 3's
  `15b3`; the default run's 1 h 37 min against CLAUDE.md's "about 1 h 10 min"; D390's
  `WatcherRefreshes` seam in the app; and the live guard's GitLab project scope, if the owner's
  answer on the account's five projects asks for it.
- **WP-N's merge runs** (2026-11-07, 18:58 to 21:15 on the laptop, on `cf82d50`'s code, through
  `bash scripts/heavy-run.sh`, one command at a time, the session's `wpn-runs.sh`): the lagged list
  (1500 ms) green but one test: phase 2's 25 of 25, App.Tests' eight classes 66, Phase6's 21, the 7a
  project 207, 7a's Hosting classes 44, 7b's Hosting classes with `SignInBindingsWindowTests` 74, and
  the 7b project 175 of 176: `Check_P11_azure_checkout_of_11_and_13s_reason` saw one whole refresh
  with no operation id (the seven refresh forms once each) in "!13's double-click"'s exact budget of
  0 (`Phase7bChecks.P.Checkout.cs:196`). A write reported late, as the lag stands for macOS, can put
  a whole refresh into any step (the watcher's `IsQuiet` cannot know a held event), and the recorded
  rule is to let such refreshes through only whole (7a design 5.3, D280, D281), as phase 6's checks
  do with `GitCallBudget.AssertOnlyWholeRefreshes(await app.QuietCallsSinceAsync(mark), …)`; the
  same exact 0 stands in `RequestOperationsProvidersTests.cs:172` and `:339`. The fixer of D388's
  test (above) stopped at that form: the queue's refresh after an operation runs once the
  operation's scope has ended, so its calls carry no operation id, and it would have let a refresh
  of the step's own through. The three went instead to `AssertNoStepCallsAsync` (D390), which sets
  aside only as many whole refreshes as the watchers asked for and fails on any operation; with the
  lag P11's step saw 7 calls, one whole refresh, the watchers' count 1, in three of three runs, and
  passes; a refresh added after !13's checkout fails with and without the lag. The merge's runs
  (2026-11-07, 23:13 to 23:23, on `98cdd86`, git 2.56.0.windows.4, through
  `bash scripts/heavy-run.sh`): `SignInBindingsWindowTests` with `RequestOperationsProvidersTests`
  25 of 25, `GitCallBudgetTests` with `RepositoryWatcherQuietTests` 10 of 10 and part P's
  `Check_P11` 2 of 2, each plain and with the 1500 ms lag; the measure read Kept. Then the whole default run, 7,817 tests, 7,813 passed,
  4 skipped (the real keychains' tests), none failed, in 1 h 37 min 45 s at four projects at once
  (`VisualCommit.App.Tests` ended last, 1 h 32 min 56 s of its own; 7a's gate took 1 h 12 min 18 s
  for 5,952); then 7a's `Phase7aChecksN` alone 7 of 7 and phase 3's walk-through alone 82 of 82
  (8 min 10 s). Their pictures are kept in `D:/Projects/Personal/phase7b-work/wpn-pictures/`
  (`default/` and `alone/`). 13.9's comparison (a Sonnet comparer with `scripts/compare-pictures.py`
  and SHA-256, an Opus judge who opened every changed pair with its crops; output in
  `D:/Projects/Personal/phase7b-work/cmp-wpn/`, `judgement.md`): against 0.7's references, phases 0
  to 7a, every picture there on both sides; changed 4 of phase 1's 43, 12 of phase 3's 265, 1 of
  phase 4's 122, 5 of phase 5's 264, 17 of phase 6's 198 and 27 of 7a's 85 (phase 0's and 2's all the
  same files); alone, 2 of 7a's 20 part N pictures and 19 of phase 3's 265; 7b's 71 earlier pictures
  against WP-P's merge copies, 3 changed, and part N's 22 new. All 90 allowed, none failed: part A's
  23 in the intro alone (the report's change), the fake's port (`a09b`, `b06a`), temporary folders'
  names, carets, a held fetch's percentage and bar (phase 3's "any N"), the indeterminate bar's
  segment, and anti-aliased edges and icons of 1 to 25 levels (7a's ruling 19); nothing in the git box
  or right of x 485 in the status bar, no offer anywhere. `n04a` (7a's): 7 px one level apart, two
  forms on the same code, the current one 7a's list's file, so WP-N changed nothing there.
  Anti-aliasing forms not seen before, for the gate's inspection: `l27e-menu-item-tooltip-dark` (8 px,
  the lane's curve, up to 22 levels) and phase 3's `15b3` (22 px up to 25 levels) beside `15b2`. Part
  N's 22 pictures inspected (an Opus inspector, `part-n-inspection.md`): all pass. Its remarks: the
  report's notes missed two carets (`n01a`, `n04c`), named at this commit; `n05b`'s GitLab error
  wraps inside `origin/main` (whole, cosmetic; for the review before the gate); `n07a` and `n07b`
  keep N5's failed status text, as the method's order gives it.
- **The watcher's dispose test on CI** (2026-11-07, run 42386428809, `windows-latest`):
  `RepositoryWatcherTests.Nothing_is_raised_before_start_or_after_dispose` counted two `Changed`, not
  one, 2.9 s in, with seven other projects on the runner. The test runs the watcher on the wall clock
  (quiet period 250 ms) and writes "just-before-dispose" just before `Dispose`; `Dispose` does not
  wait for a raise in progress (`RepositoryWatcher.cs:174`), so a test thread held back longer than
  the quiet period, or a late second event of the first write, would raise a second time before the
  dispose. Established (one Opus investigator, three Opus skeptics, two follow-up rounds each with a
  verifier; experiments in `D:/Projects/Personal/phase7b-work/verify/watcher-dispose/`): a race in
  the test, not the product. A hold of the test thread of 250 ms or more, inside or after the write
  of "just-before-dispose", or a second notice of the first write after its raise followed by such a
  hold, raised a burst before `Dispose`; a 400 ms hold injected there failed the test every time with
  CI's message, while natural load on the laptop never did (0 of about 400 runs and traced rounds).
  Fixed on `wip/p7b-watcher-dispose` (`80d5f41..8cc8c00`), merged at `abc6df7`:
  - The test runs on `QueuingClock` (a held clock whose due callbacks wait in a queue for the test,
    as a timer's wait for the thread pool) and moves it by the maximum delay before each assertion;
    its three assertions are kept word for word. A second test disposes while the change still waits
    for its quiet period (the case the original named; one of the two catches each of the
    skeptics' mutations of `Dispose` and `OnTimer`).
  - `A_burst_of_changes_raises_changed_once` and `It_is_not_quiet_from_a_bursts_first_change_until_its_raise_has_ended`
    wait in real time until the watcher has counted nothing for 1 s before the clock moves: a late
    notice between their two advances (Quiet minus a tick, then a tick) re-armed the burst; it
    failed the first once on the laptop under load, and failed both every time when injected.
  - One product fix: the watcher's ignore check (`AllIgnored`) passed `_lifetime.Token`, whose
    getter throws once `Dispose` has disposed the source; the check took that as "git cannot say"
    and the burst was raised after `Dispose` returned. It passes `_lifetimeToken` now (the burst is
    dropped), tested through an internal hook in `EventCounted`'s style (`RaisePassedChecks`).
  - `IRepositoryWatcher.Changed`'s doc says a raise already under way can finish after `Dispose`
    returns, with only changes counted before `Dispose` marked the watcher disposed.
  - Left: the lag tests (`:234`, `:259`) keep 1 s and 2 s windows on the system's clock by design
    (D262); the laptop showed a 1.1 s stall inside a write under file load. Recorded, not changed.
- **The three failures of 42339567989** (2026-11-07; one Opus investigator and two Opus skeptics
  each, none refuted, minors only; the lead's rulings at the merges):
  - B7 below git 2.46 (both containers): git 2.43's clone to an `<organization>@` URL sends its first
    request without credentials (401), libcurl's own retry with the URL's user and an empty password
    second (401), and the helper's `x-access-token` third (200); design 8.6's "first" meant before
    the helper (r7 H4b, g3 G2.2's H2). B7 now asserts those three in order, stricter than the one it
    asserted; the report's part B convention and B7 changed, with the reason under "Changed in this
    report after its first commit". Reproduced on the laptop with Git for Windows' 2.43.0 first on
    `PATH` (`D:/Projects/Personal/phase7b-work/verify/git243/git-2.43.0-hb`, kept for such runs):
    `Phase7bChecksB` 6 of 6 there after the fix. Ubuntu's patched curl and git were read, not run;
    the next CI run's containers measure them.
  - The sign-out's end (the container with Microsoft's SDK): a reopened block could read not busy
    before its account and note were set (2,000 runs of the old test: 10 failures). The production
    code now ends a sign-out in one place, `AccountBlockViewModel.EndSignOut`, busy last in a
    `finally`; the tests read the state in `IsBusy`'s change (two tests failed every time before,
    pass every time after). Design 10's As built says so.
  - A9 (Windows): a refusal swaps the session's slot before it posts each hosting's change to the
    UI thread, so the check's waits could end before hello's hosting took `SignedOut`. A9 waits for
    that state in steps 2 and 5 (`a09b` after it); no production change. The report's part A
    "Earlier tests" says so; 7a's report is unchanged.
  - At the merges, through `heavy-run.sh` on 2.56: the 7b project's `Phase7bChecksB`,
    `AccountsProvidersViewModelTests` and `Phase7bChecksA` 30 of 30; 7a's `Phase7aChecksA` and
    `AccountsSettingsViewModelTests` 27 of 27; `SignInEntryProvidersTests` and `SignInEntryTests` 13
    of 13; `SettingsDialogViewTests` 11 of 11; and on 2.43.0 `Phase7bChecksB` 6 of 6. Part A's and
    part B's 49 pictures are the same files as WP-B's merge run's; of 7a's 24 part A pictures only
    `a09b` differs from 7a's gate list, in 2 pixels by one level at (350, 206), anti-aliasing.
- **Part A's pictures inspected** (2026-11-06, an Opus agent, after WP-A's merge runs): all 29 match
  their steps, no defect; 7a's 28 changed pictures as the lead read them (the intro's box only in the
  23 part A ones). Its notes, for later: the report's "about 553 of 495" for three signed-in blocks
  measures about 562 (a02c to a02d scroll 67 px; A2 step 6, the conventions' "Scrolling" and open
  point 2 to say so, after part B's writer); in `a06c`, behind the backdrop, mobile-app's Pull
  requests section reads "Sign in to Git…", cut by the card, likely 7a's GitHub wording on an Azure
  DevOps repository: WP-P's region, for WP-P to check; in `a11b` no control shows a focus ring after
  the organization's local refusal (open point 5 leaves it open; for the review before the gate).
  Its crops and scripts are in `D:/Projects/Personal/phase7b-work/inspect-wpa/`.
- **GitHub's push protection blocks a token-shaped literal** (2026-11-06): the first push of WP-A's
  merge was refused because `SignInDialogProvidersTests` held made-up GitLab values of a real token's
  shape (`glpat-` and 20 characters). WP-A's branch was never pushed, so its 17 commits were rewritten
  to split each such literal (`"glpat-" + "…"`; `git filter-branch`, the old ones kept on the local
  branch `backup/p7b-accounts-before-split`) and the merge redone (`b6de3cf`; the first, `da7a296`, on
  `backup/phase-before-split`). Every builder's brief says so from now on: a made-up token of a real
  token's full shape is written split, as the scenario's `"ghp_" + new string('F', 36)` already is.
- **Passed on to later packages** (from WP-S's hunts, 2026-11-02): WP-N (section 13) makes the
  create's two log lines (`RepositoryOperations.CreatePullRequest.cs:293` and `:403`) write
  `baseRepository.FullName` through `HostingHttpClient.ScreenEmail`, with a test of a create into an
  Azure DevOps parent whose name is address-shaped (D368; an echoed token cannot reach them since
  WP-S's S1). WP-LF may give `FakeAzureDevOps` a configurable web base, so that
  `ProvidersSessionSetup`'s `ServiceAnswers` shim can go (WP-S's builder's question; optional).
- **Wave 3's closing runs** (2026-11-02, on `a924cda`, through `bash scripts/heavy-run.sh`, one at a
  time): the lagged list with `VISUALCOMMIT_WATCHER_LAG_MS=1500`, 01:37 to 01:52, all passed (phases
  0 to 2's part 2, 25; the App.Tests classes, 66; phase 6's, 21; 7a's walk-through, 207; 7a's six
  wiring classes, 44; 7b's `RepositoryHostingProvidersWiringTests`, 2, the only 7b class of its line
  that exists yet); then the whole default run, 01:52 to 03:12: 7,561 tests, 7,557 passed, 4
  skipped, none failed, 1 h 20 min 21 s. MTP started Hosting.Tests, Phase6Look, Phase7a and
  Git.Tests. Projects ended after: Phase6Look 10 min 17 s, Phase7a 14 min 42 s, Phase7b 17 min 27 s,
  Hosting.Tests 20 min 2 s, phases 0 to 2's 31 min 39 s, Git.Tests 51 min 43 s, Phase3 55 min 46 s,
  Phase5 56 min 25 s, Phase4 70 min 4 s, Phase6 77 min 45 s, App.Tests 78 min 37 s (61 min 10 s of
  its own), Phase5Undo 80 min 16 s (16.2). The logs are `$TMP/p7b-runs/lagged-w3.txt` and
  `full-w3.txt`.
- **Wave 2's whole default run** (2026-11-01, 15:01 to 16:17, on `c0af1be`, through
  `bash scripts/heavy-run.sh`, four projects at once): 7,494 tests, 7,490 passed, 4 skipped (as
  wave 1's), none failed, 1 h 14 min 20 s. MTP started App.Tests, Phase7a, Git.Tests and
  Hosting.Tests first this time (16.2: the order is not fixed). Projects ended after (from each
  one's own time and the order they started in): Phase7a 15 min 26 s, Hosting.Tests 17 min 12 s
  (+1 min 38 s on wave 1), Phase7b 17 min 58 s (2 min 32 s), phases 0 to 2's 28 min 54 s, Phase5
  42 min 52 s, Git.Tests 50 min 7 s, Phase4 60 min 15 s, Phase3 65 min 18 s, App.Tests 67 min 28 s
  (all its own), Phase6 68 min 35 s, Phase6Look 69 min 15 s, Phase5Undo 74 min 12 s. The log is
  `$TMP/p7b-runs/full-w2.txt`.
- **Wave 1's whole default run** (2026-11-01, 06:49 to 08:07, on `6586066` with `e14c3ab`'s docs,
  through `bash scripts/heavy-run.sh`, four projects at once): 6,862 tests, 6,858 passed, 4 skipped
  (the real keychains' three and `Off_Windows_…`), none failed, 1 h 16 min 56 s. MTP started
  Phase5Undo, Git.Tests, Phase6 and Hosting.Tests first, and App.Tests when Hosting.Tests ended, so
  the run ends at Hosting.Tests' time plus App.Tests' own (16.2). Projects ended after: Hosting.Tests
  15 min 34 s, Phase6 37 min 49 s, Phase7b 41 min 24 s (3 min 35 s of its own, WP-F's tests),
  Phase5Undo 45 min 49 s, Git.Tests 47 min 52 s, phases 0 to 2's 55 min 22 s, Phase4 56 min 4 s,
  Phase6Look 62 min 6 s, Phase5 69 min 57 s, Phase7a 71 min 2 s, Phase3 74 min 21 s, App.Tests
  76 min 53 s (61 min 20 s of its own, 59 min 31 s at 7a's gate). Before it the lead ran
  Hosting.Tests (724 passed, 4 skipped, 3 min 52 s), 7a's walk-through (205 of 205, its pictures
  differing from 7a's list only where they vary by nature: n02b and n07a's progress bar, b09a and
  n06b's caret, b06a's fallback port) and the lagged list with `VISUALCOMMIT_WATCHER_LAG_MS=1500`
  (all passed, 06:32 to 06:49). The log is `$TMP/p7b-runs/full-w1.txt`.
- **CI found what the laptop could not** (run 41311809889): .NET off Windows and other git and curl
  builds. MinGit 2.56.0.windows.2, CI's Windows git, lies in
  `D:/Projects/Personal/phase7b-work/research/scratch-g3-git/git-2.56.0.2/`, given the install's
  `git-http-backend.exe` so that `AuthRemoteServer` runs under it: put its `cmd` first on `PATH`
  for one class run (plain `dotnet test`) to see CI's Windows git here.
- **WP0** (`e8a275d` and its review's fixes) was built in the main checkout. The readings that
  differ from the design's text, or settle a choice it left open, are in the design's 1.10, "As
  built". The review accepted these other readings:
  - Only 7a's GitHub forms are wired. `TabServices.SignIn` is the method group
    `MainWindowViewModel.SignInAsync`, which answers false for any provider but GitHub until WP-A.
    The settings screen's `SettingsScreenServices.SignIn` stays a `Func<Task<bool>>` set to
    `() => SignInAsync(HostingProvider.GitHub)`. `SignInToHostAsync`'s body still signs in to
    GitHub, and WP-P owns it.
  - `OpenPullRequestsInBrowserAsync` goes through `ProviderOf(repository.Provider)`, which is
    GitHub's for every 7a repository. `GitHubProvider.Covers` is by provider and host, without case,
    and ignores the owner.
  - `NullHostingSession` declares no GitHub-named member: the interface's default bodies answer for
    them. Its off failure is 7a's "GitHub is off." for GitHub and "{Name} is off." for the others,
    with `Provider` set.
  - `SignInEntryTests`' `EntryOf` wraps `SignIn(HostingProvider.GitHub)` in a `Func<Task<bool>>`,
    so its callers are unchanged.
  - `PullRequestRefs.IsFetchableHead` guards the length. For `refs/pull/head` 7a's check threw
    `ArgumentOutOfRangeException`, which is an `ArgumentException`, so the fetch still refuses it
    the same way.
  - `TokenFormOf` keeps case, so a lower-case `azdo` mark gives null.
  - `PullRequestRefsTests` and `HostingContractsTests` pin nothing WP-C changes: no new providers'
    `SourceCanExist`, no `refs/merge-requests` head, no stub.

  Tests run (plain `dotnet test`, the classes WP0 touched): in Hosting.Tests the builder's 18
  classes passed, 273 and 67 tests. In the first of four runs of the 67, one test failed, and which
  one was not kept. It did not come back in the next three runs, nor in the review's run of the
  session, sign-in, palette and wiring classes (340 tests). WP0 changes no timing, so if a test of
  those classes fails in wave 1's default run, suspect a flake under load that was there before.
  Git.Tests' `PullRequestRefsTests`, `CredentialBindingSelectTests`,
  `GitRepositoryPullRequestsTests` and `GitIsolationTests` passed, 87 tests. Seven App.Tests
  classes passed, 27 tests, and six classes of the Phase7a project passed, 67 tests, none of
  which saves a picture. `Phase7aChecksP` and `Phase7aChecksG` (the rename alone) passed, 8 of 8,
  and their 31 pictures are byte for byte those of `docs/test-reports/phase-7a-pictures.sha256`.
  That run emptied `artifacts/visual/phase-7a/scripted/`, which now holds only those 31; the next
  run of 7a's walk-through writes it again. `RealTokenStoreSteps`' new Azure DevOps key is written
  but not run: only CI's real-store steps run it. After the review's fixes, `HostingOptionsTests`
  and `HostingSmokeTests` passed (36 tests), and the root build and the live project's Release
  build had no warnings.
- `master` holds 7a; the working tree was clean, and no worktree,
  `wip/*` or `worktree-*` branch was left, when phase 7a was merged (`019e68a`). Phase 7a's gate
  references and work lie outside the repository: `D:/Projects/Personal/gate7a-reference/` (phases
  0 to 6's pictures on `master` with git 2.56, the earlier pictures' references) and
  `D:/Projects/Personal/phase7a-work/` (the research notes, the design's drafts, the review's
  findings, the lead's rulings, the live runs' `run.txt` copies and, in `gate/`, the gate's logs,
  the counted round's copy `round-a2c312a/` and the inspection's verdicts). Phase 7b's gate makes
  its own references on `master` if it changes earlier pictures in many places (phase 7a's
  handoff, "Running the visual gate").
- **The review before phase 7 was applied on 2026-10-28** (D239 to D248, on `master`): the build
  trim (`Directory.Build.targets`, D239), the owner's Claude Code settings and a one-time clean-up
  of disk (D240), the split of phase 7 and the weekly allowance (D241), the rules for agents, the
  design, the checks, the test runs with the heavy-run lock (`scripts/heavy-run.sh`), the
  real-window rounds and the inspection (D242 to D247; `plan.md`'s "Workflow" and "Visual test
  gate", `CLAUDE.md`), and the development machine's git 2.56 (D248). The review's other proposals
  stay proposals. D240's connectors switch did not keep the claude.ai connectors out of the
  sessions that followed (phase 7a's sessions still listed them; Waiting on the owner). The brief, `before-phase-7-review.md`, was deleted (it is in
  `be63d89`).
- **The build output is trimmed** (D239): on 2026-10-28, after a clean rebuild of the main tree
  (`dotnet build` at the root and the real-window project's Release build), the 40 `bin` and `obj`
  folders under `src/` and `tests/` that were built again took 0.64 GB instead of 6.96 GB (the
  other 8, 2.27 GB of outputs nothing local runs, were removed and not built again), a test
  project's `bin/Debug` about 49 MB instead of about 589, and D: had 27.2 GB free. The trim takes
  every package's files for other platforms on Windows, not only SkiaSharp's and HarfBuzzSharp's. The real-window pass ran on the trimmed
  build at phase 7a's gate, 72 of 72, and CI's Windows job builds and tests it on every push. If
  either shows trouble with the part that keeps only the machine's platform, the `.pdb` part
  alone is the fallback (D239).
- **The development machine's git is 2.56.0.windows.4 since 2026-10-28** (D248; the owner updated
  it from 2.36.0.windows.1), first on `PATH` (`C:/Program Files/Git/ucrt64/bin/git.exe` in Git
  Bash, `C:\Program Files\Git\cmd\git.exe` from PowerShell, the same version). CI's jobs
  run Windows 2.56.0.windows.2 (since 2026-10-15; 2.55.0 before), macOS 2.56.0 (Homebrew),
  `ubuntu-latest` 2.56.0 (since 2026-10-29, run 40848974456 on; 2.55.0 before) and the two
  containers 2.43.0, so no machine runs 2.36 any more and the
  checks that assert git's own words only on 2.36 assert them on none (plan risk 26). On 2.56 a
  `git stash pop` refused by local changes exits 128, not 1: phase 3's check 15b accepts both
  (`281dd69`), as phase 5's report records under "Changes to expected results" (phase 3's report
  says "exit 1" in 15b's step 2 and in T5). CI's containers are the only check on 2.43, and since
  `ubuntu-latest` moved to 2.56.0 no machine runs 2.55 but Visual Studio's trimmed one below
  (plan risk 26). The status bar now shows `Git 2.56.0`, so earlier pictures that show
  it differ in that box from the lists of pictures of phases 1 to 6's gates.
- **Git 2.55 (CI's `ubuntu-latest` version until 2026-10-28) is on the development machine**, inside Visual Studio:
  `C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TeamFoundation/Team Explorer/Git/mingw64/bin/git.exe`.
  Put that `mingw64/bin` first on `PATH` (otherwise its child processes fall back to the 2.56 on
  `PATH`). Phase 5's research used it to check what 2.55 writes differently, which phase 4 could only
  learn from CI runs. With it first on `PATH`, 6 of the 49 tests of `VisualCommit.App.Tests`'
  fetch, pull, push and remotes classes, those against the credential test server, fail locally
  and pass on CI: that trimmed git has no `git http-backend`, which the server runs (explained in
  phase 6; plan risk 28). It also lacks `sleep`, so phase 6's fetch hold does not hold with it.
- Phases 1 to 7a were built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 3 to 7a handoffs' "Things that cost time" say what to watch
  for: a worktree starts from `master` and its agent must create its branch from the phase
  branch first; merged worktrees must be removed at once (a built worktree took about 6.4 GB in
  phase 6, and the review measured 0.658 GB with D239's trim; plan risk 27); a global
  `core.fsmonitor=true` leaves a git daemon per worktree running
  (phase 4's handoff says how to list and stop them before a gate).
- The default run takes about 1 h 10 min on the development laptop (1 h 12 min 18 s at phase 7a's
  gate, rested, 5,952 tests in eleven projects, at most four at once through the script, D343; 56
  min 25 s at phase 6's gate, 5,037 tests in nine; 43 min 22 s at phase 5's, 3,233 in seven). On CI
  at phase 7a's gate (run 41190572421) the Windows job took 24 min 6 s (its Test step 21 min 48 s of
  55), macOS's 34 min 11 s (Test step 32 min 16 s; up to 37 min 57 s during the phase),
  `ubuntu-latest`'s 6 min 26 s (Test step 4 min 54 s) and the containers' 6 min 28 s and 6 min 6 s.
  The real-window pass takes about 31 minutes (31 min 6 s for phases 0 to 7a at phase 7a's gate),
  after a rest and away from the night's scheduled tasks of other programs (a hot or busy laptop
  fails Q1). See phase 7a's handoff, "Running the visual gate", for how to run both and what to
  stop first. On the laptop the default run and whole-project runs go through
  `scripts/heavy-run.sh`, one at a time (D245), and a session's start makes one only when the
  machine changed or the tree holds code that is not pushed.
- **The laptop's default runs since phase 6's gate** (the detail is in this file as of `3a3cc71`):
  every default run there from 2026-10-26 to 2026-10-28 stopped on the hang watchdog under load (in
  `VisualCommit.App.Tests` on git 2.36, then twice in phase 6's G23, with nine projects at once and
  with five), while `WUDFHost` leaked memory since the boot of 2026-10-14. After the owner's reboot
  of 2026-10-28 the baseline failed in G23 alone, which the owner chose to split (D264); at phase
  7a's gate two rested runs stopped in slow tests until the owner chose at most four projects at
  once (D343), and with it the gate's runs passed (1 h 8 min 39 s and 1 h 12 min 18 s). `dotnet
  test` itself exits with 127 when a test process is stopped, so a 127 from `scripts/heavy-run.sh`
  is that, not the script.
- **No agent that runs processes works beside a heavy run**, not even "light" git experiments:
  phase 7a's first baseline (2026-10-28) ran beside seven research agents and lost three
  walk-throughs to the watchdog and one check to a timeout, and was no baseline.
- **The live command's variables in a session that started before them** (2026-10-30): Windows
  gives a process the environment of the one that started it, so VS Code (and every Claude Code
  shell in it) lacks user variables set after it started, until it is quit entirely. The live
  command then gets them for its own process alone, without printing them (CLAUDE.md's "What to
  know" gives the PowerShell line). Each run's run.txt is copied outside the repository, since
  `artifacts/` is ignored and the next run overwrites it: `D:/Projects/Personal/phase7a-work/live/`
  holds `run-1-step16.txt`, `run-1c-check1-alone.txt` and `run-2-after-d342.txt`, with the
  investigators' reports (`inv0.md` to `inv3.md`, `critic.md`).
- The next phase can be started on the Windows machine or in a cloud session; see
  [cloud-sessions.md](cloud-sessions.md). No cloud session has worked on the repo yet, and that
  page lists what the first one should check.
- On the Windows development machine only: Windows 11 on an Intel Core i7-8650U laptop, .NET SDK
  10.0.303, Git 2.56.0.windows.4 (since 2026-10-28, D248; 2.36.0.windows.1 before), `gh` signed in
  as RahimPasha. Its screen is 3000×2000 at 200%
  scaling: 1500×1000 logical pixels, of which 1500×952 is the work area. A 1920×1080 window does
  not fit, so the real-window pass can only use sizes up to 1400×900 there. The laptop is slower
  when hot: let it rest a few minutes before the gate's `dotnet test` and again before the
  real-window pass, which times Q1 (limit 2,000 ms; 937 ms on phase 4's gate commit, rested; at
  phase 5's gate 1,749 ms and 1,826 ms in its two passes, 1,147 ms alone after a rest, and
  2,613 ms on the laptop hot from the 43-minute default run, which failed that attempt; up to
  2,268 ms on a warm machine during phase 3; at phase 6's gate, each pass after a 10-minute rest,
  1,264 ms in round 1's pass and 1,805 ms in the counted one; at phase 7a's gate 2,456 ms in a
  pass at 02:18 while other programs' scheduled tasks ran, and 1,400 ms in the counted pass an
  hour later; plan risk 10).
- Packages: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and
  TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI.UIA3 5.0.0. Phases 3 to 6 added no packages; phase 7a added Tmds.DBus.Protocol 0.94.1, the
  Secret Service's D-Bus client, pinned to the version Avalonia 12.1.3 resolves (plan risk 9).
- Git 2.55 on CI writes its own files and messages differently from 2.36, the development
  machine's git until 2026-10-28; phase 4's
  handoff, "Rules worth keeping", lists what it found and how the checks compare git's files,
  phase 5's adds the exit codes and side effects that differ between versions, and phase 6's
  "Things that cost time" what 2.55 and 2.56 did to its tests.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- **Phase 7a's acceptance**: given on 2026-10-31; merged into `master` (`019e68a`); its gate ran on
  `a2c312a`. The tab the real-window pass opened on the fake's page was closed by the owner the
  same day (D321). Still open from the phase, below: risk 39's sign-in dialog in a bound tab and
  risk 40's torn banner.
- **For phase 7b, answered on 2026-10-31**, after 7a's acceptance and before 7b's start (the
  questions were in `plan.md`'s phase 7b section until D344 to D351 replaced them with the
  answers, and are in the 7a handoff's "Do first", step 2):
  - **Azure DevOps**: the owner has an organization on dev.azure.com for 7b's checks, under a
    Microsoft account of the owner's. That account's address stays out of the repository
    (CLAUDE.md's rule on the owner's addresses); the organization's name reaches the live command
    through the variables the design names. The owner makes a personal access token for it when
    the design says which scopes and which variables, and sets it as a Windows user variable in the
    Environment Variables dialog, never typed in a shell or pasted into the conversation (D250,
    D260).
  - **GitLab**: the owner has no GitLab account and no experience of it, and leaves every GitLab
    question to the lead's recommendation. When 7b's design names the token's kind, its
    permissions and the variables, the owner makes a new, free gitlab.com account that holds
    only a private sandbox project, makes the token as the design says, and sets the token and
    the project as Windows user variables (D346, D260). The account's e-mail address never
    enters the repository.
  - **The lead's recommendations** (2026-10-31), for both providers, which the owner accepted the
    same day: D345 to D351, carried into `plan.md` and `requirements.md`. With them, D344: until
    the reset of 2026-11-05 phase 7b may use all that is left of the week's allowance, with 48 %
    of it used on 2026-10-31, and waits for the reset if it runs out. After the owner's
    acceptance, the lead's review of the decisions the same day refined three of the
    recommendations below as recorded, without the owner, who may want to look at them: D345
    takes GitLab's fine-grained tokens as well as legacy ones with the `api` scope; D346 keeps
    each live token to its sandbox, Azure DevOps' by an organization that holds nothing but the
    sandbox, and names the new variables with CLAUDE.md's `VISUALCOMMIT_LIVE_` prefix; D350 binds
    an Azure DevOps account to its own organization's remotes alone.
    1. Sign-in with a personal access token, as D249 for GitHub, with no app registration (risk
       5): for Azure DevOps a token for the one organization, for GitLab one with the `api` scope;
       the design names the exact scopes and the dialog's link to each provider's token page.
    2. A local fake of each provider's API for the default run and CI, as `FakeGitHub` (D250,
       D266), and the opt-in live command extended to both providers in the same project,
       `tests/VisualCommit.LiveHostingTests`, so that D335's exception holds as written: each
       provider against a private sandbox repository, its token and repository in Windows user
       variables that the design names (risk 4; the live run found two faults the fake had hidden,
       D342, risk 42). For GitLab that needs a free gitlab.com account: a new one that holds only
       the sandbox, so that its token, which reaches every project its account can, reaches the
       sandbox alone.
    3. Hosts: Azure DevOps Services alone, its remotes on `dev.azure.com` and on the older
       `<organization>.visualstudio.com` (the same service), and gitlab.com alone. Azure DevOps
       Server on premises and self-managed GitLab go to the backlog beside GitHub Enterprise Server
       (D258): each provider's hosts stay fixed, and no live run could try them. `plan.md`'s 7b
       then loses "self-hosted GitLab included"; `requirements.md`'s providers line already names
       GitLab alone.
    4. One account per provider at once: D263's one account becomes one each of GitHub, Azure
       DevOps and GitLab; Azure DevOps' account is one organization.
    5. Avatars stay GitHub's alone (D252 unchanged): Azure DevOps' and GitLab's authors look as
       they do today, and their avatars go to the backlog. Each provider's lookup would send the authors'
       addresses to another service and need its own research and checks, in a phase that already
       holds two providers.
    6. D255 (the account's token on git's calls through the app's own credential helper) and D257
       (no request signed out) hold for the new hosts as for github.com.
    7. GitLab's screens say "merge request", GitHub's and Azure DevOps' "pull request". 7a's
       GitHub-worded screens and commands name the provider of the account or remote they act on,
       as the design settles, and every 7a picture that changes is inspected again (risk 32).
- **The Azure DevOps sandbox's organization** (D346): answered at 7b's start on 2026-10-31. The
  organization the owner named holds other work, and the owner accepts that the token reaches it
  (D352, which sets D260's reason aside for that token alone); the live command keeps to the
  sandbox by its own guard, the token's scopes and a short expiry, as the design names them. The
  owner makes the Azure DevOps token, the GitLab account and its token when the design names their
  scopes and variables (step 16). The design names them in 15.8 (D364): the Azure DevOps project
  and token, the GitLab account, project and fine-grained token, and the four Windows user
  variables `VISUALCOMMIT_LIVE_AZURE_DEVOPS_TOKEN`, `…_AZURE_DEVOPS_REPO`, `…_GITLAB_TOKEN` and
  `…_GITLAB_REPO`, then VS Code quit and started again, or, without the restart, the lead gives
  the six to the live command's own process from the user scope (15.8's PowerShell line). Needed
  before WP-S's merge (wave 3), since stage 1 of the live command follows it; on 2026-11-01 none of the four was set (checked by name
  alone). Each token's expiry date goes here once it is made. On 2026-11-02 the owner made both
  sandbox projects and a token for each, and showed the two tokens in the session's conversation;
  so both are revoked and made again, never shown anywhere (D250, D260). GitLab's first token was a
  legacy one with every scope; the new one is 15.8's fine-grained token (D375), and Azure DevOps'
  new one has Code "Read & write" alone, for D352's organization alone, at most 30 days. The lead
  gave the owner a PowerShell script, outside the repository, that sets the four variables and
  prints only 15.8's true-or-false checks; the GitLab account's user name, like the organization's
  name, stays out of the repository. Done on 2026-11-05: the old tokens revoked (the owner), the new
  ones set (the script reads each from the clipboard; its first two forms lost a pasted token and
  saved a message with it, which the lead stripped in place, printing nothing), and all six variables
  pass 15.8's checks. GitLab's is a fine-grained token for "Only my personal projects" with 15.8's
  permissions and a few more (Merge Request: Approve, Merge, Subscribe; Pages Domain: Read). **The
  tokens expire**: GitHub's on 2026-11-29 (risk 42), Azure DevOps' on 2026-12-04 (the owner's
  answer); the gate's two live runs fall before 2026-11-29, or the owner makes GitHub's again first.
  GitLab's was made again on 2026-11-05 with Project: Read among its user permissions too (D385;
  the one it replaced, which expired on 2026-12-04, revoked), set with the lead's GitLab-only script
  and checked as 15.8 says; it expires on 2026-12-04 (the owner's answer of 2026-11-06).
- **Azure DevOps' probes** (the report's open point 8; design 16.1's As built): the owner agreed on
  2026-11-06. The lead runs `--filter-class "*AzureDevOpsLiveTests"` with `--filter-method "*Check_1b_probes*"`,
  17 reads and no write in the sandbox's organization, after the offline classes on the same commit
  (`9409aa8`: 404 of 404); its new probe tells whether the organization allows public projects, which
  decides A9's expected result. Run on 2026-11-07 (UTC) through `heavy-run.sh`: passed, 17 requests,
  no write. connectionData with no credential answered 200 with the Anonymous identity, where the
  fake answers 401: the sandbox's organization allows public projects. Every other probe answered as
  before. As 16.1 decided, the fake keeps the Anonymous 200 to public organizations, the live command's
  fake treats the sandbox's organization as public, and the lead ruled A9 through the fault
  `AnonymousIdentity` (open point 8's second way). Built in `D:/Projects/Personal/vc-wt/p7b-anon-probe`
  (`wip/p7b-anon-probe`), reviewed by two Opus reviewers (no blocker or major; five minors, fixed: the
  fake's own test now sends the measured request, the report records the run under "Live command") and
  merged at `20e529e`. The run's notes are outside the
  repository, in `D:/Projects/Personal/phase7b-work/live/azure-devops-check1b-2026-11-07.txt`.
- **Decisions made during phase 7a without the owner**, which the owner may want to look at: the
  design's D265 to D338 (after its reviews, with the owner's answers of 2026-10-29); D339 to D341
  (the review before the gate and the runs after it: the avatars' lookup per author, git's
  `proactiveAuth` and the netrc, D237's 60 s for every wait); D342 (the live run's corrections of
  the client and the fake); and the lead's rulings in the design (its 32 of 2026-10-28) and in the
  report's "Open points and the lead's rulings". D343 is the owner's choice. D344 to D351 are the
  owner's answers for 7b, three of them (D345, D346, D350) with the lead's refinements made after
  the owner's acceptance (above); D352 is the owner's answer at 7b's start, and D353 to D356 the
  owner's rulings on the research's questions of taste; D357 to D372 the design's (2026-10-31);
  D373 to D376 the owner's answers of 2026-11-01, D377 and D378 the design review's, D379 and
  D380 the lead's rulings on wave 1's adversarial check, D381 and D382 on its second round, D383
  the lead's on WP-S's hunts (an account switch within a provider drops the tab's list and A3
  answer), D384 the lead's at WP-LIVE's stage 1 (the screen hides GitLab's sandbox namespace), D385
  stage 1's measurement (a GitLab token's Project: Read among its user permissions too), and the
  lead's: D386 (git's calls at below-normal priority on Windows, held by a job), D387 (the sign-in
  dialog finds another provider's token inside a pasted value), D388 (a network operation inside
  D290's window binds once the URL rules are known), D389 (D278's checks read the URL's user by the
  measured git; risk 43), D390 (a check of no git call sets aside only the watchers' refreshes),
  D391 (a clone's bindings read the URL rules of git's conditional includes) and D392 (the review
  before the gate's rulings) and D393 (the live command's GitLab guard admits the sandbox project alone,
  the owner's choice), D394 (the fake of GitHub answers a commit it lacks beside `author` 404, as
  github.com did at the gate's first live run) and D395 (a lookup's 404 in a repository not known to be
  readable is checked against its read; the owner's approval). The next decision number is D396.
- **A sign-in dialog in a bound tab** (plan.md's risk 39, design 19 item 8; no answer needed
  for the gate): when the session refuses a bound account through the API, or it signs out,
  just after an operation started, git's "Sign in to github.com" dialog opens instead of D273's
  refusal without a dialog, as D340 records for a sign-out. Should the broker also refuse such a
  prompt without a dialog, from what the session knows (amending D273 and D340)?
- **The banner's brief torn state** (plan.md's risk 40, design 21 item 4): during a merge started
  in a terminal the banner can read "Conflicts resolved" for about 0.3 s and one refresh. Removing
  it takes a status read that checks the in-progress files before and after `git status`; no
  requirement asks for it. Whether to build it is the owner's.
- **Answered 2026-11-08: (b), narrow the guard only; the account stays as it is (D393).** **The GitLab sandbox account lists five projects** (2026-11-08; seen in stage 1's run of
  2026-11-06 too, and not reported then): the app's list of the account's projects
  (`GET projects?membership=true`, with the fine-grained token) gave five rows, the sandbox and four
  others, where the owner's accepted recommendation 2 (above) has a new account "that holds only the
  sandbox, so that its token, which reaches every project its account can, reaches the sandbox
  alone". The live command's code touches the sandbox alone (its clean-up selects by the sandbox's
  path and project id, and the three hunts of both stages found no other path), but D371's GitLab
  guard admits any project under `https://gitlab.com/api/v4/`, resting on that premise. The other
  projects' names are not read or recorded here. Which is wanted? (a) The owner looks at the
  account's projects on gitlab.com and deletes those that are not the sandbox (onboarding may have
  made some), then nothing changes in the code; (b) the lead narrows the live command's GitLab
  guard to the sandbox's own project, by path and by the id the sandbox read gives, beside the
  account's project list, which the app's browser needs (amending D371; a change to
  `tests/VisualCommit.LiveHostingTests` alone, which re-runs only the live command, D335); (c) both,
  which the lead recommends: the token then reaches the sandbox alone and the guard refuses the rest
  even if the account gains a project. No answer is needed before the review before the gate; one is
  before the gate's live runs.
- **A git that loses the URL's user** (Notes, "D388's test on macOS"; D389, plan risk 43, design
  16.3's bullet on D275; no answer needed for the gate): git 2.56.0 without git's own fix `76524c2604bc`, over a
  curl that strips the user from its effective URL (curl 8.23.0, CI's macOS Homebrew git today),
  asks for credentials without the URL's user, so the broker's refusal of another user (D275) does
  not see bob. Where Select's drop misses a spelling (design 16.3's accepted GitHub cases: a reserved
  character escaped in the binding and raw in the other URL), the account's token then answers for a
  URL that names another user, on the bound host alone (github.com names no repository with such
  characters); on git 2.31 to 2.45 over such a curl, stale bindings and submodule URLs too (no known
  distribution ships that pairing). Git's fix reaches Homebrew with git 2.57.0 or a formula patch.
  Should the app harden now, which reopens design 16.3 (bind nothing on such a git, measured at
  start, where any remote names another user on the host; or move GitHub onto `SelectCovered`'s
  credential-view drop; or read the URL rules at every bound operation, amending D388)? The lead
  recommends leaving it as a recorded risk until a report, since the token never leaves the bound
  host.
- **The start reading has grown** (the cold read at phase 7a's close): CLAUDE.md's step 4 reads
  `architecture.md` (about 622 KB, about 429 KB at 7a's start) and `decisions.md` (about 262 KB)
  whole. The review before phase 7 set aside P6 (start reading by index, `architecture.md` split by
  area) with "reconsider it if the start reading keeps growing" (review-before-phase-7.md, section
  on the proposals not taken); until the owner decides, the 7a handoff's "Do first" names the
  sections 7b needs.
- **The weekly allowance** (D241): a session cannot read `/usage` itself, so phase 7a's checkpoints
  record "share not read"; for 7b the owner gave the share before the start (48 % used on
  2026-10-31) and lets the phase use the rest of that week (D344): the session asks for the share
  before each stage to record it, but until the reset of 2026-11-05 no stage waits for it
  (plan.md's "Workflow"). For the week from 2026-11-05 the owner gives the phase about all of it
  (the owner's answer of 2026-11-05).
- **The live command's sandbox for GitHub (D250)**: the private `RahimPasha/visualcommit-sandbox`
  and a fine-grained token for it alone, expiring 2026-11-29, in the two Windows user variables
  `VISUALCOMMIT_LIVE_GITHUB_TOKEN` and `VISUALCOMMIT_LIVE_GITHUB_REPO` (set on 2026-10-30; the
  live command passed 80 of 80 at status step 16 and 112 of 112 twice at the gate). A gate after
  2026-11-29 needs a new token, made as design 14.6 says.
- **D240's connectors switch** (Notes): `ENABLE_CLAUDEAI_MCP_SERVERS=false` in
  `.claude/settings.local.json` did not keep the claude.ai connectors out of this session. Turning
  them off another way (the variable set in Windows' user environment before VS Code starts, or
  the connectors switched off on claude.ai) is the owner's.
- **Phase 6's acceptance**: given on 2026-10-24; merged into `master` (`3f34c87`); its gate ran on
  `b561b7c`. Still open, asked at acceptance and not yet answered (the handoff's "Waiting on the
  owner"; each answer becomes a decision, D386 on, since D239 to D248 went to the review before
  phase 7, D249 to D264 to phase 7a's start, D265 to D338 to its design, D339 to D343 to its build
  and gate D344 to D356 to 7b's start and its research, D357 to D378 to its design and D379 on to its
  build): design 17.5's reading of "ten repos can be checked and
  updated from one screen" (one dashboard of the ten, one Fetch all and one Pull all deciding every
  row); the report's by-hand System step (switch Windows' app mode while the app runs with System
  chosen, D224); and the move of the text boxes' placeholder contrast (Fluent's watermark opacity,
  about 2.5:1 dark and 2.1:1 light) to phase 8's accessibility pass, which the lead made at the
  gate's inspection rather than change nearly every picture.
- **Decisions made during phase 6 without the owner**, which the owner may want to look at: the
  design's D203 to D230 (after its reviews); D231 (the rebase chosen for one pull reads `pull.ff`
  alone); D232 (CI's limits, 55 and 65 minutes); D233 (git calls leave out the repository-selecting
  variables); D234 (the session's end cancels every dialog; the window waits for the settings
  screen's global writes); D235 (Amend refuses when HEAD moved); D236 (the app's own files flushed
  before their rename); D237 (the default run's waits fit nine projects side by side); D238 (part
  L's own test server port); and the lead's rulings recorded in the design and the report.
- **Answered at the start of phase 7a** on 2026-10-28, before its design (D249 to D254): plan
  risks 4 and 5 for GitHub (a personal access token; a local fake of GitHub's API, and an opt-in
  command against real GitHub), where the pull-request list lives, avatars, a Linux desktop
  without a keychain, and the review's question 11 with its S8 and R8 (all three in the first
  package). Azure DevOps' and GitLab's accounts and sign-in were answered on 2026-10-31 (D345,
  D346).
- **Answered during phase 7a's design review** on 2026-10-29, written into the design's decisions
  when the review's findings were applied: the real-window pass may open the default
  browser once, on the fake's page (D321); the status bar's button after a create reads "Open in
  browser" (D317); no palette command signs in to GitHub in 7a; the sign-out confirmation uses the
  primary button (D296). On the review's findings (`phase7a-work/review/findings.md`): F1, on git
  older than 2.31 the account is not used for git at all (ruling 3's fallback would have let git
  store the token in the user's own helpers); F2, Create pushes a branch under its own name and
  leaves an upstream of another name as it is; F15, avatar lookups send GitHub only the addresses of
  commits reachable from the GitHub remote's branches, and sign-out deletes the avatars folder; F21,
  a commit after the counted round that changes only the live project re-runs only the live command
  (extends D246), and the live command also runs once before the counted round.
- **Still open from the review before phase 7** ([review-before-phase-7.md](review-before-phase-7.md);
  each answer becomes a decision, D386 on): its questions 1 (how the plan's limits count tokens),
  10 (CI split into shards, S5, which reopens D134 and D232) and 12 (the machine: the page file,
  the space on C:, and how long to keep transcripts, D8; the reboot was made on 2026-10-28); and
  the proposals that no decision names: D5, T10/P7, R4, R9, R1/R6, R10, S6, S7, S10, R7, D3, D4,
  N4, S11 and T8/P12's trial of a lower effort level. The review's section 5, step 11 (one default
  run re-measured on a quiet laptop) was made on 2026-10-28 and failed on load, and so did S10's
  run with at most 5 projects at once (Notes); phase 7a's baseline, after the reboot, failed in G23
  alone (step 2), and the owner chose to split the check (D264).
- **Git Credential Manager since git 2.56** (2026-10-28): the new install's system configuration
  sets `credential.helper=manager`, and a `git push` from Claude Code waited on its sign-in window.
  The pushes of 2026-10-28 used `gh`'s login for that command only
  (`git -c credential.helper= -c 'credential.helper=!gh auth git-credential' push ...`); no git
  configuration was changed. Signing in to it once, or choosing another helper, is the owner's.
- **Phase 5's acceptance**: given on 2026-10-17; merged into `master` (`c299939`). The decisions
  made during the phase without the owner, which the owner may want to look at: D182 (tooltips take
  no input), D183 (the report's rulings), D184 and D186 (CI's time limits, 40 and 50 minutes, raised
  past 32), D185 (the wait for git's merge tool not counted by the hang watchdog).
- Answered during phase 5's design on 2026-10-12 (D164 to D166), after its reviews: the details
  panel gains an "All files" switch so that any line of any file is two clicks from its commit;
  an Abort is undone from the backups list, not by Undo; undoing the automatic stash's checkout
  switches back and pops that stash.
- Answered at the start of phase 5 on 2026-10-12 (D153 to D163): C12 is built in phase 5, its
  refusal keeps the "Stash changes and check out" button, the automatic stash is taken always
  when there are changes and only for a commit the current branch reaches, and it is kept until
  popped or dropped; undo reaches many steps back within a session; undoing a change to a remote
  asks, then pushes back with a lease; the activity log is a panel under the graph; search marks
  its matches in the graph; file history and blame open in place of the graph; hidden and soloed
  branches are remembered across restarts; the cosmetic issues phase 4 left are fixed first;
  every stash's line to its commit is dashed; two selected commits are compared at once in the
  right panel; Ctrl+Z and Ctrl+Y undo and redo git actions outside text boxes.
- Answered for phase 4 on 2026-10-12, before its design: git's
  own list of commits for several picks (D136, replacing a sentence of D88); automatic cleanup of
  the backups after 30 days or when more than a set number exist (D137); then, asked at the start
  of phase 4: 50 backups are kept besides the 30 days, the clean-up says nothing on screen (the
  log records it), the conflict resolver shows ours and theirs side by side above the editable
  result across the graph and right-panel area, and the interactive rebase's list replaces the
  graph with the newest commit on top.
- The virus-scanner exclusion on the development machine (the repository folder and
  `C:\Users\rpkha\AppData\Local\Temp\VisualCommit.Tests`) was set up by the owner on
  2026-10-12. Whether it is in effect cannot be read without administrator rights; phase 5's gate
  ran the default tests in 43 min 22 s with seven test projects (phase 4's: 21 min 52 s with
  five; phase 3's: 19 min with four).
- Settled on 2026-10-12: the working-changes row's dashed line crossing another branch's node
  (06f) is left for now; case-only branch renames on Windows and macOS stay refused (D131);
  auto-fetch gets no ssh timeout, only Cancel by hand (D125).
- When you have a Mac at hand: the eight-step checklist at the end of the
  [phase 7a test report](test-reports/phase-7a.md), the seven-step one at the end of the
  [phase 6 test report](test-reports/phase-6.md), the thirteen-step one at the end of the
  [phase 5 test report](test-reports/phase-5.md), the eight-step one at the end of the
  [phase 4 test report](test-reports/phase-4.md), the eleven-step one at the end of the
  [phase 3 test report](test-reports/phase-3.md), the seven-step one at the end of the
  [phase 2 test report](test-reports/phase-2.md), the nine-step one at the end of the
  [phase 1 test report](test-reports/phase-1.md) and the eight-step one at the end of the
  [phase 0 test report](test-reports/phase-0.md). The owner will do them at the end of the
  project, when a Mac is at hand (2026-10-11, again on 2026-10-12 with phase 4's acceptance).
- When the project is over: whether a CI run of 30 minutes or more is reasonable or needs another
  solution (D134).

Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors", and
the repository was made public, which lifts the limit on CI minutes.

Settled on 2026-10-09 (D41): the owner deleted the GitHub repository and a session created it
again, empty and public, and pushed `master` and `phase/0-foundation` unchanged. The commits from
before the rewrite of the history (D40), which GitHub had kept serving by their IDs, now answer
"not found"; every commit on GitHub carries the owner's personal address. The session's
permission system refuses to delete a repository, so a deletion is always the owner's to do.
