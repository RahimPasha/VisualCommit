using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using static VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;

namespace VisualCommit.App.ViewModels.Pages;

/// <summary>
/// The reflog page (docs/phase-5-design.md 5.1 to 5.3, D180): one reflog at a time (HEAD's or a
/// local branch's), read through git with its lost commits marked once the list shows, or the
/// backups list, each force push's with whether its lease can still be found (7.10). The source
/// changes through the header's source menu. The page reads again on every refresh while it is
/// open (some entries move no ref), and never while it is closed; a read again keeps the rows it
/// did not change, so that their marks, the selection and the keyboard focus stay. A click or a
/// key on a row shows its commit in the right panel (11.3, BD9); each row's menu starts phase 3's
/// operations or a backup's restore.
/// </summary>
public sealed partial class ReflogViewModel : GraphAreaPageViewModel
{
    /// <summary>The most entries a reflog reads (5.2).</summary>
    public const int Limit = 500;

    /// <summary>A reflog row's time shows from this page width (BD3), its selector from <see cref="SelectorFromWidth"/>.</summary>
    public const double TimeFromWidth = 552;

    /// <inheritdoc cref="TimeFromWidth"/>
    public const double SelectorFromWidth = 432;

    /// <summary>The empty reflog's explanation of git's rules (5.2).</summary>
    public const string ExpiryText = "Git keeps a reflog entry for 90 days, and for 30 days when no branch holds its commit.";

    /// <summary>The empty backups list.</summary>
    public const string NoBackupsText = "There are no backups";

    /// <summary>The footer when the reflog has more entries than the page reads (5.2, 15.9).</summary>
    public const string MoreText = "Only the newest 500 entries are shown";

    private const string HeadsPrefix = "refs/heads/";

    private int _version;
    private bool _selectingQuietly;

    /// <summary>The read that runs: a newer one cancels it, so that its git calls stop (15.9).</summary>
    private CancellationTokenSource? _reading;

    /// <summary>The refs as last read (every refresh hands them over), which the next read is made for.</summary>
    private RepoRefs? _refs;

    /// <param name="host">The repository.</param>
    /// <param name="source"><c>HEAD</c>, a local branch's full name, or null for the backups.</param>
    public ReflogViewModel(IGraphAreaPageHost host, string? source)
        : base(host)
    {
        SetSource(source);
    }

    /// <summary><c>HEAD</c>, a local branch's full name, or null while the page shows the backups.</summary>
    public string? Source { get; private set; }

    /// <summary>The page shows the backups list (5.3).</summary>
    public bool ShowsBackups => Source is null;

    /// <summary>The source's name on <c>ReflogSource</c>: <c>HEAD</c>, a branch's name, <c>Backups</c>.</summary>
    [ObservableProperty]
    public partial string SourceLabel { get; private set; } = string.Empty;

    /// <summary>The reflog's entries or the backups, newest first.</summary>
    public ObservableCollection<PageListRow> Rows { get; } = [];

    /// <summary>The first read has not ended: the list's place says <c>Loading…</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLoading))]
    public partial bool IsLoading { get; private set; } = true;

    public bool ShowsLoading => IsLoading && Rows.Count == 0;

    /// <summary>
    /// What the list's place says instead of rows: <c>{name} has no reflog</c> with git's rules,
    /// the empty backups list, or git's failure; null while there are rows.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    /// <summary>A second line under <see cref="Message"/>: git's rules for an empty reflog (5.2); null otherwise.</summary>
    [ObservableProperty]
    public partial string? Detail { get; private set; }

    /// <summary>The message is git's failure: drawn in the error style.</summary>
    [ObservableProperty]
    public partial bool IsError { get; private set; }

    /// <summary>The footer (<c>ReflogFooter</c>, BD5) when git skipped entries or the reflog has more than <see cref="Limit"/>; null otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFooter))]
    public partial string? Footer { get; private set; }

    public bool HasFooter => Footer is not null;

