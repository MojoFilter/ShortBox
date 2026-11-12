using ShortBox.Services;

namespace ShortBoxFunctions;

public class ScanLibrary(ILibraryIngestor ingestor, ILogger<ScanLibrary> logger)
{
    [Function("ScanLibraryTimer")]
    public async Task RunOnSchedule(
        [TimerTrigger("0 */15 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        await this.ScanAsync(cancellationToken).ConfigureAwait(false);
    }

    [Function("ScanLibrary")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "library/scan")]
        HttpRequest req,
        CancellationToken cancellationToken) =>
        new OkObjectResult(await this.ScanAsync(cancellationToken).ConfigureAwait(false));

    private async Task<IngestResult> ScanAsync(CancellationToken cancellationToken)
    {
        var result = await _ingestor.IngestAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Library scan added {added}, failed {failed}, matched {linked} releases, {remaining} left for the next run",
            result.Added.Count, result.Failed.Count, result.Linked, result.Remaining);
        return result;
    }

    private readonly ILibraryIngestor _ingestor = ingestor;
    private readonly ILogger<ScanLibrary> _logger = logger;
}
