using ShortBox.Services;

namespace ShortBoxFunctions;

/// <param name="Status">"pending", "ready" or "failed".</param>
public sealed record PageStatusResponse(string Status, int? PageCount, string? Error)
{
    public static PageStatusResponse From(BookPageStatus status) =>
        new(status.State.ToString().ToLowerInvariant(), status.PageCount, status.Error);

    /// <summary>
    /// The answer for a book whose pages are not ready: a retryable <c>202</c> carrying the status,
    /// so a client can tell "wait" from "broken" without a second request.
    /// </summary>
    public static IActionResult NotReady(HttpRequest req, BookPageStatus status)
    {
        req.HttpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString();
        return new ObjectResult(From(status)) { StatusCode = StatusCodes.Status202Accepted };
    }

    private const int RetryAfterSeconds = 2;
}

/// <summary>A ready book's pages, in reading order.</summary>
public sealed record PageListResponse(string Status, int PageCount, IReadOnlyList<PageInfo> Pages)
{
    public static PageListResponse From(BookPages pages) =>
        new(PageStatusResponse.From(pages.Status).Status, pages.Pages.Count, pages.Pages);
}
