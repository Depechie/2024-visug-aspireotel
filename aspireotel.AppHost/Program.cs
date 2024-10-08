var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.aspireotel_ApiService>("apiservice");

builder.AddProject<Projects.aspireotel_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
