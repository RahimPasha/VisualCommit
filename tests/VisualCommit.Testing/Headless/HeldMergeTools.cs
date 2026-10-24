using VisualCommit.Core;
using VisualCommit.Core.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Tool catalogues whose merge-tool list waits until the test releases it (docs/phase-6-design.md
/// 12.9, 8.10): a check of the loading state, which D202's kept list leaves to the very first
/// lookup on a machine. The editors and <c>PATHEXT</c> are <see cref="ToolCatalogues.Fixed"/>'s.
/// </summary>
public sealed class HeldMergeTools
{
    private readonly TaskCompletionSource<MergeToolHelp> _list = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _requests;

    /// <param name="list">What the lookup answers once released; <see cref="ToolCatalogues.FixedMergeTools"/> when null.</param>
    public HeldMergeTools(MergeToolHelp? list = null)
    {
        List = list ?? ToolCatalogues.FixedMergeTools;
        Catalogues = new ToolCatalogues(
            ToolCatalogues.Fixed.FindEditors,
            cancellationToken =>
            {
                Interlocked.Increment(ref _requests);
                return _list.Task.WaitAsync(cancellationToken);
            },
            ToolCatalogues.Fixed.PathExtensions);
    }

    /// <summary>The catalogues to give the app or the screen.</summary>
    public ToolCatalogues Catalogues { get; }

    /// <summary>What the lookup answers once released.</summary>
    public MergeToolHelp List { get; }

    /// <summary>How many times the list was asked for.</summary>
    public int Requests => Volatile.Read(ref _requests);

    /// <summary>The lookup answers <see cref="List"/>.</summary>
    public void Release() => _list.TrySetResult(List);

    /// <summary>The lookup fails, as git that could not list its tools (8.10): no list, <c>Failed</c> set.</summary>
    public void Fail() => _list.TrySetResult(MergeToolHelp.FailedLookup);
}
