namespace VisualCommit.Hosting.AzureDevOps;

/// <summary>
/// What every request to Azure DevOps' REST API names (docs/phase-7b-design.md 1.4, 6.3), as 7a's
/// <c>GitHubApi</c>: the live command's probes send the same.
/// </summary>
public static class AzureDevOpsApi
{
    /// <summary>The <c>api-version</c> of every git route (MEASURED r5 0.1).</summary>
    public const string Version = "7.1";

    /// <summary>
    /// The <c>api-version</c> of <c>connectionData</c> and the resource area: a preview one, since
    /// <c>7.1</c> is a 400 there (MEASURED r5 0.1).
    /// </summary>
    public const string ConnectionDataVersion = "7.1-preview.1";

    /// <summary>The core resource area's id, a published constant (MEASURED g1 G4.2): its <c>locationUrl</c> names the organization.</summary>
    public const string CoreAreaId = "79134c72-4a58-4b42-976c-04e7115f32bf";
}
