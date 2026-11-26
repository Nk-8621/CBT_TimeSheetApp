using Meridian.Api.Auth;
using Meridian.Api.Middleware;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Application.Services;
using Meridian.Infrastructure;
using Meridian.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
builder.Services.AddScoped<IUserStoryService, UserStoryService>();
builder.Services.AddScoped<IWeekApprovalService, WeekApprovalService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();
builder.Services.AddScoped<IDayTypeResolutionService, DayTypeResolutionService>();
builder.Services.AddScoped<IDayTypeRequestService, DayTypeRequestService>();
builder.Services.AddScoped<IWeekOffService, WeekOffService>(); // WEEK OFFS (Nov 2026)

builder.Services.AddScoped<ITimesheetExcelImportService, TimesheetExcelImportService>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---- Authentication ----
// Local admin login (Microsoft Entra sign-in removed). POST /api/auth/login
// checks the username/password under "LocalAuth" and issues a JWT signed by
// this API; every other request must carry that token as a Bearer header.
var localAuth = builder.Configuration.GetSection(LocalAuthOptions.SectionName).Get<LocalAuthOptions>()
	?? new LocalAuthOptions();
builder.Services.AddSingleton(localAuth);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = LocalAuthOptions.Issuer,
			ValidateAudience = true,
			ValidAudience = LocalAuthOptions.Audience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = localAuth.GetSigningKey(),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromMinutes(1),
		};
	});

builder.Services.AddAuthorization();

// ---- CORS (the React dev server runs on a different origin) ----
// Only the addresses listed under Cors:AllowedOrigins in appsettings.json may
// call this API from a browser (previously any website could).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
	?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
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
		Description = "Paste the token returned by POST /api/auth/login.",
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
