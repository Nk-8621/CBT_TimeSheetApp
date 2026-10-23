using Meridian.Application.Exceptions;

namespace Meridian.Application.Common;

/// <summary>
/// Rules for the hours on a single timesheet line. The My Timesheet screen
/// already applies these while typing (EntryDrawer / WeekGrid cap each day
/// at 4 h); they're repeated here so every route that saves a line - the
/// screen, the Excel import, or a direct API call - follows the same rule.
/// </summary>
public static class TimeEntryRules
{
    /// <summary>One task line can hold at most this many hours on one day.
    /// More work on the same task that day goes on a separate line.</summary>
    public const decimal MaxHoursPerLinePerDay = 4m;

    private static readonly string[] DayNames = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    public static void ValidateHours(decimal[] hoursByDay)
    {
        if (hoursByDay is null || hoursByDay.Length != 7)
            throw new BusinessRuleException("Hours must be given for exactly 7 days (Monday to Sunday).");

        for (var i = 0; i < 7; i++)
        {
            if (hoursByDay[i] < 0)
                throw new BusinessRuleException($"{DayNames[i]} has negative hours.");

            if (hoursByDay[i] > MaxHoursPerLinePerDay)
                throw new BusinessRuleException(
                    $"{DayNames[i]} has {hoursByDay[i]:0.##} h on this line — a line can hold at most {MaxHoursPerLinePerDay:0.##} h per day. Add a separate line for the rest.");
        }
    }
}
