using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// The steps every real store's class takes against its throw-away entry (docs/phase-7a-design.md
/// 3.9, D270): probe, read nothing, save, read, save another, read, delete, read nothing, delete
/// nothing; and a delete in <c>finally</c> whatever failed. A failed step's message quotes the log,
/// where the store wrote the platform's detail (the Win32 error, <c>security</c>'s error line, the
/// D-Bus error's name).
/// </summary>
internal static class RealTokenStoreSteps
{
    /// <summary>Whether <see cref="TokenStores.TestsVariable"/> names <paramref name="store"/>.</summary>
    public static bool Enabled(string store) =>
        string.Equals(Environment.GetEnvironmentVariable(TokenStores.TestsVariable), store, StringComparison.Ordinal);

    /// <summary>The application part of a test's entry names: never the app's own entries.</summary>
    public static string TestApplication() => "VisualCommit-test:" + Guid.NewGuid();

    /// <summary>A dummy secret, unique to the run.</summary>
    public static string DummySecret() => "vc-test-" + Guid.NewGuid();

    public static async Task RunAsync(ITokenStore store, ListLog log, CancellationToken ct, params string[] moreTokens)
    {
        var key = new TokenKey("github", "github.com", "vc-test-" + Guid.NewGuid().ToString("N")[..12]);
        var first = DummySecret();
        var second = DummySecret();
        string[] tokens = [first, second, .. moreTokens];
        try
        {
            var probe = await store.ProbeAsync(ct);
            Check(log, probe.Available, $"the probe found no store ({probe.Reason})");
            await ExpectNotFoundAsync(store, log, key, "before the first save", ct);

            foreach (var token in tokens)
            {
                var saved = await store.SaveAsync(key, token, ct);
                Check(log, saved.Outcome == TokenStoreOutcome.Done, $"a save of {token.Length} characters ended {saved.Outcome} ({saved.Reason})");
                var read = await store.ReadAsync(key, ct);
                Check(log, read.Outcome == TokenStoreOutcome.Done, $"the read after a save ended {read.Outcome} ({read.Reason})");
                Check(log, string.Equals(read.Token, token, StringComparison.Ordinal), "the read did not give the token last saved");
            }

            var deleted = await store.DeleteAsync(key, ct);
            Check(log, deleted.Outcome == TokenStoreOutcome.Done, $"the delete ended {deleted.Outcome} ({deleted.Reason})");
            await ExpectNotFoundAsync(store, log, key, "after the delete", ct);
            var again = await store.DeleteAsync(key, ct);
            Check(log, again.Outcome == TokenStoreOutcome.NotFound, $"the second delete ended {again.Outcome} ({again.Reason})");
        }
        finally
        {
            await store.DeleteAsync(key, CancellationToken.None);
        }

        Assert.All(log.Lines, line => Assert.All(tokens, token => Assert.DoesNotContain(token, line.Message, StringComparison.Ordinal)));
    }

    private static async Task ExpectNotFoundAsync(ITokenStore store, ListLog log, TokenKey key, string when, CancellationToken ct)
    {
        var read = await store.ReadAsync(key, ct);
        Check(log, read.Outcome == TokenStoreOutcome.NotFound, $"the read {when} ended {read.Outcome} ({read.Reason})");
    }

    private static void Check(ListLog log, bool condition, string what)
    {
        if (!condition)
        {
            Assert.Fail($"{what}. The store's log:{Environment.NewLine}"
                + string.Join(Environment.NewLine, log.Lines.Select(line => $"  {line.Level}: {line.Message}")));
        }
    }
}
