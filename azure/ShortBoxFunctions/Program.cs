using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddShortBoxServices()
                .AddShortBoxAcquisition()
                .AddShortBoxDataAccess(builder.Configuration)
                .AddShortBoxGoogle(opt =>
                {
                    opt.CoversFolderId = builder.Configuration["Google:CoversFolderId"] ?? throw new InvalidOperationException("Missing Google:CoversFolderId");
                    opt.ArchivesFolderId = builder.Configuration["Google:ArchivesFolderId"] ?? throw new InvalidOperationException("Missing Google:ArchivesFolderId");
                    opt.CredentialsJson = builder.Configuration["Google:CredentialsJson"] ?? throw new InvalidOperationException("Missing Google:CredentialsJson");
                    opt.CredentialsUser = builder.Configuration["Google:CredentialsUser"] ?? throw new InvalidOperationException("Missing Google:CredentialsUser");
                })
                .AddShortBoxAzureServices()
                .AddAzureClients(builder =>
                builder.AddBlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")));

// Where the weekly releases come from: set Releases__Source to "Metron" to switch; Comic Vine is the default.
// The credentials are optional at startup so the reader functions keep working without them;
// release lookups fail until they are set.
if (string.Equals(builder.Configuration["Releases:Source"], "Metron", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddShortBoxMetron(opt =>
    {
        opt.Username = builder.Configuration["Metron:Username"] ?? string.Empty;
        opt.Password = builder.Configuration["Metron:Password"] ?? string.Empty;
        opt.Publisher = builder.Configuration["Metron:Publisher"] ?? "marvel";
    });
}
else
{
    builder.Services.AddShortBoxComicVine(opt =>
    {
        opt.ApiKey = builder.Configuration["ComicVine:ApiKey"] ?? string.Empty;
        opt.Publisher = builder.Configuration["ComicVine:Publisher"] ?? "Marvel";
    });
}

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
