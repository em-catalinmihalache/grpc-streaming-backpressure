using System.Threading.Channels;
using TelemetryGrpc;

public class TelemetryProcessor
{
    private readonly Channel<TelemetryEvent> _channel;

    public TelemetryProcessor()
    {
        var options = new BoundedChannelOptions(20)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<TelemetryEvent>(options);
    }

    public Channel<TelemetryEvent> ProcessorChannel => _channel;

    public async Task StartAsync(CancellationToken ct = default)
    {
        await foreach (var ev in _channel.Reader.ReadAllAsync(ct))
        {
            // Simulate slow processing BEFORE freeing the channel
            await Task.Delay(Random.Shared.Next(1, 500), ct);
        }
    }
}