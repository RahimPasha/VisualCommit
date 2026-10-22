using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.WorkspaceTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// A saved folder sorted before anything reads it (docs/phase-6-design.md 1.10, D204): ready, gone,
/// no repository (also a bare one, an orphaned worktree, a folder inside another working tree,
/// whose outer repository is dropped unread), or refused by git, each with its short text and
/// tooltip; the existence check off the UI thread.
/// </summary>
public class WorkspaceFolderProbeTests
{
    private static readonly CancellationToken Cancelled = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_working_trees_top_is_ready_with_its_repository()
    {
        using var tree = await TreeAsync();
        var session = NewSession();

        var probe = await WorkspaceFolderProbe.ProbeAsync(session.Services.Repositories, tree.Api.Path, Cancelled);

        Assert.Equal(WorkspaceFolderState.Ready, probe.State);
        Assert.NotNull(probe.Repository);
        Assert.Null(probe.ShortText);
        Assert.Null(probe.ToolTip);
    }

    [Fact]
    public async Task A_folder_that_is_gone_is_not_found_without_a_git_call()
    {
        using var tree = await TreeAsync();
        var gone = tree.PathOf("old-site");
        var session = NewSession();

        var probe = await WorkspaceFolderProbe.ProbeAsync(session.Services.Repositories, gone, Cancelled);

        Assert.Equal((WorkspaceFolderState.FolderNotFound, "Folder not found", $"{gone} does not exist"), (probe.State, probe.ShortText, probe.ToolTip));
        Assert.Null(probe.Repository);
        Assert.Empty(session.Calls.Snapshot());
    }

    [Fact]
    public async Task A_path_that_is_not_fully_qualified_is_not_found_without_asking_the_file_system()
    {
        var asked = 0;
        var session = NewSession();

        var probe = await WorkspaceFolderProbe.ProbeAsync(session.Services.Repositories, Path.Combine("src", "api"), Cancelled, _ => { asked++; return true; });

        Assert.Equal(WorkspaceFolderState.FolderNotFound, probe.State);
        Assert.Equal($"{Path.Combine("src", "api")} does not exist", probe.ToolTip);
        Assert.Equal(0, asked);
        Assert.Empty(session.Calls.Snapshot());
    }

