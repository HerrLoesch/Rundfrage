namespace Rundfrage.Api.Http;

/// <summary>
/// The slug-building half of a download's file name, shared by every export that names itself
/// after operator-written text (<c>Polls/PollExport.cs</c>, <c>Forms/FormExport.cs</c>) -
/// extracted on its second concrete use, which is what Principle III asks for rather than
/// anticipating one.
/// </summary>
public static class DownloadFileName
{
    private const int MaxSlugLength = 60;

    /// <summary>
    /// Lowercases <paramref name="title"/>, replaces everything but letters and digits with a
    /// dash, collapses repeated dashes, and bounds the result - a title can be very long (a poll's
    /// may run to 300 characters) or made entirely of punctuation, and neither should produce an
    /// unusable file name. Falls back to <paramref name="fallback"/> when nothing survives.
    /// </summary>
    public static string Slugify(string title, string fallback)
    {
        var slug = new string(title.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        slug = slug.Length > MaxSlugLength ? slug[..MaxSlugLength].Trim('-') : slug;
        return slug.Length == 0 ? fallback : slug;
    }

    /// <summary>Names the file after the slug and the moment, so several exports can share a folder.</summary>
    public static string WithTimestamp(string slug, DateTime takenAtUtc, string extension) =>
        $"{slug}-{takenAtUtc:yyyy-MM-dd'T'HHmmss'Z'}.{extension}";
}
