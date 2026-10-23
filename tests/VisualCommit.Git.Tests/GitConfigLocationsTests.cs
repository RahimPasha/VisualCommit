using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitConfigLocations.GlobalFile"/> (docs/phase-6-design.md 8.2, facts 0.3): the pure
/// rule for each case, Windows' home among them, and a parity test that git writes
/// <c>--global</c> where the rule predicts.
/// </summary>
public class GitConfigLocationsTests
{
    private const string WindowsProfile = @"C:\Users\ann";
    private const string WindowsShare = @"H:\ann";

    [Fact]
    public void GIT_CONFIG_GLOBAL_wins_when_set()
    {
        var environment = Variables(("GIT_CONFIG_GLOBAL", "/lab/session.gitconfig"), ("HOME", "/home/ann"));

        Assert.Equal("/lab/session.gitconfig", GitConfigLocations.GlobalFile(environment, Exists("/home/ann/.gitconfig"), NoFolder, isWindows: false));
    }

    [Fact]
    public void A_set_but_empty_GIT_CONFIG_GLOBAL_leaves_no_global_file()
    {
        // Git reads no global file then, not even an existing ~/.gitconfig, and refuses a global
        // write (exit 4); the parity test's "session file empty" case shows it.
        var environment = Variables(("GIT_CONFIG_GLOBAL", string.Empty), ("HOME", "/home/ann"), ("XDG_CONFIG_HOME", "/xdg"));

        Assert.Null(GitConfigLocations.GlobalFile(environment, Exists("/home/ann/.gitconfig", "/xdg/git/config"), _ => true, isWindows: false));
        Assert.Null(GitConfigLocations.GlobalFile(environment, NoFile, _ => true, isWindows: true));
    }

    [Fact]
    public void The_home_file_wins_when_it_exists()
    {
        var environment = Variables(("HOME", "/home/ann"), ("XDG_CONFIG_HOME", "/xdg"));

        Assert.Equal("/home/ann/.gitconfig", GitConfigLocations.GlobalFile(environment, Exists("/home/ann/.gitconfig", "/xdg/git/config"), NoFolder, isWindows: false));
    }

    [Fact]
    public void The_xdg_file_is_written_when_only_it_exists()
    {
        Assert.Equal("/xdg/git/config", GitConfigLocations.GlobalFile(Variables(("HOME", "/home/ann"), ("XDG_CONFIG_HOME", "/xdg")), Exists("/xdg/git/config"), NoFolder, isWindows: false));
        Assert.Equal(
            "/home/ann/.config/git/config",
            GitConfigLocations.GlobalFile(Variables(("HOME", "/home/ann")), Exists("/home/ann/.config/git/config"), NoFolder, isWindows: false));
        Assert.Equal(
            "/home/ann/.config/git/config",
            GitConfigLocations.GlobalFile(Variables(("HOME", "/home/ann"), ("XDG_CONFIG_HOME", string.Empty)), Exists("/home/ann/.config/git/config"), NoFolder, isWindows: false));
    }

    [Fact]
    public void With_neither_file_the_home_file_is_written()
    {
        Assert.Equal("/home/ann/.gitconfig", GitConfigLocations.GlobalFile(Variables(("HOME", "/home/ann/"), ("XDG_CONFIG_HOME", "/xdg")), NoFile, NoFolder, isWindows: false));
    }

    [Fact]
    public void Without_a_home_there_is_no_global_file_outside_Windows()
    {
        Assert.Null(GitConfigLocations.GlobalFile(Variables(("USERPROFILE", "/home/ann")), NoFile, _ => true, isWindows: false));
    }

    [Fact]
    public void On_Windows_HOME_beats_both_others()
    {
        var environment = Variables(("HOME", WindowsProfile + @"\elsewhere"), ("HOMEDRIVE", "H:"), ("HOMEPATH", @"\ann"), ("USERPROFILE", WindowsProfile));

        Assert.Equal(WindowsProfile + @"\elsewhere\.gitconfig", GitConfigLocations.GlobalFile(environment, NoFile, _ => true, isWindows: true));
    }

