using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <c>VcLinkButton</c>'s keyboard frame (<c>Theme/Controls.axaml</c>; phase 8a's A1 step 9, design
/// 8.3.1): a link focused by the keyboard shows its whole text, its underline and Fluent's frame
/// around them, not over them, in About (dark and light) and in the sign-in dialog (GitLab's, with
/// both its links, 4 apart); the frame moves nothing, and a link not focused draws nothing outside
/// its bounds. The headless app, its pictures read with <see cref="LinkFrame"/>;
/// <see cref="TestPictures"/> saves them when asked.
/// </summary>
public class LinkFocusFrameTests
{
    /// <summary>
    /// How far the frame reaches outside the link's bounds: left and right 1 of air, Fluent's 1 and 2;
    /// above and below Fluent's 1 and 2, the line box keeping the text and its underline clear.
    /// </summary>
    private static readonly (int X, int Y) FrameReach = (4, 3);

    [AvaloniaTheory]
    [InlineData("AboutRepositoryLink", 3, true)]
    [InlineData("AboutGuideLink", 4, true)]
    [InlineData("AboutRepositoryLink", 3, false)]
    public async Task An_About_link_focused_by_Tab_shows_its_text_and_underline_inside_the_frame(string id, int tabs, bool dark)
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnAboutAsync(data, dark);
        var unfocused = app.Capture();

        for (var i = 0; i < tabs; i++)
        {
            app.PressKey(Key.Tab);
        }

