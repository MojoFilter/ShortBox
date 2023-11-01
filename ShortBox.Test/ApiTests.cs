using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShortBox.Services;

namespace ShortBox.Test;

[TestClass]
public class ApiTests
{
    [TestMethod]
    public void ApiServiceConfigurationValidates()
    {
        var config = new ConfigurationBuilder().Build();
        var builder = Host.CreateDefaultBuilder()
                          .UseDefaultServiceProvider(opt => opt.ValidateOnBuild = true);
        builder.ConfigureServices(services => services.AddShortBoxApi(config));
        var host = builder.Build();
        var scanner = host.Services.GetRequiredService<IComicFolderScanner>();
        var str = scanner.ToString();
    }
}
