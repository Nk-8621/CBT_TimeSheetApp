using Meridian.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Repositories
{
	public interface INotificationRepository
	{
		/// <summary>Personal notifications for this employee plus broadcast ones
		/// (EmployeeId IS NULL), newest first.</summary>
		Task<IReadOnlyList<Notification>> GetForEmployeeAsync(int employeeId, CancellationToken ct = default);

		Task<Notification?> GetByIdAsync(int notificationId, CancellationToken ct = default);
		Task SaveChangesAsync(CancellationToken ct = default);
	}
}
