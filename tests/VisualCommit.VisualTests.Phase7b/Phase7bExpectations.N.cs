using System.Globalization;
using VisualCommit.Core.Hosting;

namespace VisualCommit.VisualTests.Phase7b;

public static partial class Phase7bExpectations
{
    /// <summary>
    /// Part N, a new pull or merge request (docs/test-reports/phase-7b.md, part N: its conventions,
    /// scenario facts and texts; design 13.2 to 13.8), written from the report and not from the
    /// App's texts: 13.2's texts per provider and 7a's that part N keeps, the menus' lines per
    /// provider, the heads, bases and titles, the new numbers, the create's routes and bodies, the
    /// log lines and the picture names. For WP-R (13.8) one record per provider,
    /// <see cref="GitLab"/> and <see cref="AzureDevOps"/>, of the R steps' state: the prepared
    /// <c>topic/no-request</c>'s form and the status after creating from it, which pushes nothing
    /// (open point 35). Compiled by the real-window pass too: it uses nothing of the 7b project.
    /// </summary>
    public static class PartN
    {
        // ---- 13.2's texts: GitLab's, Azure DevOps' (GitHub's are 7a's part N texts) -------------------

        public const string GitLabDialogTitle = "Create merge request";
        public const string AzureDevOpsDialogTitle = "Create pull request";

        public const string GitLabReading = "Reading GitLab…";
        public const string AzureDevOpsReading = "Reading Azure DevOps…";

        public const string GitLabCreating = "Creating merge request…";
        public const string AzureDevOpsCreating = "Creating pull request…";

        public const string GitLabCreateItem = "Create merge request…";
        public const string AzureDevOpsCreateItem = "Create pull request…";

        public const string GitLabErrorTitle = "Create merge request failed";
        public const string AzureDevOpsErrorTitle = "Create pull request failed";

        public const string GitLabFailureSummary = "GitLab could not create the merge request.";
        public const string AzureDevOpsFailureSummary = "Azure DevOps could not create the pull request.";

        public const string GitLabCancelled = "Cancelled before the merge request was created.";
        public const string AzureDevOpsCancelled = "Cancelled before the pull request was created.";

        public const string QuickActionsNote = "GitLab runs quick actions in the description, such as /close.";

        public const string GitLabRefused = "GitLab refused to create a merge request in northfield/platform/web-app with this token.";
        public const string GitLabRefusedScope = " It needs the api scope, or Merge Request: Create.";
        public const string AzureDevOpsRefused = "Azure DevOps refused to create a pull request in Fernhill-Labs/Mobile App/mobile-app with this token.";

        public const string GitLabHeadUnknown = "GitLab has no branch topic/no-request in northfield/platform/web-app to merge from. Fetch, then try again.";
        public const string AzureDevOpsBranchesUnknown = "Azure DevOps cannot find topic/no-request or main. Fetch, then try again.";

        public static string DialogTitleOf(HostingProvider p) => IsGitLab(p) ? GitLabDialogTitle : AzureDevOpsDialogTitle;

        public static string ReadingOf(HostingProvider p) => IsGitLab(p) ? GitLabReading : AzureDevOpsReading;

        public static string CreatingOf(HostingProvider p) => IsGitLab(p) ? GitLabCreating : AzureDevOpsCreating;

        public static string CreateItemOf(HostingProvider p) => IsGitLab(p) ? GitLabCreateItem : AzureDevOpsCreateItem;

        public static string CancelledOf(HostingProvider p) => IsGitLab(p) ? GitLabCancelled : AzureDevOpsCancelled;

        /// <summary><c>Create merge request for {head}</c>, its running text, error title and summary.</summary>
        public static (string Title, string Running, string ErrorTitle, string Summary) OperationOf(HostingProvider p, string head) =>
            IsGitLab(p)
                ? ($"Create merge request for {head}", GitLabCreating, GitLabErrorTitle, GitLabFailureSummary)
                : ($"Create pull request for {head}", AzureDevOpsCreating, AzureDevOpsErrorTitle, AzureDevOpsFailureSummary);

        public static string NumberOf(int number) => "!" + number.ToString(CultureInfo.InvariantCulture);

        /// <summary><c>Created merge request !24</c>, <c>Created draft pull request !14</c>.</summary>
        public static string CreatedOf(HostingProvider p, int number, bool draft) =>
            $"Created {(draft ? "draft " : string.Empty)}{(IsGitLab(p) ? "merge request" : "pull request")} {NumberOf(number)}";

