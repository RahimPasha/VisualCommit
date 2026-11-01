using System.Reflection;
using System.Text.Json;
using VisualCommit.Hosting.AzureDevOps;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Azure DevOps' answers as the client reads them (docs/phase-7b-design.md 6.7), on made-up copies
/// of r5's and g1's shapes, each with its mark: their member names, nesting and order kept, every
/// value made up but the services' published constants (the Anonymous identity's id). The
/// capital-A <c>Account</c>, errors with <c>$id</c> and TF400677's <c>value</c>, a repository
/// without <c>defaultBranch</c>, the zero date, <see cref="AzureDevOpsDates.DateOf"/>, and no
/// <see cref="DateTimeOffset"/> member.
/// </summary>
public sealed class AzureDevOpsJsonTests
{
    /// <summary>An anonymous connectionData in r5 1.2's shape (MEASURED), its instance ids made up.</summary>
    private const string AnonymousConnection = """
        {"authenticatedUser":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","descriptor":"System:PublicAccess;aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","providerDisplayName":"Anonymous","customDisplayName":"Anonymous","isActive":true,"properties":{"Account":{"$type":"System.String","$value":"Anonymous"}},"resourceVersion":2,"metaTypeId":255},"authorizedUser":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","descriptor":"System:PublicAccess;aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","providerDisplayName":"Anonymous","customDisplayName":"Anonymous","isActive":true,"properties":{"Account":{"$type":"System.String","$value":"Anonymous"}},"resourceVersion":2,"metaTypeId":255},"instanceId":"7b0a0001-0000-4a00-8000-00000000fa4e","deploymentId":"7b0a0002-0000-4a00-8000-00000000fa4e","deploymentType":"hosted","locationServiceData":{"serviceOwner":"7b0a0003-0000-4a00-8000-00000000fa4e","defaultAccessMappingMoniker":"PublicAccessMapping","lastChangeId":1001,"lastChangeId64":1001}}
        """;

    /// <summary>A member's connectionData in g1 G3.2 sample 1's member names and nesting (READ), every value made up.</summary>
    private const string MemberConnection = """
        {"authenticatedUser":{"id":"7b0a1111-0000-4a00-8000-000000000001","descriptor":"Microsoft.IdentityModel.Claims.ClaimsIdentity;7b0a0004-0000-4a00-8000-00000000fa4e\\ada.lovelace@example.com","subjectDescriptor":"aad.N2IwYTExMTEtMDAwMC00YTAwLTgwMDAtMDAwMDAwMDAwMDAx","providerDisplayName":"Ada Lovelace","isActive":true,"properties":{"Account":{"$type":"System.String","$value":"ada.lovelace@example.com"}},"resourceVersion":2,"metaTypeId":0},"authorizedUser":{"id":"7b0a1111-0000-4a00-8000-000000000001"},"instanceId":"7b0a0001-0000-4a00-8000-00000000fa4e","deploymentType":"hosted"}
        """;

    /// <summary>A list of repositories in r5 2.1's shape (MEASURED): an empty one without <c>defaultBranch</c>, and a project date without offset (g4 D4 §3).</summary>
    private const string Repositories = """
        {"value":[{"id":"7b0a2222-0000-4a00-8000-000000000001","name":"mobile-app","url":"http://127.0.0.1:2/Fernhill-Labs/7b0a3333-0000-4a00-8000-000000000001/_apis/git/repositories/7b0a2222-0000-4a00-8000-000000000001","project":{"id":"7b0a3333-0000-4a00-8000-000000000001","name":"Mobile App","url":"http://127.0.0.1:2/_apis/projects/7b0a3333-0000-4a00-8000-000000000001","state":"wellFormed","revision":11,"visibility":"private","lastUpdateTime":"0001-01-01T00:00:00"},"defaultBranch":"refs/heads/main","size":1234,"remoteUrl":"http://Fernhill-Labs@127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app","sshUrl":"git@ssh.dev.azure.com:v3/Fernhill-Labs/Mobile%20App/mobile-app","webUrl":"http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/mobile-app","isDisabled":false,"isInMaintenance":false},{"id":"7b0a2222-0000-4a00-8000-000000000002","name":"sandbox","url":"http://127.0.0.1:2/x","project":{"id":"7b0a3333-0000-4a00-8000-000000000001","name":"Mobile App","visibility":"private"},"size":0,"remoteUrl":"http://Fernhill-Labs@127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/sandbox","webUrl":"http://127.0.0.1:2/Fernhill-Labs/Mobile%20App/_git/sandbox","isDisabled":false,"isInMaintenance":false}],"count":2}
        """;

