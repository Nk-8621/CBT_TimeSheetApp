using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class DayTypeResolutionService(
	IDayTypeRepository dayTypeRepository,
	IMasterDataRepository masterDataRepository,
	ILeaveRepository leaveRepository,
	IDayTypeRequestRepository dayTypeRequestRepository,
	IEmployeeRepository employeeRepository,
	IEmployeeWeekOffRepository weekOffRepository) : IDayTypeResolutionService
{
	public async Task<List<DayTypeDto>> ResolveWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByIdAsync(employeeId, ct);
		var primaryAccountId = employee?.PrimaryAccountId;

		var days = WeekMath.WeekDays(weekStartDate);
		var overrides = await dayTypeRepository.GetForWeekAsync(employeeId, weekStartDate, ct);
		var overrideByDate = overrides.ToDictionary(o => o.EntryDate, o => o.DayType);
		var leaveRecords = await leaveRepository.GetForEmployeeAsync(employeeId, days[0], days[^1], ct);
		var leaveDates = leaveRecords.Select(l => l.LeaveDate).ToHashSet();
		var leaveRequests = await dayTypeRequestRepository.GetActiveLeaveForWeekAsync(employeeId, weekStartDate, ct);
		var leaveRequestByDate = leaveRequests.ToDictionary(r => r.RequestDate, r => r.RequestType);

		// One query for the whole week instead of one per day.
		var weekHolidays = await masterDataRepository.GetHolidaysAsync(days[0], days[^1], ct);

		// WEEK OFFS (Nov 2026): the employee's client assignments for this week
		// (usually none), and those clients' week-off periods.
		var weekOffRecords = await weekOffRepository.GetForEmployeeAsync(employeeId, days[0], days[^1], ct);
		var clientWeekOffs = weekOffRecords.Count == 0
			? []
			: await weekOffRepository.GetClientWeekOffsForAccountsAsync(weekOffRecords.Select(w => w.AccountId).Distinct().ToList(), ct);

		// CLIENT WEEK OFF (Nov 2026): employees not in a custom week off follow
		// their primary client's default week off; no primary client -> Sat, Sun.
		var primaryAccount = primaryAccountId is int accountId
			? await masterDataRepository.GetAccountByIdAsync(accountId, ct)
			: null;
		var defaultWeekOffDays = primaryAccount is null ? [] : WeekOffDays.Parse(primaryAccount.WeekOffDays);
		if (defaultWeekOffDays.Count == 0) defaultWeekOffDays = WeekOffDays.CompanyDefault.ToList();

		var result = new List<DayTypeDto>(7);
		foreach (var date in days)
		{
			// WEEK OFFS (Nov 2026): in a custom week off on this day -> its
			// week-off days (for this date) and only that client's holidays.
			// Otherwise -> the primary client's default week off (see above), and
			// company-wide holidays plus their primary client's (same rule as before).
			var weekOff = weekOffRecords.FirstOrDefault(w => w.EffectiveFrom <= date && (w.EffectiveTo == null || w.EffectiveTo >= date));
			var clientDays = weekOff is null
				? null
				: clientWeekOffs.FirstOrDefault(c => c.AccountId == weekOff.AccountId && c.EffectiveFrom <= date && (c.EffectiveTo == null || c.EffectiveTo >= date));
			var weekOffDays = clientDays is null ? defaultWeekOffDays : WeekOffDays.Parse(clientDays.WeekOffDays);
			var isWeekOff = weekOffDays.Contains(date.DayOfWeek);
			var isHoliday = weekOff is null
				? weekHolidays.Any(h => h.HolidayDate == date && (h.AccountId == null || h.AccountId == primaryAccountId))
				: weekHolidays.Any(h => h.HolidayDate == date && h.AccountId == weekOff.AccountId);
			var isOnLeave = leaveDates.Contains(date);
			overrideByDate.TryGetValue(date, out var overrideType);
			leaveRequestByDate.TryGetValue(date, out var leaveRequestType);

			var resolved = DayTypeResolver.Resolve(
				date, isHoliday, isOnLeave,
				leaveRequestByDate.ContainsKey(date) ? leaveRequestType : null,
				overrideByDate.ContainsKey(date) ? overrideType : null,
				isWeekOff);
			var capacity = resolved switch
			{
				DayType.W or DayType.WFH or DayType.OD => WeekMath.StandardHoursPerDay,
				DayType.LH => WeekMath.StandardHoursPerDay / 2,
				_ => 0m,
			};
			var requestTypeForLeaveHalf = resolved == DayType.LH && leaveRequestByDate.ContainsKey(date)
				? leaveRequestType.ToString()
				: null;
			// DAY STATUS (Nov 2026): only plain days (not W-OFF, holiday, KEKA
			// leave or a leave/On Duty request) can be changed by the employee.
			var canChange = !isHoliday && !isOnLeave && !leaveRequestByDate.ContainsKey(date) && !isWeekOff;
			result.Add(new DayTypeDto(date, resolved.ToString(), capacity, requestTypeForLeaveHalf, canChange));
		}
		return result;
	}
}
