// Part D's check of the light theme at 1920×1080 (docs/test-reports/phase-3.md, check 27): a
// menu, the reset dialog, the conflict banner and the error dialog in the light tokens.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksD
{
    /// <summary>
    /// Check 27: ops in the light theme at 1920×1080: the commit menu of "Alpha two", the reset
    /// dialog with Hard chosen, the conflict banner of a merge, and the error dialog of a checkout
    /// during the merge, all six graph columns shown.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_27_light_theme_at_1920x1080()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, FullSize.Width, FullSize.Height, AppTheme.Light);
        var palette = Light;
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        Assert.Equal(new Size(FullSize.Width, FullSize.Height), app.Window.ClientSize);
        var graph = Graph(app);
        Assert.True(graph.Columns.ShowsAuthor && graph.Columns.ShowsDate && graph.Columns.ShowsSha, "Not all six graph columns show.");
        Assert.Equal(["Branch / Tag", "Graph", "Message", "Author", "Date", "SHA"], TextsIn(app.Find<Border>("ColumnHeaders")));
        AssertGraph(app, PartD.OpsGraph, lanes: 3, showsDate: true);
        var head = await repo.HeadAsync();

        // Step 1: the commit menu of "Alpha two".
        var pointer = app.GraphRowPoint(PartD.AlphaTwoRow);
        app.RightClick(pointer);
        var menu = app.Menu();
        await app.WaitForAsync(() => TextsIn(app.FindByAutomationId("RightPanel")).Contains("Alpha two"), "the details of \"Alpha two\"");
        var shot = Shot(app, "27a-menu-light");
        Assert.Equal(PartD.AlphaTwoRow, graph.SelectedIndex);
        AssertMenu(menu, PartD.AlphaTwoMenu);
        Assert.All(menu.Items, item => Assert.True(item.IsEnabled && !item.IsDestructive, $"\"{item.Label}\" is not an enabled ordinary item."));
        AssertHighlighted(menu, "Check out this commit");
        Assert.Equal(PartD.AlphaTwoMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuShape(app, menu);
        AssertMenuAtPointer(menu, pointer);
        AssertLightMenu(app, menu, shot, palette);

        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 2: the reset dialog of "Add core", Hard chosen; then Cancel.
        app.RightClick(app.GraphRowPoint(PartD.AddCoreRow));
        app.ClickMenuItem("Reset main to this commit…");
        await app.WaitForDialogAsync<ResetDialogViewModel>();
        ClickId(app, "ResetHard");
        shot = Shot(app, "27b-reset-hard-light");
        AssertCard(app, "ResetDialogCard", 400);
        Assert.Equal(PartD.ResetTitle, DialogText(app, "ResetTitle"));
        Assert.Equal(PartD.ResetText, DialogText(app, "ResetText"));
        var view = app.TopDialogView!;
        foreach (var (mode, text) in PartD.ResetModes)
        {
            var radio = (RadioButton)app.FindByAutomationId(view, "Reset" + mode);
            Assert.Equal(mode == "Hard", radio.IsChecked == true);
            Assert.Contains(radio.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == mode);
            var description = radio.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == text);
            Assert.Equal(mode == "Hard" ? palette.Danger : palette.TextSecondary, (description.Foreground as ISolidColorBrush)?.Color);
        }

        Assert.Equal(PartD.BackupDetail, DialogText(app, "ConfirmDetail"));
        Assert.Equal(["Cancel", "Reset"], DialogButtons(app));
        var reset = DialogButton(app, "ConfirmButton");
        Assert.Equal(palette.Danger, (reset.Background as ISolidColorBrush)?.Color);
        Assert.Equal(Colors.White, (reset.Foreground as ISolidColorBrush)?.Color);
        AssertColour(palette.Danger, shot, app.BoundsOf(reset).TopLeft + new Point(4, 4), "Reset button's background");
        AssertColour(BackdropOver(palette.Window, palette.Backdrop), shot, new Point(700, 1000), "backdrop over the graph's empty part");
        AssertNoClippedText(app);
        ClickId(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Empty(await repo.BackupRefsAsync());

        // Step 3: the merge of feature/conflict stops; the banner.
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        await app.WaitForAsync(() => app.RefRowModel("Local branches", "feature").IsOpen, "the folder feature to open");
        await RightClickRowAsync(app, "Local branches", "feature", "conflict");
        app.ClickMenuItem("Merge feature/conflict into main");
        await WaitForResultAsync(app, PartD.MergeStopped);
        shot = Shot(app, "27c-banner-light");
        AssertStatus(app, PartD.MergeStopped, OperationStatusTone.Warning);
        Assert.Equal(Color.Parse("#B7791F"), palette.Warning);
        await AssertNoDialogAsync(app);
        var banner = AssertBanner(app, PartD.MergeBanner, PartD.AbortMerge, oneLine: true);
        var bannerControl = app.Window.GetVisualDescendants().OfType<Control>().Single(control => AutomationProperties.GetAutomationId(control) == "ConflictBanner");
        var bannerBorder = bannerControl.GetVisualDescendants().OfType<Border>().First();
        Assert.Equal(palette.BannerBackground, (bannerBorder.Background as ISolidColorBrush)?.Color);
        Assert.Equal(palette.Warning, (bannerBorder.BorderBrush as ISolidColorBrush)?.Color);
        Assert.Equal(new Thickness(0, 0, 0, 1), bannerBorder.BorderThickness);
        AssertColour(palette.BannerBackground, shot, new Point(banner.Right - 220, banner.Top + 3), "banner's background");
        AssertColour(palette.Warning, shot, new Point(banner.Right - 220, banner.Bottom - 0.5), "banner's bottom line");
        Assert.True(bannerControl.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "BannerIcon").IsEffectivelyVisible, "The alert icon is hidden.");
        Assert.True(banner.Bottom <= app.BoundsOf(app.Find<Border>("ColumnHeaders")).Top + 0.5, "The banner is not above the column headers.");
        Assert.True(graph.ShowsWorkingRow, "The working-changes row is not shown.");
        Assert.Equal(PartD.WorkingRowConflict, graph.WorkingRowText);
        Assert.Contains("UU src/core.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')));
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), "MERGE_HEAD is missing.");
        Assert.Equal("Merge branch 'feature/conflict'", File.ReadAllLines(Path.Combine(repo.Path, ".git", "MERGE_MSG"))[0]);
        AssertNoClippedText(app);

        // Step 4: a checkout during the merge fails.
        app.DoubleClick(await app.RefRowAsync("Local branches", "feature", "alpha"));
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        Shot(app, "27d-error-light");
        await AssertErrorDialogAsync(app, Expected(PartD.CheckoutDuringMergeDialog));
        Assert.NotNull(app.BannerBounds);
        AssertStatus(app, PartD.CheckoutAlphaFailed, OperationStatusTone.Danger);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), "The merge is no longer in progress.");
        AssertNoClippedText(app);
        ClickId(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
    }

    /// <summary>
    /// Check 27 step 1's colours of the open menu in the light theme: its background, border and
    /// shadow (brushes, and the background by pixel), the items' text, the header's, and the
    /// highlighted item's background by pixel.
    /// </summary>
    private static void AssertLightMenu(ShellDriver app, MenuSnapshot menu, Screenshot shot, Palette palette)
    {
        var presenter = (MenuFlyoutPresenter)(app.OpenMenuView ?? throw new Xunit.Sdk.XunitException("No menu is open."));
        Assert.Equal(palette.Panel, (presenter.Background as ISolidColorBrush)?.Color);
        Assert.Equal(palette.Border, (presenter.BorderBrush as ISolidColorBrush)?.Color);
        var root = presenter.GetVisualDescendants().OfType<Border>().First(border => border.Name == "LayoutRoot");
        Assert.Equal(BoxShadows.Parse(palette.MenuShadow), root.BoxShadow);
        Assert.All(menu.Items, item => Assert.Equal(palette.TextPrimary, item.TextColor));
        var header = presenter.GetVisualDescendants().OfType<MenuItem>().Single(item => item.Classes.Contains("vcMenuHeader"));
        Assert.Equal(palette.TextSecondary, (header.Foreground as ISolidColorBrush)?.Color);
        AssertColour(palette.Panel, shot, new Point(menu.Bounds.Right - 20, menu.Bounds.Top + 16), "menu's background (the header row's right end)");
        var highlighted = menu.Highlighted!.Bounds;
        AssertColour(palette.Hover, shot, new Point(highlighted.Left + 4, highlighted.Center.Y), "highlighted item's background");
    }

    /// <summary>What <paramref name="backdrop"/> drawn over <paramref name="under"/> looks like.</summary>
    private static Color BackdropOver(Color under, Color backdrop)
    {
        var alpha = backdrop.A / 255.0;
        byte Blend(byte below, byte above) => (byte)Math.Round((above * alpha) + (below * (1 - alpha)));
        return Color.FromRgb(Blend(under.R, backdrop.R), Blend(under.G, backdrop.G), Blend(under.B, backdrop.B));
    }
}
