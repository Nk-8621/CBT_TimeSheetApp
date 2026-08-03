using Meridian.Domain.Enums;

namespace Meridian.Application.Common;

/// <summary>
/// Decides what a given day's DayType is. Priority order matters: Holiday
/// and Leave are authoritative facts synced from KEKA and can't be
/// overridden; WFH is a discretionary employee choice that only applies
/// on days that would otherwise be a working day; Weekend/Working are the
/// calendar defaults when nothing else applies.
/// </summary>
public static class DayTypeResolver
{
    public static DayType Resolve(DateOnly date, bool isHoliday, bool isOnLeave, DayType? explicitOverride)
    {
        if (isHoliday) return DayType.H;
        if (isOnLeave) return DayType.L;
        if (explicitOverride is DayType.WFH) return DayType.WFH;
        if (WeekMath.IsWeekend(date)) return DayType.O;
        return DayType.W;
    }
}
