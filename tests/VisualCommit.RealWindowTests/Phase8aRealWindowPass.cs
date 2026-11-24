// What is where in the 8a real-window pass (docs/test-reports/phase-8a.md: "Conventions of every part",
// each part's "The real window"; docs/phase-8a-design.md 12; D33, D58, D224, D442 to D446, D448 to D450):
// - Phase8aRealWindowPass.cs (this file): the run (Phase8aRun: one folder of pictures and one run.txt
//   for the whole pass), the order of its thirteen methods (Phase8aOrder, design 12.3.4's), the limits,
//   launching the real app on a new data folder (Launch, Sized, Opened), the pictures (Capture, Compare,
//   CompareMasked, Picture), the ink of a text in a capture (InkOf and its rectangles), the guards of
//   real input (AssertDesktopReady, AssertNeverPressed, Press, Keys, TypeKeys), run.txt's lines
//   (NoteLaunch, Note), and Phase8aOrderTests, which start no app.
// - Phase8aRealWindowPass.L.cs, .AB.cs, .I.cs, .F.cs, .K.cs and .U.cs: one file per part, A and B
//   together, as they share one launch; each method one launch.
// - RealApp.Phase8a.cs: the stand-in for git, a second start, the window's state, icons and captures
//   outside the window, the built version and the graph row's events; the earlier RealApp files.
// - The 8a walk-through's expectations (VisualTests.Phase8a/Phase8aExpectations*.cs) are compiled into
//   this project too (see the .csproj), so both harnesses use the same constants, texts, colours,
//   picture names and launch facts.
//
// Conventions for the part files (design 12.3.1 to 12.3.3):
// - Every method: NoteLaunch first (run.txt's start and end), then AssertDesktopReady (high contrast
//   read, never changed, D224); the scenario's copy declared before the app, so that the app is stopped
//   first when a step fails; a new data folder whose session holds exactly the twin's tabs and starting
//   theme (D442), written before the start; 1100×700 (Sized) unless the launch shows the notice.
// - After an input, wait for a named condition with a limit (StepTimeout, LoadTimeout or the
//   expectations' own), never for a fixed time.
// - A picture: Picture(app, the twin's name, keepPointer, reads), which rests the pointer at the
//   neutral point unless a menu is open or the step hovers, waits until no tooltip shows, captures the
//   client area, saves it, notes every read in run.txt, compares it with the scripted twin of the same
//   name (at most 3 %, D33, D58) and then asserts the reads. The twins must exist on the machine: run
//   `dotnet test --project tests/VisualCommit.VisualTests.Phase8a` first.
// - Input: Press for a real click on an element, Keys and TypeKeys for the real keyboard. Press refuses
//   what AssertNeverPressed names (D444), Keys refuses F1 and an Enter or Space on such an element, and
//   Keys and TypeKeys send nothing while the window is not the foreground window. A launch whose first
//   input is a key asserts the foreground first, with its own fallback (design 12.3.3, 12.3.4).
// - Each launch ends with the app closed by its window, exit code 0, unless the method asserts another.
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase8a;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;
using AvaloniaColor = Avalonia.Media.Color;

namespace VisualCommit.RealWindowTests;

/// <summary>One run of the 8a pass, shared by its methods: one folder of screenshots and one run.txt.</summary>
public sealed class Phase8aRun : IDisposable
{
    public RealWindowRun Run { get; } = new(phase: Phase8aExpectations.Phase);

    /// <summary>Whether the display has been noted in run.txt yet (once, with the first sized launch).</summary>
    internal bool DisplayNoted { get; set; }

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// Runs the 8a pass's thirteen methods in design 12.3.4's order: part L's two, A and B's one, part I's
/// three, part F's four, part K's two and part U's one (D442). A method the list does not name runs
/// after them, by name. A name of a method not written yet is a literal; it becomes a nameof once the
/// method exists.
/// </summary>
public sealed class Phase8aOrder : ITestCaseOrderer
{
    /// <summary>The methods in their order.</summary>
    public static IReadOnlyList<string> Methods { get; } =
    [
        nameof(Phase8aRealWindowPass.Check_L_changes_placeholders_and_danger_button),
        nameof(Phase8aRealWindowPass.Check_L_graph_danger_item_and_head_row),
        nameof(Phase8aRealWindowPass.Check_A_and_B_about_and_icon),
        "Check_I1_second_start_hands_over",
        "Check_I2_notice_and_exit_code_2",
        "Check_I2_notice_try_again_starts_here",
        "Check_F_missing_offers_keys_and_choose",
        "Check_F5_too_old_stand_in",
        "Check_F6_config_fault_stand_in",
        "Check_F6b_real_git_broken_config",
        "Check_K1_hunk_by_keys",
        "Check_K2_blame_by_arrows",
        "Check_U_graph_row_and_names",
    ];

    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : notnull, ITestCase
    {
        ArgumentNullException.ThrowIfNull(testCases);
        return [.. testCases.OrderBy(test => Rank(test.TestMethodName)).ThenBy(test => test.TestMethodName, StringComparer.Ordinal)];
    }

