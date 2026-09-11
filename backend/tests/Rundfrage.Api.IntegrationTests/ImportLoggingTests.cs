namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// T019: the fact of an import may be logged; its content may not (FR-005, 002 FR-026).
/// </summary>
/// <remarks>
/// Reads what the application actually wrote to standard output, the same way
/// <see cref="AdminLoggingTests"/> does and for the same reason: a test that inspects a logger it
/// created itself proves nothing about what the production code passed to the real one. Console
/// output is process-global, so this shares that suite's non-parallel collection.
/// </remarks>
[Collection(nameof(ConsoleCapturingCollection))]
public class ImportLoggingTests
{
    private static async Task<string> CapturingAsync(Func<ApiFactory, Task> exercise)
    {
        var captured = new StringWriter();
        var original = Console.Out;
        Console.SetOut(captured);

        var directory = Path.Combine(Path.GetTempPath(), "rundfrage-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directory);

        try
        {
            using var factory = new ApiFactory(directory);
            await exercise(factory);
            await Task.Delay(50); // let the sink drain
        }
        finally
        {
            Console.SetOut(original);
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        return captured.ToString();
    }

    [Fact]
    public async Task No_field_of_an_imported_file_reaches_the_log()
    {
        const string secretTitle = "Geheime-Betriebsfeier-XYZZY";
        const string secretName = "Frau-Vertraulich-PLUGH";
        const string secretMessage = "Bitte-nicht-weitersagen-QUUX";

        var log = await CapturingAsync(factory => ImportTestHelper.ImportAsync(
            factory,
            new ExportDocumentBuilder()
                .WithTitle(secretTitle)
                .WithMessage(secretMessage)
                .WithResponse(secretName)));

        Assert.DoesNotContain(secretTitle, log);
        Assert.DoesNotContain(secretName, log);
        Assert.DoesNotContain(secretMessage, log);
    }

    [Fact]
    public async Task A_refused_import_does_not_log_the_file_either()
    {
        // The refusal path reads the document far enough to reject it, which is exactly where a
        // helpful "could not parse: <content>" tends to be added.
        const string secret = "Vertraulich-GRUE";

        var log = await CapturingAsync(factory => ImportTestHelper.ImportAsync(
            factory, ExportDocumentBuilder.FormContentFor($$"""{"nonsense":"{{secret}}"}""")));

        Assert.DoesNotContain(secret, log);
    }

    [Fact]
    public async Task That_an_import_happened_is_logged_with_its_counts()
    {
        // FR-005 allows the fact and forbids the content. Asserted against the message template
        // and the counts rather than a substring: "mported" also matches text this feature never
        // wrote, so it would have passed against a log that said nothing about the import.
        var log = await CapturingAsync(factory => ImportTestHelper.ImportAsync(
            factory,
            new ExportDocumentBuilder().WithTitle("Belanglos").WithResponse("Egal")));

        Assert.Contains("Imported poll {PollId}", log);
        Assert.Contains("\"ResponseCount\":1", log);
    }
}
