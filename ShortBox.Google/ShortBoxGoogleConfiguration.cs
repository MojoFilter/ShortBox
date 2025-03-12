using ShortBox.Google;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxGoogleConfigurationExtensions
{
    public static IServiceCollection AddShortBoxGoogle(this IServiceCollection services, Action<GoogleOptions> googleConfig) =>
        services.AddTransient<IBookCoverFileBusiness, BookCoverFileBusiness>()
                .AddTransient<IDriveBusiness, DriveBusiness>()
                .AddTransient<IArchiveStore, ArchiveStore>()
                .AddTransient<ICoverStore, CoverStore>()
                .AddTransient<IDriveServiceFactory, DriveServiceFactory>()
                .Configure(googleConfig);
}
