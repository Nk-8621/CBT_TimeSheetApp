using Meridian.Api.Auth;
using Meridian.Api.Middleware;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Application.Services;
using Meridian.Infrastructure;
using Meridian.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Application + Infrastructure services ----
// (Application's own services are registered directly here — rather than
// via an AddApplication() extension inside Meridian.Application — so that
// project can stay free of any dependency-injection package reference.)

builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IMasterDataService, MasterDataService>();
//builder.Services.AddScoped<IMasterDataAdminService, MasterDataAdminService>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<IWeekApprovalService, WeekApprovalService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddScoped<IDayTypeResolutionService, DayTypeResolutionService>();
builder.Services.AddScoped<IDayTypeRequestService, DayTypeRequestService>();

builder.Services.AddScoped<ITimesheetExcelImportService, TimesheetExcelImportService>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---- Authentication ----
// Real Microsoft Entra login — validates access tokens issued against the
// App Registration configured under "AzureAd" in appsettings.json
// (TenantId/ClientId/Audience). There is no local password login any
// more — every request must carry a Bearer token issued by Entra.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// ---- CORS (the React dev server runs on a different origin) ----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
	?? ["http://localhost:5173", "http://172.16.0.177:8077","*"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://timesheet.carbynetech.com:8077", "https://timesheet.carbynetech.com:8443")

			.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Name = "Authorization",
		Type = SecuritySchemeType.ApiKey,
		Scheme = "Bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "Paste a Bearer token issued by your Entra tenant.",
	});
	options.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
			Array.Empty<string>()
		},
	});
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();


	app.UseSwagger();
	app.UseSwaggerUI();


//app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();