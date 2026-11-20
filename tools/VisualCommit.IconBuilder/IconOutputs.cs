namespace VisualCommit.IconBuilder;

/// <summary>A master: its file name in <see cref="IconOutputs.MastersFolder"/> and the one viewBox it may have.</summary>
/// <param name="Name">The file's name.</param>
/// <param name="ViewBox">Its viewBox, exactly as the file must give it.</param>
/// <param name="Size">The width and height of that viewBox.</param>
internal sealed record Master(string Name, string ViewBox, int Size);

/// <summary>One file the tool writes, relative to the repository, and the sizes it holds in order.</summary>
/// <param name="Path">The file, with <c>/</c> between its parts.</param>
/// <param name="Sizes">The sizes it holds; one for a PNG.</param>
internal sealed record OutputFile(string Path, IReadOnlyList<int> Sizes);

/// <summary>
/// What the icon's builder reads and writes (D429, D447), in one place: the three masters, the
/// <c>.ico</c>'s sizes, Linux's PNGs, macOS's <c>.iconset</c> and the <c>.icns</c>'s entries, the
/// app's two PNGs, and the folders the tool owns. The tests keep their own copy of these lists
/// (<c>tests/VisualCommit.Testing/IconFile.cs</c>), so that a change here without the tests fails.
/// </summary>
internal static class IconOutputs
{
    /// <summary>The masters' folder, relative to the repository.</summary>
    public const string MastersFolder = "assets/brand";

    /// <summary>The generated files that are no resource of the app: Linux's PNGs, macOS's files and the manifest.</summary>
    public const string GeneratedFolder = MastersFolder + "/generated";

    /// <summary>The app's icon files, which its build embeds.</summary>
    public const string AppIconFolder = "src/VisualCommit.App/Assets/Icon";

    public const string IcoFile = AppIconFolder + "/VisualCommit.ico";

    public const string IconSetFolder = GeneratedFolder + "/VisualCommit.iconset";

    public const string IcnsFile = GeneratedFolder + "/VisualCommit.icns";

    public const string ManifestFile = GeneratedFolder + "/manifest.json";

    /// <summary>The full drawing: every size above <see cref="SmallUpTo"/>, the app's PNGs, and Linux's scalable icon.</summary>
    public static readonly Master Large = new("icon.svg", "0 0 1024 1024", 1024);

    /// <summary>The drawing on a 16 grid, for <see cref="SmallUpTo"/> pixels and below.</summary>
    public static readonly Master Small = new("icon-small.svg", "0 0 16 16", 16);

    /// <summary>The mark on Apple's opaque rounded square, for the <c>.iconset</c>.</summary>
    public static readonly Master MacOS = new("icon-macos.svg", "0 0 1024 1024", 1024);

    public static IReadOnlyList<Master> Masters { get; } = [Large, Small, MacOS];

    /// <summary>The largest size drawn from <see cref="Small"/>.</summary>
    public const int SmallUpTo = 32;

    /// <summary>The <c>.ico</c>'s sizes, ascending: Windows' sizes for the title bar, the taskbar and Start. The 256 is a PNG, the others bitmaps.</summary>
    public static IReadOnlyList<int> IcoSizes { get; } = [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256];

    /// <summary>The <c>.ico</c>'s one PNG entry: Windows compresses only its largest.</summary>
    public const int IcoPngSize = 256;

    /// <summary>Linux's PNGs, drawn as the <c>.ico</c>'s are.</summary>
    public static IReadOnlyList<int> PngSizes { get; } = [16, 24, 32, 48, 64, 128, 256, 512];

    /// <summary>The app's two PNGs, from <see cref="Large"/>.</summary>
    public static IReadOnlyList<int> AppImageSizes { get; } = [48, 96];

    /// <summary>The <c>.iconset</c>'s ten files, Apple's names, and their sizes, from <see cref="MacOS"/>.</summary>
    public static IReadOnlyList<(string Name, int Size)> IconSet { get; } =
    [
        ("icon_16x16", 16),
        ("icon_16x16@2x", 32),
        ("icon_32x32", 32),
        ("icon_32x32@2x", 64),
        ("icon_128x128", 128),
        ("icon_128x128@2x", 256),
        ("icon_256x256", 256),
        ("icon_256x256@2x", 512),
        ("icon_512x512", 512),
        ("icon_512x512@2x", 1024),
    ];

    /// <summary>
    /// The <c>.icns</c>'s entries in the file's order (D447): Apple's type and the <c>.iconset</c>
    /// file whose bytes it holds. <c>icp4</c> and <c>icp5</c> hold PNG data at 16 and 32 pixels,
    /// which macOS's own <c>iconutil</c> is asked to read in CI's macOS job.
    /// </summary>
    public static IReadOnlyList<(string Type, string IconSetName)> IcnsEntries { get; } =
    [
        ("icp4", "icon_16x16"),
        ("icp5", "icon_32x32"),
        ("ic11", "icon_16x16@2x"),
        ("ic12", "icon_32x32@2x"),
        ("ic07", "icon_128x128"),
        ("ic13", "icon_128x128@2x"),
        ("ic08", "icon_256x256"),
        ("ic14", "icon_256x256@2x"),
        ("ic09", "icon_512x512"),
        ("ic10", "icon_512x512@2x"),
    ];

    /// <summary>The folders whose every file the tool writes: a file in them that it does not write is deleted.</summary>
    public static IReadOnlyList<string> OwnedFolders { get; } = [GeneratedFolder, AppIconFolder];

    /// <summary>The master a size of the <c>.ico</c> or of Linux's PNGs is drawn from.</summary>
    public static Master MasterFor(int size) => size <= SmallUpTo ? Small : Large;

    public static string PngFile(int size) => $"{GeneratedFolder}/png/icon-{size}.png";

    public static string AppImageFile(int size) => $"{AppIconFolder}/VisualCommit-{size}.png";

    public static string IconSetFile(string name) => $"{IconSetFolder}/{name}.png";

    /// <summary>Every file the tool writes but the manifest, with its sizes, in the manifest's order.</summary>
    public static IReadOnlyList<OutputFile> Files()
    {
        var files = new List<OutputFile> { new(IcoFile, IcoSizes) };
        files.AddRange(AppImageSizes.Select(size => new OutputFile(AppImageFile(size), [size])));
        files.AddRange(PngSizes.Select(size => new OutputFile(PngFile(size), [size])));
        files.AddRange(IconSet.Select(file => new OutputFile(IconSetFile(file.Name), [file.Size])));
        files.Add(new OutputFile(IcnsFile, [.. IcnsEntries.Select(entry => IconSet.Single(file => file.Name == entry.IconSetName).Size)]));
        return files;
    }
}
