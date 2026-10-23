// Check L32 of part L (docs/test-reports/phase-6.md): the long folder (design 13.2, D227; plan.md's
// "From phase 1"): the welcome page's error for a plain folder and the clone form's error for a
// missing URL, each drawn by PathTextBlock, its lines breaking only after a separator or at a space,
// both in both themes. The part's map is in Phase6Checks.L.cs.
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6Look;

public sealed partial class Phase6ChecksL
{
    /// <summary>
    /// Check L32: the welcome page's error for a plain folder and the clone form's error for a
    /// missing URL, each drawn line breaking only after a separator or at a space, nothing cut off,
    /// the texts unchanged; both themes (13.2, D227; LD20). Empty tab, System with the fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L32_long_folder_in_both_themes()
    {
        using var data = new TempDirectory("data");
        using var plain = new TempDirectory("plain");
        using var clones = new TempDirectory("clones");
        var url = new Uri(clones.Combine("does-not-exist")).AbsoluteUri;
        var cloneError = await CloneErrorTextAsync(url, clones.Path, data.Path);
        WriteSystemTheme(data);
        using var app = ShellDriver.Start(data.Path, PartL.Size.Width, PartL.Size.Height, systemDark: true);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        AssertSystemDarkStart(app);
        var welcome = app.Session.Shell.ActiveTab.Welcome;

        // Step 1: Open on a plain folder: WelcomeError.
        app.Folders.Answer(plain.Path);
        app.Click(app.Find<Button>("OpenRepoButton"));
        await app.WaitForAsync(() => welcome.HasError && app.Find<TextBlock>("WelcomeError").IsEffectivelyVisible, "the error of opening a plain folder");
        await ShotInBothThemesAsync(app, "l32a-not-a-repository", assertEach: (picture, palette) =>
        {
            var error = app.Find<TextBlock>("WelcomeError");
            var text = PartL.NotARepository(plain.Path);
            Assert.Equal(text, error.Text);
            Assert.Equal(text, ControlAutomationPeer.CreatePeerForElement(error).GetName());
            Assert.Equal(TextAlignment.Center, error.TextAlignment);
            AssertPathLines(DrawnLines.LinesOf(error), text, plain.Path, "WelcomeError");
            Assert.Contains(DrawnLines.Of(error), line => line.Contains(Path.GetFileName(plain.Path), StringComparison.Ordinal));
            AssertNoClippedText(app);
            var bounds = app.BoundsOf(error);
            Assert.True(bounds.Top >= app.BoundsOf(app.Find<Button>("OpenRepoButton")).Bottom - 0.5, "WelcomeError is not under the buttons' row.");
            Assert.True(bounds.Bottom <= app.BoundsOf(app.Find<TextBlock>("WorkspacesHeading")).Top + 0.5, "WelcomeError is not above the Workspaces section.");
            Assert.True(app.Find<TextBlock>("NoWorkspacesText").IsEffectivelyVisible, "No workspaces does not show.");
            AssertInk(picture, bounds, palette.Danger, palette.Window, "WelcomeError (VcDangerBrush, 11.10)");
            Assert.Equal([PartL.NewTab], app.Session.Shell.Tabs.Select(tab => tab.Title));
        });

        // Step 2: Clone with the missing URL: CloneError.
        app.Click(app.Find<Button>("CloneRepoButton"));
        await TypeCloneFormAsync(app, url, clones.Path, "cloned");
        app.Click(app.Find<Button>("CloneStartButton"));
        await app.WaitForAsync(() => !welcome.IsCloning && welcome.HasError && app.Find<TextBlock>("CloneError").IsEffectivelyVisible, "the failed clone's error", NetworkTimeout);
        var version = await MachineGitVersionAsync();
        var reportGit = version is { Major: 2, Minor: 36 or 55 };
        await ShotInBothThemesAsync(app, "l32b-clone-failed", assertEach: (picture, palette) =>
        {
            var urlBox = app.Find<TextBox>("CloneUrlBox");
            Assert.Equal(url, urlBox.Text);
            Assert.True(urlBox.IsEffectivelyEnabled, "The clone form is not enabled again.");
            var error = app.Find<TextBlock>("CloneError");
            Assert.Equal(cloneError, error.Text);
            var lines = cloneError.Split('\n');
            Assert.Contains(lines, line => line.StartsWith("fatal: ", StringComparison.Ordinal) && line.EndsWith(PartL.CloneKeyEnd, StringComparison.Ordinal));
            if (reportGit)
            {
                Assert.Equal(PartL.CloneErrorLines(GitPathOf(url)), lines);
            }

            Assert.True(error.TextAlignment is TextAlignment.Left or TextAlignment.Start, $"CloneError is aligned {error.TextAlignment}, not left.");
            AssertPathLines(DrawnLines.LinesOf(error), cloneError, GitPathOf(url), "CloneError");
            AssertNoClippedText(app);
            AssertInk(picture, app.BoundsOf(error), palette.Danger, palette.Window, "CloneError (VcDangerBrush, 11.10)");
            Assert.False(Directory.Exists(clones.Combine("cloned")), "The failed clone left a folder cloned.");
        });
    }

    /// <summary>The path git names for a <c>file:</c> URL: Windows' drive path with <c>/</c> separators, elsewhere the rooted path.</summary>
    private static string GitPathOf(string url)
    {
        var path = new Uri(url).LocalPath;
        return OperatingSystem.IsWindows() ? path.Replace('\\', '/') : path;
    }
}
