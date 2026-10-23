// Part L of phase 6's scripted walk-through (docs/test-reports/phase-6.md, "Part L: the look: every
// main screen in both themes, the light colours, the long folder and the tab strip"): checks L26 to
// L33 of design 17.4 (L34 is the real window's alone), in the project
// VisualCommit.VisualTests.Phase6Look of its own (the report's ruling 1), which compiles the base,
// the support and the common expectations of VisualCommit.VisualTests.Phase6. This class is part
// L's alone; its expected texts and states are in Phase6Expectations.L.cs
// (Phase6Expectations.PartL), held against git and against each other in
// Phase6ExpectationTests.L.cs. One test method per app start, as "Test methods and app starts
// (part L)" names them: Phase6Checks.L.Sweep.cs holds L26 and L27, Phase6Checks.L.Pages.cs L28
// and L29, Phase6Checks.L.Overlays.cs L30 and L31 (its two methods), Phase6Checks.L.LongFolder.cs
// L32 and Phase6Checks.L.Strip.cs L33. Every pair of the sweep is one ShotInBothThemesAsync (the
// support's, 17.2, LD1, LD19), whose callback asserts the step's expectations once per variant,
// the colours by variant. This file holds what the methods share: the start with System and the
// fake system dark, the colours read from a picture, the toolbar's buttons, and small lookups.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase6;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6Look;

/// <summary>The checks of part L: the look (L26 to L33).</summary>
public sealed partial class Phase6ChecksL : Phase6Checks
{
    // ---- The start (part L's "Window, theme, scenario") ----------------------------------------

