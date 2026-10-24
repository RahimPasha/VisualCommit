using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// The error dialog (<c>ErrorDialog</c>, docs/phase-3-design.md 1.6, 4f, C11, D95): the
/// failure's summary (<c>ErrorSummary</c>), the repository (<c>ErrorRepository</c>, "In
/// &lt;repo&gt;", always but for a write of git's global settings, which belongs to no repository,
/// docs/phase-6-design.md 8.3), git's own output in the code font (<c>ErrorOutput</c>), the command
/// that failed (<c>ErrorCommand</c>), and the buttons "Copy output" (<c>ErrorCopyButton</c>), an
/// optional extra action (<c>ErrorExtraButton</c>: "Stash changes and check out", "Pull",
/// "Fetch origin", "Remove lock files") and "Close" (<c>ConfirmButton</c>). A dialog without git
/// output ("Changes re-applied with conflicts", 2.31) leaves out the output, the command and
/// "Copy output" (D114). Answers true when the extra button was clicked (the dialog closes and the
/// caller runs the action), false for Close and Esc.
/// </summary>
public sealed partial class ErrorDialogViewModel : DialogViewModel<bool>
{
    private readonly IClipboardService? _clipboard;
    private readonly IAppLog _log;

    /// <param name="title">"Checkout blocked", "Push rejected", "Fetch origin cancelled", … (2.33).</param>
    /// <param name="summary">The one-line sentence that says what happened.</param>
    /// <param name="repositoryName">The repository's name, shown as "In &lt;name&gt;"; null hides the line (a global settings write, 8.3).</param>
    /// <param name="output">Git's cleaned output, or null for a dialog without it.</param>
    /// <param name="command">The failing git call's display text, or null.</param>
    /// <param name="extraAction">The extra button's label, or null for none.</param>
    /// <param name="clipboard">Where "Copy output" puts the output; null where nothing may be copied (view-model tests).</param>
    /// <param name="log">Where a failed copy is logged.</param>
    public ErrorDialogViewModel(
        string title,
        string summary,
        string? repositoryName,
        string? output = null,
        string? command = null,
        string? extraAction = null,
        IClipboardService? clipboard = null,
        IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(summary);
        Title = title;
        Summary = summary;
        RepositoryName = repositoryName;
        Output = string.IsNullOrEmpty(output) ? null : output;
        Command = Output is null || string.IsNullOrEmpty(command) ? null : command;
        ExtraAction = extraAction;
        _clipboard = clipboard;
        _log = log ?? NullAppLog.Instance;
    }

    /// <summary>The dialog of an operation's failure: its summary, git's output and the command (1.6).</summary>
    public static ErrorDialogViewModel ForFailure(
        string title,
        GitFailure failure,
        string? repositoryName,
        string? extraAction = null,
        IClipboardService? clipboard = null,
        IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ErrorDialogViewModel(title, failure.Summary, repositoryName, failure.GitOutput, failure.CommandText, extraAction, clipboard, log);
    }

    public override string Title { get; }

    /// <summary>What happened, in one sentence (wrapped).</summary>
    public string Summary { get; }

    /// <summary>Git's output, or null for a dialog without it.</summary>
    public string? Output { get; }

    public bool HasOutput => Output is not null;

    /// <summary>The git call that failed, or null.</summary>
    public string? Command { get; }

    public bool HasCommand => Command is not null;

    /// <summary>The extra button's label, or null for none.</summary>
    public string? ExtraAction { get; }

    public bool HasExtraAction => ExtraAction is not null;

    protected override bool CancelledResult => false;

    /// <summary>"Copy output": puts exactly <see cref="Output"/> on the clipboard. The dialog stays open.</summary>
    [RelayCommand]
    private async Task CopyOutput()
    {
        if (Output is null || _clipboard is null)
        {
            return;
        }

        try
        {
            await _clipboard.SetTextAsync(Output);
        }
        catch (Exception ex)
        {
            _log.Warning("Copying git's output to the clipboard failed.", ex);
        }
    }

    /// <summary>The extra button: closes the dialog with true.</summary>
    [RelayCommand]
    private void Extra() => Complete(true);

    /// <summary>"Close".</summary>
    [RelayCommand]
    private void Confirm() => Complete(false);
}
