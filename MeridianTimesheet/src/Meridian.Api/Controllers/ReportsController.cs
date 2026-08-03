using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers
{
	[ApiController]
	[Route("api/reports")]
	[Authorize]
	public class ReportsController(IReportsService reportsService, IEmployeeService employeeService, ICurrentUserService currentUser) : ControllerBase
	{
		/// <summary>Hours by project for one week — Admin sees the whole
		/// organization, everyone else sees their own direct reports.</summary>
		[HttpGet("project-hours")]
		public async Task<IActionResult> GetProjectHours([FromQuery] DateOnly weekStart, CancellationToken ct)
		{
			if (currentUser.EmployeeCode is null) return Unauthorized();

			var scopeEmployees = currentUser.IsAdmin
				? await employeeService.GetAllAsync(ct)
				: await employeeService.GetDirectReportsAsync(currentUser.EmployeeCode, ct);

			var employeeIds = scopeEmployees.Select(e => e.Id).ToList();
			return Ok(await reportsService.GetProjectHoursAsync(employeeIds, weekStart, ct));
		}
	}
}
