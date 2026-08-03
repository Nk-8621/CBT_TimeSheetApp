using Meridian.Domain.Enums;

namespace Meridian.Domain.Entities;

public class Notification
{
    public int NotificationId { get; set; }

    /// <summary>Null = broadcast to everyone.</summary>
    public int? EmployeeId { get; set; }

    public required string Title { get; set; }
    public required string Message { get; set; }
    public NotificationKind NotificationKind { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public Employee? Employee { get; set; }
}