    /// <summary>How many reads the page has shown: a check waits for the next one after an operation (B8, B9).</summary>
    [ObservableProperty]
    public partial int ReadsShown { get; private set; }

    /// <summary>
    /// The refs the last read shown was made for (the repository's after the refresh that asked
    /// for it), lost marks included; null while the source's first read has not ended. A check
    /// that waits for an operation's effect waits until these are the repository's refs now, so
    /// that a read asked for before the operation does not count.
    /// </summary>
    public RepoRefs? ShownFor { get; private set; }

    /// <summary>The selected row. The list binds to it both ways: a change from the list is the user's.</summary>
    [ObservableProperty]
    public partial PageListRow? SelectedRow { get; set; }

    /// <summary>The page's width, which decides a reflog row's columns (BD3). The view sets it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTime), nameof(ShowsSelector))]
    public partial double PageWidth { get; set; }

    public bool ShowsTime => PageWidth >= TimeFromWidth;

    public bool ShowsSelector => PageWidth >= SelectorFromWidth;

    /// <summary>Reads the source (5.2, 5.3) and shows it.</summary>
    public Task LoadAsync()
    {
        _refs ??= Host.MenuContext.Refs;
        return ReadAsync();
    }

    /// <summary>The refs were read again (every refresh): the page reads again while it is open (5.2).</summary>
    public override void OnRefsRead(RepoRefs refs)
    {
        base.OnRefsRead(refs);
        _refs = refs;
        if (!IsClosed)
        {
            _ = ReadAsync();
        }
    }

    /// <summary>The source menu's choice (5.1, BD7): <c>HEAD</c>, a local branch's full name, or null for the backups.</summary>
    public void ChooseSource(string? source)
    {
        if (IsClosed)
        {
            return;
        }

        SetSource(source);
        Rows.Clear();
        SelectedRow = null;
        Message = null;
        Detail = null;
        Footer = null;
        ShownFor = null;
        IsLoading = true;
        OnPropertyChanged(nameof(ShowsLoading));
        _ = ReadAsync();
        RequestFocus();
    }

    /// <summary>The source menu (<see cref="RepositoryMenus.ForReflogSource"/>).</summary>
    public MenuModel SourceMenu() => RepositoryMenus.ForReflogSource(Host.MenuContext, ChooseSource);

    /// <summary>A row's menu: a reflog entry's (<see cref="RepositoryMenus.ForReflogEntry"/>) or a backup's (<see cref="RepositoryMenus.ForBackup"/>).</summary>
    public MenuModel MenuFor(PageListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var context = Host.MenuContext;
        return row switch
        {
            ReflogRowViewModel entry => RepositoryMenus.ForReflogEntry(context, entry.Entry, Host.HasCommit(entry.Sha), !entry.IsLost && Host.IsOnHead(entry.Sha), Host.Actions),
            BackupRowViewModel backup => RepositoryMenus.ForBackup(context, backup.Backup, Host.Actions, backup.HasForcePushLease),
            _ => throw new ArgumentException($"Not a row of this page: {row.GetType().Name}.", nameof(row)),
        };
    }

