using Meridian.Domain.Enums;

namespace Meridian.Application.Common;

/// <summary>
/// The review flags shown to an approver — distinct from TimesheetValidator
/// (which governs whether an employee CAN submit). These flags exist to help
/// a reviewer spot what needs a closer look; they never block anything.
/// Ported directly from the original wireframe's flagsFor() function.
/// </summary>
public static class ApprovalFlagsCalculator
{
	public record FlagLine(string TaskName, string? Note, decimal[] HoursByDay, string Classification);

	private static readonly string[] DayNames = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

	public static List<string> Calculate(IReadOnlyList<FlagLine> lines, IReadOnlyList<DayType> dayTypes)
	{
		var flags = new List<string>();
		var dayTotals = new decimal[7];
		foreach (var line in lines)
			for (var i = 0; i < 7; i++)
				dayTotals[i] += line.HoursByDay[i];

		var capacity = dayTypes.Select(t => t is DayType.W or DayType.WFH ? WeekMath.StandardHoursPerDay : 0m).ToArray();
		var total = dayTotals.Sum();
		var capacityTotal = capacity.Sum();

		if (total < capacityTotal)
			flags.Add($"{capacityTotal - total:0.##} h under capacity");
		if (total > capacityTotal)
			flags.Add($"{total - capacityTotal:0.##} h over capacity");

		for (var i = 0; i < 7; i++)
		{
			if (dayTotals[i] > 0 && capacity[i] == 0)
				flags.Add($"Hours on {DayNames[i]} ({dayTypes[i]})");
		}

		var nonBillable = lines.Where(l => l.Classification == "NonBillable").Sum(l => l.HoursByDay.Sum());
		if (total > 0 && nonBillable / total > 0.4m)
			flags.Add($"{Math.Round(nonBillable / total * 100)}% non-billable");

		var noDescCount = lines.Count(l => l.HoursByDay.Sum() >= 8 && string.IsNullOrWhiteSpace(l.Note));
		if (noDescCount > 0)
			flags.Add($"{noDescCount} line{(noDescCount > 1 ? "s" : "")} without description");

		var reworkHours = lines
			.Where(l => l.TaskName.Contains("Rework", StringComparison.OrdinalIgnoreCase)
					 || l.TaskName.Contains("Bug Fix", StringComparison.OrdinalIgnoreCase))
			.Sum(l => l.HoursByDay.Sum());
		if (reworkHours > 0 && total > 0 && reworkHours / total > 0.25m)
			flags.Add("Rework above 25%");

		return flags;
	}
}