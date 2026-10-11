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

    /// <summary>The open diff closes, unless a text box has the focus (D63).</summary>
    public const int Diff = 400;
}

/// <summary>An <see cref="IEscapeHandler"/> made of a function.</summary>
public sealed class EscapeHandler(Func<bool> handle) : IEscapeHandler
{
    public bool HandleEscape() => handle();
}
