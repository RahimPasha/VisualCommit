using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>
/// The left panel of one repository tab: the filter and the sections Local branches, Remotes,
/// Pull requests, Tags and Stashes, as "What a repository tab must show" in
/// docs/test-reports/phase-1.md describes them. A tab without a repository uses an instance that
/// was never updated: five closed sections with the count 0.
/// </summary>
/// <remarks>
/// The panel is one flat list of rows (<see cref="Rows"/>) rebuilt from the last
/// <see cref="RepoRefs"/> whenever something changes. Which sections, remotes and folders are
/// open is kept by key, not on the rows, so it survives a rebuild and a refresh.
/// </remarks>
public partial class LeftPanelViewModel : ObservableObject
{
    public const string LocalBranchesTitle = "Local branches";
    public const string RemotesTitle = "Remotes";
    public const string PullRequestsTitle = "Pull requests";
    public const string TagsTitle = "Tags";
    public const string StashesTitle = "Stashes";

    /// <summary>Open (true) or closed (false), for every section, remote and folder the user opened or closed.</summary>
    private readonly Dictionary<string, bool> _toggled = new(StringComparer.Ordinal);

    /// <summary>
    /// Folders and remotes the user closed while the current filter text was in the box. A filter
    /// opens everything that holds a match; this lets the user still close a folder then, and is
    /// forgotten when the filter changes, so the panel without a filter is as it was before.
    /// </summary>
    private readonly HashSet<string> _closedWhileFiltering = new(StringComparer.Ordinal);

    private RepoRefs? _refs;
    private string? _selectedKey;

    /// <summary>What the graph hides and solos (docs/phase-5-design.md 8.3), which the rows mark; null while nothing is.</summary>
    private RepositoryGraphFilter? _filters;

    /// <summary>
    /// The last list the section showed, and of which repository: a failed or loading refresh of
    /// that repository keeps its rows (docs/phase-7a-design.md 10.3, D303; design 17 item 3), while
    /// the hosting's state holds no list then (7.8). Signed out, without a GitHub remote or for
    /// another repository it goes.
    /// </summary>
    private (HostedRepository Repository, PullRequestList List)? _keptList;

    public LeftPanelViewModel()
    {
        Rebuild();
    }

    /// <summary>The text in the filter box. Items whose full name contains it (ignoring case) stay.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>The rows the list shows, top to bottom: section headers, remotes, folders and refs.</summary>
    public RowCollection<RefListRow> Rows { get; } = [];

    /// <summary>
    /// Raised when the user activates (clicks) a branch, remote branch, tag or stash: the id of
    /// the commit it points to, which the graph then selects and scrolls into view.
    /// </summary>
    public event EventHandler<string>? RefActivated;

    /// <summary>
    /// Raised when the user clicks a branch the graph leaves out whose commit is no row
    /// (docs/phase-5-design.md 8.3, FD19): nothing is to be selected, whatever was before.
    /// </summary>
    public event EventHandler? NothingActivated;

    /// <summary>Raised when the eye button of a hidden branch's or remote's row is clicked: it is to be shown again (docs/phase-5-design.md 8.3).</summary>
    public event EventHandler<RefItemRow>? HiddenToggleClicked;

    /// <summary>Raised when the solo button of a soloed branch's row is clicked: its solo is to end (8.3).</summary>
    public event EventHandler<RefItemRow>? SoloToggleClicked;

    /// <summary>
    /// Whether a commit is a row of the graph shown, which a hidden branch's row asks (8.3): its
    /// click selects nothing and its name's tooltip says so when it is not. Null: every commit is.
    /// </summary>
    public Func<string, bool>? IsInGraph { get; set; }

    /// <summary>Shows what the graph hides and solos (8.3): the marks and toggles of the rows.</summary>
    public void UpdateFilters(RepositoryGraphFilter? filters)
    {
        _filters = filters;
        Rebuild();
    }

    /// <summary>The graph shown changed: a hidden branch's row asks again whether its commit is a row (8.3).</summary>
    public void RefreshMarks() => Rebuild();

    /// <summary>
    /// The repository's hosting as last applied (docs/phase-7a-design.md 1.7, 10): what the "Pull
    /// requests" section and the branch rows' pull requests show. <see cref="RepositoryHostingState.NotGitHub"/>
    /// until the first <see cref="UpdatePullRequests"/>, so a panel without hosting is phase 1's.
    /// </summary>
    public RepositoryHostingState HostingState { get; private set; } = RepositoryHostingState.NotGitHub;

