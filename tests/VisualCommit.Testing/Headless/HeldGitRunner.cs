using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Git;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// A slow git (docs/phase-8a-design.md 1.15, 6.4): an <see cref="IGitRunner"/> over the git the
/// machine's search finds, which holds the next call whose first argument is the word given to
/// <see cref="HoldNext"/> (an unstaged file's diff is <c>diff-files</c>, a commit <c>commit</c>)
/// until <see cref="Release"/>; <see cref="Started"/> completes with that call when it comes. Every
/// other call goes through at once. A session gets it as its detection's runner through
/// <see cref="Probe"/> (<c>ShellDriver.Start(gitProbe: held.Probe)</c>): a tab's git calls take that
/// runner (<c>GitAccess.GetRunnerAsync</c>), and the call log records each call as the found git's
/// runner does.
/// </summary>
public sealed class HeldGitRunner : IGitRunner
{
    private readonly Lock _gate = new();
    private IGitRunner? _inner;
    private string? _word;
    private TaskCompletionSource<GitCommand> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public HeldGitRunner()
    {
        Probe = new HandingProbe(this);
    }

    /// <summary>The probe to start a session with: the machine's search, its detection handing this runner over in place of the found git's.</summary>
    public IGitSetupProbe Probe { get; }

    /// <summary>The found git's path.</summary>
    public string ExecutablePath => Inner.ExecutablePath;

    /// <summary>The call held since the last <see cref="HoldNext"/>, once it has come.</summary>
    public Task<GitCommand> Started
    {
        get
        {
            lock (_gate)
            {
                return _started.Task;
            }
        }
    }

    private IGitRunner Inner => _inner ?? throw new InvalidOperationException("No session has found git through the held runner's probe yet.");

    /// <summary>Holds the next call whose first argument is <paramref name="word"/>, until <see cref="Release"/>; a hold not yet met is replaced.</summary>
    public void HoldNext(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        lock (_gate)
        {
            _release.TrySetResult();
            _word = word;
            _started = new TaskCompletionSource<GitCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Lets the held call go on to git; a hold not yet met is dropped.</summary>
    public void Release()
    {
        lock (_gate)
        {
            _word = null;
            _release.TrySetResult();
        }
    }

    /// <inheritdoc/>
    public async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        Task? held = null;
        lock (_gate)
        {
            if (_word is { } word && command.Arguments.Count > 0 && string.Equals(command.Arguments[0], word, StringComparison.Ordinal))
            {
                _word = null;
                held = _release.Task;
                _started.TrySetResult(command);
            }
        }

        if (held is not null)
        {
            await held.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await Inner.RunAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The machine's search, its detection handing the held runner over in place of the found git's runner.</summary>
    private sealed class HandingProbe(HeldGitRunner held) : IGitSetupProbe
    {
        public async Task<GitDetection> DetectAsync(
            GitLookup lookup,
            IGitCallLog? callLog,
            IAppLog log,
            TimeProvider? recordClock,
            IReadOnlyDictionary<string, string?>? environment,
            CancellationToken cancellationToken)
        {
            var detection = await MachineGitSetupProbe.Instance.DetectAsync(lookup, callLog, log, recordClock, environment, cancellationToken).ConfigureAwait(false);
            if (detection.Runner is not { } runner)
            {
                return detection;
            }

            lock (held._gate)
            {
                held._inner = runner;
            }

            return detection with { Runner = held };
        }

        public GitGuidance ReadGuidance() => MachineGitSetupProbe.Instance.ReadGuidance();
    }
}
