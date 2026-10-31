using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CloneTexts;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part B's expectations against the App (docs/test-reports/phase-7a.md, part B; design 9.7):
/// <c>CloneTexts</c> and the two <c>HostingTexts.Describe</c> texts equal <see cref="PartB"/>'s,
/// which were written from the report; the rows' details are the scenario's flags and description;
/// the picture names are unique and of design 0.3's form.
/// </summary>
public sealed partial class Phase7aExpectationTests
{
    [Fact]
    public void Part_B_s_texts_are_the_app_s()
    {
        Assert.Equal(PartB.FromGitHubButton, Texts.FromGitHubButton);
        Assert.Equal(PartB.DialogTitle, Texts.DialogTitle);
        Assert.Equal(PartB.PaletteCommand, Texts.PaletteCommand);
        Assert.Equal(PartB.Loading, Texts.Loading);
        Assert.Equal(PartB.Empty, Texts.Empty);
        Assert.Equal(PartB.MoreExist, Texts.MoreExist);
        Assert.Equal(PartB.SomeHiddenBySso, Texts.SomeHiddenBySso);
        Assert.Equal(PartB.Unexpected, Texts.Unexpected);
        Assert.Equal(PartB.Choose, Texts.Choose);
        Assert.Equal(PartB.Cancel, Texts.Cancel);
        Assert.Equal(PartB.Filter, Texts.Filter);
        Assert.Equal((PartB.Private, PartB.Fork, PartB.Archived, PartB.DetailSeparator), (Texts.Private, Texts.Fork, Texts.Archived, Texts.DetailSeparator));
        Assert.Equal(PartB.ServerErrorText, HostingTexts.Describe(new HostingFailure(HostingFailureKind.ServerError, "Server Error"), OctoLogin));
        Assert.Equal(PartB.RefusedOcto, HostingTexts.Describe(new HostingFailure(HostingFailureKind.BadCredentials, "Bad credentials"), OctoLogin));
        Assert.StartsWith(PartB.PaletteQuery, PartB.PaletteCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void Part_B_s_rows_are_the_scenario_s_and_its_pictures_unique_and_of_the_report_s_form()
    {
        Assert.Equal(PartB.ToolDescription, Scenarios.ToolDescription);
        Assert.Equal(
            [("octo/hello", "Private"), ("octo/tool", "Fork · Small command-line tools."), ("octo/notes", "Private"), ("octo/website", "Archived")],
            PartB.Rows);
        Assert.Equal(2, PartB.Rows.Count(row => row.Detail?.Contains("private", StringComparison.OrdinalIgnoreCase) == true));
        Assert.Equal("octo/listed-0001", PartB.Listed(1));
        Assert.Equal("octo/listed-0996", PartB.Listed(PartB.ListedCap - PartB.Rows.Count));
        Assert.Equal("http://127.0.0.1:47395/octo/hello.git", PartB.HelloCloneUrl(ServerPort));
        Assert.Equal("github:127.0.0.1:47395:octo", PartB.OctoAccountId(ServerPort));

        var pattern = new Regex(@"^[abpgn]?\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
        Assert.Equal(10, PartB.Pictures.Count);
        Assert.Equal(PartB.Pictures.Count, PartB.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartB.Pictures, name => Assert.Matches(pattern, name));
        Assert.All(PartB.Pictures, name => Assert.StartsWith("b", name, StringComparison.Ordinal));
        Assert.All(PartB.RealWindowPictures, name => Assert.Contains(name, PartB.Pictures));
        Assert.Equal(3, PartB.Tokens.Select(token => token.Token).Distinct(StringComparer.Ordinal).Count());
    }
}
