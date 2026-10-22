using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The colour tokens of <c>Theme/Tokens.axaml</c> (docs/phase-6-design.md 11.10 to 11.13, 15.3,
/// D198): both themes hold the same keys; in the light theme every text token reaches 4.5:1 on
/// every background its text lies on, hovered and selected rows included, the warning also on the
/// banner and the conflict marker, and a label's white text on every lane; the dark pairs that
/// reached 4.5:1 before phase 6 still do. The dark theme's known gaps are left to phase 8's
/// accessibility pass with the owner's consent (D198) and are named here, not asserted at 4.5:1.
/// Ratios are WCAG 2's (<see cref="Contrast.Ratio"/>).
/// </summary>
public class ThemeTokensTests
{
    private const double TextContrast = 4.5;

    private const double MarkContrast = 3.0;

    private static readonly string[] TextTokens =
        ["VcTextPrimaryBrush", "VcTextSecondaryBrush", "VcAccentBrush", "VcSuccessBrush", "VcWarningBrush", "VcDangerBrush"];

    private static readonly string[] Backgrounds =
        ["VcWindowBackgroundBrush", "VcPanelBackgroundBrush", "VcChromeBackgroundBrush", "VcHoverBackgroundBrush", "VcSelectionBrush"];

    private static readonly string[] WarningBackgrounds = ["VcBannerBackgroundBrush", "VcConflictMarkerBrush"];

    private static readonly string[] Lanes = [.. Enumerable.Range(0, 8).Select(lane => string.Create(CultureInfo.InvariantCulture, $"VcLane{lane}Brush"))];

    /// <summary>
    /// The dark theme's gaps (11.10, 11.11), left to phase 8 (D198): the danger colour on a hovered
    /// (3.64) and a selected row (3.76), the accent on both (4.13, 4.26), and the danger button's
    /// white label (3.38).
    /// </summary>
    private static readonly (string Text, string Background, double Ratio)[] DarkKnownGaps =
    [
        ("VcDangerBrush", "VcHoverBackgroundBrush", 3.64),
        ("VcDangerBrush", "VcSelectionBrush", 3.76),
        ("VcAccentBrush", "VcHoverBackgroundBrush", 4.13),
        ("VcAccentBrush", "VcSelectionBrush", 4.26),
        ("VcDangerTextBrush", "VcDangerBrush", 3.38),
    ];

    [AvaloniaFact]
    public void Both_themes_hold_the_same_keys()
    {
        var dark = Keys(ThemeVariant.Dark);
        var light = Keys(ThemeVariant.Light);

        Assert.Equal(dark, light);
        Assert.Contains("VcDangerTextBrush", dark);
        Assert.Contains("ToolTipBackground", dark);
        Assert.Contains("ToolTipForeground", dark);
        Assert.Contains("ToolTipBorderBrush", dark);
    }

    [AvaloniaFact]
    public void Phase_6s_values_are_the_designs()
    {
        foreach (var (key, dark, light) in new[]
        {
            ("VcSuccessBrush", "#3FB97F", "#187248"),
            ("VcWarningBrush", "#E0A23B", "#895A17"),
            ("VcDangerBrush", "#E5606B", "#B7323D"),
            ("VcDangerTextBrush", "#FFFFFF", "#FFFFFF"),
            ("VcLane1Brush", "#3FB97F", "#1C8655"),
            ("VcLane2Brush", "#E0A23B", "#A16A1B"),
            ("VcLane3Brush", "#E5606B", "#C93A46"),
            ("VcLane4Brush", "#4FB3D9", "#1D7EA6"),
            ("VcLane7Brush", "#8FB84A", "#59811C"),
            ("ToolTipBackground", "#1A1D24", "#F5F6F8"),
            ("ToolTipForeground", "#E4E7EC", "#1B1F27"),
            ("ToolTipBorderBrush", "#323845", "#D2D6DE"),
        })
        {
            Assert.True(Color.Parse(dark) == Token(key, ThemeVariant.Dark), $"{key} (dark) is {Token(key, ThemeVariant.Dark)}, not {dark}.");
            Assert.True(Color.Parse(light) == Token(key, ThemeVariant.Light), $"{key} (light) is {Token(key, ThemeVariant.Light)}, not {light}.");
        }

        // The tooltips' keys are copies of the panel's tokens (11.12).
        foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            Assert.Equal(Token("VcPanelBackgroundBrush", variant), Token("ToolTipBackground", variant));
            Assert.Equal(Token("VcTextPrimaryBrush", variant), Token("ToolTipForeground", variant));
            Assert.Equal(Token("VcBorderBrush", variant), Token("ToolTipBorderBrush", variant));
        }
    }

    [AvaloniaFact]
    public void Light_text_reaches_4_5_to_1_on_every_background_it_lies_on()
    {
        var light = ThemeVariant.Light;
        var failures = new List<string>();
        foreach (var text in TextTokens)
        {
            foreach (var background in Backgrounds)
            {
                Check(failures, light, text, background, TextContrast);
            }
        }

        foreach (var background in WarningBackgrounds)
        {
            Check(failures, light, "VcWarningBrush", background, TextContrast);
        }

        Check(failures, light, "VcAccentTextBrush", "VcAccentBrush", TextContrast);
        Check(failures, light, "VcDangerTextBrush", "VcDangerBrush", TextContrast);
        Check(failures, light, "ToolTipForeground", "ToolTipBackground", TextContrast);
        Assert.Empty(failures);

        // The figures of 11.10's table, on the hover row where each is least.
        Assert.Equal(4.57, Ratio("VcWarningBrush", "VcHoverBackgroundBrush", light), 2);
        Assert.Equal(4.57, Ratio("VcSuccessBrush", "VcHoverBackgroundBrush", light), 2);
        Assert.Equal(4.57, Ratio("VcDangerBrush", "VcHoverBackgroundBrush", light), 2);
        Assert.Equal(5.94, Ratio("VcDangerTextBrush", "VcDangerBrush", light), 2);
        Assert.Equal(15.27, Ratio("ToolTipForeground", "ToolTipBackground", light), 2);
    }

    [AvaloniaFact]
    public void A_labels_text_reaches_4_5_to_1_on_every_lane_in_both_themes_and_a_light_lane_3_to_1_as_a_line()
    {
        var failures = new List<string>();
        foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            foreach (var lane in Lanes)
            {
                Check(failures, variant, "VcLabelTextBrush", lane, TextContrast);
            }
        }

        // A lane is also a line on the graph's background, a mark that is not text (11.13).
        foreach (var lane in Lanes)
        {
            Check(failures, ThemeVariant.Light, lane, "VcWindowBackgroundBrush", MarkContrast);
        }

        Assert.Empty(failures);
    }

    [AvaloniaFact]
    public void The_dark_pairs_still_reach_4_5_to_1_but_the_gaps_left_to_phase_8()
    {
        var dark = ThemeVariant.Dark;
        var failures = new List<string>();
        foreach (var text in TextTokens)
        {
            foreach (var background in Backgrounds)
            {
                if (!DarkKnownGaps.Any(gap => gap.Text == text && gap.Background == background))
                {
                    Check(failures, dark, text, background, TextContrast);
                }
            }
        }

        foreach (var background in WarningBackgrounds)
        {
            Check(failures, dark, "VcWarningBrush", background, TextContrast);
        }

        Check(failures, dark, "VcAccentTextBrush", "VcAccentBrush", TextContrast);
        Check(failures, dark, "ToolTipForeground", "ToolTipBackground", TextContrast);
        Assert.Empty(failures);

        // The known gaps are as 11.10 and 11.11 measured them: if one changes, D198's list changes.
        foreach (var (text, background, ratio) in DarkKnownGaps)
        {
            Assert.Equal(ratio, Ratio(text, background, dark), 2);
        }

        Assert.Equal(13.61, Ratio("ToolTipForeground", "ToolTipBackground", dark), 2);
    }

    private static void Check(List<string> failures, ThemeVariant variant, string text, string background, double needed)
    {
        var ratio = Ratio(text, background, variant);
        if (ratio < needed)
        {
            failures.Add(string.Create(CultureInfo.InvariantCulture, $"{variant}: {text} on {background} is {ratio:0.00}:1, below {needed}:1."));
        }
    }

    private static double Ratio(string text, string background, ThemeVariant variant) =>
        Contrast.Ratio(Token(text, variant), Token(background, variant));

    /// <summary>A solid token of the application's resources in <paramref name="variant"/>.</summary>
    private static Color Token(string key, ThemeVariant variant) =>
        Application.Current!.TryGetResource(key, variant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No solid brush {key} in the {variant} theme.");

    /// <summary>
    /// The keys of one theme's dictionary of <c>Theme/Tokens.axaml</c>, as the application merged
    /// it (App.axaml): the merged dictionary with theme dictionaries that holds the window's token.
    /// </summary>
    private static List<string> Keys(ThemeVariant variant)
    {
        var tokens = Application.Current!.Resources.MergedDictionaries
            .Select(provider => provider is ResourceInclude include ? include.Loaded : provider)
            .OfType<ResourceDictionary>()
            .Single(dictionary => dictionary.ThemeDictionaries.TryGetValue(ThemeVariant.Dark, out var dark)
                && dark is ResourceDictionary darkTokens
                && darkTokens.ContainsKey("VcWindowBackgroundBrush"));
        var theme = tokens.ThemeDictionaries[variant] as ResourceDictionary
            ?? throw new InvalidOperationException($"Theme/Tokens.axaml has no {variant} dictionary.");
        return [.. theme.Keys.Select(key => key.ToString()!).Order(StringComparer.Ordinal)];
    }
}
