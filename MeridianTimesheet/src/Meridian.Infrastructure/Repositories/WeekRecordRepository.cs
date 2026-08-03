using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class WeekRecordRepository(MeridianDbContext db) : IWeekRecordRepository
{
	public Task<WeekRecord?> GetAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default) =>
		db.WeekRecords
			.Include(w => w.ApprovalEvents)
			.Include(w => w.RejectedBy)
			.FirstOrDefaultAsync(w => w.EmployeeId == employeeId && w.WeekStartDate == weekStartDate, ct);

	public async Task<WeekRecord> GetOrCreateAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default)
	{
		var existing = await GetAsync(employeeId, weekStartDate, ct);
		if (existing is not null) return existing;

		var created = new WeekRecord
		{
			EmployeeId = employeeId,
			WeekStartDate = weekStartDate,
			Status = WeekStatus.Draft,
			CreatedAt = DateTime.UtcNow,
		};
		await db.WeekRecords.AddAsync(created, ct);
		// Intentionally not saved here — caller (service layer) saves once,
		// after also adding whatever ApprovalEvents belong to this change.
		return created;
	}

	public async Task<IReadOnlyList<WeekRecord>> GetByStatusAsync(WeekStatus status, CancellationToken ct = default) =>
		await db.WeekRecords
			.Include(w => w.ApprovalEvents)
			.Where(w => w.Status == status)
			.ToListAsync(ct);

	public async Task<IReadOnlyList<WeekRecord>> GetAllForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
		await db.WeekRecords
			.Where(w => w.EmployeeId == employeeId)
			.OrderByDescending(w => w.WeekStartDate)
			.ToListAsync(ct);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
