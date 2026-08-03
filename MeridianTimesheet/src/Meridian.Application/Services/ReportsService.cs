using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Services
{
	public class ReportsService(
	ITimeEntryRepository timeEntryRepository,
	IMasterDataRepository masterDataRepository) : IReportsService
	{
		public async Task<IReadOnlyList<ProjectHoursReportRowDto>> GetProjectHoursAsync(
			IReadOnlyList<int> employeeIds, DateOnly weekStart, CancellationToken ct = default)
		{
			var projects = await masterDataRepository.GetProjectsAsync(ct);
			var accounts = await masterDataRepository.GetAccountsAsync(ct);
			var accountById = accounts.ToDictionary(a => a.AccountId);

			// Bucket: projectId -> (billable hours, non-billable hours, distinct employees)
			var billable = new Dictionary<int, decimal>();
			var nonBillable = new Dictionary<int, decimal>();
			var employeesByProject = new Dictionary<int, HashSet<int>>();

			foreach (var employeeId in employeeIds)
			{
				var entries = await timeEntryRepository.GetForWeekAsync(employeeId, weekStart, ct);
				foreach (var entry in entries)
				{
					var hours = entry.TotalHours;
					if (hours == 0) continue;

					if (entry.IsBillable) billable[entry.ProjectId] = billable.GetValueOrDefault(entry.ProjectId) + hours;
					else nonBillable[entry.ProjectId] = nonBillable.GetValueOrDefault(entry.ProjectId) + hours;

					if (!employeesByProject.TryGetValue(entry.ProjectId, out var set))
						employeesByProject[entry.ProjectId] = set = [];
					set.Add(employeeId);
				}
			}

			var touchedProjectIds = billable.Keys.Union(nonBillable.Keys).ToHashSet();

			var rows = projects
				.Where(p => touchedProjectIds.Contains(p.ProjectId))
				.Select(p =>
				{
					var b = billable.GetValueOrDefault(p.ProjectId);
					var nb = nonBillable.GetValueOrDefault(p.ProjectId);
					var accountName = accountById.TryGetValue(p.AccountId, out var acc) ? acc.Name : "—";
					return new ProjectHoursReportRowDto(
						p.ProjectId, p.Code, p.Name, accountName, b, nb, b + nb,
						employeesByProject.GetValueOrDefault(p.ProjectId)?.Count ?? 0
					);
				})
				.OrderByDescending(r => r.TotalHours)
				.ToList();

			return rows;
		}
	}
}
