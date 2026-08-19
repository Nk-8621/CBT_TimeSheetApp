using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

/// <summary>
/// Named UserAuthenticationService rather than AuthenticationService
/// deliberately — ASP.NET Core itself defines
/// Microsoft.AspNetCore.Authentication.IAuthenticationService, and the two
/// collide (an ambiguous-reference compile error) if named the same.
/// </summary>
public interface IUserAuthenticationService
{
	Task<LoginResult?> LoginAsync(string employeeCode, string password, CancellationToken ct = default);

	Task<LoginResult> VerifyFirstLoginOtpAndSetPasswordAsync(
		string employeeCode, string otpCode, string newPassword, string confirmNewPassword, CancellationToken ct = default);

	Task ResendFirstLoginOtpAsync(string employeeCode, CancellationToken ct = default);
}