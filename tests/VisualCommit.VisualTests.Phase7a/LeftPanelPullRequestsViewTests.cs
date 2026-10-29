using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Hosting;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.PullRequestTestData;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The "Pull requests" section's rows and a branch row's <c>#n</c> in the left panel's view
/// (docs/phase-7a-design.md 10.3, 10.6; D303, D308), headless, the panel alone in a window of its
/// size at 1100×700: the templates' automation ids and parts, 26-high rows, the <c>#n</c>
/// collapsed without a pull request and the name's width unchanged then, the disabled message
/// rows' tooltip. The clicks that reach the repository (the sign-in row, a double-click) are
/// <c>PullRequestPanelClickTests</c>' in a whole window.
/// </summary>
public sealed class LeftPanelPullRequestsViewTests
{
    /// <summary>The main area's height at 1100×700: the window less the tab strip, toolbar and status bar.</summary>
    private const double PanelHeight = 700 - 36 - 52 - 26;

    [AvaloniaFact]
    public void Rows_have_their_ids_parts_and_height()
    {
        var panel = Panel(Loaded());
        using var host = Show(panel);
        var window = host.Window;

        var items = WithAutomationId(window, "PullRequestItem");
        Assert.Equal(["#5 Fix a typo in the greeting", "#3 Shorten long labels", "#2 Update the readme", "#1 Add the login form"], items.Select(AutomationProperties.GetName));
        Assert.All(items, item => Assert.Equal(26, item.Bounds.Height));
        var two = items[2];
        Assert.Equal("#2", Part(two, "PullRequestNumber").Text);
        Assert.Equal("Update the readme", Part(two, "PullRequestTitle").Text);
        Assert.Equal("octo", Part(two, "PullRequestAuthor").Text);
        Assert.True(Part(two, "PullRequestDraft").IsEffectivelyVisible);
        Assert.Equal("Draft", Part(two, "PullRequestDraft").Text);
        Assert.False(Part(items[3], "PullRequestDraft").IsEffectivelyVisible);
        Assert.Equal(TextTrimming.CharacterEllipsis, Part(two, "PullRequestTitle").TextTrimming);
        Assert.Equal(90, Part(two, "PullRequestAuthor").MaxWidth);

        // Number, title, then Draft and the author at the right end, in that order from the left.
        var number = Bounds(window, Part(two, "PullRequestNumber"));
        var title = Bounds(window, Part(two, "PullRequestTitle"));
        var draft = Bounds(window, Part(two, "PullRequestDraft"));
        var author = Bounds(window, Part(two, "PullRequestAuthor"));
        Assert.True(number.Right <= title.Left && title.Right <= draft.Left && draft.Right <= author.Left, $"{number} {title} {draft} {author}");
        Assert.True(author.Right <= Bounds(window, two).Right - 12 + 0.5);

        // Depth 1: as a branch row, the indent and the chevron's room before the number.
        Assert.Equal(Bounds(window, two).Left + RefListRow.FirstIndent + RefListRow.IndentPerLevel + 18, number.Left, 0.5);
        Assert.Equal(two.DataContext is PullRequestRow { ToolTip: { } tip } ? tip : null, ToolTip.GetTip((Control)Part(two, "PullRequestTitle").GetVisualParent()!));
        Assert.DoesNotContain(LayoutAudit.FindClippedText(window), line => !line.Contains("ellipsis", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Message_rows_have_their_id_text_and_tooltip_and_only_two_take_a_click()
    {
        foreach (var (state, text, clickable) in new (RepositoryHostingState, string, bool)[]
        {
            (SignedOut(), "Sign in to GitHub to see pull requests…", true),
            (Loading(), "Loading pull requests…", false),
            (Failed(Forbidden), "GitHub answered: Resource not accessible by personal access token.", false),
            (Loaded(new PullRequestList([], false)), "No open pull requests", false),
        })
        {
            var panel = Panel(state);
            using var host = Show(panel);
            var window = host.Window;

            var message = Assert.Single(WithAutomationId(window, "PullRequestMessage"));
            Assert.Equal(text, AutomationProperties.GetName(message));
            Assert.Equal(26, message.Bounds.Height);
            Assert.Equal(clickable, message.IsEffectivelyEnabled);
            var textBlock = Part(message, "PullRequestMessageText");
            Assert.Equal(text, textBlock.Text);
            Assert.Equal(TextTrimming.CharacterEllipsis, textBlock.TextTrimming);
            Assert.True(ToolTip.GetShowOnDisabled(textBlock));
            Assert.Equal(((PullRequestMessageRow)message.DataContext!).ToolTip, ToolTip.GetTip(textBlock));
        }
    }

    [AvaloniaFact]
    public void A_branch_row_s_number_is_collapsed_without_a_pull_request_and_leaves_the_name_its_width()
    {
        var without = new LeftPanelViewModel();
        without.Update(HelloRefs());
        OpenFolders(without);
        using var plainHost = Show(without);
        var plain = plainHost.Window;
        var plainLogin = NameOf(plain, "login", depth: 2);

        var with = Panel(Loaded());
        OpenFolders(with);
        using var host = Show(with);
        var window = host.Window;

        // Without hosting no #n shows and every name keeps phase 1's width.
        Assert.All(plain.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name == "RefPullRequest"), text => Assert.False(text.IsEffectivelyVisible));
        var local = RowOf(window, "refs/heads/feature/login");
        var number = Part(local, "RefPullRequest");
        Assert.True(number.IsEffectivelyVisible);
        Assert.Equal("#1", number.Text);
        Assert.Equal("Pull request #1: Add the login form", ToolTip.GetTip(number));
        Assert.Equal("RefPullRequest", AutomationProperties.GetAutomationId(number));
        var name = Bounds(window, Part(local, "RefName"));
        Assert.True(name.Right <= Bounds(window, number).Left, $"{name} {Bounds(window, number)}");
        Assert.True(Bounds(window, number).Right <= Bounds(window, local).Right - 12 + 0.5);

        // A row without one: its name as wide as without hosting.
        var main = RowOf(window, "refs/heads/main");
        Assert.False(Part(main, "RefPullRequest").IsEffectivelyVisible);
        Assert.Equal(Bounds(plain, RowOf(plain, "refs/heads/main").GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "RefName")).Width, Bounds(window, Part(main, "RefName")).Width, 0.5);
        Assert.True(plainLogin.Width > 0);

        // The long branch's name is cut and its #3 kept whole.
        var longRow = RowOf(window, "refs/remotes/origin/" + LongBranch);
        Assert.Equal("#3", Part(longRow, "RefPullRequest").Text);
        Assert.Contains(Part(longRow, "RefName").TextLayout.TextLines, line => line.HasCollapsed);
        var longNumber = Part(longRow, "RefPullRequest");
        Assert.True(longNumber.DesiredSize.Width - longNumber.Margin.Left <= longNumber.Bounds.Width + 0.5);
        Assert.DoesNotContain(longNumber.TextLayout.TextLines, line => line.HasCollapsed);
    }

    [AvaloniaFact]
    public void A_click_on_a_row_selects_it_and_a_message_row_starts_no_drag_or_selection()
    {
        var panel = Panel(Loaded());
        var activated = new List<string>();
        panel.RefActivated += (_, sha) => activated.Add(sha);
        using var host = Show(panel);
        var window = host.Window;
        var one = WithAutomationId(window, "PullRequestItem").Single(item => AutomationProperties.GetName(item) == "#1 Add the login form");

        Click(window, one);

        Assert.Equal([One.HeadSha], activated);
        Assert.Contains("selected", one.Classes);
    }

    private static LeftPanelViewModel Panel(RepositoryHostingState state)
    {
        var panel = new LeftPanelViewModel();
        panel.Update(HelloRefs());
        panel.UpdatePullRequests(state);
        return panel;
    }

    private static void OpenFolders(LeftPanelViewModel panel)
    {
        while (panel.Rows.OfType<RefItemRow>().FirstOrDefault(row => row.IsContainer && !row.IsOpen) is { } closed)
        {
            closed.ActivateCommand.Execute(null);
        }
    }

    private static ShownPanel Show(LeftPanelViewModel panel)
    {
        var window = new Window { Width = 260, Height = PanelHeight, Content = new LeftPanelView { DataContext = panel } };
        window.Show();
        Settle(window);
        return new ShownPanel(window);
    }

    /// <summary>The panel's window, closed at the test's end.</summary>
    private sealed record ShownPanel(Window Window) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Visual visual)
    {
        var centre = Bounds(window, visual).Center;
        window.MouseMove(centre);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Settle(window);
    }

    private static List<Control> WithAutomationId(Window window, string automationId) =>
        window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId)
            .ToList();

    private static TextBlock Part(Visual row, string name) =>
        row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == name);

    private static Button RowOf(Window window, string fullName) =>
        window.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "RefItem" && button.DataContext is RefItemRow row && LeftPanelViewModel.RefNameOf(row) == fullName);

    private static Rect NameOf(Window window, string text, int depth) =>
        Bounds(window, window.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetAutomationId(button) == "RefItem" && button.DataContext is RefItemRow row && row.Text == text && row.Depth == depth)
            .GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "RefName"));

    private static Rect Bounds(Window window, Visual visual) =>
        new(visual.TranslatePoint(default, window) ?? throw new InvalidOperationException("Not in the window."), visual.Bounds.Size);
}
