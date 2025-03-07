namespace ShortBoxFunctions;

public class GetSeriesArchive(
    IBookStore bookStore,
    ILogger<GetSeriesArchive> logger)
{

    [Function("GetSeriesArchive")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "get", "post", Route = "series/{seriesName}/archive")] 
        HttpRequest req,
        string seriesName,
        CancellationToken cancellationToken)
    {
        var archive = await _bookStore.GetSeriesArchiveAsync(seriesName, cancellationToken).ConfigureAwait(false);
        return new OkObjectResult(archive);
    }

    private readonly ILogger<GetSeriesArchive> _logger = logger;
    private readonly IBookStore _bookStore = bookStore;
}
