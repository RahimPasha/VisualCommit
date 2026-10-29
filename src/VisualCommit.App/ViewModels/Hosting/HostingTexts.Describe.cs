using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Hosting;

public static partial class HostingTexts
{
    /// <summary>
    /// The one wording of a GitHub failure that the clone browser, the "Pull requests" section and
    /// the create form show (D338, docs/phase-7a-design.md 1.7), shown as it is; <paramref name="login"/>
    /// is the signed-in account's, or null.
    /// </summary>
    /// <remarks>Still the failure's own message: WP-B builds the wording (docs/phase-7a-design.md 9.1).</remarks>
    public static string Describe(HostingFailure failure, string? login)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Message;
    }
}
