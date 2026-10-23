using Meridian.Application.Interfaces.Repositories;
using Meridian.Infrastructure.Persistence;
using Meridian.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Empty in the git-tracked appsettings.json on purpose - fail at startup
        // with a clear message rather than on the first database query.
        var connectionString = configuration.GetConnectionString("MeridianDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'MeridianDatabase' is not configured. See the README section \"Database connection string\".");

        services.AddDbContext<MeridianDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IMasterDataRepository, MasterDataRepository>();
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
        services.AddScoped<IWeekRecordRepository, WeekRecordRepository>();
        services.AddScoped<IDayTypeRepository, DayTypeRepository>();
        services.AddScoped<IDayTypeRequestRepository, DayTypeRequestRepository>();
        services.AddScoped<ILeaveRepository, LeaveRepository>();
		services.AddScoped<INotificationRepository, NotificationRepository>();

		return services;
    }
}
