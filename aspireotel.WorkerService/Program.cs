using aspireotel.QueueCommon;
using aspireotel.WorkerService;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddRabbitMQClient(Bus.Host, configureConnectionFactory: (connectionFactory) =>
{
    connectionFactory.ClientProvidedName = "app:event-consumer";
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
