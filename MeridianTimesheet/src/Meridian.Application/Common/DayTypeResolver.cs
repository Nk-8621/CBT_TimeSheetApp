using Meridian.Domain.Enums;

namespace Meridian.Application.Common;

/// <summary>
/// Decides what a given day's DayType is. Priority order matters: Holiday
/// and Leave synced from KEKA are authoritative facts and can't be
/// overridden; next is an approved-or-pending Meridian leave request (full
/// or half day - see DayTypeRequest), since once that's in effect it should
/// win over a plain WFH override too; WFH is a discretionary employee
/// choice that only applies on days that would otherwise be a working day;
/// Weekend/Working are the calendar defaults when nothing else applies.
/// </summary>
public static class DayTypeResolver
{
    public static DayType Resolve(
        DateOnly date,
        bool isHoliday,
        bool isOnLeave,
        DayTypeRequestType? activeMeridianLeaveRequest,
        DayType? explicitOverride,
        // WEEK OFFS (Nov 2026): the employee's own week-off day (company
        // default Sat/Sun, or their client's days from Master Data -> Week Offs).
        bool isWeekOff)
    {
        if (isHoliday) return DayType.H;
        if (isOnLeave) return DayType.L;
        if (activeMeridianLeaveRequest is DayTypeRequestType.LeaveFull or DayTypeRequestType.CompOffFull or DayTypeRequestType.UnpaidFull) return DayType.L;
        if (activeMeridianLeaveRequest is DayTypeRequestType.LeaveFirstHalf or DayTypeRequestType.LeaveSecondHalf
            or DayTypeRequestType.CompOffFirstHalf or DayTypeRequestType.CompOffSecondHalf
            or DayTypeRequestType.UnpaidFirstHalf or DayTypeRequestType.UnpaidSecondHalf) return DayType.LH;
        if (activeMeridianLeaveRequest is DayTypeRequestType.OnDuty) return DayType.OD;
        // DAY STATUS (Nov 2026): Leave picked by the employee on My Timesheet.
        if (explicitOverride is DayType.L) return DayType.L;
        if (explicitOverride is DayType.WFH) return DayType.WFH;
        if (isWeekOff) return DayType.O;
        return DayType.W;
    }
}
