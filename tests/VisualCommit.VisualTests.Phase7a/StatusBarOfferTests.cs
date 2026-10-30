using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The status bar's offer (D317, docs/phase-7a-design.md 12.2, 12.7): <c>OperationOfferButton</c>
/// after a success whose report carries an offer, 6 right of the text; gone when the user's next
/// operation starts and after "Auto-fetch failed"; kept through a background success; hidden under
/// ssh's notice; collapsed before any offer, so the bar's other parts are where they were; never on
/// a dashboard.
/// </summary>
public class StatusBarOfferTests
{
    private const string Created = "Created pull request #9";

    private const string OpenInBrowser = "Open in browser";

    [AvaloniaFact]
    public async Task A_success_with_an_offer_shows_its_button_after_the_text_and_a_click_runs_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var (status, offer, git) = Parts(app);

        // Before any offer: collapsed, taking no room.
        await repository.Queue.RunAsync(Operation("Push main", OperationReport.Done("Pushed main to origin/main")));
        await app.WaitForIdleAsync(repository);
        Assert.False(offer.IsVisible);
        Assert.False(repository.OperationStatus.HasOffer);
        Assert.Null(repository.OperationStatus.OfferText);
        Assert.False(repository.OperationStatus.OfferCommand.CanExecute(null));
        var statusBefore = app.BoundsOf(status);
        var gitBefore = app.BoundsOf(git);

        var runs = 0;
        await repository.Queue.RunAsync(Operation("Create pull request for topic", OperationReport.Done(Created) with
        {
            Offer = new OperationOffer(OpenInBrowser, () =>
            {
                runs++;
                return Task.CompletedTask;
            }),
        }));
        await app.WaitForIdleAsync(repository);

        Assert.Equal(Created, status.Text);
        Assert.True(offer.IsEffectivelyVisible);
        Assert.Equal(OpenInBrowser, Assert.IsType<TextBlock>(offer.Content).Text);
        Assert.Equal(OpenInBrowser, Avalonia.Automation.AutomationProperties.GetName(offer));
        var statusBounds = app.BoundsOf(status);
        Assert.Equal(statusBefore.X, statusBounds.X, 0.01);
        Assert.Equal(statusBounds.Right + 6, app.BoundsOf(offer).X, 0.5);
        Assert.Equal(gitBefore, app.BoundsOf(git));
        Assert.True(app.BoundsOf(offer).Bottom <= app.BoundsOf(app.FindByAutomationId("StatusBar")).Bottom);

        app.Click(offer);
        await app.WaitForAsync(() => runs == 1, "the offer's run");
        Assert.Equal(Created, status.Text);
        Assert.True(offer.IsVisible, "A click keeps the offer: it lasts as the text does.");

