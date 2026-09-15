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
	/// <summary>Projects to allocate this employee to right away - the same
	/// checkbox list shown on the Add Employee form. Pass null/empty to skip
	/// (allocations can always be added later from Edit Employee).</summary>
	IReadOnlyList<int>? ProjectIds = null
	);

	public record SetPrimaryAccountRequest(int? AccountId);

	/// <summary>Replaces an employee's full set of Project allocations in one
	/// call - IEmployeeService.SetProjectAllocationsAsync adds whatever's newly
	/// listed here and removes whatever's no longer listed. Backs the "Projects"
	/// editor on the Master Data Resources tab (EmployeeAllocationsDrawer on the
	/// frontend).</summary>
	public record SetEmployeeProjectAllocationsRequest(IReadOnlyList<int> ProjectIds);
}
