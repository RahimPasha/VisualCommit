using System.Globalization;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>The colour of a dashboard cell's text (docs/phase-6-design.md 2.6): one of the four text tokens.</summary>
public enum DashboardTone
{
    /// <summary><c>VcTextPrimaryBrush</c>.</summary>
    Primary,

    /// <summary><c>VcTextSecondaryBrush</c>.</summary>
    Secondary,

    /// <summary><c>VcWarningBrush</c>.</summary>
    Warning,

    /// <summary><c>VcDangerBrush</c>.</summary>
    Danger,
}

/// <summary>
/// One cell of a dashboard row as <see cref="DashboardTexts"/> words it (2.6): its text (empty for an
/// empty cell), its tone and its tooltip (null for none).
/// </summary>
/// <param name="Text">What the cell shows; empty for nothing.</param>
/// <param name="Tone">Its colour.</param>
/// <param name="ToolTip">Its tooltip, or null.</param>
public sealed record DashboardCellText(string Text, DashboardTone Tone, string? ToolTip)
{
    /// <summary>An empty cell.</summary>
    public static DashboardCellText Empty { get; } = new(string.Empty, DashboardTone.Secondary, null);

    /// <summary>The cell shows nothing.</summary>
    public bool IsEmpty => Text.Length == 0;
}

/// <summary>
/// The Ahead / behind cell (2.6): the left panel's arrows with their counts, or a text. With
/// <see cref="Ahead"/> or <see cref="Behind"/> above 0 the cell draws <c>IconArrowUp</c> and
/// <c>IconArrowDown</c> with the counts and <see cref="Text"/> is empty.
/// </summary>
/// <param name="Ahead">Commits ahead of the upstream; 0 when not shown.</param>
/// <param name="Behind">Commits behind it; 0 when not shown.</param>
/// <param name="Text">The text in place of the arrows: <c>Up to date</c>, <c>{up} is gone</c>, <c>No remote</c>, <c>No upstream</c>, <c>No commits yet</c>; empty with arrows or nothing.</param>
/// <param name="Tone">The text's colour (the arrows and counts are always secondary).</param>
/// <param name="ToolTip">The cell's tooltip, or null.</param>
/// <param name="AutomationName">What UI Automation reads: the arrows' <c>{a} ahead, {n} behind</c>, else the text.</param>
public sealed record DashboardAheadBehind(int Ahead, int Behind, string Text, DashboardTone Tone, string? ToolTip, string AutomationName)
{
    /// <summary>An empty cell: detached, during a rebase, or not read.</summary>
    public static DashboardAheadBehind Empty { get; } = new(0, 0, string.Empty, DashboardTone.Secondary, null, string.Empty);

    /// <summary>The arrows show.</summary>
    public bool ShowsArrows => Ahead > 0 || Behind > 0;

    /// <summary><c>IconArrowUp</c> and its count show.</summary>
    public bool ShowsAhead => Ahead > 0;

    /// <summary><c>IconArrowDown</c> and its count show.</summary>
    public bool ShowsBehind => Behind > 0;

    /// <summary>The ahead count as drawn.</summary>
    public string AheadText => Ahead.ToString(CultureInfo.InvariantCulture);

    /// <summary>The behind count as drawn.</summary>
    public string BehindText => Behind.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The dashboard's cells and header texts, pure, from <c>RepoRefs</c> and <c>WorkingTreeStatus</c>
/// (docs/phase-6-design.md 2.5, 2.6, 2.7, 2.9, 14.3, 14.13). House style (14): disabled reasons,
/// tooltips and status texts have no full stop; notices and sentences have one. Counts take the
/// singular for one (D113).
/// </summary>
public static class DashboardTexts
{
    // The header (2.5, 14.3).

    /// <summary>The header's Add repository button and the empty state's.</summary>
    public const string AddRepository = "Add repository…";

    /// <summary>The header menu's and the empty state's Add open tabs.</summary>
    public const string AddOpenTabs = "Add open tabs";

    /// <summary>The header's Fetch all.</summary>
    public const string FetchAll = "Fetch all";

    /// <summary>The header's Pull all.</summary>
    public const string PullAll = "Pull all";

    /// <summary>The header's Refresh.</summary>
    public const string Refresh = "Refresh";

    /// <summary><c>DashboardMore</c>'s tooltip and automation name.</summary>
    public const string More = "More";

    /// <summary>The header menu's rename.</summary>
    public const string RenameWorkspace = "Rename workspace…";

    /// <summary>The header menu's deletion (destructive).</summary>
    public const string DeleteWorkspace = "Delete workspace…";

    /// <summary><c>DashboardNotice</c> while the store's last save failed (1.3, 2.5).</summary>
    public const string SaveFailedNotice = "workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.";

