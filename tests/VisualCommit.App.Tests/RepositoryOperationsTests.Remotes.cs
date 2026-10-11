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

namespace VisualCommit.App.Tests;

/// <summary>
/// The remote operations of <see cref="RepositoryOperations"/> (docs/phase-3-design.md 2.16 to
/// 2.23, 2.29, WP7-remotes) against the scenario repositories, through
/// <see cref="OperationsHarness"/>: for each, its confirmation, the backup it saves, what git holds
/// afterwards, the status bar's text, its failure dialog with git's own output and its extra
/// button, its cancel, a closed tab, and that a second click runs nothing more. Folder remotes
/// serve most of them; <see cref="AuthRemoteServer"/> holds a fetch for its progress and Cancel and
/// asks for a sign-in, and <see cref="FakeSsh"/> asks ssh's questions.
/// </summary>
public partial class RepositoryOperationsTests
{
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(60);

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
    public async Task The_fetch_of_all_remotes_shows_its_progress_too_and_a_second_click_while_it_runs_runs_nothing_more()
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
        var second = app.Operations.FetchAllAsync();

        Assert.Matches(@"^Fetching from all remotes… Counting objects \d{1,3}%$", app.StatusText);
        Assert.Equal(0, app.Repository.Queue.Waiting);

        server.Release();
        await Task.WhenAll(first, second).WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        Assert.Equal("Fetched from all remotes", app.StatusText);
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

