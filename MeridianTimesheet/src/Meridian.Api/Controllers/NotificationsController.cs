using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers
{
	[ApiController]
	[Route("api/notifications")]
	[Authorize]
	public class NotificationsController(INotificationService notificationService, ICurrentUserService currentUser) : ControllerBase
	{
		/// <summary>The calling employee's own notifications (personal + broadcast).</summary>
		[HttpGet]
		public async Task<IActionResult> GetMine(CancellationToken ct)
		{
			if (currentUser.EmployeeCode is null) return Unauthorized();
			return Ok(await notificationService.GetForEmployeeAsync(currentUser.EmployeeCode, ct));
		}

		[HttpPut("{notificationId:int}/read")]
		public async Task<IActionResult> MarkAsRead(int notificationId, CancellationToken ct)
		{
			if (currentUser.EmployeeCode is null) return Unauthorized();
			await notificationService.MarkAsReadAsync(notificationId, currentUser.EmployeeCode, ct);
			return NoContent();
		}
	}
}
