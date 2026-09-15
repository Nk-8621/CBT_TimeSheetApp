using Meridian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meridian.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Carbynetech_Project");
        builder.HasKey(p => p.ProjectId);
        builder.Property(p => p.Code).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.ProjectTech).HasMaxLength(200);
        builder.Property(p => p.BillingType).HasMaxLength(30);
        builder.Property(p => p.CustomerPO).HasMaxLength(200);
        builder.Property(p => p.Notes).HasMaxLength(2000);
        builder.HasIndex(p => p.Code).IsUnique();

        builder.HasOne(p => p.Account)
            .WithMany(a => a.Projects)
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull, not Restrict - same reasoning as Module.ProjectType below:
        // DeleteProjectTypeAsync always reassigns referencing Projects or
        // blocks the delete in the application layer before this FK is ever
        // exercised, but SetNull keeps that a defensive default rather than
        // a hard DB dependency on the app always getting there first.
        builder.HasOne(p => p.ProjectType)
            .WithMany()
            .HasForeignKey(p => p.ProjectTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.ProjectLeadEmployee)
            .WithMany()
            .HasForeignKey(p => p.ProjectLeadEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ProjectManagerEmployee)
            .WithMany()
            .HasForeignKey(p => p.ProjectManagerEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.DeliveryHeadEmployee)
            .WithMany()
            .HasForeignKey(p => p.DeliveryHeadEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProjectTypeConfiguration : IEntityTypeConfiguration<ProjectType>
{
    public void Configure(EntityTypeBuilder<ProjectType> builder)
    {
        builder.ToTable("Carbynetech_ProjectType");
        builder.HasKey(t => t.ProjectTypeId);
        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.Code).IsUnique();
    }
}

public class ProjectTypeModuleTemplateConfiguration : IEntityTypeConfiguration<ProjectTypeModuleTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectTypeModuleTemplate> builder)
    {
        builder.ToTable("Carbynetech_ProjectTypeModuleTemplate");
        builder.HasKey(t => t.ProjectTypeModuleTemplateId);
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(t => new { t.ProjectTypeId, t.Name }).IsUnique();

        builder.HasOne(t => t.ProjectType)
            .WithMany(pt => pt.ModuleTemplates)
            .HasForeignKey(t => t.ProjectTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProjectTypeTaskTemplateConfiguration : IEntityTypeConfiguration<ProjectTypeTaskTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectTypeTaskTemplate> builder)
    {
        builder.ToTable("Carbynetech_ProjectTypeTaskTemplate");
        builder.HasKey(t => t.ProjectTypeTaskTemplateId);
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(t => new { t.ProjectTypeModuleTemplateId, t.Name }).IsUnique();

        builder.HasOne(t => t.ModuleTemplate)
            .WithMany(m => m.TaskTemplates)
            .HasForeignKey(t => t.ProjectTypeModuleTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToTable("Carbynetech_Module");
        builder.HasKey(m => m.ModuleId);
        builder.Property(m => m.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(m => new { m.ProjectId, m.Name }).IsUnique();

        builder.HasOne(m => m.Project)
            .WithMany(p => p.Modules)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull (not Restrict) - Module.ProjectTypeId is just lineage
        // ("which template generated this module"), not load-bearing for the
        // timesheet grid. Letting it null out means deleting a ProjectType
        // never gets blocked by Modules it previously generated, on top of
        // the explicit Project-level reassignment in DeleteProjectTypeAsync.
        builder.HasOne(m => m.ProjectType)
            .WithMany()
            .HasForeignKey(m => m.ProjectTypeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("Carbynetech_Task");
        builder.HasKey(t => t.TaskId);
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(t => new { t.ModuleId, t.Name }).IsUnique();

        builder.HasOne(t => t.Module)
            .WithMany(m => m.Tasks)
            .HasForeignKey(t => t.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
