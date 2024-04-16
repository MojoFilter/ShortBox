using MarvelApiClient;
using Microsoft.EntityFrameworkCore;
using ShortBox.Api;
using ShortBox.Api.Data;
using ShortBox.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddShortBoxApi(builder.Configuration);

builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope()) {
    var context = scope.ServiceProvider.GetRequiredService<ShortBoxContext>();
    if (context.Database.IsSqlServer()) {
        context.Database.Migrate();
    }
}


app.MapGet("/series", (IBookStore store, CancellationToken ct) => store.GetAllSeriesAsync(ct))
    .WithName("GetAllSeries")
    .WithOpenApi();

app.MapGet("/series/{seriesName}/cover",
    async (string seriesName, IBookStore store, int? height, CancellationToken ct) =>
        Results.File(await store.GetSeriesCoverAsync(seriesName, height, ct), "image/jpeg"))
    .WithName("GetSeriesCover")
    .WithOpenApi();

app.MapGet("/books", (IBookStore store, CancellationToken ct) => store.GetRecentBooksAsync(ct))
   .WithName("GetBooks")
   .WithOpenApi();

app.MapGet("/book/{bookId}", (BookId bookId, IBookStore store, CancellationToken ct) => store.GetBookAsync(bookId, ct))
    .WithName("GetBook")
    .WithOpenApi();

app.MapGet("/book/{bookId}/cover", async (BookId bookId, int? height, IBookStore store, CancellationToken ct) =>
        Results.File(await store.GetBookCoverAsync(bookId, height, ct), "image/jpeg"))
   .WithName("GetBookCover")
   .WithOpenApi();

app.MapPut("/book/{bookId}/mark/{pageNumber}", async (BookId bookId, int pageNumber, IBookStore store, CancellationToken ct) =>
        await store.MarkPageAsync(bookId, pageNumber, ct))
   .WithName("MarkPage")
   .WithOpenApi();

app.MapGet("/book/{bookId}/{pageNumber}", async (BookId bookId, int pageNumber, IBookStore store, CancellationToken ct) =>
        Results.File(await store.GetBookPageAsync(bookId, pageNumber, ct), "image/jpeg"));

app.MapGet("series/{seriesName}", (string seriesName, IBookStore store, CancellationToken ct) =>
        store.GetIssuesAsync(seriesName, ct))
    .WithName("GetIssues")
    .WithOpenApi();

app.MapGet("series/{seriesName}/archive", (string seriesName, IBookStore store, CancellationToken ct) =>
        store.GetSeriesArchiveAsync(seriesName, ct))
    .WithName("GetSeriesArchive")
    .WithOpenApi();

app.MapGet("/scan", (IComicFolderScanner scanner, CancellationToken ct) => scanner.ScanFolderAsync(ct))
   .WithName("Scan")
   .WithOpenApi();

app.MapGet("/pullList/update", UpdatePullListAsync)
    .WithName("UpdatePullList")
    .WithOpenApi();

app.Run();

async Task<IEnumerable<PullListEntry>> UpdatePullListAsync(IMarvelApiClient marvel, IBookStore store, CancellationToken ct)
{
    var recentComics = await marvel.GetReleasesAsync(ReleaseDateDescriptor.thisWeek, ct);
    return await store.UpdatePullListAsync(recentComics, ct);
}