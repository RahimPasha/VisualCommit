// What is where in phase 7b's scripted walk-through (docs/test-reports/phase-7b.md, docs/phase-7b-design.md 0.3, 0.5, 2.7):
// - Phase7bChecks.cs (this file): the abstract base class Phase7bChecks that every part derives from.
// - Phase7bChecks.Support.cs: what every check uses, copied from phase 7a's support file with 7b's
//   names: saving pictures (Shot, with 7b's part letters and the timings in
//   artifacts/visual/phase-7b/timings/), the menu, dialog, status, clipped-text, tooltip and
//   error-dialog assertions, the call assertions (presence and order only, from a long mark; a count
//   of git calls goes through GitCallBudget, ruling 28), and the start of a check on the scenario
//   "hosting7b" served by the three fakes on 47397 to 47399 (ServeAsync, OpenAsync, StartEmptyAsync).
//   Shared helpers live there only.
// - Phase7bChecks.<part>[.<topic>].cs (each part's package, written after its checks, D244): the
//   checks of the report's parts A (accounts and sign-in), B (browse and clone), P (requests in the
//   left panel and the graph) and N (a new request), in the classes Phase7bChecksA, Phase7bChecksB,
//   Phase7bChecksP and Phase7bChecksN, each deriving from Phase7bChecks and partial. One class per
//   part: xunit runs one class's tests one after another and different classes side by side. The
//   part letters avoid the requirement prefixes C, O, Q, R and T (D230, design 0.3).
// - Phase7bSupportTests.cs: the harness's safety nets and seams tried once, without pictures.
// - Phase7bExpectations.Common.cs and Phase7bExpectations.<part>.cs: what the checks compare against,
//   with PrepareRequestsAsync (design 2.7), also compiled by the real-window pass; their tests are
//   Phase7bExpectationTests.<part>.cs, which the pass's link pattern does not match.
// - The scenario: tests/VisualCommit.Testing/Scenarios.Phase7b.cs ("hosting7b") with its two halves,
//   Scenarios.Phase7b.AzureDevOps.cs and Scenarios.Phase7b.GitLab.cs; the fakes:
//   tests/VisualCommit.Testing/Hosting/FakeAzureDevOps*.cs, FakeGitLab*.cs and FakeGitHub*.cs on the
//   fakes' core (FakeHostingService*.cs) and AuthRemoteServer's host mode.
// - The driver: tests/VisualCommit.Testing/Headless/ShellDriver.Phase7b.cs (the new fakes, a sign-in to
//   any provider) beside ShellDriver.Phase7a*.cs; the keychain is FakeTokenStore.
// - The real-window pass: tests/VisualCommit.RealWindowTests/Phase7bRealWindowPass*.cs (WP-R), with
//   RealApp.Phase7b.cs.
//
// Conventions for the part files are phase 7a's: a test method per app start, named
// Check_<id>[<suffix>]_<what>; pictures only through Shot(app, "<check><step>-<what>"), with
// exactly the names of the report's Screenshots column; every wait on a condition, never a fixed time.
namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The scripted walk-through of phase 7b: the base of the classes that hold the checks of
/// docs/test-reports/phase-7b.md, one class per part (<c>Phase7bChecksA</c>, <c>Phase7bChecksB</c>,
/// <c>Phase7bChecksP</c>, <c>Phase7bChecksN</c>). Its helpers are in <c>Phase7bChecks.Support.cs</c>.
/// </summary>
public abstract partial class Phase7bChecks;
