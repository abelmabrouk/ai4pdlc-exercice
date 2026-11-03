namespace Conges.Tests;

/// <summary>
/// Horloge figée pour les tests qui dépendent de la date du jour (début dans le passé, délai de prévenance).
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
