namespace ShortBoxFunctions;

public class MarkPage(IBookStore bookStore, ILogger<MarkPage> logger)
{

    [Function("MarkPage")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "put", Route ="/book/{bookId:int}/mark/{pageNumber:int}")] 
        HttpRequest req,
        int bookId,
        int pageNumber)
    {
        _logger.LogInformation("Marking book {bookId} at page {pageNumber}", bookId, pageNumber);
        await _bookStore.MarkPageAsync(new(bookId), pageNumber, CancellationToken.None).ConfigureAwait(false);
        return new OkResult();
    }

    private readonly ILogger<MarkPage> _logger = logger;
    private readonly IBookStore _bookStore = bookStore;
}
