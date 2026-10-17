using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Shell;

/// <summary>
/// What a comparison (<see cref="CompareViewModel"/>, docs/phase-5-design.md 6) asks of its
/// repository: <see cref="RepositoryViewModel"/> implements it, so that the comparison is tested
/// with a fake. Every member is called on the UI thread.
/// </summary>
public interface ICompareHost
{
    /// <summary>Cancelled when the tab closes, and with it every read of the comparison.</summary>
    CancellationToken Lifetime { get; }

    IAppLog Log { get; }

    /// <summary>The settings: the file list's flat or tree view (D44), shared with the details panel.</summary>
    ISettingsStore Settings { get; }

    /// <summary>What two commits or branches differ in (<see cref="IGitRepository.CompareAsync"/>, 6.3).</summary>
    Task<CompareResult> CompareAsync(string from, string to, bool threeDot, bool countCommits, CancellationToken cancellationToken);

    /// <summary>Whether the graph has a row for the commit: a link to one it lacks is disabled (6.4).</summary>
    bool HasRow(string sha);

    /// <summary>Scrolls the commit's row into view without selecting it, so that the comparison stays (6.4, 11.3).</summary>
    void Reveal(string sha);

    /// <summary>Opens a listed file's diff in the graph area, from <paramref name="old"/> to <paramref name="to"/> (6.4).</summary>
    void OpenDiff(CompareViewModel comparison, ChangedFile file, string old, string to);

    /// <summary>A branch comparison's close button (6.2): the details of the graph's selection show again, and the graph takes the focus.</summary>
    void Close(CompareViewModel comparison);
}

/// <summary>One side of a comparison: what git is given for it and how the view names it.</summary>
/// <param name="Revision">A full SHA (two commits) or a full ref name (a branch or a tag, FD20).</param>
/// <param name="Label">How the view names it: <c>0575c7f Rename the parser to reader</c>, or the branch's name <c>main</c>.</param>
public sealed record CompareSide(string Revision, string Label);

/// <summary>
/// A comparison in the right panel (docs/phase-5-design.md 6, D162, D177): two selected commits,
/// their trees From against To (two-dot); or two branches (or a tag) from a menu or a drop, the
/// changes on To since it left From (three-dot) with the switch <c>All differences</c> for the two
/// trees, the counts of the commits only on each side, and a close button. The names are resolved
/// to SHAs once and kept, so that refs moving meanwhile change nothing (6.3). From and To are
/// links that reveal their commit's row without selecting it; the changed files open their diffs
/// in the graph area. Each kind of list read is kept, so that the switch and the swap back read
/// nothing again. The view is <c>Views/Shell/CompareView</c>.
/// </summary>
public sealed partial class CompareViewModel : ObservableObject, IDisposable
{
    public const string TitleText = "Compare";

    public const string SwapToolTip = "Swap From and To";

    public const string CloseToolTip = "Close the comparison";

    public const string NotInGraphToolTip = "This commit is not in the graph";

    public const string LoadingText = "Loading…";

    public const string NoDifferencesText = "No differences";

    public const string NoCommonHistoryText = "No common history: showing all differences";

    public const string RenamesSkippedText = "Renames are shown as removed and added files.";

    private readonly ICompareHost _host;
    private readonly ChangedFileList _fileList = new();
    private readonly Dictionary<(string From, string To, bool ThreeDot), CompareResult> _read = [];

    /// <summary>
    /// The counts read, by From and To as asked and as resolved (15.9): kept apart from the lists,
    /// so that a list read without them (the switch's) never leaves the line empty, and a read
    /// that a swap or the switch cancelled is made again where they are still missing.
    /// </summary>
    private readonly Dictionary<(string From, string To), CompareCounts> _counts = [];
    private CancellationTokenSource? _loading;
    private bool _disposed;

