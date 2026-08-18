using System.Security.Cryptography;
using Meridian.Application.Interfaces.Services;

namespace Meridian.Application.Services;

public class PasswordHasher : IPasswordHasher
{
	private const int Iterations = 210_000; // OWASP 2023 recommendation for PBKDF2-SHA256
	private const int SaltSizeBytes = 16;
	private const int HashSizeBytes = 32;

	public string Hash(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
		var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
		return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
	}

	public bool Verify(string password, string storedHash)
	{
		var parts = storedHash.Split('.');
		if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
			return false;

		var salt = Convert.FromBase64String(parts[1]);
		var expectedHash = Convert.FromBase64String(parts[2]);
		var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

		return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
	}
}