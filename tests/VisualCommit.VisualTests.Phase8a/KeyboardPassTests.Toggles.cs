using System.Globalization;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using U = VisualCommit.VisualTests.Phase8a.Phase8aExpectations.U;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The toggles of the lists by the keys (docs/phase-8a-design.md 6.3.9, 6.4; 1.17's list for the
/// review before the gate): Enter or Space on a section header or a folder of the left panel, or on
/// a tree folder of the stage lists, the commit's details or a comparison, opens or closes it and
/// leaves the focus on it, with the keyboard's frame; the row is kept, so its button never loses its
/// data, and a screen reader never reads it by its content's type (9.3).
/// </summary>
public sealed partial class KeyboardPassTests
{
    private const string FeatureFolderKey = "local:feature";

    /// <summary>
    /// "Local branches", reached by Shift+F6 from the graph: Enter closes it and Space opens it
    /// again, and each time its header keeps the focus, with the frame, as the same button with the
    /// same row, whose data never changes under it.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_and_Space_on_the_left_panels_first_section_close_and_open_it_and_keep_the_focus_with_the_frame()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(app.GraphRowPoint(0));
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await FocusLeftSectionAsync(app);
        var header = (Button)app.FocusedElement!;
        var section = (RefSectionRow)header.DataContext!;
        using var contexts = DataContextRecorder.On(header);

        foreach (var (key, open) in new[] { (Key.Enter, false), (Key.Space, true) })
        {
            app.PressKey(key);
            await app.WaitForIdleAsync();
            app.Settle();

            var shown = (RefSectionRow)app.Session.Shell.ActiveTab.LeftPanel.Rows[0];
            Assert.True(shown.IsOpen == open, $"{key} left Local branches {(shown.IsOpen ? "open" : "closed")}.");
            Assert.True(FocusFact(app) == LeftSectionFact, $"After {key} on Local branches the focus is on {FocusFact(app)}.");
            Assert.Same(header, app.FocusedElement);
            Assert.Same(section, shown);
            Assert.Same(section, header.DataContext);
        }

        Assert.True(contexts.Changes.Count == 0, $"The header's data changed under it: {contexts}.");
    }

    /// <summary>
    /// Local branches › feature, reached by Tab from its section, the window's peer made: Enter opens
    /// it and its button keeps the focus, with the frame, its row and the name "feature" for a
    /// screen reader, alpha's row now below it; a click on it closes it again, the focus staying on
    /// it without the frame. The button's data never changes under it, so its peer is never named by
    /// its content's type ("Avalonia.Controls.DockPanel").
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_and_a_click_on_a_folder_of_the_left_panel_open_and_close_it_and_its_button_keeps_the_focus_its_row_and_its_name()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        _ = app.WindowPeer();
        app.Click(app.GraphRowPoint(0));
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await FocusLeftSectionAsync(app);
        await app.TabToAsync(focused => focused is Button { DataContext: RefItemRow { Key: FeatureFolderKey } } button && AutomationProperties.GetAutomationId(button) == U.RefItemId, "Local branches › feature");
        var folder = (Button)app.FocusedElement!;
        var row = (RefItemRow)folder.DataContext!;
        Assert.False(row.IsOpen, "Local branches › feature starts open.");
        using var contexts = DataContextRecorder.On(folder);

        app.PressKey(Key.Enter);
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.True(app.RefRowModel(U.LocalBranches, U.Feature) is RefItemRow { IsOpen: true }, "Enter did not open Local branches › feature.");
        Assert.True(ReferenceEquals(app.FocusedElement, folder), $"After Enter on Local branches › feature the focus is on {app.DescribeFocus()}.");
        Assert.True(app.FocusVisible, "Local branches › feature shows no keyboard frame after Enter.");
        Assert.Same(row, folder.DataContext);
        Assert.Same(row, app.RefRowModel(U.LocalBranches, U.Feature));
        Assert.Equal(U.Feature, ControlAutomationPeer.CreatePeerForElement(folder).GetName());
        _ = await app.RefRowAsync(U.LocalBranches, U.Feature, U.Alpha);

        app.Click(folder);
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.True(row is { IsOpen: false }, "The click did not close Local branches › feature.");
        Assert.True(ReferenceEquals(app.FocusedElement, folder), $"After the click on Local branches › feature the focus is on {app.DescribeFocus()}.");
        Assert.False(app.FocusVisible, "Local branches › feature shows the keyboard frame after a click.");
        Assert.Same(row, folder.DataContext);
        Assert.True(contexts.Changes.Count == 0, $"The folder's data changed under it: {contexts}.");
    }

    /// <summary>
    /// The stage panel in tree mode, its first row the folder assets, reached by Tab from Tree: Enter
    /// closes it and its button keeps the focus, with the frame; Tab then goes to the next row, the
    /// folder data.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_on_a_tree_folder_of_the_stage_list_closes_it_and_keeps_the_focus_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        app.Click(app.FindByAutomationId("StageTreeButton"));
        var unstaged = Repository(app).Changes.Unstaged.Rows;
        await app.WaitForAsync(() => unstaged is [{ IsFolder: true, Key: "assets" }, ..], "the unstaged list as a tree");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await TabToFolderAsync(app, "StageTreeFolder", "assets");

        await AssertEnterKeepsTheFolderAsync(app, unstaged, "StageTreeFolder", "assets", count: 1);

        await app.TabToAsync(focused => IsFolderButton(focused, "StageTreeFolder", "data"), "the folder data");
        Assert.True(app.FocusVisible, "The folder data shows no keyboard frame.");
    }

    /// <summary>
    /// A stage list in tree mode whose first folder, many, holds seventy changed files: Enter closes
    /// it and Enter opens it again, and each time its button keeps the focus, with the frame. More
    /// than 64 rows go and come back at once, which the list once took as a reason to make every row
    /// again.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_on_a_tree_folder_of_seventy_files_closes_and_opens_it_and_keeps_the_focus_with_the_frame()
    {
        using var repo = await SeventyInOneFolderAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        app.Click(app.FindByAutomationId("StageTreeButton"));
        var unstaged = Repository(app).Changes.Unstaged.Rows;
        await app.WaitForAsync(() => unstaged is [{ IsFolder: true, Key: "many" }, ..] && unstaged.Count == 72, "the unstaged list as a tree");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await TabToFolderAsync(app, "StageTreeFolder", "many");

        await AssertEnterKeepsTheFolderAsync(app, unstaged, "StageTreeFolder", "many", count: 70);
    }

    /// <summary>
    /// HEAD's commit, Main work, in the details in tree mode: Tab from Tree to the folder src, then
    /// Enter closes it and its button keeps the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_on_a_tree_folder_of_the_commits_details_closes_it_and_keeps_the_focus_with_the_frame()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(await app.RefRowAsync(U.LocalBranches, U.Main));
        app.WaitForCommitDetails();
        var files = Repository(app).Details.Files;
        await app.WaitForAsync(() => files is [{ Key: "src/core.txt" }], "Main work's file");
        await app.WaitForIdleAsync();
        var tree = app.FindByAutomationId("TreeFilesButton");
        await app.WaitForAsync(() => tree.IsEffectivelyVisible && tree.Bounds.Width > 0, "the details' Tree button");
        app.Click(tree);
        await app.WaitForAsync(() => files is [{ IsFolder: true, Key: "src" }, ..], $"Main work's files as a tree (they are {string.Join(", ", files.Select(row => row.Key))})");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();

        await TabToFolderAsync(app, "FileTreeFolder", "src");
        await AssertEnterKeepsTheFolderAsync(app, files, "FileTreeFolder", "src", count: 1);
    }

    /// <summary>
    /// The comparison of feature/conflict with main in tree mode: Tab from Tree to the folder src,
    /// then Enter closes it and its button keeps the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_on_a_tree_folder_of_a_comparison_closes_it_and_keeps_the_focus_with_the_frame()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(await app.RefRowAsync(U.LocalBranches, U.Feature));
        app.RightClick(await app.RefRowAsync(U.LocalBranches, U.Feature, U.Conflict));
        app.ClickMenuItem(U.CompareConflictWithMain);
        await app.WaitForCompareAsync();
        app.Click(app.FindByAutomationId("CompareTreeButton"));
        var files = Repository(app).Comparison!.Files;
        await app.WaitForAsync(() => files is [{ IsFolder: true, Key: "src" }, ..], "the comparison's files as a tree");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();

        await TabToFolderAsync(app, "FileTreeFolder", "src");
        await AssertEnterKeepsTheFolderAsync(app, files, "FileTreeFolder", "src", count: 1);
    }

    /// <summary>
    /// Tab from the Tree button just clicked, which has the focus, until a folder's button
    /// <paramref name="automationId"/> has it, which must be the folder <paramref name="folder"/>.
    /// </summary>
    private static async Task TabToFolderAsync(ShellDriver app, string automationId, string folder)
    {
        for (var tab = 0; !(app.FocusedElement is Control control && AutomationProperties.GetAutomationId(control) == automationId); tab++)
        {
            Assert.True(tab < 6, $"Six Tabs did not reach a folder of the tree: the focus is on {app.DescribeFocus()}.");
            app.PressKey(Key.Tab);
            await app.WaitForIdleAsync();
        }

        Assert.True(IsFolderButton(app.FocusedElement, automationId, folder), $"Tab reached {app.DescribeFocus()}, not the folder {folder}.");
        Assert.True(app.FocusVisible, $"The folder {folder} shows no keyboard frame.");
    }

    /// <summary>
    /// Enter on the focused folder <paramref name="name"/> of <paramref name="list"/>, whose
    /// <paramref name="count"/> files show below it: the folder closes, its button keeps the focus,
    /// with the frame, and its row, which shows itself closed; nothing else in the list was made again.
    /// </summary>
    private static async Task AssertEnterKeepsTheFolderAsync(ShellDriver app, IReadOnlyList<ChangedFileRow> list, string automationId, string name, int count)
    {
        var folder = (Button)app.FocusedElement!;
        var row = (ChangedFileRow)folder.DataContext!;
        var before = list.Count;
        using var contexts = DataContextRecorder.On(folder);

        app.PressKey(Key.Enter);
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.True(list.Count == before - count, string.Create(CultureInfo.InvariantCulture, $"Enter on the folder {name} left {list.Count} rows of {before}."));
        Assert.True(ReferenceEquals(app.FocusedElement, folder), $"After Enter on the folder {name} the focus is on {app.DescribeFocus()}.");
        Assert.True(app.FocusVisible, $"The folder {name} shows no keyboard frame after Enter.");
        Assert.True(IsFolderButton(folder, automationId, name), $"The focused button is no longer the folder {name}.");
        Assert.Same(row, list[list.ToList().FindIndex(each => each.Key == name)]);
        Assert.False(row.IsOpen, $"The folder {name} shows itself open.");
        Assert.True(contexts.Changes.Count == 0, $"The folder's data changed under it: {contexts}.");

        if (count > 64)
        {
            app.PressKey(Key.Enter);
            await app.WaitForIdleAsync();
            app.Settle();

            Assert.True(list.Count == before, string.Create(CultureInfo.InvariantCulture, $"Enter again on the folder {name} left {list.Count} rows of {before}."));
            Assert.True(ReferenceEquals(app.FocusedElement, folder), $"After Enter again on the folder {name} the focus is on {app.DescribeFocus()}.");
            Assert.True(app.FocusVisible, $"The folder {name} shows no keyboard frame after Enter again.");
            Assert.True(row.IsOpen, $"The folder {name} shows itself closed.");
            Assert.True(contexts.Changes.Count == 0, $"The folder's data changed under it: {contexts}.");
        }
    }

    /// <summary>Whether <paramref name="element"/> is a folder's button <paramref name="automationId"/> of a tree whose folder is <paramref name="name"/>.</summary>
    private static bool IsFolderButton(IInputElement? element, string automationId, string name) =>
        element is Button { DataContext: ChangedFileRow { IsFolder: true, Key: var key } } button && key == name && AutomationProperties.GetAutomationId(button) == automationId;

    /// <summary>A repository whose working tree changes many/f00.txt to many/f69.txt and z.txt.</summary>
    private static async Task<TempRepo> SeventyInOneFolderAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            string[] names = [.. Enumerable.Range(0, 70).Select(number => string.Create(CultureInfo.InvariantCulture, $"many/f{number:D2}.txt")), "z.txt"];
            foreach (var name in names)
            {
                repo.WriteFile(name, name + "\n");
            }

            await repo.CommitAsync("Add seventy-one files");
            foreach (var name in names)
            {
                repo.WriteFile(name, name + "\nchanged\n");
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>Records every change of a control's data from now on, with the name its peer reads then and the rule of the sweep that name breaks.</summary>
    private sealed class DataContextRecorder : IDisposable
    {
        private readonly Control _control;

        private DataContextRecorder(Control control)
        {
            _control = control;
            _control.DataContextChanged += OnChanged;
        }

        public List<(object? Data, string? Name, string? Broken)> Changes { get; } = [];

        public static DataContextRecorder On(Control control) => new(control);

        public void Dispose() => _control.DataContextChanged -= OnChanged;

        public override string ToString() =>
            string.Join("; ", Changes.Select(change => $"{change.Data?.GetType().Name ?? "null"}, named \"{change.Name}\"{(change.Broken is null ? string.Empty : $" ({change.Broken})")}"));

        private void OnChanged(object? sender, EventArgs e)
        {
            var name = ControlAutomationPeer.CreatePeerForElement(_control).GetName();
            Changes.Add((_control.DataContext, name, AutomationSweep.RuleBroken(name, null)));
        }
    }
}