    /// <summary>Writes <c>"theme": "System"</c> into the data folder's <c>settings.json</c> before a start without a scenario's tab (L26, L32).</summary>
    private static void WriteSystemTheme(TempDirectory data) =>
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { Theme = AppTheme.System });

    /// <summary>
    /// What every sweep method has at its start (part L's conventions, 11.5, 11.6): the start-up line
    /// <c>Theme: System (the system's theme is dark).</c> once, the window dark under System, and
    /// <c>ThemeSwitch</c> with <c>IconSun</c> and <c>Switch to the light theme (stops following the system)</c>.
    /// </summary>
    private static void AssertSystemDarkStart(ShellDriver app)
    {
        Assert.Equal(AppTheme.System, app.Session.Themes.Setting);
        Assert.True(app.SystemTheme.IsDark, "The fake system is not dark at the start.");
        Assert.True(app.ShowsDarkTheme, $"The window shows {app.ShownThemeVariant} at the start, not Dark.");
        Assert.True(
            app.CountLogLines(AppLogFile.Containing(PartL.SystemDarkStart)) == 1,
            $"The start-up log has not one line \"{PartL.SystemDarkStart}\":\n" + string.Join('\n', app.LogLines().Where(line => line.Contains("Theme", StringComparison.Ordinal))));
        AssertThemeSwitch(app, "IconSun", PartL.SwitchToLightSystem);
    }

    /// <summary><c>ThemeSwitch</c>'s icon, tooltip and help text.</summary>
    private static void AssertThemeSwitch(ShellDriver app, string icon, string text)
    {
        var button = ToolbarButton(app, "ThemeSwitch");
        Assert.True(ReferenceEquals(Application.Current!.FindResource(icon), button.IconData), $"ThemeSwitch does not show {icon}.");
        Assert.Equal(text, ToolTip.GetTip(button) as string);
        Assert.Equal(text, AutomationProperties.GetHelpText(button));
    }

    /// <summary>Whether a palette is the dark theme's.</summary>
    private static bool IsDark(Palette palette) => ReferenceEquals(palette, Dark);

    // ---- Colours read from a picture (part L's "Colours") ---------------------------------------

    /// <summary>Whether some whole pixel inside <paramref name="area"/> is exactly <paramref name="colour"/> (phase 1's <c>ContainsColour</c>).</summary>
    private static bool ContainsColour(Screenshot picture, Rect area, Color colour)
    {
        for (var y = (int)Math.Floor(area.Top); y < (int)Math.Ceiling(area.Bottom); y++)
        {
            for (var x = (int)Math.Floor(area.Left); x < (int)Math.Ceiling(area.Right); x++)
            {
                if (picture.PixelAt(x, y) == colour)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>A text's (or an icon's) colour as the report writes it: "contains <paramref name="colour"/>" within <paramref name="area"/>.</summary>
    private static void AssertContains(Screenshot picture, Rect area, Color colour, string what) =>
        Assert.True(ContainsColour(picture, area, colour), $"No pixel of {what} at {area} is {Hex(colour)}.");

    /// <summary>A control's drawn area "contains" <paramref name="colour"/>.</summary>
    private static void AssertContains(ShellDriver app, Screenshot picture, Visual control, Color colour, string what) =>
        AssertContains(picture, app.BoundsOf(control), colour, what);

    /// <summary>No pixel inside <paramref name="area"/> is <paramref name="colour"/>.</summary>
    private static void AssertNoPixel(Screenshot picture, Rect area, Color colour, string what) =>
        Assert.False(ContainsColour(picture, area, colour), $"{what} at {area} has a pixel of {Hex(colour)}.");

    /// <summary>A pixel within <paramref name="tolerance"/> per channel of <paramref name="expected"/> (a translucent thing's blend).</summary>
    private static void AssertNear(Color expected, Screenshot picture, Point point, int tolerance, string what)
    {
        var actual = picture.PixelAt(point);
        Assert.True(
            Math.Abs(actual.R - expected.R) <= tolerance && Math.Abs(actual.G - expected.G) <= tolerance && Math.Abs(actual.B - expected.B) <= tolerance,
            $"The {what} at {point} is {Hex(actual)}, not within {tolerance} of {Hex(expected)}.");
    }

    /// <summary>A small text's ink (<see cref="TextInk"/>): within 8 of <paramref name="foreground"/>, and more than 8 from <paramref name="replaced"/> when given.</summary>
    private static void AssertInk(Screenshot picture, Rect area, Color foreground, Color background, string what, Color? replaced = null)
    {
        var problems = TextInk.Problems(picture, area, foreground, background, replaced, what);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>A colour as #RRGGBB.</summary>
    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    // ---- The toolbar ---------------------------------------------------------------------------

    /// <summary>A toolbar button by its id: enabled or not, and its tooltip (null: none), with one its help text the same.</summary>
    private static void AssertButton(ShellDriver app, string id, bool enabled, string? toolTip)
    {
        var button = ToolbarButton(app, id);
        Assert.True(button.IsEffectivelyEnabled == enabled, $"{id} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}, expected {(enabled ? "enabled" : "disabled")}.");
        Assert.True(ToolTip.GetTip(button) as string == toolTip, $"{id}'s tooltip is \"{ToolTip.GetTip(button)}\", expected \"{toolTip}\".");
        if (toolTip is not null)
        {
            Assert.Equal(toolTip, AutomationProperties.GetHelpText(button));
        }
    }

    /// <summary>A toolbar button's state where the report states only whether it is enabled and, for some, the tooltip.</summary>
    private static void AssertToolbar(ShellDriver app, IEnumerable<(string Id, bool Enabled, string? ToolTip)> buttons)
    {
        foreach (var (id, enabled, toolTip) in buttons)
        {
            var button = ToolbarButton(app, id);
            Assert.True(button.IsEffectivelyEnabled == enabled, $"{id} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}, expected {(enabled ? "enabled" : "disabled")}.");
            if (toolTip is not null)
            {
                Assert.Equal(toolTip, ToolTip.GetTip(button) as string);
            }
        }
    }

    // ---- Lookups -------------------------------------------------------------------------------

    /// <summary>The visible control with this automation id, or null while none is in the window.</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>The visible control with this automation id inside <paramref name="scope"/>, or null.</summary>
    private static Control? Shown(Visual scope, string automationId) =>
        scope.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>The visible control with this automation id inside the top dialog, as <typeparamref name="T"/>; throws when none.</summary>
    private static T InDialog<T>(ShellDriver app, string automationId)
        where T : Control =>
        Shown(app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open."), automationId) as T
            ?? throw new Xunit.Sdk.XunitException($"The dialog on top shows no {typeof(T).Name} {automationId}.");

    /// <summary>The text of a text block in the top dialog.</summary>
    private static string? DialogText(ShellDriver app, string automationId) => InDialog<Control>(app, automationId) switch
    {
        TextBox box => box.Text ?? string.Empty,
        TextBlock block => block.Text,
        ContentControl { Content: string content } => content,
        var other => AutomationProperties.GetName(other),
    };

    /// <summary>The visible buttons of a view, left to right, by their labels.</summary>
    private static List<string> ButtonsOf(ShellDriver app, Visual view) =>
    [
        .. view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && (button.Content is string || button.Content is TextBlock))
            .OrderBy(button => app.BoundsOf(button).X)
            .Select(button => button.Content as string ?? ((TextBlock)button.Content!).Text ?? string.Empty),
    ];

    /// <summary>A dialog's card: its bounds, its width and left (±2), and its background the panel's.</summary>
    private static Rect AssertCard(ShellDriver app, Screenshot picture, Palette palette, double width, double left)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var card = view.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("dialogCard") && border.IsEffectivelyVisible);
        var bounds = app.BoundsOf(card);
        Assert.True(Math.Abs(bounds.Width - width) <= PartL.Tolerance && Math.Abs(bounds.Left - left) <= PartL.Tolerance, $"The card is at {bounds}, not {width} wide from x {left}.");
        AssertColour(palette.Panel, picture, bounds.TopLeft + new Point(10, 10), "card's background (VcPanelBackgroundBrush)");
        return bounds;
    }

    /// <summary>The backdrop over the graph area at (300, 600) (L27 step 6), within ±8 of its blend.</summary>
    private static void AssertBackdrop(Screenshot picture, Palette palette) =>
        AssertNear(PartL.BackdropPoint(IsDark(palette)), picture, PartL.BackdropSample, PartL.BlendTolerance, "backdrop over the graph area");

    /// <summary>Waits until the focus is on the control with this automation id in the top dialog.</summary>
    private static Task WaitForDialogFocusAsync(ShellDriver app, string automationId) =>
        app.WaitForAsync(() => app.TopDialogView is { } view && Shown(view, automationId) is { IsFocused: true }, $"{automationId} to have the keyboard focus");
}
