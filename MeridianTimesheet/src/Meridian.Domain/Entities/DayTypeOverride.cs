using Meridian.Domain.Enums;

namespace Meridian.Domain.Entities;

/// <summary>
/// Explicit day-type overrides. Weekly-off is computed from the calendar,
/// Holiday comes from Holiday, Leave comes from LeaveRecord — the only day
/// type that actually needs to be stored here is WFH (a person's own
/// choice, not derivable from anywhere else).
/// </summary>
public class DayTypeOverride
{
    public int DayTypeOverrideId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly EntryDate { get; set; }
    public DayType DayType { get; set; }
    public DateTime CreatedAt { get; set; }

    public Employee? Employee { get; set; }
}