    private static int Rank(string? method) => method is not null && Methods.Contains(method) ? Methods.ToList().IndexOf(method) : int.MaxValue;
}

/// <summary>
/// The real-window pass of 8a: every step of docs/test-reports/phase-8a.md marked R, repeated in the real
/// app on the Windows desktop with the real mouse and keyboard (design 12). Screenshots go to
/// artifacts/visual/phase-8a/real-window/ and are compared with the scripted twins of the same name (D33,
/// D58), which must have run first; part B's have no twin and are inspected. The launches are in the
/// part files, Phase8aRealWindowPass.&lt;Part&gt;.cs.
/// </summary>
[TestCaseOrderer(typeof(Phase8aOrder))]
public partial class Phase8aRealWindowPass(Phase8aRun fixture) : IClassFixture<Phase8aRun>
{
    /// <summary>How long the app may take to open a scenario: its history read and drawn, its status shown.</summary>
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a change of the window that follows an input may take to show: the scripted run's default wait.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    /// <summary>run.txt's line and the failure's message when Windows' high contrast is on (design 12.3.1).</summary>
    private const string HighContrastOn = "Windows' high contrast is on. The pass needs it off and never changes it (D224): turn it off and run again.";

    /// <summary>How far a colour read from the screen may lie from the expected one in each channel: <see cref="RealWindowRun.AssertColour"/>'s.</summary>
    private const int SpotTolerance = 3;

    /// <summary>
    /// How far in from a text box's edges its text's rectangle lies: past its border (1 px, in the accent
    /// while the box has the focus) and its rounded corners (radius 4), and short of its text, which starts
    /// 9 px in (border and padding).
    /// </summary>
    private const float BoxInset = 4;

    /// <summary>How far in from a button's or a menu item's edges the rectangle of its label lies when UI Automation gives the label no element of its own.</summary>
    private const float LabelInset = 4;

    /// <summary>The caret's columns left out of a focused box's text, each side of the caret (the twins' rule).</summary>
    private const float CaretMargin = 2;

    /// <summary>The colour the masked comparison paints over the notice's folder line, in the capture and in the twin's copy (D443, D448).</summary>
    private static readonly Color MaskColour = Color.FromArgb(255, 255, 0, 255);

    /// <summary>
    /// What no step presses, clicks or sends Enter or Space to (design 12.3.2, D444): the first-run
    /// screen's buttons and links that leave the app, About's links and the text dialog's "Copy all".
    /// </summary>
    private static readonly string[] NeverPressedIds =
    [
        "GitSetupDownloadButton", "GitSetupMoreLink", "GitSetupGuideLink", "GitSetupCopyButton", "GitSetupShowFileButton",
        "GitSetupCltButton", "AboutRepositoryLink", "AboutGuideLink", "TextDocumentCopyButton",
    ];

    /// <summary>The names no step presses: the winget confirm's <c>Install</c> and the palette's <c>Open the user guide</c> (design 12.3.2).</summary>
    private static readonly string[] NeverPressedNames = ["Install", "Open the user guide"];

    /// <summary>How the names of what no step presses start: any "Copy …", "Open in browser", "Show in folder", "Open in editor" (design 12.3.2).</summary>
    private static readonly string[] NeverPressedNameStarts = ["Copy", "Open in browser", "Show in folder", "Open in editor"];

    /// <summary>The method under way, which every line of run.txt from <see cref="Note"/> starts with.</summary>
    private string _method = "(no method)";

    private RealWindowRun Run => fixture.Run;

    // ---- Every method's start (design 12.3.1) ----

