namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of the remote operations (docs/phase-3-design.md 2.16 to 2.23, 2.29, 4f) that
/// <c>OperationTexts.cs</c> does not hold: the labels of the dialogs' confirm buttons the remote
/// operations open. Built by WP7-remotes.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>The confirm button of the remote choice dialog "Push &lt;t&gt; to…" (2.29, 4f).</summary>
    public const string PushTagChoiceButton = "Push";

    /// <summary>
    /// The confirm button of the remote choice dialog "Delete &lt;t&gt; on…" (2.29, 4f), in the
    /// danger style; the confirmation of 2.29 follows it.
    /// </summary>
    public const string DeleteRemoteTagChoiceButton = "Delete";

    /// <summary>
    /// The line of git's output that names a remote a fetch of all remotes could not fetch
    /// (<c>error: Could not fetch origin</c>, as <c>git fetch --all</c> prints it, D109).
    /// </summary>
    public const string CouldNotFetchPrefix = "error: Could not fetch ";

    /// <summary>The end of the sentence pull's integration step fails with for a setting it does not know (2.18).</summary>
    public const string SettingNotUnderstoodEnd = ", which this app does not understand.";

    /// <summary>
    /// "Pull failed"'s summary when <c>pull.ff=only</c> refused a pull of diverged branches
    /// (docs/phase-6-design.md 4.8, D211): git's <c>git merge --ff-only …</c> said "Not possible to
    /// fast-forward". <paramref name="upstream"/> as the menus name it (D114), echoing their
    /// "Merge {up} into {b}" and "Rebase {b} onto {up}":
    /// "main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main."
    /// </summary>
    public static string PullNotFastForward(string branch, string upstream) =>
        $"{branch} and {upstream} have diverged, and pull.ff is set to only: merge {upstream} into {branch} or rebase {branch} onto {upstream}.";
}
