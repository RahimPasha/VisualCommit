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

        // Phase 0's toggle on a tab without a repository, which phase 5 keeps (docs/phase-5-design.md 1.2),
        // with its key since phase 6 (docs/phase-6-design.md 6.8).
        var toggle = app.FindByAutomationId("ActivityLogToggle");
        Assert.False(toggle.IsEffectivelyEnabled);
        Assert.Equal("Show every git command the app has run (Ctrl+J)", ToolTip.GetTip(toggle));
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

        // Phase 5 (docs/phase-5-design.md 1.2, 14.1): with a repository the activity log's toggle is enabled.
        Assert.True(app.FindByAutomationId("ActivityLogToggle").IsEffectivelyEnabled);

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

        // Within a bound: a click that missed Cancel fails here with a message instead of hanging.
        app.Click(parts.Cancel);
        await app.WaitForAsync(() => fetching.IsCompleted, "the fetch to end after Cancel");
        Assert.Equal(OperationOutcome.Cancelled, (await fetching).Outcome);
        await app.WaitForAsync(() => waiting.IsCompleted, "the waiting operation to run after the fetch");
        await waiting;
        await app.WaitForIdleAsync(repository);
        Assert.Equal("Created hotfix and checked it out", parts.Status.Text);
        Assert.False(parts.Progress.IsVisible);
        Assert.False(parts.Cancel.IsVisible);
        Assert.False(parts.Waiting.IsVisible);
    }

    [AvaloniaFact]
    public async Task While_the_merge_tool_runs_Cancel_says_it_stops_git_and_the_merge_tool()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var parts = Parts(app);

        var tool = RunMergeTool(repository);
        await app.WaitForAsync(() => parts.Cancel.IsVisible, "Cancel");

        Assert.Equal("Stop git and the merge tool", ToolTip.GetTip(parts.Cancel));

        // The operation runs until it is cancelled, so the wait is bounded: a click that missed
        // fails here with a message instead of hanging.
        app.Click(parts.Cancel);
        await app.WaitForAsync(() => tool.IsCompleted, "the merge tool's operation to end after Cancel");
        Assert.Equal(OperationOutcome.Cancelled, (await tool).Outcome);
        await app.WaitForIdleAsync(repository);
        Assert.False(parts.Cancel.IsVisible);
    }

    [AvaloniaFact]
    public async Task Resting_on_Cancel_until_its_tooltip_shows_and_then_clicking_cancels()
    {
        // Cancel sits on the window's bottom edge, where its tooltip slides up over the pointer
        // (D105). A tooltip takes no input (Theme/Controls.axaml): the press reaches Cancel, which
        // keeps its hover look while its tooltip is open.
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var parts = Parts(app);

        var tool = RunMergeTool(repository);
        await app.WaitForAsync(() => parts.Cancel.IsVisible, "Cancel");
        app.MoveMouse(parts.Cancel);
        Assert.Equal("Stop git and the merge tool", await app.WaitForToolTipAsync(parts.Cancel));
        Assert.True(parts.Cancel.IsPointerOver, "Cancel lost its hover look when its tooltip opened.");

        // The click's move goes to where the pointer already is.
        app.Click(parts.Cancel);
        await app.WaitForAsync(() => tool.IsCompleted, "the merge tool's operation to end after a click under Cancel's open tooltip");
        Assert.Equal(OperationOutcome.Cancelled, (await tool).Outcome);
        Assert.False(ToolTip.GetIsOpen(parts.Cancel));
        await app.WaitForIdleAsync(repository);
        Assert.False(parts.Cancel.IsVisible);
    }

    [AvaloniaFact]
    public async Task A_click_during_which_Cancel_s_tooltip_opens_still_cancels()
    {
        // A slow click: under load the headless window rendered for longer than the show delay
        // between the click's move and its press, and Cancel's tooltip opened over the pointer in
        // between. A show delay of 0 opens it during the move every time.
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var parts = Parts(app);
        var opened = false;
        parts.Cancel.PropertyChanged += (_, e) => opened |= e.Property == ToolTip.IsOpenProperty && e.NewValue is true;

        var tool = RunMergeTool(repository);
        await app.WaitForAsync(() => parts.Cancel.IsVisible, "Cancel");
        app.MoveToNeutral();
        ToolTip.SetShowDelay(parts.Cancel, 0);
        app.Click(parts.Cancel);
        parts.Cancel.ClearValue(ToolTip.ShowDelayProperty);

        Assert.True(opened, "Cancel's tooltip did not open during the click.");
        await app.WaitForAsync(() => tool.IsCompleted, "the merge tool's operation to end after a click during which Cancel's tooltip opened");
        Assert.Equal(OperationOutcome.Cancelled, (await tool).Outcome);
        await app.WaitForIdleAsync(repository);
        Assert.False(parts.Cancel.IsVisible);
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

    [AvaloniaFact]
    public async Task An_ssh_notice_shows_in_the_status_bar_until_its_scope_ends()
    {
        // Design 3.2: a "none" prompt of ssh gets no dialog; the status bar shows its text, in the
        // secondary colour, until the scope ends; on the welcome page (a clone) as in a repository.
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var parts = Parts(app);
        var notices = app.Session.Services.DialogHost.Notices;
        const string Presence = "Confirm user presence for key ED25519-SK SHA256:skokdAxs76eWz/jm50ga0Qz+ewMVrjE4KE91GAFIgTE";

        await repository.Queue.RunAsync(new GitOperation("Push main", "Pushing main to origin/main…", OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git push --progress origin refs/heads/main:refs/heads/main", 1, " ! [rejected]        main -> main (fetch first)")));
        app.Settle();
        Assert.Equal("Push main failed", parts.Status.Text);

        var shown = notices.Show(Presence);
        app.Settle();
        Assert.Equal(Presence, parts.Status.Text);
        Assert.Equal(Presence, ToolTip.GetTip(parts.Status));
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), Color(parts.Status));
        Assert.False(app.Session.Services.DialogHost.IsOpen, "A notice opens no dialog.");
        Assert.DoesNotContain(
            LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true),
            problem => problem.Contains("Confirm user presence", StringComparison.Ordinal));
        Pictures(app, "statusbar-ssh-notice");

        shown.Dispose();
        app.Settle();
        Assert.Equal("Push main failed", parts.Status.Text);
        Assert.Equal(Brush(app, "VcDangerBrush"), Color(parts.Status));

        // A new tab: the welcome page's "Ready" makes way for a notice as well.
        app.Session.Shell.NewTabCommand.Execute(null);
        app.Settle();
        Assert.Null(app.Session.Shell.ActiveTab.Repository);
        Assert.Equal("Ready", parts.Status.Text);
        using (notices.Show(Presence))
        {
            app.Settle();
            Assert.Equal(Presence, parts.Status.Text);
        }

        app.Settle();
        Assert.Equal("Ready", parts.Status.Text);
    }

    /// <summary>The merge tool's operation (docs/phase-4-design.md 11.9): local, it can be cancelled (D149), and it runs until it is.</summary>
    private static Task<OperationResult> RunMergeTool(RepositoryViewModel repository) =>
        repository.Queue.RunAsync(new GitOperation("Merge tool for notes.txt", "Waiting for fake to close…", OperationKind.Local, RefreshScope.None, async context =>
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Resolved notes.txt");
        })
        {
            CanBeCancelled = true,
        });

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
            TestPictures.Save(app, $"{name}-{(theme == ThemeVariant.Dark ? "dark" : "light")}");
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
