using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.App.ViewModels.GitSetup.GitSetupAction;

namespace VisualCommit.App.Tests;

/// <summary>
/// The first-run screen's content by state and by the guidance's platform (docs/phase-8a-design.md
/// 5.3; D405, D423, D449, D453): titles, bodies with <c>{v}</c> and <c>{min}</c>, the path, git's own
/// words, the guidance with its command, the buttons and links in their order, the guide's page;
/// then the status bar's texts and tooltips and the settings row's notes. Pure: no window, no disk.
/// </summary>
public class GitSetupTextsTests
{
    private const string WindowsGit = @"C:\Program Files\Git\cmd\git.exe";
    private const string WingetPath = @"C:\Users\demo\AppData\Local\Microsoft\WindowsApps\winget.exe";

    private static readonly GitGuidance Windows = new(GuidancePlatform.Windows, WingetPath, null);
    private static readonly GitGuidance WindowsWithoutWinget = new(GuidancePlatform.Windows, null, null);
    private static readonly GitGuidance MacOS = new(GuidancePlatform.MacOS, null, null);
    private static readonly LinuxDistribution Ubuntu = new("ubuntu", ["debian"], "Ubuntu 22.04.5 LTS");
    private static readonly GitDetection Missing = new(GitAvailability.NotFound, null, null, null);

    private static GitGuidance Linux(LinuxDistribution? distribution) => new(GuidancePlatform.Linux, null, distribution);

    private static GitDetection TooOld(string path, GitSource source = GitSource.Path) =>
        new(GitAvailability.TooOld, path, new GitVersion(2, 29, 0), null) { Source = source };

    private static GitSetupContent ContentOf(GitDetection git, GitGuidance guidance, string? helper = null) =>
        GitSetupTexts.For(GitSetupStates.Of(git, helper), git, guidance, helper) ?? throw new InvalidOperationException("No screen.");

    private static GitSetupAction[] Buttons(GitSetupContent content) => [.. content.Buttons.Select(offer => offer.Action)];

    private static GitSetupAction[] Links(GitSetupContent content) => [.. content.Links.Select(offer => offer.Action)];

    [Fact]
    public void Missing_on_Windows_with_winget()
    {
        var content = ContentOf(Missing, Windows);

        Assert.Equal("VisualCommit needs Git", content.Title);
        Assert.Equal("VisualCommit works through the Git installed on this computer, and it found none: not on the PATH, nor in Git for Windows' usual install folders.", content.Body);
        Assert.Equal("Install Git for Windows, then press Check again. VisualCommit does not need to restart.", content.Guidance);
        Assert.Null(content.Path);
        Assert.Null(content.GitSaid);
        Assert.Null(content.Command);
        Assert.Equal([Winget, Download, Choose, CheckAgain], Buttons(content));
        Assert.Equal(new[] { "Install with winget…", "Download Git for Windows", "Choose git.exe…", "Check again" }, content.Buttons.Select(offer => offer.Text));
        Assert.Equal([GuideLink], Links(content));
        Assert.Equal("How VisualCommit finds Git", content.Links[0].Text);
        Assert.Equal(GuidePage.GitMissing, content.Page);
    }

    [Fact]
    public void Missing_on_Windows_without_winget_offers_no_winget()
    {
        Assert.Equal([Download, Choose, CheckAgain], Buttons(ContentOf(Missing, WindowsWithoutWinget)));
    }

