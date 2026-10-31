using System.Runtime.CompilerServices;

// The client's and the stores' parsers and helpers are internal; their tests read them directly.
[assembly: InternalsVisibleTo("VisualCommit.Hosting.Tests")]
