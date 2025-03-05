namespace ShortBoxFunctions;

public class GetAllSeries(IBookStore bookStore)
{

    [Function("GetAllSeries")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "get", "post", Route = "series")]
        HttpRequest req,
        CancellationToken cancellationToken)
    {
        var seriesList = await _bookStore.GetAllSeriesAsync(cancellationToken).ConfigureAwait(false);
        return new OkObjectResult(seriesList);
    }

    private readonly IBookStore _bookStore = bookStore;
}
