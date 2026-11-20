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
/// D198; the contrast round, docs/phase-8a-design.md 1.5, 2.3, 2.4, D410, D418): both themes hold
/// the same keys; one table says which text lies on which background, and in both themes every
/// pair of it reaches 4.5:1, with no list of gaps; the round's values, the placeholder's own token
/// at full opacity, and a label's white text on every lane. Ratios are WCAG 2's
/// (<see cref="Contrast.Ratio"/>).
/// </summary>
public class ThemeTokensTests
{
    private const double TextContrast = 4.5;

    private const double MarkContrast = 3.0;

    /// <summary>The text tokens that lie on every background of <see cref="RowBackgrounds"/>.</summary>
    private static readonly string[] TextTokens =
        ["VcTextPrimaryBrush", "VcTextSecondaryBrush", "VcSuccessBrush", "VcWarningBrush", "VcDangerBrush"];

    /// <summary>
    /// Every background a text lies on: the window, the panels, the chrome, a control at rest (the
    /// Remotes dialog's "Remove", <c>VcActionButton</c>), a hovered row or highlighted menu item, a
    /// selected row, and a hovered row of the pages and the dashboard (<c>VcRowHoverBrush</c>).
    /// </summary>
    private static readonly string[] RowBackgrounds =
        ["VcWindowBackgroundBrush", "VcPanelBackgroundBrush", "VcChromeBackgroundBrush", "VcControlBackgroundBrush", "VcHoverBackgroundBrush", "VcSelectionBrush", "VcRowHoverBrush"];

    /// <summary>
    /// The accent's backgrounds as text: never a hovered or selected row (<c>VcHoverBackgroundBrush</c>,
    /// <c>VcSelectionBrush</c>), where the row's styles draw <c>VcAccentOnRowBrush</c> instead (D418).
    /// </summary>
    private static readonly string[] AccentBackgrounds =
        ["VcWindowBackgroundBrush", "VcPanelBackgroundBrush", "VcChromeBackgroundBrush", "VcControlBackgroundBrush", "VcRowHoverBrush"];

    /// <summary>The backgrounds of a hovered or selected row, which <c>VcAccentOnRowBrush</c> lies on.</summary>
    private static readonly string[] OnRowBackgrounds = ["VcHoverBackgroundBrush", "VcSelectionBrush"];

    /// <summary>A text box's backgrounds at rest, under the pointer and focused; a disabled box keeps Fluent's (ruling 29).</summary>
    private static readonly string[] BoxBackgrounds = ["TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused"];

    private static readonly string[] WarningBackgrounds = ["VcBannerBackgroundBrush", "VcConflictMarkerBrush"];

    private static readonly string[] PlaceholderKeys =
        ["TextControlPlaceholderForeground", "TextControlPlaceholderForegroundPointerOver", "TextControlPlaceholderForegroundFocused"];

    private static readonly string[] Lanes = [.. Enumerable.Range(0, 8).Select(lane => string.Create(CultureInfo.InvariantCulture, $"VcLane{lane}Brush"))];

    private static readonly ThemeVariant[] Variants = [ThemeVariant.Dark, ThemeVariant.Light];

    /// <summary>
    /// Which text lies on which background (design 2.4): each text token on each background it is
    /// drawn on, in both themes. Every pair must reach 4.5:1; there is no list of gaps (D410).
    /// </summary>
    private static IEnumerable<(string Text, string Background)> TextPairs()
    {
        foreach (var text in TextTokens)
        {
            foreach (var background in RowBackgrounds)
            {
                yield return (text, background);
            }
        }

        foreach (var background in AccentBackgrounds)
        {
            yield return ("VcAccentBrush", background);
        }

        foreach (var background in OnRowBackgrounds)
        {
            yield return ("VcAccentOnRowBrush", background);
        }

        foreach (var background in BoxBackgrounds)
        {
            yield return ("VcTextPlaceholderBrush", background);
            yield return ("TextControlForeground", background);
        }

        foreach (var background in WarningBackgrounds)
        {
            yield return ("VcWarningBrush", background);
        }

        yield return ("VcAccentTextBrush", "VcAccentBrush");
        yield return ("VcDangerTextBrush", "VcDangerBrush");
        yield return ("ToolTipForeground", "ToolTipBackground");
    }

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

