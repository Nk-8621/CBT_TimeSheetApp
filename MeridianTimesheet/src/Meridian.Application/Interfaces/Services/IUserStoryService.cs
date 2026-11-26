using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

/// <summary>Creating and listing an employee's user stories. Adding tasks
/// (timesheet lines) under a story is TimesheetService's job, so all the
/// existing timesheet rules apply to them in one place.</summary>
public interface IUserStoryService
{
	Task<IReadOnlyList<UserStoryDto>> GetForEmployeeAsync(string employeeCode, CancellationToken ct = default);
	Task<UserStoryDto> CreateAsync(string employeeCode, CreateUserStoryRequest request, CancellationToken ct = default);

	/// <summary>Changes a story's name and/or dates. Tasks already logged
	/// outside the new dates are kept as they are (history).</summary>
	Task<UserStoryDto> UpdateAsync(string employeeCode, int userStoryId, UpdateUserStoryRequest request, CancellationToken ct = default);

	/// <summary>Deletes a story that has no tasks in any week. A story with
	/// tasks can't be deleted - its tasks have to be removed first.</summary>
	Task DeleteAsync(string employeeCode, int userStoryId, CancellationToken ct = default);

	// ---- PROJECT STORIES (Nov 2026) ----
	/// <summary>Projects the user can add story names for, with their names:
	/// an Admin gets every active project; a manager (has direct reports) gets
	/// the active projects allocated to him; anyone else gets none.</summary>
	Task<IReadOnlyList<ManagedProjectDto>> GetManagedProjectsAsync(string employeeCode, bool isAdmin, CancellationToken ct = default);
	Task<ProjectStoryNameDto> AddProjectStoryNameAsync(string employeeCode, bool isAdmin, CreateProjectStoryNameRequest request, CancellationToken ct = default);
	Task<ProjectStoryNameDto> UpdateProjectStoryNameAsync(string employeeCode, bool isAdmin, int projectStoryNameId, UpdateProjectStoryNameRequest request, CancellationToken ct = default);
	/// <summary>Open story names of the projects this employee is allocated to,
	/// offered when he creates a user story.</summary>
	Task<IReadOnlyList<ProjectStoryNameDto>> GetAvailableProjectStoryNamesAsync(string employeeCode, CancellationToken ct = default);
}
