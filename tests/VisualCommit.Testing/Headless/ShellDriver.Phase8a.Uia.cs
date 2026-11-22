using Avalonia.Automation;
using Avalonia.Automation.Peers;
using VisualCommit.App.Controls;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// What stands for a screen reader in the headless run (docs/phase-8a-design.md 1.15, 9.5; the
/// report's part U): the window's automation peer, made once as a client's first read makes it,
/// the graph's peer and its one exposed row, and a recorder of the row's name changes, which the
/// Windows bridge would turn into live region changes. Without these nothing makes a peer, and the
/// app runs as it does with no client.
/// </summary>
public sealed partial class ShellDriver
{
    private AutomationPeer? _windowPeer;

    /// <summary>The window's automation peer, made on the first call and kept, as a client's first read of the window makes it.</summary>
    public AutomationPeer WindowPeer() => _windowPeer ??= ControlAutomationPeer.CreatePeerForElement(Window);

    /// <summary>The active tab's commit graph (<c>GraphRows</c>).</summary>
    public CommitGraphControl GraphControl() => Find<CommitGraphControl>("GraphRows");

    /// <summary>The graph's peer (<c>GraphRows</c>, a list), the window's made first: its peer is the same one a client reaches through the window.</summary>
    public AutomationPeer GraphPeer()
    {
        WindowPeer();
        return ControlAutomationPeer.CreatePeerForElement(GraphControl());
    }

    /// <summary>
    /// The graph's one exposed row (<c>GraphSelectedRow</c>), its peers made as a client's first
    /// read makes them.
    /// </summary>
    /// <exception cref="InvalidOperationException">The graph exposes no row, or more than one.</exception>
    public AutomationPeer GraphRowPeer()
    {
        var children = GraphPeer().GetChildren();
        return children.Count == 1
            ? children[0]
            : throw new InvalidOperationException($"The graph exposes {children.Count} rows to UI Automation; one is expected while a row is selected.");
    }

    /// <summary>
    /// Records each change of <paramref name="peer"/>'s name, its old and new value in order, as a
    /// client's handler of the name's changes receives them. Dispose it to stop.
    /// </summary>
    public static NameChangeRecorder RecordNameChanges(AutomationPeer peer) => new(peer);
}

/// <summary>A peer's name changes, in order (<see cref="ShellDriver.RecordNameChanges"/>).</summary>
public sealed class NameChangeRecorder : IDisposable
{
    private readonly AutomationPeer _peer;
    private readonly List<(string? Old, string? New)> _changes = [];

    internal NameChangeRecorder(AutomationPeer peer)
    {
        ArgumentNullException.ThrowIfNull(peer);
        _peer = peer;
        peer.PropertyChanged += OnPropertyChanged;
    }

    /// <summary>Every change so far, the oldest first.</summary>
    public IReadOnlyList<(string? Old, string? New)> Changes => _changes;

    /// <summary>How many changes have come.</summary>
    public int Count => _changes.Count;

    /// <summary>The last change; throws when none has come.</summary>
    public (string? Old, string? New) Last => _changes.Count > 0 ? _changes[^1] : throw new InvalidOperationException("No name change has come.");

    public void Dispose() => _peer.PropertyChanged -= OnPropertyChanged;

    private void OnPropertyChanged(object? sender, AutomationPropertyChangedEventArgs change)
    {
        if (change.Property == AutomationElementIdentifiers.NameProperty)
        {
            _changes.Add((change.OldValue as string, change.NewValue as string));
        }
    }
}
