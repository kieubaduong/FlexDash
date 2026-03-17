using FlexDash.Client.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace FlexDash.Client.Services;

public sealed class SignalRService : IAsyncDisposable {
    private HubConnection? _connection;

    public Action<WidgetDataUpdate>? OnDataReceived { get; set; }
    public Action<AlertEventDto>? OnAlertReceived { get; set; }

    public async Task StartAsync(string hubUrl) {
        if (_connection is not null &&
            _connection.State != HubConnectionState.Disconnected) {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<WidgetDataUpdate>("DataUpdate", update =>
            OnDataReceived?.Invoke(update));

        _connection.On<AlertEventDto>("AlertEvent", alert =>
            OnAlertReceived?.Invoke(alert));

        await _connection.StartAsync();
    }

    public Task SubscribeToSourceAsync(Guid sourceId) {
        if (_connection is null) {
            return Task.CompletedTask;
        }

        return _connection.InvokeAsync("SubscribeToSource", sourceId);
    }

    public Task UnsubscribeFromSourceAsync(Guid sourceId) {
        if (_connection is null) {
            return Task.CompletedTask;
        }

        return _connection.InvokeAsync("UnsubscribeFromSource", sourceId);
    }

    public async ValueTask DisposeAsync() {
        if (_connection is not null) {
            await _connection.DisposeAsync();
        }
    }
}
