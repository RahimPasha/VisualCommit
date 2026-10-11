namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>One running operation in the "Finishing operations" dialog (docs/phase-3-design.md 1.8, 4f).</summary>
/// <param name="Text">"Push main — graph": the operation's title and its repository.</param>
/// <param name="RemainingStepsWillNotRun">A local operation of several steps: "Its remaining steps will not run if you quit now".</param>
public sealed record FinishingItem(string Text, bool RemainingStepsWillNotRun);

/// <summary>
/// "Finishing operations" (docs/phase-3-design.md 1.8, 4f): shown when the window is closed while
/// operations run; the window closes by itself when they end. Its answer is true for "Quit now"
/// and false for "Keep open" (Cancel, Esc). Work package WP0 made this skeleton for the call site
/// in <see cref="MainWindowViewModel"/>; WP6 owns this file and its view.
/// </summary>
public sealed partial class FinishingDialogViewModel : DialogViewModel<bool>
{
    public FinishingDialogViewModel(IReadOnlyList<FinishingItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
    }

    public override string Title => "Finishing operations";

    /// <summary>One line per running operation.</summary>
    public IReadOnlyList<FinishingItem> Items { get; }

    protected override bool CancelledResult => false;

    /// <summary>"Quit now": the window closes; network operations are stopped.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void QuitNow() => Complete(true);

    /// <summary>The operations ended: the dialog closes without an answer from the user.</summary>
    public void CloseAsFinished() => Complete(false);
}
