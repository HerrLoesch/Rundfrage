using Microsoft.EntityFrameworkCore;

namespace Rundfrage.Api.Data;

/// <summary>
/// Declares no <c>DbSet&lt;&gt;</c>. This feature stores no domain data: the connectivity
/// check is schema-independent and reads no application table (data-model.md §1,
/// research.md R-1). The context exists to own the connection, host the migration pipeline,
/// and execute the probe query. The first survey feature adds entities here.
/// </summary>
/// <remarks>
/// Deliberately configured without <c>EnableRetryOnFailure</c>. An execution strategy with
/// retries would multiply the probe's 2-second budget (FR-012). Retry lives explicitly around
/// the startup migration instead - see <c>DatabaseStartup</c> and research.md R-3.
/// </remarks>
public sealed class RundfrageDbContext(DbContextOptions<RundfrageDbContext> options)
    : DbContext(options);
