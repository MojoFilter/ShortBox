using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddShortBoxServices()
                .AddShortBoxDataAccess(builder.Configuration)
                .AddShortBoxGoogle(opt =>
                {
                    opt.CoversFolderId = builder.Configuration["Google:CoversFolderId"] ?? throw new ArgumentNullException("Missing Google:ConversFolderId");
                    opt.CredentialsJson = builder.Configuration["Google:CredentialsJson"] ?? throw new ArgumentNullException("Missing Google:CredentialsJson");
                    opt.CredentialsUser = builder.Configuration["Google:CredentialsUser"] ?? throw new ArgumentNullException("Missing Google:CredentialsUser");
                });

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
