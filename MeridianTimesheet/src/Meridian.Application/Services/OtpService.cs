using Meridian.Application.Common;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Meridian.Application.Services;

public class OtpService(
	IOtpRepository otpRepository,
	IOtpGenerator otpGenerator,
	IEmailSender emailSender,
	IOptions<OtpSettings> otpSettingsOptions) : IOtpService
{
	private readonly OtpSettings settings = otpSettingsOptions.Value;

	public async Task RequestAsync(int employeeId, string email, OtpPurpose purpose, CancellationToken ct = default)
	{
		var mostRecent = await otpRepository.GetMostRecentAsync(employeeId, purpose, ct);
		if (IsStillActive(mostRecent))
			return; // a valid code is already outstanding — don't send a second email for it

		await IssueNewOtpAsync(employeeId, email, purpose, ct);
	}

	public async Task ResendAsync(int employeeId, string email, OtpPurpose purpose, CancellationToken ct = default)
	{
		var mostRecent = await otpRepository.GetMostRecentAsync(employeeId, purpose, ct);
		if (mostRecent is not null)
		{
			var secondsSinceIssued = (DateTime.UtcNow - mostRecent.CreatedAt).TotalSeconds;
			var remaining = settings.ResendCooldownSeconds - (int)secondsSinceIssued;
			if (remaining > 0)
				throw new BusinessRuleException($"Please wait {remaining}s before requesting another code.");
		}

		await IssueNewOtpAsync(employeeId, email, purpose, ct);
	}

	public async Task<OtpVerificationOutcome> VerifyAsync(int employeeId, OtpPurpose purpose, string plainCode, CancellationToken ct = default)
	{
		var otp = await otpRepository.GetMostRecentAsync(employeeId, purpose, ct);
		if (otp is null || otp.IsUsed || otp.InvalidatedAt is not null)
			return OtpVerificationOutcome.NoActiveOtp;

		if (otp.ExpiresAt < DateTime.UtcNow)
			return OtpVerificationOutcome.Expired;

		if (otp.AttemptCount >= settings.MaxVerificationAttempts)
		{
			// Burn the OTP once the attempt budget is spent — the only way
			// forward at that point is requesting a fresh one.
			otp.InvalidatedAt = DateTime.UtcNow;
			await otpRepository.SaveChangesAsync(ct);
			return OtpVerificationOutcome.TooManyAttempts;
		}

		otp.AttemptCount++;

		if (!otpGenerator.Verify(plainCode, otp.OtpHash))
		{
			await otpRepository.SaveChangesAsync(ct); // persist the incremented attempt count even on failure
			return OtpVerificationOutcome.InvalidCode;
		}

		otp.IsUsed = true;
		await otpRepository.SaveChangesAsync(ct);
		return OtpVerificationOutcome.Success;
	}

	private async Task IssueNewOtpAsync(int employeeId, string email, OtpPurpose purpose, CancellationToken ct)
	{
		// At most one OTP is ever valid per employee/purpose — invalidate
		// whatever was outstanding before creating the new one.
		var previous = await otpRepository.GetMostRecentAsync(employeeId, purpose, ct);
		if (IsStillActive(previous))
			previous!.InvalidatedAt = DateTime.UtcNow;

		var generated = otpGenerator.Generate();
		var otp = new Otp
		{
			EmployeeId = employeeId,
			Purpose = purpose,
			OtpHash = generated.Hash,
			ExpiresAt = DateTime.UtcNow.AddMinutes(settings.ExpiryMinutes),
			CreatedAt = DateTime.UtcNow,
		};
		await otpRepository.AddAsync(otp, ct);
		await otpRepository.SaveChangesAsync(ct);

		var subject = purpose == OtpPurpose.FirstLogin ? "Your Meridian verification code" : "Your Meridian password reset code";
		var body = $"Your one-time code is {generated.PlainCode}. It expires in {settings.ExpiryMinutes} minutes. " +
				   "If you didn't request this, you can safely ignore this email.";
		await emailSender.SendAsync(email, subject, body, ct);
	}

	private static bool IsStillActive(Otp? otp) =>
		otp is not null && !otp.IsUsed && otp.InvalidatedAt is null && otp.ExpiresAt >= DateTime.UtcNow;
}