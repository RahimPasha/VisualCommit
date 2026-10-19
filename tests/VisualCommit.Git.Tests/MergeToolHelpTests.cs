using System.Text.Json;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The merge tools git knows (docs/phase-6-design.md 8.10, D202): <see cref="GitMergeTools.ParseToolHelp"/>
/// on recorded 2.36 and 2.55 outputs, the live run, and <see cref="GitConfiguration.ReadToolHelpAsync"/>'s
/// kept list, over a runner that holds the run until the test releases it.
/// </summary>
public class MergeToolHelpTests
{
    /// <summary><c>git mergetool --tool-help</c> as Git 2.36 printed it with two tools of one's own (2026-10-18, outside a repository).</summary>
    private const string Recorded236 =
        "'git mergetool --tool=<tool>' may be set to one of the following:\n" +
        "\t\tvimdiff\n\t\tvimdiff1\n\t\tvimdiff2\n\t\tvimdiff3\n" +
        "\n" +
        "\tuser-defined:\n" +
        "\t\tmytool.cmd mytool --merge $BASE $LOCAL $REMOTE -o $MERGED\n" +
        "\t\tother.cmd other.exe\n" +
        "\n" +
        "The following tools are valid, but not currently available:\n" +
        "\t\taraxis\n\t\tbc\n\t\tbc3\n\t\tbc4\n\t\tcodecompare\n\t\tdeltawalker\n\t\tdiffmerge\n\t\tdiffuse\n\t\tecmerge\n\t\temerge\n" +
        "\t\texamdiff\n\t\tguiffy\n\t\tgvimdiff\n\t\tgvimdiff1\n\t\tgvimdiff2\n\t\tgvimdiff3\n\t\tkdiff3\n\t\tmeld\n\t\tnvimdiff\n" +
        "\t\tnvimdiff1\n\t\tnvimdiff2\n\t\tnvimdiff3\n\t\topendiff\n\t\tp4merge\n\t\tsmerge\n\t\ttkdiff\n\t\ttortoisemerge\n\t\twinmerge\n\t\txxdiff\n" +
        "\n" +
        "Some of the tools listed above only work in a windowed\n" +
        "environment. If run in a terminal-only session, they will fail.\n";

    /// <summary>The same with Visual Studio's Git 2.55 (2026-10-18): a description after each name, and <c>vscode</c> found.</summary>
    private const string Recorded255 =
        "'git mergetool --tool=<tool>' may be set to one of the following:\n" +
        "\t\tvimdiff          Use Vim with a custom layout (see `git help mergetool`'s `BACKEND SPECIFIC HINTS` section)\n" +
        "\t\tvimdiff1         Use Vim with a 2 panes layout (LOCAL and REMOTE)\n" +
        "\t\tvimdiff2         Use Vim with a 3 panes layout (LOCAL, MERGED and REMOTE)\n" +
        "\t\tvimdiff3         Use Vim where only the MERGED file is shown\n" +
        "\t\tvscode           Use Visual Studio Code (requires a graphical session)\n" +
        "\n" +
        "\tuser-defined:\n" +
        "\t\tmytool.cmd mytool --merge $BASE $LOCAL $REMOTE -o $MERGED\n" +
        "\t\tother.cmd other.exe\n" +
        "\n" +
        "The following tools are valid, but not currently available:\n" +
        "\t\taraxis           Use Araxis Merge (requires a graphical session)\n" +
        "\t\temerge           Use Emacs' Emerge\n" +
        "\t\tkdiff3           Use KDiff3 (requires a graphical session)\n" +
        "\t\tmeld             Use Meld (requires a graphical session) with optional `auto merge` (see `git help mergetool`'s `CONFIGURATION` section)\n" +
        "\t\txxdiff           Use xxdiff (requires a graphical session)\n" +
        "\n" +
        "Some of the tools listed above only work in a windowed\n" +
        "environment. If run in a terminal-only session, they will fail.\n";

    private static readonly string FakeGit = OperatingSystem.IsWindows() ? @"C:\Tools\Git\cmd\git.exe" : "/opt/git/bin/git";

