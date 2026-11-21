using System.Globalization;
using System.Text;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Pages;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Workspaces;
using VisualCommit.Git.Prompts;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The sweep of names over the app's states (docs/phase-8a-design.md 9.3.8; the report's part U,
/// U2): one fact and one start per group of states, each state swept by
/// <see cref="AutomationSweep"/> from the window's own peer as a client reads it. A fact sweeps all
/// its states before it fails, and its failure lists every problem of every state, so that one run
/// gives the whole list. State 3, About and its license's text dialog, is swept from the merge of
/// the package that builds About (the place is marked below).
/// </summary>
public sealed class AccessibleNameSweepTests : Phase8aChecks
{
    /// <summary>(1) The welcome page of an empty tab, then the palette open on it.</summary>
    [AvaloniaFact]
    public async Task State_01_the_welcome_page_and_the_palette()
    {
        using var data = new TempDirectory("phase8a sweep");
        using var app = await StartEmptyAsync(data);
        var sweeps = new Sweeps();

        sweeps.Look(app.Window, "1: the welcome page");
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen && app.PaletteBox.IsFocused, "the palette");
        sweeps.Look(app.Window, "1: the palette");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the palette to close");

        sweeps.AssertNone();
    }

    /// <summary>(2) The settings screen in each section, the Git program row, Accounts with the three providers on, and each provider's sign-in dialog.</summary>
    [AvaloniaFact]
    public async Task State_02_settings_each_section_the_git_row_accounts_and_the_sign_in_dialogs()
    {
        using var data = new TempDirectory("phase8a sweep");
        using var fakes = new TempDirectory("phase8a sweep fakes");
        var cancel = TestContext.Current.CancellationToken;
        await using var gitHub = await FakeGitHub.StartAsync(Path.Combine(fakes.Path, "github"), ct: cancel);
        await using var azure = await FakeAzureDevOps.StartAsync(Path.Combine(fakes.Path, "azure"), ct: cancel);
        await using var gitLab = await FakeGitLab.StartAsync(Path.Combine(fakes.Path, "gitlab"), ct: cancel);
        using var app = ShellDriver.Start(data.Path, DefaultSize.Width, DefaultSize.Height, gitHub: gitHub, azureDevOps: azure, gitLab: gitLab);
        await app.WaitUntilReadyAsync();
        var sweeps = new Sweeps();

        var settings = await app.OpenSettingsAsync();
        foreach (var section in Enum.GetValues<SettingsSection>())
        {
            app.ChooseSection(section);
            await app.WaitForSettingsReadsAsync(settings);
            sweeps.Look(app.Window, $"2: settings, {section}");
        }

        app.ChooseSection(SettingsSection.Git);
        await app.WaitForAsync(() => app.SettingsSnapshot().Shows(F.ProgramPathId), "the Git program row");
        sweeps.Look(app.Window, "2: settings, the Git program row");

        app.ChooseSection(SettingsSection.Accounts);
        foreach (var provider in new[] { HostingProvider.GitHub, HostingProvider.AzureDevOps, HostingProvider.GitLab })
        {
            var dialog = await app.OpenSignInDialogAsync(provider);
            sweeps.Look(app.Window, $"2: the sign-in dialog of {provider}");
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => dialog.IsClosed, "the sign-in dialog to close");
        }

        app.CancelSettings();
        sweeps.AssertNone();
    }

    // (3) About and its license's text dialog: swept from the merge of the package that builds the
    // About section. It needs the settings screen on About (SettingsNavAbout, then SettingsAbout
    // with AboutShowLicenseButton, AboutShowNoticesButton, AboutRepositoryLink and AboutGuideLink),
    // then the license's text dialog (AboutShowLicenseButton: DialogTitle, TextDocumentSwitch with
    // TextDocumentPartMain and one TextDocumentPart<name> per vendor file, TextDocumentText,
    // TextDocumentCopyButton, ConfirmButton), each swept as "3: about" and "3: the license".

    /// <summary>(4) Every kind of dialog through the dialog host, and each kind of git's prompts through its presenter.</summary>
    [AvaloniaFact]
    public async Task State_04_every_dialog_kind()
    {
        using var data = new TempDirectory("phase8a sweep");
        using var app = await StartEmptyAsync(data);
        var dialogs = app.Session.Services.DialogHost;
        var sweeps = new Sweeps();

        var remotes = new RemotesDialogViewModel(new ListedRemotes(new RemoteInfo("origin", "https://example.com/demo/app.git", null)));
        var shown = new List<(DialogViewModel Dialog, string State)>
        {
            (remotes, "the remotes"),
            (BranchNameDialogViewModel.Create("From main", []), "the branch name"),
            (new TagDialogViewModel("At main", ["v1.0"]), "the tag"),
            (new StashDialogViewModel(true), "the stash"),
            (new ResetDialogViewModel("main", "1234567", "Main work"), "the reset"),
            (new PickDialogViewModel("Set upstream for topic", ["origin/main", "origin/topic"], null, "Set upstream"), "the pick"),
            (new ChoiceDialogViewModel("Push v1.0 to…", ["mirror", "origin"], "Push"), "the choice"),
            (new PushDialogViewModel("topic", ["mirror", "origin"]), "the push"),
            (new FinishingDialogViewModel([new FinishingItem("Fetch origin — demo", false)]), "the finishing"),
            (new ErrorDialogViewModel("Push rejected", "origin has commits that your main does not have.", "demo", "! [rejected] main -> main (fetch first)", "git push origin main", "Pull"), "the error"),
            (new MessageDialogViewModel("Rebase stopped", "The rebase stopped at a commit to edit.", "OK"), "the message"),
            (WorkspaceNameDialogViewModel.Create(["Web"]), "the workspace's name"),
            (new CloneFromGitHubDialogViewModel([HostingProvider.GitHub, HostingProvider.AzureDevOps, HostingProvider.GitLab], HostingProvider.GitHub, ListedRepositories, NullAppLog.Instance), "the repository browser"),
        };

        foreach (var (dialog, state) in shown)
        {
            var closed = dialogs.ShowAsync(dialog);
            if (ReferenceEquals(dialog, remotes))
            {
                await remotes.Loading;
            }

            if (dialog is CloneFromGitHubDialogViewModel browser)
            {
                // The welcome page starts the list when it shows the browser; here the sweep does.
                var listing = browser.Start();
                await app.WaitForAsync(() => listing.IsCompleted && browser.Rows.Count == 2, "the browser's two rows");
            }

            app.Settle();
            sweeps.Look(app.Window, "4: " + state);
            dialog.HandleEscape();
            await app.WaitForAsync(() => closed.IsCompleted, $"{state} dialog to close");
        }

        var asking = dialogs.ConfirmAsync(new ConfirmationRequest("Delete branch?", "Delete the branch feature/ff?", DialogText.BackupDetail, "Delete"));
        app.Settle();
        sweeps.Look(app.Window, "4: the confirmation");
        dialogs.CancelCurrent();
        Assert.False(await asking);

        var presenter = new PromptDialogPresenter(dialogs);
        foreach (var (kind, text) in new[]
        {
            (GitPromptKind.Username, "Username for 'https://example.com': "),
            (GitPromptKind.Password, "Password for 'https://alice@example.com': "),
            (GitPromptKind.Passphrase, FakeSsh.PassphrasePrompt),
            (GitPromptKind.HostKey, FakeSsh.HostKeyPrompt),
            (GitPromptKind.Other, FakeSsh.PinPrompt),
            (GitPromptKind.Confirm, "Allow use of key /home/demo/.ssh/id_ed25519?"),
        })
        {
            using var scope = new CancellationTokenSource();
            var answer = presenter.ShowAsync(Prompt(kind, text), scope.Token);
            app.Settle();
            sweeps.Look(app.Window, $"4: git's prompt, {kind}");
            await scope.CancelAsync();
            await app.WaitForAsync(() => answer.IsCompleted, $"the {kind} prompt to close");
        }

        sweeps.AssertNone();
    }

    /// <summary>
    /// (5) The ops tab, its states the busiest the tab has: two branches hidden, a commit with files
    /// and parent links, the graph's row, two unstaged and one staged file with one row selected and
    /// the pointer over another, the diff's hunk bars, the activity log, a context menu on the graph
    /// and on a file, one branch soloed, the restore bar after a discard.
    /// </summary>
    [AvaloniaFact]
    public async Task State_05_the_ops_tab()
    {
        using var repo = await OpsWithThreeChangesAsync();
        using var data = new TempDirectory("phase8a sweep");
        using var app = await OpenAsync(data, repo.Path);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var sweeps = new Sweeps();

        // Two branches hidden: feature/alpha, and its upstream with it.
        app.Click(await app.RefRowAsync(U.LocalBranches, U.Feature));
        app.RightClick(await app.RefRowAsync(U.LocalBranches, U.Feature, U.Alpha));
        app.ClickMenuItem(U.HideFeatureAlpha);
        await app.WaitForAsync(() => app.RefRowMarks(U.LocalBranches, U.Feature, U.Alpha).ShowsHiddenToggle, "feature/alpha hidden");
        app.Click(await app.RefRowAsync(U.Remotes, U.Origin, U.Feature));
        await app.WaitForAsync(() => app.RefRowMarks(U.Remotes, U.Origin, U.Feature, U.Alpha).ShowsHiddenToggle, "origin/feature/alpha hidden");
        await app.WaitForIdleAsync();

        // A commit with files and parent links, its row exposed.
        app.Click(await app.RefRowAsync(U.LocalBranches, U.Main));
        await app.WaitForAsync(() => repository.SelectedCommits is [{ Subject: "Main work" }], "Main work selected");
        app.WaitForCommitDetails();
        sweeps.Look(app.Window, "5: a commit with files and parent links");

        // The working-changes row: README.md selected with its diff, the pointer over the untracked file's row.
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => app.StageRow(U.UnstagedListId, U.Untracked) is not null && app.StageRow(U.StagedListId, U.Core) is not null, "the stage panel's rows");
        app.Click(app.StageRow(U.UnstagedListId, U.Readme)!);
        await app.WaitForAsync(() => repository.Diff is { ShowsText: true } diff && diff.Target.Path == U.Readme, "README.md's diff");
        app.PressKey(Key.J, RawInputModifiers.Control);
        await app.WaitForAsync(() => app.FindByAutomationId("ActivityLogClose").IsEffectivelyVisible, "the activity log");
        app.MoveMouse(app.StageRow(U.UnstagedListId, U.Untracked)!);
        await app.WaitForIdleAsync();
        sweeps.Look(app.Window, "5: two unstaged and one staged, one selected and one hovered, the diff, the log");

        // A context menu on the graph and on a file.
        app.RightClick(app.GraphRowPoint(U.MainWork.DisplayRow - 1));
        _ = app.Menu();
        sweeps.Look(app.Window, "5: the graph's menu");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => app.StageRow(U.UnstagedListId, U.Untracked) is not null, "the stage panel");
        app.RightClick(ShellDriver.RowButton(app.StageRow(U.UnstagedListId, U.Untracked)!, "StageFileRow"));
        _ = app.Menu();
        sweeps.Look(app.Window, "5: a file's menu");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // One branch soloed.
        app.RightClick(await app.RefRowAsync(U.LocalBranches, U.Feature, U.Ff));
        app.ClickMenuItem(U.SoloFeatureFf);
        await app.WaitForAsync(() => app.RefRowMarks(U.LocalBranches, U.Feature, U.Ff).ShowsSoloToggle, "feature/ff soloed");
        await app.WaitForIdleAsync();
        sweeps.Look(app.Window, "5: one branch soloed");

        // The restore bar after a discard of README.md.
        app.Click(app.StageRow(U.UnstagedListId, U.Readme)!);
        await app.WaitForAsync(() => repository.Diff is { ShowsText: true } diff && diff.Target.Path == U.Readme, "README.md's diff");
        app.Click(ShellDriver.RowButton(app.StageRow(U.UnstagedListId, U.Readme)!, "RowDiscardButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => app.FindByAutomationId("RestoreBarText") is TextBlock { IsEffectivelyVisible: true, Text: U.RestoreText }, "the restore bar");
        await app.WaitForIdleAsync();
        sweeps.Look(app.Window, "5: the restore bar");

        sweeps.AssertNone();
    }

    /// <summary>(6) The search bar, a branch comparison, a file's history, its blame and the reflog.</summary>
    [AvaloniaFact]
    public async Task State_06_search_compare_file_history_blame_and_reflog()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile(U.Readme, U.ReadmeChanged);
        using var data = new TempDirectory("phase8a sweep");
        using var app = await OpenAsync(data, repo.Path);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var sweeps = new Sweeps();

        app.PressSearch();
        await app.WaitForAsync(() => repository.Search is not null && app.FindByAutomationId("SearchBox").IsFocused, "the search bar");
        app.Type(U.Alpha);
        await app.WaitForSearchAsync();
        sweeps.Look(app.Window, "6: the search");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => repository.Search is null, "the search bar to close");

        app.Click(await app.RefRowAsync(U.LocalBranches, U.Feature));
        app.RightClick(await app.RefRowAsync(U.LocalBranches, U.Feature, U.Conflict));
        app.ClickMenuItem(U.CompareConflictWithMain);
        await app.WaitForComparisonShownAsync();
        sweeps.Look(app.Window, "6: the comparison");
        app.Click(app.FindByAutomationId("CompareClose"));
        await app.WaitForAsync(() => app.Compare() is null, "the comparison to close");

        app.Click(await app.RefRowAsync(U.LocalBranches, U.Main));
        await app.WaitForAsync(() => repository.SelectedCommits is [{ Subject: "Main work" }], "Main work selected");
        app.WaitForCommitDetails();
        app.RightClick(await app.FileRowAsync("FileList", U.Core));
        app.ClickMenuItem("File history");
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(2);
        sweeps.Look(app.Window, "6: the file history");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null, "the file history to close");

        app.Click(app.WorkingRowPoint());
        app.RightClick(await app.FileRowAsync(U.UnstagedListId, U.Readme));
        app.ClickMenuItem("Blame");
        await app.WaitForPageAsync<BlameView>();
        await app.WaitForAsync(() => app.BlameSnapshot() is { IsLoading: false }, "the blame");
        sweeps.Look(app.Window, "6: the blame");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null, "the blame to close");

        app.PressKey(Key.R, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForPageAsync<ReflogView>();
        await app.WaitForAsync(() => app.ReflogSnapshot() is { IsLoading: false }, "the reflog");
        sweeps.Look(app.Window, "6: the reflog");

        sweeps.AssertNone();
    }

    /// <summary>(7) The interactive rebase list.</summary>
    [AvaloniaFact]
    public async Task State_07_the_rebase_list()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("phase8a sweep");
        using var app = await OpenAsync(data, repo.Path);
        var sweeps = new Sweeps();

        app.RightClick(app.GraphRowPoint(6));
        app.ClickMenuItem("Interactive rebase of main from here…");
        await app.WaitForRebaseListAsync();
        sweeps.Look(app.Window, "7: the rebase list");
        app.ClickRebaseCancel();

        sweeps.AssertNone();
    }

    /// <summary>(8) The conflict resolver on a text file and its card on a file without text, under the merge's banner.</summary>
    [AvaloniaFact]
    public async Task State_08_the_resolver_its_card_and_the_banner()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.MergeKinds);
        using var data = new TempDirectory("phase8a sweep");
        using var app = await OpenAsync(data, repo.Path);
        var sweeps = new Sweeps();

        app.Click(app.WorkingRowPoint());
        app.DoubleClick(await app.FileRowAsync(U.UnstagedListId, "image.png"));
        await app.WaitForResolverAsync("image.png");
        await app.WaitForIdleAsync();
        sweeps.Look(app.Window, "8: the resolver's card and the banner");

        // The text file by the resolver's own "Next file", at most once per conflicted file.
        for (var next = 0; FileInResolver(app) != "new.txt"; next++)
        {
            Assert.True(next < 3, $"Three Next file did not reach new.txt: the resolver shows {FileInResolver(app)}.");
            var shown = FileInResolver(app);
            app.ClickResolverButton("ResolverNextFile");
            await app.WaitForAsync(() => FileInResolver(app) != shown, $"Next file to leave {shown}");
            await app.WaitForResolverAsync();
        }

        await app.WaitForResolverAsync("new.txt");
        sweeps.Look(app.Window, "8: the resolver and the banner");

        sweeps.AssertNone();
    }

    /// <summary>(9) A workspace's dashboard, and its row's menu.</summary>
    [AvaloniaFact]
    public async Task State_09_the_dashboard()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("phase8a sweep");
        using var app = ShellDriver.StartWithWorkspace(data.Path, new WorkspaceList { Workspaces = [new Workspace("web", "Web", [new WorkspaceRepository(repo.Path)])] }, openDashboardOf: "web");
        await app.WaitUntilReadyAsync();
        await app.WaitForDashboardReadAsync();
        var sweeps = new Sweeps();

        sweeps.Look(app.Window, "9: the dashboard");
        _ = await app.RowMenuAsync(Path.GetFileName(repo.Path));
        sweeps.Look(app.Window, "9: a dashboard row's menu");
        app.PressKey(Key.Escape);

        sweeps.AssertNone();
    }

    /// <summary>(10) First run on Windows: git missing, then by "Check again" the chosen git gone, a git too old and one that does not work, with git's words.</summary>
    [AvaloniaFact]
    public async Task State_10_first_run_on_windows_from_missing_through_check_again()
    {
        using var data = new TempDirectory("phase8a sweep");
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { GitExecutable = F.Chosen });
        var probe = new FakeGitSetupProbe(
            FakeGitSetupProbe.Windows(),
            FakeGitAnswer.Missing,
            FakeGitAnswer.ChosenMissing(F.WindowsGit),
            FakeGitAnswer.TooOld(F.WindowsGit),
            FakeGitAnswer.NotWorking(F.WindowsGit, NotWorkingWords, 128));
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        var sweeps = new Sweeps();
        var tooOld = string.Format(CultureInfo.InvariantCulture, F.TooOldTitleFormat, F.TooOldVersion);
        var tooOldBody = string.Format(CultureInfo.InvariantCulture, F.TooOldBodyFormat, GitVersion.Minimum, F.TooOldVersion);

        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceWindows);
        LookAtTheScreen(app, sweeps, "10: missing on Windows", F.MissingTitle, F.MissingBodyWindows);

        await CheckAgainAsync(app, probe, F.ChosenMissingTitle);
        LookAtTheScreen(app, sweeps, "10: the chosen git gone", F.ChosenMissingTitle, F.ChosenMissingBody);

        await CheckAgainAsync(app, probe, tooOld);
        LookAtTheScreen(app, sweeps, "10: too old", tooOld, tooOldBody);

        await CheckAgainAsync(app, probe, F.NotWorkingTitle);
        Assert.Equal(NotWorkingWords, app.GitSetupText(F.GitSaidId));
        LookAtTheScreen(app, sweeps, "10: not working, with git's words", F.NotWorkingTitle, F.NotWorkingBody);

        sweeps.AssertNone();
    }

    /// <summary>(11) First run on macOS: git missing.</summary>
    [AvaloniaFact]
    public async Task State_11_first_run_missing_on_macos()
    {
        using var data = new TempDirectory("phase8a sweep");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.MacOS, FakeGitAnswer.Missing);
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        var sweeps = new Sweeps();

        await app.WaitForGitSetupAsync(guidance: F.MissingGuidanceMacOS);
        LookAtTheScreen(app, sweeps, "11: missing on macOS", F.MissingTitle, F.MissingBodyMacOS);

        sweeps.AssertNone();
    }

    /// <summary>(12) First run on Linux: git missing on a known distribution.</summary>
    [AvaloniaFact]
    public async Task State_12_first_run_missing_on_linux()
    {
        using var data = new TempDirectory("phase8a sweep");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Linux(FakeGitSetupProbe.Ubuntu), FakeGitAnswer.Missing);
        using var app = await StartEmptyAsync(data, gitProbe: probe);
        var sweeps = new Sweeps();

        await app.WaitForGitSetupAsync(guidance: string.Format(CultureInfo.InvariantCulture, F.MissingGuidanceLinuxKnownFormat, F.UbuntuPrettyName));
        LookAtTheScreen(app, sweeps, "12: missing on Linux", F.MissingTitle, F.MissingBodyLinux);

        sweeps.AssertNone();
    }

    /// <summary>(13) First run with git found and the helper for git's questions missing.</summary>
    [AvaloniaFact]
    public async Task State_13_first_run_with_the_helper_missing()
    {
        using var data = new TempDirectory("phase8a sweep");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(F.WindowsGit));
        using var app = await StartEmptyAsync(data, gitProbe: probe, askPassHelper: F.MissingHelper);
        var sweeps = new Sweeps();

        await app.WaitForGitSetupAsync(guidance: F.HelperMissingGuidance);
        LookAtTheScreen(app, sweeps, "13: the helper missing", F.HelperMissingTitle, F.HelperMissingBody);

        sweeps.AssertNone();
    }

    /// <summary>(14) The notice of a second start whose first instance did not answer.</summary>
    [AvaloniaFact]
    public void State_14_the_instance_notice()
    {
        using var host = InstanceNoticeHost.Show(GuidancePlatform.Windows, I.WindowsFolder, ThemeVariant.Dark);
        var sweeps = new Sweeps();

        sweeps.Look(host.Window, "14: the instance notice");

        sweeps.AssertNone();
    }

    /// <summary>The words git says in state 10's last answer, made up.</summary>
    private const string NotWorkingWords = "fatal: unable to read C:/Users/demo/.gitconfig: Permission denied";

    /// <summary>
    /// "ops" with method 3's changes (the report's part U, open point 79): README.md changed,
    /// src/core.txt changed and staged, the untracked u-untracked.txt.
    /// </summary>
    internal static async Task<TempRepo> OpsWithThreeChangesAsync()
    {
        var repo = await Scenarios.OperationsAsync();
        try
        {
            repo.WriteFile(U.Readme, U.ReadmeChanged);
            repo.WriteFile(U.Core, U.CoreChanged);
            await repo.GitAsync("add", "--", U.Core);
            repo.WriteFile(U.Untracked, U.UntrackedContent);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>Clicks "Check again" and waits until its search has ended on the state titled <paramref name="title"/>.</summary>
    private static async Task CheckAgainAsync(ShellDriver app, FakeGitSetupProbe probe, string title)
    {
        var lookups = probe.Lookups.Count;
        app.Click(app.GitSetupPart(F.CheckAgainId) ?? throw new InvalidOperationException("Check again does not show."));
        await app.WaitForAsync(
            () => probe.Lookups.Count > lookups && !app.Session.Shell.CheckGitAgainCommand.IsRunning
                && app.GitSetupText(F.TitleId) == title && app.GitSetupText(F.CheckAgainId) == F.CheckAgain,
            $"Check again to end on \"{title}\"");
        app.Settle();
    }

    /// <summary>
    /// Sweeps the window in a first-run state, and asserts that the screen's own peer is named by the
    /// state's title with its body as the help text, which a screen reader speaks (9.3.8).
    /// </summary>
    private static void LookAtTheScreen(ShellDriver app, Sweeps sweeps, string state, string title, string body)
    {
        var screen = ControlAutomationPeer.CreatePeerForElement(app.GitSetupView());
        Assert.Equal((title, body), (screen.GetName(), screen.GetHelpText()));
        Assert.Equal((title, body), (app.GitSetupText(F.TitleId), app.GitSetupText(F.BodyId)));
        sweeps.Look(app.Window, state);
    }

    /// <summary>The file the resolver shows, or null while none is shown.</summary>
    private static string? FileInResolver(ShellDriver app) => (app.ShownResolver?.DataContext as ConflictResolverViewModel)?.FileName;

    /// <summary>A prompt of git as the broker hands it over (PromptDialogTests' way).</summary>
    private static GitPrompt Prompt(GitPromptKind kind, string text)
    {
        var classified = GitPromptClassifier.Classify(text, null);
        return new GitPrompt(kind, text, "op-1", "Fetch", "demo", classified.Url, classified.User, classified.KeyPath);
    }

    /// <summary>The problems of every state a fact swept, reported together at its end, state by state.</summary>
    private sealed class Sweeps
    {
        private readonly List<(string State, IReadOnlyList<AutomationProblem> Problems)> _states = [];

        public void Look(TopLevel root, string state)
        {
            var problems = AutomationSweep.FindProblems(root, state);
            _states.Add((state, problems));
            TestContext.Current.TestOutputHelper?.WriteLine($"{state}: {problems.Count} problem(s)");
            foreach (var problem in problems)
            {
                TestContext.Current.TestOutputHelper?.WriteLine("  " + problem);
            }
        }

        public void AssertNone()
        {
            var failing = _states.Where(state => state.Problems.Count > 0).ToList();
            if (failing.Count == 0)
            {
                return;
            }

            var message = new StringBuilder($"The sweep found controls without a usable name in {failing.Count} of {_states.Count} states:");
            foreach (var (state, problems) in failing)
            {
                message.Append("\n").Append(state).Append(':');
                foreach (var problem in problems)
                {
                    message.Append("\n  ").Append(problem);
                }
            }

            Assert.Fail(message.ToString());
        }
    }

    /// <summary>Each provider's list in the repository browser: two made-up repositories of the account <c>demo</c>.</summary>
    private static Task<HostingResult<RepositoryList>> ListedRepositories(HostingProvider provider, CancellationToken cancellationToken)
    {
        var updated = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        RepositoryInfo Repository(string name, bool isPrivate) => new(
            new HostedRepository(provider, "example.com", "demo", name),
            "main",
            isPrivate,
            IsFork: false,
            IsArchived: false,
            Description: null,
            new Uri($"https://example.com/demo/{name}.git"),
            new Uri($"https://example.com/demo/{name}"),
            updated,
            CanPush: true,
            Parent: null,
            ParentDefaultBranch: null);
        return Task.FromResult(HostingResult<RepositoryList>.Ok(new RepositoryList([Repository("app", isPrivate: true), Repository("site", isPrivate: false)], MoreExist: false, SomeHiddenBySso: false)));
    }

    /// <summary>The Remotes dialog's actions over a fixed list: the sweep only reads it.</summary>
    private sealed class ListedRemotes(params RemoteInfo[] remotes) : IRemoteActions
    {
        public Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync() => Task.FromResult<IReadOnlyList<RemoteInfo>>(remotes);

        public Task<string?> AddRemoteAsync(string name, string url, bool fetchNow) => Task.FromResult<string?>(null);

        public Task<string?> RenameRemoteAsync(string name, string newName) => Task.FromResult<string?>(null);

        public Task<string?> SetRemoteUrlAsync(string name, string url) => Task.FromResult<string?>(null);

        public Task RemoveRemoteAsync(string name) => Task.CompletedTask;
    }
}