    /// <summary>
    /// Shows the repository's hosting (docs/phase-7a-design.md 10.3; D303, D308): the "Pull
    /// requests" section's rows and the branch rows' <c>#n</c>. A clicked pull request no longer
    /// listed is no longer selected. A failure or a refresh of the repository whose list the section
    /// showed keeps that list's rows (design 17 item 3).
    /// </summary>
    public void UpdatePullRequests(RepositoryHostingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        HostingState = state;
        if (state.PullRequests is PullRequestsState.NotGitHub or PullRequestsState.SignedOut || state.Repository is null)
        {
            _keptList = null;
        }
        else if (state.List is { } list)
        {
            _keptList = (state.Repository, list);
        }
        else if (_keptList is { } kept && !kept.Repository.Equals(state.Repository))
        {
            _keptList = null;
        }

        if (_selectedKey is not null && _selectedKey.StartsWith(PullRequestRow.KeyPrefix, StringComparison.Ordinal) && PullRequestTargetOf(_selectedKey) is null)
        {
            _selectedKey = null;
        }

        Rebuild();
    }

    /// <summary>
    /// Shows <paramref name="refs"/>. Keeps the filter and which folders and sections the user
    /// opened or closed, so a refresh after an outside change does not undo them.
    /// </summary>
    public void Update(RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        _refs = refs;
        if (_selectedKey is not null && !ItemKeys(refs).Contains(_selectedKey) && PullRequestTargetOf(_selectedKey) is null)
        {
            // The ref the user clicked is gone (deleted, or a stash dropped).
            _selectedKey = null;
        }

        Rebuild();
    }

    /// <summary>
    /// What a click on <paramref name="row"/> does: a section header, remote or folder opens or
    /// closes; a branch, tag or stash becomes the selected item and raises
    /// <see cref="RefActivated"/> with its commit; a branch the graph leaves out whose commit is no
    /// row raises <see cref="NothingActivated"/> instead.
    /// </summary>
    public void Activate(RefListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        switch (row)
        {
            case RefSectionRow section:
                if (section.CanToggle)
                {
                    _toggled[section.Key] = !section.IsOpen;
                    Rebuild();
                }

                break;

            case RefItemRow { IsContainer: true } container:
                if (IsFiltering)
                {
                    if (!_closedWhileFiltering.Remove(container.Key))
                    {
                        _closedWhileFiltering.Add(container.Key);
                    }
                }
                else
                {
                    _toggled[container.Key] = !container.IsOpen;
                }

                Rebuild();
                break;

            case RefItemRow { TargetSha: { } sha } item:
                if ((item.Marks.IsHidden || item.Marks.IsDimmed) && IsInGraph?.Invoke(sha) == false)
                {
                    // A branch the filter leaves out whose commit is no row: nothing is selected
                    // and no row is highlighted (8.3, FD19).
                    ActivateNothing();
                    return;
                }

                MarkSelected(item.Key);
                OnRefActivated(sha);
                break;

            case PullRequestRow pullRequest:
                // Its head commit (D304); one that is no row of the graph, such as a fork's head not
                // fetched, selects nothing (FD19's rule).
                if (IsInGraph?.Invoke(pullRequest.TargetSha) == false)
                {
                    ActivateNothing();
                    return;
                }

                MarkSelected(pullRequest.Key);
                OnRefActivated(pullRequest.TargetSha);
                break;

            // A message row does nothing here: the view hands a click on the sign-in or the "more"
            // row to the repository (RepositoryViewModel.ActivatePullRequestMessage, 10.3).
        }
    }

    /// <summary>
    /// Follows the graph's selection (docs/phase-3-design.md 6.3, D98): the ref the user clicked
    /// keeps its selection background only while <paramref name="selectedSha"/>, the graph's
    /// single selected commit, is that ref's target. Any other selection (another row, the keys,
    /// a parent link, several rows or none: null) clears it. The graph's selection never
    /// highlights a ref by itself, because several refs can point at one commit.
    /// </summary>
    public void SyncSelection(string? selectedSha)
    {
        if (_selectedKey is null)
        {
            return;
        }

        if (selectedSha is not null && _refs is not null && TargetOf(_refs, _selectedKey) == selectedSha)
        {
            return;
        }

        MarkSelected(null);
    }

