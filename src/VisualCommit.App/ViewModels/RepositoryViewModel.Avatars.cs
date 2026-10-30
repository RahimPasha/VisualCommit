// The authors' avatars of a repository tab (docs/phase-7a-design.md 11.3; D252, D257, D310, D311):
// the tab tells its hosting the authors of the commits that a GitHub remote's branch reaches, each
// with the newest such commit and that remote (D339), and gives the avatars it gets back to the
// details panel; the graph's view logs them once drawn (D322).
// The only override of OnPropertyChanged in RepositoryViewModel (1.7).

using System.ComponentModel;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels;

public sealed partial class RepositoryViewModel
{
    /// <summary>The graph's rows whose authors may be told (D311): the first 2,000, one pass on the UI thread.</summary>
    public const int AvatarRows = 2000;

    /// <summary>The most authors told at once (D311), newest first.</summary>
    public const int AvatarAuthors = 200;

    // On the UI thread: the loading graph followed until it is whole or has AvatarRows rows, and the
    // set last given to the hosting, forgotten signed out or without a GitHub remote.
    private CommitGraphData? _followedGraph;
    private IReadOnlyList<AuthorCommit>? _authorsSent;

    /// <summary>
    /// The graph's view drew this tab's avatars (<c>CommitGraphControl.AvatarsApplied</c>, on the UI
    /// thread): the Debug line <c>{repo}: showing GitHub avatars for {n} authors</c> (1.9, D322), with
    /// <paramref name="count"/> the authors whose image decoded.
    /// </summary>
    public void NoteAvatarsShown(int count) =>
        _log.Debug(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Name}: showing GitHub avatars for {count} authors"));

    /// <summary>
    /// The graph, the hosting's state and the avatars (11.3): a new graph is followed while it loads
    /// and the authors told once it is whole or has <see cref="AvatarRows"/> rows; a new state tells
    /// them again (signed out or without a GitHub remote, the set last sent is forgotten); new avatars
    /// go to the details panel.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(Graph):
                FollowGraph(Graph);
                UpdateAuthors();
                break;
            case nameof(HostingState):
                if (HostingState.PullRequests is PullRequestsState.SignedOut or PullRequestsState.NotGitHub)
                {
                    _authorsSent = null;
                }

                UpdateAuthors();
                break;
            case nameof(Avatars):
                // Raised by the constructor's set-up before Details exists only in a view model
                // that has none yet: then there is nothing to give it to.
                if (Details is { } details)
                {
                    details.Avatars = Avatars;
                }

                break;
        }
    }

    private void FollowGraph(CommitGraphData graph)
    {
        if (_followedGraph is { } followed)
        {
            followed.Changed -= OnFollowedGraphChanged;
        }

        _followedGraph = !graph.IsComplete && graph.Count < AvatarRows ? graph : null;
        if (_followedGraph is { } loading)
        {
            loading.Changed += OnFollowedGraphChanged;
        }
    }

    /// <summary>Rows were added to the loading graph, on the UI thread: once it is whole or has enough rows, the authors are told and the graph no longer followed.</summary>
    private void OnFollowedGraphChanged(object? sender, EventArgs e)
    {
        if (sender is not CommitGraphData graph || !ReferenceEquals(graph, _followedGraph) || (!graph.IsComplete && graph.Count < AvatarRows))
        {
            return;
        }

        graph.Changed -= OnFollowedGraphChanged;
        _followedGraph = null;
        UpdateAuthors();
    }

    /// <summary>
    /// Tells the hosting the authors of the graph's commits on a GitHub remote's branches (D311), when
    /// their e-mails differ from those last sent (none before any): never in summary mode or without an account's listing
    /// (D257: only <c>Loading</c>, <c>Failed</c> or <c>Loaded</c>), nor while the graph still loads
    /// its first <see cref="AvatarRows"/> rows. An author's newer commit alone is not news: the one
    /// sent before still finds the author on GitHub, unless GitHub could not answer that lookup (its
    /// remote unreadable, its branch rewritten); that author waits for the next change of the e-mails (D339).
    /// </summary>
    private void UpdateAuthors()
    {
        if (IsSummary || _disposed || HostingState.PullRequests is not (PullRequestsState.Loading or PullRequestsState.Failed or PullRequestsState.Loaded))
        {
            return;
        }

        var graph = Graph;
        if (!graph.IsComplete && graph.Count < AvatarRows)
        {
            return;
        }

        // Nothing sent counts as the empty set: the placeholder graph a tab starts with, or a history
        // no GitHub branch reaches, tells the hosting nothing (an empty set would only replace a set
        // the hosting has waiting).
        var authors = AuthorsOnGitHub(graph, HostingState.Remotes);
        if ((_authorsSent ?? []).Select(author => author.Email).SequenceEqual(authors.Select(author => author.Email), StringComparer.Ordinal))
        {
            return;
        }

        _authorsSent = authors;
        Hosting.ShowAvatarsFor(authors);
    }

    /// <summary>
    /// The distinct author e-mails, lower-case (invariant), newest first and at most
    /// <see cref="AvatarAuthors"/>, of those of the first <see cref="AvatarRows"/> rows that a
    /// remote-tracking branch of a GitHub remote (a key of <paramref name="remotes"/>) reaches, each
    /// with its newest such commit and the repository of the remote that reaches it, the first in
    /// <see cref="HostingRemoteOrder"/> when several do (D339). One pass in row order: the branches'
    /// tips start the set and a row in it adds its parents, with its remote, since the graph's order
    /// shows no parent before its children. The address of a commit that is only local, or only on
    /// another remote, is never in it (D311).
    /// </summary>
    public static IReadOnlyList<AuthorCommit> AuthorsOnGitHub(CommitGraphData graph, IReadOnlyDictionary<string, HostedRepository> remotes)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(remotes);

        // Each reached SHA with the place, in the remotes' order, of the first remote that reaches it.
        var order = HostingRemoteOrder.Of(remotes.Keys);
        var places = order.Select((remote, place) => (remote, place)).ToDictionary(pair => pair.remote, pair => pair.place, StringComparer.Ordinal);
        var reached = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var gitRef in graph.Refs.Refs)
        {
            if (gitRef.Kind == RefKind.RemoteBranch && gitRef.RemoteName is { } remote && places.TryGetValue(remote, out var place))
            {
                Reach(reached, gitRef.TargetSha, place);
            }
        }

        var authors = new List<AuthorCommit>();
        if (reached.Count == 0)
        {
            return authors;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = Math.Min(graph.Count, AvatarRows);
        for (var row = 0; row < rows && authors.Count < AvatarAuthors; row++)
        {
            var commit = graph.CommitAt(row);
            if (!reached.TryGetValue(commit.Sha, out var by))
            {
                continue;
            }

            foreach (var parent in commit.Parents)
            {
                Reach(reached, parent, by);
            }

            var email = commit.AuthorEmail.ToLowerInvariant();
            if (email.Length > 0 && seen.Add(email))
            {
                authors.Add(new AuthorCommit(email, commit.Sha, remotes[order[by]]));
            }
        }

        return authors;
    }

    /// <summary>Marks <paramref name="sha"/> reached by the remote at <paramref name="place"/>, unless one before it already reaches it.</summary>
    private static void Reach(Dictionary<string, int> reached, string sha, int place)
    {
        if (!reached.TryGetValue(sha, out var held) || place < held)
        {
            reached[sha] = place;
        }
    }
}
