using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;

namespace Meridian.Application.Services;

public class EmployeeService(IEmployeeRepository employeeRepository, IMasterDataRepository masterDataRepository) : IEmployeeService
{
	public async Task<EmployeeDto?> GetByCodeAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct);
		return employee is null ? null : await ToDtoAsync(employee, ct);
	}

	public async Task<EmployeeDto?> GetManagerAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct);
		if (employee?.ManagerEmployeeId is not int managerId) return null;

		var manager = await employeeRepository.GetByIdAsync(managerId, ct);
		return manager is null ? null : await ToDtoAsync(manager, ct);
	}

	public async Task<EmployeeDto?> GetSkipManagerAsync(string employeeCode, CancellationToken ct = default)
	{
		var manager = await GetManagerAsync(employeeCode, ct);
		return manager is null ? null : await GetManagerAsync(manager.EmployeeCode, ct);
	}

	public async Task<IReadOnlyList<EmployeeDto>> GetDirectReportsAsync(string managerEmployeeCode, CancellationToken ct = default)
	{
		var manager = await employeeRepository.GetByCodeAsync(managerEmployeeCode, ct);
		if (manager is null) return [];

		// Every direct report's manager is, by definition, this same manager -
		// no need to look the name up again per report.
		var reports = await employeeRepository.GetDirectReportsAsync(manager.EmployeeId, ct);
		return reports.Select(report => ToDto(report, manager.FullName)).ToList();
	}

	public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken ct = default)
	{
		// Everyone is already loaded, so manager names come from this same list
		// instead of one extra query per employee.
		var everyone = await employeeRepository.GetAllAsync(ct);
		var nameById = everyone.ToDictionary(e => e.EmployeeId, e => e.FullName);

		return everyone.Select(employee =>
		{
			string? managerName = null;
			if (employee.ManagerEmployeeId is int managerId && nameById.TryGetValue(managerId, out var name))
				managerName = name;
			return ToDto(employee, managerName);
		}).ToList();
	}

	private async Task<EmployeeDto> ToDtoAsync(Employee employee, CancellationToken ct)
	{
		string? managerName = null;
		if (employee.ManagerEmployeeId is int managerId)
		{
			var manager = await employeeRepository.GetByIdAsync(managerId, ct);
			managerName = manager?.FullName;
		}

		return ToDto(employee, managerName);
	}

	private static EmployeeDto ToDto(Employee employee, string? managerName)
	{
		return new EmployeeDto(
		employee.EmployeeId,
		employee.EmployeeCode,
		employee.FullName,
		employee.Initials,
		employee.DepartmentId,
		employee.LocationId,
		employee.Designation,
		employee.Grade,
		employee.ManagerEmployeeId,
		managerName,
		employee.IsActive,
		employee.PrimaryAccountId,
		employee.Email
		);
	}

	public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken ct = default)
	{
		ValidateBasicDetails(request.FullName, request.Email, request.Designation);
		await EnsureEmailNotInUseAsync(request.Email, excludeEmployeeId: null, ct);

		var manager = await employeeRepository.GetByCodeAsync(request.ManagerEmployeeCode, ct)
			?? throw new BusinessRuleException($"Manager '{request.ManagerEmployeeCode}' not found.");

		string employeeCode;
		if (request.IsExternal)
		{
			employeeCode = await GenerateNextExternalCodeAsync(ct);
		}
		else
		{
			if (string.IsNullOrWhiteSpace(request.EmployeeCode))
				throw new BusinessRuleException("Employee code is required for internal employees.");
			if (await employeeRepository.GetByCodeAsync(request.EmployeeCode, ct) is not null)
				throw new BusinessRuleException($"Employee code '{request.EmployeeCode}' is already in use.");
			employeeCode = request.EmployeeCode;
		}

		// Validate every requested allocation up front, before anything is
		// written � a bad project id or an invalid classification/category
		// should fail the whole create, not leave a half-allocated employee
		// behind.
		var allocations = (request.Allocations ?? []).ToList();
		var allocationProjectIds = allocations.Select(a => a.ProjectId).ToList();
		if (allocationProjectIds.Distinct().Count() != allocationProjectIds.Count)
			throw new BusinessRuleException("Each project can only be assigned once.");
		foreach (var allocation in allocations)
		{
			_ = await masterDataRepository.GetProjectByIdAsync(allocation.ProjectId, ct)
				?? throw new EntityNotFoundException(nameof(Project), allocation.ProjectId);
			BillingClassificationRules.Validate(allocation.Classification, allocation.BillingCategory);
		}

		var employee = new Employee
		{
			EmployeeCode = employeeCode,
			FullName = request.FullName,
			Initials = ComputeInitials(request.FullName),
			JobTitleRaw = request.Designation, // no separate "raw" source for a freshly created record
			Email = request.Email,
			Designation = request.Designation,
			DepartmentId = request.DepartmentId,
			LocationId = manager.LocationId, // new employees default to their manager's location; there's no location picker on the create form
			ManagerEmployeeId = manager.EmployeeId,
			IsExternal = request.IsExternal,
			LoginAccessGrantedAt = DateTime.UtcNow,
		};

		await employeeRepository.AddAsync(employee, ct);
		await employeeRepository.SaveChangesAsync(ct); // populates employee.EmployeeId before an allocation can reference it

		foreach (var allocation in allocations)
			await employeeRepository.AddProjectAllocationAsync(new EmployeeProjectAllocation
			{
				EmployeeId = employee.EmployeeId,
				ProjectId = allocation.ProjectId,
				Classification = allocation.Classification,
				BillingCategory = allocation.BillingCategory,
				AssignedAt = DateTime.UtcNow,
			}, ct);
		if (allocations.Count > 0)
			await employeeRepository.SaveChangesAsync(ct);

		return await ToDtoAsync(employee, ct);
	}

	private async Task<string> GenerateNextExternalCodeAsync(CancellationToken ct)
	{
		var everyone = await employeeRepository.GetAllAsync(ct);
		var maxNumber = everyone
			.Where(e => e.EmployeeCode.StartsWith("EXT", StringComparison.OrdinalIgnoreCase))
			.Select(e => int.TryParse(e.EmployeeCode.AsSpan(3), out var n) ? n : 0)
			.DefaultIfEmpty(0)
			.Max();
		return $"EXT{(maxNumber + 1):D4}";
	}

	private static string ComputeInitials(string fullName) =>
	string.Concat(fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

	public async Task SetPrimaryAccountAsync(string employeeCode, int? accountId, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);
		employee.PrimaryAccountId = accountId;
		await employeeRepository.SaveChangesAsync(ct);
	}

	public async Task<EmployeeDto> UpdateEmployeeAsync(string employeeCode, UpdateEmployeeRequest request, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

		ValidateBasicDetails(request.FullName, request.Email, request.Designation);
		await EnsureEmailNotInUseAsync(request.Email, excludeEmployeeId: employee.EmployeeId, ct);
		await EnsureDepartmentExistsAsync(request.DepartmentId, ct);

		var manager = await employeeRepository.GetByCodeAsync(request.ManagerEmployeeCode, ct)
			?? throw new BusinessRuleException($"Manager '{request.ManagerEmployeeCode}' not found.");
		await EnsureValidManagerAsync(employee, manager, ct);

		employee.FullName = request.FullName.Trim();
		employee.Initials = ComputeInitials(employee.FullName);
		employee.Email = request.Email.Trim();
		// JobTitleRaw is deliberately left alone - it's the original HR source
		// string, kept for audit, not the display value.
		employee.Designation = request.Designation.Trim();
		employee.DepartmentId = request.DepartmentId;
		employee.ManagerEmployeeId = manager.EmployeeId;
		employee.UpdatedAt = DateTime.UtcNow;

		await employeeRepository.SaveChangesAsync(ct);
		return await ToDtoAsync(employee, ct);
	}

	// ---- Shared validation for create + update ----

	private static void ValidateBasicDetails(string fullName, string email, string designation)
	{
		if (string.IsNullOrWhiteSpace(fullName))
			throw new BusinessRuleException("Full name is required.");
		if (string.IsNullOrWhiteSpace(email))
			throw new BusinessRuleException("Email is required.");
		if (!email.Contains('@'))
			throw new BusinessRuleException("Enter a valid email address.");
		if (string.IsNullOrWhiteSpace(designation))
			throw new BusinessRuleException("Designation is required.");
	}

	/// <summary>First sign-in links a Microsoft account to an Employee by email
	/// (see CurrentUserService), so two employees sharing one email would make
	/// that link pick whichever row the database returns first.</summary>
	private async Task EnsureEmailNotInUseAsync(string email, int? excludeEmployeeId, CancellationToken ct)
	{
		var trimmedEmail = email.Trim();
		var everyone = await employeeRepository.GetAllAsync(ct);
		var emailTaken = everyone.Any(e =>
			e.EmployeeId != excludeEmployeeId
			&& string.Equals(e.Email, trimmedEmail, StringComparison.OrdinalIgnoreCase));

		if (emailTaken)
			throw new BusinessRuleException($"An employee with email '{trimmedEmail}' already exists.");
	}

	private async Task EnsureDepartmentExistsAsync(int departmentId, CancellationToken ct)
	{
		var departments = await masterDataRepository.GetDepartmentsAsync(ct);
		if (!departments.Any(d => d.DepartmentId == departmentId))
			throw new EntityNotFoundException(nameof(Department), departmentId);
	}

	/// <summary>The new manager must be an active employee, not the employee
	/// themselves, and not someone who (directly or further down) reports to
	/// this employee - that would create a loop in the approval chain.</summary>
	private async Task EnsureValidManagerAsync(Employee employee, Employee manager, CancellationToken ct)
	{
		if (manager.EmployeeId == employee.EmployeeId)
			throw new BusinessRuleException("An employee can't be their own manager.");
		if (!manager.IsActive)
			throw new BusinessRuleException($"{manager.FullName} is inactive and can't be a manager.");

		// Walk up from the proposed manager. Reaching this employee means the
		// manager already sits somewhere below them. The step limit only guards
		// against bad existing data that already contains a loop.
		var current = manager;
		for (var step = 0; step < 50 && current.ManagerEmployeeId is int nextManagerId; step++)
		{
			if (nextManagerId == employee.EmployeeId)
				throw new BusinessRuleException($"{manager.FullName} reports to {employee.FullName}, so can't also be their manager.");

			var next = await employeeRepository.GetByIdAsync(nextManagerId, ct);
			if (next is null) break;
			current = next;
		}
	}

	public async Task DeactivateEmployeeAsync(string employeeCode, string deactivatedByEmployeeCode, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

		if (!employee.IsActive)
			throw new BusinessRuleException($"{employeeCode} is already inactive.");
		// Deactivation now also blocks sign-in (see CurrentUserService), so an
		// admin deactivating themselves would lock themselves out immediately.
		if (string.Equals(employeeCode, deactivatedByEmployeeCode, StringComparison.OrdinalIgnoreCase))
			throw new BusinessRuleException("You can't deactivate your own account - ask another administrator.");

		var actingAdmin = await employeeRepository.GetByCodeAsync(deactivatedByEmployeeCode, ct);

		// Auto-reassign direct reports to this manager's own manager (skip-level).
		var directReports = await employeeRepository.GetDirectReportsAsync(employee.EmployeeId, ct);
		foreach (var report in directReports)
			report.ManagerEmployeeId = employee.ManagerEmployeeId;

		employee.IsActive = false;
		employee.DeactivatedAt = DateTime.UtcNow;
		employee.DeactivatedByEmployeeId = actingAdmin?.EmployeeId;

		await employeeRepository.SaveChangesAsync(ct);
	}

	public async Task ReactivateEmployeeAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

		employee.IsActive = true;
		employee.DeactivatedAt = null;
		employee.DeactivatedByEmployeeId = null;
		// Deliberately NOT restoring former direct reports to this manager �
		// they were already reassigned; someone can manually move them back if
		// that's actually wanted.
		await employeeRepository.SaveChangesAsync(ct);
	}

	// ---- Project allocation ----

	public async Task<IReadOnlyList<EmployeeProjectAllocationDto>> GetProjectAllocationsAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

		var allocations = await employeeRepository.GetProjectAllocationsAsync(employee.EmployeeId, ct);
		return allocations
			.Select(a => new EmployeeProjectAllocationDto(
				a.ProjectId,
				a.Project?.Code ?? "",
				a.Project?.Name ?? "",
				a.Classification,
				a.BillingCategory))
			.ToList();
	}

	public async Task SetProjectAllocationsAsync(string employeeCode, SetEmployeeProjectAllocationsRequest request, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

		var requested = request.Allocations ?? [];
		var requestedProjectIds = requested.Select(a => a.ProjectId).ToList();
		if (requestedProjectIds.Distinct().Count() != requestedProjectIds.Count)
			throw new BusinessRuleException("Each project can only be assigned once.");

		foreach (var allocation in requested)
		{
			_ = await masterDataRepository.GetProjectByIdAsync(allocation.ProjectId, ct)
				?? throw new EntityNotFoundException(nameof(Project), allocation.ProjectId);
			BillingClassificationRules.Validate(allocation.Classification, allocation.BillingCategory);
		}

		var current = await employeeRepository.GetProjectAllocationsAsync(employee.EmployeeId, ct);
		var currentByProjectId = current.ToDictionary(a => a.ProjectId);
		var requestedIds = requestedProjectIds.ToHashSet();

		var toRemove = current.Where(a => !requestedIds.Contains(a.ProjectId)).ToList();
		employeeRepository.RemoveProjectAllocations(toRemove);

		foreach (var allocation in requested)
		{
			if (currentByProjectId.TryGetValue(allocation.ProjectId, out var existing))
			{
				// Already allocated - just refresh the classification/category
				// admin chose this time, in place.
				existing.Classification = allocation.Classification;
				existing.BillingCategory = allocation.BillingCategory;
			}
			else
			{
				await employeeRepository.AddProjectAllocationAsync(new EmployeeProjectAllocation
				{
					EmployeeId = employee.EmployeeId,
					ProjectId = allocation.ProjectId,
					Classification = allocation.Classification,
					BillingCategory = allocation.BillingCategory,
					AssignedAt = DateTime.UtcNow,
				}, ct);
			}
		}

		await employeeRepository.SaveChangesAsync(ct);
	}
}
