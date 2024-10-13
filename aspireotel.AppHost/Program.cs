using aspireotel.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin()
    .PublishAsContainer();

var loki = builder
    .AddContainer("loki", "grafana/loki", "3.2.0")
    .WithEndpoint(targetPort: 3100, port: 3100, name: "http", scheme: "http")
    .WithEndpoint(targetPort: 9096, port: 9096, name: "grpc", scheme: "http")
    .WithBindMount("../config/loki.yml", "/etc/loki/local-config.yaml")
    .WithVolume("loki", "/data/loki")
    .WithArgs("-config.file=/etc/loki/local-config.yaml");

var tempo = builder
    .AddContainer("tempo", "grafana/tempo", "2.6.0")
    .WithEndpoint(targetPort: 3200, port: 3200, name: "http", scheme: "http")
    .WithEndpoint(targetPort: 4317, port: 4007, name: "otlp", scheme: "http")
    .WithBindMount("../config/tempo.yml", "/etc/tempo.yaml")
    .WithVolume("tempo", "/tmp/tempo")
    .WithArgs("-config.file=/etc/tempo.yaml")
    .WithArgs("chown 10001:10001 /var/tempo");

var otel = builder
    .AddContainer("otel", "otel/opentelemetry-collector-contrib", "0.111.0")
    .WithEndpoint(targetPort: 4317, port: 4317,  name: "grpc", scheme: "http") // Have to put the schema to HTTP otherwise the C# will complain about the OTEL_EXPORTER_OTLP_ENDPOINT variable
    .WithBindMount("../config/otel.yml", "/etc/otel-collector-config.yaml")
    .WithArgs("--config=/etc/otel-collector-config.yaml")
    .WithLokiPushUrl("LOKI_URL", loki.GetEndpoint("http"))
    .WithEnvironment("TEMPO_URL", tempo.GetEndpoint("otlp"))
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

builder.AddContainer("grafana", "grafana/grafana", "11.2.2")
    .WithEndpoint(targetPort: 3000, port: 3000, name: "http", scheme: "http")
    .WithBindMount("../config/grafana/provisioning", "/etc/grafana/provisioning")
    .WithVolume("grafana-data", "/var/lib/grafana")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
    .WithEnvironment("GF_AUTH_DISABLE_LOGIN_FORM", "true")
    .WithEnvironment("LOKI_URL", loki.GetEndpoint("http"))
    .WithEnvironment("TEMPO_URL", tempo.GetEndpoint("http"));

builder.Build().Run();
