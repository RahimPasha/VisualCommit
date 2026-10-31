using System.Diagnostics;
using System.Net;
using Avalonia.Threading;
using VisualCommit.Hosting;
using VisualCommit.Testing.Hosting;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 7a's seams of the harness (docs/phase-7a-design.md 2.5, 1.9): the keychain, the fake of
/// GitHub and the hosting options the session was started with, the network tripwire, and a
/// sign-in through the session without the dialog.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The keychain the session keeps its tokens in (<see cref="Start"/>'s <c>tokens</c>, or a new one).</summary>
    public FakeTokenStore Tokens { get; }

    /// <summary>The fake of GitHub the session was started with; null when it was given none.</summary>
    public FakeGitHub? GitHub { get; }

    /// <summary>The hosting options passed to the session: the fake's through the tripwire, those given, or off through the tripwire.</summary>
    public HostingOptions Hosting { get; }

    /// <summary>The requests the harness's handlers refused because they were not for a loopback address: empty in every check.</summary>
    public RefusedRequests RefusedRequests { get; }

    /// <summary>
    /// Signs in with <paramref name="token"/> through the session (<see cref="IHostingSession.SignInAsync"/>),
    /// with no dialog, then lets the layout settle. Anything but a signed-in account throws with the
    /// failure's kind and message, never the token. Until the hosting session exists (WP-S's
    /// merge) the session is <see cref="NullHostingSession"/>, so it throws. The sign-in is waited on
    /// for at most <see cref="DefaultWaitTimeout"/>, so a held route or a stuck save fails with a
    /// message rather than at the hang watchdog; <paramref name="cancellationToken"/> (the test's)
    /// goes to the session and ends the wait.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not signed in afterwards.</exception>
    /// <exception cref="TimeoutException">The sign-in had not ended after <see cref="DefaultWaitTimeout"/>.</exception>
    public async Task SignInAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        Dispatcher.UIThread.VerifyAccess();
        var signIn = Session.Services.Hosting.SignInAsync(token, cancellationToken);
        SignInResult result;
        try
        {
            result = await signIn.WaitAsync(DefaultWaitTimeout, cancellationToken);
        }
        catch (TimeoutException) when (!signIn.IsCompleted)
        {
            throw new TimeoutException($"Timed out after {DefaultWaitTimeout.TotalSeconds:F0} s waiting for the sign-in to GitHub.");
        }

        if (result.Status is not { State: AccountState.SignedIn })
        {
            throw new InvalidOperationException(result.Failure is { } failure
                ? $"Signing in to GitHub failed: {failure.Kind}: {failure.Message}"
                : $"Signing in to GitHub did not sign in: {result.Status?.State.ToString() ?? "no account"}.");
        }

        Settle();
    }

    /// <summary>
    /// Refuses a fake without a server (<see cref="FakeGitHub.InProcess"/>) as <c>gitHub</c>: its
    /// endpoints are <c>Local(1)</c>, which the default handler would try to reach and fail on. Such
    /// a fake goes in <c>hosting: new HostingOptions(fake.Endpoints, fake.CreateHandler)</c>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="gitHub"/> is in process.</exception>
    private static void RefuseInProcess(FakeGitHub? gitHub, string parameterName)
    {
        if (gitHub is { Port: FakeGitHub.InProcessPort })
        {
            throw new ArgumentException(
                "An in-process fake of GitHub has no server to reach: give it as hosting: new HostingOptions(fake.Endpoints, fake.CreateHandler), not as gitHub.",
                parameterName);
        }
    }

    /// <summary>
    /// The harness's hosting options (2.5): with a fake, its endpoints through a
    /// <see cref="LoopbackOnlyHandler"/> over the default handler; without one, GitHub off with the
    /// tripwire over a handler that uses no proxy and follows no redirect.
    /// </summary>
    private static HostingOptions HarnessHosting(FakeGitHub? gitHub, RefusedRequests refused)
    {
        if (gitHub is not null)
        {
            var endpoints = gitHub.Endpoints;
            var inner = HostingOptions.DefaultHandler(endpoints);
            return new HostingOptions(endpoints, () => new LoopbackOnlyHandler(inner(), refused));
        }

        return HostingOptions.Off with
        {
            CreateHandler = () => new LoopbackOnlyHandler(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All }, refused),
        };
    }

    /// <summary>
    /// Runs the UI thread's jobs, 15 ms apart, until the hosting session's start has ended: its
    /// posts need the UI thread, so the start is never waited on by blocking. Rethrows what the
    /// start threw; a cancelled start is a failure too.
    /// </summary>
    /// <exception cref="TimeoutException">The start had not ended after <see cref="DefaultWaitTimeout"/>.</exception>
    /// <exception cref="OperationCanceledException">The start was cancelled.</exception>
    private void PumpUntilHostingStarted()
    {
        var started = Session.HostingStarted;
        var clock = Stopwatch.StartNew();
        while (!started.IsCompleted)
        {
            if (clock.Elapsed > DefaultWaitTimeout)
            {
                throw new TimeoutException($"Timed out after {DefaultWaitTimeout.TotalSeconds:F0} s waiting for the hosting session's start.");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(PollInterval);
        }

        // Completed: throws what a faulted start threw, and a cancelled one's cancellation.
        started.GetAwaiter().GetResult();
    }
}
