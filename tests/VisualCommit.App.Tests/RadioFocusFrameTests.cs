using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <c>VcRadioButton</c>'s keyboard frame (<c>Theme/Dialogs.axaml</c>), as <see cref="LinkFocusFrameTests"/>
/// holds the link's: while a radio button has the keyboard's frame, the frame is only drawn, never
/// hit, so a pointer on it reaches what lies under it, and a hover or a click just outside the
/// button is none on it (a frame that took the pointer checked Hard, the Reset dialog's destructive
/// choice, on a click in the gap above it). The frame moves nothing and draws exactly its own box
/// (inside it for Theme's options, whose frame the settings screen's scroller cuts off above them),
/// and in the Reset dialog no other control lies under it. In the Reset dialog (three stretched
/// radios 10 apart) and the settings screen's Pull options (left-aligned, 8 apart) and Theme options
/// (left-aligned, in a row, the section's note 4 below them).
/// The headless app; <see cref="TestPictures"/> saves its pictures when asked.
/// </summary>
public class RadioFocusFrameTests
{
    public static TheoryData<string> ResetRadios => ["ResetSoft", "ResetMixed", "ResetHard"];

    public static TheoryData<string, string> SettingsRadios => new()
    {
        { "General", "SettingsThemeDark" },
        { "General", "SettingsThemeLight" },
        { "General", "SettingsThemeSystem" },
        { "Git", "PullNotSet" },
        { "Git", "PullMerge" },
        { "Git", "PullRebase" },
        { "Git", "PullFastForwardOnly" },
    };

