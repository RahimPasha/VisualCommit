# Phase 8a handoff — Release readiness in the app

Written at the end of phase 8a for the session that runs phase 8b. That session has not seen any of
this phase's conversation. How the app is built is in [architecture.md](../architecture.md): its
"Git's detection and the first-run screen", "The user guide and the app's links", "Start-up, memory
and reload measures" and "The app's icon" sections are 8a's, and "Solution layout", "As built
(after phase 8a)", "Start-up and composition", "Git layer", "Commit graph", "Working changes and
writes", "Prompts and credentials", "Dialogs", "The command palette", "Keyboard shortcuts", "The
settings screen", "Shell and views", "Theme", "Conventions", "Tests" and "CI" are where it changed
the earlier ones most. Why is in [decisions.md](../decisions.md): D397 and D398 are the owner's
answers on the licence and the About screen (2026-11-18), D399 to D415 the owner's answers before
the start (the split, 8b's first answers, 8a's scope), D416 to D446 the design's, D447 to D452 its
review's, D453 the owner's answer on winget, D454 to D459 the build's, D460 the owner's choice of
when Narrator is heard, D461 to D468 the review before the gate's and step 19's, D469 the owner's
answer on the dimmed texts, D470 the held fetch's, and D471 to D473 the gate's. The design is
[phase-8a-design.md](../phase-8a-design.md): section 0 the scope, the naming, the packages and waves
with the lagged list, the parts, the cost in time, the earlier pictures and what it asks of the
owner; 1 the shared contracts, with 1.17's as-built notes wave by wave; 2 to 12 and 14 one brief per
work package with its "As built" notes; 13 the lead's rulings in short form (13.1), what waits on
the owner (13.3), the open points (13.4) and what the design's review changed (13.5) ("design N.N"
below). "Ruling n" is one of the lead's 116 rulings in 13.1: 1 to 75 of 2026-11-19 (their full text
in `D:/Projects/Personal/phase8-work/design/rulings.md`, outside the repo), 76 of 2026-11-22 (D457),
and 77 to 116, written into 13.1 as they were made, to 2026-11-27. The report,
[test-reports/phase-8a.md](../test-reports/phase-8a.md), holds the 45 checks in eight parts (L1 to
L6, I1 to I3, F1 to F14, K1 to K8, B1 and B2, A1 to A6, U1 to U3, G1 to G3), the checks by hand,
"The performance series" and the open points 1 to 113 with the lead's readings.

## Result

Everything in plan.md's phase 8a "Delivers" list was built. The report's "What proves each Delivers
item" is the longer form of this table.

| Delivered (plan.md, phase 8a) | Proven by |
|---|---|
| First-run experience (R4, D405, D423): git missing, too old or not working, each state with its own text; "Check again" without a restart; "Choose git.exe…" and the settings screen's "Git program" row; on Windows a winget install after a confirm, in a visible terminal, for a missing and a too-old git (D449, D453); the Command Line Tools or Homebrew on macOS, the distribution's package on Linux; the missing askpass helper | F1 to F13 (the real window repeats F1, F2, F4, F5, F6, F6b and F13); G2's five links; `GitSetupShellTests` and `FirstRunFocusTests` (8a's project); Git.Tests' `GitLocatorTests`, `OsReleaseTests`, `GitStandInTests`; App.Tests' `GitAccessTests`, `CheckGitAgainTests`, `GitSetupTextsTests`, `GitSetupViewModelTests`, `GitProgramViewModelTests`, `FileLauncherTests`; the screen's path drawn as one path (D472): App.Tests' `PathTextBlockTests` and `GitSetupViewModelTests`' test of the view at the screen's width, and the real-window `f5a` and `f6a` of the counted round that stands (rulings 115, 116); the Narrator walk-through's steps 20 to 21a |
| From phase 6: a broken `~/.gitconfig` shows git's own line | F6 (the stand-in) and F6b (real git, in CI's floor job on 2.34.1 too), both repeated in the real window; `GitLocatorTests` |
| The git floor, 2.34.1 (D406, D420): `GitVersion.Minimum`, the "too old" texts, `requirements.md`, the README and CLAUDE.md's prerequisites; D277's branch for a git below 2.31 removed | CI's job "ubuntu 22.04 container, git 2.34 (the floor)", green first in run 44727459663 (7,989 tests; 20 failed before §3), required since `bba59c7`, green in the Gate commit's run 46007767846 (8,933 tests); `GitWordsTests`, `GitVersionTests`, `BranchOperationTests`, `RepositoryOperationsBranchesTests` |
| From phase 3: OpenSSH older than 8.4 (risk 15; ruling 7 as ruling 100 amends it; D424) | No check: one Info line per session, its words per system (`AskPassBrokerHelperTests`); the guide's `troubleshooting/sign-in-and-ssh.md` |
| From phase 7a: the netrc note on git 2.34 to 2.45 (risk 35; D424) | F14; `AccountsNetrcNoteTests` |
| Keyboard-accessibility pass (R7, D411): a hunk and chosen lines from the diff, a blame run, "Cancel {title}" in the palette (D417, D452), the stage rows' buttons on keyboard focus (D425), Tab out of every read-only text view (D426), the splitters by keys (D427), Enter in a dialog's list (D454); from phase 7a the focus after "Choose" and the loading diff's late focus; from 7b's review the commit boxes while "Committing…" runs (D392) | K1 to K8 (the real window repeats K1 and K2); `KeyboardPassTests` and its partial files in 8a's project, the window's rule for a press by the keys among them (D462 (4)); `DiffTextViewTests`, `BlameViewTests`, `WorkingChangesTests`, `StatusBarTests`, `RegionKeysTests`; the inventory of main actions in the report (design 6.3.9) |
| An accessible name on every control (from phase 6, the icon buttons); the graph's selected row exposed to UI Automation (D49 as D435 amends it, D450); one Narrator walk-through by hand | U1 to U3 (the real window repeats U1 and U3 in launch 13); `AccessibleNameSweepTests` (14 states, 8a's project), `CommitGraphControlAutomationTests`, `GraphRowSpeechTests`, `StatusBarTests` (D463 (1)), `OutcomeAtFocusTests` (D463 (3)); the Narrator walk-through, by the owner at the gate (D460) |
| The contrast fixes in one round (D410, D418, D419): the placeholder, the dark danger colour with the dark label, the dark accent on hovered and selected rows, which covers phase 6's dark texts below 4.5:1 and its placeholder; three texts the app dims on purpose stay under 4.5:1 by the owner's answer (D469) | L1 to L6 (the real window repeats L1, L2, L4 a, L5 dark and L6 a to c); `ThemeTokensTests`, one table with no list of gaps, and `ThemeTokensTests.DimmedTexts.cs`, which pins D469's figures and guards the dimmed colour; wave 1's masked comparison with phase 7b's references and the scan's eight lists in `docs/test-reports/phase-8a-contrast/` |
| Performance pass (D436 to D438, D457) | No part (design 10.5): the report's "The performance series" (three "before" series, the bounds in `Phase8aPerformance`, four triggers fired, two rows to the Backlog and one change); ruling 76's restore order, proven by its alternated series, taken again by D461 and holding (D458, D467); `PerformanceSeriesTests`, `StartupTimelineTests`, `RestoreOrderTests`, `RepositoryFirstPageTests`, `RestoreOrderShellTests`; Q1 in phase 1's real-window pass |
| Third-party notices, complete for what the app ships and reachable from the app (R6; D431, D433, D434) | A4, A5; `ThirdPartyNoticesTests` (held to the app's `VisualCommit.deps.json`; macOS's and Linux's natives in CI's jobs); the files `THIRD-PARTY-NOTICES.md` and `notices/` |
| The About screen (R6, D398, D409): the settings screen's sixth section, the palette's "About VisualCommit…" and the macOS application menu's item | A1 to A6, G3 (the real window repeats A1 and A4); `AboutSettingsViewModelTests`, `AboutSectionTests`, `AboutEntryTests` (the macOS menu's item, headless), `LinkFocusFrameTests`, `AppVersionInfoTests`; About on a real Mac is 8b's checklist |
| Icons and branding (R5, D407, D428 to D430, D447): the masters, the tool, the `.ico`, the `.icns` and the PNG files, the window's and the executable's icon; the README's "Name" note names the icon (D412) | B1, B2 (the real window: the executable's icon, the window's two icons, the title bar dark and light, the taskbar button); `IconFilesTests` (item 8, `iconutil` both ways, in CI's macOS job), `WindowIconTests`; the owner's choice in `docs/brand/icon/round-1/` and `round-2/`; the README (the file); the look-alike comparison by eye, a check by hand |
| User guide (D408, D439 to D441, D451): Markdown pages in `docs/guide/`, a site on GitHub Pages built by `.github/workflows/guide.yml`, pictures in both themes from `tests/VisualCommit.GuideShots`, the app's links (F1, the palette's row, the first-run links, About's link) | G1 to G3; the 33 pages and `SUMMARY.md` (the files); the guide workflow's runs, 45917903593 on `53e07c4`, 45953481558 on `6035e71` and 45965143205 on `e632e72`; `AppLinksTests`, `GuideShortcutsPageTests`, `GuideCatalogueTests`, `LeakGuardTests`; `scripts/check-guide-links.py` with its self-test; the site's check by hand, made in the first round on the `guide-site` artifact of 45917903593 and, after D471's fix, on 45953481558's (ruling 114), whose pictures and pages the site built from the gate's app code holds unchanged (ruling 116); the live site after "Phase 8a accepted" |
| From the repo readers: one instance per data folder (D421, D422, D448, D464); the macOS data folder | I1 (real window only), I2 (and the real window), I3; `SingleInstanceGuardTests`, `InstanceNoticeViewModelTests`, `MainWindowBringForwardTests`; `AppPathsTests` in CI's macOS job; a second start behind another window, by hand |
| From D397 and the 7b handoff: `Directory.Build.props`' `Authors` and `Copyright` read "Rahim Khajei"; `VisualCommit.slnx`'s comment names the live command's three services | `e04762b` (the files) |

