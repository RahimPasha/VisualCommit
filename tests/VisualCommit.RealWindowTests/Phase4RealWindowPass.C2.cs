// Part C's real-window steps for checks C14 to C17 (docs/test-reports/phase-4.md, part C, the "Real
// window" column): C14 steps 1 to 3 (real typing into the result), C15 steps 1 to 3 (the "deleted
// by them" card and Keep the file) and C16 steps 2 and 4 (a real process for the merge tool), each
// in a launch of its own on a fresh stopped "conflicts" copy at 1100×700, dark. C17 is "n/a" (the
// report gives the reason). The working-changes row, the stage panel's rows, the resolver's texts
// and buttons are found as in Phase4RealWindowPass.C.cs, whose helpers these use; an operation's end
// is its queue's log line (the report's conventions of C14 to C17).
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC2;

namespace VisualCommit.RealWindowTests;

public partial class Phase4RealWindowPass
{
    /// <summary>
    /// Check C14, steps 1 to 3 (step 3's two right-clicks are scripted only), with the real mouse
    /// and keyboard on the merge of feature/theirs stopped: the working-changes row and notes.txt
    /// clicked, the resolver open; a real click in the result at line 2, column 1, Shift+Down five
    /// times, the text typed and Enter on the real keyboard; the wait until the result's count
    /// reads <c>1 conflict left</c> (the real window cannot read the resolver's parse state, 14.3)
    /// (<c>c14a-typed</c>); then a real Esc, which changes nothing: before and after it the result's
    /// Value pattern holds step 2's 16 lines, and after it the resolver is still there with no dialog.
    /// </summary>
    [Fact]
    public async Task Check_C14_typing_into_the_result()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = Launch(repo);
        var name = NameOf(repo);

        // When the app has started: the merge's banner, its buttons below the text.
        AssertBanner(app, MergeTheirsBanner, ResolveFirst(1), skip: false, AbortMerge, besideText: false);
        Assert.Equal(MergeStopStatus, await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Equal(Fm, WorkingFile(repo, "notes.txt"));

        // Step 1: the working-changes row, then notes.txt in the unstaged list: the resolver opens.
        OpenConflictedFile(app, "notes.txt", UnstagedTitle(1, 1), StagedTitle(0));
        app.WaitForResultCount(ConflictsLeft(2));
        AssertResolverHeader(app, "notes.txt", BothModified, FilePosition(1, 1));
        WaitForResult(app, Fm, ConflictsLeft(2), ConflictPosition(1, 2));
        AssertBanner(app, MergeTheirsBanner, ResolveFirst(1), skip: false, AbortMerge, besideText: true);
        var mark = app.LogMark();

        // Step 2: block 1 selected from line 2 and typed over on the real keyboard, then parsed.
        app.ClickResultAt(2, 1);
        for (var press = 0; press < 5; press++)
        {
            app.PressKeys(VirtualKeyShort.SHIFT, VirtualKeyShort.DOWN);
        }

        app.TypeText("line 2 edited by hand");
        app.PressKey(VirtualKeyShort.RETURN);
        WaitForResult(app, Typed, ConflictsLeft(1), ConflictPosition(1, 2));
        Assert.Equal([NotUsed, NotUsed], app.BarStateTexts().Take(2));
        Compare(app, "c14a-typed");

        // Nothing written: the file as git wrote it, git's state, no Resolve operation.
        Assert.Equal(Fm, WorkingFile(repo, "notes.txt"));
        Assert.Equal(MergeStopStatus, await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains($"] {name}: {ResolveTitle("notes.txt")}: ", StringComparison.Ordinal));

        // Step 3: Esc inside the result does nothing: the resolver stays, with no dialog and no menu.
        app.PressKey(VirtualKeyShort.ESCAPE);
        Assert.True(app.IsResolverShown, "Esc in the result closed the resolver.");
        AssertNoDialog(app);
        Assert.Empty(app.FindAll("ContextMenu"));
        Assert.Equal(Typed, app.ResultText());
        Assert.Equal(ConflictsLeft(1), app.ResolverText("ConflictResultCount"));
        Assert.Equal(Fm, WorkingFile(repo, "notes.txt"));

        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Check C15, steps 1 to 3, with the real mouse on the merge of feature/kinds stopped: the
    /// working-changes row and other.txt clicked: the "deleted by them" card
    /// (<c>c15b-deleted-by-them</c>); a real click on <c>Keep the file</c>
    /// (<c>ConflictKeepFile</c>), the wait for the log line of <c>Resolve other.txt</c> and for the
    /// resolver to show image.png: the binary card (<c>c15c-binary-card</c>).
    /// </summary>
    [Fact]
    public async Task Check_C15_deleted_by_them_card_and_keep_the_file()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.MergeKinds);
        using var app = Launch(repo);
        var name = NameOf(repo);

