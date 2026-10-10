using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace VisualCommit.App.Services;

/// <summary>
/// Asks the user for a folder (D42). The app uses the platform's own folder dialog; the scripted
/// walk-through, which cannot drive a native dialog, gives the app a fake that answers with a
/// folder the test chose.
/// </summary>
public interface IFolderPicker
{
    /// <summary>Shows the dialog with <paramref name="title"/> and returns the chosen folder, or null when the user cancelled.</summary>
    Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default);
}

/// <summary>The platform's folder dialog, opened over the app's main window.</summary>
public sealed class StorageFolderPicker : IFolderPicker
{
    private readonly Func<TopLevel?> _owner;

    public StorageFolderPicker(Func<TopLevel?> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
    {
        var owner = _owner();
        if (owner is null || !owner.StorageProvider.CanPickFolder)
        {
            return null;
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}
