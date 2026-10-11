using System.Text;

namespace VisualCommit.Testing;

/// <summary>
/// A stand-in for ssh that git runs as a repository's <c>core.sshCommand</c> (D115, design 3.5):
/// a shell script that, like OpenSSH 8.9 meeting a passphrase-protected key, an unknown host and a
/// security key, runs <c>"$SSH_ASKPASS"</c> once per recorded prompt text, in turn, with the text
/// as its one argument and <c>SSH_ASKPASS_PROMPT</c> unset (as OpenSSH 8.9 leaves it for these
/// three). It appends each answer (the program's standard output up to its first carriage return
/// or line feed, as OpenSSH reads it) to <see cref="AnswersPath"/>, one per line. It then prints OpenSSH's refusal of the key
/// (<see cref="Refusal"/>) to standard error and exits 1, so the fetch fails as one against a
/// server that refused every key. A prompt that gets no answer (the program exits with an error,
/// or <c>SSH_ASKPASS</c> is not set) ends the script there in the same way, as ssh gives up: the
/// prompts after it are not asked.
/// <para>
/// No network is involved: the remote's host, <c>example.invalid</c>, is never resolved. The
/// script runs through <c>sh</c> on every platform (git for Windows' own <c>sh</c> there), so it
/// needs nothing from the machine beyond git. It turns off the argument conversion of Git for
/// Windows' shell (<c>MSYS_NO_PATHCONV</c>, <c>MSYS2_ARG_CONV_EXCL</c>), so that a native askpass
/// program gets the texts unchanged, <c>'/home/u/.ssh/id_ed25519'</c> included.
/// </para>
/// </summary>
public sealed class FakeSsh
{
    /// <summary>The remote check 21b adds: <c>fake-ssh</c>.</summary>
    public const string RemoteName = "fake-ssh";

    /// <summary>The remote's URL: an ssh URL whose host is never looked up.</summary>
    public const string RemoteUrl = "ssh://git@example.invalid/x.git";

    /// <summary>OpenSSH 8.9's question for the passphrase of a key (recorded, check 21b).</summary>
    public const string PassphrasePrompt = "Enter passphrase for key '/home/u/.ssh/id_ed25519': ";

    /// <summary>
    /// OpenSSH 8.9's question about an unknown host key: four lines, the last ending in a space
    /// (recorded from Git for Windows' OpenSSH against a local sshd, check 21b).
    /// </summary>
    public const string HostKeyPrompt =
        "The authenticity of host '[127.0.0.1]:47392 ([127.0.0.1]:47392)' can't be established.\n" +
        "ED25519 key fingerprint is SHA256:skokdAxs76eWz/jm50ga0Qz+ewMVrjE4KE91GAFIgTE.\n" +
        "This key is not known by any other names\n" +
        "Are you sure you want to continue connecting (yes/no/[fingerprint])? ";

    /// <summary>OpenSSH's question for a security key's PIN, which no dialog of its own recognises (the generic prompt, check 21b).</summary>
    public const string PinPrompt = "Enter PIN for ED25519-SK key /home/u/.ssh/id_ed25519_sk: ";

    /// <summary>What the script prints to standard error before it exits 1: OpenSSH's text for a refused key.</summary>
    public const string Refusal = "git@example.invalid: Permission denied (publickey).";

    /// <summary>The script's file name in its folder.</summary>
    public const string ScriptName = "fake-ssh.sh";

    private FakeSsh(string folder, IReadOnlyList<string> prompts)
    {
        Folder = folder;
        Prompts = prompts;
    }

    /// <summary>The three texts of check 21b, in the order the script asks them: passphrase, host key, PIN.</summary>
    public static IReadOnlyList<string> RecordedPrompts { get; } = [PassphrasePrompt, HostKeyPrompt, PinPrompt];

    /// <summary>The folder that holds the script, the answer file and the call file.</summary>
    public string Folder { get; }

    /// <summary>The texts the script asks, in order.</summary>
    public IReadOnlyList<string> Prompts { get; }

    /// <summary>The script.</summary>
    public string ScriptPath => Path.Combine(Folder, ScriptName);

    /// <summary>The file the answers are appended to, one per line.</summary>
    public string AnswersPath => Path.Combine(Folder, "answers.txt");

    /// <summary>The file each run of the script appends its arguments to (git's host and remote command), one line per run.</summary>
    public string CallsPath => Path.Combine(Folder, "calls.txt");

    /// <summary>
    /// The value for <c>core.sshCommand</c>: <c>sh</c> with the script's path quoted for the shell,
    /// with forward slashes on Windows. Git runs it through its shell, which adds git's arguments.
    /// </summary>
    public string SshCommand => "sh " + Quote(ShellPath(ScriptPath));

