using System.Net;

namespace VisualCommit.Testing.Hosting;

/// <summary>
/// The test harness's network tripwire (docs/phase-7a-design.md 2.5): a request whose host parses
/// as a loopback IP address passes to the inner handler; any other (<c>localhost</c> included, which
/// a resolver may send elsewhere) is recorded in <see cref="RefusedRequests"/> and fails with an
/// <see cref="HttpRequestException"/>, so that no test reaches github.com or any other host.
/// </summary>
public sealed class LoopbackOnlyHandler : DelegatingHandler
{
    /// <summary>The message of the refusal.</summary>
    public const string RefusalMessage = "The test harness lets only loopback requests through.";

    private readonly RefusedRequests _refused;

    /// <param name="inner">The handler of the requests that pass.</param>
    /// <param name="refused">Where refused requests are recorded; shared by every handler of one app session.</param>
    public LoopbackOnlyHandler(HttpMessageHandler inner, RefusedRequests refused)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(refused);
        _refused = refused;
    }

    /// <summary>The requests this handler refused, as their recorder keeps them.</summary>
    public RefusedRequests RefusedRequests => _refused;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestUri is { IsAbsoluteUri: true } uri && IPAddress.TryParse(uri.IdnHost, out var address) && IPAddress.IsLoopback(address))
        {
            return base.SendAsync(request, cancellationToken);
        }

        _refused.Add(request.RequestUri);
        return Task.FromException<HttpResponseMessage>(new HttpRequestException(RefusalMessage));
    }
}

/// <summary>The addresses a <see cref="LoopbackOnlyHandler"/> refused, in order; safe from any thread. Never holds a request's headers.</summary>
public sealed class RefusedRequests
{
    private readonly Lock _gate = new();
    private readonly List<Uri?> _addresses = [];

    /// <summary>The refused addresses, in the order they were refused; null for a request without one.</summary>
    public IReadOnlyList<Uri?> Addresses
    {
        get
        {
            lock (_gate)
            {
                return [.. _addresses];
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _addresses.Count;
            }
        }
    }

    internal void Add(Uri? address)
    {
        lock (_gate)
        {
            _addresses.Add(address);
        }
    }
}
