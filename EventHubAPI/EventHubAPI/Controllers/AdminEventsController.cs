using EventHubAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Dto;
using Services.Interfaces;
using Storage.Entities;

namespace EventHubAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/events")]
public class AdminEventsController : ControllerBase
{
	private readonly IEventService _events;

	public AdminEventsController(IEventService events)
	{
		_events = events;
	}

	[HttpGet]
	public async Task<ActionResult<PagedResult<EventResponse>>> GetAll(
		[FromQuery] EventSearchRequest request,
		CancellationToken cancellationToken)
	{
		var page = await _events.GetAdminEventsAsync(request.ToFilter(), cancellationToken);
		return Ok(new PagedResult<EventResponse>(page.Items.Select(EventResponse.From).ToList(),
			page.Page, page.PageSize, page.TotalCount, page.HasNextPage));
	}

	[HttpGet("{eventId:guid}")]
	public async Task<ActionResult<EventResponse>> GetById(Guid eventId, CancellationToken cancellationToken)
	{
		return Ok(EventResponse.From(await _events.GetEventByIdAsync(eventId, cancellationToken)));
	}

	[HttpPost]
	public async Task<ActionResult<EventResponse>> Create(EventDto request, CancellationToken cancellationToken)
	{
		var item = await _events.CreateEventAsync(request, cancellationToken);
		return CreatedAtAction(nameof(GetById), new { eventId = item.Id }, EventResponse.From(item));
	}

	[HttpPut("{eventId:guid}")]
	public async Task<ActionResult<EventResponse>> Update(Guid eventId, EventDto request, CancellationToken cancellationToken)
	{
		await _events.UpdateEventAsync(eventId, request, cancellationToken);
		return Ok(EventResponse.From(await _events.GetEventByIdAsync(eventId, cancellationToken)));
	}

	[HttpPut("{eventId:guid}/tags")]
	public async Task<ActionResult<EventResponse>> ConfirmTags(
		Guid eventId, TagIdsRequest request, CancellationToken cancellationToken)
	{
		await _events.ConfirmEventTagsAsync(eventId, request.TagIds, cancellationToken);
		return Ok(EventResponse.From(await _events.GetEventByIdAsync(eventId, cancellationToken)));
	}

	[HttpPost("{eventId:guid}/publish")]
	public async Task<ActionResult<EventResponse>> Publish(Guid eventId, CancellationToken cancellationToken)
	{
		await _events.ChangeEventStatusAsync(eventId, EventStatus.Published, cancellationToken);
		return Ok(EventResponse.From(await _events.GetEventByIdAsync(eventId, cancellationToken)));
	}

	[HttpPost("{eventId:guid}/unpublish")]
	public async Task<IActionResult> Unpublish(Guid eventId, CancellationToken cancellationToken)
	{
		await _events.UnpublishEventAsync(eventId, cancellationToken);
		return NoContent();
	}
}
