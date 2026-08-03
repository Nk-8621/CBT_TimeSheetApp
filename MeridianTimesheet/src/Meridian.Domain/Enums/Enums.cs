namespace Meridian.Domain.Enums;

/// <summary>
/// Matches the CHECK constraint on Carbynetech_WeekRecord.Status.
/// Stored as strings in the DB (not integers), so string conversion is
/// configured explicitly in the EF Core entity configuration.
/// </summary>
public enum WeekStatus
{
    Draft,
    PendingL1,
    PendingL2,
    Approved,
    Rejected,
}

/// <summary>
/// Matches the CHECK constraint on Carbynetech_DayTypeOverride.DayType.
/// W = working, WFH = work from home, L = leave, H = holiday, O = weekly off.
/// </summary>
public enum DayType
{
    W,
    WFH,
    L,
    H,
    O,
}

/// <summary>
/// Matches Carbynetech_ApprovalEvent.EventStatus.
/// </summary>
public enum ApprovalEventStatus
{
    Ok,
    Pending,
    Rejected,
}

/// <summary>
/// Matches Carbynetech_Notification.NotificationKind.
/// </summary>
public enum NotificationKind
{
    Warning,
    Info,
    Risk,
}

/// <summary>
/// Matches Carbynetech_Account.AccountType.
/// </summary>
public enum AccountType
{
    Customer,
    Internal,
}
