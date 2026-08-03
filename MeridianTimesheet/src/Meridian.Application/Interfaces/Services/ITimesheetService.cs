using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;


/// <summary>Owns everything about a single employee's single week: the logged
/// entries, day types, and the aggregate totals derived from them. Submission/
/// approval workflow is a separate concern — see IWeekApprovalService.</summary>
public interface ITimesheetService
{
	Task<WeekSummaryDto> GetWeekAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default);

	/// <summary>Every week this employee has logged time in, newest first —
	/// backs the "My Timesheets" history screen.</summary>
	Task<IReadOnlyList<WeekHistoryItemDto>> GetHistoryAsync(string employeeCode, CancellationToken ct = default);

	Task<TimeEntryDto> AddEntryAsync(string employeeCode, DateOnly weekStartDate, CreateTimeEntryRequest request, CancellationToken ct = default);
	Task<TimeEntryDto> UpdateEntryAsync(int timeEntryId, UpdateTimeEntryRequest request, CancellationToken ct = default);
	Task RemoveEntryAsync(int timeEntryId, CancellationToken ct = default);

	Task<DayTypeDto> SetDayTypeAsync(string employeeCode, DateOnly date, string dayType, CancellationToken ct = default);

	/// <summary>Copies last week's distinct (module, task) lines forward with hours
	/// blanked out. Returns how many new lines were added (duplicates are skipped).</summary>
	Task<int> CopyLastWeekAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default);
}

