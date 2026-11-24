using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using VisualCommit.Git;
using SettingsTexts = VisualCommit.App.ViewModels.Operations.OperationTexts.SettingsTexts;

namespace VisualCommit.App.ViewModels.GitSetup;

/// <summary>What the first-run screen works with: the window's services, and the shell's search for git.</summary>
/// <param name="CheckAgain">The shell's "Check again" (<c>MainWindowViewModel.CheckGitAgainCommand</c>), disabled while a search runs.</param>
/// <param name="ReadGuidance">Reads the guidance's facts, off the UI thread; called once per session, the first time a state shows the screen.</param>
/// <param name="ProbeGit">What a chosen file would give as the git (<c>GitAccess.ProbeAsync</c>).</param>
/// <param name="Settings">The app's settings, where Choose writes <c>gitExecutable</c>.</param>
/// <param name="Folders">The file dialog of Choose.</param>
/// <param name="Dialogs">The window's confirmations.</param>
/// <param name="Launcher">Starts winget or the Command Line Tools' install, opens the links, shows a file; null where nothing may be opened.</param>
/// <param name="Clipboard">Copy's clipboard; null where nothing may be copied.</param>
/// <param name="Log">The app's log.</param>
public sealed record GitSetupServices(
    IAsyncRelayCommand CheckAgain,
    Func<Task<GitGuidance>> ReadGuidance,
    Func<string, CancellationToken, Task<GitDetection>> ProbeGit,
    ISettingsStore Settings,
    IFolderPicker Folders,
    IDialogService Dialogs,
    IFileLauncher? Launcher,
    IClipboardService? Clipboard,
    IAppLog Log)
{
    /// <summary>The window's lifetime, read when Choose probes a file, which ends that probe; null for none.</summary>
    public Func<CancellationToken>? Lifetime { get; init; }

    /// <summary>How long Choose's probe of a file may take; null for <see cref="GitSetupViewModel.ChosenProbeLimit"/>. A test's seam.</summary>
    public TimeSpan? ProbeLimit { get; init; }
}

/// <summary>
/// The first-run screen (R4, D405, D423, D453), one per window: it stands in the welcome page's
/// place on every empty tab while git is missing, chosen and gone, too old or not working, or
/// while the askpass helper is missing (git working) until "Continue without it". Its texts and
/// offers are <see cref="GitSetupTexts.For"/>'s for the state and the guidance's platform. The
/// guidance is read once per session, off the UI thread, the first time a state shows the screen,
/// and the screen shows only once it is read, so that no state shows another platform's texts.
/// </summary>
public sealed partial class GitSetupViewModel : ObservableObject
{
    /// <summary>How long the probe of a chosen git program may take (Choose's and the settings row's Browse's).</summary>
    public static readonly TimeSpan ChosenProbeLimit = TimeSpan.FromSeconds(10);

    private readonly GitSetupServices _services;
    private Task<GitGuidance>? _guidanceRead;
    private GitGuidance? _guidance;
    private GitDetection? _git;
    private bool _continued;

    public GitSetupViewModel(GitSetupServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        services.CheckAgain.PropertyChanged += OnCheckAgainChanged;
    }

    /// <summary>The state the screen was last given; <see cref="GitSetupState.Searching"/> before the first search ends.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown), nameof(InScreenState), nameof(StatusButtonText))]
    public partial GitSetupState State { get; private set; } = GitSetupState.Searching;

    /// <summary>What the screen shows for <see cref="State"/>; null when it shows none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown), nameof(Title), nameof(Body), nameof(PathText), nameof(ShowsPath), nameof(GitSaid), nameof(ShowsGitSaid))]
    [NotifyPropertyChangedFor(nameof(GuidanceText), nameof(Command), nameof(ShowsCommand), nameof(ShowsWinget), nameof(WingetText), nameof(ShowsDownload))]
    [NotifyPropertyChangedFor(nameof(ShowsCommandLineTools), nameof(ShowsShowFile), nameof(ShowsChoose), nameof(ChooseText), nameof(ShowsUsePath), nameof(UsePathText))]
    [NotifyPropertyChangedFor(nameof(ShowsContinue), nameof(ShowsMoreLink), nameof(GuideLinkText))]
    public partial GitSetupContent? Content { get; private set; }

