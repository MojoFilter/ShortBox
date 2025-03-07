namespace ShortBoxFunctions;

public class GetSeriesIssues(
    IBookStore bookStore,
    ILogger<GetSeriesIssues> logger)
{

    [Function("GetSeriesIssues")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route ="series/{seriesName}")] 
        HttpRequest req,
        string seriesName,
        CancellationToken cancellationToken)
    {
        var issues = await _bookStore.GetIssuesAsync(seriesName, cancellationToken).ConfigureAwait(false);
        return new OkObjectResult(issues);
    }

    private readonly ILogger<GetSeriesIssues> _logger = logger;
    private readonly IBookStore _bookStore = bookStore;
}
