using Conges.Application.LeaveRequests;
using Conges.Infrastructure.Calendar;
using Microsoft.Extensions.Options;

namespace Conges.Tests;

public sealed class LeaveDayCounterTests
{
    // Même configuration que la production : fermeture le vendredi 7 mai 2027, lundi de Pentecôte travaillé.
    private static readonly LeaveDayCounter Counter = new(new HolidayCalendarService(Options.Create(new HolidayCalendarOptions
    {
        CompanyClosures = [new DateOnly(2027, 5, 7)],
    })));

    // RG-02 : jours ouvrables (lundi → samedi), bornes incluses, hors fériés et fermetures.
    [Theory]
    [InlineData("2027-03-08", "2027-03-12", 5)] // lundi → vendredi
    [InlineData("2027-03-08", "2027-03-13", 6)] // lundi → samedi : le samedi compte
    [InlineData("2027-03-04", "2027-03-09", 5)] // jeudi → mardi : jeu, ven, sam, lun, mar
    [InlineData("2027-03-08", "2027-03-14", 6)] // lundi → dimanche : le dimanche ne compte jamais
    [InlineData("2027-03-12", "2027-03-15", 3)] // vendredi → lundi : ven, sam, lun
    [InlineData("2027-05-03", "2027-05-08", 3)] // Ascension (jeu 6), fermeture (ven 7), Victoire 1945 (sam 8)
    [InlineData("2027-05-17", "2027-05-17", 1)] // lundi de Pentecôte : journée de solidarité travaillée
    [InlineData("2027-03-14", "2027-03-14", 0)] // dimanche seul
    [InlineData("2027-05-06", "2027-05-06", 0)] // férié seul
    public void RG02_leave_days_are_counted_in_working_days_monday_to_saturday(string start, string end, int expected) =>
        Assert.Equal(expected, Counter.CountLeaveDays(DateOnly.Parse(start), DateOnly.Parse(end)));

    // RG-03 : jours ouvrés (lundi → vendredi) strictement entre la soumission et le début du congé.
    [Theory]
    [InlineData("2027-03-01", "2027-03-03", 1)] // lundi → mercredi : mardi
    [InlineData("2027-03-01", "2027-03-04", 2)] // lundi → jeudi : mardi, mercredi
    [InlineData("2027-03-05", "2027-03-09", 1)] // vendredi → mardi : le samedi n'est pas ouvré, seul lundi compte
    [InlineData("2027-05-04", "2027-05-10", 1)] // mer 5 ; Ascension, fermeture, samedi et dimanche exclus
    [InlineData("2027-05-04", "2027-05-11", 2)] // mer 5 et lun 10
    [InlineData("2027-03-01", "2027-03-01", 0)] // même jour
    [InlineData("2027-03-01", "2027-03-02", 0)] // lendemain
    public void RG03_notice_counts_business_days_strictly_between_two_dates(string after, string before, int expected) =>
        Assert.Equal(expected, Counter.CountWorkingDaysBetween(DateOnly.Parse(after), DateOnly.Parse(before)));
}
