using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.Core.Logging;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AboutTexts;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>One text of the text dialog: its switch button's label, the text and the button's automation id.</summary>
public sealed partial class TextDocumentPart : ObservableObject
{
    /// <param name="label">The switch button's label.</param>
    /// <param name="text">The text, with <c>\n</c> line ends.</param>
    /// <param name="automationId">The switch button's automation id.</param>
    public TextDocumentPart(string label, string text, string automationId)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(automationId);
        Label = label;
        Text = text;
        AutomationId = automationId;
    }

    public string Label { get; }

    public string Text { get; }

    public string AutomationId { get; }

    /// <summary>The dialog shows this text now: its switch button has the selected look.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; }
}

/// <summary>
/// The read-only text dialog (D432) that About opens for the license and the third-party notices,
/// stacked over the settings screen (D104): a title, a switch between its texts when it has more
/// than one, the shown text in a virtualised, wrapped, read-only view, "Copy all" and "Close". It
/// writes nothing to disk and starts no other program; "Copy all" puts the shown text whole on the
/// clipboard and leaves the dialog open. Close answers true, Esc false.
/// </summary>
public sealed partial class TextDocumentDialogViewModel : DialogViewModel<bool>
{
    private readonly IClipboardService? _clipboard;
    private readonly IAppLog _log;

    /// <param name="title">The card's title.</param>
    /// <param name="parts">The texts, the first shown at opening; at least one.</param>
    /// <param name="clipboard">Where "Copy all" puts the text; null where nothing may be copied (view-model tests).</param>
    /// <param name="log">Where a failed copy is logged.</param>
    public TextDocumentDialogViewModel(string title, IReadOnlyList<TextDocumentPart> parts, IClipboardService? clipboard, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(log);
        if (parts.Count == 0)
        {
            throw new ArgumentException("The dialog shows at least one text.", nameof(parts));
        }

        Title = title;
        Parts = parts;
        _clipboard = clipboard;
        _log = log;
        foreach (var part in parts)
        {
            part.IsShown = false;
        }

        parts[0].IsShown = true;
        Shown = parts[0];
    }

    public override string Title { get; }

    /// <summary>The texts, in the switch's order.</summary>
    public IReadOnlyList<TextDocumentPart> Parts { get; }

    /// <summary>The text shown now.</summary>
    [ObservableProperty]
    public partial TextDocumentPart Shown { get; private set; }

    /// <summary>The switch shows only when there is more than one text.</summary>
    public bool ShowsSwitch => Parts.Count > 1;

    protected override bool CancelledResult => false;

    /// <summary>A switch button: shows its text (the view starts it from its top).</summary>
    [RelayCommand]
    private void Show(TextDocumentPart? part)
    {
        if (part is null || ReferenceEquals(part, Shown) || !Parts.Contains(part))
        {
            return;
        }

        Shown.IsShown = false;
        part.IsShown = true;
        Shown = part;
    }

    /// <summary>
    /// "Copy all": the shown text whole on the clipboard; the dialog stays open. The button stays
    /// enabled while another program holds the clipboard: disabled, it would lose the keyboard focus.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task CopyAll()
    {
        if (_clipboard is null)
        {
            return;
        }

        try
        {
            await _clipboard.SetTextAsync(Shown.Text);
        }
        catch (Exception ex)
        {
            _log.Warning(Texts.CopyFailed, ex);
        }
    }

    /// <summary>"Close".</summary>
    [RelayCommand]
    private void Confirm() => Complete(true);
}
