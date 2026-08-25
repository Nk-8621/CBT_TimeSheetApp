using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

/// <summary>Owns the submission/approval workflow and its validation rules —
/// deliberately separate from ITimesheetService, which only owns the data
/// being submitted.</summary>
public interface IWeekApprovalService
{
    Task<WeekValidationResult> ValidateForSubmissionAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default);

    Task<WeekRecordDto> SubmitAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default);
    Task<WeekRecordDto> RecallAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default);

    Task<WeekRecordDto> ApproveLevel1Async(string employeeCode, DateOnly weekStartDate, string approverEmployeeCode, CancellationToken ct = default);
    Task<WeekRecordDto> ApproveLevel2Async(string employeeCode, DateOnly weekStartDate, string approverEmployeeCode, CancellationToken ct = default);
    Task<WeekRecordDto> RejectAsync(string employeeCode, DateOnly weekStartDate, string approverEmployeeCode, string reason, CancellationToken ct = default);

    /// <summary>Weeks currently awaiting this approver's action (their Level 1 or
    /// Level 2 queue, depending on which the caller asks for).</summary>
    Task<IReadOnlyList<WeekRecordDto>> GetPendingForApproverAsync(string approverEmployeeCode, bool level2, CancellationToken ct = default);

    /// <summary>The full-detail version of the queue above — flags, line-level
    /// detail with every hierarchy name resolved, and the billable split —
    /// everything the Approvals screen needs in one call. Sorted flagged
    /// items first, matching the original wireframe.</summary>
    Task<IReadOnlyList<ApprovalQueueItemDto>> GetApprovalQueueAsync(string approverEmployeeCode, bool level2, CancellationToken ct = default);

	Task<ApprovalQueueItemDto?> GetWeekDetailAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default);
}