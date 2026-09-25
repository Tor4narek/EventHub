using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EventHubAPI.Auth;

public class TokenService
{
	private readonly IConfiguration _configuration;

	public TokenService(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public (string Token, DateTime ExpiresAt) Create(string subject, string role)
	{
		var expiresAt = DateTime.UtcNow.AddHours(role == "Admin" ? 8 : 24);
		var key = _configuration["Jwt:SigningKey"]
			?? throw new InvalidOperationException("Jwt:SigningKey не задан.");
		var token = new JwtSecurityToken(
			issuer: _configuration["Jwt:Issuer"] ?? "EventHubAPI",
			audience: _configuration["Jwt:Audience"] ?? "EventHubClients",
			claims:
			[
				new Claim(JwtRegisteredClaimNames.Sub, subject),
				new Claim("role", role)
			],
			expires: expiresAt,
			signingCredentials: new SigningCredentials(
				new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
				SecurityAlgorithms.HmacSha256));

		return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
	}
}
