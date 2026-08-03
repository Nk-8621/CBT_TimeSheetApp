namespace Meridian.Application.Common;

/// <summary>Week/date helpers — mirrors the frontend's lib/dates.ts so both
/// layers agree on what "the start of the week" means.</summary>
public static class WeekMath
{
    public const decimal StandardHoursPerDay = 8m;

    /// <summary>Returns the Monday on/before the given date.</summary>
    public static DateOnly MondayOf(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7; // Monday = 0 ... Sunday = 6
        return date.AddDays(-diff);
    }

    public static DateOnly[] WeekDays(DateOnly weekStart) =>
        Enumerable.Range(0, 7).Select(weekStart.AddDays).ToArray();

    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
