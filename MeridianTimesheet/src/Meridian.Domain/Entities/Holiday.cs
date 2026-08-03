namespace Meridian.Domain.Entities;

/// <summary>
/// Synced from KEKA (Carbynetech's HR/leave system). Meridian does not
/// originate these records — it caches what KEKA reports.
/// </summary>
public class Holiday
{
    public int HolidayId { get; set; }
    public DateOnly HolidayDate { get; set; }
    public required string Name { get; set; }
    public string Location { get; set; } = "All India";
    public string SourceSystem { get; set; } = "KEKA";
    public DateTime SyncedAt { get; set; }
}
