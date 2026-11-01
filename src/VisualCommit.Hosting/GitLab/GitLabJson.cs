using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisualCommit.Hosting.GitLab;

// GitLab's REST shapes (r6 1.3, 3.1, 4.1, 5.1), as far as the provider reads them
// (docs/phase-7b-design.md 7.3, 7.4). Field names are GitLab's snake_case through GitLabJsonContext;
// fields not listed are ignored. A member is `required` only where every answer has it (section 1's
// rule; g4 D4 §4 and §5: one row without it would lose the whole list), and a null where none is
// allowed fails the parse. No shape has an address member: `GET user`'s `email`, `commit_email` and
// `public_email` and an author's `public_email` are never read (D346). The error bodies (r6 10's
// shapes A to G) are read by GitLabProfile without a shape, since `message` is a string, an array
// or an object.

/// <summary>A user (<c>GET user</c>, a merge request's <c>author</c>).</summary>
internal sealed class GitLabUserJson
{
    public long Id { get; set; }

    public string? Username { get; set; }

    public string? Name { get; set; }
}

/// <summary>A project (A2's rows, A3, a fork's lookup, <c>forked_from_project</c>).</summary>
internal sealed class GitLabProjectJson
{
    public required long Id { get; set; }

    public required string PathWithNamespace { get; set; }

    public string? DefaultBranch { get; set; }

    public string? Visibility { get; set; }

    public bool Archived { get; set; }

    public string? Description { get; set; }

    public string? HttpUrlToRepo { get; set; }

    public string? WebUrl { get; set; }

    public DateTimeOffset? LastActivityAt { get; set; }

    public GitLabPermissionsJson? Permissions { get; set; }

    public GitLabProjectJson? ForkedFromProject { get; set; }
}

/// <summary>A project's <c>permissions</c> for the token's user (r6 3.1): either access may be null.</summary>
internal sealed class GitLabPermissionsJson
{
    public GitLabAccessJson? ProjectAccess { get; set; }

    public GitLabAccessJson? GroupAccess { get; set; }
}

/// <summary>One access: <c>access_level</c> 10 Guest to 50 Owner (r6 3.1).</summary>
internal sealed class GitLabAccessJson
{
    public int AccessLevel { get; set; }
}

/// <summary>A merge request (A4's rows, A5's answer; r6 4.1, MEASURED): the members every one has are required.</summary>
internal sealed class GitLabMergeRequestJson
{
    public required int Iid { get; set; }

    public required string Title { get; set; }

    public GitLabUserJson? Author { get; set; }

    public bool Draft { get; set; }

    public required string WebUrl { get; set; }

    public required string SourceBranch { get; set; }

    public required string TargetBranch { get; set; }

    public string? Sha { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public required long SourceProjectId { get; set; }

    public required long TargetProjectId { get; set; }
}

/// <summary>
/// A5's body (r6 5.1): never <c>draft</c>, which GitLab does not declare; <c>target_project_id</c>
/// and <c>allow_collaboration</c> only across projects (left out when null).
/// </summary>
internal sealed class GitLabNewMergeRequestJson
{
    public required string SourceBranch { get; set; }

    public required string TargetBranch { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    public long? TargetProjectId { get; set; }

    public bool? AllowCollaboration { get; set; }
}

/// <summary>GitLab's names: snake_case, unknown fields ignored, nulls checked against the shapes above, nothing null written (7a's options, <c>GitHubJsonContext</c>).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(GitLabUserJson))]
[JsonSerializable(typeof(GitLabProjectJson))]
[JsonSerializable(typeof(List<GitLabProjectJson>))]
[JsonSerializable(typeof(GitLabMergeRequestJson))]
[JsonSerializable(typeof(List<GitLabMergeRequestJson>))]
[JsonSerializable(typeof(GitLabNewMergeRequestJson))]
internal sealed partial class GitLabJsonContext : JsonSerializerContext;

/// <summary>
/// What a GitLab error body says (r6 10, g2 G2), read without a shape: the text the failure shows,
/// rack-oauth2's <c>error</c> code (shape F), and the fields of a validation's object (shape C).
/// </summary>
/// <param name="Text">GitLab's words by 7.3's rule; null when the body holds none.</param>
/// <param name="Code">Shape F's <c>error</c> (<c>invalid_token</c>, <c>insufficient_scope</c>, <c>insufficient_granular_scope</c>); null otherwise.</param>
/// <param name="Fields">Shape C's keys, in the body's order; empty otherwise.</param>
internal sealed record GitLabError(string? Text, string? Code, IReadOnlyList<string> Fields)
{
    private static readonly GitLabError None = new(null, null, []);

    /// <summary>
    /// Reads <paramref name="body"/>: <c>error_description</c> (F) ?? <c>message</c> as a string (A)
    /// ?? its first item (B) ?? each field and its first message, <c>"; "</c> between (C) ??
    /// <c>message.error</c> (D) ?? <c>error</c> (E). Any other body (not JSON, another shape, a string
    /// that is not valid UTF-8 or holds a lone surrogate) holds none.
    /// </summary>
    public static GitLabError Of(byte[] body)
    {
        if (body.Length == 0)
        {
            return None;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return None;
            }

            var code = root.TryGetProperty("error_description", out _) && StringOf(root, "error") is { } named ? named : null;
            var fields = new List<string>();
            string? text = StringOf(root, "error_description");
            if (text is null && root.TryGetProperty("message", out var message))
            {
                text = message.ValueKind switch
                {
                    JsonValueKind.String => NonEmpty(message.GetString()),
                    JsonValueKind.Array => message.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? NonEmpty(item.GetString()) : null).FirstOrDefault(),
                    JsonValueKind.Object => ObjectText(message, fields),
                    _ => null,
                };
            }

            return new GitLabError(text ?? StringOf(root, "error"), code, fields);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // JsonDocument.Parse leaves a string's UTF-8 and escapes unchecked; GetString and a
            // property's Name check them and throw InvalidOperationException. Such a body holds no
            // words, so its kind still comes from the status alone (7.3).
            return None;
        }
    }

    /// <summary>Shape D's <c>error</c> when it has one; else shape C's fields, each with its first message.</summary>
    private static string? ObjectText(JsonElement message, List<string> fields)
    {
        if (StringOf(message, "error") is { } error)
        {
            return error;
        }

        var parts = new List<string>();
        foreach (var field in message.EnumerateObject())
        {
            if (field.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            fields.Add(field.Name);
            var first = field.Value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? NonEmpty(item.GetString()) : null).FirstOrDefault(item => item is not null);
            parts.Add(first is null ? field.Name : field.Name + " " + first);
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? StringOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? NonEmpty(value.GetString()) : null;

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
