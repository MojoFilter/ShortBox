using Microsoft.EntityFrameworkCore;
using ShortBox.Api.Data;
using ShortBox.Api;
using ShortBox.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShortBoxApiConfiguration
{
    public static IServiceCollection AddShortBoxApi(this IServiceCollection services, IConfiguration configuration) =>
        services.AddDbContext<ShortBoxContext>(options => options.UseSqlServer(configuration.GetConnectionString("ShortBoxContext")))
                //.AddTransient<FolderBookStore>()
                //.AddTransient<IBookStore>(p => p.GetRequiredService<FolderBookStore>())
                //.AddTransient<IFolderBookStore>(p => p.GetRequiredService<FolderBookStore>())
                .AddTransient<IBookStore, FolderBookStore>()
                .AddTransient<IFolderBookStore, FolderBookStore>()
                .AddShortBoxServices()
                .Configure<FolderBookStoreOptions>(o => o.Path = "/usr/store");
}
