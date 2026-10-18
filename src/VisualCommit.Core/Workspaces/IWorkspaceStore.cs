namespace VisualCommit.Core.Workspaces;

/// <summary>Why a workspace store saves nothing this session (docs/phase-6-design.md 1.3).</summary>
public enum WorkspaceReadOnlyCause
{
    /// <summary><c>workspaces.json</c> was made by a newer version: it is read, never saved, and every edit is disabled.</summary>
    NewerVersion,

    /// <summary><c>workspaces.json</c> could be neither read nor set aside: nothing writes over it, and the next start reads it again.</summary>
    Unreadable,
}

/// <summary>
/// Holds the user's workspaces and saves every change (D189, docs/phase-6-design.md 1.3), shaped
/// like <see cref="Settings.ISettingsStore"/>. The app's store keeps them in
/// <c>workspaces.json</c> in the data folder; view-model tests use one in memory
/// (<see cref="InMemoryWorkspaceStore"/>).
/// </summary>
public interface IWorkspaceStore
{
    /// <summary>The workspaces as last loaded or changed.</summary>
    WorkspaceList Current { get; }

    /// <summary>Nothing is saved this session: see <see cref="ReadOnlyCause"/>. <see cref="Update"/> still changes <see cref="Current"/>.</summary>
    bool IsReadOnly { get; }

    /// <summary>Why the store is read-only, or null while it is not.</summary>
    WorkspaceReadOnlyCause? ReadOnlyCause { get; }

    /// <summary>
    /// What the welcome page says about the file (a file set aside, one that could not be read, a
    /// newer version's, a failed save), and while <see cref="SaveFailed"/> the dashboard too
    /// (2.5); null when there is nothing to say.
    /// </summary>
    string? Notice { get; }

    /// <summary>True from a failed save until a save succeeds; each save writes the whole list, so one that succeeds keeps every earlier change.</summary>
    bool SaveFailed { get; }

    /// <summary>
    /// Applies <paramref name="change"/> to <see cref="Current"/> and saves the result. A change
    /// that gives the same object changes and saves nothing. A failed save is logged and never
    /// thrown; the change still applies for the session. While read-only nothing is written.
    /// </summary>
    void Update(Func<WorkspaceList, WorkspaceList> change);

    /// <summary>
    /// Raised after <see cref="Update"/> changed <see cref="Current"/>, on the caller's thread,
    /// with <see cref="Notice"/> and <see cref="SaveFailed"/> already set.
    /// </summary>
    event EventHandler? Changed;
}

/// <summary>
/// Workspaces kept in memory only, never saved: <c>TabServices</c>' store when it is given none
/// (view-model tests), and the session's until the file store (work package WP4,
/// docs/phase-6-design.md 1.3) takes its place. Never read-only, never a notice, never a failed save.
/// </summary>
public sealed class InMemoryWorkspaceStore(WorkspaceList? initial = null) : IWorkspaceStore
{
    private readonly Lock _gate = new();

    public WorkspaceList Current { get; private set; } = initial ?? new WorkspaceList();

    public bool IsReadOnly => false;

    public WorkspaceReadOnlyCause? ReadOnlyCause => null;

    public string? Notice => null;

    public bool SaveFailed => false;

    public event EventHandler? Changed;

    public void Update(Func<WorkspaceList, WorkspaceList> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            var next = change(Current);
            if (ReferenceEquals(next, Current))
            {
                return;
            }

            Current = next;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
