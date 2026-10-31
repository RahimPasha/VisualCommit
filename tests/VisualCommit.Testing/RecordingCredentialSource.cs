using System.Collections.Concurrent;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;

namespace VisualCommit.Testing;

/// <summary>
/// A credential source for the app's helper (docs/phase-7a-design.md 1.9, 4.8) that answers every
/// <c>get</c> with <see cref="Token"/> and records each <c>get</c> and each refusal. Its text never
/// shows the token.
/// </summary>
public sealed class RecordingCredentialSource(string? token = null) : IGitCredentialSource
{
    private readonly ConcurrentQueue<AccountKey> _gets = new();
    private readonly ConcurrentQueue<AccountKey> _refusals = new();
    private volatile string? _token = token;

    /// <summary>What a <c>get</c> answers; null: no token (signed out, the switch off).</summary>
    public string? Token
    {
        get => _token;
        set => _token = value;
    }

    /// <summary>Every <c>get</c>'s account, in order.</summary>
    public IReadOnlyList<AccountKey> Gets => [.. _gets];

    /// <summary>Every <see cref="Refused"/>'s account, in order.</summary>
    public IReadOnlyList<AccountKey> Refusals => [.. _refusals];

    public Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        _gets.Enqueue(account);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_token is { } value ? new GitCredential(value) : null);
    }

    public void Refused(AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(account);
        _refusals.Enqueue(account);
    }

    public override string ToString() => $"RecordingCredentialSource({_gets.Count} gets, {_refusals.Count} refusals)";
}
