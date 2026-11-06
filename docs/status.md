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
- [ ] 9. The account's token on the app's git calls to the remotes it covers (D350): gitlab.com's,
  and Azure DevOps' signed-in organization's alone; the push guard and the prompt group for the new
  hosts, as the design decides. Tests. Under way: WP-C (`Select` with a coverage, the push guard
  and the prompts, section 8) merged at `6586066`, with wave 1's adversarial fixes (D379, D381);
  the session's union of the coverages is WP-S's (section 9, wave 3).
- [ ] 10. Sign-in dialog and the Accounts section with an account of each provider (D345, D348).
- [ ] 11. Browse and clone the repositories of an Azure DevOps and a GitLab account.
- [ ] 12. The left panel's section for each provider (GitLab's merge requests, D351), its menu,
  checkout, the badge on branches, open in browser.
- [ ] 13. Create a pull request or merge request from the branch menus and the drop menu, and its
  form, for each provider.
- [ ] 14. 7a's screens and commands that name GitHub name the provider they act on (D351); the
  earlier checks whose pictures change updated in the same commits, under "Changes to expected
  results".

Checks, live trial and review:

- [ ] 15. Each part's visual checks written into `docs/test-reports/phase-7b.md` just before its
  package is built (D244), by one writer and one reviewer, each marked for the real-window pass or
  not, committed before the UI they test.
- [ ] 16. The owner's sandboxes and tokens for Azure DevOps and GitLab made as the design says
  (D346, D352), and the live command extended to both, run with them; what it finds goes into the
  fakes (D342). Under way: the sandboxes are made (2026-11-02; their first tokens replaced, Waiting
  on the owner); stage 1 (15.1) merged at `b377aa3`, built, reviewed and hunted, offline classes
  404 of 404; its first run with the tokens (2026-11-05) passed 8 of 9 (Notes, D385), and GitLab's
  again with a new token (2026-11-06) 4 of 4, so stage 1's 9 tests have passed; WP-LF (0.4) puts
  what they found into the fakes; stage 2 (`Check_3`) follows WP-N.
- [ ] 17. An independent review of the code before the gate; every confirmed finding fixed with
  tests.

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

Stage 1 has passed all 9 tests (Notes): Azure DevOps' 5 on 2026-11-05 and GitLab's 4 again on
2026-11-06 with the owner's new token (D385). Next is WP-LF (design 0.4, branch `wip/p7b-live-fix`):
the fakes and clients take both run.txt files' findings, each corrected answer marked MEASURED with
its date, with made-up values only; its classes and then the lead's lagged list (0.4) run, it merges
and its CI run is read; then part A's checks are written (D244) and WP-A is built. For the week from
2026-11-05 the owner gives the phase about all of its allowance.

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
  and checked as 15.8 says; its expiry date is still to be told by the owner.
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
  stage 1's measurement (a GitLab token's Project: Read among its user permissions too). The next
  decision number is D386.
- **A sign-in dialog in a bound tab** (plan.md's risk 39, design 19 item 8; no answer needed
  for the gate): when the session refuses a bound account through the API, or it signs out,
  just after an operation started, git's "Sign in to github.com" dialog opens instead of D273's
  refusal without a dialog, as D340 records for a sign-out. Should the broker also refuse such a
  prompt without a dialog, from what the session knows (amending D273 and D340)?
- **The banner's brief torn state** (plan.md's risk 40, design 21 item 4): during a merge started
  in a terminal the banner can read "Conflicts resolved" for about 0.3 s and one refresh. Removing
  it takes a status read that checks the in-progress files before and after `git status`; no
  requirement asks for it. Whether to build it is the owner's.
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
