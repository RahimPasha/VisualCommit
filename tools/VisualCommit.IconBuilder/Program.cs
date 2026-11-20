using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SkiaSharp;
using Svg.Skia;
using VisualCommit.IconBuilder;

// The icon's builder (D407, D428, D429, D447). Checks the three masters in assets/brand/, draws
// every size of every file IconOutputs lists, writes them all into a temporary folder and then moves
// them into place, the manifest last, so that a failure leaves the committed files as they were. A
// file in the folders the tool owns that it did not write is deleted. Nothing it writes holds a time
// stamp: a run with nothing changed writes the same bytes.

const string Usage = "Usage: dotnet run --project tools/VisualCommit.IconBuilder [-- --root <the repository's folder>]";

string? root;
if (args.Length == 0)
{
    var here = Directory.GetCurrentDirectory();
    root = RootAbove(here);
    if (root is null)
    {
        Console.Error.WriteLine($"No VisualCommit.slnx above {here}: give the repository's folder with --root.");
        return 2;
    }
}
else if (args.Length == 2 && args[0] == "--root")
{
    root = Path.GetFullPath(args[1]);
}
else
{
    Console.Error.WriteLine(Usage);
    return 2;
}

// Every master is checked before anything is drawn.
var problems = IconOutputs.Masters.SelectMany(master => MasterRules.Check(InRoot(master.Name, root), Shown(master), master)).ToList();
if (problems.Count > 0)
{
    problems.ForEach(Console.Error.WriteLine);
    return 1;
}