    [Fact]
    public void On_Windows_HOMEDRIVE_and_HOMEPATH_beat_USERPROFILE_when_that_folder_exists()
    {
        var environment = Variables(("HOMEDRIVE", "H:"), ("HOMEPATH", @"\ann"), ("USERPROFILE", WindowsProfile));

        Assert.Equal(WindowsShare + @"\.gitconfig", GitConfigLocations.GlobalFile(environment, NoFile, folder => folder == WindowsShare, isWindows: true));
    }

    [Fact]
    public void On_Windows_a_missing_HOMEPATH_folder_falls_back_to_USERPROFILE()
    {
        var environment = Variables(("HOMEDRIVE", "H:"), ("HOMEPATH", @"\ann"), ("USERPROFILE", WindowsProfile));

        Assert.Equal(WindowsProfile + @"\.gitconfig", GitConfigLocations.GlobalFile(environment, NoFile, NoFolder, isWindows: true));
    }

    [Fact]
    public void On_Windows_an_unset_HOMEPATH_falls_back_to_USERPROFILE()
    {
        var environment = Variables(("HOMEDRIVE", "H:"), ("USERPROFILE", WindowsProfile));

        Assert.Equal(WindowsProfile + @"\.gitconfig", GitConfigLocations.GlobalFile(environment, NoFile, _ => true, isWindows: true));
    }

    [Fact]
    public void On_Windows_the_paths_have_backslashes_and_the_xdg_rule_holds()
    {
        var environment = Variables(("HOME", "C:/Users/ann"), ("XDG_CONFIG_HOME", "C:/xdg"));

        Assert.Equal(@"C:\Users\ann\.gitconfig", GitConfigLocations.GlobalFile(environment, NoFile, NoFolder, isWindows: true));
        Assert.Equal(@"C:\xdg\git\config", GitConfigLocations.GlobalFile(environment, Exists(@"C:\xdg\git\config"), NoFolder, isWindows: true));
        Assert.Equal(@"C:\xdg\git\config", GitConfigLocations.XdgConfigFile(environment, NoFolder, isWindows: true));
        Assert.Equal(@"C:\Users\ann\.config\git\config", GitConfigLocations.XdgConfigFile(Variables(("HOME", "C:/Users/ann")), NoFolder, isWindows: true));
    }

    /// <summary>Each case's variables for the git call; the cases that need Windows' home rule are Windows only.</summary>
    public static TheoryData<string> ParityCases()
    {
        var cases = new TheoryData<string> { "session file", "session file in a new file", "session file empty", "home file only", "xdg file only", "both files", "neither file", "home's xdg file only" };
        if (OperatingSystem.IsWindows())
        {
            cases.Add("home share present");
            cases.Add("home share missing");
            cases.Add("home path unset");
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(ParityCases))]
    public async Task Git_writes_global_where_the_rule_predicts(string name)
    {
        using var lab = new TempDirectory("where");

        // The test process's own global file, whatever the isolation names (a file in a folder nobody
        // makes, 17.2), must stay as it was: missing, or with its text.
        var processGlobal = Environment.GetEnvironmentVariable(GitConfigLocations.GlobalVariable);
        var processGlobalBefore = TextOf(processGlobal);
        var home = lab.Combine("home");
        var xdg = lab.Combine("xdg");
        var share = lab.Combine("share");
        var profile = lab.Combine("profile");
        var data = lab.Combine("data");
        foreach (var folder in new[] { home, xdg, profile, data })
        {
            Directory.CreateDirectory(folder);
        }

        // Every variable the rule reads is the case's own; the process's GIT_CONFIG_GLOBAL is never left.
        var variables = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = null,
            ["HOME"] = home,
            ["XDG_CONFIG_HOME"] = xdg,
            ["HOMEDRIVE"] = null,
            ["HOMEPATH"] = null,
            ["USERPROFILE"] = profile,
        };