    /// <summary>A double-click on a row: the page closes and the graph shows its commit; nothing for a commit the graph does not have (4.3).</summary>
    public void ShowInGraph(PageListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!IsClosed && row.Sha is { Length: > 0 } sha && Host.HasCommit(sha))
        {
            Host.ShowInGraph(sha);
        }
    }

    partial void OnSelectedRowChanged(PageListRow? value)
    {
        // The user's click or key (11.3, BD9): the right panel shows the row's commit.
        if (value is { Sha: { Length: > 0 } sha } && !_selectingQuietly && !IsClosed)
        {
            Host.ShowCommit(sha);
        }
    }

    private void SetSource(string? source)
    {
        Source = source;
        if (source is null)
        {
            SourceLabel = BackupsSource;
            Title = BackupsSource;
        }
        else
        {
            SourceLabel = source.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? source[HeadsPrefix.Length..] : source;
            Title = $"Reflog of {SourceLabel}";
        }

        OnPropertyChanged(nameof(ShowsBackups));
    }

    /// <summary>
    /// One read of the source; a newer read, or another source, cancels an older one, whose git
    /// calls stop, and which shows nothing (15.9).
    /// </summary>
    private async Task ReadAsync()
    {
        var version = ++_version;
        var source = Source;
        var refs = _refs;
        _reading?.Cancel();
        _reading?.Dispose();
        var reading = CancellationTokenSource.CreateLinkedTokenSource(Lifetime);
        _reading = reading;
        var token = reading.Token;
        try
        {
            if (source is null)
            {
                var backups = await Host.Repository.ReadBackupsAsync(token);
                var leases = await ReadForcePushLeasesAsync(backups, token);
                if (version == _version && !IsClosed)
                {
                    ShowBackupsList(backups, leases);
                    Shown(refs);
                }

                return;
            }

            var reflog = await Host.Repository.ReadReflogAsync(source, Limit, token);
            if (version != _version || IsClosed)
            {
                return;
            }

            ShowReflog(reflog);

            // The lost marks come once the list shows (5.2): one walk for every distinct commit.
            // Until it ends, every row keeps the mark it had, so that the marks do not flicker.
            var shas = reflog.Entries.Select(entry => entry.Sha).Distinct(StringComparer.Ordinal).ToList();
            var lost = shas.Count == 0 ? new HashSet<string>(StringComparer.Ordinal) : await Host.Repository.FindLostCommitsAsync(shas, token);
            if (version == _version && !IsClosed)
            {
                foreach (var row in Rows.OfType<ReflogRowViewModel>())
                {
                    row.IsLost = lost.Contains(row.Sha);
                }

                Shown(refs);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer read, or the page closed.
        }
        catch (Exception ex)
        {
            Host.Log.Error($"The {(source is null ? "backups" : "reflog of " + source)} could not be read.", ex);
            if (version == _version && !IsClosed)
            {
                var reason = ex is GitException { StandardError: { } error } && !string.IsNullOrWhiteSpace(error) ? error.Trim() : ex.Message;
                Replace([], reason, null, isError: true);
                Shown(refs);
            }
        }
        finally
        {
            if (ReferenceEquals(_reading, reading))
            {
                _reading = null;
                reading.Dispose();
            }
        }
    }

    /// <summary>A read is shown, its lost marks included: what it was made for, and one more read counted.</summary>
    private void Shown(RepoRefs? refs)
    {
        ShownFor = refs;
        ReadsShown++;
    }

    private void ShowReflog(ReflogResult reflog)
    {
        // A row the read brings anew keeps the lost mark its commit had until the walk ends.
        var lostBefore = Rows.OfType<ReflogRowViewModel>().Where(row => row.IsLost).Select(row => row.Sha).ToHashSet(StringComparer.Ordinal);
        var rows = reflog.Entries.Select(entry => (PageListRow)new ReflogRowViewModel(entry, DisplayedSelector(entry.Selector), Host.Dates.Format(entry.Time))
        {
            IsLost = lostBefore.Contains(entry.Sha),
        }).ToList();
        Replace(rows, rows.Count == 0 ? $"{SourceLabel} has no reflog" : null, rows.Count == 0 ? ExpiryText : null, isError: false);
        var skippedText = reflog.Skipped switch
        {
            0 => null,
            1 => "1 entry is not shown: it names no commit that exists",
            var skipped => string.Create(CultureInfo.InvariantCulture, $"{skipped} entries are not shown: they name no commit that exists"),
        };
        Footer = (reflog.More, skippedText) switch
        {
            (true, null) => MoreText,
            (true, { } skipped) => $"{MoreText}. {skipped}",
            (false, var skipped) => skipped,
        };
    }

    private void ShowBackupsList(IReadOnlyList<BackupInfo> backups, IReadOnlyDictionary<string, bool> leases)
    {
        var remotes = Host.MenuContext.Remotes;
        var zone = Host.Dates.TimeZone;
        var rows = backups.Select(backup => (PageListRow)new BackupRowViewModel(
            backup,
            TimeZoneInfo.ConvertTime(backup.Time, zone).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            BackupWords(backup, remotes))
        {
            HasForcePushLease = leases.TryGetValue(backup.Name, out var found) ? found : null,
        }).ToList();
        Replace(rows, rows.Count == 0 ? NoBackupsText : null, null, isError: false);
        Footer = null;
    }

    /// <summary>
    /// For each force push's backup, whether the remote-tracking reflog of its ref still has the
    /// commit the force push left (7.10, <see cref="ReflogResult.ForcePushLease"/>, read as deep as
    /// the restore reads it): its restore is disabled otherwise. A reflog that cannot be read
    /// leaves the backup out: the restore decides.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, bool>> ReadForcePushLeasesAsync(IReadOnlyList<BackupInfo> backups, CancellationToken token)
    {
        var leases = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var backup in backups)
        {
            if (backup is not { Kind: "force-push", Main: { Name: { Length: > 0 } name, Commit: { Length: > 0 } commit } })
            {
                continue;
            }

            try
            {
                var reflog = await Host.Repository.ReadReflogAsync("refs/remotes/" + name, ReflogResult.ForcePushLeaseDepth, token);
                leases[backup.Name] = reflog.ForcePushLease(commit) is not null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Host.Log.Warning($"The reflog of refs/remotes/{name} could not be read for the backup {backup.Name}.", ex);
            }
        }

        return leases;
    }

    /// <summary>
    /// Shows a read's rows. A row the read did not change keeps its instance and its place in the
    /// list, and with them the list's container, the keyboard focus on it and its lost mark; the
    /// selected row is kept, or found again by its key when the read made it anew.
    /// </summary>
    private void Replace(IReadOnlyList<PageListRow> rows, string? message, string? detail, bool isError)
    {
        var selected = SelectedRow;
        _selectingQuietly = true;
        try
        {
            var before = new Dictionary<string, PageListRow>(StringComparer.Ordinal);
            foreach (var row in Rows)
            {
                before.TryAdd(row.Key, row);
            }

            var wanted = rows.Select(row => before.TryGetValue(row.Key, out var old) && old.SameAs(row) ? old : row).ToList();
            Synchronize(Rows, wanted);
            SelectedRow = selected is null ? null
                : wanted.Contains(selected) ? selected
                : wanted.FirstOrDefault(row => row.Key == selected.Key);
        }
        finally
        {
            _selectingQuietly = false;
        }

        IsLoading = false;
        Message = message;
        Detail = detail;
        IsError = isError;
        OnPropertyChanged(nameof(ShowsLoading));
    }

    /// <summary>Makes <paramref name="rows"/> hold <paramref name="wanted"/> in its order, removing, moving and inserting rows rather than clearing the list.</summary>
    private static void Synchronize(ObservableCollection<PageListRow> rows, IReadOnlyList<PageListRow> wanted)
    {
        var keep = new HashSet<PageListRow>(wanted);
        if (!rows.Any(keep.Contains))
        {
            rows.Clear();
            foreach (var row in wanted)
            {
                rows.Add(row);
            }

            return;
        }

        for (var index = rows.Count - 1; index >= 0; index--)
        {
            if (!keep.Contains(rows[index]))
            {
                rows.RemoveAt(index);
            }
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            if (index < rows.Count && ReferenceEquals(rows[index], wanted[index]))
            {
                continue;
            }

            var at = rows.IndexOf(wanted[index]);
            if (at > index)
            {
                rows.Move(at, index);
            }
            else
            {
                rows.Insert(index, wanted[index]);
            }
        }
    }

    /// <summary>The selector as the page shows it (5.2): git's <c>refs/heads/main@{0}</c> as <c>main@{0}</c>.</summary>
    public static string DisplayedSelector(string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector.StartsWith(HeadsPrefix, StringComparison.Ordinal) ? selector[HeadsPrefix.Length..] : selector;
    }
}

