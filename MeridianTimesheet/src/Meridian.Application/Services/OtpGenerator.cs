using System.Security.Cryptography;
using System.Text;
using Meridian.Application.Interfaces.Services;

namespace Meridian.Application.Services;

public class OtpGenerator : IOtpGenerator
{
	private const int CodeLength = 6;

	public GeneratedOtp Generate()
	{
		var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeLength)).ToString($"D{CodeLength}");
		return new GeneratedOtp(code, HashCode(code));
	}

	public bool Verify(string plainCode, string storedHash) =>
		CryptographicOperations.FixedTimeEquals(
			Encoding.UTF8.GetBytes(HashCode(plainCode)),
			Encoding.UTF8.GetBytes(storedHash));

	private static string HashCode(string code)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
		return Convert.ToBase64String(bytes);
	}
}