using ShortBox.Services;

namespace ShortBoxFunctions;

public class GetPage(
    IBookStore bookStore,
    ILogger<GetPage> logger)
{
    [Function("GetPage")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "book/{bookId:int}/{pageNumber:int}")] HttpRequest req,
        int bookId,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving page {page} of book #{bookId}", pageNumber, bookId);
        try
        {
            // Opt-in so clients that predate prepare/status keep their blocking behaviour: they would
            // otherwise take the empty 202 for a broken image.
            if (string.Equals(req.Query["wait"], "false", StringComparison.OrdinalIgnoreCase)
                && await _bookStore.GetPageStatusAsync(new(bookId), cancellationToken).ConfigureAwait(false) is { State: not PageState.Ready } status)
            {
                _logger.LogInformation("Book #{bookId} is not ready ({state}). Not waiting for page {page}.", bookId, status.State, pageNumber);
                req.HttpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString();
                return new ObjectResult(PageStatusResponse.From(status)) { StatusCode = StatusCodes.Status202Accepted };
            }

            var page = await _bookStore.GetBookPageAsync(new(bookId), pageNumber, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Page {page} of book #{bookId} retrieved", pageNumber, bookId);
            return new FileStreamResult(page.Content, page.ContentType);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Page {page} of book #{bookId} not found", pageNumber, bookId);
            return new NotFoundResult();
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "Book #{bookId} has no readable pages", bookId);
            return new UnprocessableEntityObjectResult(ex.Message);
        }
    }

    private const int RetryAfterSeconds = 2;

    private readonly IBookStore _bookStore = bookStore;
    private readonly ILogger<GetPage> _logger = logger;
}
