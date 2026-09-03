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
        IReadOnlyList<DayType> dayTypes,
        DateOnly today)
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
        }

        for (var i = 0; i < 7; i++)
        {
            var date = days[i];
            var label = date.ToString("d MMM");

            if (dayTotals[i] > 0 && capacity[i] == 0)
                errors.Add($"{label} is marked {dayTypes[i]} but has {dayTotals[i]:0.##} h logged.");

            if (capacity[i] > 0 && dayTotals[i] == 0 && date <= today)
                warnings.Add($"{label} has no hours against {capacity[i]:0.##} h capacity.");

            if (capacity[i] > 0 && dayTotals[i] > capacity[i])
                warnings.Add($"{label} logged {dayTotals[i]:0.##} h against {capacity[i]:0.##} h capacity.");
        }

        foreach (var line in lines)
        {
            if (line.HoursByDay.Sum() >= 8 && string.IsNullOrWhiteSpace(line.Note))
                warnings.Add($"\"{line.TaskName}\" has {line.HoursByDay.Sum():0.##} h with no description.");
        }

        var capacityTotal = capacity.Sum();
        if (total < capacityTotal)
            warnings.Add($"Total {total:0.##} h is below available capacity of {capacityTotal:0.##} h.");

        return (errors, warnings);
    }
}
