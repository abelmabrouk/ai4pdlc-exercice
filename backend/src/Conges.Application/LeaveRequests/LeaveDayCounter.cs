using Conges.Application.Common;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Décompte des jours de congé (RG-02) et du délai de prévenance (RG-03). Les jours fériés et les fermetures
/// proviennent exclusivement du calendrier de l'entreprise (ADR-004).
/// </summary>
public sealed class LeaveDayCounter(IHolidayCalendarService calendar)
{
    /// <summary>Jours ouvrables (lundi → samedi, hors fériés et fermetures) de <paramref name="start"/> à <paramref name="end"/> inclus.</summary>
    public int CountLeaveDays(DateOnly start, DateOnly end) =>
        CountDays(start, end, d => d.DayOfWeek != DayOfWeek.Sunday);

    /// <summary>Jours ouvrés (lundi → vendredi, hors fériés et fermetures) strictement entre les deux dates.</summary>
    public int CountWorkingDaysBetween(DateOnly after, DateOnly before) =>
        CountDays(after.AddDays(1), before.AddDays(-1), d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));

    private int CountDays(DateOnly from, DateOnly to, Func<DateOnly, bool> isCountedWeekday)
    {
        if (to < from) return 0;

        var nonWorkingDays = calendar.GetHolidays(from, to).Select(h => h.Date).ToHashSet();
        var count = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (isCountedWeekday(day) && !nonWorkingDays.Contains(day)) count++;
        }

        return count;
    }
}
