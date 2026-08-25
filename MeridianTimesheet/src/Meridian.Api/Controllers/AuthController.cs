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
		var result = await userAuthenticationService.LoginAsync(request.Identifier?.Trim() ?? "", request.Password, ct);
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

	[HttpPost("forgot-password/request-otp")]
	public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest request, CancellationToken ct)
	{
		await userAuthenticationService.RequestPasswordResetAsync(request.Identifier?.Trim() ?? "", ct);
		return Ok(new { title = "If an account exists for that identifier, a code has been sent to its registered email." });
	}

	[HttpPost("forgot-password/resend-otp")]
	public async Task<IActionResult> ResendPasswordResetOtp([FromBody] RequestPasswordResetRequest request, CancellationToken ct)
	{
		await userAuthenticationService.ResendPasswordResetOtpAsync(request.Identifier?.Trim() ?? "", ct);
		return Ok(new { title = "If an account exists for that identifier, a new code has been sent." });
	}

	[HttpPost("forgot-password/reset")]
	public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
	{
		var result = await userAuthenticationService.ResetPasswordAsync(
			request.Identifier?.Trim() ?? "", request.OtpCode?.Trim() ?? "", request.NewPassword, request.ConfirmNewPassword, ct);
		return Ok(result);
	}
}