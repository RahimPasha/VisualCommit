using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels.Diff;

/// <summary>What the diff view's body shows.</summary>
public enum DiffBody
{
    /// <summary>The diff is being read.</summary>
    Loading,

    /// <summary>The diff as text, inline or side by side.</summary>
    Text,

    /// <summary>"Very large diff", with "Show diff" (D65).</summary>
    VeryLarge,

    /// <summary>"Binary file", with the sizes before and after.</summary>
    Binary,

    /// <summary>Two images, before and after.</summary>
    Image,

    /// <summary>A diff without hunks: "No changes to the file's content."</summary>
    Empty,

    /// <summary>A conflicted file, which phase 4's resolver will show.</summary>
    Conflict,

    /// <summary>The diff could not be read; <see cref="DiffViewModel.ErrorText"/> says why.</summary>
    Error,
}

/// <summary>A line action of the diff view.</summary>
public enum LineAction
{
    Stage,
    Unstage,
    Discard,
}

/// <summary>What the diff view asks its repository to do. Each write returns git's message when it fails, or null.</summary>
public interface IDiffHost
{
    Task<FileDiff> ReadDiffAsync(DiffTarget target, CancellationToken cancellationToken);

    Task<byte[]?> ReadFileAsync(FileVersion version, long maxBytes, CancellationToken cancellationToken);

    Task<long?> ReadFileSizeAsync(FileVersion version, CancellationToken cancellationToken);

    Task<string?> StageAsync(IReadOnlyList<ChangedFile> files);

    Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files);

    Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files);

    /// <summary>
    /// Stages, unstages or discards the chosen lines of <paramref name="diff"/>; discarding asks
    /// first and saves a snapshot (D62, D67). <paramref name="wholeHunk"/> words the question for a hunk.
    /// </summary>
    Task<string?> ApplyLinesAsync(DiffTarget target, FileDiff diff, IReadOnlySet<DiffLineRef> lines, LineAction action, bool wholeHunk);

    /// <summary>Closes the diff view and shows the graph again.</summary>
    void CloseDiff();
}

/// <summary>
/// One open diff (C5, D61, D63 to D65): which file and which versions, how it is shown, and the
/// actions on the file, a hunk or the selected lines. The view draws the text with the diff
/// text control.
/// </summary>
public sealed partial class DiffViewModel : ObservableObject, IDisposable
{
    /// <summary>An image larger than this is not read into memory; its sizes still show.</summary>
    public const long MaxImageBytes = 20 * 1024 * 1024;

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    private readonly IDiffHost _host;
    private readonly ISettingsStore _settings;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _lifetime = new();
    private int _version;

    public DiffViewModel(IDiffHost host, DiffTarget target, string sideLabel, ISettingsStore settings, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        _host = host;
        _settings = settings;
        _log = log ?? NullAppLog.Instance;
        Target = target;
        SideLabel = sideLabel;
        Mode = settings.Current.DiffMode;
    }

    /// <summary>The file and the versions compared.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLetter), nameof(FileName), nameof(Folder), nameof(HasFolder), nameof(OriginText), nameof(HasOrigin), nameof(PathText), nameof(IsAdded), nameof(IsModified), nameof(IsDeleted), nameof(IsRenamed))]
    public partial DiffTarget Target { get; private set; }

    /// <summary>"Unstaged", "Staged" or the commit's short id.</summary>
    public string SideLabel { get; }

    public string StatusLetter => Target.Kind switch
    {
        FileChangeKind.Added => "A",
        FileChangeKind.Modified => "M",
        FileChangeKind.Deleted => "D",
        FileChangeKind.Renamed => "R",
        FileChangeKind.Copied => "C",
        FileChangeKind.TypeChanged => "T",
        FileChangeKind.Conflicted => "U",
        _ => "?",
    };

    public bool IsAdded => Target.Kind == FileChangeKind.Added;

    public bool IsModified => Target.Kind == FileChangeKind.Modified;

    public bool IsDeleted => Target.Kind is FileChangeKind.Deleted or FileChangeKind.Conflicted;

    public bool IsRenamed => Target.Kind is FileChangeKind.Renamed or FileChangeKind.Copied;

    public string FileName => WorkingChangesViewModel.FileName(Target.Path);

    public string Folder => Target.Path.LastIndexOf('/') is var slash and > 0 ? Target.Path[..slash] : string.Empty;

    public bool HasFolder => Folder.Length > 0;

    public string OriginText => Target.OldPath is { } old && IsRenamed
        ? $"{(Target.Kind == FileChangeKind.Copied ? "copied" : "renamed")} from {old}"
        : string.Empty;

    public bool HasOrigin => OriginText.Length > 0;

