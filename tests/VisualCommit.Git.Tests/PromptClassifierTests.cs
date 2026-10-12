using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitPromptClassifier"/> on prompt texts as git and OpenSSH write them
/// (docs/phase-3-design.md 3.2). Git's texts come from <c>credential_ask_one</c>, the same from
/// git 2.30 to 2.55; OpenSSH's from ssh 8.x and 9.x (Git for Windows 2.36 bundles 8.9, section 0).
/// </summary>
public class PromptClassifierTests
{
    private const string HostKeyQuestion89 =
        "The authenticity of host 'github.com (140.82.121.4)' can't be established.\n" +
        "ED25519 key fingerprint is SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU.\n" +
        "This key is not known by any other names\n" +
        "Are you sure you want to continue connecting (yes/no/[fingerprint])? ";

    private const string HostKeyQuestion80 =
        "The authenticity of host 'example.com (93.184.216.34)' can't be established.\n" +
        "ECDSA key fingerprint is SHA256:4r1QGSo0wmmvE8k6Ox3gpJcGI7eljPUyhG4qSdqK7f0.\n" +
        "Are you sure you want to continue connecting (yes/no)? ";

    private const string HostKeyQuestion9WithOtherNames =
        "The authenticity of host 'gitlab.example.com (10.0.0.5)' can't be established.\n" +
        "ED25519 key fingerprint is SHA256:eUXGGm1YGsMAS7vkcx6JOJdOGHPem5gQp4taiCfCLB8.\n" +
        "This host key is known by the following other names/addresses:\n" +
        "    ~/.ssh/known_hosts:3: gitlab.example.com\n" +
        "Are you sure you want to continue connecting (yes/no/[fingerprint])? ";

    [Theory]
    [InlineData("Username for 'https://github.com': ", "https://github.com")]
    [InlineData("Username for 'http://127.0.0.1:47391': ", "http://127.0.0.1:47391")]
    [InlineData("Username for 'https://example.com/team/repo.git': ", "https://example.com/team/repo.git")]
    public void Gits_user_name_prompt_is_a_Username_with_its_url(string prompt, string url)
    {
        var result = GitPromptClassifier.Classify(prompt, sshPrompt: null);

        Assert.Equal(new GitPromptClassification(GitPromptKind.Username, Url: url), result);
        Assert.False(GitPromptClassifier.IsMasked(result.Kind, prompt));
    }

    [Theory]
    [InlineData("Password for 'https://alice@github.com': ", "https://alice@github.com", "alice")]
    [InlineData("Password for 'http://alice@127.0.0.1:47391': ", "http://alice@127.0.0.1:47391", "alice")]
    [InlineData("Password for 'https://first.last@example.com@git.example.com': ", "https://first.last@example.com@git.example.com", "first.last@example.com")]
    [InlineData("Password for 'https://example.com': ", "https://example.com", null)]
    public void Gits_password_prompt_is_a_Password_with_its_url_and_user(string prompt, string url, string? user)
    {
        var result = GitPromptClassifier.Classify(prompt, sshPrompt: null);

        Assert.Equal(new GitPromptClassification(GitPromptKind.Password, Url: url, User: user), result);
        Assert.True(GitPromptClassifier.IsMasked(result.Kind, prompt));
    }

    [Theory]
    [InlineData("Enter passphrase for key '/home/u/.ssh/id_ed25519': ", "/home/u/.ssh/id_ed25519")]
    [InlineData("Enter passphrase for key 'C:/Users/u/.ssh/id_rsa': ", "C:/Users/u/.ssh/id_rsa")]
    [InlineData("Enter passphrase for key '/Users/someone with a long name/.ssh/keys/work/id_ed25519_work_laptop_2026_with_a_ve': ", "/Users/someone with a long name/.ssh/keys/work/id_ed25519_work_laptop_2026_with_a_ve")]
    [InlineData("Enter passphrase for \"/home/u/.ssh/id_ed25519\": ", "/home/u/.ssh/id_ed25519")]
    [InlineData("Enter passphrase: ", null)]
    public void Sshs_passphrase_prompts_are_a_Passphrase_with_the_keys_path(string prompt, string? keyPath)
    {
        var result = GitPromptClassifier.Classify(prompt, sshPrompt: null);

        Assert.Equal(new GitPromptClassification(GitPromptKind.Passphrase, KeyPath: keyPath), result);
        Assert.True(GitPromptClassifier.IsMasked(result.Kind, prompt));
    }

    [Theory]
    [InlineData(HostKeyQuestion89)]
    [InlineData(HostKeyQuestion80)]
    [InlineData(HostKeyQuestion9WithOtherNames)]
    public void Sshs_host_key_question_is_a_HostKey(string prompt)
    {
        // OpenSSH 8.9 leaves SSH_ASKPASS_PROMPT unset for it (section 0).
        var result = GitPromptClassifier.Classify(prompt, sshPrompt: null);

        Assert.Equal(GitPromptKind.HostKey, result.Kind);
        Assert.False(GitPromptClassifier.IsMasked(result.Kind, prompt));
    }

