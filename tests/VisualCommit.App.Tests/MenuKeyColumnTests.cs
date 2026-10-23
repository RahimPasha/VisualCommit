using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Views.Shell;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A keyed menu item's key column (docs/phase-6-design.md 6.7, 6.12, 15.1, D215): the key at the
/// item's right edge, at least 24 right of its label, in the secondary colour (disabled on a
/// disabled item) in both themes, as the item's AcceleratorKey; the menu as wide as its widest
/// item with its key; a label cut at the widest menu keeps its key whole; the Header still the label.
/// </summary>
public class MenuKeyColumnTests
{
    [AvaloniaFact]
    public async Task The_empty_areas_keys_lie_at_the_items_right_in_the_secondary_colour_in_both_themes()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();

        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            app.Window.RequestedThemeVariant = theme;
            app.RightClick(new Point(484, 360));
            var menu = app.Menu();
            Assert.Equal(
                ["ops", "Fetch all remotes [Ctrl+Shift+F]", "Pull [Ctrl+Shift+L]", "Push [Ctrl+Shift+K]", "—", "Create branch… [Ctrl+B]", "Stash changes… [Ctrl+Shift+S] (disabled: \"There are no changes to stash\")", "—", "Show reflog [Ctrl+Shift+R]", "Show backups", "Manage remotes…"],
                menu.LinesWithKeys());

            // The phase 3 lines stay as they were: the keys are no part of an item's label.
            Assert.Equal(["ops", "Fetch all remotes", "Pull", "Push", "—", "Create branch…", "Stash changes… (disabled: \"There are no changes to stash\")", "—", "Show reflog", "Show backups", "Manage remotes…"], menu.Lines());

            var widest = 0.0;
            foreach (var item in Items(app))
            {
                var label = (string)item.Header!;
                var labelBlock = item.GetVisualDescendants().OfType<TrimmedTextBlock>().Single();
                Assert.Equal(label, labelBlock.Text);
                Assert.Equal(label, AutomationProperties.GetName(item));
                var key = ShellDriver.MenuKeyBlockOf(item);
                if (key is null)
                {
                    Assert.Null(AutomationProperties.GetAcceleratorKey(item));
                    widest = Math.Max(widest, 10 + labelBlock.TextLayout.Width + 10);
                    continue;
                }

                // The key: its text the item's AcceleratorKey, right-aligned 10 before the item's
                // right edge, at least 24 right of the label, in the secondary colour or disabled.
                Assert.Equal(key.Text, AutomationProperties.GetAcceleratorKey(item));
                Assert.Equal("ContextMenuItemKey", AutomationProperties.GetAutomationId(key));
                Assert.Equal(MenuLayer.KeyName, key.Name);
                var itemBounds = app.BoundsOf(item);
                var keyText = TextBounds(app, key);
                Assert.Equal(itemBounds.Right - 10, keyText.Right, 2.0);
                Assert.True(keyText.Left - TextBounds(app, labelBlock).Right >= 24 - 0.5, $"{label}'s key lies at least 24 right of it.");
                Assert.Equal(Brush(app, item.IsEffectivelyEnabled ? "VcTextSecondaryBrush" : "VcTextDisabledBrush"), ((ISolidColorBrush)key.Foreground!).Color);
                Assert.Equal(12.5, key.FontSize);
                widest = Math.Max(widest, 10 + labelBlock.TextLayout.Width + 24 + key.TextLayout.Width + 10);
            }

