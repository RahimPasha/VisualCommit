using System.Runtime.CompilerServices;

// The parsers of git's output are internal; their tests read them directly.
[assembly: InternalsVisibleTo("VisualCommit.Git.Tests")]
