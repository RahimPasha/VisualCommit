using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// The Secret Service itself (docs/phase-7a-design.md 3.9, 3.10, D259, D270, D271): only where
/// <c>VISUALCOMMIT_KEYCHAIN_TESTS=secret-service</c> turns it on, which CI's bare containers do
/// under a private bus with a throw-away gnome-keyring:
/// <c>dbus-run-session -- sh -c 'echo pw | gnome-keyring-daemon --unlock --components=secrets &gt;/dev/null &amp;&amp; …'</c>,
/// with <c>XDG_DATA_HOME</c> an empty folder holding <c>keyrings/</c>.
/// </summary>
public sealed class SecretServiceTokenStoreRealTests
{
    [Fact]
    public async Task The_Secret_Service_saves_reads_replaces_and_deletes_a_token()
    {
        Assert.SkipUnless(
            RealTokenStoreSteps.Enabled("secret-service"),
            $"{TokenStores.TestsVariable}=secret-service turns on the real Secret Service's test; only CI sets it, under a private bus.");
        var log = new ListLog();
        var store = new SecretServiceStore(log, application: RealTokenStoreSteps.TestApplication());

        await RealTokenStoreSteps.RunAsync(store, log, TestContext.Current.CancellationToken);
    }
}
