using Microsoft.EntityFrameworkCore;
using ShortBox.Api.Data;
using ShortBox.Api;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxApiConfiguration
{
    public static IServiceCollection AddShortBoxApi(this IServiceCollection services, IConfiguration configuration) =>
        services.AddDbContext<ShortBoxContext>(options => options.UseSqlServer(configuration.GetConnectionString("ShortBoxContext")))
                .AddTransient<IBookStore, FolderBookStore>()
                .AddTransient<IFolderBookStore, FolderBookStore>()
                .AddShortBoxServices(configuration.GetSection("MarvelApi"))
                .Configure<FolderBookStoreOptions>(configuration.GetSection("store"));
}
