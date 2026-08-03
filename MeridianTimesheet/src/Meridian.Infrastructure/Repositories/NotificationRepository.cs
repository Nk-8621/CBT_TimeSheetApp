using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Infrastructure.Repositories
{
	public class NotificationRepository(MeridianDbContext db) : INotificationRepository
	{
		public async Task<IReadOnlyList<Notification>> GetForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
			await db.Notifications
				.Where(n => n.EmployeeId == employeeId || n.EmployeeId == null)
				.OrderByDescending(n => n.CreatedAt)
				.ToListAsync(ct);

		public Task<Notification?> GetByIdAsync(int notificationId, CancellationToken ct = default) =>
			db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId, ct);

		public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
	}
}
