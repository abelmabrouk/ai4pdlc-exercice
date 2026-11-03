namespace Conges.Tests;

/// <summary>
/// Horloge de test : l'instant courant est fixé (et modifiable), dans le fuseau UTC.
/// </summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>Horloge positionnée au jour <paramref name="isoDate"/> (yyyy-MM-dd), à <paramref name="hour"/>:00 UTC.</summary>
    public static TestClock At(string isoDate, int hour = 9) => new()
    {
        Now = new DateTimeOffset(DateOnly.Parse(isoDate).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero),
    };
}
