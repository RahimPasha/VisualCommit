// What part B's checks share (docs/test-reports/phase-4.md, part B's conventions): opening the
// list as B6 steps 1 and 2 do, the list as "The list's geometry" draws it (L0, L7 and the states
// between), a row's menu, the message dialog, the details panel, the graph's selection and focus,
// and "git unchanged". Only part B's writer edits this file; what every part shares is in
// Phase4Checks.Support.cs.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Rebase;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartB;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksB
{
    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    /// <summary>
    /// B6 steps 1 and 2 without their pictures, for the checks that "open the list as B6 steps 1
    /// and 2": Base's row right-clicked in its Message column, "Interactive rebase of main from
    /// here…" clicked, the queue idle and the list in view. Asserts the menu's lines on the way
    /// and L0 with Five selected and focused at the end.
    /// </summary>
    private static async Task OpenListAsync(ShellDriver app)
    {
        var point = GraphRowPoint(6);
        Assert.Equal(point, app.GraphRowPoint(6));
        app.RightClick(point);
        AssertMenu(app.Menu(), [.. BaseCommitMenu]);
        app.ClickMenuItem(InteractiveFromHereItem);
        await app.WaitForIdleAsync();
        await app.WaitForRebaseListAsync();
        await AssertNoMenuAsync(app, logMark: null);
        await WaitForListFocusAsync(app, "Five");
        AssertList(app, L0, L0BaseTop, selected: "Five", canStart: false, startToolTip: UnchangedReason);
    }

    /// <summary>Waits until the list's row <paramref name="subject"/> is selected and has the keyboard focus (the view gives it the focus once the layout has made the row).</summary>
    private static Task WaitForListFocusAsync(ShellDriver app, string subject) =>
        app.WaitForAsync(
            () => app.RebaseListSnapshot().Rows.SingleOrDefault(row => row.IsSelected) is { } selected && selected.Subject == subject && selected.HasKeyboardFocus,
            $"the list's row \"{subject}\" to be selected with the keyboard focus");

    /// <summary>
    /// Asserts the list's frame (design 2.2, "The list's geometry"): where it, its header and its
    /// footer are; the header's text on the left and the count on the right, not overlapping, the
    /// header whole (no tooltip: it fits); no message; no author column at 440 wide; the graph and
    /// its column headers hidden behind it.
    /// </summary>
    private static void AssertListFrame(ShellDriver app, InteractiveRebaseSnapshot list)
    {
        Assert.Equal(ListBounds, list.Bounds);
        Assert.Equal(HeaderBounds, list.HeaderBounds);
        Assert.Equal(FooterBounds, list.FooterBounds);
        Assert.Equal((ListHeader, (string?)null, ListCount, true), (list.Header, list.HeaderToolTip, list.Count, list.CountShown));
        Assert.Null(list.Message);
        var header = app.BoundsOf(app.FindByAutomationId("RebaseListHeader"));
        var count = app.BoundsOf(app.FindByAutomationId("RebaseListCount"));
        Assert.True(header.Right <= count.Left + 0.5, $"The header's text ({header}) overlaps the count ({count}).");
        Assert.True(header.Left < count.Left, "The header's text is not left of the count.");
        Assert.True(count.Right <= list.Bounds.Right && header.Left >= list.Bounds.Left, "The header's parts are not inside the list.");
        Assert.False(list.ShowsAuthor, "The list shows the author column at 440 wide.");
        Assert.False(Graph(app).IsEffectivelyVisible, "The graph shows beside the list.");
        Assert.False(app.Find<Border>("ColumnHeaders").IsEffectivelyVisible, "The graph's column headers show over the list.");
    }

    /// <summary>
    /// Asserts the list as the report's tables give it: its frame (<see cref="AssertListFrame"/>);
    /// each commit row top to bottom with its name, short SHA, action button text, bounds (30 high),
    /// indent, opacity (1, or 0.5 for <paramref name="dragged"/>), strike-through, second line and
    /// <c>Edit…</c> at its right; inside each row the grip (14, secondary colour, centred in its
    /// 24-wide column), the action button (84 × 22 inside the row), the short SHA (64 wide,
    /// secondary colour; a dropped row's in the disabled colour, its subject too) and the subject,
    /// all 16 further right for a squash or fixup row, no author; the join lines in the lane colour
    /// from each squash or fixup row's centre to its target's top edge; the base row
    /// (<c>Base</c> in the secondary colour, 4b4622c, Base) at <paramref name="baseTop"/>; the
    /// selected row (<paramref name="selected"/>) alone with the selection background; Start and
    /// Cancel right-aligned in the footer, 8 apart, Start's right edge at 688, Start enabled or not
    /// with <paramref name="startToolTip"/>, Cancel enabled. Returns the snapshot.
    /// </summary>
    private static InteractiveRebaseSnapshot AssertList(
        ShellDriver app,
        IReadOnlyList<ListRow> rows,
        double baseTop,
        string? selected,
        bool canStart,
        string? startToolTip,
        string? dragged = null)
    {
        var list = app.RebaseListSnapshot();
        var palette = PaletteOf(app);
        AssertListFrame(app, list);
        Assert.True(
            list.Rows.Select(row => row.Subject).SequenceEqual(rows.Select(row => row.Subject)),
            $"The list's rows are [{string.Join(", ", list.Subjects)}], expected [{string.Join(", ", rows.Select(row => row.Subject))}].");
        for (var index = 0; index < rows.Count; index++)
        {
            var want = rows[index];
            var row = list.Rows[index];
            var what = $"the row \"{want.Subject}\"";
            Assert.Equal((want.Name, Short(want.Sha), want.Sha, want.Action, ActionName(want.Action)), (row.Name, row.ShortSha, row.Sha, row.Action, row.ActionText));
            Assert.True(row.Bounds == want.Bounds, $"{what} is at {row.Bounds}, expected {want.Bounds}.");
            Assert.True(row.Indent == want.Indent, $"{what} is indented {row.Indent}, expected {want.Indent}.");
            Assert.True(row.Opacity == (want.Subject == dragged ? 0.5 : 1), $"{what} is drawn at opacity {row.Opacity}.");
            Assert.True(row.IsStruckThrough == want.StruckThrough, $"{what} is {(row.IsStruckThrough ? string.Empty : "not ")}struck through.");
            Assert.True(row.SecondLine == want.SecondLine, $"{what}'s second line is \"{row.SecondLine}\", expected \"{want.SecondLine}\".");
            Assert.True(row.SecondLineBounds == want.SecondLineBounds, $"{what}'s second line is at {row.SecondLineBounds}, expected {want.SecondLineBounds}.");
            if (want.SecondLine is not null)
            {
                var edit = row.EditButtonBounds ?? throw new Xunit.Sdk.XunitException($"{what}'s second line has no Edit… button.");
                var line = row.SecondLineBounds!.Value;
                Assert.True(edit.Top >= line.Top - 0.5 && edit.Bottom <= line.Bottom + 0.5 && edit.Right <= StartRight + 0.5 && edit.Left > line.Center.X, $"{what}'s Edit… ({edit}) is not at the right of its second line ({line}).");
            }
            else
            {
                Assert.Null(row.EditButtonBounds);
            }

            var x = want.Indent;
            var grip = row.GripBounds!.Value;
            Assert.Equal((GripSize, GripSize), (grip.Width, grip.Height));
            Assert.Equal(GripColumnX + x + (GripColumnWidth / 2), grip.Center.X, 0.5);
            Assert.Equal(want.Centre, grip.Center.Y, 0.5);
            var button = row.ActionButtonBounds!.Value;
            Assert.Equal(new Rect(ActionButtonX + x, button.Y, ActionButtonWidth, ActionButtonHeight), button);
            Assert.True(button.Top >= want.Top && button.Bottom <= want.Top + RowHeight, $"{what}'s action button ({button}) is not inside its 30 px.");
            Assert.Equal((ShaX + x, ShaWidth), (row.ShaBounds!.Value.X, row.ShaBounds.Value.Width));
            Assert.Equal(SubjectX + x, row.SubjectBounds!.Value.X);
            Assert.Null(row.AuthorBounds);
            Assert.Null(row.SubjectToolTip);
            Assert.Equal(palette.TextSecondary, row.GripColor);
            Assert.Equal(want.StruckThrough ? palette.TextDisabled : palette.TextSecondary, row.ShaColor);
            if (want.StruckThrough)
            {
                Assert.Equal(palette.TextDisabled, row.SubjectColor);
            }

            var isSelected = want.Subject == selected;
            Assert.True(row.IsSelected == isSelected, $"{what} is {(row.IsSelected ? string.Empty : "not ")}selected.");
            if (isSelected)
            {
                Assert.Equal(palette.Selection, row.Background);
            }
            else
            {
                Assert.NotEqual(palette.Selection, row.Background);
            }
        }

        // The join lines: from each squash or fixup row's centre to the top edge of its target,
        // the nearest Pick or Reword row below it.
        var expectedLines = new List<(string Member, string Target, Rect Bounds)>();
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Action is RebaseAction.Squash or RebaseAction.Fixup)
            {
                var target = rows.Skip(index + 1).First(row => row.Action is RebaseAction.Pick or RebaseAction.Reword);
                expectedLines.Add((rows[index].Sha, target.Sha, JoinLine(rows[index].Top, target.Top)));
            }
        }

        Assert.Equal(expectedLines, list.JoinLines.Select(line => (line.MemberSha, line.TargetSha, line.Bounds)));
        Assert.All(list.JoinLines, line => Assert.Equal(palette.Lane(0), line.Color));
        Assert.All(list.Rows.Where(row => row.Action is not (RebaseAction.Squash or RebaseAction.Fixup)), row => Assert.Null(row.JoinTargetSha));

        var baseRow = list.BaseRow ?? throw new Xunit.Sdk.XunitException("The list has no base row.");
        Assert.Equal((Short(BaseSha), "Base", (Rect?)new Rect(ListLeft, baseTop, ListWidth, RowHeight), palette.TextSecondary), (baseRow.ShortSha, baseRow.Subject, baseRow.Bounds, baseRow.LabelColor));
        var baseLabel = app.FindByAutomationId("RebaseBaseRow").GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == "RebaseBaseLabel");
        Assert.Equal(BaseLabel, baseLabel.Text);

        Assert.True(list.StartShown, "Start rebase is not shown.");
        Assert.Equal(StartText, ((Button)app.FindByAutomationId("RebaseStartButton")).Content);
        Assert.Equal(CancelText, ((Button)app.FindByAutomationId("RebaseCancelButton")).Content);
        Assert.Equal(StartRight, list.StartBounds.Right, 0.5);
        Assert.Equal(FooterButtonGap, list.StartBounds.Left - list.CancelBounds.Right, 0.5);
        Assert.True(list.FooterBounds.Contains(list.StartBounds) && list.FooterBounds.Contains(list.CancelBounds), "Start rebase and Cancel are not in the footer.");
        Assert.True((canStart, startToolTip) == (list.CanStart, list.StartToolTip), $"Start rebase is {(list.CanStart ? "enabled" : "disabled")} with the tooltip \"{list.StartToolTip}\"; expected {(canStart ? "enabled" : "disabled")} with \"{startToolTip}\".");
        Assert.True(app.FindByAutomationId("RebaseCancelButton").IsEffectivelyEnabled, "Cancel is disabled.");
        Assert.True(list.IsInteractive, "The list takes no input.");
        Assert.True(selected is null ? list.Selected is null : list.Selected?.Subject == selected, $"The selected row is \"{list.Selected?.Subject}\", expected \"{selected}\".");
        return list;
    }

    /// <summary>Asserts that the list's row <paramref name="subject"/> is selected and has the keyboard focus (design 2.2).</summary>
    private static void AssertListFocus(ShellDriver app, string subject)
    {
        var selected = app.RebaseListSnapshot().Selected ?? throw new Xunit.Sdk.XunitException("No row of the list is selected.");
        Assert.Equal(subject, selected.Subject);
        Assert.True(selected.HasKeyboardFocus, $"The selected row \"{subject}\" does not have the keyboard focus: {app.Window.FocusManager?.GetFocusedElement()?.GetType().Name ?? "nothing"} has it.");
    }

    /// <summary>
    /// Asserts a list row's menu (2.2, "A list row's menu"): its lines, the highlighted item, 4
    /// items 136 high and 4 px inside the window, its top-left at <paramref name="topLeft"/> (±1),
    /// the items' text colours (a disabled one in the disabled colour, the others primary).
    /// </summary>
    private static void AssertRowMenu(ShellDriver app, MenuSnapshot menu, IReadOnlyList<string> lines, Point topLeft)
    {
        AssertMenu(menu, [.. lines]);
        AssertHighlighted(menu, RowMenuHighlighted);
        AssertMenuShape(app, menu);
        Assert.Equal(RowMenuHeight, menu.Bounds.Height, 2);
        Assert.True(Math.Abs(menu.Bounds.X - topLeft.X) <= 1 && Math.Abs(menu.Bounds.Y - topLeft.Y) <= 1, $"The menu's top-left is {menu.Bounds.TopLeft}, expected {topLeft}.");
        AssertMenuColours(app, menu);
    }

    /// <summary>The menu's items' text colours: a disabled item in the disabled colour, an enabled one in the primary colour (4a, D114).</summary>
    private static void AssertMenuColours(ShellDriver app, MenuSnapshot menu)
    {
        var palette = PaletteOf(app);
        foreach (var item in menu.Items)
        {
            var colour = !item.IsEnabled ? palette.TextDisabled : item.IsDestructive ? palette.Danger : palette.TextPrimary;
            Assert.True(item.TextColor == colour, $"The menu item \"{item.Label}\" is drawn in {item.TextColor}, expected {colour}.");
        }
    }

    /// <summary>
    /// Waits for the message dialog on top and asserts it (7.2): the title, the summary with the
    /// keyboard focus and the caret at its end, the counter (72 less the summary's length), the
    /// description, Cancel and the confirm button (enabled or not, with its tooltip), the wide
    /// card (460), no "In …" line. Returns what it shows.
    /// </summary>
    private static async Task<MessageDialogSnapshot> AssertMessageDialogAsync(
        ShellDriver app,
        string title,
        string summary,
        string description,
        string confirmText,
        bool confirmEnabled = true,
        string? confirmToolTip = null,
        bool summaryFocused = true)
    {
        await app.WaitForDialogAsync<MessageDialogViewModel>();
        if (summaryFocused)
        {
            await app.WaitForAsync(() => app.MessageDialog().SummaryHasFocus, "the message dialog's summary to have the keyboard focus");
        }

        var dialog = app.MessageDialog();
        Assert.Equal(title, dialog.Title);
        Assert.Equal(summary, dialog.Summary);
        if (summaryFocused)
        {
            Assert.True(dialog.SummaryHasFocus, "The summary does not have the keyboard focus.");
            Assert.Equal(summary.Length, dialog.SummaryCaret);
        }

        Assert.Equal(Counter(summary), dialog.Counter);
        Assert.Equal(description, dialog.Description.ReplaceLineEndings("\n"));
        Assert.Equal(confirmText, dialog.ConfirmText);
        Assert.True(confirmEnabled == dialog.ConfirmEnabled, $"The dialog's {confirmText} is {(dialog.ConfirmEnabled ? "enabled" : "disabled")}.");
        Assert.Equal(confirmToolTip, dialog.ConfirmToolTip);
        Assert.Equal(CancelText, dialog.CancelText);
        Assert.Null(dialog.Repository);
        Assert.Equal(MessageCardWidth, dialog.CardBounds.Width, 0.5);
        var view = app.TopDialogView!;
        Assert.Equal("MessageDialog", AutomationProperties.GetAutomationId(view));
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.True(ToolTip.GetShowOnDisabled(confirm), "The disabled confirm button does not show its reason.");
        return dialog;
    }

    /// <summary>The message dialog's confirm button is drawn in the primary style: its background, sampled 3 px inside its left edge, is the accent colour.</summary>
    private static void AssertPrimaryConfirm(ShellDriver app, Screenshot picture)
    {
        var confirm = app.BoundsOf(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        AssertColour(PaletteOf(app).Accent, picture, new Point(confirm.Left + 3, confirm.Center.Y), "the confirm button's background (primary)");
    }

    /// <summary>The dialog on top lies over the backdrop: its panel of the class <c>dialogBackdrop</c>, in the backdrop's colour, covers the whole window.</summary>
    private static void AssertOverBackdrop(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var backdrop = view.GetVisualDescendants().OfType<Panel>().FirstOrDefault(panel => panel.Classes.Contains("dialogBackdrop"))
            ?? throw new Xunit.Sdk.XunitException("The dialog has no backdrop.");
        Assert.Equal(PaletteOf(app).Backdrop, (backdrop.Background as ISolidColorBrush)?.Color);
        var bounds = app.BoundsOf(backdrop);
        var window = app.Window.ClientSize;
        Assert.True(bounds.Left <= 0.5 && bounds.Top <= 0.5 && bounds.Right >= window.Width - 0.5 && bounds.Bottom >= window.Height - 0.5, $"The backdrop at {bounds} does not cover the {window.Width}x{window.Height} window.");
    }

    /// <summary>
    /// Waits until the details panel shows the commit the graph has selected alone, with
    /// <paramref name="expected"/>'s subject, and asserts the rest (phase 1 and 3's panel): the
    /// body (or none), the author, the Date, no Committer row, the "Commit date" row only when
    /// expected, the parents' short SHAs in order, "Changed files (N)" and each file as "letter
    /// name folder".
    /// </summary>
    private static async Task AssertDetailsAsync(ShellDriver app, ShownDetails expected)
    {
        await app.WaitForAsync(
            () => Shown(app, "DetailsSubject") is SelectableTextBlock subject && subject.Text == expected.Subject
                && Shown(app, "DetailsSha") is SelectableTextBlock sha
                && Repo(app).Selection.Single is { } selected && sha.Text == selected
                && !Repo(app).Details.IsLoading,
            $"the details of \"{expected.Subject}\", the selected commit");
        app.Settle();
        var body = (SelectableTextBlock)app.FindByAutomationId("DetailsBody");
        Assert.True((expected.Body is not null) == body.IsEffectivelyVisible, $"The details {(body.IsEffectivelyVisible ? "show the body \"" + body.Text + "\"" : "show no body")}.");
        if (expected.Body is not null)
        {
            Assert.Equal(expected.Body, (body.Text ?? string.Empty).ReplaceLineEndings("\n"));
        }

        Assert.Equal(Author, ((SelectableTextBlock)app.FindByAutomationId("DetailsAuthor")).Text);
        Assert.Equal(expected.Date, ((TextBlock)app.FindByAutomationId("DetailsDate")).Text);
        Assert.False(app.FindByAutomationId("DetailsCommitter").IsEffectivelyVisible, "The details show a Committer row.");
        var commitDate = (TextBlock)app.FindByAutomationId("DetailsCommitDate");
        Assert.True((expected.CommitDate is not null) == commitDate.IsEffectivelyVisible, $"The details {(commitDate.IsEffectivelyVisible ? "show" : "do not show")} a Commit date row.");
        if (expected.CommitDate is not null)
        {
            Assert.Equal(expected.CommitDate, commitDate.Text);
        }

        var parents = app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "ParentLink")
            .OrderBy(link => app.BoundsOf(link).X)
            .Select(link => AutomationProperties.GetName(link) ?? string.Empty)
            .ToList();
        Assert.Equal(expected.Parents, parents);
        Assert.Equal($"Changed files ({expected.Files.Count.ToString(CultureInfo.InvariantCulture)})", ((TextBlock)app.FindByAutomationId("ChangedFilesTitle")).Text);
        await app.WaitForAsync(() => DetailsFileRows(app).SequenceEqual(expected.Files), $"the details' files {string.Join(", ", expected.Files)}");
    }

    /// <summary>The details panel's changed files as "letter name folder", top to bottom.</summary>
    private static List<string> DetailsFileRows(ShellDriver app) =>
        [.. DetailsFiles(app).Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => part.Length > 0)))];

    /// <summary>The details panel's file rows.</summary>
    private static List<ChangedFileRow> DetailsFiles(ShellDriver app)
    {
        var list = (ItemsControl)app.FindByAutomationId("FileList");
        return [.. ((IEnumerable<ChangedFileRow>)(list.ItemsSource ?? Array.Empty<ChangedFileRow>())).Where(row => row.IsFile)];
    }

    /// <summary>The visible control with this automation id, or null while none is in the window.</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>Asserts that the graph's selection (<c>RepositoryViewModel.Selection</c>) holds <paramref name="sha"/> alone, not the working-changes row.</summary>
    private static void AssertGraphSelection(ShellDriver app, string sha)
    {
        var selection = Repo(app).Selection;
        Assert.True(selection.Count == 1 && selection.Single == sha, $"The graph's selection is {selection}, expected {Short(sha)} alone.");
        Assert.False(Repo(app).IsWorkingRowSelected, "The working-changes row is selected.");
    }

    /// <summary>
    /// Asserts the graph's selected rows (by index, in the graph shown) and, with a picture, the
    /// selection background on each of them and on no other row (sampled at the top right of the
    /// row's Message cell, where no text reaches).
    /// </summary>
    private static void AssertSelectedRows(ShellDriver app, Screenshot? picture, params int[] rows)
    {
        var graph = Graph(app);
        var data = graph.Data!;
        var expected = rows.Select(row => data.CommitAt(row).Sha).Order(StringComparer.Ordinal).ToList();
        var actual = Repo(app).Selection.Shas.Order(StringComparer.Ordinal).ToList();
        Assert.True(expected.SequenceEqual(actual), $"The selection is [{string.Join(", ", actual.Select(sha => sha[..7]))}], expected rows [{string.Join(", ", rows)}].");
        if (rows.Length == 1)
        {
            Assert.Equal(rows[0], Repo(app).SelectedIndex);
        }

        if (picture is null)
        {
            return;
        }

        var palette = PaletteOf(app);
        var origin = app.BoundsOf(graph).TopLeft;
        var covers = app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && (control is ToolTip || AutomationProperties.GetAutomationId(control) == "ContextMenu"))
            .Select(control => app.BoundsOf(control))
            .ToList();
        for (var row = 0; row < data.Count; row++)
        {
            var bounds = graph.RowBounds(row);
            var point = new Point(origin.X + graph.Columns.Message.Right - 4, origin.Y + bounds.Top + 2);
            if (covers.Any(cover => cover.Inflate(24).Contains(point)))
            {
                continue;
            }

            var colour = picture.PixelAt(point);
            Assert.True(rows.Contains(row) == (colour == palette.Selection), $"Row {row} is {(rows.Contains(row) ? string.Empty : "not ")}selected, and its background at {point} is {colour}.");
        }
    }

    /// <summary>Waits until the keyboard focus is on the graph (2.2: where it goes when the list closes).</summary>
    private static Task WaitForGraphFocusAsync(ShellDriver app) =>
        app.WaitForAsync(() => Graph(app).IsFocused, "the graph to have the keyboard focus");

    /// <summary>Waits until the list has closed and the graph shows with its column headers.</summary>
    private static async Task WaitForListClosedAsync(ShellDriver app)
    {
        await app.WaitForAsync(() => !app.IsRebaseListOpen && !Repo(app).IsInteractiveRebaseOpen && Graph(app).IsEffectivelyVisible, "the list to close and the graph to show");
        Assert.True(app.Find<Border>("ColumnHeaders").IsEffectivelyVisible, "The graph's column headers do not show.");
        Assert.True(Repo(app).ShowsGraph, "The graph area does not show the graph.");
    }

    /// <summary>
    /// "Git unchanged" (part B's conventions): refs/heads/main at Five (57f089d), <c>git status
    /// --short --branch</c> prints exactly <c>## main</c>, nothing under refs/visualcommit/, and no
    /// <c>rebase-merge/</c> or <c>rebase-apply/</c> in .git.
    /// </summary>
    private static async Task AssertGitUnchangedAsync(TempRepo repo, string tip = FiveSha)
    {
        Assert.Equal(tip, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal([CleanStatus], await StatusAsync(repo));
        Assert.Empty(await GitLinesAsync(repo, "for-each-ref", "refs/visualcommit/"));
        AssertNoRebaseFolders(repo);
    }

    /// <summary>No rebase is in progress: no <c>rebase-merge/</c> and no <c>rebase-apply/</c> in .git.</summary>
    private static void AssertNoRebaseFolders(TempRepo repo)
    {
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-merge")), "A rebase-merge/ folder is in .git.");
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-apply")), "A rebase-apply/ folder is in .git.");
    }

    /// <summary>Asserts that the app ran no <c>git rebase</c> call at all.</summary>
    private static void AssertNoRebaseCall(ShellDriver app)
    {
        var rebases = app.GitCalls().Where(call => call.CommandText.StartsWith("git rebase", StringComparison.Ordinal)).Select(call => call.CommandText).ToList();
        Assert.True(rebases.Count == 0, "The app ran git rebase:\n" + string.Join('\n', rebases));
    }

    /// <summary>
    /// "No write call has run" since <paramref name="callMark"/> and <paramref name="logMark"/>:
    /// no call that writes (rebase, update-ref, commit, reset, checkout, switch, cherry-pick,
    /// revert, branch, tag, stash) and no operation of the queue ended, other than the backups'
    /// background clean-up.
    /// </summary>
    private static void AssertNoWriteSince(ShellDriver app, int callMark, int logMark)
    {
        string[] writes = ["git rebase", "git update-ref", "git commit", "git reset", "git checkout", "git switch", "git cherry-pick", "git revert", "git branch", "git tag", "git stash"];
        var written = CallsSince(app, callMark).Where(call => writes.Any(write => call.StartsWith(write, StringComparison.Ordinal))).ToList();
        Assert.True(written.Count == 0, "The app ran a write call:\n" + string.Join('\n', written));
        var ended = app.LogLinesSince(logMark)
            .Where(line => AnyOperationEnded().IsMatch(line) && !line.Contains(": " + RepositoryViewModel.CleanUpBackupsTitle + ": ", StringComparison.Ordinal))
            .ToList();
        Assert.True(ended.Count == 0, "An operation ran:\n" + string.Join('\n', ended));
    }

    /// <summary>Any line the queue logs at an operation's end in the interactive tab.</summary>
    private static System.Text.RegularExpressions.Regex AnyOperationEnded() =>
        new($@"\] {Repository}: [^:]+: \w+ after \d+ ms", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>The lines of <c>git status --short --branch</c>.</summary>
    private static Task<List<string>> StatusAsync(TempRepo repo) => GitLinesAsync(repo, "status", "--short", "--branch");

    /// <summary>The lines git prints for these arguments, without empty ones and line-end carriage returns.</summary>
    private static async Task<List<string>> GitLinesAsync(TempRepo repo, params string[] arguments) =>
        [.. (await repo.GitAsync(arguments)).StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    /// <summary>A commit's message exactly as git keeps it: <c>git cat-file commit</c> after the header's first empty line.</summary>
    private static async Task<string> RawMessageAsync(TempRepo repo, string sha)
    {
        var raw = (await repo.GitAsync("cat-file", "commit", sha)).StandardOutput;
        var end = raw.IndexOf("\n\n", StringComparison.Ordinal);
        Assert.True(end >= 0, $"git cat-file commit {sha} has no message.");
        return raw[(end + 2)..];
    }

    /// <summary>A commit's author and committer dates as UTC times, from <c>%at</c> and <c>%ct</c>.</summary>
    private static async Task<(DateTimeOffset Author, DateTimeOffset Committer)> DatesAsync(TempRepo repo, string sha)
    {
        var parts = (await GitLinesAsync(repo, "log", "-1", "--format=%at %ct", sha)).Single().Split(' ');
        return (
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[0], CultureInfo.InvariantCulture)),
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[1], CultureInfo.InvariantCulture)));
    }

    /// <summary>A commit's author and committer as "name &lt;email&gt; / name &lt;email&gt;".</summary>
    private static async Task<string> PeopleAsync(TempRepo repo, string sha) =>
        (await GitLinesAsync(repo, "log", "-1", "--format=%an <%ae> / %cn <%ce>", sha)).Single();

    /// <summary>A commit's parents' full SHAs.</summary>
    private static async Task<List<string>> ParentsAsync(TempRepo repo, string sha) =>
        [.. (await GitLinesAsync(repo, "log", "-1", "--format=%P", sha)).Single().Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>What a commit changes against its first parent, as git's <c>--name-status</c> lines ("A\tsrc/four.txt").</summary>
    private static Task<List<string>> ChangesAsync(TempRepo repo, string sha) =>
        GitLinesAsync(repo, "diff-tree", "--no-commit-id", "--name-status", "-r", sha + "^", sha);

    /// <summary>A file's content in a commit, line ends as git keeps them.</summary>
    private static async Task<string> FileAtAsync(TempRepo repo, string sha, string path) =>
        (await repo.GitAsync("show", sha + ":" + path)).StandardOutput;

    /// <summary>
    /// The backups (part B's conventions): exactly one ref, <c>refs/visualcommit/backup/rebase-&lt;time&gt;/main</c>,
    /// its time D67's, pointing at <paramref name="target"/> (main's old tip).
    /// </summary>
    private static async Task AssertRebaseBackupAsync(TempRepo repo, string target)
    {
        var backups = await BackupsAsync(repo);
        var backup = Assert.Single(backups);
        Assert.Equal(("rebase", (int?)null, false, "main"), (backup.Ref.Kind, backup.Ref.Number, backup.Ref.IsChanges, backup.Ref.Name));
        Assert.Equal(target, backup.Sha);
        Assert.StartsWith(BackupRef.Prefix + "rebase-", backup.Name, StringComparison.Ordinal);
        Assert.EndsWith("/main", backup.Name, StringComparison.Ordinal);
        Assert.Single(await GitLinesAsync(repo, "for-each-ref", "refs/visualcommit/"));
    }

    /// <summary>The open diff's text rows (<c>DiffText</c>) as "K text": hunk headers as "@@", lines with their kind's letter (A added, R removed, C context).</summary>
    private static List<string> DiffRows(ShellDriver app)
    {
        var view = app.Window.GetVisualDescendants().OfType<DiffTextView>().First(text => text.Name == "DiffText" && text.IsEffectivelyVisible);
        return [.. view.Rows.SelectMany(row => row.Kind switch
        {
            DiffRowKind.HunkHeader => ["@@"],
            DiffRowKind.Line => new[] { row.Left, row.Right }.OfType<DiffCell>().Select(cell => $"{Letter(cell.Kind)} {cell.Text}"),
            _ => ["\\ no newline"],
        })];
    }

    private static string Letter(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Added => "A",
        DiffLineKind.Removed => "R",
        _ => "C",
    };

    /// <summary>A picture's pixel is not the selection colour (a row that is not selected).</summary>
    private static void AssertNotSelectionColour(Screenshot picture, Color selection, Point at, string what)
    {
        var colour = picture.PixelAt(at);
        Assert.True(colour != selection, $"The {what} at {at} has the selection colour {selection}.");
    }

    /// <summary>
    /// Rests the pointer on the open menu's item <paramref name="label"/> in one move (so it
    /// crosses no other item) until its tooltip is open and faded in; returns its text.
    /// </summary>
    private static async Task<string> RestOnMenuItemAsync(ShellDriver app, string label)
    {
        var text = await app.RestPointerOnMenuItemAsync(label);
        await WaitForToolTipShownAsync(app);
        return text;
    }

    /// <summary>Rests the pointer on <paramref name="target"/> until its tooltip is open and faded in; returns its text.</summary>
    private static async Task<string> RestOnAsync(ShellDriver app, Visual target)
    {
        var text = await app.RestPointerOnAsync(target);
        await WaitForToolTipShownAsync(app);
        return text;
    }

    /// <summary>Waits until a tooltip is open and every open tooltip has faded in completely (a condition, not a time).</summary>
    private static async Task WaitForToolTipShownAsync(ShellDriver app)
    {
        await app.WaitForAsync(
            () => app.Window.GetVisualDescendants().OfType<ToolTip>().Any(tip => tip.IsEffectivelyVisible)
                && app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible).All(tip => tip.Opacity >= 1),
            "the open tooltip to fade in completely");
        app.Settle();
    }

    /// <summary>The open tooltip with <paramref name="text"/> is drawn inside the window (D105).</summary>
    private static void AssertToolTipInsideWindow(ShellDriver app, string text)
    {
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
        var tip = Assert.Single(tips, candidate => ToolTipText(candidate) == text);
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height, $"The tooltip at {bounds} is not inside the {size.Width}x{size.Height} window.");
    }

    private static string ToolTipText(ToolTip tip) => tip.Content switch
    {
        string text => text,
        Visual visual => string.Join('\n', (visual is TextBlock self ? [self] : visual.GetVisualDescendants().OfType<TextBlock>()).Select(block => block.Text)),
        _ => string.Empty,
    };

    /// <summary>The platform's command modifier as raw keys (Ctrl, Cmd on macOS): what "select all" holds.</summary>
    private static RawInputModifiers CommandKeys(ShellDriver app) =>
        app.CommandModifier == KeyModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control;

    /// <summary>The platform's select-all in the focused text box.</summary>
    private static void SelectAll(ShellDriver app) => app.PressKey(Key.A, CommandKeys(app));

    /// <summary>The control named <paramref name="name"/> inside the diff view.</summary>
    private static T InDiff<T>(ShellDriver app, string name)
        where T : Control =>
        app.FindByAutomationId("DiffView").GetVisualDescendants().OfType<T>().First(control => control.Name == name);
}
