// Part A's expected results (docs/test-reports/phase-3.md, part A), copied from the report. Only
// part A's writer edits this file; its members live in Phase3Expectations.PartA. Compiled by the
// real-window pass too: see Phase3Expectations.Common.cs for what may be used here.
namespace VisualCommit.VisualTests.Phase3;

public static partial class Phase3Expectations
{
    /// <summary>What part A's checks (1 to 7, 5b, 23, 24, 26) compare against.</summary>
    public static class PartA
    {
        /// <summary>The ops scenario's row "Alpha two" (G0's row 4).</summary>
        public const int AlphaTwoRow = 4;

        /// <summary>Check 1 step 1: the commit menu of "Alpha two", as the report writes it (232 high).</summary>
        public static readonly string[] AlphaTwoCommitMenu =
        [
            "ce5ea6a Alpha two",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto main",
            "Revert this commit",
            "Reset main to this commit…",
            "—",
            "Copy SHA",
        ];
    }
}
