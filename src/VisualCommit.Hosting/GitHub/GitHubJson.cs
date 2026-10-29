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

/// <summary>GitHub's error body: <c>message</c> and a 422's <c>errors</c>.</summary>
internal sealed class GitHubErrorJson
{
    public string? Message { get; set; }

    public List<GitHubErrorItemJson>? Errors { get; set; }
}

/// <summary>One of a 422's <c>errors</c>.</summary>
internal sealed class GitHubErrorItemJson
{
    public string? Field { get; set; }

    public string? Code { get; set; }

    public string? Message { get; set; }
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
