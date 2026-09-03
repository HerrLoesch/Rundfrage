namespace Rundfrage.Api.IntegrationTests;

/// <summary>
/// FR-011a resolves every day boundary against Europe/Berlin, and every poll creation needs it
/// to compute a retention deadline. Without zone data the very first poll throws.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this test does not catch.</b> It runs on the test host - a developer machine or a CI
/// runner - both of which carry zone data. The defect this guards against lives only in the
/// Alpine <i>runtime</i> image, and the SDK image used to build and test carries zone data too.
/// So a green run here proves the host is fine, not that the container is.
/// </para>
/// <para>
/// The container is guarded separately by <c>e2e/tests/timezone.spec.ts</c>, which inspects the
/// running image. Both exist because they fail for different reasons: this one if the zone id is
/// ever wrong, that one if the image ever stops shipping tzdata.
/// </para>
/// </remarks>
public class TimeZoneAvailabilityTests
{
    private const string ZoneId = "Europe/Berlin";

    [Fact]
    public void The_configured_zone_resolves_on_this_host()
    {
        var exception = Record.Exception(() => TimeZoneInfo.FindSystemTimeZoneById(ZoneId));

        Assert.Null(exception);
    }

    [Fact]
    public void The_configured_zone_observes_summer_time()
    {
        // FR-011b: a fixed offset would not do, because the offset changes twice a year.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);

        var january = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Unspecified);
        var july = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified);

        Assert.False(zone.IsDaylightSavingTime(january));
        Assert.True(zone.IsDaylightSavingTime(july));
        Assert.NotEqual(zone.GetUtcOffset(january), zone.GetUtcOffset(july));
    }
}
