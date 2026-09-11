using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Rundfrage.Api.Data;
using Rundfrage.Api.Retention;

namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// T051 and T052: the failure that research R-2 measured, made deterministic.
/// </summary>
/// <remarks>
/// <b>This is the test most likely to be skipped and least safe to skip.</b> A restore fails with
/// <c>SQLite Error 5: 'database is locked'</c> if any connection holds an open transaction, and
/// the five-second busy timeout does not rescue it. A suite that merely runs a restore passes and
/// proves nothing: the collision depends on whether <see cref="RetentionSweep"/> happened to be
/// awake, which is to say on what time of day the operator acted.
/// <para>
/// So the condition is created on purpose here, rather than waited for.
/// </para>
/// </remarks>
public class RestoreLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));

    public RestoreLockTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static async Task SeedAsync(ApiFactory factory)
    {
        var admin = await factory.CreateSignedInClientAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/admin/polls", new
        {
            title = "Vor der Sicherung",
            message = (string?)null,
            days = new[] { "2027-09-10" },
        });
        created.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_restore_refuses_cleanly_while_a_transaction_is_open()
    {
        using var factory = new ApiFactory(_directory);
        await SeedAsync(factory);
        var backup = await BackupFileFixture.CreateAsync(_directory);

        try
        {
            // The condition R-2 measured: a reader inside a transaction, holding its lock.
            await using var holder = new SqliteConnection(
                StorageLocation.ConnectionStringFor(_directory));
            await holder.OpenAsync();
            StorageSetup.Apply(holder);

            await using var transaction = await holder.BeginTransactionAsync();
            await using (var read = holder.CreateCommand())
            {
                read.CommandText = "SELECT COUNT(*) FROM Polls";
                read.Transaction = (SqliteTransaction)transaction;
                await read.ExecuteScalarAsync();
            }

            using var scope = factory.Services.CreateScope();
            var restore = scope.ServiceProvider.GetRequiredService<RestoreService>();

            var (refusal, summary) = await restore.RestoreAsync(backup, CancellationToken.None);

            // A refusal the operator can act on, not an unhandled SqliteException.
            Assert.NotNull(refusal);
            Assert.Equal("storage_locked", refusal!.Code);
            Assert.Null(summary);

            await transaction.RollbackAsync();
        }
        finally
        {
            BackupFileFixture.TryDelete(backup);
        }
    }

    [Fact]
    public async Task The_retention_sweep_and_a_restore_cannot_run_at_the_same_time()
    {
        // The gate is what makes maintenance mode sufficient. Without it, FR-024 removes every
        // participant but not the sweep, which is not a request and is untouched by maintenance.
        var suspension = new RetentionSuspension();

        using var held = await suspension.AcquireAsync(CancellationToken.None);

        Assert.Null(suspension.TryAcquire());
    }

    [Fact]
    public async Task The_sweep_can_run_again_once_the_restore_has_released_the_storage()
    {
        var suspension = new RetentionSuspension();

        using (await suspension.AcquireAsync(CancellationToken.None))
        {
            Assert.Null(suspension.TryAcquire());
        }

        using var afterwards = suspension.TryAcquire();
        Assert.NotNull(afterwards);
    }

    [Fact]
    public async Task A_restore_succeeds_once_the_transaction_is_gone()
    {
        // The other half: the refusal above must be about the lock, not about the backup.
        using var factory = new ApiFactory(_directory);
        await SeedAsync(factory);
        var backup = await BackupFileFixture.CreateAsync(_directory);

        try
        {
            using var scope = factory.Services.CreateScope();
            var restore = scope.ServiceProvider.GetRequiredService<RestoreService>();

            var (refusal, summary) = await restore.RestoreAsync(backup, CancellationToken.None);

            Assert.Null(refusal);
            Assert.NotNull(summary);

            // The poll that was in the backup is what came back - not merely "a summary exists".
            Assert.Equal(1, summary!.Polls);
        }
        finally
        {
            BackupFileFixture.TryDelete(backup);
        }
    }
}
