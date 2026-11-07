using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Controls;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.GraphTestHost;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The graph's request badge for each provider (docs/phase-7b-design.md 12.6, 12.7; D355): GitLab's
/// <c>!21</c> drawn and measured as 7a's <c>#1</c>, the badge's <see cref="PullRequestBadge.Provider"/>,
/// GitLab's draft and Azure DevOps' tooltip lines; a GitHub map's labels, widths and frame as 7a's.
/// </summary>
public sealed class GraphRequestBadgeTests
{
    private static readonly HostedRepository WebApp = RequestTestData.WebApp;
    private static readonly HostedRepository MobileApp = RequestTestData.MobileApp;

    [AvaloniaFact]
    public void Gitlab_s_badge_is_its_number_drawn_and_measured_as_7a_s()
    {
        // A name short enough that the badge cuts nothing (the column cuts feature/search, open point 26).
        var data = Graph(3, (0, Local("search")), (1, Remote("origin/docs/x")), (2, Local("main", head: true)));
        using var plain = Show(new CommitGraphControl { Data = data });
        using var host = Show(new CommitGraphControl { Data = data, PullRequests = ByRef(("refs/heads/search", Request(21, "Add the search box", "search", WebApp))) });

        var before = Assert.Single(plain.Control.LabelsInRow(0));
        var label = Assert.Single(host.Control.LabelsInRow(0));
        Assert.Equal(new PullRequestBadge(21, "Add the search box", false, HostingProvider.GitLab), label.Badge);
        Assert.Equal(HostingProvider.GitLab, label.Badge!.Provider);
        var width = CommitGraphControl.BadgeGap + CommitGraphControl.LabelIconSize + CommitGraphControl.BadgeIconGap + host.LabelTextWidth("!21");
        Assert.Equal(before.Bounds.Width + width, label.Bounds.Width, 0.01);
        Assert.Equal(before.Bounds.X, label.Bounds.X);

        var badge = host.Control.BadgeInRow(0, 0) ?? throw new Xunit.Sdk.XunitException("Row 0's label has no badge.");
        Assert.Equal(host.LabelTextWidth("!21"), badge.Number.Width, 0.01);
        Assert.Equal(badge.Icon.Right + CommitGraphControl.BadgeIconGap, badge.Number.X, 0.01);
        Assert.Equal(label.Bounds.Right - CommitGraphControl.LabelPadding, badge.Number.Right, 0.01);
        Assert.Equal(plain.Control.LabelsInRow(1), host.Control.LabelsInRow(1));
    }

    [AvaloniaFact]
    public void Gitlab_s_draft_and_azure_devops_tooltip_lines()
    {
        var search = Request(21, "Add the search box", "feature/search", WebApp);
        var readme = Request(22, "Draft: Update the readme", "docs/readme", WebApp, draft: true);
        var data = Graph(3, (0, Local("feature/search")), (0, Remote("origin/feature/search")), (1, Remote("origin/docs/readme")), (2, Local("main", head: true)));
        using var gitLab = Show(new CommitGraphControl
        {
            Data = data,
            PullRequests = ByRef(("refs/heads/feature/search", search), ("refs/remotes/origin/feature/search", search), ("refs/remotes/origin/docs/readme", readme)),
        });

        Assert.Equal(new PullRequestBadge(22, "Draft: Update the readme", true, HostingProvider.GitLab), gitLab.Control.LabelsInRow(1)[0].Badge);
        gitLab.MoveMouse(gitLab.Control.LabelsInRow(0)[0].Bounds.Center);
        Assert.Equal("feature/search (merge request !21: Add the search box)\norigin/feature/search (merge request !21: Add the search box)", ToolTip.GetTip(gitLab.Control));
        gitLab.MoveMouse(gitLab.Control.LabelsInRow(1)[0].Bounds.Center);
        Assert.Equal("origin/docs/readme (draft merge request !22: Draft: Update the readme)", ToolTip.GetTip(gitLab.Control));

        var signIn = Request(11, "Add the sign-in page", "feature/sign-in", MobileApp);
        var guide = Request(12, "Write the guide", "docs/guide", MobileApp, draft: true);
        var azureData = Graph(3, (0, Local("feature/sign-in")), (0, Remote("origin/feature/sign-in")), (1, Remote("origin/docs/guide")), (2, Local("main", head: true)));
        using var azure = Show(new CommitGraphControl
        {
            Data = azureData,
            PullRequests = ByRef(("refs/heads/feature/sign-in", signIn), ("refs/remotes/origin/feature/sign-in", signIn), ("refs/remotes/origin/docs/guide", guide)),
        });

        var labels = azure.Control.LabelsInRow(0);
        Assert.Equal(new PullRequestBadge(11, "Add the sign-in page", false, HostingProvider.AzureDevOps), labels[0].Badge);
        Assert.All(labels.Skip(1), other => Assert.Null(other.Badge));
        azure.MoveMouse(labels[0].Bounds.Center);
        Assert.Equal("feature/sign-in (pull request !11: Add the sign-in page)\norigin/feature/sign-in (pull request !11: Add the sign-in page)", ToolTip.GetTip(azure.Control));
        azure.MoveMouse(azure.Control.LabelsInRow(1)[0].Bounds.Center);
        Assert.Equal("origin/docs/guide (draft pull request !12: Write the guide)", ToolTip.GetTip(azure.Control));
        Assert.Equal(new PullRequestBadge(12, "Write the guide", true, HostingProvider.AzureDevOps), azure.Control.LabelsInRow(1)[0].Badge);
    }

    [AvaloniaFact]
    public void A_github_map_s_labels_widths_and_frame_are_7a_s()
    {
        var login = Pull(1, "Add the login form", "feature/login");
        var data = Graph(3, (0, Local("feature/login")), (0, Remote("origin/feature/login")), (2, Local("main", head: true)));
        using var host = Show(new CommitGraphControl { Data = data, PullRequests = ByRef(("refs/heads/feature/login", login), ("refs/remotes/origin/feature/login", login)) });

        var label = host.Control.LabelsInRow(0)[0];
        Assert.Equal(new PullRequestBadge(1, "Add the login form", false), label.Badge);
        Assert.Equal(HostingProvider.GitHub, label.Badge!.Provider);
        Assert.Equal(host.BadgeWidth(1), CommitGraphControl.BadgeGap + CommitGraphControl.LabelIconSize + CommitGraphControl.BadgeIconGap + host.LabelTextWidth("#1"), 0.001);
        var badge = host.Control.BadgeInRow(0, 0) ?? throw new Xunit.Sdk.XunitException("Row 0's label has no badge.");
        Assert.Equal(host.LabelTextWidth("#1"), badge.Number.Width, 0.01);

        // 7a's measure (its open point 41): the badge 31.4, the label to x 117.8.
        Assert.Equal(31.4, host.BadgeWidth(1), 0.6);
        host.MoveMouse(label.Bounds.Center);
        Assert.Equal("feature/login (pull request #1: Add the login form)\norigin/feature/login (pull request #1: Add the login form)", ToolTip.GetTip(host.Control));
    }

    private static PullRequest Request(int number, string title, string head, HostedRepository repository, bool draft = false) =>
        RequestTestData.Request(number, title, "ada", head, '0', repository, repository, draft);
}
