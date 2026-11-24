using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Git;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The settings screen's "Git program" row (docs/phase-8a-design.md 5.3; rulings 40, 42): the
/// pending choice, else the git in use, else <c>None found</c>; the note on where it came from and,
/// while the choice differs from the git in use, when it applies; Browse's checks; the button that
/// drops the choice, by name. Save writes the choice (<see cref="GitProgramViewModel.IsChanged"/>).
/// </summary>
public class GitProgramViewModelTests
{
    private const string WindowsGit = @"C:\Program Files\Git\cmd\git.exe";

    private static readonly string Chosen = Path.Combine(Path.GetTempPath(), "PortableGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
    private static readonly string Old = Path.Combine(Path.GetTempPath(), "OldGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
    private static readonly GitDetection OnPath = new(GitAvailability.Available, WindowsGit, new GitVersion(2, 47, 1), null);

    [Fact]
    public void With_no_choice_the_row_shows_the_git_in_use_and_where_it_came_from()
    {
        var row = Row(null, OnPath);

        Assert.Equal("Git program", row.Label);
        Assert.Equal(WindowsGit, row.PathText);
        Assert.Equal("Found on the PATH.", row.NoteText);
        Assert.False(row.ShowsUsePath);
        Assert.False(row.ShowsStatus);
        Assert.False(row.IsChanged);
        Assert.Equal(OperatingSystem.IsWindows() ? "Found in Git for Windows' install folder." : "Found in a usual install folder.", Row(null, OnPath with { Source = GitSource.InstallFolder }).NoteText);
        Assert.Equal("From VISUALCOMMIT_GIT_STAND_IN, a test switch.", Row(null, OnPath with { Source = GitSource.StandIn }).NoteText);
    }

    [Fact]
    public void With_no_git_the_row_says_none_found()
    {
        var row = Row(null, new GitDetection(GitAvailability.NotFound, null, null, null));

        Assert.Equal("None found", row.PathText);
        Assert.Equal("No Git was found on the PATH or in the usual install folders.", row.NoteText);
    }

    [Fact]
    public void Before_a_search_has_ended_or_after_one_failed_the_row_says_none_found()
    {
        // The window's Git is null then: no search has ended, or the last one threw.
        var row = Row(null, null);

        Assert.Equal("None found", row.PathText);
        Assert.Equal("No Git was found on the PATH or in the usual install folders.", row.NoteText);
        Assert.Equal("Chosen here. VisualCommit looks for Git again when you save.", Row(Chosen, null).NoteText);
    }

    [Fact]
    public void A_gone_choice_dropped_says_what_save_does_next()
    {
        // No other git was found: the note for no git in use, then when the change applies.
        var gone = new GitDetection(GitAvailability.NotFound, null, null, null) { Source = GitSource.Setting, ChosenMissing = Chosen };
        var none = Row(Chosen, gone);
        none.UsePathCommand.Execute(null);

        Assert.Equal("None found", none.PathText);
        Assert.Equal("No Git was found on the PATH or in the usual install folders. VisualCommit looks for Git again when you save.", none.NoteText);

        // Another git was found but is not in use (no fallback): only what Save does next.
        var found = Row(Chosen, gone with { PathCandidate = WindowsGit });
        found.UsePathCommand.Execute(null);

        Assert.Equal("None found", found.PathText);
        Assert.Equal("VisualCommit looks for Git again when you save.", found.NoteText);
    }

    [Fact]
    public async Task Browse_takes_a_git_that_works_as_the_choice_used_from_the_next_start_while_git_works()
    {
        var folders = new FakeFolderPicker().AnswerFile(Chosen);
        var row = Row(null, OnPath, folders, Probing(Chosen));

        await row.BrowseCommand.ExecuteAsync(null);

        Assert.Equal([OperatingSystem.IsWindows() ? "Choose git.exe" : "Choose the git program"], folders.FileTitles);
        Assert.Equal(Chosen, row.Choice);
        Assert.True(row.IsChanged);
        Assert.Equal(Chosen, row.PathText);
        Assert.Equal("Chosen here. VisualCommit uses it from its next start.", row.NoteText);
        Assert.True(row.ShowsUsePath);
        Assert.Equal("Use the Git on the PATH", row.UsePathText);
        Assert.False(row.ShowsStatus);
    }

    [Fact]
    public async Task While_git_does_not_work_a_choice_is_looked_for_at_save()
    {
        var row = Row(null, new GitDetection(GitAvailability.NotFound, null, null, null), new FakeFolderPicker().AnswerFile(Chosen), Probing(Chosen));

        await row.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("Chosen here. VisualCommit looks for Git again when you save.", row.NoteText);
    }

    [Fact]
    public void A_saved_choice_that_differs_from_the_git_in_use_says_when_it_applies_and_one_that_is_in_use_does_not()
    {
        var saved = Row(Chosen, OnPath);
        var inUse = Row(Chosen, OnPath with { ExecutablePath = Chosen, Source = GitSource.Setting });

        Assert.Equal("Chosen here. VisualCommit uses it from its next start.", saved.NoteText);
        Assert.False(saved.IsChanged);
        Assert.Equal("Chosen here.", inUse.NoteText);
    }

    [Fact]
    public void Use_the_git_on_the_path_drops_the_choice_and_shows_the_git_in_use()
    {
        var row = Row(Chosen, OnPath);

        row.UsePathCommand.Execute(null);

        Assert.Null(row.Choice);
        Assert.True(row.IsChanged);
        Assert.Equal(WindowsGit, row.PathText);
        Assert.Equal("Found on the PATH.", row.NoteText);
        Assert.False(row.ShowsUsePath);

        // Dropping the chosen git in use: the row still shows it, and says what the next start uses.
        var chosenInUse = Row(Chosen, OnPath with { ExecutablePath = Chosen, Source = GitSource.Setting });
        chosenInUse.UsePathCommand.Execute(null);
        Assert.Equal(Chosen, chosenInUse.PathText);
        Assert.Equal("Chosen here. VisualCommit uses the Git on the PATH from its next start.", chosenInUse.NoteText);
    }

    [Fact]
    public void A_dropped_choice_that_is_the_git_in_use_says_what_the_next_start_uses_also_on_reopening()
    {
        var chosenInUse = OnPath with { ExecutablePath = Chosen, Source = GitSource.Setting };

        // Saved without a choice, the session still on the chosen git: the screen reopened.
        var reopened = Row(null, chosenInUse);
        Assert.Equal(Chosen, reopened.PathText);
        Assert.Equal("Chosen here. VisualCommit uses the Git on the PATH from its next start.", reopened.NoteText);
        Assert.False(reopened.ShowsUsePath);

        // A chosen git in use that does not work: Save looks for git again at once.
        var tooOld = Row(Chosen, chosenInUse with { Availability = GitAvailability.TooOld, Version = new GitVersion(2, 29, 0) });
        tooOld.UsePathCommand.Execute(null);
        Assert.Equal("Chosen here. VisualCommit looks for Git again when you save.", tooOld.NoteText);
    }

    [Fact]
    public void A_gone_choice_says_so_and_offers_the_git_it_found()
    {
        var gone = new GitDetection(GitAvailability.NotFound, null, null, null) { Source = GitSource.Setting, ChosenMissing = Chosen, PathCandidate = WindowsGit };
        var row = Row(Chosen, gone);
        var none = Row(Chosen, gone with { PathCandidate = null });

        Assert.Equal(Chosen, row.PathText);
        Assert.Equal("Chosen here. There is no file at this path.", row.NoteText);
        Assert.Equal("Use the Git it found", row.UsePathText);
        Assert.Equal("Use the Git on the PATH", none.UsePathText);
        Assert.Equal("Chosen here. VisualCommit runs Git only from a full path to the program itself.", Row("git.exe", gone with { ChosenMissing = "git.exe" }).NoteText);
    }

    [Fact]
    public void A_choice_that_gives_way_to_a_git_of_the_install_folders_offers_the_git_it_found()
    {
        Assert.Equal("Use the Git it found", Row(Chosen, OnPath with { Source = GitSource.InstallFolder }).UsePathText);
    }

    [Fact]
    public async Task Browse_refuses_a_too_old_git_and_changes_nothing()
    {
        var row = Row(null, OnPath, new FakeFolderPicker().AnswerFile(Old), (_, _) => Task.FromResult(new GitDetection(GitAvailability.TooOld, Old, new GitVersion(2, 29, 0), null)));

        await row.BrowseCommand.ExecuteAsync(null);

        Assert.Equal($"Git 2.29.0 at this path is too old: VisualCommit needs {GitVersion.Minimum} or newer.", row.StatusText);
        Assert.True(row.IsStatusRefusal);
        Assert.Null(row.Choice);
        Assert.False(row.IsChanged);
        Assert.Equal(WindowsGit, row.PathText);
        Assert.Equal("Found on the PATH.", row.NoteText);
    }

    [Fact]
    public async Task Browse_says_checking_while_the_probe_runs_and_a_cancelled_dialog_changes_nothing()
    {
        var probe = new TaskCompletionSource<GitDetection>();
        var folders = new FakeFolderPicker().AnswerFile(Chosen);
        var row = Row(null, OnPath, folders, (_, _) => probe.Task);

        var browsing = row.BrowseCommand.ExecuteAsync(null);
        Assert.Equal("Checking…", row.StatusText);
        Assert.False(row.IsStatusRefusal);
        probe.SetResult(new GitDetection(GitAvailability.Available, Chosen, new GitVersion(2, 47, 1), null));
        await browsing;
        Assert.Null(row.StatusText);

        var cancelled = Row(null, OnPath, new FakeFolderPicker());
        await cancelled.BrowseCommand.ExecuteAsync(null);
        Assert.Null(cancelled.Choice);
        Assert.False(cancelled.ShowsStatus);
    }

    [Fact]
    public async Task Browse_stops_a_probe_that_does_not_answer_in_time_and_says_so()
    {
        var probing = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var row = new GitProgramViewModel(
            null,
            OnPath,
            async (_, token) =>
            {
                probing.TrySetResult(token);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("The probe ended without its token.");
            },
            new FakeFolderPicker().AnswerFile(Chosen),
            CancellationToken.None,
            TimeSpan.FromMilliseconds(200));

        var browsing = row.BrowseCommand.ExecuteAsync(null);
        var token = await probing.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(token.CanBeCanceled, "Browse's probe was given a token nothing can cancel.");
        await browsing.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal("This file did not report its version within 10 seconds, so VisualCommit stopped it.", row.StatusText);
        Assert.True(row.IsStatusRefusal);
        Assert.True(token.IsCancellationRequested);
        Assert.Null(row.Choice);
        Assert.False(row.IsChanged);
        Assert.Equal("Found on the PATH.", row.NoteText);
    }

    private static GitProgramViewModel Row(string? saved, GitDetection? inUse, FakeFolderPicker? folders = null, Func<string, CancellationToken, Task<GitDetection>>? probe = null) =>
        new(saved, inUse, probe, folders ?? new FakeFolderPicker(), CancellationToken.None);

    private static Func<string, CancellationToken, Task<GitDetection>> Probing(string path) =>
        (_, _) => Task.FromResult(new GitDetection(GitAvailability.Available, path, new GitVersion(2, 47, 1), null) { Source = GitSource.Setting });
}
