namespace VisualCommit.Testing;

/// <summary>
/// A folder under the system temp folder that is deleted on dispose. Tests keep everything they
/// write (repos, data folders) in one of these, so they never touch real user files.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory(string label = "tmp")
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "VisualCommit.Tests",
            $"{label}-{Guid.NewGuid():N}"[..(label.Length + 13)]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Returns the full path of a file or folder inside this folder.</summary>
    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        // Git marks its object files read-only, and a process that has only just exited can
        // still hold a file for a moment on Windows. Clear the attributes and retry briefly.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!Directory.Exists(Path))
                {
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Path, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }

        // Left behind in the temp folder; not worth failing a test over.
    }
}
