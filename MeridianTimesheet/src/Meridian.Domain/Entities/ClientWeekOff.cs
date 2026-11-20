namespace Meridian.Domain.Entities;

/// <summary>
/// WEEK OFFS (Nov 2026): a client's own week-off days, e.g. Zamil = Fri, Sat.
/// Created first in Master Data -> Week Offs; employees are then assigned to
/// the client (see EmployeeWeekOff). EffectiveFrom / EffectiveTo let a client
/// change its week offs later without changing past weeks. Periods for the
/// same client must not overlap.
/// </summary>
public class ClientWeekOff
{
	public int ClientWeekOffId { get; set; }
	public int AccountId { get; set; }

	/// <summary>Comma-separated short day names, e.g. "Fri,Sat".</summary>
	public required string WeekOffDays { get; set; }

	public DateOnly EffectiveFrom { get; set; }
	public DateOnly? EffectiveTo { get; set; }

	public int? CreatedByEmployeeId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }

	public Account? Account { get; set; }
	public Employee? CreatedBy { get; set; }
}
