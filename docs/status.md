# Status

Last updated: 2026-10-06

Remote: `origin` is https://github.com/RahimPasha/VisualCommit.git. The local folder is still
named "Visual Git"; that has no effect on the build.

## Phases

| Phase | State | Branch | Handoff | Test report |
|---|---|---|---|---|
| 0. Foundation | In progress | `phase/0-foundation` | | [phase-0](test-reports/phase-0.md) |
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

Phase 0 — Foundation, on branch `phase/0-foundation`. Started 2026-10-06.

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

- [x] V1–V4. Shell in dark and light at 1100×700 and 1920×1080
- [x] V5. Clicking the theme switch changes the theme
- [x] V6. The chosen theme survives a restart
- [x] V7. Dragging a panel edge resizes the panel
- [x] V8. Real-window screenshots match the scripted ones

Closing steps (from `CLAUDE.md`):

- [x] C1. Run the visual test gate and complete the test report; every screenshot inspected
- [x] C2. Update `architecture.md` to what is built (including the colour tokens)
- [ ] C3. Update `plan.md`: move undelivered items, update risks
- [ ] C4. Write `docs/handoffs/phase-0.md`
- [ ] C5. Cold-read check with a fresh agent; fix the gaps it finds
- [ ] C6. CI green on all three platforms, read from GitHub
- [ ] C7. Update this file: links, state, anything waiting on the owner
- [ ] C8. Report to the owner

### Next step

Find the intermittent hang of the tests on macOS in CI (see Notes): read the next CI runs; the hang
watchdog now names the stuck test. Then closing steps C3 (`plan.md`), C4 (handoff) and C5
(cold-read check). The "Other platforms" section of the test report still has a placeholder to
fill in from the screenshots CI uploads.

### Notes for whoever resumes

- Environment: .NET SDK 10.0.303, Git 2.36.0.windows.1, `gh` signed in as RahimPasha. Packages:
  Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, xunit.v3 3.2.2 (pinned, see D24), FlaUI 5.0.0.
- The development screen is 3000×2000 at 200% scaling, so 1500×1000 logical pixels. A 1920×1080
  window does not fit; the real-window pass can only use 1100×700 on this machine.
- The gate ran on commit `cfa3193` and passed. The test report is filled in except "Other platforms".
- CI run 37436976773 (commit `9fd6555`) hung in the test step on macOS for over 7 minutes and was
  cancelled by the next push; its log could not be fetched. The runs before and after it passed on
  macOS in about 12 seconds. The cause is not known yet. `HangWatchdogAttribute`
  (`tests/VisualCommit.Testing`) was added to get the name of the stuck test the next time.
- A test that needs a second app instance must go through `FreshApplication.RunAsync`
  (`tests/VisualCommit.Testing/Headless`). Awaiting `HeadlessUnitTestSession.Dispatch` directly
  hangs the next test.
- Edit docs with the file tools, not with PowerShell text replacement: PowerShell 5.1 garbled the
  dashes and backticks in this file once already.
- The GitHub repo is private, so CI minutes are limited and macOS minutes count ten times. Pushes
  that change only docs do not start a CI run.

## Waiting on the owner

- The copyright line in `LICENSE` reads "VisualCommit contributors". Change it if the owner wants their own name there.
