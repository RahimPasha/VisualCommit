using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A save of git's settings tells every open repository view model (docs/phase-6-design.md 8.11):
/// while files conflict, <c>merge.tool</c> is read again; never in summary mode, never once closed.
/// Each case that reads nothing has a positive control on the same services: a tab with conflicts,
/// made after the subject (so told after it), whose read of <c>merge.tool</c> the test awaits before
/// it counts the subject's.
/// </summary>
public class GitSettingsChangedTests
{
    private static readonly WorkingTreeStatus Conflicted = new([], [new ChangedFile("notes.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal))
    {
        Conflicts = [new ConflictEntry("notes.txt", ConflictKind.BothModified, false, "100644", "100644", "100644", "1", "2", "3")],
    };

    [AvaloniaFact]
    public async Task A_tab_with_conflicts_reads_the_merge_tool_again_after_a_save()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("conflicts", status: Conflicted);
        var services = DashboardTestSupport.Services(provider);
        using var tab = Full(services, repository);
        await tab.StartAsync();
        await DashboardTestSupport.WaitUntilAsync(() => repository.MergeToolReads == 1, "the start's read of merge.tool");
        Assert.Equal("fake", tab.MergeTool);
        var control = await ControlAsync(provider, services);

        services.RaiseGitSettingsChanged();

        await DashboardTestSupport.WaitUntilAsync(() => repository.MergeToolReads == 2, "the read after the save");
        await control.ToldAsync();
        Assert.Equal(2, repository.MergeToolReads);
    }

    [AvaloniaFact]
    public async Task Without_conflicts_nothing_is_read()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("clean");
        var services = DashboardTestSupport.Services(provider);
        using var tab = Full(services, repository);
        await tab.StartAsync();
        var control = await ControlAsync(provider, services);

        services.RaiseGitSettingsChanged();
        await control.ToldAsync();

        Assert.Equal(0, repository.MergeToolReads);
    }

    [AvaloniaFact]
    public async Task A_summary_with_conflicts_reads_nothing()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("billing", status: Conflicted);
        var services = DashboardTestSupport.Services(provider);
        using var summary = RepositoryViewModel.ForSummary(repository, services);
        await summary.StartAsync();
        Assert.Single(summary.Status.Conflicts);
        var control = await ControlAsync(provider, services);

        services.RaiseGitSettingsChanged();
        await control.ToldAsync();

        Assert.Equal(0, repository.MergeToolReads);
    }

    [AvaloniaFact]
    public async Task A_closed_view_model_reads_nothing_and_no_longer_listens()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("conflicts", status: Conflicted);
        var services = DashboardTestSupport.Services(provider);
        var tab = Full(services, repository);
        await tab.StartAsync();
        await DashboardTestSupport.WaitUntilAsync(() => repository.MergeToolReads == 1, "the start's read of merge.tool");
        var control = await ControlAsync(provider, services);

        tab.Dispose();
        services.RaiseGitSettingsChanged();
        await control.ToldAsync();

        Assert.Equal(1, repository.MergeToolReads);
    }

    private static RepositoryViewModel Full(TabServices services, IGitRepository repository) =>
        new(repository, services.Settings, services.Dates, services.Log, dialogs: services.Dialogs, services: services);

    /// <summary>The positive control: a started tab with conflicts on the same services, its start's read of <c>merge.tool</c> done.</summary>
    internal static async Task<ToldControl> ControlAsync(FakeProvider provider, TabServices services)
    {
        var repository = provider.Add("control", status: Conflicted);
        var tab = Full(services, repository);
        await tab.StartAsync();
        await DashboardTestSupport.WaitUntilAsync(() => repository.MergeToolReads == 1, "the control's start's read of merge.tool");
        return new ToldControl(repository, tab);
    }

    /// <summary>A control tab and its repository: <see cref="ToldAsync"/> completes once a save's read of <c>merge.tool</c> has happened in it, and closes it.</summary>
    internal sealed record ToldControl(CountingRepository Repository, RepositoryViewModel Tab)
    {
        public async Task ToldAsync()
        {
            await DashboardTestSupport.WaitUntilAsync(() => Repository.MergeToolReads == 2 && Tab.MergeTool == "fake", "the control's read after the save");
            Tab.Dispose();
        }
    }
}
