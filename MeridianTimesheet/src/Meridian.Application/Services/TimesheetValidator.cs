using Meridian.Application.Common;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public record ValidationLine(string TaskName, string? Note, decimal[] HoursByDay);

/// <summary>
/// Pure business-rule validation for "can this week be submitted?" — no
/// database access, so it's trivial to unit test in isolation. Deliberately
/// mirrors the frontend's SubmitDrawer checks so both layers agree on what
/// "valid" means; if one changes, the other needs to change with it.
/// </summary>
public static class TimesheetValidator
{
    public static (List<string> Errors, List<string> Warnings) Validate(
        DateOnly weekStartDate,
        IReadOnlyList<ValidationLine> lines,
        IReadOnlyList<DayType> dayTypes)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var days = WeekMath.WeekDays(weekStartDate);

        var dayTotals = new decimal[7];
        foreach (var line in lines)
            for (var i = 0; i < 7; i++)
                dayTotals[i] += line.HoursByDay[i];

        var capacity = dayTypes.Select(t => t switch
        {
            DayType.W or DayType.WFH => WeekMath.StandardHoursPerDay,
            DayType.LH => WeekMath.StandardHoursPerDay / 2,
            _ => 0m,
        }).ToArray();
        var total = dayTotals.Sum();

        if (total == 0)
            errors.Add("No hours logged for this week.");

        foreach (var line in lines)
        {
            if (line.HoursByDay.Sum() == 0)
                errors.Add($"Line \"{line.TaskName}\" has no hours — remove it or enter hours.");

            if (line.HoursByDay.Any(hours => hours > TimeEntryRules.MaxHoursPerLinePerDay))
                errors.Add($"Line \"{line.TaskName}\" has more than {TimeEntryRules.MaxHoursPerLinePerDay:0.##} h on one day — split it into separate lines.");
        }

        for (var i = 0; i < 7; i++)
        {
            var date = days[i];
            var label = date.ToString("d MMM");

            if (dayTotals[i] > 0 && capacity[i] == 0)
                errors.Add($"{label} is marked {dayTypes[i]} but has {dayTotals[i]:0.##} h logged.");

            // Same rule the My Timesheet screen applies before enabling Submit:
            // every working day must total exactly its capacity (8 h, or 4 h on
            // a half-day leave) - now also enforced here so no route around the
            // screen (e.g. Excel import) can submit an unbalanced week.
            if (capacity[i] > 0 && dayTotals[i] != capacity[i])
                errors.Add($"{label} has {dayTotals[i]:0.##} h logged — it must be exactly {capacity[i]:0.##} h.");
        }

        foreach (var line in lines)
        {
            if (line.HoursByDay.Sum() >= 8 && string.IsNullOrWhiteSpace(line.Note))
                warnings.Add($"\"{line.TaskName}\" has {line.HoursByDay.Sum():0.##} h with no description.");
        }

        return (errors, warnings);
    }
}
