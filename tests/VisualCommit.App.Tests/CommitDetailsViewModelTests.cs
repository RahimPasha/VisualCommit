using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The commit details' view model (no UI), against "Commit details" and checks 3 and 4 of
/// docs/test-reports/phase-1.md: with the graph scenario read by real git, and with a fake
/// repository for loading, cancelling and failing.
/// </summary>
public class CommitDetailsViewModelTests(CommitDetailsViewModelTests.GraphScenario scenario)
    : IClassFixture<CommitDetailsViewModelTests.GraphScenario>
{
    /// <summary>The graph scenario, opened once for every test of the class; the tests only read it.</summary>
    public sealed class GraphScenario : IAsyncLifetime
    {
        private static readonly IGitRunner Runner = new GitRunner(
            GitLocator.FindExecutable(GitSearchContext.FromSystem())
            ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

        public TempRepo Repo { get; private set; } = null!;

        public IGitRepository Repository { get; private set; } = null!;

        public RepoRefs Refs { get; private set; } = null!;

        public async ValueTask InitializeAsync()
        {
            Repo = await Scenarios.GraphAsync();
            Repository = await GitRepository.OpenAsync(Runner, Repo.Path, TestContext.Current.CancellationToken);
            Refs = await Repository.ReadRefsAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>The full id of a commit named by the short id the report gives it.</summary>
        public async Task<string> FullShaAsync(string shortSha) =>
            (await Repo.GitAsync("rev-parse", "--verify", shortSha + "^{commit}")).StandardOutput.Trim();

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static readonly DateDisplay Utc = new(TimeZoneInfo.Utc);

    [Fact]
    public async Task Shows_the_details_of_Add_settings_page_with_its_files_flat_in_gits_order()
    {
        var details = Create(out _);
        var sha = await scenario.FullShaAsync("47e6ec7");

        await details.ShowAsync(sha, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(details.HasCommit);
        Assert.False(details.ShowsPlaceholder);
        Assert.False(details.HasError);
        Assert.Equal("Add settings page", details.Subject);
        Assert.Equal("The settings page lists the defaults.\nIt replaces the old notes.", details.Body);
        Assert.True(details.HasBody);
        Assert.Equal("Test Author <author@example.com>", details.AuthorText);
        Assert.Equal("2026-01-01 12:10", details.DateText);
        Assert.False(details.HasCommitter);
        Assert.False(details.HasCommitDate);
        Assert.Equal(sha, details.Sha);
        Assert.Equal(40, sha.Length);
        Assert.Equal(["2775228"], details.Parents.Select(parent => parent.ShortSha));
        Assert.False(details.IsStash);
        Assert.Equal("Changed files (5)", details.ChangedFilesTitle);
        Assert.True(details.IsFlat);
        Assert.Equal(
            [
                "M README.md",
                "D notes.txt | docs",
                "R main-app.txt | src | renamed from src/app.txt",
                "A defaults.txt | src/settings",
                "A page.txt | src/settings",
            ],
            Describe(details));
    }

    [Fact]
    public async Task The_tree_lists_folders_first_then_files_and_the_choice_is_saved()
    {
        var details = Create(out var settings);
        await details.ShowAsync(await scenario.FullShaAsync("47e6ec7"), cancellationToken: TestContext.Current.CancellationToken);

        details.ShowTreeCommand.Execute(null);

        Assert.True(details.IsTree);
        Assert.Equal(FileListMode.Tree, settings.Current.FileList);
        Assert.Equal(
            [
                "v docs",
                "  D notes.txt",
                "v src",
                "  v settings",
                "    A defaults.txt",
                "    A page.txt",
                "  R main-app.txt | renamed from src/app.txt",
                "M README.md",
            ],
            Describe(details));

        // A new panel on the same settings (a new tab, or the app started again) starts as a tree.
        var next = new CommitDetailsViewModel(scenario.Repository, settings, Utc);
        Assert.True(next.IsTree);
        await next.ShowAsync(await scenario.FullShaAsync("47e6ec7"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("v docs", Describe(next)[0]);

        details.ShowFlatCommand.Execute(null);
        Assert.Equal(FileListMode.Flat, settings.Current.FileList);
        Assert.Equal("M README.md", Describe(details)[0]);
    }

    [Fact]
    public async Task A_folder_of_the_tree_closes_and_opens_again()
    {
        var details = Create(out _, FileListMode.Tree);
        await details.ShowAsync(await scenario.FullShaAsync("47e6ec7"), cancellationToken: TestContext.Current.CancellationToken);
        var src = details.Files.Single(row => row.IsFolder && row.Name == "src");

        src.ToggleCommand.Execute(null);

        Assert.Equal(["v docs", "  D notes.txt", "> src", "M README.md"], Describe(details));

        details.Files.Single(row => row.IsFolder && row.Name == "src").ToggleCommand.Execute(null);

        Assert.Equal(8, details.Files.Count);
        Assert.False(details.Files.Single(row => row.Name == "README.md").ToggleCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_merge_shows_both_parents_and_its_changes_against_the_first()
    {
        var details = Create(out _);

        await details.ShowAsync(await scenario.FullShaAsync("f7919b8"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Merge branch 'feature/login'", details.Subject);
        Assert.Equal(["10bcd42", "4a62ef9"], details.Parents.Select(parent => parent.ShortSha));
        Assert.Equal("Changed files (2)", details.ChangedFilesTitle);
        Assert.Equal(["A login.txt | src", "A rules.txt | src/validation"], Describe(details));
    }

    [Fact]
    public async Task A_stash_shows_its_name_and_only_the_commit_it_was_made_on_as_parent()
    {
        var details = Create(out _);
        var stash = Assert.Single(scenario.Refs.Stashes);

        await details.ShowAsync(stash.Sha, stash, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("On main: Work in progress on README", details.Subject);
        Assert.Equal("95daa96", details.Sha![..7]);
        Assert.True(details.IsStash);
        Assert.Equal("stash@{0}", details.StashName);
        Assert.Equal(["47e6ec7"], details.Parents.Select(parent => parent.ShortSha));
        Assert.Equal("Changed files (1)", details.ChangedFilesTitle);
        Assert.Equal(["M README.md"], Describe(details));
    }

    [Fact]
    public async Task Clicking_a_parent_raises_ParentActivated_with_its_full_id()
    {
        var details = Create(out _);
        var activated = new List<string>();
        details.ParentActivated += (_, sha) => activated.Add(sha);
        await details.ShowAsync(await scenario.FullShaAsync("f7919b8"), cancellationToken: TestContext.Current.CancellationToken);

        details.Parents[1].ActivateCommand.Execute(null);

        Assert.Equal([await scenario.FullShaAsync("4a62ef9")], activated);
    }

    [Fact]
    public async Task Showing_no_commit_brings_back_the_placeholder()
    {
        var details = Create(out _);
        await details.ShowAsync(await scenario.FullShaAsync("47e6ec7"), cancellationToken: TestContext.Current.CancellationToken);

        await details.ShowAsync(null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(details.HasCommit);
        Assert.True(details.ShowsPlaceholder);
        Assert.Null(details.Sha);
        Assert.Empty(details.Files);
        Assert.Empty(details.Parents);
    }

    [Fact]
    public async Task A_panel_without_a_repository_always_shows_the_placeholder()
    {
        var details = new CommitDetailsViewModel(null, new MainWindowViewModelTests.FakeSettings(), Utc);

        await details.ShowAsync(new string('a', 40), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(details.HasCommit);
        Assert.True(details.ShowsPlaceholder);
    }

    [Fact]
    public async Task The_committer_and_commit_date_show_only_when_they_differ_from_the_author()
    {
        var authored = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var repository = new FakeRepository(sha => Task.FromResult(Details(sha) with
        {
            CommitterName = "Someone Else",
            CommitterEmail = "else@example.com",
            AuthorDate = authored,
            CommitDate = authored.AddMinutes(30),
        }));
        var details = new CommitDetailsViewModel(repository, new MainWindowViewModelTests.FakeSettings(), Utc);

        await details.ShowAsync(new string('a', 40), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Someone Else <else@example.com>", details.CommitterText);
        Assert.True(details.HasCommitter);
        Assert.Equal("2026-01-01 12:30", details.CommitDateText);
        Assert.True(details.HasCommitDate);

        // A commit date in another offset that shows as the same minute is not shown twice.
        repository.Read = sha => Task.FromResult(Details(sha) with
        {
            AuthorDate = authored,
            CommitDate = authored.ToOffset(TimeSpan.FromHours(2)),
        });
        await details.ShowAsync(new string('b', 40), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(details.HasCommitter);
        Assert.False(details.HasCommitDate);
    }

    [Fact]
    public async Task A_newer_selection_cancels_an_earlier_one_that_is_still_loading()
    {
        var slow = new TaskCompletionSource<CommitDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new List<CancellationToken>();
        var repository = new FakeRepository((sha, token) =>
        {
            tokens.Add(token);
            return sha.StartsWith('a') ? slow.Task.WaitAsync(token) : Task.FromResult(Details(sha) with { Subject = "The newer one" });
        });
        var details = new CommitDetailsViewModel(repository, new MainWindowViewModelTests.FakeSettings(), Utc);

        var first = details.ShowAsync(new string('a', 40), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(details.IsLoading);
        await details.ShowAsync(new string('b', 40), cancellationToken: TestContext.Current.CancellationToken);
        slow.SetResult(Details(new string('a', 40)) with { Subject = "The earlier one" });
        await first;

        Assert.True(tokens[0].IsCancellationRequested);
        Assert.Equal("The newer one", details.Subject);
        Assert.Equal(new string('b', 40), details.Sha);
        Assert.False(details.IsLoading);
    }

    [Fact]
    public async Task A_failure_is_logged_and_shown_and_not_thrown()
    {
        var log = new RecordingLog();
        var repository = new FakeRepository(_ => Task.FromException<CommitDetails>(
            new GitException("git log -1", 128, "fatal: bad object 0000000\n")));
        var details = new CommitDetailsViewModel(repository, new MainWindowViewModelTests.FakeSettings(), Utc, log);

        await details.ShowAsync(new string('0', 40), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(details.HasCommit);
        Assert.False(details.ShowsPlaceholder);
        Assert.True(details.HasError);
        Assert.Contains("fatal: bad object 0000000", details.ErrorText, StringComparison.Ordinal);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<GitException>(entry.Exception);

        // The next selection that works clears the error.
        repository.Read = sha => Task.FromResult(Details(sha));
        await details.ShowAsync(new string('1', 40), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(details.HasCommit);
        Assert.False(details.HasError);
    }

    private CommitDetailsViewModel Create(out MainWindowViewModelTests.FakeSettings settings, FileListMode mode = FileListMode.Flat)
    {
        settings = new MainWindowViewModelTests.FakeSettings();
        settings.Update(current => current with { FileList = mode });
        return new CommitDetailsViewModel(scenario.Repository, settings, Utc);
    }

    /// <summary>Details with one changed file, for a fake repository to return.</summary>
    internal static CommitDetails Details(string sha) => new(
        sha,
        [new string('p', 40)],
        "Test Author",
        "author@example.com",
        new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        "Test Author",
        "author@example.com",
        new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        "Subject",
        string.Empty,
        [new ChangedFile("file.txt", FileChangeKind.Modified)]);

    /// <summary>
    /// The file rows as the panel shows them: a folder as "v name" (open) or "&gt; name", a
    /// file as its status letter and name, then its folder and where it came from, if any.
    /// </summary>
    private static List<string> Describe(CommitDetailsViewModel details) => details.Files.Select(row =>
        new string(' ', 2 * row.Depth)
        + (row.IsFolder
            ? (row.IsOpen ? "v " : "> ") + row.Name
            : $"{row.StatusLetter} {row.Name}"
              + (row.HasFolder ? " | " + row.Folder : string.Empty)
              + (row.HasOrigin ? " | " + row.OriginText : string.Empty)))
        .ToList();

    /// <summary>A repository that only reads commit details, through a function the test sets.</summary>
    internal sealed class FakeRepository : FakeRepositoryBase
    {
        public FakeRepository(Func<string, Task<CommitDetails>> read)
        {
            Read = read;
        }

        public FakeRepository(Func<string, CancellationToken, Task<CommitDetails>> read)
        {
            ReadWithToken = read;
            Read = sha => throw new InvalidOperationException("Unused.");
        }

        public Func<string, Task<CommitDetails>> Read { get; set; }

        private Func<string, CancellationToken, Task<CommitDetails>>? ReadWithToken { get; }

        public override string WorkingDirectory => "/fake";

        public override string GitDirectory => "/fake/.git";

        public override string CommonDirectory => "/fake/.git";

        public override string Name => "fake";

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) =>
            ReadWithToken is { } read ? read(sha, cancellationToken) : Read(sha);

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override IRepositoryWatcher CreateWatcher() => throw new NotSupportedException();
    }
}
