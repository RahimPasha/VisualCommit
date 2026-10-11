// Part B's expected results (docs/test-reports/phase-3.md, part B: the graphs G0 to G12, E0 and
// E1, the commits the checks make, the menus and texts), copied from the report. Only part B's
// writer edits this file; its members live in Phase3Expectations.PartB. Compiled by the real-window
// pass too: see Phase3Expectations.Common.cs for what may be used here.
namespace VisualCommit.VisualTests.Phase3;

public static partial class Phase3Expectations
{
    /// <summary>What part B's checks (8, 9, 9b, 10, 11, 11b, 12, 32) compare against.</summary>
    public static class PartB
    {
        /// <summary>G0, the ops scenario as built: the rows' messages from the top.</summary>
        public static readonly string[] G0Subjects =
        [
            "On main: Experiment",
            "Conflicting change",
            "Fast-forward me",
            "Main work",
            "Alpha two",
            "Alpha one",
            "Add core",
            "Initial commit",
        ];
    }
}
