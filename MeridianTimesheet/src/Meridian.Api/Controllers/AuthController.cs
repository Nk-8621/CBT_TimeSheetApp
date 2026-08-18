using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IUserAuthenticationService userAuthenticationService) : ControllerBase
{
	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
	{
		var result = await userAuthenticationService.LoginAsync(request.EmployeeCode?.Trim() ?? "", request.Password, ct);
		if (result is null)
			return Unauthorized(new { title = "Incorrect employee ID or password." });

		return Ok(result);
	}

	[HttpPost("first-login/verify-otp")]
	public async Task<IActionResult> VerifyFirstLoginOtp([FromBody] VerifyFirstLoginOtpRequest request, CancellationToken ct)
	{
		var result = await userAuthenticationService.VerifyFirstLoginOtpAndSetPasswordAsync(
			request.EmployeeCode?.Trim() ?? "", request.OtpCode?.Trim() ?? "", request.NewPassword, request.ConfirmNewPassword, ct);
		return Ok(result);
	}

	[HttpPost("first-login/resend-otp")]
	public async Task<IActionResult> ResendFirstLoginOtp([FromBody] ResendOtpRequest request, CancellationToken ct)
	{
		await userAuthenticationService.ResendFirstLoginOtpAsync(request.EmployeeCode?.Trim() ?? "", ct);
		return NoContent();
	}
}