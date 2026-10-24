using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

// The settings' part of a repository's view model (docs/phase-6-design.md 7, 12.1): the chosen
// editor for the file menus, read from the settings each time a menu is built, with no
// subscription.
public sealed partial class RepositoryViewModel
{
    /// <summary>
    /// The unknown editor id this view model logged while it stays in the settings, so that each is
    /// logged once (12.6); only touched on the UI thread.
    /// </summary>
    private string? _unknownEditorLogged;

    /// <summary>
    /// Whether an editor is chosen for the file menus (12.1), from the settings, with no disk access:
    /// <see cref="RepositoryMenuContext"/> is built on the UI thread for every menu, the palette and
    /// some page loads, and a check of a program on a share that does not answer blocks for minutes
    /// (0.9); a program that is gone is said when the item is clicked (12.7). False while none is
    /// chosen (<see cref="KnownEditors.IsChosen"/>): an id the catalogue no longer knows, other than
    /// Other's, is logged once while it stays in the settings.
    /// </summary>
    private bool IsEditorChosen()
    {
        var editor = _settings.Current.Editor;
        if (editor is null || KnownEditors.IsChosen(editor))
        {
            _unknownEditorLogged = null;
            return editor is not null;
        }

        if (editor.Id != _unknownEditorLogged)
        {
            _unknownEditorLogged = editor.Id;
            _log.Info($"The editor {editor.Id} in the settings is not known to this version; no editor is chosen.");
        }

        return false;
    }
}
