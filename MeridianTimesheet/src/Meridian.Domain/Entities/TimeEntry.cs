namespace Meridian.Domain.Entities;

/// <summary>
/// One logged line: a single project/module/task combination, with hours
/// spread across the seven days of one week.
/// </summary>
public class TimeEntry
{
    public int TimeEntryId { get; set; }
    public int EmployeeId { get; set; }

    /// <summary>Always a Monday.</summary>
    public DateOnly WeekStartDate { get; set; }

    public int ProjectId { get; set; }
    public int ModuleId { get; set; }
    public int TaskId { get; set; }
    public string Classification { get; set; } = "Billable";
    public string? BillingCategory { get; set; }
    public string? Note { get; set; }

    public decimal MondayHours { get; set; }
    public decimal TuesdayHours { get; set; }
    public decimal WednesdayHours { get; set; }
    public decimal ThursdayHours { get; set; }
    public decimal FridayHours { get; set; }
    public decimal SaturdayHours { get; set; }
    public decimal SundayHours { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
    public Project? Project { get; set; }
    public Module? Module { get; set; }
    public WorkTask? Task { get; set; }

    /// <summary>Convenience accessor — Monday..Sunday as an array, matching how the UI works with a week.</summary>
    public decimal[] HoursByDay
    {
        get => [MondayHours, TuesdayHours, WednesdayHours, ThursdayHours, FridayHours, SaturdayHours, SundayHours];
        set
        {
            if (value.Length != 7) throw new ArgumentException("Expected exactly 7 values (Monday..Sunday).", nameof(value));
            MondayHours = value[0]; TuesdayHours = value[1]; WednesdayHours = value[2]; ThursdayHours = value[3];
            FridayHours = value[4]; SaturdayHours = value[5]; SundayHours = value[6];
        }
    }

    public decimal TotalHours => MondayHours + TuesdayHours + WednesdayHours + ThursdayHours + FridayHours + SaturdayHours + SundayHours;
}
