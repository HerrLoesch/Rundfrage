using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rundfrage.Api.Http;

/// <summary>
/// Serializer options for the places that serialise by hand rather than through the endpoint
/// pipeline - downloads and patch bodies. Created once: a <see cref="JsonSerializerOptions"/>
/// caches its type metadata, and building a fresh one per request throws that cache away (CA1869).
/// </summary>
public static class JsonDefaults
{
    /// <summary>The same casing as everything else the API emits.</summary>
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// <see cref="Web"/> plus enums as camelCase text, matching the converter Program.cs registers
    /// for API responses - for a download that carries an enum (010's FieldType).
    /// </summary>
    public static readonly JsonSerializerOptions WebWithEnumNames = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
