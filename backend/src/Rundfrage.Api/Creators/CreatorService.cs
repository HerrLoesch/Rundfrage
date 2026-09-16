using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Creators;

/// <summary>
/// A refusal carrying the machine-readable code from the contract.
/// </summary>
/// <remarks>
/// <paramref name="Limit"/> and <paramref name="Detail"/> mean what they mean in
/// <c>WishError</c>: the number or the word that makes the refusal actionable. Here that is the
/// character limit, the hundred of FR-009, or the name a duplicate collides with (FR-002).
/// Operator-written text only - there is no participant anywhere near this type.
/// </remarks>
public sealed record CreatorError(string Code, int? Limit = null, string? Detail = null);

/// <summary>
/// Creating, renaming, reissuing, revoking and deleting Ersteller (009 FR-001 to FR-020c).
/// </summary>
/// <remarks>
/// A sibling of <c>PollService</c> and <c>WishListService</c>, not an abstraction over them. They
/// share mechanisms - the capability token, the write transaction, the neutral refusal - and no
/// behaviour, which is the note <c>Program.cs</c> already carries for feature 008.
/// </remarks>
public sealed class CreatorService(
    RundfrageDbContext db, BerlinClock clock, ILogger<CreatorService> logger)
{
    /// <summary>
    /// Pure, so FR-001 and FR-002 are testable without a database and so there is exactly one
    /// place where "enforced on the server" is true.
    /// </summary>
    public static CreatorError? ValidateName(string? name) => name switch
    {
        null or "" => new CreatorError(ErrorCodes.CreatorNameRequired),
        _ when string.IsNullOrWhiteSpace(name) => new CreatorError(ErrorCodes.CreatorNameRequired),
        { Length: > Creator.NameMaxLength } =>
            new CreatorError(ErrorCodes.CreatorNameTooLong, Creator.NameMaxLength),
        _ => null,
    };

    /// <summary>
    /// FR-001, FR-002, FR-004, FR-009.
    /// </summary>
    /// <remarks>
    /// <b>The cap is the transaction, not a check before one.</b> Counting the Ersteller and
    /// inserting one are a read-modify-write, and SQLite's default deferred transaction takes its
    /// lock too late to make that safe: two requests would both read ninety-nine. The window is
    /// small and the damage mild - a hundred and first Ersteller - but the pattern already exists
    /// for capacity in <c>ClaimService</c>, and writing the naive version here would leave the
    /// codebase with two answers to one question (research R-11).
    /// <para>
    /// The uniqueness check sits inside the same transaction for the same reason, and the unique
    /// index behind it is the backstop for any write path that does not come through here.
    /// </para>
    /// </remarks>
    public async Task<(Creator? Created, CreatorError? Error)> CreateAsync(
        string? name, CancellationToken ct)
    {
        var invalid = ValidateName(name);
        if (invalid is not null)
        {
            return (null, invalid);
        }

        var trimmed = name!.Trim();

        await using var transaction = await SqliteWriteTransaction.BeginAsync(db, ct);

        // FR-009a: revoked Ersteller are counted. One still exists and still owns its content, so
        // it still occupies a place - which is why the refusal below has to say that a place is
        // freed by deleting rather than by revoking.
        var existing = await db.Creators.CountAsync(ct);
        if (existing >= Creator.MaxCreators)
        {
            await transaction.RollbackAsync(ct);
            return (null, new CreatorError(ErrorCodes.CreatorLimitReached, Creator.MaxCreators));
        }

        var collision = await FindByNameAsync(trimmed, excluding: null, ct);
        if (collision is not null)
        {
            await transaction.RollbackAsync(ct);
            return (null, new CreatorError(
                ErrorCodes.CreatorNameDuplicate, Detail: collision.Name));
        }

        var creator = new Creator
        {
            Id = Guid.CreateVersion7(),
            Name = trimmed,
            LinkToken = CapabilityToken.Mint(),
            CreatedAt = clock.Now,
        };

        db.Creators.Add(creator);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // FR-037: the identifier only. Never the token, and never the name either - a log line
        // naming a person is a step this feature does not need (research R-13).
        logger.LogInformation("Ersteller created {CreatorId}", creator.Id);

        return (creator, null);
    }

    public Task<Creator?> FindAsync(Guid id, CancellationToken ct) =>
        db.Creators.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <summary>
    /// Resolves a link to its Ersteller, or to nothing (FR-006, FR-008).
    /// </summary>
    /// <remarks>
    /// <b>No shape check before the lookup, deliberately.</b> Rejecting a malformed token early
    /// would make it measurably faster than an unknown one, and telling those apart is exactly what
    /// the neutral refusal exists to deny (002 research R-4). The null and empty guards below are
    /// not that: they stop a null token matching a <i>revoked</i> Ersteller, whose
    /// <see cref="Creator.LinkToken"/> is null - which would hand a revoked link's holder their
    /// access back, and would be the single worst bug this feature could have.
    /// </remarks>
    public Task<Creator?> ResolveAsync(string? linkToken, CancellationToken ct) =>
        string.IsNullOrEmpty(linkToken)
            ? Task.FromResult<Creator?>(null)
            : db.Creators.FirstOrDefaultAsync(c => c.LinkToken == linkToken, ct);

    /// <summary>FR-017: the name changes; the link and the content do not.</summary>
    public async Task<(Creator? Updated, CreatorError? Error)> RenameAsync(
        Creator creator, string? name, CancellationToken ct)
    {
        var invalid = ValidateName(name);
        if (invalid is not null)
        {
            return (null, invalid);
        }

        var trimmed = name!.Trim();

        var collision = await FindByNameAsync(trimmed, excluding: creator.Id, ct);
        if (collision is not null)
        {
            return (null, new CreatorError(ErrorCodes.CreatorNameDuplicate, Detail: collision.Name));
        }

        creator.Name = trimmed;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Ersteller renamed {CreatorId}", creator.Id);

        return (creator, null);
    }

    /// <summary>
    /// FR-016: a new link. The old one stops resolving immediately, and everything this Ersteller
    /// owns stays theirs.
    /// </summary>
    /// <remarks>
    /// This is also how a revoked Ersteller is given access again (FR-020): there is no separate
    /// "unrevoke", because revocation is the absence of a token and issuing one is its opposite.
    /// <para>
    /// The old link stops working by the only mechanism that cannot be forgotten - the old value is
    /// no longer in the table, so <see cref="ResolveAsync"/> misses and the neutral refusal answers.
    /// Nothing filters a stale token, because no stale token is kept (research R-3).
    /// </para>
    /// </remarks>
    public async Task<Creator?> ReissueAsync(Guid id, CancellationToken ct)
    {
        var creator = await FindAsync(id, ct);
        if (creator is null)
        {
            return null;
        }

        creator.LinkToken = CapabilityToken.Mint();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Ersteller link reissued {CreatorId}", creator.Id);

        return creator;
    }

    /// <summary>
    /// FR-018 and FR-020: the link stops working, and <b>nothing else happens at all</b>.
    /// </summary>
    /// <remarks>
    /// No poll, wish list, answer or entry is removed, altered or reassigned; the Ersteller stays
    /// listed under its name and keeps owning everything it owned. That is the whole of the
    /// difference between this and <see cref="DeleteAsync"/>, and it is why the two are separate
    /// operations on separate addresses rather than one operation with a flag (FR-020b).
    /// </remarks>
    public async Task<bool> RevokeAsync(Guid id, CancellationToken ct)
    {
        var creator = await FindAsync(id, ct);
        if (creator is null)
        {
            return false;
        }

        creator.LinkToken = null;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Ersteller link revoked {CreatorId}", creator.Id);

        return true;
    }

    /// <summary>
    /// FR-020a: destroys the Ersteller together with every poll and wish list it owns.
    /// </summary>
    /// <remarks>
    /// The cascade is the database's, configured explicitly in <c>RundfrageDbContext</c> and
    /// enforced by <c>PRAGMA foreign_keys</c>, which <c>StorageSetup</c> turns on for every
    /// connection. It has to be explicit: EF Core's default for an optional relationship is
    /// <c>ClientSetNull</c>, which would set <c>CreatorId</c> to null on everything this Ersteller
    /// owned - handing it to the operator rather than destroying it, which FR-020c forbids and
    /// which nothing would have reported (research R-4).
    /// <para>
    /// Deleting also frees the name and the place against the hundred; revoking frees neither
    /// (FR-002, FR-009a).
    /// </para>
    /// </remarks>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var removed = await db.Creators.Where(c => c.Id == id).ExecuteDeleteAsync(ct);

        if (removed == 0)
        {
            return false;
        }

        logger.LogInformation("Ersteller deleted {CreatorId}", id);

        return true;
    }

    /// <summary>
    /// FR-002, case-insensitively and across revoked Ersteller too - a revoked one still exists,
    /// so its name is still taken.
    /// </summary>
    /// <remarks>
    /// Compared with <see cref="StringComparison.OrdinalIgnoreCase"/> in memory after a NOCASE
    /// match in the database, so the service is the stricter of the two beyond ASCII: SQLite's
    /// NOCASE folds A-Z only, so "Jörg"/"JÖRG" is caught here and not by the index. That is the
    /// same arrangement <c>WishItem.Name</c> records, and the same safe direction.
    /// </remarks>
    private async Task<Creator?> FindByNameAsync(string name, Guid? excluding, CancellationToken ct)
    {
        var candidates = await db.Creators
            .Where(c => excluding == null || c.Id != excluding)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        return match is null ? null : new Creator { Id = match.Id, Name = match.Name };
    }
}
