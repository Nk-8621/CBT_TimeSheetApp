using Meridian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meridian.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Carbynetech_Notification");
        builder.HasKey(n => n.NotificationId);
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(500).IsRequired();
        builder.Property(n => n.NotificationKind).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.HasIndex(n => n.EmployeeId);

        builder.HasOne(n => n.Employee).WithMany().HasForeignKey(n => n.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
