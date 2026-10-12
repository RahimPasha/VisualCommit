// Tests of Phase4Expectations.D.cs's data against itself and the palette (the real-window pass
// leaves this file out: its name does not match Phase4Expectations*.cs).
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using Tint = VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC2.Tint;

namespace VisualCommit.VisualTests.Phase4;

public sealed class PartDExpectationTests
{
    [Fact]
    public void N3_has_24_lines_and_after_Use_ours_in_block_2_18()
    {
        Assert.Equal(24, PartD.N3.Count(character => character == '\n'));
        Assert.Equal(24, PartD.N3Tints.Count);
        Assert.Equal(18, PartD.N3OursOnBlock2.Count(character => character == '\n'));
        Assert.Equal(18, PartD.N3OursOnBlock2Tints.Count);
        Assert.Equal(PartD.N3Tints.Take(8), PartD.N3OursOnBlock2Tints.Take(8));
        Assert.Equal(Tint.None, PartD.N3OursOnBlock2Tints[15]);
        Assert.Equal(8, PartD.N3Tints.Count(tint => tint == Tint.Marker));
        Assert.Equal(2, PartD.N3Tints.Count(tint => tint == Tint.Base));
    }

    [Fact]
    public void The_light_hexes_the_report_gives_are_distinct_tokens_and_the_list_is_G_I()
    {
        Assert.Equal(PartD.LightHexes.Count, PartD.LightHexes.Select(entry => entry.Token).Distinct().Count());
        Assert.Equal(Light.Accent, PartD.LightHexes.Single(entry => entry.Token == "VcAccentBrush").Colour);
        Assert.Equal(8, PartD.GraphGI.Count);
        Assert.Equal("Base", PartD.GraphGI[PartD.BaseRow].Message);
        Assert.Equal(6, PartD.Rows.Count);
        Assert.Equal(PartD.GraphGI.Take(6).Select(row => (row.ShortSha, row.Message)), PartD.Rows.Select(row => ((string?)row.Sha, row.Subject)));
    }
}