        /// <summary><c>Open merge request !24</c>, <c>Open pull request !14</c> (the form's button and the branch menu's item).</summary>
        public static string OpenExistingOf(HostingProvider p, int number) => $"Open {(IsGitLab(p) ? "merge request" : "pull request")} {NumberOf(number)}";

        /// <summary><c>Merge request !24 is already open from topic/no-request into origin/main.</c></summary>
        public static string AlreadyOpenOf(HostingProvider p, int number) =>
            $"{(IsGitLab(p) ? "Merge request" : "Pull request")} {NumberOf(number)} is already open from {Head} into {Base}.";

        /// <summary><c>Not on GitLab yet: Create pushes it to origin/local-work first and sets that as its upstream.</c></summary>
        public static string PushedFirstWithUpstreamOf(HostingProvider p) =>
            $"Not on {(IsGitLab(p) ? "GitLab" : "Azure DevOps")} yet: Create pushes it to origin/local-work first and sets that as its upstream.";

        /// <summary>GitLab's sent title under Draft: <c>Title on GitLab: Draft: Local change</c>.</summary>
        public static string SentTitle(string title) => $"Title on GitLab: {title}";

        public const string LocalWorkSentTitle = "Title on GitLab: Draft: Local change";

        /// <summary>A branch row's tooltip of the new request: <c>Merge request !24: Try an idea</c>.</summary>
        public static string RequestOfBranch(HostingProvider p, int number, string title) =>
            $"{(IsGitLab(p) ? "Merge request" : "Pull request")} {NumberOf(number)}: {title}";

        // ---- 7a's texts part N keeps (13.2; 7a's 12.6) ------------------------------------------------

        public const string From = "From";
        public const string Into = "Into";
        public const string TitlePlaceholder = "Title";
        public const string DescriptionPlaceholder = "Description";
        public const string Draft = "Draft";
        public const string Create = "Create";
        public const string CreatingButton = "Creating…";
        public const string Choose = "Choose…";
        public const string ChooseBaseTitle = "Choose the branch to merge into";
        public const string Cancel = "Cancel";
        public const string OpenInBrowser = "Open in browser";
        public const string StopGit = "Stop git";
        public const string PushingLocalWork = "Pushing local-work to origin/local-work…";

        public static string FailedOf(string title) => $"{title} failed";

        public static string CheckedOut(string branch) => $"Checked out {branch}";

        // ---- The scenario's heads, bases, titles and numbers (part N's scenario facts) ----------------

        public const string Head = "topic/no-request";
        public const string HeadRef = "refs/heads/topic/no-request";
        public const string RemoteHead = "origin/topic/no-request";
        public const string RemoteHeadRef = "refs/remotes/origin/topic/no-request";
        public const string LocalWork = "local-work";
        public const string LocalWorkRef = "refs/heads/local-work";
        public const string Base = "origin/main";
        public const string BaseRef = "refs/remotes/origin/main";
        public const string HeadTitle = "Try an idea";
        public const string LocalWorkTitle = "Local change";
        public const string LocalWorkDraftTitle = "Draft: Local change";
        public const string Description = "A note for the reviewers.";
        public const string TopicFolder = "topic";
        public const string NoRequest = "no-request";

        public const int GitLabNext = 24;
        public const int AzureDevOpsNext = 14;
        public const int GitLabProjectId = 5001;

        /// <summary>The count listed after a create (the scenario's three and the new one).</summary>
        public const int CountAfterCreate = 4;

        /// <summary>The long repository of N7 (78 characters).</summary>
        public const string LongFullName = "Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service";

        public const string LongRemote = "long";

        /// <summary>N7's remote's URL before <c>WithPorts</c>.</summary>
        public const string LongUrl = "http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/customer-onboarding-and-identity-verification-service";

        public const string GitLabFullName = "northfield/platform/web-app";
        public const string AzureDevOpsFullName = "Fernhill-Labs/Mobile App/mobile-app";

        public static string FullNameOf(HostingProvider p) => IsGitLab(p) ? GitLabFullName : AzureDevOpsFullName;

        public static int NextOf(HostingProvider p) => IsGitLab(p) ? GitLabNext : AzureDevOpsNext;

        // ---- The create's routes, paths and log lines --------------------------------------------------

