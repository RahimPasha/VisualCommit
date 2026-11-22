// Part A's helpers (docs/test-reports/phase-8a.md, part A's conventions): the settings screen's view
// and About's controls, the section list's places, About's rows as the report reads them (texts,
// places, colours, the icon, no scroll bar, no "@"), the text dialog's view and its text's lines, and
// how a colour is read.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit.Rendering;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

public sealed partial class Phase8aChecksA
{
    /// <summary>The other sections' panels, by their names, none of which shows while About does.</summary>
    private static readonly string[] OtherSections = ["SettingsGeneral", "SettingsGit", "SettingsTools", "SettingsShortcuts", "SettingsAccounts"];

    /// <summary>
    /// A picture of part A (<see cref="Phase8aChecks.Shot"/>), the pointer at the neutral point: saved
    /// under its name, then no text cut off (shortened text whole in its tooltip) and no tooltip open.
    /// </summary>
    private static Screenshot Picture(ShellDriver app, string name)
    {
        var picture = Shot(app, name);
        AssertNoClippedText(app);
        Assert.False(ToolTipOpen(app), $"A tooltip is open in {name}.");
        return picture;
    }

    private static A.Colours ColoursOf(ShellDriver app) => app.ShowsDarkTheme ? A.Dark : A.Light;

    // ---- The settings screen and its controls ------------------------------------------------

