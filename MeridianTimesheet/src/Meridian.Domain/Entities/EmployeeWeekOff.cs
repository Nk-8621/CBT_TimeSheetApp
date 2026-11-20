namespace Meridian.Domain.Entities;

/// <summary>
/// WEEK OFFS (Nov 2026): an employee assigned to a client's week offs
/// (Master Data -> Week Offs -> Add Employees). Between EffectiveFrom and
/// EffectiveTo (open ended when null) the employee follows that client's
/// week-off days (ClientWeekOff) and only that client's holidays. Employees
/// without an assignment follow the company default (Sat + Sun).
/// </summary>
public class EmployeeWeekOff
{
	public int EmployeeWeekOffId { get; set; }
	public int EmployeeId { get; set; }

	/// <summary>The client the employee works for during this period.</summary>
	public int AccountId { get; set; }

	public DateOnly EffectiveFrom { get; set; }
	public DateOnly? EffectiveTo { get; set; }

	public int? CreatedByEmployeeId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }

	public Employee? Employee { get; set; }
	public Account? Account { get; set; }
	public Employee? CreatedBy { get; set; }
}
