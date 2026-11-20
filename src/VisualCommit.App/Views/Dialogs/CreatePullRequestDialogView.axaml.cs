using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;

namespace VisualCommit.App.Views.Dialogs;

/// <summary>
/// The "Create pull request" form (<c>CreatePullRequestDialog</c>, docs/phase-7a-design.md 12.4) of
/// a <see cref="ViewModels.Dialogs.CreatePullRequestDialogViewModel"/>: the title takes the focus,
/// with the caret at its end; Enter there creates and Mod+Enter in the description does
/// (DialogView's rules). The base's "Choose…" gets the focus back, with no frame, once the pick it
/// opened has ended, unless the focus is somewhere in the form by then.
/// </summary>
public partial class CreatePullRequestDialogView : DialogView
{
    public CreatePullRequestDialogView()
    {
        InitializeComponent();
        PullRequestBaseChooseButton.Click += (_, _) =>
        {
            if (DataContext is CreatePullRequestDialogViewModel viewModel)
            {
                FocusReturn.AfterRun(viewModel.ChooseBaseCommand, this, () => PullRequestBaseChooseButton, NavigationMethod.Unspecified);
            }
        };
    }

    protected override TextBox? InitialTextBox => PullRequestTitle;
}

/// <summary>
/// A row's repository text (docs/phase-7b-design.md 13.4, ruling 3): a <see cref="TextBlock"/>, styled
/// as one, that measures its own text while it fits and, once cut with "…", the whole room its
/// row leaves it, so a row sized to its content places what follows it where it did and, cut,
/// ends at its column's right (a cut text alone is narrower than its room by part of a character).
/// </summary>
public sealed class RowRepositoryText : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return double.IsFinite(availableSize.Width) && TextLayout.TextLines.Any(line => line.HasCollapsed)
            ? size.WithWidth(availableSize.Width)
            : size;
    }
}
