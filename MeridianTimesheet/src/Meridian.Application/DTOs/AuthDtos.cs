namespace Meridian.Application.DTOs;

//public record LoginRequest(string EmployeeCode, string Password);

/// <summary>RequiresOtpVerification=true means Token/ExpiresAtUtc are null —
/// the frontend must show the OTP-and-change-password screen next rather
/// than treating this as a completed login.</summary>
public record LoginResult(
	bool RequiresOtpVerification,
	string? Token,
	DateTime? ExpiresAtUtc,
	string EmployeeCode,
	string FullName
);

public record VerifyFirstLoginOtpRequest(string EmployeeCode, string OtpCode, string NewPassword, string ConfirmNewPassword);

public record ResendOtpRequest(string EmployeeCode);

public record RequestPasswordResetRequest(string Identifier);
public record ResetPasswordRequest(string Identifier, string OtpCode, string NewPassword, string ConfirmNewPassword);