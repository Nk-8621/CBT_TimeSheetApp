namespace Meridian.Domain.Entities;

/// <summary>
/// One row per approved leave-day per employee. Already approved in KEKA —
/// Meridian only ever displays this, it never handles a leave *request*.
/// </summary>
public class LeaveRecord
{
    public int LeaveRecordId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public required string LeaveType { get; set; }
    public string Status { get; set; } = "Approved";
    public string SourceSystem { get; set; } = "KEKA";
    public DateTime SyncedAt { get; set; }

    public Employee? Employee { get; set; }
}
