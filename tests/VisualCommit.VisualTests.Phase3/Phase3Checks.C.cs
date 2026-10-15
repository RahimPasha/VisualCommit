// Part C of phase 3's scripted walk-through (docs/test-reports/phase-3.md, "Part C: branches,
// checkout with local changes, stashes, tags and the in-progress banners"): checks 13, 13b, 14,
// 14b, 15, 15b, 16 and 25. This class is part C's alone; its expected texts (T1 to T7, the
// scenarios' graphs) go into Phase3Expectations.C.cs (Phase3Expectations.PartC), and further files
// of its own may hold more of the class (Phase3Checks.C.<topic>.cs). Everything the checks share is
// in Phase3Checks.Support.cs; read its header for the conventions (one test method per app start,
// pictures through Shot, no fixed waits). "Select all and type X" is ShellDriver.ReplaceText; the
// error dialogs are asserted with AssertErrorDialogAsync (git's texts word for word on Git 2.36
// only).
//
// This file: checks 13 and 13b (branches). Phase3Checks.C.Checkout.cs: 14 and 14b.
// Phase3Checks.C.Stash.cs: 15 and 15b. Phase3Checks.C.Tags.cs: 16. Phase3Checks.C.Banners.cs: 25.
// Phase3Checks.C.Helpers.cs: what they share.
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The checks of part C: branches, checkout with local changes, stashes, tags and the in-progress banners.</summary>
public sealed partial class Phase3ChecksC : Phase3Checks
{
    /// <summary>
    /// Check 13: create, rename and delete branches on the ops scenario: the commit's menu, the
    /// name dialog's validation (folder clashes, names that exist, D96's case, git's and the app's
    /// own rules), creating and checking out <c>hotfix</c>, a branch's menu and renaming it, both
    /// forms of the delete question (Cancel, then Delete with its backup ref), and the current
    /// branch's disabled "Delete" with its tooltip.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_13_create_rename_delete_branches()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the commit's menu.
        var pointer = app.GraphRowPoint(3);
        app.RightClick(pointer);
        var menu = app.Menu();
        await WaitForDetailsAsync(app, "Main work");
        var shot = Shot(app, "13a-commit-menu");
        AssertSelection(app, shot, 3);
        AssertMenuAt(app, menu, shot, pointer, "Check out this commit", PartC.Check13CommitMenu);
        Assert.Equal(284, menu.Bounds.Height, 2.0);

