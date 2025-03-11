namespace ShortBoxFunctions;

public class GetPage(
    IBookStore bookStore,
    ILogger<GetPage> logger)
{
    [Function("GetPage")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "book/{bookId}/{pageNumber}")] HttpRequest req,
        int bookId,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken = CancellationToken.None;
        _logger.LogInformation("Retrieving page {page} of book #{bookId}", pageNumber, bookId);
        var page = await _bookStore.GetBookPageAsync(new(bookId), pageNumber, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Page {page} of book #{bookId} retrieved", pageNumber, bookId);
        return new FileStreamResult(page, "image/jpeg");
    }

    private readonly IBookStore _bookStore = bookStore;
    private readonly ILogger<GetPage> _logger = logger;
}
