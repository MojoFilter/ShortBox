namespace ShortBoxFunctions;

public class CleanUpReadBooks(IBookStore bookStore, ILoggerFactory loggerFactory)
{

    [Function("CleanUpReadBooks")]
    public async Task Run(
        [TimerTrigger("0 30 3 * * *")] TimerInfo myTimer,
        //[HttpTrigger(AuthorizationLevel.Admin, "get", "post")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        await _bookStore.CleanUpReadBooksAsync(cancellationToken).ConfigureAwait(false);
        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
        }
    }

    private readonly ILogger _logger = loggerFactory.CreateLogger<CleanUpReadBooks>();
    private readonly IBookStore _bookStore = bookStore;
}