    /// <summary>The header's count: <c>1 repository</c>, <c>{n} repositories</c>, <c>No repositories</c>.</summary>
    public static string CountText(int repositories) => repositories switch
    {
        0 => "No repositories",
        1 => "1 repository",
        _ => string.Create(CultureInfo.InvariantCulture, $"{repositories} repositories"),
    };

    /// <summary>The empty state's line: <c>{workspace} has no repositories yet.</c></summary>
    public static string EmptyText(string workspace) => $"{workspace} has no repositories yet.";

    /// <summary><c>Add repository…</c>'s enabled tooltip: <c>Add a repository to {workspace}</c>.</summary>
    public static string AddRepositoryToolTip(string workspace) => $"Add a repository to {workspace}";

    /// <summary>Refresh's tooltip: <c>Read the repositories of {workspace} again ({key})</c>, Refresh's key (6.8, D199).</summary>
    public static string RefreshToolTip(string workspace) =>
        Commands.KeyTexts.Current.WithKey($"Read the repositories of {workspace} again", Commands.AppCommand.Refresh);

    // The column headings (2.6).

    public const string RepositoryHeading = "Repository";

    public const string FolderHeading = "Folder";

    public const string BranchHeading = "Branch";

    public const string AheadBehindHeading = "Ahead / behind";

    public const string ChangesHeading = "Changes";

    public const string StateHeading = "State";

    public const string OperationHeading = "Operation";

    // The cells (2.6, 14.3).

    /// <summary>The State cell of a row whose first read has not ended (2.6, 14.13).</summary>
    public const string ReadingText = "Reading…";

    /// <summary>The Ahead / behind cell of a branch level with its upstream.</summary>
    public const string UpToDateText = "Up to date";

    /// <summary>The Ahead / behind cell of a repository without remotes (tested before "no upstream", WD4).</summary>
    public const string NoRemoteText = "No remote";

    /// <summary>The Ahead / behind cell of a branch without an upstream.</summary>
    public const string NoUpstreamText = "No upstream";

    /// <summary>The Ahead / behind cell of an unborn branch.</summary>
    public const string NoCommitsYetText = "No commits yet";

    /// <summary>The Changes cell of a clean working tree.</summary>
    public const string CleanText = "Clean";

    /// <summary>The Repository cell: the folder's name of <paramref name="path"/>, what <c>IGitRepository.Name</c> gives once it is open.</summary>
    public static string NameOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;
    }

