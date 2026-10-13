using System.Runtime.CompilerServices;

// The credential test server's reading of pkt-lines is internal; its tests read it directly.
[assembly: InternalsVisibleTo("VisualCommit.Git.Tests")]
