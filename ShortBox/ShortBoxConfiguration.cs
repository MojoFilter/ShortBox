using Microsoft.Extensions.Configuration;
using ShortBox;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxConfiguration
{
    public static IServiceCollection AddShortBoxClient(this IServiceCollection services) =>
        services.AddSingleton<IShortBoxClientSettings, ShortBoxClientSettings>();

    public static IServiceCollection AddShortBoxServices(this IServiceCollection services, IConfiguration marvelApiConfiguration) =>
        services.AddTransient<IComicFileNameParser, ComicFileNameParser>()
                .AddTransient<IComicInfoReader, ComicInfoReader>()
                .AddTransient<IRarReader, RarReader>()
                .AddTransient<IZipReader, ZipReader>()
                .AddTransient<IBookFactory, BookFactory>()
                .AddTransient<IComicFolderScanner, ComicFolderScanner>()
                .AddTransient<IComicFileReader, ComicFileReader>()
                .AddTransient<IArchiveReaderFactory, ArchiveReaderFactory>()
                .AddTransient<IImageBusiness, ImageBusiness>()
                .AddMarvelApi(marvelApiConfiguration);
}
