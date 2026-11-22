using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TextMateSharp.Grammars;
using VisualCommit.App.Services;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <c>THIRD-PARTY-NOTICES.md</c> held true to what the build ships (R6; D431, D433): every package
/// of the app's <c>VisualCommit.deps.json</c> that ships a file is named with its version, and every
/// file it ships on that package's entry; no entry names a package the build does not ship; every
/// grammar and theme of TextMateSharp.Grammars' assembly is listed; every license named has its
/// text; the only e-mail addresses are those of the copyright lines; and the app embeds
/// <c>LICENSE</c>, the notices and the vendors' files word for word. The
/// deps.json read is the one beside this assembly, the app's own as this machine built it: on
/// Windows only this machine's natives (D239), on macOS and Linux every platform's, so CI's macOS
/// and Linux jobs prove the natives Windows does not ship. Reads files only.
/// </summary>
public sealed partial class ThirdPartyNoticesTests
{
    /// <summary>
    /// The packages that ship only on macOS and Linux: a Windows build trims their natives (D239), so
    /// its deps.json lacks them. Each comes out with its managed package, at its version.
    /// </summary>
    private static readonly string[] OtherPlatformsOnly =
    [
        "SkiaSharp.NativeAssets.macOS", "SkiaSharp.NativeAssets.Linux", "HarfBuzzSharp.NativeAssets.macOS", "HarfBuzzSharp.NativeAssets.Linux",
    ];

    /// <summary>NuGet's placeholder for "no asset for this framework", which ships nothing.</summary>
    private const string NuGetPlaceholder = "_._";

    /// <summary>The asset kinds of a deps.json library that put a file in the build output.</summary>
    private static readonly string[] ShippedAssetKinds = ["runtime", "runtimeTargets", "native", "resources"];

    private const string GrammarPrefix = "TextMateSharp.Grammars.Resources.Grammars.";
    private const string ManifestSuffix = ".cgmanifest.json";
    private const string ThemePrefix = "TextMateSharp.Grammars.Resources.Themes.";

