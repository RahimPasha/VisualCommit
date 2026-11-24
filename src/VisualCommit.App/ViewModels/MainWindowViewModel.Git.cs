using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

// Git's setup in the window (R4, D423): the first-run screen, one per window, which every empty tab
// shows in the welcome page's place while git cannot be used; "Check again", which looks for git
// without a restart and reopens the tabs whose restore failed; the status bar's git box with its
// tooltip, and its "Set up Git…" button.
public partial class MainWindowViewModel
{
    /// <summary>The window's lifetime, which a search for git started by "Check again" ends with: the start-up's token.</summary>
    private CancellationToken _lifetime;

    /// <summary>
    /// "Check again" (the first-run screen's button, and after Choose, "Use the Git…" and a setting
    /// changed while git does not work): looks for git again without a restart and, when it is found,
    /// opens what could not open without it. Disabled while it runs; the screen's button then reads
    /// <c>Checking…</c>.
    /// </summary>
    public IAsyncRelayCommand CheckGitAgainCommand { get; private set; } = null!;

    /// <summary>The first-run screen, one per window: every tab without a repository shows it while its state is one of the screen's.</summary>
    public GitSetupViewModel GitSetup { get; private set; } = null!;

    /// <summary>The git box's tooltip and help text: the git in use and where it came from; null while there is none.</summary>
    [ObservableProperty]
    public partial string? GitStatusToolTip { get; private set; }

    /// <summary>
    /// The status bar's <c>GitSetupButton</c> (<c>Set up Git…</c>, or <c>Details…</c> while the helper
    /// is missing, which undoes "Continue without it"): activates the first tab that shows the screen,
    /// else a new tab.
    /// </summary>
    [RelayCommand]
    private void ShowGitSetup()
    {
        GitSetup.ShowAgain();
        if (Tabs.FirstOrDefault(tab => tab.ShowsGitSetup) is { } showing)
        {
            Activate(showing);
            return;
        }

        NewTab();
    }

    /// <summary>Called once from the constructor, before the tabs are restored: the command and the screen that every tab is given.</summary>
    private void InitializeGitSetup()
    {
        CheckGitAgainCommand = new AsyncRelayCommand(CheckGitAgainAsync);
        var access = _gitAccess;
        GitSetup = new GitSetupViewModel(new GitSetupServices(
            CheckGitAgainCommand,
            access is null
                ? () => Task.FromResult(new GitGuidance(GitAccess.RunningPlatform, null, null))
                : () => Task.Run(access.ReadGuidance),
            access is null
                ? (_, _) => Task.FromResult(new GitDetection(GitAvailability.NotFound, null, null, null))
                : access.ProbeAsync,
            _settings,
            _tabServices.Folders,
            _tabServices.Dialogs,
            _tabServices.Launcher,
            _tabServices.Clipboard,
            _log)
        {
            // Read when Choose probes a file: the start sets it after the screen is made.
            Lifetime = () => _lifetime,
        });
    }

    /// <summary>
    /// "Check again", in order: <c>Looking for Git...</c>; the search; <see cref="Git"/>, the
    /// status text and its tooltip; the helper looked for again, off the UI thread; the screen. When
    /// git is found: the merge tools' lookup forgotten, every tab whose restore failed reopened, every
    /// empty tab's error from the missing git dropped, the active dashboard read, then the merge
    /// tools looked up again. While the helper is missing git works, so only the helper is looked for.
    /// </summary>
    private async Task CheckGitAgainAsync()
    {
        if (GitSetup.State == GitSetupState.HelperMissing && Git is { Availability: GitAvailability.Available } working)
        {
            var missing = await Task.Run(() => _tabServices.Prompts.CheckHelper());
            GitStatusText = GitSetupTexts.Status(working, missing is not null);
            await GitSetup.ShowAsync(working, missing);
            return;
        }

        GitStatusText = GitSetupTexts.LookingForGit;
        GitStatusToolTip = null;
        GitDetection? git;
        Exception? failure = null;
        try
        {
            git = await (_gitAccess?.DetectAsync(_lifetime) ?? _detectGit(_lifetime));
        }
        catch (OperationCanceledException)
        {
            // The window is closing.
            return;
        }
        catch (Exception ex)
        {
            _log.Error("Looking for Git failed.", ex);
            git = null;
            failure = ex;
        }

        await ShowGitAsync(git, failure);
        if (git is { Availability: GitAvailability.Available })
        {
            await OpenWhatWaitedForGitAsync();
        }
    }

    /// <summary>
    /// A search's end, shown (the start-up's and "Check again"'s): <see cref="Git"/>, the status
    /// text and tooltip, then the helper looked for again off the UI thread (only while git works and
    /// the helper was missing), then the screen with its guidance. A search that threw shows the
    /// screen's "not working" state without a path, its message as git's words.
    /// </summary>
    private async Task ShowGitAsync(GitDetection? git, Exception? failure)
    {
        Git = git;
        var missingHelper = _tabServices.Prompts.MissingHelperPath;
        GitStatusText = failure is not null ? GitSetupTexts.GitNotWorkingStatus : GitSetupTexts.Status(git, git is { Availability: GitAvailability.Available } && missingHelper is not null);
        GitStatusToolTip = GitSetupTexts.StatusTip(git, OperatingSystem.IsWindows());
        if (git is { Availability: GitAvailability.Available } && missingHelper is not null)
        {
            missingHelper = await Task.Run(() => _tabServices.Prompts.CheckHelper());
            GitStatusText = GitSetupTexts.Status(git, missingHelper is not null);
        }

        var shown = failure is not null
            ? new GitDetection(GitAvailability.Broken, null, null, null) { ErrorText = failure.Message }
            : git;
        await GitSetup.ShowAsync(shown, missingHelper);
    }

    /// <summary>
    /// Git was found again: what the start could not open without it. The merge tools' lookup is
    /// forgotten, so that the next asks the new git; each tab whose restore failed is reopened, each
    /// empty tab drops an error the missing git gave; the active dashboard reads its rows; once the
    /// reopenings have ended, the merge tools are looked up again.
    /// </summary>
    private async Task OpenWhatWaitedForGitAsync()
    {
        _tabServices.GitSettings?.ForgetToolLookup();
        var reopenings = new List<Task>();
        foreach (var tab in Tabs.ToList())
        {
            if (tab.FailedPath is not null && tab.Repository is null && !tab.IsOpening)
            {
                reopenings.Add(tab.ReopenAsync());
            }
            else if (tab.Repository is null && !tab.IsDashboard)
            {
                tab.Welcome.ClearGitError();
            }
        }

        if (ActiveTab?.Dashboard is { } dashboard)
        {
            _ = dashboard.RefreshCommand.ExecuteAsync(null);
        }

        await Task.WhenAll(reopenings);
        UpdateCanClose();
        StartMergeToolLookup();
    }
}
