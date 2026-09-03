using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

/// <summary>Owns the WFH/Leave request-and-approval workflow - submitting a
/// request applies its effect immediately (the grid reflects it right away,
/// tagged Pending), and Level 1 manager approval or rejection follows
/// afterwards. See DayTypeRequestService for exactly what "applied" means
/// per request type and how a rejection reverts it.</summary>
public interface IDayTypeRequestService
{
    Task<DayTypeRequestDto> SubmitAsync(string employeeCode, DateOnly date, string requestType, string? note, CancellationToken ct = default);

    Task<DayTypeRequestDto> ApproveAsync(int dayTypeRequestId, string approverEmployeeCode, CancellationToken ct = default);
    Task<DayTypeRequestDto> RejectAsync(int dayTypeRequestId, string approverEmployeeCode, string? comment, CancellationToken ct = default);

    /// <summary>This employee's own requests, newest first.</summary>
    Task<IReadOnlyList<DayTypeRequestDto>> GetMyRequestsAsync(string employeeCode, CancellationToken ct = default);

    /// <summary>Requests currently Pending from this approver's direct reports -
    /// their Level 1 inbox for the Request Approval screen.</summary>
    Task<IReadOnlyList<DayTypeRequestDto>> GetApprovalQueueAsync(string approverEmployeeCode, CancellationToken ct = default);
}