    /// <summary>
    /// The Branch cell (2.6): the current branch (also unborn); during a rebase that names its
    /// branch, that branch (git detaches HEAD while it rebases); else <c>Detached at {short}</c>,
    /// the status bar's; empty with nothing to say.
    /// </summary>
    public static string BranchText(RepoRefs refs, WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(status);
        return refs.Head switch
        {
            { BranchName: { } branch } => branch,
            _ when status.InProgress is { Kind: InProgressKind.Rebase, Branch: { Length: > 0 } rebased } => rebased,
            { Sha: { } sha } => $"Detached at {sha[..Math.Min(7, sha.Length)]}",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The Ahead / behind cell (2.6): empty while detached or rebasing; for an unborn branch
    /// <c>No commits yet</c>; with an upstream its counts (the arrows), <c>Up to date</c> or
    /// <c>{up} is gone</c>; without one <c>No remote</c> when the repository has no remotes (tested
    /// first, WD4), else <c>No upstream</c>. <c>{up}</c> is the upstream as the toolbar names it.
    /// </summary>
    public static DashboardAheadBehind AheadBehind(RepoRefs refs, WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(status);
        var head = refs.Head;
        if (head.BranchName is not { } branch || status.InProgress.Kind == InProgressKind.Rebase)
        {
            return DashboardAheadBehind.Empty;
        }

        if (head.IsUnborn)
        {
            return Text(NoCommitsYetText, DashboardTone.Secondary, null);
        }

        var current = refs.Refs.FirstOrDefault(r => r.Kind == RefKind.LocalBranch && r.FullName == "refs/heads/" + branch);
        if (current is not null && (current.UpstreamRef is not null || current.Upstream is not null))
        {
            var upstream = ToolbarViewModel.UpstreamText(current) ?? current.Upstream ?? string.Empty;
            if (current.UpstreamGone)
            {
                return Text($"{upstream} is gone", DashboardTone.Warning, $"{upstream} no longer exists");
            }

            var (ahead, behind) = (current.Ahead, current.Behind);
            if (ahead == 0 && behind == 0)
            {
                return Text(UpToDateText, DashboardTone.Secondary, $"{branch} is level with {upstream}");
            }

            return new DashboardAheadBehind(
                ahead,
                behind,
                string.Empty,
                DashboardTone.Secondary,
                AheadBehindToolTip(ahead, behind, upstream),
                AheadBehindName(ahead, behind));
        }

        return refs.Remotes.Count == 0
            ? Text(NoRemoteText, DashboardTone.Secondary, ToolbarViewModel.NoRemotesText)
            : Text(NoUpstreamText, DashboardTone.Secondary, $"{branch} has no upstream");

        static DashboardAheadBehind Text(string text, DashboardTone tone, string? toolTip) => new(0, 0, text, tone, toolTip, text);
    }

    /// <summary>
    /// The Ahead / behind tooltip (2.6): <c>1 commit ahead of {up}</c> / <c>{a} commits ahead of
    /// {up}</c>; <c>1 commit behind {up}</c> / <c>{n} commits behind {up}</c>; <c>{a} commits ahead
    /// of and {n} behind {up}</c> (<c>1 commit ahead of and …</c>).
    /// </summary>
    public static string AheadBehindToolTip(int ahead, int behind, string upstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);
        var commitsAhead = ahead == 1 ? "1 commit" : string.Create(CultureInfo.InvariantCulture, $"{ahead} commits");
        var commitsBehind = behind == 1 ? "1 commit" : string.Create(CultureInfo.InvariantCulture, $"{behind} commits");
        return (ahead, behind) switch
        {
            ( > 0, > 0) => string.Create(CultureInfo.InvariantCulture, $"{commitsAhead} ahead of and {behind} behind {upstream}"),
            ( > 0, _) => $"{commitsAhead} ahead of {upstream}",
            _ => $"{commitsBehind} behind {upstream}",
        };
    }

    /// <summary>The arrows' automation name: <c>{a} ahead, {n} behind</c>, <c>{a} ahead</c>, <c>{n} behind</c>.</summary>
    public static string AheadBehindName(int ahead, int behind) => (ahead, behind) switch
    {
        ( > 0, > 0) => string.Create(CultureInfo.InvariantCulture, $"{ahead} ahead, {behind} behind"),
        ( > 0, _) => string.Create(CultureInfo.InvariantCulture, $"{ahead} ahead"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{behind} behind"),
    };

    /// <summary>
    /// The Changes cell (2.6): <c>Clean</c> (secondary), or the counts above 0 joined by <c>, </c>:
    /// <c>1 conflict</c> / <c>{c} conflicts</c>, <c>{s} staged</c>, <c>{u} unstaged</c>, <c>{t}
    /// untracked</c>, in the danger colour while a file conflicts, else primary, the whole text its
    /// tooltip. The unstaged count leaves out the untracked and conflicted entries of
    /// <see cref="WorkingTreeStatus.Unstaged"/>, each counted by its own kind (WD5); a file staged and
    /// changed again counts in both.
    /// </summary>
    public static DashboardCellText Changes(WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var conflicts = status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted);
        var staged = status.Staged.Count(file => file.Kind != FileChangeKind.Conflicted);
        var untracked = status.Unstaged.Count(file => file.Kind != FileChangeKind.Conflicted && status.IsUntracked(file.Path));
        var unstaged = status.Unstaged.Count(file => file.Kind != FileChangeKind.Conflicted && !status.IsUntracked(file.Path));
        var parts = new List<string>();
        if (conflicts > 0)
        {
            parts.Add(conflicts == 1 ? "1 conflict" : string.Create(CultureInfo.InvariantCulture, $"{conflicts} conflicts"));
        }

        if (staged > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{staged} staged"));
        }

        if (unstaged > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{unstaged} unstaged"));
        }

        if (untracked > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{untracked} untracked"));
        }

        if (parts.Count == 0)
        {
            return new DashboardCellText(CleanText, DashboardTone.Secondary, null);
        }

        var text = string.Join(", ", parts);
        return new DashboardCellText(text, conflicts > 0 ? DashboardTone.Danger : DashboardTone.Primary, text);
    }

    /// <summary>
    /// The State cell of what is in progress (2.6): <c>Merge in progress</c>, <c>Cherry-pick in
    /// progress</c>, <c>Revert in progress</c>, <c>Rebase in progress</c> or <c>Rebase in progress
    /// ({k} of {n})</c> when the rebase counts, <c>git am in progress</c>, in the warning colour with
    /// the banner's tooltip; empty with nothing in progress.
    /// </summary>
    public static DashboardCellText InProgress(RepoRefs refs, WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(status);
        var state = status.InProgress;
        var text = state.Kind switch
        {
            InProgressKind.Merge => "Merge in progress",
            InProgressKind.CherryPick => "Cherry-pick in progress",
            InProgressKind.Revert => "Revert in progress",
            InProgressKind.Rebase when state.Total > 0 => string.Create(CultureInfo.InvariantCulture, $"Rebase in progress ({state.Done} of {state.Total})"),
            InProgressKind.Rebase => "Rebase in progress",
            InProgressKind.ApplyMailbox => "git am in progress",
            _ => null,
        };
        if (text is null)
        {
            return DashboardCellText.Empty;
        }

        var conflicts = status.Unstaged.Count(file => file.Kind == FileChangeKind.Conflicted);
        return new DashboardCellText(text, DashboardTone.Warning, ConflictBannerViewModel.ToolTipFor(state, conflicts, refs.Head.BranchName ?? "HEAD"));
    }

    /// <summary>The State cell of a row whose repository could not be read (3.1, 2.6): <c>Git could not open {path}: {line}</c>.</summary>
    public static DashboardCellText CouldNotOpen(string path, string error)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(error);
        var line = error.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? error;
        return new DashboardCellText(WorkspaceFolderProbe.NotOpenedText, DashboardTone.Warning, $"Git could not open {path}: {line}");
    }

