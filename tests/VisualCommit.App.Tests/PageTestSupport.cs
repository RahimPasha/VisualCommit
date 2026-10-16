using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;

namespace VisualCommit.App.Tests;

/// <summary>
/// A page's host for the pages' view-model tests (docs/phase-5-design.md 11.1, 11.3): it records
/// what a page asks of its repository (the commits shown in the right panel, "Show in graph",
/// closing, the pages opened from it) and answers from what the test set: the commits the graph
/// has, the refs the menus see, a fake repository's reads.
/// </summary>
internal sealed class RecordingPageHost(IGitRepository repository) : IFileHistoryHost, IBlameHost
{
    private readonly CancellationTokenSource _lifetime = new();

    public IGitRepository Repository { get; } = repository;

    public CancellationToken Lifetime => _lifetime.Token;

    public ISettingsStore Settings { get; } = new MainWindowViewModelTests.FakeSettings();

    public DateDisplay Dates { get; } = new(TimeZoneInfo.Utc);

    public RecordingLog RecordedLog { get; } = new();

    public IAppLog Log => RecordedLog;

    public IRepositoryActions Actions { get; } = RecordingActions.Create().Actions;

    /// <summary>The refs the menus and the "from" text see: HEAD on main at <see cref="HeadSha"/> unless a test sets others.</summary>
    public RepoRefs Refs { get; set; } = new(new HeadState("main", PageFakes.HeadSha), [], [], ["origin"]);

    public RepositoryMenuContext MenuContext =>
        new("repo", "/repo", Refs, WorkingTreeStatus.Clean, [], ToolbarMenuState.Disabled, IsOperationRunning: false);

    /// <summary>The commits the graph has: "Show in graph" is enabled for these (and the zero id while working changes show).</summary>
    public HashSet<string> InGraph { get; } = new(StringComparer.Ordinal);

    /// <summary>Every commit a page showed in the right panel, in order.</summary>
    public List<string> Shown { get; } = [];

    /// <summary>Every commit a page showed in the graph, in order.</summary>
    public List<string> ShownInGraph { get; } = [];

    /// <summary>The pages that asked to be closed.</summary>
    public List<IGraphAreaPage> Closed { get; } = [];

    /// <summary>The blames the history page opened.</summary>
    public List<BlameRequest> Blames { get; } = [];

    /// <summary>The histories the blame page opened.</summary>
    public List<FileHistoryRequest> Histories { get; } = [];

    /// <summary>The commits HEAD's history holds, as the graph's walk knows them.</summary>
    public HashSet<string> OnHead { get; } = new(StringComparer.Ordinal);

    public bool HasCommit(string sha) => InGraph.Contains(sha);

    public bool IsOnHead(string sha) => OnHead.Contains(sha);

    public void ShowCommit(string sha) => Shown.Add(sha);

    public void ShowInGraph(string sha) => ShownInGraph.Add(sha);

    public void Close(IGraphAreaPage page) => Closed.Add(page);

    public void OpenBlame(BlameRequest request) => Blames.Add(request);

    public void OpenFileHistory(FileHistoryRequest request) => Histories.Add(request);

    /// <summary>The tab closes: every read of its pages is cancelled.</summary>
    public void CloseTab() => _lifetime.Cancel();
}

/// <summary>A repository whose page reads answer from what the test set (docs/phase-5-design.md 4, 5).</summary>
internal sealed class PageFakeRepository : FakeRepositoryBase
{
    /// <summary>The history's rows, handed over one by one.</summary>
    public List<FileHistoryRow> History { get; } = [];

    /// <summary>When set, the history fails with it after its rows.</summary>
    public Exception? HistoryFailure { get; set; }

    /// <summary>The blame, or a failure.</summary>
    public Func<string?, string, BlameResult> Blame { get; set; } = (_, _) => new BlameResult([]);

    /// <summary>The reflogs by full ref name; a missing one has none.</summary>
    public Dictionary<string, ReflogResult> Reflogs { get; } = new(StringComparer.Ordinal);

    /// <summary>The commits no ref reaches.</summary>
    public HashSet<string> Lost { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, the walk for lost commits waits for it, then answers from <see cref="Lost"/> as it is then.</summary>
    public TaskCompletionSource? LostGate { get; set; }

    /// <summary>How many lost-commit walks waiting at <see cref="LostGate"/> were cancelled.</summary>
    public int CancelledLostWalks => Volatile.Read(ref _cancelledLostWalks);

    private int _cancelledLostWalks;

    /// <summary>The backups.</summary>
    public List<BackupInfo> Backups { get; } = [];

    /// <summary>How many times each read ran.</summary>
    public Dictionary<string, int> Reads { get; } = new(StringComparer.Ordinal);

    /// <summary>The diffs a page asked for.</summary>
    public List<DiffTarget> Diffs { get; } = [];

    public override string WorkingDirectory => "/repo";

    public override string GitDirectory => "/repo/.git";

    public override string CommonDirectory => "/repo/.git";

    public override string Name => "repo";

    public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public override IRepositoryWatcher CreateWatcher() => throw new NotSupportedException();

    public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default)
    {
        Count("details");
        var full = sha == PageFakes.HeadSha + "^" ? PageFakes.ParentSha : sha;
        return Task.FromResult(CommitDetailsViewModelTests.Details(full) with { Subject = PageFakes.SubjectOf(full) });
    }

