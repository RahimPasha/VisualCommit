using System.Globalization;
using System.Text.Json.Serialization;

namespace VisualCommit.Hosting.AzureDevOps;

// Azure DevOps' answers as the client reads them (docs/phase-7b-design.md 6.7): camelCase, unknown
// members ignored (the $id of an error, _links, the identities' URLs), `required` only where every
// answer has it (g4 D4 §4: one empty repository without defaultBranch would make a whole list
// Malformed). Dates are text, read by AzureDevOpsDates.DateOf: no member is a DateTimeOffset, since
// an offset-less date read as one is a JsonException east of UTC only (g4 D4 §3).

/// <summary>A list's answer, <c>{"count": n, "value": […]}</c> (MEASURED r5 2.1).</summary>
internal sealed class AzureDevOpsListJson<T>
{
    public required List<T> Value { get; set; }

    public int? Count { get; set; }
}

/// <summary><c>connectionData</c> (MEASURED r5 1.2 anonymously; a member's READ g1 G3.2): its <c>authenticatedUser</c> alone is read.</summary>
internal sealed class AzureDevOpsConnectionJson
{
    public AzureDevOpsIdentityJson? AuthenticatedUser { get; set; }
}

/// <summary>An identity: <c>metaTypeId</c> is not read, since it tells no member from the Anonymous one (g1 G0.4).</summary>
internal sealed class AzureDevOpsIdentityJson
{
    public string? Id { get; set; }

    public string? Descriptor { get; set; }

    public string? ProviderDisplayName { get; set; }

    public string? CustomDisplayName { get; set; }

    public AzureDevOpsPropertiesJson? Properties { get; set; }
}

/// <summary>An identity's <c>properties</c>: <c>Account</c> with a capital A, which camelCase would miss silently (MEASURED g4 D4 §1).</summary>
internal sealed class AzureDevOpsPropertiesJson
{
    [JsonPropertyName("Account")]
    public AzureDevOpsPropertyJson? Account { get; set; }
}

/// <summary>A property's <c>{"$type", "$value"}</c> (MEASURED r5 1.2).</summary>
internal sealed class AzureDevOpsPropertyJson
{
    [JsonPropertyName("$type")]
    public string? Type { get; set; }

    [JsonPropertyName("$value")]
    public string? Value { get; set; }
}

/// <summary>A resource area (MEASURED g1 G4.2 anonymously): its <c>locationUrl</c> names the organization in its spelling.</summary>
internal sealed class AzureDevOpsResourceAreaJson
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? LocationUrl { get; set; }
}

/// <summary>A repository (MEASURED r5 2.1): <c>defaultBranch</c> absent when empty, <c>isFork</c> only when true; <c>parentRepository</c> on a single one (READ).</summary>
internal sealed class AzureDevOpsRepositoryJson
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required AzureDevOpsProjectJson Project { get; set; }

    public string? DefaultBranch { get; set; }

    public string? RemoteUrl { get; set; }

    public string? WebUrl { get; set; }

    public bool IsDisabled { get; set; }

    public bool IsFork { get; set; }

    public AzureDevOpsRepositoryRefJson? ParentRepository { get; set; }
}

/// <summary>A repository's <c>project</c> (MEASURED r5 2.1); <c>lastUpdateTime</c> may have no offset (g4 D4 §3).</summary>
internal sealed class AzureDevOpsProjectJson
{
    public string? Id { get; set; }

    public required string Name { get; set; }

    public string? Visibility { get; set; }

    public string? LastUpdateTime { get; set; }
}

/// <summary>A repository named inside another answer (a parent, a fork source, a request's): every member may be missing.</summary>
internal sealed class AzureDevOpsRepositoryRefJson
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public AzureDevOpsProjectRefJson? Project { get; set; }
}

/// <summary>The project of <see cref="AzureDevOpsRepositoryRefJson"/>.</summary>
internal sealed class AzureDevOpsProjectRefJson
{
    public string? Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>
/// A pull request (MEASURED r5 3.1): no web address; <c>createdBy.uniqueName</c> (an e-mail) is
/// never read; a fork's <c>sourceRefName</c> is <c>refs/pull/&lt;n&gt;/source</c> and its head in
/// <c>forkSource</c> (READ g1 G2.6).
/// </summary>
internal sealed class AzureDevOpsPullRequestJson
{
    public required int PullRequestId { get; set; }

