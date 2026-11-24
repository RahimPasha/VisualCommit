using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The broker's helper and its one line about ssh (docs/phase-8a-design.md 1.2, 5.3; D423, D424):
/// a missing helper is reported where it was looked for, "Check again" looks for it again and
/// says when it is there now; a found helper stays found. ssh is never asked for its version: the
/// session's first Ask or Deny scope logs once what an OpenSSH older than 8.4 does.
/// </summary>
public class AskPassBrokerHelperTests
{
    [Fact]
    public void A_missing_helper_is_reported_at_its_path_until_check_helper_finds_it()
    {
        using var folder = new TempDirectory("helper");
        var helper = folder.Combine(AskPassLocator.FileName);
        var log = new ListLog();
        using var broker = new AskPassBroker(new RecordingPromptHandler(), log, helperPath: helper);
        IGitPromptBroker prompts = broker;

        Assert.False(broker.HelperFound);
        Assert.Equal(helper, prompts.MissingHelperPath);
        Assert.Equal(helper, prompts.CheckHelper());
        Assert.DoesNotContain(log.Entries, entry => entry.Message.StartsWith("The askpass helper is there now", StringComparison.Ordinal));

        File.WriteAllText(helper, string.Empty);

        Assert.Null(prompts.CheckHelper());
        Assert.True(broker.HelperFound);
        Assert.Null(prompts.MissingHelperPath);
        Assert.Single(log.Entries, entry => entry == (LogLevel.Info, $"The askpass helper is there now ({helper})."));

        // Found stays found, and is said once.
        Assert.Null(prompts.CheckHelper());
        Assert.Single(log.Entries, entry => entry.Message.StartsWith("The askpass helper is there now", StringComparison.Ordinal));
    }

    [Fact]
    public void A_helper_found_at_the_start_is_not_missing()
    {
        using var folder = new TempDirectory("helper");
        var helper = folder.Combine(AskPassLocator.FileName);
        File.WriteAllText(helper, string.Empty);
        using var broker = new AskPassBroker(new RecordingPromptHandler(), new ListLog(), helperPath: helper);
        IGitPromptBroker prompts = broker;

        Assert.Null(prompts.MissingHelperPath);
        Assert.Null(prompts.CheckHelper());
    }

    /// <summary>The line on macOS and Linux, where ssh shares the terminal VisualCommit was started from.</summary>
    private const string UnixLine =
        "Git and ssh get SSH_ASKPASS_REQUIRE=force, so that ssh asks through VisualCommit; OpenSSH older than 8.4 ignores it and asks in the terminal VisualCommit was started from, if there is one.";

    /// <summary>The line on Windows, where VisualCommit is a window program and starts git without a console a terminal shows.</summary>
    private const string WindowsLine =
        "Git and ssh get SSH_ASKPASS_REQUIRE=force, so that ssh asks through VisualCommit; OpenSSH older than 8.4 ignores it, and on Windows no terminal shows its question: cancel the operation if it waits.";

    [Fact]
    public void The_line_about_ssh_comes_once_per_session_at_its_first_ask_or_deny_scope()
    {
        var log = new ListLog();
        using var broker = new AskPassBroker(new RecordingPromptHandler(), log);
        var line = (LogLevel.Info, OperatingSystem.IsWindows() ? WindowsLine : UnixLine);

        using (broker.Begin("Commit", "repo", PromptMode.None))
        {
            Assert.DoesNotContain(line, log.Entries);
        }

        using (broker.Begin("Fetch origin", "repo", PromptMode.Deny))
        {
            Assert.Equal(line, Assert.Single(log.Entries, entry => entry.Message.StartsWith("Git and ssh get SSH_ASKPASS_REQUIRE=force", StringComparison.Ordinal)));
        }

        using (broker.Begin("Push", "repo", PromptMode.Ask))
        using (broker.Begin("Pull", "repo", PromptMode.Deny))
        {
            Assert.Single(log.Entries, entry => entry == line);
        }

        Assert.Equal(AskPassBroker.AskPassRequireLine, line.Item2);
    }
}