            // The menu as wide as its widest item, the key included: the 4 of its border and padding
            // at each side; 239 at 1100×700 (Inter is the app's own font, so on every system).
            TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"The empty area's menu: {menu.Bounds.Width:F1} wide; its widest item needs {widest:F1}."));
            Assert.Equal(Math.Max(widest + 8, MenuLayer.MinMenuWidth), menu.Bounds.Width, 2.0);
            Assert.Equal(239, menu.Bounds.Width, 3.0);
            TestPictures.Save(app, $"wpk-menu-empty-area-{(theme == ThemeVariant.Dark ? "dark" : "light")}");
            app.PressKey(Avalonia.Input.Key.Escape);
            await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
        }
    }

    [AvaloniaFact]
    public async Task The_working_changes_and_the_checked_out_branchs_menus_carry_their_keys_and_another_branchs_none()
    {
        using var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nmore\n");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();

        // K11 step 4: the working-changes row's menu.
        app.RightClick(app.WorkingRowPoint());
        var menu = app.Menu();
        Assert.Equal(
            ["Working changes", "Stage all [Ctrl+Shift+A]", "Unstage all [Ctrl+Shift+X] (disabled: \"Nothing staged\")", "Discard all changes… (danger)", "—", "Stash changes… [Ctrl+Shift+S]", "Create branch here… [Ctrl+B]", "—", "Show reflog [Ctrl+Shift+R]", "Show backups", "Show in folder"],
            menu.LinesWithKeys());
        TestPictures.Save(app, "wpk-menu-working-changes");
        AssertMenuAsWideAsItsWidestItem(app, menu, 235);
        app.PressKey(Avalonia.Input.Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");

        // K12 step 18: main's menu by the left panel's row; feature/alpha's push is another branch's.
        app.RightClick(await app.RefRowAsync("Local branches", "main"));
        menu = app.Menu();
        Assert.Equal(
            [
                "main", "Pull from origin/main [Ctrl+Shift+L]", "Pull from origin/main (rebase)", "—", "Push main to origin/main [Ctrl+Shift+K]", "Force push main… (danger)", "—",
                "Create branch here…", "Create tag here…", "Rename main…", "Delete main… (disabled: \"You cannot delete the branch you are on\")", "—",
                "Change upstream…", "Stop tracking origin/main", "—", "Show reflog of main", "Hide main (disabled: \"The checked-out branch is always shown\")", "Solo main", "—", "Copy branch name",
            ],
            menu.LinesWithKeys());
        TestPictures.Save(app, "wpk-menu-main");
        AssertMenuAsWideAsItsWidestItem(app, menu, 289);
        app.PressKey(Avalonia.Input.Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");

        // K11 step 2: feature/alpha's menu by its label on row 4, another branch's: no item has a
        // key, its push included (6.7: only the checked-out branch's push and pull are the keys').
        app.RightClick(app.GraphLabelBounds(4, "feature/alpha").Center);
        menu = app.Menu();
        Assert.Equal("feature/alpha", menu.Header);
        Assert.Contains(menu.Items, item => item.Label == "Push feature/alpha to origin/feature/alpha");
        Assert.All(menu.Items, item => Assert.Null(item.KeyText));
        Assert.All(Items(app), item => Assert.Null(AutomationProperties.GetAcceleratorKey(item)));
        Assert.All(Items(app), item => Assert.Null(ShellDriver.MenuKeyBlockOf(item)));
        TestPictures.Save(app, "wpk-menu-feature-alpha");
        app.PressKey(Avalonia.Input.Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
    }

    [AvaloniaFact]
    public async Task A_label_cut_at_the_widest_menu_keeps_its_key_whole()
    {
        using var repo = await Scenarios.OperationsAsync();
        var name = "topic/" + new string('w', 70);
        await repo.GitAsync("switch", "--create", name);
        await repo.GitAsync("update-ref", $"refs/remotes/origin/{name}", "HEAD");
        await repo.GitAsync("config", $"branch.{name}.remote", "origin");
        await repo.GitAsync("config", $"branch.{name}.merge", $"refs/heads/{name}");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();
        var label = app.Window.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is ViewModels.Panels.RefItemRow { IsHead: true } && button.IsEffectivelyVisible);
        app.RightClick(label);
        var menu = app.Menu();
        Assert.Equal(MenuLayer.MaxMenuWidth, menu.Bounds.Width, 1.0);

        var push = Items(app).Single(item => (string)item.Header! == $"Push {name} to origin/{name}");
        var block = push.GetVisualDescendants().OfType<TrimmedTextBlock>().Single();
        var key = ShellDriver.MenuKeyBlockOf(push)!;
        Assert.True(block.IsTrimmed, "The label is cut with \"…\".");
        Assert.Equal($"Push {name} to origin/{name}", ToolTip.GetTip(block));
        Assert.Equal("Ctrl+Shift+K", key.Text);
        Assert.DoesNotContain(key.TextLayout.TextLines, line => line.HasCollapsed);
        Assert.Equal(app.BoundsOf(push).Right - 10, TextBounds(app, key).Right, 2.0);
        Assert.True(TextBounds(app, key).Left - app.BoundsOf(block).Right >= 24 - 0.5, "The cut label keeps 24 before the key.");
        TestPictures.Save(app, "wpk-menu-long-label");
        app.PressKey(Avalonia.Input.Key.Escape);
    }

    /// <summary>
    /// The open menu is as wide as its widest item needs, a keyed one's label with 24 and its key
    /// (6.7), and 4 of border and padding at each side; and <paramref name="width"/> ±3 at
    /// 1100×700, as measured (Inter is the app's own font, so on every system).
    /// </summary>
    private static void AssertMenuAsWideAsItsWidestItem(ShellDriver app, MenuSnapshot menu, double width)
    {
        var widest = Items(app).Max(item =>
        {
            var label = item.GetVisualDescendants().OfType<TrimmedTextBlock>().Single();
            var key = ShellDriver.MenuKeyBlockOf(item);
            return 10 + label.TextLayout.Width + (key is null ? 0 : 24 + key.TextLayout.Width) + 10;
        });
        TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"The menu \"{menu.Header}\": {menu.Bounds.Width:F1} wide; its widest item needs {widest:F1}."));
        Assert.Equal(Math.Max(widest + 8, MenuLayer.MinMenuWidth), menu.Bounds.Width, 2.0);
        Assert.Equal(width, menu.Bounds.Width, 3.0);
    }

    private static IEnumerable<Avalonia.Controls.MenuItem> Items(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<Avalonia.Controls.MenuItem>().Where(item => AutomationProperties.GetAutomationId(item) == MenuLayer.ItemAutomationId && item.IsEffectivelyVisible);

    /// <summary>Where a text block draws its text: its bounds at its text's width, right-aligned or left as it lies.</summary>
    private static Rect TextBounds(ShellDriver app, TextBlock block)
    {
        var bounds = app.BoundsOf(block);
        var width = block.TextLayout.WidthIncludingTrailingWhitespace;
        return block.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Right
            ? new Rect(bounds.Right - width, bounds.Y, width, bounds.Height)
            : new Rect(bounds.X, bounds.Y, Math.Min(width, bounds.Width), bounds.Height);
    }

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");
}
