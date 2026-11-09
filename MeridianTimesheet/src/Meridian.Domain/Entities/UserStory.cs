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
	public bool IsActive { get; set; } = true;
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }

	public Employee? Employee { get; set; }
	public Project? Project { get; set; }
}
