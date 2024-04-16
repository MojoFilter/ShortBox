using ShortBox.Communication;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxCommunicationConfiguration
{
    public static IServiceCollection AddShortBoxCommunication(this IServiceCollection services, Action<HttpClient>? configureClient = default)
    {
        configureClient = configureClient ?? (client => client.BaseAddress = new Uri("http://api/"));
        services.AddHttpClient<IShortBoxApiClient, ShortBoxApiClient>(configureClient);
        return services
            .AddTransient<IShortBoxApiClientFactory, ShortBoxApiClientFactory>();
    }
}
