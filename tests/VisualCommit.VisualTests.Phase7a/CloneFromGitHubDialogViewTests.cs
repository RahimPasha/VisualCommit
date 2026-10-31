using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The repository browser's view (docs/phase-7a-design.md 9.4, 9.6; D301) on one empty tab with
/// GitHub off, the list a delegate: the view the locator finds, its ids and names, the wide card,
/// rows 28 high, a detail only with text and cut at its end, each line in its state, the error in
/// the danger colour, the filter focused and Enter confirming.
/// </summary>
public sealed class CloneFromGitHubDialogViewTests
{
    private static readonly string[] LineIds = ["CloneFromGitHubStatus", "CloneFromGitHubError", "CloneFromGitHubMore", "CloneFromGitHubSso"];

    [AvaloniaFact]
    public async Task The_view_has_its_ids_and_names_on_the_wide_card_and_the_filter_takes_the_focus()
    {
        Assert.IsType<CloneFromGitHubDialogView>(new DialogViewLocator().Build(new CloneFromGitHubDialogViewModel(_ => new TaskCompletionSource<HostingResult<RepositoryList>>().Task, new RecordingLog())));
        foreach (var (width, height) in new[] { DefaultSize, FullSize })
        {
            using var data = new TempDirectory("clone browser view");
            using var app = ShellDriver.Start(data.Path, width, height);
            var held = new TaskCompletionSource<HostingResult<RepositoryList>>();
            var (dialog, view) = await ShowAsync(app, _ => held.Task);

            Assert.Equal("CloneFromGitHubDialog", AutomationProperties.GetAutomationId(view));
            Assert.Equal("Clone from GitHub", AutomationProperties.GetName(view));
            Assert.Equal("Clone from GitHub", ((TextBlock)app.FindByAutomationId(view, "CloneFromGitHubTitle")).Text);
            Assert.Contains("dialogTitle", ((TextBlock)app.FindByAutomationId(view, "CloneFromGitHubTitle")).Classes);
            var filter = (TextBox)app.FindByAutomationId(view, "CloneFromGitHubFilter");
            Assert.Equal(("Filter", "Filter"), (AutomationProperties.GetName(filter), filter.PlaceholderText));
            Assert.True(filter.IsEffectivelyEnabled);
            Assert.Equal("Clone from GitHub", AutomationProperties.GetName(app.FindByAutomationId(view, "CloneFromGitHubList")));
            Assert.Equal(("Cancel", "Choose"), (AutomationProperties.GetName(app.FindByAutomationId(view, "CancelButton")), AutomationProperties.GetName(app.FindByAutomationId(view, "ConfirmButton"))));
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), block => block.Classes.Contains("dialogRepository") && block.IsEffectivelyVisible);
            await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneFromGitHubFilter", "the focus in the filter");

