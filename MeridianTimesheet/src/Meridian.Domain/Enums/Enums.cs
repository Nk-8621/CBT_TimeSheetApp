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
/// The DayTypeOverride table's own CHECK constraint only allows W/WFH/L/H/O
/// (see Carbynetech_DayTypeOverride.DayType) - LH (half-day leave) is never
/// written there. It only ever comes out of DayTypeResolver, computed from an
/// active Meridian half-day leave request (see DayTypeRequest), so it needs
/// no corresponding change to that constraint.
/// OD (on duty) is likewise never written there - it only comes out of
/// DayTypeResolver, computed from an active On Duty request.
/// W = working, WFH = work from home, L = leave (full day), LH = leave
/// (half day - 4h leave, 4h still worked), H = holiday, O = weekly off,
/// OD = on duty (a full 8h working day spent away on official duty).
/// </summary>
public enum DayType
{
    W,
    WFH,
    L,
    LH,
    H,
    O,
    OD,
}

/// <summary>What an employee is asking to change a day to. Matches the CHECK
/// constraint on Carbynetech_DayTypeRequest.RequestType. LeaveFirstHalf and
/// LeaveSecondHalf both resolve to the same DayType.LH (4h leave, 4h still
/// worked) - which half only matters for what the request itself records,
/// not for capacity or grid resolution. OnDuty resolves to DayType.OD (a
/// full 8h working day). CompOffFirstHalf / CompOffSecondHalf / CompOffFull
/// (compensatory off) are shown exactly like leave - DayType.LH and DayType.L
/// respectively - only the request itself records that it was a comp-off.</summary>
public enum DayTypeRequestType
{
    WFH,
    LeaveFirstHalf,
    LeaveSecondHalf,
    LeaveFull,
    OnDuty,
    CompOffFirstHalf,
    CompOffSecondHalf,
    CompOffFull,
}

/// <summary>Matches the CHECK constraint on Carbynetech_DayTypeRequest.Status.</summary>
public enum DayTypeRequestStatus
{
    Pending,
    Approved,
    Rejected,
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
