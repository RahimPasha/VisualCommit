using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="PullModes"/> and <see cref="GitBoolean"/> (docs/phase-6-design.md 8.7): what each
/// mode writes, how a scope's pair of keys reads by rules 1 to 8, and booleans read as git reads
/// them, checked against <c>git config --type=bool</c> itself.
/// </summary>
public class PullModesTests
{
    public static TheoryData<PullMode, string?, string?> ModeWrites => new()
    {
        { PullMode.NotSet, null, null },
        { PullMode.Merge, "false", "true" },
        { PullMode.MergeCommit, "false", "false" },
        { PullMode.Rebase, "true", "true" },
        { PullMode.RebaseMerges, "merges", "true" },
        { PullMode.FastForwardOnly, null, "only" },
    };

    [Theory]
    [MemberData(nameof(ModeWrites))]
    public void Each_mode_writes_both_keys_rebase_first(PullMode mode, string? rebase, string? fastForward)
    {
        Assert.Equal([new GitConfigWrite("pull.rebase", rebase), new GitConfigWrite("pull.ff", fastForward)], PullModes.Writes(mode));
    }

    [Theory]
    [MemberData(nameof(ModeWrites))]
    public void What_each_mode_writes_reads_back_as_that_mode(PullMode mode, string? rebase, string? fastForward)
    {
        _ = rebase;
        _ = fastForward;
        var writes = PullModes.Writes(mode);

        Assert.Equal(new PullModeReading(mode, null, null), PullModes.Read(writes[0].Value, rebaseIsBare: false, writes[1].Value));
    }

    /// <summary>A scope's pair and the radio it shows (8.7): a mode, or "Other" with its line and second line.</summary>
    public static TheoryData<string?, bool, string?, PullMode?, string?, string?> Readings => new()
    {
        // 8. Nothing set.
        { null, false, null, PullMode.NotSet, null, null },

        // The single keys git's hint and Git for Windows' installer write show as their modes.
        { "false", false, null, PullMode.Merge, null, null },
        { "true", false, null, PullMode.Rebase, null, null },
        { null, false, "only", PullMode.FastForwardOnly, null, null },

        // 2. pull.ff = only wins over every rebase setting, an unusual one included.
        { "true", false, "only", PullMode.FastForwardOnly, null, null },
        { "merges", false, "only", PullMode.FastForwardOnly, null, null },
        { "false", false, "only", PullMode.FastForwardOnly, null, null },
        { "interactive", false, "only", PullMode.FastForwardOnly, null, null },
        { "k", false, "only", PullMode.FastForwardOnly, null, null },

        // 4. A rebase ignores pull.ff = false.
        { "true", false, "false", PullMode.Rebase, null, null },
        { "true", false, "true", PullMode.Rebase, null, null },
        { null, true, null, PullMode.Rebase, null, null },
        { "1", false, null, PullMode.Rebase, null, null },
        { "10k", false, null, PullMode.Rebase, null, null },
        { "TRUE", false, null, PullMode.Rebase, null, null },
        { "Yes", false, null, PullMode.Rebase, null, null },
        { "ON", false, null, PullMode.Rebase, null, null },
        { "0x10", false, null, PullMode.Rebase, null, null },
        { "-1", false, null, PullMode.Rebase, null, null },
        { "010", false, null, PullMode.Rebase, null, null },
        { "1K", false, null, PullMode.Rebase, null, null },

        // 5.
        { "merges", false, null, PullMode.RebaseMerges, null, null },
        { "merges", false, "true", PullMode.RebaseMerges, null, null },
        { "merges", false, "false", PullMode.RebaseMerges, null, null },

        // 6.
        { "false", false, "false", PullMode.MergeCommit, null, null },
        { null, false, "false", PullMode.MergeCommit, null, null },
        { null, false, string.Empty, PullMode.MergeCommit, null, null },

        // 7.
        { "false", false, "true", PullMode.Merge, null, null },
        { null, false, "true", PullMode.Merge, null, null },
        { "0x0", false, null, PullMode.Merge, null, null },
        { "00", false, null, PullMode.Merge, null, null },
        { string.Empty, false, null, PullMode.Merge, null, null },

        // 1. A value Pull refuses, whatever the other key says.
        { "preserve", false, null, null, "Other: pull.rebase = preserve", PullModes.RefusedNote },
        { "preserve", false, "only", null, "Other: pull.rebase = preserve", PullModes.RefusedNote },
        { null, false, "foo", null, "Other: pull.ff = foo", PullModes.RefusedNote },
        { "true", false, "sometimes", null, "Other: pull.ff = sometimes", PullModes.RefusedNote },
        { "preserve", false, "sometimes", null, "Other: pull.rebase = preserve, pull.ff = sometimes", PullModes.RefusedNote },
        { "preserve", false, "k", null, "Other: pull.rebase = preserve, pull.ff = k", PullModes.RefusedNote },
        { "MERGES", false, null, null, "Other: pull.rebase = MERGES", PullModes.RefusedNote },
        { "Interactive", false, null, null, "Other: pull.rebase = Interactive", PullModes.RefusedNote },
        { null, false, "ONLY", null, "Other: pull.ff = ONLY", PullModes.RefusedNote },
        { "99999999999", false, null, null, "Other: pull.rebase = 99999999999", PullModes.RefusedNote },

        // 3. Values the app reads as its git does, or without git's editor.
        { "interactive", false, null, null, "Other: pull.rebase = interactive", PullModes.InteractiveNote },
        { "i", false, "true", null, "Other: pull.rebase = i", PullModes.InteractiveNote },
        { "m", false, null, null, "Other: pull.rebase = m", PullModes.MNote },
        { "M", false, null, null, "Other: pull.rebase = M", PullModes.UnitLetterNote },
        { "k", false, null, null, "Other: pull.rebase = k", PullModes.UnitLetterNote },
        { "G", false, "false", null, "Other: pull.rebase = G", PullModes.UnitLetterNote },
        { null, false, "m", null, "Other: pull.ff = m", PullModes.UnitLetterNote },
        { "true", false, "K", null, "Other: pull.ff = K", PullModes.UnitLetterNote },
        { "interactive", false, "k", null, "Other: pull.rebase = interactive, pull.ff = k", PullModes.InteractiveNote },
    };

