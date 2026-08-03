using Meridian.Application.DTOs;

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
}
