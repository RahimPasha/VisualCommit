// File actions: docs/phase-3-design.md 2.30 (ignore), 2.32 (open, show in folder, copy SHA,
// SHAs, path, branch or tag name, URL), 4b's file menus and the working-changes row's Stage all,
// Unstage all and Discard all, which delegate to phase 2's code in RepositoryViewModel (stage,
// unstage, discard with its confirmation and snapshot, open diff). The launcher and the
// clipboard come from TabServices (Launcher, Clipboard); none of these but ignore is an
// operation in the queue. Stubs left by WP7-core: the sub-package WP7-stashtags replaces each
// body and keeps every signature (IRepositoryActions lists them). Its tests go into
// tests/VisualCommit.App.Tests/RepositoryOperationsTests.Files.cs (a part of the partial class
// RepositoryOperationsTests, through OperationsHarness, whose FakeFileLauncher and FakeClipboard
// record what was asked).

using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    private const string FilesPackage = "stashtags";

    public Task StageFilesAsync(IReadOnlyList<ChangedFile> files) => NotBuilt(FilesPackage);

    public Task UnstageFilesAsync(IReadOnlyList<ChangedFile> files) => NotBuilt(FilesPackage);

    public Task DiscardFilesAsync(IReadOnlyList<ChangedFile> files) => NotBuilt(FilesPackage);

    public Task StageAllAsync() => NotBuilt(FilesPackage);

    public Task UnstageAllAsync() => NotBuilt(FilesPackage);

    public Task DiscardAllAsync() => NotBuilt(FilesPackage);

    public Task IgnoreFileAsync(string path) => NotBuilt(FilesPackage);

    public Task IgnoreExtensionAsync(string path) => NotBuilt(FilesPackage);

    public Task OpenFileAsync(string path) => NotBuilt(FilesPackage);

    public Task ShowInFolderAsync(string? path) => NotBuilt(FilesPackage);

    public Task CopyShaAsync(string sha) => NotBuilt(FilesPackage);

    public Task CopyShasAsync(IReadOnlyList<string> shas) => NotBuilt(FilesPackage);

    public Task CopyPathAsync(string path) => NotBuilt(FilesPackage);

    public Task CopyRefNameAsync(string fullRef) => NotBuilt(FilesPackage);

    public Task CopyRemoteUrlAsync(string remote) => NotBuilt(FilesPackage);

    public void OpenDiff(ChangedFile file, bool staged) => throw new NotImplementedException($"WP7: {FilesPackage}: {nameof(OpenDiff)}");
}
