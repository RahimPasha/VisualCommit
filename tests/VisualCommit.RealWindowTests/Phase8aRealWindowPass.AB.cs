// Parts A and B's one launch of the 8a pass (docs/test-reports/phase-8a.md, "The real window (part B)" and
// "The real window (part A)"; docs/phase-8a-design.md 7.5, 8.5, 12.3.4 launch 3, 12.3.5; D407, D409, D417,
// D428 to D432, D444): the executable's icon and the window's two icons against the .ico's frames, the
// title bar dark with the mark; About from the palette by real keys, its version read against the built
// app's, at 1920×1080 where it fits; the license's text dialog by a real Tab and Enter; the title bar
// light and the taskbar's button. Part B's six captures have no scripted twin and are inspected; part A's
// are compared with their twins. Nothing is pressed that leaves the app: no link, no "Copy all", no F1.
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Testing;
using Xunit;
using Xunit.Sdk;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase8aRealWindowPass
{
    /// <summary>Launch 3's failure before its first key when its window does not become the foreground window (design 12.3.4 launch 3, 12.3.5).</summary>
    private const string MainNotForegroundMessage = "The app's window is not the foreground window, even after a click on its title bar: a real Ctrl+Shift+P would reach another window.";

    /// <summary>Launch 3's failure when the pass's Release build carries no commit (design 12.3.4 launch 3; the report's open point 65); {0} the product version, {1} what About shows.</summary>
    private const string NoCommitMessage = "The built VisualCommit.dll's ProductVersion is \"{0}\", which names no commit, so About shows \"{1}\" without the short commit (design 1.6): the lead rules (design 12.3.4 launch 3).";

    /// <summary>
    /// B2's taskbar step's failure when more than one taskbar button is named for VisualCommit
    /// (<see cref="MoreThanOneTaskbarButton"/>); {0} how many, {1} each one's name, bounds and taskbar. One
    /// found is captured; none found stays run.txt's "Not run" line (<c>B.TaskbarButtonNotRun</c>).
    /// </summary>
    private const string MoreThanOneTaskbarButtonMessage = "B2's taskbar button: {0} taskbar buttons named VisualCommit were found, so none was captured: another VisualCommit is running (or is pinned to the taskbar), and its button cannot be told from this launch's. Close every other VisualCommit, or unpin it, and run the launch again. The buttons: {1}.";

    /// <summary>A spot of the left panel clear of every text and control at 1100×700 on the welcome page, where <c>Phase0RealWindowPass</c> reads the panel's colour: B2's wait for the light theme drawn.</summary>
    private static readonly PointF LeftPanelClear = new(130, 700 - 66);

    /// <summary>
    /// Launch 3 (design 12.3.4; the twin of part A's method 1, <c>Check_A1_A4_about_dark_and_license</c>,
    /// and, at 1920×1080, of method 3's picture), one empty tab, dark, 1100×700:
    /// <list type="number">
    /// <item>B1 (a): the built executable's icon asked at 64 pixels, a frame the <c>.ico</c> holds whatever
    /// the scaling, against the <c>.ico</c>'s frame of the size it came at, every pixel at alpha 255 within 2
    /// per channel; the size asked and got in run.txt. (b): <c>WM_GETICON</c>'s small and big icons, each
    /// given (a message the app does not answer within 5 s counts as none), each of a size the
    /// <c>.ico</c> holds compared as (a); their sizes, the window's DPI, <c>SM_CXSMICON</c> and
    /// <c>SM_CXICON</c> in run.txt. (a) and (b) are asserted at the launch's end, so that a miss there
    /// still lets A1 and A4 run; run.txt has every value at once.</item>
    /// <item>The window the foreground window before B1 (c) and the first key, else one real click on its
    /// title bar (only where the window under that point is the app's) and a wait of 5 s, else the method
    /// fails before any key.</item>
    /// <item>B1 (c): the window with its frame, <c>b1-title-bar-dark-with-frame</c>, and its title bar's
    /// left 64 pixels from the visible frame's corner by the title bar's height, four times,
    /// <c>b1-title-bar-icon-dark</c>; whether the window was the foreground window in run.txt.</item>
    /// <item>A1: a real Ctrl+Shift+P, <c>about</c> typed, the one row <c>About VisualCommit…</c> highlighted,
    /// a real Enter: About with <c>SettingsNavAbout</c> focused; <c>AboutVersion</c>'s name the built app's
    /// version with its short commit; <c>a1a-about-dark</c>. At 1920×1080 where it fits,
    /// <c>a3a-about-1920</c>, then back to 1100×700; else run.txt's line.</item>
    /// <item>A4: a real Tab to <c>Show license…</c>, a real Enter: the text dialog <c>MIT License</c>, the
    /// focus in its text; <c>a4a-license</c>; a real Esc: the dialog closed, the focus back on the button.
    /// A real Esc: the settings screen closed.</item>
    /// <item>B2: a real click on <c>ThemeSwitch</c>; the light theme drawn; <c>b2-title-bar-light-with-frame</c>,
    /// <c>b2-title-bar-icon-light</c>; the taskbar's button named <c>VisualCommit…</c> within 10 s,
    /// <c>b2-taskbar-button</c> and <c>b2-taskbar-button-enlarged</c>, or run.txt's "Not run" line when
    /// none shows; more than one such button: none captured, each noted, and the method fails at its end
    /// saying to close the other VisualCommit.</item>
    /// </list>
    /// The app closed by its window, exit code 0.
    /// </summary>
    [Fact]
    public void Check_A_and_B_about_and_icon()
    {
        using var notes = NoteLaunch(nameof(Check_A_and_B_about_and_icon));
        AssertDesktopReady();
        var data = Run.NewDataDirectory();
        using var app = Launch(data, OneTab(null));
        WaitForWelcome(app);

        // B1 (a) and (b): the icons against the .ico's frames, noted now, asserted at the end.
        var frames = IconFile.Read(IconFile.RepositoryPath(IconFile.IcoFile));
        var sizes = frames.Select(frame => frame.Size).ToList();
        Note($"B1: the .ico's frames {string.Join(", ", sizes)} px; expected {string.Join(", ", B.IcoSizes)}");
        Assert.Equal(B.IcoSizes, sizes);
        List<string> b1 = [.. ExecutableIcon(frames)];
        b1.AddRange(WindowIcons(app, frames));

        // Before B1 (c)'s capture and the first key (B1 presses nothing): the window the foreground window,
        // so that B1 (c)'s title bar is the active caption, as B2's is after its click.
        EnsureForegroundBeforeFirstKey(app, (foreground, _) => foreground, "the app's window the foreground window", MainNotForegroundMessage);

        // B1 (c): the title bar, dark, the pointer at the neutral point.
        var darkTitleBar = TitleBarPictures(app, B.B1TitleBarDarkWithFrame, B.B1TitleBarIconDark);

        // A1: About from the palette by real keys; the built version; a1a.
        OpenAboutFromThePalette(app);
        AssertBuiltVersion(app);
        using (var about = Capture(app, A.A1aAboutDark))
        {
            Compare(about, A.A1aAboutDark);
        }

        // A1 at 1920×1080 where it fits (A3's picture), then back to 1100×700.
        AboutAtFullSize(app);

        // A4: the license by a real Tab and Enter; a4a; Esc.
        Keys(app, VirtualKeyShort.TAB);
        app.WaitForFocusOn(A.ShowLicenseId, StepTimeout);
        KeysOn(app, new FocusTarget(A.ShowLicenseId), VirtualKeyShort.RETURN);
        app.WaitForText(A.DialogTitleId, A.LicenseTitle, StepTimeout);
        WaitForFocusInText(app, A.TextId, StepTimeout);
        using (var license = Capture(app, A.A4aLicense))
        {
            Compare(license, A.A4aLicense);
        }

        Keys(app, VirtualKeyShort.ESCAPE);
        app.WaitFor(
            () => app.ShownElements(A.TextId).Count == 0 && app.FocusedId() == A.ShowLicenseId && app.ShownElements(A.VersionId).Count == 1,
            $"the text dialog closed alone, {A.ShowLicenseId} UI Automation's focused element and {A.VersionId} still shown",
            StepTimeout);

        // The screen closed: the named condition B2 waits on before its click.
        Keys(app, VirtualKeyShort.ESCAPE);
        app.WaitUntilGone(RealApp.SettingsDialogId, StepTimeout);
        Note($"after the settings screen closed, the data folder holds settings.json: {File.Exists(Path.Combine(data, "settings.json"))}");

        // B2: the title bar light, then the taskbar's button.
        Press(app, app.Find("ThemeSwitch"));
        WaitForSpot(app, "the left panel's background in the light theme (VcPanelBackgroundBrush)", LeftPanelClear, A.Light.Panel);
        WaitForTitleBarRedrawn(app, darkTitleBar);
        TitleBarPictures(app, B.B2TitleBarLightWithFrame, B.B2TitleBarIconLight);
        List<string> problems = [.. b1, .. TaskbarButtonPictures(app)];

        Assert.True(problems.Count == 0, string.Join('\n', problems));
        Assert.Equal(0, CloseNoted(app));
    }

    // ---- What launches 3 to 6 share ----

    /// <summary>
    /// Waits until the empty tab's welcome page shows: the status bar names the git found (the start's
    /// work has ended, <see cref="RealApp.WaitForGitFound"/>) and the welcome page's <c>OpenRepoButton</c>
    /// is shown, each within <see cref="LoadTimeout"/>.
    /// </summary>
    private static void WaitForWelcome(RealApp app)
    {
        app.WaitForGitFound(LoadTimeout);
        app.WaitFor(() => app.ShownElements("OpenRepoButton").Count == 1, "the welcome page (OpenRepoButton shown)", LoadTimeout);
    }

    /// <summary>
    /// The guard before a launch's first key (design 12.3.3, 12.3.4 launches 3, 5 and 6, 12.3.5), and in
    /// launch 5 before its first capture too (D459): each look reads the two conditions once,
    /// <see cref="RealApp.ForegroundIsThis"/> and the id of UI Automation's focused element
    /// (<see cref="RealApp.FocusedId"/>, read through <see cref="RealApp.ReadOr{T}"/>, so that a read that fails,
    /// also while another window is in front, counts as a look with no focused id and ends nothing), and
    /// <paramref name="ready"/> judges those reads. When it does not
    /// hold, one real click on the window's title bar, away from its left end and its caption buttons
    /// (<see cref="RealApp.ClickTitleBar"/>), then a wait of at most 5 s until it holds, else the method fails
    /// with <paramref name="failure"/> before any key. When another window lies over the point the click
    /// would land on, nothing is clicked and the method fails with <paramref name="failure"/> and that
    /// window's words. run.txt says whether the click was needed and, on each of its lines, what the look
    /// behind it read; each line starts with <paramref name="when"/>. Returns it.
    /// </summary>
    private bool EnsureForegroundBeforeFirstKey(RealApp app, Func<bool, string?, bool> ready, string what, string failure, string when = "before the first key")
    {
        var (foreground, focused) = (app.ForegroundIsThis, RealApp.ReadOr(app.FocusedId, (string?)null));
        if (ready(foreground, focused))
        {
            Note($"{when}: {what}, with no click ({GuardReads(foreground, focused)})");
            return false;
        }

        Note($"{when}: not {what} ({GuardReads(foreground, focused)}); one real click on the title bar");
        try
        {
            app.ClickTitleBar();
        }
        catch (InvalidOperationException ex)
        {
            var refused = $"{failure} {ex.Message}";
            Note(refused);
            throw new XunitException(refused);
        }
        try
        {
            app.WaitFor(() => ready(foreground = app.ForegroundIsThis, focused = RealApp.ReadOr(app.FocusedId, (string?)null)), what, I.ForegroundAgainWithin);
        }
        catch (TimeoutException)
        {
            Note($"{when}: the last look after the click read {GuardReads(foreground, focused)}");
            Note(failure);
            throw new XunitException(failure);
        }

        Note($"{when}: {what} after the click on the title bar ({GuardReads(foreground, focused)})");
        return true;
    }

    /// <summary>The guard's two reads of one look in words for run.txt.</summary>
    private static string GuardReads(bool foreground, string? focusedId) =>
        $"ForegroundIsThis {(foreground ? "true" : "false")}, UI Automation's focused element {(string.IsNullOrEmpty(focusedId) ? "none, or one without an id" : focusedId)}";

    /// <summary>
    /// Waits until the keyboard focus is in the text <paramref name="textId"/>, an AvaloniaEdit editor
    /// (<c>TextDocumentText</c>, <c>DiffText</c>, <c>BlameText</c>), whose inner <c>TextArea</c> takes the
    /// focus and has no automation id of its own (design 12.3.4 launch 3): UI Automation's focused element is
    /// the text itself, or lies inside its bounds and is no button. Notes in run.txt which of the two held
    /// and what the focused element was; returns that note. Part K's text waits use it too.
    /// </summary>
    private string WaitForFocusInText(RealApp app, string textId, TimeSpan timeout)
    {
        string? held = null;
        var seen = "nothing";
        try
        {
            app.WaitFor(
                () =>
                {
                    held = FocusInText(app, textId, out seen);
                    return held is not null;
                },
                $"the keyboard focus in {textId}",
                timeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} UI Automation's focused element is {seen}.", ex);
        }

        Note($"the focus in {textId}: {held}");
        return held!;
    }

    /// <summary>One look for <see cref="WaitForFocusInText"/>: which condition holds, or null; <paramref name="seen"/> describes the focused element.</summary>
    private static string? FocusInText(RealApp app, string textId, out string seen)
    {
        seen = "nothing";
        if (app.FocusedElement() is not { } focused)
        {
            return null;
        }

        string? id;
        ControlType type;
        Rectangle bounds;
        try
        {
            id = focused.Properties.AutomationId.ValueOrDefault;
            type = focused.Properties.ControlType.ValueOrDefault;
            bounds = focused.BoundingRectangle;
        }
        catch (Exception ex) when (ex is COMException or FlaUI.Core.Exceptions.FlaUIException)
        {
            return null;
        }

        seen = string.Create(CultureInfo.InvariantCulture, $"{(string.IsNullOrEmpty(id) ? "(no id)" : id)} ({type}) at {bounds}");
        if (id == textId)
        {
            return $"UI Automation's focused element is {textId} itself";
        }

        if (app.ShownElements(textId) is not [var text] || type == ControlType.Button || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        var area = text.BoundingRectangle;
        area.Inflate(1, 1);
        return area.Contains(bounds) ? $"UI Automation's focused element, {seen}, lies inside {textId}'s bounds {text.BoundingRectangle} and is no button" : null;
    }

    // ---- Part B's steps ----

    /// <summary>
    /// The size B1 (a) asks the executable's icon at: one of the <c>.ico</c>'s frames, so that Windows hands
    /// back that frame whatever the display's scaling. Asked at none, Windows gives <c>SM_CXICON</c> for the
    /// test host's DPI, 32 scaled by it, which at 175 % is 56, a size the <c>.ico</c> lacks and a frame
    /// Windows scaled (design 12.3.5). 64 is <c>SM_CXICON</c> at the laptop's 200 %: the frame the first
    /// runs compared.
    /// </summary>
    internal const int ExecutableIconSize = 64;

    /// <summary>
    /// B1 (a): the built <c>VisualCommit.exe</c>'s icon (its <c>ApplicationIcon</c>) asked at
    /// <see cref="ExecutableIconSize"/> (<see cref="ExecutableIconAt"/>) against the <c>.ico</c>'s frame of
    /// the size it came at (<see cref="IconAgainstFrame"/>); the size asked and the size got in run.txt.
    /// Returns what is wrong.
    /// </summary>
    private List<string> ExecutableIcon(IReadOnlyList<IconFrame> frames)
    {
        Note($"B1 (a): the executable {RealApp.ExecutablePath}");
        using var image = ExecutableIconAt(ExecutableIconSize);
        Note(string.Create(CultureInfo.InvariantCulture, $"B1 (a): the executable's icon asked at {ExecutableIconSize}x{ExecutableIconSize}, a frame the .ico holds, whatever the scaling; got {SizeOf(image)}"));
        if (image is null)
        {
            return [string.Create(CultureInfo.InvariantCulture, $"B1 (a): Icon.ExtractIcon gave no icon at {ExecutableIconSize} px for the built VisualCommit.exe: the file holds no icon of its own.")];
        }

        return [.. IconAgainstFrame(image, frames, "B1 (a): the executable's icon")];
    }

    /// <summary>
    /// The built <c>VisualCommit.exe</c>'s first icon, <paramref name="size"/> pixels square, as a bitmap with
    /// its alpha: <c>Icon.ExtractIcon</c>, Windows' <c>SHDefExtractIcon</c> at that size, which takes the
    /// icon's frame of that size where it holds one, whatever the display's scaling. Null when the file holds
    /// no icon. The caller disposes it. It reads the file alone: no app is started.
    /// </summary>
    internal static Bitmap? ExecutableIconAt(int size)
    {
        using var icon = Icon.ExtractIcon(RealApp.ExecutablePath, 0, size);
        return icon?.ToBitmap();
    }

    /// <summary>
    /// B1 (b): <c>WM_GETICON</c>'s <c>ICON_SMALL</c> and <c>ICON_BIG</c>, each asserted given; their sizes,
    /// the window's DPI and <c>SM_CXSMICON</c> and <c>SM_CXICON</c> at it in run.txt; each of a size the
    /// <c>.ico</c> holds compared with that frame as (a) compares. The small pair is inspected, not asserted
    /// (the report's open point 48): run.txt says whether the title bar draws <c>ICON_SMALL</c> at its own
    /// size, and when not, whether that is design 7.9 item 2's miss. Returns what is wrong.
    /// </summary>
    private List<string> WindowIcons(RealApp app, IReadOnlyList<IconFrame> frames)
    {
        using var icons = app.WindowIcons();
        static string Unanswered(bool unanswered) => unanswered ? " (WM_GETICON unanswered within 5 s)" : string.Empty;
        Note(string.Create(
            CultureInfo.InvariantCulture,
            $"B1 (b): ICON_SMALL {SizeOf(icons.Small)}{Unanswered(icons.SmallUnanswered)}, ICON_BIG {SizeOf(icons.Big)}{Unanswered(icons.BigUnanswered)}; the window's DPI {icons.Dpi}; SM_CXSMICON {icons.SmallIconMetric}, SM_CXICON {icons.IconMetric} at that DPI"));
        List<string> problems = [];
        foreach (var (image, which, unanswered) in new[] { (icons.Small, "ICON_SMALL", icons.SmallUnanswered), (icons.Big, "ICON_BIG", icons.BigUnanswered) })
        {
            if (image is null)
            {
                problems.Add(unanswered
                    ? $"B1 (b): the app's window did not answer WM_GETICON for {which} within 5 s, so no {which} icon was read."
                    : $"B1 (b): WM_GETICON gave no {which} icon for the app's window.");
            }
            else if (image.Width == image.Height && B.IcoSizes.Contains(image.Width))
            {
                problems.AddRange(IconAgainstFrame(image, frames, $"B1 (b): {which}"));
            }
            else
            {
                Note($"B1 (b): {which} is {SizeOf(image)}, none of the .ico's sizes: not compared");
            }
        }

        var small = icons.Small?.Width;
        var metric = icons.SmallIconMetric;
        Note(small == metric
            ? string.Create(CultureInfo.InvariantCulture, $"B1 (c): the title bar draws ICON_SMALL at its own size, {metric}")
            : B.IcoSizes.Contains(metric)
                ? string.Create(CultureInfo.InvariantCulture, $"B1 (c): FINDING for the lead (design 7.9 item 2): SM_CXSMICON is {metric}, a size the .ico holds, but ICON_SMALL is {small?.ToString(CultureInfo.InvariantCulture) ?? "none"}: Windows stretches another frame in the title bar")
                : string.Create(CultureInfo.InvariantCulture, $"B1 (c): SM_CXSMICON is {metric}, none of the .ico's sizes; the title bar stretches the {small?.ToString(CultureInfo.InvariantCulture) ?? "missing"} frame it was handed (no miss)"));
        return problems;
    }

    /// <summary>
    /// An icon's image against the <c>.ico</c>'s frame of its size (design 7.5; the report's open point
    /// 48): every pixel of the image at alpha 255 equals the frame's pixel within
    /// <see cref="Phase8aExpectations.B.IconTolerance"/> in each channel, alpha included. run.txt gets the
    /// size, the pixels compared and those that differ; returns what is wrong, never met by a wider
    /// tolerance.
    /// </summary>
    private IEnumerable<string> IconAgainstFrame(Bitmap image, IReadOnlyList<IconFrame> frames, string what)
    {
        var size = image.Width;
        var frame = image.Height == size ? frames.FirstOrDefault(candidate => candidate.Size == size) : null;
        if (frame is null)
        {
            Note($"{what}: {SizeOf(image)}, a size the .ico does not hold");
            return [string.Create(CultureInfo.InvariantCulture, $"{what} is {SizeOf(image)}; the .ico holds no frame of that size ({string.Join(", ", B.IcoSizes)}).")];
        }

        var (compared, differing, examples) = PixelsAgainstFrame(image, frame);
        Note(string.Create(CultureInfo.InvariantCulture, $"{what}: {size}x{size}, {compared} opaque pixels against the .ico's {size} px frame, {differing} differ by more than {B.IconTolerance}{(examples.Count > 0 ? ": " + string.Join("; ", examples) : string.Empty)}"));
        if (compared == 0)
        {
            return [$"{what} has no pixel at alpha 255, so nothing of it was compared with the .ico's frame."];
        }

        return differing == 0
            ? []
            : [string.Create(CultureInfo.InvariantCulture, $"{what}: {differing} of its {compared} opaque pixels differ from the .ico's {size} px frame by more than {B.IconTolerance} in a channel, for example {string.Join("; ", examples)}.")];
    }

    /// <summary>
    /// <see cref="IconAgainstFrame"/>'s rule for an image of <paramref name="frame"/>'s size: each pixel of
    /// <paramref name="image"/> at alpha 255 against the frame's pixel, which differs when it lies further
    /// than <see cref="Phase8aExpectations.B.IconTolerance"/> from it in a channel, alpha included. Returns
    /// how many were compared, how many differ and the first five that do, in words.
    /// </summary>
    internal static (int Compared, int Differing, List<string> Examples) PixelsAgainstFrame(Bitmap image, IconFrame frame)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(frame);
        var size = frame.Size;
        var pixels = frame.Bgra();
        var compared = 0;
        var differing = 0;
        List<string> examples = [];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.A != 255)
                {
                    continue;
                }

                compared++;
                var at = ((y * size) + x) * 4;
                var expected = Color.FromArgb(pixels[at + 3], pixels[at + 2], pixels[at + 1], pixels[at]);
                if (Math.Max(Distance(pixel, expected), Math.Abs(pixel.A - expected.A)) > B.IconTolerance)
                {
                    differing++;
                    if (examples.Count < 5)
                    {
                        examples.Add(string.Create(CultureInfo.InvariantCulture, $"({x}, {y}) {Hex(pixel)} against {Hex(expected)} at alpha {expected.A}"));
                    }
                }
            }
        }

        return (compared, differing, examples);
    }

    /// <summary>
    /// B1 (c) and B2's title bar: the pointer at the neutral point and no tooltip in the window; the window
    /// with its frame (<see cref="RealApp.CaptureWindow"/>) saved as <paramref name="framed"/>; the title
    /// bar's left <see cref="Phase8aExpectations.B.TitleBarCropWidth"/> pixels from the visible frame's
    /// corner (<see cref="RealApp.VisibleFrameOffset"/>: the capture's own corner lies on the invisible
    /// resize border, which shows the desktop behind the window; the capture's corner when DWM does not
    /// say, which run.txt then names) down to the client area's top (<see cref="RealApp.TitleBarHeight"/>,
    /// the source the report's open point 49 leaves to the first run, named in run.txt), enlarged
    /// <see cref="Phase8aExpectations.B.Enlargement"/> times nearest-neighbour, saved as
    /// <paramref name="enlarged"/>. Neither has a twin; both are inspected. run.txt says whether the window
    /// was the foreground window, whose title bar is drawn active. Returns the colour of the title bar's
    /// middle, where <see cref="RealApp.ClickTitleBar"/> clicks.
    /// </summary>
    private Color TitleBarPictures(RealApp app, string framed, string enlarged)
    {
        Assert.Matches(PictureName(), framed);
        Assert.Matches(PictureName(), enlarged);
        app.MoveToNeutral();
        app.WaitFor(() => ToolTipsShown(app).Count == 0, $"no tooltip in the window before {framed}", StepTimeout);
        var title = app.Window.Title;
        Note($"{framed}: the window's title \"{title}\", expected \"{B.WindowTitle}\"");
        Assert.Equal(B.WindowTitle, title);
        var visible = app.VisibleFrameOffset(out var frames);
        var foreground = app.ForegroundIsThis;
        using var frame = app.CaptureWindow();
        Run.Save(frame, framed);
        var height = Math.Min(app.TitleBarHeight, frame.Height);
        var (left, top) = visible is { } corner ? (corner.X, corner.Y) : (0, 0);
        var crop = new Rectangle(left, top, Math.Min(B.TitleBarCropWidth, frame.Width - left), Math.Max(1, height - top));
        using (var big = Enlarged(frame, crop, B.Enlargement))
        {
            Run.Save(big, enlarged);
        }

        var middle = frame.GetPixel(frame.Width / 2, height / 2);
        Note(string.Create(
            CultureInfo.InvariantCulture,
            $"{framed}: {frame.Width}x{frame.Height} screen pixels, the window the foreground window: {foreground}; the title bar {height} high (GetWindowRect's top to the client area's top); {frames}; {enlarged}: the title bar's left {crop.Width} by {crop.Height} from ({left}, {top}) of the capture, {(visible is null ? "the capture's own corner, as no visible frame was read" : "the visible frame's corner")}, {B.Enlargement} times; the title bar's middle {Hex(middle)}"));
        return middle;
    }

    /// <summary>
    /// B2's wait for the title bar (the report's open point 50), after the light theme is drawn in the
    /// client area: until the title bar's middle, in a capture of the window with its frame, is no longer
    /// <paramref name="dark"/>, the colour B1 (c) read there, at most <see cref="StepTimeout"/>. The title
    /// bar's colour is Windows' and only inspected (as <c>Phase0RealWindowPass</c>'s framed captures show it turning light), so a
    /// title bar that stays as it was is noted in run.txt and B2's pictures are taken all the same.
    /// </summary>
    private void WaitForTitleBarRedrawn(RealApp app, Color dark)
    {
        var seen = dark;
        try
        {
            app.WaitFor(
                () =>
                {
                    using var frame = app.CaptureWindow();
                    seen = frame.GetPixel(frame.Width / 2, Math.Min(app.TitleBarHeight, frame.Height) / 2);
                    return Distance(seen, dark) > SpotTolerance;
                },
                $"the title bar's middle no longer {Hex(dark)}",
                StepTimeout);
            Note($"B2: the title bar redrawn after the click on ThemeSwitch: its middle {Hex(seen)}, {Hex(dark)} in B1 (c)");
        }
        catch (TimeoutException)
        {
            Note(string.Create(CultureInfo.InvariantCulture, $"B2: the title bar's middle still {Hex(seen)} after {StepTimeout.TotalSeconds:0} s, as in B1 (c): inspect {B.B2TitleBarLightWithFrame}"));
        }
    }

    /// <summary>
    /// B2's taskbar button (the report's open point 51): the taskbar's buttons whose UI Automation names
    /// start with <see cref="Phase8aExpectations.B.TaskbarButtonNameStart"/>, looked for from the desktop
    /// root for <see cref="Phase8aExpectations.B.TaskbarButtonLimit"/> until one shows
    /// (<see cref="RealApp.TaskbarButtons"/>), the pointer at the neutral point inside the window (no preview
    /// or tooltip of the taskbar opens); each one found in run.txt with its name, bounds and taskbar. One
    /// found: its bounds asserted not empty, captured from the screen by its bounds as
    /// <c>b2-taskbar-button</c> and four times enlarged as <c>b2-taskbar-button-enlarged</c>. None found:
    /// run.txt's "Not run" line, and the method goes on. More than one: none is captured, and the problem
    /// (<see cref="MoreThanOneTaskbarButton"/>), noted, fails the method at its end. Returns what is wrong.
    /// </summary>
    private List<string> TaskbarButtonPictures(RealApp app)
    {
        app.MoveToNeutral();
        var clock = Stopwatch.StartNew();
        var found = app.TaskbarButtons(B.TaskbarButtonNameStart, B.TaskbarButtonLimit);
        var after = clock.Elapsed;
        foreach (var each in found)
        {
            Note(string.Create(CultureInfo.InvariantCulture, $"B2's taskbar button: \"{each.Name}\" at {each.ScreenBounds} (screen pixels) on {each.Taskbar}, found after {after.TotalSeconds:0.0} s"));
        }

        if (found.Count == 0)
        {
            Note(B.TaskbarButtonNotRun);
            return [];
        }

        if (MoreThanOneTaskbarButton(found) is { } failure)
        {
            Note(failure);
            return [failure];
        }

        var button = found[0];
        Assert.True(button.ScreenBounds is { Width: > 0, Height: > 0 }, $"The taskbar's button \"{button.Name}\" has empty bounds: {button.ScreenBounds}.");
        using var shot = RealApp.CaptureScreen(button.ScreenBounds);
        Run.Save(shot, B.B2TaskbarButton);
        using var big = Enlarged(shot, new Rectangle(0, 0, shot.Width, shot.Height), B.Enlargement);
        Run.Save(big, B.B2TaskbarButtonEnlarged);
        return [];
    }

    /// <summary>
    /// B2's taskbar step's failure for the buttons <see cref="RealApp.TaskbarButtons"/> found, or null for
    /// none or one. Windows gives each executable's path a button of its own, named for its app whichever
    /// build it is, and a button does not say whose process it is: of two or more, any could be another
    /// VisualCommit's (a build elsewhere, the owner's own copy) or one pinned to the taskbar, whose icon
    /// the pictures would then show. So none is taken, and the message names each and says to close the other.
    /// </summary>
    internal static string? MoreThanOneTaskbarButton(IReadOnlyList<RealApp.TaskbarEntry> found)
    {
        ArgumentNullException.ThrowIfNull(found);
        return found.Count <= 1
            ? null
            : string.Format(
                CultureInfo.InvariantCulture,
                MoreThanOneTaskbarButtonMessage,
                found.Count,
                string.Join("; ", found.Select(each => string.Create(CultureInfo.InvariantCulture, $"\"{each.Name}\" at {each.ScreenBounds} on {each.Taskbar}"))));
    }

    /// <summary><paramref name="crop"/> of <paramref name="source"/>, <paramref name="factor"/> times as wide and high, each pixel a block of the same colour (nearest-neighbour).</summary>
    private static Bitmap Enlarged(Bitmap source, Rectangle crop, int factor)
    {
        var big = new Bitmap(crop.Width * factor, crop.Height * factor, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (var y = 0; y < crop.Height; y++)
        {
            for (var x = 0; x < crop.Width; x++)
            {
                var pixel = source.GetPixel(crop.X + x, crop.Y + y);
                for (var dy = 0; dy < factor; dy++)
                {
                    for (var dx = 0; dx < factor; dx++)
                    {
                        big.SetPixel((x * factor) + dx, (y * factor) + dy, pixel);
                    }
                }
            }
        }

        return big;
    }

    private static string SizeOf(Bitmap? image) =>
        image is null ? "none" : string.Create(CultureInfo.InvariantCulture, $"{image.Width}x{image.Height}");

    // ---- Part A's steps ----

    /// <summary>
    /// A1's route by real keys: the pointer at the neutral point; Ctrl+Shift+P; <c>CommandPaletteBox</c> UI
    /// Automation's focused element; <c>about</c> typed; the list holding one row, highlighted, whose name is
    /// asserted to be <c>About VisualCommit…</c> before the real Enter (design 12.3.2: else no Enter); then
    /// the palette closed, <c>AboutVersion</c> shown (a text only About shows; the section's panel is no
    /// UI Automation element) and <c>SettingsNavAbout</c> UI Automation's focused element.
    /// </summary>
    private void OpenAboutFromThePalette(RealApp app)
    {
        app.MoveToNeutral();
        Keys(app, VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
        app.WaitForPalette(StepTimeout);
        app.WaitForFocusOn("CommandPaletteBox", StepTimeout);
        TypeKeys(app, A.PaletteQuery);
        app.WaitFor(() => app.PaletteQuery() == A.PaletteQuery, $"the palette's box to hold \"{A.PaletteQuery}\"", StepTimeout);
        try
        {
            app.WaitFor(() => app.PaletteRows() is [{ IsHighlighted: true }], "the palette's list to hold one row, highlighted", StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} It shows: {app.DescribePaletteRows()}.", ex);
        }

        var row = app.PaletteRows()[0];
        Note($"A1: the palette's one row \"{row.Label}\", highlighted, key \"{row.Key}\", detail \"{row.Detail}\"");
        Assert.True(row.Label == A.PaletteCommand, $"The palette's highlighted row reads \"{row.Label}\", not \"{A.PaletteCommand}\": no Enter is sent (D444).");
        KeysOn(app, new FocusTarget("CommandPaletteBox"), VirtualKeyShort.RETURN);
        app.WaitForPaletteClosed(StepTimeout);
        app.WaitFor(() => app.ShownElements(A.VersionId).Count == 1, $"About shown ({A.VersionId})", StepTimeout);
        app.WaitForFocusOn(A.NavAboutId, StepTimeout);
    }

    /// <summary>
    /// <c>AboutVersion</c>'s automation name against the built app (design 1.6, 12.3.4 launch 3; the report's open point 65):
    /// <see cref="RealApp.BuiltProductVersion"/> and the name in run.txt; a build that names no commit
    /// fails saying so; then the name equals <see cref="RealApp.BuiltVersionDisplay"/> and ends with a space
    /// and seven hexadecimal digits in brackets.
    /// </summary>
    private void AssertBuiltVersion(RealApp app)
    {
        var product = RealApp.BuiltProductVersion();
        var display = RealApp.BuiltVersionDisplay();
        var name = app.ShownElements(A.VersionId)[0].Name;
        Note($"A1: the built VisualCommit.dll's ProductVersion \"{product}\", read as \"{display}\"; AboutVersion's name \"{name}\" (the twin shows \"{PinnedVersion}\")");
        if (!Regex.IsMatch(display, A.BuiltVersionEnding, RegexOptions.CultureInvariant))
        {
            var message = string.Format(CultureInfo.InvariantCulture, NoCommitMessage, product, display);
            Note(message);
            throw new XunitException(message);
        }

        Assert.Equal(display, name);
        Assert.Matches(A.BuiltVersionEnding, name);
    }

    /// <summary>
    /// A1's second capture (the report's open point 64): where a 1920×1080 client area fits the screen, the
    /// window sized so, a wait until <c>SettingsNavAbout</c> lies at A3's place, <c>a3a-about-1920</c>
    /// compared with A3's picture, then the window sized 1100×700 again and a wait until the button lies at
    /// A1's place; otherwise run.txt's line in <c>Phase0RealWindowPass</c>'s form.
    /// </summary>
    private void AboutAtFullSize(RealApp app)
    {
        if (!app.Fits(FullSize.Width, FullSize.Height))
        {
            Note(string.Format(
                CultureInfo.InvariantCulture,
                A.FullSizeNotRunLine,
                app.WorkAreaSize.Width.ToString("F0", CultureInfo.InvariantCulture),
                app.WorkAreaSize.Height.ToString("F0", CultureInfo.InvariantCulture),
                string.Create(CultureInfo.InvariantCulture, $"{app.Scaling * 100:0}%")));
            return;
        }

        app.SetClientSize(FullSize.Width, FullSize.Height);
        WaitForAboutButtonAt(app, A.AboutAtFull, "1920x1080");
        using (var full = Capture(app, A.A3aAbout1920))
        {
            Compare(full, A.A3aAbout1920);
        }

        app.SetClientSize(DefaultSize.Width, DefaultSize.Height);
        WaitForAboutButtonAt(app, A.AboutAt, "1100x700");
    }

    /// <summary>Waits until <c>SettingsNavAbout</c> lies at <paramref name="place"/> in the client area at scale 1, within <see cref="Phase8aExpectations.A.Tolerance"/> (the settings card is no UI Automation element).</summary>
    private void WaitForAboutButtonAt(RealApp app, (double X, double Y) place, string size)
    {
        var seen = RectangleF.Empty;
        app.WaitFor(
            () =>
            {
                seen = app.BoundsOf(app.Find(A.NavAboutId));
                return Math.Abs(seen.X - place.X) <= A.Tolerance && Math.Abs(seen.Y - place.Y) <= A.Tolerance;
            },
            string.Create(CultureInfo.InvariantCulture, $"{A.NavAboutId} at ({place.X}, {place.Y}) at {size}"),
            StepTimeout);
        Note(string.Create(CultureInfo.InvariantCulture, $"A1 at {size}: {A.NavAboutId} at ({seen.X:0.#}, {seen.Y:0.#})"));
    }
}