        switch (name)
        {
            case "session file":
                variables["GIT_CONFIG_GLOBAL"] = Touch(lab.Combine("session.gitconfig"));
                break;
            case "session file in a new file":
                variables["GIT_CONFIG_GLOBAL"] = lab.Combine("new.gitconfig");
                break;
            case "session file empty":
                // Set but empty: git uses neither the home file nor the XDG file, both present here.
                variables["GIT_CONFIG_GLOBAL"] = string.Empty;
                Touch(Path.Combine(home, ".gitconfig"));
                Touch(Path.Combine(xdg, "git", "config"));
                break;
            case "home file only":
                Touch(Path.Combine(home, ".gitconfig"));
                break;
            case "xdg file only":
                Touch(Path.Combine(xdg, "git", "config"));
                break;
            case "both files":
                Touch(Path.Combine(home, ".gitconfig"));
                Touch(Path.Combine(xdg, "git", "config"));
                break;
            case "neither file":
                break;
            case "home's xdg file only":
                variables["XDG_CONFIG_HOME"] = null;
                Touch(Path.Combine(home, ".config", "git", "config"));
                break;
            case "home share present" or "home share missing" or "home path unset":
                variables["HOME"] = null;
                variables["XDG_CONFIG_HOME"] = null;
                if (name == "home share present")
                {
                    Directory.CreateDirectory(share);
                }

                var root = Path.GetPathRoot(share)!.TrimEnd('\\');
                variables["HOMEDRIVE"] = root;
                variables["HOMEPATH"] = name == "home path unset" ? null : share[root.Length..];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "No such case.");
        }

        var predicted = GitConfigLocations.GlobalFile(
            variable => variables.TryGetValue(variable, out var value) ? value : Environment.GetEnvironmentVariable(variable),
            File.Exists,
            Directory.Exists,
            OperatingSystem.IsWindows());
        var marker = $"Parity {Guid.NewGuid():N}";

        var write = await NewRunner().RunAsync(
            new GitCommand("config", "--global", "user.name", marker)
            {
                WorkingDirectory = data,
                Environment = new Dictionary<string, string?>(variables) { [GitIsolation.CeilingVariable] = lab.Path },
            },
            TestCancelled);

        var written = Directory.EnumerateFiles(lab.Path, "*", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains(marker, StringComparison.Ordinal))
            .ToList();
        if (predicted is null)
        {
            // No global file: git refuses the write with git config's own code for a file it
            // cannot write (4) and writes it nowhere; its words ("could not write config file")
            // are asserted on the report's git only.
            Assert.Equal(4, write.ExitCode);
            Assert.Empty(written);
            if (await GitConfigTestSupport.GitIs236Async())
            {
                Assert.Contains("could not write config file", write.StandardError, StringComparison.Ordinal);
            }
        }
        else
        {
            Assert.True(write.Succeeded, write.StandardError);
            Assert.Equal(new[] { Path.GetFullPath(predicted) }, written.Select(Path.GetFullPath).ToArray());
        }

        Assert.Equal(processGlobalBefore, TextOf(processGlobal));
    }

    /// <summary>A file's text, or null when there is no such file (or no name).</summary>
    private static string? TextOf(string? file) => file is { Length: > 0 } && File.Exists(file) ? File.ReadAllText(file) : null;

    private static string Touch(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Empty);
        return file;
    }

    private static Func<string, string?> Variables(params (string Name, string? Value)[] variables) =>
        name => variables.Where(variable => variable.Name == name).Select(variable => variable.Value).FirstOrDefault();

    private static Func<string, bool> Exists(params string[] files) => file => files.Contains(file, StringComparer.Ordinal);

    private static bool NoFile(string file) => false;

    private static bool NoFolder(string folder) => false;
}
