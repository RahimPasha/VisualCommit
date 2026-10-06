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

- [ ] 1. Visual-check expectations written to `docs/test-reports/phase-0.md` (before any UI exists)
- [ ] 2. Repo files: `.gitignore`, `.gitattributes`, `.editorconfig`
- [ ] 3. Solution: `VisualCommit.slnx`, `Directory.Build.props`, `Directory.Packages.props`, all projects created and building
- [ ] 4. CI workflow for Windows, macOS and Linux; first run read back from GitHub
- [ ] 5. Git runner: locate git and check its version, with tests
- [ ] 6. Git runner: run asynchronously, stream output, cancel, record each call, with tests
- [ ] 7. Data folder (`VISUALCOMMIT_DATA_DIR`) and JSON settings store, with tests
- [ ] 8. Local log file, with tests
- [ ] 9. Test harness: temporary-repo builder and a first scenario repo, with tests
- [ ] 10. Theme colour tokens for dark and light
- [ ] 11. App shell: main window with all regions and placeholder content
- [ ] 12. Theme switch in the toolbar; the chosen theme is saved and restored
- [ ] 13. Start-up: git detected and shown in the status bar; start-up written to the log
- [ ] 14. Headless UI tests for the shell (`VisualCommit.App.Tests`)
- [ ] 15. Scripted walk-through with screenshots (`VisualCommit.VisualTests`)
- [ ] 16. Real-window pass harness (`VisualCommit.RealWindowTests`), proven on this machine
- [ ] 17. README
- [ ] 18. Commands section of `CLAUDE.md` filled in

Visual checks (details in the test report):

- [ ] V1–V4. Shell in dark and light at 1100×700 and 1920×1080
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

Step 1: write the visual-check expectations into `docs/test-reports/phase-0.md` and commit them.

### Notes for whoever resumes

- Environment found at the start of the phase: .NET SDK 10.0.303, Git 2.36.0.windows.1, `gh` signed
  in as RahimPasha. Latest stable packages on NuGet: Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2,
  xunit.v3 4.0.1, FlaUI 5.0.0. `Avalonia.Headless.XUnit` 12.1.3 is built against xunit.v3 3.2.2.
- The development screen is 3000×2000 at 200% scaling, so 1500×1000 logical pixels. A 1920×1080
  window does not fit; the real-window pass can only use 1100×700 on this machine.
- The GitHub repo is private. The `gh` token has the scopes `repo`, `gist` and `read:org` but not
  `workflow`; git itself pushes through Git Credential Manager. If pushing the CI workflow file is
  refused, the owner has to grant the `workflow` scope.

## Waiting on the owner

- The copyright line in `LICENSE` reads "VisualCommit contributors". Change it if the owner wants their own name there.
