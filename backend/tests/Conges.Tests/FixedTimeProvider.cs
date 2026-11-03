namespace Conges.Tests;

/// <summary>Horloge figée pour les tests dépendant de la date du jour (RG-01, RG-03).</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