        public const string GitLabCreateRoute = "create:northfield/platform/web-app";
        public const string AzureDevOpsCreateRoute = "create:fernhill-labs/mobile app/mobile-app";
        public const string GitLabCreatePath = "/api/v4/projects/5001/merge_requests";
        public const string AzureDevOpsCreatePath = "/Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests";
        public const string AzureDevOpsCreateQuery = "api-version=7.1";
        public const string GitLabSourceRoute = "project:northfield/platform/web-app";
        public const string GitLabSourcePath = "/api/v4/projects/northfield%2Fplatform%2Fweb-app";
        public const string GitLabListRoute = "mrs:northfield/platform/web-app";
        public const string GitLabListQuery = "state=opened&per_page=100";
        public const string AzureDevOpsListRoute = "pulls:fernhill-labs/mobile app/mobile-app";
        public const string AzureDevOpsListQuery = "searchCriteria.status=active&api-version=7.1&$top=100&$skip=0";

        public static string CreateRouteOf(HostingProvider p) => IsGitLab(p) ? GitLabCreateRoute : AzureDevOpsCreateRoute;

        public static string ListRouteOf(HostingProvider p) => IsGitLab(p) ? GitLabListRoute : AzureDevOpsListRoute;

        /// <summary>D322's line for <paramref name="p"/>'s repository: <c>web-app: listed 4 open pull requests of northfield/platform/web-app</c>.</summary>
        public static string ListedOf(HostingProvider p, int count) =>
            string.Create(CultureInfo.InvariantCulture, $"{RepositoryOf(p)}: listed {count} open pull requests of {FullNameOf(p)}");

        /// <summary>WP-N's Info line before the POST: <c>web-app: Create merge request for topic/no-request: into northfield/platform/web-app main, draft no, no push</c>.</summary>
        public static string IntoLineOf(HostingProvider p, string head, bool draft, string? pushTarget) =>
            $"{RepositoryOf(p)}: {OperationOf(p, head).Title}: into {FullNameOf(p)} main, draft {(draft ? "yes" : "no")}, "
            + (pushTarget is null ? "no push" : $"push to {pushTarget} first");

        /// <summary>WP-N's Info line after a create: <c>web-app: Create merge request for topic/no-request: created !24 in northfield/platform/web-app.</c></summary>
        public static string CreatedLineOf(HostingProvider p, string head, int number) =>
            $"{RepositoryOf(p)}: {OperationOf(p, head).Title}: created {NumberOf(number)} in {FullNameOf(p)}.";

        public static string RepositoryOf(HostingProvider p) => IsGitLab(p) ? WebApp : MobileApp;

        /// <summary>The created request's address (through <c>WithPorts</c>).</summary>
        public static string WebUrlOf(HostingProvider p, int number) => IsGitLab(p)
            ? string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:47398/northfield/platform/web-app/-/merge_requests/{number}")
            : string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/{number}");

        // ---- The menus (12.4; 7a's N1, N5 and P12 with 7b's names) -------------------------------------

        /// <summary>Method 1's and 2's menu of the prepared <c>topic/no-request</c> (20 items, 5 separators, 597 high).</summary>
        public static string[] LocalBranchMenuOf(HostingProvider p) =>
        [
            Head,
            $"Check out {Head}",
            $"Merge {Head} into main",
            $"Merge {Head} into main (create a merge commit)",
            $"Rebase main onto {Head}",
            $"Interactive rebase of main onto {Head}…",
            $"Fast-forward {Head} to {RemoteHead} (disabled: \"{Head} is up to date with {RemoteHead}\")",
            "—",
            $"Push {Head} to {RemoteHead}",
            $"Force push {Head}… (danger)",
            CreateItemOf(p),
            "—",
            "Create branch here…",
            "Create tag here…",
            $"Rename {Head}…",
            $"Delete {Head}… (danger)",
            "—",
            "Change upstream…",
            $"Stop tracking {RemoteHead}",
            "—",
            $"Compare {Head} with main",
            $"Show reflog of {Head}",
            $"Hide {Head}",
            $"Solo {Head}",
            "—",
            "Copy branch name",
        ];

        public const double LocalBranchMenuHeight = 597;

        /// <summary>Method 3's and 4's menu of <c>origin/topic/no-request</c> (13 items, 5 separators, 415 high).</summary>
        public static string[] RemoteBranchMenuOf(HostingProvider p) =>
        [
            RemoteHead,
            $"Check out {RemoteHead}",
            $"Merge {RemoteHead} into main",
            $"Merge {RemoteHead} into main (create a merge commit)",
            $"Rebase main onto {RemoteHead}",
            $"Interactive rebase of main onto {RemoteHead}…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            $"Delete {Head} on origin… (danger)",
            "—",
            CreateItemOf(p),
            "—",
            $"Compare {RemoteHead} with main",
            $"Hide {RemoteHead}",
            $"Solo {RemoteHead}",
            "—",
            "Copy branch name",
        ];

