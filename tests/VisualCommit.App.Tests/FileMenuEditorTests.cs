using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// "Open in editor" in the file menus (docs/phase-6-design.md 12.1, 12.10, D226): every file menu
/// as phase 5 had it while no editor is chosen; with one, the item right after "Open file", enabled
/// whatever its program (no menu looks at the disk), disabled only for a file that is not in the
/// working tree; and the menu context's editor from the settings.
/// </summary>
public class FileMenuEditorTests
{
    private const string MainWork = "88d6b91f6c2b5cb1eee0a1e2a8bbff2d3a8ee8c0";

    private static readonly FileMenuTarget Reader = new(new ChangedFile("src/Reader.cs", FileChangeKind.Modified), IsUntracked: false, ExistsInWorkingTree: true);

    public static TheoryData<string> Builders => new() { "unstaged", "staged", "commit", "diff", "compare" };

    [Theory]
    [MemberData(nameof(Builders))]
    public void Without_an_editor_every_file_menu_is_as_before_and_with_one_open_in_editor_follows_open_file(string builder)
    {
        var (actions, recorder) = RecordingActions.Create();

        var without = RepositoryMenusTests.Lines(Menu(builder, Context(editor: null), Reader, actions));
        var with = RepositoryMenusTests.Lines(Menu(builder, Context(new EditorMenuState("Visual Studio Code")), Reader, actions));

        Assert.DoesNotContain("Open in editor", without);
        var openFile = with.IndexOf("Open file");
        Assert.True(openFile >= 0, string.Join(" | ", with));
        Assert.Equal("Open in editor", with[openFile + 1]);
        Assert.Equal(["Open file", "Open in editor", "Show in folder", "Copy path"], with.Skip(openFile).Take(4));
        with.RemoveAt(openFile + 1);
        Assert.Equal(without, with);

        // Its click opens the working tree's file in the editor.
        var menu = Menu(builder, Context(new EditorMenuState("Visual Studio Code")), Reader, actions);
        menu.Items.Single(item => item.Label == "Open in editor").Invoke();
        Assert.Contains(recorder.Calls, call => call.Method == nameof(IRepositoryActions.OpenInEditorAsync) && Equals(call.Arguments[0], "src/Reader.cs"));
    }

    [Theory]
    [MemberData(nameof(Builders))]
    public void Open_in_editor_is_disabled_only_for_a_file_that_is_not_in_the_working_tree(string builder)
    {
        var (actions, _) = RecordingActions.Create();
        var gone = Reader with { ExistsInWorkingTree = false };

        var lines = RepositoryMenusTests.Lines(Menu(builder, Context(new EditorMenuState("Other")), gone, actions));

        Assert.Contains("Open in editor (disabled: \"The file is not in the working tree\")", lines);
    }

    [Fact]
    public async Task The_menu_context_names_the_chosen_editor_from_the_settings_and_never_looks_at_its_program()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        Assert.Null(app.Repository.MenuContext.Editor);

        // A program that does not exist: the item is still offered and enabled (12.1: a program
        // that is gone is said when the item is clicked, 12.7).
        app.Settings.Update(settings => settings with { Editor = new EditorSetting("vscode", @"Z:\nowhere\Code.exe") });
        Assert.Equal(new EditorMenuState("Visual Studio Code"), app.Repository.MenuContext.Editor);
        var file = new FileMenuTarget(new ChangedFile("src/helpers.py", FileChangeKind.Modified), false, true);
        var menu = RepositoryMenus.ForUnstagedFile(app.Repository.MenuContext, file, app.Operations);
        Assert.True(menu.Items.Single(item => item.Label == "Open in editor").IsEnabled);

        app.Settings.Update(settings => settings with { Editor = new EditorSetting(KnownEditors.CustomId, @"D:\Tools\Ed.exe", "{path}") });
        Assert.Equal(new EditorMenuState("Ed.exe"), app.Repository.MenuContext.Editor);

        // An id the catalogue no longer knows means none chosen, logged once (12.6).
        app.Settings.Update(settings => settings with { Editor = new EditorSetting("an-editor-of-the-future", @"C:\x.exe") });
        Assert.Null(app.Repository.MenuContext.Editor);
        Assert.Null(app.Repository.MenuContext.Editor);
        Assert.Single(app.Log.Messages, message => message.Contains("an-editor-of-the-future", StringComparison.Ordinal));
    }

    private static MenuModel Menu(string builder, RepositoryMenuContext context, FileMenuTarget file, IRepositoryActions actions) => builder switch
    {
        "unstaged" => RepositoryMenus.ForUnstagedFile(context, file, actions),
        "staged" => RepositoryMenus.ForStagedFile(context, file, actions),
        "commit" => RepositoryMenus.ForCommitFile(context, file with { CommitSha = MainWork }, actions),
        "diff" => RepositoryMenus.ForDiff(context, new DiffMenuTarget(file, DiffSide.Unstaged, HasSelectedLines: false, Hunk: null, ActsOnLines: false), actions, new NoLines()),
        _ => RepositoryMenus.ForCompareFile(context, file with { CommitSha = MainWork }, actions),
    };

    private static RepositoryMenuContext Context(EditorMenuState? editor)
    {
        var refs = new RepoRefs(
            new HeadState("main", MainWork),
            [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, MainWork, IsHead: true)],
            [],
            []);
        var status = WorkingTreeStatus.Clean;
        return new RepositoryMenuContext("repo", "C:/repo", refs, status, [], ToolbarViewModel.StatesFor(refs, status), IsOperationRunning: false)
        {
            Editor = editor,
        };
    }

    private sealed class NoLines : IDiffMenuActions
    {
        public Task StageLinesAsync() => Task.CompletedTask;

        public Task UnstageLinesAsync() => Task.CompletedTask;

        public Task DiscardLinesAsync() => Task.CompletedTask;

        public Task RunHunkActionAsync(int hunk, LineAction action) => Task.CompletedTask;
    }
}
