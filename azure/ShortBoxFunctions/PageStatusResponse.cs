using ShortBox.Services;

namespace ShortBoxFunctions;

/// <param name="Status">"pending", "ready" or "failed".</param>
public sealed record PageStatusResponse(string Status, int? PageCount, string? Error)
{
    public static PageStatusResponse From(BookPageStatus status) =>
        new(status.State.ToString().ToLowerInvariant(), status.PageCount, status.Error);
}
