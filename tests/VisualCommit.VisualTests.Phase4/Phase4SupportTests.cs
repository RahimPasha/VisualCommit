using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

/// <summary>
/// The assertions every part of phase 4's walk-through shares (Phase4Checks.Support.cs) and the
/// helpers of Phase4Expectations.Common.cs, tried once, on states whose expected results phase 4
/// does not change (design 5.1, 5.2: a <c>git am</c> stop keeps phase 3's banner, and so do
/// conflicts without an operation), so that a check that fails points at the app and not at the
/// assertion. No pictures: these are not checks of the report.
/// </summary>
public sealed class Phase4SupportTests : Phase4Checks
{
    [Theory]
    [InlineData("2026-01-02T09:00:00+00:00")]
    [InlineData("2026-01-02T09:00:00Z")]
    public void A_git_date_in_UTC_passes_whichever_way_the_git_version_writes_it(string gitText)
    {
        AssertGitDate(AppCommitTime, gitText);
        AssertGitDate(AppCommitTime, gitText + "\n");
        Assert.ThrowsAny<Exception>(() => AssertGitDate(AppCommitTime, "2026-01-02T09:00:00+01:00"));
    }

    [Fact]
    public async Task The_working_tree_is_the_full_folder_git_names_without_a_trailing_separator()
    {
        using var repo = await TempRepo.CreateAsync();
        var workingTree = await WorkingTreeAsync(repo);

        Assert.Equal(Path.GetFullPath(workingTree), workingTree);
        Assert.False(Path.EndsInDirectorySeparator(workingTree), $"Ends with a separator: {workingTree}");
        var marker = $"same-folder-{Guid.NewGuid():N}";
        File.WriteAllText(Path.Combine(repo.Path, marker), string.Empty);
        Assert.True(File.Exists(Path.Combine(workingTree, marker)), $"{workingTree} is not the folder {repo.Path}.");
    }

