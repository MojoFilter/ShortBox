using Microsoft.Extensions.Configuration;
using ShortBox.DataAccess;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class DataAccessConfiguration
{
    public static IServiceCollection AddShortBoxDataAccess(this IServiceCollection services, IConfiguration configuration) =>
        services.AddDbContextFactory<ShortBoxContext>(options => options.UseSqlServer(configuration.GetConnectionString("ShortBoxContext")))
                .AddTransient<IBookStore, BookStore>()
                .AddTransient<IBookRepo, BookRepo>();
}