        public const double RemoteBranchMenuHeight = 415;

        /// <summary><c>local-work</c>'s menu (17 items, 5 separators, 519 high).</summary>
        public static string[] LocalWorkMenuOf(HostingProvider p) =>
        [
            LocalWork,
            $"Check out {LocalWork}",
            $"Merge {LocalWork} into main",
            $"Merge {LocalWork} into main (create a merge commit)",
            $"Rebase main onto {LocalWork}",
            $"Interactive rebase of main onto {LocalWork}…",
            "—",
            $"Push {LocalWork}…",
            CreateItemOf(p),
            "—",
            "Create branch here…",
            "Create tag here…",
            $"Rename {LocalWork}…",
            $"Delete {LocalWork}… (danger)",
            "—",
            "Set upstream…",
            "—",
            $"Compare {LocalWork} with main",
            $"Show reflog of {LocalWork}",
            $"Hide {LocalWork}",
            $"Solo {LocalWork}",
            "—",
            "Copy branch name",
        ];

        public const double LocalWorkMenuHeight = 519;

        /// <summary>The drop of <c>local-work</c> on <c>origin/main</c> (6 items, 3 separators, 215 high).</summary>
        public static string[] DropMenuOf(HostingProvider p) =>
        [
            $"{LocalWork} → {Base}",
            $"Push {LocalWork} to {Base}",
            $"Push {LocalWork} to {Base} and track it",
            "—",
            $"Check out {LocalWork} and rebase it onto {Base} (disabled: \"{LocalWork} is up to date with {Base}\")",
            $"Check out {LocalWork} and rebase it onto {Base} interactively…",
            "—",
            CreateItemOf(p),
            "—",
            $"Compare {LocalWork} with {Base}",
        ];

        public const double DropMenuHeight = 215;

        // ---- The form's places (7a's FormLayout, copied: no 7b file references 7a's project) ----------

        /// <summary>
        /// 7a's <c>Phase7aExpectations.PartN.FormLayout</c>, relative to the card. The x and widths of
        /// the rows' children (<c>PullRequestHead</c>, <c>PullRequestHeadRepository</c>,
        /// <c>PullRequestBase</c>, <c>PullRequestBaseRepository</c>, <c>PullRequestBaseChooseButton</c>)
        /// are 7a's texts' (<c>topic/no-pr</c>, <c>octo/hello</c>) and are never asserted for 7b: their
        /// x follows the 8-px rule and each width is its own (part N's conventions, "The form").
        /// </summary>
        public static readonly (string Name, double X, double Y, double Width, double Height)[] FormLayout =
        [
            ("CreatePullRequestDialogCard", 0, 0, 460, 386),
            ("CreatePullRequestDialogTitle", 21, 21, 418, 20),
            (From, 21, 55, 48, 16),
            ("PullRequestHead", 69, 55, 67, 16),
            ("PullRequestHeadRepository", 144, 55, 59, 16),
            (Into, 21, 86, 48, 16),
            ("PullRequestBase", 69, 86, 66, 16),
            ("PullRequestBaseRepository", 143, 86, 59, 16),
            ("PullRequestBaseChooseButton", 210, 79, 82, 30),
            ("PullRequestTitle", 21, 121, 418, 32),
            ("PullRequestDescription", 21, 161, 418, 120),
            ("PullRequestDraft", 21, 293, 52, 20),
            ("CancelButton", 299, 333, 67, 32),
            ("ConfirmButton", 374, 333, 65, 32),
        ];

        /// <summary>The rows' children, whose x and width the 8-px rule gives (never <see cref="FormLayout"/>'s).</summary>
        public static IReadOnlyList<string> RowChildren { get; } =
            ["PullRequestHead", "PullRequestHeadRepository", "PullRequestBase", "PullRequestBaseRepository", "PullRequestBaseChooseButton"];

        public const double CardWidth = 460;
        public const double CardHeight = 386;
        public const double CardLeft = 320;
        public const double CardLeft1920 = 730;
        public const double ColumnLeft = 388;
        public const double ColumnWidth = 370;
        public const double ColumnRight = 758;
        public const double ChooseWidth = 82;
        public const double Gap = 8;
        public const double LineGap = 4;
        public const double Tolerance = 1;

        /// <summary>N9's rest point at 1920×1080 (7a's open point 56): the backdrop under the card.</summary>
        public static readonly (double X, double Y) RestPoint1920 = (760, 1000);

        // ---- Pictures (the methods table's order) -----------------------------------------------------

