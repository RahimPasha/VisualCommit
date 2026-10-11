// Part C's expected results (docs/test-reports/phase-3.md, part C: the scenarios as these checks
// use them, git's texts T1 to T7, the menus, dialogs and status texts), copied from the report.
// Only part C's writer edits this file; its members live in Phase3Expectations.PartC. Compiled by
// the real-window pass too: see Phase3Expectations.Common.cs for what may be used here.
namespace VisualCommit.VisualTests.Phase3;

public static partial class Phase3Expectations
{
    /// <summary>What part C's checks (13, 13b, 14, 14b, 15, 15b, 16, 25) compare against.</summary>
    public static class PartC
    {
        /// <summary>G14a, the blocked checkout scenario's commit rows from the top (below its working-changes row).</summary>
        public static readonly string[] G14aSubjects =
        [
            "Add newfile",
            "Clashing change",
            "Other change",
            "Add notes",
        ];
    }
}
