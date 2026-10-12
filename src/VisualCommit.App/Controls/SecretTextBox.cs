using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace VisualCommit.App.Controls;

/// <summary>
/// A stock <see cref="TextBox"/>, styled as one, for a password, token or passphrase
/// (docs/phase-3-design.md 3.2, D103): while it is masked (<see cref="TextBox.PasswordChar"/> is
/// set), its automation peer gives UI Automation clients none of its text. The stock peer
/// offers the Value pattern with the plain text and raises a change of it, old and new text, on
/// every key, so any process of the same user that walks the window's automation tree could read
/// what is typed. Avalonia's peers have no "is password" property to report instead.
/// </summary>
public sealed class SecretTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>Whether the box hides what is typed, and its peer hides it too.</summary>
    public bool IsMasked => PasswordChar != '\0';

    protected override AutomationPeer OnCreateAutomationPeer() => new SecretTextBoxAutomationPeer(this);

    /// <summary>
    /// The stock text box's peer, whose Value pattern reads as empty and whose text changes are
    /// not raised while the box is masked; a box that is not masked (a prompt that asks for no
    /// secret) behaves as a stock one. Setting the value still works, so that a password manager
    /// can fill the box.
    /// </summary>
    private sealed class SecretTextBoxAutomationPeer(SecretTextBox owner) : TextBoxAutomationPeer(owner), IValueProvider
    {
        bool IValueProvider.IsReadOnly => IsReadOnly;

        string? IValueProvider.Value => owner.IsMasked ? string.Empty : Value;

        void IValueProvider.SetValue(string? value) => SetValue(value);

        protected override void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (owner.IsMasked && e.Property == TextBox.TextProperty)
            {
                return;
            }

            base.OwnerPropertyChanged(sender, e);
        }
    }
}
