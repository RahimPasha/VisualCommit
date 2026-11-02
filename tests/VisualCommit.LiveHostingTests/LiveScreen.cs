using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VisualCommit.Hosting.AzureDevOps;

namespace VisualCommit.LiveHostingTests;

/// <summary>
/// The screen of the live command's notes for Azure DevOps and GitLab (docs/phase-7b-design.md 15.3;
/// D372, g4 D1.7): beside 7a's search for the token, it hides the organization's name, the addresses
/// and the organization's ids (its host ids, its tenant, the user's id) that the services' answers
/// hold. It learns them before a note can hold them: the organization from the variable, the rest
/// from every answer the test's own API reads (<see cref="Learn"/>, <see cref="LearnFromAnswer"/>)
/// and from the clone's authors (<see cref="LearnFromGit"/>). <see cref="Apply"/> replaces them in
/// any text; <see cref="SurvivorIn"/> says whether a text still holds one, raw or in any reading that
/// decoding JSON's string escapes, HTML's character references and percent escapes reaches, and never
/// which value.
/// Safe from any thread.
/// </summary>
internal sealed class LiveScreen
{
    public const string OrganizationPlaceholder = "<the organization>";
    public const string OrganizationIdPlaceholder = "<the organization's id>";
    public const string TenantPlaceholder = "<the tenant>";
    public const string UserIdPlaceholder = "<the user's id>";
    public const string SignInQueryPlaceholder = "<the sign-in query>";
    public const string AddressPlaceholder = "<the user's address>";
    public const string OtherAddressPlaceholder = "<an address>";

    /// <summary>D325's address, the command's own commit's: kept wherever it shows.</summary>
    public const string KeptAddress = "live-test@visualcommit.invalid";

    /// <summary>The Anonymous identity's id (g1 G0.3), a published constant: never learned, never replaced.</summary>
    public const string AnonymousId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

    /// <summary>The Git resource area's id, a published constant beside <see cref="AzureDevOpsApi.CoreAreaId"/>: never learned.</summary>
    public const string GitAreaId = "4e080c62-fa21-4fbc-8fef-2a10a2b38049";

    private static readonly string[] Placeholders =
        [OrganizationPlaceholder, OrganizationIdPlaceholder, TenantPlaceholder, UserIdPlaceholder, SignInQueryPlaceholder, AddressPlaceholder, OtherAddressPlaceholder];

    /// <summary>
    /// The zero GUID, which <c>X-VSS-ResourceTenant</c> sends for an organization without a tenant (on a
    /// 302 or a refused token's answer, and on every answer of an organization backed by a Microsoft
    /// account): no tenant, so never learned; learned, it would eat a zero object id and a zero GUID.
    /// </summary>
    public const string ZeroId = "00000000-0000-0000-0000-000000000000";

    private static readonly string[] Published = [AnonymousId, AzureDevOpsApi.CoreAreaId, GitAreaId, ZeroId];

