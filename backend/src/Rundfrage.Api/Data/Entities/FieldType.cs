namespace Rundfrage.Api.Data.Entities;

/// <summary>
/// The seven kinds of question a form field may be (010 FR-003). Fixed for a field's whole
/// lifetime - there is no route that changes it once the field exists (010 FR-008, spec
/// clarification 2026-09-22).
/// </summary>
/// <remarks>
/// Stored via <c>HasConversion&lt;string&gt;()</c> rather than as a bare integer, so the database
/// file and a <c>sqlite3 .dump</c> stay readable without cross-referencing this enum (010
/// research.md R-2).
/// </remarks>
public enum FieldType
{
    Text,
    Integer,
    Decimal,
    Boolean,
    Email,
    Phone,
    PostalCode,
}
