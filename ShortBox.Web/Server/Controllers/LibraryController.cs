using Microsoft.AspNetCore.Mvc;
using ShortBox.Api.Data;
using ShortBox.Communication;

namespace ShortBox.Web.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class LibraryController : ControllerBase
{

    public LibraryController(IShortBoxApiClient client)
    {
        _client = client;
    }

    [HttpGet]
    public  Task<IEnumerable<Series>> Get(CancellationToken cancellationToken) =>
        _client.GetAllSeriesAsync(cancellationToken);

    [HttpGet("series/{seriesId}/cover")]
    public async Task<IActionResult> GetSeriesCover(string seriesId, CancellationToken cancellationToken) =>
        File(await _client.GetSeriesCoverAsync(seriesId, cancellationToken), "image/jpeg");

    private readonly IShortBoxApiClient _client;
}
