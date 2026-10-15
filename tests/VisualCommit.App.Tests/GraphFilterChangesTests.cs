using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What hiding and soloing do to a repository's filter (docs/phase-5-design.md 8.1, 8.4, D159,
/// D178, report part F's FD14): a local branch's remote-tracking upstream following it and showing
/// again with it unless toggled on its own since, a local upstream never following, soloing a
/// hidden branch and hiding a soloed one, the sections' items, hidden remotes expanded to their
/// branches at each read, names gone at the first read, the app's own rename, delete and create;
/// and the session's store of them (<see cref="GraphFilterStore"/>): an empty filter removed, the
/// 100 most recently used kept, folders gone at start-up dropped, an older file read.
/// </summary>
public class GraphFilterChangesTests
{
    private const string Alpha = "refs/heads/feature/alpha";
    private const string OriginAlpha = "refs/remotes/origin/feature/alpha";
    private const string Beta = "refs/heads/feature/beta";
    private const string OriginBeta = "refs/remotes/origin/feature/beta";
    private const string Local = "refs/heads/local";
    private const string OriginMain = "refs/remotes/origin/main";
    private const string MirrorMain = "refs/remotes/mirror/main";

    private static readonly RepoRefs Refs = new(
        new HeadState("main", "aaaa"),
        [
            new GitRef(Alpha, "feature/alpha", RefKind.LocalBranch, "a1", Upstream: "origin/feature/alpha", UpstreamRef: OriginAlpha, UpstreamRemote: "origin"),
            new GitRef(Beta, "feature/beta", RefKind.LocalBranch, "b1", Upstream: "origin/feature/beta", UpstreamRef: OriginBeta, UpstreamRemote: "origin"),
            new GitRef("refs/heads/main", "main", RefKind.LocalBranch, "aaaa", IsHead: true, Upstream: "origin/main", UpstreamRef: OriginMain, UpstreamRemote: "origin"),
            new GitRef(Local, "local", RefKind.LocalBranch, "c1", Upstream: "main", UpstreamRef: "refs/heads/main", UpstreamRemote: "."),
            new GitRef(MirrorMain, "mirror/main", RefKind.RemoteBranch, "aaaa", RemoteName: "mirror"),
            new GitRef(OriginAlpha, "origin/feature/alpha", RefKind.RemoteBranch, "a2", RemoteName: "origin"),
            new GitRef(OriginBeta, "origin/feature/beta", RefKind.RemoteBranch, "b1", RemoteName: "origin"),
            new GitRef(OriginMain, "origin/main", RefKind.RemoteBranch, "aaaa", RemoteName: "origin"),
            new GitRef("refs/tags/v1", "v1", RefKind.Tag, "aaaa"),
        ],
        [],
        ["mirror", "origin"]);

    [Fact]
    public void Hiding_a_local_branch_hides_its_remote_tracking_upstream_and_showing_it_shows_both()
    {
        var hidden = GraphFilterChanges.Hide(null, Refs, Alpha)!;
        Assert.Equal([Alpha, OriginAlpha], hidden.Hidden);
        Assert.Equal([new BranchUpstream(Alpha, OriginAlpha)], hidden.Upstreams);
        Assert.Equal(["mirror", "origin"], hidden.Remotes);
        Assert.Equal(
            ["--exclude=feature/alpha", "--branches", "--exclude=origin/feature/alpha", "--exclude=origin/HEAD", "--remotes", "--tags", "--ignore-missing", "HEAD", "--"],
            GraphFilterChanges.ToGraphFilter(hidden)!.RevisionArguments);

        // Showing it undoes both, and a filter that hides nothing is none (FD14).
        Assert.Null(GraphFilterChanges.Show(hidden, Refs, Alpha));

        // A local upstream (D101) never follows.
        Assert.Equal([Local], GraphFilterChanges.Hide(null, Refs, Local)!.Hidden);
        Assert.Empty(GraphFilterChanges.Hide(null, Refs, Local)!.Upstreams);
    }

    [Fact]
    public void An_upstream_shown_or_hidden_on_its_own_no_longer_follows_its_branch()
    {
        var hidden = GraphFilterChanges.Hide(null, Refs, Alpha);
        var upstreamShown = GraphFilterChanges.Show(hidden, Refs, OriginAlpha)!;
        Assert.Equal([Alpha], upstreamShown.Hidden);
        Assert.Empty(upstreamShown.Upstreams);

        var hiddenAgain = GraphFilterChanges.Hide(upstreamShown, Refs, OriginAlpha);
        var branchShown = GraphFilterChanges.Show(hiddenAgain, Refs, Alpha)!;
        Assert.Equal([OriginAlpha], branchShown.Hidden);
    }