    private const string FakeVersion = "git version 2.99.0.lab";

    [Fact]
    public void Git_236s_list_has_names_only_and_its_own_tools_are_skipped()
    {
        var help = GitMergeTools.ParseToolHelp(Recorded236.Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.False(help.Failed);
        Assert.Equal(["vimdiff", "vimdiff1", "vimdiff2", "vimdiff3"], help.Found.Select(tool => tool.Name));
        Assert.Equal(29, help.NotFound.Count);
        Assert.Equal(["araxis", "bc", "bc3"], help.NotFound.Take(3).Select(tool => tool.Name));
        Assert.Equal("xxdiff", help.NotFound[^1].Name);
        Assert.All(help.Found.Concat(help.NotFound), tool => Assert.Null(tool.Description));
        Assert.DoesNotContain(help.Found.Concat(help.NotFound), tool => tool.Name.StartsWith("mytool", StringComparison.Ordinal) || tool.Name.StartsWith("other", StringComparison.Ordinal));
    }

    [Fact]
    public void Git_255s_list_has_descriptions()
    {
        var help = GitMergeTools.ParseToolHelp(Recorded255);

        Assert.Equal(["vimdiff", "vimdiff1", "vimdiff2", "vimdiff3", "vscode"], help.Found.Select(tool => tool.Name));
        Assert.Equal(new MergeTool("vscode", "Use Visual Studio Code (requires a graphical session)"), help.Found[^1]);
        Assert.Equal(["araxis", "emerge", "kdiff3", "meld", "xxdiff"], help.NotFound.Select(tool => tool.Name));
        Assert.Equal("Use Emacs' Emerge", help.NotFound[1].Description);
    }

    [Fact]
    public void A_list_without_tools_found_has_none()
    {
        var help = GitMergeTools.ParseToolHelp(
            "No suitable tool for 'git mergetool --tool=<tool>' found.\n\nThe following tools are valid, but not currently available:\n\t\tkdiff3\n\t\tmeld\n");
        var onlyOwn = GitMergeTools.ParseToolHelp(
            "'git mergetool --tool=<tool>' may be set to one of the following:\n\tuser-defined:\n\t\tmine.cmd mine\n\nThe following tools are valid, but not currently available:\n\t\tmeld\n");

        Assert.Equal((0, 2), (help.Found.Count, help.NotFound.Count));
        Assert.Equal((0, 1), (onlyOwn.Found.Count, onlyOwn.NotFound.Count));
    }

    [Fact]
    public void The_tools_that_need_a_terminal_are_the_designs()
    {
        Assert.Equal(
            ["emerge", "nvimdiff", "nvimdiff1", "nvimdiff2", "nvimdiff3", "vimdiff", "vimdiff1", "vimdiff2", "vimdiff3"],
            GitMergeTools.TerminalTools.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("gvimdiff", GitMergeTools.TerminalTools);
    }

    [Fact]
    public async Task The_live_run_lists_the_tools_this_git_knows_and_keeps_them()
    {
        using var lab = new ConfigLab();
        // At normal priority: below normal, under the default run's load, git's shell script was
        // starved past the hang watchdog's limit; the flag is the held runner's test's.
        using var configuration = new GitConfiguration(lab.Runner, lab.DataDirectory, lab.MergeToolsFile, lab.Environment, lab.Log) { ToolHelpBelowNormal = false };

        MergeToolHelp help;
        using (HangWatchdogAttribute.WaitingForSlowProgram("git mergetool --tool-help"))
        {
            help = await configuration.ReadToolHelpAsync(TestCancelled);
        }

        // Exit 0 on every git, and every git knows meld, found here or not.
        Assert.False(help.Failed);
        Assert.Contains(help.Found.Concat(help.NotFound), tool => tool.Name == "meld");
        var kept = JsonDocument.Parse(File.ReadAllText(lab.MergeToolsFile)).RootElement;
        Assert.Equal(lab.Runner.ExecutablePath, kept.GetProperty("git").GetString());
        Assert.StartsWith("git version ", kept.GetProperty("version").GetString(), StringComparison.Ordinal);
        Assert.Equal(help.NotFound.Count, kept.GetProperty("notFound").GetArrayLength());
        AssertSameHelp(help, await configuration.ReadToolHelpAsync(TestCancelled));
    }

    [Fact]
    public async Task A_kept_list_of_this_git_answers_at_once_then_the_runs_result_replaces_it()
    {
        using var lab = new ConfigLab();
        var runner = new HeldToolHelpRunner();
        using var configuration = new GitConfiguration(runner, lab.DataDirectory, lab.MergeToolsFile, lab.Environment, lab.Log);
        var kept = new MergeToolHelp([new("kdiff3", null)], [new("meld", "Use Meld")], Failed: false);
        WriteKept(lab, FakeGit, FakeVersion, kept);

        // The run is held: the kept list answers.
        var first = await configuration.ReadToolHelpAsync(TestCancelled);
        AssertSameHelp(kept, first);
        await runner.ToolHelpStarted.WaitAsync(TestCancelled);
        AssertSameHelp(kept, await configuration.ReadToolHelpAsync(TestCancelled));

        runner.Release(new GitResult(0, Recorded255, string.Empty, TimeSpan.Zero));
        var result = await AnswerOtherThanAsync(configuration, first);

        AssertSameHelp(GitMergeTools.ParseToolHelp(Recorded255), result);
        AssertSameHelp(result, ReadKeptFile(lab.MergeToolsFile));
        Assert.Equal(1, runner.ToolHelpCalls);
        var command = runner.Commands.Single(call => call.Arguments is ["mergetool", "--tool-help"]);
        Assert.True(command.BelowNormalPriority);
        Assert.Equal(Path.GetFullPath(lab.DataDirectory), command.WorkingDirectory);
        Assert.Equal(lab.Folder, command.Environment[GitIsolation.CeilingVariable]);
    }

    public static TheoryData<string> OtherKeptFiles => ["another git", "another version", "unreadable", "incomplete"];

    [Theory]
    [MemberData(nameof(OtherKeptFiles))]
    public async Task Another_gits_or_an_unreadable_kept_list_counts_as_none(string file)
    {
        using var lab = new ConfigLab();
        var runner = new HeldToolHelpRunner();
        using var configuration = new GitConfiguration(runner, lab.DataDirectory, lab.MergeToolsFile, lab.Environment, lab.Log);
        var kept = new MergeToolHelp([new("kdiff3", null)], [], Failed: false);
        switch (file)
        {
            case "another git":
                WriteKept(lab, FakeGit + ".old", FakeVersion, kept);
                break;
            case "another version":
                WriteKept(lab, FakeGit, "git version 2.98.0", kept);
                break;
            case "unreadable":
                Directory.CreateDirectory(lab.DataDirectory);
                File.WriteAllText(lab.MergeToolsFile, "{ not json");
                break;
            default:
                Directory.CreateDirectory(lab.DataDirectory);
                File.WriteAllText(lab.MergeToolsFile, "{ \"git\": \"" + JsonEncodedText.Encode(FakeGit) + "\" }");
                break;
        }

        // The first answer is the run's: the call waited for it.
        var answer = configuration.ReadToolHelpAsync(TestCancelled);
        await runner.ToolHelpStarted.WaitAsync(TestCancelled);
        runner.Release(new GitResult(0, Recorded236, string.Empty, TimeSpan.Zero));

        AssertSameHelp(GitMergeTools.ParseToolHelp(Recorded236), await answer);
        AssertSameHelp(GitMergeTools.ParseToolHelp(Recorded236), ReadKeptFile(lab.MergeToolsFile));
        Assert.Contains(lab.Log.Entries, entry => entry.Level is LogLevel.Info or LogLevel.Warning && entry.Message.Contains("kept merge tools", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_run_leaves_the_kept_list_as_it_was_and_answers_with_it()
    {
        using var lab = new ConfigLab();
        var runner = new HeldToolHelpRunner();
        using var configuration = new GitConfiguration(runner, lab.DataDirectory, lab.MergeToolsFile, lab.Environment, lab.Log);
        var kept = new MergeToolHelp([new("kdiff3", null)], [new("meld", null)], Failed: false);
        WriteKept(lab, FakeGit, FakeVersion, kept);
        var before = File.ReadAllText(lab.MergeToolsFile);

        AssertSameHelp(kept, await configuration.ReadToolHelpAsync(TestCancelled));
        await runner.ToolHelpStarted.WaitAsync(TestCancelled);
        runner.Release(new GitResult(1, string.Empty, "error: something\n", TimeSpan.Zero));
        await runner.ToolHelpEnded.WaitAsync(TestCancelled);
        await WaitUntilAsync(() => lab.Log.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("exit code 1", StringComparison.Ordinal)));

        AssertSameHelp(kept, await configuration.ReadToolHelpAsync(TestCancelled));
        Assert.Equal(before, File.ReadAllText(lab.MergeToolsFile));
    }

    [Fact]
    public async Task A_failed_run_with_nothing_kept_is_the_failed_lookup()
    {
        using var lab = new ConfigLab();
        var runner = new HeldToolHelpRunner();
        using var configuration = new GitConfiguration(runner, lab.DataDirectory, lab.MergeToolsFile, lab.Environment, lab.Log);

        var answer = configuration.ReadToolHelpAsync(TestCancelled);
        await runner.ToolHelpStarted.WaitAsync(TestCancelled);
        runner.Release(new GitResult(1, string.Empty, string.Empty, TimeSpan.Zero));

        AssertSameHelp(MergeToolHelp.FailedLookup, await answer);
        Assert.False(File.Exists(lab.MergeToolsFile));
    }

    [Fact]
    public async Task Git_that_cannot_be_used_is_the_failed_lookup()
    {
        using var lab = new ConfigLab();
        using var configuration = new GitConfiguration(
            _ => Task.FromException<IGitRunner>(new InvalidOperationException("Git was not found on this computer.")),
            lab.DataDirectory,
            lab.MergeToolsFile,
            lab.Environment,
            lab.Log);

        AssertSameHelp(MergeToolHelp.FailedLookup, await configuration.ReadToolHelpAsync(TestCancelled));
        Assert.Contains(lab.Log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task A_callers_cancel_stops_its_own_wait_only()
    {
        using var lab = new ConfigLab();
        var runner = new HeldToolHelpRunner();
        using var configuration = new GitConfiguration(runner, lab.DataDirectory, lab.MergeToolsFile, lab.Environment, lab.Log);
        using var caller = new CancellationTokenSource();

        var waiting = configuration.ReadToolHelpAsync(caller.Token);
        await runner.ToolHelpStarted.WaitAsync(TestCancelled);
        await caller.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        runner.Release(new GitResult(0, Recorded236, string.Empty, TimeSpan.Zero));
        AssertSameHelp(GitMergeTools.ParseToolHelp(Recorded236), await configuration.ReadToolHelpAsync(TestCancelled));
        Assert.Equal(1, runner.ToolHelpCalls);
    }

    [Fact]
    public async Task Disposing_stops_the_run_and_keeps_nothing()
    {
        using var lab = new ConfigLab();
        var runner = new HeldToolHelpRunner();
        var configuration = new GitConfiguration(runner, lab.DataDirectory, lab.MergeToolsFile, lab.Environment, lab.Log);

        var answer = configuration.ReadToolHelpAsync(TestCancelled);
        await runner.ToolHelpStarted.WaitAsync(TestCancelled);
        configuration.Dispose();

        AssertSameHelp(MergeToolHelp.FailedLookup, await answer);
        Assert.True(runner.ToolHelpCancelled);
        Assert.False(File.Exists(lab.MergeToolsFile));
        AssertSameHelp(MergeToolHelp.FailedLookup, await configuration.ReadToolHelpAsync(TestCancelled));
    }

    /// <summary>Writes a kept list as the app keeps it: <c>{ "git", "version", "found", "notFound" }</c>, each tool with its name and description (8.10).</summary>
    private static void WriteKept(ConfigLab lab, string git, string version, MergeToolHelp help)
    {
        static object Tools(IReadOnlyList<MergeTool> tools) => tools.Select(tool => new Dictionary<string, string?> { ["name"] = tool.Name, ["description"] = tool.Description }).ToList();
        Directory.CreateDirectory(lab.DataDirectory);
        File.WriteAllText(
            lab.MergeToolsFile,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["git"] = git, ["version"] = version, ["found"] = Tools(help.Found), ["notFound"] = Tools(help.NotFound) }));
    }

    private static MergeToolHelp ReadKeptFile(string file)
    {
        var root = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
        static List<MergeTool> Tools(JsonElement tools) =>
            [.. tools.EnumerateArray().Select(tool => new MergeTool(tool.GetProperty("name").GetString()!, tool.GetProperty("description").GetString()))];
        Assert.Equal((FakeGit, FakeVersion), (root.GetProperty("git").GetString(), root.GetProperty("version").GetString()));
        return new MergeToolHelp(Tools(root.GetProperty("found")), Tools(root.GetProperty("notFound")), Failed: false);
    }

    /// <summary>The same lists and the same failure (a record's lists compare by reference).</summary>
    private static void AssertSameHelp(MergeToolHelp expected, MergeToolHelp actual)
    {
        Assert.Equal(expected.Failed, actual.Failed);
        Assert.Equal(expected.Found, actual.Found);
        Assert.Equal(expected.NotFound, actual.NotFound);
    }

    private static bool SameHelp(MergeToolHelp a, MergeToolHelp b) =>
        a.Failed == b.Failed && a.Found.SequenceEqual(b.Found) && a.NotFound.SequenceEqual(b.NotFound);

    /// <summary>Waits until <paramref name="condition"/> holds, looking every few milliseconds, for at most 30 seconds.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold within 30 seconds.");
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
        }
    }

    /// <summary>The answer once the run has ended: the kept list answers until then, each call a moment apart.</summary>
    private static async Task<MergeToolHelp> AnswerOtherThanAsync(GitConfiguration configuration, MergeToolHelp kept)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            var answer = await configuration.ReadToolHelpAsync(TestCancelled);
            if (!SameHelp(answer, kept) || DateTime.UtcNow > deadline)
            {
                return answer;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
        }
    }

    /// <summary>
    /// A runner for <see cref="GitConfiguration.ReadToolHelpAsync"/>: <c>git --version</c> answers at
    /// once with <see cref="FakeVersion"/>, <c>git mergetool --tool-help</c> waits until
    /// <see cref="Release"/> gives its result or its token is cancelled.
    /// </summary>
    private sealed class HeldToolHelpRunner : IGitRunner
    {
        private readonly TaskCompletionSource<GitResult> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _toolHelpCalls;

        public string ExecutablePath => FakeGit;

        public List<GitCommand> Commands { get; } = [];

        public Task ToolHelpStarted => _started.Task;

        public Task ToolHelpEnded => _ended.Task;

        public int ToolHelpCalls => Volatile.Read(ref _toolHelpCalls);

        public bool ToolHelpCancelled { get; private set; }

        public void Release(GitResult result) => _release.TrySetResult(result);

        public async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            lock (Commands)
            {
                Commands.Add(command);
            }

            if (command.Arguments is ["--version"])
            {
                return new GitResult(0, FakeVersion + "\n", string.Empty, TimeSpan.Zero);
            }

            if (command.Arguments is not ["mergetool", "--tool-help"])
            {
                throw new InvalidOperationException($"Not expected: {command.DisplayText}");
            }

            Interlocked.Increment(ref _toolHelpCalls);
            _started.TrySetResult();
            try
            {
                return await _release.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ToolHelpCancelled = true;
                throw;
            }
            finally
            {
                _ended.TrySetResult();
            }
        }
    }
}
