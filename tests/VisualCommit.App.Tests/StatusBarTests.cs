using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The status bar in the window (docs/phase-3-design.md 1.9, 4e, D95): the operation's text and
/// colour, the progress bar, Cancel, the waiting count, and what it shows of a background
/// operation, as the active tab's queue runs. What the text says in each state is
/// <c>OperationStatusViewModelTests</c>'; this is what the view draws of it.
/// </summary>
public class StatusBarTests
{
    [AvaloniaFact]
    public async Task An_idle_bar_shows_Ready_and_hides_the_progress_Cancel_and_the_waiting_count()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        var parts = Parts(app);
        Assert.Equal("Ready", parts.Status.Text);
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), Color(parts.Status));
        Assert.False(parts.Progress.IsVisible);
        Assert.False(parts.Cancel.IsVisible);
        Assert.False(parts.Waiting.IsVisible);
        Assert.Equal(
            ["No repository", "Ready", app.Session.Shell.GitStatusText, "Activity log"],
            app.FindByAutomationId("StatusBar").GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
                .OrderBy(text => app.BoundsOf(text).X)
                .Select(text => text.Text!));
    }

    [AvaloniaFact]
    public async Task A_running_operation_shows_its_progress_Cancel_and_what_waits_and_Cancel_stops_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var parts = Parts(app);

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fetching = repository.Queue.RunAsync(new GitOperation("Fetch origin", "Fetching from origin…", OperationKind.Network, RefreshScope.None, async context =>
        {
            context.Progress.Report(new OperationProgress("Counting objects", 60, "remote: Counting objects:  60% (3/5)"));
            await held.Task.WaitAsync(context.CancellationToken);
            return OperationReport.Done("Fetched from origin");
        }));
        await app.WaitForAsync(() => parts.Status.Text == "Fetching from origin… Counting objects 60%", "the progress");
        var waiting = repository.Queue.RunAsync(new GitOperation("Create branch hotfix", "Creating hotfix…", OperationKind.Local, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Done("Created hotfix and checked it out"))));
        app.Settle();

        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), Color(parts.Status));
        Assert.True(parts.Progress.IsVisible);
        Assert.False(parts.Progress.IsIndeterminate);
        Assert.Equal(60, parts.Progress.Value);
        Assert.Equal(new Size(80, 4), parts.Progress.Bounds.Size);
        Assert.True(parts.Cancel.IsVisible);
        Assert.Equal("Stop git", ToolTip.GetTip(parts.Cancel));
        Assert.True(parts.Waiting.IsVisible);
        Assert.Equal("1 waiting", parts.Waiting.Text);
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), Color(parts.Waiting));
        var order = new Control[] { parts.Status, parts.Progress, parts.Cancel, parts.Waiting }.Select(control => app.BoundsOf(control).X).ToList();
        Assert.Equal(order.Order(), order);
        Pictures(app, "statusbar-fetch-progress");

        app.Click(parts.Cancel);
        Assert.Equal(OperationOutcome.Cancelled, (await fetching).Outcome);
        await waiting;
        await app.WaitForIdleAsync(repository);
        Assert.Equal("Created hotfix and checked it out", parts.Status.Text);
        Assert.False(parts.Progress.IsVisible);
        Assert.False(parts.Cancel.IsVisible);
        Assert.False(parts.Waiting.IsVisible);
    }

    [AvaloniaFact]
    public async Task Results_stay_in_their_colours_and_a_long_one_is_cut_with_its_whole_text_in_the_tooltip()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var parts = Parts(app);

        await repository.Queue.RunAsync(new GitOperation("Fetch origin", "Fetching from origin…", OperationKind.Network, RefreshScope.None, _ =>
            Task.FromCanceled<OperationReport>(new CancellationToken(canceled: true))));
        app.Settle();
        Assert.Equal("Fetch origin cancelled", parts.Status.Text);
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), Color(parts.Status));

        await repository.Queue.RunAsync(new GitOperation("Merge feature/conflict", "Merging feature/conflict into main…", OperationKind.Local, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Conflicts("Merge stopped: 1 file has conflicts"))));
        app.Settle();
        Assert.Equal("Merge stopped: 1 file has conflicts", parts.Status.Text);
        Assert.Equal(Brush(app, "VcWarningBrush"), Color(parts.Status));
        Pictures(app, "statusbar-conflicts");

        await repository.Queue.RunAsync(new GitOperation("Push main", "Pushing main to origin/main…", OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git push --progress origin refs/heads/main:refs/heads/main", 1, " ! [rejected]        main -> main (fetch first)")));
        app.Settle();
        Assert.Equal("Push main failed", parts.Status.Text);
        Assert.Equal(Brush(app, "VcDangerBrush"), Color(parts.Status));
        Assert.Equal("Push main failed", ToolTip.GetTip(parts.Status));
        Pictures(app, "statusbar-failed");

        var picked = "Cherry-picked 1 of 3 commits. dff8fbf stopped with conflicts in 1 file; not picked: ce5ea6a \"Alpha two\", 078ef05 \"Conflicting change\", 88d6b91 \"Main work\".";
        await repository.Queue.RunAsync(new GitOperation("Cherry-pick 3 commits", "Cherry-picking 3 commits…", OperationKind.Local, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Conflicts(picked))));
        app.Settle();
        Assert.Equal(picked, parts.Status.Text);
        Assert.Equal(picked, ToolTip.GetTip(parts.Status));
        Assert.True(parts.Status.Bounds.Width <= 520);
        Assert.DoesNotContain(
            LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true),
            problem => problem.Contains("Cherry-picked", StringComparison.Ordinal));
        Pictures(app, "statusbar-long-message");
    }

    [AvaloniaFact]
    public async Task An_operation_without_a_percentage_has_an_indeterminate_bar_and_a_local_one_no_Cancel()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var parts = Parts(app);

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkingOut = repository.Queue.RunAsync(new GitOperation("Check out feature/alpha", "Checking out feature/alpha…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            await held.Task;
            return OperationReport.Done("Checked out feature/alpha");
        }));
        await app.WaitForAsync(() => parts.Status.Text == "Checking out feature/alpha…", "the running text");

        Assert.True(parts.Progress.IsVisible);
        Assert.True(parts.Progress.IsIndeterminate);
        Assert.False(parts.Cancel.IsVisible, "A local operation cannot be cancelled.");
        Pictures(app, "statusbar-indeterminate");

        held.SetResult();
        await checkingOut;
        await app.WaitForIdleAsync(repository);
        Assert.Equal("Checked out feature/alpha", parts.Status.Text);
    }

    [AvaloniaFact]
    public async Task Behind_an_auto_fetch_it_says_so_and_a_failed_auto_fetch_shows_git_s_first_line_as_its_tooltip()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var parts = Parts(app);

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(repository.Queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, async _ =>
        {
            await held.Task;
            throw new GitException("git fetch --progress --no-auto-maintenance origin", 128, "fatal: unable to access 'http://127.0.0.1:1/x.git/': Failed to connect\nsecond line");
        })
        {
            IsBackground = true,
            Prompts = PromptMode.Deny,
        }));
        app.Settle();
        Assert.Equal("Ready", parts.Status.Text);
        Assert.False(parts.Progress.IsVisible, "A background operation shows no progress.");
        Assert.False(parts.Cancel.IsVisible, "A background operation is never cancelled.");

        var pushing = repository.Queue.RunAsync(new GitOperation("Push main", "Pushing main to origin/main…", OperationKind.Network, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Done("Pushed main to origin/main"))));
        await app.WaitForAsync(() => parts.Status.Text == "Waiting for auto-fetch…", "the waiting text");
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), Color(parts.Status));
        Pictures(app, "statusbar-waiting-for-auto-fetch");

        held.SetResult();
        await pushing;
        await app.WaitForIdleAsync(repository);
        Assert.Equal("Pushed main to origin/main", parts.Status.Text);

        Assert.True(repository.Queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git fetch --progress --no-auto-maintenance origin", 128, "fatal: unable to access 'http://127.0.0.1:1/x.git/': Failed to connect\nsecond line"))
        {
            IsBackground = true,
            Prompts = PromptMode.Deny,
        }));
        await app.WaitForAsync(() => parts.Status.Text == "Auto-fetch failed", "the auto-fetch's failure");
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), Color(parts.Status));
        Assert.Equal("fatal: unable to access 'http://127.0.0.1:1/x.git/': Failed to connect", ToolTip.GetTip(parts.Status));
        Pictures(app, "statusbar-auto-fetch-failed");
    }

    private static async Task<RepositoryViewModel> GraphShownAsync(ShellDriver app)
    {
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync(repository);
        return repository;
    }

    private static void Pictures(ShellDriver app, string name)
    {
        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            app.Window.RequestedThemeVariant = theme;
            app.Settle();
            TestPictures.Save(app.Capture(), $"{name}-{(theme == ThemeVariant.Dark ? "dark" : "light")}");
        }

        app.Window.RequestedThemeVariant = ThemeVariant.Dark;
        app.Settle();
    }

    private static (TextBlock Status, ProgressBar Progress, Button Cancel, TextBlock Waiting) Parts(ShellDriver app) => (
        (TextBlock)app.FindByAutomationId("OperationStatus"),
        (ProgressBar)app.FindByAutomationId("OperationProgress"),
        (Button)app.FindByAutomationId("OperationCancelButton"),
        (TextBlock)app.FindByAutomationId("OperationWaiting"));

    private static Color Color(TextBlock text) => ((ISolidColorBrush)text.Foreground!).Color;

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");
}
