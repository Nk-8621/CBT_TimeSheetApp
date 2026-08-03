using Meridian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meridian.Infrastructure.Persistence.Configurations;

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Carbynetech_Holiday");
        builder.HasKey(h => h.HolidayId);
        builder.Property(h => h.Name).HasMaxLength(150).IsRequired();
        builder.Property(h => h.Location).HasMaxLength(100).IsRequired();
        builder.Property(h => h.SourceSystem).HasMaxLength(20).IsRequired();
        builder.HasIndex(h => new { h.HolidayDate, h.Location }).IsUnique();
    }
}

public class LeaveRecordConfiguration : IEntityTypeConfiguration<LeaveRecord>
{
    public void Configure(EntityTypeBuilder<LeaveRecord> builder)
    {
        builder.ToTable("Carbynetech_LeaveRecord");
        builder.HasKey(l => l.LeaveRecordId);
        builder.Property(l => l.LeaveType).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Status).HasMaxLength(20).IsRequired();
        builder.Property(l => l.SourceSystem).HasMaxLength(20).IsRequired();
        builder.HasIndex(l => new { l.EmployeeId, l.LeaveDate }).IsUnique();

        builder.HasOne(l => l.Employee)
            .WithMany()
            .HasForeignKey(l => l.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
