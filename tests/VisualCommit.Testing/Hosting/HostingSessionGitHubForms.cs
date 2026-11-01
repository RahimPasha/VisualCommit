using VisualCommit.Core.Hosting;

// In the session's namespace, not the folder's (docs/phase-7b-design.md 1.9): no caller gains a using.
namespace VisualCommit.Hosting;

/// <summary>
/// 7a's GitHub-named forms of the session, for tests alone (docs/phase-7b-design.md 1.9, 9.2): C# 14
/// extension members on <see cref="IHostingSession"/>, each delegating with
/// <see cref="HostingProvider.GitHub"/>. <see cref="HostingSession"/> declares none of them, so 7a's
/// tests, which call them on a <see cref="HostingSession"/>, compile through these; a call on an
/// <see cref="IHostingSession"/>-typed receiver takes the interface's default body until WP-N
/// removes it. Only tests see them: src/ references no test project.
/// </summary>
public static class HostingSessionGitHubForms
{
    extension(IHostingSession session)
    {
        /// <summary>GitHub's provider; null while off.</summary>
        public IHostingProvider? GitHub => session.ProviderOf(HostingProvider.GitHub);

        /// <summary>
        /// GitHub's account, signed in or needing sign-in; null without one. Declared without
        /// <c>?</c> for the flow analysis alone: an extension property's null state is not kept
        /// between two reads, which 7a's <c>Account!.State, Account.NoteKind</c> rests on.
        /// </summary>
        public AccountStatus Account => session.AccountOf(HostingProvider.GitHub)!;

        /// <summary>A1 on GitHub with <paramref name="token"/>.</summary>
        public Task<SignInResult> SignInAsync(string token, CancellationToken ct) =>
            session.SignInAsync(new SignInRequest(HostingProvider.GitHub, token), ct);

        /// <summary>Signs GitHub's account out and deletes <c>avatars/</c> (D291).</summary>
        public Task<string?> SignOutAsync(CancellationToken ct) => session.SignOutAsync(HostingProvider.GitHub, ct);

        /// <summary>GitHub's "Use for git over HTTPS".</summary>
        public void SetUseForGit(bool useForGit) => session.SetUseForGit(HostingProvider.GitHub, useForGit);

        /// <summary>A2 with GitHub's account's token.</summary>
        public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct) =>
            session.ListRepositoriesAsync(HostingProvider.GitHub, ct);
    }
}
