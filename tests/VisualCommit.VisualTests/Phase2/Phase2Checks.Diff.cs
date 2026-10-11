using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase0;
using VisualCommit.VisualTests.Phase1;
using Xunit;

namespace VisualCommit.VisualTests.Phase2;

/// <summary>Phase 2's checks of the diff view.</summary>
public partial class Phase2Checks
{
    [AvaloniaFact]
    public async Task Check_05_image_diff()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        await OpenDiffAsync(app, "UnstagedFileList", "assets/logo.png", DiffBody.Image);
        await app.WaitForAsync(() => app.Find<TextBlock>("AfterCaption").Text?.Contains("pixels", StringComparison.Ordinal) == true, "the images' captions");
        app.MoveMouse(new Point(130, 700 - 13));
        var shot = app.Capture();
        shot.Save(Phase, "05-image-diff");

        AssertDiffHeader(app, "M", "logo.png", "assets", "Unstaged");
        Assert.False(app.Find<Button>("InlineDiffButton").IsEffectivelyVisible);
        Assert.False(app.Find<Button>("SideBySideDiffButton").IsEffectivelyVisible);
        Assert.True(app.Find<Button>("StageFileButton").IsEffectivelyVisible);
        Assert.True(app.Find<Button>("DiscardFileButton").IsEffectivelyVisible);
        Assert.Equal("48 × 48 pixels, 7,028 bytes", app.Find<TextBlock>("BeforeCaption").Text);
        Assert.Equal("64 × 48 pixels, 9,332 bytes", app.Find<TextBlock>("AfterCaption").Text);

        var before = app.BoundsOf(app.Find<Image>("BeforeImage"));
        var after = app.BoundsOf(app.Find<Image>("AfterImage"));
        Assert.Equal(new Size(48, 48), before.Size);
        Assert.Equal(new Size(64, 48), after.Size);
        var white = Color.FromRgb(255, 255, 255);
        ShellExpectations.AssertColour(white, shot, before.Center, "the middle of the image before");
        ShellExpectations.AssertColour(white, shot, after.Center, "the middle of the image after");
        ShellExpectations.AssertColour(Color.Parse("#4353D8"), shot, new Point(before.X + 4, before.Center.Y), "the image before, 4 inside its left edge");
        ShellExpectations.AssertColour(Color.Parse("#1E8E5A"), shot, new Point(after.X + 4, after.Center.Y), "the image after, 4 inside its left edge");
        Assert.True(app.BoundsOf(app.Find<Image>("BeforeImage")).Right <= app.BoundsOf(app.FindByAutomationId("DiffView")).Center.X);
        Assert.True(app.BoundsOf(app.Find<Image>("AfterImage")).Left >= app.BoundsOf(app.FindByAutomationId("DiffView")).Center.X);
        AssertNoClippedText(app);
    }

    [AvaloniaFact]
    public async Task Check_06_binary_file()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, commits: 2);
        app.Click(WorkingRowPoint(app));
        await WaitForStagePanelAsync(app, unstaged: 7, staged: 3);

        await OpenDiffAsync(app, "UnstagedFileList", "data/blob.bin", DiffBody.Binary);
        app.MoveMouse(new Point(130, 700 - 13));
        app.Capture().Save(Phase, "06-binary-file");

        AssertDiffHeader(app, "M", "blob.bin", "data", "Unstaged");
        Assert.False(app.Find<Button>("InlineDiffButton").IsEffectivelyVisible);
        Assert.Equal(["Binary file", "Before: 256 bytes", "After: 320 bytes"], VisibleTexts(app, "DiffView").Where(text => text is "Binary file" || text.StartsWith("Before:", StringComparison.Ordinal) || text.StartsWith("After:", StringComparison.Ordinal)));
        AssertNoClippedText(app);
    }

    /// <summary>Clicks a file's row in a stage list and waits until its diff shows the given body.</summary>
    private static async Task OpenDiffAsync(ShellDriver app, string list, string path, DiffBody body)
    {
        var row = await ScrollToFileRowAsync(app, list, path);
        app.Click(row);
        await app.WaitForAsync(
            () => app.Session.Shell.ActiveTab.Repository is { Diff: { } diff } && diff.Target.Path == path && diff.Body == body && app.FindByAutomationId("DiffView").IsEffectivelyVisible,
            $"the diff of {path} ({body})");
        app.Settle();
    }

    /// <summary>The diff view's header: the status letter, the file's name and folder, and what is compared.</summary>
    private static void AssertDiffHeader(ShellDriver app, string letter, string name, string folder, string side)
    {
        Assert.Equal(letter, app.Find<TextBlock>("DiffStatus").Text);
        Assert.Equal(name, app.Find<TextBlock>("DiffFileName").Text);
        Assert.Equal(folder, app.Find<TextBlock>("DiffFolder").Text);
        Assert.Equal(folder.Length > 0, app.Find<TextBlock>("DiffFolder").IsEffectivelyVisible);
        Assert.Equal(side, app.Find<TextBlock>("DiffSideLabel").Text);
        Assert.True(app.Find<Button>("CloseDiffButton").IsEffectivelyVisible);
        var view = app.BoundsOf(app.FindByAutomationId("DiffView"));
        var graphArea = app.BoundsOf(app.FindByAutomationId("CommitGraph"));
        Assert.Equal(graphArea, view);
        Assert.False(app.Find<Control>("GraphRows").IsEffectivelyVisible);
    }

    /// <summary>The texts shown in a region, in visual order, without duplicates of hidden ones.</summary>
    private static List<string> VisibleTexts(ShellDriver app, string automationId) =>
        [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(app.FindByAutomationId(automationId)).OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
            .Select(text => text.Text!)];
}
