using System.Reflection;
using System.Runtime.ExceptionServices;
using VisualCommit.Core.Git;

namespace VisualCommit.App.Tests;

/// <summary>
/// The real repository, except that the reads a menu waits for (the worktrees, and whether a
/// branch is an ancestor of another; 4a, D131) wait while <see cref="Hold"/> holds them, until
/// <see cref="Release"/>. A test then makes Esc, another menu, a dialog or another tab come
/// before the read ends, however fast git answers on the machine. Every other call goes straight
/// to the real repository, and so do these reads while nothing holds them.
/// </summary>
internal class HeldReads : DispatchProxy
{
    private static readonly MethodInfo HeldCall =
        typeof(HeldReads).GetMethod(nameof(CallWhenReleasedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private IGitRepository _inner = null!;
    private TaskCompletionSource _gate = Released();
    private int _waiting;

    /// <summary>How many reads wait for <see cref="Release"/> now.</summary>
    public int Waiting => Volatile.Read(ref _waiting);

    /// <summary><paramref name="inner"/> with its menus' reads held while <see cref="Hold"/> holds them.</summary>
    public static (IGitRepository Repository, HeldReads Reads) Wrap(IGitRepository inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        var proxy = Create<IGitRepository, HeldReads>();
        var reads = (HeldReads)(object)proxy;
        reads._inner = inner;
        return (proxy, reads);
    }

    /// <summary>From now on, the menus' reads wait for <see cref="Release"/>.</summary>
    public void Hold() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The reads that wait run now, and those that come later run at once.</summary>
    public void Release() => _gate.TrySetResult();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        return targetMethod.Name is nameof(IGitRepository.ReadWorktreesAsync) or nameof(IGitRepository.IsAncestorAsync)
            ? HeldCall.MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0]).Invoke(this, [targetMethod, args])
            : Forward(targetMethod, args);
    }

    private static TaskCompletionSource Released()
    {
        var released = new TaskCompletionSource();
        released.SetResult();
        return released;
    }

    private async Task<T> CallWhenReleasedAsync<T>(MethodInfo method, object?[]? args)
    {
        var gate = _gate.Task;
        if (!gate.IsCompleted)
        {
            Interlocked.Increment(ref _waiting);
            try
            {
                await gate;
            }
            finally
            {
                Interlocked.Decrement(ref _waiting);
            }
        }

        return await (Task<T>)Forward(method, args)!;
    }

    private object? Forward(MethodInfo method, object?[]? args)
    {
        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(ex.InnerException);
            throw;
        }
    }
}
