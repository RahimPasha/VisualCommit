using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The file actions (docs/phase-3-design.md 2.30, 2.32, 4b's file menus, D62, D67, D70, D103;
/// visual checks 4 and 26) against real scenario repositories: stage, unstage and discard as
/// phase 2 runs them, ignore a file, open it, show it in the file manager, copy, and open a
/// diff, with what each leaves in the repository, the status bar, the stage panel, the
/// <see cref="FakeFileLauncher"/> and the <see cref="FakeClipboard"/>.
/// </summary>
public partial class RepositoryOperationsTests
{
    // Stage, unstage, discard: phase 2's writes, through the repository (1.4).

    [Fact]
    public async Task Stage_and_unstage_from_a_menu_are_phase_2s_writes_and_leave_the_status_bar_ready()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var calculator = app.Operations.Status.Unstaged.Single(file => file.Path == "src/Calculator.cs");
        var helpers = app.Operations.Status.Staged.Single(file => file.Path == "src/helpers.py");

        await app.Operations.StageFilesAsync([calculator]);
        await app.Operations.UnstageFilesAsync([helpers]);

        var staged = (await repo.GitAsync("diff", "--cached", "--name-only")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("src/Calculator.cs", staged);

        // A rename unstages both its paths: the new file is untracked again, the old one deleted.
        Assert.DoesNotContain("src/helpers.py", staged);
        Assert.DoesNotContain("src/util.py", staged);
        Assert.Contains(app.Operations.Status.Unstaged, file => file.Path == "src/helpers.py");
        Assert.Contains(app.Operations.Status.Unstaged, file => file.Path == "src/util.py" && file.Kind == FileChangeKind.Deleted);
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);
        Assert.Null(app.Repository.Changes.ErrorText);
        Assert.Empty(app.Dialogs.Shown);
        AssertRanOnce(app, "Stage");
        AssertRanOnce(app, "Unstage");
    }

    [Fact]
    public async Task Mark_as_resolved_stages_a_conflicted_file()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        var conflicted = app.Operations.Status.Unstaged.Single(file => file.Kind == FileChangeKind.Conflicted);

        await app.Operations.StageFilesAsync([conflicted]);

        Assert.Equal(string.Empty, (await repo.GitAsync("ls-files", "--unmerged")).StandardOutput);
        Assert.DoesNotContain(app.Operations.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted);
        Assert.Equal(InProgressKind.Merge, app.Operations.Status.InProgress.Kind);
    }

    [Fact]
    public async Task Discard_from_a_menu_asks_phase_2s_question_and_keeps_its_snapshot_and_restore_bar()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var calculator = app.Operations.Status.Unstaged.Single(file => file.Path == "src/Calculator.cs");
        var full = Path.Combine(repo.Path, "src", "Calculator.cs");
        var before = File.ReadAllBytes(full);
        app.Dialogs.Confirm(yes: false).Confirm();

        await app.Operations.DiscardFilesAsync([calculator]);

        Assert.Equal(before, File.ReadAllBytes(full));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Null(app.Repository.Changes.RestoreText);

        await app.Operations.DiscardFilesAsync([calculator]);

        Assert.All(app.Dialogs.Confirmations, question => Assert.Equal(
            ("Discard changes?", "Discard the changes to Calculator.cs?", "A snapshot is saved first, so the changes can be restored.", "Discard", true),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive)));
        Assert.Equal(string.Empty, (await repo.GitAsync("diff", "--name-only", "--", "src/Calculator.cs")).StandardOutput);
        var snapshot = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/discard-", snapshot.Name);
        Assert.Equal("Discarded changes to Calculator.cs.", app.Repository.Changes.RestoreText);
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_working_changes_rows_stage_all_unstage_all_and_discard_all_are_phase_2s()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm();

        await app.Operations.StageAllAsync();

        Assert.Empty(app.Operations.Status.Unstaged);
        Assert.Equal(string.Empty, (await repo.GitAsync("diff", "--name-only")).StandardOutput);
        Assert.Equal(9, app.Operations.Status.Staged.Count);

        await app.Operations.UnstageAllAsync();

        Assert.Empty(app.Operations.Status.Staged);
        Assert.Equal(string.Empty, (await repo.GitAsync("diff", "--cached", "--name-only")).StandardOutput);

        await app.Operations.DiscardAllAsync();

        var question = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(("Discard changes?", "Discard", "A snapshot is saved first, so the changes can be restored."), (question.Title, question.ConfirmText, question.Detail));
        Assert.Matches("^Discard the changes to [0-9]+ files\\?$", question.Question);
        Assert.False(app.Repository.HasWorkingChanges);
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
        Assert.StartsWith("Discarded changes to ", app.Repository.Changes.RestoreText, StringComparison.Ordinal);
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_stage_that_git_refuses_shows_gits_message_in_the_stage_panel_not_in_a_dialog()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var calculator = app.Operations.Status.Unstaged.Single(file => file.Path == "src/Calculator.cs");
        File.WriteAllText(Path.Combine(repo.Path, ".git", "index.lock"), string.Empty);

        await app.Operations.StageFilesAsync([calculator]);

        Assert.Contains("index.lock", app.Repository.Changes.ErrorText, StringComparison.Ordinal);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);
        File.Delete(Path.Combine(repo.Path, ".git", "index.lock"));

        await app.Operations.StageFilesAsync([calculator]);

        Assert.Null(app.Repository.Changes.ErrorText);
    }

    // 2.30: ignore.

    [Fact]
    public async Task Ignore_by_name_adds_the_files_line_after_a_missing_line_feed()
    {
        using var repo = await IgnoreScenarioAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.IgnoreFileAsync("logs/run.log");

        Assert.Equal("*.tmp\n/logs/run.log\n", File.ReadAllText(Path.Combine(repo.Path, ".gitignore")));
        Assert.Equal("Added /logs/run.log to .gitignore", app.StatusText);
        Assert.Equal(OperationStatusTone.Secondary, app.StatusTone);
        Assert.DoesNotContain(app.Operations.Status.Unstaged, file => file.Path == "logs/run.log");
        Assert.Equal([" M src/core.txt", "?? .gitignore", "?? TODO", "?? drafts/[wip].txt", "?? trace.log"], await PorcelainAllAsync(repo));
        Assert.Single(app.Log.Messages, message => message.StartsWith($"{app.Repository.Name}: Ignore run.log: Succeeded after ", StringComparison.Ordinal));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Ignore_by_extension_from_the_diff_view_closes_the_diff_once_its_file_leaves_the_list()
    {
        using var repo = await IgnoreScenarioAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Operations.OpenDiff(app.Operations.Status.Unstaged.Single(file => file.Path == "trace.log"), staged: false);
        Assert.Equal("trace.log", app.Repository.Diff?.Target.Path);

        await app.Operations.IgnoreExtensionAsync("trace.log");

        Assert.Equal("*.tmp\n*.log\n", File.ReadAllText(Path.Combine(repo.Path, ".gitignore")));
        Assert.Null(app.Repository.Diff);
        Assert.Equal("Added *.log to .gitignore", app.StatusText);
        Assert.Equal([" M src/core.txt", "?? .gitignore", "?? TODO", "?? drafts/[wip].txt"], await PorcelainAllAsync(repo));
        AssertRanOnce(app, "Ignore all *.log files");
    }

    [Fact]
    public async Task Ignore_escapes_the_patterns_characters_so_that_the_line_means_that_file_only()
    {
        using var repo = await IgnoreScenarioAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.IgnoreFileAsync("drafts/[wip].txt");

        Assert.Equal("*.tmp\n/drafts/\\[wip].txt\n", File.ReadAllText(Path.Combine(repo.Path, ".gitignore")));
        Assert.Equal("Added /drafts/\\[wip].txt to .gitignore", app.StatusText);
        // check-ignore refuses GIT_LITERAL_PATHSPECS, which TempRepo sets.
        Assert.StartsWith(".gitignore:2:", (await repo.GitAsync("--no-literal-pathspecs", "check-ignore", "-v", "drafts/[wip].txt")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("--no-literal-pathspecs", "check-ignore", "drafts/w.txt")).ExitCode);
    }

    [Fact]
    public async Task Ignore_does_nothing_for_a_tracked_file_or_a_name_without_an_extension()
    {
        using var repo = await IgnoreScenarioAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.IgnoreFileAsync("src/core.txt");
        await app.Operations.IgnoreExtensionAsync("src/core.txt");
        await app.Operations.IgnoreExtensionAsync("TODO");

        Assert.Equal("*.tmp", File.ReadAllText(Path.Combine(repo.Path, ".gitignore")));
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);
        Assert.DoesNotContain(app.Log.Messages, message => message.Contains(" after ", StringComparison.Ordinal) && message.Contains(": Ignore ", StringComparison.Ordinal));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Ignore_that_cannot_write_gitignore_opens_the_error_dialog()
    {
        using var repo = await IgnoreScenarioAsync(gitignore: false);

        // A folder where .gitignore should be: no file can be written there, as root too.
        Directory.CreateDirectory(Path.Combine(repo.Path, ".gitignore"));
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.IgnoreFileAsync("logs/run.log");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Ignoring the file failed", "The file .gitignore could not be written."), (error.Title, error.Summary));
        Assert.False(string.IsNullOrWhiteSpace(error.Output));
        Assert.Equal("Ignore run.log failed", app.StatusText);
        Assert.Equal(OperationStatusTone.Danger, app.StatusTone);
        Assert.Contains(app.Operations.Status.Unstaged, file => file.Path == "logs/run.log");
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Ignore_of_a_name_with_a_line_break_says_that_it_cannot_be_written()
    {
        // Linux and macOS allow a line break in a name; no .gitignore line can hold it.
        using var repo = await IgnoreScenarioAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        await app.Operations.IgnoreFileAsync("logs/a\nb.log");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Ignoring the file failed", "This file name cannot be written to .gitignore."), (error.Title, error.Summary));
        Assert.Equal(("Ignore a\nb.log failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal("*.tmp", File.ReadAllText(Path.Combine(repo.Path, ".gitignore")));
        app.AssertScriptFollowed();
    }

    // 2.32: open, show in folder, copy.

    [Theory]
    [InlineData("setup.cmd", null, true)]
    [InlineData("SETUP.EXE", null, true)]
    [InlineData("tools/install.ps1", null, true)]
    [InlineData("docs/README.txt.lnk", null, true)]
    [InlineData("site.url", null, true)]
    [InlineData("run.command", null, true)]
    [InlineData("build.sh", null, true)]
    [InlineData("app.desktop", null, true)]
    [InlineData("notes.txt", null, false)]
    [InlineData("helpers.py", null, false)]
    [InlineData("Makefile", null, false)]
    [InlineData("build", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, true)]
    [InlineData("data.bin", UnixFileMode.UserRead | UnixFileMode.OtherExecute, true)]
    [InlineData("notes.txt", UnixFileMode.UserRead | UnixFileMode.UserWrite, false)]
    public void Programs_scripts_and_shortcuts_are_files_that_can_run_programs(string name, UnixFileMode? mode, bool runs) =>
        Assert.Equal(runs, RepositoryOperations.CanRunPrograms(name, mode));

    [Fact]
    public async Task Open_file_on_a_file_that_can_run_programs_asks_first()
    {
        using var repo = await Scenarios.ChangesAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "setup.cmd"), "echo hello\r\n", TestContext.Current.CancellationToken);
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Confirm(yes: false).Confirm();
        var setup = Path.Combine(app.Repository.Path, "setup.cmd");
        var helpers = Path.Combine(app.Repository.Path, "src", "helpers.py");

        await app.Operations.OpenFileAsync("setup.cmd");
        Assert.Empty(app.Launcher.Opened);
        await app.Operations.OpenFileAsync("setup.cmd");
        await app.Operations.OpenFileAsync("src/helpers.py");

        Assert.Equal([setup, helpers], app.Launcher.Opened);
        Assert.Equal(2, app.Dialogs.Confirmations.Count);
        var question = app.Dialogs.Confirmations[0];
        Assert.Equal(
            ("Open setup.cmd?", "This file can run programs. Open it anyway?", string.Empty, "Open", false),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Open_file_and_show_in_folder_ask_the_launcher_for_the_working_trees_file()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var helpers = Path.Combine(app.Repository.Path, "src", "helpers.py");
        var guide = Path.Combine(app.Repository.Path, "docs", "guide.md");
        var gone = Path.Combine(app.Repository.Path, "docs", "old-notes.txt");

        await app.Operations.OpenFileAsync("src/helpers.py");
        await app.Operations.OpenFileAsync("docs/old-notes.txt");
        await app.Operations.ShowInFolderAsync("docs/guide.md");
        await app.Operations.ShowInFolderAsync("docs/old-notes.txt");
        await app.Operations.ShowInFolderAsync(null);

        // A deleted file is not opened; the launcher shows a missing file's folder itself.
        Assert.Equal([helpers], app.Launcher.Opened);
        Assert.Equal([guide, gone, app.Repository.Path], app.Launcher.Revealed);
        Assert.Null(app.Clipboard.Text);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);
        Assert.Null(app.LastResult);
    }

    [Fact]
    public async Task Copy_puts_the_sha_the_shas_in_row_order_the_path_and_the_short_names_on_the_clipboard()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var alphaTwo = await app.ShaAsync("feature/alpha");
        var alphaOne = await app.ShaAsync("feature/alpha^");
        var main = await app.ShaAsync("main");

        await app.Operations.CopyShaAsync(alphaTwo);
        await app.Operations.CopyShasAsync([alphaOne, main, alphaTwo]);
        await app.Operations.CopyPathAsync("src/core.txt");
        await app.Operations.CopyRefNameAsync("refs/heads/feature/alpha");
        await app.Operations.CopyRefNameAsync("refs/remotes/origin/feature/alpha");
        await app.Operations.CopyRefNameAsync("refs/tags/v1.0");

        // A backslash is part of a name where git gives one (Linux and macOS): kept as it is.
        await app.Operations.CopyPathAsync("notes\\2024.txt");

        Assert.Equal(
            [alphaTwo, $"{main}\n{alphaTwo}\n{alphaOne}", "src/core.txt", "feature/alpha", "origin/feature/alpha", "v1.0", "notes\\2024.txt"],
            app.Clipboard.Texts);
        Assert.Empty(app.Launcher.Requests);
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task Copy_URL_copies_the_fetch_url_without_its_user_information()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("remote", "add", "mirror", "https://alice:s3cret@example.com/team/ops.git");
        using var app = await OperationsHarness.OpenAsync(repo);
        var origin = (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();

        await app.Operations.CopyRemoteUrlAsync("origin");
        await app.Operations.CopyRemoteUrlAsync("mirror");
        await app.Operations.CopyRemoteUrlAsync("nowhere");

        Assert.Equal([origin, "https://example.com/team/ops.git"], app.Clipboard.Texts);
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task OpenDiff_opens_a_working_tree_files_diff_as_a_click_on_its_row_does()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var settings = app.Operations.Status.Staged.Single(file => file.Path == "config/settings.json");

        app.Operations.OpenDiff(settings, staged: true);

        Assert.Equal(("config/settings.json", DiffSide.Staged), (app.Repository.Diff?.Target.Path, app.Repository.Diff?.Target.Side));
        Assert.Equal("config/settings.json", app.Repository.Changes.Staged.SelectedPath);
    }

    [Fact]
    public async Task After_the_tab_closed_no_file_action_does_anything()
    {
        using var repo = await IgnoreScenarioAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var core = app.Operations.Status.Unstaged.Single(file => file.Path == "src/core.txt");
        var porcelain = await PorcelainAllAsync(repo);
        app.Repository.Dispose();
        app.Dialogs.Confirm();

        await app.Operations.StageFilesAsync([core]);
        await app.Operations.UnstageFilesAsync([core]);
        await app.Operations.DiscardFilesAsync([core]);
        await app.Operations.StageAllAsync();
        await app.Operations.UnstageAllAsync();
        await app.Operations.DiscardAllAsync();
        await app.Operations.IgnoreFileAsync("logs/run.log");
        await app.Operations.IgnoreExtensionAsync("trace.log");
        await app.Operations.OpenFileAsync("src/core.txt");
        await app.Operations.ShowInFolderAsync(null);
        await app.Operations.CopyShaAsync(new string('a', 40));
        await app.Operations.CopyShasAsync([new string('a', 40)]);
        await app.Operations.CopyPathAsync("src/core.txt");
        await app.Operations.CopyRefNameAsync("refs/heads/main");
        await app.Operations.CopyRemoteUrlAsync("origin");
        app.Operations.OpenDiff(core, staged: false);

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(1, app.Dialogs.Unused);
        Assert.Empty(app.Launcher.Requests);
        Assert.Empty(app.Clipboard.Texts);
        Assert.Null(app.Repository.Diff);
        Assert.Equal(porcelain, await PorcelainAllAsync(repo));
        Assert.Equal("*.tmp", File.ReadAllText(Path.Combine(repo.Path, ".gitignore")));
    }

    /// <summary>
    /// The ops scenario with check 26's files, written before the app starts: <c>.gitignore</c>
    /// "*.tmp" without a final line feed (or none), <c>logs/run.log</c>, <c>trace.log</c>,
    /// <c>TODO</c>, <c>drafts/[wip].txt</c>, and <c>src/core.txt</c> changed.
    /// </summary>
    private static async Task<TempRepo> IgnoreScenarioAsync(bool gitignore = true)
    {
        var repo = await Scenarios.OperationsAsync();
        if (gitignore)
        {
            repo.WriteFile(".gitignore", "*.tmp");
        }

        return repo
            .WriteFile("logs/run.log", "run\n")
            .WriteFile("trace.log", "trace\n")
            .WriteFile("TODO", "todo\n")
            .WriteFile("drafts/[wip].txt", "wip\n")
            .WriteFile("src/core.txt", "core\nmain work\nlocal\n");
    }

    private static async Task<IReadOnlyList<string>> PorcelainAllAsync(TempRepo repo) =>
        (await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