    /// <summary>
    /// Notes the method's start in run.txt and makes it the prefix of <see cref="Note"/>'s lines; the
    /// result notes its end when disposed. Declare it first, so that its end is noted after the app stopped.
    /// </summary>
    private LaunchNotes NoteLaunch(string method)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        _method = method;
        Note(string.Create(CultureInfo.InvariantCulture, $"start {DateTimeOffset.Now:HH:mm:ss.fff}"));
        return new LaunchNotes(this);
    }

    /// <summary>Adds a line to run.txt, prefixed with the method under way.</summary>
    private void Note(string line) => Run.Note($"{_method}: {line}");

    /// <summary>
    /// Reads Windows' high contrast first and only (D224): on, run.txt and the failure say
    /// <see cref="HighContrastOn"/>; the pass never changes it.
    /// </summary>
    private void AssertDesktopReady()
    {
        if (RealApp.HighContrastIsOn())
        {
            Note(HighContrastOn);
            throw new XunitException(HighContrastOn);
        }
    }

    // ---- Launches ----

    /// <summary>
    /// Starts the real app on <paramref name="data"/>, a new data folder (<see cref="RealWindowRun.NewDataDirectory"/>)
    /// holding whatever the method wrote before the start: with <paramref name="session"/> written as its
    /// session file, or with none when null (a launch that shows the notice, whose twin has none), and
    /// <paramref name="environment"/>'s variables set last. Sized 1100×700 (<see cref="Sized"/>) unless
    /// <paramref name="sized"/> is false. Disposes the app when that fails.
    /// </summary>
    private RealApp Launch(string data, SessionState? session, IReadOnlyDictionary<string, string?>? environment = null, bool sized = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(data);
        var app = session is null ? RealApp.Launch(data, environment) : RealApp.Launch(data, session, environment);
        return sized ? Sized(app) : app;
    }

