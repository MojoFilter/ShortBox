var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureFunctionsProject<Projects.ShortBoxFunctions>("shortboxfunctions");

builder.Build().Run();