            var card = Card(view);
            var bounds = app.BoundsOf(card);
            Assert.Equal(460, bounds.Width);
            Assert.Equal((width - 460) / 2.0, bounds.X, 1.0);
            Assert.Equal((height - bounds.Height) / 2.0, bounds.Y, 1.0);
            held.SetResult(HostingResult<RepositoryList>.Ok(new RepositoryList([], false, false)));
            await dialog.Load;
            dialog.HandleEscape();
        }
    }

    [AvaloniaFact]
    public async Task Each_state_shows_its_own_line_the_error_in_the_danger_colour()
    {
        using var data = new TempDirectory("clone browser lines");
        using var app = ShellDriver.Start(data.Path);

        var held = new TaskCompletionSource<HostingResult<RepositoryList>>();
        var (loading, view) = await ShowAsync(app, _ => held.Task);
        AssertLines(app, view, ("CloneFromGitHubStatus", "Loading repositories…"));
        AssertListHeight(app, view, 58);
        Assert.False(((Button)app.FindByAutomationId(view, "ConfirmButton")).IsEffectivelyEnabled);
        Assert.True(((Button)app.FindByAutomationId(view, "CancelButton")).IsEffectivelyEnabled);
        loading.HandleEscape();
        app.Settle();

        var (_, empty) = await ShowListedAsync(app, new RepositoryList([], false, false));
        AssertLines(app, empty, ("CloneFromGitHubStatus", "No repositories to list"));
        Close(app);

        var (_, capped) = await ShowListedAsync(app, new RepositoryList(Rows(3), true, true));
        AssertLines(
            app,
            capped,
            ("CloneFromGitHubMore", "Only the 1,000 most recently updated repositories are listed."),
            ("CloneFromGitHubSso", "Some organisations' repositories are hidden until the token is authorised for single sign-on."));
        var list = app.BoundsOf(app.FindByAutomationId(capped, "CloneFromGitHubList"));
        var more = app.BoundsOf(app.FindByAutomationId(capped, "CloneFromGitHubMore"));
        var sso = app.BoundsOf(app.FindByAutomationId(capped, "CloneFromGitHubSso"));
        Assert.True(list.Bottom <= more.Top && more.Bottom <= sso.Top, $"The lines are not below the list in order: {list}, {more}, {sso}.");
        Close(app);

        var (failed, failedView) = await ShowAsync(app, _ => Task.FromResult(HostingResult<RepositoryList>.Fail(new HostingFailure(HostingFailureKind.ServerError, "Server Error"))));
        await failed.Load;
        app.Settle();
        AssertLines(app, failedView, ("CloneFromGitHubError", "GitHub had a problem. Try again later."));
        var error = (TextBlock)app.FindByAutomationId(failedView, "CloneFromGitHubError");
        Assert.Equal(Dark.Danger, (error.Foreground as ISolidColorBrush)?.Color);
        Assert.Contains("dialogError", error.Classes);
        AssertListHeight(app, failedView, 58);
        Assert.True(((TextBox)app.FindByAutomationId(failedView, "CloneFromGitHubFilter")).IsEffectivelyEnabled);
        var status = (TextBlock)app.FindByAutomationId(failedView, "CloneFromGitHubMore");
        Assert.Equal(Dark.TextSecondary, (status.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(11.5, status.FontSize);
        Close(app);
    }

    [AvaloniaFact]
    public async Task Rows_are_28_high_and_a_detail_shows_only_with_text_cut_at_its_end()
    {
        using var data = new TempDirectory("clone browser rows");
        using var app = ShellDriver.Start(data.Path);
        var longDescription = string.Join(' ', Enumerable.Repeat("A description long enough to be cut at its end", 4));
        var (dialog, view) = await ShowListedAsync(app, new RepositoryList(
        [
            Repository("octo", "hello", isPrivate: true),
            Repository("octo", "plain"),
            Repository("octo", "long", isFork: true, description: longDescription),
        ], false, false));

        var items = Items(view);
        Assert.Equal(["octo/hello", "octo/plain", "octo/long"], items.Select(item => AutomationProperties.GetName(item)));
        Assert.All(items, item => Assert.Equal("CloneFromGitHubItem", AutomationProperties.GetAutomationId(item)));
        Assert.Equal(["Private", null, "Fork · " + longDescription], items.Select(item => AutomationProperties.GetHelpText(item)));
        Assert.Equal(["octo/hello\nPrivate", "octo/plain", "octo/long\nFork · " + longDescription], items.Select(item => ToolTip.GetTip(item) as string));
        Assert.All(items, item => Assert.Equal(28, item.Bounds.Height));
        Assert.All(items, item => Assert.Equal(new Thickness(10, 0), item.Padding));

        var details = items.Select(item => item.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "CloneFromGitHubItemDetail")).ToList();
        Assert.Equal([true, false, true], details.Select(detail => detail.IsEffectivelyVisible));
        Assert.All(details, detail => Assert.Equal(TextTrimming.CharacterEllipsis, detail.TextTrimming));
        Assert.Equal(11.5, details[0].FontSize);
        Assert.Equal(Dark.TextSecondary, (details[0].Foreground as ISolidColorBrush)?.Color);
        var name = items[0].GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == "octo/hello");
        Assert.Equal(12, app.BoundsOf(details[0]).X - app.BoundsOf(name).Right, 1.0);
        Assert.Equal(Dark.TextPrimary, (name.Foreground as ISolidColorBrush)?.Color);

        // The long detail fills the row to its end and is cut there, its start whole.
        var row = app.BoundsOf(items[2]);
        var cut = app.BoundsOf(details[2]);
        Assert.True(cut.Right <= row.Right - 10 + 1, $"The detail {cut} runs past the row {row}.");
        Assert.True(details[2].TextLayout.TextLines.Any(line => line.HasCollapsed), "The long detail is not cut.");
        dialog.HandleEscape();
    }

    [AvaloniaFact]
    public async Task Enter_in_the_filter_confirms_with_a_selection_and_the_list_scrolls_after_8_rows()
    {
        using var data = new TempDirectory("clone browser enter");
        using var app = ShellDriver.Start(data.Path);
        var (dialog, view) = await ShowListedAsync(app, new RepositoryList(Rows(12), false, false));
        AssertListHeight(app, view, 232);
        var scroll = app.FindByAutomationId(view, "CloneFromGitHubList").GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, "The list of 12 rows does not scroll.");

        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneFromGitHubFilter", "the focus in the filter");
        app.PressKey(Key.Enter);
        Assert.False(dialog.IsClosed);

        app.Type("repo-03");
        app.Settle();
        Assert.Equal(["octo/repo-03"], dialog.Rows.Select(row => row.FullName));
        app.Click(Items(view)[0]);
        app.Settle();
        Assert.Equal("octo/repo-03", dialog.Selected?.FullName);
        app.Click(app.FindByAutomationId(view, "CloneFromGitHubFilter"));
        app.PressKey(Key.Enter);
        Assert.True(dialog.IsClosed);
        Assert.Equal("http://127.0.0.1:47395/octo/repo-03.git", await dialog.Result);
    }

    private static async Task<(CloneFromGitHubDialogViewModel Dialog, Control View)> ShowListedAsync(ShellDriver app, RepositoryList list)
    {
        var shown = await ShowAsync(app, _ => Task.FromResult(HostingResult<RepositoryList>.Ok(list)));
        await shown.Dialog.Load;
        app.Settle();
        return shown;
    }

    private static async Task<(CloneFromGitHubDialogViewModel Dialog, Control View)> ShowAsync(ShellDriver app, Func<CancellationToken, Task<HostingResult<RepositoryList>>> list)
    {
        var dialog = new CloneFromGitHubDialogViewModel(list, new RecordingLog(), () => OctoLogin);
        _ = dialog.Start();
        _ = app.Session.Services.DialogHost.ShowAsync(dialog);
        await app.WaitForAsync(() => app.TopDialogView is CloneFromGitHubDialogView, "the browser");
        app.Settle();
        return (dialog, app.TopDialogView!);
    }

    private static void Close(ShellDriver app)
    {
        app.Session.Services.DialogHost.CancelAll();
        app.Settle();
    }

    private static Border Card(Control view) =>
        view.GetVisualDescendants().OfType<Border>().First(border => border.Name == "CloneFromGitHubCard");

    private static List<ListBoxItem> Items(Control view) =>
        [.. view.GetVisualDescendants().OfType<ListBoxItem>().Where(item => item.IsEffectivelyVisible).OrderBy(item => item.Bounds.Y)];

    private static void AssertLines(ShellDriver app, Control view, params (string Id, string Text)[] shown)
    {
        foreach (var id in LineIds)
        {
            var line = (TextBlock)app.FindByAutomationId(view, id);
            var expected = shown.FirstOrDefault(entry => entry.Id == id).Text;
            Assert.Equal(expected is not null, line.IsEffectivelyVisible);
            if (expected is not null)
            {
                Assert.Equal(expected, line.Text);
            }
        }

        var clipped = LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true);
        Assert.True(clipped.Count == 0, string.Join(Environment.NewLine, clipped));
    }

    /// <summary>The list's height inside its border.</summary>
    private static void AssertListHeight(ShellDriver app, Control view, double height) =>
        Assert.Equal(height, app.BoundsOf(app.FindByAutomationId(view, "CloneFromGitHubList")).Height, 1.0);

    private static IReadOnlyList<RepositoryInfo> Rows(int count) =>
        [.. Enumerable.Range(1, count).Select(index => Repository("octo", $"repo-{index:00}"))];

    private static RepositoryInfo Repository(string owner, string name, bool isPrivate = false, bool isFork = false, string? description = null) =>
        new(
            new HostedRepository(HostingProvider.GitHub, "127.0.0.1:47395", owner, name),
            "main",
            isPrivate,
            isFork,
            false,
            description,
            new Uri($"http://127.0.0.1:47395/{owner}/{name}.git"),
            new Uri($"http://127.0.0.1:47395/{owner}/{name}"),
            DateTimeOffset.UnixEpoch,
            true,
            null,
            null);
}