    /// <summary>Sizes the window to 1100×700 and notes the display once. Disposes the app when that fails. As 7b's.</summary>
    private RealApp Sized(RealApp app)
    {
        try
        {
            app.SetClientSize(DefaultSize.Width, DefaultSize.Height);
            if (!fixture.DisplayNoted)
            {
                Run.NoteDisplay(app);
                fixture.DisplayNoted = true;
            }

            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Waits until <paramref name="repository"/> has opened: its history loaded and its status shown,
    /// the log lines the app writes for both, each within <see cref="LoadTimeout"/>. Disposes the app
    /// when that fails. As 7b's.
    /// </summary>
    private static RealApp Opened(RealApp app, string repository)
    {
        try
        {
            app.WaitForLogLine(AppLogFile.Containing($"] {repository}: loaded "), LoadTimeout);
            app.WaitForLogLine(AppLogFile.WorkingTreeRead(repository), LoadTimeout);
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>A session of one tab, on <paramref name="repository"/> or empty (null), active: the twins' sessions (D442).</summary>
    private static SessionState OneTab(string? repository) => new() { Tabs = [new TabState(repository)], ActiveTab = 0 };

    /// <summary>The name the app gives a scenario's copy, its folder's: <c>changes</c>, <c>graph</c>.</summary>
    private static string NameOf(TempRepo repo) => Path.GetFileName(repo.Path);

    // ---- Pictures (design 12.3.1) ----

    /// <summary>
    /// Rests the pointer at the neutral point unless <paramref name="keepPointer"/> is true (a menu open, a
    /// hover asked for) and then waits until no tooltip shows in the window; captures the client area and
    /// saves it as <c>&lt;name&gt;.png</c>. The caller disposes it.
    /// </summary>
    private Bitmap Capture(RealApp app, string name, bool keepPointer = false)
    {
        ArgumentNullException.ThrowIfNull(app);
        Assert.Matches(PictureName(), name);
        if (!keepPointer)
        {
            app.MoveToNeutral();
            app.WaitFor(() => ToolTipsShown(app).Count == 0, $"no tooltip in the window before {name}", StepTimeout);
        }

        var capture = app.CaptureClient();
        Run.Save(capture, name);
        return capture;
    }

    /// <summary>Asserts that <paramref name="capture"/> matches the scripted twin of the same name within 3 % (D33, D58); run.txt gets the share.</summary>
    private void Compare(Bitmap capture, string name) => Run.AssertMatchesScripted(capture, name, name);

    /// <summary>
    /// A picture of a step: <see cref="Capture"/>, then <paramref name="reads"/> on the capture (each read
    /// notes its value in run.txt and returns what is wrong with it), then <see cref="Compare"/> with the
    /// scripted twin, then the reads' problems asserted. So run.txt holds every read even when the
    /// comparison fails, and a failed read does not hide the comparison's share.
    /// </summary>
    private void Picture(RealApp app, string name, bool keepPointer, Func<Bitmap, IEnumerable<string>> reads)
    {
        ArgumentNullException.ThrowIfNull(reads);
        using var capture = Capture(app, name, keepPointer);
        var problems = reads(capture).ToList();
        Compare(capture, name);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>
    /// The notice's masked comparison (D443 as D448 amends it): the capture's height divided by the
    /// scaling must equal the twin's within 1 px; then <c>InstanceNoticeFolder</c>'s UI Automation bounds,
    /// relative to the client area, are painted <see cref="MaskColour"/> in a copy of the capture and,
    /// divided by the scaling, in a copy of the twin in a temporary folder (the scripted file never
    /// changes), and the two compared at 3 %. The capture itself is saved unpainted by <see cref="Capture"/>.
    /// </summary>
    private void CompareMasked(RealApp app, Bitmap capture, string name)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(capture);
        var scripted = Screens.ScriptedFile(Run.Phase, name);
        Assert.True(File.Exists(scripted), $"The scripted screenshot {scripted} is missing. Run the scripted walk-through first: dotnet test --project tests/VisualCommit.VisualTests.Phase8a.");
        using var twin = new Bitmap(scripted);
        var height = capture.Height / app.Scaling;
        Assert.True(
            Math.Abs(height - twin.Height) <= 1,
            string.Format(CultureInfo.InvariantCulture, I.HeightsDifferMessage, height.ToString("0.##", CultureInfo.InvariantCulture), twin.Height));

        var folder = app.BoundsOf(app.Find(I.FolderId));
        Note(string.Create(CultureInfo.InvariantCulture, $"{name}: the folder line masked at {folder} (logical pixels of the client area)"));
        using var temporary = new TempDirectory("rw8a-masked");
        var maskedTwin = temporary.Combine(name + ".png");
        using (var copy = Painted(twin, folder, 1))
        {
            copy.Save(maskedTwin, ImageFormat.Png);
        }

        using var maskedCapture = Painted(capture, folder, app.Scaling);
        var (difference, inPlace) = maskedCapture.DifferenceFrom(maskedTwin, Path.Combine(Run.Folder, name + "-difference.png"));
        Run.Note(string.Create(CultureInfo.InvariantCulture, $"{name} against scripted {name}, its folder line masked: {difference:P2} of pixels differ ({inPlace:P2} compared in place only)"));
        Assert.True(
            difference <= RealWindowRun.MaxDifference,
            string.Create(CultureInfo.InvariantCulture, $"{name} differs from the scripted screenshot {name}, its folder line masked, in {difference:P2} of its pixels; at most {RealWindowRun.MaxDifference:P0} is allowed. See {name}-difference.png."));
    }

    /// <summary>A copy of <paramref name="source"/> with <paramref name="area"/> (logical pixels), times <paramref name="scaling"/> and rounded outward to whole pixels, painted <see cref="MaskColour"/>.</summary>
    private static Bitmap Painted(Bitmap source, RectangleF area, double scaling)
    {
        var copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(copy);

        // Pixel for pixel, whatever resolution the file records: no scaling, no smoothing.
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
        graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        var left = (int)Math.Floor(area.Left * scaling);
        var top = (int)Math.Floor(area.Top * scaling);
        var right = (int)Math.Ceiling(area.Right * scaling);
        var bottom = (int)Math.Ceiling(area.Bottom * scaling);
        using var brush = new SolidBrush(MaskColour);
        graphics.FillRectangle(brush, left, top, right - left, bottom - top);
        return copy;
    }

    /// <summary>The tooltips on screen in the window (tooltips are drawn on its overlay layer, D105).</summary>
    private static IReadOnlyList<AutomationElement> ToolTipsShown(RealApp app) =>
        [.. app.Window.FindAllDescendants(condition => condition.ByControlType(ControlType.ToolTip))
            .Where(tip => !tip.IsOffscreen && tip.BoundingRectangle is { Width: > 0, Height: > 0 })];

    // ---- Colours in a capture (design 12.3.3) ----

    /// <summary>
    /// The ink of the text drawn in <paramref name="foreground"/> on <paramref name="background"/> inside
    /// <paramref name="area"/> (logical pixels of the client area) of a capture taken at
    /// <paramref name="scaling"/>: <c>TextInk.Of</c>'s rule over the capture's whole pixels inside the
    /// area times the scaling, per channel the extreme toward the foreground.
    /// </summary>
    internal static Color InkOf(Bitmap capture, RectangleF area, double scaling, Color foreground, Color background)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var (r, g, b) = (background.R, background.G, background.B);
        var top = Math.Max(0, (int)Math.Ceiling(area.Top * scaling));
        var bottom = Math.Min(capture.Height, (int)Math.Floor(area.Bottom * scaling));
        var left = Math.Max(0, (int)Math.Ceiling(area.Left * scaling));
        var right = Math.Min(capture.Width, (int)Math.Floor(area.Right * scaling));
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var pixel = capture.GetPixel(x, y);
                r = Toward(r, pixel.R, foreground.R, background.R);
                g = Toward(g, pixel.G, foreground.G, background.G);
                b = Toward(b, pixel.B, foreground.B, background.B);
            }
        }

        return Color.FromArgb(r, g, b);
    }

    /// <summary>
    /// Reads a text's ink (<see cref="InkOf"/>) and notes it in run.txt; returns what is wrong with it by
    /// <c>TextInk</c>'s rule: further than <see cref="TextInk.Tolerance"/> from <paramref name="foreground"/>
    /// in a channel, or within it of <paramref name="replaced"/> in every channel. A null
    /// <paramref name="area"/> is a text no rectangle holds alone: run.txt says so and the comparison with
    /// the twin carries its colour (design 12.3.3). A miss is reported, never met by a wider tolerance.
    /// </summary>
    private IEnumerable<string> ReadInk(Bitmap capture, double scaling, string what, RectangleF? area, AvaloniaColor foreground, AvaloniaColor background, AvaloniaColor? replaced)
    {
        if (area is not { } rectangle)
        {
            Note($"{what}: no rectangle holds the text alone; the comparison with the twin carries its colour.");
            return [];
        }

        var ink = InkOf(capture, rectangle, scaling, Drawn(foreground), Drawn(background));
        var expected = Drawn(foreground);
        var problems = new List<string>();
        if (Distance(ink, expected) > TextInk.Tolerance)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The ink of {what} at {rectangle} is {Hex(ink)}, not within {TextInk.Tolerance} of {Hex(expected)} in each channel."));
        }

        if (replaced is { } old && Distance(ink, Drawn(old)) <= TextInk.Tolerance)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"The ink of {what} at {rectangle} is {Hex(ink)}, within {TextInk.Tolerance} of the colour it replaced, {Hex(Drawn(old))}."));
        }

        Note(string.Create(
            CultureInfo.InvariantCulture,
            $"{what}: ink {Hex(ink)} in {rectangle}, expected {Hex(expected)} on {Hex(Drawn(background))}{(replaced is { } was ? ", replaced " + Hex(Drawn(was)) : string.Empty)}: {(problems.Count == 0 ? "held" : "not held")}"));
        return problems;
    }

    /// <summary>
    /// Reads the colour at <paramref name="point"/> (logical pixels of the client area), a spot clear of
    /// text, and notes it; returns a problem when it lies further than <see cref="SpotTolerance"/> from
    /// <paramref name="expected"/> in a channel (<see cref="RealWindowRun.AssertColour"/>'s rule).
    /// </summary>
    private IEnumerable<string> ReadSpot(Bitmap capture, double scaling, string what, PointF point, AvaloniaColor expected)
    {
        var actual = capture.ColourAt(point.X, point.Y, scaling);
        var held = Distance(actual, Drawn(expected)) <= SpotTolerance;
        Note(string.Create(CultureInfo.InvariantCulture, $"{what} at ({point.X:0.#}, {point.Y:0.#}): {Hex(actual)}, expected {Hex(Drawn(expected))}: {(held ? "held" : "not held")}"));
        return held
            ? []
            : [string.Create(CultureInfo.InvariantCulture, $"The {what} at ({point.X:0.#}, {point.Y:0.#}) is {Hex(actual)} on screen, expected {Hex(Drawn(expected))}. If the colour is unrelated to the app, the desktop may be locked or the window covered.")];
    }

    /// <summary>
    /// Waits until the colour at <paramref name="point"/> (logical pixels of the client area) is
    /// <paramref name="expected"/>, within <see cref="SpotTolerance"/>, in a fresh capture each time, at most
    /// <see cref="StepTimeout"/>: a hover, a selection or a theme drawn. run.txt names the condition.
    /// </summary>
    private void WaitForSpot(RealApp app, string what, PointF point, AvaloniaColor expected)
    {
        var seen = Color.Empty;
        try
        {
            app.WaitFor(
                () =>
                {
                    using var capture = app.CaptureClient();
                    seen = capture.ColourAt(point.X, point.Y, app.Scaling);
                    return Distance(seen, Drawn(expected)) <= SpotTolerance;
                },
                $"{what}: {Hex(Drawn(expected))} at ({point.X:0.#}, {point.Y:0.#})",
                StepTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"{ex.Message} It read {Hex(seen)}.", ex);
        }

        Note(string.Create(CultureInfo.InvariantCulture, $"waited for {what}: {Hex(seen)} at ({point.X:0.#}, {point.Y:0.#})"));
    }

    /// <summary>
    /// The rectangle of a text box's text: its UI Automation bounds inset by <see cref="BoxInset"/> on every
    /// side, past its border, and on the right by <paramref name="rightPadding"/> where the box draws
    /// something else there (<c>CommitSummary</c>'s counter lies in its 40 px right padding).
    /// </summary>
    private static RectangleF BoxTextArea(RealApp app, AutomationElement box, float rightPadding = BoxInset)
    {
        var bounds = app.BoundsOf(box);
        var right = bounds.Right - Math.Max(BoxInset, rightPadding);
        return RectangleF.FromLTRB(bounds.Left + BoxInset, bounds.Top + BoxInset, right, bounds.Bottom - BoxInset);
    }

    /// <summary>
    /// The rectangle of the text typed into a focused box, less the caret's column: the box's text area
    /// (<see cref="BoxTextArea"/>) up to <see cref="CaretMargin"/> left of the caret, which stands at the
    /// text's end. The caret is the rightmost column of the area that holds a pixel more than
    /// <see cref="TextInk.Tolerance"/> from the box's colour in a channel; while it blinks off, that column
    /// is the last letter's, and the area loses only that letter's edge.
    /// </summary>
    private RectangleF TypedArea(RealApp app, Bitmap capture, AutomationElement box, AvaloniaColor boxColour, string what)
    {
        var area = BoxTextArea(app, box);
        var scaling = app.Scaling;
        var background = Drawn(boxColour);
        var top = (int)Math.Ceiling(area.Top * scaling);
        var bottom = Math.Min(capture.Height, (int)Math.Floor(area.Bottom * scaling));
        var left = (int)Math.Ceiling(area.Left * scaling);
        for (var x = Math.Min(capture.Width, (int)Math.Floor(area.Right * scaling)) - 1; x >= left; x--)
        {
            for (var y = top; y < bottom; y++)
            {
                if (Distance(capture.GetPixel(x, y), background) > TextInk.Tolerance)
                {
                    var caret = (float)(x / scaling);
                    Note(string.Create(CultureInfo.InvariantCulture, $"{what}: the caret's column (or the text's last) at x {caret:0.#}"));
                    return RectangleF.FromLTRB(area.Left, area.Top, Math.Max(area.Left, caret - CaretMargin), area.Bottom);
                }
            }
        }

        Note($"{what}: no ink in the box's text area");
        return area;
    }

    /// <summary>
    /// The rectangle of <paramref name="text"/> inside <paramref name="scope"/> (a row, a button, a menu
    /// item): the bounds of a UI Automation text element named so, inside the scope or, where UI
    /// Automation lists the scope's texts elsewhere, on screen with its vertical centre inside the scope's
    /// bounds; null when there is none (the comparison then carries its colour).
    /// </summary>
    private static RectangleF? TextArea(RealApp app, AutomationElement scope, string text)
    {
        var bounds = app.BoundsOf(scope);
        bool Shown(AutomationElement element) => !element.IsOffscreen && element.BoundingRectangle is { Width: > 0, Height: > 0 };
        var inside = scope.FindAllDescendants(condition => condition.ByControlType(ControlType.Text))
            .FirstOrDefault(element => element.Name == text && Shown(element));
        if (inside is not null)
        {
            return app.BoundsOf(inside);
        }

        var beside = app.Window.FindAllDescendants(condition => condition.ByControlType(ControlType.Text))
            .Where(element => element.Name == text && Shown(element))
            .Select(app.BoundsOf)
            .FirstOrDefault(area => area.Top + (area.Height / 2) > bounds.Top && area.Top + (area.Height / 2) < bounds.Bottom
                && area.Left >= bounds.Left - 0.5f && area.Right <= bounds.Right + 0.5f);
        return beside.IsEmpty ? null : beside;
    }

    /// <summary>
    /// The rectangle of a button's or menu item's label <paramref name="text"/>: its text element
    /// (<see cref="TextArea"/>), else the element's bounds inset by <see cref="LabelInset"/>, ended left
    /// of a key text (<c>ContextMenuItemKey</c>) drawn on the same line, which lies toward the foreground.
    /// </summary>
    private RectangleF LabelArea(RealApp app, AutomationElement element, string text, string what)
    {
        if (TextArea(app, element, text) is { } area)
        {
            return area;
        }

        var bounds = app.BoundsOf(element);
        var right = bounds.Right - LabelInset;
        foreach (var key in app.ShownElements("ContextMenuItemKey").Select(app.BoundsOf))
        {
            var centre = key.Top + (key.Height / 2);
            if (centre > bounds.Top && centre < bounds.Bottom && key.Left < right)
            {
                right = key.Left - CaretMargin;
            }
        }

        Note($"{what}: UI Automation gives the label no element; read in the element's bounds inset by {LabelInset}, left of any key text");
        return RectangleF.FromLTRB(bounds.Left + LabelInset, bounds.Top + LabelInset, right, bounds.Bottom - LabelInset);
    }

    /// <summary>A spot of a row clear of its text and its buttons: 2 px in from its left edge, at its middle (the twins' <c>ClearOf</c>).</summary>
    private static PointF ClearOf(RealApp app, AutomationElement row)
    {
        var bounds = app.BoundsOf(row);
        return new PointF(bounds.Left + 2, bounds.Top + (bounds.Height / 2));
    }

    /// <summary>A spot of a button's left padding, clear of its centred label: 4 px in, at its middle (the twins' <c>LeftPadding</c>).</summary>
    private static PointF LeftPaddingOf(RealApp app, AutomationElement button)
    {
        var bounds = app.BoundsOf(button);
        return new PointF(bounds.Left + 4, bounds.Top + (bounds.Height / 2));
    }

    /// <summary>A colour of the expectations' palettes as the screen capture gives colours.</summary>
    private static Color Drawn(AvaloniaColor colour) => Color.FromArgb(colour.A, colour.R, colour.G, colour.B);

    /// <summary>The largest difference of two colours in one channel.</summary>
    private static int Distance(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    /// <summary>The channel value furthest toward the foreground's so far (<c>TextInk</c>'s rule).</summary>
    private static byte Toward(byte best, byte pixel, byte foreground, byte background) =>
        foreground >= background ? Math.Max(best, pixel) : Math.Min(best, pixel);

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    // ---- Real input (design 12.3.2, 12.3.3; D444) ----

    /// <summary>
    /// Fails when <paramref name="target"/> is something no step presses, clicks or sends Enter or Space to
    /// (design 12.3.2): an id of <see cref="NeverPressedIds"/>, a name of <see cref="NeverPressedNames"/>
    /// or one that starts as <see cref="NeverPressedNameStarts"/> do. A target whose id and name cannot be
    /// read fails too: what is not known is not pressed.
    /// </summary>
    private static void AssertNeverPressed(AutomationElement target)
    {
        ArgumentNullException.ThrowIfNull(target);
        string? id;
        string? name;
        try
        {
            id = target.Properties.AutomationId.ValueOrDefault;
            name = target.Properties.Name.ValueOrDefault;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or FlaUI.Core.Exceptions.FlaUIException)
        {
            throw new XunitException($"The element to press could not be read ({ex.GetType().Name}), so it is not pressed (D444).");
        }

        var refused = (id is not null && NeverPressedIds.Contains(id, StringComparer.Ordinal))
            || (name is not null && (NeverPressedNames.Contains(name, StringComparer.Ordinal)
                || NeverPressedNameStarts.Any(start => name.StartsWith(start, StringComparison.Ordinal))));
        Assert.False(refused, $"A step was about to press {id ?? "(no id)"} \"{name}\", which leaves the app: no step presses it (D444).");
    }

    /// <summary>Fails for a key no step sends: F1, which opens the user guide in the browser (design 12.3.2).</summary>
    private static void AssertNeverPressed(VirtualKeyShort key) =>
        Assert.False(key == VirtualKeyShort.F1, "A step was about to press F1, which opens the user guide in the browser: no step sends it (D444).");

    /// <summary>A real click on <paramref name="target"/>'s centre, after <see cref="AssertNeverPressed(AutomationElement)"/>.</summary>
    private static void Press(RealApp app, AutomationElement target)
    {
        AssertNeverPressed(target);
        app.ClickWithMouse(target);
    }

    /// <summary>
    /// A key or a combination on the real keyboard (<see cref="RealApp.PressKeys"/>): never F1; an Enter or
    /// a Space only while the focused element is none of what no step presses; and nothing while the
    /// window is not the foreground window, where the keys would reach another window.
    /// </summary>
    private static void Keys(RealApp app, params VirtualKeyShort[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            AssertNeverPressed(key);
        }

        if (keys[^1] is VirtualKeyShort.RETURN or VirtualKeyShort.SPACE)
        {
            AssertNeverPressed(app.FocusedElement() ?? throw new XunitException($"No element has the keyboard focus, so {keys[^1]} is not sent (D444)."));
        }

        Assert.True(app.ForegroundIsThis, $"The app's window is not the foreground window: {string.Join("+", keys)} would reach another window.");
        app.PressKeys(keys);
    }

    /// <summary>Text typed on the real keyboard (<see cref="RealApp.TypeText"/>), only while the window is the foreground window.</summary>
    private static void TypeKeys(RealApp app, string text)
    {
        Assert.True(app.ForegroundIsThis, $"The app's window is not the foreground window: typing \"{text}\" would reach another window.");
        app.TypeText(text);
    }

    /// <summary>run.txt's start and end of one method (<see cref="NoteLaunch"/>): the end is noted when it is disposed.</summary>
    private sealed class LaunchNotes(Phase8aRealWindowPass pass) : IDisposable
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public void Dispose() =>
            pass.Note(string.Create(CultureInfo.InvariantCulture, $"end {DateTimeOffset.Now:HH:mm:ss.fff}, after {_clock.Elapsed.TotalSeconds:0.0} s"));
    }
}

