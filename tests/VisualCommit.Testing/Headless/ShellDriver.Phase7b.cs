using Avalonia.Threading;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing.Hosting;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 7b's seams of the harness (docs/phase-7b-design.md 1.9, 2.4): the fakes of Azure DevOps and
/// GitLab the session was started with, the refusals of a fake given with hosting options or in
/// process, and a sign-in to any provider through the session without the dialog.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The fake of Azure DevOps the session was started with; null when it was given none.</summary>
    public FakeAzureDevOps? AzureDevOps { get; }

    /// <summary>The fake of GitLab the session was started with; null when it was given none.</summary>
    public FakeGitLab? GitLab { get; }

    /// <summary>
    /// Signs in to <paramref name="provider"/> with <paramref name="token"/> (and Azure DevOps'
    /// <paramref name="organization"/>) through the session (<see cref="IHostingSession.SignInAsync(SignInRequest, CancellationToken)"/>),
    /// with no dialog, then lets the layout settle, as 7a's <see cref="SignInAsync(string, CancellationToken)"/>
    /// does for GitHub: waited on for at most <see cref="DefaultWaitTimeout"/>, and anything but a
    /// signed-in account throws with the failure's kind and message. The messages name the provider
    /// (<see cref="HostingProviders.DisplayName"/>), never the token or the organization.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not signed in afterwards.</exception>
    /// <exception cref="TimeoutException">The sign-in had not ended after <see cref="DefaultWaitTimeout"/>.</exception>
    public async Task SignInAsync(HostingProvider provider, string token, string? organization = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        Dispatcher.UIThread.VerifyAccess();
        var name = HostingProviders.DisplayName(provider);
        var signIn = Session.Services.Hosting.SignInAsync(new SignInRequest(provider, token, organization), cancellationToken);
        SignInResult result;
        try
        {
            result = await signIn.WaitAsync(DefaultWaitTimeout, cancellationToken);
        }
        catch (TimeoutException) when (!signIn.IsCompleted)
        {
            throw new TimeoutException($"Timed out after {DefaultWaitTimeout.TotalSeconds:F0} s waiting for the sign-in to {name}.");
        }

        if (result.Status is not { State: AccountState.SignedIn })
        {
            var message = result.Failure is { } failure
                ? $"Signing in to {name} failed: {failure.Kind}: {failure.Message}"
                : $"Signing in to {name} did not sign in: {result.Status?.State.ToString() ?? "no account"}.";
            throw new InvalidOperationException(Screened(message, token, organization));
        }

        Settle();
    }

    /// <summary><paramref name="message"/> with the token and the organization replaced, so that a failure's text never shows them.</summary>
    private static string Screened(string message, string token, string? organization)
    {
        var screened = message.Replace(token, "***", StringComparison.Ordinal);
        return string.IsNullOrEmpty(organization) ? screened : screened.Replace(organization, "<organization>", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Refuses a fake of 7b's given with hosting options: the options would leave it unused.</summary>
    /// <exception cref="ArgumentException">A fake and <paramref name="hosting"/> are both given.</exception>
    private static void RefuseWithHosting(FakeAzureDevOps? azureDevOps, FakeGitLab? gitLab, HostingOptions? hosting)
    {
        if (hosting is not null && (azureDevOps is not null || gitLab is not null))
        {
            throw new ArgumentException("Give the fakes (azureDevOps, gitLab) or the hosting options (hosting), not both.", nameof(hosting));
        }
    }

    /// <summary>
    /// Refuses a fake of 7b's without a server (<see cref="FakeAzureDevOps.InProcess"/>,
    /// <see cref="FakeGitLab.InProcess"/>): its endpoints are <c>Local(2)</c> or <c>Local(3)</c>, which
    /// the default handler would try to reach. Such a fake's handler goes in the hosting options.
    /// </summary>
    /// <exception cref="ArgumentException">The fake is in process.</exception>
    private static void RefuseInProcess(FakeHostingService? fake, int inProcessPort, string parameterName)
    {
        if (fake is not null && fake.Port == inProcessPort)
        {
            throw new ArgumentException(
                $"An in-process fake of {fake.DisplayName} has no server to reach: give its handler in hosting (fake.CreateHandler), not as {parameterName}.",
                parameterName);
        }
    }
}
