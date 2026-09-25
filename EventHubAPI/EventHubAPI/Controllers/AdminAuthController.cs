using EventHubAPI.Auth;
using EventHubAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventHubAPI.Controllers;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
	[HttpPost("login")]
	public ActionResult<TokenResponse> Login(
		AdminLoginRequest request,
		[FromServices] AdminCredentialVerifier credentials,
		[FromServices] TokenService tokens)
	{
		if (!credentials.Verify(request.Username, request.Password))
		{
			return Unauthorized();
		}

		var (token, expiresAt) = tokens.Create(request.Username, "Admin");
		return Ok(new TokenResponse(token, expiresAt));
	}
}
