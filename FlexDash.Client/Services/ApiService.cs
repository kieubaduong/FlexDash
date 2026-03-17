using FlexDash.Client.Models;
using System.Net.Http.Json;

namespace FlexDash.Client.Services;

public sealed class ApiService(HttpClient http) {
    // Widgets
    public Task<List<WidgetDto>?> GetWidgetsAsync()
        => http.GetFromJsonAsync<List<WidgetDto>>("api/dashboard/widgets");

    public async Task<WidgetDto?> AddWidgetAsync(CreateWidgetDto dto) {
        var response = await http.PostAsJsonAsync("api/dashboard/widgets", dto);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<WidgetDto>();
    }

    public async Task<WidgetDto?> UpdateWidgetPositionAsync(Guid widgetId, UpdateWidgetPositionDto dto) {
        var response = await http.PutAsJsonAsync($"api/dashboard/widgets/{widgetId}/position", dto);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<WidgetDto>();
    }

    public Task<HttpResponseMessage> RemoveWidgetAsync(Guid widgetId)
        => http.DeleteAsync($"api/dashboard/widgets/{widgetId}");

    // Data Sources
    public Task<List<DataSourceDto>?> GetDataSourcesAsync()
        => http.GetFromJsonAsync<List<DataSourceDto>>("api/datasources");

    public Task<DataSourceDto?> GetDataSourceAsync(Guid id)
        => http.GetFromJsonAsync<DataSourceDto>($"api/datasources/{id}");

    public async Task<DataSourceDto?> CreateDataSourceAsync(CreateDataSourceDto dto) {
        var response = await http.PostAsJsonAsync("api/datasources", dto);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<DataSourceDto>();
    }

    public async Task<DataSourceDto?> UpdateDataSourceAsync(Guid id, UpdateDataSourceDto dto) {
        var response = await http.PutAsJsonAsync($"api/datasources/{id}", dto);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<DataSourceDto>();
    }

    public Task<HttpResponseMessage> DeleteDataSourceAsync(Guid id)
        => http.DeleteAsync($"api/datasources/{id}");

    public async Task<ValidationResultDto?> ValidateDataSourceAsync(ValidateDataSourceDto dto) {
        var response = await http.PostAsJsonAsync("api/datasources/validate", dto);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ValidationResultDto>();
    }

    public async Task<ValidationResultDto?> TestConnectionAsync(TestConnectionDto dto) {
        var response = await http.PostAsJsonAsync("api/datasources/test-connection", dto);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ValidationResultDto>();
    }

    public async Task<List<DataPointDto>?> FetchDataSourceAsync(Guid id) {
        var response = await http.PostAsync($"api/datasources/{id}/fetch", null);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<List<DataPointDto>>();
    }

    public Task<List<DataPointDto>?> GetDataPointsAsync(Guid sourceId, int limit = 100)
        => http.GetFromJsonAsync<List<DataPointDto>>($"api/datasources/{sourceId}/datapoints?limit={limit}");

    // Alerts
    public Task<List<AlertRuleDto>?> GetAlertRulesAsync(Guid? dataSourceId = null) {
        string url = dataSourceId.HasValue
            ? $"api/alerts/rules?dataSourceId={dataSourceId}"
            : "api/alerts/rules";
        return http.GetFromJsonAsync<List<AlertRuleDto>>(url);
    }

    public async Task<AlertRuleDto?> CreateAlertRuleAsync(CreateAlertRuleDto dto) {
        var response = await http.PostAsJsonAsync("api/alerts/rules", dto);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AlertRuleDto>();
    }

    public Task<HttpResponseMessage> DeleteAlertRuleAsync(Guid id)
        => http.DeleteAsync($"api/alerts/rules/{id}");

    public Task<List<AlertEventDto>?> GetAlertEventsAsync(int limit = 100)
        => http.GetFromJsonAsync<List<AlertEventDto>>($"api/alerts/events?limit={limit}");

    public Task<HttpResponseMessage> AcknowledgeAlertAsync(Guid id)
        => http.PostAsync($"api/alerts/events/{id}/ack", null);
}
