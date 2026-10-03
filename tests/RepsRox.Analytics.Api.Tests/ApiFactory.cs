using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using RepsRox.Analytics.Api.Data;

namespace RepsRox.Analytics.Api.Tests;

/// <summary>
/// Hosts the API over an in-memory SQLite database, so the tests run without a SQL
/// Server. The schema comes from the model rather than the SQL Server migrations.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            // EF Core 9+ also keeps the SQL Server setup as an options configuration.
            services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AnalyticsDbContext>>();
            _connection.Open();
            services.AddDbContext<AnalyticsDbContext>(o => o.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList()) services.Remove(d);
    }
}
