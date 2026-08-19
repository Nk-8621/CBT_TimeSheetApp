using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class OtpRepository(MeridianDbContext db) : IOtpRepository
{
	public Task<Otp?> GetMostRecentAsync(int employeeId, OtpPurpose purpose, CancellationToken ct = default) =>
		db.Otps
			.Where(o => o.EmployeeId == employeeId && o.Purpose == purpose)
			.OrderByDescending(o => o.CreatedAt)
			.FirstOrDefaultAsync(ct);

	public async Task AddAsync(Otp otp, CancellationToken ct = default) =>
		await db.Otps.AddAsync(otp, ct);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}