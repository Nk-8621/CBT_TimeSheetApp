using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence;

/// <summary>
/// Maps to the EXISTING Carbynetech_* schema (created via raw SQL scripts,
/// already populated with real data) — this context does not own schema
/// creation. Do not run `dotnet ef database update` against a database that
/// already has these tables; if you add EF Core Migrations later, start
/// from a baseline migration that matches what's already there.
/// </summary>
public class MeridianDbContext(DbContextOptions<MeridianDbContext> options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskCategory> TaskCategories => Set<TaskCategory>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<EmployeeRole> EmployeeRoles => Set<EmployeeRole>();
    public DbSet<EmployeeDepartment> EmployeeDepartments => Set<EmployeeDepartment>();

    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<LeaveRecord> LeaveRecords => Set<LeaveRecord>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<DayTypeOverride> DayTypeOverrides => Set<DayTypeOverride>();
    public DbSet<WeekRecord> WeekRecords => Set<WeekRecord>();
    public DbSet<ApprovalEvent> ApprovalEvents => Set<ApprovalEvent>();

    public DbSet<Notification> Notifications => Set<Notification>();

	public DbSet<Otp> Otps => Set<Otp>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DepartmentConfiguration());
        modelBuilder.ApplyConfiguration(new LocationConfiguration());
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new TaskCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ModuleConfiguration());
        modelBuilder.ApplyConfiguration(new WorkTaskConfiguration());

        modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeRoleConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeDepartmentConfiguration());

        modelBuilder.ApplyConfiguration(new HolidayConfiguration());
        modelBuilder.ApplyConfiguration(new LeaveRecordConfiguration());

        modelBuilder.ApplyConfiguration(new TimeEntryConfiguration());
        modelBuilder.ApplyConfiguration(new DayTypeOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new WeekRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ApprovalEventConfiguration());

        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
    }
}