    [Fact]
    public void Missing_on_macOS_with_and_without_the_command_line_tools()
    {
        var toolsMissing = ContentOf(Missing with { CommandLineToolsMissing = true }, MacOS);
        var plain = ContentOf(Missing, MacOS);

        Assert.Equal("VisualCommit works through the Git installed on this Mac, and it found none. Apple's Command Line Tools, which include Git, are not installed.", toolsMissing.Body);
        Assert.Equal("Install the Command Line Tools, which macOS asks you to confirm, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:", toolsMissing.Guidance);
        Assert.Equal("brew install git", toolsMissing.Command);
        Assert.Equal([CommandLineTools, Choose, CheckAgain], Buttons(toolsMissing));
        Assert.Equal("Choose the git program…", toolsMissing.Buttons[1].Text);
        Assert.Equal([MoreLink, GuideLink], Links(toolsMissing));

        Assert.Equal("VisualCommit works through the Git installed on this Mac, and it found none.", plain.Body);
        Assert.Equal("Install Git with Homebrew, then press Check again. In Terminal:", plain.Guidance);
        Assert.Equal("brew install git", plain.Command);
        Assert.Equal([Choose, CheckAgain], Buttons(plain));
    }

    [Theory]
    [InlineData("ubuntu", "", "sudo apt install git")]
    [InlineData("debian", "", "sudo apt install git")]
    [InlineData("linuxmint", "ubuntu debian", "sudo apt install git")]
    [InlineData("fedora", "", "sudo dnf install git")]
    [InlineData("rocky", "rhel centos fedora", "sudo dnf install git")]
    [InlineData("centos", "", "sudo dnf install git")]
    [InlineData("arch", "", "sudo pacman -S git")]
    [InlineData("manjaro", "arch", "sudo pacman -S git")]
    [InlineData("opensuse-tumbleweed", "opensuse suse", "sudo zypper install git")]
    [InlineData("opensuse-leap", "", "sudo zypper install git")]
    [InlineData("sles", "suse", "sudo zypper install git")]
    [InlineData("alpine", "", null)]
    [InlineData("nixos", "", null)]
    public void The_linux_command_goes_by_the_id_then_each_id_like(string id, string like, string? command)
    {
        var distribution = new LinuxDistribution(id, like.Split(' ', StringSplitOptions.RemoveEmptyEntries), null);

        Assert.Equal(command, GitSetupTexts.LinuxCommand(distribution));
        var content = ContentOf(Missing, Linux(distribution));
        Assert.Equal(command, content.Command);
        Assert.Equal(
            command is null
                ? "Install the package named git with your distribution's package manager, then press Check again."
                : $"Install it with your distribution's package manager, in a terminal, then press Check again. On {id}:",
            content.Guidance);
    }

    [Fact]
    public void Missing_on_Linux_names_the_distribution_by_its_pretty_name()
    {
        var content = ContentOf(Missing, Linux(Ubuntu));

        Assert.Equal("VisualCommit works through the Git installed on this computer, and it found none on the PATH or in the usual folders.", content.Body);
        Assert.Equal("Install it with your distribution's package manager, in a terminal, then press Check again. On Ubuntu 22.04.5 LTS:", content.Guidance);
        Assert.Equal("sudo apt install git", content.Command);
        Assert.Equal([Choose, CheckAgain], Buttons(content));
        Assert.Equal([MoreLink, GuideLink], Links(content));
        Assert.Equal("Other ways to install Git", content.Links[0].Text);
    }

    [Fact]
    public void Missing_on_an_unknown_linux_has_no_command()
    {
        var content = ContentOf(Missing, Linux(null));

        Assert.Equal("Install the package named git with your distribution's package manager, then press Check again.", content.Guidance);
        Assert.Null(content.Command);
    }

