namespace Meridian.Application.Common;

/// <summary>
/// A starting password policy — adjust the rule set here if the real
/// organizational policy differs. Kept as one small, pure, static method
/// so both the first-login and forgot-password flows enforce identical rules.
/// </summary>
public static class PasswordPolicy
{
	public const int MinimumLength = 8;
	public const string DefaultPassword = "cbt@2026";

	public static List<string> Validate(string newPassword, string confirmNewPassword)
	{
		var errors = new List<string>();

		if (newPassword != confirmNewPassword)
			errors.Add("New password and confirmation do not match.");

		if (string.Equals(newPassword, DefaultPassword, StringComparison.Ordinal))
			errors.Add("New password cannot be the default password.");

		if (newPassword.Length < MinimumLength)
			errors.Add($"Password must be at least {MinimumLength} characters.");

		if (!newPassword.Any(char.IsLetter))
			errors.Add("Password must contain at least one letter.");

		if (!newPassword.Any(char.IsDigit))
			errors.Add("Password must contain at least one number.");

		return errors;
	}
}