    [AvaloniaTheory]
    [MemberData(nameof(ResetRadios))]
    public async Task A_reset_radios_frame_takes_no_pointer_moves_nothing_and_covers_no_other_control(string id)
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnResetAsync(data);
        var radio = Radio(app, id);
        var card = app.TopDialogView!.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("dialogCard"));
        var places = Places(app, card);
        var unfocused = HitsOnRing(app, radio);
        TestPictures.Save(app, $"radio-frame-{id}-unfocused");

        FocusByTab(app, radio, id);
        TestPictures.Save(app, $"radio-frame-{id}-focused");

        AssertRingHitsAsWithoutFocus(app, radio, unfocused, id);
        Assert.Equal(places, Places(app, card));
        AssertDrawsItsFrame(app, radio, id);
        AssertClearOfNeighbours(app, card, radio);
    }

    [AvaloniaTheory]
    [MemberData(nameof(SettingsRadios))]
    public async Task A_settings_radios_frame_takes_no_pointer_and_moves_nothing(string section, string id)
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnSettingsAsync(data, section == "Git" ? SettingsSection.Git : SettingsSection.General);
        var radio = Radio(app, id);
        radio.BringIntoView();
        app.Settle();
        var scroller = (ScrollViewer)app.FindByAutomationId(app.TopDialogView!, "SettingsSection");
        var places = Places(app, scroller);
        var unfocused = HitsOnRing(app, radio);
        TestPictures.Save(app, $"radio-frame-{id}-unfocused");

        FocusByTab(app, radio, id);
        TestPictures.Save(app, $"radio-frame-{id}-focused");

        AssertRingHitsAsWithoutFocus(app, radio, unfocused, id);
        Assert.Equal(places, Places(app, scroller));

        // Theme's options stand at the top of the section's scroller, which cuts off the frame's
        // 4 px above them: the frame draws inside its box there, whole elsewhere.
        AssertDrawsItsFrame(app, radio, id, exactly: section == "Git");
    }

    /// <summary>
    /// The pointer resting on, then clicking, the frame's outer line left of Hard's ring, its inner line
    /// above Hard and its outer line below it, in the gaps between the options, each time with Hard
    /// focused again first as Tab focuses it: the point shows the frame, Hard is not hovered, and Mixed
    /// stays the checked option.
    /// </summary>
    [AvaloniaFact]
    public async Task A_click_on_the_frame_of_the_focused_hard_option_leaves_mixed_checked()
    {
        using var data = new TempDirectory("data");
        using var app = await StartOnResetAsync(data);
        var dialog = Assert.IsType<ResetDialogViewModel>(app.Session.Services.DialogHost.Current);
        var hard = Radio(app, "ResetHard");
        var frame = PixelBox(app, FocusFrame(hard));
        var bounds = PixelBox(app, hard);
        var ring = app.BoundsOf(hard.GetVisualDescendants().OfType<Border>().First(border => border.Name == "Ring"));
        var middleX = Math.Floor((frame.Left + frame.Right) / 2.0) + 0.5;
        var points = new (string Where, Point At)[]
        {
            ("the outer line 6 left of the ring", new Point(Math.Floor(ring.Left) - 6 + 0.5, Math.Floor(ring.Center.Y) + 0.5)),
            ("the inner line above the option", new Point(middleX, frame.Top + 2 + 0.5)),
            ("the outer line 5 below the option", new Point(middleX, bounds.Bottom + 4 + 0.5)),
        };
        var background = Colour(app, "VcPanelBackgroundBrush");
        foreach (var (where, at) in points)
        {
            app.MoveToNeutral();
            FocusByTab(app, hard, "ResetHard");
            Assert.True(app.Capture().PixelAt(at) != background, Text($"{where} of Hard's frame at ({at}) shows the background: the point is not on the frame."));
            Assert.False(bounds.Contains((int)at.X, (int)at.Y), Text($"{where} at ({at}) lies inside Hard's bounds {bounds}."));
            app.MoveMouse(at);
            Assert.False(hard.IsPointerOver, Text($"The pointer on {where} at ({at}) hovers Hard."));
            app.Click(at);
            app.Settle();
            Assert.True(dialog.IsMixed && !dialog.IsHard, Text($"A click on {where} at ({at}) checked {(dialog.IsHard ? "Hard" : dialog.IsSoft ? "Soft" : "nothing")}."));
        }

        app.MoveToNeutral();
    }

    /// <summary>The Reset dialog over the empty tab, dark, Mixed checked, the pointer at rest.</summary>
    private static async Task<ShellDriver> StartOnResetAsync(TempDirectory data)
    {
        var app = ShellDriver.Start(data.Path);
        try
        {
            await app.WaitUntilReadyAsync();
            var dialog = new ResetDialogViewModel("main", "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "Add the core");
            _ = app.Session.Services.DialogHost.ShowAsync(dialog);
            await app.WaitForAsync(() => app.TopDialogView is { } view && view.DataContext == dialog && app.FocusedElement is not null, "the Reset dialog");
            Assert.True(dialog.IsMixed);
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

    /// <summary>The settings screen on <paramref name="section"/>, its reads ended, the pointer at rest.</summary>
    private static async Task<ShellDriver> StartOnSettingsAsync(TempDirectory data, SettingsSection section)
    {
        var app = ShellDriver.Start(data.Path);
        try
        {
            await app.WaitUntilReadyAsync();
            app.Session.Shell.OpenSettingsCommand.Execute(section);
            await app.WaitForAsync(() => app.SettingsDialog is { } dialog && dialog.SelectedSection == section, "the settings screen");
            await app.WaitForSettingsReadsAsync();
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

    private static RadioButton Radio(ShellDriver app, string id) => (RadioButton)app.FindByAutomationId(app.TopDialogView!, id);

    private static Border FocusFrame(RadioButton radio) => radio.GetVisualDescendants().OfType<Border>().First(border => border.Name == "FocusFrame");

    private static void FocusByTab(ShellDriver app, RadioButton radio, string id)
    {
        radio.BringIntoView();
        Assert.True(radio.Focus(NavigationMethod.Tab), $"{id} takes no focus.");
        app.Settle();
        Assert.True(app.FocusedAutomationId() == id && app.FocusVisible, $"{id} is not focused with the keyboard's frame.");
    }

    /// <summary>The frame's ring: every whole pixel of the frame's box outside the button's bounds, where only the frame can take the pointer.</summary>
    private static List<(int X, int Y)> Ring(ShellDriver app, RadioButton radio)
    {
        var frame = PixelBox(app, FocusFrame(radio));
        var bounds = PixelBox(app, radio);
        var ring = new List<(int X, int Y)>();
        for (var y = frame.Top; y < frame.Bottom; y++)
        {
            for (var x = frame.Left; x < frame.Right; x++)
            {
                if (!bounds.Contains(x, y))
                {
                    ring.Add((x, y));
                }
            }
        }

        return ring;
    }

    /// <summary>What a hit test finds at the centre of every pixel of the frame's ring, after the window has drawn what it shows.</summary>
    private static Dictionary<(int X, int Y), IInputElement?> HitsOnRing(ShellDriver app, RadioButton radio)
    {
        app.Settle();
        return Ring(app, radio).ToDictionary(pixel => pixel, pixel => app.Window.InputHitTest(new Point(pixel.X + 0.5, pixel.Y + 0.5)));
    }

    /// <summary>
    /// <paramref name="radio"/>, focused with the keyboard's frame, changes nothing a hit test finds on
    /// its frame's ring against <paramref name="unfocused"/>, and nothing there is the button or inside it.
    /// </summary>
    private static void AssertRingHitsAsWithoutFocus(ShellDriver app, RadioButton radio, Dictionary<(int X, int Y), IInputElement?> unfocused, string id)
    {
        Assert.True(app.FocusVisible, $"{id} has the focus without the keyboard's frame.");
        var focused = HitsOnRing(app, radio);
        Assert.Equal(unfocused.Keys.Order(), focused.Keys.Order());
        Assert.NotEmpty(focused);
        var wrong = focused
            .Where(hit => InRadio(radio, hit.Value) || !ReferenceEquals(hit.Value, unfocused[hit.Key]))
            .Select(hit => Text($"({hit.Key.X + 0.5}, {hit.Key.Y + 0.5}): {Describe(hit.Value)}, without the focus {Describe(unfocused[hit.Key])}"))
            .ToList();
        Assert.True(
            wrong.Count == 0,
            Text($"While {id} has the keyboard's frame, {wrong.Count} of the {focused.Count} pixels of the frame's ring at {PixelBox(app, FocusFrame(radio))} hit something else than without the focus, or the radio button; the first: {string.Join("; ", wrong.Take(4))}."));
    }

    /// <summary>
    /// The focused button draws in its frame's box and nothing outside it, exactly that box when
    /// <paramref name="exactly"/> (the frame whole); its picture against the one with its opacity 0.
    /// </summary>
    private static void AssertDrawsItsFrame(ShellDriver app, RadioButton radio, string id, bool exactly = true)
    {
        var frame = PixelBox(app, FocusFrame(radio));
        var with = app.Capture();
        radio.Opacity = 0;
        Screenshot without;
        try
        {
            without = app.Capture();
        }
        finally
        {
            radio.Opacity = 1;
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

        var drawn = new Box(left, top, right, bottom);
        Assert.True(
            exactly ? drawn == frame : left >= frame.Left && top >= frame.Top && right <= frame.Right && bottom <= frame.Bottom && left < right,
            Text($"{id} focused draws in {drawn}, not {(exactly ? "exactly" : "inside")} its frame's box {frame}."));
    }

    /// <summary>
    /// The focused button's frame meets no other control of <paramref name="scope"/> that is neither its
    /// ancestor nor its descendant, nor another radio button's frame, which draws nothing without the focus.
    /// </summary>
    private static void AssertClearOfNeighbours(ShellDriver app, Visual scope, RadioButton radio)
    {
        var frame = app.BoundsOf(FocusFrame(radio));
        var neighbours = scope.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0 && control.Bounds.Height > 0)
            .Where(control => !control.GetSelfAndVisualAncestors().Contains(radio) && !radio.GetVisualAncestors().Contains(control))
            .Where(control => !(control.GetSelfAndVisualAncestors().OfType<RadioButton>().FirstOrDefault() is { } other && control is Border { Name: "FocusFrame" or "FocusFrameInner" } && !ReferenceEquals(other, radio)))
            .Where(control => app.BoundsOf(control).Intersects(frame))
            .Select(control => Text($"{control.GetType().Name} {control.Name ?? AutomationProperties.GetAutomationId(control)} at {app.BoundsOf(control)}"))
            .ToList();
        Assert.True(neighbours.Count == 0, Text($"The frame of {AutomationProperties.GetAutomationId(radio)} at {frame} meets {string.Join("; ", neighbours)}."));
    }

    /// <summary>The place of every shown control in <paramref name="scope"/>, in the window, in tree order.</summary>
    private static IReadOnlyList<string> Places(ShellDriver app, Visual scope) =>
        [.. scope.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible).Select(control => Text($"{control.GetType().Name} {control.Name} {app.BoundsOf(control)}"))];

    /// <summary>The whole pixels <paramref name="control"/>'s bounds cover in the window.</summary>
    private static Box PixelBox(ShellDriver app, Visual control)
    {
        var bounds = app.BoundsOf(control);
        return new Box((int)Math.Floor(bounds.Left), (int)Math.Floor(bounds.Top), (int)Math.Ceiling(bounds.Right), (int)Math.Ceiling(bounds.Bottom));
    }

    private static bool InRadio(RadioButton radio, IInputElement? hit) => hit is Visual visual && visual.GetSelfAndVisualAncestors().Contains(radio);

    private static string Describe(IInputElement? hit) => hit is null ? "nothing" : Text($"{hit.GetType().Name} {(hit as Control)?.Name}").TrimEnd();

    private static Color Colour(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>A box of whole pixels, its right and bottom exclusive.</summary>
    private sealed record Box(int Left, int Top, int Right, int Bottom)
    {
        public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

        public override string ToString() => Text($"({Left}, {Top})-({Right}, {Bottom})");
    }
}
