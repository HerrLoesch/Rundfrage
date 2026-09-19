using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Retention;

namespace Rundfrage.Api.Creators;

/// <summary>
/// The only way a creator request reaches data (009 FR-032, FR-033, research R-1).
/// </summary>
/// <remarks>
/// <b>Built the way <see cref="RetentionService.LivePolls"/> is built, and for the same reason.</b>
/// That method is documented as "the only way in" because 002 FR-039b is a statement about every
/// read, and a filter each caller remembers to apply is a filter somebody eventually forgets.
/// 009 FR-033 makes the same demand of ownership: scoping must be enforced where the data is read,
/// not by filtering a full result for display.
/// <para>
/// <b>What makes that structural rather than a review comment: creator handlers are not given
/// <see cref="RundfrageDbContext"/> at all.</b> There is no unscoped queryable within their reach,
/// so a handler that forgets the filter has nothing to call. A leak would have to be written
/// deliberately.
/// </para>
/// <para>
/// Two compositions, and each one is load-bearing:
/// </para>
/// <list type="bullet">
/// <item><b>Polls compose with the retention filter.</b> 009 FR-014 says ownership changes nothing
/// about retention, so an expired poll must be invisible to its Ersteller exactly as it is to the
/// operator. Writing <c>db.Polls.Where(...)</c> here would have made an Ersteller the only person
/// in the system who can still see their own expired poll.</item>
/// <item><b>Wish lists compose with nothing.</b> There is no retention filter for them and there
/// must not be one - a wish list stays reachable until somebody deletes it (008 FR-037), which is
/// the note <c>WishEndpoints.LoadAsync</c> already carries.</item>
/// </list>
/// <para>
/// Scoped per request. <see cref="Bind"/> is called once by the creator group's endpoint filter,
/// after the token has resolved; reaching <see cref="Owner"/> before that is a programming error
/// rather than an authorisation failure, and throws rather than returning something permissive.
/// </para>
/// </remarks>
public sealed class OwnerScope(RundfrageDbContext db, RetentionService retention)
{
    private Creator? _owner;

    /// <summary>The Ersteller this request belongs to.</summary>
    public Creator Owner =>
        _owner ?? throw new InvalidOperationException(
            "No Ersteller is bound to this request. OwnerScope is reachable only from the creator "
            + "route group, whose endpoint filter binds it after resolving the token.");

    public void Bind(Creator owner) => _owner = owner;

    /// <summary>
    /// This Ersteller's polls, and no others. Composed with the retention filter, so ownership
    /// changes nothing about when a poll stops being reachable (FR-014).
    /// </summary>
    public IQueryable<Poll> Polls()
    {
        // Read into a local first, deliberately. Left as `Owner.Id` inside the lambda it becomes
        // part of the expression tree, so an unbound scope would not complain here but somewhere
        // deep inside EF Core at enumeration - a stack trace pointing at the wrong thing, for a
        // mistake that is always at the call site.
        var owner = Owner.Id;
        return retention.LivePolls().Where(p => p.CreatorId == owner);
    }

    /// <summary>
    /// This Ersteller's wish lists, and no others. Deliberately unfiltered by retention: nothing
    /// expires a wish list (008 FR-037).
    /// </summary>
    public IQueryable<WishList> WishLists()
    {
        var owner = Owner.Id;
        return db.WishLists.Where(l => l.CreatorId == owner);
    }
}
