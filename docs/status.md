# Status

Last updated: 2026-11-18

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
| 7b. Azure DevOps and GitLab | Done | `phase/7b-hosting-azure-gitlab` | [phase-7b](handoffs/phase-7b.md) | [phase-7b](test-reports/phase-7b.md) |
| 8a. Release readiness in the app | In progress | `phase/8a-release-readiness` | | |
| 8b. Packaging and public beta | Not started | `phase/8b-packaging-beta` | | |

States: Not started, In progress, Blocked (cannot continue or close without the owner, or without
a session on the Windows machine; the reason is under "Waiting on the owner"), Awaiting
acceptance (whole gate passed, not yet merged), Done (merged).

## Current phase

Phase 8a, "Release readiness in the app" (plan.md's "Phase 8a"; D399), is in progress on
`phase/8a-release-readiness`, started on 2026-11-19 from `master` at `66063f3`. It is the in-app
half of the public release; 8b, the packaging, follows it. Phase 7b was accepted by the owner on
2026-11-08 and merged into `master` (merge commit `3dc7f79`); its gate ran on `cb9af6d`. Phase 7a was accepted on 2026-10-31 (merge commit
`019e68a`), phase 6 on 2026-10-24 (merge commit `3f34c87`), phase 5 on 2026-10-17 (merge commit
`c299939`), phase 4 on 2026-10-12 (merge commit `ce5f337`), phase 3 on 2026-10-11 (merge commit
`8b33745`), phase 2 on 2026-10-10 (merge commit `e039070`), phase 1 on 2026-10-10 (merge commit
`b5eb1ed`) and phase 0 on 2026-10-06 (merge commit `b5f4723`).

Gate commit: none yet. When a phase is set to "Awaiting acceptance", this line names the commit
that both passes of the gate ran on (or, by D246, the real-window pass's commit when it changes
only `tests/VisualCommit.RealWindowTests` after the commit the default run ran on); "Phase N accepted" checks that
nothing but docs changed after it.

### Progress

Each item is ticked in the commit that completes it, and each tick, like the start of each stage,
records the share of the plan's weekly allowance used at that point (D241). The start runs through
the reset of Thursday 2026-11-19 at 2:00 without that check until the design is reviewed (D415);
from step 4 on the session asks the owner for the share before each stage, and a checkpoint
records the answer, or "share not read". The numbers are plain step numbers, not requirement
numbers. The Build steps follow the design's work packages in the order of its waves (design 0.4);
they were renumbered when the design was reviewed, so an earlier commit's "step 5" (the floor) is
step 6 here. Phase 7b's checklist, with a note on each step, is in this file as of `3dc7f79`'s second
parent (`git show 1c2eca3:docs/status.md`).

Start:

- [x] 1. Phase branch created from `master` at `66063f3`, phase set to "In progress", this
  checklist. The owner's answers before the design were given before the start (D399 to D415, in
  five sittings on 2026-11-18 and 19, after the two workflows of the Notes' "Phase 8's start").
  `dotnet build` passed on `master` (0 warnings, 2 min 38 s). The baseline that D245 asks for
  after the Windows updates of 2026-11-11: the whole run lost the App tests to the hang watchdog
  under the load of the agents beside it, and the App tests then passed alone, 2,233 of 2,233, so
  every project has passed since the updates (Notes). (2026-11-19. Week: under 50 % used at 22:00
  on 2026-11-18; the reset is at 2:00 on 2026-11-19, D415.)

Design:

- [x] 2. Before the drafting: the search of the web for other uses of the name "VisualCommit"
  (D412), reported to the owner; the lead's rulings on the questions the readers left to the
  design ("Waiting on the owner", "Left to 8a's design"); and what the design needs measured and
  no brief holds: what the tests do on git 2.34.1 (D406; nothing below 2.43 has run), a pixel scan
  of phase 7b's pictures for each contrast fix's reach (D410), where Avalonia 12.1.3 sets the
  placeholder's opacity, how the custom-drawn graph can expose a row to UI Automation (D411), the
  macOS application menu (D409), and how the first-run states are scripted in both harnesses.
  Done 2026-11-19. The search found no product of the name (Notes, "The name's search"). The
  workflow `phase8a-design-research` (run `wf_7a57f7e4-dcb`: eight Opus agents, 2.96M tokens, 50
  minutes) wrote the eight inputs in `D:/Projects/Personal/phase8-work/research2/`, and the lead
  ruled on their open points: 75 rulings in all, in
  `D:/Projects/Personal/phase8-work/design/rulings.md`, which the design carries in short form.
  What the inputs measured: **git 2.34.1** fails 20 of 7,889 tests (CI's new job, run
  44479788558), in three groups: git's own words and exit codes that changed in 2.35 (7 tests),
  stash names below 2.36, where git names a stash after the last part of the branch (10 tests),
  and one fault of the app's (3 tests: a fast-forward of a branch that another worktree has
  checked out is refused by git only from 2.35, so on 2.34 the app moves it); the app uses no git
  command newer than 2.34, and the package is of middle size. **The contrast round** changes
  1,333 of the 1,350 pictures of phase 7b's counted round (the placeholder alone 1,293, the dark
  danger colour 360, the accent on rows 13); the plan's list of 35 danger-label pictures misses 6;
  the dark danger text on a control's background is 4.15:1, a gap `ThemeTokensTests` does not
  hold. The placeholder's opacity is one resource key of Avalonia's Fluent theme, which the app's
  tokens can set. **Names**: the 18 icon buttons the plan lists as unnamed take their names from
  their tooltips since 2026-10-10, so the names are weak and not missing; the true gaps are the
  activity log's toggle and three splitters. **The graph** can expose its selected row with three
  peers at most; which UI Automation event makes Narrator speak on a move is unproven until a
  spike (ruling 32). **The macOS data folder** in the docs is right. **Keyboard**: six gaps are
  real, and eight fixes change no earlier picture. **Notices**: the build output holds 53 files
  from 24 packages on Windows, and the present notices cover two components in full. (Week:
  share not read; the reset was at 2:00 on 2026-11-19, D415.)
