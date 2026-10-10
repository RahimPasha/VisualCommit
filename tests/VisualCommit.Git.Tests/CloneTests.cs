using System.Collections.Concurrent;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="GitRepository.CloneAsync"/>, from local repositories through file:// URLs.</summary>
public class CloneTests(CloneTests.Source source) : IClassFixture<CloneTests.Source>
{
    /// <summary>The repository the tests clone, built once; cloning only reads it.</summary>
    public sealed class Source : IAsyncLifetime
    {
        public TempRepo Repo { get; private set; } = null!;

        public string Head { get; private set; } = string.Empty;

        /// <summary>A file:// URL, so that git clones through its transport, as from a server, and reports progress.</summary>
        public string Url => UrlOf(Repo.Path);

        public async ValueTask InitializeAsync()
        {
            Repo = await TempRepo.CreateAsync("source");
            await Repo.CommitFileAsync("README.md", "# Source\n", "Add README");
            Head = await Repo.CommitFileAsync("src/code.txt", "code\n", "Add code");
        }

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static string UrlOf(string path) => new Uri(path).AbsoluteUri;

    [Fact]
    public async Task Clones_reports_progress_and_opens_the_result()
    {
        using var target = new TempDirectory("clone");
        var destination = target.Combine("parent", "copy");
        var progress = new CollectingProgress();

        var clone = await GitRepository.CloneAsync(NewRunner(), source.Url, destination, progress, TestCancelled);

        AssertSameFolder(destination, clone.WorkingDirectory);
        Assert.Equal("copy", clone.Name);
        Assert.Equal("# Source\n", File.ReadAllText(Path.Combine(destination, "README.md")));

        var reports = progress.Reports;
        Assert.NotEmpty(reports);
        Assert.Contains(reports, report => report.Stage == "Cloning into 'copy'...");
        Assert.Contains(reports, report => report.Percent is not null);
        Assert.DoesNotContain(reports, report => report.Text.StartsWith("remote:", StringComparison.Ordinal));

        var refs = await clone.ReadRefsAsync(TestCancelled);
        Assert.Equal(new HeadState("main", source.Head), refs.Head);
        Assert.Equal(["origin"], refs.Remotes);
        Assert.Contains(refs.Refs, gitRef => gitRef.Name == "origin/main" && gitRef.TargetSha == source.Head);
        Assert.Contains(refs.Refs, gitRef => gitRef is { Name: "main", Upstream: "origin/main", IsHead: true });
    }

    [Fact]
    public async Task Clones_into_an_existing_empty_folder()
    {
        using var target = new TempDirectory("clone");
        var destination = target.Combine("empty");
        Directory.CreateDirectory(destination);

        var clone = await GitRepository.CloneAsync(NewRunner(), source.Url, destination, null, TestCancelled);

        AssertSameFolder(destination, clone.WorkingDirectory);
    }

    [Fact]
    public async Task A_folder_that_is_not_empty_is_refused_and_left_alone()
    {
        using var target = new TempDirectory("clone");
        var destination = target.Combine("busy");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "keep.txt"), "mine");

        await Assert.ThrowsAsync<IOException>(
            () => GitRepository.CloneAsync(NewRunner(), source.Url, destination, null, TestCancelled));

        Assert.Equal(["keep.txt"], Directory.EnumerateFileSystemEntries(destination).Select(Path.GetFileName));
    }

    [Fact]
    public async Task A_failing_clone_reports_gits_message_and_removes_what_it_created()
    {
        using var target = new TempDirectory("clone");
        var missing = UrlOf(target.Combine("no-such-repository"));
        var destination = target.Combine("parent", "copy");
        var emptyFolder = target.Combine("kept");
        Directory.CreateDirectory(emptyFolder);

        var exception = await Assert.ThrowsAsync<GitException>(
            () => GitRepository.CloneAsync(NewRunner(), missing, destination, null, TestCancelled));
        await Assert.ThrowsAsync<GitException>(
            () => GitRepository.CloneAsync(NewRunner(), missing, emptyFolder, null, TestCancelled));

        Assert.StartsWith("fatal:", exception.StandardError);
        Assert.Contains("no-such-repository", exception.Message);
        Assert.DoesNotContain("Cloning into", exception.StandardError);
        Assert.False(Directory.Exists(destination));
        Assert.True(Directory.Exists(emptyFolder));
        Assert.Empty(Directory.EnumerateFileSystemEntries(emptyFolder));
    }

    [Fact]
    public async Task A_cancelled_clone_removes_what_it_created()
    {
        using var target = new TempDirectory("clone");
        var destination = target.Combine("copy");
        var emptyFolder = target.Combine("kept");
        Directory.CreateDirectory(emptyFolder);

        foreach (var folder in new[] { destination, emptyFolder })
        {
            // Cancelled as soon as git reports that it has started, while it still works.
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
            var progress = new CollectingProgress(onReport: cancellation.Cancel);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => GitRepository.CloneAsync(NewRunner(), source.Url, folder, progress, cancellation.Token));
        }

        Assert.False(Directory.Exists(destination));
        Assert.True(Directory.Exists(emptyFolder));
        Assert.Empty(Directory.EnumerateFileSystemEntries(emptyFolder));
    }

    /// <summary>Keeps every report, on the thread that reports it (unlike <see cref="Progress{T}"/>, which posts them).</summary>
    private sealed class CollectingProgress(Action? onReport = null) : IProgress<CloneProgress>
    {
        private readonly ConcurrentQueue<CloneProgress> _reports = new();

        public IReadOnlyList<CloneProgress> Reports => [.. _reports];

        public void Report(CloneProgress value)
        {
            _reports.Enqueue(value);
            onReport?.Invoke();
        }
    }
}
