using Microsoft.Extensions.Configuration;
using ShortBox.Google;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxGoogleConfigurationExtensions
{
    public static IServiceCollection AddShortBoxGoogle(this IServiceCollection services, Action<GoogleOptions> googleConfig) =>
        services.AddTransient<IBookCoverFileBusiness, BookCoverFileBusiness>()
                .Configure(googleConfig);
}
