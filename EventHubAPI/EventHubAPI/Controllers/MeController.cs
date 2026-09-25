using System.IdentityModel.Tokens.Jwt;
using EventHubAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace EventHubAPI.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[Route("api/me")]
public class MeController : ControllerBase
{
	private readonly IUserService _users;
	private readonly IUserEventService _savedEvents;
	private readonly IRecomendationService _recommendations;

	public MeController(IUserService users, IUserEventService savedEvents, IRecomendationService recommendations)
	{
		_users = users;
		_savedEvents = savedEvents;
		_recommendations = recommendations;
	}

	private Guid UserId => Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

	[HttpGet]
	public async Task<IActionResult> Get(CancellationToken cancellationToken)
	{
		var user = await _users.GetByIdAsync(UserId, cancellationToken);
		var tagIds = await _users.GetUserTagIdsAsync(UserId, cancellationToken);
		return Ok(new { user!.Id, user.MaxUserId, user.IsWeeklyDigestEnabled, TagIds = tagIds });
	}

	[HttpPut("interests")]
	public async Task<IActionResult> UpdateInterests(TagIdsRequest request, CancellationToken cancellationToken)
	{
		await _users.UpdateUserTagsAsync(UserId, request.TagIds, cancellationToken);
		return NoContent();
	}

	[HttpGet("recommendations")]
	public async Task<ActionResult<IReadOnlyList<EventResponse>>> GetRecommendations(
		[FromQuery] int limit = 3,
		CancellationToken cancellationToken = default)
	{
		if (limit is < 1 or > 20)
		{
			return BadRequest("limit должен быть от 1 до 20.");
		}

		var now = DateTime.UtcNow;
		var events = await _recommendations.GetTopEventsAsync(UserId, limit, now, now.AddDays(7), cancellationToken);
		return Ok(events.Select(EventResponse.From).ToList());
	}

	[HttpGet("saved-events")]
	public async Task<ActionResult<IReadOnlyList<EventResponse>>> GetSaved(CancellationToken cancellationToken)
	{
		var events = await _savedEvents.GetSavedEventsAsync(UserId, cancellationToken);
		return Ok(events.Select(EventResponse.From).ToList());
	}

	[HttpPost("saved-events/{eventId:guid}")]
	public async Task<IActionResult> Save(Guid eventId, CancellationToken cancellationToken)
	{
		await _savedEvents.SaveEventAsync(UserId, eventId, cancellationToken);
		return NoContent();
	}

	[HttpDelete("saved-events/{eventId:guid}")]
	public async Task<IActionResult> Remove(Guid eventId, CancellationToken cancellationToken)
	{
		await _savedEvents.RemoveEventAsync(UserId, eventId, cancellationToken);
		return NoContent();
	}

	[HttpPatch("settings")]
	public async Task<IActionResult> UpdateSettings(SettingsRequest request, CancellationToken cancellationToken)
	{
		await _users.SetWeeklyDigestAsync(UserId, request.IsWeeklyDigestEnabled, cancellationToken);
		return NoContent();
	}
}
