// Check C15 of phase 4's walk-through (docs/test-reports/phase-4.md, the row C15 of "Checks"):
// conflicts without text, in the merge of feature/kinds stopped in a terminal: the "deleted by
// them" card, the binary card, the add/add file in the panes, every file resolved. What checks C14
// to C17 share is in Phase4Checks.C.EditingSupport.cs; the expected values in
// Phase4Expectations.C2.cs (PartC2).
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksC
{
    /// <summary>
    /// Check C15: the merge of feature/kinds with a file deleted by them, a binary file and a file
    /// added by both; the cards, Keep the file, Use theirs, the add/add file in the panes, every
    /// file resolved; dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C15_conflicts_without_text()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.MergeKinds);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var name = NameOf(repo);

        // Step 1: the working-changes row: the stage panel with the three conflicted files.
        app.Click(app.WorkingRowPoint());
        await C14To17.WaitForStagePanelAsync(app, PartC2.UnstagedTitle(3, 3), PartC2.StagedTitle(0));
        await app.WaitForIdleAsync();
        Shot(app, "c15a-kinds-stage-panel");
        AssertBanner(app, PartC2.MergeKindsBanner(3), PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(3)), oneLine: false, placement: BannerButtonPlacement.OwnRow);
        AssertGraph(app, PartC2.GcM, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.WorkingConflicts(3)), showsDate: false);
        Assert.True(Graph(app).IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.Equal(["U image.png", "U new.txt", "U other.txt"], C14To17.FileRows(app, "UnstagedFileList"));
        foreach (var path in new[] { "image.png", "new.txt", "other.txt" })
        {
            await C14To17.AssertConflictRowAsync(app, path);
        }

        C14To17.AssertEmptyLists(app, unstagedEmpty: false, stagedEmpty: true);
        C14To17.AssertMessage(app, PartC2.MergeKindsSummary, string.Empty);
        C14To17.AssertCommitButton(app, PartC2.StageFilesToCommit, enabled: false);
        Assert.Equal(PartC2.KindsStopStatus, await C14To17.PorcelainAsync(repo));
        var unmergedAtStop = await C14To17.UnmergedAsync(repo);
        C15Kinds.AssertKindsStages(unmergedAtStop);
        Assert.Empty(await BackupsAsync(repo));

        // Step 2: other.txt, deleted by them: its card.
        app.Click(await app.FileRowAsync("UnstagedFileList", "other.txt"));
        await app.WaitForResolverAsync("other.txt");
        await app.WaitForIdleAsync();
        Shot(app, "c15b-deleted-by-them");
        C14To17.AssertResolverPlace(app);
        AssertBanner(app, PartC2.MergeKindsBanner(3), PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(3)), oneLine: null, placement: BannerButtonPlacement.BesideText);
        var resolver = app.Resolver();
        C14To17.AssertHeader(app, resolver, "other.txt", PartC2.DeletedByThem, PartC2.FilePosition(3, 3));
        C15Kinds.AssertEmptyToolbar(app, resolver);
        C15Kinds.AssertCard(app, resolver, PartC2.DeletedByThemCard, PartC2.DeletedByThemButtons);
        Assert.Equal(PartC2.KindsStopStatus, await C14To17.PorcelainAsync(repo));
        Assert.Equal(unmergedAtStop, await C14To17.UnmergedAsync(repo));

        // Step 3: Keep the file: other.txt staged as it is; the resolver moves on to image.png.
        var calls = app.GitCalls().Count;
        var mark = app.LogMark();
        app.ClickCardButton("ConflictKeepFile");
        await WaitForOperationAsync(app, repo, PartC2.ResolveTitle("other.txt"), mark);
        await app.WaitForResolverAsync("image.png");
        await app.WaitForIdleAsync();
        Shot(app, "c15c-binary-card");
        Assert.Equal(0, AssertCalled(app, calls, PartC2.AddOther).ExitCode);
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git checkout", StringComparison.Ordinal));
        AssertStatus(app, PartC2.Resolved("other.txt"));
        Assert.Equal(C14To17.Bytes(PartC2.OtherOurs), C14To17.WorkingFile(repo, "other.txt"));
        Assert.StartsWith(PartC2.OtherOursBlob, await C14To17.StagedBlobAsync(repo, "other.txt"), StringComparison.Ordinal);
        Assert.Equal(["UU image.png", "AA new.txt"], await C14To17.PorcelainAsync(repo));
        Assert.Empty(await BackupsAsync(repo));
        AssertBanner(app, PartC2.MergeKindsBanner(2), PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(2)), oneLine: null, placement: BannerButtonPlacement.BesideText);
        resolver = app.Resolver();
        C14To17.AssertHeader(app, resolver, "image.png", PartC2.BothModified, PartC2.FilePosition(1, 2));
        Assert.Null(resolver.ConflictPosition);
        Assert.Null(resolver.TakeOurs);
        Assert.Null(resolver.TakeTheirs);
        Assert.Null(resolver.MarkResolved);
        Assert.Equal(new ResolverButton(PartC2.MergeToolButton, false, PartC2.NoMergeTool), resolver.MergeTool);
        C15Kinds.AssertCard(app, resolver, PartC2.BinaryCard, PartC2.BinaryButtons);

        // Step 4: Use theirs: a snapshot of the changed picture, then theirs checked out and staged.
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        app.ClickCardButton("ConflictUseTheirsFile");
        await WaitForOperationAsync(app, repo, PartC2.ResolveTitle("image.png"), mark);
        await app.WaitForResolverAsync("new.txt");
        await app.WaitForIdleAsync();
        Shot(app, "c15d-both-added");
        AssertCalledInOrder(app, calls, PartC2.CheckoutTheirsImage, PartC2.AddImage);
        AssertStatus(app, PartC2.Resolved("image.png"));
        var snapshot = Assert.Single(await BackupsAsync(repo));
        await C15Kinds.AssertImageSnapshotAsync(repo, snapshot);
        C15Kinds.AssertSnapshotFirst(app, calls, snapshot.Name);
        Assert.StartsWith(PartC2.ImageKindsBlob, (await repo.GitAsync("hash-object", "--", "image.png")).StandardOutput.Trim(), StringComparison.Ordinal);
        Assert.Equal(C14To17.WorkingFile(repo, "image.png"), Scenarios.ConflictsFiles.ImageKinds);
        Assert.Equal(["M  image.png", "AA new.txt"], await C14To17.PorcelainAsync(repo));
        AssertBanner(app, PartC2.MergeKindsBanner(1), PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(1)), oneLine: null, placement: BannerButtonPlacement.BesideText);
        resolver = app.Resolver();
        C14To17.AssertHeader(app, resolver, "new.txt", PartC2.BothAdded, PartC2.FilePosition(1, 1));
        C14To17.AssertTextToolbar(app, resolver, PartC2.ConflictPosition(1, 1), mergeTool: false);
        C14To17.AssertTitles(resolver, PartC2.OursMain, PartC2.TheirsKinds, 1);
        C14To17.AssertPanes(resolver, PartC2.NewOursPane, PartC2.NewTheirsPane);
        C14To17.AssertResult(resolver, PartC2.NewConflicted, PartC2.NewConflictedTints);
        C14To17.AssertFocusAndCaret(app, 2, 1);
        Assert.Null(resolver.CardText);

        // Step 5: Use both (ours first), then Mark resolved: no question; new.txt written and staged; the resolver closes.
        app.ClickBarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseBothOursFirst);
        resolver = app.Resolver();
        C14To17.AssertResult(resolver, PartC2.NewResolved, PartC2.Tints(3));
        Assert.Equal((PartC2.NoConflictsLeft, PartC2.NoConflictsLeft), (resolver.ResultCount, resolver.ConflictPosition));
        C14To17.AssertFocusAndCaret(app, 2, 1);
        calls = app.GitCalls().Count;
        mark = app.LogMark();
        app.ClickResolverButton("ResolverMarkResolved");
        app.Settle();
        Assert.Null(app.Session.Services.DialogHost.Current);
        await WaitForOperationAsync(app, repo, PartC2.ResolveTitle("new.txt"), mark);
        await app.WaitForResolverClosedAsync();
        await AssertNoDialogAsync(app);
        await C14To17.WaitForStagePanelAsync(app, PartC2.UnstagedTitle(0), PartC2.StagedTitle(2));
        await app.WaitForAsync(() => app.BannerContinueButton.IsFocused, "the keyboard focus on the banner's Continue");
        await app.WaitForIdleAsync();
        Shot(app, "c15e-all-resolved");
        Assert.Equal(0, AssertCalled(app, calls, PartC2.AddNew).ExitCode);
        AssertStatus(app, PartC2.Resolved("new.txt"));
        Assert.Equal(C14To17.Bytes(PartC2.NewResolved), C14To17.WorkingFile(repo, "new.txt"));
        AssertGraph(app, PartC2.GcM, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.Working), showsDate: false);
        Assert.True(Graph(app).IsWorkingRowSelected, "The working-changes row is not selected.");
        AssertBanner(app, PartC2.MergeKindsResolvedBanner, PartC2.AbortMerge, BannerButton.Continue, oneLine: false, placement: BannerButtonPlacement.OwnRow);
        C14To17.AssertEmptyLists(app, unstagedEmpty: true, stagedEmpty: false);
        Assert.Equal(["M image.png", "M new.txt"], C14To17.FileRows(app, "StagedFileList"));
        C14To17.AssertMessage(app, PartC2.MergeKindsSummary, string.Empty);
        C14To17.AssertCommitButton(app, PartC2.Commit2Files, enabled: true);
        Assert.False(app.Find<Control>("RestoreBar").IsEffectivelyVisible, "The stage panel offers the card's snapshot in its restore bar.");
        Assert.Equal(["M  image.png", "M  new.txt"], await C14To17.PorcelainAsync(repo));
        Assert.Empty(await C14To17.UnmergedAsync(repo));
        var index = C14To17.Lines((await repo.GitAsync("ls-files", "-s")).StandardOutput).ToList();
        Assert.Equal(["README.md", "image.png", "new.txt", "notes.txt", "other.txt"], index.Select(line => line.Split('\t')[1]));
        Assert.All(index, line => Assert.Equal("0", line.Split('\t')[0].Split(' ')[2]));
        Assert.StartsWith(PartC2.ImageKindsBlob, await C14To17.StagedBlobAsync(repo, "image.png"), StringComparison.Ordinal);
        Assert.StartsWith(PartC2.NewResolvedBlob, await C14To17.StagedBlobAsync(repo, "new.txt"), StringComparison.Ordinal);
        Assert.StartsWith(PartC2.OtherOursBlob, await C14To17.StagedBlobAsync(repo, "other.txt"), StringComparison.Ordinal);
        Assert.StartsWith(PartC2.MainNotesBlob, await C14To17.StagedBlobAsync(repo, "notes.txt"), StringComparison.Ordinal);
        Assert.StartsWith(PartC2.KindsSha, C14To17.GitFile(repo, "MERGE_HEAD"), StringComparison.Ordinal);
        Assert.StartsWith(PartC2.MainSha, await repo.HeadAsync(), StringComparison.Ordinal);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal([snapshot], await BackupsAsync(repo));
        AssertNoClippedText(app);
    }

    /// <summary>What check C15 asserts of the stop and the cards.</summary>
    private static class C15Kinds
    {
        /// <summary>The stages git wrote at the kinds merge's stop ("The kinds merge").</summary>
        public static void AssertKindsStages(IReadOnlyList<string> unmerged)
        {
            var stages = unmerged.Select(line => line.Split('\t')).Select(parts => (Path: parts[1], Stage: parts[0].Split(' ')[2], Blob: parts[0].Split(' ')[1])).ToList();
            Assert.Equal(
                [("image.png", "1", PartC2.ImageBaseBlob), ("image.png", "2", PartC2.ImageOursBlob), ("image.png", "3", PartC2.ImageKindsBlob), ("new.txt", "2", PartC2.NewOursBlob), ("new.txt", "3", PartC2.NewKindsBlob), ("other.txt", "1", PartC2.OtherBaseBlob), ("other.txt", "2", PartC2.OtherOursBlob)],
                stages.Select(stage => (stage.Path, stage.Stage, stage.Blob[..7])));
        }

        /// <summary>
        /// A card's toolbar (14.3): the row stays, 36 high, with none of the conflict position and
        /// its arrows, Take ours, Take theirs, <c>Merge tool…</c> and <c>Mark resolved</c>.
        /// </summary>
        public static void AssertEmptyToolbar(ShellDriver app, ResolverSnapshot resolver)
        {
            Assert.Null(resolver.ConflictPosition);
            Assert.Null(resolver.TakeOurs);
            Assert.Null(resolver.TakeTheirs);
            Assert.Null(resolver.MergeTool);
            Assert.Null(resolver.MarkResolved);
            foreach (var id in new[] { "ResolverConflictPosition", "ResolverPreviousConflict", "ResolverNextConflict" })
            {
                Assert.False(app.FindByAutomationId(app.ResolverView, id).IsEffectivelyVisible, $"{id} shows on a card's toolbar.");
            }

            Assert.Equal(PartC2.ResolverRowHeight, app.ResolverView.FindControl<Border>("ResolverToolbar")!.Bounds.Height, 0.5);
        }

        /// <summary>
        /// The card (11.8): in the middle of the resolver's body, its text and buttons, and no panes
        /// and no result.
        /// </summary>
        public static void AssertCard(ShellDriver app, ResolverSnapshot resolver, string text, IReadOnlyList<string> buttons)
        {
            Assert.Equal(text, resolver.CardText);
            Assert.Equal(buttons, resolver.CardButtons);
            Assert.Null(resolver.Result);
            Assert.Null(resolver.OursTitle);
            Assert.Empty(resolver.OursRows);
            Assert.False(app.FindByAutomationId(app.ResolverView, "ConflictResult").IsEffectivelyVisible, "The result shows beside a card.");
            Assert.False(app.ResolverPanes.IsEffectivelyVisible, "The panes show beside a card.");
            var card = app.BoundsOf(app.FindByAutomationId(app.ResolverView, "ConflictCard"));
            var resolverBounds = app.BoundsOf(app.ResolverView);
            var bodyTop = app.BoundsOf(app.ResolverView.FindControl<Border>("ResolverToolbar")!).Bottom;
            Assert.Equal(resolverBounds.Center.X, card.Center.X, 1.0);
            Assert.Equal((bodyTop + resolverBounds.Bottom) / 2, card.Center.Y, 1.0);
        }

        /// <summary>The snapshot's ref was written before git checked theirs out over the picture (11.8: "a snapshot is saved first").</summary>
        public static void AssertSnapshotFirst(ShellDriver app, int mark, string snapshotRef)
        {
            var calls = CallsSince(app, mark);
            var saved = calls.FindIndex(call => call.Contains(snapshotRef, StringComparison.Ordinal));
            var checkout = calls.IndexOf(PartC2.CheckoutTheirsImage);
            Assert.True(saved >= 0 && saved < checkout, $"The snapshot {snapshotRef} was not written before \"{PartC2.CheckoutTheirsImage}\". The app ran:\n" + string.Join('\n', calls));
        }

        /// <summary>
        /// The snapshot "Use theirs" saved before writing over the changed picture (11.8, D148):
        /// <c>discard-&lt;time&gt;</c>, a commit by VisualCommit's snapshot identity whose only parent
        /// is main's da31c97 and whose image.png is the green picture c22a1df. By parent and content,
        /// never by id.
        /// </summary>
        public static async Task AssertImageSnapshotAsync(TempRepo repo, (BackupRef Ref, string Name, string Sha) snapshot)
        {
            Assert.Equal("discard", snapshot.Ref.Kind);
            Assert.False(snapshot.Ref.IsChanges);
            Assert.Null(snapshot.Ref.Name);
            var commit = C14To17.Lines((await repo.GitAsync("cat-file", "-p", snapshot.Sha)).StandardOutput).ToList();
            var parents = commit.Where(line => line.StartsWith("parent ", StringComparison.Ordinal)).ToList();
            var parent = Assert.Single(parents);
            Assert.StartsWith("parent " + PartC2.MainSha, parent, StringComparison.Ordinal);
            Assert.Contains(commit, line => line.StartsWith("author VisualCommit <snapshot@visualcommit.invalid> ", StringComparison.Ordinal));
            Assert.Contains(commit, line => line.StartsWith("committer VisualCommit <snapshot@visualcommit.invalid> ", StringComparison.Ordinal));
            Assert.StartsWith(PartC2.ImageOursBlob, (await repo.GitAsync("rev-parse", snapshot.Sha + ":image.png")).StandardOutput.Trim(), StringComparison.Ordinal);
        }
    }
}
