using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.Services;

/// <summary>
/// Answers git's and ssh's prompts with dialogs in the window (docs/phase-3-design.md 3.2, D82).
/// A placeholder of work package WP0 so that <c>AppSession</c> can create the prompt channel:
/// it refuses every prompt, and <see cref="CreateBroker"/> gives a broker that leads no prompt to
/// the app, as phases 0 to 2 had (D30). WP3 owns this file: it builds the dialogs here and makes
/// <see cref="CreateBroker"/> return the askpass broker.
/// </summary>
public sealed class PromptDialogs : IGitPromptHandler
{
    public PromptDialogs(DialogHostViewModel dialogs, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        Dialogs = dialogs;
        Log = log ?? NullAppLog.Instance;
    }

    private DialogHostViewModel Dialogs { get; }

    private IAppLog Log { get; }

    public Task<PromptAnswer> AnswerAsync(GitPrompt prompt, CancellationToken cancellationToken) =>
        Task.FromResult(PromptAnswer.Refused);

    /// <summary>
    /// The app session's prompt broker, answering through <paramref name="handler"/>. Called once
    /// by <c>AppSession.Start</c>, which disposes the broker at the end of the session when it is
    /// <see cref="IDisposable"/>.
    /// </summary>
    public static IGitPromptBroker CreateBroker(IGitPromptHandler handler, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(log);
        return NullGitPromptBroker.Instance;
    }
}
