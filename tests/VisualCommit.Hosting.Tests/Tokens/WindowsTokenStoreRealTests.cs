using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// Windows' Credential Manager itself (docs/phase-7a-design.md 3.9, 3.10, D270, D333): only where
/// <c>VISUALCOMMIT_KEYCHAIN_TESTS=windows</c> turns it on, which CI's Windows job does and the
/// laptop never does. Its entries are named <c>VisualCommit-test:&lt;guid&gt;</c> and hold dummy
/// secrets; a refused runner (error 1312) fails the probe's step rather than skipping it.
/// </summary>
public sealed class WindowsTokenStoreRealTests
{
    [Fact]
    public async Task The_Credential_Manager_saves_reads_replaces_and_deletes_a_token()
    {
        Assert.SkipUnless(
            RealTokenStoreSteps.Enabled("windows"),
            $"{TokenStores.TestsVariable}=windows turns on the real Credential Manager's test; only CI sets it.");
        var log = new ListLog();
        var store = new WindowsCredentialStore(log, RealTokenStoreSteps.TestApplication());

        // The longest token that fits is saved too: 1,280 characters (2,560 bytes).
        var longest = "vc-test-" + new string('x', WindowsCredentialStore.MaxTokenLength - "vc-test-".Length);
        await RealTokenStoreSteps.RunAsync(store, log, TestContext.Current.CancellationToken, longest);
    }
}
