namespace Meridian.Domain.Entities;

/// <summary>
/// WEEK OFFS (Nov 2026): an employee working for a client whose week offs
/// differ from the company default (Sat + Sun). Added by HR / Admin from
/// Master Data -> Week Offs. Between EffectiveFrom and EffectiveTo (open
/// ended when null) the employee follows these week-off days and the
/// client's holidays. Employees without a record follow the company default.
/// </summary>
public class EmployeeWeekOff
{
	public int EmployeeWeekOffId { get; set; }
	public int EmployeeId { get; set; }

	/// <summary>The client the employee works for during this period.</summary>
	public int AccountId { get; set; }

	/// <summary>Comma-separated short day names, e.g. "Fri,Sat".</summary>
	public required string WeekOffDays { get; set; }

	public DateOnly EffectiveFrom { get; set; }
	public DateOnly? EffectiveTo { get; set; }

	public int? CreatedByEmployeeId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }

	public Employee? Employee { get; set; }
	public Account? Account { get; set; }
	public Employee? CreatedBy { get; set; }
}