    /// <summary>A fork's pull request in r5 3.1's shape (MEASURED) with g1 G2.6's <c>forkSource</c> (READ), every value made up.</summary>
    private const string ForkPullRequest = """
        {"repository":{"id":"7b0a2222-0000-4a00-8000-000000000001","name":"mobile-app","url":"http://127.0.0.1:2/x","project":{"id":"7b0a3333-0000-4a00-8000-000000000001","name":"Mobile App","state":"unchanged","visibility":"unchanged","lastUpdateTime":"0001-01-01T00:00:00"}},"pullRequestId":13,"codeReviewId":13,"status":"active","createdBy":{"displayName":"Ada Lovelace","url":"https://spsprodfake.vssps.visualstudio.com/x","_links":{"avatar":{"href":"http://127.0.0.1:2/x"}},"id":"7b0a1111-0000-4a00-8000-000000000001","uniqueName":"ada.lovelace@example.com","imageUrl":"http://127.0.0.1:2/y","descriptor":"aad.x"},"creationDate":"2026-01-02T09:00:00.0000000Z","title":"Fix a typo in the greeting","sourceRefName":"refs/pull/13/source","targetRefName":"refs/heads/main","mergeStatus":"succeeded","isDraft":false,"mergeId":"7b0a4444-0000-4a00-8000-000000000001","lastMergeSourceCommit":{"commitId":"1111111111111111111111111111111111111111","url":"http://127.0.0.1:2/z"},"reviewers":[],"forkSource":{"name":"refs/heads/fix-typo","objectId":null,"repository":{"id":"7b0a2222-0000-4a00-8000-000000000003","name":"mobile-app-fork","project":{"id":"7b0a3333-0000-4a00-8000-000000000001","name":"Mobile App"}}},"url":"http://127.0.0.1:2/w","supportsIterations":true}
        """;

    [Fact]
    public void An_anonymous_connection_reads_the_anonymous_identity_and_its_capital_a_account()
    {
        var user = JsonSerializer.Deserialize(AnonymousConnection, AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson)!.AuthenticatedUser!;

        Assert.Equal(AzureDevOpsClient.AnonymousId, user.Id);
        Assert.StartsWith(AzureDevOpsClient.AnonymousDescriptor, user.Descriptor, StringComparison.Ordinal);
        Assert.Equal(("System.String", "Anonymous"), (user.Properties!.Account!.Type, user.Properties.Account.Value));
        Assert.Equal(("Anonymous", "Anonymous"), (user.ProviderDisplayName, user.CustomDisplayName));
    }

    [Fact]
    public void A_members_connection_reads_its_id_names_and_account_and_camel_case_would_miss_account()
    {
        var user = JsonSerializer.Deserialize(MemberConnection, AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson)!.AuthenticatedUser!;

        Assert.Equal("7b0a1111-0000-4a00-8000-000000000001", user.Id);
        Assert.Equal(("Ada Lovelace", null), (user.ProviderDisplayName, user.CustomDisplayName));
        Assert.Equal("ada.lovelace@example.com", user.Properties!.Account!.Value);

        // MEASURED g4 D4 §1: the service writes Account with a capital A; a lower-case one is not it.
        var lower = JsonSerializer.Deserialize("""{"authenticatedUser":{"id":"x","properties":{"account":{"$value":"y"}}}}""", AzureDevOpsJsonContext.Default.AzureDevOpsConnectionJson)!;
        Assert.Null(lower.AuthenticatedUser!.Properties!.Account);
    }

    [Fact]
    public void Errors_read_their_message_and_type_key_beside_dollar_id_and_tf400677s_value()
    {
        // r5 1.4's shape (MEASURED), the words made up.
        var error = JsonSerializer.Deserialize("""{"$id":"1","innerException":null,"message":"TF401019: The Git repository with name or identifier nowhere does not exist.","typeName":"Microsoft.Fake.GitRepositoryNotFoundException, Microsoft.Fake","typeKey":"GitRepositoryNotFoundException","errorCode":0,"eventId":3000}""", AzureDevOpsJsonContext.Default.AzureDevOpsErrorJson)!;
        Assert.Equal(("TF401019: The Git repository with name or identifier nowhere does not exist.", "GitRepositoryNotFoundException", null), (error.Message, error.TypeKey, error.Value));

        // An expired token's body with customProperties (READ g1 G2).
        var expired = JsonSerializer.Deserialize("""{"$id":"1","customProperties":{"Descriptor":null,"IdentityDisplayName":null,"Token":null,"RequestedPermissions":0,"NamespaceId":"00000000-0000-0000-0000-000000000000"},"innerException":null,"message":"Access Denied: The Personal Access Token used has expired.","typeName":"Microsoft.Fake.AccessCheckException, Microsoft.Fake","typeKey":"AccessCheckException","errorCode":0,"eventId":3000}""", AzureDevOpsJsonContext.Default.AzureDevOpsErrorJson)!;
        Assert.Equal("AccessCheckException", expired.TypeKey);

        // TF400677's shape (MEASURED g1 G4.2): value.Message, a capital M.
        var host = JsonSerializer.Deserialize("""{"count":1,"value":{"Message":"TF400677: Host nowhere does not exist."}}""", AzureDevOpsJsonContext.Default.AzureDevOpsErrorJson)!;
        Assert.Equal((null, "TF400677: Host nowhere does not exist."), (host.Message, host.Value!.Message));
    }