        await WaitForFocusAsync(app, id);
        TestPictures.Save(app, $"link-frame-{id}-{(dark ? "dark" : "light")}");
        AssertFramed(app, unfocused, app.Capture(), Link(app, id), id);
    }

    [AvaloniaTheory]
    [InlineData("SignInLegacyTokenLink", 1)]
    [InlineData("SignInTokenLink", 2)]
    public async Task A_sign_in_link_focused_by_Shift_Tab_shows_its_text_and_underline_inside_the_frame(string id, int tabs)
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnGitLabSignInAsync(data);
        var unfocused = app.Capture();

        for (var i = 0; i < tabs; i++)
        {
            app.PressKey(Key.Tab, RawInputModifiers.Shift);
        }

        await WaitForFocusAsync(app, id);
        TestPictures.Save(app, $"link-frame-{id}");
        AssertFramed(app, unfocused, app.Capture(), Link(app, id), id);
    }

    [AvaloniaFact]
    public async Task The_frame_moves_nothing_and_a_link_draws_outside_its_bounds_only_while_focused()
    {
        using var data = new TempDirectory("data");
        using (var app = await StartOnAboutAsync(data, dark: true))
        {
            var section = app.FindByAutomationId(app.TopDialogView!, "SettingsAbout");
            var scroller = (ScrollViewer)app.FindByAutomationId(app.TopDialogView!, "SettingsSection");
            var places = Places(app, section);
            var extent = scroller.Extent;
            foreach (var id in new[] { "AboutRepositoryLink", "AboutGuideLink" })
            {
                AssertDrawsInside(app, Link(app, id), (0, 0), id + " without the focus");
            }

            app.PressKey(Key.Tab);
            app.PressKey(Key.Tab);
            foreach (var id in new[] { "AboutRepositoryLink", "AboutGuideLink" })
            {
                app.PressKey(Key.Tab);
                await WaitForFocusAsync(app, id);
                Assert.Equal(places, Places(app, section));
                Assert.Equal(extent, scroller.Extent);
                AssertDrawsInside(app, Link(app, id), FrameReach, id + " focused");
                AssertClearOfNeighbours(app, section, Link(app, id));
            }
        }

        using (var app = await StartOnGitLabSignInAsync(data))
        {
            // The focus on Cancel, out of the token box, whose caret blinks between two pictures.
            app.PressKey(Key.Tab);
            await WaitForFocusAsync(app, "CancelButton");
            var card = app.TopDialogView!.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "SignInCard");
            var places = Places(app, card);
            foreach (var id in new[] { "SignInTokenLink", "SignInLegacyTokenLink" })
            {
                AssertDrawsInside(app, Link(app, id), (0, 0), id + " without the focus");
            }

            app.PressKey(Key.Tab, RawInputModifiers.Shift);
            await WaitForFocusAsync(app, "SignInToken");
            foreach (var id in new[] { "SignInLegacyTokenLink", "SignInTokenLink" })
            {
                app.PressKey(Key.Tab, RawInputModifiers.Shift);
                await WaitForFocusAsync(app, id);
                Assert.Equal(places, Places(app, card));
                AssertDrawsInside(app, Link(app, id), FrameReach, id + " focused");
                AssertClearOfNeighbours(app, card, Link(app, id));
            }
        }
    }

    /// <summary>The app on the settings screen's About, its reads ended, the focus on its section button, the pointer at rest.</summary>
    private static async Task<ShellDriver> StartOnAboutAsync(TempDirectory data, bool dark)
    {
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = dark ? AppTheme.Dark : AppTheme.Light });
        var app = ShellDriver.Start(data.Path);
        try
        {
            await app.WaitUntilReadyAsync();
            Assert.Equal(dark, app.ShowsDarkTheme);
            app.Session.Shell.OpenSettingsCommand.Execute(SettingsSection.About);
            await app.WaitForAsync(() => app.SettingsDialog is { ShowsAbout: true }, "the settings screen on About");
            await app.WaitForSettingsReadsAsync();
            await WaitForFocusAsync(app, "SettingsNavAbout");
            app.MoveToNeutral();
            app.Settle();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>"Add GitLab account" over the empty tab, dark, on a session that makes no request; the focus in its token box, the pointer at rest.</summary>
    private static async Task<ShellDriver> StartOnGitLabSignInAsync(TempDirectory data)
    {
        var app = ShellDriver.Start(data.Path);
        try
        {
            await app.WaitUntilReadyAsync();
            var hosting = new HostingSessionStub(new GitCoverage(new AccountKey(HostingProvider.GitLab, "gitlab.example.com", "ada"), new Uri("https://gitlab.example.com/")), null);
            var dialog = new SignInDialogViewModel(hosting, HostingProvider.GitLab, app.Launcher, app.Session.Log);
            _ = app.Session.Services.DialogHost.ShowAsync(dialog);
            await dialog.Opened;
            await app.WaitForAsync(() => app.TopDialogView is SignInDialogView && app.FocusedAutomationId() == "SignInToken", "the sign-in dialog with the focus in its token box");
            Assert.True(Link(app, "SignInLegacyTokenLink").IsEffectivelyVisible, "GitLab's legacy link does not show.");
            app.MoveToNeutral();
            app.Settle();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static Task WaitForFocusAsync(ShellDriver app, string id) =>
        app.WaitForAsync(() => app.FocusedAutomationId() == id, $"the keyboard focus on {id} (it is on {app.FocusedAutomationId() ?? "nothing"})");

    private static Button Link(ShellDriver app, string id) => (Button)app.FindByAutomationId(app.TopDialogView!, id);

    /// <summary>
    /// The focused link (<c>:focus-visible</c>, not hovered) in <paramref name="focused"/> against itself
    /// in <paramref name="unfocused"/>, by <see cref="LinkFrame"/>: the text's ink all there, the
    /// underline under it, the frame's outer line whole around them in Fluent's colour of the theme.
    /// </summary>
    private static void AssertFramed(ShellDriver app, Screenshot unfocused, Screenshot focused, Button link, string id)
    {
        Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame.");
        Assert.False(link.IsPointerOver, $"{id} is hovered.");
        Assert.Same(Application.Current!.FindResource("VcLinkButton"), link.Theme);
        var text = link.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(text.TextDecorations is { Count: > 0 }, $"{id} is not underlined while focused.");
        var accent = Colour(app, "VcAccentBrush");
        Assert.Equal(accent, Assert.IsAssignableFrom<ISolidColorBrush>(text.Foreground).Color);
        var problems = LinkFrame.Problems(unfocused, focused, app.BoundsOf(text), accent, Colour(app, "VcPanelBackgroundBrush"), Colour(app, "SystemControlFocusVisualPrimaryBrush"), id);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>
    /// What <paramref name="link"/> draws, the picture with it against the picture with its opacity 0
    /// (which moves nothing): inside its bounds when <paramref name="reach"/> is none; with the frame,
    /// exactly its bounds grown by <paramref name="reach"/> left and right and above and below.
    /// </summary>
    private static void AssertDrawsInside(ShellDriver app, Button link, (int X, int Y) reach, string what)
    {
        var bounds = app.BoundsOf(link);
        var pixels = new Box((int)Math.Floor(bounds.Left), (int)Math.Floor(bounds.Top), (int)Math.Ceiling(bounds.Right), (int)Math.Ceiling(bounds.Bottom));
        var drawn = DrawnBy(app, link);
        Assert.False(drawn is null, $"{what} draws nothing.");
        if (reach == (0, 0))
        {
            Assert.True(pixels.Contains(drawn!), Text($"{what} draws in {drawn}, outside its bounds {pixels}."));
        }
        else
        {
            Assert.True(drawn == pixels.Grown(reach), Text($"{what} draws in {drawn}, not exactly its bounds {pixels} grown by {reach.X} left and right and {reach.Y} above and below, {pixels.Grown(reach)}."));
        }
    }

    /// <summary>
    /// The focused link's frame (its bounds grown by <see cref="FrameReach"/>) meets no other control of
    /// <paramref name="scope"/> that is neither its ancestor nor its descendant, nor the frame's borders
    /// of another link, which draw nothing while it has no focus.
    /// </summary>
    private static void AssertClearOfNeighbours(ShellDriver app, Visual scope, Button link)
    {
        var frame = app.BoundsOf(link).Inflate(new Thickness(FrameReach.X, FrameReach.Y));
        var neighbours = scope.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0 && control.Bounds.Height > 0)
            .Where(control => !control.GetSelfAndVisualAncestors().Contains(link) && !link.GetVisualAncestors().Contains(control))
            .Where(control => control is not Border { Name: "FocusFrame" or "FocusFrameInner" } || control.TemplatedParent is not Button { } other || !ReferenceEquals(other.Theme, link.Theme))
            .Where(control => app.BoundsOf(control).Intersects(frame))
            .Select(control => Text($"{control.GetType().Name} {control.Name ?? AutomationProperties.GetAutomationId(control)} at {app.BoundsOf(control)}"))
            .ToList();
        Assert.True(neighbours.Count == 0, Text($"The frame of {AutomationProperties.GetAutomationId(link)} at {frame} meets {string.Join("; ", neighbours)}."));
    }

    /// <summary>The bounding box of the pixels that change when <paramref name="control"/>'s opacity is 0; null when none do.</summary>
    private static Box? DrawnBy(ShellDriver app, Control control)
    {
        var with = app.Capture();
        control.Opacity = 0;
        Screenshot without;
        try
        {
            without = app.Capture();
        }
        finally
        {
            control.Opacity = 1;
            app.Settle();
        }

        var (left, top, right, bottom) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        for (var y = 0; y < with.Height; y++)
        {
            for (var x = 0; x < with.Width; x++)
            {
                if (with.PixelAt(x, y) != without.PixelAt(x, y))
                {
                    (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x + 1), Math.Max(bottom, y + 1));
                }
            }
        }

        return left == int.MaxValue ? null : new Box(left, top, right, bottom);
    }

    /// <summary>The place of every shown control in <paramref name="scope"/>, in the window, in tree order.</summary>
    private static IReadOnlyList<string> Places(ShellDriver app, Visual scope) =>
        [.. scope.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible).Select(control => Text($"{control.GetType().Name} {control.Name} {app.BoundsOf(control)}"))];

    private static Color Colour(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>A box of whole pixels, its right and bottom exclusive.</summary>
    private sealed record Box(int Left, int Top, int Right, int Bottom)
    {
        public bool Contains(Box other) => other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

        public Box Grown((int X, int Y) by) => new(Left - by.X, Top - by.Y, Right + by.X, Bottom + by.Y);

        public override string ToString() => Text($"({Left}, {Top})-({Right}, {Bottom})");
    }
}
