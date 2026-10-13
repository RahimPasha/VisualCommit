namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// What a rebase item of a menu rebases onto (docs/phase-4-design.md 1.1, D124, D142): the
/// target's SHA as it was read when the menu opened, and how the texts name it. An operation acts
/// on what the user saw: auto-fetch may move a remote branch while the operation waits.
/// </summary>
/// <param name="Sha">The full SHA git rebases onto: a commit's, or a branch's tip as the menu's refs had it.</param>
/// <param name="Name">How the texts name the target (<c>{target}</c>, section 10): a local branch's short name, a remote branch's <c>&lt;r&gt;/&lt;b&gt;</c>, or a commit's short SHA.</param>
public sealed record RebaseTarget(string Sha, string Name);
