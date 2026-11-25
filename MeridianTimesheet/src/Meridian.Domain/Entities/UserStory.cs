namespace Meridian.Domain.Entities;

/// <summary>
/// A piece of work an employee groups their tasks under, e.g. "Employee
/// management module". Each story belongs to one employee and one project.
/// Tasks are ordinary timesheet lines (TimeEntry) that point back to their
/// story through TimeEntry.UserStoryId - the task name is the line's Note.
/// </summary>
public class UserStory
{
	public int UserStoryId { get; set; }
	public int EmployeeId { get; set; }
	public int ProjectId { get; set; }
	public required string Name { get; set; }

	/// <summary>First and last day the story runs. Both required; EndDate is
	/// never before StartDate. Tasks (hours) can only be added on days inside
	/// this range, and the story shows only in weeks that overlap it (plus any
	/// week where it already has tasks, as history).</summary>
	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }

	public Employee? Employee { get; set; }
	public Project? Project { get; set; }
}
