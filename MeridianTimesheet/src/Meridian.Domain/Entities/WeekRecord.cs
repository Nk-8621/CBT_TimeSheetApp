using Meridian.Domain.Enums;

namespace Meridian.Domain.Entities;

/// <summary>One employee's submission/approval status for one week.</summary>
public class WeekRecord
{
    public int WeekRecordId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public WeekStatus Status { get; set; } = WeekStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public int? RejectedByEmployeeId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
    public Employee? RejectedBy { get; set; }
    public ICollection<ApprovalEvent> ApprovalEvents { get; set; } = new List<ApprovalEvent>();
}
