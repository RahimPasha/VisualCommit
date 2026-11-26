using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views.Graph;
using VisualCommit.App.Views.Rebase;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The texts the app dims on purpose on rows the user still acts on, which the owner kept under
/// 4.5:1 as accepted exceptions (D469, amending D418; the review before phase 8a's gate, ruling 99's
/// colours-1): (a) a hidden branch's name in the left panel, and in solo mode the name of every
/// branch left out; (b) a dropped row of the interactive rebase's list; (c) the graph's rows that a
/// search dims. The rows are not disabled, so WCAG's exemption of disabled controls does not cover
/// them, and <see cref="TextPairs"/> leaves them out. Each exception is pinned to the figures of
/// what the app draws today, computed from its own tokens and styles (for (c), the colours blended
/// at the search's opacity; the render fact holds the labels' fills within a step of the blend), on
/// every background its text lies on, in both themes: a colour that changes, a new state of the
/// row, or a fix that lifts the text to 4.5:1 fails its fact, to be looked at with D469. (a) and
/// (b) read the views' styles; the graph draws in code, so (c)'s tokens stand in tables that a
/// render of the graph holds it to, in its text columns and its labels' column (its graph's column,
/// where the node is dimmed, is not read). A guard keeps the disabled text colour and the search's
/// dimming to the uses it lists, each with its reason.
/// </summary>
public partial class ThemeTokensTests
{
    /// <summary>The disabled text colour's token, the colour of exceptions (a) and (b).</summary>
    private const string DimmedToken = "VcTextDisabledBrush";

    /// <summary>The app's source folder, as the guard names its files: relative to the repository's root, with <c>/</c>.</summary>
    private const string AppSource = "src/VisualCommit.App";

