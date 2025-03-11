using Microsoft.Extensions.Options;
using ShortBox.Azure;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxAzureConfiguration
{
    public static IServiceCollection AddShortBoxAzure(this IServiceCollection services, Action<ShortBoxAzureOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IShortBoxReaderClient, ShortBoxAzureClient>()
                .AddHttpClient<ShortBoxAzureClient>((p, client) =>
                {
                    var options = p.GetRequiredService<IOptions<ShortBoxAzureOptions>>().Value;
                    client.BaseAddress = options.BaseAddress;
                    client.Timeout = TimeSpan.FromHours(2.0);
                });
        return services;
    }
}