    public string? Status { get; set; }

    public AzureDevOpsIdentityRefJson? CreatedBy { get; set; }

    public string? CreationDate { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public required string SourceRefName { get; set; }

    public required string TargetRefName { get; set; }

    public bool IsDraft { get; set; }

    public AzureDevOpsCommitJson? LastMergeSourceCommit { get; set; }

    public AzureDevOpsForkSourceJson? ForkSource { get; set; }
}

/// <summary>An IdentityRef: its <c>displayName</c> alone is read (MEASURED r5 3.1).</summary>
internal sealed class AzureDevOpsIdentityRefJson
{
    public string? DisplayName { get; set; }
}

/// <summary>A commit named in a pull request.</summary>
internal sealed class AzureDevOpsCommitJson
{
    public string? CommitId { get; set; }
}

/// <summary>A fork's pull request's <c>forkSource</c>, <c>{name, objectId, repository}</c> (READ g1 G2.6).</summary>
internal sealed class AzureDevOpsForkSourceJson
{
    public string? Name { get; set; }

    public AzureDevOpsRepositoryRefJson? Repository { get; set; }
}

/// <summary>
/// A5's body (r5 4, READ): both refs as <c>refs/heads/…</c>, the title, the description, the draft
/// flag and, across forks, <c>forkSource.repository.id</c> (g1 G1.2; the minimal shape ASSUMED).
/// </summary>
internal sealed class AzureDevOpsNewPullRequestJson
{
    public required string SourceRefName { get; set; }

    public required string TargetRefName { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public bool IsDraft { get; set; }

    public AzureDevOpsNewForkSourceJson? ForkSource { get; set; }
}

/// <summary>A5's <c>forkSource</c>: the fork's repository by id.</summary>
internal sealed class AzureDevOpsNewForkSourceJson
{
    public required AzureDevOpsIdJson Repository { get; set; }
}

/// <summary><c>{"id": …}</c>.</summary>
internal sealed class AzureDevOpsIdJson
{
    public required string Id { get; set; }
}

/// <summary>
/// An error (MEASURED r5 1.4): <c>message</c> and <c>typeKey</c>, which the client classifies by
/// (never by the words, ruling 20); or TF400677's <c>{"count", "value": {"Message"}}</c> (MEASURED g1 G4.2).
/// </summary>
internal sealed class AzureDevOpsErrorJson
{
    public string? Message { get; set; }

    public string? TypeKey { get; set; }

    public AzureDevOpsErrorValueJson? Value { get; set; }
}

/// <summary>TF400677's <c>value</c>, its <c>Message</c> with a capital M.</summary>
internal sealed class AzureDevOpsErrorValueJson
{
    [JsonPropertyName("Message")]
    public string? Message { get; set; }
}

/// <summary>Azure DevOps' dates, which may have no offset (MEASURED r5 2.2, g4 D4 §3).</summary>
internal static class AzureDevOpsDates
{
    /// <summary>
    /// <paramref name="text"/> as a date, taken as UTC when it names no offset (<c>0001-01-01T00:00:00</c>
    /// is <see cref="DateTimeOffset.MinValue"/>), in UTC; <see cref="DateTimeOffset.MinValue"/> when it
    /// is missing or cannot be read.
    /// </summary>
    public static DateTimeOffset DateOf(string? text) =>
        text is not null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : DateTimeOffset.MinValue;
}

/// <summary>Azure DevOps' names: camelCase, unknown members ignored, nulls checked against the shapes above, nothing null written.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(AzureDevOpsConnectionJson))]
[JsonSerializable(typeof(AzureDevOpsResourceAreaJson))]
[JsonSerializable(typeof(AzureDevOpsRepositoryJson))]
[JsonSerializable(typeof(AzureDevOpsListJson<AzureDevOpsRepositoryJson>), TypeInfoPropertyName = "RepositoryList")]
[JsonSerializable(typeof(AzureDevOpsPullRequestJson))]
[JsonSerializable(typeof(AzureDevOpsListJson<AzureDevOpsPullRequestJson>), TypeInfoPropertyName = "PullRequestList")]
[JsonSerializable(typeof(AzureDevOpsNewPullRequestJson))]
[JsonSerializable(typeof(AzureDevOpsErrorJson))]
internal sealed partial class AzureDevOpsJsonContext : JsonSerializerContext;
