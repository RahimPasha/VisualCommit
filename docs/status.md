# Status

Last updated: 2026-10-29

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
| 7a. Hosting foundation and GitHub | In progress | `phase/7a-hosting-github` | | |
| 7b. Azure DevOps and GitLab | Not started | `phase/7b-hosting-azure-gitlab` | | |
| 8. Public release | Not started | `phase/8-release` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 7a, "Hosting foundation and GitHub", is in progress on `phase/7a-hosting-github`, started on
2026-10-28 from `master` at `5fe9148`. Phase 6 was accepted by the owner on 2026-10-24 and merged into `master` (merge
commit `3f34c87`); its gate ran on `b561b7c`. Phase 5 was accepted on 2026-10-17 (merge commit
`c299939`), phase 4 on 2026-10-12 (merge commit `ce5f337`), phase 3 on 2026-10-11 (merge commit
`8b33745`), phase 2 on 2026-10-10 (merge commit `e039070`), phase 1 on 2026-10-10 (merge commit
`b5eb1ed`) and phase 0 on 2026-10-06 (merge commit `b5f4723`). Phase 7 was split into 7a and 7b on
2026-10-28 (D241).

Gate commit: none yet. When a phase is set to "Awaiting acceptance", this line names the commit
that both passes of the gate ran on (or, by D246, the real-window pass's commit when it changes
only `tests/VisualCommit.RealWindowTests` after the commit the default run ran on); "Phase N accepted" checks that
nothing but docs changed after it.

### Progress

Each item is ticked in the commit that completes it, and each tick, like the start of each stage,
records the share of the plan's weekly allowance used at that point (D241). The numbers are plain
step numbers, not requirement numbers. The design (step 3) splits the Core, Git-layer and App
steps into work packages and says how they map onto the steps below.

The week: the plan's allowance resets on Thursdays at 2:00. The session cannot read `/usage`; on
2026-10-28, at the phase's start, the owner said that what is left of the week will not run out
before the reset of 2026-10-29.

Start:

- [x] 1. Phase branch created, phase set to "In progress", this checklist; the owner's answers to
  the open points of phase 6's handoff and plan risks 4 and 5 recorded (D249 to D254) and carried
  into `plan.md` and `requirements.md`. (2026-10-28: sign-in with a personal access token, no
  device flow; a local fake of GitHub's API in the default run and CI, and an opt-in command
  against real GitHub with the owner's fine-grained token for a sandbox repository; the
  pull-request list in the left panel's section with a menu; avatars from GitHub only, every
  other author as before; the session only where no keychain can be reached; the review's W1, S8
  and R8 in the first package. After the design's research, before its drafting, the same day:
  D255 to D263, recorded in step 3.)