var staging = Directory.CreateTempSubdirectory("VisualCommit.IconBuilder-").FullName;
try
{
    Dictionary<string, byte[]> files;
    IReadOnlyList<RenderedImage> icoImages;
    try
    {
        (files, icoImages) = Draw(root);
    }
    catch (InvalidDataException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }

    foreach (var (path, bytes) in files)
    {
        var staged = Path.Combine(staging, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllBytes(staged, bytes);
    }

    // Into place: every file, then the manifest, then whatever else the owned folders hold goes.
    var outputs = IconOutputs.Files();
    foreach (var output in outputs)
    {
        MoveIntoPlace(staging, root, output.Path);
        var sizes = string.Join(", ", output.Sizes.Select(size => size.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine($"Wrote {output.Path} ({output.Sizes.Count} {(output.Sizes.Count == 1 ? "size" : "sizes")}: {sizes}).");
    }

    MoveIntoPlace(staging, root, IconOutputs.ManifestFile);
    Console.WriteLine($"Wrote {IconOutputs.ManifestFile}.");
    RemoveOthers(root, [.. files.Keys]);

    var dark = new SKColor(0x20, 0x20, 0x20);
    var light = new SKColor(0xF3, 0xF3, 0xF3);
    foreach (var image in icoImages)
    {
        var onDark = DeskContrast.ShareAtThreeToOne(image.Bgra, dark);
        var onLight = DeskContrast.ShareAtThreeToOne(image.Bgra, light);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{image.Size} px: {onDark:0} % of the opaque pixels at 3:1 on #202020, {onLight:0} % on #F3F3F3."));
    }

    Console.WriteLine("Done. Commit assets/brand and src/VisualCommit.App/Assets/Icon.");
    return 0;
}
finally
{
    Directory.Delete(staging, recursive: true);
}

// Draws every file in memory: the .ico, the app's PNGs, Linux's PNGs, the .iconset, the .icns from
// the .iconset's bytes, and the manifest. Also gives the .ico's images, for the contrast printout.
static (Dictionary<string, byte[]> Files, IReadOnlyList<RenderedImage> IcoImages) Draw(string root)
{
    var renderers = new Dictionary<Master, IconRenderer>();
    try
    {
        foreach (var master in IconOutputs.Masters)
        {
            renderers[master] = IconRenderer.Load(InRoot(master.Name, root), Shown(master), master);
        }

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var icoImages = IconOutputs.IcoSizes.Select(size => renderers[IconOutputs.MasterFor(size)].Render(size)).ToList();
        files[IconOutputs.IcoFile] = IcoWriter.Write(
            [.. icoImages.Select(image => new IcoImage(image.Size, image.Size == IconOutputs.IcoPngSize ? image.Png : IcoWriter.Bitmap(image.Size, image.Bgra)))]);

        foreach (var size in IconOutputs.AppImageSizes)
        {
            files[IconOutputs.AppImageFile(size)] = renderers[IconOutputs.Large].Render(size).Png;
        }

        foreach (var size in IconOutputs.PngSizes)
        {
            files[IconOutputs.PngFile(size)] = renderers[IconOutputs.MasterFor(size)].Render(size).Png;
        }

        foreach (var (name, size) in IconOutputs.IconSet)
        {
            files[IconOutputs.IconSetFile(name)] = renderers[IconOutputs.MacOS].Render(size).Png;
        }

        files[IconOutputs.IcnsFile] = IcnsWriter.Write([.. IconOutputs.IcnsEntries.Select(entry => (entry.Type, files[IconOutputs.IconSetFile(entry.IconSetName)]))]);
        files[IconOutputs.ManifestFile] = Manifest(root);
        return (files, icoImages);
    }
    finally
    {
        foreach (var renderer in renderers.Values)
        {
            renderer.Dispose();
        }
    }
}

// The manifest: the masters' hashes and every file's sizes, with no time stamp.
static byte[] Manifest(string root)
{
    var text = new StringBuilder();
    text.Append("{\n");
    text.Append("  \"v\": 1,\n");
    text.Append($"  \"renderer\": {Json(Renderer())},\n");
    text.Append("  \"masters\": {\n");
    text.AppendJoin(",\n", IconOutputs.Masters.Select(master => $"    {Json(master.Name)}: {Json(MasterRules.Hash(InRoot(master.Name, root)))}"));
    text.Append("\n  },\n");
    text.Append("  \"files\": {\n");
    text.AppendJoin(",\n", IconOutputs.Files().Select(file =>
        $"    {Json(file.Path)}: [{string.Join(", ", file.Sizes.Select(size => size.ToString(CultureInfo.InvariantCulture)))}]"));
    text.Append("\n  }\n");
    text.Append("}\n");
    return Encoding.UTF8.GetBytes(text.ToString());
}

// Relaxed escaping: the paths' "@" and "+" stay as they are, which a JSON reader takes alike.
static string Json(string value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

// "Svg.Skia 4.9.1, SkiaSharp 3.119.4": the packages that drew the files, read from their assemblies.
static string Renderer() => $"Svg.Skia {VersionOf(typeof(SKSvg).Assembly)}, SkiaSharp {VersionOf(typeof(SKSurface).Assembly)}";

static string VersionOf(Assembly assembly)
{
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString()
        ?? "unknown";
    var plus = version.IndexOf('+', StringComparison.Ordinal);
    return plus < 0 ? version : version[..plus];
}

static string? RootAbove(string folder)
{
    for (var at = new DirectoryInfo(folder); at is not null; at = at.Parent)
    {
        if (File.Exists(Path.Combine(at.FullName, "VisualCommit.slnx")))
        {
            return at.FullName;
        }
    }

    return null;
}

static string InRoot(string masterName, string root) => FullPath(root, IconOutputs.MastersFolder + "/" + masterName);

static string Shown(Master master) => IconOutputs.MastersFolder + "/" + master.Name;

static string FullPath(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

static void MoveIntoPlace(string staging, string root, string relative)
{
    var target = FullPath(root, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Move(FullPath(staging, relative), target, overwrite: true);
}

// Deletes every file in the owned folders that this run did not write, then the folders left empty.
static void RemoveOthers(string root, IReadOnlyCollection<string> written)
{
    var keep = written.Select(path => Path.GetFullPath(FullPath(root, path))).ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    foreach (var folder in IconOutputs.OwnedFolders.Select(folder => FullPath(root, folder)))
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(file => !keep.Contains(Path.GetFullPath(file))).ToList())
        {
            File.Delete(file);
            Console.WriteLine($"Deleted {Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/')}, which this tool does not write.");
        }

        foreach (var empty in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length).Where(path => !Directory.EnumerateFileSystemEntries(path).Any()).ToList())
        {
            Directory.Delete(empty);
        }
    }
}