    /// <summary>Raises <see cref="RefActivated"/>.</summary>
    protected void OnRefActivated(string sha) => RefActivated?.Invoke(this, sha);

    /// <summary>
    /// The commit the ref, stash or pull request of <paramref name="key"/> points to (a pull
    /// request's head, D304), or null when it is gone.
    /// </summary>
    private string? TargetOf(RepoRefs refs, string key) =>
        refs.Refs.FirstOrDefault(gitRef => RefKey(gitRef) == key)?.TargetSha
        ?? refs.Stashes.FirstOrDefault(stash => StashKey(stash) == key)?.Sha
        ?? PullRequestTargetOf(key);

    /// <summary>The head commit of the listed pull request whose row has <paramref name="key"/>, or null.</summary>
    private string? PullRequestTargetOf(string key) =>
        ShownList?.PullRequests.FirstOrDefault(pullRequest => PullRequestRow.KeyOf(pullRequest.Number) == key)?.HeadSha;

    /// <summary>
    /// <paramref name="key"/>'s row becomes the one selected and every other row is unmarked; null
    /// selects none. The panel's selection is one key over branches, tags, stashes and pull requests.
    /// </summary>
    private void MarkSelected(string? key)
    {
        _selectedKey = key;
        foreach (var row in Rows)
        {
            switch (row)
            {
                case RefItemRow item:
                    item.IsSelected = key is not null && item.Key == key;
                    break;
                case PullRequestRow pullRequest:
                    pullRequest.IsSelected = key is not null && pullRequest.Key == key;
                    break;
            }
        }
    }

    /// <summary>A click that selects nothing (8.3, FD19): no row is marked and the graph's selection is emptied.</summary>
    private void ActivateNothing()
    {
        MarkSelected(null);
        NothingActivated?.Invoke(this, EventArgs.Empty);
    }

    partial void OnFilterTextChanged(string value)
    {
        _closedWhileFiltering.Clear();
        Rebuild();
    }

    private string Filter => FilterText?.Trim() ?? string.Empty;

    private bool IsFiltering => Filter.Length > 0;

