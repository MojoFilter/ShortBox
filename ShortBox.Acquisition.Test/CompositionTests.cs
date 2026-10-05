using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShortBox.Services;

namespace ShortBox.Acquisition.Test;

/// <summary>Registers the services the way the Functions host does and checks the new entry points can be built.</summary>
[TestClass]
public class CompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShortBoxContext"] = "Server=(localdb)\\none;Database=none"
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddShortBoxServices()
            .AddShortBoxAcquisition()
            .AddShortBoxDataAccess(configuration)
            .AddShortBoxGoogle(opt =>
            {
                opt.CoversFolderId = "covers";
                opt.ArchivesFolderId = "archives";
                opt.CredentialsJson = "{}";
                opt.CredentialsUser = "someone@example.com";
            })
            .AddShortBoxMetron(opt =>
            {
                opt.Username = "user";
                opt.Password = "password";
            })
            .BuildServiceProvider();
    }

    /// <summary>
    /// The Functions host validates every registration at startup when running locally (Development environment),
    /// and a single unresolvable service stops the whole worker. This mirrors Program.cs with the same validation.
    /// </summary>
    [TestMethod]
    [DataRow("ComicVine")]
    [DataRow("Metron")]
    public void FunctionsHostRegistrationsValidate(string releaseSource)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShortBoxContext"] = "Server=(localdb)\\none;Database=none"
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddShortBoxServices()
            .AddShortBoxAcquisition()
            .AddShortBoxDataAccess(configuration)
            .AddShortBoxGoogle(opt => opt.CredentialsJson = "{}")
            .AddShortBoxAzureServices();
        services.AddAzureClients(clients => clients.AddBlobServiceClient("UseDevelopmentStorage=true"));
        _ = releaseSource == "Metron"
            ? services.AddShortBoxMetron(opt => { })
            : services.AddShortBoxComicVine(opt => { });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.IsNotNull(provider.GetRequiredService<IReleaseChecklist>());
    }

    [TestMethod]
    public void IngestorAndChecklistCanBeResolved()
    {
        using var provider = BuildProvider();

        Assert.IsNotNull(provider.GetRequiredService<ILibraryIngestor>());
        Assert.IsNotNull(provider.GetRequiredService<IReleaseChecklist>());
        Assert.IsNotNull(provider.GetRequiredService<IPullListStore>());
    }

    [TestMethod]
    public void MetronCanBeRegisteredAsTheReleaseSource()
    {
        using var provider = BuildProvider();
        var source = provider.GetRequiredService<IReleaseSource>();

        Assert.IsInstanceOfType<ShortBox.Metron.MetronReleaseSource>(source);
    }

    [TestMethod]
    public void ComicVineCanBeRegisteredAsTheReleaseSource()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddShortBoxComicVine(opt => opt.ApiKey = "key")
            .BuildServiceProvider();

        Assert.IsInstanceOfType<ShortBox.ComicVine.ComicVineReleaseSource>(provider.GetRequiredService<IReleaseSource>());
    }
}
