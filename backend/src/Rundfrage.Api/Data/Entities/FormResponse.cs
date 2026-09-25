namespace Rundfrage.Api.Data.Entities;

/// <summary>
/// One participant's complete submission to a <see cref="Form"/> at one point in time
/// (010 FR-019). Carries no participant identity, IP address or user-agent (Principle IV).
/// </summary>
public sealed class FormResponse
{
    public Guid Id { get; set; }

    public Guid FormId { get; set; }

    public Form? Form { get; set; }

    public DateTime SubmittedAt { get; set; }

    public List<FormFieldValue> Values { get; set; } = [];
}