    private bool Matches(string name) => name.Contains(Filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>A pull request matches when its <c>#n</c>, title, author or head label holds the filter, ignoring case (10.3).</summary>
    private bool Matches(PullRequest pullRequest) =>
        Matches("#" + pullRequest.Number.ToString(CultureInfo.InvariantCulture))
        || Matches(pullRequest.Title)
        || (pullRequest.Author is { } author && Matches(author))
        || Matches(PullRequestRow.HeadLabelOf(pullRequest));

    /// <summary>
    /// The list the section shows: the state's, or while a refresh of the same repository runs or
    /// after it failed, the one it showed last (<see cref="_keptList"/>); null when there is none.
    /// </summary>
    private PullRequestList? ShownList => HostingState.PullRequests switch
    {
        PullRequestsState.Loaded => HostingState.List,
        PullRequestsState.Loading or PullRequestsState.Failed => HostingState.List ?? _keptList?.List,
        _ => null,
    };

    private void Rebuild()
    {
        var rows = new List<RefListRow>();
        if (_refs is null)
        {
            // A tab without a repository: phase 0's five closed, empty sections.
            foreach (var (section, title) in SectionTitles)
            {
                rows.Add(new RefSectionRow(section, title, 0, isOpen: false, canToggle: false, Activate));
            }

            Rows.ReplaceAll(rows);
            return;
        }

        var refs = _refs;

        // Local branches.
        var local = refs.Refs.Where(r => r.Kind == RefKind.LocalBranch && Matches(r.Name)).ToList();
        if (AddSection(rows, RefSection.LocalBranches, LocalBranchesTitle, local.Count))
        {
            AddTree(rows, "local:", string.Empty, depth: 1, local.Select(r => new Leaf(r.Name, LeafFor(r))));
        }

        // Remotes, each with its branches in folders.
        var remoteBranches = refs.Refs.Where(r => r.Kind == RefKind.RemoteBranch && Matches(r.Name)).ToList();
        if (AddSection(rows, RefSection.Remotes, RemotesTitle, remoteBranches.Count))
        {
            var remotes = refs.Remotes
                .Concat(remoteBranches.Select(r => r.RemoteName).OfType<string>())
                .Distinct(StringComparer.Ordinal)
                .Order(NameOrder.Instance);
            foreach (var remote in remotes)
            {
                var branches = remoteBranches.Where(r => r.RemoteName == remote).ToList();
                if (IsFiltering && branches.Count == 0)
                {
                    continue;
                }

                var key = "remote:" + remote;
                var open = IsContainerOpen(key, defaultOpen: true);
                var hidden = _filters?.HiddenRemotes.Contains(remote, StringComparer.Ordinal) == true;
                var marks = hidden ? new RefRowMarks(IsHidden: true, IsSoloed: false, IsDimmed: true, NotInGraphToolTip: null) : null;
                rows.Add(new RefItemRow(RefItemKind.Remote, key, remote, remote, 1, Activate, isOpen: open, marks: marks, showHidden: OnHiddenToggleClicked));
                if (open)
                {
                    AddTree(rows, key + ":", remote + "/", depth: 2, branches.Select(r => new Leaf(
                        RemoteBranchNames.WithoutRemote(r.Name, remote),
                        LeafFor(r))));
                }
            }
        }

        // Pull requests: without a GitHub remote phase 1's placeholder, closed and empty (D251);
        // with one its states and rows (docs/phase-7a-design.md 10.3).
        AddPullRequests(rows);

        // Tags.
        var tags = refs.Refs.Where(r => r.Kind == RefKind.Tag && Matches(r.Name)).ToList();
        if (AddSection(rows, RefSection.Tags, TagsTitle, tags.Count))
        {
            AddTree(rows, "tag:", string.Empty, depth: 1, tags.Select(r => new Leaf(r.Name, LeafFor(r))));
        }

        // Stashes, newest first, as git lists them.
        var stashes = refs.Stashes.Where(s => Matches(s.Message)).ToList();
        if (AddSection(rows, RefSection.Stashes, StashesTitle, stashes.Count))
        {
            foreach (var stash in stashes)
            {
                rows.Add(Selected(new RefItemRow(
                    RefItemKind.Stash, StashKey(stash), stash.Message, stash.Message, 1, Activate, targetSha: stash.Sha)));
            }
        }

        Rows.ReplaceAll(rows, (shown, rebuilt) => shown.ShowsSameAs(rebuilt));
    }

    /// <summary>
    /// The "Pull requests" section (10.3, D303): the placeholder without a GitHub remote; else a
    /// header whose count is the matching pull requests', and while open the state's message row
    /// (signed out, loading without rows, a failure, none open) and the matching rows, newest first
    /// as GitHub lists them, then "more" when the list stopped at 300. Message rows show while
    /// filtering too.
    /// </summary>
    private void AddPullRequests(List<RefListRow> rows)
    {
        var state = HostingState;
        if (state.PullRequests == PullRequestsState.NotGitHub)
        {
            rows.Add(new RefSectionRow(RefSection.PullRequests, PullRequestsTitle, 0, isOpen: false, canToggle: false, Activate));
            return;
        }

        var list = ShownList;
        var matching = list?.PullRequests.Where(Matches).ToList() ?? [];
        if (!AddSection(rows, RefSection.PullRequests, PullRequestsTitle, matching.Count))
        {
            return;
        }

        switch (state.PullRequests)
        {
            case PullRequestsState.SignedOut:
                rows.Add(Message(PullRequestMessageKind.SignIn, OperationTexts.SignInToSeePullRequests));
                return;
            case PullRequestsState.Loading when list is null:
                rows.Add(Message(PullRequestMessageKind.Loading, OperationTexts.LoadingPullRequests));
                return;
            case PullRequestsState.Failed:
                // HostingTexts.Describe's words as they are (D338): the state holds no login, and a
                // 401 ends signed out (7.8); the tooltip adds GitHub's Detail on a second line.
                var failure = state.Failure is { } failed ? HostingTexts.Describe(failed, null) : string.Empty;
                var toolTip = state.Failure?.Detail is { Length: > 0 } detail ? failure + "\n" + detail : failure;
                rows.Add(Message(PullRequestMessageKind.Failed, failure, toolTip));
                break;
            case PullRequestsState.Loaded when list is { PullRequests.Count: 0 }:
                rows.Add(Message(PullRequestMessageKind.None, OperationTexts.NoOpenPullRequests));
                return;
        }

        foreach (var pullRequest in matching)
        {
            rows.Add(new PullRequestRow(pullRequest, Activate) { IsSelected = PullRequestRow.KeyOf(pullRequest.Number) == _selectedKey });
        }

        if (list?.MoreExist == true)
        {
            rows.Add(Message(PullRequestMessageKind.More, OperationTexts.MorePullRequests, OperationTexts.MenuTexts.OpenPullRequestsInBrowser));
        }
    }

    private PullRequestMessageRow Message(PullRequestMessageKind kind, string text, string? toolTip = null) =>
        new(kind, text, toolTip ?? text, Activate);

    /// <summary>Adds a section header and returns whether the section is open.</summary>
    private bool AddSection(List<RefListRow> rows, RefSection section, string title, int count)
    {
        var key = "section:" + section;
        var open = !_toggled.TryGetValue(key, out var toggled) || toggled;
        rows.Add(new RefSectionRow(section, title, count, open, canToggle: true, Activate));
        return open;
    }

    /// <summary>
    /// Adds refs as a tree: their names split at slashes into folders. In each folder the
    /// subfolders come first, then the refs, each sorted by name, as the report's left panel
    /// shows them (<see cref="RefTreeOrder"/>, which the command palette's local branches follow
    /// too: the tree is built from the refs in that order). A folder's tooltip is its path after
    /// <paramref name="namePrefix"/>: the name its refs' full names start with, such as
    /// <c>origin/feature</c>.
    /// </summary>
    private void AddTree(List<RefListRow> rows, string keyPrefix, string namePrefix, int depth, IEnumerable<Leaf> leaves)
    {
        var root = new Folder();
        foreach (var leaf in leaves.OrderBy(leaf => leaf.Path, RefTreeOrder.Instance))
        {
            var parts = leaf.Path.Split('/');
            var folder = root;
            folder.HoldsHead |= leaf.Row.IsHead;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (!folder.Folders.TryGetValue(parts[i], out var child))
                {
                    child = new Folder();
                    folder.Folders.Add(parts[i], child);
                }

                folder = child;
                folder.HoldsHead |= leaf.Row.IsHead;
            }

            folder.Leaves.Add((parts[^1], leaf.Row));
        }

        AddFolder(rows, root, keyPrefix, namePrefix, path: string.Empty, depth);
    }

