using System.Text;
using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// <see cref="MacKeychainStore"/> over a recording runner (docs/phase-7a-design.md 3.5, 3.9): the
/// program, its arguments and the one line a save writes to <c>security -i</c>, the read-back, and
/// how each of <c>security</c>'s answers ends. Nothing is run.
/// </summary>
public sealed class MacKeychainStoreTests
{
    // A token holding a quote, a space and a line feed: base64 keeps them out of the line's quotes.
    private const string Token = "ghp_FAKE-it's a\ntoken";

    private static readonly TokenKey Octocat = new("github", "github.com", "octocat");
    private static readonly string Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Token));

    private readonly RecordingProcessRunner _runner = new();
    private readonly ListLog _log = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_save_writes_one_base64_line_to_security_i_and_reads_the_entry_back()
    {
        _runner.Answer(0, "security> ").Answer(0, Base64 + "\n");
        var store = Store();

        var result = await store.SaveAsync(Octocat, Token, _ct);

        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Done), result);
        Assert.Collection(
            _runner.Calls,
            save =>
            {
                Assert.Equal("/usr/bin/security", save.FileName);
                Assert.Equal(["-i"], save.Arguments);
                Assert.Equal(
                    $"add-generic-password -U -s 'VisualCommit:github:github.com' -a 'octocat' -l 'VisualCommit: GitHub (octocat)' -w '{Base64}'\n",
                    save.StandardInput);
                Assert.Equal(TimeSpan.FromSeconds(60), save.Limit);
            },
            back =>
            {
                Assert.Equal("/usr/bin/security", back.FileName);
                Assert.Equal(["find-generic-password", "-s", "VisualCommit:github:github.com", "-a", "octocat", "-w"], back.Arguments);
                Assert.Null(back.StandardInput);
            });
        Assert.Matches("^[^']*'[^']*'[^']*'[^']*'[^']*'[^']*' -w '[A-Za-z0-9+/=]+'\n$", _runner.Calls[0].StandardInput!);
        AssertNoTokenInTheArgumentsOrTheLog();
    }

    [Fact]
    public async Task With_a_keychain_file_every_command_names_it_last()
    {
        const string file = "/tmp/vc-test/vc-test.keychain-db";
        _runner.Answer(0).Answer(0, Base64 + "\n").Answer(0, Base64 + "\n").Answer(0);
        var store = Store(file);

        await store.SaveAsync(Octocat, Token, _ct);
        await store.ReadAsync(Octocat, _ct);
        await store.DeleteAsync(Octocat, _ct);

        var calls = _runner.Calls;
        Assert.EndsWith($" -w '{Base64}' '{file}'\n", calls[0].StandardInput, StringComparison.Ordinal);
        Assert.Equal(["find-generic-password", "-s", "VisualCommit:github:github.com", "-a", "octocat", "-w", file], calls[1].Arguments);
        Assert.Equal(["find-generic-password", "-s", "VisualCommit:github:github.com", "-a", "octocat", "-w", file], calls[2].Arguments);
        Assert.Equal(["delete-generic-password", "-s", "VisualCommit:github:github.com", "-a", "octocat", file], calls[3].Arguments);
        AssertNoTokenInTheArgumentsOrTheLog();
    }

    [Fact]
    public async Task A_read_decodes_the_base64_without_its_line_feed()
    {
        _runner.Answer(0, Base64 + "\n");

        var read = await Store().ReadAsync(Octocat, _ct);

        Assert.Equal((TokenStoreOutcome.Done, Token), (read.Outcome, read.Token));
        Assert.Equal(TimeSpan.FromSeconds(60), Assert.Single(_runner.Calls).Limit);
        AssertNoTokenInTheArgumentsOrTheLog();
    }

    [Fact]
    public async Task A_delete_names_the_service_and_account_without_a_file()
    {
        _runner.Answer(0);

        var result = await Store().DeleteAsync(Octocat, _ct);

        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Done), result);
        Assert.Equal(["delete-generic-password", "-s", "VisualCommit:github:github.com", "-a", "octocat"], Assert.Single(_runner.Calls).Arguments);
    }

    [Theory]
    [InlineData(44, "")]
    [InlineData(1, "security: SecKeychainSearchCopyNext: The specified item could not be found in the keychain.")]
    public async Task Exit_44_or_could_not_be_found_is_no_entry(int exitCode, string error)
    {
        _runner.Answer(exitCode, string.Empty, error).Answer(exitCode, string.Empty, error);
        var store = Store();

        var read = await store.ReadAsync(Octocat, _ct);
        var deleted = await store.DeleteAsync(Octocat, _ct);

        Assert.Equal((TokenStoreOutcome.NotFound, (string?)null), (read.Outcome, read.Reason));
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.NotFound), deleted);
    }

    [Fact]
    public async Task The_limit_is_no_answer()
    {
        _runner.Answer((int?)null).Answer((int?)null).Answer((int?)null);
        var store = Store();

        Assert.Equal(TokenStoreReasons.NoAnswer, (await store.SaveAsync(Octocat, Token, _ct)).Reason);
        Assert.Equal(TokenStoreReasons.NoAnswer, (await store.ReadAsync(Octocat, _ct)).Reason);
        Assert.Equal(TokenStoreReasons.NoAnswer, (await store.DeleteAsync(Octocat, _ct)).Reason);
        Assert.Equal(3, _runner.Calls.Count);
        AssertNoTokenInTheArgumentsOrTheLog();
    }

    [Fact]
    public async Task User_interaction_is_not_allowed_means_the_keychain_cannot_be_used()
    {
        const string error = "security: SecKeychainItemCopyContent: User interaction is not allowed.";
        _runner.Answer(1, string.Empty, error).Answer(0, string.Empty, error);
        var store = Store();

        Assert.Equal(TokenStoreReasons.CannotUse, (await store.ReadAsync(Octocat, _ct)).Reason);
        Assert.Equal(TokenStoreReasons.CannotUse, (await store.SaveAsync(Octocat, Token, _ct)).Reason);
        Assert.Equal(2, _runner.Calls.Count);
        Assert.Contains(_log.Lines, line => line.Message.Contains("User interaction is not allowed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_dismissed_unlock_dialog_leaves_the_keychain_locked_in_a_read()
    {
        _runner.Answer(128, string.Empty, "security: SecKeychainItemCopyContent: User canceled the operation.");

        var read = await Store().ReadAsync(Octocat, _ct);

        Assert.Equal((TokenStoreOutcome.Failed, TokenStoreReasons.Locked), (read.Outcome, read.Reason));
    }

    [Fact]
    public async Task A_dismissed_unlock_dialog_ends_a_save_without_the_read_back()
    {
        _runner.Answer(0, "security> ", "add-generic-password: User canceled the operation.");

        var result = await Store().SaveAsync(Octocat, Token, _ct);

        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Locked), result);
        Assert.Equal(["-i"], Assert.Single(_runner.Calls).Arguments);
        AssertNoTokenInTheArgumentsOrTheLog();
    }

    [Fact]
    public async Task A_read_back_that_differs_is_a_refusal()
    {
        var other = Convert.ToBase64String(Encoding.UTF8.GetBytes("ghp_FAKE-octocat-0"));
        _runner.Answer(0, "security> ", "add-generic-password: something went wrong").Answer(0, other + "\n");

        var result = await Store().SaveAsync(Octocat, Token, _ct);

        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused), result);
        Assert.Contains(_log.Lines, line => line.Message.Contains("holds another token", StringComparison.Ordinal));
        AssertNoTokenInTheArgumentsOrTheLog();
    }

    [Fact]
    public async Task A_read_back_that_finds_nothing_is_a_refusal()
    {
        _runner.Answer(0, "security> ").Answer(44);

        var result = await Store().SaveAsync(Octocat, Token, _ct);

        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused), result);
    }

    [Fact]
    public async Task Another_error_is_a_refusal_with_its_exit_code_and_first_line_logged()
    {
        _runner.Answer(51, string.Empty, "security: SecKeychainDelete: something odd\nmore detail");

        var result = await Store().DeleteAsync(Octocat, _ct);

        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused), result);
        Assert.Contains(
            _log.Lines,
            line => line.Message == "macOS keychain: security's delete for github.com/octocat failed (exit code 51: security: SecKeychainDelete: something odd).");
    }

    [Fact]
    public async Task A_save_error_line_that_quotes_the_base64_is_logged_without_it()
    {
        _runner.Answer(0, "security> ", $"add-generic-password: bad input near '{Base64}'").Answer(44);

        await Store().SaveAsync(Octocat, Token, _ct);

        AssertNoTokenInTheArgumentsOrTheLog();
        Assert.Contains(_log.Lines, line => line.Message.Contains("bad input near '***'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("not base64!\n")]
    [InlineData("YWJj\n\n")]
    [InlineData("YWJ\n")]
    [InlineData("\n")]
    [InlineData("/w==\n")]
    public async Task An_entry_that_is_not_base64_of_UTF8_is_unreadable(string output)
    {
        _runner.Answer(0, output);

        var read = await Store().ReadAsync(Octocat, _ct);

        Assert.Equal((TokenStoreOutcome.Failed, TokenStoreReasons.Unreadable), (read.Outcome, read.Reason));
    }

    [Fact]
    public async Task A_reads_result_shows_neither_the_token_nor_its_base64()
    {
        TokenProcessResult? seen = null;
        _runner.Answer(_ => seen = new TokenProcessResult(0, Base64 + "\n", string.Empty));

        var read = await Store().ReadAsync(Octocat, _ct);

        Assert.Equal(Token, read.Token);
        Assert.NotNull(seen);
        Assert.Equal("TokenProcessResult(exit code 0)", seen.ToString());
        Assert.DoesNotContain(Base64, seen.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("FAKE", seen.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("FAKE", read.ToString(), StringComparison.Ordinal);
        Assert.Equal("TokenProcessResult(stopped at the limit)", new TokenProcessResult(null, string.Empty, string.Empty).ToString());
    }

    [Fact]
    public async Task A_key_or_a_keychain_file_that_cannot_be_quoted_is_refused_before_any_call()
    {
        var quoted = Store("/tmp/it's.keychain-db");
        var badKey = new TokenKey("github", "github.com", "octo'cat");

        Assert.Equal(TokenStoreReasons.Refused, (await quoted.SaveAsync(Octocat, Token, _ct)).Reason);
        Assert.Equal(TokenStoreReasons.Refused, (await quoted.ReadAsync(Octocat, _ct)).Reason);
        Assert.Equal(TokenStoreReasons.Refused, (await quoted.DeleteAsync(Octocat, _ct)).Reason);
        Assert.Equal(TokenStoreReasons.Refused, (await Store().SaveAsync(badKey, Token, _ct)).Reason);
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public async Task The_probe_finds_no_keychain_without_security()
    {
        var store = new MacKeychainStore(_log, _runner) { FileExists = _ => false };

        Assert.Equal(new TokenStoreProbe(false, TokenStoreReasons.NoKeychain), await store.ProbeAsync(_ct));
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public async Task The_probe_asks_for_the_users_default_keychain_within_5_seconds()
    {
        _runner.Answer(0, "    \"/Users/octocat/Library/Keychains/login.keychain-db\"\n").Answer(50, string.Empty, "security: SecKeychainCopyDefault: A default keychain could not be found.");
        var store = Store();

        Assert.Equal(new TokenStoreProbe(true, null), await store.ProbeAsync(_ct));
        Assert.Equal(new TokenStoreProbe(false, TokenStoreReasons.NoKeychain), await store.ProbeAsync(_ct));
        Assert.All(_runner.Calls, call =>
        {
            Assert.Equal("/usr/bin/security", call.FileName);
            Assert.Equal(["default-keychain", "-d", "user"], call.Arguments);
            Assert.Equal(TimeSpan.FromSeconds(5), call.Limit);
        });
    }

    [Fact]
    public async Task With_a_keychain_file_the_probe_asks_only_whether_it_exists()
    {
        const string file = "/tmp/vc-test/vc-test.keychain-db";
        var present = new MacKeychainStore(_log, _runner, file) { FileExists = path => path is MacKeychainStore.SecurityPath or file };
        var absent = new MacKeychainStore(_log, _runner, file) { FileExists = path => path is MacKeychainStore.SecurityPath };

        Assert.Equal(new TokenStoreProbe(true, null), await present.ProbeAsync(_ct));
        Assert.Equal(new TokenStoreProbe(false, TokenStoreReasons.NoKeychain), await absent.ProbeAsync(_ct));
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public void The_store_names_itself_for_the_log_and_its_constructor_does_nothing()
    {
        var store = new MacKeychainStore(_log);

        Assert.Equal("macOS keychain", store.Name);
        Assert.True(store.KeepsTokensAcrossRestarts);
        Assert.Empty(_log.Lines);
    }

    private MacKeychainStore Store(string? keychainFile = null) =>
        new(_log, _runner, keychainFile) { FileExists = _ => true };

    private void AssertNoTokenInTheArgumentsOrTheLog()
    {
        Assert.All(_runner.Calls, call => Assert.All(call.Arguments, argument =>
        {
            Assert.DoesNotContain("FAKE", argument, StringComparison.Ordinal);
            Assert.DoesNotContain(Base64, argument, StringComparison.Ordinal);
        }));
        Assert.All(_log.Lines, line =>
        {
            Assert.DoesNotContain("FAKE", line.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Base64, line.Message, StringComparison.Ordinal);
        });
    }
}
