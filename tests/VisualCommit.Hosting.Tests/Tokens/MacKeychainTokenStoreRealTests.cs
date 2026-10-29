using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// The macOS keychain through <c>/usr/bin/security</c> itself (docs/phase-7a-design.md 3.9, D259,
/// D270): only where <c>VISUALCOMMIT_KEYCHAIN_TESTS=macos</c> turns it on, which CI's macOS job
/// does. The class makes a keychain file of its own with a random password, unlocked and without an
/// automatic lock, and deletes it in <c>finally</c>; the login keychain is never named.
/// </summary>
public sealed class MacKeychainTokenStoreRealTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task A_throwaway_keychain_saves_reads_replaces_and_deletes_a_token()
    {
        Assert.SkipUnless(
            RealTokenStoreSteps.Enabled("macos"),
            $"{TokenStores.TestsVariable}=macos turns on the real macOS keychain's test; only CI sets it.");
        var ct = TestContext.Current.CancellationToken;
        var folder = Directory.CreateTempSubdirectory("vc-test-keychain-");
        var file = Path.Combine(folder.FullName, "vc-test.keychain-db");
        var password = Guid.NewGuid().ToString("N");
        try
        {
            await SecurityAsync(ct, "create-keychain", "-p", password, file);
            await SecurityAsync(ct, "set-keychain-settings", file);
            await SecurityAsync(ct, "unlock-keychain", "-p", password, file);
            var log = new ListLog();
            var store = new MacKeychainStore(log, keychainFile: file, application: RealTokenStoreSteps.TestApplication());

            await RealTokenStoreSteps.RunAsync(store, log, ct);
        }
        finally
        {
            await TokenProcessRunner.Instance.RunAsync(MacKeychainStore.SecurityPath, ["delete-keychain", file], null, Limit, CancellationToken.None);
            folder.Delete(recursive: true);
        }
    }

    private static async Task SecurityAsync(CancellationToken ct, params string[] arguments)
    {
        var result = await TokenProcessRunner.Instance.RunAsync(MacKeychainStore.SecurityPath, arguments, null, Limit, ct);
        Assert.True(result.ExitCode == 0, $"security {arguments[0]} ended with {result}: {result.StandardError}");
    }
}
