var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
builder.Services.AddSingleton<TelemetryProcessor>();

var app = builder.Build();
app.MapGrpcService<TelemetryService>();
app.MapGet("/", () => "Telemetry gRPC Server running...");
app.Run();