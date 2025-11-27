using Grpc.Core;
using TelemetryGrpc;

public class TelemetryService : Telemetry.TelemetryBase
{
    private readonly TelemetryProcessor _processor;    

    public TelemetryService(TelemetryProcessor processor)
    {
        _processor = processor;

        // Start background processor
        _ = _processor.StartAsync();
    }

    public override async Task Ingest(
        IAsyncStreamReader<TelemetryEvent> requestStream,
        IServerStreamWriter<TelemetryAck> responseStream,
        ServerCallContext context)
    {
        await foreach (var ev in requestStream.ReadAllAsync(context.CancellationToken))
        {
            // Server writes to processor channel (backpressure-aware)
            var writeTask = _processor.ProcessorChannel.Writer.WriteAsync(ev, context.CancellationToken).AsTask();

            if (!writeTask.IsCompleted)
                Console.WriteLine($"Server WriteAsync paused for backpressure at EventId: {ev.EventId}");

            await writeTask;

            // Send ACK to client
            await responseStream.WriteAsync(new TelemetryAck
            {
                ClientId = ev.ClientId,
                EventId = ev.EventId,
                Status = "ok"
            });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Server ACK -> EventId: {ev.EventId} -> ok | Channel count: {_processor.ProcessorChannel.Reader.Count}");
            Console.ResetColor();
        }
    }
}