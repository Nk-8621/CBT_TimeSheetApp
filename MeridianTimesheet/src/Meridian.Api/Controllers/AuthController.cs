using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Meridian.Api.Auth;
using Meridian.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Meridian.Api.Controllers;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTime ExpiresAtUtc, string EmployeeCode, string FullName);

/// <summary>Username/password admin login - replaces Microsoft Entra sign-in.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(LocalAuthOptions options, IEmployeeRepository employeeRepository) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(options.AdminPassword) || string.IsNullOrEmpty(options.AdminEmployeeCode))
            return Problem(statusCode: 503, title: "Admin login is not configured on the server.");

        var userOk = FixedTimeEquals(request.Username?.Trim() ?? "", options.AdminUsername);
        var passOk = FixedTimeEquals(request.Password ?? "", options.AdminPassword);
        if (!userOk || !passOk)
            return Unauthorized(new { status = 401, title = "Invalid username or password." });

        var employee = await employeeRepository.GetByCodeAsync(options.AdminEmployeeCode, ct);
        if (employee is null || !employee.IsActive
            || !await employeeRepository.HasRoleAsync(employee.EmployeeId, "ADMIN", ct))
            return Problem(statusCode: 503, title: "The configured admin employee record is missing, inactive, or not an admin.");

        var expires = DateTime.UtcNow.AddHours(options.TokenLifetimeHours);
        var token = new JwtSecurityToken(
            issuer: LocalAuthOptions.Issuer,
            audience: LocalAuthOptions.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, options.AdminUsername),
                new Claim(LocalAuthOptions.EmployeeCodeClaim, employee.EmployeeCode),
            ],
            expires: expires,
            signingCredentials: new SigningCredentials(options.GetSigningKey(), SecurityAlgorithms.HmacSha256));

        return Ok(new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token), expires, employee.EmployeeCode, employee.FullName));
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
