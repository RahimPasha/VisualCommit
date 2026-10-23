using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// Fetch refspecs (docs/phase-6-design.md 10.4, 10.6, 10.9; D201) where 10.9 asks for more than the
/// remotes' and pushes' tests had: a double-click on a ref its remote fetches from two refs, with
/// no branch of its name yet, creates that branch without an upstream; and deleting a renamed
/// remote branch whose name has a folder (fork's <c>feature/x</c>, tracked as
/// <c>fork/up/feature/x</c>) deletes the branch by its whole name on the remote, with the
/// mapped lease and backup, which Undo and "Push … back" bring back. On "refspecs" (RS0), in
/// WP7-remotes' collection.
/// </summary>
[Collection(RemotesCollection)]
public class RepositoryOperationsRefspecFolderTests
{
    private const string Parser = "8394a5e2e769c4601161cbce8c239ff2aa246eb9";
    private const string Topic = "de4a87cd5a84d5ed135863187487f81fe89d030c";

    [Fact]
    public async Task A_double_click_on_a_ref_its_remote_fetches_from_two_refs_creates_its_branch_without_an_upstream()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Settings.Update(settings => settings with { KeepIgnoredFiles = false });
        Assert.Null(await repo.RefTargetAsync("refs/heads/pr/1"));
        var mark = app.Calls.Snapshot().Count;

        app.Repository.DoubleTapRefRow(LeftPanelRow(app, "ref:refs/remotes/origin/pr/1", "remote:origin:pr"));
        await WaitUntilAsync(() => Ran(app, "Check out origin/pr/1") == 1, "the double-click's checkout");
        await app.WaitForIdleAsync();

        // D201: git would track its first candidate, refs/heads/pr/1, whose push stops every fetch.
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(("Created pr/1 at origin/pr/1 without an upstream: origin/pr/1 is fetched from more than one ref of origin", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        var calls = app.Calls.Snapshot().Skip(mark).ToList();
        Assert.Contains(calls, call => call.CommandText == "git switch --create pr/1 --no-track refs/remotes/origin/pr/1" && call.Succeeded);
        Assert.DoesNotContain(calls, call => call.CommandText.Contains(" --track ", StringComparison.Ordinal));
        Assert.Equal("refs/heads/pr/1", (await repo.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
        Assert.Equal(Topic, await repo.RefTargetAsync("refs/heads/pr/1"));
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.pr/1\.")).ExitCode);
        Assert.Null(app.Operations.FindRef("refs/heads/pr/1")!.UpstreamRef);
    }

    [Fact]
    public async Task Deleting_a_renamed_remote_branch_in_a_folder_deletes_it_by_its_whole_name_with_the_mapped_lease_and_backup()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.PushAsync("fork", setUpstream: false, "topic:refs/heads/feature/x");
        await repo.GitAsync("fetch", "--quiet", "fork");
        Assert.Equal(Topic, await repo.RefTargetAsync("refs/remotes/fork/up/feature/x"));
        using var app = await OperationsHarness.OpenAsync(repo);
        var texts = RecordStatusTexts(app);
        app.Dialogs.Confirm();

        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/fork/up/feature/x");

        // 10.4 #9: fork's feature/x, the lease on it at the tracking ref's commit.
        var confirmation = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("Delete feature/x on fork?", "Delete the branch feature/x on fork? Everyone who uses fork loses it.", "Delete on fork", true),
            (confirmation.Title, confirmation.Question, confirmation.ConfirmText, confirmation.IsDestructive));
        Assert.Contains("Deleting feature/x on fork…", texts);
        Assert.Equal("Deleted feature/x on fork", app.StatusText);
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == $"git push --progress --force-with-lease=refs/heads/feature/x:{Topic} fork --delete refs/heads/feature/x" && call.Succeeded);
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/feature/x", "fork"));
        Assert.Equal(Parser, await RemoteHeadAsync(repo, "refs/heads/main", "fork"));
        Assert.Null(await repo.RefTargetAsync("refs/remotes/fork/up/feature/x"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches($"^refs/visualcommit/backup/remote-branch-{BackupTime}/fork/feature/x$", backup.Name);
        Assert.Equal(Topic, backup.Sha);

        // "Push feature/x back to fork…" brings it back, by the name the backup recorded.
        var saved = (await app.Repository.Repository.ReadBackupsAsync(TestContext.Current.CancellationToken)).Single(entry => entry.Kind == "remote-branch");
        app.Dialogs.Confirm();
        await app.Operations.RestoreBackupAsync(saved);
        Assert.Equal("Push feature/x back to fork?", app.Dialogs.Confirmations[^1].Title);
        Assert.Equal("Pushed feature/x back to fork", app.StatusText);
        Assert.Equal(Topic, await RemoteHeadAsync(repo, "refs/heads/feature/x", "fork"));
        Assert.Equal(Topic, await repo.RefTargetAsync("refs/remotes/fork/up/feature/x"));

        // Deleted again, then undone: the undo pushes feature/x back to fork, and nothing named up/….
        app.Dialogs.Confirm().Confirm();
        await app.Operations.DeleteRemoteBranchAsync("refs/remotes/fork/up/feature/x");
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/feature/x", "fork"));
        await app.Operations.UndoAsync();
        Assert.Equal(Topic, await RemoteHeadAsync(repo, "refs/heads/feature/x", "fork"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/up/feature/x", "fork"));
        Assert.Null(await RemoteHeadAsync(repo, "refs/heads/x", "fork"));
        app.AssertScriptFollowed();
    }

    /// <summary>The left panel's row <paramref name="key"/>, its folder <paramref name="folder"/> opened first as a click opens it.</summary>
    private static RefListRow LeftPanelRow(OperationsHarness app, string key, string folder)
    {
        var panel = app.Repository.LeftPanel;
        if (panel.Rows.SingleOrDefault(row => row.Key == folder) is RefItemRow { IsOpen: false } closed)
        {
            panel.Activate(closed);
        }

        return panel.Rows.Single(row => row.Key == key);
    }
}
