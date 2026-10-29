using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The shapes of the fake's answers (docs/phase-7a-design.md 2.4, r5 10.4): snake_case fields,
/// times in UTC as <c>yyyy-MM-ddTHH:mm:ssZ</c>, addresses on <see cref="Endpoints"/>.
/// </summary>
public sealed partial class FakeGitHub
{
    private const string DocumentationUrl = "https://docs.github.com/rest";

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Time(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static byte[] Serialize(JsonNode node) => JsonSerializer.SerializeToUtf8Bytes(node, JsonOptions);

    private Uri Web(string relative) => new(Endpoints.WebBase, relative);

    /// <summary>A simple user (<c>login</c>, <c>id</c>, <c>type</c>, <c>avatar_url</c>, <c>html_url</c>); A1 adds <c>name</c>.</summary>
    private JsonObject UserJson(FakeUser user, bool withName = false)
    {
        var json = new JsonObject
        {
            ["login"] = user.Login,
            ["id"] = user.Id,
            ["type"] = user.IsOrganization ? "Organization" : "User",
            ["avatar_url"] = Web(string.Create(CultureInfo.InvariantCulture, $"avatars/u/{user.Id}?v=4")).AbsoluteUri,
            ["html_url"] = Web(user.Login).AbsoluteUri,
            ["site_admin"] = false,
        };
        if (withName)
        {
            json["name"] = user.Name;
        }

        return json;
    }

    /// <summary>A user by login; a login the fake does not know is a ghost of id 0.</summary>
    private JsonObject UserJson(string login)
    {
        FakeUser? user;
        lock (_gate)
        {
            user = UserLocked(login);
        }

        return UserJson(user ?? new FakeUser(login, 0, null));
    }

    /// <summary>A repository, with the permissions of <paramref name="login"/>; on A3 a fork's <c>parent</c> and <c>source</c>.</summary>
    private JsonObject RepositoryJson(FakeRepository repository, string? login, bool withParent = false)
    {
        var pushes = login is not null && repository.MayPush(login);
        var owns = login is not null && repository.IsOwnedBy(login);
        var json = new JsonObject
        {
            ["id"] = repository.Id,
            ["name"] = repository.Name,
            ["full_name"] = repository.FullName,
            ["owner"] = UserJson(repository.Owner),
            ["private"] = repository.IsPrivate,
            ["fork"] = repository.ForkOf is not null,
            ["archived"] = repository.IsArchived,
            ["description"] = repository.Description,
            ["default_branch"] = repository.DefaultBranch,
            ["html_url"] = Web(repository.FullName).AbsoluteUri,
            ["clone_url"] = Web(repository.FullName + ".git").AbsoluteUri,
            ["ssh_url"] = $"git@127.0.0.1:{repository.FullName}.git",
            ["created_at"] = Time(repository.UpdatedAt),
            ["updated_at"] = Time(repository.UpdatedAt),
            ["pushed_at"] = Time(repository.UpdatedAt),
            ["visibility"] = repository.IsPrivate ? "private" : "public",
            ["permissions"] = new JsonObject
            {
                ["admin"] = owns,
                ["maintain"] = owns,
                ["push"] = pushes,
                ["triage"] = pushes,
                ["pull"] = true,
            },
        };

        if (withParent && repository.ForkOf is not null)
        {
            FakeRepository? parent;
            lock (_gate)
            {
                parent = RepositoryLocked(repository.ForkOf);
            }

            if (parent is not null)
            {
                json["parent"] = RepositoryJson(parent, login);
                var source = parent;
                for (var depth = 0; depth < 16 && source.ForkOf is not null; depth++)
                {
                    FakeRepository? next;
                    lock (_gate)
                    {
                        next = RepositoryLocked(source.ForkOf);
                    }

                    if (next is null)
                    {
                        break;
                    }

                    source = next;
                }

                json["source"] = RepositoryJson(source, login);
            }
        }

        return json;
    }

    /// <summary>A pull request, its <c>head</c> and <c>base</c> with their labels, SHAs, users and repositories (a deleted fork's <c>repo</c> null).</summary>
    private JsonObject PullJson(FakePullRequest pull, FakeRepository baseRepository, string? login)
    {
        var headOwner = pull.HeadOwner(baseRepository.Owner);
        FakeRepository? headRepository;
        FakeUser? headUser;
        string baseSha;
        lock (_gate)
        {
            headRepository = pull.HeadRepository is null ? baseRepository : RepositoryLocked(pull.HeadRepository);
            headUser = UserLocked(headOwner);
            baseSha = _branches.TryGetValue(baseRepository.FullName, out var refs) && refs.TryGetValue("refs/heads/" + pull.BaseRef, out var sha) ? sha : string.Empty;
        }

        var html = Web(string.Create(CultureInfo.InvariantCulture, $"{baseRepository.FullName}/pull/{pull.Number}")).AbsoluteUri;
        return new JsonObject
        {
            ["id"] = 4000 + pull.Number,
            ["number"] = pull.Number,
            ["state"] = pull.State,
            ["draft"] = pull.IsDraft,
            ["title"] = pull.Title,
            ["body"] = pull.Body,
            ["html_url"] = html,
            ["url"] = new Uri(Endpoints.ApiBase, string.Create(CultureInfo.InvariantCulture, $"repos/{baseRepository.FullName}/pulls/{pull.Number}")).AbsoluteUri,
            ["created_at"] = Time(pull.CreatedAt),
            ["updated_at"] = Time(pull.CreatedAt),
            ["user"] = UserJson(pull.Author),
            ["head"] = new JsonObject
            {
                ["label"] = headOwner + ":" + pull.HeadRef,
                ["ref"] = pull.HeadRef,
                ["sha"] = pull.HeadSha,
                ["user"] = headUser is null ? null : UserJson(headUser),
                ["repo"] = headRepository is null ? null : RepositoryJson(headRepository, login),
            },
            ["base"] = new JsonObject
            {
                ["label"] = baseRepository.Owner + ":" + pull.BaseRef,
                ["ref"] = pull.BaseRef,
                ["sha"] = baseSha,
                ["user"] = UserJson(baseRepository.Owner),
                ["repo"] = RepositoryJson(baseRepository, login),
            },
            ["maintainer_can_modify"] = pull.MaintainerCanModify,
        };
    }

    /// <summary>A commit of A6, its <c>author</c> and <c>committer</c> the users their e-mails are linked to, else null.</summary>
    private JsonObject CommitJson(FakeRepository repository, FakeCommit commit)
    {
        return new JsonObject
        {
            ["sha"] = commit.Sha,
            ["html_url"] = Web($"{repository.FullName}/commit/{commit.Sha}").AbsoluteUri,
            ["commit"] = new JsonObject
            {
                ["author"] = new JsonObject { ["name"] = commit.AuthorName, ["email"] = commit.AuthorEmail, ["date"] = Time(commit.AuthorDate) },
                ["committer"] = new JsonObject { ["name"] = commit.CommitterName, ["email"] = commit.CommitterEmail, ["date"] = Time(commit.CommitterDate) },
                ["message"] = commit.Subject,
            },
            ["author"] = LinkedUserJson(commit.AuthorEmail),
            ["committer"] = LinkedUserJson(commit.CommitterEmail),
        };
    }

    private JsonObject? LinkedUserJson(string email)
    {
        FakeUser? user;
        lock (_gate)
        {
            user = _emails.TryGetValue(email, out var login) ? UserLocked(login) : null;
        }

        return user is null ? null : UserJson(user);
    }

    /// <summary>GitHub's error body: <c>message</c>, <c>documentation_url</c>, <c>status</c> (a string), and a 422's <c>errors</c>.</summary>
    private static JsonObject ErrorJson(int status, string message, JsonObject? error = null)
    {
        var json = new JsonObject { ["message"] = message };
        if (error is not null)
        {
            json["errors"] = new JsonArray(error);
        }

        json["documentation_url"] = DocumentationUrl;
        json["status"] = status.ToString(CultureInfo.InvariantCulture);
        return json;
    }
}
