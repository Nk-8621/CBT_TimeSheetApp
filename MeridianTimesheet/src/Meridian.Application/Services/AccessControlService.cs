using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;

namespace Meridian.Application.Services;

public class AccessControlService(IEmployeeRepository employeeRepository) : IAccessControlService
{
	/// <summary>Designations that exempt someone from filling their own
	/// timesheet even if they technically still have a manager above them
	/// (e.g. a Vice President a level below the SVP). Being at the very top
	/// of the hierarchy (no manager at all) always exempts someone too,
	/// regardless of designation — see IsTopOfHierarchy below.</summary>
	private static readonly string[] SeniorLeadershipDesignations = ["Vice President", "Chief"];

	public async Task<AccessProfileDto> GetAccessProfileAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

		var isAdmin = await employeeRepository.HasRoleAsync(employee.EmployeeId, "ADMIN", ct);
		var directReports = await employeeRepository.GetDirectReportsAsync(employee.EmployeeId, ct);
		var isTopOfHierarchy = employee.ManagerEmployeeId is null;

		var isSeniorLeadership = isTopOfHierarchy
			|| SeniorLeadershipDesignations.Any(d => employee.Designation.Contains(d, StringComparison.OrdinalIgnoreCase));
		var requiresTimesheet = !isSeniorLeadership;

		// If I'm at the top of the hierarchy, my direct reports' submissions
		// skip Level 1 entirely and come straight to me as a Level 2 action
		// (see WeekApprovalService.DetermineApprovalRoutingAsync) — so I
		// never do Level 1 approval for them.
		var isLevel1ApproverForSomeone = directReports.Count > 0 && !isTopOfHierarchy;

		// I'm someone's Level 2 approver if either: I'm at the top and have
		// direct reports (the skip-Level-1 case above), or at least one of my
		// direct reports has their own direct reports (a normal "manager's
		// manager" relationship).
		var isLevel2ApproverForSomeone = isTopOfHierarchy && directReports.Count > 0;
		if (!isLevel2ApproverForSomeone)
		{
			foreach (var report in directReports)
			{
				var grandReports = await employeeRepository.GetDirectReportsAsync(report.EmployeeId, ct);
				if (grandReports.Count > 0) { isLevel2ApproverForSomeone = true; break; }
			}
		}

		var nav = new List<string>();
		if (requiresTimesheet) { nav.Add("ts"); nav.Add("hist"); }
		// WFH/Leave requests: everyone who fills a timesheet can ask for one -
		// including a manager asking for their own, which goes to their own
		// manager exactly like a normal approval does.
		if (requiresTimesheet) nav.Add("req");
		if (isLevel1ApproverForSomeone) nav.Add("ap1");
		if (isLevel2ApproverForSomeone) nav.Add("ap2");
		// Request approval is Level 1 only - there's no Level 2 step for these.
		if (isLevel1ApproverForSomeone) nav.Add("reqap");
		if (isLevel1ApproverForSomeone || isLevel2ApproverForSomeone || isAdmin) { nav.Add("team"); nav.Add("rep"); }
		if (isAdmin) nav.Add("mast");
		nav.Add("notif");

		return new AccessProfileDto(employee.EmployeeCode, isAdmin, requiresTimesheet, isLevel1ApproverForSomeone, isLevel2ApproverForSomeone, nav);
	}
}