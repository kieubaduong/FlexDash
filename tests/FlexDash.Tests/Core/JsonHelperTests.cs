using FlexDash.Api.Core;
using Xunit;

namespace FlexDash.Tests.Core;

public class JsonHelperTests {
    private record TestConfig(string Name, int Value);

    [Theory]
    [InlineData("""{"name":"test","value":42}""", "test", 42)]
    [InlineData("""{"Name":"test","Value":10}""", "test", 10)]
    public void TryDeserialize_Valid_Json_Returns_Ok(string json, string expectedName, int expectedValue) {
        Result<TestConfig> result = JsonHelper.TryDeserialize<TestConfig>(json);

        Assert.True(result.IsOk);
        Assert.Equal(expectedName, result.GetData().Name);
        Assert.Equal(expectedValue, result.GetData().Value);
    }

    [Theory]
    [InlineData("{ not valid }", "Invalid JSON")]
    [InlineData("null", "parsed")]
    public void TryDeserialize_Invalid_Returns_Err(string json, string expectedError) {
        Result<TestConfig> result = JsonHelper.TryDeserialize<TestConfig>(json);

        Assert.True(result.IsError);
        Assert.Contains(expectedError, result.GetError());
    }
}
