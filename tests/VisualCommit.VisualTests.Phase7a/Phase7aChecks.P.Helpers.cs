using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part P's helpers (docs/test-reports/phase-7a.md, part P's conventions): a snapshot of the
/// section, a row's control by number or message kind, the waits for a list (D322) and for a state
/// without one (open point 34), the API requests since a mark, and the end of every method.
/// </summary>
public sealed partial class Phase7aChecksP
{
    /// <summary>The active tab's "Pull requests" section as lines: "v"/"&gt;" title count; a row's name; a message row's "[Kind] text".</summary>
    private static List<string> Section(ShellDriver app)
    {
        app.Settle();
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.FindIndex(row => row is RefSectionRow { Section: RefSection.PullRequests });
        var lines = new List<string>();
        for (var i = start; i < rows.Count && (i == start || rows[i] is not RefSectionRow); i++)
        {
            lines.Add(rows[i] switch
            {
                RefSectionRow section => $"{(section.IsOpen ? "v" : ">")} {section.Text} {section.CountText}",
                PullRequestRow pull => pull.Text,
                PullRequestMessageRow message => $"[{message.Kind}] {message.Text}",
                _ => rows[i].Text,
            });
        }

        return lines;
    }

    private static List<PullRequestRow> PullRequestRows(ShellDriver app) =>
        [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<PullRequestRow>()];

    /// <summary>The control of the row <paramref name="which"/> picks, scrolled into view and laid out.</summary>
    private static async Task<Control> RowAsync(ShellDriver app, Func<RefListRow, bool> which, string what)
    {
        var list = app.Find<ItemsControl>("RefList");
        Control? found = null;
        await app.WaitForAsync(
            () =>
            {
                var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
                var index = rows.FindIndex(row => which(row));
                if (index < 0)
                {
                    return false;
                }

                found = list.GetVisualDescendants().OfType<Button>()
                    .FirstOrDefault(button => ReferenceEquals(button.DataContext, rows[index]) && button.IsEffectivelyVisible && button.Bounds.Height > 0
                        && Avalonia.Automation.AutomationProperties.GetAutomationId(button) is "PullRequestItem" or "PullRequestMessage" or "Section");
                if (found is null)
                {
                    list.ScrollIntoView(index);
                    return false;
                }

                return app.BoundsOf(found).Bottom <= app.BoundsOf(list).Bottom + 0.5;
            },
            what);
        return found!;
    }

    private static Task<Control> PullRequestAsync(ShellDriver app, int number) =>
        RowAsync(app, row => row is PullRequestRow pull && pull.Number == number, $"pull request #{number}'s row");

    private static Task<Control> MessageAsync(ShellDriver app) =>
        RowAsync(app, row => row is PullRequestMessageRow, "the section's message row");

    private static Task<Control> HeaderAsync(ShellDriver app) =>
        RowAsync(app, row => row is RefSectionRow { Section: RefSection.PullRequests }, "the section's header");

    /// <summary>D322: the tab's <c>{repo}: listed {count} open pull requests of {listed}</c> since <paramref name="logMark"/>, then quiet watchers.</summary>
    private static async Task WaitForListedAsync(ShellDriver app, string repository, int count, string listed, int logMark)
    {
        var line = await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(repository), mark: logMark);
        var match = AppLogFile.PullRequestsListed(repository).Match(line);
        Assert.Equal((count.ToString(System.Globalization.CultureInfo.InvariantCulture), listed), (match.Groups["count"].Value, match.Groups["repository"].Value));
        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>Open point 34: a state without a list waits for the view model's state, the section's message row, then quiet watchers.</summary>
    private static async Task WaitForStateAsync(ShellDriver app, RepositoryViewModel repository, PullRequestsState state, PullRequestMessageKind message)
    {
        await app.WaitForAsync(() => repository.HostingState.PullRequests == state, $"the hosting {state}");
        await app.WaitForAsync(() => repository.LeftPanel.Rows.OfType<PullRequestMessageRow>().Any(row => row.Kind == message), $"the {message} row");
        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>The API requests since <paramref name="mark"/>, leaving out the avatars' (routes <c>commits:</c> and <c>avatar:</c>, 7.9).</summary>
    private static List<FakeGitHubRequest> ApiSince(ServedHosting served, int mark) =>
        [.. served.GitHub.Requests.Skip(mark).Where(request => request.Route is not { } route || !(route.StartsWith("commits:", StringComparison.Ordinal) || route.StartsWith("avatar:", StringComparison.Ordinal)))];

    private static List<string> OpenedUrls(ShellDriver app) =>
        [.. app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath)];

    /// <summary>Every method's end (part P's conventions): the app closed, the tripwire for the three tokens, the fake's errors and refusals none, no 400 or 415.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting served, params string[] more)
    {
        var calls = app.Session.GitCalls;
        app.Dispose();
        foreach (var token in new[] { served.OctoToken, served.OctoClassicToken, served.AdaToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.Hello.Path, served.Tool.Path, .. more], calls);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
    }

    private static string Url(ServedHosting served, string path) => P.PullsUrl(served.GitHub.Port).Replace("octo/hello/pulls", path, StringComparison.Ordinal);
}
