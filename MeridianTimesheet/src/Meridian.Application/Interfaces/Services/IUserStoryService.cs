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
}