    /// <summary>
    /// A sign-in URL's query and <c>#ctx=</c> fragment: replaced whole, so that <c>reply_to</c>,
    /// <c>hid</c> and <c>context</c> never show. It needs one character after the <c>?</c> or <c>#</c>
    /// that is not blank, a quote or a bracket, so that a second <see cref="Apply"/> leaves
    /// <c>/_signin?&lt;the sign-in query&gt;</c> as it is.
    /// </summary>
    private static readonly Regex SignInQuery = new(@"(/_signin)[?#][^\s""'<>]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>A <c>context=</c> value anywhere: replaced, and a survivor, when it is a sign-in context (<see cref="HidOfContext"/>).</summary>
    private static readonly Regex ContextValue = new(@"(?<=(?:^|[?&#;])context=)[^&#\s""'<>]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>A <c>hid=</c> value anywhere, learned or not.</summary>
    private static readonly Regex HidValue = new(@"(?<=(?:^|[?&#;])hid=)[^&#\s""'<>]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex HidOrContext = new(@"(?:^|[?&#;])(?<name>hid|context)=(?<value>[^&#\s""'<>]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TenantAfterLogin = new(@"login\.microsoftonline\.com/(?<tenant>[^/""'\s,?#]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DescriptorTenant = new(@"ClaimsIdentity;(?<tenant>[^\\;\s]+)\\", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly string[] UserParents = ["authenticatedUser", "authorizedUser", "createdBy"];

    /// <summary>A JSON string escape: <c>\uXXXX</c>, or <c>\</c> and one of <c>"\/bfnrt</c>.</summary>
    private static readonly Regex JsonEscape = new(@"\\(?:u(?<hex>[0-9A-Fa-f]{4})|(?<char>[""\\/bfnrt]))", RegexOptions.CultureInvariant);

    /// <summary>An HTML numeric character reference, decimal or hexadecimal, its leading zeros skipped and its <c>;</c> optional, as browsers read it.</summary>
    private static readonly Regex NumericReference = new(@"&#(?:[xX]0*(?<hex>[0-9A-Fa-f]{1,6})|0*(?<dec>[0-9]{1,7}));?", RegexOptions.CultureInvariant);

    /// <summary>What stands between a character reference's <c>&amp;</c> and its <c>;</c>: a decimal or hexadecimal number, or a name.</summary>
    private static readonly Regex ReferenceName = new(@"^(?:#[0-9]{1,8}|#[xX][0-9A-Fa-f]{1,8}|[A-Za-z][A-Za-z0-9]{1,31})$", RegexOptions.CultureInvariant);

    /// <summary>What an address's local part may hold besides letters and digits: RFC 5322's <c>atext</c> marks and <c>.</c>.</summary>
    private const string LocalPartMarks = "!#$%&'*+-/=?^_`{|}~.";

    /// <summary>The most readings <see cref="Decodings"/> gives of one text; past it, <see cref="SurvivorIn"/> withholds the text.</summary>
    internal const int ReadingLimit = 512;

    /// <summary>The kind <see cref="SurvivorIn"/> names for a text whose readings pass <see cref="ReadingLimit"/>.</summary>
    internal const string NestedPastTheSearch = "escapes nested past the screen's search";

    /// <summary>The three decodings <see cref="Decodings"/> applies, each once over a whole text.</summary>
    private static readonly Func<string, string>[] Decoders = [JsonDecoded, HtmlDecoded, Uri.UnescapeDataString];

    /// <summary>The quotes that end an address run unless they stand between letters, digits or <c>.</c> (<c>mary.o'connor@…</c>).</summary>
    private static readonly char[] Quotes = ['"', '\'', '`'];

    private readonly Lock _gate = new();
    private readonly List<(string Name, Regex Pattern)> _organizations = [];
    private readonly HashSet<string> _addresses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _ids = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many values the screen knows: for its tests.</summary>
    internal (int Organizations, int Addresses, int Ids) Counts
    {
        get
        {
            lock (_gate)
            {
                return (_organizations.Count, _addresses.Count, _ids.Count);
            }
        }
    }

    /// <summary>
    /// The organization's name (15.2's grammar), hidden in any case at a boundary: a text's end, a
    /// character outside <c>[A-Za-z0-9-]</c>, a <c>%XX</c> escape, a JSON string escape (<c>\n</c>,
    /// <c>\u002F</c>) or an HTML character reference (<c>&amp;#47;</c>, <c>&amp;amp;</c>); before it
    /// also an escape's start whose digits may run into the name, so that its letters never stay
    /// readable where a decoder would read them as digits: <c>&amp;#x2F</c> or <c>&amp;#x</c> without
    /// its <c>;</c> (<c>&amp;#x2Ffabrikam</c> reads as U+2FFAB), <c>&amp;#1</c>, <c>%F</c>, <c>\u00</c>.
    /// </summary>
    public void AddOrganization(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!LiveSettings.Organization.IsMatch(name))
        {
            throw new ArgumentException("An organization's name is letters, digits and '-' (g3 G3.4).", nameof(name));
        }

        var pattern = new Regex(
            @"(?<=^|[^A-Za-z0-9-]|%[0-9A-Fa-f]{1,2}|\\[bfnrt""\\/]|\\u[0-9A-Fa-f]{0,4}|&#[0-9]+;?|&#[xX][0-9A-Fa-f]*;?|&[A-Za-z][A-Za-z0-9]*;)"
                + Regex.Escape(name) + @"(?=$|[^A-Za-z0-9-]|%[0-9A-Fa-f]{2})",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        lock (_gate)
        {
            if (!_organizations.Any(known => string.Equals(known.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                _organizations.Add((name, pattern));
            }
        }
    }

    /// <summary>An address to hide, as written and URL-escaped; D325's and <c>example.com</c> ones are kept.</summary>
    public void AddAddress(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        AddCore(CoreOf(address));
    }

    private void AddCore(string? core)
    {
        if (core is null || IsKept(core))
        {
            return;
        }

        lock (_gate)
        {
            _addresses.Add(core);
        }
    }

    /// <summary>
    /// A value (a JSON string, a line of git's <c>%ae</c>): when it is address-shaped as a whole
    /// (<see cref="IsWholeAddress"/>), learned whole and never in pieces, so that an apostrophe, <c>=</c>
    /// or <c>/</c> in it (<c>mary.o'connor@…</c>, <c>pat=dev@…</c>) cuts nothing off and no piece
    /// (<c>connor@…</c>) is replaced while its head stays; its ids are learned. Otherwise its address runs
    /// and ids, as in any text (a descriptor's <c>…;&lt;tenant&gt;\&lt;address&gt;</c>, a <c>mailto:</c>).
    /// </summary>
    private void LearnValue(string value)
    {
        var whole = value.Trim();
        if (IsWholeAddress(whole))
        {
            AddCore(whole);
            LearnText(value, addresses: false);
            return;
        }

        LearnText(value);
    }

    /// <summary>
    /// Whether <paramref name="value"/> is address-shaped as a whole: one <c>@</c> (or U+FF20) with
    /// something on each side; before it only letters, digits and RFC 5322's <c>atext</c> marks and
    /// <c>.</c> (<see cref="LocalPartMarks"/>: an apostrophe, <c>=</c> or <c>/</c> is part of an address,
    /// a blank, <c>\</c>, <c>;</c>, <c>:</c>, <c>,</c> or a double quote makes a composite such as a
    /// descriptor, <c>mailto:</c> or a URL's user part); after it only letters, digits, <c>-</c>,
    /// <c>.</c> and <c>_</c>, the last no <c>.</c>.
    /// </summary>
    internal static bool IsWholeAddress(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var at = value.IndexOfAny(['@', '＠']);
        return at > 0 && at < value.Length - 1 && value.IndexOfAny(['@', '＠'], at + 1) < 0 && value[^1] != '.'
            && value[..at].All(c => char.IsLetterOrDigit(c) || LocalPartMarks.Contains(c, StringComparison.Ordinal))
            && value[(at + 1)..].All(c => char.IsLetterOrDigit(c) || c is '-' or '.' or '_');
    }

    /// <summary>An id to hide by <paramref name="placeholder"/>, in any case, with and without hyphens; a published constant or a short value is not learned.</summary>
    internal void AddId(string id, string placeholder)
    {
        ArgumentNullException.ThrowIfNull(id);
        var value = id.Trim();
        if (value.Length < 8 || Published.Any(constant => SameId(constant, value)))
        {
            return;
        }

        lock (_gate)
        {
            _ids.TryAdd(value, placeholder);
        }
    }

    /// <summary>
    /// Learns from a JSON answer before it is noted: every address in any string (<c>Account.$value</c>,
    /// <c>descriptor</c>, <c>uniqueName</c>, <c>/user</c>'s <c>email</c>, <c>commit_email</c> and
    /// <c>public_email</c>, <c>author.public_email</c> among them), a string that is address-shaped as
    /// a whole as it stands (<see cref="LearnValue"/>), <c>instanceId</c> as the
    /// organization's id, the tenant of a <c>ClaimsIdentity</c> descriptor, the user's <c>id</c> and
    /// <c>subjectDescriptor</c> under <c>authenticatedUser</c>, <c>authorizedUser</c> and
    /// <c>createdBy</c> (never the Anonymous id), and every <c>hid=</c> and sign-in <c>context=</c>.
    /// </summary>
    public void Learn(JsonElement element) => Walk(element, null, null);

    /// <summary>
    /// Learns from an answer's headers (every value of a repeated one) and from a body that is not
    /// JSON (a JSON one goes to <see cref="Learn"/>): the tenant in <c>X-VSS-ResourceTenant</c> and
    /// after <c>login.microsoftonline.com/</c> in <c>WWW-Authenticate</c>, every <c>hid=</c> and
    /// sign-in <c>context=</c> in any value or the body, and every address.
    /// </summary>
    public void LearnFromAnswer(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string? body)
    {
        ArgumentNullException.ThrowIfNull(headers);
        foreach (var (name, values) in headers)
        {
            foreach (var value in values)
            {
                if (string.Equals(name, "X-VSS-ResourceTenant", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var tenant in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        AddId(tenant, TenantPlaceholder);
                    }
                }

                LearnText(value);
            }
        }

        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            Learn(document.RootElement);
        }
        catch (JsonException)
        {
            LearnText(body);
        }
    }

    /// <summary>Learns the addresses in <c>git log --all --format=%ae%n%ce</c>'s output, each line as a whole value (<see cref="LearnValue"/>).</summary>
    public void LearnFromGit(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            LearnValue(line);
        }
    }

    /// <summary>
    /// <paramref name="text"/> screened (15.3): first a sign-in URL's query and <c>#ctx=</c> fragment,
    /// any <c>hid=</c> value and any sign-in <c>context=</c> value, before a learned id's placeholder
    /// could end the sign-in query's run and leave its <c>context=</c> behind; then each learned
    /// address in each of <see cref="AddressFormsOf"/>'s forms;
    /// each learned id in any case, with and without hyphens, as
    /// written and URL-escaped; any other address but D325's and <c>example.com</c> ones (judged before
    /// the organization is replaced, so that a URL's user part stays one); then the organization in
    /// any case at a boundary (<see cref="AddOrganization"/>). A second pass changes nothing. What an
    /// escape hides inside a value (<c>\u0066abrikam</c>) is left for <see cref="SurvivorIn"/> to find.
    /// </summary>
    public string Apply(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] addresses;
        KeyValuePair<string, string>[] ids;
        (string Name, Regex Pattern)[] organizations;
        lock (_gate)
        {
            addresses = [.. _addresses.OrderByDescending(address => address.Length)];
            ids = [.. _ids.OrderByDescending(id => id.Key.Length)];
            organizations = [.. _organizations];
        }

        var screened = SignInQuery.Replace(text, "$1?" + SignInQueryPlaceholder);
        screened = HidValue.Replace(screened, OrganizationIdPlaceholder);
        screened = ContextValue.Replace(screened, match => IsSignInContext(match.Value) ? OrganizationIdPlaceholder : match.Value);
        foreach (var address in addresses)
        {
            foreach (var form in AddressFormsOf(address))
            {
                screened = screened.Replace(form, AddressPlaceholder, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var (id, placeholder) in ids)
        {
            foreach (var form in FormsOf(id))
            {
                screened = screened.Replace(form, placeholder, StringComparison.OrdinalIgnoreCase);
            }
        }

        screened = ReplaceAddresses(screened);
        foreach (var (_, pattern) in organizations)
        {
            screened = pattern.Replace(screened, OrganizationPlaceholder);
        }

        return screened;
    }

    /// <summary>
    /// What <paramref name="text"/> (screened run.txt) still holds, or null: the organization at a
    /// boundary, a learned address or id in any form <see cref="Apply"/> replaces, an address, sign-in
    /// query or sign-in context <see cref="Apply"/> would replace, or a <c>hid=</c> value (a host id);
    /// searched raw, then in each of its readings (<see cref="Decodings"/>: every text that decoding
    /// JSON's string escapes, HTML's character references and percent escapes reaches, in any order;
    /// an invalid escape such as <c>%C0%AE</c> stays), and a text with more readings than
    /// <see cref="ReadingLimit"/> is withheld whole. The answer names the kind of value and whether
    /// percent decoding alone reached it, never the value. D374's made-up organization is no survivor.
    /// </summary>
    public string? SurvivorIn(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (FindIn(WithoutPlaceholders(text)) is { } raw)
        {
            return raw;
        }

        var (readings, complete) = Decodings(text);
        foreach (var (decoded, percentAlone) in readings)
        {
            if (FindIn(WithoutPlaceholders(decoded)) is { } kind)
            {
                return percentAlone ? $"{kind} once percent-decoded" : $"{kind} once decoded";
            }
        }

        return complete ? null : NestedPastTheSearch;
    }

    /// <summary>
    /// <paramref name="text"/>'s readings, the text itself left out: every text that the three
    /// decodings (JSON's string escapes, HTML's character references, percent escapes, each over the
    /// whole text at once) reach from it, applied in any order and any number of times, so that no
    /// order hides what another shows (<c>fabrik%61m7&amp;#65;</c> percent-decoded alone shows the name
    /// at the reference). First those that percent decoding alone reaches, marked so, then the others
    /// breadth first. A decoding that changes a text shortens it, so the readings end; <c>Complete</c>
    /// is false when they passed <see cref="ReadingLimit"/> and the rest were not given.
    /// </summary>
    internal static (IReadOnlyList<(string Text, bool PercentAlone)> Readings, bool Complete) Decodings(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var readings = new List<(string Text, bool PercentAlone)>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { text };
        var pending = new Queue<string>([text]);
        for (var current = text; ;)
        {
            var next = Uri.UnescapeDataString(current);
            if (!seen.Add(next))
            {
                break;
            }

            if (readings.Count == ReadingLimit)
            {
                return (readings, false);
            }

            readings.Add((next, true));
            pending.Enqueue(next);
            current = next;
        }

        while (pending.TryDequeue(out var reading))
        {
            foreach (var decode in Decoders)
            {
                var next = decode(reading);
                if (!seen.Add(next))
                {
                    continue;
                }

                if (readings.Count == ReadingLimit)
                {
                    return (readings, false);
                }

                readings.Add((next, false));
                pending.Enqueue(next);
            }
        }

        return (readings, true);
    }

    /// <summary><paramref name="text"/> with its JSON string escapes decoded once.</summary>
    private static string JsonDecoded(string text) =>
        !text.Contains('\\', StringComparison.Ordinal) ? text : JsonEscape.Replace(text, match => match.Groups["hex"].Success
            ? ((char)Convert.ToInt32(match.Groups["hex"].Value, 16)).ToString()
            : match.Groups["char"].Value switch
            {
                "b" => "\b",
                "f" => "\f",
                "n" => "\n",
                "r" => "\r",
                "t" => "\t",
                var other => other,
            });

    /// <summary><paramref name="text"/> with its HTML character references decoded once: numeric ones (also without <c>;</c>), <c>&amp;commat;</c>, and the named ones <see cref="System.Net.WebUtility.HtmlDecode(string)"/> knows.</summary>
    private static string HtmlDecoded(string text)
    {
        if (!text.Contains('&', StringComparison.Ordinal))
        {
            return text;
        }

        var numeric = NumericReference.Replace(text, match =>
        {
            var code = match.Groups["hex"].Success ? Convert.ToInt32(match.Groups["hex"].Value, 16) : int.Parse(match.Groups["dec"].Value, System.Globalization.CultureInfo.InvariantCulture);
            return code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF) ? char.ConvertFromUtf32(code) : match.Value;
        });
        return System.Net.WebUtility.HtmlDecode(numeric.Replace("&commat;", "@", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A learned address's forms that <see cref="Apply"/> replaces and <see cref="SurvivorIn"/> looks
    /// for: as written, URL-escaped, and with its <c>@</c> as <c>&amp;#64;</c> or <c>&amp;#x40;</c>, with
    /// or without the <c>;</c> (<c>pat.user&amp;#x40corp.test</c>, whose hexadecimal digits run into the
    /// domain, reads as U+040C and hides the address from any decoding).
    /// </summary>
    private static IEnumerable<string> AddressFormsOf(string address) =>
        new[] { address, Uri.EscapeDataString(address) }
            .Concat(new[] { "&#64;", "&#x40;", "&#64", "&#x40" }.Select(at => address.Replace("@", at, StringComparison.Ordinal)))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a data folder's file (<c>accounts.json</c>, the log) holds of the learned addresses, or
    /// null: each searched as written, URL-escaped and JSON-escaped as the app's writer escapes it
    /// (System.Text.Json's default encoder writes <c>+</c> and any letter beyond ASCII as an escape),
    /// and, when the text is one JSON document, in its decoded strings, whatever escapes wrote them.
    /// Both files name the organization (the login, the clone's command line) and the log holds git's
    /// <c>https://***@…</c> URLs, so nothing else is searched there.
    /// </summary>
    public string? SurvivorInDataFile(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] addresses;
        lock (_gate)
        {
            addresses = [.. _addresses];
        }

        if (addresses.Length == 0)
        {
            return null;
        }

        var strings = JsonStringsOf(text);
        return addresses.Any(address =>
                new[] { address, Uri.EscapeDataString(address), JsonEncodedText.Encode(address).ToString() }.Any(form => text.Contains(form, StringComparison.OrdinalIgnoreCase))
                || strings.Any(value => value.Contains(address, StringComparison.OrdinalIgnoreCase)))
            ? "a learned address"
            : null;
    }

    /// <summary>
    /// Whether <paramref name="core"/> is an address the screen keeps: D325's, or one at
    /// <c>example.com</c>, holding one <c>@</c> (or U+FF20) once percent-decoded, so that an address
    /// glued in front of a kept one (<c>a@b.test@example.com</c>, <c>a%40b.test@example.com</c>) is none.
    /// </summary>
    internal static bool IsKept(string core)
    {
        var decoded = Decoded(core);
        return decoded.Count(c => c is '@' or '＠') == 1
            && (string.Equals(decoded, KeptAddress, StringComparison.OrdinalIgnoreCase)
                || decoded.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary><paramref name="text"/> percent-decoded again until a pass changes nothing, at most eight passes.</summary>
    private static string Decoded(string text)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var next = Uri.UnescapeDataString(text);
            if (string.Equals(next, text, StringComparison.Ordinal))
            {
                break;
            }

            text = next;
        }

        return text;
    }

    /// <summary>Every property name and string value of <paramref name="text"/>, decoded, when it is one JSON document; none otherwise.</summary>
    private static List<string> JsonStringsOf(string text)
    {
        var strings = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            Collect(document.RootElement);
        }
        catch (JsonException)
        {
            strings.Clear();
        }

        return strings;

        void Collect(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        strings.Add(property.Name);
                        Collect(property.Value);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Collect(item);
                    }

                    break;
                case JsonValueKind.String:
                    strings.Add(element.GetString() ?? string.Empty);
                    break;
            }
        }
    }

    /// <summary>
    /// The address-shaped runs of <paramref name="text"/> (D368: an <c>@</c> or U+FF20 between
    /// non-blank characters, a run bounded by white space, brackets, <c>,</c>, <c>;</c> and quotes, a
    /// quote only when it does not stand between letters, digits or <c>.</c>, as in
    /// <c>mary.o'connor@…</c>, and a <c>;</c> only when it ends no character reference standing so, as in
    /// <c>mary.o&amp;#39;connor@…</c>), but for an <c>@</c> that ends a URL's user information
    /// (<c>&lt;scheme&gt;://&lt;user&gt;@</c>, git's <c>***@</c> included) or git's scp-like
    /// <c>git@&lt;host&gt;:</c>: start, length.
    /// </summary>
    internal static IReadOnlyList<(int Start, int Length)> AddressRuns(string text)
    {
        var runs = new List<(int Start, int Length)>();
        for (var at = 0; at < text.Length; at++)
        {
            if (text[at] is not ('@' or '＠'))
            {
                continue;
            }

            var start = at;
            while (start > 0 && !IsBoundAt(text, start - 1))
            {
                start--;
            }

            var end = at + 1;
            while (end < text.Length && !IsBoundAt(text, end))
            {
                end++;
            }

            if (start == at || end == at + 1 || EndsUserPart(text, start, at))
            {
                continue;
            }

            if (runs.Count > 0 && runs[^1].Start == start)
            {
                continue;
            }

            runs.Add((start, end - start));
            at = end - 1;
        }

        return runs;
    }

    private static bool IsBound(char c) =>
        char.IsWhiteSpace(c) || char.IsControl(c) || c is '"' or '\'' or '<' or '>' or '(' or ')' or '[' or ']' or '{' or '}' or ',' or ';' or '`';

    /// <summary>Whether <paramref name="text"/>'s character at <paramref name="index"/> bounds an address run: a quote between letters, digits or <c>.</c> does not, nor the <c>;</c> of a character reference standing so (<see cref="EndsReferenceInWord"/>).</summary>
    private static bool IsBoundAt(string text, int index) =>
        IsBound(text[index])
        && !(Quotes.Contains(text[index]) && index > 0 && index < text.Length - 1 && InWord(text[index - 1]) && InWord(text[index + 1]))
        && !EndsReferenceInWord(text, index);

    private static bool InWord(char c) => char.IsLetterOrDigit(c) || c == '.';

    /// <summary>
    /// Whether the <c>;</c> at <paramref name="index"/> ends a character reference (<c>&amp;#NN;</c>,
    /// <c>&amp;#xHH;</c>, <c>&amp;name;</c>) that stands between letters, digits or <c>.</c>, another
    /// reference beside it counting as one, and encodes neither a blank, a bracket, <c>,</c> nor
    /// <c>;</c> (a quote counts as in a word, as written; a name it cannot decode too): what it encodes
    /// is part of the word, as in <c>mary.o&amp;#39;connor</c> or <c>a&amp;amp;b</c>, but not in
    /// <c>Ada&amp;lt;ada@…</c>.
    /// </summary>
    private static bool EndsReferenceInWord(string text, int index)
    {
        if (index < 3 || text[index] != ';' || index == text.Length - 1 || !(InWord(text[index + 1]) || text[index + 1] == '&'))
        {
            return false;
        }

        var amp = text.LastIndexOf('&', index - 1, Math.Min(index, 34));
        if (amp <= 0 || !(InWord(text[amp - 1]) || text[amp - 1] == ';') || !ReferenceName.IsMatch(text[(amp + 1)..index]))
        {
            return false;
        }

        var reference = text[amp..(index + 1)];
        var decoded = HtmlDecoded(reference);
        return string.Equals(decoded, reference, StringComparison.Ordinal) || !decoded.Any(c => IsBound(c) && !Quotes.Contains(c));
    }

    /// <summary>Whether the <c>@</c> at <paramref name="at"/> ends a URL's user part or git's scp-like <c>git@host:</c>, judged on the run from <paramref name="start"/>.</summary>
    private static bool EndsUserPart(string text, int start, int at)
    {
        var before = text[start..at];
        var scheme = before.LastIndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0 && before[(scheme + 3)..].IndexOfAny(['/', '?', '#', '@', '＠']) < 0)
        {
            return true;
        }

        if (string.Equals(before, "git", StringComparison.Ordinal))
        {
            var host = at + 1;
            while (host < text.Length && (char.IsAsciiLetterOrDigit(text[host]) || text[host] is '.' or '-'))
            {
                host++;
            }

            return host > at + 1 && host < text.Length && text[host] == ':';
        }

        return false;
    }

    /// <summary>The address in a run: from after its last <c>\</c>, <c>/</c>, <c>:</c>, <c>=</c> or <c>;</c> before the <c>@</c> (but a <c>;</c> that ends a character reference inside a word, <see cref="EndsReferenceInWord"/>) to before the first <c>/</c>, <c>?</c>, <c>#</c>, <c>&amp;</c>, <c>\</c> or <c>:</c> after it, less a final <c>.</c>; null without one.</summary>
    internal static string? CoreOf(string run)
    {
        var at = run.IndexOfAny(['@', '＠']);
        if (at <= 0 || at == run.Length - 1)
        {
            return null;
        }

        var start = at;
        while (start > 0 && !(run[start - 1] is '\\' or '/' or ':' or '=' || (run[start - 1] == ';' && !EndsReferenceInWord(run, start - 1))))
        {
            start--;
        }

        var end = run.IndexOfAny(['/', '?', '#', '&', '\\', ':'], at + 1);
        var core = run[start..(end < 0 ? run.Length : end)].TrimEnd('.', '!', '?');
        return core.Length > 2 && core.IndexOfAny(['@', '＠']) is > 0 and var index && index < core.Length - 1 ? core : null;
    }

    /// <summary><paramref name="text"/> with every address-shaped run but kept ones replaced by <see cref="OtherAddressPlaceholder"/>.</summary>
    private static string ReplaceAddresses(string text)
    {
        var runs = AddressRuns(text);
        if (runs.Count == 0)
        {
            return text;
        }

        var screened = new StringBuilder(text.Length);
        var from = 0;
        foreach (var (start, length) in runs)
        {
            var run = text.Substring(start, length);
            var trimmed = run.TrimEnd('.', ',', ':', '!', '?');
            screened.Append(text, from, start - from);
            screened.Append(CoreOf(trimmed) is { } core && IsKept(core) && core.Length == trimmed.Length ? run : OtherAddressPlaceholder + run[trimmed.Length..]);
            from = start + length;
        }

        return screened.Append(text, from, text.Length - from).ToString();
    }

    private string? FindIn(string text)
    {
        (string Name, Regex Pattern)[] organizations;
        string[] addresses;
        string[] ids;
        lock (_gate)
        {
            organizations = [.. _organizations];
            addresses = [.. _addresses];
            ids = [.. _ids.Keys];
        }

        if (organizations.Any(organization => organization.Pattern.IsMatch(text)))
        {
            return "the organization's name";
        }

        if (addresses.Any(address => AddressFormsOf(address).Any(form => text.Contains(form, StringComparison.OrdinalIgnoreCase))))
        {
            return "a learned address";
        }

        if (ids.Any(id => FormsOf(id).Any(form => text.Contains(form, StringComparison.OrdinalIgnoreCase))))
        {
            return "a learned id";
        }

        if (SignInQuery.IsMatch(text))
        {
            return "a sign-in query";
        }

        if (ContextValue.Matches(text).Any(match => IsSignInContext(match.Value)))
        {
            return "a sign-in context";
        }

        if (HidValue.IsMatch(text))
        {
            return "a host id";
        }

        var runs = AddressRuns(text);
        return runs.Any(run => !(CoreOf(text.Substring(run.Start, run.Length).TrimEnd('.', ',', ':', '!', '?')) is { } core && IsKept(core) && core.Length == text.Substring(run.Start, run.Length).TrimEnd('.', ',', ':', '!', '?').Length))
            ? "an address"
            : null;
    }

    /// <summary>The screen's placeholders as blanks, so that a placeholder's words never pass for the organization and an <c>@</c> right after one ends a user part.</summary>
    private static string WithoutPlaceholders(string text)
    {
        foreach (var placeholder in Placeholders)
        {
            text = text.Replace(placeholder, " ", StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>An id's forms: as learned and without hyphens, each as written and URL-escaped.</summary>
    private static IEnumerable<string> FormsOf(string id) =>
        new[] { id, id.Replace("-", string.Empty, StringComparison.Ordinal) }
            .SelectMany(form => new[] { form, Uri.EscapeDataString(form) })
            .Where(form => form.Length >= 8)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static bool SameId(string a, string b) =>
        string.Equals(a.Replace("-", string.Empty, StringComparison.Ordinal), b.Replace("-", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);

    private void Walk(JsonElement element, string? name, string? parent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, property.Name, name);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, name, parent);
                }

                break;
            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;
                LearnValue(value);
                if (string.Equals(name, "instanceId", StringComparison.OrdinalIgnoreCase))
                {
                    AddId(value, OrganizationIdPlaceholder);
                }
                else if (parent is not null && UserParents.Any(user => string.Equals(user, parent, StringComparison.OrdinalIgnoreCase))
                    && (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "subjectDescriptor", StringComparison.OrdinalIgnoreCase)))
                {
                    AddId(value, UserIdPlaceholder);
                }
                else if (string.Equals(name, "subjectDescriptor", StringComparison.OrdinalIgnoreCase))
                {
                    AddId(value, UserIdPlaceholder);
                }

                break;
        }
    }

    /// <summary>
    /// The ids and, unless <paramref name="addresses"/> is false, the addresses a text holds: tenants;
    /// <c>hid=</c> and sign-in <c>context=</c> values in the text as it is, with <c>&amp;amp;</c> as
    /// <c>&amp;</c>, and in each of its readings (<see cref="Decodings"/>), so that an escaped one
    /// (<c>reply_to=…%26hid%3D…</c>) is learned too; each run's address (<see cref="CoreOf"/>) and, when
    /// a quote stands in a run whose address is no address as a whole (<see cref="IsWholeAddress"/>),
    /// each part between its quotes too.
    /// </summary>
    private void LearnText(string text, bool addresses = true)
    {
        foreach (Match match in TenantAfterLogin.Matches(text))
        {
            AddId(Uri.UnescapeDataString(match.Groups["tenant"].Value), TenantPlaceholder);
        }

        foreach (Match match in DescriptorTenant.Matches(text))
        {
            AddId(match.Groups["tenant"].Value, TenantPlaceholder);
        }

        var readings = new[] { text, text.Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase) }.Concat(Decodings(text).Readings.Select(decoded => decoded.Text));
        foreach (var decoded in readings.Distinct(StringComparer.Ordinal))
        {
            foreach (Match match in HidOrContext.Matches(decoded))
            {
                var value = Uri.UnescapeDataString(match.Groups["value"].Value);
                if (string.Equals(match.Groups["name"].Value, "hid", StringComparison.OrdinalIgnoreCase))
                {
                    AddId(value, OrganizationIdPlaceholder);
                }
                else if (HidOfContext(value) is { } hid)
                {
                    AddId(hid, OrganizationIdPlaceholder);
                }
            }
        }

        if (!addresses)
        {
            return;
        }

        foreach (var (start, length) in AddressRuns(text))
        {
            var run = text.Substring(start, length);
            var core = CoreOf(run);
            AddCore(core);
            if (run.IndexOfAny(Quotes) >= 0 && !(core is not null && IsWholeAddress(core)))
            {
                foreach (var part in run.Split(Quotes, StringSplitOptions.RemoveEmptyEntries))
                {
                    AddAddress(part);
                }
            }
        }
    }

    /// <summary>Whether a <c>context=</c> value, URL-escaped or not, is a sign-in context.</summary>
    private static bool IsSignInContext(string value) => HidOfContext(Uri.UnescapeDataString(value)) is not null;

    /// <summary>
    /// The <c>hid</c> of a sign-in URL's <c>context=</c>, JSON <c>{"ht":2,"hid":"…",…}</c> (MEASURED r5
    /// p03, p15) in ASP.NET's UrlToken form, which the service sends (base64url without its padding,
    /// then one digit, 0 to 2, counting the <c>=</c> it had; 125 characters ending <c>0</c> in every
    /// capture), or in base64 or base64url with or without padding; null when it is not one. A text that
    /// can be read both ways (a padded base64 text ending <c>1</c>) is tried in the UrlToken form first,
    /// then as it is.
    /// </summary>
    internal static string? HidOfContext(string context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Length > 1 && context[^1] is >= '0' and <= '2' && (context.Length - 1 + (context[^1] - '0')) % 4 == 0
            && HidOfBase64(context[..^1] + new string('=', context[^1] - '0')) is { } hid)
        {
            return hid;
        }

        return HidOfBase64(context);
    }

    /// <summary>The <c>hid</c> of base64 or base64url JSON, padded or not; null when it is not one.</summary>
    private static string? HidOfBase64(string context)
    {
        var text = context.Replace('-', '+').Replace('_', '/');
        text = text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '=');
        try
        {
            using var document = JsonDocument.Parse(Convert.FromBase64String(text));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("hid", out var hid) && hid.ValueKind == JsonValueKind.String
                ? hid.GetString()
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
