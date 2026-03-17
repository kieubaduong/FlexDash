using FlexDash.Api.Dtos;
using FlexDash.Api.Plugins;
using Xunit;

namespace FlexDash.Tests.Plugins;

public class SystemMetricsPluginTests {
    private readonly SystemMetricsPlugin _plugin = new();

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"host":"192.168.1.1","port":22,"username":"admin","password":"secret"}""")]
    [InlineData("""{"host":"192.168.1.1","port":22,"username":"admin","privateKeyPath":"/home/.ssh/id_rsa"}""")]
    public void ValidateConfig_Valid_Returns_True(string config) {
        ValidationResultDto result = _plugin.ValidateConfig(config);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("""{"host":"192.168.1.1","password":"secret"}""", "Username")]
    [InlineData("""{"host":"192.168.1.1","username":"admin"}""", "Password")]
    [InlineData("broken", "Invalid JSON")]
    public void ValidateConfig_Invalid_Returns_Error(string config, string expectedError) {
        ValidationResultDto result = _plugin.ValidateConfig(config);

        Assert.False(result.IsValid);
        Assert.Contains(expectedError, result.ErrorMessage);
    }
}