    [Fact]
    public void A_backup_refs_name_is_taken_apart_with_the_kinds_of_design_section_9()
    {
        var rebase = BackupRef.Parse("refs/visualcommit/backup/rebase-20260102T090000123Z/feature/topic");
        Assert.Equal(("rebase", (int?)null, false, "feature/topic"), (rebase.Kind, rebase.Number, rebase.IsChanges, rebase.Name));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 0, 123, TimeSpan.Zero), rebase.Time);
        Assert.Equal("rebase-20260102T090000123Z", rebase.Stem);

        var skip = BackupRef.Parse("refs/visualcommit/backup/skip-20260102T090000000Z");
        var skipChanges = BackupRef.Parse("refs/visualcommit/backup/skip-20260102T090000000Z-changes");
        Assert.Equal(("skip", false, (string?)null), (skip.Kind, skip.IsChanges, skip.Name));
        Assert.Equal(("skip", true), (skipChanges.Kind, skipChanges.IsChanges));
        Assert.Equal(skip.Stem, skipChanges.Stem);

        var discard = BackupRef.Parse("refs/visualcommit/backup/discard-20260102T090000000Z-2");
        Assert.Equal(("discard", (int?)2, "discard-20260102T090000000Z-2"), (discard.Kind, discard.Number, discard.Stem));

        var tag = BackupRef.Parse("refs/visualcommit/backup/remote-tag-20260102T090000000Z-3/origin/v1.2");
        Assert.Equal(("remote-tag", (int?)3, "origin/v1.2"), (tag.Kind, tag.Number, tag.Name));

        foreach (var kind in BackupKinds)
        {
            Assert.Equal(kind, BackupRef.Parse($"refs/visualcommit/backup/{kind}-20260102T090000000Z").Kind);
        }

        Assert.Throws<FormatException>(() => BackupRef.Parse("refs/heads/main"));
        Assert.Throws<FormatException>(() => BackupRef.Parse("refs/visualcommit/backup/reset-2026"));
        Assert.Throws<FormatException>(() => BackupRef.Parse("refs/visualcommit/backup/merge-20260102T090000000Z"));
    }

    [Fact]
    public void The_menu_height_is_4as_formula()
    {
        Assert.Equal(232, MenuHeight(7, 2));
        Assert.Equal(84, MenuHeight(2, 0));
    }

    /// <summary>Design 5.2's layout rules, on made-up positions: what the banner assertion accepts and what it reports.</summary>
    [Fact]
    public void The_banners_layout_rules_accept_buttons_beside_the_text_or_on_a_row_of_their_own()
    {
        // 600 wide and one line: the text, then Continue, Skip and Abort, 6 apart, 8 from the right edge.
        var banner = new Rect(260, 74, 600, 36);
        var text = new Rect(296, 83, 300, 18);
        Rect[] beside = [new(612, 79, 80, 26), new(698, 79, 60, 26), new(764, 79, 88, 26)];
        Assert.Empty(BannerLayoutProblems(banner, text, 1, beside, oneLine: true, BannerButtonPlacement.BesideText));
        Assert.NotEmpty(BannerLayoutProblems(banner, text, 1, beside, oneLine: false, null));
        Assert.NotEmpty(BannerLayoutProblems(banner, text, 1, beside, null, BannerButtonPlacement.OwnRow));
        Assert.NotEmpty(BannerLayoutProblems(banner, text, 2, beside, oneLine: true, null));

        // A pixel off is layout's rounding; nine apart, or 11 from the edge, is reported.
        Rect[] rounded = [new(611, 79, 80, 26), new(697.5, 79, 60, 26), new(764, 79, 88.5, 26)];
        Assert.Empty(BannerLayoutProblems(banner, text, 1, rounded, null, null));
        Rect[] spread = [new(609, 79, 80, 26), new(698, 79, 60, 26), new(764, 79, 88, 26)];
        Assert.NotEmpty(BannerLayoutProblems(banner, text, 1, spread, null, null));
        Rect[] inset = [new(609, 79, 80, 26), new(695, 79, 60, 26), new(761, 79, 88, 26)];
        Assert.NotEmpty(BannerLayoutProblems(banner, text, 1, inset, null, null));

        // A text column narrower than 240 beside the buttons: they belong on a row of their own.
        Assert.NotEmpty(BannerLayoutProblems(banner, text.WithWidth(230), 1, beside, null, null));

        // On a row of their own under two lines of text, right-aligned.
        var tall = new Rect(260, 74, 440, 86);
        var wrapped = new Rect(296, 83, 396, 36);
        Rect[] row = [new(452, 126, 80, 26), new(538, 126, 60, 26), new(604, 126, 88, 26)];
        Assert.Empty(BannerLayoutProblems(tall, wrapped, 2, row, oneLine: false, BannerButtonPlacement.OwnRow));
        Assert.NotEmpty(BannerLayoutProblems(tall, wrapped, 2, row, oneLine: true, null));
        Assert.NotEmpty(BannerLayoutProblems(tall, wrapped, 2, row, null, BannerButtonPlacement.BesideText));

        // No buttons: only the height counts.
        Assert.Empty(BannerLayoutProblems(banner, text, 1, [], oneLine: true, BannerButtonPlacement.BesideText));
        Assert.NotEmpty(BannerLayoutProblems(banner.WithHeight(30), text, 1, [], null, null));
    }

    /// <summary>
    /// A <c>git am</c> stopped with a conflict in the conflicts scenario (phase 3's banner, which
    /// phase 4 keeps: Abort only, design 5.1): the banner, the graph with its working-changes row,
    /// the Abort question, the operation's end, its call and backups, and the graph after it.
    /// </summary>
    [AvaloniaFact]
    public async Task The_banner_graph_question_operation_and_backup_assertions_hold_on_a_stopped_git_am()
    {
        using var repo = await Scenarios.ConflictsAsync();
        await StopInGitAmAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);
        await AssertNoDialogAsync(app);
        await AssertNoMenuAsync(app, logMark: 0);

        AssertBanner(app, "Applying patches (git am). 1 file has conflicts.", "Abort", oneLine: true, placement: BannerButtonPlacement.BesideText);
        Assert.ThrowsAny<Exception>(() => AssertBanner(app, "Applying patches (git am).", "Abort"));
        Assert.ThrowsAny<Exception>(() => AssertBanner(app, "Applying patches (git am). 1 file has conflicts.", "Abort git am"));
        Assert.ThrowsAny<Exception>(() => AssertBanner(app, "Applying patches (git am). 1 file has conflicts.", "Abort", continueButton: BannerButton.Continue));
        Assert.ThrowsAny<Exception>(() => AssertBanner(app, "Applying patches (git am). 1 file has conflicts.", "Abort", oneLine: false));
        Assert.Throws<InvalidOperationException>(() => app.ClickBannerContinue());
        Assert.Throws<InvalidOperationException>(() => app.ClickBannerSkip());

        AssertGraph(app, ConflictsGraph, 5, new WorkingChangesRow("Working changes · 1 conflict", 0, 0), picture: app.Capture());
        Assert.ThrowsAny<Exception>(() => AssertGraph(app, ConflictsGraph, 5));
        Assert.ThrowsAny<Exception>(() => AssertGraph(app, [.. ConflictsGraph.Skip(1)], 5, new WorkingChangesRow("Working changes · 1 conflict", 0, 0)));
        Assert.ThrowsAny<Exception>(() => AssertGraph(app, [ConflictsGraph[0] with { Labels = ["main"] }, .. ConflictsGraph.Skip(1)], 5, new WorkingChangesRow("Working changes · 1 conflict", 0, 0)));

        // The banner's Abort asks first; Cancel is the cancel button's label.
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(
            app,
            "Abort git am?",
            "Abort git am? HEAD goes back to where it was before it started, and the patches not applied yet are dropped.",
            "A backup is saved first, so this can be undone.",
            "Abort git am");
        await Assert.ThrowsAnyAsync<Exception>(() => AssertConfirmationAsync(
            app,
            "Abort git am?",
            "Abort git am? HEAD goes back to where it was before it started, and the patches not applied yet are dropped.",
            "A backup is saved first, so this can be undone.",
            "Abort git am",
            cancelText: "Keep editing"));

        var logMark = app.LogMark();
        var callMark = app.GitCalls().Count;
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await WaitForOperationAsync(app, repo, "Abort git am", logMark);

        AssertCalled(app, callMark, "git am --abort");
        AssertCalledInOrder(app, callMark, "git am --abort");
        Assert.ThrowsAny<Exception>(() => AssertCalled(app, callMark, "git rebase --abort"));
        AssertStatus(app, "Aborted git am");
        Assert.Null(app.BannerBounds);
        Assert.ThrowsAny<Exception>(() => AssertBanner(app, "Applying patches (git am). 1 file has conflicts.", "Abort"));
        Assert.Contains("git am --abort", CallsSince(app, callMark));

        var backups = await BackupsAsync(repo);
        Assert.NotEmpty(backups);
        Assert.All(backups, backup => Assert.Equal("abort", backup.Ref.Kind));
        Assert.Single(backups.Select(backup => backup.Ref.Stem).Distinct());
        var head = backups.Single(backup => !backup.Ref.IsChanges);
        Assert.Equal(await repo.RefTargetAsync("refs/heads/main"), head.Sha);

        AssertGraph(app, ConflictsGraph, 5, picture: app.Capture());
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput);
    }

    /// <summary>
    /// Conflicts without an operation (phase 3's stash that did not apply cleanly; no button in
    /// phase 4 either, design 5.1): the banner without buttons, and the platform's undo and redo
    /// pressed by the driver on a real text box, the commit summary.
    /// </summary>
    [AvaloniaFact]
    public async Task The_banner_without_buttons_and_the_platforms_undo_and_redo_hold_on_conflicts_without_an_operation()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.None);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        AssertBanner(app, "1 file has conflicts. Resolve it, then stage it.", abortText: null, oneLine: true);
        Assert.ThrowsAny<Exception>(() => AssertBanner(app, "1 file has conflicts. Resolve it, then stage it.", "Abort"));
        Assert.ThrowsAny<Exception>(() => AssertBanner(app, "1 file has conflicts. Resolve it, then stage it.", null, skipButton: BannerButton.Skip));

        // The driver's undo and redo are the platform's gestures (HotkeyConfiguration), which a text box obeys too.
        Assert.NotEqual(Key.None, app.UndoGesture.Key);
        Assert.NotEqual(app.UndoGesture, app.RedoGesture);
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => app.FindByAutomationId("CommitSummary").IsEffectivelyVisible, "the stage panel's summary box");
        var summary = (TextBox)app.FindByAutomationId("CommitSummary");
        var before = summary.Text ?? string.Empty;
        app.TypeInto("CommitSummary", "Resolve notes");
        Assert.Equal(before + "Resolve notes", summary.Text);
        app.Undo();
        Assert.Equal(before, summary.Text ?? string.Empty);
        app.Redo();
        Assert.Equal(before + "Resolve notes", summary.Text);
        Assert.Null(app.Session.Services.DialogHost.Current);
    }

    /// <summary>The conflicts scenario's graph (Phase4ScenarioTests pins it), as the report's tables write it.</summary>
    private static readonly CommitRow[] ConflictsGraph =
    [
        Row("Ours edits", 0, 0, Current("main")) with { ShortSha = "da31c97" },
        Row("Kinds edits", 1, 1, "feature/kinds") with { ShortSha = "3a9de3e" },
        Row("Pick last", 2, 2, "feature/picks") with { ShortSha = "5c4c257" },
        Row("Pick conflicting", 2, 2) with { ShortSha = "01fd976" },
        Row("Pick clean", 2, 2) with { ShortSha = "01d2046" },
        Row("Second change", 3, 3, "feature/rebase") with { ShortSha = "305f7f7" },
        Row("First change", 3, 3) with { ShortSha = "70e8c78" },
        Row("Theirs edits", 4, 4, "feature/theirs") with { ShortSha = "5c77ffb" },
        Row("Base", 0, 0) with { ShortSha = "195768b", Ends = [1, 2, 3, 4], Merge = false },
        Row("Initial commit", 0, 0) with { ShortSha = "6bd579f" },
    ];

    /// <summary>
    /// Stops <c>git am -3</c> of feature/theirs's commit on main, as a terminal would: notes.txt
    /// both modified, <c>rebase-apply/</c> in the git folder.
    /// </summary>
    private static async Task StopInGitAmAsync(TempRepo repo)
    {
        var patch = (await repo.GitAsync("format-patch", "-1", "--stdout", "refs/heads/feature/theirs")).StandardOutput;
        var file = repo.SiblingPath("theirs.patch");
        File.WriteAllText(file, patch);
        var am = await repo.GitAllowingFailureAsync("am", "-3", file);
        Assert.False(am.Succeeded, "git am was expected to stop with a conflict.");
        Assert.True(Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-apply")));
        Assert.Equal("UU notes.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
    }
}
