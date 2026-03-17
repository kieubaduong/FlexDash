
namespace FlexDash.Api.Plugins;

public interface IDataSourcePlugin {
    DataSourceType Type { get; }
    Task<List<DataPointDto>> FetchAsync(DataSource source, CancellationToken ct);
    IAsyncEnumerable<DataPointDto>? StreamAsync(DataSource source, CancellationToken ct);
    ValidationResultDto ValidateConfig(string configJson);
}
