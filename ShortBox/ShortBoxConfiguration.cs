using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxConfiguration
{
    public static IServiceCollection AddShortBoxServices(this IServiceCollection services) =>
        services.AddTransient<IComicFileNameParser, ComicFileNameParser>()
                .AddTransient<IComicInfoReader, ComicInfoReader>()
                .AddTransient<IRarReader, RarReader>()
                .AddTransient<IZipReader, ZipReader>()
                .AddTransient<IBookFactory, BookFactory>()
                .AddTransient<IComicFolderScanner, ComicFolderScanner>();
}
