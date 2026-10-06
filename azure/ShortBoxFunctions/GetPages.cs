using ShortBox.Services;

namespace ShortBoxFunctions;

public class GetPages(IBookStore bookStore, ILogger<GetPages> logger)
{
    /// <summary>
    /// The pages of a book in reading order, each with its content type and pixel size, so a reader can lay pages
    /// out before they load. Never extracts: a book that is not ready answers with the retryable <c>202</c> that
    /// <c>GET book/{id}/{page}?wait=false</c> uses, so call <c>POST book/{id}/prepare</c> first.
    /// </summary>
    [Function("GetPages")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "book/{bookId:int}/pages")] HttpRequest req,
        int bookId,
        CancellationToken cancellationToken)
    {
        try
        {
            var pages = await _bookStore.GetPagesAsync(new(bookId), cancellationToken).ConfigureAwait(false);
            if (pages.Status.State != PageState.Ready)
            {
                _logger.LogInformation("Book #{bookId} is not ready ({state}), so it has no page list yet", bookId, pages.Status.State);
                return PageStatusResponse.NotReady(req, pages.Status);
            }
            return new OkObjectResult(PageListResponse.From(pages));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Book #{bookId} not found", bookId);
            return new NotFoundResult();
        }
    }

    private readonly IBookStore _bookStore = bookStore;
    private readonly ILogger<GetPages> _logger = logger;
}
