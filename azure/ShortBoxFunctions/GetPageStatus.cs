namespace ShortBoxFunctions;

public class GetPageStatus(IBookStore bookStore, ILogger<GetPageStatus> logger)
{
    /// <summary>Whether a book's pages are ready to serve: "pending", "ready" (with the page count) or "failed" (with the reason).</summary>
    [Function("GetPageStatus")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "book/{bookId:int}/status")] HttpRequest req,
        int bookId,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _bookStore.GetPageStatusAsync(new(bookId), cancellationToken).ConfigureAwait(false);
            return new OkObjectResult(PageStatusResponse.From(status));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Book #{bookId} not found", bookId);
            return new NotFoundResult();
        }
    }

    private readonly IBookStore _bookStore = bookStore;
    private readonly ILogger<GetPageStatus> _logger = logger;
}
