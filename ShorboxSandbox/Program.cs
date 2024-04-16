using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShortBox.Services;
using System.Text;

var cbzFile = "X:\\Weapon X-Men 002 (2024) (digital) (Marika-Empire).cbz";
var pageNumber = 0;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var config = new ConfigurationBuilder()
    .Build();

var services = new ServiceCollection()
    .AddShortBoxServices(config)
    .BuildServiceProvider();

var reader = services.GetRequiredService<IComicFileReader>();
var page = await reader.GetBookPageAsync(cbzFile, pageNumber, default);
using var fout = File.OpenWrite("page.jpg");
await page.CopyToAsync(fout);
await fout.FlushAsync();