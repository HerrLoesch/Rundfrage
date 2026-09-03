using Testcontainers.PostgreSql;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// A disposable, genuinely empty PostgreSQL instance per test class. Reusing the Compose
/// database would not do: it is not empty after the first run, so the schema-creation test
/// would pass for the wrong reason (plan.md Complexity Tracking).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // The image goes to the constructor: the parameterless one is obsolete and will be removed.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("rundfrage")
        .WithUsername("rundfrage")
        .WithPassword("rundfrage_test")
        .Build();

    public string ConnectionString => $"{_container.GetConnectionString()};Timeout=2";

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
