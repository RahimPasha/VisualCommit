using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="FakeSsh"/> (D115) under real git: a fetch of the <c>fake-ssh</c> remote runs the
/// script as <c>core.sshCommand</c>, which asks <c>$SSH_ASKPASS</c> the recorded texts in turn,
/// writes the answers and fails as ssh would. The askpass program here is a stand-in script that
/// records what it was asked (the app's helper takes its place in the checks), or git itself as a
/// native program, which shows that the texts reach such a program unchanged.
/// </summary>
public class FakeSshTests
{
    [Fact]
    public async Task A_fetch_asks_the_three_recorded_prompts_in_turn_writes_the_answers_and_fails()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var fake = await FakeSsh.AddRemoteAsync(repo);
        var askpass = StandInAskPass.Create(repo.SiblingPath("askpass"));

        // An SSH_ASKPASS_PROMPT left in the environment does not reach the program: OpenSSH 8.9
        // sets none for these prompts.
        var fetch = await FetchAsync(repo, askpass.Path, ("SSH_ASKPASS_PROMPT", "confirm"));

        Assert.False(fetch.Succeeded);
        Assert.Contains(FakeSsh.Refusal, fetch.StandardError, StringComparison.Ordinal);
        Assert.Contains("Could not read from remote repository", fetch.StandardError, StringComparison.Ordinal);
        Assert.Equal(["s3cret", "yes", "123456"], fake.Answers);
        Assert.Equal(
            [
                ("unset", "Enter passphrase for key '/home/u/.ssh/id_ed25519': "),
                ("unset", "The authenticity of host '[127.0.0.1]:47392 ([127.0.0.1]:47392)' can't be established.\nED25519 key fingerprint is SHA256:skokdAxs76eWz/jm50ga0Qz+ewMVrjE4KE91GAFIgTE.\nThis key is not known by any other names\nAre you sure you want to continue connecting (yes/no/[fingerprint])? "),
                ("unset", "Enter PIN for ED25519-SK key /home/u/.ssh/id_ed25519_sk: "),
            ],
            askpass.Asked());

