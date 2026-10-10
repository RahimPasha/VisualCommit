using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.Core.Git;

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
    /// Shows <paramref name="refs"/>. Keeps the filter and which folders and sections the user
    /// opened or closed, so a refresh after an outside change does not undo them.
    /// </summary>
    public void Update(RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        _refs = refs;
        if (_selectedKey is not null && !ItemKeys(refs).Contains(_selectedKey))
        {
            // The ref the user clicked is gone (deleted, or a stash dropped).
            _selectedKey = null;
        }

        Rebuild();
    }

    /// <summary>
    /// What a click on <paramref name="row"/> does: a section header, remote or folder opens or
    /// closes; a branch, tag or stash becomes the selected item and raises
    /// <see cref="RefActivated"/> with its commit.
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
                _selectedKey = item.Key;
                foreach (var other in Rows.OfType<RefItemRow>())
                {
                    other.IsSelected = other.Key == _selectedKey;
                }

                OnRefActivated(sha);
                break;
        }
    }

    /// <summary>Raises <see cref="RefActivated"/>.</summary>
    protected void OnRefActivated(string sha) => RefActivated?.Invoke(this, sha);

    partial void OnFilterTextChanged(string value)
    {
        _closedWhileFiltering.Clear();
        Rebuild();
    }

    private string Filter => FilterText?.Trim() ?? string.Empty;

    private bool IsFiltering => Filter.Length > 0;

    private bool Matches(string name) => name.Contains(Filter, StringComparison.OrdinalIgnoreCase);

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
                rows.Add(new RefItemRow(RefItemKind.Remote, key, remote, remote, 1, Activate, isOpen: open));
                if (open)
                {
                    var prefix = remote + "/";
                    AddTree(rows, key + ":", prefix, depth: 2, branches.Select(r => new Leaf(
                        r.Name.StartsWith(prefix, StringComparison.Ordinal) ? r.Name[prefix.Length..] : r.Name,
                        LeafFor(r))));
                }
            }
        }

        // Pull requests: closed and empty until phase 7.
        rows.Add(new RefSectionRow(RefSection.PullRequests, PullRequestsTitle, 0, isOpen: false, canToggle: false, Activate));

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
    /// shows them. A folder's tooltip is its path after <paramref name="namePrefix"/>: the
    /// name its refs' full names start with, such as <c>origin/feature</c>.
    /// </summary>
    private void AddTree(List<RefListRow> rows, string keyPrefix, string namePrefix, int depth, IEnumerable<Leaf> leaves)
    {
        var root = new Folder();
        foreach (var leaf in leaves)
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

        foreach (var (name, leaf) in folder.Leaves.OrderBy(entry => entry.Name, NameOrder.Instance))
        {
            rows.Add(Selected(new RefItemRow(
                leaf.Kind, leaf.Key, name, leaf.FullName, depth, Activate,
                targetSha: leaf.Sha, isHead: leaf.IsHead, ahead: leaf.Ahead, behind: leaf.Behind)));
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

    private sealed class Folder
    {
        public SortedDictionary<string, Folder> Folders { get; } = new(NameOrder.Instance);

        public List<(string Name, LeafRow Row)> Leaves { get; } = [];

        public bool HoldsHead { get; set; }
    }
}
