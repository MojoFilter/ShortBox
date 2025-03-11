using ShortBox.Azure.Services;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;


public static class AzureServiceConfiguration
{
    public static IServiceCollection AddShortBoxAzureServices(this IServiceCollection services) =>
            services.AddTransient<IPageCache, AzureStoragePageCache>();
}