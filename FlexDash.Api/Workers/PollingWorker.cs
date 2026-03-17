using FlexDash.Api.Hubs;
using FlexDash.Api.Services;
using FlexDash.Api.Services.Alert;
using Microsoft.AspNetCore.SignalR;

namespace FlexDash.Api.Workers;

public sealed class PollingWorker : BackgroundService {
    private readonly DataSourceOrchestrator _orchestrator;
    private readonly AlertEngine _alertEngine;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<PollingWorker> _logger;

    public PollingWorker(
        DataSourceOrchestrator orchestrator,
        AlertEngine alertEngine,
        IHubContext<DashboardHub> hubContext,
        ILogger<PollingWorker> logger) {
        _orchestrator = orchestrator;
        _alertEngine = alertEngine;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("PollingWorker started");

        // Start streaming sources (WebSocket etc.) in background
        Task streamingTask = ConsumeStreamsAsync(stoppingToken);

        // Polling loop for FetchAsync-based sources
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) {
                break;
            }

            try {
                List<DataPointDto> newPoints = await _orchestrator.PollAllAsync(stoppingToken);
                await BroadcastPointsAsync(newPoints, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                _logger.LogError(ex, "Error in polling cycle");
            }
        }

        await streamingTask;
        _logger.LogInformation("PollingWorker stopped");
    }

    private async Task ConsumeStreamsAsync(CancellationToken stoppingToken) {
        try {
            await foreach (DataPointDto point in _orchestrator.StreamAllAsync(stoppingToken)) {
                var update = new WidgetDataUpdate(point.DataSourceId, [point]);
                await _hubContext.Clients
                    .Group(point.DataSourceId.ToString())
                    .SendAsync("DataUpdate", update, stoppingToken);

                List<AlertEventDto> alertEvents = await _alertEngine.EvaluateAsync([point], stoppingToken);
                foreach (AlertEventDto alertEvent in alertEvents) {
                    await _hubContext.Clients.All.SendAsync("AlertEvent", alertEvent, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) {
            // Expected on shutdown
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in streaming consumer");
        }
    }

    private async Task BroadcastPointsAsync(List<DataPointDto> newPoints, CancellationToken stoppingToken) {
        IEnumerable<IGrouping<Guid, DataPointDto>> grouped = newPoints.GroupBy(point => point.DataSourceId);
        foreach (IGrouping<Guid, DataPointDto> group in grouped) {
            var update = new WidgetDataUpdate(group.Key, [.. group]);
            await _hubContext.Clients
                .Group(group.Key.ToString())
                .SendAsync("DataUpdate", update, stoppingToken);
        }

        List<AlertEventDto> alertEvents = await _alertEngine.EvaluateAsync(newPoints, stoppingToken);
        foreach (AlertEventDto alertEvent in alertEvents) {
            await _hubContext.Clients.All.SendAsync("AlertEvent", alertEvent, stoppingToken);
        }
    }
}
