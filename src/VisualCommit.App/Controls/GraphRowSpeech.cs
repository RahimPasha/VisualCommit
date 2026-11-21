using System.Globalization;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Git;

namespace VisualCommit.App.Controls;

/// <summary>
/// What a screen reader is told of the commit graph's selected row (D435): its name, which the
/// graph's one exposed row gives UI Automation, and its hint, the row's help text. Pure, so that
/// every word can be checked without a window; each word is a constant here.
/// </summary>
/// <remarks>
/// A commit's name is its subject, author and date as the Date column shows them, then each of
/// its labels in drawing order, those folded into "+N" too: the row a sighted user reads. The
/// short commit id goes into the hint, read after a pause, so that every move is not slowed by
/// seven spoken characters.
/// </remarks>
public static class GraphRowSpeech
{
    /// <summary>Between the parts of a name or a hint.</summary>
    public const string Separator = ", ";

    /// <summary>A commit whose subject is empty or blank.</summary>
    public const string NoMessage = "No message";

    /// <summary>A stash's row: <c>Stash: {subject}</c>, then its date and its labels.</summary>
    public const string StashFormat = "Stash: {0}";

    /// <summary>The branch HEAD is on.</summary>
    public const string CurrentBranchFormat = "current branch {0}";

    /// <summary>Any other local branch.</summary>
    public const string BranchFormat = "branch {0}";

    /// <summary>A remote branch.</summary>
    public const string RemoteBranchFormat = "remote branch {0}";

    /// <summary>A tag.</summary>
    public const string TagFormat = "tag {0}";

    /// <summary>HEAD's label while it is detached.</summary>
    public const string DetachedHead = "HEAD, detached";

    /// <summary>A label with an open pull or merge request: the label's words, then the request's in parentheses, as the label's tooltip line has them.</summary>
    public const string WithRequestFormat = "{0} ({1})";

    /// <summary>A request's words: <c>pull request #12: Add the login form</c>, with <see cref="DraftPrefix"/> before a draft.</summary>
    public const string RequestFormat = "{0}{1} {2}: {3}";

    /// <summary>Before a draft request's words.</summary>
    public const string DraftPrefix = "draft ";

    /// <summary>The working-changes row's own text when it has none of its own (the graph's default).</summary>
    public const string WorkingChanges = "Working changes";

    /// <summary>How the working-changes row's text joins its parts on screen ("Working changes · 1 conflict").</summary>
    public const string WorkingRowDot = " · ";

    /// <summary>The hint's start: the short commit id.</summary>
    public const string CommitFormat = "Commit {0}";

    /// <summary>The hint's count while several commits are selected, in the multi-selection panel's words.</summary>
    public const string SelectedCountFormat = "{0} commits selected";

    /// <summary>The hint's end while an active search marks the row.</summary>
    public const string MatchesSearch = "matches the search";

    /// <summary>
    /// A commit's or a stash's name: <c>{subject}, {author}, {date}</c> (a stash's
    /// <c>Stash: {subject}, {date}</c>), then <c>, {label}</c> for each label in drawing order.
    /// </summary>
    /// <param name="commit">The row's commit or stash.</param>
    /// <param name="labels">Every label of the row, in drawing order, those under "+N" too.</param>
    /// <param name="formatDate">The Date column's own format.</param>
    /// <param name="requestOf">A label's open request in words (<see cref="Request"/>), or null for none.</param>
    public static string Name(CommitInfo commit, IReadOnlyList<RefLabel> labels, Func<DateTimeOffset, string> formatDate, Func<RefLabel, string?>? requestOf = null)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(formatDate);

        var subject = string.IsNullOrWhiteSpace(commit.Subject) ? NoMessage : commit.Subject;
        var parts = new List<string>(labels.Count + 3);
        if (commit.Kind == CommitKind.Stash)
        {
            // A stash is the user's own work put aside: its author says nothing the user needs.
            parts.Add(Format(StashFormat, subject));
        }
        else
        {
            parts.Add(subject);
            parts.Add(commit.AuthorName);
        }

        parts.Add(formatDate(commit.AuthorDate));
        foreach (var label in labels)
        {
            var words = LabelWords(label);
            parts.Add(requestOf?.Invoke(label) is { Length: > 0 } request ? Format(WithRequestFormat, words, request) : words);
        }

        return string.Join(Separator, parts);
    }

    /// <summary>The working-changes row's name: its text, or <see cref="WorkingChanges"/>, with <see cref="WorkingRowDot"/> read as <see cref="Separator"/>.</summary>
    public static string WorkingRowName(string workingRowText) =>
        string.IsNullOrWhiteSpace(workingRowText)
            ? WorkingChanges
            : workingRowText.Replace(WorkingRowDot, Separator, StringComparison.Ordinal);

    /// <summary>
    /// The row's hint: <c>Commit {short SHA}</c>, then <c>, {n} commits selected</c> while two or
    /// more are, then <c>, matches the search</c> while an active search marks the row.
    /// </summary>
    public static string Hint(CommitInfo commit, int selectedCount, bool matchesSearch)
    {
        ArgumentNullException.ThrowIfNull(commit);
        var hint = Format(CommitFormat, commit.ShortSha);
        if (selectedCount >= 2)
        {
            hint += Separator + Format(SelectedCountFormat, selectedCount);
        }

        if (matchesSearch)
        {
            hint += Separator + MatchesSearch;
        }

        return hint;
    }

    /// <summary>
    /// A request's words, as the graph's label tooltip says them: <c>pull request #12: Add the
    /// login form</c>, <c>draft pull request #2: …</c>, GitLab's <c>merge request !21: …</c>.
    /// </summary>
    /// <param name="isDraft">The request is a draft.</param>
    /// <param name="request">The provider's word for it (<c>pull request</c>, <c>merge request</c>).</param>
    /// <param name="number">The provider's number (<c>#12</c>, <c>!21</c>).</param>
    /// <param name="title">Its title.</param>
    public static string Request(bool isDraft, string request, string number, string title) =>
        Format(RequestFormat, isDraft ? DraftPrefix : string.Empty, request, number, title);

    /// <summary>A label's words: its kind and its name, a stash's own text, a detached HEAD's <see cref="DetachedHead"/>.</summary>
    private static string LabelWords(RefLabel label) => label.Kind switch
    {
        RefLabelKind.DetachedHead => DetachedHead,
        RefLabelKind.CurrentBranch => Format(CurrentBranchFormat, label.Text),
        RefLabelKind.LocalBranch => Format(BranchFormat, label.Text),
        RefLabelKind.RemoteBranch => Format(RemoteBranchFormat, label.Text),
        RefLabelKind.Tag => Format(TagFormat, label.Text),
        _ => label.Text,
    };

    /// <summary>The invariant culture's digits, as the multi-selection panel writes its count.</summary>
    private static string Format(string format, params object[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
