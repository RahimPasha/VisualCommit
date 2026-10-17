namespace VisualCommit.App.Views;

/// <summary>
/// One link of the main window's Esc chain (docs/phase-3-design.md 4a, section 8): Esc goes to
/// the handlers in their order until one takes it. The menu layer, the drag service, the dialog
/// layer and the diff each register one with <see cref="MainWindow.AddEscapeHandler"/>, so that no
/// package edits the window's code for its Esc.
/// </summary>
public interface IEscapeHandler
{
    /// <summary>Handles Esc if there is something to close or cancel; returns whether it did.</summary>
    bool HandleEscape();
}

/// <summary>Where each part's handler stands in the Esc chain: the lowest number is asked first.</summary>
public static class EscapeOrder
{
    /// <summary>An open context menu closes first (4a).</summary>
    public const int Menu = 100;

    /// <summary>A branch drag in progress is cancelled (4g).</summary>
    public const int Drag = 200;

    /// <summary>The top dialog is cancelled (D69, D104).</summary>
    public const int Dialog = 300;

    /// <summary>
    /// The conflict resolver (docs/phase-4-design.md 11.10): it takes Esc and does nothing while
    /// the focus is in its result, so a slip never loses work; otherwise it closes, asking first
    /// when the result is modified.
    /// </summary>
    public const int Resolver = 350;

    /// <summary>
    /// The activity log (docs/phase-5-design.md 2.1): it closes, but only while the focus is inside
    /// it, before the resolver, the list and a page, which would otherwise take Esc wherever the focus is.
    /// </summary>
    public const int ActivityLog = 340;

    /// <summary>The interactive rebase's list (2.2): a row drag in progress is cancelled first; otherwise the list closes, asking first when it was changed.</summary>
    public const int InteractiveRebase = 360;

    /// <summary>A page of the graph area (docs/phase-5-design.md 11.1): it closes, unless a diff is open over it or a text box has the focus.</summary>
    public const int Page = 370;

    /// <summary>The search bar (docs/phase-5-design.md 3.1): it closes while the focus is in it, before the diff.</summary>
    public const int Search = 390;

    /// <summary>The open diff closes, unless a text box has the focus (D63).</summary>
    public const int Diff = 400;
}

/// <summary>An <see cref="IEscapeHandler"/> made of a function.</summary>
public sealed class EscapeHandler(Func<bool> handle) : IEscapeHandler
{
    public bool HandleEscape() => handle();
}
