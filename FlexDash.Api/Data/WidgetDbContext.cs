using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Data;

public sealed class WidgetDbContext : DbContext {
    public WidgetDbContext(DbContextOptions<WidgetDbContext> options) : base(options) { }
    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Widget>()
            .Property(w => w.Type)
            .HasConversion<string>();
    }
}
