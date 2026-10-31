using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisualCommit.Hosting.GitHub;

// GitHub's REST shapes, as far as the provider reads them (docs/phase-7a-design.md 6.2, 6.3). Field
// names are GitHub's snake_case through GitHubJsonContext; fields not listed are ignored. A field
// the provider needs is `required`, and a null where none is allowed fails the parse, so an answer
// without them is Malformed rather than a half-filled record.

/// <summary>A user (<c>GET user</c>, a pull request's <c>user</c>, a commit's <c>author</c>); <c>{}</c> leaves every field unset.</summary>
internal sealed class GitHubUserJson
{
    public string? Login { get; set; }

    public long Id { get; set; }

    public string? Name { get; set; }

    public string? AvatarUrl { get; set; }
}

/// <summary>A repository (A2, A3, a pull request's <c>head.repo</c> and <c>base.repo</c>).</summary>
internal sealed class GitHubRepositoryJson
{
    public required string Name { get; set; }

    public required GitHubOwnerJson Owner { get; set; }

    public bool Private { get; set; }

    public bool Fork { get; set; }

    public bool Archived { get; set; }

    public string? Description { get; set; }

    public string? DefaultBranch { get; set; }

    public string? CloneUrl { get; set; }

    public string? HtmlUrl { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public GitHubPermissionsJson? Permissions { get; set; }

    public GitHubRepositoryJson? Parent { get; set; }
}

/// <summary>A repository's <c>owner</c>.</summary>
internal sealed class GitHubOwnerJson
{
    public required string Login { get; set; }
}

/// <summary>The token's permissions on a repository.</summary>
internal sealed class GitHubPermissionsJson
{
    public bool Push { get; set; }
}

/// <summary>A pull request (A4, A5).</summary>
internal sealed class GitHubPullRequestJson
{
    public required int Number { get; set; }

    public required string Title { get; set; }

    public GitHubUserJson? User { get; set; }

    public bool Draft { get; set; }

    public required string HtmlUrl { get; set; }

    public required GitHubBranchJson Head { get; set; }

    public required GitHubBranchJson Base { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A pull request's <c>head</c> or <c>base</c>: its branch, SHA and repository (a deleted fork's null).</summary>
internal sealed class GitHubBranchJson
{
    public required string Ref { get; set; }

    public required string Sha { get; set; }

    public GitHubRepositoryJson? Repo { get; set; }
}

/// <summary>One commit of A6's list: the git author's e-mail and the GitHub user it is linked to (null or <c>{}</c> when none).</summary>
internal sealed class GitHubCommitItemJson
{
    public GitHubCommitJson? Commit { get; set; }

    public GitHubUserJson? Author { get; set; }
}

/// <summary>A commit's git data.</summary>
internal sealed class GitHubCommitJson
{
    public GitHubCommitAuthorJson? Author { get; set; }
}

/// <summary>A commit's git author.</summary>
internal sealed class GitHubCommitAuthorJson
{
    public string? Email { get; set; }
}

/// <summary>GitHub's error body: <c>message</c> and <c>errors</c> (a 422's array, a 400's one string; <see cref="GitHubErrorsConverter"/>).</summary>
internal sealed class GitHubErrorJson
{
    public string? Message { get; set; }

    [JsonConverter(typeof(GitHubErrorsConverter))]
    public List<GitHubErrorItemJson>? Errors { get; set; }
}

/// <summary>One of <c>errors</c>: a 422's object, or a string as its <c>message</c>.</summary>
internal sealed class GitHubErrorItemJson
{
    public string? Field { get; set; }

    public string? Code { get; set; }

    public string? Message { get; set; }
}

/// <summary>
/// <c>errors</c> in each shape GitHub sends (D342): an array of objects (a 422's: <c>field</c>,
/// <c>code</c>, <c>message</c>, other members and a known one's value that is not a string
/// skipped), an array of strings, or one string (a 400 for an unsupported
/// <c>X-GitHub-Api-Version</c>, the live run of 2026-10-30), each string an item with that
/// <c>message</c>. Any other value is skipped and gives null, so it never costs the body its
/// <c>message</c>. Read only: nothing writes an error body.
/// </summary>
internal sealed class GitHubErrorsConverter : JsonConverter<List<GitHubErrorItemJson>>
{
    public override List<GitHubErrorItemJson>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return [new GitHubErrorItemJson { Message = reader.GetString() }];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        var items = new List<GitHubErrorItemJson>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                items.Add(new GitHubErrorItemJson { Message = reader.GetString() });
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                items.Add(ReadItem(ref reader));
            }
            else
            {
                reader.Skip();
            }
        }

        return items;
    }

    public override void Write(Utf8JsonWriter writer, List<GitHubErrorItemJson> value, JsonSerializerOptions options) =>
        throw new NotSupportedException("GitHub's errors are read, never written.");

    // From an object's start to its end: the strings of field, code and message.
    private static GitHubErrorItemJson ReadItem(ref Utf8JsonReader reader)
    {
        var item = new GitHubErrorItemJson();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            reader.Read();
            var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            if (text is null)
            {
                reader.Skip();
                continue;
            }

            switch (name)
            {
                case "field":
                    item.Field = text;
                    break;
                case "code":
                    item.Code = text;
                    break;
                case "message":
                    item.Message = text;
                    break;
            }
        }

        return item;
    }
}

/// <summary>A5's body; <c>maintainer_can_modify</c> only across forks (left out when null).</summary>
internal sealed class GitHubNewPullRequestJson
{
    public required string Title { get; set; }

    public required string Body { get; set; }

    public required string Head { get; set; }

    public required string Base { get; set; }

    public bool Draft { get; set; }

    public bool? MaintainerCanModify { get; set; }
}

/// <summary>GitHub's names: snake_case, unknown fields ignored, nulls checked against the shapes above, nothing null written.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(GitHubUserJson))]
[JsonSerializable(typeof(GitHubRepositoryJson))]
[JsonSerializable(typeof(List<GitHubRepositoryJson>))]
[JsonSerializable(typeof(GitHubPullRequestJson))]
[JsonSerializable(typeof(List<GitHubPullRequestJson>))]
[JsonSerializable(typeof(List<GitHubCommitItemJson>))]
[JsonSerializable(typeof(GitHubErrorJson))]
[JsonSerializable(typeof(GitHubNewPullRequestJson))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
