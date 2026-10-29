using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// <see cref="TokenEntryNames"/> and <see cref="TokenStoreReasons"/> (docs/phase-7a-design.md
/// 3.3): what each platform's store calls a token's entry, and which keys no store is asked about.
/// </summary>
public sealed class TokenEntryNamesTests
{
    private static readonly TokenKey Octocat = new("github", "github.com", "octocat");

    [Fact]
    public void A_key_is_lower_case_whatever_the_account_says()
    {
        var key = TokenKey.Of(new AccountKey(HostingProvider.GitHub, "GitHub.COM", "OctoCat"));

        Assert.Equal(new TokenKey("github", "github.com", "octocat"), key);
        Assert.Equal("github:github.com:octocat", key.ToString());
    }

    [Fact]
    public void Windows_names_the_entry_by_application_provider_host_and_account()
    {
        var names = TokenEntryNames.For(Octocat);

        Assert.Equal("VisualCommit:github:github.com:octocat", names.WindowsTarget);
        Assert.Equal(names.Entry, names.WindowsTarget);
        Assert.Equal("octocat", names.WindowsUser);
        Assert.Equal("GitHub token for octocat, kept by VisualCommit", names.WindowsComment);
    }

    [Fact]
    public void MacOS_names_the_service_by_host_and_the_account_by_login()
    {
        var names = TokenEntryNames.For(Octocat);

        Assert.Equal("VisualCommit:github:github.com", names.MacService);
        Assert.Equal("octocat", names.MacAccount);
        Assert.Equal("VisualCommit: GitHub (octocat)", names.MacLabel);
    }

    [Fact]
    public void The_Secret_Service_searches_four_attributes_and_creates_five()
    {
        var names = TokenEntryNames.For(Octocat);

        Assert.Equal(
            [
                new("application", "VisualCommit"),
                new("provider", "github"),
                new("host", "github.com"),
                new("account", "octocat"),
            ],
            names.SecretServiceSearch);
        Assert.Equal(
            [.. names.SecretServiceSearch, new("xdg:schema", "io.github.rahimpasha.VisualCommit.Token")],
            names.SecretServiceAttributes);
        Assert.Equal("VisualCommit: GitHub token for octocat", names.SecretServiceLabel);
    }

    [Fact]
    public void The_fake_host_keeps_its_port_in_every_name()
    {
        var names = TokenEntryNames.For(new TokenKey("github", "127.0.0.1:47395", "octocat"));

        Assert.Equal("VisualCommit:github:127.0.0.1:47395:octocat", names.WindowsTarget);
        Assert.Equal("VisualCommit:github:127.0.0.1:47395", names.MacService);
        Assert.Contains(new KeyValuePair<string, string>("host", "127.0.0.1:47395"), names.SecretServiceAttributes);
    }

    [Fact]
    public void A_test_prefix_takes_the_application_part_of_every_name()
    {
        var application = "VisualCommit-test:" + Guid.NewGuid();

        var names = TokenEntryNames.For(Octocat, application);

        Assert.Equal($"{application}:github:github.com:octocat", names.WindowsTarget);
        Assert.Equal($"{application}:github:github.com", names.MacService);
        Assert.Equal(application, names.SecretServiceSearch[0].Value);
        Assert.StartsWith(application, names.MacLabel, StringComparison.Ordinal);
        Assert.StartsWith(application, names.SecretServiceLabel, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("git")]
    [InlineData("gh")]
    [InlineData("git:https")]
    [InlineData("GH:github.com")]
    public void No_name_starts_as_Git_Credential_Managers_or_GitHub_CLIs_do(string application)
    {
        Assert.False(TokenEntryNames.TryFor(Octocat, application, out var names));
        Assert.Null(names);
        Assert.Throws<ArgumentException>(() => TokenEntryNames.For(Octocat, application));

        var ours = TokenEntryNames.For(Octocat);
        Assert.All(
            [ours.WindowsTarget, ours.MacService],
            name => Assert.False(
                name.StartsWith("git:", StringComparison.OrdinalIgnoreCase) || name.StartsWith("gh:", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("", "github.com", "octocat")]
    [InlineData("github", "", "octocat")]
    [InlineData("github", "github.com", "")]
    [InlineData("github", "github.com", "octo cat")]
    [InlineData("github", "github.com", "octo'cat")]
    [InlineData("github", "github.com", "octo\\cat")]
    [InlineData("github", "github.com", "octo\ncat")]
    [InlineData("github", "github.com\t", "octocat")]
    [InlineData("git'hub", "github.com", "octocat")]
    [InlineData("github", "github.com", "octo\u0000cat")]
    public void A_key_part_that_cannot_be_named_is_refused(string provider, string host, string account)
    {
        var key = new TokenKey(provider, host, account);

        Assert.False(TokenEntryNames.TryFor(key, TokenEntryNames.Application, out _));
        Assert.Throws<ArgumentException>(() => TokenEntryNames.For(key));
    }

    [Fact]
    public void The_log_names_a_key_by_host_and_account_with_control_characters_escaped()
    {
        Assert.Equal("github.com/octocat", TokenEntryNames.ForLog(Octocat));
        Assert.Equal("127.0.0.1:47395/octo cat", TokenEntryNames.ForLog(new TokenKey("github", "127.0.0.1:47395", "octo cat")));
        Assert.Equal(
            "github.com\\u000d\\u000a/octo\\u000acat\\u2028\\u202e\\u005c\\u0000 x",
            TokenEntryNames.ForLog(new TokenKey("github", "github.com\r\n", "octo\ncat\u2028\u202e\\\0 x")));
    }

    [Fact]
    public void An_application_part_that_cannot_be_named_is_refused()
    {
        Assert.False(TokenEntryNames.TryFor(Octocat, string.Empty, out _));
        Assert.False(TokenEntryNames.TryFor(Octocat, "Visual Commit", out _));
        Assert.False(TokenEntryNames.TryFor(Octocat, "Visual'Commit", out _));
    }

    [Fact]
    public void The_reasons_are_seven_texts_that_name_no_store_and_no_system()
    {
        Assert.Equal(7, TokenStoreReasons.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(TokenStoreReasons.All, reason =>
        {
            Assert.DoesNotContain("Windows", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("macOS", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Linux", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret Service", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Credential Manager", reason, StringComparison.OrdinalIgnoreCase);
            Assert.False(reason.EndsWith('.'), reason);
        });
        Assert.Equal("no keychain was found on this computer", TokenStoreReasons.NoKeychain);
        Assert.Equal("the keychain refused it", TokenStoreReasons.Refused);
    }
}
