using Conges.Application.LeaveRequests;
using Conges.Infrastructure.Calendar;
using Microsoft.Extensions.Options;

namespace Conges.Tests;

/// <summary>
/// Décompte en jours ouvrables (RG-02, wiki « Décompte des jours ») et délai de prévenance en jours ouvrés (RG-03).
/// Calendrier réel de l'entreprise, avec la fermeture du vendredi 2027-05-07.
/// </summary>
public sealed class LeaveDayCounterTests
{
    private readonly LeaveDayCounter _counter = new(new HolidayCalendarService(Options.Create(new HolidayCalendarOptions
    {
        CompanyClosures = [new DateOnly(2027, 5, 7)],
    })));

    [Theory]
    [InlineData("2027-03-08", "2027-03-12", 5)] // lundi → vendredi
    [InlineData("2027-03-08", "2027-03-13", 6)] // lundi → samedi : le samedi est ouvrable
    [InlineData("2027-03-11", "2027-03-16", 5)] // jeudi → mardi : jeu, ven, sam, lun, mar
    [InlineData("2027-03-08", "2027-03-14", 6)] // lundi → dimanche : le dimanche n'est jamais décompté
    [InlineData("2027-05-03", "2027-05-08", 3)] // Ascension, fermeture du 7 mai et 8 mai exclus
    [InlineData("2027-05-17", "2027-05-17", 1)] // lundi de Pentecôte : journée de solidarité, travaillée
    [InlineData("2027-03-14", "2027-03-14", 0)] // dimanche seul
    [InlineData("2027-05-06", "2027-05-06", 0)] // Ascension seule
    public void RG02_leave_days_are_counted_in_working_days_monday_to_saturday(string start, string end, int expected) =>
        Assert.Equal(expected, _counter.CountLeaveDays(DateOnly.Parse(start), DateOnly.Parse(end)));

    [Fact]
    public void RG02_inverted_period_counts_no_day() =>
        Assert.Equal(0, _counter.CountLeaveDays(new DateOnly(2027, 3, 12), new DateOnly(2027, 3, 8)));

    [Theory]
    [InlineData("2027-03-01", "2027-03-04", 2)] // lundi → jeudi : mardi et mercredi
    [InlineData("2027-03-01", "2027-03-03", 1)] // lundi → mercredi : mardi seul
    [InlineData("2027-03-04", "2027-03-08", 1)] // jeudi → lundi : vendredi seul, le samedi ne compte pas
    [InlineData("2027-03-04", "2027-03-09", 2)] // jeudi → mardi : vendredi et lundi
    [InlineData("2027-05-04", "2027-05-10", 1)] // Ascension et fermeture du 7 mai ne comptent pas
    [InlineData("2027-05-04", "2027-05-11", 2)] // mercredi 5 et lundi 10
    [InlineData("2027-03-01", "2027-03-01", 0)] // début le jour de la soumission
    [InlineData("2027-03-01", "2027-02-28", 0)] // début avant la soumission
    public void RG03_notice_days_are_business_days_strictly_between_submission_and_start(
        string submission, string start, int expected) =>
        Assert.Equal(expected, _counter.CountNoticeDays(DateOnly.Parse(submission), DateOnly.Parse(start)));
}
