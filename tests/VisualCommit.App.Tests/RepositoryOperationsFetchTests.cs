using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP7-remotes: fetch in <see cref="RepositoryOperations"/> (docs/phase-3-design.md 2.16), with
/// <see cref="AuthRemoteServer"/> holding a fetch for its progress and Cancel and asking for a
/// sign-in and <see cref="FakeSsh"/> asking ssh's questions, also through the window (checks 17
/// and 21),
/// against the scenario repositories, through <see cref="OperationsHarness"/>: for each, its
/// confirmation, the backup it saves, what git holds afterwards, the status bar's text, its
/// failure dialog with git's own output and its extra button, its cancel, and that a second
/// click runs nothing more. Folder remotes serve most of them. WP7-remotes' tests are four
/// classes in one xunit collection, which runs beside those of the other sub-packages
/// (<see cref="RepositoryOperationsTestSupport"/> says why): <see cref="RepositoryOperationsFetchTests"/>, <see cref="RepositoryOperationsPullTests"/>,
/// <see cref="RepositoryOperationsPushTests"/> and <see cref="RepositoryOperationsRemotesTests"/>;
/// the helpers they share are in <see cref="RepositoryOperationsTestSupport"/>.
/// </summary>
[Collection(RepositoryOperationsTestSupport.RemotesCollection)]
public class RepositoryOperationsFetchTests
{
    // 2.16: fetch.

    [Fact]
    public async Task Fetch_of_one_remote_brings_its_branches_and_says_so()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.FetchAsync("origin");