    /// <summary>The settings screen's view (<c>SettingsDialog</c>), wherever it is in the dialog stack.</summary>
    private static SettingsDialogView SettingsView(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<SettingsDialogView>().Single(view => view.IsEffectivelyVisible);

    /// <summary>A control of the settings screen by its automation id.</summary>
    private static T InSettings<T>(ShellDriver app, string id)
        where T : Control => (T)app.FindByAutomationId(SettingsView(app), id);

    /// <summary>The settings screen's card (<c>SettingsCard</c>, a plain border with a name).</summary>
    private static Border SettingsCard(ShellDriver app) =>
        SettingsView(app).GetVisualDescendants().OfType<Border>().Single(border => border.Name == "SettingsCard");

    /// <summary>The text dialog's view, on top.</summary>
    private static TextDocumentDialogView TextDialogView(ShellDriver app) =>
        Assert.IsType<TextDocumentDialogView>(app.TopDialogView);

    private static T InTextDialog<T>(ShellDriver app, string id)
        where T : Control => (T)app.FindByAutomationId(TextDialogView(app), id);

    private static DocumentTextView DocumentText(ShellDriver app) => InTextDialog<DocumentTextView>(app, A.TextId);

    /// <summary>Waits until the control <paramref name="id"/> has the keyboard focus (the nearest ancestor with an id).</summary>
    private static Task WaitForFocusAsync(ShellDriver app, string id) =>
        app.WaitForAsync(() => app.FocusedAutomationId() == id, $"the keyboard focus on {id} (it is on {app.FocusedAutomationId() ?? "nothing"})");

    /// <summary>Waits until the settings screen shows About: its panel shown, its reads ended.</summary>
    private static async Task WaitForAboutAsync(ShellDriver app)
    {
        await app.WaitForAsync(() => app.SettingsDialog is { ShowsAbout: true } && InSettings<Control>(app, A.SectionId).IsEffectivelyVisible, "About shown");
        await app.WaitForSettingsReadsAsync();
    }

    /// <summary>The keyboard focus is in the text dialog's text: its text area holds it.</summary>
    private static bool FocusInText(ShellDriver app) =>
        app.TopDialogView is TextDocumentDialogView && DocumentText(app).TextArea.IsFocused && app.FocusedAutomationId() == A.TextId;

    private static bool ToolTipOpen(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<Control>().Any(control => ToolTip.GetIsOpen(control));

    // ---- The section list (A1, A2, A3) -------------------------------------------------------

    /// <summary>
    /// The section list: six buttons in order, each named by its text, 176 × 30, 2 apart; About at its
    /// place within 2, exactly 32 below Accounts with the same left; About alone selected, its
    /// background the control colour at a point clear of its text.
    /// </summary>
    private static void AssertSectionList(ShellDriver app, Screenshot picture, bool full)
    {
        var buttons = InSettings<StackPanel>(app, A.NavId).GetVisualChildren().OfType<Button>().ToList();
        Assert.Equal(A.NavIds, buttons.Select(button => AutomationProperties.GetAutomationId(button)));
        Assert.Equal(A.NavButtons, buttons.Select(button => AutomationProperties.GetName(button)));
        for (var i = 0; i < buttons.Count; i++)
        {
            var bounds = app.BoundsOf(buttons[i]);
            Assert.Equal((A.NavWidth, A.NavHeight), (bounds.Width, bounds.Height));
            if (i > 0)
            {
                Assert.Equal(A.NavGap, bounds.Top - app.BoundsOf(buttons[i - 1]).Bottom, 0.5);
            }

            Assert.True(buttons[i].Classes.Contains("selected") == (i == buttons.Count - 1), $"{A.NavIds[i]} is {(i == buttons.Count - 1 ? "not " : string.Empty)}selected.");
        }

        var about = app.BoundsOf(buttons[^1]);
        var accounts = app.BoundsOf(buttons[^2]);
        var (x, y) = full ? A.AboutAtFull : A.AboutAt;
        Assert.True(
            Math.Abs(about.X - x) <= A.Tolerance && Math.Abs(about.Y - y) <= A.Tolerance,
            $"SettingsNavAbout is at ({about.X}, {about.Y}), expected ({x}, {y}) ±{A.Tolerance}.");
        Assert.Equal((accounts.X, accounts.Y + A.AboutBelowAccounts), (about.X, about.Y));
        AssertColour(ColoursOf(app).Control, picture, new Point(about.Right - 6, about.Center.Y), "selected About button");
    }

    /// <summary>The settings card's place and size (phase 7a's <c>Card</c> and <c>CardFull</c>).</summary>
    private static void AssertCard(ShellDriver app, bool full)
    {
        var (x, y, width, height) = full ? A.CardFull : A.Card;
        Assert.Equal(new Rect(x, y, width, height), app.BoundsOf(SettingsCard(app)));
    }

    // ---- About's rows (A1, A2, A3, A6) -------------------------------------------------------

    /// <summary>
    /// About as A1 reads it after its step 3, in the window's theme and at its size: only About shows;
    /// its rows 18 apart; the head (the icon, the 96 × 96 bitmap drawn at 48 × 48 in high quality,
    /// then 16 to its right the name at 20 SemiBold over the creator, secondary); the version, its
    /// name its text; the copyright line; the license with
    /// Show license… 10 below; the note with Show third-party notices… 10 below; the two links with
    /// their tooltips, enabled, not underlined, in the accent colour; each label secondary at the
    /// section's left and each value at the values' left; every button and link named by its text;
    /// no scroll bar; Cancel and Save at the card's bottom; no "@" anywhere in the section.
    /// </summary>
    private static void AssertAbout(ShellDriver app, Screenshot picture, bool full)
    {
        var colours = ColoursOf(app);
        var screen = app.SettingsDialog!;
        Assert.Equal(SettingsSection.About, screen.SelectedSection);
        var section = InSettings<StackPanel>(app, A.SectionId);
        Assert.True(section.IsEffectivelyVisible, "About does not show.");
        foreach (var other in OtherSections)
        {
            var panel = SettingsView(app).GetVisualDescendants().OfType<Control>().Single(control => control.Name == other);
            Assert.False(panel.IsEffectivelyVisible, $"{other} shows beside About.");
        }

        // The rows, 18 apart.
        var rows = section.Children.OfType<Control>().Where(row => row.IsVisible).ToList();
        Assert.Equal(7, rows.Count);
        for (var i = 1; i < rows.Count; i++)
        {
            Assert.Equal(A.RowSpacing, app.BoundsOf(rows[i]).Top - app.BoundsOf(rows[i - 1]).Bottom, 0.5);
        }

        var sectionX = full ? A.SectionXFull : A.SectionX;
        var valuesX = full ? A.ValuesXFull : A.ValuesX;

        // The head.
        var icon = InSettings<Image>(app, A.IconId);
        var iconBounds = app.BoundsOf(icon);
        Assert.Equal((A.IconSize, A.IconSize), (iconBounds.Width, iconBounds.Height));
        Assert.Equal(sectionX, iconBounds.X, A.Tolerance);
        Assert.True(icon.Source is Bitmap { PixelSize: { Width: 96, Height: 96 } }, "The icon is not drawn from the 96 × 96 bitmap.");
        Assert.Equal(BitmapInterpolationMode.HighQuality, RenderOptions.GetBitmapInterpolationMode(icon));
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(icon)), "The icon is decorative but has a name.");
        AssertNear(Colors.White, picture, iconBounds.Center, A.IconTolerance, "icon's centre");
        AssertNear(colours.Panel, picture, iconBounds.TopLeft, A.IconTolerance, "icon's top-left pixel");
        var name = InSettings<TextBlock>(app, A.NameId);
        Assert.Equal(A.Name, name.Text);
        Assert.Equal(A.NameFontSize, name.FontSize);
        Assert.Equal(FontWeight.SemiBold, name.FontWeight);
        Assert.Equal(iconBounds.Right + A.NameAfterIcon, app.BoundsOf(name).X, 0.5);
        var creator = InSettings<TextBlock>(app, A.CreatorId);
        Assert.Equal(A.Creator, creator.Text);
        Assert.Contains("secondary", creator.Classes);
        AssertBrush(colours.Secondary, creator.Foreground, "AboutCreator");
        Assert.True(app.BoundsOf(creator).Top >= app.BoundsOf(name).Bottom - 0.5, "The creator is not under the name.");

        // The labels, in the form's first column, secondary.
        var labels = section.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Classes.Contains("settingsLabel")).ToList();
        Assert.Equal(A.Labels, labels.Select(label => label.Text));
        Assert.All(labels, label =>
        {
            Assert.Equal(sectionX, app.BoundsOf(label).X, A.Tolerance);
            AssertBrush(colours.Secondary, label.Foreground, $"the label {label.Text}");
        });

