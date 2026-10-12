using System.Text;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.App.Tests.ResolverFixtures;

namespace VisualCommit.App.Tests;

/// <summary>
/// The conflict resolver's view model (docs/phase-4-design.md section 11, the report's part C)
/// with a recording fake of <see cref="IConflictActions"/>: opening a file, the sides' names,
/// every choice and the bars' states after it, typing and its parse, undo and redo, a whole side,
/// the counts, the files' navigation, Mark resolved with its questions, the cards' actions, the
/// merge tool, leaving and the refreshes while it is open. Nothing is written but through the
/// fake's writes. On the headless UI thread, as the app runs it.
/// </summary>
public class ConflictResolverViewModelTests
{
    private static readonly ConflictBlockBars Open = ConflictBlockBars.Open;
    private static readonly ConflictBlockBars OursUsed = new(ConflictSideState.Used, ConflictSideState.NotUsed);
    private static readonly ConflictBlockBars TheirsUsed = new(ConflictSideState.NotUsed, ConflictSideState.Used);
    private static readonly ConflictBlockBars BothUsed = new(ConflictSideState.Used, ConflictSideState.Used);
    private static readonly ConflictBlockBars NoneUsed = new(ConflictSideState.NotUsed, ConflictSideState.NotUsed);

    // --- Opening (11.1, 11.2, 11.3) ---------------------------------------------------------

    [AvaloniaFact]
    public async Task Opening_a_merge_s_file_shows_its_header_titles_panes_result_and_reaches_its_first_block()
    {
        var (actions, _, resolver) = MergeStop();
        var navigations = new List<ConflictNavigation>();
        resolver.NavigationRequested += (_, navigation) => navigations.Add(navigation);

        await resolver.OpenAsync("notes.txt");

        Assert.True(resolver.IsOpen);
        Assert.Equal(ConflictResolverBody.Text, resolver.Body);
        Assert.Equal(("U", "notes.txt", string.Empty, "Both modified", "File 1 of 1"), (resolver.StatusLetter, resolver.FileName, resolver.Folder, resolver.KindText, resolver.FilePositionText));
        Assert.False(resolver.HasOtherFiles);
        Assert.Equal(("Ours: main", "Theirs: feature/theirs"), (resolver.OursTitle, resolver.TheirsTitle));
        Assert.Equal(("Result", "2 conflicts left", "Conflict 1 of 2"), (resolver.ResultTitle, resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.Equal(Notes, Text(resolver));
        Assert.False(resolver.IsModified);
        Assert.False(resolver.IsParsePending);
        Assert.Equal([Open, Open], resolver.Bars);
        Assert.Equal(0, resolver.CurrentBlock);

        // The panes: the file with every block's ours side, and with its theirs side (PM).
        Assert.Equal(5, resolver.Parts.Count);
        Assert.Equal(["line 1"], resolver.Parts[0].Ours);
        Assert.True(resolver.Parts[1].IsBlock);
        Assert.Equal(["line 2 ours"], resolver.Parts[1].Ours);
        Assert.Equal(["line 2 theirs"], resolver.Parts[1].Theirs);
        Assert.Equal(["line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9"], resolver.Parts[2].Ours);
        Assert.True(resolver.Parts[3].IsBlock);
        Assert.Equal(["line 10 ours"], resolver.Parts[3].Ours);
        Assert.Equal(["line 10 theirs"], resolver.Parts[3].Theirs);

        // The first block is reached: the caret at the start of its < line, its bar and line in
        // view, the focus in the result.
        var reached = Assert.Single(navigations);
        Assert.Equal(new ConflictNavigation(LineStart(resolver, 2), 0, 2, FocusResult: true), reached);
        Assert.Equal(LineStart(resolver, 2), resolver.CaretOffset);

        // Toolbar: the merge tool is off without merge.tool; Take ours and Take theirs and Mark resolved work.
        Assert.True(resolver.ShowsConflictTools);
        Assert.True(resolver.ShowsMergeTool);
        Assert.False(resolver.RunMergeToolCommand.CanExecute(null));
        Assert.Equal("Set merge.tool in git's configuration to use a merge tool", resolver.MergeToolReason);
        Assert.True(resolver.TakeOursCommand.CanExecute(null));
        Assert.True(resolver.TakeTheirsCommand.CanExecute(null));
        Assert.Null(resolver.TakeOursReason);
        Assert.True(resolver.MarkResolvedCommand.CanExecute(null));
        Assert.True(resolver.IsInDoubleClickGuard);

        // Nothing written: only the file was read.
        Assert.Equal(["Read notes.txt"], actions.Calls);
    }

    [AvaloniaFact]
    public async Task A_folder_is_shown_and_the_files_are_counted_in_the_stage_list_s_order()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("docs/a.txt", Notes);
        actions.SetText("src/b.txt", Notes);
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge(), Entry("docs/a.txt"), Entry("src/b.txt")));

        await resolver.OpenAsync("src/b.txt");

