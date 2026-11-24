using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
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
/// both its links, 4 apart); the frame moves nothing, a link not focused draws nothing outside its
/// bounds, and the frame takes no pointer: a hover or a click on it is none on the link; and the
/// frame is the keyboard's alone: a link focused by a click is underlined without it; and the
/// frame keeps clear of the text at every display scaling from 1 to 2 in steps of 0.25. The
/// headless app, its pictures read with <see cref="LinkFrame"/>;
/// <see cref="TestPictures"/> saves them when asked.
/// </summary>
public class LinkFocusFrameTests
{
    /// <summary>
    /// How far the frame reaches outside the link's bounds on each side: 1 of air, Fluent's 1 and 2.
    /// Above and below, where the line box keeps the text and its underline 2 and 1 px clear at a
    /// scaling of 1, the air is no less: with 3 the frame's lines lay on the text's first and last
    /// rows at 1.5 and touched them at 1.75, the 3 px rounding to 4 and 5 device pixels there and
    /// the 2 and 1 px lines to 3 + 2 and 4 + 2.
    /// </summary>
    private static readonly (int X, int Y) FrameReach = (4, 4);

    /// <summary>Windows' display scalings from 100 % to 200 %.</summary>
    private static readonly double[] Scalings = [1, 1.25, 1.5, 1.75, 2];

    /// <summary>About's two links, each with the Tabs from the section's button that focus it, at each of <see cref="Scalings"/>.</summary>
    public static TheoryData<string, int, double> AboutLinksScaled => Scaled(("AboutRepositoryLink", 3), ("AboutGuideLink", 4));

    /// <summary>GitLab's sign-in links, each with the Shift+Tabs from the token box that focus it, at each of <see cref="Scalings"/>.</summary>
    public static TheoryData<string, int, double> SignInLinksScaled => Scaled(("SignInLegacyTokenLink", 1), ("SignInTokenLink", 2));

