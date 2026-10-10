using System.Globalization;
using Avalonia.Media;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using VisualCommit.Core.Diff;
using TmFontStyle = TextMateSharp.Themes.FontStyle;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// Which version of the file a diff line is read in: the old side is the context and removed
/// lines, the new side the context and added lines.
/// </summary>
public enum DiffVersion
{
    Old,
    New,
}

/// <summary>How a run of text is drawn: its colour from the TextMate theme (null: the main text colour) and its font style.</summary>
public readonly record struct SyntaxStyle(Color? Foreground, bool Bold, bool Italic, bool Underline, bool Strikethrough)
{
    /// <summary>Text the theme leaves alone.</summary>
    public static SyntaxStyle Plain => default;
}

/// <summary>A run of a line's text with one style: characters [Start, Start + Length).</summary>
public readonly record struct SyntaxRun(int Start, int Length, SyntaxStyle Style);

/// <summary>
/// Syntax highlighting of a diff (D64), with TextMateSharp's grammars and Visual Studio Code's
/// Dark+ and Light+ themes, called directly rather than through AvaloniaEdit.TextMate. Each side
/// of the diff is tokenized on its own, its lines in order across all hunks from the grammar's
/// initial state, and each line takes its colours from its own side.
/// </summary>
/// <remarks>
/// The registry, the grammars and the two themes are loaded once per process and shared: loading
/// them takes a noticeable time. Grammars are not safe for use by two threads at once, so each
/// line is tokenized under the grammar's lock; a tokenization on a background thread then holds
/// up one on the UI thread by one line at most.
/// </remarks>
public static class DiffHighlighter
{
    /// <summary>The time a single line may take to tokenize; the rest of a line that takes longer is left plain.</summary>
    public static readonly TimeSpan LineTimeLimit = TimeSpan.FromMilliseconds(50);

    /// <summary>Lines longer than this are not tokenized: they show in the main text colour.</summary>
    public const int MaxLineLength = 5_000;

    private static readonly Lock CatalogGate = new();
    private static readonly Dictionary<string, Grammar?> GrammarsByExtension = new(StringComparer.Ordinal);
    private static RegistryOptions? _options;
    private static Registry? _registry;

    /// <summary>
    /// Tokenizes both sides of <paramref name="diff"/> with the grammar that <paramref name="path"/>'s
    /// extension picks. Null when the path is null or has no grammar: the file shows without
    /// syntax colours.
    /// </summary>
    public static DiffSyntax? Tokenize(FileDiff diff, string? path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var grammar = GrammarFor(path);
        if (grammar is null)
        {
            return null;
        }

        var scopes = new ScopeTable();
        var old = TokenizeSide(diff, grammar, DiffLineKind.Added, scopes, cancellationToken);
        var @new = TokenizeSide(diff, grammar, DiffLineKind.Removed, scopes, cancellationToken);
        return new DiffSyntax(old, @new, scopes.Lists);
    }

    /// <summary>Whether <paramref name="path"/>'s extension has a grammar.</summary>
    public static bool HasGrammar(string? path) => GrammarFor(path) is not null;

    /// <summary>The style the theme gives a token with these scopes (D64): of the rules it matches, the first with a colour gives the colour and the first with a font style the style.</summary>
    internal static SyntaxStyle Resolve(bool dark, IList<string> scopes)
    {
        var theme = dark ? Themes.Dark : Themes.Light;
        List<ThemeTrieElementRule> rules;
        lock (theme)
        {
            rules = theme.Match(scopes);
        }

        var foreground = 0;
        var fontStyle = 0;
        foreach (var rule in rules)
        {
            if (foreground == 0 && rule.foreground > 0)
            {
                foreground = rule.foreground;
            }

            if (fontStyle == 0 && rule.fontStyle > 0)
            {
                fontStyle = (int)rule.fontStyle;
            }
        }

        var color = foreground > 0 ? ParseColor(theme.GetColor(foreground)) : null;
        var style = (TmFontStyle)fontStyle;
        return new SyntaxStyle(
            color,
            style.HasFlag(TmFontStyle.Bold),
            style.HasFlag(TmFontStyle.Italic),
            style.HasFlag(TmFontStyle.Underline),
            style.HasFlag(TmFontStyle.Strikethrough));
    }

    /// <summary>A TextMate colour, <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</summary>
    private static Color? ParseColor(string? text)
    {
        if (string.IsNullOrEmpty(text) || text[0] != '#')
        {
            return null;
        }

        var hex = text[1..];
        if (hex.Length == 8)
        {
            // TextMate puts the alpha last; Avalonia reads it first.
            hex = string.Concat(hex.AsSpan(6, 2), hex.AsSpan(0, 6));
        }

        return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? hex.Length == 6 ? Color.FromUInt32(0xFF000000 | value) : Color.FromUInt32(value)
            : null;
    }

    private static Grammar? GrammarFor(string? path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        lock (CatalogGate)
        {
            if (GrammarsByExtension.TryGetValue(extension, out var known))
            {
                return known;
            }

            _options ??= new RegistryOptions(ThemeName.DarkPlus);
            _registry ??= new Registry(_options);
            var scope = _options.GetScopeByExtension(extension) ?? _options.GetScopeByExtension(extension.ToLowerInvariant());
            var loaded = scope is null ? null : _registry.LoadGrammar(scope);
            var grammar = loaded is null ? null : new Grammar(loaded);
            GrammarsByExtension[extension] = grammar;
            return grammar;
        }
    }

