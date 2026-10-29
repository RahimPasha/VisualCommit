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
    /// merge) the session is <see cref="NullHostingSession"/>, so it throws.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not signed in afterwards.</exception>
    public async Task SignInAsync(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        Dispatcher.UIThread.VerifyAccess();
        var result = await Session.Services.Hosting.SignInAsync(token, CancellationToken.None);
        if (result.Status is not { State: AccountState.SignedIn })
        {
            throw new InvalidOperationException(result.Failure is { } failure
                ? $"Signing in to GitHub failed: {failure.Kind}: {failure.Message}"
                : $"Signing in to GitHub did not sign in: {result.Status?.State.ToString() ?? "no account"}.");
        }

        Settle();
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
    /// start threw.
    /// </summary>
    /// <exception cref="TimeoutException">The start had not ended after <see cref="DefaultWaitTimeout"/>.</exception>
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

        if (started.IsFaulted)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(started.Exception!.GetBaseException()).Throw();
        }
    }
}