    /// <summary>The State cell of a folder state (1.10): its short text in the warning colour with its tooltip.</summary>
    public static DashboardCellText FolderState(WorkspaceFolderProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        return probe.ShortText is { } text ? new DashboardCellText(text, DashboardTone.Warning, probe.ToolTip) : DashboardCellText.Empty;
    }

    /// <summary>The Operation cell's tone for the status's <paramref name="tone"/>.</summary>
    public static DashboardTone ToneOf(OperationStatusTone tone) => tone switch
    {
        OperationStatusTone.Warning => DashboardTone.Warning,
        OperationStatusTone.Danger => DashboardTone.Danger,
        _ => DashboardTone.Secondary,
    };

    // The row menu (2.7, 14.10) and its reasons (14.5).

    public const string Open = "Open";

    public const string Fetch = "Fetch";

    public const string Pull = "Pull";

    public const string ErrorDetails = "Error details…";

    public const string MoveUp = "Move up";

    public const string MoveDown = "Move down";

    public const string Locate = "Locate…";

    public const string RemoveFromWorkspace = "Remove from workspace";

    /// <summary>A row's <c>Cancel {title}</c> (2.7).</summary>
    public static string CancelTitle(string title) => $"Cancel {title}";

    /// <summary>Why <c>Cancel {title}</c> is disabled past its cancel window (2.7).</summary>
    public static string NoLongerCancellable(string title) => $"{title} can no longer be cancelled";

    /// <summary>Why a row not read yet cannot be opened, nor its activity shown (2.7, 2.9).</summary>
    public static string WaitToBeRead(string name) => $"Wait for {name} to be read";

    /// <summary>Why <c>Show in folder</c> is disabled: <c>{path} does not exist</c>.</summary>
    public static string DoesNotExist(string path) => $"{path} does not exist";

    /// <summary>Why <c>Move up</c> is disabled on the first row.</summary>
    public const string FirstRepositoryText = "It is the first repository";

    /// <summary>Why <c>Move down</c> is disabled on the last row.</summary>
    public const string LastRepositoryText = "It is the last repository";

    // The activity log on a dashboard (2.9, 14.5).

    /// <summary>The toggle's second reason: no row is selected.</summary>
    public const string SelectRepositoryText = "Select a repository to see its activity";

    /// <summary>The toggle's first reason, also Fetch all's (4.10): <c>{workspace} has no repositories</c>.</summary>
    public static string NoRepositoriesText(string workspace) => $"{workspace} has no repositories";

    // A tab that waits for a closed queue (3.3, 14.13).

    /// <summary><c>TabWaitingText</c>: <c>Waiting for {title} to finish…</c>.</summary>
    public static string WaitingToFinish(string title) => $"Waiting for {title} to finish…";

    // Log lines (14.14).

    /// <summary>A read pass's line: <c>{workspace}: read 1 repository in {ms} ms</c> / <c>… read {n} repositories in {ms} ms</c>.</summary>
    public static string ReadLine(string workspace, int repositories, long milliseconds) => repositories == 1
        ? string.Create(CultureInfo.InvariantCulture, $"{workspace}: read 1 repository in {milliseconds} ms")
        : string.Create(CultureInfo.InvariantCulture, $"{workspace}: read {repositories} repositories in {milliseconds} ms");

    /// <summary>A summary read's line: <c>{name}: summary read in {ms} ms</c> (Debug).</summary>
    public static string SummaryReadLine(string name, long milliseconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{name}: summary read in {milliseconds} ms");
}
