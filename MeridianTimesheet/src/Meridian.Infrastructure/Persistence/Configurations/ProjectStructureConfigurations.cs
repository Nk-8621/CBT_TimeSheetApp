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
        builder.HasIndex(p => p.Code).IsUnique();

        builder.HasOne(p => p.Account)
            .WithMany(a => a.Projects)
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TaskCategoryConfiguration : IEntityTypeConfiguration<TaskCategory>
{
    public void Configure(EntityTypeBuilder<TaskCategory> builder)
    {
        builder.ToTable("Carbynetech_TaskCategory");
        builder.HasKey(t => t.TaskCategoryId);
        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.Code).IsUnique();
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

        builder.HasOne(m => m.TaskCategory)
            .WithMany()
            .HasForeignKey(m => m.TaskCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
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
