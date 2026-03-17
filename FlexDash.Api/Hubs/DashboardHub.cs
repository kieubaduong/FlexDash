using Microsoft.AspNetCore.SignalR;

namespace FlexDash.Api.Hubs;

public sealed class DashboardHub : Hub {
    public Task SubscribeToSource(Guid sourceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, sourceId.ToString());

    public Task UnsubscribeFromSource(Guid sourceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, sourceId.ToString());
}