        // Step 1: the working-changes row: the stage panel with the three conflicted files.
        AssertBanner(app, MergeKindsBanner(3), ResolveFirst(3), skip: false, AbortMerge, besideText: false);
        ClickWorkingRow(app, UnstagedTitle(3, 3), StagedTitle(0));
        Assert.Equal(KindsStopStatus, await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all"));
        var unmergedAtStop = await GitLinesAsync(repo, "ls-files", "-u");

        // Step 2: other.txt, deleted by them: its card, no toolbar's buttons, no panes, no result.
        app.ClickWithMouse(app.StageFileRow("UnstagedFileList", "other.txt"));
        app.WaitForResolver("other.txt");
        app.WaitForText("ConflictCardText", DeletedByThemCard);
        AssertResolverHeader(app, "other.txt", DeletedByThem, FilePosition(3, 3));
        AssertBanner(app, MergeKindsBanner(3), ResolveFirst(3), skip: false, AbortMerge, besideText: true);
        AssertCardButtons(app, ("ConflictKeepFile", "Keep the file"), ("ConflictDeleteFile", "Delete it"));
        AssertNotShown(app, "ResolverConflictPosition", "ResolverPreviousConflict", "ResolverNextConflict", "ResolverTakeOurs", "ResolverTakeTheirs", "ResolverMergeTool", "ResolverMarkResolved", "ConflictOurs", "ConflictTheirs", "ConflictResult");
        Assert.Equal(KindsStopStatus, await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Equal(unmergedAtStop, await GitLinesAsync(repo, "ls-files", "-u"));
        Compare(app, "c15b-deleted-by-them");

        // Step 3: Keep the file: other.txt staged as it is; the resolver moves on to image.png.
        var keep = ExpectOperation(app, name, ResolveTitle("other.txt"));
        app.ClickResolverButton("ConflictKeepFile");
        keep.Wait(OperationOutcome.Succeeded, Resolved("other.txt"));
        app.WaitForResolver("image.png", OperationTimeout);
        app.WaitForText("ConflictCardText", BinaryCard);
        WaitForStatus(app, Resolved("other.txt"));
        AssertResolverHeader(app, "image.png", BothModified, FilePosition(1, 2));
        AssertBanner(app, MergeKindsBanner(2), ResolveFirst(2), skip: false, AbortMerge, besideText: true);
        AssertCardButtons(app, ("ConflictUseOursFile", "Use ours"), ("ConflictUseTheirsFile", "Use theirs"));
        AssertNotShown(app, "ResolverConflictPosition", "ResolverTakeOurs", "ResolverTakeTheirs", "ResolverMarkResolved", "ConflictOurs", "ConflictTheirs", "ConflictResult");
        var tool = Assert.Single(app.ShownElements("ResolverMergeTool"));
        Assert.False(tool.IsEnabled, "Merge tool… is enabled without merge.tool.");
        Assert.Equal(NoMergeTool, tool.HelpText);
        Compare(app, "c15c-binary-card");

        // Git: other.txt staged as main's (its blob at stage 0), nothing checked out, no backup.
        Assert.Equal(["UU image.png", "AA new.txt"], await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Equal(OtherOurs, WorkingFile(repo, "other.txt"));
        Assert.StartsWith(OtherOursBlob, (await repo.GitAsync("rev-parse", ":0:other.txt")).StandardOutput.Trim(), StringComparison.Ordinal);
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Check C16, steps 2 and 4, with the real mouse on a fresh copy of the merge of feature/theirs
    /// stopped, with the fake merge tool configured: the working-changes row and notes.txt clicked;
    /// <c>Use ours</c> in block 1's bar (the <c>ConflictUseOurs</c> whose bounding rectangle is
    /// higher); <c>Merge tool…</c>: the question for a modified result
    /// (<c>c16b-tool-question</c>); a real click on <c>Discard</c>: git runs a real <c>sh</c>
    /// process with the fake tool, and the pass waits for the queue's log line of
    /// <c>Merge tool for notes.txt</c>: the file resolved, the resolver closed
    /// (<c>c16c-tool-resolved</c>). Step 1 (the file's menu) and step 3 (Keep editing) are the
    /// scripted run's alone.
    /// </summary>
    [Fact]
    public async Task Check_C16_merge_tool_with_a_real_process()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge, mergeTool: true);
        using var app = Launch(repo);
        var name = NameOf(repo);
        AssertBanner(app, MergeTheirsBanner, ResolveFirst(1), skip: false, AbortMerge, besideText: false);
        Assert.Equal(Scenarios.FakeMergeTool, (await repo.GitAsync("config", "--get", "merge.tool")).StandardOutput.Trim());

        // Step 2: notes.txt; Use ours in block 1's ours bar; Merge tool… asks first.
        OpenConflictedFile(app, "notes.txt", UnstagedTitle(1, 1), StagedTitle(0));
        WaitForResult(app, Fm, ConflictsLeft(2), ConflictPosition(1, 2));
        AssertResolverHeader(app, "notes.txt", BothModified, FilePosition(1, 1));
        Assert.True(app.IsResolverButtonEnabled("ResolverMergeTool"), "Merge tool… is disabled with merge.tool set.");
        var uses = app.ShownElements("ConflictUseOurs");
        Assert.NotEmpty(uses);
        Assert.All(uses.Skip(1), other => Assert.True(other.BoundingRectangle.Top > uses[0].BoundingRectangle.Top, "Block 1's Use ours is not the upper one."));
        app.ClickBarButton("ConflictUseOurs", 0);
        WaitForResult(app, OursOnBlock1, ConflictsLeft(1), ConflictPosition(1, 2));
        Assert.Equal([Used, NotUsed], app.BarStateTexts().Take(2));
        app.ClickResolverButton("ResolverMergeTool");
        app.WaitForText("ConfirmTitle", DiscardTitle("notes.txt"));
        Assert.Equal(MergeToolQuestion, app.ResolverText("ConfirmQuestion"));
        Assert.DoesNotContain(app.ShownElements("ConfirmDetail"), detail => !string.IsNullOrEmpty(detail.Name));
        var keepEditing = app.Find("CancelButton");
        Assert.Equal(KeepEditing, keepEditing.Name);
        app.WaitFor(() => RealApp.HasKeyboardFocus(app.Find("CancelButton")), "Keep editing to have the keyboard focus", RealApp.DefaultWaitTimeout);
        Assert.Equal(Discard, app.Find("ConfirmButton").Name);
        Assert.Equal(Fm, WorkingFile(repo, "notes.txt"));
        Compare(app, "c16b-tool-question");

        // Step 4: Discard: the app runs git mergetool, git runs sh with the fake tool, which writes
        // the file; git stages it, the resolver finds no conflicted file left and closes.
        var tool = ExpectOperation(app, name, MergeToolTitle);
        app.ClickWithMouse(app.Find("ConfirmButton"));
        tool.Wait(OperationOutcome.Succeeded, ResolvedInFake);
        app.WaitForResolverClosed(OperationTimeout);
        app.WaitUntilGone("ConfirmationDialog");
        AssertNoDialog(app);
        app.WaitForStageTitles(UnstagedTitle(0), StagedTitle(1), OperationTimeout);
        AssertBanner(app, MergeTheirsResolvedBanner, continueReason: null, skip: false, AbortMerge, besideText: false);
        WaitForContinueFocus(app);
        WaitForStatus(app, ResolvedInFake);
        Compare(app, "c16c-tool-resolved");

        // Git: the tool's text, staged; no backup file of mergetool's left; the merge still in progress.
        Assert.Equal(FakeToolResult, WorkingFile(repo, "notes.txt"));
        Assert.StartsWith(FakeToolBlob, (await repo.GitAsync("rev-parse", ":0:notes.txt")).StandardOutput.Trim(), StringComparison.Ordinal);
        Assert.Equal(["M  notes.txt"], await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Empty(await GitLinesAsync(repo, "ls-files", "-u"));
        Assert.StartsWith(TheirsSha, File.ReadAllText(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), StringComparison.Ordinal);
        Assert.StartsWith(MainSha, await repo.HeadAsync(), StringComparison.Ordinal);
        Assert.Equal(0, app.Close());
    }

    /// <summary>Clicks the working-changes row below the banner, waits for the stage panel with these titles, then clicks the conflicted file's row and waits for the resolver on it.</summary>
    private static void OpenConflictedFile(RealApp app, string path, string unstagedTitle, string stagedTitle)
    {
        ClickWorkingRow(app, unstagedTitle, stagedTitle);
        app.ClickWithMouse(app.StageFileRow("UnstagedFileList", path));
        app.WaitForResolver(path, OperationTimeout);
    }

    /// <summary>The card's buttons (11.8), left to right: their automation ids and labels, each enabled.</summary>
    private static void AssertCardButtons(RealApp app, params (string Id, string Label)[] buttons)
    {
        Assert.Single(app.ShownElements("ConflictCard"));
        var shown = buttons.Select(button => (button, Element: Assert.Single(app.ShownElements(button.Id)))).ToList();
        foreach (var (button, element) in shown)
        {
            Assert.Equal(button.Label, element.Name);
            Assert.True(element.IsEnabled, $"The card's {button.Label} is disabled.");
        }

        var lefts = shown.Select(entry => entry.Element.BoundingRectangle.Left).ToList();
        Assert.True(lefts.SequenceEqual(lefts.Order()), $"The card's buttons are not left to right: {string.Join(", ", buttons.Select(button => button.Label))}.");
    }

    /// <summary>None of the elements with these automation ids is on screen.</summary>
    private static void AssertNotShown(RealApp app, params string[] automationIds)
    {
        foreach (var id in automationIds)
        {
            Assert.True(app.ShownElements(id).Count == 0, $"{id} is on screen.");
        }
    }
}