    private void AddFolder(List<RefListRow> rows, Folder folder, string keyPrefix, string namePrefix, string path, int depth)
    {
        foreach (var (name, child) in folder.Folders)
        {
            var childPath = path.Length == 0 ? name : path + "/" + name;
            var key = keyPrefix + childPath;
            var open = IsContainerOpen(key, defaultOpen: child.HoldsHead);
            rows.Add(new RefItemRow(RefItemKind.Folder, key, name, namePrefix + childPath, depth, Activate, isOpen: open));
            if (open)
            {
                AddFolder(rows, child, keyPrefix, namePrefix, childPath, depth + 1);
            }
        }

        foreach (var (name, leaf) in folder.Leaves)
        {
            rows.Add(Selected(new RefItemRow(
                leaf.Kind, leaf.Key, name, leaf.FullName, depth, Activate,
                targetSha: leaf.Sha, isHead: leaf.IsHead, ahead: leaf.Ahead, behind: leaf.Behind,
                marks: MarksOf(leaf), showHidden: OnHiddenToggleClicked, stopSoloing: OnSoloToggleClicked,
                pullRequest: PullRequestOf(leaf))));
        }
    }

    /// <summary>
    /// Whether a remote or folder is open. Without a filter: as the user last left it, or else
    /// its default (remotes open; folders closed unless they hold the branch HEAD is on). With a
    /// filter, everything shown holds a match and is open unless closed during this filter.
    /// </summary>
    private bool IsContainerOpen(string key, bool defaultOpen)
    {
        if (IsFiltering)
        {
            return !_closedWhileFiltering.Contains(key);
        }

        return _toggled.TryGetValue(key, out var open) ? open : defaultOpen;
    }

