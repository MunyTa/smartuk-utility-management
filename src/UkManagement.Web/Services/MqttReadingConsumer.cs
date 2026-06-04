using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace UkManagement.Web.Services;

public sealed class MqttReadingConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<MqttOptions> options,
    ILogger<MqttReadingConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MqttOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("MQTT consumer is disabled by configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunClientAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MQTT consumer disconnected. Reconnecting in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunClientAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        var clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.Host, _options.Port)
            .WithCleanSession()
            .Build();

        await client.ConnectAsync(clientOptions, stoppingToken);
        await client.SubscribeAsync(_options.Topic, cancellationToken: stoppingToken);
        logger.LogInformation("Subscribed to MQTT topic {Topic} at {Host}:{Port}", _options.Topic, _options.Host, _options.Port);

        while (!stoppingToken.IsCancellationRequested && client.IsConnected)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var payloadText = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
        MeterReadingPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MeterReadingPayload>(payloadText, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid MQTT payload: {Payload}", payloadText);
            return;
        }

        if (payload is null)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<MeterReadingIngestionService>();
        var result = await ingestion.IngestAsync(payload);
        logger.LogInformation("MQTT reading processed: {Accepted}, {Message}", result.Accepted, result.Message);
    }
}
