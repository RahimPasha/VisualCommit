using System.Runtime.CompilerServices;
using Avalonia.Headless;
using VisualCommit.LiveHostingTests;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]
[assembly: HangWatchdog]
[assembly: CollectionBehavior(DisableTestParallelization = true)]   // one sandbox, steps in order

namespace VisualCommit.LiveHostingTests
{
    internal static class ProcessIsolation
    {
        /// <summary>
        /// Before <c>Main</c>, any test and Avalonia's set-up (docs/phase-7a-design.md 14.3): the live
        /// variables are read and removed first, since the isolation removes both too (1.8) and no git
        /// or app process may inherit them; then the process is cut off from the machine as every test
        /// project's is. Check_2's step limits sum to about 510 s (14.4), above the watchdog's default
        /// 180 s, so the limit is 600 s unless the owner set one. The watchdog holds each test to it on
        /// its own: Check_1's calls sum to about 440 s (13 to GitHub at 30 s, 10 to the fake in process
        /// at 5 s) and 30 s more per leftover, so the 600 s cover five leftovers at full limits (a
        /// stopped Check_2 leaves two: its branch and its pull request); the fake's start, a few git
        /// calls with no limit of their own, is outside that sum. Check_3's calls sum to about 150 s
        /// (D342). Real calls take a small part of their limits. The new providers' sums
        /// (docs/phase-7b-design.md 15.5; <c>LiveSteps.Sums</c> computes them from the limits, and
        /// <c>LiveSafetyTests</c> holds each under 600 s), each own call at 30 s, each call to the fake
        /// in process at 5 s, each app step at 60 s, the clone at 120 s and a merge ref's poll at its
        /// 30 s: Azure DevOps' Check_1 sends 4 calls and 6 for five leftovers (5 abandons and one refs
        /// POST), 300 s; its Check_1b_probes 17 calls, the first of them the identity read, and 17 to
        /// the fake, 595 s; its Check_2 the identity and the sandbox's reads and five app steps and the
        /// clone, 480 s; its Check_2b_probes 9 probes, two polls and the clean-up's 5 calls, with 10 to
        /// the fake (its refs delete beside the clean-up's among them), 530 s; its Check_3 (stage 2,
        /// 15.6) the identity, the sandbox and the read back, the clean-up's 4 calls (the active list,
        /// an abandon, the refs read and POST), four app steps (the sign-in through the dialog, the
        /// browser's list, the push, the create through the form) and the clone, whose limit its
        /// requests' load shares, 570 s; its Check_4 the identity, three app steps and the clone, 330 s. GitLab's
        /// Check_1 sends 4 calls and 15 for five leftovers (each request's close and delete, each
        /// branch's delete), 570 s; its Check_1b_probes 6 calls and 7 to the fake, 215 s; its Check_2
        /// the two reads, six app steps (two pushes) and the clone, 540 s; its Check_2b_probes 5 probes
        /// and the clean-up's 7 calls, with 5 to the fake, 385 s; its Check_3 as Azure DevOps' (the
        /// clean-up's 4 calls the opened list, the request's close and delete and the branch's delete),
        /// 570 s. The fakes' start, a few git calls, is outside the sums, as in 7a's.
        /// </summary>
        [ModuleInitializer]
        internal static void Isolate()
        {
            LiveSettings.CaptureAndRemove();          // first: the isolation removes both too (1.8)
            HeadlessTestApp.IsolateFromTheMachine();
            if (Environment.GetEnvironmentVariable(HangWatchdogAttribute.LimitVariable) is null)
            {
                // Check_2's step limits sum to about 510 s (14.4), above the default 180 s.
                Environment.SetEnvironmentVariable(HangWatchdogAttribute.LimitVariable, "600");
            }
        }
    }
}
