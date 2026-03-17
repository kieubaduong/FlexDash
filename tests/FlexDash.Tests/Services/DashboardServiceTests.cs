using FlexDash.Api.Data;
using FlexDash.Api.Dtos;
using FlexDash.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexDash.Tests.Services;

public class DashboardServiceTests {
    private static WidgetDbContext CreateInMemoryDb() {
        DbContextOptions<WidgetDbContext> options = new DbContextOptionsBuilder<WidgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WidgetDbContext(options);
    }

    [Fact]
    public async Task GetWidgetsAsync_Returns_Empty_Initially() {
        WidgetDbContext db = CreateInMemoryDb();
        var service = new DashboardService(db);

        List<WidgetDto> widgets = await service.GetWidgetsAsync();

        Assert.Empty(widgets);
    }

    [Theory]
    [InlineData("LineChart", "CPU")]
    [InlineData("StatCard", "Memory")]
    [InlineData("Gauge", "Disk")]
    public async Task AddWidgetAsync_Valid_Type_Returns_Widget(string type, string title) {
        WidgetDbContext db = CreateInMemoryDb();
        var service = new DashboardService(db);
        Guid sourceId = Guid.NewGuid();

        var dto = new CreateWidgetDto(sourceId, title, type, 0, 0, 2, 1, "cpu");
        WidgetDto? result = await service.AddWidgetAsync(dto);

        Assert.NotNull(result);
        Assert.Equal(title, result.Title);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Theory]
    [InlineData("InvalidType")]
    [InlineData("PieChart")]
    [InlineData("")]
    public async Task AddWidgetAsync_Invalid_Type_Returns_Null(string type) {
        WidgetDbContext db = CreateInMemoryDb();
        var service = new DashboardService(db);

        var dto = new CreateWidgetDto(Guid.NewGuid(), "Test", type, 0, 0, 1, 1, null);
        WidgetDto? result = await service.AddWidgetAsync(dto);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWidgetsAsync_Returns_Added_Widgets() {
        WidgetDbContext db = CreateInMemoryDb();
        var service = new DashboardService(db);

        await service.AddWidgetAsync(new CreateWidgetDto(Guid.NewGuid(), "W1", "StatCard", 0, 0, 1, 1, null));
        await service.AddWidgetAsync(new CreateWidgetDto(Guid.NewGuid(), "W2", "Gauge", 1, 0, 1, 1, null));

        List<WidgetDto> widgets = await service.GetWidgetsAsync();

        Assert.Equal(2, widgets.Count);
    }

    [Fact]
    public async Task UpdateWidgetPositionAsync_Updates_Grid_Properties() {
        WidgetDbContext db = CreateInMemoryDb();
        var service = new DashboardService(db);

        WidgetDto? widget = await service.AddWidgetAsync(
            new CreateWidgetDto(Guid.NewGuid(), "W", "LineChart", 0, 0, 1, 1, null));

        WidgetDto? updated = await service.UpdateWidgetPositionAsync(widget!.Id,
            new UpdateWidgetPositionDto(3, 2, 4, 3));

        Assert.NotNull(updated);
        Assert.Equal(3, updated.GridColumn);
        Assert.Equal(2, updated.GridRow);
        Assert.Equal(4, updated.GridWidth);
        Assert.Equal(3, updated.GridHeight);
    }

    [Fact]
    public async Task UpdateWidgetPositionAsync_Unknown_Id_Returns_Null() {
        WidgetDbContext db = CreateInMemoryDb();
        var service = new DashboardService(db);

        WidgetDto? result = await service.UpdateWidgetPositionAsync(
            Guid.NewGuid(), new UpdateWidgetPositionDto(0, 0, 1, 1));

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveWidgetAsync_Deletes_Widget() {
        WidgetDbContext db = CreateInMemoryDb();
        var service = new DashboardService(db);

        WidgetDto? widget = await service.AddWidgetAsync(
            new CreateWidgetDto(Guid.NewGuid(), "W", "StatCard", 0, 0, 1, 1, null));

        bool removed = await service.RemoveWidgetAsync(widget!.Id);

        Assert.True(removed);
        Assert.Empty(await service.GetWidgetsAsync());
    }

    [Fact]
    public async Task RemoveWidgetAsync_Unknown_Id_Returns_False() {
        WidgetDbContext db = CreateInMemoryDb();
        var service = new DashboardService(db);

        bool removed = await service.RemoveWidgetAsync(Guid.NewGuid());

        Assert.False(removed);
    }
}
