// GitLab's real-window steps (docs/test-reports/phase-7b.md, the R steps in the Notes of A5, B1, B3,
// B7, P1, P10, N1, N2 and N6; docs/phase-7b-design.md 14.4): GitLab's "Sign-in, browse, clone" on one
// empty tab (a05b-gitlab-token-masked, b03a-gitlab-listed, a clone of northfield/platform/web-app, a
// nested path) and its "Requests and create" on web-app, prepared as its twins are (p01a-gitlab-rows,
// p10a-gitlab-section-menu, n02a-gitlab-form, n06a-gitlab-created). Both launches are shared with Azure
// DevOps' (Phase7bRealWindowPass.cs), as the twins share their methods.
using VisualCommit.Core.Hosting;
using Xunit;

namespace VisualCommit.RealWindowTests;

public partial class Phase7bRealWindowPass
{
    /// <summary>
    /// GitLab's "Sign-in, browse, clone" (14.4; <see cref="SignInBrowseAndCloneAsync"/>): A5's dialog
    /// with the token masked, B1's <c>From GitLab…</c>, B3's list and B7's clone of
    /// <c>northfield/platform/web-app</c> through the app's helper as <c>x-access-token</c>.
    /// </summary>
    [Fact]
    public Task Check_GitLab_sign_in_browse_and_clone() => SignInBrowseAndCloneAsync(HostingProvider.GitLab);

    /// <summary>
    /// GitLab's "Requests and create" (14.4; <see cref="RequestsAndCreateAsync"/>) on <c>web-app</c>,
    /// whose upstream is GitHub's and is never asked: P1's rows and badge, P10's menus, N2's form for
    /// <c>topic/no-request</c> and N6's <c>Created merge request !24</c>.
    /// </summary>
    [Fact]
    public Task Check_GitLab_requests_and_create() => RequestsAndCreateAsync(HostingProvider.GitLab);
}