    /// <summary>Why the last Choose was refused, <c>Checking…</c> while it runs, or a failed start (<c>GitSetupError</c>); null for none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsError), nameof(IsChoiceBeingChecked))]
    public partial string? ErrorText { get; private set; }

    /// <summary><see cref="ErrorText"/> is Choose's <c>Checking…</c>, drawn secondary, not as an error.</summary>
    public bool IsChoiceBeingChecked => ErrorText == GitSetupTexts.Checking;

    /// <summary>A state of the screen's has shown it once this session; it stays so. The graph area makes the screen's view only then.</summary>
    [ObservableProperty]
    public partial bool HasShown { get; private set; }

    /// <summary>The screen shows: a state of its own with its guidance read, and the helper's state until "Continue without it".</summary>
    public bool IsShown => Content is not null && !(State == GitSetupState.HelperMissing && _continued);

    /// <summary>The state is one of the screen's, shown or not: the status bar's <c>GitSetupButton</c> shows then.</summary>
    public bool InScreenState => State is not (GitSetupState.Searching or GitSetupState.Found);

    /// <summary><c>GitSetupButton</c>'s text: <c>Details…</c> while the helper is missing, <c>Set up Git…</c> otherwise.</summary>
    public string StatusButtonText => State == GitSetupState.HelperMissing ? GitSetupTexts.Details : GitSetupTexts.SetUpGit;

    /// <summary>The guidance read for the session; null until a state first showed the screen.</summary>
    public GitGuidance? Guidance => _guidance;

    public string Title => Content?.Title ?? string.Empty;

    public string Body => Content?.Body ?? string.Empty;

    public string? PathText => Content?.Path;

    public bool ShowsPath => !string.IsNullOrEmpty(PathText);

    public string? GitSaid => Content?.GitSaid;

    public bool ShowsGitSaid => !string.IsNullOrEmpty(GitSaid);

    public string GuidanceText => Content?.Guidance ?? string.Empty;

    public string? Command => Content?.Command;

    public bool ShowsCommand => !string.IsNullOrEmpty(Command);

    public bool ShowsError => !string.IsNullOrEmpty(ErrorText);

    public bool ShowsWinget => Offers(GitSetupAction.Winget);

    public string WingetText => TextOf(GitSetupAction.Winget) ?? GitSetupTexts.InstallWithWinget;

    public bool ShowsDownload => Offers(GitSetupAction.Download);

    public bool ShowsCommandLineTools => Offers(GitSetupAction.CommandLineTools);

    public bool ShowsShowFile => Offers(GitSetupAction.ShowFile);

    public bool ShowsChoose => Offers(GitSetupAction.Choose);

    public string ChooseText => TextOf(GitSetupAction.Choose) ?? GitSetupTexts.ChooseGitExe;

    public bool ShowsUsePath => Offers(GitSetupAction.UsePath);

    public string UsePathText => TextOf(GitSetupAction.UsePath) ?? GitSetupTexts.UseTheGitOnThePath;

    public bool ShowsContinue => Offers(GitSetupAction.Continue);

    public bool ShowsMoreLink => Offers(GitSetupAction.MoreLink);

    public string GuideLinkText => TextOf(GitSetupAction.GuideLink) ?? GitSetupTexts.HowVisualCommitFindsGit;

    /// <summary>The shell's "Check again", which the screen's button runs; disabled while a search runs.</summary>
    public IAsyncRelayCommand CheckAgainCommand => _services.CheckAgain;

