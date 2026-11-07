using System.Text.Json;
using Rundfrage.Api.Http;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// Reading a PATCH body that may leave properties out - shared by the wish-list, Ersteller and
/// form-field edits, which each carried their own copy of these rules before.
/// </summary>
public class PatchBodyTests
{
    public sealed record Patch(string? Label, int? MinLength);

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    [Fact]
    public void An_object_is_read_into_the_patch_with_web_casing()
    {
        Assert.True(PatchBody.TryRead<Patch>(Json("""{ "label": "Name", "minLength": 2 }"""), out var patch));
        Assert.Equal(new Patch("Name", 2), patch);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("5")]
    [InlineData("\"x\"")]
    public void Valid_json_that_is_not_an_object_is_not_a_patch(string text)
    {
        Assert.False(PatchBody.TryRead<Patch>(Json(text), out _));
    }

    [Fact]
    public void A_property_of_the_wrong_type_is_not_a_patch()
    {
        Assert.False(PatchBody.TryRead<Patch>(Json("""{ "minLength": "zwei" }"""), out _));
    }

    [Fact]
    public void An_explicit_null_counts_as_mentioned_and_an_absent_property_does_not()
    {
        // The distinction a nullable record property cannot make: "clear it" versus "leave it".
        var body = Json("""{ "minLength": null }""");

        Assert.True(PatchBody.Mentions(body, "minLength"));
        Assert.False(PatchBody.Mentions(body, "label"));
    }
}
