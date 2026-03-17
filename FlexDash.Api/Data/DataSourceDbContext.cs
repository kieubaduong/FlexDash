using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Data;

public sealed class DataSourceDbContext : DbContext {
    public DataSourceDbContext(DbContextOptions<DataSourceDbContext> options) : base(options) { }
    public DbSet<DataSource> DataSources => Set<DataSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DataSource>()
            .Property(ds => ds.Type)
            .HasConversion<string>();
    }
}
