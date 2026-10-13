// What is where in phase 4's scripted walk-through (docs/test-reports/phase-4.md, design 13.2, 13.3):
// - Phase4Checks.cs (this file): the abstract base class Phase4Checks that every part derives from.
// - Phase4Checks.Support.cs: what every check uses: the default start on a scenario, saving
//   pictures, and the assertions the checks repeat (menus, dialogs, the status bar, "no menu",
//   "no dialog", clipped text), copied from phase 3's support file, and once what phase 3's parts
//   each wrote for themselves: the graph (AssertGraph), the backups (BackupsAsync), the git calls
//   (AssertCalled), the end of an operation (WaitForOperationAsync); the banner (AssertBanner)
//   knows Continue, Skip and design 5.2's layout.
// - Phase4Checks.A.cs to .D.cs (the report's writers, WP7): the checks of the report's parts A to
//   D, in the classes Phase4ChecksA to Phase4ChecksD, each deriving from Phase4Checks and partial
//   (a part may add files Phase4Checks.A.<topic>.cs of its own). One class per part, because xunit
//   runs the tests of one class one after another and different classes side by side.
// - Phase4SupportTests.cs: the shared assertions tried once on real states, without pictures.
// - Phase4Expectations.Common.cs and Phase4Expectations.A.cs to .D.cs: what the checks compare
//   against, also compiled by the real-window pass. Phase4ExpectationTests.cs (WP7) tests the
//   parts' helpers; the pass leaves it out.
// - The project: these files are VisualCommit.VisualTests.Phase4 (D151), apart from phases 0 to
//   2's walk-through (VisualCommit.VisualTests) and phase 3's (VisualCommit.VisualTests.Phase3): a
//   test assembly has one headless UI thread, and a project of its own is a test process with a UI
//   thread of its own, which "dotnet test" runs beside the others.
// - The driver: tests/VisualCommit.Testing/Headless/ShellDriver.Phase4.cs (the platform's undo
//   and redo, the banner's Continue and Skip, the result's caret held still), .Phase4.Rebase.cs
//   (WP5: the interactive list), .Phase4.Conflicts.cs (WP6: the resolver), and phase 3's parts.
// - The real-window pass: tests/VisualCommit.RealWindowTests/Phase4RealWindowPass*.cs (WP8), with
//   RealApp.Phase4*.cs.
//
// Conventions for the part files are phase 3's (Phase3Checks.Support.cs), with phase 4's report's
// "Conventions of every part": a test method per app start, named Check_<id>[<suffix>]_<what>;
// pictures only through Shot(app, "<check><step>-<what>"), with exactly the names of the report's
// Screenshots column; every wait on a condition, never a fixed time.
namespace VisualCommit.VisualTests.Phase4;

/// <summary>
/// The scripted walk-through of phase 4: the base of the classes that hold the checks of
/// docs/test-reports/phase-4.md, one class per part (<c>Phase4ChecksA</c> to <c>Phase4ChecksD</c>).
/// Its members, shared by every part, are in Phase4Checks.Support.cs.
/// </summary>
public abstract partial class Phase4Checks
{
}
