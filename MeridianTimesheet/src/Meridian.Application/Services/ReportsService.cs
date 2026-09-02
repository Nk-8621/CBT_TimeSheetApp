using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class ReportsService(
	ITimeEntryRepository timeEntryRepository,
	IMasterDataRepository masterDataRepository,
	IWeekRecordRepository weekRecordRepository,
	IEmployeeRepository employeeRepository) : IReportsService
{
	// Matches the original wireframe's STATSETS exactly.
	private static readonly Dictionary<string, WeekStatus[]> StatusSets = new()
	{
		["all"] = [WeekStatus.Draft, WeekStatus.PendingL1, WeekStatus.PendingL2, WeekStatus.Approved, WeekStatus.Rejected],
		["sub"] = [WeekStatus.PendingL1, WeekStatus.PendingL2, WeekStatus.Approved],
		["ok"] = [WeekStatus.Approved],
	};

	private record Contribution(int DeptId, int AccountId, int ProjectId, int ModuleId, int TaskId, int EmployeeId, string Classification, decimal Hours);

	public async Task<IReadOnlyList<ProjectHoursReportRowDto>> GetProjectHoursAsync(
		IReadOnlyList<int> employeeIds, DateOnly weekStart, CancellationToken ct = default)
	{
		var projects = await masterDataRepository.GetProjectsAsync(ct);
		var accounts = await masterDataRepository.GetAccountsAsync(ct);
		var accountById = accounts.ToDictionary(a => a.AccountId);

		var billable = new Dictionary<int, decimal>();
		var partialBillable = new Dictionary<int, decimal>();
		var nonBillable = new Dictionary<int, decimal>();
		var employeesByProject = new Dictionary<int, HashSet<int>>();

		foreach (var employeeId in employeeIds)
		{
			var entries = await timeEntryRepository.GetForWeekAsync(employeeId, weekStart, ct);
			foreach (var entry in entries)
			{
				var hours = entry.TotalHours;
				if (hours == 0) continue;

				if (entry.Classification == "Billable") billable[entry.ProjectId] = billable.GetValueOrDefault(entry.ProjectId) + hours;
				else if (entry.Classification == "PartialBillable") partialBillable[entry.ProjectId] = partialBillable.GetValueOrDefault(entry.ProjectId) + hours;
				else nonBillable[entry.ProjectId] = nonBillable.GetValueOrDefault(entry.ProjectId) + hours;

				if (!employeesByProject.TryGetValue(entry.ProjectId, out var set))
					employeesByProject[entry.ProjectId] = set = [];
				set.Add(employeeId);
			}
		}

		var touchedProjectIds = billable.Keys.Union(partialBillable.Keys).Union(nonBillable.Keys).ToHashSet();

		return projects
			.Where(p => touchedProjectIds.Contains(p.ProjectId))
			.Select(p =>
			{
				var b = billable.GetValueOrDefault(p.ProjectId);
				var pb = partialBillable.GetValueOrDefault(p.ProjectId);
				var nb = nonBillable.GetValueOrDefault(p.ProjectId);
				var accountName = accountById.TryGetValue(p.AccountId, out var acc) ? acc.Name : "—";
				return new ProjectHoursReportRowDto(
					p.ProjectId, p.Code, p.Name, accountName, b, pb, nb, b + pb + nb,
					employeesByProject.GetValueOrDefault(p.ProjectId)?.Count ?? 0
				);
			})
			.OrderByDescending(r => r.TotalHours)
			.ToList();
	}

	public async Task<ReportsSummaryDto> GetSummaryAsync(
		IReadOnlyList<int> employeeIds, DateOnly weekFrom, DateOnly weekTo, int? departmentId, string approvalStatus, CancellationToken ct = default)
	{
		var statusSet = StatusSets.TryGetValue(approvalStatus, out var s) ? s : StatusSets["all"];

		var deptById = (await masterDataRepository.GetDepartmentsAsync(ct)).ToDictionary(d => d.DepartmentId);
		var accountById = (await masterDataRepository.GetAccountsAsync(ct)).ToDictionary(a => a.AccountId);
		var projectById = (await masterDataRepository.GetProjectsAsync(ct)).ToDictionary(p => p.ProjectId);
		var moduleById = (await masterDataRepository.GetModulesAsync(ct: ct)).ToDictionary(m => m.ModuleId);
		var taskById = (await masterDataRepository.GetTasksAsync(ct: ct)).ToDictionary(t => t.TaskId);
		var employeeById = (await employeeRepository.GetAllAsync(ct)).ToDictionary(e => e.EmployeeId);

		var weeksInRange = new List<DateOnly>();
		for (var w = weekFrom; w <= weekTo; w = w.AddDays(7))
			weeksInRange.Add(w);

		var contributions = new List<Contribution>();
		foreach (var employeeId in employeeIds)
		{
			foreach (var week in weeksInRange)
			{
				var weekRecord = await weekRecordRepository.GetAsync(employeeId, week, ct);
				var status = weekRecord?.Status ?? WeekStatus.Draft;
				if (!statusSet.Contains(status)) continue;

				var entries = await timeEntryRepository.GetForWeekAsync(employeeId, week, ct);
				foreach (var entry in entries)
				{
					var hours = entry.TotalHours;
					if (hours == 0) continue;
					if (!projectById.TryGetValue(entry.ProjectId, out var project)) continue;
					if (!accountById.TryGetValue(project.AccountId, out var account)) continue;

					if (departmentId is int filterDept && account.DepartmentId != filterDept) continue;

					contributions.Add(new Contribution(
						account.DepartmentId, account.AccountId, entry.ProjectId, entry.ModuleId, entry.TaskId,
						employeeId, entry.Classification, hours));
				}
			}
		}

		List<ReportRollupRowDto> BuildRollup(IEnumerable<IGrouping<int, Contribution>> groups, Func<int, (string Label, string Sub)> labelOf)
		{
			return groups
				.Select(g =>
				{
					var (label, sub) = labelOf(g.Key);
					var billable = g.Where(x => x.Classification == "Billable").Sum(x => x.Hours);
					var partialBillable = g.Where(x => x.Classification == "PartialBillable").Sum(x => x.Hours);
					var nonBillable = g.Where(x => x.Classification == "NonBillable").Sum(x => x.Hours);
					var total = g.Sum(x => x.Hours);
					var resources = g.Select(x => x.EmployeeId).Distinct().Count();
					return new ReportRollupRowDto(label, sub, total, billable, partialBillable, nonBillable, resources);
				})
				.OrderByDescending(r => r.TotalHours)
				.ToList();
		}

		var deptWise = BuildRollup(contributions.GroupBy(c => c.DeptId), id =>
			deptById.TryGetValue(id, out var d) ? (d.Name, d.Code) : ("—", "—"));

		var accWise = BuildRollup(contributions.GroupBy(c => c.AccountId), id =>
		{
			if (!accountById.TryGetValue(id, out var a)) return ("—", "—");
			var deptName = deptById.TryGetValue(a.DepartmentId, out var d) ? d.Name : "—";
			return (a.Name, $"{a.AccountType} · {deptName}");
		});

		var projWise = BuildRollup(contributions.GroupBy(c => c.ProjectId), id =>
		{
			if (!projectById.TryGetValue(id, out var p)) return ("—", "—");
			var accName = accountById.TryGetValue(p.AccountId, out var a) ? a.Name : "—";
			return (p.Name, $"{p.Code} · {accName}");
		});

		var resWise = BuildRollup(contributions.GroupBy(c => c.EmployeeId), id =>
		{
			if (!employeeById.TryGetValue(id, out var e)) return ("—", "—");
			var deptName = deptById.TryGetValue(e.DepartmentId, out var d) ? d.Name : "—";
			return (e.FullName, $"{e.Designation} · {deptName}");
		});

		var taskWise = BuildRollup(contributions.GroupBy(c => c.TaskId), id =>
		{
			if (!taskById.TryGetValue(id, out var t)) return ($"Task #{id}", "—");
			var moduleName = moduleById.TryGetValue(t.ModuleId, out var m) ? m.Name : "—";
			var projName = m is not null && projectById.TryGetValue(m.ProjectId, out var p) ? p.Name : "—";
			return (t.Name, $"{moduleName} · {projName}");
		});

		var totalHours = contributions.Sum(c => c.Hours);
		var totalBillable = contributions.Where(c => c.Classification == "Billable").Sum(c => c.Hours);
		var totalPartialBillable = contributions.Where(c => c.Classification == "PartialBillable").Sum(c => c.Hours);
		var totalNonBillable = contributions.Where(c => c.Classification == "NonBillable").Sum(c => c.Hours);

		return new ReportsSummaryDto(
			totalHours, totalBillable, totalPartialBillable, totalNonBillable,
			contributions.Select(c => c.EmployeeId).Distinct().Count(),
			contributions.Select(c => c.ProjectId).Distinct().Count(),
			contributions.Count,
			deptWise, accWise, projWise, resWise, taskWise
		);
	}
}