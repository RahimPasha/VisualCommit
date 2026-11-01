using VisualCommit.Hosting.AzureDevOps;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// A local Azure DevOps for the tests (docs/phase-7b-design.md 1.9, 2.3; D346, D363): its API, pages and git
/// on one host, <c>http://127.0.0.1:&lt;port&gt;/</c>, on the fakes' shared core
/// (<see cref="FakeHostingService"/>) and <see cref="AuthRemoteServer"/>'s host mode
/// (<see cref="StartAsync"/>), or the same routes in process (<see cref="InProcess"/>,
/// <see cref="FakeHostingService.CreateHandler"/>).
/// <para>
/// WP-F's skeleton: the start, the endpoints and the realm; no route yet, so every request in
/// process is answered 404 and the server claims nothing. WP-FA builds the rest (docs/phase-7b-design.md 3; its
/// members of 1.9) on its branch `wip/p7b-fake-azure`, in this file and the partial files beside it.
/// </para>
/// </summary>
public sealed partial class FakeAzureDevOps : FakeHostingService
{
    /// <summary>The port of <see cref="InProcess"/>'s endpoints, <c>Local(2)</c>: never listened on.</summary>
    public const int InProcessPort = 2;

    private FakeAzureDevOps(string root)
        : base(root, "Azure DevOps", InProcessPort)
    {
    }

    /// <summary>The fake's addresses: <c>AzureDevOpsEndpoints.Local(Port)</c>; <c>Local(2)</c> in process.</summary>
    public AzureDevOpsEndpoints Endpoints => AzureDevOpsEndpoints.Local(Port);

    /// <inheritdoc/>
    protected override string Realm => "Azure DevOps";

    /// <summary>
    /// Starts the fake on 127.0.0.1 in <see cref="AuthRemoteServer"/>'s host mode on
    /// <paramref name="root"/> (made when missing): on <paramref name="port"/>, or one the system
    /// picks when it is 0; with <paramref name="fallBackToFreePort"/> a taken port gives way to a
    /// free one (<see cref="FakeHostingService.PortWasTaken"/>). Nothing under <paramref name="root"/> is deleted.
    /// </summary>
    public static async Task<FakeAzureDevOps> StartAsync(string root, int port = 0, bool fallBackToFreePort = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ct.ThrowIfCancellationRequested();
        var fake = new FakeAzureDevOps(root);
        try
        {
            await fake.StartServerAsync(port, fallBackToFreePort).ConfigureAwait(false);
            return fake;
        }
        catch
        {
            await fake.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// The fake without a server: its routes reached only through <see cref="FakeHostingService.CreateHandler"/>,
    /// by path from any host; git's paths get 404. <see cref="Endpoints"/> is <c>Local(2)</c>.
    /// </summary>
    public static FakeAzureDevOps InProcess(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new FakeAzureDevOps(root);
    }

    /// <summary>No path yet (WP-FA's routes).</summary>
    protected override bool Claims(string method, string path) => false;

    /// <summary>No route yet (WP-FA's).</summary>
    protected override string? RouteOf(string method, string path) => null;

    /// <summary>Not reached until a path is claimed (WP-FA's routes).</summary>
    protected override Task<AuthRemoteResponse> AnswerAsync(FakeServiceCall call, CancellationToken ct) =>
        Task.FromResult(new AuthRemoteResponse(404, "Not Found", [KeyValuePair.Create("Content-Type", "text/plain; charset=utf-8")], "Not found.\n"u8.ToArray()));

    /// <summary>No repository is public yet (WP-FA's).</summary>
    protected override bool IsPublic(string repository) => false;

    /// <summary>Nobody may push yet (WP-FA's).</summary>
    protected override bool MayPush(string repository, string login) => false;
}
