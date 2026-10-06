# Decisions

Decisions already made, with their reasons, so later sessions do not reopen them. Add new entries
at the bottom. To change one, add a new entry that replaces it; do not edit history.

| # | Date | Decision | Why |
|---|---|---|---|
| D1 | 2026-10-05 | UI is Avalonia, all C#, in one process. | The app must run on Windows, macOS and Linux from one codebase and look the same on each. The owner is stronger in C# than Angular. One process means no API layer between UI and git. It starts faster and uses less memory than a bundled browser. Rejected: Angular in WebView2 (Windows-only), Electron (heavy, two runtimes), OS web views (three browser engines to test), WPF (Windows-only), Blazor Hybrid (weak for a large virtualised graph). Accepted cost: the diff viewer and conflict resolver are hand-built on AvaloniaEdit. |
| D2 | 2026-10-05 | The git engine is the real `git` executable, not LibGit2Sharp. Minimum Git 2.30. | Full feature coverage (interactive rebase, worktrees, LFS, hooks, SSH, credential helpers) and behaviour identical to the command line. |
| D3 | 2026-10-05 | .NET 10. | Current long-term-support release; installed on the development machine. |
| D4 | 2026-10-05 | MVVM with CommunityToolkit.Mvvm. | Simpler than ReactiveUI and source-generated. |
| D5 | 2026-10-05 | The product is named Tracery. "Visual Git" is dropped. | The Git trademark policy restricts "Git" in product names. A search found no Git client named Tracery. Rejected: Switchyard and Branchline (Git clients with those names exist), Trellis and Ramus (crowded among developer tools). |
| D6 | 2026-10-05 | MIT licence. | Owner chose an open licence; MIT matches Avalonia and the other planned dependencies. |
| D7 | 2026-10-05 | Windows is developed and tested first. macOS and Linux run in CI; the owner tests macOS by hand later. | The development machine is Windows. The owner has a Mac but Mac testing is not the priority now. |
| D8 | 2026-10-05 | Scope is as listed in `requirements.md`. Rebase and conflict resolution get their own phase (4). | The owner accepted the suggested picks and singled out rebase and conflict solving as important. |
| D9 | 2026-10-05 | Hosting providers are GitHub, Azure DevOps and GitLab. No Bitbucket. | Owner's choice. |
| D10 | 2026-10-05 | No account, login or telemetry. Everything stays local. | A deliberate improvement over GitKraken. |
| D11 | 2026-10-05 | Every phase ends with a visual test gate: screenshots taken and each one inspected. | Owner requirement, stated as very important. The owner left the cadence open; a gate after every phase was chosen because each phase builds on the last and late bugs cost more. |
| D12 | 2026-10-05 | Visual test reports are committed; screenshots stay local in a git-ignored folder. | Keeps the public repo small. |
| D13 | 2026-10-05 | Each phase is built on its own branch and merged into `master` after the owner accepts it. Nothing is pushed. | The owner asked for work to be committed and will push to their own GitHub account themselves. |
| D14 | 2026-10-05 | One phase per session. All context passes through the repo: `CLAUDE.md`, `status.md`, `architecture.md`, `decisions.md` and a handoff per phase. | The project does not fit in one session, and a new session remembers nothing from earlier ones. |
| D15 | 2026-10-05 | Expected results for visual checks are written and committed before the UI they test is built. | Otherwise a session could write expectations to match whatever it built, and the gate would prove nothing. |
| D16 | 2026-10-05 | The real-window pass uses an in-repo harness on Windows UI Automation (FlaUI). | It must not depend on the tools a particular session happens to have. Phase 0 proves it works; a fallback is agreed with the owner if it does not. |
| D17 | 2026-10-05 | CI is reported as unverified until the owner pushes the repo. | Sessions never push, and the repo has no remote yet. |
| D18 | 2026-10-05 | Accepted phases are merged with `git merge --no-ff`. | Each phase stays visible as one unit in the history. |
| D19 | 2026-10-05 | The real-window pass is its own project (`Tracery.RealWindowTests`): Windows-only, opt-in, never in the default test run or CI. Tests always use a temporary data folder (`TRACERY_DATA_DIR`). | Every session runs the default tests at start-up. They must not take over the mouse, fail on macOS or Linux, or overwrite the user's real settings. |
| D20 | 2026-10-05 | The main window follows the layout table in `requirements.md`: tabs, toolbar, left panel, graph, right panel, status bar. Proposed by the planning session; the owner has not reviewed it yet. | Visual checks need a written layout to be compared against. It follows the familiar GitKraken arrangement, with Tracery's own styling. |
