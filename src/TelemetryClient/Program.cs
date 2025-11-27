using Grpc.Core;
using Grpc.Net.Client;
using System.Diagnostics;
using TelemetryGrpc;

var channel = GrpcChannel.ForAddress("https://localhost:55424", new GrpcChannelOptions
{
    // Very small for demo
    // 200 KB per message
    MaxSendMessageSize = 200 * 1024
});
var client = new Telemetry.TelemetryClient(channel);

// The client opens a bidirectional stream to the server
using var call = client.Ingest();

var cts = new CancellationTokenSource();

// Generate a 100 KB payload string
var payloadSize = 100 * 1024; // 100 KB
var payload = new string('X', payloadSize);

var sendTask = Task.Run(async () =>
{
    var sw = Stopwatch.StartNew();
    long totalPausedMs = 0;
    long peakPauseMs = 0;

    // Then it sends 50000 TelemetryEvent messages
    for (int i = 0; i < 50000; i++)
    {

        var ev = new TelemetryEvent
        {
            ClientId = "client-1",
            EventId = Convert.ToString(i),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = $"{payload}-{i}"
        };

        var t0 = sw.ElapsedMilliseconds;

        // The call is awaited, so if the server’s bounded channel is full, the client automatically waits
        // This is how backpressure is applied from server -> client
        await call.RequestStream.WriteAsync(ev);

        var t1 = sw.ElapsedMilliseconds;       

        var pauseMs = t1 - t0;
        if (pauseMs > 1)
        {
            totalPausedMs += pauseMs;
            if (pauseMs > peakPauseMs) peakPauseMs = pauseMs;

            Console.WriteLine($"WriteAsync paused for {pauseMs} ms at event {i} | Total paused: {totalPausedMs} ms | Peak: {peakPauseMs} ms");
        }

        // Optional: log every 1000 events
        if (i % 1000 == 0)
        {
            Console.WriteLine($"Sent {i} events, elapsed: {sw.ElapsedMilliseconds} ms");
        }
    }

    await call.RequestStream.CompleteAsync();
});

var recvTask = Task.Run(async () =>
{
    try
    {
        // Simultaneously, the client reads acknowledgments from the server
        await foreach (var ack in call.ResponseStream.ReadAllAsync(cts.Token))
        {
            Console.WriteLine($"ACK: {ack.ClientId} -> {ack.EventId} -> {ack.Message}");
        }
        // This shows server processed events and returned an acknowledgment
    }
    catch (OperationCanceledException) { }
});

await Task.WhenAll(sendTask, recvTask);