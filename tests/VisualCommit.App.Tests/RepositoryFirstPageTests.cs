using System.Diagnostics;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="RepositoryViewModel.FirstPage"/> (docs/phase-8a-design.md 14.2, 14.3.2), the signal the
/// restore order waits for: it completes when the first full start's history has its first page in
/// the graph, not before, or when that start ends without one (no commits, the refs or the load
/// failing, the view model closed), never faulted; a summary-mode view model's waits for the full
/// start after its promotion. A view model over a fake without a window, as
/// <c>RepositoryViewModelFilterTests</c> makes one: on the thread pool, its pages are appended where
/// the fake hands them.
/// </summary>
public class RepositoryFirstPageTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>How the first start ends without a first page.</summary>
    public enum NoPage
    {
        NoCommits,
        RefsFail,
        LoadFails,
    }

    [Fact]
    public async Task FirstPage_completes_when_the_first_page_is_in_the_graph_and_not_before()
    {
        var repository = new RestoreFake(Folder("first-page"));
        using var viewModel = NewViewModel(repository);
        var starting = viewModel.StartAsync();
        await WaitUntilAsync(() => viewModel.Refs is not null && !ReferenceEquals(viewModel.Graph, CommitGraphData.Empty), "the refs to be read");
        Assert.False(viewModel.FirstPage.IsCompleted);

        repository.SendFirstPage();
        await viewModel.FirstPage.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal(1, viewModel.Graph.Count);
        Assert.False(viewModel.Graph.IsComplete);
        repository.End();
        await starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(NoPage.NoCommits)]
    [InlineData(NoPage.RefsFail)]
    [InlineData(NoPage.LoadFails)]
    public async Task FirstPage_completes_when_the_first_load_ends_without_a_page(NoPage how)
    {
        var history = how switch
        {
            NoPage.NoCommits => RestoreHistory.NoCommits,
            NoPage.RefsFail => RestoreHistory.RefsFail,
            _ => RestoreHistory.LoadFails,
        };
        var repository = new RestoreFake(Folder("no-page"), history);
        using var viewModel = NewViewModel(repository);
        var starting = viewModel.StartAsync();
        repository.End();

        await viewModel.FirstPage.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.True(viewModel.FirstPage.IsCompletedSuccessfully);
        Assert.Equal(0, viewModel.Graph.Count);
        if (how == NoPage.NoCommits)
        {
            Assert.True(viewModel.HasNoCommits);
            Assert.Null(viewModel.ErrorText);
        }
        else
        {
            Assert.NotNull(viewModel.ErrorText);
        }

        await starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FirstPage_completes_when_the_view_model_closes_before_a_page()
    {
        var repository = new RestoreFake(Folder("closed"));
        var viewModel = NewViewModel(repository);
        var starting = viewModel.StartAsync();
        await WaitUntilAsync(() => viewModel.Refs is not null, "the refs to be read");
        Assert.False(viewModel.FirstPage.IsCompleted);

        viewModel.Dispose();

        await viewModel.FirstPage.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.True(viewModel.FirstPage.IsCompletedSuccessfully);
        Assert.Equal(0, viewModel.Graph.Count);
        await starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_summary_view_models_FirstPage_waits_for_its_promoted_start()
    {
        var repository = new RestoreFake(Folder("summary"));
        var settings = new MainWindowViewModelTests.FakeSettings();
        var services = new TabServices(new HeldRestores(), new FakeFolderPicker(), settings, new DashboardTestSupport.MemorySession(), new DateDisplay(TimeZoneInfo.Utc));
        using var viewModel = RepositoryViewModel.ForSummary(repository, services);

        await viewModel.StartAsync().WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.True(viewModel.FirstRead.IsCompleted);
        Assert.False(viewModel.FirstPage.IsCompleted);

        viewModel.Promote(Stopwatch.StartNew());
        var starting = viewModel.StartAsync();
        await WaitUntilAsync(() => !ReferenceEquals(viewModel.Graph, CommitGraphData.Empty), "the promoted start's refs to be read");
        Assert.False(viewModel.FirstPage.IsCompleted);

        repository.SendFirstPage();
        await viewModel.FirstPage.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal(1, viewModel.Graph.Count);
        repository.End();
        await starting.WaitAsync(Bound, TestContext.Current.CancellationToken);
    }

    private static RepositoryViewModel NewViewModel(RestoreFake repository) =>
        new(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));

    private static string Folder(string name) => Path.Combine(Path.GetTempPath(), "restored", name);

    /// <summary>Polls <paramref name="condition"/> every 10 ms; fails after <see cref="Bound"/>.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var waited = Stopwatch.StartNew();
        while (!condition())
        {
            if (waited.Elapsed > Bound)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
