using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;

namespace Meridian.Application.Services;

public class EmployeeService(IEmployeeRepository employeeRepository, IPasswordHasher passwordHasher) : IEmployeeService
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

		var reports = await employeeRepository.GetDirectReportsAsync(manager.EmployeeId, ct);
		var dtos = new List<EmployeeDto>(reports.Count);
		foreach (var report in reports) dtos.Add(await ToDtoAsync(report, ct));
		return dtos;
	}

	public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken ct = default)
	{
		var everyone = await employeeRepository.GetAllAsync(ct);
		var dtos = new List<EmployeeDto>(everyone.Count);
		foreach (var employee in everyone) dtos.Add(await ToDtoAsync(employee, ct));
		return dtos;
	}

	private async Task<EmployeeDto> ToDtoAsync(Employee employee, CancellationToken ct)
	{
		string? managerName = null;
		if (employee.ManagerEmployeeId is int managerId)
		{
			var manager = await employeeRepository.GetByIdAsync(managerId, ct);
			managerName = manager?.FullName;
		}

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
		employee.IsActive
		);
	}

	public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.FullName))
			throw new BusinessRuleException("Full name is required.");
		if (string.IsNullOrWhiteSpace(request.Email))
			throw new BusinessRuleException("Email is required.");

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

		var employee = new Employee
		{
			EmployeeCode = employeeCode,
			FullName = request.FullName,
			Initials = ComputeInitials(request.FullName),
			JobTitleRaw = request.Designation, // no separate "raw" source for a freshly created record
			Email = request.Email,
			Designation = request.Designation,
			DepartmentId = request.DepartmentId,
			ManagerEmployeeId = manager.EmployeeId,
			IsExternal = request.IsExternal,
			PasswordHash = passwordHasher.Hash("cbt@2026"),
			MustChangePassword = true,
			LoginAccessGrantedAt = DateTime.UtcNow,
		};

		await employeeRepository.AddAsync(employee, ct);
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

	public async Task DeactivateEmployeeAsync(string employeeCode, string deactivatedByEmployeeCode, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

		if (!employee.IsActive)
			throw new BusinessRuleException($"{employeeCode} is already inactive.");

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
		// Deliberately NOT restoring former direct reports to this manager —
		// they were already reassigned; someone can manually move them back if
		// that's actually wanted.
		await employeeRepository.SaveChangesAsync(ct);
	}
}
