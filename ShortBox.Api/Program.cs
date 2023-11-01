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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
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
    async (string seriesName, IBookStore store, CancellationToken ct) =>
        Results.File(await store.GetSeriesCoverAsync(seriesName, ct), "image/jpeg"))
    .WithName("GetSeriesCover")
    .WithOpenApi();

app.MapGet("/books", (ShortBoxContext context) => context.Books.ToListAsync())
   .WithName("GetBooks")
   .WithOpenApi();

app.MapGet("/scan", (IComicFolderScanner scanner, CancellationToken ct) => scanner.ScanFolderAsync(ct))
   .WithName("Scan")
   .WithOpenApi();

app.Run();