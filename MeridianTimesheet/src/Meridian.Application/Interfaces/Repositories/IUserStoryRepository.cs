using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

public interface IUserStoryRepository
{
	/// <summary>All of one employee's user stories (newest first), with Project loaded.</summary>
	Task<IReadOnlyList<UserStory>> GetForEmployeeAsync(int employeeId, CancellationToken ct = default);
	Task<UserStory?> GetByIdAsync(int userStoryId, CancellationToken ct = default);
	Task AddAsync(UserStory userStory, CancellationToken ct = default);
	void Remove(UserStory userStory);

	/// <summary>Number of tasks (timesheet lines) per story, in any week, for one
	/// employee's stories. Stories with no tasks are not in the dictionary.</summary>
	Task<Dictionary<int, int>> GetTaskCountsAsync(int employeeId, CancellationToken ct = default);
	Task SaveChangesAsync(CancellationToken ct = default);
}
