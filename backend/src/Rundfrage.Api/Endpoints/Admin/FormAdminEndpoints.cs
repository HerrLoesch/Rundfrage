using System.Text.Json;
using System.Text.Json.Serialization;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Forms;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.Endpoints.Admin;

/// <summary>
/// Building, editing, exporting and deleting forms (010 FR-001 to FR-011a, FR-034 to FR-042a).
/// </summary>
/// <remarks>
/// The session requirement is inherited: the admin group carries RequireAuthorization(), so no
/// route here repeats it (002 FR-048). Mounted under /api/v1/admin deliberately - unlike feature
/// 009's creator surface, this feature is operator-only and has no bearer token to keep separate
/// from the maintenance-mode exemption that group carries (010 research.md R-8).
/// <para>
/// A miss answers the same neutral payload the participant routes use (010 FR-033).
/// </para>
/// </remarks>
public static class FormAdminEndpoints
{
    public sealed record CreateFormRequest(string? Title);

    public sealed record RenameFormRequest(string? Title);

    public sealed record FieldRequest(
        FieldType? Type, string? Label, bool Required, int? MaxLength, int? MinLength);

    public sealed record FieldPatch(string? Label, bool? Required, int? MaxLength, int? MinLength);

    public sealed record ReorderRequest(Guid[]? FieldIds);

    /// <summary>
    /// <paramref name="responseCount"/> is always given explicitly rather than read from
    /// <c>form.Responses.Count</c>: <c>FindAsync</c> deliberately never includes that navigation
    /// (it would mean loading every response just to count them, at a documented scale of up to
    /// 200,000), so the collection is always empty here and every caller must supply the real
    /// count from <see cref="FormService.CountResponsesAsync"/>.
    /// </summary>
    private static object ToDetail(Form form, int responseCount) => new
    {
        id = form.Id,
        title = form.Title,
        createdAt = form.CreatedAt,
        fieldCount = form.Fields.Count,
        responseCount,
        formToken = form.FormToken,
        fields = form.Fields.OrderBy(f => f.DisplayOrder).Select(ToFieldDetail),
    };

    private static object ToFieldDetail(FormField field) => new
    {
        id = field.Id,
        type = field.Type,
        label = field.Label,
        required = field.Required,
        maxLength = field.MaxLength,
        minLength = field.MinLength,
        displayOrder = field.DisplayOrder,
    };

    private static object ToSummary(FormSummary summary) => new
    {
        id = summary.Id,
        title = summary.Title,
        createdAt = summary.CreatedAt,
        fieldCount = summary.FieldCount,
        responseCount = summary.ResponseCount,
        formToken = summary.FormToken,
    };

