using System.Globalization;
using System.Runtime.InteropServices;
using Xunit.Sdk;

namespace VisualCommit.App.Tests;

/// <summary>
/// The measures of <see cref="PaletteGrowthTests.The_build_and_a_one_letter_query_grow_in_step_with_the_refs"/>
/// and what they leave: a warm-up of the small context and then the large, and three rounds of the
/// same (docs/phase-6-design.md 5.12), each measure's work kept as soon as its measure returns, so
/// that a failed measure's record and failure give every work measured before it, the failed one
/// named (docs/phase-7b-design.md 16.3).
/// </summary>
internal sealed class PaletteGrowthWorks
{
    private const int Rounds = 3;

    /// <summary>The warm-up's pair and then each round's: the small context's work and the large's, null when not measured.</summary>
    private readonly List<(ulong? Small, ulong? Large)> _pairs = new(Rounds + 1);

    public ulong BestSmall { get; private set; } = ulong.MaxValue;

    public ulong BestLarge { get; private set; } = ulong.MaxValue;

    public double FastestSmall { get; private set; } = double.MaxValue;

    public double FastestLarge { get; private set; } = double.MaxValue;

    public int SmallCount { get; private set; }

    public int LargeCount { get; private set; }

    /// <summary>
    /// Runs the warm-up and the rounds, the small context before the large, and keeps the best work
    /// and the fastest time of the rounds. A measure that throws ends the run: its line, with every
    /// work kept, goes to <paramref name="record"/>, and the test fails with the measure's message,
    /// the same data and, when <paramref name="record"/> returned one, the note of the unwritten record.
    /// The works are kept between measures, outside their regions without collection.
    /// </summary>
    public static PaletteGrowthWorks Measure<TContext>(
        TContext small,
        TContext large,
        Func<TContext, (ulong Work, double Milliseconds, int Items)> measure,
        Func<string, string?> record)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(record);
        var works = new PaletteGrowthWorks();
        try
        {
            // Pair 0 is the warm-up: its works are kept, and counted among neither the best nor the fastest.
            for (var pair = 0; pair <= Rounds; pair++)
            {
                works._pairs.Add((null, null));
                var (work, milliseconds, items) = measure(small);
                works._pairs[pair] = (work, null);
                works.SmallCount = items;
                if (pair > 0)
                {
                    (works.BestSmall, works.FastestSmall) = (Math.Min(works.BestSmall, work), Math.Min(works.FastestSmall, milliseconds));
                }

                (work, milliseconds, items) = measure(large);
                works._pairs[pair] = (works._pairs[pair].Small, work);
                works.LargeCount = items;
                if (pair > 0)
                {
                    (works.BestLarge, works.FastestLarge) = (Math.Min(works.BestLarge, work), Math.Min(works.FastestLarge, milliseconds));
                }
            }
        }
        catch (Exception ex)
        {
            var failedMeasure = ex.Message.Split('\n')[0].TrimEnd('\r');
            var data = works.Describe();
            var notWritten = record($"the measure failed ({failedMeasure}); {data}");
            throw new XunitException($"{ex.Message}\nThe data: {data}." + (notWritten is null ? string.Empty : "\n" + notWritten), ex);
        }

        return works;
    }

    /// <summary>
    /// Each measure's work at 2,500/10,000 tags, the warm-up's and then each round's, a measure that
    /// failed written "failed" and the large one it left unmeasured "-"; the items of each context,
    /// "not counted" before its first measure returned; the framework and the runtime identifier.
    /// </summary>
    public string Describe()
    {
        static string Pair((ulong? Small, ulong? Large) pair) => pair.Small is { } small
            ? string.Create(CultureInfo.InvariantCulture, $"{small}/{(pair.Large is { } large ? large.ToString(CultureInfo.InvariantCulture) : "failed")}")
            : "failed/-";
        static string Count(int items) => items == 0 ? "not counted" : items.ToString(CultureInfo.InvariantCulture);

        var warmUp = _pairs.Count == 0 ? "none" : Pair(_pairs[0]);
        var rounds = _pairs.Count <= 1 ? "none" : string.Join(", ", _pairs.Skip(1).Select(Pair));
        var items = SmallCount == 0 && LargeCount == 0 ? "not counted" : $"{Count(SmallCount)} and {Count(LargeCount)}";
        return $"the work at 2,500 and 10,000 tags: warm-up {warmUp}, rounds {rounds}; items {items}; {RuntimeInformation.FrameworkDescription} on {RuntimeInformation.RuntimeIdentifier}";
    }
}
