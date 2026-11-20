using Avalonia.Media;

namespace VisualCommit.VisualTests.Phase8a;

public static partial class Phase8aExpectations
{
    /// <summary>
    /// Part B, the icon (docs/test-reports/phase-8a.md, checks B1 and B2; docs/phase-8a-design.md 7.3,
    /// 7.5, 12.3.4 launch 3): the icon files' lists, the sheet's layout, heading and colours, the
    /// tolerance, the picture names, and for the real window launch 3's facts for part B. Written
    /// from the report and the design, not from the tool's or the tests' lists, which
    /// <c>Phase8aExpectationTests.B</c> compares with these. The real-window pass links this file
    /// (design 0.3): it uses the BCL and Avalonia only.
    /// </summary>
    public static class B
    {
        // ---- The files (design 7.3) ----------------------------------------------------------

        /// <summary>The <c>.ico</c>'s sizes, ascending: row 1 of the sheet, left to right.</summary>
        public static IReadOnlyList<int> IcoSizes { get; } = [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256];

        /// <summary>Linux's PNGs, <c>assets/brand/generated/png/icon-&lt;N&gt;.png</c>.</summary>
        public static IReadOnlyList<int> PngSizes { get; } = [16, 24, 32, 48, 64, 128, 256, 512];

        /// <summary>The <c>.iconset</c>'s ten names, Apple's.</summary>
        public static IReadOnlyList<string> IconSetNames { get; } =
        [
            "icon_16x16", "icon_16x16@2x", "icon_32x32", "icon_32x32@2x", "icon_128x128",
            "icon_128x128@2x", "icon_256x256", "icon_256x256@2x", "icon_512x512", "icon_512x512@2x",
        ];

        /// <summary>The app's two PNGs, <c>VisualCommit-48.png</c> and <c>-96.png</c>; no row of the sheet shows them.</summary>
        public static IReadOnlyList<int> AppImageSizes { get; } = [48, 96];

        /// <summary>The <c>.ico</c> the sheet's row 1 shows, relative to the repository.</summary>
        public const string IcoFile = "src/VisualCommit.App/Assets/Icon/VisualCommit.ico";

        /// <summary>Row 2, left to right: each file, relative to the repository, and its size.</summary>
        public static IReadOnlyList<(string File, int Size)> Row2 { get; } =
        [
            ("assets/brand/generated/png/icon-16.png", 16),
            ("assets/brand/generated/png/icon-24.png", 24),
            ("assets/brand/generated/png/icon-32.png", 32),
            ("assets/brand/generated/png/icon-48.png", 48),
            ("assets/brand/generated/png/icon-64.png", 64),
            ("assets/brand/generated/png/icon-128.png", 128),
            ("assets/brand/generated/VisualCommit.iconset/icon_16x16.png", 16),
            ("assets/brand/generated/VisualCommit.iconset/icon_32x32.png", 32),
            ("assets/brand/generated/VisualCommit.iconset/icon_128x128.png", 128),
        ];

        // ---- The sheet (design 7.5; the report's open points 44 and 45) --------------------------

        /// <summary>Row 1's heading, word for word (7.5).</summary>
        public const string Heading = "VisualCommit.ico";

        /// <summary>The heading's font size, in <c>VcTextPrimaryBrush</c> (open point 44, fixed here).</summary>
        public const double HeadingFontSize = 13;

        /// <summary>The size under each of row 1's images, a bare number, in <c>VcTextSecondaryBrush</c>.</summary>
        public const double SizeFontSize = 11;

        /// <summary>
        /// The sizes' weight (open point 44, fixed here): at the regular weight "20" and "32" reach their
        /// full ink in no pixel on white, which <c>TextInk</c>'s rule needs.
        /// </summary>
        public static readonly FontWeight SizeFontWeight = FontWeight.SemiBold;

        /// <summary>Each row's first image's left.</summary>
        public const int Left = 24;

        /// <summary>The gap between two neighbouring images of a row.</summary>
        public const int Gap = 12;

        /// <summary>Row 1's lefts: left 24, gap 12.</summary>
        public static IReadOnlyList<int> Row1Lefts { get; } = [24, 52, 84, 120, 162, 206, 254, 306, 366, 438, 514, 598, 690, 798];

        /// <summary>Where row 1's last image, the 256, ends.</summary>
        public const int Row1End = 1054;

