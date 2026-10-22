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
}
