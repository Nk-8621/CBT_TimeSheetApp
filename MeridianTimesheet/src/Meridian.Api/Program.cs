using Meridian.Api.Auth;
using Meridian.Api.Middleware;
using Meridian.Application.Common;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Application.Services;
using Meridian.Infrastructure;
using Meridian.Infrastructure.Repositories;
using Meridian.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;


// using Microsoft.AspNetCore.Authentication.JwtBearer;   // needed again once real Entra login is switched on
// using Microsoft.Identity.Web;                          // needed again once real Entra login is switched on
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---- Application + Infrastructure services ----
// (Application's own services are registered directly here — rather than
// via an AddApplication() extension inside Meridian.Application — so that
// project can stay free of any dependency-injection package reference.)

builder.Services.Configure<OtpSettings>(builder.Configuration.GetSection("Otp"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
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

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IOtpGenerator, OtpGenerator>();
builder.Services.AddScoped<IEmailSender, DevEmailSender>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---- Authentication ----
// Dev mode trusts a header instead of a real Microsoft Entra token —
// mirrors the frontend's dev-mode fallback. Real Microsoft login is
// deferred for now (see the commented-out branch below); dev mode is the
// only active path until that's switched back on.
var devModeEnabled = builder.Configuration.GetValue<bool>("Authentication:DevMode");

if (devModeEnabled)
{
	var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

	builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
		.AddJwtBearer(options =>
		{
			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidateIssuer = true,
				ValidIssuer = jwtSettings.Issuer,
				ValidateAudience = true,
				ValidAudience = jwtSettings.Audience,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
				ClockSkew = TimeSpan.FromMinutes(2), // small leeway for clock drift between machines
			};
		});
}
else
{
	// Real Microsoft Entra login — re-enable this branch (and the two
	// commented-out `using` directives above, and the Microsoft.Identity.Web
	// PackageReference in Meridian.Api.csproj) once an App Registration
	// exists in your Entra tenant and Authentication:DevMode is set to false.
	//
	// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	//     .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
	throw new InvalidOperationException(
		"Authentication:DevMode is false, but real Microsoft login isn't wired up yet — " +
		"see the commented-out code in Program.cs. Set DevMode back to true for now.");
}

builder.Services.AddAuthorization();

// ---- CORS (the React dev server runs on a different origin) ----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
	?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
	options.AddPolicy("Frontend", policy =>
		policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	if (devModeEnabled)
	{
		// Dev mode reads a plain header, not a Bearer token — wire Swagger's
		// Authorize button to match, so testing doesn't require curl/Postman.
		options.AddSecurityDefinition("DevAuth", new OpenApiSecurityScheme
		{
			Name = DevAuthenticationHandler.EmployeeCodeHeader,
			Type = SecuritySchemeType.ApiKey,
			In = ParameterLocation.Header,
			Description = "Dev mode is ON. Click Authorize and enter an employee code (e.g. CBT1267) — " +
						  "no need for a real token.",
		});
		options.AddSecurityRequirement(new OpenApiSecurityRequirement
		{
			{
				new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "DevAuth" } },
				Array.Empty<string>()
			},
		});
	}
	else
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
	}
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();