    [Fact]
    public void Soloing_shows_a_hidden_branch_hiding_ends_a_solo_and_the_sections_end_each_kind()
    {
        var hidden = GraphFilterChanges.Hide(null, Refs, Alpha);
        var soloed = GraphFilterChanges.Solo(hidden, Refs, Alpha)!;
        Assert.Empty(soloed.Hidden);
        Assert.Equal([Alpha, OriginAlpha], soloed.Soloed);
        Assert.Equal(
            ["--ignore-missing", Alpha, OriginAlpha, "HEAD", "--"],
            GraphFilterChanges.ToGraphFilter(soloed)!.RevisionArguments);

        var both = GraphFilterChanges.Hide(GraphFilterChanges.Solo(null, Refs, Beta), Refs, Alpha)!;
        Assert.Equal([Alpha, OriginAlpha], both.Hidden);
        Assert.Equal([Beta, OriginBeta], both.Soloed);

        // Hiding a soloed branch ends its solo, and its upstream's.
        var hiddenBeta = GraphFilterChanges.Hide(both, Refs, Beta)!;
        Assert.Empty(hiddenBeta.Soloed);
        Assert.Equal([Alpha, Beta, OriginAlpha, OriginBeta], hiddenBeta.Hidden);

        // The sections' items: solo mode stays when the hidden ones go, and the reverse.
        var showAll = GraphFilterChanges.ShowAllHidden(both, Refs)!;
        Assert.Empty(showAll.Hidden);
        Assert.Equal([Beta, OriginBeta], showAll.Soloed);
        Assert.Equal([new BranchUpstream(Beta, OriginBeta)], showAll.Upstreams);
        var stopSoloing = GraphFilterChanges.StopSoloingAll(both, Refs)!;
        Assert.Empty(stopSoloing.Soloed);
        Assert.Equal([Alpha, OriginAlpha], stopSoloing.Hidden);
        Assert.Null(GraphFilterChanges.StopSoloing(GraphFilterChanges.Solo(null, Refs, Beta), Refs, Beta));
    }

    [Fact]
    public void A_hidden_remote_hides_every_branch_of_it_now_and_after_a_fetch_and_showing_it_shows_them()
    {
        var hidden = GraphFilterChanges.HideRemote(null, Refs, "origin")!;
        Assert.Equal(["origin"], hidden.HiddenRemotes);
        Assert.Equal([OriginAlpha, OriginBeta, OriginMain], hidden.Hidden);

        // A fetch brought a branch, and one went: the next read corrects the names.
        var fetched = Refs with
        {
            Refs = [.. Refs.Refs.Where(gitRef => gitRef.FullName != OriginBeta), new GitRef("refs/remotes/origin/new", "origin/new", RefKind.RemoteBranch, "d1", RemoteName: "origin")],
        };
        var corrected = GraphFilterChanges.Correct(hidden, fetched, dropMissing: false)!;
        Assert.Equal([OriginAlpha, OriginMain, "refs/remotes/origin/new"], corrected.Hidden);
        Assert.Same(corrected, GraphFilterChanges.Correct(corrected, fetched, dropMissing: false));

        Assert.Null(GraphFilterChanges.ShowRemote(corrected, fetched, "origin"));

        // Showing one branch of it on its own: the remote is no longer hidden as a whole.
        var one = GraphFilterChanges.Show(hidden, Refs, OriginMain)!;
        Assert.Empty(one.HiddenRemotes);
        Assert.Equal([OriginAlpha, OriginBeta], one.Hidden);
    }

    [Fact]
    public void The_first_read_drops_names_that_no_longer_exist_and_ends_solo_mode_with_them()
    {
        var filter = new RepositoryGraphFilter("C:/repo", ["refs/heads/gone", Alpha], ["refs/heads/old"], ["upstream"], [new BranchUpstream("refs/heads/gone", OriginAlpha)], ["origin"], default);
        var corrected = GraphFilterChanges.Correct(filter, Refs, dropMissing: true)!;
        Assert.Equal([Alpha], corrected.Hidden);
        Assert.Empty(corrected.Soloed);
        Assert.Empty(corrected.HiddenRemotes);
        Assert.Empty(corrected.Upstreams);
        Assert.Equal(["mirror", "origin"], corrected.Remotes);
        Assert.Equal("C:/repo", corrected.RepositoryPath);

        // Later reads leave stale names alone: a branch deleted and re-created is hidden again (0.6).
        Assert.Contains("refs/heads/gone", GraphFilterChanges.Correct(filter, Refs, dropMissing: false)!.Hidden);
    }

