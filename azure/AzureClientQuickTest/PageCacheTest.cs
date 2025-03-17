using ShortBox.Api.Data;
using ShortBox.Services;

namespace AzureClientQuickTest;

internal class PageCacheTest(IPageCache pageCache)
{
    public async Task RunAsync()
    {
        var id = new BookId(3846);
        var fileName = "Sabretooth - The Dead Don't Talk 003 (2025) (digital) (Marika-Empire).cbz";
        var pageNumber = 2;
        var page = await pageCache.GetPageAsync(id, fileName, pageNumber, default);
        Console.WriteLine($"Page {pageNumber} of {id} retrieved. {page.Length} bytes.");
    }
}
