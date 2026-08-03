using Meridian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meridian.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Carbynetech_Employee");
        builder.HasKey(e => e.EmployeeId);
        builder.Property(e => e.EmployeeCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.FullName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Initials).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Designation).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Grade).HasMaxLength(5);
        builder.Property(e => e.JobTitleRaw).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.HasIndex(e => e.EmployeeCode).IsUnique();
        builder.HasIndex(e => e.EntraObjectId).IsUnique();

        builder.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Location)
            .WithMany()
            .HasForeignKey(e => e.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Manager)
            .WithMany(e => e.DirectReports)
            .HasForeignKey(e => e.ManagerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Carbynetech_Role");
        builder.HasKey(r => r.RoleId);
        builder.Property(r => r.Code).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
    }
}

public class EmployeeRoleConfiguration : IEntityTypeConfiguration<EmployeeRole>
{
    public void Configure(EntityTypeBuilder<EmployeeRole> builder)
    {
        builder.ToTable("Carbynetech_EmployeeRole");
        builder.HasKey(er => new { er.EmployeeId, er.RoleId });

        builder.HasOne(er => er.Employee)
            .WithMany(e => e.EmployeeRoles)
            .HasForeignKey(er => er.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(er => er.Role)
            .WithMany(r => r.EmployeeRoles)
            .HasForeignKey(er => er.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmployeeDepartmentConfiguration : IEntityTypeConfiguration<EmployeeDepartment>
{
    public void Configure(EntityTypeBuilder<EmployeeDepartment> builder)
    {
        builder.ToTable("Carbynetech_EmployeeDepartment");
        builder.HasKey(ed => new { ed.EmployeeId, ed.DepartmentId });

        builder.HasOne(ed => ed.Employee)
            .WithMany(e => e.EmployeeDepartments)
            .HasForeignKey(ed => ed.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ed => ed.Department)
            .WithMany()
            .HasForeignKey(ed => ed.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
