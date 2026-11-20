namespace Meridian.Application.Common;

/// <summary>
/// WEEK OFFS (Nov 2026): reads and writes the week-off days stored on an
/// employee week-off record, e.g. "Fri,Sat". Short English day names only.
/// </summary>
public static class WeekOffDays
{
	/// <summary>Company default week offs - used when an employee has no record.</summary>
	public static readonly DayOfWeek[] CompanyDefault = [DayOfWeek.Saturday, DayOfWeek.Sunday];

	private static readonly (string Short, DayOfWeek Day)[] Names =
	[
		("Mon", DayOfWeek.Monday),
		("Tue", DayOfWeek.Tuesday),
		("Wed", DayOfWeek.Wednesday),
		("Thu", DayOfWeek.Thursday),
		("Fri", DayOfWeek.Friday),
		("Sat", DayOfWeek.Saturday),
		("Sun", DayOfWeek.Sunday),
	];

	/// <summary>"Fri, sat" -> [Friday, Saturday]. Accepts short or full names
	/// in any case. Returns null if any part isn't a day name.</summary>
	public static List<DayOfWeek>? TryParse(IEnumerable<string> parts)
	{
		var result = new List<DayOfWeek>();
		foreach (var raw in parts)
		{
			var text = raw.Trim();
			if (text.Length == 0) continue;
			var match = Names.FirstOrDefault(n =>
				text.StartsWith(n.Short, StringComparison.OrdinalIgnoreCase)
				&& n.Day.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase));
			if (match.Short is null) return null;
			if (!result.Contains(match.Day)) result.Add(match.Day);
		}
		return result;
	}

	/// <summary>"Fri,Sat" (stored text) -> [Friday, Saturday].</summary>
	public static List<DayOfWeek> Parse(string stored) =>
		TryParse(stored.Split(',')) ?? [];

	/// <summary>[Saturday, Friday] -> "Fri,Sat" (always Monday-first order).</summary>
	public static string Format(IEnumerable<DayOfWeek> days)
	{
		var set = days.ToHashSet();
		return string.Join(",", Names.Where(n => set.Contains(n.Day)).Select(n => n.Short));
	}

	/// <summary>[Friday, Saturday] -> ["Fri", "Sat"] for the API.</summary>
	public static string[] ToShortNames(IEnumerable<DayOfWeek> days)
	{
		var set = days.ToHashSet();
		return Names.Where(n => set.Contains(n.Day)).Select(n => n.Short).ToArray();
	}
}
