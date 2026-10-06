namespace ShortBoxFunctions;

public class MarkRead(IBookStore bookStore, ILogger<MarkRead> logger)
{

    [Function("MarkRead")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "put", Route = "book/{bookId:int}/read/{read:bool}")]
        HttpRequest req,
        int bookId,
        bool read,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking book {bookId} as {state}", bookId, read ? "read" : "unread");
        try
        {
            await _bookStore.MarkReadAsync(new(bookId), read, cancellationToken).ConfigureAwait(false);
            return new OkResult();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Book not found");
            return new NotFoundResult();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Book {bookId} cannot be marked read", bookId);
            return new ConflictObjectResult(ex.Message);
        }
    }

    private readonly ILogger<MarkRead> _logger = logger;
    private readonly IBookStore _bookStore = bookStore;
}
