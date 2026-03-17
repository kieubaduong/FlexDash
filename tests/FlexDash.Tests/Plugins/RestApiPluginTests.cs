using FlexDash.Api.Dtos;
using FlexDash.Api.Plugins;
using Xunit;

namespace FlexDash.Tests.Plugins;

public class RestApiPluginTests {
    private RestApiPlugin CreatePlugin() {
        var factory = new TestHttpClientFactory();
        return new RestApiPlugin(factory);
    }

    [Fact]
    public void ValidateConfig_Valid_Returns_IsValid_True() {
        RestApiPlugin plugin = CreatePlugin();
        var config = """{"url":"https://api.example.com/data","valuePath":"result.value"}""";

        ValidationResultDto result = plugin.ValidateConfig(config);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("""{"valuePath":"result.value"}""", "Url")]
    [InlineData("""{"url":"https://api.example.com/data"}""", "ValuePath")]
    [InlineData("""{"url":"not-a-url","valuePath":"value"}""", "URI")]
    [InlineData("{ not valid json", "JSON")]
    [InlineData("{}", null)]
    public void ValidateConfig_Invalid_Returns_Error(string config, string? expectedErrorSubstring) {
        RestApiPlugin plugin = CreatePlugin();

        ValidationResultDto result = plugin.ValidateConfig(config);

        Assert.False(result.IsValid);
        if (expectedErrorSubstring is not null)
            Assert.Contains(expectedErrorSubstring, result.ErrorMessage);
    }
}

internal class TestHttpClientFactory : IHttpClientFactory {
    public HttpClient CreateClient(string name) => new();
}
