using Services.Dto;
using Storage.Entities;

namespace Services.Interfaces;

public interface IEventService
{
	public Task<Event> CreateEventAsync(
		EventDto eventDto,
		CancellationToken cancellationToken);

	public Task<Event> GetEventByIdAsync(
		Guid eventId,
		CancellationToken cancellationToken,
		EventStatus? eventStatus = null);

	public Task<PagedResult<Event>> GetEventsAsync(
		int page,
		int pageSize,
		IReadOnlyCollection<Guid> tags,
		CancellationToken cancellationToken);

	public Task<PagedResult<Event>> SearchPublishedEventsAsync(
		EventSearchFilter filter,
		CancellationToken cancellationToken);

	public Task<PagedResult<Event>> GetAdminEventsAsync(
		EventSearchFilter filter,
		CancellationToken cancellationToken);

	public Task<Event> ConfirmEventTagsAsync(
		Guid eventId,
		IReadOnlyCollection<Guid> tagIds,
		CancellationToken cancellationToken);

	public Task<Event> UpdateEventAsync(
		Guid eventId,
		EventDto eventDto,
		CancellationToken cancellationToken);

	public Task<Event> ChangeEventStatusAsync(Guid eventId,
		EventStatus eventStatus,
		CancellationToken cancellationToken);

	public Task UnpublishEventAsync(
		Guid eventId,
		CancellationToken cancellationToken);
}
