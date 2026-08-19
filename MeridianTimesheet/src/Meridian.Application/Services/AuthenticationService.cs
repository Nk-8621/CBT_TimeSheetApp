using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class AuthenticationService(
	IEmployeeRepository employeeRepository,
	IPasswordHasher passwordHasher,
	IOtpService otpService,
	IJwtTokenService jwtTokenService) : IAuthenticationService
{
	public async Task<LoginResult?> LoginAsync(string employeeCode, string password, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct);

		// Same generic failure whether the employee doesn't exist, has never
		// been granted portal access (PasswordHash is null), or typed the
		// wrong password — none of these should be distinguishable from
		// outside, or the response itself becomes a way to enumerate real
		// employee codes.
		if (employee is null || employee.PasswordHash is null || !passwordHasher.Verify(password, employee.PasswordHash))
			return null;

		if (employee.MustChangePassword)
		{
			RequireEmailOnFile(employee.Email, employee.EmployeeCode);
			await otpService.RequestAsync(employee.EmployeeId, employee.Email!, OtpPurpose.FirstLogin, ct);
			return new LoginResult(RequiresOtpVerification: true, Token: null, ExpiresAtUtc: null, employee.EmployeeCode, employee.FullName);
		}

		var isAdmin = await employeeRepository.HasRoleAsync(employee.EmployeeId, "ADMIN", ct);
		var issued = jwtTokenService.GenerateToken(employee.EmployeeCode, isAdmin);
		return new LoginResult(RequiresOtpVerification: false, issued.Token, issued.ExpiresAtUtc, employee.EmployeeCode, employee.FullName);
	}

	public async Task<LoginResult> VerifyFirstLoginOtpAndSetPasswordAsync(
		string employeeCode, string otpCode, string newPassword, string confirmNewPassword, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Domain.Entities.Employee), employeeCode);

		// Validate the new password BEFORE spending an OTP attempt on it —
		// no reason to burn one of a limited number of tries on a request
		// that was always going to fail regardless of the code.
		var passwordErrors = PasswordPolicy.Validate(newPassword, confirmNewPassword);
		if (passwordErrors.Count > 0)
			throw new BusinessRuleException(string.Join(" ", passwordErrors));

		var outcome = await otpService.VerifyAsync(employee.EmployeeId, OtpPurpose.FirstLogin, otpCode, ct);
		if (outcome != OtpVerificationOutcome.Success)
			throw new BusinessRuleException(DescribeOtpFailure(outcome));

		employee.PasswordHash = passwordHasher.Hash(newPassword);
		employee.MustChangePassword = false;
		await employeeRepository.SaveChangesAsync(ct);

		var isAdmin = await employeeRepository.HasRoleAsync(employee.EmployeeId, "ADMIN", ct);
		var issued = jwtTokenService.GenerateToken(employee.EmployeeCode, isAdmin);
		return new LoginResult(RequiresOtpVerification: false, issued.Token, issued.ExpiresAtUtc, employee.EmployeeCode, employee.FullName);
	}

	public async Task ResendFirstLoginOtpAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Domain.Entities.Employee), employeeCode);

		if (!employee.MustChangePassword)
			throw new BusinessRuleException("This account is not awaiting first-login verification.");

		RequireEmailOnFile(employee.Email, employee.EmployeeCode);
		await otpService.ResendAsync(employee.EmployeeId, employee.Email!, OtpPurpose.FirstLogin, ct);
	}

	private static void RequireEmailOnFile(string? email, string employeeCode)
	{
		if (string.IsNullOrWhiteSpace(email))
			throw new BusinessRuleException($"{employeeCode} has no email on file — contact an administrator to add one before granting portal access.");
	}

	private static string DescribeOtpFailure(OtpVerificationOutcome outcome) => outcome switch
	{
		OtpVerificationOutcome.InvalidCode => "That code is incorrect. Please check your email and try again.",
		OtpVerificationOutcome.Expired => "That code has expired. Request a new one.",
		OtpVerificationOutcome.TooManyAttempts => "Too many incorrect attempts. Request a new code.",
		OtpVerificationOutcome.NoActiveOtp => "No active code found for this account. Request a new one.",
		_ => "Could not verify the code. Request a new one.",
	};
}