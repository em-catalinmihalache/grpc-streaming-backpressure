using Grpc.Core;
using Grpc.Net.Client;
using System.Diagnostics;
using TelemetryGrpc;

var channel = GrpcChannel.ForAddress("https://localhost:55424", new GrpcChannelOptions
{
    // Very small for demo
    // 600 KB per message
    MaxSendMessageSize = 600 * 1024
});
var client = new Telemetry.TelemetryClient(channel);

// The client opens a bidirectional stream to the server
using var call = client.Ingest();

var cts = new CancellationTokenSource();

var sw = Stopwatch.StartNew();
long totalClientPausedMs = 0;
long peakClientPauseMs = 0;

// 1. Start reading ACKs concurrently (server-to-client streaming)
var readTask = Task.Run(async () =>
{
    try
    {
        // Simultaneously, the client reads acknowledgments from the server
        await foreach (var ack in call.ResponseStream.ReadAllAsync(cts.Token))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Client received ACK: {ack.ClientId} -> {ack.EventId} -> {ack.Status}");
            Console.ResetColor();
        }
        // This shows server processed events and returned an acknowledgment
    }
    catch (OperationCanceledException) { }
});

// 2. Write events (client-to-server streaming)
var writeTask = Task.Run(async () =>
{
    var rnd = new Random();

    for (int i = 0; i < 500; i++) // reduced for demo
    {
        // Generate a  50–600 KB payload string
        int size = rnd.Next(50 * 1024, 600 * 1024);
        var payload = new string('X', size);

        var ev = new TelemetryEvent
        {
            ClientId = "client-1",
            EventId = i,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = payload
        };

        // Random delay to simulate client "thinking" / producing bursts
        if (rnd.NextDouble() < 0.25) // 25% chance
        {
            await Task.Delay(850);
        }

        var t0 = sw.ElapsedMilliseconds;

        // It will pause if server backpressure
        await call.RequestStream.WriteAsync(ev);

        var t1 = sw.ElapsedMilliseconds;

        var pauseMs = t1 - t0;
        if (pauseMs > 1)
        {
            totalClientPausedMs += pauseMs;
            if (pauseMs > peakClientPauseMs) peakClientPauseMs = pauseMs;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Client WriteAsync paused for {pauseMs} ms at event {i} | Total paused: {totalClientPausedMs} ms | Peak: {peakClientPauseMs} ms");
            Console.ResetColor();
        }
    }

    await call.RequestStream.CompleteAsync();
});

await Task.WhenAll(readTask, writeTask);

Console.WriteLine("Client finished sending events");
Console.ReadLine();
