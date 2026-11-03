using Conges.Application.Common;
using Conges.Infrastructure.Calendar;
using Microsoft.Extensions.Options;

namespace Conges.Tests;

public sealed class HolidayCalendarServiceTests
{
    private static HolidayCalendarService Calendar(params string[] closures) => new(Options.Create(new HolidayCalendarOptions
    {
        CompanyClosures = closures.Select(DateOnly.Parse).ToList(),
    }));

    [Theory]
    [InlineData("2027-01-01")]
    [InlineData("2027-03-29")] // lundi de Pâques
    [InlineData("2027-05-06")] // Ascension
    [InlineData("2027-07-14")]
    [InlineData("2027-12-25")]
    public void Public_holidays_are_detected(string date) =>
        Assert.True(Calendar().IsHoliday(DateOnly.Parse(date)));

    [Fact]
    public void Whit_monday_is_worked_by_default() =>
        Assert.False(Calendar().IsHoliday(new DateOnly(2027, 5, 17)));

    [Fact]
    public void Company_closures_come_from_configuration()
    {
        var holidays = Calendar("2027-05-07").GetHolidays(new DateOnly(2027, 5, 3), new DateOnly(2027, 5, 9));

        Assert.Collection(holidays,
            h => Assert.Equal((new DateOnly(2027, 5, 6), HolidayKind.PublicHoliday), (h.Date, h.Kind)),
            h => Assert.Equal((new DateOnly(2027, 5, 7), HolidayKind.CompanyClosure), (h.Date, h.Kind)),
            h => Assert.Equal((new DateOnly(2027, 5, 8), HolidayKind.PublicHoliday), (h.Date, h.Kind)));
    }

    [Theory]
    [InlineData("2027-03-08", "2027-03-12", 5)] // semaine complète
    [InlineData("2027-03-08", "2027-03-08", 1)] // un seul jour ouvré
    [InlineData("2027-03-13", "2027-03-14", 0)] // week-end seul
    [InlineData("2027-05-03", "2027-05-09", 3)] // Ascension + fermeture ; 8 mai un samedi, pas retiré deux fois
    [InlineData("2027-05-17", "2027-05-17", 1)] // lundi de Pentecôte travaillé
    [InlineData("2026-12-28", "2027-01-04", 4)] // fermeture du 31/12 + jour de l'an, à cheval sur deux années
    [InlineData("2027-03-12", "2027-03-08", 0)] // fin avant début
    public void Working_days_exclude_weekends_holidays_and_closures(string from, string to, int expected) =>
        Assert.Equal(expected, Calendar("2026-12-31", "2027-05-07").CountWorkingDays(DateOnly.Parse(from), DateOnly.Parse(to)));

    // RG-02, page « Décompte des jours » : jours ouvrables = lundi → samedi, hors fériés et fermetures.
    [Theory]
    [InlineData("2027-03-08", "2027-03-12", 5)] // lundi → vendredi
    [InlineData("2027-03-08", "2027-03-13", 6)] // lundi → samedi
    [InlineData("2027-03-11", "2027-03-16", 5)] // jeudi → mardi suivant
    [InlineData("2027-03-08", "2027-03-14", 6)] // lundi → dimanche
    [InlineData("2027-03-14", "2027-03-14", 0)] // dimanche seul
    [InlineData("2027-05-03", "2027-05-09", 3)] // Ascension (jeu.), fermeture (ven.), 8 mai férié un samedi
    [InlineData("2027-05-17", "2027-05-17", 1)] // lundi de Pentecôte travaillé
    [InlineData("2026-12-28", "2027-01-04", 5)] // fermeture du 31/12, jour de l'an ; samedi 2 janvier décompté
    [InlineData("2027-03-12", "2027-03-08", 0)] // fin avant début
    public void Workable_days_count_saturdays_but_never_sundays_holidays_or_closures(string from, string to, int expected) =>
        Assert.Equal(expected, Calendar("2026-12-31", "2027-05-07").CountWorkableDays(DateOnly.Parse(from), DateOnly.Parse(to)));
}