    /// <summary>The whole path, and where a rename came from, for the header's tooltip.</summary>
    public string PathText => HasOrigin ? $"{Target.Path}\n{OriginText}" : Target.Path;

    public bool IsUnstaged => Target.Side == DiffSide.Unstaged;

    public bool IsStaged => Target.Side == DiffSide.Staged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInline), nameof(IsSideBySide))]
    public partial DiffMode Mode { get; private set; }

    public bool IsInline => Mode == DiffMode.Inline;

    public bool IsSideBySide => Mode == DiffMode.SideBySide;

    /// <summary>The diff read last; null until then.</summary>
    [ObservableProperty]
    public partial FileDiff? Diff { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsText), nameof(ShowsVeryLarge), nameof(ShowsBinary), nameof(ShowsImage), nameof(ShowsEmpty), nameof(ShowsConflict), nameof(ShowsError), nameof(ShowsModeToggle))]
    public partial DiffBody Body { get; private set; } = DiffBody.Loading;

    public bool ShowsText => Body == DiffBody.Text;

    public bool ShowsVeryLarge => Body == DiffBody.VeryLarge;

    public bool ShowsBinary => Body == DiffBody.Binary;

    public bool ShowsImage => Body == DiffBody.Image;

    public bool ShowsEmpty => Body == DiffBody.Empty;

    public bool ShowsConflict => Body == DiffBody.Conflict;

    public bool ShowsError => Body == DiffBody.Error;

    /// <summary>Inline and Side by side apply to text; an image or a binary file hides them.</summary>
    public bool ShowsModeToggle => Body is not (DiffBody.Image or DiffBody.Binary);

    /// <summary>Syntax colours and word-level highlights: off for a very large diff shown on request (D65).</summary>
    [ObservableProperty]
    public partial bool Highlighting { get; private set; } = true;

    /// <summary>"N lines removed and M added." for a very large diff.</summary>
    [ObservableProperty]
    public partial string LargeText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBefore))]
    public partial string? BeforeSizeText { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAfter))]
    public partial string? AfterSizeText { get; private set; }

    public bool HasBefore => BeforeSizeText is not null;

    public bool HasAfter => AfterSizeText is not null;

    /// <summary>The image before the change, or null.</summary>
    [ObservableProperty]
    public partial byte[]? BeforeImage { get; private set; }

    /// <summary>The image after the change, or null.</summary>
    [ObservableProperty]
    public partial byte[]? AfterImage { get; private set; }

    /// <summary>The size of the image before the change in bytes, for its caption.</summary>
    [ObservableProperty]
    public partial long? BeforeBytes { get; private set; }

    [ObservableProperty]
    public partial long? AfterBytes { get; private set; }

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary>The added and removed lines the text's selection holds; the view keeps it up to date.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedLines), nameof(ShowsFileActions), nameof(ShowsLineStageDiscard), nameof(ShowsLineUnstage), nameof(ShowsFileStageDiscard), nameof(ShowsFileUnstage))]
    public partial IReadOnlySet<DiffLineRef> SelectedChanges { get; set; } = new HashSet<DiffLineRef>();

    public bool HasSelectedLines => SelectedChanges.Count > 0 && Body == DiffBody.Text;

    /// <summary>The file's own buttons show while no lines are selected.</summary>
    public bool ShowsFileActions => !HasSelectedLines;

    public bool ShowsFileStageDiscard => IsUnstaged && !HasSelectedLines;

    public bool ShowsFileUnstage => IsStaged && !HasSelectedLines;

    public bool ShowsLineStageDiscard => IsUnstaged && HasSelectedLines;

    public bool ShowsLineUnstage => IsStaged && HasSelectedLines;

    /// <summary>Raised after an action on lines or a hunk: the view clears its selection.</summary>
    public event EventHandler? SelectionResetRequested;

    /// <summary>Reads the diff and shows it.</summary>
    public Task LoadAsync() => LoadAsync(Target);

    /// <summary>Reads the diff again for <paramref name="target"/>: the same file after a write or an outside change, maybe with another kind.</summary>
    public async Task LoadAsync(DiffTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var version = ++_version;
        var cancellationToken = _lifetime.Token;
        Target = target;
        try
        {
            if (target.Kind == FileChangeKind.Conflicted)
            {
                Diff = FileDiff.Empty;
                Body = DiffBody.Conflict;
                return;
            }

            var diff = await _host.ReadDiffAsync(target, cancellationToken);
            if (version != _version)
            {
                return;
            }

            Diff = diff;
            if (diff.IsBinary)
            {
                await ShowBinaryAsync(target, version, cancellationToken);
            }
            else if (diff.Hunks.Count == 0)
            {
                Body = DiffBody.Empty;
            }
            else if (diff.IsVeryLarge && Highlighting)
            {
                LargeText = string.Create(CultureInfo.InvariantCulture, $"{diff.RemovedCount:N0} lines removed and {diff.AddedCount:N0} added.");
                Body = DiffBody.VeryLarge;
            }
            else
            {
                Body = DiffBody.Text;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (version == _version)
            {
                _log.Error($"The diff of {target.Path} could not be read.", ex);
                ErrorText = ex is GitException git && !string.IsNullOrWhiteSpace(git.StandardError) ? git.StandardError.Trim() : ex.Message;
                Body = DiffBody.Error;
            }
        }
    }

    public void Dispose()
    {
        _version++;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    [RelayCommand]
    private void Close() => _host.CloseDiff();

    [RelayCommand]
    private void ShowInline() => SetMode(DiffMode.Inline);

    [RelayCommand]
    private void ShowSideBySide() => SetMode(DiffMode.SideBySide);

    /// <summary>Shows a very large diff after all, without highlighting (D65).</summary>
    [RelayCommand]
    private void ShowLargeDiff()
    {
        Highlighting = false;
        if (Diff is { Hunks.Count: > 0 })
        {
            Body = DiffBody.Text;
        }
    }

    [RelayCommand]
    private Task StageFile() => _host.StageAsync([File]);

    [RelayCommand]
    private Task UnstageFile() => _host.UnstageAsync([File]);

    [RelayCommand]
    private Task DiscardFile() => _host.DiscardAsync([File]);

    [RelayCommand]
    private Task StageLines() => ApplyLinesAsync(SelectedChanges, LineAction.Stage, wholeHunk: false);

    [RelayCommand]
    private Task UnstageLines() => ApplyLinesAsync(SelectedChanges, LineAction.Unstage, wholeHunk: false);

    [RelayCommand]
    private Task DiscardLines() => ApplyLinesAsync(SelectedChanges, LineAction.Discard, wholeHunk: false);

    /// <summary>A hunk header's button: the hunk's changed lines, all of them.</summary>
    public Task RunHunkActionAsync(int hunk, LineAction action)
    {
        if (Diff is not { } diff || hunk < 0 || hunk >= diff.Hunks.Count)
        {
            return Task.CompletedTask;
        }

        var lines = diff.Hunks[hunk].Lines
            .Select((line, index) => (line, index))
            .Where(pair => pair.line.IsChange)
            .Select(pair => new DiffLineRef(hunk, pair.index))
            .ToHashSet();
        return ApplyLinesAsync(lines, action, wholeHunk: true);
    }

    /// <summary>The file as the status lists it, for the file's own actions.</summary>
    private ChangedFile File => new(Target.Path, Target.Kind, Target.OldPath);

    private async Task ApplyLinesAsync(IReadOnlySet<DiffLineRef> lines, LineAction action, bool wholeHunk)
    {
        if (Diff is not { } diff || lines.Count == 0)
        {
            return;
        }

        var error = await _host.ApplyLinesAsync(Target, diff, lines, action, wholeHunk);
        if (error is null)
        {
            SelectionResetRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetMode(DiffMode mode)
    {
        if (Mode != mode)
        {
            Mode = mode;
            _settings.Update(settings => settings with { DiffMode = mode });
        }
    }

    private async Task ShowBinaryAsync(DiffTarget target, int version, CancellationToken cancellationToken)
    {
        var before = target.Before;
        var after = target.After;
        var beforeSize = before is null ? null : await _host.ReadFileSizeAsync(before, cancellationToken);
        var afterSize = after is null ? null : await _host.ReadFileSizeAsync(after, cancellationToken);
        if (version != _version)
        {
            return;
        }

        if (IsImage(target.Path))
        {
            var beforeImage = before is null ? null : await _host.ReadFileAsync(before, MaxImageBytes, cancellationToken);
            var afterImage = after is null ? null : await _host.ReadFileAsync(after, MaxImageBytes, cancellationToken);
            if (version != _version)
            {
                return;
            }

            BeforeImage = beforeImage;
            AfterImage = afterImage;
            BeforeBytes = beforeSize;
            AfterBytes = afterSize;
            Body = DiffBody.Image;
            return;
        }

        BeforeSizeText = beforeSize is { } b ? $"Before: {Bytes(b)}" : null;
        AfterSizeText = afterSize is { } a ? $"After: {Bytes(a)}" : null;
        Body = DiffBody.Binary;
    }

    /// <summary>A number of bytes as the diff view writes it: "1 byte", "7,028 bytes".</summary>
    public static string Bytes(long count) =>
        count == 1 ? "1 byte" : string.Create(CultureInfo.InvariantCulture, $"{count:N0} bytes");

    private static bool IsImage(string path) =>
        ImageExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
}
