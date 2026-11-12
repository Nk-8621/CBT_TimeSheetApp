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

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
