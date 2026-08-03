using Meridian.Domain.Enums;

namespace Meridian.Domain.Entities;

/// <summary>One entry in a week's approval trail (submitted / L1 approved / L2 approved / rejected...).</summary>
public class ApprovalEvent
{
    public int ApprovalEventId { get; set; }
    public int WeekRecordId { get; set; }
    public required string EventText { get; set; }
    public string? EventMeta { get; set; }
    public ApprovalEventStatus? EventStatus { get; set; }
    public int? ActedByEmployeeId { get; set; }
    public DateTime EventTimestamp { get; set; }

    public WeekRecord? WeekRecord { get; set; }
    public Employee? ActedBy { get; set; }
}
