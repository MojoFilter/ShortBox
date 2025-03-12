using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddShortBoxServices()
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

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