    /// <summary>
    /// What the filter marks on a branch's row (8.3): hidden (the eye button, the disabled colour,
    /// and the tooltip when its commit is no row), soloed (the solo button), or, in solo mode, left
    /// out (the disabled colour) unless it is HEAD's branch, which is always shown. Tags keep their look.
    /// </summary>
    private RefRowMarks? MarksOf(LeafRow leaf)
    {
        if (_filters is not { } filters || leaf.Kind is not (RefItemKind.LocalBranch or RefItemKind.RemoteBranch) || !leaf.Key.StartsWith(RefKeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var fullName = leaf.Key[RefKeyPrefix.Length..];
        var hidden = filters.Hidden.Contains(fullName, StringComparer.Ordinal);
        var soloed = filters.Soloed.Contains(fullName, StringComparer.Ordinal);
        var leftOut = filters.Soloed.Count > 0 && !soloed;
        if (!hidden && !soloed && !leftOut)
        {
            return null;
        }

        var notInGraph = hidden && IsInGraph?.Invoke(leaf.Sha) == false ? $"{leaf.FullName} is hidden in the graph" : null;
        return new RefRowMarks(hidden, soloed, IsDimmed: (hidden || leftOut) && !leaf.IsHead, notInGraph);
    }

    /// <summary>
    /// The open pull request of a local or remote branch's row (D308): its full name in the
    /// hosting's <see cref="RepositoryHostingState.ByRef"/> (a local branch through an upstream of
    /// the same name, 7.8); none without a GitHub remote.
    /// </summary>
    private (int Number, string Title)? PullRequestOf(LeafRow leaf) =>
        leaf.Kind is RefItemKind.LocalBranch or RefItemKind.RemoteBranch
        && leaf.Key.StartsWith(RefKeyPrefix, StringComparison.Ordinal)
        && HostingState.ByRef.TryGetValue(leaf.Key[RefKeyPrefix.Length..], out var pullRequest)
            ? (pullRequest.Number, pullRequest.Title)
            : null;

    private void OnHiddenToggleClicked(RefItemRow row) => HiddenToggleClicked?.Invoke(this, row);

    private void OnSoloToggleClicked(RefItemRow row) => SoloToggleClicked?.Invoke(this, row);

    private RefItemRow Selected(RefItemRow row)
    {
        row.IsSelected = row.Key == _selectedKey;
        return row;
    }

    private static LeafRow LeafFor(GitRef gitRef) => new(
        gitRef.Kind switch
        {
            RefKind.LocalBranch => RefItemKind.LocalBranch,
            RefKind.RemoteBranch => RefItemKind.RemoteBranch,
            _ => RefItemKind.Tag,
        },
        RefKey(gitRef),
        gitRef.Name,
        gitRef.TargetSha,
        gitRef.IsHead,
        gitRef.Ahead,
        gitRef.Behind);

    private static string RefKey(GitRef gitRef) => "ref:" + gitRef.FullName;

    private static string StashKey(StashEntry stash) => "stash:" + stash.Sha;

    private static HashSet<string> ItemKeys(RepoRefs refs) =>
        refs.Refs.Select(RefKey).Concat(refs.Stashes.Select(StashKey)).ToHashSet(StringComparer.Ordinal);

    private static readonly (RefSection Section, string Title)[] SectionTitles =
    [
        (RefSection.LocalBranches, LocalBranchesTitle),
        (RefSection.Remotes, RemotesTitle),
        (RefSection.PullRequests, PullRequestsTitle),
        (RefSection.Tags, TagsTitle),
        (RefSection.Stashes, StashesTitle),
    ];

    /// <summary>A ref to place in a tree, by its path inside the tree.</summary>
    private sealed record Leaf(string Path, LeafRow Row);

    /// <summary>What a ref's row shows, apart from its position in the tree.</summary>
    private sealed record LeafRow(RefItemKind Kind, string Key, string FullName, string Sha, bool IsHead, int Ahead, int Behind);

    /// <summary>A folder of a tree, its subfolders and refs in the order they were added: <see cref="RefTreeOrder"/>'s.</summary>
    private sealed class Folder
    {
        public OrderedDictionary<string, Folder> Folders { get; } = new(StringComparer.Ordinal);

        public List<(string Name, LeafRow Row)> Leaves { get; } = [];

        public bool HoldsHead { get; set; }
    }
}
