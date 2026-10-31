using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Operations;

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

    /// <summary>
    /// Whether <paramref name="other"/>, a row of a later rebuild, shows exactly what this row
    /// shows, so the list can keep this row instead of drawing it again.
    /// </summary>
    public abstract bool ShowsSameAs(RefListRow other);
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

    /// <summary>
    /// Matching local branches, remote branches, tags, stashes or pull requests; 0 for the "Pull
    /// requests" placeholder of a repository without a GitHub remote (docs/phase-7a-design.md 10.3).
    /// </summary>
    public int Count { get; }

    public string CountText => Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A click opens or closes the section. False for the "Pull requests" placeholder of a
    /// repository without a GitHub remote, which stays closed (D251), and for every section of a
    /// panel without a repository.
    /// </summary>
    public bool CanToggle { get; }

    public override bool ShowsSameAs(RefListRow other) =>
        other is RefSectionRow section
        && section.Key == Key && section.Text == Text && section.IsOpen == IsOpen
        && section.Count == Count && section.CanToggle == CanToggle;
}

/// <summary>
/// What the graph's filter marks on a left-panel row (docs/phase-5-design.md 8.3): a hidden branch
/// or remote (its name in the disabled colour, the eye button), a soloed branch (the solo button),
/// a branch that solo mode leaves out (its name in the disabled colour), and the name's tooltip
/// when a hidden branch's commit is no row of the graph.
/// </summary>
/// <param name="IsHidden">The branch is hidden, or the remote hidden as a whole: the eye button shows it again.</param>
/// <param name="IsSoloed">The branch is soloed: the solo button ends its solo.</param>
/// <param name="IsDimmed">The name is drawn in the disabled colour: hidden, or neither soloed nor HEAD's in solo mode.</param>
/// <param name="NotInGraphToolTip">The name's tooltip in place of the full name: <c>{b} is hidden in the graph</c>; null otherwise.</param>
public sealed record RefRowMarks(bool IsHidden, bool IsSoloed, bool IsDimmed, string? NotInGraphToolTip)
{
    /// <summary>No filter mark: the row as before phase 5.</summary>
    public static RefRowMarks None { get; } = new(false, false, false, null);
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
        int behind = 0,
        RefRowMarks? marks = null,
        Action<RefItemRow>? showHidden = null,
        Action<RefItemRow>? stopSoloing = null,
        (int Number, string Title)? pullRequest = null)
        : base(key, text, depth, isOpen, activate)
    {
        Kind = kind;
        ToolTip = toolTip;
        TargetSha = targetSha;
        IsHead = isHead;
        Ahead = ahead;
        Behind = behind;
        Marks = marks ?? RefRowMarks.None;
        ShowHiddenCommand = new RelayCommand(() => showHidden?.Invoke(this), () => showHidden is not null);
        StopSoloingCommand = new RelayCommand(() => stopSoloing?.Invoke(this), () => stopSoloing is not null);
        if (pullRequest is { } open)
        {
            PullRequestText = "#" + open.Number.ToString(CultureInfo.InvariantCulture);
            PullRequestToolTip = OperationTexts.PullRequestOfBranch(open.Number, open.Title);
        }
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

    /// <summary>What the graph's filter marks on the row (docs/phase-5-design.md 8.3).</summary>
    public RefRowMarks Marks { get; }

    /// <summary>The eye button (<c>RefHiddenToggle</c>) shows: a hidden branch, or a remote hidden as a whole.</summary>
    public bool ShowsHiddenToggle => Marks.IsHidden;

    /// <summary>The solo button (<c>RefSoloToggle</c>) shows: a soloed branch.</summary>
    public bool ShowsSoloToggle => Marks.IsSoloed;

    /// <summary>The name is drawn in the disabled colour (8.3).</summary>
    public bool IsDimmed => Marks.IsDimmed;

    /// <summary>The name's tooltip: the whole name, or <c>{b} is hidden in the graph</c> for a hidden branch whose commit is no row (8.3).</summary>
    public string NameToolTip => Marks.NotInGraphToolTip ?? ToolTip;

    /// <summary>The eye button: shows the branch (with the upstream hidden with it) or the remote's branches again (8.3).</summary>
    public IRelayCommand ShowHiddenCommand { get; }

    /// <summary>The solo button: ends the branch's solo (8.3).</summary>
    public IRelayCommand StopSoloingCommand { get; }

    /// <summary>
    /// The branch's open pull request (docs/phase-7a-design.md 10.3, D308): <c>#1</c> at the row's
    /// right before its counts and toggles; empty without one, and for every row of a repository
    /// without a GitHub remote.
    /// </summary>
    public string PullRequestText { get; } = string.Empty;

    /// <summary>The <c>#1</c>'s tooltip: <c>Pull request #1: Add the login form</c>; null without one.</summary>
    public string? PullRequestToolTip { get; }

    /// <summary>The <c>#1</c> shows (<c>RefPullRequest</c>); collapsed, it takes no room.</summary>
    public bool ShowsPullRequest => PullRequestText.Length > 0;

    public override bool ShowsSameAs(RefListRow other) =>
        other is RefItemRow item
        && item.Key == Key && item.Kind == Kind && item.Text == Text && item.ToolTip == ToolTip
        && item.Depth == Depth && item.IsOpen == IsOpen && item.TargetSha == TargetSha
        && item.IsHead == IsHead && item.Ahead == Ahead && item.Behind == Behind
        && item.IsSelected == IsSelected && item.Marks == Marks
        && item.PullRequestText == PullRequestText && item.PullRequestToolTip == PullRequestToolTip;
}
