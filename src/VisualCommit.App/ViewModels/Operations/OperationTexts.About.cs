namespace VisualCommit.App.ViewModels.Operations;

public static partial class OperationTexts
{
    /// <summary>
    /// The texts of the settings screen's About section, of the text dialog that shows the license
    /// and the third-party notices, of the palette's command and of the macOS application menu's item
    /// (D409, D431, D432). American spelling on screen, as every label of the app; no text holds an
    /// e-mail address (D409). The log lines are here too, for the tests.
    /// </summary>
    public static class AboutTexts
    {
        // The section.
        public const string Section = "About";

        /// <summary>The line under the app's name: who made it (D398).</summary>
        public static string CreatedBy(string creator) => $"Created by {creator}";

        public const string VersionLabel = "Version";
        public const string CopyrightLabel = "Copyright";
        public const string LicenseLabel = "License";
        public const string NoticesLabel = "Notices";
        public const string SourceCodeLabel = "Source code";
        public const string UserGuideLabel = "User guide";

        /// <summary>The app's license, as the section names it.</summary>
        public const string MitLicense = "MIT License";

        /// <summary>It opens a dialog, so it ends in "…".</summary>
        public const string ShowLicense = "Show license…";

        public const string NoticesNote = "VisualCommit is built on open-source components, each under its own license.";

        public const string ShowNotices = "Show third-party notices…";

        /// <summary>The repository link's tooltip.</summary>
        public const string RepositoryTip = "Opens the repository's page in your browser.";

        /// <summary>The guide link's text, beside the label <see cref="UserGuideLabel"/>.</summary>
        public const string OpenUserGuide = "Open the user guide";

        /// <summary>The guide link's tooltip.</summary>
        public const string GuideTip = "Opens the user guide in your browser.";

        /// <summary>A link's text: the address's host and path, without its scheme (<c>github.com/RahimPasha/VisualCommit</c>).</summary>
        public static string LinkText(Uri address)
        {
            ArgumentNullException.ThrowIfNull(address);
            return address.Host + address.AbsolutePath.TrimEnd('/');
        }

        // The text dialog.
        public const string LicenseTitle = "MIT License";
        public const string NoticesTitle = "Third-party notices";

        /// <summary>The switch's first button: <c>THIRD-PARTY-NOTICES.md</c>.</summary>
        public const string MainNotices = "Components and licenses";

        /// <summary>
        /// The switch's button of a vendor's file in <c>notices/</c>, named by the component that ships it;
        /// a file it does not know keeps its own name.
        /// </summary>
        public static string NoticeFileLabel(string file)
        {
            ArgumentNullException.ThrowIfNull(file);
            return NoticeFileKey(file) switch
            {
                "Avalonia" => "Avalonia",
                "CommunityToolkit.Mvvm" => ".NET Community Toolkit",
                "Onigwrap" => "Onigwrap",
                "SkiaSharp" => "SkiaSharp and HarfBuzzSharp",
                _ => file,
            };
        }

        /// <summary>The part of a vendor file's name before its first '-': the component it belongs to.</summary>
        public static string NoticeFileKey(string file)
        {
            ArgumentNullException.ThrowIfNull(file);
            var dash = file.IndexOf('-', StringComparison.Ordinal);
            return dash < 0 ? file : file[..dash];
        }

        public const string CopyAll = "Copy all";
        public const string Close = "Close";

        // Where About opens from.
        public const string PaletteCommand = "About VisualCommit…";

        /// <summary>The macOS application menu's own item, in place of Avalonia's "About Avalonia".</summary>
        public const string MenuItem = "About VisualCommit";

        // The log lines.
        /// <summary>The Error when an embedded text could not be read; <paramref name="name"/> is its resource's name.</summary>
        public static string TextNotRead(string name) => $"The text {name} could not be read.";

        public const string RepositoryFailed = "Opening the repository's page failed.";
        public const string GuideFailed = "Opening the user guide failed.";
        public const string CopyFailed = "Copying the text to the clipboard failed.";
    }
}
