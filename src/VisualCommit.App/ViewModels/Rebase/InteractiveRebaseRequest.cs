using VisualCommit.App.ViewModels.Operations;

namespace VisualCommit.App.ViewModels.Rebase;

/// <summary>
/// What an "Interactive rebase…" item of a menu asks for (docs/phase-4-design.md 2.1): the list
/// opens for it in the graph area (2.2), reads its commits once (2.3), and Start rebases them
/// onto <see cref="Onto"/>'s SHA (2.4, D124).
/// </summary>
/// <param name="Onto">
/// The target as the menu read it: a local or remote branch's tip with its name, or for "from
/// here" the base commit, named by its short SHA.
/// </param>
/// <param name="BranchRef">The full name of the branch whose commits are listed and rebased (<c>refs/heads/&lt;b&gt;</c>): HEAD's branch, or a drop's source.</param>
/// <param name="FromHere">
/// "Interactive rebase of {cur} from here…": the list holds <c>&lt;onto&gt;..HEAD</c> on HEAD's
/// linear first-parent line, and its header says <c>from {short}</c>.
/// </param>
/// <param name="CheckoutFirst">
/// A drop's source that is not checked out ("Check out {A} and rebase it onto {B}
/// interactively…"): Start checks it out first, after 1.3's dirty-tree check.
/// </param>
public sealed record InteractiveRebaseRequest(RebaseTarget Onto, string BranchRef, bool FromHere, bool CheckoutFirst);
