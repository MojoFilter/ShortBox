var builder = DistributedApplication.CreateBuilder(args);

// Azurite stands in for Azure Storage: the Functions host's own storage (queues, locks) and the pages/covers blobs.
// Everything else the Functions app needs (Google, Comic Vine, SQL) still comes from its local.settings.json.
var storage = builder.AddAzureStorage("storage")
                     .RunAsEmulator(azurite => azurite.WithLifetime(ContainerLifetime.Persistent));
var blobs = storage.AddBlobs("blobs");

var functions = builder.AddAzureFunctionsProject<Projects.ShortBoxFunctions>("shortboxfunctions")
                       .WithHostStorage(storage)
                       .WithReference(blobs)
                       .WaitFor(storage);

builder.AddProject<Projects.ShortBoxAzureClientTesting>("shortboxazureclienttesting")
       .WithReference(functions)
       .WaitFor(functions);

builder.Build().Run();
