using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortBox.Communication;
using System.Diagnostics;

var services = new ServiceCollection()
    .AddShortBoxAzure(opt =>
    {
        opt.BaseAddress = new("http://localhost:7206");
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
using (var stream = await client.GetBookPageAsync(3864, 0, CancellationToken.None))
using (var file = File.Create("page.jpg"))
{
    sw.Stop();
    log.LogInformation("Page downloaded in {seconds}s. Saving to page.jpg", sw.Elapsed.TotalSeconds);
    await stream.CopyToAsync(file);
}
Process.Start(new ProcessStartInfo("page.jpg") { UseShellExecute = true });