/// <summary>A row of the reflog page's list: a reflog entry or a backup.</summary>
public abstract partial class PageListRow : ObservableObject
{
    /// <summary>What names the row across reads (its selector and commit, or the backup's name), so that a read keeps the selection.</summary>
    public abstract string Key { get; }

    /// <summary>The row's automation name: a reflog entry's selector, a backup's name.</summary>
    public abstract string Name { get; }

    /// <summary>The commit the row stands for, shown in the right panel when it is picked; empty for a backup that keeps none.</summary>
    public abstract string Sha { get; }

    /// <summary>Whether <paramref name="other"/>, a later read's row with the same key, shows the same: then this row stays in the list in its place.</summary>
    internal abstract bool SameAs(PageListRow other);
}

/// <summary>One entry of a reflog (5.2, BD3): selector, time, action, commit, and whether it is lost.</summary>
public sealed partial class ReflogRowViewModel(ReflogEntry entry, string selector, string timeText) : PageListRow
{
    public ReflogEntry Entry { get; } = entry;

    /// <summary><c>HEAD@{n}</c> or <c>{b}@{n}</c>.</summary>
    public string Selector { get; } = selector;

    /// <summary>The entry's own time, <c>yyyy-MM-dd HH:mm</c>.</summary>
    public string TimeText { get; } = timeText;

