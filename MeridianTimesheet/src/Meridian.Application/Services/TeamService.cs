using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Services
{
	public class TeamService(
	IEmployeeRepository employeeRepository,
	ITimeEntryRepository timeEntryRepository,
	IWeekRecordRepository weekRecordRepository) : ITeamService
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
			var rows = new List<TeamComplianceRowDto>(employees.Count);
			foreach (var employee in employees)
			{
				var entries = await timeEntryRepository.GetForWeekAsync(employee.EmployeeId, weekStart, ct);
				var week = await weekRecordRepository.GetAsync(employee.EmployeeId, weekStart, ct);
				var total = entries.Sum(e => e.TotalHours);
				var hasLogged = entries.Count > 0;
				var status = !hasLogged && week is null ? "NotStarted" : (week?.Status ?? WeekStatus.Draft).ToString();

				rows.Add(new TeamComplianceRowDto(employee.EmployeeCode, employee.FullName, employee.Designation, status, total, hasLogged));
			}
			return rows.OrderBy(r => r.FullName).ToList();
		}
	}
}
