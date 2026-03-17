using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

app.UseWebSockets();

// --- REST API Fake Endpoints ---

double stockPrice = 142.0;

app.MapGet("/api/fake/temperature", () => {
    double temperature = Math.Round(20.0 + Random.Shared.NextDouble() * 15.0, 1);
    return Results.Json(new {
        sensor = "demo-thermostat",
        reading = new { temperature },
        timestamp = DateTime.UtcNow
    });
});

app.MapGet("/api/fake/stockprice", () => {
    stockPrice += (Random.Shared.NextDouble() - 0.5) * 20.0;
    stockPrice = Math.Round(Math.Max(50, Math.Min(300, stockPrice)), 2);
    return Results.Json(new {
        ticker = "DEMO",
        data = new { price = stockPrice },
        timestamp = DateTime.UtcNow
    });
});

// --- WebSocket Fake Endpoint ---

app.Map("/ws/fake/heartrate", async context => {
    if (!context.WebSockets.IsWebSocketRequest) {
        context.Response.StatusCode = 400;
        return;
    }

    using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
    double baseHeartRate = 72.0;

    while (webSocket.State == WebSocketState.Open) {
        baseHeartRate += (Random.Shared.NextDouble() - 0.48) * 6.0;
        baseHeartRate = Math.Round(Math.Max(55, Math.Min(110, baseHeartRate)), 1);

        var message = JsonSerializer.Serialize(new {
            device = "wearable-01",
            metric = new { heartRate = baseHeartRate }
        });

        byte[] bytes = Encoding.UTF8.GetBytes(message);

        try {
            await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            await Task.Delay(2000);
        }
        catch (WebSocketException) {
            break;
        }
    }
});

Console.WriteLine("FlexDash.Demo running at http://localhost:5190");
Console.WriteLine("  REST:      GET /api/fake/temperature");
Console.WriteLine("  REST:      GET /api/fake/stockprice");
Console.WriteLine("  WebSocket: ws://localhost:5190/ws/fake/heartrate");

await app.RunAsync();
