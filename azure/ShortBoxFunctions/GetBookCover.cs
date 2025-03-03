using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ShortBox.DataAccess;

namespace ShortBoxFunctions;

public class GetBookCover(
    IBookStore bookStore,
    ILogger<GetBookCover> logger)
{

    [Function("GetBookCover")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Admin, "get", "post", Route = "book/{bookIdValue}/cover")] 
        HttpRequest req,
        int bookIdValue,
        int? height,
        CancellationToken cancellationToken)
    {
        var coverData = await _bookStore.GetBookCoverAsync(new(bookIdValue), height, cancellationToken).ConfigureAwait(false);
        return new FileContentResult(coverData, ImageContentType);
    }

    private readonly ILogger<GetBookCover> _logger = logger;
    private readonly IBookStore _bookStore = bookStore;

    private const string ImageContentType = "image/jpeg";
}
