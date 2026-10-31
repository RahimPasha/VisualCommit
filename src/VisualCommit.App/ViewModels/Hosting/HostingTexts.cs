namespace VisualCommit.App.ViewModels.Hosting;

/// <summary>
/// The hosting's texts that more than one package shows (docs/phase-7a-design.md 1.7). Each
/// area's other texts are in a file of its own (<c>OperationTexts.Hosting.&lt;Area&gt;.cs</c>).
/// </summary>
public static partial class HostingTexts
{
    public const string GitHub = "GitHub";

    /// <summary>The settings screen's section (D256).</summary>
    public const string AccountsSection = "Accounts";

    /// <summary>Every platform's store, in one wording (ruling 17).</summary>
    public const string Keychain = "this computer's keychain";

    public const string SignInInSettings = "Sign in to GitHub in Settings, Accounts.";

    /// <summary>The sign-in dialog's title.</summary>
    public const string AddAccountTitle = "Add GitHub account";

    public const string SignInToGitHubItem = "Sign in to GitHub…";
}
