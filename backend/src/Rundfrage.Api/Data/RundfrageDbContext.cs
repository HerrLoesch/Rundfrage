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
