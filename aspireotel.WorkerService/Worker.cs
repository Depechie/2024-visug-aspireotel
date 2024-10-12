using System.Diagnostics;
using System.Text;
using aspireotel.QueueCommon;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace aspireotel.WorkerService;

public class Worker : BackgroundService
{
    private static readonly ActivitySource _activitySource = new("Aspire.RabbitMQ.Client");
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IConnection? _messageConnection;
    private IModel? _messageChannel;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Factory.StartNew(() =>
        {
             _logger.LogInformation($"Awaiting messages...");
             
            _messageConnection = _serviceProvider.GetRequiredService<IConnection>();
            _messageChannel = _messageConnection.CreateModel();
            _messageChannel.QueueDeclare(Queue.Orders, durable: true, exclusive: false);

            var consumer = new EventingBasicConsumer(_messageChannel);
            consumer.Received += async (s, e) => await ProcessMessageAsync(s, e);

            _messageChannel.BasicConsume(queue: Queue.Orders,
                                         autoAck: true,
                                         consumer: consumer);
        }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
       await base.StopAsync(cancellationToken);

        _messageChannel?.Dispose();
    }

    private async Task ProcessMessageAsync(object? sender, BasicDeliverEventArgs args)
    {
        _logger.LogInformation($"Processing message...");

        var parentContext = Propagator.Extract(default, args.BasicProperties, ExtractTraceContextFromBasicProperties);
        Baggage.Current = parentContext.Baggage;

        using var activity = _activitySource.StartActivity($"{Queue.Orders} consume", ActivityKind.Consumer, parentContext.ActivityContext);
        if (activity is not null)
        {
            AddActivityTags(activity);
            var body = args.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            _logger.LogInformation($"Message received: {message}");
        }
    }

    private IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
    {
        try
        {
            if (props.Headers.TryGetValue(key, out var value))
            {
                var bytes = value as byte[];
                return new[] { bytes is null ? "" : Encoding.UTF8.GetString(bytes) };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to extract trace context: {ex}");
        }

        return Enumerable.Empty<string>();
    }

    private void AddActivityTags(Activity activity)
    {
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.destination", Queue.Orders);
        activity?.SetTag("messaging.rabbitmq.routing_key", Queue.Orders);
    }    
}