        // The values.
        var version = InSettings<TextBlock>(app, A.VersionId);
        Assert.Equal(PinnedVersion, version.Text);
        Assert.Equal(PinnedVersion, AutomationProperties.GetName(version));
        Assert.Equal(A.Copyright, InSettings<TextBlock>(app, A.CopyrightId).Text);
        var license = InSettings<TextBlock>(app, A.LicenseId);
        Assert.Equal(A.MitLicense, license.Text);
        var showLicense = InSettings<Button>(app, A.ShowLicenseId);
        AssertActionButton(app, showLicense, A.ShowLicense, license);
        var note = InSettings<TextBlock>(app, A.NoticesNoteId);
        Assert.Equal(A.NoticesNote, note.Text);
        Assert.Contains("settingsNote", note.Classes);
        AssertBrush(colours.Secondary, note.Foreground, "AboutNoticesNote");
        AssertActionButton(app, InSettings<Button>(app, A.ShowNoticesId), A.ShowNotices, note);
        AssertLink(app, picture, A.RepositoryLinkId, A.RepositoryLink, A.RepositoryTip, underlined: false);
        AssertLink(app, picture, A.GuideLinkId, A.OpenUserGuide, A.GuideTip, underlined: false);
        foreach (var id in new[] { A.VersionId, A.CopyrightId, A.LicenseId, A.NoticesNoteId, A.RepositoryLinkId, A.GuideLinkId })
        {
            Assert.Equal(valuesX, app.BoundsOf(InSettings<Control>(app, id)).X, A.Tolerance);
        }

        // No scroll bar: the section is no taller than its viewer.
        var scroller = InSettings<ScrollViewer>(app, A.SectionScrollerId);
        Assert.True(scroller.Extent.Height <= scroller.Viewport.Height + 0.5, $"About scrolls: its extent is {scroller.Extent.Height} high in a viewport of {scroller.Viewport.Height}.");

        // Cancel and Save at the card's bottom.
        var card = app.BoundsOf(SettingsCard(app));
        foreach (var (id, text) in new[] { ("CancelButton", "Cancel"), ("ConfirmButton", "Save") })
        {
            var button = InSettings<Button>(app, id);
            Assert.Equal(text, AutomationProperties.GetName(button));
            Assert.Equal(card.Bottom - 21, app.BoundsOf(button).Bottom, A.Tolerance);
        }