        /// <summary>Row 2's lefts, laid out as row 1 (open point 45).</summary>
        public static IReadOnlyList<int> Row2Lefts { get; } = [24, 52, 88, 132, 192, 268, 408, 436, 480];

        /// <summary>Where row 2's last image ends.</summary>
        public const int Row2End = 608;

        /// <summary>The heading's top.</summary>
        public const int HeadingTop = 24;

        /// <summary>Row 1's bottom line: every image of row 1 ends on it.</summary>
        public const int Row1Bottom = 312;

        /// <summary>The top of the sizes under row 1.</summary>
        public const int SizesTop = 316;

        /// <summary>Row 2's bottom line, below row 1's sizes.</summary>
        public const int Row2Bottom = 488;

        // ---- The colours: tokens the contrast round keeps, dark then light ---------------------

        /// <summary><c>VcWindowBackgroundBrush</c>: the sheet's background, the gaps and every image's top-left pixel.</summary>
        public static readonly Color BackgroundDark = Color.FromRgb(0x14, 0x16, 0x1B);

        public static readonly Color BackgroundLight = Color.FromRgb(0xFF, 0xFF, 0xFF);

        /// <summary><c>VcTextSecondaryBrush</c>: the sizes under row 1.</summary>
        public static readonly Color SizeDark = Color.FromRgb(0x9B, 0xA3, 0xB0);

        public static readonly Color SizeLight = Color.FromRgb(0x5C, 0x65, 0x72);

        /// <summary><c>VcTextPrimaryBrush</c>: the heading.</summary>
        public static readonly Color HeadingDark = Color.FromRgb(0xE4, 0xE7, 0xEC);

        public static readonly Color HeadingLight = Color.FromRgb(0x1B, 0x1F, 0x27);

        /// <summary>How far an image's centre may lie from its file's centre pixel composited over the background, in each channel.</summary>
        public const int Tolerance = 2;

        // ---- The pictures (the report's Screenshots column) ----------------------------------

        public const string B1IconSizesDark = "b1-icon-sizes-dark";

        public const string B2IconSizesLight = "b2-icon-sizes-light";

        public static IReadOnlyList<string> Pictures { get; } = [B1IconSizesDark, B2IconSizesLight];

        // ---- The real window: launch 3's facts for part B (12.3.4) -----------------------------

        /// <summary>The window's title, right of the mark in the framed captures.</summary>
        public const string WindowTitle = "VisualCommit";

        public const string B1TitleBarDarkWithFrame = "b1-title-bar-dark-with-frame";

        public const string B1TitleBarIconDark = "b1-title-bar-icon-dark";

        public const string B2TitleBarLightWithFrame = "b2-title-bar-light-with-frame";

        public const string B2TitleBarIconLight = "b2-title-bar-icon-light";

        public const string B2TaskbarButton = "b2-taskbar-button";

        public const string B2TaskbarButtonEnlarged = "b2-taskbar-button-enlarged";

        /// <summary>The six captures of launch 3 for part B, in the launch's order; none has a scripted twin.</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } =
            [B1TitleBarDarkWithFrame, B1TitleBarIconDark, B2TitleBarLightWithFrame, B2TitleBarIconLight, B2TaskbarButton, B2TaskbarButtonEnlarged];

        /// <summary>The title bar's crop: its left 64 pixels of the framed capture, by the caption's height (open point 49).</summary>
        public const int TitleBarCropWidth = 64;

        /// <summary>The crops' enlargement, nearest-neighbour.</summary>
        public const int Enlargement = 4;

        /// <summary>How the taskbar button's UI Automation name starts.</summary>
        public const string TaskbarButtonNameStart = "VisualCommit";

        /// <summary>How long the taskbar button is looked for, from UI Automation's desktop root.</summary>
        public static readonly TimeSpan TaskbarButtonLimit = TimeSpan.FromSeconds(10);

        /// <summary>run.txt's line when no taskbar button is found within the limit (open point 51).</summary>
        public const string TaskbarButtonNotRun = "B2's taskbar button: Not run (no taskbar button named VisualCommit was found within 10 s).";

        /// <summary>How far an extracted or window icon's opaque pixel may lie from the <c>.ico</c>'s frame of its size, in each channel (B1 (a), (b)).</summary>
        public const int IconTolerance = 2;
    }
}
