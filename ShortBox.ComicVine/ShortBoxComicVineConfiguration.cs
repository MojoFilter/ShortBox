using Microsoft.Extensions.Options;
using ShortBox.ComicVine;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxComicVineConfigurationExtensions
{
    public static IServiceCollection AddShortBoxComicVine(this IServiceCollection services, Action<ComicVineOptions> comicVineConfig)
    {
        services.Configure(comicVineConfig);
        services.AddHttpClient<IReleaseSource, ComicVineReleaseSource>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ComicVineOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseAddress);
                // Comic Vine rejects the default .NET user agent.
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ShortBox/1.0");
            })
            .RemoveAllLoggers(); // request URLs carry the api_key
        return services;
    }
}