        AssertNoAddress(section);
    }

    /// <summary>
    /// About's texts as A1 reads them, without its places and colours (A6 after its step 3): the name,
    /// the creator, the labels in order, the version (its name its text), the copyright line, the
    /// license, the note, the two Show buttons and the two links, each named by its text, the links
    /// with their tooltips; no "@" in the section.
    /// </summary>
    private static void AssertAboutTexts(ShellDriver app)
    {
        var section = InSettings<StackPanel>(app, A.SectionId);
        Assert.Equal(A.Name, InSettings<TextBlock>(app, A.NameId).Text);
        Assert.Equal(A.Creator, InSettings<TextBlock>(app, A.CreatorId).Text);
        Assert.Equal(A.Labels, section.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Classes.Contains("settingsLabel")).Select(label => label.Text));
        var version = InSettings<TextBlock>(app, A.VersionId);
        Assert.Equal(PinnedVersion, version.Text);
        Assert.Equal(PinnedVersion, AutomationProperties.GetName(version));
        Assert.Equal(A.Copyright, InSettings<TextBlock>(app, A.CopyrightId).Text);
        Assert.Equal(A.MitLicense, InSettings<TextBlock>(app, A.LicenseId).Text);
        Assert.Equal(A.NoticesNote, InSettings<TextBlock>(app, A.NoticesNoteId).Text);
        foreach (var (id, text) in new[] { (A.ShowLicenseId, A.ShowLicense), (A.ShowNoticesId, A.ShowNotices) })
        {
            var button = InSettings<Button>(app, id);
            Assert.Equal(text, button.Content);
            Assert.Equal(text, AutomationProperties.GetName(button));
        }

        foreach (var (id, text, tip) in new[] { (A.RepositoryLinkId, A.RepositoryLink, A.RepositoryTip), (A.GuideLinkId, A.OpenUserGuide, A.GuideTip) })
        {
            var link = InSettings<Button>(app, id);
            Assert.Equal(text, TextOf(link).Text);
            Assert.Equal(text, AutomationProperties.GetName(link));
            Assert.Equal(tip, ToolTip.GetTip(link) as string);
        }

        AssertNoAddress(section);
    }

    /// <summary>A Show button: a <c>VcActionButton</c> named by its text, enabled, 10 below its row's text.</summary>
    private static void AssertActionButton(ShellDriver app, Button button, string text, TextBlock above)
    {
        Assert.Equal(text, button.Content);
        Assert.Equal(text, AutomationProperties.GetName(button));
        Assert.True(button.IsEffectivelyEnabled, $"{text} is disabled.");
        Assert.Same(Application.Current!.FindResource("VcActionButton"), button.Theme);
        Assert.Equal(A.ButtonBelowText, app.BoundsOf(button).Top - app.BoundsOf(above).Bottom, 0.5);
    }

    /// <summary>
    /// A link: its text, its name its text, its tooltip, enabled, underlined or not, its text in the
    /// accent colour and its ink that colour on the card's background.
    /// </summary>
    private static void AssertLink(ShellDriver app, Screenshot picture, string id, string text, string tip, bool underlined)
    {
        var colours = ColoursOf(app);
        var link = InSettings<Button>(app, id);
        var block = TextOf(link);
        Assert.Equal(text, block.Text);
        Assert.Equal(text, AutomationProperties.GetName(link));
        Assert.Equal(tip, ToolTip.GetTip(link) as string);
        Assert.True(link.IsEffectivelyEnabled, $"{id} is disabled.");
        Assert.True(IsUnderlined(block) == underlined, $"{id} is {(underlined ? "not " : string.Empty)}underlined.");
        AssertBrush(colours.Accent, block.Foreground, id);
        if (!underlined)
        {
            AssertInk(picture, TextRect(app, block), colours.Accent, colours.Panel, id);
        }
    }

    /// <summary>
    /// A link focused by the keyboard in <paramref name="focused"/> against itself in
    /// <paramref name="unfocused"/>, a picture of the same screen without its focus (A1's step 9
    /// against a1a), by <see cref="LinkFrame"/>: its text's ink all as there, the first and last
    /// letters too; the underline under it in the accent across the text; Fluent's frame
    /// (<c>SystemControlFocusVisualPrimaryBrush</c>, part K's conventions: Fluent's own colours)
    /// whole on four sides, outside the text.
    /// </summary>
    private static void AssertFramedLink(ShellDriver app, Screenshot unfocused, Screenshot focused, string id)
    {
        var colours = ColoursOf(app);
        Assert.True(
            app.Window.TryFindResource("SystemControlFocusVisualPrimaryBrush", app.Window.ActualThemeVariant, out var resource) && resource is ISolidColorBrush,
            "The theme has no SystemControlFocusVisualPrimaryBrush.");
        var frame = ((ISolidColorBrush)resource!).Color;
        var problems = LinkFrame.Problems(unfocused, focused, app.BoundsOf(TextOf(InSettings<Button>(app, id))), colours.Accent, colours.Panel, frame, id);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    private static TextBlock TextOf(Button link) => link.GetVisualDescendants().OfType<TextBlock>().Single();

    private static bool IsUnderlined(TextBlock block) =>
        block.TextDecorations is { Count: > 0 } decorations && decorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline);

    /// <summary>No text, tooltip or automation name in <paramref name="scope"/> holds "@" (D409).</summary>
    private static void AssertNoAddress(Visual scope)
    {
        var texts = scope.GetSelfAndVisualDescendants().OfType<Control>()
            .SelectMany(control => new[] { (control as TextBlock)?.Text, ToolTip.GetTip(control) as string, AutomationProperties.GetName(control), AutomationProperties.GetHelpText(control) })
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();
        Assert.DoesNotContain(texts, text => text!.Contains('@', StringComparison.Ordinal));
    }

    // ---- The text dialog (A4, A5) ------------------------------------------------------------

    /// <summary>
    /// The text dialog's frame: the card at its place on the panel's colour, the title, the text view
    /// (read-only, wrapped, no tab kept, the code font, its colours and border, no caret), its peer a
    /// document named by the title, the keyboard focus in it, and "Copy all" at the left and "Close"
    /// at the right on one line 20 below the text.
    /// </summary>
    private static void AssertTextDialog(ShellDriver app, Screenshot picture, string title)
    {
        var colours = ColoursOf(app);
        var view = TextDialogView(app);
        var card = app.BoundsOf(view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "TextDocumentCard"));
        var (x, y, width, height) = A.DialogCard;
        Assert.True(
            Math.Abs(card.X - x) <= A.Tolerance && Math.Abs(card.Y - y) <= A.Tolerance && Math.Abs(card.Width - width) <= A.Tolerance && Math.Abs(card.Height - height) <= A.Tolerance,
            $"The text dialog's card is {card}, expected ({x}, {y}, {width}, {height}) ±{A.Tolerance}.");
        AssertColour(colours.Panel, picture, new Point(card.X + 10, card.Center.Y), "text dialog's card");

        var titleBlock = InTextDialog<TextBlock>(app, A.DialogTitleId);
        Assert.Equal(title, titleBlock.Text);
        Assert.Equal(A.TitleFontSize, titleBlock.FontSize);
        Assert.Equal(FontWeight.SemiBold, titleBlock.FontWeight);

        var text = DocumentText(app);
        Assert.True(text.IsReadOnly);
        Assert.True(text.WordWrap);
        Assert.False(text.Options.AcceptsTab);
        Assert.Equal(A.TextFontSize, text.FontSize);
        Assert.True(Application.Current!.TryGetResource("VcCodeFontFamily", app.ShowsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light, out var font) && Equals(font, text.FontFamily), "The text is not in the code font.");
        AssertBrush(colours.Primary, text.Foreground, "TextDocumentText's text");
        AssertBrush(colours.Control, text.Background, "TextDocumentText's background");
        AssertBrush(colours.Border, text.BorderBrush, "TextDocumentText's border");
        Assert.Equal(new Thickness(1), text.BorderThickness);
        var caret = Assert.IsAssignableFrom<ISolidColorBrush>(text.TextArea.CaretBrush);
        Assert.Equal(0, caret.Color.A);
        var peer = ControlAutomationPeer.CreatePeerForElement(text);
        Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
        Assert.Equal(title, peer.GetName());
        Assert.True(FocusInText(app), $"The keyboard focus is on {app.FocusedAutomationId() ?? "nothing"}, not in the text.");

        var textBounds = app.BoundsOf(text);
        var copy = InTextDialog<Button>(app, A.CopyId);
        var close = InTextDialog<Button>(app, A.CloseId);
        Assert.Equal(A.CopyAll, copy.Content);
        Assert.Equal(A.CopyAll, AutomationProperties.GetName(copy));
        Assert.Equal(A.Close, close.Content);
        Assert.Equal(A.Close, AutomationProperties.GetName(close));
        Assert.Same(Application.Current!.FindResource("VcPrimaryButton"), close.Theme);
        var copyBounds = app.BoundsOf(copy);
        var closeBounds = app.BoundsOf(close);
        Assert.Equal(A.ButtonsBelowText, copyBounds.Top - textBounds.Bottom, 0.5);
        Assert.Equal(copyBounds.Center.Y, closeBounds.Center.Y, 0.5);
        Assert.Equal(card.X + 21, copyBounds.X, A.Tolerance);
        Assert.Equal(card.Right - 21, closeBounds.Right, A.Tolerance);
    }

    /// <summary>The text dialog's backdrop over the settings card, at a point of that card clear of the dialog's card, within ±8 of the blend.</summary>
    private static void AssertBackdropOverSettings(ShellDriver app, Screenshot picture)
    {
        var colours = ColoursOf(app);
        var alpha = colours.Backdrop.A / 255.0;
        var blend = Color.FromRgb(
            (byte)Math.Round(colours.Panel.R * (1 - alpha)),
            (byte)Math.Round(colours.Panel.G * (1 - alpha)),
            (byte)Math.Round(colours.Panel.B * (1 - alpha)));
        var settings = app.BoundsOf(SettingsCard(app));
        AssertNear(blend, picture, new Point(settings.X + 25, settings.Bottom - 120), 8, "text dialog's backdrop over the settings card");
    }

    /// <summary>The visual lines the text view shows whole now: their first and last document lines.</summary>
    private static (int First, int Last) LinesInView(DocumentTextView text)
    {
        var textView = text.TextArea.TextView;
        Assert.True(textView.VisualLinesValid, "The text is not laid out.");
        var whole = textView.VisualLines.Where(line => line.VisualTop >= textView.VerticalOffset - 0.5 && line.VisualTop + line.Height <= textView.VerticalOffset + textView.Bounds.Height + 0.5).ToList();
        Assert.NotEmpty(whole);
        return (whole[0].FirstDocumentLine.LineNumber, whole[^1].LastDocumentLine.LineNumber);
    }

    /// <summary>The view's scroll offset is at its end: the extent less the viewport, within 1.</summary>
    private static bool AtEnd(DocumentTextView text) =>
        text.ScrollExtent.Height > text.ScrollViewport.Height
        && Math.Abs(text.ScrollOffset.Y - (text.ScrollExtent.Height - text.ScrollViewport.Height)) <= 1;

    private static string ScrollState(DocumentTextView text) => string.Create(
        CultureInfo.InvariantCulture,
        $"offset {text.ScrollOffset.Y}, extent {text.ScrollExtent.Height}, viewport {text.ScrollViewport.Height}");

    /// <summary>The ink rectangle of the text's first visual line, in window coordinates: from the line's start, as wide as its text.</summary>
    private static Rect FirstLineRect(ShellDriver app, DocumentTextView text)
    {
        var textView = text.TextArea.TextView;
        var line = textView.GetVisualLine(1) ?? throw new InvalidOperationException("Line 1 is not laid out.");
        var width = line.TextLines[0].WidthIncludingTrailingWhitespace;
        var topLeft = textView.TranslatePoint(new Point(0, line.VisualTop - textView.VerticalOffset), app.Window) ?? default;
        return new Rect(topLeft.X, topLeft.Y, Math.Max(1, width), line.Height);
    }

    // ---- Colours read from a picture and from the tree -----------------------------------------

    private static Rect TextRect(ShellDriver app, TextBlock block)
    {
        var bounds = app.BoundsOf(block);
        return new Rect(bounds.X, bounds.Y, Math.Min(block.TextLayout.WidthIncludingTrailingWhitespace + 1, bounds.Width), bounds.Height);
    }

    private static void AssertInk(Screenshot picture, Rect area, Color foreground, Color background, string what)
    {
        var problems = TextInk.Problems(picture, area, foreground, background, null, what);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    private static void AssertColour(Color expected, Screenshot picture, Point point, string what)
    {
        var actual = picture.PixelAt(point);
        Assert.True(actual == expected, $"The {what} at {point} is {Hex(actual)}, not {Hex(expected)}.");
    }

    private static void AssertNear(Color expected, Screenshot picture, Point point, int tolerance, string what)
    {
        var actual = picture.PixelAt(point);
        Assert.True(
            Math.Abs(actual.R - expected.R) <= tolerance && Math.Abs(actual.G - expected.G) <= tolerance && Math.Abs(actual.B - expected.B) <= tolerance,
            $"The {what} at {point} is {Hex(actual)}, not within {tolerance} of {Hex(expected)}.");
    }

    private static void AssertBrush(Color expected, IBrush? brush, string what)
    {
        var actual = (brush as ISolidColorBrush)?.Color;
        Assert.True(actual == expected, $"{what} is drawn in {(actual is { } colour ? Hex(colour) : "no solid brush")}, not {Hex(expected)}.");
    }

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    /// <summary>A file of the repository as the app embeds it: <c>\n</c> line ends.</summary>
    private static string RepositoryText(string relative) =>
        File.ReadAllText(IconFile.RepositoryPath(relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The data folder's settings file, which no part A step writes but A2's start.</summary>
    private static string SettingsFile(TempDirectory data) => Path.Combine(data.Path, "settings.json");
}
