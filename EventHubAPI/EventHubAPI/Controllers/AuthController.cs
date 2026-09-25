using EventHubAPI.Auth;
using EventHubAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace EventHubAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
	[HttpPost("max")]
	public async Task<ActionResult<TokenResponse>> LoginWithMax(
		MaxLoginRequest request,
		[FromServices] MaxInitDataValidator validator,
		[FromServices] IUserService users,
		[FromServices] TokenService tokens,
		CancellationToken cancellationToken)
	{
		var maxUserId = validator.Validate(request.InitData);
		if (maxUserId is null)
		{
			return Unauthorized();
		}

		var user = await users.CreateByMaxUserIdAsync(maxUserId.Value, cancellationToken);
		var (token, expiresAt) = tokens.Create(user.Id.ToString(), "User");
		return Ok(new TokenResponse(token, expiresAt));
	}
}
