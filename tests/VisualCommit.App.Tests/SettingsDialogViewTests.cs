using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The settings screen as drawn (docs/phase-6-design.md 7.1, 7.6, 7.7, 7.11): the card's place at
/// each window size, the first focus and the section list's keys, Enter and Esc, the Pick dialog
/// stacked over it, and the Keyboard shortcuts rows' layout, in the headless app with the fixed
/// catalogues (12.9). <see cref="TestPictures"/> saves the pictures when asked.
/// </summary>
public class SettingsDialogViewTests
{
    private const string UseConfigOnly = "[user]\n\tuseConfigOnly = true\n";

    /// <summary>6.11's groups in Windows' and the headless platform's words, written out by hand (the report's part S table).</summary>
    public static IReadOnlyList<ShortcutGroup> ShortcutTable { get; } =
    [
        new("App", [new("Command palette", "Ctrl+Shift+P"), new("Open settings…", "Ctrl+,"), new("Show or hide the activity log", "Ctrl+J"), new("Refresh", "F5, Ctrl+R")], null),
        new("Tabs", [new("New tab", "Ctrl+T"), new("Close tab", "Ctrl+W"), new("Next tab", "Ctrl+Tab, Ctrl+PageDown"), new("Previous tab", "Ctrl+Shift+Tab, Ctrl+PageUp"), new("Go to tab 1 to 8", "Ctrl+1 to Ctrl+8"), new("Go to the last tab", "Ctrl+9"), new("Go to the next dashboard", "Ctrl+Shift+D")], null),
        new("Repositories", [new("Open repository…", "Ctrl+O"), new("Clone repository…", "Ctrl+Shift+C"), new("Init repository…", "Ctrl+Shift+I")], null),
        new(
            "Repository",
            [
                new("Fetch all remotes", "Ctrl+Shift+F"), new("Pull", "Ctrl+Shift+L"), new("Push", "Ctrl+Shift+K"), new("Create branch…", "Ctrl+B"),
                new("Stash changes…", "Ctrl+Shift+S"), new("Pop the newest stash", "Ctrl+Shift+O"), new("Undo", "Ctrl+Z"), new("Redo", "Ctrl+Y, Ctrl+Shift+Z"),
                new("Search commits", "Ctrl+F"), new("Next match", "F3, Ctrl+G"), new("Previous match", "Shift+F3, Ctrl+Shift+G"), new("Show reflog", "Ctrl+Shift+R"),
            ],
            "On a dashboard, Fetch and Pull fetch and pull every repository of its workspace."),
        new("Changes", [new("Stage all", "Ctrl+Shift+A"), new("Unstage all", "Ctrl+Shift+X"), new("Go to the commit message", "Ctrl+Shift+M"), new("Commit, in the message boxes", "Ctrl+Enter")], null),
        new("Files", [new("File history of the open diff's file", "Ctrl+Shift+H"), new("Blame of the open diff's file", "Ctrl+Shift+B"), new("Close the diff, a page or the search bar", "Esc")], null),
        new(
            "Moving around",
            [
                new("Filter branches and tags", "Ctrl+Shift+E"), new("Next region", "F6"), new("Previous region", "Shift+F6"), new("Context menu", "Shift+F10, Menu"),
                new("Move in the graph", "Up, Down, PageUp, PageDown, Home, End"),
                new("Extend the graph's selection", "Shift+Up, Shift+Down, Shift+PageUp, Shift+PageDown, Shift+Home, Shift+End"),
            ],
            null),
        new("Interactive rebase", [new("Move the selected row", "Alt+Up, Alt+Down"), new("Pick, reword, squash, fixup, drop", "P, R, S, F, D")], null),
        new("Conflict resolver", [new("Previous or next conflict", "Alt+Up, Alt+Down"), new("Previous or next file", "Alt+Left, Alt+Right"), new("Leave the result", "Ctrl+Tab")], null),
        new("Dashboard", [new("Open the repository", "Enter"), new("Remove from workspace", "Delete"), new("Move the repository", "Alt+Up, Alt+Down")], null),
        new("Dialogs and menus", [new("Confirm", "Enter"), new("Confirm from a box of several lines", "Ctrl+Enter"), new("Cancel or close", "Esc")], null),
    ];