        Assert.Equal(("b.txt", "src", true, "File 2 of 2"), (resolver.FileName, resolver.Folder, resolver.HasFolder, resolver.FilePositionText));
        Assert.True(resolver.HasOtherFiles);
        Assert.True(resolver.NextFileCommand.CanExecute(null));
    }

    // --- The sides' names (11.4) --------------------------------------------------------------

    [Theory]
    [InlineData(InProgressKind.Merge, "Merge branch 'feature/theirs'", null, "Ours: main", "Theirs: feature/theirs")]
    [InlineData(InProgressKind.Merge, "Merge remote-tracking branch 'origin/topic' into main", null, "Ours: main", "Theirs: origin/topic")]
    [InlineData(InProgressKind.Merge, "Merge commit '1234567'", "feature/label", "Ours: main", "Theirs: feature/label")]
    [InlineData(InProgressKind.Merge, "Merge commit '1234567'", null, "Ours: main", "Theirs: 5c77ffb")]
    [InlineData(InProgressKind.CherryPick, "Pick conflicting", null, "Ours: main", "Theirs: 5c77ffb Pick conflicting")]
    [InlineData(InProgressKind.Revert, "Two", null, "Ours: main", "Theirs: the revert of 5c77ffb Two")]
    [InlineData(InProgressKind.ApplyMailbox, null, null, "Ours: main", "Theirs: the patch")]
    [InlineData(InProgressKind.None, null, "Stashed changes", "Ours: main", "Theirs: the stash")]
    [InlineData(InProgressKind.None, null, "something else", "Ours: main", "Theirs: the other side")]
    public void The_sides_are_named_after_what_is_in_progress(InProgressKind kind, string? subject, string? theirsLabel, string ours, string theirs)
    {
        var state = kind == InProgressKind.None ? InProgressState.None : new InProgressState(kind, "5c77ffb0ec78abf986d121f42013eee14422e5a5", subject, null, null, 0, false);

        var (oursName, theirsName) = ConflictResolverTexts.SideNames(state, "main", null, theirsLabel);

        Assert.Equal((ours, theirs), (ConflictResolverTexts.OursTitle(oursName), ConflictResolverTexts.TheirsTitle(theirsName)));
    }

    [Fact]
    public void A_rebase_names_its_new_base_and_the_user_s_own_commit()
    {
        var state = new InProgressState(InProgressKind.Rebase, "70e8c78a00000000000000000000000000000000", "First change", "feature/rebase", "da31c97000000000000000000000000000000000", 1, false);

        Assert.Equal(("main (the new base)", "70e8c78 First change (your commit)"), ConflictResolverTexts.SideNames(state, "HEAD", "main", null));
        Assert.Equal(("da31c97 (the new base)", "70e8c78 First change (your commit)"), ConflictResolverTexts.SideNames(state, "HEAD", null, null));
    }

    [AvaloniaFact]
    public async Task A_merge_without_a_branch_in_its_message_names_theirs_by_the_file_s_marker()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("notes.txt", Notes);
        var state = new InProgressState(InProgressKind.Merge, "5c77ffb0ec78abf986d121f42013eee14422e5a5", "Merge commit '5c77ffb'", null, null, 0, false);
        var resolver = Resolver(actions, new ManualDelays(), Status(state, Entry("notes.txt")));

        await resolver.OpenAsync("notes.txt");

        Assert.Equal("Theirs: feature/theirs", resolver.TheirsTitle);
    }

    // --- Choosing per block, the bars, undo and redo (11.5, 14.3; C10) ------------------------

    [AvaloniaFact]
    public async Task C10_s_choices_undo_and_both_give_the_report_s_results_bars_counts_and_caret()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        var navigations = new List<ConflictNavigation>();
        resolver.NavigationRequested += (_, navigation) => navigations.Add(navigation);

        // Use ours on block 1: R1, a choice does not move on.
        resolver.Choose(0, ConflictChoice.UseOurs);
        Assert.Equal(R1, Text(resolver));
        Assert.True(resolver.IsModified);
        Assert.Equal(("1 conflict left", "Conflict 1 of 2"), (resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.Equal([OursUsed, Open], resolver.Bars);
        Assert.Equal(new ConflictNavigation(LineStart(resolver, 2), null, null, FocusResult: true), navigations[^1]);

        // The next conflict: block 2, at line 10 of R1.
        resolver.NextConflictCommand.Execute(null);
        Assert.Equal("Conflict 2 of 2", resolver.ConflictPositionText);
        Assert.Equal(new ConflictNavigation(LineStart(resolver, 10), 1, 10, FocusResult: false), navigations[^1]);

        // Use theirs on block 2: R2, nothing left.
        resolver.Choose(1, ConflictChoice.UseTheirs);
        Assert.Equal(R2, Text(resolver));
        Assert.Equal(("No conflicts left", "No conflicts left"), (resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.Equal([OursUsed, TheirsUsed], resolver.Bars);
        Assert.Equal(LineStart(resolver, 10), navigations[^1].CaretOffset);

        // The platform's undo: one step, R1 again, block 2 the block last reached.
        Assert.True(resolver.Undo());
        Assert.Equal(R1, Text(resolver));
        Assert.Equal(("1 conflict left", "Conflict 2 of 2"), (resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.Equal([OursUsed, Open], resolver.Bars);
        Assert.True(resolver.IsModified);

        // Use both from the ours pane: R3.
        resolver.Choose(1, ConflictChoice.UseBothOursFirst);
        Assert.Equal(R3, Text(resolver));
        Assert.Equal([OursUsed, BothUsed], resolver.Bars);
        Assert.Equal("No conflicts left", resolver.ResultCountText);

        // Nothing was written.
        Assert.Empty(actions.Writes);
    }

    [AvaloniaFact]
    public async Task Use_theirs_and_both_from_the_theirs_pane_and_undo_redo_follow_the_bars()
    {
        var (_, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");

        resolver.Choose(0, ConflictChoice.UseTheirs);
        Assert.StartsWith("line 1\nline 2 theirs\nline 3\n", Text(resolver), StringComparison.Ordinal);
        Assert.Equal([TheirsUsed, Open], resolver.Bars);

        resolver.Choose(1, ConflictChoice.UseBothTheirsFirst);
        Assert.EndsWith("line 9\nline 10 theirs\nline 10 ours\nline 11\nline 12\n", Text(resolver), StringComparison.Ordinal);
        Assert.Equal([TheirsUsed, BothUsed], resolver.Bars);

        Assert.True(resolver.Undo());
        Assert.True(resolver.Undo());
        Assert.Equal(Notes, Text(resolver));
        Assert.Equal([Open, Open], resolver.Bars);
        Assert.False(resolver.IsModified);
        Assert.False(resolver.CanUndo);
        Assert.False(resolver.Undo());

        Assert.True(resolver.Redo());
        Assert.Equal([TheirsUsed, Open], resolver.Bars);
        Assert.True(resolver.Redo());
        Assert.Equal([TheirsUsed, BothUsed], resolver.Bars);
        Assert.False(resolver.CanRedo);

        // A new choice after an undo replaces what the redo would have done.
        Assert.True(resolver.Undo());
        resolver.Choose(1, ConflictChoice.UseOurs);
        Assert.Equal([TheirsUsed, OursUsed], resolver.Bars);
        Assert.False(resolver.CanRedo);
    }

    [AvaloniaFact]
    public async Task A_choice_on_a_block_no_longer_in_the_result_does_nothing()
    {
        var (_, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);

        resolver.Choose(0, ConflictChoice.UseTheirs);
        resolver.Choose(7, ConflictChoice.UseTheirs);

        Assert.Equal(R1, Text(resolver));
        Assert.Equal([OursUsed, Open], resolver.Bars);
    }

    // --- Typing and its parse (11.5, 14.3; C12, C14) ------------------------------------------

    [AvaloniaFact]
    public async Task Typing_is_parsed_after_the_delay_and_a_block_whose_markers_typing_removed_says_not_used_on_both_bars()
    {
        var (_, delays, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        var document = resolver.Document!;

        // C14 step 2: lines 2 to 6 (block 1) replaced by a typed line.
        var start = document.GetLineByNumber(2).Offset;
        var end = document.GetLineByNumber(7).Offset;
        document.Replace(start, end - start, "line 2 edited by hand\n");

        Assert.True(resolver.IsParsePending);
        Assert.True(resolver.IsModified);
        Assert.Equal("2 conflicts left", resolver.ResultCountText);
        Assert.Equal(1, delays.Waiting);

        delays.Elapse();

        Assert.False(resolver.IsParsePending);
        Assert.Equal(("1 conflict left", "Conflict 1 of 2"), (resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.Equal([NoneUsed, Open], resolver.Bars);
    }

    [AvaloniaFact]
    public async Task Each_key_restarts_the_delay_and_only_the_last_one_parses()
    {
        var (_, delays, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        var document = resolver.Document!;

        document.Insert(0, "a");
        document.Insert(1, "b");

        Assert.Equal(1, delays.Waiting);
        delays.Elapse();
        Assert.False(resolver.IsParsePending);
        Assert.Equal("ab" + Notes, Text(resolver));
    }

    [AvaloniaFact]
    public async Task Typing_inside_a_chosen_text_keeps_its_choice_until_an_undo_brings_its_markers_back()
    {
        var (_, delays, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);

        // C12 step 6: End, then " and picked" typed after "line 2 ours".
        var document = resolver.Document!;
        document.Insert(document.GetLineByNumber(2).EndOffset, " and picked");
        delays.Elapse();

        Assert.Equal("line 2 ours and picked", document.GetText(document.GetLineByNumber(2)));
        Assert.Equal([OursUsed, Open], resolver.Bars);

        // Undo takes back the typing (one character per step in AvaloniaEdit), then the choice.
        while (resolver.Bars[0] == OursUsed && resolver.Undo())
        {
        }

        Assert.Equal(Notes, Text(resolver));
        Assert.Equal([Open, Open], resolver.Bars);
    }

    [AvaloniaFact]
    public async Task A_choice_right_after_typing_parses_the_typing_first()
    {
        var (_, delays, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        var document = resolver.Document!;
        document.Insert(0, "top\n");
        Assert.True(resolver.IsParsePending);

        resolver.Choose(1, ConflictChoice.UseTheirs);

        Assert.False(resolver.IsParsePending);
        Assert.Equal(0, delays.Waiting);
        Assert.EndsWith("line 9\nline 10 theirs\nline 11\nline 12\n", Text(resolver), StringComparison.Ordinal);
        Assert.StartsWith("top\nline 1\n<<<<<<< HEAD\n", Text(resolver), StringComparison.Ordinal);
        Assert.Equal([Open, TheirsUsed], resolver.Bars);
    }

    [AvaloniaFact]
    public async Task A_block_whose_sides_the_user_edited_keeps_its_buttons_matched_by_order()
    {
        var (_, delays, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        var document = resolver.Document!;
        document.Replace(document.GetLineByNumber(3).Offset, "line 2 ours".Length, "line 2 OURS");
        delays.Elapse();

        Assert.Equal([Open, Open], resolver.Bars);
        Assert.Equal("2 conflicts left", resolver.ResultCountText);

        // A choice on it takes the result's own lines, with the edit.
        resolver.Choose(0, ConflictChoice.UseOurs);
        Assert.StartsWith("line 1\nline 2 OURS\nline 3\n", Text(resolver), StringComparison.Ordinal);
        Assert.Equal([OursUsed, Open], resolver.Bars);
    }

    [AvaloniaFact]
    public async Task Two_identical_blocks_keep_their_own_bars_when_they_are_chosen_one_after_the_other()
    {
        var (_, _, resolver) = TwinStop();
        await resolver.OpenAsync("version.txt");
        var navigations = new List<ConflictNavigation>();
        resolver.NavigationRequested += (_, navigation) => navigations.Add(navigation);

        // Use ours on block 1: block 2's markers are still in the result, and its bars keep their buttons.
        resolver.Choose(0, ConflictChoice.UseOurs);
        Assert.Equal("x = 2\na\nb\nc\n" + Twin + "d\n", Text(resolver));
        Assert.Equal([OursUsed, Open], resolver.Bars);

        // The next conflict reaches block 2 in the panes and in the result.
        resolver.NextConflictCommand.Execute(null);
        Assert.Equal(new ConflictNavigation(LineStart(resolver, 5), 1, 5, FocusResult: false), navigations[^1]);

        // Use theirs on block 2 replaces block 2 and records it against block 2.
        resolver.Choose(1, ConflictChoice.UseTheirs);
        Assert.Equal("x = 2\na\nb\nc\nx = 3\nd\n", Text(resolver));
        Assert.Equal([OursUsed, TheirsUsed], resolver.Bars);

        // A choice on block 1, whose markers are gone, does nothing.
        resolver.Choose(0, ConflictChoice.UseTheirs);
        Assert.Equal("x = 2\na\nb\nc\nx = 3\nd\n", Text(resolver));

        // Bottom to top: the same.
        Assert.True(resolver.Undo());
        Assert.True(resolver.Undo());
        resolver.Choose(1, ConflictChoice.UseTheirs);
        Assert.Equal(Twin + "a\nb\nc\nx = 3\nd\n", Text(resolver));
        Assert.Equal([Open, TheirsUsed], resolver.Bars);
        resolver.Choose(0, ConflictChoice.UseOurs);
        Assert.Equal([OursUsed, TheirsUsed], resolver.Bars);
    }

    [AvaloniaFact]
    public async Task Typing_away_the_first_of_two_identical_blocks_leaves_the_second_its_bars()
    {
        var (_, delays, resolver) = TwinStop();
        await resolver.OpenAsync("version.txt");
        var document = resolver.Document!;

        document.Replace(0, Twin.Length, "x = 2\n");
        delays.Elapse();

        Assert.Equal([NoneUsed, Open], resolver.Bars);
        Assert.Equal("1 conflict left", resolver.ResultCountText);
        resolver.Choose(1, ConflictChoice.UseTheirs);
        Assert.Equal("x = 2\na\nb\nc\nx = 3\nd\n", Text(resolver));
        Assert.Equal([NoneUsed, TheirsUsed], resolver.Bars);
    }

    [AvaloniaFact]
    public async Task Typing_in_a_block_after_another_was_chosen_keeps_both_blocks_bars()
    {
        var (_, delays, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);

        // Block 2's ours line (line 11 of R1) edited by hand.
        var document = resolver.Document!;
        Assert.Equal("line 10 ours", document.GetText(document.GetLineByNumber(11)));
        document.Insert(document.GetLineByNumber(11).EndOffset, " and more");
        delays.Elapse();

        Assert.Equal([OursUsed, Open], resolver.Bars);
        Assert.Equal(("1 conflict left", "Conflict 1 of 2"), (resolver.ResultCountText, resolver.ConflictPositionText));

        // Its bar acts on it: the second block of the result is replaced.
        resolver.Choose(1, ConflictChoice.UseTheirs);
        Assert.Equal(R2, Text(resolver));
        Assert.Equal([OursUsed, TheirsUsed], resolver.Bars);
    }

    // --- A whole side (11.6; C14 step 7) --------------------------------------------------------

    [AvaloniaFact]
    public async Task Take_theirs_replaces_the_whole_result_as_one_step_with_every_block_as_if_theirs_were_chosen()
    {
        var (actions, delays, resolver) = MergeStop();
        actions.Sides[("notes.txt", ConflictSide.Theirs)] = Encoding.UTF8.GetBytes(Scenarios_NotesTheirs);
        await resolver.OpenAsync("notes.txt");
        var document = resolver.Document!;
        document.Replace(document.GetLineByNumber(2).Offset, document.GetLineByNumber(7).Offset - document.GetLineByNumber(2).Offset, "line 2 edited by hand\n");
        delays.Elapse();
        var typed = Text(resolver);

        await resolver.TakeTheirsCommand.ExecuteAsync(null);

        Assert.Equal(Scenarios_NotesTheirs, Text(resolver));
        Assert.Equal(("No conflicts left", "No conflicts left"), (resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.Equal([TheirsUsed, TheirsUsed], resolver.Bars);
        Assert.Contains("ReadSide notes.txt Theirs", actions.Calls);
        Assert.Empty(actions.Writes);

        Assert.True(resolver.Undo());
        Assert.Equal(typed, Text(resolver));
        Assert.Equal(("1 conflict left", "Conflict 1 of 2"), (resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.Equal([NoneUsed, Open], resolver.Bars);
    }

    [AvaloniaFact]
    public async Task Take_ours_uses_ours_side_and_a_side_the_conflict_lacks_is_disabled_with_its_reason()
    {
        var (actions, _, resolver) = MergeStop();
        actions.Sides[("notes.txt", ConflictSide.Ours)] = Encoding.UTF8.GetBytes(Scenarios_NotesOurs);
        await resolver.OpenAsync("notes.txt");

        await resolver.TakeOursCommand.ExecuteAsync(null);
        Assert.Equal(Scenarios_NotesOurs, Text(resolver));
        Assert.Equal([OursUsed, OursUsed], resolver.Bars);

        var lacking = new RecordingConflictActions();
        lacking.SetText("notes.txt", Notes);
        var entry = Entry("notes.txt") with { OursId = Zero, OursMode = "000000" };
        var other = Resolver(lacking, new ManualDelays(), Status(Merge(), entry));
        await other.OpenAsync("notes.txt");
        Assert.False(other.TakeOursCommand.CanExecute(null));
        Assert.Equal("This side deleted the file", other.TakeOursReason);
        Assert.True(other.TakeTheirsCommand.CanExecute(null));
    }

    // --- Mark resolved (11.7, 7.2) ---------------------------------------------------------------

    [AvaloniaFact]
    public async Task Mark_resolved_writes_the_result_as_read_and_closes_when_no_conflicted_file_is_left()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);
        resolver.Choose(1, ConflictChoice.UseBothOursFirst);
        var closed = 0;
        resolver.Closed += (_, _) => closed++;
        actions.BeforeWriteEnds = path => resolver.Update(Status(Merge()), "main", null);

        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        Assert.Empty(actions.Questions);
        Assert.Equal(["MarkResolved notes.txt"], actions.Writes);
        Assert.Equal(Encoding.UTF8.GetBytes(R3), actions.Written["notes.txt"]);
        Assert.Equal(1, closed);
        Assert.False(resolver.IsOpen);
        Assert.Equal(ConflictResolverBody.None, resolver.Body);
    }

    [AvaloniaFact]
    public async Task Mark_resolved_with_blocks_left_asks_and_cancel_writes_nothing()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        actions.Answer(false);

        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        var question = Assert.Single(actions.Questions);
        Assert.Equal(("notes.txt still has conflict markers.", "Mark it resolved anyway? The markers are committed with it.", string.Empty, "Mark resolved", false, "Cancel"),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive, question.CancelText));
        Assert.Empty(actions.Writes);
        Assert.True(resolver.IsOpen);

        actions.Answer(true);
        await resolver.MarkResolvedCommand.ExecuteAsync(null);
        Assert.Equal(["MarkResolved notes.txt"], actions.Writes);
        Assert.Equal(Encoding.UTF8.GetBytes(Notes), actions.Written["notes.txt"]);
    }

    [AvaloniaFact]
    public async Task Mark_resolved_asks_when_a_single_marker_line_is_left_as_staging_does()
    {
        var (actions, delays, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(1, ConflictChoice.UseTheirs);

        // Block 1's =, theirs and > lines deleted by hand: its < line is left.
        var document = resolver.Document!;
        Assert.Equal("=======", document.GetText(document.GetLineByNumber(4)));
        document.Remove(document.GetLineByNumber(4).Offset, document.GetLineByNumber(7).Offset - document.GetLineByNumber(4).Offset);
        delays.Elapse();
        Assert.Equal("No conflicts left", resolver.ResultCountText);
        actions.Answer(false);

        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        Assert.Equal("notes.txt still has conflict markers.", Assert.Single(actions.Questions).Title);
        Assert.Empty(actions.Writes);
    }

    [AvaloniaFact]
    public async Task Mark_resolved_over_a_file_changed_on_disk_asks_and_overwrites_only_when_told()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);
        resolver.Choose(1, ConflictChoice.UseOurs);
        actions.SetText("notes.txt", "changed by a terminal\n");
        actions.Answer(false);

        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        var question = Assert.Single(actions.Questions);
        Assert.Equal(("notes.txt changed on disk.", "Overwrite it with the result?", string.Empty, "Overwrite", true, "Cancel"),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive, question.CancelText));
        Assert.Empty(actions.Writes);

        actions.Answer(true);
        await resolver.MarkResolvedCommand.ExecuteAsync(null);
        Assert.Equal(["MarkResolved notes.txt"], actions.Writes);
    }

    [AvaloniaFact]
    public async Task A_refresh_while_the_markers_question_is_open_neither_replaces_the_result_nor_skips_the_changed_on_disk_question()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        var document = resolver.Document;

        // While the markers question is open, an editor saves the file and a refresh comes.
        actions.WhileAsking = question =>
        {
            if (question.Title == "notes.txt still has conflict markers.")
            {
                actions.SetText("notes.txt", "resolved in an editor\n");
                resolver.Update(Status(Merge(), Entry("notes.txt")), "main", null);
                Settle();
                Assert.Same(document, resolver.Document);
            }
        };
        actions.Answer(true, false);

        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        // The operation found the file changed and wrote nothing; Cancel: the unmodified result is read again.
        Assert.Equal(["notes.txt still has conflict markers.", "notes.txt changed on disk."], actions.Questions.Select(question => question.Title));
        Assert.Empty(actions.Writes);
        Assert.Contains("Refused notes.txt: changed on disk", actions.Calls);
        Settle();
        Assert.Equal("resolved in an editor\n", Text(resolver));
        Assert.False(resolver.IsModified);
        Assert.False(resolver.IsBusy);
    }

    [AvaloniaFact]
    public async Task Overwrite_after_a_change_on_disk_writes_the_click_s_text_over_the_file_as_the_operation_found_it()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);
        resolver.Choose(1, ConflictChoice.UseBothOursFirst);
        var closed = false;
        resolver.Closed += (_, _) => closed = true;

        // The write waits in the queue; meanwhile a terminal changes the file.
        var hold = actions.Hold = new TaskCompletionSource();
        var marking = resolver.MarkResolvedCommand.ExecuteAsync(null);
        Settle();
        Assert.True(resolver.IsBusy);
        actions.SetText("notes.txt", "changed by a terminal\n");
        actions.Hold = null;
        actions.BeforeWriteEnds = _ => resolver.Update(Status(Merge()), "main", null);
        actions.Answer(true);
        hold.SetResult();
        await marking;

        Assert.Equal("notes.txt changed on disk.", Assert.Single(actions.Questions).Title);
        Assert.Equal(["Refused notes.txt: changed on disk", "MarkResolved notes.txt"], actions.Calls.Where(call => call.StartsWith("Refused", StringComparison.Ordinal) || call.StartsWith("MarkResolved", StringComparison.Ordinal)));
        Assert.Equal(Encoding.UTF8.GetBytes(R3), actions.Written["notes.txt"]);
        Assert.True(closed);
    }

    [AvaloniaFact]
    public async Task A_file_that_was_missing_when_read_and_is_still_missing_is_written_without_asking()
    {
        var actions = new RecordingConflictActions();
        actions.Contents["gone.txt"] = new ConflictContent([], 7, false, false, false, WorkingTreeExists: false);
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge(), Entry("gone.txt")));
        await resolver.OpenAsync("gone.txt");
        resolver.Document!.Insert(0, "written\n");

        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        Assert.Empty(actions.Questions);
        Assert.Equal("written\n"u8.ToArray(), actions.Written["gone.txt"]);
    }

    [AvaloniaFact]
    public async Task The_file_is_written_in_the_encoding_and_with_the_line_ends_it_was_read_with()
    {
        var actions = new RecordingConflictActions();
        var latin1 = Encoding.Latin1.GetBytes("café\r\n<<<<<<< HEAD\r\nours é\r\n=======\r\ntheirs\r\n>>>>>>> other\r\nend\r\n");
        actions.Contents["latin.txt"] = new ConflictContent(latin1, 7, false, false, false, true);
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge(), Entry("latin.txt")));
        await resolver.OpenAsync("latin.txt");

        resolver.Choose(0, ConflictChoice.UseOurs);
        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        Assert.Equal(Encoding.Latin1.GetBytes("café\r\nours é\r\nend\r\n"), actions.Written["latin.txt"]);
    }

    [AvaloniaFact]
    public async Task A_side_taken_whole_is_written_byte_for_byte_whatever_encoding_the_file_with_markers_was_read_in()
    {
        // Ours is Latin-1 ("café" as E9), theirs UTF-8 with an arrow: the file with markers mixes
        // them, is not valid UTF-8 and is read as Latin-1.
        var ours = Encoding.Latin1.GetBytes("café\nours\n");
        var theirs = Encoding.UTF8.GetBytes("café → done\ntheirs\n");
        var marked = (byte[])[.. "<<<<<<< HEAD\n"u8, .. ours, .. "=======\n"u8, .. theirs, .. ">>>>>>> feature\n"u8];
        var actions = new RecordingConflictActions();
        actions.Contents["mixed.txt"] = new ConflictContent(marked, 7, false, false, false, true);
        actions.Sides[("mixed.txt", ConflictSide.Ours)] = ours;
        actions.Sides[("mixed.txt", ConflictSide.Theirs)] = theirs;
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge(), Entry("mixed.txt")));
        await resolver.OpenAsync("mixed.txt");

        await resolver.TakeTheirsCommand.ExecuteAsync(null);
        Assert.Equal("café → done\ntheirs\n", Text(resolver));
        await resolver.MarkResolvedCommand.ExecuteAsync(null);
        Assert.Equal(theirs, actions.Written["mixed.txt"]);

        // A file with markers that is valid UTF-8, and a side that is not: the side's bytes too.
        var utf8 = new RecordingConflictActions();
        utf8.SetText("other.txt", "<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> feature\ncafé\n");
        var latin = Encoding.Latin1.GetBytes("ours\ncafé\n");
        utf8.Sides[("other.txt", ConflictSide.Ours)] = latin;
        var other = Resolver(utf8, new ManualDelays(), Status(Merge(), Entry("other.txt")));
        await other.OpenAsync("other.txt");
        await other.TakeOursCommand.ExecuteAsync(null);
        await other.MarkResolvedCommand.ExecuteAsync(null);
        Assert.Equal(latin, utf8.Written["other.txt"]);
    }

    [AvaloniaFact]
    public async Task A_side_taken_whole_and_then_edited_is_written_in_the_encoding_the_file_was_read_in()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("other.txt", "<<<<<<< HEAD\nours\n=======\ntheirs\n>>>>>>> feature\ncafé\n");
        actions.Sides[("other.txt", ConflictSide.Ours)] = Encoding.Latin1.GetBytes("ours\ncafé\n");
        var delays = new ManualDelays();
        var resolver = Resolver(actions, delays, Status(Merge(), Entry("other.txt")));
        await resolver.OpenAsync("other.txt");
        await resolver.TakeOursCommand.ExecuteAsync(null);

        resolver.Document!.Insert(0, "top\n");
        delays.Elapse();
        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        Assert.Equal(Encoding.UTF8.GetBytes("top\nours\ncafé\n"), actions.Written["other.txt"]);
    }

    [AvaloniaFact]
    public async Task While_a_write_runs_the_result_is_read_only_and_the_resolver_moves_on_to_the_next_file_when_it_ends()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("a.txt", Notes);
        actions.SetText("b.txt", Notes);
        actions.SetText("c.txt", Notes);
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge(), Entry("a.txt"), Entry("b.txt"), Entry("c.txt")));
        await resolver.OpenAsync("c.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);
        resolver.Choose(1, ConflictChoice.UseOurs);
        actions.Hold = new TaskCompletionSource();
        actions.BeforeWriteEnds = _ => resolver.Update(Status(Merge(), Entry("a.txt"), Entry("b.txt")), "main", null);

        var marking = resolver.MarkResolvedCommand.ExecuteAsync(null);
        Settle();

        Assert.True(resolver.IsBusy);
        Assert.False(resolver.IsIdle);
        Assert.False(resolver.CanUndo);
        Assert.False(resolver.Undo());
        Assert.False(resolver.TakeOursCommand.CanExecute(null));
        Assert.False(resolver.NextFileCommand.CanExecute(null));
        resolver.Choose(0, ConflictChoice.UseTheirs);
        Assert.Single(actions.Writes);

        actions.Release();
        await marking;

        // c.txt was the last of the list: the next one, wrapping, is a.txt.
        Assert.False(resolver.IsBusy);
        Assert.Equal(("a.txt", "File 1 of 2"), (resolver.Path, resolver.FilePositionText));
        Assert.Equal(Notes, Text(resolver));
        Assert.False(resolver.IsModified);
    }

    [AvaloniaFact]
    public async Task A_write_that_fails_leaves_the_resolver_on_the_file_with_its_result()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);
        actions.WritesResolve = false;
        actions.Answer(true);

        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        Assert.True(resolver.IsOpen);
        Assert.Equal(R1, Text(resolver));
        Assert.True(resolver.IsModified);
        Assert.False(resolver.IsBusy);
    }

    // --- The files' navigation (11.2) ----------------------------------------------------------

    [AvaloniaFact]
    public async Task The_header_goes_to_the_previous_and_next_files_wrapping_and_asks_first_for_a_modified_result()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("a.txt", Notes);
        actions.SetText("b.txt", Notes);
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge(), Entry("a.txt"), Entry("b.txt")));
        await resolver.OpenAsync("a.txt");

        await resolver.PreviousFileCommand.ExecuteAsync(null);
        Assert.Equal(("b.txt", "File 2 of 2"), (resolver.Path, resolver.FilePositionText));
        await resolver.NextFileCommand.ExecuteAsync(null);
        Assert.Equal("a.txt", resolver.Path);
        Assert.Empty(actions.Questions);

        resolver.Choose(0, ConflictChoice.UseOurs);
        actions.Answer(false);
        await resolver.NextFileCommand.ExecuteAsync(null);
        Assert.Equal(("a.txt", true), (resolver.Path, resolver.IsModified));
        var question = Assert.Single(actions.Questions);
        Assert.Equal(("Discard your changes to a.txt?", "The result has not been marked resolved.", string.Empty, "Discard", true, "Keep editing"),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive, question.CancelText));

        actions.Answer(true);
        await resolver.NextFileCommand.ExecuteAsync(null);
        Assert.Equal(("b.txt", false), (resolver.Path, resolver.IsModified));
        Assert.Empty(actions.Writes);
    }

    // --- The cards (11.8; C15) ---------------------------------------------------------------------

    [AvaloniaFact]
    public async Task C15_s_cards_resolve_each_file_at_once_and_move_on_in_the_stage_list_s_order()
    {
        var actions = new RecordingConflictActions();
        actions.Contents["image.png"] = new ConflictContent([0x89, 0x50, 0x00], 7, true, true, false, true);
        actions.SetText("new.txt", Testing.Scenarios.ConflictsFiles.NewKindsConflicted);
        actions.Contents["other.txt"] = new ConflictContent(Encoding.UTF8.GetBytes("other file\nchanged on main\n"), 7, false, false, false, true);
        var image = Entry("image.png");
        var added = Entry("new.txt", ConflictKind.BothAdded);
        var other = Entry("other.txt", ConflictKind.DeletedByThem);
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge("feature/kinds"), image, added, other));
        var left = new List<ConflictEntry> { image, added, other };
        actions.BeforeWriteEnds = path =>
        {
            left.RemoveAll(entry => entry.Path == path);
            resolver.Update(Status(Merge("feature/kinds"), [.. left]), "main", null);
        };

        await resolver.OpenAsync("other.txt");

        Assert.Equal(ConflictResolverBody.Card, resolver.Body);
        Assert.Equal(("other.txt", "Deleted by them", "File 3 of 3"), (resolver.FileName, resolver.KindText, resolver.FilePositionText));
        Assert.Equal("main changed other.txt; feature/kinds deleted it.", resolver.CardText);
        Assert.Equal([("Keep the file", "ConflictKeepFile"), ("Delete it", "ConflictDeleteFile")], resolver.CardButtons.Select(button => (button.Label, button.AutomationId)));
        Assert.False(resolver.ShowsConflictTools);
        Assert.False(resolver.ShowsMergeTool);
        Assert.Null(resolver.Document);
        Assert.False(resolver.IsModified);

        await resolver.CardButtons[0].RunCommand.ExecuteAsync(null);

        Assert.Equal(["TakeSide other.txt Ours"], actions.Writes);
        Assert.Equal(("image.png", "Both modified", "File 1 of 2"), (resolver.FileName, resolver.KindText, resolver.FilePositionText));
        Assert.Equal("image.png is a binary file: its two versions cannot be merged line by line.", resolver.CardText);
        Assert.Equal([("Use ours", "ConflictUseOursFile"), ("Use theirs", "ConflictUseTheirsFile")], resolver.CardButtons.Select(button => (button.Label, button.AutomationId)));
        Assert.False(resolver.ShowsConflictTools);
        Assert.True(resolver.ShowsMergeTool);
        Assert.False(resolver.RunMergeToolCommand.CanExecute(null));

        // The card that opens asks for the focus on its first button.
        resolver.ResultFocusPending = false;
        await resolver.OpenAsync("image.png");
        Assert.True(resolver.ResultFocusPending);

        await resolver.CardButtons[1].RunCommand.ExecuteAsync(null);

        Assert.Equal(["TakeSide other.txt Ours", "TakeSide image.png Theirs"], actions.Writes);
        Assert.Equal(("new.txt", "Both added", "File 1 of 1", ConflictResolverBody.Text), (resolver.FileName, resolver.KindText, resolver.FilePositionText, resolver.Body));
        Assert.Equal(("Ours: main", "Theirs: feature/kinds", "1 conflict left", "Conflict 1 of 1"), (resolver.OursTitle, resolver.TheirsTitle, resolver.ResultCountText, resolver.ConflictPositionText));
        Assert.True(resolver.TakeOursCommand.CanExecute(null));
        Assert.True(resolver.TakeTheirsCommand.CanExecute(null));

        resolver.Choose(0, ConflictChoice.UseBothOursFirst);
        Assert.Equal("new file\nadded on main\nadded on feature/kinds\n", Text(resolver));
        await resolver.MarkResolvedCommand.ExecuteAsync(null);

        Assert.Equal(["TakeSide other.txt Ours", "TakeSide image.png Theirs", "MarkResolved new.txt"], actions.Writes);
        Assert.Empty(actions.Questions);
        Assert.False(resolver.IsOpen);
    }

    [AvaloniaTheory]
    [InlineData(ConflictKind.DeletedByUs, false, false, false, "main deleted f.txt; feature/theirs changed it.", "Keep the file|Delete it", "TakeSide f.txt Theirs")]
    [InlineData(ConflictKind.AddedByUs, false, false, false, "f.txt exists only in main.", "Keep it|Delete it", "TakeSide f.txt Ours")]
    [InlineData(ConflictKind.AddedByThem, false, false, false, "f.txt exists only in feature/theirs.", "Keep it|Delete it", "TakeSide f.txt Theirs")]
    [InlineData(ConflictKind.BothDeleted, false, false, false, "Both sides deleted f.txt.", "Delete it", "Remove f.txt")]
    [InlineData(ConflictKind.BothModified, true, false, false, "f.txt is a symbolic link changed on both sides.", "Use ours|Use theirs", "TakeSide f.txt Ours")]
    [InlineData(ConflictKind.BothModified, false, true, false, "f.txt is a submodule: resolve it in a terminal.", "", null)]
    [InlineData(ConflictKind.BothAdded, false, false, true, "f.txt is a binary file: its two versions cannot be merged line by line.", "Use ours|Use theirs", "TakeSide f.txt Ours")]
    public async Task Every_kind_without_text_has_its_card_and_its_first_button_runs_its_operation(ConflictKind kind, bool link, bool submodule, bool binary, string text, string buttons, string? first)
    {
        var actions = new RecordingConflictActions();
        actions.Contents["f.txt"] = new ConflictContent([], 7, binary, false, link, false);
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge(), Entry("f.txt", kind, submodule)));

        await resolver.OpenAsync("f.txt");

        Assert.Equal(ConflictResolverBody.Card, resolver.Body);
        Assert.Equal(text, resolver.CardText);
        Assert.Equal(buttons, string.Join('|', resolver.CardButtons.Select(button => button.Label)));
        Assert.False(resolver.ShowsConflictTools);
        Assert.Equal(binary, resolver.ShowsMergeTool);
        if (first is not null)
        {
            await resolver.CardButtons[0].RunCommand.ExecuteAsync(null);
            Assert.Equal([first], actions.Writes);
            Assert.False(resolver.IsOpen);
        }
    }

    [AvaloniaFact]
    public async Task A_file_moved_aside_for_a_folder_has_its_own_card()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("p~HEAD", "file content\n");
        var moved = Entry("p~HEAD", ConflictKind.AddedByUs);
        var status = Status(Merge(), moved) with
        {
            Staged = [new ChangedFile("p/inside.txt", FileChangeKind.Added)],
        };
        var resolver = Resolver(actions, new ManualDelays(), status);

        await resolver.OpenAsync("p~HEAD");

        Assert.Equal("Git moved p~HEAD aside because feature/theirs has a folder named p: rename or remove it, then mark it resolved.", resolver.CardText);
        var button = Assert.Single(resolver.CardButtons);
        Assert.Equal(("Mark resolved", "ConflictCardMarkResolved"), (button.Label, button.AutomationId));
        await button.RunCommand.ExecuteAsync(null);
        Assert.Equal(["TakeSide p~HEAD Ours"], actions.Writes);
    }

    [AvaloniaTheory]
    [InlineData("p~HEAD", ConflictKind.DeletedByThem, "Git moved p~HEAD aside because feature/theirs has a folder named p: rename or remove it, then mark it resolved.", "TakeSide p~HEAD Ours")]
    [InlineData("p~feature_theirs", ConflictKind.DeletedByUs, "Git moved p~feature_theirs aside because main has a folder named p: rename or remove it, then mark it resolved.", "TakeSide p~feature_theirs Theirs")]
    public async Task A_file_moved_aside_that_git_reports_as_modify_delete_has_the_moved_aside_card(string path, ConflictKind kind, string text, string write)
    {
        // Git 2.36: ours modified p, theirs replaced it with the folder p/: "UD p~HEAD" (14.2); the
        // label of a branch has its slashes made underscores.
        var actions = new RecordingConflictActions();
        actions.SetText(path, "file content\n");
        var status = Status(Merge(), Entry(path, kind)) with
        {
            Staged = [new ChangedFile("p/x", FileChangeKind.Added)],
        };
        var resolver = Resolver(actions, new ManualDelays(), status);

        await resolver.OpenAsync(path);

        Assert.Equal((ConflictCardKind.MovedAside, text), (resolver.CardKind, resolver.CardText));
        var button = Assert.Single(resolver.CardButtons);
        Assert.Equal(("Mark resolved", "ConflictCardMarkResolved"), (button.Label, button.AutomationId));
        await button.RunCommand.ExecuteAsync(null);
        Assert.Equal([write], actions.Writes);

        // Without a folder of that name it is the deletion's card.
        var plain = Resolver(actions, new ManualDelays(), Status(Merge(), Entry(path, kind)));
        await plain.OpenAsync(path);
        Assert.Equal(kind == ConflictKind.DeletedByThem ? ConflictCardKind.DeletedByThem : ConflictCardKind.DeletedByUs, plain.CardKind);
    }

    // --- The merge tool (11.9; C16) ------------------------------------------------------------

    [AvaloniaFact]
    public async Task The_merge_tool_asks_for_a_modified_result_and_keep_editing_does_not_start_it()
    {
        var (actions, _, resolver) = MergeStop();
        resolver.MergeTool = "fake";
        await resolver.OpenAsync("notes.txt");
        Assert.True(resolver.RunMergeToolCommand.CanExecute(null));
        Assert.Null(resolver.MergeToolReason);
        resolver.Choose(0, ConflictChoice.UseOurs);
        actions.Answer(false);

        await resolver.RunMergeToolCommand.ExecuteAsync(null);

        var question = Assert.Single(actions.Questions);
        Assert.Equal(("Discard your changes to notes.txt?", "The merge tool starts from the file as it is on disk.", string.Empty, "Discard", true, "Keep editing"),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive, question.CancelText));
        Assert.Empty(actions.Writes);
        Assert.Equal(R1, Text(resolver));
    }

    [AvaloniaFact]
    public async Task The_merge_tool_runs_with_the_result_read_only_and_the_resolver_closes_when_git_staged_the_file()
    {
        var (actions, _, resolver) = MergeStop();
        resolver.MergeTool = "fake";
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);
        actions.Hold = new TaskCompletionSource();
        actions.BeforeWriteEnds = _ => resolver.Update(Status(Merge()), "main", null);
        var closed = false;
        resolver.Closed += (_, _) => closed = true;

        var running = resolver.RunMergeToolCommand.ExecuteAsync(null);
        Settle();

        Assert.True(resolver.IsBusy);
        Assert.Equal("Waiting for fake to close…", resolver.ResultTitle);
        Assert.False(resolver.MarkResolvedCommand.CanExecute(null));

        actions.Release();
        await running;

        Assert.Equal(["RunMergeTool notes.txt"], actions.Writes);
        Assert.True(closed);
    }

    [AvaloniaFact]
    public async Task A_tool_that_closes_without_resolving_leaves_the_result_read_again_from_the_file()
    {
        var (actions, _, resolver) = MergeStop();
        resolver.MergeTool = "fake";
        await resolver.OpenAsync("notes.txt");
        actions.WritesResolve = false;
        actions.SetText("notes.txt", "written by the tool\n<<<<<<< HEAD\na\n=======\nb\n>>>>>>> x\n");

        await resolver.RunMergeToolCommand.ExecuteAsync(null);

        Assert.True(resolver.IsOpen);
        Assert.Equal("written by the tool\n<<<<<<< HEAD\na\n=======\nb\n>>>>>>> x\n", Text(resolver));
        Assert.False(resolver.IsModified);
        Assert.Equal(("Result", "1 conflict left"), (resolver.ResultTitle, resolver.ResultCountText));
    }

    [AvaloniaFact]
    public async Task A_resolver_opened_while_the_file_menu_s_merge_tool_runs_on_the_file_waits_for_it_read_only()
    {
        var (actions, _, resolver) = MergeStop();
        resolver.MergeTool = "fake";
        var tool = new TaskCompletionSource<bool>();
        actions.RunningMergeTool = new MergeToolRun("notes.txt", "meld", tool.Task);
        var closed = false;
        resolver.Closed += (_, _) => closed = true;

        await resolver.OpenAsync("notes.txt");

        // 11.9: read-only, waiting for the tool, as when the resolver started it itself.
        Assert.Equal((true, "Waiting for meld to close…"), (resolver.IsBusy, resolver.ResultTitle));
        Assert.False(resolver.MarkResolvedCommand.CanExecute(null));
        Assert.False(resolver.RunMergeToolCommand.CanExecute(null));
        Assert.False(resolver.CloseResolverCommand.CanExecute(null));
        resolver.Choose(0, ConflictChoice.UseOurs);
        Assert.Equal(Notes, Text(resolver));
        await resolver.MarkResolvedCommand.ExecuteAsync(null);
        Assert.Empty(actions.Writes);

        // The tool staged the file: the resolver moves on, here closes.
        actions.RunningMergeTool = null;
        resolver.Update(Status(Merge()), "main", null);
        tool.SetResult(true);
        Settle();
        Assert.True(closed);
    }

    [AvaloniaFact]
    public async Task A_merge_tool_that_starts_on_the_open_file_makes_it_wait_and_a_tool_that_did_not_resolve_leaves_it_read_again()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        Assert.False(resolver.IsBusy);

        // The file menu's tool, queued before the resolver opened, starts now.
        var tool = new TaskCompletionSource<bool>();
        actions.StartMergeTool("notes.txt", "meld", tool.Task);
        Assert.Equal((true, "Waiting for meld to close…"), (resolver.IsBusy, resolver.ResultTitle));

        // It closes without resolving, having written the file: the result is read from it.
        actions.RunningMergeTool = null;
        actions.SetText("notes.txt", "written by the tool\n");
        tool.SetResult(false);
        Settle();
        Assert.Equal((false, "Result", "written by the tool\n"), (resolver.IsBusy, resolver.ResultTitle, Text(resolver)));
        Assert.Empty(actions.Writes);

        // A tool on another file leaves the resolver as it is.
        actions.StartMergeTool("other.txt", "meld", new TaskCompletionSource<bool>().Task);
        Assert.False(resolver.IsBusy);
    }

    [AvaloniaFact]
    public async Task A_modified_result_is_kept_when_a_tool_the_resolver_did_not_start_ends()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);

        var tool = new TaskCompletionSource<bool>();
        actions.StartMergeTool("notes.txt", "meld", tool.Task);
        Assert.True(resolver.IsBusy);
        actions.RunningMergeTool = null;
        actions.SetText("notes.txt", "written by the tool\n");
        tool.SetResult(false);
        Settle();

        Assert.Equal((false, R1, true), (resolver.IsBusy, Text(resolver), resolver.IsModified));
    }

    // --- Leaving and closing (11.1, 11.10) -----------------------------------------------------------

    [AvaloniaFact]
    public async Task Leaving_asks_only_for_a_modified_result_and_closing_says_so()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        var closed = 0;
        resolver.Closed += (_, _) => closed++;

        Assert.True(await resolver.TryLeaveAsync());
        Assert.Empty(actions.Questions);

        resolver.Choose(0, ConflictChoice.UseOurs);
        actions.Answer(false, true);
        Assert.False(await resolver.TryLeaveAsync());
        Assert.True(await resolver.TryLeaveAsync());
        Assert.Equal(2, actions.Questions.Count);
        Assert.Equal(0, closed);

        actions.Answer(false);
        await resolver.CloseResolverCommand.ExecuteAsync(null);
        Assert.Equal(0, closed);
        Assert.True(resolver.IsOpen);

        actions.Answer(true);
        await resolver.CloseResolverCommand.ExecuteAsync(null);
        Assert.Equal(1, closed);
        Assert.False(resolver.IsOpen);
        Assert.Empty(actions.Writes);
    }

    // --- Refreshes while it is open (11.11) -------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_refresh_reads_an_unmodified_result_again_only_when_the_file_changed()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        var document = resolver.Document;
        resolver.Choose(0, ConflictChoice.UseOurs);
        resolver.Undo();

        resolver.Update(Status(Merge(), Entry("notes.txt")), "main", null);
        Settle();
        Assert.Same(document, resolver.Document);
        Assert.True(resolver.CanRedo);

        actions.SetText("notes.txt", R1);
        resolver.ResultFocusPending = false;
        var navigations = new List<ConflictNavigation>();
        resolver.NavigationRequested += (_, navigation) => navigations.Add(navigation);
        resolver.Update(Status(Merge(), Entry("notes.txt")), "main", null);
        Settle();
        Assert.NotSame(document, resolver.Document);

        // Read again on a refresh, the file's first block is reached without taking the focus.
        Assert.False(Assert.Single(navigations).FocusResult);
        Assert.False(resolver.ResultFocusPending);
        Assert.Equal(R1, Text(resolver));
        Assert.False(resolver.CanUndo);
        Assert.Equal("1 conflict left", resolver.ResultCountText);
    }

    [AvaloniaFact]
    public async Task A_refresh_keeps_a_modified_result_as_it_is()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);
        actions.SetText("notes.txt", "changed\n");
        var reads = actions.Calls.Count;

        resolver.Update(Status(Merge(), Entry("notes.txt")), "main", null);
        Settle();

        Assert.Equal(R1, Text(resolver));
        Assert.Equal(reads, actions.Calls.Count);
    }

    [AvaloniaFact]
    public async Task A_file_resolved_elsewhere_moves_on_unless_its_result_is_modified()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("a.txt", Notes);
        actions.SetText("b.txt", Notes);
        var resolver = Resolver(actions, new ManualDelays(), Status(Merge(), Entry("a.txt"), Entry("b.txt")));
        await resolver.OpenAsync("a.txt");

        resolver.Update(Status(Merge(), Entry("b.txt")), "main", null);
        Settle();
        Assert.Equal(("b.txt", "File 1 of 1", "Result"), (resolver.Path, resolver.FilePositionText, resolver.ResultTitle));

        resolver.Choose(0, ConflictChoice.UseOurs);
        resolver.Update(Status(Merge()), "main", null);
        Settle();
        Assert.Equal(("b.txt", true, "Marked resolved elsewhere"), (resolver.Path, resolver.IsResolvedElsewhere, resolver.ResultTitle));
        Assert.True(resolver.IsOpen);

        actions.Answer(false);
        Assert.False(await resolver.TryLeaveAsync());
    }

    [AvaloniaFact]
    public async Task The_end_of_the_operation_closes_the_resolver_without_asking()
    {
        var (actions, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        resolver.Choose(0, ConflictChoice.UseOurs);
        var closed = false;
        resolver.Closed += (_, _) => closed = true;

        resolver.Update(Status(InProgressState.None), "main", null);

        Assert.True(closed);
        Assert.False(resolver.IsOpen);
        Assert.Empty(actions.Questions);
    }

    [AvaloniaFact]
    public async Task The_header_s_menu_is_the_repository_s_menu_of_the_conflicted_file()
    {
        var (_, _, resolver) = MergeStop();
        await resolver.OpenAsync("notes.txt");
        ChangedFile? asked = null;
        resolver.FileMenu = file =>
        {
            asked = file;
            return null;
        };

        resolver.FileMenu(new ChangedFile(resolver.Path, FileChangeKind.Conflicted));

        Assert.Equal(new ChangedFile("notes.txt", FileChangeKind.Conflicted), asked);
    }

    // --- The menus' data (11.12, 14.3) ---------------------------------------------------------------------

    [Fact]
    public void The_result_s_menu_has_undo_redo_then_the_clipboard_with_14_3_s_enabling()
    {
        var actions = new ConflictResultMenuActions(() => { }, () => { }, () => { }, () => { }, () => { }, () => { });

        var nothing = ConflictMenus.ForConflictResult("notes.txt", new ConflictResultMenuState(false, false, false), actions);
        Assert.Equal(
            ["header notes.txt (result)", "Undo (disabled: Nothing to undo)", "Redo (disabled: Nothing to redo)", "—", "Cut (disabled: Select some text first)", "Copy (disabled: Select some text first)", "Paste", "Select all"],
            Lines(nothing));

        var everything = ConflictMenus.ForConflictResult("notes.txt", new ConflictResultMenuState(true, true, true), actions);
        Assert.Equal(["header notes.txt (result)", "Undo", "Redo", "—", "Cut", "Copy", "Paste", "Select all"], Lines(everything));

        var pane = ConflictMenus.ForConflictPane("Ours: main", false, () => { });
        Assert.Equal(["header Ours: main", "Copy (disabled: Select some text first)"], Lines(pane));
        Assert.Equal(["header Theirs: feature/theirs", "Copy"], Lines(ConflictMenus.ForConflictPane("Theirs: feature/theirs", true, () => { })));
    }

    private static IEnumerable<string> Lines(MenuModel menu) => menu.Entries.Select(entry => entry switch
    {
        MenuHeader header => "header " + header.Text,
        MenuSeparator => "—",
        ViewModels.Menus.MenuItem { IsEnabled: false } item => $"{item.Label} (disabled: {item.DisabledReason})",
        ViewModels.Menus.MenuItem item => item.Label,
        _ => "?",
    });

    private static string Scenarios_NotesTheirs => Testing.Scenarios.ConflictsFiles.NotesTheirs;

    private static string Scenarios_NotesOurs => Testing.Scenarios.ConflictsFiles.NotesOurs;

    /// <summary>One of the two identical blocks git writes when both sides changed <c>x = 1</c> in two places alike (checked with Git 2.36).</summary>
    private const string Twin = "<<<<<<< HEAD\nx = 2\n=======\nx = 3\n>>>>>>> feature\n";

    /// <summary>A merge's stop on version.txt: two identical blocks with three common lines between them.</summary>
    private static (RecordingConflictActions Actions, ManualDelays Delays, ConflictResolverViewModel Resolver) TwinStop()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("version.txt", Twin + "a\nb\nc\n" + Twin + "d\n");
        var delays = new ManualDelays();
        return (actions, delays, Resolver(actions, delays, Status(ResolverFixtures.Merge("feature"), Entry("version.txt"))));
    }

    /// <summary>The merge's stop: notes.txt both modified with two blocks (FM).</summary>
    private static (RecordingConflictActions Actions, ManualDelays Delays, ConflictResolverViewModel Resolver) MergeStop()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("notes.txt", Notes);
        var delays = new ManualDelays();
        return (actions, delays, Resolver(actions, delays, Status(ResolverFixtures.Merge(), Entry("notes.txt"))));
    }
}