    /// <summary><c>Checking…</c> while a search runs, <c>Check again</c> otherwise.</summary>
    public string CheckAgainText => _services.CheckAgain.IsRunning ? GitSetupTexts.Checking : GitSetupTexts.CheckAgain;

    /// <summary>
    /// The screen's last step of a search (the shell's start-up and "Check again"): the state of
    /// <paramref name="git"/> and <paramref name="missingHelperPath"/>; for a state that shows the
    /// screen, the guidance first, read once per session, so that the screen and its guidance show
    /// together. A new state clears <see cref="ErrorText"/>.
    /// </summary>
    public async Task ShowAsync(GitDetection? git, string? missingHelperPath)
    {
        var state = GitSetupStates.Of(git, missingHelperPath);
        _git = git;
        if (state is GitSetupState.Searching or GitSetupState.Found)
        {
            Apply(state, null);
            return;
        }

        var guidance = _guidance ?? await ReadGuidanceOnceAsync();
        Apply(state, GitSetupTexts.For(state, git, guidance, missingHelperPath));
    }

    /// <summary><c>GitSetupButton</c>'s <c>Details…</c>: undoes "Continue without it", so that the screen shows again.</summary>
    public void ShowAgain()
    {
        if (_continued)
        {
            _continued = false;
            OnPropertyChanged(nameof(IsShown));
        }
    }

    /// <summary>Install, or Update, with winget: the confirm, then git-scm.com's winget line in a terminal of its own, started by winget's full path (D449, D453).</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task WingetAsync()
    {
        if (_guidance?.WingetPath is not { } winget)
        {
            return;
        }

        var update = State == GitSetupState.TooOld;
        var confirmed = await _services.Dialogs.ConfirmAsync(new ConfirmationRequest(
            update ? GitSetupTexts.UpdateConfirmTitle : GitSetupTexts.InstallConfirmTitle,
            GitSetupTexts.ConfirmQuestion,
            update ? GitSetupTexts.UpdateConfirmDetail : GitSetupTexts.InstallConfirmDetail,
            update ? GitSetupTexts.Update : GitSetupTexts.Install,
            IsDestructive: false));
        if (confirmed)
        {
            await StartAsync(WingetLaunch(winget), "winget");
        }
    }

