using System.Text.Json;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// <c>accounts.json</c> (docs/phase-7a-design.md 7.2, 7.13; D287, D292, D336): its camelCase
/// names, no token, an unreadable file set aside, other hosts' records kept, and a missing
/// <c>tokenState</c> read as <c>keychain</c>; 7b's records per provider (docs/phase-7b-design.md 9.3,
/// 9.10): three providers' names exactly, <c>userId</c> only where held, <see cref="AccountsFile.For"/>,
/// <see cref="AccountsFile.With"/> and <see cref="AccountsFile.Without"/>, and 7a's reader shape on a
/// 7b file both ways.
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
        Assert.Equal(("old", false, AccountTokenStates.Refused), (file.For(HostingProvider.GitHub, "github.com")!.Login, file.For(HostingProvider.GitHub, "github.com")!.UseForGit, file.For(HostingProvider.GitHub, "github.com")!.TokenState));
        Assert.Equal(AccountTokenStates.Session, file.For(HostingProvider.GitHub, "127.0.0.1:47395")!.TokenState);

        store.Save(file.With(Octocat));
        var saved = store.Load();
        Assert.Equal(["gitlab.com", "127.0.0.1:47395", "github.com"], saved.Accounts.Select(record => record.Host));
        Assert.Equal(Octocat, saved.For(HostingProvider.GitHub, "github.com"));

        store.Save(saved.Without(HostingProvider.GitHub, "github.com"));
        Assert.Equal(["gitlab.com", "127.0.0.1:47395"], store.Load().Accounts.Select(record => record.Host));
        Assert.Null(store.Load().For(HostingProvider.GitHub, "github.com"));
    }

    [Fact]
    public void A_missing_or_unknown_token_state_is_keychain_and_use_for_git_defaults_to_on()
    {
        using var data = new TempDirectory("accounts");
        var path = data.Combine("accounts.json");
        File.WriteAllText(path, """{ "accounts": [ { "provider": "github", "host": "github.com", "login": "octocat", "id": 583231 } ] }""");

        var record = new JsonAccountStore(path, new ListLog()).Load().For(HostingProvider.GitHub, "github.com")!;

        Assert.Null(record.TokenState);
        Assert.True(record.UseForGit);
        Assert.Equal(AccountTokenStates.Keychain, AccountTokenStates.Of(record.TokenState));
        Assert.Equal(AccountTokenStates.Keychain, AccountTokenStates.Of("cloud"));
        Assert.Equal(AccountTokenStates.Session, AccountTokenStates.Of("session"));
        Assert.Equal(AccountTokenStates.Refused, AccountTokenStates.Of("refused"));
    }

    private static readonly AccountRecord Ada = new()
    {
        Provider = "gitlab",
        Host = "gitlab.com",
        Login = "Ada",
        Id = 2001,
        Name = "Ada Lovelace",
        UseForGit = true,
        TokenState = AccountTokenStates.Keychain,
    };

    private static readonly AccountRecord FakeOrg = new()
    {
        Provider = "azure-devops",
        Host = "dev.azure.com",
        Login = "Fake-Org",
        Id = 0,
        Name = null,
        UseForGit = false,
        TokenState = AccountTokenStates.Session,
        UserId = "0f3c1d2e-4b5a-4c6d-8e7f-90a1b2c3d4e5",
    };

    [Fact]
    public void Three_providers_records_round_trip_with_exact_names_and_user_id_only_where_held()
    {
        using var data = new TempDirectory("accounts");
        var path = data.Combine("accounts.json");
        var store = new JsonAccountStore(path, new ListLog());

        store.Save(new AccountsFile { Accounts = [Octocat, Ada, FakeOrg] });
        var text = File.ReadAllText(path);

        using var json = JsonDocument.Parse(text);
        var records = json.RootElement.GetProperty("accounts").EnumerateArray().ToList();
        string[] sevenA = ["provider", "host", "login", "id", "name", "avatarUrl", "useForGit", "tokenState"];
        Assert.Equal(sevenA, records[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(sevenA, records[1].EnumerateObject().Select(property => property.Name));
        Assert.Equal([.. sevenA, "userId"], records[2].EnumerateObject().Select(property => property.Name));
        Assert.Equal(("azure-devops", JsonValueKind.Number, 0L, JsonValueKind.String, FakeOrg.UserId),
            (records[2].GetProperty("provider").GetString(), records[2].GetProperty("id").ValueKind, records[2].GetProperty("id").GetInt64(),
             records[2].GetProperty("userId").ValueKind, records[2].GetProperty("userId").GetString()));
        Assert.Equal(JsonValueKind.Null, records[2].GetProperty("avatarUrl").ValueKind);
        Assert.Equal([Octocat, Ada, FakeOrg], store.Load().Accounts);
    }

    [Fact]
    public void For_with_and_without_match_the_provider_and_host_without_case_and_keep_every_other_record()
    {
        var file = new AccountsFile
        {
            Accounts =
            [
                new AccountRecord { Provider = "bitbucket", Host = "bitbucket.org", Login = "x" },
                Octocat,
                Ada with { Provider = "GitLab", Host = "GITLAB.com" },
                FakeOrg,
                Ada with { Login = "second" },
            ],
        };

        Assert.Equal("Ada", file.For(HostingProvider.GitLab, "gitlab.com")!.Login);
        Assert.Same(FakeOrg, file.For(HostingProvider.AzureDevOps, "DEV.azure.com"));
        Assert.Null(file.For(HostingProvider.AzureDevOps, "127.0.0.1:2"));
        Assert.Null(file.For(HostingProvider.GitHub, "gitlab.com"));

        var grace = Ada with { Login = "grace", Id = 2002 };
        var replaced = file.With(grace);
        Assert.Equal(["x", "octocat", "grace", "Fake-Org", "second"], replaced.Accounts.Select(record => record.Login));
        var appended = file.With(FakeOrg with { Host = "127.0.0.1:2" });
        Assert.Equal(6, appended.Accounts.Count);
        Assert.Equal("127.0.0.1:2", appended.Accounts[^1].Host);

        var without = file.Without(HostingProvider.GitLab, "gitlab.com");
        Assert.Equal(["x", "octocat", "Fake-Org", "second"], without.Accounts.Select(record => record.Login));
        Assert.Equal(["x", "octocat", "Ada", "second"], file.Without(HostingProvider.AzureDevOps, "dev.azure.com").Accounts.Select(record => record.Login));
        Assert.Same(file.Accounts[0], without.Accounts[0]);
    }

    [Fact]
    public void Sevens_a_reader_shape_reads_a_file_7b_wrote_and_its_save_read_by_7b_has_no_user_id()
    {
        using var data = new TempDirectory("accounts");
        var path = data.Combine("accounts.json");
        var store = new JsonAccountStore(path, new ListLog());
        store.Save(new AccountsFile { Accounts = [Octocat, Ada, FakeOrg] });

        var old = JsonSerializer.Deserialize(File.ReadAllText(path), Store7a.AccountsJsonContext.Default.AccountsFile)!;
        Assert.Equal(3, old.Accounts.Count);
        var gitHub = old.GitHubOn("github.com")!;
        Assert.Equal(
            (Octocat.Provider, Octocat.Host, Octocat.Login, Octocat.Id, Octocat.Name, Octocat.AvatarUrl, Octocat.UseForGit, Octocat.TokenState),
            (gitHub.Provider, gitHub.Host, gitHub.Login, gitHub.Id, gitHub.Name, gitHub.AvatarUrl, gitHub.UseForGit, gitHub.TokenState));
        Assert.Equal(("azure-devops", "Fake-Org", 0L, false), (old.Accounts[2].Provider, old.Accounts[2].Login, old.Accounts[2].Id, old.Accounts[2].UseForGit));

        File.WriteAllText(path, JsonSerializer.Serialize(old, Store7a.AccountsJsonContext.Default.AccountsFile));
        var read = store.Load();
        Assert.Equal(Octocat, read.For(HostingProvider.GitHub, "github.com"));
        Assert.Equal(Ada, read.For(HostingProvider.GitLab, "gitlab.com"));
        Assert.Equal(FakeOrg with { UserId = null }, read.For(HostingProvider.AzureDevOps, "dev.azure.com"));
        Assert.DoesNotContain("userId", File.ReadAllText(path), StringComparison.Ordinal);
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
