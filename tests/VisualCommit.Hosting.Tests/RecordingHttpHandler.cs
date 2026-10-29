using System.Net;
using System.Text;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A handler that records every request as it is sent (its method, address, headers and body),
/// then passes it to the handler inside, or answers it from a script when there is none: for the
/// client's and the cache's tests, which look at what left the app and where it went.
/// </summary>
internal sealed class RecordingHttpHandler : DelegatingHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _script;
    private readonly Action<HttpRequestMessage>? _edit;
    private readonly List<SentRequest> _sent = [];

    /// <summary>Passes every request to <paramref name="inner"/>, after <paramref name="edit"/> when given (which runs after the request was recorded).</summary>
    public RecordingHttpHandler(HttpMessageHandler inner, Action<HttpRequestMessage>? edit = null)
        : base(inner) => _edit = edit;

    /// <summary>Answers every request with what <paramref name="script"/> gives.</summary>
    public RecordingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> script) => _script = script;

    public IReadOnlyList<SentRequest> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    /// <summary>An answer of <paramref name="status"/> with a JSON body and the headers given.</summary>
    public static HttpResponseMessage Json(HttpStatusCode status, string json, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
            {
                response.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in request.Headers.NonValidated)
        {
            headers[name] = string.Join(", ", values);
        }

        string? body = null;
        if (request.Content is not null)
        {
            foreach (var (name, values) in request.Content.Headers.NonValidated)
            {
                headers[name] = string.Join(", ", values);
            }

            body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_sent)
        {
            _sent.Add(new SentRequest(request.Method.Method, request.RequestUri!, headers, body));
        }

        if (_script is not null)
        {
            var response = _script(request);
            response.RequestMessage = request;
            return response;
        }

        _edit?.Invoke(request);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One request as it was sent.</summary>
    public sealed record SentRequest(string Method, Uri Url, IReadOnlyDictionary<string, string> Headers, string? Body)
    {
        public string? Header(string name) => Headers.GetValueOrDefault(name);
    }
}
