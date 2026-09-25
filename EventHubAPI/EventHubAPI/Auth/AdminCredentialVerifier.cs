using Microsoft.AspNetCore.Identity;

namespace EventHubAPI.Auth;

public class AdminCredentialVerifier
{
	private readonly IConfiguration _configuration;
	private readonly PasswordHasher<string> _hasher = new();

	public AdminCredentialVerifier(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public bool Verify(string? username, string? password)
	{
		var configuredUsername = _configuration["Admin:Username"];
		var configuredHash = _configuration["Admin:PasswordHash"];
		if (string.IsNullOrWhiteSpace(configuredUsername) || string.IsNullOrWhiteSpace(configuredHash))
		{
			throw new InvalidOperationException("Admin:Username и Admin:PasswordHash должны быть заданы.");
		}

		return username == configuredUsername && password is not null &&
		       _hasher.VerifyHashedPassword(configuredUsername, configuredHash, password) != PasswordVerificationResult.Failed;
	}
}
