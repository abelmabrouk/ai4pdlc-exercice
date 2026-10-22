using Conges.Application.Common;
using Microsoft.Extensions.Options;

namespace Conges.Infrastructure.Calendar;

public sealed class HolidayCalendarOptions
{
    public const string SectionName = "HolidayCalendar";

    /// <summary>Jours de fermeture de l'entreprise, au format yyyy-MM-dd.</summary>
    public List<DateOnly> CompanyClosures { get; set; } = [];

    /// <summary>
    /// Le lundi de Pentecôte est travaillé dans l'entreprise (journée de solidarité) :
    /// il n'est pas considéré comme férié tant que cette option vaut <c>false</c>.
    /// </summary>
    public bool WhitMondayIsHoliday { get; set; }
}

public sealed class HolidayCalendarService(IOptions<HolidayCalendarOptions> options) : IHolidayCalendarService
{
    private readonly HolidayCalendarOptions _options = options.Value;

    public bool IsHoliday(DateOnly date) => GetHolidays(date, date).Count > 0;

    public IReadOnlyList<Holiday> GetHolidays(DateOnly from, DateOnly to)
    {
        if (to < from) return [];

        var publicHolidays = Enumerable.Range(from.Year, to.Year - from.Year + 1).SelectMany(PublicHolidays);
        var closures = _options.CompanyClosures.Select(d => new Holiday(d, "Fermeture entreprise", HolidayKind.CompanyClosure));

        return publicHolidays
            .Concat(closures)
            .Where(h => h.Date >= from && h.Date <= to)
            .GroupBy(h => h.Date)
            .Select(g => g.First())
            .OrderBy(h => h.Date)
            .ToList();
    }

    private IEnumerable<Holiday> PublicHolidays(int year)
    {
        var easter = EasterSunday(year);

        yield return Public(new DateOnly(year, 1, 1), "Jour de l'an");
        yield return Public(easter.AddDays(1), "Lundi de Pâques");
        yield return Public(new DateOnly(year, 5, 1), "Fête du travail");
        yield return Public(new DateOnly(year, 5, 8), "Victoire 1945");
        yield return Public(easter.AddDays(39), "Ascension");
        if (_options.WhitMondayIsHoliday) yield return Public(easter.AddDays(50), "Lundi de Pentecôte");
        yield return Public(new DateOnly(year, 7, 14), "Fête nationale");
        yield return Public(new DateOnly(year, 8, 15), "Assomption");
        yield return Public(new DateOnly(year, 11, 1), "Toussaint");
        yield return Public(new DateOnly(year, 11, 11), "Armistice 1918");
        yield return Public(new DateOnly(year, 12, 25), "Noël");
    }

    private static Holiday Public(DateOnly date, string label) => new(date, label, HolidayKind.PublicHoliday);

    // Algorithme de Meeus/Jones/Butcher (calendrier grégorien).
    private static DateOnly EasterSunday(int year)
    {
        int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4;
        int f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