    [Theory]
    [MemberData(nameof(Readings))]
    public void A_scopes_pair_reads_by_the_first_rule_that_holds(string? rebase, bool rebaseIsBare, string? fastForward, PullMode? mode, string? other, string? note)
    {
        Assert.Equal(new PullModeReading(mode, other, note), PullModes.Read(rebase, rebaseIsBare, fastForward));
    }

    [Fact]
    public void A_bare_pull_ff_reads_as_true()
    {
        Assert.Equal(new PullModeReading(PullMode.Merge, null, null), PullModes.Read(null, rebaseIsBare: false, null, fastForwardIsBare: true));
        Assert.Equal(new PullModeReading(PullMode.NotSet, null, null), PullModes.Read(null, rebaseIsBare: false, null, fastForwardIsBare: false));
    }

    [Fact]
    public void The_second_lines_are_the_designs()
    {
        Assert.Equal("Git opens an editor for the list; the app rebases without one.", PullModes.InteractiveNote);
        Assert.Equal("Git 2.36 merges with this value and newer gits rebase keeping merge commits; the app does what its git does.", PullModes.MNote);
        Assert.Equal("Git 2.36 reads this value as false and newer gits refuse it; the app does what its git does.", PullModes.UnitLetterNote);
        Assert.Equal("Pulls fail while this value is set.", PullModes.RefusedNote);
    }

    /// <summary>Values and how git reads them as booleans (0.3): true, false, or none (null).</summary>
    public static TheoryData<string, bool?> Booleans => new()
    {
        { "true", true }, { "TRUE", true }, { "True", true }, { "Yes", true }, { "ON", true }, { "on", true },
        { "false", false }, { "FALSE", false }, { "No", false }, { "off", false }, { string.Empty, false },
        { "1", true }, { "2", true }, { "-1", true }, { "+1", true }, { " 1", true }, { "0x10", true }, { "0X10", true },
        { "010", true }, { "1K", true }, { "1k", true }, { "1g", true }, { "10k", true }, { "2147483647", true },
        { "-2147483647", true }, { "2097151k", true }, { "-2097151k", true }, { "0x7fffffff", true }, { "017777777777", true },
        { "0", false }, { "00", false }, { "0x0", false }, { "-0", false },
        { "99999999999", null }, { "2147483648", null }, { "2g", null }, { "2097152k", null }, { "0x80000000", null },
        { "0xffffffffffffffffff", null }, { "08", null }, { "0x", null }, { "1.5", null }, { "+", null }, { "-", null }, { "-k", null },
        { "1 ", null }, { "kb", null }, { "1kb", null }, { "merges", null }, { "only", null }, { "ONLY", null }, { "MERGES", null },
        { "interactive", null }, { "i", null }, { "foo", null },
    };

