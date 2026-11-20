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
// them into place, the manifest last. A file in the folders the tool owns that it did not write is
// deleted. Each file replaced or deleted is copied aside first and put back if a later step fails,
// so that a failure leaves the committed files as they were. Nothing it writes holds a time stamp: a
// run with nothing changed writes the same bytes.

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

    var drawn = Path.Combine(staging, "new");
    foreach (var (path, bytes) in files)
    {
        var staged = FullPath(drawn, path);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllBytes(staged, bytes);
    }

    // Into place: every file, then the manifest, then whatever else the owned folders hold goes.
    // Each file is copied aside before it is replaced or deleted, so that a failure on the way puts
    // the files back as they were before it is reported; what was done is printed only once all is.
    var placement = new Placement(root, drawn, Path.Combine(staging, "old"));
    var done = new List<string>();
    try
    {
        foreach (var output in IconOutputs.Files())
        {
            placement.Replace(output.Path);
            var sizes = string.Join(", ", output.Sizes.Select(size => size.ToString(CultureInfo.InvariantCulture)));
            done.Add($"Wrote {output.Path} ({output.Sizes.Count} {(output.Sizes.Count == 1 ? "size" : "sizes")}: {sizes}).");
        }

        placement.Replace(IconOutputs.ManifestFile);
        done.Add($"Wrote {IconOutputs.ManifestFile}.");
        RemoveOthers(root, [.. files.Keys], placement, done);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"{placement.Failing}: {exception.Message}");
        placement.PutBack().ToList().ForEach(Console.Error.WriteLine);
        return 1;
    }

    done.ForEach(Console.WriteLine);

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

static string FullPath(string root, string relative) => Placement.FullPath(root, relative);

// Deletes every file in the owned folders that this run did not write, then the folders left empty.
static void RemoveOthers(string root, IReadOnlyCollection<string> written, Placement placement, List<string> done)
{
    var keep = written.Select(path => Path.GetFullPath(FullPath(root, path))).ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    foreach (var owned in IconOutputs.OwnedFolders)
    {
        var folder = FullPath(root, owned);
        placement.Listing(owned);
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(file => !keep.Contains(Path.GetFullPath(file))).ToList())
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            placement.Delete(relative);
            done.Add($"Deleted {relative}, which this tool does not write.");
        }

        // The deepest first (a folder's path is longer than its parent's), each looked at just before
        // it would go, so that a folder left empty by its last subfolder's going goes too.
        placement.Listing(owned);
        foreach (var empty in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length).Where(path => !Directory.EnumerateFileSystemEntries(path).Any()))
        {
            placement.DeleteFolder(Path.GetRelativePath(root, empty).Replace(Path.DirectorySeparatorChar, '/'));
        }
    }
}

/// <summary>
/// Puts the drawn files into the repository and deletes the others, after copying aside each file
/// it replaces or deletes, so that a failure on the way can put every file back as it was: a failed
/// run leaves the committed files as they were. A move may be a copy and a delete, since the
/// temporary folder is often on another volume than the repository, so any step can fail halfway.
/// </summary>
/// <param name="root">The repository's folder.</param>
/// <param name="drawn">The folder that holds the drawn files at their paths in the repository.</param>
/// <param name="aside">An empty folder for the copies of the files replaced or deleted.</param>
internal sealed class Placement(string root, string drawn, string aside)
{
    /// <summary>Every file replaced or deleted so far, relative, and whether it was there before.</summary>
    private readonly List<(string Path, bool Existed)> _changed = [];

    /// <summary>What was being done when a step failed, for its message: "{path}: could not be written".</summary>
    public string Failing { get; private set; } = string.Empty;

    /// <summary><paramref name="relative"/> with the platform's separator, under <paramref name="folder"/>.</summary>
    public static string FullPath(string folder, string relative) => Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Replaces the repository's <paramref name="relative"/> with the drawn file of that path.</summary>
    public void Replace(string relative)
    {
        Failing = $"{relative}: could not be written";
        var target = FullPath(root, relative);
        KeepAside(relative, target);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(FullPath(drawn, relative), target, overwrite: true);
    }

    /// <summary>Deletes the repository's <paramref name="relative"/>.</summary>
    public void Delete(string relative)
    {
        Failing = $"{relative}: could not be deleted";
        var target = FullPath(root, relative);
        KeepAside(relative, target);
        File.Delete(target);
    }

    /// <summary>Notes that the owned folder <paramref name="relative"/> is being looked through, for a failure's message.</summary>
    public void Listing(string relative) => Failing = $"{relative}: could not be read";

    /// <summary>Deletes the repository's empty folder <paramref name="relative"/>; git keeps no folder, so none is put back.</summary>
    public void DeleteFolder(string relative)
    {
        Failing = $"{relative}: could not be deleted";
        Directory.Delete(FullPath(root, relative));
    }

    /// <summary>
    /// Puts back every file changed so far, the last first: a replaced or deleted file from its
    /// copy, unless it still holds the same bytes (a file that refused to change is left alone); a
    /// file that was not there before deleted. Gives what could not be put back.
    /// </summary>
    public IEnumerable<string> PutBack()
    {
        foreach (var (relative, existed) in Enumerable.Reverse(_changed))
        {
            var target = FullPath(root, relative);
            var copy = FullPath(aside, relative);
            string? failure = null;
            try
            {
                if (existed && !(File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(File.ReadAllBytes(copy))))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(copy, target, overwrite: true);
                }
                else if (!existed && File.Exists(target))
                {
                    File.Delete(target);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failure = $"{relative}: could not be put back as it was: {exception.Message}";
            }

            if (failure is not null)
            {
                yield return failure;
            }
        }
    }

    // A file is listed as changed only once its copy is made, so that one whose copy failed is never
    // "put back" from a copy that does not exist. The copy loses a read-only flag the file had, which
    // would keep the temporary folder from being deleted.
    private void KeepAside(string relative, string target)
    {
        var existed = File.Exists(target);
        if (existed)
        {
            var copy = FullPath(aside, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(target, copy);
            File.SetAttributes(copy, File.GetAttributes(copy) & ~FileAttributes.ReadOnly);
        }

        _changed.Add((relative, existed));
    }
}
