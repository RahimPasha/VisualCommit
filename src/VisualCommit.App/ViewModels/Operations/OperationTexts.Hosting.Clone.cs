using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

public static partial class OperationTexts
{
    /// <summary>
    /// The texts of the clone form's "From GitHub…", the repository browser "Clone from GitHub" and
    /// the palette's "Clone from GitHub…" (docs/phase-7a-design.md 9.2; D261, D299, D301). A failure
    /// to list is worded by <c>HostingTexts.Describe</c> (D338), not here. Since phase 7b each text
    /// has a form per provider (docs/phase-7b-design.md 11.2; D354), which returns 7a's constant for
    /// GitHub; single sign-on's line stays GitHub's alone.
    /// </summary>
    public static class CloneTexts
    {
        /// <summary>The clone form's button at the URL row's end, shown only while signed in (D261, D302).</summary>
        public const string FromGitHubButton = "From GitHub…";

        /// <summary>The browser's title, which also names its list.</summary>
        public const string DialogTitle = "Clone from GitHub";

        /// <summary>The palette's command, without a key (D299); none of the words earlier checks typed in the palette.</summary>
        public const string PaletteCommand = "Clone from GitHub…";

        public const string Loading = "Loading repositories…";
        public const string Empty = "No repositories to list";

        /// <summary>The provider stopped at its cap of ten pages (6.3; <c>RepositoryList.MoreExist</c>).</summary>
        public const string MoreExist = "Only the 1,000 most recently updated repositories are listed.";

        /// <summary>GitHub's <c>X-GitHub-SSO: partial-results</c> (<c>RepositoryList.SomeHiddenBySso</c>).</summary>
        public const string SomeHiddenBySso = "Some organisations' repositories are hidden until the token is authorised for single sign-on.";

        /// <summary>An exception the list's call did not turn into a failure.</summary>
        public const string Unexpected = "The repositories could not be loaded.";

        public const string Choose = "Choose";

        /// <summary>The filter's placeholder and automation name, as the pick dialog's.</summary>
        public const string Filter = "Filter";

        public const string Cancel = "Cancel";

        // A row's detail (9.4): these flags in this order, each when true, then the description.
        public const string Private = "Private";
        public const string Fork = "Fork";
        public const string Archived = "Archived";

        /// <summary>What joins a row's flags and its description.</summary>
        public const string DetailSeparator = " · ";

        /// <summary>The clone form's button while two or three providers' accounts are signed in (D354).</summary>
        public const string FromAnAccount = "From an account…";

        /// <summary>The button while <paramref name="provider"/>'s account alone is signed in: <c>From GitHub…</c>, <c>From GitLab…</c>, <c>From Azure DevOps…</c>.</summary>
        public static string FromButtonOf(HostingProvider provider) =>
            provider == HostingProvider.GitHub ? FromGitHubButton : $"From {HostingTexts.Name(provider)}…";

        /// <summary>The browser's title on <paramref name="provider"/>: <c>Clone from GitHub</c>, <c>Clone from GitLab</c>, <c>Clone from Azure DevOps</c>.</summary>
        public static string DialogTitleOf(HostingProvider provider) =>
            provider == HostingProvider.GitHub ? DialogTitle : $"Clone from {HostingTexts.Name(provider)}";

        /// <summary>The palette's command of <paramref name="provider"/>, without a key (D299, D354): <c>Clone from GitHub…</c> and the others likewise.</summary>
        public static string PaletteCommandOf(HostingProvider provider) =>
            provider == HostingProvider.GitHub ? PaletteCommand : $"Clone from {HostingTexts.Name(provider)}…";

        /// <summary>
        /// The cap's line of <paramref name="provider"/>'s list: GitHub's 7a's; GitLab's ten pages of 100 by
        /// <c>last_activity_at</c> (7.4); null for Azure DevOps, whose list is not paged (6.5).
        /// </summary>
        public static string? MoreExistOf(HostingProvider provider) => provider switch
        {
            HostingProvider.GitHub => MoreExist,
            HostingProvider.GitLab => "Only the 1,000 most recently active repositories are listed.",
            _ => null,
        };

        /// <summary>The warning a failed list writes once (GitHub's 7a's byte for byte): <c>Listing GitLab repositories failed: </c>, then the failure.</summary>
        public static string ListingFailedOf(HostingProvider provider) => $"Listing {HostingTexts.Name(provider)} repositories failed";
    }
}
