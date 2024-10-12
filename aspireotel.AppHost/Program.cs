var builder = DistributedApplication.CreateBuilder(args);

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .PublishAsContainer();

var apiService = builder.AddProject<Projects.aspireotel_ApiService>("apiservice");

var serviceWorker = builder.AddProject<Projects.aspireotel_WorkerService>("workerservice")
    .WithReference(messaging);

builder.AddProject<Projects.aspireotel_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WithReference(messaging);

builder.Build().Run();