    [Theory]
    [MemberData(nameof(Booleans))]
    public void A_value_is_a_boolean_as_git_reads_one(string value, bool? expected)
    {
        var parsed = GitBoolean.TryParse(value, bare: false, out var result);

        Assert.Equal(expected is not null, parsed);
        Assert.Equal(expected ?? false, result);
    }

    [Fact]
    public void A_bare_key_is_true_and_an_unset_key_no_boolean()
    {
        Assert.True(GitBoolean.TryParse(null, bare: true, out var bare));
        Assert.True(bare);
        Assert.False(GitBoolean.TryParse(null, bare: false, out var unset));
        Assert.False(unset);
    }

    [Theory]
    [InlineData("k")]
    [InlineData("K")]
    [InlineData("m")]
    [InlineData("M")]
    [InlineData("g")]
    [InlineData("G")]
    public void A_unit_letter_alone_is_no_boolean_here(string value)
    {
        Assert.True(GitBoolean.IsUnitLetterAlone(value));
        Assert.False(GitBoolean.TryParse(value, bare: false, out _));
    }

    [Fact]
    public void The_int_minimum_is_a_boolean_as_newer_gits_read_it()
    {
        // Git 2.55 reads -2147483648 (and -2097152k) as true; Git 2.36 refuses it, a value no setting has.
        Assert.True(GitBoolean.TryParse("-2147483648", bare: false, out var result));
        Assert.True(result);
        Assert.True(GitBoolean.TryParse("-2097152k", bare: false, out var withUnit));
        Assert.True(withUnit);
    }

    [Theory]
    [MemberData(nameof(Booleans))]
    public async Task Git_reads_each_value_as_TryParse_does(string value, bool? expected)
    {
        Assert.Equal(expected, await GitBooleanAsync(value));
    }

    [Theory]
    [InlineData("k")]
    [InlineData("K")]
    [InlineData("m")]
    [InlineData("M")]
    [InlineData("g")]
    [InlineData("G")]
    public async Task Git_236_reads_a_unit_letter_alone_as_false_and_newer_gits_refuse_it(string value)
    {
        var read = await GitBooleanAsync(value);

        // Checked on 2.36 and 2.55 (0.3); which git between them began to refuse it was not, so a
        // git between them may do either, but none reads it as true.
        var version = await GitConfigTestSupport.GitVersionAsync();
        if (version is { Major: 2, Minor: 36 })
        {
            Assert.False(read);
        }
        else if (version >= new GitVersion(2, 55, 0))
        {
            Assert.Null(read);
        }
        else
        {
            Assert.NotEqual(true, read);
        }
    }

    /// <summary>How the git the tests run reads <paramref name="value"/> as a boolean: <c>git -c probe.value=&lt;v&gt; config --type=bool</c>, outside any repository.</summary>
    private static async Task<bool?> GitBooleanAsync(string value)
    {
        using var folder = new TempDirectory("bool");
        var result = await NewRunner().RunAsync(
            new GitCommand("-c", "probe.value=" + value, "config", "--type=bool", "--get", "probe.value") { WorkingDirectory = folder.Path },
            TestCancelled);
        if (!result.Succeeded)
        {
            // git dies on a bad boolean ("bad boolean config value" on the report's git).
            Assert.Equal(128, result.ExitCode);
            return null;
        }

        return result.StandardOutput.Trim() switch
        {
            "true" => true,
            "false" => false,
            var other => throw new InvalidOperationException($"git config --type=bool printed {other}."),
        };
    }
}
