using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rundfrage.Api.Data;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Forms;
using Rundfrage.Api.Http;
using Rundfrage.Api.Time;

namespace Rundfrage.Api.Endpoints.Public;

/// <summary>
/// Everything a participant reaches on a form. No session, no account - the token in the path is
/// the whole of the authorisation (Principle I, 010 FR-012).
/// </summary>
/// <remarks>
/// Beside the other participant routes and NOT inside the admin group, so
/// <c>MaintenanceMiddleware</c> gates it (010 FR-045, research R-8).
/// <para>
/// A form with zero fields is refused exactly like an unknown token (010 FR-011, FR-021): there
/// is no third, distinguishable state for "exists but nothing to answer yet".
/// </para>
/// </remarks>
public static class FormEndpoints
{
    public sealed record SubmittedValue(Guid FieldId, JsonElement Value);

    public sealed record SubmissionRequest(SubmittedValue[]? Values);

    public static IEndpointRouteBuilder MapFormEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/f/{formToken}", async (
            string formToken, RundfrageDbContext db, CancellationToken ct) =>
        {
            var form = await LoadAsync(db, formToken, ct);

            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            return Results.Ok(new
            {
                title = form.Title,
                fields = form.Fields.OrderBy(f => f.DisplayOrder).Select(FormFieldView.Of),
            });
        })
        .AllowAnonymous()
        .WithName("getPublicForm");

        routes.MapPost("/f/{formToken}/responses", async (
            string formToken,
            SubmissionRequest request,
            RundfrageDbContext db,
            BerlinClock clock,
            CancellationToken ct) =>
        {
            var values = request.Values ?? [];

            // One value per field. A second entry for the same field has no meaning to pick
            // between, and ToDictionary below would otherwise throw and answer 500.
            if (values.Select(v => v.FieldId).Distinct().Count() != values.Length)
            {
                return Refusal.BadRequest(ErrorCodes.MalformedRequest);
            }

            // The form is read inside the write lock, so the field list validated here is the one
            // the values are stored against: an operator removing a field between the read and
            // the insert would otherwise turn a valid submission into a foreign-key failure.
            await using var transaction = await SqliteWriteTransaction.BeginAsync(db, ct);

            var form = await LoadAsync(db, formToken, ct);

            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            var submitted = values.ToDictionary(v => v.FieldId, v => Canonicalize(v.Value));

            var fieldErrors = new List<object>();

            foreach (var field in form.Fields)
            {
                var value = submitted.GetValueOrDefault(field.Id);

                if (!FormFieldValidation.IsAnswered(value))
                {
                    // FR-017: a required field with no value at all is refused. An optional field
                    // left unanswered is fine for every type, Boolean included (research R-13).
                    if (field.Required)
                    {
                        fieldErrors.Add(new { fieldId = field.Id, error = ErrorCodes.FieldRequired });
                    }

                    continue;
                }

                if (!FormFieldValidation.IsValid(field, value))
                {
                    fieldErrors.Add(new { fieldId = field.Id, error = ErrorCodes.FieldInvalidFormat });
                }
            }

            if (fieldErrors.Count > 0)
            {
                // FR-018: nothing is stored when any field fails.
                return Results.BadRequest(new { code = ErrorCodes.SubmissionInvalid, fields = fieldErrors });
            }

            var response = new FormResponse
            {
                Id = Guid.CreateVersion7(),
                FormId = form.Id,
                SubmittedAt = clock.Now,
                Values = [.. form.Fields
                    .Where(f => FormFieldValidation.IsAnswered(submitted.GetValueOrDefault(f.Id)))
                    .Select(f => new FormFieldValue
                    {
                        Id = Guid.CreateVersion7(),
                        FieldId = f.Id,
                        Value = submitted[f.Id]!,
                    })],
            };

            db.FormResponses.Add(response);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Results.Created($"/api/v1/f/{formToken}/responses/{response.Id}", new { id = response.Id });
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimiting.SubmissionPolicy)
        .WithName("submitFormResponse");

        return routes;
    }

    /// <summary>
    /// The canonical, culture-invariant text form of a submitted JSON value (010 data-model.md
    /// §5). Returns null for a JSON shape no field value may take (an array, an object, or
    /// explicit JSON null), which the caller treats as "not answered".
    /// </summary>
    private static string? Canonicalize(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        // 010 FR-026: the canonical stored value is "yes"/"no", matching the wording of the
        // requirement and this codebase's existing wire vocabulary for answer-shaped booleans
        // (PollResponse/DayAnswer already use "yes"/"maybe"/"no"). A JSON boolean is accepted for
        // convenience (the contract's oneOf: string, number, boolean) and mapped here; a literal
        // JSON string "true"/"false" is correctly rejected by FormFieldValidation as not one of
        // "yes" or "no".
        JsonValueKind.True => "yes",
        JsonValueKind.False => "no",
        _ => null,
    };

    /// <summary>
    /// The one way in from a participant token. No retention filter: a form stays reachable until
    /// the operator deletes it (010 FR-043). A form with zero fields is treated as absent here -
    /// the one place that decides it - so both callers stay in agreement by construction rather
    /// than by repeating the same compound condition (010 FR-011, FR-021).
    /// </summary>
    private static async Task<Form?> LoadAsync(
        RundfrageDbContext db, string formToken, CancellationToken ct)
    {
        var form = await db.Forms
            .Include(f => f.Fields.OrderBy(field => field.DisplayOrder))
            .FirstOrDefaultAsync(f => f.FormToken == formToken, ct);

        return form is null || form.Fields.Count == 0 ? null : form;
    }
}
