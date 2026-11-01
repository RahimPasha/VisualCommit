using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The token's tripwire at the three base64 alignments (docs/phase-7b-design.md 2.5, g4 D1.6): the
/// fakes' tokens after <c>x-access-token:</c>, Azure DevOps' <c>:</c>, <c>ab:</c>, <c>abcd:</c>,
/// <c>oauth2:</c> and alone, with and without a trailing newline, are found in a file and in a git
/// call's record, each named by its offset (the prefix's length over a multiple of 3), never by the token.
/// </summary>
public sealed class TokenTripwireFormsTests
{
    public static TheoryData<string, string, bool> Cases()
    {
        var cases = new TheoryData<string, string, bool>();
        foreach (var token in new[] { "github_pat_FAKE-octo-1", "glpat-FAKE-ada-1", "azdo_FAKE-Fernhill-Labs-1" })
        {
            foreach (var prefix in new[] { "x-access-token:", ":", "ab:", "abcd:", "oauth2:", string.Empty })
            {
                cases.Add(token, prefix, false);
                cases.Add(token, prefix, true);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_token_in_base64_after_any_prefix_is_found_and_named_by_its_offset(string token, string prefix, bool newline)
    {
        using var folder = new TempDirectory("tripwire forms");
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(prefix + token + (newline ? "\n" : string.Empty)));
        var form = $"base64 at offset {prefix.Length % 3}";
        Assert.DoesNotContain(token, encoded, StringComparison.Ordinal);

        // Clean, the folder passes.
        File.WriteAllText(Path.Combine(folder.Path, "clean.txt"), "Authorization: Basic " + Convert.ToBase64String("x-access-token:another"u8) + "\r\n");
        TokenTripwire.AssertAbsent(token, [folder.Path], null);

        // In a file, as a Basic header would write it.
        File.WriteAllText(Path.Combine(folder.Path, "trace.txt"), "Authorization: Basic " + encoded + "\r\n");
        var inFile = Assert.Throws<TokenTripwireException>(() => TokenTripwire.AssertAbsent(token, [folder.Path], null));
        Assert.Contains($"trace.txt ({form})", inFile.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(token, inFile.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("clean.txt", inFile.Message, StringComparison.Ordinal);
        File.Delete(Path.Combine(folder.Path, "trace.txt"));

        // In a git call's recorded error output.
        var log = new GitCallLog();
        log.Add(new GitCallRecord(DateTimeOffset.UnixEpoch, TimeSpan.Zero, "git fetch origin", folder.Path, GitCallOutcome.Completed, 0, string.Empty, "trace: Authorization: Basic " + encoded + "\n"));
        var inCall = Assert.Throws<TokenTripwireException>(() => TokenTripwire.AssertAbsent(token, [folder.Path], log));
        Assert.Contains($"the error of the git call git fetch origin ({form})", inCall.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(token, inCall.Message, StringComparison.Ordinal);
    }
}
