// A new pull request (docs/phase-7a-design.md 1.7, section 12; D251, D257, D312 to D318, D338):
// the form of a branch's or a drop's "Create pull request…", then the push when one is needed and
// the create, as one network operation of the repository's queue. The form never runs git or HTTP
// itself: PullRequestForm does, on the UI thread. Since docs/phase-7b-design.md 13 for the listed
// repository's provider, in its words and numbers (D351, D355): head and base among the remotes
// where a request can go (RequestRemotes), compared as records, and a refusal read from
// HostingFailure.Refusal, never from a service's sentence.

using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Panels;
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
    /// the base. The form's answer is not used. 7b (13.3): <c>p</c> is the listed repository's
    /// provider; nothing starts while <c>OtherOrganization</c> (D356), and signed out that
    /// provider's sign-in comes first (<c>Services.SignIn(p)</c>).
    /// </remarks>
    public Task CreatePullRequestAsync(string headRef, string? baseRef)
    {
        ArgumentNullException.ThrowIfNull(headRef);
        var p = Repository.HostingState.Repository?.Provider ?? HostingProvider.GitHub;
        return GuardAsync(Texts.Operation(p, ShortName(headRef)), async () =>
        {
            if (IsClosed)
            {
                return;
            }

            var title = Texts.Operation(p, ShortName(headRef)).Title;
            switch (Repository.HostingState.PullRequests)
            {
                case PullRequestsState.NoHostedRemote:
                    Log.Info($"{Repository.Name}: {title}: not started, the repository has no hosted remote.");
                    return;
                case PullRequestsState.OtherOrganization:
                    Log.Info($"{Repository.Name}: {title}: not started, the signed-in account is in another organization.");
                    return;
            }

            if (Repository.HostingState.PullRequests == PullRequestsState.SignedOut
                && (Services?.SignIn is not { } signIn || !await signIn(p)))
            {
                return;
            }

            // A sign-in to another organization of Azure DevOps leaves no remote where a request can go (D350, D356).
            // Read from the session's account now, by the rule the hosting's state follows (Covers): the
            // account's change reaches the hosting, and then the view model's copy, only through posts
            // that have not run yet when the sign-in returns.
            if (Repository.Hosting.State.PullRequests == PullRequestsState.OtherOrganization || InAnotherOrganization(p))
            {
                Log.Info($"{Repository.Name}: {title}: not started, the signed-in account is in another organization.");
                return;
            }

            if (IsClosed || Refs is not { } refs || PullRequestForm.HeadOf(this, refs, headRef) is not { } head)
            {
                Log.Info($"{Repository.Name}: {title}: not started, it is no branch {HostingTexts.Name(p)} can hold.");
                return;
            }

            var form = new PullRequestForm(this, head, p);
            await form.ComputePlaceAsync();
            form.DefaultTitle = await ReadSubjectAsync(head.TargetSha) ?? string.Empty;
            if (baseRef is not null)
            {
                form.GivenBase = form.BaseOf(refs, baseRef);
                if (form.GivenBase is null)
                {
                    Log.Info($"{Repository.Name}: {Texts.Operation(p, form.Head.Text).Title}: not started, {ShortName(baseRef)} is no branch on {HostingTexts.Name(p)}.");
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
    /// D356 as the hosting's state decides it (<c>OtherOrganization</c>), from the session's account
    /// now: Azure DevOps' account held does not cover the listed repository.
    /// </summary>
    private bool InAnotherOrganization(HostingProvider p) =>
        p == HostingProvider.AzureDevOps
        && Repository.HostingState.Repository is { } listed
        && Services?.Hosting is { } hosting
        && hosting.AccountOf(p) is { } account
        && hosting.ProviderOf(p) is { } provider
        && !provider.Covers(listed, account.Account.Key);

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
    private sealed partial class PullRequestForm(RepositoryOperations operations, GitRef head, HostingProvider provider) : ICreatePullRequestActions
    {
        private PullRequestPlace? _place;
        private PullRequest? _existing;
        private CancellationTokenSource? _create;
        private bool _window;

        public PullRequestEnd Head => new(head.Name, _place?.Repository.FullName ?? string.Empty) { Branch = _place?.Branch, Hosted = _place?.Repository };

        /// <summary>The listed repository's provider when the form opened (13.4).</summary>
        public HostingProvider Provider => provider;

        public string DefaultTitle { get; set; } = string.Empty;

        public PullRequestEnd? GivenBase { get; set; }

        public bool CanCancelCreate => _window;

        public event EventHandler? CanCancelCreateChanged;

        private RepositoryViewModel Repository => operations.Repository;

        private IAppLog Log => operations.Log;

        /// <summary>The head as the form and the operation's title name it: a local branch's name, a remote branch's ref name (the report's ruling 48).</summary>
        private string HeadText => head.Name;

        private string? Login => operations.Services?.Hosting.AccountOf(provider)?.Account.Login;

        /// <summary>
        /// The head from the refs now (12.3 step 2): a local branch, or a remote branch of a remote
        /// that is GitHub's whose fetch line maps it from a branch of that remote; null otherwise.
        /// 7b: a remote where a request can go (<c>RequestRemotes</c>, 13.3).
        /// </summary>
        public static GitRef? HeadOf(RepositoryOperations operations, RepoRefs refs, string headRef) =>
            operations.FindRef(headRef) switch
            {
                { Kind: RefKind.LocalBranch } local => local,
                { Kind: RefKind.RemoteBranch, RemoteName: { } remote } remoteBranch
                    when operations.Repository.HostingState.RequestRemotes.ContainsKey(remote)
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
            var remotes = Repository.HostingState.RequestRemotes;
            return operations.FindRef(baseRef) switch
            {
                { Kind: RefKind.RemoteBranch } remoteBranch => RemoteBranchEnd(refs, remotes, remoteBranch),
                { Kind: RefKind.LocalBranch, UpstreamRemote: { } remote, UpstreamRemoteRef: { } remoteRef } local
                    when remotes.TryGetValue(remote, out var repository) && string.Equals(remoteRef, HeadsPrefix + local.Name, StringComparison.Ordinal)
                    => new PullRequestEnd(UpstreamName(local, remote), repository.FullName) { Branch = local.Name, Hosted = repository },
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

            var remotes = Repository.HostingState.RequestRemotes;
            var current = operations.FindRef(head.FullName) ?? head;
            if (current.Kind == RefKind.RemoteBranch)
            {
                // A remote branch is never pushed.
                if (refs.SourceOf(current) is { IsBranch: true, Remote: { } remote, Branch: { } branch } && remotes.TryGetValue(remote, out var repository))
                {
                    _place = new PullRequestPlace(repository, branch, null, current.Name, null);
                }

                return;
            }

            var name = current.Name;
            if (current.UpstreamRemote is { } upstreamRemote
                && remotes.TryGetValue(upstreamRemote, out var upstreamRepository)
                && string.Equals(current.UpstreamRemoteRef, HeadsPrefix + name, StringComparison.Ordinal))
            {
                // An upstream of the same name on GitHub: pushed to it, without --set-upstream, when ahead or gone.
                var target = UpstreamName(current, upstreamRemote);
                var push = current.Ahead > 0 || current.UpstreamGone ? new PushRequest(upstreamRemote, name, current.UpstreamRemoteRef!) : null;
                var note = current.UpstreamGone
                    ? Texts.GonePushedFirst(target)
                    : current.Ahead > 0 ? Texts.AheadPushedFirst(provider, current.Ahead, target) : null;
                _place = new PullRequestPlace(upstreamRepository, name, push, target, note);
                return;
            }

            // Any other local branch: the first remote where a request can go in HostingRemoteOrder, under its own name.
            if (HostingRemoteOrder.Of(remotes.Keys).FirstOrDefault() is not { } first)
            {
                return;
            }

            var firstRepository = remotes[first];
            var firstTarget = $"{first}/{name}";
            var request = new PushRequest(first, name, HeadsPrefix + name);
            if (current.UpstreamRef is null)
            {
                _place = new PullRequestPlace(firstRepository, name, request with { SetUpstream = true }, firstTarget, Texts.PushedFirstWithUpstream(provider, firstTarget));
                return;
            }

            // With an upstream elsewhere, left as it is: pushed only when GitHub lacks the branch's commits.
            var onGitHub = refs.Refs.FirstOrDefault(other => other.Kind == RefKind.RemoteBranch
                && refs.SourceOf(other) is { IsBranch: true } source
                && string.Equals(source.Remote, first, StringComparison.Ordinal)
                && string.Equals(source.Branch, name, StringComparison.Ordinal));
            if (onGitHub is null)
            {
                _place = new PullRequestPlace(firstRepository, name, request, firstTarget, Texts.PushedFirst(provider, firstTarget));
                return;
            }

            var count = string.Equals(onGitHub.TargetSha, current.TargetSha, StringComparison.OrdinalIgnoreCase)
                ? 0
                : await operations.Git.CountCommitsAsync(onGitHub.FullName, current.FullName);
            _place = count > 0
                ? new PullRequestPlace(firstRepository, name, request, onGitHub.Name, Texts.AheadPushedFirst(provider, count, onGitHub.Name))
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

        /// <summary>Head and base one branch of one repository (D318), the repositories compared as records (13.3).</summary>
        public bool IsSameBranch(PullRequestEnd baseEnd)
        {
            ArgumentNullException.ThrowIfNull(baseEnd);
            return _place is { } place
                && place.Repository.Equals(baseEnd.Hosted)
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
            var text = Texts.Operation(provider, HeadText);
            await ComputePlaceAsync();
            if (_place is not { } place || RepositoryOf(baseEnd.Hosted) is not { } baseRepository)
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

                        context.SetRunningText(Texts.CreatingRequest(provider));
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
                OperationOutcome.Cancelled => new CreatePullRequestOutcome(false, Texts.CancelledOf(provider), null),
                _ => new CreatePullRequestOutcome(false, words ?? GitWords(result, push, place), _existing?.Number),
            };
        }

        /// <summary>
        /// 12.5 steps 3 to 5: the POST, the open pull requests from the head into the base noted
        /// before it, and what its answer means. 7b (13.5): a head in another repository goes as
        /// <see cref="NewPullRequest.HeadRepository"/> too (GitHub reads <c>HeadOwner</c>, GitLab
        /// posts to the source project, Azure DevOps sends <c>forkSource</c>); a refusal is read
        /// from <see cref="HostingFailure.Refusal"/>, then its kind, never from its words.
        /// </summary>
        private async Task<(OperationReport Report, string? Words)> PostAsync(
            OperationText text, PullRequestPlace place, HostedRepository baseRepository, PullRequestEnd baseEnd, string baseBranch, string title, string body, bool draft)
        {
            // D316's re-read tells a request this POST made from one open before it only against a
            // list held before the POST: with none held (its read failed or has not ended), it is read
            // first; still none, a request found after a lost answer is offered as open, never as created.
            if (Repository.Hosting.State.List is null)
            {
                await RefreshAsync();
            }

            var known = Repository.Hosting.State.List is not null;
            var listedBefore = OpenFromHeadIntoBase(place, baseRepository, baseBranch).Select(pull => pull.Number).ToHashSet();
            var sameRepository = place.Repository.Equals(baseRepository);
            var request = new NewPullRequest(title, body, place.Branch, sameRepository ? null : place.Repository.Owner, baseBranch, draft)
            {
                HeadRepository = sameRepository ? null : place.Repository,
            };
            var answer = await Repository.Hosting.CreatePullRequestAsync(baseRepository, request, CancellationToken.None);
            if (answer.Value is { } created)
            {
                // GitHub's draft is the box's (7a's); elsewhere the answer's: a GitLab title typed "Draft: x" is a draft.
                return (await CreatedAsync(text, baseRepository, created, provider == HostingProvider.GitHub ? draft : created.IsDraft), null);
            }

            var failure = answer.Failure ?? new HostingFailure(HostingFailureKind.Malformed, string.Empty);
            Log.Warning($"{Repository.Name}: {text.Title}: the request to create it failed: {failure.Kind}, {failure.Message}"
                + (failure.RequestId is { } id ? $" (request {id})" : string.Empty));
            string words;
            if (failure.Refusal == PullRequestRefusal.AlreadyExists)
            {
                // The list read again (D316): the one open from this head into this base, or the provider's words.
                await RefreshAsync();
                _existing = OpenFromHeadIntoBase(place, baseRepository, baseBranch).FirstOrDefault();
                words = _existing is { } open ? Texts.AlreadyOpen(provider, open.Number, HeadText, baseEnd.Text) : ProviderSaid(failure);
            }
            else if (failure.Kind is HostingFailureKind.Network or HostingFailureKind.ServerError)
            {
                // The POST may have arrived (D316): one from the head into the base not listed before counts as created;
                // with no list before the POST, one found now may be older than it, so it is only offered as open.
                await RefreshAsync();
                var open = OpenFromHeadIntoBase(place, baseRepository, baseBranch).ToList();
                if (known && open.FirstOrDefault(pull => !listedBefore.Contains(pull.Number)) is { } found)
                {
                    return (await CreatedAsync(text, baseRepository, found, found.IsDraft), null);
                }

                _existing = known ? null : open.FirstOrDefault();
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
            Log.Info($"{Repository.Name}: {text.Title}: created {HostingTexts.Number(provider, created.Number)} in {baseRepository.FullName}.");
            if (Repository.Hosting.State.Repository is { } listed && listed.Equals(baseRepository))
            {
                await RefreshAsync();
            }

            return OperationReport.Done(Texts.Created(provider, created.Number, draft)) with
            {
                Offer = new OperationOffer(Texts.OpenInBrowser, () => operations.OpenPullRequestAsync(created)),
            };
        }

        /// <summary>
        /// 12.6's rows for a failure that has one of its own, by its <see cref="HostingFailure.Refusal"/>
        /// and then its kind (13.5's table); else <see cref="HostingTexts.Describe"/> (D338). No arm
        /// reads the failure's words, which are only shown.
        /// </summary>
        private string Describe(HostingFailure failure, PullRequestPlace place, HostedRepository baseRepository, PullRequestEnd baseEnd, string baseBranch, bool sameRepository)
        {
            var gitHub = provider == HostingProvider.GitHub;

            // Across repositories GitLab and Azure DevOps read the head's repository first and return
            // that read's 403 or 404 as it came (GitLab also posts to the head's project): which of the
            // two failed is not told, so both are named, the head's first.
            var named = gitHub || sameRepository ? baseRepository.FullName : $"{place.Repository.FullName} or {baseRepository.FullName}";
            return failure.Refusal switch
            {
                PullRequestRefusal.NoCommits => Texts.NoCommits(baseEnd.Text, HeadText),
                PullRequestRefusal.HeadMissing when gitHub
                    => Texts.HeadNotAccepted(PullRequestRow.BranchLabelOf(place.Repository, place.Branch, baseRepository), baseRepository.FullName),
                PullRequestRefusal.HeadMissing => Texts.HeadUnknown(provider, place.Branch, place.Repository.FullName),
                PullRequestRefusal.BaseMissing => Texts.BaseUnknown(provider, baseBranch, baseRepository.FullName),
                PullRequestRefusal.BranchesMissing => Texts.BranchesUnknown(provider, place.Branch, baseBranch),
                _ => failure.Kind switch
                {
                    HostingFailureKind.Validation => ProviderSaid(failure),
                    HostingFailureKind.Forbidden when gitHub && !sameRepository => Texts.ForkTokenRefused(baseRepository.FullName),
                    HostingFailureKind.Forbidden => Texts.Refused(provider, named, failure.Detail is not null),
                    HostingFailureKind.NotFound => Texts.NotVisible(provider, named),
                    _ => HostingTexts.Describe(failure, Login),
                },
            };
        }

        /// <summary>The provider's words (ruling 15; 7a's GitHub's): "{Message}: {Detail}", or the message alone.</summary>
        private static string ProviderSaid(HostingFailure failure) =>
            failure.Detail is { Length: > 0 } detail && !string.Equals(detail, failure.Message, StringComparison.Ordinal)
                ? $"{failure.Message}: {detail}"
                : failure.Message;

        /// <summary>A git failure of the push (12.5's "Afterwards"): rejected, git's credentials refused (D276, D330), or git's first line.</summary>
        private string GitWords(OperationResult result, PushRequest? push, PullRequestPlace place)
        {
            var failure = result.Failure;
            if (push is null || failure is null)
            {
                return failure?.Summary ?? Texts.Operation(provider, HeadText).FailureSummary;
            }

            if (failure.Kind is GitFailureKind.PushRejected or GitFailureKind.PushRejectedStale)
            {
                return OperationTexts.PushRejected(push.Remote, push.Branch);
            }

            if (failure.Kind == GitFailureKind.AuthenticationFailed && result.RefusedAccount is { } refused)
            {
                return OperationTexts.GitCredentialsRefused(refused.Provider, LoginOf(refused));
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
            operations.Services?.Hosting.AccountOf(key.Provider)?.Account is { } account && account.Key.Equals(key) ? account.Login : key.Login;

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
                && place.Repository.Equals(pull.HeadRepository)
                && string.Equals(pull.BaseRef, baseBranch, StringComparison.Ordinal)
                && baseRepository.Equals(pull.BaseRepository)) ?? [];

        /// <summary>
        /// 12.3's base options (D318): every remote branch of a GitHub remote that fetches a branch,
        /// in the refs' order, and the listed repository's default branch as <c>owner:branch</c> when
        /// no GitHub remote is that repository. 7b: the remotes where a request can go
        /// (<c>RequestRemotes</c>), the label WP-P's <see cref="PullRequestRow.BranchLabelOf"/>.
        /// </summary>
        private async Task<IReadOnlyList<PullRequestEnd>> BaseOptionsAsync()
        {
            var hosting = Repository.HostingState;
            var remotes = hosting.RequestRemotes;
            var options = new List<PullRequestEnd>();
            if (operations.Refs is { } refs)
            {
                foreach (var remoteBranch in refs.Refs.Where(other => other.Kind == RefKind.RemoteBranch))
                {
                    if (RemoteBranchEnd(refs, remotes, remoteBranch) is { } end)
                    {
                        options.Add(end);
                    }
                }
            }

            if (hosting.Repository is { } listed && !remotes.Values.Any(listed.Equals))
            {
                var read = await Repository.Hosting.ReadRepositoryAsync(CancellationToken.None);
                if (read.Value is { } info)
                {
                    var branch = info.Parent is not null ? info.ParentDefaultBranch ?? info.DefaultBranch : info.DefaultBranch;
                    options.Add(new PullRequestEnd(PullRequestRow.BranchLabelOf(listed, branch, listed), listed.FullName) { Branch = branch, Hosted = listed });
                }
            }

            return options;
        }

        private static PullRequestEnd? RemoteBranchEnd(RepoRefs refs, IReadOnlyDictionary<string, HostedRepository> remotes, GitRef remoteBranch) =>
            refs.SourceOf(remoteBranch) is { IsBranch: true, Remote: { } remote, Branch: { } branch } && remotes.TryGetValue(remote, out var repository)
                ? new PullRequestEnd(remoteBranch.Name, repository.FullName) { Branch = branch, Hosted = repository }
                : null;

        /// <summary>A3's default (12.3): the remote branch of the remote that is <paramref name="repository"/> when the refs hold it, else <c>owner:branch</c>.</summary>
        private PullRequestEnd DefaultEnd(HostedRepository repository, string branch)
        {
            var remotes = Repository.HostingState.RequestRemotes;
            if (operations.Refs is { } refs)
            {
                foreach (var remote in HostingRemoteOrder.Of(remotes.Keys))
                {
                    if (!remotes[remote].Equals(repository))
                    {
                        continue;
                    }

                    var held = refs.Refs.FirstOrDefault(other => other.Kind == RefKind.RemoteBranch
                        && refs.SourceOf(other) is { IsBranch: true } source
                        && string.Equals(source.Remote, remote, StringComparison.Ordinal)
                        && string.Equals(source.Branch, branch, StringComparison.Ordinal));
                    if (held is not null)
                    {
                        return new PullRequestEnd(held.Name, repository.FullName) { Branch = branch, Hosted = repository };
                    }
                }
            }

            return new PullRequestEnd(PullRequestRow.BranchLabelOf(repository, branch, repository), repository.FullName) { Branch = branch, Hosted = repository };
        }

        /// <summary>A base's repository while it is still a remote where a request can go, or the listed one (a fork's parent); compared as records (13.3).</summary>
        private HostedRepository? RepositoryOf(HostedRepository? hosted)
        {
            var hosting = Repository.HostingState;
            return hosted is null
                ? null
                : hosting.RequestRemotes.Values.Append(hosting.Repository).OfType<HostedRepository>().FirstOrDefault(hosted.Equals);
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