    /// <summary>The XAML namespace of <c>x:Key</c> and <c>x:Name</c>.</summary>
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// D469's exception (a): a hidden branch's name in the left panel, and in solo mode the name of
    /// every branch left out (docs/phase-5-design.md 8.3), drawn in <c>VcTextDisabledBrush</c> by
    /// <c>LeftPanelView</c>'s style <c>TextBlock.refName.dimmed</c>, which only HEAD's branch overrides.
    /// The row (<c>RefRowButton</c>) is transparent at rest over the panel's
    /// <c>VcPanelBackgroundBrush</c>, <c>VcHoverBackgroundBrush</c> under the pointer and
    /// <c>VcSelectionBrush</c> selected, also under the pointer: every colour and opacity setter of
    /// the view's styles and control theme is read for those states, the name's element is held to
    /// set no foreground and no opacity of its own (an ancestor's own opacity is not read), and the
    /// name's ratio on each is pinned to two places, under 4.5:1.
    /// </summary>
    [AvaloniaFact]
    public void Exception_D469_a_a_hidden_or_left_out_branchs_name_keeps_its_pinned_figures_under_4_5_to_1()
    {
        // The view's own styles and control themes: every setter of a colour or an opacity, whatever
        // it selects, so that a style that recolours or fades the dimmed name or the row, or a new
        // state of the row, fails here first. The chevrons and the counts' arrows are icons; the
        // control theme's own foreground is the one the name's style overrides.
        var view = new LeftPanelView();
        var styles = ViewStyles(view);
        Assert.Equal(
            [
                "Icon.chevron: VcTextSecondaryBrush",
                "Icon.aheadBehind: VcTextSecondaryBrush",
                "TextBlock.refName.dimmed: VcTextDisabledBrush",
                "TextBlock.refName.head: VcAccentBrush",
                "Button:pointerover TextBlock.refName.head, Button.selected TextBlock.refName.head: VcAccentOnRowBrush",
                "Button: VcTextPrimaryBrush",
            ],
            Setters(styles, "Foreground"));
        Assert.Equal(
            ["Button: transparent", "^:pointerover: VcHoverBackgroundBrush", "^.selected: VcSelectionBrush"],
            Setters(styles, "Background"));
        Assert.Empty(Setters(styles, "Opacity"));
        Assert.Equal(["TextBlock RefName"], DimmedElements(AppSource + "/Views/Shell/LeftPanelView.axaml", "dimmed"));
        AssertBackgroundIs(view, () => Assert.IsType<Border>(view.Content).Background, "VcPanelBackgroundBrush");

        var dark = ThemeVariant.Dark;
        var light = ThemeVariant.Light;
        var failures = new List<string>();
        foreach (var (background, variant, pinned) in new[]
        {
            ("VcPanelBackgroundBrush", dark, 2.96),
            ("VcHoverBackgroundBrush", dark, 2.16),
            ("VcSelectionBrush", dark, 2.23),
            ("VcPanelBackgroundBrush", light, 2.22),
            ("VcHoverBackgroundBrush", light, 1.84),
            ("VcSelectionBrush", light, 1.85),
        })
        {
            ExpectPinned(failures, $"(a) a hidden branch's name, {DimmedToken} on {background}", variant, Ratio(DimmedToken, background, variant), pinned);
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    /// <summary>
    /// D469's exception (b): a dropped row of the interactive rebase's list (docs/phase-4-design.md
    /// 2.2), its SHA and subject struck through in <c>VcTextDisabledBrush</c> by
    /// <c>InteractiveRebaseView</c>'s style <c>TextBlock.dropped</c>. The row
    /// (<c>RebaseListRow.commit</c>) is transparent at rest over the list's
    /// <c>VcWindowBackgroundBrush</c>, <c>VcRowHoverBrush</c> under the pointer and
    /// <c>VcSelectionBrush</c> selected: every colour and opacity setter of the view's styles is read
    /// for those states, the SHA's and the subject's elements are held to set no foreground and no
    /// opacity of their own (an ancestor's own opacity is not read), and the text's ratio on each is
    /// pinned to two places, under 4.5:1.
    /// </summary>
    [AvaloniaFact]
    public void Exception_D469_b_a_dropped_rebase_rows_sha_and_subject_keep_their_pinned_figures_under_4_5_to_1()
    {
        // The view's own styles, as for (a): every setter of a colour or an opacity, whatever it
        // selects. A dropped text's style comes after the SHA's and the subject's, so that it wins;
        // the join line is a mark at the row's left, not its background.
        var view = new InteractiveRebaseView();
        var styles = ViewStyles(view);
        Assert.Equal(
            ["TextBlock.sha: VcTextSecondaryBrush", "TextBlock.subject: VcTextPrimaryBrush", "TextBlock.dropped: VcTextDisabledBrush"],
            Setters(styles, "Foreground"));
        Assert.Equal(
            [
                "RebaseListRow.commit: transparent",
                "RebaseListRow.commit:pointerover: VcRowHoverBrush",
                "RebaseListRow.commit.selected: VcSelectionBrush",
                "Border.joinLine: VcLane0Brush",
            ],
            Setters(styles, "Background"));

        // The row being dragged, at half opacity while the drag lasts: a state of the drag, dropped or not.
        Assert.Equal(["StackPanel.dragged: 0.5"], Setters(styles, "Opacity"));
        Assert.Equal(
            ["TextBlock RebaseRowSha", "TrimmedTextBlock RebaseRowSubject"],
            DimmedElements(AppSource + "/Views/Rebase/InteractiveRebaseView.axaml", "dropped"));
        AssertBackgroundIs(view, () => Assert.IsType<DockPanel>(view.Content).Background, "VcWindowBackgroundBrush");

        var dark = ThemeVariant.Dark;
        var light = ThemeVariant.Light;
        var failures = new List<string>();
        foreach (var (background, variant, pinned) in new[]
        {
            ("VcWindowBackgroundBrush", dark, 3.17),
            ("VcRowHoverBrush", dark, 2.89),
            ("VcSelectionBrush", dark, 2.23),
            ("VcWindowBackgroundBrush", light, 2.40),
            ("VcRowHoverBrush", light, 2.18),
            ("VcSelectionBrush", light, 1.85),
        })
        {
            ExpectPinned(failures, $"(b) a dropped row's SHA and subject, {DimmedToken} on {background}", variant, Ratio(DimmedToken, background, variant), pinned);
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    /// <summary>
    /// D469's exception (c): the graph's rows that a search dims (docs/phase-5-design.md 3.3),
    /// whose texts <c>CommitGraphControl</c> draws at <see cref="CommitGraphControl.SearchDimmedOpacity"/>
    /// over the row's background, each in the token <see cref="DimmedRowTexts"/> names: the message
    /// in <c>VcTextPrimaryBrush</c> (a stash's in <c>VcTextSecondaryBrush</c>), the author, date and
    /// SHA in <c>VcTextSecondaryBrush</c>; a remote branch's label, which has no fill, in
    /// <c>VcTextPrimaryBrush</c>. The graph draws its rows in code (<c>RowBackground</c>), on the
    /// backgrounds <see cref="DimmedRowBackgrounds"/> names: <c>VcWindowBackgroundBrush</c> at rest,
    /// <c>VcRowHoverBrush</c> under the pointer and <c>VcSelectionBrush</c> selected, which a search
    /// leaves as they are. The render fact below holds the graph to those tables, and this fact's
    /// pairs to them. Each text's colour is blended at that opacity over the background, a channel
    /// rounded to the nearest step as the graph's other tests fade a colour, and its ratio pinned to
    /// two places, under 4.5:1. The pins are the blend's figures, not figures read from pixels: the
    /// render fact holds the labels' fills within a step of the blend, and a drawn figure may differ
    /// from its pin by up to about 0.04 (a step in every channel).
    /// </summary>
    [AvaloniaFact]
    public void Exception_D469_c_the_texts_of_a_row_a_search_dims_keep_their_pinned_figures_under_4_5_to_1()
    {
        var dark = ThemeVariant.Dark;
        var light = ThemeVariant.Light;
        (string Text, string Background, ThemeVariant Variant, double Pinned)[] pins =
        [
            // The message, and a remote branch's label, which has no fill.
            ("VcTextPrimaryBrush", "VcWindowBackgroundBrush", dark, 2.83),
            ("VcTextPrimaryBrush", "VcRowHoverBrush", dark, 2.82),
            ("VcTextPrimaryBrush", "VcSelectionBrush", dark, 2.62),
            ("VcTextPrimaryBrush", "VcWindowBackgroundBrush", light, 2.15),
            ("VcTextPrimaryBrush", "VcRowHoverBrush", light, 2.14),
            ("VcTextPrimaryBrush", "VcSelectionBrush", light, 2.08),

            // The author, the date, the SHA and a stash's message.
            ("VcTextSecondaryBrush", "VcWindowBackgroundBrush", dark, 1.94),
            ("VcTextSecondaryBrush", "VcRowHoverBrush", dark, 1.94),
            ("VcTextSecondaryBrush", "VcSelectionBrush", dark, 1.83),
            ("VcTextSecondaryBrush", "VcWindowBackgroundBrush", light, 1.66),
            ("VcTextSecondaryBrush", "VcRowHoverBrush", light, 1.63),
            ("VcTextSecondaryBrush", "VcSelectionBrush", light, 1.56),
        ];

        // The pairs the graph draws, each in both themes: a text of DimmedRowTexts, or a label's
        // without a fill, on each background of DimmedRowBackgrounds.
        var drawn = DimmedRowTexts.Select(cell => cell.Token)
            .Concat(DimmedRowLabels.Where(label => label.Fill is null).Select(label => label.Text))
            .Distinct()
            .SelectMany(text => DimmedRowBackgrounds.SelectMany(background => Variants.Select(variant => $"{variant}: {text} on {background.Token}")));
        Assert.Equal(drawn.Order(StringComparer.Ordinal), pins.Select(pin => $"{pin.Variant}: {pin.Text} on {pin.Background}").Order(StringComparer.Ordinal));

        var failures = new List<string>();
        foreach (var (text, background, variant, pinned) in pins)
        {
            var behind = Token(background, variant);
            var ratio = Contrast.Ratio(Faded(Token(text, variant), behind), behind);
            ExpectPinned(failures, $"(c) a dimmed row's {text} on {background}", variant, ratio, pinned, faded: true);
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    /// <summary>
    /// D469's exception (c), the part the review before the gate did not count: a dimmed row's ref
    /// labels, which <c>CommitGraphControl</c> draws at the same opacity, fill and text alike. The
    /// renderer fades each drawn shape by itself (the render fact finds the fill faded over the row,
    /// and the text's ink at the text faded over that fill), so a label's text is blended over its
    /// faded fill, as <see cref="DimmedRowLabels"/> names them:
    /// <c>VcLabelTextBrush</c> on a local branch's, the current branch's or a detached HEAD's fill,
    /// the row's lane (all eight lanes), and <c>VcTextPrimaryBrush</c> on a tag's, a stash's or a
    /// "+N" label's <c>VcControlBackgroundBrush</c>; a pull request's <c>#n</c> takes its label's
    /// colours. A remote branch's label has no fill: its text is the message's pair. The render
    /// fact below holds the graph to the table, and this fact's pairs to it. Each ratio is pinned to
    /// two places, under 4.5:1, the blend's figures as for the texts.
    /// </summary>
    [AvaloniaFact]
    public void Exception_D469_c_the_labels_of_a_row_a_search_dims_keep_their_pinned_figures_under_4_5_to_1()
    {
        var dark = ThemeVariant.Dark;
        var light = ThemeVariant.Light;
        (string Background, ThemeVariant Variant, double[] Pinned)[] pins =
        [
            // Lanes 0 to 7 with VcLabelTextBrush, then VcControlBackgroundBrush with VcTextPrimaryBrush.
            ("VcWindowBackgroundBrush", dark, [1.29, 1.33, 1.37, 1.26, 1.35, 1.29, 1.31, 1.35, 2.81]),
            ("VcRowHoverBrush", dark, [1.34, 1.37, 1.41, 1.31, 1.39, 1.33, 1.35, 1.40, 2.79]),
            ("VcSelectionBrush", dark, [1.44, 1.49, 1.51, 1.39, 1.50, 1.44, 1.45, 1.49, 2.64]),
            ("VcWindowBackgroundBrush", light, [1.22, 1.19, 1.18, 1.22, 1.19, 1.21, 1.20, 1.18, 2.15]),
            ("VcRowHoverBrush", light, [1.25, 1.22, 1.22, 1.25, 1.22, 1.25, 1.24, 1.22, 2.14]),
            ("VcSelectionBrush", light, [1.32, 1.29, 1.28, 1.32, 1.29, 1.31, 1.30, 1.28, 2.11]),
        ];

        // The labels' pairs with a fill, in DimmedRowLabels' order, a fill of the row's lane once for
        // each lane; each background of DimmedRowBackgrounds in both themes.
        var labels = DimmedRowLabels.Where(label => label.Fill is not null)
            .Select(label => (label.Text, Fill: label.Fill!))
            .Distinct()
            .SelectMany(label => label.Fill == RowLane ? Lanes.Select(lane => (label.Text, Fill: lane)) : new[] { label })
            .ToArray();
        (string Text, string Fill)[] columns = [.. Lanes.Select(lane => ("VcLabelTextBrush", lane)), ("VcTextPrimaryBrush", "VcControlBackgroundBrush")];
        Assert.Equal(columns, labels);
        Assert.Equal(
            DimmedRowBackgrounds.SelectMany(background => Variants.Select(variant => $"{variant}: {background.Token}")).Order(StringComparer.Ordinal),
            pins.Select(pin => $"{pin.Variant}: {pin.Background}").Order(StringComparer.Ordinal));

        var failures = new List<string>();
        foreach (var (background, variant, pinned) in pins)
        {
            Assert.Equal(labels.Length, pinned.Length);
            var behind = Token(background, variant);
            for (var i = 0; i < labels.Length; i++)
            {
                var (text, fill) = labels[i];
                var faded = Faded(Token(fill, variant), behind);
                var ratio = Contrast.Ratio(Faded(Token(text, variant), faded), faded);
                ExpectPinned(failures, $"(c) a dimmed row's label, {text} on {fill} over {background}", variant, ratio, pinned[i], faded: true);
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    /// <summary>
    /// D469's exception (c), held to what the graph draws. The pins above compute from three tables,
    /// <see cref="DimmedRowTexts"/>, <see cref="DimmedRowLabels"/> and
    /// <see cref="DimmedRowBackgrounds"/>; this fact holds <c>CommitGraphControl</c> to them. The
    /// graph is drawn with a colour of its own standing in for each token of the tables
    /// (<see cref="DimmedRowStandIns"/>, put in the control's resources, where it looks its tokens
    /// up), so that a text drawn in any other token, or in a colour written out, shows. In each
    /// theme, with HEAD on a branch each dimmed row, and with HEAD detached HEAD's row, is read at
    /// rest, under the pointer and selected: the row's background is its state's token; in each
    /// text's column every pixel lies between the background and the text's token faded over it, and
    /// the ink (<see cref="TextInk"/>'s rule) reaches that colour; in each label the fill is its token
    /// faded over the background, within a step, every pixel 3 px inside the label's edge lies
    /// between the fill and the text's token faded over the fill, and the ink of the name and of a
    /// pull request's number reaches that colour; in the labels' column outside the labels, a pixel
    /// left round each for its edge, every pixel is the row's background, within 2. Nothing is drawn
    /// outside the six columns, so that a new column under the dimming fails here. The graph's column
    /// is not read: a text or a mark drawn there under the node's dimming is read by neither this
    /// fact nor the guard.
    /// </summary>
    [AvaloniaFact]
    public void Exception_D469_c_the_graph_draws_a_dimmed_rows_texts_and_labels_in_the_tokens_the_pins_name()
    {
        var failures = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in Variants)
        {
            foreach (var detachedHead in new[] { false, true })
            {
                var data = DimmedRowsHistory(detachedHead);
                var control = new CommitGraphControl
                {
                    Data = data,
                    Dates = new DateDisplay(TimeZoneInfo.Utc),
                    Search = new GraphSearchMarks([data.CommitAt(0).Sha], isActive: true),
                    PullRequests = new Dictionary<string, PullRequest>(StringComparer.Ordinal) { ["refs/heads/topic"] = TopicPullRequest(data.CommitAt(2).Sha) },
                    SelectedIndex = 0,
                };
                foreach (var (token, colour) in DimmedRowStandIns())
                {
                    control.Resources[token] = new SolidColorBrush(colour);
                }

                using var host = CommitGraphControlTests.Host.Show(control, DimmedRowsWidth, data.Count * CommitGraphControl.RowHeight, variant);
                Assert.Equal(GraphSearchRowState.Marked, control.SearchStateOf(0));

                // Detached, only HEAD's row differs from the history with HEAD on a branch.
                int[] rows = detachedHead ? [1] : [.. Enumerable.Range(1, data.Count - 1)];
                Assert.All(rows, row => Assert.Equal(GraphSearchRowState.Dimmed, control.SearchStateOf(row)));
                var rest = host.Capture();
                foreach (var row in rows)
                {
                    CheckDimmedRow(failures, seen, control, rest, row, "at rest", variant);
                }

                foreach (var row in rows)
                {
                    host.MoveMouse(control, new Point(control.Columns.Message.X + 4, control.RowBounds(row).Center.Y));
                    CheckDimmedRow(failures, seen, control, host.Capture(), row, "under the pointer", variant);
                }

                host.MoveMouse(control, new Point(control.Columns.Message.X + 4, control.RowBounds(0).Center.Y));
                foreach (var row in rows)
                {
                    control.SelectedIndex = row;
                    CheckDimmedRow(failures, seen, control, host.Capture(), row, "selected", variant);
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));

        // Every text and label of the tables was drawn and read, and a pull request's number.
        Assert.Equal(
            DimmedRowTexts.Select(cell => cell.Text).Concat(DimmedRowLabels.Select(label => label.Label)).Append(PullRequestNumber).Order(StringComparer.Ordinal),
            seen.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// D469's guard: in the files the app is built from (every XAML and C# file under
    /// src/VisualCommit.App, bin and obj left out), the disabled text colour, by its token and by
    /// its colours written out, a class whose own style gives a text that colour
    /// (<see cref="DimmingClasses"/>), where it is applied, and the search's dimming, by its
    /// constant and by its value written out, are used only where <see cref="DimmedTextUses"/>
    /// lists them, each with its reason: a definition, a disabled state (a <c>:disabled</c>
    /// selector, a class bound to a disabled command, or one set in code by a line that names
    /// <c>IsEnabled</c>), a mark that is no text, a class's name that applies no dimming there, or
    /// one of D469's three exceptions by its place. The table keeps the rules
    /// <see cref="AssertGuardTable"/> names. A new reference to the dimmed colour or to the search's
    /// dimming, or a new application of such a class by its name (or a listed one bound otherwise,
    /// since a class's place holds its binding), fails with its file and line, as far as a reading
    /// of the text sees it (<see cref="DimmedNeedles"/>: a class applied through a constant, for
    /// one, is seen only where the constant is defined); a listed use that is gone fails too, and so
    /// does a scan that finds the token or the constant nowhere, so that a rename cannot leave the
    /// guard looking for nothing. A text that comes under a dimming already listed adds no
    /// reference: the render fact of exception (c) reads a dimmed row's message, author, date and
    /// SHA columns, the inside of each label and the labels' column outside them, where such a text
    /// shows; a text or a mark drawn in the graph's column under the node's dimming is read by
    /// neither fact.
    /// </summary>
    [AvaloniaFact]
    public void The_dimmed_colour_and_the_searchs_dimming_are_used_only_where_the_guard_lists_them()
    {
        var problems = GuardProblems(ScanAppSource(DimmedNeedles()), DimmedTextUses);
        Assert.True(problems.Count == 0, "Uses of the dimmed text colour or of the search's dimming (D469's guard):\n" + string.Join('\n', problems));
        AssertGuardTable(DimmedTextUses);
    }

    /// <summary>
    /// The rules the guard's table (<paramref name="accepted"/>) keeps: every use has its reason, and
    /// only a class's name is listed as applying no dimming; a use under a disabled state names it,
    /// a class set in code by <c>IsEnabled</c> in its line; every class that a listed style in XAML
    /// names outside a disabled state is a dimming class (<see cref="DimmingClasses"/>) or one of
    /// <see cref="NonDimmingClasses"/>, and each of those is named so; each exception gives the
    /// colour, or the dimming, at one place, and what applies it lies in the same file.
    /// </summary>
    private static void AssertGuardTable(IReadOnlyList<AcceptedUse> accepted)
    {
        foreach (var entry in accepted)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Reason), $"{entry.File}: \"{entry.Place}\" is listed without its reason.");
            Assert.True(
                entry.Kind != DimmedUseKind.NotDimming || entry.Needle == DimmedNeedle.Class,
                $"{entry.File}: \"{entry.Place}\" is listed as applying no dimming, which only a class's name can be.");
        }

        foreach (var entry in accepted.Where(entry => entry.Kind == DimmedUseKind.Disabled))
        {
            // A class set in code under a disabled state names that state in its line.
            if (entry.Needle == DimmedNeedle.Class && !IsXaml(entry.File))
            {
                Assert.True(
                    entry.Place.Contains("IsEnabled", StringComparison.Ordinal),
                    $"{entry.File}: \"{entry.Place}\" is listed under a disabled state, but its line names no IsEnabled.");
                continue;
            }

            Assert.True(
                entry.Place.Contains(":disabled", StringComparison.Ordinal) || entry.Place.Contains(".disabled", StringComparison.Ordinal),
                $"{entry.File}: \"{entry.Place}\" is listed under a disabled state, but its place names none.");

            // A class applied under a disabled state is bound to the command's own state.
            Assert.True(
                entry.Needle != DimmedNeedle.Class || entry.Place.EndsWith("={Binding !IsEnabled}", StringComparison.Ordinal),
                $"{entry.File}: \"{entry.Place}\" is listed under a disabled state, but the class is not bound to !IsEnabled.");
        }

        // A class a listed style names gives a text the colour where it is applied, unless the style
        // applies under a disabled state alone or the class needs another beside it.
        var named = accepted
            .Where(entry => entry.Needle is DimmedNeedle.Token or DimmedNeedle.Colour && IsXaml(entry.File))
            .SelectMany(entry => StyleClassesOf(entry.Place).Select(name => (Class: name, entry.File, Style: entry.Place)))
            .ToList();
        var required = named.Where(use => !NonDimmingClasses.Any(none => (none.Class, none.File) == (use.Class, use.File))).ToList();
        var listed = DimmingClasses.Select(dimming => (dimming.Class, dimming.File, dimming.Style)).ToList();
        var untied = required.Except(listed)
            .Select(use => $"{use.File}: \"{use.Style}\" names the class {use.Class}, which DimmingClasses does not list: list it there, or in NonDimmingClasses with its reason.")
            .Concat(listed.Except(required).Select(dimming => $"{dimming.File}: DimmingClasses lists {dimming.Class} in \"{dimming.Style}\", which no listed style names so: take it out."))
            .Concat(NonDimmingClasses.Where(none => !named.Any(use => (use.Class, use.File) == (none.Class, none.File)))
                .Select(none => $"{none.File}: NonDimmingClasses lists {none.Class}, which no listed style names: take it out."))
            .ToList();
        Assert.True(untied.Count == 0, string.Join('\n', untied));

        // Each exception gives the colour, or the dimming, at one place, by the token, its colour, the
        // constant or its value; what applies it lies in the same file.
        foreach (var kind in new[] { DimmedUseKind.ExceptionA, DimmedUseKind.ExceptionB, DimmedUseKind.ExceptionC })
        {
            var entry = Assert.Single(accepted, entry => entry.Kind == kind && entry.Needle != DimmedNeedle.Class);
            Assert.All(accepted.Where(other => other.Kind == kind), other => Assert.Equal(entry.File, other.File));
        }
    }

    /// <summary>
    /// The classes that the styles of a place in XAML name (<see cref="DimmedTextUses"/> says how a
    /// place is named) outside a disabled state: in each style's selector the alternatives that name
    /// no <c>:disabled</c>, property matches in brackets left out; none when a style of the place
    /// applies under <c>:disabled</c> alone. A control theme's key is no selector.
    /// </summary>
    private static List<string> StyleClassesOf(string place)
    {
        // A selector's child combinator, " > ", is also what joins a place's scopes.
        var selectors = new List<string>();
        foreach (var part in place.Split(" > ")[..^1])
        {
            if (part.StartsWith("Style ", StringComparison.Ordinal))
            {
                selectors.Add(part["Style ".Length..]);
            }
            else if (selectors.Count > 0 && !part.StartsWith("ControlTheme ", StringComparison.Ordinal) && !part.StartsWith("ResourceDictionary ", StringComparison.Ordinal))
            {
                selectors[^1] += " > " + part;
            }
        }

        var disabled = new Regex(@"(?<!not\():disabled\b");
        var alternatives = selectors.Select(selector => Regex.Replace(selector, @"\[[^\]]*\]", string.Empty).Split(',')).ToList();
        return alternatives.Any(each => each.All(disabled.IsMatch))
            ? []
            : [.. alternatives.SelectMany(each => each)
                .Where(alternative => !disabled.IsMatch(alternative))
                .SelectMany(alternative => Regex.Matches(alternative, @"\.([\w-]+)").Select(match => match.Groups[1].Value))
                .Distinct()];
    }

    /// <summary>
    /// The guard's scan reports a use it does not list with its file and line (made-up uses in copies
    /// of the app's own texts; src/ is never changed): a style that dims a new text, an element's
    /// colour written out, a pushed opacity at the search's constant and its value written out, the
    /// colour in code by each of the ways <see cref="DimmedNeedles"/> knows, a dimming class applied
    /// in XAML, by a list and by a binding, and in code, and a <c>.xaml</c> file. A mention in a
    /// comment is no use, nor are channels one off or given to another method, nor the opacity's
    /// value in a geometry's path data.
    /// </summary>
    [AvaloniaFact]
    public void The_guard_reports_a_use_it_does_not_list_with_its_file_and_line()
    {
        var needles = DimmedNeedles();
        const string Panel = AppSource + "/Views/Shell/LeftPanelView.axaml";
        const string Render = AppSource + "/Controls/CommitGraphControl.Render.cs";
        var panel = SourceLines(Panel);
        var render = SourceLines(Render);

        // A style for a new dimmed text, an element in the colour written out, two elements that take
        // the dimming class, by a list and by a binding, and a comment; the opacity's value in path
        // data, which is no use, and on an element.
        var head = Array.FindIndex(panel, line => line.Contains("<Style Selector=\"TextBlock.refName.head\">", StringComparison.Ordinal));
        Assert.True(head > 0, "LeftPanelView.axaml has no style TextBlock.refName.head to insert before.");
        var colour = Token(DimmedToken, ThemeVariant.Light);
        var hex = string.Create(CultureInfo.InvariantCulture, $"#{colour.R:x2}{colour.G:x2}{colour.B:x2}");
        string[] insertedXaml =
        [
            "    <!-- VcTextDisabledBrush and Classes.dimmed in a comment are no use. -->",
            "    <Style Selector=\"TextBlock.refName.stale\">",
            "      <Setter Property=\"Foreground\" Value=\"{DynamicResource VcTextDisabledBrush}\" />",
            "    </Style>",
            $"    <Style Selector=\"TextBlock.refName.gone\"><Setter Property=\"Foreground\" Value=\"{hex}\" /></Style>",
            "    <TextBlock Name=\"ListedCopy\" Classes=\"refName dimmed\" />",
            "    <TextBlock Name=\"BoundCopy\" Classes.dimmed=\"{Binding IsDimmed}\" />",
            "    <Path Data=\"M1 .35 2\" />",
            "    <StreamGeometry x:Key=\"Probe\">M1 .35 2</StreamGeometry>",
            "    <Border Name=\"Faded\" Opacity=\"0.35\" />",
        ];
        var changedPanel = string.Join('\n', panel[..head].Concat(insertedXaml).Concat(panel[head..]));

        // A pushed opacity at the constant, its value written out, and both in comments; the colour
        // in code, packed with another alpha, and by its channels, hexadecimal and decimal; the value
        // with a trailing zero; a dimming class set in code.
        var drawTexts = Array.FindIndex(render, line => line.Contains("DrawTextCells(context, GetRowVisual(data, index)", StringComparison.Ordinal));
        Assert.True(drawTexts > 0, "CommitGraphControl.Render.cs has no call of DrawTextCells to insert before.");
        var dark = Token(DimmedToken, ThemeVariant.Dark);
        var packed = string.Create(CultureInfo.InvariantCulture, $"var packed = Color.FromUInt32(0xCC{dark.R:X2}{dark.G:X2}{dark.B:X2});");
        var channels = string.Create(CultureInfo.InvariantCulture, $"var channels = Color.FromRgb(0x{dark.R:X2}, 0x{dark.G:x2}, 0x{dark.B:X2});");
        var decimals = string.Create(CultureInfo.InvariantCulture, $"var decimals = new Color(255, {colour.R}, {colour.G}, {colour.B});");
        string[] insertedCode =
        [
            "                // SearchDimmedOpacity and 0.35 in a comment are no use.",
            "                using var again = context.PushOpacity(SearchDimmedOpacity); /* 0.35 */",
            "                using var fixedValue = context.PushOpacity(0.35); // SearchDimmedOpacity",
            "                " + packed,
            "                " + channels,
            "                " + decimals,
            "                var trailing = .350;",
            "                label.Classes.Set(\"dropped\", true); // \"dimmed\"",
        ];

        // The colour by its channels after an alpha in parentheses, a computed cast and a call, to a
        // constructor named in full and to one typed by its target, and in CSS's form to parse; a
        // channel one off, and the channels to another method, are no use.
        string[] colourForms =
        [
            string.Create(CultureInfo.InvariantCulture, $"var computed = Color.FromArgb((byte)(255 * share), {dark.R}, {dark.G}, {dark.B});"),
            string.Create(CultureInfo.InvariantCulture, $"var capped = Color.FromArgb(Math.Min(alpha, 255), 0x{dark.R:X2}, 0x{dark.G:X2}, 0x{dark.B:X2});"),
            string.Create(CultureInfo.InvariantCulture, $"var qualified = new Avalonia.Media.Color(255, {colour.R}, {colour.G}, {colour.B});"),
            string.Create(CultureInfo.InvariantCulture, $"Color targeted = new(255, {colour.R}, {colour.G}, {colour.B});"),
            string.Create(CultureInfo.InvariantCulture, $"var css = Color.Parse(\"rgba({colour.R}, {colour.G}, {colour.B}, 0.5)\");"),
        ];
        string[] noUses =
        [
            string.Create(CultureInfo.InvariantCulture, $"var oneOff = Color.FromArgb((byte)204, {dark.R}, {dark.G}, {dark.B + 1});"),
            string.Create(CultureInfo.InvariantCulture, $"var other = Mix((byte)204, {dark.R}, {dark.G}, {dark.B});"),
        ];
        insertedCode = [.. insertedCode, .. colourForms.Concat(noUses).Select(line => "                " + line)];
        var changedRender = string.Join('\n', render[..drawTexts].Concat(insertedCode).Concat(render[drawTexts..]));

        // A XAML file named .xaml, which Avalonia builds too, is read as XAML.
        const string Copied = AppSource + "/Views/Shell/Copied.xaml";
        var copied = $"<Styles xmlns=\"https://github.com/avaloniaui\">\n  <Style Selector=\"TextBlock.copied\"><Setter Property=\"Foreground\" Value=\"#FF{hex[1..]}\" /></Style>\n</Styles>";

        var uses = ScanAppSource(needles).Where(use => use.File is not (Panel or Render)).ToList();
        uses.AddRange(ScanText(Panel, changedPanel, needles));
        uses.AddRange(ScanText(Render, changedRender, needles));
        uses.AddRange(ScanText(Copied, copied, needles));
        var problems = GuardProblems(uses, DimmedTextUses);

        var lines = (Xaml: head + 3, Hex: head + 5, Constant: drawTexts + 2, Value: drawTexts + 3);
        Assert.Equal(
            [
                $"{Panel}:{lines.Xaml}: the disabled text colour's token in \"Style TextBlock.refName.stale > Setter Foreground\"",
                $"{Panel}:{lines.Hex}: the disabled text colour written out in \"Style TextBlock.refName.gone > Setter Foreground\"",
                $"{Panel}:{lines.Hex + 1}: a class whose style gives a text the disabled colour in \"TextBlock ListedCopy @Classes=refName dimmed\"",
                $"{Panel}:{lines.Hex + 2}: a class whose style gives a text the disabled colour in \"TextBlock BoundCopy @Classes.dimmed={{Binding IsDimmed}}\"",
                $"{Panel}:{lines.Hex + 5}: the search's dimming's value written out in \"Border Faded @Opacity\"",
                $"{Render}:{lines.Constant}: the search's dimming in \"using var again = context.PushOpacity(SearchDimmedOpacity);\"",
                $"{Render}:{lines.Value}: the search's dimming's value written out in \"using var fixedValue = context.PushOpacity(0.35);\"",
                $"{Render}:{lines.Value + 1}: the disabled text colour written out in \"{packed}\"",
                $"{Render}:{lines.Value + 2}: the disabled text colour written out in \"{channels}\"",
                $"{Render}:{lines.Value + 3}: the disabled text colour written out in \"{decimals}\"",
                $"{Render}:{lines.Value + 4}: the search's dimming's value written out in \"var trailing = .350;\"",
                $"{Render}:{lines.Value + 5}: a class whose style gives a text the disabled colour in \"label.Classes.Set(\"dropped\", true);\"",
                .. colourForms.Select((form, i) => $"{Render}:{lines.Value + 6 + i}: the disabled text colour written out in \"{form}\""),
                $"{Copied}:2: the disabled text colour written out in \"Style TextBlock.copied > Setter Foreground\"",
            ],
            problems.Select(problem => problem.IndexOf(", a use the guard does not list", StringComparison.Ordinal) is var at and >= 0 ? problem[..at] : problem));
        Assert.All(problems, problem => Assert.Contains("add it to DimmedTextUses with its reason", problem, StringComparison.Ordinal));
    }

    /// <summary>
    /// The guard's other two failures (copies of the app's own texts; src/ is never changed): a
    /// listed use that is gone, here exception (b) fixed by the secondary text colour, and a scan
    /// that finds the token or the constant nowhere, as after a rename.
    /// </summary>
    [AvaloniaFact]
    public void The_guard_reports_a_listed_use_that_is_gone_and_a_token_it_finds_nowhere()
    {
        var needles = DimmedNeedles();
        const string Rebase = AppSource + "/Views/Rebase/InteractiveRebaseView.axaml";
        var rebase = SourceLines(Rebase);
        var dropped = Array.FindIndex(rebase, line => line.Contains("<Style Selector=\"TextBlock.dropped\">", StringComparison.Ordinal));
        Assert.True(dropped > 0 && rebase[dropped + 1].Contains(DimmedToken, StringComparison.Ordinal), "InteractiveRebaseView.axaml's style TextBlock.dropped does not set the disabled colour first.");
        rebase[dropped + 1] = rebase[dropped + 1].Replace(DimmedToken, "VcTextSecondaryBrush", StringComparison.Ordinal);
        var uses = ScanAppSource(needles).Where(use => use.File != Rebase).ToList();
        uses.AddRange(ScanText(Rebase, string.Join('\n', rebase), needles));

        var gone = Assert.Single(GuardProblems(uses, DimmedTextUses));
        Assert.StartsWith($"{Rebase}: no longer the disabled text colour's token in \"Style TextBlock.dropped > Setter Foreground\" (ExceptionB:", gone, StringComparison.Ordinal);

        // A source that names neither the token nor the constant: each is reported, beside every listed use now gone.
        var renamed = GuardProblems(ScanText(AppSource + "/Theme/Renamed.axaml", "<Styles xmlns=\"https://github.com/avaloniaui\" />", needles), DimmedTextUses);
        Assert.Contains(renamed, problem => problem.StartsWith("The app's source names the disabled text colour's token nowhere", StringComparison.Ordinal));
        Assert.Contains(renamed, problem => problem.StartsWith("The app's source names the search's dimming nowhere", StringComparison.Ordinal));
        Assert.Equal(DimmedTextUses.Length, renamed.Count(problem => problem.Contains(": no longer ", StringComparison.Ordinal)));
    }

    /// <summary>A label's fill in <see cref="DimmedRowLabels"/>: the row's lane, <c>VcLane{n}Brush</c> for the row's colour n; all eight lanes in the pins.</summary>
    private const string RowLane = "the row's lane";

    /// <summary>What the render fact reads besides the tables' texts and labels: a pull request's number, in its label's colours.</summary>
    private const string PullRequestNumber = "a pull request's number";

    /// <summary>The width the render fact shows the graph at: every column visible.</summary>
    private const double DimmedRowsWidth = 760;

    /// <summary>
    /// D469's exception (c): each text of a row a search dims and the token <c>CommitGraphControl</c>
    /// draws it in (<c>CreateRowVisual</c>, <c>AuthorLayout</c>). The pins compute from it; the
    /// render fact holds the graph to it.
    /// </summary>
    private static readonly (string Text, string Token)[] DimmedRowTexts =
    [
        ("the message", "VcTextPrimaryBrush"),
        ("a stash's message", "VcTextSecondaryBrush"),
        ("the author", "VcTextSecondaryBrush"),
        ("the date", "VcTextSecondaryBrush"),
        ("the SHA", "VcTextSecondaryBrush"),
    ];

    /// <summary>
    /// D469's exception (c): each label of a row a search dims, its text's token and its fill's,
    /// as <c>CommitGraphControl</c> draws them (<c>LabelLayout</c>, <c>LabelColors</c>; null for no
    /// fill), a pull request's number in its label's. The pins compute from it; the render fact
    /// holds the graph to it.
    /// </summary>
    private static readonly (string Label, string Text, string? Fill)[] DimmedRowLabels =
    [
        ("the current branch's label", "VcLabelTextBrush", RowLane),
        ("a local branch's label", "VcLabelTextBrush", RowLane),
        ("a detached HEAD's label", "VcLabelTextBrush", RowLane),
        ("a tag's label", "VcTextPrimaryBrush", "VcControlBackgroundBrush"),
        ("a stash's label", "VcTextPrimaryBrush", "VcControlBackgroundBrush"),
        ("the +N label", "VcTextPrimaryBrush", "VcControlBackgroundBrush"),
        ("a remote branch's label", "VcTextPrimaryBrush", null),
    ];

    /// <summary>
    /// D469's exception (c): a graph row's background in each of its states, as
    /// <c>RowBackground</c> picks it, which a search leaves as it is. The pins compute from it; the
    /// render fact holds the graph to it.
    /// </summary>
    private static readonly (string State, string Token)[] DimmedRowBackgrounds =
    [
        ("at rest", "VcWindowBackgroundBrush"),
        ("under the pointer", "VcRowHoverBrush"),
        ("selected", "VcSelectionBrush"),
    ];

    /// <summary>
    /// Every use of the disabled text colour and of the search's dimming that D469's guard accepts,
    /// each with its reason. Places are as the scan names them: in a XAML file the enclosing
    /// control themes, styles and theme dictionaries, then the setter's property or the element and
    /// its attribute, and for a class applied the attribute's value; in a C# file the line's code,
    /// comments left out.
    /// </summary>
    private static readonly AcceptedUse[] DimmedTextUses =
    [
        // The token and its colours, defined once in each theme.
        new(AppSource + "/Theme/Tokens.axaml", DimmedNeedle.Token, "ResourceDictionary Dark > SolidColorBrush VcTextDisabledBrush @x:Key", DimmedUseKind.Definition, "the token's definition, dark"),
        new(AppSource + "/Theme/Tokens.axaml", DimmedNeedle.Colour, "ResourceDictionary Dark > SolidColorBrush VcTextDisabledBrush @Color", DimmedUseKind.Definition, "the token's colour, dark"),
        new(AppSource + "/Theme/Tokens.axaml", DimmedNeedle.Token, "ResourceDictionary Light > SolidColorBrush VcTextDisabledBrush @x:Key", DimmedUseKind.Definition, "the token's definition, light"),
        new(AppSource + "/Theme/Tokens.axaml", DimmedNeedle.Colour, "ResourceDictionary Light > SolidColorBrush VcTextDisabledBrush @Color", DimmedUseKind.Definition, "the token's colour, light"),

        // The same colours as a text box's border under the pointer: a line round the box, no text.
        new(AppSource + "/Theme/Tokens.axaml", DimmedNeedle.Colour, "ResourceDictionary Dark > SolidColorBrush TextControlBorderBrushPointerOver @Color", DimmedUseKind.NotText, "a text box's border under the pointer, dark: no text"),
        new(AppSource + "/Theme/Tokens.axaml", DimmedNeedle.Colour, "ResourceDictionary Light > SolidColorBrush TextControlBorderBrushPointerOver @Color", DimmedUseKind.NotText, "a text box's border under the pointer, light: no text"),

        // The controls' disabled states, which WCAG exempts.
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme {x:Type c:ToolbarButton} > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled toolbar button's icon and label"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcFlatButton > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled flat button"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcActionButton > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled action button"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcLinkButton > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled link"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcIconButton > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled icon button"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcPrimaryButton > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled primary button, and the danger button based on it"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcCheckBox > Style ^:pointerover /template/ Border#Box > Setter BorderBrush", DimmedUseKind.NotText, "a check box's border under the pointer: a line round the box, no text"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcCheckBox > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled check box's text"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcCheckBox > Style ^:disabled:checked /template/ Border#Box > Setter Background", DimmedUseKind.Disabled, "a disabled ticked box's fill"),
        new(AppSource + "/Theme/Controls.axaml", DimmedNeedle.Token, "ControlTheme VcCheckBox > Style ^:disabled:checked /template/ Border#Box > Setter BorderBrush", DimmedUseKind.Disabled, "a disabled ticked box's border"),
        new(AppSource + "/Theme/Dialogs.axaml", DimmedNeedle.Token, "ControlTheme VcRadioButton > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled radio button's text"),
        new(AppSource + "/Theme/Menus.axaml", DimmedNeedle.Token, "Style MenuFlyoutPresenter.vcContextMenu MenuItem:disabled TextBlock.vcMenuKey > Setter Foreground", DimmedUseKind.Disabled, "a disabled menu item's key"),
        new(AppSource + "/Theme/Menus.axaml", DimmedNeedle.Token, "Style MenuFlyoutPresenter.vcContextMenu MenuItem:disabled /template/ ContentPresenter#PART_HeaderPresenter > Setter Foreground", DimmedUseKind.Disabled, "a disabled menu item's words"),
        new(AppSource + "/Views/Commands/CommandPaletteView.axaml", DimmedNeedle.Token, "Style TextBlock.disabled > Setter Foreground", DimmedUseKind.Disabled, "a palette entry whose command is disabled, its label, key and the reason shown in its detail's place (the class is bound to !IsEnabled): the entry can be highlighted, but it runs nothing, and its tooltip repeats the reason"),
        new(AppSource + "/Views/Commands/CommandPaletteView.axaml", DimmedNeedle.Class, "TrimmedTextBlock CommandPaletteItemLabel @Classes.disabled={Binding !IsEnabled}", DimmedUseKind.Disabled, "a disabled palette entry's label"),
        new(AppSource + "/Views/Commands/CommandPaletteView.axaml", DimmedNeedle.Class, "TrimmedTextBlock CommandPaletteItemDetail @Classes.disabled={Binding !IsEnabled}", DimmedUseKind.Disabled, "a disabled palette entry's detail, its reason"),
        new(AppSource + "/Views/Commands/CommandPaletteView.axaml", DimmedNeedle.Class, "TextBlock CommandPaletteItemKey @Classes.disabled={Binding !IsEnabled}", DimmedUseKind.Disabled, "a disabled palette entry's key"),
        new(AppSource + "/Views/Shell/CompareView.axaml", DimmedNeedle.Token, "ControlTheme CompareLinkButton > Style ^:disabled > Setter Foreground", DimmedUseKind.Disabled, "a disabled From or To link: no row in the graph"),

        // D469's exceptions (a) and (b), pinned above.
        new(AppSource + "/Views/Shell/LeftPanelView.axaml", DimmedNeedle.Token, "Style TextBlock.refName.dimmed > Setter Foreground", DimmedUseKind.ExceptionA, "a hidden branch's name, and in solo mode every branch left out"),
        new(AppSource + "/Views/Shell/LeftPanelView.axaml", DimmedNeedle.Class, "TextBlock RefName @Classes.dimmed={Binding IsDimmed}", DimmedUseKind.ExceptionA, "a ref row's name, while the graph hides the ref or a solo leaves it out"),
        new(AppSource + "/Views/Rebase/InteractiveRebaseView.axaml", DimmedNeedle.Token, "Style TextBlock.dropped > Setter Foreground", DimmedUseKind.ExceptionB, "a dropped row's SHA and subject"),
        new(AppSource + "/Views/Rebase/InteractiveRebaseView.axaml", DimmedNeedle.Class, "TextBlock RebaseRowSha @Classes.dropped={Binding IsDropped}", DimmedUseKind.ExceptionB, "a dropped row's SHA"),
        new(AppSource + "/Views/Rebase/InteractiveRebaseView.axaml", DimmedNeedle.Class, "TrimmedTextBlock RebaseRowSubject @Classes.dropped={Binding IsDropped}", DimmedUseKind.ExceptionB, "a dropped row's subject"),

        // The search's dimming: its definition, D469's exception (c), and the node, which is no text.
        new(AppSource + "/Controls/CommitGraphControl.Search.cs", DimmedNeedle.Dimming, "public const double SearchDimmedOpacity = 0.35;", DimmedUseKind.Definition, "the constant's definition"),
        new(AppSource + "/Controls/CommitGraphControl.Search.cs", DimmedNeedle.DimmingValue, "public const double SearchDimmedOpacity = 0.35;", DimmedUseKind.Definition, "the constant's value"),
        new(AppSource + "/Controls/CommitGraphControl.Render.cs", DimmedNeedle.Dimming, "using var opacity = IsDimmed(data, index, search) ? context.PushOpacity(SearchDimmedOpacity) : default(DrawingContext.PushedState?);", DimmedUseKind.ExceptionC, "a dimmed row's labels, and its message, author, date and SHA (Render)", Count: 2),
        new(AppSource + "/Controls/CommitGraphControl.Render.cs", DimmedNeedle.Dimming, "using var opacity = dimmed ? context.PushOpacity(SearchDimmedOpacity) : default(DrawingContext.PushedState?);", DimmedUseKind.NotText, "a dimmed row's node (DrawGraphCell): a mark, no text"),
    ];

    /// <summary>What D469's guard looks for.</summary>
    private enum DimmedNeedle
    {
        /// <summary><c>VcTextDisabledBrush</c>, the disabled text colour's token.</summary>
        Token,

        /// <summary>The token's colour in either theme written out, with any alpha (<see cref="DimmedNeedles"/> says how).</summary>
        Colour,

        /// <summary><see cref="CommitGraphControl.SearchDimmedOpacity"/>, the opacity a search dims a row to.</summary>
        Dimming,

        /// <summary>That opacity's value written out.</summary>
        DimmingValue,

        /// <summary>A class of <see cref="DimmingClasses"/> applied to an element, in XAML or in code.</summary>
        Class,
    }

    /// <summary>
    /// The classes whose own style gives a text the disabled colour, each style a listed use, with
    /// that style's file and place: where the app applies one, a text takes the colour without
    /// naming it, so each place it is applied is a use the guard lists too. Every class a listed
    /// style names outside a disabled state is one of these or of <see cref="NonDimmingClasses"/>.
    /// </summary>
    private static readonly (string Class, string File, string Style)[] DimmingClasses =
    [
        ("disabled", AppSource + "/Views/Commands/CommandPaletteView.axaml", "Style TextBlock.disabled > Setter Foreground"),
        ("dimmed", AppSource + "/Views/Shell/LeftPanelView.axaml", "Style TextBlock.refName.dimmed > Setter Foreground"),
        ("dropped", AppSource + "/Views/Rebase/InteractiveRebaseView.axaml", "Style TextBlock.dropped > Setter Foreground"),
    ];

    /// <summary>
    /// The classes a listed style in XAML names outside a disabled state that are no dimming class,
    /// each with its file and its reason: where the app applies one alone, no text takes the colour.
    /// </summary>
    private static readonly (string Class, string File, string Reason)[] NonDimmingClasses =
    [
        ("refName", AppSource + "/Views/Shell/LeftPanelView.axaml", "every ref row's name takes it; its style gives the colour only with dimmed beside it"),
    ];

    /// <summary>Why D469's guard accepts a use; the exceptions last, in their order.</summary>
    private enum DimmedUseKind
    {
        /// <summary>Where the token, its colour or the constant is defined.</summary>
        Definition,

        /// <summary>
        /// Under a disabled state, which WCAG exempts: a <c>:disabled</c> selector, a class bound to a
        /// disabled command, or a class set in code by a line that names <c>IsEnabled</c>.
        /// </summary>
        Disabled,

        /// <summary>A mark that is no text: a border, a node.</summary>
        NotText,

        /// <summary>
        /// A dimming class's name that applies no dimming here: a string of code that is no class, or
        /// a class on an element that no listed style giving it the colour reaches.
        /// </summary>
        NotDimming,

        /// <summary>D469's exception (a): a hidden or left-out branch's name in the left panel.</summary>
        ExceptionA,

        /// <summary>D469's exception (b): a dropped row of the interactive rebase's list.</summary>
        ExceptionB,

        /// <summary>D469's exception (c): the graph's rows a search dims.</summary>
        ExceptionC,
    }

    /// <summary>A use the guard found: where, what, and its place (<see cref="DimmedTextUses"/> says how a place is named).</summary>
    private sealed record DimmedUse(string File, int Line, DimmedNeedle Needle, string Place);

    /// <summary>A use the guard accepts, <paramref name="Count"/> times at its place, with its reason.</summary>
    private sealed record AcceptedUse(string File, DimmedNeedle Needle, string Place, DimmedUseKind Kind, string Reason, int Count = 1);

    /// <summary>
    /// Adds a failure when <paramref name="ratio"/>, to two places, is not <paramref name="pinned"/>,
    /// or when it reaches 4.5:1; <paramref name="faded"/> for a pair of exception (c), drawn at the
    /// search's opacity, which <see cref="TextPairs"/> cannot hold.
    /// </summary>
    private static void ExpectPinned(List<string> failures, string what, ThemeVariant variant, double ratio, double pinned, bool faded = false)
    {
        if (Math.Round(ratio, 2) != pinned)
        {
            failures.Add(string.Create(CultureInfo.InvariantCulture, $"{variant}: {what} is {ratio:0.00}:1, not the {pinned:0.00}:1 D469 accepted. A colour or a state changed: look at it with the decision before pinning the new figure."));
        }

        if (ratio >= TextContrast)
        {
            var next = faded
                ? "Take the pin out with the decision, D469: TextPairs holds tokens at full opacity, not a faded pair."
                : "Put the pair in TextPairs and take it out of D469's pins.";
            failures.Add(string.Create(CultureInfo.InvariantCulture, $"{variant}: {what} is {ratio:0.00}:1 and reaches {TextContrast}:1: the exception is no longer needed. {next}"));
        }
    }

    /// <summary>A colour at the opacity a search dims a row to (<see cref="CommitGraphControl.SearchDimmedOpacity"/>) over <paramref name="behind"/>, each channel rounded to the nearest step.</summary>
    private static Color Faded(Color colour, Color behind)
    {
        static byte Mix(byte over, byte under) =>
            (byte)Math.Round((CommitGraphControl.SearchDimmedOpacity * over) + ((1 - CommitGraphControl.SearchDimmedOpacity) * under));
        return Color.FromRgb(Mix(colour.R, behind.R), Mix(colour.G, behind.G), Mix(colour.B, behind.B));
    }

    /// <summary>
    /// The colours that stand in for the tokens of <see cref="DimmedRowTexts"/>,
    /// <see cref="DimmedRowLabels"/> and <see cref="DimmedRowBackgrounds"/> in the render fact:
    /// saturated colours and black, far from each other and from the app's own colours, so that a
    /// text or a fill drawn in another token shows.
    /// </summary>
    private static List<(string Token, Color Colour)> DimmedRowStandIns() =>
    [
        ("VcWindowBackgroundBrush", Color.Parse("#000000")),
        ("VcRowHoverBrush", Color.Parse("#003C50")),
        ("VcSelectionBrush", Color.Parse("#50003C")),
        ("VcTextPrimaryBrush", Color.Parse("#FF0000")),
        ("VcTextSecondaryBrush", Color.Parse("#00FF00")),
        ("VcLabelTextBrush", Color.Parse("#FFFF00")),
        ("VcControlBackgroundBrush", Color.Parse("#0000FF")),
        .. Lanes.Select((lane, i) => (lane, Color.FromRgb(0x60, 0x00, (byte)(0x80 + (0x10 * i))))),
    ];

    /// <summary>The colour that stands in for <paramref name="token"/> in the render fact.</summary>
    private static Color StandIn(string token)
    {
        var standIns = DimmedRowStandIns().Where(standIn => standIn.Token == token).ToList();
        Assert.True(standIns.Count == 1, $"{token} has no colour of its own in DimmedRowStandIns: give it one, far from the others and from the app's colours.");
        return standIns[0].Colour;
    }

    /// <summary>
    /// The render fact's history, newest first: a commit the search matches, then a row for each
    /// label of <see cref="DimmedRowLabels"/> (HEAD's branch, or with <paramref name="detachedHead"/>
    /// HEAD and that branch as a local one; a local branch with a pull request; a tag; a remote
    /// branch; a long branch that leaves a "+N" for two tags; a stash), and a root without a label.
    /// </summary>
    private static CommitGraphData DimmedRowsHistory(bool detachedHead)
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        string[] subjects = ["Matches the search", "On main", "On topic", "Tagged", "On origin/main", "Crowded", "On main: Work in progress", "Initial commit"];
        static string Sha(int row) => (row + 1).ToString("x40", CultureInfo.InvariantCulture);

        // The stash and the commit above it both stand on the root.
        static string[] Parents(int row) => row switch { 5 or 6 => [Sha(7)], 7 => [], _ => [Sha(row + 1)] };
        var commits = Enumerable.Range(0, subjects.Length)
            .Select(row => new CommitInfo(Sha(row), Parents(row), "Test Author", "author@example.com", start.AddMinutes(-row), start.AddMinutes(-row), subjects[row], row == 6 ? CommitKind.Stash : CommitKind.Commit))
            .ToArray();
        GitRef[] refs =
        [
            new("refs/heads/a-rather-long-branch-name", "a-rather-long-branch-name", RefKind.LocalBranch, Sha(5)),
            new("refs/heads/main", "main", RefKind.LocalBranch, Sha(1), IsHead: !detachedHead),
            new("refs/heads/topic", "topic", RefKind.LocalBranch, Sha(2)),
            new("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, Sha(4), RemoteName: "origin"),
            new("refs/tags/t1", "t1", RefKind.Tag, Sha(5)),
            new("refs/tags/t2", "t2", RefKind.Tag, Sha(5)),
            new("refs/tags/v1.0", "v1.0", RefKind.Tag, Sha(3)),
        ];
        var stash = new StashEntry(0, Sha(6), Sha(7), subjects[6], "Test Author", "author@example.com", start.AddMinutes(-6), start.AddMinutes(-6));
        var data = new CommitGraphData(new RepoRefs(new HeadState(detachedHead ? null : "main", Sha(1)), refs, [stash], ["origin"]));
        var layout = new GraphLayout();
        data.Append(commits, [.. commits.Select(layout.Add)]);
        data.Complete();
        return data;
    }

    /// <summary>The open pull request of the render fact's branch <c>topic</c>, whose label shows <c>#7</c>.</summary>
    private static PullRequest TopicPullRequest(string headSha)
    {
        var repository = new HostedRepository(HostingProvider.GitHub, "example.com", "demo", "app");
        return new PullRequest(7, "Topic", "demo", false, new Uri("https://example.com/demo/app/7"), "topic", headSha, repository, "main", repository, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// Adds to <paramref name="failures"/> what is wrong with dimmed row <paramref name="row"/> in
    /// <paramref name="shot"/>, in <paramref name="state"/> (the render fact's rules), and to
    /// <paramref name="seen"/> the texts and labels it read.
    /// </summary>
    private static void CheckDimmedRow(List<string> failures, HashSet<string> seen, CommitGraphControl control, Screenshot shot, int row, string state, ThemeVariant variant)
    {
        var data = control.Data!;
        var where = string.Create(CultureInfo.InvariantCulture, $"{variant}: row {row} {state}");
        var backgroundToken = DimmedRowBackgrounds.Single(background => background.State == state).Token;
        var behind = StandIn(backgroundToken);
        var bounds = control.RowBounds(row);
        var columns = control.Columns;

        // The row's background, where nothing is drawn: at the end of the message's column.
        ExpectPixel(failures, shot, new Point(columns.Message.Right - 2, bounds.Y + 1), behind, 0, $"{where}: the background, {backgroundToken}");

        // The texts, each alone in its column.
        var message = data.CommitAt(row).Kind == CommitKind.Stash ? "a stash's message" : "the message";
        foreach (var (text, column) in new[] { (message, columns.Message), ("the author", columns.Author), ("the date", columns.Date), ("the SHA", columns.Sha) })
        {
            var token = DimmedRowTexts.Single(cell => cell.Text == text).Token;
            var area = new Rect(column.X, bounds.Y, column.Width, bounds.Height);
            var ink = Faded(StandIn(token), behind);
            var what = $"{where}: {text}, {token} over {backgroundToken}";
            failures.AddRange(TextInk.Problems(shot, area, ink, behind, what: what));
            failures.AddRange(BlendProblems(shot, area, behind, ink, what));
            seen.Add(text);
        }

        // Nothing outside the six columns.
        GraphColumn[] all = [columns.Refs, columns.Graph, columns.Message, columns.Author, columns.Date, columns.Sha];
        for (var x = 0; x < shot.Width; x++)
        {
            if (!all.Any(column => x >= column.X && x < column.Right))
            {
                failures.AddRange(BlendProblems(shot, new Rect(x, bounds.Y, 1, bounds.Height), behind, behind, $"{where}: outside the columns"));
            }
        }

        // The labels: the fill faded over the background; inside the border, the name, its icon and
        // a pull request's number faded over the fill.
        var labels = control.LabelsInRow(row);
        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            var name = LabelName(label);
            var (_, textToken, fillToken) = DimmedRowLabels.Single(entry => entry.Label == name);
            var fillKey = fillToken == RowLane ? Lanes[((data.RowAt(row).NodeColor % Lanes.Length) + Lanes.Length) % Lanes.Length] : fillToken;
            var fill = fillKey is null ? behind : Faded(StandIn(fillKey), behind);
            var ink = Faded(StandIn(textToken), fill);
            var what = $"{where}: {name}, {textToken} on {fillKey ?? "no fill"} over {backgroundToken}";
            ExpectPixel(failures, shot, new Point(label.Bounds.Right - 3, label.Bounds.Center.Y), fill, 1, what + ", its fill");
            var inside = label.Bounds.Deflate(3);
            failures.AddRange(BlendProblems(shot, inside, fill, ink, what));

            var icon = !label.IsOverflow && label.Kind is RefLabelKind.CurrentBranch or RefLabelKind.Tag
                ? CommitGraphControl.LabelIconSize + CommitGraphControl.LabelIconGap
                : 0;
            var badge = control.BadgeInRow(row, i);
            var nameArea = badge?.Name ?? new Rect(
                label.Bounds.X + CommitGraphControl.LabelPadding + icon,
                label.Bounds.Y,
                label.Bounds.Width - (2 * CommitGraphControl.LabelPadding) - icon,
                label.Bounds.Height);
            failures.AddRange(TextInk.Problems(shot, nameArea.Intersect(inside), ink, fill, what: what + ", its name"));
            seen.Add(name);
            if (badge is not null)
            {
                failures.AddRange(TextInk.Problems(shot, badge.Number.Intersect(inside), ink, fill, what: what + ", its pull request's number"));
                seen.Add(PullRequestNumber);
            }
        }

        // The labels' column outside the labels, each with a pixel round it for its edge: the
        // background alone, as nothing else is drawn there on a dimmed row.
        var drawn = labels.Select(label => label.Bounds.Inflate(1)).ToList();
        failures.AddRange(BlendProblems(
            shot,
            new Rect(columns.Refs.X, bounds.Y, columns.Refs.Width, bounds.Height),
            behind,
            behind,
            $"{where}: the labels' column outside the labels, {backgroundToken}",
            (x, y) => drawn.Any(box => box.Intersects(new Rect(x, y, 1, 1)))));
    }

    /// <summary>A label's entry in <see cref="DimmedRowLabels"/>; a kind without one is named so that the lookup fails.</summary>
    private static string LabelName(PlacedLabel label) => label.IsOverflow ? "the +N label" : label.Kind switch
    {
        RefLabelKind.CurrentBranch => "the current branch's label",
        RefLabelKind.LocalBranch => "a local branch's label",
        RefLabelKind.DetachedHead => "a detached HEAD's label",
        RefLabelKind.Tag => "a tag's label",
        RefLabelKind.Stash => "a stash's label",
        RefLabelKind.RemoteBranch => "a remote branch's label",
        _ => $"a label of the kind {label.Kind}",
    };

    /// <summary>Adds a failure when the pixel at <paramref name="point"/> lies further than <paramref name="tolerance"/> from <paramref name="expected"/> in a channel.</summary>
    private static void ExpectPixel(List<string> failures, Screenshot shot, Point point, Color expected, int tolerance, string what)
    {
        var pixel = shot.PixelAt(point);
        if (Math.Max(Math.Abs(pixel.R - expected.R), Math.Max(Math.Abs(pixel.G - expected.G), Math.Abs(pixel.B - expected.B))) > tolerance)
        {
            failures.Add(string.Create(CultureInfo.InvariantCulture, $"{what}: {Hex(pixel)} at {point}, not within {tolerance} of {Hex(expected)}."));
        }
    }

    /// <summary>
    /// What is wrong with the whole pixels of <paramref name="area"/>, but those
    /// <paramref name="skip"/> names: a pixel that lies outside the blends of <paramref name="ink"/>
    /// over <paramref name="behind"/> by more than 2 in a channel, as another colour drawn there
    /// does; one line for the area, with the first such pixel.
    /// </summary>
    private static IEnumerable<string> BlendProblems(Screenshot shot, Rect area, Color behind, Color ink, string what, Func<int, int, bool>? skip = null)
    {
        const int Tolerance = 2;
        static bool Between(byte value, byte a, byte b) => value >= Math.Min(a, b) - Tolerance && value <= Math.Max(a, b) + Tolerance;
        var outside = 0;
        var first = (X: 0, Y: 0, Pixel: default(Color));
        for (var y = (int)Math.Ceiling(area.Top); y < (int)Math.Floor(area.Bottom); y++)
        {
            for (var x = (int)Math.Ceiling(area.Left); x < (int)Math.Floor(area.Right); x++)
            {
                if (skip?.Invoke(x, y) == true)
                {
                    continue;
                }

                var pixel = shot.PixelAt(x, y);
                if (!(Between(pixel.R, behind.R, ink.R) && Between(pixel.G, behind.G, ink.G) && Between(pixel.B, behind.B, ink.B)))
                {
                    first = outside == 0 ? (x, y, pixel) : first;
                    outside++;
                }
            }
        }

        return outside == 0
            ? []
            : [string.Create(CultureInfo.InvariantCulture, $"{what}: {outside} pixels lie outside the blends of {Hex(ink)} over {Hex(behind)}, the first {Hex(first.Pixel)} at ({first.X}, {first.Y}): another colour is drawn there.")];
    }

    private static string Hex(Color colour) => string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");

    /// <summary>
    /// The setters of <paramref name="property"/> in <paramref name="styles"/>, their nested styles
    /// and the styles of a nested <c>Styles</c>, in their order, as <c>selector: value</c>; a control
    /// theme's own setters under its target type's name. Any other kind of style, as a
    /// <c>StyleInclude</c>, fails the fact by its type, since its setters are not read.
    /// </summary>
    private static List<string> Setters(IEnumerable<IStyle> styles, string property)
    {
        var found = new List<string>();
        foreach (var style in styles)
        {
            if (style is Styles nested)
            {
                found.AddRange(Setters(nested, property));
                continue;
            }

            var (selector, rule) = style switch
            {
                Style plain => (plain.Selector?.ToString() ?? string.Empty, (StyleBase)plain),
                ControlTheme theme => (theme.TargetType?.Name ?? string.Empty, theme),
                _ => throw new InvalidOperationException($"The view's styles hold a {style.GetType().Name}, whose setters the fact does not read: read them here."),
            };
            found.AddRange(rule.Setters.OfType<Setter>().Where(setter => setter.Property?.Name == property).Select(setter => selector + ": " + ValueOf(setter.Value)));
            found.AddRange(Setters(rule.Children, property));
        }

        return found;
    }

    /// <summary>
    /// A view's own styles, then the control themes of its resources, which <see cref="Setters"/>
    /// reads. Each key is looked up first: XAML defers a resource until its key is, and until then
    /// the dictionary's values hold the deferred content, not the control theme. Merged or theme
    /// dictionaries in the view's resources fail the fact, since their control themes are not read.
    /// </summary>
    private static List<IStyle> ViewStyles(UserControl view)
    {
        var resources = view.Resources;
        Assert.True(
            resources.MergedDictionaries.Count == 0 && resources.ThemeDictionaries.Count == 0,
            $"{view.GetType().Name}'s resources merge or hold theme dictionaries, whose control themes the fact does not read: read them here.");
        return [.. view.Styles, .. resources.Keys.ToList().Select(key => resources[key]).OfType<ControlTheme>()];
    }

    /// <summary>A setter's value as <see cref="Setters"/> writes it: a dynamic resource's key, "transparent", or the value, a number invariant.</summary>
    private static string ValueOf(object? value) => value switch
    {
        DynamicResourceExtension resource => resource.ResourceKey?.ToString() ?? "a resource without a key",
        ISolidColorBrush { Color.A: 0 } => "transparent",
        ISolidColorBrush brush => brush.Color.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "null",
    };

    /// <summary>
    /// The elements of a view's XAML (<paramref name="file"/>, or <paramref name="text"/> as its
    /// text) that take the class <paramref name="dimming"/>, by type and name. Each is held to set no
    /// foreground and no opacity of its own, by an attribute or a property element, which would
    /// outrank or add to the styles the fact reads.
    /// </summary>
    private static List<string> DimmedElements(string file, string dimming, string? text = null)
    {
        static bool TakesClass(XAttribute attribute, string dimming) =>
            attribute.Name.LocalName == "Classes." + dimming
            || (attribute.Name.LocalName == "Classes" && attribute.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(dimming));
        static bool IsOwnColour(string property) =>
            property is "Foreground" or "Opacity"
            || property.EndsWith(".Foreground", StringComparison.Ordinal) || property.EndsWith(".Opacity", StringComparison.Ordinal);

        var found = new List<string>();
        var document = XDocument.Parse(text ?? File.ReadAllText(RepositoryFile(file)));
        foreach (var element in document.Descendants().Where(element => element.Attributes().Any(attribute => TakesClass(attribute, dimming))))
        {
            var own = element.Attributes().Select(attribute => attribute.Name.LocalName)
                .Concat(element.Elements().Select(child => child.Name.LocalName))
                .Where(IsOwnColour)
                .ToList();
            Assert.True(
                own.Count == 0,
                $"{file}: {Describe(element)} sets {string.Join(" and ", own)} of its own, which outranks or adds to the styles the fact reads: give it by a style, or read it here.");
            found.Add(Describe(element));
        }

        return found;
    }

    /// <summary>
    /// The colour that <paramref name="background"/> resolves to in <paramref name="view"/>, shown in
    /// a window of each theme, is <paramref name="token"/>'s: the background the view's rows lie on at rest.
    /// </summary>
    private static void AssertBackgroundIs(Control view, Func<IBrush?> background, string token)
    {
        var window = new Window { Width = 600, Height = 400, Content = view };
        try
        {
            window.Show();
            foreach (var variant in Variants)
            {
                window.RequestedThemeVariant = variant;
                Dispatcher.UIThread.RunJobs();
                var brush = background();
                Assert.True(
                    brush is ISolidColorBrush solid && solid.Color == Token(token, variant),
                    $"{view.GetType().Name}'s rows lie on {(brush as ISolidColorBrush)?.Color.ToString() ?? brush?.ToString() ?? "nothing"} in the {variant} theme, not on {token}.");
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The guard's needles: the token by its name, which must stand in both themes; its colours in
    /// both themes as the app resolves them, in either case and with any alpha, written
    /// <c>#RRGGBB</c> or <c>#AARRGGBB</c> (XAML, or a string to parse), as a <c>0x</c> literal
    /// (<c>Color.FromUInt32</c>), or as its three channels, hexadecimal or decimal and each perhaps
    /// cast, after an alpha that nests parentheses at most two deep, to <c>Color.FromRgb</c>,
    /// <c>Color.FromArgb</c>, a constructor of <c>Color</c>, its name qualified or not, or one typed
    /// by its target (<c>new(</c>), or in CSS's <c>rgb(</c> or <c>rgba(</c> with its three channels
    /// in decimal (XAML, or a string to parse); the constant by its name; its value written out,
    /// with or without its leading 0 and with trailing zeros, but not in a geometry's path data
    /// (<see cref="IsPathData"/>); and a class of
    /// <see cref="DimmingClasses"/> by its whole name, which <see cref="ScanXaml"/> looks for where
    /// XAML applies a class and <see cref="ScanCode"/> in a string of code. A colour mixed by other
    /// means or computed, as CSS's <c>hsl(</c> is, the value computed, or a class's name put
    /// together, is beyond a reading of the text; a class held in a constant, or the colour behind
    /// a resource alias, is seen where it is defined, not where it is used.
    /// </summary>
    private static List<(DimmedNeedle Needle, Regex Pattern)> DimmedNeedles()
    {
        var needles = new List<(DimmedNeedle, Regex)> { (DimmedNeedle.Token, new Regex(@"\b" + DimmedToken + @"\b")) };
        foreach (var variant in Variants)
        {
            Assert.True(TryToken(DimmedToken, variant, out var colour), $"The {variant} theme has no {DimmedToken}: if it was renamed, give the guard and the pins the new name.");
            var hex = string.Create(CultureInfo.InvariantCulture, $"{colour.R:X2}{colour.G:X2}{colour.B:X2}");
            needles.Add((DimmedNeedle.Colour, new Regex("(?:#|\\b0x)(?:[0-9A-F]{2})?" + hex + "(?![0-9A-F])", RegexOptions.IgnoreCase)));

            // The three channels after the alpha, when one is given: any argument without a comma
            // outside its parentheses, nested at most two deep, as a cast, a product or a call.
            needles.Add((DimmedNeedle.Colour, new Regex(
                @"(?:\bColor\.From(?:Rgb|Argb)|\bnew\s+(?:[\w.:]+\.)?Color|\bnew)\s*\(\s*(?:(?:[^,()]|\((?:[^()]|\([^()]*\))*\))+,\s*)?"
                    + ChannelPattern(colour.R) + @"\s*,\s*" + ChannelPattern(colour.G) + @"\s*,\s*" + ChannelPattern(colour.B) + @"\s*\)",
                RegexOptions.IgnoreCase)));

            // CSS's form, which Avalonia parses too: rgb( or rgba( and the three channels in decimal.
            needles.Add((DimmedNeedle.Colour, new Regex(
                string.Create(CultureInfo.InvariantCulture, $@"\brgba?\(\s*0*{colour.R}\s*,\s*0*{colour.G}\s*,\s*0*{colour.B}\s*[,)]"),
                RegexOptions.IgnoreCase)));
        }

        needles.Add((DimmedNeedle.Dimming, new Regex(@"\b" + nameof(CommitGraphControl.SearchDimmedOpacity) + @"\b")));
        var value = CommitGraphControl.SearchDimmedOpacity.ToString(CultureInfo.InvariantCulture);
        Assert.StartsWith("0.", value, StringComparison.Ordinal);
        needles.Add((DimmedNeedle.DimmingValue, new Regex(@"(?<![\w.])0*" + Regex.Escape(value[1..]) + @"0*(?!\d)")));
        needles.Add((DimmedNeedle.Class, new Regex("^(?:" + string.Join('|', DimmingClasses.Select(dimming => Regex.Escape(dimming.Class))) + ")$")));
        return needles;
    }

    /// <summary>One channel of a colour as code writes a byte: hexadecimal or decimal, perhaps cast.</summary>
    private static string ChannelPattern(byte value) =>
        string.Create(CultureInfo.InvariantCulture, $@"(?:\(byte\)\s*)?(?:0x0*{value:X2}|0*{value})(?![0-9A-F])");

    /// <summary>A XAML file, which the guard reads by its elements: Avalonia builds <c>.axaml</c> and <c>.xaml</c> alike.</summary>
    private static bool IsXaml(string file) =>
        file.EndsWith(".axaml", StringComparison.Ordinal) || file.EndsWith(".xaml", StringComparison.Ordinal);

    /// <summary>Every use of the needles in the files the app is built from: each XAML and C# file under src/VisualCommit.App, bin and obj left out.</summary>
    private static List<DimmedUse> ScanAppSource(IReadOnlyList<(DimmedNeedle Needle, Regex Pattern)> needles)
    {
        var root = RepositoryFile(AppSource);
        Assert.True(Directory.Exists(root), $"The app's source is not at {root}.");
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => IsXaml(path) || path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Select(path => AppSource + "/" + Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Contains(files, file => file.EndsWith(".axaml", StringComparison.Ordinal));
        Assert.Contains(files, file => file.EndsWith(".cs", StringComparison.Ordinal));
        return [.. files.SelectMany(file => ScanText(file, File.ReadAllText(RepositoryFile(file)), needles))];
    }

    /// <summary>The uses of the needles in one file's text, <paramref name="file"/> naming it: a XAML file by its elements and attributes, a C# file by its lines.</summary>
    private static List<DimmedUse> ScanText(string file, string text, IReadOnlyList<(DimmedNeedle Needle, Regex Pattern)> needles) =>
        IsXaml(file) ? ScanXaml(file, text, needles) : ScanCode(file, text, needles);

    /// <summary>
    /// The uses in a XAML file: each match in an attribute's value or an element's text, comments
    /// left out, at its place (the enclosing control themes, styles and theme dictionaries, then the
    /// setter's property, or the element and its attribute), the opacity's value not looked for in a
    /// geometry's path data (<see cref="IsPathData"/>); and each dimming class the element takes, by
    /// <c>Classes.dropped="…"</c> or in the list of <c>Classes="…"</c>.
    /// </summary>
    private static List<DimmedUse> ScanXaml(string file, string text, IReadOnlyList<(DimmedNeedle Needle, Regex Pattern)> needles)
    {
        var uses = new List<DimmedUse>();
        var document = XDocument.Parse(text, LoadOptions.SetLineInfo);
        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            {
                var name = AttributeName(element, attribute);
                var line = ((IXmlLineInfo)attribute).LineNumber;
                var place = PlaceOf(element, element.Name.LocalName == "Setter" && name == "Value" ? string.Empty : " @" + name);
                AddMatches(uses, file, line, attribute.Value, place, needles, pathData: IsPathData(name));

                // A class applied is named with what applies it, so that a changed binding is a new use.
                string[] classes = name.StartsWith("Classes.", StringComparison.Ordinal) ? [name["Classes.".Length..]]
                    : name == "Classes" ? attribute.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    : [];
                AddClasses(uses, file, line, classes, place + "=" + attribute.Value, needles);
            }

            foreach (var node in element.Nodes().OfType<XText>())
            {
                AddMatches(uses, file, ((IXmlLineInfo)node).LineNumber, node.Value, PlaceOf(element, " text"), needles, pathData: IsPathData(element.Name.LocalName));
            }
        }

        return uses;
    }

    /// <summary>
    /// A geometry's path data, whose numbers are no opacity, by the name of the attribute or the
    /// element that holds it: <c>Data</c> or <c>Geometry</c>, a property element as <c>Path.Data</c>,
    /// or a <c>StreamGeometry</c>.
    /// </summary>
    private static bool IsPathData(string name) =>
        name is "Data" or "Geometry" or "StreamGeometry" || name.EndsWith(".Data", StringComparison.Ordinal);

    /// <summary>
    /// The uses in a C# file: each match in a line's code, comments left out, its place the line's
    /// code; and each dimming class a string of the code names whole, as <c>Classes.Set("dropped", …)</c> does.
    /// The strings are taken from the code by a pattern that does not know character literals: after
    /// <c>'"'</c> on a line, a class a later string names is missed.
    /// </summary>
    private static List<DimmedUse> ScanCode(string file, string text, IReadOnlyList<(DimmedNeedle Needle, Regex Pattern)> needles)
    {
        var uses = new List<DimmedUse>();
        var lines = text.Split('\n');
        var inComment = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var code = CodeOf(lines[i].TrimEnd('\r'), ref inComment).Trim();
            AddMatches(uses, file, i + 1, code, code, needles);
            AddClasses(uses, file, i + 1, [.. Regex.Matches(code, @"""((?:[^""\\]|\\.)*)""").Select(literal => literal.Groups[1].Value)], code, needles);
        }

        return uses;
    }

    /// <summary>
    /// The matches of every needle but the dimming classes' in <paramref name="text"/>; in a
    /// geometry's path data (<paramref name="pathData"/>) the opacity's value is not looked for.
    /// </summary>
    private static void AddMatches(List<DimmedUse> uses, string file, int line, string text, string place, IReadOnlyList<(DimmedNeedle Needle, Regex Pattern)> needles, bool pathData = false)
    {
        var looked = needles.Where(needle => needle.Needle != DimmedNeedle.Class && !(pathData && needle.Needle == DimmedNeedle.DimmingValue));
        foreach (var (needle, pattern) in looked)
        {
            uses.AddRange(pattern.Matches(text).Select(_ => new DimmedUse(file, line, needle, place)));
        }
    }

    /// <summary>A use for each of <paramref name="names"/> that is a dimming class's whole name.</summary>
    private static void AddClasses(List<DimmedUse> uses, string file, int line, IEnumerable<string> names, string place, IReadOnlyList<(DimmedNeedle Needle, Regex Pattern)> needles)
    {
        foreach (var (needle, pattern) in needles.Where(needle => needle.Needle == DimmedNeedle.Class))
        {
            uses.AddRange(names.Where(name => pattern.IsMatch(name)).Select(_ => new DimmedUse(file, line, needle, place)));
        }
    }

    /// <summary>
    /// A line of C# without its comments: <c>//</c> to the line's end, and <c>/*</c> to <c>*/</c> also across
    /// lines (<paramref name="inComment"/>), outside string and character literals. A plain reading,
    /// not a compiler's: a verbatim string that ends in a backslash, or a raw string with an odd
    /// number of quotes, can keep a comment as code; and a raw or verbatim string over several lines
    /// is read as code, where a <c>/*</c> in it drops the lines after it until a <c>*/</c>.
    /// </summary>
    private static string CodeOf(string line, ref bool inComment)
    {
        var code = new StringBuilder();
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            var next = i + 1 < line.Length ? line[i + 1] : '\0';
            if (inComment)
            {
                if (c == '*' && next == '/')
                {
                    inComment = false;
                    i++;
                }

                continue;
            }

            if (quote != '\0')
            {
                code.Append(c);
                if (c == '\\' && next != '\0')
                {
                    code.Append(next);
                    i++;
                }
                else if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c == '/' && next == '/')
            {
                break;
            }

            if (c == '/' && next == '*')
            {
                inComment = true;
                i++;
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }

            code.Append(c);
        }

        return code.ToString();
    }

    /// <summary>An element's place: its enclosing control themes, styles and keyed theme dictionaries, outermost first, then the element itself and <paramref name="suffix"/>.</summary>
    private static string PlaceOf(XElement element, string suffix)
    {
        var scopes = element.Ancestors().Reverse().Select(Scope).OfType<string>();
        return string.Join(" > ", scopes.Append(Describe(element) + suffix));
    }

    /// <summary>A scope of a place: a control theme by its key, a style by its selector, a theme dictionary by its key; null for any other element.</summary>
    private static string? Scope(XElement element) => element.Name.LocalName switch
    {
        "ControlTheme" => "ControlTheme " + KeyOf(element),
        "Style" => "Style " + (string?)element.Attribute("Selector"),
        "ResourceDictionary" when KeyOf(element) is { } key => "ResourceDictionary " + key,
        _ => null,
    };

    /// <summary>The element itself: a setter by its property, any other element by its type and its key or name.</summary>
    private static string Describe(XElement element)
    {
        if (element.Name.LocalName == "Setter")
        {
            return "Setter " + (string?)element.Attribute("Property");
        }

        var id = KeyOf(element) ?? (string?)element.Attribute("Name") ?? (string?)element.Attribute(XamlNamespace + "Name");
        return id is null ? element.Name.LocalName : element.Name.LocalName + " " + id;
    }

    private static string? KeyOf(XElement element) => (string?)element.Attribute(XamlNamespace + "Key");

    /// <summary>An attribute's name as the file writes it: <c>x:Key</c>, <c>Foreground</c>.</summary>
    private static string AttributeName(XElement element, XAttribute attribute)
    {
        var space = attribute.Name.Namespace;
        if (space == XNamespace.None)
        {
            return attribute.Name.LocalName;
        }

        return (element.GetPrefixOfNamespace(space) ?? space.NamespaceName) + ":" + attribute.Name.LocalName;
    }

    /// <summary>
    /// What is wrong with <paramref name="uses"/> against <paramref name="accepted"/>: a needle the
    /// source names nowhere (the token or the constant renamed), a use at a place the table does not
    /// list, a place used more or less often than the table says, and a listed place no longer used.
    /// Each says what to do.
    /// </summary>
    private static List<string> GuardProblems(IReadOnlyList<DimmedUse> uses, IReadOnlyList<AcceptedUse> accepted)
    {
        var problems = new List<string>();
        foreach (var needle in new[] { DimmedNeedle.Token, DimmedNeedle.Dimming })
        {
            if (!uses.Any(use => use.Needle == needle))
            {
                problems.Add($"The app's source names {NeedleName(needle)} nowhere: if it was renamed, give the guard and the pins the new name, so that the guard does not look for nothing.");
            }
        }

        foreach (var group in uses.GroupBy(use => (use.File, use.Needle, use.Place)))
        {
            var lines = string.Join(", ", group.Select(use => use.Line));
            var entry = accepted.FirstOrDefault(entry => (entry.File, entry.Needle, entry.Place) == group.Key);
            if (entry is null)
            {
                problems.Add(
                    $"{group.Key.File}:{lines}: {NeedleName(group.Key.Needle)} in \"{group.Key.Place}\", a use the guard does not list. "
                    + "If it dims a text on a row the user acts on, draw the text in another colour (VcTextSecondaryBrush reaches 4.5:1 on every row) or leave the class off; "
                    + "if it lies under a disabled state, is no text, applies no dimming here, or belongs to one of D469's exceptions, add it to DimmedTextUses with its reason.");
            }
            else if (group.Count() != entry.Count)
            {
                problems.Add($"{group.Key.File}:{lines}: {NeedleName(group.Key.Needle)} in \"{group.Key.Place}\" {group.Count()} times, where DimmedTextUses lists it {entry.Count} times ({entry.Kind}: {entry.Reason}): give each use its reason in the table, or another colour.");
            }
        }

        foreach (var entry in accepted.Where(entry => !uses.Any(use => (use.File, use.Needle, use.Place) == (entry.File, entry.Needle, entry.Place))))
        {
            problems.Add($"{entry.File}: no longer {NeedleName(entry.Needle)} in \"{entry.Place}\" ({entry.Kind}: {entry.Reason}): take the entry out of DimmedTextUses; if the change fixes one of D469's exceptions, its pinned figures fail too and the decision is looked at again.");
        }

        return problems;
    }

    private static string NeedleName(DimmedNeedle needle) => needle switch
    {
        DimmedNeedle.Token => "the disabled text colour's token",
        DimmedNeedle.Colour => "the disabled text colour written out",
        DimmedNeedle.Dimming => "the search's dimming",
        DimmedNeedle.DimmingValue => "the search's dimming's value written out",
        _ => "a class whose style gives a text the disabled colour",
    };

    /// <summary>The lines of a file of the repository, without their line ends.</summary>
    private static string[] SourceLines(string relative) =>
        [.. File.ReadAllText(RepositoryFile(relative)).Split('\n').Select(line => line.TrimEnd('\r'))];

    /// <summary>The path of a file of the repository, from the root the build names, as AppLinksTests finds the guide's source.</summary>
    private static string RepositoryFile(string relative) =>
        Path.GetFullPath(Path.Combine(VisualRun.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
}
