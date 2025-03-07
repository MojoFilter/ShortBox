namespace ShortBoxFunctions;

public class BookInfo(IBookStore bookStore, ILogger<BookInfo> logger)
{
    [Function("BookInfo")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "get", "post", Route="book/{bookIdValue:int}")]
        HttpRequest req,
        int bookIdValue,
        CancellationToken cancellationToken)
    {
        try
        {
            var book = await _bookStore.GetBookAsync(new(bookIdValue), cancellationToken);
            return new OkObjectResult(book);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Book not found");
            return new NotFoundResult();
        }
    }

    private readonly ILogger<BookInfo> _logger = logger;
    private readonly IBookStore _bookStore = bookStore;
}