namespace ShortBoxFunctions;

public class Books(ILogger<Books> logger, IBookStore bookStore)
{
    [Function("Books")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Admin, "get", "post")] HttpRequest req, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching full book list");
        var books = await _bookStore.GetRecentBooksAsync(cancellationToken).ToListAsync();
        return new OkObjectResult(books);
    }

    private readonly ILogger<Books> _logger = logger;
    private readonly IBookStore _bookStore = bookStore;
}
