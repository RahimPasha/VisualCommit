using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using VisualCommit.AskPass;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The helper's credential mode (docs/phase-7a-design.md 4.5, D255, D273): what it reads of git's
/// input, the answer it prints, and the real helper process run as git runs a credential helper
/// (<see cref="AskPassRunner.RunCredentialAsync"/>) against a pipe of the test's own that answers
/// as the app's broker would.
/// </summary>
public class CredentialHelperTests
{
    // What git sent a recording helper on get (docs/phase-7a-design.md 4.2): 2.43 gives only the
    // protocol and the host; 2.46 and later announce capabilities and pass the server's challenge.
    private const string Get243 = "protocol=https\nhost=github.com\n\n";

    private const string Get256 =
        "capability[]=authtype\ncapability[]=state\nprotocol=https\nhost=127.0.0.1:47395\n"
        + "wwwauth[]=Basic realm=\"GitHub\"\n\n";

    private const string Token = "github_pat_FAKE-octocat-1";

    [Fact]
    public void Git_2_43s_get_gives_the_protocol_and_the_host()
    {
        var input = AskPassProtocol.ParseGitCredentialInput(Get243);

        Assert.Equal(new GitCredentialInput("https", "github.com", null, null), input);
    }

    [Fact]
    public void Git_2_56s_get_keeps_the_four_keys_and_ignores_capabilities_challenges_and_the_rest()
    {
        var input = AskPassProtocol.ParseGitCredentialInput(
            "capability[]=authtype\r\nstate[]=helper:abc\nprotocol=https\npath=o/r.git\nurl=https://github.com/o/r.git\n"
            + "host=GitHub.com:443\nwwwauth[]=Basic realm=\"GitHub\"\nusername=octocat\npassword_expiry_utc=1700000000\n"
            + "something-new=1\nno equals sign\n\n" + "host=after-the-end\n");

        Assert.Equal(new GitCredentialInput("https", "GitHub.com:443", "octocat", null), input);
        Assert.Equal(new GitCredentialInput("https", "127.0.0.1:47395", null, null), AskPassProtocol.ParseGitCredentialInput(Get256));
    }

