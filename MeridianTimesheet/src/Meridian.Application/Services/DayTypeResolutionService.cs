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
	IEmployeeRepository employeeRepository) : IDayTypeResolutionService
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

		var result = new List<DayTypeDto>(7);
		foreach (var date in days)
		{
			var holiday = await masterDataRepository.GetHolidayOnAsync(date, primaryAccountId, ct);
			var isOnLeave = leaveDates.Contains(date);
			overrideByDate.TryGetValue(date, out var overrideType);
			leaveRequestByDate.TryGetValue(date, out var leaveRequestType);

			var resolved = DayTypeResolver.Resolve(
				date, holiday is not null, isOnLeave,
				leaveRequestByDate.ContainsKey(date) ? leaveRequestType : null,
				overrideByDate.ContainsKey(date) ? overrideType : null);
			var capacity = resolved switch
			{
				DayType.W or DayType.WFH => WeekMath.StandardHoursPerDay,
				DayType.LH => WeekMath.StandardHoursPerDay / 2,
				_ => 0m,
			};
			var requestTypeForLeaveHalf = resolved == DayType.LH && leaveRequestByDate.ContainsKey(date)
				? leaveRequestType.ToString()
				: null;
			result.Add(new DayTypeDto(date, resolved.ToString(), capacity, requestTypeForLeaveHalf));
		}
		return result;
	}
}