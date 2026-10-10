using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels.Panels;

/// <summary>The five sections of the left panel, in the order the panel shows them.</summary>
public enum RefSection
{
    LocalBranches,
    Remotes,
    PullRequests,
    Tags,
    Stashes,
}

/// <summary>What a row of the left panel below a section header stands for.</summary>
public enum RefItemKind
{
    /// <summary>A remote: its branches are inside it.</summary>
    Remote,

    /// <summary>A folder: the part of branch or tag names before a slash, such as <c>feature</c> in <c>feature/login</c>.</summary>
    Folder,

    LocalBranch,
    RemoteBranch,
    Tag,
    Stash,
}

/// <summary>
/// One row of the left panel's list. The panel shows its sections, folders and refs as one flat,
/// virtualised list of these rows, rebuilt whenever what is open or what matches the filter
/// changes, so that a repository with thousands of tags costs only the rows in view.
/// </summary>
public abstract class RefListRow : ObservableObject
{
    /// <summary>Where the chevron of a row at depth 0 (a section header) starts.</summary>
    public const double FirstIndent = 10;

    /// <summary>How much further each level of the tree is indented (the report: 14 per level).</summary>
    public const double IndentPerLevel = 14;

    private protected RefListRow(string key, string text, int depth, bool isOpen, Action<RefListRow> activate, bool canActivate = true)
    {
        Key = key;
        Text = text;
        Depth = depth;
        IsOpen = isOpen;
        ActivateCommand = new RelayCommand(() => activate(this), () => canActivate);
    }

    /// <summary>
    /// Names the row across rebuilds and refreshes: the section, the folder's path or the ref's
    /// full name. The panel keeps which rows are open and which one is selected by this key.
    /// </summary>
    public string Key { get; }

    /// <summary>What the row shows: a section's title, a folder's or remote's name, a ref's last name part or a stash's message.</summary>
    public string Text { get; }

    /// <summary>The level in the tree: 0 for a section header, 1 for what is directly inside a section.</summary>
    public int Depth { get; }

    /// <summary>The space before the row's chevron, in logical pixels.</summary>
    public double Indent => FirstIndent + (IndentPerLevel * Depth);

    /// <summary>For a section, remote or folder: its content is shown below it.</summary>
    public bool IsOpen { get; }

    /// <summary>What a click on the row does: open or close it, or select the commit a ref points to.</summary>
    public IRelayCommand ActivateCommand { get; }
}

/// <summary>A section header: a chevron, the title and the number of items that match the filter.</summary>
public sealed class RefSectionRow : RefListRow
{
    internal RefSectionRow(RefSection section, string title, int count, bool isOpen, bool canToggle, Action<RefListRow> activate)
        : base("section:" + section, title, 0, isOpen, activate, canActivate: canToggle)
    {
        Section = section;
        Count = count;
        CanToggle = canToggle;
    }

    public RefSection Section { get; }

    /// <summary>Matching local branches, remote branches, tags or stashes; always 0 for pull requests until phase 7.</summary>
    public int Count { get; }

    public string CountText => Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A click opens or closes the section. False for "Pull requests", which stays closed until
    /// phase 7, and for every section of a panel without a repository.
    /// </summary>
    public bool CanToggle { get; }
}

/// <summary>A remote, a folder, a branch, a tag or a stash under a section header.</summary>
public sealed partial class RefItemRow : RefListRow
{
    internal RefItemRow(
        RefItemKind kind,
        string key,
        string text,
        string toolTip,
        int depth,
        Action<RefListRow> activate,
        bool isOpen = false,
        string? targetSha = null,
        bool isHead = false,
        int ahead = 0,
        int behind = 0)
        : base(key, text, depth, isOpen, activate)
    {
        Kind = kind;
        ToolTip = toolTip;
        TargetSha = targetSha;
        IsHead = isHead;
        Ahead = ahead;
        Behind = behind;
    }

    public RefItemKind Kind { get; }

    /// <summary>
    /// The whole name, for the tooltip, since the row cuts a long one off with "…": the full
    /// branch or tag name (<c>feature/search</c>, <c>origin/feature/search</c>), the folder's
    /// path, the remote's name or the stash's message.
    /// </summary>
    public string ToolTip { get; }

    /// <summary>A remote or a folder: it has a chevron and opens and closes.</summary>
    public bool IsContainer => Kind is RefItemKind.Remote or RefItemKind.Folder;

    /// <summary>The chevron points down: an open remote or folder.</summary>
    public bool ShowsOpenChevron => IsContainer && IsOpen;

    /// <summary>The chevron points right: a closed remote or folder.</summary>
    public bool ShowsClosedChevron => IsContainer && !IsOpen;

    /// <summary>The commit a branch, tag or stash points to; null for a remote or a folder.</summary>
    public string? TargetSha { get; }

    /// <summary>The local branch HEAD is on: its name is drawn in the accent colour.</summary>
    public bool IsHead { get; }

    /// <summary>Commits on a local branch that its upstream lacks; 0 without an upstream.</summary>
    public int Ahead { get; }

    /// <summary>Commits on the upstream that the local branch lacks; 0 without an upstream.</summary>
    public int Behind { get; }

    public bool ShowsAhead => Ahead > 0;

    public bool ShowsBehind => Behind > 0;

    public bool ShowsAheadBehind => ShowsAhead || ShowsBehind;

    public string AheadText => Ahead.ToString(CultureInfo.InvariantCulture);

    public string BehindText => Behind.ToString(CultureInfo.InvariantCulture);

    /// <summary>The ref the user clicked last: drawn with the selection background.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
