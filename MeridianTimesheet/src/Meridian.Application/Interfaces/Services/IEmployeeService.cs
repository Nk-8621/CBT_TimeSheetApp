using Meridian.Application.DTOs;
using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Services;

public interface IEmployeeService
{
	Task<EmployeeDto?> GetByCodeAsync(string employeeCode, CancellationToken ct = default);

	/// <summary>Direct manager — the Level 1 approver.</summary>
	Task<EmployeeDto?> GetManagerAsync(string employeeCode, CancellationToken ct = default);

	/// <summary>Manager's manager — the Level 2 approver. Computed by walking the
	/// real hierarchy two steps up, since Meridian doesn't store a fixed L2 column.</summary>
	Task<EmployeeDto?> GetSkipManagerAsync(string employeeCode, CancellationToken ct = default);

	Task<IReadOnlyList<EmployeeDto>> GetDirectReportsAsync(string managerEmployeeCode, CancellationToken ct = default);

	/// <summary>Every employee — callers must check Admin status themselves
	/// before calling this (same enforcement pattern as the rest of the API).</summary>
	Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken ct = default);

	/// <summary>Creates a new employee — internal (real HR-assigned EmployeeCode
	/// required) or external (synthetic EXT#### code auto-generated). Grants
	/// portal access immediately with the same default password + forced
	/// first-login flow as everyone else. If Allocations is supplied, also
	/// allocates the new employee to those Projects (with their mandatory
	/// Classification/BillingCategory) right away.</summary>
	Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken ct = default);

	/// <summary>Sets which client's holiday calendar this employee follows.
	/// AccountId null means "no specific client - company-wide holidays only".</summary>
	Task SetPrimaryAccountAsync(string employeeCode, int? accountId, CancellationToken ct = default);

	/// <summary>Updates name, email, designation, department and manager.
	/// Changing the manager takes effect immediately - including for weeks
	/// already waiting for approval, since approvers are worked out live from
	/// the reporting line.</summary>
	Task<EmployeeDto> UpdateEmployeeAsync(string employeeCode, UpdateEmployeeRequest request, CancellationToken ct = default);

	Task DeactivateEmployeeAsync(string employeeCode, string deactivatedByEmployeeCode, CancellationToken ct = default);
	Task ReactivateEmployeeAsync(string employeeCode, CancellationToken ct = default);

	// ---- Project allocation (which Projects this employee is actually working on) ----

	/// <summary>This employee's current project allocations, each with its
	/// admin-set Classification/BillingCategory - backs the allocation editor
	/// on the Resources tab.</summary>
	Task<IReadOnlyList<EmployeeProjectAllocationDto>> GetProjectAllocationsAsync(string employeeCode, CancellationToken ct = default);

	/// <summary>Replaces this employee's full set of project allocations with
	/// exactly the IDs supplied (adds new ones, removes any no longer ticked).</summary>
	Task SetProjectAllocationsAsync(string employeeCode, SetEmployeeProjectAllocationsRequest request, CancellationToken ct = default);
}
