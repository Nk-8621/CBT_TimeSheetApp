using Meridian.Domain.Enums;

namespace Meridian.Domain.Entities;

/// <summary>
/// An employee's request to change a single day's type to WFH, half-day
/// Leave, or full-day Leave, routed to their Level 1 manager for approval.
/// The requested effect is applied immediately on submission (the grid
/// reflects it right away, tagged Pending) - see DayTypeRequestService for
/// exactly what "applied" means for each RequestType and how a rejection
/// reverts it.
///
/// Deliberately kept separate from DayTypeOverride and LeaveRecord: those
/// two keep meaning exactly what they already mean (the WFH toggle, and
/// Keka-synced leave) - this table owns only the request/approval
/// lifecycle. Source and ExternalRef are unused placeholders for a future
/// Keka integration (still on hold as of this writing), so that a later
/// sync job can reuse this same shape without a schema change.
/// </summary>
public class DayTypeRequest
{
    public int DayTypeRequestId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly RequestDate { get; set; }
    public DayTypeRequestType RequestType { get; set; }
    public DayTypeRequestStatus Status { get; set; } = DayTypeRequestStatus.Pending;
    public string? Note { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int? ApproverEmployeeId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }

    /// <summary>Always "Meridian" for now - reserved for a future Keka-synced row.</summary>
    public string Source { get; set; } = "Meridian";

    /// <summary>Unused today - reserved for Keka's own record id once synced.</summary>
    public string? ExternalRef { get; set; }

    public Employee? Employee { get; set; }
    public Employee? Approver { get; set; }
}
