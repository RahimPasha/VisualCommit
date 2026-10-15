using System.Globalization;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// C12's texts (docs/phase-5-design.md 9, 12.1, 12.2; D153, D179; the report's part P): the
/// refusal message of a checkout and of the operations that refuse for the same reason, with
/// git's count (D153's sentence, the singular for one, D113), the ignored-files text, and the
/// automatic stash's message, running text, result and failures. Built by work package WP4.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>9.2's running text while the automatic stash stashes; then the checkout's own (<see cref="CheckOut"/>).</summary>
    public const string StashingYourChanges = "Stashing your changes…";

    /// <summary>The last sentence of a checkout refused because HEAD moved while it waited (9.2 step 1, D124).</summary>
    public const string NothingCheckedOut = "Nothing was checked out.";

    /// <summary>
    /// D153 (1), 9.3: a checkout refused because changes are in the way, with the number of paths
    /// git named (<see cref="Core.Operations.GitFailure.BlockedPaths"/>): "1 conflict prevents
    /// checkout. Commit, stash, or discard your changes first." / "3 conflicts prevent checkout. …".
    /// </summary>
    public static string ConflictsPreventCheckout(int conflicts) =>
        $"{ConflictsPrevent(conflicts)} checkout. Commit, stash, or discard your changes first.";

    /// <summary>
    /// 9.3's second table: a merge, cherry-pick or revert (<paramref name="operation"/>: "merge",
    /// "cherry-pick", "revert") refused because changes are in the way of the files it writes:
    /// "1 conflict prevents the merge. Commit, stash, or discard your changes first."
    /// </summary>
    public static string ConflictsPrevent(int conflicts, string operation) =>
        $"{ConflictsPrevent(conflicts)} the {operation}. Commit, stash, or discard your changes first.";

    /// <summary>9.3: a merge, cherry-pick or revert refused because something is staged: "Your staged changes prevent the merge. Commit, stash, or unstage them first."</summary>
    public static string StagedChangesPrevent(string operation) =>
        $"Your staged changes prevent the {operation}. Commit, stash, or unstage them first.";

    /// <summary>
    /// 9.3: a stash's apply or pop refused by the app's own check before git, counting the stash's
    /// paths that have a local change and its untracked paths that exist: "1 conflict prevents
    /// applying the stash. Commit, stash, or discard your changes first."
    /// </summary>
    public static string ConflictsPreventApplyingStash(int conflicts) =>
        $"{ConflictsPrevent(conflicts)} applying the stash. Commit, stash, or discard your changes first.";

    /// <summary>
    /// 9.3: a checkout refused only because ignored files are in the way (after the automatic stash,
    /// or with <c>--no-overwrite-ignore</c>); <paramref name="name"/> is the target's short SHA or
    /// tag name (PD9): "1 ignored file is in the way: 73e0bab tracks a file of the same name. Move
    /// it away first." / "2 ignored files are in the way: 73e0bab tracks files of the same names.
    /// Move them away first."
    /// </summary>
    public static string IgnoredFilesInTheWay(int count, string name) =>
        count == 1
            ? $"1 ignored file is in the way: {name} tracks a file of the same name. Move it away first."
            : string.Create(CultureInfo.InvariantCulture, $"{count} ignored files are in the way: {name} tracks files of the same names. Move them away first.");

    /// <summary>The automatic stash's message (9.2, D153 (2)): "Auto stash before checking out f39ea2d"; git lists it as "On main: …".</summary>
    public static string AutoStashMessage(string name) => $"Auto stash before checking out {name}";

    /// <summary>9.2 step 6, the status bar and its tooltip after the automatic stash's checkout.</summary>
    public static string ChangesInAutoStash(string name) => $"Your changes are in the stash \"{AutoStashMessage(name)}\".";

    /// <summary>
    /// 9.2 step 3, "Checkout failed": git saved the stash but could not clear the working tree (a
    /// file another program held), and every untracked file it had deleted was put back.
    /// </summary>
    public static string AutoStashNotCleared(string name) =>
        $"Git could not clear your changes from the working tree, so nothing was checked out. They are also saved in the stash \"{AutoStashMessage(name)}\".";

    /// <summary>9.2 step 3, the same when a file could not be put back.</summary>
    public static string AutoStashPartlyCleared(string name) =>
        $"Your changes are saved in the stash \"{AutoStashMessage(name)}\". Git could not finish clearing the working tree, so some of them are still there too. "
        + "Nothing was checked out. Keep the stash until you have checked your files.";

    /// <summary>9.2 step 5, "Checkout failed": the checkout failed after the automatic stash, and popping it back failed too (phase 3's 2.31 sentence, naming this stash).</summary>
    public static string AutoStashKept(string name) =>
        $"Git could not check out {name}, and your changes could not be re-applied. They are kept in the stash \"{AutoStashMessage(name)}\".";

    /// <summary>"1 conflict prevents", "3 conflicts prevent" (D113).</summary>
    private static string ConflictsPrevent(int conflicts) =>
        conflicts == 1 ? "1 conflict prevents" : string.Create(CultureInfo.InvariantCulture, $"{conflicts} conflicts prevent");
}
