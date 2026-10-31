using System.Text.Json;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// <c>accounts.json</c> (docs/phase-7a-design.md 7.2, 7.13; D287, D292, D336): its camelCase
/// names, no token, an unreadable file set aside, other hosts' records kept, and a missing
/// <c>tokenState</c> read as <c>keychain</c>.
/// </summary>
public sealed class JsonAccountStoreTests
{
    private static readonly AccountRecord Octocat = new()
    {
        Provider = "github",
        Host = "github.com",
        Login = "octocat",
        Id = 583231,
        Name = "The Octocat",
        AvatarUrl = "https://avatars.githubusercontent.com/u/583231?v=4",
        UseForGit = true,
        TokenState = AccountTokenStates.Keychain,
    };

    [Fact]
    public void A_round_trip_writes_the_designs_names_exactly_and_no_token()
    {
        using var data = new TempDirectory("accounts");
        var path = data.Combine("accounts.json");
        var store = new JsonAccountStore(path, new ListLog());

        store.Save(new AccountsFile { Accounts = [Octocat] });
        var text = File.ReadAllText(path);
        var read = store.Load();

        using var json = JsonDocument.Parse(text);
        Assert.Equal(["version", "accounts"], json.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
        var record = Assert.Single(json.RootElement.GetProperty("accounts").EnumerateArray());
        Assert.Equal(
            ["provider", "host", "login", "id", "name", "avatarUrl", "useForGit", "tokenState"],
            record.EnumerateObject().Select(property => property.Name));
        Assert.Equal(("github", "github.com", "octocat", 583231L, "keychain", true),
            (record.GetProperty("provider").GetString(), record.GetProperty("host").GetString(), record.GetProperty("login").GetString(),
             record.GetProperty("id").GetInt64(), record.GetProperty("tokenState").GetString(), record.GetProperty("useForGit").GetBoolean()));
        Assert.DoesNotContain("token\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Octocat, Assert.Single(read.Accounts));
    }

    [Fact]
    public void A_missing_file_is_no_account_and_an_unreadable_one_is_set_aside_with_a_warning()
    {
        using var data = new TempDirectory("accounts");
        var path = data.Combine("accounts.json");
        var log = new ListLog();
        var store = new JsonAccountStore(path, log);

        Assert.Empty(store.Load().Accounts);
        Assert.Empty(log.Lines);

        File.WriteAllText(path, "{ \"version\": 1, \"accounts\": [ { \"login\": ");
        Assert.Empty(store.Load().Accounts);

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(data.Combine("accounts.unreadable.json")));
        Assert.Contains(log.Lines, line => line.Level == LogLevel.Warning && line.Message.Contains("could not be read", StringComparison.Ordinal));
    }

    [Fact]
    public void Other_hosts_records_are_kept_and_a_save_replaces_only_the_hosts_first_github_record()
    {
        using var data = new TempDirectory("accounts");
        var path = data.Combine("accounts.json");
        File.WriteAllText(path, """
            {
              // Written by hand, with a comment and a trailing comma.
              "version": 1,
              "accounts": [
                { "provider": "gitlab", "host": "gitlab.com", "login": "octo", "id": 7 },
                { "provider": "github", "host": "127.0.0.1:47395", "login": "octo", "id": 1001, "tokenState": "session" },
                null,
                { "provider": "github", "host": "github.com", "login": "old", "id": 1, "useForGit": false, "tokenState": "refused" },
              ],
            }
            """);
        var store = new JsonAccountStore(path, new ListLog());

        var file = store.Load();
        Assert.Equal(3, file.Accounts.Count);
        Assert.Equal(("old", false, AccountTokenStates.Refused), (file.GitHubOn("github.com")!.Login, file.GitHubOn("github.com")!.UseForGit, file.GitHubOn("github.com")!.TokenState));
        Assert.Equal(AccountTokenStates.Session, file.GitHubOn("127.0.0.1:47395")!.TokenState);

        store.Save(file.WithGitHub(Octocat));
        var saved = store.Load();
        Assert.Equal(["gitlab.com", "127.0.0.1:47395", "github.com"], saved.Accounts.Select(record => record.Host));
        Assert.Equal(Octocat, saved.GitHubOn("github.com"));

        store.Save(saved.WithoutGitHub("github.com"));
        Assert.Equal(["gitlab.com", "127.0.0.1:47395"], store.Load().Accounts.Select(record => record.Host));
        Assert.Null(store.Load().GitHubOn("github.com"));
    }

    [Fact]
    public void A_missing_or_unknown_token_state_is_keychain_and_use_for_git_defaults_to_on()
    {
        using var data = new TempDirectory("accounts");
        var path = data.Combine("accounts.json");
        File.WriteAllText(path, """{ "accounts": [ { "provider": "github", "host": "github.com", "login": "octocat", "id": 583231 } ] }""");

        var record = new JsonAccountStore(path, new ListLog()).Load().GitHubOn("github.com")!;

        Assert.Null(record.TokenState);
        Assert.True(record.UseForGit);
        Assert.Equal(AccountTokenStates.Keychain, AccountTokenStates.Of(record.TokenState));
        Assert.Equal(AccountTokenStates.Keychain, AccountTokenStates.Of("cloud"));
        Assert.Equal(AccountTokenStates.Session, AccountTokenStates.Of("session"));
        Assert.Equal(AccountTokenStates.Refused, AccountTokenStates.Of("refused"));
    }

    [Fact]
    public void A_failed_write_is_logged_as_an_error_and_not_thrown()
    {
        using var data = new TempDirectory("accounts");
        Directory.CreateDirectory(data.Combine("accounts.json"));
        var log = new ListLog();

        new JsonAccountStore(data.Combine("accounts.json"), log).Save(new AccountsFile { Accounts = [Octocat] });

        Assert.Contains(log.Lines, line => line.Level == LogLevel.Error && line.Message.Contains("could not be saved", StringComparison.Ordinal));
    }
}
