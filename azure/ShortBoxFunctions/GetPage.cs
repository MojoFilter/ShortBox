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

    private readonly IBookStore _bookStore = bookStore;
    private readonly ILogger<GetPage> _logger = logger;
}
