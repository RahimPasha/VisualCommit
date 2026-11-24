using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The first-run screen's view model (docs/phase-8a-design.md 5.3, 5.4; D405, D423, D449, D453):
/// the guidance read once, the winget button by state, its confirm and its one start through the
/// launcher, the Command Line Tools without a confirm, Choose's checks and write, the links, Copy,
/// "Continue without it". Nothing is started: the launcher is the harness's fake.
/// </summary>
public class GitSetupViewModelTests
{
    private const string WindowsGit = @"C:\Program Files\Git\cmd\git.exe";
    private const string Winget = @"C:\Users\demo\AppData\Local\Microsoft\WindowsApps\winget.exe";

    private static readonly GitDetection Missing = new(GitAvailability.NotFound, null, null, null);

    private static GitDetection TooOld(GitSource source) => new(GitAvailability.TooOld, WindowsGit, new GitVersion(2, 29, 0), null) { Source = source };

    [Fact]
    public async Task The_screen_shows_only_once_the_guidance_is_read_which_happens_once_per_session()
    {
        var guidance = new TaskCompletionSource<GitGuidance>();
        using var lab = new Lab(read: () => guidance.Task);

        var showing = lab.Screen.ShowAsync(Missing, null);
        Assert.False(lab.Screen.IsShown);
        Assert.Equal(1, lab.GuidanceReads);

        guidance.SetResult(new GitGuidance(GuidancePlatform.Windows, Winget, null));
        await showing;
        Assert.True(lab.Screen.IsShown);
        Assert.True(lab.Screen.ShowsWinget);

        await lab.Screen.ShowAsync(TooOld(GitSource.Path), null);
        await lab.Screen.ShowAsync(new GitDetection(GitAvailability.Available, WindowsGit, new GitVersion(2, 47, 1), null), null);
        Assert.False(lab.Screen.IsShown);
        Assert.Equal(GitSetupState.Found, lab.Screen.State);
        Assert.Equal(1, lab.GuidanceReads);
    }

    [Fact]
    public async Task A_working_git_reads_no_guidance()
    {
        using var lab = new Lab();

        await lab.Screen.ShowAsync(new GitDetection(GitAvailability.Available, WindowsGit, new GitVersion(2, 47, 1), null), null);

        Assert.Equal(0, lab.GuidanceReads);
        Assert.False(lab.Screen.IsShown);
        Assert.False(lab.Screen.InScreenState);
    }

    public static TheoryData<string, bool, string?> WingetCases => new()
    {
        { "missing", true, "Install with winget…" },
        { "too old, found on the machine", true, "Update with winget…" },
        { "too old, from the stand-in", true, "Update with winget…" },
        { "too old, chosen", false, null },
        { "not working", false, null },
        { "config fault", false, null },
        { "chosen gone, no candidate", true, "Install with winget…" },
        { "chosen gone, a candidate", false, null },
    };

