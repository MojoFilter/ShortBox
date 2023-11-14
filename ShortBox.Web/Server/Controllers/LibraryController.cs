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
    public async Task<IActionResult> GetSeriesCover(string seriesId, int? height, CancellationToken cancellationToken) =>
        File(await _client.GetSeriesCoverAsync(seriesId, height, cancellationToken), "image/jpeg");

    [HttpGet("book/{bookId}/cover")]
    public async Task<IActionResult> GetBookCover(int bookId, int? height, CancellationToken cancellationToken) =>
        File(await _client.GetBookCoverAsync(bookId, height, cancellationToken), "image/jpeg");

    [HttpGet("book/{bookId}/{pageNumber}")]
    public async Task<IActionResult> GetBookPage(int bookId, int pageNumber, CancellationToken cancellationToken) =>
        File(await _client.GetBookPageAsync(bookId, pageNumber, cancellationToken), "image/jpeg");

    [HttpPut("book/{bookId}/mark/{pageNumber}")]
    public async Task<IActionResult> MarkBookPage(int bookId, int pageNumber, CancellationToken cancellationToken)
    {
        await _client.MarkPageAsync(bookId, pageNumber, cancellationToken);
        return Ok();
    }

    [HttpGet("{seriesName}")]
    public Task<IEnumerable<Book>> GetIssues(string seriesName, CancellationToken cancellationToken) => _client.GetIssuesAsync(seriesName, cancellationToken);

    private readonly IShortBoxApiClient _client;
}
