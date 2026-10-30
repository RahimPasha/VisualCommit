using System.Runtime.CompilerServices;

// The credential test server's reading of pkt-lines is internal; its tests read it directly.
[assembly: InternalsVisibleTo("VisualCommit.Git.Tests")]

// The driver's own tests stop a window's input clock (HeadlessInputClock) to stamp a click and a
// double-click as one moment.
[assembly: InternalsVisibleTo("VisualCommit.App.Tests")]
