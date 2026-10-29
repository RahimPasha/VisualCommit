using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>What the hosting reads of the repository (docs/phase-7a-design.md 1.3): its URL rewrites.</summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// The repository's URL rewrites (docs/phase-7a-design.md 4.3): one
    /// <c>git config --null --get-regexp ^url\.</c> in the working folder, exit 1 meaning none;
    /// another failure throws <see cref="GitException"/>. <c>GIT_CONFIG_PARAMETERS</c> is removed,
    /// as in a scope bound to an account (D329), so the read sees the rules the bound calls apply.
    /// The output's credentials are hidden in the call record (D103, <see cref="GitCommand.EndsAuthority"/>).
    /// </summary>
    public async Task<UrlRewrites> ReadUrlRewritesAsync(CancellationToken cancellationToken = default)
    {
        var environment = new Dictionary<string, string?>(ReadEnvironment) { ["GIT_CONFIG_PARAMETERS"] = null };
        var command = new GitCommand("config", "--null", "--get-regexp", @"^url\.") { WorkingDirectory = WorkingDirectory, Environment = environment };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return UrlRewrites.None;
        }

        var entries = new List<(string Key, string Value)>();
        foreach (var entry in result.EnsureSuccess(command).StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // A key without a value ("[url "x"] insteadOf") has no line feed and gives no rule.
            var newline = entry.IndexOf('\n', StringComparison.Ordinal);
            if (newline >= 0)
            {
                entries.Add((entry[..newline], entry[(newline + 1)..]));
            }
        }

        return UrlRewrites.Parse(entries);
    }
}