    [AvaloniaTheory]
    [InlineData(1100, 700, 120, 40, 860, 620)]
    [InlineData(1920, 1080, 530, 200, 860, 680)]
    [InlineData(1000, 560, 70, 40, 860, 480)]
    public async Task The_card_lies_where_7_1_puts_it(int width, int height, double x, double y, double cardWidth, double cardHeight)
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        ShellDriver.SeedGlobalGitConfig(data.Path, UseConfigOnly);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: width, height: height);
        await app.WaitForRepositoryAsync();
        await app.WaitForIdleAsync();

        await app.OpenSettingsAsync();
        var card = Card(app);
        Assert.Equal(new Rect(x, y, cardWidth, cardHeight), app.BoundsOf(card));

        foreach (var section in new[] { SettingsSection.General, SettingsSection.Git, SettingsSection.Tools, SettingsSection.KeyboardShortcuts })
        {
            app.ChooseSection(section);
            var cardArea = app.BoundsOf(card);

            // Every control of the screen lies inside the card, also at the smallest window, where
            // General and Git scroll a little (7.1): what scrolls is clipped by the section's viewer.
            var section_ = app.BoundsOf(app.FindByAutomationId(app.TopDialogView!, "SettingsSection"));
            foreach (var control in card.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0 && AutomationProperties.GetAutomationId(control) is { Length: > 0 }))
            {
                var bounds = app.BoundsOf(control);
                var clipped = control.GetVisualAncestors().OfType<ScrollViewer>().Any() ? bounds.Intersect(section_) : bounds;
                if (clipped.Width <= 0 || clipped.Height <= 0)
                {
                    continue;
                }

                Assert.True(cardArea.Inflate(0.5).Contains(clipped), $"{AutomationProperties.GetAutomationId(control)} at {bounds} lies outside the card {cardArea} in {section} at {width}×{height}.");
            }

            TestPictures.Save(app, $"settings-{section}-{width}x{height}");
        }

        app.CancelSettings();
    }

    [AvaloniaFact]
    public async Task Pictures_of_the_settings_screen_in_both_themes()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        ShellDriver.SeedGlobalGitConfig(data.Path, UseConfigOnly);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        await app.WaitForIdleAsync();

        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            Application.Current!.RequestedThemeVariant = theme;
            var suffix = theme == ThemeVariant.Dark ? "dark" : "light";
            var dialog = await app.OpenSettingsAsync();
            Shot("general");
            app.ChooseSection(SettingsSection.Git);
            Shot("git-global");
            app.ChooseScope(SettingsScope.Repository);
            Shot("git-repository");
            app.ChooseScope(SettingsScope.Global);
            app.Click(app.FindByAutomationId(app.TopDialogView!, "MergeToolChooseButton"));
            await app.WaitForDialogAsync<PickDialogViewModel>();
            Shot("pick-merge-tool");
            app.PressKey(Key.Escape);
            app.ChooseSection(SettingsSection.Tools);
            Shot("tools");
            app.Click(app.FindByAutomationId(app.TopDialogView!, "EditorChooseButton"));
            await app.WaitForDialogAsync<PickDialogViewModel>();
            Shot("pick-editor");
            await app.PickAsync("Other…");
            app.Folders.AnswerFile(OperatingSystem.IsWindows() ? @"C:\Tools\Missing\Ed.exe" : "/opt/tools/missing/ed");
            app.Click(app.FindByAutomationId(app.TopDialogView!, "EditorBrowseButton"));
            await app.WaitForAsync(() => dialog.Tools.ProgramText.Length > 0, "the program box");
            await app.SaveSettingsAsync();
            Shot("tools-other-error");
            app.ChooseSection(SettingsSection.KeyboardShortcuts);
            Shot("shortcuts");
            app.CancelSettings();
            Assert.True(dialog.IsClosed);
        }

        void Shot(string name)
        {
            app.MoveMouse(new Point(760, 687));
            app.Settle();
            Assert.Empty(LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true));
            TestPictures.Save(app, $"settings-{name}-{(Application.Current!.ActualThemeVariant == ThemeVariant.Dark ? "dark" : "light")}");
        }
    }

    [AvaloniaFact]
    public async Task The_first_focus_is_the_section_button_up_and_down_move_and_enter_shows_without_saving()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        ShellDriver.SeedGlobalGitConfig(data.Path, UseConfigOnly);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        await app.WaitForIdleAsync();
        var dialog = await app.OpenSettingsAsync();
        await app.WaitForAsync(() => Focused(app) == "SettingsNavGeneral", "the first focus on General");
        Assert.DoesNotContain(app.TopDialogView!.GetVisualDescendants().OfType<TextBox>(), box => box.IsFocused);

        app.PressKey(Key.Down);
        Assert.Equal("SettingsNavGit", Focused(app));
        app.PressKey(Key.Down);
        Assert.Equal("SettingsNavTools", Focused(app));
        Assert.Equal(SettingsSection.General, dialog.SelectedSection);
        app.PressKey(Key.Up);
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        Assert.Equal("SettingsNavShortcuts", Focused(app));
        app.PressKey(Key.Up);
        app.PressKey(Key.Enter);
        app.Settle();

        // Enter on a section's button shows it, the focus staying there, and saves nothing (7.1).
        Assert.Equal(SettingsSection.Tools, dialog.SelectedSection);
        Assert.Equal("SettingsNavTools", Focused(app));
        Assert.False(dialog.IsClosed);
        Assert.False(File.Exists(Path.Combine(data.Path, "settings.json")));

        // Esc closes the screen, the focus back on the toolbar's button.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => dialog.IsClosed, "the screen to close");
        await app.WaitForAsync(() => Focused(app) == "SettingsButton", "the focus back on Settings");
        Assert.False(File.Exists(Path.Combine(data.Path, "settings.json")));
    }

    [AvaloniaFact]
    public async Task Enter_in_a_box_saves_and_the_pick_dialog_stacks_over_the_screen_and_gives_the_focus_back()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var dialog = await app.OpenSettingsAsync();

        // The Pick dialog over the screen (D104): two layers, the focus in its filter, then back on Choose….
        app.ChooseSection(SettingsSection.Tools);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "EditorChooseButton"));
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        Assert.Equal(2, app.Session.Services.DialogHost.Open.Count);
        await app.WaitForAsync(() => Focused(app) == "PickFilter", "the focus in the filter");
        Assert.Equal(
            ["None", "Visual Studio Code", "Sublime Text", "IntelliJ IDEA", "Other…"],
            app.FindByAutomationId(app.TopDialogView!, "PickList").GetVisualDescendants().OfType<ListBoxItem>().Select(item => AutomationProperties.GetName(item)));
        var vscode = app.PickItem("Visual Studio Code");
        Assert.Equal(ToolCatalogues.FixedEditors[0].Program, AutomationProperties.GetHelpText(vscode));
        var text = vscode.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == "Visual Studio Code");
        var detail = vscode.GetVisualDescendants().OfType<TextBlock>().First(block => block.Name == "PickItemDetail");
        Assert.Equal(PickRowPanel.Gap, app.BoundsOf(detail).X - app.BoundsOf(text).Right, 1);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => pick.IsClosed && Focused(app) == "EditorChooseButton", "the focus back on Choose…");
        Assert.False(dialog.IsClosed);

        // Enter in a box saves (7.8).
        app.ChooseSection(SettingsSection.General);
        app.ReplaceText("AutoFetchMinutesBox", "15");
        app.PressKey(Key.Enter);
        await app.WaitForSettingsSaveAsync(dialog);
        Assert.True(dialog.IsClosed);
        Assert.Equal(15, app.Session.Settings.Current.AutoFetchMinutes);
    }

    [AvaloniaTheory]
    [InlineData(1100, 700)]
    [InlineData(1920, 1080)]
    public async Task Every_shortcut_row_shows_its_action_and_keys_apart_and_whole_and_the_extend_row_takes_two_lines(int width, int height)
    {
        // The rows come from the app's own key table (AppShortcuts.Groups, 6.4, 6.11), which must
        // equal ShortcutTable, written out by hand in Windows' and Linux's words (6.12), so that the
        // screen and the report's table cannot drift.
        var groups = AppShortcuts.Groups(CommandKey.Ctrl);
        AssertGroups(ShortcutTable, groups);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, width, height);
        await app.WaitUntilReadyAsync();
        var shell = app.Session.Shell;
        var dialog = new SettingsDialogViewModel(
            new SettingsScreenServices(
                app.Session.Settings,
                shell.Theme,
                shell.UseTheme,
                null,
                app.Session.Services.GitSettings,
                ToolCatalogues.Fixed,
                groups,
                app.Folders,
                app.Session.Services.DialogHost,
                null,
                app.Session.Log),
            SettingsSection.KeyboardShortcuts);
        _ = app.Session.Services.DialogHost.ShowAsync(dialog);
        await app.WaitForSettingsReadsAsync(dialog);

        var view = app.TopDialogView!;
        Assert.Equal("These keys are fixed.", ((TextBlock)app.FindByAutomationId(view, "ShortcutsIntro")).Text);
        Assert.Equal(ShortcutTable.Select(group => group.Title), view.GetVisualDescendants().OfType<TextBlock>().Where(block => AutomationProperties.GetAutomationId(block) == "ShortcutGroup").Select(block => block.Text));
        var rows = view.GetVisualDescendants().OfType<Grid>().Where(grid => AutomationProperties.GetAutomationId(grid) == "ShortcutRow").ToList();
        Assert.Equal(ShortcutTable.Sum(group => group.Rows.Count), rows.Count);
        var scroller = (ScrollViewer)app.FindByAutomationId(view, "SettingsSection");
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height, "The section scrolls.");
        foreach (var row in rows)
        {
            var model = (ShortcutRow)row.DataContext!;
            var blocks = row.Children.OfType<TextBlock>().ToList();
            var (label, keys) = (blocks[0], blocks[1]);
            Assert.Equal($"{model.Action}: {model.Keys}", AutomationProperties.GetName(row));
            Assert.Equal((model.Action, model.Keys), (label.Text, keys.Text));
            Assert.True(row.Bounds.Height >= 22);
            Assert.Equal(TextAlignment.Right, keys.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, keys.TextWrapping);
            Assert.DoesNotContain(label.TextLayout.TextLines, line => line.HasCollapsed);
            Assert.DoesNotContain(keys.TextLayout.TextLines, line => line.HasCollapsed);
            Assert.True(new Rect(row.Bounds.Size).Contains(keys.Bounds), $"{model.Action}'s keys lie inside the row.");
            Assert.True(new Rect(row.Bounds.Size).Contains(label.Bounds), $"{model.Action}'s label lies inside the row.");
            Assert.True(label.Bounds.X + label.TextLayout.Width + 16 <= keys.Bounds.Right - keys.TextLayout.Width + 0.5, $"{model.Action}: its label and keys are apart.");
            var lines = keys.TextLayout.TextLines.Count;
            if (model.Action == "Extend the graph's selection")
            {
                Assert.Equal("Shift+Up, Shift+Down, Shift+PageUp, Shift+PageDown, Shift+Home, Shift+End", keys.Text);
                Assert.Equal(2, lines);
                Assert.True(row.Bounds.Height > 22);
            }
            else
            {
                Assert.Equal(1, lines);
            }
        }

        // The end of the list: the last group's three rows wholly in view once scrolled down.
        scroller.Offset = new Vector(0, scroller.Extent.Height);
        app.Settle();
        var section = app.BoundsOf(scroller);
        foreach (var row in rows.TakeLast(3))
        {
            Assert.True(section.Contains(app.BoundsOf(row)), $"{AutomationProperties.GetName(row)} lies in view.");
        }

        TestPictures.Save(app, $"settings-shortcuts-end-{width}x{height}");
        dialog.Cancel();
    }

    [AvaloniaFact]
    public async Task The_screen_reads_and_writes_gits_own_settings_through_real_git()
    {
        // The report's S14 step 2 and S18 and S19's saves, in one start: the opening's seven reads,
        // the global identity through the app's writer, Rebase in "This repository" as one
        // operation of its queue, each read back and the screen closed.
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        ShellDriver.SeedGlobalGitConfig(data.Path, UseConfigOnly);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        await app.WaitForIdleAsync();
        var since = app.Session.GitCalls.Snapshot().Count;

        await app.OpenSettingsAsync();
        // The backups' clean-up at the tab's opening (phase 4's) may still run its read meanwhile.
        var opening = app.Session.GitCalls.Snapshot().Skip(since)
            .Where(call => !call.CommandText.StartsWith("git for-each-ref", StringComparison.Ordinal))
            .ToList();
        Assert.True(opening.Count == 7, string.Join("\n", opening.Select(call => $"{call.CommandText} in {call.WorkingDirectory}")));
        Assert.Equal(2, opening.Count(call => call.CommandText == "git config --show-scope --show-origin -z --list"));
        Assert.Contains(opening, call => call.CommandText == "git config --show-scope --show-origin -z --list" && GitFileNames.SamePath(call.WorkingDirectory, data.Path));
        Assert.Equal(["git config --get pull.ff", "git config --get branch.main.rebase", "git config --get pull.rebase"], opening.Where(call => call.CommandText.StartsWith("git config --get", StringComparison.Ordinal)).Select(call => call.CommandText));
        Assert.Equal(2, opening.Count(call => call.CommandText.StartsWith("git var GIT_", StringComparison.Ordinal)));

        app.ChooseScope(SettingsScope.Repository);
        var snapshot = app.SettingsSnapshot();
        Assert.Equal("Git cannot make commits here: no email is set.", snapshot.Field("IdentityInEffect")?.Text);
        Assert.Equal(["user.useConfigOnly is set, so git does not guess a missing name or email."], snapshot.Lines("IdentityNotices"));
        Assert.Equal("Pull into main: Merge (fast-forward when possible): nothing is set, so the app merges; git in a terminal stops when the branches have diverged.", snapshot.Field("PullInEffect")?.Text);
        Assert.Equal("Same as global (not set)", snapshot.Field("MergeToolValue")?.Text);

        // A global identity: two writes in the data folder, then the read back; the screen closes.
        app.SetIdentity(SettingsScope.Global, "Ann Example", "ann@example.com");
        since = app.Session.GitCalls.Snapshot().Count;
        Assert.True(await app.SaveSettingsAsync());
        var saved = app.Session.GitCalls.Snapshot().Skip(since).ToList();
        Assert.Equal(
            ["git config --global --replace-all user.name \"Ann Example\"", "git config --global --replace-all user.email ann@example.com"],
            saved.Where(call => call.CommandText.Contains("--replace-all", StringComparison.Ordinal)).Select(call => call.CommandText));
        Assert.Equal(
            "[user]\n\tuseConfigOnly = true\n\tname = Ann Example\n\temail = ann@example.com\n",
            File.ReadAllText(ShellDriver.GlobalGitConfigPath(data.Path)).Replace("\r\n", "\n", StringComparison.Ordinal));

        // The repository's Rebase: one operation, both keys at --local, the status bar's result.
        await app.OpenSettingsAsync();
        app.ChooseScope(SettingsScope.Repository);
        snapshot = app.SettingsSnapshot();
        Assert.Equal(("Same as global: Ann Example", "Commits here are made as Ann Example <ann@example.com>."), (snapshot.Field("UserNameBox")?.Placeholder, snapshot.Field("IdentityInEffect")?.Text));
        app.ChoosePullMode(SettingsScope.Repository, Core.Git.PullMode.Rebase);
        Assert.True(await app.SaveSettingsAsync());
        await app.WaitForIdleAsync();
        Assert.Equal("Saved the repository's settings", app.Session.Shell.ActiveTab.OperationStatus.Text);
        Assert.Equal("true", (await repo.GitAsync("config", "--local", "--get", "pull.rebase")).StandardOutput.Trim());
        Assert.Equal("true", (await repo.GitAsync("config", "--local", "--get", "pull.ff")).StandardOutput.Trim());

        // The global scope notices the repository's own mode (SD8); its in-effect line names .git/config (SD18).
        await app.OpenSettingsAsync(SettingsOpening.Button);
        app.ChooseSection(SettingsSection.Git);
        Assert.Equal(["settings sets its own: Rebase."], app.SettingsSnapshot().Lines("PullNotices"));
        app.ChooseScope(SettingsScope.Repository);
        Assert.Equal("Pull into main: Rebase (pull.rebase = true in .git/config).", app.SettingsSnapshot().Field("PullInEffect")?.Text);
        TestPictures.Save(app, "settings-real-git-repository");
        app.CancelSettings();
    }

    [AvaloniaFact]
    public async Task A_refused_save_focuses_the_wrong_box_with_the_caret_at_its_end_and_nothing_selected()
    {
        // 7.8: the first wrong field takes the focus, its caret at the end as a dialog's box on
        // opening. The Tab focus selected the whole text, which a caret already at the end left
        // selected (the gate's s16b and s18a).
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        await app.OpenSettingsAsync();
        app.ChooseSection(SettingsSection.General);
        app.ReplaceText("AutoFetchMinutesBox", "61");

        Assert.False(await app.SaveSettingsAsync(), "Save closed the screen with 61 minutes.");

        await app.WaitForAsync(() => Focused(app) == "AutoFetchMinutesBox", "the focus in the minutes' box");
        var box = (TextBox)app.FindByAutomationId(app.TopDialogView!, "AutoFetchMinutesBox");
        Assert.Equal((2, 2, 2), (box.CaretIndex, box.SelectionStart, box.SelectionEnd));
    }

    [AvaloniaFact]
    public async Task A_disabled_ticked_box_is_drawn_in_the_disabled_colour_not_the_accent()
    {
        // VcCheckBox's box: the accent while ticked and enabled, the disabled colour while ticked
        // and disabled (the gate's w1e and l30c, whose "Already in this workspace" rows, ticked and
        // disabled, looked like the others; design 1.9). Keep ignored files is ticked by default.
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        await app.OpenSettingsAsync();
        app.ChooseSection(SettingsSection.General);
        var check = (CheckBox)app.FindByAutomationId(app.TopDialogView!, "KeepIgnoredCheckBox");
        var box = check.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Box");
        Assert.True(check.IsChecked);

        Assert.Equal(Brush(app, "VcAccentBrush"), ((ISolidColorBrush)box.Background!).Color);
        check.IsEnabled = false;
        app.Settle();
        Assert.Equal((Brush(app, "VcTextDisabledBrush"), Brush(app, "VcTextDisabledBrush")), (((ISolidColorBrush)box.Background!).Color, ((ISolidColorBrush)box.BorderBrush!).Color));
        check.IsEnabled = true;
        app.Settle();
        Assert.Equal(Brush(app, "VcAccentBrush"), ((ISolidColorBrush)box.Background!).Color);
    }

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    private static string? Focused(ShellDriver app)
    {
        app.Settle();
        return app.Window.FocusManager?.GetFocusedElement() is Control focused
            ? focused.GetSelfAndVisualAncestors().OfType<Control>().Select(AutomationProperties.GetAutomationId).FirstOrDefault(id => id is { Length: > 0 })
            : null;
    }

    private static Border Card(ShellDriver app) =>
        app.TopDialogView!.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("settings"));

    /// <summary>The settings list's groups equal <paramref name="expected"/>, title by title, row by row and note by note (the records' lists compare by reference).</summary>
    internal static void AssertGroups(IReadOnlyList<ShortcutGroup> expected, IReadOnlyList<ShortcutGroup> actual)
    {
        Assert.Equal(expected.Select(group => group.Title), actual.Select(group => group.Title));
        foreach (var (want, got) in expected.Zip(actual))
        {
            Assert.Equal(want.Rows, got.Rows);
            Assert.Equal(want.Note, got.Note);
        }
    }
}