    [Fact]
    public void A_repository_without_a_default_branch_is_read_and_one_without_an_id_is_not()
    {
        var list = JsonSerializer.Deserialize(Repositories, AzureDevOpsJsonContext.Default.RepositoryList)!;

        Assert.Equal(2, list.Count);
        Assert.Equal(("mobile-app", "Mobile App", "refs/heads/main", "private"), (list.Value[0].Name, list.Value[0].Project.Name, list.Value[0].DefaultBranch, list.Value[0].Project.Visibility));
        Assert.Equal(("sandbox", null, false, false), (list.Value[1].Name, list.Value[1].DefaultBranch, list.Value[1].IsFork, list.Value[1].IsDisabled));
        Assert.Equal(DateTimeOffset.MinValue, AzureDevOpsDates.DateOf(list.Value[0].Project.LastUpdateTime));

        // required only where every answer has it (g4 D4 §4): a list's value, a repository's id, name and project.name.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"value":[{"name":"x","project":{"name":"p"}}]}""", AzureDevOpsJsonContext.Default.RepositoryList));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"value":[{"id":"1","name":"x","project":{}}]}""", AzureDevOpsJsonContext.Default.RepositoryList));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"count":0}""", AzureDevOpsJsonContext.Default.RepositoryList));
    }

    [Fact]
    public void A_forks_pull_request_reads_its_fork_source_and_never_a_unique_name()
    {
        var pull = JsonSerializer.Deserialize(ForkPullRequest, AzureDevOpsJsonContext.Default.AzureDevOpsPullRequestJson)!;

        Assert.Equal((13, "Fix a typo in the greeting", "refs/pull/13/source", "refs/heads/main", false), (pull.PullRequestId, pull.Title, pull.SourceRefName, pull.TargetRefName, pull.IsDraft));
        Assert.Equal(("refs/heads/fix-typo", "mobile-app-fork", "Mobile App"), (pull.ForkSource!.Name, pull.ForkSource.Repository!.Name, pull.ForkSource.Repository.Project!.Name));
        Assert.Equal(("Ada Lovelace", "1111111111111111111111111111111111111111"), (pull.CreatedBy!.DisplayName, pull.LastMergeSourceCommit!.CommitId));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero), AzureDevOpsDates.DateOf(pull.CreationDate));
        Assert.DoesNotContain(typeof(AzureDevOpsIdentityRefJson).GetProperties(), property => property.Name.Contains("Unique", StringComparison.Ordinal));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"pullRequestId":1,"sourceRefName":"refs/heads/a","targetRefName":"refs/heads/main"}""", AzureDevOpsJsonContext.Default.AzureDevOpsPullRequestJson));
    }

    [Theory]
    [InlineData("2026-01-02T09:00:00.0000000Z", "2026-01-02T09:00:00.0000000+00:00")]
    [InlineData("2026-01-02T09:00:00", "2026-01-02T09:00:00.0000000+00:00")]
    [InlineData("2026-01-02T11:00:00+02:00", "2026-01-02T09:00:00.0000000+00:00")]
    [InlineData("0001-01-01T00:00:00", "0001-01-01T00:00:00.0000000+00:00")]
    [InlineData("not a date", "0001-01-01T00:00:00.0000000+00:00")]
    [InlineData(null, "0001-01-01T00:00:00.0000000+00:00")]
    public void A_date_without_an_offset_is_utc_and_one_that_cannot_be_read_is_the_minimum(string? text, string expected)
    {
        var date = AzureDevOpsDates.DateOf(text);

        Assert.Equal(expected, date.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(TimeSpan.Zero, date.Offset);
    }

    [Fact]
    public void No_shape_holds_a_date_time_offset()
    {
        var shapes = typeof(AzureDevOpsJsonContext).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(AzureDevOpsJsonContext).Namespace && type.Name.StartsWith("AzureDevOps", StringComparison.Ordinal) && type.Name.Contains("Json", StringComparison.Ordinal) && type != typeof(AzureDevOpsJsonContext))
            .ToList();

        Assert.Contains(typeof(AzureDevOpsPullRequestJson), shapes);
        Assert.Contains(typeof(AzureDevOpsListJson<>), shapes);
        Assert.All(shapes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)), property =>
            Assert.True(property.PropertyType != typeof(DateTimeOffset) && property.PropertyType != typeof(DateTimeOffset?) && property.PropertyType != typeof(DateTime), property.DeclaringType!.Name + "." + property.Name));
    }
}
