// A new pull request (docs/phase-7a-design.md 1.7, section 12; D251, D257, D312 to D318, D338):
// the form of a branch's or a drop's "Create pull request…", then the push when one is needed and
// the create, as one network operation of the repository's queue. The form never runs git or HTTP
// itself: PullRequestForm does, on the UI thread.

using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;
using VisualCommit.Hosting;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <inheritdoc cref="IRepositoryActions.CreatePullRequestAsync"/>
    /// <remarks>
    /// 12.3: without a GitHub remote nothing starts; signed out the sign-in comes first (8.5); the
    /// head is a local branch, or a remote branch of a GitHub remote that fetches a branch; the form
    /// opens with the head's place (D313), its subject as the title and, for a drop, the target as
    /// the base. The form's answer is not used.
    /// </remarks>
    public Task CreatePullRequestAsync(string headRef, string? baseRef)
    {
        ArgumentNullException.ThrowIfNull(headRef);
        return GuardAsync(Texts.Operation(ShortName(headRef)), async () =>
        {
            if (IsClosed)
            {
                return;
            }

            if (Repository.HostingState.PullRequests == PullRequestsState.NotGitHub)
            {
                Log.Info($"{Repository.Name}: Create pull request for {ShortName(headRef)}: not started, the repository has no GitHub remote.");
                return;
            }

            if (Repository.HostingState.PullRequests == PullRequestsState.SignedOut
                && (Services?.SignInToGitHub is not { } signIn || !await signIn()))
            {
                return;
            }

            if (IsClosed || Refs is not { } refs || PullRequestForm.HeadOf(this, refs, headRef) is not { } head)
            {
                Log.Info($"{Repository.Name}: Create pull request for {ShortName(headRef)}: not started, it is no branch GitHub can hold.");
                return;
            }

            var form = new PullRequestForm(this, head);
            await form.ComputePlaceAsync();
            form.DefaultTitle = await ReadSubjectAsync(head.TargetSha) ?? string.Empty;
            if (baseRef is not null)
            {
                form.GivenBase = form.BaseOf(refs, baseRef);
                if (form.GivenBase is null)
                {
                    Log.Info($"{Repository.Name}: Create pull request for {form.Head.Text}: not started, {ShortName(baseRef)} is no branch on GitHub.");
                    return;
                }
            }

            if (!IsClosed)
            {
                await ShowDialogAsync(new CreatePullRequestDialogViewModel(form, Log));
            }
        });
    }

    /// <summary>
    /// Where the head is on GitHub (12.3 step 3, D313): its repository and branch, and the push
    /// that comes first, if any, with its note.
    /// </summary>
    private sealed record PullRequestPlace(HostedRepository Repository, string Branch, PushRequest? Push, string Target, string? Note);

    /// <summary>
    /// The form's side of a new pull request (12.3 to 12.5): the head's place computed from the
    /// refs at each Create, the bases, A3's default, and Create as one network operation of the
    /// queue (D312, D315, D316), its cancel window open while the push runs (ruling 59).
    /// </summary>
    private sealed partial class PullRequestForm(RepositoryOperations operations, GitRef head) : ICreatePullRequestActions
    {
        private PullRequestPlace? _place;
        private PullRequest? _existing;
        private CancellationTokenSource? _create;
        private bool _window;

        public PullRequestEnd Head => new(head.Name, _place?.Repository.FullName ?? string.Empty) { Branch = _place?.Branch };

        public string DefaultTitle { get; set; } = string.Empty;

        public PullRequestEnd? GivenBase { get; set; }

        public bool CanCancelCreate => _window;

        public event EventHandler? CanCancelCreateChanged;

        private RepositoryViewModel Repository => operations.Repository;

        private IAppLog Log => operations.Log;

        /// <summary>The head as the form and the operation's title name it: a local branch's name, a remote branch's ref name (the report's ruling 48).</summary>
        private string HeadText => head.Name;

        private string? Login => operations.Services?.Hosting.Account?.Account.Login;

        /// <summary>
        /// The head from the refs now (12.3 step 2): a local branch, or a remote branch of a remote
        /// that is GitHub's whose fetch line maps it from a branch of that remote; null otherwise.
        /// </summary>
        public static GitRef? HeadOf(RepositoryOperations operations, RepoRefs refs, string headRef) =>
            operations.FindRef(headRef) switch
            {
                { Kind: RefKind.LocalBranch } local => local,
                { Kind: RefKind.RemoteBranch, RemoteName: { } remote } remoteBranch
                    when operations.Repository.HostingState.Remotes.ContainsKey(remote)
                        && refs.SourceOf(remoteBranch) is { IsBranch: true, Remote: { } source }
                        && string.Equals(source, remote, StringComparison.Ordinal) => remoteBranch,
                _ => null,
            };

        /// <summary>
        /// A drop's base (12.3 step 5): a remote branch of a GitHub remote, or the upstream of a
        /// local branch whose upstream is a branch of one under the same name; null otherwise.
        /// </summary>
        public PullRequestEnd? BaseOf(RepoRefs refs, string baseRef)
        {
            var hosting = Repository.HostingState;
            return operations.FindRef(baseRef) switch
            {
                { Kind: RefKind.RemoteBranch } remoteBranch => RemoteBranchEnd(refs, hosting, remoteBranch),
                { Kind: RefKind.LocalBranch, UpstreamRemote: { } remote, UpstreamRemoteRef: { } remoteRef } local
                    when hosting.Remotes.TryGetValue(remote, out var repository) && string.Equals(remoteRef, HeadsPrefix + local.Name, StringComparison.Ordinal)
                    => new PullRequestEnd(UpstreamName(local, remote), repository.FullName) { Branch = local.Name },
                _ => null,
            };
        }

        /// <summary>
        /// The head's place (12.3 step 3, D313), computed again at each Create from the refs then;
        /// the count of commits GitHub's branch lacks is read here, only when that branch exists
        /// and is not at the head's tip. None when the refs and remotes now give none (a remote
        /// branch whose remote is no longer GitHub's): the earlier place is never used again.
        /// </summary>
        public async Task ComputePlaceAsync()
        {
            _place = null;
            if (operations.Refs is not { } refs)
            {
                return;
            }

            var hosting = Repository.HostingState;
            var current = operations.FindRef(head.FullName) ?? head;
            if (current.Kind == RefKind.RemoteBranch)
            {
                // A remote branch is never pushed.
                if (refs.SourceOf(current) is { IsBranch: true, Remote: { } remote, Branch: { } branch } && hosting.Remotes.TryGetValue(remote, out var repository))
                {
                    _place = new PullRequestPlace(repository, branch, null, current.Name, null);
                }

                return;
            }

            var name = current.Name;
            if (current.UpstreamRemote is { } upstreamRemote
                && hosting.Remotes.TryGetValue(upstreamRemote, out var upstreamRepository)
                && string.Equals(current.UpstreamRemoteRef, HeadsPrefix + name, StringComparison.Ordinal))
            {
                // An upstream of the same name on GitHub: pushed to it, without --set-upstream, when ahead or gone.
                var target = UpstreamName(current, upstreamRemote);
                var push = current.Ahead > 0 || current.UpstreamGone ? new PushRequest(upstreamRemote, name, current.UpstreamRemoteRef!) : null;
                var note = current.UpstreamGone
                    ? Texts.GonePushedFirst(target)
                    : current.Ahead > 0 ? Texts.AheadPushedFirst(current.Ahead, target) : null;
                _place = new PullRequestPlace(upstreamRepository, name, push, target, note);
                return;
            }

            // Any other local branch: the first GitHub remote in HostingRemoteOrder, under its own name.
            if (HostingRemoteOrder.Of(hosting.Remotes.Keys).FirstOrDefault() is not { } first)
            {
                return;
            }

            var firstRepository = hosting.Remotes[first];
            var firstTarget = $"{first}/{name}";
            var request = new PushRequest(first, name, HeadsPrefix + name);
            if (current.UpstreamRef is null)
            {
                _place = new PullRequestPlace(firstRepository, name, request with { SetUpstream = true }, firstTarget, Texts.PushedFirstWithUpstream(firstTarget));
                return;
            }

            // With an upstream elsewhere, left as it is: pushed only when GitHub lacks the branch's commits.
            var onGitHub = refs.Refs.FirstOrDefault(other => other.Kind == RefKind.RemoteBranch
                && refs.SourceOf(other) is { IsBranch: true } source
                && string.Equals(source.Remote, first, StringComparison.Ordinal)
                && string.Equals(source.Branch, name, StringComparison.Ordinal));
            if (onGitHub is null)
            {
                _place = new PullRequestPlace(firstRepository, name, request, firstTarget, Texts.PushedFirst(firstTarget));
                return;
            }

            var count = string.Equals(onGitHub.TargetSha, current.TargetSha, StringComparison.OrdinalIgnoreCase)
                ? 0
                : await operations.Git.CountCommitsAsync(onGitHub.FullName, current.FullName);
            _place = count > 0
                ? new PullRequestPlace(firstRepository, name, request, onGitHub.Name, Texts.AheadPushedFirst(count, onGitHub.Name))
                : new PullRequestPlace(firstRepository, name, null, onGitHub.Name, null);
        }

        public string? HeadNote() => _place?.Note;

        /// <summary>The default base (12.3, D318): A3's parent's default branch for a fork, else its own; with A3's failure, none and its words.</summary>
        public async Task<CreatePullRequestStart> StartAsync(CancellationToken ct)
        {
            var read = await Repository.Hosting.ReadRepositoryAsync(ct);
            if (read.Value is not { } info)
            {
                return new CreatePullRequestStart(null, read.Failure is { } failure ? HostingTexts.Describe(failure, Login) : null);
            }

            var (repository, branch) = info.Parent is { } parent
                ? (parent, info.ParentDefaultBranch ?? info.DefaultBranch)
                : (info.Repository, info.DefaultBranch);
            return new CreatePullRequestStart(DefaultEnd(repository, branch), null);
        }

        /// <summary>The stacked Pick of the bases (12.3; D104, D318), each with its repository as its detail; Esc closes the Pick only.</summary>
        public async Task<PullRequestEnd?> ChooseBaseAsync(PullRequestEnd? current)
        {
            var options = await BaseOptionsAsync();
            var pick = new PickDialogViewModel(
                Texts.ChooseBaseTitle,
                [.. options.Select(option => new PickOption(option.Text, option.Repository))],
                current?.Text,
                OperationTexts.SettingsTexts.PickConfirm);
            return await operations.ShowDialogAsync(pick) is { } chosen
                ? options.FirstOrDefault(option => string.Equals(option.Text, chosen, StringComparison.Ordinal))
                : null;
        }

        public bool IsSameBranch(PullRequestEnd baseEnd)
        {
            ArgumentNullException.ThrowIfNull(baseEnd);
            return _place is { } place
                && string.Equals(place.Repository.FullName, baseEnd.Repository, StringComparison.OrdinalIgnoreCase)
                && string.Equals(place.Branch, baseEnd.Branch ?? baseEnd.Text, StringComparison.Ordinal);
        }

        public Task OpenExistingAsync() => _existing is { } existing ? operations.OpenPullRequestAsync(existing) : Task.CompletedTask;

        public void CancelCreate()
        {
            if (_window)
            {
                _create?.Cancel();
            }
        }

        /// <summary>
        /// 12.5: the head's place again from the refs now, then one network operation: the push when
        /// it needs one (WP-C's guards, then git), the end of the cancel window (D315), the POST, and
        /// on success the list's refresh and the status bar's offer (D317); a failure's words come
        /// back to the form (D312), with the head's place read again from the refs the operation
        /// left (12.4: a push that ended before a refused POST leaves no note). Its own
        /// cancellation (<see cref="CancelCreate"/>) cancels it through the queue.
        /// </summary>
        public async Task<CreatePullRequestOutcome> CreateAsync(PullRequestEnd baseEnd, string title, string body, bool draft)
        {
            ArgumentNullException.ThrowIfNull(baseEnd);
            var text = Texts.Operation(HeadText);
            await ComputePlaceAsync();
            if (_place is not { } place || RepositoryOf(baseEnd.Repository) is not { } baseRepository)
            {
                return new CreatePullRequestOutcome(false, text.FailureSummary, null);
            }

            var baseBranch = baseEnd.Branch ?? baseEnd.Text;
            var push = place.Push;
            string? words = null;
            _existing = null;
            Log.Info($"{Repository.Name}: {text.Title}: into {baseRepository.FullName} {baseBranch}, draft {(draft ? "yes" : "no")}, "
                + (push is null ? "no push" : $"push to {place.Target} first"));

            using var cancellation = new CancellationTokenSource();
            _create = cancellation;
            SetWindow(push is not null);
            OperationResult result;
            try
            {
                // Pushing, the status bar names the push from the start (12.5 step 1), then the POST.
                result = await operations.RunAsync(
                    push is null ? text : text with { RunningText = OperationTexts.Push(push.Branch, place.Target).RunningText },
                    OperationKind.Network,
                    push is null ? RefreshScope.None : RefreshScope.Refs,
                    async context =>
                    {
                        if (push is not null)
                        {
                            context.SetRunningText(OperationTexts.Push(push.Branch, place.Target).RunningText);
                            if ((operations.RefuseIfNotABranch(text, push.Remote, push.RemoteRef, place.Target)
                                ?? await operations.RefuseIfPushStopsFetchAsync(text, push.Branch, push.Remote, push.RemoteRef, place.Target)) is { } refused)
                            {
                                words = refused.Failure?.Summary;
                                return refused;
                            }

                            UndoCapture.Push(push.Remote, await operations.Git.PushAsync(push, NetworkOptions.From(context), context.CancellationToken));
                        }

                        context.SetRunningText(Texts.CreatingPullRequest);
                        SetWindow(false);
                        context.EndCancelWindow();
                        var (report, said) = await PostAsync(text, place, baseRepository, baseEnd, baseBranch, title, body, draft);
                        words = said;
                        return report;
                    },
                    new RunOptions
                    {
                        Prompts = PromptMode.Ask,
                        ShowsErrorDialog = false,
                        Undo = push is null ? UndoScope.None : UndoScope.Push(push.Branch, push.Remote, force: false),
                        Cancellation = cancellation.Token,
                    });
            }
            finally
            {
                _create = null;
                SetWindow(false);
            }

            // The queue ends a push's operation only once it has read the refs again (RefreshScope.Refs).
            if (result.Outcome != OperationOutcome.Succeeded)
            {
                await ComputePlaceAsync();
            }

            return result.Outcome switch
            {
                OperationOutcome.Succeeded => new CreatePullRequestOutcome(true, null, null),
                OperationOutcome.Cancelled => new CreatePullRequestOutcome(false, Texts.Cancelled, null),
                _ => new CreatePullRequestOutcome(false, words ?? GitWords(result, push, place), _existing?.Number),
            };
        }

        /// <summary>12.5 steps 3 to 5: the POST, the open pull requests from the head into the base noted before it, and what its answer means.</summary>
        private async Task<(OperationReport Report, string? Words)> PostAsync(
            OperationText text, PullRequestPlace place, HostedRepository baseRepository, PullRequestEnd baseEnd, string baseBranch, string title, string body, bool draft)
        {
            var listedBefore = OpenFromHeadIntoBase(place, baseRepository, baseBranch).Select(pull => pull.Number).ToHashSet();
            var sameRepository = string.Equals(place.Repository.FullName, baseRepository.FullName, StringComparison.OrdinalIgnoreCase);
            var request = new NewPullRequest(title, body, place.Branch, sameRepository ? null : place.Repository.Owner, baseBranch, draft);
            var answer = await Repository.Hosting.CreatePullRequestAsync(baseRepository, request, CancellationToken.None);
            if (answer.Value is { } created)
            {
                return (await CreatedAsync(text, baseRepository, created, draft), null);
            }

            var failure = answer.Failure ?? new HostingFailure(HostingFailureKind.Malformed, string.Empty);
            Log.Warning($"{Repository.Name}: {text.Title}: the request to create it failed: {failure.Kind}, {failure.Message}"
                + (failure.RequestId is { } id ? $" (request {id})" : string.Empty));
            string words;
            if (failure.Kind == HostingFailureKind.Validation && failure.Detail?.StartsWith("A pull request already exists", StringComparison.Ordinal) == true)
            {
                // The list read again (D316): the one open from this head into this base, or GitHub's words.
                await RefreshAsync();
                _existing = OpenFromHeadIntoBase(place, baseRepository, baseBranch).FirstOrDefault();
                words = _existing is { } open ? Texts.AlreadyOpen(open.Number, HeadText, baseEnd.Text) : GitHubSaid(failure);
            }
            else if (failure.Kind is HostingFailureKind.Network or HostingFailureKind.ServerError)
            {
                // The POST may have arrived (D316): one from the head into the base not listed before counts as created.
                await RefreshAsync();
                if (OpenFromHeadIntoBase(place, baseRepository, baseBranch).FirstOrDefault(pull => !listedBefore.Contains(pull.Number)) is { } found)
                {
                    return (await CreatedAsync(text, baseRepository, found, found.IsDraft), null);
                }

                words = HostingTexts.Describe(failure, Login);
            }
            else
            {
                words = Describe(failure, place, baseRepository, baseEnd, baseBranch, sameRepository);
            }

            return (RepositoryOperations.Failed(text, StoppedFailure(words)), words);
        }

        /// <summary>12.5 step 4: the list read when the base is the listed repository, then the report with the offer (D317; the browser never opens by itself, ruling 13).</summary>
        private async Task<OperationReport> CreatedAsync(OperationText text, HostedRepository baseRepository, PullRequest created, bool draft)
        {
            Log.Info($"{Repository.Name}: {text.Title}: created #{created.Number.ToString(CultureInfo.InvariantCulture)} in {baseRepository.FullName}.");
            if (Repository.Hosting.State.Repository is { } listed && string.Equals(listed.FullName, baseRepository.FullName, StringComparison.OrdinalIgnoreCase))
            {
                await RefreshAsync();
            }

            return OperationReport.Done(Texts.Created(created.Number, draft)) with
            {
                Offer = new OperationOffer(Texts.OpenInBrowser, () => operations.OpenPullRequestAsync(created)),
            };
        }

        /// <summary>12.6's rows for a failure that has one of its own; else <see cref="HostingTexts.Describe"/> (D338).</summary>
        private string Describe(HostingFailure failure, PullRequestPlace place, HostedRepository baseRepository, PullRequestEnd baseEnd, string baseBranch, bool sameRepository) =>
            failure.Kind switch
            {
                HostingFailureKind.Validation when failure.Detail?.StartsWith("No commits between", StringComparison.Ordinal) == true
                    => Texts.NoCommits(baseEnd.Text, HeadText),
                HostingFailureKind.Validation when string.Equals(failure.Detail, "head is invalid", StringComparison.Ordinal)
                    => Texts.HeadNotAccepted($"{place.Repository.Owner}:{place.Branch}", baseRepository.FullName),
                HostingFailureKind.Validation when string.Equals(failure.Detail, "base is invalid", StringComparison.Ordinal)
                    => Texts.BaseUnknown(baseBranch, baseRepository.FullName),
                HostingFailureKind.Validation => GitHubSaid(failure),
                HostingFailureKind.Forbidden when !sameRepository => Texts.ForkTokenRefused(baseRepository.FullName),
                HostingFailureKind.Forbidden => Texts.Refused(baseRepository.FullName, failure.Detail is not null),
                HostingFailureKind.NotFound => Texts.NotVisible(baseRepository.FullName),
                _ => HostingTexts.Describe(failure, Login),
            };

        /// <summary>GitHub's words (ruling 15): "{Message}: {Detail}", or the message alone.</summary>
        private static string GitHubSaid(HostingFailure failure) =>
            failure.Detail is { Length: > 0 } detail && !string.Equals(detail, failure.Message, StringComparison.Ordinal)
                ? $"{failure.Message}: {detail}"
                : failure.Message;

        /// <summary>A git failure of the push (12.5's "Afterwards"): rejected, git's credentials refused (D276, D330), or git's first line.</summary>
        private string GitWords(OperationResult result, PushRequest? push, PullRequestPlace place)
        {
            var failure = result.Failure;
            if (push is null || failure is null)
            {
                return failure?.Summary ?? Texts.Operation(HeadText).FailureSummary;
            }

            if (failure.Kind is GitFailureKind.PushRejected or GitFailureKind.PushRejectedStale)
            {
                return OperationTexts.PushRejected(push.Remote, push.Branch);
            }

            if (failure.Kind == GitFailureKind.AuthenticationFailed && result.RefusedAccount is { } refused)
            {
                return OperationTexts.GitCredentialsRefused(LoginOf(refused));
            }

            var line = failure.GitOutput.Split('\n').Select(part => part.Trim()).FirstOrDefault(part => part.Length > 0 && !PushReportLine().IsMatch(part)) ?? failure.Summary;
            return Texts.PushFailed(push.Branch, place.Target, line);
        }

        /// <summary>
        /// The lines a push writes whatever its outcome, which say nothing of a failure: the pack's
        /// (<c>Enumerating objects: 4, done.</c>, <c>Delta compression using up to 8 threads</c>,
        /// <c>Total 3 (delta 0), …</c>, which the progress filter keeps, having no percentage) and
        /// the report's <c>To {url}</c>.
        /// </summary>
        [GeneratedRegex(@"^(?:Enumerating objects: |Delta compression using |Total [0-9]+ \(delta |To \S+$)", RegexOptions.CultureInvariant)]
        private static partial Regex PushReportLine();

        private string LoginOf(AccountKey key) =>
            operations.Services?.Hosting.Account?.Account is { } account && account.Key.Equals(key) ? account.Login : key.Login;

        private async Task RefreshAsync()
        {
            try
            {
                await Repository.Hosting.RefreshPullRequestsAsync(CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Warning($"{Repository.Name}: reading the pull requests after a create failed.", ex);
            }
        }

        private IEnumerable<PullRequest> OpenFromHeadIntoBase(PullRequestPlace place, HostedRepository baseRepository, string baseBranch) =>
            Repository.Hosting.State.List?.PullRequests.Where(pull =>
                string.Equals(pull.HeadRef, place.Branch, StringComparison.Ordinal)
                && pull.HeadRepository is { } headRepository
                && string.Equals(headRepository.FullName, place.Repository.FullName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pull.BaseRef, baseBranch, StringComparison.Ordinal)
                && string.Equals(pull.BaseRepository.FullName, baseRepository.FullName, StringComparison.OrdinalIgnoreCase)) ?? [];

        /// <summary>
        /// 12.3's base options (D318): every remote branch of a GitHub remote that fetches a branch,
        /// in the refs' order, and the listed repository's default branch as <c>owner:branch</c> when
        /// no GitHub remote is that repository.
        /// </summary>
        private async Task<IReadOnlyList<PullRequestEnd>> BaseOptionsAsync()
        {
            var hosting = Repository.HostingState;
            var options = new List<PullRequestEnd>();
            if (operations.Refs is { } refs)
            {
                foreach (var remoteBranch in refs.Refs.Where(other => other.Kind == RefKind.RemoteBranch))
                {
                    if (RemoteBranchEnd(refs, hosting, remoteBranch) is { } end)
                    {
                        options.Add(end);
                    }
                }
            }

            if (hosting.Repository is { } listed
                && !hosting.Remotes.Values.Any(repository => string.Equals(repository.FullName, listed.FullName, StringComparison.OrdinalIgnoreCase)))
            {
                var read = await Repository.Hosting.ReadRepositoryAsync(CancellationToken.None);
                if (read.Value is { } info)
                {
                    var branch = info.Parent is not null ? info.ParentDefaultBranch ?? info.DefaultBranch : info.DefaultBranch;
                    options.Add(new PullRequestEnd($"{listed.Owner}:{branch}", listed.FullName) { Branch = branch });
                }
            }

            return options;
        }

        private static PullRequestEnd? RemoteBranchEnd(RepoRefs refs, RepositoryHostingState hosting, GitRef remoteBranch) =>
            refs.SourceOf(remoteBranch) is { IsBranch: true, Remote: { } remote, Branch: { } branch } && hosting.Remotes.TryGetValue(remote, out var repository)
                ? new PullRequestEnd(remoteBranch.Name, repository.FullName) { Branch = branch }
                : null;

        /// <summary>A3's default (12.3): the remote branch of the remote that is <paramref name="repository"/> when the refs hold it, else <c>owner:branch</c>.</summary>
        private PullRequestEnd DefaultEnd(HostedRepository repository, string branch)
        {
            var hosting = Repository.HostingState;
            if (operations.Refs is { } refs)
            {
                foreach (var remote in HostingRemoteOrder.Of(hosting.Remotes.Keys))
                {
                    if (!string.Equals(hosting.Remotes[remote].FullName, repository.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var held = refs.Refs.FirstOrDefault(other => other.Kind == RefKind.RemoteBranch
                        && refs.SourceOf(other) is { IsBranch: true } source
                        && string.Equals(source.Remote, remote, StringComparison.Ordinal)
                        && string.Equals(source.Branch, branch, StringComparison.Ordinal));
                    if (held is not null)
                    {
                        return new PullRequestEnd(held.Name, repository.FullName) { Branch = branch };
                    }
                }
            }

            return new PullRequestEnd($"{repository.Owner}:{branch}", repository.FullName) { Branch = branch };
        }

        /// <summary>The repository of a base's full name: a GitHub remote's, or the listed one (a fork's parent).</summary>
        private HostedRepository? RepositoryOf(string fullName)
        {
            var hosting = Repository.HostingState;
            return hosting.Remotes.Values.Append(hosting.Repository).OfType<HostedRepository>()
                .FirstOrDefault(repository => string.Equals(repository.FullName, fullName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>An upstream's name as its remote branch is called (<c>origin/feature</c>).</summary>
        private string UpstreamName(GitRef local, string remote) =>
            (local.UpstreamRef is { } upstreamRef ? operations.FindRef(upstreamRef)?.Name : null) ?? local.Upstream ?? $"{remote}/{local.Name}";

        /// <summary>The create's cancel window (ruling 59), raised on the UI thread, where the form and the operation's run are.</summary>
        private void SetWindow(bool open)
        {
            if (_window == open)
            {
                return;
            }

            _window = open;
            CanCancelCreateChanged?.Invoke(this, EventArgs.Empty);
        }

        private static GitFailure StoppedFailure(string summary) => new(GitFailureKind.Other, summary, string.Empty, string.Empty, null);
    }
}
