using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class UserAuthenticationService(
	IEmployeeRepository employeeRepository,
	IPasswordHasher passwordHasher,
	IOtpService otpService,
	IJwtTokenService jwtTokenService) : IUserAuthenticationService
{
	public async Task<LoginResult?> LoginAsync(string employeeCode, string password, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeOrEmailAsync(employeeCode, ct);

		if (employee is null || employee.PasswordHash is null || !passwordHasher.Verify(password, employee.PasswordHash))
			return null;

		if (employee.MustChangePassword)
		{
			if (string.IsNullOrWhiteSpace(employee.Email))
				throw new BusinessRuleException($"{employee.EmployeeCode} has no email on file — contact an administrator to add one before granting portal access.");

			await otpService.RequestAsync(employee.EmployeeId, employee.Email, OtpPurpose.FirstLogin, ct);
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

		if (string.IsNullOrWhiteSpace(employee.Email))
			throw new BusinessRuleException($"{employee.EmployeeCode} has no email on file — contact an administrator to add one before granting portal access.");

		await otpService.ResendAsync(employee.EmployeeId, employee.Email, OtpPurpose.FirstLogin, ct);
	}

	private static string DescribeOtpFailure(OtpVerificationOutcome outcome) => outcome switch
	{
		OtpVerificationOutcome.InvalidCode => "That code is incorrect. Please check your email and try again.",
		OtpVerificationOutcome.Expired => "That code has expired. Request a new one.",
		OtpVerificationOutcome.TooManyAttempts => "Too many incorrect attempts. Request a new code.",
		OtpVerificationOutcome.NoActiveOtp => "No active code found for this account. Request a new one.",
		_ => "Could not verify the code. Request a new one.",
	};

	public async Task RequestPasswordResetAsync(string identifier, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeOrEmailAsync(identifier, ct);

		if (employee is null || employee.PasswordHash is null || string.IsNullOrWhiteSpace(employee.Email))
			return;

		await otpService.RequestAsync(employee.EmployeeId, employee.Email, OtpPurpose.ForgotPassword, ct);
	}

	public async Task ResendPasswordResetOtpAsync(string identifier, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeOrEmailAsync(identifier, ct);
		if (employee is null || employee.PasswordHash is null || string.IsNullOrWhiteSpace(employee.Email))
			return;
		if (!employee.IsActive)
			return; // same generic failure as wrong credentials - don't reveal deactivation status
		await otpService.ResendAsync(employee.EmployeeId, employee.Email, OtpPurpose.ForgotPassword, ct);
	}
	public async Task<LoginResult> ResetPasswordAsync(
		string identifier, string otpCode, string newPassword, string confirmNewPassword, CancellationToken ct = default)
	{
		var employee = await employeeRepository.GetByCodeOrEmailAsync(identifier, ct)
			?? throw new BusinessRuleException(DescribeOtpFailure(OtpVerificationOutcome.NoActiveOtp));

		var passwordErrors = PasswordPolicy.Validate(newPassword, confirmNewPassword);
		if (passwordErrors.Count > 0)
			throw new BusinessRuleException(string.Join(" ", passwordErrors));

		var outcome = await otpService.VerifyAsync(employee.EmployeeId, OtpPurpose.ForgotPassword, otpCode, ct);
		if (outcome != OtpVerificationOutcome.Success)
			throw new BusinessRuleException(DescribeOtpFailure(outcome));

		employee.PasswordHash = passwordHasher.Hash(newPassword);
		employee.MustChangePassword = false;
		await employeeRepository.SaveChangesAsync(ct);

		var isAdmin = await employeeRepository.HasRoleAsync(employee.EmployeeId, "ADMIN", ct);
		var issued = jwtTokenService.GenerateToken(employee.EmployeeCode, isAdmin);
		return new LoginResult(RequiresOtpVerification: false, issued.Token, issued.ExpiresAtUtc, employee.EmployeeCode, employee.FullName);
	}
}