        var first = app.Operations.FetchAsync("origin");
        await WaitUntilAsync(() => prompts.Prompts.Count == 1, "git's question");
        var second = app.Operations.FetchAsync("origin");
        Assert.Equal(0, app.Repository.Queue.Waiting);
        gate.SetResult();
        await Task.WhenAll(first, second).WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Fetch failed", "Git could not fetch from origin."), (error.Title, error.Summary));
        Assert.Contains("could not read Username", error.Output, StringComparison.Ordinal);
        Assert.Single(prompts.Prompts);
        Assert.Single(server.Requests);
        Assert.Equal("Fetch origin failed", app.StatusText);
        app.AssertScriptFollowed();
    }

    // 2.18: pull.

    [Fact]
    public async Task Pull_merges_diverged_branches_as_git_pull_would_and_says_so()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var app = await OperationsHarness.OpenAsync(repo);
        IToolbarActions toolbar = app.Operations;

        await toolbar.PullAsync();

        Assert.Equal(("Pulled and merged origin/main into main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        var parents = (await repo.GitAsync("rev-list", "--parents", "-n", "1", "HEAD")).StandardOutput.Trim().Split(' ');
        Assert.Equal([local, remote], parents[1..]);
        Assert.StartsWith("Merge branch 'main' of ", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal(remote, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(2, app.Operations.CurrentBranch?.Ahead);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task Pull_fast_forwards_counts_the_commits_and_says_when_there_was_nothing_to_pull()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullAsync();

        Assert.Equal("Pulled 1 commit into main (fast-forward)", app.StatusText);
        Assert.Equal(remote, await repo.HeadAsync());

        await app.Operations.PullAsync();

        Assert.Equal("main is already up to date", app.StatusText);
        Assert.Equal(remote, await repo.HeadAsync());
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Pull_refused_by_pull_ff_only_shows_gits_message_after_the_fetch_ran()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.ff", "only");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "Git could not pull origin/main into main.", "git merge --ff-only FETCH_HEAD"), (error.Title, error.Summary, error.Command));
        Assert.Contains("Not possible to fast-forward", error.Output, StringComparison.Ordinal);
        Assert.False(error.HasExtraAction);
        Assert.Equal(("Pull failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(local, await repo.HeadAsync());
        Assert.Equal(remote, await repo.RefTargetAsync("refs/remotes/origin/main"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Pull_with_a_setting_the_app_does_not_understand_says_which_and_changes_nothing()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var head = await repo.HeadAsync();
        await repo.ConfigAsync("pull.rebase", "preserve");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.PullAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Pull failed", "pull.rebase is set to preserve, which this app does not understand."), (error.Title, error.Summary));
        Assert.Equal("Pull failed", app.StatusText);
        Assert.Equal(head, await repo.HeadAsync());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Pull_that_stops_with_conflicts_says_so_in_the_warning_colour_without_a_dialog()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/shared.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/shared.txt", "local\n", "Local change");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullAsync();

        Assert.Equal(("Pull stopped: 1 file has conflicts", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        Assert.Equal(OperationOutcome.Conflicts, app.LastResult?.Outcome);
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Pull_rebases_when_git_is_told_to()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.ConfigAsync("pull.rebase", "true");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PullAsync();

        Assert.Equal("Pulled and rebased main onto origin/main", app.StatusText);
        Assert.Equal(remote, (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim());
        Assert.Equal("Local change", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Pull_from_a_local_upstream_skips_the_fetch_and_names_the_branch_alone()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        await repo.CheckoutAsync("loc");
        var main = await repo.RefTargetAsync("refs/heads/main");
        using var app = await OperationsHarness.OpenAsync(repo);
        var texts = RecordStatusTexts(app);

        await app.Operations.PullAsync();

        Assert.Contains("Pulling main into loc…", texts);
        Assert.Equal("Pulled 1 commit into loc (fast-forward)", app.StatusText);
        Assert.Equal(main, await repo.HeadAsync());
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Pull_cancelled_during_its_fetch_integrates_nothing()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await repo.HeadAsync();

        var held = server.HoldNextFetchPack();
        var pull = app.Operations.PullAsync();
        await held.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => app.StatusText.EndsWith('%'), "the pull's percentage");
        Assert.Matches(@"^Pulling origin/main into main… Counting objects \d{1,3}%$", app.StatusText);

        app.Repository.Queue.CancelCurrent();
        await pull.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        server.Release();

        Assert.Equal(("Pull cancelled", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        Assert.Empty(app.Dialogs.Shown);
    }

    // 2.19: push.

    [Fact]
    public async Task Push_of_the_current_branch_goes_to_its_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var app = await OperationsHarness.OpenAsync(repo);
        var texts = RecordStatusTexts(app);
        IToolbarActions toolbar = app.Operations;

        await toolbar.PushAsync();

        Assert.Contains("Pushing main to origin/main…", texts);
        Assert.Equal(("Pushed main to origin/main", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(local, await RemoteHeadAsync(repo, "refs/heads/main"));
        Assert.Equal(0, app.Operations.CurrentBranch?.Ahead);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task A_rejected_push_of_the_current_branch_offers_Pull_and_the_push_then_goes_through()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var remote = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError(clickExtra: true);

        await app.Operations.PushCurrentBranchAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Push rejected", "origin has commits that your main does not have. Pull first, then push again.", "Pull"), (error.Title, error.Summary, error.ExtraAction));
        Assert.Contains(" ! [rejected]        main -> main (fetch first)", error.Output, StringComparison.Ordinal);
        Assert.Equal("git push --progress origin refs/heads/main:refs/heads/main", error.Command);

        // "Pull" ran the pull, which merged.
        Assert.Equal("Pulled and merged origin/main into main", app.StatusText);
        var parents = (await repo.GitAsync("rev-list", "--parents", "-n", "1", "HEAD")).StandardOutput.Trim().Split(' ');
        Assert.Equal([local, remote], parents[1..]);

        await app.Operations.PushCurrentBranchAsync();

        Assert.Equal("Pushed main to origin/main", app.StatusText);
        Assert.Equal(await repo.HeadAsync(), await RemoteHeadAsync(repo, "refs/heads/main"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_rejected_push_of_another_branch_offers_Fetch_of_its_remote()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var elsewhere = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError(clickExtra: true);

        await app.Operations.PushBranchAsync("refs/heads/published");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Push rejected", "origin has commits that your published does not have. Pull first, then push again.", "Fetch origin"), (error.Title, error.Summary, error.ExtraAction));
        Assert.Contains(" ! [rejected]        published -> published (fetch first)", error.Output, StringComparison.Ordinal);
        Assert.Equal("git push --progress origin refs/heads/published:refs/heads/published", error.Command);
        Assert.Equal("Fetched from origin", app.StatusText);
        Assert.Equal(elsewhere, await repo.RefTargetAsync("refs/remotes/origin/published"));
        Assert.Equal((1, 1), (app.Operations.FindRef("refs/heads/published")?.Ahead, app.Operations.FindRef("refs/heads/published")?.Behind));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_push_that_fails_otherwise_opens_Push_failed_without_an_extra_button()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        await repo.GitAsync("remote", "set-url", "origin", repo.SiblingPath("nowhere.git"));
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.PushCurrentBranchAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Push failed", "Git could not push main to origin/main."), (error.Title, error.Summary));
        Assert.False(error.HasExtraAction);
        Assert.False(string.IsNullOrWhiteSpace(error.Output));
        Assert.Equal(("Push main failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Push_of_a_branch_without_an_upstream_goes_through_the_push_dialog_and_can_set_it_as_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var topic = await repo.RefTargetAsync("refs/heads/topic");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Push().Cancel();

        await app.Operations.PushBranchAsync("refs/heads/topic");

        var dialog = Assert.IsType<PushDialogViewModel>(app.Dialogs.Shown.Single());
        Assert.Equal(("Push topic", true, "origin", "topic", true), (dialog.Title, dialog.HasOneRemote, dialog.Remote, dialog.BranchName, dialog.SetUpstream));
        Assert.Equal("Pushed topic to origin/topic, and set it as upstream", app.StatusText);
        Assert.Equal(topic, await RemoteHeadAsync(repo, "refs/heads/topic"));
        Assert.Equal(topic, await repo.RefTargetAsync("refs/remotes/origin/topic"));
        Assert.Equal("refs/heads/topic", await ConfigAsync(repo, "branch.topic.merge"));
        Assert.Equal("origin/topic", app.Operations.FindRef("refs/heads/topic")?.Upstream);

        // Now it has an upstream: a push goes there without a dialog. A cancelled dialog of another branch runs nothing.
        await repo.GitAsync("branch", "other", "refs/heads/main");
        await app.Repository.RefreshAsync();
        var operations = app.Repository.Queue.LastResult;
        await app.Operations.PushBranchAsync("refs/heads/other");
        Assert.Same(operations, app.Repository.Queue.LastResult);
        Assert.Equal(2, app.Dialogs.Shown.Count);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Push_through_the_dialog_to_another_name_without_setting_the_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var topic = await repo.RefTargetAsync("refs/heads/topic");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Push(branch: "topic-2", setUpstream: false);

        await app.Operations.PushBranchAsync("refs/heads/topic");

        Assert.Equal("Pushed topic to origin/topic-2", app.StatusText);
        Assert.Equal(topic, await RemoteHeadAsync(repo, "refs/heads/topic-2"));
        Assert.Null(await ConfigAsync(repo, "branch.topic.merge"));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_drops_push_goes_to_the_remote_branch_it_was_dropped_on()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var topic = await repo.RefTargetAsync("refs/heads/topic");
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/origin/main", setUpstream: true);

        Assert.Equal("Pushed topic to origin/main, and set it as upstream", app.StatusText);
        Assert.Equal(topic, await RemoteHeadAsync(repo, "refs/heads/main"));
        Assert.Equal("refs/heads/main", await ConfigAsync(repo, "branch.topic.merge"));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Fetch_pull_and_push_without_choices_are_deduplicated_and_nothing_else_is()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var queue = new KeyRecordingQueue();
        var operations = new RepositoryOperations(app.Repository, queue, app.Services, app.Log);
        app.Dialogs.Push().Confirm().Pick("origin/main").Confirm();

        await operations.FetchAllAsync();
        await operations.FetchAsync("origin");
        await operations.PullAsync();
        await operations.PushCurrentBranchAsync();
        await operations.PushBranchAsync("refs/heads/topic");
        await operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/origin/main", setUpstream: false);
        await operations.PushBranchToAsync("refs/heads/topic", "refs/remotes/origin/main", setUpstream: true);
        await operations.ForcePushAsync("refs/heads/published");
        await operations.SetUpstreamAsync("refs/heads/topic");
        await operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");

        Assert.Equal(
            [
                ("Fetch", "fetch:*"),
                ("Fetch origin", "fetch:origin"),
                ("Pull", "pull:refs/heads/main"),
                ("Push main", "push:refs/heads/main->origin/refs/heads/main"),
                ("Push topic", null),
                ("Push topic", "push:refs/heads/topic->origin/refs/heads/main"),
                ("Push topic", null),
                ("Force push published", null),
                ("Set upstream of topic", null),
                ("Delete published on origin", null),
            ],
            queue.Operations.Select(operation => (operation.Title, operation.DedupeKey)));
        Assert.All(queue.Operations.Where(operation => operation.Kind == OperationKind.Network), operation => Assert.Equal(PromptMode.Ask, operation.Prompts));
        Assert.Equal(
            ["Fetching from all remotes…", "Fetching from origin…", "Pulling origin/main into main…", "Pushing main to origin/main…", "Pushing topic to origin/topic…"],
            queue.Operations.Take(5).Select(operation => operation.RunningText));
        app.AssertScriptFollowed();
    }

    // 2.20: force push with lease.

    [Fact]
    public async Task Force_push_confirms_backs_up_the_remote_branch_and_replaces_it_and_is_stopped_by_a_fetched_commit()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var published = await repo.RefTargetAsync("refs/remotes/origin/published");
        await repo.CheckoutAsync("published");
        await repo.GitAsync("reset", "--quiet", "--hard", "refs/heads/main");
        var rewritten = await repo.CommitFileAsync("src/published.txt", "rewritten\n", "Rewritten work");
        await repo.CheckoutAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm(yes: false).Confirm().Confirm().CloseError();

        // Declined: nothing.
        await app.Operations.ForcePushAsync("refs/heads/published");
        Assert.Equal(published, await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);

        // Check 20 step 3, which relies on published's reflog (12:02 to 12:05) being no older than
        // origin/published's last update (12:03), both older than the app's calls (D111).
        await app.Operations.ForcePushAsync("refs/heads/published");

        var confirmation = app.Dialogs.Confirmations[1];
        Assert.Equal(
            ("Force push published?",
                "Replace published on origin with your published? Commits on origin that your branch does not have are dropped there. The push stops if origin has commits you have not integrated into your published, even ones the last fetch brought.",
                "A backup is saved first, so this can be undone.",
                "Force push",
                true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Equal("Force-pushed published to origin/published", app.StatusText);
        Assert.Equal(rewritten, await RemoteHeadAsync(repo, "refs/heads/published"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/force-push-[0-9]{8}T[0-9]{9}Z/origin/published$", backup.Name);
        Assert.Equal(published, backup.Sha);

        // Steps 4 and 5: someone else pushed, the toolbar's Fetch brought it, a local commit follows; the lease stops the push.
        var someoneElse = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "rewritten\nsomeone else\n", "Someone else's work");
        await app.Operations.FetchAllAsync();
        await repo.CheckoutAsync("published");
        await repo.CommitFileAsync("src/more.txt", "more\n", "More rewritten work");
        await repo.CheckoutAsync("main");
        await app.Repository.RefreshAsync();

        await app.Operations.ForcePushAsync("refs/heads/published");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Force push stopped", "origin/published has commits that your published does not include. Pull or look at them before forcing."), (error.Title, error.Summary));
        Assert.Contains(" ! [rejected]        published -> published (remote ref updated since checkout)", error.Output, StringComparison.Ordinal);
        Assert.Equal("git push --progress --force-with-lease=refs/heads/published --force-if-includes origin refs/heads/published:refs/heads/published", error.Command);
        Assert.False(error.HasExtraAction);
        Assert.Equal(("Force push published failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(someoneElse, await RemoteHeadAsync(repo, "refs/heads/published"));
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        Assert.Contains(backups, entry => entry.Sha == someoneElse && entry.Name.EndsWith("/origin/published", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Force_push_of_a_branch_whose_upstream_has_another_name_asks_to_replace_the_upstream()
    {
        // fix tracks origin/main: the push replaces main on origin, which the question must name.
        using var repo = await Scenarios.RemoteSyncAsync();
        var main = await RemoteHeadAsync(repo, "refs/heads/main");
        await repo.GitAsync("branch", "--quiet", "--track", "fix", "origin/main");
        await repo.CheckoutAsync("fix");
        await repo.GitAsync("commit", "--quiet", "--amend", "-m", "Rewritten main work");
        await repo.CheckoutAsync("main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm(yes: false);

        await app.Operations.ForcePushAsync("refs/heads/fix");

        var confirmation = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("Force push fix?",
                "Replace main on origin with your fix? Commits on origin that your branch does not have are dropped there. The push stops if origin has commits you have not integrated into your fix, even ones the last fetch brought."),
            (confirmation.Title, confirmation.Question));
        Assert.Equal(main, await RemoteHeadAsync(repo, "refs/heads/main"));
        app.AssertScriptFollowed();
    }

    // 2.21: upstreams.

    [Fact]
    public async Task Set_change_and_stop_tracking_an_upstream()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var selectedOnOpening = new List<string?>();
        var itemsSeen = new List<string[]>();
        app.Dialogs
            .Answer<PickDialogViewModel>(dialog =>
            {
                selectedOnOpening.Add(dialog.Selected);
                itemsSeen.Add([.. dialog.Items]);
                dialog.Filter = "pub";
                itemsSeen.Add([.. dialog.Items]);
                dialog.Selected = "origin/published";
                dialog.ConfirmCommand.Execute(null);
            })
            .Answer<PickDialogViewModel>(dialog =>
            {
                selectedOnOpening.Add(dialog.Selected);
                dialog.Selected = "origin/main";
                dialog.ConfirmCommand.Execute(null);
            })
            .Cancel();

        await app.Operations.SetUpstreamAsync("refs/heads/topic");

        var pick = Assert.IsType<PickDialogViewModel>(app.Dialogs.Shown[0]);
        Assert.Equal(("Set upstream for topic", "Set upstream"), (pick.Title, pick.ConfirmText));
        Assert.Equal([["origin/main", "origin/published"], ["origin/published"]], itemsSeen);
        Assert.Equal("topic now tracks origin/published", app.StatusText);
        Assert.Equal(("origin", "refs/heads/published"), (await ConfigAsync(repo, "branch.topic.remote"), await ConfigAsync(repo, "branch.topic.merge")));
        var topic = app.Operations.FindRef("refs/heads/topic");
        Assert.Equal((1, 1), (topic?.Ahead, topic?.Behind));

        await app.Operations.SetUpstreamAsync("refs/heads/topic");

        Assert.Equal([null, "origin/published"], selectedOnOpening);
        Assert.Equal("topic now tracks origin/main", app.StatusText);
        Assert.Equal("refs/heads/main", await ConfigAsync(repo, "branch.topic.merge"));

        // A cancelled pick changes nothing.
        await app.Operations.SetUpstreamAsync("refs/heads/topic");
        Assert.Equal("topic now tracks origin/main", app.StatusText);

        await app.Operations.UnsetUpstreamAsync("refs/heads/topic");

        Assert.Equal("topic no longer tracks origin/main", app.StatusText);
        Assert.Null(await ConfigAsync(repo, "branch.topic.merge"));
        Assert.Null(await ConfigAsync(repo, "branch.topic.remote"));
        Assert.Null(app.Operations.FindRef("refs/heads/topic")?.Upstream);
        Assert.Empty(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    // 2.22: delete a branch on a remote.

    [Fact]
    public async Task Delete_on_a_remote_confirms_backs_up_and_deletes_with_a_lease()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var published = await repo.RefTargetAsync("refs/remotes/origin/published");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm(yes: false).Confirm();

        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");
        Assert.Equal(published, await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Empty(await repo.BackupRefsAsync());

        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");

        var confirmation = app.Dialogs.Confirmations[1];
        Assert.Equal(
            ("Delete published on origin?", "Delete the branch published on origin? Everyone who uses origin loses it.", "A backup is saved first, so this can be undone.", "Delete on origin", true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Equal("Deleted published on origin", app.StatusText);
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Null(await repo.RefTargetAsync("refs/remotes/origin/published"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/remote-branch-[0-9]{8}T[0-9]{9}Z/origin/published$", backup.Name);
        Assert.Equal(published, backup.Sha);
        Assert.True(app.Operations.FindRef("refs/heads/published")?.UpstreamGone);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Delete_on_a_remote_is_stopped_when_someone_pushed_since_the_last_fetch()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var published = await repo.RefTargetAsync("refs/remotes/origin/published");
        var elsewhere = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm().CloseError();

        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Deletion stopped", "published on origin has changed since you last fetched. Fetch and look at it before deleting."), (error.Title, error.Summary));
        Assert.Contains("stale info", error.Output, StringComparison.Ordinal);
        Assert.Equal($"git push --progress --force-with-lease=refs/heads/published:{published} origin --delete refs/heads/published", error.Command);
        Assert.Equal(("Delete published on origin failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal(elsewhere, await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Equal(published, Assert.Single(await repo.BackupRefsAsync()).Sha);
        app.AssertScriptFollowed();
    }

    // 2.23: remotes.

    [Fact]
    public async Task Add_rename_change_the_url_and_remove_a_remote()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var shared = await repo.RefTargetAsync("refs/heads/main");
        var published = await repo.RefTargetAsync("refs/remotes/origin/published");
        using var app = await OperationsHarness.OpenAsync(repo);
        IRemoteActions remotes = app.Operations;
        const string Token = "https://alice:ghp_SECRET123@127.0.0.1:1/x.git";
        app.Dialogs.Confirm(yes: false).Confirm();

        // Add with "Fetch now": the fetch is an operation of its own, under its own title.
        Assert.Null(await remotes.AddRemoteAsync("mirror", repo.SiblingPath("origin.git"), fetchNow: true));
        await app.WaitForIdleAsync();
        Assert.Equal("Fetched from mirror", app.StatusText);
        Assert.Equal(shared, await repo.RefTargetAsync("refs/remotes/mirror/main"));
        Assert.Contains(app.Log.Messages, message => message.Contains(": Add remote mirror: Succeeded after ", StringComparison.Ordinal));

        // Git's refusal goes back to the form, without a dialog.
        var refused = await remotes.AddRemoteAsync("origin", repo.SiblingPath("origin.git"), fetchNow: true);
        Assert.Equal("error: remote origin already exists.", refused);
        Assert.Equal("Add remote origin failed", app.StatusText);

        Assert.Null(await remotes.SetRemoteUrlAsync("mirror", Token));
        Assert.Equal("Changed the URL of mirror", app.StatusText);
        Assert.Equal(Token, (await repo.GitAsync("remote", "get-url", "mirror")).StandardOutput.Trim());
        Assert.Equal("https://***@127.0.0.1:1/x.git", (await remotes.ReadRemotesAsync()).Single(remote => remote.Name == "mirror").ShownUrl);

        Assert.Null(await remotes.RenameRemoteAsync("mirror", "backup"));
        Assert.Equal("Renamed remote mirror to backup", app.StatusText);
        Assert.Equal(["backup", "origin"], app.Operations.RemoteNames);

        // Remove: declined, then confirmed; the backup holds every remote branch.
        await remotes.RemoveRemoteAsync("backup");
        Assert.Equal(["backup", "origin"], app.Operations.RemoteNames);
        await remotes.RemoveRemoteAsync("backup");

        var confirmation = app.Dialogs.Confirmations[1];
        Assert.Equal(
            ("Remove remote backup?",
                "Remove the remote backup and its 2 remote branches? Your local branches stay; those that track it lose their upstream. Its URL was https://***@127.0.0.1:1/x.git.",
                "A backup is saved first, so this can be undone.",
                "Remove",
                true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Equal("Removed remote backup", app.StatusText);
        Assert.Equal("origin", (await repo.GitAsync("remote")).StandardOutput.Trim());
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(2, backups.Count);
        Assert.Matches("^refs/visualcommit/backup/remote-[0-9]{8}T[0-9]{9}Z/backup/main$", backups[0].Name);
        Assert.Equal((shared, published), (backups[0].Sha, backups[1].Sha));
        Assert.Equal(backups[0].Name[..^"main".Length] + "published", backups[1].Name);

        // D103: the token is in no log line, and nowhere under .git once the remote is gone.
        Assert.DoesNotContain(app.Log.Messages, message => message.Contains("ghp_SECRET123", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.Combine(repo.Path, ".git"), "*", SearchOption.AllDirectories),
            file => File.ReadAllText(file).Contains("ghp_SECRET123", StringComparison.Ordinal));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_remotes_dialog_opens_on_its_list_its_add_form_or_a_remotes_form_and_removes_with_the_confirmation_on_top()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var url = (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Keep().Keep().Keep();

        var managing = app.Operations.ManageRemotesAsync();
        var list = await app.Dialogs.WaitForAsync<RemotesDialogViewModel>();
        Assert.Equal((true, "origin", url), (list.IsListShown, list.Rows.Single().Name, list.Rows.Single().ShownUrl));
        list.Cancel();
        await managing.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        var adding = app.Operations.ShowAddRemoteAsync();
        await WaitUntilAsync(() => app.Dialogs.Shown.Count == 2, "the second dialog");
        var add = Assert.IsType<RemotesDialogViewModel>(app.Dialogs.Shown[1]);
        Assert.Equal((true, true, string.Empty, true), (add.IsFormShown, add.IsAdding, add.RemoteName, add.FetchNow));
        add.HandleEscape();
        add.HandleEscape();
        await adding.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);

        app.Dialogs.Confirm();
        var editing = app.Operations.ShowEditRemoteAsync("origin");
        await WaitUntilAsync(() => app.Dialogs.Shown.Count == 3, "the third dialog");
        var edit = Assert.IsType<RemotesDialogViewModel>(app.Dialogs.Shown[2]);
        Assert.Equal((true, false, "origin", url), (edit.IsFormShown, edit.IsAdding, edit.RemoteName, edit.RemoteUrl));
        edit.HandleEscape();
        await edit.RemoveCommand.ExecuteAsync(edit.Rows.Single());

        // The confirmation came on top of the dialog, which stays open and lists nothing now.
        Assert.Equal("Remove remote origin?", app.Dialogs.Confirmations.Single().Title);
        Assert.Same(edit, app.DialogHost.Current);
        Assert.Empty(edit.Rows);
        Assert.Equal("Removed remote origin", app.StatusText);
        edit.Cancel();
        await editing.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        app.AssertScriptFollowed();
    }

    // 2.29: tags on a remote.

    [Fact]
    public async Task Push_a_tag_to_the_one_remote_then_delete_it_there_with_a_backup_and_a_lease()
    {
        using var repo = await Scenarios.OperationsAsync();
        var tagObject = await repo.RefTargetAsync("refs/tags/v1.0");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm().Confirm().CloseError();

        await app.Operations.PushTagAsync("refs/tags/v1.0");

        Assert.Equal("Pushed tag v1.0 to origin", app.StatusText);
        Assert.Equal(tagObject, await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        Assert.Empty(app.Dialogs.Shown);

        await app.Operations.DeleteRemoteTagAsync("refs/tags/v1.0");

        var confirmation = app.Dialogs.Confirmations[0];
        Assert.Equal(
            ("Delete v1.0 on origin?", "Delete the tag v1.0 on origin? Everyone who uses origin loses it.", "A backup is saved first, so this can be undone.", "Delete on origin", true),
            (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Equal("Deleted tag v1.0 on origin", app.StatusText);
        Assert.Null(await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        Assert.Equal(tagObject, await repo.RefTargetAsync("refs/tags/v1.0"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/remote-tag-[0-9]{8}T[0-9]{9}Z/origin/v1.0$", backup.Name);
        Assert.Equal(tagObject, backup.Sha);

        // A remote without the tag: the lease refuses, and the deletion says so.
        await app.Operations.DeleteRemoteTagAsync("refs/tags/v1.1");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Deletion stopped", "origin has no tag v1.1, or its v1.1 is not the same as yours."), (error.Title, error.Summary));
        Assert.Contains("stale info", error.Output, StringComparison.Ordinal);
        Assert.Equal(("Delete tag v1.1 on origin failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task With_several_remotes_a_tag_is_pushed_and_deleted_on_the_remote_chosen()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.AddBareRemoteAsync("mirror");
        var tagObject = await repo.RefTargetAsync("refs/tags/v1.0");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Cancel().Choose("origin").Choose("mirror").Confirm(yes: false).Choose("origin").Confirm();

        await app.Operations.PushTagAsync("refs/tags/v1.0");
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);

        await app.Operations.PushTagAsync("refs/tags/v1.0");

        var choice = Assert.IsType<ChoiceDialogViewModel>(app.Dialogs.Shown[1]);
        Assert.Equal(("Push v1.0 to…", "Push", false), (choice.Title, choice.ConfirmText, choice.IsDestructive));
        Assert.Equal(["mirror", "origin"], choice.Options.Select(option => option.Text));
        Assert.Equal("Pushed tag v1.0 to origin", app.StatusText);
        Assert.Equal(tagObject, await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/tags/v1.0", "mirror"));

        await app.Operations.DeleteRemoteTagAsync("refs/tags/v1.0");
        await app.Operations.DeleteRemoteTagAsync("refs/tags/v1.0");

        var deleteChoice = Assert.IsType<ChoiceDialogViewModel>(app.Dialogs.Shown[2]);
        Assert.Equal(("Delete v1.0 on…", "Delete", true), (deleteChoice.Title, deleteChoice.ConfirmText, deleteChoice.IsDestructive));
        Assert.Equal(["Delete v1.0 on mirror?", "Delete v1.0 on origin?"], app.Dialogs.Confirmations.Select(request => request.Title));
        Assert.Equal("Deleted tag v1.0 on origin", app.StatusText);
        Assert.Null(await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        app.AssertScriptFollowed();
    }

    // A closed tab.

    [Fact]
    public async Task After_the_tab_closed_no_remote_operation_starts_or_shows_anything()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        var remoteMain = await RemoteHeadAsync(repo, "refs/heads/main");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Repository.Dispose();

        await app.Operations.FetchAllAsync();
        await app.Operations.FetchAsync("origin");
        await app.Operations.PullAsync();
        await app.Operations.PushCurrentBranchAsync();
        await app.Operations.PushBranchAsync("refs/heads/topic");
        await app.Operations.ForcePushAsync("refs/heads/published");
        await app.Operations.SetUpstreamAsync("refs/heads/topic");
        await app.Operations.UnsetUpstreamAsync("refs/heads/main");
        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");
        await app.Operations.ManageRemotesAsync();
        await app.Operations.RemoveRemoteAsync("origin");
        await app.Operations.PushTagAsync("refs/tags/none");
        IRemoteActions remotes = app.Operations;
        Assert.NotNull(await remotes.AddRemoteAsync("mirror", repo.SiblingPath("origin.git"), fetchNow: true));

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(remoteMain, await RemoteHeadAsync(repo, "refs/heads/main"));
        Assert.Equal("origin", (await repo.GitAsync("remote")).StandardOutput.Trim());
        Assert.Equal("refs/heads/main", await ConfigAsync(repo, "branch.main.merge"));
        Assert.NotNull(await RemoteHeadAsync(repo, "refs/heads/published"));
        Assert.Empty(await repo.BackupRefsAsync());
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

    [AvaloniaFact]
    public async Task Through_the_window_the_dialogs_of_the_remote_operations_open_and_their_results_show()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/remote.txt", "remote\n", "Remote change");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local change");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync();
        var status = (TextBlock)app.FindByAutomationId("OperationStatus");
        var operations = repository.Operations;

        // Check 19 steps 1 and 2: the toolbar's Push is rejected; "Pull" pulls.
        app.Click(app.FindByAutomationId("PushButton"));
        var rejected = await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-19a-push-rejected");
        Assert.Equal(("Push rejected", "Pull"), (rejected.Title, rejected.ExtraAction));
        Assert.Equal("Push main failed", status.Text);
        app.Click(app.FindByAutomationId("ErrorExtraButton"));
        await app.WaitForAsync(() => status.Text == "Pulled and merged origin/main into main", "the pull", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-19b-pulled");
        Assert.False(app.Session.Services.DialogHost.IsOpen);

        // Check 28 step 2: the pick dialog, filtered, an item clicked.
        var picking = operations.SetUpstreamAsync("refs/heads/topic");
        await app.WaitForDialogAsync<PickDialogViewModel>(NetworkTimeout);
        app.TypeInto("PickFilter", "pub");
        app.Click(DialogControl(app, "PickItem", "origin/published"));
        app.Settle();
        TestPictures.Save(app, "remotes-28b-pick");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => picking.IsCompleted, "the upstream to be set", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        Assert.Equal("topic now tracks origin/published", status.Text);
        await operations.UnsetUpstreamAsync("refs/heads/topic");
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        Assert.Equal("topic no longer tracks origin/published", status.Text);

        // Check 29 steps 1 and 2: the push dialog of a branch without an upstream.
        var pushing = operations.PushBranchAsync("refs/heads/topic");
        await app.WaitForDialogAsync<PushDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-29a-push-dialog");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => pushing.IsCompleted, "the push", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-29b-pushed");
        Assert.Equal("Pushed topic to origin/topic, and set it as upstream", status.Text);

        // Check 20's confirmation, cancelled.
        var forcing = operations.ForcePushAsync("refs/heads/published");
        await app.WaitForDialogAsync<ConfirmationViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-20c-force-push-confirmation");
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => forcing.IsCompleted, "the cancelled force push", NetworkTimeout);
        Assert.Empty(await repo.BackupRefsAsync());

        // Check 30 step 4: delete a branch on its remote.
        var deleting = operations.DeleteRemoteBranchAsync("refs/remotes/origin/published");
        await app.WaitForDialogAsync<ConfirmationViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-30d-delete-confirmation");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => deleting.IsCompleted, "the deletion", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);
        TestPictures.Save(app, "remotes-30e-deleted");
        Assert.Equal("Deleted published on origin", status.Text);

        // Check 22: the Remotes dialog, its form, and the removal's confirmation stacked over it (cancelled).
        var managing = operations.ManageRemotesAsync();
        await app.WaitForDialogAsync<RemotesDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-22a-remotes");
        app.Click(app.FindByAutomationId("AddRemoteButton"));
        app.TypeInto("RemoteNameBox", "mirror");
        app.Settle();
        TestPictures.Save(app, "remotes-22-add-form");
        app.Click(app.FindByAutomationId("CancelButton"));
        app.Click(app.FindByAutomationId("RemoteRemoveButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-22d-remove-stacked");
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForDialogAsync<RemotesDialogViewModel>(NetworkTimeout);
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => managing.IsCompleted, "the Remotes dialog to close", NetworkTimeout);
        Assert.Equal("origin", (await repo.GitAsync("remote")).StandardOutput.Trim());
    }

    [AvaloniaFact]
    public async Task Through_the_window_a_tag_is_pushed_to_the_remote_chosen_in_the_choice_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.AddBareRemoteAsync("mirror");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync();

        var pushing = repository.Operations.PushTagAsync("refs/tags/v1.0");
        await app.WaitForDialogAsync<ChoiceDialogViewModel>(NetworkTimeout);
        app.Settle();
        TestPictures.Save(app, "remotes-29c-choice");
        app.Click(DialogControl(app, "ChoiceOption", "origin"));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => pushing.IsCompleted, "the push", NetworkTimeout);
        await app.WaitForIdleAsync(repository, NetworkTimeout);

        Assert.Equal("Pushed tag v1.0 to origin", ((TextBlock)app.FindByAutomationId("OperationStatus")).Text);
        Assert.Equal(await repo.RefTargetAsync("refs/tags/v1.0"), await RemoteHeadAsync(repo, "refs/tags/v1.0"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/tags/v1.0", "mirror"));
    }

    /// <summary>The control <paramref name="automationId"/> of the dialog on top that stands for <paramref name="text"/> (a list item, a radio button).</summary>
    private static Control DialogControl(ShellDriver app, string automationId, string text)
    {
        var dialog = app.TopDialogView ?? throw new InvalidOperationException("No dialog is open.");
        return dialog.GetVisualDescendants().OfType<Control>().First(control =>
            AutomationProperties.GetAutomationId(control) == automationId
            && control.IsEffectivelyVisible
            && (control.DataContext is string item ? item == text : control.DataContext is ChoiceOption option && option.Text == text));
    }

    /// <summary>What <paramref name="remote"/> (a folder remote) has at <paramref name="fullRef"/>, unpeeled; null when it has nothing there.</summary>
    private static async Task<string?> RemoteHeadAsync(TempRepo repo, string fullRef, string remote = "origin")
    {
        var output = (await repo.GitAsync("ls-remote", remote, fullRef)).StandardOutput;
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t'))
            .FirstOrDefault(fields => fields.Length == 2 && fields[1].Trim() == fullRef)?[0];
    }

    /// <summary>A setting's value, or null when it is not set.</summary>
    private static async Task<string?> ConfigAsync(TempRepo repo, string key)
    {
        var result = await repo.GitAllowingFailureAsync("config", "--get", key);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>Every text the status bar shows from now on, in order (the running texts too).</summary>
    private static List<string> RecordStatusTexts(OperationsHarness app)
    {
        var texts = new List<string>();
        app.Repository.OperationStatus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(OperationStatusViewModel.Text))
            {
                lock (texts)
                {
                    texts.Add(app.Repository.OperationStatus.Text);
                }
            }
        };
        return texts;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + NetworkTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A queue that runs nothing: it records each operation, as given, and answers that it was cancelled.</summary>
    private sealed class KeyRecordingQueue : IOperationQueue
    {
        private readonly List<GitOperation> _operations = [];

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public event EventHandler<OperationFinishedEventArgs>? Finished
        {
            add { }
            remove { }
        }

        public IReadOnlyList<GitOperation> Operations => _operations;

        public OperationSnapshot? Current => null;

        public int Waiting => 0;

        public OperationResult? LastResult => null;

        public Task<OperationResult> RunAsync(GitOperation operation)
        {
            _operations.Add(operation);
            return Task.FromResult(new OperationResult(OperationOutcome.Cancelled, $"{operation.Title} cancelled", null));
        }

        public bool TryStartInBackground(GitOperation operation) => false;

        public Task WhenIdleAsync() => Task.CompletedTask;

        public void CancelCurrent()
        {
        }

        public void DropWaiting()
        {
        }

        public void Dispose()
        {
        }
    }
}
