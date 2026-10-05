using ShortBox.Services;

namespace ShortBoxFunctions;

public class Releases(IReleaseChecklist checklist, IPullListStore pullList)
{
    [Function("GetReleases")]
    public async Task<IActionResult> GetWeek(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "releases")]
        HttpRequest req,
        CancellationToken cancellationToken)
    {
        var date = DateOnly.TryParse(req.Query["week"], out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.Today);
        var refresh = bool.TryParse(req.Query["refresh"], out var r) && r;
        var entries = await _checklist.GetWeekAsync(date, refresh, cancellationToken).ConfigureAwait(false);
        return new OkObjectResult(entries);
    }

    [Function("GetWantedReleases")]
    public async Task<IActionResult> GetWanted(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "wanted")]
        HttpRequest req,
        CancellationToken cancellationToken) =>
        new OkObjectResult(await _pullList.GetOutstandingAsync(cancellationToken).ConfigureAwait(false));

    [Function("SetReleaseWanted")]
    public async Task<IActionResult> SetWanted(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "releases/{entryId:int}/wanted/{wanted:bool}")]
        HttpRequest req,
        int entryId,
        bool wanted,
        CancellationToken cancellationToken)
    {
        try
        {
            await _pullList.SetWantedAsync(entryId, wanted, cancellationToken).ConfigureAwait(false);
            return new OkResult();
        }
        catch (KeyNotFoundException)
        {
            return new NotFoundResult();
        }
    }

    private readonly IReleaseChecklist _checklist = checklist;
    private readonly IPullListStore _pullList = pullList;
}
