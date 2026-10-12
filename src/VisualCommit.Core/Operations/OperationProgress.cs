namespace VisualCommit.Core.Operations;

/// <summary>A progress line of git (phase 3 design 1.9), as <c>GitProgressParser</c> reads it.</summary>
/// <param name="Stage">What git is doing: <c>Receiving objects</c>, <c>Updating files</c>.</param>
/// <param name="Percent">How far that stage is, 0 to 100, or null when git gives no percentage.</param>
/// <param name="Text">The whole line, without a leading <c>remote: </c>.</param>
public sealed record OperationProgress(string Stage, int? Percent, string Text);