        // The contrast round's two new tokens (D418).
        Assert.Contains("VcTextPlaceholderBrush", dark);
        Assert.Contains("VcAccentOnRowBrush", dark);
    }

    [AvaloniaFact]
    public void Phase_6s_values_are_the_designs()
    {
        foreach (var (key, dark, light) in new[]
        {
            ("VcSuccessBrush", "#3FB97F", "#187248"),
            ("VcWarningBrush", "#E0A23B", "#895A17"),
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
        foreach (var variant in Variants)
        {
            Assert.Equal(Token("VcPanelBackgroundBrush", variant), Token("ToolTipBackground", variant));
            Assert.Equal(Token("VcTextPrimaryBrush", variant), Token("ToolTipForeground", variant));
            Assert.Equal(Token("VcBorderBrush", variant), Token("ToolTipBorderBrush", variant));
        }
    }

    /// <summary>
    /// The contrast round's values (design 1.5, D418): the placeholder's own token and the three
    /// keys Fluent's text box reads, the dark danger raised along its hue with the dark label, and
    /// the accent of a hovered or selected row; lane 3 and the accent itself unchanged, so that
    /// neither raise reaches the graph or the accent's other uses, and the light accent on a row
    /// the light accent.
    /// </summary>
    [AvaloniaFact]
    public void The_contrast_rounds_values_are_the_designs()
    {
        var failures = new List<string>();
        foreach (var (key, dark, light) in new[]
        {
            ("VcTextPlaceholderBrush", "#8F97A4", "#68707D"),
            ("TextControlPlaceholderForeground", "#8F97A4", "#68707D"),
            ("TextControlPlaceholderForegroundPointerOver", "#8F97A4", "#68707D"),
            ("TextControlPlaceholderForegroundFocused", "#8F97A4", "#68707D"),
            ("VcDangerBrush", "#EA7D86", "#B7323D"),
            ("VcDangerTextBrush", "#10121A", "#FFFFFF"),
            ("VcAccentOnRowBrush", "#8B9AFF", "#4353D8"),
            ("VcLane3Brush", "#E5606B", "#C93A46"),
            ("VcAccentBrush", "#7C8CFF", "#4353D8"),
        })
        {
            ExpectValue(failures, key, ThemeVariant.Dark, dark);
            ExpectValue(failures, key, ThemeVariant.Light, light);
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
        Assert.Equal(Token("VcAccentBrush", ThemeVariant.Light), Token("VcAccentOnRowBrush", ThemeVariant.Light));
    }

    /// <summary>
    /// Every pair of <see cref="TextPairs"/> reaches 4.5:1 in both themes (D410, D418), and the
    /// lowest figures of each token the round set, with phase 6's light ones, are as design 1.5 and
    /// 2.3 measured them, to two places.
    /// </summary>
    [AvaloniaFact]
    public void Every_text_reaches_4_5_to_1_on_every_background_it_lies_on()
    {
        var failures = new List<string>();
        foreach (var variant in Variants)
        {
            foreach (var (text, background) in TextPairs())
            {
                Check(failures, variant, text, background, TextContrast);
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));

        var dark = ThemeVariant.Dark;
        var light = ThemeVariant.Light;
        foreach (var (text, background, variant, ratio) in new[]
        {
            // The round's lowest figures (design 1.5, 2.3).
            ("VcTextPlaceholderBrush", "TextControlBackground", dark, 4.75),
            ("VcTextPlaceholderBrush", "TextControlBackground", light, 5.00),
            ("VcDangerBrush", "VcHoverBackgroundBrush", dark, 4.55),
            ("VcDangerBrush", "VcSelectionBrush", dark, 4.70),
            ("VcDangerBrush", "VcControlBackgroundBrush", dark, 5.18),
            ("VcDangerTextBrush", "VcDangerBrush", dark, 6.92),
            ("VcAccentOnRowBrush", "VcHoverBackgroundBrush", dark, 4.78),
            ("VcAccentOnRowBrush", "VcSelectionBrush", dark, 4.94),
            ("VcAccentOnRowBrush", "VcHoverBackgroundBrush", light, 4.66),
            ("VcAccentBrush", "VcControlBackgroundBrush", dark, 4.70),
            ("VcAccentBrush", "VcChromeBackgroundBrush", light, 5.17),

            // Phase 6's light figures (11.10, 11.12), on the hover row where each is least.
            ("VcWarningBrush", "VcHoverBackgroundBrush", light, 4.57),
            ("VcSuccessBrush", "VcHoverBackgroundBrush", light, 4.57),
            ("VcDangerBrush", "VcHoverBackgroundBrush", light, 4.57),
            ("VcDangerTextBrush", "VcDangerBrush", light, 5.94),
            ("ToolTipForeground", "ToolTipBackground", light, 15.27),
            ("ToolTipForeground", "ToolTipBackground", dark, 13.61),
        })
        {
            Assert.True(
                Math.Round(Ratio(text, background, variant), 2) == ratio,
                string.Create(CultureInfo.InvariantCulture, $"{variant}: {text} on {background} is {Ratio(text, background, variant):0.00}:1, not {ratio:0.00}:1."));
        }
    }

    /// <summary>
    /// The placeholder's own token at full opacity (design 1.5, 2.3; ruling 24): Fluent's text box
    /// draws <c>PART_Placeholder</c> in <c>TextControlPlaceholderForeground</c> at
    /// <c>TextControlPlaceholderOpacity</c>, 0.5 in Fluent's own resources; the app's keys win, so the
    /// three keys are the token and the opacity is 1 in both variants. On a box the placeholder lies
    /// between the disabled and the secondary text (D418: the Summary box's counter, in the secondary
    /// colour, must not read as its placeholder). A disabled box keeps Fluent's background (ruling 29).
    /// </summary>
    [AvaloniaFact]
    public void The_placeholder_has_its_own_token_at_full_opacity()
    {
        foreach (var variant in Variants)
        {
            foreach (var key in PlaceholderKeys)
            {
                Assert.True(TryToken(key, variant, out var colour) && TryToken("VcTextPlaceholderBrush", variant, out var token) && colour == token, $"{key} is not VcTextPlaceholderBrush in the {variant} theme.");
            }

            Assert.True(
                Application.Current!.TryGetResource("TextControlPlaceholderOpacity", variant, out var opacity) && opacity is double value && value == 1.0,
                $"TextControlPlaceholderOpacity is {opacity} in the {variant} theme, not 1.");

            var disabled = Ratio("VcTextDisabledBrush", "TextControlBackground", variant);
            var placeholder = Ratio("VcTextPlaceholderBrush", "TextControlBackground", variant);
            var secondary = Ratio("VcTextSecondaryBrush", "TextControlBackground", variant);
            var typed = Ratio("TextControlForeground", "TextControlBackground", variant);
            Assert.True(
                disabled < placeholder && placeholder < secondary && secondary < typed,
                string.Create(CultureInfo.InvariantCulture, $"{variant}: on a box the disabled text is {disabled:0.00}:1, the placeholder {placeholder:0.00}:1, the secondary text {secondary:0.00}:1 and the typed text {typed:0.00}:1, not in that order."));
        }

        // Design 2.3's figures, dark then light.
        Assert.Equal((2.46, 4.75, 5.51, 11.30), BoxFigures(ThemeVariant.Dark));
        Assert.Equal((2.40, 5.00, 5.90, 16.51), BoxFigures(ThemeVariant.Light));

        Assert.DoesNotContain("TextControlBackgroundDisabled", Keys(ThemeVariant.Dark));
        Assert.DoesNotContain("TextControlBackgroundDisabled", Keys(ThemeVariant.Light));
        Assert.False(TokensDictionary().ContainsKey("TextControlBackgroundDisabled"), "Theme/Tokens.axaml sets TextControlBackgroundDisabled outside the themes.");
    }

    [AvaloniaFact]
    public void A_labels_text_reaches_4_5_to_1_on_every_lane_in_both_themes_and_a_light_lane_3_to_1_as_a_line()
    {
        var failures = new List<string>();
        foreach (var variant in Variants)
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

    /// <summary>The disabled, placeholder, secondary and typed text's ratios on a box, to two places.</summary>
    private static (double Disabled, double Placeholder, double Secondary, double Typed) BoxFigures(ThemeVariant variant) =>
        (Math.Round(Ratio("VcTextDisabledBrush", "TextControlBackground", variant), 2),
         Math.Round(Ratio("VcTextPlaceholderBrush", "TextControlBackground", variant), 2),
         Math.Round(Ratio("VcTextSecondaryBrush", "TextControlBackground", variant), 2),
         Math.Round(Ratio("TextControlForeground", "TextControlBackground", variant), 2));

    /// <summary>Adds a failure when <paramref name="text"/> on <paramref name="background"/> is below <paramref name="needed"/>, or either token is missing.</summary>
    private static void Check(List<string> failures, ThemeVariant variant, string text, string background, double needed)
    {
        if (!TryToken(text, variant, out var foreground) || !TryToken(background, variant, out var behind))
        {
            failures.Add($"{variant}: {text} on {background}: no such solid token.");
            return;
        }

        var ratio = Contrast.Ratio(foreground, behind);
        if (ratio < needed)
        {
            failures.Add(string.Create(CultureInfo.InvariantCulture, $"{variant}: {text} on {background} is {ratio:0.00}:1, below {needed}:1."));
        }
    }

    /// <summary>Adds a failure when <paramref name="key"/> is missing or not <paramref name="expected"/> in <paramref name="variant"/>.</summary>
    private static void ExpectValue(List<string> failures, string key, ThemeVariant variant, string expected)
    {
        if (!TryToken(key, variant, out var colour))
        {
            failures.Add($"{key} ({variant}) is missing, not {expected}.");
        }
        else if (colour != Color.Parse(expected))
        {
            failures.Add($"{key} ({variant}) is {colour}, not {expected}.");
        }
    }

    private static double Ratio(string text, string background, ThemeVariant variant) =>
        Contrast.Ratio(Token(text, variant), Token(background, variant));

    /// <summary>A solid token of the application's resources in <paramref name="variant"/>.</summary>
    private static Color Token(string key, ThemeVariant variant) =>
        TryToken(key, variant, out var colour) ? colour : throw new InvalidOperationException($"No solid brush {key} in the {variant} theme.");

    private static bool TryToken(string key, ThemeVariant variant, out Color colour)
    {
        if (Application.Current!.TryGetResource(key, variant, out var value) && value is ISolidColorBrush brush)
        {
            colour = brush.Color;
            return true;
        }

        colour = default;
        return false;
    }

    /// <summary>
    /// <c>Theme/Tokens.axaml</c> as the application merged it (App.axaml): the merged dictionary with
    /// theme dictionaries that holds the window's token.
    /// </summary>
    private static ResourceDictionary TokensDictionary() =>
        Application.Current!.Resources.MergedDictionaries
            .Select(provider => provider is ResourceInclude include ? include.Loaded : provider)
            .OfType<ResourceDictionary>()
            .Single(dictionary => dictionary.ThemeDictionaries.TryGetValue(ThemeVariant.Dark, out var dark)
                && dark is ResourceDictionary darkTokens
                && darkTokens.ContainsKey("VcWindowBackgroundBrush"));

    /// <summary>The keys of one theme's dictionary of <c>Theme/Tokens.axaml</c>.</summary>
    private static List<string> Keys(ThemeVariant variant)
    {
        var theme = TokensDictionary().ThemeDictionaries[variant] as ResourceDictionary
            ?? throw new InvalidOperationException($"Theme/Tokens.axaml has no {variant} dictionary.");
        return [.. theme.Keys.Select(key => key.ToString()!).Order(StringComparer.Ordinal)];
    }
}
