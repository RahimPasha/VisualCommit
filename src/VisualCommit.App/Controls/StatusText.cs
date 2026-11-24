using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace VisualCommit.App.Controls;

/// <summary>
/// The status bar's operation text, a polite live region (D435) that is told of what an operation
/// says, not of each step of its progress. It shows its <see cref="TextBlock.Text"/>, git's stage
/// and percentage included, and UI Automation names it by that text, as a <see cref="TextBlock"/>;
/// but where a text block's peer raises a Name change, which a live region turns into speech, on
/// every new text, up to ten times a second while git reports its progress, this one raises it only
/// when <see cref="SpokenText"/> changes: an operation's start, its result, ssh's notice. Styled as
/// a plain <see cref="TextBlock"/>.
/// </summary>
public sealed class StatusText : TextBlock
{
    /// <summary>Defines the <see cref="SpokenText"/> property.</summary>
    public static readonly StyledProperty<string?> SpokenTextProperty =
        AvaloniaProperty.Register<StatusText, string?>(nameof(SpokenText));

    /// <summary>The text without its progress: each change raises one Name change on the peer, with the text shown as the new name. Bind it after <see cref="TextBlock.Text"/>.</summary>
    public string? SpokenText
    {
        get => GetValue(SpokenTextProperty);
        set => SetValue(SpokenTextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override AutomationPeer OnCreateAutomationPeer() => new StatusTextAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Only a peer a client has made hears it: none is made here.
        if (change.Property == SpokenTextProperty && ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, change.OldValue, Text);
        }
    }

    /// <summary>A text block's peer, but for the Name changes it raises: the owner raises them, for its spoken text alone.</summary>
    private sealed class StatusTextAutomationPeer(StatusText owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(TextBlock);

        protected override string? GetNameCore() => owner.Inlines?.Text ?? owner.Text;

        protected override bool IsControlElementCore() => owner.TemplatedParent is null && base.IsControlElementCore();
    }
}
