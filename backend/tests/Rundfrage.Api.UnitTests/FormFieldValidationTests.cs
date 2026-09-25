using System.Text.Json;
using Rundfrage.Api.Data.Entities;
using Rundfrage.Api.Forms;

namespace Rundfrage.Api.UnitTests;

/// <summary>
/// The seven type rules of 010 FR-023 to FR-030, checked against the fixture shared with the
/// frontend's own implementation (010 research.md R-7) - the mechanism that keeps FR-030's
/// promise ("the two MUST never disagree") true without a shared runtime.
/// </summary>
public class FormFieldValidationTests
{
    public sealed record FixtureField(int? MaxLength, int? MinLength);

    public sealed record FixtureCase(string Type, FixtureField? Field, string Value, bool ExpectedValid);

    private sealed record Fixture(FixtureCase[] Cases);

    private static readonly Fixture SharedFixture = Load();

    private static Fixture Load()
    {
        var path = FindFixture(AppContext.BaseDirectory);
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<Fixture>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    /// <summary>
    /// Walks up from the test binary's directory to the repository root, so the fixture is found
    /// regardless of the configuration (Debug/Release) or runtime it was built for.
    /// </summary>
    private static string FindFixture(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "frontend", "tests", "fixtures", "form-field-validation.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "form-field-validation.json was not found above " + startDirectory);
    }

    private static FieldType ParseType(string type) => type switch
    {
        "text" => FieldType.Text,
        "integer" => FieldType.Integer,
        "decimal" => FieldType.Decimal,
        "boolean" => FieldType.Boolean,
        "email" => FieldType.Email,
        "phone" => FieldType.Phone,
        "postalCode" => FieldType.PostalCode,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown fixture type"),
    };

    public static IEnumerable<object[]> Cases() =>
        SharedFixture.Cases.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_the_shared_fixture(FixtureCase testCase)
    {
        var field = new FormField
        {
            Id = Guid.NewGuid(),
            FormId = Guid.NewGuid(),
            Type = ParseType(testCase.Type),
            Label = "Testfeld",
            MaxLength = testCase.Field?.MaxLength,
            MinLength = testCase.Field?.MinLength,
        };

        Assert.Equal(testCase.ExpectedValid, FormFieldValidation.IsValid(field, testCase.Value));
    }

    [Fact]
    public void An_absent_value_is_always_valid_because_requiredness_is_a_separate_check()
    {
        var field = new FormField
        {
            Id = Guid.NewGuid(), FormId = Guid.NewGuid(), Type = FieldType.Email, Label = "E-Mail",
        };

        Assert.True(FormFieldValidation.IsValid(field, null));
    }

    [Fact]
    public void The_fixture_covers_every_field_type_at_least_once()
    {
        var covered = SharedFixture.Cases.Select(c => c.Type).Distinct().ToHashSet();
        var expected = new[] { "text", "integer", "decimal", "boolean", "email", "phone", "postalCode" };

        foreach (var type in expected)
        {
            Assert.Contains(type, covered);
        }
    }
}
