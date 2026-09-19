using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Rundfrage.Api.Data;

/// <summary>
/// Begins a transaction that holds the write lock from its first statement.
/// </summary>
/// <remarks>
/// <b>The provider's own <c>BeginTransactionAsync</c> starts a <i>deferred</i> transaction</b>,
/// which is the right default for reads and the wrong one for every read-modify-write in this
/// system. A deferred transaction takes its lock at the first write, so two callers can both read
/// "one place free" or "ninety-nine Ersteller" before either of them writes.
/// <para>
/// Extracted on its <i>second</i> concrete caller, which is exactly the threshold Principle III
/// sets: claiming an item against its capacity (008 FR-017a), and creating an Ersteller against the
/// limit of a hundred (009 FR-009, research R-11). Feature 009's research predicted a third caller
/// in <c>WishListService</c>; there was none, and the correction is recorded in R-11 rather than
/// quietly left as a stale justification.
/// </para>
/// <para>
/// Sqlite-specific by construction, and deliberately so: it reaches for the underlying
/// <see cref="SqliteConnection"/> because <c>deferred: false</c> exists nowhere else. The
/// constitution pins SQLite, so there is no second provider for this to be wrong about.
/// </para>
/// </remarks>
public static class SqliteWriteTransaction
{
    public static async Task<IDbContextTransaction> BeginAsync(
        DbContext db, CancellationToken ct)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var immediate = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

        try
        {
            return await db.Database.UseTransactionAsync(immediate, ct)
                   ?? throw new InvalidOperationException("The write transaction could not be adopted.");
        }
        catch
        {
            await immediate.DisposeAsync();
            throw;
        }
    }
}
