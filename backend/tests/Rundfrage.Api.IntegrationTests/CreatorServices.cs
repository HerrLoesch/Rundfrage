using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rundfrage.Api.Creators;
using Rundfrage.Api.Data;
using Rundfrage.Api.Retention;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// Builds the creator services against a real storage file, without a host.
/// </summary>
/// <remarks>
/// The two things this feature most needs proved - the access filter and the cap of a hundred -
/// are reachable with no endpoint in existence, which is why 009's tasks put their tests in the
/// foundational phase ahead of the code. Driving them through HTTP would have meant waiting two
/// phases to find out whether they work, and both are the kind of defect that looks like a working
/// feature until somebody looks.
/// </remarks>
public sealed class CreatorServices(string dataDirectory) : IAsyncDisposable
{
    public RundfrageDbContext Db { get; } = new(
        new DbContextOptionsBuilder<RundfrageDbContext>()
            .UseSqlite(StorageLocation.ConnectionStringFor(dataDirectory))
            .AddInterceptors(StorageSetup.Interceptor)
            .Options);

    public BerlinClock Clock { get; } = new(TimeProvider.System);

    public RetentionService Retention => new(Db, Clock, NullLogger<RetentionService>.Instance);

    public CreatorService Creators => new(Db, Clock, NullLogger<CreatorService>.Instance);

    public OwnerScope ScopeFor(Rundfrage.Api.Data.Entities.Creator owner)
    {
        var scope = new OwnerScope(Db, Retention);
        scope.Bind(owner);
        return scope;
    }

    public static async Task<CreatorServices> StartAsync(string dataDirectory)
    {
        var services = new CreatorServices(dataDirectory);
        await DatabaseStartup.ApplyMigrationsAsync(
            services.Db, NullLogger.Instance, CancellationToken.None);
        return services;
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
