namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The labels and disabled reasons of phase 5's compare, hide and solo items and of the
/// comparison's file menu (docs/phase-5-design.md 6.2, 6.4, 8.3, 12.4, 13.1), which
/// <c>RepositoryMenus.Filters.cs</c> builds its items from. The comparison's file menu has the
/// file menus' own texts.
/// </summary>
public static partial class OperationTexts
{
    public static partial class MenuTexts
    {
        // Compare (6.2).

        /// <summary><c>Compare {b} with {cur}</c>, <c>Compare {r}/{b} with {cur}</c>, <c>Compare {t} with {cur}</c>; a drop's <c>Compare {A} with {B}</c>.</summary>
        public static string CompareWith(string name, string other) => $"Compare {name} with {other}";

        /// <summary>12.4: nothing to compare with while HEAD has no commit.</summary>
        public const string HeadIsUnborn = "HEAD is unborn";

        // Hide and solo (8.1, 8.3).

        public static string HideLabel(string name) => $"Hide {name}";

        public static string ShowLabel(string name) => $"Show {name}";

        public static string SoloLabel(string name) => $"Solo {name}";

        public static string StopSoloingLabel(string name) => $"Stop soloing {name}";

        /// <summary>12.4: <c>Hide {b}</c> on the checked-out branch.</summary>
        public const string CheckedOutBranchAlwaysShown = "The checked-out branch is always shown";

        /// <summary>The Local branches and Remotes sections' item (8.3).</summary>
        public const string ShowAllHiddenBranches = "Show all hidden branches";

        /// <summary>12.4: <see cref="ShowAllHiddenBranches"/> with nothing hidden.</summary>
        public const string NoBranchIsHidden = "No branch is hidden";

        /// <summary>The Local branches and Remotes sections' item (8.3).</summary>
        public const string StopSoloing = "Stop soloing";

        /// <summary>12.4: <see cref="StopSoloing"/> with nothing soloed.</summary>
        public const string NoBranchIsSoloed = "No branch is soloed";

        /// <summary>A remote's item (8.1): every branch of the remote, now and later.</summary>
        public static string HideEveryBranchOf(string remote) => $"Hide every branch of {remote}";

        public static string ShowEveryBranchOf(string remote) => $"Show every branch of {remote}";
    }
}
