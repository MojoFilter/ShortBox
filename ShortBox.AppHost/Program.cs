var builder = DistributedApplication.CreateBuilder(args);

var functions = builder.AddAzureFunctionsProject<Projects.ShortBoxFunctions>("shortboxfunctions");

builder.AddProject<Projects.ShortBoxAzureClientTesting>("shortboxazureclienttesting")
       .WithReference(functions);

builder.Build().Run();
