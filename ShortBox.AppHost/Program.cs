var builder = DistributedApplication.CreateBuilder(args);

var functions = builder.AddAzureFunctionsProject<Projects.ShortBoxFunctions>("shortboxfunctions");
    //.WithEnvironment(ctx =>
    //{
    //    ctx.EnvironmentVariables["AzureWebJobsStorage"] = "UseDevelopmentStorage=True";// builder.Configuration["AzureWebJobsStorage"]!;
    //});

builder.AddProject<Projects.ShortBoxAzureClientTesting>("shortboxazureclienttesting")
       .WithReference(functions)
       .WaitFor(functions);

builder.Build().Run();