    /// <summary>The tokens of one side's lines, by hunk and line; null for a line of the other side or one left plain.</summary>
    private static Token[]?[][] TokenizeSide(FileDiff diff, Grammar grammar, DiffLineKind otherSide, ScopeTable scopes, CancellationToken cancellationToken)
    {
        var result = new Token[]?[diff.Hunks.Count][];
        IStateStack? state = null;
        for (var h = 0; h < diff.Hunks.Count; h++)
        {
            var lines = diff.Hunks[h].Lines;
            result[h] = new Token[]?[lines.Count];
            for (var l = 0; l < lines.Count; l++)
            {
                var line = lines[l];
                if (line.Kind == otherSide || line.Text.Length > MaxLineLength)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                ITokenizeLineResult tokenized;
                lock (grammar)
                {
                    tokenized = grammar.Inner.TokenizeLine(new LineText(line.Text), state, LineTimeLimit);
                }

                state = tokenized.RuleStack;
                result[h][l] = ToTokens(tokenized.Tokens, line.Text.Length, scopes);
            }
        }

        return result;
    }

    private static Token[] ToTokens(IToken[] tokens, int length, ScopeTable scopes)
    {
        var result = new List<Token>(tokens.Length);
        foreach (var token in tokens)
        {
            var start = Math.Min(token.StartIndex, length);
            var end = Math.Min(token.EndIndex, length);
            if (end > start)
            {
                result.Add(new Token(start, end - start, scopes.KeyOf(token.Scopes)));
            }
        }

        return [.. result];
    }

    /// <summary>A grammar and the lock its use is serialized under (the instance itself).</summary>
    private sealed class Grammar(IGrammar inner)
    {
        public IGrammar Inner { get; } = inner;
    }

    /// <summary>The two themes, loaded on first use.</summary>
    private static class Themes
    {
        public static readonly Theme Dark = new Registry(new RegistryOptions(ThemeName.DarkPlus)).GetTheme();

        public static readonly Theme Light = new Registry(new RegistryOptions(ThemeName.LightPlus)).GetTheme();
    }

    /// <summary>Gives each distinct list of scopes a number, so tokens store a number and styles are resolved once per list and theme.</summary>
    private sealed class ScopeTable
    {
        private readonly Dictionary<string, int> _keys = new(StringComparer.Ordinal);

        public List<IList<string>> Lists { get; } = [];

        public int KeyOf(List<string> scopes)
        {
            var name = string.Join(' ', scopes);
            if (!_keys.TryGetValue(name, out var key))
            {
                key = Lists.Count;
                Lists.Add(scopes);
                _keys[name] = key;
            }

            return key;
        }
    }
}

/// <summary>A token of a tokenized line: characters [Start, Start + Length) and the number of its list of scopes.</summary>
internal readonly record struct Token(int Start, int Length, int Scopes);

/// <summary>
/// The tokens of a diff's two sides (<see cref="DiffHighlighter.Tokenize"/>), with the style of
/// each token in either theme resolved when first asked for.
/// </summary>
public sealed class DiffSyntax
{
    private readonly Token[]?[][] _old;
    private readonly Token[]?[][] _new;
    private readonly IReadOnlyList<IList<string>> _scopes;
    private readonly SyntaxStyle?[] _dark;
    private readonly SyntaxStyle?[] _light;

    internal DiffSyntax(Token[]?[][] old, Token[]?[][] @new, IReadOnlyList<IList<string>> scopes)
    {
        _old = old;
        _new = @new;
        _scopes = scopes;
        _dark = new SyntaxStyle?[scopes.Count];
        _light = new SyntaxStyle?[scopes.Count];
    }

    /// <summary>
    /// The styled runs of a line as <paramref name="version"/> reads it, in order; text between and
    /// after them is plain. Empty for a line of the other side or one that was not tokenized.
    /// Called on the UI thread.
    /// </summary>
    public IReadOnlyList<SyntaxRun> RunsOf(DiffLineRef line, DiffVersion version, bool dark)
    {
        var tokens = TokensOf(line, version);
        if (tokens is null)
        {
            return [];
        }

        var runs = new List<SyntaxRun>(tokens.Length);
        foreach (var token in tokens)
        {
            var style = StyleOf(token.Scopes, dark);
            if (style != SyntaxStyle.Plain)
            {
                runs.Add(new SyntaxRun(token.Start, token.Length, style));
            }
        }

        return runs;
    }

    /// <summary>The style of the character at <paramref name="column"/> of a line's text, as <paramref name="version"/> reads it.</summary>
    public SyntaxStyle StyleAt(DiffLineRef line, DiffVersion version, int column, bool dark)
    {
        foreach (var run in RunsOf(line, version, dark))
        {
            if (column >= run.Start && column < run.Start + run.Length)
            {
                return run.Style;
            }
        }

        return SyntaxStyle.Plain;
    }

    private Token[]? TokensOf(DiffLineRef line, DiffVersion version)
    {
        var side = version == DiffVersion.Old ? _old : _new;
        return line.Hunk >= 0 && line.Hunk < side.Length && line.Line >= 0 && line.Line < side[line.Hunk].Length
            ? side[line.Hunk][line.Line]
            : null;
    }

    private SyntaxStyle StyleOf(int key, bool dark)
    {
        var cache = dark ? _dark : _light;
        return cache[key] ??= DiffHighlighter.Resolve(dark, _scopes[key]);
    }
}
