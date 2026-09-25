using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Http;
using Rundfrage.Api.Security;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Forms;

/// <summary>A refusal carrying the machine-readable code from the contract.</summary>
public sealed record FormError(string Code, int? Limit = null, string? Detail = null);

/// <summary>A field as it arrives from the client when it is first added (010 FR-003 to FR-006).</summary>
public sealed record FormFieldDraft(
    FieldType? Type, string? Label, bool Required, int? MaxLength, int? MinLength);

/// <summary>Creating, editing and deleting forms and their fields (010 FR-001 to FR-011a, FR-041 to FR-042a).</summary>
/// <remarks>
/// Deliberately operator-only: no <c>CreatorId</c> parameter anywhere here, unlike
/// <c>PollService</c> and <c>WishListService</c> (010 FR-032, research.md R-3).
/// </remarks>
public sealed class FormService(RundfrageDbContext db, BerlinClock clock, ILogger<FormService> logger)
{
    /// <summary>
    /// Pure, so FR-001's title rule is testable without a database and so there is exactly one
    /// place where "enforced on the server" is true (mirrors <c>WishListService.ValidateList</c>).
    /// </summary>
    public static FormError? ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new FormError(ErrorCodes.TitleRequired);
        }

        return title.Length > Form.TitleMaxLength
            ? new FormError(ErrorCodes.TitleTooLong, Form.TitleMaxLength)
            : null;
    }

    /// <summary>
    /// FR-003 to FR-006: a field's type, label and - for Text - its length limits. Fresh addition
    /// only; editing an existing field goes through <see cref="ValidateFieldEdit"/>, which never
    /// touches type (010 FR-008).
    /// </summary>
    public static FormError? ValidateNewField(FormFieldDraft draft)
    {
        if (draft.Type is null)
        {
            return new FormError(ErrorCodes.FieldTypeInvalid);
        }

        var labelError = ValidateLabel(draft.Label);
        if (labelError is not null)
        {
            return labelError;
        }

        return ValidateTextLengths(draft.Type.Value, draft.MaxLength, draft.MinLength, isNew: true);
    }

    /// <summary>
    /// FR-008: label, required flag and - for Text - length limits, changeable at any time. Type
    /// is never a parameter here; there is no code path that writes a new one onto an existing
    /// field (spec clarification 2026-09-22).
    /// </summary>
    public static FormError? ValidateFieldEdit(
        FieldType currentType, string? label, int? maxLength, int? minLength)
    {
        if (label is not null)
        {
            var labelError = ValidateLabel(label);
            if (labelError is not null)
            {
                return labelError;
            }
        }

        return ValidateTextLengths(currentType, maxLength, minLength, isNew: false);
    }

    private static FormError? ValidateLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return new FormError(ErrorCodes.LabelRequired);
        }

        return label.Length > FormField.LabelMaxLength
            ? new FormError(ErrorCodes.LabelTooLong, FormField.LabelMaxLength)
            : null;
    }

    /// <summary>
    /// FR-006: a Text field must have a maximum length, within the system's bound, and a minimum
    /// (when given) must not exceed it. Every other type must carry neither.
    /// </summary>
    private static FormError? ValidateTextLengths(
        FieldType type, int? maxLength, int? minLength, bool isNew)
    {
        if (type != FieldType.Text)
        {
            return null;
        }

        // A brand-new Text field must be given a maximum length; an edit that does not mention
        // it leaves the existing one alone; whichever value is in play now, it must be sane.
        if (isNew && maxLength is null)
        {
            return new FormError(ErrorCodes.TextMaxLengthMissing);
        }

        if (maxLength is { } max && (max < 1 || max > FormField.TextLengthCeiling))
        {
            return new FormError(ErrorCodes.TextLengthRangeInvalid);
        }

        if (minLength is { } min && maxLength is { } effectiveMax && min > effectiveMax)
        {
            return new FormError(ErrorCodes.TextLengthRangeInvalid);
        }

        return null;
    }

    /// <summary>FR-001: an empty form. FR-011 keeps its link unreachable until a field exists.</summary>
    public async Task<Form> CreateAsync(string title, CancellationToken ct)
    {
        var form = new Form
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            FormToken = CapabilityToken.Mint(),
            CreatedAt = clock.Now,
        };

        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);

        // Identifiers and counts only - never a title (Principle IV).
        logger.LogInformation("Form created {FormId}", form.Id);

        return form;
    }

    /// <summary>A form with its fields in display order, or null. The only way in for the admin area.</summary>
    public Task<Form?> FindAsync(Guid id, CancellationToken ct) =>
        db.Forms
            .Include(f => f.Fields.OrderBy(field => field.DisplayOrder))
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    /// <summary>
    /// A form's response count without loading the responses themselves - at the documented
    /// scale of up to 200,000 responses installation-wide (FR-047a), <c>FindAsync</c>'s
    /// <see cref="Form.Responses"/> navigation is deliberately never included, so any caller that
    /// needs the count asks here instead of reading <c>form.Responses.Count</c> against an empty,
    /// un-loaded collection.
    /// </summary>
    public Task<int> CountResponsesAsync(Guid formId, CancellationToken ct) =>
        db.FormResponses.CountAsync(r => r.FormId == formId, ct);

    /// <summary>Every response to a form, oldest first, with its raw stored values (010 FR-040).</summary>
    public async Task<IReadOnlyList<FormResponseSummary>> ListResponsesAsync(
        Guid formId, CancellationToken ct)
    {
        var responses = await db.FormResponses
            .Where(r => r.FormId == formId)
            .OrderBy(r => r.SubmittedAt)
            .Select(r => new
            {
                r.Id,
                r.SubmittedAt,
                Values = r.Values.Select(v => new { v.FieldId, v.Value }).ToList(),
            })
            .ToListAsync(ct);

        return responses
            .Select(r => new FormResponseSummary(
                r.Id,
                r.SubmittedAt,
                r.Values.Select(v => new FormFieldValueView(v.FieldId, v.Value)).ToList()))
            .ToList();
    }

    /// <summary>Every form, newest first, with its field and response counts (010 FR-040).</summary>
    public async Task<IReadOnlyList<FormSummary>> ListAsync(CancellationToken ct)
    {
        return await db.Forms
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FormSummary(
                f.Id,
                f.Title,
                f.CreatedAt,
                f.Fields.Count,
                f.Responses.Count,
                f.FormToken))
            .ToListAsync(ct);
    }

    /// <summary>FR-041: the title only. Link, fields and responses are untouched.</summary>
    public async Task<(Form? Form, FormError? Error)> RenameAsync(
        Form form, string? title, CancellationToken ct)
    {
        var error = ValidateTitle(title);
        if (error is not null)
        {
            return (null, error);
        }

        form.Title = title!.Trim();
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Form renamed {FormId}", form.Id);

        return (form, null);
    }

    /// <summary>FR-042: the whole thing, cascading to its fields, responses and their values.</summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var removed = await db.Forms.Where(f => f.Id == id).ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            logger.LogInformation("Form deleted {FormId}", id);
        }

        return removed > 0;
    }

    /// <summary>FR-042a: one response only. The form, its fields and every other response are untouched.</summary>
    public async Task<bool> DeleteResponseAsync(Guid formId, Guid responseId, CancellationToken ct)
    {
        var removed = await db.FormResponses
            .Where(r => r.Id == responseId && r.FormId == formId)
            .ExecuteDeleteAsync(ct);

        if (removed > 0)
        {
            logger.LogInformation("Form response deleted {ResponseId} from form {FormId}", responseId, formId);
        }

        return removed > 0;
    }

    /// <summary>
    /// FR-003 to FR-006, FR-011a: appends a field, refusing past the 50-field cap.
    /// </summary>
    /// <remarks>
    /// The cap is checked inside an immediate write transaction, re-read from the database rather
    /// than from <paramref name="form"/>.Fields - that navigation collection was loaded before
    /// this call and two concurrent requests reading the same stale count could otherwise both
    /// see 49 and both succeed, taking the form to 51 (mirrors CreatorService.CreateAsync's cap
    /// check against 009 FR-009).
    /// </remarks>
    public async Task<(FormField? Field, FormError? Error)> AddFieldAsync(
        Form form, FormFieldDraft draft, CancellationToken ct)
    {
        var error = ValidateNewField(draft);
        if (error is not null)
        {
            return (null, error);
        }

        await using var transaction = await SqliteWriteTransaction.BeginAsync(db, ct);

        var existing = await db.FormFields.CountAsync(f => f.FormId == form.Id, ct);
        if (existing >= Form.MaxFields)
        {
            await transaction.RollbackAsync(ct);
            return (null, new FormError(ErrorCodes.FieldLimitReached, Form.MaxFields));
        }

        var maxOrder = await db.FormFields
            .Where(f => f.FormId == form.Id)
            .Select(f => (int?)f.DisplayOrder)
            .MaxAsync(ct);

        var field = new FormField
        {
            Id = Guid.CreateVersion7(),
            FormId = form.Id,
            Type = draft.Type!.Value,
            Label = draft.Label!.Trim(),
            Required = draft.Required,
            MaxLength = draft.Type == FieldType.Text ? draft.MaxLength : null,
            MinLength = draft.Type == FieldType.Text ? draft.MinLength : null,
            // Appended, so the order stays the one the operator built (010 FR-010).
            DisplayOrder = maxOrder is { } order ? order + 1 : 0,
        };

        db.FormFields.Add(field);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Form field added {FieldId} to form {FormId}", field.Id, form.Id);

        return (field, null);
    }

    /// <summary>
    /// FR-008: label, required flag and - for Text - length limits, at any time, including after
    /// the form has collected responses. Type is never touched (spec clarification 2026-09-22).
    /// </summary>
    /// <remarks>
    /// <paramref name="minLengthGiven"/> is what makes clearing a minimum possible: without it,
    /// an explicit <c>minLength: null</c> and an omitted property both arrive here as
    /// <paramref name="minLength"/> being <c>null</c>, and the field's existing minimum could
    /// never be cleared once set (mirrors <c>WishListAdminEndpoints</c>'s own
    /// <c>descriptionGiven</c>). <c>maxLength</c> needs no equivalent: it is required for a Text
    /// field for as long as the field exists, so there is no valid "cleared" state for it to
    /// reach - an edit that does not mention it always means "leave it as it is".
    /// </remarks>
    public async Task<(FormField? Field, FormError? Error)> UpdateFieldAsync(
        Form form, Guid fieldId, string? label, bool? required, int? maxLength, int? minLength,
        bool minLengthGiven, CancellationToken ct)
    {
        var field = form.Fields.FirstOrDefault(f => f.Id == fieldId);
        if (field is null)
        {
            return (null, new FormError(ErrorCodes.FieldSetMismatch));
        }

        var newMaxLength = maxLength ?? field.MaxLength;
        var newMinLength = minLengthGiven ? minLength : field.MinLength;

        var error = ValidateFieldEdit(field.Type, label, newMaxLength, newMinLength);
        if (error is not null)
        {
            return (null, error);
        }

        if (label is not null)
        {
            field.Label = label.Trim();
        }

        if (required is not null)
        {
            field.Required = required.Value;
        }

        if (field.Type == FieldType.Text)
        {
            field.MaxLength = newMaxLength;
            field.MinLength = newMinLength;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Form field updated {FieldId}", fieldId);

        return (field, null);
    }

    /// <summary>
    /// FR-009: removes a field and cascades to every value collected for it, then renumbers the
    /// remainder so DisplayOrder stays dense.
    /// </summary>
    /// <remarks>
    /// The delete and the renumber share one immediate write transaction, for the same reason
    /// <see cref="AddFieldAsync"/>'s cap check does: two concurrent removals on the same form
    /// would otherwise each read-then-rewrite DisplayOrder 0..N-1 from independent, possibly
    /// interleaved snapshots, leaving it non-dense or duplicated.
    /// </remarks>
    public async Task<bool> RemoveFieldAsync(Guid formId, Guid fieldId, CancellationToken ct)
    {
        await using var transaction = await SqliteWriteTransaction.BeginAsync(db, ct);

        var removed = await db.FormFields
            .Where(f => f.Id == fieldId && f.FormId == formId)
            .ExecuteDeleteAsync(ct);

        if (removed == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        await RenumberAsync(formId, ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Form field removed {FieldId} from form {FormId}", fieldId, formId);

        return true;
    }

    /// <summary>
    /// FR-007: replaces the field order from a dragged (or button-reordered) sequence. The
    /// request must name exactly the form's current fields, once each (010 research.md R-4).
    /// </summary>
    public async Task<(Form? Form, FormError? Error)> ReorderFieldsAsync(
        Form form, IReadOnlyList<Guid> fieldIds, CancellationToken ct)
    {
        var current = form.Fields.Select(f => f.Id).ToHashSet();

        if (fieldIds.Count != current.Count || !current.SetEquals(fieldIds))
        {
            return (null, new FormError(ErrorCodes.FieldSetMismatch));
        }

        for (var position = 0; position < fieldIds.Count; position++)
        {
            form.Fields.First(f => f.Id == fieldIds[position]).DisplayOrder = position;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Form fields reordered {FormId}", form.Id);

        return (form, null);
    }

    /// <summary>Keeps DisplayOrder dense (0..N-1) after a field is removed (010 research.md R-4).</summary>
    private async Task RenumberAsync(Guid formId, CancellationToken ct)
    {
        var remaining = await db.FormFields
            .Where(f => f.FormId == formId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(ct);

        for (var position = 0; position < remaining.Count; position++)
        {
            remaining[position].DisplayOrder = position;
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>One row of the forms area's list (010 FR-040).</summary>
public sealed record FormSummary(
    Guid Id, string Title, DateTime CreatedAt, int FieldCount, int ResponseCount, string FormToken);

/// <summary>One raw field value within one response, keyed by field id (010 FR-040).</summary>
public sealed record FormFieldValueView(Guid FieldId, string Value);

/// <summary>One response, as the operator sees it in the responses panel (010 FR-040).</summary>
public sealed record FormResponseSummary(
    Guid Id, DateTime SubmittedAt, IReadOnlyList<FormFieldValueView> Values);
