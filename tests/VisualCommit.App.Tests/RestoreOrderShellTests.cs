using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The restore order in the whole app with real git (D457 (5), ruling 76; docs/phase-8a-design.md
/// 14.4): three restored repositories, the middle one active, open in the order's way and every
/// history loads before the start ends; a waiting tab the user shows opens at once while the
/// active tab's first git call is held. The restore clock is a <see cref="ManualTimeProvider"/>
/// that never moves (ruling 87), so no release can come from the 5 s between the steps, while the
/// session's own clock runs on.
/// </summary>
public class RestoreOrderShellTests
{
    [AvaloniaFact]
    public async Task Behind_an_active_repository_tab_the_others_open_after_the_release_and_every_history_loads()
    {
        using var first = await RepositoryAsync("first");
        using var second = await RepositoryAsync("second");
        using var third = await RepositoryAsync("third");
        using var data = new TempDirectory("data");
        var clock = new ManualTimeProvider();

        using var app = ShellDriver.StartWithTabs(data.Path, [first.Path, second.Path, third.Path], activeTab: 1, restoreClock: clock);
        await app.WaitUntilReadyAsync();

        var shell = app.Session.Shell;
        Assert.All(shell.Tabs, tab => Assert.True(tab.Repository is { Graph: { Count: 1, IsComplete: true } }, $"{tab.Title} shows its whole history."));
        Assert.Equal("second", shell.ActiveTab.Title);
        Assert.Same(clock, app.Session.Services.RestoreClock);

        var lines = app.LogLines();
        Assert.Single(lines, line => line.EndsWith("] " + Started("second"), StringComparison.Ordinal));
        var released = SingleIndex(lines, line => line.EndsWith("] " + Released("second", 2), StringComparison.Ordinal), "the release line");
        Assert.True(SingleIndex(lines, OpenedAfter("first").IsMatch, "first's opened line") > released, "first opens after the release.");
        Assert.True(SingleIndex(lines, OpenedAfter("third").IsMatch, "third's opened line") > released, "third opens after the release.");
    }

    [AvaloniaFact]
    public async Task A_restored_tab_shown_while_it_waits_opens_at_once()
    {
        using var first = await RepositoryAsync("first");
        using var second = await RepositoryAsync("second");
        using var third = await RepositoryAsync("third");
        using var data = new TempDirectory("data");
        var clock = new ManualTimeProvider();

        // The session's first rev-parse is held: Start pumps the UI thread before it returns, so a
        // hold set after it would miss the active tab's opening.
        var held = new HeldGitRunner();
        held.HoldNext("rev-parse");
        using var app = ShellDriver.StartWithTabs(data.Path, [first.Path, second.Path, third.Path], activeTab: 1, gitProbe: held.Probe, restoreClock: clock);
        var call = await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        Assert.Equal(WorktreeKey.Of(second.Path), WorktreeKey.Of(call.WorkingDirectory!), WorktreeKey.Comparer);

        // The waiting third tab, shown: it opens at once, while second can neither show a page nor
        // end its opening and the clock stands still, so nothing else can release it.
        var shell = app.Session.Shell;
        shell.ActivateTabCommand.Execute(shell.Tabs[2]);
        Assert.Contains(app.LogLines(), line => line.EndsWith("] " + Shown("third"), StringComparison.Ordinal));
        await app.WaitForLogLineAsync(OpenedAfter("third"), timeout: ShellDriver.DefaultWaitTimeout);
        var lines = app.LogLines();
        var shown = SingleIndex(lines, line => line.EndsWith("] " + Shown("third"), StringComparison.Ordinal), "the shown line");
        Assert.True(SingleIndex(lines, OpenedAfter("third").IsMatch, "third's opened line") > shown, "third opens after the shown line.");
        Assert.DoesNotContain(lines, line => line.Contains("] Restore order: second: ", StringComparison.Ordinal));

        held.Release();
        await app.WaitUntilReadyAsync();

        Assert.All(shell.Tabs, tab => Assert.True(tab.Repository is { Graph: { Count: 1, IsComplete: true } }, $"{tab.Title} shows its whole history."));
        lines = app.LogLines();
        var released = SingleIndex(lines, line => line.EndsWith("] " + Released("second", 1), StringComparison.Ordinal), "the release line");
        Assert.True(SingleIndex(lines, OpenedAfter("first").IsMatch, "first's opened line") > released, "first opens after the release.");
    }

    private static async Task<TempRepo> RepositoryAsync(string name)
    {
        var repo = await TempRepo.CreateAsync(name);
        try
        {
            await repo.CommitFileAsync("README.md", $"# {name}\n", "Initial commit");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static string Started(string first) =>
        string.Format(CultureInfo.InvariantCulture, RestoreOrder.StartedFormat, first, 2, RestoreOrder.OpensAtOnce);

    /// <summary>The release at the first tab's first page, with the restore clock still at 0 ms and <paramref name="waiting"/> tabs still waiting.</summary>
    private static string Released(string first, int waiting) =>
        string.Format(CultureInfo.InvariantCulture, RestoreOrder.ReleasedFormat, first, RestoreOrder.FirstPageReason, 0, waiting, RestoreOrder.OpensAtOnce);

    private static string Shown(string tab) => string.Format(CultureInfo.InvariantCulture, RestoreOrder.ShownFormat, tab);

    /// <summary>The Debug line each repository writes when its full start begins: <c>&lt;name&gt;: opened after &lt;N&gt; ms</c>.</summary>
    private static Regex OpenedAfter(string name) =>
        new(string.Create(CultureInfo.InvariantCulture, $@"\] {Regex.Escape(name)}: opened after \d+ ms$"), RegexOptions.CultureInvariant);

    private static int SingleIndex(IReadOnlyList<string> lines, Func<string, bool> match, string what)
    {
        var found = lines.Select((line, index) => (line, index)).Where(entry => match(entry.line)).Select(entry => entry.index).ToList();
        Assert.True(found.Count == 1, $"The log holds {found.Count} of {what}, not one:\n{string.Join('\n', lines)}");
        return found[0];
    }
}