    [Fact]
    public void Every_shipped_package_is_named_with_its_version()
    {
        var notices = Notices.Read();
        var missing = new List<string>();
        foreach (var package in ShippedPackages())
        {
            var entries = notices.Components.Where(entry => entry.Packages.Contains(package.Name, StringComparer.Ordinal)).ToList();
            if (entries.Count != 1)
            {
                missing.Add($"{package.Name}: on {entries.Count} Packages: lines");
            }
        }

        Assert.True(missing.Count == 0, "Packages the build ships that THIRD-PARTY-NOTICES.md does not name once, with this version:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void Every_shipped_file_is_named()
    {
        var notices = Notices.Read();
        var missing = new List<string>();
        foreach (var package in ShippedPackages())
        {
            var entry = notices.Components.FirstOrDefault(entry => entry.Packages.Contains(package.Name, StringComparer.Ordinal));
            foreach (var file in package.Files)
            {
                if (entry is null || !entry.Files.Contains(file, StringComparer.Ordinal))
                {
                    missing.Add($"{file} ({package.Name}, entry {entry?.Name ?? "none"})");
                }
            }
        }

        Assert.True(missing.Count == 0, "Files the build ships that are not on their entry's Files: line:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void Every_named_package_ships()
    {
        var notices = Notices.Read();
        var inDeps = DepsPackages().ToDictionary(package => package.Id, package => package.Version, StringComparer.Ordinal);
        var stale = new List<string>();
        foreach (var named in notices.Components.SelectMany(entry => entry.Packages))
        {
            var space = named.LastIndexOf(' ');
            var (id, version) = space < 0 ? (named, string.Empty) : (named[..space], named[(space + 1)..]);
            if (inDeps.TryGetValue(id, out var shipped))
            {
                if (shipped != version)
                {
                    stale.Add($"{named}: the build ships {shipped}");
                }
            }
            else if (OtherPlatformsOnly.Contains(id, StringComparer.Ordinal))
            {
                // Not built here: it ships at its managed package's version on macOS and Linux.
                var managed = id[..id.IndexOf(".NativeAssets", StringComparison.Ordinal)];
                if (!inDeps.TryGetValue(managed, out var managedVersion) || managedVersion != version)
                {
                    stale.Add($"{named}: {managed} ships {managedVersion ?? "nothing"}");
                }
            }
            else
            {
                stale.Add($"{named}: not in VisualCommit.deps.json");
            }
        }

        Assert.True(stale.Count == 0, "Packages THIRD-PARTY-NOTICES.md names that the build does not ship as named:\n" + string.Join('\n', stale));
    }

    [Fact]
    public void Every_grammar_and_theme_is_listed()
    {
        var notices = Notices.Read();
        var grammars = notices.Components.Single(entry => entry.Packages.Any(package => package.StartsWith("TextMateSharp.Grammars ", StringComparison.Ordinal)));
        var rows = grammars.TableRows;
        var assembly = typeof(RegistryOptions).Assembly;
        var names = assembly.GetManifestResourceNames();
        var missing = new List<string>();

        var manifests = names.Where(name => name.StartsWith(GrammarPrefix, StringComparison.Ordinal) && name.EndsWith(ManifestSuffix, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(manifests);
        foreach (var manifest in manifests)
        {
            var folder = manifest[GrammarPrefix.Length..^ManifestSuffix.Length];
            foreach (var url in RegistrationUrls(assembly, manifest))
            {
                if (!rows.Any(cells => cells.Count >= 2 && cells[0] == folder && cells[1] == url))
                {
                    missing.Add($"grammar {folder}: {url}");
                }
            }
        }

        var themes = names.Where(name => name.StartsWith(ThemePrefix, StringComparison.Ordinal)).Select(name => name[ThemePrefix.Length..]).ToList();
        Assert.NotEmpty(themes);
        foreach (var theme in themes.Where(theme => !rows.Any(cells => cells.Contains(theme, StringComparer.Ordinal))))
        {
            missing.Add($"theme {theme}");
        }

        Assert.True(missing.Count == 0, "Grammars and themes of TextMateSharp.Grammars that its entry's tables do not list:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void Every_license_named_has_its_text()
    {
        var notices = Notices.Read();
        var vendorFiles = VendorFiles().Select(file => "notices/" + file).ToHashSet(StringComparer.Ordinal);
        var unknown = new List<string>();
        foreach (var entry in notices.Components)
        {
            Assert.True(entry.Licenses.Count > 0, $"The entry {entry.Name} names no license.");
            unknown.AddRange(entry.Licenses
                .Where(license => !notices.LicenseHeadings.Contains(license, StringComparer.Ordinal) && !vendorFiles.Contains(license))
                .Select(license => $"{entry.Name}: {license}"));
        }

        Assert.True(unknown.Count == 0, "Licenses named with no heading under \"License texts\" and no file in notices/:\n" + string.Join('\n', unknown));
    }

    [Fact]
    public void The_app_embeds_the_texts_word_for_word()
    {
        Assert.Equal(ReadRepositoryText("LICENSE"), EmbeddedTexts.Read(EmbeddedTexts.License));
        Assert.Equal(ReadRepositoryText("THIRD-PARTY-NOTICES.md"), EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices));

        var files = VendorFiles();
        Assert.Equal(files.Select(file => EmbeddedTexts.VendorNoticePrefix + file), EmbeddedTexts.VendorNotices());
        var main = EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices);
        foreach (var file in files)
        {
            Assert.Equal(ReadRepositoryText("notices/" + file), EmbeddedTexts.Read(EmbeddedTexts.VendorNoticePrefix + file));
            Assert.Contains("notices/" + file, main, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_identity_matches_LICENSE()
    {
        var license = ReadRepositoryText("LICENSE").Split('\n');
        Assert.Equal("MIT License", license[0]);
        var identity = AppIdentity.FromAssembly(typeof(App).Assembly);
        Assert.Equal(license[2], identity.Copyright);
        Assert.Equal("Rahim Khajei", identity.Creator);
        Assert.Contains(identity.Creator, license[2], StringComparison.Ordinal);
    }

    [Fact]
    public void The_notices_files_only_addresses_are_its_copyright_lines()
    {
        // The copyright lines are quoted as their holders wrote them, with the addresses some of
        // them carry (design 8.3.4; the lead's ruling of 2026-11-21). Every "@" lies inside a
        // fenced block, and the addresses are exactly these: a new one is a change to this list.
        var lines = ReadRepositoryText("THIRD-PARTY-NOTICES.md").Split('\n');
        var outside = new List<string>();
        var fenced = false;
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
            }
            else if (!fenced && lines[index].Contains('@', StringComparison.Ordinal))
            {
                outside.Add($"{index + 1}: {lines[index]}");
            }
        }

        Assert.True(outside.Count == 0, "THIRD-PARTY-NOTICES.md holds an \"@\" outside a fenced block:\n" + string.Join('\n', outside));
        var found = lines.SelectMany(line => EmailAddress().Matches(line)).Select(match => match.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        Assert.Equal(CopyrightAddresses.Order(StringComparer.Ordinal), found);
    }

    /// <summary>
    /// The e-mail addresses that the copyright lines of <c>THIRD-PARTY-NOTICES.md</c> carry, each as
    /// its source writes it: Tmds.DBus' <c>COPYING</c>, zlib's <c>LICENSE</c> in Chromium's copy,
    /// Oniguruma's notice in Onigwrap's package and the yaml grammar's manifest in TextMateSharp.Grammars.
    /// </summary>
    private static readonly string[] CopyrightAddresses =
    [
        "alp@atoker.com", "tom.deseyn@gmail.com", "jloup@gzip.org", "madler@alumni.caltech.edu", "kkosako0@gmail.com", "fichtefoll2@googlemail.com",
    ];

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailAddress();

    /// <summary>The path of a file of the repository, from the root the build names.</summary>
    private static string RepositoryPath(string relative) =>
        Path.GetFullPath(Path.Combine(VisualRun.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>The names of the files in the repository's <c>notices/</c> folder, in ordinal order; none fails.</summary>
    private static List<string> VendorFiles()
    {
        var folder = RepositoryPath("notices");
        Assert.True(Directory.Exists(folder), $"The repository has no notices/ folder at {folder}.");
        var files = Directory.GetFiles(folder).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(files);
        return files;
    }

    /// <summary>A file of the repository as the app embeds it: <c>\n</c> line ends, no byte-order mark.</summary>
    private static string ReadRepositoryText(string relative) =>
        File.ReadAllText(RepositoryPath(relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The app's deps.json, as this machine built it, beside this assembly.</summary>
    private static JsonDocument ReadDeps()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "VisualCommit.deps.json");
        Assert.True(File.Exists(path), $"No VisualCommit.deps.json beside the tests at {path}.");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    /// <summary>Every package library of the deps.json's target, with its version.</summary>
    private static List<(string Id, string Version)> DepsPackages()
    {
        using var deps = ReadDeps();
        var libraries = deps.RootElement.GetProperty("libraries");
        return [.. libraries.EnumerateObject()
            .Where(library => library.Value.GetProperty("type").GetString() == "package")
            .Select(library => library.Name.Split('/'))
            .Select(parts => (parts[0], parts[1]))];
    }

    /// <summary>
    /// The packages of the deps.json's target that put a file in the build output, each as
    /// "&lt;id&gt; &lt;version&gt;" with the file names of its runtime, runtimeTargets, native and
    /// resources assets.
    /// </summary>
    private static List<(string Name, IReadOnlyList<string> Files)> ShippedPackages()
    {
        using var deps = ReadDeps();
        var root = deps.RootElement;
        var libraries = root.GetProperty("libraries");
        var target = root.GetProperty("targets").EnumerateObject().First().Value;
        var shipped = new List<(string, IReadOnlyList<string>)>();
        foreach (var library in target.EnumerateObject())
        {
            if (libraries.GetProperty(library.Name).GetProperty("type").GetString() != "package")
            {
                continue;
            }

            var files = ShippedAssetKinds
                .Where(kind => library.Value.TryGetProperty(kind, out _))
                .SelectMany(kind => library.Value.GetProperty(kind).EnumerateObject().Select(asset => Path.GetFileName(asset.Name)))
                .Where(file => file != NuGetPlaceholder)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (files.Count > 0)
            {
                var parts = library.Name.Split('/');
                shipped.Add(($"{parts[0]} {parts[1]}", files));
            }
        }

        Assert.NotEmpty(shipped);
        return shipped;
    }

    /// <summary>The repositories a grammar's cgmanifest.json registers, each without a trailing slash.</summary>
    private static IEnumerable<string> RegistrationUrls(Assembly assembly, string manifest)
    {
        using var stream = assembly.GetManifestResourceStream(manifest)!;
        using var json = JsonDocument.Parse(stream);
        var urls = new List<string>();
        foreach (var registration in json.RootElement.GetProperty("registrations").EnumerateArray())
        {
            var component = registration.GetProperty("component");
            var type = component.GetProperty("type").GetString()!;
            var details = component.GetProperty(type);
            var url = details.TryGetProperty("repositoryUrl", out var repository) ? repository.GetString()
                : details.TryGetProperty("downloadUrl", out var download) ? download.GetString()
                : null;
            Assert.False(string.IsNullOrWhiteSpace(url), $"{manifest} registers a component without an address.");
            urls.Add(url!.Trim().TrimEnd('/'));
        }

        return urls;
    }

    /// <summary>An entry of the notices' "Components": its heading and its machine-read lines, and its tables' rows.</summary>
    private sealed record Entry(string Name, List<string> Packages, List<string> Files, List<string> Licenses, List<List<string>> TableRows);

    /// <summary>
    /// <c>THIRD-PARTY-NOTICES.md</c> read as its "Keeping this file true" section describes it: under
    /// "## Components" a "### " entry each, with the lines <c>Packages: &lt;id&gt; &lt;version&gt;, …</c>,
    /// <c>Files: &lt;file&gt;, …</c> and <c>License: &lt;name&gt;[, &lt;name&gt;]. &lt;text&gt;</c>;
    /// under "## License texts" a "### " heading per license. Lines inside fenced blocks are text only.
    /// </summary>
    private sealed record Notices(List<Entry> Components, List<string> LicenseHeadings)
    {
        public static Notices Read()
        {
            var components = new List<Entry>();
            var headings = new List<string>();
            string? section = null;
            Entry? entry = null;
            var fenced = false;
            foreach (var line in ReadRepositoryText("THIRD-PARTY-NOTICES.md").Split('\n'))
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;
                    continue;
                }

                if (fenced)
                {
                    continue;
                }

                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    section = line[3..].Trim();
                    entry = null;
                }
                else if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    var name = line[4..].Trim();
                    if (section == "Components")
                    {
                        entry = new Entry(name, [], [], [], []);
                        components.Add(entry);
                    }
                    else if (section == "License texts")
                    {
                        headings.Add(name);
                    }
                }
                else if (entry is not null)
                {
                    Take(entry, line);
                }
            }

            Assert.True(components.Count > 0, "THIRD-PARTY-NOTICES.md has no \"## Components\" with \"### \" entries.");
            Assert.True(headings.Count > 0, "THIRD-PARTY-NOTICES.md has no \"## License texts\" with \"### \" headings.");
            return new Notices(components, headings);
        }

        private static void Take(Entry entry, string line)
        {
            if (line.StartsWith("Packages: ", StringComparison.Ordinal))
            {
                entry.Packages.AddRange(List(line["Packages: ".Length..]));
            }
            else if (line.StartsWith("Files: ", StringComparison.Ordinal))
            {
                entry.Files.AddRange(List(line["Files: ".Length..]));
            }
            else if (line.StartsWith("License: ", StringComparison.Ordinal))
            {
                // The names end at the first full stop that ends a sentence.
                var rest = line["License: ".Length..];
                var stop = rest.IndexOf(". ", StringComparison.Ordinal);
                var names = (stop < 0 ? rest : rest[..stop]).TrimEnd('.');
                entry.Licenses.AddRange(List(names));
            }
            else if (line.StartsWith('|') && !line.Replace("|", string.Empty, StringComparison.Ordinal).Trim().All(character => character is '-' or ':' or ' '))
            {
                entry.TableRows.Add([.. line.Trim().Trim('|').Split('|').Select(cell => cell.Trim().Trim('`'))]);
            }
        }

        private static IEnumerable<string> List(string text) =>
            text.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
