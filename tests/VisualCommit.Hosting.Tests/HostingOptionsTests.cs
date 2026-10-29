using VisualCommit.Core.Logging;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// <see cref="HostingOptions"/> and <see cref="GitHubEndpoints"/> (docs/phase-7a-design.md 1.4,
/// 1.8, 6.5): where the environment says GitHub is.
/// </summary>
public sealed class HostingOptionsTests
{
    [Theory]
    [InlineData("http://x:ghp_FAKE-octocat-1@127.0.0.1:47395/")]
    [InlineData("http://ghp_FAKE-octocat-1@127.0.0.1:47395/")]
    [InlineData("ghp_FAKE-octocat-1")]
    public void A_value_that_is_not_accepted_turns_GitHub_off_and_stays_out_of_the_warning(string value)
    {
        // Section 1's rule: no token in a log line. A user part is refused, and the warning names
        // the variable without its value, so nothing of what was typed reaches the log.
        var log = new ListLog();

        var options = HostingOptions.FromEnvironment(name => name == HostingOptions.GitHubVariable ? value : null, log);

        Assert.Same(HostingOptions.Off, options);
        var line = Assert.Single(log.Lines);
        Assert.Equal(
            (LogLevel.Warning, "VISUALCOMMIT_GITHUB_URL is neither none nor http://127.0.0.1:<port>/: GitHub is off."),
            line);
        Assert.DoesNotContain("FAKE", line.Message, StringComparison.Ordinal);
    }
}
