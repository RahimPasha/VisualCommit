using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The search bar in the whole app on the history scenario (docs/phase-5-design.md 3, report part F's
/// F10 and F11, FD1 to FD5): its place and parts over the graph, the box searching as the user
/// types, the marks and the count in each of their forms, Enter and Shift+Enter, the mode menu,
/// results kept by SHA across a refresh, Esc; and the driver's helpers for them (WP7's part of
/// ShellDriver.Phase5.Find.cs), which these tests also prove.
/// </summary>
public class SearchBarTests
{
    // The history scenario's rows (docs/test-reports/phase-5.md, scenario facts 2.1).
    private const int CompareWholeLines = 6;
    private const int RenameTheParser = 7;
    private const int HandleEmptyInput = 10;

    [AvaloniaFact]
    public async Task The_bar_opens_above_the_column_headers_and_searches_messages_authors_and_sha_prefixes()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.HistoryAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        Assert.Null(app.SearchBar());

        // F10 step 1: the bar, 36 high above the column headers, with the focus in its empty box.
        repository.OpenSearch();
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true }, "the search box to take the focus");
        var bar = app.SearchBar()!;
        Assert.Equal(new Rect(260, 88, 440, 36), bar.Bounds);
        Assert.Equal("Commits", bar.Mode);
        Assert.Equal(string.Empty, bar.Text);
        Assert.Equal("Message, author or SHA", bar.Placeholder);
        Assert.Equal(string.Empty, bar.Count);
        Assert.False(bar.PreviousEnabled);
        Assert.False(bar.NextEnabled);
        Assert.Equal("Previous match (Shift+Enter, Shift+F3)", bar.PreviousToolTip);
        Assert.Equal("Next match (Enter, F3)", bar.NextToolTip);
        Assert.Equal("Close the search (Esc)", bar.CloseToolTip);
        Assert.False(bar.ProgressShown);
        Assert.Equal(268, bar.ModeButtonBounds.X, 1);
        Assert.Equal(692, bar.CloseBounds.Right, 1);
        Assert.Equal(22, bar.PreviousBounds.Width, 1);
        Assert.Equal(6, bar.NextBounds.X - bar.PreviousBounds.Right, 1);
        Assert.Equal(6, bar.CloseBounds.X - bar.NextBounds.Right, 1);
        Assert.True(bar.BoxBounds.Width >= SearchBarView.MinBoxWidth);
        Assert.Equal(152, app.GraphRowsTop, 1);
        Assert.False(app.SearchMarks().IsActive);
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.StartsWith("git log --no-walk=unsorted", StringComparison.Ordinal));

        // Its colours (F10 step 1): the chrome, and the border line along its bottom; previous and
        // next, with nothing to go to, in the disabled text colour (the gate's f10a).
        var shot = app.Capture();
        Assert.Equal(Brush(app, "VcChromeBackgroundBrush"), shot.PixelAt(new Point(262, 90)));
        Assert.Equal(Brush(app, "VcBorderBrush"), shot.PixelAt(new Point(262, 123)));
        Assert.Equal(Brush(app, "VcTextDisabledBrush"), IconColour(app.Find<Button>("SearchPrevious")));
        Assert.Equal(Brush(app, "VcTextDisabledBrush"), IconColour(app.Find<Button>("SearchNext")));
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), IconColour(app.Find<Button>("SearchClose")));

        // Its root is a control element, a pane named "Search".
        var view = app.Window.GetVisualDescendants().OfType<SearchBarView>().Single();
        var peer = ControlAutomationPeer.CreatePeerForElement(view);
        Assert.True(peer.IsControlElement());
        Assert.Equal("SearchBar", peer.GetAutomationId());

        // Step 2: a word only in two commits' bodies.
        app.Type(Scenarios.HistoryFiles.BodyWord);
        await app.WaitForSearchAsync();
        Assert.Equal("2 matches", app.SearchBar()!.Count);
        var marks = app.SearchMarks();
        Assert.True(marks.IsActive);
        Assert.Equal([CompareWholeLines, HandleEmptyInput], marks.Marked);
        Assert.Equal(12, marks.Dimmed.Count);
        Assert.True(app.SearchBar()!.NextEnabled);
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), IconColour(app.Find<Button>("SearchPrevious")));
        Assert.Equal(Brush(app, "VcTextSecondaryBrush"), IconColour(app.Find<Button>("SearchNext")));
        var call = Assert.Single(app.GitCalls(), candidate => candidate.CommandText.StartsWith("git log --no-walk=unsorted", StringComparison.Ordinal));
        Assert.Equal("git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%H -F --regexp-ignore-case --grep=tokenizer --", call.CommandText);

        // Step 3: Enter selects the first match as a click does; the focus stays in the box (FD3).
        app.PressKey(Key.Enter);
        Assert.Equal(CompareWholeLines, repository.SelectedIndex);
        Assert.Equal("1 of 2", app.SearchBar()!.Count);
        Assert.True(app.SearchBar()!.BoxHasFocus);
        await app.WaitForAsync(() => repository.Details.Subject == "Compare whole lines", "the details of Compare whole lines");

        // Step 4: on, wrapping round, and back.
        app.PressKey(Key.Enter);
        Assert.Equal(HandleEmptyInput, repository.SelectedIndex);
        Assert.Equal("2 of 2", app.SearchBar()!.Count);
        app.PressKey(Key.Enter);
        Assert.Equal(CompareWholeLines, repository.SelectedIndex);
        app.PressKey(Key.Enter, RawInputModifiers.Shift);
        Assert.Equal(HandleEmptyInput, repository.SelectedIndex);
        Assert.Equal("2 of 2", app.SearchBar()!.Count);
        Assert.Single(app.GitCalls(), candidate => candidate.CommandText.StartsWith("git log --no-walk=unsorted", StringComparison.Ordinal));

        // Step 5: an author, matched in memory; the selection stays and is the fifth match.
        app.ReplaceText("SearchBox", "Jonas");
        await app.WaitForSearchAsync();
        Assert.Equal([0, 4, 6, 8, 10], app.SearchMarks().Marked);
        Assert.Equal("5 of 5", app.SearchBar()!.Count);
        Assert.Equal(HandleEmptyInput, repository.SelectedIndex);

        // Step 6: a SHA prefix.
        app.ReplaceText("SearchBox", "0575");
        await app.WaitForSearchAsync();
        Assert.Equal("1 match", app.SearchBar()!.Count);
        app.PressKey(Key.Enter);
        Assert.Equal(RenameTheParser, repository.SelectedIndex);
        Assert.Equal("1 of 1", app.SearchBar()!.Count);
        Assert.Equal([RenameTheParser], app.SearchMarks().Marked);

        // Step 7: Esc closes the bar, clears the marks, keeps the selection and gives the graph the focus.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.Window.FocusManager?.GetFocusedElement() is CommitGraphControl, "the focus on the graph");
        Assert.Null(app.SearchBar());
        Assert.Null(repository.Search);
        Assert.False(app.SearchMarks().IsActive);
        Assert.Equal(RenameTheParser, repository.SelectedIndex);
        Assert.Equal(116, app.GraphRowsTop, 1);

        // Step 8: the graph has the focus.
        app.PressKey(Key.Down);
        Assert.Equal(RenameTheParser + 1, repository.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task The_modes_keep_the_text_and_a_refresh_gives_git_only_the_new_rows()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.HistoryAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        repository.OpenSearch();
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true }, "the search box to take the focus");

        // F11 step 1: the mode menu at the mode button's bottom-left corner (FD5).
        var button = app.SearchBar()!.ModeButtonBounds;
        app.Click(button.Center);
        var menu = app.Menu();
        Assert.Equal(["Search", "Commits: message, author or SHA", "Files: a path or file name", "Changes: text added or removed"], menu.Lines());
        Assert.Equal("Commits: message, author or SHA", menu.Highlighted?.Label);
        Assert.Equal(button.X, menu.Bounds.X, 2);
        Assert.Equal(button.Bottom, menu.Bounds.Y, 2);
        Assert.Equal(WindowLayout.MenuHeight(3, 0), menu.Bounds.Height, 2);

        // Step 2: Files; the box has the focus again (FD4).
        app.ClickMenuItem("Files: a path or file name");
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true, Mode: "Files" }, "the Files mode with the focus in the box");
        Assert.Equal("File name or path (case ignored for A–Z)", app.SearchBar()!.Placeholder);
        app.Type("parser");
        await app.WaitForSearchAsync();
        Assert.Equal([RenameTheParser, HandleEmptyInput, 12], app.SearchMarks().Marked);
        Assert.Equal("3 matches", app.SearchBar()!.Count);
        Assert.Contains(
            app.GitCalls(),
            call => call.CommandText == "git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%x1e%H --first-parent --root --no-renames --name-only -- :(glob,icase)**/*parser* :(glob,icase)**/*parser*/**");

        // Step 3 and 4: the new name, then a text no path holds.
        app.ReplaceText("SearchBox", "reader");
        await app.WaitForSearchAsync();
        Assert.Equal([0, RenameTheParser], app.SearchMarks().Marked);
        app.ReplaceText("SearchBox", Scenarios.HistoryFiles.PickaxeText);
        await app.WaitForSearchAsync();
        Assert.Equal("No matches", app.SearchBar()!.Count);
        Assert.Empty(app.SearchMarks().Marked);
        Assert.Equal(14, app.SearchMarks().Dimmed.Count);
        Assert.False(app.SearchBar()!.NextEnabled);

        // Step 5: the Changes mode searches the same text (FD4).
        app.ChooseSearchMode("Changes: text added or removed");
        await app.WaitForSearchAsync();
        Assert.Equal("Changes", app.SearchBar()!.Mode);
        Assert.Equal(Scenarios.HistoryFiles.PickaxeText, app.SearchBar()!.Text);
        Assert.Equal([1, 9], app.SearchMarks().Marked);
        Assert.Equal("2 matches", app.SearchBar()!.Count);
        Assert.False(app.SearchBar()!.ProgressShown);

        // Step 6: a commit made outside the app: the marks stay and git is given the new row alone (3.4, FD8).
        var before = app.GitCalls().Count;
        repo.WriteFile("src/Config.cs", Scenarios.HistoryFiles.ConfigTraced);
        await repo.GitAsync("add", "--", "src/Config.cs");
        await repo.GitAsync("commit", "--quiet", "--message", "Trace again");
        var traced = (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim();
        await app.WaitForAsync(() => repository.Graph.Count > 0 && repository.Graph.CommitAt(0).Sha == traced, "the outside commit in the graph");
        await app.WaitForSearchAsync();
        Assert.Equal([0, 2, 10], app.SearchMarks().Marked);
        Assert.Equal("3 matches", app.SearchBar()!.Count);
        var searches = app.GitCalls().Skip(before).Where(call => call.CommandText.StartsWith("git log --no-walk=unsorted", StringComparison.Ordinal)).ToList();
        var search = Assert.Single(searches);
        Assert.Equal(traced, search.StandardOutput.Trim());
    }

    [AvaloniaFact]
    public async Task Search_closes_a_diff_and_Esc_closes_the_bar_only_while_the_focus_is_in_it()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.HistoryAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        app.Click(app.GraphRowPoint(0));
        app.WaitForCommitDetails();
        await app.WaitForAsync(() => repository.Details.Files.Count > 0, "the details' files");
        repository.Details.ActivateFileCommand.Execute(repository.Details.Files.First(row => row.IsFile));
        Assert.True(repository.IsDiffOpen);

        // Search closes the diff, which holds no work (11.1, 11.2), and opens the bar over the graph.
        repository.OpenSearch();
        Assert.False(repository.IsDiffOpen);
        Assert.True(repository.ShowsGraph);
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true }, "the search box to take the focus");

        // Search again selects the box's text (1.3: the key in the box selects its text).
        app.Type("lines");
        repository.OpenSearch();
        await app.WaitForAsync(() => app.Find<TextBox>("SearchBox").SelectedText == "lines", "the box's text selected");

        // With the focus on the graph, Esc goes on down the chain: the bar stays.
        app.Find<CommitGraphControl>("GraphRows").Focus();
        app.PressKey(Key.Escape);
        Assert.NotNull(app.SearchBar());

        // The close button closes it.
        app.Click(app.SearchBar()!.CloseBounds.Center);
        Assert.Null(app.SearchBar());
        Assert.False(app.SearchMarks().IsActive);
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void A_disabled_icon_button_draws_its_icon_in_the_disabled_text_colour(string variant)
    {
        // The gate's f10a, f11a and f11d: the bar's previous and next, disabled, were drawn as when
        // enabled. VcIconButton now has the rule of every other button theme of the app.
        Assert.True(Application.Current!.TryFindResource("VcIconButton", out var resource), "VcIconButton is not a resource.");
        var theme = Assert.IsType<ControlTheme>(resource);
        Assert.True(Application.Current!.TryFindResource("IconChevronUp", out var chevron), "IconChevronUp is not a resource.");
        Button Make(bool enabled) => new()
        {
            Theme = theme,
            IsEnabled = enabled,
            Content = new Icon { Data = (Geometry)chevron!, Width = 14, Height = 14 },
        };
        var (enabled, disabled) = (Make(true), Make(false));
        var window = new Window
        {
            Width = 120,
            Height = 60,
            RequestedThemeVariant = variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { enabled, disabled } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.CaptureRenderedFrame());

        Assert.Equal(Brush(window, "VcTextSecondaryBrush"), IconColour(enabled));
        Assert.Equal(Brush(window, "VcTextDisabledBrush"), IconColour(disabled));
        Assert.NotEqual(IconColour(enabled), IconColour(disabled));
        window.Close();
    }

    /// <summary>The colour the icon inside <paramref name="button"/> is drawn in.</summary>
    private static Color IconColour(Button button) =>
        button.GetVisualDescendants().OfType<Icon>().Single().Foreground is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"{button.Name}'s icon has no solid colour.");

    private static Color Brush(ShellDriver app, string key) => Brush(app.Window, key);

    private static Color Brush(Window window, string key) =>
        window.TryFindResource(key, window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");
}
