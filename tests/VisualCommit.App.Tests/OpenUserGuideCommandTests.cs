using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.ViewModels.Commands.ShortcutKey;

namespace VisualCommit.App.Tests;

/// <summary>
/// "Open the user guide" where no check of the walk-through shows it (D417; ruling 82): without a
/// launcher it says so in the log at Info and opens nothing; a launcher that throws is the command's
/// Error line; F1's route, the key read as the router reads it and run by the window's commands, opens
/// the guide's start page on a dashboard tab and on a welcome tab. The window is a view model's,
/// with no app started: what F1 does with a dialog or a menu open or during a drag is the router's
/// rule for every key, which <see cref="KeyRouterTests"/>' decline facts hold by pressing every key
/// of the tunnelling pass, F1 among them.
/// </summary>
public class OpenUserGuideCommandTests
{
    private const string NoLauncherLine = "Open the user guide: there is no launcher.";

    private const string FailedLine = "Open the user guide failed.";

    private const string StartPage = "https://rahimpasha.github.io/VisualCommit/";

    [AvaloniaFact]
    public async Task Without_a_launcher_the_command_says_so_at_Info_and_opens_nothing()
    {
        using var provider = new FakeProvider();
        var log = new RecordingLog();
        var window = DashboardTestSupport.Window(Services(provider, log, launcher: null));
        Assert.True(window.ActiveTab.ShowsWelcome, "The window does not start on a welcome tab.");

        var state = window.Commands.Describe(AppCommand.OpenUserGuide);
        Assert.Equal((true, true, "Open the user guide", null, null), (state.IsListed, state.IsEnabled, state.Label, state.Detail, state.DisabledReason));
        Assert.True(window.Commands.Run(AppCommand.OpenUserGuide), "The command did not run.");
        await DashboardTestSupport.WaitUntilAsync(() => GuideLines(log).Count > 0, "the command's log line");
        await DashboardTestSupport.SettleAsync();

        Assert.Equal([(LogLevel.Info, NoLauncherLine, (Exception?)null)], GuideLines(log));
        Assert.DoesNotContain(log.Entries, entry => entry.Level == LogLevel.Error);
    }

    [AvaloniaFact]
    public async Task A_launcher_that_throws_is_the_commands_Error_line()
    {
        using var provider = new FakeProvider();
        var log = new RecordingLog();
        var launcher = new ThrowingLauncher();
        var window = DashboardTestSupport.Window(Services(provider, log, launcher));

        Assert.True(window.Commands.Run(AppCommand.OpenUserGuide), "The command did not run.");
        await DashboardTestSupport.WaitUntilAsync(() => GuideLines(log).Count > 0, "the command's log line");
        await DashboardTestSupport.SettleAsync();

        Assert.Equal([new Uri(StartPage)], launcher.Asked);
        var (level, message, exception) = Assert.Single(GuideLines(log));
        Assert.Equal((LogLevel.Error, FailedLine), (level, message));
        Assert.IsType<InvalidOperationException>(Assert.Single(Assert.IsType<AggregateException>(exception).InnerExceptions));
        Assert.DoesNotContain(log.Entries, entry => entry.Message == NoLauncherLine);
    }

    [AvaloniaFact]
    public async Task F1s_route_on_a_welcome_tab_opens_the_guides_start_page()
    {
        using var provider = new FakeProvider();
        var launcher = new FakeFileLauncher();
        var log = new RecordingLog();
        var window = DashboardTestSupport.Window(Services(provider, log, launcher));
        Assert.True(window.ActiveTab.ShowsWelcome, "The window does not start on a welcome tab.");

        await PressF1Async(window, launcher, 1);

        Assert.Equal([new FileLaunch(FileLaunchKind.OpenUrl, StartPage)], launcher.Requests);
        Assert.Empty(GuideLines(log));
    }

    [AvaloniaFact]
    public async Task F1s_route_on_a_dashboard_tab_opens_the_guides_start_page()
    {
        using var provider = new FakeProvider();
        var launcher = new FakeFileLauncher();
        var log = new RecordingLog();
        var services = Services(provider, log, launcher, DashboardTestSupport.Store());
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        Assert.True(window.ActiveTab.IsDashboard, "The active tab is no dashboard.");

        await PressF1Async(window, launcher, 1);

        Assert.Equal([new FileLaunch(FileLaunchKind.OpenUrl, StartPage)], launcher.Requests);
        Assert.Empty(GuideLines(log));
    }

    /// <summary>F1 as the router takes it on the platform's tunnelling pass: routed by the key table, then run by the window's commands; waits for the launcher's <paramref name="requests"/>th request.</summary>
    private static async Task PressF1Async(MainWindowViewModel window, FakeFileLauncher launcher, int requests)
    {
        var routed = AppShortcuts.Route(new Shortcut(F1), CommandKey.Ctrl);
        Assert.Equal(new RoutedCommand(AppCommand.OpenUserGuide), routed);
        Assert.True(window.Commands.Run(routed!.Value.Command, routed.Value.Argument), "F1's command did not run.");
        await DashboardTestSupport.WaitUntilAsync(() => launcher.Requests.Count >= requests, "the launcher's request");
        await DashboardTestSupport.SettleAsync();
    }

    /// <summary>The log's lines of the command, in order.</summary>
    private static List<(LogLevel Level, string Message, Exception? Exception)> GuideLines(RecordingLog log) =>
        [.. log.Entries.Where(entry => entry.Message.StartsWith("Open the user guide", StringComparison.Ordinal))];

    /// <summary>The session's services around <paramref name="provider"/> with <paramref name="launcher"/> (none when null) and the workspaces <paramref name="store"/>.</summary>
    private static TabServices Services(FakeProvider provider, RecordingLog log, IFileLauncher? launcher, IWorkspaceStore? store = null) =>
        new(
            provider,
            new NoFolders(),
            new MainWindowViewModelTests.FakeSettings(),
            new DashboardTestSupport.MemorySession(),
            new DateDisplay(TimeZoneInfo.Utc),
            log,
            launcher: launcher,
            workspaces: store ?? new InMemoryWorkspaceStore());

    /// <summary>A launcher whose every address throws before it opens anything, as one with no browser to start might.</summary>
    private sealed class ThrowingLauncher : IFileLauncher
    {
        private readonly List<Uri> _asked = [];

        /// <summary>The addresses it was asked to open, in order.</summary>
        public IReadOnlyList<Uri> Asked => [.. _asked];

        public Task OpenAsync(string fullPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RevealAsync(string fullPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenInEditorAsync(LaunchCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenUrlAsync(Uri url, CancellationToken cancellationToken = default)
        {
            _asked.Add(url);
            throw new InvalidOperationException("No browser.");
        }
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
