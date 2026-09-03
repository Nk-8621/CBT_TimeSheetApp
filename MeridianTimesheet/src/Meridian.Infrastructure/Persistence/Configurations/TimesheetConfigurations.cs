using Meridian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meridian.Infrastructure.Persistence.Configurations;

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("Carbynetech_TimeEntry");
        builder.HasKey(t => t.TimeEntryId);
        builder.Property(t => t.Note).HasMaxLength(500);
        builder.Property(t => t.Classification).HasMaxLength(20).IsRequired();
        builder.Property(t => t.BillingCategory).HasMaxLength(10);

        // HoursByDay/TotalHours are computed convenience accessors over the
        // seven real columns below — not columns themselves.
        builder.Ignore(t => t.HoursByDay);
        builder.Ignore(t => t.TotalHours);

        foreach (var dayProperty in new[]
        {
            nameof(TimeEntry.MondayHours), nameof(TimeEntry.TuesdayHours), nameof(TimeEntry.WednesdayHours),
            nameof(TimeEntry.ThursdayHours), nameof(TimeEntry.FridayHours), nameof(TimeEntry.SaturdayHours),
            nameof(TimeEntry.SundayHours),
        })
        {
            builder.Property(dayProperty).HasPrecision(4, 2);
        }

        builder.HasIndex(t => new { t.EmployeeId, t.WeekStartDate });

        builder.HasOne(t => t.Employee).WithMany().HasForeignKey(t => t.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Project).WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Module).WithMany().HasForeignKey(t => t.ModuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Task).WithMany().HasForeignKey(t => t.TaskId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DayTypeOverrideConfiguration : IEntityTypeConfiguration<DayTypeOverride>
{
    public void Configure(EntityTypeBuilder<DayTypeOverride> builder)
    {
        builder.ToTable("Carbynetech_DayTypeOverride");
        builder.HasKey(d => d.DayTypeOverrideId);
        builder.Property(d => d.DayType).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.HasIndex(d => new { d.EmployeeId, d.EntryDate }).IsUnique();

        builder.HasOne(d => d.Employee).WithMany().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DayTypeRequestConfiguration : IEntityTypeConfiguration<DayTypeRequest>
{
    public void Configure(EntityTypeBuilder<DayTypeRequest> builder)
    {
        builder.ToTable("Carbynetech_DayTypeRequest");
        builder.HasKey(r => r.DayTypeRequestId);
        builder.Property(r => r.RequestType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(500);
        builder.Property(r => r.DecisionComment).HasMaxLength(500);
        builder.Property(r => r.Source).HasMaxLength(20).IsRequired();
        builder.Property(r => r.ExternalRef).HasMaxLength(100);
        builder.HasIndex(r => new { r.EmployeeId, r.RequestDate });

        builder.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Approver).WithMany().HasForeignKey(r => r.ApproverEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WeekRecordConfiguration : IEntityTypeConfiguration<WeekRecord>
{
    public void Configure(EntityTypeBuilder<WeekRecord> builder)
    {
        builder.ToTable("Carbynetech_WeekRecord");
        builder.HasKey(w => w.WeekRecordId);
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(w => w.RejectionReason).HasMaxLength(500);
        builder.HasIndex(w => new { w.EmployeeId, w.WeekStartDate }).IsUnique();

        builder.HasOne(w => w.Employee).WithMany().HasForeignKey(w => w.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.RejectedBy).WithMany().HasForeignKey(w => w.RejectedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalEventConfiguration : IEntityTypeConfiguration<ApprovalEvent>
{
    public void Configure(EntityTypeBuilder<ApprovalEvent> builder)
    {
        builder.ToTable("Carbynetech_ApprovalEvent");
        builder.HasKey(a => a.ApprovalEventId);
        builder.Property(a => a.EventText).HasMaxLength(300).IsRequired();
        builder.Property(a => a.EventMeta).HasMaxLength(200);
        builder.Property(a => a.EventStatus).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(a => a.WeekRecordId);

        builder.HasOne(a => a.WeekRecord)
            .WithMany(w => w.ApprovalEvents)
            .HasForeignKey(a => a.WeekRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ActedBy).WithMany().HasForeignKey(a => a.ActedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
