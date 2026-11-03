using Conges.Application.Common;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Décompte des jours de congé et du délai de prévenance. Les jours non travaillés (fériés et fermetures)
/// viennent exclusivement du calendrier de l'entreprise (ADR-004).
/// </summary>
public sealed class LeaveDayCounter(IHolidayCalendarService calendar)
{
    /// <summary>
    /// Jours ouvrables (du lundi au samedi) de <paramref name="start"/> à <paramref name="end"/> inclus,
    /// hors jours non travaillés (RG-02, wiki « Décompte des jours »).
    /// </summary>
    public int CountLeaveDays(DateOnly start, DateOnly end) =>
        CountDays(start, end, includeSaturday: true);

    /// <summary>
    /// Jours ouvrés (du lundi au vendredi) strictement compris entre <paramref name="submissionDate"/> et
    /// <paramref name="startDate"/>, hors jours non travaillés (RG-03, délai de prévenance).
    /// </summary>
    public int CountNoticeDays(DateOnly submissionDate, DateOnly startDate) =>
        CountDays(submissionDate.AddDays(1), startDate.AddDays(-1), includeSaturday: false);

    private int CountDays(DateOnly from, DateOnly to, bool includeSaturday)
    {
        if (to < from) return 0;

        var nonWorkingDays = calendar.GetHolidays(from, to).Select(h => h.Date).ToHashSet();
        var count = 0;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var isCounted = day.DayOfWeek switch
            {
                DayOfWeek.Sunday => false,
                DayOfWeek.Saturday => includeSaturday,
                _ => true,
            };

            if (isCounted && !nonWorkingDays.Contains(day)) count++;
        }

        return count;
    }
}
