using FlexDash.Api.Data;
using FlexDash.Api.Hubs;
using FlexDash.Api.Plugins;
using FlexDash.Api.Services;
using FlexDash.Api.Services.Alert;
using FlexDash.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;

// Bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try {
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // 1. Serilog
    builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

    // 2. EF Core SQLite — single DB, separate contexts per domain
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<WidgetDbContext>(options => options.UseSqlite(connectionString));
    builder.Services.AddDbContext<DataSourceDbContext>(options => options.UseSqlite(connectionString));
    builder.Services.AddDbContext<AlertDbContext>(options => options.UseSqlite(connectionString));

    // 3. SignalR
    builder.Services.AddSignalR();

    // 4. Controllers + OpenAPI
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // 5. CORS — AllowCredentials required for SignalR
    builder.Services.AddCors(options => {
        options.AddPolicy("BlazorClient", policy =>
            policy
                .WithOrigins("https://localhost:7123", "http://localhost:5230")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
    });

    // 6. Data source plugins (singletons registered as IDataSourcePlugin)
    builder.Services.AddSingleton<IDataSourcePlugin, RestApiPlugin>();
    builder.Services.AddSingleton<IDataSourcePlugin, SystemMetricsPlugin>();
    builder.Services.AddSingleton<IDataSourcePlugin, WebSocketStreamPlugin>();

    // 7. HttpClient factory (used by RestApiPlugin)
    builder.Services.AddHttpClient();

    // 8. Application services
    builder.Services.AddScoped<DashboardService>();
    builder.Services.AddSingleton<DataPointBuffer>();
    builder.Services.AddSingleton<DataSourceOrchestrator>();
    builder.Services.AddSingleton<AlertEngine>();

    // 9. Background worker
    builder.Services.AddHostedService<PollingWorker>();

    WebApplication app = builder.Build();

    // 10. Ensure all tables created + seed
    // EnsureCreatedAsync() only works once per database file — subsequent calls
    // see the DB exists and skip table creation. With 3 DbContexts sharing one
    // SQLite file, we must use migrations or manual SQL. Here we use
    // RelationalDatabaseCreator to create tables for each context individually.
    using (var scope = app.Services.CreateScope()) {
        var widgetDb = scope.ServiceProvider.GetRequiredService<WidgetDbContext>();
        var dataSourceDb = scope.ServiceProvider.GetRequiredService<DataSourceDbContext>();
        var alertDb = scope.ServiceProvider.GetRequiredService<AlertDbContext>();

        // Create the database file if it doesn't exist
        await widgetDb.Database.EnsureCreatedAsync();

        // Create missing tables for the other contexts
        var dataSourceCreator = (RelationalDatabaseCreator)
            dataSourceDb.GetInfrastructure().GetRequiredService<IDatabaseCreator>();
        try { await dataSourceCreator.CreateTablesAsync(); } catch { /* tables may already exist */ }

        var alertCreator = (RelationalDatabaseCreator)
            alertDb.GetInfrastructure().GetRequiredService<IDatabaseCreator>();
        try { await alertCreator.CreateTablesAsync(); } catch { /* tables may already exist */ }

        await SeedData.SeedAsync(dataSourceDb, widgetDb, alertDb);
    }

    // Middleware pipeline
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment()) {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseCors("BlazorClient");
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<DashboardHub>("/hubs/dashboard");

    await app.RunAsync();
}
catch (Exception ex) {
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally {
    await Log.CloseAndFlushAsync();
}
