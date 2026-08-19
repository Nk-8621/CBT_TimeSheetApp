using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Meridian.Application.Common;
using Meridian.Application.Interfaces.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Meridian.Infrastructure.Services;

public class JwtTokenService(IOptions<JwtSettings> jwtSettingsOptions) : IJwtTokenService
{
	private readonly JwtSettings settings = jwtSettingsOptions.Value;

	public IssuedToken GenerateToken(string employeeCode, bool isAdmin)
	{
		var expiresAt = DateTime.UtcNow.AddMinutes(settings.ExpiryMinutes);

		// NameIdentifier is the claim CurrentUserService should read to resolve
		// "who is calling" — matching whatever claim type the dev-mode header
		// handler populated before, so CurrentUserService needs little or no
		// change. Verify against your actual CurrentUserService.cs.
		var claims = new[]
		{
			new Claim(ClaimTypes.NameIdentifier, employeeCode),
			new Claim("employee_code", employeeCode),
			new Claim("is_admin", isAdmin.ToString()),
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));
		var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var token = new JwtSecurityToken(
			issuer: settings.Issuer,
			audience: settings.Audience,
			claims: claims,
			expires: expiresAt,
			signingCredentials: credentials
		);

		var jwt = new JwtSecurityTokenHandler().WriteToken(token);
		return new IssuedToken(jwt, expiresAt);
	}
}