    [Fact]
    public void The_apps_own_rename_delete_and_create_keep_the_filter_right()
    {
        var hidden = GraphFilterChanges.Hide(null, Refs, Alpha);
        var renamed = GraphFilterChanges.Renamed(hidden, Alpha, "refs/heads/feature/omega")!;
        Assert.Equal(["refs/heads/feature/omega", OriginAlpha], renamed.Hidden);
        Assert.Equal([new BranchUpstream("refs/heads/feature/omega", OriginAlpha)], renamed.Upstreams);

        var deleted = GraphFilterChanges.Deleted(renamed, "refs/heads/feature/omega")!;
        Assert.Equal([OriginAlpha], deleted.Hidden);
        Assert.Empty(deleted.Upstreams);

        // In solo mode a branch the app creates is soloed too; otherwise nothing changes.
        var soloed = GraphFilterChanges.Solo(null, Refs, Beta);
        Assert.Equal([Beta, OriginBeta, "refs/heads/new"], GraphFilterChanges.Created(soloed, "refs/heads/new")!.Soloed);
        Assert.Same(hidden, GraphFilterChanges.Created(hidden, "refs/heads/new"));
        Assert.Null(GraphFilterChanges.Created(null, "refs/heads/new"));
    }

    [Fact]
    public void The_store_keeps_one_entry_per_repository_removes_an_empty_one_and_bounds_the_list()
    {
        using var folder = new TempDirectory("repo");
        var clock = new ManualTimeProvider();
        var session = new MemorySession();
        var store = new GraphFilterStore(session, clock);
        var hidden = GraphFilterChanges.Hide(null, Refs, Alpha);

        var kept = store.Write(folder.Path, hidden)!;
        Assert.Equal(folder.Path, kept.RepositoryPath);
        Assert.Equal(clock.GetUtcNow(), kept.LastUsed);
        Assert.Same(kept, store.Find(folder.Path + Path.DirectorySeparatorChar));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(clock.GetUtcNow(), store.Touch(folder.Path)!.LastUsed);
        Assert.Single(session.Current.GraphFilters);

        // Nothing hidden or soloed: the entry goes (FD14).
        Assert.Null(store.Write(folder.Path, GraphFilterChanges.Show(kept, Refs, Alpha)));
        Assert.Empty(session.Current.GraphFilters);

        // The 100 most recently used repositories' filters, whatever the recent list holds.
        for (var i = 0; i < SessionState.MaxGraphFilters + 5; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            store.Write(Path.Combine(folder.Path, i.ToString(System.Globalization.CultureInfo.InvariantCulture)), hidden);
        }

        Assert.Equal(SessionState.MaxGraphFilters, session.Current.GraphFilters.Count);
        Assert.Null(store.Find(Path.Combine(folder.Path, "0")));
        Assert.NotNull(store.Find(Path.Combine(folder.Path, "104")));
    }

    [Fact]
    public void At_start_up_the_entries_of_folders_that_no_longer_exist_are_dropped()
    {
        using var folder = new TempDirectory("repo");
        var hidden = GraphFilterChanges.Hide(null, Refs, Alpha)!;
        var session = new MemorySession();
        session.Update(state => state with
        {
            GraphFilters = [hidden with { RepositoryPath = folder.Path }, hidden with { RepositoryPath = Path.Combine(folder.Path, "gone") }],
        });

        _ = new ViewModels.TabServices(new NoRepositories(), new NoFolders(), new MainWindowViewModelTests.FakeSettings(), session, new DateDisplay(TimeZoneInfo.Utc));
        Assert.Equal([folder.Path], session.Current.GraphFilters.Select(entry => entry.RepositoryPath));
    }

    [Fact]
    public void A_session_file_without_filters_or_with_lists_left_out_is_read()
    {
        using var data = new TempDirectory("data");
        var file = Path.Combine(data.Path, "session.json");
        File.WriteAllText(file, """{ "tabs": [], "recent": [] }""");
        Assert.Empty(new JsonSessionStore(file).Current.GraphFilters);

        File.WriteAllText(file, """{ "graphFilters": [ null, { "repositoryPath": "C:/repo", "hidden": [ "refs/heads/x" ] }, { "hidden": [] } ] }""");
        var entry = Assert.Single(new JsonSessionStore(file).Current.GraphFilters);
        Assert.Equal("C:/repo", entry.RepositoryPath);
        Assert.Equal(["refs/heads/x"], entry.Hidden);
        Assert.Empty(entry.Soloed);
        Assert.Empty(entry.HiddenRemotes);
        Assert.Empty(entry.Upstreams);
        Assert.Empty(entry.Remotes);

        // Written and read again as it was.
        var hidden = GraphFilterChanges.Hide(null, Refs, Alpha)! with { RepositoryPath = "C:/other" };
        JsonSessionStore.Write(file, new SessionState { GraphFilters = [hidden] });
        Assert.True(GraphFilterChanges.Same(hidden, Assert.Single(new JsonSessionStore(file).Current.GraphFilters)));
    }

    private sealed class MemorySession : ISessionStore
    {
        public SessionState Current { get; private set; } = new();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
