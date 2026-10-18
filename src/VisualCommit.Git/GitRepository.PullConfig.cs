using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Git's pull settings as Pull reads them (phase 3 design 2.18, D84; docs/phase-6-design.md 8.9):
/// the readers <see cref="IntegratePullAsync(GitRef, PullIntegration, Func{string, Task}?, NetworkOptions?, PullIntegrationOptions?, CancellationToken)"/>
/// calls, moved here unchanged from <c>GitRepository.Remotes.cs</c>, their enums now Core's
/// <see cref="PullFastForwardSetting"/> and <see cref="PullRebaseSetting"/>. Work package WP2 builds
/// <see cref="ReadPullConfigurationAsync"/> on them.
/// </summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// Pull's settings for <paramref name="branch"/>, read before its fetch (8.9). Work package WP2
    /// gives it its body; nothing calls it before then.
    /// </summary>
    public Task<PullConfiguration> ReadPullConfigurationAsync(string branch, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Work package WP2 builds ReadPullConfigurationAsync (docs/phase-6-design.md 8.9).");

    /// <summary><c>pull.ff</c> as git pull reads it: a boolean (<c>--ff</c> or <c>--no-ff</c>), or <c>only</c>.</summary>
    private async Task<PullFastForwardSetting> ReadPullFastForwardAsync(CancellationToken cancellationToken)
    {
        const string Key = "pull.ff";
        if (await ReadPullSettingAsync(Key, cancellationToken).ConfigureAwait(false) is not { } value)
        {
            return PullFastForwardSetting.Unset;
        }

        if (await ReadPullBoolAsync(Key, value, cancellationToken).ConfigureAwait(false) is { } flag)
        {
            return flag ? PullFastForwardSetting.Always : PullFastForwardSetting.Never;
        }

        return value == "only" ? PullFastForwardSetting.Only : throw RemoteArguments.NotUnderstood(Key, value);
    }

    /// <summary>
    /// <c>branch.&lt;b&gt;.rebase</c>, or when it is unset <c>pull.rebase</c>, as git pull reads it:
    /// a boolean, <c>merges</c> (<c>m</c>) or <c>interactive</c> (<c>i</c>).
    /// </summary>
    private async Task<PullRebaseSetting> ReadPullRebaseAsync(string branch, CancellationToken cancellationToken)
    {
        var key = $"branch.{branch}.rebase";
        var value = await ReadPullSettingAsync(key, cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            key = "pull.rebase";
            value = await ReadPullSettingAsync(key, cancellationToken).ConfigureAwait(false);
        }

        if (value is null)
        {
            return PullRebaseSetting.None;
        }

        if (await ReadPullBoolAsync(key, value, cancellationToken).ConfigureAwait(false) is { } flag)
        {
            return flag ? PullRebaseSetting.Rebase : PullRebaseSetting.None;
        }

        return value switch
        {
            "merges" or "m" => PullRebaseSetting.Merges,
            "interactive" or "i" => PullRebaseSetting.Rebase,
            _ => throw RemoteArguments.NotUnderstood(key, value),
        };
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
}