    [Fact]
    public async Task A_plain_folder_a_bare_repository_and_an_orphaned_worktree_are_no_repositories()
    {
        using var tree = await TreeAsync();
        var plain = tree.Folder("plain");
        var bare = tree.PathOf("remotes/api.git");
        await tree.Api.GitAsync("init", "--quiet", "--bare", bare);
        var orphan = tree.PathOf("orphan");
        await tree.Api.GitAsync("worktree", "add", "--quiet", "-b", "orphan", orphan);
        Directory.Delete(Path.Combine(tree.Api.Path, ".git", "worktrees", "orphan"), recursive: true);
        var session = NewSession();

        foreach (var folder in new[] { plain, bare, orphan })
        {
            var probe = await WorkspaceFolderProbe.ProbeAsync(session.Services.Repositories, folder, Cancelled);
            Assert.Equal((WorkspaceFolderState.NotARepository, "Not a repository", $"{folder} is not a git repository"), (probe.State, probe.ShortText, probe.ToolTip));
            Assert.Null(probe.Repository);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_folder_inside_another_working_tree_names_it_and_reads_nothing_more_of_it(bool emptyGitFolder)
    {
        using var tree = await TreeAsync();
        var inside = Directory.CreateDirectory(Path.Combine(tree.Api.Path, "inner")).FullName;
        if (emptyGitFolder)
        {
            // A folder whose own .git is empty: git passes over it and opens the outer repository (0.8).
            Directory.CreateDirectory(Path.Combine(inside, ".git"));
        }

        var session = NewSession();
        var outer = (await session.Services.Repositories.OpenAsync(tree.Api.Path, Cancelled)).WorkingDirectory;
        var mark = session.Calls.Snapshot().Count;

        var probe = await WorkspaceFolderProbe.ProbeAsync(session.Services.Repositories, inside, Cancelled);

        Assert.Equal(WorkspaceFolderState.NotARepository, probe.State);
        Assert.Equal("Not a repository", probe.ShortText);
        Assert.Equal($"{inside} is not a git repository of its own: it lies inside {outer}", probe.ToolTip);
        Assert.Null(probe.Repository);

        // The opening's calls, then --show-prefix, and nothing after it: the outer repository is dropped unread.
        var calls = session.Calls.Snapshot().Skip(mark).Select(call => call.CommandText).ToList();
        Assert.All(calls, call => Assert.StartsWith("git rev-parse ", call, StringComparison.Ordinal));
        Assert.Equal("git rev-parse --show-prefix", calls[^1]);
        Assert.StartsWith("git rev-parse --is-inside-work-tree", calls[0], StringComparison.Ordinal);
        Assert.InRange(calls.Count, 2, 3);
    }

    [Fact]
    public async Task Gits_refusal_is_not_opened_with_gits_first_line_and_no_usable_git_with_its_message()
    {
        var folder = Path.GetTempPath();
        var refusal = new GitException(
            "git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir",
            128,
            "\nfatal: detected dubious ownership in repository at 'D:/src/api'\nTo add an exception for this directory, call:\n");

        var probe = await WorkspaceFolderProbe.ProbeAsync(new Throwing(refusal), folder, Cancelled);
        Assert.Equal(
            (WorkspaceFolderState.NotOpened, "Git could not open it", $"Git could not open {folder}: fatal: detected dubious ownership in repository at 'D:/src/api'"),
            (probe.State, probe.ShortText, probe.ToolTip));

        probe = await WorkspaceFolderProbe.ProbeAsync(new Throwing(new GitUnavailableException("Git was not found on this computer.")), folder, Cancelled);
        Assert.Equal((WorkspaceFolderState.NotOpened, $"Git could not open {folder}: Git was not found on this computer."), (probe.State, probe.ToolTip));

        // A refusal of the top's question counts the same.
        probe = await WorkspaceFolderProbe.ProbeAsync(new Opening(new RefusingTop(folder, refusal)), folder, Cancelled);
        Assert.Equal(WorkspaceFolderState.NotOpened, probe.State);
        Assert.Null(probe.Repository);
    }

    [Fact]
    public async Task A_cancelled_probe_is_the_callers()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WorkspaceFolderProbe.ProbeAsync(NewSession().Services.Repositories, Path.GetTempPath(), source.Token));
    }

    [AvaloniaFact]
    public async Task The_existence_check_runs_off_the_ui_thread()
    {
        bool? onUiThread = null;
        var folder = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "not-there");

        var probe = await WorkspaceFolderProbe.ProbeAsync(
            NewSession().Services.Repositories,
            folder,
            Cancelled,
            path =>
            {
                onUiThread = Dispatcher.UIThread.CheckAccess();
                return false;
            });

        Assert.Equal(WorkspaceFolderState.FolderNotFound, probe.State);
        Assert.False(onUiThread);
    }

    /// <summary>A provider whose every open fails with <paramref name="failure"/>.</summary>
    private sealed class Throwing(Exception failure) : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => Task.FromException<IGitRepository>(failure);

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    /// <summary>A provider that opens <paramref name="repository"/> whatever the folder.</summary>
    private sealed class Opening(IGitRepository repository) : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(repository);

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string?>? environment = null) =>
            throw new NotSupportedException();
    }

    /// <summary>A repository whose question about its top fails with <paramref name="failure"/>.</summary>
    private sealed class RefusingTop(string folder, Exception failure) : FakeRepositoryBase
    {
        public override string WorkingDirectory => folder;

        public override string GitDirectory => Path.Combine(folder, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => "x";

        public override Task<bool> OpenedAtTopAsync(CancellationToken cancellationToken = default) => Task.FromException<bool>(failure);

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override IRepositoryWatcher CreateWatcher() => throw new NotSupportedException();
    }
}