    public static IEndpointRouteBuilder MapFormAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/forms", async (
            CreateFormRequest request, FormService forms, CancellationToken ct) =>
        {
            var error = FormService.ValidateTitle(request.Title);
            if (error is not null)
            {
                return Results.BadRequest(new { code = error.Code, limit = error.Limit });
            }

            var form = await forms.CreateAsync(request.Title!, ct);
            // A freshly created form has no responses yet - no query needed to know that.
            return Results.Created($"/api/v1/admin/forms/{form.Id}", ToDetail(form, responseCount: 0));
        })
        .WithName("createForm");

        routes.MapGet("/forms", async (FormService forms, CancellationToken ct) =>
            Results.Ok((await forms.ListAsync(ct)).Select(ToSummary)))
            .WithName("listForms");

        routes.MapGet("/forms/{formId:guid}", async (
            Guid formId, FormService forms, CancellationToken ct) =>
        {
            var form = await forms.FindAsync(formId, ct);
            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            var responseCount = await forms.CountResponsesAsync(formId, ct);
            return Results.Ok(ToDetail(form, responseCount));
        })
        .WithName("getForm");

        routes.MapPatch("/forms/{formId:guid}", async (
            Guid formId, RenameFormRequest request, FormService forms, CancellationToken ct) =>
        {
            var form = await forms.FindAsync(formId, ct);
            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            var (updated, error) = await forms.RenameAsync(form, request.Title, ct);
            if (error is not null)
            {
                return Results.BadRequest(new { code = error.Code, limit = error.Limit });
            }

            var responseCount = await forms.CountResponsesAsync(formId, ct);
            return Results.Ok(ToDetail(updated!, responseCount));
        })
        .WithName("renameForm");

        routes.MapDelete("/forms/{formId:guid}", async (
            Guid formId, FormService forms, CancellationToken ct) =>
                await forms.DeleteAsync(formId, ct) ? Results.NoContent() : NeutralNotFound.Result())
            .WithName("deleteForm");

        routes.MapPost("/forms/{formId:guid}/fields", async (
            Guid formId, FieldRequest request, FormService forms, CancellationToken ct) =>
        {
            var form = await forms.FindAsync(formId, ct);
            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            var draft = new FormFieldDraft(
                request.Type, request.Label, request.Required, request.MaxLength, request.MinLength);
            var (field, error) = await forms.AddFieldAsync(form, draft, ct);

            return error is not null
                ? Results.BadRequest(new { code = error.Code, limit = error.Limit })
                : Results.Created($"/api/v1/admin/forms/{formId}", ToFieldDetail(field!));
        })
        .WithName("addFormField");

        routes.MapPatch("/forms/{formId:guid}/fields/{fieldId:guid}", async (
            Guid formId, Guid fieldId, JsonElement body, FormService forms, CancellationToken ct) =>
        {
            var form = await forms.FindAsync(formId, ct);
            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            if (body.ValueKind != JsonValueKind.Object)
            {
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            FieldPatch? patch;
            try
            {
                patch = JsonSerializer.Deserialize<FieldPatch>(
                    body.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            if (patch is null)
            {
                return Results.BadRequest(new { code = ErrorCodes.MalformedRequest });
            }

            // Read from the raw document, because a null MinLength in the record means both
            // "clear it" and "not mentioned", and those are different instructions - the same
            // ambiguity WishListAdminEndpoints already solves for its own optional Description.
            // Without this, minLength could be set once but never cleared: UpdateFieldAsync's
            // fallback-to-old-value-when-omitted logic would treat an explicit null exactly like
            // an absent property.
            var minLengthGiven = body.TryGetProperty("minLength", out _);

            // A "type" in the request body is silently ignored: there is no property for it in
            // FieldPatch, so there is no code path that could apply one (010 FR-008).
            var (field, error) = await forms.UpdateFieldAsync(
                form, fieldId, patch.Label, patch.Required, patch.MaxLength, patch.MinLength,
                minLengthGiven, ct);

            if (error is not null)
            {
                return error.Code == ErrorCodes.FieldSetMismatch
                    ? NeutralNotFound.Result()
                    : Results.BadRequest(new { code = error.Code, limit = error.Limit });
            }

            return Results.Ok(ToFieldDetail(field!));
        })
        .WithName("editFormField");

        routes.MapDelete("/forms/{formId:guid}/fields/{fieldId:guid}", async (
            Guid formId, Guid fieldId, FormService forms, CancellationToken ct) =>
                await forms.RemoveFieldAsync(formId, fieldId, ct)
                    ? Results.NoContent()
                    : NeutralNotFound.Result())
            .WithName("removeFormField");

        routes.MapPut("/forms/{formId:guid}/fields/order", async (
            Guid formId, ReorderRequest request, FormService forms, CancellationToken ct) =>
        {
            var form = await forms.FindAsync(formId, ct);
            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            var (updated, error) = await forms.ReorderFieldsAsync(form, request.FieldIds ?? [], ct);
            if (error is not null)
            {
                return Results.BadRequest(new { code = error.Code });
            }

            var responseCount = await forms.CountResponsesAsync(formId, ct);
            return Results.Ok(ToDetail(updated!, responseCount));
        })
        .WithName("reorderFormFields");

        routes.MapGet("/forms/{formId:guid}/responses", async (
            Guid formId, FormService forms, CancellationToken ct) =>
        {
            var form = await forms.FindAsync(formId, ct);
            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            var responses = await forms.ListResponsesAsync(formId, ct);

            return Results.Ok(responses.Select(r => new
            {
                id = r.Id,
                submittedAt = r.SubmittedAt,
                values = r.Values.Select(v => new { fieldId = v.FieldId, value = v.Value }),
            }));
        })
        .WithName("listFormResponses");

        routes.MapDelete("/forms/{formId:guid}/responses/{responseId:guid}", async (
            Guid formId, Guid responseId, FormService forms, CancellationToken ct) =>
                await forms.DeleteResponseAsync(formId, responseId, ct)
                    ? Results.NoContent()
                    : NeutralNotFound.Result())
            .WithName("deleteFormResponse");

        routes.MapGet("/forms/{formId:guid}/export/csv", async (
            Guid formId, FormService forms, CancellationToken ct) =>
        {
            var form = await forms.FindAsync(formId, ct);
            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            var responses = await forms.ListResponsesAsync(formId, ct);
            var csv = FormCsvExport.Build(form.Fields.OrderBy(f => f.DisplayOrder).ToList(), responses);

            return Results.File(csv, contentType: "text/csv",
                fileDownloadName: FormExport.FileNameFor(form.Title, form.CreatedAt, "csv"));
        })
        .WithName("exportFormCsv");

        routes.MapGet("/forms/{formId:guid}/export/json", async (
            Guid formId, FormService forms, FormExport export, CancellationToken ct) =>
        {
            var form = await forms.FindAsync(formId, ct);
            if (form is null)
            {
                return NeutralNotFound.Result();
            }

            var responses = await forms.ListResponsesAsync(formId, ct);
            var document = export.Build(form, responses);

            // Serialised here rather than returned as an object, because this is a download and
            // not an API response: Results.Json cannot set the file name FR-035 asks for, and
            // Results.File can (mirrors Polls/PollAdminEndpoints.cs's own JSON export).
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            var json = JsonSerializer.SerializeToUtf8Bytes(document, options);

            return Results.File(
                json,
                contentType: "application/json",
                fileDownloadName: FormExport.FileNameFor(form.Title, document.ExportedAt, "json"));
        })
        .WithName("exportFormJson");

        return routes;
    }
}
