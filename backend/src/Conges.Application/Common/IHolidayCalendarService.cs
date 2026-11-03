namespace Conges.Application.Common;

public enum HolidayKind
{
    /// <summary>Jour férié légal.</summary>
    PublicHoliday,

    /// <summary>Jour de fermeture décidé par l'entreprise (pont, fermeture de fin d'année…).</summary>
    CompanyClosure,
}

public sealed record Holiday(DateOnly Date, string Label, HolidayKind Kind);

/// <summary>
/// Calendrier des jours non travaillés de l'entreprise : jours fériés légaux et fermetures.
/// </summary>
public interface IHolidayCalendarService
{
    bool IsHoliday(DateOnly date);

    /// <summary>Jours fériés et fermetures compris entre <paramref name="from"/> et <paramref name="to"/> inclus.</summary>
    IReadOnlyList<Holiday> GetHolidays(DateOnly from, DateOnly to);

    /// <summary>
    /// Jours ouvrables (unité de décompte des congés, RG-02) : du lundi au samedi entre <paramref name="from"/> et
    /// <paramref name="to"/> inclus, hors jours fériés et fermetures ; 0 si <paramref name="to"/> est antérieur à
    /// <paramref name="from"/>.
    /// </summary>
    int CountWorkableDays(DateOnly from, DateOnly to);

    /// <summary>
    /// Jours ouvrés (unité des délais, RG-03) : du lundi au vendredi entre <paramref name="from"/> et <paramref name="to"/> inclus, hors jours fériés
    /// et fermetures ; 0 si <paramref name="to"/> est antérieur à <paramref name="from"/>.
    /// </summary>
    int CountWorkingDays(DateOnly from, DateOnly to);
}
