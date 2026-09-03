using System.Text.Json;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// FR-027, FR-040, SC-012. Unknown, malformed, expired and deleted must be indistinguishable.
/// Four handlers would drift apart the first time one of them gained a helpful detail, so there
/// is exactly one (research.md R-4).
/// </summary>
public class NeutralNotFoundTests
{
    [Fact]
    public void Exposes_one_payload_and_no_variants()
    {
        // If a per-cause overload ever appears, this is where it gets caught.
        var factories = typeof(NeutralNotFound)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name != nameof(object.Equals) && m.Name != nameof(object.ReferenceEquals))
            .ToArray();

        Assert.Single(factories);
        Assert.Empty(factories[0].GetParameters());
    }

    [Fact]
    public void Payload_is_a_bare_code_with_nothing_else()
    {
        var json = JsonSerializer.Serialize(NeutralNotFound.Payload);

        using var parsed = JsonDocument.Parse(json);
        var names = parsed.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(["code"], names);
        Assert.Equal("not_found", parsed.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void Payload_names_no_cause()
    {
        // The point of the requirement: nothing may hint at which of the four situations it was.
        var json = JsonSerializer.Serialize(NeutralNotFound.Payload).ToLowerInvariant();

        foreach (var leak in new[] { "expired", "deleted", "malformed", "unknown", "invalid", "gone" })
        {
            Assert.DoesNotContain(leak, json);
        }
    }

    [Fact]
    public void Every_call_produces_an_identical_payload()
    {
        var first = JsonSerializer.Serialize(NeutralNotFound.Payload);
        var second = JsonSerializer.Serialize(NeutralNotFound.Payload);

        Assert.Equal(first, second);
    }
}