        // Git ran it once, as ssh with the simple variant: the host, then the remote command.
        var call = Assert.Single(fake.Calls);
        Assert.StartsWith("git@example.invalid ", call, StringComparison.Ordinal);
        Assert.Contains("git-upload-pack", call, StringComparison.Ordinal);
        Assert.Equal(FakeSsh.RemoteUrl, (await repo.GitAsync("remote", "get-url", FakeSsh.RemoteName)).StandardOutput.Trim());
        Assert.Equal("simple", (await repo.GitAsync("config", "ssh.variant")).StandardOutput.Trim());
    }

    [Fact]
    public async Task A_prompt_without_an_answer_ends_the_script_and_the_prompts_after_it_are_not_asked()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var fake = await FakeSsh.AddRemoteAsync(repo, prompts: [FakeSsh.PassphrasePrompt, "Nobody answers this: ", FakeSsh.PinPrompt]);
        var askpass = StandInAskPass.Create(repo.SiblingPath("askpass"));

        var fetch = await FetchAsync(repo, askpass.Path);

        Assert.False(fetch.Succeeded);
        Assert.Contains(FakeSsh.Refusal, fetch.StandardError, StringComparison.Ordinal);
        Assert.Equal(["s3cret"], fake.Answers);
        Assert.Equal([FakeSsh.PassphrasePrompt, "Nobody answers this: "], askpass.Asked().Select(asked => asked.Prompt));
    }

    [Fact]
    public async Task Without_an_askpass_program_the_script_refuses_at_once()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        var fake = await FakeSsh.AddRemoteAsync(repo);

        var fetch = await FetchAsync(repo, askPass: null);

        Assert.False(fetch.Succeeded);
        Assert.Contains(FakeSsh.Refusal, fetch.StandardError, StringComparison.Ordinal);
        Assert.Empty(fake.Answers);
        Assert.Single(fake.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task A_native_askpass_program_gets_each_recorded_text_unchanged(int which)
    {
        // Git itself as the askpass program: it repeats an unknown command, here the prompt, in
        // its error, which reaches the fetch's standard error. Git for Windows' shell would turn
        // '/home/u/...' into a Windows path if the script let it.
        using var repo = await Scenarios.RemoteSyncAsync();
        var prompt = FakeSsh.RecordedPrompts[which];
        var fake = await FakeSsh.AddRemoteAsync(repo, prompts: [prompt]);

        var fetch = await FetchAsync(repo, ((GitRunner)repo.Runner).ExecutablePath);

        Assert.False(fetch.Succeeded);
        Assert.Contains("'" + prompt + "'", fetch.StandardError.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Empty(fake.Answers);
    }

    [Fact]
    public void Texts_are_quoted_for_the_shell_whatever_they_hold()
    {
        Assert.Equal("'plain'", FakeSsh.Quote("plain"));
        Assert.Equal("'can'\\''t'", FakeSsh.Quote("can't"));
        Assert.Equal("'$HOME `x` \"y\"\nz'", FakeSsh.Quote("$HOME `x` \"y\"\nz"));
    }

    private static Task<GitResult> FetchAsync(TempRepo repo, string? askPass, params (string Name, string Value)[] more)
    {
        var environment = new Dictionary<string, string?>(repo.Environment) { ["SSH_ASKPASS"] = askPass };
        foreach (var (name, value) in more)
        {
            environment[name] = value;
        }

        var command = new GitCommand("fetch", FakeSsh.RemoteName) { WorkingDirectory = repo.Path, Environment = environment };
        return repo.Runner.RunAsync(command, TestCancelled);
    }

    /// <summary>
    /// An askpass program for these tests: a shell script that records each prompt and the
    /// <c>SSH_ASKPASS_PROMPT</c> it saw, and answers the passphrase with "s3cret" and a carriage
    /// return (as a Windows program's line ending), the host key with "yes", a PIN with "123456",
    /// and anything else with nothing and exit code 1.
    /// </summary>
    private sealed class StandInAskPass
    {
        private const string End = "--end of prompt--";

        private StandInAskPass(string folder) => Folder = folder;

        public string Folder { get; }

        /// <summary>The value for <c>SSH_ASKPASS</c>, as the shell takes it.</summary>
        public string Path => ShellPath(System.IO.Path.Combine(Folder, "askpass.sh"));

        private string LogPath => System.IO.Path.Combine(Folder, "asked.txt");

        public static StandInAskPass Create(string folder)
        {
            Directory.CreateDirectory(folder);
            var askpass = new StandInAskPass(folder);
            var script = new StringBuilder()
                .Append("#!/bin/sh\n")
                .Append("log=").Append(FakeSsh.Quote(ShellPath(askpass.LogPath))).Append('\n')
                .Append("printf '%s\\n' \"${SSH_ASKPASS_PROMPT-unset}\" >> \"$log\"\n")
                .Append("printf '%s\\n' \"$1\" ").Append(FakeSsh.Quote(End)).Append(" >> \"$log\"\n")
                .Append("case \"$1\" in\n")
                .Append("\t*passphrase*) printf 's3cret\\r\\n' ;;\n")
                .Append("\t*authenticity*) printf 'yes\\n' ;;\n")
                .Append("\t*PIN*) printf '123456\\n' ;;\n")
                .Append("\t*) exit 1 ;;\n")
                .Append("esac\n")
                .ToString();
            var file = System.IO.Path.Combine(folder, "askpass.sh");
            File.WriteAllText(file, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            return askpass;
        }

        /// <summary>Each prompt asked, in order, with the <c>SSH_ASKPASS_PROMPT</c> the program saw ("unset" for none).</summary>
        public IReadOnlyList<(string Kind, string Prompt)> Asked()
        {
            if (!File.Exists(LogPath))
            {
                return [];
            }

            var asked = new List<(string Kind, string Prompt)>();
            var lines = File.ReadAllText(LogPath).Split('\n');
            var i = 0;
            while (i < lines.Length && lines[i].Length > 0)
            {
                var kind = lines[i++];
                var prompt = new List<string>();
                while (lines[i] != End)
                {
                    prompt.Add(lines[i++]);
                }

                i++;
                asked.Add((kind, string.Join('\n', prompt)));
            }

            return asked;
        }

        private static string ShellPath(string path) => OperatingSystem.IsWindows() ? path.Replace('\\', '/') : path;
    }
}