    /// <summary>
    /// About's links focused by Tab at every scaling from 1 to 2 in steps of 0.25, dark (the card's
    /// background, Fluent's white frame): by <see cref="LinkFrame"/>'s rule in the window's device
    /// pixels, the text's ink all there, the underline under it and the frame's outer line whole
    /// around them, with a line of the background between the frame and the ink on each side. About's
    /// guide link is the one part G's G3 frames.
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(AboutLinksScaled))]
    public async Task An_About_link_keeps_its_frame_clear_of_its_text_at_every_scaling(string id, int tabs, double scale)
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnAboutAsync(data, dark: true, scale);
        var unfocused = app.Capture();

        for (var i = 0; i < tabs; i++)
        {
            app.PressKey(Key.Tab);
        }

        await WaitForFocusAsync(app, id);
        TestPictures.Save(app, Text($"link-frame-{id}-{scale:0.00}"));
        AssertFramed(app, unfocused, app.Capture(), Link(app, id), id, scale);
    }

    /// <summary>GitLab's sign-in links, 4 apart, focused by Shift+Tab, at the same scalings, as About's.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(SignInLinksScaled))]
    public async Task A_sign_in_link_keeps_its_frame_clear_of_its_text_at_every_scaling(string id, int tabs, double scale)
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnGitLabSignInAsync(data, scale);
        var unfocused = app.Capture();

        for (var i = 0; i < tabs; i++)
        {
            app.PressKey(Key.Tab, RawInputModifiers.Shift);
        }

        await WaitForFocusAsync(app, id);
        TestPictures.Save(app, Text($"link-frame-{id}-{scale:0.00}"));
        AssertFramed(app, unfocused, app.Capture(), Link(app, id), id, scale);
    }

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

    /// <summary>
    /// The frame is drawn, never hit: while a link has the keyboard's frame, every pixel of the
    /// frame's ring (its bounds grown by <see cref="FrameReach"/>, less its bounds) hits what it hits
    /// without the focus, nothing of the link; and the pointer on the frame's outer or inner line
    /// neither hovers the link nor presses it: a click there asks for no address. A click just outside
    /// the text is no click on the link, focused or not (a frame that took the pointer opened the
    /// repository's address on a click 3.5 left of the link's bounds).
    /// </summary>
    [AvaloniaFact]
    public async Task A_pointer_on_the_frame_reaches_what_lies_under_it_not_the_link()
    {
        using var data = new TempDirectory("data");
        using (var app = await StartOnAboutAsync(data, dark: true))
        {
            string[] ids = ["AboutRepositoryLink", "AboutGuideLink"];
            var unfocused = ids.ToDictionary(id => id, id => HitsOnRing(app, Link(app, id)));
            app.PressKey(Key.Tab);
            app.PressKey(Key.Tab);
            foreach (var id in ids)
            {
                app.PressKey(Key.Tab);
                await WaitForFocusAsync(app, id);
                AssertRingHitsAsWithoutFocus(app, Link(app, id), unfocused[id], id);
            }

            AssertPointerOnFramePressesNothing(app, Link(app, "AboutRepositoryLink"), "AboutRepositoryLink");
        }

        using (var app = await StartOnGitLabSignInAsync(data))
        {
            string[] ids = ["SignInLegacyTokenLink", "SignInTokenLink"];
            var unfocused = ids.ToDictionary(id => id, id => HitsOnRing(app, Link(app, id)));
            foreach (var id in ids)
            {
                app.PressKey(Key.Tab, RawInputModifiers.Shift);
                await WaitForFocusAsync(app, id);
                AssertRingHitsAsWithoutFocus(app, Link(app, id), unfocused[id], id);
            }

            AssertPointerOnFramePressesNothing(app, Link(app, "SignInLegacyTokenLink"), "SignInLegacyTokenLink");
        }
    }

    /// <summary>
    /// The frame is the keyboard's alone (<c>:focus-visible</c>; part K's conventions): an About link
    /// focused by a click opens its address once and is underlined, as while hovered, but shows no
    /// frame, so nothing of its frame's ring changes against the picture before the click and it draws
    /// nothing outside its bounds (a frame under <c>:focus</c> drew 870 pixels of the guide link's ring
    /// after a click).
    /// </summary>
    [AvaloniaTheory]
    [InlineData("AboutRepositoryLink", true)]
    [InlineData("AboutGuideLink", true)]
    [InlineData("AboutGuideLink", false)]
    public async Task An_About_link_focused_by_a_click_is_underlined_without_the_frame(string id, bool dark)
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnAboutAsync(data, dark);
        var unfocused = app.Capture();
        var requests = app.Launcher.Requests.Count;

        app.Click(Link(app, id));
        await app.WaitForAsync(() => app.Launcher.Requests.Count > requests, $"the address of {id} asked for");
        app.MoveToNeutral();
        app.Settle();

        TestPictures.Save(app, $"link-clicked-{id}-{(dark ? "dark" : "light")}");
        AssertFocusedByPointerWithoutFrame(app, unfocused, Link(app, id), id);
        Assert.Single(app.Launcher.Requests.Skip(requests));
    }

    /// <summary>
    /// The same in GitLab's sign-in dialog, whose two links lie 4 apart: a link focused by a click is
    /// underlined and shows no frame. The dialog's session knows no provider, so the click asks for
    /// no address.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("SignInTokenLink")]
    [InlineData("SignInLegacyTokenLink")]
    public async Task A_sign_in_link_focused_by_a_click_is_underlined_without_the_frame(string id)
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnGitLabSignInAsync(data);
        var unfocused = app.Capture();

        app.Click(Link(app, id));
        await WaitForFocusAsync(app, id);
        app.MoveToNeutral();
        app.Settle();

        TestPictures.Save(app, $"link-clicked-{id}");
        AssertFocusedByPointerWithoutFrame(app, unfocused, Link(app, id), id);
    }

    /// <summary>Each of <paramref name="links"/> at each of <see cref="Scalings"/>.</summary>
    private static TheoryData<string, int, double> Scaled(params (string Id, int Presses)[] links)
    {
        var data = new TheoryData<string, int, double>();
        foreach (var (id, presses) in links)
        {
            foreach (var scale in Scalings)
            {
                data.Add(id, presses, scale);
            }
        }

        return data;
    }

    /// <summary>
    /// The app started on <paramref name="data"/>, its window's render scaling set to
    /// <paramref name="scale"/> before the pointer first moves (set later, the scaling re-reads the
    /// pointer's last place in the new scaling's pixels and hovers what lies there).
    /// </summary>
    private static ShellDriver StartScaled(TempDirectory data, double scale)
    {
        var app = ShellDriver.Start(data.Path);
        if (scale != 1)
        {
            app.Window.SetRenderScaling(scale);
            app.Settle();
        }

        return app;
    }

    /// <summary>The app on the settings screen's About, its reads ended, the focus on its section button, the pointer at rest.</summary>
    private static async Task<ShellDriver> StartOnAboutAsync(TempDirectory data, bool dark, double scale = 1)
    {
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = dark ? AppTheme.Dark : AppTheme.Light });
        var app = StartScaled(data, scale);
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
    private static async Task<ShellDriver> StartOnGitLabSignInAsync(TempDirectory data, double scale = 1)
    {
        var app = StartScaled(data, scale);
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
    /// The pictures are the window's at its render scaling <paramref name="scale"/>, whose device
    /// pixels the text's bounds are read in.
    /// </summary>
    private static void AssertFramed(ShellDriver app, Screenshot unfocused, Screenshot focused, Button link, string id, double scale = 1)
    {
        Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame.");
        Assert.False(link.IsPointerOver, $"{id} is hovered.");
        Assert.Same(Application.Current!.FindResource("VcLinkButton"), link.Theme);
        Assert.True(
            focused.Width == (int)Math.Round(app.Window.ClientSize.Width * scale) && focused.Height == (int)Math.Round(app.Window.ClientSize.Height * scale),
            Text($"The picture is {focused.Width} x {focused.Height}, not the window's {app.Window.ClientSize} at a scaling of {scale}."));
        var text = link.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(text.TextDecorations is { Count: > 0 }, $"{id} is not underlined while focused.");
        var accent = Colour(app, "VcAccentBrush");
        Assert.Equal(accent, Assert.IsAssignableFrom<ISolidColorBrush>(text.Foreground).Color);
        var problems = LinkFrame.Problems(unfocused, focused, InDevicePixels(app.BoundsOf(text), scale), accent, Colour(app, "VcPanelBackgroundBrush"), Colour(app, "SystemControlFocusVisualPrimaryBrush"), id, scale);
        Assert.True(problems.Count == 0, Text($"At a scaling of {scale}:\n") + string.Join('\n', problems));
    }

    /// <summary>
    /// <paramref name="bounds"/>, in the window's logical pixels, in its device pixels at
    /// <paramref name="scale"/>; an edge that lies within a thousandth of a whole pixel there is that
    /// pixel (the layout's rounding puts edges on whole device pixels, which the product misses by the
    /// floating point's error: 372.571… × 1.75 is 652.0000000000001).
    /// </summary>
    private static Rect InDevicePixels(Rect bounds, double scale)
    {
        static double Snap(double value) => Math.Abs(value - Math.Round(value)) < 0.001 ? Math.Round(value) : value;
        var (left, top, right, bottom) = (Snap(bounds.Left * scale), Snap(bounds.Top * scale), Snap(bounds.Right * scale), Snap(bounds.Bottom * scale));
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// <paramref name="link"/>, just clicked, the pointer then moved away: it has the focus without the
    /// keyboard's frame (not <c>:focus-visible</c>) and is not hovered; its text is underlined and the
    /// underline drawn inside its bounds; every pixel of its frame's ring (its bounds grown by
    /// <see cref="FrameReach"/>, less its bounds) is as in <paramref name="unfocused"/>, the picture
    /// before the click; and it draws nothing outside its bounds.
    /// </summary>
    private static void AssertFocusedByPointerWithoutFrame(ShellDriver app, Screenshot unfocused, Button link, string id)
    {
        Assert.True(app.FocusedAutomationId() == id, Text($"A click on {id} leaves the focus on {app.FocusedAutomationId() ?? "nothing"}."));
        Assert.False(app.FocusVisible, $"{id} focused by a click has the keyboard's frame (:focus-visible).");
        Assert.False(link.IsPointerOver, $"{id} is hovered.");
        Assert.Same(Application.Current!.FindResource("VcLinkButton"), link.Theme);
        var text = link.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.True(text.TextDecorations is { Count: > 0 }, $"{id} focused by a click is not underlined.");

        var focused = app.Capture();
        var bounds = PixelBox(app, link);
        var ring = bounds.Grown(FrameReach);
        var inside = new List<(int X, int Y)>();
        var outside = new List<(int X, int Y)>();
        for (var y = ring.Top; y < ring.Bottom; y++)
        {
            for (var x = ring.Left; x < ring.Right; x++)
            {
                if (focused.PixelAt(x, y) != unfocused.PixelAt(x, y))
                {
                    (bounds.Contains(x, y) ? inside : outside).Add((x, y));
                }
            }
        }

        if (outside.Count > 0)
        {
            var (x, y) = outside[0];
            Assert.Fail(Text($"{id} focused by a click changes {outside.Count} pixels of its frame's ring at {ring} against the picture before the click, the first at ({x}, {y}): {Hex(unfocused.PixelAt(x, y))} became {Hex(focused.PixelAt(x, y))}; a frame shows without the keyboard."));
        }

        Assert.True(inside.Count > 0, Text($"{id} focused by a click changes no pixel of its bounds {bounds}: its underline does not show."));
        var middle = (bounds.Top + bounds.Bottom) / 2;
        if (inside.FindIndex(p => p.Y < middle) is >= 0 and var above)
        {
            Assert.Fail(Text($"{id} focused by a click changes {inside.Count(p => p.Y < middle)} pixels of its bounds {bounds} above their lower half, the first at ({inside[above].X}, {inside[above].Y}): more than an underline is drawn."));
        }

        AssertDrawsInside(app, link, (0, 0), id + " focused by a click");
    }

    /// <summary>
    /// What <paramref name="link"/> draws, the picture with it against the picture with its opacity 0
    /// (which moves nothing): inside its bounds when <paramref name="reach"/> is none; with the frame,
    /// exactly its bounds grown by <paramref name="reach"/> left and right and above and below.
    /// </summary>
    private static void AssertDrawsInside(ShellDriver app, Button link, (int X, int Y) reach, string what)
    {
        var pixels = PixelBox(app, link);
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

    /// <summary>The whole pixels <paramref name="control"/>'s bounds cover in the window.</summary>
    private static Box PixelBox(ShellDriver app, Visual control)
    {
        var bounds = app.BoundsOf(control);
        return new Box((int)Math.Floor(bounds.Left), (int)Math.Floor(bounds.Top), (int)Math.Ceiling(bounds.Right), (int)Math.Ceiling(bounds.Bottom));
    }

    /// <summary>
    /// What a hit test finds at the centre of every pixel of <paramref name="link"/>'s frame's ring, its
    /// bounds grown by <see cref="FrameReach"/> less its bounds, after the window has drawn what it shows.
    /// </summary>
    private static Dictionary<(int X, int Y), IInputElement?> HitsOnRing(ShellDriver app, Button link)
    {
        app.Settle();
        var bounds = PixelBox(app, link);
        var ring = bounds.Grown(FrameReach);
        var hits = new Dictionary<(int X, int Y), IInputElement?>();
        for (var y = ring.Top; y < ring.Bottom; y++)
        {
            for (var x = ring.Left; x < ring.Right; x++)
            {
                if (!bounds.Contains(x, y))
                {
                    hits[(x, y)] = app.Window.InputHitTest(new Point(x + 0.5, y + 0.5));
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// <paramref name="link"/>, focused with the keyboard's frame, changes nothing a hit test finds on its
    /// frame's ring against <paramref name="unfocused"/>, and nothing there is the link or inside it.
    /// </summary>
    private static void AssertRingHitsAsWithoutFocus(ShellDriver app, Button link, Dictionary<(int X, int Y), IInputElement?> unfocused, string id)
    {
        Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame.");
        var focused = HitsOnRing(app, link);
        Assert.Equal(unfocused.Keys.Order(), focused.Keys.Order());
        var wrong = focused
            .Where(hit => InLink(link, hit.Value) || !ReferenceEquals(hit.Value, unfocused[hit.Key]))
            .Select(hit => Text($"({hit.Key.X + 0.5}, {hit.Key.Y + 0.5}): {Describe(hit.Value)}, without the focus {Describe(unfocused[hit.Key])}"))
            .ToList();
        Assert.True(
            wrong.Count == 0,
            Text($"While {id} has the keyboard's frame, {wrong.Count} of the {focused.Count} pixels of the frame's ring at {PixelBox(app, link).Grown(FrameReach)} hit something else than without the focus, or the link; the first: {string.Join("; ", wrong.Take(4))}."));
    }

    /// <summary>
    /// The pointer resting on, then clicking, a point of <paramref name="link"/>'s frame's outer line (2
    /// wide, the ring's outermost) or inner line (1 wide, inside it), each time with the link focused again
    /// first as Tab focuses it (a click elsewhere can take the focus): the point shows the frame, the link
    /// is not hovered and no address is asked for.
    /// </summary>
    private static void AssertPointerOnFramePressesNothing(ShellDriver app, Button link, string id)
    {
        var bounds = PixelBox(app, link);
        var middleX = Math.Floor((bounds.Left + bounds.Right) / 2.0) + 0.5;
        var middleY = Math.Floor((bounds.Top + bounds.Bottom) / 2.0) + 0.5;
        var points = new (string Where, Point At)[]
        {
            ("the outer line's left", new Point(bounds.Left - FrameReach.X + 0.5, middleY)),
            ("the inner line's top", new Point(middleX, bounds.Top - FrameReach.Y + 2 + 0.5)),
            ("the inner line's right", new Point(bounds.Right + FrameReach.X - 3 + 0.5, middleY)),
            ("the outer line's bottom", new Point(middleX, bounds.Bottom + FrameReach.Y - 1 + 0.5)),
        };
        var background = Colour(app, "VcPanelBackgroundBrush");
        var requests = app.Launcher.Requests.Count;
        foreach (var (where, at) in points)
        {
            app.MoveToNeutral();
            Assert.True(link.Focus(NavigationMethod.Tab), $"{id} takes no focus.");
            app.Settle();
            Assert.True(app.FocusedAutomationId() == id && app.FocusVisible, Text($"{id} is not focused with the keyboard's frame before the pointer goes to {where} at ({at})."));
            Assert.True(app.Capture().PixelAt(at) != background, Text($"{where} of {id}'s frame at ({at}) shows the background: the point is not on the frame."));
            app.MoveMouse(at);
            Assert.False(link.IsPointerOver, Text($"The pointer on {where} of {id}'s frame at ({at}) hovers the link."));
            app.Click(at);
            Assert.True(app.Launcher.Requests.Count == requests, Text($"A click on {where} of {id}'s frame at ({at}) pressed the link: {string.Join(", ", app.Launcher.Requests.Skip(requests).Select(request => request.FullPath))}."));
        }

        app.MoveToNeutral();
    }

    private static bool InLink(Button link, IInputElement? hit) => hit is Visual visual && visual.GetSelfAndVisualAncestors().Contains(link);

    private static string Describe(IInputElement? hit) => hit is null ? "nothing" : Text($"{hit.GetType().Name} {(hit as Control)?.Name}").TrimEnd();

    /// <summary>The place of every shown control in <paramref name="scope"/>, in the window, in tree order.</summary>
    private static IReadOnlyList<string> Places(ShellDriver app, Visual scope) =>
        [.. scope.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible).Select(control => Text($"{control.GetType().Name} {control.Name} {app.BoundsOf(control)}"))];

    private static Color Colour(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string Hex(Color colour) => Text($"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    /// <summary>A box of whole pixels, its right and bottom exclusive.</summary>
    private sealed record Box(int Left, int Top, int Right, int Bottom)
    {
        public bool Contains(Box other) => other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

        public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

        public Box Grown((int X, int Y) by) => new(Left - by.X, Top - by.Y, Right + by.X, Bottom + by.Y);

        public override string ToString() => Text($"({Left}, {Top})-({Right}, {Bottom})");
    }
}
