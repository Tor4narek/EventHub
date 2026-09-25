using EventHubAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Services.Dto;
using Services.Interfaces;
using Storage.Entities;

namespace EventHubAPI.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
	private readonly IEventService _events;

	public EventsController(IEventService events)
	{
		_events = events;
	}

	[HttpGet]
	public async Task<ActionResult<PagedResult<EventResponse>>> GetAll(
		[FromQuery] EventSearchRequest request,
		CancellationToken cancellationToken)
	{
		var page = await _events.SearchPublishedEventsAsync(request.ToFilter(), cancellationToken);
		return Ok(new PagedResult<EventResponse>(page.Items.Select(EventResponse.From).ToList(),
			page.Page, page.PageSize, page.TotalCount, page.HasNextPage));
	}

	[HttpGet("{eventId:guid}")]
	public async Task<ActionResult<EventResponse>> GetById(Guid eventId, CancellationToken cancellationToken)
	{
		var item = await _events.GetEventByIdAsync(eventId, cancellationToken, EventStatus.Published);
		return Ok(EventResponse.From(item));
	}
}
