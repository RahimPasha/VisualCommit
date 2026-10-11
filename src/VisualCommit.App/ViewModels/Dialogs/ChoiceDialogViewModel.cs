using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>One radio button of a choice (<c>ChoiceOption</c>, named by its text): checking one unchecks the others.</summary>
public sealed partial class ChoiceOption : ObservableObject
{
    private readonly Action<ChoiceOption> _checked;

    internal ChoiceOption(string text, Action<ChoiceOption> onChecked)
    {
        Text = text;
        _checked = onChecked;
    }

    /// <summary>What it says: a remote's name.</summary>
    public string Text { get; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    partial void OnIsCheckedChanged(bool value)
    {
        if (value)
        {
            _checked(this);
        }
    }

    /// <summary>Radio buttons, the first checked, of which one is always checked.</summary>
    internal static IReadOnlyList<ChoiceOption> Group(IReadOnlyList<string> texts, Action<ChoiceOption> onChecked)
    {
        var options = new List<ChoiceOption>();
        foreach (var text in texts)
        {
            options.Add(new ChoiceOption(text, chosen =>
            {
                foreach (var other in options.Where(other => !ReferenceEquals(other, chosen)))
                {
                    other.IsChecked = false;
                }

                onChecked(chosen);
            }));
        }

        if (options.Count > 0)
        {
            options[0].IsChecked = true;
        }

        return options;
    }
}

/// <summary>
/// The remote choice dialog (<c>ChoiceDialog</c>, docs/phase-3-design.md 2.29, 4f): "Push v1.0
/// to…", "Delete v1.0 on…": one radio button per remote (<c>ChoiceOption</c>), the first checked.
/// Answers the chosen text, or null when cancelled.
/// </summary>
public sealed partial class ChoiceDialogViewModel : DialogViewModel<string?>
{
    /// <param name="title">The card's title, such as "Push v1.0 to…".</param>
    /// <param name="options">The choices, such as the remotes' names; at least one.</param>
    /// <param name="confirmText">The confirm button's label, such as "Push".</param>
    /// <param name="isDestructive">The confirm button has the danger style (a deletion on a remote).</param>
    public ChoiceDialogViewModel(string title, IReadOnlyList<string> options, string confirmText, bool isDestructive = false)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(confirmText);
        if (options.Count == 0)
        {
            throw new ArgumentException("A choice needs at least one option.", nameof(options));
        }

        Title = title;
        ConfirmText = confirmText;
        IsDestructive = isDestructive;
        Options = ChoiceOption.Group(options, chosen => Selected = chosen.Text);
        Selected = options[0];
    }

    public override string Title { get; }

    public string ConfirmText { get; }

    public bool IsDestructive { get; }

    public IReadOnlyList<ChoiceOption> Options { get; }

    /// <summary>The checked option's text.</summary>
    [ObservableProperty]
    public partial string Selected { get; private set; }

    protected override string? CancelledResult => null;

    [RelayCommand]
    private void Confirm() => Complete(Selected);
}