        Assert.Equal(("Fetched from origin", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(remote, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(1, app.Operations.CurrentBranch?.Behind);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Contains(app.Log.Messages, message => message.Contains(": Fetch origin: Succeeded after ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fetch_of_all_remotes_goes_on_after_a_remote_fails_and_names_every_remote_that_failed()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.GitAsync("remote", "add", "broken", repo.SiblingPath("nowhere.git"));
        await repo.GitAsync("remote", "add", "gone", repo.SiblingPath("gone.git"));
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.FetchAllAsync();

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Fetch failed", "Git could not fetch from broken and gone."), (dialog.Title, dialog.Summary));
        Assert.Contains("error: Could not fetch broken", dialog.Output, StringComparison.Ordinal);
        Assert.Contains("error: Could not fetch gone", dialog.Output, StringComparison.Ordinal);
        Assert.Equal("git fetch --progress --no-auto-maintenance broken", dialog.Command);
        Assert.False(dialog.HasExtraAction);
        Assert.Equal(("Fetch failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));

        // origin, fetched between them, was fetched all the same.
        Assert.Equal(remote, await repo.RefTargetAsync("refs/remotes/origin/main"));
        app.AssertScriptFollowed();
    }

    // docs/phase-6-design.md 4.6 (D195): a bulk run's row of Fetch all.

    [Fact]
    public async Task A_bulk_runs_fetch_of_a_failing_remote_opens_no_dialog_and_keeps_the_identical_one()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.GitAsync("remote", "add", "broken", repo.SiblingPath("nowhere.git"));
        using var app = await OperationsHarness.OpenAsync(repo);
        var kept = new List<DeferredDialog>();
        using var group = new PromptGroup();

        var result = await app.Operations.FetchAllAsync(new BulkPart(CancellationToken.None, group, kept.Add));

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal((OperationOutcome.Failed, "Fetch failed"), (result?.Outcome, result?.Message));
        Assert.Equal(("Fetch failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        var dialog = Assert.Single(kept);
        Assert.Equal(("Fetch failed", "Git could not fetch from broken."), (dialog.Display?.Title, dialog.Display?.Summary));
        Assert.Equal("git fetch --progress --no-auto-maintenance broken", dialog.Failure?.CommandText);
        Assert.Empty(dialog.StaleLockFiles);

        // ShowDeferredAsync opens what the toolbar's Fetch opens for the same failure.
        app.Dialogs.CloseError().CloseError();
        await app.Operations.ShowDeferredAsync(dialog);
        await app.Operations.FetchAllAsync();
        var (deferred, toolbar) = (app.Dialogs.Errors[0], app.Dialogs.Errors[1]);
        Assert.Equal(
            (toolbar.Title, toolbar.Summary, toolbar.Output, toolbar.Command, toolbar.ExtraAction, toolbar.RepositoryName, toolbar.HasOutput),
            (deferred.Title, deferred.Summary, deferred.Output, deferred.Command, deferred.ExtraAction, deferred.RepositoryName, true));
        Assert.Contains("error: Could not fetch broken", deferred.Output, StringComparison.Ordinal);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_bulk_runs_part_cancelled_before_its_call_queues_nothing()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        using var group = new PromptGroup();
        var mark = app.Calls.Snapshot().Count;

        var result = await app.Operations.FetchAllAsync(new BulkPart(new CancellationToken(canceled: true), group, _ => Assert.Fail("Nothing to keep.")));

        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (result?.Outcome, result?.Message));
        Assert.Null(app.LastResult);
        Assert.Empty(app.Calls.Snapshot().Skip(mark));
        Assert.Contains(app.Log.Messages, message => message.EndsWith(": Fetch: not started, cancelled.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cancelled_bulk_fetchs_lock_files_are_kept_with_their_stamps_and_removed_later_only_when_unchanged()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var app = await OperationsHarness.OpenAsync(repo);
        var kept = new List<DeferredDialog>();
        using var group = new PromptGroup();
        using var cancel = new CancellationTokenSource();
        var lockFile = Path.Combine(repo.Path, ".git", "refs", "remotes", "origin", "main.lock");

        var held = server.HoldNextFetchPack();
        var fetch = app.Operations.FetchAllAsync(new BulkPart(cancel.Token, group, kept.Add));
        await held.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(lockFile, "left by the stopped fetch", TestContext.Current.CancellationToken);
        await cancel.CancelAsync();
        var result = await fetch.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        server.Release();

        Assert.Equal(OperationOutcome.Cancelled, result?.Outcome);
        Assert.Empty(app.Dialogs.Shown);
        var dialog = Assert.Single(kept);
        Assert.Equal([Path.GetFullPath(lockFile)], dialog.StaleLockFiles);
        Assert.Null(dialog.Failure);
        Assert.Equal(LockFiles.Stamp(lockFile), dialog.Listed![Path.GetFullPath(lockFile)]);

        // Changed since it was kept (a live git took the name again): "Remove lock files" leaves it.
        File.SetLastWriteTimeUtc(lockFile, DateTime.UtcNow.AddSeconds(5));
        app.Dialogs.CloseError(clickExtra: true);
        await app.Operations.ShowDeferredAsync(dialog);
        Assert.True(File.Exists(lockFile), "A lock file that changed since it was kept was removed.");

        // Still the same file: removed.
        File.Delete(lockFile);
        await File.WriteAllTextAsync(lockFile, "left by the stopped fetch", TestContext.Current.CancellationToken);
        var again = dialog with { Listed = new Dictionary<string, (DateTime Time, long Length)?> { [Path.GetFullPath(lockFile)] = LockFiles.Stamp(lockFile) } };
        app.Dialogs.CloseError(clickExtra: true);
        await app.Operations.ShowDeferredAsync(again);
        await WaitUntilAsync(() => !File.Exists(lockFile), "Remove lock files");

        var shown = app.Dialogs.Errors[0];
        Assert.Equal(("Fetch cancelled", OperationTexts.StaleLockFilesSummary, ".git/refs/remotes/origin/main.lock", OperationTexts.RemoveLockFilesButton), (shown.Title, shown.Summary, shown.Output, shown.ExtraAction));
        Assert.StartsWith("git fetch --progress", shown.Command, StringComparison.Ordinal);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_bulk_runs_fetch_merged_into_the_toolbars_waiting_one_keeps_the_dialog_the_toolbar_shows()
    {
        // 4.6, D123: one git call, one dialog for the toolbar's click, and the row keeps the same.
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.GitAsync("remote", "add", "broken", repo.SiblingPath("nowhere.git"));
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();
        var kept = new List<DeferredDialog>();
        using var group = new PromptGroup();

        var (hold, ahead) = HoldQueue(app);
        var toolbar = app.Operations.FetchAllAsync();
        var row = app.Operations.FetchAllAsync(new BulkPart(CancellationToken.None, group, kept.Add));
        Assert.Equal(1, app.Repository.Queue.Waiting);
        hold.SetResult();
        await Task.WhenAll(ahead, toolbar, row).WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        var error = Assert.Single(app.Dialogs.Errors);
        var dialog = Assert.Single(kept);
        Assert.Equal((error.Title, error.Summary, error.Command), (dialog.Display?.Title, dialog.Display?.Summary, dialog.Failure?.CommandText));
        Assert.Equal(1, Ran(app, "Fetch"));
        Assert.Equal(OperationOutcome.Failed, (await row)?.Outcome);
        app.AssertScriptFollowed();
    }

    [Fact]
    public void The_remotes_a_fetch_of_all_could_not_fetch_are_read_from_gits_output()
    {
        Assert.Equal(["origin", "my mirror"], RepositoryOperations.FailedRemotes("fatal: x\r\nerror: Could not fetch origin\r\nfatal: y\nerror: Could not fetch my mirror\nerror: Could not fetch origin\n"));
        Assert.Empty(RepositoryOperations.FailedRemotes("fatal: unable to access 'x'\n"));
    }

    [Fact]
    public async Task A_held_fetch_shows_its_progress_and_Cancel_stops_it_without_changing_a_ref()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var app = await OperationsHarness.OpenAsync(repo);
        var before = await repo.RefTargetAsync("refs/remotes/origin/main");

        var held = server.HoldNextFetchPack();
        var fetch = app.Operations.FetchAsync("origin");
        await held.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => app.StatusText.EndsWith('%'), "the fetch's percentage");

        Assert.Matches(@"^Fetching from origin… Counting objects \d{1,3}%$", app.StatusText);
        Assert.True(app.Repository.Queue.Current?.CanCancel);
        Assert.False(fetch.IsCompleted);

        app.Repository.Queue.CancelCurrent();
        await fetch.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        server.Release();

        Assert.Equal(("Fetch origin cancelled", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(OperationOutcome.Cancelled, app.LastResult?.Outcome);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(before, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(repo.Path, ".git"), "*.lock", SearchOption.AllDirectories));
        Assert.Contains(app.Log.Messages, message => message.Contains(": Fetch origin: Cancelled after ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_fetch_of_all_remotes_shows_its_progress_too_and_clicks_while_it_runs_queue_one_more_fetch()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var app = await OperationsHarness.OpenAsync(repo);

        var held = server.HoldNextFetchPack();
        IToolbarActions toolbar = app.Operations;
        var first = toolbar.FetchAllAsync();
        await held.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => app.StatusText.EndsWith('%'), "the fetch's percentage");

        // A running fetch has already asked for what the remote has: a click now waits behind it
        // and fetches again; more clicks while that one waits add nothing (1.2).
        var second = app.Operations.FetchAllAsync();
        var third = app.Operations.FetchAllAsync();

        Assert.Matches(@"^Fetching from all remotes… Counting objects \d{1,3}%$", app.StatusText);
        Assert.Equal(1, app.Repository.Queue.Waiting);

        server.Release();
        await Task.WhenAll(first, second, third).WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        Assert.Equal("Fetched from all remotes", app.StatusText);
        // The second fetch found nothing new, so only the first asked the server for a pack.
        Assert.Equal(2, Ran(app, "Fetch"));
        Assert.Single(server.Requests, request => request.Command == "fetch");
        Assert.Equal("Remote work", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());
        Assert.Equal(1, app.Operations.CurrentBranch?.Behind);
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task A_fetch_signs_in_through_the_credentials_dialog_a_cancel_ends_it_cancelled_and_a_wrong_password_fails()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var app = await OperationsHarness.OpenAsync(repo, host => new PromptDialogs(host));
        app.Dialogs
            .Answer<CredentialsViewModel>(dialog =>
            {
                dialog.UserName = "alice";
                dialog.Password = "s3cret";
                dialog.ConfirmCommand.Execute(null);
            })
            .Answer<CredentialsViewModel>(dialog => dialog.Cancel())
            .Answer<CredentialsViewModel>(dialog =>
            {
                dialog.UserName = "alice";
                dialog.Password = "wrong";
                dialog.ConfirmCommand.Execute(null);
            })
            .CloseError();

        // Check 21 step 1 and 2: the toolbar's Fetch; one dialog, the password kept for git's second question.
        await app.Operations.FetchAllAsync();
        var credentials = Assert.IsType<CredentialsViewModel>(app.Dialogs.Shown.Single());
        Assert.Equal($"Sign in to 127.0.0.1:{server.Port}", credentials.Title);
        Assert.Equal($"Git needs a user name and a password or token for http://127.0.0.1:{server.Port}.", credentials.Text);
        Assert.Equal("Fetched from all remotes", app.StatusText);
        Assert.Equal("Remote work", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());

        // Step 3: nothing was stored, so it asks again; Cancel ends the fetch as cancelled, without an error dialog.
        var before = await repo.RefTargetAsync("refs/remotes/origin/main");
        await app.Operations.FetchAllAsync();
        Assert.Equal(2, app.Dialogs.Shown.Count);
        Assert.Equal(("Fetch cancelled", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(before, await repo.RefTargetAsync("refs/remotes/origin/main"));

        // Step 4: a wrong password is a failure with git's own words.
        await app.Operations.FetchAsync("origin");
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Fetch failed", "Git could not fetch from origin.", "git fetch --progress origin"), (error.Title, error.Summary, error.Command));
        Assert.Contains("remote: Authentication required.", error.Output, StringComparison.Ordinal);
        Assert.Contains($"fatal: Authentication failed for 'http://127.0.0.1:{server.Port}/remote.git/'", error.Output, StringComparison.Ordinal);
        Assert.Equal(("Fetch origin failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();

        // Throughout: neither password is in a log line.
        Assert.DoesNotContain(app.Log.Messages, message => message.Contains("s3cret", StringComparison.Ordinal) || message.Contains("wrong", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cancel_at_a_prompt_ends_the_fetch_of_all_remotes_without_asking_for_the_next_remote()
    {
        // Two remotes on a host that asks for credentials; Cancel at the first dialog cancels the
        // whole fetch (2.16), so no second dialog asks for the other remote.
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var url = (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();
        await repo.GitAsync("remote", "add", "mirror", url);
        using var app = await OperationsHarness.OpenAsync(repo, host => new PromptDialogs(host));
        app.Dialogs.Answer<CredentialsViewModel>(dialog => dialog.Cancel());

        await app.Operations.FetchAllAsync();

        Assert.IsType<CredentialsViewModel>(Assert.Single(app.Dialogs.Shown));
        Assert.Equal(("Fetch cancelled", OperationStatusTone.Secondary, OperationOutcome.Cancelled), (app.StatusText, app.StatusTone, app.LastResult?.Outcome));
        Assert.Single(app.Calls.Snapshot(), call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_fetch_over_ssh_shows_the_passphrase_host_key_and_prompt_dialogs_and_then_its_failure()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var fakeSsh = await FakeSsh.AddRemoteAsync(repo);
        using var app = await OperationsHarness.OpenAsync(repo, host => new PromptDialogs(host));
        app.Dialogs
            .Answer<PassphraseViewModel>(dialog =>
            {
                dialog.Passphrase = "s3cret";
                dialog.ConfirmCommand.Execute(null);
            })
            .Answer<HostKeyViewModel>(dialog => dialog.ConfirmCommand.Execute(null))
            .Answer<PromptViewModel>(dialog =>
            {
                dialog.Answer = "123456";
                dialog.ConfirmCommand.Execute(null);
            })
            .CloseError();

        await app.Operations.FetchAsync(FakeSsh.RemoteName);

        Assert.Equal(["Key passphrase", "Unknown host", "Git asks", "Fetch failed"], app.Dialogs.Shown.Select(dialog => dialog.Title));
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Git could not fetch from fake-ssh.", "git fetch --progress fake-ssh"), (error.Summary, error.Command));
        Assert.StartsWith(FakeSsh.Refusal, error.Output, StringComparison.Ordinal);
        Assert.Equal("Fetch fake-ssh failed", app.StatusText);
        Assert.Equal(["s3cret", "yes", "123456"], fakeSsh.Answers);
        Assert.DoesNotContain(app.Log.Messages, message => message.Contains("s3cret", StringComparison.Ordinal) || message.Contains("123456", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_failing_fetch_clicked_twice_runs_once_and_opens_one_error_dialog()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompts = new ScriptedPromptHandler().Then(async (_, cancellationToken) =>
        {
            await gate.Task.WaitAsync(cancellationToken);
            return PromptAnswer.Refused;
        });
        using var app = await OperationsHarness.OpenAsync(repo, _ => prompts);
        app.Dialogs.CloseError();

        // Both clicks come while an operation ahead runs: the second is merged with the first,
        // which waits (1.2).
        var (hold, ahead) = HoldQueue(app);
        var first = app.Operations.FetchAsync("origin");
        var second = app.Operations.FetchAsync("origin");
        Assert.Equal(1, app.Repository.Queue.Waiting);
        hold.SetResult();
        await WaitUntilAsync(() => prompts.Prompts.Count == 1, "git's question");
        gate.SetResult();
        await Task.WhenAll(ahead, first, second).WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Fetch failed", "Git could not fetch from origin."), (error.Title, error.Summary));
        Assert.Contains("could not read Username", error.Output, StringComparison.Ordinal);
        Assert.Single(prompts.Prompts);
        Assert.Single(server.Requests);
        Assert.Equal("Fetch origin failed", app.StatusText);
        app.AssertScriptFollowed();
    }

    // Through the window (ShellDriver): checks 17 and 21 as a user starts them, the toolbar's
    // buttons clicked and the remote's "Fetch origin" called as its menu item calls it. Their
    // pictures are kept when VISUALCOMMIT_TEST_PICTURES names a folder (TestPictures).

    [AvaloniaFact]
    public async Task Through_the_window_a_held_fetch_shows_its_progress_and_Cancel_and_the_toolbars_Fetch_brings_the_remote_work()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync();
        var status = (TextBlock)app.FindByAutomationId("OperationStatus");
        var cancel = app.FindByAutomationId("OperationCancelButton");
        var progress = (ProgressBar)app.FindByAutomationId("OperationProgress");
        TestPictures.Save(app, "remotes-17a-before-fetch");
        Assert.Equal("Ready", status.Text);
        Assert.False(cancel.IsEffectivelyVisible);
        Assert.Empty(server.Requests);

        // Step 2: "Fetch origin", held with a percentage shown.
        var held = server.HoldNextFetchPack();
        var fetch = repository.Operations.FetchAsync("origin");
        await app.WaitForAsync(() => held.IsCompleted && status.Text?.EndsWith('%') == true, "the fetch's percentage", NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-17b-fetch-progress");
        Assert.Matches(@"^Fetching from origin… Counting objects \d{1,3}%$", status.Text);
        Assert.True(cancel.IsEffectivelyVisible);
        Assert.True(progress.IsEffectivelyVisible);
        Assert.False(progress.IsIndeterminate);
        Assert.InRange(progress.Value, 1, 100);
        var before = await repo.RefTargetAsync("refs/remotes/origin/main");

        // Step 3: Cancel in the status bar.
        app.Click(cancel);
        await app.WaitForAsync(() => fetch.IsCompleted, "the cancelled fetch to end", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        server.Release();
        TestPictures.Save(app, "remotes-17c-fetch-cancelled");
        Assert.Equal("Fetch origin cancelled", status.Text);
        Assert.False(cancel.IsEffectivelyVisible);
        Assert.False(progress.IsEffectivelyVisible);
        Assert.False(app.Session.Services.DialogHost.IsOpen);
        Assert.Equal(before, await repo.RefTargetAsync("refs/remotes/origin/main"));

        // Step 4: the toolbar's Fetch, held, then released.
        held = server.HoldNextFetchPack();
        app.Click(app.FindByAutomationId("FetchButton"));
        await app.WaitForAsync(() => held.IsCompleted && status.Text?.EndsWith('%') == true, "the toolbar fetch's percentage", NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-17d-fetch-all-progress");
        Assert.Matches(@"^Fetching from all remotes… Counting objects \d{1,3}%$", status.Text);
        Assert.True(cancel.IsEffectivelyVisible);

        server.Release();
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-17e-fetched");
        Assert.Equal("Fetched from all remotes", status.Text);
        Assert.Equal("Remote work", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());
        Assert.Equal(1, repository.Operations.CurrentBranch?.Behind);
        Assert.False(app.Session.Services.DialogHost.IsOpen);
    }

    [AvaloniaFact]
    public async Task Through_the_window_the_toolbars_Fetch_signs_in_through_the_credentials_dialog_and_a_wrong_password_fails()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync();
        var status = (TextBlock)app.FindByAutomationId("OperationStatus");

        // Check 21 step 1: the toolbar's Fetch asks for a sign-in.
        app.Click(app.FindByAutomationId("FetchButton"));
        var credentials = await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-21a-credentials");
        Assert.Equal($"Sign in to 127.0.0.1:{server.Port}", credentials.Title);
        Assert.Equal("Fetching from all remotes…", status.Text);
        Assert.True(app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible);

        // Step 2: sign in; git's second question is answered with the kept password.
        app.TypeInto("CredentialUsername", "alice");
        app.TypeInto("CredentialPassword", "s3cret");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-21b-signed-in");
        Assert.Equal("Fetched from all remotes", status.Text);
        Assert.False(app.Session.Services.DialogHost.IsOpen);
        Assert.Equal(1, repository.Operations.CurrentBranch?.Behind);

        // Step 3: the dialog comes again; Cancel ends the fetch as cancelled, without an error dialog.
        app.Click(app.FindByAutomationId("FetchButton"));
        await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-21c-cancelled");
        Assert.Equal("Fetch cancelled", status.Text);
        Assert.False(app.Session.Services.DialogHost.IsOpen);

        // Step 4: "Fetch origin" with a wrong password: the error dialog with git's own words.
        var fetch = repository.Operations.FetchAsync("origin");
        await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
        app.TypeInto("CredentialUsername", "alice");
        app.TypeInto("CredentialPassword", "wrong");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        var error = await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-21d-wrong-password");
        Assert.Equal(("Fetch failed", "Git could not fetch from origin.", "In auth", "git fetch --progress origin"), (error.Title, error.Summary, error.RepositoryText, error.Command));
        Assert.Contains($"fatal: Authentication failed for 'http://127.0.0.1:{server.Port}/remote.git/'", error.Output, StringComparison.Ordinal);
        Assert.Equal("Fetch origin failed", status.Text);
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => fetch.IsCompleted, "the failed fetch to end", NetworkTimeout);
        Assert.False(app.Session.Services.DialogHost.IsOpen);

        // Throughout: neither password is in a call record.
        Assert.All(app.Session.GitCalls.Snapshot(), call =>
        {
            Assert.DoesNotContain("s3cret", call.CommandText + call.StandardOutput + call.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("wrong", call.CommandText + call.StandardOutput + call.StandardError, StringComparison.Ordinal);
        });
    }
}