    [Theory]
    [MemberData(nameof(WingetCases))]
    public async Task The_winget_button_shows_for_a_missing_git_and_a_too_old_one_found_on_the_machine_never_for_one_that_does_not_work(string state, bool shown, string? text)
    {
        using var lab = new Lab();
        var chosen = Path.Combine(Path.GetTempPath(), "PortableGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
        var git = state switch
        {
            "missing" => Missing,
            "too old, found on the machine" => TooOld(GitSource.Path),
            "too old, from the stand-in" => TooOld(GitSource.StandIn),
            "too old, chosen" => TooOld(GitSource.Setting),
            "not working" => new GitDetection(GitAvailability.Broken, WindowsGit, null, null) { ErrorText = "error", ExitCode = 1 },
            "config fault" => new GitDetection(GitAvailability.Broken, WindowsGit, null, null) { ErrorText = "fatal: bad config line 1 in file C:/Users/demo/.gitconfig", ExitCode = 128, ConfigFault = new("C:/Users/demo/.gitconfig", 1) },
            "chosen gone, no candidate" => Missing with { Source = GitSource.Setting, ChosenMissing = chosen },
            _ => Missing with { Source = GitSource.Setting, ChosenMissing = chosen, PathCandidate = WindowsGit },
        };

        await lab.Screen.ShowAsync(git, null);

        Assert.Equal(shown, lab.Screen.ShowsWinget);
        if (text is not null)
        {
            Assert.Equal(text, lab.Screen.WingetText);
        }

        // The download stays for every state D405 names but a gone choice that has another git.
        Assert.Equal(state != "chosen gone, a candidate", lab.Screen.ShowsDownload);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("too old")]
    public async Task Without_winget_on_the_path_no_state_shows_the_button(string state)
    {
        using var lab = new Lab(guidance: new GitGuidance(GuidancePlatform.Windows, null, null));

        await lab.Screen.ShowAsync(state == "missing" ? Missing : TooOld(GitSource.Path), null);

        Assert.False(lab.Screen.ShowsWinget);
        Assert.True(lab.Screen.ShowsDownload);
    }

    [Fact]
    public async Task Install_confirms_then_hands_the_launcher_one_start_of_winget_by_its_full_path_in_its_folder()
    {
        using var lab = new Lab();
        await lab.Screen.ShowAsync(Missing, null);

        lab.Dialogs.Answers.Enqueue(true);
        await lab.Screen.WingetCommand.ExecuteAsync(null);

        var request = Assert.Single(lab.Dialogs.Requests);
        Assert.Equal(
            ("Install Git with winget?", "VisualCommit opens a terminal window that runs winget install --id Git.Git -e --source winget", "Install", false),
            (request.Title, request.Question, request.ConfirmText, request.IsDestructive));
        Assert.Equal("winget downloads Git for Windows from its community repository, which the Git for Windows project does not maintain, and runs the installer; Windows may ask for permission. When it has finished, close the window and press Check again.", request.Detail);

        var start = Assert.Single(lab.Launcher.Starts);
        Assert.Equal("cmd.exe", start.FileName);
        Assert.Empty(start.Arguments);
        Assert.Equal("/S /K \"\"" + Winget + "\" install --id Git.Git -e --source winget\"", start.CommandLine);
        Assert.Equal(OperatingSystem.IsWindows() ? @"C:\Users\demo\AppData\Local\Microsoft\WindowsApps" : Path.GetDirectoryName(Winget), start.WorkingDirectory);
        Assert.True(start.InNewTerminal);
        Assert.Single(lab.Log.Messages, message => message == "Started in a terminal: cmd.exe /S /K \"\"" + Winget + "\" install --id Git.Git -e --source winget\"");
        Assert.Null(lab.Screen.ErrorText);
    }

    [Fact]
    public async Task For_a_too_old_git_the_button_and_the_confirm_read_update_and_cancel_starts_nothing()
    {
        using var lab = new Lab();
        await lab.Screen.ShowAsync(TooOld(GitSource.Path), null);

        lab.Dialogs.Answers.Enqueue(false);
        await lab.Screen.WingetCommand.ExecuteAsync(null);

        var request = Assert.Single(lab.Dialogs.Requests);
        Assert.Equal(("Update Git with winget?", "Update"), (request.Title, request.ConfirmText));
        Assert.Equal("winget downloads the newest Git for Windows from its community repository, which the Git for Windows project does not maintain, and runs the installer, which updates the one installed; Windows may ask for permission. When it has finished, close the window and press Check again.", request.Detail);
        Assert.Empty(lab.Launcher.Requests);
        Assert.DoesNotContain(lab.Log.Messages, message => message.StartsWith("Started", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_start_that_fails_says_why_on_the_screen()
    {
        using var lab = new Lab();
        await lab.Screen.ShowAsync(Missing, null);
        lab.Launcher.StartFailure = new FileNotFoundException("cmd.exe is not there.");

        lab.Dialogs.Answers.Enqueue(true);
        await lab.Screen.WingetCommand.ExecuteAsync(null);

        Assert.Equal("winget could not be started: cmd.exe is not there.", lab.Screen.ErrorText);
        Assert.True(lab.Screen.ShowsError);
    }

    [Fact]
    public async Task The_command_line_tools_start_without_the_apps_confirm_and_copy_gives_the_command()
    {
        using var lab = new Lab(guidance: new GitGuidance(GuidancePlatform.MacOS, null, null));
        await lab.Screen.ShowAsync(Missing with { CommandLineToolsMissing = true }, null);

        Assert.True(lab.Screen.ShowsCommandLineTools);
        await lab.Screen.CommandLineToolsCommand.ExecuteAsync(null);
        await lab.Screen.CopyCommand.ExecuteAsync(null);

        Assert.Empty(lab.Dialogs.Requests);
        var start = Assert.Single(lab.Launcher.Starts);
        Assert.Equal(("/usr/bin/xcode-select", "--install", (string?)null, false), (start.FileName, Assert.Single(start.Arguments), start.CommandLine, start.InNewTerminal));
        Assert.Single(lab.Log.Messages, message => message == "Started: /usr/bin/xcode-select --install");
        Assert.Equal(["brew install git"], lab.Clipboard.Texts);
    }

    [Fact]
    public async Task The_links_open_git_scms_pages_and_the_guides_page_of_the_state()
    {
        using var windows = new Lab();
        await windows.Screen.ShowAsync(Missing, null);
        await windows.Screen.DownloadCommand.ExecuteAsync(null);
        await windows.Screen.GuideLinkCommand.ExecuteAsync(null);
        Assert.Equal(["https://git-scm.com/install/windows", AppLinks.Guide(GuidePage.GitMissing).AbsoluteUri], windows.Launcher.Requests.Select(request => request.FullPath));

        using var mac = new Lab(guidance: new GitGuidance(GuidancePlatform.MacOS, null, null));
        await mac.Screen.ShowAsync(Missing, null);
        await mac.Screen.MoreLinkCommand.ExecuteAsync(null);

        using var linux = new Lab(guidance: new GitGuidance(GuidancePlatform.Linux, null, null));
        await linux.Screen.ShowAsync(Missing, null);
        await linux.Screen.MoreLinkCommand.ExecuteAsync(null);

        Assert.Equal(["https://git-scm.com/install/mac"], mac.Launcher.Requests.Select(request => request.FullPath));
        Assert.Equal(["https://git-scm.com/install/linux"], linux.Launcher.Requests.Select(request => request.FullPath));
    }

    [Fact]
    public async Task Show_the_file_reveals_the_file_git_named()
    {
        using var lab = new Lab();
        await lab.Screen.ShowAsync(new GitDetection(GitAvailability.Broken, WindowsGit, null, null) { ErrorText = "fatal: bad config line 1 in file C:/Users/demo/.gitconfig", ExitCode = 128, ConfigFault = new("C:/Users/demo/.gitconfig", 1) }, null);

        await lab.Screen.ShowFileCommand.ExecuteAsync(null);

        Assert.Equal(["C:/Users/demo/.gitconfig"], lab.Launcher.Revealed);
    }

    public static TheoryData<string> LateAnswers => ["Show the file", "Install the Command Line Tools", "Download", "Other ways to install Git", "the guide's page", "Copy"];

    /// <summary>
    /// The real launcher shows a file and starts a program off the UI thread, and opens an address
    /// through the window's launcher; the clipboard can be held by another program. A command that
    /// waits for them stays enabled while it runs: a disabled button loses the keyboard focus
    /// (docs/phase-8a-design.md 5.3, 8.3.1).
    /// </summary>
    [Theory]
    [MemberData(nameof(LateAnswers))]
    public async Task A_button_whose_launch_or_copy_answers_late_stays_enabled_while_it_runs(string button)
    {
        var mac = button is "Install the Command Line Tools" or "Other ways to install Git" or "Copy";
        using var lab = new Lab(guidance: mac ? new GitGuidance(GuidancePlatform.MacOS, null, null) : null);
        await lab.Screen.ShowAsync(
            button == "Show the file"
                ? new GitDetection(GitAvailability.Broken, WindowsGit, null, null) { ErrorText = "fatal: bad config line 1 in file C:/Users/demo/.gitconfig", ExitCode = 128, ConfigFault = new("C:/Users/demo/.gitconfig", 1) }
                : Missing with { CommandLineToolsMissing = mac },
            null);
        var held = new TaskCompletionSource();
        lab.Launcher.Hold = held.Task;
        lab.Clipboard.Hold = held.Task;
        IAsyncRelayCommand command = button switch
        {
            "Show the file" => lab.Screen.ShowFileCommand,
            "Install the Command Line Tools" => lab.Screen.CommandLineToolsCommand,
            "Download" => lab.Screen.DownloadCommand,
            "Other ways to install Git" => lab.Screen.MoreLinkCommand,
            "the guide's page" => lab.Screen.GuideLinkCommand,
            _ => lab.Screen.CopyCommand,
        };

        var running = command.ExecuteAsync(null);

        Assert.False(running.IsCompleted, $"{button} did not wait for its answer.");
        Assert.True(command.CanExecute(null), $"{button} is disabled while its answer is held.");
        held.SetResult();
        await running;
        Assert.True(command.CanExecute(null));
        Assert.Equal(1, lab.Launcher.Requests.Count + lab.Clipboard.Texts.Count);
    }

    [Fact]
    public async Task Choose_refuses_a_too_old_git_and_leaves_the_settings_then_writes_one_that_works_and_checks_again()
    {
        var old = Path.Combine(Path.GetTempPath(), "OldGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
        var chosen = Path.Combine(Path.GetTempPath(), "PortableGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
        using var lab = new Lab(probe: (path, _) => Task.FromResult(path == old
            ? new GitDetection(GitAvailability.TooOld, old, new GitVersion(2, 29, 0), null) { Source = GitSource.Setting }
            : new GitDetection(GitAvailability.Available, chosen, new GitVersion(2, 47, 1), null) { Source = GitSource.Setting }));
        await lab.Screen.ShowAsync(Missing, null);

        lab.Folders.AnswerFile(old).AnswerFile(chosen);
        await lab.Screen.ChooseCommand.ExecuteAsync(null);

        Assert.Equal(["Choose git.exe"], lab.Folders.FileTitles);
        Assert.Equal($"Git 2.29.0 at this path is too old: VisualCommit needs {GitVersion.Minimum} or newer.", lab.Screen.ErrorText);
        Assert.Null(lab.Settings.Current.GitExecutable);
        Assert.Equal(0, lab.ChecksAgain);
        Assert.Equal([old], lab.Probes);

        await lab.Screen.ChooseCommand.ExecuteAsync(null);

        Assert.Equal(chosen, lab.Settings.Current.GitExecutable);
        Assert.Equal(1, lab.ChecksAgain);
        Assert.Null(lab.Screen.ErrorText);
        Assert.Equal([old, chosen], lab.Probes);
    }

    [Fact]
    public async Task Choose_says_checking_while_the_probe_runs()
    {
        var probe = new TaskCompletionSource<GitDetection>();
        using var lab = new Lab(probe: (_, _) => probe.Task);
        await lab.Screen.ShowAsync(Missing, null);
        lab.Folders.AnswerFile(Path.Combine(Path.GetTempPath(), OperatingSystem.IsWindows() ? "git.exe" : "git"));

        var choosing = lab.Screen.ChooseCommand.ExecuteAsync(null);

        Assert.Equal("Checking…", lab.Screen.ErrorText);
        Assert.True(lab.Screen.IsChoiceBeingChecked);
        probe.SetResult(new GitDetection(GitAvailability.Broken, "x", null, null) { ErrorText = "fatal: one\nfatal: two", ExitCode = 128 });
        await choosing;
        Assert.Equal("This Git did not report its version. Git said: fatal: one", lab.Screen.ErrorText);
        Assert.False(lab.Screen.IsChoiceBeingChecked);
    }

    [Fact]
    public async Task Choose_names_a_file_that_could_not_be_started_or_is_not_there_and_on_windows_refuses_a_file_that_is_no_exe_unrun()
    {
        var file = Path.Combine(Path.GetTempPath(), OperatingSystem.IsWindows() ? "git.exe" : "git");
        var answers = new Queue<GitDetection>([
            new GitDetection(GitAvailability.Broken, file, null, null) { ErrorText = "Access is denied." },
            new GitDetection(GitAvailability.NotFound, null, null, null) { ChosenMissing = file },
        ]);
        using var lab = new Lab(probe: (_, _) => Task.FromResult(answers.Dequeue()));
        await lab.Screen.ShowAsync(Missing, null);

        lab.Folders.AnswerFile(file).AnswerFile(file);
        await lab.Screen.ChooseCommand.ExecuteAsync(null);
        Assert.Equal("This file could not be started: Access is denied.", lab.Screen.ErrorText);
        await lab.Screen.ChooseCommand.ExecuteAsync(null);
        Assert.Equal("There is no file at this path.", lab.Screen.ErrorText);

        lab.Folders.AnswerFile(Path.Combine(Path.GetTempPath(), "git.cmd"));
        await lab.Screen.ChooseCommand.ExecuteAsync(null);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("Choose the program itself (an .exe file): a .cmd or .bat file would run through cmd.exe.", lab.Screen.ErrorText);
            Assert.Equal(2, lab.Probes.Count);
        }
        else
        {
            Assert.Equal(3, lab.Probes.Count);
        }

        Assert.Null(lab.Settings.Current.GitExecutable);
    }

    [Fact]
    public async Task Use_the_git_on_the_path_removes_the_setting_and_checks_again()
    {
        using var lab = new Lab();
        lab.Settings.Update(settings => settings with { GitExecutable = WindowsGit });
        await lab.Screen.ShowAsync(TooOld(GitSource.Setting), null);

        Assert.True(lab.Screen.ShowsUsePath);
        Assert.Equal("Use the Git on the PATH", lab.Screen.UsePathText);
        await lab.Screen.UsePathCommand.ExecuteAsync(null);

        Assert.Null(lab.Settings.Current.GitExecutable);
        Assert.Equal(1, lab.ChecksAgain);
    }

    [Fact]
    public async Task Choose_and_use_the_git_on_the_path_while_a_search_runs_check_again_once_it_has_ended()
    {
        // A search that runs went by the setting it read when it started, and a call while it runs
        // would join it: the changed setting gets a search of its own, after that one.
        var chosen = Path.Combine(Path.GetTempPath(), "PortableGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
        var running = new TaskCompletionSource();
        var startedAfterTheRunningOne = new List<bool>();
        using var lab = new Lab(
            probe: (_, _) => Task.FromResult(new GitDetection(GitAvailability.Available, chosen, new GitVersion(2, 47, 1), null) { Source = GitSource.Setting }),
            checkAgain: () =>
            {
                startedAfterTheRunningOne.Add(running.Task.IsCompleted);
                return startedAfterTheRunningOne.Count == 1 ? running.Task : Task.CompletedTask;
            });
        await lab.Screen.ShowAsync(Missing, null);

        var checking = lab.Screen.CheckAgainCommand.ExecuteAsync(null);
        lab.Folders.AnswerFile(chosen);
        var choosing = lab.Screen.ChooseCommand.ExecuteAsync(null);
        var usingPath = lab.Screen.UsePathCommand.ExecuteAsync(null);

        Assert.Equal(1, lab.ChecksAgain);
        Assert.False(choosing.IsCompleted);
        Assert.False(usingPath.IsCompleted);
        running.SetResult();
        await Task.WhenAll(checking, choosing, usingPath);

        Assert.Equal([false, true, true], startedAfterTheRunningOne);
    }

    [Fact]
    public async Task Continue_without_the_helper_hides_the_screen_for_the_session_and_details_brings_it_back()
    {
        using var lab = new Lab();
        await lab.Screen.ShowAsync(new GitDetection(GitAvailability.Available, WindowsGit, new GitVersion(2, 47, 1), null), "C:/missing/visualcommit-askpass.exe");

        Assert.True(lab.Screen.IsShown);
        Assert.Equal("Details…", lab.Screen.StatusButtonText);
        lab.Screen.ContinueCommand.Execute(null);
        Assert.False(lab.Screen.IsShown);
        Assert.True(lab.Screen.InScreenState);
        Assert.Null(lab.Settings.Current.GitExecutable);

        lab.Screen.ShowAgain();
        Assert.True(lab.Screen.IsShown);
    }

    [Fact]
    public async Task Check_again_reads_checking_while_it_runs()
    {
        var running = new TaskCompletionSource();
        using var lab = new Lab(checkAgain: () => running.Task);
        await lab.Screen.ShowAsync(Missing, null);

        Assert.Equal("Check again", lab.Screen.CheckAgainText);
        var checking = lab.Screen.CheckAgainCommand.ExecuteAsync(null);
        Assert.Equal("Checking…", lab.Screen.CheckAgainText);
        Assert.False(lab.Screen.CheckAgainCommand.CanExecute(null));
        running.SetResult();
        await checking;
        Assert.Equal("Check again", lab.Screen.CheckAgainText);
    }

    /// <summary>The screen over fakes: the settings, the dialog layer that answers from a queue, the file dialog, the launcher, the clipboard, a probe and a check again that count.</summary>
    private sealed class Lab : IDisposable
    {
        private int _guidanceReads;
        private int _checksAgain;

        public Lab(
            GitGuidance? guidance = null,
            Func<Task<GitGuidance>>? read = null,
            Func<string, CancellationToken, Task<GitDetection>>? probe = null,
            Func<Task>? checkAgain = null)
        {
            var fixedGuidance = guidance ?? new GitGuidance(GuidancePlatform.Windows, Winget, null);
            var command = new AsyncRelayCommand(() =>
            {
                Interlocked.Increment(ref _checksAgain);
                return checkAgain?.Invoke() ?? Task.CompletedTask;
            });
            Screen = new GitSetupViewModel(new GitSetupServices(
                command,
                () =>
                {
                    Interlocked.Increment(ref _guidanceReads);
                    return read?.Invoke() ?? Task.FromResult(fixedGuidance);
                },
                (path, token) =>
                {
                    Probes.Add(path);
                    return probe?.Invoke(path, token) ?? Task.FromResult(new GitDetection(GitAvailability.NotFound, null, null, null));
                },
                Settings,
                Folders,
                Dialogs,
                Launcher,
                Clipboard,
                Log));
        }

        public GitSetupViewModel Screen { get; }

        public MainWindowViewModelTests.FakeSettings Settings { get; } = new();

        public FakeFolderPicker Folders { get; } = new();

        public QueuedDialogs Dialogs { get; } = new();

        public FakeFileLauncher Launcher { get; } = new();

        public FakeClipboard Clipboard { get; } = new();

        public RecordingLog Log { get; } = new();

        public List<string> Probes { get; } = [];

        public int GuidanceReads => Volatile.Read(ref _guidanceReads);

        public int ChecksAgain => Volatile.Read(ref _checksAgain);

        public void Dispose()
        {
        }
    }

    /// <summary>A dialog layer that records each confirmation and answers it from a queue (false when it is empty).</summary>
    private sealed class QueuedDialogs : IDialogService
    {
        public List<ConfirmationRequest> Requests { get; } = [];

        public Queue<bool> Answers { get; } = new();

        public Task<bool> ConfirmAsync(ConfirmationRequest request)
        {
            Requests.Add(request);
            return Task.FromResult(Answers.Count > 0 && Answers.Dequeue());
        }
    }
}