    /// <summary>Git's message for the entry: <c>checkout: moving from main to feature/alpha</c>.</summary>
    public string Action => Entry.Message;

    public string ShortSha => Entry.Sha.Length > 7 ? Entry.Sha[..7] : Entry.Sha;

    public string Subject => Entry.Subject;

    /// <summary>No branch, tag or remote branch reaches the entry's commit (the lost mark, filled in after the list shows).</summary>
    [ObservableProperty]
    public partial bool IsLost { get; set; }

    /// <summary>The lost mark's tooltip.</summary>
    public const string LostToolTip = "No branch, tag or remote branch reaches this commit";

    public override string Key => $"{Selector} {Entry.Sha}";

    public override string Name => Selector;

    public override string Sha => Entry.Sha;

    internal override bool SameAs(PageListRow other) =>
        other is ReflogRowViewModel row
        && row.Selector == Selector
        && row.TimeText == TimeText
        && row.Entry.Sha == Entry.Sha
        && row.Entry.Message == Entry.Message
        && row.Entry.Subject == Entry.Subject
        && row.Entry.Parents.SequenceEqual(Entry.Parents, StringComparer.Ordinal);
}

/// <summary>One backup of the backups list (5.3, BD4): time, words, commit.</summary>
public sealed class BackupRowViewModel(BackupInfo backup, string timeText, string words) : PageListRow
{
    public BackupInfo Backup { get; } = backup;

    /// <summary>The time in its name, <c>yyyy-MM-dd HH:mm:ss</c> in the dates' zone.</summary>
    public string TimeText { get; } = timeText;

    /// <summary>The backup in words (12.3).</summary>
    public string Words { get; } = words;

    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

    /// <summary>The subject of the commit it keeps.</summary>
    public string Subject => Backup.Main?.CommitSubject ?? string.Empty;

    /// <summary>
    /// For a force push's backup, whether the remote-tracking reflog still has the commit the
    /// force push left, its restore's lease (7.10): its restore is disabled when it has not. Null
    /// for another kind, or when the reflog could not be read.
    /// </summary>
    public bool? HasForcePushLease { get; init; }

    public override string Key => Backup.Name;

    public override string Name => Backup.Name;

    public override string Sha => Backup.Main?.Commit ?? string.Empty;

    internal override bool SameAs(PageListRow other) =>
        other is BackupRowViewModel row
        && row.TimeText == TimeText
        && row.Words == Words
        && row.HasForcePushLease == HasForcePushLease
        && row.Backup.Kind == Backup.Kind
        && row.Backup.Refs.Select(entry => (entry.FullName, entry.Target, entry.Commit, entry.CommitSubject))
            .SequenceEqual(Backup.Refs.Select(entry => (entry.FullName, entry.Target, entry.Commit, entry.CommitSubject)));
}
