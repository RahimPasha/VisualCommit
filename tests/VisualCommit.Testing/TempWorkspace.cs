using VisualCommit.Core.Git;
using VisualCommit.Core.Workspaces;
using VisualCommit.Git;

namespace VisualCommit.Testing;

/// <summary>
/// The ten repositories of phase 6's workspace "Web" (docs/phase-6-design.md 17.1): a temporary
/// folder with a working tree per repository and, for the eight with a remote,
/// <c>remotes/&lt;name&gt;.git</c>, reached by the relative URL <c>../remotes/&lt;name&gt;.git</c>, so
/// that a copy fetches from its own remotes with nothing rewritten. <c>old-site</c> is listed and
/// never made. Built once per test process by <see cref="Scenarios.WorkspaceAsync"/> (the lean
/// way, <see cref="LeanRepositories"/>, 28 git processes) and copied for each test with no git
/// process at all (<see cref="CopyAsync"/>). Disposing it deletes the folder.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    /// <summary>The repositories in the dashboard's order (1.1), <c>old-site</c> last.</summary>
    public static IReadOnlyList<string> DashboardOrder { get; } =
        ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes", "old-site"];

    /// <summary>The repositories with a remote of their own, <c>remotes/&lt;name&gt;.git</c>.</summary>
    public static IReadOnlyList<string> WithRemote { get; } =
        ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch"];

    private readonly TempDirectory _root;
    private readonly IReadOnlyDictionary<string, DateTimeOffset> _clocks;

    internal TempWorkspace(TempDirectory root, IReadOnlyDictionary<string, DateTimeOffset> clocks, IGitCallLog calls)
    {
        _root = root;
        _clocks = clocks;
        Calls = calls;
    }

    /// <summary>The folder that holds the working trees and <c>remotes/</c>, as the system's temp folder spells it.</summary>
    public string Root => _root.Path;

    /// <summary>The repositories in the dashboard's order (<see cref="DashboardOrder"/>).</summary>
    public IReadOnlyList<string> Names => DashboardOrder;

    /// <summary>
    /// The git calls that built this workspace: the template's 28 (17.1); none for a copy, which
    /// is made without git (<see cref="CopyAsync"/>).
    /// </summary>
    public IGitCallLog Calls { get; }

    /// <summary>The working tree of <paramref name="name"/>: <c>&lt;root&gt;/&lt;name&gt;</c>. For <c>old-site</c> a folder that does not exist.</summary>
    public string PathOf(string name) => _root.Combine(Known(name));

    /// <summary>The bare remote of <paramref name="name"/>, <c>&lt;root&gt;/remotes/&lt;name&gt;.git</c>; throws for <c>notes</c> and <c>old-site</c>, which have none.</summary>
    public string RemoteOf(string name) =>
        WithRemote.Contains(Known(name))
            ? _root.Combine(LeanRepositories.RemotesFolder, name + ".git")
            : throw new ArgumentException($"{name} has no remote.", nameof(name));

    /// <summary>
    /// A <see cref="TempRepo"/> on <paramref name="name"/>'s working tree for a test's own git
    /// calls and files, with <see cref="TempRepo"/>'s isolation and author and a clock a minute after
    /// the repository's last commit. It does not own the folder: the workspace does.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"><paramref name="name"/> is <c>old-site</c>, which is never made.</exception>
    public TempRepo Repo(string name)
    {
        var path = PathOf(name);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"{name} is not in the workspace's folder: {path}.");
        }

        return TempRepo.Over(_root, path, _clocks[name]);
    }

    /// <summary>
    /// An independent copy of the whole folder in a new temporary folder, made by copying files
    /// (<see cref="TempRepo.CopyDirectory"/>) and nothing else: no git process, and so nothing in
    /// <see cref="Calls"/> of either. The relative URLs point the copy's repositories at the copy's
    /// remotes.
    /// </summary>
    public Task<TempWorkspace> CopyAsync()
    {
        var root = new TempDirectory("workspace");
        try
        {
            TempRepo.CopyDirectory(_root.Path, root.Path);
            return Task.FromResult(new TempWorkspace(root, _clocks, new GitCallLog()));
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The workspace "Web" over this folder (docs/phase-6-design.md 1.1), its ten repositories in
    /// the dashboard's order, for <c>ShellDriver.WriteWorkspaces</c> (17.2).
    /// </summary>
    public Workspace ToWorkspace(string id = "web", string name = "Web") =>
        new(id, name, [.. Names.Select(repository => new WorkspaceRepository(PathOf(repository)))]);

    public void Dispose() => _root.Dispose();

    private static string Known(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return DashboardOrder.Contains(name, StringComparer.Ordinal)
            ? name
            : throw new ArgumentException($"The workspace has no repository {name}.", nameof(name));
    }
}
