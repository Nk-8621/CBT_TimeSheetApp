using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;

namespace Meridian.Application.Services;

public class EmployeeService(IEmployeeRepository employeeRepository) : IEmployeeService
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
			managerName
		);
	}
}