    [Fact]
    public void A_confirm_prompt_of_ssh_is_a_Confirm()
    {
        const string prompt = "Allow use of key /home/u/.ssh/id_ed25519?\nKey fingerprint SHA256:Ok3qVUGkBo1yQ5k2Y8nXr7mI9qY7GJ0bJmKZbZr3y2U.";

        var result = GitPromptClassifier.Classify(prompt, GitPromptClassifier.SshConfirm);

        Assert.Equal(GitPromptKind.Confirm, result.Kind);
        Assert.False(GitPromptClassifier.IsMasked(result.Kind, prompt));

        // The host-key question is told by its text, even should ssh mark it as a confirmation.
        Assert.Equal(GitPromptKind.HostKey, GitPromptClassifier.Classify(HostKeyQuestion89, GitPromptClassifier.SshConfirm).Kind);
    }

    [Theory]
    [InlineData("Confirm user presence for key ED25519-SK SHA256:Ok3qVUGkBo1yQ5k2Y8nXr7mI9qY7GJ0bJmKZbZr3y2U")]
    [InlineData("Enter passphrase: ")]
    public void A_prompt_marked_none_is_a_Notice_whatever_its_text(string prompt)
    {
        var result = GitPromptClassifier.Classify(prompt, GitPromptClassifier.SshNotice);

        Assert.Equal(new GitPromptClassification(GitPromptKind.Notice), result);
        Assert.False(GitPromptClassifier.IsMasked(result.Kind, prompt));
    }

    [Theory]
    [InlineData("git@example.com's password: ", true)]
    [InlineData("(git@example.com) Password: ", true)]
    [InlineData("Enter PIN for ED25519-SK key /home/u/.ssh/id_ed25519_sk: ", true)]
    [InlineData("Enter pin for PKCS#11 token: ", true)]
    [InlineData("Personal access token: ", true)]
    [InlineData("Enter your PASSPHRASE now: ", true)]
    [InlineData("Verification code: ", false)]
    [InlineData("Type yes to go on: ", false)]
    [InlineData("", false)]
    public void Anything_else_is_Other_and_masked_when_it_asks_for_a_secret(string prompt, bool masked)
    {
        var result = GitPromptClassifier.Classify(prompt, sshPrompt: null);

        Assert.Equal(new GitPromptClassification(GitPromptKind.Other), result);
        Assert.Equal(masked, GitPromptClassifier.IsMasked(result.Kind, prompt));
    }

    [Theory]
    [InlineData("Username for 'https://github.com'")]
    [InlineData("username for 'https://github.com': ")]
    [InlineData("Password: ")]
    public void A_text_close_to_gits_prompts_is_not_taken_for_one(string prompt)
    {
        Assert.Equal(GitPromptKind.Other, GitPromptClassifier.Classify(prompt, sshPrompt: null).Kind);
    }

    [Theory]
    [InlineData("https://github.com", "github.com")]
    [InlineData("http://alice@127.0.0.1:47391", "127.0.0.1:47391")]
    [InlineData("https://alice:token@example.com/team/repo.git", "example.com")]
    [InlineData("https://a@b@example.com?x=1", "example.com")]
    [InlineData("https://[::1]:8443/x.git", "[::1]:8443")]
    [InlineData("example.com", "example.com")]
    public void HostOf_gives_the_host_and_port_without_the_user(string url, string host)
    {
        Assert.Equal(host, GitPromptClassifier.HostOf(url));
    }

    [Theory]
    [InlineData("https://alice@github.com", "alice")]
    [InlineData("https://alice:token@github.com/x.git", "alice")]
    [InlineData("https://github.com/alice@x", null)]
    [InlineData("https://github.com", null)]
    [InlineData("github.com", null)]
    public void UserOf_gives_the_user_name_of_the_url(string url, string? user)
    {
        Assert.Equal(user, GitPromptClassifier.UserOf(url));
    }

    [Fact]
    public void PasswordPromptAfter_is_the_text_git_asks_after_the_user_name()
    {
        Assert.Equal("Password for 'https://alice@github.com': ", GitPromptClassifier.PasswordPromptAfter("https://github.com", "alice"));
        Assert.Equal("Password for 'http://bob@127.0.0.1:47391/x.git': ", GitPromptClassifier.PasswordPromptAfter("http://127.0.0.1:47391/x.git", "bob"));

        var prompt = GitPromptClassifier.PasswordPromptAfter("https://github.com", "alice");
        Assert.Equal(new GitPromptClassification(GitPromptKind.Password, "https://alice@github.com", "alice"), GitPromptClassifier.Classify(prompt, null));
    }
}
