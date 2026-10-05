using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using ShortBox.Metron;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxMetronConfigurationExtensions
{
    public static IServiceCollection AddShortBoxMetron(this IServiceCollection services, Action<MetronOptions> metronConfig)
    {
        services.Configure(metronConfig);
        services.AddHttpClient<IReleaseSource, MetronReleaseSource>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MetronOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ShortBox/1.0");
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        });
        return services;
    }
}
