using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Security;

namespace Rundfrage.Api.Data;

/// <summary>
/// Feature 001 left this deliberately empty; feature 002 gives it its first real entities.
/// </summary>
/// <remarks>
/// Still configured without <c>EnableRetryOnFailure</c>. An execution strategy with retries
/// would multiply the connectivity probe's 2-second budget (feature 001, FR-012); retry lives
/// explicitly around the startup migration instead.
/// </remarks>
public sealed class RundfrageDbContext(DbContextOptions<RundfrageDbContext> options)
    : DbContext(options)
{
    public DbSet<Poll> Polls => Set<Poll>();

    public DbSet<CandidateDay> CandidateDays => Set<CandidateDay>();

    public DbSet<PollResponse> Responses => Set<PollResponse>();

    public DbSet<DayAnswer> DayAnswers => Set<DayAnswer>();

    public DbSet<WishList> WishLists => Set<WishList>();

    public DbSet<WishItem> WishItems => Set<WishItem>();

    public DbSet<WishClaim> WishClaims => Set<WishClaim>();

    public DbSet<Creator> Creators => Set<Creator>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Poll>(poll =>
        {
            poll.HasKey(p => p.Id);
            poll.Property(p => p.Title).HasMaxLength(Poll.TitleMaxLength).IsRequired();
            poll.Property(p => p.Message).HasMaxLength(Poll.MessageMaxLength);
            poll.Property(p => p.ParticipantToken)
                .HasMaxLength(CapabilityToken.TokenLength)
                .IsRequired();

            // The token is the lookup key on every participant request, so it is indexed and
            // unique rather than merely stored.
            poll.HasIndex(p => p.ParticipantToken).IsUnique();

            // Drives both the access filter and the erasure sweep (FR-039b, FR-039c).
            poll.HasIndex(p => p.RetentionDeadline);

            poll.HasMany(p => p.Days)
                .WithOne(d => d.Poll!)
                .HasForeignKey(d => d.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            poll.HasMany(p => p.Responses)
                .WithOne(r => r.Poll!)
                .HasForeignKey(r => r.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CandidateDay>(day =>
        {
            day.HasKey(d => d.Id);

            // FR-012: a day selected twice is stored once. Enforced by the database, not only by
            // the code that happens to de-duplicate before inserting.
            day.HasIndex(d => new { d.PollId, d.Date }).IsUnique();
        });

        builder.Entity<PollResponse>(response =>
        {
            response.HasKey(r => r.Id);
            response.Property(r => r.DisplayName)
                .HasMaxLength(PollResponse.DisplayNameMaxLength)
                .IsRequired();
            response.Property(r => r.EditToken)
                .HasMaxLength(CapabilityToken.TokenLength)
                .IsRequired();

            response.HasIndex(r => r.EditToken).IsUnique();

            // Deliberately no index on DisplayName, and no uniqueness: it is a label, and two
            // participants may legitimately use the same one (FR-022).

            response.HasMany(r => r.Answers)
                .WithOne(a => a.Response!)
                .HasForeignKey(a => a.ResponseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Wish lists (feature 008) ----------------------------------------------------
        builder.Entity<WishList>(list =>
        {
            list.HasKey(l => l.Id);
            list.Property(l => l.Title).HasMaxLength(WishList.TitleMaxLength).IsRequired();
            list.Property(l => l.Description).HasMaxLength(WishList.DescriptionMaxLength);
            list.Property(l => l.ListToken)
                .HasMaxLength(CapabilityToken.TokenLength)
                .IsRequired();

            // The token is the lookup key on every participant request (008 FR-012).
            list.HasIndex(l => l.ListToken).IsUnique();

            // The overview orders by it, open lists first and nearest date on top (008 FR-048c).
            // There is deliberately no index on "closed", because there is no such column: it is
            // derived from this date and the clock (008 FR-028c).
            list.HasIndex(l => l.TargetDate);

            list.HasMany(l => l.Items)
                .WithOne(i => i.WishList!)
                .HasForeignKey(i => i.WishListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WishItem>(item =>
        {
            item.HasKey(i => i.Id);

            // NOCASE, so the index below agrees with the service about what "the same name" is.
            // WishListService compares with OrdinalIgnoreCase - a BINARY column would accept
            // "Kuchen" beside "kuchen" and the index would be backstopping a rule it does not
            // share. The two still differ beyond ASCII: SQLite's NOCASE folds A-Z only, so
            // "Äpfel"/"äpfel" is caught by the service and not by the index. That is the service
            // being stricter than the index, which is the safe direction.
            item.Property(i => i.Name)
                .HasMaxLength(WishItem.NameMaxLength)
                .UseCollation("NOCASE")
                .IsRequired();

            // 008 FR-008: two items of one list may not share a name. Enforced here rather than
            // only in the service, so a second write path cannot quietly create the duplicate.
            item.HasIndex(i => new { i.WishListId, i.Name }).IsUnique();

            item.HasMany(i => i.Claims)
                .WithOne(c => c.WishItem!)
                .HasForeignKey(c => c.WishItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WishClaim>(claim =>
        {
            claim.HasKey(c => c.Id);
            claim.Property(c => c.DisplayName)
                .HasMaxLength(WishClaim.DisplayNameMaxLength)
                .IsRequired();
            claim.Property(c => c.ClaimToken)
                .HasMaxLength(CapabilityToken.TokenLength)
                .IsRequired();

            // Indexed but NOT unique: every claim of one submission carries the same token, and
            // that sharing is what the personal link resolves through (008 FR-022b).
            claim.HasIndex(c => c.ClaimToken);

            // Deliberately no index on DisplayName, and no uniqueness: it is a label, and two
            // people may legitimately bring the same thing under the same name (008 FR-020) -
            // the same decision PollResponse.DisplayName records.
        });

        // --- Ersteller (feature 009) -----------------------------------------------------
        builder.Entity<Creator>(creator =>
        {
            creator.HasKey(c => c.Id);

            // NOCASE for the same reason WishItem.Name carries it: the index must agree with the
            // service about what "the same name" is. The service compares with OrdinalIgnoreCase,
            // so beyond ASCII the service is the stricter of the two - the safe direction.
            creator.Property(c => c.Name)
                .HasMaxLength(Creator.NameMaxLength)
                .UseCollation("NOCASE")
                .IsRequired();

            // 009 FR-002, enforced here rather than only in the service, so a second write path
            // cannot quietly create the duplicate.
            creator.HasIndex(c => c.Name).IsUnique();

            creator.Property(c => c.LinkToken).HasMaxLength(CapabilityToken.TokenLength);

            // The lookup key on every creator request (009 FR-004). Nullable, because its absence
            // is what "revoked" means (009 FR-020, research R-3) - and unique anyway, because
            // SQLite treats NULLs in a unique index as distinct, so any number of revoked
            // Ersteller coexist. SchemaCreationTests asserts that rather than trusting it.
            creator.HasIndex(c => c.LinkToken).IsUnique();

            // Cascade, stated EXPLICITLY and not left to the default.
            //
            // EF Core's default for an *optional* relationship is ClientSetNull. Left at it,
            // deleting an Ersteller would set CreatorId = NULL on everything it owned - which is
            // to say it would hand that content to the operator, the exact outcome 009 FR-020c
            // forbids and the spec's first clarification rejected. It would also fail silently:
            // nothing would error, and the content would simply change hands (research R-4).
            creator.HasMany(c => c.Polls)
                .WithOne(p => p.Creator!)
                .HasForeignKey(p => p.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            creator.HasMany(c => c.WishLists)
                .WithOne(l => l.Creator!)
                .HasForeignKey(l => l.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // The owner filter of OwnerScope is the hot path on every creator request, and this
        // equality is its only new predicate (009 research R-14).
        builder.Entity<Poll>().HasIndex(p => p.CreatorId);
        builder.Entity<WishList>().HasIndex(l => l.CreatorId);

        builder.Entity<DayAnswer>(answer =>
        {
            // One answer per response per day - and only for days actually answered.
            answer.HasKey(a => new { a.ResponseId, a.CandidateDayId });

            answer.HasOne(a => a.CandidateDay!)
                  .WithMany(d => d.Answers)
                  .HasForeignKey(a => a.CandidateDayId)
                  .OnDelete(DeleteBehavior.Cascade);

            answer.Property(a => a.Availability).HasConversion<int>();
        });
    }
}
