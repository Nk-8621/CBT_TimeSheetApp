using Meridian.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public interface INotificationService
	{
		Task<IReadOnlyList<NotificationDto>> GetForEmployeeAsync(string employeeCode, CancellationToken ct = default);

		/// <summary>Only personal notifications can be marked read — a broadcast
		/// notification is shared across everyone, so "read" doesn't have a
		/// well-defined per-person meaning without a separate read-receipts
		/// table, which doesn't exist yet. Throws BusinessRuleException if
		/// called on a broadcast notification.</summary>
		Task MarkAsReadAsync(int notificationId, string employeeCode, CancellationToken ct = default);
	}
}
