using System.Collections.Concurrent;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

// The settings' part of a repository's view model (docs/phase-6-design.md 7, 12.1): the chosen
// editor for the file menus, read from the settings each time a menu is built, with no
// subscription.
public sealed partial class RepositoryViewModel
{
    /// <summary>The editor ids already logged as unknown, so that each is logged once (12.6).</summary>
    private static readonly ConcurrentDictionary<string, bool> UnknownEditorsLogged = new(StringComparer.Ordinal);

    /// <summary>
    /// The chosen editor as the file menus name it (12.1), from the settings, with no disk access:
    /// <see cref="RepositoryMenuContext"/> is built on the UI thread for every menu, the palette and
    /// some page loads, and a check of a program on a share that does not answer blocks for minutes
    /// (0.9); a program that is gone is said when the item is clicked (12.7). Null while none is
    /// chosen, and for an id the catalogue no longer knows (other than Other's), which is logged once.
    /// </summary>
    private EditorMenuState? EditorForMenus()
    {
        if (_settings.Current.Editor is not { } editor)
        {
            return null;
        }

        if (editor.Id == KnownEditors.CustomId)
        {
            return new EditorMenuState(VisualCommit.App.ViewModels.Operations.RepositoryOperations.EditorName(editor, known: null));
        }

        if (KnownEditors.Find(editor.Id) is { } known)
        {
            return new EditorMenuState(known.Name);
        }

        if (UnknownEditorsLogged.TryAdd(editor.Id, true))
        {
            _log.Info($"The editor {editor.Id} in the settings is not known to this version; no editor is chosen.");
        }

        return null;
    }
}