        public const string N01a = "n01a-gitlab-remote-branch-form";
        public const string N01b = "n01b-gitlab-drop-menu";
        public const string N02a = "n02a-gitlab-form";
        public const string N02b = "n02b-azure-form";
        public const string N03a = "n03a-gitlab-draft-sent-title";
        public const string N03b = "n03b-gitlab-created-draft";
        public const string N03c = "n03c-azure-created-draft";
        public const string N04a = "n04a-gitlab-pushed-first";
        public const string N04b = "n04b-gitlab-creating";
        public const string N04c = "n04c-azure-pushed-first";
        public const string N05a = "n05a-gitlab-missing-branch";
        public const string N05b = "n05b-gitlab-already-open";
        public const string N05c = "n05c-azure-missing-branch";
        public const string N05d = "n05d-azure-already-open";
        public const string N06a = "n06a-gitlab-created";
        public const string N06b = "n06b-azure-created";
        public const string N07a = "n07a-azure-long-names";
        public const string N07b = "n07b-azure-long-name-tooltip";
        public const string N08a = "n08a-gitlab-sign-in-first";
        public const string N08b = "n08b-azure-sign-in-first";
        public const string N09a = "n09a-gitlab-form-1920";
        public const string N09b = "n09b-gitlab-form-1920-light";

        public static IReadOnlyList<string> Pictures { get; } =
        [
            N02a, N06a, N02b, N06b, N08a, N01a, N01b, N04a, N03a, N04b, N03b,
            N08b, N04c, N03c, N05a, N05b, N05c, N05d, N07a, N07b, N09a, N09b,
        ];

        /// <summary>The real window's captures, under their scripted twins' names (13.8, 14.4 "Requests and create").</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [N02a, N06a, N02b, N06b];

        // ---- For WP-R: one record per provider ----------------------------------------------------------

        /// <summary>
        /// The R steps of one provider (13.8; 14.4 "Requests and create", steps 3 and 4): the branch
        /// row, its menu item, the form's title, head, base, their repository text and the title; the
        /// create's route, status and body in its provider's own form (member, JSON value); whether the
        /// head is pushed first; the operation's title, the status text, the new row's name first in
        /// the section, the count after the create and the two pictures.
        /// </summary>
        public sealed record Provider(
            HostingProvider Kind,
            string Repository,
            IReadOnlyList<string> BranchRowPath,
            string MenuItem,
            string FormTitle,
            string Head,
            string Base,
            string RepositoryText,
            string Title,
            string CreateRoute,
            int CreateStatus,
            IReadOnlyList<(string Member, string Json)> Body,
            IReadOnlyList<string> AbsentMembers,
            bool PushedFirst,
            string OperationTitle,
            string StatusText,
            string NewRowName,
            int CountAfterCreate,
            string FormPicture,
            string CreatedPicture);

        public static Provider GitLab { get; } = new(
            HostingProvider.GitLab,
            WebApp,
            ["Local branches", TopicFolder, NoRequest],
            GitLabCreateItem,
            GitLabDialogTitle,
            Head,
            Base,
            GitLabFullName,
            HeadTitle,
            GitLabCreateRoute,
            201,
            [("source_branch", "\"topic/no-request\""), ("target_branch", "\"main\""), ("title", "\"Try an idea\""), ("description", "\"\"")],
            ["draft", "target_project_id", "allow_collaboration"],
            false,
            "Create merge request for topic/no-request",
            "Created merge request !24",
            "!24 Try an idea",
            CountAfterCreate,
            N02a,
            N06a);

        public static Provider AzureDevOps { get; } = new(
            HostingProvider.AzureDevOps,
            MobileApp,
            ["Local branches", TopicFolder, NoRequest],
            AzureDevOpsCreateItem,
            AzureDevOpsDialogTitle,
            Head,
            Base,
            AzureDevOpsFullName,
            HeadTitle,
            AzureDevOpsCreateRoute,
            201,
            [("sourceRefName", "\"refs/heads/topic/no-request\""), ("targetRefName", "\"refs/heads/main\""), ("title", "\"Try an idea\""), ("description", "\"\""), ("isDraft", "false")],
            ["forkSource"],
            false,
            "Create pull request for topic/no-request",
            "Created pull request !14",
            "!14 Try an idea",
            CountAfterCreate,
            N02b,
            N06b);

        public static Provider Of(HostingProvider p) => IsGitLab(p) ? GitLab : AzureDevOps;

        private static bool IsGitLab(HostingProvider p) => p == HostingProvider.GitLab;
    }
}
