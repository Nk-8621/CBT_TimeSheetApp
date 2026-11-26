using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class UserStoryRepository(MeridianDbContext db) : IUserStoryRepository
{
	public async Task<IReadOnlyList<UserStory>> GetForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
		await db.UserStories
			.Include(s => s.Project)
			.Where(s => s.EmployeeId == employeeId)
			.OrderByDescending(s => s.CreatedAt)
			.ToListAsync(ct);

	public Task<UserStory?> GetByIdAsync(int userStoryId, CancellationToken ct = default) =>
		db.UserStories
			.Include(s => s.Project)
			.FirstOrDefaultAsync(s => s.UserStoryId == userStoryId, ct);

	public async Task AddAsync(UserStory userStory, CancellationToken ct = default) =>
		await db.UserStories.AddAsync(userStory, ct);

	public void Remove(UserStory userStory) => db.UserStories.Remove(userStory);

	public Task<Dictionary<int, int>> GetTaskCountsAsync(int employeeId, CancellationToken ct = default) =>
		db.TimeEntries
			.Where(t => t.EmployeeId == employeeId && t.UserStoryId != null)
			.GroupBy(t => t.UserStoryId!.Value)
			.Select(g => new { UserStoryId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.UserStoryId, x => x.Count, ct);

	// ---- PROJECT STORIES (Nov 2026) ----
	public async Task<IReadOnlyList<ProjectStoryName>> GetProjectStoryNamesAsync(IReadOnlyCollection<int> projectIds, CancellationToken ct = default) =>
		await db.ProjectStoryNames
			.Include(n => n.CreatedBy)
			.Where(n => projectIds.Contains(n.ProjectId))
			.OrderBy(n => n.Name)
			.ToListAsync(ct);

	public Task<ProjectStoryName?> GetProjectStoryNameByIdAsync(int projectStoryNameId, CancellationToken ct = default) =>
		db.ProjectStoryNames.Include(n => n.CreatedBy).FirstOrDefaultAsync(n => n.ProjectStoryNameId == projectStoryNameId, ct);

	public async Task AddProjectStoryNameAsync(ProjectStoryName storyName, CancellationToken ct = default) =>
		await db.ProjectStoryNames.AddAsync(storyName, ct);

	public Task<Dictionary<int, int>> GetProjectStoryNameUsageAsync(IReadOnlyCollection<int> projectStoryNameIds, CancellationToken ct = default) =>
		db.UserStories
			.Where(s => s.ProjectStoryNameId != null && projectStoryNameIds.Contains(s.ProjectStoryNameId.Value))
			.GroupBy(s => s.ProjectStoryNameId!.Value)
			.Select(g => new { Id = g.Key, Count = g.Select(s => s.EmployeeId).Distinct().Count() })
			.ToDictionaryAsync(x => x.Id, x => x.Count, ct);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
