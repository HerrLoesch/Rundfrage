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
}
