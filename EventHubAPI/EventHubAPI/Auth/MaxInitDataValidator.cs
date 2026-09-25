using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace EventHubAPI.Auth;

public class MaxInitDataValidator
{
	private readonly IConfiguration _configuration;

	public MaxInitDataValidator(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public long? Validate(string? initData)
	{
		if (string.IsNullOrWhiteSpace(initData))
		{
			return null;
		}

		var botToken = _configuration["Max:BotToken"]
			?? throw new InvalidOperationException("Max:BotToken не задан.");
		var parameters = QueryHelpers.ParseQuery("?" + initData);
		if (parameters.Count == 0 || parameters.Any(p => p.Value.Count != 1) ||
		    !parameters.TryGetValue("hash", out var hashValue) ||
		    !parameters.TryGetValue("auth_date", out var authDateValue) ||
		    !parameters.TryGetValue("user", out var userValue))
		{
			return null;
		}

		if (!long.TryParse(authDateValue.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var unixTime))
		{
			return null;
		}

		DateTimeOffset issuedAt;
		try
		{
			issuedAt = DateTimeOffset.FromUnixTimeSeconds(unixTime);
		}
		catch (ArgumentOutOfRangeException)
		{
			return null;
		}

		var now = DateTimeOffset.UtcNow;
		if (issuedAt > now.AddMinutes(1) || issuedAt < now.AddHours(-24))
		{
			return null;
		}

		byte[] providedHash;
		try
		{
			providedHash = Convert.FromHexString(hashValue.ToString());
		}
		catch (FormatException)
		{
			return null;
		}

		var dataCheckString = string.Join("\n", parameters
			.Where(p => p.Key != "hash")
			.OrderBy(p => p.Key, StringComparer.Ordinal)
			.Select(p => $"{p.Key}={p.Value}"));
		var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
		var actualHash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
		if (!CryptographicOperations.FixedTimeEquals(actualHash, providedHash))
		{
			return null;
		}

		try
		{
			using var userJson = JsonDocument.Parse(userValue.ToString());
			return userJson.RootElement.GetProperty("id").GetInt64();
		}
		catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
		{
			return null;
		}
	}
}
