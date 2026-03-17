using Microsoft.EntityFrameworkCore;

namespace FlexDash.Api.Data;

public sealed class AlertDbContext : DbContext {
    public AlertDbContext(DbContextOptions<AlertDbContext> options) : base(options) { }
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AlertRule>()
            .Property(ar => ar.Severity)
            .HasConversion<string>();

        modelBuilder.Entity<AlertRule>()
            .Property(ar => ar.ConditionType)
            .HasConversion<string>();
    }
}
