// Check S18 (docs/test-reports/phase-6.md, part S): the identity, global and per repository (D190,
// D200, design 7.4, 8.1 to 8.4, 8.6): with nothing set but user.useConfigOnly, the global scope
// empty and the repository's in-effect line in the app's words; a name with "<" refused at Save
// and an email without "@" warned about (SD4, SD25); a global identity saved into the global file,
// shown as the repository's placeholder and in effect there, and on the next commit; on a copy
// whose global file holds it (SD3), the repository's own identity saved as one operation of its
// queue, noticed in the global scope (SD8), in effect and on the next commit (SD27).
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksS
{
    /// <summary>Check S18, part A (steps 1 to 6): the global identity, refused, then saved, in effect in the repository and on a commit.</summary>
    [AvaloniaFact]
    public async Task Check_S18_identity_global()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 1" };

        // Step 1: the seed user.useConfigOnly (SD3), notes.txt changed; the screen on Git, global.
        repo.WriteFile(PartS.NotesFile, PartS.NotesThreeLines);
        using var app = await StartAsync(data, repo, PartS.SeedConfigOnly);
        var configAtStart = ConfigText(repo);
        var folder = await WorkingTreeAsync(repo);
        var start = await QuietMarkAsync(app);
        AssertGraph(app, PartS.St0, PartS.StLanes, PartS.St0wWorking);
        var screen = await app.OpenSettingsAsync();
        var opened = await QuietMarkAsync(app);
        app.ChooseSection(SettingsSection.Git);
        AssertGitGlobal(app, layout, data, repo, repositoryFolder: folder);
        await AssertScreenReadsAsync(app, start, data, repo, varExit: 128);

        // Step 2: This repository, as S14 step 5's; a scope switch reads nothing.
        layout.Step = "step 2";
        app.ChooseScope(SettingsScope.Repository);
        AssertGitRepository(app, layout, folder, string.Empty, PartS.NotSet, string.Empty, PartS.NotSet, PartS.NoEmailSet, [PartS.UseConfigOnly], "PullNotSet", PartS.PullNothingSet(PartS.Main));
        await AssertNoCallsAsync(app, opened, "The scope switch");

        // Step 3: a name with "<" and an email without "@" in the global scope: refused at Save (SD25).
        layout.Step = "step 3";
        app.ChooseScope(SettingsScope.Global);
        app.TypeInto("UserNameBox", PartS.RefusedName);
        AssertNameRefused(app);
        app.TypeInto("UserEmailBox", PartS.WarnedEmail);
        AssertEmailWarned(app);
        var beforeSave = await QuietMarkAsync(app);
        Assert.False(await app.SaveSettingsAsync(), "Save closed the screen with a refused name.");
        ShotAtRest(app, "s18a-name-refused");
        Assert.Equal(SettingsSection.Git, screen.SelectedSection);
        Assert.True(screen.Git.IsGlobalScope, "The refused Save does not show the global scope.");
        AssertNameRefused(app);
        AssertEmailWarned(app);
        AssertNav(app, "SettingsNavGit", "SettingsNavGit");
        await AssertFocusAsync(app, "UserNameBox");
        await AssertNoCallsAsync(app, beforeSave, "The refused Save");
        Assert.Equal(PartS.SeedConfigOnly, GlobalText(data));

        // Step 4: corrected as typed (SD9); Save writes the two global keys, reads back, closes.
        layout.Step = "step 4";
        app.ReplaceText("UserNameBox", PartS.AnnName);
        app.ReplaceText("UserEmailBox", PartS.AnnEmail);
        var snapshot = app.SettingsSnapshot();
        AssertHidden(snapshot, "UserNameError");
        AssertHidden(snapshot, "UserEmailWarning");
        AssertNav(app, "SettingsNavGit");
        var status = app.Session.Shell.ActiveTab.OperationStatus.Text;
        var operations = OperationCount(app);
        beforeSave = await QuietMarkAsync(app);
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForScreenClosedAsync(app, screen);
        await AssertWritesAsync(app, beforeSave, data, repo, (PartS.WriteGlobalAnnName, "data"), (PartS.WriteGlobalAnnEmail, "data"));
        await AssertScreenReadsAsync(app, beforeSave, data, repo, varExit: 0, ident: PartS.AnnIdent, others: IsWrite, allowWrites: true);
        AssertReadsAfterWrites(app, beforeSave);
        Assert.Equal(PartS.SeedAnnExample, GlobalText(data));
        Assert.Equal(configAtStart, ConfigText(repo));
        Assert.Equal(operations, OperationCount(app));
        Assert.Equal(status, app.Session.Shell.ActiveTab.OperationStatus.Text);
        AssertUndo(app, PartS.NothingToUndo);

        // Step 5: the global scope shows the values; the repository's placeholders and in-effect line follow them.
        layout.Step = "step 5";
        var step5 = await QuietMarkAsync(app);
        screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        AssertGitGlobal(app, layout, data, repo, name: PartS.AnnName, email: PartS.AnnEmail, repositoryFolder: folder);
        app.ChooseScope(SettingsScope.Repository);
        ShotAtRest(app, "s18b-repository-global-identity");
        AssertGitRepository(app, layout, folder, string.Empty, PartS.SameAsGlobalValue(PartS.AnnName), string.Empty, PartS.SameAsGlobalValue(PartS.AnnEmail), PartS.CommitsMadeAs(PartS.AnnIdent), [PartS.UseConfigOnly], "PullNotSet", PartS.PullNothingSet(PartS.Main));
        await EscapeScreenAsync(app);
        AssertNoWrite(app, step5);

        // Step 6: Stage all and a commit: made as Ann Example (D72's dates).
        layout.Step = "step 6";
        await CommitThirdNoteAsync(app, repo, layout);
        Shot(app, "s18c-committed-as-global");
        await AssertThirdNoteAsync(app, repo, PartS.GlobalCommitSha, PartS.AnnIdent, PartS.St1);
        Assert.Equal(PartS.SeedAnnExample, GlobalText(data));
        layout.AssertNone();
    }

    /// <summary>Check S18, part B (steps 7 to 9): on a copy whose global file holds Ann Example (SD3), the repository's own identity saved as an operation, noticed globally, in effect and on a commit.</summary>
    [AvaloniaFact]
    public async Task Check_S18_identity_per_repository()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        var layout = new Layout { Step = "step 7" };

        // Step 7: the global file part A's step 4 writes; the repository's own identity saved.
        repo.WriteFile(PartS.NotesFile, PartS.NotesThreeLines);
        using var app = await StartAsync(data, repo, PartS.SeedAnnExample);
        var configAtStart = ConfigText(repo);
        var folder = await WorkingTreeAsync(repo);
        AssertGraph(app, PartS.St0, PartS.StLanes, PartS.St0wWorking);
        AssertUndo(app, PartS.NothingToUndo);
        var start = await QuietMarkAsync(app);
        var screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        await AssertScreenReadsAsync(app, start, data, repo, varExit: 0, ident: PartS.AnnIdent);
        app.ChooseScope(SettingsScope.Repository);
        AssertGitRepository(app, layout, folder, string.Empty, PartS.SameAsGlobalValue(PartS.AnnName), string.Empty, PartS.SameAsGlobalValue(PartS.AnnEmail), PartS.CommitsMadeAs(PartS.AnnIdent), [PartS.UseConfigOnly], "PullNotSet", PartS.PullNothingSet(PartS.Main));
        app.TypeInto("UserNameBox", PartS.BoName);
        app.TypeInto("UserEmailBox", PartS.BoEmail);
        var mark = app.LogMark();
        var beforeSave = await QuietMarkAsync(app);
        Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
        await WaitForOutcomeAsync(app, repo, PartS.SaveRepositorySettings, mark, OperationOutcome.Succeeded);
        await WaitForScreenClosedAsync(app, screen);
        Assert.Single(app.LogLinesSince(mark), line => line.Contains($"{PartS.Settings}: {PartS.SaveRepositorySettings}: Succeeded after ", StringComparison.Ordinal) && line.EndsWith(" ms: " + PartS.SavedRepositorySettings, StringComparison.Ordinal));
        await AssertWritesAsync(app, beforeSave, data, repo, (PartS.WriteLocalBoName, "repo"), (PartS.WriteLocalBoEmail, "repo"));
        await AssertScreenReadsAsync(app, beforeSave, data, repo, varExit: 0, ident: PartS.BoIdent, others: call => IsWrite(call) || call.OperationId is not null, allowWrites: true);
        AssertReadsAfterWrites(app, beforeSave);
        AssertStatus(app, PartS.SavedRepositorySettings);
        Assert.Equal(configAtStart + PartS.LocalIdentitySection, ConfigText(repo));
        Assert.Equal(PartS.SeedAnnExample, GlobalText(data));
        AssertUndo(app, PartS.NothingToUndo);

        // Step 8: the global scope keeps the file's values with SD8's notices; the repository shows its own.
        layout.Step = "step 8";
        var step8 = await QuietMarkAsync(app);
        screen = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        ShotAtRest(app, "s18d-global-repository-sets-its-own");
        AssertGitGlobal(
            app,
            layout,
            data,
            repo,
            name: PartS.AnnName,
            email: PartS.AnnEmail,
            nameOrigins: [PartS.SetsItsOwn(PartS.Settings, PartS.BoName)],
            emailOrigins: [PartS.SetsItsOwn(PartS.Settings, PartS.BoEmail)],
            repositoryFolder: folder);
        foreach (var id in new[] { "UserNameOrigin", "UserEmailOrigin" })
        {
            var notice = app.Window.GetVisualDescendants().OfType<TextBlock>().Single(block => AutomationProperties.GetAutomationId(block) == id && block.IsEffectivelyVisible);
            Assert.Equal(PaletteOf(app).TextSecondary, (notice.Foreground as ISolidColorBrush)?.Color);
        }

        app.ChooseScope(SettingsScope.Repository);
        ShotAtRest(app, "s18e-repository-own-identity");
        AssertGitRepository(app, layout, folder, PartS.BoName, null, PartS.BoEmail, null, PartS.CommitsMadeAs(PartS.BoIdent), [PartS.UseConfigOnly], "PullNotSet", PartS.PullNothingSet(PartS.Main));
        await EscapeScreenAsync(app);
        AssertNoWrite(app, step8);

        // Step 9: Stage all and a commit: made as Bo Local.
        layout.Step = "step 9";
        await CommitThirdNoteAsync(app, repo, layout);
        Shot(app, "s18f-committed-as-repository");
        await AssertThirdNoteAsync(app, repo, PartS.RepositoryCommitSha, PartS.BoIdent, PartS.St1r);
        Assert.Equal(PartS.SeedAnnExample, GlobalText(data));
        layout.AssertNone();
    }

    /// <summary><c>UserNameError</c> <c>A name cannot contain &lt;, &gt; or a line break.</c> under the name, in the danger colour.</summary>
    private static void AssertNameRefused(ShellDriver app)
    {
        AssertText(app.SettingsSnapshot(), "UserNameError", PartS.NameRefused);
        Assert.Equal(PaletteOf(app).Danger, (Named<TextBlock>(app, "UserNameError").Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary><c>UserEmailWarning</c> <c>This does not look like an email address.</c> (<c>TextBlock.dialogWarning</c>, the warning colour) under the email; no <c>UserEmailError</c>.</summary>
    private static void AssertEmailWarned(ShellDriver app)
    {
        var snapshot = app.SettingsSnapshot();
        AssertText(snapshot, "UserEmailWarning", PartS.EmailWarning);
        AssertHidden(snapshot, "UserEmailError");
        var warning = Named<TextBlock>(app, "UserEmailWarning");
        Assert.Contains("dialogWarning", warning.Classes);
        Assert.Equal(PaletteOf(app).Warning, (warning.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>The read back comes after the writes (7.8 step 6): every read since <paramref name="sequence"/> starts after the last write ended.</summary>
    private static void AssertReadsAfterWrites(ShellDriver app, long sequence)
    {
        var calls = app.CallsSince(sequence).OrderBy(call => call.Sequence).ToList();
        var lastWrite = calls.Last(IsWrite).Sequence;
        var reads = calls.Where(call => call.CommandText == PartS.ConfigListCommand || call.CommandText.StartsWith("git var ", StringComparison.Ordinal)).ToList();
        Assert.True(reads.Count > 0 && reads.All(call => call.Sequence > lastWrite), "The read back did not come after the writes: " + string.Join(" | ", calls.Select(call => call.CommandText)));
    }

    /// <summary>S18's commit (steps 6 and 9, SD5): the working-changes row, Stage all, <c>Add a third note</c>, Commit; waited for until "Commit" succeeded.</summary>
    private static async Task CommitThirdNoteAsync(ShellDriver app, TempRepo repo, Layout layout)
    {
        app.Click(app.WorkingRowPoint());
        layout.Near(PartS.StRowPoint(0).X, app.WorkingRowPoint().X, "the working-changes row's click point's x");
        layout.Near(PartS.StRowPoint(0).Y, app.WorkingRowPoint().Y, "the working-changes row's click point's y");
        await app.WaitForAsync(() => Repo(app).IsWorkingRowSelected, "the working-changes row to be selected");
        await app.WaitForIdleAsync();
        app.Click(app.FindByAutomationId("StageAllButton"));
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => Repo(app).Changes.Staged.Files.Count == 1 && Repo(app).Changes.Unstaged.Files.Count == 0, "notes.txt to be staged");
        Assert.Equal(["M notes.txt"], Repo(app).Changes.Staged.Rows.Where(row => row.IsFile).Select(row => $"{row.StatusLetter} {row.Key}"));
        app.TypeInto("CommitSummary", PartS.ThirdNote);
        var button = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal(PartS.CommitOneFile, button.Content);
        Assert.Equal(PartS.CommitOneFileTip, ToolTip.GetTip(button) as string);
        var mark = app.LogMark();
        var sequence = await QuietMarkAsync(app);
        app.Click(button);
        await WaitForOutcomeAsync(app, repo, PartS.CommitTitle, mark, OperationOutcome.Succeeded);
        var commit = app.CallsSince(sequence).Where(call => call.CommandText == PartS.CommitCommand).ToList();
        Assert.True(commit.Count == 1 && commit[0].ExitCode == 0, $"The commit ran {commit.Count} times: " + string.Join(" | ", commit.Select(call => $"{call.CommandText} → {call.ExitCode}")));
    }

    /// <summary>The third note committed (S18 steps 6 and 9): main at <paramref name="sha"/> as <paramref name="ident"/> with D72's dates, +2 -1, clean; the graph, its row 0 selected, the details, Undo.</summary>
    private static async Task AssertThirdNoteAsync(ShellDriver app, TempRepo repo, string sha, string ident, IReadOnlyList<CommitRow> graph)
    {
        Assert.Equal(sha, await repo.HeadAsync());
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal([PartS.LocalWorkSha], await GitLinesAsync(repo, "log", "-1", "--format=%P", "HEAD"));
        Assert.Equal([ident, ident], await GitLinesAsync(repo, "log", "-1", "--format=%an <%ae>%n%cn <%ce>", "HEAD"));
        Assert.All(await GitLinesAsync(repo, "log", "-1", "--format=%aI%n%cI", "HEAD"), date => AssertGitDate(AppCommitTime, date));
        Assert.Equal([PartS.ThirdNote], await GitLinesAsync(repo, "log", "-1", "--format=%B", "HEAD"));
        Assert.Equal("+2 -1", await AheadBehindAsync(repo));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain=v1"));
        AssertGraph(app, graph, PartS.StLanes, working: null);
        Assert.Equal(0, Repo(app).SelectedIndex);
        await AssertCommitDetailsAsync(app, PartS.ThirdNote, ident, PartS.AppCommitDate, [PartS.LocalWorkSha[..7]], PartS.ChangedFilesOne, ["M notes.txt"]);
        AssertUndo(app, PartS.UndoCommit(PartS.ThirdNote));
    }

    /// <summary>The details panel: the subject, Author, Date, no Committer and no Commit date row, the parents' links, the changed files' title and rows.</summary>
    private static async Task AssertCommitDetailsAsync(ShellDriver app, string subject, string author, string date, IReadOnlyList<string> parents, string title, IReadOnlyList<string> files)
    {
        await AssertDetailsAsync(app, subject, title, files);
        string? Shown(string id) => app.Window.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.Name == id && control.IsEffectivelyVisible) switch
        {
            SelectableTextBlock block => block.Text,
            TextBlock block => block.Text,
            _ => null,
        };
        Assert.Equal(author, Shown("DetailsAuthor"));
        Assert.Equal(date, Shown("DetailsDate"));
        Assert.Null(Shown("DetailsCommitter"));
        Assert.Null(Shown("DetailsCommitDate"));
        var links = app.Window.GetVisualDescendants().OfType<Button>().Where(button => AutomationProperties.GetAutomationId(button) == "ParentLink" && button.IsEffectivelyVisible).Select(button => AutomationProperties.GetName(button) ?? string.Empty).ToList();
        Assert.Equal(parents, links);
    }
}
