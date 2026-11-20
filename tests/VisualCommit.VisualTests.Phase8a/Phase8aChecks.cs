// What is where in phase 8a's scripted walk-through (docs/test-reports/phase-8a.md, docs/phase-8a-design.md 0.3, 0.5, 1.15):
// - Phase8aChecks.cs (this file): the abstract base class Phase8aChecks that every part derives from.
// - Phase8aChecks.Support.cs: what every check uses, copied from phase 7b's support file with 8a's
//   names: saving pictures (Shot, with 8a's part letters and the timings in
//   artifacts/visual/phase-8a/timings/), the menu, dialog, status, clipped-text, tooltip and
//   error-dialog assertions, the call assertions (presence and order only, from a long mark; a count
//   of git calls goes through GitCallBudget), and the start of a check on an empty tab or on one
//   repository, in a theme, with a probe for git of the check's own (StartEmptyAsync, OpenAsync).
//   Shared helpers live there only.
// - Phase8aChecks.<part>[.<topic>].cs (each part's package, written after its checks, D244): the
//   checks of the report's parts L (the contrast round), I (one instance per data folder), F (first
//   run), K (keyboard), B (the icon), A (About and the notices), U (screen readers) and G (the
//   guide's links), in the classes Phase8aChecksL, Phase8aChecksI and so on, each deriving from
//   Phase8aChecks and partial. One class per part: xunit runs one class's tests one after another
//   and different classes side by side. The part letters avoid the requirement prefixes C, O, Q, R
//   and T (D230, design 0.3).
// - Phase8aSupportTests.cs: the harness's seams tried once, without pictures.
// - Phase8aExpectations.Common.cs and Phase8aExpectations.<part>.cs: what the checks compare
//   against, also compiled by the real-window pass, so they use nothing of this project; their tests
//   are Phase8aExpectationTests.<part>.cs, which the pass's link pattern does not match.
// - The driver: tests/VisualCommit.Testing/Headless/ShellDriver.cs (its starts take a probe for git,
//   the helper's path and the identity, TestIdentity.Pinned when none), and each package's helpers
//   in a partial ShellDriver.Phase8a.<Key>.cs of its own.
// - The real-window pass: tests/VisualCommit.RealWindowTests/Phase8aRealWindowPass*.cs, with
//   RealApp.Phase8a.cs.
//
// Conventions for the part files are phase 7b's: a test method per app start, named
// Check_<id>[<suffix>]_<what>; pictures only through Shot(app, "<check><step>-<what>"), with
// exactly the names of the report's Screenshots column; every wait on a condition, never a fixed time.
namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The scripted walk-through of phase 8a: the base of the classes that hold the checks of
/// docs/test-reports/phase-8a.md, one class per part (<c>Phase8aChecksL</c>, <c>Phase8aChecksI</c>,
/// <c>Phase8aChecksF</c>, <c>Phase8aChecksK</c>, <c>Phase8aChecksB</c>, <c>Phase8aChecksA</c>,
/// <c>Phase8aChecksU</c>, <c>Phase8aChecksG</c>). Its helpers are in <c>Phase8aChecks.Support.cs</c>.
/// </summary>
public abstract partial class Phase8aChecks;
