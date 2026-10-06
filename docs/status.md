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

Phase 0 â€” Foundation, on branch `phase/0-foundation`. Started 2026-10-06.

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
- [ ] 17. README
- [ ] 18. Commands section of `CLAUDE.md` filled in

Visual checks (details in the test report):

- [ ] V1â€“V4. Shell in dark and light at 1100Ã—700 and 1920Ã—1080
- [ ] V5. Clicking the theme switch changes the theme
- [ ] V6. The chosen theme survives a restart
- [ ] V7. Dragging a panel edge resizes the panel
- [ ] V8. Real-window screenshots match the scripted ones

Closing steps (from `CLAUDE.md`):

- [ ] C1. Run the visual test gate and complete the test report; every screenshot inspected
- [ ] C2. Update `architecture.md` to what is built (including the colour tokens)
- [ ] C3. Update `plan.md`: move undelivered items, update risks
- [ ] C4. Write `docs/handoffs/phase-0.md`
- [ ] C5. Cold-read check with a fresh agent; fix the gaps it finds
- [ ] C6. CI green on all three platforms, read from GitHub
- [ ] C7. Update this file: links, state, anything waiting on the owner
- [ ] C8. Report to the owner

### Next step

Steps 17 and 18: write the README and fill in the Commands section of `CLAUDE.md`; record the
decisions made this phase in `decisions.md` and add `THIRD-PARTY-NOTICES.md`. Then the closing steps,
starting with the visual test gate (C1).

### Notes for whoever resumes

- Environment found at the start of the phase: .NET SDK 10.0.303, Git 2.36.0.windows.1, `gh` signed
  in as RahimPasha. Latest stable packages on NuGet: Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2,
  xunit.v3 4.0.1, FlaUI 5.0.0. `Avalonia.Headless.XUnit` 12.1.3 is built against xunit.v3 3.2.2.
- The development screen is 3000Ã—2000 at 200% scaling, so 1500Ã—1000 logical pixels. A 1920Ã—1080
  window does not fit; the real-window pass can only use 1100Ã—700 on this machine.
- The code for steps 5 to 9 is written (src/VisualCommit.Git, src/VisualCommit.App/Services,
  	ests/VisualCommit.Testing) but has no tests of its own yet; the steps stay unticked until it has.
- Tests run on Microsoft Testing Platform (global.json), so a single project is run with
  dotnet test --project <path>. xunit.v3 is pinned to 3.2.2: Avalonia.Headless.XUnit 12.1.3 fails
  to discover tests with xunit.v3 4.x.
- A test that needs a second app instance must go through FreshApplication.RunAsync`n  (	ests/VisualCommit.Testing/Headless). Awaiting HeadlessUnitTestSession.Dispatch directly hangs
  the next test.
- The real-window pass is `dotnet test --project tests/VisualCommit.RealWindowTests -c Release`. It
  needs the scripted screenshots, so run `dotnet test` at the repo root first. It takes about 30
  seconds and moves the real mouse.
- CI was green on all three platforms for commit 66f08a4 (run 37435533667). Pushes that change
  only docs do not start a CI run.
- The GitHub repo is private. The `gh` token has the scopes `repo`, `gist` and `read:org` but not
  `workflow`; git itself pushes through Git Credential Manager. If pushing the CI workflow file is
  refused, the owner has to grant the `workflow` scope.

## Waiting on the owner

- The copyright line in `LICENSE` reads "VisualCommit contributors". Change it if the owner wants their own name there.
