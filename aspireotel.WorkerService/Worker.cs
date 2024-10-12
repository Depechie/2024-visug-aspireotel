using System.Text;
using aspireotel.QueueCommon;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace aspireotel.WorkerService;

public class Worker : BackgroundService
{
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
        
        var body = args.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        _logger.LogInformation($"Message received: {message}");
    }
}