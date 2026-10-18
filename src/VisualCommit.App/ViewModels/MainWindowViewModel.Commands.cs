using VisualCommit.App.ViewModels.Commands;

namespace VisualCommit.App.ViewModels;

// The keys and the command palette (D191, D192; docs/phase-6-design.md 5, 6). Work package WP-K
// adds the commands' states and runs, the palette and the router's calls.
public partial class MainWindowViewModel
{
    /// <summary>
    /// The shown key texts of the shell's buttons (6.8), from <see cref="KeyTexts.Current"/>: what
    /// their <c>AutomationProperties.AcceleratorKey</c> binds to, enabled or not.
    /// </summary>
    public ShellKeys Keys { get; } = new(KeyTexts.Current);

    /// <summary>What the window does for a key or a palette entry that moves the focus (6.3); set by <c>MainWindow</c> with its data context, null without a window.</summary>
    public IWindowKeyActions? WindowActions { get; set; }
}