    public override Task<string?> ResolveAsync(string revision, CancellationToken cancellationToken = default)
    {
        Count("resolve");
        return Task.FromResult<string?>(revision == "HEAD" ? PageFakes.HeadSha : revision);
    }

    public override Task<FileDiff> ReadDiffAsync(DiffTarget target, CancellationToken cancellationToken = default)
    {
        Diffs.Add(target);
        return Task.FromResult(FileDiff.Empty);
    }

    public override async Task ReadFileHistoryAsync(string startSha, string path, Action<FileHistoryRow> onRow, CancellationToken cancellationToken = default)
    {
        Count("history");
        foreach (var row in History)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onRow(row);
        }

        await Task.Yield();
        if (HistoryFailure is { } failure)
        {
            throw failure;
        }
    }

    public override Task<BlameResult> ReadBlameAsync(string? sha, string path, CancellationToken cancellationToken = default)
    {
        Count("blame");
        return Task.FromResult(Blame(sha, path));
    }

    public override Task<ReflogResult> ReadReflogAsync(string fullRef, int limit, CancellationToken cancellationToken = default)
    {
        Count("reflog " + fullRef);
        return Task.FromResult(Reflogs.TryGetValue(fullRef, out var reflog) ? reflog : ReflogResult.Empty);
    }

    public override async Task<IReadOnlySet<string>> FindLostCommitsAsync(IReadOnlyCollection<string> shas, CancellationToken cancellationToken = default)
    {
        Count("lost");
        if (LostGate is { } gate)
        {
            try
            {
                await gate.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref _cancelledLostWalks);
                throw;
            }
        }

        return new HashSet<string>(shas.Where(Lost.Contains), StringComparer.Ordinal);
    }

    public override Task<IReadOnlyList<BackupInfo>> ReadBackupsAsync(CancellationToken cancellationToken = default)
    {
        Count("backups");
        return Task.FromResult<IReadOnlyList<BackupInfo>>([.. Backups]);
    }

    /// <summary>How many times <paramref name="read"/> has run.</summary>
    public int ReadCount(string read)
    {
        lock (Reads)
        {
            return Reads.GetValueOrDefault(read);
        }
    }

    private void Count(string read)
    {
        lock (Reads)
        {
            Reads[read] = Reads.GetValueOrDefault(read) + 1;
        }
    }
}

/// <summary>Made-up commits for the pages' tests: "history"'s SHAs and subjects where they matter.</summary>
internal static class PageFakes
{
    /// <summary>Read whole lines, HEAD's commit.</summary>
    public const string HeadSha = "c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9";

    /// <summary>Stop tracing, HEAD's parent.</summary>
    public const string ParentSha = "20d7fbe203a4a8ceeb5ef00da13ceda91ed70739";

    public const string RenameSha = "0575c7f6631b3ba6c9d623f53b28489405230cfd";

    public const string AddTracingSha = "14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c";

    public const string EmptyInputSha = "365482be62488b4c1259f0fb7dfa1631c797470b";

    public const string AddParserSha = "1d5a7cf999727e7f724054348c81866a62025184";

    public const string InitialSha = "7829335c71bb8f1161b668a29afb602632228425";

    public static readonly string Zero = new('0', 40);

    public static string SubjectOf(string sha) => sha switch
    {
        HeadSha => "Read whole lines",
        ParentSha => "Stop tracing",
        RenameSha => "Rename the parser to reader",
        AddTracingSha => "Add tracing",
        EmptyInputSha => "Handle empty input",
        AddParserSha => "Add the parser",
        InitialSha => "Initial commit",
        _ => "Subject",
    };

    public static CommitInfo Commit(string sha, string author, int minute, params string[] parents) =>
        new(sha, parents, author, author.Split(' ')[0].ToLowerInvariant() + "@example.com", Minute(minute), Minute(minute), SubjectOf(sha));

    public static DateTimeOffset Minute(int minute) => new(2026, 1, 1, 12, minute, 0, TimeSpan.Zero);

    /// <summary>src/Reader.cs's history from HEAD (report part B, "The file histories").</summary>
    public static List<FileHistoryRow> ReaderHistory() =>
    [
        new(Commit(HeadSha, "Jonas Berg", 13, ParentSha), FileChangeKind.Modified, "src/Reader.cs"),
        new(Commit(RenameSha, "Mira Kovac", 6, AddTracingSha), FileChangeKind.Renamed, "src/Reader.cs", "src/Parser.cs"),
        new(Commit(EmptyInputSha, "Jonas Berg", 3, "775113f4ae456d7dc7dc5380c669b0d2950289e2"), FileChangeKind.Modified, "src/Parser.cs"),
        new(Commit(AddParserSha, "Mira Kovac", 1, InitialSha), FileChangeKind.Added, "src/Parser.cs"),
    ];

    public static BlameCommit BlameCommitOf(string sha, string author, int minute, string path, string? previous = null, string? previousPath = null) =>
        new(sha, author, author.Split(' ')[0].ToLowerInvariant() + "@example.com", Minute(minute), SubjectOf(sha), path, previous, previousPath, IsBoundary: false);
}
