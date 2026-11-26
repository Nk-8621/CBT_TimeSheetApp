namespace Meridian.Domain.Entities;

/// <summary>
/// PROJECT STORIES (Nov 2026): a user story name a Level 1 manager (or an
/// Admin) adds for a project. Everyone allocated to the project sees it when
/// creating a user story and can pick it - or type their own name instead.
/// Picking it creates the employee's own UserStory with this name (linked
/// through UserStory.ProjectStoryNameId); the employee still chooses the dates.
/// </summary>
public class ProjectStoryName
{
	public int ProjectStoryNameId { get; set; }
	public int ProjectId { get; set; }
	public required string Name { get; set; }

	/// <summary>False = closed: no longer offered for new stories. Stories
	/// already made from it are not affected.</summary>
	public bool IsActive { get; set; } = true;

	public int? CreatedByEmployeeId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }

	public Project? Project { get; set; }
	public Employee? CreatedBy { get; set; }
}
