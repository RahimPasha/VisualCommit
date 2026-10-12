// Part D of phase 4's scripted walk-through (docs/test-reports/phase-4.md, "Part D: the light theme
// at 1920×1080"): check D18, the interactive list with a dropped and a squashed row, the message
// dialog and the resolver in the light theme, with every tint of design 7.3 sampled from the
// pictures. Scripted only: 1920×1080 does not fit the development screen (design 13.4). It
// follows the conventions of checks C14 to C17 and uses their helpers (Phase4ChecksC.C14To17, in
// Phase4Checks.C.EditingSupport.cs); its expected values are in Phase4Expectations.D.cs (PartD).
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.App.Views.Rebase;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using Steps = VisualCommit.VisualTests.Phase4.Phase4ChecksC.C14To17;
using Tint = VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC2.Tint;

namespace VisualCommit.VisualTests.Phase4;

/// <summary>The checks of part D: the light theme at 1920×1080.</summary>
public sealed partial class Phase4ChecksD : Phase4Checks
{
    /// <summary>
    /// Check D18, part 1: the interactive list from "Base" with Five dropped and Four squashed,
    /// then the message dialog of the squashed commit; light, 1920×1080.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_D18_interactive_list_light()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, FullSize.Width, FullSize.Height, AppTheme.Light);
        var palette = Light;
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        Assert.Equal(new Size(FullSize.Width, FullSize.Height), app.Window.ClientSize);
        foreach (var (token, colour) in PartD.LightHexes)
        {
            Assert.Equal(colour, LightToken(palette, token));
        }

        // Step 1: G-I, every column shown.
        AssertGraph(app, PartD.GraphGI, lanes: 1, showsDate: true);
        Assert.True(Graph(app).Columns.ShowsAuthor && Graph(app).Columns.ShowsSha, "Not every graph column shows at 1920×1080.");

        // Step 2: the commit menu of "Base", "Interactive rebase of main from here…": the list in place of the graph.
        app.RightClick(app.GraphRowPoint(PartD.BaseRow));
        app.ClickMenuItem(PartD.InteractiveItem);
        await app.WaitForRebaseListAsync();
        await app.WaitForIdleAsync();
        var list = app.RebaseListSnapshot();
        Assert.Equal(new Rect(PartD.ListLeft, WindowLayout.GraphAreaTop, PartD.ListWidth, FullSize.Height - WindowLayout.GraphAreaTop - WindowLayout.StatusBarHeight), list.Bounds);
        Assert.Equal((PartD.ListHeader, PartD.ListCount), (list.Header, list.Count));
        Assert.True(app.FindByAutomationId("RefList").IsEffectivelyVisible, "The left panel is hidden.");
        Assert.True(app.FindByAutomationId("RightPanel").IsEffectivelyVisible, "The right panel is hidden.");
        var selected = list.Selected ?? throw new Xunit.Sdk.XunitException("No row of the list is selected.");
        await WaitForDetailsAsync(app, selected.Subject);