- [x] 3. Design in `docs/phase-8a-design.md` (D243: about 150 to 200 KB, its decisions only in
  `decisions.md`; the 7b handoff's "Do first", step 4): the scope and what stays out, how the code
  and the harness name 8a (the 7b handoff's "Naming", with `8a` for `8`), its lagged list (D262),
  an estimate of what the phase adds to the default run (1 h 31 min 48 s at 7b's gate) and to the
  real-window pass (33 min 35 s), the earlier pictures each change touches, the shared contracts,
  and the work packages, each section a builder's brief of at most about 25 KB (D242). Its review:
  3 or 4 lenses, git safety and tests/gate among them, a verifier only for each blocker and major;
  its decisions recorded (D416 on).
  Drafted on 2026-11-19 by the workflow `phase8a-design-draft` (run `wf_2e69aec7-03e`: an Opus
  architect, eleven drafters and an integrator, 13 agents, 5.20M tokens, 1 h 43 min): sections 0
  to 13, eleven work packages in four waves after the lead's first package (design 0.4), eight
  parts of visual checks (0.5), and decisions D416 to D446, now in `decisions.md`. Its size is
  305 KB, over D243's 150 to 200, as 7a's and 7b's were (7b's 274.6 KB): eleven briefs of 17 to 22
  KB each, within D242's 25, and 39 KB of shared contracts; the integrator cut the repetitions
  (from about 320 KB), and a further cut would take content out of the briefs. Three of the
  decisions amend a ruling of the lead's, each for a fact its drafter found in the code: D420
  (ruling 73's common-dir call is no dead code: it serves a folder opened below its top), D425
  (ruling 49's hidden buttons on every row would put them all into UI Automation and stop two
  earlier real-window helpers) and D434 (the notices file and the guide spell as the app's labels
  do). The lead settled nine of the design's 21 open points (design 13.4, "Settled").
  Reviewed on 2026-11-19 by the workflow `phase8a-design-review` (run `wf_e96519ef-ed4`: five Opus
  readers through four lenses, git safety, the tests and the gate, the contracts against the code
  in two halves, and the owner's decisions with the texts, security and privacy; a verifier for each
  blocker and major, 17 of them; 22 agents, 4.32M tokens, 38 minutes). It made 58 findings: 17
  blockers and majors, every one confirmed by its verifier (two blockers and 13 majors after
  verification, two judged minor), and 41 minors. The lead accepted all of them. What mattered
  most: the winget install was started through `cmd.exe` with a bare `winget`, which `cmd.exe`
  looks for in the current folder first, so the app could have run a cloned repository's
  `winget.cmd` (now the full path it found); a second start that handed over deleted the running
  instance's `instance.json`, so every later start would have shown the "did not answer" notice;
  the icon's tool made no `.icns` and the candidates were outside the repository, both against
  D407; an Ubuntu text named a third-party package archive as "the Git team's"; and three test
  plans that would have failed at the gate (the notice's comparison, K10's walk once the palette
  has 35 commands, timing assertions in the most loaded test project). Corrected by the workflow
  `phase8a-design-fixes` (run `wf_77d18914-cbf`: a fixer per section on its own part of the design,
  three checkers, which found 33 misses, and a repair pass; 18 agents, 4.27M tokens, 39 minutes),
  then by the lead's rulings on what the passes left open (design 13.5). The review's decisions are
  D447 to D452. One finding went to the owner: D405 offers the winget install for a git that is
  missing, too old or not working, and ruling 44 with D423 had narrowed it to a missing git. The
  owner answered on 2026-11-20: a missing and a too-old git, not one that does not work (D453,
  carried into design 0.2, 1.3, 5.3 to 5.5, 12.3.4 and section 13). The design is now 356 KB: sections 0 and 1, which every builder reads,
  31 and 44 KB, and the briefs 18 to 29 KB, §5's alone over D242's 25. The stage's agents used
  13.8M tokens in three workflows (5.20M, 4.32M, 4.27M), after step 2's 2.96M. (Week: 25 to 50 %
  used, the owner's reading of 2026-11-20 after this stage; D415 ended here.)

Build, in the order of the design's waves (design 0.4). Each part's visual checks are written into
`docs/test-reports/phase-8a.md` and committed just before its package is built (D244). A builder
works in a worktree on `wip/p8a-<key>` and runs only the classes its section names; the lead merges,
and runs whole projects one at a time through `scripts/heavy-run.sh` (D245), never with an agent
that runs a process beside them (ruling 23).

- [x] 4. The lead's first package, WP0 (design section 1, its list in 1.16), in the main checkout:
  the contracts and stubs the packages build on, with no behaviour and no picture changed; the
  walk-through project `tests/VisualCommit.VisualTests.Phase8a` with
  `scripts/time-scripted-check.sh`'s `8a` case, and CLAUDE.md's Commands rows for it. (D397's
  `Authors` and `Copyright` and the `.slnx` comment were done in `e04762b`.)
  Done 2026-11-20 in `fc8a063`: built by one Opus builder (0.40M tokens, 26 minutes), then a code
  review of its diff (`phase8a-wp0-code-review`, run `wf_f3bdee43-0f6`: four Opus reviewers, a
  verifier for each finding, one fixer; 25 agents, 3.07M tokens, 29 minutes): 20 findings, all
  minor, 17 confirmed and fixed, 3 refuted; no contract of section 1 missing or wrong, no behaviour
  changed. `dotnet build`: 0 warnings. The lead's run of the classes 1.16 names, alone on the
  machine: `VisualCommit.App.Tests`' `ShellTests`, `MainWindowViewModelTests`, `AppShortcutsTests`,
  `PaletteEntriesTests`, `SettingsDialogViewTests`, `PromptDialogsTests`,
  `GitRepositoryProviderTests`, `StatusBarTests`, `AppSessionTests`, `GitAccessTests`,
  `AppVersionInfoTests` and `AppLinksTests`, 124 of 124 in 2 min 32 s; the 8a project, 7 of 7;
  `VisualCommit.Git.Tests`' `GitSetupStatesTests` and `GitLocatorTests`, 16 of 16. The first two
  parts' checks, L and I, were written and reviewed beside it and committed in `a1fc720`, before
  their UI (D244): `docs/test-reports/phase-8a.md`, nine checks (one Opus writer, 0.35M tokens;
  the review `phase8a-checks-review-L-I`, run `wf_17f3e7ad-e6a`: two reviewers, a verifier for each
  of four majors, one fixer; 7 agents, 1.46M tokens, 39 minutes; all twelve findings applied, the
  design's 12.3 corrected with them). CI's run for the push, 44703936572, is green in the five
  regular jobs (the CI section). (Week: 25 to
  50 % used at the owner's reading of 2026-11-20, before this step; not read since.)

Wave 1:

- [x] 5. §2, the contrast round (D410, D418, D419), part L: the placeholder, the dark danger colour
  with the dark label, the dark accent on hovered and selected rows; `ThemeTokensTests` as one
  table with no list of gaps; the scan's script and lists, `compare-pictures.py --expected`.
  Merged on 2026-11-20 in `3b9d159` (`wip/p8a-colours`, nine commits, 44 files). The builder saw 10
  of `ThemeTokensTests`' and `PanelViewTests`' 24 red on the old code and green after; part L's
  seven scripted methods wrote their 31 pictures (the class alone: 131 s). Its review (three
  readers, a verifier for each major) found one thing three times over: earlier checks the round
  makes false that the builder had not changed (phase 3's check 6, phase 5's B5, phase 6's G25),
  each seen red and then corrected, with one minor fault in part L's own helper (a plain `git
  status` that rewrote the index under the running app). Every changed expectation is in the
  report with its reason ("Changes to expected results"). The builder's comparison of its own runs
  with phase 7b's references: 326 changed pictures, 304 inside their expected regions, 22 outside,
  each explained (caret blinks, progress bars, temporary names, placeholders the scan cannot see).
  The comparison of the whole run is step 8's.
- [x] 6. §3, the git floor (D406, D420): the worktree check before a fast-forward on every git,
  the stash names below 2.36, the branches the floor makes dead removed; CI's floor job green, and
  in that commit `GitVersion.Minimum` 2.34.1 with the "too old" texts, `continue-on-error`
  removed, `requirements.md`, the README and CLAUDE.md's prerequisites.
  Done 2026-11-20. The package was merged in `a63390c` (`wip/p8a-floor`, two commits, 33 files;
  its review found no major), and the lead removed the broker's `gitVersion` at the merge (D420).
  **CI's floor job passed on its first round**: run 44727459663, on the three merges (`d63c012`),
  7,989 tests on git 2.34.1 with 3 skipped and none failed, in a Test step of 7 min 6 s; before
  the package it failed 20. So the rewritten stash ids, 2.34's refusal of `zdiff3`, phase 5's U18
  from its undo's stash on and the fast-forward's worktree read all hold on 2.34.1, and the 20
  tests hid no further assertion. The lead's commit after it moves `GitVersion.Minimum` to 2.34.1
  (with `GitVersionTests` and `MainWindowViewModelTests`' "too old" text), removes the floor job's
  `continue-on-error`, so that its failure fails the run from now on, and changes
  `requirements.md`, the README, CLAUDE.md's prerequisites and plan.md's line; on that tree
  `dotnet build` has no warning, and `GitVersionTests`, `GitLocatorTests`, `GitSetupStatesTests`
  (36 of 36), `MainWindowViewModelTests`, `GitRepositoryProviderTests`, `GitAccessTests` and
  `StatusBarTests` (28 of 28) pass. No fixer's round was needed. Still to read, in the macOS job's
  pictures: phase 5's `a2d-show-reads`, which should show the second `rev-parse` call with
  `--path-format=absolute` (design 3.6).
- [x] 7. §4, one instance per data folder (D421, D422, D448), part I, and the macOS data folder's
  test in CI's macOS job.
  Merged on 2026-11-20 in `b70abdb` (`wip/p8a-single`, seven commits, 19 files): the guard with
  its 17 tests, the notice, `BringForward`, `Program.Main`'s exit codes, part I's scripted I2 and
  I3 with their four pictures, `AppPathsTests`' test of each platform's documented folder. Its
  review found two faults that tests now hold: on Windows `instance.json` could not be replaced
  while a second start had it open, so the holder now tries for up to a second before its
  Warning; and an endpoint name the pipe API refuses threw where it must count as a refused
  connection. `Program.Main`'s exit codes and the notice under a real desktop lifetime are proven
  only by the real-window launches (step 18). The macOS job's reading of `AppPathsTests` is with
  the CI run of the merges' push.
- [ ] 8. Wave 1's close: the lagged list (D262) and the whole default run; §2's comparison against
  phase 7b's references (`D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/`) with the
  scan's boxes, every difference outside them explained; the run's scripted pictures then become
  the references of every later merge (`D:/Projects/Personal/phase8-work/references/wave1/`).

Wave 2:

- [ ] 9. §5, the first-run experience (R4, D405, D423, D424, D449), part F: the detection's states
  with git's own message for a broken `~/.gitconfig` and the missing askpass helper, "Check again"
  without a restart, the "Git program" setting, the winget install after a confirm, the macOS and
  Linux guidance, the OpenSSH line and the netrc note; winget for a missing and for a too-old git
  (D453).
- [ ] 10. §6, the keyboard pass (R7, D425 to D427, D452), part K: the gaps each shown by a failing
  test first, the focus after "Choose" and after a row's action, the loading diff's late focus,
  the commit boxes while "Committing…" runs (D392), "Cancel {title}" in the palette.
- [ ] 11. §7, the icon's pipeline with a placeholder mark (R5, D407, D428 to D430, D447), part B:
  the masters, the tool, the `.ico`, the `.icns` and the PNG files, the window's and the
  executable's icon; then the owner's choice (rounds in `docs/brand/icon/`), the chosen mark's
  files, and the README's "Name" note naming the icon.
- [ ] 12. Wave 2's close: the lagged list and the whole default run; the pictures against wave 1's
  references, with only the changes design 0.7 names; D427's fixer if §6's test asks for it.

Wave 3:

- [ ] 13. §8, the About section and the complete third-party notices (R6, D398, D409, D431 to
  D434), part A.
- [ ] 14. §9, screen readers (D411, D435, D450), part U: the spike first (the owner told), the
  graph's selected row, a name on every control with the sweep; merged after §8; a short listen
  with Narrator by the owner at its merge.
- [ ] 15. §10, the performance pass (D436 to D438): the log lines and the opt-in class.
- [ ] 16. Wave 3's close: the lagged list, the whole default run and the pictures; then the
  "before" series on the rested laptop, the owner told, and a change only where a trigger fires,
  after a ruling.

Wave 4 and the review:

- [ ] 17. §11, the user guide (D408, D439 to D441, D451), part G: the pages, the workflow that
  builds the site and runs the links' tests, the pictures in both themes, the app's links and F1.
  (GitHub Pages is turned on at "Phase 8a accepted", before `master` is pushed.)
- [ ] 18. §12, the real-window part for 8a's checks (`Phase8aRealWindowPass`, D442 to D446): built
  on the Windows machine, each of its 13 methods run alone, the owner told before each.
- [ ] 19. An independent review of the code before the gate, each finding verified, the confirmed
  ones fixed with tests; then §12's merge, the lagged list and the whole default run.

Gate:

- [ ] 20. The live command once before the gate's pictures, the owner told (D335). It needs the
  three tokens made again first: GitHub's expires on 2026-11-29, Azure DevOps' and GitLab's on
  2026-12-04 ("Waiting on the owner").
- [ ] 21. The visual test gate: D246's two runs of 8a's real-window checks on their own, the
  owner present; `dotnet test` and the real-window pass on one commit; the list of pictures; the
  live command on the counted round; every picture of both passes inspected, the changed earlier
  ones against the references; the guide's site from the gate commit's `guide-site` artifact; the
  checks by hand (the Narrator walk-through of 21 steps, D411 and D450; phase 6's System step,
  D224; a second start from the lead's isolating start file); `docs/test-reports/phase-8a.md`
  completed.

Closing (CLAUDE.md's "Closing a phase"):

- [ ] 22. `docs/architecture.md` describes what is built.
- [ ] 23. `docs/plan.md`: what 8a did not deliver moved to the phase that owns it, the risks
  updated.
- [ ] 24. `docs/handoffs/phase-8a.md` written from the template.
- [ ] 25. The cold-read check, and its gaps fixed.
- [ ] 26. The CI run of the gate's commit read and recorded here, with each job's result.
- [ ] 27. This file: links to the handoff and the report, the "Gate commit", what waits on the
  owner, and the state "Awaiting acceptance" or "Blocked"; the owner told what passed, what did not
  and what needs a decision.

### CI

Recorded here at the end of each phase, after each merge and after each change made on `master`
outside a phase that starts a run: the last CI run, its commit and its result in each job.

Phase 8a's runs on `phase/8a-release-readiness`, the newest first. CI has six jobs since
`e04762b`: the sixth, "ubuntu 22.04 container, git 2.34 (the floor)", did not fail the run until
`bba59c7`, which made it required after its first green run.

- [44727459663](https://github.com/RahimPasha/VisualCommit/actions/runs/44727459663), for
  `d63c012` (wave 1's three merges and the broker's `gitVersion` removed): all six jobs succeeded,
  none failed a test. **The floor's job passed for the first time**: 7,989 tests on git 2.34.1
  with 3 skipped, Test step 7 min 6 s (20 failed in every run before). `windows-latest` 7,992
  tests with 4 skipped, Test step 40 min 23 s of 70, its keychain step green and "Build the
  opt-in projects" green (the real-window project compiles with the palettes' new members);
  `macos-latest` 7,989 with 3 skipped, Test step 39 min 41 s (`AppPathsTests`' test of the
  documented data folder ran there and passed: the skipped count is the same 3 as before, which
  closes plan.md's doubt about the macOS folder); `ubuntu-latest` 7,989 with 3 skipped, 6 min 46
  s; the two "bare ubuntu container" jobs 7,989 with 3 skipped each, Test steps 4 min 57 s
  (Ubuntu's packages) and 7 min 3 s (Microsoft's installer). Wave 1 added 68 tests.
- [44703936572](https://github.com/RahimPasha/VisualCommit/actions/runs/44703936572), for
  `d37036a` (WP0, `fc8a063`; parts L's and I's checks; the icon's round 2): the run succeeded.
  `windows-latest` green, 7,924 tests with 4 skipped, Test step 40 min 49 s of 70;
  `macos-latest` green, 7,921 with 3 skipped, Test step 45 min 44 s (under the 56 minutes at which
  D377 raises the limit, and close to phase 7b's worst, 46 min 21 s); `ubuntu-latest` green, 7,921
  with 3 skipped, 6 min 37 s; the two "bare ubuntu container" jobs green, 7,921 with 3 skipped
  each, Test steps 5 min 2 s (Ubuntu's packages) and 7 min 0 s (Microsoft's installer); the
  floor's job failed the same 20 of 7,921 on git 2.34.1 (Test step 7 min 22 s), which does not
  fail the run until step 6. WP0 added 32 tests and changed no earlier one's result.
- [44479788558](https://github.com/RahimPasha/VisualCommit/actions/runs/44479788558), for
  `e04762b` (the floor's job, D397's two properties, the solution's comment). First attempt:
  `ubuntu-latest` green (Test step 5 min 7 s) and `macos-latest` green (Test step 36 min 38 s, its
  keychain step green); `windows-latest` failed one test of 7,892, phase 6's
  `PaletteGrowthTests.The_build_and_a_one_letter_query_grow_in_step_with_the_refs` with a ratio of
  8.1 against 6.3 (plan risk 46, the first time on Windows), 7,887 passed and 4 skipped, Test step
  22 min 53 s; both "bare ubuntu container" jobs were cancelled at their 25-minute job limit,
  because Ubuntu's package servers were slow that hour (the set-up step took 18 min 27 s in one
  and never ended in the other, where it takes about a minute); the floor's job failed 20 of
  7,889 tests on git 2.34.1 in a Test step of 7 min 15 s, after a set-up of 19 min 3 s (Progress,
  step 2, says what the 20 are). The lead ran the failed and cancelled jobs again at 01:38 on
  2026-11-19. Attempt 2: the run succeeded. `windows-latest` green, 7,892 tests with 4 skipped,
  Test step 40 min 30 s of 70 (under the 56 minutes at which D377 raises the limit, but close to
  phase 7b's worst, 41 min 6 s), its keychain step green, "Build the opt-in projects" 33 s; the
  two "bare ubuntu container" jobs green, 7,889 tests with 3 skipped each: Microsoft's installer
  with a set-up of 1 min 51 s and a Test step of 5 min 19 s, Ubuntu's packages 41 s and 6 min 54
  s, their Secret Service steps green; the floor's job failed the same 20 of 7,889 on git 2.34.1
  (Test step 6 min 59 s, set-up 3 min 6 s), which does not fail the run until step 6.

The run for phase 7b's merge on `master` is
[42574680274](https://github.com/RahimPasha/VisualCommit/actions/runs/42574680274), for `1a4a555`
(the merge commit `3dc7f79` and the commit that set phase 7b to "Done"): all five jobs succeeded,
none failed a test: `windows-latest` 7,892 tests with 4 skipped, 27 min 33 s (Test step 25 min 33 s
of 70; its keychain step green; "Build the opt-in projects" 18 s), `macos-latest` 7,889 with 3
skipped, 23 min 47 s (Test step 22 min 49 s; its keychain step green), `ubuntu-latest` 7,889 with 3
skipped, 8 min 40 s (Test step 6 min 40 s), and the two "bare ubuntu container" jobs 7,889 with 3
skipped each: Ubuntu's packages 7 min 17 s (Test step 5 min 25 s) and Microsoft's installer 7 min
57 s (Test step 5 min 51 s), their Secret Service steps green.

The last run on `phase/7b-hosting-azure-gitlab` was
[42522049151](https://github.com/RahimPasha/VisualCommit/actions/runs/42522049151), for the Gate
commit's code `cb9af6d` (D394 and D395's merge; the branch at `9b044af`, which changes only docs
after it): all five jobs
succeeded, none failed a test: `windows-latest` (git 2.56.0.windows.4) 7,892 tests with 4 skipped, 25
min 42 s (Test step 23 min 41 s of 70; its keychain step green; "Build the opt-in projects" 19 s),
`macos-latest` (2.56.0) 7,889 with 3 skipped, 35 min 9 s (Test step 33 min 9 s; its keychain step
green), `ubuntu-latest` (2.56.0, image `ubuntu-26.04`) 7,889 with 3 skipped, 8 min 28 s (Test step 6
min 23 s), and the two "bare ubuntu container" jobs (2.43.0) 7,889 with 3 skipped each: Ubuntu's
packages (SDK 10.0.114) 7 min 41 s (Test step 5 min 28 s) and Microsoft's installer (SDK 10.0.403) 8
min 19 s (Test step 5 min 51 s), their Secret Service steps green. Every Test step is under four
fifths of its limit (D377). The commits after `9b044af` change only docs and started no run.
Phase 7b's 29 runs on its branch, each with its jobs, are in that branch's copy of this file
(`git show 1c2eca3:docs/status.md`).

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
at which the lead raises D232's limit of 55 (D186's rule). Each of the phase's runs, with what it
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

The owner said "Start phase 8" on 2026-11-09 and gave the first answers before its design ("Waiting
on the owner", "For phase 8, before its design"). On 2026-11-18 the owner reconsidered the licence
and kept MIT with the owner's name in the copyright line (D397), and asked for an About screen
that names the owner (D398). Later that day the owner said "Start phase 8" again and answered at
once: Fable 5.2 leads phase 8 (CLAUDE.md's Models rule), under 50 % of the week's allowance was
used at 22:00, four hours before the reset of Thursday 2026-11-19 at 2:00, and no legal or
professional point is left, so the hold on the two signing questions was lifted. Two workflows of
Opus agents then read the repo for phase 8 and researched the release questions on the web (Notes,
"Phase 8's start"), and the owner answered the questions in five sittings that night: D399 (the
split into 8a and 8b) to D415, carried into plan.md's "Phase 8a" and "Phase 8b" sections,
`requirements.md`, the README and CLAUDE.md's counting of phases. Phase 8a was started the same
night on `phase/8a-release-readiness` (Progress, step 1).

Next: step 8, wave 1's close. Steps 5 to 7 are done: the three packages are merged, CI's floor
job passed on git 2.34.1 at its first round, and the lead's commit after it moves the minimum to
2.34.1 and makes that job required. In this order: (1) read CI's runs: the one on the merges
(44727459663: the Windows job's build of the real-window project, the macOS job's `AppPathsTests`
and its `a2d-show-reads` picture) and the one on the commit that moves the minimum, in every job.
(2) With no agent at work: the whole default run through `bash scripts/heavy-run.sh` (about 1 h
50 min; its log is `D:/Projects/Personal/phase8-work/wave1/default-run-wave1.log`), then the
lagged list of design 0.4, one command at a time. (3) §2's comparison: `python -I
scripts/scan-contrast.py D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual <out> --jobs
4` (its lists must equal `docs/test-reports/phase-8a-contrast/`'s), then `python
scripts/compare-pictures.py <the same references> artifacts/visual <out> --expected
<out>/expected.json`; every picture under "Changed outside the expected regions" and "Changed, not
expected" is explained or is a finding; the lead inspects part L's and part I's pictures. (4) The
run's scripted pictures are copied to `D:/Projects/Personal/phase8-work/references/wave1/` and are
the references from then on. Then step 8 is ticked, and before wave 2 (step 9) the owner is asked
for the week's share (D241). The icon's round 2 is with the owner (Notes, "The icon"). By hand
before §5's merge, the owner told: `cmd.exe` started as the app will start it, with `/S /K` and
winget's quoted path and `--version`, to see that its window stays open (design 13.4 item 7).
The [phase 7b handoff](handoffs/phase-7b.md), "For the next phase", says what to reuse, what to do
first and which risks to watch.

Once a phase is running, this always names the single next thing to do and is kept accurate in
every commit.

### Notes for whoever resumes

- **Wave 1 is merged (2026-11-20); step 8, its close, is next.** The workflow `phase8a-wave-1` (run
  `wf_463ba2b4-db6`: three Opus builders, nine reviewers, a verifier for each of 11 majors, three
  fixers; 26 agents, 6.33M tokens, 2 h 35 min) built, reviewed and fixed the three packages in
  their worktrees; no file was changed by more than one. The lead merged them in the order
  colours, floor, single (`3b9d159`, `a63390c`, `b70abdb`), removed the broker's `gitVersion`
  (D420), and ran on the merged tree, alone on the machine: `dotnet build` and the builds of the
  real-window and live projects, 0 warnings; the 8a project 25 of 25 (53 s); the Git.Tests classes
  of the prompt broker and the credentials 265 of 265; `PromptDialogsTests`, `ThemeTokensTests`,
  `SingleInstanceGuardTests` and `AppSessionTests` 54 of 54. The worktrees
  `D:/Projects/Personal/vc-wt/p8a-colours`, `p8a-floor` and `p8a-single` and their `wip` branches
  were kept for the floor's rounds; the floor job is green, so they can go at the wave's close.
  Each builder's and fixer's notes for the lead, in full, are in
  `D:/Projects/Personal/phase8-work/wave1/for-the-lead.md`. Still the lead's from them, at the
  close unless named: architecture.md (the tokens' table; the fast-forward's worktree read; the
  guard, the notice and the data folder's two new files; the removed D277 passages); plan.md (line
  768's 35 pictures replaced by the scan's lists; line 778 closed by `AppPathsTests`; the risks the
  floor names: the mirror refspec, a branch being rebased or bisected in another worktree on 2.34;
  a data folder on a network share). What wave 1 built otherwise than written is in the design's
  1.17, which the later packages' builders read with section 1. For the later packages:
  §7 gives the notice window its icon; §9's sweep shows the notice through
  `InstanceNoticeHost.Show`; §12 links `Phase8aExpectations*.cs`, whose parts L and I hold the
  launches' facts; `l3a` and `l3f` change at §5's, §8's and §11's merges (design 0.7).
- **Phase 8's start, 2026-11-18** (no branch yet: everything so far is on `master` or outside the
  repo). The machine changed since phase 7b's gate (Windows updates KB5132061 and KB5132688 on
  2026-11-11, and a boot that day; git 2.56.0.windows.4 and SDK 10.0.303 as before), so step 6's
  whole default run was started at about 22:05 through `scripts/heavy-run.sh`
  (`D:/Projects/Personal/phase8-work/baseline/default-run-2026-11-18.log`, whose last line is the
  exit code). `dotnet build` at the root before it: 0 warnings, 0 errors, 2 min 38 s. D: had
  19.5 GB free and C: 29.1 GB. Four `git fsmonitor--daemon` processes were running (the owner's
  global `core.fsmonitor=true`); none was stopped. **The run is no clean baseline**: in 1 h 30 min
  48 s eleven of the twelve projects passed (6,706 tests, 4 skipped as always, none failed), and
  `VisualCommit.App.Tests` was stopped by the hang watchdog 2,478 s into its run, when
  `RepositoryOperationsSequenceTests.Continue_and_Skip_of_a_list_that_stops_again_say_how_far_it_came_and_a_refused_commit_opens_the_dialog`
  had run for 180 s (exit code 127, `dotnet test`'s for a stopped process). Two workflows worked
  beside it, up to 13 Opus agents at once that read files and the web and ran no process but
  their file searches (one ran a single `echo` by mistake): the lead judged that light enough,
  against this section's rule on agents beside a heavy run, and it was not. The lead then ran
  `VisualCommit.App.Tests` again alone, with nothing beside it, from 00:07 on 2026-11-19
  (`app-tests-alone-2026-11-19.log` in the same folder): 2,233 of 2,233 passed in 33 min 35 s, so
  the stop was the load beside the run and no fault of the machine's updates. Taken together,
  every project of the default run has passed on the machine since the updates: eleven in the
  whole run (5,655 tests and the 4 always skipped) and the App tests alone. `dotnet build` on the
  phase's first code commit, `e04762b`: 0 warnings, 0 errors, 2 min 35 s.
  The workflows, whose briefs are in `D:/Projects/Personal/phase8-work/research/`, outside the
  repo: `phase8-repo-map`, seven Opus readers and a critic, 2.27M tokens in 62 minutes
  (`repo-ship.md`, `repo-git.md`, `repo-a11y.md`, `repo-perf.md`, `repo-gate.md`, `repo-owed.md`,
  `repo-about.md`, `repo-critic.md`), and `phase8-release-research`, six Opus researchers and three
  skeptics, 2.09M tokens in 48 minutes (`web-installers.md`, `web-winsign.md`, `web-macos.md`,
  `web-git.md`, `web-channels.md`, `web-brand.md` and three `web-verify-*.md`; the skeptics
  confirmed 106 of 128 claims and corrected the rest, and where a brief and its skeptic differ the
  skeptic's file is the one to go by). The researchers put neither the owner's name nor the
  product's into any web request; the search for other uses of the name "VisualCommit" is allowed
  since D412 and not yet made. Things the readers found that the docs had wrong or did not say:
  no code sets `DOTNET_ROOT` for the askpass helper (plan.md's 8b item says so now); the macOS
  data folder in the docs is unproven and probably wrong (plan.md's 8a items); the app shows its
  own version nowhere and has no icon of any kind; nothing stops two instances.
- **The name's search** (D412), made by the lead on 2026-11-19 with five web searches, one of
  them extended: "VisualCommit" and "Visual Commit" as software, as a git client, extension or app,
  as a trademark, company or domain, and as a repository, package or store listing. None found a
  product, repository, package, extension, company or trademark of that name. The nearest names
  are "VisualGit" (an educational Python app on GitHub, and other small projects called
  visual-git), GNOME's "Commit" (a commit message editor) and a registered mark COMMIT of Commit
  Incorporated for database software; none is a git client called by this name. What the searches
  cannot see: the trademark registers themselves (the USPTO's and CIPO's searches are interactive
  pages), domain registrations and the app stores' own catalogues, so a look there by hand is the
  owner's if wanted. One of the five queries also carried the owner's GitHub handle beside the
  name, which D412's leave did not cover; the handle is public on the repository's page, and the
  lead told the owner. D21's search of 2026-10-06 found the same: no existing product.
- **The icon** (D407, D447): **the owner chose A, Lens, on 2026-11-20**, with no change asked
  for (round 1, `docs/brand/icon/round-1/`, `306f61f`; its sheet is also on a private page of the
  owner's, https://claude.ai/artifact/BDztvt827qm6QeypyqZmxM). **Round 2 is with the owner since
  2026-11-20**: `docs/brand/icon/round-2/` holds Lens worked out into its three masters by design
  7.3's rules (`icon.svg`, the chosen drawing at four times its size; `icon-small.svg` on a 16
  grid for 32 pixels and below; `icon-macos.svg` on Apple's rounded square), a runner-up for the
  small sizes (`icon-small-alt.svg`, a solid eye with a blue pupil), three pictures of the real
  pixels, the sheet `candidates.html` and a README with what is still soft; the sheet is also at
  https://claude.ai/artifact/Xez1bXLnA2p4XNsQ7wtkcb. A plain down-scaling of the chosen drawing is
  mush at 16 pixels, which is why the round was needed. It was made by the workflow
  `icon-round-2-lens-masters` (run `wf_b5547917-771`: three Opus draughtsmen with different aims,
  each rendering and judging its own work, three judges, one final set; 7 agents, 1.35M tokens, 56
  minutes); all three judges chose the set faithful to the owner's drawing. The working files are
  in `D:/Projects/Personal/phase8-work/icon/round-2/`. The lead can look at a drawing: `python
  D:/Projects/Personal/phase8-work/icon/render/render_icon_sheet.py <folder with the three
  masters>` renders them with headless Edge at every size on both desks, with the small sizes
  enlarged. **Asked of the owner**: yes to the set or what to change; the small sizes outlined
  (the lead's recommendation) or solid; and, only if wanted, a blue one step lighter (`#5866E6`)
  for dark taskbars. With the owner's word before §7 starts (step 11), §7's masters are round 2's
  files and no placeholder is written (design 7.8); §7's builder compares the tool's renders (Skia)
  with round 2's pictures (Edge) before the files are frozen. The look-alike check against Git's
  logo and the other clients' icons needs the owner's leave for an image search, or is made by eye.
- Nothing else is in flight. `master` holds everything; the working tree was clean when phase 7b was
  merged (`3dc7f79`). **Cleaned up on 2026-11-18, at the owner's word** (the 7b handoff's "Do
  first", step 3): the seven worktrees under `D:/Projects/Personal/vc-wt/` (each clean, its branch
  merged) with that folder, and the 23 merged `wip/p7b-*` branches. Left, all local:
  `wip/p7b-palette-growth` (the refuted fix, plan risk 46) and the two `backup/*` branches, which
  must never be pushed (D396); whether they go is the owner's. Removed outside the repository, as
  D240 removed phase 5's: `gate6-reference`, `gate6-inspection`, `gate7a-reference` and
  `gate7b-reference` under `D:/Projects/Personal/` (the gate folders of an accepted phase serve no
  later gate; the docs that name them are history), and the 622 .NET build output folders (`bin`
  and `obj`, 6.3 GB) inside the `verify/` and `review/` scratch of
  `D:/Projects/Personal/phase7b-work/`. Still there: `D:/Projects/Personal/phase7b-work/` (about
  6 GB: the research notes, the design's drafts, the review's findings, the lead's rulings, the
  live runs' `run.txt` copies, the verifiers' source copies and pictures, the kept gits of the 7b
  handoff's Environment in `research/scratch-g3-git/` and `verify/git243/git-2.43.0-hb/`, and, in
  `gate/`, the gate's logs, the counted round's copy `round-3dc0887/`, whose `visual/` is phase
  8's references, and the inspection's verdicts) and `D:/Projects/Personal/phase7a-work/` (1.2 GB:
  phase 7a's notes, live run copies and pictures; history only). D: had 19.5 GB free afterwards
  (12.7 GB before), C: 25.4 GB.
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
  run Windows 2.56.0.windows.4 (since 2026-11-02; 2.56.0.windows.2 from 2026-10-15, 2.55.0 before), macOS 2.56.0 (Homebrew),
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

- **Phase 8a, from the owner** (2026-11-19; the owner's answers D399 to D415 are below, under "For
  phase 8, before its design"):
  - **Before wave 2 (step 9): the share of the week's allowance used** (D241). On 2026-11-20 it
    was 25 to 50 %, with which step 4 and wave 1 go on.
  - **To look over**: the lead's 75 rulings (design 13.1) and the design's decisions D416 to D452,
    above all those that amend a ruling (D420, D424, D425, D434) and those the review made (D447 to
    D452; design 13.5). None of them is the owner's own, so the owner can overturn any. (D453, on
    winget, is the owner's answer of 2026-11-20.)
  - **The three live tokens expire before 8a's gate can run**: GitHub's on 2026-11-29, Azure
    DevOps' and GitLab's on 2026-12-04, and a gate runs the live command twice (D335). Before 8a's
    gate the owner makes each again, with an expiry that also covers 8b's gate, as phase 7a's
    design 14.6 and phase 7b's design 15.8 say, through the script that never shows a token.
  - **The icon** (D407, D447): the owner chose A, Lens, on 2026-11-20. Round 2, Lens worked out
    for the small sizes and for macOS, is with the owner (`docs/brand/icon/round-2/candidates.html`,
    or https://claude.ai/artifact/Xez1bXLnA2p4XNsQ7wtkcb): yes to the set or what to change; the
    small sizes outlined (recommended) or solid; and, only if wanted, the lighter blue.
  - **The desktop, told first each time** (design 13.3): the lead's try of the winget terminal
    window before §5's merge; two earlier real-window checks alone at §6's merge (about 2
    minutes); §9's spike (a minute of keys and clicks) and a short listen with Narrator at §9's
    merge (about 5 minutes); the performance series after wave 3 (12 to 18 minutes a run, rested);
    §12's build (each of its 13 methods alone, about 15 minutes in all); D246's two runs and the
    pass at the gate.
  - **By hand, at the gate**: one walk-through with Windows Narrator, with the lead's 21 steps, the
    first-run screen among them (D411, D450); phase 6's System step (switch Windows' app mode
    while the app runs with System chosen, D224); a second start of the app from the lead's
    isolating start file while another window is in front (design 4.6).
  - **GitHub Pages** (D408, D451): the owner allowed it. Its source is set to "GitHub Actions" at
    "Phase 8a accepted", before `master` is pushed, by the owner or by the lead with `gh` at the
    owner's word; if the `github-pages` environment then admits more than `master`, the owner's
    leave is asked to set its branch rule.
  - **Leave for a reverse image search** for the icon's look-alike check (D412 covers only the
    search for the name): the mark is chosen, so the lead asks with round 2. It would send the
    unpublished drawing to an image search service; without it the lead compares by eye against
    Git's logo and the well-known clients' icons.
  - **Left to 8a's design, now ruled**: the points the readers left to the design (one instance
    per data folder, the git program setting, the netrc note, the OpenSSH line, the guide's tested
    sizes and its generator, risk 44 left alone, no writes to a user's repository for speed, the
    fixed set of keys) are the lead's rulings 1 to 23 and the decisions D416 to D446, which the
    owner has above to look over.
- **Phase 8b, to ask before its design**: plan.md's "Phase 8b" lists the questions, each with the
  research's recommendation: the installers per platform, the macOS token store (risk 34), the
  platforms and oldest systems, the version scheme, what proves "receive an update" and when the
  beta becomes 1.0, from which commit a public beta is built, the shape of the regression against
  the installed build, test packages in the owner's own Windows profile, what an uninstall removes,
  the report channels, and when a Mac is at hand for the 78 steps of the macOS checklists. With
  them the Windows route (D402), a month before the first stable release if it is to be Azure
  Artifact Signing, and the publisher name it shows.
- **Phase 7b's acceptance**: given on 2026-11-08; merged into `master` (`3dc7f79`); its gate ran on
  `cb9af6d`. Still open from the phase, below: risk 39's sign-in dialog in a bound tab, risk 40's torn
  banner and risk 43's git that loses a URL's user. The real-window pass opened the default browser once, on the fake of GitHub's page
  (phase 7a's part, P15, D321): close that tab before the next run of the pass; no 7b step opens
  it. The live command closed and deleted what it made in the three sandboxes. Before a later live run: GitHub's token expires on 2026-11-29, Azure
  DevOps' and GitLab's on 2026-12-04 (below, "The Azure DevOps sandbox's organization").
- **For phase 8, before its design** (the 7b handoff's "For the next phase", "Do first", step 2;
  each answer becomes a decision, D399 on, carried into plan.md): the code-signing certificate and
  the Apple Developer membership (risk 6); the installers per platform and where the installers,
  the updates and the beta are published (a release on GitHub is the owner's to allow); whether to
  bundle git on Windows and the git versions the release supports (risks 15, 31, 35); the macOS
  token store with the signing (risk 34); the icons, the branding and the user guide's form; the
  placeholder contrast's move into the accessibility pass (phase 6's question, below); and whether
  the phase is split, as D241 split phase 7. With them, the share of the week's allowance (D241).
  Answered on 2026-11-09: 61 % of the week's allowance used (it resets on Thursday 2026-11-12 at
  2:00), and the phase is to be split, 8a the in-app work (the first-run Git check, the
  accessibility and performance passes, the small fixes, icons, notices and the user guide) and 8b
  the packaging (installers, auto-update, signing, notarisation, the beta and the full regression
  against the installed build). The split's decision (the next free number, amending D241) is still to be written,
  with the phases' names and branches and CLAUDE.md's counting of phases in the same commit. **On
  hold at the owner's word**: the Windows code signing and the Apple Developer membership, until
  the owner has talked through some legal and professional points with the lead. **Lifted on
  2026-11-18**: the owner has no such point left (the licence, D397, and the About screen, D398,
  came of them), so the two signing questions are asked with the others. Answered the same day:
  under 50 % of the week's allowance used at 22:00 (it resets on Thursday 2026-11-19 at 2:00), and
  the session's model (Fable 5.2 leads phase 8; CLAUDE.md's Models rule). The other
  questions above have not been asked yet. Found for the two signing questions on 2026-11-09, to
  be checked again before any choice: Azure Artifact Signing bills from the account's creation and
  its price could not be read; a Microsoft Q&A answer of March 2026, marked AI-generated, limits
  new individual sign-ups to the US and Canada; SignPath Foundation signs open source for free
  after approval and is itself the publisher shown; the Apple Developer Program costs $99 a year.
  **Answered on 2026-11-18 and 19, in five sittings, after the research (Notes, "Phase 8's
  start"); each answer is a decision**: the split into 8a, "Release readiness in the app", and 8b,
  "Packaging and public beta" (D399); the installers, the updates and the beta on GitHub Releases,
  each release approved by the owner (D400); the update check asked at the first start (D401);
  Windows ships its beta unsigned and the route is chosen before the first stable release, after
  the owner first chose the Microsoft Store alone and reconsidered (D402); the owner already holds
  a paid individual Apple Developer Program membership, so macOS is signed and notarised (D403); a
  plain-language note in the README beside the unchanged licence, in the owner's words "I developed
  this to help the community and use it on your own responsibility" (D404); no git bundled, and a
  first-run screen that offers to install it (D405); the git floor 2.34, tested by a new CI job
  (D406); the icon drafted by the lead and chosen by the owner (D407); the guide as a site on GitHub
  Pages (D408); the About screen as a settings section, a palette command and the macOS menu's item
  (D409); the three contrast fixes in one round, the placeholder's among them (D410); accessible
  names, the graph's row and one Narrator walk-through, no high-contrast palette (D411); the search
  for the name allowed, and a "Name" note in the README (D412); and 8a's start through the reset
  (D415). Not asked yet: 8b's own questions (above).
- **The licence (D6), reopened by the owner on 2026-11-18. Answered the same day (D397): MIT
  stays, with the owner's name.** `LICENSE` reads "Copyright (c) 2026 Rahim Khajei" and
  `README.md`'s Licence section names the owner as the app's creator; `Directory.Build.props`'
  `Authors` and `Copyright` follow in phase 8's first code commit (plan.md's phase 8). Answered the
  same day too (D398): the app gains an About screen that names the owner as its creator, built
  in phase 8a (plan.md's "Phase 8a", R6, D409). Asked since: the README says that a fork takes
  another name (D412, 2026-11-19), and the publisher name on signed installers waits for the
  Windows route (D402), while macOS shows the owner's legal name (D403). The owner's aim:
  no direct income from the product; wide free use; the owner's name kept visible, for reputation.
  What the lead found and told the owner that day:
  - The owner alone decides: all 1,809 commits carry the owner's identity, and GitHub showed no
    fork, star, issue or outside pull request. A change binds new versions only; what has been
    public under MIT since 2026-10-10 stays under MIT for whoever has a copy.
  - Before the answer the owner's name was nowhere in the product: `LICENSE` read "VisualCommit
    contributors" (settled 2026-10-06), and the app has no About screen.
  - The options laid out: MIT with the owner's name in the copyright line; Apache-2.0 with a
    `NOTICE` file and its trademark clause; GPL-3.0 with an attribution term under its section
    7(b), which keeps forks open and the credit in their legal notices. Every dependency (MIT,
    ISC, OFL, Apache-2.0) fits all three. A non-commercial or source-available licence is not open
    source and was advised against. The lead leans to GPL-3.0 if a closed, renamed copy would
    bother the owner, and to MIT with the name otherwise.
  - Two cautions: 1,804 commits carry an AI co-author trailer, and copyright in AI-written code is
    unsettled (in the US only human authorship is protected), which weakens the enforcement of any
    licence term, restrictive ones most; and the owner's employment terms on outside work are the
    owner's to read.
  - Beside the licence, for the name: an About screen, the README, the publisher name on signed
    installers (which ties this to the signing answers above), and a short trademark note that a
    fork takes another name.
  `requirements.md`'s R6 ("MIT licence file and third-party notices", and since D398 an About
  screen that names the app's creator) and `THIRD-PARTY-NOTICES.md` stand as they are; 8a
  completes the notices.
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
  scopes and variables (phase 7b's status step 16). The design names them in 15.8 (D364): the Azure DevOps project
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
- **Decisions made during phases 7a and 7b without the owner**, which the owner may want to look at: the
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
  readable is checked against its read; the owner's approval), and D396 the cold read's at the close
  (CLAUDE.md's step 1 never checks out or pushes a local `wip/...` or `backup/...` branch). D397 is
  the owner's choice of 2026-11-18 (MIT stays, the copyright line names the owner) and D398 the
  owner's answer of the same day (an About screen that names the owner). D399 to D415 are the
  owner's answers of 2026-11-18 and 19 at phase 8's start; in D399 the two phases' names and
  branches are the lead's. The next decision number is D416.
- **Answered 2026-11-19: left as a recorded risk (D413).** **A sign-in dialog in a bound tab** (plan.md's risk 39, design 19 item 8; no answer needed
  for the gate): when the session refuses a bound account through the API, or it signs out,
  just after an operation started, git's "Sign in to github.com" dialog opens instead of D273's
  refusal without a dialog, as D340 records for a sign-out. Should the broker also refuse such a
  prompt without a dialog, from what the session knows (amending D273 and D340)?
- **Answered 2026-11-19: left as a recorded risk (D413).** **The banner's brief torn state** (plan.md's risk 40, design 21 item 4): during a merge started
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
- **Answered 2026-11-19: left as a recorded risk (D413).** **A git that loses the URL's user** (the Notes of phase 7b's `status.md`, "D388's test on macOS"; D389, plan risk 43, design
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
- **The start reading has grown** (the cold reads at phase 7a's and 7b's close): CLAUDE.md's step 4
  reads `architecture.md` (about 836 KB at 7b's close, 622 KB at 7a's, 429 KB at 7a's start) and
  `decisions.md` (about 353 KB, 262 KB at 7a's close) whole. The review before phase 7 set aside P6
  (start reading by index, `architecture.md` split by area) with "reconsider it if the start reading
  keeps growing" (review-before-phase-7.md, section on the proposals not taken); until the owner
  decides, the 7b handoff's "Do first", step 1, names the sections phase 8 needs (about 240 KB).
- **The weekly allowance** (D241): a session cannot read `/usage` itself, so phase 7a's and 7b's
  checkpoints record "share not read". For 7b the owner gave the share before the start (48 % used on
  2026-10-31) and let the phase use the rest of that week (D344), then gave it about all of the week
  from 2026-11-05 (the owner's answer of 2026-11-05). From phase 8, D241's rule applies unless the
  owner says otherwise: the session asks for the share at the phase's start and before each stage,
  and records it (plan.md's "Workflow"). At phase 8's start the owner gave it: under 50 % used at
  22:00 on 2026-11-18, four hours before the reset; and 8a's start runs through that reset without
  the check until its design is reviewed (D415).
- **The live command's sandbox for GitHub (D250)**: the private `RahimPasha/visualcommit-sandbox`
  and a fine-grained token for it alone, expiring 2026-11-29, in the two Windows user variables
  `VISUALCOMMIT_LIVE_GITHUB_TOKEN` and `VISUALCOMMIT_LIVE_GITHUB_REPO` (set on 2026-10-30; the
  live command passed 80 of 80 at phase 7a's status step 16 and 112 of 112 twice at 7a's gate, and
  GitHub's class passed in both of 7b's gate runs). A gate after 2026-11-29 needs a new token, made
  as phase 7a's design 14.6 says.
- **D240's connectors switch** (Notes): `ENABLE_CLAUDEAI_MCP_SERVERS=false` in
  `.claude/settings.local.json` did not keep the claude.ai connectors out of this session. Turning
  them off another way (the variable set in Windows' user environment before VS Code starts, or
  the connectors switched off on claude.ai) is the owner's.
- **Phase 6's acceptance**: given on 2026-10-24; merged into `master` (`3f34c87`); its gate ran on
  `b561b7c`. Still open, asked at acceptance and not yet answered (the handoff's "Waiting on the
  owner"; each answer becomes a decision, D399 on, since D239 to D248 went to the review before
  phase 7, D249 to D264 to phase 7a's start, D265 to D338 to its design, D339 to D343 to its build
  and gate, D344 to D356 to 7b's start and its research, D357 to D378 to its design, D379 to D395 to
  its build, review and gate, D396 to its close, and D397 and D398 to the owner's name in the licence and the app): design 17.5's reading of "ten repos can be checked and
  updated from one screen" (one dashboard of the ten, one Fetch all and one Pull all deciding every
  row); the report's by-hand System step (switch Windows' app mode while the app runs with System
  chosen, D224); and the move of the text boxes' placeholder contrast (Fluent's watermark opacity,
  about 2.5:1 dark and 2.1:1 light) to phase 8's accessibility pass, which the lead made at the
  gate's inspection rather than change nearly every picture. **Closed on 2026-11-18 and 19**: the
  owner meant by the sentence that several repositories can be open at once, which the tabs give,
  so design 17.5's reading goes beyond it and stands (D414); the placeholder's move is confirmed
  and 8a fixes it with the other contrast gaps (D410); the System step is done by hand with 8a's
  checks (above, "Phase 8a, from the owner").
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
  each answer becomes a decision, D399 on): its questions 1 (how the plan's limits count tokens),
  10 (CI split into shards, S5, which reopens D134 and D232) and 12 (the machine: the page file,
  the space on C:, and how long to keep transcripts, D8; the reboot was made on 2026-10-28); and
  the proposals that no decision names: D5, T10/P7, R4, R9, R1/R6, R10, S6, S7, S10, R7, D3, D4,
  N4, S11 and T8/P12's trial of a lower effort level. The review's section 5, step 11 (one default
  run re-measured on a quiet laptop) was made on 2026-10-28 and failed on load, and so did S10's
  run with at most 5 projects at once (Notes); phase 7a's baseline, after the reboot, failed in G23
  alone (phase 7a's status step 2), and the owner chose to split the check (D264).
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
- When you have a Mac at hand: the seven-step checklist in the
  [phase 7b test report](test-reports/phase-7b.md)'s "macOS checklist for the owner", the eight-step
  one at the end of the [phase 7a test report](test-reports/phase-7a.md), the seven-step one at the end of the
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

Settled on 2026-10-06: the copyright line in `LICENSE` stays "VisualCommit contributors" (until
2026-11-18, when D397 put the owner's name in it), and the repository was made public, which lifts
the limit on CI minutes.

Settled on 2026-10-09 (D41): the owner deleted the GitHub repository and a session created it
again, empty and public, and pushed `master` and `phase/0-foundation` unchanged. The commits from
before the rewrite of the history (D40), which GitHub had kept serving by their IDs, now answer
"not found"; every commit on GitHub carries the owner's personal address. The session's
permission system refuses to delete a repository, so a deletion is always the owner's to do.