        // Step 2: the empty name dialog.
        app.ClickMenuItem("Create branch here…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        Shot(app, "13b-name-dialog-empty");
        Assert.False(app.IsMenuOpen);
        await AssertNameDialogAsync(app, "Create branch", PartC.Check13Context, string.Empty, "Branch name", string.Empty, "Check out the new branch", true, "Create branch", false);

        // Step 3: a folder clash.
        app.TypeInto("NameBox", "feature");
        Shot(app, "13c-folder-clash");
        AssertNameError(app, PartC.FolderClash);
        AssertDialogButtons(app, "Create branch", false);

        // Step 4: the other names, each read without a picture.
        var caseInsensitive = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        foreach (var (name, error) in PartC.Check13Names(caseInsensitive))
        {
            app.ReplaceText("NameBox", name);
            AssertNameError(app, error ?? string.Empty);
            AssertDialogButtons(app, "Create branch", error is null);
        }

        // Step 5: a valid name, then "Create branch".
        app.ReplaceText("NameBox", "hotfix");
        Shot(app, "13d-name-valid");
        AssertNameError(app, string.Empty);
        AssertDialogButtons(app, "Create branch", true);
        var calls = app.GitCalls().Count;
        ClickDialogButton(app, "ConfirmButton");
        await app.WaitForIdleAsync();
        shot = Shot(app, "13e-hotfix-created");
        await AssertNoDialogAsync(app);
        AssertCalled(app, calls, "git switch --create hotfix " + PartC.MainWorkSha);
        Assert.Equal(["## hotfix"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal(["feature/alpha", "feature/conflict", "feature/ff", "* hotfix", "main"], await BranchesAsync(repo));
        AssertGraph(app, shot, PartC.Check13Hotfix, lanes: 3);
        AssertSelection(app, shot, 3);
        AssertBranch(app, "hotfix");
        AssertStatus(app, "Created hotfix and checked it out");
        Assert.Equal(5, SectionRow(app, "Local branches").Count);
        Assert.Equal(["feature", "hotfix", "main"], SectionLines(app, "Local branches"));
        await AssertBranchRowAsync(app, ["Local branches", "hotfix"], head: true);
        await AssertBranchRowAsync(app, ["Local branches", "main"], head: false);
        AssertNoClippedText(app);

        // Step 6: a branch's menu from its label, then Rename.
        pointer = app.GraphLabelBounds(1, "feature/conflict").Center;
        app.RightClick(pointer);
        menu = app.Menu();
        await WaitForDetailsAsync(app, "Conflicting change");
        shot = Shot(app, "13f-branch-menu");
        AssertSelection(app, shot, 1);
        AssertMenuAt(app, menu, shot, pointer, "Check out feature/conflict", PartC.Check13BranchMenu);
        app.ClickMenuItem("Rename feature/conflict…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        Shot(app, "13g-rename-dialog");
        await AssertNameDialogAsync(app, "Rename branch", "Rename feature/conflict", "feature/conflict", null, string.Empty, null, false, "Rename", false);
        var box = (Avalonia.Controls.TextBox)app.FindByAutomationId("NameBox");
        Assert.Equal(0, Math.Min(box.SelectionStart, box.SelectionEnd));
        Assert.Equal("feature/conflict".Length, Math.Max(box.SelectionStart, box.SelectionEnd));

        // Step 7: a name that exists, then a new one, then "Rename".
        app.ReplaceText("NameBox", "feature/alpha");
        AssertNameError(app, "A branch named feature/alpha exists.");
        AssertDialogButtons(app, "Rename", false);
        app.ReplaceText("NameBox", "feature/conflicting");
        AssertNameError(app, string.Empty);
        AssertDialogButtons(app, "Rename", true);
        calls = app.GitCalls().Count;
        ClickDialogButton(app, "ConfirmButton");
        await app.WaitForIdleAsync();
        shot = Shot(app, "13h-renamed");
        await AssertNoDialogAsync(app);
        AssertCalled(app, calls, "git branch --move feature/conflict feature/conflicting");
        Assert.Equal(["feature/alpha", "feature/conflicting", "feature/ff", "* hotfix", "main"], await BranchesAsync(repo));
        AssertGraph(app, shot, PartC.Check13Renamed, lanes: 3);
        AssertSelection(app, shot, 1);
        AssertStatus(app, "Renamed feature/conflict to feature/conflicting");

        // Step 8: main's menu from the left panel, its delete question, Cancel.
        var mainRow = await app.RefRowAsync("Local branches", "main");
        pointer = app.BoundsOf(mainRow).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        var menuShot = app.Capture();
        AssertMenuAt(app, menu, menuShot, pointer, "Check out main", PartC.Check13MainMenu);
        AssertNoLeftPanelSelection(app);
        AssertSelection(app, null, 1);
        app.ClickMenuItem("Delete main…");
        await AssertConfirmationAsync(app, "Delete branch?", "Delete the branch main?", PartC.BackupDetail, "Delete");
        Shot(app, "13i-delete-main-question");
        Assert.Equal(0, (await repo.GitAllowingFailureAsync("merge-base", "--is-ancestor", "refs/heads/main", "HEAD")).ExitCode);
        ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Assert.NotNull(await repo.RefTargetAsync("refs/heads/main"));
        Assert.Empty(await repo.BackupRefsAsync());

        // Step 9: feature/ff's delete question, then Delete.
        pointer = app.GraphLabelBounds(2, "feature/ff").Center;
        app.RightClick(pointer);
        app.Menu();
        app.ClickMenuItem("Delete feature/ff…");
        await AssertConfirmationAsync(app, "Delete branch?", "Delete the branch feature/ff? It has commits that hotfix does not have.", PartC.BackupDetail, "Delete");
        Shot(app, "13j-delete-ff-question");

        // Behind the dialog's backdrop: the selection is read, not sampled.
        AssertSelection(app, null, 2);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge-base", "--is-ancestor", "refs/heads/feature/ff", "HEAD")).ExitCode);
        calls = app.GitCalls().Count;
        ClickDialogButton(app, "ConfirmButton");
        await app.WaitForIdleAsync();
        shot = Shot(app, "13k-ff-deleted");
        await AssertNoDialogAsync(app);
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal("branch", backup.Ref.Kind);
        Assert.False(backup.Ref.IsChanges);
        Assert.Equal("feature/ff", backup.Ref.Name);
        Assert.StartsWith(PartC.FastForwardShort, backup.Sha, StringComparison.Ordinal);
        var delete = AssertCalled(app, calls, "git branch --delete --force feature/ff");
        Assert.Contains("Deleted branch feature/ff (was d93814d).", delete.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(["feature/alpha", "feature/conflicting", "* hotfix", "main"], await BranchesAsync(repo));
        AssertGraph(app, shot, PartC.Check13FfDeleted, lanes: 3);
        AssertSelection(app, shot, null);
        Assert.Equal("Select a commit to see its details.", RightPanelText(app, "DetailsPlaceholder"));
        AssertStatus(app, "Deleted feature/ff");
        Assert.Equal(4, SectionRow(app, "Local branches").Count);

        // Step 10: the current branch's menu; the pointer rests on the disabled "Delete hotfix…".
        pointer = app.GraphLabelBounds(2, "hotfix").Center;
        app.RightClick(pointer);
        menu = app.Menu();
        menuShot = app.Capture();
        AssertMenuAt(app, menu, menuShot, pointer, "Push hotfix…", PartC.Check13HotfixMenu);
        Assert.Equal(302, menu.Bounds.Height, 2.0);
        var tip = await app.RestPointerOnMenuItemAsync("Delete hotfix…");
        shot = Shot(app, "13l-delete-current-disabled", keepPointer: true);
        AssertSelection(app, shot, 2);
        Assert.Equal(PartC.DeleteCurrentReason, tip);
        AssertHighlighted(app.Menu(), "Push hotfix…");
        AssertToolTipInsideWindow(app, PartC.DeleteCurrentReason);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, null);
        Assert.Equal(["feature/alpha", "feature/conflicting", "* hotfix", "main"], await BranchesAsync(repo));
    }

    /// <summary>
    /// Check 13b: a branch made from a remote branch (with its upstream) and from a tag (without
    /// one), neither checked out, and a folder name in another case (D96).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_13b_branch_from_remote_branch_and_tag()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: open Remotes › origin › feature, right-click its alpha.
        app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
        await app.WaitForAsync(() => ItemRow(app, "Remotes", "origin", "feature").IsOpen, "the folder origin › feature to open");
        var row = await app.RefRowAsync("Remotes", "origin", "feature", "alpha");
        var pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "13b1-remote-branch-menu");
        AssertMenuAt(app, menu, shot, pointer, "Check out origin/feature/alpha", PartC.Check13bRemoteMenu);
        AssertSelection(app, shot, null);
        AssertNoLeftPanelSelection(app);

        // Step 2: a folder in another case.
        app.ClickMenuItem("Create branch here…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        app.TypeInto("NameBox", "Feature/beta");
        Shot(app, "13b2-folder-case");
        var caseInsensitive = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        await AssertNameDialogAsync(app, "Create branch", "From origin/feature/alpha", "Feature/beta", "Branch name", caseInsensitive ? PartC.FolderClash : string.Empty, "Check out the new branch", true, "Create branch", !caseInsensitive);

        // Step 3: alpha-copy, not checked out.
        app.ReplaceText("NameBox", "alpha-copy");
        app.Click(app.FindByAutomationId(app.TopDialogView!, "NameDialogOption"));
        Assert.False(((Avalonia.Controls.CheckBox)app.FindByAutomationId(app.TopDialogView!, "NameDialogOption")).IsChecked);
        var calls = app.GitCalls().Count;
        ClickDialogButton(app, "ConfirmButton");
        await app.WaitForIdleAsync();
        shot = Shot(app, "13b3-alpha-copy");
        await AssertNoDialogAsync(app);
        var branch = AssertCalled(app, calls, "git branch alpha-copy refs/remotes/origin/feature/alpha");
        Assert.Contains("branch 'alpha-copy' set up to track 'origin/feature/alpha'.", branch.StandardOutput + branch.StandardError, StringComparison.Ordinal);
        Assert.Equal(["branch.alpha-copy.remote origin", "branch.alpha-copy.merge refs/heads/feature/alpha"], await GitLinesAsync(repo, "config", "--get-regexp", @"^branch\.alpha-copy\."));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        AssertBranch(app, "main");
        AssertGraph(app, shot, PartC.Check13bAlphaCopy, lanes: 3);
        AssertStatus(app, "Created alpha-copy");
        Assert.Equal(5, SectionRow(app, "Local branches").Count);
        Assert.Equal(["feature", "alpha-copy", "main"], SectionLines(app, "Local branches").Where(line => !line.StartsWith(' ')));
        await AssertBranchRowAsync(app, ["Local branches", "alpha-copy"], head: false);
        await AssertBranchRowAsync(app, ["Local branches", "main"], head: true);

        // Step 4: the tag's menu, then release-1.0 from it, not checked out.
        pointer = app.GraphLabelBounds(6, "v1.0").Center;
        app.RightClick(pointer);
        menu = app.Menu();
        var menuShot = app.Capture();
        AssertSelection(app, menuShot, 6);
        AssertMenuAt(app, menu, menuShot, pointer, "Check out v1.0", PartC.Check13bTagMenu);
        app.ClickMenuItem("Create branch here…");
        await AssertNameDialogAsync(app, "Create branch", "From v1.0", string.Empty, "Branch name", string.Empty, "Check out the new branch", true, "Create branch", false);
        app.TypeInto("NameBox", "release-1.0");
        app.Click(app.FindByAutomationId(app.TopDialogView!, "NameDialogOption"));
        calls = app.GitCalls().Count;
        ClickDialogButton(app, "ConfirmButton");
        await app.WaitForIdleAsync();
        shot = Shot(app, "13b4-release-branch");
        await AssertNoDialogAsync(app);
        AssertCalled(app, calls, "git branch release-1.0 refs/tags/v1.0^{commit}");
        Assert.StartsWith(PartC.AddCoreShort, await RevParseAsync(repo, "refs/heads/release-1.0"), StringComparison.Ordinal);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.release-1\.0\.")).ExitCode);
        AssertGraph(app, shot, PartC.Check13bRelease, lanes: 3);
        AssertStatus(app, "Created release-1.0");
        Assert.Equal(6, SectionRow(app, "Local branches").Count);
        Assert.Equal(["feature", "alpha-copy", "main", "release-1.0"], SectionLines(app, "Local branches").Where(line => !line.StartsWith(' ')));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        AssertBranch(app, "main");
    }
}
