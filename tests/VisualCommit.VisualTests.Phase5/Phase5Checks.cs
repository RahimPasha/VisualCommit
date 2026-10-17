// What is where in phase 5's scripted walk-through (docs/test-reports/phase-5.md, docs/phase-5-design.md 15.3, 15.4):
// - Phase5Checks.cs (this file): the abstract base class Phase5Checks that every part derives from.
// - Phase5Checks.Support.cs: what every check uses, copied from phase 4's support file: the default
//   start on a scenario, saving pictures (with phase 5's part letters), and the assertions the
//   checks repeat (menus, dialogs, the status bar, "no menu", "no dialog", clipped text, the
//   banner, the graph, the backups, the git calls, the end of an operation).
// - Phase5Checks.<part>[.<topic>].cs (the report's writers, design 16's step 6 and WP8): the
//   checks of the report's parts A (the shell and the activity log), B (the pages and "All
//   files"), F (finding: search, compare, solo and hide, dashed stashes) and P (protected changes,
//   C12), in the classes Phase5ChecksA, Phase5ChecksB, Phase5ChecksF and Phase5ChecksP, each
//   deriving from Phase5Checks and partial. One class per part, because xunit runs the tests of
//   one class one after another and different classes side by side.
// - Phase5SupportTests.cs: the shared assertions tried once, and a check that the history
//   scenario opens, without pictures.
// - Phase5Expectations.Common.cs and Phase5Expectations.<part>.cs: what the checks compare
//   against, also compiled by the real-window pass. Phase5ExpectationTests.<part>.cs tests the
//   parts' helpers; the pass leaves it out.
// - The projects (D181): these files are VisualCommit.VisualTests.Phase5. Part U, the undo and redo
//   checks, is VisualCommit.VisualTests.Phase5Undo (Phase5ChecksU, Phase5Expectations.U*.cs),
//   which compiles this file, the support and the common expectations from here: the undo checks
//   alone are about 100 operations, and a test assembly runs its headless tests one at a time on
//   its one UI thread, so each project is a test process with a UI thread of its own, which
//   "dotnet test" runs beside the others. Both save their pictures in artifacts/visual/phase-5/
//   scripted/, which VisualRun empties once per run of both.
// - The scenarios: tests/VisualCommit.Testing/Scenarios.Phase5.cs (history, filters, reflog,
//   protect), pinned by tests/VisualCommit.Git.Tests/Phase5ScenarioTests.cs, and phases 3's and 4's.
// - The driver: tests/VisualCommit.Testing/Headless/ShellDriver.Phase5*.cs (design 15.2, as the
//   views arrive), and the earlier phases' parts.
// - The real-window pass: tests/VisualCommit.RealWindowTests/Phase5RealWindowPass*.cs (WP9), with
//   RealApp.Phase5*.cs.
//
// Conventions for the part files are phase 4's (Phase4Checks.cs), with phase 5's report's: a test
// method per app start, named Check_<id>[<suffix>]_<what>; every U check several test methods, one
// action with its undo and redo each, on a fresh copy, so that none comes near the hang watchdog's
// 3 minutes (design 15.4); pictures only through Shot(app, "<check><step>-<what>"), with exactly
// the names of the report's Screenshots column; every wait on a condition, never a fixed time.
namespace VisualCommit.VisualTests.Phase5;

/// <summary>
/// The scripted walk-through of phase 5: the base of the classes that hold the checks of
/// docs/test-reports/phase-5.md, one class per part (<c>Phase5ChecksA</c>, <c>Phase5ChecksB</c>,
/// <c>Phase5ChecksF</c>, <c>Phase5ChecksP</c>, and the undo project's <c>Phase5ChecksU</c>). Its
/// members, shared by every part, are in Phase5Checks.Support.cs.
/// </summary>
public abstract partial class Phase5Checks
{
}
