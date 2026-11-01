namespace VisualCommit.Hosting.Http;

/// <summary>
/// One answer as the shared HTTP core read it, before anything parsed it (docs/phase-7b-design.md
/// 1.4, 5.2): what a profile classifies, refuses or pages by. A redirect's exchange carries no body.
/// </summary>
/// <param name="Method">The request's method.</param>
/// <param name="Url">The address that answered (after the redirects followed), with its query.</param>
/// <param name="Status">The answer's status code; 0 when none came.</param>
/// <param name="Body">The body, at most 10 MB, a leading UTF-8 byte order mark removed; for a 304 to a kept answer, the kept body.</param>
/// <param name="Headers">The answer's headers and its content's, without case, a header given twice joined with <c>", "</c>; for a 304 to a kept answer, the kept ones under the 304's own.</param>
internal sealed record HostingExchange(HttpMethod Method, Uri Url, int Status, byte[] Body, IReadOnlyDictionary<string, string> Headers)
{
    /// <summary>The answer's reason phrase, when it gave one.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// One result of <see cref="HostingHttpClient"/>: its value, or why it failed (docs/phase-7b-design.md 1.4, 5.2).
/// </summary>
/// <param name="Value">The parsed body; null on a failure and on a status the profile takes as success without a value.</param>
/// <param name="Failure">Why the request failed; null when it did not.</param>
/// <param name="Status">The last answer's status code (304 when the kept body was used); 0 when none came.</param>
/// <param name="Next">For pages: the next page left when the cap was reached; otherwise null.</param>
/// <param name="Headers">The last answer's headers, as <see cref="HostingExchange.Headers"/>; empty when none came.</param>
/// <param name="RequestId">The last answer's request id, as the profile's <see cref="HostingHttpProfile.RequestIdOf"/> reads it.</param>
internal sealed record HostingReply<T>(T? Value, HostingFailure? Failure, int Status, Uri? Next, IReadOnlyDictionary<string, string> Headers, string? RequestId)
{
    public bool Succeeded => Failure is null;
}