    /// <param name="host">The repository.</param>
    /// <param name="from">The side compared from: the lower of two commits, or the current branch (a drop's target).</param>
    /// <param name="to">The side compared to: the upper commit, or the branch the menu is on (a drop's source).</param>
    /// <param name="isBranches">Branches (or a tag): three-dot by default, with the switch, the counts and a close button.</param>
    public CompareViewModel(ICompareHost host, CompareSide from, CompareSide to, bool isBranches)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        _host = host;
        From = from;
        To = to;
        IsBranches = isBranches;
        OffersAllDifferences = isBranches;
        FileList = host.Settings.Current.FileList;
        _fileList.SetMode(FileList);
    }

    /// <summary>The header's title, which also names the view's pane in UI Automation.</summary>
    public string Title => TitleText;

    /// <summary>Branches (or a tag) compared, not two selected commits: the switch, the counts and the close button show (6.4).</summary>
    public bool IsBranches { get; }

    /// <summary>The side compared from.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FromText))]
    public partial CompareSide From { get; private set; }

    /// <summary>The side compared to.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToText))]
    public partial CompareSide To { get; private set; }

    /// <summary>The From link's text: <c>From {short} {subject}</c> or <c>From {name}</c>.</summary>
    public string FromText => "From " + From.Label;

    /// <summary>The To link's text: <c>To {short} {subject}</c> or <c>To {name}</c>.</summary>
    public string ToText => "To " + To.Label;

    /// <summary>The commit From names, once resolved; null before.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FromLinkToolTip))]
    public partial string? FromSha { get; private set; }

    /// <summary>The commit To names, once resolved; null before.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToLinkToolTip))]
    public partial string? ToSha { get; private set; }

    /// <summary>The From link reveals its commit: it is resolved and a row of the graph.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FromLinkToolTip))]
    [NotifyCanExecuteChangedFor(nameof(RevealFromCommand))]
    public partial bool CanRevealFrom { get; private set; }

    /// <summary>The To link reveals its commit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToLinkToolTip))]
    [NotifyCanExecuteChangedFor(nameof(RevealToCommand))]
    public partial bool CanRevealTo { get; private set; }

    /// <summary>Why the From link is disabled (6.4, 12.4): its commit is not in the graph; null while it is enabled or not resolved yet.</summary>
    public string? FromLinkToolTip => !CanRevealFrom && FromSha is not null ? NotInGraphToolTip : null;

    /// <summary>Why the To link is disabled.</summary>
    public string? ToLinkToolTip => !CanRevealTo && ToSha is not null ? NotInGraphToolTip : null;

    /// <summary>The switch <c>All differences</c> (6.3): the two trees instead of the changes since the merge base. A branch comparison starts without it (FD9).</summary>
    [ObservableProperty]
    public partial bool AllDifferences { get; set; }

    /// <summary>
    /// The switch shows (6.3): for branches, until their first read finds no common history, when
    /// the list is the two trees already and three-dot is not offered (15.9).
    /// </summary>
    [ObservableProperty]
    public partial bool OffersAllDifferences { get; private set; }

    /// <summary>The list is being read: <c>Loading…</c> shows under the header (6.4, 12.5).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoDifferences))]
    public partial bool IsLoading { get; private set; } = true;

    /// <summary>The counts of a branch comparison (6.4): <c>{a} commits only on {To}, {b} only on {From}</c>; empty for two commits or before they are read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCounts))]
    public partial string CountsText { get; private set; } = string.Empty;

    public bool HasCounts => CountsText.Length > 0;

    /// <summary>
    /// What the comparison says about itself (6.3): no common history, several merge bases, renames
    /// not detected, the same commit on both sides; empty otherwise.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string NoticeText { get; private set; } = string.Empty;

    public bool HasNotice => NoticeText.Length > 0;

    /// <summary>Why the comparison could not be read, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorText { get; private set; }

    public bool HasError => ErrorText is not null;

    /// <summary>The list read and its title: <c>Changed files ({N})</c> with Flat and Tree (6.4); not for one commit on both sides.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoDifferences))]
    public partial bool HasFiles { get; private set; }

    /// <summary><c>Changed files ({N})</c>.</summary>
    [ObservableProperty]
    public partial string ChangedFilesTitle { get; private set; } = string.Empty;

    /// <summary>The two trees are the same: <c>No differences</c> (6.4).</summary>
    public bool ShowsNoDifferences => HasFiles && !IsLoading && _fileList.Files.Count == 0;

    /// <summary>The side the listed files are compared from: the merge base in three-dot, From otherwise; a file's diff starts there (6.4). Null before the list is read.</summary>
    public string? Old { get; private set; }

    /// <summary>Flat or tree, from the settings (D44), saved when changed here as in the details panel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFlat), nameof(IsTree))]
    public partial FileListMode FileList { get; private set; }

    public bool IsFlat => FileList == FileListMode.Flat;

    public bool IsTree => FileList == FileListMode.Tree;

    /// <summary>The rows of the changed-file list (<c>CompareFileList</c>), phase 1's.</summary>
    public RowCollection<ChangedFileRow> Files => _fileList.Rows;

    /// <summary>The files as read, in git's order.</summary>
    public IReadOnlyList<ChangedFile> ChangedFiles => _fileList.Files;

    /// <summary>The path of the file whose diff is open from the list: its row has the selection background while it is (6.4).</summary>
    public string? SelectedFilePath
    {
        get => _fileList.SelectedPath;
        set => _fileList.SelectedPath = value;
    }

    /// <summary>The comparison of <paramref name="older"/> and <paramref name="newer"/>, selected in the graph, whichever side it shows now.</summary>
    public bool Compares(string older, string newer) =>
        !IsBranches && new HashSet<string>([From.Revision, To.Revision], StringComparer.Ordinal).SetEquals([older, newer]);

    /// <summary>Reads the comparison (6.3): the list, and for branches the counts. Completes when it is shown.</summary>
    public Task LoadAsync() => LoadAsync(From, To, keepCounts: false);

    /// <summary>The graph was loaded again, or has more rows: a link follows whether its commit is a row now.</summary>
    public void UpdateLinks()
    {
        CanRevealFrom = FromSha is { } from && _host.HasRow(from);
        CanRevealTo = ToSha is { } to && _host.HasRow(to);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
    }

    partial void OnAllDifferencesChanged(bool value)
    {
        if (!_disposed && IsBranches)
        {
            _ = LoadAsync(Resolved(From, FromSha), Resolved(To, ToSha), keepCounts: true);
        }
    }

    /// <summary>The swap button (6.1): From and To change places, and the list is read the other way round.</summary>
    [RelayCommand]
    private Task SwapAsync()
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        var (from, to) = (Resolved(To, ToSha), Resolved(From, FromSha));
        (From, To) = (from, to);
        (FromSha, ToSha) = (ToSha, FromSha);
        UpdateLinks();
        return LoadAsync(from, to, keepCounts: false);
    }

    /// <summary>A branch comparison's close button (6.2).</summary>
    [RelayCommand]
    private void Close()
    {
        if (!_disposed)
        {
            _host.Close(this);
        }
    }

    /// <summary>The From link: reveals its commit's row without selecting it (6.4).</summary>
    [RelayCommand(CanExecute = nameof(CanRevealFrom))]
    private void RevealFrom()
    {
        if (FromSha is { } sha)
        {
            _host.Reveal(sha);
        }
    }

    /// <summary>The To link.</summary>
    [RelayCommand(CanExecute = nameof(CanRevealTo))]
    private void RevealTo()
    {
        if (ToSha is { } sha)
        {
            _host.Reveal(sha);
        }
    }

    /// <summary>A click on a listed file: its diff opens in the graph area (6.4); a folder of the tree does nothing.</summary>
    [RelayCommand]
    private void ActivateFile(ChangedFileRow? row)
    {
        if (row?.File is { } file && Old is { } old && ToSha is { } to && !_disposed)
        {
            _host.OpenDiff(this, file, old, to);
        }
    }

    /// <summary>Lists the files with their folders, and saves the choice (D44).</summary>
    [RelayCommand]
    private void ShowFlat() => SetFileList(FileListMode.Flat);

    /// <summary>Lists the files as a tree of folders, and saves the choice.</summary>
    [RelayCommand]
    private void ShowTree() => SetFileList(FileListMode.Tree);

    private void SetFileList(FileListMode mode)
    {
        if (FileList != mode)
        {
            FileList = mode;
            _host.Settings.Update(settings => settings with { FileList = mode });
            _fileList.SetMode(mode);
        }
    }

    /// <summary>A side named by its resolved commit once it is known, so that a ref that moved meanwhile changes nothing (6.3).</summary>
    private static CompareSide Resolved(CompareSide side, string? sha) => sha is null ? side : side with { Revision = sha };

    private async Task LoadAsync(CompareSide from, CompareSide to, bool keepCounts)
    {
        Cancel();
        var loading = CancellationTokenSource.CreateLinkedTokenSource(_host.Lifetime);
        _loading = loading;
        var threeDot = IsBranches && !AllDifferences;
        var key = (from.Revision, to.Revision, threeDot);
        IsLoading = true;
        ErrorText = null;
        if (!keepCounts)
        {
            CountsText = string.Empty;
        }

        try
        {
            // The counts are read with any list while they are missing for these sides: also by
            // the switch when the first read was cancelled or failed.
            var needCounts = IsBranches && !_counts.ContainsKey((from.Revision, to.Revision));
            if (!_read.TryGetValue(key, out var result) || (needCounts && result.Counts is null))
            {
                result = await _host.CompareAsync(from.Revision, to.Revision, threeDot, countCommits: needCounts, loading.Token);
                _read[key] = result;

                // The same comparison by the resolved SHAs, which the switch and the swap read by.
                _read[(result.From, result.To, threeDot)] = result;
            }

            if (IsBranches && result.Counts is { } counts)
            {
                _counts[(from.Revision, to.Revision)] = counts;
                _counts[(result.From, result.To)] = counts;
            }

            if (!ReferenceEquals(_loading, loading))
            {
                return;
            }

            Show(result);
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
            // A swap, the switch, or the comparison closed.
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_loading, loading))
            {
                _host.Log.Warning($"Comparing {from.Revision} with {to.Revision} failed.", ex);
                var reason = ex is GitException { StandardError: { } error } && !string.IsNullOrWhiteSpace(error) ? error.Trim() : ex.Message;
                ErrorText = $"The comparison could not be read. {reason}";
                IsLoading = false;
            }
        }
        finally
        {
            if (ReferenceEquals(_loading, loading))
            {
                _loading = null;
                loading.Dispose();
            }
        }
    }

    private void Show(CompareResult result)
    {
        FromSha = result.From;
        ToSha = result.To;
        UpdateLinks();
        Old = result.Old;

        if (IsBranches && _counts.TryGetValue((result.From, result.To), out var counts))
        {
            CountsText = Counts(counts, To.Label, From.Label);
        }

        // No common history: the list is the two trees, and the switch is not offered (6.3); a
        // merge base is the same both ways, so a swap keeps it so.
        if (result.Base == CompareBase.NoCommonHistory)
        {
            OffersAllDifferences = false;
        }

        if (result.IsSameCommit)
        {
            // Nothing to compare: no list (6.3).
            NoticeText = $"Both are {OperationTexts.Short(result.From)}: nothing to compare";
            HasFiles = false;
            _fileList.SelectedPath = null;
            _fileList.Show([]);
            ChangedFilesTitle = string.Empty;
            IsLoading = false;
            return;
        }

        var notices = new List<string>();
        if (result.Base == CompareBase.NoCommonHistory)
        {
            notices.Add(NoCommonHistoryText);
        }
        else if (result.Base == CompareBase.SeveralMergeBases)
        {
            notices.Add($"Several merge bases: changes since {OperationTexts.Short(result.Old)}");
        }

        if (result.RenamesSkipped)
        {
            notices.Add(RenamesSkippedText);
        }

        NoticeText = string.Join('\n', notices);
        _fileList.SelectedPath = null;
        _fileList.Show(result.Files);
        ChangedFilesTitle = string.Create(CultureInfo.InvariantCulture, $"Changed files ({result.Files.Count})");
        HasFiles = true;
        IsLoading = false;
        OnPropertyChanged(nameof(ShowsNoDifferences));
    }

    /// <summary>The counts' text (6.4): <c>{a} commits only on {To}, {b} only on {From}</c>, <c>1 commit only on …</c> for one (D113).</summary>
    public static string Counts(CompareCounts counts, string to, string from)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var onTo = counts.OnlyOnTo == 1 ? "1 commit" : string.Create(CultureInfo.InvariantCulture, $"{counts.OnlyOnTo} commits");
        return string.Create(CultureInfo.InvariantCulture, $"{onTo} only on {to}, {counts.OnlyOnFrom} only on {from}");
    }

    private void Cancel()
    {
        var loading = _loading;
        _loading = null;
        if (loading is not null)
        {
            loading.Cancel();
            loading.Dispose();
        }
    }
}