    /// <summary>
    /// The command winget's install runs: <c>cmd.exe /S /K</c> with winget's full path in quotes and
    /// git-scm.com's arguments, in winget's folder, in a console window of its own. With <c>/S</c>,
    /// <c>cmd.exe</c> strips only the outer pair of quotes, so the path stays one piece.
    /// </summary>
    public static LaunchCommand WingetLaunch(string wingetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wingetPath);
        return new LaunchCommand("cmd.exe", [])
        {
            CommandLine = "/S /K \"\"" + wingetPath + "\" install --id Git.Git -e --source winget\"",
            WorkingDirectory = Path.GetDirectoryName(wingetPath),
            InNewTerminal = true,
        };
    }

    /// <summary>The command of "Install the Command Line Tools": macOS asks the user itself, so the app asks nothing first.</summary>
    public static LaunchCommand CommandLineToolsLaunch { get; } = new("/usr/bin/xcode-select", ["--install"]);

    // The launches and Copy keep their buttons enabled while they run: the launcher starts a
    // program off the UI thread and the clipboard can be held by another program, and a button
    // disabled meanwhile would lose the keyboard focus. A second press starts the same thing again.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task CommandLineToolsAsync() => StartAsync(CommandLineToolsLaunch, "xcode-select");

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task DownloadAsync() => OpenAsync(GitSetupTexts.DownloadForWindows);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task MoreLinkAsync() =>
        OpenAsync(_guidance?.Platform == GuidancePlatform.MacOS ? GitSetupTexts.InstallOnMacOS : GitSetupTexts.InstallOnLinux);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task GuideLinkAsync() => Content is { } content ? OpenAsync(AppLinks.Guide(content.Page)) : Task.CompletedTask;

    /// <summary>Shows the file git could not read, in the file manager.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ShowFileAsync()
    {
        if (_git?.ConfigFault is not { } fault || _services.Launcher is not { } launcher)
        {
            return;
        }

        try
        {
            await launcher.RevealAsync(fault.File);
        }
        catch (Exception ex)
        {
            _services.Log.Warning($"Could not show {fault.File} in its folder.", ex);
        }
    }

    /// <summary>Copy: the command shown, to the clipboard.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task CopyAsync()
    {
        if (Command is { } command && _services.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(command);
        }
    }

    /// <summary>
    /// "Choose git.exe…": the file dialog, then the file's checks: on Windows an <c>.exe</c> only,
    /// unrun; then the probe, with <c>Checking…</c> meanwhile, at most <see cref="ChosenProbeLimit"/>
    /// and no longer than the window. A git that is found and new enough is written as
    /// <c>gitExecutable</c> and searched for again; otherwise the settings stay as they were and
    /// <c>GitSetupError</c> says why.
    /// </summary>
    [RelayCommand]
    private async Task ChooseAsync()
    {
        var windows = (_guidance?.Platform ?? GitAccess.RunningPlatform) == GuidancePlatform.Windows;
        var file = await _services.Folders.PickFileAsync(windows ? GitSetupTexts.ChooseGitExeTitle : GitSetupTexts.ChooseGitProgramTitle);
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        var lifetime = _services.Lifetime?.Invoke() ?? CancellationToken.None;
        string? refusal;
        try
        {
            refusal = await CheckChosenAsync(file, _services.ProbeGit, text => ErrorText = text, lifetime, _services.ProbeLimit);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // The window is closing.
            return;
        }

        if (refusal is not null)
        {
            ErrorText = refusal;
            return;
        }

        ErrorText = null;
        _services.Settings.Update(settings => settings with { GitExecutable = file });
        await CheckAgainAfterRunningAsync(_services.CheckAgain);
    }

    /// <summary>"Use the Git it found" and "Use the Git on the PATH": the setting removed, then a search.</summary>
    [RelayCommand]
    private async Task UsePathAsync()
    {
        _services.Settings.Update(settings => settings with { GitExecutable = null });
        await CheckAgainAfterRunningAsync(_services.CheckAgain);
    }

    /// <summary>
    /// "Check again" for a setting just changed, once a search that runs now has ended: that search
    /// went by the setting it read when it started, and a call while it runs would join it.
    /// </summary>
    public static async Task CheckAgainAfterRunningAsync(IAsyncRelayCommand checkAgain)
    {
        ArgumentNullException.ThrowIfNull(checkAgain);
        while (checkAgain.ExecutionTask is { IsCompleted: false } running)
        {
            // Its outcome is its own: it reports and logs it.
            await Task.WhenAny(running);
        }

        await checkAgain.ExecuteAsync(null);
    }

    /// <summary>"Continue without it": the screen hidden for the session while the helper is missing.</summary>
    [RelayCommand]
    private void Continue()
    {
        _continued = true;
        OnPropertyChanged(nameof(IsShown));
    }

    /// <summary>
    /// The checks of a chosen git program, Choose's and the settings row's Browse's: on Windows an
    /// <c>.exe</c> only, unrun; then <paramref name="probe"/>, <paramref name="checking"/> told
    /// <c>Checking…</c> meanwhile. Null when the file is a git that is found and new enough; else why not.
    /// The probe is stopped after <paramref name="limit"/> (<see cref="ChosenProbeLimit"/> when null),
    /// which stops the program it runs (a program that does not exit on <c>--version</c>), and is
    /// refused then; <paramref name="cancellationToken"/>'s cancel is thrown.
    /// </summary>
    public static async Task<string?> CheckChosenAsync(
        string file,
        Func<string, CancellationToken, Task<GitDetection>> probe,
        Action<string> checking,
        CancellationToken cancellationToken,
        TimeSpan? limit = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(checking);
        if (OperatingSystem.IsWindows() && !file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return SettingsTexts.ProgramNotExe;
        }

        checking(GitSetupTexts.Checking);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limited.CancelAfter(limit ?? ChosenProbeLimit);
        GitDetection found;
        try
        {
            found = await probe(file, limited.Token);
        }
        catch (OperationCanceledException) when (limited.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return GitSetupTexts.ChooseNoAnswer;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Format(CultureInfo.InvariantCulture, GitSetupTexts.ChooseNotStartedFormat, ex.Message);
        }

        return found switch
        {
            { Availability: GitAvailability.Available } => null,
            { Availability: GitAvailability.TooOld } => string.Format(CultureInfo.InvariantCulture, GitSetupTexts.ChooseTooOldFormat, found.Version, GitVersion.Minimum),
            { Availability: GitAvailability.Broken, ExitCode: null, ExecutablePath: not null } => string.Format(CultureInfo.InvariantCulture, GitSetupTexts.ChooseNotStartedFormat, found.ErrorText),
            { Availability: GitAvailability.Broken } => string.Format(CultureInfo.InvariantCulture, GitSetupTexts.ChooseNoVersionFormat, GitLocator.FirstLine(found.ErrorText ?? string.Empty)),
            _ => SettingsTexts.ProgramMissing,
        };
    }

    private void Apply(GitSetupState state, GitSetupContent? content)
    {
        ErrorText = null;
        Content = content;
        State = state;
        if (content is not null)
        {
            HasShown = true;
        }
    }

    /// <summary>The guidance, read once for the session; a read that fails gives the running platform's, with no winget and no distribution.</summary>
    private async Task<GitGuidance> ReadGuidanceOnceAsync()
    {
        _guidanceRead ??= ReadGuidanceSafelyAsync();
        var guidance = await _guidanceRead;
        _guidance = guidance;
        return guidance;
    }

    private async Task<GitGuidance> ReadGuidanceSafelyAsync()
    {
        try
        {
            return await _services.ReadGuidance();
        }
        catch (Exception ex)
        {
            _services.Log.Warning("The first-run screen's guidance could not be read.", ex);
            return new GitGuidance(GitAccess.RunningPlatform, null, null);
        }
    }

    private async Task StartAsync(LaunchCommand command, string program)
    {
        if (_services.Launcher is not { } launcher)
        {
            return;
        }

        ErrorText = null;
        try
        {
            await launcher.StartAsync(command);
            _services.Log.Info(string.Format(
                CultureInfo.InvariantCulture,
                command.InNewTerminal ? GitSetupTexts.StartedInTerminalFormat : GitSetupTexts.StartedFormat,
                command.DisplayText));
        }
        catch (Exception ex)
        {
            _services.Log.Warning($"{program} could not be started: {command.DisplayText}.", ex);
            ErrorText = string.Format(CultureInfo.InvariantCulture, GitSetupTexts.StartFailedFormat, program, ex.Message);
        }
    }

    private async Task OpenAsync(Uri address)
    {
        if (_services.Launcher is not { } launcher)
        {
            return;
        }

        try
        {
            await launcher.OpenUrlAsync(address);
        }
        catch (Exception ex)
        {
            _services.Log.Warning($"Could not open {address.AbsoluteUri}.", ex);
        }
    }

    private bool Offers(GitSetupAction action) =>
        Content is { } content && (content.Buttons.Any(offer => offer.Action == action) || content.Links.Any(offer => offer.Action == action));

    private string? TextOf(GitSetupAction action) =>
        Content?.Buttons.Concat(Content.Links).FirstOrDefault(offer => offer.Action == action)?.Text;

    private void OnCheckAgainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
        {
            OnPropertyChanged(nameof(CheckAgainText));
        }
    }
}
