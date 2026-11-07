using System.Text.Json;

namespace Rundfrage.Api.Http;

/// <summary>
/// Reads a PATCH body that is bound as a raw <see cref="JsonElement"/>, so the endpoint can still
/// ask whether an optional property was mentioned at all.
/// </summary>
/// <remarks>
/// A null in the deserialised record means both "clear it" and "not mentioned", and those are
/// different instructions; <see cref="Mentions"/> tells them apart from the raw document.
/// </remarks>
public static class PatchBody
{
    /// <summary>
    /// The patch, or false for anything that is not a change to make: <c>null</c>, <c>[]</c>,
    /// <c>5</c> and <c>"x"</c> are valid JSON but not objects, and a property of the right name
    /// but the wrong type (<c>targetDate: "gestern"</c>) does not deserialise.
    /// </summary>
    public static bool TryRead<T>(JsonElement body, out T patch)
        where T : class
    {
        patch = null!;

        if (body.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        try
        {
            patch = body.Deserialize<T>(JsonDefaults.Web)!;
        }
        catch (JsonException)
        {
            return false;
        }

        return patch is not null;
    }

    /// <summary>Whether the body names <paramref name="property"/>, even with a null value.</summary>
    /// <remarks>Only meaningful after <see cref="TryRead{T}"/> confirmed the body is an object.</remarks>
    public static bool Mentions(JsonElement body, string property) =>
        body.TryGetProperty(property, out _);
}
