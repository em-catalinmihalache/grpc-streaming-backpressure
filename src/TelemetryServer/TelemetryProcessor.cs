using TelemetryGrpc;

public class TelemetryProcessor
{
    private readonly ILogger<TelemetryProcessor> _logger;

    public TelemetryProcessor(ILogger<TelemetryProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Accepted, string Message)> ProcessEventAsync(
        TelemetryEvent ev, CancellationToken ct)
    {
        await Task.Delay(500, ct);

        _logger.LogInformation("Processed {ClientId}:{EventId}", ev.ClientId, ev.EventId);

        return (true, "ok");
    }
}