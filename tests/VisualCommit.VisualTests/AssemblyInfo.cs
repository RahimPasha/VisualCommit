using Avalonia.Headless;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]
[assembly: HangWatchdog]
