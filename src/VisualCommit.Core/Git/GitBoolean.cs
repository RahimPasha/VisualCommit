namespace VisualCommit.Core.Git;

/// <summary>A configuration value read as git reads a boolean (docs/phase-6-design.md 8.7, facts 0.3).</summary>
public static class GitBoolean
{
    /// <summary>
    /// Whether <paramref name="value"/> is a boolean to git, and which: a bare key
    /// (<paramref name="bare"/>) is true and the empty value false; <c>true</c>, <c>yes</c>,
    /// <c>on</c>, <c>false</c>, <c>no</c>, <c>off</c> ignoring case; an integer as git parses it
    /// (a sign, decimal, <c>0x</c> hex or leading-<c>0</c> octal, a unit <c>k</c>, <c>m</c> or
    /// <c>g</c>, fitting in 32 bits), not zero true; a unit letter alone is none.
    /// </summary>
    /// <remarks>
    /// The integer follows git's <c>git_parse_signed</c> (C's <c>strtoimax</c> with base 0, then the
    /// unit): white space before it is skipped and none may follow it (" 1" is true, "1 " none);
    /// <c>08</c>, <c>0x</c> and <c>1.5</c> are none; the value times its unit must lie within a
    /// 32-bit int, as Git 2.55 checks it (Git 2.36 also refuses the int's minimum, -2147483648,
    /// a value no setting has). A unit letter alone (<c>k</c>, <c>M</c>) is none: Git 2.36 reads it
    /// as false and newer gits refuse it (0.3), which <see cref="PullModes.Read(string?, bool, string?)"/> shows.
    /// A null <paramref name="value"/> that is not <paramref name="bare"/> is an unset key: none.
    /// </remarks>
    public static bool TryParse(string? value, bool bare, out bool result)
    {
        result = false;
        if (bare)
        {
            result = true;
            return true;
        }

        if (value is null)
        {
            return false;
        }

        if (value.Length == 0)
        {
            return true;
        }

        if (IsWord(value, "true") || IsWord(value, "yes") || IsWord(value, "on"))
        {
            result = true;
            return true;
        }

        if (IsWord(value, "false") || IsWord(value, "no") || IsWord(value, "off"))
        {
            return true;
        }

        if (!TryParseInt(value, out var number))
        {
            return false;
        }

        result = number != 0;
        return true;
    }

    /// <summary>Whether <paramref name="value"/> is one unit letter alone (<c>k</c>, <c>m</c>, <c>g</c> in either case), which the two gits read differently (0.3).</summary>
    public static bool IsUnitLetterAlone(string? value) =>
        value is { Length: 1 } && value[0] is 'k' or 'K' or 'm' or 'M' or 'g' or 'G';

    private static bool IsWord(string value, string word) => string.Equals(value, word, StringComparison.OrdinalIgnoreCase);

    /// <summary>An integer as git's <c>git_parse_int</c> reads it: <c>strtoimax</c> with base 0, a unit, the result within an int.</summary>
    private static bool TryParseInt(string value, out long number)
    {
        number = 0;
        var i = 0;

        // strtoimax skips C's white space first.
        while (i < value.Length && value[i] is ' ' or '\t' or '\n' or '\v' or '\f' or '\r')
        {
            i++;
        }

        var negative = false;
        if (i < value.Length && value[i] is '+' or '-')
        {
            negative = value[i] == '-';
            i++;
        }

        var radix = 10;
        if (i + 1 < value.Length && value[i] == '0' && value[i + 1] is 'x' or 'X' && i + 2 < value.Length && DigitOf(value[i + 2], 16) >= 0)
        {
            radix = 16;
            i += 2;
        }
        else if (i < value.Length && value[i] == '0')
        {
            radix = 8;
        }

        var start = i;
        var magnitude = 0L;
        while (i < value.Length && DigitOf(value[i], radix) is var digit && digit >= 0)
        {
            // Beyond what an int with any unit can hold: strtoimax's range error, or the range check below.
            magnitude = magnitude * radix + digit;
            if (magnitude > (long)int.MaxValue + 1)
            {
                return false;
            }

            i++;
        }

        if (i == start)
        {
            // No digits: "k", "-", "0x" with nothing after it was read as "0" with the unit "x".
            return false;
        }

        long factor = (value.Length - i) switch
        {
            0 => 1,
            1 => value[i] switch
            {
                'k' or 'K' => 1024L,
                'm' or 'M' => 1024L * 1024,
                'g' or 'G' => 1024L * 1024 * 1024,
                _ => 0,
            },
            _ => 0,
        };

        if (factor == 0)
        {
            return false;
        }

        var signed = negative ? -magnitude : magnitude;
        var total = signed * factor;
        if (total < int.MinValue || total > int.MaxValue)
        {
            return false;
        }

        number = total;
        return true;
    }

    private static int DigitOf(char c, int radix)
    {
        var digit = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
        return digit < radix ? digit : -1;
    }
}
