using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Git's pull settings as Pull reads them (phase 3 design 2.18, D84; docs/phase-6-design.md 8.9):
/// one read before Pull's fetch, which
/// <see cref="IntegratePullAsync(GitRef, PullIntegration, Func{string, Task}?, NetworkOptions?, PullIntegrationOptions?, CancellationToken)"/>
/// then integrates by, or makes itself when it is given none.
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// Pull's settings for <paramref name="branch"/> (its short name), read as <c>git pull</c> reads
    /// them at its start (8.9): <c>pull.ff</c>, a boolean (<c>--ff</c> or <c>--no-ff</c>) or
    /// <c>only</c>; then <c>branch.&lt;b&gt;.rebase</c>, or when it is unset <c>pull.rebase</c>, a
    /// boolean, <c>merges</c> (<c>m</c>) or <c>interactive</c> (<c>i</c>). Booleans are read as git
    /// reads them (<see cref="ReadPullBoolAsync"/>), so <c>m</c> and a unit letter alone follow each
    /// git (0.3). A value git pull would refuse fills <see cref="PullConfiguration.Problem"/> with
    /// "&lt;key&gt; is set to &lt;value&gt;, which this app does not understand." and stops the read
    /// there, as git pull stops: after a <c>pull.ff</c> problem the rebase setting is not read
    /// (<see cref="PullConfiguration.Rebase"/> none, its key <c>pull.rebase</c>, no value); after the
    /// branch's own key an invalid <c>pull.rebase</c> is never read, as git never reads it. A read.
    /// </summary>
    public async Task<PullConfiguration> ReadPullConfigurationAsync(string branch, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        var (fastForward, fastForwardValue, refused) = await ReadPullFastForwardAsync(cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        var key = $"branch.{branch}.rebase";
        var value = await ReadPullSettingAsync(key, cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            key = PullConfigKeys.Rebase;
            value = await ReadPullSettingAsync(key, cancellationToken).ConfigureAwait(false);
        }

        var rebase = PullRebaseSetting.None;
        if (value is not null)
        {
            if (await ReadPullBoolAsync(key, value, cancellationToken).ConfigureAwait(false) is { } flag)
            {
                rebase = flag ? PullRebaseSetting.Rebase : PullRebaseSetting.None;
            }
            else
            {
                switch (value)
                {
                    case "merges" or "m":
                        rebase = PullRebaseSetting.Merges;
                        break;
                    case "interactive" or "i":
                        rebase = PullRebaseSetting.Rebase;
                        break;
                    default:
                        return new PullConfiguration(fastForward, PullRebaseSetting.None, key, value, fastForwardValue, RemoteArguments.NotUnderstood(key, value), key);
                }
            }
        }

        return new PullConfiguration(fastForward, rebase, key, value, fastForwardValue, null, null);
    }

    /// <summary>
    /// <c>pull.ff</c> alone, for the rebase chosen for one pull (8.9, D231): what
    /// <see cref="ReadPullFastForwardAsync"/> reads, as a configuration whose rebase setting is none
    /// and unread, or the refused value's configuration. A read.
    /// </summary>
    public async Task<PullConfiguration> ReadPullFastForwardSettingAsync(CancellationToken cancellationToken = default)
    {
        var (fastForward, value, refused) = await ReadPullFastForwardAsync(cancellationToken).ConfigureAwait(false);
        return refused ?? new PullConfiguration(fastForward, PullRebaseSetting.None, PullConfigKeys.Rebase, null, value, null, null);
    }

    /// <summary>
    /// <c>pull.ff</c> as <c>git pull</c> reads it first, whenever no <c>--ff</c> option is given, a
    /// <c>git pull --rebase</c> included (8.9): a boolean (<see cref="ReadPullBoolAsync"/>) or
    /// <c>only</c>, with its value as set (null: unset); for a value git would refuse, the
    /// configuration Pull reports (<see cref="PullConfiguration.Problem"/>, the rebase setting not
    /// read), else null.
    /// </summary>
    private async Task<(PullFastForwardSetting Setting, string? Value, PullConfiguration? Refused)> ReadPullFastForwardAsync(CancellationToken cancellationToken)
    {
        var value = await ReadPullSettingAsync(PullConfigKeys.FastForward, cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            return (PullFastForwardSetting.Unset, null, null);
        }

        if (await ReadPullBoolAsync(PullConfigKeys.FastForward, value, cancellationToken).ConfigureAwait(false) is { } flag)
        {
            return (flag ? PullFastForwardSetting.Always : PullFastForwardSetting.Never, value, null);
        }

        if (value == "only")
        {
            return (PullFastForwardSetting.Only, value, null);
        }

        var refused = new PullConfiguration(
            PullFastForwardSetting.Unset,
            PullRebaseSetting.None,
            PullConfigKeys.Rebase,
            null,
            value,
            RemoteArguments.NotUnderstood(PullConfigKeys.FastForward, value),
            PullConfigKeys.FastForward);
        return (PullFastForwardSetting.Unset, value, refused);
    }

    /// <summary>A setting's value (<c>git config --get</c>, the last one wins), or null when it is not set.</summary>
    private async Task<string?> ReadPullSettingAsync(string key, CancellationToken cancellationToken)
    {
        var command = Read("config", "--get", key);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 1 ? null : result.EnsureSuccess(command).StandardOutput.TrimEnd('\r', '\n');
    }

    /// <summary>
    /// <paramref name="value"/> of <paramref name="key"/> read as git reads a boolean, or null
    /// when it is not one. The common words are read here; anything else is asked of git
    /// (<c>git config --type=bool</c>), which knows its own rules: a key without a value is true,
    /// a number is a boolean, and so is <c>m</c>, which Git 2.36 reads as the number 0 with the
    /// unit "mega" before it would read it as <c>merges</c> (git pull then merges).
    /// </summary>
    private async Task<bool?> ReadPullBoolAsync(string key, string value, CancellationToken cancellationToken)
    {
        switch (value.ToLowerInvariant())
        {
            case "true" or "yes" or "on":
                return true;
            case "false" or "no" or "off":
                return false;
        }

        var result = await _runner.RunAsync(Read("config", "--type=bool", "--get", key), cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? result.StandardOutput.Trim() == "true" : null;
    }

    /// <summary>The keys of <see cref="ReadPullConfigurationAsync"/>, kept out of the partial class so that their names cannot meet another package's.</summary>
    private static class PullConfigKeys
    {
        public const string FastForward = "pull.ff";
        public const string Rebase = "pull.rebase";
    }
}
