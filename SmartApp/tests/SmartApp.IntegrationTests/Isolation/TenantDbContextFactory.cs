using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartApp.Application.Common.Interfaces;
using SmartApp.Persistence.Context;
using SmartApp.Persistence.Interceptors;

namespace SmartApp.IntegrationTests.Isolation;

/// <summary>
/// Builds an <see cref="AppDbContext"/> over a shared in-memory SQLite connection. SQLite is a real
/// relational engine, so the EF Core global query filter and soft-delete behavior execute exactly as
/// they would against SQL Server (unlike the EF in-memory provider). The connection stays open for the
/// fixture's lifetime so the schema and data persist across contexts.
///
/// The audit/soft-delete interceptor is wired in — the same one used in production — so tests exercise
/// the real delete-to-soft-delete conversion path.
/// </summary>
public sealed class TenantDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly AuditableEntityInterceptor _interceptor;

    public TestTenantProvider TenantProvider { get; } = new();

    public TenantDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _interceptor = new AuditableEntityInterceptor(new TestCurrentUser(), new TestClock());

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_interceptor)
            .Options;

        // Create the schema once from the model.
        using AppDbContext ctx = Create();
        ctx.Database.EnsureCreated();
    }

    /// <summary>Creates a fresh context sharing the same underlying database and tenant provider.</summary>
    public AppDbContext Create() => new(_options, TenantProvider);

    public void Dispose() => _connection.Dispose();

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public long? UserId => 999;
        public bool IsAuthenticated => true;
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