The plan's "Done when": a user whose git is missing, too old or broken is led to a working git by
the app's own guidance (part F, its real-window launches and G2's links); every main action is
reached by keyboard (part K and the report's inventory) and every control has an accessible name
(U2's sweep, U3); no text pair of either theme is left below 4.5:1 in `ThemeTokensTests` but D469's
three, pinned there; the About screen, the icon, the complete notices and the user guide exist; the
visual gate's outcome is under State of the repo.

Not delivered, and where each item went:

- **Phase 8b** (D399; plan.md's "Phase 8b"): the packaging, signing, updates, the beta and the
  regression against the installed build; and from 8a: the runtime's and the installers' notices
  (ruling 15); macOS 26's layered `.icon` (ruling 17), with `Info.plist`, Linux's `.desktop` file
  and hicolor tree and the installers' images (design 7.1); an updater's start-up hook before the
  single-instance guard and a second guard key by install folder (ruling 67), and D401's question
  at the first start asked only while the first-run screen is not shown (design 5.1); the install
  page's installers and its "What an uninstall leaves", and `troubleshooting/data-and-logs.md`'s
  "Reporting a problem", which waits for the channels of bug and security reports (design 11.1,
  11.3); the time before the window opens and the performance measures on the published, installed
  build, and whether a build compiled ahead of time (ReadyToRun) shortens the start (design 10.1,
  14.1); and the Mac checklist's new lines: a second start while the app runs, About in the
  application menu (`About VisualCommit`, no `About Avalonia`), F1 with Fn unless the function keys
  are standard (ruling 85), the Command Line Tools' install (`xcode-select -p`, the shim,
  `--install`) and the `/usr/bin/git` rule, the guard's socket under a real `TMPDIR` and the app
  brought forward, the Dock icon and `icon-macos.svg` on macOS 26's plate, and X11's window icon
  (design 0.5, 4.9, 5.9, 7.9 item 6, 8.9 item 2).
- **Backlog** (plan.md's rows "Left out of phase 8a by its performance pass" and "Kept as they are
  in phase 8a", and the rows the rulings name for the close): an incremental reload of the graph,
  S8's batch of Pull's settings reads and background tabs restored in summary mode (D457); a
  de-emphasis that keeps 4.5:1 (D469); "Check again" reopening the failed tabs through the restore
  order (ruling 90); the focus limits of rulings 101 to 103 and 105 and the frames' of ruling 101
  (Known issues); the overlay scroll bar over the graph's widest dates and a conflict's last line
  (S19-07, S19-10); Recent repositories opening a second tab on a worktree another tab shows
  (S19-08, D209); Open not finding a tab through a link's spelling of its folder (ruling 104); git
  found behind Git for Windows' launcher (S19-22); the row list's limits (S19-26, S19-28); the hunk
  bars' repeated names (design 13.4 item 15); an item status on the left panel's rows (ruling 36);
  keyboard selection of commits not next to each other (ruling 50); what D473 leaves in the
  real-window harness (the wait conditions that reach `Find` or a LINQ `Single`, the loops that act
  and read between their actions, the reads after a time-out in some catch blocks, FlaUI's reads on
  an element that has gone); for phase 8b, the paths D472 leaves as they are (a path inside running
  text in a plain text block, a tooltip whose text is a path alone, a UNC path written with forward
  slashes; ruling 115) and a test that holds the guide's image widths (D471, ruling 114); and, from
  the owner's answers before the start, a bundled git, a palette made for high contrast and a
  registered mark (D405, D411, D412).
- **Left recorded, by the owner**: risks 39, 40 and 43 (D413); the click that carries the focus
  frame is the owner's to look at (Waiting on the owner).

## State of the repo

Branch `phase/8a-release-readiness`, started on 2026-11-19 from `master` at `66063f3` (phase 7b
merged) and built on the Windows machine to 2026-11-27. At `e4b323c`, the commit of the first
counted round, it held 576 commits on top of `66063f3`, 43 of them merges
(`git log --oneline --merges 66063f3..e4b323c`; one of them, `290ccc3`, brought the phase into
`wip/p8a-uia` before that package's merge). The last commit before that round that changes anything
but docs is `2a52897` (comments in the test support code); `53e07c4`, `5cfc58a` and `e4b323c` change
docs only. That round's inspection found one fault of the app (below). `ee3cdc4` merges its fix,
and `e632e72` (docs) records ruling 115 and D472. The counted round that stands ran its default run
on `0f9099a`, which changes only docs after `e632e72`, so its code is `ee3cdc4`'s, and its
real-window pass on `750eb33`, which merges the harness's fix (D473) and changes nothing but
`tests/VisualCommit.RealWindowTests` and docs after `0f9099a`, as D246 allows (ruling 116). **The
Gate commit is `750eb33`.** At it the branch holds 591 commits on `66063f3`, 45 of them merges.
After it only docs change: `d6dbe33` (status.md), `ca0460c` (ruling 116 and D473), `6120f86` (the
inspection's outcome in ruling 116 and status.md, and the list of pictures committed) and the
close's commits of docs.
[LEAD: the merge into `master`, once the phase is accepted, with its merge commit.]

Before the merges, in the main checkout: `e04762b`, the phase's first code commit (CI's floor job,
D397's `Authors` and `Copyright`, the `.slnx` comment), and WP0 `fc8a063`. The merges: wave 1
`3b9d159` (contrast), `a63390c` (floor), `b70abdb` (one instance); wave 2 `6fe69bd` (icon),
`e463392` (keyboard), `53fc353` (first run), with the lead's commits at the merge (`578d07c`,
`25dd5c8`, `24b9d16`, `9b63f26`, `fc123ec`); wave 3 `86ecb3a` (performance), `f014b2b` (About and
the notices), `769f459` (screen readers), `eba3717` (the fetch hold's script), `85e2884` (About's
fixes), and at its close `fcf8c1e` (the link frame), `70a8fc0` and `809b77f` (CI's two fixes) and
`bad98b1` (the row list); wave 4 `aedb12f` (the restore order; its bounds `23bea54`) and `57ad86e`
(the guide); step 19 the review's fixes `c0bedc6`, `a60056e`, `67ff55d`, `9e225d1`, `ce144f4` and
`876c197`, its second round `950ca7d`, `39c632f` and `db7fbce`, its third `c9c3279`, its fourth
`161a801`, the real-window part `b24b44a` with the harness's look by handle `14789f7` and its
hardening `640f728`, the temp cleaner's `c1bfaf7` and `67e1842` (D468), the dimmed texts' `fab4073`
and `15beabd` (D469), and the held fetch's `4370039` and `72993a7` (D470), with `2a52897`; at the
gate the first-run path's fix `ee3cdc4` (D472) and the real-window wait's `750eb33` (D473). The
docs' merges are `b593097`, `c914fb7`, `05e3b3e`, `ba4570e`, `25dd908` and `2b2247c`. The Progress
checklist and Notes of status.md on the phase's branch give the rest
(`git show origin/phase/8a-release-readiness:docs/status.md`, the merge's second parent once 8a is
merged; the acceptance resets `master`'s).

- **D246's two runs** of `Phase8aRealWindowPass` (13 launches, design 12), the owner present, on
  `e4b323c`, 2026-11-26: 13 of 13 at 18:17 in 5 min 41 s and 13 of 13 at 18:31 in 5 min 5 s (396 and
  314 s with the lock). Made once; they stand for the gate.
- **The live command before the pictures** (D335; status step 20): 2026-11-26, 11:00 to 11:04, on
  `fab4073`, the owner told: the offline classes 582 of 582 (1 min 36 s), then the whole project 596
  of 596 in 3 min 31 s (218 s with the lock). The three `run.txt` hold no token, organization or
  namespace, and their differences from the fakes are those phase 7b accepted, so nothing was
  corrected (ruling 112). Copies in `D:/Projects/Personal/phase8-work/gate/live-before/`. Made once.
  `tests/VisualCommit.LiveHostingTests` has no commit in phase 8a.
- **The first counted round**, on `e4b323c`, 2026-11-26. The whole default run's first attempt
  (18:36 to 20:29) failed with no failed assertion: the hang watchdog stopped phase 3's walk-through
  in `Phase3ChecksD.Check_19_push_rejected` and `VisualCommit.App.Tests` in
  `RepositoryOperationsProtectTests.Merge_pick_and_revert_refused_for_a_changed_file_count_it_and_for_a_staged_one_say_so`
  (7,254 tests ran, 7,250 passed, 4 skipped), while other work of the lead's ran beside it (Things
  that cost time). Its second attempt (20:32 to 22:22), with nothing else running: 8,927 tests,
  8,920 passed, 7 skipped, none failed, in 1 h 48 min 52 s (6,569 s with the lock), no watchdog
  stop; each project took of its own: `VisualCommit.App.Tests` 53 min 2 s, `VisualCommit.Git.Tests`
  46 min 46 s, phase 3's walk-through 43 min 52 s, phase 5's undo walk-through 43 min 36 s, phase
  8a's 37 min 6 s, `VisualCommit.Hosting.Tests` 34 min 33 s, phase 6's 23 min 39 s, phase 4's 22 min
  20 s, phase 5's 16 min 25 s, phase 7b's 13 min 27 s, phase 7a's 9 min 56 s, phases 0 to 2's 9 min
  1 s and part L's 8 min 40 s. The real-window pass after a rest of 14 minutes (22:36 to 23:17): 124
  tests, 119 passed and the 5 facts of the opt-in performance series skipped, in 41 min 34 s (2,504
  s with its build and the lock); display scaling 200 %, work area 1500×952; Q1 1,912 ms (the limit
  2,000), 459 frames with a 95th percentile of 1 ms and the longest 13.4 ms; the 220 captures
  compared with their scripted twins within 2.36 % of their pixels (4.08 % compared in place only),
  phase 8a's 22 within 0.14 to 1.76 % (0.55 to 3.66 %). The list of pictures, written at once: 1,696
  files, 1,239 scripted (phases 0 to 8a: 14, 43, 66, 265, 122, 264, 198, 85, 93 and 89) and 457
  real-window ones (8, 30, 26, 90, 56, 96, 51, 18, 26 and 56). The live command (23:18 to 23:23):
  582 of 582, then 596 of 596 in 3 min (192 s with the lock); the three `run.txt` hold no token,
  organization or namespace and differ from the run before the pictures only in what changes from
  run to run. The round's copy, logs, batches and verdicts are in
  `D:/Projects/Personal/phase8-work/gate/round-e4b323c/`.
- **The first round's inspection** (D247): 28 Opus inspectors in three groups read the 1,696
  pictures and 45 of CI's; 1,694 passed. The other two are one finding, which five of six second
  looks judged a fault of the app: in the real-window captures `f5a-too-old` and `f6a-config-fault`
  the first-run screen's path (`GitSetupPath`, a `PathTextBlock` whose text is a path alone) breaks
  at the space of `Visual Git` and after the hyphen of `net10.0-windows`, because the control ends a
  path at white space, D227's rule for a path inside running text, and the stand-in's path in the
  main checkout holds a space (`…\Visual Git\…`). The fix, `PathTextBlock.IsOnePath` set on
  `GitSetupPath` (D472, ruling 115), was built on `wip/p8a-path-wrap` (`fe3f35d`, `f337fbf`,
  `6fa567f`) in `D:/Projects/Personal/phase8-work/gate/fix/p8a-path-wrap` and merged at `ee3cdc4`;
  on the merged tree `PathTextBlockTests`, `GitSetupViewModelTests` and
  `AddRepositoriesDialogViewModelTests` pass, 64 of 64. So the round on `e4b323c` does not stand as
  the gate's.
- **The counted round that stands** (ruling 116), 2026-11-27, its two passes on two commits, as
  D246 and plan.md's Visual test gate, step 6, allow. The default run on `0f9099a` (02:36 to 04:33),
  with nothing else running on the laptop (the closing docs' drafts set aside outside the repo, so
  that the tree was the commit; two leftover `git fsmonitor--daemon` processes stopped): 8,936
  tests, 8,929 passed, 7 skipped (the three real keychains' tests, `WindowsCredentialStoreTests`'
  off-Windows test, `IconFilesTests`' `iconutil` fact and `SingleInstanceGuardTests`' two of Unix
  file modes), none failed, in 1 h 53 min 8 s (7,004 s with the build and the lock), no watchdog
  stop; each project's own time, from its start to its end with the others beside it:
  `VisualCommit.App.Tests` 1 h 35 min 25 s, `VisualCommit.Git.Tests` 1 h 10 min 35 s,
  `VisualCommit.Hosting.Tests` 1 h 9 min 54 s, phase 5's undo walk-through 52 min 29 s, phase 8a's
  33 min 53 s, phase 3's 24 min 1 s, phase 5's 20 min 45 s, phase 7b's 16 min 24 s, phase 7a's 16
  min 23 s, phase 6's 16 min 17 s, phase 4's 13 min 9 s, phases 0 to 2's 7 min 3 s and part L's 4
  min 59 s.
- **The pass that failed in the harness**, on `0f9099a`, after a rest of nine minutes (04:42 to
  05:25): 124 tests, 118 passed, 5 skipped, 1 failed, with no fault of the app; Q1 1,938 ms. Phase
  3's `Check_22_remotes_dialog` ended on a `COMException` ("Unexpected HRESULT has been returned
  from a call to a COM component", .NET 10's words for E_FAIL) from UI Automation's `FindAll`, in
  the poll that waits for the Remotes dialog's rows after the remote's URL was changed: one look
  walked the app's automation tree while the dialog built its rows again, and `RealApp.WaitFor` let
  a failed look end the check. The fix, D473, test code in `tests/VisualCommit.RealWindowTests`
  alone, was built on `wip/p8a-uia-poll` (`664a973`, `7e6d851`, `14592aa`, `c8d6aa1`, `df263aa`) in
  `D:/Projects/Personal/phase8-work/gate/fix/p8a-uia-poll` and merged at `750eb33`; the lead ran
  `WaitForRuleTests`, 21 tests with no window, there: 21 of 21 at `df263aa`, 6 failing at `c8d6aa1`
  and at `664a973`, before their fixes. The failed pass's log is `real-window.log` in the round's
  folder (below), its pictures that folder's `visual-failed-pass/`.
- **The pass that stands**, alone, on `750eb33` (06:31 to 07:12, the owner told in the chat), with
  D246's two conditions checked first: the `git diff --stat 0f9099a 750eb33` that leaves out
  `tests/VisualCommit.RealWindowTests` and the docs prints nothing, and the 1,239 scripted pictures
  were still the files listed after the failed pass. 145 tests, the 124 of before and
  `WaitForRuleTests`' 21, 140 passed, the 5 facts of the opt-in performance series skipped, none
  failed, in 40 min 56 s (2,484 s with the lock and the build); display scaling 200 %, work area
  1500×952; Q1 1,626 ms of 2,000, 468 frames with a 95th percentile of 0.9 ms and the longest 4.8
  ms; the 220 captures compared with their scripted twins within 2.36 % of their pixels (4.09 %
  compared in place only), phase 8a's 22 within 0.14 to 1.76 % (0.55 to 3.66 %). In `f5a-too-old`
  and `f6a-config-fault` the path draws as `D:\Projects\Personal\Visual Git\tests\`,
  `VisualCommit.RealWindowTests\bin\Release\net10.0-windows\` and
  `visualcommit-git-stand-in.exe`: D472's fix in the real window. The list of pictures, written
  right after the pass: 1,696 files, 1,239 scripted and 457 real-window ones, in the same counts per
  phase as the first round's (`docs/test-reports/phase-8a-pictures.sha256`). The live command on
  `750eb33` (07:13 to 07:18): 582 of 582 in 1 min 12 s, then 596 of 596 in 3 min 4 s (195 s with the
  lock); the three `run.txt` hold no token, organization or namespace and differ from the two runs
  before only in what changes from run to run. The round's copy, logs, batches and verdicts are in
  `D:/Projects/Personal/phase8-work/gate/round-0f9099a/`.
- **CI**: the run of the Gate commit is
  [46007767846](https://github.com/RahimPasha/VisualCommit/actions/runs/46007767846), on `750eb33`:
  all six jobs green at the first attempt: `windows-latest` (git 2.56.1.windows.1) 8,936 tests,
  8,929 passed, 7 skipped, Test step 45 min 28 s of 70, job 48 min 53 s; `macos-latest` 8,933 tests,
  8,930 passed, 3 skipped, 26 min 53 s, job 28 min 7 s; `ubuntu-latest` 8,933 tests, 8,929 passed,
  4 skipped, 6 min 47 s, job 8 min 47 s; the floor's job (git 2.34.1) 8,933 with 7 skipped, 7 min
  14 s of 25, job 9 min 44 s; the two containers 8,933 with 7 skipped each, 7 min 3 s (Microsoft's
  installer, job 9 min 49 s) and 7 min 45 s (Ubuntu's packages, job 14 min 18 s) of 14; every
  keychain step passed, and every job's `git-url-user.txt` reads Kept. Its push changed nothing the
  guide's workflow watches, so it started none. Before it:
  [45965143178](https://github.com/RahimPasha/VisualCommit/actions/runs/45965143178) on `e632e72`,
  the app's fix, and
  [45917903492](https://github.com/RahimPasha/VisualCommit/actions/runs/45917903492) on `53e07c4`,
  the first round's code, each green in all six jobs at the first attempt (Windows' Test step 40 min
  34 s and 34 min 2 s, macOS' 44 min 0 s and 34 min 28 s). The guide's workflow: 45917903593 on
  `53e07c4`, 45953481558 on `6035e71` and, on the fix's push, 45965143205 on `e632e72`, all three
  green (status.md's CI section). The phase's 30 runs, each failure with its fix, are in the CI
  section of status.md on the phase's branch; their Test steps reached 45 min 28 s on Windows
  (46007767846) and 47 min 44 s on macOS (45528731262), 8 min 51 s in a container and 8 min 27 s in
  the floor's job, each under four fifths of its limit, so no limit changed in 8a (D377). The
  Windows Test step read 34 min 2 s, 40 min 34 s and 45 min 28 s in the three runs of 2026-11-26 and
  27, on the same tests but nine: the runners' spread.
- **Gate**: the guide's site was checked by hand in the first round, on the `guide-site` artifact of
  run 45917903593, built from `53e07c4`'s code (ruling 114: two faults of the pages, corrected in
  docs at `6035e71`, D471, whose run 45953481558's site the lead checked after them). The app's fix
  changes `src/`, which `guide.yml`'s `paths` name, so its push started the guide's workflow again
  (45965143205 on `e632e72`, the gate's app code), and the guide's first-run shots draw
  `GitSetupPath`. That run's site holds the same 84 picture files as 45953481558's and the same
  pages. The Gate commit's push, a change to the real-window project alone, started no guide run, so
  the lead started one by hand, as D451 says (`gh workflow run guide.yml --ref
  phase/8a-release-readiness`: 46024262689 on `6120f86`, docs alone after the Gate commit): its site
  holds the same 84 pictures and the same 35 pages again, each picture tag with its width, and the
  links' check has no finding on it, so the lead's reading is the gate's check of the guide's site
  (ruling 116). The 1920×1080 real window does not fit this screen, so the real window's checks at
  that size, screenshot D of phase 0's check 8 and A1's second capture among them, are proven
  scripted only, as the plan allows. No pass touches a real keychain (CI's steps do), and no real
  window of 8a opens a browser (D444).
- **The counted round's inspection** (D247; 2026-11-27, 07:20 to 07:46; ruling 116): Opus
  inspectors in 28 batches, CI's one of them, judged every picture again against the first round's
  copy, all of which had been read: 1,257 of the 1,696 are the same files, 353 real-window files
  differ only in the window's two bottom corners, where the desktop's wallpaper shows through and
  had changed in the night, and 86 differ elsewhere, each with a sheet of the capture, the reference
  and the changed pixels. **All 1,696 passed, and the 45 of CI's from run 46007767846**; none was in
  doubt, so no second look was called. `f5a-too-old` and `f6a-config-fault` passed with their three
  lines read character by character. The verdicts are in
  `D:/Projects/Personal/phase8-work/gate/round-0f9099a/inspect/verdicts/`.
  [LEAD: whether the whole gate passed, with anything that could not run.]
- **Checks by hand**, with the owner, from the lead's start files in
  `D:/Projects/Personal/phase8-work/gate/hand/` (outside the repo): the Narrator walk-through of 21
  steps and step 21a (D411, D450), phase 6's System step (D224), the icon beside its look-alikes by
  eye, and a second start while another window is in front (design 4.6).
  [LEAD: each check by hand's result, as the report's "Checks by hand" records it.]
- **The cold-read check** (CLAUDE.md's closing step 5), made on the finished docs.
  [LEAD: what the fresh agent hit and what it changed in the docs.]

## Environment

| | |
|---|---|
| Machine | Windows 11 Pro 10.0.26300 on an Intel Core i7-8650U laptop; screen 3000×2000 at 200 % scaling (1500×952 logical work area). The whole phase was built on this machine, the lead on Fable 5.2 (CLAUDE.md's Models rule) and Opus sub-agents in worktrees under `D:/Projects/Personal/vc-wt/p8a-<key>` (the gate's two fixes under `D:/Projects/Personal/phase8-work/gate/fix/`); no cloud session worked on it. The machine changed before the phase: Windows updates KB5132061 and KB5132688 and a boot on 2026-11-11 |
| Machine's state | Adobe's Desktop Service used a full processor core without pause from 2026-11-11 until 2026-11-24 (D467); Snagit's editor used one for about nine minutes on 2026-11-24 and for about 23 hours until about 22:30 on 2026-11-26, minutes before the first round's pass; the counted round ran without it. C: had 4.7 GB free of 350 GB on 2026-11-24, when Storage Sense cleaned `%TEMP%` under the tests (D468), and 58.5 GB from 2026-11-26, freed by the owner; Storage Sense is still on. D: had 19.5 GB free on 2026-11-18, 5.8 GB on 2026-11-26 and 3.3 GB on 2026-11-27 after the counted round |
| .NET SDK | 10.0.303 on the laptop (`global.json` unchanged: 10.0.100 with `latestFeature`); in CI's runs 45917903492 and 46007767846 the containers 10.0.404 (Microsoft's installer, the floor's job too) and 10.0.115 (Ubuntu's packages) |
| Git | The laptop 2.56.0.windows.4, first on `PATH` (D248), with `core.fsmonitor=true` in the owner's global configuration and `credential.helper=manager` in the install's system configuration (Waiting on the owner). In CI's runs 45917903492 and 46007767846, the Gate commit's: `windows-latest` 2.56.1.windows.1 (runner image 20261116.315.1), `macos-latest` Homebrew's 2.56.1, whose `git-url-user.txt` reads Kept, `ubuntu-latest` 2.56.1, the two containers 2.43.0, the floor's job 2.34.1 on Ubuntu 22.04. The gits kept for runs with another git first on `PATH` are the 7b handoff's Environment's |
| Packages | The app's unchanged from 7b: Avalonia 12.1.3, Avalonia.AvaloniaEdit 12.0.0, TextMateSharp and TextMateSharp.Grammars 2.0.4, CommunityToolkit.Mvvm 8.4.2, Tmds.DBus.Protocol 0.94.1, xunit.v3 3.2.2 (pinned, D24), FlaUI.UIA3 5.0.0. The icon's tool alone references Svg.Skia 4.9.1, SkiaSharp 3.119.4, SkiaSharp.NativeAssets.Linux 3.119.4 and HarfBuzzSharp.NativeAssets.Linux 8.3.1.3 (D428; Svg.Skia stays on 4.x because 5.x needs SkiaSharp 4.x, which Avalonia 12.1.3 does not take) |
| New projects | `tests/VisualCommit.VisualTests.Phase8a` (thirteen test processes in the default run); `tests/VisualCommit.GitStandIn`, the program `visualcommit-git-stand-in`, in the solution; `tests/VisualCommit.GuideShots`, outside the solution, built by CI's Windows job with the opt-in projects; `tools/VisualCommit.IconBuilder`, in the solution's `/tools/` folder, never shipped and run by no test |
| Ports | None new (ruling 20): 47391 to 47395 and 47397 to 47399 pinned in `AuthRemoteServerTests`, 47396 reserved |
| Outside the repo | mdBook 0.5.4 for Windows at `D:/Projects/Personal/phase8-work/tools/mdbook-0.5.4/mdbook.exe` (design 13.4 item 20 has the hashes; `guide.yml` downloads the Linux archive and checks its SHA-256); the guide's demo root `C:\demo`; winget v1.29.380, an app execution alias at `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe`, on the `PATH` (8a's real-window launches 7 and 8 fail without it, D449) |
| Sandboxes | As in 7b: GitHub's token expires on 2026-11-29, Azure DevOps' and GitLab's on 2026-12-04; 8a made none again |
| Screenshots | Scripted: scaling 1. Real window: 200 % and a work area of 1500×952, as `run.txt` records them. The guide's 84 pictures (42 shots in both themes): scale 2 from demo data, made on every build of the guide and never committed (D439, D440) |

## Build, run and test

The table with notes is the Commands section of [CLAUDE.md](../../CLAUDE.md#commands). New in this
phase:

```
dotnet test --project tests/VisualCommit.VisualTests.Phase8a
dotnet test --project tests/VisualCommit.VisualTests.Phase8a --filter-class "*Phase8aChecksL"
bash scripts/time-scripted-check.sh "<method filter>"
bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase8aRealWindowPass"
VISUALCOMMIT_PERF_RUNS=5 bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase8aPerformance"
VISUALCOMMIT_PERF_RUNS=1 bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-method "*Phase8aPerformance.M2_*" --filter-method "*Phase8aPerformance.M3_*"
dotnet run --project tools/VisualCommit.IconBuilder
VISUALCOMMIT_GUIDE_ROOT='C:\demo' bash scripts/heavy-run.sh --project tests/VisualCommit.GuideShots -c Release
"D:/Projects/Personal/phase8-work/tools/mdbook-0.5.4/mdbook.exe" build docs/guide
python -I scripts/check-guide-links.py --self-test
python -I scripts/check-guide-links.py docs/guide/book
gh run list --workflow guide.yml --branch <branch> --limit 3
python scripts/compare-pictures.py <reference folder> artifacts/visual <output folder> --expected <file>
python -I scripts/scan-contrast.py <pictures folder> <output folder> --jobs 4
```

- `bash scripts/heavy-run.sh` with no arguments is the whole default run, at most four of its
  thirteen projects at once (D343). `scripts/time-scripted-check.sh` runs phase 8a's project when
  the phase is left out; it knows 0 to 6, 7a, 7b and 8a, and exits 2 for any other phase.
- The lagged list (D262) is design 0.4's seven commands, with `RestoreOrderShellTests` added to its
  App.Tests command since the restore order's merge (design 14.10), one at a time through
  `scripts/heavy-run.sh` with `VISUALCOMMIT_WATCHER_LAG_MS=1500` and no agent at work. At step 19
  (2026-11-24, on `640f728`'s code) it ran 1,114 tests in 46 minutes, none failed. A failure under
  the lag gets a fix, never a looser test.
- New variables (design 1.14): `VISUALCOMMIT_GIT_STAND_IN` (a full path, or `none` for no git) with
  `VISUALCOMMIT_GIT_STAND_IN_VERSION` and `VISUALCOMMIT_GIT_STAND_IN_FAILURE`, which the harness
  removes from every test process; `VISUALCOMMIT_PERF_RUNS` (1 to 20 runs per measure; unset, the
  series' facts skip); `VISUALCOMMIT_GUIDE_ROOT` and `VISUALCOMMIT_GUIDE_PICTURES` (GuideShots). The
  data folder gains `instance.lock`, `instance.json` and, only when set, `settings.json`'s
  `gitExecutable`.
- A hand run: CLAUDE.md's isolating variables as before; `VISUALCOMMIT_GIT_STAND_IN=none` shows the
  first-run screen without touching the machine's git. Since D421 a second start on the same data
  folder, `dotnet run` among them, hands over to the running app and exits 0, so a hand run with a
  `VISUALCOMMIT_DATA_DIR` of its own never meets the owner's app.
- A run of `guide.yml` started by hand (`workflow_dispatch`, D451) is made for a gate commit whose
  push starts no guide run, as one that changes only the real-window project does: at 8a's gate it
  was `gh workflow run guide.yml --ref phase/8a-release-readiness` (46024262689), which has its row
  in CLAUDE.md's Commands. One command has not run yet: setting GitHub Pages' source through `gh
  api` at "Phase 8a accepted" (ruling 64). It gets its row the first time it works.
- The live command, as the 7b handoff says: tell the owner first, run its offline classes alone on
  the same commit first (582 tests), never print the whole environment and tell every agent so, and
  check each `run.txt`, fully decoded, for the tokens, the organization's name, the namespace and
  any address before anything from it enters the repo (D372, D384).

## Running the visual gate

8a's gate ran in this order (State of the repo gives the figures): the live command before the
pictures, on `fab4073` (2026-11-26, 11:00); D246's two runs, on `e4b323c` (18:17 and 18:31); the
first round, on `e4b323c`: a default run stopped by the hang watchdog while other work ran beside
it, the guide site's check among it (ruling 114), a second one alone that passed, the pass, the list
and the live command, then D471's correction of the guide's pages with the site read again (ruling
114) and the inspection, which found the first-run path's fault (ruling 115); its fix, D472, merged
at `ee3cdc4`; the counted round (ruling 116), on 2026-11-27: the default run on `0f9099a`, a pass
that failed in the harness, its fix, D473, merged at `750eb33`, the pass again alone on `750eb33`,
the list, the live command and the inspection; then the checks by hand and the close. The steps, as
in phase 7b's handoff, with 8a's numbers and what this gate taught:

1. **Stop what is left and look at the machine**, before each heavy run of the gate: no worktree but
   the main checkout (`git worktree list`); the `git fsmonitor--daemon` processes stopped, which the
   owner's `core.fsmonitor=true` leaves behind (phase 6's handoff gives the PowerShell:
   `Get-CimInstance Win32_Process -Filter "Name='git.exe'" | Where-Object { $_.CommandLine -match 'fsmonitor--daemon run' }`
   lists them, `| ForEach-Object { Stop-Process -Id $_.ProcessId -Force }` stops them; five were
   left before the first round's second default run, two before the counted round's); C: and D:
   (`Get-PSDrive C,D`: Storage Sense cleans `%TEMP%` while C: is low, D468); Windows' Task
   Scheduler for what is due, and a process that holds a processor core (two readings of
   `Get-Process`' `CPU` a few seconds apart; CLAUDE.md's "What to know" has both). Nothing runs
   beside a counted run (ruling 115): no agent, not even a read-only one, no browser, no download.
2. **References** (design 0.7): the scripted pictures against
   `D:/Projects/Personal/phase8-work/references/step19/` (1,239 pictures, its list
   `references/step19-pictures.sha256`), step 19's second whole run, read without fault (ruling
   109); the real-window files of phases 0 to 7b against phase 7b's counted round,
   `D:/Projects/Personal/phase7b-work/gate/round-3dc0887/visual/`, with the scan's `expected.json`
   (`scripts/scan-contrast.py` writes it) and the lead's
   `D:/Projects/Personal/phase8-work/gate/expected-captures.json` for the regions 0.7 names; 8a's
   own real-window files against their scripted twins. That was the first round's; the counted
   round's pictures were compared with the first round's copy, `gate/round-e4b323c/visual/`, with
   the lead's `gate/expected-round2.json` for the path in `f5a` and `f6a`.
3. **The live command before the pictures** (D335), the owner told, its offline classes first on the
   same commit (1 min 36 s), then the whole project (3 min 31 s); its corrections to the fakes
   merged before any picture (8a had none, ruling 112). Made once.
4. **D246's two runs**, the owner present, the desktop unlocked, Windows' high contrast off, winget
   on the `PATH` (D449), 8a's scripted pictures on the machine as twins:
   `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release --filter-class "*Phase8aRealWindowPass"`
   (5 min 5 s to 5 min 41 s each). The part starts a second process of the app, minimises the window
   and brings it back, and its F4 confirms Windows' Open dialog on git's program, which leaves an
   entry in the user's lists of recent files; it finds every window by its handle (ruling 105) and
   sends a key only while the app's window is in front (design 12.10). Made once.
5. **The counted round**, each takeover of the desktop announced in the chat just before it starts:
   `bash scripts/heavy-run.sh`, alone on the laptop (1 h 53 min 8 s for 8,936 tests at 8a's counted
   round, 7,004 s with the build and the lock; 1 h 48 min 52 s for 8,927 at its first); a rest, the
   longer the better (below); the Task Scheduler and a busy process read again;
   `bash scripts/heavy-run.sh --project tests/VisualCommit.RealWindowTests -c Release` (never
   `--no-build`; 40 min 56 s for 145 tests, 2,484 s with its build and the lock); at once
   `find artifacts/visual -name '*.png' | sort | xargs sha256sum > docs/test-reports/phase-8a-pictures.sha256`,
   after a pass that failed too, since a pass run again alone is checked against that list (D246);
   then the live command again, its offline classes first (582 in 1 min 12 s, then 596 in 3 min 4 s,
   195 s with the lock), while GitHub's token still serves.
6. **Comparison and inspection** (D247), with the lead's tools, which are outside the repository and
   no part of it, in `D:/Projects/Personal/phase8-work/gate/`: `inspect_prep_8a.py` (its docstring
   gives the call, run with `python -I`) compares every picture of the round's copy with its
   reference, the expected boxes in `compare-pictures.py --expected`'s form, and writes the batches
   of at most about 150 (`<round>/inspect/batch-<id>.json`) and, for each picture changed beyond the
   window's two bottom corners, a sheet of the capture, the reference and the changed pixels (yellow
   inside an expected box, red outside); `inspect-gate-8a.workflow.js` gives each batch to one Opus
   inspector and each picture not passed to three Opus skeptics; the counted round's batches came
   from `inspect_prep_8a_r2.py`, which takes the same arguments. CI's pictures come from
   `gh run download <run id> -D <folder>` (the Gate commit's run's in `gate/ci-46007767846/`, its
   jobs' logs in `gate/ci-46007767846-logs/`). At 8a's first round 28 inspectors read the 1,696
   pictures and 45 of CI's (ruling 115); the counted round's were read again in 28 batches, CI's one
   of them, in 26 minutes (ruling 116).
7. **The guide's site** (D451; ruling 114): from the `guide-site` artifact of `guide.yml`'s run on
   the gate's code (`gh run download <id> -n guide-site`, served by `python -m http.server`): every
   picture in both themes, search, no request to another host, every `SUMMARY.md` link and the five
   anchors the first-run links open. 8a's check, in its first round (on 45917903593), measured the
   pictures in headless Edge on throwaway profiles and had two inspectors read the 42 shots against
   their pages; it stands for the Gate commit, since the site built from the gate's app code holds
   the same pictures and pages as the one the lead checked after D471 (ruling 116).
8. **The checks by hand**, with the owner, from start files that set CLAUDE.md's isolating variables
   (8a's are in `D:/Projects/Personal/phase8-work/gate/hand/`, with a `README.txt`).
9. **After a fix**: one that changes only `tests/VisualCommit.RealWindowTests` re-runs only the pass
   (D246); one that changes only `tests/VisualCommit.LiveHostingTests` only the live command (D335);
   any other starts a new counted round. 8a's first inspection found a fault of the app, so its
   fix's merge got a new counted round (the default run, the pass, the list, the live command and
   the inspection), while D246's two runs and the live command's run before the pictures stood. In
   that round the pass failed in the harness, and its fix changed only the real-window project, so
   only the pass ran again, on `750eb33`, and the default run on `0f9099a` stands (ruling 116). Copy
   a round's `artifacts/visual/` and its list out of the repo before anything runs again.

What this gate taught:

- **Nothing beside a counted run.** The first attempt of the first round's default run lost two
  projects to the hang watchdog with no assertion failed while the lead ran eight read-only agents,
  two picture inspectors, a download of CI's pictures and, from about 19:16, a headless browser;
  both hangs began within a minute of a browser's start, and the cause is not established. Beside
  that load most projects took far longer (`VisualCommit.Git.Tests` 1 h 8 min 30 s of its own
  against 46 min 46 s in the second attempt, phase 8a's walk-through 59 min 30 s against 37 min 6 s,
  `VisualCommit.Hosting.Tests` 57 min 36 s against 34 min 33 s), though phase 6's and phase 5's undo
  walk-throughs took less (15 min 53 s and 26 min 20 s against 23 min 39 s and 43 min 36 s). The
  rule since ruling 115: nothing runs beside a counted run on the laptop. Both default runs made so
  since, the first round's second and the counted round's, passed with no watchdog stop.
- **Look for a busy process.** Before the real-window pass and before a performance series, read the
  Task Scheduler and look for a process that holds a core (D467): Adobe's Desktop Service did so for
  thirteen days unseen, and on 2026-11-26 Snagit's editor held one for about 23 hours until minutes
  before the pass, so every run of that day before about 22:30 ran under it, the first round's
  default run and D246's two runs among them. Look before the default run too.
- **Rest the laptop before the pass**: Q1 measured 1,912 ms of 2,000 after a rest of 14 minutes and
  1,938 ms after one of nine, each straight after a default run, and 1,626 ms in the pass that
  stands, which began when Windows had counted 65 minutes since the last input, on code that is the
  same for Q1 (risk 10).
- **A commit that changes only the real-window project re-runs only the pass** (D246): when the
  counted round's pass failed in the harness, its fix (D473) changed nothing else, so the default
  run on `0f9099a` stood and the pass ran again alone on `750eb33`, once `git diff --stat` without
  the project and the docs printed nothing and the scripted pictures were still the files of the
  list written after the failed pass. The default run's two hours were not spent again.
- **A wait in the real window looks again after a failed look** (D473): UI Automation can fail a
  look while the app rebuilds what it reads, and before D473 `RealApp.WaitFor` let that end the
  check. Its looks now go through `RealApp.Poll`, and a look that fails as `RealApp.IsFailedLook`
  says counts as "not yet"; a time-out after such looks says how many failed and with what, which
  tells a changing tree from an app that stopped answering. A new wait goes through `WaitFor`; a
  condition that reaches `Find` or a LINQ `Single` still ends a wait at once when an element is
  briefly missing or listed twice (D473 leaves those, the Backlog).
- **The live command runs twice and the tokens expire**: GitHub's on 2026-11-29, Azure DevOps' and
  GitLab's on 2026-12-04; a round after a date needs that token made again first.
- **The pass's share proves nothing about a wrap**: in §12's first runs, made in a worktree whose
  path held no space, the stand-in's path wrapped at its separators. In step 19's run of the part
  (2026-11-24) and in D246's two runs, made in the main checkout, whose path holds a space, it broke
  at that space and after the hyphen of `net10.0-windows`. `f5a` and `f6a` differed from their twins
  by 1.39 and 1.71 % in both cases, under the 3 %, and only the gate's inspectors called the break a
  fault (D472). Read the captures, not the shares; and a run in a worktree whose path holds no space
  does not show such a break.

For phase 8b's gate the references are the copy of 8a's counted round that stands,
`D:/Projects/Personal/phase8-work/gate/round-0f9099a/visual/` with its `pictures.sha256` (the same
list as `docs/test-reports/phase-8a-pictures.sha256`; the failed pass's files are in
`visual-failed-pass/` beside it, and are no reference): every picture of phases 0 to 8a, scripted
and real-window, as the Gate commit's code drew them on this machine.

## What changed in the code

architecture.md describes each part; this table says where to start. Unless a project is named, a
path is in `src/VisualCommit.App/`. Core, Git and Testing are `src/VisualCommit.Core/`,
`src/VisualCommit.Git/` and `tests/VisualCommit.Testing/`, as in the earlier handoffs.

| To do this | Start here |
|---|---|
| Git's detection, the first-run screen, the Git program setting | Core `Git/GitSetup.cs`, `ProgramSearch.cs`; Git `GitLocator.cs`, `GitSetupProbe.cs`, `OsRelease.cs`; `Services/GitAccess.cs`, `ViewModels/GitSetup/GitSetupViewModel.cs` and `GitSetupTexts.cs`, `Views/Shell/GitSetupView.axaml`, `ViewModels/MainWindowViewModel.Git.cs`, `ViewModels/Settings/GitProgramViewModel.cs`, `Services/FileLauncher.cs` (winget's start, D449), `Controls/PathTextBlock.cs` (`IsOnePath`, set on `GitSetupPath`; D472); the stand-in `tests/VisualCommit.GitStandIn` (D423, D449, D453) |
| The git floor | Core `Git/GitVersion.cs` (`Minimum` 2.34.1), `Git/IGitRepository.cs` (`BranchCheckedOutElsewhereException`), Git `GitRepository.Branches.cs` (the fast-forward's worktree read); Testing `GitWords.cs`, `TempRepo.StashAsync`; `.github/workflows/ci.yml`'s floor job (D420) |
| One instance per data folder | `Program.cs` (the guard before Avalonia starts, the exit codes), `Services/SingleInstanceGuard.cs` (`ResolveHeld`, D464), `Services/AppLaunch.cs`, `Views/InstanceNoticeWindow.axaml`, `ViewModels/InstanceNoticeViewModel.cs` and `InstanceNoticeTexts.cs`, Core `AppPaths.cs` (D421, D422, D448) |
| The keyboard's focus | `Views/FocusReturn.cs` and `FocusReturn.Presses.cs` (`FollowKeyboardPresses`, the window's rule, D462 (4)), `Views/MainWindow.Keys.cs` (`FocusAfterSwitch`, D462 (1)), `Views/RowOrderStackPanel.cs` (D466), `ViewModels/Panels/RowCollection.cs`, `Views/Dialogs/DialogView.cs` (D454) |
| Screen readers | `Controls/CommitGraphControl.Automation.cs`, `Controls/GraphRowSpeech.cs`, `Controls/StatusText.cs` and `IStatusBarOperation.SpokenText` (D463), `ViewModels/Commands/AutomationNames.cs`; Testing `Headless/AutomationSweep.cs` (D435, D456) |
| Contrast | `Theme/Tokens.axaml`; App.Tests' `ThemeTokensTests` and `ThemeTokensTests.DimmedTexts.cs` (D418, D469); `scripts/scan-contrast.py`, `scripts/compare-pictures.py --expected` (D419) |
| About and the notices | `ViewModels/Settings/AboutSettingsViewModel.cs`, `ViewModels/Operations/OperationTexts.About.cs`, `Services/EmbeddedTexts.cs`, `Services/AppIdentity.cs`, `Services/AppLinks.cs`, `Controls/DocumentTextView.cs`, `Views/Dialogs/TextDocumentDialogView.axaml`, `VcLinkButton` in `Theme/Controls.axaml`; `THIRD-PARTY-NOTICES.md`, `notices/` and `LICENSE`, embedded by the App's csproj (D431 to D434) |
| The icon | `assets/brand/` (the three masters and `generated/`), `tools/VisualCommit.IconBuilder`, `Assets/Icon/` (the `.ico` as `ApplicationIcon`, the two PNGs), `Services/AppIcon.cs` (D428 to D430, D447) |
| The performance pass and the restore order | `Services/StartupTimeline.cs`, `ViewModels/RepositoryViewModel.cs`'s log lines and `FirstPage`, `ViewModels/RestoreOrder.cs` (`TabServices.RestoreClock`; D457, D458); RealWindowTests `Phase8aPerformance.cs`, `PerformanceSeries.cs` |
| The guide | `docs/guide/` (`book.toml`, `guide.css`, `src/`), `.github/workflows/guide.yml`, `scripts/check-guide-links.py`, `tests/VisualCommit.GuideShots`; the app's links in `Services/AppLinks.cs` (`GuideBase`, `GuidePage`); F1 in `ViewModels/Commands/AppShortcuts.cs` (D408, D417, D439 to D441, D451) |
| 8a's real window | `tests/VisualCommit.RealWindowTests/Phase8aRealWindowPass.cs` with `.AB`, `.F`, `.I`, `.K`, `.L` and `.U.cs`, `RealApp.Phase8a.cs` (`TopLevelElementsOf`, the keys' guard; D442 to D446, D459; rulings 105, 106); the rules it holds with no window, `Phase8aOrderTests` in `Phase8aRealWindowPass.cs` |
| The real-window harness's waits | `tests/VisualCommit.RealWindowTests/RealApp.cs` (`WaitFor`, `Poll`, `IsFailedLook`), `RealApp.Phase3.cs` (`WaitForMenu`, `SettlingRectangle`), `RealApp.Phase4.Checks.cs` (`StateForMessage`), `RealApp.Phase7a.cs` (`WithoutToken`); `WaitForRuleTests.cs`, 21 tests with no window (D473, ruling 116) |
| The test support | Testing `TempRepo.cs` and `LargeHistory.cs` (D468), `AuthRemoteServer.cs` (the holds and their description, D470), `FetchHold.cs`, `Headless/FakeGitSetupProbe.cs`, `Headless/HeldGitRunner.cs`, `IconFile.cs`, `LinkFrame.cs`, `Headless/ShellDriver.Phase8a.*.cs`; 8a's project's `ClientWalk.cs` |

The hosting code was left alone (ruling 2): `src/VisualCommit.Hosting` has no change in 8a, and
`VisualCommit.Hosting.Tests` one line of `HeldRulesTab.cs`. Phase 8b's work lies mostly where 8a
left room: `Program.cs`'s comment for an updater's hook before the guard, the askpass helper's place
(`src/VisualCommit.Git/Prompts/AskPassLocator.cs`; risk 14), the build's trim
(`Directory.Build.targets`, D239), macOS's token store
(`src/VisualCommit.Hosting/Tokens/MacKeychainStore.cs`; risk 34) and the real-window harness's app
path (the `AppExecutable` metadata in
`tests/VisualCommit.RealWindowTests/VisualCommit.RealWindowTests.csproj`, read by `RealApp`).

## Patterns to follow

Phase 7b's patterns still hold (its handoff's table), and those of the phases before. Phase 8a adds:

| For | Example |
|---|---|
| A first-run state in a test | `tests/VisualCommit.VisualTests.Phase8a/GitSetupShellTests.cs`: `StartEmptyAsync(data, gitProbe: probe, askPassHelper: helper)` with a `FakeGitSetupProbe` (Testing `Headless/FakeGitSetupProbe.cs`), then `WaitForGitSetupAsync` (`Headless/ShellDriver.Phase8a.FirstRun.cs`); the machine's git is never removed or faked; a real window or a hand run points the app at `tests/VisualCommit.GitStandIn` through `VISUALCOMMIT_GIT_STAND_IN` (design 1.14). A new test that starts the app through `ShellDriver` goes into the phase's walk-through project, unless it extends an App.Tests class (D416) |
| A test whose git words differ by version | `tests/VisualCommit.Testing/GitWords.cs` (`StashBranch`, `StashText`, `TrackingSetUp`, `HasZdiff3`, `FetchGuardsEveryWorktree`): the git under test's words in one place, never a literal; the scenarios' stashes rebuilt below 2.36 by `TempRepo.StashAsync`; CI's floor job is the run on 2.34.1 (D420) |
| A control whose own press hides, removes or disables it | `src/VisualCommit.App/Views/FocusReturn.Presses.cs` (`FocusReturn.FollowKeyboardPresses`, registered once in `MainWindow`): a press by the keys is followed to its run's end, and a focus left on nothing goes to the control, to the target its view names (`FocusReturn.Instead`), to the item now at its place, to its dialog's or region's target, or to the graph area's, with the frame if the control had it; a view's own return acts first. Hold it with a fact that presses the control by the keys (`KeyboardPassTests.AfterPress*.cs`; D462 (4), ruling 103) |
| A list of rows | `src/VisualCommit.App/ViewModels/Panels/RowCollection.cs`: a list of one row height calls `ReplaceAll(rows, showSame)`, a list of mixed heights `ReplaceAll(rows, showSame, sameItem)`; a row's `ShowsSameAs` compares everything its actions use; a row that implements `IUpdatableRow` raises a change for every bound property, and its `CanUpdateTo` says yes only when all that `UpdateTo` does not copy is the same. A list whose rows hold Tab stops takes `RowOrderStackPanel` as its items panel (D466). A walk-through that hides or solos a branch waits for `WaitForGraphReloadAsync` (design 1.17) |
| A link | `VcLinkButton` (`src/VisualCommit.App/Theme/Controls.axaml`): its frame lies 4 px outside the link on every side, takes no pointer and shows only under `:focus-visible`; `LinkFrame` (Testing) and App.Tests' `LinkFocusFrameTests` read it at the scalings 1 to 2 (ruling 101) |
| A path on screen | `src/VisualCommit.App/Controls/PathTextBlock.cs`: a text that is a path alone sets `IsOnePath`, so that it breaks only after `\` or `/`, also where the path holds a space or a hyphen; a path inside running text keeps the default mode, D227's rule, which ends a path at white space (D472). App.Tests' `PathTextBlockTests` holds both modes |
| A control a screen reader meets | An automation name without the key's text (`ViewModels/Commands/AutomationNames.cs` for shared ones), or `AccessibleNameSweepTests` fails (D435, D456); a new `IStatusBarOperation` gives its `SpokenText`: the start and the result, not the progress (D463 (1)); a button that gets the focus back after an outcome is described by it through `AutomationProperties.HelpText` (D463 (3)) |
| A text colour | `tests/VisualCommit.App.Tests/ThemeTokensTests.cs`: one table of the backgrounds each text lies on, 4.5:1 in both themes, no list of gaps (D418); D469's three dimmed looks are the only exceptions, pinned in `ThemeTokensTests.DimmedTexts.cs`, whose guard fails on a use of the disabled text colour, the dimming classes or the search's dimming that its table `DimmedTextUses` does not list |
| A package added or moved | `THIRD-PARTY-NOTICES.md` with its component, packages, files and licence, held to the app's `VisualCommit.deps.json` by `ThirdPartyNoticesTests`; a vendor's file under `notices/` byte for byte (`notices/** -text`) (D431, D433) |
| A master of the icon | `dotnet run --project tools/VisualCommit.IconBuilder`, then commit what it writes; `IconFilesTests` fails on a master without its files (D428, D430) |
| The app's words, keys or behaviour | The guide's page changes with them; only the pages and anchors the app links to and the shortcuts page's tables are held by tests (`AppLinksTests`, `GuideShortcutsPageTests`; rulings 94, 95); a picture added to a page needs its two images' `width` by hand (D471) |
| A real-window launch | `tests/VisualCommit.RealWindowTests/Phase8aRealWindowPass.I.cs`: a window found by its handle (`RealApp.TopLevelElementsOf`, from `EnumWindows`), never by a search of the desktop's children through UI Automation (ruling 105); every key through `Keys`, `KeysOn` or `TypeKeys`, only while the app's window is the foreground window (design 12.10); nothing that leaves the app pressed (D444); a failed read throws or is noted, never reads as "no window" (ruling 106) |
| A wait in the real window | `RealApp.WaitFor` (`tests/VisualCommit.RealWindowTests/RealApp.cs`), or a wait built on it: its looks go through `RealApp.Poll`, where a look that fails in UI Automation (`IsFailedLook`: a `COMException` of any HRESULT, FlaUI's `ElementNotAvailableException`, its `PatternNotSupportedException` around a `COMException`) counts as "not yet" and any other exception leaves at once; a read of the window for a time-out's message goes through `StateForMessage`, which cannot replace the time-out. `WaitForRuleTests` holds the rule with no window and touches nothing of the desktop: it runs in the whole pass and alone with `--filter-class "*WaitForRuleTests"` (D473) |
| A repository that outlives a test process, and a held fetch | `tests/VisualCommit.Testing/LargeHistory.cs` and `TempRepo.cs`: a shared copy is proven through git before use and made again when damaged, and a kept template's folders are recorded and given to every copy (D468). A held fetch: `ShellDriver.WaitForHeldFetchAsync` before the wait for the percentage, whose timeout's message carries the server's description (D470) |

Rules worth keeping:

- **A user-facing text is checked whole**: §11's reviewers sampled the guide's pages and found nine
  false statements; a pass over all 1,157 statements found 73 (ruling 94).
- **A test that can fail only off Windows has its first run in CI**: reasoning from a platform's
  documents was wrong twice (the folder modes, D464 (4); macOS's `/var`, ruling 104).
- **Measure a list change in the real app, scrolled**, before merging it (the row list, design
  1.17).
- **Run the walk-through checks a fix touches, and the lagged list at each merge** (D262), as in 7b.

## Deviations from the plan

| What | Decision |
|---|---|
| Phase 8 split into 8a and 8b, the phases' names and branches the lead's (amends D241); 8b's first answers: GitHub Releases with the owner's approval of each release, the update check asked at the first start, Windows' beta unsigned with its route chosen before the first stable release, macOS signed and notarised | D399 to D403 |
| The README's note; no git bundled and a first-run screen; the floor 2.34 (amends D2); the icon drafted by the lead and chosen by the owner; the guide on GitHub Pages; About as a settings section, a palette command and the macOS menu's item; the three contrast fixes in one round (amends D198); names, the graph's row and one Narrator walk-through, no high-contrast palette; the name's search and the README's "Name" note | D404 to D412 |
| Risks 39, 40 and 43 left recorded; phase 6's design 17.5 closed; 8a's start through the weekly reset | D413 to D415 |
| The design 305 KB drafted, 356 KB after its review and about 775 KB at the Gate commit with its as-built notes and 115 rulings, against D243's 150 to 200 (§5's brief over D242's 25 KB) | D242, D243 |
| The design's mechanisms: tests that start the app in 8a's project; the palette's three commands and F1 (amends D192, D199, D213); the tokens (amends D198); the scan and the masked comparison; the floor's code (amends D2, D406's "2.34"); the guard; the first run's setting and lookup; no ssh probe and the netrc note; the stage rows' buttons on focus; Tab out of read-only views; splitters by keys; the icon's masters, tool and checks; About, the notices, the text dialog and the notices as build inputs; American spelling in the notices and the guide; the graph as a List with one spoken row (amends D49); the measures and the series; mdBook and GuideShots; the real window's launches, masked notice and never-pressed list | D416 to D446 |
| The review's: the tool writes the `.icns` (amends D429); the notice's folder line and the guard's server for the user alone (amends D443); winget by its full path, no third-party package source (amends D423); the row proven by `LiveRegionChanged` (amends D445); the guide workflow's "App's links" and the hand check's artifact; "Cancel {title}" only while its operation runs (amends D417) | D447 to D452 |
| winget for a missing and a too-old git, not one that does not work (the owner; amends D405, D423 and ruling 44); the owner's mark chosen before §7 was built, so the placeholder D428 named was never written | D453; design 7.8 |
| Enter in a dialog's list confirms (amends design 6.3.9); the spike's verdict, the public route with the risk recorded; the sweep's rules (amends design 9.3.8 and ruling 34 for one control); the notice's real-window comparison at 5 % (amends D448 and, for the notice alone, D33's 3 %); the Narrator listen at the gate (the owner; amends D455) | D454 to D456, D459, D460 |
| The performance pass's bounds and four fired triggers: the reload and Pull's reads to the Backlog, and a change the plan did not list, the restore order (ruling 76), with a design section of its own (section 14), proven and merged; its proof taken again (D461) and holding (D467) | D457, D458, D461, D467 |
| The review before the gate: the keyboard focus kept or given back by one rule for each way it was lost, and the window's rule for every press by the keys; what a screen reader is told of an operation and an outcome (refines D435); the guard after the review (amends D421, D443, D448); Pull's boolean reads by the value in effect and the top commit of a scrolled graph kept across a reload; Tab through a list's rows in their order | D462 to D466 |
| The tests made independent of a cleaner of the temp folder | D468 |
| Three texts the app dims on purpose kept under 4.5:1 as recorded exceptions (the owner; amends D418; plan.md's "Done when" names them) | D469 |
| A hold at 0 of the tests' server ends with the packet that completes the first whole progress line with a percentage, and its waits say what the server did (takes back ruling 109 for the held fetch) | D470 |
| Each image of a guide picture carries `width`, half its pixel width (amends D440) | D471 |
| A `PathTextBlock` whose text is a path alone marks all of it as one path (`IsOnePath`), and the first-run screen's path sets it; found by the gate's inspection; the default mode unchanged (extends D227) | D472 |
| The gate ran two rounds: the first, on `e4b323c`, does not stand, since its inspection found the fault D472 fixes (ruling 115); the counted round's two passes ran on two commits, the default run on `0f9099a` and the real-window pass on `750eb33`, the Gate commit, as D246 allows (ruling 116) | D246; rulings 115, 116 |
| A wait of the real-window harness takes a look that fails in UI Automation as "not yet"; found when the counted round's first pass failed in phase 3's check 22 with no fault of the app; test code in `tests/VisualCommit.RealWindowTests` alone | D473 |

## Known issues

| Issue | How much it matters | Where |
|---|---|---|
| The graph's selected row raises its events only once a UI Automation client has read below the graph (D455's cold case); the first-run screen's error line and the notice's status show with their text in one step, so their live setting is most likely never heard, and the buttons that get the focus back are described by the outcome instead (D463 (3)); VoiceOver is untried and Linux announces nothing (D411) | The route's risk, accepted by D455; the Narrator walk-through at the gate is its test (D460): silence on the row reopens the route by a decision and a gate round | design 9.9 items 1 to 3 and 6; rulings 100, 101; plan risk 53 |
| Three texts the app dims on purpose read under 4.5:1 (D469): a hidden or left-out branch's name, a dropped rebase row, a row a search dims; a selected dimmed row and its labels read lower than the figures the owner was shown (down to 1.26:1 in dark and 1.18:1 in light, a branch label on its lane's colour) | Accepted by the owner, pinned and guarded; a de-emphasis that keeps 4.5:1 is plan.md's Backlog; a word of the owner on the labels drops their own fact | `ThemeTokensTests.DimmedTexts.cs`; D469 |
| What the dimmed texts' guard and render do not see: a colour computed at run time, a class name built at run time, a mark in the graph's column; the C# rule for a disabled state asks only that `IsEnabled` is named; a class that a string names after a `'"'` on the same line, and CSS channels written as percentages or with a decimal point, are named as unseen in their summaries; two message lines of 179 and 188 characters and three `…` in two summaries; a problem the guard did not expect shows only its first 50 characters in xUnit's output | Low; accepted by rulings 112 and 113 | the same file |
| The held fetch (D470): that a progress line cut between two packets is what failed twice in CI's Windows job is likely and not shown (the runner's git 2.56.1 was not measured); ruling 113's nine limits, among them a failed write of a held response's headers that leaves its hold taken until a release or the server's disposal, no committed test of the states only a race reaches, and the real-window pass's two waits for a held fetch (`Phase3RealWindowPass.D.cs`), which get the new rule but neither wait for the hold first nor add the server's description | Low; another failure of this kind in CI or at a gate reopens it: read its message first, which says whether the hold began, what went before it and the last whole line git could show | `tests/VisualCommit.Testing/AuthRemoteServer.cs`; ruling 113; plan risk 51 |
| A cleaner of the temp folder (D468): Storage Sense, still on, removes empty folders under `%TEMP%` when C: is low. Not helped: a test in flight at a strike, and a template or the shared bare copy stripped after its record or proof (the proof runs once per process, so a `Check_14` can fail after a `Check_13` passed in it); the sixteen templates `VisualCommit.Git.Tests` keeps by helpers of its own; a delete of a damaged shared copy that fails midway; the copies that stay strict (`Scenarios.AuthRemoteAsync`'s among them). The tolerant copy leaves out, without a word, what a folder refused for another reason holds, and the Windows fact must be its folder's only holder | Low while C: has room; accepted by D468 (4) and rulings 108 and 110. A run that fails in many set-ups at one moment is read against the disk first (`Get-PSDrive C`), and run again | `TempRepo.cs`, `LargeHistory.cs`, `TempFolderCleanerTests`; CLAUDE.md's "What to know"; plan risk 48 |
| The keyboard focus: while a press's own run disables its control (Fetch all and Pull all, the toolbar's Fetch and Pull on a dashboard, the rebase's Start, the resolver's Merge tool, Undo and Redo) the focus is on nothing until the run ends; only the clone form's view names a target for that time; a loss with no press before it (an operation's end hiding the status bar's Cancel, a bulk run's end hiding Cancel all, a refresh replacing focused pull request rows) and a command run from a palette opened over an unframed focus are not followed; after a search that finds git, the graph area's target, and after a Choose that passes, Check again, get the focus without the frame; a failed start of winget or of the Command Line Tools shows in the error line and is spoken nowhere while its button keeps the focus; `GitSetupView.GiveFocusBack`'s latent case | Low; accepted with Backlog rows by rulings 101 to 103 and 105 | `Views/FocusReturn.Presses.cs`; design 13.1 |
| A click on a button that still shows the keyboard's frame counts as a press by the keys (ruling 102), so after a click on the banner's framed Continue a mouse user sees the focus frame round the commit list until the focus moves (phase 4's `c10l-merged`, `c11g-second-stop` and `c11j-rebased`) | Accepted by ruling 107 as the rule's outcome; for the owner, who may want a click never to carry the frame, a change to ruling 102 | ruling 107 |
| Focus frames: at a scaling of 1 Dark's frame lies under two faint pixels of the Theme note's ink, and at other scalings Dark's and System's touch the note or keep one row from it; GitLab's legacy token link's frame touches the token link's descenders at 150 %; a radio button's frame is cut by the scroller when Tab brings it to a scrolled section's edge (6 px at the minimum window in the repository scope with "Other" shown) | Low; each fix moves other parts of earlier pictures; accepted with Backlog rows by ruling 101 | `Theme/Controls.axaml`, the settings screen, 7b's GitLab dialog |
| The row list: a row that keeps its key but cannot change itself is replaced, so as the top row in view it leaves a scrolled list short or sends it to its top, and a press on it loses its click; a row left in place between the first and the last row that shows something new stays in Avalonia's estimate, so deep in a long list the offset can end further off than before 8a. Older than the phase: Avalonia's estimate after a change above the view (a branch created above the rows in view draws them 22 px lower with new controls); a solo's cost with thousands of branches, unmeasured | Low; Backlog rows (S19-26, S19-28; ruling 100) | `ViewModels/Panels/RowCollection.cs` and its comment; design 1.17 |
| The restore order: "Check again" reopens the tabs whose restore failed all at once (ruling 90); while tabs wait (5 s at most, then a free slot) a dialog does not name the repository and a dashboard's Add open tabs leaves them out (S19-03, S19-04); a share that does not answer holds a slot for minutes; macOS and Linux measured nowhere | Low; ruling 90's Backlog row; ruling 99 accepted the waits as bounded | `ViewModels/RestoreOrder.cs`; design 14.9 |
| The floor on git 2.34: the fast-forward's worktree read does not see a branch being rebased or bisected in another worktree, nor a checkout between the read and the fetch (2.35 and later refuse both; nothing is lost); a mirror refspec can move a branch checked out in another worktree; a worktree path that holds an empty line loses its branch line | Low; accepted (ruling 75) | design 3.9; plan risk 49 |
| One instance per data folder: a first instance hung before its window keeps answering "starting"; a second start waits up to 30 s unseen while the first closes; a data folder on a network share depends on the mount's locks; a new holder that keeps the lock while its endpoint refuses holds a waiting start until the closing wait ends | Low; accepted (design 13.4 item 5; ruling 100) | `Services/SingleInstanceGuard.cs`; design 4.9; plan risk 50 |
| The netrc note reads git's home only: curl's `NETRC`, or Windows without `HOME`, escapes it (the guide says so) | Low | design 5.9 |
| What D472 left to D227's rule: a path inside running text in a plain text block (the first-run screen's body, guidance, git's words and command; the settings row's status), a tooltip whose text is a path alone (what `TrimmedTextBlock` shows when it cuts its text), and a UNC path written with forward slashes, which the default mode lets break between its two leading slashes. No scripted picture shows the fix, since the walk-through's paths are short: the real-window `f5a` and `f6a` of the counted round that stands are where it is seen (ruling 115's limit 3), the path in three lines that break after `tests\` and after `net10.0-windows\` (ruling 116) | Low; none seen wrong in a gate picture; accepted by D472 and ruling 115, in plan.md's Backlog for phase 8b | `Controls/PathTextBlock.cs`; D472, ruling 115 |
| What D473 left in the real-window harness: about 70 of the pass's 335 wait conditions reach `Find` or a LINQ `Single` and still end a wait at once when an element is briefly missing or listed twice; loops that act and read between their actions (`RealApp.Phase7b`'s `ScrollAccountsTo` and `ScrollAccountsToEnd` among them); reads after a time-out in some catch blocks, which can still replace the time-out with the read's exception; FlaUI's `CheckBox.IsChecked` and `TextBox.Text` on an element that has gone. D473's rule covers a `COMException` and FlaUI's two forms of it, not every way a changing tree can end a wait | Low; a check can still fail in the harness while the app rebuilds what it reads, as phase 3's check 22 did; a time-out now names a failed look. Accepted by D473 and ruling 116, for plan.md's Backlog | `tests/VisualCommit.RealWindowTests/RealApp.cs`; D473, ruling 116 |
| Gathered at wave 2: a search for git that throws shows "the path below" with no path under it; `FakeGitAnswer.RealAt` reports a made-up path while the search behind it logs the machine's git; on macOS a chosen `/usr/bin/git` while the Command Line Tools are missing shows Missing without "Use the Git on the PATH", and the row's note may then say that VisualCommit looks for Git again when you save although nothing is pending (no test reaches it); `FetchHold`'s script holds no fetch from a remote whose path git hands it with backslashes; a theme switch or a large diff's late colouring while a hunk bar's button has the frame moves the focus to the diff's text; the icon's tool has no automated test and deletes every file it does not write in its two output folders | Low | the Notes of status.md on the phase's branch, "Wave 2 is built and merged" |
| Kept as they are, rows for plan.md's Backlog: the graph's widest dates and a conflict's last line under the overlay scroll bar (S19-07, S19-10); Recent repositories opens a second tab on a worktree another tab shows (S19-08, D209); Open does not find a tab through a link's spelling of its folder (macOS's `/var`, ruling 104); the hunk bars' repeated "Stage hunk" and "Discard hunk" names (13.4 item 15); an item status on the left panel's rows (ruling 36); keyboard selection of commits not next to each other (ruling 50) | Low | rulings 99 and 104; design 0.1, 13.4 |
| Unexplained, each seen once in 8a: `PaletteGrowthTests` failed for the first time on Windows (ratio 8.1 against 6.3, run 44479788558; plan risk 46); `PaletteEntriesTests`' dashboard wait timed out on macOS (run 44851353430; since step 19 the wait no longer hangs on a line a pass rightly does not write); the hang watchdog stopped `UndoRecordingTests.A_hard_reset_is_undone_with_its_files_and_index_from_the_snapshot_and_redone_with_a_new_one` at 181 s in a whole run of 2026-11-22 that counted for nothing (S19-32: four projects at once stays); and the two watchdog stops of the first round's first default run, with other work beside it (ruling 115; Things that cost time) | A CI run or a whole run can fail on one alone | plan risk 46; status.md's Notes, "Timeouts of older tests" |
| `scripts/setup-linux.sh`'s header still says that CI runs the script in a bare ubuntu:24.04 container and sets `SETUP_LINUX_SDK_SOURCE` in one of its two container jobs; since `e04762b` it also runs in the floor's Ubuntu 22.04 container, which sets it too, so two of three container jobs do. The script is code, so its comment was left past the Gate commit | Low: a comment | For 8b's first code commit ("Do first") |
| Unverified: `ci.yml`'s `paths` order rule until a push changes only the notices (design 8.9 item 6); that no other component hides in a vendor's notices file, as Wuffs and SPIRV-Cross did (8.9 item 4); that `Process.StartTime` reads on macOS and Linux (10.9); in CI's macOS pictures phase 5's `a2d-show-reads` holds one `git status` more than the laptop's, not looked into | Low | design 8.9, 10.9; the report's "Other platforms" |
| No test holds the guide's image widths: a picture added to a page needs its two widths by hand; Firefox and Safari were not measured | Low; a picture a browser shows at another size than the app's reopens it (ruling 114); the test is plan.md's Backlog for 8b | D471; CLAUDE.md's note on the guide |
| Pictures that vary by nature, besides 7b's kinds (a caret's blink, a temporary folder's random name, a fake's fallback port, a progress bar, edge pixels): the held fetch's percentage and bar in phase 3's `17b`, `17d`, `18b`, `31a` and `31b` and their real-window twins (N is not fixed; ruling 113); phase 3's `18b` lane edge in three forms; phase 2's `17c-restored`, which flips between two files; phase 0's `06c-restarted-first-frame` by its first-frame race; part F's real-window captures with the stand-in's path (ruling 47) | Ordinary: an inspection expects them | rulings 107, 109, 111 and 113; the report's "Run" |
| The run time: the default run 1 h 53 min 8 s for 8,936 tests at 8a's counted round (1 h 48 min 52 s for 8,927 at its first round; 1 h 31 min 48 s for 7,892 at 7b's gate), `VisualCommit.App.Tests` the longest project, 1 h 35 min 25 s from its start to its end beside the others; the real-window pass 40 min 56 s for 145 tests, the first round's 124 and `WaitForRuleTests`' 21, which start no app (41 min 34 s for the 124 at the first round; 33 min 35 s for 77 at 7b's); CI's Test steps during the phase up to 45 min 28 s on Windows (the Gate commit's run) and 47 min 44 s on macOS, against the 56 at which D377 raises the limit; the Windows step read 34 min 2 s, 40 min 34 s and 45 min 28 s in the three runs of 2026-11-26 and 27, on the same tests but nine | Every full run, the gate and CI | plan risks 8, 12, 18; D343, D377 |
| Q1 1,626 ms of 2,000 in the pass that stands, after about an hour without input; 1,912 ms and 1,938 ms in the two passes that did not count, after rests of 14 and 9 minutes straight after a default run, on code that is the same for Q1 (1,827 ms at 7b's gate, 1,400 ms at 7a's); the pass starts the app from Git Bash, whose `PATH` finds `ucrt64\bin\git.exe`, where a user's desktop finds `cmd\git.exe`, Git for Windows' launcher, which costs a process more per git call (S19-22 kept the test host's `PATH` in 8a) | A hot or busy laptop fails a gate by itself | plan risk 10 |
| Leftovers at this writing (2026-11-27): 42 local `wip/p8a-*` branches, each merged into the phase's branch, never pushed: `about` `120df7e`, `after-browse` `e2aa0e9`, `after-frames` `e742475`, `after-hide` `8af7590`, `after-order` `1382040`, `after-speech` `7010e55`, `ci-sweep05` `996b9fc`, `ci-underline` `8f50a34`, `close-docs` `13d36a6`, `colours` `a1a4064`, `copy-walk` `4fe7bd3`, `dimmed` `315b902`, `docs` `5c7e215`, `docs-34` `affbd6f`, `docs-56` `47fd7ed`, `docs-after` `519fb10`, `docs-fixes` `bc14009`, `fetch-hold` `e305acc`, `firstrun` `1daecda`, `fix-firstrun` `a733f68`, `fix-focus` `947f24a`, `fix-git` `23dd655`, `fix-harness` `8be5582`, `fix-instance` `8f793ce`, `fix-speech` `83e36f6`, `floor` `3e6f89e`, `guide` `bc24568`, `hold` `f019688`, `hold-notes` `fdf7414`, `icon` `a8f1dfb`, `keyboard` `87adae7`, `linkframe` `5177cec`, `path-wrap` `6fa567f` (the gate's fix of the app, merged at `ee3cdc4`), `perf` `62e031c`, `realwindow` `902288e`, `restore` `aa83146`, `rows-scroll` `a9a7847`, `rw-handles` `418ab68`, `single` `894829a`, `temp-cleaner` `d576558`, `uia` `eab8dc6`, `uia-poll` `df263aa` (the gate's fix of the harness, merged at `750eb33`); the last two fixes' worktrees, `D:/Projects/Personal/phase8-work/gate/fix/p8a-path-wrap` and `p8a-uia-poll`, are still there, and the gate's step 1 wants no worktree but the main checkout before a heavy run; and, unmerged, `wip/p7b-palette-growth` at `c90b862` (7b's refuted fix, plan risk 46), `backup/phase-before-split` at `d3a5780` and `backup/p7b-accounts-before-split` at `bf6383c` (WP-A's commits from before the token-shaped literals were split, never to be pushed). `D:/Projects/Personal/vc-wt/` is empty | Clutter; CLAUDE.md's step 1 leaves every `wip/` and `backup/` branch out (D396), and whether they go is the owner's | For the next phase, "Do first" |
| Working folders outside the repo. Needed later: `D:/Projects/Personal/phase8-work/research/` (plan.md's "Phase 8b" cites its briefs), `tools/mdbook-0.5.4/` (CLAUDE.md's row for a local build of the guide), `wave4/restore/series2/run_series2.sh` (CLAUDE.md's row for the alternated series; it expects its two builds' worktrees at `D:/Projects/Personal/vc-wt/p8a-restore-base` and `p8a-restore`, which are removed, so make them again or change its `BASE` and `FIX`) and `gate/` (8a's gate). History once 8a is accepted: the waves' folders, `wp0/`, `ci/`, `icon/` (the drafts; the owner's choice is kept in `docs/brand/icon/`), `references/`, `baseline/`, `design/` and `research2/`, and `D:/Projects/Personal/phase7b-work/` (about 6 GB) and `phase7a-work/` (1.2 GB). But `phase7b-work/`'s `research/scratch-g3-git/` and `verify/git243/` hold the gits kept for runs with another git first on `PATH` (Environment, the 7b handoff's): they are needed while such a run may come, or are moved first | Disk: D: had 3.3 GB free on 2026-11-27 after the counted round; what goes is the owner's word | Waiting on the owner |
| Phase 7b's known issues still stand, but for the `.slnx` comment (fixed in `e04762b`) and phase 2's commit boxes (K7, D392); a netrc entry still answers a bound call first on git 2.34 to 2.45, and the Accounts section now says so (F14); risks 39, 40 and 43 the owner left recorded (D413). Risk 43's macOS case: CI's macOS git is Homebrew's 2.56.1, whose `git-url-user.txt` reads Kept in runs 45917903492, 45965143178 and 46007767846; risk 43's last column names a 2.56.1 in Homebrew as what retires that case, and the risk stays as the owner left it (D413) | As there | [phase 7b's handoff](phase-7b.md) |

## Things that cost time

- **The gate's two rounds** (rulings 115 and 116): three default runs and three real-window passes,
  about 7 h 45 min of the laptop in runs, for the one of each that stands. The first round's first
  default run lost two projects to the hang watchdog with no assertion failed, while other work of
  the lead's ran beside it (Running the visual gate), and was run again alone: almost four hours for
  one counted run. Ruling 23 kept agents that run a process away from a heavy run; read-only agents,
  inspectors, a download and a browser ran beside it. Since ruling 115 nothing runs beside a counted
  run: no agent, not even a read-only one, no browser, no download. The round it began then fell to
  the app's fault in the first-run path (D472), so the default run and the pass were run again; and
  the counted round's first pass failed in the harness, on one look of UI Automation that met the
  Remotes dialog while it built its rows again, in a check that had passed in every pass before
  (D473). Its fix took a package of a builder, two readers and a fixer, and the lead's three runs of
  `WaitForRuleTests`, before the pass ran again.
- **The held fetch that showed no percentage** (D470; rulings 109 and 113). CI's Windows job failed
  twice on it (run 45586070342 in `RemoteOperationTests`, run 45871534398 in phase 3's check 17; 2
  of the 29 Windows jobs counted on 2026-11-26, both on runner image 20261116.315.1 with git
  2.56.1.windows.1) and the laptop once (wave 2's lagged run), each passing when run again. Ruling
  109 first left it for the Backlog; the second failure brought two packages before the gate
  (`4370039`, `72993a7`) and a commit of comments (`2a52897`). The probe found that Git for Windows
  2.56 writes a progress line of odd length in two writes, its last byte apart (600 of 600 runs),
  and that once in 510 fetches the server's hold ended between them. The lead's first guesses, git
  writing character by character and a busier machine failing more often, were refuted or not borne
  out. A wait now says whether the hold began and what the server sent: read that first.
- **Measures taken under a busy processor** (D461, D467). Adobe's Desktop Service used a full core
  from 2026-11-11 and was found only on 2026-11-24, so every measure of the phase before that day
  (the three "before" series of 2026-11-22, the first alternated series, Q1 in the real-window runs)
  was taken under it; the restore order's proof was taken again on a quieter laptop, and the two
  series' times are not to be compared. The "before" series also met a Release build within two
  minutes (the build is stamped with the commit, so a commit after a build makes the command build
  again) and an idle-triggered task (D457 (1)); four of the first alternated series' nine pairs met
  tasks the scheduler had not shown as due. Look for a busy process, and read the scheduler before
  and after every measured run.
- **The temp folder's cleaner** (D468; rulings 107 to 111). With C: at 1.3 % free, Storage Sense
  removed empty folders under `%TEMP%` three times in about eighteen hours (2026-11-24 15:05,
  2026-11-25 03:21:43 and 09:31:22), and a git folder whose refs are all packed is then no
  repository. It failed phase 1's checks 13 and 14 in step 19's first whole run and three of 7b's
  checks in its second; two packages and a third whole run followed, and the owner freed C:. A probe
  of the lead's logged the strikes
  (`D:/Projects/Personal/phase8-work/wave4/temp-cleaner-probe.log`). Look at the disks before a
  heavy run.
- **A search of the desktop through UI Automation** (rulings 105 and 106). After §12's merge launch
  6 failed in about two runs of five, on the package's own commit too: a search of the desktop's
  children by process id asks every program on the desktop, and a slow one held it for up to 18.9 s.
  The lead's first two readings were wrong; a trace and the two commits compared found it. The
  harness finds every window by its handle since `14789f7`. The session's permissions refused
  `dotnet tool install dotnet-stack`, and nothing was done around the refusal.
- **The row list** (design 1.17). A first fix of a click CI's Windows job lost (`48f403c`, merged at
  `809b77f`) told the view only of the rows that show something new, and Avalonia's virtualizing
  panel takes a replace of the top row in view as a slot of no height, so scrolled lists ended short
  or jumped to their top. A review and five rounds reached the rule that stands (`bad98b1`); with
  its pictures and the checks of its docs it cost about 7.9M tokens.
- **CI found what the laptop did not** (the CI section of status.md on the phase's branch): Ctrl+C
  is not the copy key on macOS (run 44969394972); a pixel rule of the underline that was wrong on
  macOS, and a click the app lost on Windows (45019059991); .NET gives the mode asked for to the
  last folder alone, which the guard's test had read otherwise, in the five jobs off Windows at its
  first run anywhere (45421164866, D464 (4)); macOS reaches the temporary folder through `/var` for
  `/private/var` (45515065583, ruling 104); a race of the fetch hold's script in a container
  (44969394972, `eba3717`). Ubuntu's slow package servers once cancelled both containers at their 25
  minutes and made the floor's set-up take 19 min 3 s (44479788558): such jobs are run again (ruling
  74).
- **Agents beside a heavy run**: the baseline of 2026-11-18 ran beside up to 13 Opus agents that
  only read files and the web; the hang watchdog stopped `VisualCommit.App.Tests` 2,478 s in, and
  the project passed alone (33 min 35 s).
- **A window behind the test host**: Windows keeps a process the test host starts behind the
  foreground window, so in §12's runs the app's window was not in front when a launch began, at
  launches 3 and 6 in the first runs and at launch 3 and the notice's launches in the second; one
  click on its title bar brought it forward each time (status.md's Notes on §12's first and second
  runs; the report's real-window texts).
- **Two faults of the lead's own agents at the gate** (ruling 115, told to the owner). The builder
  of the session for the checks by hand started the app on the owner's desktop by mistake
  (2026-11-27, 00:06:49, two windows on throwaway data folders for about 35 s): its dry run of the
  start files was to cut the start line with `sed`, but the pattern ended in a carriage return,
  which Git Bash's `sed` had already taken away. It closed both windows and removed what they wrote.
  The fix's two readers ran their experiments in one worktree at the same time, so that one found
  the other's unfinished probe. A reader that changes files to prove a test gets a worktree of its
  own, or the readers run one after the other.
- **The cost in tokens** (the records' figures): the two workflows before the questions about 4.4M;
  the design's research 2.96M; the design, its review and its fixes 13.8M; WP0 and wave 1 about
  14.6M; wave 2 about 17.6M and 1.69M for its new pictures; wave 3 with its close about 27.3M, where
  15 to 18M were expected, because its close found three faults and each was fixed and checked
  independently; wave 4 35.5M before the review's fix run, and the fix run with step 19's runs after
  it at least 30M more by the figures recorded (several record none). One night's workflows took
  about eleven hours of the clock for an hour or two of work, the agents standing still, by the look
  of it on the plan's usage limit.

## For the next phase

Reuse:

- **What the release ships**:
  - `visualcommit-askpass` is the prompt and credential helper of all three providers (risk 14):
    `AskPassLocator` looks for it next to `VisualCommit.Git`'s assembly, or in the app's base folder
    when the assembly has no location (single-file). No code under `src/` sets `DOTNET_ROOT`, so in
    a self-contained package the framework-dependent helper does not start on a machine without .NET
    10 (plan.md's phase 8b item). Since 8a a missing helper is a first-run state (F11), "Part of
    VisualCommit is missing".
  - The build's trim (`Directory.Build.targets`, D239): on Windows a build without a
    `RuntimeIdentifier` keeps only the machine's platform's native libraries, so a portable
    `dotnet publish` (no `-r`) needs `-p:VisualCommitAllRuntimes=true`, or it carries win-x64's
    alone; a `dotnet publish -r <rid>` loses only the native `.pdb` files, which
    `-p:VisualCommitNativeSymbols=true` copies again.
  - The icon (D428 to D430, D447): the masters `assets/brand/icon.svg`, `icon-small.svg` and
    `icon-macos.svg`; `tools/VisualCommit.IconBuilder` writes the `.ico` (the App's
    `ApplicationIcon`, `src/VisualCommit.App/Assets/Icon/VisualCommit.ico`) and the app's two PNGs,
    Linux's eight PNGs of 16 to 512 (`assets/brand/generated/png/`, with `icon.svg` as the scalable
    icon), the `.iconset`, `assets/brand/generated/VisualCommit.icns` for the macOS bundle, and
    `manifest.json`. It runs by hand when a master changes; `IconFilesTests` checks formats, sizes
    and the masters' hashes, never pixels (D430).
  - The notices (R6; D431, D433): `THIRD-PARTY-NOTICES.md` and the four vendor files in `notices/`
    cover what the build output ships; the .NET runtime's notices and the installers' are 8b's
    (ruling 15): a self-contained package ships the runtime, which no test reads yet. The app embeds
    `LICENSE`, the notices file and `notices/`, so they are code in CLAUDE.md's acceptance diff and
    start CI runs (`ci.yml`'s `paths`, D433).
  - The version: `Directory.Build.props`' `Version` is 0.1.0, and the build stamps the commit into
    `ProductVersion` (`0.1.0+<commit>`), which About shows as `0.1.0 (<short commit>)` (ruling 59);
    the version scheme from a tag is one of 8b's questions.
  - One instance per data folder (D421, D464): `Program.Main` takes `instance.lock` before Avalonia
    starts; an updater's start-up hook goes before the guard (the comment at `Program.cs` line 15);
    a second key by install folder and the macOS limits are 8b's (ruling 67). The data folder holds
    `instance.lock`, `instance.json` and, when set, `settings.json`'s `gitExecutable`, for 8b's
    question of what an uninstall removes.
  - Git (D405, D406, D420): no git is bundled; the minimum is 2.34.1, tested by CI's floor job, and
    is looked at again in May 2027, when Ubuntu 22.04's standard support ends. A missing or too-old
    git gets the first-run screen: winget on Windows where it exists, started by its full path
    (D449, D453), the Command Line Tools on macOS, run on no Mac yet, the distribution's package on
    Linux.
  - The guide (D408, D439, D451): its site deploys to GitHub Pages from `master` alone once Pages'
    source is set (ruling 64); until the first deploy the app's links to
    `https://rahimpasha.github.io/VisualCommit/` answer "not found" (design 11.9). `install.md` says
    the installers come with the public beta and that the app is built from source until then; 8b
    rewrites it and its "What an uninstall leaves" with its packages, and `AppLinksTests` holds the
    pages and anchors the app links to. After a gate commit `docs/guide/**` stays docs, checked by
    the guide's workflow, which the lead reads and records beside CI's (D451).
- **The performance series** (`Phase8aPerformance`, opt-in through `VISUALCOMMIT_PERF_RUNS`; D436,
  D457, D458): each counted run is held to M2 9,500 ms, M3 8,900 ms, M4 370 MB after the load and
  440 MB at the peak, M5 4,600 ms, M6 8,800 ms and M7 630 ms. Take a series as CLAUDE.md's row says
  (the Release build on the very commit at least ten minutes before, nothing committed between, the
  scheduler read before and after, no process holding a core, no agent at work); its numbers are the
  laptop's of that hour (D467). 8a measured the JIT-compiled Release build: what a start costs in a
  build compiled ahead of time (ReadyToRun) is a point for 8b's published build.
- **The regression against the installed build** (plan.md's phase 8b): the real-window harness
  starts the app that its csproj's `AppExecutable` names,
  `$(RepoRoot)src\VisualCommit.App\bin\$(Configuration)\net10.0\VisualCommit.exe`, fixed at build
  time, so a pass against the installed app needs that path, or a second one beside it. The pass
  starts the app from Git Bash, whose `PATH` finds git itself, not the launcher a user's desktop
  finds (S19-22, risk 10).
- **The checks by hand**: plan.md's phase 8b items from phases 3, 6, 7a and 7b (risk 41), 8a's lines
  for the Mac checklist (Result, "Not delivered"), and the macOS checklists of the phase 0 to 7b
  reports, 78 steps.
- **Naming**, as 8a's design 0.3 named 8a's: `8b` in the docs (`docs/phase-8b-design.md`,
  `docs/test-reports/phase-8b.md`, `docs/test-reports/phase-8b-pictures.sha256`,
  `docs/handoffs/phase-8b.md`) and the folders
  (`artifacts/visual/phase-8b/{scripted,real-window,timings}`); if the phase adds a walk-through,
  the project and namespace `tests/VisualCommit.VisualTests.Phase8b`, the classes `Phase8bChecks`
  and `Phase8bChecks<Part>`, `Phase8bExpectations.Phase = "8b"` (a `string`), an `8b)` case before
  `8a)` in `scripts/time-scripted-check.sh` with its default moved to `8b` in the commit that
  creates the project; in the real window `Phase8bRealWindowPass.cs` with `.<Part>.cs`,
  `RealApp.Phase8b.cs` and the csproj link
  `..\VisualCommit.VisualTests.Phase8b*\Phase8bExpectations*.cs` as
  `Phase8b\%(Filename)%(Extension)`; parts avoid C, O, Q, R and T (D230); branches `wip/p8b-<key>`
  from `phase/8b-packaging-beta`, worktrees `D:/Projects/Personal/vc-wt/p8b-<key>`. Ports 47391 to
  47399 are taken or reserved, and 8a added none: a new one takes 47400 or later, pinned in
  `AuthRemoteServerTests`, by a decision that amends D97 as D265 and D363 did.
- **The lagged list** (D262): 8b's design names its own, from 8a's (design 0.4's seven commands with
  `RestoreOrderShellTests` in the App.Tests command) and the classes 8b adds that start the app with
  real git.

Do first:

1. The start-of-session steps. CLAUDE.md's step 4 names `architecture.md` and `decisions.md` whole
   (about 1.0 MB and 0.47 MB at 8a's close; 828 and 353 KB at 7b's merge; whether to read them by an
   index is the owner's, Waiting on the owner). What 8b needs of them: in `architecture.md`
   "Design", "Solution layout", "As built", "Start-up and composition", "Prompts and credentials",
   "Tokens and the keychains", "Settings, session, log and data folder", "Tests", "CI", "Package
   versions" and what it says of 8a's single-instance guard, icon, notices and guide; in
   `decisions.md` D82, D239, D255, D399 to D403, D405, D406, D420, D421, D428 to D433, D439, D451
   and D464, and any other by its number where a section cites it. Then plan.md's phase 8b and its
   risks 1, 6, 14, 34 and 41, with 8, 10, 12, 18, 26, 27, 42, 46 and 48 to 53; the Known issues
   above and 7b's; CI's run on `master` after the merge. On the Windows machine push in the form
   CLAUDE.md's Commands give until the owner settles Git Credential Manager.
2. Before the design, the owner's answers to plan.md's "Phase 8b" questions, each with the
   research's recommendation (`D:/Projects/Personal/phase8-work/research/`: `web-installers.md`,
   `web-macos.md`, `web-channels.md`, `repo-ship.md`, `repo-gate.md`, `repo-critic.md`), each
   recorded as a decision and carried into plan.md; with them the Windows route, a month before the
   first stable release if it is to be Azure Artifact Signing (D402), the week's share (D241) and
   the three live tokens made again.
3. The leftovers in Known issues, if the closing session did not remove them: the worktrees
   `D:/Projects/Personal/phase8-work/gate/fix/p8a-path-wrap` and `p8a-uia-poll`, if still there, go
   with `git worktree remove`; the merged `wip/p8a-*` branches, `wip/p7b-palette-growth` and the two
   `backup/*` branches stay local or go, never pushed (D396); fsmonitor daemons; room on C: and D:.
   A baseline whole run only as D245 allows. In 8b's first code commit, `scripts/setup-linux.sh`'s
   header (Known issues), which is code and so was left past 8a's Gate commit.
4. The design: 8a's is the model (section 0 with the naming in 0.3, the lagged list in 0.4, the cost
   in 0.6 and the earlier pictures in 0.7; section 1's contracts; a brief per package). Estimate
   what the phase adds to the default run (1 h 53 min 8 s for 8,936 tests at 8a's counted round)
   and to the real-window pass (40 min 56 s for 145 tests), and how the regression against the
   installed build runs beside them, before writing the checks.

Risks to watch:

- The run time (risks 8, 12, 18): the default run grew from 1 h 31 min 48 s for 7,892 tests at 7b's
  gate to 1 h 53 min 8 s for 8,936 at 8a's counted round; CI's macOS Test step reached 47 min 44 s
  and the Windows one 45 min 28 s, against the 56 at which D377 raises a limit (only at a merge
  before the counted round), and the Windows step spread from 34 min 2 s to 45 min 28 s over three
  runs of nearly the same tests.
- Q1 (risk 10): 1,626 ms of 2,000 in the pass that stands, 1,912 and 1,938 ms in the two that did
  not count, after rests of 14 and 9 minutes; rest the laptop, read the scheduler and look for a
  busy process before the pass.
- Disk (risk 27): D: had 3.3 GB free on 2026-11-27 after the counted round, and Storage Sense cleans
  `%TEMP%` while C: is low (D468).
- The real-window harness (D473): a wait condition that reaches `Find` or a LINQ `Single` still
  ends a check when the app rebuilds what it reads (Known issues).
- The palette flake (risk 46), which failed once on Windows in 8a; the held fetch (D470), whose
  messages now say whether the hold began.
- Risk 44, GitLab's rate limit against restored tabs, was left alone in 8a (ruling 2); the restore
  order opens the restored tabs at most four at once after the active one, not measured against the
  limit.
- Git's versions (risk 26): CI's Windows, macOS and `ubuntu-latest` jobs run 2.56.1, the containers
  2.43.0 and the floor's job 2.34.1; each job's `git-url-user.txt` says which kind of git it ran
  (risk 43).
- The live tokens (risk 42): GitHub's expires on 2026-11-29, Azure DevOps' and GitLab's on
  2026-12-04. A gate runs the live command twice (D335), so 8b's gate needs all three made again
  first, as 7a's design 14.6 and 7b's design 15.8 say, set through a script that never shows a token
  and checked as CLAUDE.md's "What to know" says.

## Waiting on the owner

- **Acceptance of phase 8a.** The Gate commit is `750eb33` (State of the repo).
  [LEAD: the phase's state, and, once given, the acceptance's date and the merge commit.]
- **GitHub Pages** (D408, D451; ruling 64): at "Phase 8a accepted", before `master` is pushed, its
  source is set to "GitHub Actions", by the owner or by the lead through `gh api` at the owner's
  word. After the push the guide's run on `master` is read, and run again by `workflow_dispatch` if
  it ran before the setting; then `https://rahimpasha.github.io/VisualCommit/` is checked, and the
  `github-pages` environment's branch rule read: if it admits more than `master`, the owner's leave
  is asked to set it.
- **The live tokens**: GitHub's expires on 2026-11-29, Azure DevOps' and GitLab's on 2026-12-04;
  before 8b's gate the owner makes all three again, with an expiry that covers it.
- **A click that carries the frame** (ruling 107): after a click on the banner's framed Continue a
  mouse user sees the focus frame round the commit list until the focus moves. Should a click never
  carry the frame along, which changes ruling 102?
- **The dimmed texts**: answered on 2026-11-26, "Keep, as exceptions" (D469). For the owner to know:
  the question showed a dimmed row at rest; a selected dimmed row reads lower, and so do the row's
  labels, whose fill fades with their text (a branch's name on its lane's colour 1.26 to 1.51:1 in
  dark, 1.18 to 1.32:1 in light). The lead took both as part of the look kept; the labels' pins
  stand in a fact of their own, dropped at the owner's word.
- **Decisions made during 8a without the owner**, which the owner may overturn: the lead's 116
  rulings (design 13.1), above all those a decision amends; the design's D416 to D446 and its
  review's D447 to D452; D454 to D459, D461 to D468 and D470 to D473, among them D459 (the notice's
  comparison at 5 %, amending D33 for it), D461 and D467 (the restore order's proof taken again),
  D462 to D466 (the review before the gate), D472 (the gate's fix of the app, extending D227) and
  D473 (the gate's fix of the real-window harness's wait). The owner's
  own in 8a are D397 to D415 (D399's names and branches the lead's), D453, D460 and D469. The owner
  may also overrule D457's two Backlog rows (the reload after a commit or a fetch; Pull's settings
  reads, which a rested median of about 600 ms, or the owner, reopens).
- **For phase 8b, before its design** (plan.md's "Phase 8b", each with the research's
  recommendation): the installers per platform; the macOS token store (risk 34); the platforms,
  architectures and oldest systems; the version scheme; what proves "receive an update" and when the
  beta becomes 1.0; from which commit a public beta is built; the shape of the regression against
  the installed build; whether the opt-in install checks may install a test-labelled package in the
  owner's own Windows profile; what an uninstall removes; the channels for bug and security reports;
  and when a Mac is at hand for the macOS checklists. With them the Windows route, a month before
  the first stable release if it is to be Azure Artifact Signing, and the publisher name it shows
  (D402).
- **The machine**: Storage Sense is still on; with C: out of its low state it has no cause to run
  (ruling 111), and switching its temporary-file cleaning off is the owner's (the lead changes no
  Windows setting). D: had 3.3 GB free on 2026-11-27 after the counted round; which working folders
  outside the repo go is the owner's word (Known issues).
- **Answered during 8a**: the C: drive, "I'll free space" (2026-11-26); the week's share, "all of
  wave 3 now" (2026-11-21) and "All of wave 4 now" (2026-11-23; D241); the icon, Lens with round 2's
  masters and the outlined eye at the small sizes, blue `#4C5BE0` (2026-11-20; D407, D447); the
  look-alike check by eye, with no image search (2026-11-20); winget for a missing and a too-old git
  (D453); the Narrator listen at the gate (D460); the dimmed texts (D469).
- **Still open from earlier** (status.md's "Waiting on the owner" gives each): the review before
  phase 7's questions 1, 10 and 12 and its unnamed proposals; D240's connectors switch; Git
  Credential Manager's sign-in since git 2.56; the start reading by index (the review's P6); CI's
  duration when the project is over (D134); the macOS checklists of phases 0 to 7b, by hand on a
  Mac; the decisions of the earlier phases made without the owner; whether `wip/p7b-palette-growth`,
  the two `backup/*` branches and the working folders of accepted phases go; a look by hand at the
  trademark registers, domains and stores for the name, if wanted, which D412's search could not
  see.
