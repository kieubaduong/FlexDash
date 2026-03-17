using FlexDash.Api.Core;
using FlexDash.Api.Dtos;
using Xunit;

namespace FlexDash.Tests.Core;

public class RingBufferTests {
    [Fact]
    public void AddRange_And_ToList_Returns_Items_In_Order() {
        var buffer = new DataPointBuffer(10);
        Guid id = Guid.NewGuid();

        buffer.Add(id, [
            new(id, 1.0, "a", DateTime.UtcNow),
            new(id, 2.0, "b", DateTime.UtcNow),
            new(id, 3.0, "c", DateTime.UtcNow)
        ]);

        List<DataPointDto> points = buffer.Get(id);
        Assert.Equal(3, points.Count);
        Assert.Equal(1.0, points[0].Value);
        Assert.Equal(3.0, points[2].Value);
    }

    [Fact]
    public void Buffer_Overwrites_When_Capacity_Exceeded() {
        var buffer = new DataPointBuffer(3);
        Guid id = Guid.NewGuid();

        buffer.Add(id, [
            new(id, 1.0, null, DateTime.UtcNow),
            new(id, 2.0, null, DateTime.UtcNow),
            new(id, 3.0, null, DateTime.UtcNow),
            new(id, 4.0, null, DateTime.UtcNow),
            new(id, 5.0, null, DateTime.UtcNow)
        ]);

        List<DataPointDto> points = buffer.Get(id);
        Assert.Equal(3, points.Count);
        Assert.Equal(3.0, points[0].Value);
        Assert.Equal(5.0, points[2].Value);
    }

    [Fact]
    public void Get_Unknown_SourceId_Returns_Empty() {
        var buffer = new DataPointBuffer();
        Assert.Empty(buffer.Get(Guid.NewGuid()));
    }

    [Fact]
    public void GetSince_Filters_By_Timestamp() {
        var buffer = new DataPointBuffer();
        Guid id = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        buffer.Add(id, [
            new(id, 1.0, null, now.AddMinutes(-10)),
            new(id, 2.0, null, now.AddMinutes(-1)),
            new(id, 3.0, null, now)
        ]);

        List<DataPointDto> recent = buffer.GetSince(id, now.AddMinutes(-2));
        Assert.Equal(2, recent.Count);
    }

    [Fact]
    public void HasDataSince_Returns_True_When_Data_Exists() {
        var buffer = new DataPointBuffer();
        Guid id = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        buffer.Add(id, [new(id, 1.0, null, now)]);

        Assert.True(buffer.HasDataSince(id, now.AddSeconds(-5)));
        Assert.False(buffer.HasDataSince(id, now.AddSeconds(5)));
    }

    [Fact]
    public void HasDataSince_Unknown_Source_Returns_False() {
        var buffer = new DataPointBuffer();
        Assert.False(buffer.HasDataSince(Guid.NewGuid(), DateTime.UtcNow));
    }
}
