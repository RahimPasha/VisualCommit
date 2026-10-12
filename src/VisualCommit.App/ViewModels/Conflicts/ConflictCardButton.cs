using CommunityToolkit.Mvvm.Input;

namespace VisualCommit.App.ViewModels.Conflicts;

/// <summary>
/// A button of the card a conflict without text shows (docs/phase-4-design.md 11.8): its label,
/// its automation id (7.4) and what it runs, one operation <c>Resolve {file}</c> after which the
/// resolver moves on (11.7).
/// </summary>
public sealed partial class ConflictCardButton
{
    private readonly Func<Task> _run;

    public ConflictCardButton(string label, string automationId, Func<Task> run)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(automationId);
        ArgumentNullException.ThrowIfNull(run);
        Label = label;
        AutomationId = automationId;
        _run = run;
    }

    /// <summary>What it says: <c>Keep the file</c>, <c>Delete it</c>, <c>Use ours</c>, …</summary>
    public string Label { get; }

    /// <summary>Its automation id: <c>ConflictKeepFile</c>, <c>ConflictDeleteFile</c>, <c>ConflictUseOursFile</c>, …</summary>
    public string AutomationId { get; }

    /// <summary>Resolves the file as the button says.</summary>
    [RelayCommand]
    private Task Run() => _run();
}
