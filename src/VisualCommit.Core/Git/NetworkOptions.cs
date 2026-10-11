using VisualCommit.Core.Operations;

namespace VisualCommit.Core.Git;

/// <summary>
/// How the git calls of a phase 3 operation run (phase 3 design 1.5, 1.9): the prompt scope's
/// variables for their environment and where their progress goes. Network operations need it;
/// phase 3's local operations take it too, because a filter or hook they run can ask for
/// credentials (git-lfs) and a large checkout prints progress.
/// </summary>
public sealed record NetworkOptions
{
    private static readonly IReadOnlyDictionary<string, string?> NoVariables = new Dictionary<string, string?>();

    /// <summary>No extra variables, no progress: a call that cannot prompt.</summary>
    public static NetworkOptions None { get; } = new();

    /// <summary>Variables added to the environment of every git call (the prompt scope's, section 3).</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = NoVariables;

    /// <summary>Where git's progress lines go, parsed (<c>GitProgressParser</c>); null drops them.</summary>
    public IProgress<OperationProgress>? Progress { get; init; }

    /// <summary>
    /// Whether the user has cancelled a prompt of the operation (<see cref="OperationContext.PromptCancelled"/>):
    /// a call made of several git calls stops before the next one, which could ask again. Null: never.
    /// </summary>
    public Func<bool>? PromptCancelled { get; init; }

    /// <summary>The options of a running operation: its prompt scope's variables, its progress and whether a prompt of it was cancelled.</summary>
    public static NetworkOptions From(OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new NetworkOptions { Environment = context.Environment, Progress = context.Progress, PromptCancelled = () => context.PromptCancelled };
    }
}