        // Step 3: d on Five, Down, s on Four.
        app.PressKey(Key.D);
        app.PressKey(Key.Down);
        app.PressKey(Key.S);
        await WaitForDetailsAsync(app, "Four");
        await app.WaitForIdleAsync();
        var shot = Shot(app, "d18a-list-light");
        list = app.RebaseListSnapshot();
        D18List.AssertRows(app, list, palette);
        D18List.AssertColours(list, shot, palette);
        await D18List.AssertFourDetailsAsync(app);
        Assert.Equal(PartD.MainTip, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Empty(await repo.BackupRefsAsync());
        AssertNoClippedText(app);

        // Step 4: Edit… on Three's second line: the message dialog; Cancel.
        var backdropAt = new Point(700, 900);
        var under = shot.PixelAt(backdropAt);
        app.ClickRebaseEditMessage("Three");
        await app.WaitForAsync(() => app.TopDialogView is MessageDialogView, "the message dialog");
        var dialog = app.MessageDialog();
        shot = Shot(app, "d18b-message-dialog-light");
        Assert.Equal(PartD.WideCard, dialog.CardBounds.Width, 0.5);
        Assert.Equal((PartD.MessageTitle, PartD.MessageSummary, PartD.MessageCounter, PartD.MessageDescription), (dialog.Title, dialog.Summary, dialog.Counter, dialog.Description));
        Assert.True(dialog.SummaryHasFocus, "The summary does not have the keyboard focus.");
        Assert.Equal(PartD.MessageSummary.Length, dialog.SummaryCaret);
        Assert.Equal((PartD.Cancel, PartD.Save, true), (dialog.CancelText, dialog.ConfirmText, dialog.ConfirmEnabled));
        var save = (Button)app.FindByAutomationId(app.TopDialogView!, "ConfirmButton");
        AssertColour(palette.Accent, shot, new Point(app.BoundsOf(save).X + 4, app.BoundsOf(save).Center.Y), "Save's background");
        Assert.Equal(PartD.PrimaryLabel, (save.Foreground as ISolidColorBrush)?.Color);
        D18List.AssertBackdrop(shot, backdropAt, under);
        AssertNoClippedText(app);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "CancelButton"));
        await AssertNoDialogAsync(app);
        list = app.RebaseListSnapshot();
        D18List.AssertRows(app, list, palette);
        Assert.Equal(PartD.MainTip, await repo.RefTargetAsync("refs/heads/main"));
    }

    /// <summary>
    /// Check D18, part 2: the resolver on a diff3 merge stop, light, 1920×1080, every tint of
    /// design 7.3 sampled; then <c>Use ours</c> in block 2.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_D18_resolver_light()
    {
        using var repo = await Scenarios.ConflictsAsync();
        await repo.ConfigAsync("merge.conflictStyle", "diff3");
        var merge = await repo.GitAllowingFailureAsync("merge", "feature/theirs");
        Assert.Equal(1, merge.ExitCode);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, FullSize.Width, FullSize.Height, AppTheme.Light);
        var palette = Light;
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        var unmergedAtStop = await Steps.UnmergedAsync(repo);
        var start = app.LogMark();

        // Step 5: git's stop, N3; GC-M with every column shown.
        Assert.Equal(PartC2.MergeStopStatus, await Steps.PorcelainAsync(repo));
        Assert.Equal(Steps.Bytes(PartD.N3), Steps.WorkingFile(repo, "notes.txt"));
        AssertGraph(app, PartC2.GcM, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.WorkingConflicts(1)), showsDate: true);
        Assert.True(Graph(app).Columns.ShowsAuthor && Graph(app).Columns.ShowsSha, "Not every graph column shows at 1920×1080.");

        // Step 6: the resolver on notes.txt.
        await Steps.OpenResolverAsync(app, "notes.txt", PartC2.UnstagedTitle(1, 1));
        var shot = Shot(app, "d18c-resolver-light");
        Steps.AssertResolverPlace(app);
        Assert.Equal(PartD.ResolverWidth, app.BoundsOf(app.ResolverView).Width, 0.5);
        var banner = AssertBanner(app, PartC2.MergeTheirsBanner, PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(1)), oneLine: true, placement: BannerButtonPlacement.BesideText);
        Assert.Equal(PartD.ResolverWidth, banner.Width, 0.5);
        var bannerBorder = Steps.BannerBorder(app);
        Assert.Equal(palette.BannerBackground, (bannerBorder.Background as ISolidColorBrush)?.Color);
        Assert.Equal(palette.Warning, (bannerBorder.BorderBrush as ISolidColorBrush)?.Color);
        AssertColour(palette.BannerBackground, shot, new Point(banner.Right - 220, banner.Top + 3), "banner's background");
        AssertColour(palette.Warning, shot, new Point(banner.Right - 220, banner.Bottom - 0.5), "banner's bottom line");
        Assert.Equal(palette.Warning, (app.Find<Icon>("BannerIcon").Foreground as ISolidColorBrush)?.Color);

        var resolver = app.Resolver();
        Steps.AssertHeader(app, resolver, "notes.txt", PartC2.BothModified, PartC2.FilePosition(1, 1));
        Steps.AssertTextToolbar(app, resolver, PartC2.ConflictPosition(1, 2), mergeTool: false);
        var markResolved = (Button)app.FindByAutomationId(app.ResolverView, "ResolverMarkResolved");
        AssertColour(palette.Accent, shot, new Point(app.BoundsOf(markResolved).X + 4, app.BoundsOf(markResolved).Center.Y), "Mark resolved's background");
        Assert.Equal(PartD.PrimaryLabel, (markResolved.Foreground as ISolidColorBrush)?.Color);
        Steps.AssertTitles(resolver, PartC2.OursMain, PartC2.TheirsTheirs, 2);
        Steps.AssertPanes(resolver, PartC2.PmOurs(), PartC2.PmTheirs());
        D18Resolver.AssertEveryRowInView(app, resolver.OursRows.Count, 24);
        Steps.AssertResult(resolver, PartD.N3, PartD.N3Tints);
        Steps.AssertFocusAndCaret(app, 2, 1);
        D18Resolver.AssertPaneColours(app, shot, palette, PartC2.PmOurs(), PartC2.PmTheirs());
        D18Resolver.AssertResultColours(app, shot, palette, PartD.N3Tints);

        // Step 7: Use ours in block 2's ours bar.
        app.ClickBarButton(1, ConflictPaneSide.Ours, ConflictChoice.UseOurs);
        shot = Shot(app, "d18d-used-light");
        resolver = app.Resolver();
        Steps.AssertResult(resolver, PartD.N3OursOnBlock2, PartD.N3OursOnBlock2Tints);
        Assert.Equal((PartC2.ConflictsLeft(1), PartC2.ConflictPosition(1, 2)), (resolver.ResultCount, resolver.ConflictPosition));
        Steps.AssertBars(app, 0, null, null);
        Steps.AssertBars(app, 1, PartC2.Used, PartC2.NotUsed);
        Steps.AssertPanes(resolver, PartC2.PmOurs(Tint.Ours, Tint.Used), PartC2.PmTheirs(Tint.Theirs, Tint.None));
        Steps.AssertFocusAndCaret(app, 16, 1);
        D18Resolver.AssertPaneColours(app, shot, palette, PartC2.PmOurs(Tint.Ours, Tint.Used), PartC2.PmTheirs(Tint.Theirs, Tint.None));
        D18Resolver.AssertResultColours(app, shot, palette, PartD.N3OursOnBlock2Tints);
        var used = D18Resolver.BarStateText(app, ConflictPaneSide.Ours, PartC2.Used);
        Assert.Equal(palette.TextSecondary, (used.Foreground as ISolidColorBrush)?.Color);
        await Steps.AssertNothingWrittenAsync(app, repo, "notes.txt", PartD.N3, PartC2.MergeStopStatus, unmergedAtStop, start);
        AssertNoClippedText(app);
    }

    /// <summary>A light token's colour in the palette, by the token's name as the report gives it.</summary>
    private static Color LightToken(Palette palette, string token) => token switch
    {
        "VcConflictOursBrush" => palette.ConflictOurs,
        "VcConflictTheirsBrush" => palette.ConflictTheirs,
        "VcConflictBaseBrush" => palette.ConflictBase,
        "VcConflictMarkerBrush" => palette.ConflictMarker,
        "VcConflictUsedBrush" => palette.ConflictUsed,
        "VcWarningBrush" => palette.Warning,
        "VcBannerBackgroundBrush" => palette.BannerBackground,
        "VcDangerBrush" => palette.Danger,
        "VcAccentBrush" => palette.Accent,
        "VcLane0Brush" => palette.Lane(0),
        "VcSelectionBrush" => palette.Selection,
        "VcTextSecondaryBrush" => palette.TextSecondary,
        "VcTextDisabledBrush" => palette.TextDisabled,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "No such token in the palette."),
    };

    /// <summary>Waits until the details panel shows the commit with this subject, the one the graph has selected.</summary>
    private static Task WaitForDetailsAsync(ShellDriver app, string subject) =>
        app.WaitForAsync(
            () => Shown(app, "DetailsSubject") is SelectableTextBlock text && text.Text == subject
                && Shown(app, "DetailsSha") is SelectableTextBlock sha && Graph(app).Selection.Shas is [var selected] && sha.Text == selected,
            $"the details of \"{subject}\", the selected commit");

    /// <summary>The visible control with this automation id, or null.</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>What part 1 of D18 asserts of the list.</summary>
    private static class D18List
    {
        /// <summary>The rows after step 3 (design 2.2): 30 high, the grip, the 84 × 22 action button, the code-font SHA, the subject, the author.</summary>
        public static void AssertRows(ShellDriver app, InteractiveRebaseSnapshot list, Palette palette)
        {
            Assert.Equal((PartD.ListHeader, PartD.ListCount), (list.Header, list.Count));
            Assert.True(list.ShowsAuthor, "The author column is hidden in a list wider than 520.");
            Assert.Equal(PartD.Rows.Select(row => (row.Sha, row.Subject, row.Action, row.StruckThrough, row.Indent)), list.Rows.Select(row => (row.ShortSha, row.Subject, row.ActionText, row.IsStruckThrough, row.Indent)));
            foreach (var row in list.Rows)
            {
                Assert.Equal(PartD.RowHeight, row.Bounds!.Value.Height, 0.5);
                Assert.Equal(new Size(PartD.ActionButtonWidth, PartD.ActionButtonHeight), row.ActionButtonBounds!.Value.Size);
                Assert.Equal(PartD.AuthorWidth, row.AuthorBounds!.Value.Width, 0.5);
                Assert.Equal(palette.TextSecondary, row.GripColor);
                Assert.NotNull(row.GripBounds);
            }

            var authors = app.RebaseListView.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && text.Text == PartD.Author).ToList();
            Assert.Equal(list.Rows.Count, authors.Count);
            var shas = app.RebaseListView.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && list.Rows.Any(row => row.ShortSha == text.Text)).ToList();
            Assert.All(shas, sha => Assert.Contains("Mono", sha.FontFamily.Name, StringComparison.OrdinalIgnoreCase));

            var five = list.Row("Five");
            Assert.Equal((palette.TextDisabled, palette.TextDisabled), (five.ShaColor, five.SubjectColor));
            var four = list.Row("Four");
            Assert.True(four.IsSelected && four.HasKeyboardFocus, "Four is not the selected row with the keyboard focus.");
            Assert.Equal(list.Row("Three").GripBounds!.Value.X + PartD.SquashIndent, four.GripBounds!.Value.X, 0.5);
            Assert.Equal(list.Row("Three").ActionButtonBounds!.Value.X + PartD.SquashIndent, four.ActionButtonBounds!.Value.X, 0.5);
            Assert.Equal(list.Row("Three").ShaBounds!.Value.X + PartD.SquashIndent, four.ShaBounds!.Value.X, 0.5);
            Assert.Equal(list.Row("Three").SubjectBounds!.Value.X + PartD.SquashIndent, four.SubjectBounds!.Value.X, 0.5);
            Assert.Equal(list.Row("Three").Sha, four.JoinTargetSha);
            var three = list.Row("Three");
            Assert.Equal(PartD.ThreeSecondLine, three.SecondLine);
            Assert.Equal(PartD.SecondLineHeight, three.SecondLineBounds!.Value.Height, 0.5);
            var edit = three.EditButtonBounds ?? throw new Xunit.Sdk.XunitException("Three's second line has no Edit….");
            Assert.True(edit.Right <= three.SecondLineBounds!.Value.Right + 0.5 && edit.Left > three.SecondLineBounds!.Value.Center.X, "Edit… is not at the right of Three's second line.");
            Assert.Equal(PartD.EditButton, ((Button)app.FindByAutomationId(app.RebaseListView, "RebaseEditMessageButton")).Content);
            Assert.All(list.Rows.Where(row => row.Subject != "Three"), row => Assert.Null(row.SecondLine));

            var baseRow = list.BaseRow ?? throw new Xunit.Sdk.XunitException("The list has no base row.");
            Assert.Equal((PartD.BaseSha, PartD.BaseSubject), (baseRow.ShortSha, baseRow.Subject));
            Assert.Equal(palette.TextSecondary, baseRow.LabelColor);
            var baseTexts = TextsIn(app.FindByAutomationId(app.RebaseListView, "RebaseBaseRow"));
            Assert.Equal([PartD.BaseLabel, PartD.BaseSha, PartD.BaseSubject], baseTexts);
            Assert.True(list.StartShown && list.CanStart, "Start rebase is not shown and enabled.");
            var start = (Button)app.FindByAutomationId(app.RebaseListView, "RebaseStartButton");
            Assert.Equal(PartD.StartRebase, start.Content);
            Assert.Equal(PartD.PrimaryLabel, (start.Foreground as ISolidColorBrush)?.Color);
            Assert.Equal(PartD.Cancel, ((Button)app.FindByAutomationId(app.RebaseListView, "RebaseCancelButton")).Content);
        }

        /// <summary>Step 3's sampled colours: Four's selected background, the join line, Start rebase's background; and its label's colour from the control.</summary>
        public static void AssertColours(InteractiveRebaseSnapshot list, Screenshot shot, Palette palette)
        {
            var four = list.Row("Four").Bounds!.Value;
            Assert.Equal(PartD.FourBackgroundPoint.Y, four.Center.Y, 0.5);
            AssertColour(palette.Selection, shot, new Point(PartD.FourBackgroundPoint.X, PartD.FourBackgroundPoint.Y), "Four's selected background");
            var threeTop = list.Row("Three").Bounds!.Value.Top;
            Assert.Equal(PartD.JoinLineX, list.Bounds.X + 12, 0.5);
            AssertColour(palette.Lane(0), shot, new Point(PartD.JoinLineX, threeTop - PartD.JoinLineAboveThree), "the join line");
            var join = Assert.Single(list.JoinLines);
            Assert.Equal(2, join.Bounds.Width, 0.5);
            Assert.Equal(four.Center.Y, join.Bounds.Top, 0.5);
            Assert.Equal(threeTop, join.Bounds.Bottom, 0.5);
            Assert.Equal(palette.Lane(0), join.Color);
            AssertColour(palette.Accent, shot, new Point(list.StartBounds.X + 3, list.StartBounds.Center.Y), "Start rebase's background");
        }

        /// <summary>The right panel shows Four's details (step 3).</summary>
        public static async Task AssertFourDetailsAsync(ShellDriver app)
        {
            Assert.StartsWith(PartD.FourSha, ((SelectableTextBlock)app.FindByAutomationId("DetailsSha")).Text, StringComparison.Ordinal);
            Assert.Equal("Four", ((SelectableTextBlock)app.FindByAutomationId("DetailsSubject")).Text);
            Assert.StartsWith(PartD.Author, ((SelectableTextBlock)app.FindByAutomationId("DetailsAuthor")).Text, StringComparison.Ordinal);
            Assert.Equal(PartD.FourDate, ((TextBlock)app.FindByAutomationId("DetailsDate")).Text);
            Assert.Equal("Changed files (1)", ((TextBlock)app.FindByAutomationId("ChangedFilesTitle")).Text);
            await app.WaitForAsync(() => DetailsFiles(app).SequenceEqual(PartD.FourFiles), "Four's changed file A four.txt src");
        }

        /// <summary>The backdrop (<c>#66000000</c>) over what was at <paramref name="at"/> before the dialog opened (±2 a channel).</summary>
        public static void AssertBackdrop(Screenshot shot, Point at, Color under)
        {
            var alpha = PartD.Backdrop.A / 255.0;
            int Mix(byte below, byte above) => (int)Math.Round((above * alpha) + (below * (1 - alpha)));
            var actual = shot.PixelAt(at);
            Assert.True(
                Math.Abs(actual.R - Mix(under.R, PartD.Backdrop.R)) <= 2 && Math.Abs(actual.G - Mix(under.G, PartD.Backdrop.G)) <= 2 && Math.Abs(actual.B - Mix(under.B, PartD.Backdrop.B)) <= 2,
                $"At {at} the window under the dialog is {actual}, not {under} under the backdrop {PartD.Backdrop}.");
        }

        private static List<string> DetailsFiles(ShellDriver app)
        {
            var list = (ItemsControl)app.FindByAutomationId("FileList");
            return [.. ((IEnumerable<ChangedFileRow>)(list.ItemsSource ?? Array.Empty<ChangedFileRow>())).Where(row => row.IsFile)
                .Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => part.Length > 0)))];
        }
    }

    /// <summary>What part 2 of D18 asserts of the resolver.</summary>
    private static class D18Resolver
    {
        /// <summary>Every row of both panes and every line of the result is in view (step 6).</summary>
        public static void AssertEveryRowInView(ShellDriver app, int paneRows, int resultLines)
        {
            var panes = app.ResolverPanes;
            foreach (var side in new[] { ConflictPaneSide.Ours, ConflictPaneSide.Theirs })
            {
                for (var row = 0; row < paneRows; row++)
                {
                    var bounds = panes.RowBounds(side, row) ?? throw new Xunit.Sdk.XunitException($"Row {row} of the {side} pane is not in view.");
                    Assert.True(bounds.Bottom <= panes.Bounds.Height + 0.5, $"Row {row} of the {side} pane ({bounds}) runs past the panes' bottom ({panes.Bounds.Height}).");
                }
            }

            for (var line = 1; line <= resultLines; line++)
            {
                Assert.True(app.ResultEditor.PointAt(line, 1) is not null, $"Line {line} of the result is not in view.");
            }
        }

        /// <summary>
        /// The panes' text rows sampled at their vertical centre and the pane's horizontal middle:
        /// a tinted row its token's colour, any other the pane's own background at <c>line 1</c>.
        /// </summary>
        public static void AssertPaneColours(ShellDriver app, Screenshot shot, Palette palette, IReadOnlyList<PartC2.PaneRow> ours, IReadOnlyList<PartC2.PaneRow> theirs)
        {
            foreach (var (side, rows) in new[] { (ConflictPaneSide.Ours, ours), (ConflictPaneSide.Theirs, theirs) })
            {
                var plain = shot.PixelAt(Middle(app, side, 0));
                for (var row = 0; row < rows.Count; row++)
                {
                    if (rows[row].IsBar)
                    {
                        continue;
                    }

                    var expected = rows[row].Tint == Tint.None ? plain : Steps.ColourOf(palette, rows[row].Tint);
                    AssertColour(expected, shot, Middle(app, side, row), string.Create(CultureInfo.InvariantCulture, $"{side} pane's line {rows[row].Number} ({rows[row].Tint})"));
                }
            }
        }

        /// <summary>The result's lines sampled at their vertical centre and the result's horizontal middle (none of N3's lines reaches it).</summary>
        public static void AssertResultColours(ShellDriver app, Screenshot shot, Palette palette, IReadOnlyList<Tint> tints) =>
            Steps.AssertResultColours(app, shot, tints, app.ResultEditor.Bounds.Width / 2);

        /// <summary>The bar state's text (<c>ConflictBarState</c>) in <paramref name="side"/>'s pane that says <paramref name="text"/>.</summary>
        public static TextBlock BarStateText(ShellDriver app, ConflictPaneSide side, string text) =>
            app.ResolverPanes.PaneEditor(side).GetVisualDescendants().OfType<TextBlock>()
                .Single(block => block.IsEffectivelyVisible && AutomationProperties.GetAutomationId(block) == ConflictPanes.BarStateAutomationId && block.Text == text);

        private static Point Middle(ShellDriver app, ConflictPaneSide side, int row)
        {
            var panes = app.ResolverPanes;
            var bounds = panes.RowBounds(side, row) ?? throw new Xunit.Sdk.XunitException($"Row {row} of the {side} pane is not in view.");
            return panes.TranslatePoint(new Point(bounds.X + (bounds.Width / 2), bounds.Center.Y), app.Window)!.Value;
        }
    }
}
