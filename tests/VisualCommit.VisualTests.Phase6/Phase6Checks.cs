// What is where in phase 6's scripted walk-through (docs/test-reports/phase-6.md, docs/phase-6-design.md 17.3, 17.4):
// - Phase6Checks.cs (this file): the abstract base class Phase6Checks that every part derives from.
// - Phase6Checks.Support.cs: what every check uses, copied from phase 5's support file: the default
//   start on a scenario (with the system's theme, 17.2), saving pictures (with phase 6's part
//   letters, a name saved once per run, 11.8), and the assertions the checks repeat (menus,
//   dialogs, the status bar, "no menu", "no dialog", clipped text, the banner, the graph, the
//   backups, the git calls, the end of an operation). The dashboard's assertions,
//   OpenWorkspaceAsync and ShotInBothThemesAsync (17.2) join it with the driver parts they read.
// - Phase6Checks.<part>[.<topic>].cs (the report's writers, design 17.4 and work package WP-V): the
//   checks of the report's parts W (workspaces and the dashboard, W1 to W9), K (keys and the
//   palette, K10 to K13), S (settings and the theme's following, S14 to S21), G (ignored files and
//   refspecs, G22 to G25) and L (the look: the sweep, the long folder, the tab strip, L26 to L33),
//   in the classes Phase6ChecksW, Phase6ChecksK, Phase6ChecksS, Phase6ChecksG and Phase6ChecksL,
//   each deriving from Phase6Checks and partial. One class per part, because xunit runs the tests
//   of one class one after another and different classes side by side. The part letters avoid the
//   requirement prefixes C, O, Q, R and T; numbers run on across parts.
// - Phase6SupportTests.cs: the shared assertions tried once, the isolation's tripwire, and a check
//   that a scenario opens with the harness's per-session git environment, without pictures.
// - Phase6Expectations.Common.cs and Phase6Expectations.<part>.cs: what the checks compare
//   against, also compiled by the real-window pass. Phase6ExpectationTests.<part>.cs tests the
//   parts' helpers; the pass leaves it out.
// - The scenarios: tests/VisualCommit.Testing/Scenarios.Phase6.cs (the workspace "Web" in
//   TempWorkspace, "refspecs", "ignored", "settings"), pinned by
//   tests/VisualCommit.Git.Tests/Phase6ScenarioTests.cs, and phases 1, 3, 4 and 5's ("graph", "ops",
//   "auth", "conflicts", "history", "reflog"). The folder server: AuthRemoteServer.StartForFolderAsync
//   through Scenarios.ServeWorkspaceAsync, on port 47393; the fetch hold: FetchHold.
// - The driver: tests/VisualCommit.Testing/Headless/ShellDriver.Phase6.*.cs (design 17.2, as the
//   packages arrive), and the earlier phases' parts; the system's theme is FakeSystemTheme.
// - The real-window pass: tests/VisualCommit.RealWindowTests/Phase6RealWindowPass*.cs (WP-R), with
//   RealApp.Phase6*.cs.
//
// Conventions for the part files are phase 5's (Phase5Checks.cs): a test method per app start
// (a restart two), named Check_<id>[<suffix>]_<what>; pictures only through
// Shot(app, "<check><step>-<what>"), with exactly the names of the report's Screenshots column;
// every wait on a condition, never a fixed time.
namespace VisualCommit.VisualTests.Phase6;

/// <summary>
/// The scripted walk-through of phase 6: the base of the classes that hold the checks of
/// docs/test-reports/phase-6.md, one class per part (<c>Phase6ChecksW</c>, <c>Phase6ChecksK</c>,
/// <c>Phase6ChecksS</c>, <c>Phase6ChecksG</c>, <c>Phase6ChecksL</c>). Its members, shared by every
/// part, are in Phase6Checks.Support.cs.
/// </summary>
public abstract partial class Phase6Checks
{
}