    /// <summary>The answers the script has written so far, in order; empty when it has written none.</summary>
    public IReadOnlyList<string> Answers => ReadLines(AnswersPath);

    /// <summary>The arguments of each run of the script so far, one entry per run.</summary>
    public IReadOnlyList<string> Calls => ReadLines(CallsPath);

    /// <summary>
    /// Writes the script into <paramref name="folder"/> (created when missing), asking
    /// <paramref name="prompts"/> in turn, or <see cref="RecordedPrompts"/> when null.
    /// </summary>
    public static FakeSsh Create(string folder, IReadOnlyList<string>? prompts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var fake = new FakeSsh(Path.GetFullPath(folder), prompts ?? RecordedPrompts);
        Directory.CreateDirectory(fake.Folder);
        File.WriteAllText(fake.ScriptPath, fake.Script(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                fake.ScriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }

        return fake;
    }

    /// <summary>
    /// What check 21b does through <see cref="TempRepo"/> before the app starts: writes the script
    /// into the folder <c>fake-ssh</c> next to <paramref name="repo"/>'s working tree, adds the
    /// remote <paramref name="remoteName"/> at <see cref="RemoteUrl"/> and sets the repository's
    /// <c>core.sshCommand</c> to the script and <c>ssh.variant</c> to <c>simple</c>.
    /// </summary>
    public static async Task<FakeSsh> AddRemoteAsync(TempRepo repo, string remoteName = RemoteName, IReadOnlyList<string>? prompts = null)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        var fake = Create(repo.SiblingPath("fake-ssh"), prompts);
        await repo.GitAsync("remote", "add", remoteName, RemoteUrl).ConfigureAwait(false);
        await repo.ConfigAsync("core.sshCommand", fake.SshCommand).ConfigureAwait(false);
        await repo.ConfigAsync("ssh.variant", "simple").ConfigureAwait(false);
        return fake;
    }

    /// <summary>The script, with Unix line endings, which every <c>sh</c> reads.</summary>
    private string Script()
    {
        var script = new StringBuilder()
            .Append("#!/bin/sh\n")
            .Append("# A stand-in for ssh, written by VisualCommit's tests (FakeSsh, D115). Git runs it as\n")
            .Append("# core.sshCommand; it asks $SSH_ASKPASS as OpenSSH 8.9 would and never connects.\n")
            .Append("unset SSH_ASKPASS_PROMPT\n")
            .Append("MSYS_NO_PATHCONV=1; export MSYS_NO_PATHCONV\n")
            .Append("MSYS2_ARG_CONV_EXCL='*'; export MSYS2_ARG_CONV_EXCL\n")
            .Append("answers=").Append(Quote(ShellPath(AnswersPath))).Append('\n')
            .Append("cr=$(printf '\\r')\n")
            .Append("nl='\n'\n")
            .Append("printf '%s\\n' \"$*\" >> ").Append(Quote(ShellPath(CallsPath))).Append('\n')
            .Append("refuse() {\n")
            .Append("\tprintf '%s\\n' ").Append(Quote(Refusal)).Append(" >&2\n")
            .Append("\texit 1\n")
            .Append("}\n")
            .Append("ask() {\n")
            .Append("\t[ -n \"$SSH_ASKPASS\" ] || refuse\n")
            .Append("\tanswer=$(\"$SSH_ASKPASS\" \"$1\") || refuse\n")
            .Append("\t# As OpenSSH reads it: up to the first carriage return or line feed.\n")
            .Append("\tanswer=${answer%%\"$cr\"*}\n")
            .Append("\tanswer=${answer%%\"$nl\"*}\n")
            .Append("\tprintf '%s\\n' \"$answer\" >> \"$answers\"\n")
            .Append("}\n");
        foreach (var prompt in Prompts)
        {
            script.Append("ask ").Append(Quote(prompt)).Append('\n');
        }

        return script.Append("refuse\n").ToString();
    }

    /// <summary>Quotes <paramref name="text"/> for sh: in single quotes, each single quote written as <c>'\''</c>. Line feeds stay as they are.</summary>
    public static string Quote(string text) => "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>A path as the shell takes it: with forward slashes on Windows (<c>C:/Users/...</c>), which Git for Windows' shell reads as the same file.</summary>
    private static string ShellPath(string path) => OperatingSystem.IsWindows() ? path.Replace('\\', '/') : path;

    private static string[] ReadLines(string file)
    {
        if (!File.Exists(file))
        {
            return [];
        }

        var text = File.ReadAllText(file);
        if (text.EndsWith('\n'))
        {
            text = text[..^1];
        }

        return text.Length == 0 && new FileInfo(file).Length == 0 ? [] : text.Split('\n');
    }
}
