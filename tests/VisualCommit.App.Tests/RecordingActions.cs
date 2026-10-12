using System.Reflection;
using VisualCommit.App.ViewModels.Operations;

namespace VisualCommit.App.Tests;

/// <summary>
/// An <see cref="IRepositoryActions"/> that does nothing but record each call, for the menu
/// builders' tests (<c>RepositoryMenusTests</c>): invoking a menu item shows which action it
/// started, with which arguments. Every action completes at once.
/// </summary>
internal class RecordingActions : DispatchProxy
{
    private readonly Lock _gate = new();
    private readonly List<RecordedAction> _calls = [];

    /// <summary>The actions started, in order.</summary>
    public IReadOnlyList<RecordedAction> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>The log the menu items write to.</summary>
    public RecordingLog Log { get; } = new();

    /// <summary>A recording <see cref="IRepositoryActions"/> and its recorder.</summary>
    public static (IRepositoryActions Actions, RecordingActions Recorder) Create()
    {
        var actions = Create<IRepositoryActions, RecordingActions>();
        return (actions, (RecordingActions)(object)actions);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.Name == "get_" + nameof(IRepositoryActions.Log))
        {
            return Log;
        }

        lock (_gate)
        {
            _calls.Add(new RecordedAction(targetMethod.Name, args ?? []));
        }

        return targetMethod.ReturnType == typeof(Task) ? Task.CompletedTask : null;
    }
}

/// <summary>One call of <see cref="IRepositoryActions"/>: the method's name and its arguments.</summary>
internal sealed record RecordedAction(string Method, IReadOnlyList<object?> Arguments)
{
    public override string ToString() => $"{Method}({string.Join(", ", Arguments)})";
}

