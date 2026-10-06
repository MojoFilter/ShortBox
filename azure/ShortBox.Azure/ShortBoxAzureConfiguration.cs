using Microsoft.Extensions.Options;
using ShortBox.Azure;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxAzureConfiguration
{
    public static IServiceCollection AddShortBoxAzure(this IServiceCollection services, Action<ShortBoxAzureOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IShortBoxReaderClient, ShortBoxAzureClient>()
                .AddSingleton<IShortBoxReleasesClient, ShortBoxAzureClient>()
                .AddSingleton<IShortBoxReaderClientFactory, ShortBoxAzureClientFactory>()
                .AddHttpClient<ShortBoxAzureClient>((p, client) =>
                {
                    var options = p.GetRequiredService<IOptions<ShortBoxAzureOptions>>().Value;
                    client.BaseAddress = options.BaseAddress;
                    client.Timeout = TimeSpan.FromHours(2.0);
                    client.DefaultRequestHeaders.Add("x-functions-key", options.HostKey);
                });
        return services;
    }

    /// <summary>Adds the reader's page provider. Needs <see cref="AddShortBoxAzure"/> for the HTTP client it uses.</summary>
    public static IServiceCollection AddShortBoxPageProvider(this IServiceCollection services, Action<PageProviderOptions> configure)
    {
        var options = new PageProviderOptions { CacheDirectory = string.Empty };
        configure(options);
        // The prefetcher holds the window for one book, so each reader gets its own.
        return services.AddSingleton<IPageProvider>(p => new PageProvider(p.GetRequiredService<IHttpClientFactory>(), options))
                       .AddTransient(p => new PagePrefetcher(p.GetRequiredService<IPageProvider>(), options.PrefetchAhead, options.PrefetchBehind));
    }
}
