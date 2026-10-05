using ShortBox;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxConfiguration
{
    public static IServiceCollection AddShortBoxClient(this IServiceCollection services) =>
        services.AddSingleton<IShortBoxClientSettings, ShortBoxClientSettings>();

    /// <summary>The scanner for a local comics folder. Needs an <see cref="IFolderBookStore"/> registered by the host, so only the legacy Api uses it.</summary>
    public static IServiceCollection AddShortBoxFolderScanner(this IServiceCollection services) =>
        services.AddTransient<IComicFolderScanner, ComicFolderScanner>();

    /// <summary>Drive ingest and the weekly releases checklist. Needs <see cref="IArchiveLibrary"/>, <see cref="IBookCatalog"/>, <see cref="IPullListStore"/> and <see cref="IReleaseSource"/> registered by the host.</summary>
    public static IServiceCollection AddShortBoxAcquisition(this IServiceCollection services) =>
        services.AddTransient<ILibraryIngestor, LibraryIngestor>()
                .AddTransient<IReleaseChecklist, ReleaseChecklist>();

    public static IServiceCollection AddShortBoxServices(this IServiceCollection services) => //, IConfiguration marvelApiConfiguration) =>
        services.AddTransient<IComicFileNameParser, ComicFileNameParser>()
                .AddTransient<IComicInfoReader, ComicInfoReader>()
                .AddTransient<IRarReader, RarReader>()
                .AddTransient<IZipReader, ZipReader>()
                .AddTransient<IBookFactory, BookFactory>()
                .AddTransient<IComicFileReader, ComicFileReader>()
                .AddTransient<IArchiveReaderFactory, ArchiveReaderFactory>()
                .AddTransient<IImageBusiness, ImageBusiness>()
                .AddTransient<IArchiveBusiness, ArchiveBusiness>()
                .AddTransient<ICoverBusiness, CoverBusiness>()
                .AddKeyedTransient<IArchiveExtractor, ZipExtractor>(ServiceKeys.ZipExtractor)
                .AddKeyedTransient<IArchiveExtractor, RarExtractor>(ServiceKeys.RarExtractor);
                //.AddMarvelApi(marvelApiConfiguration);
}
