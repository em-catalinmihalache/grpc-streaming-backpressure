using Grpc.Core;
using System.Threading.Channels;
using TelemetryGrpc;

public class TelemetryService : Telemetry.TelemetryBase
{
    private readonly TelemetryProcessor _processor;
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(TelemetryProcessor processor, ILogger<TelemetryService> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public override async Task Ingest(
        IAsyncStreamReader<TelemetryEvent> requestStream,
        IServerStreamWriter<TelemetryAck> responseStream,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;

        // A bounded channel is created
        // Max 1000 events buffered at a time
        // Wait mode -> if full, producer (client) must wait -> backpressure
        var channel = Channel.CreateBounded<TelemetryEvent>(new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        var consumerTask = Task.Run(async () =>
        {
            try
            {
                // Consumer Task runs in background
                // Reads events from the bounded channel at its own pace
                // Calls TelemetryProcessor.ProcessEventAsync(simulated 10ms delay)
                // Writes TelemetryAck back to the client
                // This ensures the server never consumes faster than it can handle
                await foreach (var ev in channel.Reader.ReadAllAsync(ct))
                {
                    _logger.LogInformation("Processing event from {ClientId} at {Timestamp}", ev.ClientId, ev.TimestampUnixMs);

                    var result = await _processor.ProcessEventAsync(ev, ct);

                    var ack = new TelemetryAck
                    {
                        ClientId = ev.ClientId,
                        EventId = ev.EventId,
                        TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Accepted = result.Accepted,
                        Message = result.Message                        
                    };

                    await responseStream.WriteAsync(ack);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Consumer cancelled for a client stream.");
            }
        }, ct);

        try
        {
            // Producer loop — incoming client events
            // Writes client events into the bounded channel
            // If the channel is full, WriteAsync waits -> backpressure propagates to client
            await foreach (var ev in requestStream.ReadAllAsync(ct))
            {
                _logger.LogDebug("Received event from {ClientId}", ev.ClientId);
                await channel.Writer.WriteAsync(ev, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Producer cancelled for a client stream.");
        }
        finally
        {
            channel.Writer.Complete();
            await consumerTask;
        }
    }
}