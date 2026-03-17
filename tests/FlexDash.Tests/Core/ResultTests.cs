using FlexDash.Api.Core;
using Xunit;

namespace FlexDash.Tests.Core;

public class ResultTests {
    [Theory]
    [InlineData(42)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ok_Returns_IsOk_True(int value) {
        Result<int> result = Result<int>.Ok(value);

        Assert.True(result.IsOk);
        Assert.False(result.IsError);
        Assert.Equal(value, result.GetData());
    }

    [Theory]
    [InlineData("something went wrong")]
    [InlineData("timeout")]
    [InlineData("")]
    public void Err_Returns_IsOk_False(string error) {
        Result<int> result = Result<int>.Err(error);

        Assert.False(result.IsOk);
        Assert.True(result.IsError);
        Assert.Equal(error, result.GetError());
    }

    [Fact]
    public void Ok_With_String_Value() {
        Result<string> result = Result<string>.Ok("hello");

        Assert.True(result.IsOk);
        Assert.Equal("hello", result.GetData());
    }
}
