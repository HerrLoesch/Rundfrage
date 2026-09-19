namespace Rundfrage.Api.Http;

/// <summary>
/// The machine-readable refusal codes the import and restore routes return.
/// </summary>
/// <remarks>
/// FR-006 requires a refusal to name its defect rather than report one generic failure. Named
/// constants rather than string literals at each call site, because the interface renders these
/// into German and a code that exists in only one of the two places is a blank message in front
/// of an operator who is already having a bad day.
/// </remarks>
public static class ErrorCodes
{
    /// <summary>The upload is not a poll export at all - not JSON, or not this document.</summary>
    public const string NotAValidExport = "not_a_valid_export";

    /// <summary>Refused rather than interpreted: a raised version means a field changed meaning.</summary>
    public const string FormatVersionTooNew = "format_version_too_new";

    /// <summary>The document exceeds a limit that makes the poll uncreatable (FR-014).</summary>
    public const string PollTooLarge = "poll_too_large";

    /// <summary>The upload is not a backup this system can restore (FR-019).</summary>
    public const string NotABackup = "not_a_backup";

    /// <summary>A restore was attempted while maintenance mode is off (FR-024).</summary>
    public const string MaintenanceRequired = "maintenance_required";

    /// <summary>The storage could not be taken exclusively (research R-2).</summary>
    public const string StorageLocked = "storage_locked";

    /// <summary>A restore arrived without the operator's confirmation (FR-018).</summary>
    public const string ConfirmationRequired = "confirmation_required";

    /// <summary>
    /// The request body is valid JSON but not the document the route expects - a patch that is
    /// <c>null</c>, an array, or a property of the right name and the wrong type.
    /// </summary>
    /// <remarks>
    /// Its own code rather than an unhandled exception. A route that answers 500 to a defective
    /// request tells the caller nothing about what to fix, and tells the log something alarming
    /// that is not true.
    /// </remarks>
    public const string MalformedRequest = "malformed_request";

    // Reused from feature 002's vocabulary rather than spelled a second way. The interface
    // renders one German string per code, and two codes meaning "no title" would be two.

    /// <summary>No title was given (002 FR-008, 008 FR-001).</summary>
    public const string TitleRequired = "title_required";

    /// <summary>The title exceeds its limit (002 FR-015, 008 FR-010).</summary>
    public const string TitleTooLong = "title_too_long";

    /// <summary>No display name was given (002 FR-022, 008 FR-015).</summary>
    public const string DisplayNameRequired = "display_name_required";

    /// <summary>The display name exceeds its limit (002 FR-015, 008 FR-010).</summary>
    public const string DisplayNameTooLong = "display_name_too_long";

    // --- Wish lists (feature 008) ----------------------------------------------------------
    // Operator-side refusals. Every one of these is raised where the operator works, so the
    // number that makes the refusal actionable travels with it (008 FR-010a, FR-033).

    /// <summary>No target date was given (008 FR-002).</summary>
    public const string TargetDateRequired = "target_date_required";

    /// <summary>The description exceeds its limit (008 FR-010).</summary>
    public const string DescriptionTooLong = "description_too_long";

    /// <summary>A wish list was created without a single item (008 FR-004).</summary>
    public const string ItemsRequired = "items_required";

    /// <summary>An item was given no name (008 FR-005).</summary>
    public const string ItemNameRequired = "item_name_required";

    /// <summary>An item name exceeds its limit (008 FR-010).</summary>
    public const string ItemNameTooLong = "item_name_too_long";

    /// <summary>Two items in one list would carry the same name (008 FR-008).</summary>
    public const string DuplicateItemName = "duplicate_item_name";

    /// <summary>A wanted count below 1 or above the maximum (008 FR-007).</summary>
    public const string WantedCountInvalid = "wanted_count_invalid";

    /// <summary>More items than a list may hold (008 FR-010).</summary>
    public const string TooManyItems = "too_many_items";

    /// <summary>
    /// The wished places would exceed the list's capacity. Carries how many remain, because a
    /// limit the operator cannot see is a refusal nobody can act on (008 FR-010a).
    /// </summary>
    public const string TooManyPlaces = "too_many_places";

    /// <summary>
    /// A wanted count was lowered below the claims already made. Carries that number (008 FR-033).
    /// </summary>
    public const string CountBelowEntries = "count_below_entries";

    // Participant-side refusals. Exactly three, and no more: capacity, the closed list, and the
    // rate limit. Nothing list-wide may ever refuse a participant (008 FR-010a).

    /// <summary>A claim naming an item that belongs to another list (008 FR-017).</summary>
    public const string UnknownItem = "unknown_item";

    /// <summary>Every place of the item is taken (008 FR-017, FR-017a).</summary>
    public const string ItemFull = "item_full";

    /// <summary>The target day has ended, so nothing may be claimed or withdrawn (008 FR-028a).</summary>
    public const string WishListClosed = "wish_list_closed";

    // --- Ersteller (feature 009) -----------------------------------------------------------
    // Operator-side only. An Ersteller is created, renamed and deleted by the operator, so every
    // refusal here is raised where the operator works and carries what makes it actionable
    // (009 FR-001, FR-002, FR-009).

    /// <summary>An Ersteller was created or renamed without a name (009 FR-001).</summary>
    public const string CreatorNameRequired = "creator_name_required";

    /// <summary>The name exceeds its limit of 100 characters (009 FR-002).</summary>
    public const string CreatorNameTooLong = "creator_name_too_long";

    /// <summary>
    /// The name is already taken. Carries the colliding name in <c>detail</c>, because the
    /// operator cannot act on a collision they cannot see (009 FR-002).
    /// </summary>
    /// <remarks>
    /// Revoked Ersteller count: a revoked one still exists and still owns content, so its name is
    /// still taken. Only deleting an Ersteller releases its name (009 FR-002, FR-020).
    /// </remarks>
    public const string CreatorNameDuplicate = "creator_name_duplicate";

    /// <summary>
    /// A hundred Ersteller already exist. Carries the limit, and the interface adds the sentence
    /// that matters more than the number: a place is freed by <i>deleting</i> an Ersteller, not by
    /// revoking one (009 FR-009, FR-009a).
    /// </summary>
    public const string CreatorLimitReached = "creator_limit_reached";
}
