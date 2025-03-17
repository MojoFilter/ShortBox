using Azure.Identity;
using AzureClientQuickTest;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortBox.Communication;
using System.Diagnostics;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();


var serviceBuilder = new ServiceCollection();
serviceBuilder.AddAzureClients(builder =>
{
    builder.AddBlobServiceClient(config["BlobStorage"] ?? throw new InvalidOperationException("Missing BlobStorage"));

    var credential = new DefaultAzureCredential();
    builder.UseCredential(credential);
});
var services = serviceBuilder
    .AddShortBoxAzure(opt =>
    {
        opt.BaseAddress = new(config["FunctionsBaseUrl"] ?? "http://localhost:7206");
        opt.HostKey = config["FunctionsKey"] ?? throw new InvalidOperationException("Missing FunctionsKey");
    })
    .AddLogging(cfg =>
    {
        cfg.AddConsole();
        cfg.SetMinimumLevel(LogLevel.Information);
    })
    .AddShortBoxGoogle(opt =>
    {
        opt.CoversFolderId = config["CoversFolderId"] ?? throw new InvalidOperationException("Missing CoversFolderId");
        opt.ArchivesFolderId = config["ArchivesFolderId"] ?? throw new InvalidOperationException("Missing ArchivesFolderId");
        opt.CredentialsUser = config["CredentialsUser"] ?? throw new InvalidOperationException("Missing CredentialsUser");
        opt.CredentialsJson = config["CredentialsJson"] ?? throw new InvalidOperationException("Missing CredentialsJson");
    })
    .AddShortBoxServices()
    .AddShortBoxAzureServices()
    .AddTransient<PageCacheTest>()
    .BuildServiceProvider();

var test = services.GetRequiredService<PageCacheTest>();
await test.RunAsync();

//await DownloadPageAsync(services);

static async Task DownloadPageAsync(ServiceProvider services)
{
    var log = services.GetRequiredService<ILogger<Program>>();
    var client = services.GetRequiredService<IShortBoxReaderClient>();

    log.LogInformation("Fetching page");
    var sw = Stopwatch.StartNew();
    using (var stream = await client.GetBookCoverAsync(3863, 250, CancellationToken.None))
    using (var file = File.Create("page.jpg"))
    {
        sw.Stop();
        log.LogInformation("Page downloaded in {seconds}s. Saving to page.jpg", sw.Elapsed.TotalSeconds);
        await stream.CopyToAsync(file);
    }
    Process.Start(new ProcessStartInfo("page.jpg") { UseShellExecute = true });
}
