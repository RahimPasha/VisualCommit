using System.Text;
using VisualCommit.Core;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="DurableFile"/>, the one way the app saves its own files (settings.json, session.json,
/// workspaces.json, merge-tools.json): the text written beside the file is flushed to the disk
/// before it is swapped in, through a stream that records what is done to it.
/// </summary>
public class DurableFileTests
{
    [Fact]
    public void Replace_flushes_the_whole_text_to_the_disk_before_it_swaps_the_file_in()
    {
        using var folder = new TempDirectory("durable");
        var path = Path.Combine(folder.Path, "settings.json");
        File.WriteAllText(path, "old");
        var events = new List<string>();
        const string Text = "{ \"theme\": \"Dark\" }";

        DurableFile.Replace(path, Text, file => new RecordingStream(file, events, () => File.ReadAllText(path)));

        Assert.Equal(
            [
                "open settings.json.tmp",
                $"write {Text.Length}",
                $"flush to disk at {Text.Length} while the file holds old",
                "close",
            ],
            events);
        Assert.Equal(Text, File.ReadAllText(path));
        Assert.False(File.Exists(DurableFile.TemporaryOf(path)));
    }

    [Fact]
    public void Write_writes_utf8_without_a_byte_order_mark_as_write_all_text_does_and_replaces_what_was_there()
    {
        using var folder = new TempDirectory("durable");
        var ours = Path.Combine(folder.Path, "ours.json");
        var theirs = Path.Combine(folder.Path, "theirs.json");
        const string Text = "{ \"name\": \"Zoë — ✓\" }";
        File.WriteAllText(ours, new string('x', 200));

        DurableFile.Write(ours, Text);
        File.WriteAllText(theirs, Text);

        Assert.Equal(File.ReadAllBytes(theirs), File.ReadAllBytes(ours));
        Assert.Equal(Encoding.UTF8.GetByteCount(Text), new FileInfo(ours).Length);
    }

    [Fact]
    public void A_replace_that_cannot_write_leaves_the_file_as_it_was()
    {
        using var folder = new TempDirectory("durable");
        var path = Path.Combine(folder.Path, "session.json");
        File.WriteAllText(path, "old");

        Assert.Throws<IOException>(() => DurableFile.Replace(path, "new", _ => throw new IOException("The disk is full.")));

        Assert.Equal("old", File.ReadAllText(path));
    }

    /// <summary>A file stream that records its writes, its flushes to the disk (with what the target file holds then) and its close.</summary>
    private sealed class RecordingStream : FileStream
    {
        private readonly List<string> _events;
        private readonly Func<string> _target;
        private bool _closed;

        public RecordingStream(string path, List<string> events, Func<string> target)
            : base(path, FileMode.Create, FileAccess.Write, FileShare.Read)
        {
            _events = events;
            _target = target;
            events.Add($"open {System.IO.Path.GetFileName(path)}");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _events.Add($"write {count}");
            base.Write(buffer, offset, count);
        }

        public override void Flush(bool flushToDisk)
        {
            base.Flush(flushToDisk);

            // Only a flush to the disk counts: the stream may flush its buffer on its own as it closes.
            if (flushToDisk)
            {
                _events.Add($"flush to disk at {Length} while the file holds {_target()}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_closed)
            {
                _closed = true;
                _events.Add("close");
            }

            base.Dispose(disposing);
        }
    }
}
