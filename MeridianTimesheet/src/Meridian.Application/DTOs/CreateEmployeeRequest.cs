using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.DTOs
{
	public record CreateEmployeeRequest(
	string FullName,
	string Email,
	string Designation,
	string ManagerEmployeeCode,
	int DepartmentId,
	bool IsExternal,
	string? EmployeeCode = null, // required when IsExternal is false; ignored (auto-generated) when true
	/// <summary>Projects to allocate this employee to right away, each with its
	/// mandatory Classification + BillingCategory (set by the admin here, not
	/// picked by the employee per timesheet line) - the same editor shown on
	/// the Add Employee form. Pass null/empty to skip (allocations can always
	/// be added later from the Resources tab).</summary>
	IReadOnlyList<EmployeeProjectAllocationInput>? Allocations = null
	);

	public record SetPrimaryAccountRequest(int? AccountId);

	/// <summary>Replaces an employee's full set of Project allocations in one
	/// call, each with its admin-set Classification + BillingCategory -
	/// IEmployeeService.SetProjectAllocationsAsync adds/updates whatever's
	/// listed here and removes whatever's no longer listed. Backs the
	/// "Projects" editor on the Master Data Resources tab
	/// (EmployeeAllocationsDrawer on the frontend).</summary>
	public record SetEmployeeProjectAllocationsRequest(IReadOnlyList<EmployeeProjectAllocationInput> Allocations);
}
