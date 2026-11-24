// Part U's launch of the 8a pass (docs/test-reports/phase-8a.md, U1, U3 and "The real window (part U)";
// docs/phase-8a-design.md 1.12, 9.3, 12.3.4 launch 13, 12.3.5; D411, D435, D445, D450, D455): the graph's
// selected row as a screen reader meets it on a stopped merge, through UI Automation's events, and the
// fixed names of the resolver, the activity log, the splitters, the search bar and a stage row's
// buttons, each read through UI Automation with its key. No capture: part U's real-window steps have no
// twin. Nothing is pressed that leaves the app; the resolver is closed by its own button, since Esc in
// its result closes nothing.
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
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
    /// <summary>The lanes of the stopped merge's graph (GC-M): its Message column starts at x 486.</summary>
    private const int ConflictsLanes = VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC2.ConflictsLanes;

    /// <summary>How far, in logical pixels, the exposed row's height may lie from a display row's (<c>WindowLayout.RowHeight</c>).</summary>
    private const double RowHeightTolerance = 1;

    /// <summary>
    /// Launch 13 (design 12.3.4; method 2 of part U, <c>Check_U1_U3_stopped_merge_rows_and_resolver</c>, is
    /// the scripted counterpart of its rows and its resolver; no twin), one tab on a copy of the stopped
    /// merge (<c>Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)</c>) with <c>u-untracked.txt</c>
    /// written into its work tree before the start, dark, 1100×700, no capture:
    /// <list type="number">
    /// <item>The graph's rows below the merge's banner. A real click on display row 1, "Ours edits": UI
    /// Automation's focused element <c>GraphRows</c> and the details of "Ours edits". The graph's first child
    /// read once (its node made, D455): <c>GraphSelectedRow</c>, its name and help text "Ours edits"'s. Then
    /// the row's events recorded (<c>RecordSelectedRowEvents</c>).</item>
    /// <item>Real Up, then Down twice (<c>U.MergeMoves</c>), one at a time: after each, within
    /// <c>U.EventWait</c>, a LiveRegionChanged from <c>GraphSelectedRow</c> (D445 as D450 amends it; a Name
    /// change alone fails the method and is written to run.txt); then the row's name and help text the
    /// table's, its control type ListItem, <c>GraphRows</c>' Selection pattern holding that one element, its
    /// height a display row's ±1 inside <c>GraphRowsBounds()</c>, UI Automation's focused element still
    /// <c>GraphRows</c>, never the row (ruling 35). Each move's events by kind, id and time in run.txt, with
    /// their counts.</item>
    /// <item>The names (1.12), each read through UI Automation, noted with its <c>AcceleratorKey</c> and
    /// compared with U3's table: the resolver's five (a real click on display row 0, a real double-click on
    /// <c>notes.txt</c>, then a real click on <c>ResolverCloseButton</c> and a wait until the resolver has
    /// closed); a real Ctrl+J, the activity log's two and the three splitters; a real Ctrl+F, the search
    /// bar's three, a real Esc; the real pointer onto <c>u-untracked.txt</c>'s row, its Stage and Discard
    /// (each the one on that row, D425), a real click on its Stage, the real pointer onto its staged row,
    /// its Unstage. The names are asserted after the app has closed, so that run.txt holds every one.</item>
    /// </list>
    /// The app closed by its window, exit code 0.
    /// </summary>
    [Fact]
    public async Task Check_U_graph_row_and_names()
    {
        using var notes = NoteLaunch(nameof(Check_U_graph_row_and_names));
        AssertDesktopReady();
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        repo.WriteFile(U.GraphRowLaunch.File, U.GraphRowLaunch.FileContent);
        var name = NameOf(repo);
        var before = await StatusAsync(repo);
        Note($"git status --porcelain before the start: {before}");
        using var app = Opened(Launch(Run.NewDataDirectory(), OneTab(repo.Path)), name);
        app.WaitFor(
            () => app.BannerBounds() is not null && app.ShownElements(U.GraphRowsId).Count == 1,
            "the graph's rows below the merge's banner",
            LoadTimeout);
        Note($"the merge's banner at {app.BannerBounds()}; the graph's rows at {app.GraphRowsBounds()}");

        // The row: display row 1, "Ours edits", by a real click, the window's first input; the graph's
        // first child read once before the recording starts (D455); then the moves.
        var ours = app.GraphRowPoint(U.OursEdits.DisplayRow, ConflictsLanes);
        app.ClickAt(ours.X, ours.Y);
        app.WaitForFocusOn(U.GraphRowsId, StepTimeout);
        app.WaitFor(() => app.NamesOnScreen("DetailsSubject").Contains(U.OursEdits.Subject), $"the details of \"{U.OursEdits.Subject}\"", StepTimeout);
        var graphRows = ShownOne(app, U.GraphRowsId);
        var first = RealApp.ReadOr<AutomationElement?>(() => graphRows.FindFirstChild(), null)
            ?? throw new XunitException($"{U.GraphRowsId} has no child that UI Automation gives after a click on \"{U.OursEdits.Subject}\".");
        var firstId = ReadText(() => first.Properties.AutomationId.ValueOrDefault);
        Note($"the graph's first child, read before the recording (D455): {Describe(first)}; its id \"{firstId}\", expected \"{U.SelectedRowId}\"");
        Assert.Equal(U.SelectedRowId, firstId);
        WaitForRowName(app, first, U.OursEdits.Name, "after the click");
        var firstHint = ReadText(() => RealApp.HelpTextOf(first));
        Note($"after the click: {U.SelectedRowId} named \"{U.OursEdits.Name}\" (waited for), its help text \"{firstHint}\"");
        Assert.Equal(U.OursEdits.Hint, firstHint);

        using (var events = app.RecordSelectedRowEvents())
        {
            foreach (var move in U.MergeMoves)
            {
                var mark = events.Now;
                Keys(app, KeyOf(move.Key));
                WaitForLiveRegion(app, events, mark, move.Key);
                AssertExposedRow(app, move);
                NoteEvents(events, mark, move.Key);
            }

            NoteEvents(events, TimeSpan.Zero, "all three moves");
        }

        // The names (1.12): each read, noted with its key, compared at the end.
        List<string> problems = [];

        // The resolver's five: the working-changes row by a real click, notes.txt by a real double-click
        // (phase 4's way), the resolver closed by its own button.
        var working = app.GraphRowPoint(0, ConflictsLanes);
        app.ClickAt(working.X, working.Y);
        _ = app.StageFileRow(U.UnstagedListId, U.Notes, StepTimeout);
        _ = app.StageFileRow(U.UnstagedListId, U.Untracked, StepTimeout);
        app.MoveToNeutral();
        var conflicted = app.StageFileRow(U.UnstagedListId, U.Notes, StepTimeout);
        AssertNeverPressed(conflicted);
        app.DoubleClickWithMouse(conflicted);
        app.WaitForResolver(U.Notes, StepTimeout);
        foreach (var id in U.ResolverIds)
        {
            problems.AddRange(ReadFixedName(app, id));
        }

        Press(app, ShownOne(app, "ResolverCloseButton"));
        app.WaitForResolverClosed(StepTimeout);
        Note("the resolver closed by a real click on ResolverCloseButton");

        // A real Ctrl+J: the activity log's two and the three splitters.
        Keys(app, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_J);
        app.WaitForActivityLog(shown: true, StepTimeout);
        app.WaitFor(() => U.LogAndSplitterIds.All(id => app.ShownElements(id).Count == 1), "the activity log's buttons and the three splitters on screen", StepTimeout);
        foreach (var id in U.LogAndSplitterIds)
        {
            problems.AddRange(ReadFixedName(app, id));
        }

        // A real Ctrl+F: the search bar's three; a real Esc in its box.
        Keys(app, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
        app.WaitFor(
            () => app.IsSearchBarShown && app.HasFocusOnScreen("SearchBox") && U.SearchIds.All(id => app.ShownElements(id).Count == 1),
            "the search bar with its box focused and its three buttons",
            StepTimeout);
        foreach (var id in U.SearchIds)
        {
            problems.AddRange(ReadFixedName(app, id));
        }

        Keys(app, VirtualKeyShort.ESCAPE);
        app.WaitFor(() => !app.IsSearchBarShown, "the search bar to close", StepTimeout);

        // The untracked row hovered: its Stage and Discard, each the one on that row (D425); Stage by a
        // real click; the staged row hovered: its Unstage.
        var (stage, discard, unstage) = (U.LaunchRowNames[0], U.LaunchRowNames[1], U.LaunchRowNames[2]);
        app.SetPointerOn(app.StageFileRow(stage.List, stage.Row, StepTimeout));
        var stageButton = RowButton(app, stage.List, stage.Row, stage.Id);
        problems.AddRange(ReadRowName(app, stageButton, stage));
        problems.AddRange(ReadRowName(app, RowButton(app, discard.List, discard.Row, discard.Id), discard));
        Press(app, stageButton);
        app.SetPointerOn(app.StageFileRow(unstage.List, unstage.Row, StepTimeout));
        problems.AddRange(ReadRowName(app, RowButton(app, unstage.List, unstage.Row, unstage.Id), unstage));

        Note($"git status --porcelain at the end: {await StatusAsync(repo)}");
        Assert.Equal(0, CloseNoted(app));
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    // ---- Part U's steps ----

    /// <summary>The real key of a move of <c>U.MergeMoves</c>: <c>Up</c> or <c>Down</c>.</summary>
    private static VirtualKeyShort KeyOf(string key) => key switch
    {
        "Up" => VirtualKeyShort.UP,
        "Down" => VirtualKeyShort.DOWN,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Launch 13 moves by Up and Down alone."),
    };

    /// <summary>
    /// Waits, at most <c>U.EventWait</c>, for a LiveRegionChanged from <c>GraphSelectedRow</c> after
    /// <paramref name="mark"/> (D445 as D450 amends it). None: the method fails, and run.txt and the failure
    /// say whether a Name change came alone (D450) or nothing came at all, with the events seen.
    /// </summary>
    private void WaitForLiveRegion(RealApp app, RealApp.SelectedRowEvents events, TimeSpan mark, string key)
    {
        IReadOnlyList<RealApp.RowEvent> seen = [];
        var clock = Stopwatch.StartNew();
        try
        {
            app.WaitFor(
                () => (seen = EventsSince(events, mark)).Any(item => item.Kind == RealApp.SelectedRowEvents.LiveRegionChanged && item.AutomationId == U.SelectedRowId),
                $"{key}: a LiveRegionChanged from {U.SelectedRowId}",
                U.EventWait);
        }
        catch (TimeoutException)
        {
            var names = seen.Count(item => item.Kind == RealApp.SelectedRowEvents.NameChanged && item.AutomationId == U.SelectedRowId);
            var message = string.Create(
                CultureInfo.InvariantCulture,
                $"{key}: no LiveRegionChanged from {U.SelectedRowId} within {U.EventWait.TotalSeconds:0} s; {(names > 0 ? $"{names} Name changes from it came alone, which fails (D450)" : "no Name change from it either")}. Events since the key: {Described(seen, mark)}.");
            Note(message);
            throw new XunitException(message);
        }

        Note(string.Create(CultureInfo.InvariantCulture, $"{key}: a LiveRegionChanged from {U.SelectedRowId} within {clock.Elapsed.TotalSeconds:0.00} s of the key's end"));
    }

    /// <summary>
    /// After a move: the graph's one child, <c>GraphSelectedRow</c>, named as the move's row within
    /// <c>U.EventWait</c>, its help text the move's hint, its control type ListItem; <c>GraphRows</c>'
    /// Selection pattern that one element; its height a display row's ±1 inside <c>GraphRowsBounds()</c>;
    /// UI Automation's focused element <c>GraphRows</c>, never the row (ruling 35). Noted in run.txt, then
    /// asserted. Every read of the row and the selection is a current one and never throws
    /// (<see cref="RealApp.ReadOr{T}"/>): a read that fails is noted as <see cref="RealApp.NotReadable"/> and
    /// fails its assertion by name, so the method still judges the rest.
    /// </summary>
    private void AssertExposedRow(RealApp app, U.Move move)
    {
        var rows = ShownOne(app, U.GraphRowsId);
        var row = RealApp.ReadOr<AutomationElement?>(() => rows.FindFirstChild(), null)
            ?? throw new XunitException($"{move.Key}: {U.GraphRowsId} has no child that UI Automation gives.");
        WaitForRowName(app, row, move.Row.Name, move.Key);
        var id = ReadText(() => row.Properties.AutomationId.ValueOrDefault);
        var help = ReadText(() => RealApp.HelpTextOf(row));
        var type = ReadText(() => row.Properties.ControlType.ValueOrDefault.ToString());
        var selection = RealApp.ReadOr<AutomationElement[]?>(() => rows.Patterns.Selection.PatternOrDefault?.Selection.ValueOrDefault ?? [], null);
        List<string?> selected = selection is null
            ? [RealApp.NotReadable]
            : [.. selection.Select(element => ReadText(() => element.Properties.AutomationId.ValueOrDefault ?? "(no id)"))];
        var bounds = RealApp.ReadOr<RectangleF?>(() => app.BoundsOf(row), null);
        var graph = app.GraphRowsBounds();
        var focus = app.FocusedId();
        Note(string.Create(
            CultureInfo.InvariantCulture,
            $"{move.Key}: the exposed row {Describe(row)}, help text \"{help}\"; {U.GraphRowsId}' selection [{string.Join(", ", selected)}]; its bounds {bounds?.ToString() ?? RealApp.NotReadable} in the graph's {graph}, display row {move.Row.DisplayRow}'s centre at y {app.GraphRowCentreY(move.Row.DisplayRow):0.#}; the focus on {focus ?? "nothing"}"));

        Assert.Equal(U.SelectedRowId, id);
        Assert.Equal(move.Hint, help);
        Assert.Equal(ControlType.ListItem.ToString(), type);
        Assert.Equal([U.SelectedRowId], selected);
        if (bounds is not { } exposed)
        {
            throw new XunitException($"{move.Key}: the exposed row's bounds could not be read through UI Automation.");
        }

        Assert.True(
            Math.Abs(exposed.Height - WindowLayout.RowHeight) <= RowHeightTolerance,
            string.Create(CultureInfo.InvariantCulture, $"{move.Key}: the exposed row is {exposed.Height:0.##} high, not {WindowLayout.RowHeight} ± {RowHeightTolerance}."));
        var inside = RectangleF.Inflate(graph, 0.5f, 0.5f);
        Assert.True(inside.Contains(exposed), $"{move.Key}: the exposed row at {exposed} does not lie inside {U.GraphRowsId} at {graph}.");
        Assert.True(focus == U.GraphRowsId, $"{move.Key}: UI Automation's focused element is {focus ?? "nothing"}, not {U.GraphRowsId} (ruling 35: never the row).");
    }

    /// <summary>
    /// Waits, at most <c>U.EventWait</c>, until <paramref name="row"/>'s name is <paramref name="expected"/>; a
    /// read that fails (<see cref="RealApp.ReadOr{T}"/>) counts as not yet and the wait looks again; a timeout
    /// quotes the name read last.
    /// </summary>
    private static void WaitForRowName(RealApp app, AutomationElement row, string expected, string when)
    {
        var seen = string.Empty;
        try
        {
            app.WaitFor(() => (seen = ReadText(() => row.Properties.Name.ValueOrDefault) ?? string.Empty) == expected, $"{when}: {U.SelectedRowId} named \"{expected}\"", U.EventWait);
        }
        catch (TimeoutException ex)
        {
            throw new XunitException($"{ex.Message} It is named \"{seen}\".");
        }
    }

    /// <summary>The events recorded after <paramref name="mark"/>, in the order they came.</summary>
    private static IReadOnlyList<RealApp.RowEvent> EventsSince(RealApp.SelectedRowEvents events, TimeSpan mark) =>
        [.. events.Events().Where(item => item.At > mark)];

    /// <summary>run.txt's line of the events after <paramref name="mark"/>: each by kind, sender's id and time after the mark, and their counts by kind and sender.</summary>
    private void NoteEvents(RealApp.SelectedRowEvents events, TimeSpan mark, string what)
    {
        var since = EventsSince(events, mark);
        var counts = since.GroupBy(item => $"{item.Kind} from {item.AutomationId ?? "(no id)"}")
            .Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Key}: {group.Count()}"));
        Note($"{what}: the events {Described(since, mark)}; counts {(since.Count == 0 ? "none" : string.Join(", ", counts))}");
    }

    /// <summary>Events in words: kind, sender's id and milliseconds after <paramref name="mark"/>.</summary>
    private static string Described(IReadOnlyList<RealApp.RowEvent> events, TimeSpan mark) =>
        events.Count == 0
            ? "none"
            : string.Join("; ", events.Select(item => string.Create(CultureInfo.InvariantCulture, $"{item.Kind} from {item.AutomationId ?? "(no id)"} at +{(item.At - mark).TotalMilliseconds:0} ms")));

    /// <summary>An element in words for run.txt: its id, control type, class and name, each <see cref="RealApp.NotReadable"/> where its read fails.</summary>
    private static string Describe(AutomationElement element) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ReadText(() => element.Properties.AutomationId.ValueOrDefault) ?? "(no id)"} ({ReadText(() => element.Properties.ControlType.ValueOrDefault.ToString())}, class {ReadText(() => element.Properties.ClassName.ValueOrDefault) ?? "none"}) \"{ReadText(() => element.Properties.Name.ValueOrDefault)}\"");

    /// <summary>A text read through UI Automation that never throws (<see cref="RealApp.ReadOr{T}"/>): <see cref="RealApp.NotReadable"/> when the read fails.</summary>
    private static string? ReadText(Func<string?> read) => RealApp.ReadOr(read, RealApp.NotReadable);

    /// <summary>
    /// The one control <paramref name="id"/> on screen, read through UI Automation: its name and its
    /// <c>AcceleratorKey</c> noted in run.txt beside U3's table's; returns a problem when the name is not
    /// the table's (the key is noted, as the report asks, not asserted). A read that fails gives
    /// <see cref="RealApp.NotReadable"/>, and so a problem, never an exception.
    /// </summary>
    private IEnumerable<string> ReadFixedName(RealApp app, string id)
    {
        var expected = U.FixedNames.Single(named => named.Id == id);
        var control = ShownOne(app, id);
        var name = ReadText(() => control.Name);
        var key = ReadText(() => RealApp.AcceleratorKeyOf(control));
        Note($"{id}: name \"{name}\", AcceleratorKey \"{key}\" (U3's table: \"{expected.Name}\", key {Quoted(expected.Key)})");
        return name == expected.Name ? [] : [$"{id} is named \"{name}\", not \"{expected.Name}\"."];
    }

    /// <summary>
    /// A row's button found on its row (<see cref="RowButton"/>), read through UI Automation as
    /// <see cref="ReadFixedName"/> reads a fixed name, never found by it: its name and key noted, a problem
    /// when the name is not U3's.
    /// </summary>
    private IEnumerable<string> ReadRowName(RealApp app, AutomationElement button, U.RowNamed expected)
    {
        var name = ReadText(() => button.Name);
        var key = ReadText(() => RealApp.AcceleratorKeyOf(button));
        Note($"{expected.Row}'s {expected.Id} in {expected.List}: name \"{name}\", AcceleratorKey \"{key}\" (U3's table: \"{expected.Name}\", no key)");
        return name == expected.Name ? [] : [$"{expected.Row}'s {expected.Id} in {expected.List} is named \"{name}\", not \"{expected.Name}\"."];
    }

    /// <summary><c>git status --porcelain</c> of the copy, without the optional locks, its lines joined by <c> | </c>.</summary>
    private static async Task<string> StatusAsync(TempRepo repo) =>
        string.Join(" | ", (await repo.GitAsync("--no-optional-locks", "status", "--porcelain", "--untracked-files=all")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r')));
}
