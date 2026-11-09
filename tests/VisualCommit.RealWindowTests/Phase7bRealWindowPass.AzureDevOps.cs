// Azure DevOps' real-window steps (docs/test-reports/phase-7b.md, the R steps in the Notes of A6, B1,
// B3, B7, P1, P10, N1, N2 and N6; docs/phase-7b-design.md 14.4): Azure DevOps' "Sign-in, browse, clone"
// on one empty tab, the organization typed (a06b-azure-token-masked, b03b-azure-listed, a clone of
// mobile-app through its Fernhill-Labs@ URL with %20), and its "Requests and create" on mobile-app, the
// organization prefilled from the tab's remote (ruling 16), prepared as its twins are (p01c-azure-rows,
// p10c-azure-section-menu, n02b-azure-form, n06b-azure-created). Both launches are shared with GitLab's
// (Phase7bRealWindowPass.cs), as the twins share their methods. Never <organization>.visualstudio.com
// (14.1).
using VisualCommit.Core.Hosting;
using Xunit;

namespace VisualCommit.RealWindowTests;

public partial class Phase7bRealWindowPass
{
    /// <summary>
    /// Azure DevOps' "Sign-in, browse, clone" (14.4; <see cref="SignInBrowseAndCloneAsync"/>): A6's
    /// dialog with <c>Fernhill-Labs</c> typed and the token masked, B1's <c>From Azure DevOps…</c>, B3's
    /// list and B7's clone of <c>mobile-app</c> through the app's helper as <c>x-access-token</c>.
    /// </summary>
    [Fact]
    public Task Check_AzureDevOps_sign_in_browse_and_clone() => SignInBrowseAndCloneAsync(HostingProvider.AzureDevOps);

    /// <summary>
    /// Azure DevOps' "Requests and create" (14.4; <see cref="RequestsAndCreateAsync"/>) on
    /// <c>mobile-app</c>: P1's rows and badge, P10's menus, N2's form for <c>topic/no-request</c> and
    /// N6's <c>Created pull request !14</c>.
    /// </summary>
    [Fact]
    public Task Check_AzureDevOps_requests_and_create() => RequestsAndCreateAsync(HostingProvider.AzureDevOps);
}