        // The user's next operation takes it away when it starts, and its own result has none.
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fetching = repository.Queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            await held.Task;
            return OperationReport.Done("Fetched from all remotes");
        }));
        await app.WaitForAsync(() => status.Text == "Fetching from all remotes…", "the running text");
        Assert.False(offer.IsVisible, "An operation of the user started.");
        held.SetResult();
        await fetching;
        await app.WaitForIdleAsync(repository);
        Assert.Equal("Fetched from all remotes", status.Text);
        Assert.False(offer.IsVisible);
        Assert.Equal(statusBefore.X, app.BoundsOf(status).X, 0.01);
        Assert.Equal(gitBefore, app.BoundsOf(git));
        Assert.Equal(1, runs);
    }

    [AvaloniaFact]
    public async Task A_background_success_keeps_the_offer_and_a_failed_auto_fetch_takes_it_away()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var (status, offer, _) = Parts(app);

        await repository.Queue.RunAsync(Operation("Create pull request for topic", WithOffer()));
        await app.WaitForIdleAsync(repository);
        Assert.True(offer.IsVisible);

        Assert.True(repository.Queue.TryStartInBackground(AutoFetch(_ => Task.FromResult(OperationReport.Done("Fetched from all remotes")))));
        await app.WaitForIdleAsync(repository);
        Assert.Equal(Created, status.Text);
        Assert.True(offer.IsVisible, "A background success changes nothing.");

        Assert.True(repository.Queue.TryStartInBackground(AutoFetch(_ =>
            throw new GitException("git fetch --progress --no-auto-maintenance origin", 128, "fatal: unable to access 'http://127.0.0.1:1/x.git/': Failed to connect"))));
        await app.WaitForAsync(() => status.Text == OperationStatusViewModel.AutoFetchFailedText, "the auto-fetch's failure");
        Assert.False(offer.IsVisible);
        Assert.False(repository.OperationStatus.HasOffer);
        Assert.Null(repository.OperationStatus.OfferText);
    }

    [AvaloniaFact]
    public async Task Under_ssh_s_notice_the_offer_is_hidden_and_comes_back_with_the_text()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var (status, offer, _) = Parts(app);
        const string Presence = "Confirm user presence for key ED25519-SK SHA256:skokdAxs76eWz/jm50ga0Qz+ewMVrjE4KE91GAFIgTE";

        await repository.Queue.RunAsync(Operation("Create pull request for topic", WithOffer()));
        await app.WaitForIdleAsync(repository);
        Assert.True(offer.IsVisible);

        var shown = app.Session.Services.DialogHost.Notices.Show(Presence);
        app.Settle();
        Assert.Equal(Presence, status.Text);
        Assert.False(offer.IsVisible);
        Assert.False(repository.OperationStatus.OfferCommand.CanExecute(null));

        shown.Dispose();
        app.Settle();
        Assert.Equal(Created, status.Text);
        Assert.True(offer.IsVisible);
    }

    [AvaloniaFact]
    public async Task A_failed_or_a_stage_panel_result_never_offers_and_an_idle_bar_has_no_button()
    {
        using var data = new TempDirectory("data");
        using (var empty = ShellDriver.Start(data.Path))
        {
            await empty.WaitUntilReadyAsync();
            Assert.False(Parts(empty).Offer.IsVisible);
            Assert.Equal(
                ["No repository", "Ready", empty.Session.Shell.GitStatusText, "Activity log"],
                empty.FindByAutomationId("StatusBar").GetVisualDescendants().OfType<TextBlock>()
                    .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
                    .OrderBy(text => empty.BoundsOf(text).X)
                    .Select(text => text.Text!));
        }

        using var repo = await Scenarios.OperationsAsync();
        using var data2 = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data2.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var (status, offer, _) = Parts(app);
        var failure = new GitFailure(GitFailureKind.Other, "No.", string.Empty, string.Empty, null);

        await repository.Queue.RunAsync(Operation("Create pull request for topic", OperationReport.Failed("Create pull request for topic failed", failure) with { Offer = Offer() }));
        await app.WaitForIdleAsync(repository);
        Assert.Equal("Create pull request for topic failed", status.Text);
        Assert.False(offer.IsVisible);

        await repository.Queue.RunAsync(new GitOperation("Stage a.txt", "Staging a.txt…", OperationKind.Local, RefreshScope.None, _ => Task.FromResult(WithOffer()))
        {
            Errors = ErrorDisplay.StagePanel,
        });
        await app.WaitForIdleAsync(repository);
        Assert.Equal(OperationStatusViewModel.ReadyText, status.Text);
        Assert.False(offer.IsVisible);
    }

    [Fact]
    public void A_dashboard_never_offers()
    {
        // DashboardBulkViewModel's three members are constants of the type (12.2), whatever its
        // dashboard holds, so the object is made without one.
        IStatusBarOperation bulk = (DashboardBulkViewModel)RuntimeHelpers.GetUninitializedObject(typeof(DashboardBulkViewModel));

        Assert.Null(bulk.OfferText);
        Assert.False(bulk.HasOffer);
        Assert.False(bulk.OfferCommand.CanExecute(null));
    }

    private static OperationOffer Offer() => new(OpenInBrowser, () => Task.CompletedTask);

    private static OperationReport WithOffer() => OperationReport.Done(Created) with { Offer = Offer() };

    private static GitOperation Operation(string title, OperationReport report) =>
        new(title, title + "…", OperationKind.Network, RefreshScope.None, _ => Task.FromResult(report));

    private static GitOperation AutoFetch(Func<OperationContext, Task<OperationReport>> run) =>
        new(AutoFetchScheduler.Title, string.Empty, OperationKind.Network, RefreshScope.None, run)
        {
            IsBackground = true,
            Prompts = PromptMode.Deny,
        };

    private static async Task<RepositoryViewModel> GraphShownAsync(ShellDriver app)
    {
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync(repository);
        return repository;
    }

    private static (TextBlock Status, Button Offer, Control Git) Parts(ShellDriver app) => (
        (TextBlock)app.FindByAutomationId("OperationStatus"),
        (Button)app.FindByAutomationId("OperationOfferButton"),
        app.FindByAutomationId("GitStatus"));
}
