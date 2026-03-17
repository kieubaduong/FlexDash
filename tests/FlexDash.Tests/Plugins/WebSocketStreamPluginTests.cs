using FlexDash.Api.Dtos;
using FlexDash.Api.Plugins;
using Xunit;

namespace FlexDash.Tests.Plugins;

public class WebSocketStreamPluginTests {
    private readonly WebSocketStreamPlugin _plugin = new();

    [Theory]
    [InlineData("""{"url":"ws://localhost:8080/stream","valuePath":"data.value"}""")]
    [InlineData("""{"url":"wss://example.com/stream","valuePath":"value"}""")]
    public void ValidateConfig_Valid_Returns_True(string config) {
        ValidationResultDto result = _plugin.ValidateConfig(config);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("""{"url":"http://example.com/stream","valuePath":"value"}""", "ws://")]
    [InlineData("""{"valuePath":"value"}""", "Url")]
    [InlineData("broken json", "JSON")]
    public void ValidateConfig_Invalid_Returns_Error(string config, string expectedError) {
        ValidationResultDto result = _plugin.ValidateConfig(config);

        Assert.False(result.IsValid);
        Assert.Contains(expectedError, result.ErrorMessage);
    }
}
