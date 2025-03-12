using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortBox.Communication;
using System.Diagnostics;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var services = new ServiceCollection()
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
    .BuildServiceProvider();

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
