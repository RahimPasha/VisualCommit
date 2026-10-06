# Status

Last updated: 2026-10-06

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git. The local folder is still
named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | Awaiting acceptance | `phase/0-foundation` | [phase-0](handoffs/phase-0.md) | [phase-0](test-reports/phase-0.md) |
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

Phase 0 — Foundation, on branch `phase/0-foundation`. Started and closed on 2026-10-06. It is
complete and waits for the owner's acceptance; it is not merged into `master` yet.

### Progress

Delivers (from `plan.md`), split into steps:

- [x] 1. Visual-check expectations written to `docs/test-reports/phase-0.md` (before any UI exists)
- [x] 2. Repo files: `.gitignore`, `.gitattributes`, `.editorconfig`
- [x] 3. Solution: `VisualCommit.slnx`, `Directory.Build.props`, `Directory.Packages.props`, all projects created and building
- [x] 4. CI workflow for Windows, macOS and Linux; first run read back from GitHub
- [x] 5. Git runner: locate git and check its version, with tests
- [x] 6. Git runner: run asynchronously, stream output, cancel, record each call, with tests
- [x] 7. Data folder (`VISUALCOMMIT_DATA_DIR`) and JSON settings store, with tests
- [x] 8. Local log file, with tests
- [x] 9. Test harness: temporary-repo builder and a first scenario repo, with tests
- [x] 10. Theme colour tokens for dark and light
- [x] 11. App shell: main window with all regions and placeholder content
- [x] 12. Theme switch in the toolbar; the chosen theme is saved and restored
- [x] 13. Start-up: git detected and shown in the status bar; start-up written to the log
- [x] 14. Headless UI tests for the shell (`VisualCommit.App.Tests`)
- [x] 15. Scripted walk-through with screenshots (`VisualCommit.VisualTests`)
- [x] 16. Real-window pass harness (`VisualCommit.RealWindowTests`), proven on this machine
- [x] 17. README
- [x] 18. Commands section of `CLAUDE.md` filled in

Visual checks (details in the test report):

- [x] Checks 1–4. Shell in dark and light at 1100×700 and 1920×1080
- [x] Check 5. Clicking the theme switch changes the theme
- [x] Check 6. The chosen theme survives a restart
- [x] Check 7. Dragging a panel edge resizes the panel
- [x] Check 8. Real-window screenshots match the scripted ones

Closing steps (from `CLAUDE.md`, plus the CI result that the plan's "Done when" asks for):

- [x] Closing 1. Run the visual test gate and complete the test report; every screenshot inspected
- [x] Closing 2. Update `architecture.md` to what is built (including the colour tokens)
- [x] Closing 3. Update `plan.md`: move undelivered items, update risks
- [x] Closing 4. Write `docs/handoffs/phase-0.md`
- [x] Closing 5. Cold-read check with a fresh agent; fix the gaps it found
- [x] Closing 6. CI green on all three platforms, read from GitHub
- [x] Closing 7. Update this file: links, state, anything waiting on the owner
- [x] Closing 8. Report to the owner

### CI

The last CI run of the branch is [37446300297](https://github.com/RahimPasha/VisualCommit/actions/runs/37446300297),
for commit `2806273`: success on Windows, macOS and Linux. Commits after it changed only docs,
which do not start a run.

Earlier in the phase the test step froze the whole job on macOS in 4 of 9 runs. The freezes
stopped with commit `b907071`, which removed .NET's process-tree kill on macOS and Linux
(decision D37). Since then the test step has run 13 times on macOS without a freeze: twelve times
in run [37445085434](https://github.com/RahimPasha/VisualCommit/actions/runs/37445085434) and its
re-run, which repeated the step on purpose, and once in the last run. If a macOS job ever runs
for minutes in its Test step again, read "Known issues" in the handoff first.

### Next step

Wait for the owner. "Phase 0 accepted" means: merge `phase/0-foundation` into `master` with
`git merge --no-ff`, set phase 0 to "Done" here, commit, push `master`. After that the owner
starts phase 1 with "Start phase 1"; its first steps are in the handoff under "For the next phase".

### Notes for whoever resumes

- Nothing is in flight. The working tree is clean and everything is pushed.
- Environment: .NET SDK 10.0.303, Git 2.36.0.windows.1, `gh` signed in as RahimPasha. Packages:
  Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24), FlaUI 5.0.0.
- The development screen is 3000×2000 at 200% scaling: 1500×1000 logical pixels, of which
  1500×952 is the work area. A 1920×1080 window does not fit, so the real-window pass can only
  use 1100×700 on this machine.
- The visual gate ran three times, on `cfa3193`, `45f3e85` and `b907071`, because the app's code
  changed after the first run. The pictures were the same each time.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled
  the dashes and backticks in this file once already.
- The GitHub repo is private, so CI minutes are limited and macOS minutes count ten times. This
  phase used a good part of a month's free allowance, mostly on the frozen macOS jobs.

## Waiting on the owner

- **Accept phase 0**, or say what should change. Two things to know before deciding:
  - One part of the gate could not run: the real window at 1920×1080 does not fit the
    development screen. The plan asks for that size only when it fits, the scripted walk-through
    covers it in both themes, and the real window passes at 1100×700.
  - The macOS freeze in CI is stopped but not explained. The test step has been clean 13 times
    in a row since the fix; before it, 4 of 9 runs froze.
- The copyright line in `LICENSE` reads "VisualCommit contributors". Change it if you want your
  own name there.
- The repo is private, so CI runs on a monthly allowance of minutes (risk 8 in `plan.md`).
  Making it public removes the limit. No action is needed now, but check the remaining Actions
  minutes on GitHub before phase 1.
- When you have a Mac at hand: the eight-step checklist at the end of the test report.
