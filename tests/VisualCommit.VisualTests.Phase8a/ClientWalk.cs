using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// What a UI Automation client reaches of a window, as far as the headless run can tell it
/// (docs/phase-8a-design.md 9.3.7, 9.3.9): the peers a walk down from the window's own peer is given,
/// each peer's children in turn, as a screen reader is given them when it reads the window. Windows'
/// bridge makes the node that carries a peer's events only for a peer a client has been given, so a
/// live region speaks through a peer such a walk returned before its text changed, and through no
/// other. A control that is not shown is not among its parent's children: no walk reaches it, and a
/// peer made by hand for it stands for no client. A test records name changes only on peers a walk
/// returned.
/// </summary>
internal static class ClientWalk
{
    /// <summary>Every peer a client reaches now, walking down from <paramref name="window"/>'s peer, in the walk's order.</summary>
    public static IReadOnlyList<AutomationPeer> Reach(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return Reach(ControlAutomationPeer.CreatePeerForElement(window));
    }

    /// <summary>Every peer a client reaches now below <paramref name="root"/>, itself first, in the walk's order.</summary>
    public static IReadOnlyList<AutomationPeer> Reach(AutomationPeer root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var reached = new List<AutomationPeer>();
        Walk(root);
        return reached;

        void Walk(AutomationPeer peer)
        {
            reached.Add(peer);
            foreach (var child in peer.GetChildren())
            {
                Walk(child);
            }
        }
    }

    /// <summary>The peer with automation id <paramref name="automationId"/> among <paramref name="reached"/>, or null when the walk did not reach it.</summary>
    public static AutomationPeer? Find(IReadOnlyList<AutomationPeer> reached, string automationId)
    {
        ArgumentNullException.ThrowIfNull(reached);
        return reached.FirstOrDefault(peer => peer.GetAutomationId() == automationId);
    }

    /// <summary>
    /// The live regions among <paramref name="reached"/> (a live setting other than Off), in the
    /// walk's order: each by its automation id, or, without one, by its class name and its name.
    /// </summary>
    public static IReadOnlyList<string> LiveRegions(IReadOnlyList<AutomationPeer> reached)
    {
        ArgumentNullException.ThrowIfNull(reached);
        return reached
            .Where(peer => peer.GetLiveSetting() != AutomationLiveSetting.Off)
            .Select(peer => peer.GetAutomationId() ?? $"{peer.GetClassName()} \"{peer.GetName()}\"")
            .ToList();
    }

    /// <summary>
    /// Records what a client is told at each of <paramref name="window"/>'s focus events: the focused
    /// element's automation id, name and help text, read from its peer at the event. Windows' bridge
    /// makes the focused peer's node at that event, whether or not a client had reached it before
    /// (Avalonia 12.1.3, <c>RootAutomationNode.OnRootFocusChanged</c>), and a screen reader reads the
    /// node's name, type and help text then; macOS reads the same help text as the element's help.
    /// Dispose it to stop.
    /// </summary>
    public static FocusRecorder RecordFocus(TopLevel window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return ControlAutomationPeer.CreatePeerForElement(window) is WindowBaseAutomationPeer root
            ? new FocusRecorder(root)
            : throw new InvalidOperationException("The window's peer is no window's: it raises no focus events.");
    }
}

/// <summary>What a client is told at one focus event (<see cref="ClientWalk.RecordFocus"/>); all null when the focus left the window's elements.</summary>
internal sealed record FocusEvent(string? AutomationId, string? Name, string? HelpText);

/// <summary>A window's focus events, in order, as a client receives them (<see cref="ClientWalk.RecordFocus"/>).</summary>
internal sealed class FocusRecorder : IDisposable
{
    private readonly WindowBaseAutomationPeer _root;
    private readonly List<FocusEvent> _events = [];

    internal FocusRecorder(WindowBaseAutomationPeer root)
    {
        _root = root;
        root.FocusChanged += OnFocusChanged;
    }

    /// <summary>Every focus event so far, the oldest first.</summary>
    public IReadOnlyList<FocusEvent> Events => _events;

    /// <summary>The last event; throws when none has come.</summary>
    public FocusEvent Last => _events.Count > 0 ? _events[^1] : throw new InvalidOperationException("No focus event has come.");

    public void Dispose() => _root.FocusChanged -= OnFocusChanged;

    private void OnFocusChanged(object? sender, EventArgs e)
    {
        var focus = _root.GetFocus();
        _events.Add(new FocusEvent(focus?.GetAutomationId(), focus?.GetName(), focus?.GetHelpText()));
    }
}
