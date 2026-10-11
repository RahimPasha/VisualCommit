using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="RefNameRules"/> (phase 3 design 2.4, D96): git's own rules, checked against <c>git check-ref-format</c>.</summary>
public class RefNameRulesTests
{
    /// <summary>Names that git accepts and names it refuses for every reason check-ref-format has.</summary>
    public static TheoryData<string> Names =>
    [
        "main", "feature/alpha", "feature/alpha/x", "release-1.0", "v1.0", "a.b", "a@b", "@a", "a@", "Fix_#42", "dash-in-middle",
        "-leading", "HEAD", "café", "数字", "x.lockx", "a.lock.b", "a+b", "a=b", "a{b}", "a}b", "a!", "a'b", "a\"b", "a,b", "a;b",
        "", "@", ".hidden", "a/.hidden", "a..b", "a/..", "x.lock", "a/x.lock/b", "a/", "/a", "a//b", "a.", "a/b.",
        "a b", "a~1", "a^", "a:b", "a?", "a*", "a[b", "a\\b", "a@{1}", "@{-1}", "a\tb", "a\u007fb", "a\u0001b",
    ];

    [Theory]
    [MemberData(nameof(Names))]
    public async Task A_full_ref_name_is_valid_exactly_when_git_check_ref_format_accepts_it(string name)
    {
        foreach (var fullName in new[] { "refs/heads/" + name, "refs/tags/" + name, "refs/remotes/" + name + "/HEAD" })
        {
            var git = await NewRunner().RunAsync(new GitCommand("check-ref-format", fullName), TestCancelled);

            var problem = RefNameRules.RefNameProblem(fullName);

            Assert.True(
                git.Succeeded == (problem is null),
                $"{fullName}: git check-ref-format exits {git.ExitCode}, the rules say {problem ?? "valid"}.");
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Branch_tag_and_remote_names_follow_git_and_refuse_a_leading_dash(string name)
    {
        var branch = (await NewRunner().RunAsync(new GitCommand("check-ref-format", "refs/heads/" + name), TestCancelled)).Succeeded;
        var tag = (await NewRunner().RunAsync(new GitCommand("check-ref-format", "refs/tags/" + name), TestCancelled)).Succeeded;
        var remote = (await NewRunner().RunAsync(new GitCommand("check-ref-format", "refs/remotes/" + name + "/HEAD"), TestCancelled)).Succeeded;
        var dashOrEmpty = name.Length == 0 || name.StartsWith('-');

        Assert.Equal(branch && !dashOrEmpty && name != "HEAD", RefNameRules.IsValidBranchName(name));
        Assert.Equal(tag && !dashOrEmpty, RefNameRules.IsValidTagName(name));
        Assert.Equal(remote && !dashOrEmpty, RefNameRules.IsValidRemoteName(name));
    }

    [Theory]
    [InlineData("a b", "it contains a space")]
    [InlineData("a..b", "it contains ..")]
    [InlineData("x.lock", "a part of it ends with .lock")]
    [InlineData(".x", "a part of it starts with .")]
    [InlineData("a//b", "it contains //")]
    [InlineData("a/", "it ends with /")]
    [InlineData("a.", "it ends with .")]
    [InlineData("a@{1}", "it contains @{")]
    [InlineData("-x", "it starts with -")]
    [InlineData("HEAD", "it cannot be HEAD")]
    [InlineData("", "it is empty")]
    public void A_branch_name_problem_says_why_in_words(string name, string reason) =>
        Assert.Equal(reason, RefNameRules.BranchNameProblem(name));

    [Theory]
    [InlineData("feature/alpha", false, RefNameClashKind.SameName, "feature/alpha")]
    [InlineData("feature", false, RefNameClashKind.Folder, "feature/alpha")]
    [InlineData("feature/alpha/x", false, RefNameClashKind.Folder, "feature/alpha")]
    [InlineData("Feature/alpha", true, RefNameClashKind.OtherCase, "feature/alpha")]
    [InlineData("Feature/beta", true, RefNameClashKind.OtherCase, "feature/alpha")]
    [InlineData("MAIN", true, RefNameClashKind.OtherCase, "main")]
    [InlineData("Feature", true, RefNameClashKind.OtherCase, "feature/alpha")]
    public void A_name_clashes_with_the_same_name_a_folder_or_another_case(string name, bool ignoreCase, RefNameClashKind kind, string existing) =>
        Assert.Equal(new RefNameClash(kind, existing), RefNameRules.FindClash(name, ["main", "feature/alpha", "featured"], ignoreCase));

    [Theory]
    [InlineData("Feature/beta", false)]
    [InlineData("MAIN", false)]
    [InlineData("feature/beta", true)]
    [InlineData("feat", true)]
    [InlineData("featured/x", true)]
    [InlineData("mainline", true)]
    public void Other_names_do_not_clash(string name, bool ignoreCase) =>
        Assert.Null(RefNameRules.FindClash(name, ["main", "feature/alpha"], ignoreCase));

    [Fact]
    public void An_exact_clash_wins_over_a_clash_in_another_case()
    {
        Assert.Equal(
            new RefNameClash(RefNameClashKind.SameName, "Main"),
            RefNameRules.FindClash("Main", ["main", "Main"], ignoreCase: true));
    }
}
