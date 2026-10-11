using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the phase-3 review found in the dialogs: the Remotes form after a rename whose URL change
/// failed, a branch rename that changes only the case, and remote names cut without a tooltip.
/// </summary>
public class DialogReviewFixTests
{
    private const string LongRemote = "company-internal-mirror-for-continuous-integration-builds-a";
    private const string OtherLongRemote = "company-internal-mirror-for-continuous-integration-builds-b";

    [Fact]
    public async Task A_Save_whose_url_change_fails_after_the_rename_goes_on_with_the_new_name_and_Save_again_changes_only_the_url()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var configLock = Path.Combine(repo.Path, ".git", "config.lock");
        var dialog = new RemotesDialogViewModel(new LockingConfigAfterRename(app.Operations, configLock));
        await dialog.Loading;
        var url = repo.SiblingPath("moved.git");

        dialog.EditCommand.Execute(dialog.Rows.Single(row => row.Name == "origin"));
        dialog.RemoteName = "upstream";
        dialog.RemoteUrl = url;
        await dialog.SaveCommand.ExecuteAsync(null);

        // Git renamed the remote, then could not change its URL: the form stays, the list has the new name.
        Assert.Contains("could not lock config file", dialog.RemoteError, StringComparison.Ordinal);
        Assert.True(dialog.IsFormShown);
        Assert.Equal(["upstream"], dialog.Rows.Select(row => row.Name));
        Assert.Equal("upstream", (await repo.GitAsync("remote")).StandardOutput.Trim());

        // Whatever held the configuration lets go; Save again changes the URL of upstream.
        File.Delete(configLock);
        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, dialog.RemoteError);
        Assert.False(dialog.IsFormShown);
        Assert.Equal(url, (await repo.GitAsync("remote", "get-url", "upstream")).StandardOutput.Trim());
        Assert.Equal([("upstream", url)], dialog.Rows.Select(row => (row.Name, row.Url)));
    }

    [Fact]
    public async Task Renaming_a_branch_only_in_case_is_refused_where_git_refuses_it()
    {
        // What git does with `git branch --move Topic topic` here: on Windows and macOS the old
        // ref's file answers for the new name and git refuses; on Linux it renames.
        using var repo = await Scenarios.OperationsAsync();
        await repo.CreateBranchAsync("Topic");
        var moved = await repo.GitAllowingFailureAsync("branch", "--move", "Topic", "topic");
        var gitRenames = moved.ExitCode == 0;
        Assert.Equal(!RefNameRules.NamesIgnoreCase, gitRenames);
        if (!gitRenames)
        {
            Assert.Contains("a branch named 'topic' already exists", moved.StandardError, StringComparison.Ordinal);
        }

        // The dialog with this platform's rule says the same before git is asked.
        string[] existing = ["Topic", "feature/alpha", "feature/conflict", "feature/ff", "main"];
        var dialog = BranchNameDialogViewModel.Rename("Topic", existing);
        dialog.Name = "topic";
        Assert.Equal(gitRenames, dialog.CanConfirm);
        Assert.Equal(gitRenames ? string.Empty : "A branch named Topic exists in another case.", dialog.NameError);

        // Both rules, whatever the platform.
        var insensitive = BranchNameDialogViewModel.Rename("Topic", ["Topic", "main"], ignoreCase: true);
        insensitive.Name = "topic";
        Assert.Equal(("A branch named Topic exists in another case.", false), (insensitive.NameError, insensitive.CanConfirm));
        var sensitive = BranchNameDialogViewModel.Rename("Topic", ["Topic", "main"], ignoreCase: false);
        sensitive.Name = "topic";
        Assert.Equal((string.Empty, true), (sensitive.NameError, sensitive.CanConfirm));

        // A folder of the branch's own name is no clash: git moves the branch into it.
        insensitive.Name = "topic/x";
        Assert.Equal((string.Empty, true), (insensitive.NameError, insensitive.CanConfirm));
        insensitive.Name = "Topic/x";
        Assert.Equal((string.Empty, true), (insensitive.NameError, insensitive.CanConfirm));
    }

    [AvaloniaFact]
    public async Task A_remote_name_cut_in_the_choice_and_push_dialogs_shows_its_whole_name_as_tooltip()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        app.Window.RequestedThemeVariant = ThemeVariant.Dark;
        var dialogs = app.Session.Services.DialogHost;

        var choice = dialogs.ShowAsync(new ChoiceDialogViewModel("Push v1.0 to…", [LongRemote + "-and-more-and-more", OtherLongRemote + "-and-more-and-more"], "Push"));
        AssertCutWithToolTips(app, "push-tag-choice", LongRemote, OtherLongRemote);
        Assert.True(dialogs.CancelCurrent());
        await choice;

        var push = dialogs.ShowAsync(new PushDialogViewModel("feature/alpha", [LongRemote + "-and-more-and-more", OtherLongRemote + "-and-more-and-more"]));
        AssertCutWithToolTips(app, "push-dialog-remotes", LongRemote, OtherLongRemote);
        Assert.True(dialogs.CancelCurrent());
        await push;

        push = dialogs.ShowAsync(new PushDialogViewModel("feature/alpha", [LongRemote + "-and-more-and-more"]));
        AssertCutWithToolTips(app, "push-dialog-one-remote", LongRemote);
        Assert.True(dialogs.CancelCurrent());
        await push;
    }

    /// <summary>
    /// The dialog on top cuts each of <paramref name="names"/> with "…" (the case this is about),
    /// and the layout audit lets them through only because each has its whole text as tooltip.
    /// </summary>
    private static void AssertCutWithToolTips(ShellDriver app, string picture, params string[] names)
    {
        app.Settle();
        TestPictures.Save(app, picture);
        var cut = LayoutAudit.FindClippedText(app.Window);
        foreach (var name in names)
        {
            Assert.Contains(cut, problem => problem.Contains(name, StringComparison.Ordinal));
        }

        Assert.DoesNotContain(
            LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true),
            problem => names.Any(name => problem.Contains(name, StringComparison.Ordinal)));
        var texts = app.Window.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && names.Any(name => text.Text?.StartsWith(name, StringComparison.Ordinal) == true))
            .ToList();
        Assert.Equal(names.Length, texts.Count);
        Assert.All(texts, text => Assert.Equal(text.Text, ToolTip.GetTip(text)));
    }

    /// <summary>
    /// The repository's own remote actions (real git), except that a rename leaves the
    /// configuration locked behind it, as another program holding <c>.git/config</c> would.
    /// </summary>
    private sealed class LockingConfigAfterRename(IRemoteActions inner, string configLock) : IRemoteActions
    {
        public Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync() => inner.ReadRemotesAsync();

        public Task<string?> AddRemoteAsync(string name, string url, bool fetchNow) => inner.AddRemoteAsync(name, url, fetchNow);

        public async Task<string?> RenameRemoteAsync(string name, string newName)
        {
            var error = await inner.RenameRemoteAsync(name, newName);
            await File.WriteAllTextAsync(configLock, string.Empty);
            return error;
        }

        public Task<string?> SetRemoteUrlAsync(string name, string url) => inner.SetRemoteUrlAsync(name, url);

        public Task RemoveRemoteAsync(string name) => inner.RemoveRemoteAsync(name);
    }
}