/// <summary>
/// The 8a pass's order and picture names (docs/phase-8a-design.md 12.4), with no app and no window: the
/// class constructs no fixture of the pass and touches nothing of the desktop. It runs in the whole
/// pass, outside D246's filter, and alone with <c>--filter-class "*Phase8aOrderTests"</c>.
/// </summary>
public class Phase8aOrderTests
{
    /// <summary>
    /// <see cref="Phase8aOrder"/> names thirteen methods, each once, the expectations' launches at their
    /// places; every <c>[Fact]</c> of <see cref="Phase8aRealWindowPass"/> is one of them. While the part
    /// files are written one by one, the order names methods that do not exist yet; the last part's
    /// commit makes the facts equal the order's thirteen.
    /// </summary>
    [Fact]
    public void The_order_names_every_fact_of_the_pass_once()
    {
        var facts = typeof(Phase8aRealWindowPass).GetMethods()
            .Where(method => method.GetCustomAttributes(inherit: true).OfType<IFactAttribute>().Any())
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        var order = Phase8aOrder.Methods;

        Assert.Equal(13, order.Count);
        Assert.Equal(order.Count, order.Distinct(StringComparer.Ordinal).Count());
        Assert.Subset(order.ToHashSet(StringComparer.Ordinal), facts);
        Assert.Equal(
            [L.ChangesLaunch.Method, L.GraphLaunch.Method, K.ChangesLaunch.Method, K.HistoryLaunch.Method, U.GraphRowLaunch.Method],
            [order[0], order[1], order[10], order[11], order[12]]);
    }

    /// <summary>
    /// Every capture the pass takes, by its part's list, matches the picture names' pattern (design 0.3),
    /// and no name is taken twice: 32 in all, A3's at 1920×1080 among them (design 12.6).
    /// </summary>
    [Fact]
    public void Every_capture_name_matches_the_pattern()
    {
        string[] captures =
        [
            .. L.ChangesLaunch.Captures, .. L.GraphLaunch.Captures, .. A.RealWindowPictures, .. B.RealWindowPictures,
            .. I.RealWindowPictures, .. F.RealWindowPictures, .. K.ChangesLaunch.Captures, .. K.HistoryLaunch.Captures,
        ];

        Assert.All(captures, name => Assert.Matches(PictureName(), name));
        Assert.Equal(32, captures.Length);
        Assert.Equal(captures.Length, captures.Distinct(StringComparer.Ordinal).Count());
    }
}
