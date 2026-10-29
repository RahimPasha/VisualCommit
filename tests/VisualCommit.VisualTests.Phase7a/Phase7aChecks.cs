// What is where in phase 7a's scripted walk-through (docs/test-reports/phase-7a.md, docs/phase-7a-design.md 0.5, 2.8):
// - Phase7aChecks.cs (this file): the abstract base class Phase7aChecks that every part derives from.
// - Phase7aChecks.Support.cs: what every check uses, copied from phase 6's support file with 7a's
//   names: saving pictures (Shot, with 7a's part letters and the timings in
//   artifacts/visual/phase-7a/timings/), the menu, dialog, status, clipped-text and tooltip
//   assertions, the call assertions (presence and order only, from a long mark; a count of git
//   calls goes through GitCallBudget, design 5.3, ruling 28), and the start of a check on the
//   scenario "hosting" served on 47395 (ServeAsync, OpenAsync). Shared helpers live there only.
// - Phase7aChecks.<part>[.<topic>].cs (each part's package, written after its checks, D244): the
//   checks of the report's parts A (accounts and sign-in), B (browse and clone), P (pull requests),
//   G (the graph's badge and avatars) and N (a new pull request), in the classes Phase7aChecksA,
//   Phase7aChecksB, Phase7aChecksP, Phase7aChecksG and Phase7aChecksN, each deriving from
//   Phase7aChecks and partial. One class per part: xunit runs one class's tests one after another
//   and different classes side by side. The part letters avoid the requirement prefixes C, O, Q, R
//   and T (design 0.3).
// - Phase7aSupportTests.cs: the harness's safety nets and seams tried once, without pictures.
// - Phase7aExpectations.Common.cs and Phase7aExpectations.<part>.cs: what the checks compare against,
//   with each part's PrepareAsync (design 2.6), also compiled by the real-window pass.
// - The scenario: tests/VisualCommit.Testing/Scenarios.Phase7a.cs ("hosting"), pinned by
//   tests/VisualCommit.Git.Tests/Phase7aScenarioTests.cs; the fake of GitHub:
//   tests/VisualCommit.Testing/Hosting/FakeGitHub*.cs on AuthRemoteServer's host mode, port 47395.
// - The driver: tests/VisualCommit.Testing/Headless/ShellDriver.Phase7a.cs (the hosting seams), and
//   the earlier phases' parts; the keychain is FakeTokenStore.
// - The real-window pass: tests/VisualCommit.RealWindowTests/Phase7aRealWindowPass*.cs (WP-R), with
//   RealApp.Phase7a.cs.
//
// Conventions for the part files are phase 6's: a test method per app start, named
// Check_<id>[<suffix>]_<what>; pictures only through Shot(app, "<check><step>-<what>"), with
// exactly the names of the report's Screenshots column; every wait on a condition, never a fixed time.
namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The scripted walk-through of phase 7a: the base of the classes that hold the checks of
/// docs/test-reports/phase-7a.md, one class per part (<c>Phase7aChecksA</c>, <c>Phase7aChecksB</c>,
/// <c>Phase7aChecksP</c>, <c>Phase7aChecksG</c>, <c>Phase7aChecksN</c>). Its helpers are in
/// <c>Phase7aChecks.Support.cs</c>.
/// </summary>
public abstract partial class Phase7aChecks;
