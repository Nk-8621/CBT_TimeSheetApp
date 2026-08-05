using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class TeamService(
	IEmployeeRepository employeeRepository,
	ITimeEntryRepository timeEntryRepository,
	IWeekRecordRepository weekRecordRepository,
	IMasterDataRepository masterDataRepository,
	IDayTypeResolutionService dayTypeResolutionService) : ITeamService
{
	public async Task<IReadOnlyList<TeamComplianceRowDto>> GetComplianceForManagerAsync(string managerEmployeeCode, DateOnly weekStart, CancellationToken ct = default)
	{
		var manager = await employeeRepository.GetByCodeAsync(managerEmployeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), managerEmployeeCode);

		var reports = await employeeRepository.GetDirectReportsAsync(manager.EmployeeId, ct);
		return await BuildRowsAsync(reports, weekStart, ct);
	}

	public async Task<IReadOnlyList<TeamComplianceRowDto>> GetComplianceForAllAsync(DateOnly weekStart, CancellationToken ct = default)
	{
		var everyone = await employeeRepository.GetAllAsync(ct);
		return await BuildRowsAsync(everyone, weekStart, ct);
	}

	private async Task<IReadOnlyList<TeamComplianceRowDto>> BuildRowsAsync(IReadOnlyList<Employee> employees, DateOnly weekStart, CancellationToken ct)
	{
		var departments = await masterDataRepository.GetDepartmentsAsync(ct);
		var deptById = departments.ToDictionary(d => d.DepartmentId);

		var rows = new List<TeamComplianceRowDto>(employees.Count);
		foreach (var employee in employees)
		{
			var entries = await timeEntryRepository.GetForWeekAsync(employee.EmployeeId, weekStart, ct);
			var week = await weekRecordRepository.GetAsync(employee.EmployeeId, weekStart, ct);
			var dayTypeDtos = await dayTypeResolutionService.ResolveWeekAsync(employee.EmployeeId, weekStart, ct);

			var dailyHours = new decimal[7];
			for (var i = 0; i < 7; i++)
				dailyHours[i] = entries.Sum(e => e.HoursByDay[i]);

			var total = dailyHours.Sum();
			var billable = entries.Where(e => e.IsBillable).Sum(e => e.TotalHours);
			var capacity = dayTypeDtos.Sum(d => d.CapacityHours);
			var hasLogged = entries.Count > 0;
			var status = !hasLogged && week is null ? "NotStarted" : (week?.Status ?? WeekStatus.Draft).ToString();
			var departmentName = deptById.TryGetValue(employee.DepartmentId, out var dept) ? dept.Name : "—";

			rows.Add(new TeamComplianceRowDto(
				employee.EmployeeCode, employee.FullName, employee.Designation, departmentName,
				status, total, hasLogged,
				dailyHours, dayTypeDtos.Select(d => d.DayType).ToArray(), capacity,
				billable, total - billable
			));
		}
		return rows.OrderBy(r => r.FullName).ToList();
	}
}