- [x] 2. Baseline: the default run on `master` (`5fe9148`) as inherited, after the owner's reboot,
  through `bash scripts/heavy-run.sh`, before anything changes. (2026-10-28, 13:54 to 14:54, the
  laptop quiet: 5,001 of 5,001 tests that ran passed in 58 min 13 s, exit 127: the watchdog
  stopped `VisualCommit.VisualTests.Phase6` 1,028 s into the run in G23's part B,
  `Check_G23_pull_refused_inside_the_integration`, after its 180 s (pictures at 28.8 s, 119.7 s
  and 160.9 s), so that project's other tests did not run; the fourth such failure on git 2.56,
  the first on a quiet laptop after the reboot. The owner chose to split the check (D264, step 4).
  The projects ended after: part L's walk-through 11 min 15 s, phases 0 to 2's 15 min 0 s, phase
  6's 17 min 11 s (stopped), phase 4's 29 min 5 s, phase 5's 32 min 18 s, phase 5's undo
  walk-through 49 min 50 s, phase 3's 50 min 9 s, `VisualCommit.Git.Tests` 50 min 11 s and
  `VisualCommit.App.Tests` 58 min 12 s. The first attempt, from 12:01, was disturbed by the
  session's own research (Notes). Week: share not read; the owner: enough left before the reset.)
- [x] 3. Design in `docs/phase-7a-design.md` (D243: about 150 to 200 KB, its decisions only in
  `decisions.md`): GitHub's API facts as the fake must serve them, the keychains of Windows, macOS
  and Linux, git 2.56's credential facts (with 2.43 and 2.55 from CI where they differ, D248), the
  texts, the layouts, the tests and harness (the fake, the keychain's seam, the opt-in live
  command), how the code and the harness name phase 7a, an estimate of what the phase adds to the
  default run (about 55 minutes) and to the real-window pass (28), and the work packages, each
  section a builder's brief of at most about 25 KB (D242). Its review: 3 or 4 lenses, git safety
  and tests/gate among them, a verifier only for each blocker and major; questions of taste to
  the owner; its decisions recorded. (Done 2026-10-29: drafted by 9 agents, reviewed by four
  lenses with 20 agents, 36 findings fixed by 9; its decisions D265 to D338; the owner answered
  eight questions, the lead ruled on the rest (Notes). About 345 KB, over D243's mark, accepted by
  the lead (Notes). Week: share not read; the owner: enough left before the reset.)
- [x] 4. First package (plan.md's "From phase 6" and D254): `ShellDriverTests`' wheel test waits
  for the hit test; the real-window csproj's comment gives the pass of phases 0 to 6 as 27 min
  39 s; W1's failure under load looked at and fixed; S8's git-call counts as upper bounds taken
  with the watcher quiet; R8's test switch that delays the watcher's events; G23's part B split
  (D264). (Done 2026-10-29: L1 at `f1dc80e`, WP1 merged at `a4a388e`; the lagged list's first run
  passed at that merge, and the whole default run on `069c5c7` passed: 5,547 of 5,551, 4 skipped.)
- [x] 5. Harness: phase 7a's walk-through project, the local fake of GitHub's API (its port
  amending D97, as D150, D229 and D238 did), the keychain's fake in both harnesses so that no run
  touches the owner's keychain, the timing script and CLAUDE.md's Commands, the real-window
  project's link to the phase's expectations. (Done 2026-10-29: WP-F merged at `c562ff4`; CLAUDE.md's
  7a commands at `069c5c7`.)

Core and Git layer:

- [x] 6. The provider model that 7b builds Azure DevOps and GitLab on: hosts, accounts, a GitHub
  remote recognised from its URL. Tests. (Done 2026-10-29: WP0, WP-G at `1d6b688` and WP-S at
  `7bea4fb`; CI green on `5613450`.)
- [x] 7. Tokens in the OS keychain on Windows, macOS and Linux behind a seam (R3), and in memory
  for the session where none can be reached (D253). Tests. (Done 2026-10-29: WP-K merged at `2582e53`;
  each real store's test passed on CI's run on `8b02805`, the first run of any.)
- [x] 8. GitHub's API: a token checked at sign-in, the account's repositories, a repository's pull
  requests, creating one, avatars; all against the fake. Tests. (Done 2026-10-29: WP-G merged at
  `1d6b688`.)
- [x] 9. The token reaching git (plan.md's "From phase 3": git's credential helpers first, then the
  askpass helper and `AskPassBroker`; auto-fetch never prompts), a bulk run's prompt group and
  accounts ("From phase 6"), and the push guard's refusals of `pr/*` and two-namespace pushes
  ("From phase 6"), each as the design decides. Tests. (Done 2026-10-29: WP-C at `afb91a7`, WP-S's
  bindings at `7bea4fb`, the prompt channel's fix for macOS and Linux at `9d48d0b`; CI green on
  `5613450`.)

App:

- [x] 10. Sign-in dialog and the accounts in the settings screen (D249, D253). (Done 2026-10-29:
  WP-A merged at `42b6be2`; part A's 24 pictures and the 31 settings pictures it changes inspected;
  CI green on `e40ddc8`.)
- [x] 11. Browse and clone the repositories of a GitHub account. (Done 2026-10-29: WP-B merged at
  `b361388`, the lead's follow-ups `e40ddc8`; part B's ten pictures inspected (design 17 item 5); the
  whole run's earlier pictures unchanged but for what varies by nature; CI green on `e40ddc8`.)
- [ ] 12. The left panel's "Pull requests" section with its menu (D251), the badge on branches,
  open in browser. (The left panel's half done 2026-10-30: WP-P merged at `1958755`, part P's 24
  pictures inspected, the harness's double-click fix at `ccd1ebe`, the lagged list on it passed; the
  badge is WP-V's.)
- [ ] 13. "Create pull request" in the branch menus (`ForLocalBranch`, `ForRemoteBranch`) and the
  drop menu (`ForDrop`), and its form.
- [ ] 14. Author avatars from GitHub (D252).

Checks, live trial and review:

- [ ] 15. Each part's visual checks written into `docs/test-reports/phase-7a.md` just before its
  package is built (D244), by one writer and one reviewer, each marked for the real-window pass or
  not, committed before the UI they test.
- [ ] 16. The opt-in live command against real GitHub (D250), run with the owner's token on the
  sandbox repository.
- [ ] 17. An independent review of the code before the gate; every confirmed finding fixed with
  tests.

Closing (`CLAUDE.md`, "Closing a phase"):

- [ ] 18. The visual test gate: the scripted walk-through's pictures on this machine, then the new
  real-window checks twice on their own with the owner told and present (D246); `dotnet test` and
  the real-window pass on one commit; every picture inspected, in batches of at most about 150
  (D247); `docs/test-reports/phase-7a.md` completed.
- [ ] 19. `docs/architecture.md` describes what is built.
- [ ] 20. `docs/plan.md`: what 7a did not deliver moved to the phase that owns it; risks updated.
- [ ] 21. `docs/handoffs/phase-7a.md` written from the template.
- [ ] 22. Cold-read check by a fresh agent; every gap fixed.
- [ ] 23. The CI run of the gate's commit read, with each job's result.
- [ ] 24. `docs/status.md`: links, the CI run, the "Gate commit", and "Awaiting acceptance" only if
  the whole gate passed; the owner told what passed and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

Phase 7a's run for WP-V's merge (`8810136`) is
[40993671970](https://github.com/RahimPasha/VisualCommit/actions/runs/40993671970): `ubuntu-latest`
(6 min 2 s; Test step 4 min 35 s, 5,811 of 5,814 passed and 3 skipped) and the two "bare ubuntu
container" jobs (7 min 10 s and 5 min 46 s; Test steps 5 min 5 s and 4 min 9 s; their Secret Service
steps green) succeeded. `windows-latest` (37 min 58 s; its Test step 35 min 21 s of 55; its keychain
step green) failed P13 at step 2 again, tool's list request after the mark (5,812 of 5,817 passed, 4
skipped). `macos-latest` (24 min 45 s; Test step 23 min 28 s; its keychain step green) failed part G's
method 1 at G1's new pixel check, "Row 0's name ends at x 79; the report gives 80.4 ±1" (5,810 of
5,814 passed, 3 skipped): it read a text's ink to ±1 px, which the platforms' glyph rasterisation
moves. Its fix is being built (Notes). Windows' Test step has taken 34 and 35 minutes in two of the
last three runs, against D232's mark of 44.

Phase 7a's run for the lead's icon commit before WP-V (`fb43def`, pushed with the docs commit
`713b31f`) is [40986166736](https://github.com/RahimPasha/VisualCommit/actions/runs/40986166736): all
five jobs succeeded: `windows-latest` (25 min 35 s; its Test step 23 min 25 s of 55, 5,786 of 5,790
passed and 4 skipped; its keychain step green), `macos-latest` (26 min 51 s; Test step 24 min 58 s,
5,784 of 5,787 passed and 3 skipped; its keychain step green), `ubuntu-latest` (6 min 40 s; Test step
4 min 54 s, 5,784 of 5,787) and the two "bare ubuntu container" jobs (8 min 6 s and 5 min 38 s; Test
steps 5 min 50 s and 3 min 57 s; their Secret Service steps green). macOS passed the two tests it
failed on `ccd1ebe`, so both failures come and go.

Phase 7a's run for the harness's double-click fix (`ccd1ebe`) is
[40982132445](https://github.com/RahimPasha/VisualCommit/actions/runs/40982132445): `windows-latest`
(37 min 12 s; its Test step 34 min 16 s of 55, 5,786 of 5,790 passed and 4 skipped; its keychain
step green), `ubuntu-latest` (7 min 25 s; Test step 5 min 32 s, 5,784 of 5,787 passed and 3 skipped)
and the two "bare ubuntu container" jobs (7 min 5 s and 5 min 35 s; Test steps 4 min 56 s and 4 min
6 s; their Secret Service steps green) succeeded, with the double-click test passing. `macos-latest`
(21 min 46 s; Test step 20 min 52 s) failed two tests that passed on every other job and on macOS
in the runs before; 5,782 of 5,787 passed and 3 skipped. They are part P's
`Check_P13_P6_checkout_and_a_forks_tab` (step 2's "no request since the mark" saw tool's list
request, `pulls:acme/tool`) and `KeyRouterTests.Every_key_declines_during_a_branch_drag` (Ctrl+Shift+E
"never arrived" at the focused `ChangedFile` button). Both are being investigated (Notes).

Phase 7a's run for WP-P's merge (`1958755`) is
[40965836423](https://github.com/RahimPasha/VisualCommit/actions/runs/40965836423): all five jobs
failed one and the same test, `LeftPanelPullRequestsViewTests.In_the_window_the_sign_in_row_signs_in_and_a_double_click_checks_out`
("Timed out after 60 s waiting for the double-click's action"); every other test passed
(`windows-latest` 29 min 46 s, 5,786 of 5,787; `macos-latest` 34 min 54 s, 5,783 of 5,784; the
three Linux jobs 7 min 22 s, 8 min 12 s and 6 min 56 s, 5,783 of 5,784 each, with 3 or 4 skipped).
The cause is the headless harness, not the product (Notes); the fix is being built.

Phase 7a's run for WP-B's merge (`b361388`, the lead's follow-ups `e40ddc8`) is
[40939757597](https://github.com/RahimPasha/VisualCommit/actions/runs/40939757597), for `e40ddc8`:
all five jobs succeeded: `windows-latest` (23 min 26 s; its Test step 21 min 22 s of 55, 5,710 of
5,714 passed and 4 skipped; its keychain step green), `macos-latest` (33 min 25 s; Test step 31 min
10 s, 5,708 of 5,711 passed and 3 skipped; its keychain step green), `ubuntu-latest` (5 min 50 s;
Test step 4 min 26 s) and the two "bare ubuntu container" jobs (7 min 50 s and 6 min 8 s; Test steps
5 min 44 s and 4 min 10 s; their Secret Service steps green), each Linux job 5,708 of 5,711 passed
and 3 skipped. The run before it,
[40936332833](https://github.com/RahimPasha/VisualCommit/actions/runs/40936332833) for `2a90e05`
(part A's start), passed on the three Linux jobs; its Windows and macOS jobs were cancelled by the
push of `e40ddc8`, not failed.

Phase 7a's run for WP-A's merge (`42b6be2`, the lead's follow-ups `c25326e`) is
[40925952120](https://github.com/RahimPasha/VisualCommit/actions/runs/40925952120), for `c25326e`:
four jobs succeeded (`macos-latest` 37 min 58 s, `ubuntu-latest` 5 min 38 s, the containers 6 min
8 s and 5 min 55 s) and `windows-latest` (34 min 7 s) failed one test of 5,663:
`Phase7aChecksA.Check_A1_A2_A5_signed_out_dialog_and_signed_in`, whose git calls after A1's quiet
mark began with git's detection (`git --version`): part A's start did not wait for the app's
start-up work (design 17 item 2); fixed by the lead on the phase branch.

Phase 7a's run for the prompt channel's fix and WP-S (`9d48d0b`, `7bea4fb`, the wiring `04952c8`) is
[40916152337](https://github.com/RahimPasha/VisualCommit/actions/runs/40916152337), for `5613450`:
all five jobs succeeded: `windows-latest` (25 min 16 s; its Test step 23 min 5 s of 55, 5,603 of
5,607 passed and 4 skipped), `macos-latest` (27 min 17 s; Test step 25 min 17 s), `ubuntu-latest`
(7 min 2 s; Test step 5 min 17 s) and the two "bare ubuntu container" jobs (7 min 5 s and 5 min
41 s; Test steps 4 min 58 s and 3 min 49 s), each with 5,601 of 5,604 passed and 3 skipped; every
keychain step passed with its one test. The run before it,
[40910893877](https://github.com/RahimPasha/VisualCommit/actions/runs/40910893877) for `6fc2d28`
(the held test alone), failed on purpose on macOS and the three Linux jobs with exactly
`AskPassChannelTests`' three tests (Notes); its Windows job was cancelled by the next push.

Phase 7a's run for wave 1 (its five merges and the lead's follow-ups `8b02805`) is
[40878208671](https://github.com/RahimPasha/VisualCommit/actions/runs/40878208671), for `8b02805`:
four jobs succeeded and `macos-latest` failed. `windows-latest` (35 min 24 s; its Test step 32 min
34 s of 55, 5,547 of 5,551 tests passed and 4 skipped; "Build the real-window pass" green),
`ubuntu-latest` (6 min 7 s; Test step 4 min 40 s) and the two "bare ubuntu container" jobs (7 min
42 s and 8 min 1 s; Test steps 5 min 39 s and 5 min 40 s, 5,545 of 5,548 passed, 3 skipped).
`macos-latest` (34 min 42 s; Test step 32 min 25 s) failed one test of 5,548:
`RefusedTokenDisplayTests.A_bulk_rows_kept_dialog_on_a_row_without_a_tab_says_the_same`, whose
row's kept dialog said "Git could not fetch from origin." without "Open settings…" instead of the
refusal (Notes, "Wave 1"). The keychain steps, the first run of any real store (WP-K 3.10), all
passed with their one test run, not skipped: Windows' Credential Manager in 2 s (no error 1312,
so D333's fallback is not needed), the macOS keychain in 3 s, and the Secret Service in both
containers in 2 s and 1 s, under `dbus-run-session` with gnome-keyring and `--cap-add=IPC_LOCK`.

Phase 7a's first run with code, for L1 and WP0 (`f1dc80e` to `8bfdd6b`), is
[40848974456](https://github.com/RahimPasha/VisualCommit/actions/runs/40848974456), for `37fb9e0`:
all five jobs succeeded: `windows-latest` (24 min 48 s; its Test step 22 min 4 s of 55, and "Build
the real-window pass" green), `macos-latest` (25 min 11 s; Test step 23 min 10 s), `ubuntu-latest`
(5 min 54 s; Test step 4 min 24 s) and the two "bare ubuntu container" jobs (6 min 23 s and 5 min
39 s; Test steps 4 min 31 s and 4 min 13 s).

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
CI's Test step on Windows, macOS and Linux has a limit of 55 minutes and each of their jobs 65
(D232); the containers keep 14 and 25. The owner looks again at CI's duration when the project is
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

Wave 1 is merged into `phase/7a-hosting-github` (2026-10-29, 09:30 to 13:05): WP-K at `2582e53`,
WP-F at `c562ff4`, WP-C at `afb91a7`, WP-G at `1d6b688` and WP1 at `a4a388e`, each built in its
worktree (`D:/Projects/Personal/vc-wt/p7a-<key>`, branch `wip/p7a-<key>`), reviewed by an agent
that did not build it, its findings fixed, and merged with `--no-ff` one at a time; WP1's merge ran
the lagged list (design 0.4) for the first time, every class passing. The lead's follow-ups are at
`8b02805` (CI's keychain steps, the containers' `IPC_LOCK`, `FailNext` refusing avatar and page
routes), pushed; the as-built notes and the lead's rulings are the design's section 16, and
CLAUDE.md, D270, plan.md's risk 34 and THIRD-PARTY-NOTICES.md follow them. CI's run on `8b02805`
(under CI) passed on four jobs, every real keychain's test included, and failed one test on
macOS, a race in the prompt channel on macOS and Linux (Notes); the whole default run passed and
its pictures differ from the baseline's only where they vary by nature (Notes); steps 4, 5, 7
and 8 are ticked and wave 1's worktrees are gone. The channel's race is proved on CI's Linux and
macOS jobs and fixed at `9d48d0b` (Notes; design 16 item 11). WP-S is merged at `7bea4fb` (built,
reviewed by an agent that did not build it, its one major and seven minor findings fixed, one
ruled on; design 17 item 1), with 7.11's wiring at `04952c8`; 7.14's runs passed (Hosting.Tests
whole 375 of 379 with the 4 skips, `Phase6ChecksS` 13 of 13 with its counts unmoved, and the lagged
list's five commands: 25, 66, 21, 6 and 6 tests, all passing) and CI's run on `5613450` is green on
all five jobs; steps 6 and 9 are ticked. WP-A is merged at `42b6be2` (design 17 item 2): 8.9's runs
passed, part A's 24 pictures and the 31 settings pictures it changes were inspected and pass (the
settings pictures differ from wave 1's run only in the Accounts button's slot), and CI's Windows job
on `c25326e` found a start that did not wait for finding git, fixed at `2a90e05`. Parts B and P's
checks are written, reviewed and ruled on (rulings 11 to 34). WP-B is merged at `b361388` with the
lead's follow-ups at `e40ddc8` (design 17 item 4: one waiting empty-tab start for every part). The
whole default run on `e40ddc8` failed only phase 1's 100k-commit layout timing under load, which
passes alone, and the lagged list passed; its earlier pictures differ from wave 1's run only in the
Accounts slot and where they vary by nature, and CI's run on `e40ddc8` is green on all five jobs
(Notes). Part B's ten pictures are inspected and pass (design 17 item 5); steps 10 and 11 are
ticked. Part G's checks are written, reviewed and ruled on (rulings 35 to 44; design 17 item 6).
WP-P is merged at `1958755` (design 17 item 7): 10.8's classes, part P's checks and part A's check 9
pass, and their 24 pictures are inspected and pass; part N's checks are written, reviewed and ruled on
(rulings 45 to 61; design 17 item 8). CI's run on `1958755` failed one test on all five jobs, a
double-click the headless harness turned into a triple click (Notes); the harness's fix is merged at
`ccd1ebe` (design 17 item 9), the lagged list passed on it, and step 12's left-panel half is done. The
lead's icon commit for WP-V is `fb43def` (design 11.1). WP-V is merged at `8810136` (design 17
item 10: built, reviewed by an agent that did not build it, one major and six minor findings fixed,
two ruled on); its builder and reviewer ran its classes, part G, part P, A9, B6 and B8, and the
review inspected part G's pictures and the part P and A9 pictures it changes. CI's run on `ccd1ebe`
failed two tests on macOS alone (Notes); P13's failure came once on the laptop too, in WP-V's build.
Both were races in the tests, fixed at `33313ef` (design 17 item 11). CI's run on WP-V's merge
also failed G1's new pixel check on macOS; its fix is being built (workflow `wzf5zbhxr`;
`wip/p7a-g1`). Next: when it is merged and pushed, run 11.6's checks on it with no agent running: the lagged
list; phase 1's checks, phase 3's walk-through, `Phase5` and `Phase6Look` through
`bash scripts/heavy-run.sh`, compared by region with `master`'s references; part G; part P, part A's
check 9 and part B's B6 (`--filter-method "*Check_B3_B6_B7*"`) and B8 again (design 17 items 3, 6 and
10); inspect every picture; read CI on WP-V's merge and on the fix; tick steps 12 and 14; then
WP-N. Each of parts B, P, G and N
gets its checks there just before its package (D244). The first steps of 7a, and the order to take
them in, are in the [phase 6 handoff](handoffs/phase-6.md) under "For the next phase", read with the note at its start: D241 to
D248 change how phases are built (`plan.md`'s "Workflow" and "Visual test gate", `CLAUDE.md`).
They come from the review of phase 6's cost that the owner asked for on 2026-10-26,
[review-before-phase-7.md](review-before-phase-7.md), kept as history; its proposals that no
decision names are still proposals.

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

- **Phase 7a started on 2026-10-28** from `master` at `5fe9148`, after the owner's reboot of
  11:36 that day (`WUDFHost`'s instances held 2 to 12 MB afterwards). At the start no worktree, no
  `wip/*` or `worktree-*` branch and only the main tree's fsmonitor daemon existed; C: had 36.1 GB
  free and D: 27.2 GB.
- **D240's connectors switch does not work**: this session, started after the reboot with
  `ENABLE_CLAUDEAI_MCP_SERVERS=false` in `.claude/settings.local.json`'s `env`, still listed the
  claude.ai connectors (Gmail, Google Calendar, Figma, Claude Docs and others) among its tools
  (Waiting on the owner).
- **The design's inputs lie outside the repository**, in `D:/Projects/Personal/phase7a-work/`:
  seven research notes (`research/r1` to `r7`, 2026-10-28: the credential and network code, the UI
  surfaces, the harness and gate, the first package's items, GitHub's API, the keychains, git
  2.56's credentials), and `lead-rulings.md`, the lead's 31 rulings on their questions, which the
  designer follows beside D249 to D263. A cloud session cannot see them; the design, once written,
  carries everything that matters.
- **The design's draft** (2026-10-28, 14:57 to 16:42): an architect wrote the scope and the
  contracts, seven drafters one or two briefs each (the drafts in `phase7a-work/design/`, 384 KB),
  and a consolidator assembled `docs/phase-7a-design.md` (297 KB) and numbered its decisions D265
  to D328, which `decisions.md` holds as proposed; the review may change them. 9 Opus agents, 3.66M
  tokens, 918 tool calls; none built or ran tests. It is about half as large again as D243's 150 to
  200 KB: thirteen work packages, each brief self-contained (sections 0 and 1 together 50 KB, every
  brief 13 to 26 KB), so a builder still reads about 70 KB. Its section 15 lists the open points.
- **The design's review** (2026-10-28 to 29, at `6fc5b2b`): four lenses (git safety and
  credentials; the token's security and privacy; tests, gate, harness and CI; conformance and fit
  with the code), 46 findings merged into 36 (F1 to F36: 1 blocker, 8 majors, 27 minors, none
  refuted), each blocker and major verified (20 Opus agents, 3.20M tokens). The findings lie in
  `D:/Projects/Personal/phase7a-work/review/findings.md`, outside the repository; the design and
  decisions carry their fixes. Seven fixers applied them in turn (six batches of findings, one of
  the lead's rulings and the owner's answers), a checker read the diff and found 13 gaps, and one
  more fixer closed them (9 Opus agents, 2.10M tokens); the new decisions are D329 to D338.
- **The lead's rulings on the design, 2026-10-29**, besides the owner's answers: accounts.json and
  7a's other JSON files in camelCase (D336); one `HostingTexts.Describe` for GitHub's failures
  (D338); CI's Windows job compiles the live project and never runs it (D337, amends ruling 26);
  Windows' real store tried on CI and taken off the job's list if refused (D333); ruling 3
  withdrawn (D277, the owner's answer to F1); D330 accepted (git's `erase` marks the account only
  after `GET /user` answers 401), with a text of its own for a refusal from git; D334 and D335
  confirmed. **The design's size is accepted at about 345 KB**, against D243's 150 to 200: it has
  thirteen work packages, each brief self-contained, and the review's fixes added 40 KB; a builder
  reads sections 0 and 1 (about 59 KB) and its own (13 to 28 KB), about 85 KB, far below what
  phase 6's builders read before their first edit (306k to 557k tokens). Section 1 (about 33 KB)
  and section 2 (about 28) are over D242's 25 KB per brief.
- **The first baseline run was disturbed by the session's own research** (2026-10-28, `5fe9148`,
  12:01 to 13:10, 1 h 6 min 58 s): seven research agents ran git experiments and local servers
  beside it for its first 50 minutes. 4,915 of the 4,917 tests that ran passed; the watchdog
  stopped phase 3's walk-through at 602 s (`Check_18b`, waiting for the sync scenario's
  template), phase 4's at 805 s (`Check_C13`, after `Check_B7` timed out waiting for the
  interactive rebase's log line) and phase 6's at 1,321 s (G23, as in every run since phase 6's
  gate), and phase 5's `Check_A2` timed out waiting 120 s for the merge tool. It is no baseline;
  the run was repeated on the quiet laptop from 13:54 (step 2). Lesson: no agent that runs
  processes works beside a heavy run, not even "light" git experiments.
- When phase 6 was merged (`3f34c87`), the working tree was clean and no worktree, `wip/*` or
  `worktree-*` branch was left. The gate's
  reference pictures, inspection verdicts and the lead's rulings lie outside the repository in
  `D:/Projects/Personal/gate6-reference/` and `D:/Projects/Personal/gate6-inspection/`; phase 7a's
  gate makes its own references on `master` if it changes earlier pictures in many places (phase 6's
  handoff, "Running the visual gate").
- **The review before phase 7 was applied on 2026-10-28** (D239 to D248, on `master`): the build
  trim (`Directory.Build.targets`, D239), the owner's Claude Code settings and a one-time clean-up
  of disk (D240), the split of phase 7 and the weekly allowance (D241), the rules for agents, the
  design, the checks, the test runs with the heavy-run lock (`scripts/heavy-run.sh`), the
  real-window rounds and the inspection (D242 to D247; `plan.md`'s "Workflow" and "Visual test
  gate", `CLAUDE.md`), and the development machine's git 2.56 (D248). The review's other proposals
  stay proposals. D240's connectors switch is still to be checked, and not known to work until it
  is: the next session's `/mcp` list should show no claude.ai connectors (the sub-agents of the
  session that set it, started after `.claude/settings.local.json` was written, still had their
  tools, Gmail, Figma and Calendar among them). The brief, `before-phase-7-review.md`, was deleted (it is in
  `be63d89`).
- **The build output is trimmed** (D239): on 2026-10-28, after a clean rebuild of the main tree
  (`dotnet build` at the root and the real-window project's Release build), the 40 `bin` and `obj`
  folders under `src/` and `tests/` that were built again took 0.64 GB instead of 6.96 GB (the
  other 8, 2.27 GB of outputs nothing local runs, were removed and not built again), a test
  project's `bin/Debug` about 49 MB instead of about 589, and D: had 27.2 GB free. The trim takes
  every package's files for other platforms on Windows, not only SkiaSharp's and HarfBuzzSharp's. The real-window pass has not run on the trimmed
  build yet; CI's Windows job first builds and tests it on the push of `master` that adds the file
  (its run is under CI). If either shows trouble with the part that keeps only the machine's
  platform, the `.pdb` part alone is the fallback (D239).
- **The development machine's git is 2.56.0.windows.4 since 2026-10-28** (D248; the owner updated
  it from 2.36.0.windows.1), first on `PATH` (`C:/Program Files/Git/ucrt64/bin/git.exe` in Git
  Bash, `C:\Program Files\Git\cmd\git.exe` from PowerShell, the same version). CI's jobs
  run Windows 2.56.0.windows.2 (since 2026-10-15; 2.55.0 before), macOS 2.56.0 (Homebrew),
  `ubuntu-latest` 2.55.0 and the two containers 2.43.0, so no machine runs 2.36 any more and the
  checks that assert git's own words only on 2.36 assert them on none (plan risk 26). On 2.56 a
  `git stash pop` refused by local changes exits 128, not 1: phase 3's check 15b accepts both
  (`281dd69`), as phase 5's report records under "Changes to expected results" (phase 3's report
  says "exit 1" in 15b's step 2 and in T5). CI is the only check on 2.43, and on 2.55's HTTP paths
  (the 2.55 below is trimmed). The status bar now shows `Git 2.56.0`, so earlier pictures that show
  it differ in that box from the lists of pictures of phases 1 to 6's gates.
- **Git 2.55 (CI's version) is on the development machine**, inside Visual Studio:
  `C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/CommonExtensions/Microsoft/TeamFoundation/Team Explorer/Git/mingw64/bin/git.exe`.
  Put that `mingw64/bin` first on `PATH` (otherwise its child processes fall back to the 2.56 on
  `PATH`). Phase 5's research used it to check what 2.55 writes differently, which phase 4 could only
  learn from CI runs. With it first on `PATH`, 6 of the 49 tests of `VisualCommit.App.Tests`'
  fetch, pull, push and remotes classes, those against the credential test server, fail locally
  and pass on CI: that trimmed git has no `git http-backend`, which the server runs (explained in
  phase 6; plan risk 28). It also lacks `sleep`, so phase 6's fetch hold does not hold with it.
- Phases 1 to 6 were built partly by sub-agents in git worktrees under `.claude/worktrees/`
  (git-ignored); the phase 3, 4, 5 and 6 handoffs' "Things that cost time" say what to watch
  for: a worktree starts from `master` and its agent must create its branch from the phase
  branch first; merged worktrees must be removed at once (a built worktree took about 6.4 GB in
  phase 6, and the review measured 0.658 GB with D239's trim; plan risk 27); a global
  `core.fsmonitor=true` leaves a git daemon per worktree running
  (phase 4's handoff says how to list and stop them before a gate).
- The default run takes about 55 minutes on the development laptop (56 min 25 s at phase 6's gate,
  rested, 5,037 tests in nine projects; 43 min 22 s at phase 5's, 3,233 tests in seven). On CI at
  phase 6's gate (run 40059391404) the Windows job took 26 min 47 s (its Test step 24 min 37 s of
  55), macOS's 25 min 22 s (Test step 23 min 33 s; 43 min 12 s in run 40019921605 before the gate),
  `ubuntu-latest`'s 5 min 50 s (Test step 4 min 21 s) and the containers' 7 min 45 s and 7 min 16 s.
  The real-window pass takes about 28 minutes (27 min 39 s for phases 0 to 6 at phase 6's gate),
  after a rest (a hot laptop fails Q1). See phase 6's handoff, "Running the visual gate", for how
  to run both and what to stop first. On the laptop the default run and whole-project runs go
  through `scripts/heavy-run.sh`, one at a time (D245), and a session's start makes one only when
  the machine changed or the tree holds code that is not pushed: git 2.56 and the trimmed build
  are new since phase 6's gate, so phase 7a's baseline run is such a run.
- **The first default run with git 2.56 and the trimmed build failed on load** (2026-10-28, on
  `abf6ca5`, through `bash scripts/heavy-run.sh`, after about 1.75 hours of builds and test runs
  that applied the review, so not rested): 66 min 47 s, against 56 min 25 s at phase 6's gate. The
  hang watchdog stopped `VisualCommit.VisualTests.Phase6` 1,232 s into it, in
  `Phase6ChecksG.Check_G23_pull_refused_inside_the_integration`, which had run its 180 s; the
  project's other 31 tests did not run. The other eight projects passed, 5,006 tests, and ended
  after: part L's walk-through 12 min 44 s, phases 0 to 2's 15 min 48 s, phase 4's 30 min 27 s,
  phase 5's 33 min 50 s, phase 5's undo walk-through 54 min 54 s, phase 3's 55 min 52 s,
  `VisualCommit.Git.Tests` 56 min 0 s and `VisualCommit.App.Tests` 66 min 44 s. G23 did not hang:
  its app log shows every git call taking 0.8 to 2.5 s (a `git rev-parse` takes about 80 ms alone
  with 2.56, measured from Git Bash), and G23 alone passes in 40 s. The review's run of
  2026-10-26, on git 2.36, failed the same way in `VisualCommit.App.Tests`, so the load of nine
  projects side by side, not git 2.56, is the likely cause; the review's S10 (a cap of
  `--max-parallel-test-modules`) is the measurement for it. `dotnet test` itself exits with 127
  when a test process is stopped (tried with `VISUALCOMMIT_TEST_HANG_SECONDS=5`, plain and through
  the script), so a 127 from `scripts/heavy-run.sh` is that, not the script.
- **The review's S10, measured, did not help** (2026-10-28, on `71e174e`, after 10 minutes' rest:
  `bash scripts/heavy-run.sh --max-parallel-test-modules 5`, S10's first half only, without
  `VisualCommit.Git.Tests` at 4 threads): 72 min 22 s, longer than the 66 min 47 s with nine
  projects at once, and the watchdog stopped `VisualCommit.VisualTests.Phase6` again in G23's pull
  check, 1,170 s into the run, after its 180 s. Its step times (`artifacts/visual/phase-6/timings/`)
  were 40 s to step 10's picture and 115 s more for steps 11 to 13, four pulls of about 29 s each:
  slow throughout, not stuck in one call. The other eight projects passed, 5,005 tests, and ended
  after: phases 0 to 2's 6 min 56 s, part L's 9 min 13 s, phase 4's 26 min 47 s, phase 5's 34 min
  43 s, phase 5's undo walk-through 40 min 18 s, `VisualCommit.Git.Tests` 60 min 27 s, phase 3's
  60 min 44 s and `VisualCommit.App.Tests` 71 min 1 s. A cap on projects side by side is
  therefore not adopted. All three default runs made on the laptop since phase 6's gate failed on
  the watchdog (2026-10-26 on git 2.36, and these two), so the cause is more likely the machine
  than git or the number of projects: right after this run, with no test running, `WUDFHost`
  (PID 10968, running since the boot of 2026-10-14) held 11,190 MB private, up from 9,569 MB on
  2026-10-26 (the review's N4), 32.4 of 35.9 GB was committed and 2.3 of 15.9 GB of memory was
  free. C: had 29.3 GB free and D: 27.2 GB. Until a default run passes on the laptop again, phase
  7a's gate cannot pass there; the owner's reboot comes first (Waiting on the owner).
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
  1,264 ms in round 1's pass and 1,805 ms in the counted one; plan risk 10).
- Packages: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and
  TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24),
  FlaUI.UIA3 5.0.0. Phases 3, 4, 5 and 6 added no packages.
- Git 2.55 on CI writes its own files and messages differently from 2.36, the development
  machine's git until 2026-10-28; phase 4's
  handoff, "Rules worth keeping", lists what it found and how the checks compare git's files,
  phase 5's adds the exit codes and side effects that differ between versions, and phase 6's
  "Things that cost time" what 2.55 and 2.56 did to its tests.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once. In the Bash tool, a long heredoc that holds
  apostrophes fails and turns `\n` in a string into a line break, and `sed -i` drops a CRLF
  file's carriage returns; write such a script to a file first.
- **Wave 1** (2026-10-29): one workflow of 22 agents (builders, reviewers, fixers, merge agents)
  ran 3 h 35 min and used about 4.9 million tokens of sub-agents. WP-K's and WP-C's first builders
  stopped unfinished and a second builder finished from their last commits. The reviews found 36
  items (one blocker, WP-G's paged list after ETags; two majors), all fixed or ruled on (design
  16). The worktrees, their branches and WP1's picture references are removed.
- **Wave 1's whole run and pictures** (2026-10-29, `069c5c7`, through `bash scripts/heavy-run.sh`,
  nothing else running): 5,547 of 5,551 passed and 4 skipped (the three real stores and
  `WindowsCredentialStoreTests`' Off-Windows test, D270) in 1 h 3 min 26 s; the projects ended
  after: phase 7a's walk-through 26 s, `VisualCommit.Hosting.Tests` 4 min 29 s, part L's 11 min
  59 s, phases 0 to 2's 16 min 15 s, phase 4's 25 min 20 s, phase 5's 29 min 9 s, phase 6's 44 min
  25 s, `VisualCommit.Git.Tests` 52 min 8 s, phase 3's 53 min 33 s, phase 5's undo walk-through
  53 min 48 s and `VisualCommit.App.Tests` 1 h 3 min 24 s. Its pictures, kept in
  `D:/Projects/Personal/phase7a-work/wave1-run-pictures/` as wave 2's reference, against the
  baseline's (step 2): 1,217 the same, 38 changed, 74 new (phase 6's whose walk-through the
  baseline's G23 had stopped). Four inspectors judged all 38, and the 74 against phase 6's gate run:
  every change varies by nature: a temporary folder's name (14), a caret (11), anti-aliased edges
  and icons and the indeterminate bar (12), `a2d`'s number of reads, and against phase 6's gate
  git 2.56's version in the status bar and its nine `advice.diverging` lines in `s19b` and `w7b`.
  None comes from wave 1's code (it changed no view, style or icon). Their verdicts are in the
  workflow's output (task `wtdlbm9d2`).
- **WP-B's whole run and pictures** (2026-10-29, `e40ddc8`, through `bash scripts/heavy-run.sh`, no
  agent running): 5,709 of 5,714 passed, 4 skipped and 1 failed, in 1 h 15 min 28 s, 12 minutes
  longer than wave 1's. The failure is `GraphLayoutTests.A_100k_commit_history_with_a_merge_every_50_commits_lays_out_in_under_2_seconds`,
  phase 1's in-memory layout of 100k commits (no git), which took 2,620 ms against its 2 s; run
  alone three times right after, it passed each time (the whole test, with building the history,
  in 1.6 to 2.9 s). It measures the processor under the run's load (eleven projects side by side),
  its first failure; it is watched at the next whole runs and dealt with at the gate if it fails
  again (plan.md's risk 12). The projects ended after: phase 7a's walk-through 9 min 31 s,
  part L's 11 min 11 s, `VisualCommit.Hosting.Tests` 11 min 13 s, phases 0 to 2's 23 min 9 s,
  phase 4's 29 min 1 s, phase 5's 32 min 46 s, phase 6's 48 min 35 s, phase 5's undo walk-through
  50 min 37 s, phase 3's 56 min 3 s, `VisualCommit.Git.Tests` 1 h 0 min 30 s and
  `VisualCommit.App.Tests` 1 h 15 min 27 s. The lagged list's five commands passed (25, 66, 21, 95
  and 23 tests). Its pictures, kept in `D:/Projects/Personal/phase7a-work/wpb-run-pictures/`,
  against wave 1's run (ruling 19): 1,264 the same, 34 new (parts A and B), 65 changed: the 31
  settings pictures of the Accounts slot, the same files as at WP-A's merge, and 34 that vary by
  nature (a temporary folder's name 16, `k10e` and the push refusals' paths in `16j` and `u21v`
  among them; a caret 11; anti-aliased edges and the indeterminate bar 7), each of which also
  changed between earlier runs or was looked at by the lead. Part A's 24 pictures are the same files
  as at WP-A's merge.
- **WP-P's merge: a double-click the harness turned into a triple click** (CI's run on `1958755`).
  The test clicks the sign-in row, signs in from code, and pull request #5's row then takes that row's
  place at the same point; when the double-click comes 500 ms or less after that click by the window's
  input clock, Avalonia 12.1.3's `MouseDevice` counts its presses 2 and 3 (it resets the count only
  after 500 ms or outside a 4×4 rect, whichever element was pressed) and `Gestures` raises no
  `DoubleTapped`, since press 2's element is not the remembered one. Fast CI machines always fell under
  500 ms; the laptop mostly not (it failed warm in Release in 2 of 18 runs alone). Found by four
  investigators and a judge (task `wt75y84uy`), reproduced on Windows with logged click counts. A user
  cannot reach it. The fix: `ShellDriver.DoubleClick` waits out the double-tap time after a press at
  the same spot and fails at once when its presses are not counted 1 and 2, with three tests in
  `ShellDriverTests`; built on `wip/p7a-dblclick`, reviewed by an agent that did not build it (five
  minor findings) and merged at `ccd1ebe` (design 17 item 9, with the lead's ruling that `Click` and
  `RightClick` stay as they are). On the merge `ShellDriverTests` passed 11 of 11 and
  `LeftPanelPullRequestsViewTests` 5 of 5 in Release, and the lagged list's five commands passed
  (25, 66, 21, 147 and 29 tests; `VisualCommit.VisualTests.Phase7a` has grown from 95 to 147 tests
  since WP-B's merge).
- **macOS on `ccd1ebe`: two failures new to CI** (run 40982132445). They are part P's P13: tool's
  list request came after hello's step 2 mark, and the two-tab start waits for hello's list only. The
  other is `KeyRouterTests`' branch drag: a key never reached the element that had the focus when the
  test read it. Each passed on the other four jobs and in every earlier macOS run. Investigators and
  a judge are on both while WP-V is built; a fix is merged before WP-V's merge runs. The judges
  (workflow `ww0ovir23`, high confidence, both test races, no product bug). P13: a sign-in lists
  every open tab (design 7.8, D289) through the session's one client, which sends one request at a
  time (D285), so tool's list can be answered after hello's line; step 1 is to wait for both tabs'
  lines (a wait only, recorded in the report). The branch drag: the drag's press selects row 4, whose
  details nothing waited for, and the keys the drag lets through move the focus (Ctrl+Tab and
  Ctrl+Shift+Tab reach the details' file row; Mod+Enter on it opens a diff). The diff's late focus
  move then fell between the test's read of the focus and a key's delivery: headless input runs the
  dispatcher's jobs before it delivers. The test is to wait for row 4's details and give the graph
  the focus before every key. Both fixes are merged at `33313ef` (`79e631b`, `b3fe91a`, the report's
  `5f7d1aa`; design 17 item 11): on the merge `KeyRouterTests` passed 7 of 7 and part P 4 of 4 in
  Release. A diff's pending focus, which the investigation found, goes to phase 8's keyboard pass.
- **CI on WP-V's merge: P13 again on Windows, and G1's new pixel check on macOS** (run 40993671970).
  WP-V's review added `AssertLoginLabel`, which reads where the name's ink ends to ±1 px; macOS draws
  the name's last column 1.4 px before its layout edge. The report gives layout positions, so the
  fix asserts the control's own layout of the badge exactly. It reads the ink only by rules that hold
  on every platform (each part's ink inside its box, none in the gaps), and audits parts A, B, G and
  P for other text-ink reads (workflow `wzf5zbhxr`, `wip/p7a-g1`).
- **macOS's failed refusal test is a product race on macOS and Linux** (CI's run on `8b02805`): .NET
  backs every pipe instance of one name with one listening Unix socket and closes it with the
  last instance (dotnet/runtime's `NamedPipeServerStream.Unix.cs`, `SharedServer.Dispose`, read by
  the lead); `AskPassBroker.ListenAsync` keeps one waiting instance and serves an answer without UI
  (WP-C's credential `get` and `erase`) to its end before it makes the next, so a second helper
  queued on the socket is dropped silently: a lost `erase` gives the generic dialog, a lost `get`
  git's fallback prompt. Found by four investigators and a judge (task `wtdlbm9d2`). The fix (a
  keeper instance on macOS and Linux, `MaxConnections + 1` there, diagnostics, the held test
  `AskPassChannelTests` and a failure report for the credential dashboard's tests; design 16 item
  11) was built on `wip/p7a-channel`, reviewed by an agent that did not build it (two minor findings,
  fixed) and merged in two steps: the held test alone at `6fc2d28`, whose CI run 40910893877 failed
  on all three Linux jobs and on macOS with exactly its three tests (the queued client: "Connection
  reset by peer" on Linux, an end without a line on macOS; the other 5,545 passed), which proves
  the cause, and the fix at `9d48d0b` (the 105 tests of
  the eight classes it touched pass on Windows). The channel's builder was stopped by an API
  safeguard's false positive ("reasoning_extraction") after its work was committed; its report was
  read from its transcript, and later prompts ask for an explanation of a mechanism, not reasoning.
- When a cloud session has worked on a phase, say so here. If it had to use a branch of its own,
  that name stands in the Branch column of the Phases table in place of the phase's.

## Waiting on the owner

- **Phase 6's acceptance**: given on 2026-10-24; merged into `master` (`3f34c87`); its gate ran on
  `b561b7c`. Still open, asked at acceptance and not yet answered (the handoff's "Waiting on the
  owner"; each answer becomes a decision, D329 on, since D239 to D248 went to the review before
  phase 7, D249 to D264 to phase 7a's start and D265 to D328 to its design): design 17.5's reading of "ten repos can be checked and
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
  package). Azure DevOps' and GitLab's accounts and sign-in are asked before 7b's design.
- **Answered during phase 7a's design review** on 2026-10-29, to be written into the design's
  decisions when the review's findings are applied: the real-window pass may open the default
  browser once, on the fake's page (D321); the status bar's button after a create reads "Open in
  browser" (D317); no palette command signs in to GitHub in 7a; the sign-out confirmation uses the
  primary button (D296). On the review's findings (`phase7a-work/review/findings.md`): F1, on git
  older than 2.31 the account is not used for git at all (ruling 3's fallback would have let git
  store the token in the user's own helpers); F2, Create pushes a branch under its own name and
  leaves an upstream of another name as it is; F15, avatar lookups send GitHub only the addresses of
  commits reachable from the GitHub remote's branches, and sign-out deletes the avatars folder; F21,
  a commit after the counted round that changes only the live project re-runs only the live command
  (extends D246), and the live command also runs once before the counted round.
- **For phase 7a's live command (D250), before step 16**: a fine-grained token that the owner
  creates for one sandbox repository on the owner's account, with the rights the design names,
  set in the environment variable the design names. It is never written to the repository.
- **D240's connectors switch** (Notes): `ENABLE_CLAUDEAI_MCP_SERVERS=false` in
  `.claude/settings.local.json` did not keep the claude.ai connectors out of this session. Turning
  them off another way (the variable set in Windows' user environment before VS Code starts, or
  the connectors switched off on claude.ai) is the owner's.
- **Still open from the review before phase 7** ([review-before-phase-7.md](review-before-phase-7.md);
  each answer becomes a decision, D329 on): its questions 1 (how the plan's limits count tokens),
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
- When you have a Mac at hand: the seven-step checklist at the end of the
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