    [Fact]
    public void An_erase_keeps_the_user_and_a_password_split_at_its_first_equals_sign()
    {
        var input = AskPassProtocol.ParseGitCredentialInput("protocol=https\r\nhost=github.com\r\nusername=x-access-token\r\npassword=a=b==c\r\n");

        Assert.Equal(new GitCredentialInput("https", "github.com", "x-access-token", "a=b==c"), input);
        Assert.DoesNotContain("a=b==c", input.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_answer_is_the_fixed_user_name_and_the_token_on_two_lines()
    {
        Assert.Equal($"username=x-access-token\npassword={Token}\n", AskPassProtocol.CredentialAnswerText(Token));
        Assert.Equal(GitCredential.UserName, AskPassProtocol.CredentialUserName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("to\rken")]
    [InlineData("to\nken")]
    [InlineData("token\n")]
    [InlineData("to\0ken")]
    public void A_missing_token_or_one_that_would_break_gits_format_prints_nothing(string? token)
    {
        Assert.Null(AskPassProtocol.CredentialAnswerText(token));
    }

    [Fact]
    public void A_credential_request_goes_over_the_pipe_and_back_and_is_never_taken_for_a_prompt()
    {
        var request = new AskPassCredentialRequest("t", "op-1", "erase", "https", "github.com", "x-access-token", AskPassProtocol.SecretHash(Token));
        var line = AskPassProtocol.FormatCredentialRequest(request);

        Assert.Equal(request, AskPassProtocol.ParseCredentialRequest(line.AsSpan(0, line.Length - 1)));
        Assert.Null(AskPassProtocol.ParseRequest(line.AsSpan(0, line.Length - 1)));
        Assert.DoesNotContain(Token, Encoding.UTF8.GetString(line), StringComparison.Ordinal);
        var prompt = AskPassProtocol.FormatRequest(new AskPassRequest("t", "op-1", "Username for 'https://github.com': ", null));
        Assert.Null(AskPassProtocol.ParseCredentialRequest(prompt.AsSpan(0, prompt.Length - 1)));
    }

    [Theory]
    [InlineData("""{"token":"t","operation":"o","credential":"store","protocol":"https","host":"h","username":null,"secretHash":null}""")]
    [InlineData("""{"token":"t","operation":"o","credential":"get","protocol":"https","username":null,"secretHash":null}""")]
    [InlineData("""{"token":"t","operation":"o","credential":"get","protocol":"https","host":"h","secretHash":null}""")]
    [InlineData("""{"token":"t","operation":"o","credential":"get","protocol":"https","host":3,"username":null,"secretHash":null}""")]
    [InlineData("not json")]
    public void A_line_that_is_not_a_get_or_an_erase_with_its_strings_is_no_credential_request(string line)
    {
        Assert.Null(AskPassProtocol.ParseCredentialRequest(Encoding.UTF8.GetBytes(line)));
    }

    [Fact]
    public void The_secret_hash_is_the_lower_case_hex_SHA_256_of_the_UTF_8_bytes()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", AskPassProtocol.SecretHash("abc"));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("é"))), AskPassProtocol.SecretHash("é"));
    }

    [Fact]
    public async Task A_get_prints_the_two_lines_and_exits_0()
    {
        await using var pipe = new CredentialPipe(Token);

        var result = await RunAsync("get", Get256, pipe.Environment);

        Assert.Equal((0, $"username=x-access-token\npassword={Token}\n", string.Empty), (result.ExitCode, result.StandardOutput, result.StandardError));
        var request = Assert.Single(pipe.Requests);
        Assert.Equal(new AskPassCredentialRequest(pipe.Token, "op-1", "get", "https", "127.0.0.1:47395", null, null), request);
    }

    [Fact]
    public async Task An_erase_sends_the_refused_passwords_hash_never_the_password_and_prints_nothing()
    {
        await using var pipe = new CredentialPipe(null);

        var result = await RunAsync("erase", $"protocol=https\nhost=github.com\nusername=x-access-token\npassword={Token}\n\n", pipe.Environment);

        Assert.Equal((0, string.Empty, string.Empty), (result.ExitCode, result.StandardOutput, result.StandardError));
        var request = Assert.Single(pipe.Requests);
        Assert.Equal(("erase", "x-access-token", AskPassProtocol.SecretHash(Token)), (request.Action, request.Username, request.SecretHash));
        Assert.All(pipe.Lines, line => Assert.DoesNotContain(Token, line, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tok\nen")]
    public async Task No_answer_or_an_answer_that_would_break_gits_format_prints_nothing(string? answer)
    {
        await using var pipe = new CredentialPipe(answer);

        var result = await RunAsync("get", Get243, pipe.Environment);

        Assert.Equal((0, string.Empty, string.Empty), (result.ExitCode, result.StandardOutput, result.StandardError));
        Assert.Single(pipe.Requests);
    }

    [Fact]
    public async Task The_deny_variable_does_not_stop_a_get()
    {
        await using var pipe = new CredentialPipe(Token);
        var environment = new Dictionary<string, string?>(pipe.Environment) { [AskPassProtocol.DenyVariable] = "1" };

        var result = await RunAsync("get", Get243, environment);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("username=x-access-token\n", result.StandardOutput, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("store")]
    [InlineData("approve")]
    [InlineData(null)]
    public async Task Store_an_unknown_action_or_none_exits_0_without_a_connection(string? action)
    {
        await using var pipe = new CredentialPipe(Token);

        var result = await RunAsync(action, $"protocol=https\nhost=github.com\nusername=x-access-token\npassword={Token}\n\n", pipe.Environment);

        Assert.Equal((0, string.Empty, string.Empty), (result.ExitCode, result.StandardOutput, result.StandardError));
        Assert.Equal(0, pipe.Connections);
    }

    [Fact]
    public async Task Without_the_pipes_variables_or_without_a_host_a_get_exits_0_without_a_connection()
    {
        await using var pipe = new CredentialPipe(Token);
        var noPipe = new Dictionary<string, string?>(pipe.Environment) { [AskPassProtocol.PipeVariable] = null };
        var noToken = new Dictionary<string, string?>(pipe.Environment) { [AskPassProtocol.TokenVariable] = null };

        var results = new[]
        {
            await RunAsync("get", Get243, noPipe),
            await RunAsync("get", Get243, noToken),
            await RunAsync("get", "protocol=https\n\n", pipe.Environment),
            await RunAsync("erase", Get243, pipe.Environment),
        };

        Assert.All(results, result => Assert.Equal((0, string.Empty, string.Empty), (result.ExitCode, result.StandardOutput, result.StandardError)));
        Assert.Equal(0, pipe.Connections);
    }

    [Fact]
    public async Task A_pipe_nobody_listens_on_exits_0_with_nothing()
    {
        var environment = new Dictionary<string, string?>
        {
            [AskPassProtocol.PipeVariable] = "vc-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)),
            [AskPassProtocol.TokenVariable] = "t",
            [AskPassProtocol.OperationVariable] = "op-1",
        };

        var result = await RunAsync("get", Get243, environment);

        Assert.Equal((0, string.Empty, string.Empty), (result.ExitCode, result.StandardOutput, result.StandardError));
    }

    [Fact]
    public async Task More_input_than_a_line_may_hold_gets_no_answer_and_is_read_to_its_end()
    {
        await using var pipe = new CredentialPipe(Token);
        var input = Get243.TrimEnd('\n') + "\nwwwauth[]=" + new string('x', AskPassProtocol.MaxLineBytes) + "\n\n";

        var result = await RunAsync("get", input, pipe.Environment);

        Assert.Equal((0, string.Empty, string.Empty), (result.ExitCode, result.StandardOutput, result.StandardError));
        Assert.Equal(0, pipe.Connections);
    }

    private static Task<AskPassResult> RunAsync(string? action, string input, IReadOnlyDictionary<string, string?> environment) =>
        AskPassRunner.RunCredentialAsync(action, input, environment, cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>
    /// A pipe that answers every credential request with one answer, as the app's broker would,
    /// and keeps the lines it got. Made as the broker makes its own: only the current user can open it.
    /// </summary>
    private sealed class CredentialPipe : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentQueue<string> _lines = new();
        private readonly Task _serving;
        private int _connections;

        public CredentialPipe(string? answer)
        {
            Name = "vc-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
            _serving = Task.Run(() => ServeAsync(answer));
        }

        public string Name { get; }

        public string Token { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

        public int Connections => Volatile.Read(ref _connections);

        public IReadOnlyList<string> Lines => [.. _lines];

        public IReadOnlyList<AskPassCredentialRequest> Requests =>
            [.. Lines.Select(line => AskPassProtocol.ParseCredentialRequest(Encoding.UTF8.GetBytes(line)) ?? throw new InvalidOperationException("Not a credential request."))];

        public IReadOnlyDictionary<string, string?> Environment => new Dictionary<string, string?>
        {
            [AskPassProtocol.PipeVariable] = Name,
            [AskPassProtocol.TokenVariable] = Token,
            [AskPassProtocol.OperationVariable] = "op-1",
            [AskPassProtocol.DenyVariable] = null,
        };

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            await _serving;
            _stop.Dispose();
        }

        private async Task ServeAsync(string? answer)
        {
            while (!_stop.IsCancellationRequested)
            {
                using var pipe = OperatingSystem.IsWindows()
                    ? NamedPipeServerStreamAcl.Create(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, WindowsPipeUser.ServerSecurity())
                    : new NamedPipeServerStream(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                try
                {
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    Interlocked.Increment(ref _connections);
                    var line = await AskPassProtocol.ReadLineAsync(pipe, _stop.Token);
                    if (line is not null)
                    {
                        _lines.Enqueue(Encoding.UTF8.GetString(line));
                    }

                    await pipe.WriteAsync(AskPassProtocol.FormatAnswer(answer), _stop.Token);
                    await pipe.FlushAsync(_stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    // The helper went away: listen again.
                }
            }
        }
    }
}
