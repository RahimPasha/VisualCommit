// Part D's expected results (docs/test-reports/phase-3.md, part D: the scenario graphs sync, auth,
// ops and edges, git's texts, the dialogs and status texts), copied from the report. Texts that
// show the server's port are written with 47391; a check asserts them through
// ServerScenario.WithPort. Only part D's writer edits this file; its members live in
// Phase3Expectations.PartD. Compiled by the real-window pass too: see Phase3Expectations.Common.cs
// for what may be used here.
namespace VisualCommit.VisualTests.Phase3;

public static partial class Phase3Expectations
{
    /// <summary>What part D's checks (17 to 22, 27 to 31) compare against.</summary>
    public static class PartD
    {
        /// <summary>Check 17 step 1: the toolbar's buttons enabled on the auth scenario.</summary>
        public static readonly string[] EnabledOnAuth = ["FetchButton", "PullButton", "PushButton", "BranchButton"];
    }
}