    [Fact]
    public void A_chosen_git_that_is_gone_with_a_candidate_and_without()
    {
        var chosen = Path.Combine(Path.GetTempPath(), "PortableGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
        var withCandidate = ContentOf(new GitDetection(GitAvailability.NotFound, null, null, null) { Source = GitSource.Setting, ChosenMissing = chosen, PathCandidate = WindowsGit }, Windows);
        var withoutCandidate = ContentOf(new GitDetection(GitAvailability.NotFound, null, null, null) { Source = GitSource.Setting, ChosenMissing = chosen }, Windows);

        Assert.Equal("The chosen Git is gone", withCandidate.Title);
        Assert.Equal("VisualCommit was set to use the Git at this path, and there is no file there now:", withCandidate.Body);
        Assert.Equal(chosen, withCandidate.Path);
        Assert.Equal($"VisualCommit found another Git at {WindowsGit}.", withCandidate.Guidance);
        Assert.Equal([Choose, UsePath, CheckAgain], Buttons(withCandidate));
        Assert.Equal("Use the Git it found", withCandidate.Buttons[1].Text);
        Assert.Equal([GuideLink], Links(withCandidate));
        Assert.Equal(GuidePage.GitChosenMissing, withCandidate.Page);

        Assert.Equal("No other Git was found on the PATH or in the usual install folders. Choose another, or press Check again once the chosen one is back.", withoutCandidate.Guidance);
        Assert.Equal([Winget, Download, Choose, CheckAgain], Buttons(withoutCandidate));
        Assert.Equal("Install with winget…", withoutCandidate.Buttons[0].Text);
        Assert.Equal([MoreLink, GuideLink], Links(ContentOf(new GitDetection(GitAvailability.NotFound, null, null, null) { Source = GitSource.Setting, ChosenMissing = chosen }, Linux(Ubuntu))));
    }

    [Fact]
    public void A_chosen_git_the_lookup_does_not_run_says_so()
    {
        var content = ContentOf(new GitDetection(GitAvailability.NotFound, null, null, null) { Source = GitSource.Setting, ChosenMissing = "git.exe" }, Windows);

        Assert.Equal("VisualCommit was set to use the Git at this path, but it runs Git only from a full path to the program itself:", content.Body);
        Assert.Equal("The Git chosen in Settings is not used: git.exe is not a full path.", GitSetupTexts.ChosenNotUsed("git.exe"));
        var gone = Path.Combine(Path.GetTempPath(), "gone", "git.exe");
        Assert.Equal($"The Git chosen in Settings was not found at {gone}.", GitSetupTexts.ChosenNotUsed(gone));
        var script = Path.Combine(Path.GetTempPath(), "git.cmd");
        Assert.Equal(
            OperatingSystem.IsWindows() ? $"The Git chosen in Settings is not used: {script} is not an .exe file." : $"The Git chosen in Settings was not found at {script}.",
            GitSetupTexts.ChosenNotUsed(script));
    }

    [Fact]
    public void Too_old_names_the_version_and_the_minimum_and_offers_winget_for_a_git_found_on_the_machine_only()
    {
        var found = ContentOf(TooOld(WindowsGit), Windows);
        var chosen = ContentOf(TooOld(WindowsGit, GitSource.Setting), Windows);
        var standIn = ContentOf(TooOld(WindowsGit, GitSource.StandIn), Windows);

        Assert.Equal("Git 2.29.0 is too old", found.Title);
        Assert.Equal($"VisualCommit needs Git {GitVersion.Minimum} or newer. It found Git 2.29.0 at:", found.Body);
        Assert.Equal(WindowsGit, found.Path);
        Assert.Equal("Update Git for Windows, then press Check again. Its installer updates it in place.", found.Guidance);
        Assert.Null(found.Command);
        Assert.Equal([Winget, Download, Choose, CheckAgain], Buttons(found));
        Assert.Equal("Update with winget…", found.Buttons[0].Text);
        Assert.Equal([GuideLink], Links(found));
        Assert.Equal(GuidePage.GitTooOld, found.Page);

        Assert.Equal([Download, Choose, UsePath, CheckAgain], Buttons(chosen));
        Assert.Equal("Use the Git on the PATH", chosen.Buttons[2].Text);
        Assert.Equal([Winget, Download, Choose, CheckAgain], Buttons(standIn));
    }

    [Fact]
    public void Too_old_on_macOS_offers_homebrew_and_on_linux_the_distribution_with_no_command_whatever_the_id()
    {
        var mac = ContentOf(TooOld("/usr/bin/git"), MacOS);
        Assert.Equal("Update the Command Line Tools in System Settings, General, Software Update, or install Git with Homebrew, then press Check again. With Homebrew, in Terminal:", mac.Guidance);
        Assert.Equal("brew install git", mac.Command);
        Assert.Equal([Choose, CheckAgain], Buttons(mac));
        Assert.Equal([MoreLink, GuideLink], Links(mac));

        foreach (var distribution in new LinuxDistribution?[] { Ubuntu, new("fedora", [], null), new("alpine", [], null), null })
        {
            var linux = ContentOf(TooOld("/usr/bin/git"), Linux(distribution));
            Assert.Equal("Install a newer Git from your distribution, or choose another Git, then press Check again.", linux.Guidance);
            Assert.Null(linux.Command);
            Assert.Equal([Choose, CheckAgain], Buttons(linux));
            Assert.Equal([MoreLink, GuideLink], Links(linux));
        }
    }

    [Fact]
    public void Not_working_shows_gits_words_and_never_offers_winget()
    {
        var said = new GitDetection(GitAvailability.Broken, WindowsGit, null, null) { ErrorText = "error: something", ExitCode = 1 };
        var content = ContentOf(said, Windows);

        Assert.Equal("Git is not working", content.Title);
        Assert.Equal("VisualCommit found Git at the path below, but it did not report its version.", content.Body);
        Assert.Equal(WindowsGit, content.Path);
        Assert.Equal("error: something", content.GitSaid);
        Assert.Equal("Repair or reinstall Git for Windows, then press Check again.", content.Guidance);
        Assert.Equal([Download, Choose, CheckAgain], Buttons(content));
        Assert.Equal([GuideLink], Links(content));
        Assert.Equal(GuidePage.GitNotWorking, content.Page);
        Assert.Equal([Download, Choose, UsePath, CheckAgain], Buttons(ContentOf(said with { Source = GitSource.Setting }, Windows)));
        Assert.Equal("Reinstall Git, then press Check again.", ContentOf(said, MacOS).Guidance);
        Assert.Equal("Reinstall git with your distribution's package manager, then press Check again.", ContentOf(said, Linux(Ubuntu)).Guidance);
        Assert.Equal([Choose, CheckAgain], Buttons(ContentOf(said, Linux(Ubuntu))));
    }

    [Fact]
    public void Not_working_with_a_config_fault_names_the_line_and_offers_the_file_and_the_download()
    {
        var git = new GitDetection(GitAvailability.Broken, WindowsGit, null, null)
        {
            ErrorText = "fatal: bad config line 1 in file C:/Users/demo/.gitconfig",
            ExitCode = 128,
            ConfigFault = new GitConfigFault("C:/Users/demo/.gitconfig", 1),
        };

        var content = ContentOf(git, Windows);

        Assert.Equal("fatal: bad config line 1 in file C:/Users/demo/.gitconfig", content.GitSaid);
        Assert.Equal("Git cannot read line 1 of C:/Users/demo/.gitconfig. Correct that line, or rename the file, then press Check again.", content.Guidance);
        Assert.Equal([Download, ShowFile, Choose, CheckAgain], Buttons(content));
        Assert.Equal("Git cannot read line 1 of C:/Users/demo/.gitconfig. Correct that line, or rename the file, then press Check again.", ContentOf(git, Linux(Ubuntu)).Guidance);
    }

    [Fact]
    public void Not_working_that_said_nothing_or_could_not_be_started()
    {
        var silent = ContentOf(new GitDetection(GitAvailability.Broken, WindowsGit, null, null) { ErrorText = string.Empty, ExitCode = 3 }, Windows);
        var notStarted = ContentOf(new GitDetection(GitAvailability.Broken, WindowsGit, null, null) { ErrorText = "The system cannot find the file specified." }, Windows);
        var thrown = ContentOf(new GitDetection(GitAvailability.Broken, null, null, null) { ErrorText = "boom" }, Windows);

        Assert.Equal("VisualCommit found Git at the path below, but it said nothing and exited with code 3.", silent.Body);
        Assert.Null(silent.GitSaid);
        Assert.Equal("VisualCommit found Git at the path below, but it could not be started: The system cannot find the file specified.", notStarted.Body);
        Assert.Null(notStarted.GitSaid);
        Assert.Equal(WindowsGit, notStarted.Path);

        // A search that threw: no path, its message as git's words.
        Assert.Null(thrown.Path);
        Assert.Equal("boom", thrown.GitSaid);
    }

    [Theory]
    [InlineData("xcrun: error: invalid active developer path (/Library/Developer/CommandLineTools)")]
    [InlineData("git: requesting installation of command line developer tools")]
    public void Not_working_on_macOS_with_the_tools_broken_offers_them_and_the_switch(string words)
    {
        var content = ContentOf(new GitDetection(GitAvailability.Broken, "/usr/bin/git", null, null) { ErrorText = words, ExitCode = 1 }, MacOS);

        Assert.Equal("Apple's Command Line Tools look damaged. Install them again, or select them in Terminal, then press Check again:", content.Guidance);
        Assert.Equal("sudo xcode-select --switch /Library/Developer/CommandLineTools", content.Command);
        Assert.Equal([CommandLineTools, Choose, CheckAgain], Buttons(content));
        Assert.Equal([GuideLink], Links(content));
    }

    [Fact]
    public void The_helper_missing_with_git_working()
    {
        var helper = @"C:\Users\demo\AppData\Local\Programs\VisualCommit\visualcommit-askpass.exe";
        var content = ContentOf(new GitDetection(GitAvailability.Available, WindowsGit, new GitVersion(2, 47, 1), null), Windows, helper);

        Assert.Equal("Part of VisualCommit is missing", content.Title);
        Assert.Equal("Git works, but VisualCommit's helper for git's questions is missing. It was looked for at:", content.Body);
        Assert.Equal(helper, content.Path);
        Assert.Equal("Without it, git and ssh cannot ask for a password, a passphrase or a host key here, and a signed-in account cannot be used for git. Install VisualCommit again to restore it.", content.Guidance);
        Assert.Equal([CheckAgain, Continue], Buttons(content));
        Assert.Equal(new[] { "Check again", "Continue without it" }, content.Buttons.Select(offer => offer.Text));
        Assert.Equal([GuideLink], Links(content));
        Assert.Equal("What the helper does", content.Links[0].Text);
        Assert.Equal(GuidePage.HelperMissing, content.Page);
    }

    [Fact]
    public void Searching_and_found_show_no_screen()
    {
        Assert.Null(GitSetupTexts.For(GitSetupState.Searching, null, Windows, null));
        Assert.Null(GitSetupTexts.For(GitSetupState.Found, new GitDetection(GitAvailability.Available, WindowsGit, new GitVersion(2, 47, 1), null), Windows, null));
    }

    [Fact]
    public void No_windows_state_shows_a_command()
    {
        foreach (var git in new[]
        {
            Missing,
            TooOld(WindowsGit),
            new GitDetection(GitAvailability.Broken, WindowsGit, null, null) { ErrorText = "xcrun: error", ExitCode = 1 },
        })
        {
            Assert.Null(ContentOf(git, Windows).Command);
        }
    }

    [Fact]
    public void The_status_bar_says_what_was_found_and_where_it_came_from()
    {
        var found = new GitDetection(GitAvailability.Available, WindowsGit, new GitVersion(2, 47, 1), null);

        Assert.Equal("Looking for Git...", GitSetupTexts.Status(null));
        Assert.Equal("Git 2.47.1", GitSetupTexts.Status(found));
        Assert.Equal("Git 2.47.1 (helper missing)", GitSetupTexts.Status(found, helperMissing: true));
        Assert.Equal($"Git 2.29.0 is too old (needs {GitVersion.Minimum} or newer)", GitSetupTexts.Status(TooOld(WindowsGit)));
        Assert.Equal("Git not found", GitSetupTexts.Status(Missing));
        Assert.Equal("Chosen Git not found", GitSetupTexts.Status(Missing with { ChosenMissing = @"D:\Tools\PortableGit\cmd\git.exe" }));
        Assert.Equal("Git is not working", GitSetupTexts.Status(new GitDetection(GitAvailability.Broken, WindowsGit, null, null)));

        Assert.Equal($"{WindowsGit}, found on the PATH", GitSetupTexts.StatusTip(found, windows: true));
        Assert.Equal($"{WindowsGit}, found in Git for Windows' install folder", GitSetupTexts.StatusTip(found with { Source = GitSource.InstallFolder }, windows: true));
        Assert.Equal("/usr/bin/git, found in a usual install folder", GitSetupTexts.StatusTip(found with { ExecutablePath = "/usr/bin/git", Source = GitSource.InstallFolder }, windows: false));
        Assert.Equal($"{WindowsGit}, chosen in Settings", GitSetupTexts.StatusTip(found with { Source = GitSource.Setting }, windows: true));
        Assert.Equal($"{WindowsGit}, from VISUALCOMMIT_GIT_STAND_IN (a test switch)", GitSetupTexts.StatusTip(found with { Source = GitSource.StandIn }, windows: true));
        Assert.Null(GitSetupTexts.StatusTip(Missing, windows: true));
        Assert.Null(GitSetupTexts.StatusTip(null, windows: true));
    }

    [Fact]
    public void The_settings_rows_note_says_where_the_git_came_from()
    {
        var found = new GitDetection(GitAvailability.Available, WindowsGit, new GitVersion(2, 47, 1), null);

        Assert.Equal("Found on the PATH.", GitSetupTexts.SourceNote(found, windows: true));
        Assert.Equal("Found in Git for Windows' install folder.", GitSetupTexts.SourceNote(found with { Source = GitSource.InstallFolder }, windows: true));
        Assert.Equal("Found in a usual install folder.", GitSetupTexts.SourceNote(found with { Source = GitSource.InstallFolder }, windows: false));
        Assert.Equal("Chosen here.", GitSetupTexts.SourceNote(found with { Source = GitSource.Setting }, windows: true));
        Assert.Equal("From VISUALCOMMIT_GIT_STAND_IN, a test switch.", GitSetupTexts.SourceNote(found with { Source = GitSource.StandIn }, windows: true));
        Assert.Equal("No Git was found on the PATH or in the usual install folders.", GitSetupTexts.SourceNote(Missing, windows: true));
        Assert.Equal("No Git was found on the PATH or in the usual install folders.", GitSetupTexts.SourceNote(null, windows: true));
        Assert.Equal("Chosen here. There is no file at this path.", GitSetupTexts.SourceNote(Missing with { ChosenMissing = Path.Combine(Path.GetTempPath(), OperatingSystem.IsWindows() ? "git.exe" : "git") }, windows: true));
        Assert.Equal("Chosen here. VisualCommit runs Git only from a full path to the program itself.", GitSetupTexts.SourceNote(Missing with { ChosenMissing = "git.exe" }, windows: true));
    }

    [Fact]
    public void The_confirms_show_git_scms_line_unchanged()
    {
        Assert.Equal("winget install --id Git.Git -e --source winget", GitSetupTexts.WingetLine);
        Assert.Equal("VisualCommit opens a terminal window that runs winget install --id Git.Git -e --source winget", GitSetupTexts.ConfirmQuestion);
        Assert.Equal(("Install Git with winget?", "Install"), (GitSetupTexts.InstallConfirmTitle, GitSetupTexts.Install));
        Assert.Equal(("Update Git with winget?", "Update"), (GitSetupTexts.UpdateConfirmTitle, GitSetupTexts.Update));
    }

    [Fact]
    public void The_netrc_note_names_the_git_found()
    {
        Assert.Equal(
            "Your home folder has a .netrc or _netrc file. With Git 2.43.0, an entry in that file for a host can answer git's requests to it before your signed-in account. Git 2.46 or later stops this for git's own requests; git-lfs reads the file with every version of Git.",
            GitSetupTexts.NetrcNote(new GitVersion(2, 43, 0)));
        Assert.Equal(new GitVersion(2, 46, 0), GitSetupTexts.NetrcSafeFrom);
    }
}
