using aspireotel.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .PublishAsContainer();

var otel = builder
    .AddContainer("otel", "otel/opentelemetry-collector-contrib", "0.111.0")
    .WithEndpoint(targetPort: 4317, port: 4317,  name: "grpc", scheme: "http") // Have to put the schema to HTTP otherwise the C# will complain about the OTEL_EXPORTER_OTLP_ENDPOINT variable
    .WithBindMount("../config/otel.yml", "/etc/otel-collector-config.yaml")
    .WithArgs("--config=/etc/otel-collector-config.yaml")
    .WithDashboardEndpoint("DASHBOARD_URL");    

var apiService = builder.AddProject<Projects.aspireotel_ApiService>("apiservice")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"));

var serviceWorker = builder.AddProject<Projects.aspireotel_WorkerService>("workerservice")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"))
    .WithReference(messaging);

builder.AddProject<Projects.aspireotel_Web>("webfrontend")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otel.GetEndpoint("grpc"))
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WithReference(messaging);

builder.Build().Run();
