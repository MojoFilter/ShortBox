using ShortBox.Services;

namespace ShortBoxFunctions;

public record PrepareBookMessage(int BookId);

public class PrepareBookResult
{
    [QueueOutput(PrepareBook.QueueName)]
    public PrepareBookMessage? Message { get; init; }

    [HttpResult]
    public required IActionResult Response { get; init; }
}

public class PrepareBook(IBookStore bookStore, ILogger<PrepareBook> logger)
{
    public const string QueueName = "book-prepare";

    /// <summary>
    /// Starts extracting a book's pages in the background. Answers 200 when they are already ready and 202 once
    /// the extraction is queued; <c>GET book/{id}/status</c> says when it is done.
    /// </summary>
    [Function("PrepareBook")]
    public async Task<PrepareBookResult> Request(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "book/{bookId:int}/prepare")] HttpRequest req,
        int bookId,
        CancellationToken cancellationToken)
    {
        BookPageStatus status;
        try
        {
            status = await _bookStore.RequestPrepareAsync(new(bookId), cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Book #{bookId} not found", bookId);
            return new() { Response = new NotFoundResult() };
        }

        if (status.State == PageState.Ready)
        {
            return new() { Response = new OkObjectResult(PageStatusResponse.From(status)) };
        }

        _logger.LogInformation("Queueing the extraction of book #{bookId}", bookId);
        return new()
        {
            Message = new(bookId),
            Response = new AcceptedResult($"/api/book/{bookId}/status", PageStatusResponse.From(status)),
        };
    }

    [Function("PrepareBookWorker")]
    public async Task Work(
        [QueueTrigger(QueueName)] PrepareBookMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Extracting book #{bookId}", message.BookId);
        try
        {
            await _bookStore.PrepareBookAsync(new(message.BookId), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidDataException)
        {
            // Retrying cannot help: the book is gone, or its archive has no pages. The failure is recorded for status checks.
            _logger.LogError(ex, "Book #{bookId} cannot be prepared", message.BookId);
        }
    }

    private readonly IBookStore _bookStore = bookStore;
    private readonly ILogger<PrepareBook> _logger = logger;
}
