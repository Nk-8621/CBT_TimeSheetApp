using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Services
{
	public class NotificationService(
	INotificationRepository notificationRepository,
	IEmployeeRepository employeeRepository) : INotificationService
	{
		public async Task<IReadOnlyList<NotificationDto>> GetForEmployeeAsync(string employeeCode, CancellationToken ct = default)
		{
			var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
				?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

			var notifications = await notificationRepository.GetForEmployeeAsync(employee.EmployeeId, ct);
			return notifications
				.OrderByDescending(n => n.CreatedAt)
				.Select(ToDto)
				.ToList();
		}

		public async Task MarkAsReadAsync(int notificationId, string employeeCode, CancellationToken ct = default)
		{
			var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
				?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

			var notification = await notificationRepository.GetByIdAsync(notificationId, ct)
				?? throw new EntityNotFoundException(nameof(Notification), notificationId);

			if (notification.EmployeeId is null)
				throw new BusinessRuleException("Broadcast notifications can't be marked read individually.");

			if (notification.EmployeeId != employee.EmployeeId)
				throw new BusinessRuleException("This notification doesn't belong to you.");

			notification.ReadAt = DateTime.UtcNow;
			await notificationRepository.SaveChangesAsync(ct);
		}

		private static NotificationDto ToDto(Notification n) => new(
			n.NotificationId,
			n.Title,
			n.Message,
			n.NotificationKind.ToString(),
			n.CreatedAt,
			n.ReadAt,
			n.EmployeeId is null
		);
	}
}
